using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class UiRedesign
{
    private const string SpriteFolder = "Assets/Textures/UI";
    private static readonly Color Cream = new Color(0.93f, 0.89f, 0.8f);
    private static readonly Color Gold = new Color(0.86f, 0.71f, 0.42f);
    private static readonly Color Dim = new Color(0.93f, 0.89f, 0.8f, 0.55f);

    private static TMP_FontAsset s_serif;
    private static TMP_FontAsset s_sans;

    // 인게임 HUD를 미니멀하게 바꾼다: 단계 글자와 알림은 세리프에 얇은 선, 시간 고리는 가늘게, 커서 옆 클릭 안내와 바닥 한 줄 안내, 아이템 버리기 창은 어두운 막 위 글자 버튼
    [MenuItem("Tools/DBtJ/Redesign Game HUD")]
    public static void BuildGameHud()
    {
        LoadFonts();
        Transform canvas = GameObject.Find("Canvas").transform;
        Transform hud = canvas.Find("DynamicHud");

        TMP_Text phase = hud.Find("PhaseText").GetComponent<TMP_Text>();
        Style(phase, s_serif, 34f, Cream, 18f);
        ((RectTransform)phase.transform).anchoredPosition = new Vector2(0f, -36f);
        Hairline(phase.transform, "LineLeft", new Vector2(-330f, 0f));
        Hairline(phase.transform, "LineRight", new Vector2(330f, 0f));
        AddFade(phase.gameObject, phase);

        TMP_Text notice = hud.Find("ItemNoticeText").GetComponent<TMP_Text>();
        Style(notice, s_serif, 40f, Cream, 6f);
        notice.fontStyle = FontStyles.Italic;
        ((RectTransform)notice.transform).anchoredPosition = new Vector2(0f, -150f);
        AddFade(notice.gameObject, notice);

        RestyleTimer(hud.Find("TurnTimer"));
        InteractionPrompt prompt = BuildPrompt(hud);
        BuildPhaseHint(hud, prompt);
        RestyleDiscard(canvas);
        AssignPrompts();
        RestyleTableText();

        EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
        EditorSceneManager.SaveScene(canvas.gameObject.scene);
    }

    // 글꼴 에셋을 불러온다
    private static void LoadFonts()
    {
        s_serif = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Cormorant/Cormorant SDF.asset");
        s_sans = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Inter/Inter SDF.asset");
    }

    // 글꼴, 크기, 색, 자간을 한 번에 정한다
    private static void Style(TMP_Text text, TMP_FontAsset font, float size, Color color, float spacing)
    {
        text.font = font;
        text.fontSharedMaterial = font.material;
        text.fontSize = size;
        text.color = color;
        text.characterSpacing = spacing;
        text.fontStyle = FontStyles.Normal;
        EditorUtility.SetDirty(text);
    }

    // 글자 양옆에 놓을 가는 금색 선을 만든다
    private static void Hairline(Transform parent, string name, Vector2 position)
    {
        Transform existing = parent.Find(name);
        Image line = existing != null ? existing.GetComponent<Image>() : NewImage(parent, name);
        line.color = new Color(Gold.r, Gold.g, Gold.b, 0.45f);
        line.raycastTarget = false;
        RectTransform rect = line.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(140f, 1f);
    }

    // 글자가 바뀔 때 떠오르며 나타나게 하는 컴포넌트를 붙인다
    private static void AddFade(GameObject go, TMP_Text text)
    {
        CanvasGroup group = go.GetComponent<CanvasGroup>();
        if (group == null) group = go.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        TextFadeIn fade = go.GetComponent<TextFadeIn>();
        if (fade == null) fade = go.AddComponent<TextFadeIn>();
        SerializedObject so = new SerializedObject(fade);
        so.FindProperty("_text").objectReferenceValue = text;
        so.FindProperty("_group").objectReferenceValue = group;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // 시간 고리를 가늘고 작게, 숫자는 본문 글꼴로, 이름표는 없앤다
    private static void RestyleTimer(Transform timer)
    {
        Sprite ring = RingSprite("UI_RingThin", 5f);
        RectTransform root = (RectTransform)timer;
        root.anchoredPosition = new Vector2(-48f, -48f);
        root.sizeDelta = new Vector2(92f, 92f);
        Transform display = timer.Find("Display");
        foreach (string name in new[] { "Track", "Ring" })
        {
            Image image = display.Find(name).GetComponent<Image>();
            image.sprite = ring;
            image.rectTransform.sizeDelta = new Vector2(84f, 84f);
            EditorUtility.SetDirty(image);
        }
        display.Find("Track").GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.08f);
        TMP_Text seconds = display.Find("Seconds").GetComponent<TMP_Text>();
        Style(seconds, s_sans, 24f, Cream, 0f);
        ((RectTransform)seconds.transform).anchoredPosition = new Vector2(0f, -42f);
        display.Find("Label").gameObject.SetActive(false);
    }

    // 커서 옆에 뜨는 반투명 알약 모양 클릭 안내(왼쪽 클릭 줄, 오른쪽 클릭 줄)를 만든다
    private static InteractionPrompt BuildPrompt(Transform hud)
    {
        Transform old = hud.Find("InteractionPrompt");
        if (old != null) Object.DestroyImmediate(old.gameObject);

        Sprite pill = FrameSprite("UI_Pill", new Color(0.02f, 0.018f, 0.016f, 0.72f), new Color(0, 0, 0, 0), 12f);
        Sprite key = FrameSprite("UI_KeyCap", new Color(0, 0, 0, 0), new Color(Gold.r, Gold.g, Gold.b, 0.9f), 5f);

        GameObject go = new GameObject("InteractionPrompt", typeof(RectTransform));
        go.transform.SetParent(hud, false);
        RectTransform root = (RectTransform)go.transform;
        root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
        root.pivot = new Vector2(0f, 1f);
        root.sizeDelta = new Vector2(260f, 76f);
        CanvasGroup group = go.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;
        Image background = go.AddComponent<Image>();
        background.sprite = pill;
        background.type = Image.Type.Sliced;
        background.raycastTarget = false;

        TMP_Text primary = PromptRow(go.transform, "Primary", "LMB", key, -20f);
        TMP_Text alt = PromptRow(go.transform, "Alt", "RMB", key, -52f);

        InteractionPrompt prompt = go.AddComponent<InteractionPrompt>();
        SerializedObject so = new SerializedObject(prompt);
        so.FindProperty("_input").objectReferenceValue = Object.FindFirstObjectByType<TableInput>();
        so.FindProperty("_root").objectReferenceValue = root;
        so.FindProperty("_group").objectReferenceValue = group;
        so.FindProperty("_primary").objectReferenceValue = primary;
        so.FindProperty("_altRow").objectReferenceValue = alt.transform.parent.gameObject;
        so.FindProperty("_alt").objectReferenceValue = alt;
        so.ApplyModifiedPropertiesWithoutUndo();
        return prompt;
    }

    // 키 모양 테두리 안 마우스 버튼 이름과 그 옆 행동 이름으로 된 한 줄을 만들고 행동 글자를 돌려준다
    private static TMP_Text PromptRow(Transform parent, string name, string keyName, Sprite keySprite, float y)
    {
        GameObject row = new GameObject(name, typeof(RectTransform));
        row.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)row.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = new Vector2(14f, y);
        rect.sizeDelta = new Vector2(232f, 26f);

        Image cap = NewImage(row.transform, "Key");
        cap.sprite = keySprite;
        cap.type = Image.Type.Sliced;
        cap.raycastTarget = false;
        cap.rectTransform.anchorMin = cap.rectTransform.anchorMax = new Vector2(0f, 0.5f);
        cap.rectTransform.pivot = new Vector2(0f, 0.5f);
        cap.rectTransform.anchoredPosition = Vector2.zero;
        cap.rectTransform.sizeDelta = new Vector2(44f, 24f);
        TMP_Text keyText = NewText(cap.transform, "Name", keyName, s_sans, 11f, Gold, 2f);
        Stretch(keyText.rectTransform);

        TMP_Text action = NewText(row.transform, "Action", "ACTION", s_sans, 15f, Cream, 4f);
        action.alignment = TextAlignmentOptions.MidlineLeft;
        RectTransform a = action.rectTransform;
        a.anchorMin = new Vector2(0f, 0f);
        a.anchorMax = new Vector2(1f, 1f);
        a.offsetMin = new Vector2(56f, 0f);
        a.offsetMax = Vector2.zero;
        return action;
    }

    // 화면 아래 가운데에 단계별 안내 한 줄(드로우, 베팅, 에이스)을 만든다
    private static void BuildPhaseHint(Transform hud, InteractionPrompt prompt)
    {
        Transform old = hud.Find("PhaseHint");
        if (old != null) Object.DestroyImmediate(old.gameObject);
        GameObject go = new GameObject("PhaseHint", typeof(RectTransform));
        go.transform.SetParent(hud, false);
        RectTransform root = (RectTransform)go.transform;
        root.anchorMin = root.anchorMax = new Vector2(0.5f, 1f);
        root.pivot = new Vector2(0.5f, 1f);
        root.anchoredPosition = new Vector2(0f, -88f);
        root.sizeDelta = new Vector2(1200f, 30f);
        CanvasGroup group = go.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;

        GameObject draw = HintLine(go.transform, "DrawHint", "CLICK THE DECK TO DRAW  ·  RING THE BELL TO STAND");
        GameObject bet = HintLine(go.transform, "BetHint", "CALL  ·  RAISE  ·  DIE");
        GameObject ace = HintLine(go.transform, "AceHint", "CLICK YOUR ACES TO CHOOSE 1 OR 11");

        PhaseHint hint = go.AddComponent<PhaseHint>();
        SerializedObject so = new SerializedObject(hint);
        so.FindProperty("_group").objectReferenceValue = group;
        so.FindProperty("_drawHint").objectReferenceValue = draw;
        so.FindProperty("_betHint").objectReferenceValue = bet;
        so.FindProperty("_aceHint").objectReferenceValue = ace;
        so.FindProperty("_prompt").objectReferenceValue = prompt;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // 안내 한 줄 글자를 만든다
    private static GameObject HintLine(Transform parent, string name, string text)
    {
        TMP_Text line = NewText(parent, name, text, s_sans, 15f, Cream, 8f);
        Stretch(line.rectTransform);
        return line.gameObject;
    }

    // 아이템 버리기 창과 상대 대기 창을 화면 전체 어두운 막, 세리프 제목, 밑줄 글자 버튼으로 바꾼다
    private static void RestyleDiscard(Transform canvas)
    {
        foreach (string name in new[] { "ItemDiscardPanel", "OpponentDiscardWaitingPanel" })
        {
            Transform panel = canvas.Find(name);
            if (panel == null) continue;
            RectTransform rect = (RectTransform)panel;
            Image image = panel.GetComponent<Image>();
            image.sprite = null;
            image.color = new Color(0.015f, 0.012f, 0.01f, 0.86f);
            EditorUtility.SetDirty(image);
            if (name == "ItemDiscardPanel")
            {
                Stretch(rect);
                Center(panel.Find("Title"), 150f);
                Center(panel.Find("CurrentLabel"), 72f);
                Center(panel.Find("NewLabel"), -48f);
            }

            foreach (TMP_Text text in panel.GetComponentsInChildren<TMP_Text>(true))
            {
                if (text.transform.parent.GetComponent<Button>() != null) continue;
                bool title = text.name == "Title" || text.name == "OpponentDiscardWaitingText";
                Style(text, title ? s_serif : s_sans, title ? 44f : 13f, title ? Cream : Dim, title ? 10f : 10f);
            }

            foreach (Button button in panel.GetComponentsInChildren<Button>(true)) TextButton(button);
        }
    }

    // 버튼을 테두리 없는 글자 버튼으로 바꾸고, 가는 밑줄이 마우스를 올리면 진해지게 한다
    private static void TextButton(Button button)
    {
        Image hit = button.GetComponent<Image>();
        hit.sprite = null;
        hit.color = new Color(0, 0, 0, 0);
        EditorUtility.SetDirty(hit);

        Image underline = FindDeep(button.transform, "Underline");
        if (underline == null) underline = NewImage(button.transform, "Underline");
        underline.color = Gold;
        underline.raycastTarget = false;
        RectTransform u = underline.rectTransform;
        u.anchorMin = new Vector2(0.2f, 0f);
        u.anchorMax = new Vector2(0.8f, 0f);
        u.pivot = new Vector2(0.5f, 0f);
        u.anchoredPosition = new Vector2(0f, 8f);
        u.sizeDelta = new Vector2(0f, 1.5f);

        button.transition = Selectable.Transition.ColorTint;
        button.targetGraphic = underline;
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(1f, 1f, 1f, 0.2f);
        colors.highlightedColor = Color.white;
        colors.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
        colors.selectedColor = new Color(1f, 1f, 1f, 0.2f);
        colors.disabledColor = new Color(1f, 1f, 1f, 0.05f);
        colors.fadeDuration = 0.12f;
        button.colors = colors;
        EditorUtility.SetDirty(button);

        TMP_Text main = null;
        foreach (TMP_Text label in button.GetComponentsInChildren<TMP_Text>(true))
        {
            Style(label, s_sans, 18f, Cream, 8f);
            label.enableAutoSizing = true;
            label.fontSizeMax = 18f;
            label.fontSizeMin = 11f;
            if (main == null || !string.IsNullOrEmpty(label.text)) main = label;
        }
        FitUnderline(button, main, false);
    }

    // 밑줄 길이를 글자 폭에 맞추는 컴포넌트를 붙이거나 설정한다
    private static void FitUnderline(Button button, TMP_Text label, bool alignLeft)
    {
        UnderlineFit fit = button.GetComponent<UnderlineFit>();
        if (fit == null) fit = button.gameObject.AddComponent<UnderlineFit>();
        SerializedObject so = new SerializedObject(fit);
        so.FindProperty("_label").objectReferenceValue = label;
        so.FindProperty("_underline").objectReferenceValue = FindDeep(button.transform, "Underline").rectTransform;
        so.FindProperty("_alignLeft").boolValue = alignLeft;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // 테이블 위 3D 글자를 새 글꼴로 바꾼다(칩 수와 합계는 본문 글꼴, 이름과 패 글자는 세리프 굵게)
    private static void RestyleTableText()
    {
        Transform table = GameObject.Find("Table").transform;
        foreach (TextMeshPro text in table.GetComponentsInChildren<TextMeshPro>(true))
        {
            string n = text.name;
            bool serif = n == "Name" || n == "Label" || n == "AltLabel" || n == "CancelLabel";
            TMP_FontAsset font = serif ? s_serif : s_sans;
            text.font = font;
            text.fontSharedMaterial = font.material;
            if (serif) text.fontStyle = FontStyles.Bold;
            if (n.EndsWith("ChipsLabel") || n.EndsWith("BetLabel") || n == "PotLabel")
            {
                text.color = new Color(Cream.r, Cream.g, Cream.b, 0.8f);
                text.fontSize = 0.3f;
            }
            if (n == "Label") text.fontSize = text.transform.parent.name == "ConfirmAcesPlaque" ? 0.115f : 0.17f;
            if (n == "AltLabel" || n == "CancelLabel") text.fontSize = 0.115f;
            EditorUtility.SetDirty(text);
        }
    }

    // 장면의 상호작용 물건과 아이템 프리팹에 커서 옆 안내 문구를 적는다
    private static void AssignPrompts()
    {
        SetPrompt(Object.FindFirstObjectByType<DeckInteractable>(FindObjectsInactive.Include), "DRAW A CARD", null);
        foreach (BellInteractable bell in Object.FindObjectsByType<BellInteractable>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            SetPrompt(bell, "STAND", null);
        Transform ui = GameObject.Find("Table/TableUI").transform;
        SetPrompt(ui.Find("CallPlaque").GetComponent<Interactable>(), "CALL", null);
        TablePlaque raise = ui.Find("RaisePlaque").GetComponent<TablePlaque>();
        SetPrompt(raise, "RAISE", null);
        SerializedObject r = new SerializedObject(raise);
        r.FindProperty("_altStatePrompt").stringValue = "CONFIRM RAISE";
        r.FindProperty("_cancelStatePrompt").stringValue = "CANCEL RAISE";
        r.FindProperty("_cancelLabel").objectReferenceValue = EnsureCancelLabel(raise.transform, "CANCEL");
        r.ApplyModifiedPropertiesWithoutUndo();
        SetPrompt(ui.Find("DiePlaque").GetComponent<Interactable>(), "DIE", null);
        SetPrompt(ui.Find("ConfirmAcesPlaque").GetComponent<Interactable>(), "CONFIRM ACES", null);
        SetPrompt(ui.Find("MyStackButton").GetComponent<Interactable>(), "ADD A CHIP  (HOLD)", "TAKE ONE BACK");
        SetPrompt(ui.Find("MyBetButton").GetComponent<Interactable>(), "TAKE ONE BACK  (HOLD)", "TAKE ONE BACK");
        SetPrompt(ui.Find("AceToggleTemplate").GetComponent<Interactable>(), "SWITCH 1 / 11", null);

        string[] prefabs = { "MagnifierItem", "FlashItem", "BribeItem" };
        string[] prompts = { "USE MAGNIFIER", "USE FLASH", "USE DEAL" };
        for (int i = 0; i < prefabs.Length; i++)
        {
            string path = $"Assets/Prefabs/Items/{prefabs[i]}.prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            SetPrompt(root.GetComponent<Interactable>(), prompts[i], null);
            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // 패에 취소 글자가 없으면 바꿔 보일 글자를 복제해 만들고 돌려준다
    private static TextMeshPro EnsureCancelLabel(Transform plaque, string text)
    {
        Transform existing = plaque.Find("CancelLabel");
        if (existing != null) return existing.GetComponent<TextMeshPro>();
        GameObject copy = Object.Instantiate(plaque.Find("AltLabel").gameObject, plaque);
        copy.name = "CancelLabel";
        copy.SetActive(false);
        TextMeshPro label = copy.GetComponent<TextMeshPro>();
        label.text = text;
        return label;
    }

    // 상호작용 물건 하나에 왼쪽·오른쪽 클릭 안내 문구를 적는다
    private static void SetPrompt(Interactable target, string prompt, string alt)
    {
        if (target == null) return;
        SerializedObject so = new SerializedObject(target);
        so.FindProperty("_prompt").stringValue = prompt;
        so.FindProperty("_altPrompt").stringValue = alt ?? string.Empty;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // 이미지 자식을 만든다
    private static Image NewImage(Transform parent, string name)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.AddComponent<Image>();
    }

    // 글자 자식을 만든다(클릭을 막지 않음)
    private static TMP_Text NewText(Transform parent, string name, string text, TMP_FontAsset font, float size, Color color, float spacing)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        Style(tmp, font, size, color, spacing);
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        return tmp;
    }

    // 로비와 대기실을 바꾼다: 왼쪽은 어둡고 오른쪽은 회전하는 방이 보이는 그라데이션, 왼쪽 정렬 큰 타이틀과 글자 메뉴, 밑줄 입력칸, 선으로만 나눈 방 목록
    [MenuItem("Tools/DBtJ/Redesign Lobby")]
    public static void BuildLobby()
    {
        LoadFonts();
        Canvas root = null;
        foreach (Canvas c in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (c.transform.parent == null) root = c;
        Transform canvas = root.transform;
        Sprite scrim = GradientSprite("UI_ScrimLeft");
        Sprite pill = AssetDatabase.LoadAssetAtPath<Sprite>($"{SpriteFolder}/UI_Pill.png");

        foreach (string panelName in new[] { "LobbyPanel", "WaitingPanel", "ConnectingPanel" })
        {
            Image bg = canvas.Find(panelName).GetComponent<Image>();
            bg.sprite = scrim;
            bg.type = Image.Type.Simple;
            bg.color = Color.white;
            EditorUtility.SetDirty(bg);
        }

        Transform lobby = canvas.Find("LobbyPanel");
        Brand(lobby);
        Brand(canvas.Find("ConnectingPanel"));
        TMP_Text connecting = canvas.Find("ConnectingPanel/ConnvtingText").GetComponent<TMP_Text>();
        Style(connecting, s_sans, 15f, Dim, 14f);
        connecting.text = "CONNECTING";
        Left(connecting.rectTransform, -840f, -380f, 520f, 40f);
        connecting.alignment = TextAlignmentOptions.MidlineLeft;

        Field(lobby.Find("NickNamePanel"), -840f, 40f, "Your name");
        LeftButton(lobby.Find("CreateButton").GetComponent<Button>(), -840f, -64f);
        Field(lobby.Find("RoomCodePanel"), -840f, -190f, "Table code");
        LeftButton(lobby.Find("JoinButton").GetComponent<Button>(), -840f, -314f);

        RectTransform list = (RectTransform)lobby.Find("RoomListScrollView");
        list.anchoredPosition = new Vector2(470f, -60f);
        list.sizeDelta = new Vector2(780f, 560f);
        Clear(list.GetComponent<Image>());
        foreach (Scrollbar bar in list.GetComponentsInChildren<Scrollbar>(true))
        {
            Clear(bar.GetComponent<Image>());
            if (bar.handleRect != null) bar.handleRect.GetComponent<Image>().color = new Color(Gold.r, Gold.g, Gold.b, 0.35f);
        }
        SetupRoomListContent(list.GetComponent<ScrollRect>());
        TMP_Text listTitle = lobby.Find("Title").GetComponent<TMP_Text>();
        listTitle.text = "OPEN TABLES";
        Label(listTitle);
        RectTransform lt = listTitle.rectTransform;
        lt.anchorMin = lt.anchorMax = new Vector2(0.5f, 0.5f);
        lt.pivot = new Vector2(0f, 0.5f);
        lt.anchoredPosition = new Vector2(80f, 250f);
        lt.sizeDelta = new Vector2(780f, 30f);
        listTitle.alignment = TextAlignmentOptions.MidlineLeft;
        Hairline(lobby, "ListLine", new Vector2(470f, 228f), 780f);

        Modal(lobby.Find("CreateRoomPanel"), pill);
        TMP_Text error = lobby.Find("ErrorText").GetComponent<TMP_Text>();
        Style(error, s_sans, 15f, new Color(0.9f, 0.45f, 0.4f), 6f);
        Left(error.rectTransform, -840f, -400f, 640f, 40f);
        error.alignment = TextAlignmentOptions.MidlineLeft;

        Transform waiting = canvas.Find("WaitingPanel");
        TMP_Text roomName = waiting.Find("RoomTitlePanel/RoomNameText").GetComponent<TMP_Text>();
        Clear(waiting.Find("RoomTitlePanel").GetComponent<Image>());
        Left((RectTransform)waiting.Find("RoomTitlePanel"), -840f, 360f, 900f, 110f);
        Style(roomName, s_serif, 76f, Gold, 4f);
        Stretch(roomName.rectTransform);
        roomName.alignment = TextAlignmentOptions.MidlineLeft;
        Clear(waiting.Find("RoomCodePanel").GetComponent<Image>());
        Left((RectTransform)waiting.Find("RoomCodePanel"), -840f, 286f, 600f, 36f);
        TMP_Text code = waiting.Find("RoomCodePanel/RoomCodeText").GetComponent<TMP_Text>();
        Style(code, s_sans, 18f, Cream, 12f);
        Stretch(code.rectTransform);
        code.alignment = TextAlignmentOptions.MidlineLeft;
        Hairline(waiting, "TitleLine", new Vector2(-690f, 256f), 300f);

        RectTransform players = (RectTransform)waiting.Find("PlayersPanel");
        Clear(players.GetComponent<Image>());
        Left(players, -840f, 70f, 620f, 300f);
        Label(players.Find("Title").GetComponent<TMP_Text>(), true);
        PlayerRow(players.Find("Player1Panel"), 40f);
        PlayerRow(players.Find("Player2Panel"), -60f);

        RectTransform characters = (RectTransform)waiting.Find("CharacterPanel");
        Clear(characters.GetComponent<Image>());
        Left(characters, -840f, -200f, 620f, 200f);
        Label(characters.Find("Title").GetComponent<TMP_Text>(), true);
        string[] buttons = { "KingButton", "QueenButton", "JackButton", "JokerButton" };
        for (int i = 0; i < buttons.Length; i++)
        {
            Button b = characters.Find(buttons[i]).GetComponent<Button>();
            TextButton(b);
            RectTransform r = (RectTransform)b.transform;
            r.anchorMin = r.anchorMax = new Vector2(0f, 0.5f);
            r.pivot = new Vector2(0f, 0.5f);
            r.anchoredPosition = new Vector2(i * 150f, 10f);
            r.sizeDelta = new Vector2(140f, 60f);
        }
        Clear(characters.Find("MyCharacterPanel").GetComponent<Image>());
        Transform plateTitle = characters.Find("MyCharacterPanel/Title");
        if (plateTitle != null) plateTitle.gameObject.SetActive(false);
        TMP_Text mine = characters.Find("MySelectedCharacter").GetComponent<TMP_Text>();
        Style(mine, s_serif, 32f, Gold, 6f);
        RectTransform m = mine.rectTransform;
        m.anchorMin = m.anchorMax = new Vector2(0f, 0.5f);
        m.pivot = new Vector2(0f, 0.5f);
        m.anchoredPosition = new Vector2(0f, -70f);
        m.sizeDelta = new Vector2(600f, 50f);
        mine.alignment = TextAlignmentOptions.MidlineLeft;

        LeftButton(waiting.Find("ReadyButton").GetComponent<Button>(), -840f, -420f);
        LeftButton(waiting.Find("StartButton").GetComponent<Button>(), -840f, -420f);
        Transform leaveLabel = waiting.Find("LeaveButton/Label");
        if (leaveLabel != null) leaveLabel.GetComponent<TMP_Text>().text = "LEAVE";
        LeftButton(waiting.Find("LeaveButton").GetComponent<Button>(), -640f, -420f);

        Modal(canvas.Find("ResultPanel"), pill);
        Style(canvas.Find("ResultPanel/ResultText").GetComponent<TMP_Text>(), s_serif, 34f, Cream, 4f);

        StyleRoomItem();
        EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
        EditorSceneManager.SaveScene(canvas.gameObject.scene);
    }

    // 왼쪽 위에 작은 'DON'T BE', 큰 세리프 'THE JOKER', 가는 선, 기울인 한 줄 소개로 된 타이틀을 놓는다
    private static void Brand(Transform parent)
    {
        Transform old = parent.Find("Brand");
        if (old != null) Object.DestroyImmediate(old.gameObject);
        GameObject brand = new GameObject("Brand", typeof(RectTransform));
        brand.transform.SetParent(parent, false);
        Left((RectTransform)brand.transform, -840f, 300f, 900f, 260f);

        TMP_Text top = NewText(brand.transform, "Kicker", "DON'T BE", s_sans, 18f, Dim, 30f);
        Left(top.rectTransform, 0f, 90f, 600f, 30f, true);
        TMP_Text main = NewText(brand.transform, "Wordmark", "THE JOKER", s_serif, 112f, Gold, 6f);
        Left(main.rectTransform, 0f, 20f, 900f, 120f, true);
        Image line = NewImage(brand.transform, "Line");
        line.color = new Color(Gold.r, Gold.g, Gold.b, 0.45f);
        line.raycastTarget = false;
        Left(line.rectTransform, 4f, -46f, 320f, 1f, true);
        TMP_Text tag = NewText(brand.transform, "Tagline", "A game of nerve, cards and lies.", s_serif, 26f, Dim, 2f);
        tag.fontStyle = FontStyles.Italic;
        Left(tag.rectTransform, 4f, -82f, 700f, 36f, true);
        foreach (TMP_Text t in new[] { top, main, tag }) t.alignment = TextAlignmentOptions.MidlineLeft;
    }

    // 판을 지우고 작은 제목과 밑줄만 남긴 입력칸으로 바꾼다
    private static void Field(Transform panel, float x, float y, string placeholder)
    {
        RectTransform rect = (RectTransform)panel;
        Clear(panel.GetComponent<Image>());
        Left(rect, x, y, 520f, 100f);
        Label(panel.Find("Title").GetComponent<TMP_Text>(), true);
        TMP_InputField input = panel.GetComponentInChildren<TMP_InputField>(true);
        UnderlineInput(input, 0f, -14f, 520f);
        SetPlaceholder(input, placeholder);
    }

    // 입력칸이 비었을 때 보일 흐린 안내 글자를 정한다
    private static void SetPlaceholder(TMP_InputField input, string text)
    {
        if (input.placeholder is TMP_Text placeholder)
        {
            placeholder.text = text;
            placeholder.fontStyle = FontStyles.Italic;
            placeholder.alignment = TextAlignmentOptions.MidlineLeft;
            EditorUtility.SetDirty(placeholder);
        }
    }

    // 입력칸 바탕을 지우고 가는 밑줄, 세리프 글자, 금색 커서로 바꿔 지정 위치에 놓는다
    private static void UnderlineInput(TMP_InputField input, float x, float y, float width)
    {
        RectTransform r = (RectTransform)input.transform;
        r.anchorMin = r.anchorMax = new Vector2(0f, 0.5f);
        r.pivot = new Vector2(0f, 0.5f);
        r.anchoredPosition = new Vector2(x, y);
        r.sizeDelta = new Vector2(width, 56f);
        Image fill = input.GetComponent<Image>();
        fill.sprite = FrameSprite("UI_Field", new Color(0.035f, 0.03f, 0.026f, 0.78f), new Color(Gold.r, Gold.g, Gold.b, 0.28f), 4f);
        fill.type = Image.Type.Sliced;
        fill.color = new Color(1f, 1f, 1f, 0.85f);
        EditorUtility.SetDirty(fill);
        Transform existing = input.transform.Find("Underline");
        Image line = existing != null ? existing.GetComponent<Image>() : NewImage(input.transform, "Underline");
        line.color = new Color(Gold.r, Gold.g, Gold.b, 0.55f);
        line.raycastTarget = false;
        RectTransform l = line.rectTransform;
        l.anchorMin = new Vector2(0f, 0f);
        l.anchorMax = new Vector2(1f, 0f);
        l.pivot = new Vector2(0.5f, 0f);
        l.anchoredPosition = Vector2.zero;
        l.sizeDelta = new Vector2(0f, 1f);
        if (input.textComponent != null) Style(input.textComponent, s_serif, 34f, Cream, 2f);
        if (input.placeholder is TMP_Text placeholder) Style(placeholder, s_serif, 30f, new Color(Cream.r, Cream.g, Cream.b, 0.38f), 2f);
        input.caretColor = Gold;
        input.customCaretColor = true;
        input.selectionColor = new Color(Gold.r, Gold.g, Gold.b, 0.3f);
        if (input.textViewport != null)
        {
            input.textViewport.offsetMin = new Vector2(16f, input.textViewport.offsetMin.y);
            input.textViewport.offsetMax = new Vector2(-16f, input.textViewport.offsetMax.y);
        }
        FieldFocusFx focus = input.GetComponent<FieldFocusFx>();
        if (focus == null) focus = input.gameObject.AddComponent<FieldFocusFx>();
        SerializedObject so = new SerializedObject(focus);
        so.FindProperty("_input").objectReferenceValue = input;
        so.FindProperty("_fill").objectReferenceValue = fill;
        so.FindProperty("_underline").objectReferenceValue = line;
        so.FindProperty("_idleFill").floatValue = 0.85f;
        so.FindProperty("_focusFill").floatValue = 1f;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(input);
    }

    // 왼쪽 정렬 글자 버튼(밑줄은 글자 아래 왼쪽부터)으로 바꿔 지정 위치에 놓는다
    private static void LeftButton(Button button, float x, float y)
    {
        TextButton(button);
        Left((RectTransform)button.transform, x, y, 300f, 56f);
        Image underlineImage = FindDeep(button.transform, "Underline");
        TMP_Text main = null;
        foreach (TMP_Text label in button.GetComponentsInChildren<TMP_Text>(true))
        {
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.fontSize = 22f;
            label.fontSizeMax = 22f;
            label.margin = Vector4.zero;
            if (main == null || !string.IsNullOrEmpty(label.text)) main = label;
        }
        underlineImage.transform.SetParent(main.transform, false);
        FitUnderline(button, main, true);

        Transform oldMarker = button.transform.Find("Marker");
        Image marker = oldMarker != null ? oldMarker.GetComponent<Image>() : NewImage(button.transform, "Marker");
        marker.color = new Color(Gold.r, Gold.g, Gold.b, 0f);
        marker.raycastTarget = false;
        RectTransform mr = marker.rectTransform;
        mr.anchorMin = mr.anchorMax = new Vector2(0f, 0.5f);
        mr.pivot = new Vector2(0.5f, 0.5f);
        mr.anchoredPosition = new Vector2(4f, 2f);
        mr.sizeDelta = new Vector2(7f, 7f);
        mr.localRotation = Quaternion.Euler(0f, 0f, 45f);
        MenuButtonFx fx = button.GetComponent<MenuButtonFx>();
        if (fx == null) fx = button.gameObject.AddComponent<MenuButtonFx>();
        SerializedObject so = new SerializedObject(fx);
        so.FindProperty("_label").objectReferenceValue = main.rectTransform;
        so.FindProperty("_marker").objectReferenceValue = marker;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // 자식 전체에서 이름이 같은 이미지를 찾는다
    private static Image FindDeep(Transform root, string name)
    {
        foreach (Image image in root.GetComponentsInChildren<Image>(true))
            if (image.name == name) return image;
        return null;
    }

    // 플레이어 줄: 판을 지우고 아래 가는 선, 작은 HOST/GUEST 제목, 큰 세리프 이름으로 바꾼다
    private static void PlayerRow(Transform row, float y)
    {
        RectTransform rect = (RectTransform)row;
        Clear(row.GetComponent<Image>());
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, y);
        rect.sizeDelta = new Vector2(600f, 90f);
        Transform title = row.Find("Title");
        if (title != null)
        {
            TMP_Text t = title.GetComponent<TMP_Text>();
            Label(t, true);
            RectTransform tr = t.rectTransform;
            tr.anchoredPosition = new Vector2(0f, -10f);
            tr.offsetMin = new Vector2(0f, tr.offsetMin.y);
        }
        foreach (TMP_Text text in row.GetComponentsInChildren<TMP_Text>(true))
        {
            if (text.name == "Title") continue;
            Style(text, s_serif, 38f, Cream, 4f);
            text.alignment = TextAlignmentOptions.BottomLeft;
            Stretch(text.rectTransform);
            text.rectTransform.offsetMin = new Vector2(0f, 10f);
        }
        Hairline(row, "Line", new Vector2(300f, -45f), 600f, new Vector2(0f, 0.5f));
    }

    // 모달 창(방 만들기, 결과)을 어두운 알약 판, 세리프 제목, 밑줄 입력칸, 글자 버튼으로 바꾼다
    private static void Modal(Transform panel, Sprite pill)
    {
        RectTransform rect = (RectTransform)panel;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        Image image = panel.GetComponent<Image>();
        image.sprite = pill;
        image.type = Image.Type.Sliced;
        image.color = Color.white;
        EditorUtility.SetDirty(image);
        TMP_Text title = panel.Find("Title").GetComponent<TMP_Text>();
        Style(title, s_serif, 44f, Gold, 8f);
        title.rectTransform.anchoredPosition = new Vector2(0f, -40f);
        TMP_InputField input = panel.GetComponentInChildren<TMP_InputField>(true);
        if (input != null)
        {
            UnderlineInput(input, 0f, 20f, 520f);
            SetPlaceholder(input, "Name your table");
            RectTransform r = (RectTransform)input.transform;
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
            r.pivot = new Vector2(0.5f, 0.5f);
        }
        foreach (Button button in panel.GetComponentsInChildren<Button>(true)) TextButton(button);
    }

    // 스크롤 뷰 안 내용 칸을 위에서부터 한 줄씩 쌓는 목록으로 만들고 로비 관리자가 그곳에 줄을 넣게 한다
    private static void SetupRoomListContent(ScrollRect scroll)
    {
        RectTransform viewport = scroll.viewport != null ? scroll.viewport : (RectTransform)scroll.transform.Find("Viewport");
        RectTransform content = scroll.content;
        if (content == null)
        {
            Transform found = viewport.Find("Content");
            content = found != null ? (RectTransform)found : (RectTransform)new GameObject("Content", typeof(RectTransform)).transform;
            content.SetParent(viewport, false);
            scroll.content = content;
        }
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = Vector2.zero;
        VerticalLayoutGroup layout = content.GetComponent<VerticalLayoutGroup>();
        if (layout == null) layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        layout.spacing = 0f;
        ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>();
        if (fitter == null) fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.horizontal = false;
        scroll.vertical = true;
        if (scroll.horizontalScrollbar != null) scroll.horizontalScrollbar.gameObject.SetActive(false);
        scroll.horizontalScrollbar = null;
        EditorUtility.SetDirty(scroll);

        LobbyManager lobby = Object.FindFirstObjectByType<LobbyManager>(FindObjectsInactive.Include);
        SerializedObject so = new SerializedObject(lobby);
        so.FindProperty("_roomListContent").objectReferenceValue = content;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // 방 목록 한 줄 프리팹을 판 없이 아래 선, 세리프 방 이름, 본문 인원수, 밑줄 JOIN 버튼으로 바꾼다
    private static void StyleRoomItem()
    {
        string path = "Assets/Prefabs/RoomItem.prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        foreach (Image image in root.GetComponentsInChildren<Image>(true))
        {
            if (image.GetComponent<Button>() != null || image.name == "Underline" || image.name == "Line") continue;
            Clear(image);
        }
        foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
        {
            if (text.transform.parent.GetComponent<Button>() != null) continue;
            bool name = text.name == "RoomNameText";
            Style(text, name ? s_serif : s_sans, name ? 30f : 18f, name ? Cream : Dim, name ? 2f : 6f);
        }
        foreach (Button button in root.GetComponentsInChildren<Button>(true)) TextButton(button);
        RectTransform item = (RectTransform)root.transform;
        item.sizeDelta = new Vector2(780f, 76f);
        RowPart(root.transform, "RoomNameText", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(8f, 0f), new Vector2(460f, 56f));
        RowPart(root.transform, "PlayerCountText", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-210f, 0f), new Vector2(80f, 56f));
        RowPart(root.transform, "JoinButton", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-70f, 0f), new Vector2(120f, 56f));
        foreach (string panel in new[] { "PlayerCountPanel", "PlayerNamePanel" })
        {
            Transform p = root.transform.Find(panel);
            if (p != null) p.gameObject.SetActive(false);
        }
        root.transform.Find("RoomNameText").GetComponent<TMP_Text>().alignment = TextAlignmentOptions.MidlineLeft;
        Hairline(root.transform, "Line", Vector2.zero, 0f, new Vector2(0.5f, 0f));
        RectTransform line = (RectTransform)root.transform.Find("Line");
        line.anchorMin = new Vector2(0f, 0f);
        line.anchorMax = new Vector2(1f, 0f);
        line.sizeDelta = new Vector2(0f, 1f);
        line.GetComponent<Image>().color = new Color(Gold.r, Gold.g, Gold.b, 0.25f);
        PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);
    }

    // 방 목록 한 줄 안 요소의 기준점과 위치, 크기를 정한다
    private static void RowPart(Transform root, string name, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
    {
        Transform t = root.Find(name);
        if (t == null) return;
        RectTransform r = (RectTransform)t;
        r.anchorMin = r.anchorMax = anchor;
        r.pivot = new Vector2(anchor.x, 0.5f);
        r.anchoredPosition = position;
        r.sizeDelta = size;
    }

    // 작은 대문자 제목 글자(본문 글꼴, 넓은 자간, 흐린 색)로 바꾼다
    private static void Label(TMP_Text text, bool leftTop = false)
    {
        Style(text, s_sans, 13f, Dim, 12f);
        if (!leftTop) return;
        text.alignment = TextAlignmentOptions.TopLeft;
        RectTransform r = text.rectTransform;
        r.anchoredPosition = new Vector2(0f, 0f);
        r.offsetMin = new Vector2(0f, r.offsetMin.y);
        r.offsetMax = new Vector2(0f, r.offsetMax.y);
    }

    // 이미지를 투명하게 해 판을 없앤다(클릭은 그대로 받음)
    private static void Clear(Image image)
    {
        if (image == null) return;
        image.sprite = null;
        image.color = new Color(0f, 0f, 0f, 0f);
        EditorUtility.SetDirty(image);
    }

    // 지정한 폭의 가는 금색 선을 만든다
    private static void Hairline(Transform parent, string name, Vector2 position, float width, Vector2? anchor = null)
    {
        Transform existing = parent.Find(name);
        Image line = existing != null ? existing.GetComponent<Image>() : NewImage(parent, name);
        line.color = new Color(Gold.r, Gold.g, Gold.b, 0.4f);
        line.raycastTarget = false;
        RectTransform rect = line.rectTransform;
        rect.anchorMin = rect.anchorMax = anchor ?? new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(width, 1f);
    }

    // 화면 가운데 기준으로 왼쪽 끝 x, 세로 가운데 y에 놓는다(inParent면 부모 왼쪽 가운데 기준)
    private static void Left(RectTransform rect, float x, float y, float width, float height, bool inParent = false)
    {
        rect.anchorMin = rect.anchorMax = inParent ? new Vector2(0f, 0.5f) : new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = new Vector2(width, height);
    }

    // 왼쪽은 짙고 오른쪽으로 갈수록 투명해지는 가로 그라데이션 스프라이트를 만든다
    private static Sprite GradientSprite(string name)
    {
        const int w = 256;
        Texture2D texture = new Texture2D(w, 4, TextureFormat.RGBA32, false);
        for (int x = 0; x < w; x++)
        {
            float t = x / (w - 1f);
            float a = Mathf.Lerp(0.9f, 0f, Mathf.SmoothStep(0.25f, 0.75f, t));
            for (int y = 0; y < 4; y++) texture.SetPixel(x, y, new Color(0.015f, 0.012f, 0.01f, a));
        }
        Sprite sprite = SaveSprite(texture, name, Vector4.zero);
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath($"{SpriteFolder}/{name}.png");
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>($"{SpriteFolder}/{name}.png");
    }

    // 화면 가운데 기준 높이에 가로로 넓게 놓는다
    private static void Center(Transform target, float y)
    {
        if (target == null) return;
        RectTransform rect = (RectTransform)target;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, y);
        rect.sizeDelta = new Vector2(1000f, 60f);
    }

    // 부모 크기에 꽉 차게 한다
    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    // 가는 고리 스프라이트(두께 지정)를 그려 저장한다
    private static Sprite RingSprite(string name, float thickness)
    {
        const int n = 256;
        Texture2D texture = new Texture2D(n, n, TextureFormat.RGBA32, false);
        float c = (n - 1) * 0.5f;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                float a = Mathf.Clamp01(c - d + 0.5f) * Mathf.Clamp01(d - (c - thickness) + 0.5f);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        return SaveSprite(texture, name, Vector4.zero);
    }

    // 둥근 모서리 판 스프라이트(바탕색, 테두리색, 반지름)를 그려 9분할로 저장한다
    private static Sprite FrameSprite(string name, Color fill, Color border, float radius)
    {
        const int w = 64;
        const int h = 64;
        Texture2D texture = new Texture2D(w, h, TextureFormat.RGBA32, false);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float qx = Mathf.Abs(x + 0.5f - w * 0.5f) - (w * 0.5f - radius);
                float qy = Mathf.Abs(y + 0.5f - h * 0.5f) - (h * 0.5f - radius);
                float d = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
                float coverage = Mathf.Clamp01(0.5f - d);
                float edge = Mathf.Clamp01(1.2f - Mathf.Abs(d + 1f));
                Color c = Color.Lerp(fill, border, border.a > 0f ? edge : 0f);
                c.a = Mathf.Max(fill.a, border.a * edge) * coverage;
                texture.SetPixel(x, y, c);
            }
        float b = radius + 2f;
        return SaveSprite(texture, name, new Vector4(b, b, b, b));
    }

    // 텍스처를 PNG로 저장하고 스프라이트로 들여온다
    private static Sprite SaveSprite(Texture2D texture, string name, Vector4 border)
    {
        texture.Apply();
        string path = $"{SpriteFolder}/{name}.png";
        File.WriteAllBytes(path, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path);
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spriteBorder = border;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
}
