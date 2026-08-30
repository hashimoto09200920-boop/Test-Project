using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

/// <summary>
/// Auroraを、複数パターン・ジッター機能を追加する前の単一画像状態に戻す一回限りの修正スクリプト。
/// </summary>
public static class AuroraRevertMigrator
{
    [MenuItem("Tools/Revert Aurora To Single Image")]
    public static void Run()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.name != "05_Game")
        {
            Debug.LogWarning("[AuroraRevertMigrator] 05_Gameシーンを開いた状態で実行してください。現在: " + scene.name);
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
            Debug.LogWarning("[AuroraRevertMigrator] Auroraが見つかりません。");
            return;
        }

        var sr = aurora.GetComponent<SpriteRenderer>();
        Sprite original = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Background/④ オーロラのみ（透過・独立レイヤー）.png");
        if (sr == null || original == null)
        {
            Debug.LogWarning($"[AuroraRevertMigrator] SpriteRenderer/元画像の取得に失敗 sr={sr} original={original}");
            return;
        }

        var srSO = new SerializedObject(sr);
        srSO.FindProperty("m_Sprite").objectReferenceValue = original;
        srSO.FindProperty("m_Enabled").boolValue = true;
        srSO.ApplyModifiedProperties();

        // AuroraWobbleは単一画像版に書き換え済みのため、auroraPatterns等の古いフィールドはもう存在しない。
        // 念のため子オブジェクト(AuroraLayerA/B、旧ジッター等で生成されたもの)が残っていれば削除する
        var toDestroy = new System.Collections.Generic.List<GameObject>();
        foreach (Transform child in aurora.transform)
        {
            if (child.name == "AuroraLayerA" || child.name == "AuroraLayerB")
                toDestroy.Add(child.gameObject);
        }
        foreach (var go in toDestroy) Object.DestroyImmediate(go);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[AuroraRevertMigrator] Auroraを単一画像状態に戻しました: " + original.name);
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
