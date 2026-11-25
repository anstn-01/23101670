// 흐물흐물한 반투명 테이프 (Verlet 로프/리본 시뮬레이션)
// - root(슬롯)에 붙어 있는 동안: 잡고 당기면 슬롯에서 계속 풀려 나옴
// - Cut() 후: 완전히 자유로운 테이프가 되어 처지고 떨어짐
// - 끝부분의 TapeHandle을 VR 손(XR Grab)이나 마우스로 잡을 수 있음
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public class TapeRibbon : MonoBehaviour
{
    [Header("Shape")]
    public float width = 0.08f;
    public float segLen = 0.02f;
    public float radius = 0.005f;           // 충돌 두께

    [Header("Feeding")]
    public float leadLength = 0.12f;        // 버튼 누르면 처음 나오는 길이
    public float autoFeedSpeed = 0.3f;
    public float pullFeedSpeed = 1.5f;      // 당길 때 최대 풀림 속도 (m/s)
    public float maxLength = 3f;
    public int tipFlexPoints = 3;           // 손을 따라 움직이는 끝부분 최소 점 수 (나머지는 슬롯 아래로 수직)

    [Header("Softness")]
    [Range(0.9f, 1f)] public float damping = 0.985f;
    [Range(0f, 1f)] public float bendStiffness = 0.25f;     // 작을수록 흐물흐물
    [Range(0f, 1f)] public float friction = 0.6f;
    public int iterations = 16;

    // 외부(마우스/테스트) 잡기
    [HideInInspector] public bool externalGrab;
    [HideInInspector] public Vector3 externalGrabPoint;

    public Transform root;                  // 슬롯. null이면 끊긴 상태
    public bool IsAttached => root != null;
    public bool IsGrabbed => externalGrab || XRGrabbed;
    public Vector3 TipPosition => p[p.Count - 1];
    public Transform Handle => handle;

    readonly List<Vector3> p = new List<Vector3>();
    readonly List<Vector3> q = new List<Vector3>();
    Mesh mesh;
    Transform handle;
    Component grabComp;
    PropertyInfo isSelectedProp;
    Vector3 lastSide0;
    float feedAccum, sleepTimer;
    bool asleep;
    readonly Collider[] hits = new Collider[16];
    readonly List<Vector3> mv = new List<Vector3>(), mn = new List<Vector3>();
    readonly List<Vector2> muv = new List<Vector2>();
    readonly List<int> mt = new List<int>();

    static SphereCollider probe;
    static readonly HashSet<Collider> ignore = new HashSet<Collider>();
    static readonly Dictionary<Collider, TapeRibbon> handleMap = new Dictionary<Collider, TapeRibbon>();
    static System.Type grabType; static bool grabSearched;

    public static TapeRibbon FromHandle(Collider c) => c != null && handleMap.TryGetValue(c, out var r) ? r : null;

    bool XRGrabbed => grabComp != null && isSelectedProp != null && (bool)isSelectedProp.GetValue(grabComp);
    Vector3 GrabPoint => externalGrab ? externalGrabPoint : handle.position;
    Vector3 RootPos => root.position - root.up * 0.004f;

    public void Init(Transform slot, Material mat, float tapeWidth)
    {
        root = slot;
        width = tapeWidth;
        lastSide0 = slot.right;
        transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

        Vector3 r = RootPos;
        p.Add(r); q.Add(r);
        Vector3 r2 = r - slot.up * segLen * 0.5f;
        p.Add(r2); q.Add(r2);

        mesh = new Mesh { name = "TapeMesh" };
        mesh.MarkDynamic();
        gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = gameObject.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        // 잡는 손잡이 (테이프 끝)
        var h = new GameObject("TapeHandle");
        h.transform.SetParent(transform, false);
        h.transform.position = r2;
        var sc = h.AddComponent<SphereCollider>();
        sc.radius = 0.035f;
        ignore.Add(sc); handleMap[sc] = this;
        var rb = h.AddComponent<Rigidbody>();
        rb.isKinematic = true; rb.useGravity = false;
        handle = h.transform;

        var gt = FindGrabType();
        if (gt != null)
        {
            grabComp = h.AddComponent(gt);
            SetProp(grabComp, "movementType", "Instantaneous");
            SetProp(grabComp, "throwOnDetach", false);
            SetProp(grabComp, "trackRotation", false);
            isSelectedProp = gt.GetProperty("isSelected");
        }
        BuildMesh();
    }

    public void Cut()
    {
        root = null;
        asleep = false;
        sleepTimer = 0f;
    }

    void Update()
    {
        if (p.Count < 2) return;
        if (root == null && p[0].y < -100f) { Destroy(gameObject); return; }
        float dt = Mathf.Min(Time.deltaTime, 1f / 30f);
        if (dt <= 0f) return;
        bool grabbed = IsGrabbed;

        if (asleep && !grabbed && root == null) { SyncHandle(); return; }

        // ---- 풀림 ----
        if (root != null)
        {
            float rest = (p.Count - 1) * segLen;
            if (grabbed)
            {
                Vector3 down = -root.up;
                float vert = Mathf.Max(0f, Vector3.Dot(GrabPoint - RootPos, down));
                float horiz = Vector3.Distance(RootPos + down * vert, GrabPoint);
                float need = vert + Mathf.Max(horiz, segLen * tipFlexPoints) * 1.02f - rest;
                if (need > 0f) feedAccum += Mathf.Min(pullFeedSpeed * dt, need);
            }
            else if (rest < leadLength) feedAccum += autoFeedSpeed * dt;

            while (feedAccum >= segLen && rest < maxLength)
            {
                InsertAtRoot();
                feedAccum -= segLen; rest += segLen;
            }
            if (rest >= maxLength) feedAccum = 0f;
        }

        // ---- 시뮬레이션 ----
        UpdateGuide(grabbed);
        const int sub = 2;
        float h = dt / sub;
        for (int s = 0; s < sub; s++) Step(h, grabbed);

        // ---- 정지 판정 (끊긴 뒤 가만히 있으면 연산 생략) ----
        if (!grabbed && root == null)
        {
            float maxMove = 0f;
            for (int i = 0; i < p.Count; i++) maxMove = Mathf.Max(maxMove, (p[i] - q[i]).sqrMagnitude);
            if (maxMove < 1e-9f) { sleepTimer += dt; if (sleepTimer > 1f) asleep = true; }
            else sleepTimer = 0f;
        }
        else { sleepTimer = 0f; asleep = false; }

        BuildMesh();
        SyncHandle();
    }

    void InsertAtRoot()
    {
        Vector3 n = RootPos - root.up * (segLen * 0.5f);
        p.Insert(1, n); q.Insert(1, n);
    }

    int guide;   // 슬롯 아래로 수직 고정되는 점 개수 (매 프레임 계산)
    int GuideCount => root != null ? guide : 0;

    void UpdateGuide(bool grabbed)
    {
        guide = 0;
        if (root == null) return;
        int maxG = Mathf.Max(0, p.Count - 1 - tipFlexPoints);
        if (!grabbed) { guide = maxG; return; }          // 안 잡으면 전부 수직으로 매달림
        float v = Vector3.Dot(GrabPoint - RootPos, -root.up);   // 손이 슬롯보다 얼마나 아래인지
        guide = Mathf.Clamp(Mathf.FloorToInt(v / segLen) - 1, 0, maxG);
    }

    bool Pinned(int i, bool grabbed) =>
        (root != null && i <= GuideCount) || (i == p.Count - 1 && grabbed);

    void Pin(bool grabbed)
    {
        if (root != null)
        {
            Vector3 r = RootPos, down = -root.up;
            p[0] = r; q[0] = r;
            int g = GuideCount;
            for (int k = 1; k <= g; k++) { p[k] = r + down * (segLen * k); q[k] = p[k]; }   // 슬롯 아래로 곧게
        }
        if (grabbed) { int t = p.Count - 1; p[t] = GrabPoint; q[t] = p[t]; }
    }

    void Step(float h, bool grabbed)
    {
        int n = p.Count;
        Vector3 g = Physics.gravity * (h * h);
        for (int i = 0; i < n; i++)
        {
            if (Pinned(i, grabbed)) continue;
            Vector3 v = (p[i] - q[i]) * damping;
            q[i] = p[i];
            p[i] += v + g;
        }

        for (int it = 0; it < iterations; it++)
        {
            Pin(grabbed);
            for (int i = 0; i < n - 1; i++)
                Constrain(i, i + 1, segLen, 1f, grabbed, false);
            if (bendStiffness > 0f)
                for (int i = 0; i < n - 2; i++)
                    Constrain(i, i + 2, segLen * 2f * 0.98f, bendStiffness, grabbed, true);
        }
        Pin(grabbed);
        Collide(grabbed);
        Pin(grabbed);
    }

    void Constrain(int a, int b, float rest, float k, bool grabbed, bool onlyCompress)
    {
        Vector3 d = p[b] - p[a];
        float len = d.magnitude;
        if (len < 1e-6f) return;
        if (onlyCompress && len >= rest) return;
        float wa = Pinned(a, grabbed) ? 0f : 1f, wb = Pinned(b, grabbed) ? 0f : 1f;
        float sum = wa + wb;
        if (sum <= 0f) return;
        Vector3 corr = d * ((len - rest) / len) * k;
        p[a] += corr * (wa / sum);
        p[b] -= corr * (wb / sum);
    }

    void Collide(bool grabbed)
    {
        EnsureProbe();
        probe.radius = radius;
        for (int i = 0; i < p.Count; i++)
        {
            if (Pinned(i, grabbed)) continue;
            // 빠르게 떨어질 때 얇은 바닥을 뚫지 않도록 이동 경로 검사
            if (Physics.Linecast(q[i], p[i], out var lh, ~0, QueryTriggerInteraction.Ignore)
                && !ignore.Contains(lh.collider) && !(lh.collider is CharacterController))
            {
                p[i] = lh.point + lh.normal * radius;
                q[i] = p[i];
            }
            int c = Physics.OverlapSphereNonAlloc(p[i], radius, hits, ~0, QueryTriggerInteraction.Ignore);
            for (int k = 0; k < c; k++)
            {
                var col = hits[k];
                if (ignore.Contains(col) || col is CharacterController) continue;
                if (Physics.ComputePenetration(probe, p[i], Quaternion.identity,
                        col, col.transform.position, col.transform.rotation, out var dir, out var dist))
                {
                    p[i] += dir * dist;
                    q[i] = Vector3.Lerp(q[i], p[i], friction);   // 마찰: 표면에 닿으면 미끄러짐 줄이기
                }
            }
        }
    }

    void SyncHandle()
    {
        if (handle != null && !XRGrabbed) handle.position = p[p.Count - 1];
    }

    void BuildMesh()
    {
        int n = p.Count;
        mv.Clear(); mn.Clear(); muv.Clear(); mt.Clear();
        Vector3 side = root != null ? root.right : lastSide0;
        float hw = width * 0.5f;
        for (int i = 0; i < n; i++)
        {
            Vector3 t = (p[Mathf.Min(i + 1, n - 1)] - p[Mathf.Max(i - 1, 0)]);
            if (t.sqrMagnitude < 1e-10f) t = Vector3.down;
            t.Normalize();
            Vector3 s = side - Vector3.Dot(side, t) * t;   // 비틀림 없이 옆방향 유지
            if (s.sqrMagnitude < 1e-8f) s = Vector3.Cross(t, Vector3.forward);
            s.Normalize();
            side = s;
            if (i == 0) lastSide0 = s;
            Vector3 nrm = Vector3.Cross(t, s).normalized;
            float v = i * segLen / width;
            mv.Add(p[i] - s * hw); mn.Add(nrm); muv.Add(new Vector2(0, v));
            mv.Add(p[i] + s * hw); mn.Add(nrm); muv.Add(new Vector2(1, v));
        }
        for (int i = 0; i < n - 1; i++)
        {
            int a = i * 2, b = a + 1, c = a + 2, d = a + 3;
            mt.Add(a); mt.Add(c); mt.Add(b);
            mt.Add(b); mt.Add(c); mt.Add(d);
        }
        mesh.Clear();
        mesh.SetVertices(mv); mesh.SetNormals(mn); mesh.SetUVs(0, muv); mesh.SetTriangles(mt, 0);
        mesh.RecalculateBounds();
    }

    void OnDestroy()
    {
        if (handle != null)
        {
            var c = handle.GetComponent<Collider>();
            if (c != null) { ignore.Remove(c); handleMap.Remove(c); }
        }
        if (mesh != null) Destroy(mesh);
    }

    static void EnsureProbe()
    {
        if (probe != null) return;
        var go = new GameObject("TapeProbe") { hideFlags = HideFlags.HideAndDontSave };
        go.transform.position = new Vector3(0, -10000, 0);
        probe = go.AddComponent<SphereCollider>();
        probe.isTrigger = true;
        ignore.Add(probe);
    }

    static void SetProp(Component c, string name, object value)
    {
        var pr = c.GetType().GetProperty(name);
        if (pr == null || !pr.CanWrite) return;
        try
        {
            if (pr.PropertyType.IsEnum && value is string s) pr.SetValue(c, System.Enum.Parse(pr.PropertyType, s));
            else pr.SetValue(c, value);
        }
        catch { }
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
