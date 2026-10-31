using System;
using System.Collections;
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

public enum GameEndReason
{
    Normal,
    OpponentLeft
}

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    private const int StartChips = 40;
    private const int Blackjack = 21;
    private const int MinDeckSize = 10;
    private const int BaseBet = 1;
    private const int DiePenalty = 5;
    private const int JokerPenalty = 10;
    private const int FlashSteal = 3;
    private const int ItemStreakInterval = 2;
    private const int DefaultItemSlots = 2;
    private const float RoundEndRevealDuration = 2.5f;

    private static readonly ItemType[] RewardItems = { ItemType.Magnifier, ItemType.Flash, ItemType.Bribe };

    public static readonly Card HiddenCard = new Card(Rank.Joker, Suit.Spade);

    private sealed class Seat
    {
        public int Chips = StartChips;
        public int TotalBet;
        public int LoseStreak;
        public int ItemSlots = DefaultItemSlots;
        public PlayerHand Hand;
        public Character Character;
        public readonly List<Item> Items = new List<Item>();

        // 매치 시작 시 좌석의 칩과 연패와 아이템을 초기화한다
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

    // 좌석 번호로 좌석 객체를 가져온다
    private Seat S(ActivePlayer player) => _seats[(int)player];
    // 상대 좌석을 반환한다
    private static ActivePlayer Opponent(ActivePlayer player) =>
        player == ActivePlayer.Player1 ? ActivePlayer.Player2 : ActivePlayer.Player1;
    private Seat CurrentSeat => CurrentPlayer == ActivePlayer.None ? null : S(CurrentPlayer);

    private Deck _deck;
    private Card _sharedCard;
    private int _pot;
    private int _round;
    private bool _isFirstRound = true;
    private bool _isFirstBet = true;
    private ActivePlayer _bettingFirst;
    private ActivePlayer _diedPlayer = ActivePlayer.None;
    private ActivePlayer _lastLoser = ActivePlayer.None;
    private Item _pendingItem;
    private ActivePlayer _pendingItemPlayer = ActivePlayer.None;
    private System.Random _random = new System.Random();

    public GamePhase CurrentPhase { get; private set; }
    public ActivePlayer CurrentPlayer { get; private set; }

    public event Action StateChanged;
    public event Action CharactersChanged;
    public event Action<int> RoundStarted;
    public event Action<ActivePlayer, int> CardDealt;
    public event Action<ActivePlayer> LastCardReplaced;
    public event Action RoundRevealed;
    public event Action<Card> PeekShown;
    public event Action<ActivePlayer, ItemType> ItemUsed;
    public event Action<GameEndReason> GameEnded;

    private GamePhase _notifiedPhase;

    // 플레이어1의 손패를 반환한다
    public IReadOnlyList<CardSlot> GetPlayer1Cards() => _seats[0].Hand?.Cards;
    // 플레이어2의 손패를 반환한다
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
    public Card SharedCard => _sharedCard;
    public Item PendingItem => _pendingItem;
    public ActivePlayer PendingItemPlayer => _pendingItemPlayer;
    public Card PeekedCard { get; private set; }

    public int CallAmount => CurrentPlayer == ActivePlayer.None
        ? 0
        : S(Opponent(CurrentPlayer)).TotalBet - CurrentSeat.TotalBet;

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

    private IGameNet _net;

    private IGameNet Net
    {
        get
        {
            if (_net != null) return _net;
            NetworkGameManager scene = NetworkGameManager.Instance;
            return scene != null ? scene : null;
        }
    }

    // 네트워크 구현체를 주입한다
    public void SetNet(IGameNet net) => _net = net;

    private bool IsMirror => Net != null && !Net.IsHost;
    private IGameNet HostNet => Net != null && Net.IsHost ? Net : null;
    private ActivePlayer LocalSeat => Net != null ? Net.LocalPlayer : ActivePlayer.Player1;
    // 이 좌석이 로컬 플레이어의 자리인지 확인한다
    private bool IsLocalSeat(ActivePlayer player) => Net == null || Net.LocalPlayer == player;

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

    // 네트워크가 없으면 로컬 게임을 시작한다
    private void Start()
    {
        if (Net == null)
            InitializeGame(CharacterType.King, CharacterType.Queen);
    }

    // 선택한 캐릭터로 새 매치를 시작한다
    public void InitializeGame(CharacterType p1Type, CharacterType p2Type)
    {
        foreach (Seat seat in _seats)
            seat.ResetMatch();

        _pot = 0;
        _isFirstRound = true;
        _lastLoser = ActivePlayer.None;

        SetCharacter(ActivePlayer.Player1, p1Type);
        SetCharacter(ActivePlayer.Player2, p2Type);

        _deck = NewDeck();
        ApplyCharacterAbilities();
        CharactersChanged?.Invoke();
        StartNewRound();
    }

    // 좌석의 에이스 선택이 확정됐는지 확인한다
    public bool IsAceConfirmed(ActivePlayer seat) => S(seat).Hand != null && S(seat).Hand.AceSelectionConfirmed;

    // 좌석의 캐릭터 이름을 반환한다
    public string GetCharacterName(ActivePlayer seat) => S(seat).Character?.GetName() ?? "-";

    // 새 덱을 만든다
    private static Deck NewDeck()
    {
        Deck deck = new Deck(includeJoker: false);
        deck.Shuffle();
        return deck;
    }

    // 두 좌석의 캐릭터 능력을 적용한다
    private void ApplyCharacterAbilities()
    {
        for (int i = 0; i < _seats.Length; i++)
            _seats[i].Character?.OnGameStart(this, (ActivePlayer)i);
    }

    // 좌석에 캐릭터를 지정한다
    public void SetCharacter(ActivePlayer player, CharacterType type)
    {
        S(player).Character = CreateCharacter(type);
        Debug.Log($"[캐릭터 선택] {player} / {type}");
    }

    // 종류에 맞는 캐릭터 객체를 만든다
    private static Character CreateCharacter(CharacterType type) => type switch
    {
        CharacterType.King => new KingCharacter(),
        CharacterType.Queen => new QueenCharacter(),
        CharacterType.Jack => new JackCharacter(),
        CharacterType.Joker => new JokerCharacter(),
        _ => null
    };

    // 아이템 슬롯 수를 설정한다
    public void SetItemSlots(ActivePlayer player, int slots) => S(player).ItemSlots = slots;
    // 칩을 추가한다
    public void AddChips(ActivePlayer player, int amount) => S(player).Chips += amount;
    // 덱에 조커를 넣는다
    public void InsertJokerCard() => _deck?.InsertJoker();

    // 새 라운드를 시작하고 상태를 전송한다
    public void StartNewRound()
    {
        BeginRound();
        Commit();
    }

    // 라운드 상태를 초기화하고 공유 카드와 첫 카드를 나눠준다
    private void BeginRound()
    {
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
        RoundStarted?.Invoke(_round);

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

    // 첫 차례 플레이어를 정한다
    private void DetermineFirstTurn()
    {
        if (!_isFirstRound && _lastLoser != ActivePlayer.None)
        {
            CurrentPlayer = _lastLoser;
            Debug.Log($"[선공결정] 이전 라운드 패자 → {CurrentPlayer}");
        }
        else
        {
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

    // 카드 목록을 섞는다
    private void Shuffle(List<Card> cards)
    {
        for (int n = cards.Count - 1; n > 0; n--)
        {
            int k = _random.Next(n + 1);
            (cards[k], cards[n]) = (cards[n], cards[k]);
        }
    }

    // 좌석에 카드를 한 장 나눠준다
    private void Deal(ActivePlayer seat, Card card)
    {
        PlayerHand hand = S(seat).Hand;
        hand.AddCard(card);

        IReadOnlyList<CardSlot> cards = hand.Cards;
        Card revealed = cards.Count > 1 ? cards[cards.Count - 2].Card : null;
        HostNet?.SendCard(seat, card, revealed);
        CardDealt?.Invoke(seat, cards.Count - 1);
    }

    // 현재 플레이어가 카드를 한 장 더 받는다
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

    // 현재 플레이어가 카드 받기를 마친다
    public void OnStand()
    {
        if (!CanStand) return;

        CurrentSeat.Hand.Stand();
        Debug.Log($"[스탠드] {CurrentPlayer}");

        if (!TryAdvancePhase())
            SwitchTurn();
        Commit();
    }

    // 차례를 상대에게 넘긴다
    private void SwitchTurn()
    {
        ActivePlayer next = Opponent(CurrentPlayer);

        if (S(next).Hand.IsStanding)
        {
            Debug.Log($"[턴 전환] 상대 스탠드 → {CurrentPlayer} 턴 유지");
            return;
        }

        CurrentPlayer = next;
        Debug.Log($"[턴 전환] → {CurrentPlayer}");
    }

    // 모두 스탠드했으면 에이스 선택 단계로 넘어간다
    private bool TryAdvancePhase()
    {
        if (!_seats.All(seat => seat.Hand.IsStanding)) return false;

        CurrentPhase = GamePhase.AceSelection;
        CurrentPlayer = ActivePlayer.None;
        Debug.Log("[에이스 선택 단계 시작]");
        return true;
    }

    // 조커를 뽑은 플레이어에게 패널티를 주고 라운드를 끝낸다
    private void HandleJokerDraw()
    {
        ActivePlayer drawer = CurrentPlayer;
        Debug.Log($"[조커] {drawer} 조커 드로우 → 즉시 패배");

        int penalty = Mathf.Min(JokerPenalty, S(drawer).Chips);
        Transfer(drawer, Opponent(drawer), penalty);
        Debug.Log($"[조커 패널티] {drawer} 칩 {penalty}개 징수 / P1: {Player1Chips} / P2: {Player2Chips}");

        CurrentPhase = GamePhase.RoundEnd;
        CurrentPlayer = ActivePlayer.None;

        if (!TryEndMatch())
            SettleRound(Opponent(drawer));
    }

    // 에이스 값을 11 또는 1로 바꾼다
    public void OnAceValueChanged(ActivePlayer player, int cardIndex, bool isEleven)
    {
        if (CurrentPhase != GamePhase.AceSelection) return;
        if (player == ActivePlayer.None) return;

        PlayerHand hand = S(player).Hand;
        if (hand.AceSelectionConfirmed) return;

        hand.SetAceEleven(cardIndex, isEleven);
        Debug.Log($"[에이스 선택] {player} / 인덱스: {cardIndex} / 값: {(isEleven ? 11 : 1)}");
    }

    // 에이스 선택을 확정하고 모두 확정되면 다음 단계로 넘어간다
    public void OnAceSelectionConfirmed(ActivePlayer player)
    {
        if (CurrentPhase != GamePhase.AceSelection) return;

        PlayerHand hand = S(player).Hand;
        hand.ConfirmAceSelection();
        Debug.Log($"[에이스 확정] {player} / 합계: {hand.GetTotal()}");

        if (player == ActivePlayer.Player1)
            HostNet?.SendOpponentAces(AceMask(hand, includeLast: false));

        if (_seats.All(seat => seat.Hand.AceSelectionConfirmed))
            EnterCardRevealPhase();
        Commit();
    }

    // 카드 공개 단계로 들어간다
    private void EnterCardRevealPhase()
    {
        CurrentPhase = GamePhase.CardReveal;
        CurrentPlayer = ActivePlayer.None;
        Debug.Log("[패 공개 단계 시작]");

        EnterBettingPhase();
    }

    // 카드 공개가 끝나면 베팅 단계로 넘어간다
    public void OnCardRevealCompleted()
    {
        if (CurrentPhase != GamePhase.CardReveal) return;
        EnterBettingPhase();
        Commit();
    }

    // 베팅 단계로 들어간다
    private void EnterBettingPhase()
    {
        CurrentPhase = GamePhase.Betting;
        CurrentPlayer = _bettingFirst;
        _isFirstBet = true;

        foreach (Seat seat in _seats)
            Pay(seat, BaseBet);

        Debug.Log($"[베팅 단계 시작] 선공: {CurrentPlayer} / 기본 베팅 각 {BaseBet}개 / 팟: {_pot} / P1칩: {Player1Chips} / P2칩: {Player2Chips}");
    }

    // 베팅 금액을 올린다
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

    // 상대 베팅에 맞춘다
    public void OnCall()
    {
        if (!CanCall) return;

        int amount = CallAmount;
        Pay(CurrentSeat, amount);
        Debug.Log($"[콜] {CurrentPlayer} / +{amount} / 팟: {_pot} / P1칩: {Player1Chips} / P2칩: {Player2Chips}");

        EnterRoundEnd(isDie: false);
        Commit();
    }

    // 라운드를 포기한다
    public void OnDie()
    {
        if (!CanDie) return;

        _diedPlayer = CurrentPlayer;
        Debug.Log($"[다이] {CurrentPlayer}");

        EnterRoundEnd(isDie: true);
        Commit();
    }

    // 좌석의 칩을 베팅에 넣는다
    private void Pay(Seat seat, int amount)
    {
        seat.Chips -= amount;
        seat.TotalBet += amount;
        _pot += amount;
    }

    // 칩을 한 좌석에서 다른 좌석으로 옮긴다
    private void Transfer(ActivePlayer from, ActivePlayer to, int amount)
    {
        S(from).Chips -= amount;
        S(to).Chips += amount;
    }

    // 마지막 카드를 공개하고 결과 연출을 시작한다
    private void EnterRoundEnd(bool isDie)
    {
        CurrentPhase = GamePhase.RoundEnd;
        CurrentPlayer = ActivePlayer.None;

        foreach (Seat seat in _seats)
            seat.Hand.RevealLastCard();
        Debug.Log("[마지막 카드 공개]");

        PlayerHand hostHand = _seats[0].Hand;
        HostNet?.SendReveal(hostHand.Cards[hostHand.Cards.Count - 1].Card, AceMask(hostHand, includeLast: true));
        RoundRevealed?.Invoke();

        StartCoroutine(RoundEndSequence(isDie));
    }

    // 연출 시간이 지난 뒤 승자를 판정한다
    private IEnumerator RoundEndSequence(bool isDie)
    {
        yield return new WaitForSeconds(RoundEndRevealDuration);
        DetermineWinner(isDie);
        Commit();
    }

    // 승자를 판정해 팟을 정산한다
    private void DetermineWinner(bool isDie)
    {
        PlayerHand h1 = _seats[0].Hand;
        PlayerHand h2 = _seats[1].Hand;
        Debug.Log($"[최종 합계] P1: {h1.GetTotal()} / P2: {h2.GetTotal()}");
        Debug.Log($"[버스트 여부] P1: {h1.IsBusted} / P2: {h2.IsBusted}");

        ActivePlayer winner = GetWinner();

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

        if (isDie && _diedPlayer != ActivePlayer.None && S(Opponent(_diedPlayer)).Hand.IsBusted)
        {
            int penalty = Mathf.Min(DiePenalty, S(_diedPlayer).Chips);
            Transfer(_diedPlayer, Opponent(_diedPlayer), penalty);
            Debug.Log($"[다이 패널티] {_diedPlayer} 칩 {penalty}개 추가 징수");
        }

        if (TryEndMatch()) return;
        SettleRound(winner);
    }

    // 에이스 선택 상태를 비트마스크로 만든다
    private static int AceMask(PlayerHand hand, bool includeLast)
    {
        int mask = 0;
        int count = includeLast ? hand.Cards.Count : hand.Cards.Count - 1;
        for (int i = 0; i < count; i++)
            if (hand.Cards[i].Card.Rank == Rank.Ace && hand.IsAceEleven(i))
                mask |= 1 << i;
        return mask;
    }

    // 합계와 버스트 여부로 승자를 구한다
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

    // 가장 높은 카드로 승자를 가린다
    private static ActivePlayer CompareHighestCard(PlayerHand h1, PlayerHand h2)
    {
        Card c1 = HighestCard(h1);
        Card c2 = HighestCard(h2);

        if (c1.Rank == c2.Rank && c1.Suit == c2.Suit)
            return ActivePlayer.None;
        return Outranks(c1, c2) ? ActivePlayer.Player1 : ActivePlayer.Player2;
    }

    // 손패에서 가장 높은 카드를 찾는다
    private static Card HighestCard(PlayerHand hand)
    {
        Card highest = hand.Cards[0].Card;
        foreach (CardSlot slot in hand.Cards)
            if (Outranks(slot.Card, highest))
                highest = slot.Card;
        return highest;
    }

    // 앞 카드가 뒤 카드보다 높은지 비교한다
    private static bool Outranks(Card a, Card b) =>
        a.Rank != b.Rank ? a.Rank > b.Rank : a.Suit > b.Suit;

    // 칩이 바닥난 플레이어가 있으면 매치를 끝낸다
    private bool TryEndMatch()
    {
        if (Player1Chips > 0 && Player2Chips > 0) return false;

        CurrentPhase = GamePhase.GameEnd;
        CurrentPlayer = ActivePlayer.None;
        Debug.Log($"[게임 종료] 승자: {(Player1Chips > 0 ? "P1" : "P2")}");
        return true;
    }

    // 연패를 집계하고 아이템 지급 뒤 다음 라운드로 넘어간다
    private void SettleRound(ActivePlayer winner)
    {
        if (winner != ActivePlayer.None)
        {
            ActivePlayer loser = Opponent(winner);
            _lastLoser = loser;
            S(winner).LoseStreak = 0;
            S(loser).LoseStreak++;

            CheckAndGiveItem(loser);
            if (CurrentPhase == GamePhase.ItemDiscard) return;
        }

        BeginRound();
    }

    // 상대가 나가면 매치를 즉시 끝내고 로비로 돌아간다
    public void HandleOpponentLeft()
    {
        if (CurrentPhase == GamePhase.GameEnd) return;

        CurrentPhase = GamePhase.GameEnd;
        CurrentPlayer = ActivePlayer.None;
        StopAllCoroutines();
        Debug.Log("[네트워크] 상대방 퇴장으로 매치 종료");

        _notifiedPhase = GamePhase.GameEnd;
        GameEnded?.Invoke(GameEndReason.OpponentLeft);

        NetworkManager nm = NetworkManager.Instance;
        if (nm == null) return;
        nm.PendingGameResultText = "OPPONENT LEFT - YOU WIN!";
        nm.ReturnAfterOpponentLeft();
    }

    // 연패 조건을 채우면 아이템을 지급한다
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

    // 플레이어에게 아이템을 추가한다
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

    // 아이템을 가지고 있는지 확인한다
    private bool HasItem(ActivePlayer player, ItemType type) => S(player).Items.Any(item => item.Type == type);

    // 아이템을 하나 제거한다
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

    // 아이템을 쓸 수 있는 상태인지 확인한다
    private bool CanUse(ActivePlayer player, ItemType type, GamePhase phase) =>
        CurrentPhase == phase && CurrentPlayer == player && HasItem(player, type);

    // 돋보기를 사용한다
    public Card OnUseMagnifier(ActivePlayer player)
    {
        if (!IsLocalSeat(player)) return null;
        if (IsMirror) { Net.RequestUseItem(ItemType.Magnifier); return null; }
        return HostUseMagnifier(player);
    }

    // 뇌물을 사용한다
    public bool OnUseBribe(ActivePlayer player)
    {
        if (!IsLocalSeat(player)) return false;
        if (IsMirror) { Net.RequestUseItem(ItemType.Bribe); return false; }
        return HostUseBribe(player);
    }

    // 플래시를 사용한다
    public bool OnUseFlash(ActivePlayer player)
    {
        if (!IsLocalSeat(player)) return false;
        if (IsMirror) { Net.RequestUseItem(ItemType.Flash); return false; }
        return HostUseFlash(player);
    }

    // 폐기할 아이템을 선택한다
    public void OnItemDiscard(ActivePlayer player, int discardIndex)
    {
        if (!IsLocalSeat(player)) return;
        if (IsMirror) { Net.RequestDiscard(discardIndex); return; }
        HostDiscard(player, discardIndex);
    }

    // 요청받은 아이템을 종류에 맞게 실행한다
    public void HostUseItem(ActivePlayer seat, ItemType type)
    {
        switch (type)
        {
            case ItemType.Magnifier: HostUseMagnifier(seat); break;
            case ItemType.Bribe: HostUseBribe(seat); break;
            case ItemType.Flash: HostUseFlash(seat); break;
        }
    }

    // 덱 맨 위 카드를 보여준다
    private Card HostUseMagnifier(ActivePlayer player)
    {
        if (!CanUse(player, ItemType.Magnifier, GamePhase.Draw)) return null;

        RemoveItem(player, ItemType.Magnifier);

        Card next = _deck.PeekTop();
        Debug.Log($"[돋보기] {player} / 다음 카드: {next}");
        RaiseItemUsed(player, ItemType.Magnifier);
        HostNet?.SendPeek(player, next);
        if (IsLocalSeat(player))
            PeekShown?.Invoke(next);
        Commit();
        return next;
    }

    // 자신의 마지막 카드를 새 카드로 교체하고 내준 카드는 덱 중간 아무 자리에 섞는다
    private bool HostUseBribe(ActivePlayer player)
    {
        if (!CanUse(player, ItemType.Bribe, GamePhase.Draw)) return false;

        PlayerHand hand = S(player).Hand;
        if (hand.Cards.Count == 0) return false;

        Card newCard = _deck.Draw();
        if (newCard == null) return false;

        Card traded = hand.Cards[hand.Cards.Count - 1].Card;
        hand.ReplaceLastCard(newCard);
        _deck.InsertRandomBelowTop(traded);
        RemoveItem(player, ItemType.Bribe);
        HostNet?.SendReplaceLast(player, newCard);

        Debug.Log($"[거래] {player} / 마지막 카드 교체");
        RaiseItemUsed(player, ItemType.Bribe);
        LastCardReplaced?.Invoke(player);
        Commit();
        return true;
    }

    // 상대의 칩을 빼앗는다
    private bool HostUseFlash(ActivePlayer player)
    {
        if (!CanUse(player, ItemType.Flash, GamePhase.Betting)) return false;

        int steal = Mathf.Min(FlashSteal, S(Opponent(player)).Chips);
        Transfer(Opponent(player), player, steal);
        RemoveItem(player, ItemType.Flash);

        Debug.Log($"[플래쉬] {player} / {steal}개 탈취 / P1칩: {Player1Chips} / P2칩: {Player2Chips}");
        RaiseItemUsed(player, ItemType.Flash);
        Commit();
        return true;
    }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
    // 테스트용으로 두 좌석에 아이템을 하나씩 주고 상태를 전송한다
    public void DebugGiveItemToBoth(ItemType type)
    {
        if (IsMirror) return;
        foreach (ActivePlayer seat in new[] { ActivePlayer.Player1, ActivePlayer.Player2 })
            if (S(seat).Items.Count < S(seat).ItemSlots) AddItem(seat, type);
        Commit();
    }

    // 테스트용으로 다음에 뽑힐 카드들을 에이스로 만든다
    public void DebugStackAces(int count)
    {
        if (IsMirror || _deck == null) return;
        for (int i = 0; i < count; i++)
            _deck.DebugPushTop(new Card(Rank.Ace, (Suit)(i % 4)));
        Debug.Log($"[테스트] 다음 {count}장 에이스");
    }
#endif

    // 아이템 사용을 게스트에 보내고 구독자에게 알린다
    private void RaiseItemUsed(ActivePlayer player, ItemType type)
    {
        HostNet?.SendItemUsed(player, type);
        ItemUsed?.Invoke(player, type);
    }

    // 폐기 선택을 처리하고 다음 라운드를 시작한다
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

    // 상태를 게스트에 보내고 구독자에게 알린다
    private void Commit()
    {
        HostNet?.PublishState(BuildSnapshot());
        NotifyStateChanged();
    }

    // 상태 변경을 알리고 게임 종료 단계로 처음 들어왔으면 종료도 알린다
    private void NotifyStateChanged()
    {
        StateChanged?.Invoke();

        if (CurrentPhase == GamePhase.GameEnd && _notifiedPhase != GamePhase.GameEnd)
        {
            _notifiedPhase = GamePhase.GameEnd;
            GameEnded?.Invoke(GameEndReason.Normal);
            return;
        }
        _notifiedPhase = CurrentPhase;
    }

    // 현재 공개 상태를 스냅샷으로 만든다
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

    // 좌석의 캐릭터를 번호로 바꾼다
    private static byte CharacterCode(Seat seat) =>
        seat.Character == null ? GameSnapshot.None : (byte)seat.Character.Type;

    // 받은 스냅샷을 게스트의 상태에 반영한다
    public void MirrorApply(in GameSnapshot s)
    {
        if (s.Round == 0) return;

        _round = s.Round;
        CurrentPhase = (GamePhase)s.Phase;
        CurrentPlayer = (ActivePlayer)s.Current;
        _pot = s.Pot;
        _isFirstBet = s.IsFirstBet;

        bool characterChanged =
            ApplySeat(_seats[0], s.P1Chips, s.P1TotalBet, s.P1Slots, s.P1Items, s.P1Character) |
            ApplySeat(_seats[1], s.P2Chips, s.P2TotalBet, s.P2Slots, s.P2Items, s.P2Character);
        if (characterChanged) CharactersChanged?.Invoke();

        _pendingItem = s.PendingItem == GameSnapshot.None ? null : new Item((ItemType)s.PendingItem);
        _pendingItemPlayer = (ActivePlayer)s.PendingItemPlayer;
        NotifyStateChanged();
    }

    // 스냅샷 값을 좌석에 반영하고 캐릭터가 바뀌었는지 알려준다
    private static bool ApplySeat(Seat seat, int chips, int totalBet, byte slots, byte items, byte character)
    {
        seat.Chips = chips;
        seat.TotalBet = totalBet;
        seat.ItemSlots = slots;
        GameSnapshot.UnpackItems(items, seat.Items);

        CharacterType? type = character == GameSnapshot.None ? (CharacterType?)null : (CharacterType)character;
        if (seat.Character?.Type == type) return false;

        seat.Character = type.HasValue ? CreateCharacter(type.Value) : null;
        return true;
    }

    // 게스트에서 새 라운드를 시작한다
    public void MirrorRoundStart(int round, Card sharedCard)
    {
        _round = round;
        _sharedCard = sharedCard;
        foreach (Seat seat in _seats)
            seat.Hand = new PlayerHand(sharedCard);
        PeekedCard = null;
        RoundStarted?.Invoke(round);
        NotifyStateChanged();
    }

    // 게스트에서 카드가 나뉜 것을 반영한다
    public void MirrorCard(ActivePlayer seat, Card card, Card revealedPrevious)
    {
        PlayerHand hand = S(seat).Hand;
        if (hand == null) return;

        if (revealedPrevious != null)
            hand.ReplaceLastCard(revealedPrevious);
        hand.AddCard(card);
        CardDealt?.Invoke(seat, hand.Cards.Count - 1);
        NotifyStateChanged();
    }

    // 게스트에서 마지막 카드를 교체한다
    public void MirrorReplaceLast(ActivePlayer seat, Card card)
    {
        PlayerHand hand = S(seat).Hand;
        if (hand == null) return;

        hand.ReplaceLastCard(card);
        LastCardReplaced?.Invoke(seat);
        NotifyStateChanged();
    }

    // 게스트에서 아이템 사용을 알린다
    public void MirrorItemUsed(ActivePlayer seat, ItemType type) => ItemUsed?.Invoke(seat, type);

    // 게스트에서 돋보기 결과를 보여준다
    public void MirrorPeek(Card card)
    {
        PeekedCard = card;
        Debug.Log($"[돋보기] 다음 카드: {card}");
        PeekShown?.Invoke(card);
    }

    // 게스트에서 라운드 종료 시 상대 카드를 공개한다
    public void MirrorReveal(Card hostLastCard, int hostAceMask)
    {
        Seat host = S(ActivePlayer.Player1);
        if (host.Hand != null && host.Hand.Cards.Count > 0 && hostLastCard != null)
        {
            PlayerHand rebuilt = new PlayerHand(_sharedCard);
            for (int i = 0; i < host.Hand.Cards.Count - 1; i++)
                rebuilt.AddCard(host.Hand.Cards[i].Card);
            rebuilt.AddCard(hostLastCard);
            ApplyAceMask(rebuilt, hostAceMask);
            host.Hand = rebuilt;
        }

        foreach (Seat seat in _seats)
            seat.Hand?.RevealLastCard();

        CurrentPhase = GamePhase.RoundEnd;
        CurrentPlayer = ActivePlayer.None;
        RoundRevealed?.Invoke();
        NotifyStateChanged();
    }

    // 게스트에서 상대의 에이스 선택을 반영한다
    public void MirrorOpponentAces(int mask)
    {
        PlayerHand hostHand = S(ActivePlayer.Player1).Hand;
        if (hostHand == null) return;

        ApplyAceMask(hostHand, mask);
        NotifyStateChanged();
    }

    // 비트마스크대로 에이스 값을 지정한다
    private static void ApplyAceMask(PlayerHand hand, int mask)
    {
        for (int i = 0; i < hand.Cards.Count; i++)
            hand.SetAceEleven(i, (mask & (1 << i)) != 0);
        hand.ConfirmAceSelection();
    }

    // 게스트에서 에이스 값을 바꾼다
    public void MirrorAceValue(ActivePlayer seat, int cardIndex, bool isEleven) =>
        S(seat).Hand?.SetAceEleven(cardIndex, isEleven);

    // 게스트에서 에이스 선택을 확정한다
    public void MirrorAceConfirm(ActivePlayer seat)
    {
        S(seat).Hand?.ConfirmAceSelection();
        NotifyStateChanged();
    }

    // 좌석의 합계를 화면에 쓸 글자로 만든다
    public string GetTotalText(ActivePlayer seat)
    {
        PlayerHand hand = S(seat).Hand;
        if (hand == null) return "0";

        if (seat == LocalSeat || hand.Cards.Count == 0)
            return hand.GetTotal().ToString();

        return hand.Cards[hand.Cards.Count - 1].IsHidden
            ? $"{hand.GetVisibleTotal()}+?"
            : hand.GetTotal().ToString();
    }
}