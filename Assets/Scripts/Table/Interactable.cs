using UnityEngine;

public abstract class Interactable : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static MaterialPropertyBlock s_block;

    [SerializeField] private Color _highlightTint = new Color(1.7f, 1.55f, 1.15f);
    [SerializeField] private string _prompt;
    [SerializeField] private string _altPrompt;

    private Renderer[] _renderers;
    private Color[] _baseColors;
    private bool _highlighted;

    // 강조할 렌더러와 각 머티리얼의 원래 기본 색을 모은다
    protected virtual void Awake()
    {
        if (s_block == null) s_block = new MaterialPropertyBlock();

        _renderers = GetComponentsInChildren<Renderer>(true);
        _baseColors = new Color[_renderers.Length];
        for (int i = 0; i < _renderers.Length; i++)
        {
            Material material = _renderers[i].sharedMaterial;
            _baseColors[i] = material != null && material.HasProperty(BaseColorId)
                ? material.GetColor(BaseColorId)
                : Color.white;
        }
    }

    // 지금 이 물건을 쓸 수 있는지 확인한다
    public abstract bool CanInteract();

    // 이 물건에 연결된 행동을 요청한다
    public abstract void Interact();

    // 오른쪽 클릭에 연결된 보조 행동을 요청한다(기본은 아무것도 하지 않음)
    public virtual void InteractAlt() { }

    public virtual bool RepeatsWhileHeld => false;

    public virtual string Prompt => _prompt;

    public virtual string AltPrompt => _altPrompt;

    // 강조하는 동안에만 기본 색을 밝게 덮어쓰고 끄면 원래 머티리얼로 돌려놓는다
    public void SetHighlighted(bool highlighted)
    {
        if (_highlighted == highlighted || _renderers == null) return;
        _highlighted = highlighted;

        for (int i = 0; i < _renderers.Length; i++)
        {
            Renderer target = _renderers[i];
            if (target == null) continue;

            if (!highlighted)
            {
                target.SetPropertyBlock(null);
                continue;
            }

            s_block.Clear();
            s_block.SetColor(BaseColorId, _baseColors[i] * _highlightTint);
            target.SetPropertyBlock(s_block);
        }
    }
}
