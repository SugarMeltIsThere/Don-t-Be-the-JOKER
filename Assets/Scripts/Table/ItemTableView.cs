using System.Collections.Generic;
using UnityEngine;

public class ItemTableView : MonoBehaviour
{
    private const int MaxSlots = 3;
    private const int PrewarmPerType = MaxSlots * 2;

    [SerializeField] private ItemInteractable _magnifierPrefab;
    [SerializeField] private ItemInteractable _flashPrefab;
    [SerializeField] private ItemInteractable _bribePrefab;
    [SerializeField] private Transform[] _mySlots = new Transform[MaxSlots];
    [SerializeField] private Transform[] _opponentSlots = new Transform[MaxSlots];

    private readonly Dictionary<ItemType, Stack<ItemInteractable>> _free = new Dictionary<ItemType, Stack<ItemInteractable>>();
    private readonly ItemInteractable[] _mine = new ItemInteractable[MaxSlots];
    private readonly ItemInteractable[] _theirs = new ItemInteractable[MaxSlots];
    private GameManager _gm;

    // 종류마다 양쪽 슬롯을 다 채울 만큼 미리 만든다
    private void Awake()
    {
        Prewarm(ItemType.Magnifier, _magnifierPrefab);
        Prewarm(ItemType.Flash, _flashPrefab);
        Prewarm(ItemType.Bribe, _bribePrefab);
    }

    // 상태 변경을 구독하고 지금 상태를 한 번 맞춘다
    private void Start()
    {
        _gm = GameManager.Instance;
        if (_gm == null) return;

        _gm.StateChanged += Sync;
        Sync();
    }

    // 구독을 해제한다
    private void OnDestroy()
    {
        if (_gm == null) return;
        _gm.StateChanged -= Sync;
        _gm = null;
    }

    // 한 종류의 아이템 오브젝트를 만들어 쌓아둔다
    private void Prewarm(ItemType type, ItemInteractable prefab)
    {
        Stack<ItemInteractable> stack = new Stack<ItemInteractable>(PrewarmPerType);
        _free[type] = stack;
        for (int i = 0; i < PrewarmPerType; i++)
        {
            ItemInteractable item = Instantiate(prefab, transform);
            item.Init(Release);
            item.gameObject.SetActive(false);
            stack.Push(item);
        }
    }

    // 양쪽 아이템 목록과 테이블 위 오브젝트를 맞춘다
    private void Sync()
    {
        if (_gm == null) return;
        SyncSide(_mine, _mySlots, PlayerActions.ItemsOf(PlayerActions.LocalSeat), isMine: true);
        SyncSide(_theirs, _opponentSlots, PlayerActions.ItemsOf(PlayerActions.OpponentSeat), isMine: false);
    }

    // 슬롯마다 종류가 다르면 기존 것은 사라지게 하고 새 것을 나타나게 한다
    private void SyncSide(ItemInteractable[] views, Transform[] anchors, IReadOnlyList<Item> items, bool isMine)
    {
        for (int i = 0; i < MaxSlots; i++)
        {
            ItemInteractable current = views[i];
            bool hasItem = i < items.Count;

            if (current != null && (!hasItem || current.Type != items[i].Type))
            {
                current.Vanish();
                views[i] = null;
                current = null;
            }

            if (!hasItem) continue;

            if (current == null)
            {
                current = Get(items[i].Type);
                current.transform.SetPositionAndRotation(anchors[i].position, anchors[i].rotation);
                current.Appear();
                views[i] = current;
            }
            current.Bind(i, isMine);
        }
    }

    // 쉬고 있는 아이템 오브젝트를 꺼내고 사라지는 중인 것까지 겹쳐 모자라면 새로 만든다
    private ItemInteractable Get(ItemType type)
    {
        Stack<ItemInteractable> stack = _free[type];
        ItemInteractable item;
        if (stack.Count > 0)
        {
            item = stack.Pop();
        }
        else
        {
            Debug.LogWarning($"[ItemTableView] {type} 오브젝트가 모자라 새로 생성");
            item = Instantiate(PrefabOf(type), transform);
            item.Init(Release);
        }
        item.gameObject.SetActive(true);
        return item;
    }

    // 종류에 맞는 프리팹을 고른다
    private ItemInteractable PrefabOf(ItemType type) => type switch
    {
        ItemType.Magnifier => _magnifierPrefab,
        ItemType.Flash => _flashPrefab,
        _ => _bribePrefab
    };

    // 사라진 아이템 오브젝트를 다시 쌓아둔다
    private void Release(ItemInteractable item)
    {
        item.gameObject.SetActive(false);
        _free[item.Type].Push(item);
    }
}
