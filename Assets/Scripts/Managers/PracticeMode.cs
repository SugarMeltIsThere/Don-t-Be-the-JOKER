using UnityEngine;

public static class PracticeMode
{
    public static bool Active { get; private set; }
    public static bool Tutorial { get; private set; }
    public static CharacterType PlayerCharacter { get; private set; } = CharacterType.King;
    public static CharacterType BotCharacter { get; private set; } = CharacterType.Queen;

    // AI 상대와의 연습을 켜고 두 자리의 캐릭터를 정한다(튜토리얼이면 조커가 섞이지 않게 킹과 퀸, 아니면 무작위)
    public static void Begin(bool tutorial)
    {
        Active = true;
        Tutorial = tutorial;
        PlayerCharacter = tutorial ? CharacterType.King : (CharacterType)Random.Range(0, 4);
        BotCharacter = tutorial ? CharacterType.Queen : (CharacterType)Random.Range(0, 4);
    }

    // 연습을 끈다
    public static void End()
    {
        Active = false;
        Tutorial = false;
    }
}
