using System;
using System.Windows.Forms;

namespace StealthVisionSystem;

public static class Program
{
    [STAThread]
    public static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new GameForm());
    }

    public static DynamicArray<WallSegment> BuildWalls()
    {
        var walls = new DynamicArray<WallSegment>();

        AddRectangle(walls, 0, 0, 16, 10);
        AddRectangle(walls, 4, 1, 6, 5);
        AddRectangle(walls, 9, 5, 12, 8);

        return walls;
    }

    private static void AddRectangle(DynamicArray<WallSegment> walls, double x1, double y1, double x2, double y2)
    {
        var a = new Vector2(x1, y1);
        var b = new Vector2(x2, y1);
        var c = new Vector2(x2, y2);
        var d = new Vector2(x1, y2);

        walls.Add(new WallSegment(a, b));
        walls.Add(new WallSegment(b, c));
        walls.Add(new WallSegment(c, d));
        walls.Add(new WallSegment(d, a));
    }

    public static WaypointGraph BuildWaypointGraph(DynamicArray<WallSegment> walls)
    {
        var graph = new WaypointGraph(walls);

        int n0 = graph.AddNode(new Vector2(2, 2));
        int n1 = graph.AddNode(new Vector2(3, 7));
        int n2 = graph.AddNode(new Vector2(7, 7));
        int n3 = graph.AddNode(new Vector2(8, 2));
        int n4 = graph.AddNode(new Vector2(13, 2));
        int n5 = graph.AddNode(new Vector2(13, 7));

        graph.ConnectIfVisible(n0, n1);
        graph.ConnectIfVisible(n1, n2);
        graph.ConnectIfVisible(n2, n5);
        graph.ConnectIfVisible(n0, n3);
        graph.ConnectIfVisible(n3, n4);
        graph.ConnectIfVisible(n4, n5);
        graph.ConnectIfVisible(n2, n3);

        return graph;
    }
}
