// 초록 버튼을 누르면 Head_Slot에서 큐브가 밀려 나와 아래로 떨어집니다.
// - 에디터 Play 모드: Game 뷰에서 버튼을 마우스로 클릭
// - VR: XR Simple Interactable(버튼)의 Select 이벤트 → Press()
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class MachineCubeDispenser : MonoBehaviour
{
    [Header("References")]
    public Transform button;        // Button_Green
    public Transform spawnPoint;    // Head_Slot
    public Material cubeMaterial;

    [Header("Cube")]
    public float cubeSize = 0.12f;
    public float ejectTime = 0.35f;     // 슬롯에서 밀려 나오는 시간
    public float dropSpeed = 0.3f;      // 나온 뒤 아래로 주는 속도
    public int maxCubes = 20;           // 넘으면 오래된 큐브부터 삭제
    public bool makeGrabbable = true;   // XRI가 있으면 큐브를 잡을 수 있게

    [Header("Button")]
    public float pushDepth = 0.008f;
    public float cooldown = 0.35f;

    float lastPress = -99f;
    Vector3 buttonRest;
    readonly Queue<GameObject> cubes = new Queue<GameObject>();

    static System.Type grabType;
    static bool grabSearched;

    void Awake()
    {
        if (button) buttonRest = button.localPosition;
    }

    void Update()
    {
#if ENABLE_INPUT_SYSTEM
        var mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame) TryClick(mouse.position.ReadValue());
#else
        if (Input.GetMouseButtonDown(0)) TryClick(Input.mousePosition);
#endif
    }

    void TryClick(Vector2 screenPos)
    {
        var cam = Camera.main;
        if (cam == null || button == null) return;
        if (Physics.Raycast(cam.ScreenPointToRay(screenPos), out var hit, 50f) && hit.transform == button)
            Press();
    }

    /// 버튼 누르기 (XRI 이벤트나 다른 스크립트에서 호출 가능)
    public void Press()
    {
        if (Time.time - lastPress < cooldown || spawnPoint == null) return;
        lastPress = Time.time;
        if (button) StartCoroutine(PressAnim());
        StartCoroutine(Eject());
    }

    IEnumerator PressAnim()
    {
        // 버튼은 Machine 로컬 +Z(팔 안쪽) 방향으로 눌림
        Vector3 pressed = buttonRest + Vector3.forward * pushDepth;
        for (float t = 0; t < 0.06f; t += Time.deltaTime)
        { button.localPosition = Vector3.Lerp(buttonRest, pressed, t / 0.06f); yield return null; }
        button.localPosition = pressed;
        yield return new WaitForSeconds(0.08f);
        for (float t = 0; t < 0.1f; t += Time.deltaTime)
        { button.localPosition = Vector3.Lerp(pressed, buttonRest, t / 0.1f); yield return null; }
        button.localPosition = buttonRest;
    }

    IEnumerator Eject()
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "DispensedCube";
        if (cubeMaterial) go.GetComponent<Renderer>().sharedMaterial = cubeMaterial;

        var rb = go.AddComponent<Rigidbody>();
        rb.mass = 0.3f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.isKinematic = true;

        Vector3 start = spawnPoint.position;
        Vector3 down = -spawnPoint.up;
        Vector3 end = start + down * (cubeSize * 0.5f + 0.006f);
        go.transform.rotation = spawnPoint.rotation;

        // 슬롯에서 작게 시작해서 커지며 밀려 나옴
        for (float t = 0; t < ejectTime; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / ejectTime);
            go.transform.localScale = Vector3.one * cubeSize * Mathf.Lerp(0.1f, 1f, k);
            go.transform.position = Vector3.Lerp(start, end, k);
            yield return null;
        }
        go.transform.localScale = Vector3.one * cubeSize;
        go.transform.position = end;

        rb.isKinematic = false;
        rb.linearVelocity = down * dropSpeed;
        rb.angularVelocity = Random.insideUnitSphere * 1.5f;

        if (makeGrabbable)
        {
            var gt = FindGrabType();
            if (gt != null) go.AddComponent(gt);
        }

        cubes.Enqueue(go);
        while (cubes.Count > maxCubes)
        {
            var oldCube = cubes.Dequeue();
            if (oldCube) Destroy(oldCube);
        }
    }

    static System.Type FindGrabType()
    {
        if (grabSearched) return grabType;
        grabSearched = true;
        foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            System.Type[] types;
            try { types = asm.GetTypes(); } catch { continue; }
            foreach (var t in types)
                if (t.Name == "XRGrabInteractable" && typeof(Component).IsAssignableFrom(t)) { grabType = t; return t; }
        }
        return null;
    }
}
