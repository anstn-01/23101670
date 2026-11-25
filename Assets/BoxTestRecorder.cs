// Tools > Record Box Test
// 빨간 버튼 누름 → 열린 상자가 나와 벨트를 따라 테이블까지 → 손으로 날개 4장을 닫고, 하나를 다시 엶
using System.Collections;
using System.IO;
using UnityEngine;

public class BoxTestRecorder : MonoBehaviour
{
    public string outputDir;
    public Vector3 widePos; public Quaternion wideRot;
    public int width = 960, height = 540, fps = 20;

    Camera cam; RenderTexture rt; Texture2D tex; GameObject finger; int frame;

    IEnumerator Start()
    {
        var btn = FindFirstObjectByType<RedPushButton>();
        var feed = FindFirstObjectByType<BoxConveyor>();
        if (btn == null || feed == null) { Debug.LogError("[Rec] 버튼/컨베이어 없음"); yield break; }

        Directory.CreateDirectory(outputDir);
        foreach (var f in Directory.GetFiles(outputDir, "*.jpg")) File.Delete(f);
        Time.captureFramerate = fps;

        var camGo = new GameObject("RecCam");
        cam = camGo.AddComponent<Camera>();
        cam.fieldOfView = 55f; cam.nearClipPlane = 0.02f;
        cam.stereoTargetEye = StereoTargetEyeMask.None; cam.depth = -100;
        rt = new RenderTexture(width, height, 24) { antiAliasing = 4 };
        cam.targetTexture = rt;
        tex = new Texture2D(width, height, TextureFormat.RGB24, false);

        finger = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(finger.GetComponent<Collider>());
        finger.transform.localScale = Vector3.one * 0.045f;
        var fm = finger.GetComponent<Renderer>().material;
        var skin = new Color(0.93f, 0.76f, 0.64f);
        if (fm.HasProperty("_BaseColor")) fm.SetColor("_BaseColor", skin); else fm.color = skin;
        finger.SetActive(false);

        // 1) 버튼 클로즈업 + 누르기
        var bt = btn.transform;
        Vector3 bPos = bt.TransformPoint(new Vector3(0, 0, 0.11f));
        Vector3 n = bt.forward, right = Vector3.Cross(Vector3.up, n).normalized;
        Vector3 closePos = bPos + n * 0.85f - right * 0.35f + Vector3.up * 0.12f;
        Quaternion closeRot = Quaternion.LookRotation(bPos - closePos, Vector3.up);

        bool pressed = false;
        for (float t = 0; t < 2.2f; t += 1f / fps)
        {
            float reach = Mathf.SmoothStep(0, 1, Mathf.Clamp01(1f - Mathf.Abs(t - 1.0f) / 0.45f));
            finger.transform.position = bPos + n * Mathf.Lerp(0.35f, 0.01f, reach);
            finger.SetActive(reach > 0.02f);
            if (t >= 1.0f && !pressed) { btn.Press(); pressed = true; }
            Cam(closePos, closeRot);
            yield return Grab();
        }
        finger.SetActive(false);

        // 2) 전체 샷으로 빠지며 상자 이동
        var box = feed.LastBox;
        float tw = 0;
        while (box != null && (feed.IsMoving(box) || tw < 1.6f) && tw < 12f)
        {
            float k = Mathf.SmoothStep(0, 1, Mathf.Clamp01(tw / 1.6f));
            Cam(Vector3.Lerp(closePos, widePos, k), Quaternion.Slerp(closeRot, wideRot, k));
            tw += 1f / fps;
            yield return Grab();
        }
        if (box == null) { Finish(); yield break; }

        // 3) 테이블 위 상자로 다가감
        var ct = feed.transform;
        Vector3 boxC = box.position;
        Vector3 room = -ct.forward;                        // 방 안쪽
        Vector3 nearPos = boxC + ct.right * 0.75f + room * 0.35f + Vector3.up * 0.6f;   // 방 앞쪽 대각선에서
        Quaternion nearRot = Quaternion.LookRotation(boxC + Vector3.up * 0.03f - nearPos, Vector3.up);
        for (float t = 0; t < 1.6f; t += 1f / fps)
        {
            float k = Mathf.SmoothStep(0, 1, t / 1.6f);
            Cam(Vector3.Lerp(widePos, nearPos, k), Quaternion.Slerp(wideRot, nearRot, k));
            yield return Grab();
        }
        for (float t = 0; t < 0.5f; t += 1f / fps) { Cam(nearPos, nearRot); yield return Grab(); }

        // 4) 손으로 날개 닫기: 아래 날개(Z) → 위 날개(X)
        var flaps = box.GetComponentsInChildren<BoxFlap>();
        System.Array.Sort(flaps, (a, b) => a.over.CompareTo(b.over));
        foreach (var f in flaps) yield return MoveFlap(f, f.angle, 0f, nearPos, nearRot);

        for (float t = 0; t < 0.8f; t += 1f / fps) { Cam(nearPos, nearRot); yield return Grab(); }

        // 5) 위 날개 하나 다시 열기
        var reopen = flaps[flaps.Length - 1];
        yield return MoveFlap(reopen, 0f, 200f, nearPos, nearRot);
        for (float t = 0; t < 1.0f; t += 1f / fps) { Cam(nearPos, nearRot); yield return Grab(); }

        Finish();
    }

