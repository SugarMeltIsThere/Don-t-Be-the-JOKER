using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum GamePhase
{
    FirstTurnDetermination,
    Draw,
    AceSelection,
    CardReveal,
    Betting,
    ItemDiscard,
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

// 게임 규칙의 단일 진실 공급원.
//  - 로컬/호스트: 규칙을 직접 실행하고, 끝날 때마다 Commit()으로 공개 상태를 게스트에 전파한다.
//  - 게스트(미러): 규칙을 실행하지 않는다. 호스트가 보낸 스냅샷/손패 이벤트(Mirror*)를 반영만 한다.
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    // ───────────────────────── 규칙 상수 ─────────────────────────
    private const int StartChips = 40;
    private const int Blackjack = 21;
    private const int MinDeckSize = 10;        // 이보다 적으면 덱 리셔플
    private const int BaseBet = 1;             // 베팅 단계 시작 시 각자 내는 기본 칩
    private const int DiePenalty = 5;          // 다이했는데 상대가 버스트였을 때 추가 징수
    private const int JokerPenalty = 10;
    private const int FlashSteal = 3;
    private const int ItemStreakInterval = 2;  // N연패마다 아이템 지급
    private const int DefaultItemSlots = 2;

    private static readonly ItemType[] RewardItems = { ItemType.Magnifier, ItemType.Flash, ItemType.Bribe };

    // 게스트가 상대의 비공개 카드 자리를 채워 두는 자리표시자 (값 0이라 합계에 영향 없음)
    public static readonly Card HiddenCard = new Card(Rank.Joker, Suit.Spade);

    // ───────────────────────── 플레이어별 상태 ─────────────────────────
    // P1/P2로 갈라져 있던 필드를 자리(Seat) 하나로 묶고, (int)ActivePlayer 로 인덱싱한다.
    private sealed class Seat
    {
        public int Chips = StartChips;
        public int TotalBet;
        public int LoseStreak;
        public int ItemSlots = DefaultItemSlots;
        public PlayerHand Hand;
        public Character Character;
        public readonly List<Item> Items = new List<Item>();

        public void ResetMatch()
        {
            Chips = StartChips;
            TotalBet = 0;
            LoseStreak = 0;
            ItemSlots = DefaultItemSlots;
            Items.Clear();
        }
    }

    private readonly Seat[] _seats = { new Seat(), new Seat() };

    private Seat S(ActivePlayer player) => _seats[(int)player];
    private static ActivePlayer Opponent(ActivePlayer player) =>
        player == ActivePlayer.Player1 ? ActivePlayer.Player2 : ActivePlayer.Player1;
    private Seat CurrentSeat => CurrentPlayer == ActivePlayer.None ? null : S(CurrentPlayer);

    // ───────────────────────── 공용 상태 ─────────────────────────
    private Deck _deck;
    private Card _sharedCard;
    private int _pot;
    private int _round;
    private bool _isFirstRound = true;
    private bool _isFirstBet = true;
    private bool _isMagnifierActive;
    private ActivePlayer _bettingFirst;
    private ActivePlayer _diedPlayer = ActivePlayer.None;
    private ActivePlayer _lastLoser = ActivePlayer.None;
    private Item _pendingItem;
    private ActivePlayer _pendingItemPlayer = ActivePlayer.None;
    private System.Random _random = new System.Random();

    public GamePhase CurrentPhase { get; private set; }
    public ActivePlayer CurrentPlayer { get; private set; }

    // ───────────────────────── UI 표시용 (기존 이름 유지) ─────────────────────────
    public IReadOnlyList<CardSlot> GetPlayer1Cards() => _seats[0].Hand?.Cards;
    public IReadOnlyList<CardSlot> GetPlayer2Cards() => _seats[1].Hand?.Cards;
    public int Player1Chips => _seats[0].Chips;
    public int Player2Chips => _seats[1].Chips;
    public int Player1TotalBet => _seats[0].TotalBet;
    public int Player2TotalBet => _seats[1].TotalBet;
    public int Player1ItemSlots => _seats[0].ItemSlots;
    public int Player2ItemSlots => _seats[1].ItemSlots;
    public CharacterType? Player1Character => _seats[0].Character?.Type;
    public CharacterType? Player2Character => _seats[1].Character?.Type;
    public IReadOnlyList<Item> Player1Items => _seats[0].Items;
    public IReadOnlyList<Item> Player2Items => _seats[1].Items;

    public int Pot => _pot;
    public bool IsFirstBet => _isFirstBet;
    public bool IsMagnifierActive => _isMagnifierActive;
    public Card SharedCard => _sharedCard;
    public Item PendingItem => _pendingItem;
    public ActivePlayer PendingItemPlayer => _pendingItemPlayer;
    public Card PeekedCard { get; private set; }   // 게스트가 돋보기로 확인한 다음 카드

    public int CallAmount => CurrentPlayer == ActivePlayer.None
        ? 0
        : S(Opponent(CurrentPlayer)).TotalBet - CurrentSeat.TotalBet;

    // 레이즈 금액이 곧 상대의 다음 콜 금액이 되므로, 상대 보유 칩을 넘지 않도록 제한
    public int MaxRaise => CurrentPlayer == ActivePlayer.None
        ? 0
        : Mathf.Max(0, Mathf.Min(CurrentSeat.Chips - CallAmount, S(Opponent(CurrentPlayer)).Chips));

    public bool CanCall =>
        !_isFirstBet &&
        CurrentPhase == GamePhase.Betting &&
        CurrentPlayer != ActivePlayer.None &&
        CallAmount > 0;

    public bool CanRaise =>
        CurrentPhase == GamePhase.Betting &&
        CurrentPlayer != ActivePlayer.None &&
        MaxRaise > 0;

    public bool CanDie =>
        CurrentPhase == GamePhase.Betting &&
        CurrentPlayer != ActivePlayer.None;

    public bool CanStand =>
        CurrentPhase == GamePhase.Draw &&
        CurrentSeat?.Hand != null;

    public bool CanHit => CanStand && !CurrentSeat.Hand.MustStand;

    // ───────────────────────── 네트워크 경계 ─────────────────────────
    private IGameNet _net;   // 테스트용 주입. 평소에는 씬의 NetworkGameManager를 쓴다.

    private IGameNet Net
    {
        get
        {
            if (_net != null) return _net;
            NetworkGameManager scene = NetworkGameManager.Instance;
            return scene != null ? scene : null;   // UnityEngine.Object의 null 비교를 거치기 위해 구체 타입으로 비교
        }
    }

    public void SetNet(IGameNet net) => _net = net;

    private bool IsMirror => Net != null && !Net.IsHost;
    private IGameNet HostNet => Net != null && Net.IsHost ? Net : null;   // 호스트일 때만 전송 대상
    private ActivePlayer LocalSeat => Net != null ? Net.LocalPlayer : ActivePlayer.Player1;
    private bool IsLocalSeat(ActivePlayer player) => Net == null || Net.LocalPlayer == player;

    // ───────────────────────── 생명주기 ─────────────────────────
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
        if (Net == null)
            InitializeGame();
    }

    // ───────────────────────── 게임 시작 / 재시작 ─────────────────────────
    public void InitializeGame()
    {
        SetCharacter(ActivePlayer.Player1, CharacterType.King);
        SetCharacter(ActivePlayer.Player2, CharacterType.Queen);
        RestartGame();
    }

    public void RestartGame()
    {
        foreach (Seat seat in _seats)
            seat.ResetMatch();

        _pot = 0;
        _isFirstRound = true;
        _lastLoser = ActivePlayer.None;

        _deck = NewDeck();
        ApplyCharacterAbilities();   // 조커 능력이 새 덱에 들어가도록 덱 생성 뒤에 적용
        StartNewRound();
    }

    private static Deck NewDeck()
    {
        Deck deck = new Deck(includeJoker: false);
        deck.Shuffle();
        return deck;
    }

    private void ApplyCharacterAbilities()
    {
        for (int i = 0; i < _seats.Length; i++)
            _seats[i].Character?.OnGameStart(this, (ActivePlayer)i);
    }

    // 캐릭터 능력(Character.cs)이 호출하는 진입점들
    public void SetCharacter(ActivePlayer player, CharacterType type)
    {
        S(player).Character = CreateCharacter(type);
        Debug.Log($"[캐릭터 선택] {player} / {type}");
    }

    private static Character CreateCharacter(CharacterType type) => type switch
    {
        CharacterType.King => new KingCharacter(),
        CharacterType.Queen => new QueenCharacter(),
        CharacterType.Jack => new JackCharacter(),
        CharacterType.Joker => new JokerCharacter(),
        _ => null
    };

    public void SetItemSlots(ActivePlayer player, int slots) => S(player).ItemSlots = slots;    // 킹: 슬롯 증가
    public void AddChips(ActivePlayer player, int amount) => S(player).Chips += amount;         // 퀸: 칩 추가
    public void InsertJokerCard() => _deck.InsertJoker();                                       // 조커: 덱에 조커 삽입

    // ───────────────────────── 라운드 시작 ─────────────────────────
    public void StartNewRound()
    {
        BeginRound();
        Commit();
    }

    private void BeginRound()
    {
        // 덱이 부족하면 리셔플
        if (_deck == null || _deck.Count < MinDeckSize)
        {
            _deck = NewDeck();
            Debug.Log("[덱 리셔플]");
        }

        do
        {
            _sharedCard = _deck.Draw();
        } while (_sharedCard.Rank == Rank.Joker);
        Debug.Log($"[공유카드] {_sharedCard}");

        _round++;
        foreach (Seat seat in _seats)
        {
            seat.Hand = new PlayerHand(_sharedCard);
            seat.TotalBet = 0;
        }
        _isFirstBet = true;
        _diedPlayer = ActivePlayer.None;
        HostNet?.SendRoundStart(_round, _sharedCard);

        CurrentPhase = GamePhase.FirstTurnDetermination;
        CurrentPlayer = ActivePlayer.None;
        DetermineFirstTurn();

        Deal(ActivePlayer.Player1, _deck.Draw());
        Deal(ActivePlayer.Player2, _deck.Draw());
        for (int i = 0; i < _seats.Length; i++)
            Debug.Log($"[P{i + 1} 패] {_seats[i].Hand.Cards[0].Card} / 합계: {_seats[i].Hand.GetTotal()}");

        CurrentPhase = GamePhase.Draw;
        _isFirstRound = false;
        Debug.Log($"[드로우 단계 시작] 선공: {CurrentPlayer}");
    }

    private void DetermineFirstTurn()
    {
        if (!_isFirstRound && _lastLoser != ActivePlayer.None)
        {
            CurrentPlayer = _lastLoser;
            Debug.Log($"[선공결정] 이전 라운드 패자 → {CurrentPlayer}");
        }
        else
        {
            // 첫 라운드: A~10을 섞어 두 장을 뽑고, 더 높은 쪽이 선공
            List<Card> turnDeck = new List<Card>(10);
            for (Rank rank = Rank.Ace; rank <= Rank.Ten; rank++)
                turnDeck.Add(new Card(rank, Suit.Spade));
            Shuffle(turnDeck);

            Card p1Card = turnDeck[0];
            Card p2Card = turnDeck[1];
            CurrentPlayer = p1Card.Rank > p2Card.Rank ? ActivePlayer.Player1 : ActivePlayer.Player2;
            Debug.Log($"[선공결정] P1: {p1Card} / P2: {p2Card} → {CurrentPlayer}");
        }

        _bettingFirst = CurrentPlayer;
    }

    private void Shuffle(List<Card> cards)   // Fisher-Yates
    {
        for (int n = cards.Count - 1; n > 0; n--)
        {
            int k = _random.Next(n + 1);
            (cards[k], cards[n]) = (cards[n], cards[k]);
        }
    }

    // 손패에 카드를 넣고, 게스트가 알아도 되는 만큼만 전송한다.
    private void Deal(ActivePlayer seat, Card card)
    {
        PlayerHand hand = S(seat).Hand;
        hand.AddCard(card);

        // AddCard가 직전 카드를 공개 상태로 바꾸므로, 그 카드는 상대에게도 알려준다.
        IReadOnlyList<CardSlot> cards = hand.Cards;
        Card revealed = cards.Count > 1 ? cards[cards.Count - 2].Card : null;
        HostNet?.SendCard(seat, card, revealed);
    }

    // ───────────────────────── 드로우 단계 ─────────────────────────
    public void OnHit()
    {
        if (!CanHit) return;

        Card drawn = _deck.Draw();
        if (drawn.Rank == Rank.Joker)
        {
            HandleJokerDraw();
            Commit();
            return;
        }

        Deal(CurrentPlayer, drawn);
        Debug.Log($"[히트] {CurrentPlayer}: {drawn} / 합계: {CurrentSeat.Hand.GetTotal()}");

        if (!TryAdvancePhase())
            SwitchTurn();
        Commit();
    }

    public void OnStand()
    {
        if (!CanStand) return;

        CurrentSeat.Hand.Stand();
        Debug.Log($"[스탠드] {CurrentPlayer}");

        if (!TryAdvancePhase())
            SwitchTurn();
        Commit();
    }

    private void SwitchTurn()
    {
        ActivePlayer next = Opponent(CurrentPlayer);

        // 상대가 스탠드면 현재 플레이어 턴 유지
        if (S(next).Hand.IsStanding)
        {
            Debug.Log($"[턴 전환] 상대 스탠드 → {CurrentPlayer} 턴 유지");
            return;
        }

        CurrentPlayer = next;
        Debug.Log($"[턴 전환] → {CurrentPlayer}");
    }

    private bool TryAdvancePhase()
    {
        if (!_seats.All(seat => seat.Hand.IsStanding)) return false;

        CurrentPhase = GamePhase.AceSelection;
        CurrentPlayer = ActivePlayer.None;
        Debug.Log("[에이스 선택 단계 시작]");
        return true;
    }

    private void HandleJokerDraw()
    {
        Debug.Log($"[조커] {CurrentPlayer} 조커 드로우 → 즉시 패배");

        int penalty = Mathf.Min(JokerPenalty, CurrentSeat.Chips);
        Transfer(CurrentPlayer, Opponent(CurrentPlayer), penalty);
        Debug.Log($"[조커 패널티] {CurrentPlayer} 칩 {penalty}개 징수 / P1: {Player1Chips} / P2: {Player2Chips}");

        CurrentPhase = GamePhase.RoundEnd;
        CurrentPlayer = ActivePlayer.None;

        if (!TryEndMatch())
            BeginRound();
    }

    // ───────────────────────── 에이스 선택 단계 ─────────────────────────
    public void OnAceValueChanged(ActivePlayer player, int cardIndex, bool isEleven)
    {
        if (CurrentPhase != GamePhase.AceSelection) return;
        if (player == ActivePlayer.None) return;

        PlayerHand hand = S(player).Hand;
        if (hand.AceSelectionConfirmed) return;

        hand.SetAceEleven(cardIndex, isEleven);
        Debug.Log($"[에이스 선택] {player} / 인덱스: {cardIndex} / 값: {(isEleven ? 11 : 1)}");
    }

    public void OnAceSelectionConfirmed(ActivePlayer player)
    {
        if (CurrentPhase != GamePhase.AceSelection) return;

        PlayerHand hand = S(player).Hand;
        hand.ConfirmAceSelection();
        Debug.Log($"[에이스 확정] {player} / 합계: {hand.GetTotal()}");

        if (_seats.All(seat => seat.Hand.AceSelectionConfirmed))
            EnterCardRevealPhase();
        Commit();
    }

    private void EnterCardRevealPhase()
    {
        CurrentPhase = GamePhase.CardReveal;
        CurrentPlayer = ActivePlayer.None;
        Debug.Log("[패 공개 단계 시작]");

        // 나중에 여기에 서버 버전 패 공개 로직 추가
        // 지금은 바로 베팅 단계로
        EnterBettingPhase();
    }

    public void OnCardRevealCompleted()
    {
        if (CurrentPhase != GamePhase.CardReveal) return;
        EnterBettingPhase();
        Commit();
    }

    // ───────────────────────── 베팅 단계 ─────────────────────────
    private void EnterBettingPhase()
    {
        CurrentPhase = GamePhase.Betting;
        CurrentPlayer = _bettingFirst;
        _isFirstBet = true;

        foreach (Seat seat in _seats)   // 양쪽 각 1개씩 기본 베팅
            Pay(seat, BaseBet);

        Debug.Log($"[베팅 단계 시작] 선공: {CurrentPlayer} / 기본 베팅 각 {BaseBet}개 / 팟: {_pot} / P1칩: {Player1Chips} / P2칩: {Player2Chips}");
    }

    public void OnRaise(int amount)
    {
        if (!CanRaise) return;
        if (amount <= 0 || amount > MaxRaise) return;

        int call = CallAmount;
        Pay(CurrentSeat, call + amount);
        _isFirstBet = false;
        Debug.Log($"[레이즈] {CurrentPlayer} / 콜:{call} + 추가:{amount} / 팟: {_pot} / P1칩: {Player1Chips} / P2칩: {Player2Chips}");

        CurrentPlayer = Opponent(CurrentPlayer);
        Debug.Log($"[베팅 턴 전환] → {CurrentPlayer}");
        Commit();
    }

    public void OnCall()
    {
        if (!CanCall) return;

        int amount = CallAmount;
        Pay(CurrentSeat, amount);
        Debug.Log($"[콜] {CurrentPlayer} / +{amount} / 팟: {_pot} / P1칩: {Player1Chips} / P2칩: {Player2Chips}");

        EnterRoundEnd(isDie: false);
        Commit();
    }

    public void OnDie()
    {
        if (!CanDie) return;

        _diedPlayer = CurrentPlayer;
        Debug.Log($"[다이] {CurrentPlayer}");

        EnterRoundEnd(isDie: true);
        Commit();
    }

    private void Pay(Seat seat, int amount)
    {
        seat.Chips -= amount;
        seat.TotalBet += amount;
        _pot += amount;
    }

    private void Transfer(ActivePlayer from, ActivePlayer to, int amount)
    {
        S(from).Chips -= amount;
        S(to).Chips += amount;
    }

    // ───────────────────────── 라운드 종료 / 승패 ─────────────────────────
    private void EnterRoundEnd(bool isDie)
    {
        CurrentPhase = GamePhase.RoundEnd;
        CurrentPlayer = ActivePlayer.None;
        Debug.Log("[마지막 카드 공개]");

        PlayerHand h1 = _seats[0].Hand;
        PlayerHand h2 = _seats[1].Hand;
        Debug.Log($"[최종 합계] P1: {h1.GetTotal()} / P2: {h2.GetTotal()}");
        Debug.Log($"[버스트 여부] P1: {h1.IsBusted} / P2: {h2.IsBusted}");

        ActivePlayer winner = GetWinner();

        // 팟 정산 먼저
        if (winner != ActivePlayer.None)
        {
            S(winner).Chips += _pot;
            _pot = 0;
            Debug.Log($"[{winner} 승리] P1: {Player1Chips} / P2: {Player2Chips}");
        }
        else
        {
            Debug.Log($"[무승부] 팟 {_pot}개 보류");
        }

        // 다이 패널티는 팟 정산 후: 다이한 쪽의 상대가 버스트였다면 추가 징수
        if (isDie && _diedPlayer != ActivePlayer.None && S(Opponent(_diedPlayer)).Hand.IsBusted)
        {
            int penalty = Mathf.Min(DiePenalty, S(_diedPlayer).Chips);
            Transfer(_diedPlayer, Opponent(_diedPlayer), penalty);
            Debug.Log($"[다이 패널티] {_diedPlayer} 칩 {penalty}개 추가 징수");
        }

        if (TryEndMatch()) return;
        SettleRound(winner);
    }

    // 승패 규칙:
    //  1) 다이한 쪽은 무조건 패배
    //  2) 한쪽만 버스트 → 버스트가 아닌 쪽이 승리 (버스트는 정상 패를 절대 이기지 못한다)
    //  3) 둘 다 정상이거나 둘 다 버스트 → 21에 더 가까운 쪽이 승리
    //  4) 그래도 같으면 가장 높은 카드(값 → 무늬 순)로 비교
    private ActivePlayer GetWinner()
    {
        if (_diedPlayer != ActivePlayer.None) return Opponent(_diedPlayer);

        PlayerHand h1 = _seats[0].Hand;
        PlayerHand h2 = _seats[1].Hand;

        if (h1.IsBusted != h2.IsBusted)
            return h1.IsBusted ? ActivePlayer.Player2 : ActivePlayer.Player1;

        int d1 = Mathf.Abs(Blackjack - h1.GetTotal());
        int d2 = Mathf.Abs(Blackjack - h2.GetTotal());
        if (d1 != d2)
            return d1 < d2 ? ActivePlayer.Player1 : ActivePlayer.Player2;

        return CompareHighestCard(h1, h2);
    }

    private static ActivePlayer CompareHighestCard(PlayerHand h1, PlayerHand h2)
    {
        Card c1 = HighestCard(h1);
        Card c2 = HighestCard(h2);

        if (c1.GetBlackjackValue() == c2.GetBlackjackValue() && c1.Suit == c2.Suit)
            return ActivePlayer.None;   // 완전 동점 → 무승부
        return Outranks(c1, c2) ? ActivePlayer.Player1 : ActivePlayer.Player2;
    }

    private static Card HighestCard(PlayerHand hand)
    {
        Card highest = hand.Cards[0].Card;
        foreach (CardSlot slot in hand.Cards)
            if (Outranks(slot.Card, highest))
                highest = slot.Card;
        return highest;
    }

    private static bool Outranks(Card a, Card b) =>
        a.GetBlackjackValue() != b.GetBlackjackValue()
            ? a.GetBlackjackValue() > b.GetBlackjackValue()
            : a.Suit > b.Suit;

    // 칩이 떨어진 쪽이 있으면 게임 종료
    private bool TryEndMatch()
    {
        if (Player1Chips > 0 && Player2Chips > 0) return false;

        CurrentPhase = GamePhase.GameEnd;
        CurrentPlayer = ActivePlayer.None;
        Debug.Log($"[게임 종료] 승자: {(Player1Chips > 0 ? "P1" : "P2")}");
        return true;
    }

    // 연패 집계 → 아이템 지급 → 다음 라운드
    private void SettleRound(ActivePlayer winner)
    {
        if (winner != ActivePlayer.None)
        {
            ActivePlayer loser = Opponent(winner);
            _lastLoser = loser;
            S(winner).LoseStreak = 0;
            S(loser).LoseStreak++;

            CheckAndGiveItem(loser);
            if (CurrentPhase == GamePhase.ItemDiscard) return;   // 슬롯이 가득 → 폐기 선택을 기다린다
        }

        BeginRound();
    }

    // 상대가 나간 경우 등: 나간 쪽 칩을 0으로 만들어 즉시 패배 처리 (재접속 없음)
    public void Forfeit(ActivePlayer leaver)
    {
        if (CurrentPhase == GamePhase.GameEnd) return;

        S(leaver).Chips = 0;
        Debug.Log($"[기권] {leaver} 이탈");
        TryEndMatch();
        Commit();
    }

    // ───────────────────────── 아이템 ─────────────────────────
    private void CheckAndGiveItem(ActivePlayer player)
    {
        Seat seat = S(player);
        Debug.Log($"[연패 체크] {player} / {seat.LoseStreak}연패");

        if (seat.LoseStreak == 0 || seat.LoseStreak % ItemStreakInterval != 0) return;

        ItemType reward = RewardItems[_random.Next(RewardItems.Length)];
        Debug.Log($"[슬롯 체크] {player} / {seat.Items.Count}/{seat.ItemSlots}");

        if (seat.Items.Count < seat.ItemSlots)
        {
            AddItem(player, reward);
            return;
        }

        _pendingItem = new Item(reward);
        _pendingItemPlayer = player;
        CurrentPhase = GamePhase.ItemDiscard;
        Debug.Log($"[아이템 슬롯 가득] {player} / {reward} 폐기 선택 필요");
    }

    public void AddItem(ActivePlayer player, ItemType type)
    {
        if (S(player).Character?.Type == CharacterType.Joker)
        {
            Debug.Log($"[조커] {player} 아이템 획득 불가");
            return;
        }

        S(player).Items.Add(new Item(type));
        Debug.Log($"[아이템 획득] {player} / {type}");
    }

    private bool HasItem(ActivePlayer player, ItemType type) => S(player).Items.Any(item => item.Type == type);

    private void RemoveItem(ActivePlayer player, ItemType type)
    {
        Character character = S(player).Character;
        if (character != null && character.OnItemUsed(this, player))
        {
            Debug.Log($"[잭 능력] {player} 아이템 유지!");
            return;
        }

        List<Item> items = S(player).Items;
        int index = items.FindIndex(item => item.Type == type);
        if (index >= 0) items.RemoveAt(index);
    }

    // 아이템 사용 가능 조건 (사용 시점과 내 차례)
    private bool CanUse(ActivePlayer player, ItemType type, GamePhase phase) =>
        CurrentPhase == phase && CurrentPlayer == player && HasItem(player, type);

    // ── UI 진입점: 온라인에서는 내 자리만 조작하고, 게스트는 호스트에 요청만 보낸다 ──
    public Card OnUseMagnifier(ActivePlayer player)
    {
        if (!IsLocalSeat(player)) return null;
        if (IsMirror) { Net.RequestUseItem(ItemType.Magnifier); return null; }
        return HostUseMagnifier(player);
    }

    public bool OnUseBribe(ActivePlayer player)
    {
        if (!IsLocalSeat(player)) return false;
        if (IsMirror) { Net.RequestUseItem(ItemType.Bribe); return false; }
        return HostUseBribe(player);
    }

    public bool OnUseFlash(ActivePlayer player)
    {
        if (!IsLocalSeat(player)) return false;
        if (IsMirror) { Net.RequestUseItem(ItemType.Flash); return false; }
        return HostUseFlash(player);
    }

    public void OnItemDiscard(ActivePlayer player, int discardIndex)
    {
        if (!IsLocalSeat(player)) return;
        if (IsMirror) { Net.RequestDiscard(discardIndex); return; }
        HostDiscard(player, discardIndex);
    }

    // ── 호스트 진입점: NetworkGameManager가 게스트의 요청을 대신 실행할 때도 쓴다 ──
    public void HostUseItem(ActivePlayer seat, ItemType type)
    {
        switch (type)
        {
            case ItemType.Magnifier: HostUseMagnifier(seat); break;
            case ItemType.Bribe: HostUseBribe(seat); break;
            case ItemType.Flash: HostUseFlash(seat); break;
        }
    }

    private Card HostUseMagnifier(ActivePlayer player)
    {
        if (!CanUse(player, ItemType.Magnifier, GamePhase.Draw)) return null;

        RemoveItem(player, ItemType.Magnifier);
        _isMagnifierActive = true;

        Card next = _deck.PeekTop();
        Debug.Log($"[돋보기] {player} / 다음 카드: {next}");
        HostNet?.SendPeek(player, next);
        Commit();
        return next;
    }

    private bool HostUseBribe(ActivePlayer player)
    {
        if (!CanUse(player, ItemType.Bribe, GamePhase.Draw)) return false;

        PlayerHand hand = S(player).Hand;
        if (hand.Cards.Count == 0) return false;

        Card newCard = _deck.Draw();
        if (newCard == null) return false;

        hand.ReplaceLastCard(newCard);
        RemoveItem(player, ItemType.Bribe);
        HostNet?.SendReplaceLast(player, newCard);

        Debug.Log($"[거래] {player} / 마지막 카드 교체");
        Commit();
        return true;
    }

    private bool HostUseFlash(ActivePlayer player)
    {
        if (!CanUse(player, ItemType.Flash, GamePhase.Betting)) return false;

        int steal = Mathf.Min(FlashSteal, S(Opponent(player)).Chips);
        Transfer(Opponent(player), player, steal);
        RemoveItem(player, ItemType.Flash);

        Debug.Log($"[플래쉬] {player} / {steal}개 탈취 / P1칩: {Player1Chips} / P2칩: {Player2Chips}");
        Commit();
        return true;
    }

    public void HostDiscard(ActivePlayer player, int discardIndex)
    {
        if (CurrentPhase != GamePhase.ItemDiscard) return;
        if (player != _pendingItemPlayer) return;

        if (discardIndex >= 0)
        {
            List<Item> inventory = S(player).Items;
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
        BeginRound();
        Commit();
    }

    // ───────────────────────── 동기화: 호스트 → 게스트 ─────────────────────────
    // 명령 하나가 끝날 때 한 번 호출. UI를 갱신하고, 호스트라면 공개 상태를 스냅샷으로 내보낸다.
    private void Commit()
    {
        RefreshUI();
        HostNet?.PublishState(BuildSnapshot());
    }

    private GameSnapshot BuildSnapshot() => new GameSnapshot
    {
        Round = _round,
        Phase = (byte)CurrentPhase,
        Current = (byte)CurrentPlayer,
        P1Chips = Player1Chips,
        P2Chips = Player2Chips,
        P1TotalBet = Player1TotalBet,
        P2TotalBet = Player2TotalBet,
        Pot = _pot,
        IsFirstBet = _isFirstBet,
        P1Slots = (byte)Player1ItemSlots,
        P2Slots = (byte)Player2ItemSlots,
        P1Items = GameSnapshot.PackItems(_seats[0].Items),
        P2Items = GameSnapshot.PackItems(_seats[1].Items),
        P1Character = CharacterCode(_seats[0]),
        P2Character = CharacterCode(_seats[1]),
        PendingItemPlayer = (byte)_pendingItemPlayer,
        PendingItem = _pendingItem == null ? GameSnapshot.None : (byte)_pendingItem.Type
    };

    private static byte CharacterCode(Seat seat) =>
        seat.Character == null ? GameSnapshot.None : (byte)seat.Character.Type;

    // ───────────────────────── 동기화: 게스트(미러) 쪽 반영 ─────────────────────────
    public void MirrorApply(in GameSnapshot s)
    {
        if (s.Round == 0) return;   // 호스트가 아직 게임을 시작하지 않음

        _round = s.Round;
        CurrentPhase = (GamePhase)s.Phase;
        CurrentPlayer = (ActivePlayer)s.Current;
        _pot = s.Pot;
        _isFirstBet = s.IsFirstBet;

        ApplySeat(_seats[0], s.P1Chips, s.P1TotalBet, s.P1Slots, s.P1Items, s.P1Character);
        ApplySeat(_seats[1], s.P2Chips, s.P2TotalBet, s.P2Slots, s.P2Items, s.P2Character);

        _pendingItem = s.PendingItem == GameSnapshot.None ? null : new Item((ItemType)s.PendingItem);
        _pendingItemPlayer = (ActivePlayer)s.PendingItemPlayer;
        RefreshUI();
    }

    private static void ApplySeat(Seat seat, int chips, int totalBet, byte slots, byte items, byte character)
    {
        seat.Chips = chips;
        seat.TotalBet = totalBet;
        seat.ItemSlots = slots;
        GameSnapshot.UnpackItems(items, seat.Items);

        CharacterType? type = character == GameSnapshot.None ? (CharacterType?)null : (CharacterType)character;
        if (seat.Character?.Type != type)
            seat.Character = type.HasValue ? CreateCharacter(type.Value) : null;   // 능력은 호스트에서만 실행되므로 객체만 만든다
    }

    public void MirrorRoundStart(int round, Card sharedCard)
    {
        _round = round;
        _sharedCard = sharedCard;
        foreach (Seat seat in _seats)
            seat.Hand = new PlayerHand(sharedCard);
        PeekedCard = null;
        RefreshUI();
    }

    // revealedPrevious가 있으면 비공개였던 직전 카드를 진짜 값으로 교체한 뒤 새 카드를 붙인다.
    public void MirrorCard(ActivePlayer seat, Card card, Card revealedPrevious)
    {
        PlayerHand hand = S(seat).Hand;
        if (hand == null) return;

        if (revealedPrevious != null)
            hand.ReplaceLastCard(revealedPrevious);
        hand.AddCard(card);
        RefreshUI();
    }

    public void MirrorReplaceLast(ActivePlayer seat, Card card)
    {
        S(seat).Hand?.ReplaceLastCard(card);
        RefreshUI();
    }

    public void MirrorPeek(Card card)
    {
        PeekedCard = card;
        Debug.Log($"[돋보기] 다음 카드: {card}");
    }

    // 내 에이스 선택은 화면 합계에 바로 반영하고, 확정은 호스트가 따로 검증한다.
    public void MirrorAceValue(ActivePlayer seat, int cardIndex, bool isEleven) =>
        S(seat).Hand?.SetAceEleven(cardIndex, isEleven);

    public void MirrorAceConfirm(ActivePlayer seat)
    {
        S(seat).Hand?.ConfirmAceSelection();
        RefreshUI();
    }

    // ───────────────────────── UI ─────────────────────────
    private void RefreshUI()
    {
        UIManager ui = UIManager.Instance;
        if (ui == null) return;

        // 단계에 맞지 않는 패널 정리 (호스트/게스트 공통)
        if (CurrentPhase != GamePhase.AceSelection) ui.HideAceSelectionPanel();
        if (CurrentPhase != GamePhase.ItemDiscard) ui.HideItemDiscardPanel();
        if (CurrentPhase != GamePhase.GameEnd) ui.HideGameEndPanel();

        ActivePlayer me = LocalSeat;
        ui.RefreshCards(
            sharedCard: _sharedCard?.ToString() ?? "-",
            p1Cards: CardsText(ActivePlayer.Player1),
            p1Total: ViewTotal(ActivePlayer.Player1, me),
            p2Cards: CardsText(ActivePlayer.Player2),
            p2Total: ViewTotal(ActivePlayer.Player2, me));
    }

    // 내 합계는 전부, 상대 합계는 공개된 카드까지만 (숨긴 마지막 카드는 합계에도 드러나지 않게)
    private int ViewTotal(ActivePlayer seat, ActivePlayer viewer)
    {
        PlayerHand hand = S(seat).Hand;
        if (hand == null) return 0;
        return seat == viewer ? hand.GetTotal() : hand.GetVisibleTotal();
    }

    private string CardsText(ActivePlayer seat)
    {
        PlayerHand hand = S(seat).Hand;
        if (hand == null) return "-";
        return string.Join(" ", hand.Cards.Select(slot => slot.IsHidden ? "?" : slot.Card.ToString()));
    }
}