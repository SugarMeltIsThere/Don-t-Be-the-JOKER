using TMPro;
using UnityEngine;

public class GameResultView : MonoBehaviour
{
    [SerializeField] private CanvasGroup _group;
    [SerializeField] private GameObject _victory;
    [SerializeField] private GameObject _defeat;
    [SerializeField] private GameObject _outOfChips;
    [SerializeField] private GameObject _opponentSurrendered;
    [SerializeField] private GameObject _youSurrendered;
    [SerializeField] private TMP_Text _chipsValue;
    [SerializeField] private float _fadeSpeed = 2.5f;

    private GameManager _gm;
    private bool _showing;

    // 숨겨 두고 경기 종료를 구독한다
    private void Start()
    {
        SetVisible(false);
        _gm = GameManager.Instance;
        if (_gm != null) _gm.GameEnded += OnGameEnded;
    }

    // 구독을 해제한다
    private void OnDestroy()
    {
        if (_gm != null) _gm.GameEnded -= OnGameEnded;
    }

    // 정상 종료(칩 소진이나 항복)면 승패, 이유, 내 최종 칩을 보여 준다
    private void OnGameEnded(GameEndReason reason)
    {
        if (reason != GameEndReason.Normal) return;

        ActivePlayer me = PlayerActions.LocalSeat;
        int myChips = me == ActivePlayer.Player1 ? _gm.Player1Chips : _gm.Player2Chips;
        int theirChips = me == ActivePlayer.Player1 ? _gm.Player2Chips : _gm.Player1Chips;
        bool won = myChips > theirChips;
        ActivePlayer surrendered = _gm.Surrendered;

        _victory.SetActive(won);
        _defeat.SetActive(!won);
        _youSurrendered.SetActive(surrendered == me);
        _opponentSurrendered.SetActive(surrendered != me && surrendered != ActivePlayer.None);
        _outOfChips.SetActive(surrendered == ActivePlayer.None);
        _chipsValue.SetText("{0}", myChips);
        SetVisible(true);
        _group.alpha = 0f;
        _showing = true;
    }

    // 보이기 시작하면 서서히 나타나게 한다
    private void Update()
    {
        if (!_showing || _group.alpha >= 1f) return;
        _group.alpha = Mathf.MoveTowards(_group.alpha, 1f, _fadeSpeed * Time.unscaledDeltaTime);
    }

    // 창을 켜거나 끈다
    private void SetVisible(bool visible)
    {
        _group.gameObject.SetActive(visible);
        _group.blocksRaycasts = visible;
    }
}
