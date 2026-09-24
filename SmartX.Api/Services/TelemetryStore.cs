// ------------------------------------------------------------------------------------
// PROG7312 - Programming 3B | Part 1: Smart-X Data Ingestion & Validation Gateway
// File:    SmartX.Api/Services/TelemetryStore.cs
// Purpose: In-memory ingestion engine: typed storage, anomaly detection, snapshots.
//
// References (Harvard) - sources consulted for the techniques used in this file:
//   - Microsoft, 2024. List<T> Class. Microsoft Learn. Available at: https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.list-1 [Accessed 10 September 2026].
//   - Microsoft, 2024. Generic classes and methods (C# Programming Guide). Microsoft Learn. Available at: https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/generics/ [Accessed 10 September 2026].
//   - Albahari, J. and Albahari, B., 2022. C# 10 in a Nutshell: The Definitive Reference. Sebastopol: O'Reilly Media.
// ------------------------------------------------------------------------------------
using SmartX.Core;

namespace SmartX.Api.Services;

/// <summary>
/// Thread-safe, in-memory processing engine for the Smart-X gateway. It registers
/// devices, ingests strongly-typed telemetry via <see cref="TelemetryPacket{T}"/>,
/// flags anomalies (spikes and sensor disconnects) and rolls everything up into the
/// snapshot that powers the dashboard's dynamic engagement strip.
///
/// A database is intentionally out of scope for this demo (per the brief) — the store
/// is heavily seeded with mock telemetry so the data structures can be exercised under load.
/// </summary>
public sealed class TelemetryStore
{
    private sealed record StoredSample(ITelemetryPacket Packet, double? Numeric, bool IsAnomaly);

    private readonly object _gate = new();
    private readonly Dictionary<string, SensorProfile> _devices = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<StoredSample>> _samples = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<AlertDto> _alerts = new();

    private static readonly TimeSpan SilenceThreshold = TimeSpan.FromMinutes(4);

    // ---- Registration ------------------------------------------------------------------
    public SensorProfile Register(RegisterSensorRequest req)
    {
        if (!MacAddress.IsValid(req.Mac))
            throw new ArgumentException($"'{req.Mac}' is not a valid MAC address / device identifier.");

        var id = MacAddress.Normalise(req.Mac);
        lock (_gate)
        {
            var profile = new SensorProfile(
                Id: id,
                Mac: id,
                Location: string.IsNullOrWhiteSpace(req.Location) ? "unassigned" : req.Location.Trim(),
                Category: req.Category,
                Description: req.Description,
                RegisteredAt: DateTimeOffset.UtcNow,
                AttachmentName: null,
                AttachmentBytes: 0);

            _devices[id] = profile;
            if (!_samples.ContainsKey(id))
                _samples[id] = new List<StoredSample>();
            return profile;
        }
    }

    public IReadOnlyList<SensorProfile> Devices()
    {
        lock (_gate) return _devices.Values.OrderBy(d => d.Location).ToList();
    }

    public SensorProfile? Find(string id)
    {
        lock (_gate) return _devices.TryGetValue(MacAddress.Normalise(id), out var p) ? p : null;
    }

    /// <summary>Attach a config file / deployment photo / hardware log to a device profile.</summary>
    public SensorProfile AttachMedia(string id, string fileName, long bytes)
    {
        var key = MacAddress.Normalise(id);
        lock (_gate)
        {
            if (!_devices.TryGetValue(key, out var p))
                throw new KeyNotFoundException($"Device '{id}' is not registered.");
            var updated = p with { AttachmentName = fileName, AttachmentBytes = bytes };
            _devices[key] = updated;
            AddAlert(new AlertDto(key, AlertSeverity.Info,
                $"Config/log '{fileName}' ({bytes:N0} bytes) attached.", DateTimeOffset.UtcNow));
            return updated;
        }
    }

    // ---- Ingestion ---------------------------------------------------------------------
    public TelemetrySample Ingest(IngestRequest req, DateTimeOffset? at = null)
    {
        var ts = at ?? DateTimeOffset.UtcNow;

        // Build a strongly-typed packet per payload kind — the value is never boxed.
        ITelemetryPacket packet = req.Kind switch
        {
            TelemetryKind.Float => TelemetryPacket.Create(req.DeviceId, req.Metric, (float)req.NumericValue, ts),
            TelemetryKind.Int   => TelemetryPacket.Create(req.DeviceId, req.Metric, (int)Math.Round(req.NumericValue), ts),
            TelemetryKind.Bool  => TelemetryPacket.Create(req.DeviceId, req.Metric, req.BoolValue, ts),
            _ => throw new ArgumentOutOfRangeException(nameof(req.Kind))
        };

        var numeric = packet.AsNumeric();
        var key = MacAddress.Normalise(req.DeviceId);

        lock (_gate)
        {
            if (!_samples.TryGetValue(key, out var history))
                _samples[key] = history = new List<StoredSample>();

            bool anomaly = numeric.HasValue && IsSpike(history, req.Metric, numeric.Value);
            history.Add(new StoredSample(packet, numeric, anomaly));

            if (anomaly)
            {
                AddAlert(new AlertDto(key, AlertSeverity.Critical,
                    $"Anomalous spike on {req.Metric}: {packet.ValueAsString()}", ts));
            }

            return ToDto(packet, numeric, anomaly);
        }
    }

