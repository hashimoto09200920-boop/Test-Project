using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Game.Shop;

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

    // ランタイムで参照（HideInInspector）
    [HideInInspector] public Image  cardBackground;
    [HideInInspector] public Button selectButton;

    private void Awake()
    {
        ReconnectReferences();
        FixFontMaterial(drinkNameText);
        FixFontMaterial(priceText);
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
        }
    }

    /// <summary>選択状態の背景色を切り替える</summary>
    public void SetHighlight(bool selected)
    {
        if (cardBackground == null) return;
        cardBackground.color = selected
            ? new Color(0.20f, 0.35f, 0.50f, 1.00f)
            : new Color(0.10f, 0.10f, 0.15f, 0.95f);
    }
}
