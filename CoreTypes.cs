using System;

namespace StealthVisionSystem;

public readonly struct Vector2
{
    public double X { get; }
    public double Y { get; }

    public Vector2(double x, double y)
    {
        X = x;
        Y = y;
    }

    public static Vector2 operator +(Vector2 a, Vector2 b) => new(a.X + b.X, a.Y + b.Y);
    public static Vector2 operator -(Vector2 a, Vector2 b) => new(a.X - b.X, a.Y - b.Y);
    public static Vector2 operator *(Vector2 a, double t) => new(a.X * t, a.Y * t);

    public double Length() => Math.Sqrt(X * X + Y * Y);

    public Vector2 Normalize()
    {
        double len = Length();
        return len < 1e-9 ? new Vector2(0, 0) : new Vector2(X / len, Y / len);
    }

    public static double Dot(Vector2 a, Vector2 b) => a.X * b.X + a.Y * b.Y;
    public static double Cross(Vector2 a, Vector2 b) => a.X * b.Y - a.Y * b.X;

    public override string ToString() => $"({X:0.00}, {Y:0.00})";
}

public readonly struct WallSegment
{
    public Vector2 A { get; }
    public Vector2 B { get; }
    public Vector2 MidPoint => new((A.X + B.X) / 2.0, (A.Y + B.Y) / 2.0);

    public WallSegment(Vector2 a, Vector2 b)
    {
        A = a;
        B = b;
    }
}

public readonly struct RayHit
{
    public bool Intersects { get; }
    public double Distance { get; }
    public Vector2 Point { get; }

    public RayHit(bool intersects, double distance, Vector2 point)
    {
        Intersects = intersects;
        Distance = distance;
        Point = point;
    }
}

public sealed class Enemy
{
    public Vector2 Position { get; private set; }
    public double DirectionRadians { get; private set; }
    public double FovRadians { get; }
    public double ViewDistance { get; }

    public Enemy(Vector2 position, double directionRadians, double fovRadians, double viewDistance)
    {
        Position = position;
        DirectionRadians = directionRadians;
        FovRadians = fovRadians;
        ViewDistance = viewDistance;
    }
}
