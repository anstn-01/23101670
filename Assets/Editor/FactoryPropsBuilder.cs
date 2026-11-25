using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// 메뉴: Tools > City > Build Factory Props (공장 내부 프리팹)
// 공장 안에 직접 끌어다 놓을 수 있는 시설물 프리팹을 "Assets/메쉬/공장 내부" 폴더에 만듭니다.
// 모든 프리팹은 바닥 가운데가 기준점(피벗)이라 바닥에 바로 놓입니다.
// 작은 물건(종이상자, 라바콘, 소화기)은 Rigidbody + XR Grab Interactable 이 있어 VR 손으로 집을 수 있습니다.
// 다시 실행하면 같은 이름의 프리팹을 새로 덮어씁니다.
public static class FactoryPropsBuilder
{
    const string Root = "Assets/메쉬/도시/Demo City By Versatile Studio/";
    const string OutDir = "Assets/메쉬/공장 내부";

    static readonly Rect Full = Rect.MinMaxRect(0, 0, 1, 1);
    static readonly Rect Wood = Rect.MinMaxRect(0.02f, 0.27f, 0.98f, 0.55f);       // props_main 나무 판자
    static readonly Rect Concrete = Rect.MinMaxRect(0.02f, 0.02f, 0.98f, 0.44f);   // building_bases 콘크리트

    enum M { Wood, Cardboard, Tape, SteelDark, SteelLight, Yellow, Orange, Red, Blue, Green, White, Black, Screen, Concrete, Count }
    static Material[] mats;

    [MenuItem("Tools/City/Build Factory Props (공장 내부 프리팹)")]
    public static void Build()
    {
        EnsureFolder(OutDir);
        EnsureFolder(OutDir + "/Materials");
        EnsureFolder(OutDir + "/Meshes");
        CreateMaterials();

        var made = new List<string>();
        made.Add(Make("팔레트", Pallet, false, 0));
        made.Add(Make("팔레트_상자적재", PalletWithBoxes, false, 0));
        made.Add(Make("종이상자", CardboardBox, true, 3f));
        made.Add(Make("나무상자", WoodCrate, false, 0));
        made.Add(Make("드럼통", Drum, false, 0));
        made.Add(Make("선반랙", Rack, false, 0));
        made.Add(Make("작업대", Workbench, false, 0));
        made.Add(Make("공구함", ToolCabinet, false, 0));
        made.Add(Make("사물함", Lockers, false, 0));
        made.Add(Make("철골기둥", Column, false, 0));
        made.Add(Make("라바콘", Cone, true, 1.5f));
        made.Add(Make("소화기", Extinguisher, true, 4f));
        made.Add(Make("산업기계", Machine, false, 0));
        made.Add(Make("안전펜스", Fence, false, 0));

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        var folder = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(OutDir);
        Selection.activeObject = folder;
        EditorGUIUtility.PingObject(folder);
        Debug.Log($"공장 내부 프리팹 {made.Count}개 생성 완료 ({OutDir}): {string.Join(", ", made)}. Project 창에서 씬으로 끌어다 놓으세요.");
    }

    // =====================================================================
    // 시설물 모양 (단위 m, y=0 이 바닥, x/z 가운데가 0)
    // =====================================================================
    static void Pallet(MeshB m, List<Bounds> c) { PalletAt(m, Vector3.zero); c.Add(B(0, 0.075f, 0, 1.2f, 0.15f, 1.0f)); }

    static void PalletAt(MeshB m, Vector3 o)
    {
        foreach (float z in new[] { -0.43f, 0f, 0.43f })
        {
            m.Box((int)M.Wood, o + new Vector3(0, 0.011f, z), new Vector3(1.2f, 0.022f, 0.12f), Wood, 1.3f);   // 아래 판
            m.Box((int)M.Wood, o + new Vector3(0, 0.075f, z), new Vector3(1.2f, 0.106f, 0.1f), Wood, 1.3f);    // 받침목
        }
        foreach (float z in new[] { -0.43f, -0.215f, 0f, 0.215f, 0.43f })
            m.Box((int)M.Wood, o + new Vector3(0, 0.139f, z), new Vector3(1.2f, 0.022f, 0.14f), Wood, 1.3f);   // 윗 판
    }

    static void BoxAt(MeshB m, Vector3 bottomCenter, Vector3 s)
    {
        m.Box((int)M.Cardboard, bottomCenter + Vector3.up * s.y * 0.5f, s, Full, 10f);
        // 테이프
        m.Box((int)M.Tape, bottomCenter + Vector3.up * (s.y + 0.002f), new Vector3(s.x + 0.004f, 0.004f, 0.06f), Full, 10f);
        m.Box((int)M.Tape, bottomCenter + new Vector3(s.x * 0.5f + 0.002f, s.y * 0.85f, 0), new Vector3(0.004f, s.y * 0.3f, 0.06f), Full, 10f);
        m.Box((int)M.Tape, bottomCenter + new Vector3(-s.x * 0.5f - 0.002f, s.y * 0.85f, 0), new Vector3(0.004f, s.y * 0.3f, 0.06f), Full, 10f);
    }

