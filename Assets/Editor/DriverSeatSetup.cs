using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public static class DriverSeatSetup
{
    // 메뉴: Tools > City > Setup Driver Seat
    // 운전대 위치를 기준으로 운전석(눈 위치/좌석 콜라이더/하차 위치)을 만들고,
    // 열린 문 사이로 좌석을 선택할 수 있게 차 콜라이더를 차체 모양(MeshCollider)으로 바꿉니다.
    [MenuItem("Tools/City/Setup Driver Seat")]
    public static void Run()
    {
        var sb = new StringBuilder();
        var doorHinge = Object.FindObjectsByType<CarDoorHinge>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
        if (doorHinge == null) { Debug.LogError("CarDoorHinge(왼쪽 문)를 찾지 못했습니다. 먼저 Setup Pickup Left Door 를 실행하세요."); return; }
        Transform door = doorHinge.transform;
        Transform car = door.parent;

        Transform fl = FindChild(car, "FL Tire"), bl = FindChild(car, "BL Tire"), fr = FindChild(car, "FR Tire");
        Transform wheel = FindChild(car, "Stearing") ?? FindChild(car, "Steering");
        if (fl == null || bl == null || wheel == null) { Debug.LogError("타이어/운전대(Stearing)를 찾지 못했습니다."); return; }

        Vector3 up = Vector3.up;
        Vector3 forward = Vector3.ProjectOnPlane(fl.position - bl.position, up).normalized;
        Vector3 right = Vector3.Cross(up, forward).normalized;
        Vector3 carCenter = fr != null ? (fl.position + fr.position) * 0.5f : car.position;

        Vector3 wheelC = Center(wheel);
        Vector3 doorC = Center(door);
        Vector3 outward = right * Mathf.Sign(Vector3.Dot(doorC - carCenter, right));

        // 눈 위치: 운전대 중심에서 뒤로 0.5m, 위로 0.35m
        Vector3 eyeW = wheelC - forward * 0.5f + up * 0.35f;
        // 좌석 쿠션: 눈에서 아래로 0.6m, 조금 뒤
        Vector3 seatW = eyeW - up * 0.6f - forward * 0.05f;

        // 하차 위치: 문 중심에서 바깥으로 1.1m, 바닥 높이
        Vector3 exitW = doorC + outward * 1.1f;
        float groundY = BottomY(fl);
        var rayHits = Physics.RaycastAll(exitW + up * 3f, Vector3.down, 10f, ~0, QueryTriggerInteraction.Ignore)
            .Where(h => !h.collider.transform.IsChildOf(car)).OrderBy(h => h.distance).ToArray();
        if (rayHits.Length > 0) groundY = rayHits[0].point.y;
        exitW.y = groundY;

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Setup Driver Seat");

        // 기존 것 정리
        var old = car.Find("Driver Seat");
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

        var seatGO = new GameObject("Driver Seat");
        Undo.RegisterCreatedObjectUndo(seatGO, "Create Driver Seat");
        Undo.SetTransformParent(seatGO.transform, car, "Parent Driver Seat");
        seatGO.transform.SetPositionAndRotation(seatW, Quaternion.LookRotation(forward, up));
        var col = seatGO.AddComponent<BoxCollider>();
        Vector3 ls = seatGO.transform.lossyScale;
        col.size = new Vector3(0.55f / Mathf.Abs(ls.x), 0.35f / Mathf.Abs(ls.y), 0.55f / Mathf.Abs(ls.z));
        seatGO.AddComponent<XRSimpleInteractable>();
        var seat = seatGO.AddComponent<CarSeat>();

        var eye = new GameObject("Eye Point").transform;
        eye.SetParent(seatGO.transform, false);
        eye.SetPositionAndRotation(eyeW, Quaternion.LookRotation(forward, up));
        var exit = new GameObject("Exit Point").transform;
        exit.SetParent(seatGO.transform, false);
        exit.SetPositionAndRotation(exitW, Quaternion.LookRotation(forward, up));

        seat.door = doorHinge;
        seat.eyePoint = eye;
        seat.exitPoint = exit;

        // 차 콜라이더: 차 전체를 감싼 BoxCollider 는 끄고, 차체/문을 메쉬 모양 MeshCollider 로
        int boxOff = 0;
        foreach (var bc in car.GetComponents<BoxCollider>())
            if (bc.enabled) { Undo.RecordObject(bc, "Disable car box"); bc.enabled = false; boxOff++; }
        var bodyMF = car.GetComponent<MeshFilter>();
        if (bodyMF != null && bodyMF.sharedMesh != null && car.GetComponent<MeshCollider>() == null)
            Undo.AddComponent<MeshCollider>(car.gameObject).sharedMesh = bodyMF.sharedMesh;
        var doorMF = door.GetComponent<MeshFilter>();
        if (doorMF != null && doorMF.sharedMesh != null && door.GetComponent<MeshCollider>() == null)
            Undo.AddComponent<MeshCollider>(door.gameObject).sharedMesh = doorMF.sharedMesh;

        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(car.gameObject.scene);
        Selection.activeGameObject = seatGO;

        sb.AppendLine($"car={car.name} forward={forward} outward={outward}");
        sb.AppendLine($"wheelC={wheelC} eye={eyeW} seat={seatW} exit={exitW} doorC={doorC}");
        sb.AppendLine($"bodyMesh={(bodyMF && bodyMF.sharedMesh ? bodyMF.sharedMesh.name : "none")} boxOff={boxOff}");
        File.WriteAllText(Application.dataPath + "/Editor/seat_log.txt", sb.ToString());
        Debug.Log("운전석 설정 완료: 문을 연 뒤 좌석(Driver Seat)을 잡으면 탑승, 탑승 중 다시 잡으면 하차합니다.");
    }

    static Vector3 Center(Transform t)
    {
        var r = t.GetComponent<Renderer>() ?? t.GetComponentInChildren<Renderer>();
        return r != null ? r.bounds.center : t.position;
    }

    static float BottomY(Transform t)
    {
        var r = t.GetComponent<Renderer>() ?? t.GetComponentInChildren<Renderer>();
        return r != null ? r.bounds.min.y : t.position.y;
    }

    static Transform FindChild(Transform root, string contains)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name.Contains(contains)) return t;
        return null;
    }
}
