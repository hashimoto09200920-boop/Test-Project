using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

/// <summary>
/// Earth_Mask/Earth_Surface/Earth_RimGlow/Auroraがシーンにアクティブ状態のまま
/// 保存されてしまっていた不具合（全Areaで表示されてしまう）を修正する。
/// これらはBackgroundManager.Start()がArea09選択時のみアクティブ化する前提のため、
/// シーン保存時のデフォルトは非アクティブでなければならない。
/// </summary>
public static class EarthLayersDeactivateFixMigrator
{
    [MenuItem("Tools/Area09/Fix Earth Layers Default Inactive")]
    public static void Run()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.name != "05_Game")
        {
            Debug.LogWarning("[EarthLayersDeactivateFixMigrator] 05_Gameシーンを開いた状態で実行してください。現在: " + scene.name);
            return;
        }

        string[] names = { "Earth_Mask", "Earth_Surface", "Earth_RimGlow", "Aurora" };
        int fixedCount = 0;
        foreach (var n in names)
        {
            GameObject go = GameObject.Find(n);
            if (go == null)
            {
                Debug.LogWarning($"[EarthLayersDeactivateFixMigrator] {n}が見つかりません。");
                continue;
            }
            if (go.activeSelf)
            {
                go.SetActive(false);
                fixedCount++;
            }
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[EarthLayersDeactivateFixMigrator] {fixedCount}件を非アクティブに修正し保存しました。");
    }
}