    static void PalletWithBoxes(MeshB m, List<Bounds> c)
    {
        PalletAt(m, Vector3.zero);
        Vector3 s = new Vector3(0.56f, 0.42f, 0.46f);
        float y = 0.15f;
        foreach (float x in new[] { -0.29f, 0.29f })
            foreach (float z in new[] { -0.24f, 0.24f })
                BoxAt(m, new Vector3(x, y, z), s);
        y += s.y;
        BoxAt(m, new Vector3(-0.29f, y, -0.24f), s);
        BoxAt(m, new Vector3(0.29f, y, -0.24f), s);
        BoxAt(m, new Vector3(-0.27f, y, 0.25f), s);
        c.Add(B(0, (0.15f + 0.84f) * 0.5f, 0, 1.2f, 0.99f, 1.0f));
    }

    static void CardboardBox(MeshB m, List<Bounds> c)
    {
        Vector3 s = new Vector3(0.5f, 0.38f, 0.4f);
        BoxAt(m, Vector3.zero, s);
        c.Add(B(0, s.y * 0.5f, 0, s.x, s.y, s.z));
    }

    static void WoodCrate(MeshB m, List<Bounds> c)
    {
        Vector3 s = new Vector3(1.0f, 0.8f, 0.8f);
        m.Box((int)M.Wood, new Vector3(0, s.y * 0.5f, 0), s, Wood, 1.0f);
        float t = 0.06f, p = 0.015f;
        // 모서리 테두리 각재
        foreach (float x in new[] { -1f, 1f })
            foreach (float z in new[] { -1f, 1f })
                m.Box((int)M.Wood, new Vector3(x * (s.x * 0.5f - t * 0.5f + p), s.y * 0.5f, z * (s.z * 0.5f - t * 0.5f + p)), new Vector3(t, s.y, t), Wood, 1.0f);
        foreach (float y in new[] { t * 0.5f, s.y - t * 0.5f })
        {
            foreach (float z in new[] { -1f, 1f })
                m.Box((int)M.Wood, new Vector3(0, y, z * (s.z * 0.5f + p * 0.5f)), new Vector3(s.x, t, p * 2), Wood, 1.0f);
            foreach (float x in new[] { -1f, 1f })
                m.Box((int)M.Wood, new Vector3(x * (s.x * 0.5f + p * 0.5f), y, 0), new Vector3(p * 2, t, s.z), Wood, 1.0f);
        }
        // 앞뒤 대각선 보강목
        foreach (float z in new[] { -1f, 1f })
            m.BoxRotZ((int)M.Wood, new Vector3(0, s.y * 0.5f, z * (s.z * 0.5f + p)), new Vector3(Mathf.Sqrt(s.x * s.x + s.y * s.y) - 0.12f, t, p * 2), Mathf.Atan2(s.y, s.x) * Mathf.Rad2Deg, Wood, 1.2f);
        c.Add(B(0, s.y * 0.5f, 0, s.x + 0.03f, s.y, s.z + 0.03f));
    }

    static void Drum(MeshB m, List<Bounds> c)
    {
        float r = 0.29f, h = 0.88f;
        m.Cyl((int)M.Blue, Vector3.zero, r, r, h, 20, true, true);
        foreach (float y in new[] { 0.28f, 0.58f })
            m.Cyl((int)M.SteelLight, new Vector3(0, y, 0), r + 0.008f, r + 0.008f, 0.03f, 20, false, false);
        m.Cyl((int)M.SteelLight, new Vector3(0, h, 0), r - 0.01f, r - 0.01f, 0.012f, 20, true, false);
        m.Cyl((int)M.SteelDark, new Vector3(0.15f, h + 0.012f, 0.05f), 0.03f, 0.03f, 0.02f, 8, true, false);
        c.Add(B(0, h * 0.5f, 0, r * 2, h, r * 2));
    }

