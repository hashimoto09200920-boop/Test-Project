using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Game.Shop;
using Game.Skills;

/// <summary>
/// ドリンクカードのUI要素参照を管理するコンポーネント。
/// ShopPanel下のDrinkCardTemplateにアタッチし、Play前にInspectorで調整する。
/// </summary>
[DisallowMultipleComponent]
public class DrinkCardUI : MonoBehaviour
{
    [Header("① ドリンク名・ゴールドアイコン・価格")]
    public TextMeshProUGUI drinkNameText;
    public Image           goldIconImage;
    public TextMeshProUGUI priceText;

    [Header("② ドリンクアイコン（NamePriceRow の下中央）")]
    public Image drinkIconImage;

    [Header("③ フレーバーテキスト（DrinkIcon 右側）")]
    public TextMeshProUGUI flavorText;

    [Header("④ 背景画像（専用オブジェクト）")]
    public Image cardBgImage;

    // ランタイムで参照（HideInInspector）
    [HideInInspector] public Image  cardBackground;
    [HideInInspector] public Button selectButton;

    private Color _normalColor;

    private void Awake()
    {
        ReconnectReferences();
        var colorSource = cardBgImage != null ? cardBgImage : cardBackground;
        _normalColor = colorSource != null ? colorSource.color : new Color(0.10f, 0.10f, 0.15f, 0.95f);
        FixFontMaterial(drinkNameText);
        FixFontMaterial(priceText);
        FixFontMaterial(flavorText);
    }

    private void Start()
    {
        StartCoroutine(EnsureLayoutAndLog());
    }

    private System.Collections.IEnumerator EnsureLayoutAndLog()
    {
        yield return null;
        var cardRT = GetComponent<RectTransform>();
        if (cardRT != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(cardRT);
    }

    private void ReconnectReferences()
    {
        if (cardBackground == null)
            cardBackground = GetComponent<Image>();
        if (selectButton == null)
            selectButton = GetComponent<Button>();
        if (cardBgImage == null)
        {
            var t = transform.Find("CardBg");
            if (t != null) cardBgImage = t.GetComponent<Image>();
        }

        var nameRow = transform.Find("NamePriceRow");
        if (nameRow == null) return;

        if (drinkNameText == null)
        {
            var t = nameRow.Find("DrinkNameText");
            if (t != null) drinkNameText = t.GetComponent<TextMeshProUGUI>();
        }
        if (goldIconImage == null)
        {
            var t = nameRow.Find("GoldIcon");
            if (t != null) goldIconImage = t.GetComponent<Image>();
        }
        if (priceText == null)
        {
            var t = nameRow.Find("PriceText");
            if (t != null) priceText = t.GetComponent<TextMeshProUGUI>();
        }

        var iconTrans = transform.Find("DrinkIcon");
        if (drinkIconImage == null && iconTrans != null)
            drinkIconImage = iconTrans.GetComponent<Image>();

        if (flavorText == null)
        {
            var t = transform.Find("FlavorTextContainer/FlavorText");
            if (t != null) flavorText = t.GetComponent<TextMeshProUGUI>();
        }
    }

    private static void FixFontMaterial(TextMeshProUGUI tmp)
    {
        if (tmp != null && tmp.font != null)
            tmp.fontSharedMaterial = tmp.font.material;
    }

    /// <summary>DrinkDefinitionのデータをUI要素にセットする</summary>
    public void Populate(DrinkDefinition drink)
    {
        if (drinkNameText != null) drinkNameText.text = drink.drinkName;
        if (priceText      != null) priceText.text     = $"{drink.price}G";
        if (drinkIconImage != null)
        {
            drinkIconImage.sprite = drink.icon;
            drinkIconImage.enabled = drink.icon != null;
            var iconRect = drinkIconImage.GetComponent<RectTransform>();
            if (iconRect != null)
                iconRect.sizeDelta = drink.iconDisplaySize != Vector2.zero ? drink.iconDisplaySize : new Vector2(160f, 160f);
        }
        if (flavorText != null) flavorText.text = drink.description;

        var skillsCont = transform.Find("SkillsContainer");
        if (skillsCont != null)
        {
            PopulateSkillRow(skillsCont.Find("SkillRow_1"), drink.targetSkill1);
            PopulateSkillRow(skillsCont.Find("SkillRow_2"), drink.targetSkill2);
            PopulateSkillRow(skillsCont.Find("SkillRow_3"), drink.targetSkill3);
        }
    }

    private static readonly Color CatAColor = new Color(0.7019608f, 0.8980392f, 0.8980392f, 0.8f);
    private static readonly Color CatBColor = new Color(0.7882353f, 0.7019608f, 0.8980392f, 0.8f);
    private static readonly Color CatCColor = new Color(0.8980392f, 0.7882353f, 0.627451f,  0.8f);
    private const float SkillIconSize = 40f;

    private static void PopulateSkillRow(Transform row, SkillDefinition skill)
    {
        if (row == null) return;
        row.gameObject.SetActive(skill != null);
        if (skill == null) return;

        var iconContainer = row.Find("IconContainer");
        var iconImg       = iconContainer?.Find("IconImage")?.GetComponent<Image>();
        var nameTMP       = row.Find("SkillName")?.GetComponent<TextMeshProUGUI>();

        // 行背景色（iconImgの有無に関わらず適用）
        var rowBg = row.GetComponent<Image>();
        if (rowBg != null)
        {
            rowBg.color = skill.category switch
            {
                Game.Skills.SkillCategory.CategoryA => CatAColor,
                Game.Skills.SkillCategory.CategoryB => CatBColor,
                Game.Skills.SkillCategory.CategoryC => CatCColor,
                _ => new Color(0.15f, 0.15f, 0.15f, 0.5f)
            };
        }

        if (iconImg != null)
        {
            iconImg.sprite = skill.icon;
            iconImg.color  = skill.icon != null ? Color.white : new Color(0.3f, 0.3f, 0.3f, 0.5f);
            var iconRect = iconImg.GetComponent<RectTransform>();
            if (iconRect != null)
            {
                iconRect.sizeDelta        = skill.iconDisplaySize != Vector2.zero ? skill.iconDisplaySize : new Vector2(SkillIconSize, SkillIconSize);
                iconRect.anchoredPosition = skill.iconDisplayOffset;
            }
        }

        if (nameTMP != null)
            nameTMP.text = skill.skillName;
    }

    /// <summary>選択状態の背景色を切り替える</summary>
    public void SetHighlight(bool selected)
    {
        var target = cardBgImage != null ? cardBgImage : cardBackground;
        if (target == null) return;
        target.color = selected
            ? new Color(0.20f, 0.35f, 0.50f, 1.00f)
            : _normalColor;
    }
}
