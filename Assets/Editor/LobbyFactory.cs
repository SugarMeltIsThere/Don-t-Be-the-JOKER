using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

public static class LobbyFactory
{
    private const string ProceduralFolder = "Assets/Models/Procedural";
    private const float Felt = 0.7505f;

    // 로비 장면에 게임과 같은 방과 테이블(카드와 칩을 늘어놓은 장식)을 깔고, 카메라가 테이블 둘레를 돌며 UI 뒤로 방이 보이게 한다
    [MenuItem("Tools/DBtJ/Build Lobby Backdrop")]
    public static void Build()
    {
        PropFactory.BuildMaterials();
        GameObject old = GameObject.Find("Table");
        if (old != null) Object.DestroyImmediate(old);
        Transform table = new GameObject("Table").transform;

        Add(table, "TableModel", Mesh("Table"), PropFactory.Mats("Wood", "Felt"), Vector3.zero, 0f);
        Transform bell = Child(table, "Bell", new Vector3(-0.13f, Felt, -0.34f), 0f);
        Transform opponentBell = Child(table, "OpponentBell", new Vector3(0.15f, Felt, 0.335f), 180f);
        foreach (Transform t in new[] { bell, opponentBell })
        {
            t.localScale = Vector3.one * 0.7f;
            Add(t, "Visual", Mesh("Bell"), PropFactory.Mats("Wood", "Brass"), Vector3.zero, 0f);
        }
        Transform deck = Child(table, "Deck", new Vector3(0.34f, Felt, 0.02f), 0f);
        Add(deck, "Visual", Mesh("CardShoe"), PropFactory.Mats("CardEdges"), Vector3.zero, 0f);
        Transform props = Child(table, "Props", Vector3.zero, 0f);
        Add(Child(props, "PendantLamp", new Vector3(0f, 1.62f, 0.05f), 0f), "Visual", Mesh("PendantLamp"), PropFactory.Mats("Enamel", "EnamelInner", "Brass", "Bulb"), Vector3.zero, 0f);

        PlaceCards(table, deck);
        PlaceChips(table);

        RoomFactory.Build();
        SetupCameraAndUi();
        foreach (MeshRenderer r in table.GetComponentsInChildren<MeshRenderer>())
            GameObjectUtility.SetStaticEditorFlags(r.gameObject, StaticEditorFlags.BatchingStatic);
        EditorSceneManager.MarkSceneDirty(table.gameObject.scene);
        EditorSceneManager.SaveScene(table.gameObject.scene);
    }

    // 덱 위 뒷면 한 장, 내 쪽 펼친 손패, 상대 쪽 엎은 두 장, 가운데 한 장을 늘어놓는다
    private static void PlaceCards(Transform table, Transform deck)
    {
        CardSpriteDatabase db = AssetDatabase.LoadAssetAtPath<CardSpriteDatabase>("Assets/CardSpriteDatabase.asset");
        Material card = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Card.mat");
        Sprite back = db.CardBackSprite;
        Transform cards = Child(table, "Cards", Vector3.zero, 0f);

        Add(cards, "DeckTopCard", CardMesh("LobbyCardBack", back), new[] { card }, deck.position + Vector3.up * 0.0243f, 1.5f);
        Add(cards, "Shared", CardMesh("LobbyCardShared", db.GetSprite(new Card(Rank.Ace, Suit.Spade))), new[] { card }, new Vector3(0f, Felt + 0.0003f, 0f), 4f);
        Card[] hand = { new Card(Rank.King, Suit.Heart), new Card(Rank.Seven, Suit.Diamond), new Card(Rank.Ten, Suit.Clover) };
        for (int i = 0; i < hand.Length; i++)
            Add(cards, "Hand" + i, CardMesh("LobbyCardHand" + i, db.GetSprite(hand[i])), new[] { card }, new Vector3(-0.07f + i * 0.07f, Felt + 0.0003f + i * 0.0002f, -0.24f), (i - 1) * 6f);
        for (int i = 0; i < 2; i++)
            Add(cards, "Opponent" + i, CardMesh("LobbyCardBack", back), new[] { card }, new Vector3(-0.04f + i * 0.075f, Felt + 0.0003f, 0.24f), 180f + (i * 2 - 1) * 4f);
    }

