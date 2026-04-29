using System;
using System.Collections.Generic;

namespace StealthVisionSystem;

public sealed class BspTree
{
    private readonly BspNode? _root;

    public BspTree(DynamicArray<WallSegment> walls)
    {
        var arr = walls.ToArray();
        _root = Build(arr, 0, arr.Length - 1, 0);
    }

    public DynamicArray<WallSegment> QueryCandidates(Vector2 from, Vector2 to)
    {
        var result = new DynamicArray<WallSegment>();
        Query(_root, from, to, result);
        return result;
    }

    private static BspNode? Build(WallSegment[] arr, int start, int end, int depth)
    {
        if (start > end) return null;
        int axis = depth % 2;
        Array.Sort(arr, start, end - start + 1, Comparer<WallSegment>.Create((x, y) =>
        {
            double vx = axis == 0 ? x.MidPoint.X : x.MidPoint.Y;
            double vy = axis == 0 ? y.MidPoint.X : y.MidPoint.Y;
            return vx.CompareTo(vy);
        }));

        int mid = (start + end) / 2;
        return new BspNode(
            arr[mid],
            axis,
            Build(arr, start, mid - 1, depth + 1),
            Build(arr, mid + 1, end, depth + 1));
    }

    private static void Query(BspNode? node, Vector2 from, Vector2 to, DynamicArray<WallSegment> result)
    {
        if (node is null) return;
        result.Add(node.PartitionWall);

        double f = node.Axis == 0 ? from.X : from.Y;
        double t = node.Axis == 0 ? to.X : to.Y;
        double p = node.Axis == 0 ? node.PartitionWall.MidPoint.X : node.PartitionWall.MidPoint.Y;

        bool fromLeft = f <= p;
        bool toLeft = t <= p;

        if (fromLeft && toLeft) Query(node.Left, from, to, result);
        else if (!fromLeft && !toLeft) Query(node.Right, from, to, result);
        else
        {
            Query(node.Left, from, to, result);
            Query(node.Right, from, to, result);
        }
    }
}

public sealed class BspNode
{
    public WallSegment PartitionWall { get; }
    public int Axis { get; }
    public BspNode? Left { get; }
    public BspNode? Right { get; }

    public BspNode(WallSegment partitionWall, int axis, BspNode? left, BspNode? right)
    {
        PartitionWall = partitionWall;
        Axis = axis;
        Left = left;
        Right = right;
    }
}

public static class Visibility
{
    public static bool HasLineOfSight(Vector2 from, Vector2 to, BspTree bsp)
    {
        var hit = CastRay(from, (to - from).Normalize(), (to - from).Length(), bsp);
        return !hit.Intersects;
    }

    public static DynamicArray<RayHit> CastFovRays(Enemy enemy, BspTree bsp, int rayCount)
    {
        var hits = new DynamicArray<RayHit>();
        if (rayCount < 2) rayCount = 2;

        double start = enemy.DirectionRadians - enemy.FovRadians / 2.0;
        double step = enemy.FovRadians / (rayCount - 1);

        for (int i = 0; i < rayCount; i++)
        {
            double angle = start + i * step;
            var dir = new Vector2(Math.Cos(angle), Math.Sin(angle));
            hits.Add(CastRay(enemy.Position, dir, enemy.ViewDistance, bsp));
        }

        return hits;
    }

    public static RayHit CastRay(Vector2 origin, Vector2 direction, double maxDistance, BspTree bsp)
    {
        var end = origin + direction * maxDistance;
        var candidates = bsp.QueryCandidates(origin, end);

        bool found = false;
        double bestDistance = maxDistance;
        Vector2 bestPoint = end;

        for (int i = 0; i < candidates.Count; i++)
        {
            var wall = candidates[i];
            if (Geometry.TryRaySegmentIntersection(origin, direction, wall.A, wall.B, out var p, out var t))
            {
                if (t >= 0 && t < bestDistance)
                {
                    bestDistance = t;
                    bestPoint = p;
                    found = true;
                }
            }
        }

        return new RayHit(found, bestDistance, bestPoint);
    }
}
///
public static class Collision
{
    public static bool CollidesWithWalls(Vector2 start, Vector2 end, BspTree bsp)
    {
        var candidates = bsp.QueryCandidates(start, end);
        for (int i = 0; i < candidates.Count; i++)
        {
            var wall = candidates[i];
            if (Geometry.SegmentsIntersect(start, end, wall.A, wall.B)) return true;
        }
        return false;
    }
}

public static class Geometry
{
    public static bool TryRaySegmentIntersection(
        Vector2 rayOrigin, Vector2 rayDir, Vector2 segA, Vector2 segB,
        out Vector2 intersection, out double rayT)
    {
        var v1 = rayOrigin - segA;
        var v2 = segB - segA;
        var v3 = new Vector2(-rayDir.Y, rayDir.X);

        double dot = Vector2.Dot(v2, v3);
        if (Math.Abs(dot) < 1e-9)
        {
            intersection = default;
            rayT = 0;
            return false;
        }

        double t1 = Vector2.Cross(v2, v1) / dot;
        double t2 = Vector2.Dot(v1, v3) / dot;

        if (t1 >= 0.0 && t2 >= 0.0 && t2 <= 1.0)
        {
            intersection = rayOrigin + rayDir * t1;
            rayT = t1;
            return true;
        }

        intersection = default;
        rayT = 0;
        return false;
    }

    public static bool SegmentsIntersect(Vector2 p1, Vector2 p2, Vector2 q1, Vector2 q2)
    {
        const double eps = 1e-9;
        double o1 = Orientation(p1, p2, q1);
        double o2 = Orientation(p1, p2, q2);
        double o3 = Orientation(q1, q2, p1);
        double o4 = Orientation(q1, q2, p2);

        if ((o1 > eps && o2 < -eps || o1 < -eps && o2 > eps) &&
            (o3 > eps && o4 < -eps || o3 < -eps && o4 > eps))
        {
            return true;
        }

        if (Math.Abs(o1) <= eps && OnSegment(p1, q1, p2, eps)) return true;
        if (Math.Abs(o2) <= eps && OnSegment(p1, q2, p2, eps)) return true;
        if (Math.Abs(o3) <= eps && OnSegment(q1, p1, q2, eps)) return true;
        if (Math.Abs(o4) <= eps && OnSegment(q1, p2, q2, eps)) return true;

        return false;
    }

    private static double Orientation(Vector2 a, Vector2 b, Vector2 c)
        => Vector2.Cross(b - a, c - a);

    private static bool OnSegment(Vector2 a, Vector2 b, Vector2 c, double eps)
    {
        return b.X <= Math.Max(a.X, c.X) + eps &&
               b.X + eps >= Math.Min(a.X, c.X) &&
               b.Y <= Math.Max(a.Y, c.Y) + eps &&
               b.Y + eps >= Math.Min(a.Y, c.Y);
    }
}
