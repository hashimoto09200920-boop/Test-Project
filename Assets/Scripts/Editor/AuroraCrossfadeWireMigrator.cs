using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

/// <summary>
/// AuroraのAuroraWobble.auroraPatternsに④修正版/反転版を設定し、
/// patternOffsetsを2要素(0,0)で初期化する（反転版側を後で個別調整できるように）。
/// </summary>
public static class AuroraCrossfadeWireMigrator
{
    [MenuItem("Tools/Area09/Wire Aurora Crossfade (Fixed + Flipped)")]
    public static void Run()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.name != "05_Game")
        {
            Debug.LogWarning("[AuroraCrossfadeWireMigrator] 05_Gameシーンを開いた状態で実行してください。現在: " + scene.name);
            return;
        }

        GameObject aurora = GameObject.Find("Aurora");
        if (aurora == null)
        {
            Debug.LogWarning("[AuroraCrossfadeWireMigrator] Auroraが見つかりません。");
            return;
        }

        var wobble = aurora.GetComponent<AuroraWobble>();
        if (wobble == null)
        {
            Debug.LogWarning("[AuroraCrossfadeWireMigrator] AuroraWobbleが見つかりません。");
            return;
        }

        Sprite fixedSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Background/④オーロラ修正版.png");
        Sprite flippedSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Background/④オーロラ修正版_反転.png");
        if (fixedSprite == null || flippedSprite == null)
        {
            Debug.LogError($"[AuroraCrossfadeWireMigrator] スプライト読み込み失敗 fixed={fixedSprite} flipped={flippedSprite}");
            return;
        }

        var so = new SerializedObject(wobble);
        var patternsProp = so.FindProperty("auroraPatterns");
        patternsProp.arraySize = 2;
        patternsProp.GetArrayElementAtIndex(0).objectReferenceValue = fixedSprite;
        patternsProp.GetArrayElementAtIndex(1).objectReferenceValue = flippedSprite;
        so.ApplyModifiedProperties();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[AuroraCrossfadeWireMigrator] Auroraのpatternsを設定しました: " + fixedSprite.name + ", " + flippedSprite.name);
    }
}
