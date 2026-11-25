using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

// 메뉴: Tools > Scene Diagnostics (진단 정보를 Assets/Editor/scene_diag.txt 에 기록)
public static class SceneDiag
{
    [MenuItem("Tools/Scene Diagnostics")]
    public static void Run()
    {
        var sb = new StringBuilder();
        var renderers = Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        int withCol = 0, noCol = 0;
        var noColNames = new System.Collections.Generic.Dictionary<string, int>();
        foreach (var r in renderers)
        {
            if (r.GetComponent<Collider>() != null) withCol++;
            else
            {
                noCol++;
                string key = Root(r.transform) + " / " + r.name.Split(' ')[0].Split('(')[0];
                noColNames[key] = noColNames.TryGetValue(key, out int c) ? c + 1 : 1;
            }
        }
        sb.AppendLine($"MeshRenderers: {renderers.Length}, with collider: {withCol}, without: {noCol}");
        foreach (var kv in noColNames.OrderByDescending(k => k.Value).Take(60)) sb.AppendLine($"  noCol {kv.Value} x {kv.Key}");

        sb.AppendLine("--- Colliders by type");
        foreach (var g in Object.FindObjectsByType<Collider>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).GroupBy(c => c.GetType().Name))
            sb.AppendLine($"  {g.Key}: {g.Count()}");
        sb.AppendLine("--- Mesh colliders sample");
        foreach (var mc in Object.FindObjectsByType<MeshCollider>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Take(40))
            sb.AppendLine($"  {Path(mc.transform)} mesh={(mc.sharedMesh ? mc.sharedMesh.name : "null")} b={mc.bounds.center} {mc.bounds.size}");
        sb.AppendLine("--- Box colliders");
        foreach (var bc in Object.FindObjectsByType<BoxCollider>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Take(40))
            sb.AppendLine($"  {Path(bc.transform)} enabled={bc.enabled} trig={bc.isTrigger} b={bc.bounds.center} {bc.bounds.size}");

        sb.AppendLine("--- CharacterControllers");
        foreach (var cc in Object.FindObjectsByType<CharacterController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            sb.AppendLine($"  {Path(cc.transform)} h={cc.height} r={cc.radius} step={cc.stepOffset} slope={cc.slopeLimit} center={cc.center} pos={cc.transform.position}");

        sb.AppendLine("--- MonoBehaviours under XR Origin");
        var xr = GameObject.Find("XR Origin (XR Rig)");
        if (xr != null)
            foreach (var mb in xr.GetComponentsInChildren<MonoBehaviour>(true))
                sb.AppendLine($"  {Path(mb.transform)} : {mb.GetType().FullName} enabled={mb.enabled} active={mb.gameObject.activeInHierarchy}");

        sb.AppendLine("--- Pick Up_9");
        var car = GameObject.Find("Pick Up_9");
        if (car != null)
        {
            sb.AppendLine($"  car pos={car.transform.position} rot={car.transform.eulerAngles} scale={car.transform.lossyScale}");
            foreach (var t in car.GetComponentsInChildren<Transform>(true))
            {
                var rr = t.GetComponent<Renderer>();
                string comps = string.Join(",", t.GetComponents<Component>().Select(c => c.GetType().Name));
                sb.AppendLine($"  {Path(t)} lpos={t.localPosition} lrot={t.localEulerAngles} [{comps}] " + (rr ? $"bounds={rr.bounds.center} {rr.bounds.size}" : ""));
            }
            foreach (var mf in car.GetComponentsInChildren<MeshFilter>(true))
                if (mf.name.Contains("L Door") && mf.sharedMesh != null)
                    sb.AppendLine($"  L Door mesh local bounds={mf.sharedMesh.bounds.center} {mf.sharedMesh.bounds.size}");
        }

        var cube = GameObject.Find("Cube");
        sb.AppendLine("Cube: " + (cube ? $"{cube.transform.position} active={cube.activeInHierarchy} col={(cube.GetComponent<Collider>() ? cube.GetComponent<Collider>().enabled.ToString() : "none")}" : "none"));
        sb.AppendLine("Physics gravity " + Physics.gravity);

        File.WriteAllText(Application.dataPath + "/Editor/scene_diag.txt", sb.ToString());
        Debug.Log("Scene diagnostics written.");
    }

    static string Root(Transform t) { while (t.parent != null && t.parent.parent != null) t = t.parent; return t.name; }
    static string Path(Transform t) { string p = t.name; while (t.parent != null) { t = t.parent; p = t.name + "/" + p; } return p; }
}
