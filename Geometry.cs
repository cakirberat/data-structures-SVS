using System;

namespace DataStructures_SVS
{
    // Geometrik yardimci fonksiyonlar: kesisim, isin (ray) testi, cember-segment mesafesi.
    // Cokme tespiti ve gorus alani (FOV) hesaplamalarinda kullanilir.
    public static class Geometry
    {
        private const float EPS = 0.0001f;

        // Iki segment kesisiyor mu? (0 <= t,s <= 1 araliginda)
        public static bool SegmentsIntersect(Vector2D p1, Vector2D p2, Vector2D p3, Vector2D p4)
        {
            float denominator = ((p2.X - p1.X) * (p4.Y - p3.Y)) - ((p2.Y - p1.Y) * (p4.X - p3.X));
            if (Math.Abs(denominator) < EPS) return false;

            float numerator1 = ((p1.Y - p3.Y) * (p4.X - p3.X)) - ((p1.X - p3.X) * (p4.Y - p3.Y));
            float numerator2 = ((p1.Y - p3.Y) * (p2.X - p1.X)) - ((p1.X - p3.X) * (p2.Y - p1.Y));

            float r = numerator1 / denominator;
            float s = numerator2 / denominator;

            return (r >= 0 && r <= 1) && (s >= 0 && s <= 1);
        }

        // Iki segment kesisiyor mu? (uc nokta toleransi ile - kose temaslari icin)
        public static bool SegmentsIntersectStrict(Vector2D p1, Vector2D p2, Vector2D p3, Vector2D p4)
        {
            float denominator = ((p2.X - p1.X) * (p4.Y - p3.Y)) - ((p2.Y - p1.Y) * (p4.X - p3.X));
            if (Math.Abs(denominator) < EPS) return false;

            float numerator1 = ((p1.Y - p3.Y) * (p4.X - p3.X)) - ((p1.X - p3.X) * (p4.Y - p3.Y));
            float numerator2 = ((p1.Y - p3.Y) * (p2.X - p1.X)) - ((p1.X - p3.X) * (p2.Y - p1.Y));

            float r = numerator1 / denominator;
            float s = numerator2 / denominator;

            return (r > EPS && r < 1 - EPS) && (s > EPS && s < 1 - EPS);
        }

        // Sonsuz isin (origin + direction * maxDist) ile segment kesisimi.
        // Duvarlar gorus alanini keserken en yakin kesisim noktasini bulmak icin kullanilir.
        public static bool RaySegmentIntersect(Vector2D origin, Vector2D direction, float maxDist,
            Vector2D segStart, Vector2D segEnd, out float hitDistance)
        {
            hitDistance = maxDist;

            float rx = direction.X;
            float ry = direction.Y;
            float sx = segEnd.X - segStart.X;
            float sy = segEnd.Y - segStart.Y;

            float denom = rx * sy - ry * sx;
            if (Math.Abs(denom) < EPS) return false;

            float ox = segStart.X - origin.X;
            float oy = segStart.Y - origin.Y;

            float t = (ox * sy - oy * sx) / denom;
            float u = (ox * ry - oy * rx) / denom;

            if (t >= EPS && t <= maxDist && u >= 0 && u <= 1)
            {
                hitDistance = t;
                return true;
            }
            return false;
        }

        // Noktanin bir segmente olan en kisa mesafesi (cember-duvar cokmesi icin)
        public static float PointToSegmentDistance(Vector2D p, Vector2D a, Vector2D b)
        {
            float dx = b.X - a.X;
            float dy = b.Y - a.Y;
            float lenSq = dx * dx + dy * dy;
            if (lenSq < EPS) return Vector2D.Distance(p, a);

            float t = ((p.X - a.X) * dx + (p.Y - a.Y) * dy) / lenSq;
            if (t < 0) t = 0;
            else if (t > 1) t = 1;

            float px = a.X + t * dx;
            float py = a.Y + t * dy;
            return Vector2D.Distance(p, new Vector2D(px, py));
        }

        // Hareket eden bir daire (from -> to, yaricap r) herhangi bir duvarla cakisiyor mu?
        // Oyuncu ve dusmanlarin duvar icinden gecmesini engeller.
        public static bool CircleHitsWalls(Vector2D from, Vector2D to, float radius, DynamicArray<Segment> walls)
        {
            for (int i = 0; i < walls.Count; i++)
            {
                Segment w = walls[i];
                if (PointToSegmentDistance(to, w.Start, w.End) < radius - EPS) return true;
                if (SegmentsIntersectStrict(from, to, w.Start, w.End)) return true;
            }
            return false;
        }

