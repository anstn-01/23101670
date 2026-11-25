// Tools > Build Machine (HQ)
// 둥근 모서리 메시를 직접 생성해서 기계 모델을 만듭니다.
// 씬에 "Machine" 오브젝트가 이미 있으면 그 위치/회전을 이어받아 교체합니다.
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public static class MachineBuilderHQ
{
    const string Root = "Assets/MachineMaterials";
    const string MeshDir = Root + "/Meshes";

    static Material cream, gray, rubber, steel, green, bezel, dark, screw;

    [MenuItem("Tools/Build Machine (HQ)")]
    public static void Build()
    {
        // 기존 Machine 교체 (위치 유지)
        Vector3 pos = Vector3.zero; Quaternion rot = Quaternion.identity;
        var old = GameObject.Find("Machine");
        if (old != null) { pos = old.transform.position; rot = old.transform.rotation; Object.DestroyImmediate(old); }

        EnsureFolder("Assets", "MachineMaterials");
        if (AssetDatabase.IsValidFolder(MeshDir)) AssetDatabase.DeleteAsset(MeshDir);
        EnsureFolder(Root, "Meshes");
        meshCounter = 0;

        cream  = Mat("M_Cream",  new Color(0.92f, 0.89f, 0.82f), 0f,  0.55f);
        gray   = Mat("M_Gray",   new Color(0.22f, 0.22f, 0.23f), 0f,  0.35f);
        rubber = Mat("M_Rubber", new Color(0.10f, 0.10f, 0.11f), 0f,  0.15f);
        steel  = Mat("M_Steel",  new Color(0.80f, 0.81f, 0.83f), 1f,  0.78f);
        bezel  = Mat("M_Bezel",  new Color(0.55f, 0.56f, 0.58f), 1f,  0.7f);
        dark   = Mat("M_Dark",   new Color(0.04f, 0.04f, 0.04f), 0f,  0.2f);
        screw  = Mat("M_Screw",  new Color(0.70f, 0.70f, 0.72f), 1f,  0.6f);
        green  = Mat("M_Green",  new Color(0.30f, 0.90f, 0.25f), 0f,  0.85f);
        green.EnableKeyword("_EMISSION");
        green.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        green.SetColor("_EmissionColor", new Color(0.15f, 0.9f, 0.1f) * 1.5f);

        var root = new GameObject("Machine");
        Undo.RegisterCreatedObjectUndo(root, "Build Machine HQ");
        root.transform.SetPositionAndRotation(pos, rot);
        var t = root.transform;

        // 앞면 = -Z, 기계 왼쪽 = -X
        const float zc = 0.08f;          // 기둥/팔/헤드의 깊이 중심 (약간 뒤쪽)
        const float bodyD = 0.60f;

        // ---------- 받침 ----------
        RBox(t, "Base_Gray",  new Vector3(-0.04f, 0.14f, 0f),  new Vector3(2.16f, 0.17f, 1.00f), 0.07f, gray);
        RBox(t, "Base_Cream", new Vector3( 0.04f, 0.29f, 0f),  new Vector3(1.94f, 0.17f, 0.97f), 0.07f, cream);

        // 스틸 판 + 나사
        RBox(t, "Plate", new Vector3(0.22f, 0.383f, -0.08f), new Vector3(1.30f, 0.016f, 0.58f), 0.025f, steel, 4);
        foreach (var p in new[] { new Vector2(-0.38f, -0.32f), new Vector2(0.82f, -0.32f),
                                  new Vector2(-0.38f,  0.16f), new Vector2(0.82f,  0.16f) })
            Screw(t, new Vector3(p.x, 0.392f, p.y), Vector3.up);

        // 고무 발
        foreach (var p in new[] { new Vector2(-0.86f, -0.36f), new Vector2(0.80f, -0.36f),
                                  new Vector2(-0.86f,  0.36f), new Vector2(0.80f,  0.36f) })
        {
            var f = RBox(t, "Foot", new Vector3(p.x, 0.035f, p.y), new Vector3(0.17f, 0.07f, 0.17f), 0.03f, rubber, 5);
            f.GetComponent<MeshFilter>().sharedMesh = SaveMesh(RoundedCylinder(0.085f, 0.07f, 0.02f, 32, 4), "Foot");
        }

        // ---------- 기둥 (회색 셸 + 크림 패널) ----------
        RBox(t, "Column_GrayShell", new Vector3(-0.90f, 0.80f, zc), new Vector3(0.44f, 1.42f, bodyD), 0.16f, gray, 8);
        RBox(t, "Column_Cream",     new Vector3(-0.74f, 0.86f, zc), new Vector3(0.36f, 1.12f, bodyD + 0.03f), 0.06f, cream);
        Screw(t, new Vector3(-0.82f, 1.33f, zc - (bodyD + 0.03f) / 2f), Vector3.back);
        Screw(t, new Vector3(-0.82f, 0.47f, zc - (bodyD + 0.03f) / 2f), Vector3.back);
        Screw(t, new Vector3(-0.82f, 1.33f, zc + (bodyD + 0.03f) / 2f), Vector3.forward);
        Screw(t, new Vector3(-0.82f, 0.47f, zc + (bodyD + 0.03f) / 2f), Vector3.forward);

        // 기둥-받침 / 기둥-팔 안쪽 필렛
        Fillet(t, "Fillet_Bottom", new Vector3(-0.56f, 0.375f, zc), 0.12f, bodyD - 0.02f, false);
        Fillet(t, "Fillet_Top",    new Vector3(-0.56f, 1.07f,  zc), 0.10f, bodyD - 0.06f, true);

        // ---------- 팔 ----------
        RBox(t, "Arm", new Vector3(-0.14f, 1.26f, zc), new Vector3(1.10f, 0.38f, bodyD - 0.04f), 0.10f, cream);

        // 초록 버튼 (베젤 + 돔)
        float armFront = zc - (bodyD - 0.04f) / 2f;
        var bz = Prim(PrimitiveType.Cylinder, t, "Button_Bezel", new Vector3(-0.02f, 1.25f, armFront - 0.008f),
                      new Vector3(0.115f, 0.01f, 0.115f), new Vector3(90, 0, 0), bezel);
        bz.GetComponent<MeshFilter>().sharedMesh = SaveMesh(RoundedCylinder(0.5f, 2f, 0.4f, 40, 3), "Bezel");
        Prim(PrimitiveType.Sphere, t, "Button_Green", new Vector3(-0.02f, 1.25f, armFront - 0.012f),
             new Vector3(0.085f, 0.085f, 0.045f), Vector3.zero, green);

        // ---------- 헤드 ----------
        RBox(t, "Head", new Vector3(0.64f, 1.17f, zc), new Vector3(0.84f, 0.72f, bodyD + 0.06f), 0.13f, cream, 8);
        // 헤드 아래 슬롯
        RBox(t, "Head_Slot", new Vector3(0.48f, 0.808f, zc - 0.12f), new Vector3(0.26f, 0.012f, 0.04f), 0.006f, dark, 2);

        // ---------- 보조 조명 (밤 씬에서 형태가 보이도록) ----------
        var lgo = new GameObject("Machine_KeyLight");
        lgo.transform.SetParent(t, false);
        lgo.transform.localPosition = new Vector3(-0.6f, 2.4f, -1.8f);
        var l = lgo.AddComponent<Light>();
        l.type = LightType.Point; l.range = 6f; l.intensity = 3f;
        l.color = new Color(1f, 0.96f, 0.9f); l.shadows = LightShadows.Soft;

        AssetDatabase.SaveAssets();
        Selection.activeGameObject = root;
        SceneView.lastActiveSceneView?.FrameSelected();
    }

    // ===================================================================
    static int meshCounter;

    static GameObject RBox(Transform parent, string name, Vector3 pos, Vector3 size, float r, Material m, int seg = 6)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.AddComponent<MeshFilter>().sharedMesh = SaveMesh(RoundedBox(size, r, seg), name);
        go.AddComponent<MeshRenderer>().sharedMaterial = m;
        return go;
    }

    static void Screw(Transform parent, Vector3 pos, Vector3 normal)
    {
        var go = Prim(PrimitiveType.Sphere, parent, "Screw", pos, new Vector3(0.042f, 0.042f, 0.016f), Vector3.zero, screw);
        go.transform.localRotation = Quaternion.LookRotation(normal);
        // 십자 홈
        var s1 = Prim(PrimitiveType.Cube, go.transform, "Slot", new Vector3(0, 0, 0.45f), new Vector3(0.7f, 0.12f, 0.2f), Vector3.zero, dark);
        var s2 = Prim(PrimitiveType.Cube, go.transform, "Slot", new Vector3(0, 0, 0.45f), new Vector3(0.12f, 0.7f, 0.2f), Vector3.zero, dark);
    }

    // 안쪽 모서리를 부드럽게 채우는 1/4 원 필렛 (Z방향으로 뻗음)
    static void Fillet(Transform parent, string name, Vector3 corner, float r, float depth, bool top)
    {
        var verts = new List<Vector3>(); var norms = new List<Vector3>(); var tris = new List<int>();
        int n = 12; float hz = depth / 2f;
        // 오목 곡면: 중심 (r, ±r), 기둥 면(x=0)과 받침/팔 면(y=0) 사이
        float sy = top ? -1f : 1f;
        Vector3 c = new Vector3(r, sy * r, 0);
        for (int i = 0; i <= n; i++)
        {
            float a = Mathf.PI * 0.5f * i / n;
            // 원 위의 점: 기둥면(0, sy*r) → 바닥면(r, 0)
            Vector3 p = c + new Vector3(-Mathf.Cos(a) * r, -sy * Mathf.Sin(a) * r, 0);
            Vector3 nn = (c - p).normalized;
            verts.Add(new Vector3(p.x, p.y, -hz)); norms.Add(nn);
            verts.Add(new Vector3(p.x, p.y,  hz)); norms.Add(nn);
        }
        for (int i = 0; i < n; i++)
        {
            int a = i * 2, b = a + 1, cc = a + 2, d = a + 3;
            AddQuad(tris, verts, a, cc, b, d, norms[a]);
        }
        // 측면 캡
        int baseIdx = verts.Count;
        foreach (float zz in new[] { -hz, hz })
        {
            int start = verts.Count;
            Vector3 capN = zz < 0 ? Vector3.back : Vector3.forward;
            verts.Add(new Vector3(0, 0, zz)); norms.Add(capN);
            for (int i = 0; i <= n; i++)
            {
                float a = Mathf.PI * 0.5f * i / n;
                Vector3 p = c + new Vector3(-Mathf.Cos(a) * r, -sy * Mathf.Sin(a) * r, 0);
                verts.Add(new Vector3(p.x, p.y, zz)); norms.Add(capN);
            }
            for (int i = 0; i < n; i++) AddTri(tris, verts, start, start + 1 + i, start + 2 + i, capN);
        }
        var mesh = new Mesh { name = name };
        mesh.SetVertices(verts); mesh.SetNormals(norms); mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds(); mesh.RecalculateTangents();

        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = corner;
        go.AddComponent<MeshFilter>().sharedMesh = SaveMesh(mesh, name);
        go.AddComponent<MeshRenderer>().sharedMaterial = cream;
    }

    // ---------- 둥근 박스 메시 ----------
    static Mesh RoundedBox(Vector3 size, float r, int seg)
    {
        Vector3 h = size * 0.5f;
        r = Mathf.Min(r, Mathf.Min(h.x, Mathf.Min(h.y, h.z)) * 0.98f);
        Vector3 inner = h - Vector3.one * r;
        float[][] ax = { Axis(h.x, r, seg), Axis(h.y, r, seg), Axis(h.z, r, seg) };

        var verts = new List<Vector3>(); var norms = new List<Vector3>(); var uvs = new List<Vector2>(); var tris = new List<int>();

        for (int axis = 0; axis < 3; axis++)
        for (int sign = -1; sign <= 1; sign += 2)
        {
            int ua = (axis + 1) % 3, va = (axis + 2) % 3;
            float[] us = ax[ua], vs = ax[va];
            int start = verts.Count;
            Vector3 faceN = Vector3.zero; faceN[axis] = sign;

            for (int j = 0; j < vs.Length; j++)
            for (int i = 0; i < us.Length; i++)
            {
                Vector3 p = Vector3.zero;
                p[axis] = sign * h[axis]; p[ua] = us[i]; p[va] = vs[j];
                Vector3 inn = new Vector3(Mathf.Clamp(p.x, -inner.x, inner.x),
                                          Mathf.Clamp(p.y, -inner.y, inner.y),
                                          Mathf.Clamp(p.z, -inner.z, inner.z));
                Vector3 d = p - inn;
                Vector3 nrm = d.sqrMagnitude < 1e-12f ? faceN : d.normalized;
                verts.Add(inn + nrm * r);
                norms.Add(nrm);
                uvs.Add(new Vector2((us[i] + h[ua]) / size[ua], (vs[j] + h[va]) / size[va]));
            }
            int w = us.Length;
            for (int j = 0; j < vs.Length - 1; j++)
            for (int i = 0; i < w - 1; i++)
            {
                int a = start + j * w + i, b = a + 1, c = a + w, d2 = c + 1;
                AddQuad(tris, verts, a, b, c, d2, faceN);
            }
        }
        var mesh = new Mesh();
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(verts); mesh.SetNormals(norms); mesh.SetUVs(0, uvs); mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds(); mesh.RecalculateTangents();
        return mesh;
    }

    static float[] Axis(float h, float r, int seg)
    {
        var l = new List<float>();
        for (int k = 0; k <= seg; k++) l.Add(-h + r - r * Mathf.Cos(Mathf.PI * 0.5f * k / seg));
        for (int k = 0; k <= seg; k++)
        {
            float v = (h - r) + r * Mathf.Sin(Mathf.PI * 0.5f * k / seg);
            if (k == 0 && Mathf.Abs(v - l[l.Count - 1]) < 1e-5f) continue;
            l.Add(v);
        }
        return l.ToArray();
    }

    // ---------- 모서리가 둥근 원기둥 (Y축) ----------
    static Mesh RoundedCylinder(float radius, float height, float edge, int sides, int seg)
    {
        edge = Mathf.Min(edge, Mathf.Min(radius, height / 2f) * 0.98f);
        var profile = new List<Vector2>(); var pn = new List<Vector2>();
        float hy = height / 2f;
        profile.Add(new Vector2(0, -hy)); pn.Add(Vector2.down);
        for (int k = 0; k <= seg; k++)
        {
            float a = -Mathf.PI / 2 + Mathf.PI / 2 * k / seg;
            var n = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            profile.Add(new Vector2(radius - edge, -hy + edge) + n * edge); pn.Add(k == 0 ? Vector2.down : n);
        }
        for (int k = 0; k <= seg; k++)
        {
            float a = Mathf.PI / 2 * k / seg;
            var n = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            profile.Add(new Vector2(radius - edge, hy - edge) + n * edge); pn.Add(k == seg ? Vector2.up : n);
        }
        profile.Add(new Vector2(0, hy)); pn.Add(Vector2.up);

        var verts = new List<Vector3>(); var norms = new List<Vector3>(); var tris = new List<int>();
        int pc = profile.Count;
        for (int s = 0; s <= sides; s++)
        {
            float a = 2 * Mathf.PI * s / sides;
            float cs = Mathf.Cos(a), sn = Mathf.Sin(a);
            for (int k = 0; k < pc; k++)
            {
                verts.Add(new Vector3(profile[k].x * cs, profile[k].y, profile[k].x * sn));
                norms.Add(new Vector3(pn[k].x * cs, pn[k].y, pn[k].x * sn).normalized);
            }
        }
        for (int s = 0; s < sides; s++)
        for (int k = 0; k < pc - 1; k++)
        {
            int a = s * pc + k, b = a + 1, c = a + pc, d = c + 1;
            Vector3 outward = (norms[a] + norms[b] + norms[c] + norms[d]).normalized;
            AddQuad(tris, verts, a, b, c, d, outward);
        }
        var mesh = new Mesh();
        mesh.SetVertices(verts); mesh.SetNormals(norms); mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds(); mesh.RecalculateTangents();
        return mesh;
    }

    // 쿼드를 바깥 방향(outN)에 맞는 감김 순서로 추가 (Unity: 시계방향 = 앞면)
    static void AddQuad(List<int> tris, List<Vector3> v, int a, int b, int c, int d, Vector3 outN)
    {
        AddTri(tris, v, a, b, d, outN);
        AddTri(tris, v, a, d, c, outN);
    }

    static void AddTri(List<int> tris, List<Vector3> v, int a, int b, int c, Vector3 outN)
    {
        Vector3 n = Vector3.Cross(v[b] - v[a], v[c] - v[a]);
        if (n.sqrMagnitude < 1e-16f) return;
        if (Vector3.Dot(n, outN) >= 0) { tris.Add(a); tris.Add(b); tris.Add(c); }
        else { tris.Add(a); tris.Add(c); tris.Add(b); }
    }

    // ---------- 공통 ----------
    static Mesh SaveMesh(Mesh mesh, string name)
    {
        string path = $"{MeshDir}/{name}_{meshCounter++}.asset";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null) AssetDatabase.DeleteAsset(path);
        mesh.name = name;
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    static GameObject Prim(PrimitiveType type, Transform parent, string name, Vector3 pos, Vector3 scale, Vector3 rot, Material m)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localEulerAngles = rot;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = m;
        return go;
    }

    static Material Mat(string name, Color c, float metallic, float smooth)
    {
        string path = $"{Root}/{name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("HDRP/Lit") ?? Shader.Find("Standard");
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", c);
        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smooth);
        if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", smooth);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static void EnsureFolder(string parent, string name)
    {
        if (!AssetDatabase.IsValidFolder($"{parent}/{name}")) AssetDatabase.CreateFolder(parent, name);
    }
}
