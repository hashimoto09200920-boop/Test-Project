using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

/// <summary>
/// Earth_SurfaceのEarthRotationScroll.seamOverlapを設定する修正スクリプト。
/// </summary>
public static class EarthSurfaceSeamOverlapFixMigrator
{
    [MenuItem("Tools/Set Earth Surface Seam Overlap To 0.3")]
    public static void Run()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.name != "05_Game")
        {
            Debug.LogWarning("[EarthSurfaceSeamOverlapFixMigrator] 05_Gameシーンを開いた状態で実行してください。現在: " + scene.name);
            return;
        }

        GameObject earthSurface = null;
        foreach (var root in scene.GetRootGameObjects())
        {
            var t = FindDeep(root.transform, "Earth_Surface");
            if (t != null) { earthSurface = t.gameObject; break; }
        }
        if (earthSurface == null)
        {
            Debug.LogWarning("[EarthSurfaceSeamOverlapFixMigrator] Earth_Surfaceが見つかりません。");
            return;
        }

        var scroll = earthSurface.GetComponent<EarthRotationScroll>();
        if (scroll == null)
        {
            Debug.LogWarning("[EarthSurfaceSeamOverlapFixMigrator] EarthRotationScrollが見つかりません。");
            return;
        }

        var so = new SerializedObject(scroll);
        var prop = so.FindProperty("seamOverlap");
        float before = prop.floatValue;
        prop.floatValue = 0.3f;
        so.ApplyModifiedProperties();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[EarthSurfaceSeamOverlapFixMigrator] seamOverlapを{before}から0.3に変更し保存しました。");
    }

    private static Transform FindDeep(Transform t, string name)
    {
        if (t.name == name) return t;
        for (int i = 0; i < t.childCount; i++)
        {
            var r = FindDeep(t.GetChild(i), name);
            if (r != null) return r;
        }
        return null;
    }
}
