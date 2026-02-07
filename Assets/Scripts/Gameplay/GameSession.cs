using UnityEngine;

/// <summary>
/// Scene間でゲームセッション情報を保持する静的クラス
/// AreaSelect → Game へのデータ受け渡しに使用
/// </summary>
public static class GameSession
{
    /// <summary>
    /// 選択されたエリア設定
    /// </summary>
    public static AreaConfig SelectedArea { get; set; }

    /// <summary>
    /// 選択されたステージ番号（AreaLaunchRandom で設定）
    /// </summary>
    public static int SelectedStageNumber { get; set; }

    /// <summary>
    /// AreaSelectシーンから明示的に設定されたかどうか
    /// （Unity Editor で直接05_Gameを再生した場合と区別するため）
    /// </summary>
    public static bool WasExplicitlySet { get; set; }

    /// <summary>
    /// プレイヤーのスコア（将来的な拡張用）
    /// </summary>
    public static int CurrentScore { get; set; }

    /// <summary>
    /// プレイヤーのライフ数（将来的な拡張用）
    /// </summary>
    public static int RemainingLives { get; set; }

    /// <summary>
    /// セッション情報をリセット
    /// </summary>
    public static void Reset()
    {
        SelectedArea = null;
        SelectedStageNumber = 0;
        CurrentScore = 0;
        RemainingLives = 3;
        WasExplicitlySet = false;
    }

    /// <summary>
    /// 選択されたエリアが有効かチェック
    /// （AreaSelectから明示的に設定された場合のみtrueを返す）
    /// </summary>
    public static bool HasValidArea()
    {
        return WasExplicitlySet && SelectedArea != null && SelectedArea.IsValid();
    }

    /// <summary>
    /// デバッグ情報を出力
    /// </summary>
    public static void LogCurrentSession()
    {
        if (SelectedArea != null)
        {
            Debug.Log($"[GameSession] Area: {SelectedArea.GetDisplayName()}, Score: {CurrentScore}, Lives: {RemainingLives}");
        }
        else
        {
            Debug.Log("[GameSession] No area selected");
        }
    }
}
