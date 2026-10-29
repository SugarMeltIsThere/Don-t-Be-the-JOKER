using Fusion;
using UnityEngine;

// 인게임 네트워크 동기화 (Photon Fusion 2, Host Mode).
// 호스트 = Player1 = 규칙 실행 주체, 게스트 = Player2 = 화면 미러.
//  - 공개 상태(단계/차례/칩/팟/베팅/아이템…): [Networked] GameSnapshot 하나로 복제
//  - 손패(비공개 정보): 호스트가 게스트에게 알려도 되는 만큼만 RPC로 전달
//  - 게스트의 조작: RPC_Command 하나로 호스트에 전달하고, 호스트가 자리·차례를 확인한 뒤 실행
// 씬(GameScene)에 NetworkObject 컴포넌트와 함께 이 컴포넌트를 올려 둔다.
public class NetworkGameManager : NetworkBehaviour, IGameNet
{
    public static NetworkGameManager Instance { get; private set; }

    private enum Command : byte
    {
        Hit, Stand, AceValue, AceConfirm, Raise, Call, Die, UseItem, Discard, ReturnToWaitingRoom
    }

    private const float ReadyRetryInterval = 0.5f;

    [Networked, OnChangedRender(nameof(OnSnapshotChanged))]
    private GameSnapshot Snapshot { get; set; }

    private bool _spawned;
    private bool _started;              // 호스트: 게임 시작 완료 여부
    private PlayerRef _guest;           // 호스트: 게스트의 PlayerRef (ClientReady로 확인)
    private float _nextReadyAt;         // 게스트: 준비 신호 재전송 시각

    // 호스트 = P1, 게스트 = P2. 역할은 세션 시작 시점에 정해지므로 Spawned 전에도 쓸 수 있다.
    public bool IsHost => NetworkManager.Instance != null && NetworkManager.Instance.IsHost;
    public ActivePlayer LocalPlayer => IsHost ? ActivePlayer.Player1 : ActivePlayer.Player2;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public override void Spawned()
    {
        _spawned = true;
        Debug.Log($"[네트워크] 나는 {LocalPlayer}");

        if (!Object.HasStateAuthority)
            GameManager.Instance.MirrorApply(Snapshot);   // 이미 시작된 상태라면 즉시 반영
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        _spawned = false;
    }

    // 게스트: 호스트가 게임을 시작할 때까지 준비 신호를 반복 전송 (호스트 쪽 오브젝트가 아직 없을 수 있어서)
    public override void Render()
    {
        if (Object.HasStateAuthority || Snapshot.Round > 0 || Time.unscaledTime < _nextReadyAt) return;

        _nextReadyAt = Time.unscaledTime + ReadyRetryInterval;
        RPC_ClientReady();
    }

    // ───────────────────────── 시작 핸드셰이크 ─────────────────────────
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_ClientReady(RpcInfo info = default)
    {
        if (_started) return;

        _guest = info.Source;
        _started = true;
        Debug.Log("[네트워크] 게스트 준비 완료 → 게임 시작");
        GameManager.Instance.InitializeGame();
    }

    // ───────────────────────── 호스트 → 게스트 ─────────────────────────
    // 카드는 (랭크*4 + 무늬) 한 개의 정수로 보낸다. 0 = 없음.
    private static int Pack(Card card) => card == null ? 0 : (int)card.Rank * 4 + (int)card.Suit;
    private static Card Unpack(int value) => value == 0 ? null : new Card((Rank)(value / 4), (Suit)(value % 4));

    public void PublishState(GameSnapshot snapshot) => Snapshot = snapshot;

    // 게스트의 자리는 항상 Player2: 게스트에게 의미 있는 이벤트만 보낸다.
    public void SendRoundStart(int round, Card sharedCard) => RPC_RoundStart(round, Pack(sharedCard));

    public void SendCard(ActivePlayer seat, Card card, Card revealedPrevious)
    {
        if (seat == ActivePlayer.Player2) RPC_OwnCard(Pack(card));                 // 내 카드: 진짜 값
        else RPC_OpponentCard(Pack(revealedPrevious));                              // 상대 카드: 공개된 직전 카드만
    }

    public void SendReplaceLast(ActivePlayer seat, Card card)
    {
        if (seat == ActivePlayer.Player2) RPC_ReplaceLast(Pack(card));
    }

