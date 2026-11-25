// 상자 안에 물건 넣기.
// 컨베이어에서 나온 상자(CardboardBox)에 자동으로 붙습니다. (씬에 따로 배치할 필요 없음)
// - 손에서 놓은 물건(Item_xxx)의 중심이 상자 입구 위/안쪽에 들어오면 상자 안으로 쏙 들어감
// - 상자 안쪽 크기보다 크면 비율을 유지한 채 줄여서 넣음 (가장 잘 맞는 방향으로 눕히기도 함)
// - 넣은 물건은 상자에 고정돼서 상자를 들고 흔들어도 밖으로 튀어나오지 않음
// - 상자 안 물건을 다시 손으로 잡으면 원래 크기로 돌아와서 꺼낼 수 있음
// - 날개 4장을 다 닫고 손을 떼면 테이프가 자동으로
//   앞면 → 윗면 이음새 → 뒷면으로 붙어서 상자가 봉인됨 (날개 잠김, 안 물건 못 꺼냄)
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class BoxSlot : MonoBehaviour
{
    public Vector3 boxSize = new Vector3(0.32f, 0.24f, 0.30f);
    public float wall = 0.008f;
    public float margin = 0.012f;        // 벽과 물건 사이 여유
    public float captureAbove = 0.16f;   // 입구 위 이 높이 안으로 들어오면 넣기
    public float minScale = 0.3f;
    [Tooltip("날개 4장이 다 닫히고 손을 뗀 뒤 이 시간(초)이 지나면 테이프 자동 부착")]
    public float autoSealDelay = 0.3f;
    public bool Sealed { get; private set; }
    internal bool IsLoadedOnPickup => cargoBed != null;

    class Content
    {
        public Rigidbody rb; public Component grab; public Vector3 origScale; public float height;
        public Collider[] cols; public bool settling;
    }
    readonly List<Content> contents = new List<Content>();
    readonly List<(Rigidbody rb, Component grab, Collider[] cols)> outside = new List<(Rigidbody, Component, Collider[])>();
    Collider[] boxCols;
    bool scanner;
    bool tapeWrapComplete;
    float closedSince = -1f;
    Rigidbody cargoBody;
    XRGrabInteractable cargoGrab;
    PickupCargoBed cargoBed;
    bool previousRetainParent;
    bool removedFromBed;
    bool tryCargoDrop;
    float nextScan;
    static readonly Collider[] hits = new Collider[64];

    // ---------- 자동 부착 ----------
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        var go = new GameObject("BoxSlotScanner");
        go.hideFlags = HideFlags.HideInHierarchy;
        go.AddComponent<BoxSlot>().scanner = true;
    }

    void OnEnable()
    {
        if (!TryGetComponent(out cargoGrab) || !TryGetComponent(out cargoBody)) return;
        cargoGrab.selectEntered.AddListener(OnCargoGrabbed);
        cargoGrab.selectExited.AddListener(OnCargoReleased);
    }

    void OnDisable()
    {
        if (cargoGrab == null) return;
        cargoGrab.selectEntered.RemoveListener(OnCargoGrabbed);
        cargoGrab.selectExited.RemoveListener(OnCargoReleased);
        tryCargoDrop = false;
    }

    void OnCargoGrabbed(SelectEnterEventArgs args)
    {
        tryCargoDrop = false;
        if (cargoBed == null) return;
        if (transform.parent == cargoBed.transform) transform.SetParent(null, true);
        cargoBed = null;
        removedFromBed = true;
    }

    void OnCargoReleased(SelectExitEventArgs args)
    {
        if (!cargoGrab.isSelected) tryCargoDrop = tapeWrapComplete && !args.isCanceled;
    }

    void LateUpdate()
    {
        if (cargoGrab == null || cargoBody == null || cargoGrab.isSelected) return;
        // Let XRI and the conveyor finish the release before changing the body's state.
        if (removedFromBed)
        {
            removedFromBed = false;
            cargoGrab.retainTransformParent = previousRetainParent;
            cargoBody.isKinematic = false;
            cargoBody.useGravity = true;
        }
        if (!tryCargoDrop) return;
        tryCargoDrop = false;

        foreach (var bed in FindObjectsByType<PickupCargoBed>())
        {
            if (!bed.TryPlace(this, out Vector3 position, out Quaternion rotation)) continue;
            if (!cargoBody.isKinematic)
            {
                cargoBody.linearVelocity = Vector3.zero;
                cargoBody.angularVelocity = Vector3.zero;
            }
            cargoBody.isKinematic = true;
            cargoBody.useGravity = false;
            previousRetainParent = cargoGrab.retainTransformParent;
            cargoGrab.retainTransformParent = false;
            transform.SetParent(bed.transform, true);
            transform.SetLocalPositionAndRotation(position, rotation);
            cargoBed = bed;
            break;
        }
    }

    void Start()
    {
        if (scanner) return;
        boxCols = GetComponentsInChildren<Collider>(true);
    }

    void Update()
    {
        if (scanner) { Scan(); return; }
        CheckRemoved();
        if (!Sealed && AnyOpen()) Capture();
        WatchOutside();
        if (!Sealed) CheckAutoSeal();
    }

    void Scan()
    {
        if (Time.time < nextScan) return;
        nextScan = Time.time + 0.2f;
        var conv = FindFirstObjectByType<BoxConveyor>();
        foreach (var rb in FindObjectsByType<Rigidbody>(FindObjectsSortMode.None))
        {
            if (rb.name != "CardboardBox" || rb.GetComponent<BoxSlot>() != null) continue;
            var s = rb.gameObject.AddComponent<BoxSlot>();
            if (conv != null) s.boxSize = conv.boxSize;
        }
    }

    // ---------- 넣기 ----------
    void Capture()
    {
        Vector3 s = boxSize;
        float floorY = -s.y * 0.5f + wall;
        float topY = s.y * 0.5f;
        Vector3 qHalf = new Vector3(s.x * 0.5f, (topY + captureAbove - floorY) * 0.5f, s.z * 0.5f);
        Vector3 qCenter = transform.TransformPoint(new Vector3(0, (topY + captureAbove + floorY) * 0.5f, 0));
        int n = Physics.OverlapBoxNonAlloc(qCenter, qHalf, hits, transform.rotation, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            var rb = hits[i].attachedRigidbody;
            if (rb == null || rb.transform == transform || !rb.name.StartsWith("Item_")) continue;
            if (rb.transform.parent != null && rb.transform.parent.GetComponent<BoxSlot>() != null) continue; // 이미 어떤 상자 안
            var grab = FindGrab(rb.gameObject);
            if (IsSelected(grab)) continue;     // 손에 들고 있는 동안은 기다림

            // 물건 중심이 상자 입구 안쪽(수평)에 들어왔는지
            var b = WorldBounds(rb.gameObject);
            Vector3 lc = transform.InverseTransformPoint(b.center);
            if (Mathf.Abs(lc.x) > s.x * 0.5f - wall || Mathf.Abs(lc.z) > s.z * 0.5f - wall) continue;
            if (lc.y < floorY - 0.02f || lc.y > topY + captureAbove) continue;

            if (Insert(rb, grab)) break;   // 한 프레임에 하나씩
        }
    }

    bool Insert(Rigidbody rb, Component grab)
    {
        Vector3 s = boxSize;
        float ix = s.x - wall * 2f - margin * 2f, iz = s.z - wall * 2f - margin * 2f;
        float floorY = -s.y * 0.5f + wall;
        float fill = 0; foreach (var c in contents) fill += c.height;
        float avail = (s.y * 0.5f - 0.012f) - (floorY + fill);    // 닫힌 날개 아래까지
        if (avail < 0.02f) return false;

        // 물건 자체 크기(자기 좌표계)
        var t = rb.transform;
        Bounds lb = LocalBounds(t);

        // 가장 크게 들어가는 방향 고르기 (원래 방향 우선)
        Quaternion best = Quaternion.identity; float bestK = -1, bestH = 0;
        foreach (var e in new[] { Vector3.zero, new Vector3(0, 90, 0), new Vector3(90, 0, 0), new Vector3(0, 0, 90), new Vector3(90, 90, 0), new Vector3(0, 90, 90) })
        {
            var q = Quaternion.Euler(e);
            Vector3 d = RotatedSize(q, lb.size);
            float k = Mathf.Min(1f, ix / d.x, iz / d.z, avail / d.y);
            if (k > bestK + 0.02f) { bestK = k; best = q; bestH = d.y; }
        }
        if (bestK < minScale) return false;   // 상자가 꽉 참

        float k0 = bestK < 1f ? bestK * 0.98f : 1f;
        var item = new Content
        {
            rb = rb, grab = grab, origScale = t.localScale, height = bestH * k0,
            cols = rb.GetComponentsInChildren<Collider>(true), settling = true
        };
        // 물리 멈추고 상자에 고정
        if (!rb.isKinematic) { rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
        rb.isKinematic = true;
        rb.interpolation = RigidbodyInterpolation.None;
        SetIgnore(item.cols, true);
        t.SetParent(transform, true);
        contents.Add(item);

        Vector3 targetCenter = new Vector3(0, floorY + fill + item.height * 0.5f, 0);
        Vector3 targetScale = item.origScale * k0;
        Vector3 targetPos = targetCenter - best * (lb.center * k0);
        StartCoroutine(Settle(item, targetPos, best, targetScale));
        Debug.Log($"[BoxSlot] {rb.name} → 상자 안 (크기 x{k0:F2})");
        return true;
    }

    IEnumerator Settle(Content c, Vector3 pos, Quaternion rot, Vector3 scale)
    {
        var t = c.rb.transform;
        Vector3 p0 = t.localPosition, s0 = t.localScale; Quaternion r0 = t.localRotation;
        for (float a = 0; a < 0.3f; a += Time.deltaTime)
        {
            if (c.rb == null || t.parent != transform) yield break;
            float k = Mathf.SmoothStep(0, 1, a / 0.3f);
            t.localPosition = Vector3.Lerp(p0, pos, k);
            t.localRotation = Quaternion.Slerp(r0, rot, k);
            t.localScale = Vector3.Lerp(s0, scale, k);
            yield return null;
        }
        if (c.rb != null && t.parent == transform)
        { t.localPosition = pos; t.localRotation = rot; t.localScale = scale; }
        c.settling = false;
    }

    // ---------- 날개 / 테이프 봉인 ----------
    BoxFlap[] flaps;
    BoxFlap[] Flaps { get { if (flaps == null || flaps.Length == 0) flaps = GetComponentsInChildren<BoxFlap>(); return flaps; } }
    bool AnyOpen() { var f = Flaps; if (f.Length == 0) return true; foreach (var x in f) if (x.angle > 45f) return true; return false; }
    bool AllClosed() { var f = Flaps; if (f.Length == 0) return false; foreach (var x in f) if (x.angle > 3f) return false; return true; }
    bool AnyHeld() { foreach (var x in Flaps) if (x != null && x.IsHeld) return true; return false; }

    // 날개 4장이 다 닫히고 손을 뗀 상태가 잠깐 유지되면 테이프 자동 부착
    void CheckAutoSeal()
    {
        if (AllClosed() && !AnyHeld())
        {
            if (closedSince < 0f) closedSince = Time.time;
            else if (Time.time - closedSince >= autoSealDelay) Seal(null, 1f);
        }
        else closedSince = -1f;
    }

    public void Seal(TapeRibbon r, float startSide)
    {
        if (Sealed) return;
        Sealed = true;
        foreach (var f in Flaps) { f.locked = true; f.angle = 0f; }
        foreach (var c in contents) if (c.grab is Behaviour b) b.enabled = false;   // 봉인 후엔 못 꺼냄

        // 테이프 재질: 컨베이어에 지정된 테이프 재질(BOX_Tape) 사용
        Material mat = null; float w = 0.08f;
        if (r != null)
        {
            w = r.width;
            var mr = r.GetComponent<MeshRenderer>();
            if (mr != null && mr.sharedMaterial != null)
            {
                mat = new Material(mr.sharedMaterial);
                var key = mat.HasProperty("_BaseColor") ? "_BaseColor" : "_Color";
                if (mat.HasProperty(key)) { var c = mat.GetColor(key); c.a = Mathf.Max(c.a, 0.7f); mat.SetColor(key, c); }
            }
            Destroy(r.gameObject);
        }
        if (mat == null)
        {
            var conv = FindFirstObjectByType<BoxConveyor>();
            if (conv != null && conv.tapeMaterial != null) mat = conv.tapeMaterial;
        }
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            var col = new Color(0.9f, 0.82f, 0.6f);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", col); else mat.color = col;
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.85f);
        }
        StartCoroutine(WrapTape(mat, Mathf.Min(w, boxSize.x * 0.4f), startSide));
        Debug.Log($"[BoxSlot] 테이프 자동 봉인 ({name})");
    }

    IEnumerator WrapTape(Material mat, float w, float side)
    {
        // 이미지처럼: 앞면 아래쪽 → 윗면 이음새(가운데) → 뒷면 아래쪽
        Vector3 s = boxSize;
        float topSurf = s.y * 0.5f + wall + 0.0036f, th = 0.0012f;
        float drop = s.y * 0.6f, zf = s.z * 0.5f + 0.0008f;
        var front = TapePiece("Tape_Front", mat);
        var top = TapePiece("Tape_Top", mat);
        var back = TapePiece("Tape_Back", mat);
        float total = drop * 2f + s.z, dur = 0.8f;
        for (float a = 0; a <= dur + 1e-4f; a += Time.deltaTime)
        {
            float u = Mathf.SmoothStep(0, 1, Mathf.Clamp01(a / dur)) * total;
            float f = Mathf.Clamp(u, 0, drop), tp = Mathf.Clamp(u - drop, 0, s.z), bk = Mathf.Clamp(u - drop - s.z, 0, drop);
            // 앞면: 아래 끝에서 위로
            SetPiece(front, new Vector3(0, topSurf - drop + f * 0.5f, side * zf), new Vector3(w, f, th));
            // 윗면: 앞쪽에서 뒤쪽으로
            SetPiece(top, new Vector3(0, topSurf, side * (s.z * 0.5f - tp * 0.5f)), new Vector3(w, th, tp + 0.002f * Mathf.Sign(tp)));
            // 뒷면: 위에서 아래로
            SetPiece(back, new Vector3(0, topSurf - bk * 0.5f, -side * zf), new Vector3(w, bk, th));
            if (a >= dur) break;
            yield return null;
        }
        SetPiece(front, new Vector3(0, topSurf - drop * 0.5f, side * zf), new Vector3(w, drop, th));
        SetPiece(top, new Vector3(0, topSurf, 0), new Vector3(w, th, s.z + 0.002f));
        SetPiece(back, new Vector3(0, topSurf - drop * 0.5f, -side * zf), new Vector3(w, drop, th));
        tapeWrapComplete = true;
    }

    Transform TapePiece(string n, Material mat)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
        g.name = n;
        Destroy(g.GetComponent<Collider>());
        g.transform.SetParent(transform, false);
        var mr = g.GetComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        g.transform.localScale = Vector3.zero;
        return g.transform;
    }

    static void SetPiece(Transform t, Vector3 pos, Vector3 size)
    {
        t.localPosition = pos;
        t.localScale = (size.x <= 1e-5f || size.y <= 1e-5f || size.z <= 1e-5f) ? Vector3.zero : size;
    }

    // ---------- 꺼내기 ----------
    void CheckRemoved()
    {
        for (int i = contents.Count - 1; i >= 0; i--)
        {
            var c = contents[i];
            if (c.rb == null) { contents.RemoveAt(i); Repack(); continue; }
            if (Sealed) continue;
            if (!IsSelected(c.grab) && c.rb.transform.parent == transform) continue;
            // 손으로 잡아서 꺼냄 → 원래 크기로
            var t = c.rb.transform;
            if (t.parent == transform) t.SetParent(null, true);
            t.localScale = c.origScale;
            SetIgnore(c.cols, false);
            contents.RemoveAt(i);
            outside.Add((c.rb, c.grab, c.cols));
            Repack();
        }
    }

    void WatchOutside()
    {
        // 손에서 놓으면 다시 물리로 (상자 위에서 놓으면 다시 들어감)
        for (int i = outside.Count - 1; i >= 0; i--)
        {
            var o = outside[i];
            if (o.rb == null) { outside.RemoveAt(i); continue; }
            if (IsSelected(o.grab)) continue;
            o.rb.isKinematic = false; o.rb.useGravity = true;
            outside.RemoveAt(i);
        }
    }

    void Repack()
    {
        // 남은 물건들을 바닥부터 다시 쌓기
        float floorY = -boxSize.y * 0.5f + wall, y = floorY;
        foreach (var c in contents)
        {
            var t = c.rb.transform;
            var b = WorldBounds(c.rb.gameObject);
            float cy = transform.InverseTransformPoint(b.center).y;
            t.localPosition += Vector3.up * (y + c.height * 0.5f - cy);
            y += c.height;
        }
    }

    void SetIgnore(Collider[] cols, bool ignore)
    {
        if (boxCols == null) boxCols = GetComponentsInChildren<Collider>(true);
        foreach (var a in cols)
        {
            if (a == null) continue;
            foreach (var b in boxCols) if (b != null && b != a) Physics.IgnoreCollision(a, b, ignore);
        }
    }

    // ---------- 유틸 ----------
    static Vector3 RotatedSize(Quaternion q, Vector3 size)
    {
        Vector3 ax = q * Vector3.right, ay = q * Vector3.up, az = q * Vector3.forward;
        return new Vector3(
            Mathf.Abs(ax.x) * size.x + Mathf.Abs(ay.x) * size.y + Mathf.Abs(az.x) * size.z,
            Mathf.Abs(ax.y) * size.x + Mathf.Abs(ay.y) * size.y + Mathf.Abs(az.y) * size.z,
            Mathf.Abs(ax.z) * size.x + Mathf.Abs(ay.z) * size.y + Mathf.Abs(az.z) * size.z);
    }

    // 물건 루트 좌표계(현재 크기 그대로)의 렌더러 경계
    static Bounds LocalBounds(Transform root)
    {
        var m = Matrix4x4.TRS(root.position, root.rotation, Vector3.one).inverse;
        bool has = false; Bounds b = new Bounds();
        foreach (var mf in root.GetComponentsInChildren<MeshFilter>())
        {
            if (mf.sharedMesh == null) continue;
            var mb = mf.sharedMesh.bounds;
            var w = m * mf.transform.localToWorldMatrix;
            for (int i = 0; i < 8; i++)
            {
                var p = w.MultiplyPoint3x4(mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)));
                if (!has) { b = new Bounds(p, Vector3.zero); has = true; } else b.Encapsulate(p);
            }
        }
        if (!has) b = new Bounds(Vector3.zero, Vector3.one * 0.1f);
        return b;
    }

    static Bounds WorldBounds(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
        var b = rs[0].bounds;
        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
        return b;
    }

    static System.Reflection.PropertyInfo selProp;
    static Component FindGrab(GameObject go)
    {
        foreach (var c in go.GetComponents<Component>())
            if (c != null && c.GetType().Name == "XRGrabInteractable") return c;
        return null;
    }
    static bool IsSelected(Component grab)
    {
        if (grab == null) return false;
        if (selProp == null) selProp = grab.GetType().GetProperty("isSelected");
        return selProp != null && (bool)selProp.GetValue(grab);
    }
}
