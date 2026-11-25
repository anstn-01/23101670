// Tools > Build Vertical Chute VD-01
// 테이블 위 천장형 수직 배출 슈트(VD-01). 본체 + 위로 천장까지 이어지는 덕트.
// 참고 규격(mm): 본체 1000x1000x400, 덕트 600x600, 배출구 800x800  → 테이블에 맞춰 0.8배
#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public static class ChuteBuilder
{
    const string Root = "Assets/ConveyorMaterials";
    const string MeshDir = Root + "/ChuteMeshes";
    const float S = 0.8f;                         // 규격 대비 배율
    static float W => 1.0f * S;                   // 본체 폭/깊이
    static float H => 0.4f * S;                   // 본체 높이
    static float D => 0.6f * S;                   // 덕트
    static float O => 0.8f * S;                   // 배출구 내부
    const float T = 0.03f;                        // 벽 두께
    const float BottomFromFloor = 1.6f;           // 바닥에서 본체 밑면까지
    static int meshCounter;

    static Material metal, metalDark, dark, yellow, black, glow, inner;

    [MenuItem("Tools/Build Vertical Chute VD-01")]
    static void Build()
    {
        var conv = GameObject.Find("Conveyor_BD01");
        var table = conv ? conv.transform.Find("OutputTable") : null;
        if (table == null) { Debug.LogWarning("[Chute] OutputTable이 없어요."); return; }

        var old = GameObject.Find("Chute_VD01");
        if (old) Undo.DestroyObjectImmediate(old);
        if (!AssetDatabase.IsValidFolder(Root)) AssetDatabase.CreateFolder("Assets", "ConveyorMaterials");
        if (AssetDatabase.IsValidFolder(MeshDir)) AssetDatabase.DeleteAsset(MeshDir);
        AssetDatabase.CreateFolder(Root, "ChuteMeshes");
        meshCounter = 0;

        metal     = Mat("VD_Metal",     new Color(0.56f, 0.57f, 0.59f), 0.75f, 0.55f);
        metalDark = Mat("VD_MetalDark", new Color(0.36f, 0.37f, 0.39f), 0.7f, 0.5f);
        dark      = Mat("VD_Dark",      new Color(0.10f, 0.10f, 0.11f), 0.3f, 0.35f);
        inner     = Mat("VD_Inner",     new Color(0.17f, 0.17f, 0.18f), 0.4f, 0.3f);
        yellow    = Mat("VD_Yellow",    new Color(1.00f, 0.74f, 0.05f), 0f, 0.5f);
        black     = Mat("VD_Black",     new Color(0.05f, 0.05f, 0.05f), 0f, 0.4f);
        glow      = Mat("VD_Glow",      new Color(1.00f, 0.88f, 0.62f), 0f, 0.8f, new Color(4.0f, 3.1f, 1.8f));

        // 위치: 테이블 위, 테이프 기계 반대쪽
        float cx = 0.55f;
        var machine = GameObject.Find("Machine");
        if (machine && table.InverseTransformPoint(machine.transform.position).x > 0) cx = -0.55f;

        var root = new GameObject("Chute_VD01").transform;
        Undo.RegisterCreatedObjectUndo(root.gameObject, "Build Chute");
        root.rotation = table.rotation;
        Vector3 basePos = table.TransformPoint(new Vector3(cx, 0, 0));
        root.position = basePos + Vector3.up * BottomFromFloor;

        // 천장 높이 찾기
        float ceilY = FindCeiling(root.position + Vector3.up * (H + 0.1f), conv.transform);
        float ductLen = Mathf.Max(0.3f, ceilY - root.position.y - H - 0.02f);

        BuildBody(root);
        BuildBottom(root);
        BuildDuct(root, ductLen);

        var drop = new GameObject("DropPoint").transform;
        drop.SetParent(root, false);
        drop.localPosition = new Vector3(0, H * 0.5f, 0);

        AssetDatabase.SaveAssets();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        Selection.activeGameObject = root.gameObject;
        Debug.Log($"[Chute] VD-01 생성 완료 (밑면 {BottomFromFloor:F2}m, 덕트 {ductLen:F2}m → 천장)");
    }

    // ---------------- 본체 ----------------
    static void BuildBody(Transform root)
    {
        var body = Child(root, "Body");

        // 4면 외벽 + 모서리 기둥
        for (int k = 0; k < 4; k++)
        {
            var f = Face(body, k, W * 0.5f);
            RBox(f, "Wall", new Vector3(0, H * 0.5f, -T * 0.5f), new Vector3(W - 0.02f, H, T), 0.01f, metal, 2).AddComponent<BoxCollider>();
            RBox(f, "InnerLiner", new Vector3(0, H * 0.5f, -T - 0.002f), new Vector3(W - 2 * T - 0.004f, H - 0.01f, 0.004f), 0.001f, inner, 1);
            for (int r = -1; r <= 1; r++)   // 안쪽 세로 리브
                Box(f, "Rib", new Vector3(r * 0.18f, H * 0.5f, -T - 0.012f), new Vector3(0.02f, H - 0.03f, 0.02f), metalDark);

            // 위쪽 환기구
            float vy = H * 0.62f, vw = W * 0.5f, vh = H * 0.36f;
            RBox(f, "VentFrame", new Vector3(0, vy, 0.006f), new Vector3(vw + 0.04f, vh + 0.04f, 0.012f), 0.006f, metalDark, 2);
            Box(f, "VentHole", new Vector3(0, vy, 0.0125f), new Vector3(vw, vh, 0.002f), black);
            for (int s = 0; s < 6; s++)
                Box(f, "Louver", new Vector3(0, vy - vh * 0.5f + vh * (s + 0.5f) / 6f, 0.018f), new Vector3(vw - 0.01f, 0.007f, 0.018f), metal, new Vector3(-35f, 0, 0));

            // 아래 경고 띠 (노랑/검정 사선)
            float by = 0.055f, bh = 0.07f;
            Box(f, "HazardBand", new Vector3(0, by, 0.004f), new Vector3(W - 0.03f, bh, 0.006f), yellow);
            for (float x = -W * 0.5f + 0.06f; x < W * 0.5f - 0.04f; x += 0.075f)
                Box(f, "HazardStripe", new Vector3(x, by, 0.0075f), new Vector3(0.028f, bh * 1.2f, 0.002f), black, new Vector3(0, 0, 40f));

            // 볼트
            foreach (int sx in new[] { -1, 1 })
                foreach (float yy in new[] { 0.125f, H - 0.03f })
                    Bolt(f, new Vector3(sx * (W * 0.5f - 0.045f), yy, 0.003f));
        }
        for (int k = 0; k < 4; k++)
        {
            float a = k * 90f * Mathf.Deg2Rad;
            var p = new Vector3(Mathf.Sin(a + Mathf.PI / 4) , 0, Mathf.Cos(a + Mathf.PI / 4)) * (W * 0.5f - 0.012f) * 1.4142f;
            RBox(body, "CornerPost", new Vector3(p.x, H * 0.5f, p.z), new Vector3(0.045f, H + 0.012f, 0.045f), 0.015f, metalDark, 3);
        }

        // 윗판 + 계단식 윗테두리
        RBox(body, "TopPlate", new Vector3(0, H - T * 0.5f, 0), new Vector3(W, T, W), 0.012f, metal, 2).AddComponent<BoxCollider>();
        RBox(body, "TopStep", new Vector3(0, H + 0.01f, 0), new Vector3(W - 0.08f, 0.02f, W - 0.08f), 0.01f, metal, 2);
        Box(body, "InnerCeiling", new Vector3(0, H - T - 0.002f, 0), new Vector3(W - 2 * T, 0.003f, W - 2 * T), inner);
    }

    // ---------------- 하단 배출구 ----------------
    static void BuildBottom(Transform root)
    {
        var bot = Child(root, "Outlet");
        float fw = (W - O) * 0.5f;
        for (int k = 0; k < 4; k++)
        {
            var f = Face(bot, k, W * 0.5f);
            RBox(f, "RingBar", new Vector3(0, 0.012f, -fw * 0.5f), new Vector3(W, 0.024f, fw), 0.008f, metalDark, 2);
            // 배출구 안쪽 조명
            RBox(f, "LightStrip", new Vector3(0, -0.002f, -fw * 0.5f), new Vector3(O * 0.62f, 0.006f, fw * 0.45f), 0.002f, glow, 1);
            RBox(f, "LightBezel", new Vector3(0, -0.001f, -fw * 0.5f), new Vector3(O * 0.66f, 0.004f, fw * 0.62f), 0.002f, black, 1);
            foreach (int sx in new[] { -1, 1 })
            {
                var b = Bolt(f, new Vector3(sx * (W * 0.5f - fw * 0.5f), -0.002f, -fw * 0.5f));
                b.transform.localRotation = Quaternion.Euler(90, 0, 0);
            }
        }

        // 아래를 비추는 조명
        var lgo = new GameObject("OutletLight");
        lgo.transform.SetParent(bot, false);
        lgo.transform.localPosition = new Vector3(0, 0.02f, 0);
        lgo.transform.localRotation = Quaternion.Euler(90, 0, 0);
        var l = lgo.AddComponent<Light>();
        l.type = LightType.Spot; l.spotAngle = 110f; l.innerSpotAngle = 60f;
        l.range = 2.2f; l.intensity = 2.2f; l.color = new Color(1f, 0.86f, 0.62f);
        l.shadows = LightShadows.None;
    }

    // ---------------- 덕트 ----------------
    static void BuildDuct(Transform root, float len)
    {
        var duct = Child(root, "Duct");
        float y0 = H + 0.02f, t = 0.02f;
        for (int k = 0; k < 4; k++)
        {
            var f = Face(duct, k, D * 0.5f);
            RBox(f, "DuctWall", new Vector3(0, y0 + len * 0.5f, -t * 0.5f), new Vector3(D, len, t), 0.006f, metal, 2).AddComponent<BoxCollider>();
        }
        for (int k = 0; k < 4; k++)
        {
            float a = (k * 90f + 45f) * Mathf.Deg2Rad;
            var p = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)) * (D * 0.5f) * 1.4142f;
            RBox(duct, "DuctEdge", new Vector3(p.x, y0 + len * 0.5f, p.z), new Vector3(0.03f, len, 0.03f), 0.01f, metalDark, 2);
        }
        RBox(duct, "BaseFlange", new Vector3(0, y0 + 0.02f, 0), new Vector3(D + 0.07f, 0.04f, D + 0.07f), 0.01f, metalDark, 2);
        for (float y = 0.9f; y < len - 0.25f; y += 0.9f)
        {
            RBox(duct, "Joint", new Vector3(0, y0 + y, 0), new Vector3(D + 0.03f, 0.05f, D + 0.03f), 0.01f, metalDark, 2);
            for (int k = 0; k < 4; k++)
            {
                var f = Face(duct, k, D * 0.5f + 0.015f);
                foreach (int sx in new[] { -1, 1 }) Bolt(f, new Vector3(sx * D * 0.35f, y0 + y, 0.002f));
            }
        }
        RBox(duct, "CeilingPlate", new Vector3(0, y0 + len - 0.01f, 0), new Vector3(D + 0.2f, 0.02f, D + 0.2f), 0.008f, metalDark, 2);
    }

    // ---------------- 헬퍼 ----------------
    static float FindCeiling(Vector3 from, Transform skip)
    {
        var temp = new List<Collider>();
        var factory = GameObject.Find("Parking Factory");
        if (factory)
            foreach (var mf in factory.GetComponentsInChildren<MeshFilter>())
                if (mf.GetComponent<Collider>() == null && mf.sharedMesh != null)
                    temp.Add(mf.gameObject.AddComponent<MeshCollider>());
        Physics.SyncTransforms();
        float best = float.MaxValue;
        foreach (var h in Physics.RaycastAll(from, Vector3.up, 50f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (h.transform.IsChildOf(skip)) continue;
            if (h.collider.GetComponentInParent<Light>() != null) continue;
            if (h.distance < best) best = h.distance;
        }
        foreach (var c in temp) Object.DestroyImmediate(c);
        if (best < float.MaxValue) return from.y + best;

        var roof = GameObject.Find("Roof");
        if (roof && roof.GetComponentInChildren<Renderer>() is Renderer rr) return rr.bounds.min.y;
        return from.y + 2.0f;
    }

    static Transform Child(Transform p, string name)
    {
        var t = new GameObject(name).transform; t.SetParent(p, false); return t;
    }

    // k번째 면: +Z 가 바깥쪽을 향하고, 원점이 그 면 위에 있는 피벗
    static Transform Face(Transform p, int k, float dist)
    {
        var t = new GameObject("Face" + k).transform;
        t.SetParent(p, false);
        t.localRotation = Quaternion.Euler(0, k * 90f, 0);
        t.localPosition = t.localRotation * new Vector3(0, 0, dist);
        return t;
    }

    static GameObject Bolt(Transform p, Vector3 pos)
    {
        var b = Box(p, "Bolt", pos, new Vector3(0.016f, 0.016f, 0.008f), metalDark, Vector3.zero, PrimitiveType.Sphere);
        return b;
    }

    static GameObject Box(Transform p, string name, Vector3 pos, Vector3 size, Material m, Vector3 rot = default, PrimitiveType type = PrimitiveType.Cube)
    {
        var g = GameObject.CreatePrimitive(type);
        g.name = name;
        Object.DestroyImmediate(g.GetComponent<Collider>());
        g.transform.SetParent(p, false);
        g.transform.localPosition = pos;
        g.transform.localRotation = Quaternion.Euler(rot);
        g.transform.localScale = size;
        g.GetComponent<MeshRenderer>().sharedMaterial = m;
        return g;
    }

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

    static Material Mat(string name, Color c, float metallic, float smooth, Color? emission = null)
    {
        string path = $"{Root}/{name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", c);
        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smooth);
        if (emission.HasValue)
        {
            mat.EnableKeyword("_EMISSION");
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            mat.SetColor("_EmissionColor", emission.Value);
        }
        EditorUtility.SetDirty(mat);
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
#endif
