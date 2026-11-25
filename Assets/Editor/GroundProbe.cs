// 임시 도구: Plane 을 도시 바닥(도로)과 같은 높이로 맞추고, 도시 바닥과 겹치는 Plane 가장자리를 잘라 맞닿게 함.
// 공장과 공장 안 물건(컨베이어/버튼/슈트/플레이어 시작 위치)도 같은 높이만큼 함께 이동.
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class GroundProbe
{
    [MenuItem("Tools/City/Align Plane To City")]
    static void Align()
    {
        var plane = GameObject.Find("Plane");
        var factory = GameObject.Find("Parking Factory");
        if (plane == null || factory == null) { Debug.LogWarning("[Align] Plane 또는 Parking Factory 없음"); return; }
        var pt = plane.transform;
        var b = plane.GetComponent<MeshFilter>().sharedMesh.bounds;

        // 1) Plane 4변마다: 변의 가운데 60% 구간에서 도시 바닥과 겹치는 깊이(m) 측정.
        //    변의 절반 이상이 도시 바닥과 겹칠 때만 그 변을 겹친 변으로 봄 (모서리만 살짝 걸친 변은 제외)
        var sides = new[] { Vector3.back, Vector3.forward, Vector3.left, Vector3.right };
        var depth = new float[4];
        var heights = new List<float>();
        float sx = Mathf.Abs(pt.lossyScale.x), sz = Mathf.Abs(pt.lossyScale.z);
        var report = new System.Text.StringBuilder();
        for (int s = 0; s < 4; s++)
        {
            Vector3 n = sides[s];
            Vector3 t = new Vector3(Mathf.Abs(n.z), 0, Mathf.Abs(n.x));
            float half = Vector3.Dot(b.extents, new Vector3(Mathf.Abs(n.x), 0, Mathf.Abs(n.z)));
            float alongHalf = Vector3.Dot(b.extents, t);
            float unit = Mathf.Abs(n.x) > 0 ? sx : sz;
            int samples = 0, overlapped = 0; float maxD = 0f; var hs = new List<float>();
            for (int i = 0; i <= 20; i++)
            {
                float u = Mathf.Lerp(-alongHalf, alongHalf, i / 20f) * 0.6f;   // 가운데 60%
                samples++;
                float dHere = 0f;
                for (float dm = 0.05f; dm < 15f; dm += 0.1f)
                {
                    Vector3 w = pt.TransformPoint(b.center + n * (half - dm / unit) + t * u);
                    if (!CityHeight(w, out float h)) break;
                    dHere = dm; hs.Add(h);
                }
                if (dHere > 0f) { overlapped++; maxD = Mathf.Max(maxD, dHere); }
            }
            bool isOverlap = overlapped >= samples / 2;
            if (isOverlap) { depth[s] = maxD; heights.AddRange(hs); }
            report.Append($"{n}: {overlapped}/{samples} 겹침, 깊이 {maxD:F2}m{(isOverlap ? " (자름)" : "")}; ");
        }
        Debug.Log($"[Align] {report}  겹친 바닥 높이 min={(heights.Count > 0 ? heights.Min() : 0):F3} max={(heights.Count > 0 ? heights.Max() : 0):F3}");
        if (heights.Count == 0) { Debug.LogWarning("[Align] Plane 과 겹치는 도시 바닥이 없습니다."); return; }

        float road = heights.Min();                 // 도로 높이 (가장 낮은 면)
        float dy = road - pt.position.y;

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Align Plane To City");

        // 2) 겹치는 변을 안쪽으로 잘라냄 (반대쪽 변은 그대로), 높이는 도로에 맞춤
        Undo.RecordObject(pt, "Align Plane");
        Vector3 scale = pt.localScale;
        Vector3 shiftLocal = Vector3.zero;
        for (int s = 0; s < 4; s++)
        {
            if (depth[s] <= 0f) continue;
            float cut = depth[s] + 0.05f;
            Vector3 n = sides[s];
            bool alongX = Mathf.Abs(n.x) > 0;
            float full = alongX ? b.size.x * sx : b.size.z * sz;
            float newFull = Mathf.Max(1f, full - cut);
            if (alongX) scale.x *= newFull / full; else scale.z *= newFull / full;
            shiftLocal += -n * (cut * 0.5f);
        }
        Vector3 shiftWorld = pt.rotation * shiftLocal;
        pt.localScale = scale;
        pt.position = new Vector3(pt.position.x + shiftWorld.x, road, pt.position.z + shiftWorld.z);

        // 3) 공장 + 공장 안 물건을 같은 높이만큼 이동 (공장 바닥이 Plane 위에 놓이도록)
        float fdy = road - factory.transform.position.y;
        foreach (var name in new[] { "Parking Factory", "Conveyor_BD01", "RedWallButton", "BlueWallButton", "Chute_VD01", "XR Origin (XR Rig)" })
        {
            var go = GameObject.Find(name);
            if (go == null) continue;
            Undo.RecordObject(go.transform, "Align " + name);
            go.transform.position += Vector3.up * fdy;
        }

        Debug.Log($"[Align] 완료: 도로 높이 {road:F3}, Plane y {pt.position.y:F3} (이동 {dy:+0.000;-0.000}), Plane scale {pt.localScale}, 중심 이동 {shiftWorld:F2}, 공장/물건 높이 이동 {fdy:+0.000;-0.000}");
    }

    // Plane / 공장 / 차를 제외한 도시 바닥 높이 (Plane 높이 ±3m 이내, 위를 보는 면)
    static bool CityHeight(Vector3 w, out float h)
    {
        h = 0;
        var hits = Physics.RaycastAll(new Vector3(w.x, w.y + 60f, w.z), Vector3.down, 200f, ~0, QueryTriggerInteraction.Ignore)
            .Where(x => x.collider.name != "Plane"
                        && !x.collider.transform.root.name.Contains("Parking Factory")
                        && !x.collider.transform.root.name.Contains("픽업")
                        && Mathf.Abs(x.point.y - w.y) < 3f)
            .OrderBy(x => x.distance).ToArray();
        foreach (var x in hits) if (x.normal.y > 0.7f) { h = x.point.y; return true; }
        return false;
    }
}
