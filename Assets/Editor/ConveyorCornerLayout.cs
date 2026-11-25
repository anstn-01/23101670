// Tools > Arrange Corner Layout (L자 배치)
// 컨베이어는 오른쪽 벽을 따라 안쪽 모서리로 흐르고,
// 테이블은 모서리(뒷벽을 따라)에, 사이드 벨트는 테이블 왼쪽 뒷벽을 따라 놓습니다.
using UnityEngine;
using UnityEditor;

public static class ConveyorCornerLayout
{
    const float Clr = 0.15f;              // 벽과의 간격
    const float L = 2.4f, W = 0.8f, Gap = 0.04f;
    const float CabHalf = 0.5f;           // 컨베이어 본체 반폭(뒷면이 벽쪽)
    const float SideLen = 2.3f;

    [MenuItem("Tools/Arrange Corner Layout (L자 배치)")]
    static void Arrange()
    {
        var conv = GameObject.Find("Conveyor_BD01");
        if (conv == null) { Debug.LogWarning("[Layout] Conveyor_BD01이 없어요."); return; }
        var ct = conv.transform;
        var table = ct.Find("OutputTable");
        var side = ct.Find("SideBelt");
        var machine = GameObject.Find("Machine");

        // 뒷벽(F)·오른쪽 벽(R) 방향과 모서리 추정
        bool arranged = table != null && Quaternion.Angle(table.localRotation, Quaternion.identity) > 1f;
        Vector3 F, R, cornerGuess;
        if (!arranged)
        {
            F = ct.forward; R = ct.right;                         // 기존: 뒷벽을 따라 놓인 상태
            cornerGuess = ct.position + R * (3.2f + Clr) + F * (CabHalf + Clr);
        }
        else
        {
            F = -ct.right; R = ct.forward;                        // 이미 L자 배치된 상태
            cornerGuess = ct.position + F * (W + Gap + Clr) + R * (CabHalf + Clr);
        }
        F = Vector3.ProjectOnPlane(F, Vector3.up).normalized;
        R = Vector3.ProjectOnPlane(R, Vector3.up).normalized;
        float y = ct.position.y;

        // 실제 벽까지 레이캐스트로 모서리 보정
        Vector3 probe = cornerGuess - F * 1.5f - R * 1.5f; probe.y = y + 0.6f;
        float dF = Hit(probe, F, conv, machine) ?? 1.5f + 0f;
        float dR = Hit(probe, R, conv, machine) ?? 1.5f + 0f;
        Vector3 corner = probe + F * dF + R * dR; corner.y = y;

        Undo.RegisterFullObjectHierarchyUndo(conv, "Arrange Corner Layout");
        if (machine) Undo.RecordObject(machine.transform, "Arrange Corner Layout");

        // 1) 컨베이어: 오른쪽 벽을 따라, 흐름(-X)이 뒷벽(F) 쪽
        ct.rotation = Quaternion.LookRotation(R, Vector3.up);       // +Z = 오른쪽 벽, -X = 뒷벽
        ct.position = corner - F * (Clr + W + Gap) - R * (Clr + CabHalf);

        // 2) 테이블: 모서리에, 뒷벽을 따라 길게
        Quaternion tRot = Quaternion.LookRotation(F, Vector3.up);   // +X = 오른쪽 벽, -Z = 방 안쪽(앞)
        if (table != null)
        {
            table.rotation = tRot;
            table.position = corner - R * (Clr + L * 0.5f) - F * (Clr + W * 0.5f);

            // 컨베이어가 앞쪽에서 들어오므로 앞 가드 제거, 끝 가드는 모서리(벽) 쪽으로
            var gf = table.Find("Guard_Front");
            if (gf) Undo.DestroyObjectImmediate(gf.gameObject);
            var ge = table.Find("Guard_End");
            if (ge) ge.localPosition = new Vector3(L * 0.5f - 0.01f, ge.localPosition.y, 0);
        }

        // 3) 사이드 벨트: 테이블 왼쪽, 뒷벽을 따라
        if (side != null && table != null)
        {
            side.rotation = tRot;
            side.position = table.TransformPoint(new Vector3(-L * 0.5f - Gap - SideLen, 0, 0));
        }

        // 4) 테이프 기계: 테이블 위, 컨베이어 투입구를 피해 약간 왼쪽
        if (machine != null && table != null)
        {
            float sc = machine.transform.localScale.x;
            machine.transform.rotation = tRot;
            machine.transform.position = table.TransformPoint(new Vector3(-0.45f, 0.755f, 0)) + tRot * new Vector3(0.04f * sc, 0, 0);
        }

        Selection.activeGameObject = conv;
        Debug.Log($"[Layout] L자 배치 완료 (뒷벽 {dF:F2}m, 오른쪽 벽 {dR:F2}m)");
    }

    static float? Hit(Vector3 o, Vector3 dir, GameObject skipA, GameObject skipB)
    {
        float min = float.MaxValue; bool ok = false;
        foreach (var h in Physics.RaycastAll(new Ray(o, dir), 60f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (h.transform.IsChildOf(skipA.transform)) continue;
            if (skipB && h.transform.IsChildOf(skipB.transform)) continue;
            if (h.distance < min) { min = h.distance; ok = true; }
        }
        return ok ? min : (float?)null;
    }
}
