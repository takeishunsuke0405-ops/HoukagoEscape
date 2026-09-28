using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 廊下のポスターの絵をプログラムで描いて、画像ファイル（PNG）として保存する。
/// 本物のイラストができたら、Assets/_Project/Textures の画像を差し替えればよい。
/// </summary>
public static class PosterPainter
{
    const string TextureFolder = "Assets/_Project/Textures";

    /// <summary>ClassPhoto だけ横長（視聴覚室のスクリーン用）。ほかは縦長のポスター</summary>
    public enum Design { Bullying, Chorus, ArtExhibition, Health, ArtClub, NoRunning, ClassPhoto }

    /// <summary>ポスターの画像を作って（なければ）、読み込んで返す</summary>
    public static Texture2D GetOrCreate(Design design)
    {
        string path = $"{TextureFolder}/Poster_{design}.png";
        var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (existing != null) return existing;

        Directory.CreateDirectory(TextureFolder);
        var canvas = design == Design.ClassPhoto ? new PosterCanvas(352, 198) : new PosterCanvas(256, 352);
        Paint(design, canvas);
        File.WriteAllBytes(path, canvas.ToPng());
        AssetDatabase.ImportAsset(path);
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static void Paint(Design design, PosterCanvas c)
    {
        switch (design)
        {
            case Design.Bullying:
                c.Fill(new Color(1f, 0.93f, 0.93f));
                c.Header(new Color(0.85f, 0.25f, 0.25f));
                c.Heart(128, 200, 70, new Color(1f, 0.55f, 0.6f));
                for (int i = 0; i < 3; i++) c.Person(68 + 60 * i, 60, 1f, new Color(0.3f, 0.3f, 0.35f));
                c.Line(68, 95, 188, 95, 3, new Color(0.3f, 0.3f, 0.35f));  // 手をつなぐ
                c.Scribble(90, 150, 80, 90, 7, Color.black);               // 誰かに塗りつぶされている
                break;

            case Design.Chorus:
                c.Fill(new Color(0.88f, 0.93f, 1f));
                c.Header(new Color(0.25f, 0.45f, 0.85f));
                c.Note(80, 210, new Color(0.15f, 0.2f, 0.45f));
                c.Note(140, 240, new Color(0.15f, 0.2f, 0.45f));
                c.Note(190, 200, new Color(0.15f, 0.2f, 0.45f));
                for (int i = 0; i < 5; i++) c.Person(40 + 44 * i, 60, 0.8f, new Color(0.2f, 0.25f, 0.4f));
                break;

            case Design.ArtExhibition:
                c.Fill(new Color(0.9f, 0.97f, 0.9f));
                c.Header(new Color(0.3f, 0.65f, 0.35f));
                c.Circle(120, 180, 80, new Color(0.85f, 0.7f, 0.5f));      // パレット
                c.Circle(150, 150, 18, new Color(0.9f, 0.97f, 0.9f));      // 指を入れる穴
                c.Circle(85, 215, 14, new Color(0.9f, 0.2f, 0.2f));
                c.Circle(125, 235, 14, new Color(0.2f, 0.4f, 0.9f));
                c.Circle(165, 210, 14, new Color(0.95f, 0.85f, 0.2f));
                c.Circle(80, 170, 14, new Color(0.2f, 0.7f, 0.3f));
                c.Line(170, 90, 230, 250, 6, new Color(0.5f, 0.3f, 0.15f)); // 筆
                c.Circle(170, 90, 8, new Color(0.1f, 0.1f, 0.1f));
                break;

            case Design.Health:
                c.Fill(new Color(1f, 0.95f, 0.97f));
                c.Header(new Color(0.9f, 0.5f, 0.65f));
                c.Rect(113, 190, 30, 90, new Color(0.3f, 0.7f, 0.4f));      // 十字
                c.Rect(83, 220, 90, 30, new Color(0.3f, 0.7f, 0.4f));
                c.Circle(128, 100, 50, new Color(1f, 0.85f, 0.3f));        // 笑顔
                c.Circle(110, 112, 6, Color.black);
                c.Circle(146, 112, 6, Color.black);
                c.Arc(128, 100, 28, 200f, 340f, 4, Color.black);
                break;

            case Design.ArtClub:
                c.Fill(new Color(1f, 0.98f, 0.9f));
                c.Header(new Color(0.9f, 0.75f, 0.2f));
                c.Rect(48, 80, 160, 200, new Color(0.45f, 0.3f, 0.15f));   // 額縁
                c.Rect(58, 90, 140, 180, new Color(0.6f, 0.8f, 1f));       // 空
                c.Triangle(58, 90, 150, 90, 104, 190, new Color(0.3f, 0.6f, 0.3f));
                c.Triangle(110, 90, 198, 90, 160, 170, new Color(0.25f, 0.5f, 0.25f));
                c.Circle(165, 235, 20, new Color(1f, 0.55f, 0.2f));        // 夕日
                break;

            case Design.NoRunning:
                c.Fill(Color.white);
                c.Header(new Color(0.5f, 0.5f, 0.5f));
                c.RunningPerson(128, 110, new Color(0.2f, 0.2f, 0.25f));
                c.Ring(128, 170, 85, 12, new Color(0.85f, 0.15f, 0.15f));
                c.Line(68, 110, 188, 230, 12, new Color(0.85f, 0.15f, 0.15f));
                break;

            case Design.ClassPhoto:
                PaintClassPhoto(c);
                break;
        }
    }

    /// <summary>
    /// 2年3組のクラス写真。3列に並んだ生徒のうち、後ろの列の2人（佐藤と山本）の顔が黒く塗りつぶされている。
    /// 右端に一人だけ、離れて立っている生徒（相沢）がいる。
    /// </summary>
    static void PaintClassPhoto(PosterCanvas c)
    {
        var skin = new Color(0.9f, 0.78f, 0.66f);
        var hair = new Color(0.12f, 0.1f, 0.1f);
        var uniform = new Color(0.18f, 0.2f, 0.3f);

        c.Fill(new Color(0.95f, 0.95f, 0.93f));                                  // 写真の白いふち
        c.Rect(8, 8, c.Width - 16, c.Height - 16, new Color(0.72f, 0.68f, 0.6f)); // 背景（校舎の壁）
        c.Rect(8, 8, c.Width - 16, 40, new Color(0.55f, 0.5f, 0.42f));           // 地面

        int[] rowY = { 30, 70, 110 };
        for (int row = 0; row < rowY.Length; row++)
        {
            for (int i = 0; i < 8; i++)
            {
                int x = 32 + 36 * i + (row % 2) * 10;
                c.Bust(x, rowY[row], skin, hair, uniform);

                // 後ろの列の3人目と4人目（佐藤と山本）の顔だけ、黒く塗りつぶされている
                if (row == 2 && (i == 2 || i == 3))
                {
                    c.Circle(x, rowY[row] + 30, 12, Color.black);
                    c.Scribble(x - 15, rowY[row] + 16, 30, 30, 3, Color.black);
                }
            }
        }

        // 右端に、一人だけ離れて立っている
        c.Bust(c.Width - 26, 70, new Color(0.8f, 0.8f, 0.78f), hair, uniform);
    }

    /// <summary>簡単なお絵かき用のキャンバス（左下が原点）</summary>
    class PosterCanvas
    {
        public readonly int Width;
        public readonly int Height;
        readonly Color[] pixels;
        readonly System.Random random = new(3);

        public PosterCanvas(int width, int height)
        {
            Width = width;
            Height = height;
            pixels = new Color[width * height];
        }

        /// <summary>胸から上の人（y は肩の下）</summary>
        public void Bust(int x, int y, Color skin, Color hair, Color uniform)
        {
            Circle(x - 8, y + 10, 8, uniform);
            Circle(x + 8, y + 10, 8, uniform);
            Rect(x - 16, y, 32, 10, uniform);
            Circle(x, y + 33, 11, hair);
            Circle(x, y + 29, 10, skin);
        }

        public byte[] ToPng()
        {
            var texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
            texture.SetPixels(pixels);
            texture.Apply();
            byte[] png = texture.EncodeToPNG();
            Object.DestroyImmediate(texture);
            return png;
        }

        void Set(int x, int y, Color color)
        {
            if (x < 0 || y < 0 || x >= Width || y >= Height) return;
            pixels[y * Width + x] = color;
        }

        public void Fill(Color color)
        {
            for (int i = 0; i < pixels.Length; i++) pixels[i] = color;
        }

        public void Rect(int x, int y, int w, int h, Color color)
        {
            for (int j = y; j < y + h; j++)
                for (int i = x; i < x + w; i++)
                    Set(i, j, color);
        }

        /// <summary>上の帯と、文字の代わりの白い線、下の小さな文字の線</summary>
        public void Header(Color color)
        {
            Rect(0, Height - 60, Width, 60, color);
            Rect(24, Height - 28, 208, 10, Color.white);
            Rect(48, Height - 48, 160, 10, Color.white);
            var gray = new Color(0.45f, 0.45f, 0.45f);
            Rect(40, 20, 176, 5, gray);
            Rect(70, 10, 116, 5, gray);
        }

        public void Circle(int cx, int cy, int r, Color color)
        {
            for (int y = -r; y <= r; y++)
                for (int x = -r; x <= r; x++)
                    if (x * x + y * y <= r * r) Set(cx + x, cy + y, color);
        }

        public void Ring(int cx, int cy, int r, int thickness, Color color)
        {
            int inner = r - thickness;
            for (int y = -r; y <= r; y++)
                for (int x = -r; x <= r; x++)
                {
                    int d = x * x + y * y;
                    if (d <= r * r && d >= inner * inner) Set(cx + x, cy + y, color);
                }
        }

        public void Arc(int cx, int cy, int r, float fromDegrees, float toDegrees, int thickness, Color color)
        {
            for (float a = fromDegrees; a <= toDegrees; a += 2f)
            {
                float rad = a * Mathf.Deg2Rad;
                Circle(cx + Mathf.RoundToInt(Mathf.Cos(rad) * r), cy + Mathf.RoundToInt(Mathf.Sin(rad) * r), thickness / 2, color);
            }
        }

        public void Line(int x0, int y0, int x1, int y1, int thickness, Color color)
        {
            int steps = Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0));
            for (int i = 0; i <= steps; i++)
            {
                float t = steps == 0 ? 0f : i / (float)steps;
                Circle(Mathf.RoundToInt(Mathf.Lerp(x0, x1, t)), Mathf.RoundToInt(Mathf.Lerp(y0, y1, t)), thickness / 2, color);
            }
        }

