using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class PropMeshBuilder
{
    private const float CreaseAngle = 35f;

    private readonly List<Vector3> _vertices = new List<Vector3>();
    private readonly List<Vector3> _normals = new List<Vector3>();
    private readonly List<Vector2> _uvs = new List<Vector2>();
    private readonly List<List<int>> _submeshes = new List<List<int>>();

    public Matrix4x4 Transform = Matrix4x4.identity;
    public bool Faceted;

    // 단면(반지름, 높이)을 y축 둘레로 돌려 회전체를 만든다
    public void Lathe(Vector2[] profile, int segments, int submesh, float uvScale)
    {
        Sweep(0f, 0f, profile, segments / 4, submesh, uvScale);
    }

    // 단면(바깥 간격, 높이)을 모서리가 둥근 사각형 경로를 따라 돌려 면을 만든다(Faceted면 둘레 방향으로 각진 면)
    public void Sweep(float coreHalfX, float coreHalfZ, Vector2[] profile, int cornerSegments, int submesh, float uvScale)
    {
        List<Vector2> corners;
        List<Vector2> directions;
        BuildPath(coreHalfX, coreHalfZ, cornerSegments, out corners, out directions);

        float maxOffset = 0f;
        foreach (Vector2 p in profile) maxOffset = Mathf.Max(maxOffset, p.x);
        float[] pathU = new float[corners.Count + 1];
        for (int k = 1; k <= corners.Count; k++)
        {
            Vector2 a = corners[k - 1] + directions[k - 1] * maxOffset;
            Vector2 b = corners[k % corners.Count] + directions[k % corners.Count] * maxOffset;
            pathU[k] = pathU[k - 1] + Vector2.Distance(a, b);
        }

        Vector2[] segmentNormals = new Vector2[profile.Length - 1];
        for (int i = 0; i < segmentNormals.Length; i++)
        {
            Vector2 t = (profile[i + 1] - profile[i]).normalized;
            segmentNormals[i] = new Vector2(t.y, -t.x);
        }

        float profileV = 0f;
        for (int i = 0; i < segmentNormals.Length; i++)
        {
            Vector2 n0 = SmoothedNormal(segmentNormals, i, true);
            Vector2 n1 = SmoothedNormal(segmentNormals, i, false);
            float length = Vector2.Distance(profile[i], profile[i + 1]);
            if (length < 1e-6f) continue;

            if (Faceted)
            {
                for (int k = 0; k < corners.Count; k++)
                {
                    int next = (k + 1) % corners.Count;
                    Vector2 mid = (directions[k] + directions[next]).normalized;
                    int a = AddVertex(PathPoint(corners[k], directions[k], profile[i]), PathNormal(mid, n0), new Vector2(pathU[k], profileV) * uvScale);
                    int b = AddVertex(PathPoint(corners[next], directions[next], profile[i]), PathNormal(mid, n0), new Vector2(pathU[k + 1], profileV) * uvScale);
                    int c = AddVertex(PathPoint(corners[next], directions[next], profile[i + 1]), PathNormal(mid, n1), new Vector2(pathU[k + 1], profileV + length) * uvScale);
                    int d = AddVertex(PathPoint(corners[k], directions[k], profile[i + 1]), PathNormal(mid, n1), new Vector2(pathU[k], profileV + length) * uvScale);
                    Quad(submesh, a, b, c, d);
                }
                profileV += length;
                continue;
            }

            int start = _vertices.Count;
            for (int k = 0; k <= corners.Count; k++)
            {
                Vector2 c = corners[k % corners.Count];
                Vector2 d = directions[k % corners.Count];
                AddVertex(PathPoint(c, d, profile[i]), PathNormal(d, n0), new Vector2(pathU[k], profileV) * uvScale);
                AddVertex(PathPoint(c, d, profile[i + 1]), PathNormal(d, n1), new Vector2(pathU[k], profileV + length) * uvScale);
            }
            for (int k = 0; k < corners.Count; k++)
            {
                int a = start + k * 2;
                Quad(submesh, a, a + 2, a + 3, a + 1);
            }
            profileV += length;
        }
    }

    // 둥근 사각형 경로를 간격만큼 넓힌 평평한 뚜껑을 높이 y에 만든다
    public void Cap(float coreHalfX, float coreHalfZ, float offset, float y, bool up, int cornerSegments, int submesh, float uvScale)
    {
        List<Vector2> corners;
        List<Vector2> directions;
        BuildPath(coreHalfX, coreHalfZ, cornerSegments, out corners, out directions);

        Vector3 normal = up ? Vector3.up : Vector3.down;
        int center = AddVertex(new Vector3(0f, y, 0f), normal, Vector2.zero);
        int start = _vertices.Count;
        for (int k = 0; k < corners.Count; k++)
        {
            Vector2 p = corners[k] + directions[k] * offset;
            AddVertex(new Vector3(p.x, y, p.y), normal, p * uvScale);
        }
        for (int k = 0; k < corners.Count; k++)
            Triangle(submesh, center, start + k, start + (k + 1) % corners.Count);
    }

    // 중심과 크기로 상자를 만들고 면마다 평면 UV를 준다
    public void Box(Vector3 center, Vector3 size, int submesh, float uvScale)
    {
        Vector3 h = size * 0.5f;
        Vector3[] axes = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
        foreach (Vector3 n in axes)
        {
            Vector3 u = Mathf.Abs(n.y) > 0.5f ? Vector3.right : Vector3.up;
            Vector3 v = Vector3.Cross(n, u);
            Vector3 faceCenter = center + Vector3.Scale(n, h);
            Vector3 du = Vector3.Scale(u, h);
            Vector3 dv = Vector3.Scale(v, h);
            int a = AddVertex(faceCenter - du - dv, n, PlanarUv(faceCenter - du - dv, n) * uvScale);
            int b = AddVertex(faceCenter + du - dv, n, PlanarUv(faceCenter + du - dv, n) * uvScale);
            int c = AddVertex(faceCenter + du + dv, n, PlanarUv(faceCenter + du + dv, n) * uvScale);
            int d = AddVertex(faceCenter - du + dv, n, PlanarUv(faceCenter - du + dv, n) * uvScale);
            Quad(submesh, a, b, c, d);
        }
    }

    // 모서리 한 점과 두 변 벡터로 평평한 사각형 판을 만들고 길이에 맞춘 UV를 준다
    public void Panel(Vector3 corner, Vector3 uAxis, Vector3 vAxis, Vector3 normal, int submesh, float uvScale, Vector2 uvOffset)
    {
        float u = uAxis.magnitude;
        float v = vAxis.magnitude;
        int a = AddVertex(corner, normal, uvOffset * uvScale);
        int b = AddVertex(corner + uAxis, normal, (uvOffset + new Vector2(u, 0f)) * uvScale);
        int c = AddVertex(corner + uAxis + vAxis, normal, (uvOffset + new Vector2(u, v)) * uvScale);
        int d = AddVertex(corner + vAxis, normal, (uvOffset + new Vector2(0f, v)) * uvScale);
        Quad(submesh, a, b, c, d);
    }

    // 볼록한 옆모양(x, y)을 z 방향으로 밀어 각진 기둥을 만든다
    public void Prism(Vector2[] polygon, float zMin, float zMax, int submesh, float uvScale)
    {
        Vector2 centroid = Vector2.zero;
        foreach (Vector2 p in polygon) centroid += p;
        centroid /= polygon.Length;

        for (int i = 0; i < polygon.Length; i++)
        {
            Vector2 p0 = polygon[i];
            Vector2 p1 = polygon[(i + 1) % polygon.Length];
            Vector2 edge = (p1 - p0).normalized;
            Vector3 n = new Vector3(edge.y, -edge.x, 0f);
            if (Vector2.Dot(new Vector2(n.x, n.y), (p0 + p1) * 0.5f - centroid) < 0f) n = -n;
            float len = Vector2.Distance(p0, p1);
            int a = AddVertex(new Vector3(p0.x, p0.y, zMin), n, new Vector2(0f, zMin) * uvScale);
            int b = AddVertex(new Vector3(p1.x, p1.y, zMin), n, new Vector2(len, zMin) * uvScale);
            int c = AddVertex(new Vector3(p1.x, p1.y, zMax), n, new Vector2(len, zMax) * uvScale);
            int d = AddVertex(new Vector3(p0.x, p0.y, zMax), n, new Vector2(0f, zMax) * uvScale);
            Quad(submesh, a, b, c, d);
        }

        foreach (float z in new[] { zMin, zMax })
        {
            Vector3 n = z == zMin ? Vector3.back : Vector3.forward;
            int center = AddVertex(new Vector3(centroid.x, centroid.y, z), n, centroid * uvScale);
            int start = _vertices.Count;
            foreach (Vector2 p in polygon) AddVertex(new Vector3(p.x, p.y, z), n, p * uvScale);
            for (int i = 0; i < polygon.Length; i++)
                Triangle(submesh, center, start + i, start + (i + 1) % polygon.Length);
        }
    }

    // 모은 정점과 서브메시로 메시를 만든다
    public Mesh ToMesh(string name)
    {
        Mesh mesh = new Mesh { name = name };
        mesh.indexFormat = _vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
        mesh.SetVertices(_vertices);
        mesh.SetNormals(_normals);
        mesh.SetUVs(0, _uvs);
        mesh.subMeshCount = _submeshes.Count;
        for (int i = 0; i < _submeshes.Count; i++) mesh.SetTriangles(_submeshes[i], i);
        mesh.RecalculateBounds();
        mesh.RecalculateTangents();
        return mesh;
    }

    // 모서리마다 꼭짓점과 바깥 방향 목록을 만든다(중심 사각형이 점이면 원)
    private static void BuildPath(float hx, float hz, int cornerSegments, out List<Vector2> corners, out List<Vector2> directions)
    {
        corners = new List<Vector2>();
        directions = new List<Vector2>();
        bool circle = hx <= 0f && hz <= 0f;
        Vector2[] cornerPoints = { new Vector2(hx, hz), new Vector2(-hx, hz), new Vector2(-hx, -hz), new Vector2(hx, -hz) };
        for (int c = 0; c < 4; c++)
        {
            int count = circle ? cornerSegments : cornerSegments + 1;
            for (int s = 0; s < count; s++)
            {
                float angle = (c * 90f + 90f * s / cornerSegments) * Mathf.Deg2Rad;
                corners.Add(cornerPoints[c]);
                directions.Add(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)));
            }
        }
    }

    // 꺾임 각이 작으면 이웃 단면 구간과 노멀을 평균한다
    private static Vector2 SmoothedNormal(Vector2[] normals, int segment, bool atStart)
    {
        int neighbour = atStart ? segment - 1 : segment + 1;
        if (neighbour < 0 || neighbour >= normals.Length) return normals[segment];
        if (Vector2.Angle(normals[segment], normals[neighbour]) > CreaseAngle) return normals[segment];
        return (normals[segment] + normals[neighbour]).normalized;
    }

    // 경로 위 한 점에서 단면 점의 위치를 구한다
    private static Vector3 PathPoint(Vector2 corner, Vector2 direction, Vector2 profilePoint)
    {
        Vector2 p = corner + direction * profilePoint.x;
        return new Vector3(p.x, profilePoint.y, p.y);
    }

    // 경로 방향과 단면 노멀을 합쳐 3D 노멀을 만든다
    private static Vector3 PathNormal(Vector2 direction, Vector2 profileNormal)
    {
        return new Vector3(direction.x * profileNormal.x, profileNormal.y, direction.y * profileNormal.x).normalized;
    }

    // 노멀 방향에 맞는 평면 UV를 고른다
    private static Vector2 PlanarUv(Vector3 p, Vector3 n)
    {
        if (Mathf.Abs(n.y) > 0.5f) return new Vector2(p.x, p.z);
        if (Mathf.Abs(n.x) > 0.5f) return new Vector2(p.z, p.y);
        return new Vector2(p.x, p.y);
    }

    // 현재 변환을 적용해 정점 하나를 넣는다
    private int AddVertex(Vector3 position, Vector3 normal, Vector2 uv)
    {
        _vertices.Add(Transform.MultiplyPoint3x4(position));
        _normals.Add(Transform.MultiplyVector(normal).normalized);
        _uvs.Add(uv);
        return _vertices.Count - 1;
    }

    // 사각형을 삼각형 두 개로 넣는다
    private void Quad(int submesh, int a, int b, int c, int d)
    {
        Triangle(submesh, a, b, c);
        Triangle(submesh, a, c, d);
    }

    // 정점 노멀과 같은 쪽을 보도록 감는 방향을 맞춰 삼각형을 넣는다
    private void Triangle(int submesh, int a, int b, int c)
    {
        while (_submeshes.Count <= submesh) _submeshes.Add(new List<int>());
        Vector3 face = Vector3.Cross(_vertices[b] - _vertices[a], _vertices[c] - _vertices[a]);
        if (face.sqrMagnitude < 1e-14f) return;
        Vector3 normal = _normals[a] + _normals[b] + _normals[c];
        List<int> list = _submeshes[submesh];
        if (Vector3.Dot(face, normal) >= 0f) { list.Add(a); list.Add(b); list.Add(c); }
        else { list.Add(a); list.Add(c); list.Add(b); }
    }
}
