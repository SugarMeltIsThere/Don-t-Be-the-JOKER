using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class UiStyler
{
    private const string Folder = "Assets/Textures/UI";
    private static readonly Color Gold = new Color(0.86f, 0.71f, 0.42f);
    private static readonly Color Cream = new Color(0.93f, 0.89f, 0.8f);
    private static readonly Color Dim = new Color(0.93f, 0.89f, 0.8f, 0.55f);

    private static readonly Dictionary<string, string> ButtonLabels = new Dictionary<string, string>
    {
        { "BTN_HIT", "HIT" }, { "BTN_STAND", "STAND" }, { "BTN_CALL", "CALL" }, { "BTN_RAISE_CANCEL", "CANCEL" },
        { "BTN_RAISE_CONFIRM", "CONFIRM" }, { "BTN_RAISE", "RAISE" }, { "BTN_DIE", "DIE" }, { "BTN_ACE_CONFIRM", "CONFIRM" },
        { "BTN_STEP_P10", "+10" }, { "BTN_STEP_P1", "+1" }, { "BTN_STEP_P5", "+5" },
        { "BTN_STEP_M10", "-10" }, { "BTN_STEP_M1", "-1" }, { "BTN_STEP_M5", "-5" },
        { "BTN_CREATE_ROOM_MAIN", "CREATE ROOM" }, { "BTN_JOIN_BY_CODE", "JOIN BY CODE" }, { "BTN_MODAL_CREATE", "CREATE" },
        { "BTN_MODAL_CANCEL", "CANCEL" }, { "BTN_READY", "READY" }, { "BTN_START", "START" }, { "BTN_LEAVE_ROOM", "LEAVE ROOM" },
        { "BTN_CHAR_QUEEN", "QUEEN" }, { "BTN_CHAR_JACK", "JACK" }, { "BTN_CHAR_JOKER", "JOKER" }, { "BTN_CHAR_KING", "KING" },
        { "BTN_ROW_JOIN", "JOIN" }
    };

    private static readonly Dictionary<string, string> HeaderTitles = new Dictionary<string, string>
    {
        { "PANEL_PLAYERS", "PLAYERS" }, { "PANEL_CHARACTER", "SELECT CHARACTER" }, { "PANEL_ROOM_LIST", "ROOM LIST" },
        { "PANEL_RESULT", "RESULT" }, { "MODAL_CREATE_ROOM", "CREATE ROOM" }
    };

    private static readonly Dictionary<string, string> CornerTitles = new Dictionary<string, string>
    {
        { "PANEL_NICKNAME", "NICKNAME" }, { "PANEL_CODE", "ROOM CODE" }, { "ROW_PLAYER_host", "HOST" }, { "ROW_PLAYER_guest", "GUEST" }
    };

    private static readonly Dictionary<string, string> PlateTitles = new Dictionary<string, string>
    {
        { "PLATE_TOTAL", "TOTAL" }, { "PLATE_MY_CHAR", "ME" }, { "PLATE_OPP_CHAR", "OPPONENT" }, { "PLATE_MY_CHIPS", "MY CHIPS" },
        { "PLATE_OPP_CHIPS", "OPPONENT CHIPS" }, { "PLATE_MY_BET", "MY BET" }, { "PLATE_OPP_BET", "OPPONENT BET" },
        { "PLATE_POT", "POT" }, { "PLATE_RAISE_AMOUNT", "RAISE" }, { "PLATE_MY_CHARACTER", "MY CHARACTER" }
    };

    private static readonly Dictionary<string, string> StaticTexts = new Dictionary<string, string>
    {
        { "Select Ace", "SELECT ACE" }, { "Opponent is selecting an Item...", "OPPONENT IS SELECTING AN ITEM..." }
    };

    // 열려 있는 장면의 캔버스 이미지와 글자를 누아르 판(어두운 바탕, 얇은 금테)과 금색 글자로 바꾼다
    [MenuItem("Tools/DBtJ/Style UI")]
    public static void StyleOpenScene()
    {
        Sprite plate = BuildFrameSprite("UI_Plate", new Color(0.05f, 0.045f, 0.04f, 0.86f));
        Sprite button = BuildFrameSprite("UI_Button", new Color(0.09f, 0.075f, 0.06f, 0.95f));
        TMP_FontAsset font = FindFont();

        foreach (Canvas canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            StyleTree(canvas.transform, plate, button, font);

        foreach (Image image in Object.FindObjectsByType<Image>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (image.sprite == null || image.sprite.name != "Background") continue;
            if (image.name == "LobbyPanel" || image.name == "WaitingPanel") image.color = new Color(0.035f, 0.03f, 0.03f, 1f);
            else if (image.name == "ConnectingPanel") image.color = new Color(0f, 0f, 0f, 0.6f);
            EditorUtility.SetDirty(image);
        }

        foreach (ScrollRect scroll in Object.FindObjectsByType<ScrollRect>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            Transform title = scroll.transform.Find("Title");
            if (title != null) title.SetParent(scroll.transform.parent, true);
        }

        foreach (string path in new[] { "Assets/Prefabs/RoomItem.prefab", "Assets/Prefabs/AceSlot.prefab" })
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            StyleTree(root.transform, plate, button, font);
            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
        }

        EditorSceneManager.MarkAllScenesDirty();
        EditorSceneManager.SaveOpenScenes();
    }

    // 한 오브젝트 아래 모든 글자와 이미지를 누아르 판과 금색 글자로 바꾼다
    private static void StyleTree(Transform root, Sprite plate, Sprite button, TMP_FontAsset font)
    {
        foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
        {
            if (StaticTexts.TryGetValue(text.text, out string upper)) text.text = upper;
            if (text.transform.parent != null && text.transform.parent.GetComponent<Button>() != null) continue;
            if (text.name == "Title" || text.name == "Label") continue;
            text.color = Cream;
            text.characterSpacing = 4f;
            EditorUtility.SetDirty(text);
        }

        foreach (Image image in root.GetComponentsInChildren<Image>(true))
        {
            if (image.sprite == null) continue;
            string name = image.sprite.name;
            if (name.StartsWith("BTN_")) StyleButton(image, button, LabelFor(name, ButtonLabels), font);
            else if (name.StartsWith("PLATE_")) StylePlate(image, plate, LabelFor(name, PlateTitles), font, TitleStyle.Small);
            else if (LabelFor(name, HeaderTitles) != null) StylePlate(image, plate, LabelFor(name, HeaderTitles), font, TitleStyle.Header);
            else if (LabelFor(name, CornerTitles) != null) StylePlate(image, plate, LabelFor(name, CornerTitles), font, TitleStyle.Corner);
            else if (name.StartsWith("PANEL_") || name.StartsWith("FIELD_") || name.StartsWith("ROW_") || name == "InputFieldBackground") SetSliced(image, plate);
        }
    }

    // 게임에서 쓰는 TMP 글꼴(Arimo-Bold SDF)을 찾는다
    private static TMP_FontAsset FindFont()
    {
        foreach (string guid in AssetDatabase.FindAssets("Arimo-Bold SDF t:TMP_FontAsset"))
            return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guid));
        return null;
    }

    // 스프라이트 이름의 앞부분이 가장 길게 맞는 표시 글자를 찾는다
    private static string LabelFor(string spriteName, Dictionary<string, string> table)
    {
        string best = null;
        int bestLength = 0;
        foreach (KeyValuePair<string, string> pair in table)
            if (spriteName.StartsWith(pair.Key) && pair.Key.Length > bestLength)
            {
                best = pair.Value;
                bestLength = pair.Key.Length;
            }
        return best;
    }

    // 버튼을 금테 판과 색 변화 전환으로 바꾸고 판에 구워져 있던 글자를 장면 글자로 붙인다
    private static void StyleButton(Image image, Sprite sprite, string label, TMP_FontAsset font)
    {
        SetSliced(image, sprite);
        Button button = image.GetComponent<Button>();
        if (button != null)
        {
            button.transition = Selectable.Transition.ColorTint;
            button.spriteState = new SpriteState();
            ColorBlock colors = button.colors;
            colors.normalColor = new Color(0.85f, 0.85f, 0.85f);
            colors.highlightedColor = Color.white;
            colors.pressedColor = new Color(0.6f, 0.56f, 0.5f);
            colors.selectedColor = new Color(0.85f, 0.85f, 0.85f);
            colors.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.5f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            EditorUtility.SetDirty(button);
        }
        TMP_Text existing = null;
        foreach (TMP_Text child in image.GetComponentsInChildren<TMP_Text>(true))
            if (child.name != "Label" && !string.IsNullOrWhiteSpace(child.text)) existing = child;
        if (existing != null)
        {
            existing.color = Gold;
            existing.characterSpacing = 6f;
            EditorUtility.SetDirty(existing);
            return;
        }
        if (label == null) return;

        TMP_Text text = EnsureLabel(image.transform, "Label", font);
        RectTransform rect = text.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        text.text = label;
        text.fontSize = Mathf.Round(((RectTransform)image.transform).rect.height * 0.36f);
        text.enableAutoSizing = true;
        text.fontSizeMax = text.fontSize;
        text.fontSizeMin = 12f;
        text.margin = new Vector4(12f, 0f, 12f, 0f);
        text.color = Gold;
        text.characterSpacing = 6f;
        text.alignment = TextAlignmentOptions.Center;
    }

    private enum TitleStyle { Small, Header, Corner }

    // 판을 금테 판으로 바꾸고 판에 구워져 있던 제목을 모양(작게 위 가운데, 크게 머리글, 왼쪽 위 모서리)에 맞춰 장면 글자로 붙인다
    private static void StylePlate(Image image, Sprite sprite, string title, TMP_FontAsset font, TitleStyle style)
    {
        SetSliced(image, sprite);
        if (title == null) return;

        TMP_Text text = EnsureLabel(image.transform, "Title", font);
        RectTransform rect = text.rectTransform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        text.text = title;
        switch (style)
        {
            case TitleStyle.Header:
                rect.pivot = new Vector2(0.5f, 1f);
                rect.anchoredPosition = new Vector2(0f, -26f);
                rect.sizeDelta = new Vector2(0f, 40f);
                text.fontSize = 26f;
                text.color = Gold;
                text.characterSpacing = 12f;
                text.alignment = TextAlignmentOptions.Center;
                break;
            case TitleStyle.Corner:
                rect.pivot = new Vector2(0f, 1f);
                rect.anchoredPosition = new Vector2(28f, -26f);
                rect.sizeDelta = new Vector2(-56f, 28f);
                text.fontSize = 18f;
                text.color = Gold;
                text.characterSpacing = 10f;
                text.alignment = TextAlignmentOptions.Left;
                break;
            default:
                rect.pivot = new Vector2(0.5f, 1f);
                rect.anchoredPosition = new Vector2(0f, -8f);
                rect.sizeDelta = new Vector2(0f, 24f);
                text.fontSize = 16f;
                text.color = Dim;
                text.characterSpacing = 10f;
                text.alignment = TextAlignmentOptions.Center;
                break;
        }
    }

    // 이미지를 9분할 판 스프라이트와 흰색으로 바꾼다
    private static void SetSliced(Image image, Sprite sprite)
    {
        image.sprite = sprite;
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = 1f;
        image.color = Color.white;
        EditorUtility.SetDirty(image);
    }

    // 자식 글자를 찾거나 만들어 돌려준다(클릭을 막지 않게 한다)
    private static TMP_Text EnsureLabel(Transform parent, string name, TMP_FontAsset font)
    {
        Transform existing = parent.Find(name);
        TextMeshProUGUI text = existing != null ? existing.GetComponent<TextMeshProUGUI>() : null;
        if (text == null)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            text = go.AddComponent<TextMeshProUGUI>();
        }
        if (font != null) text.font = font;
        text.raycastTarget = false;
        EditorUtility.SetDirty(text);
        return text;
    }

    // 둥근 모서리, 바깥 금테, 안쪽 옅은 금선이 있는 9분할 판 스프라이트를 그려 저장한다
    private static Sprite BuildFrameSprite(string name, Color fill)
    {
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Textures", "UI");
        const int w = 96;
        const int h = 96;
        const float radius = 10f;
        Texture2D texture = new Texture2D(w, h, TextureFormat.RGBA32, false);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float d = RoundedDistance(x + 0.5f, y + 0.5f, w, h, radius);
                float coverage = Mathf.Clamp01(0.5f - d);
                Color c = fill;
                float border = Mathf.Clamp01(1.5f - Mathf.Abs(d + 1.5f));
                c = Color.Lerp(c, Gold, border * 0.95f);
                float inner = Mathf.Clamp01(1f - Mathf.Abs(d + 6f));
                c = Color.Lerp(c, Gold, inner * 0.3f);
                c.a = Mathf.Max(fill.a, border) * coverage;
                texture.SetPixel(x, y, c);
            }
        texture.Apply();
        string path = $"{Folder}/{name}.png";
        File.WriteAllBytes(path, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path);

        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spriteBorder = new Vector4(18f, 18f, 18f, 18f);
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    // 둥근 사각형 가장자리까지의 부호 거리(안쪽이 음수)를 구한다
    private static float RoundedDistance(float x, float y, float w, float h, float r)
    {
        float qx = Mathf.Abs(x - w * 0.5f) - (w * 0.5f - r);
        float qy = Mathf.Abs(y - h * 0.5f) - (h * 0.5f - r);
        float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
        return outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
    }
}
