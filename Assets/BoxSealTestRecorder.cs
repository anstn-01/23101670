// Tools > Record Box Seal Test
// 1) 빨간 버튼 → 상자 3종류 소환  2) 파란 버튼 → 물건 소환  3) 물건을 상자에 넣고 날개 닫기
// 4) 초록 버튼 → 테이프  5) 테이프를 상자에 붙여 봉인 (+ 봉인 후 날개가 안 열리는지 확인)
using System.Collections;
using System.IO;
using UnityEngine;

public class BoxSealTestRecorder : MonoBehaviour
{
    public string outputDir;
    public int width = 960, height = 540, fps = 15;
    int frame;
    Camera cam; RenderTexture rt; Texture2D tex; GameObject finger;
    Vector3 camPos, camLook;
    TextMesh caption;

    IEnumerator Start()
    {
        var red = FindFirstObjectByType<RedPushButton>();
        var blue = FindFirstObjectByType<BluePushButton>();
        var feed = FindFirstObjectByType<BoxConveyor>();
        var dropper = FindFirstObjectByType<ChuteDropper>();
        var disp = FindFirstObjectByType<TapeDispenser>();
        if (!red || !blue || !feed || !dropper || !disp) { Debug.LogError("[Rec] 버튼/컨베이어/슈트/테이프 기계 중 없는 것이 있어요"); yield break; }

        Directory.CreateDirectory(outputDir);
        foreach (var f in Directory.GetFiles(outputDir, "*.jpg")) File.Delete(f);
        Time.captureFramerate = fps;
        feed.speed = 1.2f;   // 녹화용으로 빠르게

        cam = new GameObject("RecCam").AddComponent<Camera>();
        cam.fieldOfView = 50f; cam.nearClipPlane = 0.02f;
        cam.stereoTargetEye = StereoTargetEyeMask.None; cam.depth = -100;
        rt = new RenderTexture(width, height, 24) { antiAliasing = 8 };
        cam.targetTexture = rt;
        tex = new Texture2D(width, height, TextureFormat.RGB24, false);

        caption = new GameObject("RecCaption").AddComponent<TextMesh>();
        caption.characterSize = 0.0025f; caption.fontSize = 64; caption.anchor = TextAnchor.UpperLeft; caption.color = Color.white;
        caption.transform.SetParent(cam.transform, false);
        caption.transform.localPosition = new Vector3(-0.27f, 0.145f, 0.35f);

        finger = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(finger.GetComponent<Collider>());
        finger.transform.localScale = Vector3.one * 0.045f;
        var fm = finger.GetComponent<Renderer>().material;
        var skin = new Color(0.93f, 0.76f, 0.64f);
        if (fm.HasProperty("_BaseColor")) fm.SetColor("_BaseColor", skin); else fm.color = skin;
        finger.SetActive(false);

        var ct = feed.transform;
        Vector3 room = -ct.forward;
        Vector3 D = ct.right * 0.7f + room * 0.35f + Vector3.up * 0.62f;      // 상자를 보는 방향(검증된 각도)
        Vector3 boxC = ct.TransformPoint(feed.stopLocal + Vector3.up * feed.boxSize.y * 0.5f);
        Vector3 btnMid = (red.transform.position + blue.transform.position) * 0.5f;
        Vector3 btnN = red.transform.forward;
        Vector3 btnCamPos = btnMid + btnN * 0.85f + Vector3.up * 0.12f;

        // ---------- 1) 빨간 버튼 3번 → 상자 3종류 ----------
        caption.text = "1. RED button -> box (Standard / Low / Long)";
        camPos = btnCamPos; camLook = btnMid;
        yield return Hold(0.5f);
        Transform box = null;
        for (int i = 0; i < 3; i++)
        {
            yield return PressButton(red.transform, btnN, () => red.Press());
            if (i == 0) box = feed.LastBox;
            yield return Hold(0.45f);
        }
        Vector3 spawnDir = (ct.TransformPoint(feed.spawnLocal) - boxC).normalized;
        Vector3 wideLook = boxC + spawnDir * 0.55f;
        yield return MoveCam(wideLook + D.normalized * 2.0f, wideLook, 1.0f);
        for (float t = 0; t < 2.2f && box != null && feed.IsMoving(box); t += 1f / fps) yield return Grab();
        yield return Hold(1.0f);
        if (box == null) { Finish(); yield break; }
        var slot = box.GetComponent<BoxSlot>();
        Vector3 s = slot.boxSize;
        boxC = box.position;

        // ---------- 2) 파란 버튼 → 물건 ----------
        caption.text = "2. BLUE button -> item drops from chute";
        yield return MoveCam(btnCamPos, btnMid, 0.8f);
        int idx = dropper.nextIndex;
        yield return PressButton(blue.transform, btnN, () => blue.Press());
        var item = dropper.LastItem;
        Vector3 land = item != null ? item.transform.position : dropper.dropPoint.position + Vector3.down * 1f;
        Vector3 look2 = Vector3.Lerp(boxC, land, 0.5f) + Vector3.down * 0.2f;
        yield return MoveCam(look2 + D.normalized * (Vector3.Distance(boxC, land) * 1.1f + 1.1f), look2, 0.6f);
        yield return Hold(1.4f);

        // ---------- 3) 물건을 상자에 넣고 닫기 ----------
        caption.text = "3. Put item in the box, close flaps";
        if (item != null)
        {
            var irb = item.GetComponent<Rigidbody>();
            yield return MoveFinger(finger.activeSelf ? finger.transform.position : item.transform.position + Vector3.up * 0.25f, item.transform.position + Vector3.up * 0.06f, 0.4f);
            irb.isKinematic = true;
            Vector3 a = item.transform.position, aboveBox = box.TransformPoint(new Vector3(0, s.y * 0.5f + 0.1f, 0));
            Vector3 off = finger.transform.position - a;
            for (float t = 0; t < 1.1f; t += 1f / fps)
            {
                if (item.transform.parent == box) break;   // 상자가 받아감
                float k = Mathf.SmoothStep(0, 1, t / 1.1f);
                Vector3 p = Vector3.Lerp(a, aboveBox, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 0.25f;
                item.transform.position = p; irb.position = p;
                finger.transform.position = p + off;
                yield return Grab();
            }
            if (item.transform.parent != box) irb.isKinematic = false;   // 손에서 놓기
            yield return MoveFinger(finger.transform.position, finger.transform.position + Vector3.up * 0.15f, 0.25f);
        }
        Vector3 nearPos = boxC + D, nearLook = boxC + Vector3.up * 0.06f;
        yield return MoveCam(nearPos, nearLook, 0.7f);
        yield return Hold(0.5f);
        var flaps = box.GetComponentsInChildren<BoxFlap>();
        System.Array.Sort(flaps, (x, y) => x.over.CompareTo(y.over));
        foreach (var f in flaps) yield return MoveFlap(f, f.angle, 0f);
        yield return Hold(0.5f);

        // ---------- 4) 초록 버튼 → 테이프 ----------
        caption.text = "4. GREEN button -> tape";
        Vector3 gPos = disp.button.position;
        Vector3 look4 = Vector3.Lerp(boxC, gPos, 0.5f);
        Vector3 cam4 = look4 + D.normalized * (Vector3.Distance(boxC, gPos) * 1.1f + 0.75f);
        yield return MoveCam(cam4, look4, 0.8f);
        Vector3 gOut = (cam4 - gPos).normalized;
        yield return PressButton(disp.button, gOut, () => disp.Press(), 0.0f);
        var tape = disp.Current;
        yield return Hold(0.9f);

        // ---------- 5) 테이프 붙여서 봉인 ----------
        caption.text = "5. Stick tape over the box -> sealed";
        if (tape != null)
        {
            Vector3 slotLocal = box.InverseTransformPoint(disp.spawnPoint.position);
            float side = slotLocal.z >= 0 ? 1f : -1f;
            float topSurf = s.y * 0.5f + slot.wall + 0.004f;
            yield return MoveFinger(finger.transform.position, tape.TipPosition, 0.45f);
            tape.externalGrab = true;
            Vector3 p0 = tape.TipPosition;
            Vector3 p1 = box.TransformPoint(new Vector3(0, topSurf + 0.015f, side * (s.z * 0.5f + 0.06f)));
            Vector3 p2 = box.TransformPoint(new Vector3(0, topSurf + 0.01f, side * (s.z * 0.5f - 0.08f)));
            for (float t = 0; t < 1.6f; t += 1f / fps)
            {
                if (tape == null || slot.Sealed) break;
                float k = t / 1.6f;
                Vector3 p = k < 0.7f ? Vector3.Lerp(p0, p1, Mathf.SmoothStep(0, 1, k / 0.7f)) : Vector3.Lerp(p1, p2, Mathf.SmoothStep(0, 1, (k - 0.7f) / 0.3f));
                tape.externalGrabPoint = p; finger.transform.position = p;
                yield return Grab();
            }
            if (tape != null) tape.externalGrab = false;
            yield return MoveFinger(finger.transform.position, finger.transform.position + Vector3.up * 0.18f, 0.3f);
        }
        yield return MoveCam(nearPos, nearLook, 0.6f);
        yield return Hold(1.0f);

        // 봉인 확인: 위 날개를 열어보려 해도 안 열림
        caption.text = slot.Sealed ? "Sealed! flaps stay closed" : "(not sealed)";
        BoxFlap topFlap = flaps[flaps.Length - 1];
        Vector3 tip = topFlap.TipWorld(0f, 1f);
        yield return MoveFinger(finger.transform.position, tip, 0.35f);
        for (float t = 0; t < 0.9f; t += 1f / fps)
        {
            Vector3 p = tip + Vector3.up * 0.12f * Mathf.Sin(t / 0.9f * Mathf.PI);
            finger.transform.position = p; topFlap.Drive(p);
            yield return Grab();
        }
        topFlap.Release();
        finger.SetActive(false);
        yield return Hold(1.2f);
        Debug.Log($"[Rec] 봉인 테스트: sealed={slot.Sealed}");
        Finish();
    }

    // ---------- 헬퍼 ----------
    IEnumerator Hold(float sec) { for (float t = 0; t < sec; t += 1f / fps) yield return Grab(); }

    IEnumerator MoveCam(Vector3 toPos, Vector3 toLook, float dur)
    {
        Vector3 p0 = camPos, l0 = camLook;
        for (float t = 0; t < dur; t += 1f / fps)
        {
            float k = Mathf.SmoothStep(0, 1, t / dur);
            camPos = Vector3.Lerp(p0, toPos, k); camLook = Vector3.Lerp(l0, toLook, k);
            yield return Grab();
        }
        camPos = toPos; camLook = toLook;
    }

    IEnumerator MoveFinger(Vector3 from, Vector3 to, float dur)
    {
        finger.SetActive(true);
        for (float t = 0; t < dur; t += 1f / fps)
        {
            finger.transform.position = Vector3.Lerp(from, to, Mathf.SmoothStep(0, 1, t / dur));
            yield return Grab();
        }
        finger.transform.position = to;
    }

    IEnumerator PressButton(Transform btn, Vector3 outward, System.Action press, float surface = 0.11f)
    {
        Vector3 face = btn.position + outward * surface;
        finger.SetActive(true);
        bool pressed = false;
        for (float t = 0; t < 0.7f; t += 1f / fps)
        {
            float k = 1f - Mathf.Abs(t / 0.35f - 1f);     // 0→1→0
            finger.transform.position = face + outward * Mathf.Lerp(0.3f, 0.015f, Mathf.SmoothStep(0, 1, k));
            if (!pressed && t >= 0.35f) { press(); pressed = true; }
            yield return Grab();
        }
        if (!pressed) press();
        finger.SetActive(false);
    }

    IEnumerator MoveFlap(BoxFlap f, float from, float to)
    {
        Vector3 start = finger.activeSelf ? finger.transform.position : f.TipWorld(from, 1f) + Vector3.up * 0.15f;
        yield return MoveFinger(start, f.TipWorld(from, 1f), 0.25f);
        for (float t = 0; t <= 0.6f; t += 1f / fps)
        {
            Vector3 p = f.TipWorld(Mathf.Lerp(from, to, Mathf.SmoothStep(0, 1, t / 0.6f)), 1f);
            finger.transform.position = p; f.Drive(p);
            yield return Grab();
        }
        f.Drive(f.TipWorld(to, 1f));
        yield return Grab();
        f.Release();
        yield return MoveFinger(finger.transform.position, finger.transform.position + Vector3.up * 0.07f, 0.15f);
    }

    IEnumerator Grab()
    {
        cam.transform.SetPositionAndRotation(camPos, Quaternion.LookRotation(camLook - camPos, Vector3.up));
        yield return new WaitForEndOfFrame();
        var prev = RenderTexture.active; RenderTexture.active = rt;
        tex.ReadPixels(new Rect(0, 0, width, height), 0, 0); tex.Apply(false);
        RenderTexture.active = prev;
        File.WriteAllBytes(Path.Combine(outputDir, $"f_{frame++:0000}.jpg"), tex.EncodeToJPG(85));
    }

    void Finish()
    {
        Time.captureFramerate = 0;
        Debug.Log($"[Rec] 봉인 테스트 녹화 완료: {frame} frames");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}

#if UNITY_EDITOR
[UnityEditor.InitializeOnLoad]
static class BoxSealTestRecorderMenu
{
    const string Key = "BoxSealTestRecorder_Pending";
    const string OutDir = @"C:\Users\User\Desktop\바탕화면 정리\기말 프로젝트\Assets\~BoxCapture";

    static BoxSealTestRecorderMenu()
    {
        UnityEditor.EditorApplication.playModeStateChanged += s =>
        {
            if (s != UnityEditor.PlayModeStateChange.EnteredPlayMode || !UnityEditor.EditorPrefs.GetBool(Key)) return;
            UnityEditor.EditorPrefs.SetBool(Key, false);
            new GameObject("BoxSealTestRecorder").AddComponent<BoxSealTestRecorder>().outputDir = OutDir;
        };
    }

    [UnityEditor.MenuItem("Tools/Record Box Seal Test")]
    static void Record()
    {
        UnityEditor.EditorPrefs.SetBool(Key, true);
        UnityEditor.EditorApplication.isPlaying = true;
    }
}
#endif
