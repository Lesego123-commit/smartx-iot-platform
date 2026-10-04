// ------------------------------------------------------------------------------------
// PROG7312 - Programming 3B | Part 3 (PoE): Smart-X Network Topology & Mesh Routing
// File:    SmartX.Core/MeshGraph.cs
// Purpose: Weighted, undirected graph modelling the IoT mesh (vertices = sensors/gateway,
//          edges = wireless links with a routing cost). Provides BFS/DFS traversal (which
//          nodes are reachable / down), a Minimum Spanning Tree via BOTH Prim's (using the
//          custom MinHeap) and Kruskal's (using union-find), and MST-based routing to the
//          gateway - the most power-efficient path for telemetry to hop through the mesh.
//
// References (Harvard) - sources consulted for the techniques used in this file:
//   - Cormen, T.H., Leiserson, C.E., Rivest, R.L. and Stein, C., 2009. Introduction to Algorithms. 3rd ed. Cambridge, MA: MIT Press. (Ch. 22-23: graph search, minimum spanning trees.)
//   - Sedgewick, R. and Wayne, K., 2011. Algorithms. 4th ed. Boston: Addison-Wesley. (Graphs, BFS/DFS, Prim, Kruskal, union-find.)
//   - Microsoft, 2024. Collections and Data Structures. Microsoft Learn. Available at: https://learn.microsoft.com/en-us/dotnet/standard/collections/ [Accessed 25 September 2026].
//   - Albahari, J. and Albahari, B., 2022. C# 10 in a Nutshell: The Definitive Reference. Sebastopol: O'Reilly Media.
// ------------------------------------------------------------------------------------
namespace SmartX.Core;

public sealed class MeshGraph
{
    private sealed class Vertex
    {
        public required string Id { get; init; }
        public required string Label { get; init; }
        public SensorCategory Category { get; init; }
        public bool IsGateway { get; init; }
        public bool IsDown { get; set; }
        public double X { get; init; }
        public double Y { get; init; }
    }

    private readonly Dictionary<string, Vertex> _vertices = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<(string To, double W)>> _adj = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<(string A, string B, double W)> _edges = new();

    public string? GatewayId { get; private set; }

    public void AddNode(string id, string label, SensorCategory category, bool isGateway, double x, double y)
    {
        _vertices[id] = new Vertex { Id = id, Label = label, Category = category, IsGateway = isGateway, X = x, Y = y };
        if (!_adj.ContainsKey(id)) _adj[id] = new();
        if (isGateway) GatewayId = id;
    }

    public void AddEdge(string a, string b, double weight)
    {
        if (!_vertices.ContainsKey(a) || !_vertices.ContainsKey(b)) return;
        _adj[a].Add((b, weight));
        _adj[b].Add((a, weight));
        _edges.Add((a, b, weight));
    }

    public void SetDown(string id, bool down)
    {
        if (_vertices.TryGetValue(id, out var v)) v.IsDown = down;
    }

    private bool IsUp(string id) => _vertices.TryGetValue(id, out var v) && !v.IsDown;

    // ---- Traversal (BFS / DFS) ---------------------------------------------------------
    /// <summary>Breadth-first search from a start node, hopping only through live nodes.</summary>
    public TraversalDto Bfs(string startId)
    {
        var order = new List<string>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (IsUp(startId))
        {
            var queue = new Queue<string>();
            queue.Enqueue(startId);
            visited.Add(startId);
            while (queue.Count > 0)
            {
                var cur = queue.Dequeue();
                order.Add(cur);
                foreach (var (to, _) in _adj[cur].OrderBy(e => e.To))
                    if (IsUp(to) && visited.Add(to))
                        queue.Enqueue(to);
            }
        }
        return Traversal("BFS", startId, order, visited);
    }

    /// <summary>Depth-first search from a start node, hopping only through live nodes.</summary>
    public TraversalDto Dfs(string startId)
    {
        var order = new List<string>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void Visit(string id)
        {
            if (!IsUp(id) || !visited.Add(id)) return;
            order.Add(id);
            foreach (var (to, _) in _adj[id].OrderBy(e => e.To))
                Visit(to);
        }
        if (IsUp(startId)) Visit(startId);
        return Traversal("DFS", startId, order, visited);
    }

    private TraversalDto Traversal(string kind, string start, List<string> order, HashSet<string> visited)
    {
        var unreachable = _vertices.Keys.Where(k => !visited.Contains(k)).OrderBy(k => k).ToList();
        return new TraversalDto(kind, start, order, order.ToList(), unreachable);
    }

