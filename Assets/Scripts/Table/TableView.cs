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

    private CardPool _pool;
    private readonly List<CardView> _myCards = new List<CardView>(10);
    private readonly List<CardView> _opponentCards = new List<CardView>(10);
    private CardView _sharedCard;
    private GameManager _gm;
    private float _nextDealDelay;

    private static readonly Quaternion FaceDownTurn = Quaternion.Euler(0f, 0f, 180f);

    private static ActivePlayer LocalSeat => NetworkGameManager.Instance != null
        ? NetworkGameManager.Instance.LocalPlayer
        : ActivePlayer.Player1;

    // 카드 풀을 미리 채운다
    private void Awake()
    {
        _pool = new CardPool(transform, _cardMaterial, _cardSpriteDatabase, PrewarmCards);
    }

    // 게임 이벤트를 구독하고 지금 상태를 한 번 맞춘다
    private void Start()
    {
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

    // 마지막 카드가 바뀌면 그 카드를 거두고 덱에서 새로 나눠준다
    private void OnLastCardReplaced(ActivePlayer seat)
    {
        List<CardView> views = seat == LocalSeat ? _myCards : _opponentCards;
        if (views.Count > 0)
        {
            int last = views.Count - 1;
            _pool.Release(views[last]);
            views.RemoveAt(last);
        }
        Sync();
    }

    // 게임 상태와 테이블을 비교해 다른 부분만 연출과 함께 맞춘다
    private void Sync()
    {
        if (_gm == null) return;

        using (PerfMarkers.TableSync.Auto())
        {
            _nextDealDelay = 0f;
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

    // 풀에서 카드를 꺼내 덱 위에 엎어 두고 목표 자리로 날린다
    private CardView Deal(Card card, bool faceUp, Vector3 position, Quaternion rotation)
    {
        CardView view = _pool.Get();
        view.SetCard(card, faceUp);
        view.Snap(_deckTop.position, FaceRotation(_deckTop, faceUp: false));
        view.MoveTo(position, rotation, _dealDuration, _nextDealDelay, _dealArc);
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
