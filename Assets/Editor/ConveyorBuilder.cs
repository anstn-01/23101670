// Tools > Build Conveyor BD-01
// 자동 배출 컨베이어 기계(BD-01)를 치수도(mm) 기준으로 생성합니다.
// 전체 길이 3200 / 컨베이어 2300 / 본체 900 x 1000 x 1600 / 벨트 높이 750
// 씬 뷰 카메라가 보고 있는 바닥 위치에 생성됩니다. (기존 Conveyor_BD01은 교체)
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public static class ConveyorBuilder
{
    const string Root = "Assets/ConveyorMaterials";
    const string MeshDir = Root + "/Meshes";
    static int meshCounter;
    static Material metal, metalDark, black, rubber, orange, cap, white, red, yellow, green, inner;

    [MenuItem("Tools/Build Conveyor BD-01")]
    public static void Build()
    {
        // 기존 교체 (위치 유지)
        Vector3 pos; Quaternion rot;
        var old = GameObject.Find("Conveyor_BD01");
        if (old != null) { pos = old.transform.position; rot = old.transform.rotation; Object.DestroyImmediate(old); }
        else FindSpawn(out pos, out rot);

        if (!AssetDatabase.IsValidFolder(Root)) AssetDatabase.CreateFolder("Assets", "ConveyorMaterials");
        if (AssetDatabase.IsValidFolder(MeshDir)) AssetDatabase.DeleteAsset(MeshDir);
        AssetDatabase.CreateFolder(Root, "Meshes");
        meshCounter = 0;

        metal     = Mat("C_Metal",     new Color(0.82f, 0.83f, 0.84f), 0.12f, 0.45f);
        metalDark = Mat("C_MetalDark", new Color(0.34f, 0.35f, 0.37f), 0.2f,  0.4f);
        black     = Mat("C_Black",     new Color(0.06f, 0.06f, 0.07f), 0.2f,  0.3f);
        rubber    = Mat("C_Belt",      new Color(0.08f, 0.08f, 0.09f), 0f,    0.25f);
        orange    = Mat("C_Orange",    new Color(1.00f, 0.45f, 0.05f), 0f,    0.55f);
        cap       = Mat("C_Cap",       new Color(1.00f, 0.66f, 0.08f), 0.1f,  0.6f);
        white     = Mat("C_White",     new Color(0.95f, 0.95f, 0.95f), 0f,    0.4f);
        inner     = Mat("C_Inner",     new Color(0.10f, 0.10f, 0.11f), 0.3f,  0.3f);
        red       = Lamp("C_LampRed",    new Color(1f, 0.1f, 0.08f));
        yellow    = Lamp("C_LampYellow", new Color(1f, 0.8f, 0.1f));
        green     = Lamp("C_LampGreen",  new Color(0.15f, 1f, 0.2f));

        var root = new GameObject("Conveyor_BD01");
        Undo.RegisterCreatedObjectUndo(root, "Build Conveyor BD-01");
        root.transform.SetPositionAndRotation(pos, rot);
        var t = root.transform;

        // 좌표: X = 길이(0 = 컨베이어 왼쪽 끝, 3.2 = 본체 오른쪽 끝), Y = 위, Z = 폭(-Z가 정면/라벨 쪽)
        BuildConveyor(t);
        BuildCabinet(t);

        // 보조 조명 (어두운 씬에서 형태가 보이도록)
        var lgo = new GameObject("BD01_KeyLight");
        lgo.transform.SetParent(t, false);
        lgo.transform.localPosition = new Vector3(1.4f, 2.6f, -2.2f);
        var l = lgo.AddComponent<Light>();
        l.type = LightType.Point; l.range = 7f; l.intensity = 4f;
        l.color = new Color(1f, 0.97f, 0.92f); l.shadows = LightShadows.Soft;

        AssetDatabase.SaveAssets();
        Selection.activeGameObject = root;
        SceneView.lastActiveSceneView?.FrameSelected();
    }

    // ------------------------------------------------------------------ 컨베이어
    static void BuildConveyor(Transform t)
    {
        var conv = Group(t, "Conveyor");
        const float beltTop = 0.75f, halfW = 0.30f;

        // 벨트 (본체 터널 안쪽까지 이어짐)
        var belt = RBox(conv, "Belt", new Vector3(1.38f, beltTop - 0.025f, 0f), new Vector3(2.56f, 0.05f, halfW * 2f), 0.02f, rubber, 4);
        belt.AddComponent<BoxCollider>();
        // 벨트 줄무늬 (진행 느낌)
        for (float x = 0.25f; x < 2.6f; x += 0.12f)
            Box(conv, "BeltRib", new Vector3(x, beltTop + 0.001f, 0f), new Vector3(0.012f, 0.002f, halfW * 2f - 0.02f), black);

        // 끝 롤러
        Cyl(conv, "Roller_L", new Vector3(0.10f, beltTop - 0.045f, 0f), 0.065f, halfW * 2f + 0.02f, Vector3.right * 90f, metalDark);

        // 사이드 레일 (앞/뒤)
        foreach (int s in new[] { -1, 1 })
        {
            float z = s * (halfW + 0.035f);
            var rail = RBox(conv, s < 0 ? "Rail_Front" : "Rail_Back", new Vector3(1.22f, beltTop - 0.04f, z), new Vector3(2.26f, 0.13f, 0.05f), 0.015f, metal, 3);
            rail.AddComponent<BoxCollider>();
            RBox(conv, "RailLip", new Vector3(1.22f, beltTop + 0.03f, z), new Vector3(2.26f, 0.02f, 0.07f), 0.008f, metal, 2);
            RBox(conv, "RailStripe", new Vector3(1.22f, beltTop - 0.07f, z + s * 0.027f), new Vector3(2.2f, 0.05f, 0.004f), 0.002f, black, 2);

            // 끝 캡 (주황/노랑)
            Cyl(conv, "EndCap", new Vector3(0.10f, beltTop - 0.04f, z + s * 0.03f), 0.095f, 0.035f, Vector3.right * 90f, cap);
            RBox(conv, "EndPlate", new Vector3(2.18f, beltTop - 0.04f, z + s * 0.03f), new Vector3(0.1f, 0.16f, 0.02f), 0.01f, cap, 2);

            // <<< 화살표
            foreach (float cx in new[] { 0.55f, 1.95f })
                for (int k = 0; k < 3; k++)
                    Chevron(conv, new Vector3(cx + k * 0.06f, beltTop - 0.07f, z + s * 0.031f), s, white);

            // 볼트
            foreach (float bx in new[] { 0.2f, 2.25f })
                foreach (float by in new[] { -0.08f, 0.0f })
                    Bolt(conv, new Vector3(bx, beltTop + by, z + s * 0.028f), new Vector3(0, 0, s));
        }

        // 다리 + 받침 + 보강대
        foreach (float lx in new[] { 0.28f, 2.08f })
        {
            foreach (int s in new[] { -1, 1 })
            {
                float z = s * 0.3f;
                RBox(conv, "Leg", new Vector3(lx, 0.36f, z), new Vector3(0.08f, 0.64f, 0.08f), 0.012f, metal, 2).AddComponent<BoxCollider>();
                RBox(conv, "Foot", new Vector3(lx, 0.03f, z), new Vector3(0.14f, 0.06f, 0.14f), 0.015f, black, 2);
                RBox(conv, "LegBracket", new Vector3(lx, beltTop - 0.1f, z + s * 0.02f), new Vector3(0.12f, 0.08f, 0.03f), 0.008f, metalDark, 2);
            }
            RBox(conv, "CrossBrace", new Vector3(lx, 0.22f, 0f), new Vector3(0.05f, 0.05f, 0.6f), 0.008f, metalDark, 2);
        }
        foreach (int s in new[] { -1, 1 })
        {
            RBox(conv, "LowerRail", new Vector3(1.18f, 0.22f, s * 0.3f), new Vector3(1.8f, 0.05f, 0.05f), 0.008f, metalDark, 2);
            // 대각 보강
            var d = RBox(conv, "DiagBrace", new Vector3(0.48f, 0.45f, s * 0.3f), new Vector3(0.5f, 0.04f, 0.04f), 0.006f, metalDark, 2);
            d.transform.localRotation = Quaternion.Euler(0, 0, 32f);
        }
    }

    static void Chevron(Transform p, Vector3 c, int side, Material m)
    {
        for (int k = -1; k <= 1; k += 2)
        {
            var b = Box(p, "Chevron", c + new Vector3(0, k * 0.011f, 0), new Vector3(0.04f, 0.009f, 0.003f), m);
            b.transform.localRotation = Quaternion.Euler(0, 0, k * 35f);    // '<' 모양
        }
    }

    // ------------------------------------------------------------------ 본체
    static void BuildCabinet(Transform t)
    {
        var cab = Group(t, "Cabinet");
        const float x0 = 2.30f, x1 = 3.20f, hz = 0.5f, yTop = 1.60f, yBase = 0.10f;
        const float tunnelDepth = 0.32f, openZ = 0.36f, openY0 = 0.62f, openY1 = 1.36f;
        float cx = (x0 + x1) * 0.5f;

        // 받침 (검정)
        RBox(cab, "Plinth", new Vector3(cx, yBase * 0.5f, 0), new Vector3(x1 - x0 + 0.02f, yBase, hz * 2f + 0.02f), 0.015f, black, 2).AddComponent<BoxCollider>();

        // 뒤쪽 꽉 찬 몸체
        float xb0 = x0 + tunnelDepth;
        RBox(cab, "Body_Rear", new Vector3((xb0 + x1) * 0.5f, (yBase + yTop) * 0.5f, 0), new Vector3(x1 - xb0, yTop - yBase, hz * 2f), 0.05f, metal, 6).AddComponent<BoxCollider>();

        // 앞쪽(터널) 프레임: 위 / 아래 / 양옆
        float fx = x0 + tunnelDepth * 0.5f;
        RBox(cab, "Body_Top",    new Vector3(fx, (openY1 + yTop) * 0.5f, 0), new Vector3(tunnelDepth + 0.02f, yTop - openY1, hz * 2f), 0.05f, metal, 6).AddComponent<BoxCollider>();
        RBox(cab, "Body_Bottom", new Vector3(fx, (yBase + openY0) * 0.5f, 0), new Vector3(tunnelDepth + 0.02f, openY0 - yBase, hz * 2f), 0.03f, metal, 4).AddComponent<BoxCollider>();
        foreach (int s in new[] { -1, 1 })
        {
            float zc = s * (openZ + hz) * 0.5f;
            RBox(cab, "Body_Side", new Vector3(fx, (openY0 + openY1) * 0.5f, zc), new Vector3(tunnelDepth + 0.02f, openY1 - openY0 + 0.02f, hz - openZ), 0.03f, metal, 4).AddComponent<BoxCollider>();
        }

        // 터널 내부 (어두운 라이너)
        Box(cab, "Tunnel_Ceiling", new Vector3(fx, openY1 - 0.006f, 0), new Vector3(tunnelDepth, 0.012f, openZ * 2f), inner);
        Box(cab, "Tunnel_Back",    new Vector3(xb0 - 0.006f, (openY0 + openY1) * 0.5f, 0), new Vector3(0.012f, openY1 - openY0, openZ * 2f), inner);
        foreach (int s in new[] { -1, 1 })
            Box(cab, "Tunnel_Wall", new Vector3(fx, (openY0 + openY1) * 0.5f, s * (openZ - 0.006f)), new Vector3(tunnelDepth, openY1 - openY0, 0.012f), inner);

        // 터널 입구 주황 테두리 (모서리 사선 포함)
        float ox = x0 - 0.008f;
        Box(cab, "OpenTrim_Top", new Vector3(ox, openY1 + 0.03f, 0), new Vector3(0.014f, 0.04f, openZ * 2f - 0.08f), orange);
        foreach (int s in new[] { -1, 1 })
        {
            Box(cab, "OpenTrim_Side", new Vector3(ox, (openY0 + openY1) * 0.5f, s * (openZ + 0.03f)), new Vector3(0.014f, openY1 - openY0 - 0.06f, 0.04f), orange);
            var c = Box(cab, "OpenTrim_Corner", new Vector3(ox, openY1 - 0.005f, s * (openZ - 0.005f)), new Vector3(0.014f, 0.04f, 0.11f), orange);
            c.transform.localRotation = Quaternion.Euler(s * 45f, 0, 0);
        }

        // 모서리 기둥 (진한 회색)
        foreach (float px in new[] { x0 + 0.02f, x1 - 0.02f })
            foreach (int s in new[] { -1, 1 })
                RBox(cab, "CornerPost", new Vector3(px, (yBase + yTop) * 0.5f, s * (hz - 0.015f)), new Vector3(0.05f, yTop - yBase - 0.06f, 0.05f), 0.02f, metalDark, 3);

        // 하단 검정 띠 + 주황 띠 (정면 -Z, 오른쪽 +X)
        float bandFront = -hz - 0.004f;
        Box(cab, "DarkBand_Front", new Vector3((xb0 + x1) * 0.5f, 0.27f, bandFront), new Vector3(x1 - xb0 - 0.08f, 0.24f, 0.006f), black);
        Box(cab, "OrangeBand_Front", new Vector3((xb0 + x1) * 0.5f, 0.42f, bandFront), new Vector3(x1 - xb0 - 0.08f, 0.06f, 0.006f), orange);
        Box(cab, "DarkBand_Right", new Vector3(x1 + 0.004f, 0.27f, 0), new Vector3(0.006f, 0.24f, hz * 2f - 0.08f), black);
        Box(cab, "OrangeBand_Right", new Vector3(x1 + 0.004f, 0.42f, 0), new Vector3(0.006f, 0.06f, hz * 2f - 0.08f), orange);

        // //// 사선 무늬
        for (int k = 0; k < 4; k++)
        {
            var st = Box(cab, "Stripe", new Vector3(2.84f + k * 0.06f, 0.53f, bandFront), new Vector3(0.025f, 0.08f, 0.006f), orange);
            st.transform.localRotation = Quaternion.Euler(0, 0, -28f);
        }

        // BD-01 라벨
        var lab = new GameObject("Label_BD01");
        lab.transform.SetParent(cab, false);
        lab.transform.localPosition = new Vector3(3.12f, 0.355f, bandFront - 0.004f);
        lab.transform.localRotation = Quaternion.identity;
        var tm = lab.AddComponent<TextMesh>();
        tm.text = "BD-01"; tm.fontSize = 64; tm.characterSize = 0.012f;
        tm.anchor = TextAnchor.MiddleRight; tm.color = Color.white;
        tm.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var lr = lab.GetComponent<MeshRenderer>();
        if (tm.font != null) lr.sharedMaterial = tm.font.material;

        // 통풍구
        for (int k = 0; k < 5; k++)
            RBox(cab, "Vent", new Vector3(3.08f, 0.17f + k * 0.035f, bandFront - 0.004f), new Vector3(0.12f, 0.014f, 0.008f), 0.004f, metalDark, 2);

        // 패널 볼트 (정면)
        foreach (float bx in new[] { xb0 + 0.05f, x1 - 0.06f })
            foreach (float by in new[] { 0.6f, yTop - 0.08f })
                Bolt(cab, new Vector3(bx, by, -hz - 0.002f), Vector3.back);

        // 왼쪽 위 작은 패널 (뒤쪽)
        RBox(cab, "SidePanel", new Vector3(x0 - 0.005f, 1.48f, 0.38f), new Vector3(0.012f, 0.12f, 0.08f), 0.004f, metalDark, 2);

        // 신호탑 (적/황/녹)
        var tower = Group(cab, "SignalTower");
        tower.localPosition = new Vector3(xb0 + 0.12f, yTop, 0.3f);
        Cyl(tower, "Base", new Vector3(0, 0.02f, 0), 0.055f, 0.04f, Vector3.zero, black);
        Cyl(tower, "Pole", new Vector3(0, 0.08f, 0), 0.015f, 0.08f, Vector3.zero, metalDark);
        Cyl(tower, "Lamp_Green",  new Vector3(0, 0.15f, 0), 0.04f, 0.06f, Vector3.zero, green);
        Cyl(tower, "Lamp_Yellow", new Vector3(0, 0.215f, 0), 0.04f, 0.06f, Vector3.zero, yellow);
        Cyl(tower, "Lamp_Red",    new Vector3(0, 0.28f, 0), 0.04f, 0.06f, Vector3.zero, red);
        Cyl(tower, "Cap", new Vector3(0, 0.32f, 0), 0.042f, 0.02f, Vector3.zero, black);
        foreach (float y in new[] { 0.182f, 0.247f })
            Cyl(tower, "Ring", new Vector3(0, y, 0), 0.043f, 0.006f, Vector3.zero, black);
    }

    // ------------------------------------------------------------------ 배치
    static void FindSpawn(out Vector3 pos, out Quaternion rot)
    {
        pos = Vector3.zero; rot = Quaternion.identity;
        var sv = SceneView.lastActiveSceneView;
        if (sv == null || sv.camera == null) return;
        var cam = sv.camera.transform;
        var ray = new Ray(cam.position, cam.forward);
        Vector3 guess = Physics.Raycast(ray, out var hit, 200f) && hit.normal.y > 0.7f
            ? hit.point
            : cam.position + Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized * 5f;
        // 그 지점에서 아래로 바닥 찾기
        if (Physics.Raycast(guess + Vector3.up * 0.5f, Vector3.down, out var floor, 50f)) pos = floor.point;
        else pos = guess;
        // 카메라에서 정면(-Z, 라벨 쪽)이 보이도록
        Vector3 f = Vector3.ProjectOnPlane(cam.forward, Vector3.up);
        if (f.sqrMagnitude > 1e-4f) rot = Quaternion.LookRotation(f.normalized, Vector3.up);
        pos -= rot * new Vector3(1.6f, 0, 0);   // 기계 중앙이 화면 가운데에 오도록
    }

    // ------------------------------------------------------------------ 헬퍼
    static Transform Group(Transform parent, string name)
    {
        var g = new GameObject(name);
        g.transform.SetParent(parent, false);
        return g.transform;
    }

    static GameObject RBox(Transform parent, string name, Vector3 pos, Vector3 size, float r, Material m, int seg = 4)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.AddComponent<MeshFilter>().sharedMesh = SaveMesh(RoundedBox(size, r, seg), name);
        go.AddComponent<MeshRenderer>().sharedMaterial = m;
        return go;
    }

    static GameObject Box(Transform parent, string name, Vector3 pos, Vector3 size, Material m)
        => Prim(PrimitiveType.Cube, parent, name, pos, size, Vector3.zero, m);

    // 반지름/길이 기준 원기둥 (rotEuler로 축 방향 지정)
    static GameObject Cyl(Transform parent, string name, Vector3 pos, float radius, float length, Vector3 rotEuler, Material m)
        => Prim(PrimitiveType.Cylinder, parent, name, pos, new Vector3(radius * 2f, length * 0.5f, radius * 2f), rotEuler, m);

    static void Bolt(Transform parent, Vector3 pos, Vector3 normal)
    {
        var b = Prim(PrimitiveType.Sphere, parent, "Bolt", pos, new Vector3(0.018f, 0.018f, 0.007f), Vector3.zero, metalDark);
        b.transform.localRotation = Quaternion.LookRotation(normal);
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

    static Mesh SaveMesh(Mesh mesh, string name)
    {
        mesh.name = name;
        AssetDatabase.CreateAsset(mesh, $"{MeshDir}/{name}_{meshCounter++}.asset");
        return mesh;
    }

    static Material Mat(string name, Color c, float metallic, float smooth)
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
        if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", smooth);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static Material Lamp(string name, Color c)
    {
        var m = Mat(name, c, 0f, 0.8f);
        m.EnableKeyword("_EMISSION");
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        m.SetColor("_EmissionColor", c * 1.6f);
        EditorUtility.SetDirty(m);
        return m;
    }

    // ---------- 둥근 박스 메시 ----------
    static Mesh RoundedBox(Vector3 size, float r, int seg)
    {
        Vector3 h = size * 0.5f;
        r = Mathf.Min(r, Mathf.Min(h.x, Mathf.Min(h.y, h.z)) * 0.98f);
        Vector3 innerH = h - Vector3.one * r;
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
                Vector3 inn = new Vector3(Mathf.Clamp(p.x, -innerH.x, innerH.x), Mathf.Clamp(p.y, -innerH.y, innerH.y), Mathf.Clamp(p.z, -innerH.z, innerH.z));
                Vector3 d = p - inn;
                Vector3 nrm = d.sqrMagnitude < 1e-12f ? faceN : d.normalized;
                verts.Add(inn + nrm * r); norms.Add(nrm);
                uvs.Add(new Vector2((us[i] + h[ua]) / size[ua], (vs[j] + h[va]) / size[va]));
            }
            int w = us.Length;
            for (int j = 0; j < vs.Length - 1; j++)
            for (int i = 0; i < w - 1; i++)
            {
                int a = start + j * w + i, b = a + 1, c = a + w, d2 = c + 1;
                Tri(tris, verts, a, b, d2, faceN); Tri(tris, verts, a, d2, c, faceN);
            }
        }
        var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
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

    static void Tri(List<int> t, List<Vector3> v, int a, int b, int c, Vector3 outN)
    {
        Vector3 n = Vector3.Cross(v[b] - v[a], v[c] - v[a]);
        if (n.sqrMagnitude < 1e-16f) return;
        if (Vector3.Dot(n, outN) >= 0) { t.Add(a); t.Add(b); t.Add(c); } else { t.Add(a); t.Add(c); t.Add(b); }
    }
}
