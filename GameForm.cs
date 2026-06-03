using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using System.Collections.Generic;

namespace DataStructures_SVS
{
    public enum GameState { Playing, Won, Lost, LevelComplete }

    public class EnemyData
    {
        public Vector2D Position;
        public float Angle;
        public List<Vector2D> CurrentPath = new List<Vector2D>();
        public List<Vector2D> PatrolRoute = new List<Vector2D>();
        public int PatrolIndex;
        public int PathUpdateCounter;
        public int StuckFrames;
        public float LastX;
        public float LastY;
        public int ZoneIndex;
        public int IdleFramesLeft;      // hedefe varinca bekle
        public bool AlertFlash;         // gorunce uyari
        public int AlertFlashFrames;
        public Color ThemeColor;
        public DynamicArray<Vector2D> FovPolygon = new DynamicArray<Vector2D>();

        public EnemyData(Vector2D startPos, Color color, float startAngle, int zoneIndex)
        {
            Position = startPos.Clone();
            ThemeColor = color;
            Angle = startAngle;
            ZoneIndex = zoneIndex;
            LastX = startPos.X;
            LastY = startPos.Y;
        }

        public Vector2D GetPatrolTarget()
        {
            if (PatrolRoute.Count == 0) return Position.Clone();
            if (PatrolIndex >= PatrolRoute.Count) PatrolIndex = 0;
            return PatrolRoute[PatrolIndex];
        }
    }

    public partial class GameForm : Form
    {
        private Timer gameLoopTimer;

        private Vector2D player;
        private Vector2D targetExit;
        private DynamicArray<Segment> walls;
        private List<Vector2D> waypoints;
        private List<EnemyData> enemies;

        private BspTree bspTree;
        private AStarPathfinder pathfinder;
        private LevelNavigationGraph navGraph;

        private GameState currentState;
        private int currentLevelIndex;
        private string levelName;
        private string levelDescription;

        private bool moveUp, moveDown, moveLeft, moveRight;

        private float playerSpeed = 4f;
        private float enemySpeed = 2.5f;
        private float entityRadius = 10f;
        private float fovRadius = 250f;
        private float fovHalfAngle = 30f;
        private int fovRayCount = 68;

        private const int HudBarHeight = 60;           // daha ince HUD, haritaya alan kalsin
        private const int PathUpdateInterval = 12;
        private const float PatrolArrivalDist = 28f;
        private const float PathClearanceRadius = 10f;
        private const float RoutePointSep = 80f;       // rota noktaları arası min mesafe (düşürüldü)
        private const int StuckFrameLimit = 60;        // takılma eşiği: 1 sn (eski 30 çok erken replan yapıyordu)
        private const int StuckSkipLimit = 120;        // 2 sn sonra patrol index'i atla
        private const int EnemyMoveSubSteps = 4;
        private const int IdleFrames = 50;
        private const int AlertFlashDuration = 20;

        private float mapRenderScale = 1f;
        private float exitPulse = 0f;
        private float _playerMoveAngle = 0f;        // oyuncu hareket yonu (derece)

        // Font/Brush onbellegi (her kare yeni nesne yaratmayi onler)
        private Font _hudFont;
        private Font _titleFont;
        private Font _bigFont;
        private Font _alertFont;

        public GameForm()
        {
            DoubleBuffered = true;
            Text = "DataStructures_SVS - BSP Gorus ve Cokusma";
            BackColor = Color.FromArgb(25, 25, 30);
            StartPosition = FormStartPosition.CenterScreen;

            // Ekranin kullanilabilir alanina gore boyutu dinamik sec
            // (taskbar, DPI scaling ve farkli cozunurluklerle uyumlu)
            Rectangle work = Screen.PrimaryScreen.WorkingArea;
            int formW = Math.Min(1280, work.Width);
            int formH = Math.Min(880, work.Height - 8); // 8: pencere cercevesi payi
            ClientSize = new Size(formW, formH);

            _hudFont   = new Font("Segoe UI", 10f);
            _titleFont = new Font("Segoe UI", 11f, FontStyle.Bold);
            _bigFont   = new Font("Arial", 22f, FontStyle.Bold);
            _alertFont = new Font("Arial", 14f, FontStyle.Bold);

            pathfinder = new AStarPathfinder();
            currentLevelIndex = 0;
            LoadLevel(currentLevelIndex);

            gameLoopTimer = new Timer();
            gameLoopTimer.Interval = 16;
            gameLoopTimer.Tick += GameLoopTick;
            gameLoopTimer.Start();

            KeyDown += GameForm_KeyDown;
            KeyUp += GameForm_KeyUp;
        }

