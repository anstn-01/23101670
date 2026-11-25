// Tools > Build Output Table
// Conveyor_BD01 컨베이어가 끝나는 쪽(왼쪽 끝)에 작업 테이블을 붙여 만듭니다.
// 상판 높이를 벨트 높이(0.75m)에 맞춰 상자가 그대로 넘어오도록 했어요.
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public static class OutputTableBuilder
{
    const string Root = "Assets/ConveyorMaterials";
    const string MeshDir = Root + "/TableMeshes";
    static int meshCounter;

    [MenuItem("Tools/Build Output Table")]
    static void Build()
    {
        var conv = GameObject.Find("Conveyor_BD01");
        if (conv == null) { EditorUtility.DisplayDialog("Table", "Conveyor_BD01이 없어요.", "OK"); return; }

        var old = conv.transform.Find("OutputTable");
        if (old != null) Object.DestroyImmediate(old.gameObject);
        if (!AssetDatabase.IsValidFolder(Root)) AssetDatabase.CreateFolder("Assets", "ConveyorMaterials");
        if (AssetDatabase.IsValidFolder(MeshDir)) AssetDatabase.DeleteAsset(MeshDir);
        AssetDatabase.CreateFolder(Root, "TableMeshes");
        meshCounter = 0;

        var top    = Mat("T_Top",    new Color(0.84f, 0.85f, 0.86f), 0.15f, 0.55f);
        var frame  = Mat("C_MetalDark", new Color(0.34f, 0.35f, 0.37f), 0.2f, 0.4f);
        var black  = Mat("C_Black",  new Color(0.06f, 0.06f, 0.07f), 0.2f, 0.3f);
        var orange = Mat("C_Orange", new Color(1.00f, 0.45f, 0.05f), 0f, 0.55f);
        var mat    = Mat("T_Mat",    new Color(0.12f, 0.12f, 0.13f), 0f, 0.2f);

        var table = new GameObject("OutputTable").transform;
        Undo.RegisterCreatedObjectUndo(table.gameObject, "Build Output Table");
        table.SetParent(conv.transform, false);
        // 컨베이어 왼쪽 끝(x=0) 바로 바깥쪽. 상판 크기 2.4 x 0.8
        const float L = 2.4f, W = 0.8f, H = 0.75f, gap = 0.04f;
        table.localPosition = new Vector3(-(L * 0.5f + gap), 0f, 0f);

        // 상판 + 고무 매트 + 앞쪽 주황 테두리
        RBox(table, "Top", new Vector3(0, H - 0.02f, 0), new Vector3(L, 0.04f, W), 0.012f, top, 3).AddComponent<BoxCollider>();
        RBox(table, "TopMat", new Vector3(0, H + 0.002f, 0), new Vector3(L - 0.1f, 0.004f, W - 0.1f), 0.002f, mat, 2);
        RBox(table, "Apron_Front", new Vector3(0, H - 0.07f, -W * 0.5f + 0.02f), new Vector3(L - 0.04f, 0.06f, 0.02f), 0.006f, frame, 2);
        RBox(table, "Apron_Back",  new Vector3(0, H - 0.07f,  W * 0.5f - 0.02f), new Vector3(L - 0.04f, 0.06f, 0.02f), 0.006f, frame, 2);
        RBox(table, "Trim_Front", new Vector3(0, H - 0.07f, -W * 0.5f + 0.008f), new Vector3(L - 0.2f, 0.015f, 0.005f), 0.002f, orange, 2);

        // 상자가 떨어지지 않게 3면 낮은 가드 (컨베이어 쪽은 열림)
        RBox(table, "Guard_Back",  new Vector3(0, H + 0.03f,  W * 0.5f - 0.01f), new Vector3(L, 0.06f, 0.02f), 0.006f, frame, 2).AddComponent<BoxCollider>();
        RBox(table, "Guard_Front", new Vector3(0, H + 0.03f, -W * 0.5f + 0.01f), new Vector3(L, 0.06f, 0.02f), 0.006f, frame, 2).AddComponent<BoxCollider>();
        RBox(table, "Guard_End",   new Vector3(-L * 0.5f + 0.01f, H + 0.03f, 0), new Vector3(0.02f, 0.06f, W), 0.006f, frame, 2).AddComponent<BoxCollider>();

        // 다리 4개 + 받침
        foreach (int sx in new[] { -1, 1 })
            foreach (int sz in new[] { -1, 1 })
            {
                var p = new Vector3(sx * (L * 0.5f - 0.06f), 0, sz * (W * 0.5f - 0.06f));
                RBox(table, "Leg", p + Vector3.up * (H - 0.06f) * 0.5f, new Vector3(0.06f, H - 0.06f, 0.06f), 0.01f, frame, 2).AddComponent<BoxCollider>();
                RBox(table, "Foot", p + Vector3.up * 0.02f, new Vector3(0.1f, 0.04f, 0.1f), 0.012f, black, 2);
            }

        // 가운데 보조 다리 (긴 상판 처짐 방지)
        foreach (int sz in new[] { -1, 1 })
        {
            var p = new Vector3(0, 0, sz * (W * 0.5f - 0.06f));
            RBox(table, "Leg_Mid", p + Vector3.up * (H - 0.06f) * 0.5f, new Vector3(0.06f, H - 0.06f, 0.06f), 0.01f, frame, 2).AddComponent<BoxCollider>();
            RBox(table, "Foot_Mid", p + Vector3.up * 0.02f, new Vector3(0.1f, 0.04f, 0.1f), 0.012f, black, 2);
        }

        // 아래 선반 + 보강대
        RBox(table, "Shelf", new Vector3(0, 0.18f, 0), new Vector3(L - 0.1f, 0.025f, W - 0.1f), 0.008f, frame, 2).AddComponent<BoxCollider>();
        foreach (int sz in new[] { -1, 1 })
            RBox(table, "Rail", new Vector3(0, 0.18f, sz * (W * 0.5f - 0.06f)), new Vector3(L - 0.12f, 0.04f, 0.04f), 0.006f, frame, 2);

        // 왼쪽 벨트 다시 붙이기
        var side = conv.transform.Find("SideBelt");
        if (side != null)
        {
            Undo.RecordObject(side, "Move Side Belt");
            side.localPosition = new Vector3(table.localPosition.x - L * 0.5f - gap - 2.3f, 0, 0);
        }
        // 테이프 기계를 테이블 가운데 위로
        var machine = GameObject.Find("Machine");
        if (machine != null)
        {
            Undo.RecordObject(machine.transform, "Move Tape Machine");
            float sc = machine.transform.localScale.x;
            machine.transform.rotation = table.rotation;
            machine.transform.position = table.TransformPoint(new Vector3(0, 0.755f, 0)) - table.rotation * new Vector3(-0.04f * sc, 0, 0);
        }

        AssetDatabase.SaveAssets();
        Selection.activeGameObject = table.gameObject;
        Debug.Log("[Table] 컨베이어 끝에 테이블 생성 완료 (가로 " + L + "m)");
    }

    // ---------- 헬퍼 ----------
    static GameObject RBox(Transform parent, string name, Vector3 pos, Vector3 size, float r, Material m, int seg)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        var mesh = RoundedBox(size, r, seg); mesh.name = name;
        AssetDatabase.CreateAsset(mesh, $"{MeshDir}/{name}_{meshCounter++}.asset");
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = m;
        return go;
    }

    static Material Mat(string name, Color c, float metallic, float smooth)
    {
        string path = $"{Root}/{name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat != null) return mat;   // 기존 컨베이어 재질은 그대로 사용
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        mat = new Material(shader);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", c);
        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smooth);
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    static Mesh RoundedBox(Vector3 size, float r, int seg)
    {
        Vector3 h = size * 0.5f;
        r = Mathf.Min(r, Mathf.Min(h.x, Mathf.Min(h.y, h.z)) * 0.98f);
        Vector3 ih = h - Vector3.one * r;
        float[][] ax = { Axis(h.x, r, seg), Axis(h.y, r, seg), Axis(h.z, r, seg) };
        var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
        for (int axis = 0; axis < 3; axis++)
        for (int sign = -1; sign <= 1; sign += 2)
        {
            int ua = (axis + 1) % 3, va = (axis + 2) % 3;
            float[] us = ax[ua], vs = ax[va];
            int start = v.Count;
            Vector3 fn = Vector3.zero; fn[axis] = sign;
            for (int j = 0; j < vs.Length; j++)
            for (int i = 0; i < us.Length; i++)
            {
                Vector3 p = Vector3.zero; p[axis] = sign * h[axis]; p[ua] = us[i]; p[va] = vs[j];
                Vector3 inn = new Vector3(Mathf.Clamp(p.x, -ih.x, ih.x), Mathf.Clamp(p.y, -ih.y, ih.y), Mathf.Clamp(p.z, -ih.z, ih.z));
                Vector3 d = p - inn; Vector3 nr = d.sqrMagnitude < 1e-12f ? fn : d.normalized;
                v.Add(inn + nr * r); n.Add(nr); uv.Add(new Vector2((us[i] + h[ua]) / size[ua], (vs[j] + h[va]) / size[va]));
            }
            int w = us.Length;
            for (int j = 0; j < vs.Length - 1; j++)
            for (int i = 0; i < w - 1; i++)
            {
                int a = start + j * w + i, b = a + 1, c = a + w, d2 = c + 1;
                Tri(t, v, a, b, d2, fn); Tri(t, v, a, d2, c, fn);
            }
        }
        var mesh = new Mesh();
        mesh.SetVertices(v); mesh.SetNormals(n); mesh.SetUVs(0, uv); mesh.SetTriangles(t, 0);
        mesh.RecalculateBounds(); mesh.RecalculateTangents();
        return mesh;
    }

    static float[] Axis(float h, float r, int seg)
    {
        var l = new List<float>();
        for (int k = 0; k <= seg; k++) l.Add(-h + r - r * Mathf.Cos(Mathf.PI * 0.5f * k / seg));
        for (int k = 0; k <= seg; k++)
        {
            float x = (h - r) + r * Mathf.Sin(Mathf.PI * 0.5f * k / seg);
            if (k == 0 && Mathf.Abs(x - l[l.Count - 1]) < 1e-5f) continue;
            l.Add(x);
        }
        return l.ToArray();
    }

    static void Tri(List<int> t, List<Vector3> v, int a, int b, int c, Vector3 o)
    {
        Vector3 cr = Vector3.Cross(v[b] - v[a], v[c] - v[a]);
        if (cr.sqrMagnitude < 1e-16f) return;
        if (Vector3.Dot(cr, o) >= 0) { t.Add(a); t.Add(b); t.Add(c); } else { t.Add(a); t.Add(c); t.Add(b); }
    }
}
