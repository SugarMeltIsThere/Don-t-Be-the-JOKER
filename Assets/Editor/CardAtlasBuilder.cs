using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class CardAtlasBuilder
{
    private const int W = 256;
    private const int H = 340;
    private const int Cols = 13;
    private const int Rows = 5;
    private const int FontSize = 160;
    private const float CornerRadius = 14f;
    private const string AtlasPath = "Assets/Sprites/cards_real.png";
    private const string DatabasePath = "Assets/CardSpriteDatabase.asset";

    private static readonly Color Paper = new Color(0.975f, 0.965f, 0.93f);
    private static readonly Color Red = new Color(0.76f, 0.07f, 0.1f);
    private static readonly Color Black = new Color(0.07f, 0.07f, 0.09f);
    private static readonly Color Gold = new Color(0.78f, 0.6f, 0.24f);
    private static readonly Color BackRed = new Color(0.5f, 0.06f, 0.08f);

    private static readonly float[] PipX = { 0.3f, 0.5f, 0.7f };

    private static Font s_font;
    private static Texture2D s_fontTexture;
    private static Color[] s_cell;
    private static bool s_mirror;

    // 실제 트럼프 같은 카드 52장, 뒷면, 조커를 한 장의 아틀라스로 그려 저장하고 카드 데이터베이스와 머티리얼에 연결한다
    [MenuItem("Tools/DBtJ/Build Cards")]
    public static void Build()
    {
        PrepareFont();
        int width = W * Cols;
        int height = H * Rows;
        Color32[] atlas = new Color32[width * height];

        CardSpriteDatabase db = AssetDatabase.LoadAssetAtPath<CardSpriteDatabase>(DatabasePath);
        SerializedObject so = new SerializedObject(db);
        SerializedProperty entries = so.FindProperty("_entries");
        for (int i = 0; i < entries.arraySize; i++)
        {
            SerializedProperty e = entries.GetArrayElementAtIndex(i);
            Rank rank = (Rank)e.FindPropertyRelative("rank").intValue;
            Suit suit = (Suit)e.FindPropertyRelative("suit").intValue;
            DrawFace(rank, suit);
            Blit(atlas, width, i / Cols, i % Cols);
        }
        DrawJoker();
        Blit(atlas, width, 4, 0);
        DrawBack();
        Blit(atlas, width, 4, 1);

        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.SetPixels32(atlas);
        texture.Apply();
        File.WriteAllBytes(AtlasPath, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);
        Object.DestroyImmediate(s_fontTexture);
        AssetDatabase.ImportAsset(AtlasPath);

        ImportAsSprites(entries.arraySize);
        Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(AtlasPath))
            if (asset is Sprite sprite) sprites[sprite.name] = sprite;

        for (int i = 0; i < entries.arraySize; i++)
            entries.GetArrayElementAtIndex(i).FindPropertyRelative("sprite").objectReferenceValue = sprites[CellName(i / Cols, i % Cols)];
        so.FindProperty("_jokerSprite").objectReferenceValue = sprites[CellName(4, 0)];
        so.FindProperty("_cardBackSprite").objectReferenceValue = sprites[CellName(4, 1)];
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(db);

        Texture2D atlasTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(AtlasPath);
        Material card = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Card.mat");
        card.SetTexture("_BaseMap", atlasTexture);
        card.SetColor("_BaseColor", new Color(0.86f, 0.85f, 0.83f));
        card.SetFloat("_AlphaClip", 1f);
        card.SetFloat("_Cutoff", 0.5f);
        card.EnableKeyword("_ALPHATEST_ON");
        card.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
        EditorUtility.SetDirty(card);
        Material reveal = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Props/CardReveal.mat");
        if (reveal != null)
        {
            reveal.SetTexture("_BaseMap", atlasTexture);
            EditorUtility.SetDirty(reveal);
        }
        AssetDatabase.SaveAssets();
    }

    // 운영체제 세리프 글꼴에서 쓸 글자를 굽고 읽을 수 있는 사본을 만든다
    private static void PrepareFont()
    {
        s_font = Font.CreateDynamicFontFromOSFont(new[] { "Times New Roman", "Georgia" }, FontSize);
        s_font.RequestCharactersInTexture("A1234567890JQKOER", FontSize, FontStyle.Bold);
        Texture source = s_font.material.mainTexture;
        RenderTexture rt = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);
        Graphics.Blit(source, rt);
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = rt;
        s_fontTexture = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
        s_fontTexture.ReadPixels(new UnityEngine.Rect(0, 0, source.width, source.height), 0, 0);
        s_fontTexture.Apply();
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(rt);
    }

    // 숫자, 에이스, 그림 카드에 맞는 앞면을 한 칸에 그린다
    private static void DrawFace(Rank rank, Suit suit)
    {
        Background(Paper, true);
        Color ink = suit == Suit.Heart || suit == Suit.Diamond ? Red : Black;
        string label = RankLabel(rank);
        CornerIndex(label, suit, ink);

        int value = (int)rank;
        if (rank == Rank.Ace)
        {
            Pip(W * 0.5f, H * 0.5f, H * (suit == global::Suit.Spade ? 0.42f : 0.34f), suit, ink, false);
        }
        else if (rank == Rank.Jack || rank == Rank.Queen || rank == Rank.King)
        {
            CourtCard(label, suit, ink);
        }
        else
        {
            foreach (Vector2 pip in PipLayout(value))
            {
                float x = Mathf.Lerp(W * 0.2f, W * 0.8f, (pip.x - 0.2f) / 0.6f);
                float y = Mathf.Lerp(H * 0.15f, H * 0.85f, pip.y);
                Pip(x, y, H * 0.14f, suit, ink, pip.y < 0.5f);
            }
        }
    }

    // 랭크를 카드에 찍을 글자로 바꾼다
    private static string RankLabel(Rank rank) => rank switch
    {
        Rank.Ace => "A",
        Rank.Jack => "J",
        Rank.Queen => "Q",
        Rank.King => "K",
        _ => ((int)rank).ToString()
    };

    // 표준 트럼프의 무늬 배치를 0~1 좌표로 돌려준다(아래쪽 절반은 거꾸로 찍힌다)
    private static List<Vector2> PipLayout(int value)
    {
        float l = PipX[0], c = PipX[1], r = PipX[2];
        List<Vector2> p = new List<Vector2>();
        switch (value)
        {
            case 2: p.Add(new Vector2(c, 1f)); p.Add(new Vector2(c, 0f)); break;
            case 3: p.Add(new Vector2(c, 1f)); p.Add(new Vector2(c, 0.5f)); p.Add(new Vector2(c, 0f)); break;
            case 4: Corners(p, l, r); break;
            case 5: Corners(p, l, r); p.Add(new Vector2(c, 0.5f)); break;
            case 6: Corners(p, l, r); p.Add(new Vector2(l, 0.5f)); p.Add(new Vector2(r, 0.5f)); break;
            case 7: Corners(p, l, r); p.Add(new Vector2(l, 0.5f)); p.Add(new Vector2(r, 0.5f)); p.Add(new Vector2(c, 0.75f)); break;
            case 8: Corners(p, l, r); p.Add(new Vector2(l, 0.5f)); p.Add(new Vector2(r, 0.5f)); p.Add(new Vector2(c, 0.75f)); p.Add(new Vector2(c, 0.25f)); break;
            case 9:
                foreach (float y in new[] { 1f, 0.667f, 0.333f, 0f }) { p.Add(new Vector2(l, y)); p.Add(new Vector2(r, y)); }
                p.Add(new Vector2(c, 0.5f));
                break;
            default:
                foreach (float y in new[] { 1f, 0.667f, 0.333f, 0f }) { p.Add(new Vector2(l, y)); p.Add(new Vector2(r, y)); }
                p.Add(new Vector2(c, 0.833f));
                p.Add(new Vector2(c, 0.167f));
                break;
        }
        return p;
    }

    // 네 모서리 무늬 자리를 더한다
    private static void Corners(List<Vector2> p, float l, float r)
    {
        p.Add(new Vector2(l, 1f));
        p.Add(new Vector2(r, 1f));
        p.Add(new Vector2(l, 0f));
        p.Add(new Vector2(r, 0f));
    }

    // 왼쪽 위와 (거꾸로 돌린) 오른쪽 아래에 랭크 글자와 작은 무늬를 찍는다
    private static void CornerIndex(string label, Suit suit, Color ink)
    {
        for (int pass = 0; pass < 2; pass++)
        {
            s_mirror = pass == 1;
            float cx = W * 0.115f;
            Text(label, cx, H - H * 0.155f, H * 0.095f, ink);
            Pip(cx, H - H * 0.215f, H * 0.065f, suit, ink, false);
        }
        s_mirror = false;
    }

    // 그림 카드: 금테 액자 안에 위아래로 마주 보는 큰 글자와 가운데 무늬 띠를 그린다
    private static void CourtCard(string label, Suit suit, Color ink)
    {
        float x0 = W * 0.21f, x1 = W * 0.79f, y0 = H * 0.11f, y1 = H * 0.89f;
        Fill(x0, y0, x1, y1, Color.Lerp(Paper, ink, 0.08f));
        Frame(x0, y0, x1, y1, 3f, ink);
        Frame(x0 + 6f, y0 + 6f, x1 - 6f, y1 - 6f, 1.5f, Gold);
        Hatch(x0 + 9f, y0 + 9f, x1 - 9f, y1 - 9f, Color.Lerp(Paper, Gold, 0.18f));
        Fill(x0 + 3f, H * 0.5f - 1.5f, x1 - 3f, H * 0.5f + 1.5f, Gold);

        for (int pass = 0; pass < 2; pass++)
        {
            s_mirror = pass == 1;
            Text(label, W * 0.5f, H * 0.6f, H * 0.2f, ink);
            Pip(W * 0.5f, H * 0.535f, H * 0.055f, suit, ink, false);
            Pip(x0 + 22f, H - y0 - 22f, H * 0.055f, suit, Gold, false);
            Pip(x1 - 22f, H - y0 - 22f, H * 0.055f, suit, Gold, false);
        }
        s_mirror = false;
    }

    // 조커: 글자와 별 무늬
    private static void DrawJoker()
    {
        Background(Paper, true);
        for (int pass = 0; pass < 2; pass++)
        {
            s_mirror = pass == 1;
            Text("JOKER", W * 0.5f, H * 0.66f, H * 0.075f, Red);
        }
        s_mirror = false;
        Pip(W * 0.5f, H * 0.5f, H * 0.2f, global::Suit.Diamond, Gold, false);
        Frame(W * 0.12f, H * 0.09f, W * 0.88f, H * 0.91f, 2f, Gold);
    }

    // 뒷면: 흰 테두리 안 짙은 빨강 바탕에 마름모 격자, 가는 안쪽 선, 가운데 타원 장식
    private static void DrawBack()
    {
        Background(Paper, true);
        float m = W * 0.07f;
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                if (x < m || x > W - 1 - m || y < m || y > H - 1 - m) continue;
                float a = Mathf.Abs(Mathf.Repeat(x + y, 16f) - 8f);
                float b = Mathf.Abs(Mathf.Repeat(x - y, 16f) - 8f);
                float line = Mathf.Clamp01(1.4f - Mathf.Min(a, b));
                Color c = Color.Lerp(BackRed, new Color(0.86f, 0.72f, 0.68f), line * 0.75f);
                float dx = (x - W * 0.5f) / (W * 0.24f);
                float dy = (y - H * 0.5f) / (H * 0.2f);
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                if (r < 1f) c = BackRed * 0.92f;
                c = Color.Lerp(c, Paper, Mathf.Clamp01(1.2f - Mathf.Abs(r - 1f) * W * 0.24f * 0.5f));
                s_cell[y * W + x] = new Color(c.r, c.g, c.b, s_cell[y * W + x].a);
            }
        Frame(m + 6f, m + 6f, W - m - 6f, H - m - 6f, 1.5f, Paper);
        Pip(W * 0.5f, H * 0.5f, H * 0.16f, global::Suit.Spade, Paper, false);
    }

    // 둥근 모서리 종이 바탕을 칸 전체에 깔고 모서리 밖은 투명으로 둔다
    private static void Background(Color paper, bool grain)
    {
        s_cell = new Color[W * H];
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float qx = Mathf.Abs(x + 0.5f - W * 0.5f) - (W * 0.5f - CornerRadius);
                float qy = Mathf.Abs(y + 0.5f - H * 0.5f) - (H * 0.5f - CornerRadius);
                float d = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude + Mathf.Min(Mathf.Max(qx, qy), 0f) - CornerRadius;
                float alpha = Mathf.Clamp01(0.5f - d);
                float n = grain ? 1f + (Hash(x, y) - 0.5f) * 0.03f : 1f;
                s_cell[y * W + x] = new Color(paper.r * n, paper.g * n, paper.b * n, alpha);
            }
    }

    // 무늬 하나를 중심, 높이, 색으로 찍는다(뒤집기 가능, 가장자리는 거리로 부드럽게)
    private static void Pip(float cx, float cy, float size, Suit suit, Color color, bool flipped)
    {
        float half = size * 0.5f;
        int xMin = Mathf.FloorToInt(cx - half - 2), xMax = Mathf.CeilToInt(cx + half + 2);
        int yMin = Mathf.FloorToInt(cy - half - 2), yMax = Mathf.CeilToInt(cy + half + 2);
        for (int y = yMin; y <= yMax; y++)
            for (int x = xMin; x <= xMax; x++)
            {
                Vector2 p = new Vector2((x + 0.5f - cx) / half, (y + 0.5f - cy) / half);
                if (flipped) p = -p;
                float d = SuitDistance(suit, p);
                Blend(x, y, color, Mathf.Clamp01(0.5f - d * half));
            }
    }

    // 단위 상자 안 무늬 모양까지의 부호 거리(안쪽 음수)를 원과 삼각형 합으로 구한다
    private static float SuitDistance(Suit suit, Vector2 p)
    {
        switch (suit)
        {
            case global::Suit.Heart:
                return Heart(p);
            case global::Suit.Diamond:
                return (Mathf.Abs(p.x) / 0.74f + Mathf.Abs(p.y) - 1f) * 0.6f;
            case global::Suit.Spade:
                float body = Heart(new Vector2(p.x, -(p.y - 0.2f)) / 0.82f) * 0.82f;
                float stem = Triangle(p, new Vector2(-0.36f, -0.98f), new Vector2(0.36f, -0.98f), new Vector2(0f, -0.25f));
                return Mathf.Min(body, stem);
            default:
                float a = (p - new Vector2(0f, 0.44f)).magnitude - 0.42f;
                float b = (p - new Vector2(-0.46f, -0.1f)).magnitude - 0.42f;
                float c = (p - new Vector2(0.46f, -0.1f)).magnitude - 0.42f;
                float s = Triangle(p, new Vector2(-0.34f, -0.98f), new Vector2(0.34f, -0.98f), new Vector2(0f, -0.05f));
                return Mathf.Min(Mathf.Min(a, b), Mathf.Min(c, s));
        }
    }

    // 하트 모양 거리(두 원과 아래로 뾰족한 삼각형)
    private static float Heart(Vector2 p)
    {
        float left = (p - new Vector2(-0.47f, 0.4f)).magnitude - 0.5f;
        float right = (p - new Vector2(0.47f, 0.4f)).magnitude - 0.5f;
        float tip = Triangle(p, new Vector2(-0.95f, 0.26f), new Vector2(0.95f, 0.26f), new Vector2(0f, -0.98f));
        return Mathf.Min(Mathf.Min(left, right), tip);
    }

    // 볼록 삼각형까지의 근사 부호 거리
    private static float Triangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float d = float.MinValue;
        Vector2[] v = { a, b, c };
        Vector2 center = (a + b + c) / 3f;
        for (int i = 0; i < 3; i++)
        {
            Vector2 e0 = v[i];
            Vector2 e1 = v[(i + 1) % 3];
            Vector2 n = new Vector2(e1.y - e0.y, e0.x - e1.x).normalized;
            if (Vector2.Dot(n, center - e0) > 0f) n = -n;
            d = Mathf.Max(d, Vector2.Dot(p - e0, n));
        }
        return d;
    }

    // 글자열을 가운데 정렬로 찍는다(높이는 대문자 높이 기준, 글꼴 아틀라스를 이중 선형으로 샘플)
    private static void Text(string text, float cx, float baseline, float capHeight, Color color)
    {
        s_font.GetCharacterInfo('A', out CharacterInfo reference, FontSize, FontStyle.Bold);
        float scale = capHeight / Mathf.Max(1, reference.maxY);
        float tracking = text.Length > 1 && char.IsDigit(text[0]) ? -0.12f : 0.02f;
        float total = 0f;
        foreach (char ch in text)
        {
            s_font.GetCharacterInfo(ch, out CharacterInfo info, FontSize, FontStyle.Bold);
            total += (info.advance + tracking * FontSize) * scale;
        }
        float pen = cx - total * 0.5f;
        foreach (char ch in text)
        {
            s_font.GetCharacterInfo(ch, out CharacterInfo info, FontSize, FontStyle.Bold);
            float x0 = pen + info.minX * scale, x1 = pen + info.maxX * scale;
            float y0 = baseline + info.minY * scale, y1 = baseline + info.maxY * scale;
            for (int y = Mathf.FloorToInt(y0); y <= Mathf.CeilToInt(y1); y++)
                for (int x = Mathf.FloorToInt(x0); x <= Mathf.CeilToInt(x1); x++)
                {
                    float s = (x + 0.5f - x0) / (x1 - x0);
                    float t = (y + 0.5f - y0) / (y1 - y0);
                    if (s < 0f || s > 1f || t < 0f || t > 1f) continue;
                    Vector2 uv = Vector2.Lerp(Vector2.Lerp(info.uvBottomLeft, info.uvBottomRight, s), Vector2.Lerp(info.uvTopLeft, info.uvTopRight, s), t);
                    Blend(x, y, color, s_fontTexture.GetPixelBilinear(uv.x, uv.y).a);
                }
            pen += (info.advance + tracking * FontSize) * scale;
        }
    }

    // 사각형 영역을 색으로 채운다
    private static void Fill(float x0, float y0, float x1, float y1, Color color)
    {
        for (int y = Mathf.FloorToInt(y0); y < Mathf.CeilToInt(y1); y++)
            for (int x = Mathf.FloorToInt(x0); x < Mathf.CeilToInt(x1); x++)
                Blend(x, y, color, 1f);
    }

    // 사각형 테두리 선을 그린다
    private static void Frame(float x0, float y0, float x1, float y1, float width, Color color)
    {
        Fill(x0, y0, x1, y0 + width, color);
        Fill(x0, y1 - width, x1, y1, color);
        Fill(x0, y0, x0 + width, y1, color);
        Fill(x1 - width, y0, x1, y1, color);
    }

    // 액자 안쪽에 옅은 사선 빗금을 깐다
    private static void Hatch(float x0, float y0, float x1, float y1, Color color)
    {
        for (int y = Mathf.FloorToInt(y0); y < Mathf.CeilToInt(y1); y++)
            for (int x = Mathf.FloorToInt(x0); x < Mathf.CeilToInt(x1); x++)
                if (Mathf.Repeat(x + y, 7f) < 1f) Blend(x, y, color, 0.6f);
    }

    // 칸 안 한 픽셀에 색을 덮는다(뒤집기 상태면 180도 돌린 자리에 찍는다)
    private static void Blend(int x, int y, Color color, float coverage)
    {
        if (coverage <= 0f) return;
        if (s_mirror) { x = W - 1 - x; y = H - 1 - y; }
        if (x < 0 || y < 0 || x >= W || y >= H) return;
        int i = y * W + x;
        Color c = s_cell[i];
        Color mixed = Color.Lerp(c, color, coverage);
        s_cell[i] = new Color(mixed.r, mixed.g, mixed.b, c.a);
    }

    // 다 그린 칸을 아틀라스의 행, 열 자리에 옮긴다(행은 위에서부터)
    private static void Blit(Color32[] atlas, int atlasWidth, int row, int col)
    {
        int ox = col * W;
        int oy = (Rows - 1 - row) * H;
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
                atlas[(oy + y) * atlasWidth + ox + x] = s_cell[y * W + x];
    }

    // 아틀라스를 칸마다 스프라이트로 자르고 압축, 필터 설정을 맞춘다
    private static void ImportAsSprites(int faceCount)
    {
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(AtlasPath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Bilinear;
        importer.wrapMode = TextureWrapMode.Clamp;
        TextureImporterPlatformSettings settings = importer.GetDefaultPlatformTextureSettings();
        settings.maxTextureSize = 4096;
        settings.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.SetPlatformTextureSettings(settings);

        List<SpriteMetaData> metas = new List<SpriteMetaData>();
        for (int i = 0; i < faceCount + 2; i++)
        {
            int row = i < faceCount ? i / Cols : 4;
            int col = i < faceCount ? i % Cols : i - faceCount;
            metas.Add(new SpriteMetaData
            {
                name = CellName(row, col),
                rect = new UnityEngine.Rect(col * W, (Rows - 1 - row) * H, W, H),
                alignment = (int)SpriteAlignment.Center,
                pivot = new Vector2(0.5f, 0.5f)
            });
        }
#pragma warning disable 618
        importer.spritesheet = metas.ToArray();
#pragma warning restore 618
        importer.SaveAndReimport();
        AssetDatabase.ImportAsset(AtlasPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
    }

    // 칸 이름을 만든다
    private static string CellName(int row, int col) => $"real_{row}_{col}";

    // 정수 좌표로 0~1 난수를 만든다
    private static float Hash(int x, int y)
    {
        unchecked
        {
            uint h = (uint)(x * 374761393 + y * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            return ((h ^ (h >> 16)) & 0xFFFF) / 65535f;
        }
    }
}
