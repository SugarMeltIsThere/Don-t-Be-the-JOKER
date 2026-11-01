using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class FieldFocusFx : MonoBehaviour
{
    [SerializeField] private TMP_InputField _input;
    [SerializeField] private Image _fill;
    [SerializeField] private Image _underline;
    [SerializeField] private float _idleFill = 0.85f;
    [SerializeField] private float _focusFill = 1f;
    [SerializeField] private float _idleLine = 0.55f;
    [SerializeField] private float _focusLine = 1f;
    [SerializeField] private float _speed = 8f;

    private float _blend;

    // 입력 중이면 바탕과 밑줄을 밝게, 아니면 차분하게 서서히 바꾼다
    private void Update()
    {
        float goal = _input.isFocused ? 1f : 0f;
        _blend = Mathf.MoveTowards(_blend, goal, _speed * Time.unscaledDeltaTime);
        SetAlpha(_fill, Mathf.Lerp(_idleFill, _focusFill, _blend));
        SetAlpha(_underline, Mathf.Lerp(_idleLine, _focusLine, _blend));
    }

    // 이미지 색은 그대로 두고 투명도만 바꾼다
    private static void SetAlpha(Graphic graphic, float alpha)
    {
        if (graphic == null) return;
        Color c = graphic.color;
        c.a = alpha;
        graphic.color = c;
    }
}