        private void LoadLevel(int levelIndex)
        {
            currentLevelIndex = levelIndex;
            LevelDefinition level = LevelManager.GetLevel(levelIndex);

            currentState = GameState.Playing;
            levelName = level.Name;
            levelDescription = level.Description;

            // Seviyeye gore artan zorluk
            switch (levelIndex)
            {
                case 0: enemySpeed = 2.3f; fovHalfAngle = 28f; fovRadius = 240f; break;
                case 1: enemySpeed = 2.8f; fovHalfAngle = 33f; fovRadius = 265f; break;
                case 2: enemySpeed = 3.3f; fovHalfAngle = 38f; fovRadius = 290f; break;
                default: enemySpeed = 2.5f; fovHalfAngle = 30f; fovRadius = 250f; break;
            }

            player = level.PlayerStart.Clone();
            targetExit = level.Exit.Clone();

            walls = level.Walls;
            waypoints = new List<Vector2D>(level.Waypoints);

            bspTree = new BspTree();
            bspTree.Build(walls);

            navGraph = new LevelNavigationGraph();
            navGraph.Build(waypoints, walls, bspTree, PatrolSystem.GraphPathRadius);

            enemies = new List<EnemyData>();
            var reservedWaypointIds = new HashSet<int>();
            var priorRoutes = new List<List<Vector2D>>();
            int enemyCount = level.EnemySpawns.Count;

            for (int i = 0; i < enemyCount; i++)
            {
                EnemySpawn spawn = level.EnemySpawns[i];
                var enemy = new EnemyData(spawn.Position, spawn.Color, spawn.StartAngle, i);

                // Seviye tanımında PatrolPoints belirtilmişse sabit rota kullan;
                // yoksa dinamik BuildPatrolRoute hesapla (fallback).
                if (spawn.PatrolPoints != null && spawn.PatrolPoints.Count >= 2)
                {
                    enemy.PatrolRoute = new List<Vector2D>();
                    foreach (Vector2D pt in spawn.PatrolPoints)
                        enemy.PatrolRoute.Add(pt.Clone());
                }
                else
                {
                    enemy.PatrolRoute = BuildPatrolRoute(spawn.Position, reservedWaypointIds, priorRoutes, i, enemyCount);
                }

                enemy.PatrolIndex = 0;
                enemy.Angle = spawn.StartAngle;
                enemy.IdleFramesLeft = 0;
                enemies.Add(enemy);
                priorRoutes.Add(enemy.PatrolRoute);
            }

            moveUp = moveDown = moveLeft = moveRight = false;
        }

