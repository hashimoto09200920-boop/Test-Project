using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

/// <summary>
/// Earth_SurfaceのEarthRotationScroll.verticalScrollSpeedを0に戻す一回限りの修正スクリプト。
/// 0以外の値だと縦方向のタイルループが有効になり、開始位置ランダム化と組み合わさって
/// 地表の四角い境界がマスク内に見える不具合が起きていたため、問題が起きていなかった0に戻す。
/// </summary>
public static class EarthSurfaceVerticalScrollResetMigrator
{
    [MenuItem("Tools/Reset Earth Surface Vertical Scroll To Zero")]
    public static void Run()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.name != "05_Game")
        {
            Debug.LogWarning("[EarthSurfaceVerticalScrollResetMigrator] 05_Gameシーンを開いた状態で実行してください。現在: " + scene.name);
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
            Debug.LogWarning("[EarthSurfaceVerticalScrollResetMigrator] Earth_Surfaceが見つかりません。");
            return;
        }

        var scroll = earthSurface.GetComponent<EarthRotationScroll>();
        if (scroll == null)
        {
            Debug.LogWarning("[EarthSurfaceVerticalScrollResetMigrator] EarthRotationScrollが見つかりません。");
            return;
        }

        var so = new SerializedObject(scroll);
        var prop = so.FindProperty("verticalScrollSpeed");
        float before = prop.floatValue;
        prop.floatValue = 0f;
        so.ApplyModifiedProperties();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[EarthSurfaceVerticalScrollResetMigrator] verticalScrollSpeedを{before}から0に戻し保存しました。");
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
