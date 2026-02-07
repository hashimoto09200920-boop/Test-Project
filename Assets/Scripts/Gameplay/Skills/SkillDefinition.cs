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

        [Header("Effect Values")]
        [Tooltip("効果量（加算値または乗算値）\n例：白線最大値+5なら5、回復量x1.5なら1.5")]
        public float effectValue = 1.0f;

        [Tooltip("true = 乗算（現在値 × effectValue）\nfalse = 加算（現在値 + effectValue）")]
        public bool isMultiplier = false;

        [Header("Visual (Optional)")]
        [Tooltip("スキルアイコン（スキル選択画面で表示）")]
        public Sprite icon;

        [Tooltip("スキルのレアリティ色（未使用の場合はデフォルト）")]
        public Color rarityColor = Color.white;

        /// <summary>
        /// スキルの表示用テキストを取得
        /// </summary>
        public string GetDisplayText()
        {
            string valueText = isMultiplier
                ? $"x{effectValue:F2}"
                : $"+{effectValue:F1}";

            return $"{skillName}\n{description}\n{valueText}";
        }
    }
}
