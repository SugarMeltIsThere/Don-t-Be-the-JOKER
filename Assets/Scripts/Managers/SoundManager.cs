using System.Collections;
using UnityEngine;

public class SoundManager : MonoBehaviour
{
    [Header("Clips")]
    [SerializeField] private AudioClip _deal;
    [SerializeField] private AudioClip _flip;
    [SerializeField] private AudioClip _chip;
    [SerializeField] private AudioClip _bell;
    [SerializeField] private AudioClip _flashClick;
    [SerializeField] private AudioClip _flashRing;
    [SerializeField] private AudioClip _whoosh;
    [SerializeField] private AudioClip _magnifier;
    [SerializeField] private AudioClip _tick;
    [SerializeField] private AudioClip _ambience;

    [Header("Mix")]
    [SerializeField] private float _sfxVolume = 0.8f;
    [SerializeField] private float _ambienceVolume = 0.35f;
    [SerializeField] private float _flashShineDelay = 0.3f;
    [SerializeField] private TurnTimer _turnTimer;

    private AudioSource _sfx;
    private AudioSource _ambient;
    private GameManager _gm;
    private GamePhase _lastPhase;
    private ActivePlayer _lastPlayer;
    private bool _dealtSinceState;
    private int _lastChipSignature;

    // 효과음과 배경음 소스를 만들고 배경음을 반복 재생한 뒤 게임 이벤트를 구독한다
    private void Start()
    {
        _sfx = gameObject.AddComponent<AudioSource>();
        _sfx.playOnAwake = false;
        _ambient = gameObject.AddComponent<AudioSource>();
        _ambient.clip = _ambience;
        _ambient.loop = true;
        _ambient.volume = _ambienceVolume;
        _ambient.Play();

        _gm = GameManager.Instance;
        if (_gm == null) return;
        _gm.CardDealt += OnCardDealt;
        _gm.LastCardReplaced += OnLastCardReplaced;
        _gm.RoundRevealed += OnRoundRevealed;
        _gm.ItemUsed += OnItemUsed;
        _gm.StateChanged += OnStateChanged;
        if (_turnTimer != null) _turnTimer.WarningSecond += OnWarningSecond;

        _lastPhase = _gm.CurrentPhase;
        _lastPlayer = _gm.CurrentPlayer;
        _lastChipSignature = ChipSignature();
    }

    // 구독을 해제한다
    private void OnDestroy()
    {
        if (_turnTimer != null) _turnTimer.WarningSecond -= OnWarningSecond;
        if (_gm == null) return;
        _gm.CardDealt -= OnCardDealt;
        _gm.LastCardReplaced -= OnLastCardReplaced;
        _gm.RoundRevealed -= OnRoundRevealed;
        _gm.ItemUsed -= OnItemUsed;
        _gm.StateChanged -= OnStateChanged;
    }

    // 카드가 나뉘면 사각 소리를 내고 이번 상태 변경이 히트였다고 표시한다
    private void OnCardDealt(ActivePlayer seat, int index)
    {
        _dealtSinceState = true;
        Play(_deal, 0.9f);
    }

    // 뇌물로 카드가 바뀌면 카드가 슈로 돌아가고 새로 나오는 소리를 낸다
    private void OnLastCardReplaced(ActivePlayer seat)
    {
        _dealtSinceState = true;
        StartCoroutine(PlayLater(_deal, 0.4f, 0.8f));
        StartCoroutine(PlayLater(_deal, 0.85f, 0.9f));
    }

    // 라운드 공개 때 카드 뒤집는 소리를 낸다
    private void OnRoundRevealed() => Play(_flip, 1f);

    // 아이템 종류마다 소리를 낸다(플래시를 당한 쪽은 빛이 닿을 때 충격음과 이명)
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

    // 칩이 움직였으면 칩 소리를, 드로우에서 카드 없이 차례가 넘어가면 벨 소리를 낸다
    private void OnStateChanged()
    {
        int chips = ChipSignature();
        if (chips != _lastChipSignature) Play(_chip, 0.8f);
        _lastChipSignature = chips;

        bool wasDraw = _lastPhase == GamePhase.Draw;
        bool turnPassed = _gm.CurrentPlayer != _lastPlayer || _gm.CurrentPhase != GamePhase.Draw;
        if (wasDraw && turnPassed && !_dealtSinceState) Play(_bell, 0.9f);

        _lastPhase = _gm.CurrentPhase;
        _lastPlayer = _gm.CurrentPlayer;
        _dealtSinceState = false;
    }

    // 제한 시간이 얼마 남지 않으면 째깍 소리를 낸다
    private void OnWarningSecond(int seconds) => Play(_tick, 0.7f);

    // 양쪽 칩과 베팅, 팟을 한 숫자로 묶어 바뀌었는지 비교한다
    private int ChipSignature() =>
        _gm.Player1Chips * 7919 + _gm.Player2Chips * 104729 + _gm.Player1TotalBet * 31 + _gm.Player2TotalBet * 131 + _gm.Pot;

    // 정한 시간 뒤에 소리를 낸다
    private IEnumerator PlayLater(AudioClip clip, float delay, float volume)
    {
        yield return new WaitForSeconds(delay);
        Play(clip, volume);
    }

    // 효과음을 한 번 낸다
    private void Play(AudioClip clip, float volume)
    {
        if (clip == null) return;
        _sfx.PlayOneShot(clip, volume * _sfxVolume);
    }
}
