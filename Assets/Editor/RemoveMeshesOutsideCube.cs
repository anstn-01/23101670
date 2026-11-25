using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 메뉴: Tools > Remove Meshes Outside Cube
// 선택한 오브젝트(없으면 이름이 "Cube"인 오브젝트)의 범위를 기준으로,
// 범위와 전혀 겹치지 않는(완전히 바깥에 있는) 메쉬를 씬에서 삭제합니다.
// 범위에 조금이라도 걸친 메쉬는 남깁니다. Ctrl+Z 한 번으로 전부 되돌릴 수 있습니다.
public static class RemoveMeshesOutsideCube
{
    [MenuItem("Tools/Remove Meshes Outside Cube")]
    public static void Run()
    {
        GameObject box = Selection.activeGameObject;
        if (box == null || box.GetComponent<Renderer>() == null) box = GameObject.Find("Cube");
        if (box == null || box.GetComponent<Renderer>() == null)
        {
            Debug.LogError("기준 큐브를 찾지 못했습니다. 큐브를 선택하고 다시 실행하세요.");
            return;
        }
        Bounds area = box.GetComponent<Renderer>().bounds;

        var outside = new HashSet<Renderer>();
        foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!(r is MeshRenderer || r is SkinnedMeshRenderer)) continue;
            if (r.gameObject == box) continue;
            if (r.gameObject.scene != box.scene) continue;
            if (!area.Intersects(r.bounds)) outside.Add(r);
        }

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Remove Meshes Outside Cube");

        int removedObjects = 0, removedRenderers = 0;
        var parents = new HashSet<Transform>();

        foreach (var r in outside)
        {
            if (r == null) continue; // 이미 부모와 함께 삭제됨
            GameObject go = r.gameObject;
            if (go == box || box.transform.IsChildOf(go.transform)) continue;

            bool hasInsideChild = false;
            foreach (var cr in go.GetComponentsInChildren<Renderer>(true))
            {
                if (cr != r && !outside.Contains(cr)) { hasInsideChild = true; break; }
            }

            if (hasInsideChild)
            {
                // 자식 중 범위 안 메쉬가 있으면 이 오브젝트의 메쉬만 제거
                var mf = go.GetComponent<MeshFilter>();
                Undo.DestroyObjectImmediate(r);
                if (mf != null) Undo.DestroyObjectImmediate(mf);
                removedRenderers++;
            }
            else
            {
                if (go.transform.parent != null) parents.Add(go.transform.parent);
                Undo.DestroyObjectImmediate(go);
                removedObjects++;
            }
        }

        // 삭제로 인해 비어버린 부모(그룹/LOD 루트) 정리
        int removedEmpty = 0;
        var queue = new Queue<Transform>(parents);
        while (queue.Count > 0)
        {
            Transform t = queue.Dequeue();
            if (t == null || t.childCount > 0 || t.gameObject == box) continue;
            bool onlyTransform = true;
            foreach (var c in t.GetComponents<Component>())
            {
                if (!(c is Transform) && !(c is LODGroup)) { onlyTransform = false; break; }
            }
            if (!onlyTransform) continue;
            Transform p = t.parent;
            Undo.DestroyObjectImmediate(t.gameObject);
            removedEmpty++;
            if (p != null) queue.Enqueue(p);
        }

        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(box.scene);
        Debug.Log($"범위 밖 메쉬 정리 완료: 오브젝트 {removedObjects}개 삭제, 메쉬만 제거 {removedRenderers}개, 빈 그룹 {removedEmpty}개 삭제. (Ctrl+Z 로 되돌리기 가능)");
    }
}
