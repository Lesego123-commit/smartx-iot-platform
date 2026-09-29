// ------------------------------------------------------------------------------------
// PROG7312 - Programming 3B | Part 2: Smart-X Real-Time Processing & Device Management
// File:    SmartX.Api/Services/CommandStreamEngine.cs
// Purpose: Real-time processing engine. Manages volatile telemetry and device state with
//          the advanced collections the brief requires:
//            - Queue<T>            : FIFO ingestion of standard telemetry packets
//            - PriorityQueue<T,P>  : critical alerts that bypass the standard stream
//            - Stack<T>            : manual-command history for the Undo feature
//            - Dictionary<K,V>     : O(1) live device lookup by MAC / node id
//            - SortedDictionary<K,V>: per-device timeline ordered by timestamp
//            - HashSet<T>          : unique active error / disconnected node tracking
//          It also hosts the predictive pattern engine (Suggested Actions).
//
// References (Harvard) - sources consulted for the techniques used in this file:
//   - Microsoft, 2024. Queue<T> Class. Microsoft Learn. Available at: https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.queue-1 [Accessed 25 September 2026].
//   - Microsoft, 2024. PriorityQueue<TElement,TPriority> Class. Microsoft Learn. Available at: https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.priorityqueue-2 [Accessed 25 September 2026].
//   - Microsoft, 2024. Stack<T> Class. Microsoft Learn. Available at: https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.stack-1 [Accessed 25 September 2026].
//   - Microsoft, 2024. Dictionary<TKey,TValue> Class. Microsoft Learn. Available at: https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.dictionary-2 [Accessed 25 September 2026].
//   - Microsoft, 2024. SortedDictionary<TKey,TValue> Class. Microsoft Learn. Available at: https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.sorteddictionary-2 [Accessed 25 September 2026].
//   - Microsoft, 2024. HashSet<T> Class. Microsoft Learn. Available at: https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.hashset-1 [Accessed 25 September 2026].
//   - Cormen, T.H., Leiserson, C.E., Rivest, R.L. and Stein, C., 2009. Introduction to Algorithms. 3rd ed. Cambridge, MA: MIT Press.
//   - Albahari, J. and Albahari, B., 2022. C# 10 in a Nutshell: The Definitive Reference. Sebastopol: O'Reilly Media.
// ------------------------------------------------------------------------------------
using SmartX.Core;

namespace SmartX.Api.Services;

public sealed class CommandStreamEngine
{
    // Mutable per-device record held in the dictionary.
    private sealed class LiveDevice
    {
        public required string DeviceId { get; init; }
        public required string Location { get; init; }
        public SensorCategory Category { get; init; }
        public string PrimaryMetric { get; init; } = "";
        public double? LastValue { get; set; }
        public DateTimeOffset? LastSeen { get; set; }
        public DeviceAlertState AlertState { get; set; }
        public bool PumpOn { get; set; }
        public bool RelayOn { get; set; }
    }

    private readonly object _gate = new();

    // ---- Required data structures ------------------------------------------------------
    private readonly Queue<TelemetrySample> _standardStream = new();                       // FIFO
    private readonly PriorityQueue<TelemetrySample, int> _priorityStream = new();          // urgent first
    private readonly Stack<CommandRecord> _commandHistory = new();                          // undo
    private readonly Dictionary<string, LiveDevice> _devices = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, SortedDictionary<long, double>> _timeline =         // ordered logs
        new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _activeErrors = new(StringComparer.OrdinalIgnoreCase); // unique errors

    private long _standardProcessed;
    private long _priorityProcessed;

    // ---- Predictive engine state -------------------------------------------------------
    // conditionKey (e.g. "low:soil_moisture") -> how often each action followed it.
    private readonly Dictionary<string, Dictionary<CommandAction, int>> _patterns =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _searchHistory = new();
    // recent "device entered alert" events, newest last, capped.
    private readonly LinkedList<(string ConditionKey, string DeviceId, DateTimeOffset At)> _recentEvents = new();

