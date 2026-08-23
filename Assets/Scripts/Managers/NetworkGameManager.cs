using UnityEngine;
using Photon.Pun;
using Photon.Realtime;

public class NetworkGameManager : MonoBehaviourPun
{
    public static NetworkGameManager Instance { get; private set; }

    // 내가 P1인지 P2인지
    public ActivePlayer LocalPlayer { get; private set; }

    // ===================================================================
    // Unity 라이프사이클 / 게임 시작 동기화
    // ===================================================================

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
        // 방장 = P1, 게스트 = P2
        LocalPlayer = PhotonNetwork.IsMasterClient
            ? ActivePlayer.Player1
            : ActivePlayer.Player2;

        Debug.Log($"[네트워크] 나는 {LocalPlayer}");

        if (PhotonNetwork.IsMasterClient)
        {
            CharacterType p1Type = GetSelectedCharacter(ActivePlayer.Player1);
            CharacterType p2Type = GetSelectedCharacter(ActivePlayer.Player2);

            GameManager.Instance.InitializeGame(p1Type, p2Type);
            SyncGameStart();
        }
    }

    // 대기실에서 각자 골라둔 캐릭터를 Photon 커스텀 프로퍼티에서 읽어옴.
    // 이미 방 전체에 자동 복제되는 값이라 별도 RPC 없이 양쪽 다 바로 조회 가능
    private CharacterType GetSelectedCharacter(ActivePlayer player)
    {
        foreach (Player p in PhotonNetwork.PlayerList)
        {
            bool isMaster = p.IsMasterClient;
            bool isTarget = (player == ActivePlayer.Player1 && isMaster) ||
                             (player == ActivePlayer.Player2 && !isMaster);

            if (!isTarget) continue;

            if (p.CustomProperties.TryGetValue("Character", out object val) && (int)val >= 0)
                return (CharacterType)(int)val;
        }

        // 못 찾았을 때(이론상 Start 버튼 게이팅으로 발생하지 않아야 함)의 안전한 기본값
        Debug.LogWarning($"[네트워크] {player}의 캐릭터 선택을 찾지 못해 King으로 대체");
        return CharacterType.King;
    }

    private void SyncGameStart()
    {
        // 공유카드 모두에게 전송
        Card shared = GameManager.Instance.SharedCard;
        photonView.RPC("RPC_ReceiveSharedCard",
            RpcTarget.All,
            (int)shared.Rank,
            (int)shared.Suit);

        // 선공 결정 결과 전송
        photonView.RPC("RPC_SyncFirstTurn",
            RpcTarget.All,
            (int)GameManager.Instance.CurrentPlayer);

        // 각자 개인카드 전송
        // 주의: RPC_ReceiveSharedCard(InitializeForGuest)가 게스트의 손패를
        // 완전히 새로 초기화(리셋)하기 때문에, StartNewRound()에서 이미 한 번
        // 채워졌던 손패가 여기서 지워짐. 그래서 이 호출이 중복처럼 보여도
        // 실제로는 리셋된 손패를 다시 채우기 위해 반드시 필요함 (첫 라운드에만 발생)
        SyncPrivateCards();
    }

    public void SyncPrivateCards()
    {
        if (!PhotonNetwork.IsMasterClient) return;

        // 예전엔 각자 자기 카드만 받았는데, 그러면 상대방의 초기 카드를
        // 아예 모르는 상태로 시작하게 되어 히트할 때마다 표시가 한 칸씩
        // 밀리는 버그가 있었음. 이제 양쪽 다 두 플레이어의 초기 카드를 전부
        // 받되(히트 결과처럼), 어느 플레이어 카드인지 명시해서 올바른 손에 추가.
        var p1Cards = GameManager.Instance.GetPlayer1Cards();
        foreach (var slot in p1Cards)
        {
            photonView.RPC("RPC_SyncInitialCard",
                RpcTarget.All,
                (int)ActivePlayer.Player1,
                (int)slot.Card.Rank,
                (int)slot.Card.Suit);
        }

        var p2Cards = GameManager.Instance.GetPlayer2Cards();
        foreach (var slot in p2Cards)
        {
            photonView.RPC("RPC_SyncInitialCard",
                RpcTarget.All,
                (int)ActivePlayer.Player2,
                (int)slot.Card.Rank,
                (int)slot.Card.Suit);
        }
    }

    [PunRPC]
    private void RPC_ReceiveSharedCard(int rank, int suit)
    {
        Debug.Log($"[네트워크] 공유카드 수신: {(Rank)rank} of {(Suit)suit}");

        if (!PhotonNetwork.IsMasterClient)
        {
            // 게스트: 공유카드 설정 및 PlayerHand 초기화
            CharacterType p1Type = GetSelectedCharacter(ActivePlayer.Player1);
            CharacterType p2Type = GetSelectedCharacter(ActivePlayer.Player2);

            GameManager.Instance.InitializeForGuest(
                new Card((Rank)rank, (Suit)suit), p1Type, p2Type);
        }
    }

    [PunRPC]
    private void RPC_SyncFirstTurn(int player)
    {
        Debug.Log($"[네트워크] 선공: {(ActivePlayer)player}");

        if (!PhotonNetwork.IsMasterClient)
            GameManager.Instance.SetFirstTurn((ActivePlayer)player);
    }

    [PunRPC]
    private void RPC_SyncInitialCard(int player, int rank, int suit)
    {
        Debug.Log($"[네트워크] 초기 카드 수신: {(ActivePlayer)player} / {(Rank)rank} of {(Suit)suit}");

        // 마스터는 이미 자기 authoritative 데이터로 양쪽 패를 갖고 있으니
        // 중복 추가하지 않도록 게스트만 처리
        if (!PhotonNetwork.IsMasterClient)
        {
            GameManager.Instance.ReceiveMyCard(
                (ActivePlayer)player,
                new Card((Rank)rank, (Suit)suit));
        }
    }
    // ===================================================================
    // 드로우 단계 (히트 / 스탠드)
    // ===================================================================

    // 클라이언트 → 마스터로 히트 요청
    public void RequestHit()
    {
        photonView.RPC("RPC_RequestHit", RpcTarget.MasterClient,
            (int)LocalPlayer);
    }

    // 클라이언트 → 마스터로 스탠드 요청
    public void RequestStand()
    {
        photonView.RPC("RPC_RequestStand", RpcTarget.MasterClient,
            (int)LocalPlayer);
    }

    [PunRPC]
    private void RPC_RequestHit(int player)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        GameManager.Instance.OnHit();
        // 결과 동기화는 다음 단계에서
    }

    [PunRPC]
    private void RPC_RequestStand(int player)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        GameManager.Instance.OnStand();
        // 결과 동기화는 다음 단계에서
    }

    // 마스터 → 모두: 게임 상태 동기화
    public void BroadcastGameState()
    {
        if (!PhotonNetwork.IsMasterClient) return;

        GameManager gm = GameManager.Instance;

        photonView.RPC("RPC_SyncGameState",
            RpcTarget.All,
            (int)gm.CurrentPhase,
            (int)gm.CurrentPlayer,
            gm.Player1Chips,
            gm.Player2Chips,
            gm.Pot,
            gm.IsFirstBet,
            gm.Player1TotalBet,
            gm.Player2TotalBet);
    }

    [PunRPC]
    private void RPC_SyncGameState(
        int phase, int player, int p1Chips, int p2Chips, int pot,
        bool isFirstBet, int p1TotalBet, int p2TotalBet)
    {
        Debug.Log($"[네트워크] 상태 동기화: {(GamePhase)phase} / {(ActivePlayer)player}");

        if (!PhotonNetwork.IsMasterClient)
        {
            GameManager.Instance.SyncState(
                (GamePhase)phase,
                (ActivePlayer)player,
                p1Chips,
                p2Chips,
                pot,
                isFirstBet,
                p1TotalBet,
                p2TotalBet);
        }
    }

    // 마스터 → 모두: 히트 결과 (공개 카드) 동기화
    public void BroadcastHitResult(ActivePlayer player, int rank, int suit, bool isHidden)
    {
        if (!PhotonNetwork.IsMasterClient) return;

        photonView.RPC("RPC_SyncHitResult",
            RpcTarget.All,
            (int)player,
            rank,
            suit,
            isHidden);
    }

    [PunRPC]
    private void RPC_SyncHitResult(int player, int rank, int suit, bool isHidden)
    {
        Debug.Log($"[네트워크] 히트 결과: {(ActivePlayer)player} / {(Rank)rank} of {(Suit)suit}");

        if (!PhotonNetwork.IsMasterClient)
        {
            GameManager.Instance.SyncHitResult(
                (ActivePlayer)player,
                (Rank)rank,
                (Suit)suit,
                isHidden);
        }
    }

    // ===================================================================
    // 에이스 선택 단계
    // ===================================================================

    // 클라이언트 → 마스터: 에이스 값 변경 요청
    public void RequestAceValueChanged(ActivePlayer player, int cardIndex, bool isEleven)
    {
        photonView.RPC("RPC_RequestAceValueChanged", RpcTarget.MasterClient,
            (int)player, cardIndex, isEleven);
    }

    // 클라이언트 → 마스터: 에이스 확정 요청
    public void RequestAceConfirmed(ActivePlayer player)
    {
        photonView.RPC("RPC_RequestAceConfirmed", RpcTarget.MasterClient,
            (int)player);
    }

    [PunRPC]
    private void RPC_RequestAceValueChanged(int player, int cardIndex, bool isEleven)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        GameManager.Instance.OnAceValueChanged(
            (ActivePlayer)player, cardIndex, isEleven);

        // 게스트의 로컬 미러 데이터에도 반영 (안 하면 게스트 쪽에서
        // 에이스가 낀 패의 합계가 항상 1로만 계산되어 표시가 완전히 어긋남)
        photonView.RPC("RPC_SyncAceValueChanged", RpcTarget.Others,
            player, cardIndex, isEleven);
    }

    [PunRPC]
    private void RPC_SyncAceValueChanged(int player, int cardIndex, bool isEleven)
    {
        if (!PhotonNetwork.IsMasterClient)
            GameManager.Instance.OnAceValueChanged(
                (ActivePlayer)player, cardIndex, isEleven);
    }

    [PunRPC]
    private void RPC_RequestAceConfirmed(int player)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        GameManager.Instance.OnAceSelectionConfirmed((ActivePlayer)player);

        // 게스트에게도 "이 플레이어는 에이스 선택을 확정했다"만 가볍게 알림.
        // OnAceSelectionConfirmed 전체를 게스트에서 다시 실행하면 마스터 전용
        // 단계 전환/베팅 시작 로직까지 게스트 로컬에서 중복 실행되므로,
        // 합계 계산에 필요한 확정 플래그만 별도로 동기화
        photonView.RPC("RPC_SyncAceConfirmed", RpcTarget.Others, player);
    }

    [PunRPC]
    private void RPC_SyncAceConfirmed(int player)
    {
        if (!PhotonNetwork.IsMasterClient)
            GameManager.Instance.SyncAceConfirmed((ActivePlayer)player);
    }

    // 마스터 → 모두: 에이스 선택 단계 시작
    public void BroadcastAceSelectionStart()
    {
        if (!PhotonNetwork.IsMasterClient) return;
        photonView.RPC("RPC_AceSelectionStart", RpcTarget.All);
    }

    [PunRPC]
    private void RPC_AceSelectionStart()
    {
        Debug.Log("[네트워크] 에이스 선택 단계 시작");
        if (!PhotonNetwork.IsMasterClient)
            GameManager.Instance.SyncState(
                GamePhase.AceSelection,
                ActivePlayer.None,
                GameManager.Instance.Player1Chips,
                GameManager.Instance.Player2Chips,
                GameManager.Instance.Pot,
                false,
                0,
                0);
    }

    // ===================================================================
    // 베팅 단계
    // ===================================================================

    // 클라이언트 → 마스터: 콜 요청
    public void RequestRaise(int amount)
    {
        photonView.RPC("RPC_RequestRaise", RpcTarget.MasterClient, amount);
    }
    public void RequestCall()
    {
        photonView.RPC("RPC_RequestCall", RpcTarget.MasterClient);
    }

    // 클라이언트 → 마스터: 다이 요청
    public void RequestDie()
    {
        photonView.RPC("RPC_RequestDie", RpcTarget.MasterClient);
    }

    [PunRPC]
    private void RPC_RequestRaise(int amount)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        GameManager.Instance.OnRaise(amount);
        BroadcastGameState();
    }

    [PunRPC]
    private void RPC_RequestCall()
    {
        if (!PhotonNetwork.IsMasterClient) return;
        GameManager.Instance.OnCall();
        BroadcastGameState();
    }

    [PunRPC]
    private void RPC_RequestDie()
    {
        if (!PhotonNetwork.IsMasterClient) return;
        GameManager.Instance.OnDie();
        BroadcastGameState();
    }

    // 마스터 → 모두: 베팅 단계 시작
    [PunRPC]
    private void RPC_BettingStart(int player, int p1Chips, int p2Chips, int pot)
    {
        Debug.Log($"[네트워크] 베팅 단계 시작: {(ActivePlayer)player}");
        if (!PhotonNetwork.IsMasterClient)
            GameManager.Instance.SyncState(
                GamePhase.Betting,
                (ActivePlayer)player,
                p1Chips,
                p2Chips,
                pot,
                true,
                1,  // 기본 베팅 각 1개
                1);
    }

    public void BroadcastBettingStart()
    {
        if (!PhotonNetwork.IsMasterClient) return;

        GameManager gm = GameManager.Instance;

        photonView.RPC("RPC_BettingStart",
            RpcTarget.All,
            (int)gm.CurrentPlayer,
            gm.Player1Chips,
            gm.Player2Chips,
            gm.Pot);
    }
    // ===================================================================
    // 패 공개 / 라운드 종료 / 다음 라운드 / 게임 종료
    // ===================================================================

    // 마스터 → 게스트: 마지막 카드 공개 신호 (베팅 종료 직후, 정산 전)
    public void BroadcastRoundReveal()
    {
        if (!PhotonNetwork.IsMasterClient) return;
        photonView.RPC("RPC_RoundReveal", RpcTarget.Others);
    }

    [PunRPC]
    private void RPC_RoundReveal()
    {
        if (!PhotonNetwork.IsMasterClient)
            GameManager.Instance.RevealBothHandsLastCard();
    }

    public void BroadcastRoundEnd(ActivePlayer winner, int p1Chips, int p2Chips)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        photonView.RPC("RPC_RoundEnd", RpcTarget.All,
            (int)winner, p1Chips, p2Chips);
    }
    [PunRPC]
    private void RPC_RoundEnd(int winner, int p1Chips, int p2Chips)
    {
        Debug.Log($"[네트워크] 라운드 종료: {(ActivePlayer)winner}");
        if (!PhotonNetwork.IsMasterClient)
        {
            GameManager.Instance.SyncRoundEnd(
                (ActivePlayer)winner, p1Chips, p2Chips);
        }
    }

    // 마스터 → 모두: 다음 라운드 시작 동기화
    public void BroadcastNextRound(int sharedCardRank, int sharedCardSuit,
        int firstPlayer, int p1Chips, int p2Chips, int pot)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        photonView.RPC("RPC_NextRound", RpcTarget.All,
            sharedCardRank, sharedCardSuit, firstPlayer, p1Chips, p2Chips, pot);
    }

    [PunRPC]
    private void RPC_NextRound(int sharedCardRank, int sharedCardSuit,
        int firstPlayer, int p1Chips, int p2Chips, int pot)
    {
        Debug.Log($"[네트워크] 다음 라운드 시작");
        if (!PhotonNetwork.IsMasterClient)
        {
            GameManager.Instance.SyncNextRound(
                new Card((Rank)sharedCardRank, (Suit)sharedCardSuit),
                (ActivePlayer)firstPlayer,
                p1Chips,
                p2Chips,
                pot);
        }
    }

    // 마스터 → 모두: 게임 종료 (최종 칩 값도 함께 전달)
    public void BroadcastGameEnd(ActivePlayer winner, int p1Chips, int p2Chips)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        photonView.RPC("RPC_GameEnd", RpcTarget.All,
            (int)winner, p1Chips, p2Chips);
    }

    [PunRPC]
    private void RPC_GameEnd(int winner, int p1Chips, int p2Chips)
    {
        Debug.Log($"[네트워크] 게임 종료: {(ActivePlayer)winner}");
        if (!PhotonNetwork.IsMasterClient)
            GameManager.Instance.SyncGameEnd((ActivePlayer)winner, p1Chips, p2Chips);
    }

    // 마스터 → 게스트: 조커 드로우 결과 (페널티 반영된 최종 칩 + 라운드 종료 상태)
    // 예전엔 이 이벤트 자체가 게스트에게 전혀 전달되지 않아서, 조커가
    // 나오면 게스트 화면이 Draw 단계에 멈춘 채로 완전히 동기화가 끊겼음
    public void BroadcastJokerDraw(ActivePlayer player, int p1Chips, int p2Chips)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        photonView.RPC("RPC_SyncJokerDraw", RpcTarget.Others,
            (int)player, p1Chips, p2Chips);
    }

    [PunRPC]
    private void RPC_SyncJokerDraw(int player, int p1Chips, int p2Chips)
    {
        if (!PhotonNetwork.IsMasterClient)
            GameManager.Instance.SyncJokerDraw((ActivePlayer)player, p1Chips, p2Chips);
    }

    // ===================================================================
    // 아이템
    // ===================================================================

    // 클라이언트 → 마스터: 돋보기 사용 요청
    public void RequestUseMagnifier(ActivePlayer player)
    {
        photonView.RPC("RPC_RequestUseMagnifier", RpcTarget.MasterClient, (int)player);
    }

    [PunRPC]
    private void RPC_RequestUseMagnifier(int player)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        GameManager.Instance.OnUseMagnifier((ActivePlayer)player);
    }

    // 마스터 → 모두: 돋보기 결과 (본인 요청 건만 로컬에서 필터링해 표시)
    public void BroadcastMagnifierResult(ActivePlayer player, int rank, int suit)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        photonView.RPC("RPC_SyncMagnifierResult", RpcTarget.All,
            (int)player, rank, suit);
    }

    [PunRPC]
    private void RPC_SyncMagnifierResult(int player, int rank, int suit)
    {
        if ((ActivePlayer)player != LocalPlayer) return; // 요청한 본인만 표시

        UIManager.Instance?.ShowMagnifierResult(new Card((Rank)rank, (Suit)suit));
    }

    // 클라이언트 → 마스터: 뇌물(거래) 사용 요청
    public void RequestUseBribe(ActivePlayer player)
    {
        photonView.RPC("RPC_RequestUseBribe", RpcTarget.MasterClient, (int)player);
    }

    [PunRPC]
    private void RPC_RequestUseBribe(int player)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        GameManager.Instance.OnUseBribe((ActivePlayer)player);
    }

    // 마스터 → 게스트: 뇌물로 교체된 카드 동기화
    public void BroadcastBribeResult(ActivePlayer player, int rank, int suit)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        photonView.RPC("RPC_SyncBribeResult", RpcTarget.Others,
            (int)player, rank, suit);
    }

    [PunRPC]
    private void RPC_SyncBribeResult(int player, int rank, int suit)
    {
        if (!PhotonNetwork.IsMasterClient)
            GameManager.Instance.SyncBribeResult(
                (ActivePlayer)player, (Rank)rank, (Suit)suit);
    }

    // 클라이언트 → 마스터: 플래쉬 사용 요청
    public void RequestUseFlash(ActivePlayer player)
    {
        photonView.RPC("RPC_RequestUseFlash", RpcTarget.MasterClient, (int)player);
    }

    [PunRPC]
    private void RPC_RequestUseFlash(int player)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        GameManager.Instance.OnUseFlash((ActivePlayer)player);
    }

    // 마스터 → 게스트: 양쪽 아이템 인벤토리 전체 동기화
    public void BroadcastItemState()
    {
        if (!PhotonNetwork.IsMasterClient) return;

        var p1Items = GameManager.Instance.Player1Items;
        var p2Items = GameManager.Instance.Player2Items;

        int[] p1Types = new int[p1Items.Count];
        for (int i = 0; i < p1Items.Count; i++)
            p1Types[i] = (int)p1Items[i].Type;

        int[] p2Types = new int[p2Items.Count];
        for (int i = 0; i < p2Items.Count; i++)
            p2Types[i] = (int)p2Items[i].Type;

        photonView.RPC("RPC_SyncItemState", RpcTarget.Others, p1Types, p2Types);
    }

    [PunRPC]
    private void RPC_SyncItemState(int[] p1Types, int[] p2Types)
    {
        if (!PhotonNetwork.IsMasterClient)
            GameManager.Instance.SyncItemState(p1Types, p2Types);
    }

    // 마스터 → 게스트: 아이템 폐기 단계 진입 (칩 변화 포함)
    public void BroadcastItemDiscardStart(
        int pendingPlayer, int pendingItemType, int p1Chips, int p2Chips)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        photonView.RPC("RPC_ItemDiscardStart", RpcTarget.Others,
            pendingPlayer, pendingItemType, p1Chips, p2Chips);
    }

    [PunRPC]
    private void RPC_ItemDiscardStart(
        int pendingPlayer, int pendingItemType, int p1Chips, int p2Chips)
    {
        if (!PhotonNetwork.IsMasterClient)
            GameManager.Instance.SyncItemDiscardStart(
                (ActivePlayer)pendingPlayer, (ItemType)pendingItemType,
                p1Chips, p2Chips);
    }

    // 클라이언트 → 마스터: 아이템 폐기 선택 요청
    // (실제 대상 검증은 마스터의 GameManager.OnItemDiscard 내부에서
    //  player != _pendingItemPlayer 체크로 이루어짐)
    public void RequestItemDiscard(ActivePlayer player, int discardIndex)
    {
        photonView.RPC("RPC_RequestItemDiscard", RpcTarget.MasterClient,
            (int)player, discardIndex);
    }

    [PunRPC]
    private void RPC_RequestItemDiscard(int player, int discardIndex)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        GameManager.Instance.OnItemDiscard((ActivePlayer)player, discardIndex);
    }
}