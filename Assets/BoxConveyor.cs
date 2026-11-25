// 컨베이어 본체(터널)에서 상자를 내보내고 벨트를 따라 테이블까지 옮깁니다.
// Conveyor_BD01 에 붙습니다. SpawnBox() 를 부르면 상자가 하나 나와요.
// 상자는 3종류(기본 / 낮은 상자 / 긴 상자)가 순서대로 번갈아 나와요.
using System.Collections.Generic;
using UnityEngine;

public class BoxConveyor : MonoBehaviour
{
    [Header("경로 (Conveyor_BD01 로컬 좌표)")]
    public Vector3 spawnLocal = new Vector3(2.48f, 0.75f, 0f);   // 본체 터널 안
    public Vector3 beltEndLocal = new Vector3(0.3f, 0.75f, 0f);  // 벨트 끝
    public Vector3 stopLocal = new Vector3(0.3f, 0.75f, 0f);     // 멈추는 자리 (폭 0.32 상자 기준 중심)

    [Header("움직임")]
    public float speed = 0.45f;
    public float spacing = 0.66f;   // 열린 날개까지 고려한 간격
    public int maxBoxes = 6;

    [Header("상자")]
    public Vector3 boxSize = new Vector3(0.36f, 0.27f, 0.34f);   // 기본 상자
    public string[] boxTypeNames = { "Standard", "Low", "Long" };
    public Vector3[] boxTypes =
    {
        new Vector3(0.36f, 0.27f, 0.34f),   // 기본 (원래보다 조금 큼)
        new Vector3(0.36f, 0.16f, 0.34f),   // 높이가 낮은 상자
        new Vector3(0.54f, 0.27f, 0.34f),   // 가로 1.5배 긴 상자
    };
    public int nextType = 0;
    public Material boxMaterial;
    public Material tapeMaterial;

    class Item
    {
        public Transform t; public Rigidbody rb; public Component grab;
        public float dist, len; public Vector3 size; public bool detached, released;
    }
    readonly List<Item> items = new List<Item>();
    public Transform LastBox { get; private set; }
    public bool IsMoving(Transform box) { foreach (var it in items) if (it.t == box) return !it.detached && it.dist < it.len - 1e-3f; return false; }
    readonly List<Transform> ribs = new List<Transform>();
    float ribMin, ribMax;
    static System.Type grabType, simpleType; static bool grabSearched;
    System.Reflection.PropertyInfo isSelectedProp;

    void Awake()
    {
        spacing = Mathf.Max(spacing, 0.66f);   // 이전 씬 값 보정 (열린 날개)
        if (boxTypes == null || boxTypes.Length == 0) boxTypes = new[] { boxSize };
        var conv = transform.Find("Conveyor");
        if (conv != null)
        {
            ribMin = float.MaxValue; ribMax = float.MinValue;
            foreach (Transform c in conv)
                if (c.name == "BeltRib")
                {
                    ribs.Add(c);
                    ribMin = Mathf.Min(ribMin, c.localPosition.x);
                    ribMax = Mathf.Max(ribMax, c.localPosition.x);
                }
            if (ribs.Count > 1) ribMax += (ribMax - ribMin) / (ribs.Count - 1);
        }
    }

    float PathLength => Vector3.Distance(spawnLocal, beltEndLocal) + Vector3.Distance(beltEndLocal, stopLocal);

    Vector3 LocalAt(float d)
    {
        float a = Vector3.Distance(spawnLocal, beltEndLocal);
        if (d <= a) return Vector3.Lerp(spawnLocal, beltEndLocal, a > 0 ? d / a : 1f);
        float b = Vector3.Distance(beltEndLocal, stopLocal);
        return Vector3.Lerp(beltEndLocal, stopLocal, b > 0 ? Mathf.Clamp01((d - a) / b) : 1f);
    }

    Vector3 WorldAt(float d, Vector3 size) => transform.TransformPoint(LocalAt(d) + Vector3.up * size.y * 0.5f);

    // 열린 날개까지 포함한 두 상자 중심 사이 최소 간격
    float Gap(Vector3 a, Vector3 b) => Mathf.Max(spacing, a.x + b.x + 0.03f);

