using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LobbyManager : MonoBehaviourPunCallbacks
{
    public static LobbyManager Instance { get; private set; }

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

    private bool _isReady = false;

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
        _connectingPanel.SetActive(true);
        _lobbyPanel.SetActive(false);
        _waitingPanel.SetActive(false);

        _createButton.onClick.AddListener(OnCreateRoom);
        _joinButton.onClick.AddListener(OnJoinRoom);
        _readyButton.onClick.AddListener(OnReady);
        _startButton.onClick.AddListener(OnStartGame);
        _leaveButton.onClick.AddListener(OnLeaveRoom);

        if (PhotonNetwork.IsConnected)
            OnConnectedToMaster();
        else
            PhotonNetwork.ConnectUsingSettings();
    }
    public override void OnRoomListUpdate(List<RoomInfo> roomList)
    {
        // 기존 방 목록 초기화
        foreach (Transform child in _roomListContent)
            Destroy(child.gameObject);

        // 방 목록 갱신
        foreach (RoomInfo room in roomList)
        {
            if (room.RemovedFromList) continue;

            GameObject item = Instantiate(_roomItemPrefab, _roomListContent);
            Transform tf = item.transform;

            tf.Find("RoomNameText").GetComponent<TextMeshProUGUI>().text
                = room.Name;
            tf.Find("PlayerCountText").GetComponent<TextMeshProUGUI>().text
                = $"{room.PlayerCount}/{room.MaxPlayers}";

            Button joinBtn = tf.Find("JoinButton").GetComponent<Button>();
            joinBtn.interactable = room.PlayerCount < room.MaxPlayers;

            string roomName = room.Name;
            joinBtn.onClick.AddListener(() =>
            {
                PhotonNetwork.NickName = _nicknameInput.text.Trim();
                PhotonNetwork.JoinRoom(roomName);
            });
        }
    }

    public override void OnConnectedToMaster()
    {
        Debug.Log("[로비] 마스터 서버 연결 성공");
        PhotonNetwork.JoinLobby();
    }

    public override void OnJoinedLobby()
    {
        Debug.Log("[로비] 로비 입장");
        _connectingPanel.SetActive(false);
        _lobbyPanel.SetActive(true);
    }

    private void OnCreateRoom()
    {
        string nickname = _nicknameInput.text.Trim();
        string roomCode = _roomCodeInput.text.Trim().ToUpper();

        if (string.IsNullOrEmpty(nickname))
        {
            ShowError("Please enter a nickname.");
            return;
        }
        if (string.IsNullOrEmpty(roomCode))
        {
            ShowError("Please enter a room code.");
            return;
        }

        PhotonNetwork.NickName = nickname; // 닉네임 설정

        RoomOptions options = new RoomOptions
        {
            MaxPlayers = 2,
            IsVisible = true,
            IsOpen = true
        };

        PhotonNetwork.CreateRoom(roomCode, options);
    }

    private void OnJoinRoom()
    {
        string nickname = _nicknameInput.text.Trim();
        string roomCode = _roomCodeInput.text.Trim().ToUpper();

        if (string.IsNullOrEmpty(nickname))
        {
            ShowError("Please enter a nickname.");
            return;
        }
        if (string.IsNullOrEmpty(roomCode))
        {
            ShowError("Please enter a room code.");
            return;
        }

        PhotonNetwork.NickName = nickname; // 닉네임 설정
        PhotonNetwork.JoinRoom(roomCode);
    }

    public override void OnJoinedRoom()
    {
        Debug.Log($"[로비] 방 입장: {PhotonNetwork.CurrentRoom.Name}");

        _lobbyPanel.SetActive(false);
        _waitingPanel.SetActive(true);

        _roomNameText.text = $"Room: {PhotonNetwork.CurrentRoom.Name}";

        bool isHost = PhotonNetwork.IsMasterClient;
        _readyButton.gameObject.SetActive(!isHost);
        _startButton.gameObject.SetActive(isHost);
        _startButton.interactable = false; // 기본 비활성

        UpdatePlayerList();
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        Debug.Log($"[로비] 플레이어 입장: {newPlayer.NickName}");
        UpdatePlayerList();
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        Debug.Log($"[로비] 플레이어 퇴장: {otherPlayer.NickName}");
        UpdatePlayerList();
        _startButton.interactable = false;
    }

    private void UpdatePlayerList()
    {
        var players = PhotonNetwork.PlayerList;

        _player1Text.text = players.Length > 0
            ? $"Player 1: {players[0].NickName}"
            : "Player 1: ";

        _player2Text.text = players.Length > 1
            ? $"Player 2: {players[1].NickName}"
            : "Player 2: Waiting...";
    }

    private void OnReady()
    {
        _isReady = !_isReady;
        _readyButton.GetComponentInChildren<TextMeshProUGUI>().text
            = _isReady ? "Cancel" : "Ready";

        // 준비 상태를 커스텀 프로퍼티로 전송
        Hashtable props = new Hashtable { { "IsReady", _isReady } };
        PhotonNetwork.LocalPlayer.SetCustomProperties(props);

        Debug.Log($"[로비] 준비 상태: {_isReady}");
    }

    public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        if (!changedProps.ContainsKey("IsReady")) return;

        // 게스트가 준비 완료하면 호스트 시작 버튼 활성화
        bool guestReady = false;
        foreach (Player player in PhotonNetwork.PlayerList)
        {
            if (player.IsMasterClient) continue;
            if (player.CustomProperties.TryGetValue("IsReady", out object val))
                guestReady = (bool)val;
        }

        _startButton.interactable = guestReady;
        Debug.Log($"[로비] 게스트 준비 상태: {guestReady}");
    }

    private void OnStartGame()
    {
        Debug.Log($"[로비] 시작 버튼 클릭 / IsMasterClient: {PhotonNetwork.IsMasterClient} / PlayerCount: {PhotonNetwork.CurrentRoom.PlayerCount}");

        if (!PhotonNetwork.IsMasterClient) return;
        if (PhotonNetwork.CurrentRoom.PlayerCount < 2) return;

        Debug.Log("[로비] 게임 시작 → 씬 전환");
        PhotonNetwork.LoadLevel("GameScene");
    }

    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        ShowError($"Room already exists.");
    }

    // OnJoinRoomFailed()에서
    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        ShowError("Room not found or full.");
    }
    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        Debug.Log($"[로비] 방장 변경 → {newMasterClient.NickName}");

        bool isHost = PhotonNetwork.IsMasterClient;
        _readyButton.gameObject.SetActive(!isHost);
        _startButton.gameObject.SetActive(isHost);
        _startButton.interactable = false;

        UpdatePlayerList();
    }

    private void ShowError(string message)
    {
        _errorText.text = message;
        _errorText.gameObject.SetActive(true);
        Invoke(nameof(HideError), 3f); // 3초 후 자동 숨김
    }

    private void HideError()
    {
        _errorText.gameObject.SetActive(false);
    }

    private void OnLeaveRoom()
    {
        PhotonNetwork.LeaveRoom();
    }

    public override void OnLeftRoom()
    {
        Debug.Log("[로비] 방 나감");
        _waitingPanel.SetActive(false);
        _lobbyPanel.SetActive(true);
    }


}