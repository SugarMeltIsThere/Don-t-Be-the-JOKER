using System.Collections;
using Fusion;
using UnityEngine;

public class NetworkGameManager : NetworkBehaviour, IGameNet
{
    public static NetworkGameManager Instance { get; private set; }

    private enum Command : byte
    {
        Hit, Stand, AceValue, AceConfirm, Raise, Call, Die, UseItem, Discard, ReturnToWaitingRoom
    }

    private const float ReadyRetryInterval = 0.5f;
    private const float ReturnWaitSeconds = 3f;

    [Networked, OnChangedRender(nameof(OnSnapshotChanged))]
    private GameSnapshot Snapshot { get; set; }

    private bool _spawned;
    private bool _started;
    private bool _returning;
    private bool _guestWantsReturn;
    private PlayerRef _guest;
    private float _nextReadyAt;

    public bool IsHost => NetworkManager.Instance != null && NetworkManager.Instance.IsHost;
    public ActivePlayer LocalPlayer => IsHost ? ActivePlayer.Player1 : ActivePlayer.Player2;

    // 싱글턴을 등록한다
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    // 싱글턴 등록을 해제한다
    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // 생성되면 게스트는 현재 상태를 즉시 반영한다
    public override void Spawned()
    {
        _spawned = true;
        Debug.Log($"[네트워크] 나는 {LocalPlayer}");

        if (!Object.HasStateAuthority)
            GameManager.Instance.MirrorApply(Snapshot);
    }

