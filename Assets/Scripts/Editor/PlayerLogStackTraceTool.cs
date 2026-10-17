using UnityEditor;
using UnityEngine;

/// <summary>
/// 実機ビルドで、通常ログ（Debug.Log）と警告（Debug.LogWarning）の出力ごとにスタックトレース（呼び出し元の一覧）を
/// 収集しないようにする。ログ1回ごとのスタックトレース収集はスマホでは重く、処理落ちの原因になる。
/// エラー・例外・Assertは原因調査に必要なため、従来どおり（ScriptOnly）残す。
/// ゲームの挙動は変わらない。Editorのコンソール／Editor.logの表示も変わらない（Player Settingsは実機ビルド用）。
/// 設定先：Project Settings > Player > Other Settings > Stack Trace（後から直接変更してもよい）
/// </summary>
public static class PlayerLogStackTraceTool
{
    [MenuItem("Tools/ビルド設定/実機ログのスタックトレースを軽くする（Log・Warningのみ）")]
    private static void Apply()
    {
        var beforeLog = PlayerSettings.GetStackTraceLogType(LogType.Log);
        var beforeWarn = PlayerSettings.GetStackTraceLogType(LogType.Warning);
        PlayerSettings.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
        PlayerSettings.SetStackTraceLogType(LogType.Warning, StackTraceLogType.None);
        AssetDatabase.SaveAssets();
        Debug.Log($"[PlayerLogStackTraceTool] 実機ビルドのスタックトレース：Log {beforeLog}→None、Warning {beforeWarn}→None" +
                  "（Error/Exception/Assertは変更なし）。Project Settings > Player > Other Settings > Stack Trace");
    }
}
