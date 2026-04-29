using System;

namespace StealthVisionSystem;

public sealed class DynamicArray<T>
{
    private T[] _items;
    public int Count { get; private set; }

    public DynamicArray(int initialCapacity = 4)
    {
        _items = new T[Math.Max(4, initialCapacity)];
    }

    public void Add(T item)
    {
        EnsureCapacity(Count + 1);
        _items[Count++] = item;
    }

    public T this[int index]
    {
        get
        {
            if (index < 0 || index >= Count) throw new ArgumentOutOfRangeException(nameof(index));
            return _items[index];
        }
        set
        {
            if (index < 0 || index >= Count) throw new ArgumentOutOfRangeException(nameof(index));
            _items[index] = value;
        }
    }

    public T[] ToArray()
    {
        var arr = new T[Count];
        Array.Copy(_items, arr, Count);
        return arr;
    }

    public T RemoveLast()
    {
        if (Count == 0) throw new InvalidOperationException("Dizi bos.");
        int index = Count - 1;
        T item = _items[index];
        _items[index] = default!;
        Count--;
        return item;
    }

    private void EnsureCapacity(int required)
    {
        if (required <= _items.Length) return;
        int newCapacity = _items.Length * 2;
        while (newCapacity < required) newCapacity *= 2;
        var newArr = new T[newCapacity];
        Array.Copy(_items, newArr, Count);
        _items = newArr;
    }
}

public sealed class MinHeap<T>
{
    private readonly DynamicArray<T> _items = new();
    private readonly Func<T, T, int> _compare;

    public MinHeap(Func<T, T, int> compare) => _compare = compare;
    public int Count => _items.Count;

    public void Push(T value)
    {
        _items.Add(value);
        SiftUp(_items.Count - 1);
    }

    public T Pop()
    {
        if (_items.Count == 0) throw new InvalidOperationException("Heap bos.");
        T root = _items[0];
        int last = _items.Count - 1;
        _items[0] = _items[last];
        _items.RemoveLast();
        if (_items.Count > 0) SiftDown(0);
        return root;
    }

    private void SiftUp(int index)
    {
        while (index > 0)
        {
            int parent = (index - 1) / 2;
            if (_compare(_items[index], _items[parent]) >= 0) break;
            Swap(index, parent);
            index = parent;
        }
    }

    private void SiftDown(int index)
    {
        while (true)
        {
            int left = 2 * index + 1;
            int right = left + 1;
            int smallest = index;

            if (left < _items.Count && _compare(_items[left], _items[smallest]) < 0) smallest = left;
            if (right < _items.Count && _compare(_items[right], _items[smallest]) < 0) smallest = right;
            if (smallest == index) break;

            Swap(index, smallest);
            index = smallest;
        }
    }

    private void Swap(int i, int j)
    {
        T tmp = _items[i];
        _items[i] = _items[j];
        _items[j] = tmp;
    }
}
