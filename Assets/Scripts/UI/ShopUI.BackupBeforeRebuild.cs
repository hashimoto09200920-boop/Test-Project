using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Game.Shop;
using Game.Skills;

/// <summary>
/// AreaSelectシーンでショップ（ドリンク購入）を管理するオーバーレイUI
/// GemManagementUIと同じパターン：ContextMenuでHierarchyを自動生成
///
/// 手順:
///   1. ContextMenu「① Create Shop Button in AreaPanel」でAreaPanelにShopButtonを生成
///   2. ContextMenu「② Setup Shop Panel」でCanvasにShopPanel一式を生成
/// </summary>
/// <summary>復元時はこのファイルの内容を ShopUI.cs にコピーし、クラス名を ShopUI に戻す。</summary>
public class ShopUIBackupBeforeRebuild : MonoBehaviour
{
    [Header("Panel References")]
    [SerializeField] private GameObject dimPanel;
    [SerializeField] private Image shopBgImage;
    [SerializeField] private Image shopCharacterImage;
    [SerializeField] private Image shopCounterImage;
    [SerializeField] private Image customerImage;
    [SerializeField] private GameObject shopPanel;

    [Header("Header")]
    [SerializeField] private TextMeshProUGUI drinkCountText;  // "ドリンク回数: 0/1"（HeaderRow/TitleText）
    [SerializeField] private Button buyButton;
    [SerializeField] private Button closeButton;

    [Header("Drink List")]
    [SerializeField] private Transform drinkListContainer;    // ScrollView > Viewport > Content
    [SerializeField] private DrinkCardUI drinkCardTemplate;   // ⑥ContextMenuで生成するテンプレート

    [Header("Settings")]
    [Tooltip("1回のAreaSelectセッションで購入できるドリンクの上限")]
    [Min(1)]
    [SerializeField] private int drinkLimit = 1;

    [Header("Display Settings")]
    [Tooltip("ドリンクカードの横幅")]
    [SerializeField] private float cardWidth = 280f;
    [Tooltip("ドリンクカードの縦幅")]
    [SerializeField] private float cardHeight = 440f;
    [Tooltip("ドリンクアイコンの高さ")]
    [SerializeField] private float drinkIconHeight = 100f;
    [Tooltip("フレーバーテキストの行高（固定行数分の高さを確保）")]
    [SerializeField] private float flavorLineHeight = 20f;
    [Tooltip("フレーバーテキストの固定行数")]
    [SerializeField] private int flavorLineCount = 5;
    [Tooltip("スキルアイコンサイズ")]
    [SerializeField] private float skillIconSize = 36f;

    [Header("Background Animation")]
    [Range(0f, 1f)] [SerializeField] private float bgBrightnessMin = 0.7f;
    [Range(0f, 1f)] [SerializeField] private float bgBrightnessMax = 1.0f;
    [SerializeField] private float bgAnimSpeedMin = 0.2f;
    [SerializeField] private float bgAnimSpeedMax = 0.7f;
    [SerializeField] private float bgSpeedChangeInterval = 3f;

    [Header("Character Transform")]
    [SerializeField] private float characterPosX = 400f;
    [SerializeField] private float characterPosY = 0f;
    [SerializeField] private float characterWidth = 600f;
    [SerializeField] private float characterHeight = 900f;

    [Header("Counter Transform")]
    [SerializeField] private float counterPosX = 0f;
    [SerializeField] private float counterPosY = 0f;
    [SerializeField] private float counterWidth = 1920f;
    [SerializeField] private float counterHeight = 400f;

    [Header("Customer Transform")]
    [SerializeField] private float customerPosX = -400f;
    [SerializeField] private float customerPosY = 0f;
    [SerializeField] private float customerWidth = 500f;
    [SerializeField] private float customerHeight = 800f;

    [Header("Character Animation")]
    [SerializeField] private Sprite[] characterAnimFrames;
    [SerializeField] private float characterAnimFps = 6f;

    [Header("Open/Close Fade")]
    [Tooltip("フェード時間（秒）。0でフェードなし")]
    [SerializeField] private float fadeDuration = 0.3f;

    [Header("SE")]
    [SerializeField] private AudioClip buySE;
    [SerializeField] private AudioClip closeSE;
    [SerializeField] private AudioClip insufficientGoldSE;
    [SerializeField] private AudioClip selectSE;

    // ランタイム状態
    private readonly List<GameObject> drinkCardObjects = new List<GameObject>();
    private DrinkDefinition selectedDrink = null;
    private GameObject selectedCardObj = null;
    private DrinkCardUI selectedCardUI = null;
    private AudioSource audioSource;
    private Coroutine bgAnimCoroutine;
    private Coroutine characterAnimCoroutine;
    private bool isOpening = false;
    private bool isClosing = false;

    // GoldHUD 前面表示用
    private Transform goldHUDOriginalParent;
    private int goldHUDOriginalSiblingIndex;

    private static readonly Color CardNormalColor    = new Color(0.10f, 0.10f, 0.15f, 0.95f);
    private static readonly Color CardSelectedColor  = new Color(0.20f, 0.35f, 0.50f, 1.00f);

    /// <summary>ランタイムで作るTMP用フォント（未設定だとテキストが描画されないことがある）</summary>
    private static TMP_FontAsset _defaultTMPFont;
    private static TMP_FontAsset DefaultTMPFont =>
        _defaultTMPFont ? _defaultTMPFont : (_defaultTMPFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF - Fallback"));

    // ========== Unity Lifecycle ==========

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }

        // Inspector参照が失われている場合、GameObject名で自動復元
        AutoReconnectReferences();

        // GemManagementUI と同じ構成: テンプレートは ShopUI 直下に置き、Content にはクロンのみ
        EnsureDrinkCardTemplateUnderShopUI();

        if (closeButton != null)
            closeButton.onClick.AddListener(Close);
        if (buyButton != null)
            buyButton.onClick.AddListener(OnBuySelected);

