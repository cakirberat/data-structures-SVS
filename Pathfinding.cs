using System;
using System.Collections.Generic;

namespace DataStructures_SVS
{
    // Graf dugumu: grid hucresi veya waypoint konumu
    public class GraphNode
    {
        public int Id;
        public Vector2D Position;

        public GraphNode(int id, Vector2D pos)
        {
            Id = id;
            Position = pos;
        }
    }

    // Graf kenari: iki dugum arasi baglanti ve maliyeti (mesafe)
    public class GraphEdge
    {
        public int ToNodeId;
        public float Cost;

        public GraphEdge(int to, float cost)
        {
            ToNodeId = to;
            Cost = cost;
        }
    }

    // Yurunebilir alanlarin graf olarak modellenmesi.
    // Dugumler: konum noktalari; kenarlar: duvar engeli olmayan gecilebilir baglantilar.
    public class WaypointGraph
    {
        private DynamicArray<GraphNode> nodes = new DynamicArray<GraphNode>();
        private List<GraphEdge>[] adjacency;

        public int NodeCount => nodes.Count;

        public int AddNode(Vector2D position)
        {
            int id = nodes.Count;
            nodes.Add(new GraphNode(id, position));
            return id;
        }

        public GraphNode GetNode(int id) => nodes[id];

        // Tum dugumler icin bos adjacency listesi olusturur (BuildEdges oncesi cagrilabilir)
        private void EnsureAdjacency()
        {
            if (adjacency != null && adjacency.Length == nodes.Count) return;
            adjacency = new List<GraphEdge>[nodes.Count];
            for (int i = 0; i < nodes.Count; i++) adjacency[i] = new List<GraphEdge>();
        }

        // Iki dugum arasinda duvar yoksa ve gecit entity'ye yeterliyse kenar ekler
        public void BuildEdges(DynamicArray<Segment> walls, BspTree bsp, float entityRadius)
        {
            int n = nodes.Count;
            adjacency = new List<GraphEdge>[n];
            for (int i = 0; i < n; i++) adjacency[i] = new List<GraphEdge>();

            for (int i = 0; i < n; i++)
            {
                for (int j = i + 1; j < n; j++)
                {
                    if (!Geometry.PathClearForEntity(
                        nodes[i].Position, nodes[j].Position, entityRadius, bsp, walls))
                        continue;

                    float dist = Vector2D.Distance(nodes[i].Position, nodes[j].Position);
                    adjacency[i].Add(new GraphEdge(j, dist));
                    adjacency[j].Add(new GraphEdge(i, dist));
                }
            }
        }

        public List<GraphEdge> GetNeighbors(int nodeId)
        {
            EnsureAdjacency();
            return adjacency[nodeId];
        }
    }

    // A* icin heap dugumu (FCost = GCost + HCost)
    public class AStarHeapNode : IComparable<AStarHeapNode>
    {
        public int NodeId;
        public float GCost;
        public float HCost;
        public float FCost => GCost + HCost;
        public int ParentId;
        public bool HasParent;

        public int CompareTo(AStarHeapNode other)
        {
            int cmp = FCost.CompareTo(other.FCost);
            if (cmp != 0) return cmp;
            return HCost.CompareTo(other.HCost);
        }
    }

    // A* yol bulma: Min-Heap ile en dusuk maliyetli dugum secimi.
    public class AStarPathfinder
    {
        private const int NO_PARENT = -1;

