using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// 메뉴
//  Tools > City > Find Unused Assets         : 쓰이지 않는 에셋을 찾아 프로젝트 폴더에 보고서(UnusedAssets_report.txt)와 목록(UnusedAssets_list.txt)을 만듭니다. 아무것도 지우지 않습니다.
//  Tools > City > Move Unused Assets To Trash : UnusedAssets_list.txt 에 있는 에셋을 Windows 휴지통으로 옮깁니다 (복구 가능).
//
// "쓰임" 판단 기준 (여기서부터 참조를 따라가 닿는 파일은 모두 남김)
//  - 지금 열려 있는 씬 (작업 씬)
//  - 렌더 파이프라인/품질 설정, Preloaded Assets
//  - 설정/스크립트 폴더(Settings, XR, XRI, Scripts, Editor), 공장/공장 내부, Resources, TextMesh Pro 기본 리소스
//  - 공장/시설물 생성 메뉴가 경로로 불러오는 데모 시티 텍스처/프리팹
// "묶음" (예제/샘플 폴더) 은 안의 파일이 하나라도 쓰이면 통째로 남기고, 하나도 안 쓰이면 통째로 지울 후보로 올립니다.
// (샘플 스크립트끼리 코드로 참조하기 때문에 일부만 지우면 컴파일 오류가 날 수 있어서)
public static class UnusedAssetFinder
{
    static readonly string[] ProtectedPrefixes =
    {
        "Assets/Settings/", "Assets/XR/", "Assets/XRI/", "Assets/TextMesh Pro/",
        "Assets/Scripts/", "Assets/Editor/", "Assets/메쉬/공장/", "Assets/메쉬/공장 내부/",
        "Assets/InputSystem_Actions.inputactions",
    };
    static readonly string[] ProtectedExt = { ".cs", ".asmdef", ".asmref", ".dll", ".shader", ".shadergraph", ".shadersubgraph", ".hlsl", ".cginc", ".compute", ".inputactions" };

    const string DC = "Assets/메쉬/도시/Demo City By Versatile Studio/";
    static readonly string[] KeepByPath =
    {
        DC + "Textures/highway_wall.png", DC + "Textures/highway_wall_nm.png",
        DC + "Textures/building_bases.png", DC + "Textures/building_bases_nm.png",
        DC + "Textures/building_interior.png", DC + "Textures/building_interior_nm.png",
        DC + "Textures/asphalt_square.png", DC + "Textures/asphalt_square_nm.png",
        DC + "Textures/props_main.png", DC + "Textures/props_main_nm.png",
        DC + "Prefabs/factory_chimney-stalk.prefab", DC + "Prefabs/building_lamp_orange.prefab",
        DC + "Prefabs/concrete_block.prefab", DC + "Prefabs/lamp_pole_single_orange.prefab",
    };

    static string ProjectRoot => Path.GetDirectoryName(Application.dataPath);
    static string ReportPath => Path.Combine(ProjectRoot, "UnusedAssets_report.txt");
    static string ListPath => Path.Combine(ProjectRoot, "UnusedAssets_list.txt");

    // 통째로 판단할 묶음 폴더
    static List<string> Units()
    {
        var u = new List<string>();
        if (AssetDatabase.IsValidFolder("Assets/TextMesh Pro/Examples & Extras")) u.Add("Assets/TextMesh Pro/Examples & Extras");
        if (AssetDatabase.IsValidFolder("Assets/Scenes")) u.Add("Assets/Scenes");
        if (AssetDatabase.IsValidFolder("Assets/Samples"))
            foreach (var pkg in AssetDatabase.GetSubFolders("Assets/Samples"))
                foreach (var ver in AssetDatabase.GetSubFolders(pkg))
                    foreach (var sample in AssetDatabase.GetSubFolders(ver))
                        u.Add(sample);
        return u;
    }

