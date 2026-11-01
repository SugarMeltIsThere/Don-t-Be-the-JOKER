using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class RoomFactory
{
    private const float Height = 2.9f;
    private const float Back = 2.2f;
    private const float Front = -1.8f;
    private const float Side = 2.6f;
    private const float WainscotTop = 1.0f;
    private const float WindowZ0 = -0.15f;
    private const float WindowZ1 = 1.15f;
    private const float WindowY0 = 1.0f;
    private const float WindowY1 = 2.35f;
    private const float DoorZ0 = 0.85f;
    private const float DoorZ1 = 1.75f;
    private const string VolumePath = "Assets/Settings/RoomVolume.asset";

    private static readonly Vector3 ChairOpponent = new Vector3(0f, 0f, 0.86f);
    private static readonly Vector3 ChairPlayer = new Vector3(0f, 0f, -0.88f);

    // 방 텍스처와 머티리얼, 건축물과 가구, 조명과 후처리를 만들어 게임 장면에 놓는다
    [MenuItem("Tools/DBtJ/Build Room")]
    public static void Build()
    {
        BuildTextures();
        BuildMaterials();

        GameObject old = GameObject.Find("Room");
        if (old != null) Object.DestroyImmediate(old);
        Transform room = new GameObject("Room").transform;

        Place(room, "Floor", BuildFloor(), PropFactory.Mats("Planks", "Rug"), Vector3.zero, 0f, ShadowCastingMode.Off);
        Place(room, "Walls", BuildWalls(), PropFactory.Mats("Wallpaper", "Wood", "Plaster"), Vector3.zero, 0f, ShadowCastingMode.TwoSided);
        Place(room, "Window", BuildWindow(), PropFactory.Mats("Wood", "Blind", "Blind"), Vector3.zero, 0f, ShadowCastingMode.TwoSided);
        Place(room, "WindowGlass", BuildWindowGlass(), PropFactory.Mats("NightGlass"), Vector3.zero, 0f, ShadowCastingMode.Off);
        Place(room, "Door", BuildDoor(), PropFactory.Mats("Wood", "Brass"), Vector3.zero, 0f, ShadowCastingMode.On);
        Mesh chair = PropFactory.SaveMesh(BuildChair());
        Place(room, "OpponentChair", chair, PropFactory.Mats("Wood", "Leather", "Brass"), ChairOpponent, 180f, ShadowCastingMode.On);
        Place(room, "PlayerChair", chair, PropFactory.Mats("Wood", "Leather", "Brass"), ChairPlayer, 0f, ShadowCastingMode.On);
        Place(room, "Sideboard", BuildSideboard(), PropFactory.Mats("Wood", "Brass"), new Vector3(1.2f, 0f, Back - 0.25f), 0f, ShadowCastingMode.On);
        Place(room, "Bookshelf", BuildBookshelf(), PropFactory.Mats("Wood", "BookRed", "BookGreen", "BookBrown", "BookNavy"), new Vector3(-1.35f, 0f, Back - 0.18f), 0f, ShadowCastingMode.On);
        Place(room, "CoatRack", BuildCoatRack(), PropFactory.Mats("Wood", "Brass", "HatFelt"), new Vector3(-2.25f, 0f, 1.85f), 30f, ShadowCastingMode.On);
        Place(room, "SideboardWhiskey", AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Models/Procedural/WhiskeySet.asset"), PropFactory.Mats("Liquid", "BottleGlass", "Paper", "Wood", "Glass"), new Vector3(1.45f, 0.885f, Back - 0.3f), 200f, ShadowCastingMode.On);
        Place(room, "SideboardAshtray", AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Models/Procedural/Ashtray.asset"), PropFactory.Mats("Brass", "Ash", "Cigar"), new Vector3(0.85f, 0.885f, Back - 0.3f), 60f, ShadowCastingMode.On);

        Mesh sconce = PropFactory.SaveMesh(BuildSconce());
        foreach (float x in new[] { -0.6f, 0.6f })
        {
            Transform s = Place(room, "Sconce", sconce, PropFactory.Mats("Brass", "EnamelInner", "Bulb"), new Vector3(x, 1.5f, Back), 0f, ShadowCastingMode.Off);
            AddPointLight(s, new Vector3(0f, 0.05f, -0.16f), 2.2f, 1.1f);
        }
        Transform doorSconce = Place(room, "DoorSconce", sconce, PropFactory.Mats("Brass", "EnamelInner", "Bulb"), new Vector3(Side, 1.6f, DoorZ0 - 0.45f), 90f, ShadowCastingMode.Off);
        AddPointLight(doorSconce, new Vector3(0f, 0.05f, -0.16f), 2.4f, 0.9f);

        SetupLighting(room);
        SetupPostProcessing(room);
        Transform lamp = GameObject.Find("Table/Props/PendantLamp/Visual").transform;
        lamp.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;

        foreach (MeshRenderer r in room.GetComponentsInChildren<MeshRenderer>())
            GameObjectUtility.SetStaticEditorFlags(r.gameObject, StaticEditorFlags.BatchingStatic);

        EditorSceneManager.MarkSceneDirty(room.gameObject.scene);
        EditorSceneManager.SaveScene(room.gameObject.scene);
        AssetDatabase.SaveAssets();
    }

    // 마루판, 벽지, 양탄자, 가죽, 천장 텍스처를 반복되게 그려 저장한다
    private static void BuildTextures()
    {
        PropFactory.SaveTexture("Planks", 512, (u, v, x, y) =>
        {
            int plank = Mathf.FloorToInt(v * 4f);
            float shift = Hash01(plank, 3);
            float along = Mathf.Repeat(u + shift, 1f);
            float across = Mathf.Repeat(v * 4f, 1f);
            float seam = across < 0.03f || along < 0.008f ? 0.45f : 1f;
            float grain = PropFactory.Fbm(Mathf.Repeat(u + shift, 1f), v, 2, 64, 3, 50 + plank);
            float tone = 0.8f + 0.35f * Hash01(plank, 9);
            Color c = Color.Lerp(new Color(0.16f, 0.09f, 0.05f), new Color(0.3f, 0.18f, 0.1f), grain) * tone * seam;
            return c;
        }, false);

        PropFactory.SaveTexture("Wallpaper", 512, (u, v, x, y) =>
        {
            float stripe = Mathf.Abs(Mathf.Repeat(u * 8f, 1f) - 0.5f) < 0.18f ? 0.82f : 1f;
            float du = Mathf.Abs(Mathf.Repeat(u * 4f, 1f) - 0.5f);
            float dv = Mathf.Abs(Mathf.Repeat(v * 4f, 1f) - 0.5f);
            float diamond = Mathf.Abs(du + dv - 0.32f) < 0.03f ? 1f : 0f;
            float grime = 0.85f + 0.25f * PropFactory.Fbm(u, v, 4, 4, 3, 61);
            Color baseColor = new Color(0.2f, 0.055f, 0.055f) * stripe;
            return Color.Lerp(baseColor, new Color(0.32f, 0.22f, 0.1f), diamond * 0.45f) * grime;
        }, false);

        PropFactory.SaveTexture("Rug", 512, (u, v, x, y) =>
        {
            float edge = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v));
            float cx = (u - 0.5f) / 0.28f;
            float cy = (v - 0.5f) / 0.36f;
            float medallion = cx * cx + cy * cy;
            Color c;
            if (edge < 0.025f) c = new Color(0.1f, 0.08f, 0.06f);
            else if (edge < 0.09f) c = Mathf.Repeat(u * 24f + v * 24f, 1f) < 0.5f ? new Color(0.06f, 0.08f, 0.16f) : new Color(0.08f, 0.1f, 0.2f);
            else if (edge < 0.105f) c = new Color(0.45f, 0.33f, 0.14f);
            else if (medallion < 1f) c = medallion < 0.35f ? new Color(0.06f, 0.08f, 0.16f) : new Color(0.45f, 0.33f, 0.14f) * (0.8f + 0.2f * Mathf.Sin(medallion * 30f));
            else c = new Color(0.3f, 0.05f, 0.06f);
            float wear = 0.75f + 0.35f * PropFactory.Fbm(u, v, 8, 8, 3, 71);
            return c * wear;
        }, true);

        PropFactory.SaveTexture("Leather", 256, (u, v, x, y) =>
        {
            float h = 0.8f + 0.3f * PropFactory.Fbm(u, v, 16, 16, 3, 81);
            return new Color(0.24f, 0.075f, 0.045f) * h;
        }, false);

        PropFactory.SaveTexture("Plaster", 256, (u, v, x, y) =>
        {
            float h = 0.85f + 0.2f * PropFactory.Fbm(u, v, 8, 8, 3, 91);
            return new Color(0.16f, 0.15f, 0.14f) * h;
        }, false);
    }

    // 방에 쓰는 머티리얼을 만들거나 갱신한다
    private static void BuildMaterials()
    {
        PropFactory.BuildMaterials();
        PropFactory.Lit("Planks", Color.white, "Planks", null, 0f, 0f, 0.35f, 1f);
        PropFactory.Lit("Wallpaper", Color.white, "Wallpaper", null, 0f, 0f, 0.12f, 1.4f);
        PropFactory.Lit("Plaster", Color.white, "Plaster", null, 0f, 0f, 0.05f, 1f);
        Material rug = PropFactory.Lit("Rug", Color.white, "Rug", null, 0f, 0f, 0.05f, 1f);
        rug.SetTextureScale("_BaseMap", new Vector2(1f / 2.6f, 1f / 1.9f));
        PropFactory.Lit("Leather", Color.white, "Leather", null, 0f, 0f, 0.45f, 3f);
        PropFactory.Lit("Blind", new Color(0.62f, 0.58f, 0.5f), null, null, 0f, 0f, 0.3f, 1f);
        PropFactory.Lit("NightGlass", new Color(0.02f, 0.03f, 0.05f), null, null, 0f, 0f, 0.9f, 1f).SetEmission(new Color(0.08f, 0.12f, 0.22f));
        PropFactory.Lit("HatFelt", new Color(0.06f, 0.055f, 0.05f), null, null, 0f, 0f, 0.15f, 1f);
        PropFactory.Lit("BookRed", new Color(0.32f, 0.06f, 0.05f), null, null, 0f, 0f, 0.3f, 1f);
        PropFactory.Lit("BookGreen", new Color(0.07f, 0.18f, 0.1f), null, null, 0f, 0f, 0.3f, 1f);
        PropFactory.Lit("BookBrown", new Color(0.25f, 0.15f, 0.07f), null, null, 0f, 0f, 0.3f, 1f);
        PropFactory.Lit("BookNavy", new Color(0.06f, 0.08f, 0.18f), null, null, 0f, 0f, 0.3f, 1f);
    }

    // 마루와 테이블 밑 양탄자를 만든다
    private static Mesh BuildFloor()
    {
        PropMeshBuilder b = new PropMeshBuilder();
        b.Panel(new Vector3(-Side, 0f, Front), new Vector3(Side * 2f, 0f, 0f), new Vector3(0f, 0f, Back - Front), Vector3.up, 0, 1f, Vector2.zero);
        b.Panel(new Vector3(-1.3f, 0.003f, -0.85f), new Vector3(2.6f, 0f, 0f), new Vector3(0f, 0f, 1.9f), Vector3.up, 1, 1f, Vector2.zero);
        return b.ToMesh("RoomFloor");
    }

    // 네 벽(아래 나무 징두리판, 위 벽지, 걸레받이와 몰딩)과 창문 구멍, 천장을 만든다
    private static Mesh BuildWalls()
    {
        PropMeshBuilder b = new PropMeshBuilder();
        Wall(b, new Vector3(Side, 0f, Back), Vector3.left, Side * 2f, 0f, 0f);
        Wall(b, new Vector3(-Side, 0f, Front), Vector3.right, Side * 2f, 0f, 0f);
        Wall(b, new Vector3(Side, 0f, Front), Vector3.forward, Back - Front, DoorZ0 - Front, DoorZ1 - Front);

        Vector3 o = new Vector3(-Side, 0f, Back);
        Vector3 along = Vector3.back;
        Vector3 n = Vector3.right;
        float length = Back - Front;
        Wainscot(b, o, along, n, length, 0f, 0f);
        float w0 = Back - WindowZ1;
        float w1 = Back - WindowZ0;
        b.Panel(o + Vector3.up * WainscotTop, along * w0, Vector3.up * (Height - WainscotTop), n, 0, 1f, Vector2.zero);
        b.Panel(o + along * w1 + Vector3.up * WainscotTop, along * (length - w1), Vector3.up * (Height - WainscotTop), n, 0, 1f, new Vector2(w1, 0f));
        b.Panel(o + along * w0 + Vector3.up * WindowY1, along * (w1 - w0), Vector3.up * (Height - WindowY1), n, 0, 1f, new Vector2(w0, WindowY1 - WainscotTop));

        b.Panel(new Vector3(-Side, Height, Front), new Vector3(0f, 0f, Back - Front), new Vector3(Side * 2f, 0f, 0f), Vector3.down, 2, 1f, Vector2.zero);
        return b.ToMesh("RoomWalls");
    }

    // 벽 한 면을 벽지와 징두리판으로 만든다(문이 있는 구간은 판 장식을 건너뛴다)
    private static void Wall(PropMeshBuilder b, Vector3 origin, Vector3 along, float length, float skip0, float skip1)
    {
        Vector3 n = Vector3.Cross(Vector3.up, along).normalized * -1f;
        if (Vector3.Dot(n, -origin) < 0f) n = -n;
        Wainscot(b, origin, along, n, length, skip0, skip1);
        b.Panel(origin + Vector3.up * WainscotTop, along * length, Vector3.up * (Height - WainscotTop), n, 0, 1f, Vector2.zero);
    }

    // 징두리판(판, 돋운 판 장식, 걸레받이, 의자 레일, 천장 몰딩)을 벽 하나에 만든다
    private static void Wainscot(PropMeshBuilder b, Vector3 origin, Vector3 along, Vector3 n, float length, float skip0, float skip1)
    {
        b.Panel(origin, along * length, Vector3.up * WainscotTop, n, 1, 1f, Vector2.zero);
        Quaternion facing = Quaternion.LookRotation(along, Vector3.up);
        int count = Mathf.FloorToInt(length / 0.62f);
        float spacing = length / count;
        for (int i = 0; i < count; i++)
        {
            float t = (i + 0.5f) * spacing;
            if (skip1 > skip0 && t > skip0 - 0.3f && t < skip1 + 0.3f) continue;
            b.Transform = Matrix4x4.TRS(origin + along * t + n * 0.012f + Vector3.up * 0.56f, facing, Vector3.one);
            b.Box(Vector3.zero, new Vector3(0.024f, 0.62f, spacing - 0.14f), 1, 2f);
        }
        b.Transform = Matrix4x4.TRS(origin + along * (length * 0.5f), facing, Vector3.one);
        b.Box(new Vector3(-0.014f, 0.065f, 0f), new Vector3(0.028f, 0.13f, length), 1, 2f);
        b.Box(new Vector3(-0.022f, WainscotTop, 0f), new Vector3(0.044f, 0.055f, length), 1, 2f);
        b.Box(new Vector3(-0.035f, Height - 0.06f, 0f), new Vector3(0.07f, 0.12f, length), 1, 2f);
        b.Transform = Matrix4x4.identity;
    }

    // 왼쪽 벽 창문(틀, 창살, 밤하늘 유리, 반쯤 내린 블라인드)을 만든다
    private static Mesh BuildWindow()
    {
        PropMeshBuilder b = new PropMeshBuilder();
        float zc = (WindowZ0 + WindowZ1) * 0.5f;
        float yc = (WindowY0 + WindowY1) * 0.5f;
        float w = WindowZ1 - WindowZ0;
        float h = WindowY1 - WindowY0;
        float x = -Side;
        b.Box(new Vector3(x + 0.03f, WindowY1 + 0.04f, zc), new Vector3(0.08f, 0.08f, w + 0.16f), 0, 2f);
        b.Box(new Vector3(x + 0.05f, WindowY0 - 0.01f, zc), new Vector3(0.12f, 0.04f, w + 0.2f), 0, 2f);
        b.Box(new Vector3(x + 0.03f, yc, WindowZ0 - 0.04f), new Vector3(0.08f, h + 0.16f, 0.08f), 0, 2f);
        b.Box(new Vector3(x + 0.03f, yc, WindowZ1 + 0.04f), new Vector3(0.08f, h + 0.16f, 0.08f), 0, 2f);
        b.Box(new Vector3(x - 0.03f, yc, zc), new Vector3(0.03f, h, 0.03f), 0, 2f);
        b.Box(new Vector3(x - 0.03f, yc, zc), new Vector3(0.03f, 0.03f, w), 0, 2f);

        float top = WindowY1 - 0.03f;
        float bottom = WindowY0 + h * 0.3f;
        for (float y = top; y > bottom; y -= 0.045f)
        {
            b.Transform = Matrix4x4.TRS(new Vector3(x + 0.1f, y, zc), Quaternion.Euler(0f, 0f, 35f), Vector3.one);
            b.Box(Vector3.zero, new Vector3(0.04f, 0.003f, w - 0.02f), 2, 1f);
        }
        b.Transform = Matrix4x4.identity;
        b.Box(new Vector3(x + 0.1f, top + 0.02f, zc), new Vector3(0.05f, 0.03f, w), 2, 1f);
        b.Box(new Vector3(x + 0.1f, bottom - 0.01f, zc), new Vector3(0.045f, 0.012f, w - 0.02f), 2, 1f);
        return b.ToMesh("RoomWindow");
    }

    // 창밖 밤하늘처럼 보이는 유리판을 그림자 없이 따로 만든다
    private static Mesh BuildWindowGlass()
    {
        PropMeshBuilder b = new PropMeshBuilder();
        b.Panel(new Vector3(-Side - 0.05f, WindowY0, WindowZ1), new Vector3(0f, 0f, -(WindowZ1 - WindowZ0)), new Vector3(0f, WindowY1 - WindowY0, 0f), Vector3.right, 0, 1f, Vector2.zero);
        return b.ToMesh("RoomWindowGlass");
    }

    // 오른쪽 벽 문(문틀, 판넬 문짝, 황동 손잡이)을 만든다
    private static Mesh BuildDoor()
    {
        PropMeshBuilder b = new PropMeshBuilder();
        float x = Side;
        float zc = (DoorZ0 + DoorZ1) * 0.5f;
        float w = DoorZ1 - DoorZ0;
        const float h = 2.15f;
        b.Box(new Vector3(x - 0.02f, h * 0.5f, zc), new Vector3(0.04f, h, w), 0, 2f);
        b.Box(new Vector3(x - 0.035f, h + 0.05f, zc), new Vector3(0.07f, 0.1f, w + 0.2f), 0, 2f);
        b.Box(new Vector3(x - 0.035f, h * 0.5f, DoorZ0 - 0.05f), new Vector3(0.07f, h, 0.1f), 0, 2f);
        b.Box(new Vector3(x - 0.035f, h * 0.5f, DoorZ1 + 0.05f), new Vector3(0.07f, h, 0.1f), 0, 2f);
        foreach (Vector2 p in new[] { new Vector2(0.5f, 0.45f), new Vector2(1.45f, 0.75f) })
        {
            b.Box(new Vector3(x - 0.045f, p.x, zc - w * 0.22f), new Vector3(0.012f, p.y, w * 0.34f), 0, 2f);
            b.Box(new Vector3(x - 0.045f, p.x, zc + w * 0.22f), new Vector3(0.012f, p.y, w * 0.34f), 0, 2f);
        }
        b.Transform = Matrix4x4.TRS(new Vector3(x - 0.04f, 1.0f, DoorZ0 + 0.1f), Quaternion.Euler(0f, 0f, 90f), Vector3.one);
        b.Lathe(PropFactory.P(0f, 0f, 0.025f, 0f, 0.025f, 0.006f, 0.008f, 0.012f, 0.008f, 0.045f, 0.026f, 0.055f, 0.03f, 0.07f, 0f, 0.075f), 24, 1, 4f);
        return b.ToMesh("RoomDoor");
    }

    // 돌린 다리와 가죽 방석, 가죽 등받이가 있는 의자(앉는 사람이 +z를 봄)를 만든다
    private static Mesh BuildChair()
    {
        PropMeshBuilder b = new PropMeshBuilder();
        Vector2[] leg = PropFactory.P(0f, 0f, 0.016f, 0f, 0.018f, 0.02f, 0.013f, 0.12f, 0.019f, 0.2f, 0.014f, 0.3f, 0.02f, 0.4f, 0f, 0.4f);
        foreach (Vector2 s in new[] { new Vector2(1, 1), new Vector2(-1, 1) })
        {
            b.Transform = Matrix4x4.Translate(new Vector3(s.x * 0.2f, 0f, s.y * 0.19f));
            b.Lathe(leg, 16, 0, 4f);
        }
        Vector2[] post = PropFactory.P(0f, 0f, 0.02f, 0f, 0.016f, 0.15f, 0.02f, 0.42f, 0.016f, 0.8f, 0.02f, 1.02f, 0.026f, 1.05f, 0.012f, 1.08f, 0f, 1.085f);
        foreach (float sx in new[] { 1f, -1f })
        {
            b.Transform = Matrix4x4.TRS(new Vector3(sx * 0.21f, 0f, -0.2f), Quaternion.Euler(-5f, 0f, 0f), Vector3.one);
            b.Lathe(post, 16, 0, 4f);
        }
        b.Transform = Matrix4x4.identity;
        b.Box(new Vector3(0f, 0.385f, 0f), new Vector3(0.46f, 0.05f, 0.44f), 0, 4f);
        b.Cap(0.17f, 0.16f, 0.04f, 0.41f, false, 4, 1, 3f);
        b.Sweep(0.17f, 0.16f, PropFactory.P(0.04f, 0.41f, 0.05f, 0.43f, 0.05f, 0.455f, 0.04f, 0.47f, 0.02f, 0.476f), 4, 1, 3f);
        b.Cap(0.17f, 0.16f, 0.02f, 0.476f, true, 4, 1, 3f);

        b.Transform = Matrix4x4.TRS(new Vector3(0f, 0.76f, -0.235f), Quaternion.Euler(-95f, 0f, 0f), Vector3.one);
        b.Cap(0.15f, 0.21f, 0.03f, 0f, false, 4, 1, 3f);
        b.Sweep(0.15f, 0.21f, PropFactory.P(0.03f, 0f, 0.035f, 0.012f, 0.03f, 0.028f, 0.012f, 0.034f), 4, 1, 3f);
        b.Cap(0.15f, 0.21f, 0.012f, 0.034f, true, 4, 1, 3f);
        b.Transform = Matrix4x4.TRS(new Vector3(0f, 1.035f, -0.245f), Quaternion.Euler(-5f, 0f, 0f), Vector3.one);
        b.Box(Vector3.zero, new Vector3(0.44f, 0.06f, 0.04f), 0, 4f);
        for (int i = -3; i <= 3; i++)
        {
            b.Transform = Matrix4x4.TRS(new Vector3(i * 0.045f, 0.98f, -0.218f), Quaternion.Euler(-95f, 0f, 0f), Vector3.one);
            b.Lathe(PropFactory.P(0.004f, 0f, 0.004f, 0.003f, 0f, 0.0045f), 8, 2, 4f);
        }
        return b.ToMesh("RoomChair");
    }

    // 서랍 세 개와 황동 손잡이가 있는 낮은 사이드보드(앞면이 -z)를 만든다
    private static Mesh BuildSideboard()
    {
        PropMeshBuilder b = new PropMeshBuilder();
        b.Box(new Vector3(0f, 0.48f, 0f), new Vector3(1.3f, 0.74f, 0.44f), 0, 3f);
        b.Box(new Vector3(0f, 0.865f, -0.01f), new Vector3(1.38f, 0.04f, 0.5f), 0, 3f);
        b.Box(new Vector3(0f, 0.12f, -0.005f), new Vector3(1.32f, 0.03f, 0.45f), 0, 3f);
        for (int i = -1; i <= 1; i++)
        {
            b.Box(new Vector3(i * 0.42f, 0.66f, -0.225f), new Vector3(0.39f, 0.2f, 0.012f), 0, 3f);
            b.Box(new Vector3(i * 0.42f, 0.38f, -0.225f), new Vector3(0.39f, 0.3f, 0.012f), 0, 3f);
            b.Box(new Vector3(i * 0.42f, 0.66f, -0.236f), new Vector3(0.09f, 0.012f, 0.012f), 1, 3f);
            b.Box(new Vector3(i * 0.42f + 0.14f, 0.38f, -0.236f), new Vector3(0.012f, 0.06f, 0.012f), 1, 3f);
        }
        Vector2[] leg = PropFactory.P(0f, 0f, 0.022f, 0f, 0.016f, 0.05f, 0.026f, 0.11f, 0f, 0.11f);
        foreach (Vector2 s in new[] { new Vector2(1, 1), new Vector2(-1, 1), new Vector2(-1, -1), new Vector2(1, -1) })
        {
            b.Transform = Matrix4x4.Translate(new Vector3(s.x * 0.6f, 0f, s.y * 0.18f));
            b.Lathe(leg, 16, 0, 4f);
        }
        return b.ToMesh("RoomSideboard");
    }

    // 다섯 칸 책장(앞면이 -z)과 색과 크기가 제각각인 책들을 만든다
    private static Mesh BuildBookshelf()
    {
        PropMeshBuilder b = new PropMeshBuilder();
        const float w = 0.9f;
        const float h = 2.0f;
        const float d = 0.32f;
        b.Box(new Vector3(-w * 0.5f + 0.015f, h * 0.5f, 0f), new Vector3(0.03f, h, d), 0, 3f);
        b.Box(new Vector3(w * 0.5f - 0.015f, h * 0.5f, 0f), new Vector3(0.03f, h, d), 0, 3f);
        b.Box(new Vector3(0f, h * 0.5f, d * 0.5f - 0.008f), new Vector3(w, h, 0.016f), 0, 3f);
        b.Box(new Vector3(0f, h + 0.03f, -0.01f), new Vector3(w + 0.06f, 0.06f, d + 0.04f), 0, 3f);
        float[] shelves = { 0.06f, 0.46f, 0.86f, 1.26f, 1.66f };
        System.Random random = new System.Random(7);
        foreach (float y in shelves)
        {
            b.Box(new Vector3(0f, y, 0f), new Vector3(w - 0.06f, 0.025f, d - 0.02f), 0, 3f);
            float x = -w * 0.5f + 0.04f;
            while (x < w * 0.5f - 0.08f)
            {
                float bw = 0.02f + (float)random.NextDouble() * 0.025f;
                float bh = 0.22f + (float)random.NextDouble() * 0.1f;
                int color = 1 + random.Next(4);
                if (random.NextDouble() < 0.08) { x += 0.06f; continue; }
                b.Box(new Vector3(x + bw * 0.5f, y + 0.0125f + bh * 0.5f, -0.01f), new Vector3(bw, bh, 0.22f), color, 3f);
                x += bw + 0.002f;
            }
        }
        return b.ToMesh("RoomBookshelf");
    }

    // 받침이 있는 외다리 옷걸이와 황동 고리, 걸린 중절모를 만든다
    private static Mesh BuildCoatRack()
    {
        PropMeshBuilder b = new PropMeshBuilder();
        b.Lathe(PropFactory.P(0f, 0f, 0.2f, 0f, 0.2f, 0.02f, 0.06f, 0.05f, 0.025f, 0.08f, 0.018f, 0.2f, 0.016f, 1.7f, 0.022f, 1.72f, 0.03f, 1.76f, 0f, 1.8f), 24, 0, 4f);
        for (int i = 0; i < 4; i++)
        {
            b.Transform = Matrix4x4.TRS(new Vector3(0f, 1.65f, 0f), Quaternion.Euler(0f, i * 90f, 0f) * Quaternion.Euler(55f, 0f, 0f), Vector3.one);
            b.Lathe(PropFactory.P(0.006f, 0f, 0.006f, 0.12f, 0.01f, 0.13f, 0f, 0.135f), 10, 1, 4f);
        }
        b.Transform = Matrix4x4.TRS(new Vector3(0f, 1.72f, 0.09f), Quaternion.Euler(-35f, 0f, 0f), Vector3.one);
        b.Lathe(PropFactory.P(0f, -0.005f, 0.17f, -0.005f, 0.17f, 0.002f, 0.1f, 0.01f, 0.095f, 0.11f, 0.06f, 0.13f, 0f, 0.125f), 32, 2, 4f);
        return b.ToMesh("RoomCoatRack");
    }

    // 벽에 붙이는 황동 촛대형 조명(받침판, 팔, 작은 갓, 전구, 앞이 -z)을 만든다
    private static Mesh BuildSconce()
    {
        PropMeshBuilder b = new PropMeshBuilder();
        b.Transform = Matrix4x4.Rotate(Quaternion.Euler(-90f, 0f, 0f));
        b.Lathe(PropFactory.P(0f, 0f, 0.05f, 0f, 0.05f, 0.012f, 0.03f, 0.02f, 0f, 0.02f), 20, 0, 4f);
        b.Transform = Matrix4x4.identity;
        b.Box(new Vector3(0f, -0.02f, -0.08f), new Vector3(0.014f, 0.014f, 0.14f), 0, 4f);
        b.Transform = Matrix4x4.Translate(new Vector3(0f, -0.02f, -0.16f));
        b.Lathe(PropFactory.P(0f, 0f, 0.022f, 0f, 0.022f, 0.03f, 0f, 0.03f), 16, 0, 4f);
        b.Lathe(PropFactory.P(0.075f, 0.16f, 0.07f, 0.16f, 0.05f, 0.08f, 0.04f, 0.06f), 24, 1, 4f);
        b.Lathe(PropFactory.P(0.04f, 0.06f, 0.05f, 0.08f, 0.07f, 0.16f, 0.075f, 0.16f), 24, 1, 4f);
        Vector2[] bulb = new Vector2[7];
        for (int i = 0; i < bulb.Length; i++)
        {
            float a = Mathf.Lerp(-90f, 90f, i / 6f) * Mathf.Deg2Rad;
            bulb[i] = new Vector2(Mathf.Cos(a) * 0.02f, 0.075f + Mathf.Sin(a) * 0.025f);
        }
        b.Lathe(bulb, 16, 2, 1f);
        return b.ToMesh("RoomSconce");
    }

    // 메시를 저장하고 이름, 위치, y축 회전, 그림자 설정으로 방 아래에 놓는다
    private static Transform Place(Transform parent, string name, Mesh mesh, Material[] materials, Vector3 position, float yaw, ShadowCastingMode shadows)
    {
        if (!AssetDatabase.Contains(mesh)) mesh = PropFactory.SaveMesh(mesh);
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.SetLocalPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterials = materials;
        renderer.shadowCastingMode = shadows;
        return go.transform;
    }

    // 따뜻한 작은 점광원을 그림자 없이 붙인다
    private static void AddPointLight(Transform parent, Vector3 localPosition, float range, float intensity)
    {
        GameObject go = new GameObject("Light");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        Light light = go.AddComponent<Light>();
        light.type = LightType.Point;
        light.range = range;
        light.intensity = intensity;
        light.color = new Color(1f, 0.7f, 0.45f);
        light.shadows = LightShadows.None;
    }

    // 램프 스포트라이트, 창문 달빛, 어두운 주변광, 안개, 반사 프로브를 맞추고 URP 그림자 설정을 켠다
    private static void SetupLighting(Transform room)
    {
        Transform lights = new GameObject("Lights").transform;
        lights.SetParent(room, false);

        Light lamp = NewLight(lights, "LampSpot", LightType.Spot, new Vector3(0f, 1.73f, 0.05f), Quaternion.Euler(90f, 0f, 0f));
        lamp.spotAngle = 115f;
        lamp.innerSpotAngle = 55f;
        lamp.range = 3.5f;
        lamp.intensity = 2.5f;
        lamp.color = new Color(1f, 0.8f, 0.58f);
        lamp.shadows = LightShadows.Soft;
        lamp.shadowStrength = 0.9f;
        lamp.shadowNormalBias = 0.4f;
        lamp.shadowBias = 0.02f;
        lamp.shadowResolution = UnityEngine.Rendering.LightShadowResolution.High;

        Vector3 moonFrom = new Vector3(-Side - 1.4f, 3.6f, 0.5f);
        Light moon = NewLight(lights, "WindowMoon", LightType.Spot, moonFrom, Quaternion.LookRotation(new Vector3(-1.5f, 0f, 1.0f) - moonFrom));
        moon.spotAngle = 45f;
        moon.innerSpotAngle = 30f;
        moon.range = 7f;
        moon.intensity = 10f;
        moon.color = new Color(0.5f, 0.62f, 1f);
        moon.shadows = LightShadows.Soft;
        moon.shadowStrength = 1f;

        Light sun = Object.FindFirstObjectByType<Light>();
        foreach (Light l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            if (l.type == LightType.Directional) sun = l;
        if (sun != null && sun.type == LightType.Directional)
        {
            sun.intensity = 0.08f;
            sun.color = new Color(0.45f, 0.55f, 0.85f);
            sun.shadows = LightShadows.None;
        }

        RenderSettings.skybox = null;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.035f, 0.035f, 0.045f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogDensity = 0.12f;
        RenderSettings.fogColor = new Color(0.015f, 0.015f, 0.02f);

        GameObject probeGo = new GameObject("ReflectionProbe");
        probeGo.transform.SetParent(lights, false);
        probeGo.transform.localPosition = new Vector3(0f, 1.1f, 0.1f);
        ReflectionProbe probe = probeGo.AddComponent<ReflectionProbe>();
        probe.mode = ReflectionProbeMode.Realtime;
        probe.refreshMode = ReflectionProbeRefreshMode.OnAwake;
        probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.NoTimeSlicing;
        probe.size = new Vector3(Side * 2f, Height, Back - Front);
        probe.center = new Vector3(0f, Height * 0.5f - 1.1f, (Back + Front) * 0.5f - 0.1f);
        probe.resolution = 128;
        probe.clearFlags = ReflectionProbeClearFlags.SolidColor;
        probe.backgroundColor = Color.black;

        UniversalRenderPipelineAsset urp = (UniversalRenderPipelineAsset)(QualitySettings.renderPipeline != null ? QualitySettings.renderPipeline : GraphicsSettings.defaultRenderPipeline);
        SerializedObject so = new SerializedObject(urp);
        so.FindProperty("m_AdditionalLightShadowsSupported").boolValue = true;
        so.FindProperty("m_SoftShadowsSupported").boolValue = true;
        so.FindProperty("m_ShadowDistance").floatValue = 8f;
        so.FindProperty("m_AdditionalLightsPerObjectLimit").intValue = 8;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(urp);
    }

    // 이름, 종류, 위치, 방향을 정한 조명을 만든다
    private static Light NewLight(Transform parent, string name, LightType type, Vector3 position, Quaternion rotation)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.SetPositionAndRotation(position, rotation);
        Light light = go.AddComponent<Light>();
        light.type = type;
        return light;
    }

    // 톤매핑, 비네트, 블룸, 색 보정, 필름 그레인을 담은 전역 볼륨을 만들고(프로필은 다른 장면과 같이 쓰도록 에셋을 지우지 않고 내용만 다시 채운다) 카메라 후처리를 켠다
    private static void SetupPostProcessing(Transform room)
    {
        VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumePath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, VolumePath);
        }
        foreach (VolumeComponent existing in profile.components.ToArray())
        {
            profile.components.Remove(existing);
            AssetDatabase.RemoveObjectFromAsset(existing);
            Object.DestroyImmediate(existing, true);
        }

        Tonemapping tone = Add<Tonemapping>(profile);
        tone.mode.Override(TonemappingMode.ACES);
        Vignette vignette = Add<Vignette>(profile);
        vignette.intensity.Override(0.38f);
        vignette.smoothness.Override(0.45f);
        Bloom bloom = Add<Bloom>(profile);
        bloom.threshold.Override(1.1f);
        bloom.intensity.Override(0.5f);
        bloom.scatter.Override(0.6f);
        ColorAdjustments color = Add<ColorAdjustments>(profile);
        color.postExposure.Override(0.4f);
        color.contrast.Override(15f);
        color.saturation.Override(-10f);
        FilmGrain grain = Add<FilmGrain>(profile);
        grain.intensity.Override(0.22f);
        EditorUtility.SetDirty(profile);

        GameObject go = new GameObject("PostVolume");
        go.transform.SetParent(room, false);
        Volume volume = go.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.sharedProfile = profile;

        Camera camera = Camera.main;
        if (camera != null) camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
    }

    // 볼륨 프로필에 효과를 하나 더하고 프로필 에셋 안에 저장한다
    private static T Add<T>(VolumeProfile profile) where T : VolumeComponent
    {
        T component = profile.Add<T>(true);
        component.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
        AssetDatabase.AddObjectToAsset(component, profile);
        return component;
    }

    // 정수와 시드로 0~1 난수를 만든다
    private static float Hash01(int value, int seed)
    {
        unchecked
        {
            uint h = (uint)(value * 374761393 + seed * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            return ((h ^ (h >> 16)) & 0xFFFF) / 65535f;
        }
    }
}
