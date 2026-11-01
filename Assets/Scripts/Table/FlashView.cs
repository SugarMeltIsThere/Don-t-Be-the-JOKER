using UnityEngine;
using UnityEngine.UI;

public class FlashView : MonoBehaviour
{
    private const float FlashIn = 0.08f;

    [SerializeField] private GameObject _propPrefab;
    [SerializeField] private Material _glareMaterial;
    [SerializeField] private ItemTableView _items;
    [SerializeField] private Transform _opponentHead;
    [SerializeField] private Vector3 _opponentAimPoint = new Vector3(0.12f, 1.02f, 0.62f);
    [SerializeField] private Vector3 _holdOffset = new Vector3(0.09f, -0.1f, 0.3f);
    [SerializeField] private float _raiseDuration = 0.3f;
    [SerializeField] private float _shineDuration = 0.9f;
    [SerializeField] private float _blindHold = 0.35f;
    [SerializeField] private float _blindFade = 2.4f;
    [SerializeField] private float _beamIntensity = 14f;
    [SerializeField] private Color _blindColor = new Color(1f, 0.97f, 0.9f);

    private Transform _prop;
    private Light _beam;
    private GameObject _glare;
    private Canvas _overlayCanvas;
    private Image _overlay;
    private Camera _camera;
    private GameManager _gm;
    private float _time;
    private bool _victim;
    private Vector3 _restPosition;
    private Quaternion _restRotation;
    private Vector3 _aimPosition;
    private Quaternion _aimRotation;

    private float Total => _raiseDuration * 2f + _shineDuration;

    // 손전등 연출용 오브젝트, 빛줄기, 렌즈 눈부심, 화면을 덮는 흰 막을 만든다
    private void Awake()
    {
        _camera = Camera.main;
        _prop = ItemProp.Create(_propPrefab, transform);

        GameObject beam = new GameObject("Beam");
        beam.transform.SetParent(_prop, false);
        beam.transform.localPosition = new Vector3(0f, 0.016f, 0.05f);
        _beam = beam.AddComponent<Light>();
        _beam.type = LightType.Spot;
        _beam.spotAngle = 26f;
        _beam.innerSpotAngle = 12f;
        _beam.range = 4f;
        _beam.color = new Color(1f, 0.96f, 0.86f);
        _beam.shadows = LightShadows.None;
        _beam.enabled = false;

        _glare = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(_glare.GetComponent<Collider>());
        _glare.name = "Glare";
        _glare.transform.SetParent(_prop, false);
        _glare.transform.localPosition = new Vector3(0f, 0.016f, 0.046f);
        _glare.transform.localScale = new Vector3(0.028f, 0.028f, 0.006f);
        MeshRenderer glareRenderer = _glare.GetComponent<MeshRenderer>();
        glareRenderer.sharedMaterial = _glareMaterial;
        glareRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _glare.SetActive(false);

        GameObject canvasGo = new GameObject("FlashOverlay");
        canvasGo.transform.SetParent(transform, false);
        _overlayCanvas = canvasGo.AddComponent<Canvas>();
        _overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _overlayCanvas.sortingOrder = 500;
        GameObject imageGo = new GameObject("White");
        imageGo.transform.SetParent(canvasGo.transform, false);
        _overlay = imageGo.AddComponent<Image>();
        _overlay.raycastTarget = false;
        RectTransform rect = _overlay.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        SetBlind(0f);
    }

    // 아이템 사용 이벤트를 구독하고 연출이 시작될 때까지 Update를 끈다
    private void Start()
    {
        enabled = false;
        _gm = GameManager.Instance;
        if (_gm != null) _gm.ItemUsed += OnItemUsed;
    }

    // 구독을 해제한다
    private void OnDestroy()
    {
        if (_gm != null) _gm.ItemUsed -= OnItemUsed;
    }