    [MenuItem("Tools/City/Find Unused Assets")]
    public static void Find()
    {
        var all = AssetDatabase.GetAllAssetPaths()
            .Where(p => p.StartsWith("Assets/") && !AssetDatabase.IsValidFolder(p))
            .ToList();
        var units = Units();
        string UnitOf(string p) => units.FirstOrDefault(u => p.StartsWith(u + "/"));

        // ---- 루트 ----
        var roots = new HashSet<string>();
        var openScenes = new List<string>();
        for (int i = 0; i < EditorSceneManager.sceneCount; i++)
        {
            var s = EditorSceneManager.GetSceneAt(i);
            if (!string.IsNullOrEmpty(s.path)) { roots.Add(s.path); openScenes.Add(s.path); }
        }
        AddObj(roots, GraphicsSettings.defaultRenderPipeline);
        for (int i = 0; i < QualitySettings.names.Length; i++) AddObj(roots, QualitySettings.GetRenderPipelineAssetAt(i));
        foreach (var o in PlayerSettings.GetPreloadedAssets()) AddObj(roots, o);
        foreach (var p in all)
        {
            if (UnitOf(p) != null) continue;   // 묶음 안의 파일은 루트로 보지 않음
            if (IsProtected(p) || p.Contains("/Resources/") || p.Contains("/StreamingAssets/") || p.Contains("/Plugins/"))
                roots.Add(p);
        }
        foreach (var p in KeepByPath) if (File.Exists(Path.Combine(ProjectRoot, p))) roots.Add(p);

        var used = new HashSet<string>(AssetDatabase.GetDependencies(roots.ToArray(), true));
        foreach (var r in roots) used.Add(r);

        // ---- 묶음 판단 ----
        var unitReport = new List<string>();
        var candidates = new List<string>();
        foreach (var u in units)
        {
            var files = all.Where(p => p.StartsWith(u + "/")).ToList();
            var usedFiles = files.Where(used.Contains).ToList();
            long bytes = files.Sum(Size);
            if (usedFiles.Count == 0)
            {
                candidates.AddRange(files);
                unitReport.Add($"  [지울 후보] {MB(bytes),9}  {files.Count,4}개  {u}");
            }
            else
            {
                unitReport.Add($"  [유지]      {MB(bytes),9}  {files.Count,4}개  {u}   (작업 씬에서 {usedFiles.Count}개 사용, 예: {usedFiles[0]})");
            }
        }

        // ---- 묶음 밖 개별 파일 ----
        var loose = all.Where(p => UnitOf(p) == null && !used.Contains(p)).ToList();
        candidates.AddRange(loose);
        candidates = candidates.Distinct().OrderBy(p => p).ToList();

        long total = candidates.Sum(Size);
        var byFolder = new Dictionary<string, (int n, long bytes)>();
        foreach (var p in loose)
        {
            string key = FolderKey(p);
            byFolder.TryGetValue(key, out var v);
            byFolder[key] = (v.n + 1, v.bytes + Size(p));
        }

        // Build Settings 에 들어 있는데 후보인 씬
        var buildScenes = EditorBuildSettings.scenes.Select(s => s.path).Where(candidates.Contains).ToList();

        var sb = new StringBuilder();
        sb.AppendLine("[기준으로 삼은 작업 씬]");
        foreach (var s in openScenes) sb.AppendLine("  " + s);
        sb.AppendLine();
        sb.AppendLine($"전체 에셋 {all.Count}개 중 지울 후보 {candidates.Count}개, 합계 {MB(total)}");
        sb.AppendLine();
        sb.AppendLine("[예제/샘플 묶음]");
        foreach (var l in unitReport) sb.AppendLine(l);
        sb.AppendLine();
        sb.AppendLine("[묶음 밖에서 안 쓰는 파일 - 폴더별]");
        foreach (var kv in byFolder.OrderByDescending(k => k.Value.bytes))
            sb.AppendLine($"  {MB(kv.Value.bytes),9}  {kv.Value.n,4}개  {kv.Key}");
        sb.AppendLine();
        sb.AppendLine("[Build Settings 에 등록된 후보 씬]");
        foreach (var s in buildScenes) sb.AppendLine("  " + s);
        sb.AppendLine();
        sb.AppendLine("[묶음 밖에서 안 쓰는 파일 - 전체]");
        foreach (var p in loose) sb.AppendLine($"  {MB(Size(p)),9}  {p}");

        File.WriteAllText(ReportPath, sb.ToString(), Encoding.UTF8);
        File.WriteAllLines(ListPath, candidates, Encoding.UTF8);
        Debug.Log($"지울 후보 {candidates.Count}개 ({MB(total)}). 아무것도 지우지 않았습니다. 보고서: {ReportPath}");
    }

    [MenuItem("Tools/City/Move Unused Assets To Trash")]
    public static void Trash()
    {
        if (!File.Exists(ListPath)) { Debug.LogError("먼저 Find Unused Assets 를 실행하세요."); return; }
        var paths = File.ReadAllLines(ListPath, Encoding.UTF8).Where(l => !string.IsNullOrWhiteSpace(l)).Select(l => l.Trim())
            .Where(p => File.Exists(Path.Combine(ProjectRoot, p))).ToArray();

        // Build Settings 에서도 빼기
        var set = new HashSet<string>(paths);
        var kept = EditorBuildSettings.scenes.Where(s => !set.Contains(s.path)).ToArray();
        if (kept.Length != EditorBuildSettings.scenes.Length) EditorBuildSettings.scenes = kept;

        var failed = new List<string>();
        AssetDatabase.MoveAssetsToTrash(paths, failed);
        int removedFolders = RemoveEmptyFolders("Assets");
        AssetDatabase.Refresh();
        Debug.Log($"{paths.Length - failed.Count}개를 휴지통으로 옮겼습니다 (실패 {failed.Count}개, 빈 폴더 {removedFolders}개 정리). Windows 휴지통에서 복구할 수 있습니다.");
        if (failed.Count > 0) Debug.LogWarning("옮기지 못한 파일:\n" + string.Join("\n", failed));
    }

    static int RemoveEmptyFolders(string folder)
    {
        int count = 0;
        foreach (var sub in AssetDatabase.GetSubFolders(folder)) count += RemoveEmptyFolders(sub);
        if (folder != "Assets")
        {
            string full = Path.Combine(ProjectRoot, folder);
            bool empty = Directory.Exists(full) && !Directory.EnumerateFileSystemEntries(full).Any(e => !e.EndsWith(".meta"));
            if (empty && AssetDatabase.MoveAssetToTrash(folder)) count++;
        }
        return count;
    }

    static bool IsProtected(string p)
    {
        if (ProtectedPrefixes.Any(x => p.StartsWith(x))) return true;
        string ext = Path.GetExtension(p).ToLowerInvariant();
        return ProtectedExt.Contains(ext);
    }

    static void AddObj(HashSet<string> set, Object o)
    {
        if (o == null) return;
        string p = AssetDatabase.GetAssetPath(o);
        if (!string.IsNullOrEmpty(p)) set.Add(p);
    }

    static long Size(string p)
    {
        try { return new FileInfo(Path.Combine(ProjectRoot, p)).Length; } catch { return 0; }
    }

    static string FolderKey(string p)
    {
        var parts = p.Split('/');
        int depth = p.StartsWith("Assets/메쉬/") ? 4 : 3;
        return string.Join("/", parts.Take(Mathf.Min(depth, parts.Length - 1)));
    }

    static string MB(long b) { return $"{b / 1024f / 1024f:F1} MB"; }
}
