using UnityEngine;

public class BribeView : MonoBehaviour
{
    [SerializeField] private GameObject _propPrefab;
    [SerializeField] private ItemTableView _items;
    [SerializeField] private Transform _target;
    [SerializeField] private Vector3 _targetOffset = new Vector3(0f, 0.12f, 0f);
    [SerializeField] private float _flyDuration = 0.45f;
    [SerializeField] private float _arcHeight = 0.18f;
    [SerializeField] private float _spinDegrees = 540f;

    private Transform _prop;
    private GameManager _gm;
    private Vector3 _from;
    private Vector3 _to;
    private Quaternion _startRotation;
    private float _time;

    // 봉투 연출용 오브젝트를 만든다
    private void Awake()
    {
        _prop = ItemProp.Create(_propPrefab, transform);
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

    // 누가 쓰든 그 사람 슬롯에서 봉투가 딜러 쪽 카드 슈로 휙 날아가게 한다
    private void OnItemUsed(ActivePlayer seat, ItemType type)
    {
        if (type != ItemType.Bribe) return;

        Transform anchor = _items.AnchorOf(seat, ItemType.Bribe);
        _from = anchor.position;
        _startRotation = anchor.rotation;
        _to = _target.position + _targetOffset;
        _prop.SetPositionAndRotation(_from, _startRotation);
        _prop.localScale = Vector3.one;
        _prop.gameObject.SetActive(true);
        _time = 0f;
        enabled = true;
    }

    // 봉투를 포물선으로 날리며 돌리고 도착할 즈음 작아지며 사라지게 한다
    private void Update()
    {
        _time += Time.deltaTime;
        float t = Mathf.Clamp01(_time / _flyDuration);
        float eased = 1f - (1f - t) * (1f - t);
        Vector3 position = Vector3.LerpUnclamped(_from, _to, eased) + Vector3.up * (_arcHeight * 4f * t * (1f - t));
        _prop.SetPositionAndRotation(position, _startRotation * Quaternion.Euler(0f, _spinDegrees * eased, 0f));
        _prop.localScale = Vector3.one * (t < 0.75f ? 1f : Mathf.Lerp(1f, 0f, (t - 0.75f) / 0.25f));
        if (t < 1f) return;

        _prop.gameObject.SetActive(false);
        enabled = false;
    }
}
