using System;
using System.Collections.Generic;

namespace DataStructures_SVS
{
    // Seviye bazli navigasyon grafigi (bir kez olusturulur)
    public class LevelNavigationGraph
    {
        private WaypointGraph graph = new WaypointGraph();
        private List<Vector2D> nodePositions = new List<Vector2D>();
        private float pathRadius;

        public int NodeCount => nodePositions.Count;

        public void Build(List<Vector2D> waypoints, DynamicArray<Segment> walls, BspTree bsp, float entityRadius)
        {
            graph = new WaypointGraph();
            nodePositions.Clear();
            pathRadius = entityRadius;

            for (int i = 0; i < waypoints.Count; i++)
            {
                if (!Geometry.IsPositionFree(waypoints[i], entityRadius, walls))
                    continue;
                graph.AddNode(waypoints[i].Clone());
                nodePositions.Add(waypoints[i].Clone());
            }

            graph.BuildEdges(walls, bsp, entityRadius);
        }

        public int FindNearestReachableNode(Vector2D from, Vector2D toHint, DynamicArray<Segment> walls, BspTree bsp)
        {
            // 1. Geçiş: doğrudan LOS + yarıçap temizliği olan en iyi düğüm
            int best = -1;
            float bestScore = float.MaxValue;

            for (int i = 0; i < nodePositions.Count; i++)
            {
                if (!Geometry.PathClearForEntity(from, nodePositions[i], pathRadius, bsp, walls))
                    continue;

                float dFrom = Vector2D.Distance(from, nodePositions[i]);
                float dHint = Vector2D.Distance(nodePositions[i], toHint);
                float score = dFrom + dHint * 0.15f;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = i;
                }
            }
            if (best >= 0) return best;

            // 2. Geçiş fallback: LOS şartı olmadan sadece en yakın düğüm.
            // Düşman duvara sıkışmış veya dar köşedeyse bu sayede grafiğe bağlanır.
            return FindNearestNode(from);
        }

        public int FindNearestNode(Vector2D pos)
        {
            int best = -1;
            float bestDist = float.MaxValue;
            for (int i = 0; i < nodePositions.Count; i++)
            {
                float d = Vector2D.Distance(pos, nodePositions[i]);
                if (d < bestDist) { bestDist = d; best = i; }
            }
            return best;
        }

        // En yakın BAĞLI düğümü döndürür (kenarsız izole düğümleri atlar).
        // A* hedef seçiminde kullanılır: izole düğüm hedef olursa A* boş döner.
        public int FindNearestConnectedNode(Vector2D pos)
        {
            int best = -1;
            float bestDist = float.MaxValue;
            for (int i = 0; i < nodePositions.Count; i++)
            {
                if (graph.GetNeighbors(i).Count == 0) continue; // kenar yok → izole
                float d = Vector2D.Distance(pos, nodePositions[i]);
                if (d < bestDist) { bestDist = d; best = i; }
            }
            return best >= 0 ? best : FindNearestNode(pos); // tam fallback
        }

        public Vector2D GetNodePosition(int nodeId) => nodePositions[nodeId];

        public List<Vector2D> GetNodePositions() => nodePositions;

        public WaypointGraph Graph => graph;
    }

    // Devriye rotasi ve hareket yardimcilari
    public static class PatrolSystem
    {
        public const float ArrivalDistance = 18f;
        // GraphPathRadius > entityRadius (10f) olmalı: yol planlayıcı entityRadius'tan
        // geniş geçitler için kenar kurar. Dar geçitler (örn. siper sonu ↔ üst bölücü
        // arası 40px gap) otomatik elenir; entity asla sıkışmaz.
        public const float GraphPathRadius = 12f;
        public const float MoveRadius = 10f;

        public static List<Vector2D> BuildRouteFromSpawn(EnemySpawn spawn, DynamicArray<Segment> walls, BspTree bsp)
        {
            var route = new List<Vector2D>();

            if (spawn.PatrolPoints != null && spawn.PatrolPoints.Count > 0)
            {
                foreach (Vector2D p in spawn.PatrolPoints)
                {
                    if (Geometry.IsPositionFree(p, MoveRadius, walls))
                        route.Add(p.Clone());
                }
            }

            if (route.Count == 0)
            {
                route.Add(spawn.Position.Clone());
                return route;
            }

            return FilterConnectedRoute(route, walls, bsp);
        }

        // Ard arda baglantisi olmayan noktalari atla
        private static List<Vector2D> FilterConnectedRoute(List<Vector2D> points, DynamicArray<Segment> walls, BspTree bsp)
        {
            if (points.Count <= 1) return points;

            var filtered = new List<Vector2D>();
            filtered.Add(points[0].Clone());

            for (int i = 1; i < points.Count; i++)
            {
                Vector2D prev = filtered[filtered.Count - 1];
                if (Geometry.PathClearForEntity(prev, points[i], GraphPathRadius, bsp, walls))
                    filtered.Add(points[i].Clone());
            }

            if (filtered.Count < 2 && points.Count >= 2)
                filtered.Add(points[points.Count - 1].Clone());

            return filtered;
        }

        public static int FindStartPatrolIndex(List<Vector2D> route, Vector2D position)
        {
            if (route.Count == 0) return 0;

            for (int i = 0; i < route.Count; i++)
            {
                if (Vector2D.Distance(position, route[i]) >= ArrivalDistance)
                    return i;
            }
            return route.Count > 1 ? 1 : 0;
        }

        // 8 yon + hedef yonu ile duvar kaydirmali hareket
        public static bool MoveTowardTarget(ref Vector2D pos, Vector2D target, float speed,
            float radius, DynamicArray<Segment> walls)
        {
            float dx = target.X - pos.X;
            float dy = target.Y - pos.Y;
            float dist = (float)Math.Sqrt(dx * dx + dy * dy);
            if (dist < 0.001f) return false;

            float ndx = dx / dist;
            float ndy = dy / dist;

            if (TryStep(ref pos, pos.X + ndx * speed, pos.Y + ndy * speed, radius, walls))
                return true;

            float[] angles = { 0f, 30f, -30f, 60f, -60f, 90f, -90f, 120f, -120f };
            for (int i = 0; i < angles.Length; i++)
            {
                float rad = (float)((Math.Atan2(ndy, ndx) + angles[i] * Math.PI / 180.0));
                float sx = (float)Math.Cos(rad) * speed;
                float sy = (float)Math.Sin(rad) * speed;
                if (TryStep(ref pos, pos.X + sx, pos.Y + sy, radius, walls))
                    return true;
            }

            if (TryStep(ref pos, pos.X + Math.Sign(dx) * speed, pos.Y, radius, walls)) return true;
            if (TryStep(ref pos, pos.X, pos.Y + Math.Sign(dy) * speed, radius, walls)) return true;

            return false;
        }

        private static bool TryStep(ref Vector2D pos, float nx, float ny, float radius, DynamicArray<Segment> walls)
        {
            Vector2D next = new Vector2D(nx, ny);
            if (!Geometry.CircleHitsWalls(pos, next, radius, walls))
            {
                pos.X = nx;
                pos.Y = ny;
                return true;
            }

            bool moved = false;
            if (!Geometry.CircleHitsWalls(pos, new Vector2D(nx, pos.Y), radius, walls))
            {
                pos.X = nx;
                moved = true;
            }
            if (!Geometry.CircleHitsWalls(pos, new Vector2D(pos.X, ny), radius, walls))
            {
                pos.Y = ny;
                moved = true;
            }
            return moved;
        }
    }
}
