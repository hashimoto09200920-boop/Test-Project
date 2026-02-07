using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class HierarchyDumper
{
    private const string MenuPath = "Tools/Export/Export Active Scene Hierarchy";

    [MenuItem(MenuPath)]
    private static void ExportActiveSceneHierarchy()
    {
        var scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            EditorUtility.DisplayDialog("Hierarchy Dumper", "アクティブシーンが無効か未ロードです。", "OK");
            return;
        }

        var roots = scene.GetRootGameObjects();
        var sb = new StringBuilder();
        sb.AppendLine($"# Scene: {scene.name}");
        sb.AppendLine($"# Date : {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();

        foreach (var go in roots.OrderBy(g => g.name))
        {
            DumpGameObject(go, 0, sb);
        }

        var dir = "Assets/HierarchyDump";
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, $"{scene.name}_Hierarchy.txt");
        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        AssetDatabase.Refresh();
        EditorUtility.RevealInFinder(path);
        Debug.Log($"[HierarchyDumper] Exported: {path}");
    }

    private static void DumpGameObject(GameObject go, int indent, StringBuilder sb)
    {
        var ind = new string(' ', indent * 2);
        sb.AppendLine($"{ind}- {go.name} {ActiveFlags(go)}");

        // コンポーネント一覧
        var comps = go.GetComponents<Component>();
        foreach (var c in comps)
        {
            if (c == null)
            {
                sb.AppendLine($"{ind}  [Missing Component]");
                continue;
            }
            var type = c.GetType().Name;
            if (c is RectTransform rt)
            {
                var size = GetRectInfo(rt);
                sb.AppendLine($"{ind}  [RectTransform] {size}");
            }
            else
            {
                sb.AppendLine($"{ind}  [{type}]");
            }
        }

        // 子
        for (int i = 0; i < go.transform.childCount; i++)
        {
            var child = go.transform.GetChild(i).gameObject;
            DumpGameObject(child, indent + 1, sb);
        }
    }

    private static string ActiveFlags(GameObject go)
    {
        var a = go.activeInHierarchy ? "Active" : "Inactive";
        var s = go.activeSelf ? "SelfOn" : "SelfOff";
        return $"({a}/{s})";
    }

    private static string GetRectInfo(RectTransform rt)
    {
        var size = rt.rect.size;
        var anchorMin = rt.anchorMin;
        var anchorMax = rt.anchorMax;
        var pivot = rt.pivot;
        return $"Size=({size.x:0.#},{size.y:0.#}) Anchors=({anchorMin.x:0.##},{anchorMin.y:0.##})-({anchorMax.x:0.##},{anchorMax.y:0.##}) Pivot=({pivot.x:0.##},{pivot.y:0.##})";
    }
}
