using System.Collections.Generic;
using UnityEngine;

public class TableView : MonoBehaviour
{
    private const int PrewarmCards = 24;

    [Header("Cards")]
    [SerializeField] private CardSpriteDatabase _cardSpriteDatabase;
    [SerializeField] private Material _cardMaterial;

    [Header("Anchors")]
    [SerializeField] private Transform _mySide;
    [SerializeField] private Transform _opponentSide;
    [SerializeField] private Transform _sharedSlot;
    [SerializeField] private Transform _deckTop;

    [Header("Motion")]
    [SerializeField] private float _cardSpacing = 0.075f;
    [SerializeField] private float _dealDuration = 0.35f;
    [SerializeField] private float _dealStagger = 0.12f;
    [SerializeField] private float _dealArc = 0.05f;
    [SerializeField] private float _flipDuration = 0.25f;
    [SerializeField] private float _slideDuration = 0.45f;
    [SerializeField] private float _slideLift = 0.004f;
    [SerializeField] private float _landFlipArc = 0.036f;
    [SerializeField] private float _replaceReturnDelay = 0.35f;
    [SerializeField] private float _replaceDealDelay = 0.8f;

    private CardPool _pool;
    private readonly List<CardView> _myCards = new List<CardView>(10);
    private readonly List<CardView> _opponentCards = new List<CardView>(10);
    private CardView _sharedCard;
    private GameManager _gm;
    private float _nextDealDelay;
    private float _pendingDealDelay;
    private readonly List<CardView> _returning = new List<CardView>(4);
    private readonly List<float> _returnTimes = new List<float>(4);

    private static readonly Quaternion FaceDownTurn = Quaternion.Euler(0f, 0f, 180f);

    private static ActivePlayer LocalSeat => PlayerActions.LocalSeat;

    public IReadOnlyList<CardView> MyCards => _myCards;

    // 카드 풀을 미리 채운다
    private void Awake()
    {
        _pool = new CardPool(transform, _cardMaterial, _cardSpriteDatabase, PrewarmCards);
    }

    // 슈로 돌아가는 카드가 생길 때까지 Update를 끄고, 게임 이벤트를 구독하고 지금 상태를 한 번 맞춘다
    private void Start()
    {
        enabled = false;
        _gm = GameManager.Instance;
        if (_gm == null) return;

        _gm.StateChanged += Sync;
        _gm.RoundStarted += OnRoundStarted;
        _gm.CardDealt += OnCardDealt;
        _gm.LastCardReplaced += OnLastCardReplaced;
        _gm.RoundRevealed += Sync;
        Sync();
    }

    // 구독을 해제한다
    private void OnDestroy()
    {
        if (_gm == null) return;

        _gm.StateChanged -= Sync;
        _gm.RoundStarted -= OnRoundStarted;
        _gm.CardDealt -= OnCardDealt;
        _gm.LastCardReplaced -= OnLastCardReplaced;
        _gm.RoundRevealed -= Sync;
        _gm = null;
    }

    // 새 라운드가 시작되면 테이블을 비운다
    private void OnRoundStarted(int round)
    {
        ReleaseAll(_returning);
        _returnTimes.Clear();
        ReleaseAll(_myCards);
        ReleaseAll(_opponentCards);
        if (_sharedCard != null)
        {
            _pool.Release(_sharedCard);
            _sharedCard = null;
        }
        Sync();
    }

    // 카드가 나뉘면 테이블을 맞춘다
    private void OnCardDealt(ActivePlayer seat, int index) => Sync();

    // 마지막 카드가 바뀌면 봉투가 닿을 즈음 그 카드를 엎어 슈로 돌려보내고, 조금 뒤 슈에서 새 카드를 나눠준다
    private void OnLastCardReplaced(ActivePlayer seat)
    {
        List<CardView> views = seat == LocalSeat ? _myCards : _opponentCards;
        if (views.Count > 0)
        {
            int last = views.Count - 1;
            CardView old = views[last];
            views.RemoveAt(last);
            old.MoveTo(_deckTop.position, FaceRotation(_deckTop, faceUp: false), _dealDuration, _replaceReturnDelay, _dealArc);
            _returning.Add(old);
            _returnTimes.Add(Time.time + _replaceReturnDelay + _dealDuration);
            enabled = true;
        }
        _pendingDealDelay = _replaceDealDelay;
        Sync();
    }

    // 슈에 다 돌아간 카드를 풀로 돌려보낸다
    private void Update()
    {
        for (int i = _returning.Count - 1; i >= 0; i--)
        {
            if (Time.time < _returnTimes[i]) continue;
            _pool.Release(_returning[i]);
            _returning.RemoveAt(i);
            _returnTimes.RemoveAt(i);
        }
        if (_returning.Count == 0) enabled = false;
    }