    static void Rack(MeshB m, List<Bounds> c)
    {
        float w = 2.7f, d = 1.0f, h = 3.0f;
        foreach (float x in new[] { -w * 0.5f + 0.04f, w * 0.5f - 0.04f })
        {
            foreach (float z in new[] { -d * 0.5f + 0.04f, d * 0.5f - 0.04f })
            {
                m.Box((int)M.Blue, new Vector3(x, h * 0.5f, z), new Vector3(0.08f, h, 0.08f), Full, 10f);
                m.Box((int)M.SteelDark, new Vector3(x, 0.005f, z), new Vector3(0.16f, 0.01f, 0.16f), Full, 10f);
            }
            for (float y = 0.4f; y < h; y += 0.6f)
                m.Box((int)M.SteelLight, new Vector3(x, y, 0), new Vector3(0.04f, 0.04f, d - 0.1f), Full, 10f);
        }
        float[] levels = { 0.15f, 1.15f, 2.15f };
        foreach (float y in levels)
        {
            foreach (float z in new[] { -d * 0.5f + 0.04f, d * 0.5f - 0.04f })
                m.Box((int)M.Orange, new Vector3(0, y, z), new Vector3(w - 0.16f, 0.1f, 0.05f), Full, 10f);
            m.Box((int)M.Wood, new Vector3(0, y + 0.065f, 0), new Vector3(w - 0.16f, 0.03f, d - 0.08f), Wood, 1.3f);
        }
        // 물건: 1층 드럼통 2개, 2/3층 종이상자
        foreach (float x in new[] { -0.7f, -0.05f })
        {
            m.Cyl((int)M.Blue, new Vector3(x, 0.23f, 0), 0.29f, 0.29f, 0.88f, 16, true, false);
            m.Cyl((int)M.SteelLight, new Vector3(x, 0.23f + 0.4f, 0), 0.297f, 0.297f, 0.03f, 16, false, false);
        }
        BoxAt(m, new Vector3(0.75f, 0.23f, 0.1f), new Vector3(0.56f, 0.42f, 0.46f));
        BoxAt(m, new Vector3(-0.8f, 1.23f, 0), new Vector3(0.56f, 0.42f, 0.46f));
        BoxAt(m, new Vector3(-0.2f, 1.23f, 0.05f), new Vector3(0.5f, 0.38f, 0.4f));
        BoxAt(m, new Vector3(0.7f, 1.23f, -0.05f), new Vector3(0.56f, 0.42f, 0.46f));
        BoxAt(m, new Vector3(0.7f, 1.65f, -0.05f), new Vector3(0.5f, 0.38f, 0.4f));
        BoxAt(m, new Vector3(-0.5f, 2.23f, 0), new Vector3(0.56f, 0.42f, 0.46f));
        BoxAt(m, new Vector3(0.3f, 2.23f, 0.05f), new Vector3(0.56f, 0.42f, 0.46f));
        c.Add(B(0, h * 0.5f, 0, w, h, d));
    }

    static void Workbench(MeshB m, List<Bounds> c)
    {
        float w = 2.0f, d = 0.8f, top = 0.9f;
        m.Box((int)M.Wood, new Vector3(0, top - 0.03f, 0), new Vector3(w, 0.06f, d), Wood, 1.3f);
        foreach (float x in new[] { -w * 0.5f + 0.06f, w * 0.5f - 0.06f })
            foreach (float z in new[] { -d * 0.5f + 0.06f, d * 0.5f - 0.06f })
                m.Box((int)M.SteelDark, new Vector3(x, (top - 0.06f) * 0.5f, z), new Vector3(0.06f, top - 0.06f, 0.06f), Full, 10f);
        m.Box((int)M.Wood, new Vector3(0, 0.2f, 0), new Vector3(w - 0.1f, 0.03f, d - 0.1f), Wood, 1.3f);
        // 뒤쪽 타공판과 공구
        float bz = -d * 0.5f + 0.015f;
        m.Box((int)M.SteelDark, new Vector3(0, top + 0.45f, bz), new Vector3(w, 0.9f, 0.03f), Full, 10f);
        for (int i = 0; i < 6; i++)
        {
            float x = -0.8f + i * 0.32f;
            m.Box((int)M.SteelLight, new Vector3(x, top + 0.55f, bz + 0.03f), new Vector3(0.04f, 0.28f + (i % 2) * 0.1f, 0.02f), Full, 10f);
            m.Box((int)M.Red, new Vector3(x, top + 0.38f, bz + 0.03f), new Vector3(0.05f, 0.1f, 0.03f), Full, 10f);
        }
        // 바이스
        m.Box((int)M.SteelDark, new Vector3(0.75f, top + 0.06f, 0.2f), new Vector3(0.2f, 0.12f, 0.22f), Full, 10f);
        m.Box((int)M.Blue, new Vector3(0.75f, top + 0.15f, 0.25f), new Vector3(0.18f, 0.08f, 0.06f), Full, 10f);
        // 공구 상자
        m.Box((int)M.Red, new Vector3(-0.55f, top + 0.1f, 0.1f), new Vector3(0.45f, 0.2f, 0.22f), Full, 10f);
        m.Box((int)M.SteelDark, new Vector3(-0.55f, top + 0.23f, 0.1f), new Vector3(0.2f, 0.03f, 0.03f), Full, 10f);
        c.Add(B(0, top * 0.5f, 0, w, top, d));
        c.Add(B(0, top + 0.45f, bz, w, 0.9f, 0.05f));
    }

