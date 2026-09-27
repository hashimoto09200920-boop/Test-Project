using UnityEditor;
using UnityEngine;
using System.IO;

/// <summary>
/// ★一時的なセットアップツール（実行後は削除してよい）。全エネミー共通のHPゴーストバー設定
/// アセット（EnemyHealthGhostBarSettings.asset）をResourcesフォルダに新規作成する。
/// 既に存在する場合は何もしない（誤って上書きしない）。
/// </summary>
public static class CreateEnemyHealthGhostBarSettingsTool
{
    private const string AssetPath = "Assets/Resources/GameData/EnemyHealthGhostBarSettings.asset";

    [MenuItem("Tools/★全エネミー共通のHPゴーストバー設定アセットを作成")]
    public static void CreateSettingsAsset()
    {
        var existing = AssetDatabase.LoadAssetAtPath<EnemyHealthGhostBarSettings>(AssetPath);
        if (existing != null)
        {
            Debug.LogWarning($"[CreateEnemyHealthGhostBarSettingsTool] 既に{AssetPath}が存在します。中断します。Inspectorから直接調整してください。");
            Selection.activeObject = existing;
            EditorGUIUtility.PingObject(existing);
            return;
        }

        string dir = Path.GetDirectoryName(AssetPath);
        if (!AssetDatabase.IsValidFolder(dir))
        {
            Debug.LogError($"[CreateEnemyHealthGhostBarSettingsTool] フォルダが見つかりません: {dir}");
            return;
        }

        var settings = ScriptableObject.CreateInstance<EnemyHealthGhostBarSettings>();
        settings.ghostColor = new Color(1f, 0.25f, 0.1f, 0.9f);
        settings.catchUpSpeedPerSecond = 0.6f;

        AssetDatabase.CreateAsset(settings, AssetPath);
        AssetDatabase.SaveAssets();

        Selection.activeObject = settings;
        EditorGUIUtility.PingObject(settings);

        Debug.Log($"[CreateEnemyHealthGhostBarSettingsTool] {AssetPath} を作成しました。以後はこのアセットのInspectorだけで全エネミーのゴーストバー色・速度を一括調整できます。");
    }
}
