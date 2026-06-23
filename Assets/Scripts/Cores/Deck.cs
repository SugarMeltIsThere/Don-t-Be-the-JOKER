using System.Collections.Generic;

public class Deck
{
    private List<Card> _cards;
    private System.Random _random;

    public int Count => _cards.Count;   //읽기 전용 값으로 프로퍼티 선언

    public Deck(bool includeJoker = false)
    {
        _cards = new List<Card>(209);
        _random = new System.Random();  //연속된 아주 빠른 메서드 호출로 중복시드가 생성되는 걸 방지. 턴제 및 드로우 딜레이가 있는 해당 게임에서는 해당 방식을 써도 문제되지는 않음.
        Initialize(includeJoker);
    }

    private void Initialize(bool includeJoker)  //게임 진행 중 덱을 전부 소진했을 시 새 덱을 생성하는 메서드를 따로 만드는 대신 Initailize 메서드를 다시 호출하는 방식으로 재사용 가능
    {
        for (int set = 0; set < 4; set++)
        {
            foreach (Suit suit in System.Enum.GetValues(typeof(Suit)))
            {
                for (Rank rank = Rank.Ace; rank <= Rank.King; rank++)
                {
                    _cards.Add(new Card(rank, suit));
                }
            }
        }
        if (includeJoker)
            _cards.Add(new Card(Rank.Joker, Suit.Spade));
    }

    public void Shuffle()   //무작위성 보장을 위한 Fisher Yates Shuffle 알고리즘
    {
        int n = _cards.Count;
        while (n > 1)
        {
            n--;
            int k= _random.Next(n+1);
            Card temp = _cards[k];
            _cards[k] = _cards[n];
            _cards[n] = temp;
        }
    }

    public Card Draw()
    {
        if (_cards.Count == 0) return null;

        Card card = _cards[_cards.Count - 1];   //List의 형식상 마지막 카드를 꺼내는 것이 더욱 효율적. Fisher Yates Shuffle 알고리즘으로 어디서 꺼내든 무작위성은 동일
        _cards.RemoveAt(_cards.Count - 1);
        return card;
    }
}