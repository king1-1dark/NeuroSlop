using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace NeuroSlop.EditorTools
{
    /// <summary>
    /// Produces every sprite of the MVP into Assets/NeuroSlop/Art/Generated:
    ///  • hero, portrait, backgrounds and signs are "crunched" from the AI renders in Assets/Ref
    ///    (reuse, not replacement — the inconsistency between them is intentional);
    ///  • enemies, boss and FX are procedural pixel art.
    /// All world sprites use 32 PPU, point filtering, no compression.
    /// </summary>
    public static class ArtGenerator
    {
        public const string Folder = "Assets/NeuroSlop/Art/Generated/";
        public const int PPU = 32;
        const string RefFolder = "Assets/Ref/";

        static readonly Color32 Clear = new(0, 0, 0, 0);
        static readonly Color32 Ink = new(18, 10, 26, 255);
        static readonly Color32 White = new(255, 255, 255, 255);

        public class Result
        {
            public readonly Dictionary<string, Sprite> Sprites = new();
            public Sprite this[string key] => Sprites.TryGetValue(key, out var s) ? s : throw new KeyNotFoundException("Sprite not generated: " + key);
        }

        struct Pending
        {
            public string Key;
            public Vector2 Pivot;
            public float PPU;
        }

        public static Result GenerateAll()
        {
            var pending = new List<Pending>();
            void Save(string key, PixelCanvas canvas, Vector2 pivot, float ppu = PPU)
            {
                canvas.Save(Folder + key + ".png");
                pending.Add(new Pending { Key = key, Pivot = pivot, PPU = ppu });
            }

            // ---- hero (from Ref/MainHeroV2.png, already cut out) -------------------------
            var (heroIdle, heroPunch, heroPivot) = BuildHero();
            Save("hero_idle", heroIdle, heroPivot);
            Save("hero_punch", heroPunch, heroPivot);
            Save("hero_portrait", Portrait(), new Vector2(0.5f, 0.5f));

            // ---- enemies / boss ----------------------------------------------------------
            Save("thug_idle", Thug(0), new Vector2(24f / 64f, 0f));
            Save("thug_windup", Thug(1), new Vector2(24f / 64f, 0f));
            Save("thug_attack", Thug(2), new Vector2(24f / 64f, 0f));
            Save("sprinter_idle", Sprinter(0), new Vector2(22f / 60f, 0f));
            Save("sprinter_windup", Sprinter(1), new Vector2(22f / 60f, 0f));
            Save("sprinter_attack", Sprinter(2), new Vector2(22f / 60f, 0f));
            Save("boss_body", BossBody(), new Vector2(0.5f, 0f));
            Save("boss_face_idle", BossFace(0), new Vector2(0.5f, 0.5f));
            Save("boss_face_charge", BossFace(1), new Vector2(0.5f, 0.5f));
            Save("boss_face_hurt", BossFace(2), new Vector2(0.5f, 0.5f));

            // ---- FX / props --------------------------------------------------------------
            Save("missing_block", MissingBlock(), new Vector2(0.5f, 0f));
            Save("beam", Beam(), new Vector2(0.5f, 0.5f), 16);
            Save("pixel", new PixelCanvas(4, 4).Rect(0, 0, 4, 4, White), new Vector2(0.5f, 0.5f), 4);
            Save("progress", new PixelCanvas(48, 5).Rect(0, 0, 48, 5, White), new Vector2(0f, 0.5f));
            Save("marker", Marker(), new Vector2(0.5f, 0.5f));
            Save("spark", Spark(), new Vector2(0.5f, 0.5f));
            Save("shadow", new PixelCanvas(32, 8).Ellipse(16, 4, 16, 4, new Color32(0, 0, 0, 255)), new Vector2(0.5f, 0.5f));
            Save("portal", Portal(), new Vector2(0.5f, 0f));
            Save("ground", Ground(), new Vector2(0.5f, 0.5f));
            Save("gutter", Gutter(), new Vector2(0.5f, 0.5f));
            Save("dumpster", Dumpster(), new Vector2(0.5f, 0f));

            // ---- crunched AI renders -----------------------------------------------------
            var street = PixelCanvas.FromFile(RefFolder + "Concept1.png").CropTopLeft(0, 180, 1024, 750)
                .Downscale(480, 352).Adjust(0.55f, 0.75f, 0.9f).QuantizeGenesis();
            Save("bg_street", street, new Vector2(0.5f, 0.5f));

            var poster = PixelCanvas.FromFile(RefFolder + "ChatGPT Image Apr 30, 2026, 03_26_55 AM.png");
            Save("sign_nosleep", poster.CropTopLeft(25, 385, 140, 205).Downscale(36, 52).QuantizeGenesis(), new Vector2(0.5f, 0.5f));
            Save("sign_insertcoin", poster.CropTopLeft(862, 300, 150, 180).Downscale(40, 48).QuantizeGenesis(), new Vector2(0.5f, 0.5f));
            Save("sign_gameover", poster.CropTopLeft(885, 505, 127, 230).Downscale(34, 62).QuantizeGenesis(), new Vector2(0.5f, 0.5f));
            Save("menu_art", poster.Downscale(200, 300).QuantizeGenesis(), new Vector2(0.5f, 0.5f));
            Save("results_art", PixelCanvas.FromFile(RefFolder + "MainHeroV1.png").Downscale(200, 300).QuantizeGenesis(), new Vector2(0.5f, 0.5f));

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var result = new Result();
            foreach (var p in pending)
            {
                string path = Folder + p.Key + ".png";
                ConfigureImporter(path, p.Pivot, p.PPU);
                result.Sprites[p.Key] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }
            return result;
        }

        static void ConfigureImporter(string path, Vector2 pivot, float ppu)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spritePixelsPerUnit = ppu;
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = pivot;
            settings.spriteExtrude = 0;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }

        // =============================================================================== hero

        static (PixelCanvas idle, PixelCanvas punch, Vector2 pivot) BuildHero()
        {
            const int heroHeight = 88;
            const int reach = 30;
            var src = PixelCanvas.FromFile(RefFolder + "MainHeroV2.png");
            var bounds = src.OpaqueBounds(128);
            var cropped = src.Crop(bounds.x, bounds.y, bounds.width, bounds.height);
            int heroWidth = Mathf.RoundToInt(bounds.width * heroHeight / (float)bounds.height);
            var hero = cropped.Downscale(heroWidth, heroHeight).AlphaThreshold(140).QuantizeGenesis();

            int w = heroWidth + reach + 2, h = heroHeight + 2;
            var idle = new PixelCanvas(w, h).Paste(hero, 1, 1).Outline(Ink);

            // Punch = the same single generated frame + a pasted-on arm. Yes, it has three arms now.
            var punch = new PixelCanvas(w, h).Paste(hero, 1, 1);
            int row = 1 + Mathf.RoundToInt(heroHeight * 0.66f);
            int x0 = Mathf.Max(1, punch.RightmostOpaque(row) - 4);
            var skin = new Color32(224, 160, 112, 255);
            var skinDark = new Color32(160, 96, 64, 255);
            var band = new Color32(200, 32, 32, 255);
            var metal = new Color32(182, 182, 200, 255);
            var metalDark = new Color32(96, 96, 118, 255);
            punch.Rect(x0, row - 2, 20, 5, skin).Rect(x0, row - 2, 20, 1, skinDark);
            punch.Rect(x0 + 13, row - 3, 3, 7, band);
            punch.Rect(x0 + 16, row - 4, 9, 9, metal).Rect(x0 + 16, row - 4, 9, 2, metalDark).Rect(x0 + 19, row - 2, 1, 6, metalDark).Rect(x0 + 22, row - 2, 1, 6, metalDark);
            punch.Outline(Ink);
            punch.Rect(x0 + 3, row + 5, 10, 1, White).Rect(x0 + 1, row - 5, 12, 1, White); // speed lines

            var pivot = new Vector2((1f + heroWidth * 0.5f) / w, 1f / h);
            return (idle, punch, pivot);
        }

        static PixelCanvas Portrait()
        {
            var src = PixelCanvas.FromFile(RefFolder + "ComfyUI_00123_.png");
            return src.CropTopLeft(310, 120, 290, 290).Downscale(48, 48).QuantizeGenesis();
        }

        // ============================================================================ enemies

        /// <summary>THUG_BOT: slow chrome brute. pose 0 idle, 1 windup (arm back), 2 punch.</summary>
        static PixelCanvas Thug(int pose)
        {
            var dark = new Color32(58, 62, 82, 255);
            var mid = new Color32(104, 110, 138, 255);
            var light = new Color32(164, 170, 196, 255);
            var hi = new Color32(222, 226, 240, 255);
            var red = new Color32(255, 40, 60, 255);
            var glow = new Color32(255, 160, 160, 255);
            var boot = new Color32(40, 32, 48, 255);
            var c = new PixelCanvas(64, 84);

            c.Rect(12, 0, 10, 4, boot).Rect(27, 0, 10, 4, boot);
            c.Rect(14, 4, 7, 22, dark).Rect(28, 4, 7, 22, mid).Rect(14, 13, 7, 3, light).Rect(28, 13, 7, 3, light);
            c.Rect(12, 24, 25, 7, dark);
            // back arm
            if (pose == 2) c.Rect(1, 40, 9, 6, dark).Rect(0, 38, 5, 9, mid);
            else c.Rect(3, 34, 6, 18, dark).Rect(2, 28, 8, 7, mid);
            // torso
            c.Rect(9, 30, 31, 28, mid).Rect(13, 40, 22, 15, light).Rect(14, 53, 20, 2, hi);
            c.Rect(15, 43, 18, 1, dark).Rect(15, 46, 18, 1, dark).Text("01", 17, 33, dark);
            c.Ellipse(10, 55, 7, 6, light).Ellipse(39, 55, 7, 6, light);
            c.Checker(5, 52, 6, 6, 2, new Color32(255, 0, 255, 255), new Color32(0, 0, 0, 255)); // missing texture
            // head
            c.Rect(17, 58, 15, 14, mid).Rect(18, 58, 13, 3, dark).Rect(23, 64, 10, 4, red).Rect(25, 65, 6, 2, glow);
            c.Rect(23, 72, 2, 6, dark).Rect(22, 77, 4, 2, red);
            // front arm
            switch (pose)
            {
                case 0: c.Rect(40, 34, 6, 18, mid).Rect(39, 28, 9, 7, light); break;
                case 1: c.Rect(34, 56, 6, 10, mid).Rect(30, 65, 10, 9, light).Rect(31, 72, 8, 1, hi); break;
                default: c.Rect(40, 48, 16, 6, mid).Rect(54, 45, 9, 11, light).Rect(55, 54, 7, 1, hi); break;
            }
            if (pose == 1) c.Rect(23, 64, 10, 4, new Color32(255, 220, 60, 255));
            return c.Outline(Ink);
        }

        /// <summary>SPRINT_BOT: light, fast, blade arms. pose 0 idle, 1 crouch, 2 lunge.</summary>
        static PixelCanvas Sprinter(int pose)
        {
            var dark = new Color32(42, 20, 56, 255);
            var body = new Color32(208, 48, 154, 255);
            var light = new Color32(255, 122, 208, 255);
            var green = new Color32(93, 255, 122, 255);
            var cyan = new Color32(56, 244, 255, 255);
            var c = new PixelCanvas(60, 68);

            // legs (digitigrade)
            c.Rect(10, 0, 8, 3, dark).Rect(13, 3, 4, 10, dark).Rect(15, 12, 5, 10, dark);
            c.Rect(24, 0, 9, 3, body).Rect(26, 3, 4, 10, body).Rect(24, 12, 5, 10, body);

            int oy = pose == 1 ? -4 : 0;          // crouch
            int ox = pose == 2 ? 5 : 0;           // lean into lunge
            c.Rect(14, 20 + oy, 16, 6, dark);
            c.Rect(14 + ox, 25 + oy, 16, 6, body).Rect(16 + ox, 31 + oy, 16, 6, body).Rect(18 + ox, 37 + oy, 16, 6, body).Rect(20 + ox, 43 + oy, 15, 5, body);
            c.Rect(17 + ox, 33 + oy, 14, 2, green);
            for (int i = 0; i < 9; i++) c.Rect(22 + ox, 48 + oy + i, 16 - i * 3 / 2, 1, light);
            c.Rect(31 + ox, 51 + oy, 4, 2, cyan);
            c.Rect(22 + ox, 57 + oy, 3, 4, green).Rect(26 + ox, 56 + oy, 2, 3, green);

            switch (pose)
            {
                case 0:
                    c.Rect(28, 32, 4, 10, light).Line(30, 32, 38, 24, green, 2);
                    break;
                case 1:
                    c.Rect(18, 34, 6, 3, light).Line(18, 36, 7, 42, green, 2);
                    break;
                default:
                    c.Rect(34, 40, 12, 3, light).Line(46, 41, 58, 43, green, 3);
                    break;
            }
            c.Outline(Ink);
            if (pose == 2) c.Rect(2, 30, 8, 1, White).Rect(0, 38, 10, 1, White).Rect(4, 46, 7, 1, White);
            return c;
        }

        static PixelCanvas BossBody()
        {
            var beige = new Color32(184, 176, 160, 255);
            var beigeDark = new Color32(120, 112, 100, 255);
            var beigeLight = new Color32(220, 214, 198, 255);
            var bezel = new Color32(40, 36, 44, 255);
            var screen = new Color32(8, 24, 14, 255);
            var rack = new Color32(30, 32, 40, 255);
            var rackMid = new Color32(58, 62, 76, 255);
            var amber = new Color32(255, 176, 40, 255);
            var cable = new Color32(24, 20, 30, 255);
            var c = new PixelCanvas(128, 160);

            // cables ("tentacles") behind everything
            c.Line(22, 80, 6, 0, cable, 4).Line(106, 80, 122, 0, cable, 4).Line(30, 80, 20, 0, new Color32(90, 20, 60, 255), 2);
            // server rack
            c.Rect(34, 0, 60, 80, rack);
            var leds = new[] { new Color32(60, 255, 110, 255), new Color32(255, 60, 60, 255), amber };
            for (int i = 0; i < 6; i++)
            {
                c.Rect(38, 5 + i * 10, 52, 8, rackMid).Rect(40, 7 + i * 10, 30, 1, rack).Rect(40, 9 + i * 10, 30, 1, rack);
                for (int k = 0; k < 3; k++) c.Rect(76 + k * 4, 8 + i * 10, 2, 2, leds[(i + k) % 3]);
            }
            c.Text("GEN-0", 44, 64, amber, 2);
            // CRT monitor
            c.Rect(50, 74, 28, 6, beigeDark);
            c.Rect(10, 78, 108, 74, beige).Rect(108, 78, 10, 74, beigeDark).Rect(10, 150, 108, 2, beigeLight);
            c.Rect(18, 86, 92, 60, bezel).Rect(22, 90, 84, 52, screen);
            c.Text("HELPER", 14, 80, beigeDark);
            c.Rect(100, 122, 12, 12, new Color32(255, 230, 90, 255)).Rect(102, 128, 8, 1, beigeDark).Rect(102, 125, 6, 1, beigeDark); // sticky note
            return c.Outline(Ink);
        }

        static PixelCanvas BossFace(int mood)
        {
            var col = mood switch { 1 => new Color32(255, 60, 60, 255), 2 => new Color32(255, 255, 255, 255), _ => new Color32(60, 255, 110, 255) };
            var bg = new Color32(8, 24, 14, 255);
            var c = new PixelCanvas(84, 52);
            switch (mood)
            {
                case 0:
                    c.Rect(18, 24, 12, 12, col).Rect(54, 24, 12, 12, col).Rect(24, 26, 4, 6, bg).Rect(60, 26, 4, 6, bg);
                    c.Rect(28, 10, 28, 3, col);
                    break;
                case 1:
                    c.Rect(18, 24, 12, 8, col).Rect(54, 24, 12, 8, col);
                    c.Line(16, 38, 30, 33, col, 2).Line(68, 38, 54, 33, col, 2);
                    c.Rect(30, 6, 24, 11, col).Rect(32, 8, 20, 7, bg);
                    break;
                default:
                    c.Line(18, 24, 30, 36, col, 2).Line(18, 36, 30, 24, col, 2).Line(54, 24, 66, 36, col, 2).Line(54, 36, 66, 24, col, 2);
                    c.Line(28, 12, 34, 16, col).Line(34, 16, 40, 12, col).Line(40, 12, 46, 16, col).Line(46, 16, 52, 12, col).Line(52, 12, 56, 16, col);
                    break;
            }
            return c.Scanlines(0.35f);
        }

        // ========================================================================== FX/props

        static PixelCanvas MissingBlock()
        {
            var c = new PixelCanvas(48, 48).Checker(1, 1, 46, 46, 8, new Color32(255, 0, 255, 255), new Color32(10, 0, 10, 255));
            c.Rect(13, 18, 23, 11, new Color32(10, 0, 10, 255)).Text("404", 14, 20, White, 2);
            return c.Outline(Ink);
        }

        static PixelCanvas Beam()
        {
            var c = new PixelCanvas(16, 16);
            for (int y = 0; y < 16; y++)
            {
                float d = Mathf.Abs(y - 7.5f) / 8f;
                var col = d > 0.8f ? new Color32(255, 0, 255, 255) : d > 0.5f ? new Color32(40, 240, 255, 255) : White;
                c.Rect(0, y, 16, 1, col);
            }
            return c;
        }

        static PixelCanvas Marker()
        {
            var red = new Color32(255, 40, 40, 255);
            var c = new PixelCanvas(32, 12).Ellipse(16, 6, 16, 6, red).Ellipse(16, 6, 13, 4, Clear);
            return c.Line(10, 2, 22, 10, red, 2).Line(10, 10, 22, 2, red, 2);
        }

        static PixelCanvas Spark()
        {
            var c = new PixelCanvas(32, 32);
            var orange = new Color32(255, 150, 30, 255);
            var yellow = new Color32(255, 240, 80, 255);
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4f;
                float len = i % 2 == 0 ? 15f : 9f;
                c.Line(16, 16, 16 + Mathf.RoundToInt(Mathf.Cos(a) * len), 16 + Mathf.RoundToInt(Mathf.Sin(a) * len), orange, 3);
            }
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4f;
                float len = i % 2 == 0 ? 10f : 6f;
                c.Line(16, 16, 16 + Mathf.RoundToInt(Mathf.Cos(a) * len), 16 + Mathf.RoundToInt(Mathf.Sin(a) * len), yellow, 1);
            }
            return c.Ellipse(16, 16, 4, 4, White);
        }

        static PixelCanvas Portal()
        {
            var rng = new System.Random(7);
            var palette = new[] { new Color32(255, 0, 255, 255), new Color32(40, 240, 255, 255), new Color32(0, 0, 0, 255), White, new Color32(93, 255, 122, 255) };
            var c = new PixelCanvas(32, 64);
            for (int y = 0; y < 64; y += 2)
                for (int x = 0; x < 32; x += 2)
                    if (rng.NextDouble() < 0.6) c.Rect(x, y, 2, 2, palette[rng.Next(palette.Length)]);
            return c;
        }

        static PixelCanvas Ground()
        {
            var rng = new System.Random(3);
            var c = new PixelCanvas(32, 32).Rect(0, 0, 32, 32, new Color32(28, 24, 36, 255));
            for (int i = 0; i < 70; i++)
                c.Set(rng.Next(32), rng.Next(28), rng.NextDouble() < 0.5 ? new Color32(38, 33, 48, 255) : new Color32(20, 17, 26, 255));
            c.Rect(5, 6, 1, 9, new Color32(110, 40, 100, 255)).Rect(19, 10, 1, 7, new Color32(30, 100, 120, 255)).Rect(26, 3, 1, 5, new Color32(110, 40, 100, 255));
            c.Rect(0, 28, 32, 4, new Color32(74, 68, 88, 255)).Rect(0, 31, 32, 1, new Color32(120, 110, 140, 255)).Rect(0, 27, 32, 1, new Color32(14, 10, 20, 255));
            return c;
        }

        static PixelCanvas Gutter()
        {
            return new PixelCanvas(16, 16).Rect(0, 0, 16, 16, Ink).Rect(4, 0, 8, 16, new Color32(236, 232, 220, 255));
        }

        static PixelCanvas Dumpster()
        {
            var green = new Color32(40, 110, 70, 255);
            var dark = new Color32(20, 60, 40, 255);
            var c = new PixelCanvas(72, 44);
            c.Rect(4, 4, 64, 32, green).Rect(4, 4, 64, 3, dark).Rect(2, 36, 68, 5, new Color32(30, 40, 44, 255));
            c.Rect(8, 0, 6, 4, dark).Rect(58, 0, 6, 4, dark);
            for (int x = 12; x < 64; x += 10) c.Rect(x, 8, 2, 26, dark);
            c.Rect(16, 16, 42, 9, dark).Text("OLD ASSETS", 17, 18, new Color32(190, 230, 200, 255));
            return c.Outline(Ink);
        }
    }
}
