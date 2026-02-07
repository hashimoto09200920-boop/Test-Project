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
        }

        /// <summary>
        /// カードをセットアップ
        /// </summary>
        public void SetupCard(SkillDefinition skill, System.Action<SkillDefinition> onSelectedCallback)
        {
            currentSkill = skill;
            onSelected = onSelectedCallback;

            if (skill == null)
            {
                Debug.LogError($"[SkillCardUI] SetupCard called with null skill on {gameObject.name}");
                return;
            }

            Debug.Log($"[SkillCardUI] Setting up {gameObject.name} with skill: {skill.skillName}");

            // テキストを設定
            if (skillNameText != null)
            {
                skillNameText.text = skill.skillName;
                Debug.Log($"[SkillCardUI] Set skillNameText to: {skill.skillName}");
            }
            else
            {
                Debug.LogError($"[SkillCardUI] skillNameText is null on {gameObject.name}");
            }

            if (descriptionText != null)
            {
                descriptionText.text = skill.description;
            }
            else
            {
                Debug.LogError($"[SkillCardUI] descriptionText is null on {gameObject.name}");
            }

            if (effectValueText != null)
            {
                string valueText = skill.isMultiplier
                    ? $"x{skill.effectValue:F2}"
                    : $"+{skill.effectValue:F1}";
                effectValueText.text = valueText;
            }
            else
            {
                Debug.LogError($"[SkillCardUI] effectValueText is null on {gameObject.name}");
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
