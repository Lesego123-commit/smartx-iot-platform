// ------------------------------------------------------------------------------------
// PROG7312 - Programming 3B | Part 3 (PoE): Smart-X Network Topology & Mesh Routing
// File:    SmartX.Api/Services/TopologyEngine.cs
// Purpose: Builds and serves the mesh topology: a weighted MeshGraph of the ESP32 nodes plus
//          a per-device AVL tree of timestamped history. Exposes the graph, BFS/DFS traversal,
//          the MST (Prim/Kruskal), MST-based routing to the gateway, and time-range history.
//
// References (Harvard) - sources consulted for the techniques used in this file:
//   - Cormen, T.H., Leiserson, C.E., Rivest, R.L. and Stein, C., 2009. Introduction to Algorithms. 3rd ed. Cambridge, MA: MIT Press.
//   - Microsoft, 2024. Dependency injection in ASP.NET Core. Microsoft Learn. Available at: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/dependency-injection [Accessed 25 September 2026].
//   - Albahari, J. and Albahari, B., 2022. C# 10 in a Nutshell: The Definitive Reference. Sebastopol: O'Reilly Media.
// ------------------------------------------------------------------------------------
using SmartX.Core;

namespace SmartX.Api.Services;

public sealed class TopologyEngine
{
    private sealed record Node(string Id, string Label, SensorCategory Category, string Metric, double Baseline, bool Down);

    private readonly MeshGraph _graph = new();
    private readonly Dictionary<string, TelemetryAvlTree> _history = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Node> _nodes = new(StringComparer.OrdinalIgnoreCase);

    public TopologyEngine() => Seed();

    private void Seed()
    {
        var rng = new Random(20260925);
        var now = DateTimeOffset.UtcNow;

        // Central gateway in the middle of the layout.
        _graph.AddNode("GATEWAY-00", "Central Gateway", SensorCategory.PowerConsumption, isGateway: true, x: 0.5, y: 0.5);

        var sensors = new[]
        {
            new Node("3C:71:BF:0A:2D:01", "Soil Probe A",  SensorCategory.Environmental,   "soil_moisture", 42,   false),
            new Node("3C:71:BF:0A:2D:02", "Soil Probe B",  SensorCategory.Environmental,   "soil_moisture", 55,   false),
            new Node("3C:71:BF:0A:2D:03", "Roof Probe",    SensorCategory.Environmental,   "temperature",   24,   true),  // DOWN
            new Node("3C:71:BF:0A:2D:04", "Corridor Hum",  SensorCategory.Environmental,   "humidity",      60,   false),
            new Node("3C:71:BF:0A:2D:05", "Feeder 1 Mtr",  SensorCategory.PowerConsumption,"power_wattage", 1200, false),
            new Node("3C:71:BF:0A:2D:06", "Feeder 2 Mtr",  SensorCategory.PowerConsumption,"power_wattage", 1500, false),
            new Node("3C:71:BF:0A:2D:07", "Main Bus Volt", SensorCategory.PowerConsumption,"voltage",       230,  false),
            new Node("3C:71:BF:0A:2D:08", "Pump Line 1",   SensorCategory.Actuator,        "valve_state",   1,    false),
            new Node("3C:71:BF:0A:2D:09", "Pump Line 2",   SensorCategory.Actuator,        "relay_state",   0,    false),
            new Node("3C:71:BF:0A:2D:0A", "Cold Store",    SensorCategory.Environmental,   "temperature",   4,    false),
        };

        // Radial layout: sensors evenly placed on a circle around the gateway.
        int n = sensors.Length;
        var pos = new Dictionary<string, (double X, double Y)>();
        for (int i = 0; i < n; i++)
        {
            double angle = 2 * Math.PI * i / n - Math.PI / 2;
            double x = 0.5 + 0.40 * Math.Cos(angle);
            double y = 0.5 + 0.40 * Math.Sin(angle);
            pos[sensors[i].Id] = (x, y);
            _graph.AddNode(sensors[i].Id, sensors[i].Label, sensors[i].Category, false, x, y);
            _nodes[sensors[i].Id] = sensors[i];
            if (sensors[i].Down) _graph.SetDown(sensors[i].Id, true);

            // Per-device AVL history: ~60 readings over the last hour.
            var tree = new TelemetryAvlTree();
            for (int m = 60; m >= 1; m--)
            {
                double noise = (rng.NextDouble() - 0.5) * sensors[i].Baseline * 0.1;
                tree.Insert(now.AddMinutes(-m), Math.Round(sensors[i].Baseline + noise, 2));
            }
            _history[sensors[i].Id] = tree;
        }

        double W(string a, string b) // routing cost ~ physical distance between nodes
        {
            var (ax, ay) = a == "GATEWAY-00" ? (0.5, 0.5) : pos[a];
            var (bx, by) = b == "GATEWAY-00" ? (0.5, 0.5) : pos[b];
            return Math.Round(Math.Sqrt((ax - bx) * (ax - bx) + (ay - by) * (ay - by)) * 100, 1);
        }

        // Mesh links: a ring of neighbours, a few gateway spokes, and some cross links.
        for (int i = 0; i < n; i++)
        {
            var a = sensors[i].Id;
            var b = sensors[(i + 1) % n].Id;
            _graph.AddEdge(a, b, W(a, b));                       // ring
        }
        foreach (var i in new[] { 0, 3, 5, 8 })                 // spokes to gateway
            _graph.AddEdge(sensors[i].Id, "GATEWAY-00", W(sensors[i].Id, "GATEWAY-00"));
        _graph.AddEdge(sensors[0].Id, sensors[5].Id, W(sensors[0].Id, sensors[5].Id)); // cross links
        _graph.AddEdge(sensors[2].Id, sensors[7].Id, W(sensors[2].Id, sensors[7].Id));
        _graph.AddEdge(sensors[1].Id, sensors[6].Id, W(sensors[1].Id, sensors[6].Id));
    }