        public void Triangle(int x0, int y0, int x1, int y1, int x2, int y2, Color color)
        {
            int minX = Mathf.Min(x0, Mathf.Min(x1, x2)), maxX = Mathf.Max(x0, Mathf.Max(x1, x2));
            int minY = Mathf.Min(y0, Mathf.Min(y1, y2)), maxY = Mathf.Max(y0, Mathf.Max(y1, y2));
            for (int y = minY; y <= maxY; y++)
                for (int x = minX; x <= maxX; x++)
                {
                    float d0 = (x1 - x0) * (y - y0) - (y1 - y0) * (x - x0);
                    float d1 = (x2 - x1) * (y - y1) - (y2 - y1) * (x - x1);
                    float d2 = (x0 - x2) * (y - y2) - (y0 - y2) * (x - x2);
                    bool hasNeg = d0 < 0 || d1 < 0 || d2 < 0;
                    bool hasPos = d0 > 0 || d1 > 0 || d2 > 0;
                    if (!(hasNeg && hasPos)) Set(x, y, color);
                }
        }

        public void Heart(int cx, int cy, int size, Color color)
        {
            for (int y = -size; y <= size; y++)
                for (int x = -size; x <= size; x++)
                {
                    float u = x / (float)size * 1.3f;
                    float v = y / (float)size * 1.3f;
                    float a = u * u + v * v - 1f;
                    if (a * a * a - u * u * v * v * v <= 0f) Set(cx + x, cy + y, color);
                }
        }

