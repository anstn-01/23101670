using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

// 운전대(Pick Up_9 Stearing)에 붙는 스크립트. VR 손으로 운전대를 잡고 돌리면 운전대가 축을 중심으로 회전합니다.
// 한 바퀴(360도) 이상 계속 돌릴 수 있고, maxAngle 까지 양쪽으로 돌아갑니다.
// ★ 가운데가 아니라 바깥 테두리(림)만 잡을 수 있어요: 림 바깥쪽을 따라 작은 콜라이더(RimGrab)를 둘러서 그것만 잡기 판정에 씀.
// 회전축은 운전대 메쉬에서 가장 얇은 방향(원판의 두께 방향)으로 자동 계산합니다.
public class SteeringWheel : MonoBehaviour
{
    [Tooltip("양쪽으로 돌릴 수 있는 최대 각도 (360 = 양쪽으로 한 바퀴씩)")]
    public float maxAngle = 360f;
    [Tooltip("손을 놓았을 때 가운데로 돌아가는 속도 (도/초, 0 = 그 자리에 유지)")]
    public float returnSpeed = 0f;
    [Tooltip("자동 계산 대신 쓸 회전축 (운전대 로컬 방향, 0 이면 자동)")]
    public Vector3 axisOverride = Vector3.zero;

    [Header("림(테두리) 잡기")]
    [Tooltip("림 반지름 (운전대 로컬 단위, 0 이면 메쉬 바깥 테두리에서 자동)")]
    public float rimRadius = 0f;
    [Tooltip("림 반지름 자동 계산 비율 (메쉬 바깥 테두리 대비, 1 = 바깥 테두리)")]
    public float rimRatio = 1f;
    [Tooltip("잡기 판정을 바깥쪽으로 더 밀어내는 거리 (월드 m)")]
    public float rimOutward = 0.01f;
    [Tooltip("림 잡기 판정 두께 (월드 m)")]
    public float rimThickness = 0.06f;
    public int rimSegments = 24;

    [Tooltip("현재 운전대 각도. 운전자 시점에서 시계방향(오른쪽으로 돌림)이 +")]
    public float angle;

    /// <summary>-1(왼쪽 끝) ~ +1(오른쪽 끝)</summary>
    public float Normalized => maxAngle > 0f ? Mathf.Clamp(angle / maxAngle, -1f, 1f) : 0f;

    XRSimpleInteractable interactable;
    Vector3 pivotLocal;      // 부모 기준 회전 중심
    Vector3 axisLocal;       // 부모 기준 회전축
    Vector3 meshAxis;        // 메쉬 로컬 기준 회전축
    Vector3 meshCenter;      // 메쉬 로컬 기준 중심 (림이 있는 깊이)
    float meshRadius;        // 메쉬 로컬 기준 림 반지름
    float rightSign = 1f;    // AngleAxis(+) 가 운전자 기준 시계방향이면 +1
    Vector3 basePos;
    Quaternion baseRot;

    IXRSelectInteractor grabber;
    float lastHandAngle;
    bool hasTest; Vector3 testHand; bool testWasHeld;

    /// 테스트/녹화용: 이 위치에 손이 림을 잡고 있다고 가정
    public void DriveHand(Vector3 worldPos) { hasTest = true; testHand = worldPos; }
    public void ReleaseHand() { hasTest = false; }

    /// 림 위의 점(월드). deg: 운전대와 함께 도는 각도 (0 = 운전대 맨 위 기준)
    public Vector3 RimPoint(float deg)
    {
        Vector3 up0 = Vector3.ProjectOnPlane(Vector3.up, meshAxis);
        if (up0.sqrMagnitude < 1e-6f) up0 = Vector3.ProjectOnPlane(Vector3.forward, meshAxis);
        Vector3 d = Quaternion.AngleAxis(deg, meshAxis) * up0.normalized;
        return transform.TransformPoint(meshCenter + d * meshRadius);
    }

    void Awake()
    {
        basePos = transform.localPosition;
        baseRot = transform.localRotation;
        SetupAxis();
        SetupInteractable();
    }

