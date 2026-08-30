using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

/// <summary>
/// Background_FarのSpriteRendererがm_Enabled=0のまま保存されてしまっていた不具合を修正する。
/// TimeOfDayFadeのStartCycle()がbaseRenderer.enabled=falseにした状態のまま
/// leftoverオブジェクトと一緒に焼き込まれてしまっていた。
/// </summary>
public static class BackgroundFarEnableFixMigrator
{
    [MenuItem("Tools/Fix Background Far Enabled")]
    public static void Run()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.name != "05_Game")
        {
            Debug.LogWarning("[BackgroundFarEnableFixMigrator] 05_Gameシーンを開いた状態で実行してください。現在: " + scene.name);
            return;
        }

        GameObject bgFar = GameObject.Find("Background_Far");
        if (bgFar == null)
        {
            Debug.LogWarning("[BackgroundFarEnableFixMigrator] Background_Farが見つかりません。");
            return;
        }

        var sr = bgFar.GetComponent<SpriteRenderer>();
        if (sr == null)
        {
            Debug.LogWarning("[BackgroundFarEnableFixMigrator] SpriteRendererが見つかりません。");
            return;
        }

        bool before = sr.enabled;
        sr.enabled = true;

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[BackgroundFarEnableFixMigrator] Background_Far SpriteRenderer.enabledを{before}からtrueに修正し保存しました。");
    }
}