    public void SendPeek(ActivePlayer seat, Card card)
    {
        if (seat == ActivePlayer.Player2) RPC_Peek(Pack(card));
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.Proxies)]
    private void RPC_RoundStart(int round, int sharedCard) =>
        GameManager.Instance.MirrorRoundStart(round, Unpack(sharedCard));

    [Rpc(RpcSources.StateAuthority, RpcTargets.Proxies)]
    private void RPC_OwnCard(int card) =>
        GameManager.Instance.MirrorCard(ActivePlayer.Player2, Unpack(card), null);

    [Rpc(RpcSources.StateAuthority, RpcTargets.Proxies)]
    private void RPC_OpponentCard(int revealedPrevious) =>
        GameManager.Instance.MirrorCard(ActivePlayer.Player1, GameManager.HiddenCard, Unpack(revealedPrevious));

    [Rpc(RpcSources.StateAuthority, RpcTargets.Proxies)]
    private void RPC_ReplaceLast(int card) =>
        GameManager.Instance.MirrorReplaceLast(ActivePlayer.Player2, Unpack(card));

    [Rpc(RpcSources.StateAuthority, RpcTargets.Proxies)]
    private void RPC_Peek(int card) =>
        GameManager.Instance.MirrorPeek(Unpack(card));

    private void OnSnapshotChanged()
    {
        if (Object.HasStateAuthority) return;   // 호스트는 자기 상태가 원본이다
        GameManager.Instance.MirrorApply(Snapshot);
    }

    // ───────────────────────── 게스트 → 호스트 (UI 진입점) ─────────────────────────
    public void RequestHit() => Send(Command.Hit);
    public void RequestStand() => Send(Command.Stand);
    public void RequestRaise(int amount) => Send(Command.Raise, amount);
    public void RequestCall() => Send(Command.Call);
    public void RequestDie() => Send(Command.Die);
    public void RequestUseItem(ItemType type) => Send(Command.UseItem, (int)type);
    public void RequestDiscard(int discardIndex) => Send(Command.Discard, discardIndex);
    public void RequestReturnToWaitingRoom() => Send(Command.ReturnToWaitingRoom);

    // player 인자는 기존 UI 호출 형태를 맞추기 위한 것이고, 실제 자리는 보낸 사람으로 정해진다.
    public void RequestAceValueChanged(ActivePlayer player, int cardIndex, bool isEleven)
    {
        if (!Object.HasStateAuthority)
            GameManager.Instance.MirrorAceValue(LocalPlayer, cardIndex, isEleven);   // 내 화면 합계에는 바로 반영
        Send(Command.AceValue, cardIndex, isEleven ? 1 : 0);
    }

    public void RequestAceConfirmed(ActivePlayer player)
    {
        if (!Object.HasStateAuthority)
            GameManager.Instance.MirrorAceConfirm(LocalPlayer);
        Send(Command.AceConfirm);
    }

    private void Send(Command command, int a = 0, int b = 0)
    {
        if (!_spawned) return;

        if (Object.HasStateAuthority) Execute(ActivePlayer.Player1, command, a, b);   // 호스트는 직접 실행
        else RPC_Command((byte)command, a, b);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_Command(byte command, int a, int b, RpcInfo info = default)
    {
        if (info.Source != _guest) return;   // 게스트가 보낸 것만 받는다 (호스트 본인 조작은 Send에서 직접 처리)
        Execute(ActivePlayer.Player2, (Command)command, a, b);
    }

    // 호스트에서만 실행: 요청한 자리(seat)가 지금 조작할 수 있는지 확인한 뒤 GameManager에 넘긴다.
    private void Execute(ActivePlayer seat, Command command, int a, int b)
    {
        GameManager gm = GameManager.Instance;
        bool myTurn = gm.CurrentPlayer == seat;

        switch (command)
        {
            case Command.Hit: if (myTurn) gm.OnHit(); break;
            case Command.Stand: if (myTurn) gm.OnStand(); break;
            case Command.Raise: if (myTurn) gm.OnRaise(a); break;
            case Command.Call: if (myTurn) gm.OnCall(); break;
            case Command.Die: if (myTurn) gm.OnDie(); break;
            case Command.AceValue: gm.OnAceValueChanged(seat, a, b != 0); break;
            case Command.AceConfirm: gm.OnAceSelectionConfirmed(seat); break;
            case Command.UseItem: gm.HostUseItem(seat, (ItemType)a); break;
            case Command.Discard: gm.HostDiscard(seat, a); break;
            case Command.ReturnToWaitingRoom: NetworkManager.Instance.ReturnToWaitingRoom(); break;
        }
    }
}