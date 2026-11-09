using UnityEngine;

public static class PowerUpIconLibrary
{
    const int Size = 64;
    static readonly Color[] colors =
    {
        new Color(0.18f, 0.72f, 0.92f),
        new Color(0.63f, 0.35f, 0.90f),
        new Color(0.78f, 0.83f, 0.20f),
        new Color(0.98f, 0.30f, 0.13f)
    };
    static Texture2D[] textures;

    public static Color GetColor(BoatPowerUpType type) => colors[(int)type];

    public static Texture2D GetTexture(BoatPowerUpType type)
    {
        EnsureTextures();
        return textures[(int)type];
    }

    public static Material CreateBillboardMaterial(BoatPowerUpType type)
    {
        Shader shader = BoatRenderSetup.Unlit();
        var material = new Material(shader)
        {
            name = "Procedural Power-Up Icon " + type,
            mainTexture = GetTexture(type),
            renderQueue = 3000
        };
        BoatRenderSetup.Paint(material, Color.white);
        if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", GetTexture(type));
        return material;
    }

    static void EnsureTextures()
    {
        if (textures != null) return;
        textures = new Texture2D[4];
        for (int i = 0; i < textures.Length; i++)
        {
            var pixels = new Color32[Size * Size];
            textures[i] = new Texture2D(Size, Size, TextureFormat.RGBA32, false, true)
            {
                name = "Procedural Power-Up Icon " + (BoatPowerUpType)i,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            if (i == (int)BoatPowerUpType.Shield) DrawShield(pixels);
            else if (i == (int)BoatPowerUpType.Magnet) DrawMagnet(pixels);
            else if (i == (int)BoatPowerUpType.DoubleScore) DrawDouble(pixels);
            else DrawBolt(pixels);
            textures[i].SetPixels32(pixels);
            textures[i].Apply(false, true);
        }
    }

    static void DrawShield(Color32[] pixels)
    {
        Vector2[] points = { new Vector2(32, 58), new Vector2(51, 49), new Vector2(50, 30), new Vector2(43, 16),
            new Vector2(32, 9), new Vector2(21, 16), new Vector2(14, 30), new Vector2(13, 49) };
        FillPolygon(pixels, points, new Color32(17, 42, 62, 255), 4);
        Vector2[] inner = { new Vector2(32, 52), new Vector2(45, 46), new Vector2(44, 31), new Vector2(39, 21),
            new Vector2(32, 16), new Vector2(25, 21), new Vector2(20, 31), new Vector2(19, 46) };
        FillPolygon(pixels, inner, new Color32(248, 253, 255, 255), 0);
        DrawSegment(pixels, 23, 34, 30, 41, 2.8f, new Color32(26, 106, 145, 255));
        DrawSegment(pixels, 30, 41, 42, 27, 2.8f, new Color32(26, 106, 145, 255));
    }

    static void DrawMagnet(Color32[] pixels)
    {
        Color32 outline = new Color32(17, 42, 62, 255);
        Color32 body = new Color32(248, 253, 255, 255);
        DrawSegment(pixels, 18, 16, 18, 37, 8f, outline);
        DrawSegment(pixels, 18, 37, 23, 46, 8f, outline);
        DrawSegment(pixels, 23, 46, 32, 50, 8f, outline);
        DrawSegment(pixels, 32, 50, 41, 46, 8f, outline);
        DrawSegment(pixels, 41, 46, 46, 37, 8f, outline);
        DrawSegment(pixels, 46, 37, 46, 16, 8f, outline);
        DrawSegment(pixels, 18, 21, 18, 37, 4f, body);
        DrawSegment(pixels, 18, 37, 23, 46, 4f, body);
        DrawSegment(pixels, 23, 46, 32, 50, 4f, body);
        DrawSegment(pixels, 32, 50, 41, 46, 4f, body);
        DrawSegment(pixels, 41, 46, 46, 37, 4f, body);
        DrawSegment(pixels, 46, 37, 46, 21, 4f, body);
        DrawSegment(pixels, 15, 14, 21, 14, 8f, new Color32(242, 82, 70, 255));
        DrawSegment(pixels, 43, 14, 49, 14, 8f, new Color32(76, 164, 248, 255));
    }

    static void DrawDouble(Color32[] pixels)
    {
        string[] x = { "10001", "01010", "00100", "00100", "01010", "10001", "10001" };
        string[] two = { "11111", "00001", "00010", "00100", "01000", "10000", "11111" };
        DrawGlyph(pixels, x, 5, 5);
        DrawGlyph(pixels, two, 34, 5);
    }

    static void DrawGlyph(Color32[] pixels, string[] rows, int startX, int startY)
    {
        Color32 shadow = new Color32(17, 42, 62, 255);
        Color32 face = new Color32(248, 253, 255, 255);
        for (int y = 0; y < rows.Length; y++)
            for (int x = 0; x < rows[y].Length; x++)
                if (rows[y][x] == '1')
                {
                    FillRect(pixels, startX + x * 5 - 1, startY + (rows.Length - y - 1) * 6 - 1, 7, 8, shadow);
                    FillRect(pixels, startX + x * 5, startY + (rows.Length - y - 1) * 6, 5, 6, face);
                }
    }

    static void DrawBolt(Color32[] pixels)
    {
        Vector2[] points = { new Vector2(39, 60), new Vector2(15, 33), new Vector2(28, 33),
            new Vector2(23, 6), new Vector2(50, 38), new Vector2(36, 38) };
        FillPolygon(pixels, points, new Color32(17, 42, 62, 255), 4);
        Vector2[] inner = { new Vector2(37, 50), new Vector2(23, 37), new Vector2(34, 37),
            new Vector2(30, 17), new Vector2(43, 34), new Vector2(32, 34) };
        FillPolygon(pixels, inner, new Color32(255, 249, 235, 255), 0);
    }

    static void FillPolygon(Color32[] pixels, Vector2[] points, Color32 color, float outlineWidth)
    {
        for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                bool inside = PointInPolygon(x + 0.5f, y + 0.5f, points);
                if (inside) Set(pixels, x, y, color);
                else if (outlineWidth > 0f && DistanceToPolygon(x + 0.5f, y + 0.5f, points) <= outlineWidth)
                    Set(pixels, x, y, new Color32(17, 42, 62, 255));
            }
    }

