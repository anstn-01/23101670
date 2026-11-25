// Tools > Build Side Belt + Place Tape Machine
// 1) 출력 테이블 왼쪽에 벨트 컨베이어(본체 없음)를 하나 더 만듭니다.
// 2) 테이프 기계(Machine)를 작게 줄여 테이블 위에 올립니다.
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public static class SideBeltBuilder
{
    const string Root = "Assets/ConveyorMaterials";
    const string MeshDir = Root + "/BeltMeshes";
    const float MachineScale = 0.5f;
    static int meshCounter;
    static Material metal, metalDark, black, rubber, white;

    [MenuItem("Tools/Build Side Belt + Place Tape Machine")]
    static void Build()
    {
        var conv = GameObject.Find("Conveyor_BD01");
        if (conv == null) { EditorUtility.DisplayDialog("Belt", "Conveyor_BD01이 없어요.", "OK"); return; }
        var table = conv.transform.Find("OutputTable");

        metal     = Load("C_Metal",     new Color(0.82f, 0.83f, 0.84f), 0.12f, 0.45f);
        metalDark = Load("C_MetalDark", new Color(0.34f, 0.35f, 0.37f), 0.2f, 0.4f);
        black     = Load("C_Black",     new Color(0.06f, 0.06f, 0.07f), 0.2f, 0.3f);
        rubber    = Load("C_Belt",      new Color(0.08f, 0.08f, 0.09f), 0f, 0.25f);
        white     = Load("C_White",     new Color(0.95f, 0.95f, 0.95f), 0f, 0.4f);

        if (AssetDatabase.IsValidFolder(MeshDir)) AssetDatabase.DeleteAsset(MeshDir);
        AssetDatabase.CreateFolder(Root, "BeltMeshes");
        meshCounter = 0;

        // ---------- 1) 벨트 ----------
        var old = conv.transform.Find("SideBelt");
        if (old != null) Object.DestroyImmediate(old.gameObject);
        var belt = new GameObject("SideBelt").transform;
        Undo.RegisterCreatedObjectUndo(belt.gameObject, "Build Side Belt");
        belt.SetParent(conv.transform, false);
        const float len = 2.3f;
        float tableLeft = table != null ? table.localPosition.x - 0.6f : -1.24f;   // 테이블 왼쪽 끝
        belt.localPosition = new Vector3(tableLeft - 0.04f - len, 0, 0);
        BuildBelt(belt, len);

        // ---------- 2) 테이프 기계를 테이블 위로 ----------
        var machine = GameObject.Find("Machine");
        if (machine != null && table != null)
        {
            Undo.RecordObject(machine.transform, "Place Tape Machine");
            machine.transform.localScale = Vector3.one * MachineScale;
            machine.transform.rotation = table.rotation;
            // 기계 받침 중심(로컬 x≈-0.04)이 테이블 가운데에 오도록, 상판(0.754m) 위에
            Vector3 top = table.TransformPoint(new Vector3(0, 0.755f, 0));
            machine.transform.position = top - table.rotation * new Vector3(-0.04f * MachineScale, 0, 0);
        }

        AssetDatabase.SaveAssets();
        Selection.activeGameObject = belt.gameObject;
        Debug.Log("[SideBelt] 벨트 생성 + 테이프 기계 테이블 위 배치 완료");
    }

    static void BuildBelt(Transform t, float len)
    {
        const float beltTop = 0.75f, halfW = 0.30f;
        RBox(t, "Belt", new Vector3(len * 0.5f, beltTop - 0.025f, 0), new Vector3(len - 0.12f, 0.05f, halfW * 2f), 0.02f, rubber).AddComponent<BoxCollider>();
        for (float x = 0.15f; x < len - 0.1f; x += 0.12f)
            Box(t, "BeltRib", new Vector3(x, beltTop + 0.001f, 0), new Vector3(0.012f, 0.002f, halfW * 2f - 0.02f), black);
        foreach (float rx in new[] { 0.08f, len - 0.08f })
            Cyl(t, "Roller", new Vector3(rx, beltTop - 0.045f, 0), 0.065f, halfW * 2f + 0.02f, Vector3.right * 90f, metalDark);

        foreach (int s in new[] { -1, 1 })
        {
            float z = s * (halfW + 0.035f);
            RBox(t, "Rail", new Vector3(len * 0.5f, beltTop - 0.04f, z), new Vector3(len, 0.13f, 0.05f), 0.015f, metal).AddComponent<BoxCollider>();
            RBox(t, "RailLip", new Vector3(len * 0.5f, beltTop + 0.03f, z), new Vector3(len, 0.02f, 0.07f), 0.008f, metal);
            RBox(t, "RailStripe", new Vector3(len * 0.5f, beltTop - 0.07f, z + s * 0.027f), new Vector3(len - 0.08f, 0.05f, 0.004f), 0.002f, black);
            foreach (float cx in new[] { 0.45f, len - 0.45f })
                for (int k = 0; k < 3; k++)
                    for (int d = -1; d <= 1; d += 2)
                    {
                        var b = Box(t, "Chevron", new Vector3(cx + k * 0.06f, beltTop - 0.07f + d * 0.011f, z + s * 0.031f), new Vector3(0.04f, 0.009f, 0.003f), white);
                        b.transform.localRotation = Quaternion.Euler(0, 0, d * 35f);
                    }
            foreach (float bx in new[] { 0.12f, len - 0.12f })
                foreach (float by in new[] { -0.08f, 0f })
                {
                    var bolt = Prim(PrimitiveType.Sphere, t, "Bolt", new Vector3(bx, beltTop + by, z + s * 0.028f), new Vector3(0.018f, 0.018f, 0.007f), Vector3.zero, metalDark);
                    bolt.transform.localRotation = Quaternion.LookRotation(new Vector3(0, 0, s));
                }
        }

        foreach (float lx in new[] { 0.28f, len - 0.28f })
        {
            foreach (int s in new[] { -1, 1 })
            {
                float z = s * 0.3f;
                RBox(t, "Leg", new Vector3(lx, 0.36f, z), new Vector3(0.08f, 0.64f, 0.08f), 0.012f, metal).AddComponent<BoxCollider>();
                RBox(t, "Foot", new Vector3(lx, 0.03f, z), new Vector3(0.14f, 0.06f, 0.14f), 0.015f, black);
                RBox(t, "LegBracket", new Vector3(lx, beltTop - 0.1f, z + s * 0.02f), new Vector3(0.12f, 0.08f, 0.03f), 0.008f, metalDark);
            }
            RBox(t, "CrossBrace", new Vector3(lx, 0.22f, 0), new Vector3(0.05f, 0.05f, 0.6f), 0.008f, metalDark);
        }
        foreach (int s in new[] { -1, 1 })
            RBox(t, "LowerRail", new Vector3(len * 0.5f, 0.22f, s * 0.3f), new Vector3(len - 0.56f, 0.05f, 0.05f), 0.008f, metalDark);
    }

    // ---------- 헬퍼 ----------
    static Material Load(string name, Color c, float metallic, float smooth)
    {
        string path = $"{Root}/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m != null) return m;
        m = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    static GameObject RBox(Transform parent, string name, Vector3 pos, Vector3 size, float r, Material m)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        var mesh = RoundedBox(size, r, 3); mesh.name = name;
        AssetDatabase.CreateAsset(mesh, $"{MeshDir}/{name}_{meshCounter++}.asset");
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = m;
        return go;
    }

    static GameObject Box(Transform p, string name, Vector3 pos, Vector3 size, Material m)
        => Prim(PrimitiveType.Cube, p, name, pos, size, Vector3.zero, m);

    static GameObject Cyl(Transform p, string name, Vector3 pos, float radius, float length, Vector3 rot, Material m)
        => Prim(PrimitiveType.Cylinder, p, name, pos, new Vector3(radius * 2f, length * 0.5f, radius * 2f), rot, m);

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

    static Mesh RoundedBox(Vector3 size, float r, int seg)
    {
        Vector3 h = size * 0.5f;
        r = Mathf.Min(r, Mathf.Min(h.x, Mathf.Min(h.y, h.z)) * 0.98f);
        Vector3 ih = h - Vector3.one * r;
        float[][] ax = { Axis(h.x, r, seg), Axis(h.y, r, seg), Axis(h.z, r, seg) };
        var v = new List<Vector3>(); var n = new List<Vector3>(); var t = new List<int>();
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
                v.Add(inn + nr * r); n.Add(nr);
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
        mesh.SetVertices(v); mesh.SetNormals(n); mesh.SetTriangles(t, 0);
        mesh.RecalculateBounds();
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
