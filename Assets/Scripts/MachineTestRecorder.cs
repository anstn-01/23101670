// Tools > Record Machine Test
// Play 모드로 들어가서 녹화 전용 카메라로 기계를 비추며 버튼을 두 번 누르고,
// 프레임을 JPG로 저장한 뒤 Play를 자동 종료합니다.
using System.Collections;
using System.IO;
using UnityEngine;

public class MachineTestRecorder : MonoBehaviour
{
    public string outputDir;
    public int width = 1280, height = 720, fps = 30;
    public float duration = 9f;
    public float[] pressTimes = { 0.8f, 4.6f };

    IEnumerator Start()
    {
        var m = GameObject.Find("Machine");
        if (m == null) { Debug.LogError("[Rec] Machine 없음"); yield break; }
        var disp = m.GetComponent<MachineTapeDispenser>();

        Directory.CreateDirectory(outputDir);
        foreach (var f in Directory.GetFiles(outputDir, "*.jpg")) File.Delete(f);

        Time.captureFramerate = fps;

        var camGo = new GameObject("RecCam");
        var cam = camGo.AddComponent<Camera>();
        cam.fieldOfView = 30f;
        cam.nearClipPlane = 0.02f;
        cam.stereoTargetEye = StereoTargetEyeMask.None;
        cam.depth = -100;
        var rt = new RenderTexture(width, height, 24) { antiAliasing = 4 };
        cam.targetTexture = rt;
        var tex = new Texture2D(width, height, TextureFormat.RGB24, false);

        int total = Mathf.RoundToInt(duration * fps);
        int nextPress = 0;
        for (int f = 0; f < total; f++)
        {
            float t = f / (float)fps;
            float yaw = Mathf.Lerp(-40f, -12f, Mathf.SmoothStep(0, 1, t / duration));
            var pivot = m.transform.TransformPoint(new Vector3(0.1f, 0.62f, 0f));
            var rot = m.transform.rotation * Quaternion.Euler(13f, yaw, 0f);
            camGo.transform.SetPositionAndRotation(pivot - rot * Vector3.forward * 3.1f, rot);

            if (disp != null && nextPress < pressTimes.Length && t >= pressTimes[nextPress])
            { disp.Press(); nextPress++; }

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
}

#if UNITY_EDITOR
[UnityEditor.InitializeOnLoad]
static class MachineTestRecorderMenu
{
    const string Key = "MachineTestRecorder_Pending";
    public const string OutDir = @"C:\Users\User\Desktop\바탕화면 정리\기말 프로젝트\Assets\~MachineCapture";

    static MachineTestRecorderMenu()
    {
        UnityEditor.EditorApplication.playModeStateChanged += s =>
        {
            if (s != UnityEditor.PlayModeStateChange.EnteredPlayMode || !UnityEditor.EditorPrefs.GetBool(Key)) return;
            UnityEditor.EditorPrefs.SetBool(Key, false);
            var go = new GameObject("MachineTestRecorder");
            go.AddComponent<MachineTestRecorder>().outputDir = OutDir;
        };
    }

    [UnityEditor.MenuItem("Tools/Record Machine Test")]
    static void Record()
    {
        UnityEditor.EditorPrefs.SetBool(Key, true);
        UnityEditor.EditorApplication.isPlaying = true;
    }
}
#endif
