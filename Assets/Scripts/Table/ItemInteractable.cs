using System;
using UnityEngine;

public class ItemInteractable : Interactable
{
    [SerializeField] private ItemType _type;
    [SerializeField] private float _scaleDuration = 0.2f;

    private Action<ItemInteractable> _onVanished;
    private Vector3 _fullScale;
    private float _elapsed;
    private bool _vanishing;

    public ItemType Type => _type;
    public int SlotIndex { get; private set; }
    public bool IsMine { get; private set; }

    // 원래 크기를 기억하고 Update를 끈다
    protected override void Awake()
    {
        base.Awake();
        _fullScale = transform.localScale;
        enabled = false;
    }

    // 다 사라졌을 때 풀로 돌려보낼 처리기를 정한다
    public void Init(Action<ItemInteractable> onVanished) => _onVanished = onVanished;

    // 어느 쪽 몇 번째 슬롯의 아이템인지 정한다
    public void Bind(int slotIndex, bool isMine)
    {
        SlotIndex = slotIndex;
        IsMine = isMine;
    }

    // 내 아이템이고 내 차례이며 지금 단계에서 쓸 수 있는지 확인한다
    public override bool CanInteract() =>
        IsMine && !_vanishing && PlayerActions.IsLocalTurn && PlayerActions.CanUseInPhase(_type);

    // 이 슬롯의 아이템 사용을 요청한다
    public override void Interact() => PlayerActions.UseItem(SlotIndex);

    // 작은 크기에서 커지며 나타난다
    public void Appear()
    {
        _vanishing = false;
        transform.localScale = Vector3.zero;
        _elapsed = 0f;
        enabled = true;
    }

    // 작아지며 사라진 뒤 풀로 돌아간다
    public void Vanish()
    {
        SetHighlighted(false);
        _vanishing = true;
        _elapsed = 0f;
        enabled = true;
    }

    // 크기를 보간하고 다 사라졌으면 처리기를 부른다
    private void Update()
    {
        _elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(_elapsed / _scaleDuration);
        float eased = t * t * (3f - 2f * t);
        transform.localScale = _fullScale * (_vanishing ? 1f - eased : eased);
        if (t < 1f) return;

        enabled = false;
        if (_vanishing) _onVanished?.Invoke(this);
    }
}
