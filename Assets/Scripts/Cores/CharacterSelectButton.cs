using System;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 일반 Button과 똑같이 동작하지만 딱 두 가지만 다름.
//
// 1) 유니티가 자동으로 관리하는 Selected 상태 전환을 무시함.
//    Selected 스프라이트는 오직 LobbyManager의 _selectedCharacter 값을
//    기준으로 코드가 직접 결정하고, EventSystem이 자체적으로 판단하는
//    "선택됨" 개념과는 완전히 분리됨. Highlighted/Pressed/Disabled는
//    그대로 유니티가 자동 처리하므로 기존 그래픽은 그대로 살아있음.
//
// 2) onClick(=반드시 버튼 위에서 손을 떼야 발동) 대신, 손을 떼는 순간
//    (드래그해서 버튼 밖으로 나간 채 떼도) 무조건 OnConfirmSelect가 발동함.
//    "누른 시점의 캐릭터를, 손을 떼는 순간 확정"하는 규칙과 일치.
public class CharacterSelectButton : Button
{
    public event Action OnConfirmSelect;

    protected override void DoStateTransition(SelectionState state, bool instant)
    {
        if (state == SelectionState.Selected)
            state = SelectionState.Normal;

        base.DoStateTransition(state, instant);
    }

    public override void OnPointerUp(PointerEventData eventData)
    {
        base.OnPointerUp(eventData);

        if (eventData.button == PointerEventData.InputButton.Left &&
            IsActive() && IsInteractable())
        {
            OnConfirmSelect?.Invoke();
        }
    }
}
