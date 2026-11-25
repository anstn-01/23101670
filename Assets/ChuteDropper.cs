// 수직 배출 슈트(VD-01)에서 물품을 1번부터 10번까지 순서대로 하나씩 떨어뜨립니다. Chute_VD01 에 붙습니다.
// 01 그래픽카드 02 키보드 03 카메라 04 운동화 05 붕어빵 봉지 06 책 07 프라이팬 08 AI 스마트 글래스 09 축구공 10 군고구마 봉지
// 10번 다음에는 다시 1번부터. 떨어진 물건은 지워지지 않아요. VR 손으로 잡을 수 있어요.
// (디테일 버전: 둥근 모서리 메쉬, 원뿔/다각기둥 메쉬, 세부 파츠)
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ChuteDropper : MonoBehaviour
{
    public Transform dropPoint;
    public Light outletLight;
    public float cooldown = 0.4f;
    public int nextIndex = 0;            // 다음에 나올 번호(0 = 01번)

    public static readonly string[] Names =
        { "01_GraphicsCard", "02_Keyboard", "03_Camera", "04_Sneakers", "05_BungeoppangBag",
          "06_Book", "07_FryingPan", "08_AISmartGlasses", "09_SoccerBall", "10_SweetPotatoBag" };
    public static readonly string[] Labels =
        { "01  Graphics Card", "02  Keyboard", "03  Camera", "04  Sneakers", "05  Bungeoppang Bag",
          "06  Book", "07  Frying Pan", "08  AI Smart Glasses", "09  Soccer Ball", "10  Sweet Potato Bag" };

    float last = -10f;
    static System.Type grabType; static bool searched;
    static Shader lit;
    static readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();
    static readonly Dictionary<string, Mesh> meshes = new Dictionary<string, Mesh>();

    public GameObject LastItem { get; private set; }

    public void Drop()
    {
        if (Time.time - last < cooldown) return;
        last = Time.time;
        if (dropPoint == null) dropPoint = transform;

        int idx = ((nextIndex % 10) + 10) % 10;
        nextIndex = (idx + 1) % 10;
        var item = MakeItem(idx);

        Vector3 pos = dropPoint.position + Vector3.down * 0.03f + new Vector3(Random.Range(-0.03f, 0.03f), 0, Random.Range(-0.03f, 0.03f));
        Quaternion rot = Quaternion.Euler(0, transform.eulerAngles.y + Random.Range(-25f, 25f), 0) * Quaternion.Euler(Random.Range(-4f, 4f), 0, Random.Range(-4f, 4f));
        item.transform.SetPositionAndRotation(pos, rot);
        var rb = item.GetComponent<Rigidbody>();
        rb.position = pos; rb.rotation = rot;
        rb.linearVelocity = Vector3.down * 0.5f;
        rb.angularVelocity = Random.insideUnitSphere * 1.5f;
        AddGrab(item);

        LastItem = item;
        Debug.Log($"[Drop] {item.name} @ {pos}");
        if (outletLight) { StopAllCoroutines(); StartCoroutine(Flash()); }
    }

    IEnumerator Flash()
    {
        float baseI = outletLight.intensity;
        for (float t = 0; t < 0.5f; t += Time.deltaTime)
        {
            outletLight.intensity = baseI * (1f + 2.5f * Mathf.Sin(t / 0.5f * Mathf.PI));
            yield return null;
        }
        outletLight.intensity = baseI;
    }

    /// 물건만 만들어서 돌려줌(물리 O, 잡기 X). 진열/녹화용으로도 사용.
    public GameObject MakeItem(int i)
    {
        switch (i)
        {
            case 0: return GraphicsCard();
            case 1: return Keyboard();
            case 2: return CameraItem();
            case 3: return Sneakers();
            case 4: return Bungeoppang();
            case 5: return Book();
            case 6: return FryingPan();
            case 7: return SmartGlasses();
            case 8: return SoccerBall();
            default: return SweetPotato();
        }
    }

    static readonly Color Kraft = new Color(0.70f, 0.53f, 0.34f);
    static readonly Color Ink = new Color(0.40f, 0.27f, 0.16f);

    // ===================== 01 그래픽카드 32x13x6 =====================
    GameObject GraphicsCard()
    {
        var g = Root(Names[0], 1.2f, new Vector3(0.32f, 0.06f, 0.13f));
        var t = g.transform;
        var shroud = new Color(0.15f, 0.15f, 0.17f);
        var silver = new Color(0.62f, 0.63f, 0.66f);

        RB(t, new Vector3(0, 0.016f, 0), new Vector3(0.30f, 0.026f, 0.124f), 0.008f, shroud, 0.6f, 0.55f);
        // 쉬라우드 장식 라인
        foreach (float x in new[] { -0.05f, 0.05f })
            RB(t, new Vector3(x, 0.0295f, 0), new Vector3(0.008f, 0.002f, 0.12f), 0.001f, new Color(0.32f, 0.32f, 0.35f), 0.8f, 0.7f, new Vector3(0, 28f * Mathf.Sign(x), 0));
        RB(t, new Vector3(-0.148f, 0.016f, 0), new Vector3(0.006f, 0.028f, 0.124f), 0.003f, new Color(0.25f, 0.25f, 0.28f), 0.7f, 0.6f);
        RB(t, new Vector3(0.148f, 0.016f, 0), new Vector3(0.006f, 0.028f, 0.124f), 0.003f, new Color(0.25f, 0.25f, 0.28f), 0.7f, 0.6f);

        // 팬 3개
        foreach (float fx in new[] { -0.1f, 0f, 0.1f })
        {
            CY(t, new Vector3(fx, 0.0296f, 0), 0.049f, 0.049f, 0.003f, silver, 0.9f, 0.7f);
            CY(t, new Vector3(fx, 0.0302f, 0), 0.0455f, 0.0455f, 0.003f, new Color(0.02f, 0.02f, 0.025f), 0.1f, 0.3f);
            for (int k = 0; k < 11; k++)
            {
                float a = k * 360f / 11f;
                var dir = Quaternion.Euler(0, a, 0) * Vector3.right;
                RB(t, new Vector3(fx, 0.0325f, 0) + dir * 0.026f, new Vector3(0.037f, 0.0012f, 0.013f), 0.0005f, new Color(0.07f, 0.07f, 0.08f), 0.1f, 0.65f, new Vector3(22f, a, 0));
            }
            CY(t, new Vector3(fx, 0.0335f, 0), 0.013f, 0.012f, 0.004f, new Color(0.12f, 0.12f, 0.13f), 0.5f, 0.6f);
            CY(t, new Vector3(fx, 0.0356f, 0), 0.009f, 0.009f, 0.0006f, silver, 1f, 0.85f);
        }

        // RGB 띠 (앞쪽 가장자리, 무지개)
        for (int k = 0; k < 10; k++)
        {
            var c = Color.HSVToRGB(k / 10f, 0.9f, 1f);
            RB(t, new Vector3(-0.135f + k * 0.03f, 0.016f, -0.0625f), new Vector3(0.029f, 0.005f, 0.003f), 0.001f, c, 0, 0.8f, default, c * 3f);
        }
        RB(t, new Vector3(0.03f, 0.0297f, -0.05f), new Vector3(0.05f, 0.002f, 0.004f), 0.001f, new Color(1f, 0.2f, 0.9f), 0, 0.8f, new Vector3(0, -20f, 0), new Color(3f, 0.6f, 2.7f));

        // 방열판 핀 (쉬라우드 아래로 보임)
        for (int k = 0; k < 30; k++)
            BX(t, new Vector3(-0.138f + k * 0.0095f, -0.004f, 0), new Vector3(0.0012f, 0.014f, 0.112f), new Color(0.6f, 0.61f, 0.63f), 0.9f, 0.6f);
        // 히트파이프
        for (int k = 0; k < 3; k++)
            CY(t, new Vector3(-0.1f + k * 0.04f, -0.004f, 0.058f), 0.003f, 0.003f, 0.012f, new Color(0.75f, 0.45f, 0.25f), 1f, 0.8f, new Vector3(90, 0, 0));

        BX(t, new Vector3(0.005f, -0.0185f, -0.002f), new Vector3(0.292f, 0.002f, 0.112f), new Color(0.03f, 0.06f, 0.04f), 0.2f, 0.5f);
        RB(t, new Vector3(0, -0.0235f, 0), new Vector3(0.305f, 0.003f, 0.124f), 0.0015f, new Color(0.22f, 0.22f, 0.24f), 0.85f, 0.65f);
        for (int r = 0; r < 3; r++) for (int k = 0; k < 6; k++)
            BX(t, new Vector3(-0.05f + k * 0.03f, -0.0252f, -0.03f + r * 0.03f), new Vector3(0.022f, 0.0005f, 0.004f), Color.black, 0, 0.2f);

        // 브래킷 + 포트
        BX(t, new Vector3(-0.159f, -0.004f, 0.003f), new Vector3(0.0025f, 0.046f, 0.128f), silver, 0.95f, 0.6f);
        BX(t, new Vector3(-0.158f, 0.02f, 0.003f), new Vector3(0.006f, 0.002f, 0.128f), silver, 0.95f, 0.6f);
        float[] pz = { -0.04f, -0.015f, 0.01f, 0.037f };
        for (int k = 0; k < 4; k++)
            BX(t, new Vector3(-0.1604f, -0.006f, pz[k]), new Vector3(0.0008f, 0.007f, k == 3 ? 0.014f : 0.017f), Color.black, 0, 0.3f);
        for (int k = 0; k < 8; k++)
            BX(t, new Vector3(-0.1604f, 0.011f, -0.045f + k * 0.012f), new Vector3(0.0008f, 0.007f, 0.006f), new Color(0.05f, 0.05f, 0.05f), 0, 0.3f);

        // 보조전원 커넥터 2개
        foreach (float x in new[] { 0.085f, 0.112f })
        {
            RB(t, new Vector3(x, 0.006f, 0.0645f), new Vector3(0.022f, 0.01f, 0.008f), 0.001f, new Color(0.05f, 0.05f, 0.05f), 0, 0.4f);
            for (int k = 0; k < 4; k++)
                BX(t, new Vector3(x - 0.0075f + k * 0.005f, 0.006f, 0.0687f), new Vector3(0.003f, 0.007f, 0.0005f), new Color(0.15f, 0.15f, 0.15f), 0, 0.3f);
        }
        // PCIe 금색 단자
        BX(t, new Vector3(-0.068f, -0.0185f, -0.062f), new Vector3(0.09f, 0.002f, 0.012f), new Color(0.03f, 0.06f, 0.04f), 0.2f, 0.5f);
        for (int k = 0; k < 24; k++)
            BX(t, new Vector3(-0.11f + k * 0.0037f, -0.0185f, -0.065f), new Vector3(0.0024f, 0.0024f, 0.007f), new Color(0.9f, 0.7f, 0.25f), 1f, 0.85f);
        return g;
    }

    // ===================== 02 키보드 44x14x4 (텐키리스) =====================
    GameObject Keyboard()
    {
        var g = Root(Names[1], 0.9f, new Vector3(0.44f, 0.04f, 0.14f));
        var t = g.transform;
        RB(t, new Vector3(0, -0.009f, 0), new Vector3(0.44f, 0.024f, 0.14f), 0.006f, new Color(0.07f, 0.07f, 0.08f), 0.5f, 0.55f);
        RB(t, new Vector3(0, 0.0032f, 0), new Vector3(0.412f, 0.002f, 0.128f), 0.003f, new Color(0.02f, 0.02f, 0.03f), 0.3f, 0.3f);

        float p = 0.021f, x0 = -0.389f * 0.5f, zF = 0.054f;
        float[][] rows =
        {
            new float[] { 1, -1, 1, 1, 1, 1, -0.5f, 1, 1, 1, 1, -0.5f, 1, 1, 1, 1, -0.5f, 1, 1, 1 },
            new float[] { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 2, -0.5f, 1, 1, 1 },
            new float[] { 1.5f, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1.5f, -0.5f, 1, 1, 1 },
            new float[] { 1.75f, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 2.25f },
            new float[] { 2.25f, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 2.75f, -1.5f, 1 },
            new float[] { 1.25f, 1.25f, 1.25f, 6.25f, 1.25f, 1.25f, 1.25f, 1.25f, -0.5f, 1, 1, 1 },
        };
        for (int r = 0; r < rows.Length; r++)
        {
            float z = r == 0 ? zF : zF - (r + 0.25f) * p;
            float cx = x0;
            foreach (float wu in rows[r])
            {
                float w = Mathf.Abs(wu) * p;
                if (wu > 0)
                {
                    float x = cx + w * 0.5f;
                    float hue = Mathf.Lerp(0.55f, 0.93f, Mathf.InverseLerp(-0.2f, 0.2f, x) * 0.85f + r * 0.03f);
                    hue = Mathf.Round(hue * 24f) / 24f;
                    var col = Color.HSVToRGB(hue, 0.85f, 1f);
                    var tilt = new Vector3(-3f + r * 1.2f, 0, 0);
                    RB(t, new Vector3(x, 0.008f, z), new Vector3(w - 0.003f, 0.009f, p - 0.003f), 0.002f, new Color(0.11f, 0.11f, 0.13f), 0, 0.45f, tilt, col * 0.35f);
                    RB(t, new Vector3(x, 0.0131f, z), new Vector3(w - 0.0075f, 0.0016f, p - 0.0075f), 0.0012f, new Color(0.14f, 0.14f, 0.17f), 0, 0.6f, tilt, col * 0.18f);
                    BX(t, new Vector3(x - (w > p * 1.1f ? w * 0.25f : 0f), 0.0141f, z + 0.002f), new Vector3(0.0045f, 0.0003f, 0.004f), col, 0, 0.8f, tilt, col * 2.2f);
                }
                cx += w;
            }
        }
        // 측면 RGB 라이트바 + 바닥 받침 + USB-C
        for (int k = 0; k < 8; k++)
        {
            var c = Color.HSVToRGB(Mathf.Lerp(0.55f, 0.93f, k / 7f), 0.85f, 1f);
            RB(t, new Vector3(-0.19f + k * 0.0545f, -0.014f, -0.0705f), new Vector3(0.053f, 0.003f, 0.002f), 0.001f, c, 0, 0.8f, default, c * 2.5f);
        }
        foreach (int sx in new[] { -1, 1 }) foreach (int sz in new[] { -1, 1 })
            RB(t, new Vector3(sx * 0.19f, -0.0215f, sz * 0.05f), new Vector3(0.025f, 0.002f, 0.012f), 0.001f, Color.black, 0, 0.2f);
        RB(t, new Vector3(0.05f, -0.008f, 0.0705f), new Vector3(0.01f, 0.004f, 0.002f), 0.0015f, Color.black, 0, 0.5f);
        return g;
    }

    // ===================== 03 카메라 13x10x9 =====================
    GameObject CameraItem()
    {
        var g = Root(Names[2], 0.7f, new Vector3(0.13f, 0.1f, 0.09f), new Vector3(0, 0, -0.02f));
        var t = g.transform;
        var body = new Color(0.09f, 0.09f, 0.1f);
        var rubber = new Color(0.045f, 0.045f, 0.05f);
        var metal = new Color(0.22f, 0.22f, 0.24f);
        var silver = new Color(0.7f, 0.7f, 0.72f);

        RB(t, new Vector3(0, -0.006f, 0.006f), new Vector3(0.13f, 0.072f, 0.04f), 0.008f, body, 0.25f, 0.45f);
        RB(t, new Vector3(0.008f, -0.008f, -0.0142f), new Vector3(0.1f, 0.058f, 0.002f), 0.002f, rubber, 0, 0.15f);
        RB(t, new Vector3(-0.049f, -0.008f, -0.006f), new Vector3(0.034f, 0.07f, 0.052f), 0.013f, rubber, 0, 0.2f);
        // 뷰파인더 혹
        RB(t, new Vector3(0.006f, 0.04f, 0.008f), new Vector3(0.046f, 0.026f, 0.04f), 0.007f, body, 0.25f, 0.45f);
        RB(t, new Vector3(0.006f, 0.04f, 0.03f), new Vector3(0.032f, 0.022f, 0.008f), 0.006f, rubber, 0, 0.2f);
        BX(t, new Vector3(0.006f, 0.04f, 0.0342f), new Vector3(0.02f, 0.012f, 0.0005f), new Color(0.01f, 0.01f, 0.02f), 0.5f, 0.95f);
        BX(t, new Vector3(0.006f, 0.047f, -0.0121f), new Vector3(0.022f, 0.0035f, 0.0004f), new Color(0.85f, 0.85f, 0.85f), 0, 0.5f);
        RB(t, new Vector3(0.006f, 0.0545f, 0.008f), new Vector3(0.022f, 0.003f, 0.018f), 0.0008f, silver, 0.95f, 0.7f);
        // 다이얼(톱니 무늬)
        Dial(t, new Vector3(0.043f, 0.035f, 0.004f), 0.011f, 0.006f, metal, silver);
        Dial(t, new Vector3(-0.034f, 0.033f, 0.008f), 0.0095f, 0.005f, metal, silver);
        CY(t, new Vector3(-0.051f, 0.0275f, -0.014f), 0.0075f, 0.0075f, 0.002f, metal, 0.7f, 0.5f);
        CY(t, new Vector3(-0.051f, 0.0295f, -0.014f), 0.0055f, 0.005f, 0.003f, silver, 0.95f, 0.75f);
        // 뒷면 LCD + 버튼
        RB(t, new Vector3(0.006f, -0.008f, 0.0265f), new Vector3(0.078f, 0.052f, 0.003f), 0.003f, new Color(0.04f, 0.04f, 0.045f), 0.2f, 0.6f);
        BX(t, new Vector3(0.006f, -0.008f, 0.0282f), new Vector3(0.07f, 0.045f, 0.0006f), new Color(0.02f, 0.03f, 0.05f), 0.4f, 0.97f, default, new Color(0.03f, 0.05f, 0.09f));
        for (int k = 0; k < 4; k++)
            CY(t, new Vector3(0.056f, 0.012f - k * 0.012f, 0.0265f), 0.0035f, 0.0035f, 0.003f, metal, 0.6f, 0.5f, new Vector3(90, 0, 0));
        CY(t, new Vector3(0.056f, -0.034f, 0.0265f), 0.008f, 0.008f, 0.003f, metal, 0.6f, 0.5f, new Vector3(90, 0, 0));
        // 스트랩 고리
        foreach (int s in new[] { -1, 1 })
            RB(t, new Vector3(s * 0.066f, 0.024f, 0.006f), new Vector3(0.004f, 0.01f, 0.006f), 0.0015f, silver, 0.95f, 0.6f);

        // 렌즈 (-Z 방향)
        var lr = new Vector3(90, 0, 0);
        CY(t, new Vector3(0.012f, -0.008f, -0.0165f), 0.033f, 0.033f, 0.004f, silver, 0.95f, 0.7f, lr);
        CY(t, new Vector3(0.012f, -0.008f, -0.025f), 0.031f, 0.031f, 0.013f, body, 0.3f, 0.45f, lr);
        CY(t, new Vector3(0.012f, -0.008f, -0.0285f), 0.0318f, 0.0318f, 0.005f, new Color(0.13f, 0.13f, 0.14f), 0.5f, 0.5f, lr);
        BX(t, new Vector3(0.012f, 0.0238f, -0.0285f), new Vector3(0.0012f, 0.0006f, 0.004f), Color.white, 0, 0.4f);
        CY(t, new Vector3(0.012f, -0.008f, -0.046f), 0.0325f, 0.0325f, 0.018f, rubber, 0, 0.15f, lr);
        for (int k = 0; k < 40; k++)
        {
            float a = k * 9f * Mathf.Deg2Rad;
            var p = new Vector3(0.012f + Mathf.Cos(a) * 0.0328f, -0.008f + Mathf.Sin(a) * 0.0328f, -0.046f);
            BX(t, p, new Vector3(0.0018f, 0.0018f, 0.016f), new Color(0.07f, 0.07f, 0.075f), 0, 0.2f, new Vector3(0, 0, k * 9f));
        }
        CY(t, new Vector3(0.012f, -0.008f, -0.06f), 0.031f, 0.031f, 0.01f, body, 0.3f, 0.45f, lr);
        CY(t, new Vector3(0.012f, -0.008f, -0.0655f), 0.0322f, 0.0322f, 0.0018f, new Color(0.78f, 0.08f, 0.06f), 0.3f, 0.6f, lr);
        CY(t, new Vector3(0.012f, -0.008f, -0.0675f), 0.031f, 0.027f, 0.003f, body, 0.3f, 0.4f, lr);
        SP(t, new Vector3(0.012f, -0.008f, -0.067f), new Vector3(0.05f, 0.05f, 0.012f), new Color(0.02f, 0.03f, 0.07f), 0.5f, 0.98f);
        CY(t, new Vector3(0.012f, -0.008f, -0.0728f), 0.012f, 0.012f, 0.0004f, new Color(0.15f, 0.05f, 0.3f), 0.3f, 0.95f, lr, new Color(0.08f, 0.02f, 0.18f));
        for (int k = 0; k < 3; k++)
            BX(t, new Vector3(0.012f + (k - 1) * 0.008f, 0.0215f, -0.06f), new Vector3(0.005f, 0.0006f, 0.0015f), new Color(0.8f, 0.8f, 0.8f), 0, 0.4f);
        return g;
    }

    void Dial(Transform t, Vector3 pos, float r, float h, Color metal, Color top)
    {
        CY(t, pos, r, r, h, metal, 0.7f, 0.5f);
        for (int k = 0; k < 18; k++)
        {
            float a = k * 20f;
            var d = Quaternion.Euler(0, a, 0) * Vector3.forward;
            BX(t, pos + d * r, new Vector3(0.0012f, h * 0.9f, 0.002f), new Color(0.1f, 0.1f, 0.11f), 0.5f, 0.4f, new Vector3(0, a, 0));
        }
        CY(t, pos + Vector3.up * (h * 0.5f + 0.0002f), r * 0.8f, r * 0.8f, 0.0004f, top, 0.9f, 0.7f);
        BX(t, pos + Vector3.up * (h * 0.5f + 0.0005f) + Vector3.forward * r * 0.5f, new Vector3(0.0012f, 0.0003f, r * 0.6f), Color.white, 0, 0.4f);
    }

    // ===================== 04 운동화(한 켤레) 30x22x12 =====================
    GameObject Sneakers()
    {
        var g = Root(Names[3], 0.8f, new Vector3(0.22f, 0.12f, 0.30f));
        Shoe(g.transform, new Vector3(-0.056f, 0, -0.004f), -4f);
        Shoe(g.transform, new Vector3(0.056f, 0, 0.004f), 5f);
        return g;
    }

    void Shoe(Transform parent, Vector3 pos, float yaw)
    {
        var s = new GameObject("Shoe").transform;
        s.SetParent(parent, false);
        s.localPosition = pos;
        s.localRotation = Quaternion.Euler(0, yaw, 0);
        var navy = new Color(0.13f, 0.15f, 0.27f);
        var grey = new Color(0.8f, 0.81f, 0.84f);
        var white = new Color(0.95f, 0.94f, 0.9f);

        RB(s, new Vector3(0, -0.056f, 0), new Vector3(0.096f, 0.008f, 0.292f), 0.004f, new Color(0.76f, 0.55f, 0.3f), 0, 0.3f);
        RB(s, new Vector3(0, -0.042f, 0), new Vector3(0.094f, 0.022f, 0.288f), 0.009f, white, 0, 0.35f);
        BX(s, new Vector3(0, -0.037f, 0), new Vector3(0.0952f, 0.0012f, 0.27f), new Color(0.7f, 0.7f, 0.7f), 0, 0.3f);
        RB(s, new Vector3(0, -0.0005f, -0.04f), new Vector3(0.088f, 0.062f, 0.19f), 0.022f, navy, 0, 0.4f);
        SP(s, new Vector3(0, -0.016f, 0.074f), new Vector3(0.088f, 0.056f, 0.135f), grey, 0, 0.4f);
        SP(s, new Vector3(0, -0.026f, 0.092f), new Vector3(0.091f, 0.034f, 0.1f), grey * 0.95f, 0, 0.35f);
        // 옆면 덧댐 + 스우시 모양 곡선
        foreach (int sd in new[] { -1, 1 })
        {
            RB(s, new Vector3(sd * 0.0442f, -0.012f, 0.005f), new Vector3(0.002f, 0.032f, 0.1f), 0.001f, grey, 0, 0.4f);
            Vector3[] pts = { new Vector3(0, -0.018f, -0.105f), new Vector3(0, -0.022f, -0.06f), new Vector3(0, -0.016f, -0.015f), new Vector3(0, -0.004f, 0.03f), new Vector3(0, 0.008f, 0.06f) };
            for (int k = 0; k < pts.Length - 1; k++)
            {
                var a = pts[k]; var b = pts[k + 1];
                var mid = (a + b) * 0.5f; mid.x = sd * 0.0456f;
                var d = b - a;
                float ang = Mathf.Atan2(d.y, d.z) * Mathf.Rad2Deg;
                RB(s, mid, new Vector3(0.0015f, 0.012f - k * 0.002f, d.magnitude + 0.004f), 0.0006f, new Color(0.62f, 0.64f, 0.69f), 0, 0.5f, new Vector3(-ang, 0, 0));
            }
        }
        // 뒤꿈치, 칼라 패딩, 힐탭
        RB(s, new Vector3(0, -0.002f, -0.118f), new Vector3(0.09f, 0.05f, 0.04f), 0.016f, new Color(0.2f, 0.22f, 0.33f), 0, 0.4f);
        RB(s, new Vector3(0, 0.032f, -0.108f), new Vector3(0.082f, 0.016f, 0.055f), 0.008f, white, 0, 0.3f);
        RB(s, new Vector3(0, 0.038f, -0.138f), new Vector3(0.022f, 0.026f, 0.006f), 0.003f, white, 0, 0.3f);
        // 혀
        RB(s, new Vector3(0, 0.044f, -0.035f), new Vector3(0.05f, 0.008f, 0.07f), 0.004f, navy, 0, 0.4f, new Vector3(-28f, 0, 0));
        RB(s, new Vector3(0, 0.0495f, -0.05f), new Vector3(0.018f, 0.0008f, 0.012f), 0.0005f, white, 0, 0.3f, new Vector3(-28f, 0, 0));
        // 끈 구멍 + 교차 끈
        for (int k = 0; k < 6; k++)
        {
            float z = -0.06f + k * 0.017f, y = 0.031f - k * 0.0055f;
            foreach (int sd in new[] { -1, 1 })
                CY(s, new Vector3(sd * 0.024f, y, z), 0.003f, 0.003f, 0.002f, new Color(0.25f, 0.25f, 0.28f), 0.8f, 0.6f);
            if (k < 5)
                foreach (int sd in new[] { -1, 1 })
                    RB(s, new Vector3(0, y + 0.002f, z + 0.0085f), new Vector3(0.054f, 0.0025f, 0.004f), 0.001f, white, 0, 0.3f, new Vector3(0, sd * 18f, 0));
        }
        SP(s, new Vector3(0, 0.038f, -0.065f), new Vector3(0.012f, 0.006f, 0.008f), white, 0, 0.3f);
        RB(s, new Vector3(0.014f, 0.034f, -0.075f), new Vector3(0.006f, 0.003f, 0.03f), 0.001f, white, 0, 0.3f, new Vector3(20f, 25f, 0));
        RB(s, new Vector3(-0.014f, 0.034f, -0.075f), new Vector3(0.006f, 0.003f, 0.03f), 0.001f, white, 0, 0.3f, new Vector3(20f, -25f, 0));
    }

    // ===================== 05 붕어빵 봉지 18x12x20 =====================
    GameObject Bungeoppang()
    {
        var g = Root(Names[4], 0.5f, new Vector3(0.18f, 0.22f, 0.12f), new Vector3(0, 0.01f, 0));
        var t = g.transform;
        Bag(t, 0.18f, 0.17f, 0.12f);
        float[] xs = { -0.052f, -0.017f, 0.018f, 0.053f };
        for (int k = 0; k < 4; k++)
        {
            var f = new GameObject("Fish").transform;
            f.SetParent(t, false);
            f.localPosition = new Vector3(xs[k], 0.06f + (k % 2) * 0.012f, (k % 2 == 0 ? -0.016f : 0.016f));
            f.localRotation = Quaternion.Euler(0, 90f + (k % 2) * 180f, 72f + k * 5f);
            Fish(f);
        }
        Stamp(t, 0.12f, true);
        return g;
    }

    void Fish(Transform f)
    {
        var gold = new Color(0.9f, 0.58f, 0.2f);
        var crust = new Color(0.62f, 0.33f, 0.09f);
        SP(f, Vector3.zero, new Vector3(0.106f, 0.064f, 0.022f), crust, 0, 0.45f);
        SP(f, new Vector3(0, 0, 0), new Vector3(0.1f, 0.058f, 0.029f), gold, 0, 0.55f);
        SP(f, new Vector3(0.03f, 0.002f, 0), new Vector3(0.05f, 0.052f, 0.031f), gold, 0, 0.55f);
        // 꼬리
        RB(f, new Vector3(-0.06f, 0.008f, 0), new Vector3(0.03f, 0.016f, 0.012f), 0.005f, gold, 0, 0.5f, new Vector3(0, 0, 35f));
        RB(f, new Vector3(-0.06f, -0.008f, 0), new Vector3(0.03f, 0.016f, 0.012f), 0.005f, gold, 0, 0.5f, new Vector3(0, 0, -35f));
        for (int k = -1; k <= 1; k++)
            BX(f, new Vector3(-0.066f, k * 0.008f, 0), new Vector3(0.018f, 0.0015f, 0.0135f), crust, 0, 0.4f, new Vector3(0, 0, k * 35f));
        // 지느러미
        RB(f, new Vector3(-0.005f, 0.031f, 0), new Vector3(0.036f, 0.01f, 0.009f), 0.004f, gold, 0, 0.5f, new Vector3(0, 0, -8f));
        RB(f, new Vector3(0.005f, -0.03f, 0), new Vector3(0.022f, 0.008f, 0.008f), 0.003f, gold, 0, 0.5f, new Vector3(0, 0, 10f));
        // 비늘 + 눈 + 입 (양면)
        foreach (int sd in new[] { -1, 1 })
        {
            for (int r = 0; r < 3; r++) for (int c = 0; c < 4; c++)
                SP(f, new Vector3(-0.035f + c * 0.016f + (r % 2) * 0.008f, -0.014f + r * 0.014f, sd * 0.0135f), new Vector3(0.012f, 0.009f, 0.004f), new Color(0.82f, 0.5f, 0.15f), 0, 0.55f);
            SP(f, new Vector3(0.04f, 0.012f, sd * 0.0148f), new Vector3(0.008f, 0.008f, 0.003f), new Color(0.3f, 0.15f, 0.05f), 0, 0.5f);
            BX(f, new Vector3(0.052f, -0.006f, sd * 0.0145f), new Vector3(0.008f, 0.0012f, 0.001f), crust, 0, 0.4f, new Vector3(0, 0, -20f));
            BX(f, new Vector3(0.02f, 0f, sd * 0.0152f), new Vector3(0.0015f, 0.03f, 0.001f), crust, 0, 0.4f, new Vector3(0, 0, -15f));
        }
    }

    // ===================== 06 책 15x22x3 =====================
    GameObject Book()
    {
        var g = Root(Names[5], 0.6f, new Vector3(0.15f, 0.03f, 0.22f));
        var t = g.transform;
        var navy = new Color(0.14f, 0.17f, 0.32f);
        var gold = new Color(0.85f, 0.68f, 0.3f);
        RB(t, new Vector3(0.001f, -0.0135f, 0), new Vector3(0.148f, 0.003f, 0.22f), 0.0015f, navy, 0, 0.35f);
        RB(t, new Vector3(0.001f, 0.0135f, 0), new Vector3(0.148f, 0.003f, 0.22f), 0.0015f, navy, 0, 0.35f);
        CY(t, new Vector3(-0.071f, 0, 0), 0.0152f, 0.0152f, 0.22f, navy, 0, 0.35f, new Vector3(90, 0, 0), 24, 0.45f);
        foreach (float z in new[] { -0.075f, -0.025f, 0.025f, 0.075f })
            CY(t, new Vector3(-0.0715f, 0, z), 0.0158f, 0.0158f, 0.004f, navy * 0.8f, 0, 0.35f, new Vector3(90, 0, 0), 24, 0.45f);
        RB(t, new Vector3(0.004f, 0, 0), new Vector3(0.142f, 0.024f, 0.212f), 0.001f, new Color(0.93f, 0.88f, 0.74f), 0, 0.2f);
        for (int k = 0; k < 9; k++)
        {
            float y = -0.0105f + k * 0.0026f;
            BX(t, new Vector3(0.0752f, y, 0), new Vector3(0.0006f, 0.0004f, 0.21f), new Color(0.8f, 0.75f, 0.62f), 0, 0.2f);
            BX(t, new Vector3(0.004f, y, 0.1062f), new Vector3(0.14f, 0.0004f, 0.0006f), new Color(0.8f, 0.75f, 0.62f), 0, 0.2f);
            BX(t, new Vector3(0.004f, y, -0.1062f), new Vector3(0.14f, 0.0004f, 0.0006f), new Color(0.8f, 0.75f, 0.62f), 0, 0.2f);
        }
        BX(t, new Vector3(-0.064f, 0, 0.1058f), new Vector3(0.006f, 0.022f, 0.002f), new Color(0.7f, 0.15f, 0.15f), 0, 0.4f);
        BX(t, new Vector3(-0.064f, 0, -0.1058f), new Vector3(0.006f, 0.022f, 0.002f), new Color(0.7f, 0.15f, 0.15f), 0, 0.4f);
        // 표지 라벨 + 금박 테두리
        RB(t, new Vector3(0.008f, 0.0152f, -0.055f), new Vector3(0.075f, 0.0008f, 0.036f), 0.0005f, new Color(0.84f, 0.7f, 0.52f), 0, 0.3f);
        BX(t, new Vector3(0.008f, 0.0157f, -0.062f), new Vector3(0.05f, 0.0003f, 0.004f), Ink, 0, 0.3f);
        BX(t, new Vector3(0.008f, 0.0157f, -0.05f), new Vector3(0.035f, 0.0003f, 0.0025f), Ink, 0, 0.3f);
        BX(t, new Vector3(0.008f, 0.0151f, 0.098f), new Vector3(0.12f, 0.0003f, 0.0015f), gold, 0.9f, 0.7f);
        BX(t, new Vector3(0.008f, 0.0151f, -0.098f), new Vector3(0.12f, 0.0003f, 0.0015f), gold, 0.9f, 0.7f);
        BX(t, new Vector3(-0.052f, 0.0151f, 0), new Vector3(0.0015f, 0.0003f, 0.196f), gold, 0.9f, 0.7f);
        BX(t, new Vector3(0.068f, 0.0151f, 0), new Vector3(0.0015f, 0.0003f, 0.196f), gold, 0.9f, 0.7f);
        // 책갈피 끈
        RB(t, new Vector3(-0.035f, -0.004f, 0.122f), new Vector3(0.009f, 0.0008f, 0.03f), 0.0003f, gold, 0.3f, 0.6f, new Vector3(-25f, 12f, 0));
        return g;
    }

    // ===================== 07 프라이팬 45x28x6 =====================
    GameObject FryingPan()
    {
        float px = -0.085f;
        var g = Root(Names[6], 1.1f, new Vector3(0.28f, 0.05f, 0.28f), new Vector3(px, -0.005f, 0));
        var t = g.transform;
        var hc = g.AddComponent<BoxCollider>(); hc.center = new Vector3(0.14f, 0.008f, 0); hc.size = new Vector3(0.17f, 0.02f, 0.03f);
        var red = new Color(0.72f, 0.1f, 0.08f);
        var silver = new Color(0.75f, 0.76f, 0.78f);

        CY(t, new Vector3(px, -0.0035f, 0), 0.11f, 0.14f, 0.045f, red, 0.3f, 0.65f, default, 48, -1f, false);
        CY(t, new Vector3(px, -0.003f, 0), 0.106f, 0.136f, 0.044f, new Color(0.07f, 0.07f, 0.08f), 0.2f, 0.7f, default, 48, -1f, false, true);
        CY(t, new Vector3(px, -0.026f, 0), 0.11f, 0.11f, 0.002f, new Color(0.32f, 0.32f, 0.34f), 0.85f, 0.5f, default, 48);
        foreach (float r in new[] { 0.05f, 0.075f, 0.095f })
            CY(t, new Vector3(px, -0.0272f, 0), r, r, 0.0004f, new Color(0.45f, 0.45f, 0.47f), 0.9f, 0.6f, default, 40);
        CY(t, new Vector3(px, -0.0245f, 0), 0.106f, 0.106f, 0.001f, new Color(0.08f, 0.08f, 0.09f), 0.2f, 0.75f, default, 48);
        for (int k = 0; k < 40; k++)
        {
            float a = k * 137.5f * Mathf.Deg2Rad, rr = 0.1f * Mathf.Sqrt((k + 0.5f) / 40f);
            SP(t, new Vector3(px + Mathf.Cos(a) * rr, -0.0238f, Mathf.Sin(a) * rr), new Vector3(0.003f, 0.0004f, 0.003f), new Color(0.22f, 0.22f, 0.24f), 0.3f, 0.6f);
        }
        CY(t, new Vector3(px, -0.0236f, 0), 0.02f, 0.02f, 0.0004f, new Color(0.25f, 0.25f, 0.27f), 0.6f, 0.6f);
        // 테두리 립
        for (int k = 0; k < 48; k++)
        {
            float a = k * 7.5f;
            var d = Quaternion.Euler(0, a, 0) * Vector3.right;
            BX(t, new Vector3(px, 0.019f, 0) + d * 0.139f, new Vector3(0.004f, 0.003f, 0.0192f), silver, 0.95f, 0.75f, new Vector3(0, a, 0));
        }
        // 손잡이
        RB(t, new Vector3(0.062f, 0.004f, 0), new Vector3(0.04f, 0.016f, 0.03f), 0.005f, silver, 0.95f, 0.7f, new Vector3(0, 0, 8f));
        foreach (int sz in new[] { -1, 1 })
            SP(t, new Vector3(px + 0.131f, 0.002f, sz * 0.008f), Vector3.one * 0.005f, silver, 0.95f, 0.7f);
        RB(t, new Vector3(0.09f, 0.008f, 0), new Vector3(0.016f, 0.021f, 0.032f), 0.006f, red, 0.3f, 0.65f, new Vector3(0, 0, 7f));
        RB(t, new Vector3(0.158f, 0.016f, 0), new Vector3(0.13f, 0.018f, 0.029f), 0.009f, new Color(0.15f, 0.15f, 0.17f), 0.2f, 0.5f, new Vector3(0, 0, 7f));
        CY(t, new Vector3(0.202f, 0.0215f, 0), 0.0055f, 0.0055f, 0.0195f, Color.black, 0, 0.2f, new Vector3(0, 0, 7f));
        return g;
    }

    // ===================== 08 AI 스마트 글래스 15x5x4 =====================
    GameObject SmartGlasses()
    {
        var g = Root(Names[7], 0.08f, new Vector3(0.15f, 0.05f, 0.04f));
        var t = g.transform;
        var fr = new Color(0.04f, 0.04f, 0.05f);
        var silver = new Color(0.7f, 0.7f, 0.72f);
        RB(t, new Vector3(0, 0.017f, -0.012f), new Vector3(0.15f, 0.012f, 0.012f), 0.005f, fr, 0.3f, 0.8f);
        foreach (int s in new[] { -1, 1 })
        {
            RB(t, new Vector3(s * 0.037f, -0.003f, -0.012f), new Vector3(0.064f, 0.045f, 0.009f), 0.013f, fr, 0.3f, 0.8f);
            RB(t, new Vector3(s * 0.037f, -0.004f, -0.0165f), new Vector3(0.054f, 0.035f, 0.0025f), 0.01f, new Color(0.05f, 0.07f, 0.09f), 0.5f, 0.97f);
            RB(t, new Vector3(s * 0.037f, -0.004f, -0.0075f), new Vector3(0.054f, 0.035f, 0.0015f), 0.01f, new Color(0.06f, 0.08f, 0.1f), 0.5f, 0.97f);
            SP(t, new Vector3(s * 0.009f, -0.009f, -0.005f), new Vector3(0.008f, 0.012f, 0.004f), new Color(0.6f, 0.62f, 0.65f), 0.1f, 0.9f);
            RB(t, new Vector3(s * 0.073f, 0.012f, -0.008f), new Vector3(0.008f, 0.01f, 0.012f), 0.002f, silver, 0.95f, 0.7f);
            // 접힌 다리 + 테크 모듈 + 터치패드
            RB(t, new Vector3(s * 0.006f, 0.012f - (s > 0 ? 0f : 0.005f), 0.002f + (s > 0 ? 0f : 0.006f)), new Vector3(0.128f, 0.009f, 0.005f), 0.0022f, fr, 0.3f, 0.8f, new Vector3(0, s * 2f, 0));
            RB(t, new Vector3(s * 0.045f, 0.012f - (s > 0 ? 0f : 0.005f), 0.002f + (s > 0 ? 0f : 0.006f)), new Vector3(0.035f, 0.012f, 0.008f), 0.0035f, new Color(0.08f, 0.08f, 0.09f), 0.4f, 0.75f);
        }
        RB(t, new Vector3(0, 0.007f, -0.012f), new Vector3(0.016f, 0.008f, 0.008f), 0.003f, fr, 0.3f, 0.8f);
        // 카메라 렌즈 / LED / HUD
        CY(t, new Vector3(0.067f, 0.017f, -0.0185f), 0.0038f, 0.0038f, 0.0015f, silver, 0.95f, 0.7f, new Vector3(90, 0, 0));
        CY(t, new Vector3(0.067f, 0.017f, -0.0193f), 0.0026f, 0.0026f, 0.0012f, new Color(0.02f, 0.02f, 0.05f), 0.6f, 0.98f, new Vector3(90, 0, 0));
        SP(t, new Vector3(-0.067f, 0.017f, -0.0185f), Vector3.one * 0.004f, new Color(0.9f, 0.2f, 1f), 0, 0.9f, default, new Color(3f, 0.6f, 3f));
        RB(t, new Vector3(0.037f, -0.006f, -0.0181f), new Vector3(0.024f, 0.015f, 0.0004f), 0.001f, new Color(0.1f, 0.6f, 0.8f), 0, 0.9f, default, new Color(0.3f, 1.8f, 2.4f));
        for (int k = 0; k < 4; k++)
            BX(t, new Vector3(0.03f + k * 0.0045f, -0.0075f + (k % 2) * 0.002f, -0.0184f), new Vector3(0.0025f, 0.006f + (k % 2) * 0.003f, 0.0003f), new Color(0.6f, 1f, 1f), 0, 0.9f, default, new Color(1.5f, 3f, 3f));
        return g;
    }

    // ===================== 09 축구공 지름 22 =====================
    GameObject SoccerBall()
    {
        var g = new GameObject("Item_" + Names[8]);
        var rb = g.AddComponent<Rigidbody>();
        rb.mass = 0.43f; rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        var sc = g.AddComponent<SphereCollider>(); sc.radius = 0.11f;
        sc.material = new PhysicsMaterial { bounciness = 0.65f, bounceCombine = PhysicsMaterialCombine.Maximum };
        var t = g.transform;
        SP(t, Vector3.zero, Vector3.one * 0.22f, new Color(0.95f, 0.95f, 0.93f), 0, 0.6f);

        float ph = (1f + Mathf.Sqrt(5f)) * 0.5f;
        var v = new List<Vector3>();
        foreach (int a in new[] { -1, 1 }) foreach (int b in new[] { -1, 1 })
        {
            v.Add(new Vector3(0, a, b * ph).normalized); v.Add(new Vector3(a, b * ph, 0).normalized); v.Add(new Vector3(b * ph, 0, a).normalized);
        }
        var pent = Prism(5, 0.0378f, 0.006f);
        var black = new Color(0.05f, 0.05f, 0.06f);
        var seam = new Color(0.55f, 0.55f, 0.55f);
        for (int i = 0; i < v.Count; i++)
        {
            Vector3 P = v[i];
            Vector3 Q = Vector3.zero; float best = -2f;
            for (int j = 0; j < v.Count; j++) { if (j == i) continue; float d = Vector3.Dot(P, v[j]); if (d > best) { best = d; Q = v[j]; } }
            Vector3 tan = (Q - P * Vector3.Dot(Q, P)).normalized;
            MeshPart(t, "Pentagon", pent, P * 0.1082f, Quaternion.LookRotation(tan, P), Vector3.one, black, 0, 0.6f);
            for (int j = i + 1; j < v.Count; j++)
            {
                if (Vector3.Dot(P, v[j]) < 0.4f) continue;   // 이웃만 (이코사헤드론 모서리)
                Vector3 m = (P + v[j]).normalized;
                Vector3 dir = (v[j] - P).normalized;
                var go = BX(t, m * 0.1101f, new Vector3(0.0015f, 0.0008f, 0.046f), seam, 0, 0.4f);
                go.transform.localRotation = Quaternion.LookRotation(dir, m);
            }
        }
        // 육각형 면 경계 (오각형 사이 이음선)
        for (int i = 0; i < v.Count; i++)
            for (int j = i + 1; j < v.Count; j++)
                for (int k = j + 1; k < v.Count; k++)
                {
                    if (Vector3.Dot(v[i], v[j]) < 0.4f || Vector3.Dot(v[j], v[k]) < 0.4f || Vector3.Dot(v[i], v[k]) < 0.4f) continue;
                    Vector3 c = (v[i] + v[j] + v[k]).normalized;
                    SP(t, c * 0.1093f, new Vector3(0.006f, 0.006f, 0.001f), new Color(0.88f, 0.88f, 0.86f), 0, 0.5f).transform.localRotation = Quaternion.LookRotation(c);
                }
        return g;
    }

    // ===================== 10 군고구마 봉지 18x12x22 =====================
    GameObject SweetPotato()
    {
        var g = Root(Names[9], 0.7f, new Vector3(0.18f, 0.22f, 0.12f));
        var t = g.transform;
        Bag(t, 0.18f, 0.17f, 0.12f);
        for (int k = 0; k < 4; k++)
        {
            var p = new GameObject("Potato").transform;
            p.SetParent(t, false);
            p.localPosition = new Vector3(-0.05f + k * 0.033f, 0.058f + (k % 2) * 0.012f, (k % 2 == 0 ? -0.018f : 0.016f));
            p.localRotation = Quaternion.Euler(k * 20f, 30f * k, 65f + k * 8f);
            Potato(p, k == 1 || k == 3, k);
        }
        Stamp(t, 0.12f, false);
        return g;
    }

    void Potato(Transform p, bool split, int seed)
    {
        var skin = new Color(0.45f, 0.12f, 0.2f);
        var dark = new Color(0.25f, 0.06f, 0.1f);
        var inside = new Color(0.98f, 0.68f, 0.18f);
        if (!split)
        {
            SP(p, Vector3.zero, new Vector3(0.11f, 0.05f, 0.05f), skin, 0, 0.3f);
            SP(p, new Vector3(0.05f, 0.002f, 0), new Vector3(0.045f, 0.032f, 0.03f), skin, 0, 0.3f);
            SP(p, new Vector3(-0.048f, -0.001f, 0), new Vector3(0.04f, 0.03f, 0.028f), skin, 0, 0.3f);
            BX(p, new Vector3(0.078f, 0.002f, 0), new Vector3(0.02f, 0.003f, 0.003f), dark, 0, 0.3f, new Vector3(0, 0, 8f));
        }
        else
        {
            // 반으로 쪼갠 고구마: 껍질 반쪽 + 노란 속
            SP(p, new Vector3(-0.005f, 0, 0), new Vector3(0.11f, 0.05f, 0.05f), skin, 0, 0.3f);
            SP(p, new Vector3(-0.003f, 0.012f, 0), new Vector3(0.1f, 0.03f, 0.042f), inside, 0, 0.75f);
            SP(p, new Vector3(-0.003f, 0.0175f, 0), new Vector3(0.07f, 0.018f, 0.028f), new Color(1f, 0.55f, 0.1f), 0, 0.85f);
            for (int k = 0; k < 4; k++)
                BX(p, new Vector3(-0.03f + k * 0.02f, 0.025f, 0.004f * (k % 2 == 0 ? 1 : -1)), new Vector3(0.008f, 0.0008f, 0.002f), new Color(0.85f, 0.45f, 0.08f), 0, 0.7f, new Vector3(0, k * 30f, 0));
        }
        // 껍질 반점, 탄 자국
        var rnd = new System.Random(seed * 31 + 7);
        for (int k = 0; k < 7; k++)
        {
            float a = (float)rnd.NextDouble() * Mathf.PI * 2f, x = ((float)rnd.NextDouble() - 0.5f) * 0.08f;
            if (split && Mathf.Sin(a) > 0.2f) continue;
            var n = new Vector3(0, Mathf.Sin(a), Mathf.Cos(a));
            SP(p, new Vector3(x, 0, 0) + n * 0.0245f, new Vector3(0.006f, 0.005f, 0.005f), dark, 0, 0.2f);
        }
    }

    // ---------------- 봉지 / 스탬프 ----------------
    void Bag(Transform t, float w, float h, float d)
    {
        float y0 = -0.11f, th = 0.003f, yc = y0 + h * 0.5f;
        RB(t, new Vector3(0, y0 + th * 0.5f, 0), new Vector3(w, th, d), 0.001f, Kraft * 0.9f, 0, 0.2f);
        RB(t, new Vector3(0, yc, -d * 0.5f), new Vector3(w, h, th), 0.001f, Kraft, 0, 0.22f);
        RB(t, new Vector3(0, yc, d * 0.5f), new Vector3(w, h, th), 0.001f, Kraft, 0, 0.22f);
        // 옆면 접힌 주름 (안쪽으로 살짝 들어간 두 장)
        foreach (int s in new[] { -1, 1 })
            foreach (int hz in new[] { -1, 1 })
                RB(t, new Vector3(s * (w * 0.5f - 0.004f), yc, hz * d * 0.25f), new Vector3(th, h, d * 0.52f), 0.001f, Kraft * 0.93f, 0, 0.2f, new Vector3(0, s * hz * -9f, 0));
        // 위쪽 접은 테두리
        RB(t, new Vector3(0, y0 + h - 0.008f, -d * 0.5f - 0.0015f), new Vector3(w + 0.002f, 0.016f, th), 0.001f, Kraft * 0.82f, 0, 0.2f);
        RB(t, new Vector3(0, y0 + h - 0.008f, d * 0.5f + 0.0015f), new Vector3(w + 0.002f, 0.016f, th), 0.001f, Kraft * 0.82f, 0, 0.2f);
        // 구김 자국
        var rnd = new System.Random(Mathf.RoundToInt(w * 1000));
        for (int k = 0; k < 5; k++)
        {
            float x = ((float)rnd.NextDouble() - 0.5f) * w * 0.8f, y = y0 + 0.02f + (float)rnd.NextDouble() * h * 0.7f;
            BX(t, new Vector3(x, y, -d * 0.5f - 0.0017f), new Vector3(0.03f + (float)rnd.NextDouble() * 0.03f, 0.0008f, 0.0003f), Kraft * 0.78f, 0, 0.2f, new Vector3(0, 0, ((float)rnd.NextDouble() - 0.5f) * 70f));
        }
        RB(t, new Vector3(0, y0 + h * 0.3f, 0), new Vector3(w - 0.012f, h * 0.55f, d - 0.012f), 0.002f, Kraft * 0.55f, 0, 0.2f);
    }

    void Stamp(Transform t, float d, bool fish)
    {
        float z = -d * 0.5f - 0.002f, y = -0.04f;
        float rx = fish ? 0.036f : 0.038f, ry = fish ? 0.02f : 0.016f;
        for (int k = 0; k < 20; k++)   // 타원 외곽선
        {
            float a0 = k * Mathf.PI * 2f / 20f, a1 = (k + 1) * Mathf.PI * 2f / 20f;
            var p0 = new Vector3(Mathf.Cos(a0) * rx, y + Mathf.Sin(a0) * ry, z);
            var p1 = new Vector3(Mathf.Cos(a1) * rx, y + Mathf.Sin(a1) * ry, z);
            var dd = p1 - p0;
            BX(t, (p0 + p1) * 0.5f, new Vector3(dd.magnitude + 0.001f, 0.0018f, 0.0004f), Ink, 0, 0.2f, new Vector3(0, 0, Mathf.Atan2(dd.y, dd.x) * Mathf.Rad2Deg));
        }
        if (fish)
        {
            BX(t, new Vector3(-0.045f, y + 0.008f, z), new Vector3(0.022f, 0.0018f, 0.0004f), Ink, 0, 0.2f, new Vector3(0, 0, 40f));
            BX(t, new Vector3(-0.045f, y - 0.008f, z), new Vector3(0.022f, 0.0018f, 0.0004f), Ink, 0, 0.2f, new Vector3(0, 0, -40f));
            SP(t, new Vector3(0.022f, y + 0.006f, z), new Vector3(0.005f, 0.005f, 0.0005f), Ink, 0, 0.2f);
            for (int r = 0; r < 2; r++) for (int c = 0; c < 3; c++)
                BX(t, new Vector3(-0.012f + c * 0.01f, y - 0.004f + r * 0.008f, z), new Vector3(0.006f, 0.0014f, 0.0004f), Ink, 0, 0.2f, new Vector3(0, 0, 50f));
        }
        else
        {
            BX(t, new Vector3(0, y, z), new Vector3(0.05f, 0.0016f, 0.0004f), Ink, 0, 0.2f, new Vector3(0, 0, 8f));
            for (int k = 0; k < 3; k++)   // 김(스팀) 물결
                for (int s = 0; s < 4; s++)
                    BX(t, new Vector3(-0.015f + k * 0.015f + (s % 2) * 0.003f, y + 0.03f + s * 0.007f, z), new Vector3(0.0016f, 0.008f, 0.0004f), Ink, 0, 0.2f, new Vector3(0, 0, (s % 2 == 0 ? 25f : -25f)));
        }
    }

    // ---------------- 공용 헬퍼 ----------------
    GameObject Root(string name, float mass, Vector3 col, Vector3 center = default)
    {
        var g = new GameObject("Item_" + name);
        var rb = g.AddComponent<Rigidbody>();
        rb.mass = mass;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        var bc = g.AddComponent<BoxCollider>(); bc.size = col; bc.center = center;
        return g;
    }

    static GameObject BX(Transform p, Vector3 pos, Vector3 size, Color c, float m, float s, Vector3 rot = default, Color? em = null)
        => Prim(PrimitiveType.Cube, p, pos, size, c, m, s, rot, em);
    static GameObject SP(Transform p, Vector3 pos, Vector3 size, Color c, float m, float s, Vector3 rot = default, Color? em = null)
        => Prim(PrimitiveType.Sphere, p, pos, size, c, m, s, rot, em);

    static GameObject Prim(PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Color c, float metal, float smooth, Vector3 rot, Color? em)
    {
        var g = GameObject.CreatePrimitive(type);
        DestroyImmediate(g.GetComponent<Collider>());
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        g.transform.localRotation = Quaternion.Euler(rot);
        g.transform.localScale = scale;
        var r = g.GetComponent<MeshRenderer>();
        r.sharedMaterial = Mat(c, metal, smooth, em);
        r.shadowCastingMode = scale.x * scale.y * scale.z < 2e-7f ? UnityEngine.Rendering.ShadowCastingMode.Off : UnityEngine.Rendering.ShadowCastingMode.On;
        return g;
    }

    // 둥근 모서리 상자
    static GameObject RB(Transform p, Vector3 pos, Vector3 size, float r, Color c, float m, float s, Vector3 rot = default, Color? em = null)
    {
        string key = $"rb{size.x:F4},{size.y:F4},{size.z:F4},{r:F4}";
        if (!meshes.TryGetValue(key, out var mesh) || mesh == null) { mesh = RoundedBox(size, r, 3); meshes[key] = mesh; }
        return MeshPart(p, "RBox", mesh, pos, Quaternion.Euler(rot), Vector3.one, c, m, s, em);
    }

    // 원기둥/원뿔대 (Y축). inward=true 이면 안쪽 면을 보여줌(팬 안쪽 벽)
    static GameObject CY(Transform p, Vector3 pos, float rBot, float rTop, float h, Color c, float m, float s,
                         Vector3 rot = default, int seg = 32, float unused = -1f, bool caps = true, bool inward = false, Color? em = null)
    {
        string key = $"cy{rBot:F4},{rTop:F4},{h:F4},{seg},{caps},{inward}";
        if (!meshes.TryGetValue(key, out var mesh) || mesh == null) { mesh = Cone(rBot, rTop, h, seg, caps, inward, false); meshes[key] = mesh; }
        return MeshPart(p, "Cyl", mesh, pos, Quaternion.Euler(rot), Vector3.one, c, m, s, em);
    }
    static GameObject CY(Transform p, Vector3 pos, float rBot, float rTop, float h, Color c, float m, float s, Vector3 rot, Color em)
        => CY(p, pos, rBot, rTop, h, c, m, s, rot, 32, -1f, true, false, em);

    static Mesh Prism(int n, float r, float h)
    {
        string key = $"pr{n},{r:F4},{h:F4}";
        if (!meshes.TryGetValue(key, out var mesh) || mesh == null) { mesh = Cone(r, r, h, n, true, false, true); meshes[key] = mesh; }
        return mesh;
    }

    static GameObject MeshPart(Transform p, string name, Mesh mesh, Vector3 pos, Quaternion rot, Vector3 scale, Color c, float m, float s, Color? em = null)
    {
        var g = new GameObject(name);
        g.transform.SetParent(p, false);
        g.transform.localPosition = pos;
        g.transform.localRotation = rot;
        g.transform.localScale = scale;
        g.AddComponent<MeshFilter>().sharedMesh = mesh;
        g.AddComponent<MeshRenderer>().sharedMaterial = Mat(c, m, s, em);
        return g;
    }

    static Mesh Cone(float rBot, float rTop, float h, int seg, bool caps, bool inward, bool flat)
    {
        var v = new List<Vector3>(); var n = new List<Vector3>(); var tri = new List<int>();
        float hy = h * 0.5f;
        float slope = (rBot - rTop) / Mathf.Max(h, 1e-5f);
        for (int i = 0; i < seg; i++)
        {
            float a0 = i * Mathf.PI * 2f / seg, a1 = (i + 1) * Mathf.PI * 2f / seg;
            Vector3 d0 = new Vector3(Mathf.Sin(a0), 0, Mathf.Cos(a0)), d1 = new Vector3(Mathf.Sin(a1), 0, Mathf.Cos(a1));
            Vector3 b0 = d0 * rBot - Vector3.up * hy, b1 = d1 * rBot - Vector3.up * hy, t0 = d0 * rTop + Vector3.up * hy, t1 = d1 * rTop + Vector3.up * hy;
            Vector3 n0 = (d0 + Vector3.up * slope).normalized, n1 = (d1 + Vector3.up * slope).normalized;
            if (flat) { var dm = (d0 + d1).normalized; n0 = n1 = (dm + Vector3.up * slope).normalized; }
            if (inward) { n0 = -n0; n1 = -n1; }
            int s0 = v.Count;
            v.Add(b0); v.Add(b1); v.Add(t1); v.Add(t0);
            n.Add(n0); n.Add(n1); n.Add(n1); n.Add(n0);
            var outN = (n0 + n1).normalized;
            Tri(tri, v, s0, s0 + 1, s0 + 2, outN); Tri(tri, v, s0, s0 + 2, s0 + 3, outN);
        }
        if (caps)
        {
            foreach (int sgn in new[] { -1, 1 })
            {
                float r = sgn > 0 ? rTop : rBot;
                if (r <= 0) continue;
                var nn = Vector3.up * sgn;
                int c0 = v.Count;
                v.Add(Vector3.up * hy * sgn); n.Add(nn);
                for (int i = 0; i <= seg; i++)
                {
                    float a = i * Mathf.PI * 2f / seg;
                    v.Add(new Vector3(Mathf.Sin(a) * r, hy * sgn, Mathf.Cos(a) * r)); n.Add(nn);
                }
                for (int i = 0; i < seg; i++) Tri(tri, v, c0, c0 + 1 + i, c0 + 2 + i, nn);
            }
        }
        var mesh = new Mesh();
        mesh.SetVertices(v); mesh.SetNormals(n); mesh.SetTriangles(tri, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    static Mesh RoundedBox(Vector3 size, float r, int seg)
    {
        Vector3 h = size * 0.5f;
        r = Mathf.Min(r, Mathf.Min(h.x, Mathf.Min(h.y, h.z)) * 0.98f);
        Vector3 ih = h - Vector3.one * r;
        float[][] ax = { Axis(h.x, r, seg), Axis(h.y, r, seg), Axis(h.z, r, seg) };
        var v = new List<Vector3>(); var n = new List<Vector3>(); var t = new List<int>();
        for (int axis = 0; axis < 3; axis++)
        for (int sign = -1; sign <= 1; sign += 2)
        {
            int ua = (axis + 1) % 3, va = (axis + 2) % 3;
            float[] us = ax[ua], vs = ax[va];
            int start = v.Count;
            Vector3 fn = Vector3.zero; fn[axis] = sign;
            for (int j = 0; j < vs.Length; j++)
            for (int i = 0; i < us.Length; i++)
            {
                Vector3 p = Vector3.zero; p[axis] = sign * h[axis]; p[ua] = us[i]; p[va] = vs[j];
                Vector3 inn = new Vector3(Mathf.Clamp(p.x, -ih.x, ih.x), Mathf.Clamp(p.y, -ih.y, ih.y), Mathf.Clamp(p.z, -ih.z, ih.z));
                Vector3 d = p - inn; Vector3 nr = d.sqrMagnitude < 1e-12f ? fn : d.normalized;
                v.Add(inn + nr * r); n.Add(nr);
            }
            int w = us.Length;
            for (int j = 0; j < vs.Length - 1; j++)
            for (int i = 0; i < w - 1; i++)
            {
                int a = start + j * w + i, b = a + 1, c = a + w, d2 = c + 1;
                Tri(t, v, a, b, d2, fn); Tri(t, v, a, d2, c, fn);
            }
        }
        var mesh = new Mesh();
        mesh.SetVertices(v); mesh.SetNormals(n); mesh.SetTriangles(t, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    static float[] Axis(float h, float r, int seg)
    {
        var l = new List<float>();
        for (int k = 0; k <= seg; k++) l.Add(-h + r - r * Mathf.Cos(Mathf.PI * 0.5f * k / seg));
        for (int k = 0; k <= seg; k++)
        {
            float x = (h - r) + r * Mathf.Sin(Mathf.PI * 0.5f * k / seg);
            if (k == 0 && Mathf.Abs(x - l[l.Count - 1]) < 1e-6f) continue;
            l.Add(x);
        }
        return l.ToArray();
    }

    static void Tri(List<int> t, List<Vector3> v, int a, int b, int c, Vector3 o)
    {
        Vector3 cr = Vector3.Cross(v[b] - v[a], v[c] - v[a]);
        if (cr.sqrMagnitude < 1e-18f) return;
        if (Vector3.Dot(cr, o) >= 0) { t.Add(a); t.Add(b); t.Add(c); } else { t.Add(a); t.Add(c); t.Add(b); }
    }

    static Material Mat(Color c, float metal, float smooth, Color? emission)
    {
        string k = $"{c.r:F3},{c.g:F3},{c.b:F3},{metal:F2},{smooth:F2},{(emission.HasValue ? emission.Value.ToString("F3") : "-")}";
        if (mats.TryGetValue(k, out var m) && m != null) return m;
        if (lit == null) lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        m = new Material(lit);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c); else m.color = c;
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metal);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
        if (emission.HasValue) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", emission.Value); }
        mats[k] = m;
        return m;
    }

    static void AddGrab(GameObject go)
    {
        if (!searched)
        {
            searched = true;
            foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                System.Type[] types;
                try { types = asm.GetTypes(); } catch { continue; }
                foreach (var t in types)
                    if (t.Name == "XRGrabInteractable" && typeof(Component).IsAssignableFrom(t)) { grabType = t; break; }
                if (grabType != null) break;
            }
        }
        if (grabType != null) go.AddComponent(grabType);
    }
}

#if UNITY_EDITOR
static class BlueChuteButtonBuilder
{
    const string Root = "Assets/ConveyorMaterials";

    [UnityEditor.MenuItem("Tools/Build Blue Chute Button")]
    static void Build()
    {
        var red = GameObject.Find("RedWallButton");
        var chute = GameObject.Find("Chute_VD01");
        if (red == null || chute == null) { Debug.LogWarning("[Blue] RedWallButton 또는 Chute_VD01이 없어요."); return; }

        UnityEditor.GameObjectUtility.RemoveMonoBehavioursWithMissingScript(chute);
        var old = GameObject.Find("BlueWallButton");
        if (old) UnityEditor.Undo.DestroyObjectImmediate(old);

        // 슈트 쪽으로, 빨간 버튼 옆 28cm
        var rt = red.transform;
        float side = Vector3.Dot(chute.transform.position - rt.position, rt.right) >= 0 ? 1f : -1f;

        var blue = Mat("BTN_Blue", new Color(0.05f, 0.3f, 0.95f), 0f, 0.75f);
        var plate = Mat("BTN_Plate", new Color(0.78f, 0.80f, 0.83f), 0.4f, 0.5f);
        var dark = Mat("BTN_Dark", new Color(0.12f, 0.12f, 0.13f), 0.3f, 0.45f);
        var metal = Mat("BTN_Metal", new Color(0.7f, 0.71f, 0.73f), 0.8f, 0.6f);
        var stripe = Mat("BTN_BlueStripe", new Color(0.05f, 0.25f, 0.7f), 0f, 0.4f);

        var root = new GameObject("BlueWallButton").transform;
        UnityEditor.Undo.RegisterCreatedObjectUndo(root.gameObject, "Build Blue Button");
        root.SetPositionAndRotation(rt.position + rt.right * side * 0.28f, rt.rotation);

        Prim(PrimitiveType.Cube, root, "BackPlate", new Vector3(0, -0.03f, 0.008f), new Vector3(0.2f, 0.28f, 0.016f), Vector3.zero, plate);
        for (int k = 0; k < 6; k++)
            Prim(PrimitiveType.Cube, root, "Stripe", new Vector3(-0.075f + k * 0.03f, -0.155f, 0.0165f), new Vector3(0.012f, 0.03f, 0.002f), new Vector3(0, 0, 35f), stripe);
        Prim(PrimitiveType.Cube, root, "Housing", new Vector3(0, 0, 0.045f), new Vector3(0.13f, 0.13f, 0.06f), Vector3.zero, dark);
        Prim(PrimitiveType.Cylinder, root, "Collar", new Vector3(0, 0, 0.08f), new Vector3(0.1f, 0.006f, 0.1f), new Vector3(90, 0, 0), metal);
        foreach (int sx in new[] { -1, 1 }) foreach (int sy in new[] { -1, 1 })
            Prim(PrimitiveType.Sphere, root, "Screw", new Vector3(sx * 0.052f, sy * 0.052f, 0.0755f), new Vector3(0.01f, 0.01f, 0.004f), Vector3.zero, metal);

        var plunger = new GameObject("Plunger").transform;
        plunger.SetParent(root, false);
        plunger.localPosition = new Vector3(0, 0, 0.085f);
        Prim(PrimitiveType.Cylinder, plunger, "Stem", new Vector3(0, 0, 0.012f), new Vector3(0.05f, 0.012f, 0.05f), new Vector3(90, 0, 0), blue);
        var cap = Prim(PrimitiveType.Sphere, plunger, "Cap", new Vector3(0, 0, 0.026f), new Vector3(0.085f, 0.085f, 0.04f), Vector3.zero, blue);

        var labelGo = new GameObject("Label");
        labelGo.transform.SetParent(root, false);
        labelGo.transform.localPosition = new Vector3(0, 0.088f, 0.0175f);
        labelGo.transform.localRotation = Quaternion.Euler(0, 180f, 0);
        var tm = labelGo.AddComponent<TextMesh>();
        tm.text = "ITEM DROP"; tm.anchor = TextAnchor.MiddleCenter; tm.alignment = TextAlignment.Center;
        tm.fontSize = 64; tm.characterSize = 0.0045f; tm.color = new Color(0.05f, 0.15f, 0.45f); tm.fontStyle = FontStyle.Bold;
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font) { tm.font = font; labelGo.GetComponent<MeshRenderer>().sharedMaterial = font.material; }

        var col = root.gameObject.AddComponent<BoxCollider>();
        col.center = new Vector3(0, 0, 0.09f); col.size = new Vector3(0.12f, 0.12f, 0.08f);

        var btn = root.gameObject.AddComponent<BluePushButton>();
        btn.plunger = plunger;
        btn.lamp = cap.GetComponent<Renderer>();

        // 슈트에 배출기
        var drop = chute.GetComponent<ChuteDropper>();
        if (drop == null) drop = UnityEditor.Undo.AddComponent<ChuteDropper>(chute);
        drop.dropPoint = chute.transform.Find("DropPoint");
        var lt = chute.transform.Find("Outlet/OutletLight");
        drop.outletLight = lt ? lt.GetComponent<Light>() : null;
        btn.target = drop;
        UnityEditor.EditorUtility.SetDirty(drop);

        // XR 선택 → Press
        string xrMsg = "XR Interaction Toolkit이 없어 XR 연결은 건너뛰었어요.";
        System.Type simpleType = null;
        foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            System.Type[] types;
            try { types = asm.GetTypes(); } catch { continue; }
            foreach (var t in types) if (t.Name == "XRSimpleInteractable" && typeof(Component).IsAssignableFrom(t)) { simpleType = t; break; }
            if (simpleType != null) break;
        }
        if (simpleType != null)
        {
            var comp = root.gameObject.AddComponent(simpleType);
            var evt = simpleType.GetProperty("selectEntered")?.GetValue(comp) as UnityEngine.Events.UnityEventBase;
            if (evt != null)
            {
                UnityEditor.Events.UnityEventTools.AddVoidPersistentListener(evt, new UnityEngine.Events.UnityAction(btn.Press));
                xrMsg = "VR에서 버튼을 선택(그립/트리거)하면 눌려요.";
            }
        }

        UnityEditor.AssetDatabase.SaveAssets();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(chute.scene);
        UnityEditor.Selection.activeGameObject = root.gameObject;
        Debug.Log("[Blue] 파란 버튼 생성 완료. " + xrMsg);
    }

    static GameObject Prim(PrimitiveType type, Transform parent, string name, Vector3 pos, Vector3 scale, Vector3 rot, Material m)
    {
        var g = GameObject.CreatePrimitive(type);
        g.name = name;
        Object.DestroyImmediate(g.GetComponent<Collider>());
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        g.transform.localRotation = Quaternion.Euler(rot);
        g.transform.localScale = scale;
        g.GetComponent<MeshRenderer>().sharedMaterial = m;
        return g;
    }

    static Material Mat(string name, Color c, float metallic, float smooth)
    {
        if (!UnityEditor.AssetDatabase.IsValidFolder(Root)) UnityEditor.AssetDatabase.CreateFolder("Assets", "ConveyorMaterials");
        string path = $"{Root}/{name}.mat";
        var mat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat != null) return mat;
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        mat = new Material(shader);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", c);
        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smooth);
        UnityEditor.AssetDatabase.CreateAsset(mat, path);
        return mat;
    }
}
#endif
