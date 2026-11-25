using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// 픽업차량에 붙이는 스크립트. 운전석(CarSeat)에 앉아 있을 때만 차가 "전진"만 합니다. 후진은 없습니다.
// 가속: 왼쪽/오른쪽 조이스틱을 앞으로, 오른쪽 트리거, 또는 키보드 ↑ / W (에디터 테스트용)
// 방향: 운전대(SteeringWheel)를 돌린 방향으로 앞바퀴가 꺾이고, 그 방향으로 차가 앞으로 나아갑니다.
//
// 물리:
//  - 차에 Kinematic Rigidbody 를 붙여 물리 엔진 안에서 움직입니다.
//    (문/좌석을 잡기 위한 차체 MeshCollider 를 그대로 쓰기 위해 Kinematic 으로 둡니다)
//  - 충돌: 차체 크기의 상자(BoxCast)로 이동 경로를 미리 검사해서 건물/벽/가로등 등 맵 콜라이더를 뚫지 않습니다.
//          부딪히면 그 자리에서 멈추고, 회전하다 벽에 닿으면 회전도 막힙니다.
//  - 중력: 네 바퀴 아래로 바닥을 검사해서, 바닥이 없으면 중력 가속도로 떨어지고,
//          바닥에 닿으면 착지합니다. 경사/턱에서는 바퀴 위치에 맞춰 차체가 기울어집니다.
public class CarDrive : MonoBehaviour
{
    [Header("연결")]
    [Tooltip("운전석 (비워두면 자식에서 자동으로 찾음)")]
    public CarSeat seat;
    [Tooltip("운전대 (비워두면 이름에 Stearing/Steering 이 들어간 자식에 자동으로 붙임)")]
    public SteeringWheel steeringWheel;

    [Header("주행")]
    [Tooltip("최고 속도 (m/s)")]
    public float maxSpeed = 8f;
    [Tooltip("가속도 (m/s²)")]
    public float acceleration = 3f;
    [Tooltip("입력을 놓았을 때 감속도 (m/s²)")]
    public float deceleration = 4f;
    [Tooltip("운전대를 끝까지 돌렸을 때 앞바퀴가 꺾이는 각도")]
    public float maxSteerAngle = 35f;
    [Tooltip("앞바퀴가 운전대 방향으로 꺾이는 모습 표시")]
    public bool steerFrontTires = true;

    [Header("물리")]
    [Tooltip("중력 가속도 (m/s²)")]
    public float gravity = 9.81f;
    [Tooltip("바퀴로 올라갈 수 있는 턱 높이 (m)")]
    public float stepHeight = 0.3f;
    [Tooltip("벽과 유지하는 여유 거리 (m)")]
    public float skin = 0.03f;
    [Tooltip("바닥 경사에 맞춰 차체 기울이기")]
    public bool alignToGround = true;
    [Tooltip("기울기가 따라가는 속도 (클수록 빠름)")]
    public float alignSpeed = 8f;
    [Tooltip("충돌/바닥 검사 레이어")]
    public LayerMask collisionLayers = ~0;

    [Header("상태 (읽기용)")]
    [Tooltip("현재 속도 (m/s, 항상 0 이상 = 전진만)")]
    public float speed;
    [Tooltip("현재 앞바퀴 각도 (+ = 오른쪽)")]
    public float steerAngle;
    [Tooltip("바닥에 닿아 있는지")]
    public bool grounded = true;
    [Tooltip("떨어지는 속도 (m/s)")]
    public float fallSpeed;

    Vector3 forwardLocal = Vector3.forward;
    Vector3 upLocal = Vector3.up;
    Vector3 rearAxleLocal;
    float wheelBase = 3f;
    float wheelRadius = 0.35f;
    Vector3[] wheelLocal = new Vector3[0]; // FL, FR, BL, BR 중심 (차 로컬)
    Vector3 boxCenterLocal, boxHalfLocal;   // 충돌용 차체 상자 (차 로컬)
    Rigidbody rb;
    InputAction stickAction, throttleAction;
    float input;
    readonly RaycastHit[] hits = new RaycastHit[64];
    readonly Collider[] overlaps = new Collider[64];
    readonly HashSet<Collider> alreadyOverlapping = new HashSet<Collider>();

