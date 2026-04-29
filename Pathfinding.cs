using System;
using System.Collections.Generic;

namespace StealthVisionSystem;

public sealed class WaypointGraph
{
    private readonly DynamicArray<Vector2> _nodes = new();
    private readonly DynamicArray<DynamicArray<Edge>> _adjacency = new();
    private readonly BspTree _bsp;

    public WaypointGraph(DynamicArray<WallSegment> walls) => _bsp = new BspTree(walls);
    public int NodeCount => _nodes.Count;

    public int AddNode(Vector2 position)
    {
        _nodes.Add(position);
        _adjacency.Add(new DynamicArray<Edge>());
        return _nodes.Count - 1;
    }

    public Vector2 GetPosition(int node) => _nodes[node];
    public DynamicArray<Edge> GetEdges(int node) => _adjacency[node];

    public void ConnectIfVisible(int a, int b)
    {
        var pa = _nodes[a];
        var pb = _nodes[b];
        if (Collision.CollidesWithWalls(pa, pb, _bsp)) return;
        double cost = (pb - pa).Length();
        _adjacency[a].Add(new Edge(b, cost));
        _adjacency[b].Add(new Edge(a, cost));
    }

    public int FindClosestNode(Vector2 p)
    {
        int bestIndex = -1;
        double best = double.MaxValue;
        for (int i = 0; i < _nodes.Count; i++)
        {
            double d = (_nodes[i] - p).Length();
            if (d < best)
            {
                best = d;
                bestIndex = i;
            }
        }
        return bestIndex;
    }
}

public readonly struct Edge
{
    public int To { get; }
    public double Cost { get; }
    public Edge(int to, double cost)
    {
        To = to;
        Cost = cost;
    }
}

public static class AStarPathfinder
{
    public static DynamicArray<int> FindPath(WaypointGraph graph, int start, int goal)
    {
        var open = new MinHeap<OpenNode>((a, b) => a.F.CompareTo(b.F));
        var cameFrom = new Dictionary<int, int>();
        var gScore = new Dictionary<int, double>();
        var closed = new HashSet<int>();

        for (int i = 0; i < graph.NodeCount; i++) gScore[i] = double.MaxValue;
        gScore[start] = 0;

        open.Push(new OpenNode(start, Heuristic(graph, start, goal), 0));

        while (open.Count > 0)
        {
            var current = open.Pop();
            if (closed.Contains(current.Node)) continue;
            if (current.Node == goal) return ReconstructPath(cameFrom, current.Node);
            closed.Add(current.Node);

            var edges = graph.GetEdges(current.Node);
            for (int i = 0; i < edges.Count; i++)
            {
                var edge = edges[i];
                if (closed.Contains(edge.To)) continue;

                double tentativeG = gScore[current.Node] + edge.Cost;
                if (tentativeG < gScore[edge.To])
                {
                    cameFrom[edge.To] = current.Node;
                    gScore[edge.To] = tentativeG;
                    double h = Heuristic(graph, edge.To, goal);
                    open.Push(new OpenNode(edge.To, tentativeG + h, tentativeG));
                }
            }
        }

        return new DynamicArray<int>();
    }

    private static DynamicArray<int> ReconstructPath(Dictionary<int, int> cameFrom, int current)
    {
        var reverse = new DynamicArray<int>();
        reverse.Add(current);
        while (cameFrom.TryGetValue(current, out int parent))
        {
            reverse.Add(parent);
            current = parent;
        }

        var path = new DynamicArray<int>(reverse.Count);
        for (int i = reverse.Count - 1; i >= 0; i--) path.Add(reverse[i]);
        return path;
    }

    private static double Heuristic(WaypointGraph graph, int a, int b)
        => (graph.GetPosition(a) - graph.GetPosition(b)).Length();

    private readonly struct OpenNode
    {
        public int Node { get; }
        public double F { get; }
        public double G { get; }
        public OpenNode(int node, double f, double g)
        {
            Node = node;
            F = f;
            G = g;
        }
    }
}
