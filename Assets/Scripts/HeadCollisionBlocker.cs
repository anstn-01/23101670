using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// XR Origin 에 붙이는 스크립트.
// 헤드셋(카메라)이 직접 움직일 때(실제로 걸어다니거나, 시뮬레이터 WASD)도
// 메쉬 콜라이더를 통과하지 못하게 XR Origin 을 뒤로 밀어냅니다.
// 발밑(계단 높이) ~ 머리 높이까지의 캡슐로 검사하므로 낮은 벤치/울타리부터 벽까지 각 메쉬 모양대로 막힙니다.
[DefaultExecutionOrder(1000)]
public class HeadCollisionBlocker : MonoBehaviour
{
    [Tooltip("몸통 반지름 (m)")]
    public float bodyRadius = 0.2f;
    [Tooltip("이 높이 이하 턱은 넘어갈 수 있음 (m)")]
    public float stepHeight = 0.45f;
    [Tooltip("벽과 유지하는 여유 거리 (m)")]
    public float skin = 0.03f;
    [Tooltip("벽에 막혔을 때 벽을 따라 미끄러지듯 이동")]
    public bool slideAlongWalls = true;
    [Tooltip("머리 아래 바닥 높이를 따라 몸 높이를 맞춤 (턱/계단 오르내리기)")]
    public bool followGround = true;
    [Tooltip("한 번에 내려갈 수 있는 최대 높이 (m)")]
    public float maxStepDown = 1.0f;
    [Tooltip("검사할 레이어")]
    public LayerMask collisionLayers = ~0;

    XROrigin origin;
    CharacterController cc;
    Transform cam;
    Vector3 prevLocalHead;
    bool hasPrev;
    readonly RaycastHit[] hits = new RaycastHit[32];

    void Awake()
    {
        origin = GetComponent<XROrigin>();
        cc = GetComponent<CharacterController>();
    }

    void OnEnable() { hasPrev = false; }

    void LateUpdate()
    {
        if (cam == null)
        {
            if (origin != null && origin.Camera != null) cam = origin.Camera.transform;
            else if (Camera.main != null) cam = Camera.main.transform;
            if (cam == null) return;
        }

        Vector3 head = cam.position;
        Vector3 localHead = transform.InverseTransformPoint(head);
        if (!hasPrev) { prevLocalHead = localHead; hasPrev = true; return; }

        // XR Origin 자체 이동(조이스틱/점프/텔레포트/회전)은 빼고, 머리만 움직인 양을 계산
        Vector3 start = transform.TransformPoint(prevLocalHead);
        Vector3 delta = head - start;
        delta.y = 0f;
        prevLocalHead = localHead;

        float dist = delta.magnitude;
        if (dist < 0.0001f) return;
        Vector3 dir = delta / dist;

        Vector3 allowed = Sweep(start, head.y, dir, dist, out RaycastHit hit);
        Vector3 reached = start + allowed;

        if (slideAlongWalls && allowed.magnitude < dist - 0.0001f)
        {
            Vector3 remaining = delta - allowed;
            Vector3 n = hit.normal; n.y = 0f;
            if (n.sqrMagnitude > 0.0001f)
            {
                Vector3 slide = Vector3.ProjectOnPlane(remaining, n.normalized);
                float sd = slide.magnitude;
                if (sd > 0.0001f) reached += Sweep(reached, head.y, slide / sd, sd, out _);
            }
        }

        Vector3 correction = reached - (start + delta);
        correction.y = 0f;

        // 머리 아래 바닥 높이에 맞춰 몸 높이 조정 (인도 턱/계단을 걸어서 오르내리기)
        if (followGround && (cc == null || cc.velocity.y < 0.5f))
        {
            float feetY = transform.position.y;
            Vector3 probe = new Vector3(reached.x, feetY + stepHeight + 0.1f, reached.z);
            if (GroundBelow(probe, stepHeight + 0.1f + maxStepDown, out float groundY))
            {
                float dy = groundY - feetY;
                if (Mathf.Abs(dy) > 0.005f) correction.y = dy;
            }
        }

        if (correction.sqrMagnitude < 1e-8f) return;

        // XR Origin 을 밀어서 머리를 막힌 위치로 되돌림
        bool ccOn = cc != null && cc.enabled;
        if (ccOn) cc.enabled = false;
        transform.position += correction;
        if (ccOn) cc.enabled = true;
    }

    bool GroundBelow(Vector3 from, float maxDist, out float groundY)
    {
        groundY = 0f;
        int n = Physics.SphereCastNonAlloc(from, 0.05f, Vector3.down, hits, maxDist, collisionLayers, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        for (int i = 0; i < n; i++)
        {
            var h = hits[i];
            if (h.distance <= 0f) continue;
            if (h.collider.transform.IsChildOf(transform)) continue;
            if (h.collider.GetComponentInParent<XRBaseInteractable>() != null) continue;
            if (h.distance < best) { best = h.distance; groundY = h.point.y; }
        }
        return best < float.MaxValue;
    }

    // start 위치의 몸통 캡슐을 dir 방향으로 dist 만큼 이동시켰을 때 실제 이동 가능한 벡터
    Vector3 Sweep(Vector3 start, float headY, Vector3 dir, float dist, out RaycastHit nearest)
    {
        nearest = default;
        float feetY = transform.position.y;
        float bottomY = feetY + stepHeight + bodyRadius;
        float topY = Mathf.Max(bottomY, headY);
        Vector3 p1 = new Vector3(start.x, bottomY, start.z);
        Vector3 p2 = new Vector3(start.x, topY, start.z);

        int n = Physics.CapsuleCastNonAlloc(p1, p2, bodyRadius, dir, hits, dist + skin, collisionLayers, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        for (int i = 0; i < n; i++)
        {
            var h = hits[i];
            if (h.distance <= 0f) continue;                       // 이미 겹쳐 있는 것은 빠져나갈 수 있게 무시
            if (h.collider.transform.IsChildOf(transform)) continue; // 자기 자신(XR Origin)
            if (h.collider.GetComponentInParent<XRBaseInteractable>() != null) continue; // 잡을 수 있는 물체(문 손잡이 등)
            if (h.distance < best) { best = h.distance; nearest = h; }
        }
        if (best == float.MaxValue) return dir * dist;
        return dir * Mathf.Clamp(best - skin, 0f, dist);
    }
}
