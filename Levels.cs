using System;
using System.Collections.Generic;
using System.Drawing;

namespace DataStructures_SVS
{
    // Bir oyun seviyesinin tum verileri: duvarlar, waypoint'ler, baslangic konumlari
    public class LevelDefinition
    {
        public string Name;
        public string Description;
        public Vector2D PlayerStart;
        public Vector2D Exit;
        public List<EnemySpawn> EnemySpawns = new List<EnemySpawn>();
        public DynamicArray<Segment> Walls = new DynamicArray<Segment>();
        public List<Vector2D> Waypoints = new List<Vector2D>();
    }

    public class EnemySpawn
    {
        public Vector2D Position;
        public Color Color;
        public float StartAngle;
        public List<Vector2D> PatrolPoints = new List<Vector2D>();

        public EnemySpawn(Vector2D pos, Color color, float angle)
        {
            Position = pos;
            Color = color;
            StartAngle = angle;
        }

        public EnemySpawn(Vector2D pos, Color color, float angle, params Vector2D[] patrol)
        {
            Position = pos;
            Color = color;
            StartAngle = angle;
            foreach (Vector2D p in patrol)
                PatrolPoints.Add(p);
        }
    }

    // Programatik level uretimi: her seviye farkli duzen ve engel yerlesimi.
    public static class LevelManager
    {
        public const int LevelCount = 3;
        public const float MapLeft = 40f;
        public const float MapTop = 40f;
        public const float MapRight = 1200f;
        public const float MapBottom = 750f;
        private const float MARGIN = MapLeft;
        private const float MAP_W = MapRight - MapLeft;
        private const float MAP_H = MapBottom - MapTop;

        public static LevelDefinition GetLevel(int index)
        {
            switch (index)
            {
                case 0: return BuildLevel1_CorridorMaze();
                case 1: return BuildLevel2_CrossRooms();
                case 2: return BuildLevel3_Fortress();
                default: return BuildLevel1_CorridorMaze();
            }
        }

        private static void AddBorder(DynamicArray<Segment> walls)
        {
            float l = MARGIN;
            float t = MARGIN;
            float r = MARGIN + MAP_W;
            float b = MARGIN + MAP_H;
            walls.Add(new Segment(l, t, r, t));
            walls.Add(new Segment(r, t, r, b));
            walls.Add(new Segment(r, b, l, b));
            walls.Add(new Segment(l, b, l, t));
        }

        // Seviye 1: Koridor labirenti - dar gecitler, merkez blok
        private static LevelDefinition BuildLevel1_CorridorMaze()
        {
            var level = new LevelDefinition
            {
                Name = "Seviye 1 - Koridor Labirenti",
                Description = "Dar koridorlardan gec, devriyelerin gorus alanindan kacin.",
                PlayerStart = new Vector2D(80, 120),
                Exit = new Vector2D(1080, 120)
            };

            var w = level.Walls;
            AddBorder(w);

            // Merkez ada
            w.Add(new Segment(420, 220, 620, 220));
            w.Add(new Segment(620, 220, 620, 480));
            w.Add(new Segment(620, 480, 420, 480));
            w.Add(new Segment(420, 480, 420, 220));

            // Ust yatay bolucu
            w.Add(new Segment(180, 320, 380, 320));
            w.Add(new Segment(660, 320, 900, 320));

            // Alt yatay bolucu
            w.Add(new Segment(180, 520, 380, 520));
            w.Add(new Segment(660, 520, 900, 520));

            // Dikey siperler
            w.Add(new Segment(280, 120, 280, 280));
            w.Add(new Segment(820, 400, 820, 600));

            // Hedef koruyucu
            w.Add(new Segment(980, 40, 980, 220));

            level.Waypoints = new List<Vector2D>
            {
                // Ust koridor
                new Vector2D(80, 100),  new Vector2D(320, 100), new Vector2D(520, 100),
                new Vector2D(760, 100), new Vector2D(950, 100), new Vector2D(1100, 100),
                // Orta ust — (360,200) SILINDI: siper (x=280) ile ada (x=420) arasındaki
                // dar "cepe" düşüyordu; (160,200) ile siperin soluna taşındı
                new Vector2D(80, 220),  new Vector2D(160, 200), new Vector2D(700, 200), new Vector2D(950, 220),
                // Orta bant — (520,380) SILINDI: kapalı merkez adanın içinde kalıyor,
                // nav grafiğinde sıfır kenarlı izole düğüm oluşturuyordu
                new Vector2D(80, 380),  new Vector2D(320, 380),
                new Vector2D(760, 380), new Vector2D(1000, 380),
                // Orta alt
                new Vector2D(80, 560),  new Vector2D(360, 560), new Vector2D(700, 560), new Vector2D(950, 560),
                // Alt koridor
                new Vector2D(80, 680),  new Vector2D(320, 680), new Vector2D(520, 680),
                new Vector2D(760, 680), new Vector2D(1000, 680)
            };

            // Gold — sağ yarı: kolon x=980 engel değil y>220'de, alt/üst koridorlar açık
            level.EnemySpawns.Add(new EnemySpawn(new Vector2D(1000, 680), Color.Gold, 225f,
                new Vector2D(1000, 380),
                new Vector2D(1100, 100),
                new Vector2D(950,  560),
                new Vector2D(1000, 680)));
            // OrangeRed — sol/orta yarı: siper (x=280) solundan geçerek üst koridorda
            level.EnemySpawns.Add(new EnemySpawn(new Vector2D(520, 680), Color.OrangeRed, 270f,
                new Vector2D(80,  680),
                new Vector2D(80,  100),
                new Vector2D(520, 100),
                new Vector2D(80,  560),
                new Vector2D(320, 680)));

            return level;
        }

