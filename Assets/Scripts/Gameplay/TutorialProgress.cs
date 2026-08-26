using UnityEngine;

/// <summary>
/// チュートリアル既読フラグをセーブデータ全体で永続管理する静的クラス（PlayerPrefs保存）。
/// GemManager/ProgressManager等の永続系と同じくPlayerPrefsを使う（DrinkSessionのようなプレイ単位のリセットはしない）。
/// </summary>
public static class TutorialProgress
{
    private const string PrefsKey = "TutorialSeen";

    /// <summary>チュートリアルを一度でも最後まで見た（またはスキップした）ことがあるか</summary>
    public static bool HasSeenTutorial => PlayerPrefs.GetInt(PrefsKey, 0) == 1;

    /// <summary>既読として記録する（完了・スキップ問わず、チュートリアルフローが終わった時点で呼ぶ）</summary>
    public static void MarkSeen()
    {
        PlayerPrefs.SetInt(PrefsKey, 1);
        PlayerPrefs.Save();
    }

    /// <summary>既読フラグをリセットする（ゲーム進行度初期化・再度チュートリアルを見せたい時に使う。ビルドでも動作する）</summary>
    public static void Reset()
    {
        PlayerPrefs.DeleteKey(PrefsKey);
        PlayerPrefs.Save();
    }

    /// <summary>デバッグ用：既読フラグをリセットする（Editor専用、互換のため残置）</summary>
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    public static void ResetForDebug() => Reset();
}
