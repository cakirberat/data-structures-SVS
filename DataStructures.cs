using System;
using System.Collections;
using System.Collections.Generic;

namespace DataStructures_SVS
{
    // Sifirdan yazilmis, otomatik buyuyen dinamik dizi.
    // Isin sonuclari, duvar listeleri ve gecici geometrik veriler icin kullanilir.
    public class DynamicArray<T> : IEnumerable<T>
    {
        private T[] data;
        public int Count { get; private set; }
        public int Capacity => data.Length;

        public DynamicArray(int initialCapacity = 10)
        {
            if (initialCapacity < 1) initialCapacity = 1;
            data = new T[initialCapacity];
            Count = 0;
        }

        public void Add(T item)
        {
            if (Count == Capacity)
            {
                Array.Resize(ref data, Capacity * 2);
            }
            data[Count++] = item;
        }

        public T this[int index]
        {
            get
            {
                if (index < 0 || index >= Count) throw new IndexOutOfRangeException("Gecersiz indeks!");
                return data[index];
            }
            set
            {
                if (index < 0 || index >= Count) throw new IndexOutOfRangeException("Gecersiz indeks!");
                data[index] = value;
            }
        }

        // Belirtilen indeksteki elemani siler, sonraki elemanlari kaydirir.
        public void RemoveAt(int index)
        {
            if (index < 0 || index >= Count) throw new IndexOutOfRangeException("Gecersiz indeks!");
            for (int i = index; i < Count - 1; i++)
                data[i] = data[i + 1];
            data[Count - 1] = default(T);
            Count--;
        }

        // Son elemani silip geri dondurur. (Heap islemleri icin gereklidir.)
        public T RemoveLast()
        {
            if (Count == 0) throw new InvalidOperationException("Dizi bos!");
            T item = data[Count - 1];
            data[Count - 1] = default(T);
            Count--;
            return item;
        }

        public void Clear()
        {
            for (int i = 0; i < Count; i++) data[i] = default(T);
            Count = 0;
        }

