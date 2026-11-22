using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class SettingsMenu : MonoBehaviour
{
    [SerializeField] private GameObject _panel;
    [SerializeField] private Slider _volume;
    [SerializeField] private TMP_Text _volumeValue;
    [SerializeField] private Slider _music;
    [SerializeField] private TMP_Text _musicValue;
    [SerializeField] private Slider _effects;
    [SerializeField] private TMP_Text _effectsValue;
    [SerializeField] private Slider _brightness;
    [SerializeField] private TMP_Text _brightnessValue;
    [SerializeField] private Button _display;
    [SerializeField] private GameObject _fullscreenLabel;
    [SerializeField] private GameObject _windowedLabel;
    [SerializeField] private Button _close;
    [SerializeField] private Button _open;
    [SerializeField] private Button _surrender;
    [SerializeField] private GameObject _surrenderLabel;
    [SerializeField] private GameObject _confirmSurrenderLabel;

    private bool _confirmingSurrender;

    public static bool IsOpen { get; private set; }

    // 버튼과 슬라이더를 연결하고 저장된 값으로 맞춘 뒤 창을 닫아 둔다
    private void Start()
    {
        _close.onClick.AddListener(Close);
        if (_display != null) _display.onClick.AddListener(ToggleDisplay);
        if (_open != null) _open.onClick.AddListener(Open);
        if (_surrender != null) _surrender.onClick.AddListener(OnSurrender);
        _volume.onValueChanged.AddListener(OnVolumeChanged);
        if (_music != null) _music.onValueChanged.AddListener(OnMusicChanged);
        if (_effects != null) _effects.onValueChanged.AddListener(OnEffectsChanged);
        _brightness.onValueChanged.AddListener(OnBrightnessChanged);
        Close();
    }

    // 장면이 바뀌어도 열린 상태가 남지 않게 한다
    private void OnDestroy() => IsOpen = false;

    // Esc로 창을 열고 닫는다
    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || !keyboard.escapeKey.wasPressedThisFrame) return;
        if (_panel.activeSelf) Close();
        else Open();
    }

    // 지금 설정값을 슬라이더에 넣고 창을 연다(게임이 끝났으면 항복은 막는다)
    public void Open()
    {
        GameSettings settings = GameSettings.Instance;
        if (settings != null)
        {
            _volume.SetValueWithoutNotify(settings.Volume);
            _brightness.SetValueWithoutNotify(settings.Brightness);
            if (_music != null) _music.SetValueWithoutNotify(settings.Music);
            if (_effects != null) _effects.SetValueWithoutNotify(settings.Effects);
        }
        ShowValue(_volumeValue, _volume.value);
        if (_music != null) ShowValue(_musicValue, _music.value);
        if (_effects != null) ShowValue(_effectsValue, _effects.value);
        ShowValue(_brightnessValue, _brightness.value);
        ShowDisplayMode(Screen.fullScreenMode != FullScreenMode.Windowed);
        SetConfirming(false);
        if (_surrender != null)
            _surrender.interactable = GameManager.Instance != null && GameManager.Instance.CurrentPhase != GamePhase.GameEnd;
        _panel.SetActive(true);
        IsOpen = true;
    }

    // 창을 닫고 바꾼 설정을 저장한다
    public void Close()
    {
        _panel.SetActive(false);
        IsOpen = false;
        SetConfirming(false);
        if (GameSettings.Instance != null) GameSettings.Instance.Save();
    }

    // 음량을 바꾼다
    private void OnVolumeChanged(float value)
    {
        if (GameSettings.Instance != null) GameSettings.Instance.SetVolume(value);
        ShowValue(_volumeValue, value);
    }

    // 배경 음악 음량을 바꾼다
    private void OnMusicChanged(float value)
    {
        if (GameSettings.Instance != null) GameSettings.Instance.SetMusic(value);
        ShowValue(_musicValue, value);
    }

    // 효과음 음량을 바꾼다
    private void OnEffectsChanged(float value)
    {
        if (GameSettings.Instance != null) GameSettings.Instance.SetEffects(value);
        ShowValue(_effectsValue, value);
    }

    // 밝기를 바꾼다
    private void OnBrightnessChanged(float value)
    {
        if (GameSettings.Instance != null) GameSettings.Instance.SetBrightness(value);
        ShowValue(_brightnessValue, value);
    }

    // 전체 화면과 창 모드를 바꾼다
    private void ToggleDisplay()
    {
        bool fullscreen = Screen.fullScreenMode == FullScreenMode.Windowed;
        Screen.fullScreenMode = fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
        ShowDisplayMode(fullscreen);
    }

    // 지금 화면 모드 글자를 보인다
    private void ShowDisplayMode(bool fullscreen)
    {
        if (_fullscreenLabel != null) _fullscreenLabel.SetActive(fullscreen);
        if (_windowedLabel != null) _windowedLabel.SetActive(!fullscreen);
    }

    // 처음 누르면 확인 글자로 바꾸고, 한 번 더 누르면 항복하고 창을 닫는다
    private void OnSurrender()
    {
        if (!_confirmingSurrender)
        {
            SetConfirming(true);
            return;
        }
        PlayerActions.Surrender();
        Close();
    }

    // 항복 버튼에 기본 글자와 확인 글자 중 무엇을 보일지 정한다
    private void SetConfirming(bool confirming)
    {
        _confirmingSurrender = confirming;
        if (_surrenderLabel != null) _surrenderLabel.SetActive(!confirming);
        if (_confirmSurrenderLabel != null) _confirmSurrenderLabel.SetActive(confirming);
    }

    // 0~1 값을 0~100 숫자로 쓴다
    private static void ShowValue(TMP_Text text, float value) => text.SetText("{0}", Mathf.RoundToInt(value * 100f));
}
