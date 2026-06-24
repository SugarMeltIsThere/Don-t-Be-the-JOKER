using UnityEngine;
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

    public IReadOnlyList<CardSlot> GetPlayer1Cards()
    => _player1Hand?.Cards;

    public IReadOnlyList<CardSlot> GetPlayer2Cards()
        => _player2Hand?.Cards;
    public Item PendingItem => _pendingItem;
    public ActivePlayer PendingItemPlayer => _pendingItemPlayer;

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
    private const int KingItemSlots = 3;

    // 각 플레이어 슬롯 크기
    private int _player1ItemSlots = DefaultItemSlots;
    private int _player2ItemSlots = DefaultItemSlots;

    private Item _pendingItem = null;
    private ActivePlayer _pendingItemPlayer = ActivePlayer.None;
    private ActivePlayer _lastWinner = ActivePlayer.None;
    private bool _isFirstRound = true;
    private ActivePlayer _lastLoser = ActivePlayer.None;

    // UI 표시용
    public int Player1ItemSlots => _player1ItemSlots;
    public int Player2ItemSlots => _player2ItemSlots;
    public CharacterType? Player1Character => _player1Character?.Type;
    public CharacterType? Player2Character => _player2Character?.Type;

    private List<Item> _player1Items = new List<Item>();
    private List<Item> _player2Items = new List<Item>();
    private bool _isMagnifierActive = false;

    // UI 표시용
    public IReadOnlyList<Item> Player1Items => _player1Items;
    public IReadOnlyList<Item> Player2Items => _player2Items;
    public bool IsMagnifierActive => _isMagnifierActive;

    private bool _isFirstBet = true;
    private ActivePlayer _bettingFirst;
    private ActivePlayer _diedPlayer = ActivePlayer.None;

    private System.Random _random = new System.Random();

    public GamePhase CurrentPhase { get; private set; }
    public ActivePlayer CurrentPlayer { get; private set; }

    // UI 표시용
    public int Player1Chips => _player1Chips;
    public int Player2Chips => _player2Chips;
    public int Pot => _pot;
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

            return Mathf.Max(0, myChips - CallAmount);
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
        !GetCurrentHand().MustStand;

    public bool CanStand =>
        CurrentPhase == GamePhase.Draw &&
        CurrentPlayer != ActivePlayer.None;

    public bool CanDie =>
        CurrentPhase == GamePhase.Betting &&
        CurrentPlayer != ActivePlayer.None;

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

        SetCharacter(ActivePlayer.Player1, CharacterType.Jack);
        SetCharacter(ActivePlayer.Player2, CharacterType.Joker);
        

        // 게임 시작 시 덱 한 번만 생성
        _deck = new Deck(includeJoker: false);
        _deck.Shuffle();
        //덱생성 후 능력 발동해야 조커 능력 발동 가능
        ApplyCharacterAbilities();
        StartNewRound();
    }
    private void ApplyCharacterAbilities()
    {
        _player1Character?.OnGameStart(this, ActivePlayer.Player1);
        _player2Character?.OnGameStart(this, ActivePlayer.Player2);
    }
    public void RestartGame()
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

        ApplyCharacterAbilities();

        // 재시작 시 덱 새로 생성
        _deck = new Deck(includeJoker: false);
        _deck.Shuffle();

        StartNewRound();
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
        _deck.InsertJoker();
    }

    public void StartNewRound()
    {
        UIManager.Instance?.HideGameEndPanel();

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

        if (!TryAdvancePhase())
            SwitchTurn();

        RefreshUI();
    }

    public void OnStand()
    {
        if (!CanStand) return;

        GetCurrentHand().Stand();
        Debug.Log($"[스탠드] {CurrentPlayer}");

        if (!TryAdvancePhase())
            SwitchTurn();

        RefreshUI();
    }

    private void SwitchTurn()
    {
        CurrentPlayer = CurrentPlayer == ActivePlayer.Player1
            ? ActivePlayer.Player2
            : ActivePlayer.Player1;

        if (GetCurrentHand().IsStanding)
        {
            CurrentPlayer = CurrentPlayer == ActivePlayer.Player1
                ? ActivePlayer.Player2
                : ActivePlayer.Player1;
        }

        Debug.Log($"[턴 전환] → {CurrentPlayer}");
    }

    private bool TryAdvancePhase()
    {
        if (!_player1Hand.IsStanding || !_player2Hand.IsStanding) return false;

        CurrentPhase = GamePhase.AceSelection;
        CurrentPlayer = ActivePlayer.None;
        Debug.Log("[에이스 선택 단계 시작]");
        return true;
    }

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

        RefreshUI(); // 추가

        if (_player1Hand.AceSelectionConfirmed &&
            _player2Hand.AceSelectionConfirmed)
        {
            UIManager.Instance.HideAceSelectionPanel();
            EnterCardRevealPhase();
        }
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
    }

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

    private void EnterRoundEnd(bool isDie)
    {
        CurrentPhase = GamePhase.RoundEnd;
        CurrentPlayer = ActivePlayer.None;

        Debug.Log("[마지막 카드 공개]");
        DetermineWinner(isDie);
    }

    private void DetermineWinner(bool isDie)
    {
        int p1Total = _player1Hand.GetTotal();
        int p2Total = _player2Hand.GetTotal();
        bool p1Busted = _player1Hand.IsBusted;
        bool p2Busted = _player2Hand.IsBusted;

        Debug.Log($"[최종 합계] P1: {p1Total} / P2: {p2Total}");
        Debug.Log($"[버스트 여부] P1: {p1Busted} / P2: {p2Busted}");

        // 다이 패널티 체크
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

        ActivePlayer winner = GetWinner(p1Total, p2Total, p1Busted, p2Busted);
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

        int p1Value = p1Highest.GetBlackjackValue();
        int p2Value = p2Highest.GetBlackjackValue();

        if (p1Value != p2Value)
            return p1Value > p2Value
                ? ActivePlayer.Player1
                : ActivePlayer.Player2;

        if (p1Highest.Suit != p2Highest.Suit)
            return p1Highest.Suit > p2Highest.Suit
                ? ActivePlayer.Player1
                : ActivePlayer.Player2;

        // 완전 동점 → 무승부
        return ActivePlayer.None;
    }

    private Card GetHighestCard(PlayerHand hand)
    {
        Card highest = hand.Cards[0].Card;

        foreach (CardSlot slot in hand.Cards)
        {
            Card card = slot.Card;

            if (card.GetBlackjackValue() > highest.GetBlackjackValue())
            {
                highest = card;
                continue;
            }

            if (card.GetBlackjackValue() == highest.GetBlackjackValue() &&
                card.Suit > highest.Suit)
                highest = card;
        }

        return highest;
    }

    private void SettleChips(ActivePlayer winner)
    {
        _lastWinner = winner;
        if (winner == ActivePlayer.Player1)
        {
            _lastLoser = ActivePlayer.Player2;
            _player1LoseStreak = 0;
            _player2LoseStreak++;
            CheckAndGiveItem(ActivePlayer.Player2);
        }
        else if (winner == ActivePlayer.Player2)
        {
            _lastLoser = ActivePlayer.Player1;
            _player2LoseStreak = 0;
            _player1LoseStreak++;
            CheckAndGiveItem(ActivePlayer.Player1);
        }

        // ItemDiscard 페이즈면 여기서 멈춤
        if (CurrentPhase == GamePhase.ItemDiscard) return;

        // 칩 정산
        if (winner == ActivePlayer.None)
        {
            Debug.Log($"[무승부] 팟 {_pot}개 보류");
        }
        else if (winner == ActivePlayer.Player1)
        {
            _player1Chips += _pot;
            _pot = 0;
            Debug.Log($"[P1 승리] P1: {_player1Chips} / P2: {_player2Chips}");
        }
        else
        {
            _player2Chips += _pot;
            _pot = 0;
            Debug.Log($"[P2 승리] P1: {_player1Chips} / P2: {_player2Chips}");
        }

        if (_player1Chips <= 0 || _player2Chips <= 0)
        {
            CurrentPhase = GamePhase.GameEnd;
            Debug.Log($"[게임 종료] 승자: {(_player1Chips > 0 ? "P1" : "P2")}");
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
        }
        else
        {
            _pendingItem = new Item(random);
            _pendingItemPlayer = player;
            CurrentPhase = GamePhase.ItemDiscard;
            Debug.Log($"[아이템 슬롯 가득] {player} / {random} 폐기 선택 필요");
        }
    }

    private void HandleJokerDraw()
    {
        Debug.Log($"[조커] {CurrentPlayer} 조커 드로우 → 즉시 패배");

        int penalty = 10;

        if (CurrentPlayer == ActivePlayer.Player1)
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

        Debug.Log($"[조커 패널티] {CurrentPlayer} 칩 {penalty}개 징수 / P1: {_player1Chips} / P2: {_player2Chips}");

        CurrentPhase = GamePhase.RoundEnd;
        CurrentPlayer = ActivePlayer.None;

        if (_player1Chips <= 0 || _player2Chips <= 0)
        {
            CurrentPhase = GamePhase.GameEnd;
            Debug.Log($"[게임 종료] 승자: {(_player1Chips > 0 ? "P1" : "P2")}");
            return;
        }

        StartNewRound();
    }

    private PlayerHand GetCurrentHand()
    {
        return CurrentPlayer == ActivePlayer.Player1
            ? _player1Hand
            : _player2Hand;
    }

    private void RefreshUI()
    {
        if (UIManager.Instance == null) return;

        UIManager.Instance.RefreshCards(
            sharedCard: _sharedCard?.ToString() ?? "-",
            p1Cards: GetCardsText(_player1Hand),
            p1Total: _player1Hand?.GetTotal() ?? 0,
            p2Cards: GetCardsText(_player2Hand),
            p2Total: _player2Hand?.GetTotal() ?? 0
        );
    }

    private string GetCardsText(PlayerHand hand)
    {
        if (hand == null) return "-";

        System.Text.StringBuilder sb = new System.Text.StringBuilder();

        for (int i = 0; i < hand.Cards.Count; i++)
        {
            CardSlot slot = hand.Cards[i];

            if (slot.IsHidden)
                sb.Append("? ");
            else
                sb.Append($"{slot.Card} ");
        }

        return sb.ToString().TrimEnd();
    }

    // 돋보기
    public Card OnUseMagnifier(ActivePlayer player)
    {
        if (CurrentPhase != GamePhase.Draw) return null;
        if (CurrentPlayer != player) return null;
        if (!HasItem(player, ItemType.Magnifier)) return null;

        RemoveItem(player, ItemType.Magnifier);
        _isMagnifierActive = true;

        Card next = _deck.PeekTop();
        Debug.Log($"[돋보기] {player} / 다음 카드: {next}");
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

        UIManager.Instance.HideItemDiscardPanel();

        // 칩 정산 마저 처리
        ActivePlayer winner = _lastWinner; // 승자 기록 필요
        if (winner == ActivePlayer.Player1)
        {
            _player1Chips += _pot;
            _pot = 0;
        }
        else
        {
            _player2Chips += _pot;
            _pot = 0;
        }

        if (_player1Chips <= 0 || _player2Chips <= 0)
        {
            CurrentPhase = GamePhase.GameEnd;
            return;
        }

        StartNewRound();
    }


}