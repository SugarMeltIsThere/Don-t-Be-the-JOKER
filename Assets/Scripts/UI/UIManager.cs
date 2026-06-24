using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("P1 UI")]
    [SerializeField] private TextMeshProUGUI _p1CardsText;
    [SerializeField] private TextMeshProUGUI _p1ChipsText;
    [SerializeField] private TextMeshProUGUI _p1TotalText;
    [SerializeField] private Button _p1HitButton;
    [SerializeField] private Button _p1StandButton;
    [SerializeField] private Button _p1RaiseButton;
    [SerializeField] private Button _p1CallButton;
    [SerializeField] private Button _p1DieButton;
    [SerializeField] private GameObject _p1RaisePanel;
    [SerializeField] private TextMeshProUGUI _p1RaiseAmountText;
    [SerializeField] private Button _p1Plus1Button;
    [SerializeField] private Button _p1Plus5Button;
    [SerializeField] private Button _p1Plus10Button;
    [SerializeField] private Button _p1Minus1Button;
    [SerializeField] private Button _p1Minus5Button;
    [SerializeField] private Button _p1Minus10Button;
    [SerializeField] private Button _p1RaiseConfirmButton;
    [SerializeField] private Button _p1CancelButton;

    [Header("P2 UI")]
    [SerializeField] private TextMeshProUGUI _p2CardsText;
    [SerializeField] private TextMeshProUGUI _p2ChipsText;
    [SerializeField] private TextMeshProUGUI _p2TotalText;
    [SerializeField] private Button _p2HitButton;
    [SerializeField] private Button _p2StandButton;
    [SerializeField] private Button _p2RaiseButton;
    [SerializeField] private Button _p2CallButton;
    [SerializeField] private Button _p2DieButton;
    [SerializeField] private GameObject _p2RaisePanel;
    [SerializeField] private TextMeshProUGUI _p2RaiseAmountText;
    [SerializeField] private Button _p2Plus1Button;
    [SerializeField] private Button _p2Plus5Button;
    [SerializeField] private Button _p2Plus10Button;
    [SerializeField] private Button _p2Minus1Button;
    [SerializeField] private Button _p2Minus5Button;
    [SerializeField] private Button _p2Minus10Button;
    [SerializeField] private Button _p2RaiseConfirmButton;
    [SerializeField] private Button _p2CancelButton;

    [Header("Shared UI")]
    [SerializeField] private TextMeshProUGUI _sharedCardText;
    [SerializeField] private TextMeshProUGUI _potText;

    [Header("Ace Selection UI")]
    [SerializeField] private GameObject _aceSelectionPanel;
    [SerializeField] private Transform _p1AceButtonGroup;
    [SerializeField] private Transform _p2AceButtonGroup;
    [SerializeField] private Button _p1AceConfirmButton;
    [SerializeField] private Button _p2AceConfirmButton;
    
    [Header("Item Slots")]
    [SerializeField] private Button[] _p1ItemButtons;
    [SerializeField] private Button[] _p2ItemButtons;
    [SerializeField] private TextMeshProUGUI[] _p1ItemTexts;
    [SerializeField] private TextMeshProUGUI[] _p2ItemTexts;

    [Header("Item Discard UI")]
    [SerializeField] private GameObject _itemDiscardPanel;
    [SerializeField] private TextMeshProUGUI _newItemText;
    [SerializeField] private Button _discardButton1;
    [SerializeField] private Button _discardButton2;
    [SerializeField] private Button _discardButton3;
    [SerializeField] private Button _discardNewButton;

    [Header("Game End UI")]
    [SerializeField] private GameObject _gameEndPanel;
    [SerializeField] private TextMeshProUGUI _resultText;
    [SerializeField] private Button _restartButton;

    private ActivePlayer _discardTargetPlayer = ActivePlayer.None;

    private int _p1RaiseAmount = 0;
    private int _p2RaiseAmount = 0;

    private List<Button> _p1AceButtons = new List<Button>();
    private List<Button> _p2AceButtons = new List<Button>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        // P1 버튼 리스너
        _p1HitButton.onClick.AddListener(() => GameManager.Instance.OnHit());
        _p1StandButton.onClick.AddListener(() => GameManager.Instance.OnStand());
        _p1RaiseButton.onClick.AddListener(() => OpenRaisePanel(ActivePlayer.Player1));
        _p1CallButton.onClick.AddListener(() => GameManager.Instance.OnCall());
        _p1DieButton.onClick.AddListener(() => GameManager.Instance.OnDie());
        _p1Plus1Button.onClick.AddListener(() => AddRaiseAmount(ActivePlayer.Player1, 1));
        _p1Plus5Button.onClick.AddListener(() => AddRaiseAmount(ActivePlayer.Player1, 5));
        _p1Plus10Button.onClick.AddListener(() => AddRaiseAmount(ActivePlayer.Player1, 10));
        _p1Minus1Button.onClick.AddListener(() => AddRaiseAmount(ActivePlayer.Player1, -1));
        _p1Minus5Button.onClick.AddListener(() => AddRaiseAmount(ActivePlayer.Player1, -5));
        _p1Minus10Button.onClick.AddListener(() => AddRaiseAmount(ActivePlayer.Player1, -10));
        _p1RaiseConfirmButton.onClick.AddListener(() => ConfirmRaise(ActivePlayer.Player1));
        _p1CancelButton.onClick.AddListener(() => CloseRaisePanel(ActivePlayer.Player1));

        // P2 버튼 리스너
        _p2HitButton.onClick.AddListener(() => GameManager.Instance.OnHit());
        _p2StandButton.onClick.AddListener(() => GameManager.Instance.OnStand());
        _p2RaiseButton.onClick.AddListener(() => OpenRaisePanel(ActivePlayer.Player2));
        _p2CallButton.onClick.AddListener(() => GameManager.Instance.OnCall());
        _p2DieButton.onClick.AddListener(() => GameManager.Instance.OnDie());
        _p2Plus1Button.onClick.AddListener(() => AddRaiseAmount(ActivePlayer.Player2, 1));
        _p2Plus5Button.onClick.AddListener(() => AddRaiseAmount(ActivePlayer.Player2, 5));
        _p2Plus10Button.onClick.AddListener(() => AddRaiseAmount(ActivePlayer.Player2, 10));
        _p2Minus1Button.onClick.AddListener(() => AddRaiseAmount(ActivePlayer.Player2, -1));
        _p2Minus5Button.onClick.AddListener(() => AddRaiseAmount(ActivePlayer.Player2, -5));
        _p2Minus10Button.onClick.AddListener(() => AddRaiseAmount(ActivePlayer.Player2, -10));
        _p2RaiseConfirmButton.onClick.AddListener(() => ConfirmRaise(ActivePlayer.Player2));
        _p2CancelButton.onClick.AddListener(() => CloseRaisePanel(ActivePlayer.Player2));

        _discardButton1.onClick.AddListener(() => GameManager.Instance.OnItemDiscard(_discardTargetPlayer, 0));
        _discardButton2.onClick.AddListener(() => GameManager.Instance.OnItemDiscard(_discardTargetPlayer, 1));
        _discardButton3.onClick.AddListener(() => GameManager.Instance.OnItemDiscard(_discardTargetPlayer, 2));
        _discardNewButton.onClick.AddListener(() => GameManager.Instance.OnItemDiscard(_discardTargetPlayer, -1)); // -1 = 새 아이템 폐기

        _p1AceConfirmButton.onClick.AddListener(() => GameManager.Instance.OnAceSelectionConfirmed(ActivePlayer.Player1));
        _p2AceConfirmButton.onClick.AddListener(() => GameManager.Instance.OnAceSelectionConfirmed(ActivePlayer.Player2));

        _restartButton.onClick.AddListener(() => GameManager.Instance.RestartGame());

        for (int i = 0; i < _p1ItemButtons.Length; i++)
        {
            int index = i;
            _p1ItemButtons[i].onClick.AddListener(()
                => OnItemButtonClicked(ActivePlayer.Player1, index));
        }

        for (int i = 0; i < _p2ItemButtons.Length; i++)
        {
            int index = i;
            _p2ItemButtons[i].onClick.AddListener(()
                => OnItemButtonClicked(ActivePlayer.Player2, index));
        }
    }

    private void Update()
    {
        Refresh();
        UpdatePhaseUI();
        UpdateItemSlots();
    }
    private void Refresh()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null) return;

        // 칩 / 팟
        _p1ChipsText.text = $"Chips: {gm.Player1Chips}";
        _p2ChipsText.text = $"Chips: {gm.Player2Chips}";
        _potText.text = $"POT: {gm.Pot}";

        // 버튼 활성화
        UpdateButtons(gm);
    }

    private void UpdateButtons(GameManager gm)
    {
        bool isP1Turn = gm.CurrentPlayer == ActivePlayer.Player1;
        bool isP2Turn = gm.CurrentPlayer == ActivePlayer.Player2;

        _p1HitButton.interactable = isP1Turn && gm.CanHit;
        _p1StandButton.interactable = isP1Turn && gm.CanStand;
        _p1RaiseButton.interactable = isP1Turn && gm.CanRaise;
        _p1CallButton.interactable = isP1Turn && gm.CanCall;
        _p1DieButton.interactable = isP1Turn && gm.CanDie;

        _p2HitButton.interactable = isP2Turn && gm.CanHit;
        _p2StandButton.interactable = isP2Turn && gm.CanStand;
        _p2RaiseButton.interactable = isP2Turn && gm.CanRaise;
        _p2CallButton.interactable = isP2Turn && gm.CanCall;
        _p2DieButton.interactable = isP2Turn && gm.CanDie;
    }

    // 카드 텍스트 갱신
    public void RefreshCards(
        string sharedCard,
        string p1Cards, int p1Total,
        string p2Cards, int p2Total)
    {
        _sharedCardText.text = $"Shared Card: {sharedCard}";
        _p1CardsText.text = $"CARDS: {p1Cards}";
        _p1TotalText.text = $"Total: {p1Total}";
        _p2CardsText.text = $"CARDS: {p2Cards}";
        _p2TotalText.text = $"Total: {p2Total}";
    }

    private void OpenRaisePanel(ActivePlayer player)
    {
        if (player == ActivePlayer.Player1)
        {
            _p1RaiseAmount = 0;
            _p1RaiseAmountText.text = "0";
            _p1RaisePanel.SetActive(true);
        }
        else
        {
            _p2RaiseAmount = 0;
            _p2RaiseAmountText.text = "0";
            _p2RaisePanel.SetActive(true);
        }
    }

    private void CloseRaisePanel(ActivePlayer player)
    {
        if (player == ActivePlayer.Player1)
        {
            _p1RaiseAmount = 0;
            _p1RaisePanel.SetActive(false);
        }
        else
        {
            _p2RaiseAmount = 0;
            _p2RaisePanel.SetActive(false);
        }
    }

    private void AddRaiseAmount(ActivePlayer player, int amount)
    {
        int maxRaise = GameManager.Instance.MaxRaise; // MaxBet → MaxRaise

        if (player == ActivePlayer.Player1)
        {
            _p1RaiseAmount = Mathf.Clamp(_p1RaiseAmount + amount, 0, maxRaise);
            _p1RaiseAmountText.text = _p1RaiseAmount.ToString();
        }
        else
        {
            _p2RaiseAmount = Mathf.Clamp(_p2RaiseAmount + amount, 0, maxRaise);
            _p2RaiseAmountText.text = _p2RaiseAmount.ToString();
        }
    }

    private void ConfirmRaise(ActivePlayer player)
    {
        int amount = player == ActivePlayer.Player1
            ? _p1RaiseAmount
            : _p2RaiseAmount;

        if (amount <= 0) return;

        GameManager.Instance.OnRaise(amount);
        CloseRaisePanel(player);
    }
    private void UpdatePhaseUI()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null) return;

        switch (gm.CurrentPhase)
        {
            case GamePhase.Draw:
                // 히트/스탠드 버튼 영역 표시
                // 베팅 버튼 숨김
                SetBettingButtonsVisible(false);
                break;

            case GamePhase.AceSelection:
                SetBettingButtonsVisible(false);
                ShowAceSelectionPanel();  // 추가
                break;

            case GamePhase.CardReveal:
                // 나중에 애니메이션 추가
                SetBettingButtonsVisible(false);
                break;

            case GamePhase.Betting:
                SetBettingButtonsVisible(true);
                break;
            case GamePhase.GameEnd:
                ShowGameEndPanel();
                break;
            case GamePhase.ItemDiscard:
                ShowItemDiscardPanel();
                break;
        }
    }

    private void SetBettingButtonsVisible(bool visible)
    {
        _p1RaiseButton.gameObject.SetActive(visible);
        _p1CallButton.gameObject.SetActive(visible);
        _p1DieButton.gameObject.SetActive(visible);
        _p2RaiseButton.gameObject.SetActive(visible);
        _p2CallButton.gameObject.SetActive(visible);
        _p2DieButton.gameObject.SetActive(visible);
    }
    private void ShowAceSelectionPanel()
    {
        if (_aceSelectionPanel.activeSelf) return; // 이미 열려있으면 중복 실행 방지

        _aceSelectionPanel.SetActive(true);

        GenerateAceButtons(ActivePlayer.Player1);
        GenerateAceButtons(ActivePlayer.Player2);
    }

    private void GenerateAceButtons(ActivePlayer player)
    {
        GameManager gm = GameManager.Instance;
        Transform group = player == ActivePlayer.Player1
            ? _p1AceButtonGroup
            : _p2AceButtonGroup;
        List<Button> btnList = player == ActivePlayer.Player1
            ? _p1AceButtons
            : _p2AceButtons;

        // 기존 버튼 제거
        foreach (Button btn in btnList)
            Destroy(btn.gameObject);
        btnList.Clear();

        // 해당 플레이어 패에서 에이스 찾아서 버튼 생성
        var cards = player == ActivePlayer.Player1
            ? gm.GetPlayer1Cards()
            : gm.GetPlayer2Cards();

        for (int i = 0; i < cards.Count; i++)
        {
            if (cards[i].Card.Rank != Rank.Ace) continue;

            int index = i; // 클로저 캡처용

            // 버튼 동적 생성
            GameObject btnObj = new GameObject($"Ace_{index}");
            btnObj.transform.SetParent(group, false);

            Button btn = btnObj.AddComponent<Button>();
            var image = btnObj.AddComponent<UnityEngine.UI.Image>();
            image.color = Color.white;

            // 텍스트 추가
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(btnObj.transform, false);
            var tmp = textObj.AddComponent<TMPro.TextMeshProUGUI>();
            tmp.text = "Ace: 1";
            tmp.fontSize = 20;
            tmp.alignment = TMPro.TextAlignmentOptions.Center;
            tmp.color = Color.black;

            // RectTransform 설정
            var rect = btnObj.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(120, 50);

            var textRect = textObj.GetComponent<RectTransform>();
            textRect.sizeDelta = new Vector2(120, 50);

            bool isEleven = false;
            btn.onClick.AddListener(() =>
            {
                isEleven = !isEleven;
                tmp.text = $"Ace: {(isEleven ? 11 : 1)}";
                GameManager.Instance.OnAceValueChanged(player, index, isEleven);
            });

            btnList.Add(btn);
        }
    }

    public void HideAceSelectionPanel()
    {
        _aceSelectionPanel.SetActive(false);

        foreach (Button btn in _p1AceButtons) Destroy(btn.gameObject);
        foreach (Button btn in _p2AceButtons) Destroy(btn.gameObject);
        _p1AceButtons.Clear();
        _p2AceButtons.Clear();
    }

    private void ShowGameEndPanel()
    {
        if (_gameEndPanel.activeSelf) return;

        _gameEndPanel.SetActive(true);

        // 칩이 남은 쪽이 승리
        string result = GameManager.Instance.Player1Chips > 0
            ? "P1 Wins!"
            : "P2 Wins!";

        _resultText.text = result;
    }

    public void HideGameEndPanel()
    {
        _gameEndPanel.SetActive(false);
    }

    private void OnItemButtonClicked(ActivePlayer player, int index)
    {
        GameManager gm = GameManager.Instance;
        var items = player == ActivePlayer.Player1
            ? gm.Player1Items
            : gm.Player2Items;

        if (index >= items.Count) return;

        ItemType type = items[index].Type;

        switch (type)
        {
            case ItemType.Magnifier:
                OnMagnifierClicked(player);
                break;
            case ItemType.Flash:
                gm.OnUseFlash(player);
                break;
            case ItemType.Bribe:
                gm.OnUseBribe(player);
                break;
        }
    }

    private void OnMagnifierClicked(ActivePlayer player)
    {
        Card next = GameManager.Instance.OnUseMagnifier(player);
        if (next == null) return;

        // 돋보기로 본 카드를 팝업으로 표시
        // 지금은 로그로 대체
        Debug.Log($"[돋보기 UI] 다음 카드: {next}");
    }

    private void UpdateItemSlots()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null) return;

        UpdatePlayerItemSlots(
            _p1ItemButtons,
            _p1ItemTexts,
            gm.Player1Items,
            ActivePlayer.Player1);

        UpdatePlayerItemSlots(
            _p2ItemButtons,
            _p2ItemTexts,
            gm.Player2Items,
            ActivePlayer.Player2);
    }

    private void UpdatePlayerItemSlots(
        Button[] buttons,
        TextMeshProUGUI[] texts,
        IReadOnlyList<Item> items,
        ActivePlayer player)
    {
        GameManager gm = GameManager.Instance;
        bool isMyTurn = gm.CurrentPlayer == player;

        for (int i = 0; i < buttons.Length; i++)
        {
            if (i < items.Count)
            {
                // 아이템 있음
                texts[i].text = items[i].GetName();
                buttons[i].interactable = isMyTurn && CanUseItem(items[i].Type);
            }
            else
            {
                // 슬롯 비어있음
                texts[i].text = "-";
                buttons[i].interactable = false;
            }
        }
    }

    private bool CanUseItem(ItemType type)
    {
        GameManager gm = GameManager.Instance;
        return type switch
        {
            ItemType.Magnifier => gm.CurrentPhase == GamePhase.Draw,
            ItemType.Bribe => gm.CurrentPhase == GamePhase.Draw,
            ItemType.Flash => gm.CurrentPhase == GamePhase.Betting,
            _ => false
        };
    }

    private void ShowItemDiscardPanel()
    {
        if (_itemDiscardPanel.activeSelf) return;

        GameManager gm = GameManager.Instance;
        _discardTargetPlayer = gm.PendingItemPlayer;

        var items = _discardTargetPlayer == ActivePlayer.Player1
            ? gm.Player1Items
            : gm.Player2Items;

        int slots = _discardTargetPlayer == ActivePlayer.Player1
            ? gm.Player1ItemSlots
            : gm.Player2ItemSlots;

        _newItemText.text = $"New Item: {gm.PendingItem?.GetName()}";

        _discardButton1.GetComponentInChildren<TextMeshProUGUI>().text
            = $"Discard: {(items.Count > 0 ? items[0].GetName() : "-")}";
        _discardButton2.GetComponentInChildren<TextMeshProUGUI>().text
            = $"Discard: {(items.Count > 1 ? items[1].GetName() : "-")}";

        // 킹 캐릭터면 3번째 슬롯 버튼 표시
        if (slots >= 3)
        {
            _discardButton3.gameObject.SetActive(true);
            _discardButton3.GetComponentInChildren<TextMeshProUGUI>().text
                = $"Discard: {(items.Count > 2 ? items[2].GetName() : "-")}";
        }
        else
        {
            _discardButton3.gameObject.SetActive(false);
        }

        _discardNewButton.GetComponentInChildren<TextMeshProUGUI>().text
            = $"Discard: {gm.PendingItem?.GetName()} (New)";

        _itemDiscardPanel.SetActive(true);
    }

    public void HideItemDiscardPanel()
    {
        _itemDiscardPanel.SetActive(false);
    }
}