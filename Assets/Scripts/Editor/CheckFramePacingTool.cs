using UnityEditor;
using UnityEngine;

/// <summary>★一時的な確認ツール（実行後は削除してよい）。Optimized Frame Pacingの現在値を出す。</summary>
public static class CheckFramePacingTool
{
    [MenuItem("Tools/★Optimized Frame Pacingを確認")]
    public static void Check()
    {
        Debug.Log($"[CheckFramePacingTool] PlayerSettings.Android.optimizedFramePacing = {PlayerSettings.Android.optimizedFramePacing}");
    }
}
