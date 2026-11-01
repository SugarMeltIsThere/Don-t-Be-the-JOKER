using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class InteractionPrompt : MonoBehaviour
{
    [SerializeField] private TableInput _input;
    [SerializeField] private RectTransform _root;
    [SerializeField] private CanvasGroup _group;
    [SerializeField] private TMP_Text _primary;
    [SerializeField] private GameObject _altRow;
    [SerializeField] private TMP_Text _alt;
    [SerializeField] private Vector2 _cursorOffset = new Vector2(28f, -18f);
    [SerializeField] private float _fadeSpeed = 12f;

    private Interactable _shown;

    public bool IsShowing => _shown != null;

    // 처음에는 숨겨 둔다
    private void Start()
    {
        _group.alpha = 0f;
    }

    // 커서 아래 쓸 수 있는 물건이 있으면 그 옆에 왼쪽·오른쪽 클릭 안내를 띄우고 없으면 서서히 숨긴다
    private void LateUpdate()
    {
        Interactable target = _input != null ? _input.Hovered : null;
        string prompt = target != null ? target.Prompt : null;
        if (string.IsNullOrEmpty(prompt)) target = null;

        if (target != _shown && target != null) Show(target);
        _shown = target;

        float goal = target != null ? 1f : 0f;
        _group.alpha = Mathf.MoveTowards(_group.alpha, goal, _fadeSpeed * Time.deltaTime);
        if (target == null) return;

        _primary.SetText(target.Prompt);
        Mouse mouse = Mouse.current;
        if (mouse == null) return;
        Vector2 pointer = mouse.position.ReadValue();
        RectTransform parent = (RectTransform)_root.parent;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, pointer + _cursorOffset, null, out Vector2 local);
        _root.anchoredPosition = local;
    }

    // 새 물건의 안내 글자를 채운다(보조 행동이 없으면 둘째 줄을 숨긴다)
    private void Show(Interactable target)
    {
        string alt = target.AltPrompt;
        bool hasAlt = !string.IsNullOrEmpty(alt);
        _altRow.SetActive(hasAlt);
        if (hasAlt) _alt.SetText(alt);
    }
}
