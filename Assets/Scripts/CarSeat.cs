using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;

// 운전석. 문이 열려 있을 때 좌석을 잡으면(선택) 탑승, 탑승 중에 다시 좌석을 잡으면 하차합니다.
// 탑승 중에는 이동/점프/중력/텔레포트/회전과 머리 충돌 처리를 끄고,
// 매 프레임 머리 위치를 운전석 눈 위치에 고정합니다.
// (시뮬레이터 WASD 나 실제 걸음으로 머리가 움직여도 자리를 벗어나지 않고, 차가 움직이면 함께 따라갑니다.
//  고개를 돌려 둘러보는 회전은 그대로 가능합니다.)
[RequireComponent(typeof(XRSimpleInteractable))]
[DefaultExecutionOrder(1100)] // CarDrive(차 이동), HeadCollisionBlocker 보다 나중에 실행
public class CarSeat : MonoBehaviour
{
    [Tooltip("왼쪽 문 (열려 있어야 탈 수 있음)")]
    public CarDoorHinge door;
    [Tooltip("앉았을 때 눈 위치/방향")]
    public Transform eyePoint;
    [Tooltip("내릴 때 서는 위치/방향 (바닥 높이)")]
    public Transform exitPoint;
    [Tooltip("이 각도 이상 문이 열려야 탑승 가능")]
    public float requiredDoorAngle = 30f;
    [Tooltip("내릴 때 문을 자동으로 열기")]
    public bool openDoorOnExit = true;
    [Tooltip("탑승 중 머리 위치를 눈 위치에 고정 (플레이어 이동 막기)")]
    public bool lockHeadPosition = true;
    [Tooltip("탑승하면 문을 자동으로 닫기")]
    public bool closeDoorOnEnter = true;

    public bool IsSeated { get; private set; }

    XRSimpleInteractable seatInteractable;
    XROrigin origin;
    readonly List<Behaviour> disabled = new List<Behaviour>();
    CharacterController cc;
    bool ccWasEnabled;
    Vector3 lastSeatForward;

    void Awake()
    {
        seatInteractable = GetComponent<XRSimpleInteractable>();

        // 차에 운전 스크립트가 없으면 자동으로 붙임
        Transform car = door != null ? door.transform.parent : transform.parent;
        if (car != null && car.GetComponent<CarDrive>() == null)
            car.gameObject.AddComponent<CarDrive>().seat = this;
    }

    void OnEnable() { seatInteractable.selectEntered.AddListener(OnSelect); }
    void OnDisable() { seatInteractable.selectEntered.RemoveListener(OnSelect); }

    void OnSelect(SelectEnterEventArgs args)
    {
        if (IsSeated) Exit();
        else if (door == null || door.angle >= requiredDoorAngle) Enter();
        else Debug.Log("문을 먼저 열어야 탈 수 있습니다.");
    }

    void LateUpdate()
    {
        if (!IsSeated || !lockHeadPosition || origin == null || eyePoint == null) return;

        // 탑승 중 혹시 다시 켜진 이동 기능이 있으면 다시 끔
        foreach (var p in origin.GetComponentsInChildren<LocomotionProvider>())
            if (p.enabled) { p.enabled = false; if (!disabled.Contains(p)) disabled.Add(p); }

        // 차가 회전한 만큼 플레이어 시점도 같이 회전
        Vector3 fwd = Vector3.ProjectOnPlane(eyePoint.forward, Vector3.up);
        if (fwd.sqrMagnitude > 1e-6f && lastSeatForward.sqrMagnitude > 1e-6f)
        {
            float yaw = Vector3.SignedAngle(lastSeatForward, fwd, Vector3.up);
            if (Mathf.Abs(yaw) > 0.0001f) origin.RotateAroundCameraUsingOriginUp(yaw);
        }
        lastSeatForward = fwd;

        origin.MoveCameraToWorldLocation(eyePoint.position);
    }

    [ContextMenu("Enter (탑승)")]
    public void Enter()
    {
        origin = FindFirstObjectByType<XROrigin>();
        if (origin == null || eyePoint == null) return;

        // 이동 관련 기능 끄기
        disabled.Clear();
        foreach (var p in origin.GetComponentsInChildren<LocomotionProvider>(true))
            if (p.enabled) { p.enabled = false; disabled.Add(p); }
        var head = origin.GetComponent<HeadCollisionBlocker>();
        if (head != null && head.enabled) { head.enabled = false; disabled.Add(head); }
        cc = origin.GetComponent<CharacterController>();
        ccWasEnabled = cc != null && cc.enabled;
        if (ccWasEnabled) cc.enabled = false;

        // 눈 위치/방향으로 이동
        origin.MatchOriginUpCameraForward(eyePoint.up, eyePoint.forward);
        origin.MoveCameraToWorldLocation(eyePoint.position);
        lastSeatForward = Vector3.ProjectOnPlane(eyePoint.forward, Vector3.up);
        IsSeated = true;

        // 탑승하면 문 닫기
        if (closeDoorOnEnter && door != null) door.Close();
    }

    [ContextMenu("Exit (하차)")]
    public void Exit()
    {
        if (!IsSeated || origin == null) return;
        IsSeated = false;
        if (openDoorOnExit && door != null) door.Open();

        if (exitPoint != null)
        {
            origin.MatchOriginUpCameraForward(Vector3.up, exitPoint.forward);
            Transform cam = origin.Camera.transform;
            Vector3 offset = cam.position - origin.transform.position;
            origin.transform.position = new Vector3(exitPoint.position.x - offset.x, exitPoint.position.y, exitPoint.position.z - offset.z);
        }

        if (ccWasEnabled && cc != null) cc.enabled = true;
        foreach (var b in disabled) if (b != null) b.enabled = true;
        disabled.Clear();
    }
}