    // 게임 상태와 테이블을 비교해 다른 부분만 연출과 함께 맞춘다
    private void Sync()
    {
        if (_gm == null) return;

        using (PerfMarkers.TableSync.Auto())
        {
            _nextDealDelay = _pendingDealDelay;
            _pendingDealDelay = 0f;
            ActivePlayer local = LocalSeat;
            ActivePlayer opponent = local == ActivePlayer.Player1 ? ActivePlayer.Player2 : ActivePlayer.Player1;

            SyncShared();
            SyncSide(_myCards, _mySide, Cards(local), isMine: true);
            SyncSide(_opponentCards, _opponentSide, Cards(opponent), isMine: false);
        }
    }

    // 좌석의 손패를 가져온다
    private IReadOnlyList<CardSlot> Cards(ActivePlayer seat) =>
        seat == ActivePlayer.Player1 ? _gm.GetPlayer1Cards() : _gm.GetPlayer2Cards();

    // 가운데 공유 카드를 맞춘다
    private void SyncShared()
    {
        Card card = _gm.SharedCard;
        if (card == null) return;

        Quaternion rotation = FaceRotation(_sharedSlot, faceUp: true);
        if (_sharedCard == null)
        {
            _sharedCard = Deal(card, faceUp: true, _sharedSlot.position, rotation);
            return;
        }

        _sharedCard.SetCard(card, faceUp: true);
        if (!_sharedCard.IsTargeting(_sharedSlot.position, rotation))
            _sharedCard.MoveTo(_sharedSlot.position, rotation, _dealDuration);
    }

    // 한쪽 손패를 맞추고 새 카드는 덱에서 날아오게 하고 앞뒷면이 바뀐 카드는 뒤집는다
    private void SyncSide(List<CardView> views, Transform side, IReadOnlyList<CardSlot> slots, bool isMine)
    {
        int count = slots != null ? slots.Count : 0;

        while (views.Count > count)
        {
            int last = views.Count - 1;
            _pool.Release(views[last]);
            views.RemoveAt(last);
        }

        for (int i = 0; i < count; i++)
        {
            CardSlot slot = slots[i];
            bool faceUp = isMine || !slot.IsHidden;
            Vector3 position = SlotPosition(side, i, count);
            Quaternion rotation = FaceRotation(side, faceUp);

            if (i >= views.Count)
            {
                views.Add(Deal(slot.Card, faceUp, position, rotation));
                continue;
            }

            CardView view = views[i];
            bool flipping = view.FaceUp != faceUp;
            view.SetCard(slot.Card, faceUp);

            if (!view.IsTargeting(position, rotation))
                view.MoveTo(position, rotation, flipping ? _flipDuration : _dealDuration, 0f, flipping ? _dealArc : 0f);
        }
    }

    // 풀에서 카드를 꺼내 덱 위에 엎어 두고 테이블 위로 엎은 채 미끄러뜨린 뒤, 앞면이어야 하면 도착한 자리에서 뒤집는다
    private CardView Deal(Card card, bool faceUp, Vector3 position, Quaternion rotation)
    {
        CardView view = _pool.Get();
        view.SetCard(card, faceUp);
        view.Snap(_deckTop.position, FaceRotation(_deckTop, faceUp: false));
        Quaternion slideRotation = faceUp ? rotation * FaceDownTurn : rotation;
        view.MoveTo(position, slideRotation, _slideDuration, _nextDealDelay, _slideLift, slide: true);
        if (faceUp) view.QueueFlip(rotation, _flipDuration, _landFlipArc);
        _nextDealDelay += _dealStagger;
        return view;
    }

    // 목록의 카드를 모두 풀로 돌려보낸다
    private void ReleaseAll(List<CardView> views)
    {
        for (int i = 0; i < views.Count; i++)
            _pool.Release(views[i]);
        views.Clear();
    }

    // 손패 안에서 i번째 카드가 놓일 자리를 가운데 정렬로 구한다
    private Vector3 SlotPosition(Transform side, int index, int count) =>
        side.TransformPoint(new Vector3((index - (count - 1) * 0.5f) * _cardSpacing, 0f, 0f));

    // 앞면이면 기준 방향 그대로, 뒷면이면 세로축으로 뒤집은 방향을 구한다
    private static Quaternion FaceRotation(Transform anchor, bool faceUp) =>
        faceUp ? anchor.rotation : anchor.rotation * FaceDownTurn;
}
