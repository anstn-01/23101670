// 벽에 붙는 빨간 비상/시작 버튼. Press() 하면 눌리는 애니메이션 + 컨베이어에서 상자 배출.
// Tools > Build Red Wall Button 으로 씬 뷰에서 보고 있는 벽에 만들어집니다.
using System.Collections;
using UnityEngine;

public class RedPushButton : MonoBehaviour
{
    public Transform plunger;
    public Renderer lamp;
    public BoxConveyor target;
    public float travel = 0.018f;
    public float cooldown = 0.35f;

    Vector3 rest; float last = -10f; Material lampMat; Color lampBase;

    void Awake()
    {
        if (plunger) rest = plunger.localPosition;
        if (lamp) { lampMat = lamp.material; lampBase = lampMat.HasProperty("_BaseColor") ? lampMat.GetColor("_BaseColor") : lampMat.color; }
    }

    public void Press()
    {
        if (Time.time - last < cooldown) return;
        last = Time.time;
        if (target) target.SpawnBox();
        StopAllCoroutines();
        StartCoroutine(Anim());
    }

    IEnumerator Anim()
    {
        if (lampMat) { lampMat.EnableKeyword("_EMISSION"); lampMat.SetColor("_EmissionColor", new Color(2.5f, 0.15f, 0.1f)); }
        for (float t = 0; t < 1f; t += Time.deltaTime / 0.08f) { Set(t); yield return null; }
        Set(1f);
        yield return new WaitForSeconds(0.12f);
        for (float t = 1f; t > 0f; t -= Time.deltaTime / 0.15f) { Set(t); yield return null; }
        Set(0f);
        yield return new WaitForSeconds(0.25f);
        if (lampMat) lampMat.SetColor("_EmissionColor", Color.black);
    }

    void Set(float k) { if (plunger) plunger.localPosition = rest - Vector3.forward * travel * k; }
}

#if UNITY_EDITOR
static class RedPushButtonBuilder
{
    const string Root = "Assets/ConveyorMaterials";

