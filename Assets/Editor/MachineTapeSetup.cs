// Tools > Setup Machine Tape
// 큐브 디스펜서를 테이프 디스펜서로 교체하고 버튼/슬롯/XR 이벤트를 다시 연결합니다.
using UnityEngine;
using UnityEngine.Events;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;

public static class MachineTapeSetup
{
    [MenuItem("Tools/Setup Machine Tape")]
    public static void Setup()
    {
        var m = GameObject.Find("Machine");
        if (m == null) { EditorUtility.DisplayDialog("Machine", "씬에 Machine 오브젝트가 없어요.", "OK"); return; }
        var btn = m.transform.Find("Button_Green");
        var slot = m.transform.Find("Head_Slot");
        if (btn == null || slot == null) { EditorUtility.DisplayDialog("Machine", "Button_Green 또는 Head_Slot을 찾지 못했어요.", "OK"); return; }

        if (btn.GetComponent<Collider>() == null) Undo.AddComponent<SphereCollider>(btn.gameObject).radius = 0.6f;
        foreach (var n in new[] { "Base_Gray", "Base_Cream", "Plate", "Column_GrayShell", "Column_Cream", "Arm", "Head" })
        {
            var c = m.transform.Find(n);
            if (c != null && c.GetComponent<Collider>() == null) Undo.AddComponent<BoxCollider>(c.gameObject);
        }

        // 테이프 머티리얼 (마스킹 테이프 느낌)
        const string matPath = "Assets/MachineMaterials/M_Tape.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            mat = new Material(shader);
            if (!AssetDatabase.IsValidFolder("Assets/MachineMaterials")) AssetDatabase.CreateFolder("Assets", "MachineMaterials");
            AssetDatabase.CreateAsset(mat, matPath);
        }
        var col = new Color(0.94f, 0.87f, 0.64f);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", col);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", col);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.45f);
        if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", 0f); // 양면
        EditorUtility.SetDirty(mat);

        // 디스펜서 교체
        var tape = m.GetComponent<MachineTapeDispenser>();
        if (tape == null) tape = Undo.AddComponent<MachineTapeDispenser>(m);
        tape.button = btn;
        tape.spawnPoint = slot;
        tape.tapeMaterial = mat;
        EditorUtility.SetDirty(tape);

        var oldCube = m.GetComponent<MachineCubeDispenser>();

        // XR 버튼 이벤트: 큐브 연결 제거 → 테이프 연결
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
                for (int i = evt.GetPersistentEventCount() - 1; i >= 0; i--)
                {
                    var target = evt.GetPersistentTarget(i);
                    if (oldCube != null && target == oldCube) UnityEventTools.RemovePersistentListener(evt, i);
                    else if (target == tape && evt.GetPersistentMethodName(i) == nameof(MachineTapeDispenser.Press)) already = true;
                }
                if (!already) UnityEventTools.AddVoidPersistentListener(evt, new UnityAction(tape.Press));
                EditorUtility.SetDirty(comp);
                xrMsg = "VR 컨트롤러로 버튼을 선택하면 눌려요.";
            }
        }

        if (oldCube != null) Undo.DestroyObjectImmediate(oldCube);

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(m.scene);
        Selection.activeGameObject = m;
        Debug.Log("[Machine] 테이프 디스펜서 설정 완료. " + xrMsg);
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
