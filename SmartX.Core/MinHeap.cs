// ------------------------------------------------------------------------------------
// PROG7312 - Programming 3B | Part 3 (PoE): Smart-X Network Topology & Mesh Routing
// File:    SmartX.Core/MinHeap.cs
// Purpose: A generic binary min-heap (priority queue) used to drive Prim's minimum spanning
//          tree. Push/Pop are O(log n); Peek is O(1). Implemented from first principles to
//          demonstrate the Heap data structure rather than relying on the framework type.
//
// References (Harvard) - sources consulted for the techniques used in this file:
//   - Cormen, T.H., Leiserson, C.E., Rivest, R.L. and Stein, C., 2009. Introduction to Algorithms. 3rd ed. Cambridge, MA: MIT Press. (Ch. 6: heapsort / binary heaps.)
//   - Microsoft, 2024. PriorityQueue<TElement,TPriority> Class (framework reference for the same abstraction). Microsoft Learn. Available at: https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.priorityqueue-2 [Accessed 25 September 2026].
//   - Albahari, J. and Albahari, B., 2022. C# 10 in a Nutshell: The Definitive Reference. Sebastopol: O'Reilly Media.
// ------------------------------------------------------------------------------------
namespace SmartX.Core;

/// <summary>Binary min-heap ordered by a double priority (smallest priority pops first).</summary>
public sealed class MinHeap<T>
{
    private readonly List<(T Item, double Priority)> _heap = new();

    public int Count => _heap.Count;
    public bool IsEmpty => _heap.Count == 0;

    public void Push(T item, double priority)
    {
        _heap.Add((item, priority));
        SiftUp(_heap.Count - 1);
    }

    public T Peek() => _heap.Count > 0 ? _heap[0].Item : throw new InvalidOperationException("Heap is empty.");

    public T Pop()
    {
        if (_heap.Count == 0) throw new InvalidOperationException("Heap is empty.");
        var top = _heap[0].Item;
        var last = _heap[^1];
        _heap.RemoveAt(_heap.Count - 1);
        if (_heap.Count > 0)
        {
            _heap[0] = last;
            SiftDown(0);
        }
        return top;
    }

    public bool TryPop(out T item)
    {
        if (_heap.Count == 0) { item = default!; return false; }
        item = Pop();
        return true;
    }

    private void SiftUp(int i)
    {
        while (i > 0)
        {
            int parent = (i - 1) / 2;
            if (_heap[i].Priority >= _heap[parent].Priority) break;
            (_heap[i], _heap[parent]) = (_heap[parent], _heap[i]);
            i = parent;
        }
    }

    private void SiftDown(int i)
    {
        int n = _heap.Count;
        while (true)
        {
            int left = 2 * i + 1, right = 2 * i + 2, smallest = i;
            if (left < n && _heap[left].Priority < _heap[smallest].Priority) smallest = left;
            if (right < n && _heap[right].Priority < _heap[smallest].Priority) smallest = right;
            if (smallest == i) break;
            (_heap[i], _heap[smallest]) = (_heap[smallest], _heap[i]);
            i = smallest;
        }
    }
}