    void SetupAxis()
    {
        Transform p = transform.parent;
        var mf = GetComponent<MeshFilter>();
        Bounds b = mf != null && mf.sharedMesh != null ? mf.sharedMesh.bounds : new Bounds(Vector3.zero, Vector3.one * 0.4f);

        Vector3 aLocal;
        if (axisOverride.sqrMagnitude > 1e-6f) aLocal = axisOverride.normalized;
        else
        {
            // 메쉬 로컬 기준 가장 얇은 축 = 운전대 원판의 축
            Vector3 s = Vector3.Scale(b.size, transform.lossyScale);
            aLocal = Vector3.right;
            if (Mathf.Abs(s.y) < Mathf.Abs(s.x) && Mathf.Abs(s.y) <= Mathf.Abs(s.z)) aLocal = Vector3.up;
            else if (Mathf.Abs(s.z) < Mathf.Abs(s.x) && Mathf.Abs(s.z) < Mathf.Abs(s.y)) aLocal = Vector3.forward;
        }
        Vector3 axisW = transform.TransformDirection(aLocal).normalized;

        // 림 반지름: 축에 수직인 두 방향 크기 중 큰 쪽의 절반 (= 바깥 테두리)
        meshAxis = aLocal; meshCenter = b.center;
        float r = 0f;
        for (int i = 0; i < 3; i++)
        {
            Vector3 e = Vector3.zero; e[i] = 1f;
            if (Mathf.Abs(Vector3.Dot(e, aLocal)) > 0.9f) continue;
            r = Mathf.Max(r, b.size[i] * 0.5f);
        }
        meshRadius = rimRadius > 0f ? rimRadius : r * rimRatio;

        // 메쉬를 읽을 수 있으면 실제 바깥 테두리 정점으로 반지름/깊이를 정확히 맞춤
        if (rimRadius <= 0f && mf != null && mf.sharedMesh != null && mf.sharedMesh.isReadable)
            FitRimFromVertices(mf.sharedMesh, b.center, aLocal);

        // 차의 앞 방향 / 오른쪽
        Vector3 carFwd = CarForward();
        Vector3 carRight = Vector3.Cross(Vector3.up, carFwd).normalized;

        // 축이 운전자(뒤쪽)를 향하도록
        if (Vector3.Dot(axisW, -carFwd) < 0f) axisW = -axisW;

        Vector3 pivotW = transform.TransformPoint(b.center);
        pivotLocal = p != null ? p.InverseTransformPoint(pivotW) : pivotW;
        axisLocal = p != null ? p.InverseTransformDirection(axisW).normalized : axisW;

        // 운전대 맨 위 점을 +10도 돌렸을 때 오른쪽으로 가면 +가 시계방향
        Vector3 top = Vector3.ProjectOnPlane(Vector3.up, axisW).normalized;
        Vector3 moved = Quaternion.AngleAxis(10f, axisW) * top;
        rightSign = Vector3.Dot(moved - top, carRight) > 0f ? 1f : -1f;

        Debug.Log($"[SteeringWheel] axis(world)={axisW} pivot={pivotW} rimRadius={meshRadius} rightSign={rightSign}");
    }

    // 회전축에서 가장 먼 정점들(바깥 테두리)의 반지름과 깊이를 사용
    void FitRimFromVertices(Mesh m, Vector3 c, Vector3 axis)
    {
        var vs = m.vertices;
        if (vs == null || vs.Length == 0) return;
        float maxR = 0f;
        foreach (var v in vs)
        {
            Vector3 d = v - c;
            float ax = Vector3.Dot(d, axis);
            maxR = Mathf.Max(maxR, (d - axis * ax).magnitude);
        }
        if (maxR <= 1e-5f) return;

        float sumA = 0f; int n = 0;
        foreach (var v in vs)
        {
            Vector3 d = v - c;
            float ax = Vector3.Dot(d, axis);
            if ((d - axis * ax).magnitude < maxR * 0.85f) continue;
            sumA += ax; n++;
        }
        meshRadius = maxR * rimRatio;
        if (n > 0) meshCenter = c + axis * (sumA / n);
    }

