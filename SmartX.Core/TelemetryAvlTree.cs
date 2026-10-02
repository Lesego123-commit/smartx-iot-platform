// ------------------------------------------------------------------------------------
// PROG7312 - Programming 3B | Part 3 (PoE): Smart-X Network Topology & Mesh Routing
// File:    SmartX.Core/TelemetryAvlTree.cs
// Purpose: Self-balancing binary search tree (AVL) that stores timestamped telemetry keyed
//          by timestamp. It answers time-range queries (e.g. all readings between 14:00 and
//          16:00) in O(log n + k) by pruning whole subtrees, instead of scanning a flat list.
//
// References (Harvard) - sources consulted for the techniques used in this file:
//   - Cormen, T.H., Leiserson, C.E., Rivest, R.L. and Stein, C., 2009. Introduction to Algorithms. 3rd ed. Cambridge, MA: MIT Press. (Ch. 12-13: binary search trees, balanced trees.)
//   - Sedgewick, R. and Wayne, K., 2011. Algorithms. 4th ed. Boston: Addison-Wesley. (Balanced search trees.)
//   - Microsoft, 2024. SortedSet<T> Class (documentation of the framework's balanced-tree collection, used as a design reference). Microsoft Learn. Available at: https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.sortedset-1 [Accessed 25 September 2026].
//   - Albahari, J. and Albahari, B., 2022. C# 10 in a Nutshell: The Definitive Reference. Sebastopol: O'Reilly Media.
// ------------------------------------------------------------------------------------
namespace SmartX.Core;

/// <summary>
/// AVL (height-balanced) binary search tree of telemetry readings keyed by timestamp ticks.
/// Height stays O(log n) via rotations, so range queries and inserts are logarithmic.
/// </summary>
public sealed class TelemetryAvlTree
{
    private sealed class Node
    {
        public long Key;             // timestamp in UTC ticks
        public double Value;
        public int Height = 1;
        public Node? Left, Right;
        public Node(long key, double value) { Key = key; Value = value; }
    }

    private Node? _root;
    public int Count { get; private set; }
    public int Height => H(_root);

    /// <summary>Insert a reading. Duplicate timestamps are nudged right so none are lost.</summary>
    public void Insert(DateTimeOffset timestamp, double value)
    {
        long key = timestamp.UtcTicks;
        _root = Insert(_root, key, value);
        Count++;
    }

    private Node Insert(Node? node, long key, double value)
    {
        if (node is null) return new Node(key, value);

        if (key < node.Key) node.Left = Insert(node.Left, key, value);
        else node.Right = Insert(node.Right, key, value); // equal keys go right (stable for duplicates)

        node.Height = 1 + Math.Max(H(node.Left), H(node.Right));
        return Rebalance(node);
    }

    /// <summary>
    /// Return every reading whose timestamp falls in [from, to], in ascending time order.
    /// Subtrees entirely outside the window are never visited.
    /// </summary>
    public List<TimelinePoint> RangeQuery(DateTimeOffset from, DateTimeOffset to)
    {
        var results = new List<TimelinePoint>();
        RangeQuery(_root, from.UtcTicks, to.UtcTicks, results);
        return results;
    }

    private static void RangeQuery(Node? node, long lo, long hi, List<TimelinePoint> acc)
    {
        if (node is null) return;
        if (node.Key > lo) RangeQuery(node.Left, lo, hi, acc);           // prune right subtrees below lo
        if (node.Key >= lo && node.Key <= hi)
            acc.Add(new TimelinePoint(new DateTimeOffset(node.Key, TimeSpan.Zero), node.Value));
        if (node.Key < hi) RangeQuery(node.Right, lo, hi, acc);          // prune left subtrees above hi
    }

    /// <summary>All readings in ascending time order (in-order traversal).</summary>
    public List<TimelinePoint> InOrder()
    {
        var acc = new List<TimelinePoint>(Count);
        void Walk(Node? n)
        {
            if (n is null) return;
            Walk(n.Left);
            acc.Add(new TimelinePoint(new DateTimeOffset(n.Key, TimeSpan.Zero), n.Value));
            Walk(n.Right);
        }
        Walk(_root);
        return acc;
    }

    // ---- AVL balancing ------------------------------------------------------------------
    private static int H(Node? n) => n?.Height ?? 0;
    private static int Balance(Node? n) => n is null ? 0 : H(n.Left) - H(n.Right);

    private static Node Rebalance(Node node)
    {
        int balance = Balance(node);

        // Left heavy
        if (balance > 1)
        {
            if (Balance(node.Left) < 0) node.Left = RotateLeft(node.Left!); // Left-Right
            return RotateRight(node);
        }
        // Right heavy
        if (balance < -1)
        {
            if (Balance(node.Right) > 0) node.Right = RotateRight(node.Right!); // Right-Left
            return RotateLeft(node);
        }
        return node;
    }

    private static Node RotateRight(Node y)
    {
        Node x = y.Left!;
        Node? t2 = x.Right;
        x.Right = y;
        y.Left = t2;
        y.Height = 1 + Math.Max(H(y.Left), H(y.Right));
        x.Height = 1 + Math.Max(H(x.Left), H(x.Right));
        return x;
    }

    private static Node RotateLeft(Node x)
    {
        Node y = x.Right!;
        Node? t2 = y.Left;
        y.Left = x;
        x.Right = t2;
        x.Height = 1 + Math.Max(H(x.Left), H(x.Right));
        y.Height = 1 + Math.Max(H(y.Left), H(y.Right));
        return y;
    }
}
