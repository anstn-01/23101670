// 초록 버튼: 1번 누르면 테이프가 조금 나옴 → 잡고 당기면 계속 풀림 → 다시 누르면 끊김
// ※ 현재 비활성화: 상자 날개를 다 닫으면 테이프가 자동으로 붙도록 바뀌어서 손 테이프는 사용하지 않음.
//   다시 쓰려면 Machine 오브젝트의 TapeDispenser 에서 Dispenser Enabled 를 체크하세요.
// - 에디터 Play: Game 뷰에서 버튼 클릭 / 테이프 끝을 마우스로 끌기
// - VR: 버튼 선택(XR Simple Interactable) / 테이프 끝 잡기(XR Grab)
using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class TapeDispenser : MonoBehaviour
{
    [Tooltip("손으로 뽑는 테이프 사용 여부 (꺼져 있으면 버튼을 눌러도 테이프가 안 나옴)")]
    public bool dispenserEnabled = false;

    [Header("References")]
    public Transform button;        // Button_Green
    public Transform spawnPoint;    // Head_Slot
    public Material tapeMaterial;

    [Header("Tape")]
    public float tapeWidth = 0.08f;
    public int maxTapes = 10;

    [Header("Button")]
    public float pushDepth = 0.008f;
    public float cooldown = 0.25f;

    public TapeRibbon Current => current;

    TapeRibbon current;
    readonly Queue<TapeRibbon> tapes = new Queue<TapeRibbon>();
    float lastPress = -99f;
    Vector3 buttonRest;
    Coroutine pressRoutine;

    // 마우스 드래그
    TapeRibbon dragging;
    float dragDist;

    void Awake()
    {
        if (button) buttonRest = button.localPosition;
    }

    /// 버튼 누르기: 테이프가 없으면 새로 나오고, 붙어 있으면 끊음
    public void Press()
    {
        if (!dispenserEnabled) return;
        if (Time.time - lastPress < cooldown || spawnPoint == null) return;
        lastPress = Time.time;
        if (button)
        {
            if (pressRoutine != null) StopCoroutine(pressRoutine);
            pressRoutine = StartCoroutine(PressAnim());
        }

        if (current != null && current.IsAttached)
        {
            current.Cut();
            current = null;
            return;
        }

        var go = new GameObject("Tape");
        current = go.AddComponent<TapeRibbon>();
        current.Init(spawnPoint, tapeMaterial, tapeWidth);
        tapes.Enqueue(current);
        while (tapes.Count > maxTapes)
        {
            var old = tapes.Dequeue();
            if (old != null && old != current) Destroy(old.gameObject);
        }
    }

    System.Collections.IEnumerator PressAnim()
    {
        Vector3 pressed = buttonRest + Vector3.forward * pushDepth;
        for (float t = 0; t < 0.06f; t += Time.deltaTime)
        { button.localPosition = Vector3.Lerp(buttonRest, pressed, t / 0.06f); yield return null; }
        button.localPosition = pressed;
        yield return new WaitForSeconds(0.08f);
        for (float t = 0; t < 0.1f; t += Time.deltaTime)
        { button.localPosition = Vector3.Lerp(pressed, buttonRest, t / 0.1f); yield return null; }
        button.localPosition = buttonRest;
    }

    void Update()
    {
        if (!dispenserEnabled) return;
        var cam = Camera.main;
        if (cam == null) return;

        bool down, held; Vector2 pos;
#if ENABLE_INPUT_SYSTEM
        var mouse = Mouse.current;
        if (mouse == null) return;
        down = mouse.leftButton.wasPressedThisFrame;
        held = mouse.leftButton.isPressed;
        pos = mouse.position.ReadValue();
#else
        down = Input.GetMouseButtonDown(0);
        held = Input.GetMouseButton(0);
        pos = Input.mousePosition;
#endif
        var ray = cam.ScreenPointToRay(pos);

        if (down && Physics.Raycast(ray, out var hit, 50f, ~0, QueryTriggerInteraction.Collide))
        {
            var r = TapeRibbon.FromHandle(hit.collider);
            if (r != null) { dragging = r; dragDist = hit.distance; }
            else if (button != null && hit.transform == button) Press();
        }

        if (dragging != null)
        {
            if (held)
            {
                dragging.externalGrab = true;
                dragging.externalGrabPoint = ray.GetPoint(dragDist);
            }
            else
            {
                dragging.externalGrab = false;
                dragging = null;
            }
        }
    }
}

