using Fusion;
using UnityEngine;

// 대기실 상태(닉네임, 준비 여부)를 호스트와 게스트가 공유하기 위한 씬 오브젝트.
// LobbyScene에 NetworkObject 컴포넌트와 함께 올려 둔다. (PUN2의 닉네임 / IsReady 커스텀 프로퍼티 대체)
public class LobbyRoomState : NetworkBehaviour
{
    public static LobbyRoomState Instance { get; private set; }

    private const float AnnounceRetryInterval = 0.5f;

    [Networked] public NetworkString<_32> HostName { get; set; }
    [Networked] public NetworkString<_32> GuestName { get; set; }
    [Networked] public NetworkBool GuestReady { get; set; }

    public bool HasGuest => !string.IsNullOrEmpty(GuestName.ToString());

    private bool _spawned;
    private float _nextAnnounceAt;

    private void Awake() => Instance = this;

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public override void Spawned()
    {
        _spawned = true;
        if (Object.HasStateAuthority)
        {
            HostName = NetworkManager.Instance.Nickname;
            GuestName = "";
            GuestReady = false;
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState) => _spawned = false;

    // 게스트: 호스트 쪽 오브젝트가 아직 없을 수 있으므로, 내 이름이 반영될 때까지 주기적으로 알린다.
    public override void Render()
    {
        if (Object.HasStateAuthority || Time.unscaledTime < _nextAnnounceAt) return;
        if (GuestName.ToString() == NetworkManager.Instance.Nickname) return;

        _nextAnnounceAt = Time.unscaledTime + AnnounceRetryInterval;
        RPC_Announce(NetworkManager.Instance.Nickname);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_Announce(string nickname)
    {
        GuestName = nickname;
        GuestReady = false;
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_SetReady(bool ready) => GuestReady = ready;

    public void SetReady(bool ready)
    {
        if (!_spawned || Object.HasStateAuthority) return;   // 준비는 게스트만 누른다
        RPC_SetReady(ready);
    }

    // 호스트: 게스트가 나가면 자리를 비운다
    public void ClearGuest()
    {
        if (!_spawned || !Object.HasStateAuthority) return;
        GuestName = "";
        GuestReady = false;
    }
}