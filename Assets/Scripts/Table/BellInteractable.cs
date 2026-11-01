using UnityEngine;

public class BellInteractable : Interactable
{
    [SerializeField] private bool _isMine = true;

    // 내 종이고 내 차례이며 스탠드할 수 있는지 확인한다(상대 종은 누를 수 없다)
    public override bool CanInteract()
    {
        GameManager gm = GameManager.Instance;
        return _isMine && gm != null && gm.CanStand && PlayerActions.IsLocalTurn;
    }

    // 스탠드를 요청한다
    public override void Interact() => PlayerActions.Stand();
}