        // Seviye 2: Capraz odalar - L seklinde engeller, acik merkez
        private static LevelDefinition BuildLevel2_CrossRooms()
        {
            var level = new LevelDefinition
            {
                Name = "Seviye 2 - Capraz Odalar",
                Description = "L seklindeki siperlerin arasindan surun, devriye rotalarina dikkat et.",
                PlayerStart = new Vector2D(80, 620),
                Exit = new Vector2D(660, 80)
            };

            var w = level.Walls;
            AddBorder(w);

            // Sol ust niş (U-sekli, altta acik – giris/cikis var)
            w.Add(new Segment(120, 120, 320, 120));  // ust kenar
            w.Add(new Segment(120, 120, 120, 280));  // sol kenar
            w.Add(new Segment(320, 120, 320, 280));  // sag kenar
            // alt kenar KALDIRILDI → nis alttan giris verir

            // Sag alt nis (ters-U, ustte acik)
            w.Add(new Segment(880, 640, 1080, 640)); // alt kenar
            w.Add(new Segment(880, 480, 880, 640));  // sol kenar
            w.Add(new Segment(1080, 480, 1080, 640));// sag kenar
            // ust kenar KALDIRILDI → nis usttten giris verir

            // Merkez dikey kolon
            w.Add(new Segment(580, 180, 580, 580));

            // L siper sol alt
            w.Add(new Segment(200, 480, 420, 480));
            w.Add(new Segment(420, 480, 420, 580));

            // L siper sag ust
            w.Add(new Segment(780, 180, 980, 180));
            w.Add(new Segment(980, 180, 980, 320));

            // Orta yatay bariyer (daha kisa, bogaz yaratmaz)
            w.Add(new Segment(420, 380, 740, 380));

            level.Waypoints = new List<Vector2D>
            {
                // Ust koridor – kolonu hem soldan hem sagdan asmak icin
                new Vector2D(80,  80),  new Vector2D(240, 80),  new Vector2D(440, 80),
                new Vector2D(660, 80),  new Vector2D(840, 80),  new Vector2D(1060, 80),
                // Orta-ust
                new Vector2D(80,  220), new Vector2D(240, 220), new Vector2D(440, 220),
                new Vector2D(700, 220), new Vector2D(840, 220), new Vector2D(1060, 220),
                // Merkez bant – kolonu cevrelemek icin her iki taraf
                new Vector2D(80,  380), new Vector2D(280, 380),
                new Vector2D(680, 380), new Vector2D(840, 380), new Vector2D(1060, 380),
                // Orta-alt – sol L-siperin altinda, sag nise giris noktasi
                new Vector2D(80,  530), new Vector2D(280, 530),
                new Vector2D(680, 530), new Vector2D(840, 530), new Vector2D(1060, 530),
                // Alt koridor
                new Vector2D(80,  680), new Vector2D(280, 680),
                new Vector2D(680, 680), new Vector2D(840, 680), new Vector2D(1060, 680)
            };

            // DeepSkyBlue — sol taraf: kolon (x=580) solu, basit dikdörtgen
            level.EnemySpawns.Add(new EnemySpawn(new Vector2D(440, 220), Color.DeepSkyBlue, 90f,
                new Vector2D(80,  80),
                new Vector2D(440, 80),
                new Vector2D(440, 680),
                new Vector2D(80,  680)));
            // MediumOrchid — sağ taraf: L-siper sağ (x>780) ve orta-bariyer sağı (x>740)
            // x=760 hem L siper sağ kolunun (x=780-980) solunda hem de bariyer (x=420-740) sağında
            level.EnemySpawns.Add(new EnemySpawn(new Vector2D(840, 530), Color.MediumOrchid, 180f,
                new Vector2D(760,  80),
                new Vector2D(1060, 80),
                new Vector2D(1060, 380),
                new Vector2D(840,  380),
                new Vector2D(840,  680),
                new Vector2D(760,  680)));
            // Coral — sol/merkez: orta bariyer solundan (x=440 > 420 siper köşesi, engelsiz)
            level.EnemySpawns.Add(new EnemySpawn(new Vector2D(440, 680), Color.Coral, 270f,
                new Vector2D(80,  680),
                new Vector2D(80,   80),
                new Vector2D(440,  80),
                new Vector2D(440, 380),
                new Vector2D(80,  380)));

            return level;
        }

