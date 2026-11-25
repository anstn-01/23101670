using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 메뉴: Tools > City > Build Parking Factory
// 선택한 Plane(없으면 이름이 "Plane"인 오브젝트) 위에 차가 들어가 주차할 수 있는 간이 공장을 만듭니다.
// 데모 시티(Demo City By Versatile Studio) 텍스처 아틀라스에서 필요한 부분만 잘라 써서 도시와 같은 느낌을 냅니다.
//  - 외벽/내벽: highway_wall (콘크리트 블록 + 아래 콘크리트 띠 + 위 금속 몰딩)
//  - 지붕 윗면, 실내 바닥, 차 멈춤턱: building_bases 의 콘크리트 부분
//  - 천장: building_interior 의 천장(조명 자국) / 조명 패널 / 셔터 / 출입문
//  - 바깥 바닥(Plane): asphalt_square
// 다시 실행하면 이전에 만든 공장을 지우고 새로 만듭니다. Ctrl+Z 로 되돌릴 수 있습니다.
public static class ParkingFactoryBuilder
{
    // ---- 크기 (m) ----
    const float Width = 22f;        // 좌우 폭 (로컬 X)
    const float Depth = 26f;        // 앞뒤 길이 (로컬 Z, +Z 가 입구)
    const float Height = 7f;        // 벽 높이
    const float Wall = 0.4f;        // 벽 두께
    const float RoofT = 0.35f;      // 지붕 두께
    const float DoorW = 9f;         // 차량 입구 폭
    const float DoorH = 4.6f;       // 차량 입구 높이
    const float BaseBand = 1.0f;    // 벽 아래 콘크리트 띠 높이
    const float Cornice = 0.6f;     // 벽 위 몰딩 높이
    const float BayW = 3.6f;        // 주차칸 폭
    const float BayL = 6.2f;        // 주차칸 길이
    const int Bays = 4;             // 주차칸 수 (입구 앞 진입로 양옆에 절반씩)
    const float LotGap = 6f;        // 공장 앞벽에서 주차칸까지 거리
    const float LotInnerX = 6f;     // 진입로 가운데에서 주차칸 시작까지 거리 (진입로 폭 = 2배)

    const string Root = "Assets/메쉬/도시/Demo City By Versatile Studio/";
    const string OutDir = "Assets/메쉬/공장";
    const string FactoryName = "Parking Factory";

    // ---- 아틀라스 영역 (UV) ----
    static readonly Rect HW_Blocks = R(0f, 0.5f, 1f, 1f);
    static readonly Rect HW_Band = R(0f, 0.316f, 1f, 0.496f);
    static readonly Rect HW_Rail = R(0f, 0.186f, 1f, 0.305f);
    static readonly Rect BB_Concrete = R(0.02f, 0.02f, 0.98f, 0.44f);
    static readonly Rect BI_Ceiling = R(0.51f, 0.525f, 0.995f, 0.78f);
    static readonly Rect BI_Panels = R(0.51f, 0.02f, 0.995f, 0.16f);
    static readonly Rect BI_Shutter = R(0.575f, 0.305f, 0.812f, 0.505f);
    static readonly Rect BI_Door = R(0.506f, 0.305f, 0.567f, 0.436f);

    static readonly Rect PM_Wood = R(0.02f, 0.27f, 0.98f, 0.55f);   // props_main 나무 판자
    static readonly Rect Full = R(0f, 0f, 1f, 1f);

    // ---- 방 크기 (m) : 공장 안 오른쪽 안쪽 모서리 ----
    const float RoomW = 6f;         // 방 폭 (공장 로컬 X)
    const float RoomD = 5f;         // 방 깊이 (공장 로컬 Z)
    const float RoomH = 3f;         // 방 벽 높이
    const float RoomT = 0.15f;      // 칸막이 벽 두께
    const float RoomDoorW = 1.0f;   // 방 문 폭
    const float RoomDoorH = 2.1f;   // 방 문 높이

    static Rect R(float u0, float v0, float u1, float v1) { return Rect.MinMaxRect(u0, v0, u1, v1); }

    // ---- 재질 ----
    enum M { Wall, Concrete, Ceiling, Interior, Line, Panel, Wood, FabricWhite, FabricBlue, Metal, Count }
    static Material[] mats;

    [MenuItem("Tools/City/Build Parking Factory")]
    public static void Build()
    {
        GameObject plane = Selection.activeGameObject;
        // 공장 안의 오브젝트를 선택한 상태면 무시 (공장을 지울 때 함께 사라지므로)
        if (plane != null && plane.GetComponentsInParent<Transform>(true).Any(t => t.name == FactoryName)) plane = null;
        if (plane == null || plane.GetComponent<MeshFilter>() == null)
            plane = Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Select(t => t.gameObject).FirstOrDefault(g => g.name == "Plane");
        if (plane == null) { Debug.LogError("Plane 을 찾지 못했습니다. Plane 을 선택한 뒤 다시 실행하세요."); return; }

        EnsureFolder(OutDir);
        EnsureFolder(OutDir + "/Materials");
        EnsureFolder(OutDir + "/Meshes");
        CreateMaterials();

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Build Parking Factory");

        // 기존 공장 삭제
        foreach (var old in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                     .Where(t => t.name == FactoryName).Select(t => t.gameObject).ToArray())
            Undo.DestroyObjectImmediate(old);

        // Plane 크기 / 바닥 높이
        var pmf = plane.GetComponent<MeshFilter>();
        Bounds pb = pmf.sharedMesh.bounds;
        Vector3 ps = plane.transform.lossyScale;
        float halfX = pb.extents.x * Mathf.Abs(ps.x), halfZ = pb.extents.z * Mathf.Abs(ps.z);
        Vector3 planeCenter = plane.transform.TransformPoint(pb.center);
        float groundY = planeCenter.y;

        // 입구 방향: Plane 의 4방향 중 차/플레이어 쪽에 가장 가까운 방향
        Vector3 target = planeCenter + plane.transform.right * 10f;
        var car = Object.FindObjectsByType<CarDrive>(FindObjectsSortMode.None).FirstOrDefault();
        if (car != null) target = car.transform.position;
        else
        {
            var origin = Object.FindFirstObjectByType<Unity.XR.CoreUtils.XROrigin>();
            if (origin != null) target = origin.transform.position;
        }
        Vector3 toT = Vector3.ProjectOnPlane(target - planeCenter, Vector3.up);
        Vector3[] axes = { plane.transform.right, -plane.transform.right, plane.transform.forward, -plane.transform.forward };
        float[] halfAlong = { halfX, halfX, halfZ, halfZ };
        int best = 0;
        for (int i = 1; i < 4; i++) if (Vector3.Dot(axes[i], toT) > Vector3.Dot(axes[best], toT)) best = i;
        Vector3 front = Vector3.ProjectOnPlane(axes[best], Vector3.up).normalized;
        float halfFront = halfAlong[best];

        // 공장을 입구 반대쪽으로 밀어서 앞에 진입 공간 확보
        float back = Mathf.Max(0f, halfFront - Depth * 0.5f - 2f);
        float push = Mathf.Min(back, halfFront * 0.35f);
        Vector3 pos = planeCenter - front * push;
        pos.y = groundY;

        var root = new GameObject(FactoryName);
        Undo.RegisterCreatedObjectUndo(root, "Create Factory");
        root.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(front, Vector3.up));

