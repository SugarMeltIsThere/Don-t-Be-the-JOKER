using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ButtonSound : MonoBehaviour, IPointerEnterHandler
{
    [SerializeField] private AudioClip _hoverSound;
    [SerializeField] private AudioClip _clickSound;
    [SerializeField] private float _volume = 0.35f;

    private static AudioSource s_audio;

    private Button _button;

    // 버튼을 찾아 누를 때 소리를 내게 한다
    private void Awake()
    {
        _button = GetComponent<Button>();
        if (_button != null) _button.onClick.AddListener(() => Play(_clickSound));
    }

    // 누를 수 있는 버튼에 마우스가 올라오면 소리를 낸다
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_button == null || _button.interactable) Play(_hoverSound);
    }

    // 모든 버튼이 같이 쓰는 소리 소스로 효과음 음량에 맞춰 한 번 낸다
    private void Play(AudioClip clip)
    {
        if (clip == null) return;
        if (s_audio == null)
        {
            s_audio = new GameObject("ButtonSound").AddComponent<AudioSource>();
            s_audio.playOnAwake = false;
            DontDestroyOnLoad(s_audio.gameObject);
        }
        s_audio.PlayOneShot(clip, _volume * GameSettings.EffectsScale);
    }
}
