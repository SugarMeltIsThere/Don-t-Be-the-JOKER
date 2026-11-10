using TMPro;
using UnityEngine;

public class RoundBanner : MonoBehaviour
{
    [Header("Parts")]
    [SerializeField] private CanvasGroup _group;
    [SerializeField] private RectTransform _titleRoot;
    [SerializeField] private TMP_Text _title;
    [SerializeField] private TMP_Text _reason;
    [SerializeField] private TMP_Text _chips;

    [Header("Timing")]
    [SerializeField] private float _fadeIn = 0.22f;
    [SerializeField] private float _fadeOut = 0.45f;
    [SerializeField] private float _roundHold = 1.5f;
    [SerializeField] private float _resultHold = 3.4f;
    [SerializeField] private float _punchScale = 1.12f;

    [Header("Colors")]
    [SerializeField] private Color _winColor = new Color(0.9f, 0.74f, 0.42f);
    [SerializeField] private Color _loseColor = new Color(0.8f, 0.37f, 0.31f);
    [SerializeField] private Color _neutralColor = new Color(0.93f, 0.89f, 0.8f);

    [Header("Words")]
    [SerializeField] private string _roundFormat = "ROUND {0}";
    [SerializeField] private string _youLead = "YOU LEAD";
    [SerializeField] private string _opponentLeads = "OPPONENT LEADS";
    [SerializeField] private string _won = "ROUND WON";
    [SerializeField] private string _lost = "ROUND LOST";
    [SerializeField] private string _push = "PUSH";
    [SerializeField] private string _closerFormat = "{0} AGAINST {1}";
    [SerializeField] private string _youBusted = "YOU BUSTED";
    [SerializeField] private string _opponentBusted = "OPPONENT BUSTED";
    [SerializeField] private string _youFolded = "YOU FOLDED";
    [SerializeField] private string _opponentFolded = "OPPONENT FOLDED";
    [SerializeField] private string _youDrewJoker = "YOU DREW THE JOKER";
    [SerializeField] private string _opponentDrewJoker = "OPPONENT DREW THE JOKER";
    [SerializeField] private string _highCardFormat = "TIED AT {0}  ·  HIGH CARD DECIDES";
    [SerializeField] private string _potCarries = "THE POT CARRIES OVER";
    [SerializeField] private string _gainFormat = "+{0} CHIPS";
    [SerializeField] private string _lossFormat = "-{0} CHIPS";

    private GameManager _gm;
    private int _shownRound;
    private int _shownResultRound;
    private float _shownAt = -100f;
    private float _hold;

    // 숨겨 두고 상태 변경을 구독한다
    private void Start()
    {
        _group.alpha = 0f;
        _gm = GameManager.Instance;
        if (_gm != null) _gm.StateChanged += OnStateChanged;
    }

    // 구독을 해제한다
    private void OnDestroy()
    {
        if (_gm != null) _gm.StateChanged -= OnStateChanged;
    }

    // 라운드 결과가 나오면 결과 배너를, 새 라운드의 드로우가 시작되면 라운드 번호와 선공 배너를 한 번씩 띄운다
    private void OnStateChanged()
    {
        int round = _gm.Round;
        if (_gm.RoundOutcome != RoundOutcome.None && _shownResultRound != round)
        {
            _shownResultRound = round;
            ShowResult();
            return;
        }
        if (_gm.CurrentPhase == GamePhase.Draw && _gm.CurrentPlayer != ActivePlayer.None && _shownRound != round)
        {
            _shownRound = round;
            ShowRoundStart(round);
        }
    }

    // 라운드 번호와 누가 먼저 하는지 보인다
    private void ShowRoundStart(int round)
    {
        _title.SetText(string.Format(_roundFormat, round));
        _title.color = _neutralColor;
        _reason.SetText(_gm.CurrentPlayer == PlayerActions.LocalSeat ? _youLead : _opponentLeads);
        _chips.SetText(string.Empty);
        Begin(_roundHold);
    }

    // 내 기준 승패, 이유, 오가는 칩을 보인다
    private void ShowResult()
    {
        ActivePlayer me = PlayerActions.LocalSeat;
        ActivePlayer winner = _gm.RoundWinner;
        bool push = _gm.RoundOutcome == RoundOutcome.Push;
        bool won = !push && winner == me;

        _title.SetText(push ? _push : won ? _won : _lost);
        _title.color = push ? _neutralColor : won ? _winColor : _loseColor;
        _reason.SetText(Reason(me, winner));
        if (push) _chips.SetText(_potCarries);
        else _chips.SetText(won ? _gainFormat : _lossFormat, won ? _gm.RoundGain : _gm.RoundLoss);
        _chips.color = _title.color;
        Begin(_resultHold);
    }

    // 결과 이유를 내 기준 문장으로 만든다(진 쪽이 버스트, 다이, 조커를 했으면 그 사람을 주어로)
    private string Reason(ActivePlayer me, ActivePlayer winner)
    {
        bool iLost = winner != me;
        switch (_gm.RoundOutcome)
        {
            case RoundOutcome.Bust: return iLost ? _youBusted : _opponentBusted;
            case RoundOutcome.Fold: return iLost ? _youFolded : _opponentFolded;
            case RoundOutcome.Joker: return iLost ? _youDrewJoker : _opponentDrewJoker;
            case RoundOutcome.HighCard: return string.Format(_highCardFormat, Total(me));
            case RoundOutcome.Closer: return string.Format(_closerFormat, Total(me), Total(OpponentOf(me)));
            default: return string.Empty;
        }
    }

    // 좌석의 확정 합계를 글자로 만든다
    private string Total(ActivePlayer seat) => _gm.GetTotalText(seat);

    // 상대 좌석을 구한다
    private static ActivePlayer OpponentOf(ActivePlayer seat) =>
        seat == ActivePlayer.Player1 ? ActivePlayer.Player2 : ActivePlayer.Player1;

    // 배너를 띄우기 시작한다
    private void Begin(float hold)
    {
        _shownAt = Time.time;
        _hold = hold;
    }

    // 빠르게 나타나며 제목이 살짝 크게 시작해 제자리로 줄어들고, 머문 뒤 천천히 사라진다
    private void Update()
    {
        float t = Time.time - _shownAt;
        float alpha;
        if (t < _fadeIn) alpha = t / _fadeIn;
        else if (t < _fadeIn + _hold) alpha = 1f;
        else alpha = 1f - Mathf.Clamp01((t - _fadeIn - _hold) / _fadeOut);
        _group.alpha = alpha;

        float settle = Mathf.Clamp01(t / (_fadeIn * 2f));
        float eased = 1f - (1f - settle) * (1f - settle) * (1f - settle);
        _titleRoot.localScale = Vector3.one * Mathf.Lerp(_punchScale, 1f, eased);
    }
}
