using System;
using System.Drawing;
using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace StealthVisionSystem;

public sealed class GameForm : Form
{
    private const float WorldScale = 60f;
    private const float MapWidth = 16f * WorldScale;
    private const float MapHeight = 10f * WorldScale;
    private const float SidebarGap = 20f;

    private readonly DynamicArray<WallSegment> _walls;
    private readonly BspTree _bsp;
    private readonly WaypointGraph _graph;
    private readonly System.Windows.Forms.Timer _timer;
    private readonly int[][] _patrolRoutes =
    [
        [0, 1, 2, 5, 4, 3],
        [3, 4, 5, 2, 1, 0],
        [0, 3, 4, 5, 2, 1]
    ];

    private Vector2 _player = new(14, 2);
    private readonly Vector2 _goal = new(2.2, 8.4);
    private Enemy _enemy = new(new Vector2(2, 2), 0.0, Math.PI / 2, 12.0);
    private DynamicArray<int> _path = new();
    private DynamicArray<RayHit> _fov = new();
    private bool _canSeePlayer;
    private int _patrolTargetIndex = 1;
    private int _frameMs;
    private int _pathMs;
    private GameState _state = GameState.Playing;
    private int _stuckFrames;
    private EnemyMode _enemyMode = EnemyMode.Patrol;
    private int _lostSightFrames;
    private int _currentPatrolRoute;

    public GameForm()
    {
        Text = "BSP + FOV + A* Gorsel Simulasyon";
        Width = 1360;
        Height = 840;
        DoubleBuffered = true;
        KeyPreview = true;
        StartPosition = FormStartPosition.CenterScreen;

        _walls = Program.BuildWalls();
        _bsp = new BspTree(_walls);
        _graph = Program.BuildWaypointGraph(_walls);

        _timer = new System.Windows.Forms.Timer { Interval = 45 };
        _timer.Tick += (_, _) =>
        {
            UpdateEnemy();
            Recalculate();
            Invalidate();
        };
        _timer.Start();

        Recalculate();
        KeyDown += OnKeyDownMovePlayer;
    }

    private void OnKeyDownMovePlayer(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.R)
        {
            ResetGame();
            return;
        }

        if (_state != GameState.Playing) return;

        const double step = 0.25;
        var next = _player;

        if (e.KeyCode == Keys.W) next = new Vector2(_player.X, _player.Y - step);
        if (e.KeyCode == Keys.S) next = new Vector2(_player.X, _player.Y + step);
        if (e.KeyCode == Keys.A) next = new Vector2(_player.X - step, _player.Y);
        if (e.KeyCode == Keys.D) next = new Vector2(_player.X + step, _player.Y);

