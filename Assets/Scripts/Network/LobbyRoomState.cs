using Fusion;
using UnityEngine;

public class LobbyRoomState : NetworkBehaviour
{
    public static LobbyRoomState Instance { get; private set; }

    public const byte Unselected = 255;

    private const float AnnounceRetryInterval = 0.5f;

    [Networked] public NetworkString<_32> HostName { get; set; }
    [Networked] public NetworkString<_32> GuestName { get; set; }
    [Networked] public NetworkBool GuestReady { get; set; }
    [Networked] public byte HostCharacter { get; set; }
    [Networked] public byte GuestCharacter { get; set; }

    public bool IsSpawned => _spawned;
    public bool HasGuest => _spawned && !string.IsNullOrEmpty(GuestName.ToString());

    public bool CanStart => HasGuest && GuestReady
        && HostCharacter != Unselected && GuestCharacter != Unselected;

    private bool _spawned;
    private float _nextAnnounceAt;

    // 싱글턴을 등록한다
    private void Awake() => Instance = this;

    // 싱글턴 등록을 해제한다
    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // 생성되면 호스트가 대기실 상태를 초기화한다
    public override void Spawned()
    {
        _spawned = true;
        if (Object.HasStateAuthority)
        {
            HostName = NetworkManager.Instance.Nickname;
            GuestName = "";
            GuestReady = false;
            HostCharacter = Unselected;
            GuestCharacter = Unselected;
        }
    }

    // 제거되면 생성 상태를 해제한다
    public override void Despawned(NetworkRunner runner, bool hasState) => _spawned = false;

    // 게스트가 자신의 닉네임을 호스트에 반복해서 알린다
    public override void Render()
    {
        if (Object.HasStateAuthority || Time.unscaledTime < _nextAnnounceAt) return;
        if (GuestName.ToString() == NetworkManager.Instance.Nickname) return;

        _nextAnnounceAt = Time.unscaledTime + AnnounceRetryInterval;
        RPC_Announce(NetworkManager.Instance.Nickname);
    }

    // 호스트가 게스트의 입장을 기록한다
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_Announce(string nickname)
    {
        if (GuestName.ToString() == nickname) return;
        GuestName = nickname;
        GuestReady = false;
        GuestCharacter = Unselected;
    }

    // 호스트가 게스트의 준비 상태를 기록한다
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_SetReady(bool ready) => GuestReady = ready;

    // 호스트가 게스트의 캐릭터 선택을 기록한다
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_SetCharacter(byte character) => GuestCharacter = character;

    // 캐릭터를 선택한다
    public void SelectCharacter(CharacterType type)
    {
        if (!_spawned) return;
        if (Object.HasStateAuthority) HostCharacter = (byte)type;
        else RPC_SetCharacter((byte)type);
    }

    // 게스트의 준비 상태를 바꾼다
    public void SetReady(bool ready)
    {
        if (!_spawned || Object.HasStateAuthority) return;
        RPC_SetReady(ready);
    }

    // 게스트 자리를 비운다
    public void ClearGuest()
    {
        if (!_spawned || !Object.HasStateAuthority) return;
        GuestName = "";
        GuestReady = false;
        GuestCharacter = Unselected;
    }
}