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
            if (skillNameText != null && skillNameText.font != null)
            {
                skillNameText.fontSharedMaterial = skillNameText.font.material;
            }
            if (descriptionText != null && descriptionText.font != null)
            {
                descriptionText.fontSharedMaterial = descriptionText.font.material;
            }
            if (effectValueText != null && effectValueText.font != null)
            {
                effectValueText.fontSharedMaterial = effectValueText.font.material;
            }
        }

        /// <summary>
        /// カードをセットアップ
        /// </summary>
        public void SetupCard(SkillDefinition skill, System.Action<SkillDefinition> onSelectedCallback)
        {
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
