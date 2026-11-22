using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class TutorialGuide : MonoBehaviour
{
    private enum Step
    {
        Welcome, Cut, Shared, Hidden, Draw, Ace, Opponent, Stand, Aces, Raise, Showdown, Items, Magnifier, FreePlay, Done
    }

    private const ActivePlayer Me = ActivePlayer.Player1;
    private const ActivePlayer Bot = ActivePlayer.Player2;
    private const float BotDelay = 1.3f;
    private const string LanguageKey = "TutorialLanguage";
    private const float CjkSize = 23f;

    private static readonly string[] KoreanFonts = { "Malgun Gothic" };
    private static readonly string[] JapaneseFonts = { "Yu Gothic UI", "Yu Gothic", "Meiryo", "MS Gothic", "Malgun Gothic" };
    private static TMP_FontAsset s_koreanFont;
    private static TMP_FontAsset s_japaneseFont;

    private static readonly Card[] OpeningCards =
    {
        new Card(Rank.Five, Suit.Clover),
        new Card(Rank.Four, Suit.Diamond),
        new Card(Rank.Ten, Suit.Spade),
        new Card(Rank.Ace, Suit.Heart),
        new Card(Rank.Two, Suit.Heart),
        new Card(Rank.Three, Suit.Clover),
        new Card(Rank.Two, Suit.Spade)
    };

    [SerializeField] private GameObject _panel;
    [SerializeField] private TMP_Text _counter;
    [SerializeField] private TMP_Text _body;
    [SerializeField] private Button _next;
    [SerializeField] private Button _finish;
    [SerializeField] private Button _keepPlaying;
    [SerializeField, TextArea(2, 5)] private string[] _steps;
    [SerializeField, TextArea(2, 5)] private string[] _stepsKorean;
    [SerializeField, TextArea(2, 5)] private string[] _stepsJapanese;
    [SerializeField] private Button _english;
    [SerializeField] private Button _korean;
    [SerializeField] private Button _japanese;

    private GameManager _gm;
    private Step _step;
    private int _round;
    private bool _usedMagnifier;
    private float _botAt = -1f;
    private int _language;
    private TMP_FontAsset _latinFont;
    private float _latinSize;
    private float _latinSpacing;

    // 튜토리얼일 때만 첫 단계를 보이고 버튼과 게임 이벤트를 연결한다
    private void Start()
    {
        if (!PracticeMode.Tutorial)
        {
            gameObject.SetActive(false);
            return;
        }
        _gm = GameManager.Instance;
        _gm.RoundStarted += OnRoundStarted;
        _gm.ItemUsed += OnItemUsed;
        _next.onClick.AddListener(Advance);
        _finish.onClick.AddListener(Finish);
        _keepPlaying.onClick.AddListener(() => _panel.SetActive(false));
        _latinFont = _body.font;
        _latinSize = _body.fontSize;
        _latinSpacing = _body.lineSpacing;
        SetupLanguageButton(_english, 0, null);
        SetupLanguageButton(_korean, 1, KoreanFont());
        SetupLanguageButton(_japanese, 2, JapaneseFont());
        _language = Mathf.Clamp(PlayerPrefs.GetInt(LanguageKey, 0), 0, 2);
        Show(Step.Welcome);
    }

    // 구독을 해제한다
    private void OnDestroy()
    {
        if (_gm == null) return;
        _gm.RoundStarted -= OnRoundStarted;
        _gm.ItemUsed -= OnItemUsed;
    }

    // 라운드 수를 센다
    private void OnRoundStarted(int round) => _round = round;

    // 내가 돋보기를 쓴 것을 기록한다
    private void OnItemUsed(ActivePlayer seat, ItemType type)
    {
        if (seat == Me && type == ItemType.Magnifier) _usedMagnifier = true;
    }

    // 첫 라운드에는 상대를 대신 움직이고, 지금 단계의 조건이 채워지면 다음 단계로 넘어간다
    private void Update()
    {
        if (_gm == null) return;
        if (_round == 1) DriveBot();
        if (IsStepDone()) Advance();
    }

    // 지금 단계에서 기다리던 일이 일어났는지 확인한다(설명만 있는 단계는 NEXT로 넘긴다)
    private bool IsStepDone()
    {
        switch (_step)
        {
            case Step.Cut: return _round >= 1 && _gm.CurrentPhase == GamePhase.Draw;
            case Step.Draw: return Count(Me) >= 2;
            case Step.Opponent: return Count(Bot) >= 2 && (_gm.CurrentPlayer == Me || _gm.CurrentPhase != GamePhase.Draw);
            case Step.Stand: return _gm.IsStanding(Me) && _gm.CurrentPhase != GamePhase.Draw;
            case Step.Aces: return _gm.IsAceConfirmed(Me);
            case Step.Raise: return _gm.Player1TotalBet > 1 || _gm.CurrentPhase != GamePhase.Betting;
            case Step.Showdown: return _round >= 2 && _gm.CurrentPhase == GamePhase.Draw;
            case Step.Magnifier: return _usedMagnifier || _round >= 3 || (_round == 2 && _gm.CurrentPhase != GamePhase.Draw);
            case Step.FreePlay: return _round >= 3 || _gm.CurrentPhase == GamePhase.GameEnd;
            default: return false;
        }
    }

    // 다음 단계로 넘어간다
    private void Advance()
    {
        if (_step >= Step.Done) return;
        Show(_step + 1);
    }

    // 단계 글과 번호를 보이고, 설명 단계면 NEXT를, 마지막이면 끝내기 버튼을 보인다(선공 단계에서 경기를 시작해 첫 라운드 패를 정해 두고, 아이템 단계에서는 돋보기를 주고 AI를 앉힌다)
    private void Show(Step step)
    {
        if (step == Step.Cut)
        {
            _gm.InitializeGame(PracticeMode.PlayerCharacter, PracticeMode.BotCharacter);
            _gm.StackDeckTop(OpeningCards);
        }
        _step = step;
        _panel.SetActive(true);
        int index = (int)step;
        ShowText();
        _counter.SetText("{0} / {1}", index + 1, (int)Step.Done + 1);
        bool waitsForNext = step == Step.Welcome || step == Step.Shared || step == Step.Hidden || step == Step.Ace || step == Step.Items;
        _next.gameObject.SetActive(waitsForNext);
        _finish.gameObject.SetActive(step == Step.Done);
        _keepPlaying.gameObject.SetActive(step == Step.Done);

        if (step == Step.Items)
        {
            _gm.GrantItem(Me, ItemType.Magnifier);
            if (_gm.GetComponent<PracticeBot>() == null) _gm.gameObject.AddComponent<PracticeBot>();
        }
    }

    // 언어 버튼을 누르면 그 언어로 바꾸게 하고, 글자를 그 언어 글꼴로 보이게 한다
    private void SetupLanguageButton(Button button, int language, TMP_FontAsset font)
    {
        if (button == null) return;
        button.onClick.AddListener(() => SetLanguage(language));
        if (font == null) return;
        foreach (TMP_Text label in button.GetComponentsInChildren<TMP_Text>(true)) label.font = font;
    }

    // 언어를 바꾸고 기억한 뒤 지금 단계 글을 다시 쓴다
    private void SetLanguage(int language)
    {
        _language = language;
        PlayerPrefs.SetInt(LanguageKey, language);
        ShowText();
    }

    // 지금 언어의 단계 글을 그 언어 글꼴과 크기로 쓰고, 고른 언어 버튼만 밝게 한다(한국어·일본어 글꼴이 없으면 영어로 보인다)
    private void ShowText()
    {
        TMP_FontAsset cjk = _language == 1 ? KoreanFont() : _language == 2 ? JapaneseFont() : null;
        string[] steps = cjk == null ? _steps : _language == 1 ? _stepsKorean : _stepsJapanese;
        _body.font = cjk != null ? cjk : _latinFont;
        _body.fontSize = cjk != null ? CjkSize : _latinSize;
        _body.lineSpacing = cjk != null ? 8f : _latinSpacing;
        int index = (int)_step;
        _body.SetText(steps != null && index < steps.Length ? steps[index] : string.Empty);

        int shown = cjk == null ? 0 : _language;
        Highlight(_english, shown == 0);
        Highlight(_korean, shown == 1);
        Highlight(_japanese, shown == 2);
    }

    // 고른 언어 버튼은 밝게, 나머지는 흐리게 한다
    private static void Highlight(Button button, bool selected)
    {
        if (button == null) return;
        foreach (TMP_Text label in button.GetComponentsInChildren<TMP_Text>(true)) label.alpha = selected ? 1f : 0.4f;
    }

    // 윈도우에 깔린 한국어 글꼴로 글꼴 에셋을 한 번 만든다
    private static TMP_FontAsset KoreanFont() => s_koreanFont != null ? s_koreanFont : s_koreanFont = SystemFont(KoreanFonts);

    // 윈도우에 깔린 일본어 글꼴로 글꼴 에셋을 한 번 만든다
    private static TMP_FontAsset JapaneseFont() => s_japaneseFont != null ? s_japaneseFont : s_japaneseFont = SystemFont(JapaneseFonts);

    // 이름 목록에서 처음 찾은 시스템 글꼴로 동적 글꼴 에셋을 만든다
    private static TMP_FontAsset SystemFont(string[] families)
    {
        foreach (string family in families)
        {
            TMP_FontAsset font = TMP_FontAsset.CreateFontAsset(family, "Regular");
            if (font != null) return font;
        }
        return null;
    }

    // 첫 라운드의 상대: 한 장 더 받고 멈추며, 에이스는 바로 확정하고, 베팅은 따라간다(내가 설명을 볼 때까지는 기다린다)
    private void DriveBot()
    {
        bool botMoves = (_gm.CurrentPhase == GamePhase.Draw || _gm.CurrentPhase == GamePhase.Betting) && _gm.CurrentPlayer == Bot;
        if (_gm.CurrentPhase == GamePhase.AceSelection && !_gm.IsAceConfirmed(Bot)) _gm.OnAceSelectionConfirmed(Bot);
        if (!botMoves || _step < Step.Opponent)
        {
            _botAt = -1f;
            return;
        }
        if (_botAt < 0f) _botAt = Time.time + BotDelay;
        if (Time.time < _botAt) return;
        _botAt = -1f;

        if (_gm.CurrentPhase == GamePhase.Draw)
        {
            if (Count(Bot) < 2 && _gm.CanHit) _gm.OnHit();
            else _gm.OnStand(rang: true);
        }
        else if (_gm.CanCall) _gm.OnCall();
        else if (_gm.CanRaise) _gm.OnRaise(1);
        else _gm.OnDie();
    }

    // 좌석의 손패 장수를 센다
    private int Count(ActivePlayer seat)
    {
        var cards = seat == ActivePlayer.Player1 ? _gm.GetPlayer1Cards() : _gm.GetPlayer2Cards();
        return cards != null ? cards.Count : 0;
    }

    // 튜토리얼을 끝내고 로비로 돌아간다
    private void Finish()
    {
        if (NetworkManager.Instance != null) NetworkManager.Instance.LeaveToLobby();
        else SceneManager.LoadScene(0);
    }
}
