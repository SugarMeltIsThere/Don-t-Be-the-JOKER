#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System.Globalization;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PerfProbe : MonoBehaviour
{
    private const float WindowSeconds = 5f;
    private const string GameSceneName = "GameScene";

    private ProfilerRecorder _uiRecorder;
    private ProfilerRecorder _gcBytesRecorder;
    private ProfilerRecorder _gcCountRecorder;
    private ProfilerRecorder _mainThreadRecorder;

    private Window _window;
    private Window _total;
    private float _windowEndsAt;

    private struct Window
    {
        public int Frames;
        public double UiNs;
        public double GcBytes;
        public double GcCount;
        public double MainNs;

        // 한 프레임의 값을 누적한다
        public void Add(long uiNs, long gcBytes, long gcCount, long mainNs)
        {
            Frames++;
            UiNs += uiNs;
            GcBytes += gcBytes;
            GcCount += gcCount;
            MainNs += mainNs;
        }
    }

    // 개발 빌드와 에디터에서 측정기를 하나 만든다
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Create()
    {
        GameObject go = new GameObject(nameof(PerfProbe));
        DontDestroyOnLoad(go);
        go.AddComponent<PerfProbe>();
    }

    // 측정할 카운터를 연다
    private void OnEnable()
    {
        _ = PerfMarkers.UIRefresh;
        _uiRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, PerfMarkers.UIRefreshName);
        _gcBytesRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
        _gcCountRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocation In Frame Count");
        _mainThreadRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread");
        _windowEndsAt = Time.unscaledTime + WindowSeconds;

        Debug.Log($"[PERF] start ui={_uiRecorder.Valid} gcBytes={_gcBytesRecorder.Valid} gcCount={_gcCountRecorder.Valid} main={_mainThreadRecorder.Valid}");
        SceneManager.activeSceneChanged += OnSceneChanged;
    }

    // 카운터를 닫고 남은 합계를 기록한다
    private void OnDisable()
    {
        SceneManager.activeSceneChanged -= OnSceneChanged;
        FlushTotal("disable");

        _uiRecorder.Dispose();
        _gcBytesRecorder.Dispose();
        _gcCountRecorder.Dispose();
        _mainThreadRecorder.Dispose();
    }

    // 게임 씬에서 매 프레임 값을 모으고 구간마다 평균을 기록한다
    private void LateUpdate()
    {
        if (SceneManager.GetActiveScene().name != GameSceneName) return;

        long ui = _uiRecorder.Valid ? _uiRecorder.LastValue : 0;
        long gcBytes = _gcBytesRecorder.Valid ? _gcBytesRecorder.LastValue : 0;
        long gcCount = _gcCountRecorder.Valid ? _gcCountRecorder.LastValue : 0;
        long main = _mainThreadRecorder.Valid ? _mainThreadRecorder.LastValue : 0;

        _window.Add(ui, gcBytes, gcCount, main);
        _total.Add(ui, gcBytes, gcCount, main);

        if (Time.unscaledTime < _windowEndsAt) return;

        string phase = GameManager.Instance != null ? GameManager.Instance.CurrentPhase.ToString() : "-";
        Debug.Log(Format("window", phase, _window));
        _window = default;
        _windowEndsAt = Time.unscaledTime + WindowSeconds;
    }

    // 게임 씬에 들어오면 구간을 새로 시작하고 벗어나면 그동안의 합계를 기록한다
    private void OnSceneChanged(Scene from, Scene to)
    {
        if (to.name == GameSceneName) _windowEndsAt = Time.unscaledTime + WindowSeconds;
        else FlushTotal("scene-exit");
    }

    // 누적 합계를 기록하고 비운다
    private void FlushTotal(string reason)
    {
        if (_total.Frames == 0) return;
        Debug.Log(Format($"total({reason})", "-", _total));
        _total = default;
        _window = default;
    }

    // 구간 평균을 한 줄로 만든다
    private static string Format(string label, string phase, Window w)
    {
        double n = w.Frames;
        return string.Format(CultureInfo.InvariantCulture,
            "[PERF] {0} phase={1} frames={2} ui_ms={3:F4} gc_bytes={4:F1} gc_count={5:F2} main_ms={6:F3}",
            label, phase, w.Frames, w.UiNs / n / 1e6, w.GcBytes / n, w.GcCount / n, w.MainNs / n / 1e6);
    }
}
#endif
