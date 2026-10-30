using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class TableInput : MonoBehaviour
{
    private const float MaxDistance = 5f;
    private const string InteractableLayer = "Interactable";

    [SerializeField] private Camera _camera;

    private int _mask;
    private Collider _hoveredCollider;
    private Interactable _hovered;
    private Interactable _highlighted;

    // 카메라와 레이어 마스크를 준비한다
    private void Awake()
    {
        if (_camera == null) _camera = Camera.main;
        _mask = LayerMask.GetMask(InteractableLayer);
    }

    // 커서 아래의 물건을 강조하고 클릭하면 행동을 요청한다
    private void Update()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null || _camera == null) return;

        Interactable target = FindTarget(mouse);
        Interactable usable = target != null && target.CanInteract() ? target : null;

        if (usable != _highlighted)
        {
            if (_highlighted != null) _highlighted.SetHighlighted(false);
            if (usable != null) usable.SetHighlighted(true);
            _highlighted = usable;
        }

        if (usable != null && mouse.leftButton.wasPressedThisFrame)
            usable.Interact();
    }

    // 커서에서 레이를 쏴 맞은 물건을 찾고 같은 콜라이더면 이전 결과를 다시 쓴다
    private Interactable FindTarget(Mouse mouse)
    {
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return null;

        Ray ray = _camera.ScreenPointToRay(mouse.position.ReadValue());
        if (!Physics.Raycast(ray, out RaycastHit hit, MaxDistance, _mask))
        {
            _hoveredCollider = null;
            _hovered = null;
            return null;
        }

        if (hit.collider != _hoveredCollider)
        {
            _hoveredCollider = hit.collider;
            _hovered = hit.collider.GetComponentInParent<Interactable>();
        }
        return _hovered;
    }
}