    struct Tire { public Transform t; public Vector3 basePos, centerLocal, upLocal; public Quaternion baseRot; }
    Tire[] frontTires = new Tire[0];

    void Awake()
    {
        if (seat == null) seat = GetComponentInChildren<CarSeat>(true);
        upLocal = transform.InverseTransformDirection(Vector3.up).normalized;

        Transform fl = FindChild("FL Tire"), fr = FindChild("FR Tire"), bl = FindChild("BL Tire"), br = FindChild("BR Tire");

        // 앞 방향 / 축간거리 / 뒷바퀴 축
        if (fl != null && bl != null)
        {
            Vector3 f = Vector3.ProjectOnPlane(Center(fl) - Center(bl), Vector3.up);
            if (f.sqrMagnitude > 1e-6f)
            {
                forwardLocal = transform.InverseTransformDirection(f.normalized);
                wheelBase = f.magnitude;
            }
            Vector3 rear = br != null ? (Center(bl) + Center(br)) * 0.5f : Center(bl);
            rearAxleLocal = transform.InverseTransformPoint(rear);
        }

        // 바퀴 중심과 반지름 (순서: FL, FR, BL, BR)
        var wl = new List<Vector3>();
        float radius = 0f; int rc = 0;
        if (fl != null && fr != null && bl != null && br != null)
        {
            foreach (var t in new[] { fl, fr, bl, br })
            {
                wl.Add(transform.InverseTransformPoint(Center(t)));
                var r = Rend(t);
                if (r != null) { radius += r.bounds.extents.y; rc++; }
            }
        }
        wheelLocal = wl.ToArray();
        if (rc > 0) wheelRadius = radius / rc;

        // 앞바퀴 (꺾이는 모습용)
        var list = new List<Tire>();
        foreach (var t in new[] { fl, fr })
        {
            if (t == null || t.parent == null) continue;
            list.Add(new Tire
            {
                t = t,
                basePos = t.localPosition,
                baseRot = t.localRotation,
                centerLocal = t.parent.InverseTransformPoint(Center(t)),
                upLocal = t.parent.InverseTransformDirection(Vector3.up).normalized
            });
        }
        frontTires = list.ToArray();

        SetupBodyBox();

        // 운전대
        if (steeringWheel == null)
        {
            Transform w = FindChild("Stearing");
            if (w == null) w = FindChild("Steering");
            if (w != null)
            {
                steeringWheel = w.GetComponent<SteeringWheel>();
                if (steeringWheel == null) steeringWheel = w.gameObject.AddComponent<SteeringWheel>();
            }
        }

        // Kinematic Rigidbody (차체 MeshCollider 를 유지하면서 물리 엔진 안에서 이동)
        rb = GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        stickAction = new InputAction("Car Stick", InputActionType.Value, expectedControlType: "Vector2");
        stickAction.AddBinding("<XRController>{LeftHand}/{Primary2DAxis}");
        stickAction.AddBinding("<XRController>{RightHand}/{Primary2DAxis}");

        throttleAction = new InputAction("Car Throttle", InputActionType.Value, expectedControlType: "Axis");
        throttleAction.AddBinding("<XRController>{RightHand}/{Trigger}");
        throttleAction.AddBinding("<Keyboard>/upArrow");
        throttleAction.AddBinding("<Keyboard>/w");

        Debug.Log($"[CarDrive] wheelBase={wheelBase:F2} wheelRadius={wheelRadius:F2} wheels={wheelLocal.Length} box center={boxCenterLocal} half={boxHalfLocal}");
    }

