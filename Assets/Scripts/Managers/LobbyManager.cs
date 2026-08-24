using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LobbyManager : MonoBehaviourPunCallbacks
{
    public static LobbyManager Instance { get; private set; }

    [Header("Panels")]
    [SerializeField] private GameObject _connectingPanel;
    [SerializeField] private GameObject _lobbyPanel;
    [SerializeField] private GameObject _waitingPanel;

    [Header("Lobby UI")]
    [SerializeField] private TMP_InputField _roomCodeInput; // 코드로 직접 입장할 때 사용
    [SerializeField] private Button _createButton; // 방 생성 패널을 여는 버튼
    [SerializeField] private Button _joinButton; // 코드로 직접 입장
    [SerializeField] private TMP_InputField _nicknameInput;

    [Header("Create Room Panel")]
    [SerializeField] private GameObject _createRoomPanel;
    [SerializeField] private TMP_InputField _roomTitleInput;
    [SerializeField] private Button _confirmCreateRoomButton;
    [SerializeField] private Button _cancelCreateRoomButton;

    [Header("Waiting UI")]
    [SerializeField] private TextMeshProUGUI _roomNameText; // 방 제목 전용
    [SerializeField] private TextMeshProUGUI _roomCodeText; // 방 코드 전용 (별개 텍스트)
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

    [Header("Result UI")]
    [SerializeField] private GameObject _resultPanel;
    [SerializeField] private TextMeshProUGUI _resultText;
    [SerializeField] private Button _resultConfirmButton;

    [Header("Character Selection")]
    [SerializeField] private Button _kingButton;
    [SerializeField] private Button _queenButton;
    [SerializeField] private Button _jackButton;
    [SerializeField] private Button _jokerButton;
    [SerializeField] private TextMeshProUGUI _mySelectedCharacterText;

    private bool _isReady = false;
    private CharacterType? _selectedCharacter = null;

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

        _createButton.onClick.AddListener(() => _createRoomPanel.SetActive(true));
        _confirmCreateRoomButton.onClick.AddListener(OnCreateRoom);
        _cancelCreateRoomButton.onClick.AddListener(() => _createRoomPanel.SetActive(false));
        _joinButton.onClick.AddListener(OnJoinRoom);
        _readyButton.onClick.AddListener(OnReady);
        _startButton.onClick.AddListener(OnStartGame);
        _leaveButton.onClick.AddListener(OnLeaveRoom);
        _resultConfirmButton.onClick.AddListener(OnResultConfirmed);

        _kingButton.onClick.AddListener(() => OnCharacterSelected(CharacterType.King));
        _queenButton.onClick.AddListener(() => OnCharacterSelected(CharacterType.Queen));
        _jackButton.onClick.AddListener(() => OnCharacterSelected(CharacterType.Jack));
        _jokerButton.onClick.AddListener(() => OnCharacterSelected(CharacterType.Joker));

        _resultPanel.SetActive(false);
        _createRoomPanel.SetActive(false);

        if (PhotonNetwork.InRoom)
        {
            // 게임 씬에서 '로비로 돌아가기'로 돌아온 경우: 방은 유지된 상태이므로
            // 로비 재입장 절차 없이 바로 대기실로.
            // OnJoinedLobby()를 거치지 않는 경로이므로 connectingPanel을 직접 꺼야 함
            _connectingPanel.SetActive(false);

            OnJoinedRoom();
        }
        else if (PhotonNetwork.IsConnected)
        {
            OnConnectedToMaster();
        }
        else
        {
            PhotonNetwork.ConnectUsingSettings();
        }

        // 게임 종료 직후 자동 전환된 경우, NetworkManager에 남겨둔 결과를 꺼내 표시
        if (NetworkManager.Instance != null &&
            NetworkManager.Instance.PendingGameResultText != null)
        {
            _resultText.text = NetworkManager.Instance.PendingGameResultText;
            _resultPanel.SetActive(true);
            NetworkManager.Instance.PendingGameResultText = null; // 한 번만 표시
        }
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

            // 화면엔 사용자가 지은 제목(RoomTitle)을 보여주고, 실제 조인은
            // 뒤에서 고유 코드(room.Name)로 함 - 둘은 서로 다른 값
            string title = room.CustomProperties != null &&
                room.CustomProperties.TryGetValue("RoomTitle", out object titleObj)
                ? titleObj as string
                : room.Name; // 프로퍼티가 아직 안 왔을 때의 폴백

            tf.Find("RoomNameText").GetComponent<TextMeshProUGUI>().text
                = title;
            tf.Find("PlayerCountText").GetComponent<TextMeshProUGUI>().text
                = $"{room.PlayerCount}/{room.MaxPlayers}";

            Button joinBtn = tf.Find("JoinButton").GetComponent<Button>();
            joinBtn.interactable = room.PlayerCount < room.MaxPlayers;

            string roomCode = room.Name; // 조인용 실제 식별자
            joinBtn.onClick.AddListener(() =>
            {
                PhotonNetwork.NickName = _nicknameInput.text.Trim();
                PhotonNetwork.JoinRoom(roomCode);
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

    private static readonly System.Random _codeRandom = new System.Random();

    // 방 코드로 쓸 영어 대문자 6자리를 무작위로 생성.
    // Photon 방의 실제 Name(고유 식별자)으로 쓰이고, 화면에 보이는 "방 이름"과는 별개
    private static string GenerateRoomCode()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        System.Text.StringBuilder sb = new System.Text.StringBuilder(4);
        for (int i = 0; i < 4; i++)
            sb.Append(chars[_codeRandom.Next(chars.Length)]);
        return sb.ToString();
    }

    private void OnCreateRoom()
    {
        string nickname = _nicknameInput.text.Trim();
        string roomTitle = _roomTitleInput.text.Trim();

        if (string.IsNullOrEmpty(nickname))
        {
            ShowError("Please enter a nickname.");
            return;
        }
        if (string.IsNullOrEmpty(roomTitle))
        {
            ShowError("Please enter a room name.");
            return;
        }

        PhotonNetwork.NickName = nickname; // 닉네임 설정

        string roomCode = GenerateRoomCode();

        // 방 제목은 커스텀 프로퍼티로 저장. CustomRoomPropertiesForLobby에
        // 명시적으로 등록해야 로비의 방 목록(OnRoomListUpdate)에서 이 값을
        // 읽을 수 있음 - 빠뜨리면 방 목록에 제목이 하나도 안 보임
        Hashtable customProps = new Hashtable { { "RoomTitle", roomTitle } };

        RoomOptions options = new RoomOptions
        {
            MaxPlayers = 2,
            IsVisible = true,
            IsOpen = true,
            CustomRoomProperties = customProps,
            CustomRoomPropertiesForLobby = new string[] { "RoomTitle" }
        };

        PhotonNetwork.CreateRoom(roomCode, options);
        _createRoomPanel.SetActive(false);
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

        string title = PhotonNetwork.CurrentRoom.CustomProperties != null &&
            PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("RoomTitle", out object titleObj)
            ? titleObj as string
            : null;

        // 제목과 코드를 별개 텍스트로 분리해서 표시 (라벨 없이 값만)
        _roomNameText.text = string.IsNullOrEmpty(title)
            ? PhotonNetwork.CurrentRoom.Name
            : title;
        _roomCodeText.text = PhotonNetwork.CurrentRoom.Name;

        bool isHost = PhotonNetwork.IsMasterClient;
        _readyButton.gameObject.SetActive(!isHost);
        _startButton.gameObject.SetActive(isHost);
        _startButton.interactable = false; // 기본 비활성

        // 방에 들어오는 모든 경로(신규 입장/실제 재입장/로비로 돌아가기)에서
        // 항상 자신의 IsReady를 리셋. Photon이 재입장 시 이전 커스텀 프로퍼티를
        // 그대로 복원시킬 수 있어서, 여기서 리셋하지 않으면 예전에 눌러뒀던
        // "레디" 상태가 살아남아 상대가 다시 레디를 안 눌러도 시작 버튼이
        // 활성화되는 문제가 있었음
        _isReady = false;
        _readyButton.GetComponentInChildren<TextMeshProUGUI>().text = "Ready";
        _readyButton.interactable = false; // 캐릭터 선택 전엔 레디 불가
        Hashtable readyResetProps = new Hashtable { { "IsReady", false } };
        PhotonNetwork.LocalPlayer.SetCustomProperties(readyResetProps);

        // 캐릭터 선택도 매 라운드(재매칭)마다 다시 골라야 하므로 리셋
        _selectedCharacter = null;
        _mySelectedCharacterText.text = "Character: -";
        Hashtable characterResetProps = new Hashtable { { "Character", -1 } };
        PhotonNetwork.LocalPlayer.SetCustomProperties(characterResetProps);
        SetCharacterButtonsInteractable(true);

        // 이전 라운드에서 켜져 있던 GameStart 신호를 리셋
        // (재매칭 시 다음 Start 클릭에서 값 변경이 확실히 감지되도록)
        if (isHost)
        {
            Hashtable resetProps = new Hashtable { { "GameStart", false } };
            PhotonNetwork.CurrentRoom.SetCustomProperties(resetProps);
        }

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
            ? players[0].NickName
            : "";

        _player2Text.text = players.Length > 1
            ? players[1].NickName
            : "Waiting...";
    }

    private void OnReady()
    {
        _isReady = !_isReady;
        _readyButton.GetComponentInChildren<TextMeshProUGUI>().text
            = _isReady ? "Cancel" : "Ready";

        // 준비 상태를 커스텀 프로퍼티로 전송
        Hashtable props = new Hashtable { { "IsReady", _isReady } };
        PhotonNetwork.LocalPlayer.SetCustomProperties(props);

        // 레디 상태에서 캐릭터를 몰래 바꾸는 걸 막기 위해 잠금.
        // 레디 취소하면 다시 바꿀 수 있게 풀어줌
        SetCharacterButtonsInteractable(!_isReady);

        Debug.Log($"[로비] 준비 상태: {_isReady}");
    }

    private void OnCharacterSelected(CharacterType type)
    {
        _selectedCharacter = type;
        _mySelectedCharacterText.text = type.ToString();

        Hashtable props = new Hashtable { { "Character", (int)type } };
        PhotonNetwork.LocalPlayer.SetCustomProperties(props);

        // 캐릭터를 골라야만 레디를 누를 수 있음 (호스트는 이 버튼 자체가
        // 비활성 상태라 아래 줄은 사실상 게스트한테만 의미 있음)
        _readyButton.interactable = true;

        Debug.Log($"[로비] 캐릭터 선택: {type}");

        // 호스트 자신의 선택은 OnPlayerPropertiesUpdate 콜백을 거치지 않을 수 있으므로
        // (로컬 변경은 즉시 반영되지만, 명시적으로 한 번 더 확인)
        if (PhotonNetwork.IsMasterClient)
            RefreshStartButtonState();
    }

    private void SetCharacterButtonsInteractable(bool interactable)
    {
        _kingButton.interactable = interactable;
        _queenButton.interactable = interactable;
        _jackButton.interactable = interactable;
        _jokerButton.interactable = interactable;
    }

    public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        if (!changedProps.ContainsKey("IsReady") && !changedProps.ContainsKey("Character")) return;

        RefreshStartButtonState();
    }

    // 시작 조건: 호스트 본인이 캐릭터를 선택했고, 게스트가 레디 + 캐릭터 선택까지 마쳤을 때
    private void RefreshStartButtonState()
    {
        bool hostHasCharacter = _selectedCharacter != null;

        bool guestReady = false;
        bool guestHasCharacter = false;

        foreach (Player player in PhotonNetwork.PlayerList)
        {
            if (player.IsMasterClient) continue;

            if (player.CustomProperties.TryGetValue("IsReady", out object readyVal))
                guestReady = (bool)readyVal;

            if (player.CustomProperties.TryGetValue("Character", out object charVal))
                guestHasCharacter = (int)charVal >= 0;
        }

        _startButton.interactable = hostHasCharacter && guestReady && guestHasCharacter;
        Debug.Log($"[로비] 시작 가능 여부 / 호스트 캐릭터: {hostHasCharacter} / 게스트 준비: {guestReady} / 게스트 캐릭터: {guestHasCharacter}");
    }

    private void OnStartGame()
    {
        Debug.Log($"[로비] 시작 버튼 클릭 / IsMasterClient: {PhotonNetwork.IsMasterClient} / PlayerCount: {PhotonNetwork.CurrentRoom.PlayerCount}");

        if (!PhotonNetwork.IsMasterClient) return;
        if (PhotonNetwork.CurrentRoom.PlayerCount < 2) return;
        if (_selectedCharacter == null) return; // 호스트도 캐릭터 선택 필수

        Debug.Log("[로비] 게임 시작 → 씬 전환");

        // PhotonNetwork.LoadLevel 대신 커스텀 룸 프로퍼티로 직접 신호를 보냄.
        // AutomaticallySyncScene을 끈 상태이므로 각 클라이언트가 이 프로퍼티 변경을
        // OnRoomPropertiesUpdate에서 받아 스스로 씬을 로드함 (로비 복귀와 대칭되는 구조)
        Hashtable startProps = new Hashtable { { "GameStart", true } };
        PhotonNetwork.CurrentRoom.SetCustomProperties(startProps);
    }

    // 게임 시작 신호 수신: 마스터/게스트 모두 이 콜백을 통해 각자 씬을 로드
    public override void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged)
    {
        if (propertiesThatChanged.ContainsKey("GameStart") &&
            (bool)propertiesThatChanged["GameStart"])
        {
            // 신호를 소비한 즉시 false로 되돌림.
            // true 상태가 오래 남아있으면, 나중에 누군가 방에 재입장할 때
            // Photon이 기존 프로퍼티도 OnRoomPropertiesUpdate로 함께 전달하면서
            // "방금 시작 신호를 받은 것"처럼 오인되어 레디 없이 게임이 시작되는
            // 문제가 있었음
            if (PhotonNetwork.IsMasterClient)
            {
                Hashtable resetProps = new Hashtable { { "GameStart", false } };
                PhotonNetwork.CurrentRoom.SetCustomProperties(resetProps);
            }

            PhotonNetwork.IsMessageQueueRunning = false;
            SceneManager.LoadScene("GameScene");
        }
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

    // 결과 패널 확인: 방 멤버십/마스터 지위엔 전혀 영향 없는 순수 UI 닫기
    private void OnResultConfirmed()
    {
        _resultPanel.SetActive(false);
    }

    public override void OnLeftRoom()
    {
        // 에디터에서 Play를 멈추면 씬 오브젝트가 먼저 파괴된 뒤에
        // Photon의 OnLeftRoom 콜백이 뒤늦게 도착할 수 있음. 이 경우
        // this(LobbyManager 자신)나 참조 중인 패널이 이미 파괴된 상태라
        // SetActive 호출 시 MissingReferenceException이 남 (실제 플레이
        // 중 정상적으로 방을 나갈 때는 발생하지 않는, 에디터 종료 시점 한정 노이즈)
        if (this == null || _waitingPanel == null || _lobbyPanel == null) return;

        Debug.Log("[로비] 방 나감");
        _waitingPanel.SetActive(false);
        _lobbyPanel.SetActive(true);
    }


}