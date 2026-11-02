using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LobbyManager : MonoBehaviour
{
    public static LobbyManager Instance { get; private set; }

    private const float ErrorSeconds = 3f;
    private const int RetryDelayMs = 3000;
    private const int CreateAttempts = 3;

    [Header("Panels")]
    [SerializeField] private GameObject _connectingPanel;
    [SerializeField] private GameObject _lobbyPanel;
    [SerializeField] private GameObject _waitingPanel;

    [Header("Lobby UI")]
    [SerializeField] private TMP_InputField _roomCodeInput;
    [SerializeField] private Button _createButton;
    [SerializeField] private Button _joinButton;
    [SerializeField] private Button _practiceButton;
    [SerializeField] private Button _quitButton;
    [SerializeField] private Button _tutorialButton;
    [SerializeField] private TMP_InputField _nicknameInput;

    [Header("Create Room Panel")]
    [SerializeField] private GameObject _createRoomPanel;
    [SerializeField] private TMP_InputField _roomTitleInput;
    [SerializeField] private Button _confirmCreateRoomButton;
    [SerializeField] private Button _cancelCreateRoomButton;

    [Header("Waiting UI")]
    [SerializeField] private TextMeshProUGUI _roomNameText;
    [SerializeField] private TextMeshProUGUI _roomCodeText;
    [SerializeField] private TextMeshProUGUI _player1Text;
    [SerializeField] private TextMeshProUGUI _player2Text;
    [SerializeField] private Button _readyButton;
    [SerializeField] private Button _cancelReadyButton;
    [SerializeField] private Button _startButton;

    [Header("Room List")]
    [SerializeField] private Transform _roomListContent;
    private readonly List<SessionInfo> _rooms = new List<SessionInfo>();
    private bool _hasRoomList;
    [SerializeField] private GameObject _roomItemPrefab;

    [Header("Error")]
    [SerializeField] private TextMeshProUGUI _errorText;

    [Header("Leave")]
    [SerializeField] private Button _leaveButton;

    [Header("Result UI")]
    [SerializeField] private GameObject _resultPanel;
    [SerializeField] private TextMeshProUGUI _resultText;
    [SerializeField] private Button _resultConfirmButton;

    [Header("Character Selection")]
    [SerializeField] private Button _kingButton;
    [SerializeField] private Button _queenButton;
    [SerializeField] private Button _jackButton;
    [SerializeField] private Button _jokerButton;

    [SerializeField] private TextMeshProUGUI _selectedCharacterText;

    private static readonly System.Random CodeRandom = new System.Random();

    private bool _isReady;
    private CharacterType? _selectedCharacter;

    private NetworkManager Net => NetworkManager.Instance;

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

    // 버튼을 연결하고 상황에 맞는 패널을 보여준다
    private void Start()
    {
        if (Net == null)
        {
            Debug.LogError("[로비] NetworkManager가 씬에 없습니다. LobbyScene에 NetworkManager 컴포넌트가 붙은 오브젝트가 활성 상태로 있어야 합니다.");
            enabled = false;
            return;
        }

        _createButton.onClick.AddListener(() => _createRoomPanel.SetActive(true));
        _confirmCreateRoomButton.onClick.AddListener(OnCreateRoom);
        _cancelCreateRoomButton.onClick.AddListener(() => _createRoomPanel.SetActive(false));
        _joinButton.onClick.AddListener(OnJoinRoom);
        _readyButton.onClick.AddListener(OnReady);
        if (_cancelReadyButton != null) _cancelReadyButton.onClick.AddListener(OnReady);
        _startButton.onClick.AddListener(OnStartGame);
        _leaveButton.onClick.AddListener(() => ConnectToLobby());
        _resultConfirmButton.onClick.AddListener(() => _resultPanel.SetActive(false));
        PracticeMode.End();
        if (_practiceButton != null) _practiceButton.onClick.AddListener(() => Net.StartPractice());
        if (_quitButton != null) _quitButton.onClick.AddListener(Application.Quit);
        if (_tutorialButton != null) _tutorialButton.onClick.AddListener(() => Net.StartPractice(tutorial: true));

        Bind(_kingButton, CharacterType.King);
        Bind(_queenButton, CharacterType.Queen);
        Bind(_jackButton, CharacterType.Jack);
        Bind(_jokerButton, CharacterType.Joker);

        _resultPanel.SetActive(false);
        _createRoomPanel.SetActive(false);
        _errorText.gameObject.SetActive(false);

        Net.RoomListUpdated += OnRoomListUpdated;
        Net.SessionEnded += OnSessionEnded;

        if (Net.HasSession) ShowWaiting();
        else ConnectToLobby();

        if (Net.PendingGameResultText != null)
        {
            _resultText.text = Net.PendingGameResultText.ToUpperInvariant();
            _resultPanel.SetActive(true);
            Net.PendingGameResultText = null;
        }
    }

    // 이벤트 구독을 해제한다
    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (Net == null) return;
        Net.RoomListUpdated -= OnRoomListUpdated;
        Net.SessionEnded -= OnSessionEnded;
    }

    // 대기실이 열려 있으면 화면을 갱신한다
    private void Update()
    {
        if (Net != null && _waitingPanel.activeSelf) RefreshWaiting();
    }

    // 캐릭터 버튼에 선택 동작을 연결한다
    private void Bind(Button button, CharacterType type)
    {
        if (button == null) { Debug.LogWarning($"[로비] {type} 버튼이 인스펙터에 연결되지 않았습니다."); return; }

        if (button is CharacterSelectButton confirmButton) confirmButton.OnConfirmSelect += () => OnCharacterSelected(type);
        else button.onClick.AddListener(() => OnCharacterSelected(type));
    }

    // 지정한 패널만 보여준다
    private void ShowPanel(GameObject panel)
    {
        _connectingPanel.SetActive(panel == _connectingPanel);
        _lobbyPanel.SetActive(panel == _lobbyPanel);
        _waitingPanel.SetActive(panel == _waitingPanel);
    }

    // 대기실을 보여주고 준비와 캐릭터 선택을 초기화한다
    private void ShowWaiting()
    {
        _isReady = false;
        _selectedCharacter = null;
        SetCharacterButtonsInteractable(true);
        RefreshSelectedCharacterText();
        ShowPanel(_waitingPanel);
        RefreshWaiting();
    }

    // 로비에 접속하고 실패하면 다시 시도한다
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

    // 세션이 끝나면 로비로 돌아간다
    private void OnSessionEnded(string reason) => ConnectToLobby("Disconnected from the room.");

    // 방 코드를 무작위로 만든다
    private static string GenerateRoomCode()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        StringBuilder sb = new StringBuilder(4);
        for (int i = 0; i < 4; i++)
            sb.Append(chars[CodeRandom.Next(chars.Length)]);
        return sb.ToString();
    }

    // 닉네임이 입력됐는지 확인한다
    private bool TryGetNickname(out string nickname)
    {
        nickname = _nicknameInput.text.Trim();
        if (!string.IsNullOrEmpty(nickname)) return true;

        ShowError("Please enter a nickname.");
        return false;
    }

    // 입력한 방 이름으로 방을 만든다
    private async void OnCreateRoom()
    {
        if (!TryGetNickname(out string nickname)) return;

        string roomTitle = _roomTitleInput.text.Trim();
        if (string.IsNullOrEmpty(roomTitle)) { ShowError("Please enter a room name."); return; }

        Net.Nickname = nickname;
        _createRoomPanel.SetActive(false);
        SetBusy(true);

        bool ok = false;
        for (int i = 0; i < CreateAttempts && !ok; i++)
        {
            ok = await Net.CreateRoomAsync(GenerateRoomCode(), roomTitle);
            if (this == null) return;
        }

        FinishEnterRoom(ok, "Room already exists.");
    }

    // 입력한 방 코드로 참가하되, 로비 방 목록에 없는 코드나 꽉 찬 방이면 접속을 시도하지 않고 안내만 한다
    private void OnJoinRoom()
    {
        string roomCode = _roomCodeInput.text.Trim().ToUpper();
        if (string.IsNullOrEmpty(roomCode))
        {
            if (TryGetNickname(out _)) ShowError("Please enter a room code.");
            return;
        }

        if (_hasRoomList)
        {
            SessionInfo room = _rooms.Find(r => r.Name == roomCode);
            if (room == null)
            {
                if (TryGetNickname(out _)) ShowError("No table with that code.");
                return;
            }
            if (!room.IsOpen || room.PlayerCount >= room.MaxPlayers)
            {
                if (TryGetNickname(out _)) ShowError("That table is full.");
                return;
            }
        }
        JoinRoom(roomCode);
    }

    // 방에 참가한다
    private async void JoinRoom(string roomCode)
    {
        if (!TryGetNickname(out string nickname)) return;

        Net.Nickname = nickname;
        SetBusy(true);
        bool ok = await Net.JoinRoomAsync(roomCode);
        if (this == null) return;

        FinishEnterRoom(ok, "Room not found or full.");
    }

    // 입장 결과에 따라 대기실이나 오류를 보여준다
    private void FinishEnterRoom(bool ok, string failMessage)
    {
        SetBusy(false);
        if (ok) ShowWaiting();
        else ShowError(failMessage);
    }

    // 입장 중에는 버튼을 막는다
    private void SetBusy(bool busy)
    {
        _createButton.interactable = !busy;
        _joinButton.interactable = !busy;
        _confirmCreateRoomButton.interactable = !busy;
    }

    // 방 목록을 기억하고 다시 그린다
    private void OnRoomListUpdated(List<SessionInfo> rooms)
    {
        _rooms.Clear();
        _rooms.AddRange(rooms);
        _hasRoomList = true;

        foreach (Transform child in _roomListContent)
            Destroy(child.gameObject);

        foreach (SessionInfo room in rooms)
        {
            Transform item = Instantiate(_roomItemPrefab, _roomListContent).transform;

            item.Find("RoomNameText").GetComponent<TextMeshProUGUI>().text = NetworkManager.ReadTitle(room, room.Name);
            item.Find("PlayerCountText").GetComponent<TextMeshProUGUI>().text = $"{room.PlayerCount}/{room.MaxPlayers}";

            Button joinButton = item.Find("JoinButton").GetComponent<Button>();
            joinButton.interactable = room.IsOpen && room.PlayerCount < room.MaxPlayers;

            string roomCode = room.Name;
            joinButton.onClick.AddListener(() => JoinRoom(roomCode));
        }
    }

    // 대기실의 이름과 버튼 상태를 갱신한다
    private void RefreshWaiting()
    {
        LobbyRoomState room = LobbyRoomState.Instance;
        bool isHost = Net.IsHost;
        bool spawned = room != null && room.IsSpawned;

        _roomNameText.text = Net.RoomTitle;
        _roomCodeText.text = Net.RoomCode;
        _player1Text.text = spawned ? room.HostName.ToString() : "";
        _player2Text.text = spawned && room.HasGuest ? room.GuestName.ToString() : "WAITING...";

        _startButton.gameObject.SetActive(isHost);
        _startButton.interactable = isHost && spawned && room.CanStart;

        if (isHost && spawned) LogStartConditions(room);

        bool hasCancel = _cancelReadyButton != null;
        _readyButton.gameObject.SetActive(!isHost && (!_isReady || !hasCancel));
        if (hasCancel) _cancelReadyButton.gameObject.SetActive(!isHost && _isReady);
        else SetReadyLabel();
        _readyButton.interactable = _selectedCharacter != null;
    }

    private string _lastStartLog;

    // 시작 조건이 바뀌면 기록한다
    private void LogStartConditions(LobbyRoomState room)
    {
        string log = $"[로비] 시작 조건 / 게스트 있음: {room.HasGuest} / 게스트 준비: {room.GuestReady} / " +
                     $"호스트 캐릭터: {room.HostCharacter} / 게스트 캐릭터: {room.GuestCharacter} (255=미선택)";
        if (log == _lastStartLog) return;
        _lastStartLog = log;
        Debug.Log(log);
    }

    // 준비 상태를 바꾼다
    private void OnReady()
    {
        _isReady = !_isReady;

        if (LobbyRoomState.Instance != null)
            LobbyRoomState.Instance.SetReady(_isReady);

        SetCharacterButtonsInteractable(!_isReady);

        Debug.Log($"[로비] 준비 상태: {_isReady}");
    }

    // 선택한 캐릭터를 저장하고 알린다
    private void OnCharacterSelected(CharacterType type)
    {
        _selectedCharacter = type;
        RefreshSelectedCharacterText();

        if (LobbyRoomState.Instance != null)
            LobbyRoomState.Instance.SelectCharacter(type);

        Debug.Log($"[로비] 캐릭터 선택: {type}");
    }

    // 선택한 캐릭터 이름을 표시한다
    private void RefreshSelectedCharacterText()
    {
        if (_selectedCharacterText != null)
            _selectedCharacterText.text = _selectedCharacter?.ToString().ToUpperInvariant() ?? "";
    }

    // 캐릭터 버튼의 사용 여부를 바꾼다
    private void SetCharacterButtonsInteractable(bool interactable)
    {
        foreach (Button b in new Button[] { _kingButton, _queenButton, _jackButton, _jokerButton })
            if (b != null) b.interactable = interactable;
    }

    // 준비 버튼의 글자를 바꾼다
    private void SetReadyLabel()
    {
        TextMeshProUGUI label = _readyButton.GetComponentInChildren<TextMeshProUGUI>();
        if (label != null) label.text = _isReady ? "CANCEL" : "READY";
    }

    // 호스트가 게임을 시작한다
    private void OnStartGame()
    {
        Debug.Log($"[로비] 시작 버튼 클릭 / IsHost: {Net.IsHost} / PlayerCount: {Net.PlayerCount}");
        Net.StartGame();
    }

    // 오류 문구를 잠시 보여준다
    private void ShowError(string message)
    {
        CancelInvoke(nameof(HideError));
        _errorText.text = message.ToUpperInvariant();
        _errorText.gameObject.SetActive(true);
        Invoke(nameof(HideError), ErrorSeconds);
    }

    // 오류 문구를 숨긴다
    private void HideError() => _errorText.gameObject.SetActive(false);
}