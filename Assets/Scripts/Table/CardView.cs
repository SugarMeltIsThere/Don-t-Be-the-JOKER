using UnityEngine;
using UnityEngine.Rendering;

public class CardView : MonoBehaviour
{
    public const float Width = 0.063f;
    public const float Height = 0.084f;
    private const float BackOffset = 0.0002f;
    private const float PositionTolerance = 0.0001f;
    private const float AngleTolerance = 0.1f;

    private Mesh _mesh;
    private readonly Vector2[] _uvs = new Vector2[8];
    private CardSpriteDatabase _db;

    private Vector3 _fromPosition;
    private Quaternion _fromRotation;
    private float _elapsed;
    private float _duration;
    private float _delay;
    private float _arc;

    public Card Card { get; private set; }
    public bool FaceUp { get; private set; }
    public Vector3 TargetPosition { get; private set; }
    public Quaternion TargetRotation { get; private set; }

    // 메시와 렌더러를 붙인 카드 오브젝트를 만든다
    public static CardView Create(Transform parent, Material material, CardSpriteDatabase db)
    {
        GameObject go = new GameObject(nameof(CardView));
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>();

        MeshRenderer meshRenderer = go.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = material;
        meshRenderer.shadowCastingMode = ShadowCastingMode.Off;

        CardView view = go.AddComponent<CardView>();
        view.Init(db);
        return view;
    }

    // 앞면과 뒷면 두 장의 사각형을 하나의 메시로 만든다
    private void Init(CardSpriteDatabase db)
    {
        _db = db;

        float w = Width * 0.5f;
        float h = Height * 0.5f;
        _mesh = new Mesh { name = "CardMesh" };
        _mesh.vertices = new[]
        {
            new Vector3(-w, 0f, -h), new Vector3(w, 0f, -h), new Vector3(-w, 0f, h), new Vector3(w, 0f, h),
            new Vector3(-w, -BackOffset, -h), new Vector3(w, -BackOffset, -h), new Vector3(-w, -BackOffset, h), new Vector3(w, -BackOffset, h)
        };
        _mesh.normals = new[]
        {
            Vector3.up, Vector3.up, Vector3.up, Vector3.up,
            Vector3.down, Vector3.down, Vector3.down, Vector3.down
        };
        _mesh.triangles = new[] { 0, 2, 3, 0, 3, 1, 4, 7, 6, 4, 5, 7 };

        WriteUvs(4, _db.CardBackSprite, isBack: true);
        WriteUvs(0, _db.CardBackSprite, isBack: false);
        _mesh.uv = _uvs;
        _mesh.RecalculateBounds();

        GetComponent<MeshFilter>().sharedMesh = _mesh;
        enabled = false;
    }

    // 메시를 해제한다
    private void OnDestroy()
    {
        if (_mesh != null) Destroy(_mesh);
    }

    // 스프라이트가 차지하는 아틀라스 영역을 네 꼭짓점의 UV로 쓴다
    private void WriteUvs(int start, Sprite sprite, bool isBack)
    {
        if (sprite == null) return;

        Rect rect = sprite.textureRect;
        Texture texture = sprite.texture;
        float u0 = rect.xMin / texture.width;
        float u1 = rect.xMax / texture.width;
        float v0 = rect.yMin / texture.height;
        float v1 = rect.yMax / texture.height;

        if (isBack)
        {
            _uvs[start] = new Vector2(u1, v0);
            _uvs[start + 1] = new Vector2(u0, v0);
            _uvs[start + 2] = new Vector2(u1, v1);
            _uvs[start + 3] = new Vector2(u0, v1);
        }
        else
        {
            _uvs[start] = new Vector2(u0, v0);
            _uvs[start + 1] = new Vector2(u1, v0);
            _uvs[start + 2] = new Vector2(u0, v1);
            _uvs[start + 3] = new Vector2(u1, v1);
        }
    }

    // 표시할 카드와 앞뒷면 상태를 정하고 카드가 바뀌었으면 앞면 UV만 다시 쓴다
    public void SetCard(Card card, bool faceUp)
    {
        FaceUp = faceUp;
        if (card == null || IsSame(Card, card)) return;

        Card = card;
        Sprite sprite = card.Rank == Rank.Joker
            ? (_db.JokerSprite != null ? _db.JokerSprite : _db.CardBackSprite)
            : _db.GetSprite(card);
        WriteUvs(0, sprite, isBack: false);
        _mesh.uv = _uvs;
    }

    // 두 카드가 같은 랭크와 무늬인지 확인한다
    private static bool IsSame(Card a, Card b) =>
        a != null && b != null && a.Rank == b.Rank && a.Suit == b.Suit;

    // 연출 없이 그 자리로 옮긴다
    public void Snap(Vector3 position, Quaternion rotation)
    {
        transform.SetPositionAndRotation(position, rotation);
        TargetPosition = position;
        TargetRotation = rotation;
        enabled = false;
    }

    // 지금 위치에서 목표 자세로 움직이기 시작한다
    public void MoveTo(Vector3 position, Quaternion rotation, float duration, float delay = 0f, float arc = 0f)
    {
        if (duration <= 0f)
        {
            Snap(position, rotation);
            return;
        }

        _fromPosition = transform.position;
        _fromRotation = transform.rotation;
        TargetPosition = position;
        TargetRotation = rotation;
        _duration = duration;
        _delay = delay;
        _arc = arc;
        _elapsed = 0f;
        enabled = true;
    }

    // 이미 이 자세를 목표로 하고 있는지 확인한다
    public bool IsTargeting(Vector3 position, Quaternion rotation) =>
        (TargetPosition - position).sqrMagnitude < PositionTolerance * PositionTolerance &&
        Quaternion.Angle(TargetRotation, rotation) < AngleTolerance;

    // 움직임을 멈춘다
    public void Stop() => enabled = false;

    // 움직이는 동안에만 한 프레임씩 자세를 보간한다
    private void Update()
    {
        if (_delay > 0f)
        {
            _delay -= Time.deltaTime;
            return;
        }

        _elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(_elapsed / _duration);
        float eased = t * t * (3f - 2f * t);

        Vector3 position = Vector3.LerpUnclamped(_fromPosition, TargetPosition, eased);
        position.y += Mathf.Sin(t * Mathf.PI) * _arc;
        transform.SetPositionAndRotation(position, Quaternion.Slerp(_fromRotation, TargetRotation, eased));

        if (t >= 1f) enabled = false;
    }
}