    public void SpawnBox()
    {
        int onPath = 0;
        foreach (var it in items) if (!it.detached) onPath++;
        if (onPath >= maxBoxes) return;
        int ti = ((nextType % boxTypes.Length) + boxTypes.Length) % boxTypes.Length;
        Vector3 size = boxTypes[ti];
        // 출구가 막혀 있으면 기다림
        foreach (var it in items) if (!it.detached && it.dist < Gap(it.size, size)) return;
        nextType = (ti + 1) % boxTypes.Length;

        var box = BuildBox(size, out var grab);
        box.position = WorldAt(0, size);
        box.rotation = transform.rotation;
        // 큰 상자는 앞쪽 끝이 같은 자리에 오도록 조금 일찍 멈춤
        float shift = Mathf.Max(0f, size.x * 0.5f - 0.16f);
        var item = new Item { t = box, rb = box.GetComponent<Rigidbody>(), dist = 0, grab = grab, size = size, len = Mathf.Max(0.01f, PathLength - shift) };
        items.Add(item);
        LastBox = box;
    }

    void Update()
    {
        float ahead = float.MaxValue;   // 앞 상자의 위치
        Vector3 aheadSize = Vector3.zero;
        bool moving = false;
        for (int i = 0; i < items.Count; i++)
        {
            var it = items[i];
            if (it.t == null) { items.RemoveAt(i--); continue; }

            if (!it.detached)
            {
                // 플레이어가 잡았거나 밀려났으면 경로에서 빠짐
                bool selected = IsSelected(it.grab);
                if (selected || (it.t.position - WorldAt(it.dist, it.size)).sqrMagnitude > 0.06f * 0.06f)
                { it.detached = true; continue; }

                float limit = Mathf.Min(it.len, ahead == float.MaxValue ? float.MaxValue : ahead - Gap(aheadSize, it.size));
                float nd = Mathf.MoveTowards(it.dist, Mathf.Max(it.dist, limit), speed * Time.deltaTime);
                if (nd > it.dist + 1e-5f) moving = true;
                it.dist = nd;
                it.t.position = WorldAt(it.dist, it.size);
                it.t.rotation = transform.rotation;
                ahead = it.dist; aheadSize = it.size;
            }
            else if (!it.released && !IsSelected(it.grab))
            {
                it.released = true;
                it.rb.isKinematic = false;
                it.rb.useGravity = true;
            }
        }

        // 벨트 줄무늬가 흐르는 것처럼
        if (moving && ribs.Count > 1)
        {
            float span = ribMax - ribMin;
            foreach (var r in ribs)
            {
                var p = r.localPosition;
                p.x -= speed * Time.deltaTime;
                if (p.x < ribMin) p.x += span;
                r.localPosition = p;
            }
        }
    }

    bool IsSelected(Component grab)
    {
        if (grab == null) return false;
        if (isSelectedProp == null) isSelectedProp = grab.GetType().GetProperty("isSelected");
        return isSelectedProp != null && (bool)isSelectedProp.GetValue(grab);
    }

