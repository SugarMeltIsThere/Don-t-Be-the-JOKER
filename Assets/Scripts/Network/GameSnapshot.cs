using System;
using System.Collections.Generic;
using Fusion;

public struct GameSnapshot : INetworkStruct
{
    public const byte None = 255;
    private const int MaxPackedItems = 3;

    public int Round;
    public byte Phase;
    public byte Current;
    public int P1Chips, P2Chips;
    public int P1TotalBet, P2TotalBet;
    public int Pot;
    public NetworkBool IsFirstBet;
    public byte P1Slots, P2Slots;
    public byte P1Items, P2Items;
    public byte P1Character, P2Character;
    public byte PendingItemPlayer;
    public byte PendingItem;
    public byte CutP1, CutP2;
    public byte Surrendered;

    // 카드를 한 바이트로 압축한다(없으면 0)
    public static byte PackCard(Card card) => card == null ? (byte)0 : (byte)((int)card.Rank * 4 + (int)card.Suit);

    // 압축된 바이트를 카드로 푼다
    public static Card UnpackCard(byte value) => value == 0 ? null : new Card((Rank)(value / 4), (Suit)(value % 4));

    // 아이템 목록을 한 바이트로 압축한다
    public static byte PackItems(IReadOnlyList<Item> items)
    {
        int count = Math.Min(items.Count, MaxPackedItems);
        int packed = count;
        for (int i = 0; i < count; i++)
            packed |= (int)items[i].Type << (2 + 2 * i);
        return (byte)packed;
    }

    // 압축된 바이트를 아이템 목록으로 푼다
    public static void UnpackItems(byte packed, List<Item> into)
    {
        into.Clear();
        int count = packed & 3;
        for (int i = 0; i < count; i++)
            into.Add(new Item((ItemType)((packed >> (2 + 2 * i)) & 3)));
    }
}

public interface IGameNet
{
    bool IsHost { get; }
    ActivePlayer LocalPlayer { get; }

    void PublishState(GameSnapshot snapshot);
    void SendRoundStart(int round, Card sharedCard);
    void SendCard(ActivePlayer seat, Card card, Card revealedPrevious);
    void SendReplaceLast(ActivePlayer seat, Card card);
    void SendPeek(ActivePlayer seat, Card card);
    void SendReveal(Card hostLastCard, int hostAceMask);
    void SendOpponentAces(int mask);
    void SendItemUsed(ActivePlayer seat, ItemType type);
    void SendBell(ActivePlayer seat);

    void RequestUseItem(ItemType type);
    void RequestDiscard(int discardIndex);
}