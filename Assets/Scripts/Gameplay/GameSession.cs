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
    /// trueで05_Gameへ遷移すると、TutorialFlowControllerがチュートリアルを表示する
    /// （表示開始時に消費されfalseに戻る。多重起動防止）
    /// </summary>
    public static bool StartInTutorialMode { get; set; }

    /// <summary>
    /// 現在チュートリアル進行中かどうか。StartInTutorialModeと違い、TutorialFlowController.Awake()でセットされてから
    /// チュートリアル終了までtrueのまま（消費されない）。GameManager等が「装備ジェム/ドリンク効果を適用しない」等の
    /// 判定に使う。AwakeでセットするのはAwakeが全MonoBehaviourのStartより先に完了する保証を使うため（実行順に依存しない）
    /// </summary>
    public static bool IsInTutorial { get; set; }

    /// <summary>
    /// F1デバッグテストエリア(Area0Config)経由の起動かどうか。
    /// trueの間はスタミナ消費の対象外にする（Editor専用の動作確認をスタミナで詰まらせないため）。
    /// </summary>
    public static bool IsTestArea { get; set; }

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
        StartInTutorialMode = false;
        IsInTutorial = false;
        IsTestArea = false;
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