    IEnumerator MoveFlap(BoxFlap f, float from, float to, Vector3 cp, Quaternion cr)
    {
        Vector3 start = finger.activeSelf ? finger.transform.position : f.TipWorld(from, 1f) + Vector3.up * 0.15f;
        finger.SetActive(true);
        // 날개 끝으로 손 이동
        for (float t = 0; t < 0.35f; t += 1f / fps)
        {
            finger.transform.position = Vector3.Lerp(start, f.TipWorld(from, 1f), Mathf.SmoothStep(0, 1, t / 0.35f));
            Cam(cp, cr); yield return Grab();
        }
        // 잡고 돌리기 (실제 플레이어 손과 같은 BoxFlap.Drive 경로)
        for (float t = 0; t <= 0.9f; t += 1f / fps)
        {
            float a = Mathf.Lerp(from, to, Mathf.SmoothStep(0, 1, t / 0.9f));
            Vector3 p = f.TipWorld(a, 1.0f);
            finger.transform.position = p;
            f.Drive(p);
            Cam(cp, cr); yield return Grab();
        }
        f.Drive(f.TipWorld(to, 1.0f));
        Cam(cp, cr); yield return Grab();
        f.Release();
        // 손 살짝 떼기
        Vector3 s2 = finger.transform.position;
        for (float t = 0; t < 0.25f; t += 1f / fps)
        {
            finger.transform.position = s2 + Vector3.up * 0.08f * (t / 0.25f);
            Cam(cp, cr); yield return Grab();
        }
    }

    void Cam(Vector3 p, Quaternion r) => cam.transform.SetPositionAndRotation(p, r);

    IEnumerator Grab()
    {
        yield return new WaitForEndOfFrame();
        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        tex.Apply(false);
        RenderTexture.active = prev;
        File.WriteAllBytes(Path.Combine(outputDir, $"f_{frame++:0000}.jpg"), tex.EncodeToJPG(85));
    }

    void Finish()
    {
        Time.captureFramerate = 0;
        Debug.Log($"[Rec] 박스 테스트 녹화 완료: {frame} frames → {outputDir}");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}

#if UNITY_EDITOR
[UnityEditor.InitializeOnLoad]
static class BoxTestRecorderMenu
{
    const string Key = "BoxTestRecorder_Pending";
    const string OutDir = @"C:\Users\User\Desktop\바탕화면 정리\기말 프로젝트\Assets\~BoxCapture";

    static BoxTestRecorderMenu()
    {
        UnityEditor.EditorApplication.playModeStateChanged += s =>
        {
            if (s != UnityEditor.PlayModeStateChange.EnteredPlayMode || !UnityEditor.EditorPrefs.GetBool(Key)) return;
            UnityEditor.EditorPrefs.SetBool(Key, false);
            var r = new GameObject("BoxTestRecorder").AddComponent<BoxTestRecorder>();
            r.outputDir = OutDir;
            r.widePos = JsonUtility.FromJson<Vector3>(UnityEditor.EditorPrefs.GetString(Key + "_pos"));
            r.wideRot = JsonUtility.FromJson<Quaternion>(UnityEditor.EditorPrefs.GetString(Key + "_rot"));
        };
    }

    [UnityEditor.MenuItem("Tools/Record Box Test")]
    static void Record()
    {
        var sv = UnityEditor.SceneView.lastActiveSceneView;
        var ct = sv ? sv.camera.transform : null;
        UnityEditor.EditorPrefs.SetString(Key + "_pos", JsonUtility.ToJson(ct ? ct.position : Vector3.zero));
        UnityEditor.EditorPrefs.SetString(Key + "_rot", JsonUtility.ToJson(ct ? ct.rotation : Quaternion.identity));
        UnityEditor.EditorPrefs.SetBool(Key, true);
        UnityEditor.EditorApplication.isPlaying = true;
    }
}
#endif
