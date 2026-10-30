public class DeckInteractable : Interactable
{
    // 내 차례이고 카드를 더 받을 수 있는지 확인한다
    public override bool CanInteract()
    {
        GameManager gm = GameManager.Instance;
        return gm != null && gm.CanHit && IsLocalTurn(gm);
    }

    // 카드 받기를 요청한다
    public override void Interact()
    {
        if (NetworkGameManager.Instance != null)
            NetworkGameManager.Instance.RequestHit();
        else
            GameManager.Instance.OnHit();
    }
}