        // Noktada entity yaricapi kadar bos alan var mi?
        public static bool IsPositionFree(Vector2D pos, float radius, DynamicArray<Segment> walls)
        {
            for (int i = 0; i < walls.Count; i++)
            {
                if (PointToSegmentDistance(pos, walls[i].Start, walls[i].End) < radius - EPS)
                    return false;
            }
            return true;
        }

        // Iki nokta arasi hem LOS hem de dar gecitlere sigacak kadar genis mi?
        public static bool PathClearForEntity(Vector2D from, Vector2D to, float radius,
            BspTree bsp, DynamicArray<Segment> walls)
        {
            if (!HasLineOfSight(from, to, bsp, walls)) return false;

            const int samples = 6;
            for (int i = 0; i <= samples; i++)
            {
                float t = i / (float)samples;
                var p = new Vector2D(
                    from.X + (to.X - from.X) * t,
                    from.Y + (to.Y - from.Y) * t);
                if (!IsPositionFree(p, radius, walls)) return false;
            }
            return true;
        }

        // Iki nokta arasinda duvar var mi? (BSP veya dogrudan tarama)
        public static bool HasLineOfSight(Vector2D from, Vector2D to, BspTree bsp, DynamicArray<Segment> walls)
        {
            if (bsp != null && bsp.Root != null)
                return !bsp.SegmentIntersectsWall(from, to);

            for (int i = 0; i < walls.Count; i++)
            {
                if (SegmentsIntersect(from, to, walls[i].Start, walls[i].End))
                    return false;
            }
            return true;
        }

        // Dusman gorus konisi: duvarlara carpan isinlarla sinirli FOV poligonu.
        // Raycasting + BSP: her acida isin atilir, en yakin duvar kesisimi alinir.
        public static DynamicArray<Vector2D> ComputeFieldOfView(Vector2D origin, float facingAngleDeg,
            float radius, float halfAngleDeg, DynamicArray<Segment> walls, BspTree bsp, int rayCount)
        {
            var polygon = new DynamicArray<Vector2D>();
            polygon.Add(origin.Clone());

            float startAngle = facingAngleDeg - halfAngleDeg;
            float endAngle = facingAngleDeg + halfAngleDeg;
            float step = (endAngle - startAngle) / rayCount;

            for (int i = 0; i <= rayCount; i++)
            {
                float angleDeg = startAngle + step * i;
                float rad = angleDeg * (float)(Math.PI / 180.0);
                var dir = new Vector2D((float)Math.Cos(rad), (float)Math.Sin(rad));
                float hitDist = radius;

                for (int w = 0; w < walls.Count; w++)
                {
                    float d;
                    if (RaySegmentIntersect(origin, dir, radius, walls[w].Start, walls[w].End, out d))
                    {
                        if (d < hitDist) hitDist = d;
                    }
                }

                polygon.Add(new Vector2D(
                    origin.X + dir.X * hitDist,
                    origin.Y + dir.Y * hitDist));
            }

            return polygon;
        }

        // Oyuncu FOV poligonunun icinde mi? (ucgen fan testi)
        public static bool IsPointInFovPolygon(Vector2D point, DynamicArray<Vector2D> polygon)
        {
            if (polygon.Count < 3) return false;

            for (int i = 1; i < polygon.Count - 1; i++)
            {
                if (PointInTriangle(point, polygon[0], polygon[i], polygon[i + 1]))
                    return true;
            }
            return false;
        }

        private static bool PointInTriangle(Vector2D p, Vector2D a, Vector2D b, Vector2D c)
        {
            float d1 = Sign(p, a, b);
            float d2 = Sign(p, b, c);
            float d3 = Sign(p, c, a);

            bool hasNeg = (d1 < 0) || (d2 < 0) || (d3 < 0);
            bool hasPos = (d1 > 0) || (d2 > 0) || (d3 > 0);
            return !(hasNeg && hasPos);
        }

        private static float Sign(Vector2D p1, Vector2D p2, Vector2D p3)
        {
            return (p1.X - p3.X) * (p2.Y - p3.Y) - (p2.X - p3.X) * (p1.Y - p3.Y);
        }
    }
}
