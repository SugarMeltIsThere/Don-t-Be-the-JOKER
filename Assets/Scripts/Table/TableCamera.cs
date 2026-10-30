using UnityEngine;
using UnityEngine.InputSystem;

public class TableCamera : MonoBehaviour
{
    [SerializeField] private float _maxYaw = 6f;
    [SerializeField] private float _maxPitch = 4f;
    [SerializeField] private float _lookDownPitch = 22f;
    [SerializeField] private Vector3 _lookDownOffset = new Vector3(0f, -0.04f, 0.06f);
    [SerializeField] private float _smoothing = 8f;

    private Vector3 _basePosition;
    private Quaternion _baseRotation;
    private Vector2 _sway;
    private float _downBlend;
    private bool _lookingDown;

    // 씬에 놓인 자세를 기본 시점으로 기억한다
    private void Awake()
    {
        _basePosition = transform.localPosition;
        _baseRotation = transform.localRotation;
    }

    // 커서 위치로 시선을 살짝 기울이고 키나 휠로 내려다보기를 전환한다
    private void Update()
    {
        Mouse mouse = Mouse.current;
        Keyboard keyboard = Keyboard.current;
        if (mouse == null) return;

        float scroll = mouse.scroll.ReadValue().y;
        if (scroll < 0f || (keyboard != null && keyboard.sKey.wasPressedThisFrame)) _lookingDown = true;
        if (scroll > 0f || (keyboard != null && keyboard.wKey.wasPressedThisFrame)) _lookingDown = false;

        Vector2 pointer = mouse.position.ReadValue();
        Vector2 normalized = new Vector2(
            Mathf.Clamp(pointer.x / Screen.width * 2f - 1f, -1f, 1f),
            Mathf.Clamp(pointer.y / Screen.height * 2f - 1f, -1f, 1f));

        float blend = 1f - Mathf.Exp(-_smoothing * Time.deltaTime);
        _sway = Vector2.Lerp(_sway, normalized, blend);
        _downBlend = Mathf.Lerp(_downBlend, _lookingDown ? 1f : 0f, blend);

        Quaternion offset = Quaternion.Euler(-_sway.y * _maxPitch + _downBlend * _lookDownPitch, _sway.x * _maxYaw, 0f);
        transform.localRotation = _baseRotation * offset;
        transform.localPosition = _basePosition + _baseRotation * (_lookDownOffset * _downBlend);
    }
}