        // ─── Devriye rota olusturucu (yeniden yazildi) ───────────────────────────
        //
        // Strateji:
        //  1. Rezerveli ve gecersiz (duvar icindeki) waypoint'leri ele.
        //  2. Kalan noktaları "bu dusmana yakin mi diger rotalara" kriteri ile
        //     ikiye böl: tercihli (uzak) ve geri kalan (yakin).
        //  3. Her iki havuzdan secim yap; harita merkezine gore aciya gore
        //     sirala → duzgün bir devre elde et.
        //  4. A* navigasyonu arasi mesafeyi halleder; rota olusturmada
        //     PathClearForEntity kontrolü KALDIRILIYOR (cok katiydi, havuzu bosaltiyordu).
        //
        private List<Vector2D> BuildPatrolRoute(Vector2D spawnPos, HashSet<int> reservedIds,
            List<List<Vector2D>> priorRoutes, int enemyIndex, int totalEnemies)
        {
            // Adim 1: Kullanilabilir waypoint'leri topla
            var farPool  = new List<int>(); // diger rotalardan uzak
            var nearPool = new List<int>(); // diger rotalara yakin ama yine de geçerli

            for (int i = 0; i < waypoints.Count; i++)
            {
                if (reservedIds.Contains(i)) continue;
                if (!Geometry.IsPositionFree(waypoints[i], PathClearanceRadius, walls)) continue;

                bool tooClose = false;
                for (int r = 0; r < priorRoutes.Count; r++)
                    for (int p = 0; p < priorRoutes[r].Count; p++)
                        if (Vector2D.Distance(waypoints[i], priorRoutes[r][p]) < RoutePointSep)
                        { tooClose = true; break; }

                if (!tooClose) farPool.Add(i);
                else           nearPool.Add(i);
            }

            // Adim 1b: spawn'a cok yakin waypointleri preferred pool'dan cikar
            // (ilk rota noktasi spawn'in hemen yaninda olmamali)
            const float MinSpawnDist = 140f;
            var farPoolFiltered  = new List<int>();
            var nearPoolFiltered = new List<int>();
            foreach (int i in farPool)
                (Vector2D.Distance(waypoints[i], spawnPos) >= MinSpawnDist ? farPoolFiltered : nearPoolFiltered).Add(i);
            foreach (int i in nearPool)
                nearPoolFiltered.Add(i);
            // Eger uzak havuz yeterliyse onu kullan, degilse tum havuz
            if (farPoolFiltered.Count >= 2) { farPool = farPoolFiltered; }

            // Adim 2: Dusmanın sektöründeki noktaları önceliklendir
            float sectorSize  = 360f / Math.Max(1, totalEnemies);
            float sectorStart = enemyIndex * sectorSize;

            List<int> pool = farPool.Count >= 3 ? farPool
                           : (farPool.Count > 0 ? farPool : nearPool);

            if (pool.Count == 0)
            {
                // Son care: rezerve edilmis tum noktalar; sadece spawn'i dondur
                return new List<Vector2D> { spawnPos.Clone() };
            }

            // Adim 3: Sektor icindekiler once, geri kalanlar sonra – max 12 aday
            var sectorPool   = new List<int>();
            var outsidePool  = new List<int>();
            Vector2D mapCtr  = new Vector2D(
                (LevelManager.MapLeft + LevelManager.MapRight)  * 0.5f,
                (LevelManager.MapTop  + LevelManager.MapBottom) * 0.5f);

            for (int a = 0; a < pool.Count; a++)
            {
                int i = pool[a];
                float ang = (float)(Math.Atan2(waypoints[i].Y - spawnPos.Y,
                                               waypoints[i].X - spawnPos.X) * (180.0 / Math.PI));
                float rel = ((ang % 360f + 360f) % 360f - sectorStart + 360f) % 360f;
                if (rel < sectorSize) sectorPool.Add(i);
                else                  outsidePool.Add(i);
            }

            // Sektor yetersizse disaridan takviye yap
            var candidates = new List<int>(sectorPool);
            if (candidates.Count < 4)
                foreach (int x in outsidePool) { candidates.Add(x); if (candidates.Count >= 12) break; }
            else if (candidates.Count > 12)
                candidates.RemoveRange(12, candidates.Count - 12);

            // Adim 4: Aciya gore sirala → devriye devri düzgün olsun
            candidates.Sort((a, b) =>
            {
                float angA = (float)(Math.Atan2(waypoints[a].Y - mapCtr.Y,
                                                waypoints[a].X - mapCtr.X) * (180.0 / Math.PI));
                float angB = (float)(Math.Atan2(waypoints[b].Y - mapCtr.Y,
                                                waypoints[b].X - mapCtr.X) * (180.0 / Math.PI));
                return angA.CompareTo(angB);
            });

            // Adim 5: Esit aralikli secim (max 5 nokta)
            int want  = Math.Min(5, candidates.Count);
            int step  = Math.Max(1, candidates.Count / want);
            var route = new List<Vector2D>();

            for (int ci = 0; ci < candidates.Count && route.Count < want; ci += step)
            {
                int idx = candidates[ci];
                reservedIds.Add(idx);
                route.Add(waypoints[idx].Clone());
            }

            // Adim 6: Dogrulama – A* ile ulaşilamayan rota noktalarini cikar
            // (kapalı odalar veya keskin koseler nedeniyle erisim engeli olabilir)
            var validRoute = new List<Vector2D>();
            Vector2D checkFrom = spawnPos;
            foreach (Vector2D pt in route)
            {
                var testPath = pathfinder.FindPathToPosition(navGraph, checkFrom, pt, walls, bspTree);
                if (testPath.Count > 0 || Geometry.PathClearForEntity(checkFrom, pt, PathClearanceRadius, bspTree, walls))
                {
                    validRoute.Add(pt);
                    checkFrom = pt;
                }
                // Ulasilamazsa bu noktayi atla; sonraki noktaya dogrudan gidilir
            }
            if (validRoute.Count > 0) route = validRoute;

            // NOT: spawnPos rotaya EKLENMEZ.
            // Eklenseydi: enemy frame-1'de "zaten orada" tespiti yapip idle'a girerdi.
            // Bunun yerine enemy hemen ilk waypoint'e hareket eder.
            if (route.Count == 0)
            {
                // Hicbir waypoint bulunamadi – spawn yakini fallback
                route.Add(spawnPos.Clone());
                float ox = spawnPos.X + (mapCtr.X - spawnPos.X) * 0.4f;
                float oy = spawnPos.Y + (mapCtr.Y - spawnPos.Y) * 0.4f;
                route.Add(new Vector2D(ox, oy));
            }

            // Tek nokta korumasi
            if (route.Count == 1)
            {
                float ox = route[0].X + (mapCtr.X - route[0].X) * 0.5f;
                float oy = route[0].Y + (mapCtr.Y - route[0].Y) * 0.5f;
                var opp = new Vector2D(ox, oy);
                if (Geometry.IsPositionFree(opp, PathClearanceRadius, walls))
                    route.Add(opp);
                else
                    route.Add(mapCtr.Clone());
            }

            // Tek sayili dusmanlar ters yonde devriye atar
            if (enemyIndex % 2 == 1 && route.Count > 1)
                route.Reverse();

            return route;
        }

