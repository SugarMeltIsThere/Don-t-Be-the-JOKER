using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class MenuButtonFx : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private RectTransform _label;
    [SerializeField] private Graphic _marker;
    [SerializeField] private float _shift = 16f;
    [SerializeField] private float _speed = 10f;
    [SerializeField] private AudioClip _hoverSound;
    [SerializeField] private AudioClip _clickSound;
    [SerializeField] private float _soundVolume = 0.5f;

    private static AudioSource s_audio;

    private Button _button;
    private Vector2 _labelRest;
    private bool _hovered;
    private float _blend;

    // 글자 제자리와 버튼을 기억하고 표시자를 숨긴다
    private void Awake()
    {
        _button = GetComponent<Button>();
        _labelRest = _label.anchoredPosition;
        SetBlend(0f);
        if (_button != null) _button.onClick.AddListener(() => PlaySound(_clickSound));
    }

    // 마우스가 올라오면 표시하고, 누를 수 있는 버튼이면 짧은 소리를 낸다
    public void OnPointerEnter(PointerEventData eventData)
    {
        _hovered = true;
        if (_button == null || _button.interactable) PlaySound(_hoverSound);
    }

    // 마우스가 나가면 되돌린다
    public void OnPointerExit(PointerEventData eventData) => _hovered = false;

    // 꺼질 때 바로 원래대로 돌린다
    private void OnDisable()
    {
        _hovered = false;
        SetBlend(0f);
    }

    // 누를 수 있는 버튼에 마우스가 있으면 금색 마름모가 나타나고 글자가 오른쪽으로 살짝 밀린다
    private void Update()
    {
        bool active = _hovered && (_button == null || _button.interactable);
        float goal = active ? 1f : 0f;
        if (Mathf.Approximately(_blend, goal)) return;
        SetBlend(Mathf.MoveTowards(_blend, goal, _speed * Time.unscaledDeltaTime));
    }

    // 모든 메뉴 버튼이 같이 쓰는 소리 소스로 한 번 낸다
    private void PlaySound(AudioClip clip)
    {
        if (clip == null) return;
        if (s_audio == null)
        {
            s_audio = new GameObject("MenuSound").AddComponent<AudioSource>();
            s_audio.playOnAwake = false;
        }
        s_audio.PlayOneShot(clip, _soundVolume * GameSettings.EffectsScale);
    }

    // 진행 정도에 맞춰 글자 위치와 표시자 투명도를 정한다
    private void SetBlend(float blend)
    {
        _blend = blend;
        float eased = blend * blend * (3f - 2f * blend);
        _label.anchoredPosition = _labelRest + Vector2.right * (_shift * eased);
        Color c = _marker.color;
        c.a = eased;
        _marker.color = c;
    }
}