    void SetupInteractable()
    {
        // 가운데(운전대 전체)에 있던 콜라이더는 잡기에서 빼기 → 림만 잡힘
        foreach (var c in GetComponents<Collider>()) c.enabled = false;
        var old = GetComponent<XRSimpleInteractable>();
        if (old != null) Destroy(old);

        // 림 바깥쪽을 따라 콜라이더 고리 만들기 (운전대 자식이라 같이 돎)
        var rim = new GameObject("RimGrab");
        rim.transform.SetParent(transform, false);
        Vector3 u = Vector3.ProjectOnPlane(Vector3.up, meshAxis);
        if (u.sqrMagnitude < 1e-6f) u = Vector3.ProjectOnPlane(Vector3.forward, meshAxis);
        u.Normalize();
        Vector3 v = Vector3.Cross(meshAxis, u).normalized;
        float ls = Mathf.Max(1e-4f, (Mathf.Abs(transform.lossyScale.x) + Mathf.Abs(transform.lossyScale.y) + Mathf.Abs(transform.lossyScale.z)) / 3f);
        float thick = rimThickness / ls;
        float grabR = meshRadius + rimOutward / ls;   // 바깥 테두리보다 살짝 바깥
        int n = Mathf.Max(8, rimSegments);
        float segLen = 2f * Mathf.PI * grabR / n * 1.1f;
        for (int i = 0; i < n; i++)
        {
            float th = i * Mathf.PI * 2f / n;
            Vector3 dir = u * Mathf.Cos(th) + v * Mathf.Sin(th);
            Vector3 tan = -u * Mathf.Sin(th) + v * Mathf.Cos(th);
            var s = new GameObject("Rim_" + i);
            s.transform.SetParent(rim.transform, false);
            s.transform.localPosition = meshCenter + dir * grabR;
            s.transform.localRotation = Quaternion.LookRotation(tan, meshAxis);
            var bc = s.AddComponent<BoxCollider>();
            bc.size = new Vector3(thick, thick, segLen);
        }

        // RimGrab 자식 콜라이더만 수집하도록 RimGrab 에 인터랙터블을 붙임
        interactable = rim.AddComponent<XRSimpleInteractable>();
        interactable.selectMode = InteractableSelectMode.Multiple; // 양손으로 잡기 가능
    }

    void Update()
    {
        // 잡고 있는 손 (여러 손이면 가장 먼저 잡은 손)
        IXRSelectInteractor current = interactable != null && interactable.isSelected ? interactable.interactorsSelecting[0] : null;
        if (current != grabber)
        {
            grabber = current;
            if (grabber != null) lastHandAngle = HandAngle(HandPos());
        }
        if (hasTest && !testWasHeld) lastHandAngle = HandAngle(testHand);
        testWasHeld = hasTest;

        if (grabber != null || hasTest)
        {
            float a = HandAngle(grabber != null ? HandPos() : testHand);
            float delta = Mathf.DeltaAngle(lastHandAngle, a); // 프레임마다 누적 → 180도 넘게 계속 돌릴 수 있음
            lastHandAngle = a;
            angle = Mathf.Clamp(angle + delta * rightSign, -maxAngle, maxAngle);
        }
        else if (returnSpeed > 0f)
        {
            angle = Mathf.MoveTowards(angle, 0f, returnSpeed * Time.deltaTime);
        }

        Apply();
    }

    Vector3 HandPos() => grabber.GetAttachTransform(interactable).position;

    float HandAngle(Vector3 hand)
    {
        Transform p = transform.parent;
        Vector3 pivot = p != null ? p.TransformPoint(pivotLocal) : pivotLocal;
        Vector3 axis = p != null ? p.TransformDirection(axisLocal) : axisLocal;
        Vector3 refDir = Vector3.ProjectOnPlane(p != null ? p.up : Vector3.up, axis);
        Vector3 v = Vector3.ProjectOnPlane(hand - pivot, axis);
        if (v.sqrMagnitude < 1e-6f) return lastHandAngle;
        return Vector3.SignedAngle(refDir, v, axis);
    }

    void Apply()
    {
        Quaternion q = Quaternion.AngleAxis(angle * rightSign, axisLocal);
        transform.localRotation = q * baseRot;
        transform.localPosition = pivotLocal + q * (basePos - pivotLocal);
    }

    Vector3 CarForward()
    {
        Transform car = transform.parent != null ? transform.parent : transform;
        Transform fl = null, bl = null;
        foreach (var t in car.GetComponentsInChildren<Transform>(true))
        {
            if (t.name.Contains("FL Tire")) fl = t;
            else if (t.name.Contains("BL Tire")) bl = t;
        }
        if (fl != null && bl != null)
        {
            Vector3 f = Vector3.ProjectOnPlane(fl.position - bl.position, Vector3.up);
            if (f.sqrMagnitude > 1e-6f) return f.normalized;
        }
        return car.forward;
    }

    [ContextMenu("Center (가운데로)")]
    public void Center() { angle = 0f; }
}
