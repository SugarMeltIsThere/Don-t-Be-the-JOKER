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

    [Header("Shared UI")]
    [SerializeField] private TextMeshProUGUI _sharedCardText;
    [SerializeField] private TextMeshProUGUI _potText;
    [SerializeField] private TextMeshProUGUI _myBetText;
    [SerializeField] private TextMeshProUGUI _opponentBetText;
    [SerializeField] private TextMeshProUGUI _myCharacterText;
    [SerializeField] private TextMeshProUGUI _opponentCharacterText;

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
    [SerializeField] private GameObject _opponentDiscardWaitingPanel;
    [SerializeField] private TextMeshProUGUI _opponentDiscardWaitingText;
    [SerializeField] private TextMeshProUGUI _newItemText;
    [SerializeField] private Button _discardButton1;
    [SerializeField] private Button _discardButton2;
    [SerializeField] private Button _discardButton3;
    [SerializeField] private Button _discardNewButton;

    // Game End UI는 더 이상 GameScene에 없음 (LobbyScene에서 결과 패널로 표시)

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

        _discardButton1.onClick.AddListener(() => RequestDiscard(0));
        _discardButton2.onClick.AddListener(() => RequestDiscard(1));
        _discardButton3.onClick.AddListener(() => RequestDiscard(2));
        _discardNewButton.onClick.AddListener(() => RequestDiscard(-1)); // -1 = 새 아이템 폐기

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

        // (Game End 관련 버튼은 더 이상 GameScene에 없음)

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

        int myBet = localPlayer == ActivePlayer.Player1
            ? gm.Player1TotalBet
            : gm.Player2TotalBet;

        int opponentBet = localPlayer == ActivePlayer.Player1
            ? gm.Player2TotalBet
            : gm.Player1TotalBet;

        // 인스펙터 미연결 대비 방어 코드 (예전 캐릭터 텍스트 NRE 사고 재발 방지)
        if (_myBetText != null)
            _myBetText.text = $"My Bet: {myBet}";

        if (_opponentBetText != null)
            _opponentBetText.text = $"Opponent Bet: {opponentBet}";

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
    // 캐릭터는 게임 중 안 바뀌므로 초기화 시점에 한 번만 호출됨
    // (매 프레임 갱신되는 RefreshCards와 달리 별도 진입점으로 분리)
    public void SetCharacterInfo(string myCharacter, string opponentCharacter)
    {
        // 인스펙터에 아직 안 연결된 필드가 있어도(NullReferenceException으로
        // 인해 이걸 호출한 GameManager.InitializeGame()의 나머지 로직,
        // 특히 StartNewRound()까지 통째로 못 도는 사고를 막기 위한 방어 코드
        if (_myCharacterText != null)
            _myCharacterText.text = $"My Character: {myCharacter}";

        if (_opponentCharacterText != null)
            _opponentCharacterText.text = $"Opponent Character: {opponentCharacter}";
    }

    public void RefreshCards(
    string sharedCard,
    string p1Cards, string p1Total,
    string p2Cards, string p2Total)
    {
        ActivePlayer localPlayer = NetworkGameManager.Instance != null
            ? NetworkGameManager.Instance.LocalPlayer
            : ActivePlayer.Player1;

        string myCards, opponentCards;
        string myTotal, opponentTotal;

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
                HideItemDiscardPanel();
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
                HandleGameEnd();
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

    // 게임 종료 감지 즉시(양쪽 클라이언트 각자) 결과를 계산해 NetworkManager에
    // 남겨두고 바로 로비 씬으로 전환. 패널은 GameScene이 아닌 LobbyScene에서 표시.
    private bool _gameEndHandled = false;

    // 탈주 처리처럼 GameManager가 다른 경로(예: 상대 퇴장)로 이미
    // 결과 텍스트/씬 전환을 직접 처리한 경우, 여기서 CurrentPhase가
    // GameEnd로 바뀐 걸 폴링으로 뒤늦게 감지해서 칩 기준으로 승패를
    // 다시 계산해 덮어쓰는 걸 막기 위해 미리 "처리됨"으로 표시해둠
    public void SuppressGameEndHandling()
    {
        _gameEndHandled = true;
    }

    private void HandleGameEnd()
    {
        if (_gameEndHandled) return;
        _gameEndHandled = true;

        ActivePlayer winner = GameManager.Instance.Player1Chips > 0
            ? ActivePlayer.Player1
            : ActivePlayer.Player2;

        ActivePlayer localPlayer = NetworkGameManager.Instance != null
            ? NetworkGameManager.Instance.LocalPlayer
            : ActivePlayer.Player1;

        string resultText = winner == localPlayer ? "You Win!" : "You Lose!";

        if (NetworkManager.Instance != null)
            NetworkManager.Instance.PendingGameResultText = resultText;

        Photon.Pun.PhotonNetwork.IsMessageQueueRunning = false;
        SceneManager.LoadScene("LobbyScene");
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
                if (NetworkGameManager.Instance != null)
                    NetworkGameManager.Instance.RequestUseFlash(player);
                else
                    gm.OnUseFlash(player); // 로컬 테스트용
                break;
            case ItemType.Bribe:
                if (NetworkGameManager.Instance != null)
                    NetworkGameManager.Instance.RequestUseBribe(player);
                else
                    gm.OnUseBribe(player); // 로컬 테스트용
                break;
        }
    }

    private void OnMagnifierClicked(ActivePlayer player)
    {
        if (NetworkGameManager.Instance != null)
        {
            NetworkGameManager.Instance.RequestUseMagnifier(player);
        }
        else
        {
            // 로컬 테스트용
            Card next = GameManager.Instance.OnUseMagnifier(player);
            ShowMagnifierResult(next);
        }
    }

    // 돋보기 결과 표시 (네트워크 경유 시 NetworkGameManager가 결과 수신 후 호출)
    public void ShowMagnifierResult(Card card)
    {
        if (card == null) return;

        // 돋보기로 본 카드를 팝업으로 표시
        // 지금은 로그로 대체
        Debug.Log($"[돋보기 UI] 다음 카드: {card}");
    }

    // 아이템 폐기 선택: 요청자 신원은 클릭한 사람의 LocalPlayer로 보내고,
    // 실제 대상 검증(_pendingItemPlayer와 일치 여부)은 마스터 쪽에서 수행
    private void RequestDiscard(int discardIndex)
    {
        ActivePlayer requester = NetworkGameManager.Instance != null
            ? NetworkGameManager.Instance.LocalPlayer
            : _discardTargetPlayer;

        if (NetworkGameManager.Instance != null)
            NetworkGameManager.Instance.RequestItemDiscard(requester, discardIndex);
        else
            GameManager.Instance.OnItemDiscard(requester, discardIndex); // 로컬 테스트용
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
        GameManager gm = GameManager.Instance;
        ActivePlayer pendingPlayer = gm.PendingItemPlayer;

        bool isMine = NetworkGameManager.Instance != null &&
                      NetworkGameManager.Instance.LocalPlayer == pendingPlayer;

        if (!isMine)
        {
            // 상대방 차례: 조작 가능한 패널은 아예 띄우지 않고,
            // 눌러도 아무 반응 없는 버튼들 대신 안내 텍스트만 표시
            if (_itemDiscardPanel.activeSelf)
                _itemDiscardPanel.SetActive(false);

            if (!_opponentDiscardWaitingPanel.activeSelf)
            {
                _opponentDiscardWaitingText.text = "Opponent is selecting an item...";
                _opponentDiscardWaitingPanel.SetActive(true);
            }
            return;
        }

        if (_opponentDiscardWaitingPanel.activeSelf)
            _opponentDiscardWaitingPanel.SetActive(false);

        if (_itemDiscardPanel.activeSelf) return;

        _discardTargetPlayer = pendingPlayer;

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
        _opponentDiscardWaitingPanel.SetActive(false);
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