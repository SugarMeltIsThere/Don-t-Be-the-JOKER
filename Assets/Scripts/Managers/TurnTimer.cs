using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TurnTimer : MonoBehaviour
{
    [SerializeField] private float _turnSeconds = 20f;
    [SerializeField] private float _warningSeconds = 5f;
    [SerializeField] private GameObject _root;
    [SerializeField] private Image _ring;
    [SerializeField] private TMP_Text _secondsText;
    [SerializeField] private Color _myColor = new Color(0.86f, 0.7f, 0.38f);
    [SerializeField] private Color _opponentColor = new Color(0.55f, 0.55f, 0.58f);
    [SerializeField] private Color _warningColor = new Color(0.9f, 0.2f, 0.15f);

    private GameManager _gm;
    private float _deadline;
    private bool _running;
    private int _shownSeconds = -1;

    public event Action<int> WarningSecond;

    private static bool IsAuthority => NetworkGameManager.Instance == null || NetworkGameManager.Instance.IsHost;

    // 상태 변경을 구독하고 표시를 숨긴다
    private void Start()
    {
        _root.SetActive(false);
        _gm = GameManager.Instance;
        if (_gm == null) return;
        _gm.StateChanged += OnStateChanged;
        OnStateChanged();
    }

    // 구독을 해제한다
    private void OnDestroy()
    {
        if (_gm != null) _gm.StateChanged -= OnStateChanged;
    }

    // 드로우나 베팅 차례가 바뀌거나 행동이 있을 때마다 제한 시간을 처음부터 다시 잰다
    private void OnStateChanged()
    {
        bool timed = (_gm.CurrentPhase == GamePhase.Draw || _gm.CurrentPhase == GamePhase.Betting)
            && _gm.CurrentPlayer != ActivePlayer.None;
        _running = timed;
        _root.SetActive(timed);
        if (!timed) return;

        _deadline = Time.time + _turnSeconds;
        _shownSeconds = -1;
    }

    // 남은 시간을 고리와 숫자로 보여 주고, 시간이 다 되면 호스트가 안전한 행동을 대신 한다
    private void Update()
    {
        if (!_running) return;

        float remaining = Mathf.Max(0f, _deadline - Time.time);
        int seconds = Mathf.CeilToInt(remaining);
        bool warning = remaining <= _warningSeconds;
        _ring.fillAmount = remaining / _turnSeconds;
        _ring.color = warning ? _warningColor : PlayerActions.IsLocalTurn ? _myColor : _opponentColor;

        if (seconds != _shownSeconds)
        {
            _shownSeconds = seconds;
            _secondsText.SetText("{0}", seconds);
            _secondsText.color = _ring.color;
            if (warning && seconds > 0 && PlayerActions.IsLocalTurn) WarningSecond?.Invoke(seconds);
        }

        if (remaining > 0f) return;
        _running = false;
        if (IsAuthority) ActOnTimeout();
    }

    // 드로우 차례면 스탠드, 베팅 차례면 콜(첫 베팅이라 콜이 안 되면 최소 레이즈, 그것도 안 되면 다이)을 한다
    private void ActOnTimeout()
    {
        Debug.Log($"[시간 초과] {_gm.CurrentPlayer} / {_gm.CurrentPhase}");
        if (_gm.CurrentPhase == GamePhase.Draw) _gm.OnStand();
        else if (_gm.CanCall) _gm.OnCall();
        else if (_gm.CanRaise) _gm.OnRaise(1);
        else _gm.OnDie();
    }
}
