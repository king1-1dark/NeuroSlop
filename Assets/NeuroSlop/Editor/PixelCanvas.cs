using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace NeuroSlop.EditorTools
{
    /// <summary>
    /// Tiny software canvas used by the art generator: primitives for procedural pixel art and
    /// the "crunch" pipeline that turns high-res AI renders into 9-bit (Sega Genesis) sprites.
    /// Origin is bottom-left, like Texture2D.
    /// </summary>
    public class PixelCanvas
    {
        public readonly int W;
        public readonly int H;
        public readonly Color32[] Px;

        public PixelCanvas(int w, int h)
        {
            W = w;
            H = h;
            Px = new Color32[w * h];
        }

        public static PixelCanvas FromFile(string path)
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.LoadImage(File.ReadAllBytes(path));
            var c = new PixelCanvas(tex.width, tex.height);
            tex.GetPixels32().CopyTo(c.Px, 0);
            Object.DestroyImmediate(tex);
            return c;
        }

        public bool In(int x, int y) => x >= 0 && y >= 0 && x < W && y < H;
        public Color32 Get(int x, int y) => In(x, y) ? Px[y * W + x] : default;

        public void Set(int x, int y, Color32 c)
        {
            if (In(x, y)) Px[y * W + x] = c;
        }

        // ---- primitives -------------------------------------------------------------------

        public PixelCanvas Rect(int x, int y, int w, int h, Color32 c)
        {
            for (int j = y; j < y + h; j++)
                for (int i = x; i < x + w; i++)
                    Set(i, j, c);
            return this;
        }

        public PixelCanvas Ellipse(float cx, float cy, float rx, float ry, Color32 c)
        {
            for (int j = Mathf.FloorToInt(cy - ry); j <= Mathf.CeilToInt(cy + ry); j++)
                for (int i = Mathf.FloorToInt(cx - rx); i <= Mathf.CeilToInt(cx + rx); i++)
                {
                    float dx = (i + 0.5f - cx) / rx, dy = (j + 0.5f - cy) / ry;
                    if (dx * dx + dy * dy <= 1f) Set(i, j, c);
                }
            return this;
        }

        public PixelCanvas Line(int x0, int y0, int x1, int y1, Color32 c, int thickness = 1)
        {
            int steps = Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0), 1);
            int r = thickness / 2;
            for (int s = 0; s <= steps; s++)
            {
                int x = Mathf.RoundToInt(Mathf.Lerp(x0, x1, s / (float)steps));
                int y = Mathf.RoundToInt(Mathf.Lerp(y0, y1, s / (float)steps));
                Rect(x - r, y - r, thickness, thickness, c);
            }
            return this;
        }

        public PixelCanvas Checker(int x, int y, int w, int h, int cell, Color32 a, Color32 b)
        {
            for (int j = 0; j < h; j++)
                for (int i = 0; i < w; i++)
                    Set(x + i, y + j, ((i / cell) + (j / cell)) % 2 == 0 ? a : b);
            return this;
        }

        /// <summary>1px outline around every opaque pixel (4-neighbourhood).</summary>
        public PixelCanvas Outline(Color32 c)
        {
            var copy = (Color32[])Px.Clone();
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    if (copy[y * W + x].a > 0) continue;
                    bool edge = (x > 0 && copy[y * W + x - 1].a > 0) || (x < W - 1 && copy[y * W + x + 1].a > 0) ||
                                (y > 0 && copy[(y - 1) * W + x].a > 0) || (y < H - 1 && copy[(y + 1) * W + x].a > 0);
                    if (edge) Px[y * W + x] = c;
                }
            return this;
        }

        public PixelCanvas Paste(PixelCanvas src, int x, int y)
        {
            for (int j = 0; j < src.H; j++)
                for (int i = 0; i < src.W; i++)
                {
                    var c = src.Px[j * src.W + i];
                    if (c.a > 0) Set(x + i, y + j, c);
                }
            return this;
        }

        // ---- 3x5 pixel font ---------------------------------------------------------------

        static readonly Dictionary<char, string> Glyphs = new()
        {
            ['A'] = ".#.#.#####.##.#", ['B'] = "##.#.###.#.###.", ['C'] = ".###..#..#...##", ['D'] = "##.#.##.##.###.",
            ['E'] = "####..##.#..###", ['F'] = "####..##.#..#..", ['G'] = ".###..#.##.#.##", ['H'] = "#.##.#####.##.#",
            ['I'] = "###.#..#..#.###", ['J'] = "..#..#..##.#.#.", ['K'] = "#.##.###.#.##.#", ['L'] = "#..#..#..#..###",
            ['M'] = "#.########.##.#", ['N'] = "##.#.##.##.##.#", ['O'] = ".#.#.##.##.#.#.", ['P'] = "##.#.###.#..#..",
            ['Q'] = ".#.#.##.###..##", ['R'] = "##.#.###.#.##.#", ['S'] = ".###...#...###.", ['T'] = "###.#..#..#..#.",
            ['U'] = "#.##.##.##.####", ['V'] = "#.##.##.##.#.#.", ['W'] = "#.##.########.#", ['X'] = "#.##.#.#.#.##.#",
            ['Y'] = "#.##.#.#..#..#.", ['Z'] = "###..#.#.#..###", ['0'] = "####.##.##.####", ['1'] = ".#.##..#..#.###",
            ['2'] = "##...#.#.#..###", ['3'] = "##...#.#...###.", ['4'] = "#.##.####..#..#", ['5'] = "####..##...###.",
            ['6'] = ".###..####.####", ['7'] = "###..#.#..#..#.", ['8'] = "####.#####.####", ['9'] = "####.####..###.",
            ['-'] = "......###......", ['.'] = ".............#.", ['?'] = "##...#.#.....#.", ['!'] = ".#..#..#.....#.",
            ['_'] = "............###", [':'] = "....#.....#....", [' '] = "..............."
        };

        public PixelCanvas Text(string s, int x, int y, Color32 c, int scale = 1)
        {
            int cx = x;
            foreach (char raw in s.ToUpperInvariant())
            {
                if (Glyphs.TryGetValue(raw, out var g))
                {
                    for (int row = 0; row < 5; row++)
                        for (int col = 0; col < 3; col++)
                            if (g[row * 3 + col] == '#')
                                Rect(cx + col * scale, y + (4 - row) * scale, scale, scale, c);
                }
                cx += 4 * scale;
            }
            return this;
        }

        // ---- image pipeline ---------------------------------------------------------------

        /// <summary>Crop using image coordinates (top-left origin, as in any image viewer).</summary>
        public PixelCanvas CropTopLeft(int x, int yTop, int w, int h) => Crop(x, H - yTop - h, w, h);

        public PixelCanvas Crop(int x, int y, int w, int h)
        {
            var c = new PixelCanvas(w, h);
            for (int j = 0; j < h; j++)
                for (int i = 0; i < w; i++)
                    c.Px[j * w + i] = Get(x + i, y + j);
            return c;
        }

        public RectInt OpaqueBounds(byte threshold = 128)
        {
            int minX = W, minY = H, maxX = -1, maxY = -1;
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                    if (Px[y * W + x].a >= threshold)
                    {
                        if (x < minX) minX = x;
                        if (y < minY) minY = y;
                        if (x > maxX) maxX = x;
                        if (y > maxY) maxY = y;
                    }
            return maxX < 0 ? new RectInt(0, 0, 0, 0) : new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
        }

        /// <summary>Area-average downscale with premultiplied alpha (no shimmering like point sampling).</summary>
        public PixelCanvas Downscale(int w, int h)
        {
            var d = new PixelCanvas(w, h);
            for (int y = 0; y < h; y++)
            {
                int y0 = y * H / h, y1 = Mathf.Max(y0 + 1, (y + 1) * H / h);
                for (int x = 0; x < w; x++)
                {
                    int x0 = x * W / w, x1 = Mathf.Max(x0 + 1, (x + 1) * W / w);
                    float r = 0, g = 0, b = 0, a = 0;
                    int n = 0;
                    for (int j = y0; j < y1; j++)
                        for (int i = x0; i < x1; i++)
                        {
                            var p = Px[j * W + i];
                            float pa = p.a / 255f;
                            r += p.r * pa;
                            g += p.g * pa;
                            b += p.b * pa;
                            a += pa;
                            n++;
                        }
                    d.Px[y * w + x] = a > 0.0001f
                        ? new Color32((byte)(r / a), (byte)(g / a), (byte)(b / a), (byte)(a / n * 255f))
                        : default;
                }
            }
            return d;
        }

        public PixelCanvas AlphaThreshold(byte threshold = 128)
        {
            for (int i = 0; i < Px.Length; i++)
                Px[i].a = Px[i].a >= threshold ? (byte)255 : (byte)0;
            return this;
        }

        /// <summary>Sega Genesis colour depth: 3 bits per channel, 512 colours.</summary>
        public PixelCanvas QuantizeGenesis()
        {
            for (int i = 0; i < Px.Length; i++)
            {
                var p = Px[i];
                Px[i] = new Color32(Q(p.r), Q(p.g), Q(p.b), p.a);
            }
            return this;
        }

        static byte Q(byte v) => (byte)(Mathf.Round(v / 255f * 7f) / 7f * 255f);

        public PixelCanvas Adjust(float brightness, float saturation, float contrast = 1f)
        {
            for (int i = 0; i < Px.Length; i++)
            {
                var c = (Color)Px[i];
                float l = c.r * 0.3f + c.g * 0.59f + c.b * 0.11f;
                var rgb = Vector3.Lerp(new Vector3(l, l, l), new Vector3(c.r, c.g, c.b), saturation);
                rgb = (rgb - Vector3.one * 0.5f) * contrast + Vector3.one * 0.5f;
                rgb *= brightness;
                Px[i] = new Color(Mathf.Clamp01(rgb.x), Mathf.Clamp01(rgb.y), Mathf.Clamp01(rgb.z), c.a);
            }
            return this;
        }

        /// <summary>Darkens every other row of opaque pixels — CRT scanlines.</summary>
        public PixelCanvas Scanlines(float darken)
        {
            for (int y = 0; y < H; y += 2)
                for (int x = 0; x < W; x++)
                {
                    var p = Px[y * W + x];
                    Px[y * W + x] = new Color32((byte)(p.r * (1 - darken)), (byte)(p.g * (1 - darken)), (byte)(p.b * (1 - darken)), p.a);
                }
            return this;
        }

        public int RightmostOpaque(int row)
        {
            for (int x = W - 1; x >= 0; x--)
                if (Get(x, row).a > 0) return x;
            return -1;
        }

        public void Save(string assetPath)
        {
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
            tex.SetPixels32(Px);
            tex.Apply();
            Directory.CreateDirectory(Path.GetDirectoryName(assetPath));
            File.WriteAllBytes(assetPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }
    }
}
