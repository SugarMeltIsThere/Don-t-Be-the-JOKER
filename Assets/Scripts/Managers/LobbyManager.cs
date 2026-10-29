using System.Collections.Generic;
using System.Threading.Tasks;
using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 로비 UI (방 만들기/참가/대기실). 접속·방 처리는 NetworkManager, 대기실의 닉네임·준비 상태는 LobbyRoomState가 맡는다.
public class LobbyManager : MonoBehaviour
{
    public static LobbyManager Instance { get; private set; }

    private const float ErrorSeconds = 3f;
    private const int RetryDelayMs = 3000;

    [Header("Panels")]
    [SerializeField] private GameObject _connectingPanel;
    [SerializeField] private GameObject _lobbyPanel;
    [SerializeField] private GameObject _waitingPanel;

    [Header("Lobby UI")]
    [SerializeField] private TMP_InputField _roomCodeInput;
    [SerializeField] private Button _createButton;
    [SerializeField] private Button _joinButton;
    [SerializeField] private TMP_InputField _nicknameInput;

    [Header("Waiting UI")]
    [SerializeField] private TextMeshProUGUI _roomNameText;
    [SerializeField] private TextMeshProUGUI _player1Text;
    [SerializeField] private TextMeshProUGUI _player2Text;
    [SerializeField] private Button _readyButton;
    [SerializeField] private Button _startButton;

    [Header("Room List")]
    [SerializeField] private Transform _roomListContent;
    [SerializeField] private GameObject _roomItemPrefab;

    [Header("Error")]
    [SerializeField] private TextMeshProUGUI _errorText;

    [Header("Leave")]
    [SerializeField] private Button _leaveButton;

    private bool _isReady;

    private NetworkManager Net => NetworkManager.Instance;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        _createButton.onClick.AddListener(() => EnterRoom(createNew: true, _roomCodeInput.text));
        _joinButton.onClick.AddListener(() => EnterRoom(createNew: false, _roomCodeInput.text));
        _readyButton.onClick.AddListener(OnReady);
        _startButton.onClick.AddListener(OnStartGame);
        _leaveButton.onClick.AddListener(() => ConnectToLobby());

        Net.RoomListUpdated += OnRoomListUpdated;
        Net.SessionEnded += OnSessionEnded;

        // 게임을 마치고 같은 방으로 돌아온 경우에는 대기실부터 보여준다.
        if (Net.HasSession) ShowWaiting();
        else ConnectToLobby();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (Net == null) return;
        Net.RoomListUpdated -= OnRoomListUpdated;
        Net.SessionEnded -= OnSessionEnded;
    }

    private void Update()
    {
        if (_waitingPanel.activeSelf) RefreshWaiting();
    }

    // ───────────────────────── 패널 전환 ─────────────────────────
    private void ShowPanel(GameObject panel)
    {
        _connectingPanel.SetActive(panel == _connectingPanel);
        _lobbyPanel.SetActive(panel == _lobbyPanel);
        _waitingPanel.SetActive(panel == _waitingPanel);
    }

    private void ShowWaiting()
    {
        _isReady = false;
        SetReadyLabel();
        ShowPanel(_waitingPanel);
        RefreshWaiting();
    }

    // 로비(방 목록) 연결. 실패하면 잠시 뒤 다시 시도한다.
    private async void ConnectToLobby(string message = null)
    {
        ShowPanel(_connectingPanel);
        if (message != null) ShowError(message);

        while (!await Net.JoinLobbyAsync())
        {
            if (this == null) return;
            ShowError("Connection failed. Retrying...");
            await Task.Delay(RetryDelayMs);
            if (this == null) return;
        }

        if (this == null) return;
        Debug.Log("[로비] 로비 입장");
        ShowPanel(_lobbyPanel);
    }

    private void OnSessionEnded(string reason) => ConnectToLobby("Disconnected from the room.");

    // ───────────────────────── 방 만들기 / 참가 ─────────────────────────
    private async void EnterRoom(bool createNew, string roomCodeText)
    {
        string nickname = _nicknameInput.text.Trim();
        string roomCode = roomCodeText.Trim().ToUpper();

        if (string.IsNullOrEmpty(nickname)) { ShowError("Please enter a nickname."); return; }
        if (string.IsNullOrEmpty(roomCode)) { ShowError("Please enter a room code."); return; }

        Net.Nickname = nickname;
        SetBusy(true);
        bool ok = createNew ? await Net.CreateRoomAsync(roomCode) : await Net.JoinRoomAsync(roomCode);
        if (this == null) return;   // 씬이 다시 로드되었다면 새 LobbyManager가 대기실을 보여준다

        SetBusy(false);
        if (ok) ShowWaiting();
        else ShowError(createNew ? "Room already exists." : "Room not found or full.");
    }

    private void SetBusy(bool busy)
    {
        _createButton.interactable = !busy;
        _joinButton.interactable = !busy;
    }

    private void OnRoomListUpdated(List<SessionInfo> rooms)
    {
        foreach (Transform child in _roomListContent)
            Destroy(child.gameObject);

        foreach (SessionInfo room in rooms)
        {
            Transform item = Instantiate(_roomItemPrefab, _roomListContent).transform;
            item.Find("RoomNameText").GetComponent<TextMeshProUGUI>().text = room.Name;
            item.Find("PlayerCountText").GetComponent<TextMeshProUGUI>().text = $"{room.PlayerCount}/{room.MaxPlayers}";

            Button joinButton = item.Find("JoinButton").GetComponent<Button>();
            joinButton.interactable = room.IsOpen && room.PlayerCount < room.MaxPlayers;

            string roomName = room.Name;
            joinButton.onClick.AddListener(() => EnterRoom(createNew: false, roomName));
        }
    }

    // ───────────────────────── 대기실 ─────────────────────────
    private void RefreshWaiting()
    {
        LobbyRoomState room = LobbyRoomState.Instance;
        bool isHost = Net.IsHost;

        _roomNameText.text = $"Room: {Net.RoomCode}";
        _readyButton.gameObject.SetActive(!isHost);
        _startButton.gameObject.SetActive(isHost);

        _player1Text.text = $"Player 1: {(room != null ? room.HostName.ToString() : "")}";
        _player2Text.text = room != null && room.HasGuest ? $"Player 2: {room.GuestName}" : "Player 2: Waiting...";

        // 게스트가 준비를 눌러야 호스트가 시작할 수 있다
        _startButton.interactable = isHost && room != null && room.HasGuest && room.GuestReady;
    }

    private void OnReady()
    {
        _isReady = !_isReady;
        SetReadyLabel();

        if (LobbyRoomState.Instance != null)
            LobbyRoomState.Instance.SetReady(_isReady);
        Debug.Log($"[로비] 준비 상태: {_isReady}");
    }

    private void SetReadyLabel()
    {
        TextMeshProUGUI label = _readyButton.GetComponentInChildren<TextMeshProUGUI>();
        if (label != null) label.text = _isReady ? "Cancel" : "Ready";
    }

    private void OnStartGame()
    {
        Debug.Log($"[로비] 시작 버튼 클릭 / IsHost: {Net.IsHost} / PlayerCount: {Net.PlayerCount}");
        Net.StartGame();
    }

    // ───────────────────────── 오류 표시 ─────────────────────────
    private void ShowError(string message)
    {
        CancelInvoke(nameof(HideError));
        _errorText.text = message;
        _errorText.gameObject.SetActive(true);
        Invoke(nameof(HideError), ErrorSeconds);
    }

    private void HideError() => _errorText.gameObject.SetActive(false);
}