        public void Note(int x, int y, Color color)
        {
            Circle(x, y, 14, color);
            Rect(x + 10, y, 5, 60, color);
            Line(x + 12, y + 60, x + 30, y + 45, 5, color);
        }

        /// <summary>棒人間（y は足元）</summary>
        public void Person(int x, int y, float scale, Color color)
        {
            int S(float v) => Mathf.RoundToInt(v * scale);
            Circle(x, y + S(75), S(12), color);
            Line(x, y + S(62), x, y + S(28), S(5), color);
            Line(x, y + S(52), x - S(18), y + S(35), S(4), color);
            Line(x, y + S(52), x + S(18), y + S(35), S(4), color);
            Line(x, y + S(28), x - S(12), y, S(5), color);
            Line(x, y + S(28), x + S(12), y, S(5), color);
        }

        public void RunningPerson(int x, int y, Color color)
        {
            Circle(x + 15, y + 110, 16, color);
            Line(x + 8, y + 92, x - 8, y + 45, 8, color);
            Line(x + 4, y + 80, x + 40, y + 70, 6, color);
            Line(x + 4, y + 80, x - 30, y + 90, 6, color);
            Line(x - 8, y + 45, x + 30, y + 15, 7, color);
            Line(x - 8, y + 45, x - 45, y + 20, 7, color);
        }

        /// <summary>ぐちゃぐちゃの黒い線（塗りつぶし）</summary>
        public void Scribble(int x, int y, int w, int h, int thickness, Color color)
        {
            int px = x, py = y;
            for (int i = 0; i < 18; i++)
            {
                int nx = x + random.Next(w);
                int ny = y + random.Next(h);
                Line(px, py, nx, ny, thickness, color);
                px = nx;
                py = ny;
            }
        }
    }
}
