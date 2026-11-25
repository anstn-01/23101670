using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 메뉴: Tools > Create Low Poly Pickup Truck
// 로우폴리 픽업트럭 메쉬 1개(서브메쉬 7개)를 코드로 만들어 Assets/LowPolyTruck 에 저장하고 씬에 배치합니다.
public static class LowPolyTruckBuilder
{
    const string Folder = "Assets/LowPolyTruck";

    enum Part { Body, Dark, Glass, Tire, Rim, HeadLight, TailLight, Count }

    static List<Vector3> verts;
    static List<Vector3> normals;
    static List<int>[] tris;

    [MenuItem("Tools/Create Low Poly Pickup Truck")]
    public static void Create()
    {
        verts = new List<Vector3>();
        normals = new List<Vector3>();
        tris = new List<int>[(int)Part.Count];
        for (int i = 0; i < tris.Length; i++) tris[i] = new List<int>();

        BuildTruck();

        var mesh = new Mesh { name = "LowPolyPickupTruck_Mesh" };
        mesh.SetVertices(verts);
        mesh.SetNormals(normals);
        mesh.subMeshCount = tris.Length;
        for (int i = 0; i < tris.Length; i++) mesh.SetTriangles(tris[i], i);
        mesh.RecalculateBounds();

        if (!AssetDatabase.IsValidFolder(Folder))
            AssetDatabase.CreateFolder("Assets", "LowPolyTruck");

        string meshPath = Folder + "/LowPolyPickupTruck_Mesh.asset";
        AssetDatabase.DeleteAsset(meshPath);
        AssetDatabase.CreateAsset(mesh, meshPath);

        var mats = new Material[(int)Part.Count];
        mats[(int)Part.Body] = Mat("Truck_Body", new Color(0.93f, 0.45f, 0.10f), 0.35f);
        mats[(int)Part.Dark] = Mat("Truck_Dark", new Color(0.18f, 0.18f, 0.20f), 0.2f);
        mats[(int)Part.Glass] = Mat("Truck_Glass", new Color(0.12f, 0.14f, 0.17f), 0.9f);
        mats[(int)Part.Tire] = Mat("Truck_Tire", new Color(0.07f, 0.07f, 0.07f), 0.1f);
        mats[(int)Part.Rim] = Mat("Truck_Rim", new Color(0.85f, 0.85f, 0.85f), 0.5f);
        mats[(int)Part.HeadLight] = Mat("Truck_HeadLight", Color.white, 0.6f, new Color(1f, 1f, 0.9f));
        mats[(int)Part.TailLight] = Mat("Truck_TailLight", new Color(1f, 0.45f, 0.35f), 0.6f, new Color(0.8f, 0.2f, 0.1f));

        var go = new GameObject("LowPolyPickupTruck");
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterials = mats;
        var col = go.AddComponent<BoxCollider>();
        col.center = mesh.bounds.center;
        col.size = mesh.bounds.size;
        Undo.RegisterCreatedObjectUndo(go, "Create Low Poly Pickup Truck");

        PrefabUtility.SaveAsPrefabAssetAndConnect(go, Folder + "/LowPolyPickupTruck.prefab", InteractionMode.UserAction);
        AssetDatabase.SaveAssets();

        Selection.activeGameObject = go;
        if (SceneView.lastActiveSceneView != null) SceneView.lastActiveSceneView.FrameSelected();
        EditorSceneManager.MarkSceneDirty(go.scene);
        Debug.Log("LowPolyPickupTruck 생성 완료: " + Folder);
    }

    static void BuildTruck()
    {
        // +Z 가 차량 앞쪽, 단위 m
        // 차체(주황)
        Box(Part.Body, new Vector3(0, 0.85f, 1.60f), new Vector3(1.90f, 0.50f, 1.50f)); // 보닛
        Box(Part.Body, new Vector3(0, 0.85f, 0.20f), new Vector3(1.90f, 0.50f, 1.30f)); // 캐빈 하단
        Box(Part.Body, new Vector3(0, 1.78f, 0.05f), new Vector3(1.62f, 0.08f, 1.00f)); // 지붕
        Box(Part.Body, new Vector3(0, 0.65f, -1.45f), new Vector3(1.90f, 0.10f, 1.80f)); // 짐칸 바닥
        Box(Part.Body, new Vector3(-0.90f, 0.85f, -1.45f), new Vector3(0.10f, 0.50f, 1.80f)); // 짐칸 왼쪽
        Box(Part.Body, new Vector3(0.90f, 0.85f, -1.45f), new Vector3(0.10f, 0.50f, 1.80f)); // 짐칸 오른쪽
        Box(Part.Body, new Vector3(0, 0.85f, -2.30f), new Vector3(1.90f, 0.50f, 0.10f)); // 테일게이트
        Box(Part.Body, new Vector3(0, 0.85f, -0.60f), new Vector3(1.90f, 0.50f, 0.10f)); // 짐칸 앞벽

        // 유리 캐빈(경사진 앞유리)
        FrustumY(Part.Glass, new Vector3(0, 1.10f, 0.15f), new Vector2(1.85f, 1.25f),
                             new Vector3(0, 1.74f, 0.00f), new Vector2(1.60f, 0.95f));

        // 어두운 부품
        Box(Part.Dark, new Vector3(0, 0.50f, 0), new Vector3(1.60f, 0.20f, 4.40f)); // 프레임
        Box(Part.Dark, new Vector3(0, 0.62f, 2.45f), new Vector3(2.00f, 0.24f, 0.20f)); // 앞 범퍼
        Box(Part.Dark, new Vector3(0, 0.88f, 2.37f), new Vector3(1.30f, 0.30f, 0.05f)); // 그릴
        Box(Part.Dark, new Vector3(0, 0.60f, -2.45f), new Vector3(2.00f, 0.20f, 0.20f)); // 뒤 범퍼
        Box(Part.Dark, new Vector3(-1.05f, 1.30f, 0.70f), new Vector3(0.18f, 0.16f, 0.08f)); // 사이드미러
        Box(Part.Dark, new Vector3(1.05f, 1.30f, 0.70f), new Vector3(0.18f, 0.16f, 0.08f));

        // 라이트
        Box(Part.HeadLight, new Vector3(-0.78f, 0.90f, 2.37f), new Vector3(0.26f, 0.16f, 0.05f));
        Box(Part.HeadLight, new Vector3(0.78f, 0.90f, 2.37f), new Vector3(0.26f, 0.16f, 0.05f));
        Box(Part.TailLight, new Vector3(-0.88f, 0.90f, -2.37f), new Vector3(0.12f, 0.28f, 0.05f));
        Box(Part.TailLight, new Vector3(0.88f, 0.90f, -2.37f), new Vector3(0.12f, 0.28f, 0.05f));

        // 바퀴 + 휠
        float[] wz = { 1.55f, -1.50f };
        foreach (float z in wz)
        {
            foreach (float s in new[] { -1f, 1f })
            {
                CylinderX(Part.Tire, new Vector3(0.92f * s, 0.42f, z), 0.42f, 0.32f, 12);
                CylinderX(Part.Rim, new Vector3(1.09f * s, 0.42f, z), 0.24f, 0.03f, 8);
            }
        }
    }

