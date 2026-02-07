using UnityEngine;
using Game.Progress;

namespace Game.UI
{
    /// <summary>
    /// 03_AreaSelect シーンの全StageButtonを一括管理
    /// テスト用の便利機能を提供
    /// </summary>
    [DisallowMultipleComponent]
    public class StageButtonManager : MonoBehaviour
    {
        [Header("Stage Buttons")]
        [Tooltip("管理対象のStageButtonリスト（自動検索も可能）")]
        public StageButton[] stageButtons;

        [Header("Auto Find Settings")]
        [Tooltip("Start時に自動的に子オブジェクトからStageButtonを検索")]
        public bool autoFindButtons = true;

        [Header("Test Controls")]
        [Tooltip("全てのボタンでテストモードを有効化")]
        public bool enableTestModeForAll = false;

        [Tooltip("テストモード時に全てのボタンをアンロック")]
        public bool unlockAllInTestMode = false;

        [Header("Debug")]
        [Tooltip("Start時にロック状態をログ出力")]
        public bool logStatusOnStart = true;

        private void Start()
        {
            if (autoFindButtons)
            {
                FindAllStageButtons();
            }

            if (enableTestModeForAll)
            {
                ApplyTestModeToAll();
            }

            RefreshAllButtons();

            if (logStatusOnStart)
            {
                LogAllButtonStatus();
            }
        }

        private void OnEnable()
        {
            // シーンが再度有効になった時（ゲームから戻った時など）にボタン状態を更新
            if (Application.isPlaying && stageButtons != null && stageButtons.Length > 0)
            {
                RefreshAllButtons();
                Debug.Log("[StageButtonManager] OnEnable: Refreshed all buttons");
            }
        }

        /// <summary>
        /// シーン内の全StageButtonを検索
        /// </summary>
        [ContextMenu("Find All Stage Buttons")]
        public void FindAllStageButtons()
        {
            stageButtons = FindObjectsOfType<StageButton>();
            Debug.Log($"[StageButtonManager] Found {stageButtons.Length} StageButtons");
        }

        /// <summary>
        /// 全てのStageButtonのロック状態を更新
        /// </summary>
        [ContextMenu("Refresh All Buttons")]
        public void RefreshAllButtons()
        {
            if (stageButtons == null || stageButtons.Length == 0)
            {
                Debug.LogWarning("[StageButtonManager] No stage buttons to refresh!");
                return;
            }

            foreach (var btn in stageButtons)
            {
                if (btn != null)
                {
                    btn.RefreshLockStatus();
                }
            }

            Debug.Log($"[StageButtonManager] Refreshed {stageButtons.Length} buttons");
        }

        /// <summary>
        /// 全てのボタンにテストモードを適用
        /// </summary>
        [ContextMenu("Apply Test Mode To All")]
        public void ApplyTestModeToAll()
        {
            if (stageButtons == null || stageButtons.Length == 0)
            {
                Debug.LogWarning("[StageButtonManager] No stage buttons found!");
                return;
            }

            foreach (var btn in stageButtons)
            {
                if (btn != null)
                {
                    btn.useTestMode = enableTestModeForAll;
                    btn.forceUnlocked = unlockAllInTestMode;
                    btn.RefreshLockStatus();
                }
            }

            Debug.Log($"[StageButtonManager] Applied test mode to all buttons. TestMode: {enableTestModeForAll}, ForceUnlock: {unlockAllInTestMode}");
        }

        /// <summary>
        /// 全てのボタンの状態をログ出力
        /// </summary>
        [ContextMenu("Log All Button Status")]
        public void LogAllButtonStatus()
        {
            if (stageButtons == null || stageButtons.Length == 0)
            {
                Debug.LogWarning("[StageButtonManager] No stage buttons found!");
                return;
            }

            Debug.Log("=== Stage Button Status ===");
            foreach (var btn in stageButtons)
            {
                if (btn != null)
                {
                    string testMode = btn.useTestMode ? " [TEST MODE]" : "";
                    string status = btn.IsUnlocked() ? "UNLOCKED" : "LOCKED";
                    Debug.Log($"  {btn.gameObject.name} ({btn.areaId}): {status}{testMode}");
                }
            }
            Debug.Log("===========================");
        }

