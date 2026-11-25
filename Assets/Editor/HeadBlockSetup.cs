using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class HeadBlockSetup
{
    // 남아있는 콜라이더 없는 도시 메쉬(나무/덤불/벤치/가로등 등)에도 메쉬 모양 그대로 MeshCollider 추가
    // + XR Origin 에 HeadCollisionBlocker 추가
    [MenuItem("Tools/City/Block Walking Through Meshes")]
    public static void Run()
    {
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Block Walking Through Meshes");

        int added = 0;
        foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            Transform t = r.transform;
            if (IsUnder(t, "XR Origin")) continue;
            if (t.root.name.Contains("픽업") || t.root.name.Contains("Pick Up")) continue;
            if (r.GetComponent<Collider>() != null) continue;
            var mf = r.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) continue;
            var mc = Undo.AddComponent<MeshCollider>(r.gameObject);
            mc.sharedMesh = mf.sharedMesh;
            added++;
        }

        var xr = GameObject.Find("XR Origin (XR Rig)");
        string xrMsg = "XR Origin 을 찾지 못함";
        if (xr != null)
        {
            if (xr.GetComponent<HeadCollisionBlocker>() == null) Undo.AddComponent<HeadCollisionBlocker>(xr);
            xrMsg = "XR Origin 에 HeadCollisionBlocker 추가";
        }

        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log($"통과 방지 설정 완료: MeshCollider {added}개 추가, {xrMsg}. Ctrl+Z 로 되돌리기 가능.");
    }

    static bool IsUnder(Transform t, string name)
    {
        for (; t != null; t = t.parent) if (t.name.StartsWith(name)) return true;
        return false;
    }
}