    // ---------- 도형 헬퍼 ----------
    static void AddPoly(Part p, Vector3[] pts, Vector3 shapeCenter)
    {
        Vector3 fc = Vector3.zero;
        foreach (var v in pts) fc += v;
        fc /= pts.Length;
        Vector3 n = Vector3.Cross(pts[1] - pts[0], pts[2] - pts[0]).normalized;
        if (Vector3.Dot(n, fc - shapeCenter) < 0)
        {
            System.Array.Reverse(pts);
            n = -n;
        }
        int start = verts.Count;
        foreach (var v in pts) { verts.Add(v); normals.Add(n); }
        var t = tris[(int)p];
        for (int i = 1; i < pts.Length - 1; i++)
        {
            t.Add(start); t.Add(start + i); t.Add(start + i + 1);
        }
    }

    static void Hexa(Part p, Vector3[] c)
    {
        Vector3 center = Vector3.zero;
        foreach (var v in c) center += v;
        center /= c.Length;
        AddPoly(p, new[] { c[0], c[1], c[2], c[3] }, center); // 아래
        AddPoly(p, new[] { c[4], c[5], c[6], c[7] }, center); // 위
        AddPoly(p, new[] { c[0], c[1], c[5], c[4] }, center);
        AddPoly(p, new[] { c[1], c[2], c[6], c[5] }, center);
        AddPoly(p, new[] { c[2], c[3], c[7], c[6] }, center);
        AddPoly(p, new[] { c[3], c[0], c[4], c[7] }, center);
    }

    static void FrustumY(Part p, Vector3 bc, Vector2 bs, Vector3 tc, Vector2 ts)
    {
        float bx = bs.x / 2, bz = bs.y / 2, tx = ts.x / 2, tz = ts.y / 2;
        Hexa(p, new[]
        {
            bc + new Vector3(-bx, 0, -bz), bc + new Vector3(bx, 0, -bz),
            bc + new Vector3(bx, 0, bz),   bc + new Vector3(-bx, 0, bz),
            tc + new Vector3(-tx, 0, -tz), tc + new Vector3(tx, 0, -tz),
            tc + new Vector3(tx, 0, tz),   tc + new Vector3(-tx, 0, tz),
        });
    }

    static void Box(Part p, Vector3 c, Vector3 s)
    {
        FrustumY(p, c - new Vector3(0, s.y / 2, 0), new Vector2(s.x, s.z),
                    c + new Vector3(0, s.y / 2, 0), new Vector2(s.x, s.z));
    }

    static void CylinderX(Part p, Vector3 c, float r, float w, int seg)
    {
        var L = new Vector3[seg];
        var R = new Vector3[seg];
        for (int i = 0; i < seg; i++)
        {
            float a = (i + 0.5f) / seg * Mathf.PI * 2f;
            var o = new Vector3(0, Mathf.Cos(a) * r, Mathf.Sin(a) * r);
            L[i] = c + new Vector3(-w / 2, 0, 0) + o;
            R[i] = c + new Vector3(w / 2, 0, 0) + o;
        }
        for (int i = 0; i < seg; i++)
        {
            int j = (i + 1) % seg;
            AddPoly(p, new[] { L[i], L[j], R[j], R[i] }, c);
        }
        AddPoly(p, (Vector3[])L.Clone(), c);
        AddPoly(p, (Vector3[])R.Clone(), c);
    }

    // ---------- 머티리얼 ----------
    static Material Mat(string name, Color color, float smooth, Color? emission = null)
    {
        string path = Folder + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Lit");
            if (sh == null) sh = Shader.Find("Standard");
            m = new Material(sh);
            AssetDatabase.CreateAsset(m, path);
        }
        m.SetColor("_BaseColor", color);
        m.SetColor("_Color", color);
        m.SetFloat("_Smoothness", smooth);
        m.SetFloat("_Glossiness", smooth);
        if (emission.HasValue)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", emission.Value);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        EditorUtility.SetDirty(m);
        return m;
    }
}
