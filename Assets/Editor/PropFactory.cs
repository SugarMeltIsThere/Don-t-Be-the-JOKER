using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class PropFactory
{
    private const string MeshFolder = "Assets/Models/Procedural";
    private const string MaterialFolder = "Assets/Materials/Props";
    private const string TextureFolder = "Assets/Textures/Props";

    private static Dictionary<string, Material> s_materials;

    // 텍스처, 머티리얼, 오브젝트 메시를 모두 만들고 게임 장면과 아이템 프리팹에 붙인다
    [MenuItem("Tools/DBtJ/Build Props")]
    public static void BuildAll()
    {
        BuildTextures();
        BuildMaterials();
        BuildRevealMaterials();
        ApplyToScene();
        ApplyToItems();
        AssetDatabase.SaveAssets();
    }

    // 나뭇결, 펠트, 종이, 황동, 지폐 텍스처를 반복되게 그려 저장한다
    public static void BuildTextures()
    {
        EnsureFolder(TextureFolder);
        float[,] woodHeight = new float[512, 512];
        SaveTexture("Wood", 512, (u, v, x, y) =>
        {
            float warp = Fbm(u, v, 2, 3, 3, 1);
            float rings = 0.5f + 0.5f * Mathf.Sin((v * 14f + warp * 0.6f) * Mathf.PI * 2f);
            float streak = Fbm(u, v, 2, 96, 3, 3);
            float pores = Fbm(u, v, 8, 256, 1, 9);
            float value = Mathf.Clamp01(0.3f * rings + 0.5f * streak + 0.2f * pores);
            woodHeight[x, y] = value;
            return Color.Lerp(new Color(0.17f, 0.08f, 0.04f), new Color(0.33f, 0.17f, 0.085f), value);
        }, false);
        SaveNormal("Wood_N", woodHeight, 2.5f);

        float[,] feltHeight = new float[512, 512];
        SaveTexture("Felt", 512, (u, v, x, y) =>
        {
            float h = Fbm(u, v, 64, 64, 3, 11) * 0.6f + Fbm(u, v, 16, 16, 2, 5) * 0.4f;
            feltHeight[x, y] = h;
            return new Color(0.055f, 0.25f, 0.125f) * (0.8f + 0.4f * h);
        }, false);
        SaveNormal("Felt_N", feltHeight, 1.5f);

        SaveTexture("Paper", 256, (u, v, x, y) =>
        {
            float h = 0.9f + 0.15f * Fbm(u, v, 8, 8, 3, 21) + 0.06f * Fbm(u, v, 4, 64, 2, 22);
            return new Color(0.64f, 0.48f, 0.31f) * h;
        }, false);

        SaveTexture("Brass", 256, (u, v, x, y) =>
        {
            float h = 0.88f + 0.14f * Fbm(u, v, 4, 4, 3, 31) + 0.05f * Fbm(u, v, 2, 64, 2, 32);
            return new Color(0.8f, 0.62f, 0.3f) * h;
        }, false);

        SaveTexture("Banknote", 256, (u, v, x, y) =>
        {
            bool border = u < 0.07f || u > 0.93f || v < 0.12f || v > 0.88f;
            float dx = (u - 0.5f) / 0.2f;
            float dy = (v - 0.5f) / 0.3f;
            bool oval = dx * dx + dy * dy < 1f;
            Color baseColor = border ? new Color(0.27f, 0.42f, 0.3f) : oval ? new Color(0.4f, 0.55f, 0.42f) : new Color(0.58f, 0.68f, 0.52f);
            return baseColor * (0.92f + 0.12f * Fbm(u, v, 16, 16, 2, 41));
        }, true);
    }

    // 오브젝트에 쓰는 URP Lit 머티리얼을 만들거나 갱신한다
    public static void BuildMaterials()
    {
        EnsureFolder(MaterialFolder);
        s_materials = new Dictionary<string, Material>();
        Lit("Wood", Color.white, "Wood", "Wood_N", 0.7f, 0f, 0.42f, 4f);
        Lit("Felt", Color.white, "Felt", "Felt_N", 0.5f, 0f, 0.05f, 1f);
        Lit("Brass", Color.white, "Brass", null, 0f, 0.85f, 0.62f, 1f);
        Lit("Steel", new Color(0.56f, 0.57f, 0.6f), null, null, 0f, 0.85f, 0.55f, 1f);
        Lit("Rubber", new Color(0.05f, 0.05f, 0.05f), null, null, 0f, 0f, 0.25f, 1f);
        Lit("Paper", Color.white, "Paper", null, 0f, 0f, 0.15f, 1f);
        Lit("CardBack", new Color(0.45f, 0.06f, 0.07f), null, null, 0f, 0f, 0.5f, 1f);
        Lit("CardFace", new Color(0.92f, 0.9f, 0.85f), null, null, 0f, 0f, 0.4f, 1f);
        Lit("Enamel", new Color(0.07f, 0.28f, 0.17f), null, null, 0f, 0f, 0.65f, 1f);
        Lit("EnamelInner", new Color(0.9f, 0.87f, 0.78f), null, null, 0f, 0f, 0.4f, 1f);
        Lit("Ash", new Color(0.36f, 0.35f, 0.33f), null, null, 0f, 0f, 0.1f, 1f);
        Lit("Cigar", new Color(0.3f, 0.17f, 0.09f), null, null, 0f, 0f, 0.25f, 1f);
        Lit("Wax", new Color(0.55f, 0.05f, 0.05f), null, null, 0f, 0f, 0.55f, 1f);
        Lit("Banknote", Color.white, "Banknote", null, 0f, 0f, 0.2f, 1f);
        Lit("PlasticBlack", new Color(0.025f, 0.025f, 0.03f), null, null, 0f, 0f, 0.8f, 1f);
        Lit("CardStack", new Color(0.7f, 0.68f, 0.63f), null, null, 0f, 0f, 0.3f, 1f);
        Lit("Anodized", new Color(0.09f, 0.095f, 0.11f), null, null, 0f, 0.6f, 0.6f, 1f);
        Lit("Chrome", new Color(0.78f, 0.79f, 0.81f), null, null, 0f, 1f, 0.88f, 1f);
        Lit("Acrylic", new Color(0.04f, 0.04f, 0.055f, 0.85f), null, null, 0f, 0f, 0.7f, 1f).SetTransparent();
        Lit("Glass", new Color(0.85f, 0.92f, 0.95f, 0.22f), null, null, 0f, 0f, 0.95f, 1f).SetTransparent();
        Lit("Liquid", new Color(0.62f, 0.28f, 0.05f, 0.8f), null, null, 0f, 0f, 0.9f, 1f).SetTransparent();
        Lit("Bulb", new Color(1f, 0.85f, 0.6f), null, null, 0f, 0f, 0.5f, 1f).SetEmission(new Color(1f, 0.82f, 0.55f) * 3f);
        Lit("Lens", new Color(0.8f, 0.85f, 0.9f), null, null, 0f, 0f, 0.95f, 1f).SetEmission(new Color(1f, 0.95f, 0.8f) * 0.4f);
    }

    // 테이블, 카드 슈, 벨, 소품 메시를 만들어 게임 장면의 Visual 자리에 붙인다
    public static void ApplyToScene()
    {
        if (s_materials == null) BuildMaterials();
        Transform table = GameObject.Find("Table").transform;
        SetVisual(table.Find("TableModel"), SaveMesh(BuildTable()), Mats("Wood", "Felt"));
        SetVisual(VisualOf(table.Find("Bell")), SaveMesh(BuildBell()), Mats("Wood", "Brass"));

        Transform deck = table.Find("Deck");
        SetVisual(VisualOf(deck), SaveMesh(BuildCardShoe()), Mats("PlasticBlack", "Acrylic", "CardStack"));
        FitCollider(deck);
        Transform shoeCard = PlaceShoeCard(deck);
        table.Find("DeckTop").SetPositionAndRotation(shoeCard.position, Quaternion.identity);
        SetupMagnifierLens(shoeCard);

        Transform props = table.Find("Props");
        SetVisual(VisualOf(props.Find("PendantLamp")), SaveMesh(BuildLamp()), Mats("Enamel", "EnamelInner", "Brass", "Bulb"));
        SetVisual(VisualOf(props.Find("WhiskeySet")), SaveMesh(BuildWhiskey()), Mats("Glass", "Liquid", "Paper", "Wood"));
        SetVisual(VisualOf(props.Find("Ashtray")), SaveMesh(BuildAshtray()), Mats("Brass", "Ash", "Cigar"));

        EditorSceneManager.MarkSceneDirty(table.gameObject.scene);
        EditorSceneManager.SaveScene(table.gameObject.scene);
    }

    // 돋보기, 손전등, 뇌물 봉투 메시를 아이템 프리팹의 Visual로 바꾼다
    public static void ApplyToItems()
    {
        if (s_materials == null) BuildMaterials();
        ReplaceItemVisual("MagnifierItem", SaveMesh(BuildMagnifier()), Mats("Brass", "Glass", "Wood"));
        ReplaceItemVisual("FlashItem", SaveMesh(BuildFlashlight()), Mats("Anodized", "Chrome", "Lens"));
        ReplaceItemVisual("BribeItem", SaveMesh(BuildEnvelope()), Mats("Paper", "Wax", "Banknote"));
    }

    // 펠트 윗면이 0.749에 오는 카드 테이블(나무 테두리, 앞치마, 돌린 다리)을 만든다
    private static Mesh BuildTable()
    {
        PropMeshBuilder b = new PropMeshBuilder();
        const float cx = 0.54f;
        const float cz = 0.39f;
        b.Cap(cx, cz, 0.04f, 0.749f, true, 8, 1, 3f);
        b.Sweep(cx, cz, P(0.11f, 0.72f, 0.13f, 0.72f, 0.13f, 0.758f, 0.126f, 0.772f, 0.114f, 0.782f, 0.095f, 0.786f, 0.07f, 0.786f, 0.054f, 0.781f, 0.044f, 0.771f, 0.04f, 0.758f, 0.04f, 0.749f), 8, 0, 4f);
        b.Sweep(cx, cz, P(0.09f, 0.6f, 0.112f, 0.6f, 0.112f, 0.61f, 0.106f, 0.616f, 0.106f, 0.7f, 0.11f, 0.705f, 0.11f, 0.72f), 8, 0, 4f);
        Vector2[] leg = P(0f, 0f, 0.028f, 0f, 0.03f, 0.012f, 0.022f, 0.04f, 0.017f, 0.25f, 0.022f, 0.36f, 0.03f, 0.42f, 0.03f, 0.45f, 0.022f, 0.48f, 0.034f, 0.52f, 0.036f, 0.6f, 0f, 0.6f);
        foreach (Vector2 s in new[] { new Vector2(1, 1), new Vector2(-1, 1), new Vector2(-1, -1), new Vector2(1, -1) })
        {
            b.Transform = Matrix4x4.Translate(new Vector3(s.x * 0.6f, 0f, s.y * 0.44f));
            b.Lathe(leg, 24, 0, 4f);
        }
        return b.ToMesh("Table");
    }

    // 앞쪽(-x)이 비스듬한 현대식 카드 슈(검은 플라스틱 받침과 덮개, 연기색 아크릴 몸통, 안에 쌓인 카드)를 만든다
    private static Mesh BuildCardShoe()
    {
        PropMeshBuilder b = new PropMeshBuilder();
        b.Cap(0.09f, 0.038f, 0.01f, 0f, false, 4, 0, 6f);
        b.Sweep(0.09f, 0.038f, P(0.01f, 0f, 0.011f, 0.002f, 0.011f, 0.009f, 0.01f, 0.011f), 4, 0, 6f);
        b.Cap(0.09f, 0.038f, 0.01f, 0.011f, true, 4, 0, 6f);
        b.Prism(P(-0.094f, 0.011f, 0.098f, 0.011f, 0.098f, 0.09f, -0.016f, 0.09f, -0.094f, 0.031f), -0.044f, 0.044f, 1, 6f);
        b.Prism(P(-0.07f, 0.012f, 0.094f, 0.012f, 0.094f, 0.082f, -0.002f, 0.082f, -0.07f, 0.031f), -0.0315f, 0.0315f, 2, 6f);
        b.Box(new Vector3(0.041f, 0.0915f, 0f), new Vector3(0.116f, 0.003f, 0.092f), 0, 6f);
        b.Box(new Vector3(-0.097f, 0.021f, 0f), new Vector3(0.006f, 0.02f, 0.092f), 0, 6f);
        return b.ToMesh("CardShoe");
    }

    // 슈 비스듬한 면 위에 맨 위 카드가 놓일 기준점(카드 윗면이 비탈 바깥, 긴 변이 z)을 만들거나 옮긴다
    private static Transform PlaceShoeCard(Transform deck)
    {
        Transform anchor = deck.Find("ShoeCard");
        if (anchor == null)
        {
            anchor = new GameObject("ShoeCard").transform;
            anchor.SetParent(deck, false);
        }
        Vector2 mid = new Vector2(-0.055f, 0.0605f);
        Vector2 normal = new Vector2(-0.6f, 0.8f);
        anchor.localPosition = new Vector3(mid.x + normal.x * 0.0015f, mid.y + normal.y * 0.0015f, 0f);
        anchor.localRotation = Quaternion.Euler(0f, 0f, 36.87f);
        anchor.localScale = Vector3.one;
        return anchor;
    }

    // 돋보기 연출용 렌즈를 아이템과 같은 돋보기 모양과 스텐실 마스크로 바꾸고 MagnifierView에 연결한다
    private static void SetupMagnifierLens(Transform shoeCard)
    {
        MagnifierView view = Object.FindFirstObjectByType<MagnifierView>(FindObjectsInactive.Include);
        SerializedObject so = new SerializedObject(view);
        Transform lens = (Transform)so.FindProperty("_lens").objectReferenceValue;

        List<GameObject> children = new List<GameObject>();
        foreach (Transform child in lens) children.Add(child.gameObject);
        foreach (GameObject child in children) Object.DestroyImmediate(child);
        lens.localScale = Vector3.one;
        SetVisual(VisualOf(lens), SaveMesh(BuildMagnifier()), Mats("Brass", "Glass", "Wood"));

        GameObject mask = new GameObject("LensMask");
        mask.transform.SetParent(lens, false);
        PropMeshBuilder b = new PropMeshBuilder();
        b.Transform = Matrix4x4.Translate(new Vector3(0f, 0f, 0.025f));
        b.Lathe(P(0f, 0.0034f, 0.0262f, 0.0034f), 48, 0, 1f);
        SetVisual(mask.transform, SaveMesh(b.ToMesh("LensMask")), Mats("StencilMask"));
        mask.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        so.FindProperty("_shoeCard").objectReferenceValue = shoeCard;
        so.FindProperty("_revealMaterial").objectReferenceValue = s_materials["CardReveal"];
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // 렌즈 스텐실 마스크와 렌즈 안에서만 보이는 카드 앞면 머티리얼을 만든다
    public static void BuildRevealMaterials()
    {
        if (s_materials == null) BuildMaterials();
        Material mask = ShaderMaterial("StencilMask", "DBtJ/StencilMask");
        mask.SetFloat("_ColorMask", 0f);
        s_materials["StencilMask"] = mask;
        Material reveal = ShaderMaterial("CardReveal", "DBtJ/CardReveal");
        reveal.SetFloat("_StencilComp", (float)UnityEngine.Rendering.CompareFunction.Equal);
        Material card = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Card.mat");
        reveal.SetTexture("_BaseMap", card.GetTexture("_BaseMap"));
        reveal.SetColor("_BaseColor", new Color(0.72f, 0.7f, 0.66f));
        s_materials["CardReveal"] = reveal;
    }

    // 지정한 셰이더를 쓰는 머티리얼을 만들거나 가져온다
    private static Material ShaderMaterial(string name, string shader)
    {
        string path = $"{MaterialFolder}/{name}.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find(shader));
            AssetDatabase.CreateAsset(material, path);
        }
        material.shader = Shader.Find(shader);
        EditorUtility.SetDirty(material);
        return material;
    }

    // 나무 받침 위에 황동 돔과 누름쇠가 있는 데스크 벨을 만든다
    private static Mesh BuildBell()
    {
        PropMeshBuilder b = new PropMeshBuilder();
        b.Lathe(P(0f, 0f, 0.044f, 0f, 0.047f, 0.004f, 0.047f, 0.016f, 0.044f, 0.021f, 0.037f, 0.024f, 0f, 0.024f), 40, 0, 6f);
        b.Lathe(P(0.035f, 0.024f, 0.035f, 0.028f, 0.033f, 0.036f, 0.029f, 0.046f, 0.022f, 0.054f, 0.013f, 0.059f, 0.005f, 0.061f, 0f, 0.0615f), 40, 1, 8f);
        b.Lathe(P(0.0035f, 0.06f, 0.0035f, 0.071f, 0.0075f, 0.073f, 0.0075f, 0.077f, 0.005f, 0.08f, 0f, 0.081f), 24, 1, 8f);
        return b.ToMesh("Bell");
    }

    // 초록 법랑 갓과 황동 소켓, 전구, 매다는 막대가 있는 펜던트 램프를 만든다
    private static Mesh BuildLamp()
    {
        PropMeshBuilder b = new PropMeshBuilder();
        b.Lathe(P(0.195f, 0f, 0.201f, 0f, 0.2f, 0.006f, 0.17f, 0.04f, 0.11f, 0.11f, 0.06f, 0.15f, 0.045f, 0.16f), 48, 0, 3f);
        b.Lathe(P(0f, 0.15f, 0.04f, 0.15f, 0.055f, 0.144f, 0.105f, 0.104f, 0.165f, 0.037f, 0.195f, 0f), 48, 1, 3f);
        b.Lathe(P(0.045f, 0.16f, 0.03f, 0.172f, 0.03f, 0.2f, 0.014f, 0.21f, 0.008f, 0.215f, 0.008f, 0.8f, 0f, 0.8f), 24, 2, 4f);
        Vector2[] bulb = new Vector2[9];
        for (int i = 0; i < bulb.Length; i++)
        {
            float a = Mathf.Lerp(-90f, 90f, i / 8f) * Mathf.Deg2Rad;
            bulb[i] = new Vector2(Mathf.Cos(a) * 0.028f, 0.122f + Mathf.Sin(a) * 0.028f);
        }
        b.Lathe(bulb, 24, 3, 1f);
        return b.ToMesh("PendantLamp");
    }

    // 호박색 위스키가 든 유리병과 텀블러 두 잔을 만든다
    private static Mesh BuildWhiskey()
    {
        PropMeshBuilder b = new PropMeshBuilder();
        b.Lathe(P(0f, 0f, 0.031f, 0f, 0.034f, 0.004f, 0.034f, 0.14f, 0.031f, 0.155f, 0.015f, 0.174f, 0.011f, 0.185f, 0.011f, 0.214f, 0.013f, 0.217f), 40, 0, 4f);
        b.Lathe(P(0f, 0.004f, 0.0315f, 0.004f, 0.0315f, 0.095f, 0f, 0.095f), 40, 1, 4f);
        b.Lathe(P(0.0345f, 0.05f, 0.0345f, 0.112f), 40, 2, 6f);
        b.Lathe(P(0.0105f, 0.205f, 0.0105f, 0.232f, 0.009f, 0.236f, 0f, 0.237f), 24, 3, 6f);

        foreach (Vector3 at in new[] { new Vector3(0.075f, 0f, -0.025f), new Vector3(0.05f, 0f, 0.06f) })
        {
            b.Transform = Matrix4x4.Translate(at);
            b.Lathe(P(0f, 0f, 0.029f, 0f, 0.031f, 0.003f, 0.034f, 0.085f, 0.0312f, 0.085f, 0.0285f, 0.013f, 0f, 0.013f), 40, 0, 4f);
            b.Lathe(P(0f, 0.013f, 0.0283f, 0.013f, 0.0292f, 0.036f, 0f, 0.036f), 40, 1, 4f);
        }
        return b.ToMesh("WhiskeySet");
    }

    // 오목한 황동 재떨이와 재, 가장자리에 걸친 시가를 만든다
    private static Mesh BuildAshtray()
    {
        PropMeshBuilder b = new PropMeshBuilder();
        b.Lathe(P(0f, 0f, 0.067f, 0f, 0.07f, 0.004f, 0.07f, 0.024f, 0.066f, 0.03f, 0.058f, 0.03f, 0.054f, 0.026f, 0.049f, 0.012f, 0.044f, 0.008f, 0f, 0.008f), 48, 0, 6f);
        b.Lathe(P(0.043f, 0.0095f, 0f, 0.0095f), 32, 1, 6f);
        b.Transform = Matrix4x4.TRS(new Vector3(-0.035f, 0.022f, 0.012f), Quaternion.Euler(0f, 20f, -98f), Vector3.one);
        b.Lathe(P(0f, 0f, 0.006f, 0f, 0.0085f, 0.004f, 0.0085f, 0.112f, 0.007f, 0.118f, 0f, 0.12f), 20, 2, 8f);
        b.Lathe(P(0f, -0.012f, 0.0078f, -0.012f, 0.008f, 0.001f), 20, 1, 8f);
        b.Lathe(P(0.0088f, 0.084f, 0.0088f, 0.098f), 20, 0, 8f);
        return b.ToMesh("Ashtray");
    }

    // 납작하게 놓인 돋보기(황동 테, 유리, 나무 손잡이가 플레이어 쪽)를 만든다
    private static Mesh BuildMagnifier()
    {
        PropMeshBuilder b = new PropMeshBuilder();
        Vector2[] ring = new Vector2[17];
        for (int i = 0; i < ring.Length; i++)
        {
            float a = Mathf.Lerp(-90f, 270f, i / 16f) * Mathf.Deg2Rad;
            ring[i] = new Vector2(0.026f + Mathf.Cos(a) * 0.0035f, 0.0035f + Mathf.Sin(a) * 0.0035f);
        }
        b.Transform = Matrix4x4.Translate(new Vector3(0f, 0f, 0.025f));
        b.Lathe(ring, 48, 0, 8f);
        b.Lathe(P(0f, 0.0025f, 0.0262f, 0.0025f, 0.0262f, 0.0045f, 0f, 0.0045f), 48, 1, 8f);
        b.Transform = Matrix4x4.TRS(new Vector3(0f, 0.0055f, -0.004f), Quaternion.Euler(-90f, 0f, 0f), Vector3.one);
        b.Lathe(P(0f, 0f, 0.0042f, 0f, 0.0042f, 0.008f, 0.0052f, 0.0095f, 0.0052f, 0.012f, 0f, 0.012f), 20, 0, 8f);
        b.Lathe(P(0.0046f, 0.012f, 0.0055f, 0.02f, 0.0055f, 0.044f, 0.0046f, 0.049f, 0f, 0.0505f), 20, 2, 8f);
        return b.ToMesh("Magnifier");
    }

    // z 방향으로 누운 매끈한 손전등(검은 아노다이징 몸통, 크롬 베젤과 띠, 렌즈)을 만든다
    private static Mesh BuildFlashlight()
    {
        PropMeshBuilder b = new PropMeshBuilder();
        b.Transform = Matrix4x4.TRS(new Vector3(0f, 0.016f, -0.045f), Quaternion.Euler(90f, 0f, 0f), Vector3.one);
        b.Lathe(P(0f, 0f, 0.005f, 0f, 0.0085f, 0.0008f, 0.0104f, 0.0025f, 0.0111f, 0.0055f, 0.0112f, 0.009f, 0.0109f, 0.016f, 0.0108f, 0.05f, 0.0111f, 0.0545f, 0.0125f, 0.06f, 0.0144f, 0.066f, 0.0157f, 0.071f, 0.016f, 0.075f, 0.016f, 0.0855f), 48, 0, 8f);
        b.Lathe(P(0.016f, 0.0855f, 0.0164f, 0.086f, 0.0165f, 0.0885f, 0.0158f, 0.0895f, 0.0146f, 0.0895f), 48, 1, 8f);
        b.Lathe(P(0.0146f, 0.0892f, 0f, 0.0892f), 48, 2, 8f);
        b.Lathe(P(0.0108f, 0.03f, 0.011f, 0.0305f, 0.011f, 0.0335f, 0.0108f, 0.034f), 48, 1, 8f);
        return b.ToMesh("Flashlight");
    }

    // 붉은 밀랍 봉인과 비어져 나온 지폐가 있는 납작한 종이 봉투를 만든다
    private static Mesh BuildEnvelope()
    {
        PropMeshBuilder b = new PropMeshBuilder();
        const float hx = 0.031f;
        const float hz = 0.021f;
        b.Cap(hx, hz, 0.002f, 0f, false, 3, 0, 10f);
        b.Sweep(hx, hz, P(0.002f, 0f, 0.003f, 0.002f, 0.003f, 0.006f, 0.002f, 0.008f), 3, 0, 10f);
        b.Cap(hx, hz, 0.002f, 0.008f, true, 3, 0, 10f);
        b.Transform = Matrix4x4.Rotate(Quaternion.Euler(90f, 0f, 0f));
        b.Prism(P(-0.032f, 0.0225f, 0.032f, 0.0225f, 0f, -0.002f), -0.0087f, -0.008f, 0, 10f);
        b.Transform = Matrix4x4.Translate(new Vector3(0f, 0f, 0.003f));
        b.Lathe(P(0.006f, 0.0086f, 0.006f, 0.0098f, 0.004f, 0.0103f, 0f, 0.0104f), 20, 1, 10f);
        b.Transform = Matrix4x4.TRS(new Vector3(0.034f, 0.0042f, -0.003f), Quaternion.Euler(0f, 8f, 0f), Vector3.one);
        b.Box(Vector3.zero, new Vector3(0.026f, 0.0012f, 0.034f), 2, 1f);
        b.Transform = Matrix4x4.TRS(new Vector3(0.031f, 0.0058f, 0.004f), Quaternion.Euler(0f, -6f, 0f), Vector3.one);
        b.Box(Vector3.zero, new Vector3(0.022f, 0.0012f, 0.034f), 2, 1f);
        return b.ToMesh("BribeEnvelope");
    }

    // 아이템 프리팹의 자식을 지우고 새 Visual과 콜라이더를 맞춘 뒤 저장한다
    private static void ReplaceItemVisual(string prefabName, Mesh mesh, Material[] materials)
    {
        string path = $"Assets/Prefabs/Items/{prefabName}.prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        List<GameObject> children = new List<GameObject>();
        foreach (Transform child in root.transform) children.Add(child.gameObject);
        foreach (GameObject child in children) Object.DestroyImmediate(child);
        SetVisual(VisualOf(root.transform), mesh, materials);
        FitCollider(root.transform);
        PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);
    }

    // 부모 아래 Visual 자식을 찾고 없으면 만든다
    private static Transform VisualOf(Transform parent)
    {
        Transform visual = parent.Find("Visual");
        if (visual != null) return visual;
        GameObject go = new GameObject("Visual");
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    // 변환을 초기화하고 메시와 머티리얼을 붙인다
    private static void SetVisual(Transform visual, Mesh mesh, Material[] materials)
    {
        visual.localPosition = Vector3.zero;
        visual.localRotation = Quaternion.identity;
        visual.localScale = Vector3.one;
        visual.gameObject.layer = visual.parent.gameObject.layer;
        MeshFilter filter = visual.GetComponent<MeshFilter>();
        if (filter == null) filter = visual.gameObject.AddComponent<MeshFilter>();
        MeshRenderer renderer = visual.GetComponent<MeshRenderer>();
        if (renderer == null) renderer = visual.gameObject.AddComponent<MeshRenderer>();
        filter.sharedMesh = mesh;
        renderer.sharedMaterials = materials;
    }

    // 자식 렌더러 경계에 맞춰 상자 콜라이더를 다시 잡는다
    private static void FitCollider(Transform root)
    {
        BoxCollider box = root.GetComponent<BoxCollider>();
        if (box == null) return;
        Bounds bounds = new Bounds();
        bool first = true;
        foreach (Renderer r in root.GetComponentsInChildren<Renderer>())
        {
            if (first) { bounds = r.bounds; first = false; }
            else bounds.Encapsulate(r.bounds);
        }
        box.center = root.InverseTransformPoint(bounds.center);
        Vector3 size = root.InverseTransformVector(bounds.size);
        box.size = new Vector3(Mathf.Max(Mathf.Abs(size.x), 0.03f), Mathf.Max(Mathf.Abs(size.y), 0.02f), Mathf.Max(Mathf.Abs(size.z), 0.03f));
    }

    // 메시를 같은 경로의 기존 에셋에 덮어써 참조를 유지하며 저장한다
    internal static Mesh SaveMesh(Mesh mesh)
    {
        EnsureFolder(MeshFolder);
        string path = $"{MeshFolder}/{mesh.name}.asset";
        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing == null)
        {
            AssetDatabase.CreateAsset(mesh, path);
            existing = mesh;
        }
        else
        {
            existing.Clear();
            EditorUtility.CopySerialized(mesh, existing);
        }
        MeshUtility.SetMeshCompression(existing, ModelImporterMeshCompression.Off);
        SerializedObject so = new SerializedObject(existing);
        so.FindProperty("m_IsReadable").boolValue = false;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(existing);
        return existing;
    }

    // 이름 목록에 해당하는 머티리얼 배열을 만든다
    internal static Material[] Mats(params string[] names)
    {
        Material[] result = new Material[names.Length];
        for (int i = 0; i < names.Length; i++) result[i] = s_materials[names[i]];
        return result;
    }

    // URP Lit 머티리얼을 만들거나 같은 경로의 머티리얼 값을 갱신한다
    internal static Material Lit(string name, Color color, string baseMap, string normalMap, float normalScale, float metallic, float smoothness, float tiling)
    {
        string path = $"{MaterialFolder}/{name}.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(material, path);
        }
        material.SetColor("_BaseColor", color);
        material.SetTexture("_BaseMap", baseMap == null ? null : AssetDatabase.LoadAssetAtPath<Texture2D>($"{TextureFolder}/{baseMap}.png"));
        material.SetTextureScale("_BaseMap", Vector2.one * tiling);
        if (normalMap != null)
        {
            material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{TextureFolder}/{normalMap}.png"));
            material.SetFloat("_BumpScale", normalScale);
            material.EnableKeyword("_NORMALMAP");
        }
        material.SetFloat("_Metallic", metallic);
        material.SetFloat("_Smoothness", smoothness);
        material.enableInstancing = true;
        EditorUtility.SetDirty(material);
        s_materials[name] = material;
        return material;
    }

    // 머티리얼을 알파 섞기 투명으로 바꾼다
    internal static Material SetTransparent(this Material material)
    {
        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", 0f);
        material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_ZWrite", 0f);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        material.SetOverrideTag("RenderType", "Transparent");
        return material;
    }

    // 머티리얼에 발광 색을 켠다
    internal static Material SetEmission(this Material material, Color emission)
    {
        material.SetColor("_EmissionColor", emission);
        material.EnableKeyword("_EMISSION");
        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        return material;
    }

    // 픽셀마다 색을 계산해 PNG로 저장하고 반복, 밉맵, 압축으로 들여온다
    internal static void SaveTexture(string name, int size, System.Func<float, float, int, int, Color> pixel, bool clamp)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGB24, false);
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                texture.SetPixel(x, y, pixel((float)x / size, (float)y / size, x, y));
        WritePng(texture, name, false, clamp);
    }

    // 높이 배열에서 반복되는 노멀 맵을 만들어 저장한다
    internal static void SaveNormal(string name, float[,] height, float strength)
    {
        int size = height.GetLength(0);
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGB24, false);
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (height[(x + 1) % size, y] - height[(x - 1 + size) % size, y]) * strength;
                float dy = (height[x, (y + 1) % size] - height[x, (y - 1 + size) % size]) * strength;
                Vector3 n = new Vector3(-dx, -dy, 1f).normalized;
                texture.SetPixel(x, y, new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f));
            }
        WritePng(texture, name, true, false);
    }

    // 텍스처를 PNG로 쓰고 들여오기 설정을 맞춘다
    private static void WritePng(Texture2D texture, string name, bool normal, bool clamp)
    {
        texture.Apply();
        string path = $"{TextureFolder}/{name}.png";
        File.WriteAllBytes(path, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path);
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
        importer.wrapMode = clamp ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
        importer.mipmapEnabled = true;
        importer.anisoLevel = 4;
        importer.filterMode = FilterMode.Trilinear;
        importer.textureCompression = TextureImporterCompression.Compressed;
        importer.SaveAndReimport();
    }

    // 주기가 정수인 값 노이즈를 여러 옥타브 더해 0~1 값을 돌려준다
    internal static float Fbm(float u, float v, int periodX, int periodY, int octaves, int seed)
    {
        float sum = 0f;
        float amplitude = 0.5f;
        float total = 0f;
        for (int o = 0; o < octaves; o++)
        {
            int px = periodX << o;
            int py = periodY << o;
            sum += Noise(u * px, v * py, px, py, seed + o * 17) * amplitude;
            total += amplitude;
            amplitude *= 0.5f;
        }
        return sum / total;
    }

    // 격자 주기로 감싸는 부드러운 값 노이즈를 구한다
    private static float Noise(float x, float y, int px, int py, int seed)
    {
        int x0 = Mathf.FloorToInt(x);
        int y0 = Mathf.FloorToInt(y);
        float fx = x - x0;
        float fy = y - y0;
        fx = fx * fx * (3f - 2f * fx);
        fy = fy * fy * (3f - 2f * fy);
        float a = Hash(Wrap(x0, px), Wrap(y0, py), seed);
        float b = Hash(Wrap(x0 + 1, px), Wrap(y0, py), seed);
        float c = Hash(Wrap(x0, px), Wrap(y0 + 1, py), seed);
        float d = Hash(Wrap(x0 + 1, px), Wrap(y0 + 1, py), seed);
        return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
    }

    // 음수도 주기 안으로 감싼다
    private static int Wrap(int value, int period)
    {
        int r = value % period;
        return r < 0 ? r + period : r;
    }

    // 정수 좌표와 시드로 0~1 난수를 만든다
    private static float Hash(int x, int y, int seed)
    {
        unchecked
        {
            uint h = (uint)(x * 374761393 + y * 668265263 + seed * 1442695041);
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;
            return (h & 0xFFFFFF) / 16777215f;
        }
    }

    // 숫자 쌍 목록을 단면 점 배열로 바꾼다
    internal static Vector2[] P(params float[] values)
    {
        Vector2[] points = new Vector2[values.Length / 2];
        for (int i = 0; i < points.Length; i++) points[i] = new Vector2(values[i * 2], values[i * 2 + 1]);
        return points;
    }

    // 폴더가 없으면 상위부터 만든다
    internal static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
