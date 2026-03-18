using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Game.Gems;
using Game.Skills;
using Game.Progress;

/// <summary>
/// GemRewardUI で表示する1枚のジェムカードUI
/// Phase1（選択）・Phase2（結果）両方で使用する
/// </summary>
public class GemRewardCardUI : MonoBehaviour
{
    public enum CardState { Normal, Selected, Dimmed, ResultSelected, ResultUnselected }

    [Header("References")]
    [SerializeField] private Image cardBgImage;
    [SerializeField] private TextMeshProUGUI gemNameText;
    [SerializeField] private Transform skillContainer;

    [Header("Skill Row Settings")]
    [SerializeField] private float skillIconSize = 48f;
    [SerializeField] private float skillRowHeight = 60f;
    [SerializeField] private float skillFontSize = 22f;
    [SerializeField] private Color skillNameColor = Color.white;

    [Header("State Colors")]
    [SerializeField] private Color normalBgColor            = new Color(0.15f, 0.15f, 0.25f, 1f);
    [SerializeField] private Color selectedBgColor          = new Color(0.25f, 0.38f, 0.55f, 1f);
    [SerializeField] private Color dimmedBgColor            = new Color(0.07f, 0.07f, 0.12f, 1f);
    [SerializeField] private Color resultSelectedBgColor    = new Color(0.25f, 0.38f, 0.55f, 1f);
    [SerializeField] private Color resultUnselectedBgColor  = new Color(0.10f, 0.10f, 0.15f, 1f);

    private CanvasGroup canvasGroup;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    /// <summary>
    /// ジェムデータをカードに反映する
    /// </summary>
    public void Setup(GemInstance gem, GemDefinition def, SkillDefinition bonus1, SkillDefinition bonus2)
    {
        // 既存スキル行を削除
        if (skillContainer != null)
            foreach (Transform child in skillContainer)
                Destroy(child.gameObject);

        if (gemNameText != null)
            gemNameText.text = def != null ? def.gemName : "";

        if (def == null || skillContainer == null) return;

        if (def.baseSkill != null) CreateSkillRow(def.baseSkill);
        if (bonus1 != null)        CreateSkillRow(bonus1);
        if (bonus2 != null)        CreateSkillRow(bonus2);
    }

    private void CreateSkillRow(SkillDefinition skill)
    {
        var row = new GameObject("SkillRow");
        row.transform.SetParent(skillContainer, false);

        var hlg = row.AddComponent<HorizontalLayoutGroup>();
        hlg.childAlignment       = TextAnchor.MiddleLeft;
        hlg.childControlWidth    = false;
        hlg.childControlHeight   = false;
        hlg.childForceExpandWidth  = false;
        hlg.childForceExpandHeight = false;
        hlg.spacing = 8f;

        var le = row.AddComponent<LayoutElement>();
        le.preferredHeight = skillRowHeight;
        le.flexibleWidth   = 1f;

        // アイコン
        var iconObj  = new GameObject("Icon");
        iconObj.transform.SetParent(row.transform, false);
        var iconRect = iconObj.AddComponent<RectTransform>();
        iconRect.sizeDelta = new Vector2(skillIconSize, skillIconSize);
        var iconImg = iconObj.AddComponent<Image>();
        iconImg.preserveAspect = true;
        if (skill.icon != null) iconImg.sprite = skill.icon;

        // スキル名
        var nameObj  = new GameObject("SkillName");
        nameObj.transform.SetParent(row.transform, false);
        var nameRect = nameObj.AddComponent<RectTransform>();
        nameRect.sizeDelta = new Vector2(220f, skillRowHeight);
        var tmp = nameObj.AddComponent<TextMeshProUGUI>();
        tmp.text               = skill.skillName;
        tmp.fontSize           = skillFontSize;
        tmp.color              = skillNameColor;
        tmp.alignment          = TextAlignmentOptions.MidlineLeft;
        tmp.enableWordWrapping = false;
    }

    public void SetState(CardState state)
    {
        if (cardBgImage != null)
        {
            cardBgImage.color = state switch
            {
                CardState.Normal            => normalBgColor,
                CardState.Selected          => selectedBgColor,
                CardState.Dimmed            => dimmedBgColor,
                CardState.ResultSelected    => resultSelectedBgColor,
                CardState.ResultUnselected  => resultUnselectedBgColor,
                _                           => normalBgColor,
            };
        }

        if (canvasGroup != null)
            canvasGroup.alpha = (state == CardState.Dimmed || state == CardState.ResultUnselected) ? 0.45f : 1f;
    }

    public void SetAlpha(float alpha)
    {
        if (canvasGroup != null) canvasGroup.alpha = alpha;
    }
}
