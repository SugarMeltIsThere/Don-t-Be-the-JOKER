using UnityEngine;
using Photon.Pun;
using Photon.Realtime;

public class NetworkManager : MonoBehaviourPunCallbacks
{
    public static NetworkManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        PhotonNetwork.AutomaticallySyncScene = true; // 추가

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
}