    // ---- Registration / seeding --------------------------------------------------------
    public void Register(string deviceId, string location, SensorCategory category, string primaryMetric,
        double? value, DeviceAlertState state)
    {
        var id = MacAddress.IsValid(deviceId) ? MacAddress.Normalise(deviceId) : deviceId;
        lock (_gate)
        {
            _devices[id] = new LiveDevice
            {
                DeviceId = id,
                Location = location,
                Category = category,
                PrimaryMetric = primaryMetric,
                LastValue = value,
                LastSeen = state == DeviceAlertState.Disconnected ? DateTimeOffset.UtcNow.AddMinutes(-15)
                                                                  : DateTimeOffset.UtcNow,
                AlertState = state,
                PumpOn = false,
                RelayOn = category == SensorCategory.Actuator
            };
            if (!_timeline.ContainsKey(id))
                _timeline[id] = new SortedDictionary<long, double>();
            if (state is DeviceAlertState.Critical or DeviceAlertState.Disconnected)
                _activeErrors.Add(id);
        }
    }

    /// <summary>Teach the engine an association (a command that historically follows a condition).</summary>
    public void SeedPattern(string conditionKey, CommandAction action, int weight = 1)
    {
        lock (_gate)
        {
            if (!_patterns.TryGetValue(conditionKey, out var map))
                _patterns[conditionKey] = map = new Dictionary<CommandAction, int>();
            map[action] = map.GetValueOrDefault(action) + weight;
        }
    }

    // ---- Ingestion (Queue + PriorityQueue) ---------------------------------------------
    /// <summary>
    /// Accept a telemetry packet. Critical packets are pushed onto the priority queue so
    /// they bypass the standard FIFO stream; everything else joins the standard queue.
    /// </summary>
    public void Ingest(TelemetrySample sample, bool critical)
    {
        lock (_gate)
        {
            if (critical)
                _priorityStream.Enqueue(sample, 0);   // 0 = most urgent
            else
                _standardStream.Enqueue(sample);

            var id = MacAddress.IsValid(sample.DeviceId) ? MacAddress.Normalise(sample.DeviceId) : sample.DeviceId;
            if (_devices.TryGetValue(id, out var dev))
            {
                dev.LastValue = sample.NumericValue ?? dev.LastValue;
                dev.LastSeen = sample.Timestamp;
                if (sample.NumericValue is { } v)
                    _timeline[id][sample.Timestamp.UtcTicks] = v;   // SortedDictionary keeps it ordered

                if (critical)
                {
                    dev.AlertState = DeviceAlertState.Critical;
                    _activeErrors.Add(id);                          // HashSet dedupes repeat alerts
                    RecordEvent(ConditionKey(dev), id);
                }
            }
        }
    }

    /// <summary>Process the next packet: priority queue first, then the FIFO stream.</summary>
    public TelemetrySample? ProcessNext()
    {
        lock (_gate)
        {
            if (_priorityStream.Count > 0)
            {
                _priorityProcessed++;
                return _priorityStream.Dequeue();
            }
            if (_standardStream.Count > 0)
            {
                _standardProcessed++;
                return _standardStream.Dequeue();
            }
            return null;
        }
    }

    public int ProcessAll()
    {
        int n = 0;
        while (ProcessNext() is not null) n++;
        return n;
    }

    // ---- Manual override commands (Stack) ----------------------------------------------
    public CommandRecord IssueCommand(DeviceCommand cmd)
    {
        lock (_gate)
        {
            var id = MacAddress.IsValid(cmd.DeviceId) ? MacAddress.Normalise(cmd.DeviceId) : cmd.DeviceId;
            if (!_devices.TryGetValue(id, out var dev))
                throw new KeyNotFoundException($"Device '{cmd.DeviceId}' is not on the stream.");

            ApplyAction(dev, cmd.Action, undo: false);
            var record = new CommandRecord(
                Guid.NewGuid().ToString("N")[..8], id, cmd.Action,
                DateTimeOffset.UtcNow, Describe(cmd.Action, id), false);
            _commandHistory.Push(record);       // Stack: newest on top for undo

            LearnFromCommand(id, cmd.Action);   // predictive: link recent conditions -> this action
            return record;
        }
    }

    /// <summary>Undo the most recent command by popping the history stack and reverting it.</summary>
    public CommandRecord? UndoLast()
    {
        lock (_gate)
        {
            while (_commandHistory.Count > 0)
            {
                var top = _commandHistory.Pop();
                if (top.IsUndone) continue;
                var id = top.DeviceId;
                if (_devices.TryGetValue(id, out var dev))
                    ApplyAction(dev, top.Action, undo: true);
                var undone = top with { IsUndone = true };
                _commandHistory.Push(undone);   // keep it in history, marked undone
                return undone;
            }
            return null;
        }
    }

