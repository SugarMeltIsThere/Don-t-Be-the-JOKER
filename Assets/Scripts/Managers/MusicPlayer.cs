using UnityEngine;

public class MusicPlayer : MonoBehaviour
{
    [SerializeField] private AudioClip _track;
    [SerializeField] private float _volume = 0.25f;
    [SerializeField] private float _fadeIn = 3f;

    private AudioSource _source;
    private float _fade;

    // 배경 음악을 소리 없이 반복 재생으로 시작한다
    private void Start()
    {
        if (_track == null) return;
        _source = gameObject.AddComponent<AudioSource>();
        _source.clip = _track;
        _source.loop = true;
        _source.playOnAwake = false;
        _source.volume = 0f;
        _source.Play();
    }

    // 처음엔 천천히 키우고, 그 뒤로는 설정 창의 음악 음량을 그대로 따른다
    private void Update()
    {
        if (_source == null) return;
        _fade = Mathf.MoveTowards(_fade, 1f, Time.unscaledDeltaTime / _fadeIn);
        _source.volume = _volume * _fade * GameSettings.MusicScale;
    }
}
