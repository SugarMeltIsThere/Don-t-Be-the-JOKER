using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

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
            SetNamedClips(so, "_roundWon", "Jingles/jingles_SAX10", "Jingles/jingles_SAX02");
            SetNamedClips(so, "_roundLost", "Jingles/jingles_SAX14", "Jingles/jingles_SAX11");
            SetNamedClips(so, "_roundPush", "Jingles/jingles_SAX03");
            SetNamedClips(so, "_matchWon", "Jingles/jingles_SAX13");
            SetNamedClips(so, "_matchLost", "Jingles/jingles_SAX07");
            if (so.FindProperty("_turnTimer").objectReferenceValue == null)
                so.FindProperty("_turnTimer").objectReferenceValue = Object.FindFirstObjectByType<TurnTimer>(FindObjectsInactive.Include);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        AudioClip hover = Load("Chips/chips-stack-1");
        AudioClip click = Load("Cards/card-place-1");
        foreach (MenuButtonFx fx in Object.FindObjectsByType<MenuButtonFx>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            SerializedObject so = new SerializedObject(fx);
            so.FindProperty("_hoverSound").objectReferenceValue = hover;
            so.FindProperty("_clickSound").objectReferenceValue = click;
            so.FindProperty("_soundVolume").floatValue = 0.35f;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        foreach (Button button in Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            AddButtonSound(button, hover, click);
        foreach (string prefabPath in new[] { "Assets/Prefabs/RoomItem.prefab", "Assets/Prefabs/AceSlot.prefab" })
        {
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            foreach (Button button in root.GetComponentsInChildren<Button>(true)) AddButtonSound(button, hover, click);
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            PrefabUtility.UnloadPrefabContents(root);
        }

        var scene = EditorSceneManager.GetActiveScene();
        SetupMusic(scene.name.Contains("Lobby") ? "Music/drunken_saloon" : "Music/moil");
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
            bool music = path.Contains("/Music/");
            bool longClip = path.Contains("desk-bell") || path.Contains("/Jingles/");
            settings.loadType = music ? AudioClipLoadType.Streaming : longClip ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = music ? 0.6f : 0.7f;
            settings.preloadAudioData = !music;
            importer.defaultSampleSettings = settings;
            importer.forceToMono = !music;
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

    // 메뉴 버튼 효과가 없는 버튼에 마우스를 올릴 때와 누를 때 소리를 붙인다
    private static void AddButtonSound(Button button, AudioClip hover, AudioClip click)
    {
        if (button.GetComponent<MenuButtonFx>() != null) return;
        ButtonSound sound = button.GetComponent<ButtonSound>();
        if (sound == null) sound = button.gameObject.AddComponent<ButtonSound>();
        SerializedObject so = new SerializedObject(sound);
        so.FindProperty("_hoverSound").objectReferenceValue = hover;
        so.FindProperty("_clickSound").objectReferenceValue = click;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // 장면에 배경 음악 재생기(Music 오브젝트)를 하나 두고 곡을 정한다
    private static void SetupMusic(string track)
    {
        MusicPlayer player = Object.FindFirstObjectByType<MusicPlayer>(FindObjectsInactive.Include);
        if (player == null) player = new GameObject("Music").AddComponent<MusicPlayer>();
        SerializedObject so = new SerializedObject(player);
        so.FindProperty("_track").objectReferenceValue = Load(track);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // 정한 이름의 클립들을 직렬화 배열에 넣는다
    private static void SetNamedClips(SerializedObject so, string field, params string[] names)
    {
        AudioClip[] clips = names.Select(Load).Where(c => c != null).ToArray();
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
