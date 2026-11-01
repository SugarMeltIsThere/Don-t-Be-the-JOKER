using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class TableControls : MonoBehaviour
{
    [Header("Betting")]
    [SerializeField] private TablePlaque _call;
    [SerializeField] private TablePlaque _raise;
    [SerializeField] private TablePlaque _die;
    [SerializeField] private ChipColumnButton[] _chipColumns = new ChipColumnButton[3];
    [SerializeField] private TextMeshPro _raiseAmount;
    [SerializeField] private ChipTableView _chips;

    [Header("Aces")]
    [SerializeField] private TablePlaque _confirmAces;
    [SerializeField] private AceToggle _aceTemplate;
    [SerializeField] private TableView _table;

    [Header("Info")]
    [SerializeField] private TextMeshPro _myTotal;
    [SerializeField] private TextMeshPro _opponentTotal;
    [SerializeField] private TextMeshPro _myName;
    [SerializeField] private TextMeshPro _opponentName;

    private readonly List<int> _adds = new List<int>(16);
    private readonly List<AceToggle> _aceToggles = new List<AceToggle>(4);
    private GameManager _gm;
    private int _raiseSum;

    public bool IsRaising { get; private set; }

    // 패와 칩 기둥에 행동을 연결하고 레이즈 표시를 끈 뒤 게임 이벤트를 구독하고 지금 상태를 한 번 맞춘다
    private void Start()
    {
        _call.Init(OnCall);
        _raise.Init(OnRaise);
        _die.Init(OnDie);
        _confirmAces.Init(OnConfirmAces);
        foreach (ChipColumnButton column in _chipColumns) column.Init(this);
        _aceTemplate.gameObject.SetActive(false);
        RefreshRaiseVisuals(keepPreview: true);

        _gm = GameManager.Instance;
        if (_gm == null) return;
        _gm.StateChanged += OnStateChanged;
        _gm.CharactersChanged += OnCharactersChanged;
        OnCharactersChanged();
        OnStateChanged();
    }

    // 구독을 해제한다
    private void OnDestroy()
    {
        if (_gm == null) return;
        _gm.StateChanged -= OnStateChanged;
        _gm.CharactersChanged -= OnCharactersChanged;
    }

    // 합계, 베팅 패, 에이스 선택을 지금 단계와 차례에 맞춘다
    private void OnStateChanged()
    {
        _opponentTotal.text = _gm.GetTotalText(PlayerActions.OpponentSeat);
        UpdateBetting();
        UpdateAces();
        RefreshMyTotal();
    }

    // 양쪽 캐릭터 이름을 테이블 이름표에 쓴다
    private void OnCharactersChanged()
    {
        _myName.text = _gm.GetCharacterName(PlayerActions.LocalSeat).ToUpperInvariant();
        _opponentName.text = _gm.GetCharacterName(PlayerActions.OpponentSeat).ToUpperInvariant();
    }

    // 베팅 단계에만 패를 꺼내 두고 내 차례에 할 수 있는 행동만 진하게 한다
    private void UpdateBetting()
    {
        bool betting = _gm.CurrentPhase == GamePhase.Betting;
        bool myTurn = betting && PlayerActions.IsLocalTurn;
        _call.gameObject.SetActive(betting);
        _raise.gameObject.SetActive(betting);
        _die.gameObject.SetActive(betting);
        _call.SetAvailable(myTurn && _gm.CanCall);
        _raise.SetAvailable(myTurn && (IsRaising || _gm.CanRaise));
        _die.SetAvailable(myTurn && _gm.CanDie);
        if (!myTurn) ExitRaise();
        else if (IsRaising && _raiseSum > _gm.MaxRaise) ClearRaise();
    }

    // 콜을 요청한다
    private void OnCall()
    {
        ExitRaise();
        PlayerActions.Call();
    }

    // 다이를 요청한다
    private void OnDie()
    {
        ExitRaise();
        PlayerActions.Die();
    }

    // 처음 누르면 칩을 고르기 시작하고, 고르는 중에 다시 누르면 고른 만큼 레이즈를 확정한다(고른 게 없으면 취소)
    private void OnRaise()
    {
        if (!IsRaising)
        {
            IsRaising = true;
            ClearRaise();
            return;
        }

        if (_raiseSum <= 0)
        {
            ExitRaise();
            return;
        }

        int amount = _raiseSum;
        _chips.CommitPreview();
        IsRaising = false;
        _adds.Clear();
        _raiseSum = 0;
        RefreshRaiseVisuals(keepPreview: true);
        PlayerActions.Raise(amount);
    }

    // 고르는 중이면 칩 기둥 하나 값을 레이즈에 더한다(최대 레이즈를 넘으면 무시)
    public void AddChip(int value)
    {
        if (!IsRaising || _raiseSum + value > _gm.MaxRaise) return;
        _adds.Add(value);
        _raiseSum += value;
        RefreshRaiseVisuals(keepPreview: false);
    }

    // 마지막으로 더한 칩을 되돌린다
    public void UndoChip()
    {
        if (!IsRaising || _adds.Count == 0) return;
        _raiseSum -= _adds[_adds.Count - 1];
        _adds.RemoveAt(_adds.Count - 1);
        RefreshRaiseVisuals(keepPreview: false);
    }

    // 고른 칩을 비우고 고르는 상태는 유지한다
    private void ClearRaise()
    {
        _adds.Clear();
        _raiseSum = 0;
        RefreshRaiseVisuals(keepPreview: false);
    }

    // 고르기를 그만두고 미리 옮긴 칩을 되돌린다
    private void ExitRaise()
    {
        if (!IsRaising) return;
        IsRaising = false;
        _adds.Clear();
        _raiseSum = 0;
        RefreshRaiseVisuals(keepPreview: false);
    }

    // 레이즈 패 글자, 고른 금액, 칩 기둥 안내, 미리 옮긴 칩을 지금 상태로 맞춘다
    private void RefreshRaiseVisuals(bool keepPreview)
    {
        _raise.ShowAlt(IsRaising, _raiseSum <= 0);
        _raiseAmount.gameObject.SetActive(IsRaising);
        _raiseAmount.SetText("+{0}", _raiseSum);
        foreach (ChipColumnButton column in _chipColumns) column.SetHintVisible(IsRaising);
        if (!keepPreview) _chips.SetPreview(IsRaising ? _gm.CallAmount + _raiseSum : 0);
    }

    // 에이스 선택 단계에 아직 확정하지 않았으면 내 에이스마다 값 표시를 붙이고 확정 패를 꺼낸다
    private void UpdateAces()
    {
        bool choosing = _gm.CurrentPhase == GamePhase.AceSelection && !_gm.IsAceConfirmed(PlayerActions.LocalSeat);
        if (!choosing)
        {
            ClearAceToggles();
            _confirmAces.gameObject.SetActive(false);
            return;
        }

        _confirmAces.gameObject.SetActive(true);
        _confirmAces.SetAvailable(true);
        if (_aceToggles.Count > 0) return;

        IReadOnlyList<CardSlot> cards = PlayerActions.LocalSeat == ActivePlayer.Player1 ? _gm.GetPlayer1Cards() : _gm.GetPlayer2Cards();
        IReadOnlyList<CardView> views = _table.MyCards;
        for (int i = 0; i < cards.Count && i < views.Count; i++)
        {
            if (cards[i].Card.Rank != Rank.Ace) continue;
            AceToggle toggle = Instantiate(_aceTemplate, transform);
            toggle.gameObject.SetActive(true);
            toggle.Bind(views[i], i, RefreshMyTotal);
            _aceToggles.Add(toggle);
        }
    }

    // 내 합계를 쓰되, 에이스를 고르는 중이면 확정 전 합계(에이스는 모두 1)에 11로 고른 에이스마다 10을 더해 보여 준다
    private void RefreshMyTotal()
    {
        string text = _gm.GetTotalText(PlayerActions.LocalSeat);
        if (_aceToggles.Count == 0 || !int.TryParse(text, out int total))
        {
            _myTotal.text = text;
            return;
        }

        foreach (AceToggle toggle in _aceToggles)
            if (toggle.IsEleven) total += 10;
        _myTotal.SetText("{0}", total);
    }

    // 에이스 선택을 확정하고 값 표시와 확정 패를 치운다
    private void OnConfirmAces()
    {
        PlayerActions.ConfirmAces();
        ClearAceToggles();
        _confirmAces.gameObject.SetActive(false);
    }

    // 에이스 값 표시를 모두 지운다
    private void ClearAceToggles()
    {
        foreach (AceToggle toggle in _aceToggles) Destroy(toggle.gameObject);
        _aceToggles.Clear();
    }
}
