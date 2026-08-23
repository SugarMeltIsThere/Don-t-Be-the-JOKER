using UnityEngine;
using UnityEngine.SceneManagement;
using Photon.Pun;
using Photon.Realtime;

public class NetworkManager : MonoBehaviourPunCallbacks
{
    public static NetworkManager Instance { get; private set; }

    // 게임 종료 결과를 GameScene → LobbyScene으로 들고 가기 위한 임시 저장소.
    // GameManager(씬 로컬)는 씬 전환 시 사라지므로, 항상 씬을 넘나드는
    // 여기에 잠깐 담아뒀다가 LobbyManager가 대기실 진입 시 꺼내서 표시하고 비움.
    public string PendingGameResultText { get; set; } = null;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // PUN의 자동 씬 동기화는 마스터의 모든 씬 로드(호출 방식 무관)를 감지해
        // 방 전체에 강제로 전파시킴 - "로비로 돌아가기"의 개별 동작 원칙과 충돌하므로 끔.
        // 게임 시작 동기화는 LobbyManager에서 커스텀 룸 프로퍼티로 직접 제어.
        PhotonNetwork.AutomaticallySyncScene = false;

        // 씬 전환 중에는 네트워크 메시지 큐를 잠시 멈춰서, 이전/새 씬의
        // PhotonView가 아직 준비되지 않은 애매한 타이밍에 들어오는 이벤트가
        // 존재하지 않는 ViewID를 참조하다 에러가 나는 것을 방지
        SceneManager.sceneLoaded += OnSceneLoadedResumeQueue;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoadedResumeQueue;
    }

    private void OnSceneLoadedResumeQueue(Scene scene, LoadSceneMode mode)
    {
        PhotonNetwork.IsMessageQueueRunning = true;
    }

    private void Start()
    {

    }

    public override void OnConnectedToMaster()
    {
        Debug.Log("[네트워크] 마스터 서버 연결 성공!");
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        Debug.Log($"[네트워크] 연결 끊김: {cause}");
    }

    // 방을 나갔을 때: 게임 씬에서 나간 경우에만 로비 씬으로 전환
    // (로비 씬 안에서 나간 경우는 LobbyManager가 자체적으로 패널 전환 처리)
    public override void OnLeftRoom()
    {
        string currentScene = SceneManager.GetActiveScene().name;

        if (currentScene != "LobbyScene")
        {
            Debug.Log("[네트워크] 게임 씬에서 방 나감 → 로비 씬으로 전환");
            PhotonNetwork.IsMessageQueueRunning = false;
            SceneManager.LoadScene("LobbyScene");
        }
    }

    // 상대방이 방을 나갔을 때: 게임 씬(대전 중)이면 매치를 더 이상
    // 지속할 수 없으므로 즉시 종료 처리. 로비 씬(대기실)에서는
    // LobbyManager.OnPlayerLeftRoom()이 이미 목록 갱신을 처리하므로 무시.
    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        string currentScene = SceneManager.GetActiveScene().name;

        if (currentScene != "LobbyScene")
        {
            Debug.Log($"[네트워크] 상대방({otherPlayer.NickName}) 게임 중 퇴장 → 매치 강제 종료");
            GameManager.Instance?.HandleOpponentLeft();
        }
    }
}