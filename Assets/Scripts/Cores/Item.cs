public enum ItemType
{
    Magnifier, // 돋보기
    Flash,     // 플래쉬
    Bribe      // 뇌물
}

public class Item
{
    public ItemType Type { get; init; }

    public Item(ItemType type)
    {
        Type = type;
    }

    public string GetName() => Type switch
    {
        ItemType.Magnifier => "Magnifier",
        ItemType.Flash => "Flash",
        ItemType.Bribe => "Deal",
        _ => "Unknown"
    };

    public override string ToString() => GetName();
}