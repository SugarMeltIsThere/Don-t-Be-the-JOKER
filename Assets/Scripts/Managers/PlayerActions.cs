using System.Collections.Generic;

public static class PlayerActions
{
    private static NetworkGameManager Net => NetworkGameManager.Instance;
    private static GameManager Game => GameManager.Instance;

    public static ActivePlayer LocalSeat => Net != null ? Net.LocalPlayer : ActivePlayer.Player1;

    public static ActivePlayer OpponentSeat =>
        LocalSeat == ActivePlayer.Player1 ? ActivePlayer.Player2 : ActivePlayer.Player1;

    // 지금이 로컬 플레이어의 차례인지 확인한다
    public static bool IsLocalTurn => Game != null && Game.CurrentPlayer == LocalSeat;

    // 좌석의 아이템 목록을 가져온다
    public static IReadOnlyList<Item> ItemsOf(ActivePlayer seat) =>
        seat == ActivePlayer.Player1 ? Game.Player1Items : Game.Player2Items;

    // 카드 받기를 요청한다
    public static void Hit()
    {
        if (Net != null) Net.RequestHit();
        else Game.OnHit();
    }

    // 스탠드를 요청한다
    public static void Stand()
    {
        if (Net != null) Net.RequestStand();
        else Game.OnStand();
    }

    // 레이즈를 요청한다
    public static void Raise(int amount)
    {
        if (Net != null) Net.RequestRaise(amount);
        else Game.OnRaise(amount);
    }

    // 콜을 요청한다
    public static void Call()
    {
        if (Net != null) Net.RequestCall();
        else Game.OnCall();
    }

    // 다이를 요청한다
    public static void Die()
    {
        if (Net != null) Net.RequestDie();
        else Game.OnDie();
    }

    // 에이스 값 변경을 요청한다
    public static void SetAce(int cardIndex, bool isEleven)
    {
        if (Net != null) Net.RequestAceValueChanged(LocalSeat, cardIndex, isEleven);
        else Game.OnAceValueChanged(LocalSeat, cardIndex, isEleven);
    }

    // 에이스 선택 확정을 요청한다
    public static void ConfirmAces()
    {
        if (Net != null) Net.RequestAceConfirmed(LocalSeat);
        else Game.OnAceSelectionConfirmed(LocalSeat);
    }

    // 내 아이템 슬롯의 아이템 사용을 요청한다
    public static void UseItem(int slotIndex)
    {
        IReadOnlyList<Item> items = ItemsOf(LocalSeat);
        if (slotIndex < 0 || slotIndex >= items.Count) return;

        ItemType type = items[slotIndex].Type;
        if (Net != null)
        {
            Net.RequestUseItem(type);
            return;
        }

        switch (type)
        {
            case ItemType.Magnifier: Game.OnUseMagnifier(LocalSeat); break;
            case ItemType.Flash: Game.OnUseFlash(LocalSeat); break;
            case ItemType.Bribe: Game.OnUseBribe(LocalSeat); break;
        }
    }

    // 아이템 폐기 선택을 요청한다
    public static void Discard(int discardIndex)
    {
        if (Net != null) Net.RequestItemDiscard(LocalSeat, discardIndex);
        else Game.OnItemDiscard(Game.PendingItemPlayer, discardIndex);
    }

    // 지금 단계에서 이 종류의 아이템을 쓸 수 있는지 확인한다
    public static bool CanUseInPhase(ItemType type)
    {
        if (Game == null) return false;
        return type switch
        {
            ItemType.Magnifier => Game.CurrentPhase == GamePhase.Draw,
            ItemType.Bribe => Game.CurrentPhase == GamePhase.Draw,
            ItemType.Flash => Game.CurrentPhase == GamePhase.Betting,
            _ => false
        };
    }
}
