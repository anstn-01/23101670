using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

// 차 문에 붙이는 스크립트. 손잡이(handle)를 잡고 손을 움직이면 문이 경첩(hinge)을 중심으로 열리고 닫힙니다.
// 문 오브젝트를 다른 부모로 옮기지 않고, 부모 기준 좌표에서 경첩 축을 중심으로 회전시킵니다.
// 바깥 손잡이는 문 바깥면에 있어서 차 안에서는 문 메쉬에 가려 잡을 수 없으므로,
// 시작할 때 문 안쪽 면에 "안쪽 손잡이"를 자동으로 만들어 안에서도 잡을 수 있게 합니다.
public class CarDoorHinge : MonoBehaviour
{
    [Tooltip("손잡이 인터랙터블 (Door Handle, 바깥쪽)")]
    public XRBaseInteractable handle;

    [Tooltip("문 안쪽 면에 안쪽 손잡이를 자동으로 만들기")]
    public bool createInnerHandle = true;
    [Tooltip("안쪽 손잡이 두께 (m). 문 안쪽 면에서 차 안쪽으로 튀어나오는 정도")]
    public float innerHandleThickness = 0.12f;
    [Tooltip("안쪽 손잡이 (비워두면 시작할 때 자동 생성)")]
    public XRBaseInteractable innerHandle;

    [Tooltip("경첩 위치 (문 부모 기준 로컬 좌표)")]
    public Vector3 hingeLocal;
    [Tooltip("경첩 회전축 (문 부모 기준 로컬 방향)")]
    public Vector3 axisLocal = Vector3.up;
    [Tooltip("닫힌 상태에서 경첩 → 손잡이 방향 (문 부모 기준 로컬 방향)")]
    public Vector3 closedDirLocal = Vector3.back;
    [Tooltip("+1 또는 -1. 이 방향으로 돌면 문이 바깥으로 열립니다.")]
    public float openSign = 1f;

    [Tooltip("최대 열림 각도")]
    public float maxAngle = 70f;
    [Tooltip("손을 놓았을 때 이 각도 이하이면 자동으로 닫힘")]
    public float autoCloseAngle = 5f;
    [Tooltip("문이 따라오는 부드러움 (클수록 빠름)")]
    public float followSpeed = 15f;

    [Tooltip("현재 열린 각도 (0 = 닫힘)")]
    [Range(0, 120)] public float angle;

    Vector3 closedPos;
    Quaternion closedRot;
    float targetAngle;
    float grabHandAngle, grabDoorAngle;
    IXRSelectInteractor grabber;
    XRBaseInteractable grabbedHandle;

    void Awake()
    {
        closedPos = transform.localPosition;
        closedRot = transform.localRotation;
        targetAngle = angle;
        if (createInnerHandle && innerHandle == null && handle != null) CreateInnerHandle();
        Apply(angle);
    }

    void OnEnable()
    {
        Register(handle, true);
        Register(innerHandle, true);
    }

    void OnDisable()
    {
        Register(handle, false);
        Register(innerHandle, false);
    }

    void Register(XRBaseInteractable h, bool add)
    {
        if (h == null) return;
        if (add)
        {
            h.selectEntered.AddListener(OnGrab);
            h.selectExited.AddListener(OnRelease);
        }
        else
        {
            h.selectEntered.RemoveListener(OnGrab);
            h.selectExited.RemoveListener(OnRelease);
        }
    }

