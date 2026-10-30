using System.IO;
using UnityEditor;
using UnityEngine;
using UnityMeshSimplifier;

public static class MeshyAssetProcessor
{
    private const string OutputRoot = "Assets/Models/Processed";

    public struct Result
    {
        public string MeshPath;
        public string MaterialPath;
        public string TexturePath;
        public int SourceTriangles;
        public int Triangles;
        public long SourceTextureBytes;
    }

    // Meshy FBX 하나를 폴리곤 감소, 텍스처 압축, URP Lit 머티리얼 생성까지 처리해 결과 경로를 돌려준다
    public static Result Process(string fbxPath, string assetName, int targetTriangles, int maxTextureSize, float smoothness, bool preserveUVSeams)
    {
        EnsureFolder(OutputRoot);
        Result result = new Result();

        Mesh source = LoadMesh(fbxPath);
        result.SourceTriangles = source.triangles.Length / 3;

        Mesh simplified = Simplify(source, targetTriangles, preserveUVSeams);
        simplified.name = assetName;
        MeshUtility.SetMeshCompression(simplified, ModelImporterMeshCompression.Medium);
        MeshUtility.Optimize(simplified);
        result.Triangles = simplified.triangles.Length / 3;
        result.MeshPath = $"{OutputRoot}/{assetName}.asset";
        CreateOrReplace(simplified, result.MeshPath);

        Texture2D baseMap = ExtractBaseMap(fbxPath, assetName);
        if (baseMap != null)
        {
            result.TexturePath = AssetDatabase.GetAssetPath(baseMap);
            result.SourceTextureBytes = new FileInfo(result.TexturePath).Length;
            ConfigureTexture(result.TexturePath, maxTextureSize);
            baseMap = AssetDatabase.LoadAssetAtPath<Texture2D>(result.TexturePath);
        }

        result.MaterialPath = $"{OutputRoot}/{assetName}.mat";
        Material material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        material.SetTexture("_BaseMap", baseMap);
        material.SetFloat("_Smoothness", smoothness);
        CreateOrReplace(material, result.MaterialPath);

        AssetDatabase.SaveAssets();
        return result;
    }

    // 처리한 메시와 머티리얼로 바닥 가운데가 원점이고 가장 긴 변이 목표 크기인 Visual 오브젝트를 부모 아래 만든다
    public static GameObject CreateVisual(Transform parent, Result result, float targetSize, Vector3 eulerOffset)
    {
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(result.MeshPath);
        Material material = AssetDatabase.LoadAssetAtPath<Material>(result.MaterialPath);

        GameObject visual = new GameObject("Visual");
        visual.transform.SetParent(parent, false);
        visual.layer = parent.gameObject.layer;
        visual.AddComponent<MeshFilter>().sharedMesh = mesh;
        visual.AddComponent<MeshRenderer>().sharedMaterial = material;

        Quaternion rotation = Quaternion.Euler(eulerOffset);
        Bounds rotated = RotatedBounds(mesh.bounds, rotation);
        float scale = targetSize / Mathf.Max(rotated.size.x, Mathf.Max(rotated.size.y, rotated.size.z));

        visual.transform.localRotation = rotation;
        visual.transform.localScale = Vector3.one * scale;
        visual.transform.localPosition = new Vector3(-rotated.center.x, -rotated.min.y, -rotated.center.z) * scale;
        return visual;
    }

    // FBX 안에서 첫 메시를 찾는다
    private static Mesh LoadMesh(string fbxPath)
    {
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
            if (asset is Mesh mesh) return mesh;
        throw new System.InvalidOperationException($"{fbxPath}에 메시가 없음");
    }

    // 목표 삼각형 수에 맞춰 메시를 줄이고 UV 경계를 지킬지는 호출하는 쪽이 정한다
    private static Mesh Simplify(Mesh source, int targetTriangles, bool preserveUVSeams)
    {
        int sourceTriangles = source.triangles.Length / 3;
        if (sourceTriangles <= targetTriangles) return Object.Instantiate(source);

        MeshSimplifier simplifier = new MeshSimplifier
        {
            SimplificationOptions = new SimplificationOptions
            {
                PreserveBorderEdges = false,
                PreserveUVSeamEdges = preserveUVSeams,
                PreserveUVFoldoverEdges = false,
                EnableSmartLink = true,
                VertexLinkDistance = double.Epsilon,
                MaxIterationCount = 100,
                Agressiveness = 7.0
            }
        };
        simplifier.Initialize(source);
        simplifier.SimplifyMesh((float)targetTriangles / sourceTriangles);
        return simplifier.ToMesh();
    }

    // FBX에 들어 있는 텍스처를 에셋 이름 폴더로 꺼내 첫 텍스처를 기본 색 맵으로 돌려준다
    private static Texture2D ExtractBaseMap(string fbxPath, string assetName)
    {
        string folder = $"{OutputRoot}/{assetName}_Textures";
        EnsureFolder(folder);

        ModelImporter importer = (ModelImporter)AssetImporter.GetAtPath(fbxPath);
        importer.ExtractTextures(folder);
        AssetDatabase.Refresh();

        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { folder }))
            return AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(guid));
        return null;
    }

    // 텍스처를 일반 타입, 최대 크기 제한, 압축, 밉맵으로 들여오게 한다
    private static void ConfigureTexture(string path, int maxSize)
    {
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Default;
        importer.sRGBTexture = true;
        importer.mipmapEnabled = true;
        importer.anisoLevel = 2;
        importer.filterMode = FilterMode.Trilinear;

        TextureImporterPlatformSettings settings = importer.GetDefaultPlatformTextureSettings();
        settings.maxTextureSize = maxSize;
        settings.textureCompression = TextureImporterCompression.Compressed;
        importer.SetPlatformTextureSettings(settings);
        importer.SaveAndReimport();
    }

    // 회전을 적용한 뒤의 경계 상자를 구한다
    private static Bounds RotatedBounds(Bounds bounds, Quaternion rotation)
    {
        Bounds rotated = new Bounds(rotation * bounds.center, Vector3.zero);
        Vector3 e = bounds.extents;
        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = bounds.center + new Vector3((i & 1) == 0 ? -e.x : e.x, (i & 2) == 0 ? -e.y : e.y, (i & 4) == 0 ? -e.z : e.z);
            rotated.Encapsulate(rotation * corner);
        }
        return rotated;
    }

    // 같은 경로의 에셋이 있으면 지우고 새로 저장한다
    private static void CreateOrReplace(Object asset, string path)
    {
        if (AssetDatabase.LoadAssetAtPath<Object>(path) != null) AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(asset, path);
    }

    // 폴더가 없으면 상위부터 만든다
    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