        // Gittigi yone bak: A* yolundaki sonraki dugum veya devriye hedefi.
        // Idle sirasinda: yava yava donme (etraf gozetleme efekti).
        private void UpdateEnemyLookDirection(EnemyData enemy)
        {
            if (enemy.IdleFramesLeft > 0)
            {
                // Idle: her kare 2 derece don (360/180 kare ≈ 3 sn tam tur)
                // Her dusman kendi index'ine gore farkli hizda / yonde doner
                float dir = (enemy.ZoneIndex % 2 == 0) ? 1f : -1f;
                enemy.Angle += dir * 2.2f;
                if (enemy.Angle >  180f) enemy.Angle -= 360f;
                if (enemy.Angle < -180f) enemy.Angle += 360f;
                return;
            }

            Vector2D lookAt;
            if (enemy.CurrentPath != null && enemy.CurrentPath.Count > 0)
                lookAt = enemy.CurrentPath[0];
            else
                lookAt = enemy.GetPatrolTarget();

            float dx = lookAt.X - enemy.Position.X;
            float dy = lookAt.Y - enemy.Position.Y;

            if (dx * dx + dy * dy > 1f)
                enemy.Angle = (float)(Math.Atan2(dy, dx) * (180.0 / Math.PI));
        }

        private void GameLoopTick(object sender, EventArgs e)
        {
            exitPulse += 0.08f;
            if (exitPulse > (float)(2 * Math.PI)) exitPulse -= (float)(2 * Math.PI);

            if (currentState == GameState.Playing)
            {
                UpdateGameLogic();
                CheckGameConditions();
            }
            Invalidate();
        }

        private void UpdateGameLogic()
        {
            float dx = 0f, dy = 0f;
            if (moveUp)    dy -= playerSpeed;
            if (moveDown)  dy += playerSpeed;
            if (moveLeft)  dx -= playerSpeed;
            if (moveRight) dx += playerSpeed;

            // Hareket yonu acisini guncelle (sadece hareket varsa)
            if (dx * dx + dy * dy > 0.01f)
                _playerMoveAngle = (float)(Math.Atan2(dy, dx) * (180.0 / Math.PI));

            TryMovePlayer(ref player, player.X + dx, player.Y + dy);

            foreach (EnemyData enemy in enemies)
            {
                UpdateEnemyPatrol(enemy);
                UpdateEnemyLookDirection(enemy);

                enemy.FovPolygon = Geometry.ComputeFieldOfView(
                    enemy.Position, enemy.Angle, fovRadius, fovHalfAngle, walls, bspTree, fovRayCount);
            }
        }

