using UnityEngine;
using System.Collections.Generic;

public enum GamePhase
{
    FirstTurnDetermination,
    Draw,
    AceSelection,
    CardReveal,
    Betting,
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

    private Deck _deck;
    private PlayerHand _player1Hand;
    private PlayerHand _player2Hand;
    private Card _sharedCard;

    private int _player1Chips = 40;
    private int _player2Chips = 40;
    private int _pot = 0;
    private int _player1TotalBet = 0;
    private int _player2TotalBet = 0;

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
        StartNewRound(includeJoker: false);
    }

    public void StartNewRound(bool includeJoker)
    {

        if (CurrentPhase == GamePhase.GameEnd)
        {
            _player1Chips = 40;
            _player2Chips = 40;
            _pot = 0;
        }

        UIManager.Instance?.HideGameEndPanel();
        _deck = new Deck(includeJoker);
        _deck.Shuffle();

        _sharedCard = _deck.Draw();
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

        RefreshUI();
    }

    private void DetermineFirstTurn()
    {
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
        if (winner == ActivePlayer.None)
        {
            // 무승부 → 팟 유지하고 다음 라운드로
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

        // 게임 종료 체크
        if (_player1Chips <= 0 || _player2Chips <= 0)
        {
            CurrentPhase = GamePhase.GameEnd;
            Debug.Log($"[게임 종료] 승자: {(_player1Chips > 0 ? "P1" : "P2")}");
            return;
        }

        StartNewRound(includeJoker: false);
    }

    private void HandleJokerDraw()
    {
        Debug.Log($"[조커] {CurrentPlayer} 조커 드로우 → 즉시 패배");

        if (CurrentPlayer == ActivePlayer.Player1)
        {
            _player1Chips -= 10;
            _player2Chips += 10;
        }
        else
        {
            _player2Chips -= 10;
            _player1Chips += 10;
        }

        CurrentPhase = GamePhase.RoundEnd;
        CurrentPlayer = ActivePlayer.None;

        if (_player1Chips <= 0 || _player2Chips <= 0)
        {
            CurrentPhase = GamePhase.GameEnd;
            Debug.Log($"[게임 종료] 승자: {(_player1Chips > 0 ? "P1" : "P2")}");
            return;
        }

        StartNewRound(includeJoker: false);
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
}