#if UNITY_EDITOR
static class TapeDispenserSetup
{
    [UnityEditor.MenuItem("Tools/Setup Tape (v2)")]
    static void Setup()
    {
        var m = GameObject.Find("Machine");
        if (m == null) { UnityEditor.EditorUtility.DisplayDialog("Machine", "씬에 Machine 오브젝트가 없어요.", "OK"); return; }
        var btn = m.transform.Find("Button_Green");
        var slot = m.transform.Find("Head_Slot");
        if (btn == null || slot == null) { UnityEditor.EditorUtility.DisplayDialog("Machine", "Button_Green 또는 Head_Slot을 찾지 못했어요.", "OK"); return; }

        if (btn.GetComponent<Collider>() == null) UnityEditor.Undo.AddComponent<SphereCollider>(btn.gameObject).radius = 0.6f;
        foreach (var n in new[] { "Base_Gray", "Base_Cream", "Plate", "Column_GrayShell", "Column_Cream", "Arm", "Head" })
        {
            var c = m.transform.Find(n);
            if (c != null && c.GetComponent<Collider>() == null) UnityEditor.Undo.AddComponent<BoxCollider>(c.gameObject);
        }

        // 반투명 테이프 머티리얼 (URP Lit Transparent, 양면)
        const string matPath = "Assets/MachineMaterials/M_TapeClear.mat";
        var mat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            mat = new Material(shader);
            if (!UnityEditor.AssetDatabase.IsValidFolder("Assets/MachineMaterials")) UnityEditor.AssetDatabase.CreateFolder("Assets", "MachineMaterials");
            UnityEditor.AssetDatabase.CreateAsset(mat, matPath);
        }
        var col = new Color(1f, 0.93f, 0.72f, 0.45f);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", col);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", col);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.85f);
        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);
        if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
        if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 0f);
        if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", 0f);
        if (mat.HasProperty("_SrcBlend")) mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (mat.HasProperty("_DstBlend")) mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        if (mat.HasProperty("_SrcBlendAlpha")) mat.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
        if (mat.HasProperty("_DstBlendAlpha")) mat.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 0f);
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        UnityEditor.EditorUtility.SetDirty(mat);

        // 이전 디스펜서 제거
        var oldTargets = new List<Object>();
        foreach (var oldName in new[] { "MachineTapeDispenser", "MachineCubeDispenser" })
        {
            var oc = m.GetComponent(oldName);
            if (oc != null) oldTargets.Add(oc);
        }

        var disp = m.GetComponent<TapeDispenser>();
        if (disp == null) disp = UnityEditor.Undo.AddComponent<TapeDispenser>(m);
        disp.button = btn;
        disp.spawnPoint = slot;
        disp.tapeMaterial = mat;
        UnityEditor.EditorUtility.SetDirty(disp);

        // XR 버튼 이벤트 재연결
        string xrMsg = "XR Interaction Toolkit을 찾지 못해 마우스만 연결했어요.";
        System.Type simpleType = null;
        foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            System.Type[] types;
            try { types = asm.GetTypes(); } catch { continue; }
            foreach (var t in types) if (t.Name == "XRSimpleInteractable" && typeof(Component).IsAssignableFrom(t)) { simpleType = t; break; }
            if (simpleType != null) break;
        }
        if (simpleType != null)
        {
            var comp = btn.GetComponent(simpleType);
            if (comp == null) comp = UnityEditor.Undo.AddComponent(btn.gameObject, simpleType);
            var evt = simpleType.GetProperty("selectEntered")?.GetValue(comp) as UnityEngine.Events.UnityEventBase;
            if (evt != null)
            {
                bool already = false;
                for (int i = evt.GetPersistentEventCount() - 1; i >= 0; i--)
                {
                    var target = evt.GetPersistentTarget(i);
                    if (target == null || oldTargets.Contains(target)) UnityEditor.Events.UnityEventTools.RemovePersistentListener(evt, i);
                    else if (target == disp && evt.GetPersistentMethodName(i) == nameof(TapeDispenser.Press)) already = true;
                }
                if (!already) UnityEditor.Events.UnityEventTools.AddVoidPersistentListener(evt, new UnityEngine.Events.UnityAction(disp.Press));
                UnityEditor.EditorUtility.SetDirty(comp);
                xrMsg = "VR: 버튼 선택으로 누르고, 테이프 끝을 잡아 당기세요.";
            }
        }

        foreach (var o in oldTargets) UnityEditor.Undo.DestroyObjectImmediate(o);

        UnityEditor.AssetDatabase.SaveAssets();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(m.scene);
        UnityEditor.Selection.activeGameObject = m;
        Debug.Log("[Machine] 테이프 v2 설정 완료. " + xrMsg);
    }
}
#endif
