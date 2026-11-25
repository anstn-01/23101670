// Tools > Machine Polish && Front View
// 회색/스틸 머티리얼을 조금 밝게 다듬고, 씬 뷰를 기계 앞쪽 3/4 각도로 맞춥니다.
using UnityEngine;
using UnityEditor;

public static class MachineViewTools
{
    [MenuItem("Tools/Machine Polish && Front View")]
    public static void PolishAndView()
    {
        Set("M_Gray",   new Color(0.34f, 0.34f, 0.35f), 0f,   0.4f);
        Set("M_Rubber", new Color(0.16f, 0.16f, 0.17f), 0f,   0.2f);
        Set("M_Steel",  new Color(0.88f, 0.89f, 0.90f), 0.65f, 0.55f);
        Set("M_Cream",  new Color(0.93f, 0.90f, 0.83f), 0f,   0.5f);
        AssetDatabase.SaveAssets();

        var m = GameObject.Find("Machine");
        var sv = SceneView.lastActiveSceneView;
        if (m == null || sv == null) return;
        Selection.activeGameObject = m;
        var rot = m.transform.rotation * Quaternion.Euler(14f, -28f, 0f);
        sv.LookAt(m.transform.TransformPoint(new Vector3(0f, 0.75f, 0f)), rot, 1.5f);
        sv.Repaint();
    }

    static void Set(string name, Color c, float metallic, float smooth)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>($"Assets/MachineMaterials/{name}.mat");
        if (mat == null) return;
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", c);
        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smooth);
        EditorUtility.SetDirty(mat);
    }
}
