using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

/// <summary>
/// Background_Farの子として過去のテストで大量に生成され、アクティブなまま
/// シーンに焼き込まれてしまった Background_Far_TimeOfDayLayerA/B を全て削除する。
/// これが全Areaで①(宇宙背景)らしき矩形パネルが表示されてしまう不具合の直接の原因だった。
/// </summary>
public static class TimeOfDayLayerCleanupMigrator
{
    [MenuItem("Tools/Fix All Areas Background Bug (Remove Leftover TimeOfDay Layers)")]
    public static void Run()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.name != "05_Game")
        {
            Debug.LogWarning("[TimeOfDayLayerCleanupMigrator] 05_Gameシーンを開いた状態で実行してください。現在: " + scene.name);
            return;
        }

        GameObject bgFar = GameObject.Find("Background_Far");
        if (bgFar == null)
        {
            Debug.LogWarning("[TimeOfDayLayerCleanupMigrator] Background_Farが見つかりません。");
            return;
        }

        var toDestroy = new List<GameObject>();
        foreach (Transform child in bgFar.transform)
        {
            if (child.name == "Background_Far_TimeOfDayLayerA" || child.name == "Background_Far_TimeOfDayLayerB")
                toDestroy.Add(child.gameObject);
        }

        // 念のためシーン全体からも同名オブジェクトを探して削除する（親がずれているケースに備えて）
        foreach (var root in scene.GetRootGameObjects())
        {
            CollectByName(root.transform, "Background_Far_TimeOfDayLayerA", toDestroy);
            CollectByName(root.transform, "Background_Far_TimeOfDayLayerB", toDestroy);
        }

        int count = toDestroy.Count;
        foreach (var go in toDestroy)
            Object.DestroyImmediate(go);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[TimeOfDayLayerCleanupMigrator] {count}個のleftover TimeOfDayLayerを削除し保存しました。");
    }

    private static void CollectByName(Transform t, string name, List<GameObject> list)
    {
        if (t.name == name && !list.Contains(t.gameObject)) list.Add(t.gameObject);
        for (int i = 0; i < t.childCount; i++)
            CollectByName(t.GetChild(i), name, list);
    }
}
