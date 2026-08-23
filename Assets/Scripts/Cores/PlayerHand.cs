using System.Collections.Generic;

public class CardSlot
{
    public Card Card { get; init; }
    public bool IsHidden { get; set; }

    public CardSlot(Card card, bool isHidden)
    {
        Card = card;
        IsHidden = isHidden;
    }
}
public class PlayerHand
{
    private List<CardSlot> _cards;
    private Card _sharedCard;
    public IReadOnlyList<CardSlot> Cards => _cards;     //가변성의 내용물에 대한 접근을 막기 위해 private set 대산 IReadOnlyList 사용
    public bool IsBusted { get; private set; }
    public bool IsStanding { get; private set; }
    public bool MustStand { get; private set; }
    public bool AceSelectionConfirmed { get; private set; }

    private HashSet<int> _aceElevenIndices;


    public PlayerHand(Card sharedCard)
    {
        _cards = new List<CardSlot>(10);
        _sharedCard = sharedCard;
        _aceElevenIndices = new HashSet<int>();
    }

    public void AddCard(Card card)
    {
        if (_cards.Count > 0)
            _cards[_cards.Count - 1].IsHidden = false;

        _cards.Add(new CardSlot(card, isHidden: true));

        IsBusted = GetTotal() > 21;
        if (IsBusted) MustStand = true;
    }


    public void Stand()
    {
        IsStanding = true;
    }

    // 라운드 종료 시 숨겨져 있던 마지막 카드를 공개
    public void RevealLastCard()
    {
        if (_cards.Count == 0) return;
        _cards[_cards.Count - 1].IsHidden = false;
    }

    public void SetAceEleven(int cardIndex, bool isEleven)  //i번째 카드
    {
        if (AceSelectionConfirmed) return;
        if (cardIndex < 0 || cardIndex >= _cards.Count) return;
        if (_cards[cardIndex].Card.Rank != Rank.Ace) return;

        if (isEleven)
            _aceElevenIndices.Add(cardIndex);
        else
            _aceElevenIndices.Remove(cardIndex);
    }
    public void ConfirmAceSelection()
    {
        AceSelectionConfirmed = true;
    }

    public int GetTotal()   //본인 패의 합산
    {
        int total = _sharedCard.GetBlackjackValue();

        for (int i = 0; i < _cards.Count; i++)
        {
            Card card = _cards[i].Card;

            if (card.Rank == Rank.Ace)
            {
                total += AceSelectionConfirmed &&
                         _aceElevenIndices.Contains(i)
                    ? 11
                    : 1;
            }
            else
            {
                total += card.GetBlackjackValue();
            }
        }
        return total;
    }

    public int GetVisibleTotal()    //상대의 숨긴 패 한 장을 제외하고 합산 (공개패 + ?)
    {
        int total = _sharedCard.GetBlackjackValue();

        for (int i = 0; i < _cards.Count - 1; i++)
        {
            Card card = _cards[i].Card;

            if (card.Rank == Rank.Ace)
            {
                total += AceSelectionConfirmed &&
                         _aceElevenIndices.Contains(i)
                    ? 11
                    : 1;
            }
            else
            {
                total += card.GetBlackjackValue();
            }
        }
        return total;
    }

    public void ReplaceLastCard(Card newCard)
    {
        if (_cards.Count == 0) return;

        _cards[_cards.Count - 1] = new CardSlot(newCard, isHidden: true);
        IsBusted = GetTotal() > 21;
        if (IsBusted) MustStand = true;
    }
}