using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Fusion;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.SceneManagement;

// 접속·방·씬 전환을 맡는 Photon Fusion(Host Mode) 래퍼. 씬이 바뀌어도 살아 있다(DontDestroyOnLoad).
// 러너는 자식 오브젝트에 만들고, 로비용 러너(방 목록 수신)와 세션용 러너(방 생성/참가)를 번갈아 쓴다.
public class NetworkManager : MonoBehaviour, INetworkRunnerCallbacks
{
    public static NetworkManager Instance { get; private set; }

    public const int MaxPlayers = 2;

    [SerializeField] private int _lobbySceneIndex = 0;   // Build Settings: 0 LobbyScene
    [SerializeField] private int _gameSceneIndex = 1;    // Build Settings: 1 GameScene

    private enum Mode { None, Lobby, Session }

    private NetworkRunner _runner;
    private Mode _mode = Mode.None;

    public string Nickname { get; set; } = "";
    public ShutdownReason LastFailure { get; private set; }

    public event Action<List<SessionInfo>> RoomListUpdated;
    public event Action<string> SessionEnded;   // 대기실에서 세션이 끊겼을 때 (사유 문구)

    // 방을 만들었거나 들어간 상태 (시작 처리 중 포함 — 씬이 다시 로드돼도 대기실을 유지하기 위해)
    public bool HasSession => _mode == Mode.Session;
    public bool IsHost => HasSession && _runner != null && _runner.IsRunning && _runner.IsServer;
    public string RoomCode => HasSession && _runner != null && _runner.IsRunning ? _runner.SessionInfo.Name : "";
    public int PlayerCount => HasSession && _runner != null && _runner.IsRunning ? _runner.ActivePlayers.Count() : 0;

    private bool InGameScene => SceneManager.GetActiveScene().buildIndex == _gameSceneIndex;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // ───────────────────────── 접속 / 방 ─────────────────────────
    public async Task<bool> JoinLobbyAsync()
    {
        await ShutdownRunnerAsync();
        _runner = CreateRunner(Mode.Lobby);

        StartGameResult result = await _runner.JoinSessionLobby(SessionLobby.ClientServer);
        if (!result.Ok) Debug.LogWarning($"[네트워크] 로비 접속 실패: {result.ShutdownReason}");
        return result.Ok;
    }

    public Task<bool> CreateRoomAsync(string roomCode) => StartSessionAsync(GameMode.Host, roomCode);
    public Task<bool> JoinRoomAsync(string roomCode) => StartSessionAsync(GameMode.Client, roomCode);

    private async Task<bool> StartSessionAsync(GameMode mode, string roomCode)
    {
        await ShutdownRunnerAsync();
        _runner = CreateRunner(Mode.Session);
        NetworkSceneManagerDefault sceneManager = _runner.gameObject.AddComponent<NetworkSceneManagerDefault>();
        int sceneIndex = SceneManager.GetActiveScene().buildIndex;   // 현재(로비) 씬의 NetworkObject를 러너가 인식하도록 전달

        StartGameResult result = await _runner.StartGame(new StartGameArgs
        {
            GameMode = mode,
            SessionName = roomCode,
            PlayerCount = MaxPlayers,
            Scene = SceneRef.FromIndex(sceneIndex),
            SceneManager = sceneManager
        });

        if (result.Ok) return true;

        LastFailure = result.ShutdownReason;
        Debug.LogWarning($"[네트워크] 방 {mode} 실패: {result.ShutdownReason}");
        await JoinLobbyAsync();   // 실패한 러너는 정리하고, 방 목록을 계속 받도록 로비로 복귀
        return false;
    }

    // 게임 씬에서 방을 완전히 나가 로비 씬으로 (UIManager의 나가기 버튼). 대기실의 Leave는 JoinLobbyAsync()를 쓴다.
    public async void LeaveToLobby()
    {
        await ShutdownRunnerAsync();
        SceneManager.LoadScene(_lobbySceneIndex);
    }

