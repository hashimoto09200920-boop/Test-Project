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

    [Header("⑤ フレーバーコンテナ サイズ・位置")]
    [Tooltip("FlavorTextContainerのサイズ（幅×高さ）\nContextMenu「Apply Flavor Container Size」で反映")]
    [SerializeField] private Vector2 flavorContainerSize = new Vector2(204f, 240f);
    [Tooltip("カード左上を基点とした位置（x=左端からの距離, y=上端からの距離）")]
    [SerializeField] private Vector2 flavorContainerOffset = new Vector2(200f, 56f);

    [Header("⑥ 選択ハイライト（パルス）")]
    [Tooltip("パルスの暗い側の色")]
    public Color selectedHighlightColor = new Color(0.20f, 0.35f, 0.50f, 1.00f);
    [Tooltip("パルスの明るい側の色")]
    public Color selectedPulseColor = new Color(0.45f, 0.65f, 0.85f, 1.00f);
    [Tooltip("パルス速度（Hz）。2=0.5秒で1往復")]
    public float selectedPulseSpeed = 2f;

    [Header("⑦ 購入済み表示（カードは残すが選択不可にする）")]
    [Tooltip("購入済み時にカード全体へ被せる暗いオーバーレイ。ContextMenu「Setup Purchased Overlay」で自動生成")]
    public Image purchasedOverlayImage;
    [Tooltip("購入済み時に表示するラベル（例:「購入済み」）")]
    public TextMeshProUGUI purchasedLabelText;

    // ランタイムで参照（HideInInspector）
    [HideInInspector] public Image  cardBackground;
    [HideInInspector] public Button selectButton;

    private Color _normalColor;
    private Coroutine _pulseCoroutine;

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
        if (drinkNameText != null) drinkNameText.text = drink.GetLocalizedName();
        if (priceText      != null) priceText.text     = $"{drink.price}";
        if (drinkIconImage != null)
        {
            drinkIconImage.sprite = drink.icon;
            drinkIconImage.enabled = drink.icon != null;
            var iconRect = drinkIconImage.GetComponent<RectTransform>();
            if (iconRect != null)
                iconRect.sizeDelta = drink.iconDisplaySize != Vector2.zero ? drink.iconDisplaySize : new Vector2(160f, 160f);
        }
        if (flavorText != null) flavorText.text = drink.GetLocalizedDescription();

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
            nameTMP.text = skill.GetLocalizedName();
    }

    /// <summary>選択状態の背景パルスを切り替える</summary>
    public void SetHighlight(bool selected)
    {
        if (_pulseCoroutine != null)
        {
            StopCoroutine(_pulseCoroutine);
            _pulseCoroutine = null;
        }

        var target = cardBgImage != null ? cardBgImage : cardBackground;
        if (target == null) return;

        if (selected)
            _pulseCoroutine = StartCoroutine(PulseCoroutine(target));
        else
            target.color = _normalColor;
    }

    private System.Collections.IEnumerator PulseCoroutine(Image target)
    {
        while (true)
        {
            float t = (Mathf.Sin(Time.unscaledTime * selectedPulseSpeed * Mathf.PI * 2f) + 1f) / 2f;
            target.color = Color.Lerp(selectedHighlightColor, selectedPulseColor, t);
            yield return null;
        }
    }

    /// <summary>購入済み状態の見た目切り替え。カード自体は残したまま選択・購入できなくする</summary>
    public void SetPurchased(bool purchased)
    {
        if (purchased) SetHighlight(false); // 購入済みになった瞬間、選択パルスは止める

        if (selectButton != null) selectButton.interactable = !purchased;
        if (purchasedOverlayImage != null) purchasedOverlayImage.gameObject.SetActive(purchased);
        if (purchasedLabelText != null)
        {
            purchasedLabelText.gameObject.SetActive(purchased);
            if (purchased) purchasedLabelText.text = Game.Localization.LocalizationManager.GetStatic("drink.purchasedLabel", "購入済み");
        }
    }

#if UNITY_EDITOR
    [ContextMenu("Apply Flavor Container Size")]
    private void ApplyFlavorContainerSize()
    {
        var contTrans = transform.Find("FlavorTextContainer");
        if (contTrans == null) { Debug.LogWarning("[DrinkCardUI] FlavorTextContainer が見つかりません。"); return; }
        var rt = contTrans.GetComponent<RectTransform>();
        if (rt == null) return;

        rt.anchorMin        = new Vector2(0f, 1f);
        rt.anchorMax        = new Vector2(0f, 1f);
        rt.pivot            = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(flavorContainerOffset.x, -flavorContainerOffset.y);
        rt.sizeDelta        = flavorContainerSize;

        UnityEditor.EditorUtility.SetDirty(gameObject);
        Debug.Log($"[DrinkCardUI] FlavorContainer サイズ適用: {flavorContainerSize}, 位置: {flavorContainerOffset}");
    }

    // ★追加：カード全面を覆う暗いオーバーレイ＋中央の「購入済み」ラベルを自動生成する
    [ContextMenu("Setup Purchased Overlay (購入済み表示を自動生成)")]
    private void SetupPurchasedOverlay()
    {
        Transform existingOverlay = transform.Find("PurchasedOverlay");
        if (existingOverlay != null) DestroyImmediate(existingOverlay.gameObject);

        GameObject overlayObj = new GameObject("PurchasedOverlay", typeof(RectTransform), typeof(Image));
        overlayObj.transform.SetParent(transform, false);
        var overlayRect = overlayObj.GetComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.pivot = new Vector2(0.5f, 0.5f);
        overlayRect.sizeDelta = Vector2.zero;
        overlayRect.anchoredPosition = Vector2.zero;
        // カードのCardBg等より手前・かつ全ての内容の上に来るよう最後の子として追加する
        overlayObj.transform.SetAsLastSibling();

        var overlayImage = overlayObj.GetComponent<Image>();
        overlayImage.color = new Color(0f, 0f, 0f, 0.65f);
        overlayImage.raycastTarget = true; // 下のUIへのクリックも遮断する

        GameObject labelObj = new GameObject("PurchasedLabel", typeof(RectTransform));
        labelObj.transform.SetParent(overlayObj.transform, false);
        var labelRect = labelObj.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.pivot = new Vector2(0.5f, 0.5f);
        labelRect.sizeDelta = Vector2.zero;
        labelRect.anchoredPosition = Vector2.zero;

        var labelText = labelObj.AddComponent<TextMeshProUGUI>();
        labelText.text = "購入済み";
        labelText.alignment = TextAlignmentOptions.Center;
        labelText.fontSize = 32f;
        labelText.color = Color.white;
        if (drinkNameText != null && drinkNameText.font != null)
        {
            labelText.font = drinkNameText.font;
            labelText.fontSharedMaterial = drinkNameText.font.material;
        }

        purchasedOverlayImage = overlayImage;
        purchasedLabelText = labelText;
        overlayObj.SetActive(false);

        UnityEditor.EditorUtility.SetDirty(gameObject);
        Debug.Log("[DrinkCardUI] SetupPurchasedOverlay: PurchasedOverlay/PurchasedLabelを生成し、対応欄にアサインしました。", this);
    }
#endif
}
