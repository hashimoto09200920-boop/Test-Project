using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Game.Progress;

namespace Game.UI
{
    /// <summary>
    /// 03_AreaSelect シーンの各Stageボタンを管理
    /// ロック/アンロック状態の表示とデバッグ機能を提供
    /// </summary>
    [DisallowMultipleComponent]
    public class StageButton : MonoBehaviour
    {
        [Header("Stage Settings")]
        [Tooltip("このボタンが表すエリアID（例：Area_01）")]
        public string areaId = "Area_01";

        [Tooltip("このボタンが表すステージ番号（通常は1）")]
        public int stageNumber = 1;

        [Header("UI References")]
        [Tooltip("ロック時に表示するオーバーレイパネル")]
        public GameObject lockOverlay;

        [Tooltip("デバッグ表示用のTextコンポーネント（任意）")]
        public TextMeshProUGUI debugText;

        [Header("Test Mode (Inspector)")]
        [Tooltip("テストモードを有効にすると、実際のアンロック判定を無視して下記の設定を使用")]
        public bool useTestMode = false;

        [Tooltip("テストモード時の強制アンロック状態")]
        public bool forceUnlocked = false;

        [Header("Debug Display")]
        [Tooltip("デバッグテキストを表示するか")]
        public bool showDebugText = true;

        [Tooltip("デバッグテキストの色（アンロック時）")]
        public Color unlockedColor = Color.green;

        [Tooltip("デバッグテキストの色（ロック時）")]
        public Color lockedColor = Color.red;

        private Button button;
        private bool isUnlocked = false;

        private void Awake()
        {
            button = GetComponent<Button>();
        }

        private void Start()
        {
            UpdateLockStatus();
        }

        private void OnValidate()
        {
            // Inspector で値が変更されたときにリアルタイム更新
            if (Application.isPlaying)
            {
                UpdateLockStatus();
            }
        }

        /// <summary>
        /// ロック状態を更新して表示に反映
        /// </summary>
        public void UpdateLockStatus()
        {
            // アンロック判定
            if (useTestMode)
            {
                // テストモード：Inspector の設定を使用
                isUnlocked = forceUnlocked;
            }
            else
            {
                // 通常モード：UnlockRules を使用して判定
                if (UnlockRules.IsAreaUnlocked(areaId))
                {
                    // エリア自体がアンロックされている
                    isUnlocked = true;
                }
                else
                {
                    isUnlocked = false;
                }
            }

            // UI反映
            ApplyLockState();
        }

        /// <summary>
        /// ロック状態をUIに反映
        /// </summary>
        private void ApplyLockState()
        {
            // LockOverlay の表示/非表示
            if (lockOverlay != null)
            {
                lockOverlay.SetActive(!isUnlocked);
            }

            // ボタンの有効/無効
            if (button != null)
            {
                button.interactable = isUnlocked;
            }

            // デバッグテキストの更新
            UpdateDebugText();
        }

