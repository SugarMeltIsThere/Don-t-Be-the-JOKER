using System;
using TMPro;
using UnityEngine;

public class AceToggle : Interactable
{
    [SerializeField] private TextMeshPro _value;

    private CardView _card;
    private int _cardIndex;
    private bool _isEleven;
    private Action _changed;

    public int CardIndex => _cardIndex;
    public bool IsEleven => _isEleven;

    // 따라다닐 에이스 카드, 손패 안 순번, 값이 바뀔 때 알릴 곳을 정하고 값을 1로 되돌린다
    public void Bind(CardView card, int cardIndex, Action changed)
    {
        _card = card;
        _cardIndex = cardIndex;
        _changed = changed;
        _isEleven = false;
        _value.SetText("1");
        Follow();
    }

    // 카드가 움직여도 카드 위에 붙어 있게 한다
    private void LateUpdate() => Follow();

    // 카드 위치와 수평 방향을 따라간다
    private void Follow()
    {
        if (_card == null) return;
        transform.SetPositionAndRotation(_card.transform.position, Quaternion.Euler(0f, _card.transform.eulerAngles.y, 0f));
    }

    // 에이스를 고르는 동안 언제든 누를 수 있다
    public override bool CanInteract() => _card != null;

    // 1과 11을 바꾸고 값을 알린다
    public override void Interact()
    {
        _isEleven = !_isEleven;
        _value.SetText(_isEleven ? "11" : "1");
        _changed?.Invoke();
        PlayerActions.SetAce(_cardIndex, _isEleven);
    }
}
