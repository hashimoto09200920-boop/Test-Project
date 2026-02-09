using UnityEngine;

namespace Game.Skills
{
    /// <summary>
    /// スキルの定義（ScriptableObject）
    /// Inspectorで効果量を調整可能
    /// </summary>
    [CreateAssetMenu(fileName = "Skill_", menuName = "Game/Skills/Skill Definition", order = 1)]
    public class SkillDefinition : ScriptableObject
    {
        [Header("Basic Info")]
        [Tooltip("スキルの表示名")]
        public string skillName = "New Skill";

        [Tooltip("スキルの説明文")]
        [TextArea(2, 4)]
        public string description = "Skill description";

        [Tooltip("スキルのカテゴリ")]
        public SkillCategory category = SkillCategory.CategoryA;

        [Tooltip("スキルの効果タイプ")]
        public SkillEffectType effectType = SkillEffectType.LeftMaxCostUp;

        [Header("Acquisition Limit")]
        [Tooltip("このスキルを取得できる最大回数（0 = 無制限）")]
        public int maxAcquisitionCount = 0;

        [Header("Effect Values")]
        [Tooltip("変動値を使用するか（最小値～最大値のランダム）")]
        public bool useRandomRange = false;

        [Tooltip("効果量（加算値または乗算値）\n例：白線最大値+5なら5、回復量x1.5なら1.5\n※useRandomRange=trueの場合は無視される")]
        public float effectValue = 1.0f;

        [Tooltip("効果量の最小値（useRandomRange=trueの時のみ有効）")]
        public float effectValueMin = 1.0f;

        [Tooltip("効果量の最大値（useRandomRange=trueの時のみ有効）")]
        public float effectValueMax = 5.0f;

        [Tooltip("true = 乗算（現在値 × effectValue）\nfalse = 加算（現在値 + effectValue）")]
        public bool isMultiplier = false;

        [Tooltip("持続時間（秒）\n※時限効果を持つスキルのみ使用（例：シールド破壊後ダメージブースト）")]
        public float duration = 0f;

        [Header("Spawn Rate")]
        [Tooltip("Stage1のスキルカード出現率（重み）\n全スキル合計で100%になるように設定")]
        [Range(0f, 100f)]
        public float spawnWeight = 4.76f; // デフォルト: 100 / 21 ≈ 4.76 (Stage1用)

        [Tooltip("Stage2のスキルカード出現率（重み）\n全スキル合計で100%になるように設定")]
        [Range(0f, 100f)]
        public float spawnWeightStage2 = 4.76f; // デフォルト: 100 / 21 ≈ 4.76 (Stage2用)

        [Header("Visual (Optional)")]
        [Tooltip("スキルアイコン（スキル選択画面で表示）")]
        public Sprite icon;

        [Tooltip("スキルのレアリティ色（未使用の場合はデフォルト）")]
        public Color rarityColor = Color.white;

        /// <summary>
        /// 実際に適用する効果値を取得（ランダム範囲を考慮）
        /// </summary>
        public float GetRandomizedEffectValue()
        {
            if (useRandomRange)
            {
                return Random.Range(effectValueMin, effectValueMax);
            }
            return effectValue;
        }

        /// <summary>
        /// スキルの表示用テキストを取得
        /// </summary>
        public string GetDisplayText()
        {
            string valueText;
            if (useRandomRange)
            {
                // 範囲表示
                valueText = isMultiplier
                    ? $"x{effectValueMin:F2}~{effectValueMax:F2}"
                    : $"+{effectValueMin:F1}~{effectValueMax:F1}";
            }
            else
            {
                // 固定値表示
                valueText = isMultiplier
                    ? $"x{effectValue:F2}"
                    : $"+{effectValue:F1}";
            }

            return $"{skillName}\n{description}\n{valueText}";
        }
    }
}
