using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Game.Shop;
using Game.Skills;

/// <summary>
/// ショップUI（作り直し版）
/// 絶対に変えない: ShopBgImage, ShopCharacterImage, ShopCounter, Customer, ShopDimPanel, GoldHUD
/// ShopPanel は ContextMenu「Setup Shop Panel (Rebuild)」で GemManagementPanel と同じ構成で生成。
/// </summary>
public class ShopUI : MonoBehaviour
{
    [Header("Panel References（絶対に変更しない）")]
    [SerializeField] private GameObject dimPanel;
    [SerializeField] private Image shopBgImage;
    [SerializeField] private Image shopCharacterImage;
    [SerializeField] private Image shopCounterImage;
    [SerializeField] private Image customerImage;
    [SerializeField] private GameObject shopPanel;

    [Header("Header")]
    [SerializeField] private TextMeshProUGUI drinkCountText;
    [SerializeField] private Button buyButton;
    [SerializeField] private Button closeButton;

    [Header("Drink List")]
    [SerializeField] private Transform drinkListContainer;
    [SerializeField] private DrinkCardUI drinkCardTemplate;

    [Header("Settings")]
    [Min(1)] [SerializeField] private int drinkLimit = 1;

    [Header("Display Settings")]
    [SerializeField] private float cardWidth = 420f;
    [SerializeField] private float cardHeight = 440f;

    [Header("Shop Panel (Rebuild で使用)")]
    [SerializeField] private float shopPanelX = 200f;
    [SerializeField] private float shopPanelWidth = 1360f;
    [SerializeField] private float shopPanelHeight = 900f;

    [Header("Background Animation")]
    [Range(0f, 1f)] [SerializeField] private float bgBrightnessMin = 0.7f;
    [Range(0f, 1f)] [SerializeField] private float bgBrightnessMax = 1.0f;
    [SerializeField] private float bgAnimSpeedMin = 0.2f;
    [SerializeField] private float bgAnimSpeedMax = 0.7f;
    [SerializeField] private float bgSpeedChangeInterval = 3f;

    [Header("Character Animation")]
    [SerializeField] private Sprite[] characterAnimFrames;
    [SerializeField] private float characterAnimFps = 6f;

    [Header("Open/Close Fade")]
    [SerializeField] private float fadeDuration = 0.3f;

    [Header("SE")]
    [SerializeField] private AudioClip buySE;
    [SerializeField] private AudioClip closeSE;
    [SerializeField] private AudioClip insufficientGoldSE;
    [SerializeField] private AudioClip selectSE;

