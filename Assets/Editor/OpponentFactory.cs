using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class OpponentFactory
{
    private const string ModelFolder = "Assets/Models/Characters/Quaternius";
    private const string MaterialFolder = "Assets/Materials/Characters";
    private static readonly Vector3 SeatHips = new Vector3(0f, 0.5f, 0.84f);

    // 게임 장면 맞은편 의자에 그림자 진 상대를 앉힌다: 남녀 몸(퀸은 여자), 거의 검은 피부, 등 뒤 테두리 조명
    [MenuItem("Tools/DBtJ/Build Opponent")]
    public static void Build()
    {
        GameObject old = GameObject.Find("OpponentFigure");
        if (old != null) Object.DestroyImmediate(old);
        Transform root = new GameObject("OpponentFigure").transform;
        OpponentFigure figure = root.gameObject.AddComponent<OpponentFigure>();

        Material skin = Mat("FigureSkin", new Color(0.03f, 0.026f, 0.024f), 0f, 0.28f);
        Animator male = Spawn(root, "Superhero_Male_FullBody", skin);
        Animator female = Spawn(root, "Superhero_Female_FullBody", skin);

        SerializedObject so = new SerializedObject(figure);
        so.FindProperty("_male").FindPropertyRelative("Animator").objectReferenceValue = male;
        so.FindProperty("_female").FindPropertyRelative("Animator").objectReferenceValue = female;
        so.FindProperty("_tableCenter").objectReferenceValue = GameObject.Find("Table/SharedSlot").transform;
        so.ApplyModifiedPropertiesWithoutUndo();

        Light rim = new GameObject("RimLight").AddComponent<Light>();
        rim.transform.SetParent(root, false);
        rim.type = LightType.Spot;
        rim.transform.position = new Vector3(0f, 1.75f, 1.75f);
        rim.transform.LookAt(new Vector3(0f, 1.05f, 0.84f));
        rim.color = new Color(1f, 0.78f, 0.55f);
        rim.intensity = 2.2f;
        rim.range = 2.6f;
        rim.spotAngle = 48f;
        rim.shadows = LightShadows.None;

        EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        EditorSceneManager.SaveScene(root.gameObject.scene);
    }

    // 모델을 의자 위에 엉덩이 높이를 맞춰 놓고 피부 재질을 입힌다
    private static Animator Spawn(Transform root, string model, Material skin)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{ModelFolder}/{model}.fbx");
        GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root);
        go.name = model;
        Animator animator = go.GetComponent<Animator>();
        Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
        go.transform.position += SeatHips - hips.position;

        foreach (SkinnedMeshRenderer r in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            Material[] mats = new Material[r.sharedMaterials.Length];
            for (int i = 0; i < mats.Length; i++) mats[i] = skin;
            r.sharedMaterials = mats;
            r.shadowCastingMode = ShadowCastingMode.On;
        }

        return animator;
    }

    // URP Lit 머티리얼을 만들거나 값을 갱신한다
    private static Material Mat(string name, Color color, float metallic, float smoothness)
    {
        PropFactory.EnsureFolder(MaterialFolder);
        string path = $"{MaterialFolder}/{name}.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(material, path);
        }
        material.SetColor("_BaseColor", color);
        material.SetFloat("_Metallic", metallic);
        material.SetFloat("_Smoothness", smoothness);
        EditorUtility.SetDirty(material);
        return material;
    }
}