    static void ToolCabinet(MeshB m, List<Bounds> c)
    {
        float w = 0.7f, d = 0.45f, h = 0.95f, wh = 0.08f;
        foreach (float x in new[] { -w * 0.5f + 0.06f, w * 0.5f - 0.06f })
            foreach (float z in new[] { -d * 0.5f + 0.06f, d * 0.5f - 0.06f })
                m.Cyl((int)M.Black, new Vector3(x, 0, z), 0.04f, 0.04f, wh, 10, true, false);
        m.Box((int)M.Red, new Vector3(0, wh + h * 0.5f, 0), new Vector3(w, h, d), Full, 10f);
        m.Box((int)M.SteelDark, new Vector3(0, wh + h + 0.01f, 0), new Vector3(w + 0.02f, 0.02f, d + 0.02f), Full, 10f);
        float fz = d * 0.5f;
        float[] drawers = { 0.12f, 0.12f, 0.15f, 0.18f, 0.3f };
        float y = wh + h - 0.03f;
        foreach (float dh in drawers)
        {
            m.Box((int)M.SteelDark, new Vector3(0, y, fz + 0.002f), new Vector3(w - 0.04f, 0.008f, 0.004f), Full, 10f);
            m.Box((int)M.SteelLight, new Vector3(0, y - 0.035f, fz + 0.015f), new Vector3(w * 0.6f, 0.018f, 0.02f), Full, 10f);
            y -= dh;
        }
        // 옆 손잡이
        m.Box((int)M.SteelLight, new Vector3(w * 0.5f + 0.04f, wh + h - 0.15f, 0), new Vector3(0.02f, 0.02f, d * 0.7f), Full, 10f);
        c.Add(B(0, (wh + h) * 0.5f, 0, w, wh + h, d));
    }

    static void Lockers(MeshB m, List<Bounds> c)
    {
        int n = 3; float uw = 0.4f, h = 1.8f, d = 0.5f, w = n * uw;
        m.Box((int)M.SteelLight, new Vector3(0, h * 0.5f + 0.05f, 0), new Vector3(w, h, d), Full, 10f);
        m.Box((int)M.SteelDark, new Vector3(0, 0.025f, 0), new Vector3(w - 0.04f, 0.05f, d - 0.04f), Full, 10f);
        float fz = d * 0.5f;
        for (int i = 0; i < n; i++)
        {
            float x0 = -w * 0.5f + i * uw;
            if (i > 0) m.Box((int)M.SteelDark, new Vector3(x0, h * 0.5f + 0.05f, fz + 0.002f), new Vector3(0.008f, h - 0.02f, 0.004f), Full, 10f);
            float cx = x0 + uw * 0.5f;
            for (int k = 0; k < 4; k++)
                m.Box((int)M.SteelDark, new Vector3(cx, h - 0.15f - k * 0.04f, fz + 0.002f), new Vector3(uw * 0.55f, 0.012f, 0.004f), Full, 10f);
            m.Box((int)M.SteelDark, new Vector3(x0 + uw - 0.07f, h * 0.55f, fz + 0.015f), new Vector3(0.02f, 0.14f, 0.02f), Full, 10f);
            m.Box((int)M.White, new Vector3(cx, h - 0.42f, fz + 0.003f), new Vector3(0.12f, 0.06f, 0.004f), Full, 10f);
        }
        c.Add(B(0, (h + 0.05f) * 0.5f, 0, w, h + 0.05f, d));
    }

    static void Column(MeshB m, List<Bounds> c)
    {
        float h = 7f, f = 0.3f, tf = 0.025f, tw = 0.02f;
        m.Box((int)M.SteelDark, new Vector3(0, 0.015f, 0), new Vector3(0.5f, 0.03f, 0.5f), Full, 10f);
        foreach (float z in new[] { -f * 0.5f + tf * 0.5f, f * 0.5f - tf * 0.5f })
            m.Box((int)M.Yellow, new Vector3(0, h * 0.5f, z), new Vector3(f, h, tf), Full, 10f);
        m.Box((int)M.Yellow, new Vector3(0, h * 0.5f, 0), new Vector3(tw, h, f - tf * 2), Full, 10f);
        // 아래 검정 띠 (안전 표시)
        foreach (float z in new[] { -f * 0.5f - 0.001f, f * 0.5f + 0.001f })
            for (int i = 0; i < 3; i++)
                m.Box((int)M.Black, new Vector3(0, 0.25f + i * 0.4f, z), new Vector3(f + 0.002f, 0.2f, 0.004f), Full, 10f);
        foreach (float x in new[] { -0.2f, 0.2f })
            foreach (float z in new[] { -0.2f, 0.2f })
                m.Cyl((int)M.SteelLight, new Vector3(x, 0.03f, z), 0.02f, 0.02f, 0.03f, 6, true, false);
        c.Add(B(0, h * 0.5f, 0, f, h, f));
    }