    // 제거되면 생성 상태를 해제한다
    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        _spawned = false;
    }

    // 게스트가 호스트에 준비 신호를 반복해서 보낸다
    public override void Render()
    {
        if (Object.HasStateAuthority || Snapshot.Round > 0 || Time.unscaledTime < _nextReadyAt) return;

        _nextReadyAt = Time.unscaledTime + ReadyRetryInterval;
        RPC_ClientReady();
    }

    // 게스트가 준비되면 호스트가 게임을 시작한다
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_ClientReady(RpcInfo info = default)
    {
        if (_started) return;

        _guest = info.Source;
        _started = true;
        Debug.Log("[네트워크] 게스트 준비 완료 → 게임 시작");

        NetworkManager nm = NetworkManager.Instance;
        GameManager.Instance.InitializeGame(nm.HostCharacter, nm.GuestCharacter);
    }

    // 카드를 정수 하나로 압축한다
    private static int Pack(Card card) => card == null ? 0 : (int)card.Rank * 4 + (int)card.Suit;
    // 정수를 카드로 푼다
    private static Card Unpack(int value) => value == 0 ? null : new Card((Rank)(value / 4), (Suit)(value % 4));

    // 호스트가 상태를 게스트에 복제한다
    public void PublishState(GameSnapshot snapshot)
    {
        if (_spawned) Snapshot = snapshot;
    }

    // 게스트에 새 라운드 시작을 알린다
    public void SendRoundStart(int round, Card sharedCard)
    {
        if (_spawned) RPC_RoundStart(round, Pack(sharedCard));
    }

    // 게스트에 카드가 나뉜 것을 알린다
    public void SendCard(ActivePlayer seat, Card card, Card revealedPrevious)
    {
        if (!_spawned) return;
        if (seat == ActivePlayer.Player2) RPC_OwnCard(Pack(card));
        else RPC_OpponentCard(Pack(revealedPrevious));
    }

    // 게스트에 마지막 카드 교체를 알린다
    public void SendReplaceLast(ActivePlayer seat, Card card)
    {
        if (_spawned && seat == ActivePlayer.Player2) RPC_ReplaceLast(Pack(card));
    }

    // 게스트에 돋보기 결과를 알린다
    public void SendPeek(ActivePlayer seat, Card card)
    {
        if (_spawned && seat == ActivePlayer.Player2) RPC_Peek(Pack(card));
    }

    // 게스트에 라운드 종료 시 호스트 카드를 공개한다
    public void SendReveal(Card hostLastCard, int hostAceMask)
    {
        if (_spawned) RPC_Reveal(Pack(hostLastCard), hostAceMask);
    }

    // 게스트에 호스트의 에이스 선택을 알린다
    public void SendOpponentAces(int mask)
    {
        if (_spawned) RPC_OpponentAces(mask);
    }

    // 게스트에 아이템 사용을 알린다
    public void SendItemUsed(ActivePlayer seat, ItemType type)
    {
        if (_spawned) RPC_ItemUsed((byte)seat, (byte)type);
    }

    // 게스트에서 아이템 사용을 반영한다
    [Rpc(RpcSources.StateAuthority, RpcTargets.Proxies)]
    private void RPC_ItemUsed(byte seat, byte type) =>
        GameManager.Instance.MirrorItemUsed((ActivePlayer)seat, (ItemType)type);

    // 게스트에서 새 라운드를 시작한다
    [Rpc(RpcSources.StateAuthority, RpcTargets.Proxies)]
    private void RPC_RoundStart(int round, int sharedCard) =>
        GameManager.Instance.MirrorRoundStart(round, Unpack(sharedCard));

    // 게스트가 자신의 카드를 받는다
    [Rpc(RpcSources.StateAuthority, RpcTargets.Proxies)]
    private void RPC_OwnCard(int card) =>
        GameManager.Instance.MirrorCard(ActivePlayer.Player2, Unpack(card), null);

    // 게스트가 상대 카드를 가려진 채로 받는다
    [Rpc(RpcSources.StateAuthority, RpcTargets.Proxies)]
    private void RPC_OpponentCard(int revealedPrevious) =>
        GameManager.Instance.MirrorCard(ActivePlayer.Player1, GameManager.HiddenCard, Unpack(revealedPrevious));

    // 게스트의 마지막 카드를 교체한다
    [Rpc(RpcSources.StateAuthority, RpcTargets.Proxies)]
    private void RPC_ReplaceLast(int card) =>
        GameManager.Instance.MirrorReplaceLast(ActivePlayer.Player2, Unpack(card));

    // 게스트에 돋보기 결과를 보여준다
    [Rpc(RpcSources.StateAuthority, RpcTargets.Proxies)]
    private void RPC_Peek(int card) =>
        GameManager.Instance.MirrorPeek(Unpack(card));

    // 게스트에서 호스트 카드를 공개한다
    [Rpc(RpcSources.StateAuthority, RpcTargets.Proxies)]
    private void RPC_Reveal(int hostLastCard, int hostAceMask) =>
        GameManager.Instance.MirrorReveal(Unpack(hostLastCard), hostAceMask);

    // 게스트에서 호스트의 에이스 선택을 반영한다
    [Rpc(RpcSources.StateAuthority, RpcTargets.Proxies)]
    private void RPC_OpponentAces(int mask) =>
        GameManager.Instance.MirrorOpponentAces(mask);

    // 스냅샷이 바뀌면 게스트가 상태를 반영한다
    private void OnSnapshotChanged()
    {
        if (Object.HasStateAuthority) return;
        GameManager.Instance.MirrorApply(Snapshot);
    }

    // 카드 받기를 요청한다
    public void RequestHit() => Send(Command.Hit);
    // 스탠드를 요청한다
    public void RequestStand() => Send(Command.Stand);
    // 레이즈를 요청한다
    public void RequestRaise(int amount) => Send(Command.Raise, amount);
    // 콜을 요청한다
    public void RequestCall() => Send(Command.Call);
    // 다이를 요청한다
    public void RequestDie() => Send(Command.Die);

    // 돋보기 사용을 요청한다
    public void RequestUseMagnifier(ActivePlayer player) => Send(Command.UseItem, (int)ItemType.Magnifier);
    // 뇌물 사용을 요청한다
    public void RequestUseBribe(ActivePlayer player) => Send(Command.UseItem, (int)ItemType.Bribe);
    // 플래시 사용을 요청한다
    public void RequestUseFlash(ActivePlayer player) => Send(Command.UseItem, (int)ItemType.Flash);
    // 아이템 폐기를 요청한다
    public void RequestItemDiscard(ActivePlayer player, int discardIndex) => Send(Command.Discard, discardIndex);

    // 아이템 사용을 요청한다
    public void RequestUseItem(ItemType type) => Send(Command.UseItem, (int)type);
    // 아이템 폐기 선택을 요청한다
    public void RequestDiscard(int discardIndex) => Send(Command.Discard, discardIndex);

    // 에이스 값 변경을 요청한다
    public void RequestAceValueChanged(ActivePlayer player, int cardIndex, bool isEleven)
    {
        if (!Object.HasStateAuthority)
            GameManager.Instance.MirrorAceValue(LocalPlayer, cardIndex, isEleven);
        Send(Command.AceValue, cardIndex, isEleven ? 1 : 0);
    }

    // 에이스 선택 확정을 요청한다
    public void RequestAceConfirmed(ActivePlayer player)
    {
        if (!Object.HasStateAuthority)
            GameManager.Instance.MirrorAceConfirm(LocalPlayer);
        Send(Command.AceConfirm);
    }

    // 호스트는 직접 실행하고 게스트는 호스트에 명령을 보낸다
    private void Send(Command command, int a = 0, int b = 0)
    {
        if (!_spawned) return;

        if (Object.HasStateAuthority) Execute(ActivePlayer.Player1, command, a, b);
        else RPC_Command((byte)command, a, b);
    }

    // 호스트가 게스트의 명령을 받는다
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_Command(byte command, int a, int b, RpcInfo info = default)
    {
        if (info.Source != _guest) return;
        Execute(ActivePlayer.Player2, (Command)command, a, b);
    }

    // 요청한 자리의 권한을 확인한 뒤 명령을 실행한다
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
            case Command.ReturnToWaitingRoom: _guestWantsReturn = true; break;
        }
    }

    // 게임이 끝난 뒤 대기실로 돌아가기를 요청한다
    public void RequestReturnToWaitingRoom()
    {
        if (!_spawned) return;

        if (!Object.HasStateAuthority)
        {
            Send(Command.ReturnToWaitingRoom);
            return;
        }

        if (_returning) return;
        _returning = true;
        StartCoroutine(HostReturnRoutine());
    }

    // 게스트의 요청을 잠시 기다린 뒤 로비 씬으로 이동한다
    private IEnumerator HostReturnRoutine()
    {
        float deadline = Time.unscaledTime + ReturnWaitSeconds;
        while (!_guestWantsReturn && Time.unscaledTime < deadline)
            yield return null;

        NetworkManager.Instance.LoadLobbyScene();
    }
}