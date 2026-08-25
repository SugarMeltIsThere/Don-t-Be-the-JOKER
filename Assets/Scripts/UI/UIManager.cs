using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("P1 UI")]
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
    [SerializeField] private TextMeshProUGUI _p2ChipsText;
    [SerializeField] private TextMeshProUGUI _p2TotalText;

    [Header("Shared UI")]
    [SerializeField] private TextMeshProUGUI _potText;
    [SerializeField] private TextMeshProUGUI _myBetText;
    [SerializeField] private TextMeshProUGUI _opponentBetText;
    [SerializeField] private TextMeshProUGUI _myCharacterText;
    [SerializeField] private TextMeshProUGUI _opponentCharacterText;

    [Header("Card Sprites")]
    [SerializeField] private CardSpriteDatabase _cardSpriteDatabase;
    [SerializeField] private Image _sharedCardImage;
    [SerializeField] private Transform _myCardsContainer;
    [SerializeField] private Transform _opponentCardsContainer;
    [SerializeField] private GameObject _cardImagePrefab; // Image 컴포넌트가 붙은 프리팹

    [Header("Ace Selection UI")]
    [SerializeField] private GameObject _aceSelectionPanel;
    [SerializeField] private Transform _aceSlotContainer;
    [SerializeField] private GameObject _aceSlotPrefab; // AceSlotView 컴포넌트 포함 (카드 이미지 + 토글 버튼)
    [SerializeField] private Button _p1AceConfirmButton;

    [Header("Item Slots")]
    [SerializeField] private Button[] _myItemButtons; // 최대 슬롯 수(3)만큼 미리 만들어두고 필요한 만큼만 활성화
    [SerializeField] private Image[] _opponentItemImages; // 마찬가지로 미리 만들어두는 풀
    [SerializeField] private Sprite _magnifierSprite;
    [SerializeField] private Sprite _flashSprite;
    [SerializeField] private Sprite _bribeSprite;

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

    private List<AceSlotView> _aceSlots = new List<AceSlotView>();

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

        // 아이템 슬롯은 고정 개수(풀)라, 클릭 리스너를 여기서 한 번만
        // 등록해두면 됨 - 매 프레임 다시 등록할 필요 없음
        for (int i = 0; i < _myItemButtons.Length; i++)
        {
            int slotIndex = i;
            _myItemButtons[i].onClick.AddListener(() =>
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

        _p1ChipsText.text = myChips.ToString();
        _p2ChipsText.text = opponentChips.ToString();
        _potText.text = gm.Pot.ToString();

        int myBet = localPlayer == ActivePlayer.Player1
            ? gm.Player1TotalBet
            : gm.Player2TotalBet;

        int opponentBet = localPlayer == ActivePlayer.Player1
            ? gm.Player2TotalBet
            : gm.Player1TotalBet;

        // 인스펙터 미연결 대비 방어 코드 (예전 캐릭터 텍스트 NRE 사고 재발 방지)
        if (_myBetText != null)
            _myBetText.text = myBet.ToString();

        if (_opponentBetText != null)
            _opponentBetText.text = opponentBet.ToString();

        RefreshCardImages(gm, localPlayer);

        UpdateButtons(gm);
    }

    // 카드 텍스트 대신 실제 스프라이트로 표시. 다른 폴링 로직들과 마찬가지로
    // 매 프레임 GameManager의 현재 상태를 그대로 읽어와 그리기 때문에,
    // 별도의 푸시 갱신 없이도 항상 최신 상태를 반영함
    private void RefreshCardImages(GameManager gm, ActivePlayer localPlayer)
    {
        if (_cardSpriteDatabase == null) return;

        if (_sharedCardImage != null && gm.SharedCard != null)
            _sharedCardImage.sprite = _cardSpriteDatabase.GetSprite(gm.SharedCard);

        var myCards = localPlayer == ActivePlayer.Player1
            ? gm.GetPlayer1Cards()
            : gm.GetPlayer2Cards();

        var opponentCards = localPlayer == ActivePlayer.Player1
            ? gm.GetPlayer2Cards()
            : gm.GetPlayer1Cards();

        RenderCardRow(_myCardsContainer, myCards, isMine: true);
        RenderCardRow(_opponentCardsContainer, opponentCards, isMine: false);
    }

    // 컨테이너 자식(카드 이미지) 개수를 실제 카드 수에 맞춰 늘리거나 줄이고,
    // 각 슬롯에 맞는 스프라이트(공개/숨김)를 채워넣음
    private void RenderCardRow(Transform container, IReadOnlyList<CardSlot> cards, bool isMine)
    {
        if (container == null || cards == null) return;

        // Destroy()는 이번 프레임 끝에 지워지도록 "예약"만 할 뿐, 호출 직후에도
        // childCount가 그대로라서 while(container.childCount > cards.Count) 형태로
        // 짜면 조건이 절대 거짓이 안 되어 무한 루프에 빠짐(유니티 자체가 멈춤).
        // 그래서 개수 차이를 미리 한 번만 계산해두고, 그 횟수만큼만 반복함
        int currentCount = container.childCount;
        int targetCount = cards.Count;

        for (int i = currentCount; i < targetCount; i++)
            Instantiate(_cardImagePrefab, container);

        for (int i = currentCount - 1; i >= targetCount; i--)
            Destroy(container.GetChild(i).gameObject);

        for (int i = 0; i < cards.Count; i++)
        {
            Image img = container.GetChild(i).GetComponent<Image>();
            if (img == null) continue;

            CardSlot slot = cards[i];
            bool showBack = slot.IsHidden && !isMine;

            img.sprite = showBack
                ? _cardSpriteDatabase.CardBackSprite
                : _cardSpriteDatabase.GetSprite(slot.Card);
        }
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
            _myCharacterText.text = myCharacter;

        if (_opponentCharacterText != null)
            _opponentCharacterText.text = opponentCharacter;
    }

    // 카드 자체는 이제 RefreshCardImages()가 매 프레임 폴링으로 그리므로,
    // 여기서는 합계 텍스트만 처리 (합계는 숫자라 이미지가 아닌 텍스트가 적합)
    public void RefreshCards(string p1Total, string p2Total)
    {
        ActivePlayer localPlayer = NetworkGameManager.Instance != null
            ? NetworkGameManager.Instance.LocalPlayer
            : ActivePlayer.Player1;

        string myTotal, opponentTotal;

        if (localPlayer == ActivePlayer.Player1)
        {
            myTotal = p1Total;
            opponentTotal = p2Total;
        }
        else
        {
            myTotal = p2Total;
            opponentTotal = p1Total;
        }

        _p1TotalText.text = myTotal;
        _p2TotalText.text = opponentTotal;
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

        foreach (AceSlotView slot in _aceSlots)
            Destroy(slot.gameObject);
        _aceSlots.Clear();

        var cards = localPlayer == ActivePlayer.Player1
            ? gm.GetPlayer1Cards()
            : gm.GetPlayer2Cards();

        for (int i = 0; i < cards.Count; i++)
        {
            if (cards[i].Card.Rank != Rank.Ace) continue;

            int index = i;
            Card aceCard = cards[i].Card; // 실제로 들고 있는 그 에이스(하트/스페이드 등)

            GameObject slotObj = Instantiate(_aceSlotPrefab, _aceSlotContainer);
            AceSlotView view = slotObj.GetComponent<AceSlotView>();

            if (view == null)
            {
                Debug.LogError("[에이스 선택] _aceSlotPrefab에 AceSlotView 컴포넌트가 없음");
                continue;
            }

            // 내가 들고 있는 카드 그대로 표시 (예: 하트 에이스와 스페이드 에이스를
            // 같이 들고 있으면 각 슬롯에 그 두 그림이 그대로 나옴)
            if (view.CardImage != null && _cardSpriteDatabase != null)
                view.CardImage.sprite = _cardSpriteDatabase.GetSprite(aceCard);

            TextMeshProUGUI tmp = view.ToggleButton.GetComponentInChildren<TextMeshProUGUI>();
            if (tmp != null)
                tmp.text = "1";

            bool isEleven = false;
            view.ToggleButton.onClick.AddListener(() =>
            {
                isEleven = !isEleven;
                if (tmp != null)
                    tmp.text = (isEleven ? 11 : 1).ToString();

                if (NetworkGameManager.Instance != null)
                    NetworkGameManager.Instance.RequestAceValueChanged(localPlayer, index, isEleven);
                else
                    GameManager.Instance.OnAceValueChanged(localPlayer, index, isEleven);
            });

            _aceSlots.Add(view);
        }
    }

    public void HideAceSelectionPanel()
    {
        if (!_aceSelectionPanel.activeSelf) return;

        _aceSelectionPanel.SetActive(false);

        foreach (AceSlotView slot in _aceSlots) Destroy(slot.gameObject);
        _aceSlots.Clear();
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

        bool isMyTurn = gm.CurrentPlayer == localPlayer;

        // 내 아이템: 버튼 풀 - 있는 만큼만 켜고 스프라이트/상호작용 갱신
        for (int i = 0; i < _myItemButtons.Length; i++)
        {
            Button button = _myItemButtons[i];

            if (i < myItems.Count)
            {
                ItemType type = myItems[i].Type;

                button.gameObject.SetActive(true);
                button.interactable = isMyTurn && CanUseItem(type);

                Image image = button.GetComponent<Image>();
                if (image != null)
                    image.sprite = GetItemSprite(type);
            }
            else
            {
                button.gameObject.SetActive(false);
            }
        }

        // 상대 아이템: 이미지 풀 - 클릭 리스너 자체가 없음
        for (int i = 0; i < _opponentItemImages.Length; i++)
        {
            Image image = _opponentItemImages[i];

            if (i < opponentItems.Count)
            {
                image.gameObject.SetActive(true);
                image.sprite = GetItemSprite(opponentItems[i].Type);
            }
            else
            {
                image.gameObject.SetActive(false);
            }
        }
    }

    private Sprite GetItemSprite(ItemType type)
    {
        return type switch
        {
            ItemType.Magnifier => _magnifierSprite,
            ItemType.Flash => _flashSprite,
            ItemType.Bribe => _bribeSprite,
            _ => null
        };
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

        _newItemText.text = gm.PendingItem?.GetName();

        _discardButton1.GetComponentInChildren<TextMeshProUGUI>().text
            = items.Count > 0 ? items[0].GetName() : "-";
        _discardButton2.GetComponentInChildren<TextMeshProUGUI>().text
            = items.Count > 1 ? items[1].GetName() : "-";

        // 킹 캐릭터면 3번째 슬롯 버튼 표시
        if (slots >= 3)
        {
            _discardButton3.gameObject.SetActive(true);
            _discardButton3.GetComponentInChildren<TextMeshProUGUI>().text
                = items.Count > 2 ? items[2].GetName() : "-";
        }
        else
        {
            _discardButton3.gameObject.SetActive(false);
        }

        _discardNewButton.GetComponentInChildren<TextMeshProUGUI>().text
            = $"{gm.PendingItem?.GetName()} (New)";

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
        // 레이즈 패널을 연 채로 콜/다이를 누르면 패널이 안 닫히던 버그 수정
        CloseRaisePanel(ActivePlayer.Player1);

        if (NetworkGameManager.Instance != null)
            NetworkGameManager.Instance.RequestCall();
        else
            GameManager.Instance.OnCall();
    }

    private void OnDieClicked()
    {
        // 레이즈 패널을 연 채로 콜/다이를 누르면 패널이 안 닫히던 버그 수정
        CloseRaisePanel(ActivePlayer.Player1);

        if (NetworkGameManager.Instance != null)
            NetworkGameManager.Instance.RequestDie();
        else
            GameManager.Instance.OnDie();
    }
}