        /// <summary>
        /// 全てのボタンの状態を詳細にログ出力（アンロック条件含む）
        /// </summary>
        [ContextMenu("Log Detailed Status")]
        public void LogDetailedStatus()
        {
            if (stageButtons == null || stageButtons.Length == 0)
            {
                Debug.LogWarning("[StageButtonManager] No stage buttons found!");
                return;
            }

            Debug.Log("=== Detailed Stage Button Status ===");

            // ProgressManagerの情報
            if (ProgressManager.Instance != null)
            {
                Debug.Log("[ProgressManager] Instance found");
                if (ProgressManager.Instance.Data != null && ProgressManager.Instance.Data.areas != null)
                {
                    Debug.Log($"[ProgressManager] Areas in progress: {ProgressManager.Instance.Data.areas.Count}");
                    foreach (var area in ProgressManager.Instance.Data.areas)
                    {
                        if (area != null && area.clearedStages != null)
                        {
                            Debug.Log($"  {area.areaId}: Cleared stages = [{string.Join(", ", area.clearedStages)}]");
                        }
                    }
                }
            }
            else
            {
                Debug.LogWarning("[ProgressManager] Instance is NULL!");
            }

            // AreaDBの情報
            if (AreaDB.Instance != null && AreaDB.Instance.Catalog != null)
            {
                Debug.Log($"[AreaDB] Catalog loaded with {AreaDB.Instance.Catalog.areas.Count} areas");
            }
            else
            {
                Debug.LogWarning("[AreaDB] Instance or Catalog is NULL!");
            }

            Debug.Log("\n--- Button Status ---");
            foreach (var btn in stageButtons)
            {
                if (btn != null)
                {
                    string testMode = btn.useTestMode ? " [TEST MODE]" : "";
                    string status = btn.IsUnlocked() ? "UNLOCKED" : "LOCKED";

                    // アンロック条件を取得
                    string conditions = GetUnlockConditionsText(btn.areaId);

                    Debug.Log($"  {btn.gameObject.name} ({btn.areaId}): {status}{testMode}\n    Conditions: {conditions}");
                }
            }
            Debug.Log("=====================================");
        }

        private string GetUnlockConditionsText(string areaId)
        {
            if (AreaDB.Instance == null || AreaDB.Instance.Catalog == null)
                return "AreaDB not loaded";

            var area = System.Linq.Enumerable.FirstOrDefault(
                AreaDB.Instance.Catalog.areas,
                a => a != null && a.areaId == areaId);

            if (area == null)
                return "Area not found in catalog";

            var conds = area.unlockByStages;
            if (conds == null || conds.Count == 0)
                return "Always unlocked (no conditions)";

            var condTexts = new System.Collections.Generic.List<string>();
            foreach (var c in conds)
            {
                bool cleared = ProgressManager.Instance?.IsStageCleared(c.areaId, c.stageNumber) ?? false;
                string checkMark = cleared ? "○" : "×";
                condTexts.Add($"{checkMark} {c.areaId} Stage {c.stageNumber}");
            }

            return string.Join(", ", condTexts);
        }

        /// <summary>
        /// 全てのボタンをアンロック（テスト用）
        /// </summary>
        [ContextMenu("Test: Unlock All")]
        public void TestUnlockAll()
        {
            enableTestModeForAll = true;
            unlockAllInTestMode = true;
            ApplyTestModeToAll();
            Debug.Log("[StageButtonManager] Test: All buttons unlocked");
        }

        /// <summary>
        /// 全てのボタンをロック（テスト用）
        /// </summary>
        [ContextMenu("Test: Lock All")]
        public void TestLockAll()
        {
            enableTestModeForAll = true;
            unlockAllInTestMode = false;
            ApplyTestModeToAll();
            Debug.Log("[StageButtonManager] Test: All buttons locked");
        }

        /// <summary>
        /// テストモードを解除して実際のアンロック判定に戻す
        /// </summary>
        [ContextMenu("Test: Use Real Unlock Status")]
        public void UseRealUnlockStatus()
        {
            enableTestModeForAll = false;

            if (stageButtons != null)
            {
                foreach (var btn in stageButtons)
                {
                    if (btn != null)
                    {
                        btn.useTestMode = false;
                        btn.RefreshLockStatus();
                    }
                }
            }

            Debug.Log("[StageButtonManager] Switched to real unlock status");
        }

        /// <summary>
        /// ProgressManagerの進捗をリセット（デバッグ用）
        /// </summary>
        [ContextMenu("Debug: Reset Progress")]
        public void DebugResetProgress()
        {
            if (ProgressManager.Instance != null)
            {
                ProgressManager.Instance.ResetAll();
                Debug.Log("[StageButtonManager] Progress reset!");
                RefreshAllButtons();
            }
            else
            {
                Debug.LogWarning("[StageButtonManager] ProgressManager not found!");
            }
        }

        /// <summary>
        /// Area_01のStage 1をクリア済みにする（テスト用）
        /// </summary>
        [ContextMenu("Debug: Clear Area_01 Stage 1")]
        public void DebugClearArea01Stage1()
        {
            if (ProgressManager.Instance != null)
            {
                ProgressManager.Instance.MarkStageCleared("Area_01", 1);
                Debug.Log("[StageButtonManager] Marked Area_01 Stage 1 as cleared");
                RefreshAllButtons();
            }
        }

        /// <summary>
        /// Area_01のStage 3をクリア済みにする（Area_02のアンロック条件を満たす）
        /// </summary>
        [ContextMenu("Debug: Clear Area_01 Stage 3 (Unlock Area_02)")]
        public void DebugClearArea01Stage3()
        {
            if (ProgressManager.Instance != null)
            {
                ProgressManager.Instance.MarkStageCleared("Area_01", 3);
                Debug.Log("[StageButtonManager] Marked Area_01 Stage 3 as cleared → Area_02 should unlock");
                RefreshAllButtons();
            }
            else
            {
                Debug.LogWarning("[StageButtonManager] ProgressManager not found!");
            }
        }

