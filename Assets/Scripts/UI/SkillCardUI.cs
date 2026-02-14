using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Game.Skills;

namespace Game.UI
{
    /// <summary>
    /// スキル選択カード（1枚分）
    /// </summary>
    public class SkillCardUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Button button;
        [SerializeField] private TMP_Text skillNameText;
        [SerializeField] private TMP_Text descriptionText;
        [SerializeField] private TMP_Text effectValueText;
        [SerializeField] private Image iconImage;
        [SerializeField] private Image backgroundImage;

        private SkillDefinition currentSkill;
        private System.Action<SkillDefinition> onSelected;

        private void Awake()
        {
            if (button != null)
            {
                button.onClick.AddListener(OnClick);
            }

            // Font AssetのデフォルトMaterialを強制的に使用（Material Preset問題の回避）
            Debug.Log($"[SkillCardUI] Awake - skillNameText: {(skillNameText != null ? "OK" : "NULL")}, font: {(skillNameText != null && skillNameText.font != null ? skillNameText.font.name : "NULL")}, material: {(skillNameText != null && skillNameText.font != null && skillNameText.font.material != null ? skillNameText.font.material.name : "NULL")}");

            if (skillNameText != null && skillNameText.font != null)
            {
                var beforeMaterial = skillNameText.fontSharedMaterial;
                skillNameText.fontSharedMaterial = skillNameText.font.material;
                Debug.Log($"[SkillCardUI] skillNameText.fontSharedMaterial changed: {(beforeMaterial != null ? beforeMaterial.name : "NULL")} -> {(skillNameText.fontSharedMaterial != null ? skillNameText.fontSharedMaterial.name : "NULL")}");
            }
            if (descriptionText != null && descriptionText.font != null)
            {
                var beforeMaterial = descriptionText.fontSharedMaterial;
                descriptionText.fontSharedMaterial = descriptionText.font.material;
                Debug.Log($"[SkillCardUI] descriptionText.fontSharedMaterial changed: {(beforeMaterial != null ? beforeMaterial.name : "NULL")} -> {(descriptionText.fontSharedMaterial != null ? descriptionText.fontSharedMaterial.name : "NULL")}");
            }
            if (effectValueText != null && effectValueText.font != null)
            {
                var beforeMaterial = effectValueText.fontSharedMaterial;
                effectValueText.fontSharedMaterial = effectValueText.font.material;
                Debug.Log($"[SkillCardUI] effectValueText.fontSharedMaterial changed: {(beforeMaterial != null ? beforeMaterial.name : "NULL")} -> {(effectValueText.fontSharedMaterial != null ? effectValueText.fontSharedMaterial.name : "NULL")}");
            }
        }

        /// <summary>
        /// カードをセットアップ
        /// </summary>
        public void SetupCard(SkillDefinition skill, System.Action<SkillDefinition> onSelectedCallback)
        {
            Debug.Log($"[SkillCardUI] SetupCard called - skill: {(skill != null ? skill.skillName : "NULL")}");
            Debug.Log($"[SkillCardUI] SetupCard - skillNameText: {(skillNameText != null ? "OK" : "NULL")}, font: {(skillNameText != null && skillNameText.font != null ? skillNameText.font.name : "NULL")}, fontSharedMaterial: {(skillNameText != null && skillNameText.fontSharedMaterial != null ? skillNameText.fontSharedMaterial.name : "NULL")}");

            currentSkill = skill;
            onSelected = onSelectedCallback;

            if (skill == null) return;

            // テキストを設定
            if (skillNameText != null)
            {
                skillNameText.text = skill.skillName;
            }

            if (descriptionText != null)
            {
                descriptionText.text = skill.description;
            }

            if (effectValueText != null)
            {
                string valueText;
                if (skill.useRandomRange)
                {
                    // 範囲表示
                    valueText = skill.isMultiplier
                        ? $"x{skill.effectValueMin:F2}~{skill.effectValueMax:F2}"
                        : $"+{skill.effectValueMin:F1}~{skill.effectValueMax:F1}";
                }
                else
                {
                    // 固定値表示
                    valueText = skill.isMultiplier
                        ? $"x{skill.effectValue:F2}"
                        : $"+{skill.effectValue:F1}";
                }
                effectValueText.text = valueText;
            }

            // アイコンを設定
            if (iconImage != null && skill.icon != null)
            {
                iconImage.sprite = skill.icon;
                iconImage.enabled = true;
            }
            else if (iconImage != null)
            {
                iconImage.enabled = false;
            }

            // 背景色を設定
            if (backgroundImage != null)
            {
                backgroundImage.color = skill.rarityColor;
            }
        }

        /// <summary>
        /// 表示フォントを差し替える（日本語フォント用）。SkillSelectionUI から呼ばれる。
        /// </summary>
        public void SetFont(TMP_FontAsset font)
        {
            if (font == null) return;
            if (skillNameText != null)
            {
                skillNameText.font = font;
                skillNameText.fontSharedMaterial = font.material;
            }
            if (descriptionText != null)
            {
                descriptionText.font = font;
                descriptionText.fontSharedMaterial = font.material;
            }
            if (effectValueText != null)
            {
                effectValueText.font = font;
                effectValueText.fontSharedMaterial = font.material;
            }
        }

        /// <summary>
        /// クリック時の処理
        /// </summary>
        private void OnClick()
        {
            if (currentSkill != null)
            {
                onSelected?.Invoke(currentSkill);
            }
        }
    }
}
