// Tools > Record Chute Test
// 파란 버튼을 10번 눌러 슈트에서 01~10번 물품이 차례로 떨어지는 장면을 녹화
using System.Collections;
using System.IO;
using UnityEngine;

public class ChuteTestRecorder : MonoBehaviour
{
    public string outputDir;
    public int width = 960, height = 540, fps = 15;
    public float duration = 13.5f;
    public bool showcase;

    IEnumerator Start()
    {
        if (showcase) { yield return Showcase(); yield break; }
        var btn = FindFirstObjectByType<BluePushButton>();
        var chute = GameObject.Find("Chute_VD01");
        if (btn == null || chute == null) { Debug.LogError("[Rec] 파란 버튼/슈트 없음"); yield break; }

        Directory.CreateDirectory(outputDir);
        foreach (var f in Directory.GetFiles(outputDir, "*.jpg")) File.Delete(f);
        Time.captureFramerate = fps;

        var camGo = new GameObject("RecCam");
        var cam = camGo.AddComponent<Camera>();
        cam.fieldOfView = 50f; cam.nearClipPlane = 0.02f;
        cam.stereoTargetEye = StereoTargetEyeMask.None; cam.depth = -100;
        var rt = new RenderTexture(width, height, 24) { antiAliasing = 4 };
        cam.targetTexture = rt;
        var tex = new Texture2D(width, height, TextureFormat.RGB24, false);

        var finger = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(finger.GetComponent<Collider>());
        finger.transform.localScale = Vector3.one * 0.045f;
        var fm = finger.GetComponent<Renderer>().material;
        var skin = new Color(0.93f, 0.76f, 0.64f);
        if (fm.HasProperty("_BaseColor")) fm.SetColor("_BaseColor", skin); else fm.color = skin;

        var bt = btn.transform;
        Vector3 bPos = bt.TransformPoint(new Vector3(0, 0, 0.11f));
        Vector3 n = bt.forward;
        Vector3 chuteBottom = chute.transform.position;
        Vector3 look = Vector3.Lerp(bPos, chuteBottom, 0.5f) + Vector3.down * 0.35f;
        Vector3 camPos = look + n * 1.75f + Vector3.up * 0.55f + Vector3.Cross(Vector3.up, n).normalized * 0.4f;
        camGo.transform.SetPositionAndRotation(camPos, Quaternion.LookRotation(look - camPos, Vector3.up));

        var pressAt = new float[10];
        for (int i = 0; i < 10; i++) pressAt[i] = 0.8f + i * 1.2f;
        int next = 0, total = Mathf.RoundToInt(duration * fps);
        for (int f = 0; f < total; f++)
        {
            float t = f / (float)fps, best = 99f;
            foreach (var p in pressAt) best = Mathf.Min(best, Mathf.Abs(t - p));
            float reach = Mathf.SmoothStep(0, 1, Mathf.Clamp01(1f - best / 0.4f));
            finger.transform.position = bPos + n * Mathf.Lerp(0.3f, 0.01f, reach);
            finger.SetActive(reach > 0.02f);
            if (next < pressAt.Length && t >= pressAt[next]) { btn.Press(); next++; }

            yield return new WaitForEndOfFrame();
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply(false);
            RenderTexture.active = prev;
            File.WriteAllBytes(Path.Combine(outputDir, $"f_{f:0000}.jpg"), tex.EncodeToJPG(85));
        }
        Time.captureFramerate = 0;
        Debug.Log($"[Rec] 슈트 테스트 녹화 완료: {total} frames");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    // 물품 10개를 하나씩 돌려가며 클로즈업
    IEnumerator Showcase()
    {
        var btn = FindFirstObjectByType<BluePushButton>();
        var dropper = FindFirstObjectByType<ChuteDropper>();
        if (btn == null || dropper == null) { Debug.LogError("[Rec] 버튼/드로퍼 없음"); yield break; }
        Directory.CreateDirectory(outputDir);
        foreach (var f in Directory.GetFiles(outputDir, "*.jpg")) File.Delete(f);
        Time.captureFramerate = fps;

        var bt = btn.transform;
        Vector3 n = bt.forward; n.y = 0; n.Normalize();
        Vector3 spot = bt.position + n * 0.8f + Vector3.up * 0.05f;
        var camGo = new GameObject("RecCam");
        var cam = camGo.AddComponent<Camera>();
        cam.fieldOfView = 40f; cam.nearClipPlane = 0.01f;
        cam.stereoTargetEye = StereoTargetEyeMask.None; cam.depth = -100;
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.16f, 0.17f, 0.2f);
        var rt = new RenderTexture(width, height, 24) { antiAliasing = 8 };
        cam.targetTexture = rt;
        var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
        Vector3 camPos = spot + n * 0.42f + Vector3.up * 0.16f;
        camGo.transform.SetPositionAndRotation(camPos, Quaternion.LookRotation(spot - camPos, Vector3.up));

        var lg = new GameObject("FillLight"); var l = lg.AddComponent<Light>();
        l.type = LightType.Directional; l.intensity = 1.6f; l.shadows = LightShadows.Soft;
        lg.transform.rotation = Quaternion.LookRotation((spot - (camPos + Vector3.up * 0.6f + Vector3.Cross(Vector3.up, n) * 0.5f)).normalized);
        var lg2 = new GameObject("RimLight"); var l2 = lg2.AddComponent<Light>();
        l2.type = LightType.Point; l2.range = 2f; l2.intensity = 1.2f; lg2.transform.position = spot - n * 0.5f + Vector3.up * 0.4f;

        var label = new GameObject("Label").AddComponent<TextMesh>();
        label.characterSize = 0.006f; label.fontSize = 64; label.anchor = TextAnchor.MiddleCenter; label.color = Color.white;
        label.transform.position = spot + Vector3.up * 0.15f;
        label.transform.rotation = Quaternion.LookRotation(spot - camPos, Vector3.up);

        int per = Mathf.RoundToInt(1.6f * fps), fr = 0;
        for (int i = 0; i < 10; i++)
        {
            var item = dropper.MakeItem(i);
            var rb = item.GetComponent<Rigidbody>(); rb.isKinematic = true;
            label.text = ChuteDropper.Labels[i];
            for (int k = 0; k < per; k++, fr++)
            {
                item.transform.SetPositionAndRotation(spot, Quaternion.Euler(18f, k * 360f / per * 0.6f - 50f, 0));
                yield return new WaitForEndOfFrame();
                var prev = RenderTexture.active; RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0); tex.Apply(false);
                RenderTexture.active = prev;
                File.WriteAllBytes(Path.Combine(outputDir, $"f_{fr:0000}.jpg"), tex.EncodeToJPG(88));
            }
            Destroy(item);
        }
        Time.captureFramerate = 0;
        Debug.Log($"[Rec] 쇼케이스 녹화 완료: {fr} frames");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}

