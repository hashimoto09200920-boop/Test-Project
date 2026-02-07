using System.Collections.Generic;
using UnityEngine;
using Game.Progress;

/// <summary>
/// デバッグ注入：Inspector の設定に従って
/// - Area_01 / Area_02 のステージ解放
/// - Basic/Relic Unit の所持
/// - Gold の所持数
/// を適用（F2）／クリア（F3）する。
/// ※「クリア」は Progress 全消去ではなく、Inspector で指定した分だけを元に戻します。
/// 推奨設置：最初に開く Title シーンの空オブジェクトにアタッチ。
/// </summary>
public class ProgressDebugInjector : MonoBehaviour
{
    [Header("再生開始時に自動適用するか")]
    [Tooltip("ONだと Play 開始時に下の全項目を適用します")]
    public bool applyOnStart = true;

    [Header("解放させたい Area_01 のID（例: 1,2,3）")]
    public List<int> area01StagesToUnlock = new List<int>();

    [Header("解放させたい Area_02 のID（例: 1,2,3,4）")]
    public List<int> area02StagesToUnlock = new List<int>();

    [Header("所持させたい Basic_Unit のID（例: UB1, UB2）")]
    public List<string> basicUnitIds = new List<string>();

    [Header("所持させたい Relic_Unit のID（例: RB1, RB2）")]
    public List<string> relicUnitIds = new List<string>();

    [Header("所持させたい Gold の数値（例: 1000）")]
    [Min(0)]
    public int goldAmount = 0;

    private void Start()
    {
        if (applyOnStart)
        {
            ApplyAll();
        }
    }

    private void Update()
    {
        // F2：Inspector の全項目を適用（Unlock / Gold セット）
        if (Input.GetKeyDown(KeyCode.F2))
        {
            ApplyAll();
        }
        // F3：Inspector の全項目をクリア（適用分だけ元に戻す）
        if (Input.GetKeyDown(KeyCode.F3))
        {
            ClearInspectorItemsOnly();
        }
    }

    /// <summary>
    /// Inspector の全項目を適用（永続化）
    /// </summary>
    public void ApplyAll()
    {
        if (!EnsurePM()) return;
        var pm = ProgressManager.Instance;

        int newUnlocks = 0;

        // --- Area_01 / Area_02 のステージ解放
        foreach (var st in area01StagesToUnlock)
        {
            if (st <= 0) continue;
            if (pm.MarkStageCleared(AreaIds.Area_01, st)) newUnlocks++;
        }
        foreach (var st in area02StagesToUnlock)
        {
            if (st <= 0) continue;
            if (pm.MarkStageCleared(AreaIds.Area_02, st)) newUnlocks++;
        }

        // --- Unit の所持設定
        foreach (var id in basicUnitIds)
        {
            if (string.IsNullOrWhiteSpace(id)) continue;
            if (pm.UnlockBasicUnit(id)) newUnlocks++;
        }
        foreach (var id in relicUnitIds)
        {
            if (string.IsNullOrWhiteSpace(id)) continue;
            if (pm.UnlockRelicUnit(id)) newUnlocks++;
        }

        // --- Gold の絶対値セット（Add/Spend で差分反映）
        SetGoldAbsolute(goldAmount);

        Debug.Log($"[ProgressDebugInjector] Applied. Newly unlocked or changed: {newUnlocks}, Gold set to {goldAmount:N0}");
    }

    /// <summary>
    /// Inspector の全項目をクリア（Progress 全消去ではない）
    /// - Area_01/02：指定ステージのみ「未クリア化」
    /// - Unit：指定IDのみ「所持解除」
    /// - Gold：0 に戻す（Inspector 項目の“クリア”に合わせる）
    /// </summary>
    public void ClearInspectorItemsOnly()
    {
        if (!EnsurePM()) return;
        var pm = ProgressManager.Instance;

        // --- Area_01 / Area_02 のステージを“指定分だけ”未クリア化
        UndoStages(pm, AreaIds.Area_01, area01StagesToUnlock);
        UndoStages(pm, AreaIds.Area_02, area02StagesToUnlock);

        // --- Unit の所持解除（指定分のみ）
        int removed = 0;
        removed += RemoveOwnedIds(pm.Data.ownedBasicUnitIds, basicUnitIds);
        removed += RemoveOwnedIds(pm.Data.ownedRelicUnitIds, relicUnitIds);

        // --- Gold を 0 に
        SetGoldAbsolute(0);

        pm.Save();
        Debug.Log($"[ProgressDebugInjector] Cleared inspector items. Removed entries: {removed}, Gold set to 0");
    }

    // ===== 内部ユーティリティ =====

    private static bool EnsurePM()
    {
        if (ProgressManager.Instance == null)
        {
            Debug.LogError("[ProgressDebugInjector] ProgressManager not found.");
            return false;
        }
        return true;
    }

    private static void UndoStages(ProgressManager pm, string areaId, List<int> targets)
    {
        if (targets == null || targets.Count == 0) return;
        var ap = pm.Data.GetOrCreateArea(areaId);
        // 指定されたステージだけを削除（他の進捗は保持）
        foreach (var st in targets)
        {
            ap.clearedStages.Remove(st);
        }
    }

    private static int RemoveOwnedIds(List<string> ownedList, List<string> toRemove)
    {
        if (ownedList == null || toRemove == null) return 0;
        int count = 0;
        foreach (var id in toRemove)
        {
            if (string.IsNullOrWhiteSpace(id)) continue;
            if (ownedList.Remove(id)) count++;
        }
        return count;
    }

    /// <summary>
    /// Gold を“絶対値”で設定（Add/Spend を差分計算で使用）
    /// </summary>
    private static void SetGoldAbsolute(int target)
    {
        var pm = ProgressManager.Instance;
        int current = pm.GetGold();
        if (target < 0) target = 0;

        if (target > current)
        {
            pm.AddGold(target - current);
        }
        else if (target < current)
        {
            // SpendGold は不足だと失敗する仕様なので、差分ずつ消費
            int needSpend = current - target;
            pm.SpendGold(needSpend);
        }
        // 同値なら何もしない
    }
}