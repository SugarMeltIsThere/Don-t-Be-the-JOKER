using UnityEngine;

public class ChipColumnButton : Interactable
{
    [SerializeField] private int _value = 1;
    [SerializeField] private bool _takesBack;
    [SerializeField] private GameObject _hint;

    private TableControls _controls;

    // 레이즈를 관리하는 쪽을 연결한다
    public void Init(TableControls controls) => _controls = controls;

    // 레이즈를 고르는 동안에만 기둥 위 안내 글자를 보인다
    public void SetHintVisible(bool visible)
    {
        if (_hint != null) _hint.SetActive(visible);
    }

    // 레이즈를 고르는 중일 때만 누를 수 있다
    public override bool CanInteract() => _controls != null && _controls.IsRaising;

    public override bool RepeatsWhileHeld => true;

    // 내 칩 더미면 칩 한 개를 레이즈에 더하고, 베팅 더미면 한 개를 되돌린다
    public override void Interact()
    {
        if (_takesBack) _controls.UndoChip();
        else _controls.AddChip(_value);
    }

    // 오른쪽 클릭은 언제나 한 개를 되돌린다
    public override void InteractAlt() => _controls.UndoChip();
}