    static void FindXR()
    {
        if (grabSearched) return;
        grabSearched = true;
        foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            System.Type[] types;
            try { types = asm.GetTypes(); } catch { continue; }
            foreach (var t in types)
            {
                if (!typeof(Component).IsAssignableFrom(t)) continue;
                if (t.Name == "XRGrabInteractable") grabType = t;
                else if (t.Name == "XRSimpleInteractable") simpleType = t;
            }
            if (grabType != null && simpleType != null) break;
        }
    }

    static Component AddGrab(GameObject go)
    {
        FindXR();
        if (grabType == null) return null;
        var c = go.AddComponent(grabType);
        var p = grabType.GetProperty("throwOnDetach");
        if (p != null && p.CanWrite) p.SetValue(c, false);
        return c;
    }

    Transform BuildBox(Vector3 s, out Component grab)
    {
        float th = 0.008f;   // 판지 두께
        var root = new GameObject("CardboardBox").transform;
        var rb = root.gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true; rb.useGravity = false; rb.mass = 1.2f;

        // 열린 상자: 바닥 + 벽 4개 (안에 물건을 넣을 수 있게 콜라이더도 벽마다)
        var inside = new Color(0.62f, 0.45f, 0.27f);
        Wall(root, "Bottom", new Vector3(0, -s.y * 0.5f + th * 0.5f, 0), new Vector3(s.x, th, s.z));
        Wall(root, "Wall_XP", new Vector3( s.x * 0.5f - th * 0.5f, 0, 0), new Vector3(th, s.y, s.z));
        Wall(root, "Wall_XN", new Vector3(-s.x * 0.5f + th * 0.5f, 0, 0), new Vector3(th, s.y, s.z));
        Wall(root, "Wall_ZP", new Vector3(0, 0,  s.z * 0.5f - th * 0.5f), new Vector3(s.x - th * 2f, s.y, th));
        Wall(root, "Wall_ZN", new Vector3(0, 0, -s.z * 0.5f + th * 0.5f), new Vector3(s.x - th * 2f, s.y, th));
        Part(root, "InnerFloor", new Vector3(0, -s.y * 0.5f + th + 0.0005f, 0), new Vector3(s.x - th * 2f, 0.001f, s.z - th * 2f), null, inside);
        Part(root, "Label", new Vector3(s.x * 0.18f, -0.02f, -s.z * 0.5f - 0.001f), new Vector3(0.09f, 0.06f, 0.001f), null, new Color(0.95f, 0.95f, 0.93f));

        // 상자 자체 잡기 (벽 콜라이더만 사용하도록 날개보다 먼저 붙임)
        grab = AddGrab(root.gameObject);

        // 윗날개 4장: Z쪽(아래 깔림) 먼저, X쪽(위에 덮음)
        float top = s.y * 0.5f;
        Flap(root, "Flap_ZP", new Vector3(0, top, s.z * 0.5f), Vector3.back,    s.z * 0.5f, s.x - 0.004f, false, 198f);
        Flap(root, "Flap_ZN", new Vector3(0, top, -s.z * 0.5f), Vector3.forward, s.z * 0.5f, s.x - 0.004f, false, 202f);
        Flap(root, "Flap_XP", new Vector3(s.x * 0.5f, top + th, 0), Vector3.left,  s.x * 0.5f, s.z - 0.004f, true, 205f);
        Flap(root, "Flap_XN", new Vector3(-s.x * 0.5f, top + th, 0), Vector3.right, s.x * 0.5f, s.z - 0.004f, true, 195f);

        // 물건 넣기 + 테이프 봉인
        var slot = root.gameObject.AddComponent<BoxSlot>();
        slot.boxSize = s; slot.wall = th;
        return root;
    }

    void Wall(Transform root, string name, Vector3 pos, Vector3 size)
    {
        Part(root, name, pos, size, boxMaterial);
        var c = root.gameObject.AddComponent<BoxCollider>();
        c.center = pos; c.size = size;
    }

    void Flap(Transform root, string name, Vector3 hinge, Vector3 inward, float len, float width, bool over, float openAngle)
    {
        var pivot = new GameObject(name).transform;
        pivot.SetParent(root, false);
        pivot.localPosition = hinge;
        var f = pivot.gameObject.AddComponent<BoxFlap>();
        f.inwardLocal = inward; f.length = len; f.over = over; f.angle = openAngle;

        // 날개 판 (피벗 +Z 방향으로 뻗음)
        var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
        g.name = "Panel";
        g.transform.SetParent(pivot, false);
        g.transform.localPosition = new Vector3(0, 0, len * 0.5f);
        g.transform.localScale = new Vector3(width, 0.006f, len);
        g.GetComponent<MeshRenderer>().sharedMaterial = boxMaterial;
        var bc = g.GetComponent<BoxCollider>();
        bc.size = new Vector3(1f, 2f, 1f);          // 날개 판 자체는 물리 충돌만 (잡기 X)

        // 잡는 곳: 날개 끝부분(바깥쪽 35%)만. 판 콜라이더는 여기에 등록되지 않음
        var tip = new GameObject("GrabTip");
        tip.transform.SetParent(pivot, false);
        tip.transform.localPosition = new Vector3(0, 0, len * 0.825f);
        var tc = tip.AddComponent<BoxCollider>();
        tc.size = new Vector3(width, 0.035f, len * 0.35f);

        FindXR();
        if (simpleType != null) f.SetInteractable(tip.AddComponent(simpleType));
    }

    static void Part(Transform parent, string name, Vector3 pos, Vector3 size, Material m, Color? c = null)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
        g.name = name;
        DestroyImmediate(g.GetComponent<Collider>());
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        g.transform.localScale = size;
        var r = g.GetComponent<MeshRenderer>();
        if (m != null) r.sharedMaterial = m;
        else if (c.HasValue)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var mat = new Material(shader);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c.Value);
            else mat.color = c.Value;
            r.sharedMaterial = mat;
        }
    }
}
