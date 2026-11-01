using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

public class ChipTableView : MonoBehaviour
{
    private const int MyStack = 0;
    private const int OpponentStack = 1;
    private const int MyBet = 2;
    private const int OpponentBet = 3;
    private const int Pot = 4;
    private const int PileCount = 5;
    private const int MaxInstancesPerDenomination = 256;

    private const int StackSize = 10;
    private const int StacksPerRow = 4;

    private static readonly int[] Denominations = { 1 };

    [SerializeField] private Mesh _chipMesh;
    [SerializeField] private Material[] _chipMaterials = new Material[3];
    [SerializeField] private Transform[] _pileAnchors = new Transform[PileCount];
    [SerializeField] private TextMeshPro[] _labels = new TextMeshPro[PileCount];
    [SerializeField] private float _chipDiameter = 0.04f;
    [SerializeField] private float _chipHeight = 0.0035f;
    [SerializeField] private float _columnSpacing = 0.045f;
    [SerializeField] private float _flightDuration = 0.45f;
    [SerializeField] private float _flightStagger = 0.05f;
    [SerializeField] private float _flightArc = 0.06f;

    private readonly int[] _shown = new int[PileCount];
    private readonly int[] _incoming = new int[PileCount];
    private readonly int[] _target = new int[PileCount];
    private readonly int[] _labelValues = { -1, -1, -1, -1, -1 };
    private readonly int[] _delta = new int[PileCount];
    private readonly List<Flight> _flights = new List<Flight>(32);

    private Matrix4x4[][] _matrices;
    private int[] _instanceCounts;
    private RenderParams[] _renderParams;
    private Vector3 _chipScale;
    private GameManager _gm;
    private bool _initialized;
    private int _preview;
    private bool _clearPreviewOnState;

    private struct Flight
    {
        public int To;
        public int Denomination;
        public Vector3 From;
        public Vector3 Destination;
        public float StartTime;
    }

    // 액면가마다 인스턴스 행렬 배열과 렌더 설정을 미리 만든다
    private void Awake()
    {
        _chipScale = new Vector3(_chipDiameter, _chipHeight * 0.5f, _chipDiameter);
        _matrices = new Matrix4x4[Denominations.Length][];
        _instanceCounts = new int[Denominations.Length];
        _renderParams = new RenderParams[Denominations.Length];

        Bounds bounds = new Bounds(transform.position, Vector3.one * 4f);
        for (int d = 0; d < Denominations.Length; d++)
        {
            _matrices[d] = new Matrix4x4[MaxInstancesPerDenomination];
            _renderParams[d] = new RenderParams(_chipMaterials[d])
            {
                shadowCastingMode = ShadowCastingMode.On,
                receiveShadows = true,
                worldBounds = bounds
            };
        }
    }

    // 상태 변경을 구독하고 지금 상태를 한 번 맞춘다
    private void Start()
    {
        _gm = GameManager.Instance;
        if (_gm == null) return;

        _gm.StateChanged += OnStateChanged;
        OnStateChanged();
    }

    // 구독을 해제한다
    private void OnDestroy()
    {
        if (_gm == null) return;
        _gm.StateChanged -= OnStateChanged;
        _gm = null;
    }

    // 새 값을 읽어 처음이면 바로 쌓고 이후에는 줄어든 더미에서 늘어난 더미로 칩을 날린다
    private void OnStateChanged()
    {
        if (_gm == null || _gm.GetPlayer1Cards() == null) return;

        if (_clearPreviewOnState)
        {
            _preview = 0;
            _clearPreviewOnState = false;
        }
        ReadTargets();
        UpdateLabels();

        if (!_initialized)
        {
            for (int i = 0; i < PileCount; i++) _shown[i] = _target[i];
            _initialized = true;
            return;
        }

        ScheduleTransfers();
    }

    // 로컬 좌석 기준으로 다섯 더미의 목표 칩 수를 읽는다
    private void ReadTargets()
    {
        bool hostSide = PlayerActions.LocalSeat == ActivePlayer.Player1;
        _target[MyStack] = hostSide ? _gm.Player1Chips : _gm.Player2Chips;
        _target[OpponentStack] = hostSide ? _gm.Player2Chips : _gm.Player1Chips;
        _target[MyBet] = hostSide ? _gm.Player1TotalBet : _gm.Player2TotalBet;
        _target[OpponentBet] = hostSide ? _gm.Player2TotalBet : _gm.Player1TotalBet;
        _target[Pot] = Mathf.Max(0, _gm.Pot - _gm.Player1TotalBet - _gm.Player2TotalBet);

        int preview = Mathf.Min(_preview, _target[MyStack]);
        _target[MyStack] -= preview;
        _target[MyBet] += preview;
    }

    // 확정 전 베팅 금액만큼 내 칩을 베팅 자리로 미리 옮겨 보인다(0이면 되돌린다)
    public void SetPreview(int amount)
    {
        _preview = Mathf.Max(0, amount);
        if (!_initialized || _gm == null) return;
        ReadTargets();
        UpdateLabels();
        ScheduleTransfers();
    }

    // 다음 상태 변경이 오면 미리 보기를 실제 값으로 갈아끼우도록 표시한다
    public void CommitPreview() => _clearPreviewOnState = true;

    // 내 칩 더미 기준점 위치를 구한다
    public Vector3 MyStackBase => _pileAnchors[MyStack].position;

    // 내 베팅 더미 기준점 위치를 구한다
    public Vector3 MyBetBase => _pileAnchors[MyBet].position;

    // 액면가 순번의 칩 값을 돌려준다
    public static int DenominationValue(int denominationIndex) => Denominations[denominationIndex];