        /// <summary>
        /// デバッグテキストを更新
        /// </summary>
        private void UpdateDebugText()
        {
            if (debugText == null)
            {
                return;
            }

            if (showDebugText)
            {
                debugText.gameObject.SetActive(true);

                // テキスト内容
                string statusText = isUnlocked ? "UNLOCKED" : "LOCKED";
                string modeText = useTestMode ? " [TEST]" : "";

                // アンロック条件を表示（詳細モード）
                string conditionText = GetUnlockConditionDebugText();

                debugText.text = $"{statusText}{modeText}\n{conditionText}";

                // テキスト色
                debugText.color = isUnlocked ? unlockedColor : lockedColor;
            }
            else
            {
                debugText.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// アンロック条件のデバッグテキストを取得
        /// </summary>
        private string GetUnlockConditionDebugText()
        {
            if (useTestMode) return "";

            // AreaDBから条件を取得
            if (AreaDB.Instance == null || AreaDB.Instance.Catalog == null)
                return "(No AreaDB)";

            var area = System.Linq.Enumerable.FirstOrDefault(
                AreaDB.Instance.Catalog.areas,
                a => a != null && a.areaId == areaId);

            if (area == null)
                return "(No AreaDef)";

            var conds = area.unlockByStages;
            if (conds == null || conds.Count == 0)
                return "(No conditions)";

            // 条件を表示
            var condTexts = new System.Collections.Generic.List<string>();
            foreach (var c in conds)
            {
                bool cleared = ProgressManager.Instance?.IsStageCleared(c.areaId, c.stageNumber) ?? false;
                string checkMark = cleared ? "○" : "×";
                condTexts.Add($"{checkMark}{c.areaId} S{c.stageNumber}");
            }

            return string.Join(", ", condTexts);
        }

        /// <summary>
        /// 現在のロック状態を取得
        /// </summary>
        public bool IsUnlocked()
        {
            return isUnlocked;
        }

        /// <summary>
        /// ロック状態を強制的に再チェック（外部から呼び出し用）
        /// </summary>
        public void RefreshLockStatus()
        {
            UpdateLockStatus();
        }

#if UNITY_EDITOR
        [ContextMenu("Force Update Lock Status")]
        private void ForceUpdateLockStatus()
        {
            UpdateLockStatus();
            Debug.Log($"[StageButton] {gameObject.name} - Area: {areaId}, Unlocked: {isUnlocked}");
        }

        [ContextMenu("Toggle Test Mode")]
        private void ToggleTestMode()
        {
            useTestMode = !useTestMode;
            UpdateLockStatus();
        }

        [ContextMenu("Toggle Force Unlocked")]
        private void ToggleForceUnlocked()
        {
            forceUnlocked = !forceUnlocked;
            if (useTestMode)
            {
                UpdateLockStatus();
            }
        }

        [ContextMenu("Setup: Create Debug Text")]
        private void CreateDebugText()
        {
            // 既に存在する場合はスキップ
            if (debugText != null)
            {
                Debug.LogWarning($"[StageButton] Debug text already exists on {gameObject.name}");
                return;
            }

            // 既存のDebugTextを検索
            Transform existingDebugText = transform.Find("DebugText");
            if (existingDebugText != null)
            {
                debugText = existingDebugText.GetComponent<TextMeshProUGUI>();
                if (debugText != null)
                {
                    Debug.Log($"[StageButton] Found existing DebugText on {gameObject.name}");
                    return;
                }
            }

            // 新しくGameObjectを作成
            GameObject debugTextObj = new GameObject("DebugText");
            debugTextObj.transform.SetParent(transform, false);

            // RectTransformの設定
            RectTransform rectTransform = debugTextObj.AddComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0.5f, 1f); // Top-Center
            rectTransform.anchorMax = new Vector2(0.5f, 1f);
            rectTransform.pivot = new Vector2(0.5f, 1f);
            rectTransform.anchoredPosition = new Vector2(0, -10); // ボタン上部から10px下
            rectTransform.sizeDelta = new Vector2(150, 30);

            // TextMeshProUGUIコンポーネントを追加
            debugText = debugTextObj.AddComponent<TextMeshProUGUI>();
            debugText.text = "DEBUG";
            debugText.fontSize = 20;
            debugText.alignment = TextAlignmentOptions.Center;
            debugText.color = Color.white;

            Debug.Log($"[StageButton] Created debug text for {gameObject.name}");

            // エディタを更新
            UnityEditor.EditorUtility.SetDirty(gameObject);
        }

        [ContextMenu("Setup: Find LockOverlay")]
        private void FindLockOverlay()
        {
            // 既に設定されている場合はスキップ
            if (lockOverlay != null)
            {
                Debug.LogWarning($"[StageButton] LockOverlay already assigned on {gameObject.name}");
                return;
            }

            // 子オブジェクトから"LockOverlay"という名前のオブジェクトを検索
            Transform overlay = transform.Find("LockOverlay");
            if (overlay != null)
            {
                lockOverlay = overlay.gameObject;
                Debug.Log($"[StageButton] Found LockOverlay on {gameObject.name}");
                UnityEditor.EditorUtility.SetDirty(gameObject);
                return;
            }

            // "Lock"を含む名前のオブジェクトを検索
            foreach (Transform child in transform)
            {
                if (child.name.Contains("Lock") || child.name.Contains("lock"))
                {
                    lockOverlay = child.gameObject;
                    Debug.Log($"[StageButton] Found lock overlay: {child.name} on {gameObject.name}");
                    UnityEditor.EditorUtility.SetDirty(gameObject);
                    return;
                }
            }

            Debug.LogWarning($"[StageButton] Could not find LockOverlay on {gameObject.name}. Please assign manually.");
        }

        [ContextMenu("Setup: Auto Setup (Find All)")]
        private void AutoSetup()
        {
            FindLockOverlay();
            CreateDebugText();
            Debug.Log($"[StageButton] Auto setup completed for {gameObject.name}");
        }
#endif
    }
}