        BuildShell(root.transform);
        BuildInterior(root.transform);
        BuildLights(root.transform);
        BuildProps(root.transform, groundY);
        BuildRoom(root.transform);

        // 바깥 바닥(Plane)에 아스팔트
        var pr = plane.GetComponent<MeshRenderer>();
        if (pr != null)
        {
            Undo.RecordObject(pr, "Plane Material");
            pr.sharedMaterial = GroundMaterial(Mathf.Max(halfX, halfZ) * 2f);
        }

        Undo.CollapseUndoOperations(group);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(root.scene);
        Selection.activeGameObject = root;
        Debug.Log($"주차 공장 생성 완료: 위치 {pos}, 입구 방향 {front}. 크기 {Width}x{Depth}m, 입구 {DoorW}x{DoorH}m, 주차칸 {Bays}개.");
    }

    // =====================================================================
    // 건물 외곽: 벽 / 입구 / 지붕
    // =====================================================================
    static void BuildShell(Transform root)
    {
        float hx = Width * 0.5f, hz = Depth * 0.5f;

        // 뒷벽 (z = -hz), 왼쪽(x = -hx), 오른쪽(x = +hx)
        WallSegment(root, "Wall Back", new Vector3(-hx, 0, -hz), Vector3.right, Width, 0f, Height, true, true);
        WallSegment(root, "Wall Left", new Vector3(-hx, 0, hz), Vector3.back, Depth, 0f, Height, true, true);
        WallSegment(root, "Wall Right", new Vector3(hx, 0, -hz), Vector3.forward, Depth, 0f, Height, true, true);

        // 앞벽: 입구 양옆 기둥 + 입구 위 상인방
        float side = (Width - DoorW) * 0.5f;
        WallSegment(root, "Wall Front L", new Vector3(hx, 0, hz), Vector3.left, side, 0f, Height, true, false, 0f);
        WallSegment(root, "Wall Front R", new Vector3(-hx + side, 0, hz), Vector3.left, side, 0f, Height, false, true, Width - side);
        WallSegment(root, "Wall Front Lintel", new Vector3(DoorW * 0.5f, 0, hz), Vector3.left, DoorW, DoorH, Height, false, false, side);

        // 지붕
        var mb = new MB();
        Vector3 c0 = new Vector3(-hx, Height, -hz);
        // 윗면 (콘크리트)
        mb.Tiled((int)M.Concrete, new Vector3(-hx, Height + RoofT, -hz), Vector3.right, Vector3.forward, 0, Width, 0, Depth, 6f, 6f, BB_Concrete);
        // 천장 (실내 천장 텍스처, 아래를 봄)
        mb.Tiled((int)M.Interior, new Vector3(-hx + Wall, Height, hz - Wall), Vector3.right, Vector3.back, 0, Width - Wall * 2, 0, Depth - Wall * 2, 5.5f, 3f, BI_Ceiling);
        // 지붕 가장자리 (금속 몰딩, 바깥을 봄)
        mb.Tiled((int)M.Wall, new Vector3(hx, Height, hz), Vector3.left, Vector3.up, 0, Width, 0, RoofT, 6f, RoofT, HW_Rail);     // 앞
        mb.Tiled((int)M.Wall, new Vector3(-hx, Height, -hz), Vector3.right, Vector3.up, 0, Width, 0, RoofT, 6f, RoofT, HW_Rail);  // 뒤
        mb.Tiled((int)M.Wall, new Vector3(-hx, Height, hz), Vector3.back, Vector3.up, 0, Depth, 0, RoofT, 6f, RoofT, HW_Rail);    // 왼쪽
        mb.Tiled((int)M.Wall, new Vector3(hx, Height, -hz), Vector3.forward, Vector3.up, 0, Depth, 0, RoofT, 6f, RoofT, HW_Rail); // 오른쪽
        var roof = Part(root, "Roof", mb);
        var bc = roof.AddComponent<BoxCollider>();
        bc.center = new Vector3(0, Height + RoofT * 0.5f, 0);
        bc.size = new Vector3(Width, RoofT, Depth);

        // 입구 위 셔터 (말려 올라간 셔터 통)
        var sh = new MB();
        float sy = DoorH - 0.7f, sz = hz - Wall - 0.35f;
        BoxFaces(sh, new Vector3(0, sy + 0.35f, sz), new Vector3(DoorW, 0.7f, 0.5f),
            front: ((int)M.Interior, BI_Shutter, DoorW, 0.7f), other: ((int)M.Concrete, BB_Concrete, 4f, 4f));
        Part(root, "Shutter Box", sh);
    }

    // 벽 한 조각. origin 에서 dir 방향으로 length 만큼, 높이 y0~y1. 바깥은 dir 의 오른쪽(시계방향) 법선 쪽.
    static void WallSegment(Transform root, string name, Vector3 origin, Vector3 dir, float length, float y0, float y1,
        bool capStart, bool capEnd, float uStart = 0f)
    {
        Vector3 outN = Vector3.Cross(Vector3.up, dir).normalized; // dir 기준 바깥 방향
        // 바깥 면 기준선 (벽 바깥쪽 면), 안쪽 면은 Wall 만큼 안으로
        Vector3 outer = origin;
        Vector3 inner = origin - outN * Wall;

        var mb = new MB();
        // 바깥 면: 왼쪽→오른쪽 = dir
        WallFace(mb, outer, dir, length, y0, y1, uStart);
        // 안쪽 면: 반대 방향으로 감기
        WallFace(mb, inner + dir * length, -dir, length, y0, y1, uStart);

        // 위 (벽 윗면 콘크리트) - 지붕 아래라 거의 안 보이지만 닫아둠
        mb.Tiled((int)M.Concrete, outer + Vector3.up * y1, dir, -outN, 0, length, 0, Wall, 4f, 4f, BB_Concrete);
        // 아래 (상인방 밑면)
        if (y0 > 0.01f) mb.Tiled((int)M.Concrete, inner + Vector3.up * y0, dir, outN, 0, length, 0, Wall, 4f, 4f, BB_Concrete);
        // 양 끝 단면 (입구 옆 기둥 등)
        if (capStart) mb.Tiled((int)M.Wall, inner + Vector3.up * y0, outN, Vector3.up, 0, Wall, 0, y1 - y0, 4f, 1f, HW_Band);
        if (capEnd) mb.Tiled((int)M.Wall, outer + dir * length + Vector3.up * y0, -outN, Vector3.up, 0, Wall, 0, y1 - y0, 4f, 1f, HW_Band);

        var go = Part(root, name, mb);
        var bc = go.AddComponent<BoxCollider>();
        Vector3 mid = (outer + inner) * 0.5f + dir * length * 0.5f + Vector3.up * (y0 + y1) * 0.5f;
        bc.center = mid;
        Vector3 size = new Vector3(Mathf.Abs(dir.x) * length + Mathf.Abs(outN.x) * Wall, y1 - y0, Mathf.Abs(dir.z) * length + Mathf.Abs(outN.z) * Wall);
        bc.size = size;
    }

    // 벽 면: 아래 콘크리트 띠 / 블록 / 위 몰딩
    static void WallFace(MB mb, Vector3 origin, Vector3 dir, float length, float y0, float y1, float uStart)
    {
        float bandTop = BaseBand, railBottom = Height - Cornice;
        float a = Mathf.Max(y0, 0f), b = Mathf.Min(y1, bandTop);
        if (b > a) mb.Tiled((int)M.Wall, origin, dir, Vector3.up, uStart, uStart + length, a, b, 6f, BaseBand, HW_Band, uStart);
        a = Mathf.Max(y0, bandTop); b = Mathf.Min(y1, railBottom);
        if (b > a) mb.Tiled((int)M.Wall, origin, dir, Vector3.up, uStart, uStart + length, a, b, 6f, 3f, HW_Blocks, uStart);
        a = Mathf.Max(y0, railBottom); b = y1;
        if (b > a) mb.Tiled((int)M.Wall, origin, dir, Vector3.up, uStart, uStart + length, a, b, 6f, Cornice, HW_Rail, uStart);
    }

    // =====================================================================
    // 실내: 콘크리트 바닥 / 주차선 / 멈춤턱 / 출입문
    // =====================================================================
    static void BuildInterior(Transform root)
    {
        float hx = Width * 0.5f, hz = Depth * 0.5f;
        float y = 0.015f;

        var floor = new MB();
        floor.Tiled((int)M.Concrete, new Vector3(-hx + Wall, y, -hz + Wall), Vector3.right, Vector3.forward, 0, Width - Wall * 2, 0, Depth - Wall, 4f, 4f, BB_Concrete);
        // 입구 앞 진입로 (밖으로 3m)
        floor.Tiled((int)M.Concrete, new Vector3(-DoorW * 0.5f - 1f, y, hz), Vector3.right, Vector3.forward, 0, DoorW + 2f, 0, 3f, 4f, 4f, BB_Concrete);
        Part(root, "Floor", floor);

        // 바깥 주차장: 입구 앞마당, 진입로 양옆에 Bays/2 칸씩. 차 앞부분이 공장 쪽을 보고 주차
        var lines = new MB();
        var stops = new MB();
        float lw = 0.12f, ly = 0.02f;
        int perSide = Mathf.Max(1, Bays / 2);
        float rowW = perSide * BayW;
        foreach (float sign in new[] { -1f, 1f })
        {
            float xa = sign * LotInnerX;                 // 진입로 쪽 끝
            float xMin = Mathf.Min(xa, xa + sign * rowW);
            // 칸 세로선
            for (int i = 0; i <= perSide; i++)
            {
                float x = xa + sign * i * BayW;
                lines.Quad((int)M.Line, new Vector3(x - lw * 0.5f, ly, hz + LotGap), Vector3.right, Vector3.forward, lw, BayL, R(0, 0, 1, 1));
            }
            // 공장 쪽 가로선
            lines.Quad((int)M.Line, new Vector3(xMin - lw * 0.5f, ly, hz + LotGap), Vector3.right, Vector3.forward, rowW + lw, lw, R(0, 0, 1, 1));
            // 멈춤턱 (콜라이더 없음)
            for (int i = 0; i < perSide; i++)
            {
                float cx = xa + sign * (i + 0.5f) * BayW;
                BoxFaces(stops, new Vector3(cx, 0.08f, hz + LotGap + 0.7f), new Vector3(1.8f, 0.16f, 0.25f),
                    front: ((int)M.Concrete, BB_Concrete, 2f, 2f), other: ((int)M.Concrete, BB_Concrete, 2f, 2f));
            }
        }
        // 입구 안쪽 정지선
        lines.Quad((int)M.Line, new Vector3(-DoorW * 0.5f, y + 0.005f, hz - Wall - 0.8f), Vector3.right, Vector3.forward, DoorW, 0.2f, R(0, 0, 1, 1));
        Part(root, "Parking Lines", lines);
        Part(root, "Wheel Stops", stops);

        // 사람 출입문 (앞벽 바깥, 입구 오른쪽)
        var door = new MB();
        float side = (Width - DoorW) * 0.5f;
        float dx = -hx + side * 0.5f;
        door.Quad((int)M.Interior, new Vector3(dx + 0.6f, 0.02f, hz + 0.01f), Vector3.left, Vector3.up, 1.2f, 2.4f, BI_Door);
        // 안쪽 면에도
        door.Quad((int)M.Interior, new Vector3(dx - 0.6f, 0.02f, hz - Wall - 0.01f), Vector3.right, Vector3.up, 1.2f, 2.4f, BI_Door);
        Part(root, "Side Door", door);
    }

    // =====================================================================
    // 조명: 천장 조명 패널 + 포인트 라이트, 입구 밖 조명
    // =====================================================================
    static void BuildLights(Transform root)
    {
        float hz = Depth * 0.5f;
        var panels = new MB();
        var lightsRoot = new GameObject("Lights");
        lightsRoot.transform.SetParent(root, false);

        float[] xs = { -Width * 0.25f, Width * 0.25f };
        float[] zs = { -hz * 0.6f, 0f, hz * 0.6f };
        foreach (float x in xs)
            foreach (float z in zs)
            {
                // 천장 아래 조명 패널
                panels.Quad((int)M.Panel, new Vector3(x - 1.5f, Height - 0.02f, z + 0.5f), Vector3.right, Vector3.back, 3f, 1f, BI_Panels);
                var l = new GameObject("Ceiling Light").AddComponent<Light>();
                l.transform.SetParent(lightsRoot.transform, false);
                l.transform.localPosition = new Vector3(x, Height - 0.4f, z);
                l.type = LightType.Point;
                l.range = 13f;
                l.intensity = 4f;
                l.color = new Color(1f, 0.93f, 0.8f);
                l.shadows = LightShadows.None;
            }
        Part(root, "Ceiling Panels", panels);

        // 입구 위 바깥 조명 (도시 가로등과 비슷한 주황빛)
        var s = new GameObject("Entrance Light").AddComponent<Light>();
        s.transform.SetParent(lightsRoot.transform, false);
        s.transform.localPosition = new Vector3(0, DoorH + 1.2f, hz + 1.2f);
        s.transform.localRotation = Quaternion.LookRotation(new Vector3(0, -1f, 0.6f));
        s.type = LightType.Spot;
        s.spotAngle = 100f;
        s.range = 18f;
        s.intensity = 6f;
        s.color = new Color(1f, 0.7f, 0.4f);
    }

    // =====================================================================
    // 데모 시티 프리팹 소품: 굴뚝, 벽등, 콘크리트 블록
    // =====================================================================
    static void BuildProps(Transform root, float groundY)
    {
        float hx = Width * 0.5f, hz = Depth * 0.5f;
        var propsRoot = new GameObject("Props");
        propsRoot.transform.SetParent(root, false);

        // 공장 굴뚝 (뒤쪽 모서리 바깥)
        var chim = Spawn("factory_chimney-stalk", propsRoot.transform);
        if (chim != null)
        {
            chim.transform.localPosition = new Vector3(-hx - 3f, 0, -hz + 3f);
            SitOnGround(chim, groundY);
            // 굴뚝 밑동이 커서 공장 벽 안으로 파고들지 않도록 왼쪽(-X) 벽 바깥으로 밀어냄
            var rs = chim.GetComponentsInChildren<Renderer>();
            if (rs.Length > 0)
            {
                float maxX = float.MinValue;
                foreach (var r in rs)
                {
                    Bounds b = r.bounds;
                    for (int i = 0; i < 8; i++)
                    {
                        Vector3 c = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                        maxX = Mathf.Max(maxX, root.InverseTransformPoint(c).x);
                    }
                }
                float limit = -hx - 1f;
                if (maxX > limit) chim.transform.localPosition += Vector3.left * (maxX - limit);
            }
        }

        // 입구 양옆 벽등 (프리팹의 +Z 가 벽 바깥을 향하도록)
        foreach (float x in new[] { -DoorW * 0.5f - 1f, DoorW * 0.5f + 1f })
        {
            var lamp = Spawn("building_lamp_orange", propsRoot.transform);
            if (lamp == null) continue;
            lamp.transform.localPosition = new Vector3(x, DoorH + 0.6f, hz);
            lamp.transform.localRotation = Quaternion.identity;
        }

        // 콘크리트 블록 (입구 앞 양쪽, 벽 옆)
        Vector3[] blocks =
        {
            new Vector3(-DoorW * 0.5f - 2.2f, 0, hz + 2.5f), new Vector3(DoorW * 0.5f + 2.2f, 0, hz + 2.5f),
            new Vector3(hx + 1.2f, 0, -hz * 0.3f), new Vector3(hx + 1.2f, 0, hz * 0.2f),
        };
        foreach (var p in blocks)
        {
            var b = Spawn("concrete_block", propsRoot.transform);
            if (b == null) continue;
            b.transform.localPosition = p;
            SitOnGround(b, groundY);
        }

        // 바깥 주차장 가로등 (주차칸 바깥쪽 끝)
        float lotOuter = LotInnerX + Mathf.Max(1, Bays / 2) * BayW + 1.5f;
        foreach (float sign in new[] { -1f, 1f })
        {
            var pole = Spawn("lamp_pole_single_orange", propsRoot.transform);
            if (pole == null) continue;
            pole.transform.localPosition = new Vector3(sign * lotOuter, 0, hz + LotGap + BayL * 0.5f);
            pole.transform.localRotation = Quaternion.Euler(0, sign > 0 ? -90f : 90f, 0);
            SitOnGround(pole, groundY);
        }

        // 프리팹에 콜라이더가 없으면 BoxCollider 추가 (차가 뚫지 않게)
        foreach (var r in propsRoot.GetComponentsInChildren<MeshRenderer>())
            if (r.GetComponent<Collider>() == null && r.GetComponentInParent<Light>() == null)
                r.gameObject.AddComponent<BoxCollider>();
    }

    // =====================================================================
    // 방: 공장 안 오른쪽 안쪽 모서리 (입구에서 들어와 보면 오른쪽 안쪽)
    //  - 칸막이 벽 2면 + 천장, 나무 바닥, 조명
    //  - 침대, 책상, 의자, 스탠드
    //  - 손잡이를 잡고 여닫는 문 (CarDoorHinge 재사용, 안쪽 손잡이 자동 생성)
    // =====================================================================
    static void BuildRoom(Transform root)
    {
        float hx = Width * 0.5f, hz = Depth * 0.5f;
        float x0 = -hx + Wall, x1 = x0 + RoomW;   // 방 안쪽 X 범위 (x0 = 공장 왼쪽(-X) 벽 안쪽면)
        float z0 = -hz + Wall, z1 = z0 + RoomD;   // 방 안쪽 Z 범위 (z0 = 공장 뒷벽 안쪽면)
        float T = RoomT, H = RoomH;

        var room = new GameObject("Room");
        room.transform.SetParent(root, false);

        // ---- 칸막이 벽 ----
        var wm = new MB();
        var colliders = new List<(Vector3 c, Vector3 s)>();
        // 옆벽 (방의 +X 쪽, 바깥이 +X)
        RoomWall(wm, colliders, new Vector3(x1 + T, 0, z0), Vector3.forward, RoomD + T, 0, H, false, true);
        // 앞벽 (방의 +Z 쪽, 바깥이 +Z) : 문 구멍을 남기고 3조각
        float doorU0 = 0.7f, doorU1 = doorU0 + RoomDoorW;           // 앞벽 시작(x1)에서 문까지 거리
        Vector3 fStart = new Vector3(x1, 0, z1 + T);
        RoomWall(wm, colliders, fStart, Vector3.left, doorU0, 0, H, false, true);
        RoomWall(wm, colliders, fStart + Vector3.left * doorU1, Vector3.left, RoomW - doorU1, 0, H, true, false);
        RoomWall(wm, colliders, fStart + Vector3.left * doorU0, Vector3.left, RoomDoorW, RoomDoorH, H, false, false);
        var walls = Part(room.transform, "Room Walls", wm);
        foreach (var (c, s) in colliders) { var bc = walls.AddComponent<BoxCollider>(); bc.center = c; bc.size = s; }

        // ---- 천장 ----
        var cm = new MB();
        float ct = 0.12f;
        cm.Tiled((int)M.Concrete, new Vector3(x0, H + ct, z0), Vector3.right, Vector3.forward, 0, RoomW + T, 0, RoomD + T, 3f, 3f, BB_Concrete);
        cm.Tiled((int)M.Interior, new Vector3(x0, H, z1), Vector3.right, Vector3.back, 0, RoomW, 0, RoomD, 3f, 1.5f, BI_Ceiling);
        cm.Tiled((int)M.Wall, new Vector3(x1 + T, H, z1 + T), Vector3.left, Vector3.up, 0, RoomW + T, 0, ct, 6f, ct, HW_Rail);
        cm.Tiled((int)M.Wall, new Vector3(x1 + T, H, z0), Vector3.forward, Vector3.up, 0, RoomD + T, 0, ct, 6f, ct, HW_Rail);
        // 조명 패널
        float cx = (x0 + x1) * 0.5f, cz = (z0 + z1) * 0.5f;
        cm.Quad((int)M.Panel, new Vector3(cx - 0.75f, H - 0.01f, cz + 0.25f), Vector3.right, Vector3.back, 1.5f, 0.5f, BI_Panels);
        var ceil = Part(room.transform, "Room Ceiling", cm);
        var cbc = ceil.AddComponent<BoxCollider>();
        cbc.center = new Vector3((x0 + x1 + T) * 0.5f, H + ct * 0.5f, (z0 + z1 + T) * 0.5f);
        cbc.size = new Vector3(RoomW + T, ct, RoomD + T);

        // ---- 나무 바닥 ----
        var fm = new MB();
        fm.Tiled((int)M.Wood, new Vector3(x0, 0.025f, z0), Vector3.right, Vector3.forward, 0, RoomW, 0, RoomD, 2f, 0.6f, PM_Wood);
        Part(room.transform, "Room Floor", fm);

        // ---- 가구 ----
        var fu = new MB();
        var fcol = new List<(Vector3 c, Vector3 s)>();
        // 침대: 왼쪽 벽에 붙이고 머리는 뒷벽 쪽
        float bx = x0 + 0.8f, bz = z0 + 1.12f;
        Box(fu, (int)M.Wood, new Vector3(bx, 0.2f, bz), new Vector3(1.5f, 0.3f, 2.1f), PM_Wood, 1f, 0.5f);          // 프레임
        Box(fu, (int)M.FabricWhite, new Vector3(bx, 0.45f, bz + 0.02f), new Vector3(1.4f, 0.2f, 2.0f), Full, 10f, 10f); // 매트리스
        Box(fu, (int)M.FabricBlue, new Vector3(bx, 0.575f, bz + 0.35f), new Vector3(1.44f, 0.06f, 1.35f), Full, 10f, 10f); // 이불
        Box(fu, (int)M.FabricWhite, new Vector3(bx, 0.61f, z0 + 0.36f), new Vector3(0.9f, 0.12f, 0.4f), Full, 10f, 10f);   // 베개
        Box(fu, (int)M.Wood, new Vector3(bx, 0.55f, z0 + 0.09f), new Vector3(1.5f, 1.0f, 0.08f), PM_Wood, 1f, 0.5f);       // 헤드보드
        fcol.Add((new Vector3(bx, 0.33f, bz), new Vector3(1.5f, 0.66f, 2.1f)));

        // 침대 옆 협탁
        Box(fu, (int)M.Wood, new Vector3(bx + 1.05f, 0.25f, z0 + 0.3f), new Vector3(0.5f, 0.5f, 0.45f), PM_Wood, 1f, 0.5f);
        fcol.Add((new Vector3(bx + 1.05f, 0.25f, z0 + 0.3f), new Vector3(0.5f, 0.5f, 0.45f)));

        // 책상: 뒷벽, 옆벽 쪽
        float dx = x1 - 0.9f, dz = z0 + 0.4f;
        Box(fu, (int)M.Wood, new Vector3(dx, 0.74f, dz), new Vector3(1.4f, 0.04f, 0.7f), PM_Wood, 1f, 0.5f);
        foreach (float sx in new[] { -0.66f, 0.66f })
            foreach (float sz in new[] { -0.31f, 0.31f })
                Box(fu, (int)M.Metal, new Vector3(dx + sx, 0.36f, dz + sz), new Vector3(0.05f, 0.72f, 0.05f), Full, 10f, 10f);
        // 서랍
        Box(fu, (int)M.Wood, new Vector3(dx + 0.4f, 0.55f, dz), new Vector3(0.5f, 0.3f, 0.6f), PM_Wood, 1f, 0.5f);
        Box(fu, (int)M.Metal, new Vector3(dx + 0.4f, 0.55f, dz + 0.31f), new Vector3(0.18f, 0.025f, 0.02f), Full, 10f, 10f);
        fcol.Add((new Vector3(dx, 0.38f, dz), new Vector3(1.4f, 0.76f, 0.7f)));

        // 스탠드
        Box(fu, (int)M.Metal, new Vector3(dx - 0.5f, 0.77f, dz - 0.15f), new Vector3(0.16f, 0.02f, 0.16f), Full, 10f, 10f);
        Box(fu, (int)M.Metal, new Vector3(dx - 0.5f, 0.95f, dz - 0.15f), new Vector3(0.02f, 0.35f, 0.02f), Full, 10f, 10f);
        Box(fu, (int)M.FabricWhite, new Vector3(dx - 0.5f, 1.14f, dz - 0.15f), new Vector3(0.16f, 0.1f, 0.16f), Full, 10f, 10f);

        // 의자
        float chz = dz + 0.75f;
        Box(fu, (int)M.Wood, new Vector3(dx, 0.45f, chz), new Vector3(0.46f, 0.05f, 0.46f), PM_Wood, 1f, 0.5f);
        Box(fu, (int)M.Wood, new Vector3(dx, 0.73f, chz + 0.21f), new Vector3(0.46f, 0.5f, 0.04f), PM_Wood, 1f, 0.5f);
        foreach (float sx in new[] { -0.2f, 0.2f })
            foreach (float sz in new[] { -0.2f, 0.2f })
                Box(fu, (int)M.Metal, new Vector3(dx + sx, 0.21f, chz + sz), new Vector3(0.04f, 0.42f, 0.04f), Full, 10f, 10f);
        fcol.Add((new Vector3(dx, 0.48f, chz + 0.02f), new Vector3(0.46f, 0.96f, 0.5f)));

        var furn = Part(room.transform, "Furniture", fu);
        foreach (var (c, s) in fcol) { var bc = furn.AddComponent<BoxCollider>(); bc.center = c; bc.size = s; }

        // ---- 조명 ----
        var l = new GameObject("Room Light").AddComponent<Light>();
        l.transform.SetParent(room.transform, false);
        l.transform.localPosition = new Vector3(cx, H - 0.3f, cz);
        l.type = LightType.Point; l.range = 7f; l.intensity = 2.5f; l.color = new Color(1f, 0.9f, 0.75f);
        l.shadows = LightShadows.None;
        var dl = new GameObject("Desk Lamp Light").AddComponent<Light>();
        dl.transform.SetParent(room.transform, false);
        dl.transform.localPosition = new Vector3(dx - 0.5f, 1.05f, dz - 0.15f);
        dl.type = LightType.Point; dl.range = 2f; dl.intensity = 1.2f; dl.color = new Color(1f, 0.8f, 0.55f);
        dl.shadows = LightShadows.None;

        // ---- 문 (손잡이를 잡고 여닫기) ----
        float xd = x1 - (doorU0 + RoomDoorW * 0.5f);   // 문 가운데 X
        float zd = z1 + T * 0.5f;                       // 벽 두께 가운데
        float dw = RoomDoorW - 0.04f, dh = RoomDoorH - 0.04f, dt = 0.05f;
        float handleX = -(dw * 0.5f - 0.12f);           // 손잡이는 -X 쪽, 경첩은 +X 쪽

        var dm = new MB();
        Box(dm, (int)M.Wood, new Vector3(0, 0.02f + dh * 0.5f, 0), new Vector3(dw, dh, dt), PM_Wood, 1f, 0.5f);
        Box(dm, (int)M.Metal, new Vector3(handleX, 1.0f, dt * 0.5f + 0.02f), new Vector3(0.14f, 0.03f, 0.04f), Full, 10f, 10f);
        Box(dm, (int)M.Metal, new Vector3(handleX, 1.0f, -dt * 0.5f - 0.02f), new Vector3(0.14f, 0.03f, 0.04f), Full, 10f, 10f);
        var door = Part(room.transform, "Room Door", dm);
        door.transform.localPosition = new Vector3(xd, 0, zd);
        var dbc = door.AddComponent<BoxCollider>();
        dbc.center = new Vector3(0, 0.02f + dh * 0.5f, 0);
        dbc.size = new Vector3(dw, dh, dt);

        // 바깥 손잡이 (콜라이더 X축 = 문 두께 방향). 안쪽 손잡이는 CarDoorHinge 가 시작할 때 자동으로 만듦
        var handle = new GameObject("Door Handle");
        handle.transform.SetParent(door.transform, false);
        handle.transform.localPosition = new Vector3(handleX, 1.0f, dt * 0.5f + 0.04f);
        handle.transform.localRotation = Quaternion.LookRotation(Vector3.left, Vector3.up); // right = +Z
        var hbc = handle.AddComponent<BoxCollider>();
        hbc.size = new Vector3(0.1f, 0.22f, 0.25f);
        var hi = handle.AddComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable>();

        var hinge = door.AddComponent<CarDoorHinge>();
        hinge.handle = hi;
        hinge.hingeLocal = new Vector3(xd + dw * 0.5f, 1.0f, zd);   // 방(Room) 기준 좌표
        hinge.axisLocal = Vector3.up;
        hinge.closedDirLocal = Vector3.left;                        // 경첩 → 손잡이
        hinge.openSign = -1f;                                       // 방 안쪽(-Z)으로 열림
        hinge.maxAngle = 100f;
        hinge.autoCloseAngle = 5f;
        hinge.angle = 0f;
        hinge.createInnerHandle = true;
    }

    // 방 칸막이 벽 한 조각 (바깥면: 공장 벽과 같은 블록, 안쪽면: 콘크리트)
    static void RoomWall(MB mb, List<(Vector3 c, Vector3 s)> cols, Vector3 start, Vector3 dir, float len, float y0, float y1, bool capStart, bool capEnd)
    {
        float T = RoomT;
        Vector3 outN = Vector3.Cross(Vector3.up, dir).normalized;
        Vector3 outer = start, inner = start - outN * T;
        WallFace(mb, outer, dir, len, y0, y1, 0f);
        mb.Tiled((int)M.Concrete, inner + dir * len, -dir, Vector3.up, 0, len, y0, y1, 3f, 3f, BB_Concrete);
        mb.Tiled((int)M.Concrete, outer + Vector3.up * y1, dir, -outN, 0, len, 0, T, 2f, 2f, BB_Concrete);
        if (y0 > 0.01f) mb.Tiled((int)M.Concrete, inner + Vector3.up * y0, dir, outN, 0, len, 0, T, 2f, 2f, BB_Concrete);
        if (capStart) mb.Tiled((int)M.Concrete, inner + Vector3.up * y0, outN, Vector3.up, 0, T, 0, y1 - y0, 2f, 2f, BB_Concrete);
        if (capEnd) mb.Tiled((int)M.Concrete, outer + dir * len + Vector3.up * y0, -outN, Vector3.up, 0, T, 0, y1 - y0, 2f, 2f, BB_Concrete);
        Vector3 c = (outer + inner) * 0.5f + dir * len * 0.5f + Vector3.up * (y0 + y1) * 0.5f;
        Vector3 s = new Vector3(Mathf.Abs(dir.x) * len + Mathf.Abs(outN.x) * T, y1 - y0, Mathf.Abs(dir.z) * len + Mathf.Abs(outN.z) * T);
        cols.Add((c, s));
    }

    static void Box(MB mb, int m, Vector3 c, Vector3 s, Rect r, float tw, float th)
    {
        BoxFaces(mb, c, s, (m, r, tw, th), (m, r, tw, th));
    }

    static GameObject Spawn(string prefab, Transform parent)
    {
        var p = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Prefabs/" + prefab + ".prefab");
        if (p == null) { Debug.LogWarning($"프리팹 없음: {prefab}"); return null; }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(p, parent);
        go.transform.localRotation = Quaternion.identity;
        return go;
    }

    static void SitOnGround(GameObject go, float groundY)
    {
        var rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return;
        Bounds b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        go.transform.position += Vector3.up * (groundY - b.min.y);
    }

    // =====================================================================
    // 메쉬 / 재질 유틸
    // =====================================================================
    static GameObject Part(Transform root, string name, MB mb)
    {
        var go = new GameObject(name);
        go.transform.SetParent(root, false);
        var mesh = mb.ToMesh(name);
        string path = $"{OutDir}/Meshes/Factory_{name.Replace(' ', '_')}.asset";
        // 이전 메쉬 파일은 지우고 새로 저장 (덮어쓰기로는 모양이 갱신되지 않음)
        if (AssetDatabase.LoadAssetAtPath<Mesh>(path) != null) AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(mesh, path);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterials = mb.UsedMaterials().Select(i => mats[i]).ToArray();
        return go;
    }

    // 상자 6면 (front = 로컬 +Z 면)
    static void BoxFaces(MB mb, Vector3 c, Vector3 s, (int m, Rect r, float tw, float th) front, (int m, Rect r, float tw, float th) other)
    {
        Vector3 h = s * 0.5f;
        // +Z
        mb.Tiled(front.m, c + new Vector3(h.x, -h.y, h.z), Vector3.left, Vector3.up, 0, s.x, 0, s.y, front.tw, front.th, front.r);
        // -Z
        mb.Tiled(other.m, c + new Vector3(-h.x, -h.y, -h.z), Vector3.right, Vector3.up, 0, s.x, 0, s.y, other.tw, other.th, other.r);
        // +X
        mb.Tiled(other.m, c + new Vector3(h.x, -h.y, -h.z), Vector3.forward, Vector3.up, 0, s.z, 0, s.y, other.tw, other.th, other.r);
        // -X
        mb.Tiled(other.m, c + new Vector3(-h.x, -h.y, h.z), Vector3.back, Vector3.up, 0, s.z, 0, s.y, other.tw, other.th, other.r);
        // +Y
        mb.Tiled(other.m, c + new Vector3(-h.x, h.y, -h.z), Vector3.right, Vector3.forward, 0, s.x, 0, s.z, other.tw, other.th, other.r);
        // -Y
        mb.Tiled(other.m, c + new Vector3(-h.x, -h.y, h.z), Vector3.right, Vector3.back, 0, s.x, 0, s.z, other.tw, other.th, other.r);
    }

    class MB
    {
        readonly List<Vector3> v = new List<Vector3>();
        readonly List<Vector3> n = new List<Vector3>();
        readonly List<Vector2> uv = new List<Vector2>();
        readonly Dictionary<int, List<int>> tris = new Dictionary<int, List<int>>();

        // origin + uDir*u + vDir*v 평면에서 [u0,u1]x[v0,v1] 구간을 타일(tw x th) 단위로 쪼개 아틀라스 rect 를 반복 매핑.
        // 면의 앞쪽 = Cross(vDir, uDir) 방향 (uDir 이 오른쪽, vDir 이 위로 보이는 쪽)
        // uOffset: origin 에서의 u 시작값 보정 (벽 조각끼리 무늬가 이어지도록)
        public void Tiled(int mat, Vector3 origin, Vector3 uDir, Vector3 vDir, float u0, float u1, float v0, float v1,
            float tw, float th, Rect rect, float uOffset = 0f)
        {
            Vector3 normal = Vector3.Cross(vDir, uDir).normalized;
            for (float ua = u0; ua < u1 - 1e-4f;)
            {
                float uNext = Mathf.Min(u1, (Mathf.Floor(ua / tw + 1e-4f) + 1f) * tw);
                for (float va = v0; va < v1 - 1e-4f;)
                {
                    float vNext = Mathf.Min(v1, (Mathf.Floor(va / th + 1e-4f) + 1f) * th);
                    float fu0 = Frac(ua / tw), fu1 = fu0 + (uNext - ua) / tw;
                    float fv0 = Frac(va / th), fv1 = fv0 + (vNext - va) / th;
                    Vector3 p00 = origin + uDir * (ua - uOffset) + vDir * va;
                    Vector3 p10 = origin + uDir * (uNext - uOffset) + vDir * va;
                    Vector3 p11 = origin + uDir * (uNext - uOffset) + vDir * vNext;
                    Vector3 p01 = origin + uDir * (ua - uOffset) + vDir * vNext;
                    Add(mat, p00, p10, p11, p01, normal,
                        Lerp(rect, fu0, fv0), Lerp(rect, fu1, fv0), Lerp(rect, fu1, fv1), Lerp(rect, fu0, fv1));
                    va = vNext;
                }
                ua = uNext;
            }
        }

        public void Quad(int mat, Vector3 origin, Vector3 uDir, Vector3 vDir, float w, float h, Rect rect)
        {
            Tiled(mat, origin, uDir, vDir, 0, w, 0, h, w, h, rect);
        }

        static float Frac(float x) { float f = x - Mathf.Floor(x + 1e-5f); return f < 0 ? 0 : f; }
        static Vector2 Lerp(Rect r, float x, float y) { return new Vector2(r.xMin + r.width * x, r.yMin + r.height * y); }

        void Add(int mat, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 nrm, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud)
        {
            int i = v.Count;
            v.Add(a); v.Add(b); v.Add(c); v.Add(d);
            for (int k = 0; k < 4; k++) n.Add(nrm);
            uv.Add(ua); uv.Add(ub); uv.Add(uc); uv.Add(ud);
            if (!tris.TryGetValue(mat, out var t)) tris[mat] = t = new List<int>();
            // 앞면이 nrm 쪽을 보도록 감기 방향 선택
            Vector3 face = Vector3.Cross(b - a, c - a);
            if (Vector3.Dot(face, nrm) >= 0f) { t.Add(i); t.Add(i + 1); t.Add(i + 2); t.Add(i); t.Add(i + 2); t.Add(i + 3); }
            else { t.Add(i); t.Add(i + 2); t.Add(i + 1); t.Add(i); t.Add(i + 3); t.Add(i + 2); }
        }

        public int[] UsedMaterials() { return tris.Keys.OrderBy(k => k).ToArray(); }

        public Mesh ToMesh(string name)
        {
            var m = new Mesh { name = "Factory_" + name };
            m.indexFormat = v.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            m.SetVertices(v);
            m.SetNormals(n);
            m.SetUVs(0, uv);
            var keys = UsedMaterials();
            m.subMeshCount = keys.Length;
            for (int i = 0; i < keys.Length; i++) m.SetTriangles(tris[keys[i]], i);
            m.RecalculateTangents();
            m.RecalculateBounds();
            Unwrapping.GenerateSecondaryUVSet(m);
            return m;
        }
    }

    static void CreateMaterials()
    {
        mats = new Material[(int)M.Count];
        mats[(int)M.Wall] = Mat("Factory_Wall", "highway_wall", "highway_wall_nm", Color.white, 0.15f);
        mats[(int)M.Concrete] = Mat("Factory_Concrete", "building_bases", "building_bases_nm", new Color(0.92f, 0.92f, 0.92f), 0.1f);
        mats[(int)M.Interior] = Mat("Factory_Interior", "building_interior", "building_interior_nm", Color.white, 0.2f);
        mats[(int)M.Ceiling] = mats[(int)M.Interior];
        mats[(int)M.Line] = Mat("Factory_ParkingLine", null, null, new Color(0.95f, 0.95f, 0.9f), 0.3f, new Color(0.25f, 0.25f, 0.23f));
        var panel = Mat("Factory_LightPanel", "building_interior", null, Color.white, 0.5f, Color.white * 1.6f, true);
        mats[(int)M.Panel] = panel;
        mats[(int)M.Wood] = Mat("Room_Wood", "props_main", "props_main_nm", new Color(1f, 0.95f, 0.9f), 0.25f);
        mats[(int)M.FabricWhite] = Mat("Room_Fabric_White", null, null, new Color(0.88f, 0.87f, 0.84f), 0.05f);
        mats[(int)M.FabricBlue] = Mat("Room_Fabric_Blue", null, null, new Color(0.22f, 0.32f, 0.48f), 0.05f);
        mats[(int)M.Metal] = Mat("Room_Metal", null, null, new Color(0.18f, 0.18f, 0.19f), 0.65f);
        mats[(int)M.Metal].SetFloat("_Metallic", 0.8f);
    }

    static Material GroundMaterial(float size)
    {
        var m = Mat("Factory_Ground_Asphalt", "asphalt_square", "asphalt_square_nm", Color.white, 0.2f);
        float tiles = Mathf.Max(1f, size / 8f); // 8m 마다 반복
        m.SetTextureScale("_BaseMap", new Vector2(tiles, tiles));
        m.SetTextureScale("_BumpMap", new Vector2(tiles, tiles));
        m.mainTextureScale = new Vector2(tiles, tiles);
        EditorUtility.SetDirty(m);
        return m;
    }

    static Material Mat(string name, string tex, string nrm, Color color, float smooth, Color emission = default, bool emissionFromBase = false)
    {
        string path = $"{OutDir}/Materials/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
        else m.shader = shader;

        Texture2D baseTex = tex != null ? AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "Textures/" + tex + ".png") : null;
        Texture2D nrmTex = nrm != null ? AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "Textures/" + nrm + ".png") : null;
        if (nrmTex != null && AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(nrmTex)) is TextureImporter ti && ti.textureType != TextureImporterType.NormalMap)
            nrmTex = null;

        m.SetTexture("_BaseMap", baseTex);
        m.mainTexture = baseTex;
        m.SetColor("_BaseColor", color);
        m.SetFloat("_Smoothness", smooth);
        m.SetFloat("_Metallic", 0f);
        if (nrmTex != null) { m.SetTexture("_BumpMap", nrmTex); m.SetFloat("_BumpScale", 1f); m.EnableKeyword("_NORMALMAP"); }
        else { m.SetTexture("_BumpMap", null); m.DisableKeyword("_NORMALMAP"); }

        if (emission.maxColorComponent > 0f)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", emission);
            m.SetTexture("_EmissionMap", emissionFromBase ? baseTex : null);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        else
        {
            m.DisableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", Color.black);
        }
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
