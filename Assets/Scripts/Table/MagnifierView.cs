using UnityEngine;

public class MagnifierView : MonoBehaviour
{
    private const float RevealLift = 0.0006f;

    [SerializeField] private Transform _lens;
    [SerializeField] private Transform _shoeCard;
    [SerializeField] private CardSpriteDatabase _cardSpriteDatabase;
    [SerializeField] private Material _cardMaterial;
    [SerializeField] private Material _revealMaterial;
    [SerializeField] private float _hoverHeight = 0.035f;
    [SerializeField] private float _lensScale = 1.6f;
    [SerializeField] private float _lensCenterOffset = 0.025f;
    [SerializeField] private Vector3 _restOffset = new Vector3(-0.3f, 0.05f, -0.3f);
    [SerializeField] private float _moveDuration = 0.45f;
    [SerializeField] private float _holdSeconds = 1f;
    [SerializeField] private float _focusDistance = 0.17f;
    [SerializeField] private float _focusNormalWeight = 0.6f;

    private CardView _peekCard;
    private GameManager _gm;
    private Vector3 _fromPosition;
    private Vector3 _toPosition;
    private Quaternion _fromRotation;
    private Quaternion _toRotation;
    private Vector3 _fromScale;
    private Vector3 _toScale;
    private float _elapsed;
    private bool _moving;
    private bool _hideWhenArrived;
    private float _returnTimer;
    private TableCamera _tableCamera;

    // 슈 맨 위에 뒷면 카드를 놓고, 같은 자리에 렌즈 안에서만 보이는 앞면 카드를 준비한 뒤 렌즈를 숨긴다
    private void Awake()
    {
        CardView.Create(_shoeCard, _cardMaterial, _cardSpriteDatabase);

        _peekCard = CardView.Create(_shoeCard, _revealMaterial, _cardSpriteDatabase);
        _peekCard.transform.localPosition = new Vector3(0f, RevealLift, 0f);
        _peekCard.gameObject.SetActive(false);

        _lens.gameObject.SetActive(false);
    }

    // 게임 이벤트를 구독한 뒤 렌즈가 움직일 때까지 Update를 끈다
    private void Start()
    {
        enabled = false;
        if (Camera.main != null) _tableCamera = Camera.main.GetComponent<TableCamera>();

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

    // 누가 돋보기를 쓰든 돋보기를 슈 맨 위 카드 바로 위로 가져와 잠깐 띄웠다가 돌려보낸다
    private void OnItemUsed(ActivePlayer seat, ItemType type)
    {
        if (type != ItemType.Magnifier) return;
        if (!_peekCard.gameObject.activeSelf && Camera.main != null) HoverOverShoe(Camera.main.transform.position);
        StartReturnTimer();
    }

    // 돋보기를 쓴 사람의 화면에서만 시선을 카드 쪽으로 비스듬히 당기고 렌즈 너머로 앞면이 보이게 한다
    private void OnPeekShown(Card card)
    {
        _peekCard.SetCard(card, faceUp: true);
        _peekCard.gameObject.SetActive(true);
        StartReturnTimer();
        if (_tableCamera == null)
        {
            if (Camera.main != null) HoverOverShoe(Camera.main.transform.position);
            return;
        }

        Vector3 toCamera = (_tableCamera.transform.position - _shoeCard.position).normalized;
        Vector3 direction = (_shoeCard.up * _focusNormalWeight + toCamera).normalized;
        Vector3 eye = _shoeCard.position + direction * _focusDistance;
        _tableCamera.Focus(eye, Quaternion.LookRotation(_shoeCard.position - eye, Vector3.up));
        HoverOverShoe(eye);
    }

    // 다가가는 시간과 들여다보는 시간이 지나면 돌아오도록 시간을 잰다
    private void StartReturnTimer()
    {
        _returnTimer = _moveDuration + _holdSeconds;
        enabled = true;
    }

    // 눈과 맨 위 카드를 잇는 선 위, 카드에서 정한 거리만큼 떨어진 곳에 렌즈 가운데를 두고 렌즈 면이 눈을 향하게(손잡이는 화면 아래쪽) 보낸다
    private void HoverOverShoe(Vector3 eye)
    {
        Vector3 toEye = (eye - _shoeCard.position).normalized;
        Vector3 forward = Vector3.ProjectOnPlane(Vector3.up, toEye);
        if (forward.sqrMagnitude < 1e-4f) forward = _shoeCard.forward;
        Quaternion rotation = Quaternion.LookRotation(forward.normalized, toEye);
        Vector3 center = _shoeCard.position + toEye * _hoverHeight;
        MoveLens(center - rotation * Vector3.forward * (_lensCenterOffset * _lensScale), rotation, Vector3.one * _lensScale, hideWhenArrived: false);
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

    // 앞면 카드를 끄고 시선을 풀고 돋보기를 쉬는 자리로 돌려보낸 뒤 도착하면 숨긴다
    private void Hide()
    {
        _returnTimer = 0f;
        _peekCard.gameObject.SetActive(false);
        if (_tableCamera != null) _tableCamera.Release();
        if (!_lens.gameObject.activeSelf || _hideWhenArrived) return;
        MoveLens(_shoeCard.position + _restOffset, _shoeCard.rotation, Vector3.one, hideWhenArrived: true);
    }

    // 돋보기 이동을 시작하고 숨어 있었다면 쉬는 자리에서 나타나게 한다
    private void MoveLens(Vector3 position, Quaternion rotation, Vector3 scale, bool hideWhenArrived)
    {
        if (!_lens.gameObject.activeSelf)
        {
            _lens.SetPositionAndRotation(_shoeCard.position + _restOffset, rotation);
            _lens.localScale = Vector3.one;
            _lens.gameObject.SetActive(true);
        }

        _fromPosition = _lens.position;
        _fromRotation = _lens.rotation;
        _fromScale = _lens.localScale;
        _toPosition = position;
        _toRotation = rotation;
        _toScale = scale;
        _elapsed = 0f;
        _moving = true;
        _hideWhenArrived = hideWhenArrived;
        enabled = true;
    }

    // 돌아올 시간을 재고, 돋보기가 움직이는 동안 자세를 보간하며, 할 일이 없으면 Update를 끈다
    private void Update()
    {
        if (_returnTimer > 0f)
        {
            _returnTimer -= Time.deltaTime;
            if (_returnTimer <= 0f) Hide();
        }

        if (_moving) StepLens();
        if (!_moving && _returnTimer <= 0f) enabled = false;
    }

    // 돋보기 자세를 한 프레임 보간하고 다 치웠으면 숨긴다
    private void StepLens()
    {
        _elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(_elapsed / _moveDuration);
        float eased = t * t * (3f - 2f * t);
        _lens.SetPositionAndRotation(
            Vector3.LerpUnclamped(_fromPosition, _toPosition, eased),
            Quaternion.Slerp(_fromRotation, _toRotation, eased));
        _lens.localScale = Vector3.LerpUnclamped(_fromScale, _toScale, eased);
        if (t < 1f) return;

        _moving = false;
        if (!_hideWhenArrived) return;

        _lens.gameObject.SetActive(false);
        _hideWhenArrived = false;
    }
}
