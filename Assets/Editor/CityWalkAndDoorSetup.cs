using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public static class CityWalkAndDoorSetup
{
    // 콜라이더를 붙이지 않을 장식 소품 (걸어서 통과 가능하게 둠)
    static readonly string[] SkipNames = { "tree", "hedge", "lamp", "bench" };

    // ------------------------------------------------------------------
    // 1) 도시 전 구역 걷기: 콜라이더 없는 도시 메쉬에 MeshCollider 추가
    // ------------------------------------------------------------------
    [MenuItem("Tools/City/Make City Walkable (Add Mesh Colliders)")]
    public static void MakeWalkable()
    {
        var sb = new StringBuilder();
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Make City Walkable");

        int added = 0, skipped = 0;
        var addedNames = new Dictionary<string, int>();
        var skippedNames = new Dictionary<string, int>();

        foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            Transform t = r.transform;
            if (IsUnder(t, "XR Origin")) continue;
            if (t.root.name.Contains("픽업") || t.root.name.Contains("Pick Up")) continue;
            if (r.GetComponent<Collider>() != null) continue;
            var mf = r.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) continue;

            string key = Key(r.name);
            string lower = r.name.ToLowerInvariant();
            if (SkipNames.Any(s => lower.Contains(s)))
            {
                skipped++;
                skippedNames[key] = skippedNames.TryGetValue(key, out int c) ? c + 1 : 1;
                continue;
            }

            var mc = Undo.AddComponent<MeshCollider>(r.gameObject);
            mc.sharedMesh = mf.sharedMesh;
            added++;
            addedNames[key] = addedNames.TryGetValue(key, out int c2) ? c2 + 1 : 1;
        }

        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        sb.AppendLine($"MeshCollider 추가: {added}, 장식이라 건너뜀: {skipped}");
        foreach (var kv in addedNames.OrderByDescending(k => k.Value)) sb.AppendLine($"  + {kv.Value} x {kv.Key}");
        foreach (var kv in skippedNames.OrderByDescending(k => k.Value)) sb.AppendLine($"  - {kv.Value} x {kv.Key}");
        File.WriteAllText(Application.dataPath + "/Editor/walkable_log.txt", sb.ToString());
        Debug.Log($"도시 걷기 설정 완료: MeshCollider {added}개 추가 (나무/울타리 덤불/가로등/벤치 {skipped}개는 통과 가능하게 둠). Ctrl+Z 로 되돌리기 가능.");
    }

    // ------------------------------------------------------------------
    // 2) 픽업 왼쪽 문 손잡이로 열고 닫기
    // ------------------------------------------------------------------
    [MenuItem("Tools/City/Setup Pickup Left Door")]
    public static void SetupDoor()
    {
        var sb = new StringBuilder();
        GameObject door = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Select(t => t.gameObject).FirstOrDefault(g => g.name.EndsWith("L Door"));
        if (door == null) { Debug.LogError("'L Door' 오브젝트를 찾지 못했습니다."); return; }
        Transform car = door.transform.parent;
        var mf = door.GetComponent<MeshFilter>();
        if (car == null || mf == null || mf.sharedMesh == null) { Debug.LogError("문 메쉬/부모를 찾지 못했습니다."); return; }

        Transform fl = FindChild(car, "FL Tire"), bl = FindChild(car, "BL Tire"), fr = FindChild(car, "FR Tire");
        if (fl == null || bl == null) { Debug.LogError("FL/BL Tire 를 찾지 못했습니다."); return; }

        Vector3 up = Vector3.up;
        Vector3 forward = Vector3.ProjectOnPlane(fl.position - bl.position, up).normalized;
        Vector3 right = Vector3.Cross(up, forward).normalized;
        Vector3 carCenter = fr != null ? (fl.position + fr.position) * 0.5f : car.position;

        // 문 메쉬 꼭짓점 8개를 월드로 → forward/right/up 축으로 투영해 범위 계산
        Bounds mb = mf.sharedMesh.bounds;
        var corners = new List<Vector3>();
        for (int i = 0; i < 8; i++)
        {
            Vector3 c = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
            corners.Add(door.transform.TransformPoint(c));
        }
        Vector3 doorCenter = door.transform.TransformPoint(mb.center);
        float sideSign = Mathf.Sign(Vector3.Dot(doorCenter - carCenter, right)); // 문이 있는 쪽
        Vector3 outward = right * sideSign;

        float fMin = corners.Min(c => Vector3.Dot(c, forward)), fMax = corners.Max(c => Vector3.Dot(c, forward));
        float oMin = corners.Min(c => Vector3.Dot(c, outward)), oMax = corners.Max(c => Vector3.Dot(c, outward));
        float uMin = corners.Min(c => Vector3.Dot(c, up)), uMax = corners.Max(c => Vector3.Dot(c, up));

        // 경첩: 문 앞쪽 끝, 바깥면 근처
        float oHinge = oMax - Mathf.Min(0.03f, (oMax - oMin) * 0.5f);
        Vector3 hingeW = forward * fMax + outward * oHinge + up * ((uMin + uMax) * 0.5f);
        // 손잡이: 문 뒤쪽 끝에서 약간 앞, 높이 55%
        float fHandle = fMin + (fMax - fMin) * 0.12f;
        float uHandle = uMin + (uMax - uMin) * 0.55f;

        // 차 전체 BoxCollider 가 있으면, 손잡이 콜라이더가 그 바깥까지 나오게 해서 레이가 막히지 않게 함
        float oOuter = oMax + 0.06f;
        foreach (var bc in car.GetComponentsInParent<BoxCollider>(true).Concat(car.GetComponents<BoxCollider>()))
        {
            var bcs = new List<Vector3>();
            for (int i = 0; i < 8; i++)
            {
                Vector3 c = bc.center + Vector3.Scale(bc.size * 0.5f, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                bcs.Add(bc.transform.TransformPoint(c));
            }
            oOuter = Mathf.Max(oOuter, bcs.Max(c => Vector3.Dot(c, outward)) + 0.04f);
        }
        float oInner = oMax - 0.04f;
        float thick = oOuter - oInner;
        Vector3 handleW = forward * fHandle + outward * ((oInner + oOuter) * 0.5f) + up * uHandle;

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Setup Pickup Left Door");

        // 기존 손잡이/스크립트 정리
        var old = door.transform.Find("Door Handle");
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
        var hinge = door.GetComponent<CarDoorHinge>();
        if (hinge == null) hinge = Undo.AddComponent<CarDoorHinge>(door);

        // 손잡이 오브젝트
        var handleGO = new GameObject("Door Handle");
        Undo.RegisterCreatedObjectUndo(handleGO, "Create Door Handle");
        Undo.SetTransformParent(handleGO.transform, door.transform, "Parent Door Handle");
        handleGO.transform.position = handleW;
        handleGO.transform.rotation = Quaternion.LookRotation(forward, up);
        handleGO.layer = door.layer;
        var col = handleGO.AddComponent<BoxCollider>();
        Vector3 ls = handleGO.transform.lossyScale;
        Vector3 wantSize = new Vector3(thick, 0.14f, 0.30f); // (바깥방향 두께, 높이, 길이)
        // LookRotation(forward) 의 x축 = right. 바깥방향이 -right 이면 x 그대로 두께로 사용 (박스는 대칭)
        col.size = new Vector3(wantSize.x / Mathf.Abs(ls.x), wantSize.y / Mathf.Abs(ls.y), wantSize.z / Mathf.Abs(ls.z));
        var interactable = handleGO.AddComponent<XRSimpleInteractable>();

        // 경첩 스크립트 값 설정 (문 부모 기준 로컬)
        Undo.RecordObject(hinge, "Configure Door Hinge");
        hinge.handle = interactable;
        hinge.hingeLocal = car.InverseTransformPoint(hingeW);
        hinge.axisLocal = car.InverseTransformDirection(up).normalized;
        Vector3 closedDirW = Vector3.ProjectOnPlane(handleW - hingeW, up);
        hinge.closedDirLocal = car.InverseTransformDirection(closedDirW).normalized;
        Vector3 test = Quaternion.AngleAxis(10f, up) * closedDirW;
        hinge.openSign = Vector3.Dot(test - closedDirW, outward) > 0 ? 1f : -1f;
        hinge.angle = 0f;
        EditorUtility.SetDirty(hinge);

        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(door.scene);
        Selection.activeGameObject = handleGO;

        sb.AppendLine($"door={door.name} car={car.name} carScale={car.lossyScale} doorScale={door.transform.lossyScale}");
        sb.AppendLine($"forward={forward} outward={outward} side={sideSign}");
        sb.AppendLine($"door f[{fMin:F3},{fMax:F3}] o[{oMin:F3},{oMax:F3}] u[{uMin:F3},{uMax:F3}]");
        sb.AppendLine($"hingeW={hingeW} handleW={handleW} thick={thick:F3} openSign={hinge.openSign}");
        sb.AppendLine($"hingeLocal={hinge.hingeLocal} axisLocal={hinge.axisLocal} closedDirLocal={hinge.closedDirLocal}");
        File.WriteAllText(Application.dataPath + "/Editor/door_log.txt", sb.ToString());
        Debug.Log("픽업 왼쪽 문 설정 완료: 손잡이(Door Handle)를 잡고 당기면 열리고, 밀면 닫힙니다.");
    }

    static bool IsUnder(Transform t, string name)
    {
        for (; t != null; t = t.parent) if (t.name.StartsWith(name)) return true;
        return false;
    }

    static Transform FindChild(Transform root, string contains)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name.Contains(contains)) return t;
        return null;
    }

    static string Key(string n)
    {
        n = n.Split('(')[0].Trim();
        int i = n.Length;
        while (i > 0 && (char.IsDigit(n[i - 1]) || n[i - 1] == '_' || n[i - 1] == ' ')) i--;
        return i > 0 ? n.Substring(0, i) : n;
    }
}
