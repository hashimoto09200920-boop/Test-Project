using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

/// <summary>
/// AuroraのAuroraWobble.auroraPatternsに④(既存)/④2(左右反転)を設定する一回限りの移行スクリプト。
/// </summary>
public static class AuroraPatternsWireMigrator
{
    [MenuItem("Tools/Wire Aurora Patterns")]
    public static void Run()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.name != "05_Game")
        {
            Debug.LogWarning("[AuroraPatternsWireMigrator] 05_Gameシーンを開いた状態で実行してください。現在: " + scene.name);
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
            Debug.LogWarning("[AuroraPatternsWireMigrator] Auroraが見つかりません。");
            return;
        }

        var wobble = aurora.GetComponent<AuroraWobble>();
        if (wobble == null)
        {
            Debug.LogWarning("[AuroraPatternsWireMigrator] AuroraにAuroraWobbleがありません。");
            return;
        }

        Sprite original = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Background/④ オーロラのみ（透過・独立レイヤー）.png");
        Sprite flipped = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Background/④ オーロラのみ（透過・独立レイヤー）2.png");
        if (original == null || flipped == null)
        {
            Debug.LogWarning($"[AuroraPatternsWireMigrator] スプライト読み込み失敗 original={original} flipped={flipped}");
            return;
        }

        var so = new SerializedObject(wobble);
        var prop = so.FindProperty("auroraPatterns");
        prop.arraySize = 2;
        prop.GetArrayElementAtIndex(0).objectReferenceValue = original;
        prop.GetArrayElementAtIndex(1).objectReferenceValue = flipped;
        so.ApplyModifiedProperties();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[AuroraPatternsWireMigrator] Auroraのpatternsを設定しました: " + original.name + ", " + flipped.name);
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