    // 차체 범위로 충돌 상자를 만들되, 바닥은 바퀴 중심 높이부터 (낮은 턱은 바퀴가 넘도록)
    void SetupBodyBox()
    {
        Bounds lb = new Bounds();
        bool has = false;
        var mf = GetComponent<MeshFilter>();
        if (mf != null && mf.sharedMesh != null) { lb = mf.sharedMesh.bounds; has = true; }
        else
        {
            foreach (var r in GetComponentsInChildren<Renderer>())
            {
                Bounds b = r.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 c = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    Vector3 l = transform.InverseTransformPoint(c);
                    if (!has) { lb = new Bounds(l, Vector3.zero); has = true; } else lb.Encapsulate(l);
                }
            }
        }
        if (!has) lb = new Bounds(Vector3.up, new Vector3(2f, 1.5f, 4.5f));

        // 차 로컬에서 위쪽에 해당하는 축
        int ax = Mathf.Abs(upLocal.x) > Mathf.Abs(upLocal.y)
            ? (Mathf.Abs(upLocal.x) > Mathf.Abs(upLocal.z) ? 0 : 2)
            : (Mathf.Abs(upLocal.y) > Mathf.Abs(upLocal.z) ? 1 : 2);
        float sign = Mathf.Sign(upLocal[ax]);
        Vector3 min = lb.min, max = lb.max;
        if (wheelLocal.Length > 0)
        {
            float wheelC = 0f;
            foreach (var w in wheelLocal) wheelC += w[ax];
            wheelC /= wheelLocal.Length;
            if (sign > 0) min[ax] = Mathf.Max(min[ax], wheelC);
            else max[ax] = Mathf.Min(max[ax], wheelC);
        }
        boxCenterLocal = (min + max) * 0.5f;
        boxHalfLocal = (max - min) * 0.5f;
    }

    void OnEnable() { stickAction.Enable(); throttleAction.Enable(); }
    void OnDisable() { stickAction.Disable(); throttleAction.Disable(); speed = 0f; }
    void OnDestroy() { stickAction.Dispose(); throttleAction.Dispose(); }

    void Update()
    {
        input = 0f;
        if (seat != null && seat.IsSeated)
        {
            // 앞으로 미는 값만 사용 (뒤로 당기는 값은 무시 → 후진 없음)
            input = Mathf.Clamp01(Mathf.Max(stickAction.ReadValue<Vector2>().y, throttleAction.ReadValue<float>()));
        }

        steerAngle = steeringWheel != null ? steeringWheel.Normalized * maxSteerAngle : 0f;
        ApplyTireSteer();
    }

    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;

        // 공중에서는 가속/감속하지 않음 (관성 유지)
        if (grounded)
        {
            if (input > 0.05f) speed = Mathf.MoveTowards(speed, maxSpeed * input, acceleration * dt);
            else speed = Mathf.MoveTowards(speed, 0f, deceleration * dt);
        }
        speed = Mathf.Max(0f, speed);

        Vector3 pos = rb.position;
        Quaternion rot = rb.rotation;
        Vector3 scale = transform.lossyScale;

        // 1) 수평 이동 + 회전 (뒷바퀴 축 기준 자전거 모델)
        float dist = speed * dt;
        if (dist > 0f)
        {
            Vector3 up = Vector3.up;
            Vector3 fw = Vector3.ProjectOnPlane(rot * forwardLocal, up).normalized;
            float yaw = grounded && wheelBase > 0.1f ? dist * Mathf.Tan(steerAngle * Mathf.Deg2Rad) / wheelBase * Mathf.Rad2Deg : 0f;
            Quaternion turn = Quaternion.AngleAxis(yaw, up);

            Vector3 pivot = pos + rot * Vector3.Scale(rearAxleLocal, scale);
            Vector3 newPivot = pivot + (Quaternion.AngleAxis(yaw * 0.5f, up) * fw) * dist;
            Vector3 targetPos = newPivot + turn * (pos - pivot);
            Quaternion targetRot = turn * rot;

            // 회전: 회전한 자세에서 새로 벽과 겹치면 회전 취소하고 직진
            if (Mathf.Abs(yaw) > 1e-5f && RotationBlocked(pos, rot, targetRot))
            {
                targetRot = rot;
                targetPos = pos + fw * dist;
            }

            // 이동: 차체 상자로 이동 경로 검사
            Vector3 move = targetPos - pos; move.y = 0f;
            float md = move.magnitude;
            if (md > 1e-6f)
            {
                float allowed = CastBody(pos, targetRot, move / md, md);
                if (allowed < md - 1e-4f) speed = 0f; // 부딪힘 → 정지
                pos += move / md * allowed;
            }
            rot = targetRot;
        }

        // 2) 중력 / 바닥 / 기울기
        GroundStep(ref pos, ref rot, dt);

        rb.MovePosition(pos);
        rb.MoveRotation(rot);
    }

    // 네 바퀴 아래 바닥을 찾아 낙하/착지/기울기 처리
    void GroundStep(ref Vector3 pos, ref Quaternion rot, float dt)
    {
        Vector3 scale = transform.lossyScale;
        int n = wheelLocal.Length;
        if (n == 0) { grounded = true; return; }

        float minGap = float.MaxValue;
        int found = 0;
        var contacts = new Vector3[n];
        var ok = new bool[n];
        for (int i = 0; i < n; i++)
        {
            Vector3 wc = pos + rot * Vector3.Scale(wheelLocal[i], scale);
            float bottom = wc.y - wheelRadius;
            Vector3 from = new Vector3(wc.x, wc.y + stepHeight, wc.z);
            if (Ground(from, stepHeight + wheelRadius + 30f, out Vector3 p))
            {
                ok[i] = true; contacts[i] = p; found++;
                minGap = Mathf.Min(minGap, bottom - p.y);
            }
        }

        // 아래에 바닥이 전혀 없으면(맵 밖 등) 떨어뜨리지 않고 그대로 둠
        if (found == 0) { grounded = true; fallSpeed = 0f; return; }

        if (minGap > 0.02f)
        {
            // 공중: 중력 가속
            fallSpeed += gravity * dt;
            float drop = Mathf.Min(fallSpeed * dt, minGap);
            pos.y -= drop;
            grounded = drop >= minGap - 1e-4f;
            if (grounded) fallSpeed = 0f;
        }
        else
        {
            // 바닥: 가장 낮게 떠 있는 바퀴가 바닥에 닿도록 (턱은 부드럽게 올라감)
            grounded = true;
            fallSpeed = 0f;
            pos.y += Mathf.Clamp(-minGap, -0.05f, 4f * dt + 0.01f);
        }

        // 경사에 맞춰 기울기 (네 바퀴 모두 바닥이 있을 때)
        if (alignToGround && grounded && n == 4 && ok[0] && ok[1] && ok[2] && ok[3])
        {
            Vector3 front = (contacts[0] + contacts[1]) * 0.5f, rear = (contacts[2] + contacts[3]) * 0.5f;
            Vector3 left = (contacts[0] + contacts[2]) * 0.5f, right = (contacts[1] + contacts[3]) * 0.5f;
            Vector3 f = front - rear, r = right - left;
            // FL/FR 중 어느 쪽이 실제 오른쪽인지 차 기준으로 맞춤
            Vector3 carRight = Vector3.Cross(Vector3.up, Vector3.ProjectOnPlane(rot * forwardLocal, Vector3.up));
            if (Vector3.Dot(r, carRight) < 0f) r = -r;
            Vector3 normal = Vector3.Cross(f, r).normalized;
            if (normal.y > 0.6f) // 너무 가파른 면은 무시
            {
                Vector3 curUp = rot * upLocal;
                Quaternion target = Quaternion.FromToRotation(curUp, normal) * rot;
                rot = Quaternion.Slerp(rot, target, 1f - Mathf.Exp(-alignSpeed * dt));
            }
        }
    }

    bool Ground(Vector3 from, float maxDist, out Vector3 point)
    {
        point = default;
        int c = Physics.RaycastNonAlloc(from, Vector3.down, hits, maxDist, collisionLayers, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        for (int i = 0; i < c; i++)
        {
            var h = hits[i];
            if (Ignore(h.collider)) continue;
            if (h.distance < best) { best = h.distance; point = h.point; }
        }
        return best < float.MaxValue;
    }

    void BoxAt(Vector3 pos, Quaternion rot, out Vector3 center, out Vector3 half)
    {
        Vector3 scale = transform.lossyScale;
        center = pos + rot * Vector3.Scale(boxCenterLocal, scale);
        half = Vector3.Scale(boxHalfLocal, Abs(scale)) - Vector3.one * skin;
        half = Vector3.Max(half, Vector3.one * 0.01f);
    }

    // 차체 상자를 dir 방향으로 dist 만큼 움직일 때 실제로 갈 수 있는 거리
    float CastBody(Vector3 pos, Quaternion rot, Vector3 dir, float dist)
    {
        BoxAt(pos, rot, out Vector3 center, out Vector3 half);
        int c = Physics.BoxCastNonAlloc(center, half, dir, hits, rot, dist + skin, collisionLayers, QueryTriggerInteraction.Ignore);
        float best = dist + skin;
        for (int i = 0; i < c; i++)
        {
            var h = hits[i];
            if (h.distance <= 0f) continue;     // 이미 겹쳐 있는 것은 빠져나갈 수 있게 무시
            if (Ignore(h.collider)) continue;
            if (h.normal.y > 0.7f) continue;    // 바닥/완만한 경사는 막지 않음
            best = Mathf.Min(best, h.distance);
        }
        return Mathf.Clamp(best - skin, 0f, dist);
    }

    // 회전한 자세에서 새로 겹치는 물체가 있으면 true (원래 겹쳐 있던 것은 무시해서 갇히지 않게)
    bool RotationBlocked(Vector3 pos, Quaternion curRot, Quaternion newRot)
    {
        alreadyOverlapping.Clear();
        BoxAt(pos, curRot, out Vector3 c0, out Vector3 h0);
        int n0 = Physics.OverlapBoxNonAlloc(c0, h0, overlaps, curRot, collisionLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n0; i++) alreadyOverlapping.Add(overlaps[i]);

        BoxAt(pos, newRot, out Vector3 c1, out Vector3 h1);
        int n1 = Physics.OverlapBoxNonAlloc(c1, h1, overlaps, newRot, collisionLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n1; i++)
        {
            var col = overlaps[i];
            if (Ignore(col) || alreadyOverlapping.Contains(col)) continue;
            return true;
        }
        return false;
    }

    bool Ignore(Collider c)
    {
        if (c == null) return true;
        if (c.transform.IsChildOf(transform)) return true; // 차 자신
        if (c.GetComponentInParent<Unity.XR.CoreUtils.XROrigin>() != null) return true; // 플레이어
        return false;
    }

    void ApplyTireSteer()
    {
        float a = steerFrontTires ? steerAngle : 0f;
        foreach (var tire in frontTires)
        {
            if (tire.t == null) continue;
            Quaternion q = Quaternion.AngleAxis(a, tire.upLocal);
            tire.t.localRotation = q * tire.baseRot;
            tire.t.localPosition = tire.centerLocal + q * (tire.basePos - tire.centerLocal);
        }
    }

    // Scene 뷰에서 차를 선택하면 충돌 상자를 노란 선으로 표시
    void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying) return;
        Gizmos.color = Color.yellow;
        Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, transform.lossyScale);
        Gizmos.DrawWireCube(boxCenterLocal, boxHalfLocal * 2f);
    }

    static Vector3 Abs(Vector3 v) { return new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z)); }

    static Renderer Rend(Transform t)
    {
        var r = t.GetComponent<Renderer>();
        if (r == null) r = t.GetComponentInChildren<Renderer>();
        return r;
    }

    static Vector3 Center(Transform t)
    {
        var r = Rend(t);
        return r != null ? r.bounds.center : t.position;
    }

    Transform FindChild(string contains)
    {
        foreach (var t in GetComponentsInChildren<Transform>(true)) if (t.name.Contains(contains)) return t;
        return null;
    }
}
