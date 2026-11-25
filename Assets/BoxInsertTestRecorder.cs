// Tools > Record Box Insert Test
// 상자가 나와 멈추면 물건을 상자 위에서 떨어뜨려 상자 안으로 들어가는 장면 + 상자를 들어 기울여도 안 빠지는 장면 녹화
using System.Collections;
using System.IO;
using UnityEngine;

public class BoxInsertTestRecorder : MonoBehaviour
{
    public string outputDir;
    public int width = 960, height = 540, fps = 15;
    int frame;
    Camera cam; RenderTexture rt; Texture2D tex;

    IEnumerator Start()
    {
        var feed = FindFirstObjectByType<BoxConveyor>();
        var dropper = FindFirstObjectByType<ChuteDropper>();
        if (feed == null || dropper == null) { Debug.LogError("[Rec] 컨베이어/슈트 없음"); yield break; }
        Directory.CreateDirectory(outputDir);
        foreach (var f in Directory.GetFiles(outputDir, "*.jpg")) File.Delete(f);
        Time.captureFramerate = fps;
        feed.speed = 1.6f;   // 녹화용으로 빠르게

        var camGo = new GameObject("RecCam");
        cam = camGo.AddComponent<Camera>();
        cam.fieldOfView = 45f; cam.nearClipPlane = 0.02f;
        cam.stereoTargetEye = StereoTargetEyeMask.None; cam.depth = -100;
        rt = new RenderTexture(width, height, 24) { antiAliasing = 8 };
        cam.targetTexture = rt;
        tex = new Texture2D(width, height, TextureFormat.RGB24, false);

        var ct = feed.transform;
        Vector3 boxC = ct.TransformPoint(feed.stopLocal + Vector3.up * feed.boxSize.y * 0.5f);
        Vector3 room = -ct.forward;
        Vector3 camPos = boxC + ct.right * 0.7f + room * 0.35f + Vector3.up * 0.62f;
        camGo.transform.SetPositionAndRotation(camPos, Quaternion.LookRotation(boxC + Vector3.up * 0.1f - camPos, Vector3.up));

        var fill = new GameObject("RecFill").AddComponent<Light>();
        fill.type = LightType.Point; fill.range = 2.5f; fill.intensity = 1.5f;
        fill.transform.position = camPos + Vector3.up * 0.3f;

        var label = new GameObject("RecLabel").AddComponent<TextMesh>();
        label.characterSize = 0.006f; label.fontSize = 64; label.anchor = TextAnchor.MiddleCenter; label.color = Color.white;
        label.transform.position = boxC + Vector3.up * 0.42f;
        label.transform.rotation = Quaternion.LookRotation(label.transform.position - camPos, Vector3.up);

        int[] order = { 0, 3, 6, 9 };
        foreach (int idx in order)
        {
            label.text = ChuteDropper.Labels[idx];
            feed.SpawnBox();
            var box = feed.LastBox;
            for (float t = 0; t < 0.3f || (box != null && feed.IsMoving(box) && t < 6f); t += 1f / fps) yield return Grab();
            for (float t = 0; t < 0.3f; t += 1f / fps) yield return Grab();
            if (box == null) continue;

            // 상자 위 25cm에서 물건을 놓음 (손에서 놓은 것처럼)
            var item = dropper.MakeItem(idx);
            Vector3 p = box.position + Vector3.up * (feed.boxSize.y * 0.5f + 0.22f) + ct.forward * 0.03f;
            Quaternion r = Quaternion.Euler(Random.Range(-20f, 20f), Random.Range(0f, 360f), Random.Range(-20f, 20f));
            item.transform.SetPositionAndRotation(p, r);
            var irb = item.GetComponent<Rigidbody>(); irb.position = p; irb.rotation = r;
            for (float t = 0; t < 1.4f; t += 1f / fps) yield return Grab();

            // 상자를 들어 올려 기울이고 흔들기 → 물건이 안 빠짐
            var brb = box.GetComponent<Rigidbody>();
            Vector3 b0 = box.position; Quaternion q0 = box.rotation;
            for (float t = 0; t < 1.6f; t += 1f / fps)
            {
                float k = Mathf.SmoothStep(0, 1, Mathf.Clamp01(t / 0.5f));
                brb.isKinematic = true;
                box.position = b0 + Vector3.up * 0.2f * k;
                box.rotation = q0 * Quaternion.Euler(Mathf.Sin(t * 9f) * 35f * k, 0, Mathf.Cos(t * 7f) * 25f * k);
                yield return Grab();
            }
            Destroy(box.gameObject);
            yield return Grab();
        }
        Time.captureFramerate = 0;
        Debug.Log($"[Rec] 상자 넣기 테스트 녹화 완료: {frame} frames");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    IEnumerator Grab()
    {
        yield return new WaitForEndOfFrame();
        var prev = RenderTexture.active; RenderTexture.active = rt;
        tex.ReadPixels(new Rect(0, 0, width, height), 0, 0); tex.Apply(false);
        RenderTexture.active = prev;
        File.WriteAllBytes(Path.Combine(outputDir, $"f_{frame++:0000}.jpg"), tex.EncodeToJPG(85));
    }
}

#if UNITY_EDITOR
[UnityEditor.InitializeOnLoad]
static class BoxInsertTestRecorderMenu
{
    const string Key = "BoxInsertTestRecorder_Pending";
    const string OutDir = @"C:\Users\User\Desktop\바탕화면 정리\기말 프로젝트\Assets\~BoxCapture";

    static BoxInsertTestRecorderMenu()
    {
        UnityEditor.EditorApplication.playModeStateChanged += s =>
        {
            if (s != UnityEditor.PlayModeStateChange.EnteredPlayMode || !UnityEditor.EditorPrefs.GetBool(Key)) return;
            UnityEditor.EditorPrefs.SetBool(Key, false);
            new GameObject("BoxInsertTestRecorder").AddComponent<BoxInsertTestRecorder>().outputDir = OutDir;
        };
    }

    [UnityEditor.MenuItem("Tools/Record Box Insert Test")]
    static void Record()
    {
        UnityEditor.EditorPrefs.SetBool(Key, true);
        UnityEditor.EditorApplication.isPlaying = true;
    }
}
#endif
