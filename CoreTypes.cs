using System;
using System.Drawing;

namespace DataStructures_SVS
{
    // 2 boyutlu vektor / nokta. Konum, yon ve geometrik hesaplarda kullanilir.
    public class Vector2D
    {
        public float X { get; set; }
        public float Y { get; set; }

        public Vector2D(float x, float y)
        {
            X = x;
            Y = y;
        }

        public PointF ToPointF() => new PointF(X, Y);

        public Vector2D Clone() => new Vector2D(X, Y);

        public float Length => (float)Math.Sqrt(X * X + Y * Y);

        // İki nokta arası Öklid mesafesi
        public static float Distance(Vector2D v1, Vector2D v2)
        {
            float dx = v1.X - v2.X;
            float dy = v1.Y - v2.Y;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        public static float DistanceSquared(Vector2D v1, Vector2D v2)
        {
            float dx = v1.X - v2.X;
            float dy = v1.Y - v2.Y;
            return dx * dx + dy * dy;
        }
    }

    // Bir duvar parçası (iki nokta arasındaki doğru segmenti)
    public class Segment
    {
        public Vector2D Start { get; set; }
        public Vector2D End { get; set; }

        public Segment(Vector2D start, Vector2D end)
        {
            Start = start;
            End = end;
        }

        public Segment(float x1, float y1, float x2, float y2)
        {
            Start = new Vector2D(x1, y1);
            End = new Vector2D(x2, y2);
        }
    }
}
