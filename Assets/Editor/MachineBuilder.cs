// Assets/Editor/MachineBuilder.cs 에 넣고
// 상단 메뉴 Tools > Build Machine 을 누르면 씬에 기계 모델이 생성됩니다.
using UnityEngine;
using UnityEditor;

public static class MachineBuilder
{
    static Material cream, gray, steel, green, dark, screw;

    [MenuItem("Tools/Build Machine")]
    public static void Build()
    {
        cream = Mat("M_Cream", new Color(0.93f, 0.91f, 0.86f), 0f, 0.35f);
        gray  = Mat("M_Gray",  new Color(0.27f, 0.27f, 0.28f), 0f, 0.3f);
        steel = Mat("M_Steel", new Color(0.75f, 0.76f, 0.78f), 1f, 0.6f);
        green = Mat("M_Green", new Color(0.25f, 0.85f, 0.2f), 0f, 0.7f);
        green.EnableKeyword("_EMISSION");
        green.SetColor("_EmissionColor", new Color(0.1f, 0.6f, 0.1f));
        dark  = Mat("M_Dark",  new Color(0.08f, 0.08f, 0.08f), 0f, 0.2f);
        screw = Mat("M_Screw", new Color(0.6f, 0.6f, 0.62f), 1f, 0.5f);

        var root = new GameObject("Machine");
        Undo.RegisterCreatedObjectUndo(root, "Build Machine");
        var t = root.transform;

        // ---- 받침(베이스) ----
        Box(t, "Base_Gray",  new Vector3(0f, 0.12f, 0f),  new Vector3(2.2f, 0.12f, 1.0f), gray);
        Box(t, "Base_Cream", new Vector3(0.05f, 0.26f, 0f), new Vector3(2.1f, 0.16f, 0.96f), cream);
        Box(t, "Plate",      new Vector3(0.2f, 0.345f, -0.1f), new Vector3(1.3f, 0.012f, 0.55f), steel);
        foreach (var p in new[] { new Vector2(-0.42f, -0.34f), new Vector2(0.82f, -0.34f),
                                  new Vector2(-0.42f, 0.14f),  new Vector2(0.82f, 0.14f) })
            Cyl(t, "PlateScrew", new Vector3(p.x, 0.353f, p.y), new Vector3(0.035f, 0.004f, 0.035f), Vector3.zero, screw);

        // 고무 발
        foreach (var p in new[] { new Vector2(-0.85f, -0.35f), new Vector2(0.85f, -0.35f),
                                  new Vector2(-0.85f, 0.35f),  new Vector2(0.85f, 0.35f) })
            Cyl(t, "Foot", new Vector3(p.x, 0.03f, p.y), new Vector3(0.16f, 0.03f, 0.16f), Vector3.zero, gray);

        // ---- 기둥 ----
        Box(t, "Column_Cream", new Vector3(-0.775f, 0.895f, 0.1f), new Vector3(0.35f, 1.11f, 0.6f), cream);
        Box(t, "Column_GrayShell", new Vector3(-1.02f, 0.78f, 0.1f), new Vector3(0.16f, 1.44f, 0.62f), gray);
        Box(t, "Column_GrayCap",   new Vector3(-0.85f, 1.475f, 0.1f), new Vector3(0.5f, 0.05f, 0.62f), gray);
        Cyl(t, "ColumnScrew_Top", new Vector3(-0.85f, 1.32f, -0.205f), new Vector3(0.04f, 0.005f, 0.04f), new Vector3(90, 0, 0), screw);
        Cyl(t, "ColumnScrew_Bot", new Vector3(-0.85f, 0.48f, -0.205f), new Vector3(0.04f, 0.005f, 0.04f), new Vector3(90, 0, 0), screw);

        // ---- 팔(가로대) ----
        Box(t, "Arm", new Vector3(-0.15f, 1.275f, 0.1f), new Vector3(0.9f, 0.35f, 0.55f), cream);
        Cyl(t, "Button_Green", new Vector3(-0.02f, 1.25f, -0.18f), new Vector3(0.1f, 0.012f, 0.1f), new Vector3(90, 0, 0), green);

        // ---- 헤드 ----
        Box(t, "Head", new Vector3(0.625f, 1.2f, 0.1f), new Vector3(0.85f, 0.7f, 0.65f), cream);
        Box(t, "Head_Seam", new Vector3(0.32f, 1.2f, -0.226f), new Vector3(0.006f, 0.66f, 0.004f), gray);
        Box(t, "Head_Slot", new Vector3(0.45f, 0.848f, -0.05f), new Vector3(0.28f, 0.006f, 0.035f), dark);

        Selection.activeGameObject = root;
        SceneView.lastActiveSceneView?.FrameSelected();
    }

    static GameObject Box(Transform parent, string name, Vector3 pos, Vector3 scale, Material m)
        => Prim(PrimitiveType.Cube, parent, name, pos, scale, Vector3.zero, m);

    static GameObject Cyl(Transform parent, string name, Vector3 pos, Vector3 scale, Vector3 rot, Material m)
        => Prim(PrimitiveType.Cylinder, parent, name, pos, scale, rot, m);

    static GameObject Prim(PrimitiveType type, Transform parent, string name, Vector3 pos, Vector3 scale, Vector3 rot, Material m)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localEulerAngles = rot;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = m;
        return go;
    }

    static Material Mat(string name, Color c, float metallic, float smooth)
    {
        string path = "Assets/MachineMaterials/" + name + ".mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) return existing;

        var shader = Shader.Find("Universal Render Pipeline/Lit")
                  ?? Shader.Find("HDRP/Lit")
                  ?? Shader.Find("Standard");
        var mat = new Material(shader);
        mat.SetColor(mat.HasProperty("_BaseColor") ? "_BaseColor" : "_Color", c);
        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smooth);
        if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", smooth);

        if (!AssetDatabase.IsValidFolder("Assets/MachineMaterials"))
            AssetDatabase.CreateFolder("Assets", "MachineMaterials");
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }
}
