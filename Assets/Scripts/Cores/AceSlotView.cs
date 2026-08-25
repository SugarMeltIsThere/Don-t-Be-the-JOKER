using UnityEngine;
using UnityEngine.UI;

// 에이스 선택 슬롯 하나(카드 이미지 + 1/11 토글 버튼)를 감싸는 뷰.
// 카드 이미지와 버튼이 한 프리팹 안에 같이 있어서, 이름 기반 검색 없이
// 인스펙터에서 바로 연결해두면 헷갈릴 일이 없음
public class AceSlotView : MonoBehaviour
{
    [SerializeField] private Image _cardImage;
    [SerializeField] private Button _toggleButton;

    public Image CardImage => _cardImage;
    public Button ToggleButton => _toggleButton;
}