        if (!Collision.CollidesWithWalls(_player, next, _bsp))
        {
            _player = next;
            Recalculate();
            Invalidate();
        }
    }

    private void Recalculate()
    {
        var frameStart = Stopwatch.GetTimestamp();
        _canSeePlayer = IsPlayerInFovAndVisible();
        _fov = Visibility.CastFovRays(_enemy, _bsp, 31);

        int startNode = _graph.FindClosestNode(_enemy.Position);
        int goalNode = _graph.FindClosestNode(_player);

        var pathStart = Stopwatch.GetTimestamp();
        _path = AStarPathfinder.FindPath(_graph, startNode, goalNode);
        _pathMs = ElapsedMs(pathStart, Stopwatch.GetTimestamp());

        if (_canSeePlayer) _state = GameState.Caught;
        else if ((_player - _goal).Length() < 0.45) _state = GameState.Won;

        _frameMs = ElapsedMs(frameStart, Stopwatch.GetTimestamp());
    }

    private bool IsPlayerInFovAndVisible()
    {
        var toPlayer = _player - _enemy.Position;
        double distance = toPlayer.Length();
        if (distance > _enemy.ViewDistance || distance < 1e-9) return false;

        var forward = new Vector2(Math.Cos(_enemy.DirectionRadians), Math.Sin(_enemy.DirectionRadians));
        var dir = toPlayer.Normalize();
        double cos = Vector2.Dot(forward, dir);
        double halfFovCos = Math.Cos(_enemy.FovRadians / 2.0);

        // Oyuncu gorus konisi icinde degilse LOS bakmaya gerek yok.
        if (cos < halfFovCos) return false;

        return Visibility.HasLineOfSight(_enemy.Position, _player, _bsp);
    }

    private static int ElapsedMs(long start, long end)
        => (int)((end - start) * 1000.0 / Stopwatch.Frequency);

    private void ResetGame()
    {
        _player = new Vector2(14, 2);
        _enemy = new Enemy(new Vector2(2, 2), 0.0, Math.PI / 2, 12.0);
        _patrolTargetIndex = 1;
        _state = GameState.Playing;
        _enemyMode = EnemyMode.Patrol;
        _lostSightFrames = 0;
        _currentPatrolRoute = (_currentPatrolRoute + 1) % _patrolRoutes.Length;
        Recalculate();
        Invalidate();
    }

    private void FollowPatrol()
    {
        var route = _patrolRoutes[_currentPatrolRoute];
        int enemyNode = _graph.FindClosestNode(_enemy.Position);
        int targetNode = route[_patrolTargetIndex];
        var targetPos = _graph.GetPosition(targetNode);

        // Closest-node jitter'ini azaltmak icin konumsal toleransla hedefe varis kontrolu.
        if ((_enemy.Position - targetPos).Length() < 0.35 || enemyNode == targetNode)
        {
            _patrolTargetIndex = (_patrolTargetIndex + 1) % route.Length;
            if (_patrolTargetIndex == 0)
            {
                // Her tur tamamlandiginda farkli devriye rotasina gec.
                _currentPatrolRoute = (_currentPatrolRoute + 1) % _patrolRoutes.Length;
                route = _patrolRoutes[_currentPatrolRoute];
            }
            targetNode = route[_patrolTargetIndex];
            targetPos = _graph.GetPosition(targetNode);
        }

        var patrolPath = AStarPathfinder.FindPath(_graph, enemyNode, targetNode);
        if (patrolPath.Count <= 1) return;

        var current = _enemy.Position;
        var next = _graph.GetPosition(patrolPath[1]);
        var dir = (next - current).Normalize();
        var trial = current + dir * 0.12;

        if (!Collision.CollidesWithWalls(current, trial, _bsp))
        {
            double angle = Math.Atan2(dir.Y, dir.X);
            _enemy = new Enemy(trial, angle, _enemy.FovRadians, _enemy.ViewDistance);
            _stuckFrames = 0;
        }
        else
        {
            _stuckFrames++;
            if (_stuckFrames > 20)
            {
                // Uzun sure ayni noktada kalirsa sonraki devriye hedefine gecerek kilitlenmeyi kir.
                _patrolTargetIndex = (_patrolTargetIndex + 1) % route.Length;
                _stuckFrames = 0;
            }
        }
    }

    private void ChasePlayer()
    {
        int startNode = _graph.FindClosestNode(_enemy.Position);
        int goalNode = _graph.FindClosestNode(_player);
        _path = AStarPathfinder.FindPath(_graph, startNode, goalNode);
        if (_path.Count <= 1) return;

        var current = _enemy.Position;
        var next = _graph.GetPosition(_path[1]);
        var dir = (next - current).Normalize();
        var trial = current + dir * 0.14;

        if (!Collision.CollidesWithWalls(current, trial, _bsp))
        {
            double angle = Math.Atan2(dir.Y, dir.X);
            _enemy = new Enemy(trial, angle, _enemy.FovRadians, _enemy.ViewDistance);
            _stuckFrames = 0;
        }
        else
        {
            _stuckFrames++;
            if (_stuckFrames > 20)
            {
                // Takipte de kilitlenirse mevcut node'a hizalayip tekrar planla.
                int node = _graph.FindClosestNode(_enemy.Position);
                var pos = _graph.GetPosition(node);
                _enemy = new Enemy(pos, _enemy.DirectionRadians, _enemy.FovRadians, _enemy.ViewDistance);
                _stuckFrames = 0;
            }
        }
    }

    private void UpdateEnemy()
    {
        if (_state != GameState.Playing) return;

        if (_canSeePlayer)
        {
            _enemyMode = EnemyMode.Chase;
            _lostSightFrames = 0;
        }
        else if (_enemyMode == EnemyMode.Chase)
        {
            _lostSightFrames++;
            // Titremeyi onlemek icin birkac frame daha takipte kal.
            if (_lostSightFrames > 35)
            {
                _enemyMode = EnemyMode.Patrol;
                _lostSightFrames = 0;
            }
        }

        if (_enemyMode == EnemyMode.Chase) ChasePlayer();
        else FollowPatrol();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.FromArgb(20, 22, 27));

        DrawGrid(g);
        DrawWalls(g);
        DrawGraph(g);
        DrawFov(g);
        DrawPath(g);
        DrawActors(g);
        DrawGoal(g);
        DrawSidebar(g);
        DrawStatusBanner(g);
    }

    private static void DrawGrid(Graphics g)
    {
        using var mapBg = new SolidBrush(Color.FromArgb(28, 33, 42));
        g.FillRectangle(mapBg, 0, 0, MapWidth, MapHeight);

        using var pen = new Pen(Color.FromArgb(52, 58, 70), 1);
        for (int x = 0; x <= 16; x++) g.DrawLine(pen, x * WorldScale, 0, x * WorldScale, MapHeight);
        for (int y = 0; y <= 10; y++) g.DrawLine(pen, 0, y * WorldScale, MapWidth, y * WorldScale);

        using var border = new Pen(Color.FromArgb(130, 150, 180), 2);
        g.DrawRectangle(border, 0, 0, MapWidth, MapHeight);
    }

    private void DrawWalls(Graphics g)
    {
        using var penShadow = new Pen(Color.FromArgb(80, 0, 0, 0), 7);
        using var pen = new Pen(Color.FromArgb(228, 235, 247), 4);
        for (int i = 0; i < _walls.Count; i++)
        {
            var w = _walls[i];
            g.DrawLine(penShadow, ToPoint(w.A), ToPoint(w.B));
            g.DrawLine(pen, ToPoint(w.A), ToPoint(w.B));
        }
    }

    private void DrawGraph(Graphics g)
    {
        using var edgePen = new Pen(Color.FromArgb(65, 110, 185), 2);
        using var nodeBrush = new SolidBrush(Color.FromArgb(145, 190, 255));

        for (int i = 0; i < _graph.NodeCount; i++)
        {
            var edges = _graph.GetEdges(i);
            var from = _graph.GetPosition(i);
            for (int j = 0; j < edges.Count; j++)
            {
                var to = _graph.GetPosition(edges[j].To);
                g.DrawLine(edgePen, ToPoint(from), ToPoint(to));
            }
        }

        for (int i = 0; i < _graph.NodeCount; i++)
        {
            var p = ToPoint(_graph.GetPosition(i));
            g.FillEllipse(nodeBrush, p.X - 5, p.Y - 5, 10, 10);
        }
    }

    private void DrawFov(Graphics g)
    {
        var origin = ToPoint(_enemy.Position);

        if (_fov.Count >= 2)
        {
            var points = new PointF[_fov.Count + 1];
            points[0] = origin;
            for (int i = 0; i < _fov.Count; i++)
            {
                points[i + 1] = ToPoint(_fov[i].Point);
            }

            using var coneBrush = new SolidBrush(Color.FromArgb(_canSeePlayer ? 90 : 55, 255, 180, 50));
            g.FillPolygon(coneBrush, points);
        }

        using var rayPen = new Pen(Color.FromArgb(210, 255, 196, 90), 1.5f);
        for (int i = 0; i < _fov.Count; i++)
        {
            g.DrawLine(rayPen, origin, ToPoint(_fov[i].Point));
        }
    }

    private void DrawPath(Graphics g)
    {
        if (_path.Count < 2) return;
        using var shadow = new Pen(Color.FromArgb(90, 0, 0, 0), 7);
        using var pen = new Pen(Color.FromArgb(255, 80, 220, 255), 4);
        for (int i = 0; i < _path.Count - 1; i++)
        {
            var a = ToPoint(_graph.GetPosition(_path[i]));
            var b = ToPoint(_graph.GetPosition(_path[i + 1]));
            g.DrawLine(shadow, a, b);
            g.DrawLine(pen, a, b);
        }
    }

    private void DrawActors(Graphics g)
    {
        var player = ToPoint(_player);
        var enemy = ToPoint(_enemy.Position);

        using var playerBrush = new SolidBrush(_canSeePlayer ? Color.OrangeRed : Color.FromArgb(60, 225, 120));
        using var enemyBrush = new SolidBrush(Color.Gold);
        using var ringPen = new Pen(Color.White, 2);
        using var lookPen = new Pen(Color.FromArgb(255, 240, 160), 2);

        g.FillEllipse(playerBrush, player.X - 10, player.Y - 10, 20, 20);
        g.DrawEllipse(ringPen, player.X - 12, player.Y - 12, 24, 24);

        g.FillEllipse(enemyBrush, enemy.X - 10, enemy.Y - 10, 20, 20);
        g.DrawEllipse(ringPen, enemy.X - 12, enemy.Y - 12, 24, 24);

        var look = new PointF(
            enemy.X + (float)(Math.Cos(_enemy.DirectionRadians) * 22),
            enemy.Y + (float)(Math.Sin(_enemy.DirectionRadians) * 22));
        g.DrawLine(lookPen, enemy, look);
    }

    private void DrawGoal(Graphics g)
    {
        var p = ToPoint(_goal);
        using var goalPen = new Pen(Color.Violet, 4);
        using var goalFill = new SolidBrush(Color.FromArgb(90, 198, 80, 230));
        g.FillEllipse(goalFill, p.X - 11, p.Y - 11, 22, 22);
        g.DrawEllipse(goalPen, p.X - 13, p.Y - 13, 26, 26);
    }

    private void DrawSidebar(Graphics g)
    {
        float panelX = MapWidth + SidebarGap;
        float panelW = ClientSize.Width - panelX - 20;
        float panelH = MapHeight;

        using var bg = new SolidBrush(Color.FromArgb(35, 40, 52));
        using var border = new Pen(Color.FromArgb(95, 110, 138), 2);
        g.FillRectangle(bg, panelX, 0, panelW, panelH);
        g.DrawRectangle(border, panelX, 0, panelW, panelH);

        using var titleFont = new Font(Font.FontFamily, 15, FontStyle.Bold);
        using var sectionFont = new Font(Font.FontFamily, 11, FontStyle.Bold);
        using var bodyFont = new Font(Font.FontFamily, 10, FontStyle.Regular);
        using var headerBrush = new SolidBrush(Color.WhiteSmoke);
        using var bodyBrush = new SolidBrush(Color.FromArgb(220, 230, 250));
        using var mono = new Font(FontFamily.GenericMonospace, 11, FontStyle.Regular);

        g.DrawString("SIMULASYON PANELI", titleFont, headerBrush, panelX + 14, 14);
        DrawPanelCard(g, panelX + 12, 56, panelW - 24, 88, "KONTROLLER",
            "- W A S D : Oyuncu hareket\n- R : Oyunu sifirla", sectionFont, bodyFont);

        DrawPanelCard(g, panelX + 12, 154, panelW - 24, 160, "GORSEL ANAHTAR",
            "- Sari      : Dusman\n- Yesil     : Oyuncu guvende\n- Turuncu   : Oyuncu goruste\n- Mor       : Hedef\n- Acik Mavi : A* yolu\n- Turuncu fan : FOV",
            sectionFont, bodyFont);

        string metrics = $"Ray Sayisi   : {_fov.Count}\nYol Dugumu   : {_path.Count}\nPath Suresi  : {_pathMs} ms\nFrame Suresi : {_frameMs} ms\nDevriye Rota : {_currentPatrolRoute + 1}/{_patrolRoutes.Length}";
        DrawPanelCard(g, panelX + 12, 324, panelW - 24, 132, "CANLI METRIKLER", metrics, sectionFont, mono);

        string aiText = $"Dusman Modu: {(_enemyMode == EnemyMode.Chase ? "Takip" : "Devriye")}";
        DrawPanelCard(g, panelX + 12, 466, panelW - 24, 62, "AI DURUMU", aiText, sectionFont, bodyFont);

        DrawPanelCard(g, panelX + 12, 538, panelW - 24, 62, "OYUN DURUMU", GetStateText(), sectionFont, bodyFont, GetStateBrush());
    }

    private void DrawStatusBanner(Graphics g)
    {
        using var font = new Font(Font.FontFamily, 16, FontStyle.Bold);
        using var fg = new SolidBrush(Color.White);
        using var bg = new SolidBrush(GetBannerColor());

        const float w = 560f;
        const float h = 44f;
        float x = (MapWidth - w) / 2f;
        const float y = 14f;

        g.FillRectangle(bg, x, y, w, h);
        g.DrawString(GetStateText(), font, fg, x + 14, y + 8);
    }

    private string GetStateText()
    {
        return _state switch
        {
            GameState.Won => "KAZANDIN - Hedefe ulastin",
            GameState.Caught => "YAKALANDIN - Dusman seni gordu",
            _ => _canSeePlayer ? "Tehlike! Dusman gorusunde" : "Oyun devam ediyor"
        };
    }

    private Brush GetStateBrush()
    {
        return _state switch
        {
            GameState.Won => Brushes.MediumSpringGreen,
            GameState.Caught => Brushes.OrangeRed,
            _ => _canSeePlayer ? Brushes.Gold : Brushes.LightGreen
        };
    }

    private Color GetBannerColor()
    {
        return _state switch
        {
            GameState.Won => Color.FromArgb(180, 28, 120, 65),
            GameState.Caught => Color.FromArgb(180, 150, 36, 28),
            _ => _canSeePlayer ? Color.FromArgb(175, 145, 90, 20) : Color.FromArgb(170, 35, 95, 60)
        };
    }

    private static void DrawPanelCard(
        Graphics g,
        float x,
        float y,
        float w,
        float h,
        string title,
        string body,
        Font titleFont,
        Font bodyFont,
        Brush? bodyBrushOverride = null)
    {
        using var cardBg = new SolidBrush(Color.FromArgb(48, 55, 69));
        using var cardBorder = new Pen(Color.FromArgb(95, 112, 140), 1);
        using var titleBrush = new SolidBrush(Color.FromArgb(236, 241, 255));
        using var bodyBrush = new SolidBrush(Color.FromArgb(214, 224, 245));

        g.FillRectangle(cardBg, x, y, w, h);
        g.DrawRectangle(cardBorder, x, y, w, h);
        g.DrawString(title, titleFont, titleBrush, x + 10, y + 8);
        g.DrawString(body, bodyFont, bodyBrushOverride ?? bodyBrush, x + 10, y + 30);
    }

    private static PointF ToPoint(Vector2 v) => new((float)(v.X * WorldScale), (float)(v.Y * WorldScale));
}

public enum GameState
{
    Playing,
    Won,
    Caught
}

public enum EnemyMode
{
    Patrol,
    Chase
}