    static bool PointInPolygon(float x, float y, Vector2[] points)
    {
        bool inside = false;
        for (int i = 0, j = points.Length - 1; i < points.Length; j = i++)
        {
            if ((points[i].y > y) != (points[j].y > y) &&
                x < (points[j].x - points[i].x) * (y - points[i].y) / (points[j].y - points[i].y) + points[i].x)
                inside = !inside;
        }
        return inside;
    }

    static float DistanceToPolygon(float x, float y, Vector2[] points)
    {
        float nearest = float.MaxValue;
        for (int i = 0; i < points.Length; i++)
        {
            Vector2 a = points[i];
            Vector2 b = points[(i + 1) % points.Length];
            Vector2 point = new Vector2(x, y);
            Vector2 edge = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(point - a, edge) / Mathf.Max(0.0001f, edge.sqrMagnitude));
            nearest = Mathf.Min(nearest, Vector2.Distance(point, a + edge * t));
        }
        return nearest;
    }

    static void DrawSegment(Color32[] pixels, float x0, float y0, float x1, float y1, float thickness, Color32 color)
    {
        int minX = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(x0, x1) - thickness));
        int maxX = Mathf.Min(Size - 1, Mathf.CeilToInt(Mathf.Max(x0, x1) + thickness));
        int minY = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(y0, y1) - thickness));
        int maxY = Mathf.Min(Size - 1, Mathf.CeilToInt(Mathf.Max(y0, y1) + thickness));
        Vector2 a = new Vector2(x0, y0);
        Vector2 edge = new Vector2(x1 - x0, y1 - y0);
        for (int y = minY; y <= maxY; y++)
            for (int x = minX; x <= maxX; x++)
            {
                Vector2 point = new Vector2(x + 0.5f, y + 0.5f);
                float t = Mathf.Clamp01(Vector2.Dot(point - a, edge) / Mathf.Max(0.0001f, edge.sqrMagnitude));
                if (Vector2.Distance(point, a + edge * t) <= thickness * 0.5f) Set(pixels, x, y, color);
            }
    }

    static void FillRect(Color32[] pixels, int x, int y, int width, int height, Color32 color)
    {
        for (int py = Mathf.Max(0, y); py < Mathf.Min(Size, y + height); py++)
            for (int px = Mathf.Max(0, x); px < Mathf.Min(Size, x + width); px++) Set(pixels, px, py, color);
    }

    static void Set(Color32[] pixels, int x, int y, Color32 color)
    {
        if (x >= 0 && x < Size && y >= 0 && y < Size) pixels[y * Size + x] = color;
    }
}