    [UnityEditor.MenuItem("Tools/Build Red Wall Button")]
    static void Build()
    {
        var conv = GameObject.Find("Conveyor_BD01");
        if (conv == null) { Debug.LogWarning("[Button] Conveyor_BD01이 없어요."); return; }
        var sv = UnityEditor.SceneView.lastActiveSceneView;
        if (sv == null) { Debug.LogWarning("[Button] 씬 뷰가 없어요."); return; }

        // 기존 것 지우기
        var old = GameObject.Find("RedWallButton");
        if (old) UnityEditor.Undo.DestroyObjectImmediate(old);

        // 1) 보고 있는 벽 찾기 (벽에 콜라이더가 없으면 잠깐 붙여서 레이캐스트)
        var temp = new System.Collections.Generic.List<Collider>();
        var factory = GameObject.Find("Parking Factory");
        if (factory)
            foreach (var mf in factory.GetComponentsInChildren<MeshFilter>())
                if (mf.GetComponent<Collider>() == null && mf.sharedMesh != null)
                    temp.Add(mf.gameObject.AddComponent<MeshCollider>());
        Physics.SyncTransforms();

        var cam = sv.camera;
        var ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        RaycastHit best = default; float min = float.MaxValue; bool ok = false;
        foreach (var h in Physics.RaycastAll(ray, 200f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (h.transform.IsChildOf(conv.transform)) continue;
            if (Mathf.Abs(h.normal.y) > 0.5f) continue;          // 바닥/천장 제외
            if (h.distance < min) { min = h.distance; best = h; ok = true; }
        }
        foreach (var c in temp) Object.DestroyImmediate(c);

        Vector3 n, p;
        float floorY = conv.transform.position.y;
        if (ok) { n = Vector3.ProjectOnPlane(best.normal, Vector3.up).normalized; p = best.point; }
        else
        {   // 못 찾으면 테이블 왼쪽 뒷벽
            var table = conv.transform.Find("OutputTable");
            var tr = table ? table : conv.transform;
            n = -tr.forward; p = tr.TransformPoint(new Vector3(-1.6f, 0, 0.55f));
            Debug.LogWarning("[Button] 벽을 못 찾아서 테이블 왼쪽 뒷벽에 붙였어요.");
        }
        p.y = floorY + 1.2f;

        // 2) 재질
        var red = Mat("BTN_Red", new Color(0.85f, 0.05f, 0.04f), 0f, 0.75f);
        var yellow = Mat("BTN_Yellow", new Color(1f, 0.78f, 0.05f), 0f, 0.5f);
        var dark = Mat("BTN_Dark", new Color(0.12f, 0.12f, 0.13f), 0.3f, 0.45f);
        var metal = Mat("BTN_Metal", new Color(0.7f, 0.71f, 0.73f), 0.8f, 0.6f);

        // 3) 버튼 만들기 (+Z = 벽 바깥쪽)
        var root = new GameObject("RedWallButton").transform;
        UnityEditor.Undo.RegisterCreatedObjectUndo(root.gameObject, "Build Red Wall Button");
        root.SetPositionAndRotation(p + n * 0.005f, Quaternion.LookRotation(n, Vector3.up));

        Prim(PrimitiveType.Cube, root, "BackPlate", new Vector3(0, -0.03f, 0.008f), new Vector3(0.2f, 0.28f, 0.016f), Vector3.zero, yellow);
        for (int k = 0; k < 6; k++)   // 검정 사선 줄무늬
            Prim(PrimitiveType.Cube, root, "Hazard", new Vector3(-0.075f + k * 0.03f, -0.155f, 0.0165f), new Vector3(0.012f, 0.03f, 0.002f), new Vector3(0, 0, 35f), dark);
        Prim(PrimitiveType.Cube, root, "Housing", new Vector3(0, 0, 0.045f), new Vector3(0.13f, 0.13f, 0.06f), Vector3.zero, dark);
        Prim(PrimitiveType.Cylinder, root, "Collar", new Vector3(0, 0, 0.08f), new Vector3(0.1f, 0.006f, 0.1f), new Vector3(90, 0, 0), metal);
        foreach (int sx in new[] { -1, 1 }) foreach (int sy in new[] { -1, 1 })
            Prim(PrimitiveType.Sphere, root, "Screw", new Vector3(sx * 0.052f, sy * 0.052f, 0.0755f), new Vector3(0.01f, 0.01f, 0.004f), Vector3.zero, metal);

        var plunger = new GameObject("Plunger").transform;
        plunger.SetParent(root, false);
        plunger.localPosition = new Vector3(0, 0, 0.085f);
        Prim(PrimitiveType.Cylinder, plunger, "Stem", new Vector3(0, 0, 0.012f), new Vector3(0.05f, 0.012f, 0.05f), new Vector3(90, 0, 0), red);
        var cap = Prim(PrimitiveType.Sphere, plunger, "Cap", new Vector3(0, 0, 0.026f), new Vector3(0.085f, 0.085f, 0.04f), Vector3.zero, red);

        // 라벨
        var labelGo = new GameObject("Label");
        labelGo.transform.SetParent(root, false);
        labelGo.transform.localPosition = new Vector3(0, 0.088f, 0.0175f);
        labelGo.transform.localRotation = Quaternion.Euler(0, 180f, 0);
        var tm = labelGo.AddComponent<TextMesh>();
        tm.text = "BOX START"; tm.anchor = TextAnchor.MiddleCenter; tm.alignment = TextAlignment.Center;
        tm.fontSize = 64; tm.characterSize = 0.0045f; tm.color = new Color(0.08f, 0.08f, 0.08f); tm.fontStyle = FontStyle.Bold;
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font) { tm.font = font; labelGo.GetComponent<MeshRenderer>().sharedMaterial = font.material; }

        // 누르는 판정용 콜라이더
        var col = root.gameObject.AddComponent<BoxCollider>();
        col.center = new Vector3(0, 0, 0.09f); col.size = new Vector3(0.12f, 0.12f, 0.08f);

        var btn = root.gameObject.AddComponent<RedPushButton>();
        btn.plunger = plunger;
        btn.lamp = cap.GetComponent<Renderer>();

        // 4) 상자 배출기
        var feed = conv.GetComponent<BoxConveyor>();
        if (feed == null) feed = UnityEditor.Undo.AddComponent<BoxConveyor>(conv);
        feed.boxMaterial = Mat("BOX_Cardboard", new Color(0.72f, 0.53f, 0.32f), 0f, 0.15f);
        feed.tapeMaterial = Mat("BOX_Tape", new Color(0.62f, 0.45f, 0.25f), 0f, 0.7f);
        btn.target = feed;
        UnityEditor.EditorUtility.SetDirty(feed);

        // 5) XR: 선택(손으로 잡기 / 레이 트리거)하면 Press
        string xrMsg = "XR Interaction Toolkit이 없어 XR 연결은 건너뛰었어요.";
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
            var comp = root.gameObject.AddComponent(simpleType);
            var evt = simpleType.GetProperty("selectEntered")?.GetValue(comp) as UnityEngine.Events.UnityEventBase;
            if (evt != null)
            {
                UnityEditor.Events.UnityEventTools.AddVoidPersistentListener(evt, new UnityEngine.Events.UnityAction(btn.Press));
                xrMsg = "VR에서 버튼을 선택(그립/트리거)하면 눌려요.";
            }
        }

        UnityEditor.AssetDatabase.SaveAssets();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(conv.scene);
        UnityEditor.Selection.activeGameObject = root.gameObject;
        Debug.Log($"[Button] 빨간 버튼 생성 완료 ({(ok ? best.collider.name : "기본 위치")}). " + xrMsg);
    }

    static GameObject Prim(PrimitiveType type, Transform parent, string name, Vector3 pos, Vector3 scale, Vector3 rot, Material m)
    {
        var g = GameObject.CreatePrimitive(type);
        g.name = name;
        Object.DestroyImmediate(g.GetComponent<Collider>());
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        g.transform.localRotation = Quaternion.Euler(rot);
        g.transform.localScale = scale;
        g.GetComponent<MeshRenderer>().sharedMaterial = m;
        return g;
    }

    static Material Mat(string name, Color c, float metallic, float smooth)
    {
        if (!UnityEditor.AssetDatabase.IsValidFolder(Root)) UnityEditor.AssetDatabase.CreateFolder("Assets", "ConveyorMaterials");
        string path = $"{Root}/{name}.mat";
        var mat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat != null) return mat;
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        mat = new Material(shader);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", c);
        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smooth);
        UnityEditor.AssetDatabase.CreateAsset(mat, path);
        return mat;
    }
}
#endif
