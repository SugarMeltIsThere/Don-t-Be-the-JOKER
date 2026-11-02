using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class GameSettings : MonoBehaviour
{
    private const string VolumeKey = "settings.volume";
    private const string BrightnessKey = "settings.brightness";
    private const float DefaultVolume = 0.8f;
    private const float DefaultBrightness = 0.5f;
    private const float MaxGammaShift = 0.35f;
    private const int VolumePriority = 100;

    public static GameSettings Instance { get; private set; }

    public float Volume { get; private set; }
    public float Brightness { get; private set; }

    private LiftGammaGain _gamma;
    private bool _focused = true;

    // 게임이 켜질 때 장면이 바뀌어도 남는 설정 오브젝트를 하나 만든다
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Create()
    {
        GameObject go = new GameObject(nameof(GameSettings));
        DontDestroyOnLoad(go);
        go.AddComponent<GameSettings>();
    }

    // 감마를 덮어쓸 전역 볼륨을 만들고 저장된 음량과 밝기를 불러와 적용한다
    private void Awake()
    {
        Instance = this;
        Volume volume = gameObject.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = VolumePriority;
        VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
        _gamma = profile.Add<LiftGammaGain>(true);
        volume.sharedProfile = profile;

        SetVolume(PlayerPrefs.GetFloat(VolumeKey, DefaultVolume));
        SetBrightness(PlayerPrefs.GetFloat(BrightnessKey, DefaultBrightness));
    }

    // 전체 음량(0~1)을 정하고 기억한다
    public void SetVolume(float value)
    {
        Volume = Mathf.Clamp01(value);
        ApplyVolume();
        PlayerPrefs.SetFloat(VolumeKey, Volume);
    }

    // 밝기(0~1, 가운데가 기본)를 감마로 바꿔 화면 중간 밝기를 올리거나 내리고 기억한다
    public void SetBrightness(float value)
    {
        Brightness = Mathf.Clamp01(value);
        _gamma.gamma.Override(new Vector4(1f, 1f, 1f, (Brightness - 0.5f) * 2f * MaxGammaShift));
        PlayerPrefs.SetFloat(BrightnessKey, Brightness);
    }

    // 창이 포커스를 잃으면 소리를 끄고 돌아오면 다시 켠다(같은 PC의 두 창이 소리를 겹쳐 내지 않게)
    private void OnApplicationFocus(bool focused)
    {
        _focused = focused;
        ApplyVolume();
    }

    // 포커스 상태에 맞춰 전체 음량을 적용한다
    private void ApplyVolume() => AudioListener.volume = _focused ? Volume : 0f;

    // 바꾼 설정을 디스크에 저장한다
    public void Save() => PlayerPrefs.Save();

    // 끌 때 설정을 저장한다
    private void OnApplicationQuit() => Save();
}
