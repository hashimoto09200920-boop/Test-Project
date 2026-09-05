using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using Game.Shop;
using Game.Skills;
using Game.UI;

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
    [Tooltip("ドリンク画面を開いている間、一緒に非表示にしたい他画面のボタン（チュートリアルボタン等）。dimPanelの外側にあり暗転で隠せない要素向け")]
    [SerializeField] private GameObject[] hideWhileOpen;

    [Header("Header")]
    [SerializeField] private TextMeshProUGUI drinkCountText;
    [SerializeField] private Button buyButton;
    [SerializeField] private Image buyBgImage;
    // ★以前はBuy.png(暗い背景込みの1枚絵)向けの暗い無効化色だったが、
    //   ネオン枠画像に変更後はこの色を掛けると枠がほぼ黒く潰れて見えなくなるため、
    //   枠の形が視認できる程度の明るさに調整（GemManagementUIの売却ボタンと同じ対応）
    [SerializeField] private Color buyBgDisabledColor = new Color(0.45f, 0.4f, 0.4f, 1f);
    [SerializeField] private Button closeButton;

    [Header("Neon Frame + Icon (購入/閉じるボタンの2層構成)")]
    [Tooltip("ボタン枠に使うネオン管フレーム画像（Assets/Art/AreaSelect/Shop/新ネオン枠.png）")]
    [SerializeField] private Sprite neonFrameSprite;
    [SerializeField] private Sprite buyIconSprite;
    [SerializeField] private Sprite exitIconSprite;
    [SerializeField] private Image buyButtonIcon;
    [SerializeField] private Image closeButtonIcon;
    [SerializeField] private float actionIconSize = 74f;

    [Header("Drink Count Icons")]
    [SerializeField] private RectTransform drinkIconContainer;
    [SerializeField] private Sprite drinkCountIcon;
    [SerializeField] private Color drinkIconActiveColor = Color.white;
    [SerializeField] private Color drinkIconInactiveColor = new Color(0.3f, 0.3f, 0.3f, 1f);
    [SerializeField] private float drinkCountLabelFontSize = 60f;
    private readonly List<Image> drinkIconImages = new List<Image>();
    private TextMeshProUGUI drinkCountLabel;

    [Header("Drink List")]
    [SerializeField] private Transform drinkListContainer;
    [SerializeField] private DrinkCardUI drinkCardTemplate;

    [Header("Pagination")]
    [SerializeField] private Button prevPageButton;
    [SerializeField] private Button nextPageButton;
    [SerializeField] private Transform pageDotsContainer;
    [SerializeField] private int cardsPerPage = 6;
    [Tooltip("ページボタン押下時のハイライト色")]
    [SerializeField] private Color pageButtonPressedColor = new Color(0.6f, 0.75f, 1f, 1f);
    [Tooltip("ページボタン押下時のSE")]
    [SerializeField] private AudioClip pageButtonSE;

    [Header("Settings")]
    [Min(1)] [SerializeField] private int drinkLimit = 1;

    [Header("Display Settings")]
    [SerializeField] private float cardWidth = 420f;
    [SerializeField] private float cardHeight = 440f;

    [Header("Shop Panel (Rebuild で使用)")]
    [SerializeField] private float shopPanelX = 100f;
    [SerializeField] private float shopPanelWidth = 1360f;
    [SerializeField] private float shopPanelHeight = 900f;

    [Header("NavRow")]
    [SerializeField] private float navRowHeight = 80f;
    [SerializeField] private Vector2 navButtonSize = new Vector2(200f, 80f);

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
    [Tooltip("ShopPanel のみ遅れてフェードインする遅延時間（秒）")]
    [SerializeField] private float shopPanelFadeInDelay = 0.2f;
    [Tooltip("ShopPanel のフェードイン時間（秒）")]
    [SerializeField] private float shopPanelFadeInDuration = 0.25f;

    [Header("SE")]
    [SerializeField] private AudioClip buySE;
    [SerializeField] private AudioClip closeSE;
    [SerializeField] private AudioClip insufficientGoldSE;
    [SerializeField] private AudioClip selectSE;

    [Header("Drink Effect Animation")]
    [Tooltip("カウントラベルが膨らむ最大スケール")]
    [SerializeField] private float popScaleMax = 1.5f;
    [Tooltip("スケールアップにかかる時間（秒）")]
    [SerializeField] private float popScaleUpDuration = 0.15f;
    [Tooltip("スケールダウンにかかる時間（秒）")]
    [SerializeField] private float popScaleDownDuration = 0.25f;
    [Tooltip("ポップ時のラベル色（フラッシュ色）")]
    [SerializeField] private Color popFlashColor = new Color(1f, 0.9f, 0.2f, 1f);
    [Tooltip("アイコンシェイクの横幅（px）")]
    [SerializeField] private float shakeAmount = 10f;
    [Tooltip("アイコンシェイクの所要時間（秒）")]
    [SerializeField] private float shakeDuration = 0.35f;
    [Tooltip("効果テキストの表示開始位置（Canvasの中心を(0,0)とした座標）")]
    [SerializeField] private Vector2 floatTextStartPos = new Vector2(0f, 0f);
    [Tooltip("効果テキストの上昇量（px）")]
    [SerializeField] private float floatTextRise = 80f;
    [Tooltip("効果テキストの表示時間（秒）")]
    [SerializeField] private float floatTextDuration = 1.5f;
    [Tooltip("フェードアウト開始タイミング（0〜1、全体時間に対する割合）")]
    [Range(0f, 1f)]
    [SerializeField] private float floatTextFadeStartRatio = 0.4f;
    [Tooltip("効果テキストのフォントサイズ")]
    [SerializeField] private int floatTextFontSize = 40;
    [Tooltip("効果テキストの色")]
    [SerializeField] private Color floatTextColor = new Color(1f, 0.95f, 0.5f, 1f);
    [Tooltip("画面フラッシュの色")]
    [SerializeField] private Color flashColor = new Color(0.75f, 0.95f, 0.85f, 0.12f);
    [Tooltip("画面フラッシュの所要時間（秒）")]
    [SerializeField] private float flashDuration = 0.5f;

    private readonly List<GameObject> drinkCardObjects = new List<GameObject>();
    private int currentPage = 0;
    private int totalPages = 1;
    private readonly List<Image> pageDots = new List<Image>();
    private DrinkDefinition selectedDrink;
    private GameObject selectedCardObj;
    private DrinkCardUI selectedCardUI;
    private AudioSource audioSource;
    private Color buyBgOriginalColor;
    private Color buyIconOriginalColor;
    private Coroutine bgAnimCoroutine;
    private Coroutine characterAnimCoroutine;
    private bool isOpening;
    private bool isClosing;
    private Transform goldHUDOriginalParent;
    private int goldHUDOriginalSiblingIndex;
    private Transform skillHUDCanvasAncestor;
    private int skillHUDCanvasAncestorSiblingIndex;
    private bool skillHUDWasActive;
    private HPStatusHUDUI hpStatusHUD;
    private bool hpStatusHUDWasActive;
    private int hpStatusHUDOriginalSiblingIndex;
    private readonly List<(Transform t, int sibIdx, bool wasActive)> slowMotionObjects = new();

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }

        AutoReconnectReferences();
        hpStatusHUD = FindFirstObjectByType<HPStatusHUDUI>(FindObjectsInactive.Include);

        if (buyBgImage != null)
            buyBgOriginalColor = buyBgImage.color;
        if (buyButtonIcon != null)
            buyIconOriginalColor = buyButtonIcon.color;

        if (closeButton != null)
            closeButton.onClick.AddListener(Close);
        if (buyButton != null)
            buyButton.onClick.AddListener(OnBuySelected);
        if (prevPageButton != null)
            prevPageButton.onClick.AddListener(GoToPrevPage);
        if (nextPageButton != null)
            nextPageButton.onClick.AddListener(GoToNextPage);

        ApplyPageButtonPressedColor(prevPageButton, "PrevBg");
        ApplyPageButtonPressedColor(nextPageButton, "NextBg");

        if (drinkIconContainer != null)
        {
            for (int i = 1; i < drinkIconContainer.childCount; i++)
                drinkIconContainer.GetChild(i).gameObject.SetActive(false);
            var textObj = new GameObject("DrinkCountLabel");
            textObj.transform.SetParent(drinkIconContainer, false);
            var textRect = textObj.AddComponent<RectTransform>();
            textRect.sizeDelta = new Vector2(120f, 48f);
            drinkCountLabel = textObj.AddComponent<TextMeshProUGUI>();
            drinkCountLabel.fontSize = drinkCountLabelFontSize;
            drinkCountLabel.fontStyle = FontStyles.Bold;
            drinkCountLabel.alignment = TextAlignmentOptions.MidlineLeft;
            drinkCountLabel.color = Color.white;
            drinkCountLabel.enableWordWrapping = false;
        }

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
            if (buyBgImage == null) { var t = shopPanel.transform.Find("HeaderRow/BuyButton/BuyBg"); if (t != null) buyBgImage = t.GetComponent<Image>(); }
            if (closeButton == null) { var t = shopPanel.transform.Find("HeaderRow/CloseButton"); if (t != null) closeButton = t.GetComponent<Button>(); }
            if (drinkListContainer == null) { var t = shopPanel.transform.Find("ScrollView/Viewport/Content"); if (t != null) drinkListContainer = t; }
            if (prevPageButton == null) { var t = shopPanel.transform.Find("NavRow/PrevButton"); if (t != null) prevPageButton = t.GetComponent<Button>(); }
            if (nextPageButton == null) { var t = shopPanel.transform.Find("NavRow/NextButton"); if (t != null) nextPageButton = t.GetComponent<Button>(); }
            if (pageDotsContainer == null) { var t = shopPanel.transform.Find("NavRow/DotsContainer"); if (t != null) pageDotsContainer = t; }
        }
        if (drinkCardTemplate == null) { var t = transform.Find("DrinkCardTemplate"); if (t != null) drinkCardTemplate = t.GetComponent<DrinkCardUI>(); }

        EnsureNavRow();
        ApplyHeaderButtonSizes();
    }

    /// <summary>ShopPanelにNavRowが無い場合はランタイムで生成し、ページング参照を確保する。</summary>
    private void EnsureNavRow()
    {
        if (shopPanel == null) return;

        var navRowTrans = shopPanel.transform.Find("NavRow");
        if (navRowTrans != null)
        {
            if (prevPageButton == null) { var t = navRowTrans.Find("PrevButton"); if (t != null) prevPageButton = t.GetComponent<Button>(); }
            if (nextPageButton == null) { var t = navRowTrans.Find("NextButton"); if (t != null) nextPageButton = t.GetComponent<Button>(); }
            if (pageDotsContainer == null) { var t = navRowTrans.Find("DotsContainer"); if (t != null) pageDotsContainer = t; }
            return;
        }

        // NavRowが存在しない → ランタイムで生成
        var navObj = new GameObject("NavRow");
        navObj.transform.SetParent(shopPanel.transform, false);
        navObj.transform.SetAsLastSibling();
        navObj.AddComponent<LayoutElement>().preferredHeight = navRowHeight;
        var navHLG = navObj.AddComponent<HorizontalLayoutGroup>();
        navHLG.childAlignment       = TextAnchor.MiddleCenter;
        navHLG.childControlWidth    = false;
        navHLG.childControlHeight   = true;
        navHLG.childForceExpandHeight = true;
        navHLG.spacing              = 20f;

        var prevBtnObj = new GameObject("PrevButton");
        prevBtnObj.transform.SetParent(navObj.transform, false);
        prevBtnObj.AddComponent<RectTransform>().sizeDelta = navButtonSize;
        prevBtnObj.AddComponent<Image>().color = new Color(0.2f, 0.2f, 0.3f, 1f);
        prevPageButton = prevBtnObj.AddComponent<Button>();
        prevPageButton.onClick.AddListener(GoToPrevPage);
        var prevTextObj = new GameObject("Text");
        prevTextObj.transform.SetParent(prevBtnObj.transform, false);
        var prevTextRect = prevTextObj.AddComponent<RectTransform>();
        prevTextRect.anchorMin = Vector2.zero; prevTextRect.anchorMax = Vector2.one; prevTextRect.sizeDelta = Vector2.zero;
        var prevTMP = prevTextObj.AddComponent<TextMeshProUGUI>();
        prevTMP.text = "＜"; prevTMP.fontSize = 28f; prevTMP.alignment = TextAlignmentOptions.Center; prevTMP.color = Color.white;

        var dotsObj = new GameObject("DotsContainer");
        dotsObj.transform.SetParent(navObj.transform, false);
        dotsObj.AddComponent<RectTransform>().sizeDelta = new Vector2(160f, navButtonSize.y);
        var dotsHLG = dotsObj.AddComponent<HorizontalLayoutGroup>();
        dotsHLG.childAlignment    = TextAnchor.MiddleCenter;
        dotsHLG.childControlWidth  = false;
        dotsHLG.childControlHeight = false;
        dotsHLG.spacing            = 8f;
        pageDotsContainer = dotsObj.transform;

        var nextBtnObj = new GameObject("NextButton");
        nextBtnObj.transform.SetParent(navObj.transform, false);
        nextBtnObj.AddComponent<RectTransform>().sizeDelta = navButtonSize;
        nextBtnObj.AddComponent<Image>().color = new Color(0.2f, 0.2f, 0.3f, 1f);
        nextPageButton = nextBtnObj.AddComponent<Button>();
        nextPageButton.onClick.AddListener(GoToNextPage);
        var nextTextObj = new GameObject("Text");
        nextTextObj.transform.SetParent(nextBtnObj.transform, false);
        var nextTextRect = nextTextObj.AddComponent<RectTransform>();
        nextTextRect.anchorMin = Vector2.zero; nextTextRect.anchorMax = Vector2.one; nextTextRect.sizeDelta = Vector2.zero;
        var nextTMP = nextTextObj.AddComponent<TextMeshProUGUI>();
        nextTMP.text = "＞"; nextTMP.fontSize = 28f; nextTMP.alignment = TextAlignmentOptions.Center; nextTMP.color = Color.white;
    }

    /// <summary>既存のShopPanelのヘッダーボタンサイズをGemManagementPanelと同一に更新する。</summary>
    private void ApplyHeaderButtonSizes()
    {
        // BuyButton
        if (buyButton != null)
        {
            var rt = buyButton.GetComponent<RectTransform>();
            if (rt != null) { rt.sizeDelta = new Vector2(160f, 72f); rt.anchoredPosition = new Vector2(-253f, 0f); }
            var tmp = buyButton.GetComponentInChildren<TextMeshProUGUI>();
            if (tmp != null) tmp.fontSize = 24f;
        }
        // CloseButton
        if (closeButton != null)
        {
            var rt = closeButton.GetComponent<RectTransform>();
            if (rt != null) { rt.sizeDelta = new Vector2(160f, 72f); rt.anchoredPosition = new Vector2(-85f, 0f); }
            var tmp = closeButton.GetComponentInChildren<TextMeshProUGUI>();
            if (tmp != null) tmp.fontSize = 24f;
        }
        // TitleText の右端オフセット更新（ボタン2枚分 + 余白）
        if (drinkCountText != null)
        {
            var rt = drinkCountText.GetComponent<RectTransform>();
            if (rt != null) rt.offsetMax = new Vector2(-336f, rt.offsetMax.y);
        }
        // HeaderRow の高さ更新、VLG spacing を調整（Separator・ScrollView を HeaderRow から離す）
        // ShopPanel の X 位置も Inspector 値で上書き（シーン保存値のずれを補正）
        if (shopPanel != null)
        {
            var headerLE = shopPanel.transform.Find("HeaderRow")?.GetComponent<LayoutElement>();
            if (headerLE != null) headerLE.preferredHeight = 72f;
            var vlg = shopPanel.GetComponent<VerticalLayoutGroup>();
            if (vlg != null) vlg.spacing = 20f;
            var panelRT = shopPanel.GetComponent<RectTransform>();
            if (panelRT != null) panelRT.anchoredPosition = new Vector2(shopPanelX, panelRT.anchoredPosition.y);
        }
    }

    public void Open()
    {
        if (isOpening) return;
        var areaMenu = FindObjectOfType<Game.UI.AreaSelectMenu>();
        if (areaMenu != null && areaMenu.IsTransitioning) return;
        StartCoroutine(OpenCoroutine());
    }

    public void Close()
    {
        if (isClosing) return;
        DestroyDrinkFloatTexts();
        StartCoroutine(CloseCoroutine());
    }

    private void DestroyDrinkFloatTexts()
    {
        var canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;
        foreach (Transform child in canvas.transform)
        {
            if (child != null && child.name == "DrinkFloatText")
                Destroy(child.gameObject);
        }
    }

    private IEnumerator OpenCoroutine()
    {
        isOpening = true;
        yield return StartCoroutine(Fade(0f, 1f));

        if (dimPanel != null) dimPanel.SetActive(true);
        SetHideWhileOpenActive(false);
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
            var cg = shopPanel.GetComponent<CanvasGroup>();
            if (cg == null) cg = shopPanel.AddComponent<CanvasGroup>();
            cg.alpha = (shopPanelFadeInDuration > 0f) ? 0f : 1f;
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

            var gemSkillHUD = canvas.transform.Find("GemSkillPreviewHUD");
            if (gemSkillHUD != null)
            {
                skillHUDCanvasAncestor = gemSkillHUD;
                skillHUDCanvasAncestorSiblingIndex = gemSkillHUD.GetSiblingIndex();
                skillHUDWasActive = gemSkillHUD.gameObject.activeSelf;
                gemSkillHUD.gameObject.SetActive(true);
                gemSkillHUD.SetAsLastSibling();
            }

            if (hpStatusHUD != null)
            {
                hpStatusHUDWasActive = hpStatusHUD.gameObject.activeSelf;
                hpStatusHUDOriginalSiblingIndex = hpStatusHUD.transform.GetSiblingIndex();
                hpStatusHUD.Show();
                hpStatusHUD.transform.SetAsLastSibling();
            }

            slowMotionObjects.Clear();
            string[] smNames = { "SlowMotionGaugeBackground", "SlowMotionGauge", "SlowMotionGaugeInner", "SlowMotionButton",
                                  "DebugAddGemsButton", "DebugSlotLevelButton" };
            foreach (var smName in smNames)
            {
                var t = canvas.transform.Find(smName);
                if (t != null)
                {
                    slowMotionObjects.Add((t, t.GetSiblingIndex(), t.gameObject.activeSelf));
                    t.gameObject.SetActive(true);
                    t.SetAsLastSibling();
                }
            }
        }

        selectedDrink = null;
        selectedCardObj = null;
        RebuildDrinkIconImages();
        RefreshDrinkCards();
        RefreshDrinkCountDisplay();
        RefreshBuyButtonState();

        yield return StartCoroutine(Fade(1f, 0f));

        if (shopPanel != null && shopPanelFadeInDuration > 0f)
        {
            yield return new WaitForSecondsRealtime(shopPanelFadeInDelay);
            yield return StartCoroutine(FadeShopPanel(0f, 1f));
        }

        isOpening = false;
    }

    private IEnumerator CloseCoroutine()
    {
        isClosing = true;
        PlaySE(closeSE);
        yield return StartCoroutine(Fade(0f, 1f));
        HideAllPanels();

        var canvas2 = GetComponentInParent<Canvas>();
        if (goldHUDOriginalParent != null && canvas2 != null)
        {
            var goldHUD = FindTransformByName(canvas2.transform, "GoldHUD");
            if (goldHUD != null)
            {
                goldHUD.SetParent(goldHUDOriginalParent, true);
                goldHUD.SetSiblingIndex(goldHUDOriginalSiblingIndex);
            }
            goldHUDOriginalParent = null;
        }
        if (skillHUDCanvasAncestor != null)
        {
            skillHUDCanvasAncestor.SetSiblingIndex(skillHUDCanvasAncestorSiblingIndex);
            if (!skillHUDWasActive)
                skillHUDCanvasAncestor.gameObject.SetActive(false);
            skillHUDCanvasAncestor = null;
        }
        if (hpStatusHUD != null)
        {
            hpStatusHUD.transform.SetSiblingIndex(hpStatusHUDOriginalSiblingIndex);
            if (!hpStatusHUDWasActive)
                hpStatusHUD.Hide();
        }
        foreach (var (t, sibIdx, wasActive) in slowMotionObjects)
        {
            t.SetSiblingIndex(sibIdx);
            if (!wasActive) t.gameObject.SetActive(false);
        }
        slowMotionObjects.Clear();

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
        SetHideWhileOpenActive(true);

        // ★AreaSelectのGem/Drinkボタン等はこのパネル表示中もSetActive(false)にならないため、
        //   クリックで拡大・点滅のまま固定(lockedAfterClick)されたButtonHoverEffectが
        //   OnDisable経由で自動的には戻らない。ここで明示的に全て元の見た目へ戻す。
        foreach (var hover in FindObjectsByType<ButtonHoverEffect>(FindObjectsSortMode.None))
        {
            hover.ForceReset();
        }
    }

    /// <summary>
    /// dimPanel（AreaPanelの中身しか暗転できない）が届かない、Canvas直下にある要素
    /// （チュートリアルボタン等）を、ドリンク画面の開閉に合わせて表示/非表示にする。
    /// </summary>
    private void SetHideWhileOpenActive(bool active)
    {
        if (hideWhileOpen == null) return;
        foreach (var go in hideWhileOpen)
        {
            if (go != null) go.SetActive(active);
        }
    }

    private void RebuildDrinkIconImages()
    {
        drinkIconImages.Clear();
        if (drinkIconContainer == null) return;
        foreach (Transform child in drinkIconContainer)
        {
            if (!child.gameObject.activeSelf) continue;
            var img = child.GetComponent<Image>();
            if (img != null) drinkIconImages.Add(img);
        }
    }

    private void RefreshDrinkCountDisplay()
    {
        if (drinkIconImages.Count > 0)
            drinkIconImages[0].color = drinkIconActiveColor;
        if (drinkCountLabel != null)
            drinkCountLabel.text = $"{DrinkSession.PurchaseCount}/{drinkLimit}";
    }

    private void RefreshBuyButtonState()
    {
        if (buyButton == null) return;
        bool withinLimit = DrinkSession.PurchaseCount < drinkLimit;
        bool hasSelection = selectedDrink != null;
        bool hasGold = GoldManager.Instance != null && selectedDrink != null && GoldManager.Instance.PersistentGold >= selectedDrink.price;
        bool wasInteractable = buyButton.interactable;
        buyButton.interactable = withinLimit && hasSelection && hasGold;

        // ★購入直後等、ホバー中のままボタンがinteractable=falseになる場合がある。
        //   ButtonHoverEffectはOnPointerExitが来て初めて拡大・点滅を解除する作りだが、
        //   マウスがボタン上に留まったままだとそのイベントが来ず、拡大したままになってしまう。
        //   interactableがfalseに変化した瞬間、明示的に元の見た目へ戻す。
        //   ★ForceReset()はRestoreColor()でホバー開始時にキャプチャした（明るい）色へ戻すため、
        //   この下で無効時の暗い色を設定するより先に呼ぶ必要がある（後にやると上書きされて枠だけ明るいままになる）。
        if (wasInteractable && !buyButton.interactable)
        {
            var hoverEffect = buyButton.GetComponent<ButtonHoverEffect>();
            if (hoverEffect != null) hoverEffect.ForceReset();
        }

        if (buyBgImage != null)
            buyBgImage.color = withinLimit ? buyBgOriginalColor : buyBgDisabledColor;
        if (buyButtonIcon != null)
            buyButtonIcon.color = withinLimit ? buyIconOriginalColor : buyBgDisabledColor;
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

        // Content の GridLayoutGroup: 3列固定・縦並び
        // カード幅をSeparator幅（shopPanelWidth - VLG左右padding 40px）で3等分して左右端を揃える
        var grid = drinkListContainer.GetComponent<GridLayoutGroup>();
        if (grid != null)
        {
            float separatorWidth    = shopPanelWidth - 40f; // VLG padding left(20) + right(20)
            float spacingX          = grid.spacing.x;
            float adjustedCardWidth = (separatorWidth - (3 - 1) * spacingX) / 3f;
            grid.cellSize        = new Vector2(adjustedCardWidth, cardHeight);
            grid.constraint      = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 3;
            grid.childAlignment  = TextAnchor.UpperLeft;
            grid.padding         = new RectOffset(0, 0, grid.padding.top, grid.padding.bottom);
        }

        // Content を Viewport 全体に伸ばす（ページング表示のためスクロール不要）
        var contentRect = drinkListContainer.GetComponent<RectTransform>();
        if (contentRect != null)
        {
            contentRect.anchorMin        = Vector2.zero;
            contentRect.anchorMax        = Vector2.one;
            contentRect.pivot            = new Vector2(0.5f, 0.5f);
            contentRect.sizeDelta        = Vector2.zero;
            contentRect.anchoredPosition = Vector2.zero;
        }
        var contentFitter = drinkListContainer.GetComponent<ContentSizeFitter>();
        if (contentFitter != null)
        {
            contentFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            contentFitter.verticalFit   = ContentSizeFitter.FitMode.Unconstrained;
        }

        DrinkDefinition[] drinks = Resources.LoadAll<DrinkDefinition>("GameData/Drinks");
        if (drinks == null || drinks.Length == 0)
            return;
        System.Array.Sort(drinks, (a, b) => CompareNatural(a.name, b.name));

        // ScrollRect を縦スクロール無効・横スクロール無効に設定
        var scrollViewTrans = drinkListContainer.parent?.parent;
        if (scrollViewTrans != null)
        {
            var scrollRect = scrollViewTrans.GetComponent<ScrollRect>();
            if (scrollRect != null) { scrollRect.horizontal = false; scrollRect.vertical = false; }

            // ScrollView の高さはPlay前InspectorのLayoutElement.preferredHeightで管理（コード上書き無効）
            // float rowSpacing  = grid != null ? grid.spacing.y : 16f;
            // float scrollViewH = 2f * cardHeight + rowSpacing;
            // var scrollLE = scrollViewTrans.GetComponent<LayoutElement>();
            // if (scrollLE != null) scrollLE.preferredHeight = scrollViewH;

            // Viewport にスワイプハンドラを設定
            var viewportTrans = drinkListContainer.parent;
            if (viewportTrans != null)
            {
                var swipe = viewportTrans.GetComponent<ShopSwipeHandler>();
                if (swipe == null) swipe = viewportTrans.gameObject.AddComponent<ShopSwipeHandler>();
                swipe.Setup(GoToNextPage, GoToPrevPage);
            }
        }

        // ページング初期化
        currentPage = 0;
        totalPages  = Mathf.Max(1, Mathf.CeilToInt((float)drinks.Length / cardsPerPage));
        bool multiPage = totalPages > 1;
        if (prevPageButton != null) prevPageButton.gameObject.SetActive(multiPage);
        if (nextPageButton != null) nextPageButton.gameObject.SetActive(multiPage);
        if (pageDotsContainer != null) pageDotsContainer.gameObject.SetActive(multiPage);

        for (int i = 0; i < drinks.Length; i++)
        {
            DrinkDefinition drink = drinks[i];
            GameObject cardObj = Instantiate(drinkCardTemplate.gameObject, drinkListContainer);
            cardObj.SetActive(true);

            DrinkCardUI cardUI = cardObj.GetComponent<DrinkCardUI>();
            if (cardUI != null)
            {
                cardUI.Populate(drink);

                bool alreadyPurchased = DrinkSession.IsPurchased(drink.name);
                Button btn = cardUI.selectButton != null ? cardUI.selectButton : cardObj.GetComponent<Button>();
                if (btn != null && !alreadyPurchased)
                {
                    DrinkDefinition d = drink;
                    GameObject go = cardObj;
                    btn.onClick.AddListener(() => SelectDrink(d, go));
                }
                cardUI.SetHighlight(false);
                cardUI.SetPurchased(alreadyPurchased);
            }
            drinkCardObjects.Add(cardObj);
        }

        SetupPageDots(totalPages);
        UpdatePage();
    }

    private void SelectDrink(DrinkDefinition drink, GameObject cardObj)
    {
        bool isSameCard = selectedCardObj == cardObj;

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
        if (!isSameCard)
            PlaySE(GetSelectSE());
    }

    // ──────────────────────────────────────────
    // Pagination
    // ──────────────────────────────────────────

    private void SetupPageDots(int count)
    {
        if (pageDotsContainer == null) return;
        foreach (var dot in pageDots) { if (dot != null) Destroy(dot.gameObject); }
        pageDots.Clear();

        for (int i = 0; i < count; i++)
        {
            var dotObj = new GameObject($"Dot_{i}");
            dotObj.transform.SetParent(pageDotsContainer, false);
            dotObj.AddComponent<RectTransform>().sizeDelta = new Vector2(28f, 28f);
            pageDots.Add(dotObj.AddComponent<Image>());
        }
        UpdateDots();
    }

    private void UpdatePage()
    {
        int start = currentPage * cardsPerPage;
        for (int i = 0; i < drinkCardObjects.Count; i++)
            if (drinkCardObjects[i] != null)
                drinkCardObjects[i].SetActive(i >= start && i < start + cardsPerPage);

        UpdateDots();
        if (prevPageButton != null) prevPageButton.interactable = currentPage > 0;
        if (nextPageButton != null) nextPageButton.interactable = currentPage < totalPages - 1;

        Canvas.ForceUpdateCanvases();
        var contentRect = drinkListContainer?.GetComponent<RectTransform>();
        if (contentRect != null) LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);
    }

    private void UpdateDots()
    {
        for (int i = 0; i < pageDots.Count; i++)
            if (pageDots[i] != null)
                pageDots[i].color = (i == currentPage) ? Color.white : new Color(1f, 1f, 1f, 0.3f);
    }

    private void GoToNextPage()
    {
        if (currentPage < totalPages - 1) { currentPage++; ClearSelection(); UpdatePage(); PlaySE(pageButtonSE); }
    }

    private void GoToPrevPage()
    {
        if (currentPage > 0) { currentPage--; ClearSelection(); UpdatePage(); PlaySE(pageButtonSE); }
    }

    private void ApplyPageButtonPressedColor(Button btn, string bgChildName)
    {
        if (btn == null) return;

        // Button自体のImageはAlpha=0のため、PrevBg/NextBg の Image を targetGraphic に設定
        var bgTrans = btn.transform.Find(bgChildName);
        var bgImage = bgTrans != null ? bgTrans.GetComponent<Image>() : null;
        if (bgImage != null)
            btn.targetGraphic = bgImage;

        var cb = btn.colors;
        cb.normalColor    = Color.white;
        cb.pressedColor   = pageButtonPressedColor;
        cb.disabledColor  = new Color(0.5f, 0.5f, 0.5f, 1f);
        cb.colorMultiplier = 1f;
        btn.colors = cb;
    }

    private void ClearSelection()
    {
        selectedDrink    = null;
        selectedCardObj  = null;
        selectedCardUI   = null;
        foreach (var obj in drinkCardObjects)
            obj?.GetComponent<DrinkCardUI>()?.SetHighlight(false);
        RefreshBuyButtonState();
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

        // 同じドリンクは1プレイにつき1回まで（カード側は既に選択不可にしているが、念のため二重チェック）
        if (DrinkSession.IsPurchased(selectedDrink.name)) return;

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

        var actualBoosts = new List<(SkillDefinition skill, int points)>();
        if (selectedDrink.isFixed)
        {
            // 固定: 全スキル確定上昇
            foreach (var skill in candidates)
            {
                DrinkSession.AddBoost(skill.name, selectedDrink.levelUpCount);
                actualBoosts.Add((skill, selectedDrink.levelUpCount));
            }
        }
        else
        {
            // 変動: selectionCount回ランダム選択（重複あり）
            if (candidates.Count > 0)
            {
                for (int i = 0; i < selectedDrink.selectionCount; i++)
                {
                    var skill  = candidates[Random.Range(0, candidates.Count)];
                    int points = Random.Range(selectedDrink.minPoint, selectedDrink.maxPoint + 1);
                    DrinkSession.AddBoost(skill.name, points);
                    actualBoosts.Add((skill, points));
                }
            }
        }

        DrinkSession.IncrementPurchaseCount();
        DrinkSession.MarkPurchased(selectedDrink.name);

        // GemSkillPreviewHUD にドリンクブーストを即時反映（新タイルを点滅）
        var canvas = GetComponentInParent<Canvas>();
        canvas?.transform.Find("GemSkillPreviewHUD")
               ?.GetComponent<GemSkillPreviewHUD>()
               ?.RefreshWithBlink();

        PlaySE(selectedDrink.purchaseSE != null ? selectedDrink.purchaseSE : GetBuySE());
        PlayDrinkEffects(actualBoosts);

        if (selectedCardUI != null)
            selectedCardUI.SetPurchased(true);
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

    /// <summary>数字部分を数値として比較するナチュラルソート（1, 2, 3 ... 10, 11 の順になる）</summary>
    private static int CompareNatural(string a, string b)
    {
        int ia = 0, ib = 0;
        while (ia < a.Length && ib < b.Length)
        {
            if (char.IsDigit(a[ia]) && char.IsDigit(b[ib]))
            {
                int ea = ia, eb = ib;
                while (ea < a.Length && char.IsDigit(a[ea])) ea++;
                while (eb < b.Length && char.IsDigit(b[eb])) eb++;
                int na = int.Parse(a.Substring(ia, ea - ia));
                int nb = int.Parse(b.Substring(ib, eb - ib));
                int c = na.CompareTo(nb);
                if (c != 0) return c;
                ia = ea; ib = eb;
            }
            else
            {
                int c = a[ia].CompareTo(b[ib]);
                if (c != 0) return c;
                ia++; ib++;
            }
        }
        return a.Length.CompareTo(b.Length);
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

    private IEnumerator FadeShopPanel(float from, float to)
    {
        if (shopPanel == null || shopPanelFadeInDuration <= 0f) yield break;
        var cg = shopPanel.GetComponent<CanvasGroup>();
        if (cg == null) yield break;
        float elapsed = 0f;
        while (elapsed < shopPanelFadeInDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            cg.alpha = Mathf.Lerp(from, to, elapsed / shopPanelFadeInDuration);
            yield return null;
        }
        cg.alpha = to;
    }

    // ========== Drink Effect Animations ==========

    private void PlayDrinkEffects(List<(SkillDefinition skill, int points)> boosts)
    {
        StartCoroutine(PopLabelCoroutine());
        if (drinkIconImages.Count > 0)
            StartCoroutine(ShakeIconCoroutine(drinkIconImages[0].rectTransform));
        StartCoroutine(VignetteFlashCoroutine());

        string effectText = BuildEffectText(boosts);
        if (!string.IsNullOrEmpty(effectText) && drinkIconContainer != null)
            StartCoroutine(FloatTextCoroutine(effectText, drinkIconContainer));
    }

    private string BuildEffectText(List<(SkillDefinition skill, int points)> boosts)
    {
        if (boosts == null || boosts.Count == 0) return "";
        // 同じスキルへの加算をまとめる
        var totals = new System.Collections.Generic.Dictionary<string, (SkillDefinition sd, int total)>();
        foreach (var (skill, pts) in boosts)
        {
            if (skill == null) continue;
            if (totals.TryGetValue(skill.name, out var existing))
                totals[skill.name] = (skill, existing.total + pts);
            else
                totals[skill.name] = (skill, pts);
        }
        var sb = new System.Text.StringBuilder();
        foreach (var kv in totals.Values)
        {
            if (kv.total == 0) continue;
            if (sb.Length > 0) sb.Append("\n");
            sb.Append($"{kv.sd.GetLocalizedName()}  +{kv.total}");
        }
        return sb.ToString();
    }

    private IEnumerator PopLabelCoroutine()
    {
        if (drinkCountLabel == null) yield break;
        var t = drinkCountLabel.transform;
        Color origColor = drinkCountLabel.color;
        float elapsed = 0f;

        while (elapsed < popScaleUpDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float p = elapsed / popScaleUpDuration;
            t.localScale = Vector3.one * Mathf.Lerp(1f, popScaleMax, p);
            drinkCountLabel.color = Color.Lerp(origColor, popFlashColor, p);
            yield return null;
        }
        elapsed = 0f;
        while (elapsed < popScaleDownDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float p = elapsed / popScaleDownDuration;
            t.localScale = Vector3.one * Mathf.Lerp(popScaleMax, 1f, p);
            drinkCountLabel.color = Color.Lerp(popFlashColor, origColor, p);
            yield return null;
        }
        t.localScale = Vector3.one;
        drinkCountLabel.color = origColor;
    }

    private IEnumerator ShakeIconCoroutine(RectTransform rt)
    {
        if (rt == null) yield break;
        Vector3 origin = rt.localPosition;
        float elapsed = 0f;
        while (elapsed < shakeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / shakeDuration;
            float dampen = 1f - t;
            float offsetX = Mathf.Sin(elapsed * 60f) * shakeAmount * dampen;
            rt.localPosition = origin + new Vector3(offsetX, 0f, 0f);
            yield return null;
        }
        rt.localPosition = origin;
    }

    private IEnumerator FloatTextCoroutine(string text, Transform anchor)
    {
        var canvas = GetComponentInParent<Canvas>();
        if (canvas == null) yield break;

        var obj = new GameObject("DrinkFloatText");
        obj.transform.SetParent(canvas.transform, false);

        var rt = obj.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(400f, 160f);
        rt.anchoredPosition = floatTextStartPos;

        var tmp = obj.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = floatTextFontSize;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = floatTextColor;
        tmp.enableWordWrapping = true;
        if (drinkCountLabel != null && drinkCountLabel.font != null)
            tmp.font = drinkCountLabel.font;

        float startY = rt.anchoredPosition.y;
        float elapsed = 0f;
        float fadeRange = 1f - floatTextFadeStartRatio;
        while (elapsed < floatTextDuration)
        {
            if (obj == null) yield break;
            elapsed += Time.unscaledDeltaTime;
            float p = elapsed / floatTextDuration;
            rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, startY + floatTextRise * p);
            float alpha = p < floatTextFadeStartRatio ? 1f
                : Mathf.Lerp(1f, 0f, (p - floatTextFadeStartRatio) / fadeRange);
            tmp.color = new Color(floatTextColor.r, floatTextColor.g, floatTextColor.b, alpha);
            yield return null;
        }
        if (obj != null) Destroy(obj);
    }

    private IEnumerator VignetteFlashCoroutine()
    {
        var canvas = GetComponentInParent<Canvas>();
        if (canvas == null) yield break;

        var obj = new GameObject("DrinkFlashOverlay");
        obj.transform.SetParent(canvas.transform, false);
        obj.transform.SetAsLastSibling();

        var rt = obj.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var img = obj.AddComponent<Image>();
        img.color = new Color(flashColor.r, flashColor.g, flashColor.b, 0f);
        img.raycastTarget = false;

        float half = flashDuration * 0.4f;
        float elapsed = 0f;
        while (elapsed < half)
        {
            elapsed += Time.unscaledDeltaTime;
            img.color = new Color(flashColor.r, flashColor.g, flashColor.b, flashColor.a * (elapsed / half));
            yield return null;
        }
        elapsed = 0f;
        float fadeOut = flashDuration * 0.6f;
        while (elapsed < fadeOut)
        {
            elapsed += Time.unscaledDeltaTime;
            img.color = new Color(flashColor.r, flashColor.g, flashColor.b, flashColor.a * (1f - elapsed / fadeOut));
            yield return null;
        }
        Destroy(obj);
    }

#if UNITY_EDITOR
    [ContextMenu("Show Shop Panel for Editing")]
    private void ShowShopPanelForEditing()
    {
        if (shopPanel == null) { Debug.LogWarning("[ShopUI] shopPanel が未設定です。"); return; }
        shopPanel.SetActive(true);

        // NavRow が既存の場合は再生成しない（PrevBg/NextBg等の手動カスタマイズを保護）
        var navRowTrans = shopPanel.transform.Find("NavRow");
        if (navRowTrans == null)
        {
            var navObj = new GameObject("NavRow");
            navObj.transform.SetParent(shopPanel.transform, false);
            navObj.transform.SetAsLastSibling();
            navObj.AddComponent<LayoutElement>().preferredHeight = navRowHeight;
            var navHLG = navObj.AddComponent<HorizontalLayoutGroup>();
            navHLG.childAlignment       = TextAnchor.MiddleCenter;
            navHLG.childControlWidth    = false;
            navHLG.childControlHeight   = true;
            navHLG.childForceExpandHeight = true;
            navHLG.spacing              = 20f;

            var prevBtnObj = new GameObject("PrevButton");
            prevBtnObj.transform.SetParent(navObj.transform, false);
            prevBtnObj.AddComponent<RectTransform>().sizeDelta = navButtonSize;
            prevBtnObj.AddComponent<Image>().color = new Color(0.2f, 0.2f, 0.3f, 1f);
            prevBtnObj.AddComponent<Button>();
            var prevTextObj = new GameObject("Text");
            prevTextObj.transform.SetParent(prevBtnObj.transform, false);
            var prevTextRect = prevTextObj.AddComponent<RectTransform>();
            prevTextRect.anchorMin = Vector2.zero; prevTextRect.anchorMax = Vector2.one; prevTextRect.sizeDelta = Vector2.zero;
            var prevTMP = prevTextObj.AddComponent<TextMeshProUGUI>();
            prevTMP.text = "＜"; prevTMP.fontSize = 28f; prevTMP.alignment = TextAlignmentOptions.Center; prevTMP.color = Color.white;

            var dotsObj = new GameObject("DotsContainer");
            dotsObj.transform.SetParent(navObj.transform, false);
            dotsObj.AddComponent<RectTransform>().sizeDelta = new Vector2(160f, navButtonSize.y);
            var dotsHLG = dotsObj.AddComponent<HorizontalLayoutGroup>();
            dotsHLG.childAlignment    = TextAnchor.MiddleCenter;
            dotsHLG.childControlWidth  = false;
            dotsHLG.childControlHeight = false;
            dotsHLG.spacing            = 8f;

            var nextBtnObj = new GameObject("NextButton");
            nextBtnObj.transform.SetParent(navObj.transform, false);
            nextBtnObj.AddComponent<RectTransform>().sizeDelta = navButtonSize;
            nextBtnObj.AddComponent<Image>().color = new Color(0.2f, 0.2f, 0.3f, 1f);
            nextBtnObj.AddComponent<Button>();
            var nextTextObj = new GameObject("Text");
            nextTextObj.transform.SetParent(nextBtnObj.transform, false);
            var nextTextRect = nextTextObj.AddComponent<RectTransform>();
            nextTextRect.anchorMin = Vector2.zero; nextTextRect.anchorMax = Vector2.one; nextTextRect.sizeDelta = Vector2.zero;
            var nextTMP = nextTextObj.AddComponent<TextMeshProUGUI>();
            nextTMP.text = "＞"; nextTMP.fontSize = 28f; nextTMP.alignment = TextAlignmentOptions.Center; nextTMP.color = Color.white;

            // SerializedObject で参照を保存
            var so = new UnityEditor.SerializedObject(this);
            so.Update();
            so.FindProperty("prevPageButton").objectReferenceValue = prevBtnObj.GetComponent<Button>();
            so.FindProperty("nextPageButton").objectReferenceValue = nextBtnObj.GetComponent<Button>();
            so.FindProperty("pageDotsContainer").objectReferenceValue = dotsObj.transform;
            so.ApplyModifiedProperties();

            Debug.Log("[ShopUI] NavRow を新規生成しました。");
        }
        else
        {
            Debug.Log("[ShopUI] NavRow が既存のため再生成をスキップしました（手動カスタマイズを保護）。");
        }

        UnityEditor.EditorUtility.SetDirty(shopPanel);
        Debug.Log("[ShopUI] ShopPanel を表示しました（Play前Inspector調整用）。調整後は「Hide Shop Panel」で非表示に戻してください。");
    }

    [ContextMenu("Hide Shop Panel")]
    private void HideShopPanelForEditing()
    {
        if (shopPanel == null) { Debug.LogWarning("[ShopUI] shopPanel が未設定です。"); return; }
        shopPanel.SetActive(false);
        UnityEditor.EditorUtility.SetDirty(shopPanel);
        Debug.Log("[ShopUI] ShopPanel を非表示にしました。");
    }

    /// <summary>
    /// 購入/閉じるボタンを、GemManagementUIと同じ「ネオン枠 + アイコン単体画像」の2層構成に作り直す。
    /// ボタン本体・BuyBg/CloseBgを正方形に近い比率へリサイズしてから枠画像を差し替える。
    /// 既存のButtonHoverEffect（ホバー点滅）には触れない（対象Imageの色を奪い合わないため）。
    /// </summary>
    [ContextMenu("Rebuild Action Button Visuals (購入/閉じるをネオン枠+アイコンに作り直す)")]
    private void RebuildActionButtonVisuals()
    {
        neonFrameSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/AreaSelect/Shop/新ネオン枠.png");
        if (buyIconSprite == null)
            buyIconSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/AreaSelect/ドリンクアイコン.png");
        if (exitIconSprite == null)
            exitIconSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/AreaSelect/Shop/EXITアイコン.png");

        if (neonFrameSprite == null)
        {
            Debug.LogError("[ShopUI] 新ネオン枠.pngが見つかりません。");
            return;
        }

        // ★buyBgImageが未アサインだと購入ボタンの処理が丸ごとスキップされてしまうため、
        //   ここで直接子の"BuyBg"から自動補完する（AutoReconnectReferencesと同じ探索方法）。
        if (buyBgImage == null && buyButton != null)
        {
            var t = buyButton.transform.Find("BuyBg");
            if (t != null) buyBgImage = t.GetComponent<Image>();
        }

        // ★ボタン本体・枠(Bg)のサイズは初回セットアップ時に一度リサイズしただけで、
        //   その後手動で細かく調整されている想定のため、再実行時はサイズ・位置を一切上書きしない。
        //   画像(sprite)の差し替えだけ毎回行う。

        // 購入ボタン
        if (buyButton != null && buyBgImage != null)
        {
            buyBgImage.sprite = neonFrameSprite;
            buyBgImage.type = Image.Type.Simple;
            buyBgImage.preserveAspect = false;

            buyButtonIcon = SetupActionIcon(buyButton.transform, "Icon", buyIconSprite, actionIconSize);

            // ★BuyButtonのButtonHoverEffectがBuyBgと同じ画像を点滅対象にしているため、
            //   購入不可(interactable=false)の間はホバー点滅を止めて、RefreshBuyButtonState()の
            //   暗色設定と競合しないようにする。
            var hoverEffect = buyButton.GetComponent<ButtonHoverEffect>();
            if (hoverEffect != null)
            {
                var so = new UnityEditor.SerializedObject(hoverEffect);
                so.FindProperty("requireInteractable").boolValue = true;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }
        else
        {
            Debug.LogWarning("[ShopUI] buyButton/buyBgImageが未設定のためスキップしました。");
        }

        // 閉じるボタン（実際の枠画像は子の"CloseBg"にある）
        if (closeButton != null)
        {
            var closeBg = closeButton.transform.Find("CloseBg");
            var closeImg = closeBg != null ? closeBg.GetComponent<Image>() : closeButton.GetComponent<Image>();
            if (closeImg != null)
            {
                closeImg.sprite = neonFrameSprite;
                closeImg.type = Image.Type.Simple;
                closeImg.preserveAspect = false;
                closeImg.color = Color.white;
            }
            else
            {
                Debug.LogWarning("[ShopUI] CloseButtonの枠Imageが見つかりませんでした（CloseBg子/ルートImageともに無し）。");
            }

            closeButtonIcon = SetupActionIcon(closeButton.transform, "Icon", exitIconSprite, actionIconSize);

            var existingText = closeButton.transform.Find("Text");
            if (existingText != null) existingText.gameObject.SetActive(false);
        }
        else
        {
            Debug.LogWarning("[ShopUI] closeButtonが未設定のためスキップしました。");
        }

        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log("[ShopUI] Action button visuals rebuilt (neon frame + icon). サイズ・位置は変更していません（画像の差し替えのみ）。");
    }

    // ★childNameのアイコンが既に存在する場合、サイズ・位置は一切変更しない（手動調整を保護するため）。
    //   新規作成の時だけデフォルトのサイズ・中央位置を設定する。
    private Image SetupActionIcon(Transform buttonTransform, string childName, Sprite iconSprite, float size)
    {
        var existing = buttonTransform.Find(childName);
        bool isNew = existing == null;
        GameObject iconGo = isNew ? new GameObject(childName, typeof(RectTransform), typeof(Image)) : existing.gameObject;
        var rt = (RectTransform)iconGo.transform;

        if (isNew)
        {
            rt.SetParent(buttonTransform, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(size, size);
            rt.anchoredPosition = Vector2.zero;
        }

        var img = iconGo.GetComponent<Image>();
        img.sprite = iconSprite;
        img.preserveAspect = true;
        img.raycastTarget = false;
        return img;
    }

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
        so.FindProperty("prevPageButton").objectReferenceValue = prevPageButton;
        so.FindProperty("nextPageButton").objectReferenceValue = nextPageButton;
        so.FindProperty("pageDotsContainer").objectReferenceValue = (UnityEngine.Object)pageDotsContainer;
        so.ApplyModifiedProperties();
        UnityEditor.EditorUtility.SetDirty(gameObject);
        Debug.Log("[ShopUI] Setup Shop Panel (Rebuild) 完了。ShopButton から Open() を呼んで確認してください。");
    }

    /// <summary>GemManagementUI の CreateSkillRow と同じ構造でスキル行を生成する。</summary>
    private static void CreateDrinkSkillRow(string rowName, Transform parent)
    {
        const float skillIconSize    = 40f;
        const float iconColumnWidth  = 90f;

        var rowObj = new GameObject(rowName);
        rowObj.transform.SetParent(parent, false);
        rowObj.AddComponent<Image>().color = new Color(0.15f, 0.15f, 0.15f, 0.5f);

        var rowLayout = rowObj.AddComponent<HorizontalLayoutGroup>();
        rowLayout.spacing              = 6f;
        rowLayout.childAlignment       = TextAnchor.MiddleLeft;
        rowLayout.childControlWidth    = true;
        rowLayout.childControlHeight   = true;
        rowLayout.childForceExpandWidth  = false;
        rowLayout.childForceExpandHeight = false;

        var rowLE = rowObj.AddComponent<LayoutElement>();
        rowLE.preferredHeight = skillIconSize;

        // アイコン列コンテナ（固定幅でテキスト開始位置を統一）
        var iconContainerObj = new GameObject("IconContainer");
        iconContainerObj.transform.SetParent(rowObj.transform, false);
        var icLE = iconContainerObj.AddComponent<LayoutElement>();
        icLE.minWidth       = iconColumnWidth;
        icLE.preferredWidth  = iconColumnWidth;
        icLE.preferredHeight = skillIconSize;

        var iconObj = new GameObject("IconImage");
        iconObj.transform.SetParent(iconContainerObj.transform, false);
        var iconRect = iconObj.AddComponent<RectTransform>();
        iconRect.anchorMin        = new Vector2(0.5f, 0.5f);
        iconRect.anchorMax        = new Vector2(0.5f, 0.5f);
        iconRect.pivot            = new Vector2(0.5f, 0.5f);
        iconRect.sizeDelta        = new Vector2(skillIconSize, skillIconSize);
        iconRect.anchoredPosition = Vector2.zero;
        iconObj.AddComponent<Image>().color = new Color(0.3f, 0.3f, 0.3f, 0.5f);

        // スキル名テキスト
        var nameObj = new GameObject("SkillName");
        nameObj.transform.SetParent(rowObj.transform, false);
        var nameTMP = nameObj.AddComponent<TextMeshProUGUI>();
        nameTMP.text               = "";
        nameTMP.fontSize           = 20f;
        nameTMP.fontStyle          = FontStyles.Bold;
        nameTMP.color              = Color.black;
        nameTMP.alignment          = TextAlignmentOptions.MidlineLeft;
        nameTMP.enableWordWrapping = false;
        nameObj.AddComponent<LayoutElement>().flexibleWidth = 1f;

#if UNITY_EDITOR
        var skillRowFont = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/NotoSerifCJKjp-Regular SDF.asset");
        if (skillRowFont != null) nameTMP.font = skillRowFont;
#endif
    }

    /// <summary>HeaderRow/TitleText をアイコンコンテナに置き換えて drinkLimit 個のアイコンを生成。</summary>
    [ContextMenu("Setup Drink Count Icons")]
    private void SetupDrinkCountIcons()
    {
        if (shopPanel == null) { Debug.LogError("[ShopUI] shopPanel が未設定です"); return; }
        var headerRow = shopPanel.transform.Find("HeaderRow");
        if (headerRow == null) { Debug.LogError("[ShopUI] HeaderRow が見つかりません"); return; }

        // 既存の TitleText を削除
        var existing = headerRow.Find("TitleText");
        if (existing != null) DestroyImmediate(existing.gameObject);

        // アイコンコンテナを TitleText として作成
        var containerObj = new GameObject("TitleText");
        containerObj.transform.SetParent(headerRow, false);
        var containerRect = containerObj.AddComponent<RectTransform>();
        containerRect.anchorMin = Vector2.zero;
        containerRect.anchorMax = Vector2.one;
        containerRect.offsetMin = Vector2.zero;
        containerRect.offsetMax = new Vector2(-336f, 0f);
        var hlg = containerObj.AddComponent<HorizontalLayoutGroup>();
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.spacing = 8f;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;
        drinkIconContainer = containerRect;

        // drinkLimit 個のアイコンを生成
        for (int i = 0; i < drinkLimit; i++)
        {
            var iconObj = new GameObject($"DrinkIcon_{i}");
            iconObj.transform.SetParent(containerObj.transform, false);
            var rect = iconObj.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(48f, 48f);
            var img = iconObj.AddComponent<Image>();
            img.sprite = drinkCountIcon;
            img.color = drinkIconInactiveColor;
        }

#if UNITY_EDITOR
        var so = new UnityEditor.SerializedObject(this);
        so.Update();
        so.FindProperty("drinkIconContainer").objectReferenceValue = drinkIconContainer;
        so.ApplyModifiedProperties();
        UnityEditor.EditorUtility.SetDirty(this);
#endif
        Debug.Log($"[ShopUI] Drink Count Icons セットアップ完了: {drinkLimit} 個");
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

        // FlavorTextContainer（DrinkIconの右側: X200〜404, Y56〜296 from top）
        var flavorContObj = new GameObject("FlavorTextContainer");
        flavorContObj.transform.SetParent(root.transform, false);
        var flavorContRect = flavorContObj.AddComponent<RectTransform>();
        flavorContRect.anchorMin = Vector2.zero;
        flavorContRect.anchorMax = Vector2.one;
        flavorContRect.offsetMin = new Vector2(200f, 144f); // left=200, bottom=144(SkillsContainerの上)
        flavorContRect.offsetMax = new Vector2(-16f, -56f); // right=16, top=56(NamePriceRowの下)
        flavorContObj.AddComponent<Image>().color = new Color(0.14f, 0.14f, 0.20f, 0.75f);

        var flavorTextObj = new GameObject("FlavorText");
        flavorTextObj.transform.SetParent(flavorContObj.transform, false);
        var flavorTextRect = flavorTextObj.AddComponent<RectTransform>();
        flavorTextRect.anchorMin = Vector2.zero;
        flavorTextRect.anchorMax = Vector2.one;
        flavorTextRect.offsetMin = new Vector2(8f, 8f);
        flavorTextRect.offsetMax = new Vector2(-8f, -8f);
        var flavorTMP = flavorTextObj.AddComponent<TextMeshProUGUI>();
        flavorTMP.text               = "フレーバーテキスト";
        flavorTMP.fontSize           = 16f;
        flavorTMP.color              = new Color(0.85f, 0.85f, 0.92f, 1f);
        flavorTMP.alignment          = TextAlignmentOptions.TopLeft;
        flavorTMP.enableWordWrapping = true;
        flavorTMP.fontStyle          = FontStyles.Italic;

#if UNITY_EDITOR
        var flavorFont = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/NotoSerifCJKjp-Regular SDF.asset");
        if (flavorFont != null) flavorTMP.font = flavorFont;
#endif

        // SkillsContainer（DrinkIconの下、カード下部に配置）
        var skillsContObj = new GameObject("SkillsContainer");
        skillsContObj.transform.SetParent(root.transform, false);
        var skillsContRect = skillsContObj.AddComponent<RectTransform>();
        skillsContRect.anchorMin       = new Vector2(0f, 0f);
        skillsContRect.anchorMax       = new Vector2(1f, 0f);
        skillsContRect.pivot           = new Vector2(0.5f, 0f);
        skillsContRect.anchoredPosition = new Vector2(0f, 8f);
        skillsContRect.sizeDelta       = new Vector2(-16f, 136f);
        var skillsVLG = skillsContObj.AddComponent<VerticalLayoutGroup>();
        skillsVLG.spacing              = 4f;
        skillsVLG.childAlignment       = TextAnchor.UpperLeft;
        skillsVLG.childControlWidth    = true;
        skillsVLG.childControlHeight   = true;
        skillsVLG.childForceExpandWidth  = true;
        skillsVLG.childForceExpandHeight = false;

        for (int si = 1; si <= 3; si++)
            CreateDrinkSkillRow($"SkillRow_{si}", skillsContObj.transform);

        // DrinkCardUI の参照は Awake/ReconnectReferences で入る。Inspector 用にアサイン
        var soCard = new UnityEditor.SerializedObject(cardUI);
        soCard.Update();
        soCard.FindProperty("drinkNameText").objectReferenceValue = nameTMP;
        soCard.FindProperty("goldIconImage").objectReferenceValue = goldIconObj.GetComponent<Image>();
        soCard.FindProperty("priceText").objectReferenceValue = priceTMP;
        soCard.FindProperty("drinkIconImage").objectReferenceValue = drinkIconImg;
        soCard.FindProperty("flavorText").objectReferenceValue = flavorTMP;
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
        var panelImg = panelObj.AddComponent<Image>();
        panelImg.color   = new Color(0.08f, 0.08f, 0.12f, 0.97f);
        panelImg.enabled = false;

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
        headerObj.AddComponent<LayoutElement>().preferredHeight = 72f;

        var titleObj = new GameObject("TitleText");
        titleObj.transform.SetParent(headerObj.transform, false);
        var titleRect = titleObj.AddComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 0f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.offsetMin = Vector2.zero;
        titleRect.offsetMax = new Vector2(-336f, 0f);
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
        buyBtnRect.sizeDelta = new Vector2(160f, 72f);
        buyBtnRect.anchoredPosition = new Vector2(-253f, 0f);
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
        buyBtnTMP.fontSize = 24f;
        buyBtnTMP.alignment = TextAlignmentOptions.Center;
        buyBtnTMP.color = Color.white;

        var closeBtnObj = new GameObject("CloseButton");
        closeBtnObj.transform.SetParent(headerObj.transform, false);
        var closeBtnRect = closeBtnObj.AddComponent<RectTransform>();
        closeBtnRect.anchorMin = new Vector2(1f, 0.5f);
        closeBtnRect.anchorMax = new Vector2(1f, 0.5f);
        closeBtnRect.sizeDelta = new Vector2(160f, 72f);
        closeBtnRect.anchoredPosition = new Vector2(-85f, 0f);
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
        closeBtnTMP.fontSize = 24f;
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

        // ── NavRow（ページング操作）──
        var navObj = new GameObject("NavRow");
        navObj.transform.SetParent(panelObj.transform, false);
        navObj.AddComponent<LayoutElement>().preferredHeight = navRowHeight;
        var navHLG = navObj.AddComponent<HorizontalLayoutGroup>();
        navHLG.childAlignment       = TextAnchor.MiddleCenter;
        navHLG.childControlWidth    = false;
        navHLG.childControlHeight   = true;
        navHLG.childForceExpandHeight = true;
        navHLG.spacing              = 20f;

        // PrevButton
        var prevBtnObj = new GameObject("PrevButton");
        prevBtnObj.transform.SetParent(navObj.transform, false);
        prevBtnObj.AddComponent<RectTransform>().sizeDelta = navButtonSize;
        prevBtnObj.AddComponent<Image>().color = new Color(0.2f, 0.2f, 0.3f, 1f);
        prevPageButton = prevBtnObj.AddComponent<Button>();
        var prevTextObj = new GameObject("Text");
        prevTextObj.transform.SetParent(prevBtnObj.transform, false);
        var prevTextRect = prevTextObj.AddComponent<RectTransform>();
        prevTextRect.anchorMin = Vector2.zero; prevTextRect.anchorMax = Vector2.one; prevTextRect.sizeDelta = Vector2.zero;
        var prevTMP = prevTextObj.AddComponent<TextMeshProUGUI>();
        prevTMP.text = "＜"; prevTMP.fontSize = 28f; prevTMP.alignment = TextAlignmentOptions.Center; prevTMP.color = Color.white;

        // DotsContainer
        var dotsObj = new GameObject("DotsContainer");
        dotsObj.transform.SetParent(navObj.transform, false);
        dotsObj.AddComponent<RectTransform>().sizeDelta = new Vector2(160f, navButtonSize.y);
        var dotsHLG = dotsObj.AddComponent<HorizontalLayoutGroup>();
        dotsHLG.childAlignment    = TextAnchor.MiddleCenter;
        dotsHLG.childControlWidth  = false;
        dotsHLG.childControlHeight = false;
        dotsHLG.spacing            = 8f;
        pageDotsContainer = dotsObj.transform;

        // NextButton
        var nextBtnObj = new GameObject("NextButton");
        nextBtnObj.transform.SetParent(navObj.transform, false);
        nextBtnObj.AddComponent<RectTransform>().sizeDelta = navButtonSize;
        nextBtnObj.AddComponent<Image>().color = new Color(0.2f, 0.2f, 0.3f, 1f);
        nextPageButton = nextBtnObj.AddComponent<Button>();
        var nextTextObj = new GameObject("Text");
        nextTextObj.transform.SetParent(nextBtnObj.transform, false);
        var nextTextRect = nextTextObj.AddComponent<RectTransform>();
        nextTextRect.anchorMin = Vector2.zero; nextTextRect.anchorMax = Vector2.one; nextTextRect.sizeDelta = Vector2.zero;
        var nextTMP = nextTextObj.AddComponent<TextMeshProUGUI>();
        nextTMP.text = "＞"; nextTMP.fontSize = 28f; nextTMP.alignment = TextAlignmentOptions.Center; nextTMP.color = Color.white;

        drinkListContainer = contentObj.transform;
        shopPanel = panelObj;
        panelObj.SetActive(false);
    }
#endif
}

/// <summary>ショップViewportにアタッチしてスワイプでページ切り替えを行うハンドラ</summary>
public class ShopSwipeHandler : MonoBehaviour, IBeginDragHandler, IEndDragHandler
{
    [SerializeField] private float swipeThreshold = 50f;
    private System.Action onSwipeLeft;
    private System.Action onSwipeRight;
    private Vector2 dragStartPos;

    public void Setup(System.Action swipeLeft, System.Action swipeRight)
    {
        onSwipeLeft  = swipeLeft;
        onSwipeRight = swipeRight;
    }

    public void OnBeginDrag(PointerEventData eventData) => dragStartPos = eventData.position;

    public void OnEndDrag(PointerEventData eventData)
    {
        float diff = eventData.position.x - dragStartPos.x;
        if (diff < -swipeThreshold) onSwipeLeft?.Invoke();
        else if (diff > swipeThreshold) onSwipeRight?.Invoke();
    }
}