    private readonly List<GameObject> drinkCardObjects = new List<GameObject>();
    private DrinkDefinition selectedDrink;
    private GameObject selectedCardObj;
    private DrinkCardUI selectedCardUI;
    private AudioSource audioSource;
    private Coroutine bgAnimCoroutine;
    private Coroutine characterAnimCoroutine;
    private bool isOpening;
    private bool isClosing;
    private Transform goldHUDOriginalParent;
    private int goldHUDOriginalSiblingIndex;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }

        AutoReconnectReferences();

        if (closeButton != null)
            closeButton.onClick.AddListener(Close);
        if (buyButton != null)
            buyButton.onClick.AddListener(OnBuySelected);

        HideAllPanels();
    }

    private void AutoReconnectReferences()
    {
        var canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;

        if (dimPanel == null) { var t = canvas.transform.Find("ShopDimPanel"); if (t != null) dimPanel = t.gameObject; }
        if (shopBgImage == null) { var t = canvas.transform.Find("ShopBgImage"); if (t != null) shopBgImage = t.GetComponent<Image>(); }
        if (shopCharacterImage == null) { var t = canvas.transform.Find("ShopCharacterImage"); if (t != null) shopCharacterImage = t.GetComponent<Image>(); }
        if (shopCounterImage == null) { var t = canvas.transform.Find("ShopCounter"); if (t != null) shopCounterImage = t.GetComponent<Image>(); }
        if (customerImage == null) { var t = canvas.transform.Find("Customer"); if (t != null) customerImage = t.GetComponent<Image>(); }
        if (shopPanel == null) { var t = canvas.transform.Find("ShopPanel"); if (t != null) shopPanel = t.gameObject; }

        if (shopPanel != null)
        {
            if (drinkCountText == null) { var t = shopPanel.transform.Find("HeaderRow/TitleText"); if (t != null) drinkCountText = t.GetComponent<TextMeshProUGUI>(); }
            if (buyButton == null) { var t = shopPanel.transform.Find("HeaderRow/BuyButton"); if (t != null) buyButton = t.GetComponent<Button>(); }
            if (closeButton == null) { var t = shopPanel.transform.Find("HeaderRow/CloseButton"); if (t != null) closeButton = t.GetComponent<Button>(); }
            if (drinkListContainer == null) { var t = shopPanel.transform.Find("ScrollView/Viewport/Content"); if (t != null) drinkListContainer = t; }
        }
        if (drinkCardTemplate == null) { var t = transform.Find("DrinkCardTemplate"); if (t != null) drinkCardTemplate = t.GetComponent<DrinkCardUI>(); }
    }

    public void Open()
    {
        if (isOpening) return;
        StartCoroutine(OpenCoroutine());
    }

    public void Close()
    {
        if (isClosing) return;
        StartCoroutine(CloseCoroutine());
    }

    private IEnumerator OpenCoroutine()
    {
        isOpening = true;
        yield return StartCoroutine(Fade(0f, 1f));

        if (dimPanel != null) dimPanel.SetActive(true);
        if (shopBgImage != null)
        {
            shopBgImage.gameObject.SetActive(true);
            if (bgAnimCoroutine != null) StopCoroutine(bgAnimCoroutine);
            bgAnimCoroutine = StartCoroutine(AnimateBgBrightness());
        }
        if (shopCharacterImage != null)
        {
            shopCharacterImage.gameObject.SetActive(true);
            if (characterAnimFrames != null && characterAnimFrames.Length > 1)
            {
                if (characterAnimCoroutine != null) StopCoroutine(characterAnimCoroutine);
                characterAnimCoroutine = StartCoroutine(AnimateCharacter());
            }
        }
        if (shopCounterImage != null) shopCounterImage.gameObject.SetActive(true);
        if (customerImage != null) customerImage.gameObject.SetActive(true);
        if (shopPanel != null)
        {
            shopPanel.SetActive(true);
            shopPanel.transform.SetAsLastSibling();
        }

        var canvas = GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            var goldHUD = FindTransformByName(canvas.transform, "GoldHUD");
            if (goldHUD != null)
            {
                goldHUDOriginalParent = goldHUD.parent;
                goldHUDOriginalSiblingIndex = goldHUD.GetSiblingIndex();
                goldHUD.SetParent(canvas.transform, true);
                goldHUD.SetAsLastSibling();
            }
        }

        selectedDrink = null;
        selectedCardObj = null;
        RefreshDrinkCards();
        RefreshDrinkCountDisplay();
        RefreshBuyButtonState();

        yield return StartCoroutine(Fade(1f, 0f));
        isOpening = false;
    }

    private IEnumerator CloseCoroutine()
    {
        isClosing = true;
        PlaySE(closeSE);
        yield return StartCoroutine(Fade(0f, 1f));
        HideAllPanels();

        if (goldHUDOriginalParent != null)
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                var goldHUD = FindTransformByName(canvas.transform, "GoldHUD");
                if (goldHUD != null)
                {
                    goldHUD.SetParent(goldHUDOriginalParent, true);
                    goldHUD.SetSiblingIndex(goldHUDOriginalSiblingIndex);
                }
            }
            goldHUDOriginalParent = null;
        }

        yield return StartCoroutine(Fade(1f, 0f));
        FindObjectOfType<Game.UI.AreaSelectMenu>()?.ResetPanelTransition();
        isClosing = false;
    }

    private void HideAllPanels()
    {
        if (bgAnimCoroutine != null) { StopCoroutine(bgAnimCoroutine); bgAnimCoroutine = null; }
        if (characterAnimCoroutine != null) { StopCoroutine(characterAnimCoroutine); characterAnimCoroutine = null; }
        if (dimPanel != null) dimPanel.SetActive(false);
        if (shopBgImage != null) { shopBgImage.color = Color.white; shopBgImage.gameObject.SetActive(false); }
        if (shopCharacterImage != null) shopCharacterImage.gameObject.SetActive(false);
        if (shopCounterImage != null) shopCounterImage.gameObject.SetActive(false);
        if (customerImage != null) customerImage.gameObject.SetActive(false);
        if (shopPanel != null) shopPanel.SetActive(false);
    }

    private void RefreshDrinkCountDisplay()
    {
        if (drinkCountText == null) return;
        drinkCountText.text = $"ドリンク回数: {DrinkSession.PurchaseCount}/{drinkLimit}";
    }

    private void RefreshBuyButtonState()
    {
        if (buyButton == null) return;
        bool withinLimit = DrinkSession.PurchaseCount < drinkLimit;
        bool hasSelection = selectedDrink != null;
        bool hasGold = GoldManager.Instance != null && selectedDrink != null && GoldManager.Instance.PersistentGold >= selectedDrink.price;
        buyButton.interactable = withinLimit && hasSelection && hasGold;
    }

    /// <summary>Stage 2: テンプレートがあれば Resources.LoadAll で一覧表示。選択・ハイライトも有効。</summary>
    private void RefreshDrinkCards()
    {
        foreach (var obj in drinkCardObjects)
            if (obj != null) Destroy(obj);
        drinkCardObjects.Clear();
        selectedDrink = null;
        selectedCardObj = null;
        selectedCardUI = null;

        if (drinkCardTemplate == null || drinkListContainer == null)
            return;

        // Content の GridLayoutGroup に現在の cardWidth/cardHeight を適用（シーンの Cell Size より優先）
        var grid = drinkListContainer.GetComponent<GridLayoutGroup>();
        if (grid != null)
        {
            grid.cellSize = new Vector2(cardWidth, cardHeight);
        }

        DrinkDefinition[] drinks = Resources.LoadAll<DrinkDefinition>("GameData/Drinks");
        if (drinks == null || drinks.Length == 0)
            return;

        for (int i = 0; i < drinks.Length; i++)
        {
            DrinkDefinition drink = drinks[i];
            GameObject cardObj = Instantiate(drinkCardTemplate.gameObject, drinkListContainer);
            cardObj.SetActive(true);

            DrinkCardUI cardUI = cardObj.GetComponent<DrinkCardUI>();
            if (cardUI != null)
            {
                cardUI.Populate(drink);
                Button btn = cardUI.selectButton != null ? cardUI.selectButton : cardObj.GetComponent<Button>();
                if (btn != null)
                {
                    DrinkDefinition d = drink;
                    GameObject go = cardObj;
                    btn.onClick.AddListener(() => SelectDrink(d, go));
                }
                cardUI.SetHighlight(false);
            }
            drinkCardObjects.Add(cardObj);
        }

        Canvas.ForceUpdateCanvases();
        if (drinkListContainer != null)
        {
            var contentRect = drinkListContainer.GetComponent<RectTransform>();
            if (contentRect != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);
        }
    }

    private void SelectDrink(DrinkDefinition drink, GameObject cardObj)
    {
        selectedDrink = drink;
        selectedCardObj = cardObj;
        selectedCardUI = cardObj != null ? cardObj.GetComponent<DrinkCardUI>() : null;

        for (int i = 0; i < drinkCardObjects.Count; i++)
        {
            var obj = drinkCardObjects[i];
            if (obj == null) continue;
            var ui = obj.GetComponent<DrinkCardUI>();
            if (ui != null)
                ui.SetHighlight(obj == selectedCardObj);
        }

        RefreshBuyButtonState();
        PlaySE(GetSelectSE());
    }

    /// <summary>選択時SE。未設定なら AreaSelectMenu のボタンSE を流用。</summary>
    private AudioClip GetSelectSE()
    {
        if (selectSE != null) return selectSE;
        var menu = FindObjectOfType<Game.UI.AreaSelectMenu>();
        if (menu != null) return menu.buttonClickSE;
        return null;
    }
    private void OnBuySelected()
    {
        if (selectedDrink == null) return;
        if (GoldManager.Instance == null) return;

        if (DrinkSession.PurchaseCount >= drinkLimit)
        {
            PlaySE(GetInsufficientGoldSE());
            return;
        }
        if (GoldManager.Instance.PersistentGold < selectedDrink.price)
        {
            PlaySE(GetInsufficientGoldSE());
            return;
        }

        GoldManager.Instance.SpendPersistentGold(selectedDrink.price);

        var candidates = new List<SkillDefinition>();
        if (selectedDrink.targetSkill1 != null) candidates.Add(selectedDrink.targetSkill1);
        if (selectedDrink.targetSkill2 != null) candidates.Add(selectedDrink.targetSkill2);
        if (selectedDrink.targetSkill3 != null) candidates.Add(selectedDrink.targetSkill3);

        int count = Mathf.Min(selectedDrink.selectionCount, candidates.Count);
        for (int i = 0; i < count; i++)
        {
            int idx = Random.Range(0, candidates.Count);
            var skill = candidates[idx];
            candidates.RemoveAt(idx);
            DrinkSession.AddBoost(skill.name, selectedDrink.levelUpCount);
        }

        DrinkSession.IncrementPurchaseCount();
        PlaySE(GetBuySE());

        if (selectedCardUI != null)
            selectedCardUI.SetHighlight(false);
        selectedDrink = null;
        selectedCardObj = null;
        selectedCardUI = null;

        RefreshDrinkCountDisplay();
        RefreshBuyButtonState();
    }

    private AudioClip GetBuySE()
    {
        if (buySE != null) return buySE;
        return GetSelectSE();
    }

    private AudioClip GetInsufficientGoldSE()
    {
        if (insufficientGoldSE != null) return insufficientGoldSE;
        return GetSelectSE();
    }

    private IEnumerator AnimateBgBrightness()
    {
        float t = 0f, currentSpeed = Random.Range(bgAnimSpeedMin, bgAnimSpeedMax), speedTimer = 0f;
        while (true)
        {
            float dt = Time.unscaledDeltaTime;
            speedTimer += dt;
            if (speedTimer >= bgSpeedChangeInterval) { speedTimer = 0f; }
            t += dt * currentSpeed;
            float brightness = Mathf.Lerp(bgBrightnessMin, bgBrightnessMax, (Mathf.Sin(t * Mathf.PI * 2f) + 1f) * 0.5f);
            if (shopBgImage != null) shopBgImage.color = new Color(brightness, brightness, brightness, 1f);
            yield return null;
        }
    }

    private IEnumerator AnimateCharacter()
    {
        int frameIndex = 0;
        float interval = 1f / Mathf.Max(characterAnimFps, 0.1f);
        while (true)
        {
            if (shopCharacterImage != null && characterAnimFrames != null && characterAnimFrames.Length > 0)
                shopCharacterImage.sprite = characterAnimFrames[frameIndex % characterAnimFrames.Length];
            frameIndex++;
            yield return new WaitForSecondsRealtime(interval);
        }
    }

    private static Transform FindTransformByName(Transform root, string name)
    {
        if (root.name == name) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            var result = FindTransformByName(root.GetChild(i), name);
            if (result != null) return result;
        }
        return null;
    }

    private void PlaySE(AudioClip clip)
    {
        if (audioSource == null || clip == null) return;
        float vol = SoundSettingsManager.Instance != null ? SoundSettingsManager.Instance.SEVolume : 1f;
        audioSource.PlayOneShot(clip, vol);
    }

    private IEnumerator Fade(float from, float to)
    {
        if (fadeDuration <= 0f) yield break;
        var fadeObj = new GameObject("ShopFade");
        var fadeCanvas = fadeObj.AddComponent<Canvas>();
        fadeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        fadeCanvas.sortingOrder = 9999;
        fadeObj.AddComponent<CanvasScaler>();
        fadeObj.AddComponent<GraphicRaycaster>();
        var imgObj = new GameObject("FadeImage");
        imgObj.transform.SetParent(fadeObj.transform, false);
        var img = imgObj.AddComponent<Image>();
        img.color = new Color(0f, 0f, 0f, from);
        var rt = imgObj.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            img.color = new Color(0f, 0f, 0f, Mathf.Lerp(from, to, elapsed / fadeDuration));
            yield return null;
        }
        Destroy(fadeObj);
    }

