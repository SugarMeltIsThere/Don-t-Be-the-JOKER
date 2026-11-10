using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class SoundSetup
{
    private const string AudioFolder = "Assets/Audio";

    // 효과음 들여오기 설정을 맞추고, 열린 장면의 SoundManager와 메뉴 버튼에 소리를 연결한다(게임 장면과 로비 장면에서 각각 실행)
    [MenuItem("Tools/DBtJ/Assign Sounds")]
    public static void Assign()
    {
        ApplyImportSettings();

        SoundManager manager = Object.FindFirstObjectByType<SoundManager>(FindObjectsInactive.Include);
        if (manager != null)
        {
            SerializedObject so = new SerializedObject(manager);
            SetClips(so, "_deal", "Cards/card-slide-");
            SetClips(so, "_flip", "Cards/card-place-");
            SetClips(so, "_chip", "Chips/chips-stack-");
            SetClips(so, "_bell", "Table/desk-bell");
            SetClips(so, "_flashClick", "Table/metalClick");
            SetClips(so, "_flashRing", null);
            SetClips(so, "_whoosh", "Cards/card-shove-");
            SetClips(so, "_magnifier", "Table/cloth");
            SetClips(so, "_tick", "UI/tick_");
            SetClips(so, "_joker", "Table/impactPunch_heavy_");
            so.FindProperty("_ambience").objectReferenceValue = null;
            if (so.FindProperty("_turnTimer").objectReferenceValue == null)
                so.FindProperty("_turnTimer").objectReferenceValue = Object.FindFirstObjectByType<TurnTimer>(FindObjectsInactive.Include);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        AudioClip hover = Load("UI/select_001");
        AudioClip click = Load("UI/click_002");
        foreach (MenuButtonFx fx in Object.FindObjectsByType<MenuButtonFx>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            SerializedObject so = new SerializedObject(fx);
            so.FindProperty("_hoverSound").objectReferenceValue = hover;
            so.FindProperty("_clickSound").objectReferenceValue = click;
            so.FindProperty("_soundVolume").floatValue = 0.35f;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        var scene = EditorSceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    // 짧은 효과음은 메모리에 풀어 두고 바로 내도록, 긴 종소리는 압축한 채 두도록 들여오기 설정을 맞춘다
    private static void ApplyImportSettings()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { AudioFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            AudioImporter importer = (AudioImporter)AssetImporter.GetAtPath(path);
            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            bool longClip = path.Contains("desk-bell");
            settings.loadType = longClip ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = 0.7f;
            settings.preloadAudioData = true;
            importer.defaultSampleSettings = settings;
            importer.forceToMono = true;
            importer.SaveAndReimport();
        }
    }

    // 이름이 접두어로 시작하는 클립을 모두 찾아 배열 필드에 넣는다(접두어가 없으면 비운다)
    private static void SetClips(SerializedObject so, string field, string prefix)
    {
        AudioClip[] clips = prefix == null ? new AudioClip[0] : AssetDatabase.FindAssets("t:AudioClip", new[] { AudioFolder })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => p.Substring(AudioFolder.Length + 1).StartsWith(prefix))
            .OrderBy(p => p)
            .Select(AssetDatabase.LoadAssetAtPath<AudioClip>)
            .ToArray();
        SerializedProperty property = so.FindProperty(field);
        property.arraySize = clips.Length;
        for (int i = 0; i < clips.Length; i++)
            property.GetArrayElementAtIndex(i).objectReferenceValue = clips[i];
    }

    // Audio 폴더 기준 경로로 클립 하나를 찾는다
    private static AudioClip Load(string name) =>
        AssetDatabase.FindAssets("t:AudioClip", new[] { AudioFolder })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => p.Substring(AudioFolder.Length + 1).StartsWith(name))
            .Select(AssetDatabase.LoadAssetAtPath<AudioClip>)
            .FirstOrDefault();
}
