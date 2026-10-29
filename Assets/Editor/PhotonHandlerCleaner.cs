#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

// Play를 멈춘 뒤 Photon이 에디터 씬에 뒤늦게 만드는 ConnectionHandler / RegionHandler 오브젝트를
// 에디터 모드에서 발견하는 즉시 지운다. 반드시 Assets/Editor/ 폴더에 둔다.
[InitializeOnLoad]
public static class PhotonHandlerCleaner
{
    private static double _nextCheck;

    static PhotonHandlerCleaner()
    {
        EditorApplication.update += Tick;
    }

    private static void Tick()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (EditorApplication.timeSinceStartup < _nextCheck) return;
        _nextCheck = EditorApplication.timeSinceStartup + 0.5;

        foreach (GameObject go in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (EditorUtility.IsPersistent(go) || !go.scene.IsValid()) continue;   // 프로젝트 에셋/프리팹은 건드리지 않는다
            if (go.name != "RegionHandler" && go.name != "ConnectionHandler") continue;

            Object.DestroyImmediate(go);
        }
    }
}
#endif