    public IReadOnlyList<CommandRecord> CommandHistory(int limit = 20)
    {
        lock (_gate) return _commandHistory.Take(limit <= 0 ? 20 : limit).ToList(); // Stack enumerates top-first
    }

    // ---- Dictionary lookup + search ----------------------------------------------------
    public IReadOnlyList<DeviceStateDto> Devices()
    {
        lock (_gate) return _devices.Values.OrderBy(d => d.Location).Select(ToDto).ToList();
    }

    public DeviceStateDto? Device(string id)
    {
        var key = MacAddress.IsValid(id) ? MacAddress.Normalise(id) : id;
        lock (_gate) return _devices.TryGetValue(key, out var d) ? ToDto(d) : null;   // O(1) lookup
    }

    /// <summary>
    /// Search / filter devices by category and/or alert state. Each search term is logged so
    /// the predictive engine can learn what the operator tends to look for.
    /// </summary>
    public IReadOnlyList<DeviceStateDto> Search(string? category, string? alertState)
    {
        lock (_gate)
        {
            if (!string.IsNullOrWhiteSpace(category)) _searchHistory.Add($"cat:{category}");
            if (!string.IsNullOrWhiteSpace(alertState)) _searchHistory.Add($"alert:{alertState}");

            IEnumerable<LiveDevice> q = _devices.Values;
            if (Enum.TryParse<SensorCategory>(category, true, out var cat))
                q = q.Where(d => d.Category == cat);
            if (Enum.TryParse<DeviceAlertState>(alertState, true, out var st))
                q = q.Where(d => d.AlertState == st);
            return q.OrderBy(d => d.Location).Select(ToDto).ToList();
        }
    }

    // ---- SortedDictionary timeline -----------------------------------------------------
    public IReadOnlyList<TimelinePoint> Timeline(string deviceId, int limit = 60)
    {
        var key = MacAddress.IsValid(deviceId) ? MacAddress.Normalise(deviceId) : deviceId;
        lock (_gate)
        {
            if (!_timeline.TryGetValue(key, out var series)) return Array.Empty<TimelinePoint>();
            // SortedDictionary already yields keys in ascending timestamp order.
            return series.Select(kv => new TimelinePoint(new DateTimeOffset(kv.Key, TimeSpan.Zero), kv.Value))
                         .TakeLast(limit <= 0 ? 60 : limit).ToList();
        }
    }

    // ---- HashSet active errors ---------------------------------------------------------
    public IReadOnlyList<string> ActiveErrors()
    {
        lock (_gate) return _activeErrors.ToList();
    }

    public bool ClearError(string deviceId)
    {
        var key = MacAddress.IsValid(deviceId) ? MacAddress.Normalise(deviceId) : deviceId;
        lock (_gate)
        {
            if (_devices.TryGetValue(key, out var dev) && dev.AlertState != DeviceAlertState.Disconnected)
                dev.AlertState = DeviceAlertState.Normal;
            return _activeErrors.Remove(key);
        }
    }

