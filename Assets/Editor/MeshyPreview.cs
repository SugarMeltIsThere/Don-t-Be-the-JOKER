using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class MeshyPreview
{
    // 에셋 경로 목록을 격리된 미리보기 장면에서 하나씩 찍어 격자 PNG 하나로 저장한다
    public static void RenderGrid(string[] assetPaths, string outPng, int cell, int columns, float yaw, float pitch)
    {
        List<GameObject> sources = new List<GameObject>();
        foreach (string path in assetPaths) sources.Add(AssetDatabase.LoadAssetAtPath<GameObject>(path));
        RenderObjects(sources.ToArray(), outPng, cell, columns, yaw, pitch);
    }

    // 오브젝트 목록을 격리된 미리보기 장면에서 하나씩 찍어 격자 PNG 하나로 저장한다
    public static void RenderObjects(GameObject[] sources, string outPng, int cell, int columns, float yaw, float pitch)
    {
        int rows = Mathf.CeilToInt(sources.Length / (float)columns);
        Texture2D sheet = new Texture2D(cell * columns, cell * rows, TextureFormat.RGB24, false);
        Color[] clear = new Color[sheet.width * sheet.height];
        for (int i = 0; i < clear.Length; i++) clear[i] = new Color(0.18f, 0.18f, 0.2f);
        sheet.SetPixels(clear);

        for (int i = 0; i < sources.Length; i++)
        {
            if (sources[i] == null) continue;
            Texture2D shot = RenderOne(sources[i], cell, yaw, pitch);
            sheet.SetPixels(i % columns * cell, (rows - 1 - i / columns) * cell, cell, cell, shot.GetPixels());
            Object.DestroyImmediate(shot);
        }

        sheet.Apply();
        File.WriteAllBytes(outPng, sheet.EncodeToPNG());
        Object.DestroyImmediate(sheet);
    }

    // 오브젝트 하나를 경계 상자에 맞춘 카메라로 찍어 정사각형 텍스처로 돌려준다
    private static Texture2D RenderOne(GameObject source, int size, float yaw, float pitch)
    {
        PreviewRenderUtility preview = new PreviewRenderUtility();
        try
        {
            GameObject instance = Object.Instantiate(source);
            preview.AddSingleGO(instance);

            Bounds bounds = new Bounds(instance.transform.position, Vector3.zero);
            bool first = true;
            foreach (Renderer r in instance.GetComponentsInChildren<Renderer>())
            {
                if (first) { bounds = r.bounds; first = false; }
                else bounds.Encapsulate(r.bounds);
            }

            Camera camera = preview.camera;
            camera.fieldOfView = 30f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.18f, 0.18f, 0.2f);
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            float distance = bounds.extents.magnitude / Mathf.Sin(camera.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.05f;
            camera.nearClipPlane = distance * 0.01f;
            camera.farClipPlane = distance * 3f;
            camera.transform.SetPositionAndRotation(bounds.center - rotation * Vector3.forward * distance, rotation);

            preview.lights[0].intensity = 1.3f;
            preview.lights[0].transform.rotation = Quaternion.Euler(40f, yaw - 40f, 0f);
            preview.lights[1].intensity = 0.6f;
            preview.ambientColor = new Color(0.35f, 0.35f, 0.38f);

            preview.BeginStaticPreview(new Rect(0, 0, size, size));
            preview.Render(true);
            Texture2D raw = preview.EndStaticPreview();

            Texture2D result = new Texture2D(size, size, TextureFormat.RGB24, false);
            RenderTexture rt = RenderTexture.GetTemporary(size, size, 0);
            Graphics.Blit(raw, rt);
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;
            result.ReadPixels(new Rect(0, 0, size, size), 0, 0);
            result.Apply();
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);
            Object.DestroyImmediate(raw);
            return result;
        }
        finally
        {
            preview.Cleanup();
        }
    }
}