    // ---- Minimum Spanning Tree ---------------------------------------------------------
    /// <summary>
    /// Prim's algorithm using the custom <see cref="MinHeap{T}"/>. Grows the tree from the
    /// gateway, always taking the cheapest edge to a new live node. Returns the MST edges.
    /// </summary>
    public List<(string A, string B, double W)> MstPrim()
    {
        var mst = new List<(string, string, double)>();
        var start = GatewayId ?? _vertices.Keys.FirstOrDefault(IsUp);
        if (start is null) return mst;

        var inTree = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { start };
        var frontier = new MinHeap<(string From, string To)>();
        foreach (var (to, w) in _adj[start]) if (IsUp(to)) frontier.Push((start, to), w);

        int liveCount = _vertices.Values.Count(v => !v.IsDown);
        while (!frontier.IsEmpty && inTree.Count < liveCount)
        {
            var (from, to) = frontier.Pop();
            if (inTree.Contains(to)) continue;      // already connected - skip
            var w = _adj[from].First(e => e.To.Equals(to, StringComparison.OrdinalIgnoreCase)).W;
            mst.Add((from, to, w));
            inTree.Add(to);
            foreach (var (next, nw) in _adj[to]) if (IsUp(next) && !inTree.Contains(next)) frontier.Push((to, next), nw);
        }
        return mst;
    }

    /// <summary>Kruskal's algorithm using union-find - an alternative MST (same total weight).</summary>
    public List<(string A, string B, double W)> MstKruskal()
    {
        var mst = new List<(string, string, double)>();
        var parent = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var v in _vertices.Values.Where(v => !v.IsDown)) parent[v.Id] = v.Id;

        string Find(string x) { while (parent[x] != x) { parent[x] = parent[parent[x]]; x = parent[x]; } return x; }
        bool Union(string a, string b) { var ra = Find(a); var rb = Find(b); if (ra == rb) return false; parent[ra] = rb; return true; }

        foreach (var (a, b, w) in _edges.Where(e => IsUp(e.A) && IsUp(e.B)).OrderBy(e => e.W))
            if (Union(a, b)) mst.Add((a, b, w));
        return mst;
    }

    // ---- MST-based routing to the gateway ----------------------------------------------
    /// <summary>Trace the hop path from a device to the gateway along the MST (Prim's).</summary>
    public RouteDto RouteToGateway(string deviceId)
    {
        var gateway = GatewayId;
        if (gateway is null || !IsUp(deviceId))
            return new RouteDto(deviceId, Array.Empty<string>(), 0, false);

        // Build adjacency restricted to MST edges, then BFS for the device -> gateway path.
        var mst = MstPrim();
        var tree = new Dictionary<string, List<(string To, double W)>>(StringComparer.OrdinalIgnoreCase);
        foreach (var id in _vertices.Keys) tree[id] = new();
        foreach (var (a, b, w) in mst) { tree[a].Add((b, w)); tree[b].Add((a, w)); }

        var prev = new Dictionary<string, (string Node, double W)>(StringComparer.OrdinalIgnoreCase);
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { deviceId };
        var q = new Queue<string>();
        q.Enqueue(deviceId);
        while (q.Count > 0)
        {
            var cur = q.Dequeue();
            if (cur.Equals(gateway, StringComparison.OrdinalIgnoreCase)) break;
            foreach (var (to, w) in tree[cur])
                if (visited.Add(to)) { prev[to] = (cur, w); q.Enqueue(to); }
        }

        if (!visited.Contains(gateway)) return new RouteDto(deviceId, Array.Empty<string>(), 0, false);

        var hops = new List<string>();
        double cost = 0;
        for (var at = gateway; at is not null; )
        {
            hops.Add(at);
            if (prev.TryGetValue(at, out var p)) { cost += p.W; at = p.Node; }
            else at = null;
        }
        hops.Reverse();   // device -> ... -> gateway
        return new RouteDto(deviceId, hops, cost, true);
    }

    // ---- Serialisation -----------------------------------------------------------------
    public MeshGraphDto ToDto()
    {
        var mst = MstPrim();
        var mstSet = new HashSet<string>(mst.Select(e => EdgeKey(e.Item1, e.Item2)));
        double total = mst.Sum(e => e.Item3);

        var nodes = _vertices.Values
            .Select(v => new MeshNodeDto(v.Id, v.Label, v.Category, v.IsGateway, v.IsDown, v.X, v.Y))
            .ToList();

        var edges = _edges
            .Select(e => new MeshEdgeDto(e.A, e.B, e.W, mstSet.Contains(EdgeKey(e.A, e.B))))
            .ToList();

        return new MeshGraphDto(nodes, edges, total);
    }

    private static string EdgeKey(string a, string b)
        => string.CompareOrdinal(a, b) <= 0 ? $"{a}|{b}" : $"{b}|{a}";
}
