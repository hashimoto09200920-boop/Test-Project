using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

/// <summary>
/// EarthLayerFitter.auroraSafetyMarginを安全な値(1.1)に強制的に書き戻す一回限りの修正スクリプト。
/// </summary>
public static class AuroraSafetyMarginFixMigrator
{
    [MenuItem("Tools/Fix Aurora Safety Margin To 1.1")]
    public static void Run()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.name != "05_Game")
        {
            Debug.LogWarning("[AuroraSafetyMarginFixMigrator] 05_Gameシーンを開いた状態で実行してください。現在: " + scene.name);
            return;
        }

        EarthLayerFitter fitter = Object.FindFirstObjectByType<EarthLayerFitter>(FindObjectsInactive.Include);
        if (fitter == null)
        {
            Debug.LogWarning("[AuroraSafetyMarginFixMigrator] EarthLayerFitterが見つかりません。");
            return;
        }

        var so = new SerializedObject(fitter);
        var prop = so.FindProperty("auroraSafetyMargin");
        float before = prop.floatValue;
        prop.floatValue = 1.1f;
        so.ApplyModifiedProperties();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[AuroraSafetyMarginFixMigrator] auroraSafetyMarginを{before}から1.1に修正し保存しました。");
    }
}
