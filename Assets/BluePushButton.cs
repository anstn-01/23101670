// 벽에 붙는 파란 버튼. Press() 하면 눌리는 애니메이션 + 수직 배출 슈트(VD-01)에서 랜덤 물건 배출.
using System.Collections;
using UnityEngine;

public class BluePushButton : MonoBehaviour
{
    public Transform plunger;
    public Renderer lamp;
    public ChuteDropper target;
    public float travel = 0.018f;
    public float cooldown = 0.35f;

    Vector3 rest; float last = -10f; Material lampMat;

    void Awake()
    {
        if (plunger) rest = plunger.localPosition;
        if (lamp) lampMat = lamp.material;
    }

    public void Press()
    {
        if (Time.time - last < cooldown) return;
        last = Time.time;
        if (target) target.Drop();
        StopAllCoroutines();
        StartCoroutine(Anim());
    }

    IEnumerator Anim()
    {
        if (lampMat) { lampMat.EnableKeyword("_EMISSION"); lampMat.SetColor("_EmissionColor", new Color(0.2f, 0.7f, 3.0f)); }
        for (float t = 0; t < 1f; t += Time.deltaTime / 0.08f) { Set(t); yield return null; }
        Set(1f);
        yield return new WaitForSeconds(0.12f);
        for (float t = 1f; t > 0f; t -= Time.deltaTime / 0.15f) { Set(t); yield return null; }
        Set(0f);
        yield return new WaitForSeconds(0.25f);
        if (lampMat) lampMat.SetColor("_EmissionColor", Color.black);
    }

    void Set(float k) { if (plunger) plunger.localPosition = rest - Vector3.forward * travel * k; }
}
