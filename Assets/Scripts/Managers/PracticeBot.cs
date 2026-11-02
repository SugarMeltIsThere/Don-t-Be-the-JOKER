using System.Collections.Generic;
using UnityEngine;

public class PracticeBot : MonoBehaviour
{
    private const ActivePlayer Seat = ActivePlayer.Player2;
    private const ActivePlayer Human = ActivePlayer.Player1;
    private const int Blackjack = 21;
    private const float MinThink = 0.9f;
    private const float MaxThink = 1.8f;
    private const float FirstDealWait = 1.2f;
    private const int MaxOwnBet = 15;

    private GameManager _gm;
    private float _actAt = -1f;
    private Card _peeked;

    // 게임 관리자를 잡아 둔다
    private void Start() => _gm = GameManager.Instance;

    // 내가 움직여야 할 때가 오면 잠시 생각하는 척한 뒤 한 번 움직인다
    private void Update()
    {
        if (_gm == null) return;
        if (!NeedsAction())
        {
            _actAt = -1f;
            return;
        }
        if (_actAt < 0f)
        {
            _actAt = Time.time + Random.Range(MinThink, MaxThink) + (_gm.GetPlayer2Cards()?.Count == 1 ? FirstDealWait : 0f);
            return;
        }
        if (Time.time < _actAt) return;
        _actAt = -1f;
        Act();
    }

    // 지금 내 차례이거나 내가 정해야 할 것(에이스, 아이템 버리기)이 있는지 확인한다
    private bool NeedsAction()
    {
        switch (_gm.CurrentPhase)
        {
            case GamePhase.Draw:
            case GamePhase.Betting:
                return _gm.CurrentPlayer == Seat;
            case GamePhase.AceSelection:
                return !_gm.IsAceConfirmed(Seat);
            case GamePhase.ItemDiscard:
                return _gm.PendingItemPlayer == Seat;
            default:
                return false;
        }
    }

    // 단계에 맞는 판단을 한 가지 실행한다
    private void Act()
    {
        switch (_gm.CurrentPhase)
        {
            case GamePhase.Draw: ActDraw(); break;
            case GamePhase.AceSelection: ActAces(); break;
            case GamePhase.Betting: ActBet(); break;
            case GamePhase.ItemDiscard: _gm.HostDiscard(Seat, -1); break;
        }
    }

    // 버스트면 거래로 마지막 카드를 바꾸고, 애매하면 돋보기로 다음 카드를 본 뒤, 합계와 상대 예상에 따라 받거나 종을 누른다
    private void ActDraw()
    {
        IReadOnlyList<CardSlot> cards = _gm.GetPlayer2Cards();
        int hard = HardTotal(cards, includeHidden: true);
        int best = BestTotal(hard, AceCount(cards));

        if (hard > Blackjack)
        {
            if (Has(ItemType.Bribe)) _gm.HostUseItem(Seat, ItemType.Bribe);
            else _gm.OnStand(rang: true);
            return;
        }

        if (_peeked != null)
        {
            Card next = _peeked;
            _peeked = null;
            bool safe = next.Rank != Rank.Joker && hard + next.GetBlackjackValue() <= Blackjack;
            if (safe && best < 20 && _gm.CanHit) _gm.OnHit();
            else _gm.OnStand(rang: true);
            return;
        }

        if (Has(ItemType.Magnifier) && best >= 12 && best <= 18)
        {
            _peeked = _gm.HostUseMagnifierFor(Seat);
            return;
        }

        int opponentGuess = OpponentGuess();
        bool behind = _gm.IsStanding(Human) && opponentGuess > best && best < 19;
        if (_gm.CanHit && (best < 17 || behind)) _gm.OnHit();
        else _gm.OnStand(rang: true);
    }

    // 11로 써도 21을 넘지 않는 에이스 하나만 11로 정하고 확정한다
    private void ActAces()
    {
        IReadOnlyList<CardSlot> cards = _gm.GetPlayer2Cards();
        int hard = HardTotal(cards, includeHidden: true);
        for (int i = 0; i < cards.Count; i++)
        {
            if (cards[i].Card.Rank != Rank.Ace || hard + 10 > Blackjack) continue;
            _gm.OnAceValueChanged(Seat, i, true);
            break;
        }
        _gm.OnAceSelectionConfirmed(Seat);
    }

    // 손패의 세기를 어림해 플래시를 쓰거나, 레이즈, 콜, 다이 중 하나를 고른다
    private void ActBet()
    {
        if (Has(ItemType.Flash) && Random.value < 0.5f)
        {
            _gm.HostUseItem(Seat, ItemType.Flash);
            return;
        }

        float strength = Strength() + Random.Range(-0.1f, 0.1f);
        int myBet = _gm.Player2TotalBet;

        if (_gm.IsFirstBet)
        {
            if (strength > 0.35f && _gm.CanRaise) _gm.OnRaise(Mathf.Clamp(strength > 0.75f ? Random.Range(2, 5) : 1, 1, _gm.MaxRaise));
            else _gm.OnDie();
            return;
        }

        if (strength > 0.8f && _gm.CanRaise && myBet < MaxOwnBet) _gm.OnRaise(Mathf.Clamp(Random.Range(1, 4), 1, _gm.MaxRaise));
        else if (_gm.CanCall && strength > 0.3f + _gm.CallAmount * 0.04f) _gm.OnCall();
        else _gm.OnDie();
    }

    // 확정된 내 합계와 상대의 보이는 패로 0~1 사이 세기를 어림한다
    private float Strength()
    {
        IReadOnlyList<CardSlot> cards = _gm.GetPlayer2Cards();
        int total = BestTotal(HardTotal(cards, includeHidden: true), AceCount(cards));
        if (total > Blackjack) return 0.05f;

        float strength = 0.25f + (total - 12) / 9f * 0.7f;
        int visible = HardTotal(_gm.GetPlayer1Cards(), includeHidden: false);
        if (total >= OpponentGuess() + 2) strength += 0.1f;
        if (total < visible) strength -= 0.3f;
        return Mathf.Clamp(strength, 0.1f, 0.95f);
    }

    // 상대의 보이는 카드에 가려진 한 장의 평균값을 더해 상대 합계를 어림한다
    private int OpponentGuess() => HardTotal(_gm.GetPlayer1Cards(), includeHidden: false) + 6;

    // 공유 카드와 손패를 에이스 1로 더한다(가려진 카드를 뺄 수 있다)
    private int HardTotal(IReadOnlyList<CardSlot> cards, bool includeHidden)
    {
        int total = _gm.SharedCard != null ? _gm.SharedCard.GetBlackjackValue() : 0;
        if (cards == null) return total;
        foreach (CardSlot slot in cards)
            if (includeHidden || !slot.IsHidden) total += slot.Card.GetBlackjackValue();
        return total;
    }

    // 손패의 에이스 수를 센다
    private static int AceCount(IReadOnlyList<CardSlot> cards)
    {
        int count = 0;
        if (cards == null) return 0;
        foreach (CardSlot slot in cards)
            if (slot.Card.Rank == Rank.Ace) count++;
        return count;
    }

    // 에이스 하나를 11로 쳐도 넘지 않으면 그 값을 쓴다
    private static int BestTotal(int hard, int aces) => aces > 0 && hard + 10 <= Blackjack ? hard + 10 : hard;

    // 그 아이템을 가지고 있는지 확인한다
    private bool Has(ItemType type)
    {
        foreach (Item item in _gm.Player2Items)
            if (item.Type == type) return true;
        return false;
    }
}
