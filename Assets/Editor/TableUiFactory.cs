using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class TableUiFactory
{
    private const float Felt = 0.7505f;
    private const int InteractableLayer = 8;
    private static readonly Color Engraved = new Color(0.12f, 0.08f, 0.04f);
    private static readonly Color Gold = new Color(0.86f, 0.71f, 0.42f);
    private static readonly Color Cream = new Color(0.93f, 0.89f, 0.8f);
    private static readonly Color Dim = new Color(0.93f, 0.89f, 0.8f, 0.5f);

    private static TMP_FontAsset s_font;
    private static Material s_fontMaterial;

    // 베팅 패, 칩 기둥 클릭 영역, 에이스 값 표시 틀, 합계와 이름표를 테이블 위에 만들고 TableControls에 연결한 뒤 화면의 해당 패널을 끈다
    [MenuItem("Tools/DBtJ/Build Table UI")]
    public static void Build()
    {
        PropFactory.BuildMaterials();
        Transform table = GameObject.Find("Table").transform;
        TextMeshPro reference = table.Find("MyChipsLabel").GetComponent<TextMeshPro>();
        s_font = reference.font;
        s_fontMaterial = reference.fontSharedMaterial;

        Transform old = table.Find("TableUI");
        if (old != null) Object.DestroyImmediate(old.gameObject);
        Transform root = new GameObject("TableUI").transform;
        root.SetParent(table, false);

        Mesh plaque = PropFactory.SaveMesh(BuildPlaqueMesh());
        Mesh badge = PropFactory.SaveMesh(BuildBadgeMesh());
        Material brass = PropFactory.Lit("AgedBrass", new Color(0.5f, 0.38f, 0.19f), "Brass", null, 0f, 0.6f, 0.4f, 1f);

        TablePlaque call = Plaque(root, "CallPlaque", plaque, brass, new Vector3(0.12f, Felt, -0.375f), "CALL", null);
        TablePlaque raise = Plaque(root, "RaisePlaque", plaque, brass, new Vector3(0.21f, Felt, -0.375f), "RAISE", "CONFIRM");
        TablePlaque die = Plaque(root, "DiePlaque", plaque, brass, new Vector3(0.3f, Felt, -0.375f), "DIE", null);
        TablePlaque confirm = Plaque(root, "ConfirmAcesPlaque", plaque, brass, new Vector3(0.21f, Felt, -0.375f), "CONFIRM", null);
        confirm.transform.Find("Label").GetComponent<TextMeshPro>().fontSize = 0.1f;
        confirm.transform.Find("Label").GetComponent<TextMeshPro>().characterSpacing = 2f;
        TextMeshPro raiseAmount = Text(root, "RaiseAmount", "+0", new Vector3(0.21f, Felt + 0.0003f, -0.343f), 0.2f, Gold, 2f);

        ChipTableView chips = Object.FindFirstObjectByType<ChipTableView>();
        SerializedObject chipSo = new SerializedObject(chips);
        chipSo.FindProperty("_chipMaterials").GetArrayElementAtIndex(0).objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Chip5.mat");
        chipSo.ApplyModifiedPropertiesWithoutUndo();
        ChipColumnButton[] columns =
        {
            PileButton(root, "MyStackButton", chips.MyStackBase, "+1", false, 0.04f),
            PileButton(root, "MyBetButton", chips.MyBetBase, "TAKE BACK", true, -0.088f)
        };

        AceToggle ace = BuildAceTemplate(root, badge, brass);

        Mesh plate = PropFactory.SaveMesh(BuildNameplateMesh());
        Material wood = PropFactory.Mats("Wood")[0];
        Nameplate(root, "MyNameplate", plate, wood, new Vector3(0f, Felt, -0.37f), out TextMeshPro myName, out TextMeshPro myTotal);
        Nameplate(root, "OpponentNameplate", plate, wood, new Vector3(0f, Felt, 0.37f), out TextMeshPro opponentName, out TextMeshPro opponentTotal);
        root.Find("OpponentNameplate").localScale = Vector3.one * 1.4f;

        TableControls controls = root.gameObject.AddComponent<TableControls>();
        SerializedObject c = new SerializedObject(controls);
        c.FindProperty("_call").objectReferenceValue = call;
        c.FindProperty("_raise").objectReferenceValue = raise;
        c.FindProperty("_die").objectReferenceValue = die;
        SerializedProperty array = c.FindProperty("_chipColumns");
        array.arraySize = columns.Length;
        for (int d = 0; d < columns.Length; d++) array.GetArrayElementAtIndex(d).objectReferenceValue = columns[d];
        c.FindProperty("_raiseAmount").objectReferenceValue = raiseAmount;
        c.FindProperty("_chips").objectReferenceValue = chips;
        c.FindProperty("_confirmAces").objectReferenceValue = confirm;
        c.FindProperty("_aceTemplate").objectReferenceValue = ace;
        c.FindProperty("_table").objectReferenceValue = Object.FindFirstObjectByType<TableView>();
        c.FindProperty("_myTotal").objectReferenceValue = myTotal;
        c.FindProperty("_opponentTotal").objectReferenceValue = opponentTotal;
        c.FindProperty("_myName").objectReferenceValue = myName;
        c.FindProperty("_opponentName").objectReferenceValue = opponentName;
        c.ApplyModifiedPropertiesWithoutUndo();

        foreach (string path in new[] { "Canvas/DynamicHud/P1TotalPanel", "Canvas/DynamicHud/P2TotalPanel", "Canvas/P1Area/P1CharacterPanel", "Canvas/P2Area/P2CharacterPanel", "Canvas/P1Area/P1Buttons/CallButton", "Canvas/P1Area/P1Buttons/RaiseButton", "Canvas/P1Area/P1Buttons/DieButton" })
        {
            GameObject panel = GameObject.Find(path);
            if (panel != null) panel.SetActive(false);
        }

        EditorSceneManager.MarkSceneDirty(table.gameObject.scene);
        EditorSceneManager.SaveScene(table.gameObject.scene);
    }

    // 칩 더미 전체를 덮는 클릭 영역과 앞쪽 안내 글자를 만든다(되돌리기 더미면 누를 때 한 개씩 되돌린다)
    private static ChipColumnButton PileButton(Transform parent, string name, Vector3 basePoint, string hintText, bool takesBack, float hintZ)
    {
        GameObject go = new GameObject(name);
        go.layer = InteractableLayer;
        go.transform.SetParent(parent, false);
        go.transform.position = basePoint;
        BoxCollider box = go.AddComponent<BoxCollider>();
        box.center = new Vector3(0f, 0.03f, -0.0225f);
        box.size = new Vector3(0.19f, 0.06f, 0.1f);
        ChipColumnButton button = go.AddComponent<ChipColumnButton>();
        TextMeshPro hint = Text(go.transform, "Hint", hintText, new Vector3(0f, 0.0008f, hintZ), 0.12f, Gold, 6f);
        hint.gameObject.SetActive(false);
        SerializedObject so = new SerializedObject(button);
        so.FindProperty("_value").intValue = 1;
        so.FindProperty("_takesBack").boolValue = takesBack;
        so.FindProperty("_hint").objectReferenceValue = hint.gameObject;
        so.ApplyModifiedPropertiesWithoutUndo();
        return button;
    }

    // 삼각 기둥 모양 나무 명패 메시(가로 0.13, 높이 0.042, 앞뒤 면이 58도 기울어짐)를 만든다
    private static Mesh BuildNameplateMesh()
    {
        PropMeshBuilder b = new PropMeshBuilder();
        b.Transform = Matrix4x4.Rotate(Quaternion.Euler(0f, 90f, 0f));
        b.Prism(PropFactory.P(-0.025f, 0f, 0.025f, 0f, 0f, 0.042f), -0.065f, 0.065f, 0, 6f);
        return b.ToMesh("Nameplate");
    }

    // 플레이어 쪽(-z) 경사면에 이름과 합계를 새긴 명패를 놓는다
    private static void Nameplate(Transform parent, string name, Mesh mesh, Material material, Vector3 position, out TextMeshPro nameText, out TextMeshPro totalText)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = position;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = material;

        Quaternion face = Quaternion.Euler(32f, 0f, 0f);
        Vector3 up = face * Vector3.up;
        Vector3 normal = face * Vector3.back;
        Vector3 center = new Vector3(0f, 0.021f, -0.0125f) + normal * 0.0006f;
        nameText = FaceText(go.transform, "Name", "NAME", center + up * 0.009f, face, 0.12f, Gold, 8f);
        FaceText(go.transform, "TotalTitle", "TOTAL", center - up * 0.009f + Vector3.left * 0.03f, face, 0.065f, Dim, 6f);
        totalText = FaceText(go.transform, "Total", "0", center - up * 0.009f + Vector3.right * 0.016f, face, 0.15f, Cream, 2f);
    }

    // 명패 경사면에 붙는 3D 글자를 만든다
    private static TextMeshPro FaceText(Transform parent, string name, string text, Vector3 localPosition, Quaternion rotation, float size, Color color, float spacing)
    {
        TextMeshPro tmp = Text(parent, name, text, localPosition, size, color, spacing);
        tmp.transform.localRotation = rotation;
        tmp.rectTransform.sizeDelta = new Vector2(0.12f, 0.02f);
        return tmp;
    }

    // 모서리가 둥근 납작한 황동 패 메시(가로 0.068, 세로 0.026, 두께 0.0045)를 만든다
    private static Mesh BuildPlaqueMesh()
    {
        PropMeshBuilder b = new PropMeshBuilder();
        const float hx = 0.03f;
        const float hz = 0.009f;
        b.Cap(hx, hz, 0.004f, 0f, false, 4, 0, 20f);
        b.Sweep(hx, hz, PropFactory.P(0.004f, 0f, 0.004f, 0.003f, 0.0032f, 0.0042f, 0.002f, 0.0045f), 4, 0, 20f);
        b.Cap(hx, hz, 0.002f, 0.0045f, true, 4, 0, 20f);
        return b.ToMesh("Plaque");
    }

    // 에이스 값을 올려 둘 작은 황동 원판 메시를 만든다
    private static Mesh BuildBadgeMesh()
    {
        PropMeshBuilder b = new PropMeshBuilder();
        b.Lathe(PropFactory.P(0f, 0f, 0.012f, 0f, 0.012f, 0.0015f, 0.0105f, 0.0025f, 0f, 0.0025f), 32, 0, 20f);
        return b.ToMesh("AceBadge");
    }

    // 클릭할 수 있는 황동 패와 그 위에 새긴 글자(바꿔 보일 글자가 있으면 함께)를 만든다
    private static TablePlaque Plaque(Transform parent, string name, Mesh mesh, Material material, Vector3 position, string label, string altLabel)
    {
        GameObject go = new GameObject(name);
        go.layer = InteractableLayer;
        go.transform.SetParent(parent, false);
        go.transform.position = position;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = material;
        BoxCollider box = go.AddComponent<BoxCollider>();
        box.center = new Vector3(0f, 0.004f, 0f);
        box.size = new Vector3(0.07f, 0.01f, 0.028f);
        TablePlaque plaque = go.AddComponent<TablePlaque>();

        TextMeshPro main = Text(go.transform, "Label", label, new Vector3(0f, 0.0047f, 0f), 0.15f, Engraved, 6f);
        TextMeshPro alt = altLabel != null ? Text(go.transform, "AltLabel", altLabel, new Vector3(0f, 0.0047f, 0f), 0.1f, Engraved, 2f) : null;
        if (alt != null) alt.gameObject.SetActive(false);

        SerializedObject so = new SerializedObject(plaque);
        so.FindProperty("_label").objectReferenceValue = main;
        so.FindProperty("_altLabel").objectReferenceValue = alt;
        so.ApplyModifiedPropertiesWithoutUndo();
        return plaque;
    }

    // 카드 크기 클릭 영역과 카드 아래쪽 가장자리의 황동 원판 값 표시로 된 에이스 틀을 만든다
    private static AceToggle BuildAceTemplate(Transform parent, Mesh badge, Material brass)
    {
        GameObject go = new GameObject("AceToggleTemplate");
        go.layer = InteractableLayer;
        go.transform.SetParent(parent, false);
        BoxCollider box = go.AddComponent<BoxCollider>();
        box.center = new Vector3(0f, 0.003f, -0.008f);
        box.size = new Vector3(0.064f, 0.008f, 0.1f);
        AceToggle toggle = go.AddComponent<AceToggle>();

        GameObject disc = new GameObject("Badge");
        disc.layer = InteractableLayer;
        disc.transform.SetParent(go.transform, false);
        disc.transform.localPosition = new Vector3(0f, 0.0004f, -0.056f);
        disc.AddComponent<MeshFilter>().sharedMesh = badge;
        disc.AddComponent<MeshRenderer>().sharedMaterial = brass;
        TextMeshPro value = Text(disc.transform, "Value", "1", new Vector3(0f, 0.0027f, 0f), 0.15f, Engraved, 0f);

        SerializedObject so = new SerializedObject(toggle);
        so.FindProperty("_value").objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
        go.SetActive(false);
        return toggle;
    }

    // 테이블에 눕혀 플레이어 쪽에서 읽히는 3D 글자를 만든다
    private static TextMeshPro Text(Transform parent, string name, string text, Vector3 localPosition, float size, Color color, float spacing)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        TextMeshPro tmp = go.AddComponent<TextMeshPro>();
        tmp.font = s_font;
        tmp.fontSharedMaterial = s_fontMaterial;
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.characterSpacing = spacing;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.rectTransform.sizeDelta = new Vector2(0.2f, 0.04f);
        return tmp;
    }
}
