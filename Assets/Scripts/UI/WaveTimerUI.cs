using UnityEngine;
using TMPro;

/// <summary>
/// ウェーブシステムのタイマーとステージ情報を表示するUI
/// </summary>
public class WaveTimerUI : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("タイマー表示用のTextMeshPro")]
    [SerializeField] private TextMeshProUGUI timerText;

    [Tooltip("ウェーブ段階表示用のTextMeshPro（Wave 1/3など）")]
    [SerializeField] private TextMeshProUGUI stageText;

    [Tooltip("配置パターン名表示用のTextMeshPro（デバッグ用）")]
    [SerializeField] private TextMeshProUGUI formationText;

    [Header("EnemySpawner Reference")]
    [Tooltip("情報を取得するEnemySpawner")]
    [SerializeField] private EnemySpawner enemySpawner;

    [Header("Display Settings")]
    [Tooltip("配置パターン名を表示するか（デバッグ用）")]
    [SerializeField] private bool showFormationName = true;

    private void Start()
    {
        // EnemySpawner の設定を検証（警告のみ）
        if (enemySpawner != null)
        {
            int totalStages = enemySpawner.GetTotalStageCount();

            if (totalStages == 0)
            {
                Debug.LogWarning("[WaveTimerUI] Total Stages is 0. Please check if 'Use Wave System' is enabled and 'Wave Stages' are configured in EnemySpawner.");
            }
        }
        else
        {
            Debug.LogWarning("[WaveTimerUI] EnemySpawner reference is missing!");
        }
    }

    private void Update()
    {
        if (enemySpawner == null) return;

        // タイマー更新
        UpdateTimerDisplay();

        // ステージ表示更新
        UpdateStageDisplay();

        // 配置パターン名表示更新
        if (showFormationName)
        {
            UpdateFormationDisplay();
        }
    }

    /// <summary>
    /// タイマー表示を更新
    /// </summary>
    private void UpdateTimerDisplay()
    {
        if (timerText == null) return;

        float remainingTime = enemySpawner.GetStageRemainingTime();
        float timeLimit = enemySpawner.GetCurrentStageTimeLimit();

        // 時間制限がない場合は非表示
        if (timeLimit <= 0)
        {
            timerText.text = "";
            return;
        }

        // 残り時間を0以上にクランプ（負の値を防ぐ）
        remainingTime = Mathf.Max(0f, remainingTime);

        // 残り時間を分:秒形式で表示（分は1桁、秒は2桁）
        int minutes = Mathf.FloorToInt(remainingTime / 60f);
        int seconds = Mathf.FloorToInt(remainingTime % 60f);

        timerText.text = $"{minutes}:{seconds:00}";

        // 残り時間が少なくなったら色を変える（オプション）
        if (remainingTime <= 30f)
        {
            timerText.color = Color.red;
        }
        else if (remainingTime <= 60f)
        {
            timerText.color = Color.yellow;
        }
        else
        {
            timerText.color = Color.white;
        }
    }

    /// <summary>
    /// ステージ表示を更新
    /// </summary>
    private void UpdateStageDisplay()
    {
        if (stageText == null) return;

        int currentStage = enemySpawner.GetCurrentStageIndex() + 1;  // 1始まりに変換
        int totalStages = enemySpawner.GetTotalStageCount();

        // 全ステージクリア後は最大ステージ数で固定
        currentStage = Mathf.Min(currentStage, totalStages);

        stageText.text = $"Wave {currentStage}/{totalStages}";
    }

    /// <summary>
    /// 配置パターン名表示を更新
    /// </summary>
    private void UpdateFormationDisplay()
    {
        if (formationText == null) return;

        string formationName = enemySpawner.GetCurrentFormationName();

        if (string.IsNullOrEmpty(formationName))
        {
            formationText.text = "";
        }
        else
        {
            formationText.text = $"Formation: {formationName}";
        }
    }
}