        private void UpdateEnemyPatrol(EnemyData enemy)
        {
            if (enemy.PatrolRoute.Count == 0) return;

            // Alert flash sayaci
            if (enemy.AlertFlashFrames > 0) enemy.AlertFlashFrames--;
            else enemy.AlertFlash = false;

            // Idle bekleme: hedefe varinca durup bekle
            if (enemy.IdleFramesLeft > 0)
            {
                enemy.IdleFramesLeft--;
                return;
            }

            Vector2D patrolTarget = enemy.GetPatrolTarget();

            if (Vector2D.Distance(enemy.Position, patrolTarget) < PatrolArrivalDist)
            {
                enemy.PatrolIndex = (enemy.PatrolIndex + 1) % enemy.PatrolRoute.Count;
                patrolTarget = enemy.GetPatrolTarget();
                enemy.CurrentPath.Clear();
                enemy.StuckFrames = 0;
                enemy.IdleFramesLeft = IdleFrames;  // bir sonraki noktaya gecmeden bekle
                return;
            }

            float moved = (float)Math.Sqrt(
                (enemy.Position.X - enemy.LastX) * (enemy.Position.X - enemy.LastX) +
                (enemy.Position.Y - enemy.LastY) * (enemy.Position.Y - enemy.LastY));
            if (moved < 0.4f) enemy.StuckFrames++;
            else               enemy.StuckFrames = 0;

            enemy.LastX = enemy.Position.X;
            enemy.LastY = enemy.Position.Y;

            // --- Takılma kurtarma ---
            bool forceReplan = false;
            if (enemy.StuckFrames >= StuckSkipLimit)
            {
                // 2 sn boyunca hiç ilerleyemediyse: mevcut patrol noktasını atla
                enemy.PatrolIndex = (enemy.PatrolIndex + 1) % enemy.PatrolRoute.Count;
                patrolTarget = enemy.GetPatrolTarget();
                enemy.CurrentPath.Clear();
                enemy.StuckFrames = 0;   // sıfırla — yeni hedefe taze başla
                forceReplan = true;
            }
            else if (enemy.StuckFrames == StuckFrameLimit)
            {
                // TAM 60. kare: yolu bir kez temizle; sayaç artmaya devam etsin
                // (StuckFrameLimit'e sabitlemek her kare replan'a yol açıyordu)
                enemy.CurrentPath.Clear();
                forceReplan = true;
                // enemy.StuckFrames değiştirilmiyor → 61, 62 ... 120 sayılacak
            }

            enemy.PathUpdateCounter++;
            if (forceReplan || enemy.PathUpdateCounter >= PathUpdateInterval ||
                enemy.CurrentPath.Count == 0)
            {
                enemy.CurrentPath = pathfinder.FindPathToPosition(
                    navGraph, enemy.Position, patrolTarget, walls, bspTree);
                enemy.PathUpdateCounter = 0;
            }

            if (enemy.CurrentPath.Count > 0)
                FollowPathWaypoint(enemy);
            else
                MoveEnemyToward(ref enemy.Position, patrolTarget);
        }

        private void FollowPathWaypoint(EnemyData enemy)
        {
            Vector2D targetNode = enemy.CurrentPath[0];
            float dist = Vector2D.Distance(enemy.Position, targetNode);

            if (dist <= PatrolArrivalDist)
            {
                enemy.CurrentPath.RemoveAt(0);
                return;
            }

            // PatrolSystem.MoveTowardTarget: 8-yon sliding, kose suruntusu yok
            PatrolSystem.MoveTowardTarget(ref enemy.Position, targetNode, enemySpeed, entityRadius, walls);

            if (Vector2D.Distance(enemy.Position, targetNode) < PatrolArrivalDist)
                enemy.CurrentPath.RemoveAt(0);
        }

        private void MoveEnemyToward(ref Vector2D pos, Vector2D target)
        {
            PatrolSystem.MoveTowardTarget(ref pos, target, enemySpeed, entityRadius, walls);
        }

        private void TryMovePlayer(ref Vector2D pos, float nextX, float nextY)
        {
            Vector2D next = new Vector2D(nextX, nextY);
            if (!Geometry.CircleHitsWalls(pos, next, entityRadius, walls))
            {
                pos.X = nextX;
                pos.Y = nextY;
                return;
            }

            // Eksen bazli sliding
            bool movedX = false, movedY = false;
            if (!Geometry.CircleHitsWalls(pos, new Vector2D(nextX, pos.Y), entityRadius, walls))
            { pos.X = nextX; movedX = true; }
            if (!Geometry.CircleHitsWalls(pos, new Vector2D(pos.X, nextY), entityRadius, walls))
            { pos.Y = nextY; movedY = true; }

            // Kose duzeltme: sadece bir eksen bloke olduysa
            // hareket vektorunu duvara paralel bilesene yansit
            if (movedX == movedY) return; // ikisi de gecti veya ikisi de bloklandi
            float mx = nextX - (pos.X - (movedX ? 0 : nextX - pos.X));
            float my = nextY - (pos.Y - (movedY ? 0 : nextY - pos.Y));
            float len = (float)Math.Sqrt(mx * mx + my * my);
            if (len < 0.01f) return;
            // Kalan hareket yonunde kucuk bir ek deneme (kose surtustu azaltir)
            float scale = (playerSpeed * 0.3f) / len;
            float ex = pos.X + mx * scale;
            float ey = pos.Y + my * scale;
            if (!Geometry.CircleHitsWalls(pos, new Vector2D(ex, ey), entityRadius, walls))
            { pos.X = ex; pos.Y = ey; }
        }