    // 내가 쓰면 손전등을 들어 상대 얼굴에 비추고, 상대가 쓰면 그 손전등이 나를 비추며 화면이 하얗게 된다
    private void OnItemUsed(ActivePlayer seat, ItemType type)
    {
        if (type != ItemType.Flash) return;

        _victim = seat != PlayerActions.LocalSeat;
        Transform anchor = _items.AnchorOf(seat, ItemType.Flash);
        _restPosition = anchor.position;
        _restRotation = anchor.rotation;

        if (_victim)
        {
            _aimPosition = _opponentAimPoint;
            _aimRotation = Quaternion.LookRotation(_camera.transform.position - _aimPosition);
        }
        else
        {
            Transform cam = _camera.transform;
            _aimPosition = cam.position + cam.right * _holdOffset.x + cam.up * _holdOffset.y + cam.forward * _holdOffset.z;
            _aimRotation = Quaternion.LookRotation(_opponentHead.position - _aimPosition);
        }

        _prop.SetPositionAndRotation(_restPosition, _restRotation);
        _prop.localScale = Vector3.zero;
        _prop.gameObject.SetActive(true);
        _time = 0f;
        enabled = true;
    }

    // 들어 올리기, 비추기, 내려놓기 순서로 손전등을 움직이고 당한 쪽 화면의 흰 막을 걷어낸다
    private void Update()
    {
        _time += Time.deltaTime;
        float shineStart = _raiseDuration;
        float shineEnd = _raiseDuration + _shineDuration;

        if (_time < shineStart) Pose(Ease(_time / _raiseDuration));
        else if (_time < shineEnd) Pose(1f);
        else Pose(1f - Ease((_time - shineEnd) / _raiseDuration));

        bool shining = _time >= shineStart && _time < shineEnd;
        _beam.enabled = shining;
        _glare.SetActive(shining);
        if (shining)
        {
            float flicker = 0.92f + 0.08f * Mathf.PerlinNoise(_time * 30f, 0.5f);
            _beam.intensity = _beamIntensity * flicker * Mathf.Clamp01((_time - shineStart) / 0.05f);
            _prop.rotation = _aimRotation * Quaternion.Euler(Mathf.Sin(_time * 23f) * 0.6f, Mathf.Sin(_time * 17f) * 0.6f, 0f);
        }

        if (_victim) SetBlind(BlindAlpha(_time - shineStart));

        bool blindDone = !_victim || _time > shineStart + _blindHold + _blindFade;
        if (_time < Total || !blindDone) return;

        _prop.gameObject.SetActive(false);
        SetBlind(0f);
        enabled = false;
    }

    // 쉬는 자리와 겨누는 자세 사이를 비율로 보간하고 처음과 끝에서 크기를 키우고 줄인다
    private void Pose(float t)
    {
        _prop.SetPositionAndRotation(Vector3.LerpUnclamped(_restPosition, _aimPosition, t), Quaternion.Slerp(_restRotation, _aimRotation, t));
        _prop.localScale = Vector3.one * Mathf.Clamp01(t * 4f);
    }

    // 빛을 받은 순간 빠르게 하얘졌다가 잠깐 머문 뒤 길게 풀리는 흰 막의 불투명도를 구한다
    private float BlindAlpha(float sinceShine)
    {
        if (sinceShine < 0f) return 0f;
        if (sinceShine < FlashIn) return sinceShine / FlashIn;
        if (sinceShine < _blindHold) return 1f;
        float x = Mathf.Clamp01((sinceShine - _blindHold) / _blindFade);
        return (1f - x) * (1f - x);
    }

    // 흰 막의 불투명도를 정하고 보이지 않을 때는 캔버스를 끈다
    private void SetBlind(float alpha)
    {
        _overlay.color = new Color(_blindColor.r, _blindColor.g, _blindColor.b, alpha);
        _overlayCanvas.enabled = alpha > 0.001f;
    }

    // 부드럽게 시작하고 끝나는 보간 비율을 구한다
    private static float Ease(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }
}