#if UNITY_EDITOR
    /// <summary>GemManagementPanel と同じ構成で ShopPanel を一から作成。既存の ShopPanel は削除する。</summary>
    [ContextMenu("Setup Shop Panel (Rebuild)")]
    private void SetupShopPanelFromScratch()
    {
        var canvas = GetComponentInParent<Canvas>();
        if (canvas == null) { Debug.LogError("[ShopUI] Canvas が見つかりません。"); return; }

        var old = canvas.transform.Find("ShopPanel");
        if (old != null) { DestroyImmediate(old.gameObject); Debug.Log("[ShopUI] 既存 ShopPanel を削除しました。"); }

        CreateShopPanel(canvas.transform);

        var so = new UnityEditor.SerializedObject(this);
        so.Update();
        so.FindProperty("shopPanel").objectReferenceValue = shopPanel;
        so.FindProperty("drinkCountText").objectReferenceValue = drinkCountText;
        so.FindProperty("buyButton").objectReferenceValue = buyButton;
        so.FindProperty("closeButton").objectReferenceValue = closeButton;
        so.FindProperty("drinkListContainer").objectReferenceValue = drinkListContainer;
        so.ApplyModifiedProperties();
        UnityEditor.EditorUtility.SetDirty(gameObject);
        Debug.Log("[ShopUI] Setup Shop Panel (Rebuild) 完了。ShopButton から Open() を呼んで確認してください。");
    }

    /// <summary>DrinkCardUI 用テンプレートを ShopUI 直下に作成（Gem の GemItemTemplate と同様）。</summary>
    [ContextMenu("Setup Drink Card Template")]
    private void SetupDrinkCardTemplate()
    {
        // シーンに古いデフォルト 280 が保存されていれば 420 に移行
        var soSelf = new UnityEditor.SerializedObject(this);
        var propWidth = soSelf.FindProperty("cardWidth");
        if (propWidth != null && Mathf.Approximately(propWidth.floatValue, 280f))
        {
            propWidth.floatValue = 420f;
            soSelf.ApplyModifiedProperties();
        }

        var existing = transform.Find("DrinkCardTemplate");
        if (existing != null)
        {
            DestroyImmediate(existing.gameObject);
            Debug.Log("[ShopUI] 既存 DrinkCardTemplate を削除しました。");
        }

        GameObject root = new GameObject("DrinkCardTemplate");
        root.transform.SetParent(transform, false);

        var rootRect = root.AddComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0f, 1f);
        rootRect.anchorMax = new Vector2(1f, 1f);
        rootRect.pivot = new Vector2(0.5f, 1f);
        rootRect.sizeDelta = new Vector2(cardWidth > 0 ? cardWidth : 420f, cardHeight > 0 ? cardHeight : 100f);

        root.AddComponent<Image>().color = new Color(0.10f, 0.10f, 0.15f, 0.95f);
        root.AddComponent<Button>();
        var cardUI = root.AddComponent<DrinkCardUI>();
        var rootLE = root.AddComponent<LayoutElement>();
        rootLE.preferredWidth = cardWidth > 0 ? cardWidth : 420f;
        rootLE.preferredHeight = cardHeight > 0 ? cardHeight : 100f;

        // NamePriceRow（レイアウトなし・RectTransform で直接配置）
        var nameRow = new GameObject("NamePriceRow");
        nameRow.transform.SetParent(root.transform, false);
        var nameRowRect = nameRow.AddComponent<RectTransform>();
        nameRowRect.anchorMin = new Vector2(0f, 1f);
        nameRowRect.anchorMax = new Vector2(0f, 1f);
        nameRowRect.pivot = new Vector2(0f, 1f);
        nameRowRect.anchoredPosition = new Vector2(16f, -12f);
        nameRowRect.sizeDelta = new Vector2((cardWidth > 0 ? cardWidth : 420f) - 32f, 44f);

        // DrinkNameText：行の左上に固定（位置変更なし）
        var nameTextObj = new GameObject("DrinkNameText");
        nameTextObj.transform.SetParent(nameRow.transform, false);
        var nameRect = nameTextObj.AddComponent<RectTransform>();
        nameRect.anchorMin = new Vector2(0f, 1f);
        nameRect.anchorMax = new Vector2(0f, 1f);
        nameRect.pivot = new Vector2(0f, 1f);
        nameRect.anchoredPosition = Vector2.zero;
        nameRect.sizeDelta = new Vector2(200f, 44f);
        var nameTMP = nameTextObj.AddComponent<TextMeshProUGUI>();
        nameTMP.text = "ドリンク名";
        nameTMP.fontSize = 22f;
        nameTMP.alignment = TextAlignmentOptions.MidlineLeft;
        nameTMP.color = Color.white;
        nameTMP.enableWordWrapping = false;

        // PriceText：行の右上に固定（位置変更なし）
        var priceTextObj = new GameObject("PriceText");
        priceTextObj.transform.SetParent(nameRow.transform, false);
        var priceRect = priceTextObj.AddComponent<RectTransform>();
        priceRect.anchorMin = new Vector2(1f, 1f);
        priceRect.anchorMax = new Vector2(1f, 1f);
        priceRect.pivot = new Vector2(1f, 1f);
        priceRect.anchoredPosition = new Vector2(-12f, 0f);
        priceRect.sizeDelta = new Vector2(100f, 44f);
        var priceTMP = priceTextObj.AddComponent<TextMeshProUGUI>();
        priceTMP.text = "0G";
        priceTMP.fontSize = 22f;
        priceTMP.alignment = TextAlignmentOptions.MidlineRight;
        priceTMP.color = Color.white;

