using UnityEngine;

public abstract class Interactable : MonoBehaviour
{
    [SerializeField] private Renderer _renderer;
    [SerializeField] private Material _highlightMaterial;

    private Material _normalMaterial;
    private bool _highlighted;

    // 강조를 끌 때 돌아갈 원래 머티리얼을 기억한다
    protected virtual void Awake()
    {
        if (_renderer == null) _renderer = GetComponent<Renderer>();
        if (_renderer != null) _normalMaterial = _renderer.sharedMaterial;
    }

    // 지금 이 물건을 쓸 수 있는지 확인한다
    public abstract bool CanInteract();

    // 이 물건에 연결된 행동을 요청한다
    public abstract void Interact();

    // 머티리얼을 바꿔 강조 표시를 켜거나 끈다
    public void SetHighlighted(bool highlighted)
    {
        if (_highlighted == highlighted || _renderer == null || _highlightMaterial == null) return;

        _highlighted = highlighted;
        _renderer.sharedMaterial = highlighted ? _highlightMaterial : _normalMaterial;
    }

    // 지금이 로컬 플레이어의 차례인지 확인한다
    protected static bool IsLocalTurn(GameManager gm)
    {
        ActivePlayer local = NetworkGameManager.Instance != null
            ? NetworkGameManager.Instance.LocalPlayer
            : ActivePlayer.Player1;
        return gm.CurrentPlayer == local;
    }
}