    // 바깥 손잡이를 문 두께 방향으로 반대편(안쪽 면)에 복제
    void CreateInnerHandle()
    {
        var mf = GetComponent<MeshFilter>();
        var outerCol = handle.GetComponent<BoxCollider>();
        if (mf == null || mf.sharedMesh == null || outerCol == null) return;

        Transform h = handle.transform;
        Bounds mb = mf.sharedMesh.bounds;
        Vector3 doorCenterW = transform.TransformPoint(mb.center);
        Vector3 handleCenterW = h.TransformPoint(outerCol.center);

        // 손잡이 박스의 x축이 문 두께(바깥) 방향
        Vector3 outward = h.right * Mathf.Sign(Vector3.Dot(handleCenterW - doorCenterW, h.right));

        // 문 메쉬에서 가장 안쪽 면 위치
        float innerFace = float.MaxValue;
        for (int i = 0; i < 8; i++)
        {
            Vector3 c = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
            innerFace = Mathf.Min(innerFace, Vector3.Dot(transform.TransformPoint(c), outward));
        }

        // 안쪽 면에서 4cm 는 문 안에 묻히고, 나머지는 차 안쪽으로 튀어나오게
        float centerO = innerFace + 0.04f - innerHandleThickness * 0.5f;
        Vector3 innerCenterW = handleCenterW + outward * (centerO - Vector3.Dot(handleCenterW, outward));

        var go = new GameObject("Door Handle (Inner)");
        go.SetActive(false); // 콜라이더를 먼저 붙인 뒤 인터랙터블이 콜라이더를 수집하도록
        go.layer = h.gameObject.layer;
        go.transform.SetParent(transform, false);
        go.transform.SetPositionAndRotation(innerCenterW, h.rotation);

        var col = go.AddComponent<BoxCollider>();
        Vector3 ls = go.transform.lossyScale;
        Vector3 outerWorldSize = Vector3.Scale(outerCol.size, h.lossyScale);
        col.size = new Vector3(innerHandleThickness / Mathf.Abs(ls.x),
                               Mathf.Abs(outerWorldSize.y) / Mathf.Abs(ls.y),
                               Mathf.Abs(outerWorldSize.z) / Mathf.Abs(ls.z));

        var src = handle as XRSimpleInteractable;
        var inner = go.AddComponent<XRSimpleInteractable>();
        if (src != null)
        {
            inner.interactionLayers = src.interactionLayers;
            inner.selectMode = src.selectMode;
        }
        go.SetActive(true);
        innerHandle = inner;
    }

    void OnGrab(SelectEnterEventArgs args)
    {
        grabber = args.interactorObject;
        grabbedHandle = args.interactableObject as XRBaseInteractable;
        grabHandAngle = HandAngle();
        grabDoorAngle = targetAngle;
    }

    void OnRelease(SelectExitEventArgs args)
    {
        if (args.interactorObject != grabber) return;
        grabber = null;
        grabbedHandle = null;
        if (targetAngle < autoCloseAngle) targetAngle = 0f;
    }

    void Update()
    {
        if (grabber != null)
        {
            float delta = Mathf.DeltaAngle(grabHandAngle, HandAngle());
            targetAngle = Mathf.Clamp(grabDoorAngle + delta, 0f, maxAngle);
        }
        angle = Mathf.Lerp(angle, targetAngle, 1f - Mathf.Exp(-followSpeed * Time.deltaTime));
        Apply(angle);
    }

    float HandAngle()
    {
        if (grabber == null || transform.parent == null) return 0f;
        Transform p = transform.parent;
        Vector3 hand = grabber.GetAttachTransform(grabbedHandle != null ? grabbedHandle : handle).position;
        Vector3 hinge = p.TransformPoint(hingeLocal);
        Vector3 axis = p.TransformDirection(axisLocal).normalized;
        Vector3 refDir = Vector3.ProjectOnPlane(p.TransformDirection(closedDirLocal), axis);
        Vector3 v = Vector3.ProjectOnPlane(hand - hinge, axis);
        if (v.sqrMagnitude < 1e-6f) return grabHandAngle;
        return Vector3.SignedAngle(refDir, v, axis) * openSign;
    }

    void Apply(float a)
    {
        Quaternion q = Quaternion.AngleAxis(a * openSign, axisLocal);
        transform.localRotation = q * closedRot;
        transform.localPosition = hingeLocal + q * (closedPos - hingeLocal);
    }

    // 코드나 버튼에서 호출할 수 있는 함수
    [ContextMenu("Open (열기)")]
    public void Open() { targetAngle = maxAngle; }
    [ContextMenu("Close (닫기)")]
    public void Close() { targetAngle = 0f; }
    public void Toggle() { targetAngle = targetAngle > maxAngle * 0.5f ? 0f : maxAngle; }
}
