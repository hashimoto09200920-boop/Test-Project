using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

/// <summary>
/// シーンに保存済みのAuroraWobble.positionJitterX/Y/scaleJitterRangeを安全な既定値に書き戻す
/// 一回限りの移行スクリプト（C#側のデフォルト値変更はシーンに保存済みの値には反映されないため）。
/// </summary>
public static class AuroraJitterResetMigrator
{
    [MenuItem("Tools/Reset Aurora Jitter To Safe Defaults")]
    public static void Run()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.name != "05_Game")
        {
            Debug.LogWarning("[AuroraJitterResetMigrator] 05_Gameシーンを開いた状態で実行してください。現在: " + scene.name);
            return;
        }

        GameObject aurora = null;
        foreach (var root in scene.GetRootGameObjects())
        {
            var t = FindDeep(root.transform, "Aurora");
            if (t != null) { aurora = t.gameObject; break; }
        }
        if (aurora == null)
        {
            Debug.LogWarning("[AuroraJitterResetMigrator] Auroraが見つかりません。");
            return;
        }

        var wobble = aurora.GetComponent<AuroraWobble>();
        if (wobble == null)
        {
            Debug.LogWarning("[AuroraJitterResetMigrator] AuroraWobbleが見つかりません。");
            return;
        }

        var so = new SerializedObject(wobble);
        so.FindProperty("positionJitterX").floatValue = 0.3f;
        so.FindProperty("positionJitterY").floatValue = 0.06f;
        so.FindProperty("scaleJitterRange").floatValue = 0.05f;
        so.ApplyModifiedProperties();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[AuroraJitterResetMigrator] ジッター値を安全な既定値(0.3 / 0.06 / 0.05)に書き戻しました。");
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
