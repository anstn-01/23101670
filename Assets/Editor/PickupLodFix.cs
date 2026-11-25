using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class PickupLodFix
{
    // 메뉴: Tools > City > Fix Pickup LOD
    // 픽업차량은 멀어지면 LOD1/LOD2(차 전체가 한 덩어리인 저해상도 모델)로 바뀌는데,
    // 문/타이어/운전대는 따로 떨어진 오브젝트라 계속 보이기 때문에 같은 자리에 두 겹으로 겹쳐 깨져 보이고,
    // 더 멀어지면 차가 통째로 사라집니다(Culled).
    // 직접 타고 움직이는 차이므로 LOD Group 을 없애고, LOD1/LOD2 오브젝트를 꺼서 항상 원래 모델(LOD0)로 보이게 합니다.
    [MenuItem("Tools/City/Fix Pickup LOD")]
    public static void Run()
    {
        var car = Object.FindObjectsByType<CarDrive>(FindObjectsInactive.Include, FindObjectsSortMode.None).Select(c => c.gameObject).FirstOrDefault();
        if (car == null)
            car = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Select(t => t.gameObject).FirstOrDefault(g => g.name == "픽업차량");
        if (car == null)
        {
            var door = Object.FindObjectsByType<CarDoorHinge>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(d => d.transform.parent != null && d.name.Contains("Pick Up"));
            if (door != null) car = door.transform.parent.gameObject;
        }
        if (car == null) { Debug.LogError("픽업차량을 찾지 못했습니다."); return; }

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Fix Pickup LOD");

        int removed = 0, off = 0;
        foreach (var lg in car.GetComponentsInChildren<LODGroup>(true))
        {
            Undo.DestroyObjectImmediate(lg);
            removed++;
        }
        foreach (var t in car.GetComponentsInChildren<Transform>(true))
        {
            string n = t.name.ToLowerInvariant().Replace(" ", "");
            if ((n.Contains("lod_1") || n.Contains("lod_2") || n.Contains("lod1") || n.Contains("lod2")) && t.gameObject.activeSelf)
            {
                Undo.RecordObject(t.gameObject, "Disable LOD");
                t.gameObject.SetActive(false);
                off++;
            }
        }

        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(car.scene);
        Debug.Log($"픽업차량 LOD 정리 완료: LOD Group {removed}개 제거, 저해상도 모델 {off}개 끔. 이제 거리와 상관없이 원래 모델로 보입니다. (Ctrl+Z 로 되돌리기)");
    }
}
