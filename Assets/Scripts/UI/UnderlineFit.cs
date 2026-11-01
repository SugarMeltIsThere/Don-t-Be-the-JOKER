using TMPro;
using UnityEngine;

public class UnderlineFit : MonoBehaviour
{
    [SerializeField] private TMP_Text _label;
    [SerializeField] private RectTransform _underline;
    [SerializeField] private bool _alignLeft;
    [SerializeField] private float _padding = 3f;
    [SerializeField] private float _offsetY = 8f;

    private string _lastText;

    // 글자가 바뀌면 밑줄 길이를 글자 폭에 맞춘다(왼쪽 정렬이면 글자 시작점부터)
    private void LateUpdate()
    {
        if (_label == null || _underline == null || _label.text == _lastText) return;
        _lastText = _label.text;

        float width = _label.GetPreferredValues(_label.text).x + _padding * 2f;
        float x = _alignLeft ? 0f : 0.5f;
        _underline.anchorMin = _underline.anchorMax = new Vector2(x, 0f);
        _underline.pivot = new Vector2(x, 0f);
        _underline.sizeDelta = new Vector2(width, 1.5f);
        _underline.anchoredPosition = new Vector2(_alignLeft ? -_padding : 0f, _offsetY);
    }
}
