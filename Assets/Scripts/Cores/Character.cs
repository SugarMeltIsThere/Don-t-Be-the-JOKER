public enum CharacterType
{
    King,
    Queen,
    Jack,
    Joker
}

public abstract class Character
{
    public CharacterType Type { get; init; }

    protected Character(CharacterType type)
    {
        Type = type;
    }

    // 게임 시작 시 호출
    public abstract void OnGameStart(GameManager gm, ActivePlayer player);

    // 아이템 사용 시 호출 (잭 능력)
    public virtual bool OnItemUsed(GameManager gm, ActivePlayer player)
    {
        return false; // 기본값: 아이템 소모
    }

    public string GetName() => Type switch
    {
        CharacterType.King => "King",
        CharacterType.Queen => "Queen",
        CharacterType.Jack => "Jack",
        CharacterType.Joker => "Joker",
        _ => "Unknown"
    };

    public override string ToString() => GetName();
}

public class KingCharacter : Character
{
    public KingCharacter() : base(CharacterType.King) { }

    public override void OnGameStart(GameManager gm, ActivePlayer player)
    {
        gm.SetItemSlots(player, 3);
        UnityEngine.Debug.Log($"[킹 능력] {player} 아이템 슬롯 3개");
    }
}

public class QueenCharacter : Character
{
    public QueenCharacter() : base(CharacterType.Queen) { }

    public override void OnGameStart(GameManager gm, ActivePlayer player)
    {
        gm.AddChips(player, 5);
        UnityEngine.Debug.Log($"[퀸 능력] {player} 칩 5개 추가");
    }
}

public class JackCharacter : Character
{
    private const float RetainChance = 0.3f; // 30% 확률로 아이템 유지
    private System.Random _random = new System.Random();

    public JackCharacter() : base(CharacterType.Jack) { }

    public override void OnGameStart(GameManager gm, ActivePlayer player)
    {
        // 잭은 게임 시작 시 별도 능력 없음
    }

    public override bool OnItemUsed(GameManager gm, ActivePlayer player)
    {
        bool retain = _random.NextDouble() < RetainChance;
        if (retain)
            UnityEngine.Debug.Log($"[잭 능력] {player} 아이템 유지!");
        return retain; // true면 아이템 소모 안 함
    }
}

public class JokerCharacter : Character
{
    public JokerCharacter() : base(CharacterType.Joker) { }

    public override void OnGameStart(GameManager gm, ActivePlayer player)
    {
        gm.InsertJokerCard();
        UnityEngine.Debug.Log($"[조커 능력] 덱에 조커 카드 삽입");
    }
}