    // ---- Predictive action engine ------------------------------------------------------
    /// <summary>
    /// Build "Suggested Actions" for devices currently in an alert state, using the learned
    /// association between a condition (e.g. low soil moisture) and the command the operator
    /// usually issues in response. Also surfaces out-of-range devices as automated insights.
    /// </summary>
    public IReadOnlyList<Insight> Insights()
    {
        lock (_gate)
        {
            var insights = new List<Insight>();
            foreach (var dev in _devices.Values)
            {
                if (dev.AlertState is DeviceAlertState.Normal) continue;

                if (dev.AlertState == DeviceAlertState.Disconnected)
                {
                    insights.Add(new Insight(
                        "Automated insight",
                        $"{Short(dev.DeviceId)} at {dev.Location} has stopped reporting - investigate the node.",
                        dev.DeviceId, CommandAction.ResetNode, AlertSeverity.Critical));
                    continue;
                }

                var key = ConditionKey(dev);
                if (_patterns.TryGetValue(key, out var counts) && counts.Count > 0)
                {
                    var best = counts.OrderByDescending(kv => kv.Value).First().Key;
                    insights.Add(new Insight(
                        "Suggested action",
                        $"{Short(dev.DeviceId)} - {dev.PrimaryMetric} is {dev.AlertState.ToString().ToLower()} " +
                        $"({dev.LastValue:0.#}). You usually {Describe(best, dev.DeviceId).ToLower()} in this case.",
                        dev.DeviceId, best,
                        dev.AlertState == DeviceAlertState.Critical ? AlertSeverity.Critical : AlertSeverity.Warning));
                }
                else
                {
                    insights.Add(new Insight(
                        "Automated insight",
                        $"{Short(dev.DeviceId)} - {dev.PrimaryMetric} reading is out of range ({dev.LastValue:0.#}).",
                        dev.DeviceId, null,
                        dev.AlertState == DeviceAlertState.Critical ? AlertSeverity.Critical : AlertSeverity.Warning));
                }
            }
            // Most urgent first.
            return insights.OrderByDescending(i => i.Severity).ToList();
        }
    }

    public StreamStatus Status()
    {
        lock (_gate)
        {
            return new StreamStatus(
                _standardStream.Count, _priorityStream.Count,
                _standardProcessed, _priorityProcessed,
                _activeErrors.Count, _commandHistory.Count(c => !c.IsUndone),
                _devices.Count);
        }
    }

    // ---- helpers -----------------------------------------------------------------------
    private void RecordEvent(string conditionKey, string deviceId)
    {
        _recentEvents.AddLast((conditionKey, deviceId, DateTimeOffset.UtcNow));
        while (_recentEvents.Count > 25) _recentEvents.RemoveFirst();
    }

    private void LearnFromCommand(string deviceId, CommandAction action)
    {
        // Link this command to any recent alert condition on the same device.
        foreach (var ev in _recentEvents)
        {
            if (!string.Equals(ev.DeviceId, deviceId, StringComparison.OrdinalIgnoreCase)) continue;
            if (!_patterns.TryGetValue(ev.ConditionKey, out var map))
                _patterns[ev.ConditionKey] = map = new Dictionary<CommandAction, int>();
            map[action] = map.GetValueOrDefault(action) + 1;
        }
    }

    private static string ConditionKey(LiveDevice dev) => $"low:{dev.PrimaryMetric}";

    private static void ApplyAction(LiveDevice dev, CommandAction action, bool undo)
    {
        switch (action)
        {
            case CommandAction.TriggerPump: dev.PumpOn = !undo; break;
            case CommandAction.StopPump: dev.PumpOn = undo; break;
            case CommandAction.ToggleRelay: dev.RelayOn = !dev.RelayOn; break;
            case CommandAction.OpenValve: dev.RelayOn = !undo; break;
            case CommandAction.ShutValve: dev.RelayOn = undo; break;
            case CommandAction.AcknowledgeAlert:
                if (!undo && dev.AlertState != DeviceAlertState.Disconnected)
                    dev.AlertState = DeviceAlertState.Normal;
                break;
            case CommandAction.ResetNode:
                if (!undo) { dev.PumpOn = false; dev.RelayOn = false; }
                break;
        }
    }

    private static string Describe(CommandAction a, string id) => a switch
    {
        CommandAction.TriggerPump => $"Trigger pump on {Short(id)}",
        CommandAction.StopPump => $"Stop pump on {Short(id)}",
        CommandAction.ToggleRelay => $"Toggle relay on {Short(id)}",
        CommandAction.OpenValve => $"Open valve on {Short(id)}",
        CommandAction.ShutValve => $"Shut valve on {Short(id)}",
        CommandAction.ResetNode => $"Reset node {Short(id)}",
        CommandAction.AcknowledgeAlert => $"Acknowledge alert on {Short(id)}",
        _ => a.ToString()
    };

    private static string Short(string id)
    {
        var parts = id.Split(':');
        return parts.Length >= 2 ? $"{parts[^2]}:{parts[^1]}" : id;
    }

    private static DeviceStateDto ToDto(LiveDevice d) => new(
        d.DeviceId, d.Location, d.Category, d.PrimaryMetric, d.LastValue, d.LastSeen,
        d.AlertState, d.PumpOn, d.RelayOn);
}