    // ---- API surface -------------------------------------------------------------------
    public MeshGraphDto Graph() => _graph.ToDto();

    public TraversalDto Traverse(string kind, string? start)
    {
        var from = string.IsNullOrWhiteSpace(start) ? _graph.GatewayId! : start!;
        return kind.Equals("dfs", StringComparison.OrdinalIgnoreCase) ? _graph.Dfs(from) : _graph.Bfs(from);
    }

    public MstResult Mst(string? algorithm)
    {
        bool kruskal = algorithm?.Equals("kruskal", StringComparison.OrdinalIgnoreCase) == true;
        var edges = kruskal ? _graph.MstKruskal() : _graph.MstPrim();
        return new MstResult(
            kruskal ? "kruskal" : "prim",
            Math.Round(edges.Sum(e => e.W), 1),
            edges.Select(e => new MeshEdgeDto(e.A, e.B, e.W, true)).ToList());
    }

    public RouteDto Route(string deviceId) => _graph.RouteToGateway(deviceId);

    public IReadOnlyList<TimelinePoint> History(HistoryQuery q)
        => _history.TryGetValue(Normalise(q.DeviceId), out var tree)
            ? tree.RangeQuery(q.FromUtc, q.ToUtc)
            : Array.Empty<TimelinePoint>();

    public object HistoryStats(string deviceId)
    {
        var key = Normalise(deviceId);
        if (!_history.TryGetValue(key, out var tree)) return new { count = 0, height = 0 };
        return new { count = tree.Count, height = tree.Height };
    }

    public IReadOnlyList<object> Devices()
        => _nodes.Values.Select(v => (object)new { id = v.Id, label = v.Label, metric = v.Metric, down = v.Down }).ToList();

    private static string Normalise(string id) => MacAddress.IsValid(id) ? MacAddress.Normalise(id) : id;
}
