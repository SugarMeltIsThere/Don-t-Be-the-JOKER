using System;
using UnityEngine;

public class OpponentFigure : MonoBehaviour
{
    [Serializable]
    private class Body
    {
        public Animator Animator;
        [NonSerialized] public Transform Spine, Chest, Neck, Head;
        [NonSerialized] public Transform[] Arms;
        [NonSerialized] public Quaternion[] BindRotations;
        [NonSerialized] public Transform[] Bones;
    }

    public static OpponentFigure Instance { get; private set; }

    [SerializeField] private Body _male;
    [SerializeField] private Body _female;
    [SerializeField] private Transform _tableCenter;

    [Header("Pose")]
    [SerializeField] private float _lean = 14f;
    [SerializeField] private float _breathDegrees = 1.4f;
    [SerializeField] private float _breathRate = 1.5f;

    [Header("Gaze")]
    [SerializeField] private float _yawRange = 45f;
    [SerializeField] private float _restPitch = 22f;
    [SerializeField] private float _lookUpPitch = 30f;
    [SerializeField] private float _lookDownExtra = 28f;
    [SerializeField] private float _gazeSpeed = 5f;

    private Body _active;
    private GameManager _gm;
    private Vector2 _gazeTarget;
    private bool _downTarget;
    private Vector2 _gaze;
    private float _down;
    private float _autoUntil;

    // 두 몸의 뼈와 처음 자세를 기억하고 둘 다 숨긴 뒤 등록한다
    private void Awake()
    {
        Instance = this;
        Prepare(_male);
        Prepare(_female);
        _male.Animator.gameObject.SetActive(false);
        _female.Animator.gameObject.SetActive(false);
    }

    // 상대 캐릭터가 정해지면 그에 맞는 몸과 장식을 보이게 한다
    private void Start()
    {
        _gm = GameManager.Instance;
        if (_gm == null) return;
        _gm.CharactersChanged += Refresh;
        Refresh();
    }

