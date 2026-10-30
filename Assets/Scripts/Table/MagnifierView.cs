using UnityEngine;

public class MagnifierView : MonoBehaviour
{
    private const float CardScaleInLens = 0.85f;
    private const float CardDepthInLens = 0.002f;

    [SerializeField] private Transform _lens;
    [SerializeField] private Transform _deckTop;
    [SerializeField] private Camera _camera;
    [SerializeField] private CardSpriteDatabase _cardSpriteDatabase;
    [SerializeField] private Material _cardMaterial;
    [SerializeField] private float _hoverDistance = 0.08f;
    [SerializeField] private float _readingDistanceFromCamera = 0.38f;
    [SerializeField] private Vector2 _readingViewportPoint = new Vector2(0.5f, 0.5f);
    [SerializeField] private Vector3 _restOffset = new Vector3(-0.08f, 0.22f, -0.12f);
    [SerializeField] private float _moveDuration = 0.4f;

    private CardView _peekCard;
    private GameManager _gm;
    private Vector3 _fromPosition;
    private Vector3 _toPosition;
    private Quaternion _fromRotation;
    private Quaternion _toRotation;
    private float _elapsed;
    private bool _hideWhenArrived;

    // 렌즈 안에 비칠 카드를 렌즈의 자식으로 만들고 렌즈를 숨긴다
    private void Awake()
    {
        if (_camera == null) _camera = Camera.main;

        _peekCard = CardView.Create(_lens, _cardMaterial, _cardSpriteDatabase);
        _peekCard.transform.localPosition = new Vector3(0f, -CardDepthInLens, 0f);
        _peekCard.transform.localRotation = Quaternion.identity;
        _peekCard.transform.localScale = Vector3.one * CardScaleInLens;
        _peekCard.gameObject.SetActive(false);

        _lens.gameObject.SetActive(false);
    }

    // 게임 이벤트를 구독한 뒤 렌즈가 움직일 때까지 Update를 끈다
    private void Start()
    {
        enabled = false;

        _gm = GameManager.Instance;
        if (_gm == null) return;

        _gm.ItemUsed += OnItemUsed;
        _gm.PeekShown += OnPeekShown;
        _gm.CardDealt += OnCardDealt;
        _gm.RoundStarted += OnRoundStarted;
        _gm.StateChanged += OnStateChanged;
    }

    // 구독을 해제한다
    private void OnDestroy()
    {
        if (_gm == null) return;

        _gm.ItemUsed -= OnItemUsed;
        _gm.PeekShown -= OnPeekShown;
        _gm.CardDealt -= OnCardDealt;
        _gm.RoundStarted -= OnRoundStarted;
        _gm.StateChanged -= OnStateChanged;
        _gm = null;
    }

    // 누가 돋보기를 쓰든 렌즈를 덱 바로 위로 가져온다
    private void OnItemUsed(ActivePlayer seat, ItemType type)
    {
        if (type != ItemType.Magnifier || _peekCard.gameObject.activeSelf) return;
        MoveLens(PointOnSightLine(_hoverDistance), hideWhenArrived: false);
    }

    // 돋보기를 쓴 사람의 화면에서만 렌즈를 덱에서 눈앞 화면 가운데로 들어 올리고 렌즈 안에 덱 맨 위 카드를 비춘다
    private void OnPeekShown(Card card)
    {
        _peekCard.SetCard(card, faceUp: true);
        _peekCard.gameObject.SetActive(true);

        Vector3 reading = _camera.ViewportToWorldPoint(new Vector3(
            _readingViewportPoint.x, _readingViewportPoint.y, _readingDistanceFromCamera));
        MoveLens(reading, hideWhenArrived: false);
    }

    // 덱에서 카드가 나가면 본 카드가 더 이상 맨 위가 아니므로 치운다
    private void OnCardDealt(ActivePlayer seat, int index) => Hide();

    // 새 라운드가 시작되면 치운다
    private void OnRoundStarted(int round) => Hide();

    // 드로우 단계가 끝나면 치운다
    private void OnStateChanged()
    {
        if (_gm.CurrentPhase != GamePhase.Draw) Hide();
    }

    // 덱에서 카메라 쪽으로 정한 거리만큼 떨어진 시선 위의 점을 구한다
    private Vector3 PointOnSightLine(float distanceFromDeck)
    {
        Vector3 toCamera = (_camera.transform.position - _deckTop.position).normalized;
        return _deckTop.position + toCamera * distanceFromDeck;
    }

    // 렌즈 면이 카메라를 향하고 렌즈 안 카드의 윗변이 화면 위쪽을 향하는 방향을 구한다
    private Quaternion FacingCamera(Vector3 lensPosition)
    {
        Vector3 toCamera = (_camera.transform.position - lensPosition).normalized;
        return Quaternion.LookRotation(_camera.transform.up, toCamera);
    }

    // 렌즈를 쉬는 자리로 돌려보내고 도착하면 숨긴다
    private void Hide()
    {
        if (!_lens.gameObject.activeSelf || _hideWhenArrived) return;
        MoveLens(_deckTop.position + _restOffset, hideWhenArrived: true);
    }

    // 렌즈 이동을 시작하고 숨어 있었다면 쉬는 자리에서 나타나게 한다
    private void MoveLens(Vector3 target, bool hideWhenArrived)
    {
        if (!_lens.gameObject.activeSelf)
        {
            Vector3 rest = _deckTop.position + _restOffset;
            _lens.SetPositionAndRotation(rest, FacingCamera(rest));
            _lens.gameObject.SetActive(true);
        }

        _fromPosition = _lens.position;
        _fromRotation = _lens.rotation;
        _toPosition = target;
        _toRotation = FacingCamera(target);
        _elapsed = 0f;
        _hideWhenArrived = hideWhenArrived;
        enabled = true;
    }

    // 렌즈가 움직이는 동안에만 자세를 보간하고 다 치웠으면 렌즈와 카드를 숨긴다
    private void Update()
    {
        _elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(_elapsed / _moveDuration);
        float eased = t * t * (3f - 2f * t);
        _lens.SetPositionAndRotation(
            Vector3.LerpUnclamped(_fromPosition, _toPosition, eased),
            Quaternion.Slerp(_fromRotation, _toRotation, eased));
        if (t < 1f) return;

        enabled = false;
        if (!_hideWhenArrived) return;

        _peekCard.gameObject.SetActive(false);
        _lens.gameObject.SetActive(false);
        _hideWhenArrived = false;
    }
}