        // IEnumerable<T>: foreach ile kullanilabilir
        public IEnumerator<T> GetEnumerator()
        {
            for (int i = 0; i < Count; i++)
                yield return data[i];
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    // Sifirdan yazilmis Min-Heap (oncelik kuyrugu).
    // A* algoritmasinda en dusuk maliyetli dugumu O(log n) ile secmek icin kullanilir.
    public class MinHeap<T> where T : IComparable<T>
    {
        private DynamicArray<T> heap = new DynamicArray<T>();

        public int Count => heap.Count;
        public bool IsEmpty => heap.Count == 0;

        public void Push(T item)
        {
            heap.Add(item);
            BubbleUp(heap.Count - 1);
        }

        // En kucuk elemani CIKARIR ve dondurur (eski kodda silinmiyordu - hata duzeltildi).
        public T Pop()
        {
            if (heap.Count == 0) throw new InvalidOperationException("Heap bos!");

            T result = heap[0];
            T last = heap.RemoveLast();

            if (heap.Count > 0)
            {
                heap[0] = last;
                BubbleDown(0);
            }
            return result;
        }

        public T Peek()
        {
            if (heap.Count == 0) throw new InvalidOperationException("Heap bos!");
            return heap[0];
        }

        private void BubbleUp(int index)
        {
            while (index > 0)
            {
                int parentIndex = (index - 1) / 2;
                if (heap[index].CompareTo(heap[parentIndex]) >= 0) break;

                Swap(index, parentIndex);
                index = parentIndex;
            }
        }

        private void BubbleDown(int index)
        {
            int n = heap.Count;
            while (true)
            {
                int left = 2 * index + 1;
                int right = 2 * index + 2;
                int smallest = index;

                if (left < n && heap[left].CompareTo(heap[smallest]) < 0) smallest = left;
                if (right < n && heap[right].CompareTo(heap[smallest]) < 0) smallest = right;

                if (smallest == index) break;

                Swap(index, smallest);
                index = smallest;
            }
        }

        private void Swap(int a, int b)
        {
            T temp = heap[a];
            heap[a] = heap[b];
            heap[b] = temp;
        }
    }

    // BSP agacinin bir dugumu. Bir bolme duzlemi (partition line) ve o duzlem
    // uzerinde kalan duvarlari tutar; on/arka alt agaclara dallanir.
    public class BspNode
    {
        public Segment PartitionLine { get; set; }
        public BspNode Front { get; set; }
        public BspNode Back { get; set; }
        public DynamicArray<Segment> Walls { get; set; } = new DynamicArray<Segment>();
    }

    // Binary Space Partitioning Tree.
    // Duvar segmentlerini uzamsal olarak bolerek, gorus (line-of-sight) ve kesisim
    // testlerinin tum duvarlari taramadan yapilmasini saglar.
    public class BspTree
    {
        private const float EPS = 0.0001f;
        public BspNode Root { get; private set; }

        public void Build(DynamicArray<Segment> walls)
        {
            var list = new System.Collections.Generic.List<Segment>();
            for (int i = 0; i < walls.Count; i++) list.Add(walls[i]);
            Root = BuildNode(list);
        }

        private BspNode BuildNode(System.Collections.Generic.List<Segment> segments)
        {
            if (segments.Count == 0) return null;

            var node = new BspNode();
            Segment partition = segments[0];
            node.PartitionLine = partition;
            node.Walls.Add(partition);

            var frontList = new System.Collections.Generic.List<Segment>();
            var backList = new System.Collections.Generic.List<Segment>();

            for (int i = 1; i < segments.Count; i++)
            {
                Segment seg = segments[i];
                float dStart = SignedDistance(partition, seg.Start);
                float dEnd = SignedDistance(partition, seg.End);

                bool startFront = dStart > EPS;
                bool startBack = dStart < -EPS;
                bool endFront = dEnd > EPS;
                bool endBack = dEnd < -EPS;

                if (!startFront && !startBack && !endFront && !endBack)
                {
                    // Bolme cizgisiyle ayni dogru uzerinde (collinear)
                    node.Walls.Add(seg);
                }
                else if (!startBack && !endBack)
                {
                    frontList.Add(seg);
                }
                else if (!startFront && !endFront)
                {
                    backList.Add(seg);
                }
                else
                {
                    // Segment bolme cizgisini kesiyor -> ikiye bol
                    Vector2D mid = LineIntersectPoint(partition, seg);
                    if (mid == null)
                    {
                        frontList.Add(seg);
                    }
                    else
                    {
                        var first = new Segment(seg.Start.Clone(), mid.Clone());
                        var second = new Segment(mid.Clone(), seg.End.Clone());
                        if (dStart > 0) { frontList.Add(first); backList.Add(second); }
                        else { backList.Add(first); frontList.Add(second); }
                    }
                }
            }

            node.Front = BuildNode(frontList);
            node.Back = BuildNode(backList);
            return node;
        }

        // a ve b arasindaki dogru parcasi herhangi bir duvarla kesisiyor mu?
        // (Line of Sight icin: kesisiyorsa gorus engellenir.)
        public bool SegmentIntersectsWall(Vector2D a, Vector2D b)
        {
            return SegmentIntersectsNode(Root, a, b);
        }

        private bool SegmentIntersectsNode(BspNode node, Vector2D a, Vector2D b)
        {
            if (node == null) return false;

            for (int i = 0; i < node.Walls.Count; i++)
            {
                Segment w = node.Walls[i];
                if (Geometry.SegmentsIntersect(a, b, w.Start, w.End)) return true;
            }

            float da = SignedDistance(node.PartitionLine, a);
            float db = SignedDistance(node.PartitionLine, b);

            // Segmentin her iki ucu da duzlemin hangi tarafinda olduguna gore
            // sadece ilgili alt agaci tarar (BSP'nin O(log n) avantaji)
            bool checkFront = da >= 0 || db >= 0;
            bool checkBack  = da <= 0 || db <= 0;

            if (checkFront && SegmentIntersectsNode(node.Front, a, b)) return true;
            if (checkBack  && SegmentIntersectsNode(node.Back,  a, b)) return true;

            return false;
        }

        // Bir noktanin bolme cizgisine gore isaretli mesafesi (+ on taraf, - arka taraf)
        private static float SignedDistance(Segment line, Vector2D p)
        {
            float dx = line.End.X - line.Start.X;
            float dy = line.End.Y - line.Start.Y;
            // Normal: (-dy, dx)
            return (-dy) * (p.X - line.Start.X) + dx * (p.Y - line.Start.Y);
        }

        // Segmentin, bolme cizgisinin sonsuz uzantisiyla kesisim noktasi
        private static Vector2D LineIntersectPoint(Segment line, Segment seg)
        {
            float x1 = line.Start.X, y1 = line.Start.Y;
            float x2 = line.End.X, y2 = line.End.Y;
            float x3 = seg.Start.X, y3 = seg.Start.Y;
            float x4 = seg.End.X, y4 = seg.End.Y;

            float denom = (x1 - x2) * (y3 - y4) - (y1 - y2) * (x3 - x4);
            if (Math.Abs(denom) < EPS) return null;

            float t = ((x1 - x3) * (y3 - y4) - (y1 - y3) * (x3 - x4)) / denom;
            return new Vector2D(x1 + t * (x2 - x1), y1 + t * (y2 - y1));
        }
    }
}
