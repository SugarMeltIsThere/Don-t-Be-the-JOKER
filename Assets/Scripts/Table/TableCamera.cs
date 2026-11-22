using UnityEngine;
using UnityEngine.InputSystem;

public class TableCamera : MonoBehaviour
{
    [SerializeField] private float _maxYaw = 12f;
    [SerializeField] private float _maxPitch = 4f;
    [SerializeField] private float _maxPitchUp = 18f;
    [SerializeField] private float _lookDownPitch = 22f;
    [SerializeField] private Vector3 _lookDownOffset = new Vector3(0f, -0.04f, 0.06f);
    [SerializeField] private float _smoothing = 8f;
    [SerializeField] private float _focusSmoothing = 6f;

    private Vector3 _basePosition;
    private Quaternion _baseRotation;
    private Vector2 _pointer;
    private Vector2 _sway;
    private float _downBlend;
    private bool _lookingDown;
    private bool _focusing;
    private Vector3 _focusPosition;
    private Quaternion _focusRotation;
    private float _focusBlend;

    public Vector2 Pointer => _focusing ? Vector2.zero : _pointer;
    public bool LookingDown => _lookingDown || _focusing;

    // 씬에 놓인 자세를 기본 시점으로 기억한다
    private void Awake()
    {
        _basePosition = transform.localPosition;
        _baseRotation = transform.localRotation;
    }

    // 정한 자세로 시선을 옮겨 붙잡아 둔다
    public void Focus(Vector3 position, Quaternion rotation)
    {
        _focusPosition = position;
        _focusRotation = rotation;
        _focusing = true;
    }

    // 붙잡아 둔 시선을 풀고 기본 시점으로 돌아간다
    public void Release()
    {
        _focusing = false;
        _lookingDown = false;
    }

    // 커서 위치로 시선을 기울이고(가운데는 살짝, 위쪽 끝으로 갈수록 방을 올려다봄) 키나 휠로 내려다보기를 전환하며, 붙잡힌 시선이 있으면 그쪽으로 옮긴다
    private void Update()
    {
        ReadInput();

        float blend = 1f - Mathf.Exp(-_smoothing * Time.deltaTime);
        _sway = Vector2.Lerp(_sway, _focusing ? Vector2.zero : _pointer, blend);
        _downBlend = Mathf.Lerp(_downBlend, _lookingDown ? 1f : 0f, blend);
        _focusBlend = Mathf.Lerp(_focusBlend, _focusing ? 1f : 0f, 1f - Mathf.Exp(-_focusSmoothing * Time.deltaTime));

        float pitch = _sway.y > 0f ? _sway.y * Mathf.Abs(_sway.y) * _maxPitchUp : _sway.y * _maxPitch;
        float yaw = _sway.x * Mathf.Abs(_sway.x) * _maxYaw;
        Quaternion offset = Quaternion.Euler(-pitch + _downBlend * _lookDownPitch, yaw, 0f);
        Vector3 position = _basePosition + _baseRotation * (_lookDownOffset * _downBlend);
        Quaternion rotation = _baseRotation * offset;
        transform.SetLocalPositionAndRotation(
            Vector3.Lerp(position, _focusPosition, _focusBlend),
            Quaternion.Slerp(rotation, _focusRotation, _focusBlend));
    }

    // 창이 포커스를 가졌고 시선이 붙잡혀 있지 않을 때만 휠, 키, 커서 입력을 읽는다
    private void ReadInput()
    {
        Mouse mouse = Mouse.current;
        Keyboard keyboard = Keyboard.current;
        if (mouse == null || !Application.isFocused || _focusing) return;

        float scroll = mouse.scroll.ReadValue().y;
        if (scroll < 0f || (keyboard != null && keyboard.sKey.wasPressedThisFrame)) _lookingDown = true;
        if (scroll > 0f || (keyboard != null && keyboard.wKey.wasPressedThisFrame)) _lookingDown = false;

        Vector2 pointer = mouse.position.ReadValue();
        _pointer = new Vector2(
            Mathf.Clamp(pointer.x / Screen.width * 2f - 1f, -1f, 1f),
            Mathf.Clamp(pointer.y / Screen.height * 2f - 1f, -1f, 1f));
    }
}