        HideAllPanels();
    }

    /// <summary>
    /// スクリプト再コンパイル等でSerializeField参照が失われた場合、
    /// Canvas内のGameObject名で自動的に再接続する
    /// </summary>
    private void AutoReconnectReferences()
    {
        var canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;

        if (dimPanel == null)
        {
            var t = canvas.transform.Find("ShopDimPanel");
            if (t != null) dimPanel = t.gameObject;
        }
        if (shopBgImage == null)
        {
            var t = canvas.transform.Find("ShopBgImage");
            if (t != null) shopBgImage = t.GetComponent<Image>();
        }
        if (shopCharacterImage == null)
        {
            var t = canvas.transform.Find("ShopCharacterImage");
            if (t != null) shopCharacterImage = t.GetComponent<Image>();
        }
        if (shopCounterImage == null)
        {
            var t = canvas.transform.Find("ShopCounter");
            if (t != null) shopCounterImage = t.GetComponent<Image>();
        }
        if (customerImage == null)
        {
            var t = canvas.transform.Find("Customer");
            if (t != null) customerImage = t.GetComponent<Image>();
        }
        if (shopPanel == null)
        {
            var t = canvas.transform.Find("ShopPanel");
            if (t != null) shopPanel = t.gameObject;
        }
        if (shopPanel != null)
        {
            if (drinkCountText == null)
            {
                var t = shopPanel.transform.Find("HeaderRow/TitleText");
                if (t != null) drinkCountText = t.GetComponent<TextMeshProUGUI>();
            }
            if (buyButton == null)
            {
                var t = shopPanel.transform.Find("HeaderRow/BuyButton");
                if (t != null) buyButton = t.GetComponent<Button>();
            }
            if (closeButton == null)
            {
                var t = shopPanel.transform.Find("HeaderRow/CloseButton");
                if (t != null) closeButton = t.GetComponent<Button>();
            }
            if (drinkListContainer == null)
            {
                var t = shopPanel.transform.Find("ScrollView/Viewport/Content");
                if (t != null) drinkListContainer = t;
            }
        }

        // DrinkCardTemplate 参照が未設定なら ShopUI 直下または Content 内を探す
        if (drinkCardTemplate == null)
        {
            var t = transform.Find("DrinkCardTemplate");
            if (t != null) drinkCardTemplate = t.GetComponent<DrinkCardUI>();
            if (drinkCardTemplate == null && drinkListContainer != null)
            {
                t = drinkListContainer.Find("DrinkCardTemplate");
                if (t != null) drinkCardTemplate = t.GetComponent<DrinkCardUI>();
            }
        }
    }

    /// <summary>
    /// GemManagementUI と同じ構成: DrinkCardTemplate を Content 外（ShopUI 直下）に置く。表示するのはクロンのみ。
    /// </summary>
    private void EnsureDrinkCardTemplateUnderShopUI()
    {
        if (drinkCardTemplate == null) return;
        var templateObj = drinkCardTemplate.gameObject;
        if (templateObj.transform.parent == transform)
        {
            templateObj.SetActive(false);
            return;
        }
        templateObj.transform.SetParent(transform, false);
        templateObj.SetActive(false);
    }

    // ========== Public API ==========

    /// <summary>ショップを開く（ShopButtonのonClickから呼ぶ）</summary>
    public void Open()
    {
        if (isOpening) return;
        StartCoroutine(OpenCoroutine());
    }

    /// <summary>ショップを閉じる</summary>
    public void Close()
    {
        if (isClosing) return;
        StartCoroutine(CloseCoroutine());
    }

    // ========== Open / Close ==========

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

        // GoldHUDをCanvas直下に移動して最前面に表示
        var canvas = GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            var goldHUD = FindTransformByName(canvas.transform, "GoldHUD");
            if (goldHUD != null)
            {
                Debug.Log($"[ShopUI] GoldHUD found at: {GetTransformPath(goldHUD)}");
                goldHUDOriginalParent       = goldHUD.parent;
                goldHUDOriginalSiblingIndex = goldHUD.GetSiblingIndex();
                goldHUD.SetParent(canvas.transform, true);
                goldHUD.SetAsLastSibling();
            }
            else
            {
                Debug.LogWarning("[ShopUI] GoldHUD が Canvas 配下に見つかりませんでした。");
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

        // GoldHUDを元の親・位置に戻す
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
        if (shopBgImage != null)
        {
            shopBgImage.color = Color.white;
            shopBgImage.gameObject.SetActive(false);
        }
        if (shopCharacterImage != null) shopCharacterImage.gameObject.SetActive(false);
        if (shopCounterImage != null) shopCounterImage.gameObject.SetActive(false);
        if (customerImage != null) customerImage.gameObject.SetActive(false);
        if (shopPanel != null) shopPanel.SetActive(false);
    }

    // ========== Display ==========

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
        bool hasGold = GoldManager.Instance != null
                       && selectedDrink != null
                       && GoldManager.Instance.PersistentGold >= selectedDrink.price;
        buyButton.interactable = withinLimit && hasSelection && hasGold;
    }

    private void RefreshDrinkCards()
    {
        foreach (var obj in drinkCardObjects)
            if (obj != null) Destroy(obj);
        drinkCardObjects.Clear();
        selectedDrink   = null;
        selectedCardObj = null;
        selectedCardUI  = null;

        if (shopPanel == null) return;

        // シーンの Content は使わず、ドリンクリスト用の ScrollView をコードで一から作成する
        Transform contentParent = GetOrCreateDrinkListRoot();
        if (contentParent == null) return;

        if (drinkCardTemplate != null)
            drinkCardTemplate.gameObject.SetActive(false);

        var drinks = Resources.LoadAll<DrinkDefinition>("GameData/Drinks");
        foreach (var drink in drinks)
        {
            GameObject cardObj = CreateDrinkCardSimple(contentParent, drink);
            drinkCardObjects.Add(cardObj);
        }
    }

    /// <summary>ShopPanel 直下にドリンクリスト用 ScrollView をコードで作成し、Content の Transform を返す（既にあれば再利用）</summary>
    private Transform GetOrCreateDrinkListRoot()
    {
        const string rootName = "DrinkListRoot";
        Transform root = shopPanel.transform.Find(rootName);
        if (root != null)
        {
            // 既存の Content を返す（ScrollView/Viewport/Content の Content）
            var content = root.Find("ScrollView/Viewport/Content");
            return content != null ? content : root;
        }

        // 一から作成
        var rootGo = new GameObject(rootName);
        rootGo.transform.SetParent(shopPanel.transform, false);
        var rootRT = rootGo.AddComponent<RectTransform>();
        rootRT.anchorMin = new Vector2(0f, 0f);
        rootRT.anchorMax = new Vector2(1f, 1f);
        rootRT.offsetMin = new Vector2(20f, 70f);
        rootRT.offsetMax = new Vector2(-20f, -20f);

        // シーンにあった古い ScrollView は削除して重複を防ぐ
        var oldScroll = shopPanel.transform.Find("ScrollView");
        if (oldScroll != null)
            Destroy(oldScroll.gameObject);

        var scrollGo = new GameObject("ScrollView");
        scrollGo.transform.SetParent(rootGo.transform, false);
        var scrollRT = scrollGo.AddComponent<RectTransform>();
        scrollRT.anchorMin = Vector2.zero;
        scrollRT.anchorMax = Vector2.one;
        scrollRT.offsetMin = Vector2.zero;
        scrollRT.offsetMax = Vector2.zero;
        var scrollRect = scrollGo.AddComponent<ScrollRect>();
        scrollRect.horizontal = true;
        scrollRect.vertical = false;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollGo.AddComponent<Image>().color = Color.clear;

        var viewportGo = new GameObject("Viewport");
        viewportGo.transform.SetParent(scrollGo.transform, false);
        var viewportRT = viewportGo.AddComponent<RectTransform>();
        viewportRT.anchorMin = Vector2.zero;
        viewportRT.anchorMax = Vector2.one;
        viewportRT.offsetMin = Vector2.zero;
        viewportRT.offsetMax = Vector2.zero;
        viewportGo.AddComponent<Image>().color = Color.clear;
        viewportGo.AddComponent<Mask>().showMaskGraphic = false;

        var contentGo = new GameObject("Content");
        contentGo.transform.SetParent(viewportGo.transform, false);
        var contentRT = contentGo.AddComponent<RectTransform>();
        contentRT.anchorMin = new Vector2(0f, 0.5f);
        contentRT.anchorMax = new Vector2(0f, 0.5f);
        contentRT.pivot = new Vector2(0f, 0.5f);
        contentRT.sizeDelta = new Vector2(0f, cardHeight);
        contentRT.anchoredPosition = Vector2.zero;
        var hlg = contentGo.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 8f;
        hlg.padding = new RectOffset(8, 8, 8, 8);
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childControlWidth = false;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = true;
        contentGo.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect.viewport = viewportRT;
        scrollRect.content = contentRT;

        return contentGo.transform;
    }

    /// <summary>レイアウトに頼らず、RectTransform を明示してカードを作成（名前行のみ・確実に表示）</summary>
    private GameObject CreateDrinkCardSimple(Transform contentParent, DrinkDefinition drink)
    {
        var cardGo = new GameObject(drink.drinkName);
        cardGo.transform.SetParent(contentParent, false);

        var cardRT = cardGo.AddComponent<RectTransform>();
        cardRT.anchorMin = new Vector2(0f, 0.5f);
        cardRT.anchorMax = new Vector2(0f, 0.5f);
        cardRT.pivot = new Vector2(0f, 0.5f);
        cardRT.sizeDelta = new Vector2(cardWidth, cardHeight);
        cardRT.anchoredPosition = Vector2.zero;

        var bg = cardGo.AddComponent<Image>();
        bg.color = CardNormalColor;

        var btn = cardGo.AddComponent<Button>();
        var captured = drink;
        btn.onClick.AddListener(() => OnSelectDrink(captured, cardGo));
        btn.targetGraphic = bg;

        cardGo.AddComponent<LayoutElement>().preferredWidth = cardWidth;
        cardGo.AddComponent<LayoutElement>().preferredHeight = cardHeight;

        // 名前行のみ（上端に固定・レイアウトコンポーネントなし）
        const float rowH = 40f;
        const float pad = 10f;

        var rowGo = new GameObject("NamePriceRow");
        rowGo.transform.SetParent(cardGo.transform, false);
        var rowRT = rowGo.AddComponent<RectTransform>();
        rowRT.anchorMin = new Vector2(0f, 1f);
        rowRT.anchorMax = new Vector2(1f, 1f);
        rowRT.pivot = new Vector2(0.5f, 1f);
        rowRT.anchoredPosition = new Vector2(0f, -pad);
        rowRT.sizeDelta = new Vector2(-pad * 2f, rowH);

        float x = pad;
        const float gap = 4f;

        // ドリンク名
        var nameGo = new GameObject("DrinkName");
        nameGo.transform.SetParent(rowGo.transform, false);
        var nameRT = nameGo.AddComponent<RectTransform>();
        nameRT.anchorMin = new Vector2(0f, 0.5f);
        nameRT.anchorMax = new Vector2(0f, 0.5f);
        nameRT.pivot = new Vector2(0f, 0.5f);
        nameRT.anchoredPosition = new Vector2(x + 100f, 0f);
        nameRT.sizeDelta = new Vector2(200f, rowH);
        var nameTMP = nameGo.AddComponent<TextMeshProUGUI>();
        if (DefaultTMPFont != null) nameTMP.font = DefaultTMPFont;
        nameTMP.text = drink.drinkName;
        nameTMP.fontSize = 20f;
        nameTMP.fontStyle = FontStyles.Bold;
        nameTMP.color = Color.white;

        x += 200f + gap;
        var goldGo = new GameObject("GoldIcon");
        goldGo.transform.SetParent(rowGo.transform, false);
        var goldRT = goldGo.AddComponent<RectTransform>();
        goldRT.anchorMin = new Vector2(0f, 0.5f);
        goldRT.anchorMax = new Vector2(0f, 0.5f);
        goldRT.pivot = new Vector2(0.5f, 0.5f);
        goldRT.anchoredPosition = new Vector2(x + 12f, 0f);
        goldRT.sizeDelta = new Vector2(24f, 24f);
        goldGo.AddComponent<Image>().color = new Color(1f, 0.85f, 0.2f, 1f);

        x += 24f + gap;
        var priceGo = new GameObject("PriceText");
        priceGo.transform.SetParent(rowGo.transform, false);
        var priceRT = priceGo.AddComponent<RectTransform>();
        priceRT.anchorMin = new Vector2(0f, 0.5f);
        priceRT.anchorMax = new Vector2(0f, 0.5f);
        priceRT.pivot = new Vector2(0f, 0.5f);
        priceRT.anchoredPosition = new Vector2(x + 35f, 0f);
        priceRT.sizeDelta = new Vector2(70f, rowH);
        var priceTMP = priceGo.AddComponent<TextMeshProUGUI>();
        if (DefaultTMPFont != null) priceTMP.font = DefaultTMPFont;
        priceTMP.text = $"{drink.price}G";
        priceTMP.fontSize = 20f;
        priceTMP.color = new Color(1f, 0.9f, 0.3f, 1f);

        return cardGo;
    }

    // ========== Drink Card 生成 ==========

    private GameObject CreateDrinkCard(DrinkDefinition drink)
    {
        var cardObj = new GameObject(drink.drinkName);
        cardObj.transform.SetParent(drinkListContainer, false);

        var cardRT = cardObj.AddComponent<RectTransform>();
        cardRT.sizeDelta = new Vector2(cardWidth, cardHeight);

        var le = cardObj.AddComponent<LayoutElement>();
        le.minWidth       = cardWidth;
        le.preferredWidth = cardWidth;
        le.minHeight       = cardHeight;
        le.preferredHeight = cardHeight;

        var bg = cardObj.AddComponent<Image>();
        bg.color = CardNormalColor;

        var btn = cardObj.AddComponent<Button>();
        var captured = drink;
        btn.onClick.AddListener(() => OnSelectDrink(captured, cardObj));

        var vlg = cardObj.AddComponent<VerticalLayoutGroup>();
        vlg.spacing              = 6f;
        vlg.padding              = new RectOffset(10, 10, 10, 10);
        vlg.childAlignment       = TextAnchor.UpperCenter;
        vlg.childControlWidth    = true;
        vlg.childControlHeight   = true;
        vlg.childForceExpandWidth  = true;
        vlg.childForceExpandHeight = false;

        // ① 名前 + ゴールドアイコン + 価格
        CreateNamePriceRow(cardObj.transform, drink);
        // ② ドリンクアイコン
        CreateDrinkIconArea(cardObj.transform, drink);
        // ③ フレーバーテキスト（固定flavorLineCount行分）
        CreateFlavorText(cardObj.transform, drink);
        // ④ スキル枠（3枠固定）
        CreateSkillsContainer(cardObj.transform, drink);

        cardRT.sizeDelta = new Vector2(cardWidth, cardHeight);
        return cardObj;
    }

    private void CreateNamePriceRow(Transform parent, DrinkDefinition drink)
    {
        var rowObj = new GameObject("NamePriceRow");
        rowObj.transform.SetParent(parent, false);

        var rowRT = rowObj.AddComponent<RectTransform>();
        rowRT.anchorMin = new Vector2(0f, 0.5f);
        rowRT.anchorMax = new Vector2(1f, 0.5f);
        rowRT.pivot = new Vector2(0.5f, 0.5f);
        rowRT.sizeDelta = new Vector2(0f, 40f);
        rowRT.anchoredPosition = Vector2.zero;

        var hlg = rowObj.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing              = 4f;
        hlg.childAlignment       = TextAnchor.MiddleLeft;
        hlg.childControlWidth    = true;
        hlg.childControlHeight   = true;
        hlg.childForceExpandWidth  = false;
        hlg.childForceExpandHeight = true;
        var rowLE = rowObj.AddComponent<LayoutElement>();
        rowLE.preferredHeight = 40f;
        rowLE.minHeight = 40f;

        // ドリンク名
        var nameObj = new GameObject("DrinkName");
        nameObj.transform.SetParent(rowObj.transform, false);
        var nameRT = nameObj.AddComponent<RectTransform>();
        nameRT.sizeDelta = new Vector2(200f, 40f);
        var nameTMP = nameObj.AddComponent<TextMeshProUGUI>();
        if (DefaultTMPFont != null) nameTMP.font = DefaultTMPFont;
        nameTMP.text            = drink.drinkName;
        nameTMP.fontSize        = 20f;
        nameTMP.fontStyle       = FontStyles.Bold;
        nameTMP.color           = Color.white;
        nameTMP.enableWordWrapping = false;
        nameTMP.overflowMode    = TextOverflowModes.Ellipsis;
        nameObj.AddComponent<LayoutElement>().flexibleWidth = 1f;

        // ゴールドアイコン
        var goldIconObj = new GameObject("GoldIcon");
        goldIconObj.transform.SetParent(rowObj.transform, false);
        var goldRT = goldIconObj.AddComponent<RectTransform>();
        goldRT.sizeDelta = new Vector2(24f, 24f);
        var goldImg = goldIconObj.AddComponent<Image>();
        goldImg.color = new Color(1f, 0.85f, 0.2f, 1f);
        var goldLE = goldIconObj.AddComponent<LayoutElement>();
        goldLE.minWidth = goldLE.preferredWidth = 24f;
        goldLE.minHeight = goldLE.preferredHeight = 24f;

        // 価格テキスト
        var priceObj = new GameObject("PriceText");
        priceObj.transform.SetParent(rowObj.transform, false);
        var priceRT = priceObj.AddComponent<RectTransform>();
        priceRT.sizeDelta = new Vector2(70f, 40f);
        var priceTMP = priceObj.AddComponent<TextMeshProUGUI>();
        if (DefaultTMPFont != null) priceTMP.font = DefaultTMPFont;
        priceTMP.text           = $"{drink.price}G";
        priceTMP.fontSize       = 20f;
        priceTMP.color         = new Color(1f, 0.9f, 0.3f, 1f);
        priceTMP.alignment     = TextAlignmentOptions.MidlineRight;
        priceTMP.enableWordWrapping = false;
        priceObj.AddComponent<LayoutElement>().preferredWidth = 70f;
    }

    private void CreateDrinkIconArea(Transform parent, DrinkDefinition drink)
    {
        var iconObj = new GameObject("DrinkIcon");
        iconObj.transform.SetParent(parent, false);
        var img = iconObj.AddComponent<Image>();
        img.sprite         = drink.icon;
        img.color          = drink.icon != null ? Color.white : new Color(0.3f, 0.3f, 0.3f, 0.5f);
        img.preserveAspect = true;
        Debug.Log($"[ShopUI] DrinkIcon sprite={(drink.icon != null ? drink.icon.name : "NULL")}, color={img.color}");
        var le = iconObj.AddComponent<LayoutElement>();
        le.preferredHeight = drinkIconHeight;
        le.minHeight       = drinkIconHeight;
    }

    private void CreateFlavorText(Transform parent, DrinkDefinition drink)
    {
        float totalH = flavorLineHeight * flavorLineCount;

        // 枠（背景）コンテナ
        var containerObj = new GameObject("FlavorContainer");
        containerObj.transform.SetParent(parent, false);
        var containerImg = containerObj.AddComponent<Image>();
        containerImg.color = new Color(0.05f, 0.05f, 0.10f, 0.85f);
        var containerLE = containerObj.AddComponent<LayoutElement>();
        containerLE.preferredHeight = totalH + 8f;
        containerLE.minHeight       = totalH + 8f;

        // テキスト（コンテナ内にストレッチ）
        var flavorObj = new GameObject("FlavorText");
        flavorObj.transform.SetParent(containerObj.transform, false);
        var rt = flavorObj.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(4f, 4f);
        rt.offsetMax = new Vector2(-4f, -4f);
        var tmp = flavorObj.AddComponent<TextMeshProUGUI>();
        tmp.text               = drink.description ?? "";
        tmp.fontSize           = 14f;
        tmp.color              = new Color(0.75f, 0.75f, 0.75f, 1f);
        tmp.enableWordWrapping = true;
        tmp.overflowMode       = TextOverflowModes.Truncate;
    }

    private void CreateSkillsContainer(Transform parent, DrinkDefinition drink)
    {
        var containerObj = new GameObject("SkillsContainer");
        containerObj.transform.SetParent(parent, false);
        float rowH = skillIconSize + 8f;
        var skillsLE = containerObj.AddComponent<LayoutElement>();
        skillsLE.preferredHeight = rowH * 3f + 4f * 2f;
        skillsLE.minHeight       = rowH * 3f + 4f * 2f;
        var vlg = containerObj.AddComponent<VerticalLayoutGroup>();
        vlg.spacing              = 4f;
        vlg.childAlignment       = TextAnchor.UpperLeft;
        vlg.childControlWidth    = true;
        vlg.childControlHeight   = true;
        vlg.childForceExpandWidth  = true;
        vlg.childForceExpandHeight = false;

        var skills = new SkillDefinition[] { drink.targetSkill1, drink.targetSkill2, drink.targetSkill3 };
        for (int i = 0; i < 3; i++)
            CreateSkillRow(containerObj.transform, skills[i]);
    }

    private void CreateSkillRow(Transform parent, SkillDefinition skill)
    {
        var rowObj = new GameObject("SkillRow");
        rowObj.transform.SetParent(parent, false);
        rowObj.AddComponent<Image>().color = new Color(0.12f, 0.15f, 0.22f, 0.90f);

        var hlg = rowObj.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing              = 6f;
        hlg.padding              = new RectOffset(4, 4, 4, 4);
        hlg.childAlignment       = TextAnchor.MiddleLeft;
        hlg.childControlWidth    = true;
        hlg.childControlHeight   = true;
        hlg.childForceExpandWidth  = false;
        hlg.childForceExpandHeight = false;
        var skillRowLE = rowObj.AddComponent<LayoutElement>();
        skillRowLE.preferredHeight = skillIconSize + 8f;
        skillRowLE.minHeight       = skillIconSize + 8f;

        // アイコン
        var iconObj = new GameObject("SkillIcon");
        iconObj.transform.SetParent(rowObj.transform, false);
        var iconImg = iconObj.AddComponent<Image>();
        var iconLE  = iconObj.AddComponent<LayoutElement>();
        iconLE.minWidth      = skillIconSize;
        iconLE.preferredWidth  = skillIconSize;
        iconLE.minHeight     = skillIconSize;
        iconLE.preferredHeight = skillIconSize;
        if (skill != null)
        {
            iconImg.sprite = skill.icon;
            iconImg.color  = skill.icon != null ? Color.white : new Color(0.3f, 0.3f, 0.3f, 0.5f);
        }
        else
        {
            iconImg.color = new Color(0.2f, 0.2f, 0.2f, 0.3f);
        }

        // スキル名
        var nameObj = new GameObject("SkillName");
        nameObj.transform.SetParent(rowObj.transform, false);
        var nameTMP = nameObj.AddComponent<TextMeshProUGUI>();
        nameTMP.text      = skill != null ? skill.skillName : "—";
        nameTMP.fontSize  = 16f;
        nameTMP.color     = skill != null ? Color.white : new Color(0.4f, 0.4f, 0.4f, 1f);
        nameTMP.alignment = TextAlignmentOptions.MidlineLeft;
        nameTMP.enableWordWrapping = false;
        nameObj.AddComponent<LayoutElement>().flexibleWidth = 1f;
    }

    // ========== 選択 & 購入 ==========

    private void OnSelectDrink(DrinkDefinition drink, GameObject cardObj)
    {
        // 前の選択を解除
        if (selectedCardObj != null)
        {
            if (selectedCardUI != null) selectedCardUI.SetHighlight(false);
            else SetCardHighlight(selectedCardObj, false);
        }

        if (selectedDrink == drink)
        {
            selectedDrink   = null;
            selectedCardObj = null;
            selectedCardUI  = null;
        }
        else
        {
            selectedDrink   = drink;
            selectedCardObj = cardObj;
            selectedCardUI  = cardObj != null ? cardObj.GetComponent<DrinkCardUI>() : null;
            if (selectedCardUI != null) selectedCardUI.SetHighlight(true);
            else SetCardHighlight(cardObj, true);
            PlaySE(selectSE);
        }
        RefreshBuyButtonState();
    }

    private void SetCardHighlight(GameObject cardObj, bool selected)
    {
        if (cardObj == null) return;
        var img = cardObj.GetComponent<Image>();
        if (img == null) return;
        img.color = selected ? CardSelectedColor : CardNormalColor;
    }

    private void OnBuySelected()
    {
        if (selectedDrink == null) return;
        if (GoldManager.Instance == null) return;

        if (DrinkSession.PurchaseCount >= drinkLimit)
        {
            PlaySE(insufficientGoldSE);
            return;
        }
        if (GoldManager.Instance.PersistentGold < selectedDrink.price)
        {
            PlaySE(insufficientGoldSE);
            return;
        }

        GoldManager.Instance.SpendPersistentGold(selectedDrink.price);

        // スキルをランダム選択してブーストに追加
        var candidates = new List<SkillDefinition>();
        if (selectedDrink.targetSkill1 != null) candidates.Add(selectedDrink.targetSkill1);
        if (selectedDrink.targetSkill2 != null) candidates.Add(selectedDrink.targetSkill2);
        if (selectedDrink.targetSkill3 != null) candidates.Add(selectedDrink.targetSkill3);

        int count = Mathf.Min(selectedDrink.selectionCount, candidates.Count);
        for (int i = 0; i < count; i++)
        {
            int idx   = Random.Range(0, candidates.Count);
            var skill = candidates[idx];
            candidates.RemoveAt(idx);
            DrinkSession.AddBoost(skill.name, selectedDrink.levelUpCount);
            Debug.Log($"[ShopUI] 購入: {selectedDrink.drinkName} → {skill.skillName} +{selectedDrink.levelUpCount}");
        }

        DrinkSession.IncrementPurchaseCount();
        PlaySE(buySE);

        // 選択解除
        SetCardHighlight(selectedCardObj, false);
        selectedDrink   = null;
        selectedCardObj = null;

        RefreshDrinkCountDisplay();
        RefreshBuyButtonState();
    }

    // ========== Background / Character Animation ==========

    private IEnumerator AnimateBgBrightness()
    {
        float t            = 0f;
        float currentSpeed = Random.Range(bgAnimSpeedMin, bgAnimSpeedMax);
        float targetSpeed  = Random.Range(bgAnimSpeedMin, bgAnimSpeedMax);
        float speedTimer   = 0f;

        while (true)
        {
            float dt = Time.unscaledDeltaTime;
            speedTimer   += dt;
            currentSpeed  = Mathf.Lerp(currentSpeed, targetSpeed, dt * 1.5f);
            if (speedTimer >= bgSpeedChangeInterval)
            {
                targetSpeed = Random.Range(bgAnimSpeedMin, bgAnimSpeedMax);
                speedTimer  = 0f;
            }
            t += dt * currentSpeed;
            float brightness = Mathf.Lerp(bgBrightnessMin, bgBrightnessMax,
                                          (Mathf.Sin(t * Mathf.PI * 2f) + 1f) * 0.5f);
            if (shopBgImage != null)
                shopBgImage.color = new Color(brightness, brightness, brightness, 1f);
            yield return null;
        }
    }

    private IEnumerator AnimateCharacter()
    {
        int   frameIndex = 0;
        float interval   = 1f / Mathf.Max(characterAnimFps, 0.1f);
        while (true)
        {
            if (shopCharacterImage != null && characterAnimFrames != null && characterAnimFrames.Length > 0)
                shopCharacterImage.sprite = characterAnimFrames[frameIndex % characterAnimFrames.Length];
            frameIndex++;
            yield return new WaitForSecondsRealtime(interval);
        }
    }

    // ========== Hierarchy Utility ==========

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

    private static string GetTransformPath(Transform t)
    {
        string path = t.name;
        while (t.parent != null)
        {
            t    = t.parent;
            path = t.name + "/" + path;
        }
        return path;
    }

    // ========== SE ==========

    private void PlaySE(AudioClip clip)
    {
        if (audioSource == null || clip == null) return;
        float vol = SoundSettingsManager.Instance != null ? SoundSettingsManager.Instance.SEVolume : 1f;
        audioSource.PlayOneShot(clip, vol);
    }

    // ========== フェード ==========

    private IEnumerator Fade(float from, float to)
    {
        if (fadeDuration <= 0f) yield break;

        var fadeObj    = new GameObject("ShopFade");
        var fadeCanvas = fadeObj.AddComponent<Canvas>();
        fadeCanvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        fadeCanvas.sortingOrder = 9999;
        fadeObj.AddComponent<CanvasScaler>();
        fadeObj.AddComponent<GraphicRaycaster>();

        var imgObj  = new GameObject("FadeImage");
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

    // ========== Editor Auto-Setup ==========

#if UNITY_EDITOR

    [ContextMenu("⑥ Create DrinkCard Template (NamePriceRow only)")]
    private void CreateDrinkCardTemplate()
    {
        if (shopPanel == null)
        {
            Debug.LogError("[ShopUI] shopPanel が未設定です。");
            return;
        }

        // GemManagementUI と同じ: テンプレートは ShopUI 直下に作成（Content には置かない）
        Transform templateParent = transform;
        DeleteAllByName(templateParent, "DrinkCardTemplate");
        if (drinkListContainer != null)
            DeleteAllByName(drinkListContainer, "DrinkCardTemplate");
        DeleteAllByName(shopPanel.transform, "DrinkCardTemplate");
        drinkCardTemplate = null;

        // ── カード本体 ──────────────────────────
        var cardObj = new GameObject("DrinkCardTemplate");
        cardObj.transform.SetParent(templateParent, false);
        cardObj.SetActive(false); // 非表示テンプレート（GemManagementUI と同じ）

        var cardLE = cardObj.AddComponent<LayoutElement>();
        cardLE.minWidth        = cardWidth;
        cardLE.preferredWidth  = cardWidth;
        cardLE.minHeight       = cardHeight;
        cardLE.preferredHeight = cardHeight;

        var cardBg  = cardObj.AddComponent<Image>();
        cardBg.color = new Color(0.10f, 0.10f, 0.15f, 0.95f);

        var cardBtn = cardObj.AddComponent<Button>();

        var cardVLG = cardObj.AddComponent<VerticalLayoutGroup>();
        cardVLG.spacing              = 6f;
        cardVLG.padding              = new RectOffset(10, 10, 10, 10);
        cardVLG.childAlignment       = TextAnchor.UpperCenter;
        cardVLG.childControlWidth    = true;
        cardVLG.childControlHeight   = true;
        cardVLG.childForceExpandWidth  = true;
        cardVLG.childForceExpandHeight = false;

        var cardUI = cardObj.AddComponent<DrinkCardUI>();
        cardUI.cardBackground = cardBg;
        cardUI.selectButton   = cardBtn;

        // ── ① NamePriceRow ──────────────────────
        var nameRow = new GameObject("NamePriceRow");
        nameRow.transform.SetParent(cardObj.transform, false);

        var rowLE = nameRow.AddComponent<LayoutElement>();
        rowLE.preferredHeight = 40f;
        rowLE.minHeight       = 40f;

        var rowHLG = nameRow.AddComponent<HorizontalLayoutGroup>();
        rowHLG.spacing              = 4f;
        rowHLG.childAlignment       = TextAnchor.MiddleLeft;
        rowHLG.childControlWidth    = true;
        rowHLG.childControlHeight   = true;
        rowHLG.childForceExpandWidth  = false;
        rowHLG.childForceExpandHeight = true;

        // 日本語フォントをロード
        var jpFont = UnityEditor.AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>(
            "Assets/Fonts/NotoSansJP-Regular SDF.asset");

        // ドリンク名
        var nameTextObj = new GameObject("DrinkNameText");
        nameTextObj.transform.SetParent(nameRow.transform, false);
        var nameTMP = nameTextObj.AddComponent<TextMeshProUGUI>();
        nameTMP.text               = "ドリンク名";
        nameTMP.fontSize           = 20f;
        nameTMP.fontStyle          = FontStyles.Bold;
        nameTMP.color              = Color.white;
        nameTMP.enableWordWrapping = false;
        nameTMP.overflowMode       = TextOverflowModes.Ellipsis;
        if (jpFont != null) nameTMP.font = jpFont;
        nameTextObj.AddComponent<LayoutElement>().flexibleWidth = 1f;
        cardUI.drinkNameText = nameTMP;

        // ゴールドアイコン
        var goldIconObj = new GameObject("GoldIcon");
        goldIconObj.transform.SetParent(nameRow.transform, false);
        var goldImg = goldIconObj.AddComponent<Image>();
        goldImg.color = new Color(1f, 0.85f, 0.2f, 1f);
        var goldLE  = goldIconObj.AddComponent<LayoutElement>();
        goldLE.minWidth = goldLE.preferredWidth  = 24f;
        goldLE.minHeight = goldLE.preferredHeight = 24f;
        cardUI.goldIconImage = goldImg;

        // 価格テキスト
        var priceTextObj = new GameObject("PriceText");
        priceTextObj.transform.SetParent(nameRow.transform, false);
        var priceTMP = priceTextObj.AddComponent<TextMeshProUGUI>();
        priceTMP.text               = "0G";
        priceTMP.fontSize           = 20f;
        priceTMP.color              = new Color(1f, 0.9f, 0.3f, 1f);
        priceTMP.alignment          = TextAlignmentOptions.MidlineRight;
        priceTMP.enableWordWrapping = false;
        if (jpFont != null) priceTMP.font = jpFont;
        priceTextObj.AddComponent<LayoutElement>().preferredWidth = 70f;
        cardUI.priceText = priceTMP;

        drinkCardTemplate = cardUI;

        UnityEditor.EditorUtility.SetDirty(this.gameObject);
        UnityEditor.EditorUtility.SetDirty(cardObj);
        string fontMsg = (jpFont != null) ? "NotoSansJP-Regular SDF 適用済み" : "フォント未発見（手動設定要）";
        Debug.Log($"[ShopUI] DrinkCardTemplate を {templateParent.name} 下に生成しました。フォント: {fontMsg}");
    }

    [ContextMenu("⑦ Fix HeaderRow")]
    private void FixHeaderRow()
    {
        if (shopPanel == null)
        {
            Debug.LogError("[ShopUI] shopPanel が未設定です。");
            return;
        }

        var headerRow = shopPanel.transform.Find("HeaderRow");
        if (headerRow == null)
        {
            Debug.LogError("[ShopUI] HeaderRow が見つかりません。");
            return;
        }

        // GoldText 削除
        var goldText = headerRow.Find("GoldText");
        if (goldText != null)
        {
            DestroyImmediate(goldText.gameObject);
            Debug.Log("[ShopUI] GoldText を削除しました。");
        }

        // HeaderRow に HorizontalLayoutGroup がなければ追加
        var hlg = headerRow.GetComponent<HorizontalLayoutGroup>();
        if (hlg == null)
        {
            hlg = headerRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing              = 8f;
            hlg.padding              = new RectOffset(0, 0, 0, 0);
            hlg.childAlignment       = TextAnchor.MiddleLeft;
            hlg.childControlWidth    = true;
            hlg.childControlHeight   = true;
            hlg.childForceExpandWidth  = false;
            hlg.childForceExpandHeight = true;
            Debug.Log("[ShopUI] HeaderRow に HorizontalLayoutGroup を追加しました。");
        }

        // 日本語フォントをロード（CJK対応）
        var jpFont = UnityEditor.AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>(
            "Assets/Fonts/NotoSerifCJKjp-Regular SDF.asset");

        // TitleText を drinkCountText として再設定
        var titleT = headerRow.Find("TitleText");
        if (titleT != null)
        {
            var tmp = titleT.GetComponent<TextMeshProUGUI>();
            if (tmp != null)
            {
                tmp.text      = $"ドリンク回数: {DrinkSession.PurchaseCount}/{drinkLimit}";
                tmp.fontSize  = 24f;
                tmp.fontStyle = FontStyles.Normal;
                tmp.alignment = TextAlignmentOptions.MidlineLeft;
                tmp.color     = new Color(0.7f, 0.9f, 1f, 1f);
                if (jpFont != null) tmp.font = jpFont;
                drinkCountText = tmp;
            }
            // TitleText の RectTransform をリセット（アンカーによる全埋めを解除）
            var rect = titleT.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.anchorMin = new Vector2(0f, 0f);
                rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot     = new Vector2(0f, 0.5f);
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
            }
            // LayoutElement: 残り幅を全て使う
            var le = titleT.GetComponent<LayoutElement>();
            if (le == null) le = titleT.gameObject.AddComponent<LayoutElement>();
            le.flexibleWidth  = 1f;
            le.preferredWidth = -1f;
        }

        // 既存の BuyButton があれば削除（再作成）
        var existingBuy = headerRow.Find("BuyButton");
        if (existingBuy != null) DestroyImmediate(existingBuy.gameObject);

        // CloseButton を取得
        var closeBtn = headerRow.Find("CloseButton");
        if (closeBtn != null)
            closeButton = closeBtn.GetComponent<Button>();

        // BuyButton を新規作成
        var buyBtnObj = new GameObject("BuyButton");
        buyBtnObj.transform.SetParent(headerRow, false);
        buyBtnObj.AddComponent<Image>().color = new Color(0.2f, 0.45f, 0.2f, 1f);
        buyButton = buyBtnObj.AddComponent<Button>();
        buyButton.interactable = false;
        var buyLE = buyBtnObj.AddComponent<LayoutElement>();
        buyLE.preferredWidth = 160f;
        buyLE.minWidth       = 160f;
        CreateButtonText(buyBtnObj.transform, "購入", 24f);

        // CloseButton の直前に挿入
        if (closeBtn != null)
            buyBtnObj.transform.SetSiblingIndex(closeBtn.GetSiblingIndex());

        // CloseButton の LayoutElement を確保
        if (closeBtn != null)
        {
            var closeLE = closeBtn.GetComponent<LayoutElement>();
            if (closeLE == null) closeLE = closeBtn.gameObject.AddComponent<LayoutElement>();
            closeLE.preferredWidth = 160f;
            closeLE.minWidth       = 160f;
        }

        // DrinkCountRow を削除（不要）
        var drinkCountRow = shopPanel.transform.Find("DrinkCountRow");
        if (drinkCountRow != null)
        {
            DestroyImmediate(drinkCountRow.gameObject);
            Debug.Log("[ShopUI] DrinkCountRow を削除しました。");
        }

        UnityEditor.EditorUtility.SetDirty(shopPanel);
        UnityEditor.EditorUtility.SetDirty(this.gameObject);
        Debug.Log("[ShopUI] ⑦ HeaderRow 修正完了: GoldText削除 / BuyButton追加 / DrinkCountRow削除");
    }

    /// <summary>指定Transform以下の直接の子から name のものを全て削除</summary>
    private static void DeleteAllByName(Transform parent, string targetName)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            var child = parent.GetChild(i);
            if (child.name == targetName)
                DestroyImmediate(child.gameObject);
        }
    }

    [ContextMenu("⑨ Fix Content Layout")]
    private void FixContentLayout()
    {
        // drinkListContainer が未設定の場合、シーンから探す
        if (drinkListContainer == null && shopPanel != null)
        {
            var t = shopPanel.transform.Find("ScrollView/Viewport/Content");
            if (t != null) drinkListContainer = t;
        }

        if (drinkListContainer == null)
        {
            Debug.LogError("[ShopUI] drinkListContainer (Content) が見つかりません。");
            return;
        }

        var contentRT = drinkListContainer as RectTransform;
        if (contentRT == null) contentRT = drinkListContainer.GetComponent<RectTransform>();

        // Content を Viewport 中央に固定高さで配置（Viewport を縦方向にはみ出さない）
        contentRT.anchorMin        = new Vector2(0f, 0.5f);
        contentRT.anchorMax        = new Vector2(0f, 0.5f);
        contentRT.pivot            = new Vector2(0f, 0.5f);
        contentRT.sizeDelta        = new Vector2(contentRT.sizeDelta.x, cardHeight);
        contentRT.anchoredPosition = new Vector2(0f, 0f); // スクロール位置をリセット

        // HLG: childForceExpandHeight=false にしてカードが自然な高さ(cardHeight=440px)を維持
        var hlg = drinkListContainer.GetComponent<HorizontalLayoutGroup>();
        if (hlg != null)
        {
            hlg.childForceExpandHeight = false;
            hlg.childControlHeight     = true;
        }

        // ContentSizeFitter: verticalFit を明示的に Unconstrained に
        var csf = drinkListContainer.GetComponent<ContentSizeFitter>();
        if (csf != null)
            csf.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

        UnityEditor.EditorUtility.SetDirty(drinkListContainer.gameObject);
        Debug.Log($"[ShopUI] ⑨ Content Layout 修正完了: height={cardHeight}px, childForceExpandHeight=false");
    }

    [ContextMenu("⑧ Cleanup Duplicate Templates")]
    private void CleanupDuplicateTemplates()
    {
        DeleteAllByName(transform, "DrinkCardTemplate");
        if (drinkListContainer != null)
            DeleteAllByName(drinkListContainer, "DrinkCardTemplate");
        if (shopPanel != null)
            DeleteAllByName(shopPanel.transform, "DrinkCardTemplate");
        drinkCardTemplate = null;
        UnityEditor.EditorUtility.SetDirty(this.gameObject);
        Debug.Log("[ShopUI] ⑧ DrinkCardTemplate を全て削除しました。⑥ を再実行してください。");
    }

    [ContextMenu("① Create Shop Button in AreaPanel")]
    private void CreateShopButton()
    {
        var areaPanelGO = GameObject.Find("AreaPanel");
        if (areaPanelGO == null)
        {
            Debug.LogError("[ShopUI] 'AreaPanel' が見つかりません。03_AreaSelect シーンを開いているか確認してください。");
            return;
        }

        var existing = areaPanelGO.transform.Find("ShopButton");
        if (existing != null) DestroyImmediate(existing.gameObject);

        var btnObj  = new GameObject("ShopButton");
        btnObj.transform.SetParent(areaPanelGO.transform, false);

        var btnRect = btnObj.AddComponent<RectTransform>();
        btnRect.anchorMin        = new Vector2(0.5f, 0f);
        btnRect.anchorMax        = new Vector2(0.5f, 0f);
        btnRect.pivot            = new Vector2(0.5f, 0.5f);
        btnRect.anchoredPosition = new Vector2(750f, 460f);
        btnRect.sizeDelta        = new Vector2(160f, 150f);

        btnObj.AddComponent<Image>().color = new Color(0.55f, 0.35f, 0.1f, 1f);
        var btn = btnObj.AddComponent<Button>();

        var textObj  = new GameObject("Text");
        textObj.transform.SetParent(btnObj.transform, false);
        var textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
        var tmp = textObj.AddComponent<TMPro.TextMeshProUGUI>();
        tmp.text      = "SHOP";
        tmp.fontSize  = 30f;
        tmp.fontStyle = TMPro.FontStyles.Bold;
        tmp.alignment = TMPro.TextAlignmentOptions.Center;
        tmp.color     = Color.white;

        // onClick に Open() を登録
        var so    = new UnityEditor.SerializedObject(btn);
        so.Update();
        var calls = so.FindProperty("m_OnClick.m_PersistentCalls.m_Calls");
        calls.arraySize = 1;
        var call  = calls.GetArrayElementAtIndex(0);
        call.FindPropertyRelative("m_Target").objectReferenceValue          = this;
        call.FindPropertyRelative("m_TargetAssemblyTypeName").stringValue   = "ShopUI, Assembly-CSharp";
        call.FindPropertyRelative("m_MethodName").stringValue               = "Open";
        call.FindPropertyRelative("m_Mode").intValue                        = 1;
        call.FindPropertyRelative("m_CallState").intValue                   = 2;
        so.ApplyModifiedProperties();

        UnityEditor.EditorUtility.SetDirty(areaPanelGO);
        Debug.Log("[ShopUI] ShopButton を AreaPanel に生成しました。\n※画像スプライトを Inspector で差し替えてください。");
    }


    [ContextMenu("③ Apply Character Transform")]
    private void ApplyCharacterTransform()
    {
        if (shopCharacterImage == null) { Debug.LogError("[ShopUI] ShopCharacterImage が見つかりません。"); return; }
        var r = shopCharacterImage.GetComponent<RectTransform>();
        r.anchorMin        = new Vector2(0.5f, 0f);
        r.anchorMax        = new Vector2(0.5f, 0f);
        r.pivot            = new Vector2(0.5f, 0f);
        r.anchoredPosition = new Vector2(characterPosX, characterPosY);
        r.sizeDelta        = new Vector2(characterWidth, characterHeight);
        UnityEditor.EditorUtility.SetDirty(shopCharacterImage.gameObject);
        Debug.Log($"[ShopUI] ShopCharacterImage 更新: Pos=({characterPosX}, {characterPosY}), Size=({characterWidth}, {characterHeight})");
    }

    [ContextMenu("④ Apply Counter Transform")]
    private void ApplyCounterTransform()
    {
        if (shopCounterImage == null) { Debug.LogError("[ShopUI] ShopCounter が見つかりません。"); return; }
        var r = shopCounterImage.GetComponent<RectTransform>();
        r.anchoredPosition = new Vector2(counterPosX, counterPosY);
        r.sizeDelta        = new Vector2(counterWidth, counterHeight);
        UnityEditor.EditorUtility.SetDirty(shopCounterImage.gameObject);
        Debug.Log($"[ShopUI] ShopCounter 更新: Pos=({counterPosX}, {counterPosY}), Size=({counterWidth}, {counterHeight})");
    }

    [ContextMenu("⑤ Apply Customer Transform")]
    private void ApplyCustomerTransform()
    {
        if (customerImage == null) { Debug.LogError("[ShopUI] Customer が見つかりません。"); return; }
        var r = customerImage.GetComponent<RectTransform>();
        r.anchorMin        = new Vector2(0.5f, 0f);
        r.anchorMax        = new Vector2(0.5f, 0f);
        r.pivot            = new Vector2(0.5f, 0f);
        r.anchoredPosition = new Vector2(customerPosX, customerPosY);
        r.sizeDelta        = new Vector2(customerWidth, customerHeight);
        UnityEditor.EditorUtility.SetDirty(customerImage.gameObject);
        Debug.Log($"[ShopUI] Customer 更新: Pos=({customerPosX}, {customerPosY}), Size=({customerWidth}, {customerHeight})");
    }

    private void CreateDimPanel(Transform parent)
    {
        var obj  = new GameObject("ShopDimPanel");
        obj.transform.SetParent(parent, false);
        var rect = obj.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.sizeDelta = Vector2.zero;
        obj.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.75f);
        dimPanel = obj;
        obj.SetActive(false);
    }

    private void CreateMainPanel(Transform parent)
    {
        // パネル本体（画面中央、横広め）
        var panelObj  = new GameObject("ShopPanel");
        panelObj.transform.SetParent(parent, false);
        var panelRect = panelObj.AddComponent<RectTransform>();
        panelRect.anchorMin        = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax        = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta        = new Vector2(1200f, 600f);
        panelRect.anchoredPosition = Vector2.zero;
        panelObj.AddComponent<Image>().color = new Color(0.08f, 0.08f, 0.12f, 0.97f);

        var vlg = panelObj.AddComponent<VerticalLayoutGroup>();
        vlg.spacing              = 0f;
        vlg.padding              = new RectOffset(20, 20, 16, 16);
        vlg.childAlignment       = TextAnchor.UpperCenter;
        vlg.childControlWidth    = true;
        vlg.childControlHeight   = true;
        vlg.childForceExpandWidth  = true;
        vlg.childForceExpandHeight = false;
        shopPanel = panelObj;

        CreateHeaderRow(panelObj.transform);
        CreateDrinkCountRow(panelObj.transform);
        CreateSeparator(panelObj.transform);
        CreateHorizontalScrollView(panelObj.transform);

        panelObj.SetActive(false);
    }

    private void CreateHeaderRow(Transform parent)
    {
        var headerObj = new GameObject("HeaderRow");
        headerObj.transform.SetParent(parent, false);
        headerObj.AddComponent<LayoutElement>().preferredHeight = 50f;

        var titleObj  = new GameObject("TitleText");
        titleObj.transform.SetParent(headerObj.transform, false);
        var titleRect = titleObj.AddComponent<RectTransform>();
        titleRect.anchorMin = Vector2.zero;
        titleRect.anchorMax = Vector2.one;
        titleRect.offsetMin = Vector2.zero;
        titleRect.offsetMax = Vector2.zero;
        drinkCountText = titleObj.AddComponent<TextMeshProUGUI>();
        drinkCountText.text              = "SHOP";
        drinkCountText.fontSize          = 30f;
        drinkCountText.fontStyle         = FontStyles.Bold;
        drinkCountText.alignment         = TextAlignmentOptions.MidlineLeft;
        drinkCountText.color             = Color.white;
        drinkCountText.enableWordWrapping = false;
    }

    private void CreateDrinkCountRow(Transform parent)
    {
        var rowObj = new GameObject("DrinkCountRow");
        rowObj.transform.SetParent(parent, false);

        var hlg = rowObj.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing              = 8f;
        hlg.childAlignment       = TextAnchor.MiddleLeft;
        hlg.childControlWidth    = true;
        hlg.childControlHeight   = true;
        hlg.childForceExpandWidth  = false;
        hlg.childForceExpandHeight = true;
        rowObj.AddComponent<LayoutElement>().preferredHeight = 72f;

        // 日本語フォントをロード（CJK対応）
        var jpFont = UnityEditor.AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>(
            "Assets/Fonts/NotoSerifCJKjp-Regular SDF.asset");

        // ドリンク回数テキスト
        var countObj = new GameObject("DrinkCountText");
        countObj.transform.SetParent(rowObj.transform, false);
        drinkCountText = countObj.AddComponent<TextMeshProUGUI>();
        drinkCountText.text      = "ドリンク回数: 0/1";
        drinkCountText.fontSize  = 34f;
        drinkCountText.alignment = TextAlignmentOptions.MidlineLeft;
        drinkCountText.color     = new Color(0.7f, 0.9f, 1f, 1f);
        if (jpFont != null) drinkCountText.font = jpFont;
        countObj.AddComponent<LayoutElement>().flexibleWidth = 1f;

        // 購入ボタン
        var buyBtnObj = new GameObject("BuyButton");
        buyBtnObj.transform.SetParent(rowObj.transform, false);
        buyBtnObj.AddComponent<Image>().color = new Color(0.2f, 0.45f, 0.2f, 1f);
        buyButton = buyBtnObj.AddComponent<Button>();
        buyButton.interactable = false;
        buyBtnObj.AddComponent<LayoutElement>().preferredWidth = 160f;
        CreateButtonText(buyBtnObj.transform, "購入", 24f);

        // 退店ボタン
        var closeBtnObj = new GameObject("CloseButton");
        closeBtnObj.transform.SetParent(rowObj.transform, false);
        closeBtnObj.AddComponent<Image>().color = new Color(0.25f, 0.25f, 0.3f, 1f);
        closeButton = closeBtnObj.AddComponent<Button>();
        closeBtnObj.AddComponent<LayoutElement>().preferredWidth = 160f;
        CreateButtonText(closeBtnObj.transform, "退店", 24f);
    }

    private void CreateSeparator(Transform parent)
    {
        var sepObj = new GameObject("Separator");
        sepObj.transform.SetParent(parent, false);
        sepObj.AddComponent<Image>().color = new Color(0.3f, 0.3f, 0.4f, 1f);
        sepObj.AddComponent<LayoutElement>().preferredHeight = 2f;
    }

    private void CreateHorizontalScrollView(Transform parent)
    {
        var scrollObj = new GameObject("ScrollView");
        scrollObj.transform.SetParent(parent, false);
        scrollObj.AddComponent<LayoutElement>().flexibleHeight = 1f;

        var scroll = scrollObj.AddComponent<ScrollRect>();
        scroll.horizontal        = true;
        scroll.vertical          = false;
        scroll.scrollSensitivity = 30f;
        scroll.movementType      = ScrollRect.MovementType.Clamped;
        scrollObj.AddComponent<Image>().color = Color.clear;

        // Viewport
        var viewportObj  = new GameObject("Viewport");
        viewportObj.transform.SetParent(scrollObj.transform, false);
        var viewportRect = viewportObj.AddComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.sizeDelta = Vector2.zero;
        viewportObj.AddComponent<Image>().color = Color.clear;
        viewportObj.AddComponent<Mask>().showMaskGraphic = false;

        // Content（HorizontalLayoutGroup + ContentSizeFitter）
        var contentObj  = new GameObject("Content");
        contentObj.transform.SetParent(viewportObj.transform, false);
        var contentRect = contentObj.AddComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 0f);
        contentRect.anchorMax = new Vector2(0f, 1f);
        contentRect.pivot     = new Vector2(0f, 0.5f);
        contentRect.sizeDelta = Vector2.zero;

        var hlg = contentObj.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing              = 8f;   // 4枚×280 + 3間隔×8 + 左右padding×8×2 = 1160px (viewport幅ぴったり)
        hlg.padding              = new RectOffset(8, 8, 8, 8);
        hlg.childAlignment       = TextAnchor.MiddleLeft;
        hlg.childControlWidth    = false;
        hlg.childControlHeight   = true;
        hlg.childForceExpandWidth  = false;
        hlg.childForceExpandHeight = true;
        contentObj.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

        drinkListContainer = contentRect;
        scroll.viewport    = viewportRect;
        scroll.content     = contentRect;
    }

    private void CreateShopBgImage(Transform parent)
    {
        var obj  = new GameObject("ShopBgImage");
        obj.transform.SetParent(parent, false);
        var rect = obj.AddComponent<RectTransform>();
        rect.anchorMin        = Vector2.zero;
        rect.anchorMax        = Vector2.one;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta        = Vector2.zero;
        shopBgImage = obj.AddComponent<Image>();
        shopBgImage.color          = Color.white;
        shopBgImage.preserveAspect = false;
        obj.SetActive(false);
    }

    private void CreateShopCharacterImage(Transform parent)
    {
        var obj  = new GameObject("ShopCharacterImage");
        obj.transform.SetParent(parent, false);
        var rect = obj.AddComponent<RectTransform>();
        rect.anchorMin        = new Vector2(0.5f, 0f);
        rect.anchorMax        = new Vector2(0.5f, 0f);
        rect.pivot            = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(characterPosX, characterPosY);
        rect.sizeDelta        = new Vector2(characterWidth, characterHeight);
        shopCharacterImage = obj.AddComponent<Image>();
        shopCharacterImage.color          = Color.white;
        shopCharacterImage.preserveAspect = true;
        obj.SetActive(false);
    }

    private void CreateShopCounterImage(Transform parent)
    {
        var obj  = new GameObject("ShopCounter");
        obj.transform.SetParent(parent, false);
        var rect = obj.AddComponent<RectTransform>();
        rect.anchorMin        = new Vector2(0.5f, 0f);
        rect.anchorMax        = new Vector2(0.5f, 0f);
        rect.pivot            = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(counterPosX, counterPosY);
        rect.sizeDelta        = new Vector2(counterWidth, counterHeight);
        shopCounterImage = obj.AddComponent<Image>();
        shopCounterImage.color          = Color.white;
        shopCounterImage.preserveAspect = true;
        obj.SetActive(false);
    }

    private void CreateCustomerImage(Transform parent)
    {
        var obj  = new GameObject("Customer");
        obj.transform.SetParent(parent, false);
        var rect = obj.AddComponent<RectTransform>();
        rect.anchorMin        = new Vector2(0.5f, 0f);
        rect.anchorMax        = new Vector2(0.5f, 0f);
        rect.pivot            = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(customerPosX, customerPosY);
        rect.sizeDelta        = new Vector2(customerWidth, customerHeight);
        customerImage = obj.AddComponent<Image>();
        customerImage.color          = Color.white;
        customerImage.preserveAspect = true;
        obj.SetActive(false);
    }

    private void CreateButtonText(Transform parent, string text, float fontSize)
    {
        var textObj  = new GameObject("Text");
        textObj.transform.SetParent(parent, false);
        var rect = textObj.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.sizeDelta = Vector2.zero;
        var tmp = textObj.AddComponent<TextMeshProUGUI>();
        tmp.text      = text;
        tmp.fontSize  = fontSize;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color     = Color.white;
        var jpFont = UnityEditor.AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>(
            "Assets/Fonts/NotoSerifCJKjp-Regular SDF.asset");
        if (jpFont != null) tmp.font = jpFont;
    }

#endif
}
