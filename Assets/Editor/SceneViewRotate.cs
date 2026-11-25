// Tools > Scene View > Rotate Left 90 / Rotate Right 90
// 씬 뷰 카메라를 현재 보고 있는 지점을 중심으로 좌/우 90도 회전합니다.
using UnityEngine;
using UnityEditor;

public static class SceneViewRotate
{
    [MenuItem("Tools/Scene View/Rotate Left 90 %#LEFT")]
    static void Left() => Rotate(-90f);

    [MenuItem("Tools/Scene View/Rotate Right 90 %#RIGHT")]
    static void Right() => Rotate(90f);

    static void Rotate(float yaw)
    {
        var sv = SceneView.lastActiveSceneView;
        if (sv == null) return;
        sv.rotation = Quaternion.Euler(0f, yaw, 0f) * sv.rotation;
        sv.Repaint();
    }
}
