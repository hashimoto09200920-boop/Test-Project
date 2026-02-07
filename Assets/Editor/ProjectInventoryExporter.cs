using System; // StringComparer 用
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class ProjectInventoryExporter
{
    private const string ExportDir = "Assets/ProjectDump";

    // ====== エントリ1：プロジェクト全体の代表的アセットを列挙 ======
    [MenuItem("Tools/Export/Export Project Inventory")]
    public static void ExportProjectInventory()
    {
        EnsureDir();

        var path = Path.Combine(ExportDir, "Project_Inventory.txt");
        var sb = new StringBuilder();

        sb.AppendLine("# Project Inventory");
        sb.AppendLine($"# Date : {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();

        // 代表的なカテゴリを出力
        DumpAssetsOfType("Scenes", new[] { ".unity" }, sb);
        DumpAssetsOfType("Prefabs", new[] { ".prefab" }, sb);
        DumpAssetsOfType("Scripts", new[] { ".cs" }, sb);
        DumpAssetsOfType("ScriptableObjects", null, sb, FilterScriptableObjects);
        DumpAssetsOfType("Materials", new[] { ".mat" }, sb);
        DumpAssetsOfType("Resources (All)", null, sb, InResourcesFolder);

        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        AssetDatabase.Refresh();
        EditorUtility.RevealInFinder(path);
        Debug.Log($"[ProjectInventoryExporter] Exported: {path}");
    }

    // ====== エントリ2：Build Profiles（EditorBuildSettings）のシーン一覧 ======
    [MenuItem("Tools/Export/Export Build Scene List")]
    public static void ExportBuildScenes()
    {
        EnsureDir();

        var path = Path.Combine(ExportDir, "Build_Scenes.txt");
        var sb = new StringBuilder();

        sb.AppendLine("# Build Profiles Scene List");
        sb.AppendLine($"# Date : {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();

        var scenes = EditorBuildSettings.scenes;
        foreach (var s in scenes)
        {
            sb.AppendLine($"{(s.enabled ? "[ON ]" : "[OFF]")} {s.path}");
        }

        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        AssetDatabase.Refresh();
        EditorUtility.RevealInFinder(path);
        Debug.Log($"[ProjectInventoryExporter] Exported: {path}");
    }

    // ====== 共通：カテゴリ毎にアセット列挙 ======
    private static void DumpAssetsOfType(
        string title,
        string[] exts,
        StringBuilder sb,
        Func<string, bool> extraFilter = null)
    {
        sb.AppendLine($"## {title}");

        var guids = AssetDatabase.FindAssets(""); // Assets 全探索
        var paths = new List<string>(guids.Length);

        foreach (var g in guids)
        {
            var p = AssetDatabase.GUIDToAssetPath(g);
            if (string.IsNullOrEmpty(p)) continue;
            if (!p.StartsWith("Assets/")) continue;

            if (exts != null && exts.Length > 0)
            {
                var ext = Path.GetExtension(p).ToLowerInvariant();
                if (!exts.Contains(ext)) continue;
            }

            if (extraFilter != null && !extraFilter(p)) continue;

            paths.Add(p);
        }

        paths.Sort(StringComparer.OrdinalIgnoreCase);
        foreach (var p in paths) sb.AppendLine(p);
        sb.AppendLine();
    }

    // ====== ヘルパ ======
    private static void EnsureDir()
    {
        if (!Directory.Exists(ExportDir)) Directory.CreateDirectory(ExportDir);
    }

    // Resources/ 配下のみ拾うフィルタ
    private static bool InResourcesFolder(string path)
    {
        return path.IndexOf("/Resources/", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    // ScriptableObject を判定（拡張子だけでなく型でもチェック）
    private static bool FilterScriptableObjects(string path)
    {
        // SO は一般に .asset。Prefab を除外したいが、プロジェクト事情で Prefab に SO が混ざる可能性もあるため
        // いったん Load して UnityEngine.Object として判定する。
        if (!path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase)) return false;

        // ※ ここは System.Object ではなく UnityEngine.Object を明示
        UnityEngine.Object obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
        if (obj == null) return false;

        // ScriptableObject かどうか
        return obj is ScriptableObject;
    }
}
