using UnityEngine;
using TMPro;

/// <summary>
/// HP-Based Routine Switching のデバッグ情報を敵の上に表示する
/// </summary>
public class EnemyRoutineDebugText : MonoBehaviour
{
    [Header("Debug Display Settings")]
    [SerializeField] private bool showDebugText = true;
    [SerializeField] private Vector3 offset = new Vector3(0f, 1.0f, 0f);
    [SerializeField] private float fontSize = 3f;
    [SerializeField] private Color textColor = Color.white;

    private TextMeshPro debugText;
    private EnemyMover enemyMover;
    private EnemyShooter enemyShooter;
    private EnemyStats enemyStats;
    private EnemyData enemyData;

    private void Awake()
    {
        // コンポーネント参照取得
        enemyMover = GetComponent<EnemyMover>();
        enemyShooter = GetComponent<EnemyShooter>();
        enemyStats = GetComponent<EnemyStats>();

        // TextMeshPro を作成
        CreateDebugText();
    }

    private void CreateDebugText()
    {
        GameObject textObj = new GameObject("RoutineDebugText");
        textObj.transform.SetParent(transform, false);
        textObj.transform.localPosition = offset;

        debugText = textObj.AddComponent<TextMeshPro>();
        debugText.fontSize = fontSize;
        debugText.color = textColor;
        debugText.alignment = TextAlignmentOptions.Center;
        debugText.sortingOrder = 100;

        // 初期状態
        debugText.enabled = showDebugText;
    }

    private void LateUpdate()
    {
        if (!showDebugText || debugText == null)
        {
            if (debugText != null) debugText.enabled = false;
            return;
        }

        debugText.enabled = true;

        // EnemyData を取得
        if (enemyData == null && enemyShooter != null)
        {
            enemyData = enemyShooter.GetEnemyData();
        }

        // デバッグ情報を構築
        string info = BuildDebugInfo();
        debugText.text = info;
    }

    private string BuildDebugInfo()
    {
        if (enemyData == null || !enemyData.useHpBasedRoutineSwitch)
        {
            return "HP-Based: OFF";
        }

        System.Text.StringBuilder sb = new System.Text.StringBuilder();

        // HP%
        if (enemyStats != null)
        {
            float hpPercent = enemyStats.GetHpPercentage();
            sb.AppendLine($"HP: {hpPercent:F0}%");
        }

        // HP閾値
        sb.AppendLine($"Threshold: {enemyData.hpThresholdPercentage:F0}%");

        // Move Routine 情報
        if (enemyMover != null)
        {
            var moveInfo = enemyMover.GetDebugMoveRoutineInfo();
            if (moveInfo.isUsingRoutine)
            {
                string slotName = GetRoutineSlotName(moveInfo.routineType, GetCurrentMoveRoutineSlot());
                sb.AppendLine($"Move: {slotName}");
                sb.AppendLine($"  Type: {moveInfo.routineType}");
                sb.AppendLine($"  Pattern: {moveInfo.currentMoveTypeName}");
            }
            else
            {
                sb.AppendLine("Move: Not Using Routine");
            }
        }

        // Bullet Routine 情報
        if (enemyShooter != null)
        {
            var bulletInfo = enemyShooter.GetDebugRoutineInfo();
            if (bulletInfo.isUsingRoutine)
            {
                string slotName = GetRoutineSlotName(bulletInfo.routineType, GetCurrentBulletRoutineSlot());
                sb.AppendLine($"Bullet: {slotName}");
                sb.AppendLine($"  Type: {bulletInfo.routineType}");
                sb.AppendLine($"  Pattern: {bulletInfo.currentBulletTypeName}");
            }
            else
            {
                sb.AppendLine("Bullet: Not Using Routine");
            }
        }

        return sb.ToString();
    }

    private string GetRoutineSlotName(EnemyData.MoveFiringRoutine.RoutineType? routineType, EnemyData.RoutineSlot slot)
    {
        switch (slot)
        {
            case EnemyData.RoutineSlot.Sequence_01: return "Seq_01";
            case EnemyData.RoutineSlot.Sequence_02: return "Seq_02";
            case EnemyData.RoutineSlot.Probability_01: return "Prob_01";
            case EnemyData.RoutineSlot.Probability_02: return "Prob_02";
            default: return "Unknown";
        }
    }

    private string GetRoutineSlotName(EnemyData.BulletFiringRoutine.RoutineType? routineType, EnemyData.RoutineSlot slot)
    {
        switch (slot)
        {
            case EnemyData.RoutineSlot.Sequence_01: return "Seq_01";
            case EnemyData.RoutineSlot.Sequence_02: return "Seq_02";
            case EnemyData.RoutineSlot.Probability_01: return "Prob_01";
            case EnemyData.RoutineSlot.Probability_02: return "Prob_02";
            default: return "Unknown";
        }
    }

    private EnemyData.RoutineSlot GetCurrentMoveRoutineSlot()
    {
        if (enemyData == null || enemyStats == null) return EnemyData.RoutineSlot.Sequence_01;

        float hpPercent = enemyStats.GetHpPercentage();
        if (hpPercent >= enemyData.hpThresholdPercentage)
        {
            return enemyData.moveRoutineAboveThreshold;
        }
        else
        {
            return enemyData.moveRoutineBelowThreshold;
        }
    }

    private EnemyData.RoutineSlot GetCurrentBulletRoutineSlot()
    {
        if (enemyData == null || enemyStats == null) return EnemyData.RoutineSlot.Sequence_01;

        float hpPercent = enemyStats.GetHpPercentage();
        if (hpPercent >= enemyData.hpThresholdPercentage)
        {
            return enemyData.bulletRoutineAboveThreshold;
        }
        else
        {
            return enemyData.bulletRoutineBelowThreshold;
        }
    }

    /// <summary>
    /// デバッグ表示のON/OFF切り替え
    /// </summary>
    public void SetDebugTextEnabled(bool enabled)
    {
        showDebugText = enabled;
        if (debugText != null)
        {
            debugText.enabled = enabled;
        }
    }
}
