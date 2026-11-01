using System;
using TMPro;
using UnityEngine;

public class TablePlaque : Interactable
{
    [SerializeField] private TextMeshPro _label;
    [SerializeField] private TextMeshPro _altLabel;
    [SerializeField] private TextMeshPro _cancelLabel;
    [SerializeField] private Color _availableColor = new Color(0.12f, 0.08f, 0.04f);
    [SerializeField] private Color _unavailableColor = new Color(0.12f, 0.08f, 0.04f, 0.3f);
    [SerializeField] private string _altStatePrompt;
    [SerializeField] private string _cancelStatePrompt;

    private Action _onClick;
    private bool _available;
    private bool _showingAlt;
    private bool _showingCancel;

    public override string Prompt =>
        _showingCancel && !string.IsNullOrEmpty(_cancelStatePrompt) ? _cancelStatePrompt :
        _showingAlt && !string.IsNullOrEmpty(_altStatePrompt) ? _altStatePrompt : base.Prompt;

    // 눌렀을 때 할 행동을 정한다
    public void Init(Action onClick) => _onClick = onClick;

    // 지금 누를 수 있는지 정하고 글자를 진하게 또는 흐리게 한다
    public void SetAvailable(bool available)
    {
        _available = available;
        Color color = available ? _availableColor : _unavailableColor;
        _label.color = color;
        if (_altLabel != null) _altLabel.color = color;
        if (_cancelLabel != null) _cancelLabel.color = color;
    }

    // 기본 글자 대신 다른 글자(예: RAISE 대신 CONFIRM)를 보일지, 그중에서도 취소 글자(CANCEL)를 보일지 정한다
    public void ShowAlt(bool alt, bool cancel = false)
    {
        _showingAlt = alt;
        _showingCancel = alt && cancel && _cancelLabel != null;
        _label.gameObject.SetActive(!alt || _altLabel == null);
        if (_altLabel != null) _altLabel.gameObject.SetActive(alt && !_showingCancel);
        if (_cancelLabel != null) _cancelLabel.gameObject.SetActive(_showingCancel);
    }

    // 누를 수 있고 보이는 동안에만 상호작용한다
    public override bool CanInteract() => _available && gameObject.activeInHierarchy;

    // 정해 둔 행동을 한다
    public override void Interact() => _onClick?.Invoke();
}