    // ───────────────────────── 씬 전환 (호스트가 모두를 함께 이동시킨다) ─────────────────────────
    public void StartGame()
    {
        if (!IsHost || PlayerCount < MaxPlayers) return;
        Debug.Log("[로비] 게임 시작 → 씬 전환");
        _runner.LoadScene(SceneRef.FromIndex(_gameSceneIndex));
    }

    // 게임이 끝난 뒤 같은 방/상대 그대로 대기실로 복귀 (게스트는 호스트에게 요청)
    public void ReturnToWaitingRoom()
    {
        if (IsHost) _runner.LoadScene(SceneRef.FromIndex(_lobbySceneIndex));
        else if (NetworkGameManager.Instance != null) NetworkGameManager.Instance.RequestReturnToWaitingRoom();
        else LeaveToLobby();
    }

    // ───────────────────────── 러너 관리 ─────────────────────────
    private NetworkRunner CreateRunner(Mode mode)
    {
        GameObject go = new GameObject($"NetworkRunner ({mode})");
        go.transform.SetParent(transform);

        NetworkRunner runner = go.AddComponent<NetworkRunner>();
        runner.AddCallbacks(this);
        _mode = mode;
        return runner;
    }

    private async Task ShutdownRunnerAsync()
    {
        NetworkRunner old = _runner;
        _runner = null;
        _mode = Mode.None;
        if (old == null) return;

        old.RemoveCallbacks(this);   // 의도한 종료이므로 아래 콜백(세션 끊김 처리)이 반응하지 않게 한다
        await old.Shutdown();
        if (old != null) Destroy(old.gameObject);
    }

    // ───────────────────────── Fusion 콜백 ─────────────────────────
    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
    {
        if (runner == _runner && _mode == Mode.Lobby)
            RoomListUpdated?.Invoke(sessionList);
    }

    // 호스트가 게스트의 이탈을 처리한다: 게임 중이면 즉시 기권패, 대기실이면 자리를 비운다.
    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        if (runner != _runner || _mode != Mode.Session || !runner.IsServer) return;
        Debug.Log("[네트워크] 게스트 이탈");

        if (InGameScene)
        {
            if (GameManager.Instance != null) GameManager.Instance.Forfeit(ActivePlayer.Player2);
        }
        else if (LobbyRoomState.Instance != null)
        {
            LobbyRoomState.Instance.ClearGuest();
        }
    }

    // 게스트 쪽: 호스트가 나가거나 연결이 끊김
    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) =>
        HandleSessionLost(runner, $"Disconnected ({reason})");

    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) =>
        HandleSessionLost(runner, $"Session closed ({shutdownReason})");

    private void HandleSessionLost(NetworkRunner runner, string message)
    {
        if (runner != _runner || _mode == Mode.None) return;
        bool wasHost = runner.IsServer;
        Mode lostMode = _mode;
        _mode = Mode.None;   // 같은 사건을 두 번 처리하지 않도록
        Debug.LogWarning($"[네트워크] {message}");

        if (lostMode == Mode.Lobby || !InGameScene)
            SessionEnded?.Invoke(message);
        else if (!wasHost && GameManager.Instance != null)
            GameManager.Instance.Forfeit(ActivePlayer.Player1);   // 호스트가 나감 → 남은 게스트 승리, 나가기는 UI 버튼으로
        else
            LeaveToLobby();
    }

    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) =>
        Debug.LogWarning($"[네트워크] 연결 실패: {reason}");

    // ── 쓰지 않는 콜백 (인터페이스 요구사항) ──
    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player) { }
    public void OnInput(NetworkRunner runner, NetworkInput input) { }
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnConnectedToServer(NetworkRunner runner) { }
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ReadOnlySpan<byte> data) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
    public void OnSceneLoadDone(NetworkRunner runner) { }
    public void OnSceneLoadStart(NetworkRunner runner) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
}