        // Onceden kurulmus graf uzerinde iki dugum arasi A*
        public List<Vector2D> FindPathBetweenNodes(WaypointGraph graph, List<Vector2D> nodePositions,
            int startId, int targetId)
        {
            var result = new List<Vector2D>();
            if (startId < 0 || targetId < 0 || startId >= nodePositions.Count || targetId >= nodePositions.Count)
                return result;

            if (startId == targetId)
            {
                result.Add(nodePositions[targetId].Clone());
                return result;
            }

            int n = graph.NodeCount;
            var openHeap = new MinHeap<AStarHeapNode>();
            var openLookup = new Dictionary<int, AStarHeapNode>();
            var closed = new bool[n];
            var bestG = new float[n];
            var parent = new int[n];

            for (int i = 0; i < n; i++)
            {
                bestG[i] = float.MaxValue;
                parent[i] = NO_PARENT;
            }

            Vector2D targetPos = nodePositions[targetId];

            var startNode = new AStarHeapNode
            {
                NodeId = startId,
                GCost = 0,
                HCost = Vector2D.Distance(nodePositions[startId], targetPos),
                ParentId = NO_PARENT,
                HasParent = false
            };

            bestG[startId] = 0;
            openHeap.Push(startNode);
            openLookup[startId] = startNode;

            while (!openHeap.IsEmpty)
            {
                AStarHeapNode current = openHeap.Pop();
                openLookup.Remove(current.NodeId);

                if (closed[current.NodeId]) continue;
                closed[current.NodeId] = true;

                if (current.NodeId == targetId)
                {
                    return ReconstructPath(graph, parent, targetId, nodePositions[startId]);
                }

                List<GraphEdge> neighbors = graph.GetNeighbors(current.NodeId);
                for (int i = 0; i < neighbors.Count; i++)
                {
                    GraphEdge edge = neighbors[i];
                    int neighborId = edge.ToNodeId;
                    if (closed[neighborId]) continue;

                    float tentativeG = current.GCost + edge.Cost;
                    if (tentativeG >= bestG[neighborId]) continue;

                    bestG[neighborId] = tentativeG;
                    parent[neighborId] = current.NodeId;

                    var neighborHeap = new AStarHeapNode
                    {
                        NodeId = neighborId,
                        GCost = tentativeG,
                        HCost = Vector2D.Distance(graph.GetNode(neighborId).Position, targetPos),
                        ParentId = current.NodeId,
                        HasParent = true
                    };

                    openHeap.Push(neighborHeap);
                    openLookup[neighborId] = neighborHeap;
                }
            }

            return result;
        }

        public List<Vector2D> FindPathToPosition(LevelNavigationGraph nav, Vector2D from, Vector2D to,
            DynamicArray<Segment> walls, BspTree bsp)
        {
            // Direkt yol varsa grafiğe gerek yok
            if (Geometry.PathClearForEntity(from, to, PatrolSystem.GraphPathRadius, bsp, walls))
            {
                var direct = new List<Vector2D>();
                direct.Add(to.Clone());
                return direct;
            }

            // startId: LOS tercihli, yoksa en yakın düğüm (fallback)
            int startId  = nav.FindNearestReachableNode(from, to, walls, bsp);
            // targetId: bağlı düğüm tercihli (izole sıfır-kenarlı düğüm hedef seçilmez)
            int targetId = nav.FindNearestConnectedNode(to);

            // Yine de -1 çıkarsa (boş graf) boş döndür
            if (startId < 0 || targetId < 0) return new List<Vector2D>();

            return FindPathBetweenNodes(nav.Graph, nav.GetNodePositions(), startId, targetId);
        }

        private List<Vector2D> ReconstructPath(WaypointGraph graph, int[] parent, int targetId, Vector2D startPos)
        {
            var path = new List<Vector2D>();
            int current = targetId;

            // parent[startId] == NO_PARENT olacagindan baslangic dugumu dahil edilmez,
            // yalnizca hedef yonundeki adimlar eklenir
            while (current != NO_PARENT)
            {
                path.Insert(0, graph.GetNode(current).Position.Clone());
                current = parent[current];
            }

            // Baslangic konumuna cok yakin noktalari at (gereksiz adim)
            while (path.Count > 0 && Vector2D.DistanceSquared(path[0], startPos) < 100f)
                path.RemoveAt(0);

            return path;
        }
    }
}
