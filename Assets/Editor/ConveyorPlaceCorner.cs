// Tools > Place Conveyor In View Corner
// 씬 뷰에서 보고 있는 방향의 벽 모서리(정면 벽 + 오른쪽 벽)에 Conveyor_BD01을 배치합니다.
// 컨베이어는 정면 벽을 따라 놓이고, 본체가 오른쪽 모서리에 붙습니다.
using UnityEngine;
using UnityEditor;

public static class ConveyorPlaceCorner
{
    const float Clearance = 0.15f;   // 벽과의 간격(m)

    [MenuItem("Tools/Place Conveyor In View Corner")]
    static void Place()
    {
        var conv = GameObject.Find("Conveyor_BD01");
        var sv = SceneView.lastActiveSceneView;
        if (conv == null || sv == null) { Debug.LogWarning("[Place] Conveyor_BD01 또는 씬 뷰가 없어요."); return; }
        var cam = sv.camera;

        // 1) 화면 중앙 약간 아래 바닥 지점
        Vector3 p;
        if (!RayHit(cam.ViewportPointToRay(new Vector3(0.6f, 0.35f, 0)), 100f, conv, out var floorHit)) { Debug.LogWarning("[Place] 바닥을 못 찾았어요."); return; }
        p = floorHit.point;

        // 2) 벽 축 방향: 카메라 정면과 가장 가까운 팩토리(또는 월드) 축
        var factory = GameObject.Find("Parking Factory");
        Quaternion baseRot = factory ? Quaternion.Euler(0, factory.transform.eulerAngles.y, 0) : Quaternion.identity;
        Vector3 camF = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized;
        Quaternion best = baseRot; float bestDot = -2f;
        for (int k = 0; k < 4; k++)
        {
            var r = baseRot * Quaternion.Euler(0, 90f * k, 0);
            float d = Vector3.Dot(r * Vector3.forward, camF);
            if (d > bestDot) { bestDot = d; best = r; }
        }
        Vector3 fwd = best * Vector3.forward;   // 정면 벽 쪽
        Vector3 right = best * Vector3.right;   // 오른쪽 벽 쪽

        // 3) 벽까지 거리
        Vector3 probe = p + Vector3.up * 0.6f;
        float dF = RayHit(new Ray(probe, fwd), 60f, conv, out var hf) ? hf.distance : 3f;
        float dR = RayHit(new Ray(probe, right), 60f, conv, out var hr) ? hr.distance : 4f;
        Vector3 corner = p + fwd * dF + right * dR;

        // 4) 배치 (본체 오른쪽 끝 x=3.2, 뒷면 z=0.5)
        Undo.RecordObject(conv.transform, "Place Conveyor In Corner");
        conv.transform.rotation = best;
        conv.transform.position = corner - right * (3.2f + Clearance) - fwd * (0.5f + Clearance);
        conv.transform.position = new Vector3(conv.transform.position.x, p.y, conv.transform.position.z);
        Selection.activeGameObject = conv;
        Debug.Log($"[Place] 모서리 배치 완료 (정면 벽 {dF:F2}m, 오른쪽 벽 {dR:F2}m)");
    }

    static bool RayHit(Ray ray, float dist, GameObject skip, out RaycastHit best)
    {
        best = default; float min = float.MaxValue; bool ok = false;
        foreach (var h in Physics.RaycastAll(ray, dist, ~0, QueryTriggerInteraction.Ignore))
        {
            if (h.transform.IsChildOf(skip.transform)) continue;
            if (h.distance < min) { min = h.distance; best = h; ok = true; }
        }
        return ok;
    }
}
