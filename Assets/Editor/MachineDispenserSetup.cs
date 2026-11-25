// Tools > Setup Machine Button
// Machine에 MachineCubeDispenser를 붙이고 버튼/슬롯/충돌체/XR 버튼 이벤트를 연결합니다.
using UnityEngine;
using UnityEngine.Events;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;

public static class MachineDispenserSetup
{
    [MenuItem("Tools/Setup Machine Button")]
    public static void Setup()
    {
        var m = GameObject.Find("Machine");
        if (m == null) { EditorUtility.DisplayDialog("Machine", "씬에 Machine 오브젝트가 없어요.", "OK"); return; }
        var btn = m.transform.Find("Button_Green");
        var slot = m.transform.Find("Head_Slot");
        if (btn == null || slot == null) { EditorUtility.DisplayDialog("Machine", "Button_Green 또는 Head_Slot을 찾지 못했어요.", "OK"); return; }

        // 버튼 충돌체 (클릭/레이 판정용)
        if (btn.GetComponent<Collider>() == null)
        {
            var sc = Undo.AddComponent<SphereCollider>(btn.gameObject);
            sc.radius = 0.6f;
        }

        // 큐브가 기계를 통과하지 않도록 본체에 충돌체
        foreach (var n in new[] { "Base_Gray", "Base_Cream", "Plate", "Column_GrayShell", "Column_Cream", "Arm", "Head" })
        {
            var c = m.transform.Find(n);
            if (c != null && c.GetComponent<Collider>() == null) Undo.AddComponent<BoxCollider>(c.gameObject);
        }

        // 큐브 머티리얼
        const string matPath = "Assets/MachineMaterials/M_Cube.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            mat = new Material(shader);
            var col = new Color(1f, 0.55f, 0.15f);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", col);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", col);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.5f);
            if (!AssetDatabase.IsValidFolder("Assets/MachineMaterials")) AssetDatabase.CreateFolder("Assets", "MachineMaterials");
            AssetDatabase.CreateAsset(mat, matPath);
        }

        // 디스펜서
        var d = m.GetComponent<MachineCubeDispenser>();
        if (d == null) d = Undo.AddComponent<MachineCubeDispenser>(m);
        d.button = btn;
        d.spawnPoint = slot;
        d.cubeMaterial = mat;
        EditorUtility.SetDirty(d);

        // XR Interaction Toolkit이 있으면 버튼에 XR Simple Interactable + Select → Press 연결
        string xrMsg = "XR Interaction Toolkit을 찾지 못해 마우스 클릭만 연결했어요.";
        var simpleType = FindType("XRSimpleInteractable");
        if (simpleType != null)
        {
            var comp = btn.GetComponent(simpleType);
            if (comp == null) comp = Undo.AddComponent(btn.gameObject, simpleType);
            var evt = simpleType.GetProperty("selectEntered")?.GetValue(comp) as UnityEventBase;
            if (evt != null)
            {
                bool already = false;
                for (int i = 0; i < evt.GetPersistentEventCount(); i++)
                    if (evt.GetPersistentTarget(i) == d && evt.GetPersistentMethodName(i) == nameof(MachineCubeDispenser.Press)) already = true;
                if (!already) UnityEventTools.AddVoidPersistentListener(evt, new UnityAction(d.Press));
                EditorUtility.SetDirty(comp);
                xrMsg = "VR 컨트롤러로 버튼을 선택(그립/트리거)하면 눌려요.";
            }
        }

        EditorSceneManager.MarkSceneDirty(m.scene);
        Selection.activeGameObject = m;
        Debug.Log("[Machine] 버튼 설정 완료. Play 후 Game 뷰에서 초록 버튼을 클릭하세요. " + xrMsg);
    }

    static System.Type FindType(string name)
    {
        foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            System.Type[] types;
            try { types = asm.GetTypes(); } catch { continue; }
            foreach (var t in types)
                if (t.Name == name && typeof(Component).IsAssignableFrom(t)) return t;
        }
        return null;
    }
}