    // Spike detector: robust to scale via mean +/- 3σ over a trailing window of the same metric.
    private static bool IsSpike(List<StoredSample> history, string metric, double value)
    {
        var window = history
            .Where(s => string.Equals(s.Packet.Metric, metric, StringComparison.OrdinalIgnoreCase) && s.Numeric.HasValue)
            .TakeLast(20)
            .Select(s => s.Numeric!.Value)
            .ToArray();

        if (window.Length < 8) return false; // not enough context yet

        double mean = window.Average();
        double variance = window.Select(v => (v - mean) * (v - mean)).Average();
        double std = Math.Sqrt(variance);
        if (std < 1e-6) return false;

        return Math.Abs(value - mean) > 3.0 * std;
    }

    // ---- Queries -----------------------------------------------------------------------
    public IReadOnlyList<TelemetrySample> Query(string? device, string? metric, int limit)
    {
        lock (_gate)
        {
            IEnumerable<StoredSample> q = device is null
                ? _samples.Values.SelectMany(x => x)
                : (_samples.TryGetValue(MacAddress.Normalise(device), out var h) ? h : Enumerable.Empty<StoredSample>());

            if (!string.IsNullOrWhiteSpace(metric))
                q = q.Where(s => string.Equals(s.Packet.Metric, metric, StringComparison.OrdinalIgnoreCase));

            return q.OrderByDescending(s => s.Packet.Timestamp)
                    .Take(limit <= 0 ? 100 : limit)
                    .Select(s => ToDto(s.Packet, s.Numeric, s.IsAnomaly))
                    .ToList();
        }
    }

    public IReadOnlyList<TelemetrySample> Anomalies(int limit)
    {
        lock (_gate)
        {
            return _samples.Values.SelectMany(x => x)
                .Where(s => s.IsAnomaly)
                .OrderByDescending(s => s.Packet.Timestamp)
                .Take(limit <= 0 ? 100 : limit)
                .Select(s => ToDto(s.Packet, s.Numeric, s.IsAnomaly))
                .ToList();
        }
    }

    public IReadOnlyList<AlertDto> Alerts(int limit)
    {
        lock (_gate) return _alerts.OrderByDescending(a => a.Timestamp).Take(limit <= 0 ? 25 : limit).ToList();
    }

    /// <summary>Rolled-up health snapshot that drives the engagement dashboard.</summary>
    public IngestionSnapshot Snapshot()
    {
        lock (_gate)
        {
            var meshNow = _samples.Values.SelectMany(x => x)
                .Select(s => (DateTimeOffset?)s.Packet.Timestamp)
                .DefaultIfEmpty(DateTimeOffset.UtcNow)
                .Max() ?? DateTimeOffset.UtcNow;

            int totalSamples = _samples.Values.Sum(x => x.Count);
            int anomalyCount = _samples.Values.SelectMany(x => x).Count(s => s.IsAnomaly);

            var health = new List<DeviceHealth>();
            int silent = 0;
            foreach (var profile in _devices.Values)
            {
                var hist = _samples.TryGetValue(profile.Id, out var h) ? h : new List<StoredSample>();
                var last = hist.Count > 0 ? hist[^1] : null;
                var lastSeen = last?.Packet.Timestamp;
                bool isSilent = lastSeen is null || (meshNow - lastSeen.Value) > SilenceThreshold;
                if (isSilent) silent++;

                health.Add(new DeviceHealth(
                    profile.Id, profile.Location, profile.Category,
                    last?.Numeric, lastSeen,
                    hist.Count(s => s.IsAnomaly), isSilent));
            }

            // Ingest rate over the last minute of mesh time.
            var cutoff = meshNow - TimeSpan.FromMinutes(1);
            double ratePerMin = _samples.Values.SelectMany(x => x).Count(s => s.Packet.Timestamp >= cutoff);

            return new IngestionSnapshot(
                RegisteredDevices: _devices.Count,
                TotalSamples: totalSamples,
                AnomalyCount: anomalyCount,
                SilentDevices: silent,
                IngestRatePerMin: ratePerMin,
                RecentAlerts: _alerts.OrderByDescending(a => a.Timestamp).Take(12).ToList(),
                Devices: health.OrderByDescending(d => d.AnomalyCount).ThenBy(d => d.Location).ToList());
        }
    }

    /// <summary>
    /// Aggregate load across a device's stored numeric history, demonstrating the
    /// arrays -&gt; List bridge plus operator overloading on <see cref="SensorReading"/>.
    /// </summary>
    public SensorReading AggregateLoad(string device, string metric, string unit)
    {
        lock (_gate)
        {
            var hist = _samples.TryGetValue(MacAddress.Normalise(device), out var h) ? h : new List<StoredSample>();
            var values = hist
                .Where(s => string.Equals(s.Packet.Metric, metric, StringComparison.OrdinalIgnoreCase) && s.Numeric.HasValue)
                .Select(s => s.Numeric!.Value)
                .ToArray();

            // Pack into a jagged array of 5-sample cycles, then flatten to a List<SensorReading>.
            var cycles = values.Chunk(5).Select(c => c.ToArray()).ToArray();
            List<SensorReading> readings = HistoricalBatch.FromJagged(cycles, unit);
            return HistoricalBatch.AggregateLoad(readings, unit);
        }
    }

    private void AddAlert(AlertDto alert)
    {
        _alerts.Add(alert);
        if (_alerts.Count > 500) _alerts.RemoveRange(0, _alerts.Count - 500);
    }

    private static TelemetrySample ToDto(ITelemetryPacket p, double? numeric, bool anomaly)
        => new(p.DeviceId, p.Metric, p.PayloadType, p.ValueAsString(), numeric, p.Timestamp, anomaly);
}
