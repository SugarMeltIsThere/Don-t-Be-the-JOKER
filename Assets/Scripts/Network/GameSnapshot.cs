using System;
using System.Collections.Generic;
using Fusion;

// 호스트가 게스트에게 보여줘도 되는 "공개 상태" 한 덩어리.
// NetworkGameManager의 [Networked] 프로퍼티로 복제되며, 손패 같은 비공개 정보는 여기에 넣지 않는다.
public struct GameSnapshot : INetworkStruct
{
    public const byte None = 255;       // 캐릭터/아이템 "없음" 표시
    private const int MaxPackedItems = 3;

    public int Round;                   // 0이면 아직 게임 시작 전
    public byte Phase;                  // GamePhase
    public byte Current;                // ActivePlayer
    public int P1Chips, P2Chips;
    public int P1TotalBet, P2TotalBet;
    public int Pot;
    public NetworkBool IsFirstBet;
    public byte P1Slots, P2Slots;
    public byte P1Items, P2Items;       // PackItems 참고
    public byte P1Character, P2Character;
    public byte PendingItemPlayer;      // ActivePlayer
    public byte PendingItem;            // ItemType, 없으면 None

    // 아이템 목록을 1바이트로 압축: 하위 2비트 = 개수, 이후 2비트씩 아이템 종류 (최대 3개)
    public static byte PackItems(IReadOnlyList<Item> items)
    {
        int count = Math.Min(items.Count, MaxPackedItems);
        int packed = count;
        for (int i = 0; i < count; i++)
            packed |= (int)items[i].Type << (2 + 2 * i);
        return (byte)packed;
    }

    public static void UnpackItems(byte packed, List<Item> into)
    {
        into.Clear();
        int count = packed & 3;
        for (int i = 0; i < count; i++)
            into.Add(new Item((ItemType)((packed >> (2 + 2 * i)) & 3)));
    }
}

// GameManager가 네트워크 계층(Fusion)을 모르고도 동작하게 하는 경계.
// 구현체는 NetworkGameManager 하나뿐이고, 호스트 = Player1 / 게스트 = Player2 로 고정한다.
public interface IGameNet
{
    bool IsHost { get; }
    ActivePlayer LocalPlayer { get; }

    // 호스트 → 게스트 (공개 상태는 스냅샷, 손패 이벤트는 개별 전송)
    void PublishState(GameSnapshot snapshot);
    void SendRoundStart(int round, Card sharedCard);
    void SendCard(ActivePlayer seat, Card card, Card revealedPrevious);
    void SendReplaceLast(ActivePlayer seat, Card card);
    void SendPeek(ActivePlayer seat, Card card);

    // 게스트 → 호스트 (UI가 GameManager를 통해 호출하는 것들)
    void RequestUseItem(ItemType type);
    void RequestDiscard(int discardIndex);
}