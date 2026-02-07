using UnityEngine;
using Game.Progress;  // ProgressManager / AreaIds を使う

public class StageClearRelay : MonoBehaviour
{
    [Header("このボタンで記録する Area / Stage")]
    [Tooltip("空の場合は ProgressManager.Data.selectedAreaId を使用")]
    public string areaId = "";
    [Tooltip("0 の場合は現在プレイ中のステージ番号を自動判定（未実装の場合は 1）")]
    public int stage = 0;

    /// <summary>
    /// Button.OnClick から呼ぶ。ステージクリアを永続保存。
    /// 既にクリア済みなら second press 以降は new? False になる。
    /// </summary>
    public void MarkClear()
    {
        if (ProgressManager.Instance == null)
        {
            Debug.LogError("[StageClearRelay] ProgressManager not found.");
            return;
        }

        // areaId が空の場合は ProgressManager から取得
        string targetAreaId = string.IsNullOrEmpty(areaId)
            ? ProgressManager.Instance.Data.selectedAreaId
            : areaId;

        if (string.IsNullOrEmpty(targetAreaId))
        {
            Debug.LogError("[StageClearRelay] Area ID is not set!");
            return;
        }

        // stage が 0 の場合は自動判定（現在は固定で最終ステージとして扱う）
        int targetStage = stage;
        if (targetStage == 0)
        {
            // TODO: GameSession から現在のステージ番号を取得
            // 暫定的に、AreaDB から最大ステージ番号を取得
            var stageNumbers = Game.Progress.AreaDB.Instance?.GetStageNumbers(targetAreaId);
            if (stageNumbers != null && stageNumbers.Length > 0)
            {
                targetStage = stageNumbers[stageNumbers.Length - 1]; // 最終ステージ
                Debug.Log($"[StageClearRelay] Auto-detected final stage: {targetStage}");
            }
            else
            {
                targetStage = 3; // デフォルト
                Debug.LogWarning($"[StageClearRelay] Could not detect stage number, using default: {targetStage}");
            }
        }

        bool changed = ProgressManager.Instance.MarkStageCleared(targetAreaId, targetStage);
        Debug.Log($"[Progress] {targetAreaId} Stage {targetStage} cleared. (new? {changed})");
    }
}