    static void Cone(MeshB m, List<Bounds> c)
    {
        m.Box((int)M.Black, new Vector3(0, 0.015f, 0), new Vector3(0.38f, 0.03f, 0.38f), Full, 10f);
        float y0 = 0.03f, h = 0.68f, r0 = 0.17f, r1 = 0.03f;
        float R(float y) => Mathf.Lerp(r0, r1, (y - y0) / h);
        m.Cyl((int)M.Orange, new Vector3(0, y0, 0), r0, r1, h, 16, true, false);
        float a = 0.32f, b = 0.44f;
        m.Cyl((int)M.White, new Vector3(0, a, 0), R(a) + 0.004f, R(b) + 0.004f, b - a, 16, false, false);
        c.Add(B(0, 0.36f, 0, 0.38f, 0.72f, 0.38f));
    }

    static void Extinguisher(MeshB m, List<Bounds> c)
    {
        float r = 0.075f, h = 0.45f;
        m.Cyl((int)M.Red, Vector3.zero, r, r, h, 16, true, true);
        m.Cyl((int)M.Red, new Vector3(0, h, 0), r, r * 0.45f, 0.05f, 16, true, false);
        m.Cyl((int)M.Black, new Vector3(0, h + 0.05f, 0), 0.025f, 0.025f, 0.06f, 8, true, false);
        m.Box((int)M.SteelDark, new Vector3(0.03f, h + 0.12f, 0), new Vector3(0.12f, 0.015f, 0.03f), Full, 10f);  // 손잡이
        m.Box((int)M.SteelDark, new Vector3(0.0f, h + 0.1f, 0), new Vector3(0.1f, 0.012f, 0.025f), Full, 10f);
        m.Box((int)M.Black, new Vector3(r + 0.012f, h * 0.6f, 0), new Vector3(0.018f, h * 0.7f, 0.018f), Full, 10f);  // 호스
        m.Box((int)M.Black, new Vector3(r * 0.6f, h + 0.07f, 0), new Vector3(0.05f, 0.018f, 0.018f), Full, 10f);
        m.Box((int)M.White, new Vector3(0, h * 0.55f, r + 0.001f), new Vector3(0.08f, 0.12f, 0.003f), Full, 10f);    // 라벨
        c.Add(B(0, (h + 0.13f) * 0.5f, 0, r * 2 + 0.05f, h + 0.13f, r * 2));
    }

    static void Machine(MeshB m, List<Bounds> c)
    {
        m.Box((int)M.SteelDark, new Vector3(0, 0.075f, 0), new Vector3(2.2f, 0.15f, 1.3f), Full, 10f);
        m.Box((int)M.Green, new Vector3(0, 0.15f + 0.7f, 0), new Vector3(2.0f, 1.4f, 1.2f), Full, 10f);
        m.Box((int)M.Green, new Vector3(0.2f, 1.55f + 0.2f, -0.1f), new Vector3(1.4f, 0.4f, 0.8f), Full, 10f);
        // 노랑/검정 경고 띠
        m.Box((int)M.Yellow, new Vector3(0, 0.32f, 0), new Vector3(2.02f, 0.1f, 1.22f), Full, 10f);
        for (int i = 0; i < 10; i++)
            m.Box((int)M.Black, new Vector3(-0.9f + i * 0.2f, 0.32f, 0.612f), new Vector3(0.08f, 0.1f, 0.004f), Full, 10f);
        // 조작 패널
        float pz = 0.6f;
        m.Box((int)M.SteelLight, new Vector3(0.6f, 1.25f, pz + 0.06f), new Vector3(0.55f, 0.55f, 0.12f), Full, 10f);
        m.Box((int)M.Screen, new Vector3(0.6f, 1.35f, pz + 0.125f), new Vector3(0.36f, 0.2f, 0.01f), Full, 10f);
        m.Box((int)M.Red, new Vector3(0.45f, 1.1f, pz + 0.135f), new Vector3(0.06f, 0.06f, 0.03f), Full, 10f);
        m.Box((int)M.Green, new Vector3(0.6f, 1.1f, pz + 0.135f), new Vector3(0.06f, 0.06f, 0.03f), Full, 10f);
        m.Box((int)M.Yellow, new Vector3(0.75f, 1.1f, pz + 0.135f), new Vector3(0.06f, 0.06f, 0.03f), Full, 10f);
        // 문/점검구
        m.Box((int)M.SteelDark, new Vector3(-0.45f, 0.95f, pz + 0.003f), new Vector3(0.8f, 0.9f, 0.006f), Full, 10f);
        m.Box((int)M.Green, new Vector3(-0.45f, 0.95f, pz + 0.006f), new Vector3(0.76f, 0.86f, 0.006f), Full, 10f);
        m.Box((int)M.SteelLight, new Vector3(-0.12f, 0.95f, pz + 0.02f), new Vector3(0.03f, 0.2f, 0.03f), Full, 10f);
        // 옆 모터와 배관
        m.Cyl((int)M.SteelLight, new Vector3(-1.25f, 0.15f, 0.1f), 0.25f, 0.25f, 0.6f, 16, true, false);
        m.Cyl((int)M.SteelDark, new Vector3(-1.25f, 0.75f, 0.1f), 0.1f, 0.1f, 0.12f, 12, true, false);
        m.Cyl((int)M.SteelLight, new Vector3(0.7f, 1.95f, -0.3f), 0.07f, 0.07f, 0.8f, 12, true, false);
        m.Cyl((int)M.SteelLight, new Vector3(-0.3f, 1.95f, -0.3f), 0.05f, 0.05f, 0.5f, 12, true, false);
        c.Add(B(0, 1.0f, 0, 2.2f, 2.0f, 1.3f));
        c.Add(B(-1.25f, 0.45f, 0.1f, 0.5f, 0.9f, 0.5f));
    }