    // 구독과 등록을 해제한다
    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (_gm != null) _gm.CharactersChanged -= Refresh;
    }

    // 상대가 보는 방향(화면 커서 위치 -1~1)과 자기 패를 내려다보는지를 받는다
    public void SetGaze(Vector2 pointer, bool lookingDown)
    {
        _gazeTarget = pointer;
        _downTarget = lookingDown;
        _autoUntil = Time.time + 2f;
    }

    // 상대 좌석의 캐릭터에 맞춰 퀸이면 여자 몸, 아니면 남자 몸을 켠다
    private void Refresh()
    {
        CharacterType? type = PlayerActions.OpponentSeat == ActivePlayer.Player1 ? _gm.Player1Character : _gm.Player2Character;
        if (type == null) return;
        _active = type == CharacterType.Queen ? _female : _male;
        _male.Animator.gameObject.SetActive(_active == _male);
        _female.Animator.gameObject.SetActive(_active == _female);
    }

    // 사람형 뼈대에서 쓰는 뼈를 찾고 처음 회전을 저장한다
    private static void Prepare(Body body)
    {
        Animator a = body.Animator;
        body.Spine = a.GetBoneTransform(HumanBodyBones.Spine);
        body.Head = a.GetBoneTransform(HumanBodyBones.Head);
        body.Chest = a.GetBoneTransform(HumanBodyBones.Chest) ?? body.Spine;
        body.Neck = a.GetBoneTransform(HumanBodyBones.Neck) ?? body.Head;
        body.Arms = new[]
        {
            a.GetBoneTransform(HumanBodyBones.LeftUpperArm), a.GetBoneTransform(HumanBodyBones.LeftLowerArm), a.GetBoneTransform(HumanBodyBones.LeftHand),
            a.GetBoneTransform(HumanBodyBones.RightUpperArm), a.GetBoneTransform(HumanBodyBones.RightLowerArm), a.GetBoneTransform(HumanBodyBones.RightHand)
        };
        body.Bones = new[] { body.Spine, body.Chest, body.Neck, body.Head, body.Arms[0], body.Arms[1], body.Arms[3], body.Arms[4] };
        body.BindRotations = new Quaternion[body.Bones.Length];
        for (int i = 0; i < body.Bones.Length; i++) body.BindRotations[i] = body.Bones[i].localRotation;
        a.enabled = false;
    }

    // 시선을 부드럽게 따라가고, 상대가 보내는 시선이 없으면(AI 연습 등) 게임 상황에 맞춰 스스로 둘러본다
    private void Update()
    {
        if (Time.time > _autoUntil) AutoGaze();
        float blend = 1f - Mathf.Exp(-_gazeSpeed * Time.deltaTime);
        _gaze = Vector2.Lerp(_gaze, _gazeTarget, blend);
        _down = Mathf.Lerp(_down, _downTarget ? 1f : 0f, blend);
    }

    // AI 상대의 시선: 자기 차례에 카드를 받을 땐 덱을, 생각할 땐 자기 패를, 베팅 땐 나를, 그 밖엔 내 패와 나를 번갈아 본다
    private void AutoGaze()
    {
        if (_gm == null) return;
        ActivePlayer me = PlayerActions.OpponentSeat;
        bool myTurn = _gm.CurrentPlayer == me;
        float t = Time.time;
        switch (_gm.CurrentPhase)
        {
            case GamePhase.Draw when myTurn:
                _gazeTarget = Mathf.Repeat(t, 3f) < 1.6f ? new Vector2(0.75f, -0.4f) : Vector2.zero;
                _downTarget = Mathf.Repeat(t, 3f) >= 1.6f;
                break;
            case GamePhase.Betting:
            case GamePhase.RoundEnd:
                _gazeTarget = new Vector2(Mathf.Sin(t * 0.7f) * 0.15f, 0.85f);
                _downTarget = false;
                break;
            case GamePhase.AceSelection:
                _gazeTarget = Vector2.zero;
                _downTarget = true;
                break;
            default:
                _gazeTarget = Mathf.Repeat(t, 5f) < 3f ? new Vector2(Mathf.Sin(t * 0.5f) * 0.3f, -0.2f) : new Vector2(0f, 0.8f);
                _downTarget = false;
                break;
        }
    }

    // 매 프레임 처음 자세로 되돌린 뒤 앞으로 기댄 몸, 숨쉬기, 테이블에 올린 팔, 시선 방향 고개를 입힌다
    private void LateUpdate()
    {
        if (_active == null) return;
        Body b = _active;
        for (int i = 0; i < b.Bones.Length; i++) b.Bones[i].localRotation = b.BindRotations[i];

        Vector3 front = _tableCenter.position - b.Spine.position;
        front.y = 0f;
        front.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, front);

        float breath = Mathf.Sin(Time.time * _breathRate * Mathf.PI * 2f * 0.25f) * _breathDegrees;
        b.Spine.rotation = Quaternion.AngleAxis(_lean * 0.6f, right) * b.Spine.rotation;
        b.Chest.rotation = Quaternion.AngleAxis(_lean * 0.4f + breath, right) * b.Chest.rotation;

        PoseArm(b.Arms[0], b.Arms[1], b.Arms[2], front, right, -1f);
        PoseArm(b.Arms[3], b.Arms[4], b.Arms[5], front, right, 1f);

        float yaw = -_gaze.x * _yawRange;
        float pitch = _restPitch - (_gaze.y > 0f ? _gaze.y * _lookUpPitch : _gaze.y * _restPitch * 0.5f) + _down * _lookDownExtra;
        Quaternion look = Quaternion.AngleAxis(yaw, Vector3.up) * Quaternion.AngleAxis(pitch - _lean, right);
        b.Neck.rotation = Quaternion.Slerp(Quaternion.identity, look, 0.4f) * b.Neck.rotation;
        b.Head.rotation = Quaternion.Slerp(Quaternion.identity, look, 0.6f) * b.Head.rotation;
    }

    // 위팔은 아래 앞쪽으로, 아래팔은 테이블 가장자리에서 두 손이 몸 앞에 모이도록 안쪽으로 돌린다(side: 몸 기준 오른쪽 +1, 왼쪽 -1)
    private static void PoseArm(Transform upper, Transform lower, Transform hand, Vector3 front, Vector3 right, float side)
    {
        Vector3 upperGoal = (Vector3.down * 0.9f + front * 0.2f + right * side * 0.22f).normalized;
        Aim(upper, lower.position, upperGoal);
        Vector3 lowerGoal = (front * 0.7f - right * side * 0.6f + Vector3.up * 0.06f).normalized;
        Aim(lower, hand.position, lowerGoal);
    }

    // 뼈가 자식을 향하는 방향이 목표 방향이 되도록 돌린다
    private static void Aim(Transform bone, Vector3 childPosition, Vector3 goal)
    {
        Vector3 current = childPosition - bone.position;
        bone.rotation = Quaternion.FromToRotation(current, goal) * bone.rotation;
    }
}
