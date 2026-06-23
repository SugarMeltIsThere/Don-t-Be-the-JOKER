public enum Suit    //Suit의 우위를 확인하기 위해 수체계로 지정
{
    Clover = 0,
    Heart = 1,
    Diamond = 2,
    Spade = 3
}

public enum Rank    //각 랭크의 밸류를 직접 점수로 쓰기 위해 1부터 시작, 조커는 예외처리를 위해 일반 카드 밖의 범위인 14로 지정
{
    Ace = 1,
    Two = 2,
    Three = 3,
    Four = 4,
    Five = 5,
    Six = 6,
    Seven = 7,
    Eight = 8,
    Nine = 9,
    Ten = 10,
    Jack = 11,
    Queen = 12,
    King = 13,
    Joker = 14
}

public class Card
{
    public Rank Rank { get; init; }  //한번 만들어진 카드의 정보를 외부에서 가져갈 수 있게, 하지만 한번 생성되면 수정될 수 없도록 프로퍼티 사용 (멀티플레이시 임의로 카드 값을 바꾸는 핵 방지)
    public Suit Suit { get; init; }  //마찬가지

    public Card(Rank rank, Suit suit)
    {
        Rank = rank;
        Suit = suit;
    }

    public int GetBlackjackValue() => Rank switch //JOKER 예외처리 및 페이스카드의 점수를 10으로 변환. 순차 확인이 아닌 값의 분류이므로 Switch 형태로 분기. JOKER는 14이므로 >=JACK에 걸리지 않도록 맨 앞으로 분류
    {
        Rank.Joker   => 0,
        >= Rank.Jack => 10,
        _            => (int)Rank
    };

    public override string ToString()   //object의 ToString()을 덮어쓰기
    {
        if (Rank == Rank.Joker) return "Joker";
        return $"{Rank} of {Suit}";
    }
}