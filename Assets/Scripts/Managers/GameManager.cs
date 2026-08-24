using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public enum GamePhase
{
    FirstTurnDetermination,
    Draw,
    AceSelection,
    CardReveal,
    Betting,
    ItemDiscard,  // 추가
    RoundEnd,
    GameEnd
}

public enum ActivePlayer
{
    Player1,
    Player2,
    None
}

public enum BettingAction
{
    Raise,
    Call,
    Die
}

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    // ===================================================================
    // 필드
    // ===================================================================

    private Deck _deck;
    private PlayerHand _player1Hand;
    private PlayerHand _player2Hand;
    private Character _player1Character = null;
    private Character _player2Character = null;
    private Card _sharedCard;

    private int _player1Chips = 40;
    private int _player2Chips = 40;
    private int _pot = 0;
    private int _player1TotalBet = 0;
    private int _player2TotalBet = 0;
    private int _player1LoseStreak = 0;
    private int _player2LoseStreak = 0;

    private const int DefaultItemSlots = 2;

    // 각 플레이어 슬롯 크기
    private int _player1ItemSlots = DefaultItemSlots;
    private int _player2ItemSlots = DefaultItemSlots;

    private Item _pendingItem = null;
    private ActivePlayer _pendingItemPlayer = ActivePlayer.None;
    private bool _isFirstRound = true;
    private ActivePlayer _lastLoser = ActivePlayer.None;

    private List<Item> _player1Items = new List<Item>();
    private List<Item> _player2Items = new List<Item>();

    private bool _isFirstBet = true;
    private ActivePlayer _bettingFirst;
    private ActivePlayer _diedPlayer = ActivePlayer.None;

    private System.Random _random = new System.Random();

    private const float RoundEndRevealDuration = 2.5f;

    // ===================================================================
    // 프로퍼티 (UI/외부 노출용)
    // ===================================================================

    public GamePhase CurrentPhase { get; private set; }
    public ActivePlayer CurrentPlayer { get; private set; }

    public IReadOnlyList<CardSlot> GetPlayer1Cards() => _player1Hand?.Cards;
    public IReadOnlyList<CardSlot> GetPlayer2Cards() => _player2Hand?.Cards;

    public Item PendingItem => _pendingItem;
    public ActivePlayer PendingItemPlayer => _pendingItemPlayer;

    public int Player1ItemSlots => _player1ItemSlots;
    public int Player2ItemSlots => _player2ItemSlots;
    public int Player1TotalBet => _player1TotalBet;
    public int Player2TotalBet => _player2TotalBet;
    public CharacterType? Player1Character => _player1Character?.Type;
    public CharacterType? Player2Character => _player2Character?.Type;

    public IReadOnlyList<Item> Player1Items => _player1Items;
    public IReadOnlyList<Item> Player2Items => _player2Items;

    public int Player1Chips => _player1Chips;
    public int Player2Chips => _player2Chips;
    public int Pot => _pot;
    public bool IsFirstBet => _isFirstBet;
    public Card SharedCard => _sharedCard;

    public int CallAmount => CurrentPlayer == ActivePlayer.Player1
        ? _player2TotalBet - _player1TotalBet
        : _player1TotalBet - _player2TotalBet;

    public int MaxRaise
    {
        get
        {
            int myChips = CurrentPlayer == ActivePlayer.Player1
                ? _player1Chips
                : _player2Chips;

            int opponentChips = CurrentPlayer == ActivePlayer.Player1
                ? _player2Chips
                : _player1Chips;

            // 레이즈 금액만큼이 곧 상대의 다음 콜 금액이 되므로,
            // 상대가 콜 가능한 범위(상대 보유 칩)를 넘지 않도록 제한
            int myLimit = myChips - CallAmount;
            return Mathf.Max(0, Mathf.Min(myLimit, opponentChips));
        }
    }

    public bool CanCall =>
        !_isFirstBet &&
        CurrentPhase == GamePhase.Betting &&
        CurrentPlayer != ActivePlayer.None &&
        CallAmount > 0;

    public bool CanRaise =>
        CurrentPhase == GamePhase.Betting &&
        CurrentPlayer != ActivePlayer.None &&
        MaxRaise > 0;

    public bool CanHit =>
        CurrentPhase == GamePhase.Draw &&
        CurrentPlayer != ActivePlayer.None &&
        GetCurrentHand() != null &&
        !GetCurrentHand().MustStand;

    public bool CanStand =>
        CurrentPhase == GamePhase.Draw &&
        CurrentPlayer != ActivePlayer.None &&
        GetCurrentHand() != null;

    public bool CanDie =>
        CurrentPhase == GamePhase.Betting &&
        CurrentPlayer != ActivePlayer.None;

    // ===================================================================
    // Unity 라이프사이클
    // ===================================================================

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
        // NetworkGameManager가 없을 때만 로컬로 초기화 (테스트용)
        if (NetworkGameManager.Instance == null)
            InitializeGame(CharacterType.King, CharacterType.Queen);
    }

    // ===================================================================
    // 게임 초기화
    // ===================================================================

    public void InitializeGame(CharacterType p1Type, CharacterType p2Type)
    {
        _player1Chips = 40;
        _player2Chips = 40;
        _pot = 0;
        _player1LoseStreak = 0;
        _player2LoseStreak = 0;
        _player1Items.Clear();
        _player2Items.Clear();
        _player1ItemSlots = DefaultItemSlots;
        _player2ItemSlots = DefaultItemSlots;
        _isFirstRound = true;
        _lastLoser = ActivePlayer.None;

        SetCharacter(ActivePlayer.Player1, p1Type);
        SetCharacter(ActivePlayer.Player2, p2Type);

        _deck = new Deck(includeJoker: false);
        _deck.Shuffle();

        ApplyCharacterAbilities();
        RefreshCharacterUI();
        StartNewRound();
    }

    // 게스트 전용 초기화
    public void InitializeForGuest(Card sharedCard, CharacterType p1Type, CharacterType p2Type)
    {
        _player1Chips = 40;
        _player2Chips = 40;
        _pot = 0;
        _player1LoseStreak = 0;
        _player2LoseStreak = 0;
        _player1Items.Clear();
        _player2Items.Clear();
        _player1ItemSlots = DefaultItemSlots;
        _player2ItemSlots = DefaultItemSlots;
        _isFirstRound = true;
        _lastLoser = ActivePlayer.None;

        SetCharacter(ActivePlayer.Player1, p1Type);
        SetCharacter(ActivePlayer.Player2, p2Type);
        ApplyCharacterAbilities();
        RefreshCharacterUI();

        _sharedCard = sharedCard;
        _player1Hand = new PlayerHand(_sharedCard);
        _player2Hand = new PlayerHand(_sharedCard);

        CurrentPhase = GamePhase.Draw;
        RefreshUI();
    }

    // 캐릭터는 게임 중 안 바뀌므로 초기화 시점에 한 번만 표시.
    // "내 캐릭터/상대 캐릭터" 형태로 로컬 플레이어 기준으로 뒤집어서 보여줌
    private void RefreshCharacterUI()
    {
        if (UIManager.Instance == null) return;

        ActivePlayer localPlayer = NetworkGameManager.Instance != null
            ? NetworkGameManager.Instance.LocalPlayer
            : ActivePlayer.Player1;

        string myName = (localPlayer == ActivePlayer.Player1 ? _player1Character : _player2Character)?.GetName() ?? "-";
        string opponentName = (localPlayer == ActivePlayer.Player1 ? _player2Character : _player1Character)?.GetName() ?? "-";

        UIManager.Instance.SetCharacterInfo(myName, opponentName);
    }

    private void ApplyCharacterAbilities()
    {
        _player1Character?.OnGameStart(this, ActivePlayer.Player1);
        _player2Character?.OnGameStart(this, ActivePlayer.Player2);
    }

    public void SetCharacter(ActivePlayer player, CharacterType type)
    {
        Character character = type switch
        {
            CharacterType.King => new KingCharacter(),
            CharacterType.Queen => new QueenCharacter(),
            CharacterType.Jack => new JackCharacter(),
            CharacterType.Joker => new JokerCharacter(),
            _ => null
        };

        if (player == ActivePlayer.Player1)
            _player1Character = character;
        else
            _player2Character = character;

        Debug.Log($"[캐릭터 선택] {player} / {type}");
    }

    // 킹 능력 - 아이템 슬롯 증가
    public void SetItemSlots(ActivePlayer player, int slots)
    {
        if (player == ActivePlayer.Player1)
            _player1ItemSlots = slots;
        else
            _player2ItemSlots = slots;
    }

    // 퀸 능력 - 칩 추가
    public void AddChips(ActivePlayer player, int amount)
    {
        if (player == ActivePlayer.Player1)
            _player1Chips += amount;
        else
            _player2Chips += amount;
    }

    // 조커 능력 - 덱에 조커 삽입
    public void InsertJokerCard()
    {
        // 실제 드로우는 마스터의 덱에서만 일어나서, 게스트 쪽 _deck은
        // InitializeForGuest()에서 애초에 생성되지 않음.
        // 게스트가 이 능력을 실행해도 실질적 의미가 없으므로 안전하게 스킵
        if (_deck == null) return;
        _deck.InsertJoker();
    }

    // ===================================================================
    // 라운드 흐름
    // ===================================================================

    public void StartNewRound()
    {
        // 덱이 부족하면 리셔플
        if (_deck == null || _deck.Count < 10)
        {
            _deck = new Deck(includeJoker: false);
            _deck.Shuffle();
            Debug.Log("[덱 리셔플]");
        }

        do
        {
            _sharedCard = _deck.Draw();
        } while (_sharedCard.Rank == Rank.Joker);
        Debug.Log($"[공유카드] {_sharedCard}");

        _player1Hand = new PlayerHand(_sharedCard);
        _player2Hand = new PlayerHand(_sharedCard);

        _player1TotalBet = 0;
        _player2TotalBet = 0;
        _isFirstBet = true;
        _diedPlayer = ActivePlayer.None;

        CurrentPhase = GamePhase.FirstTurnDetermination;
        CurrentPlayer = ActivePlayer.None;
        DetermineFirstTurn();

        _player1Hand.AddCard(_deck.Draw());
        _player2Hand.AddCard(_deck.Draw());

        Debug.Log($"[P1 패] {_player1Hand.Cards[0].Card} / 합계: {_player1Hand.GetTotal()}");
        Debug.Log($"[P2 패] {_player2Hand.Cards[0].Card} / 합계: {_player2Hand.GetTotal()}");

        CurrentPhase = GamePhase.Draw;
        Debug.Log($"[드로우 단계 시작] 선공: {CurrentPlayer}");

        _isFirstRound = false;
        if (NetworkGameManager.Instance != null)
        {
            NetworkGameManager.Instance.BroadcastNextRound(
                (int)_sharedCard.Rank,
                (int)_sharedCard.Suit,
                (int)CurrentPlayer,
                _player1Chips,
                _player2Chips,
                _pot);

            // 개인카드도 각자에게 전송
            NetworkGameManager.Instance.SyncPrivateCards();
        }

        RefreshUI();
    }

    private void DetermineFirstTurn()
    {
        if (!_isFirstRound && _lastLoser != ActivePlayer.None)
        {
            CurrentPlayer = _lastLoser;
            _bettingFirst = _lastLoser;
            Debug.Log($"[선공결정] 이전 라운드 패자 → {CurrentPlayer}");
            return;
        }

        // 첫 라운드만 카드 뽑기
        List<Card> turnDeck = new List<Card>(10);
        for (Rank rank = Rank.Ace; rank <= Rank.Ten; rank++)
            turnDeck.Add(new Card(rank, Suit.Spade));

        int n = turnDeck.Count;
        while (n > 1)
        {
            n--;
            int k = _random.Next(n + 1);
            Card temp = turnDeck[k];
            turnDeck[k] = turnDeck[n];
            turnDeck[n] = temp;
        }

        Card p1Card = turnDeck[0];
        Card p2Card = turnDeck[1];

        CurrentPlayer = p1Card.Rank > p2Card.Rank
            ? ActivePlayer.Player1
            : ActivePlayer.Player2;

        _bettingFirst = CurrentPlayer;
        Debug.Log($"[선공결정] P1: {p1Card} / P2: {p2Card} → {CurrentPlayer}");
    }

    private PlayerHand GetCurrentHand()
    {
        if (CurrentPlayer == ActivePlayer.Player1) return _player1Hand;
        if (CurrentPlayer == ActivePlayer.Player2) return _player2Hand;
        return null;
    }

    // ===================================================================
    // 드로우 단계 (히트 / 스탠드)
    // ===================================================================

    public void OnHit()
    {
        if (!CanHit) return;

        PlayerHand currentHand = GetCurrentHand();
        Card drawn = _deck.Draw();

        if (drawn.Rank == Rank.Joker)
        {
            HandleJokerDraw();
            return;
        }

        currentHand.AddCard(drawn);
        Debug.Log($"[히트] {CurrentPlayer}: {drawn} / 합계: {currentHand.GetTotal()}");

        // 히트 결과 먼저 전송 (카드 자체 정보)
        if (NetworkGameManager.Instance != null)
            NetworkGameManager.Instance.BroadcastHitResult(
                CurrentPlayer,
                (int)drawn.Rank,
                (int)drawn.Suit,
                currentHand.Cards[currentHand.Cards.Count - 1].IsHidden);

        // 턴 전환을 먼저 처리
        if (!TryAdvancePhase())
            SwitchTurn();

        // 턴 전환 후 최종 상태를 브로드캐스트
        if (NetworkGameManager.Instance != null)
            NetworkGameManager.Instance.BroadcastGameState();

        RefreshUI();
    }

    public void OnStand()
    {
        if (!CanStand) return;

        GetCurrentHand().Stand();
        Debug.Log($"[스탠드] {CurrentPlayer}");

        if (!TryAdvancePhase())
            SwitchTurn();

        // 턴 전환 후 브로드캐스트
        if (NetworkGameManager.Instance != null)
            NetworkGameManager.Instance.BroadcastGameState();

        RefreshUI();
    }

    private void SwitchTurn()
    {
        ActivePlayer next = CurrentPlayer == ActivePlayer.Player1
            ? ActivePlayer.Player2
            : ActivePlayer.Player1;

        PlayerHand nextHand = next == ActivePlayer.Player1
            ? _player1Hand
            : _player2Hand;

        // 상대가 스탠드면 현재 플레이어 턴 유지
        if (nextHand.IsStanding)
        {
            Debug.Log($"[턴 전환] 상대 스탠드 → {CurrentPlayer} 턴 유지");
            return;
        }

        CurrentPlayer = next;
        Debug.Log($"[턴 전환] → {CurrentPlayer}");
    }

    private bool TryAdvancePhase()
    {
        if (!_player1Hand.IsStanding || !_player2Hand.IsStanding) return false;

        CurrentPhase = GamePhase.AceSelection;
        CurrentPlayer = ActivePlayer.None;
        Debug.Log("[에이스 선택 단계 시작]");

        if (NetworkGameManager.Instance != null)
            NetworkGameManager.Instance.BroadcastAceSelectionStart();

        return true;
    }

    private void HandleJokerDraw()
    {
        ActivePlayer jokerDrawer = CurrentPlayer;
        ActivePlayer winner = jokerDrawer == ActivePlayer.Player1
            ? ActivePlayer.Player2
            : ActivePlayer.Player1;

        Debug.Log($"[조커] {jokerDrawer} 조커 드로우 → 즉시 패배");

        int penalty;

        if (jokerDrawer == ActivePlayer.Player1)
        {
            penalty = Mathf.Min(10, _player1Chips);
            _player1Chips -= penalty;
            _player2Chips += penalty;
        }
        else
        {
            penalty = Mathf.Min(10, _player2Chips);
            _player2Chips -= penalty;
            _player1Chips += penalty;
        }

        Debug.Log($"[조커 패널티] {jokerDrawer} 칩 {penalty}개 징수 / P1: {_player1Chips} / P2: {_player2Chips}");

        CurrentPhase = GamePhase.RoundEnd;
        CurrentPlayer = ActivePlayer.None;

        // 조커는 일반 라운드와 달리 "카드 공개" 연출이 없으므로, 여기서
        // 무슨 일이 있었는지(누가 뽑았고 칩이 어떻게 됐는지)를 먼저
        // 게스트에게 알림. 그 이후의 연패 카운트/아이템 지급/게임종료
        // 판정/다음 라운드 진행은 조커를 예외로 두지 않고 일반 라운드
        // 승부와 완전히 동일하게 SettleChips()에 그대로 위임
        if (NetworkGameManager.Instance != null)
            NetworkGameManager.Instance.BroadcastJokerDraw(
                jokerDrawer, _player1Chips, _player2Chips);

        SettleChips(winner);
    }

    // ===================================================================
    // 에이스 선택 단계
    // ===================================================================

    public void OnAceValueChanged(ActivePlayer player, int cardIndex, bool isEleven)
    {
        if (CurrentPhase != GamePhase.AceSelection) return;
        if (player == ActivePlayer.None) return;

        PlayerHand hand = player == ActivePlayer.Player1
            ? _player1Hand
            : _player2Hand;

        if (hand.AceSelectionConfirmed) return;

        hand.SetAceEleven(cardIndex, isEleven);
        Debug.Log($"[에이스 선택] {player} / 인덱스: {cardIndex} / 값: {(isEleven ? 11 : 1)}");
    }

    public void OnAceSelectionConfirmed(ActivePlayer player)
    {
        if (CurrentPhase != GamePhase.AceSelection) return;

        PlayerHand hand = player == ActivePlayer.Player1
            ? _player1Hand
            : _player2Hand;

        hand.ConfirmAceSelection();
        Debug.Log($"[에이스 확정] {player} / 합계: {hand.GetTotal()}");

        RefreshUI();

        if (_player1Hand.AceSelectionConfirmed &&
            _player2Hand.AceSelectionConfirmed)
        {
            UIManager.Instance.HideAceSelectionPanel();
            EnterCardRevealPhase();
        }
    }

    // 게스트 전용: 마스터의 단계 전환/베팅 시작 로직은 건드리지 않고
    // 합계 계산에 필요한 "에이스 선택 확정" 플래그만 로컬 미러에 반영
    public void SyncAceConfirmed(ActivePlayer player)
    {
        PlayerHand hand = player == ActivePlayer.Player1
            ? _player1Hand
            : _player2Hand;

        hand?.ConfirmAceSelection();
        RefreshUI();
    }

    // ===================================================================
    // 패 공개 / 라운드 종료
    // ===================================================================

    private void EnterCardRevealPhase()
    {
        CurrentPhase = GamePhase.CardReveal;
        CurrentPlayer = ActivePlayer.None;
        Debug.Log("[패 공개 단계 시작]");

        // 나중에 여기에 서버 버전 패 공개 로직 추가
        // 지금은 바로 베팅 단계로
        EnterBettingPhase();
    }

    // 현재는 EnterCardRevealPhase가 바로 베팅 단계로 넘어가서 호출되지 않음.
    // 패 공개 단계에 별도 연출/대기가 추가되면 그 완료 시점에 호출할 용도로 남겨둠
    public void OnCardRevealCompleted()
    {
        if (CurrentPhase != GamePhase.CardReveal) return;
        EnterBettingPhase();
    }

    private void EnterRoundEnd(bool isDie)
    {
        CurrentPhase = GamePhase.RoundEnd;
        CurrentPlayer = ActivePlayer.None;

        // 양쪽 마지막 카드 공개 (마스터 로컬)
        _player1Hand.RevealLastCard();
        _player2Hand.RevealLastCard();

        Debug.Log("[마지막 카드 공개]");

        // 게스트에게도 공개 신호를 보내서 동시에 카드가 열리게 함
        if (NetworkGameManager.Instance != null)
            NetworkGameManager.Instance.BroadcastRoundReveal();

        RefreshUI();

        // 결과를 잠시 보여준 뒤 실제 정산/다음 라운드 진행
        StartCoroutine(RoundEndSequence(isDie));
    }

    private IEnumerator RoundEndSequence(bool isDie)
    {
        yield return new WaitForSeconds(RoundEndRevealDuration);
        DetermineWinner(isDie);
    }

    // 게스트 전용: 공개 신호 수신 시 로컬 미러 손패도 동일하게 공개
    public void RevealBothHandsLastCard()
    {
        _player1Hand?.RevealLastCard();
        _player2Hand?.RevealLastCard();
        CurrentPhase = GamePhase.RoundEnd;
        CurrentPlayer = ActivePlayer.None;
        RefreshUI();
    }

    private void DetermineWinner(bool isDie)
    {
        int p1Total = _player1Hand.GetTotal();
        int p2Total = _player2Hand.GetTotal();
        bool p1Busted = _player1Hand.IsBusted;
        bool p2Busted = _player2Hand.IsBusted;

        Debug.Log($"[최종 합계] P1: {p1Total} / P2: {p2Total}");
        Debug.Log($"[버스트 여부] P1: {p1Busted} / P2: {p2Busted}");

        ActivePlayer winner = GetWinner(p1Total, p2Total, p1Busted, p2Busted);

        // 팟 정산 먼저
        if (winner == ActivePlayer.Player1)
        {
            _player1Chips += _pot;
            _pot = 0;
            Debug.Log($"[P1 승리] P1: {_player1Chips} / P2: {_player2Chips}");
        }
        else if (winner == ActivePlayer.Player2)
        {
            _player2Chips += _pot;
            _pot = 0;
            Debug.Log($"[P2 승리] P1: {_player1Chips} / P2: {_player2Chips}");
        }
        else
        {
            Debug.Log($"[무승부] 팟 {_pot}개 보류");
        }

        // 다이 패널티는 팟 정산 후
        if (isDie && _diedPlayer != ActivePlayer.None)
        {
            bool opponentBusted = _diedPlayer == ActivePlayer.Player1
                ? p2Busted
                : p1Busted;

            if (opponentBusted)
            {
                int penalty = _diedPlayer == ActivePlayer.Player1
                    ? Mathf.Min(5, _player1Chips)
                    : Mathf.Min(5, _player2Chips);

                if (_diedPlayer == ActivePlayer.Player1)
                {
                    _player1Chips -= penalty;
                    _player2Chips += penalty;
                }
                else
                {
                    _player2Chips -= penalty;
                    _player1Chips += penalty;
                }
                Debug.Log($"[다이 패널티] {_diedPlayer} 칩 {penalty}개 추가 징수");
            }
        }

        // 게임 종료 체크는 모든 정산 후 한 번만
        if (_player1Chips <= 0 || _player2Chips <= 0)
        {
            CurrentPhase = GamePhase.GameEnd;
            Debug.Log($"[게임 종료] 승자: {(_player1Chips > 0 ? "P1" : "P2")}");

            if (NetworkGameManager.Instance != null)
                NetworkGameManager.Instance.BroadcastGameEnd(
                    _player1Chips > 0 ? ActivePlayer.Player1 : ActivePlayer.Player2,
                    _player1Chips, _player2Chips);
            return;
        }

        SettleChips(winner);
    }

    private ActivePlayer GetWinner(int p1Total, int p2Total, bool p1Busted, bool p2Busted)
    {
        // 다이한 플레이어는 무조건 패배
        if (_diedPlayer == ActivePlayer.Player1) return ActivePlayer.Player2;
        if (_diedPlayer == ActivePlayer.Player2) return ActivePlayer.Player1;

        // 한쪽만 버스트 → 버스트 아닌 쪽 승리
        if (p1Busted && !p2Busted) return ActivePlayer.Player2;
        if (!p1Busted && p2Busted) return ActivePlayer.Player1;

        // 양쪽 다 버스트 또는 둘 다 정상 → 21에 가까운 쪽
        int p1Diff = Mathf.Abs(21 - p1Total);
        int p2Diff = Mathf.Abs(21 - p2Total);

        if (p1Diff != p2Diff)
            return p1Diff < p2Diff
                ? ActivePlayer.Player1
                : ActivePlayer.Player2;

        return CompareHighestCard();
    }

    private ActivePlayer CompareHighestCard()
    {
        Card p1Highest = GetHighestCard(_player1Hand);
        Card p2Highest = GetHighestCard(_player2Hand);

        // 블랙잭 값(J/Q/K가 전부 10으로 뭉개짐)이 아닌 실제 랭크 서열로 비교
        int p1Value = (int)p1Highest.Rank;
        int p2Value = (int)p2Highest.Rank;

        if (p1Value != p2Value)
            return p1Value > p2Value ? ActivePlayer.Player1 : ActivePlayer.Player2;

        if (p1Highest.Suit != p2Highest.Suit)
            return p1Highest.Suit > p2Highest.Suit ? ActivePlayer.Player1 : ActivePlayer.Player2;

        // 완전 동점 → 무승부
        return ActivePlayer.None;
    }

    private Card GetHighestCard(PlayerHand hand)
    {
        Card highest = hand.Cards[0].Card;

        foreach (CardSlot slot in hand.Cards)
        {
            Card card = slot.Card;

            if ((int)card.Rank > (int)highest.Rank)
            {
                highest = card;
                continue;
            }

            if (card.Rank == highest.Rank &&
                card.Suit > highest.Suit)
                highest = card;
        }

        return highest;
    }

    private void SettleChips(ActivePlayer winner)
    {
        // 칩 정산 먼저
        // (DetermineWinner에서 이미 처리했으니 여기선 연패/아이템만)
        if (winner == ActivePlayer.Player1)
        {
            _lastLoser = ActivePlayer.Player2;
            _player1LoseStreak = 0;
            _player2LoseStreak++;
        }
        else if (winner == ActivePlayer.Player2)
        {
            _lastLoser = ActivePlayer.Player1;
            _player2LoseStreak = 0;
            _player1LoseStreak++;
        }

        // 게임 종료 체크
        if (_player1Chips <= 0 || _player2Chips <= 0)
        {
            CurrentPhase = GamePhase.GameEnd;
            Debug.Log($"[게임 종료] 승자: {(_player1Chips > 0 ? "P1" : "P2")}");

            if (NetworkGameManager.Instance != null)
                NetworkGameManager.Instance.BroadcastGameEnd(
                    _player1Chips > 0 ? ActivePlayer.Player1 : ActivePlayer.Player2,
                    _player1Chips, _player2Chips);
            return;
        }

        // 게임 종료가 아닐 때만 아이템 지급
        if (winner == ActivePlayer.Player1)
            CheckAndGiveItem(ActivePlayer.Player2);
        else if (winner == ActivePlayer.Player2)
            CheckAndGiveItem(ActivePlayer.Player1);

        if (CurrentPhase == GamePhase.ItemDiscard)
        {
            // 이전엔 여기서 그냥 return만 해서 게스트가 칩 변화도,
            // 아이템 폐기 단계 진입 사실도 전혀 몰랐음
            if (NetworkGameManager.Instance != null)
                NetworkGameManager.Instance.BroadcastItemDiscardStart(
                    (int)_pendingItemPlayer,
                    (int)_pendingItem.Type,
                    _player1Chips,
                    _player2Chips);
            return;
        }

        if (NetworkGameManager.Instance != null)
            NetworkGameManager.Instance.BroadcastRoundEnd(
                winner, _player1Chips, _player2Chips);

        if (_player1Chips <= 0 || _player2Chips <= 0)
        {
            CurrentPhase = GamePhase.GameEnd;
            if (NetworkGameManager.Instance != null)
                NetworkGameManager.Instance.BroadcastGameEnd(
                    _player1Chips > 0 ? ActivePlayer.Player1 : ActivePlayer.Player2,
                    _player1Chips, _player2Chips);
            return;
        }

        StartNewRound();
    }

    private void CheckAndGiveItem(ActivePlayer player)
    {
        int streak = player == ActivePlayer.Player1
            ? _player1LoseStreak
            : _player2LoseStreak;

        Debug.Log($"[연패 체크] {player} / {streak}연패");

        if (streak % 2 != 0 || streak == 0) return;

        ItemType[] items = { ItemType.Magnifier, ItemType.Flash, ItemType.Bribe };
        ItemType random = items[_random.Next(items.Length)];

        List<Item> inventory = player == ActivePlayer.Player1
            ? _player1Items
            : _player2Items;

        int slots = player == ActivePlayer.Player1
            ? _player1ItemSlots
            : _player2ItemSlots;

        Debug.Log($"[슬롯 체크] {player} / {inventory.Count}/{slots}");

        if (inventory.Count < slots)
        {
            AddItem(player, random);
            NetworkGameManager.Instance?.BroadcastItemState();
        }
        else
        {
            _pendingItem = new Item(random);
            _pendingItemPlayer = player;
            CurrentPhase = GamePhase.ItemDiscard;
            Debug.Log($"[아이템 슬롯 가득] {player} / {random} 폐기 선택 필요");
        }
    }

    // ===================================================================
    // 베팅 단계
    // ===================================================================

    private void EnterBettingPhase()
    {
        CurrentPhase = GamePhase.Betting;
        CurrentPlayer = _bettingFirst;
        _isFirstBet = true;

        // 양쪽 각 1개씩 기본 베팅
        _player1Chips -= 1;
        _player2Chips -= 1;
        _player1TotalBet += 1;
        _player2TotalBet += 1;
        _pot += 2;

        Debug.Log($"[베팅 단계 시작] 선공: {CurrentPlayer} / 기본 베팅 각 1개 / 팟: {_pot} / P1칩: {_player1Chips} / P2칩: {_player2Chips}");

        if (NetworkGameManager.Instance != null)
            NetworkGameManager.Instance.BroadcastBettingStart();
    }

    public void OnRaise(int amount)
    {
        if (!CanRaise) return;
        if (amount <= 0) return;
        if (amount > MaxRaise) return;

        int totalAmount = CallAmount + amount;

        if (CurrentPlayer == ActivePlayer.Player1)
        {
            _player1Chips -= totalAmount;
            _player1TotalBet += totalAmount;
        }
        else
        {
            _player2Chips -= totalAmount;
            _player2TotalBet += totalAmount;
        }

        _pot += totalAmount;
        _isFirstBet = false;

        Debug.Log($"[레이즈] {CurrentPlayer} / 콜:{CallAmount} + 추가:{amount} / 팟: {_pot} / P1칩: {_player1Chips} / P2칩: {_player2Chips}");

        SwitchBettingTurn();
    }

    public void OnCall()
    {
        if (!CanCall) return;

        int amount = CallAmount;

        if (CurrentPlayer == ActivePlayer.Player1)
        {
            _player1Chips -= amount;
            _player1TotalBet += amount;
        }
        else
        {
            _player2Chips -= amount;
            _player2TotalBet += amount;
        }

        _pot += amount;

        Debug.Log($"[콜] {CurrentPlayer} / +{amount} / 팟: {_pot} / P1칩: {_player1Chips} / P2칩: {_player2Chips}");

        EnterRoundEnd(isDie: false);
    }

    public void OnDie()
    {
        if (!CanDie) return;

        _diedPlayer = CurrentPlayer;
        Debug.Log($"[다이] {CurrentPlayer}");

        EnterRoundEnd(isDie: true);
    }

    private void SwitchBettingTurn()
    {
        CurrentPlayer = CurrentPlayer == ActivePlayer.Player1
            ? ActivePlayer.Player2
            : ActivePlayer.Player1;

        Debug.Log($"[베팅 턴 전환] → {CurrentPlayer}");
    }

    // ===================================================================
    // 아이템
    // ===================================================================

    // 돋보기
    public Card OnUseMagnifier(ActivePlayer player)
    {
        if (CurrentPhase != GamePhase.Draw) return null;
        if (CurrentPlayer != player) return null;
        if (!HasItem(player, ItemType.Magnifier)) return null;

        RemoveItem(player, ItemType.Magnifier);

        Card next = _deck.PeekTop();
        Debug.Log($"[돋보기] {player} / 다음 카드: {next}");

        if (NetworkGameManager.Instance != null)
        {
            // 다음 카드 값은 사용한 본인에게만 의미 있으므로 전체에 브로드캐스트하되
            // 수신 측에서 자기 자신의 요청 결과인지 확인 후 표시
            NetworkGameManager.Instance.BroadcastMagnifierResult(
                player, (int)next.Rank, (int)next.Suit);
            NetworkGameManager.Instance.BroadcastItemState();
        }

        return next;
    }

    // 뇌물
    public bool OnUseBribe(ActivePlayer player)
    {
        if (CurrentPhase != GamePhase.Draw) return false;
        if (CurrentPlayer != player) return false;
        if (!HasItem(player, ItemType.Bribe)) return false;

        PlayerHand hand = player == ActivePlayer.Player1
            ? _player1Hand
            : _player2Hand;

        if (hand.Cards.Count == 0) return false;

        Card newCard = _deck.Draw();
        if (newCard == null) return false;

        hand.ReplaceLastCard(newCard);
        RemoveItem(player, ItemType.Bribe);

        Debug.Log($"[거래] {player} / 마지막 카드 교체");

        if (NetworkGameManager.Instance != null)
        {
            // 예전엔 이 교체 자체를 상대(게스트)가 전혀 몰랐음
            NetworkGameManager.Instance.BroadcastBribeResult(
                player, (int)newCard.Rank, (int)newCard.Suit);
            NetworkGameManager.Instance.BroadcastItemState();
        }

        RefreshUI();
        return true;
    }

    // 플래쉬
    public bool OnUseFlash(ActivePlayer player)
    {
        if (CurrentPhase != GamePhase.Betting) return false;
        if (CurrentPlayer != player) return false;
        if (!HasItem(player, ItemType.Flash)) return false;

        int steal = 3;

        if (player == ActivePlayer.Player1)
        {
            steal = Mathf.Min(3, _player2Chips);
            _player2Chips -= steal;
            _player1Chips += steal;
        }
        else
        {
            steal = Mathf.Min(3, _player1Chips);
            _player1Chips -= steal;
            _player2Chips += steal;
        }

        RemoveItem(player, ItemType.Flash);
        Debug.Log($"[플래쉬] {player} / {steal}개 탈취 / P1칩: {_player1Chips} / P2칩: {_player2Chips}");

        if (NetworkGameManager.Instance != null)
        {
            // 칩 변화는 기존 BroadcastGameState()를 그대로 재사용
            NetworkGameManager.Instance.BroadcastGameState();
            NetworkGameManager.Instance.BroadcastItemState();
        }

        RefreshUI();
        return true;
    }

    // 아이템 보유 여부
    private bool HasItem(ActivePlayer player, ItemType type)
    {
        List<Item> items = player == ActivePlayer.Player1
            ? _player1Items
            : _player2Items;

        foreach (Item item in items)
        {
            if (item.Type == type) return true;
        }
        return false;
    }

    // 아이템 제거
    private void RemoveItem(ActivePlayer player, ItemType type)
    {
        Character character = player == ActivePlayer.Player1
            ? _player1Character
            : _player2Character;

        if (character != null && character.OnItemUsed(this, player))
        {
            Debug.Log($"[잭 능력] {player} 아이템 유지!");
            return;
        }

        List<Item> items = player == ActivePlayer.Player1
            ? _player1Items
            : _player2Items;

        for (int i = 0; i < items.Count; i++)
        {
            if (items[i].Type == type)
            {
                items.RemoveAt(i);
                return;
            }
        }
    }

    // 아이템 추가 (연패 시 획득)
    public void AddItem(ActivePlayer player, ItemType type)
    {
        Character character = player == ActivePlayer.Player1
            ? _player1Character
            : _player2Character;

        if (character?.Type == CharacterType.Joker)
        {
            Debug.Log($"[조커] {player} 아이템 획득 불가");
            return;
        }

        List<Item> inventory = player == ActivePlayer.Player1
            ? _player1Items
            : _player2Items;

        inventory.Add(new Item(type));
        Debug.Log($"[아이템 획득] {player} / {type}");
    }

    public void OnItemDiscard(ActivePlayer player, int discardIndex)
    {
        if (CurrentPhase != GamePhase.ItemDiscard) return;
        if (player != _pendingItemPlayer) return;

        if (discardIndex >= 0)
        {
            List<Item> inventory = player == ActivePlayer.Player1
                ? _player1Items
                : _player2Items;

            if (discardIndex >= inventory.Count) return;

            inventory.RemoveAt(discardIndex);
            inventory.Add(_pendingItem);
            Debug.Log($"[아이템 교체] {player} / 인덱스 {discardIndex} 폐기 / {_pendingItem} 획득");
        }
        else
        {
            Debug.Log($"[아이템 폐기] {player} / 새 아이템 {_pendingItem} 폐기");
        }

        _pendingItem = null;
        _pendingItemPlayer = ActivePlayer.None;

        NetworkGameManager.Instance?.BroadcastItemState();

        UIManager.Instance.HideItemDiscardPanel();
        StartNewRound(); // 칩 정산 코드 삭제, 바로 다음 라운드
    }

    // ===================================================================
    // UI 갱신 헬퍼
    // ===================================================================

    private void RefreshUI()
    {
        if (UIManager.Instance == null) return;

        ActivePlayer localPlayer = NetworkGameManager.Instance != null
            ? NetworkGameManager.Instance.LocalPlayer
            : ActivePlayer.Player1;

        // 카드 자체는 UIManager가 매 프레임 직접 조회해서 스프라이트로 그리므로,
        // 여기서는 합계 텍스트만 넘김
        UIManager.Instance.RefreshCards(
            p1Total: GetTotalText(_player1Hand, isMine: localPlayer == ActivePlayer.Player1),
            p2Total: GetTotalText(_player2Hand, isMine: localPlayer == ActivePlayer.Player2)
        );
    }

    // 내 패면 항상 정확한 합계. 상대 패면 마지막 카드가 숨겨진 동안엔
    // 보이는 카드 합("GetVisibleTotal")만 보여주고 "+?"를 붙여서
    // 실제 합계 값이 화면 텍스트로 새어나가지 않도록 함
    private string GetTotalText(PlayerHand hand, bool isMine)
    {
        if (hand == null) return "0";

        if (isMine || hand.Cards.Count == 0)
            return hand.GetTotal().ToString();

        bool hasHiddenCard = hand.Cards[hand.Cards.Count - 1].IsHidden;
        if (hasHiddenCard)
            return $"{hand.GetVisibleTotal()}+?";

        return hand.GetTotal().ToString();
    }

    // ===================================================================
    // 네트워크 동기화 (게스트 전용: 마스터가 브로드캐스트한 상태를 로컬 미러에 반영)
    // ===================================================================

    public void SyncState(
        GamePhase phase,
        ActivePlayer player,
        int p1Chips,
        int p2Chips,
        int pot,
        bool isFirstBet,
        int p1TotalBet,
        int p2TotalBet)
    {
        CurrentPhase = phase;
        CurrentPlayer = player;
        _player1Chips = p1Chips;
        _player2Chips = p2Chips;
        _pot = pot;
        _isFirstBet = isFirstBet;
        _player1TotalBet = p1TotalBet;
        _player2TotalBet = p2TotalBet;

        RefreshUI();
    }

    // 게스트가 히트 결과를 받아서 패에 추가
    public void SyncHitResult(ActivePlayer player, Rank rank, Suit suit, bool isHidden)
    {
        Card card = new Card(rank, suit);
        PlayerHand hand = player == ActivePlayer.Player1
            ? _player1Hand
            : _player2Hand;

        hand.AddCard(card);
        RefreshUI();
    }

    // 선공 설정
    public void SetFirstTurn(ActivePlayer player)
    {
        CurrentPlayer = player;
        _bettingFirst = player;
        _isFirstRound = false;
        RefreshUI();
    }

    // 내 카드 수신
    public void ReceiveMyCard(ActivePlayer player, Card card)
    {
        PlayerHand hand = player == ActivePlayer.Player1
            ? _player1Hand
            : _player2Hand;

        hand?.AddCard(card);
        RefreshUI();
    }

    public void SyncRoundEnd(ActivePlayer winner, int p1Chips, int p2Chips)
    {
        _player1Chips = p1Chips;
        _player2Chips = p2Chips;
        CurrentPhase = GamePhase.RoundEnd;
        CurrentPlayer = ActivePlayer.None;
        RefreshUI();
    }

    public void SyncNextRound(Card sharedCard, ActivePlayer firstPlayer,
        int p1Chips, int p2Chips, int pot)
    {
        _player1Chips = p1Chips;
        _player2Chips = p2Chips;
        _pot = pot;
        _sharedCard = sharedCard;
        _player1Hand = new PlayerHand(_sharedCard);
        _player2Hand = new PlayerHand(_sharedCard);
        _player1TotalBet = 0;
        _player2TotalBet = 0;
        _isFirstBet = true;
        _diedPlayer = ActivePlayer.None;
        CurrentPhase = GamePhase.Draw;
        CurrentPlayer = firstPlayer;
        _isFirstRound = false;
        RefreshUI();
    }

    public void SyncGameEnd(ActivePlayer winner, int p1Chips, int p2Chips)
    {
        _player1Chips = p1Chips;
        _player2Chips = p2Chips;
        CurrentPhase = GamePhase.GameEnd;
        CurrentPlayer = ActivePlayer.None;
        RefreshUI();
    }

    // 게스트 전용: 조커 드로우로 인한 즉시 페널티 반영
    // (라운드가 계속되는 경우엔 이후 BroadcastNextRound가, 게임이 끝나는
    //  경우엔 BroadcastGameEnd가 뒤이어 도착해 최종 상태를 마무리해줌)
    public void SyncJokerDraw(ActivePlayer player, int p1Chips, int p2Chips)
    {
        _player1Chips = p1Chips;
        _player2Chips = p2Chips;
        CurrentPhase = GamePhase.RoundEnd;
        CurrentPlayer = ActivePlayer.None;
        Debug.Log($"[네트워크] {player} 조커 드로우 → 칩 반영 / P1: {_player1Chips} / P2: {_player2Chips}");
        RefreshUI();
    }

    // 상대가 게임 도중 방을 나갔을 때: 정상 종료 흐름(승패 판정, 코루틴 대기 등)을
    // 전부 건너뛰고 즉시 로비로 복귀. 남은 사람 혼자 방에 있어도
    // LobbyManager의 InRoom 분기가 이미 대기실 화면을 정상 처리해줌
    public void HandleOpponentLeft()
    {
        // 이미 정상적으로 게임이 끝난 상태(GameEnd)면 중복 처리 방지
        if (CurrentPhase == GamePhase.GameEnd) return;

        // UIManager가 CurrentPhase 변경을 폴링으로 감지해서 자기 나름대로
        // 승패를 다시 계산(칩 기준)해 결과 텍스트를 덮어쓰는 걸 막기 위해,
        // phase를 바꾸기 전에 먼저 자동 감지를 억제해둠
        UIManager.Instance?.SuppressGameEndHandling();

        CurrentPhase = GamePhase.GameEnd;
        CurrentPlayer = ActivePlayer.None;

        // RoundEndSequence 코루틴이 대기 중이었다면(마스터였던 경우) 즉시 중단
        StopAllCoroutines();

        Debug.Log("[네트워크] 상대방 퇴장으로 매치 종료");

        if (NetworkManager.Instance != null)
            NetworkManager.Instance.PendingGameResultText = "Opponent Left - You Win!";

        Photon.Pun.PhotonNetwork.IsMessageQueueRunning = false;
        UnityEngine.SceneManagement.SceneManager.LoadScene("LobbyScene");
    }

    // 뇌물로 교체된 카드를 게스트 쪽 미러 손패에도 반영
    public void SyncBribeResult(ActivePlayer player, Rank rank, Suit suit)
    {
        PlayerHand hand = player == ActivePlayer.Player1
            ? _player1Hand
            : _player2Hand;

        hand?.ReplaceLastCard(new Card(rank, suit));
        RefreshUI();
    }

    // 양쪽 아이템 인벤토리 전체를 마스터 기준으로 덮어씀
    public void SyncItemState(int[] p1Types, int[] p2Types)
    {
        _player1Items.Clear();
        foreach (int t in p1Types)
            _player1Items.Add(new Item((ItemType)t));

        _player2Items.Clear();
        foreach (int t in p2Types)
            _player2Items.Add(new Item((ItemType)t));

        RefreshUI();
    }

    // 게스트 전용: 아이템 폐기 단계 진입 동기화 (칩 변화 포함)
    public void SyncItemDiscardStart(
        ActivePlayer pendingPlayer, ItemType pendingType,
        int p1Chips, int p2Chips)
    {
        _player1Chips = p1Chips;
        _player2Chips = p2Chips;
        _pendingItemPlayer = pendingPlayer;
        _pendingItem = new Item(pendingType);
        CurrentPhase = GamePhase.ItemDiscard;
        RefreshUI();
    }
}