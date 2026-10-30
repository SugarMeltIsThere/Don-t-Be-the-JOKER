public class BellInteractable : Interactable
{
    // 내 차례이고 스탠드할 수 있는지 확인한다
    public override bool CanInteract()
    {
        GameManager gm = GameManager.Instance;
        return gm != null && gm.CanStand && PlayerActions.IsLocalTurn;
    }

    // 스탠드를 요청한다
    public override void Interact() => PlayerActions.Stand();
}
