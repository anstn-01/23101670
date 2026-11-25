// Tools > Record Tape Test
// 버튼 누름 → 테이프 끝을 잡고 앞으로 당김 → 버튼 다시 눌러 끊기 → 놓기 를 자동으로 녹화
using System.Collections;
using System.IO;
using UnityEngine;

public class TapeTestRecorder : MonoBehaviour
{
    public string outputDir;
    public int width = 1280, height = 720, fps = 30;
    public float duration = 10f;

    IEnumerator Start()
    {
        var m = GameObject.Find("Machine");
        var disp = m ? m.GetComponent<TapeDispenser>() : null;
        if (disp == null) { Debug.LogError("[Rec] TapeDispenser 없음"); yield break; }

        Directory.CreateDirectory(outputDir);
        foreach (var f in Directory.GetFiles(outputDir, "*.jpg")) File.Delete(f);
        Time.captureFramerate = fps;

        var camGo = new GameObject("RecCam");
        var cam = camGo.AddComponent<Camera>();
        cam.fieldOfView = 36f;
        cam.nearClipPlane = 0.02f;
        cam.stereoTargetEye = StereoTargetEyeMask.None;
        cam.depth = -100;
        var rt = new RenderTexture(width, height, 24) { antiAliasing = 4 };
        cam.targetTexture = rt;
        var tex = new Texture2D(width, height, TextureFormat.RGB24, false);

        int total = Mathf.RoundToInt(duration * fps);
        bool pressed1 = false, pressed2 = false;
        Vector3 grabStart = Vector3.zero;
        bool grabbing = false;
        Vector3 pullEnd = m.transform.TransformPoint(new Vector3(0.5f, 0.62f, -0.85f));

        for (int f = 0; f < total; f++)
        {
            float t = f / (float)fps;

            // 카메라
            float yaw = Mathf.Lerp(-32f, -12f, Mathf.SmoothStep(0, 1, t / duration));
            var pivot = m.transform.TransformPoint(new Vector3(0.3f, 0.55f, -0.45f));
            var rot = m.transform.rotation * Quaternion.Euler(24f, yaw, 0f);
            camGo.transform.SetPositionAndRotation(pivot - rot * Vector3.forward * 2.7f, rot);

            // 시나리오
            if (!pressed1 && t >= 0.6f) { disp.Press(); pressed1 = true; }
            var tape = disp.Current;
            if (tape != null && !grabbing && t >= 1.8f && t < 5.0f)
            { grabbing = true; grabStart = tape.TipPosition; }
            if (grabbing && tape != null)
            {
                float k = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(1.8f, 4.3f, t));
                Vector3 wiggle = m.transform.up * (0.05f * Mathf.Sin(t * 4f)) * k;
                tape.externalGrab = true;
                tape.externalGrabPoint = Vector3.Lerp(grabStart, pullEnd, k) + wiggle;
            }
            if (!pressed2 && t >= 5.2f)
            {
                var keep = disp.Current;
                disp.Press();            // 끊기
                pressed2 = true;
                if (keep != null) StartCoroutine(HoldThenRelease(keep, 0.6f));
            }

            yield return new WaitForEndOfFrame();
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply(false);
            RenderTexture.active = prev;
            File.WriteAllBytes(Path.Combine(outputDir, $"f_{f:0000}.jpg"), tex.EncodeToJPG(88));
        }

        Time.captureFramerate = 0;
        Debug.Log($"[Rec] 녹화 완료: {total} frames → {outputDir}");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    IEnumerator HoldThenRelease(TapeRibbon tape, float secs)
    {
        Vector3 hold = tape.externalGrabPoint;
        for (float t = 0; t < secs; t += Time.deltaTime)
        { tape.externalGrab = true; tape.externalGrabPoint = hold; yield return null; }
        tape.externalGrab = false;
    }
}

#if UNITY_EDITOR
[UnityEditor.InitializeOnLoad]
static class TapeTestRecorderMenu
{
    const string Key = "TapeTestRecorder_Pending";
    const string OutDir = @"C:\Users\User\Desktop\바탕화면 정리\기말 프로젝트\Assets\~MachineCapture";

    static TapeTestRecorderMenu()
    {
        UnityEditor.EditorApplication.playModeStateChanged += s =>
        {
            if (s != UnityEditor.PlayModeStateChange.EnteredPlayMode || !UnityEditor.EditorPrefs.GetBool(Key)) return;
            UnityEditor.EditorPrefs.SetBool(Key, false);
            new GameObject("TapeTestRecorder").AddComponent<TapeTestRecorder>().outputDir = OutDir;
        };
    }

    [UnityEditor.MenuItem("Tools/Tape Color Amber")]
    static void Amber()
    {
        var mat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/MachineMaterials/M_TapeClear.mat");
        if (mat == null) return;
        var c = new Color(1f, 0.84f, 0.52f, 0.6f);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", c);
        UnityEditor.EditorUtility.SetDirty(mat);
        UnityEditor.AssetDatabase.SaveAssets();
    }

    [UnityEditor.MenuItem("Tools/Record Tape Test")]
    static void Record()
    {
        UnityEditor.EditorPrefs.SetBool(Key, true);
        UnityEditor.EditorApplication.isPlaying = true;
    }
}
#endif
