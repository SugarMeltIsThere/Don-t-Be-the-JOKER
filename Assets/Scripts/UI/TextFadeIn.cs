using TMPro;
using UnityEngine;

public class TextFadeIn : MonoBehaviour
{
    [SerializeField] private TMP_Text _text;
    [SerializeField] private CanvasGroup _group;
    [SerializeField] private float _duration = 0.35f;
    [SerializeField] private float _rise = 10f;

    private RectTransform _rect;
    private Vector2 _restPosition;
    private string _lastText;
    private float _elapsed;

    // 제자리 위치를 기억한다
    private void Awake()
    {
        _rect = (RectTransform)transform;
        _restPosition = _rect.anchoredPosition;
    }

    // 켜질 때마다 처음부터 나타난다
    private void OnEnable()
    {
        _lastText = null;
    }

    // 글자가 바뀌면 살짝 아래에서 떠오르며 투명에서 선명해진다
    private void LateUpdate()
    {
        if (_text.text != _lastText)
        {
            _lastText = _text.text;
            _elapsed = 0f;
        }

        _elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(_elapsed / _duration);
        float eased = 1f - (1f - t) * (1f - t);
        _group.alpha = eased;
        _rect.anchoredPosition = _restPosition + Vector2.down * (_rise * (1f - eased));
    }
}