#if UNITY_EDITOR
[UnityEditor.InitializeOnLoad]
static class ChuteTestRecorderMenu
{
    const string Key = "ChuteTestRecorder_Pending";
    const string OutDir = @"C:\Users\User\Desktop\바탕화면 정리\기말 프로젝트\Assets\~ChuteCapture";

    static ChuteTestRecorderMenu()
    {
        UnityEditor.EditorApplication.playModeStateChanged += s =>
        {
            if (s != UnityEditor.PlayModeStateChange.EnteredPlayMode || !UnityEditor.EditorPrefs.GetBool(Key)) return;
            UnityEditor.EditorPrefs.SetBool(Key, false);
            var r = new GameObject("ChuteTestRecorder").AddComponent<ChuteTestRecorder>();
            r.outputDir = OutDir; r.showcase = UnityEditor.EditorPrefs.GetBool(Key + "_Show");
        };
    }

    [UnityEditor.MenuItem("Tools/Record Item Showcase")]
    static void RecordShow()
    {
        UnityEditor.EditorPrefs.SetBool(Key, true);
        UnityEditor.EditorPrefs.SetBool(Key + "_Show", true);
        UnityEditor.EditorApplication.isPlaying = true;
    }

    [UnityEditor.MenuItem("Tools/Record Chute Test")]
    static void Record()
    {
        UnityEditor.EditorPrefs.SetBool(Key, true);
        UnityEditor.EditorPrefs.SetBool(Key + "_Show", false);
        UnityEditor.EditorApplication.isPlaying = true;
    }
}
#endif