    // 양쪽 칩 더미와 가운데 판돈 칩을 쌓아 둔다
    private static void PlaceChips(Transform table)
    {
        Mesh chip = Mesh("Chip");
        Material[] mats =
        {
            AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Chip10.mat"),
            AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Chip5.mat"),
            AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Chip1.mat")
        };
        Transform chips = Child(table, "Chips", Vector3.zero, 0f);
        Vector3[] piles = { new Vector3(0.22f, 0f, -0.27f), new Vector3(0.27f, 0f, -0.27f), new Vector3(0.245f, 0f, -0.225f), new Vector3(-0.27f, 0f, 0.34f), new Vector3(-0.22f, 0f, 0.34f), new Vector3(-0.14f, 0f, 0f), new Vector3(-0.1f, 0f, 0.02f) };
        int[] counts = { 7, 5, 3, 6, 4, 4, 2 };
        int[] denom = { 0, 1, 2, 0, 2, 1, 0 };
        System.Random random = new System.Random(3);
        for (int p = 0; p < piles.Length; p++)
            for (int i = 0; i < counts[p]; i++)
            {
                Vector3 jitter = new Vector3((float)(random.NextDouble() - 0.5) * 0.002f, 0f, (float)(random.NextDouble() - 0.5) * 0.002f);
                Transform t = Add(chips, "Chip", chip, new[] { mats[denom[p]] }, piles[p] + jitter + Vector3.up * (Felt + 0.00175f + i * 0.0035f), (float)random.NextDouble() * 360f);
                t.localScale = new Vector3(0.04f, 0.00175f, 0.04f);
            }
    }

    // 로비 카메라에 테이블 둘레 회전과 후처리를 붙이고, UI 배경판을 비워 방이 보이게 한다
    private static void SetupCameraAndUi()
    {
        Camera camera = Camera.main;
        if (camera != null)
        {
            if (camera.GetComponent<LobbyCameraOrbit>() == null) camera.gameObject.AddComponent<LobbyCameraOrbit>();
            camera.orthographic = false;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.fieldOfView = 45f;
            camera.nearClipPlane = 0.05f;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            EditorUtility.SetDirty(camera);
        }

        foreach (Image image in Object.FindObjectsByType<Image>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (image.name != "LobbyPanel" && image.name != "WaitingPanel" && image.name != "ConnectingPanel") continue;
            image.color = new Color(0f, 0f, 0f, image.name == "ConnectingPanel" ? 0.5f : 0.25f);
            EditorUtility.SetDirty(image);
        }
    }

    // 스프라이트 영역을 앞면 UV로 쓰는 카드 한 장 메시(가로 0.063, 세로 0.084, 위를 봄)를 만들어 저장한다
    private static Mesh CardMesh(string name, Sprite sprite)
    {
        Rect r = sprite.textureRect;
        Texture t = sprite.texture;
        Vector2 min = new Vector2(r.xMin / t.width, r.yMin / t.height);
        Vector2 max = new Vector2(r.xMax / t.width, r.yMax / t.height);
        const float w = 0.0315f;
        const float h = 0.042f;
        Mesh mesh = new Mesh { name = name };
        mesh.vertices = new[] { new Vector3(-w, 0f, -h), new Vector3(w, 0f, -h), new Vector3(-w, 0f, h), new Vector3(w, 0f, h) };
        mesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
        mesh.uv = new[] { min, new Vector2(max.x, min.y), new Vector2(min.x, max.y), max };
        mesh.triangles = new[] { 0, 2, 3, 0, 3, 1 };
        mesh.RecalculateBounds();
        return PropFactory.SaveMesh(mesh);
    }

    // 생성된 메시 에셋을 이름으로 불러온다
    private static Mesh Mesh(string name) => AssetDatabase.LoadAssetAtPath<Mesh>($"{ProceduralFolder}/{name}.asset");

    // 빈 자식 오브젝트를 위치와 y축 회전으로 만든다
    private static Transform Child(Transform parent, string name, Vector3 localPosition, float yaw)
    {
        Transform t = new GameObject(name).transform;
        t.SetParent(parent, false);
        t.SetLocalPositionAndRotation(localPosition, Quaternion.Euler(0f, yaw, 0f));
        return t;
    }

    // 메시와 머티리얼을 단 자식을 위치와 y축 회전으로 만든다
    private static Transform Add(Transform parent, string name, Mesh mesh, Material[] materials, Vector3 localPosition, float yaw)
    {
        Transform t = Child(parent, name, localPosition, yaw);
        t.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer renderer = t.gameObject.AddComponent<MeshRenderer>();
        renderer.sharedMaterials = materials;
        return t;
    }
}
