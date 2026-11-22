using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class DemoBuild
{
    private const string OutputPath = "Builds/Demo/DBtJ.exe";
    private const string DemoSymbol = "DBTJ_DEMO";

    // 개발 키(F1~F5)가 켜지고 창 포커스와 상관없이 소리가 나는 영상 촬영용 빌드를 따로 만든다(제출용 빌드 설정은 건드리지 않는다)
    [MenuItem("Tools/DBtJ/Build Demo")]
    public static void Build()
    {
        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
            locationPathName = OutputPath,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None,
            extraScriptingDefines = new[] { DemoSymbol }
        };
        BuildReport report = BuildPipeline.BuildPlayer(options);
        Debug.Log($"[시연 빌드] {report.summary.result}, {report.summary.totalSize / (1024 * 1024)} MB, {OutputPath}");
    }
}