    // 숫자가 바뀐 더미의 글자만 고친다
    private void UpdateLabels()
    {
        for (int i = 0; i < PileCount; i++)
        {
            TextMeshPro label = _labels[i];
            if (label == null) continue;

            int value = i == Pot ? _gm.Pot : _target[i];
            if (value == _labelValues[i]) continue;

            _labelValues[i] = value;
            label.gameObject.SetActive(value > 0 || i == MyStack || i == OpponentStack);
            label.SetText("{0}", value);
        }
    }

    // 줄어든 더미를 출발지로, 늘어난 더미를 도착지로 짝지어 칩 이동을 예약한다
    private void ScheduleTransfers()
    {
        for (int i = 0; i < PileCount; i++)
            _delta[i] = _target[i] - (_shown[i] + _incoming[i]);

        int launched = 0;
        for (int to = 0; to < PileCount; to++)
        {
            for (int from = 0; from < PileCount && _delta[to] > 0; from++)
            {
                if (_delta[from] >= 0) continue;

                int amount = Mathf.Min(_delta[to], Mathf.Min(-_delta[from], _shown[from]));
                if (amount <= 0) continue;

                launched = Launch(from, to, amount, launched);
                _delta[to] -= amount;
                _delta[from] += amount;
            }
        }

        for (int i = 0; i < PileCount; i++)
            _shown[i] = Mathf.Max(0, _shown[i] + _delta[i]);
    }

    // 금액을 액면가로 나눠 칩마다 비행을 하나씩 추가한다
    private int Launch(int from, int to, int amount, int launched)
    {
        _shown[from] -= amount;
        int remaining = amount;

        for (int d = 0; d < Denominations.Length; d++)
        {
            while (remaining >= Denominations[d])
            {
                remaining -= Denominations[d];
                _flights.Add(new Flight
                {
                    To = to,
                    Denomination = d,
                    From = StackTop(from, d, _shown[from]),
                    Destination = StackTop(to, d, _shown[to] + _incoming[to]),
                    StartTime = Time.time + launched * _flightStagger
                });
                _incoming[to] += Denominations[d];
                launched++;
            }
        }
        return launched;
    }

    // 도착한 비행을 더미에 더하고 모든 칩을 액면가별로 한 번씩 인스턴싱해 그린다
    private void LateUpdate()
    {
        if (!_initialized) return;

        float now = Time.time;
        for (int i = _flights.Count - 1; i >= 0; i--)
        {
            Flight flight = _flights[i];
            if (now - flight.StartTime < _flightDuration) continue;

            int value = Denominations[flight.Denomination];
            _shown[flight.To] += value;
            _incoming[flight.To] -= value;
            _flights[i] = _flights[_flights.Count - 1];
            _flights.RemoveAt(_flights.Count - 1);
        }

        for (int d = 0; d < Denominations.Length; d++) _instanceCounts[d] = 0;

        for (int pile = 0; pile < PileCount; pile++)
            AddPile(pile, _shown[pile]);

        for (int i = 0; i < _flights.Count; i++)
        {
            Flight flight = _flights[i];
            float t = Mathf.Clamp01((now - flight.StartTime) / _flightDuration);
            float eased = t * t * (3f - 2f * t);
            Vector3 position = Vector3.LerpUnclamped(flight.From, flight.Destination, eased);
            position.y += Mathf.Sin(t * Mathf.PI) * _flightArc;
            AddInstance(flight.Denomination, position, _pileAnchors[flight.To].rotation);
        }

        for (int d = 0; d < Denominations.Length; d++)
        {
            if (_instanceCounts[d] > 0)
                Graphics.RenderMeshInstanced(_renderParams[d], _chipMesh, 0, _matrices[d], _instanceCounts[d]);
        }
    }

    // 한 더미의 칩을 액면가별 기둥으로 나눠 쌓는다
    private void AddPile(int pile, int amount)
    {
        Transform anchor = _pileAnchors[pile];
        for (int d = 0; d < Denominations.Length; d++)
        {
            int count = ColumnCount(amount, d);
            for (int k = 0; k < count; k++)
                AddInstance(d, ColumnPoint(anchor, d, k), anchor.rotation);
        }
    }

    // 금액을 큰 액면가부터 나눴을 때 그 액면가 칩이 몇 개인지 구한다
    private static int ColumnCount(int amount, int denominationIndex)
    {
        int remaining = amount;
        for (int d = 0; d < denominationIndex; d++) remaining %= Denominations[d];
        return remaining / Denominations[denominationIndex];
    }

    // 더미에서 k번째 칩의 중심 위치를 구한다(10개씩 한 기둥, 기둥은 가로 4개씩 줄을 지어 플레이어 쪽으로 늘어난다)
    private Vector3 ColumnPoint(Transform anchor, int denominationIndex, int k)
    {
        int stack = k / StackSize;
        int level = k % StackSize;
        float x = (stack % StacksPerRow - (StacksPerRow - 1) * 0.5f) * _columnSpacing;
        float z = -(stack / StacksPerRow) * _columnSpacing;
        return anchor.TransformPoint(new Vector3(x, _chipHeight * (level + 0.5f), z));
    }

    // 그 금액이 쌓였을 때 액면가 기둥 맨 위에 놓일 칩의 위치를 구한다
    private Vector3 StackTop(int pile, int denominationIndex, int amount) =>
        ColumnPoint(_pileAnchors[pile], denominationIndex, ColumnCount(amount, denominationIndex));

    // 인스턴스 행렬을 하나 추가한다
    private void AddInstance(int denominationIndex, Vector3 position, Quaternion rotation)
    {
        int index = _instanceCounts[denominationIndex];
        if (index >= MaxInstancesPerDenomination) return;

        _matrices[denominationIndex][index] = Matrix4x4.TRS(position, rotation, _chipScale);
        _instanceCounts[denominationIndex] = index + 1;
    }
}
