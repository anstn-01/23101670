// 상자 윗날개 하나. 손(XR 인터랙터)으로 잡고 움직이면 경첩을 중심으로 열리고 닫혀요.
// angle 0 = 닫힘(평평하게 덮음), 약 200 = 바깥으로 활짝 열림, 약 270 = 바깥으로 넘어가 옆면(Wall_)에 붙음
// - 손에서 놓으면 중력을 받아 떨어짐 (90도보다 덜 열렸으면 닫히고, 더 열렸으면 바깥으로 넘어감)
// - 바깥으로 넘어가면 옆면(Wall_)에 닿는 각도에서 막힘 (잡고 있어도 벽을 뚫지 않음)
using UnityEngine;

public class BoxFlap : MonoBehaviour
{
    public Vector3 inwardLocal;          // 닫혔을 때 날개가 향하는 방향(상자 로컬, 수평)
    public float length = 0.15f;
    public float angle = 200f;
    public float maxAngle = 268f;        // 바깥으로 넘어가 옆면(Wall_)에 닿는 각도
    public bool over;                    // 위에 덮이는 날개(나중에 닫힘)
    public bool locked;                  // 테이프로 봉인되면 true → 안 열림

    [Header("중력")]
    public bool useGravity = true;
    [Tooltip("1 = 실제 얇은 판처럼 떨어짐. 작을수록 천천히")]
    public float gravityScale = 0.6f;
    [Tooltip("경첩 마찰 (클수록 빨리 멈춤)")]
    public float damping = 3f;
    [Tooltip("닫힘/옆면에 부딪혔을 때 튕기는 정도")]
    public float bounce = 0.15f;

    /// 지금 손으로 잡고 있는지
    public bool IsHeld => externalDrive || (interactable != null && selProp != null && (bool)selProp.GetValue(interactable));

    Component interactable;
    System.Reflection.PropertyInfo selProp, listProp;
    bool externalDrive; Vector3 externalPos;
    float vel;          // 각속도 (도/초)
    float wallLimit;    // 실제 멈추는 각도

    void Start()
    {
        // 날개가 상자 높이보다 길면(낮은 상자) 바닥 아래로 삐져나오기 전에 멈춤
        float h = transform.localPosition.y * 2f - 0.005f;     // 경첩 ~ 상자 바닥
        wallLimit = maxAngle;
        if (h > 0f && length > h)
            wallLimit = Mathf.Min(maxAngle, 180f + Mathf.Asin(Mathf.Clamp01(h / length)) * Mathf.Rad2Deg);
        Apply();
    }

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

    float Limit => wallLimit > 0f ? wallLimit : maxAngle;

    void Update()
    {
        if (locked) { angle = 0f; vel = 0f; Apply(); return; }
        Vector3? hand = null;
        if (externalDrive) hand = externalPos;
        else if (interactable != null && selProp != null && (bool)selProp.GetValue(interactable))
        {
            var list = listProp?.GetValue(interactable) as System.Collections.IList;
            if (list != null && list.Count > 0 && list[0] is Component ic) hand = ic.transform.position;
        }

        float dt = Time.deltaTime;
        if (hand.HasValue)
        {
            var box = transform.parent;
            Vector3 v = box.InverseTransformPoint(hand.Value) - transform.localPosition;
            float x = Vector3.Dot(v, inwardLocal), y = v.y;
            float a = Mathf.Atan2(y, x) * Mathf.Rad2Deg;
            if (a < -90f) a += 360f;
            a = Mathf.Clamp(a, 0f, Limit);          // 옆면(Wall_)에서 막힘
            if (a < 6f) a = 0f;                   // 거의 닫히면 딱 닫힘
            float prev = angle;
            angle = Mathf.Lerp(angle, a, 1f - Mathf.Exp(-25f * dt));
            if (Mathf.Abs(angle - a) < 0.2f) angle = a;
            // 손으로 움직이던 속도 기억 → 놓으면 그 속도 그대로 이어서 떨어짐
            if (dt > 0f) vel = Mathf.Lerp(vel, (angle - prev) / dt, 0.5f);
        }
        else if (useGravity) Fall(dt);
        Apply();
    }

    // 경첩에 달린 얇은 판에 중력 적용
    void Fall(float dt)
    {
        var box = transform.parent;
        if (box == null || dt <= 0f) return;

        // 각도가 커지는 방향(접선)이 위를 향할수록 중력이 반대로 끌어내림
        float r = angle * Mathf.Deg2Rad;
        Vector3 tangent = box.TransformDirection(-inwardLocal * Mathf.Sin(r) + Vector3.up * Mathf.Cos(r)).normalized;
        Vector3 up = Physics.gravity.sqrMagnitude > 0f ? -Physics.gravity.normalized : Vector3.up;
        float g = Physics.gravity.magnitude * gravityScale;
        float alpha = -(1.5f * g / Mathf.Max(0.02f, length)) * Vector3.Dot(up, tangent) * Mathf.Rad2Deg;

        vel += alpha * dt;
        vel *= Mathf.Exp(-damping * dt);
        angle += vel * dt;

        if (angle <= 0f)
        {
            angle = 0f;                            // 닫힘 위치에서 멈춤
            vel = -vel * bounce;
            if (Mathf.Abs(vel) < 15f) vel = 0f;
        }
        else if (angle >= Limit)
        {
            angle = Limit;                         // 옆면(Wall_)에 닿아서 멈춤
            vel = -vel * bounce;
            if (Mathf.Abs(vel) < 15f) vel = 0f;
        }
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
