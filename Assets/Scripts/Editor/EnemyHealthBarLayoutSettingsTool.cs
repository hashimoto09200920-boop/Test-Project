using UnityEditor;
using UnityEngine;

/// <summary>
/// 全エネミー共通のHP表示の配置設定アセット（Assets/Resources/GameData/EnemyHealthBarLayoutSettings.asset）を作成して選択する。
/// 既にあれば作り直さず選択するだけ（調整済みの値は消えない）。
/// </summary>
public static class EnemyHealthBarLayoutSettingsTool
{
    private const string AssetPath = "Assets/Resources/GameData/EnemyHealthBarLayoutSettings.asset";

    [MenuItem("Tools/エネミーHP表示/共通の配置設定アセットを作成")]
    private static void Run()
    {
        var asset = AssetDatabase.LoadAssetAtPath<EnemyHealthBarLayoutSettings>(AssetPath);
        if (asset == null)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources/GameData")) AssetDatabase.CreateFolder("Assets/Resources", "GameData");
            asset = ScriptableObject.CreateInstance<EnemyHealthBarLayoutSettings>();
            AssetDatabase.CreateAsset(asset, AssetPath);
            AssetDatabase.SaveAssets();
            Debug.Log("[EnemyHealthBarLayoutSettingsTool] 作成しました：" + AssetPath);
        }
        else Debug.Log("[EnemyHealthBarLayoutSettingsTool] 既にあります（作り直していません）：" + AssetPath);
        Selection.activeObject = asset;
        EditorGUIUtility.PingObject(asset);
    }
}