        private void CheckGameConditions()
        {
            if (Vector2D.Distance(player, targetExit) < entityRadius * 2.5f)
            {
                if (currentLevelIndex >= LevelManager.LevelCount - 1)
                    currentState = GameState.Won;
                else
                    currentState = GameState.LevelComplete;
                return;
            }

            foreach (EnemyData enemy in enemies)
            {
                if (Vector2D.Distance(player, enemy.Position) > fovRadius + entityRadius)
                    continue;

                // FOV poligonu zaten raycasting ile duvar-kisitli hesaplaniyor;
                // icindeyse LOS zaten var — ek HasLineOfSight gereksiz BSP traversal
                if (Geometry.IsPointInFovPolygon(player, enemy.FovPolygon))
                {
                    enemy.AlertFlash = true;
                    enemy.AlertFlashFrames = AlertFlashDuration;
                    currentState = GameState.Lost;
                    return;
                }
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            DrawHudBar(g);

            g.TranslateTransform(0, HudBarHeight);

            int playW = ClientSize.Width;
            int playH = ClientSize.Height - HudBarHeight;
            using (Brush floorBrush = new SolidBrush(Color.FromArgb(35, 38, 48)))
                g.FillRectangle(floorBrush, 0, 0, playW, playH);

            ApplyMapViewportTransform(g, playW, playH);

            float wallPenW = Math.Max(1.5f, 3f / mapRenderScale);
            using (Pen wallPen = new Pen(Color.FromArgb(220, 220, 230), wallPenW))
            {
                wallPen.StartCap = LineCap.Round;
                wallPen.EndCap   = LineCap.Round;
                for (int i = 0; i < walls.Count; i++)
                    g.DrawLine(wallPen, walls[i].Start.ToPointF(), walls[i].End.ToPointF());
            }

            // Oyuncuya görülen alan (herhangi bir FOV'da ise renk halkası)
            bool playerInAnyFov = false;
            foreach (EnemyData enemy in enemies)
            {
                if (Geometry.IsPointInFovPolygon(player, enemy.FovPolygon)) { playerInAnyFov = true; break; }
            }
            if (playerInAnyFov)
            {
                using (Pen warningPen = new Pen(Color.FromArgb(180, Color.OrangeRed), 4f / mapRenderScale))
                    g.DrawEllipse(warningPen,
                        player.X - entityRadius - 6, player.Y - entityRadius - 6,
                        entityRadius * 2 + 12, entityRadius * 2 + 12);
            }

            foreach (EnemyData enemy in enemies)
            {
                DrawFovPolygon(g, enemy);
                DrawEnemyPath(g, enemy);

                using (Brush enemyBrush = new SolidBrush(enemy.ThemeColor))
                {
                    g.FillEllipse(enemyBrush,
                        enemy.Position.X - entityRadius, enemy.Position.Y - entityRadius,
                        entityRadius * 2, entityRadius * 2);
                }

                DrawFacingIndicator(g, enemy);
                DrawEnemyAlert(g, enemy);
            }

            // Çıkış noktası — titreşen halka + EXIT etiketi
            float pulse = (float)(0.5 + 0.5 * Math.Sin(exitPulse));
            float outerR = 20f + 8f * pulse;
            using (Brush exitBrush = new SolidBrush(Color.MediumPurple))
                g.FillEllipse(exitBrush, targetExit.X - 14, targetExit.Y - 14, 28, 28);
            using (Pen exitPen = new Pen(Color.FromArgb((int)(80 + 160 * pulse), Color.Plum), 2.5f))
                g.DrawEllipse(exitPen,
                    targetExit.X - outerR, targetExit.Y - outerR, outerR * 2, outerR * 2);
            {
                // "EXIT" etiketi — ölçekle küçük olsun
                float fontSize = Math.Max(6f, 11f / mapRenderScale);
                using (Font exitFont = new Font("Segoe UI", fontSize, FontStyle.Bold))
                {
                    string label = "EXIT";
                    SizeF sz = g.MeasureString(label, exitFont);
                    g.DrawString(label, exitFont, Brushes.Plum,
                        targetExit.X - sz.Width * 0.5f, targetExit.Y + 16);
                }
            }

            // Oyuncu — daire + hareket yönü oku
            using (Brush playerBrush = new SolidBrush(Color.LimeGreen))
                g.FillEllipse(playerBrush,
                    player.X - entityRadius, player.Y - entityRadius,
                    entityRadius * 2, entityRadius * 2);
            using (Pen playerBorder = new Pen(Color.FromArgb(180, Color.White), 1.5f))
                g.DrawEllipse(playerBorder,
                    player.X - entityRadius, player.Y - entityRadius,
                    entityRadius * 2, entityRadius * 2);
            // Hareket yonu oku: sonraki hareket yönüne göre
            {
                float moveAngle = _playerMoveAngle;
                float pr = moveAngle * (float)(Math.PI / 180.0);
                float ex = player.X + (float)Math.Cos(pr) * (entityRadius + 7);
                float ey = player.Y + (float)Math.Sin(pr) * (entityRadius + 7);
                using (Pen arrowPen = new Pen(Color.FromArgb(220, Color.LimeGreen), 2f))
                {
                    arrowPen.EndCap = LineCap.ArrowAnchor;
                    g.DrawLine(arrowPen, player.X, player.Y, ex, ey);
                }
            }

            g.ResetTransform();
            g.TranslateTransform(0, HudBarHeight);
            DrawGameOverlay(g, playH);
            g.ResetTransform();
        }

        // Haritayi oyun alanina sigdir (tamamini goster)
        private void ApplyMapViewportTransform(Graphics g, float viewW, float viewH)
        {
            float mapW = LevelManager.MapRight - LevelManager.MapLeft;
            float mapH = LevelManager.MapBottom - LevelManager.MapTop;
            const float pad = 10f;

            float sx = (viewW - pad * 2f) / mapW;
            float sy = (viewH - pad * 2f) / mapH;
            mapRenderScale = Math.Min(sx, sy);

            float drawnW = mapW * mapRenderScale;
            float drawnH = mapH * mapRenderScale;
            float offsetX = (viewW - drawnW) * 0.5f - LevelManager.MapLeft * mapRenderScale;
            float offsetY = (viewH - drawnH) * 0.5f - LevelManager.MapTop * mapRenderScale;

            g.TranslateTransform(offsetX, offsetY);
            g.ScaleTransform(mapRenderScale, mapRenderScale);
        }

        private void DrawFovPolygon(Graphics g, EnemyData enemy)
        {
            if (enemy.FovPolygon == null || enemy.FovPolygon.Count < 3) return;

            PointF[] pts = new PointF[enemy.FovPolygon.Count];
            for (int i = 0; i < enemy.FovPolygon.Count; i++)
                pts[i] = enemy.FovPolygon[i].ToPointF();

            using (Brush fovBrush = new SolidBrush(Color.FromArgb(55, enemy.ThemeColor)))
                g.FillPolygon(fovBrush, pts);

            using (Pen fovEdgePen = new Pen(Color.FromArgb(120, enemy.ThemeColor), 1))
            {
                for (int i = 1; i < pts.Length; i++)
                    g.DrawLine(fovEdgePen, pts[0], pts[i]);
                g.DrawLine(fovEdgePen, pts[0], pts[pts.Length - 1]);
            }
        }

        // Debug: F1 ile acilip kapanir
        private bool _showDebugPaths = false;
        private void DrawEnemyPath(Graphics g, EnemyData enemy)
        {
            if (!_showDebugPaths) return;
            if (enemy.CurrentPath == null || enemy.CurrentPath.Count == 0) return;

            using (Pen pathPen = new Pen(Color.FromArgb(80, enemy.ThemeColor), 1.5f))
            {
                Vector2D prev = enemy.Position;
                foreach (Vector2D pt in enemy.CurrentPath)
                {
                    g.DrawLine(pathPen, prev.ToPointF(), pt.ToPointF());
                    prev = pt;
                }
            }
        }

        private void DrawEnemyAlert(Graphics g, EnemyData enemy)
        {
            if (!enemy.AlertFlash) return;
            float ax = enemy.Position.X + 2;
            float ay = enemy.Position.Y - entityRadius - 22;
            Font f = _alertFont ?? new Font("Arial", 14f, FontStyle.Bold);
            g.DrawString("!", f, Brushes.Red, ax, ay);
        }

        private void DrawFacingIndicator(Graphics g, EnemyData enemy)
        {
            float rad = enemy.Angle * (float)(Math.PI / 180.0);
            float ex = enemy.Position.X + (float)Math.Cos(rad) * (entityRadius + 6);
            float ey = enemy.Position.Y + (float)Math.Sin(rad) * (entityRadius + 6);
            using (Pen dirPen = new Pen(Color.White, 2))
                g.DrawLine(dirPen, enemy.Position.ToPointF(), new PointF(ex, ey));
        }

        // HUD ust panelde; harita alani asagida kalir
        private void DrawHudBar(Graphics g)
        {
            using (Brush barBrush = new SolidBrush(Color.FromArgb(18, 20, 28)))
                g.FillRectangle(barBrush, 0, 0, ClientSize.Width, HudBarHeight);

            using (Pen linePen = new Pen(Color.FromArgb(60, 65, 80), 1))
                g.DrawLine(linePen, 0, HudBarHeight - 1, ClientSize.Width, HudBarHeight - 1);

            string levelInfo = string.Format("Seviye  {0} / {1}", currentLevelIndex + 1, LevelManager.LevelCount);
            int cy = HudBarHeight / 2 - 7;
            if (_titleFont != null) g.DrawString(levelInfo, _titleFont, Brushes.WhiteSmoke, 14, cy);

            if (currentState == GameState.Playing && _hudFont != null)
            {
                string controls = _showDebugPaths
                    ? "WASD: Hareket  |  R: Sifirla  |  N: Sonraki  |  F1: Yol Gizle  |  ESC: Cikis"
                    : "WASD: Hareket  |  R: Sifirla  |  N: Sonraki  |  F1: Yol Goster  |  ESC: Cikis";
                SizeF sz = g.MeasureString(controls, _hudFont);
                g.DrawString(controls, _hudFont, Brushes.Gray, ClientSize.Width - sz.Width - 10, cy + 2);
            }
        }

        // Kazanma/kaybetme mesajlari harita ortasinda (HUD disinda)
        private void DrawGameOverlay(Graphics g, int playAreaHeight)
        {
            if (currentState == GameState.Playing) return;

            float cx = ClientSize.Width * 0.5f;
            float cy = playAreaHeight * 0.5f;

            Font bigFont = _bigFont ?? new Font("Arial", 22f, FontStyle.Bold);
            Font hudFont = _hudFont ?? new Font("Segoe UI", 11f);

            // Yari seffaf karartma paneli
            using (Brush dimBrush = new SolidBrush(Color.FromArgb(165, 0, 0, 0)))
                g.FillRectangle(dimBrush, 0, 0, ClientSize.Width, playAreaHeight);

            // Mesaj cercevesi
            string mainMsg = "";
            string subMsg  = "";
            Brush mainColor = Brushes.White;

            if (currentState == GameState.LevelComplete)
            {
                mainMsg  = "SEVIYE TAMAMLANDI!";
                subMsg   = "N: Sonraki Seviye   |   R: Tekrar";
                mainColor = Brushes.Gold;
            }
            else if (currentState == GameState.Won)
            {
                mainMsg  = "TUM SEVIYELERI TAMAMLADIN!";
                subMsg   = "R: Bastan Basla";
                mainColor = Brushes.LimeGreen;
            }
            else if (currentState == GameState.Lost)
            {
                mainMsg  = "YAKALANDIN!";
                subMsg   = "R: Tekrar Dene";
                mainColor = Brushes.OrangeRed;
            }

            // Panel kutusu
            SizeF mainSz = g.MeasureString(mainMsg, bigFont);
            SizeF subSz  = g.MeasureString(subMsg,  hudFont);
            float boxW = Math.Max(mainSz.Width, subSz.Width) + 60;
            float boxH = mainSz.Height + subSz.Height + 40;
            float bx = cx - boxW * 0.5f;
            float by = cy - boxH * 0.5f;

            using (Brush boxBrush = new SolidBrush(Color.FromArgb(200, 18, 20, 30)))
                g.FillRectangle(boxBrush, bx, by, boxW, boxH);
            using (Pen boxPen = new Pen(Color.FromArgb(180, Color.DimGray), 1.5f))
                g.DrawRectangle(boxPen, bx, by, boxW, boxH);

            DrawCenteredString(g, mainMsg, bigFont, mainColor, cx, by + 18);
            DrawCenteredString(g, subMsg,  hudFont, Brushes.Silver, cx, by + boxH - subSz.Height - 14);
        }

        private static void DrawCenteredString(Graphics g, string text, Font font, Brush brush, float cx, float cy)
        {
            SizeF size = g.MeasureString(text, font);
            g.DrawString(text, font, brush, cx - size.Width * 0.5f, cy - size.Height * 0.5f);
        }

        private void GameForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                Close();
                return;
            }

            if (e.KeyCode == Keys.W) moveUp = true;
            if (e.KeyCode == Keys.S) moveDown = true;
            if (e.KeyCode == Keys.A) moveLeft = true;
            if (e.KeyCode == Keys.D) moveRight = true;

            if (e.KeyCode == Keys.R)
            {
                LoadLevel(currentLevelIndex);
                return;
            }

            if (e.KeyCode == Keys.N && currentState == GameState.LevelComplete)
                LoadLevel(currentLevelIndex + 1);

            if (e.KeyCode == Keys.F1)
                _showDebugPaths = !_showDebugPaths;
        }

        private void GameForm_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.W) moveUp = false;
            if (e.KeyCode == Keys.S) moveDown = false;
            if (e.KeyCode == Keys.A) moveLeft = false;
            if (e.KeyCode == Keys.D) moveRight = false;
        }
    }
}
