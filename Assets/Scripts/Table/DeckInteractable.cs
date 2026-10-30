public class DeckInteractable : Interactable
{
    // 내 차례이고 카드를 더 받을 수 있는지 확인한다
    public override bool CanInteract()
    {
        GameManager gm = GameManager.Instance;
        return gm != null && gm.CanHit && PlayerActions.IsLocalTurn;
    }

    // 카드 받기를 요청한다
    public override void Interact() => PlayerActions.Hit();
}
