// 초록 버튼을 누르면 Head_Slot에서 테이프 띠가 라벨 프린터처럼 서서히 나오고,
// 다 나오면 끊어져서 아래로 떨어집니다.
// - 에디터 Play 모드: Game 뷰에서 버튼을 마우스로 클릭
// - VR: XR Simple Interactable(버튼)의 Select 이벤트 → Press()
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class MachineTapeDispenser : MonoBehaviour
{
    [Header("References")]
    public Transform button;        // Button_Green
    public Transform spawnPoint;    // Head_Slot
    public Material tapeMaterial;

    [Header("Tape")]
    public float tapeWidth = 0.18f;
    public float tapeThickness = 0.0025f;
    public float tapeLength = 0.35f;
    public float feedSpeed = 0.25f;     // m/s, 나오는 속도
    public float curl = 1.5f;           // 앞쪽으로 말리는 정도 (0 = 일자)
    public int maxTapes = 15;           // 넘으면 오래된 것부터 삭제
    public bool makeGrabbable = true;   // XRI가 있으면 잡을 수 있게

    [Header("Button")]
    public float pushDepth = 0.008f;

    bool busy;
    Vector3 buttonRest;
    readonly Queue<GameObject> tapes = new Queue<GameObject>();

    static System.Type grabType;
    static bool grabSearched;

    void Awake()
    {
        if (button) buttonRest = button.localPosition;
    }

    void Update()
    {
#if ENABLE_INPUT_SYSTEM
        var mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame) TryClick(mouse.position.ReadValue());
#else
        if (Input.GetMouseButtonDown(0)) TryClick(Input.mousePosition);
#endif
    }

    void TryClick(Vector2 screenPos)
    {
        var cam = Camera.main;
        if (cam == null || button == null) return;
        if (Physics.Raycast(cam.ScreenPointToRay(screenPos), out var hit, 50f) && hit.transform == button)
            Press();
    }

    /// 버튼 누르기 (XRI 이벤트나 다른 스크립트에서 호출 가능)
    public void Press()
    {
        if (busy || spawnPoint == null) return;
        busy = true;
        if (button) StartCoroutine(PressAnim());
        StartCoroutine(Feed());
    }

    IEnumerator PressAnim()
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

    IEnumerator Feed()
    {
        var go = new GameObject("Tape");
        go.transform.SetPositionAndRotation(spawnPoint.position - spawnPoint.up * 0.004f, spawnPoint.rotation);
        var mf = go.AddComponent<MeshFilter>();
        var mr = go.AddComponent<MeshRenderer>();
        if (tapeMaterial) mr.sharedMaterial = tapeMaterial;
        var mesh = new Mesh { name = "TapeMesh" };
        mesh.MarkDynamic();
        mf.sharedMesh = mesh;

        // 슬롯에서 서서히 나옴
        float len = 0f;
        while (len < tapeLength)
        {
            len = Mathf.Min(tapeLength, len + feedSpeed * Time.deltaTime);
            BuildStrip(mesh, len);
            yield return null;
        }

        // 끊어져서 떨어짐
        var col = go.AddComponent<MeshCollider>();
        col.sharedMesh = mesh;
        col.convex = true;
        var rb = go.AddComponent<Rigidbody>();
        rb.mass = 0.05f;
        rb.linearDamping = 0.6f;
        rb.angularDamping = 0.8f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.linearVelocity = -spawnPoint.up * 0.05f;

        if (makeGrabbable)
        {
            var gt = FindGrabType();
            if (gt != null) go.AddComponent(gt);
        }

        tapes.Enqueue(go);
        while (tapes.Count > maxTapes)
        {
            var old = tapes.Dequeue();
            if (old) Destroy(old);
        }
        busy = false;
    }

    // 아래로 나오면서 앞쪽(-Z)으로 살짝 말리는 얇은 띠 메시
    void BuildStrip(Mesh mesh, float length)
    {
        int segs = Mathf.Max(2, Mathf.CeilToInt(length / 0.01f));
        float hw = tapeWidth * 0.5f, ht = tapeThickness * 0.5f;

        var verts = new List<Vector3>(); var norms = new List<Vector3>();
        var uvs = new List<Vector2>(); var tris = new List<int>();

        // 각 단면의 중심점과 두께 방향
        var P = new Vector3[segs + 1]; var N = new Vector3[segs + 1]; var D = new Vector3[segs + 1];
        for (int i = 0; i <= segs; i++)
        {
            float s = length * i / segs;
            float a = curl * s;
            Vector3 p = Mathf.Abs(curl) < 1e-4f
                ? new Vector3(0, -s, 0)
                : new Vector3(0, -Mathf.Sin(a) / curl, -(1f - Mathf.Cos(a)) / curl);
            P[i] = p;
            D[i] = new Vector3(0, -Mathf.Cos(a), -Mathf.Sin(a));   // 진행 방향
            N[i] = new Vector3(0, Mathf.Sin(a), -Mathf.Cos(a));    // 앞면 방향
        }

        // 앞/뒤/좌/우 네 면
        AddSide(verts, norms, uvs, tris, segs, i => P[i] + N[i] * ht, hw, Vector3.right, i => N[i], length);
        AddSide(verts, norms, uvs, tris, segs, i => P[i] - N[i] * ht, hw, Vector3.right, i => -N[i], length);
        AddEdge(verts, norms, uvs, tris, segs, P, N, ht, +hw);
        AddEdge(verts, norms, uvs, tris, segs, P, N, ht, -hw);
        // 끝 캡
        AddCap(verts, norms, tris, P[0], N[0], hw, ht, -D[0]);
        AddCap(verts, norms, tris, P[segs], N[segs], hw, ht, D[segs]);

        mesh.Clear();
        mesh.SetVertices(verts); mesh.SetNormals(norms); mesh.SetUVs(0, uvs); mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
    }

    static void AddSide(List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<int> t, int segs,
                        System.Func<int, Vector3> center, float hw, Vector3 x, System.Func<int, Vector3> normal, float length)
    {
        int start = v.Count;
        for (int i = 0; i <= segs; i++)
        {
            Vector3 c = center(i), nn = normal(i);
            v.Add(c - x * hw); n.Add(nn); uv.Add(new Vector2(0, length * i / segs / 0.18f));
            v.Add(c + x * hw); n.Add(nn); uv.Add(new Vector2(1, length * i / segs / 0.18f));
        }
        for (int i = 0; i < segs; i++)
        {
            int a = start + i * 2, b = a + 1, c = a + 2, d = a + 3;
            Tri(t, v, a, b, d, n[a]); Tri(t, v, a, d, c, n[a]);
        }
    }

    static void AddEdge(List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<int> t, int segs,
                        Vector3[] P, Vector3[] N, float ht, float xOff)
    {
        int start = v.Count;
        Vector3 outN = new Vector3(Mathf.Sign(xOff), 0, 0);
        for (int i = 0; i <= segs; i++)
        {
            Vector3 c = P[i] + new Vector3(xOff, 0, 0);
            v.Add(c + N[i] * ht); n.Add(outN); uv.Add(Vector2.zero);
            v.Add(c - N[i] * ht); n.Add(outN); uv.Add(Vector2.zero);
        }
        for (int i = 0; i < segs; i++)
        {
            int a = start + i * 2, b = a + 1, c = a + 2, d = a + 3;
            Tri(t, v, a, b, d, outN); Tri(t, v, a, d, c, outN);
        }
    }

    static void AddCap(List<Vector3> v, List<Vector3> n, List<int> t, Vector3 c, Vector3 nrm, float hw, float ht, Vector3 outN)
    {
        int s = v.Count;
        v.Add(c + new Vector3(-hw, 0, 0) + nrm * ht);
        v.Add(c + new Vector3( hw, 0, 0) + nrm * ht);
        v.Add(c + new Vector3(-hw, 0, 0) - nrm * ht);
        v.Add(c + new Vector3( hw, 0, 0) - nrm * ht);
        for (int k = 0; k < 4; k++) n.Add(outN);
        Tri(t, v, s, s + 1, s + 3, outN); Tri(t, v, s, s + 3, s + 2, outN);
    }

    static void Tri(List<int> t, List<Vector3> v, int a, int b, int c, Vector3 outN)
    {
        Vector3 cr = Vector3.Cross(v[b] - v[a], v[c] - v[a]);
        if (cr.sqrMagnitude < 1e-16f) return;
        if (Vector3.Dot(cr, outN) >= 0) { t.Add(a); t.Add(b); t.Add(c); }
        else { t.Add(a); t.Add(c); t.Add(b); }
    }

    static System.Type FindGrabType()
    {
        if (grabSearched) return grabType;
        grabSearched = true;
        foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            System.Type[] types;
            try { types = asm.GetTypes(); } catch { continue; }
            foreach (var t in types)
                if (t.Name == "XRGrabInteractable" && typeof(Component).IsAssignableFrom(t)) { grabType = t; return t; }
        }
        return null;
    }
}