    static void Fence(MeshB m, List<Bounds> c)
    {
        float w = 2.0f, h = 1.0f;
        foreach (float x in new[] { -w * 0.5f + 0.05f, w * 0.5f - 0.05f })
        {
            m.Box((int)M.Black, new Vector3(x, 0.02f, 0), new Vector3(0.12f, 0.04f, 0.45f), Full, 10f);
            m.Box((int)M.Yellow, new Vector3(x, h * 0.5f, 0), new Vector3(0.06f, h, 0.06f), Full, 10f);
        }
        foreach (float y in new[] { 0.5f, 0.95f })
            m.Box((int)M.Yellow, new Vector3(0, y, 0), new Vector3(w - 0.1f, 0.05f, 0.04f), Full, 10f);
        // 경고 판
        m.Box((int)M.White, new Vector3(0, 0.72f, 0), new Vector3(0.9f, 0.32f, 0.02f), Full, 10f);
        for (int i = 0; i < 5; i++)
            m.Box((int)M.Red, new Vector3(-0.36f + i * 0.18f, 0.72f, 0.011f), new Vector3(0.08f, 0.3f, 0.002f), Full, 10f);
        c.Add(B(0, h * 0.5f, 0, w, h, 0.45f));
    }

    static Bounds B(float x, float y, float z, float sx, float sy, float sz) { return new Bounds(new Vector3(x, y, z), new Vector3(sx, sy, sz)); }

    // =====================================================================
    // 프리팹 만들기
    // =====================================================================
    static string Make(string name, Action<MeshB, List<Bounds>> build, bool grabbable, float mass)
    {
        var mb = new MeshB();
        var cols = new List<Bounds>();
        build(mb, cols);

        var mesh = mb.ToMesh("Prop_" + name);
        string meshPath = $"{OutDir}/Meshes/{name}.asset";
        if (AssetDatabase.LoadAssetAtPath<Mesh>(meshPath) != null) AssetDatabase.DeleteAsset(meshPath);
        AssetDatabase.CreateAsset(mesh, meshPath);

        var go = new GameObject(name);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterials = mb.UsedMaterials().Select(i => mats[i]).ToArray();
        foreach (var b in cols)
        {
            var bc = go.AddComponent<BoxCollider>();
            bc.center = b.center;
            bc.size = b.size;
        }
        if (grabbable)
        {
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = mass;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            var grab = go.AddComponent<XRGrabInteractable>();
            grab.useDynamicAttach = true;   // 잡은 위치 그대로 잡기
        }

        string path = $"{OutDir}/{name}.prefab";
        PrefabUtility.SaveAsPrefabAsset(go, path);
        UnityEngine.Object.DestroyImmediate(go);
        return name;
    }

    class MeshB
    {
        readonly List<Vector3> v = new List<Vector3>();
        readonly List<Vector3> n = new List<Vector3>();
        readonly List<Vector2> uv = new List<Vector2>();
        readonly Dictionary<int, List<int>> tris = new Dictionary<int, List<int>>();

