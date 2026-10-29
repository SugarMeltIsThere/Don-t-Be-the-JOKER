using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Fusion;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.SceneManagement;

public class NetworkManager : MonoBehaviour, INetworkRunnerCallbacks
{
    public static NetworkManager Instance { get; private set; }

    public const int MaxPlayers = 2;
    private const string RoomTitleKey = "RoomTitle";

    [SerializeField] private int _lobbySceneIndex = 0;
    [SerializeField] private int _gameSceneIndex = 1;

    private enum Mode { None, Lobby, Session }

    private NetworkRunner _runner;
    private Mode _mode = Mode.None;
    private string _localRoomTitle = "";

    public string Nickname { get; set; } = "";

    public CharacterType HostCharacter { get; private set; } = CharacterType.King;
    public CharacterType GuestCharacter { get; private set; } = CharacterType.Queen;

    public string PendingGameResultText { get; set; }

    public ShutdownReason LastFailure { get; private set; }

    public event Action<List<SessionInfo>> RoomListUpdated;
    public event Action<string> SessionEnded;

    public bool HasSession => _mode == Mode.Session;
    public bool IsHost => HasSession && _runner != null && _runner.IsRunning && _runner.IsServer;
    public string RoomCode => HasSession && _runner != null && _runner.IsRunning ? _runner.SessionInfo.Name : "";
    public int PlayerCount => HasSession && _runner != null && _runner.IsRunning ? _runner.ActivePlayers.Count() : 0;

    public string RoomTitle
    {
        get
        {
            if (!HasSession || _runner == null || !_runner.IsRunning) return "";
            if (_runner.IsServer && !string.IsNullOrEmpty(_localRoomTitle)) return _localRoomTitle;
            return ReadTitle(_runner.SessionInfo, RoomCode);
        }
    }

    // 세션 정보에서 방 제목을 읽는다
    public static string ReadTitle(SessionInfo info, string fallback)
    {
        try
        {
            if (info.Properties != null && info.Properties.TryGetValue(RoomTitleKey, out SessionProperty prop))
            {
                string title = prop;
                if (!string.IsNullOrEmpty(title)) return title;
            }
        }
        catch (Exception) { /* 속성 형식이 다르면 코드로 대체 */ }
        return fallback;
    }

    private bool InGameScene => SceneManager.GetActiveScene().buildIndex == _gameSceneIndex;

    // 싱글턴을 등록하고 씬이 바뀌어도 유지한다
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

    // 종료할 때 러너를 먼저 정리한다
    private void OnApplicationQuit()
    {
        NetworkRunner old = _runner;
        _runner = null;
        _mode = Mode.None;
        if (old == null) return;

        old.RemoveCallbacks(this);
        _ = old.Shutdown();
    }

    // 방 목록을 받는 로비에 접속한다
    public async Task<bool> JoinLobbyAsync()
    {
        await ShutdownRunnerAsync();
        _runner = CreateRunner(Mode.Lobby);

        StartGameResult result = await _runner.JoinSessionLobby(SessionLobby.ClientServer);
        if (!result.Ok) Debug.LogWarning($"[네트워크] 로비 접속 실패: {result.ShutdownReason}");
        return result.Ok;
    }

    // 방을 만들고 호스트로 시작한다
    public Task<bool> CreateRoomAsync(string roomCode, string roomTitle) =>
        StartSessionAsync(GameMode.Host, roomCode, roomTitle);

    // 방 코드로 참가한다
    public Task<bool> JoinRoomAsync(string roomCode) =>
        StartSessionAsync(GameMode.Client, roomCode, null);

    // 세션을 시작하고 성공 여부를 반환한다
    private async Task<bool> StartSessionAsync(GameMode mode, string roomCode, string roomTitle)
    {
        await ShutdownRunnerAsync();
        _runner = CreateRunner(Mode.Session);
        _localRoomTitle = roomTitle ?? "";
        NetworkSceneManagerDefault sceneManager = _runner.gameObject.AddComponent<NetworkSceneManagerDefault>();
        int sceneIndex = SceneManager.GetActiveScene().buildIndex;

        StartGameArgs args = new StartGameArgs
        {
            GameMode = mode,
            SessionName = roomCode,
            PlayerCount = MaxPlayers,
            Scene = SceneRef.FromIndex(sceneIndex),
            SceneManager = sceneManager
        };

        if (mode == GameMode.Host && !string.IsNullOrEmpty(roomTitle))
            args.SessionProperties = new Dictionary<string, SessionProperty> { { RoomTitleKey, roomTitle } };

        StartGameResult result = await _runner.StartGame(args);
        if (result.Ok) return true;

        LastFailure = result.ShutdownReason;
        Debug.LogWarning($"[네트워크] 방 {mode} 실패: {result.ShutdownReason}");
        await JoinLobbyAsync();
        return false;
    }

