using System.IO;
using Game.Testing;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 撮影モードの設定アセット（Assets/Resources/RecordingModeSettings.asset）を作成・選択する（2026/10/18）。
/// 既にあれば選択するだけ（値は変えない）。ON/OFF・カテゴリーごとの合計・入手元の配分はInspectorで変える。
/// </summary>
public static class RecordingModeSettingsMenu
{
    private const string AssetPath = "Assets/Resources/RecordingModeSettings.asset";

    [MenuItem("Tools/撮影モード/撮影モードの設定を開く（無ければ作成）")]
    private static void Open()
    {
        var s = AssetDatabase.LoadAssetAtPath<RecordingModeSettings>(AssetPath);
        if (s == null)
        {
            if (!Directory.Exists("Assets/Resources")) Directory.CreateDirectory("Assets/Resources");
            s = ScriptableObject.CreateInstance<RecordingModeSettings>();
            AssetDatabase.CreateAsset(s, AssetPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[RecordingModeSettingsMenu] 撮影モードの設定を作成しました：{AssetPath}（最初はOFF）");
        }
        Selection.activeObject = s;
        EditorGUIUtility.PingObject(s);
    }
}