        public void Box(int mat, Vector3 c, Vector3 s, Rect r, float tile) { BoxQ(mat, c, s, Quaternion.identity, r, tile); }
        public void BoxRotZ(int mat, Vector3 c, Vector3 s, float deg, Rect r, float tile) { BoxQ(mat, c, s, Quaternion.Euler(0, 0, deg), r, tile); }

        void BoxQ(int mat, Vector3 c, Vector3 s, Quaternion q, Rect r, float tile)
        {
            Vector3 h = s * 0.5f;
            // (법선, 오른쪽, 위, 오른쪽 길이, 위 길이)
            Face(mat, c, q, Vector3.forward, Vector3.left, Vector3.up, h.z, s.x, s.y, r, tile);
            Face(mat, c, q, Vector3.back, Vector3.right, Vector3.up, h.z, s.x, s.y, r, tile);
            Face(mat, c, q, Vector3.right, Vector3.forward, Vector3.up, h.x, s.z, s.y, r, tile);
            Face(mat, c, q, Vector3.left, Vector3.back, Vector3.up, h.x, s.z, s.y, r, tile);
            Face(mat, c, q, Vector3.up, Vector3.right, Vector3.forward, h.y, s.x, s.z, r, tile);
            Face(mat, c, q, Vector3.down, Vector3.right, Vector3.back, h.y, s.x, s.z, r, tile);
        }

        void Face(int mat, Vector3 c, Quaternion q, Vector3 nrm, Vector3 right, Vector3 up, float dist, float w, float hgt, Rect r, float tile)
        {
            Vector3 fc = c + q * (nrm * dist);
            Vector3 R = q * right * (w * 0.5f), U = q * up * (hgt * 0.5f);
            float fu = Mathf.Min(1f, w / tile), fv = Mathf.Min(1f, hgt / tile);
            // 판자 결이 긴 쪽을 따라가도록
            bool swap = r != Full && hgt > w;
            Vector2 Uv(float a, float b)
            {
                if (swap) { float t = a; a = b; b = t; }
                float su = swap ? fv : fu, sv = swap ? fu : fv;
                return new Vector2(r.xMin + r.width * a * su, r.yMin + r.height * b * sv);
            }
            Quad(mat, fc - R - U, fc + R - U, fc + R + U, fc - R + U, q * nrm, Uv(0, 0), Uv(1, 0), Uv(1, 1), Uv(0, 1));
        }

