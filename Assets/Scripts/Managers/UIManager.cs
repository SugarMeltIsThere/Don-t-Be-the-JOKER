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
    [SerializeField] private GameObject _cardImagePrefab;

    [Header("Ace Selection UI")]
    [SerializeField] private GameObject _aceSelectionPanel;
    [SerializeField] private Transform _aceSlotContainer;
    [SerializeField] private GameObject _aceSlotPrefab;
    [SerializeField] private Button _p1AceConfirmButton;

    [Header("Item Slots")]
    [SerializeField] private Button[] _myItemButtons;
    [SerializeField] private Image[] _opponentItemImages;
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

    private ActivePlayer _discardTargetPlayer = ActivePlayer.None;

    private int _p1RaiseAmount = 0;

    private List<AceSlotView> _aceSlots = new List<AceSlotView>();

    private GameManager _gm;

    // 싱글턴을 등록한다
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    // 버튼 동작을 연결하고 폐기 창을 닫는다
    private void Start()
    {
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
        _discardNewButton.onClick.AddListener(() => RequestDiscard(-1));

        HideItemDiscardPanel();

        if (_cardSpriteDatabase == null) Debug.LogWarning("[UI] Card Sprite Database가 인스펙터에 연결되지 않았습니다. 카드가 그려지지 않습니다.");
        if (_sharedCardImage == null) Debug.LogWarning("[UI] Shared Card Image가 인스펙터에 연결되지 않았습니다. 공유 카드가 갱신되지 않습니다.");

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

        Subscribe();
    }

    // 구독을 해제한다
    private void OnDestroy()
    {
        Unsubscribe();
    }

    // 게임 이벤트를 구독하고 구독 전에 지나간 상태를 한 번 그린다
    private void Subscribe()
    {
        _gm = GameManager.Instance;
        if (_gm == null) return;

        _gm.StateChanged += OnStateChanged;
        _gm.CharactersChanged += OnCharactersChanged;
        _gm.PeekShown += ShowMagnifierResult;
        _gm.GameEnded += OnGameEnded;

        OnCharactersChanged();
        OnStateChanged();
    }

    // 게임 이벤트 구독을 해제한다
    private void Unsubscribe()
    {
        if (_gm == null) return;

        _gm.StateChanged -= OnStateChanged;
        _gm.CharactersChanged -= OnCharactersChanged;
        _gm.PeekShown -= ShowMagnifierResult;
        _gm.GameEnded -= OnGameEnded;
        _gm = null;
    }

    // 상태가 바뀌면 화면 전체를 갱신한다
    private void OnStateChanged()
    {
        using (PerfMarkers.UIRefresh.Auto())
        {
            Refresh();
            UpdatePhaseUI();
            UpdateItemSlots();
        }
    }

    // 게임이 끝나면 상대 이탈이 아닌 경우에만 결과를 처리한다
    private void OnGameEnded(GameEndReason reason)
    {
        if (reason == GameEndReason.OpponentLeft) return;
        HandleGameEnd();
    }
    // 칩과 팟과 베팅 표시를 갱신한다
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

        if (_myBetText != null)
            _myBetText.text = myBet.ToString();

        if (_opponentBetText != null)
            _opponentBetText.text = opponentBet.ToString();

        ActivePlayer opponent = localPlayer == ActivePlayer.Player1
            ? ActivePlayer.Player2
            : ActivePlayer.Player1;

        _p1TotalText.text = gm.GetTotalText(localPlayer);
        _p2TotalText.text = gm.GetTotalText(opponent);

        RefreshCardImages(gm, localPlayer);

        UpdateButtons(gm);
    }

    // 공유 카드와 양쪽 손패를 그린다
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

    // 카드 개수에 맞춰 카드 이미지를 만들고 채운다
    private void RenderCardRow(Transform container, IReadOnlyList<CardSlot> cards, bool isMine)
    {
        if (container == null || cards == null) return;

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

    // 지금 가능한 행동만 버튼을 켠다
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

    // 양쪽 캐릭터 이름을 표시한다
    private void OnCharactersChanged()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null) return;

        ActivePlayer localPlayer = NetworkGameManager.Instance != null
            ? NetworkGameManager.Instance.LocalPlayer
            : ActivePlayer.Player1;

        ActivePlayer opponent = localPlayer == ActivePlayer.Player1
            ? ActivePlayer.Player2
            : ActivePlayer.Player1;

        if (_myCharacterText != null)
            _myCharacterText.text = gm.GetCharacterName(localPlayer).ToUpperInvariant();

        if (_opponentCharacterText != null)
            _opponentCharacterText.text = gm.GetCharacterName(opponent).ToUpperInvariant();
    }

    // 레이즈 패널을 연다
    private void OpenRaisePanel(ActivePlayer player)
    {
        Debug.Log($"[UI] 레이즈 패널 열기 시도");
        _p1RaiseAmount = 0;
        _p1RaiseAmountText.text = "0";
        _p1RaisePanel.SetActive(true);
    }

    // 레이즈 패널을 닫는다
    private void CloseRaisePanel(ActivePlayer player)
    {
        _p1RaiseAmount = 0;
        _p1RaisePanel.SetActive(false);
    }

    // 레이즈 금액을 더하거나 뺀다
    private void AddRaiseAmount(ActivePlayer player, int amount)
    {
        int maxRaise = GameManager.Instance.MaxRaise;

        _p1RaiseAmount = Mathf.Clamp(_p1RaiseAmount + amount, 0, maxRaise);
        _p1RaiseAmountText.text = _p1RaiseAmount.ToString();
    }

    // 레이즈를 확정한다
    public void ConfirmRaise(ActivePlayer player)
    {
        if (NetworkGameManager.Instance != null)
            NetworkGameManager.Instance.RequestRaise(_p1RaiseAmount);
        else
            GameManager.Instance.OnRaise(_p1RaiseAmount);

        CloseRaisePanel(player);
    }
    // 단계에 맞는 패널을 보여준다
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
            case GamePhase.ItemDiscard:
                ShowItemDiscardPanel();
                break;
        }
    }
    // 베팅 버튼을 보이거나 숨긴다
    private void SetBettingButtonsVisible(bool visible)
    {
        _p1RaiseButton.gameObject.SetActive(visible);
        _p1CallButton.gameObject.SetActive(visible);
        _p1DieButton.gameObject.SetActive(visible);
    }
    // 에이스 선택 패널을 보여준다
    private void ShowAceSelectionPanel()
    {
        if (_aceSelectionPanel.activeSelf) return;

        _aceSelectionPanel.SetActive(true);

        GenerateAceButtons();
    }

    // 에이스마다 선택 버튼을 만든다
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
            Card aceCard = cards[i].Card;

            GameObject slotObj = Instantiate(_aceSlotPrefab, _aceSlotContainer);

            RectTransform slotRect = slotObj.GetComponent<RectTransform>();
            if (slotRect != null)
            {
                slotRect.localScale = Vector3.one;
                slotRect.localRotation = Quaternion.identity;
                slotRect.anchoredPosition3D = Vector3.zero;
            }

            AceSlotView view = slotObj.GetComponent<AceSlotView>();

            if (view == null)
            {
                Debug.LogError("[에이스 선택] _aceSlotPrefab에 AceSlotView 컴포넌트가 없음");
                continue;
            }

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

    // 에이스 선택 패널을 숨긴다
    public void HideAceSelectionPanel()
    {
        if (!_aceSelectionPanel.activeSelf) return;

        _aceSelectionPanel.SetActive(false);

        foreach (AceSlotView slot in _aceSlots) Destroy(slot.gameObject);
        _aceSlots.Clear();
    }

    // 결과 문구를 남기고 대기실로 돌아간다
    private void HandleGameEnd()
    {
        ActivePlayer winner = GameManager.Instance.Player1Chips > 0
            ? ActivePlayer.Player1
            : ActivePlayer.Player2;

        ActivePlayer localPlayer = NetworkGameManager.Instance != null
            ? NetworkGameManager.Instance.LocalPlayer
            : ActivePlayer.Player1;

        string resultText = winner == localPlayer ? "YOU WIN!" : "YOU LOSE!";

        if (NetworkManager.Instance != null)
            NetworkManager.Instance.PendingGameResultText = resultText;

        if (NetworkManager.Instance != null) NetworkManager.Instance.ReturnToWaitingRoom();
        else SceneManager.LoadScene("LobbyScene");
    }
    // 누른 아이템을 사용한다
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
                    gm.OnUseFlash(player);
                break;
            case ItemType.Bribe:
                if (NetworkGameManager.Instance != null)
                    NetworkGameManager.Instance.RequestUseBribe(player);
                else
                    gm.OnUseBribe(player);
                break;
        }
    }

    // 돋보기를 사용한다
    private void OnMagnifierClicked(ActivePlayer player)
    {
        if (NetworkGameManager.Instance != null)
            NetworkGameManager.Instance.RequestUseMagnifier(player);
        else
            GameManager.Instance.OnUseMagnifier(player);
    }

    // 돋보기로 본 카드를 기록한다
    private void ShowMagnifierResult(Card card)
    {
        if (card == null) return;

        Debug.Log($"[돋보기 UI] 다음 카드: {card}");
    }

    // 폐기할 아이템을 요청한다
    private void RequestDiscard(int discardIndex)
    {
        ActivePlayer requester = NetworkGameManager.Instance != null
            ? NetworkGameManager.Instance.LocalPlayer
            : _discardTargetPlayer;

        if (NetworkGameManager.Instance != null)
            NetworkGameManager.Instance.RequestItemDiscard(requester, discardIndex);
        else
            GameManager.Instance.OnItemDiscard(requester, discardIndex);
    }

    // 아이템 슬롯 표시를 갱신한다
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

    // 아이템 종류에 맞는 이미지를 반환한다
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

    // 지금 단계에서 쓸 수 있는 아이템인지 확인한다
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

    // 아이템 폐기 패널을 보여준다
    private void ShowItemDiscardPanel()
    {
        GameManager gm = GameManager.Instance;
        ActivePlayer pendingPlayer = gm.PendingItemPlayer;

        bool isMine = NetworkGameManager.Instance != null &&
                      NetworkGameManager.Instance.LocalPlayer == pendingPlayer;

        if (!isMine)
        {
            if (_itemDiscardPanel.activeSelf)
                _itemDiscardPanel.SetActive(false);

            if (_opponentDiscardWaitingPanel != null && !_opponentDiscardWaitingPanel.activeSelf)
            {
                if (_opponentDiscardWaitingText != null)
                    _opponentDiscardWaitingText.text = "OPPONENT IS SELECTING AN ITEM...";
                _opponentDiscardWaitingPanel.SetActive(true);
            }
            return;
        }

        if (_opponentDiscardWaitingPanel != null && _opponentDiscardWaitingPanel.activeSelf)
            _opponentDiscardWaitingPanel.SetActive(false);

        if (_itemDiscardPanel.activeSelf) return;

        _discardTargetPlayer = pendingPlayer;

        var items = _discardTargetPlayer == ActivePlayer.Player1
            ? gm.Player1Items
            : gm.Player2Items;

        int slots = _discardTargetPlayer == ActivePlayer.Player1
            ? gm.Player1ItemSlots
            : gm.Player2ItemSlots;

        _newItemText.text = gm.PendingItem?.GetName().ToUpperInvariant();

        _discardButton1.GetComponentInChildren<TextMeshProUGUI>().text
            = items.Count > 0 ? items[0].GetName().ToUpperInvariant() : "-";
        _discardButton2.GetComponentInChildren<TextMeshProUGUI>().text
            = items.Count > 1 ? items[1].GetName().ToUpperInvariant() : "-";

        if (slots >= 3)
        {
            _discardButton3.gameObject.SetActive(true);
            _discardButton3.GetComponentInChildren<TextMeshProUGUI>().text
                = items.Count > 2 ? items[2].GetName().ToUpperInvariant() : "-";
        }
        else
        {
            _discardButton3.gameObject.SetActive(false);
        }

        _discardNewButton.GetComponentInChildren<TextMeshProUGUI>().text
            = $"{gm.PendingItem?.GetName().ToUpperInvariant()} (NEW)";

        _itemDiscardPanel.SetActive(true);
    }

    // 아이템 폐기 패널을 숨긴다
    public void HideItemDiscardPanel()
    {
        if (_itemDiscardPanel != null) _itemDiscardPanel.SetActive(false);
        if (_opponentDiscardWaitingPanel != null) _opponentDiscardWaitingPanel.SetActive(false);
    }
    // 카드 받기를 요청한다
    private void OnHitClicked()
    {
        if (NetworkGameManager.Instance != null)
            NetworkGameManager.Instance.RequestHit();
        else
            GameManager.Instance.OnHit();
    }

    // 스탠드를 요청한다
    private void OnStandClicked()
    {
        if (NetworkGameManager.Instance != null)
            NetworkGameManager.Instance.RequestStand();
        else
            GameManager.Instance.OnStand();
    }
    // 콜을 요청한다
    private void OnCallClicked()
    {
        CloseRaisePanel(ActivePlayer.Player1);

        if (NetworkGameManager.Instance != null)
            NetworkGameManager.Instance.RequestCall();
        else
            GameManager.Instance.OnCall();
    }

    // 다이를 요청한다
    private void OnDieClicked()
    {
        CloseRaisePanel(ActivePlayer.Player1);

        if (NetworkGameManager.Instance != null)
            NetworkGameManager.Instance.RequestDie();
        else
            GameManager.Instance.OnDie();
    }
}