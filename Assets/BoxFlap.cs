// 상자 윗날개 하나. 손(XR 인터랙터)으로 잡고 움직이면 경첩을 중심으로 열리고 닫혀요.
// angle 0 = 닫힘(평평하게 덮음), 약 200 = 바깥으로 활짝 열림
using UnityEngine;

public class BoxFlap : MonoBehaviour
{
    public Vector3 inwardLocal;          // 닫혔을 때 날개가 향하는 방향(상자 로컬, 수평)
    public float length = 0.15f;
    public float angle = 200f;
    public float maxAngle = 215f;
    public bool over;                    // 위에 덮이는 날개(나중에 닫힘)
    public bool locked;                  // 테이프로 봉인되면 true → 안 열림

    Component interactable;
    System.Reflection.PropertyInfo selProp, listProp;
    bool externalDrive; Vector3 externalPos;

    void Start() { Apply(); }

    public void SetInteractable(Component c)
    {
        interactable = c;
        if (c == null) return;
        selProp = c.GetType().GetProperty("isSelected");
        listProp = c.GetType().GetProperty("interactorsSelecting");
    }

    /// 손 위치(월드)를 주면 그 방향으로 날개 각도를 맞춤 (테스트/녹화용으로도 사용)
    public void Drive(Vector3 worldPos) { externalDrive = true; externalPos = worldPos; }
    public void Release() { externalDrive = false; }

    void Update()
    {
        if (locked) { angle = 0f; Apply(); return; }
        Vector3? hand = null;
        if (externalDrive) hand = externalPos;
        else if (interactable != null && selProp != null && (bool)selProp.GetValue(interactable))
        {
            var list = listProp?.GetValue(interactable) as System.Collections.IList;
            if (list != null && list.Count > 0 && list[0] is Component ic) hand = ic.transform.position;
        }

        if (hand.HasValue)
        {
            var box = transform.parent;
            Vector3 v = box.InverseTransformPoint(hand.Value) - transform.localPosition;
            float x = Vector3.Dot(v, inwardLocal), y = v.y;
            float a = Mathf.Atan2(y, x) * Mathf.Rad2Deg;
            if (a < -90f) a += 360f;
            a = Mathf.Clamp(a, 0f, maxAngle);
            if (a < 6f) a = 0f;                   // 거의 닫히면 딱 닫힘
            angle = Mathf.Lerp(angle, a, 1f - Mathf.Exp(-25f * Time.deltaTime));
            if (Mathf.Abs(angle - a) < 0.2f) angle = a;
        }
        Apply();
    }

    public Vector3 TipWorld(float ang, float frac = 0.85f)
    {
        float r = ang * Mathf.Deg2Rad;
        Vector3 dir = inwardLocal * Mathf.Cos(r) + Vector3.up * Mathf.Sin(r);
        return transform.parent.TransformPoint(transform.localPosition + dir * length * frac);
    }

    void Apply()
    {
        float r = angle * Mathf.Deg2Rad;
        Vector3 dir = inwardLocal * Mathf.Cos(r) + Vector3.up * Mathf.Sin(r);
        Vector3 nrm = -inwardLocal * Mathf.Sin(r) + Vector3.up * Mathf.Cos(r);
        transform.localRotation = Quaternion.LookRotation(dir, nrm);
    }
}

#if UNITY_EDITOR
static class BoxConveyorRewire
{
    // 스크립트를 교체한 뒤 빠진 컴포넌트를 정리하고 버튼 ↔ 컨베이어를 다시 연결
    [UnityEditor.MenuItem("Tools/Rewire Box Conveyor")]
    static void Rewire()
    {
        var conv = GameObject.Find("Conveyor_BD01");
        var btnGo = GameObject.Find("RedWallButton");
        if (conv == null) { Debug.LogWarning("[Rewire] Conveyor_BD01 없음"); return; }
        UnityEditor.GameObjectUtility.RemoveMonoBehavioursWithMissingScript(conv);
        if (btnGo) UnityEditor.GameObjectUtility.RemoveMonoBehavioursWithMissingScript(btnGo);

        var feed = conv.GetComponent<BoxConveyor>();
        if (feed == null) feed = UnityEditor.Undo.AddComponent<BoxConveyor>(conv);
        feed.boxMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/ConveyorMaterials/BOX_Cardboard.mat");
        feed.tapeMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/ConveyorMaterials/BOX_Tape.mat");
        feed.spacing = 0.66f;
        UnityEditor.EditorUtility.SetDirty(feed);

        var btn = btnGo ? btnGo.GetComponent<RedPushButton>() : null;
        if (btn) { btn.target = feed; UnityEditor.EditorUtility.SetDirty(btn); }
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(conv.scene);
        Debug.Log("[Rewire] 열린 상자 컨베이어 연결 완료" + (btn ? " (버튼 연결됨)" : " (버튼 없음)"));
    }
}
#endif