        // 원기둥/원뿔대 (아래 중심 b, 세로 y축)
        public void Cyl(int mat, Vector3 b, float r0, float r1, float h, int seg, bool capTop, bool capBottom)
        {
            float slope = (r0 - r1) / h;
            for (int i = 0; i < seg; i++)
            {
                float a0 = i * Mathf.PI * 2 / seg, a1 = (i + 1) * Mathf.PI * 2 / seg;
                Vector3 d0 = new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)), d1 = new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1));
                Vector3 nm = ((d0 + d1).normalized + Vector3.up * slope).normalized;
                Quad(mat, b + d0 * r0, b + d1 * r0, b + d1 * r1 + Vector3.up * h, b + d0 * r1 + Vector3.up * h, nm,
                    new Vector2((float)i / seg, 0), new Vector2((float)(i + 1) / seg, 0), new Vector2((float)(i + 1) / seg, 1), new Vector2((float)i / seg, 1));
                if (capTop && r1 > 0.001f)
                    Tri(mat, b + Vector3.up * h, b + d0 * r1 + Vector3.up * h, b + d1 * r1 + Vector3.up * h, Vector3.up);
                if (capBottom)
                    Tri(mat, b, b + d1 * r0, b + d0 * r0, Vector3.down);
            }
        }

        void Quad(int mat, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 nrm, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud)
        {
            int i = v.Count;
            v.Add(a); v.Add(b); v.Add(c); v.Add(d);
            for (int k = 0; k < 4; k++) n.Add(nrm);
            uv.Add(ua); uv.Add(ub); uv.Add(uc); uv.Add(ud);
            var t = T(mat);
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), nrm) >= 0f) { t.Add(i); t.Add(i + 1); t.Add(i + 2); t.Add(i); t.Add(i + 2); t.Add(i + 3); }
            else { t.Add(i); t.Add(i + 2); t.Add(i + 1); t.Add(i); t.Add(i + 3); t.Add(i + 2); }
        }

        void Tri(int mat, Vector3 a, Vector3 b, Vector3 c, Vector3 nrm)
        {
            int i = v.Count;
            v.Add(a); v.Add(b); v.Add(c);
            for (int k = 0; k < 3; k++) n.Add(nrm);
            uv.Add(new Vector2(0.5f, 0.5f)); uv.Add(new Vector2(0, 0)); uv.Add(new Vector2(1, 0));
            var t = T(mat);
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), nrm) >= 0f) { t.Add(i); t.Add(i + 1); t.Add(i + 2); }
            else { t.Add(i); t.Add(i + 2); t.Add(i + 1); }
        }

        List<int> T(int mat) { if (!tris.TryGetValue(mat, out var t)) tris[mat] = t = new List<int>(); return t; }

        public int[] UsedMaterials() { return tris.Keys.OrderBy(k => k).ToArray(); }

        public Mesh ToMesh(string name)
        {
            var m = new Mesh { name = name };
            m.indexFormat = v.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv);
            var keys = UsedMaterials();
            m.subMeshCount = keys.Length;
            for (int i = 0; i < keys.Length; i++) m.SetTriangles(tris[keys[i]], i);
            m.RecalculateTangents();
            m.RecalculateBounds();
            return m;
        }
    }

    // =====================================================================
    // 재질
    // =====================================================================
    static void CreateMaterials()
    {
        mats = new Material[(int)M.Count];
        mats[(int)M.Wood] = Mat("Prop_Wood", "props_main", "props_main_nm", new Color(1f, 0.95f, 0.9f), 0.25f, 0f);
        mats[(int)M.Cardboard] = Mat("Prop_Cardboard", null, null, new Color(0.62f, 0.46f, 0.3f), 0.1f, 0f);
        mats[(int)M.Tape] = Mat("Prop_Tape", null, null, new Color(0.78f, 0.66f, 0.45f), 0.4f, 0f);
        mats[(int)M.SteelDark] = Mat("Prop_SteelDark", null, null, new Color(0.17f, 0.17f, 0.18f), 0.55f, 0.7f);
        mats[(int)M.SteelLight] = Mat("Prop_SteelLight", null, null, new Color(0.55f, 0.56f, 0.58f), 0.6f, 0.8f);
        mats[(int)M.Yellow] = Mat("Prop_PaintYellow", null, null, new Color(0.95f, 0.72f, 0.08f), 0.35f, 0f);
        mats[(int)M.Orange] = Mat("Prop_PaintOrange", null, null, new Color(0.95f, 0.38f, 0.06f), 0.35f, 0f);
        mats[(int)M.Red] = Mat("Prop_PaintRed", null, null, new Color(0.72f, 0.07f, 0.05f), 0.4f, 0f);
        mats[(int)M.Blue] = Mat("Prop_PaintBlue", null, null, new Color(0.1f, 0.27f, 0.55f), 0.4f, 0.1f);
        mats[(int)M.Green] = Mat("Prop_PaintGreen", null, null, new Color(0.27f, 0.4f, 0.34f), 0.35f, 0.1f);
        mats[(int)M.White] = Mat("Prop_PaintWhite", null, null, new Color(0.9f, 0.9f, 0.88f), 0.3f, 0f);
        mats[(int)M.Black] = Mat("Prop_Black", null, null, new Color(0.05f, 0.05f, 0.05f), 0.3f, 0f);
        mats[(int)M.Screen] = Mat("Prop_Screen", null, null, new Color(0.05f, 0.25f, 0.15f), 0.8f, 0f, new Color(0.2f, 1f, 0.55f) * 1.5f);
        mats[(int)M.Concrete] = Mat("Prop_Concrete", "building_bases", "building_bases_nm", Color.white, 0.1f, 0f);
    }

    static Material Mat(string name, string tex, string nrm, Color color, float smooth, float metal, Color emission = default)
    {
        string path = $"{OutDir}/Materials/{name}.mat";
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
        else m.shader = shader;

        var baseTex = tex != null ? AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "Textures/" + tex + ".png") : null;
        var nrmTex = nrm != null ? AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "Textures/" + nrm + ".png") : null;
        if (nrmTex != null && AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(nrmTex)) is TextureImporter ti && ti.textureType != TextureImporterType.NormalMap)
            nrmTex = null;

        m.SetTexture("_BaseMap", baseTex);
        m.mainTexture = baseTex;
        m.SetColor("_BaseColor", color);
        m.SetFloat("_Smoothness", smooth);
        m.SetFloat("_Metallic", metal);
        if (nrmTex != null) { m.SetTexture("_BumpMap", nrmTex); m.EnableKeyword("_NORMALMAP"); }
        else { m.SetTexture("_BumpMap", null); m.DisableKeyword("_NORMALMAP"); }
        if (emission.maxColorComponent > 0f)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", emission);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        else { m.DisableKeyword("_EMISSION"); m.SetColor("_EmissionColor", Color.black); }
        EditorUtility.SetDirty(m);
        return m;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
