using UnityEngine;

public class PhaseHint : MonoBehaviour
{
    [SerializeField] private CanvasGroup _group;
    [SerializeField] private GameObject _drawHint;
    [SerializeField] private GameObject _betHint;
    [SerializeField] private GameObject _aceHint;
    [SerializeField] private InteractionPrompt _prompt;
    [SerializeField] private float _visibleAlpha = 0.7f;
    [SerializeField] private float _fadeSpeed = 4f;

    private GameManager _gm;
    private bool _hasHint;

    // 상태 변경을 구독하고 지금 단계에 맞춘다
    private void Start()
    {
        _group.alpha = 0f;
        _gm = GameManager.Instance;
        if (_gm == null) return;
        _gm.StateChanged += Refresh;
        Refresh();
    }

    // 구독을 해제한다
    private void OnDestroy()
    {
        if (_gm != null) _gm.StateChanged -= Refresh;
    }

    // 내 차례 단계에 맞는 안내 한 줄만 켠다(드로우, 베팅, 에이스 선택)
    private void Refresh()
    {
        bool myTurn = PlayerActions.IsLocalTurn;
        bool draw = myTurn && _gm.CurrentPhase == GamePhase.Draw;
        bool bet = myTurn && _gm.CurrentPhase == GamePhase.Betting;
        bool ace = _gm.CurrentPhase == GamePhase.AceSelection && !_gm.IsAceConfirmed(PlayerActions.LocalSeat);
        _drawHint.SetActive(draw);
        _betHint.SetActive(bet);
        _aceHint.SetActive(ace);
        _hasHint = draw || bet || ace;
    }

    // 커서 옆 안내가 떠 있는 동안에는 바닥 안내를 흐리게 숨긴다
    private void Update()
    {
        float goal = _hasHint && (_prompt == null || !_prompt.IsShowing) ? _visibleAlpha : 0f;
        _group.alpha = Mathf.MoveTowards(_group.alpha, goal, _fadeSpeed * Time.deltaTime);
    }
}
