using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

/// <summary>
/// Auroraのスプライトを新しい「④オーロラ修正版.png」に差し替え、
/// EarthLayerFitterの根元位置・スケール係数を新画像の実測値に合わせて更新する。
/// </summary>
public static class AuroraNewImageWireMigrator
{
    [MenuItem("Tools/Area09/Wire New Aurora Image")]
    public static void Run()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.name != "05_Game")
        {
            Debug.LogWarning("[AuroraNewImageWireMigrator] 05_Gameシーンを開いた状態で実行してください。現在: " + scene.name);
            return;
        }

        GameObject aurora = GameObject.Find("Aurora");
        GameObject earthMask = GameObject.Find("Earth_Mask");
        if (aurora == null || earthMask == null)
        {
            Debug.LogWarning("[AuroraNewImageWireMigrator] Aurora/Earth_Maskが見つかりません。");
            return;
        }

        Sprite newAurora = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Background/④オーロラ修正版.png");
        if (newAurora == null)
        {
            Debug.LogError("[AuroraNewImageWireMigrator] ④オーロラ修正版.pngが読み込めません。");
            return;
        }

        var sr = aurora.GetComponent<SpriteRenderer>();
        var srSO = new SerializedObject(sr);
        srSO.FindProperty("m_Sprite").objectReferenceValue = newAurora;
        srSO.ApplyModifiedProperties();

        var fitter = earthMask.GetComponent<EarthLayerFitter>();
        var fitterSO = new SerializedObject(fitter);
        fitterSO.FindProperty("auroraScaleFactor").floatValue = 0.809f; // 1672/2066: 輪郭グローと実寸幅を合わせる
        fitterSO.FindProperty("auroraRootFraction").floatValue = 0.620f; // 中央列の根元位置（新画像で実測）
        fitterSO.FindProperty("auroraRootYOffset").floatValue = 0f;
        fitterSO.ApplyModifiedProperties();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[AuroraNewImageWireMigrator] Auroraを新画像に差し替え、auroraScaleFactor=1.0, auroraRootFraction=0.921に更新しました。");
    }
}