#if UNITY_EDITOR
        var drinkCardFont = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/NotoSerifCJKjp-Regular SDF.asset");
        if (drinkCardFont != null)
        {
            nameTMP.font = drinkCardFont;
            priceTMP.font = drinkCardFont;
        }
#endif

        // GoldIcon：PriceText のすぐ左（右端から 100px の位置＝PriceText 左端に接続）
        var goldIconObj = new GameObject("GoldIcon");
        goldIconObj.transform.SetParent(nameRow.transform, false);
        var goldIconRect = goldIconObj.AddComponent<RectTransform>();
        goldIconRect.anchorMin = new Vector2(1f, 0.5f);
        goldIconRect.anchorMax = new Vector2(1f, 0.5f);
        goldIconRect.pivot = new Vector2(1f, 0.5f);
        goldIconRect.anchoredPosition = new Vector2(-80f, 0f);
        goldIconRect.sizeDelta = new Vector2(50f, 50f);
        goldIconObj.AddComponent<Image>().color = Color.yellow;

        // DrinkIcon（X-100 Y50、160x160）
        var drinkIconObj = new GameObject("DrinkIcon");
        drinkIconObj.transform.SetParent(root.transform, false);
        var drinkIconRect = drinkIconObj.AddComponent<RectTransform>();
        drinkIconRect.anchorMin = new Vector2(0.5f, 0.5f);
        drinkIconRect.anchorMax = new Vector2(0.5f, 0.5f);
        drinkIconRect.pivot = new Vector2(0.5f, 0.5f);
        drinkIconRect.anchoredPosition = new Vector2(-100f, 50f);
        drinkIconRect.sizeDelta = new Vector2(160f, 160f);
        var drinkIconImg = drinkIconObj.AddComponent<Image>();
        drinkIconImg.color = Color.white;
        drinkIconImg.raycastTarget = false;

        // DrinkCardUI の参照は Awake/ReconnectReferences で入る。Inspector 用にアサイン
        var soCard = new UnityEditor.SerializedObject(cardUI);
        soCard.Update();
        soCard.FindProperty("drinkNameText").objectReferenceValue = nameTMP;
        soCard.FindProperty("goldIconImage").objectReferenceValue = goldIconObj.GetComponent<Image>();
        soCard.FindProperty("priceText").objectReferenceValue = priceTMP;
        soCard.FindProperty("drinkIconImage").objectReferenceValue = drinkIconImg;
        soCard.ApplyModifiedProperties();

        root.SetActive(false);

        soSelf = new UnityEditor.SerializedObject(this);
        soSelf.Update();
        soSelf.FindProperty("drinkCardTemplate").objectReferenceValue = cardUI;
        soSelf.ApplyModifiedProperties();
        UnityEditor.EditorUtility.SetDirty(gameObject);
        Debug.Log("[ShopUI] DrinkCardTemplate を作成しました。ショップを開いて一覧を確認してください。");
    }

    private void CreateShopPanel(Transform parent)
    {
        var panelObj = new GameObject("ShopPanel");
        panelObj.transform.SetParent(parent, false);
        var panelRect = panelObj.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(shopPanelWidth, shopPanelHeight);
        panelRect.anchoredPosition = new Vector2(shopPanelX, 0f);
        panelObj.AddComponent<Image>().color = new Color(0.08f, 0.08f, 0.12f, 0.97f);

        var panelLayout = panelObj.AddComponent<VerticalLayoutGroup>();
        panelLayout.spacing = 10f;
        panelLayout.padding = new RectOffset(20, 20, 16, 16);
        panelLayout.childAlignment = TextAnchor.UpperCenter;
        panelLayout.childControlWidth = true;
        panelLayout.childControlHeight = true;
        panelLayout.childForceExpandWidth = true;
        panelLayout.childForceExpandHeight = false;

        // HeaderRow（Gem の CreateHeaderRow に相当）
        var headerObj = new GameObject("HeaderRow");
        headerObj.transform.SetParent(panelObj.transform, false);
        headerObj.AddComponent<LayoutElement>().preferredHeight = 50f;

        var titleObj = new GameObject("TitleText");
        titleObj.transform.SetParent(headerObj.transform, false);
        var titleRect = titleObj.AddComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 0f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.offsetMin = Vector2.zero;
        titleRect.offsetMax = new Vector2(-260f, 0f);
        drinkCountText = titleObj.AddComponent<TextMeshProUGUI>();
        drinkCountText.text = $"ドリンク回数: {DrinkSession.PurchaseCount}/{drinkLimit}";
        drinkCountText.fontSize = 30f;
        drinkCountText.fontStyle = FontStyles.Bold;
        drinkCountText.alignment = TextAlignmentOptions.MidlineLeft;
        drinkCountText.color = Color.white;
        drinkCountText.enableWordWrapping = false;

        var buyBtnObj = new GameObject("BuyButton");
        buyBtnObj.transform.SetParent(headerObj.transform, false);
        var buyBtnRect = buyBtnObj.AddComponent<RectTransform>();
        buyBtnRect.anchorMin = new Vector2(1f, 0.5f);
        buyBtnRect.anchorMax = new Vector2(1f, 0.5f);
        buyBtnRect.sizeDelta = new Vector2(100f, 46f);
        buyBtnRect.anchoredPosition = new Vector2(-185f, 0f);
        buyBtnObj.AddComponent<Image>().color = new Color(0.15f, 0.35f, 0.20f, 1f);
        buyButton = buyBtnObj.AddComponent<Button>();

        var buyBtnTextObj = new GameObject("Text");
        buyBtnTextObj.transform.SetParent(buyBtnObj.transform, false);
        var buyBtnTextRect = buyBtnTextObj.AddComponent<RectTransform>();
        buyBtnTextRect.anchorMin = Vector2.zero;
        buyBtnTextRect.anchorMax = Vector2.one;
        buyBtnTextRect.sizeDelta = Vector2.zero;
        var buyBtnTMP = buyBtnTextObj.AddComponent<TextMeshProUGUI>();
        buyBtnTMP.text = "購入";
        buyBtnTMP.fontSize = 20f;
        buyBtnTMP.alignment = TextAlignmentOptions.Center;
        buyBtnTMP.color = Color.white;

        var closeBtnObj = new GameObject("CloseButton");
        closeBtnObj.transform.SetParent(headerObj.transform, false);
        var closeBtnRect = closeBtnObj.AddComponent<RectTransform>();
        closeBtnRect.anchorMin = new Vector2(1f, 0.5f);
        closeBtnRect.anchorMax = new Vector2(1f, 0.5f);
        closeBtnRect.sizeDelta = new Vector2(120f, 46f);
        closeBtnRect.anchoredPosition = new Vector2(-65f, 0f);
        closeBtnObj.AddComponent<Image>().color = new Color(0.4f, 0.15f, 0.15f, 1f);
        closeButton = closeBtnObj.AddComponent<Button>();

        var closeBtnTextObj = new GameObject("Text");
        closeBtnTextObj.transform.SetParent(closeBtnObj.transform, false);
        var closeBtnTextRect = closeBtnTextObj.AddComponent<RectTransform>();
        closeBtnTextRect.anchorMin = Vector2.zero;
        closeBtnTextRect.anchorMax = Vector2.one;
        closeBtnTextRect.sizeDelta = Vector2.zero;
        var closeBtnTMP = closeBtnTextObj.AddComponent<TextMeshProUGUI>();
        closeBtnTMP.text = "閉じる";
        closeBtnTMP.fontSize = 20f;
        closeBtnTMP.alignment = TextAlignmentOptions.Center;
        closeBtnTMP.color = Color.white;

        // Separator
        var sepObj = new GameObject("Separator");
        sepObj.transform.SetParent(panelObj.transform, false);
        sepObj.AddComponent<Image>().color = new Color(0.3f, 0.3f, 0.4f, 1f);
        sepObj.AddComponent<LayoutElement>().preferredHeight = 2f;

        // ScrollView（横スクロール）
        var scrollObj = new GameObject("ScrollView");
        scrollObj.transform.SetParent(panelObj.transform, false);
        scrollObj.AddComponent<RectTransform>();
        var scroll = scrollObj.AddComponent<ScrollRect>();
        scroll.horizontal = true;
        scroll.vertical = false;
        scrollObj.AddComponent<LayoutElement>().preferredHeight = 720f;
        scrollObj.AddComponent<LayoutElement>().flexibleHeight = 1f;

        var viewportObj = new GameObject("Viewport");
        viewportObj.transform.SetParent(scrollObj.transform, false);
        var viewportRect = viewportObj.AddComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.sizeDelta = Vector2.zero;
        viewportRect.anchoredPosition = Vector2.zero;
        viewportObj.AddComponent<RectMask2D>();

        var contentObj = new GameObject("Content");
        contentObj.transform.SetParent(viewportObj.transform, false);
        var contentRect = contentObj.AddComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 0.5f);
        contentRect.anchorMax = new Vector2(0f, 0.5f);
        contentRect.pivot = new Vector2(0f, 0.5f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = new Vector2(0f, 0f);

        // ドリンクカードを1行で横に並べる（横スクロール用）
        var grid = contentObj.AddComponent<GridLayoutGroup>();
        grid.spacing = new Vector2(16f, 16f);
        grid.padding = new RectOffset(4, 4, 4, 4);
        grid.cellSize = new Vector2(cardWidth, cardHeight);
        grid.startAxis = GridLayoutGroup.Axis.Horizontal;
        grid.constraint = GridLayoutGroup.Constraint.FixedRowCount;
        grid.constraintCount = 1;
        grid.childAlignment = TextAnchor.MiddleLeft;

        var contentFitter = contentObj.AddComponent<ContentSizeFitter>();
        contentFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.content = contentRect;
        scroll.viewport = viewportRect;

        drinkListContainer = contentObj.transform;
        shopPanel = panelObj;
        panelObj.SetActive(false);
    }
#endif
}
