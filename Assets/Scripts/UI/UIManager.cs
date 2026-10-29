using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
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
    [SerializeField] private TextMeshProUGUI _p2RaiseAmountText;

    [Header("Shared UI")]
    [SerializeField] private TextMeshProUGUI _sharedCardText;
    [SerializeField] private TextMeshProUGUI _potText;

    [Header("Ace Selection UI")]
    [SerializeField] private GameObject _aceSelectionPanel;
    [SerializeField] private Transform _p1AceButtonGroup;
    [SerializeField] private Button _p1AceConfirmButton;

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
    [SerializeField] private Button _returnToLobbyButton;
    [SerializeField] private Button _leaveRoomButton;

    private ActivePlayer _discardTargetPlayer = ActivePlayer.None;

    private int _p1RaiseAmount = 0;

    private List<Button> _p1AceButtons = new List<Button>();

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
        _p1HitButton.onClick.AddListener(() => OnHitClicked());
        _p1StandButton.onClick.AddListener(() => OnStandClicked());
        _p1RaiseButton.onClick.AddListener(() => OpenRaisePanel(ActivePlayer.Player1));
        _p1CallButton.onClick.AddListener(() => OnCallClicked());
        _p1DieButton.onClick.AddListener(() => OnDieClicked());
        _p1Plus1Button.onClick.AddListener(() => AddRaiseAmount(ActivePlayer.Player1, 1));
        _p1Plus5Button.onClick.AddListener(() => AddRaiseAmount(ActivePlayer.Player1, 5));
        _p1Plus10Button.onClick.AddListener(() => AddRaiseAmount(ActivePlayer.Player1, 10));
        _p1Minus1Button.onClick.AddListener(() => AddRaiseAmount(ActivePlayer.Player1, -1));
        _p1Minus5Button.onClick.AddListener(() => AddRaiseAmount(ActivePlayer.Player1, -5));
        _p1Minus10Button.onClick.AddListener(() => AddRaiseAmount(ActivePlayer.Player1, -10));
        _p1RaiseConfirmButton.onClick.AddListener(() => ConfirmRaise(ActivePlayer.Player1));
        _p1CancelButton.onClick.AddListener(() => CloseRaisePanel(ActivePlayer.Player1));

        _discardButton1.onClick.AddListener(() => GameManager.Instance.OnItemDiscard(_discardTargetPlayer, 0));
        _discardButton2.onClick.AddListener(() => GameManager.Instance.OnItemDiscard(_discardTargetPlayer, 1));
        _discardButton3.onClick.AddListener(() => GameManager.Instance.OnItemDiscard(_discardTargetPlayer, 2));
        _discardNewButton.onClick.AddListener(() => GameManager.Instance.OnItemDiscard(_discardTargetPlayer, -1)); // -1 = 새 아이템 폐기

        _p1AceConfirmButton.onClick.AddListener(() =>
        {
            ActivePlayer localPlayer = NetworkGameManager.Instance != null
                ? NetworkGameManager.Instance.LocalPlayer
                : ActivePlayer.Player1;

            if (NetworkGameManager.Instance != null)
                NetworkGameManager.Instance.RequestAceConfirmed(localPlayer);
            else
                GameManager.Instance.OnAceSelectionConfirmed(localPlayer);
        });

        _returnToLobbyButton.onClick.AddListener(OnReturnToLobbyClicked);
        _leaveRoomButton.onClick.AddListener(OnLeaveRoomClicked);

        for (int i = 0; i < _p1ItemButtons.Length; i++)
        {
            int slotIndex = i;
            _p1ItemButtons[i].onClick.AddListener(() =>
            {
                ActivePlayer localPlayer = NetworkGameManager.Instance != null
                    ? NetworkGameManager.Instance.LocalPlayer
                    : ActivePlayer.Player1;
                OnItemButtonClicked(localPlayer, slotIndex);
            });
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

        ActivePlayer localPlayer = NetworkGameManager.Instance != null
            ? NetworkGameManager.Instance.LocalPlayer
            : ActivePlayer.Player1;

        int myChips = localPlayer == ActivePlayer.Player1
            ? gm.Player1Chips
            : gm.Player2Chips;

        int opponentChips = localPlayer == ActivePlayer.Player1
            ? gm.Player2Chips
            : gm.Player1Chips;

        _p1ChipsText.text = $"My Chips: {myChips}";
        _p2ChipsText.text = $"Opponent Chips: {opponentChips}";
        _potText.text = $"POT: {gm.Pot}";

        UpdateButtons(gm);
    }

    private void UpdateButtons(GameManager gm)
    {
        ActivePlayer localPlayer = NetworkGameManager.Instance != null
            ? NetworkGameManager.Instance.LocalPlayer
            : ActivePlayer.None;

        bool isP1Turn = gm.CurrentPlayer == ActivePlayer.Player1;
        bool isP2Turn = gm.CurrentPlayer == ActivePlayer.Player2;

        if (localPlayer != ActivePlayer.None)
        {
            bool isMyTurn = gm.CurrentPlayer == localPlayer;
            isP1Turn = isMyTurn;
            isP2Turn = isMyTurn;
        }

        _p1HitButton.interactable = isP1Turn && gm.CanHit;
        _p1StandButton.interactable = isP1Turn && gm.CanStand;
        _p1RaiseButton.interactable = isP1Turn && gm.CanRaise;
        _p1CallButton.interactable = isP1Turn && gm.CanCall;
        _p1DieButton.interactable = isP1Turn && gm.CanDie;
    }

    // 카드 텍스트 갱신
    public void RefreshCards(
    string sharedCard,
    string p1Cards, int p1Total,
    string p2Cards, int p2Total)
    {
        ActivePlayer localPlayer = NetworkGameManager.Instance != null
            ? NetworkGameManager.Instance.LocalPlayer
            : ActivePlayer.Player1;

        string myCards, opponentCards;
        int myTotal, opponentTotal;

        if (localPlayer == ActivePlayer.Player1)
        {
            myCards = p1Cards; myTotal = p1Total;
            opponentCards = p2Cards; opponentTotal = p2Total;
        }
        else
        {
            myCards = p2Cards; myTotal = p2Total;
            opponentCards = p1Cards; opponentTotal = p1Total;
        }

        _sharedCardText.text = $"Shared Card: {sharedCard}";
        _p1CardsText.text = $"My Cards: {myCards}";
        _p1TotalText.text = $"My Total: {myTotal}";
        _p2CardsText.text = $"Opponent Cards: {opponentCards}";
        _p2TotalText.text = $"Opponent Total: {opponentTotal}";
    }

    private void OpenRaisePanel(ActivePlayer player)
    {
        Debug.Log($"[UI] 레이즈 패널 열기 시도");
        _p1RaiseAmount = 0;
        _p1RaiseAmountText.text = "0";
        _p1RaisePanel.SetActive(true);
    }

    private void CloseRaisePanel(ActivePlayer player)
    {
        _p1RaiseAmount = 0;
        _p1RaisePanel.SetActive(false);
    }

    private void AddRaiseAmount(ActivePlayer player, int amount)
    {
        int maxRaise = GameManager.Instance.MaxRaise; // MaxBet → MaxRaise

        _p1RaiseAmount = Mathf.Clamp(_p1RaiseAmount + amount, 0, maxRaise);
        _p1RaiseAmountText.text = _p1RaiseAmount.ToString();
    }

    public void ConfirmRaise(ActivePlayer player)
    {
        if (NetworkGameManager.Instance != null)
            NetworkGameManager.Instance.RequestRaise(_p1RaiseAmount);
        else
            GameManager.Instance.OnRaise(_p1RaiseAmount);

        CloseRaisePanel(player);
    }
    private void UpdatePhaseUI()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null) return;

        switch (gm.CurrentPhase)
        {
            case GamePhase.Draw:
                SetBettingButtonsVisible(false);
                HideAceSelectionPanel();
                break;

            case GamePhase.AceSelection:
                SetBettingButtonsVisible(false);
                ShowAceSelectionPanel();
                break;

            case GamePhase.CardReveal:
                SetBettingButtonsVisible(false);
                HideAceSelectionPanel();
                break;

            case GamePhase.Betting:
                SetBettingButtonsVisible(true);
                HideAceSelectionPanel();
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
    }
    private void ShowAceSelectionPanel()
    {
        if (_aceSelectionPanel.activeSelf) return; // 이미 열려있으면 중복 실행 방지

        _aceSelectionPanel.SetActive(true);

        GenerateAceButtons();
    }

    private void GenerateAceButtons()
    {
        ActivePlayer localPlayer = NetworkGameManager.Instance != null
            ? NetworkGameManager.Instance.LocalPlayer
            : ActivePlayer.Player1;

        GameManager gm = GameManager.Instance;

        foreach (Button btn in _p1AceButtons)
            Destroy(btn.gameObject);
        _p1AceButtons.Clear();

        var cards = localPlayer == ActivePlayer.Player1
            ? gm.GetPlayer1Cards()
            : gm.GetPlayer2Cards();

        for (int i = 0; i < cards.Count; i++)
        {
            if (cards[i].Card.Rank != Rank.Ace) continue;

            int index = i;

            GameObject btnObj = new GameObject($"Ace_{index}");
            btnObj.transform.SetParent(_p1AceButtonGroup, false);

            Button btn = btnObj.AddComponent<Button>();
            var image = btnObj.AddComponent<UnityEngine.UI.Image>();
            image.color = Color.white;

            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(btnObj.transform, false);
            var tmp = textObj.AddComponent<TMPro.TextMeshProUGUI>();
            tmp.text = "Ace: 1";
            tmp.fontSize = 20;
            tmp.alignment = TMPro.TextAlignmentOptions.Center;
            tmp.color = Color.black;

            var rect = btnObj.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(120, 50);

            var textRect = textObj.GetComponent<RectTransform>();
            textRect.sizeDelta = new Vector2(120, 50);

            bool isEleven = false;
            btn.onClick.AddListener(() =>
            {
                isEleven = !isEleven;
                tmp.text = $"Ace: {(isEleven ? 11 : 1)}";

                if (NetworkGameManager.Instance != null)
                    NetworkGameManager.Instance.RequestAceValueChanged(localPlayer, index, isEleven);
                else
                    GameManager.Instance.OnAceValueChanged(localPlayer, index, isEleven);
            });

            _p1AceButtons.Add(btn);
        }
    }

    public void HideAceSelectionPanel()
    {
        if (!_aceSelectionPanel.activeSelf) return;

        _aceSelectionPanel.SetActive(false);

        foreach (Button btn in _p1AceButtons) Destroy(btn.gameObject);
        _p1AceButtons.Clear();
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

    // 같은 방/상대 유지, 대기실로 복귀 (호스트가 두 플레이어의 씬을 함께 전환)
    private void OnReturnToLobbyClicked() => NetworkManager.Instance.ReturnToWaitingRoom();

    // 방을 완전히 나가 로비로. 상대는 기권 처리된다.
    private void OnLeaveRoomClicked() => NetworkManager.Instance.LeaveToLobby();

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

        ActivePlayer localPlayer = NetworkGameManager.Instance != null
            ? NetworkGameManager.Instance.LocalPlayer
            : ActivePlayer.Player1;

        ActivePlayer opponent = localPlayer == ActivePlayer.Player1
            ? ActivePlayer.Player2
            : ActivePlayer.Player1;

        var myItems = localPlayer == ActivePlayer.Player1
            ? gm.Player1Items
            : gm.Player2Items;

        var opponentItems = opponent == ActivePlayer.Player1
            ? gm.Player1Items
            : gm.Player2Items;

        // 내 슬롯: 사용 가능
        UpdatePlayerItemSlots(_p1ItemButtons, _p1ItemTexts, myItems, localPlayer, isMine: true);

        // 상대 슬롯: 보기만 가능
        UpdatePlayerItemSlots(_p2ItemButtons, _p2ItemTexts, opponentItems, opponent, isMine: false);
    }
    private void UpdatePlayerItemSlots(
        Button[] buttons,
        TextMeshProUGUI[] texts,
        IReadOnlyList<Item> items,
        ActivePlayer player,
        bool isMine)
    {
        GameManager gm = GameManager.Instance;
        bool isMyTurn = isMine && gm.CurrentPlayer == player;

        for (int i = 0; i < buttons.Length; i++)
        {
            if (i < items.Count)
            {
                texts[i].text = items[i].GetName();
                buttons[i].interactable = isMine && isMyTurn && CanUseItem(items[i].Type);
            }
            else
            {
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
    private void OnHitClicked()
    {
        if (NetworkGameManager.Instance != null)
            NetworkGameManager.Instance.RequestHit();
        else
            GameManager.Instance.OnHit(); // 로컬 테스트용
    }

    private void OnStandClicked()
    {
        if (NetworkGameManager.Instance != null)
            NetworkGameManager.Instance.RequestStand();
        else
            GameManager.Instance.OnStand(); // 로컬 테스트용
    }
    private void OnCallClicked()
    {
        if (NetworkGameManager.Instance != null)
            NetworkGameManager.Instance.RequestCall();
        else
            GameManager.Instance.OnCall();
    }

    private void OnDieClicked()
    {
        if (NetworkGameManager.Instance != null)
            NetworkGameManager.Instance.RequestDie();
        else
            GameManager.Instance.OnDie();
    }
}