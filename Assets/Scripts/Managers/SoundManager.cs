using System.Collections;
using UnityEngine;

public class SoundManager : MonoBehaviour
{
    private const int SourceCount = 8;

    public static SoundManager Instance { get; private set; }

    [Header("Clips")]
    [SerializeField] private AudioClip[] _deal;
    [SerializeField] private AudioClip[] _flip;
    [SerializeField] private AudioClip[] _chip;
    [SerializeField] private AudioClip[] _bell;
    [SerializeField] private AudioClip[] _flashClick;
    [SerializeField] private AudioClip[] _flashRing;
    [SerializeField] private AudioClip[] _whoosh;
    [SerializeField] private AudioClip[] _magnifier;
    [SerializeField] private AudioClip[] _tick;
    [SerializeField] private AudioClip[] _joker;
    [SerializeField] private AudioClip _ambience;

    [Header("Mix")]
    [SerializeField] private float _sfxVolume = 0.8f;
    [SerializeField] private float _ambienceVolume = 0.35f;
    [SerializeField] private float _pitchJitter = 0.05f;
    [SerializeField] private float _flashShineDelay = 0.3f;
    [SerializeField] private float _chipVolume = 0.45f;
    [SerializeField] private float _jokerSoundDelay = 0.5f;
    [SerializeField] private TurnTimer _turnTimer;

    private readonly AudioSource[] _sources = new AudioSource[SourceCount];
    private int _nextSource;
    private AudioSource _ambient;
    private GameManager _gm;
    private ActivePlayer _lastPlayer;

    // 다른 연출이 소리를 낼 수 있게 등록한다
    private void Awake() => Instance = this;

    // 효과음 소스 여러 개와 배경음 소스를 만들고(배경음이 있으면 반복 재생) 게임 이벤트를 구독한다
    private void Start()
    {
        for (int i = 0; i < SourceCount; i++)
        {
            _sources[i] = gameObject.AddComponent<AudioSource>();
            _sources[i].playOnAwake = false;
        }
        _ambient = gameObject.AddComponent<AudioSource>();
        _ambient.clip = _ambience;
        _ambient.loop = true;
        _ambient.volume = _ambienceVolume;
        if (_ambience != null) _ambient.Play();

        _gm = GameManager.Instance;
        if (_gm == null) return;
        _gm.RoundRevealed += OnRoundRevealed;
        _gm.ItemUsed += OnItemUsed;
        _gm.BellRung += OnBellRung;
        _gm.JokerDrawn += OnJokerDrawn;
        _gm.StateChanged += OnStateChanged;
        if (_turnTimer != null) _turnTimer.WarningSecond += OnWarningSecond;
        _lastPlayer = _gm.CurrentPlayer;
    }

    // 구독을 해제한다
    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (_turnTimer != null) _turnTimer.WarningSecond -= OnWarningSecond;
        if (_gm == null) return;
        _gm.RoundRevealed -= OnRoundRevealed;
        _gm.ItemUsed -= OnItemUsed;
        _gm.BellRung -= OnBellRung;
        _gm.JokerDrawn -= OnJokerDrawn;
        _gm.StateChanged -= OnStateChanged;
    }

    // 카드 한 장이 움직이기 시작할 때 미끄러지는 소리를 낸다(움직임이 늦게 시작하면 그때 낸다)
    public void PlayDeal(float delay)
    {
        if (delay <= 0f) Play(_deal, 0.9f);
        else StartCoroutine(PlayLater(_deal, delay, 0.9f));
    }

    // 칩 한 개가 더미에 내려앉을 때 소리를 낸다
    public void PlayChip() => Play(_chip, _chipVolume);

    // 누군가 조커를 뽑으면 카드가 앞면으로 뒤집힐 즈음 묵직한 충격음을 낸다
    private void OnJokerDrawn(ActivePlayer seat) => StartCoroutine(PlayLater(_joker, _jokerSoundDelay, 1f));

    // 누군가 종을 누르면 종소리를 낸다
    private void OnBellRung(ActivePlayer seat) => Play(_bell, 0.9f);

    // 라운드 공개 때 카드 뒤집는 소리를 낸다
    private void OnRoundRevealed() => Play(_flip, 1f);

    // 아이템 종류마다 소리를 낸다(플래시를 당한 쪽은 빛이 닿을 때 이명도)
    private void OnItemUsed(ActivePlayer seat, ItemType type)
    {
        switch (type)
        {
            case ItemType.Magnifier:
                Play(_magnifier, 0.8f);
                break;
            case ItemType.Bribe:
                Play(_whoosh, 0.9f);
                break;
            case ItemType.Flash:
                StartCoroutine(PlayLater(_flashClick, _flashShineDelay - 0.05f, 1f));
                if (seat != PlayerActions.LocalSeat) StartCoroutine(PlayLater(_flashRing, _flashShineDelay, 1f));
                break;
        }
    }

    // 선공 카드가 뒤집히면 카드 뒤집는 소리를 낸다
    private void OnStateChanged()
    {
        if (_gm.IsCutting && _lastPlayer == ActivePlayer.None && _gm.CurrentPlayer != ActivePlayer.None) Play(_flip, 1f);
        _lastPlayer = _gm.CurrentPlayer;
    }

    // 제한 시간이 얼마 남지 않으면 째깍 소리를 낸다
    private void OnWarningSecond(int seconds) => Play(_tick, 0.7f);

    // 정한 시간 뒤에 소리를 낸다
    private IEnumerator PlayLater(AudioClip[] clips, float delay, float volume)
    {
        yield return new WaitForSeconds(delay);
        Play(clips, volume);
    }

    // 후보 중 하나를 골라 돌아가며 쓰는 소스에서 높낮이를 살짝 바꿔 한 번 낸다
    private void Play(AudioClip[] clips, float volume)
    {
        if (clips == null || clips.Length == 0) return;
        AudioClip clip = clips[Random.Range(0, clips.Length)];
        if (clip == null) return;

        AudioSource source = _sources[_nextSource];
        _nextSource = (_nextSource + 1) % SourceCount;
        source.pitch = 1f + Random.Range(-_pitchJitter, _pitchJitter);
        source.PlayOneShot(clip, volume * _sfxVolume);
    }
}