        // Seviye 3: Kale duzeni - coklu adalar, zorlu gecis
        private static LevelDefinition BuildLevel3_Fortress()
        {
            var level = new LevelDefinition
            {
                Name = "Seviye 3 - Kale",
                Description = "Uc adali kaleyi as, dort devriye gecidini gozetleyerek ilerle.",
                // (80,380) TAŞINDI: Gold (80,660)→(80,100) rotası x=80 üzerinden tam kuzey geçiyor,
                // oyuncu (80,380)'de 1. karede FOV'a giriyordu. (220,500) sol ada güneydoğusu, açık.
                PlayerStart = new Vector2D(220, 500),
                Exit = new Vector2D(1060, 380)
            };

            var w = level.Walls;
            AddBorder(w);

            // Sol ada (kare pillar)
            w.Add(new Segment(160, 220, 280, 220));
            w.Add(new Segment(280, 220, 280, 380));
            w.Add(new Segment(280, 380, 160, 380));
            w.Add(new Segment(160, 380, 160, 220));

            // Orta ada (daha dar → gecitler daha genis)
            w.Add(new Segment(460, 180, 620, 180));
            w.Add(new Segment(620, 180, 620, 420));
            w.Add(new Segment(620, 420, 460, 420));
            w.Add(new Segment(460, 420, 460, 180));

            // Sag ada
            w.Add(new Segment(800, 260, 960, 260));
            w.Add(new Segment(960, 260, 960, 500));
            w.Add(new Segment(960, 500, 800, 500));
            w.Add(new Segment(800, 500, 800, 260));

            // Gecit duvarlari: sadece ust kanalda, navgraph'i bozmayacak yerde
            w.Add(new Segment(320, 300, 420, 300));
            w.Add(new Segment(660, 300, 760, 300));

            // Ust-alt siperler: y=80/640 YERINE y=160/600 – entity radiusuna yeterli bosluk
            w.Add(new Segment(520, 160, 640, 160));
            w.Add(new Segment(520, 600, 640, 600));

            level.Waypoints = new List<Vector2D>
            {
                // Sol sutun (x=80: sol adanin solunda, duvara uzak)
                new Vector2D(80,  100), new Vector2D(80,  300), new Vector2D(80,  480), new Vector2D(80,  660),
                // Sol-orta gecit
                new Vector2D(220, 100), new Vector2D(220, 300), new Vector2D(220, 480), new Vector2D(220, 660),
                // Orta gecit – sol adanin sag tarafindan kuzey/guney
                new Vector2D(380, 100), new Vector2D(380, 460), new Vector2D(380, 660),
                // Orta ada cevresi – siper y=160/600 ustunde/altinda
                new Vector2D(540, 100), new Vector2D(540, 480), new Vector2D(540, 660),
                new Vector2D(720, 100), new Vector2D(720, 480), new Vector2D(720, 660),
                // Orta-sag gecit
                new Vector2D(740, 200), new Vector2D(740, 380), new Vector2D(740, 540), new Vector2D(740, 660),
                // Sag ada cevresi
                new Vector2D(1000, 100),new Vector2D(1000, 220),new Vector2D(1000, 540),new Vector2D(1000, 660),
                // Sag sutun
                new Vector2D(1060, 100),new Vector2D(1060, 380),new Vector2D(1060, 560),new Vector2D(1060, 660)
            };

            // Kanal waypoint'leri: x=440 (sol ada-orta ada arası, gecit x=320-420 sağında)
            //                      x=780 (orta ada-sağ ada arası, gecit x=660-760 sağında)
            // Her iki kanal x değeri gecitlerden ve adalardan >12px uzakta.
            level.EnemySpawns.Add(new EnemySpawn(new Vector2D(80,   660), Color.Gold, 0f,
                new Vector2D(80,  100),
                new Vector2D(440, 100),
                new Vector2D(780, 100),
                new Vector2D(1060,100),
                new Vector2D(1060,660)));
            level.EnemySpawns.Add(new EnemySpawn(new Vector2D(1060, 100), Color.OrangeRed, 180f,
                new Vector2D(1060,660),
                new Vector2D(780, 660),
                new Vector2D(440, 660),
                new Vector2D(80,  660),
                new Vector2D(80,  100)));
            // Cyan (80,300) → (440,100): eski spawn oyuncuyla (80,380) arası 80px, FOV'da görünüyordu.
            // Yeni spawn rotanın ilk noktasında, oyuncudan ~456px uzakta.
            level.EnemySpawns.Add(new EnemySpawn(new Vector2D(440, 100), Color.Cyan, 270f,
                new Vector2D(440, 660),
                new Vector2D(80,  660),
                new Vector2D(80,  100),
                new Vector2D(440, 100)));
            level.EnemySpawns.Add(new EnemySpawn(new Vector2D(1060, 660), Color.Violet, 180f,
                new Vector2D(780,  660),
                new Vector2D(780,  100),
                new Vector2D(1060, 100),
                new Vector2D(1060, 660)));

            return level;
        }
    }
}