    // 방을 나가 로비 씬으로 이동한다
    public async void LeaveToLobby()
    {
        await ShutdownRunnerAsync();
        SceneManager.LoadScene(_lobbySceneIndex);
    }

    // 호스트가 게임 씬으로 이동한다
    public void StartGame()
    {
        if (!IsHost || PlayerCount < MaxPlayers) return;

        LobbyRoomState room = LobbyRoomState.Instance;
        if (room == null || !room.IsSpawned || !room.CanStart) return;

        HostCharacter = (CharacterType)room.HostCharacter;
        GuestCharacter = (CharacterType)room.GuestCharacter;
        Debug.Log($"[로비] 게임 시작 → 씬 전환 (P1 {HostCharacter} / P2 {GuestCharacter})");
        _runner.LoadScene(SceneRef.FromIndex(_gameSceneIndex));
    }

    // 호스트가 로비 씬으로 이동한다
    public void LoadLobbyScene()
    {
        if (IsHost) _runner.LoadScene(SceneRef.FromIndex(_lobbySceneIndex));
    }

    // 게임이 끝난 뒤 대기실로 돌아간다
    public void ReturnToWaitingRoom()
    {
        if (NetworkGameManager.Instance != null) NetworkGameManager.Instance.RequestReturnToWaitingRoom();
        else LeaveToLobby();
    }

    // 상대가 나간 뒤 호스트는 대기실로 게스트는 로비로 돌아간다
    public void ReturnAfterOpponentLeft()
    {
        if (IsHost) LoadLobbyScene();
        else LeaveToLobby();
    }

    // 러너 오브젝트를 만든다
    private NetworkRunner CreateRunner(Mode mode)
    {
        GameObject go = new GameObject($"NetworkRunner ({mode})");
        go.transform.SetParent(transform);

        NetworkRunner runner = go.AddComponent<NetworkRunner>();
        runner.AddCallbacks(this);
        _mode = mode;
        return runner;
    }

    // 현재 러너를 종료한다
    private async Task ShutdownRunnerAsync()
    {
        NetworkRunner old = _runner;
        _runner = null;
        _mode = Mode.None;
        if (old == null) return;

        old.RemoveCallbacks(this);
        await old.Shutdown();
        if (old != null) Destroy(old.gameObject);
    }

    // 방 목록이 바뀌면 화면에 알린다
    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
    {
        if (runner == _runner && _mode == Mode.Lobby)
            RoomListUpdated?.Invoke(sessionList);
    }

    // 게스트가 나가면 호스트가 상황에 맞게 처리한다
    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        if (runner != _runner || _mode != Mode.Session || !runner.IsServer) return;
        Debug.Log("[네트워크] 게스트 이탈");

        if (InGameScene)
        {
            if (GameManager.Instance != null) GameManager.Instance.HandleOpponentLeft();
        }
        else if (LobbyRoomState.Instance != null)
        {
            LobbyRoomState.Instance.ClearGuest();
        }
    }

    // 서버 연결이 끊기면 세션 종료로 처리한다
    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) =>
        HandleSessionLost(runner, $"Disconnected ({reason})");

    // 세션이 닫히면 세션 종료로 처리한다
    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) =>
        HandleSessionLost(runner, $"Session closed ({shutdownReason})");

    // 세션이 끊겼을 때 상황에 맞게 처리한다
    private void HandleSessionLost(NetworkRunner runner, string message)
    {
        if (runner != _runner || _mode == Mode.None) return;
        bool wasHost = runner.IsServer;
        Mode lostMode = _mode;
        _mode = Mode.None;
        Debug.LogWarning($"[네트워크] {message}");

        if (lostMode == Mode.Lobby || !InGameScene)
            SessionEnded?.Invoke(message);
        else if (!wasHost && GameManager.Instance != null)
            GameManager.Instance.HandleOpponentLeft();
        else
            LeaveToLobby();
    }

    // 연결 실패를 기록한다
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) =>
        Debug.LogWarning($"[네트워크] 연결 실패: {reason}");

    // 사용하지 않는 Fusion 콜백
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