        /// <summary>
        /// Area_01のStage 3のクリアを解除する（Area_02を再ロック）
        /// </summary>
        [ContextMenu("Debug: Unclear Area_01 Stage 3 (Lock Area_02)")]
        public void DebugUnclearArea01Stage3()
        {
            if (ProgressManager.Instance != null)
            {
                // Stage 3のクリア状態を削除
                var areaData = ProgressManager.Instance.Data.areas.Find(a => a.areaId == "Area_01");
                if (areaData != null && areaData.clearedStages != null)
                {
                    areaData.clearedStages.Remove(3);
                    Debug.Log("[StageButtonManager] Removed Area_01 Stage 3 from cleared stages → Area_02 should lock");
                    RefreshAllButtons();
                }
                else
                {
                    Debug.LogWarning("[StageButtonManager] Area_01 data not found");
                }
            }
            else
            {
                Debug.LogWarning("[StageButtonManager] ProgressManager not found!");
            }
        }

        /// <summary>
        /// 指定したエリアのステージをクリア済みにする（汎用メソッド）
        /// </summary>
        public void ClearAreaStage(string areaId, int stageNumber)
        {
            if (ProgressManager.Instance != null)
            {
                ProgressManager.Instance.MarkStageCleared(areaId, stageNumber);
                Debug.Log($"[StageButtonManager] Marked {areaId} Stage {stageNumber} as cleared");
                RefreshAllButtons();
            }
            else
            {
                Debug.LogWarning("[StageButtonManager] ProgressManager not found!");
            }
        }

        /// <summary>
        /// 指定したエリアのステージのクリアを解除する（汎用メソッド）
        /// </summary>
        public void UnclearAreaStage(string areaId, int stageNumber)
        {
            if (ProgressManager.Instance != null)
            {
                var areaData = ProgressManager.Instance.Data.areas.Find(a => a.areaId == areaId);
                if (areaData != null && areaData.clearedStages != null)
                {
                    areaData.clearedStages.Remove(stageNumber);
                    Debug.Log($"[StageButtonManager] Removed {areaId} Stage {stageNumber} from cleared stages");
                    RefreshAllButtons();
                }
                else
                {
                    Debug.LogWarning($"[StageButtonManager] {areaId} data not found");
                }
            }
            else
            {
                Debug.LogWarning("[StageButtonManager] ProgressManager not found!");
            }
        }

        /// <summary>
        /// デバッグテキストの表示/非表示を切り替え
        /// </summary>
        [ContextMenu("Toggle Debug Text")]
        public void ToggleDebugText()
        {
            if (stageButtons != null)
            {
                bool newState = !stageButtons[0]?.showDebugText ?? true;
                foreach (var btn in stageButtons)
                {
                    if (btn != null)
                    {
                        btn.showDebugText = newState;
                        btn.RefreshLockStatus();
                    }
                }
                Debug.Log($"[StageButtonManager] Debug text: {(newState ? "ON" : "OFF")}");
            }
        }

        /// <summary>
        /// ボタン名から自動的にareaIdを設定（Btn_Area_01 → Area_01）
        /// </summary>
        [ContextMenu("Setup: Auto Set Area IDs from Button Names")]
        public void AutoSetAreaIdsFromButtonNames()
        {
            if (stageButtons == null || stageButtons.Length == 0)
            {
                Debug.LogWarning("[StageButtonManager] No stage buttons found!");
                return;
            }

            int successCount = 0;
            foreach (var btn in stageButtons)
            {
                if (btn == null) continue;

                string buttonName = btn.gameObject.name;

                // "Btn_Area_01" から "Area_01" を抽出
                if (buttonName.Contains("Area_"))
                {
                    int startIndex = buttonName.IndexOf("Area_");
                    string extractedAreaId = buttonName.Substring(startIndex);

                    // 数字以降の余計な文字を削除（例：Area_01_old → Area_01）
                    int digitEndIndex = 5; // "Area_" の長さ
                    while (digitEndIndex < extractedAreaId.Length &&
                           char.IsDigit(extractedAreaId[digitEndIndex]))
                    {
                        digitEndIndex++;
                    }
                    extractedAreaId = extractedAreaId.Substring(0, digitEndIndex);

                    btn.areaId = extractedAreaId;
                    successCount++;

                    Debug.Log($"[StageButtonManager] {buttonName} → areaId = {extractedAreaId}");
#if UNITY_EDITOR
                    UnityEditor.EditorUtility.SetDirty(btn);
#endif
                }
                else
                {
                    Debug.LogWarning($"[StageButtonManager] Could not extract areaId from button name: {buttonName}");
                }
            }

            Debug.Log($"[StageButtonManager] Auto-set area IDs complete. {successCount}/{stageButtons.Length} buttons updated.");

            // 更新後にリフレッシュ
            RefreshAllButtons();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Inspector で値が変更されたときにリアルタイム適用
            if (Application.isPlaying && stageButtons != null && stageButtons.Length > 0)
            {
                ApplyTestModeToAll();
            }
        }
#endif
    }
}
