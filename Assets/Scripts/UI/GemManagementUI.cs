using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Game.Progress;
using Game.Gems;
using Game.Skills;
using Game.UI;

/// <summary>
/// AreaSelectシーンでジェムの管理（装備/解除/売却）を行うオーバーレイUI
/// PauseMenuUI方式：[ContextMenu("Setup Gem Management UI")] でHierarchyを自動生成
/// AreaSelectにあるボタンの onClick から Open() を呼ぶ
/// </summary>
public class GemManagementUI : MonoBehaviour
{
    // ★警告行の高さ。AddLowUsesWarningText()のLayoutElementとUpdateGridSettings()の
    //   panelH計算の両方で使うため、値のズレが起きないよう定数化する。
    private const float LowUsesWarningRowHeight = 36f;

    [Header("Panel References")]
    [SerializeField] private GameObject dimPanel;
    [SerializeField] private GameObject gemPanel;
    [Tooltip("ジェム画面を開いている間、一緒に非表示にしたい他画面のボタン（チュートリアルボタン等）。dimPanelの外側にあり暗転で隠せない要素向け")]
    [SerializeField] private GameObject[] hideWhileOpen;

    [Header("Header")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI slotLevelText;   // "スロット使用: 2 / 5"
    [SerializeField] private Button closeButton;

    [Header("Gem List")]
    [SerializeField] private Transform gemListContainer;      // ScrollView > Viewport > Content
    [SerializeField] private GameObject gemItemTemplate;      // 非表示テンプレートオブジェクト

    [Header("Empty Message")]
    [SerializeField] private TextMeshProUGUI emptyMessageText;

    [Header("Display Settings")]
    [SerializeField] private string slotDisplayFormat = "スロット×{0}";
    [SerializeField] private float skillIconSize = 40f;
    [Tooltip("アイコン列の固定幅（全スキルでテキスト開始位置を揃える）\n最大アイコン幅(80px)より少し大きい値を設定")]
    [SerializeField] private float iconColumnWidth = 90f;
    [Tooltip("スキル行の統一高さ（最大アイコン高さ=A10の70pxを基準）\nアイコン画像サイズはそのまま、行間を均一にする")]
    [SerializeField] private float skillRowHeight = 70f;
    [Tooltip("スキル行の横幅（0 = 親コンテナいっぱいに伸ばす）\n背景色の横幅を短くしたい場合に設定")]
    [SerializeField] private float skillRowWidth = 0f;
    [Tooltip("ジェム枠の横幅（0 = パネルいっぱいに伸ばす）\nグレー背景の横幅を狭くしたい場合に設定")]
    [SerializeField] private float gemItemWidth = 0f;

    [Header("Uses (使用回数)")]
    [Tooltip("残り使用回数がこの値以下になったら警告色・警告テキストを表示する")]
    [SerializeField] private int lowUsesThreshold = 3;
    [Tooltip("残り使用回数が十分にある時のバッジ文字色")]
    [SerializeField] private Color normalUsesBadgeColor = Color.white;
    [Tooltip("残り使用回数が少ない時のバッジ文字色")]
    [SerializeField] private Color lowUsesBadgeColor = new Color(1f, 0.3f, 0.3f, 1f);
    [Tooltip("ジェム選択時、残り使用回数が少ない場合に表示する警告テキスト")]
    [SerializeField] private TextMeshProUGUI lowUsesWarningText;
    [Tooltip("警告テキストのフォーマット。{0}=残り回数")]
    [SerializeField] private string lowUsesWarningFormat = "このジェムは残り{0}回で消滅します";
    [Tooltip("警告アイコン＋テキストをまとめて表示/非表示・拡大縮小させる親（Add Low Uses Warning Textで自動生成）")]
    [SerializeField] private GameObject lowUsesWarningContainer;
    [Tooltip("警告テキストの左に表示する警告アイコン（Add Low Uses Warning Textで自動生成）")]
    [SerializeField] private Image lowUsesWarningIcon;
    [Tooltip("警告の表示/非表示をalphaで切り替えるCanvasGroup（Add Low Uses Warning Textで自動生成）。\nこれによりcontainer自体は常にActiveのままレイアウト上の縦幅を確保し続け、警告の出現/消滅で下の装備欄がズレなくなる")]
    [SerializeField] private CanvasGroup lowUsesWarningCanvasGroup;
    [Tooltip("点滅時にブレンドする色（lowUsesBadgeColorからこの色へ往復する）")]
    [SerializeField] private Color lowUsesBlinkColor = Color.white;
    [Tooltip("点滅速度（1秒あたりのサイクル数）")]
    [SerializeField] private float lowUsesBlinkSpeed = 2.5f;
    [Tooltip("拡大縮小パルスの振れ幅（0.1なら90%〜110%の間で変化）")]
    [SerializeField] private float lowUsesPulseAmount = 0.12f;
    [Tooltip("拡大縮小パルスの速さ（1秒あたりのサイクル数）")]
    [SerializeField] private float lowUsesPulseSpeed = 2f;

    private Coroutine lowUsesWarningPulseCoroutine;

    [Header("Grid Layout")]
    [Tooltip("横に並べるジェム枠の列数")]
    [SerializeField] private int gemGridColumns = 4;
    [Tooltip("ジェム枠のグリッド間隔（横/縦）")]
    [SerializeField] private Vector2 gemGridSpacing = new Vector2(8f, 8f);
    [Tooltip("ジェム枠の固定高さ（スキル3行分＋余白の目安：270px）")]
    [SerializeField] private float gemGridCellHeight = 270f;
    [Tooltip("HUDの横幅（px）。パネルをHUDを除いた領域の中央に配置する。0=画面中央")]
    [SerializeField] private float hudWidth = 0f;

    [Header("Skill Category Colors")]
    [Tooltip("スキル行の背景グラデーション開始色 - CategoryA（左端）")]
    [SerializeField] private Color categoryAColor = new Color(0.15f, 0.25f, 0.45f, 0.85f);
    [Tooltip("スキル行の背景グラデーション開始色 - CategoryB（左端）")]
    [SerializeField] private Color categoryBColor = new Color(0.15f, 0.38f, 0.22f, 0.85f);
    [Tooltip("スキル行の背景グラデーション開始色 - CategoryC（左端）")]
    [SerializeField] private Color categoryCColor = new Color(0.45f, 0.18f, 0.15f, 0.85f);
    [Tooltip("スキル行の背景グラデーション終端色（右端）\n全カテゴリ共通")]
    [SerializeField] private Color gradientEndColor = new Color(0.05f, 0.05f, 0.08f, 0.7f);
    [Tooltip("ONにするとグラデーション、OFFにするとカテゴリカラーの単色")]
    [SerializeField] private bool useGradientBackground = true;

    [Header("Open Fade")]
    [Tooltip("画面全体を暗転させるフェード時間（秒）。0にするとフェードなし")]
    [SerializeField] private float openFadeDuration = 0.5f;
    [Tooltip("GemManagementPanel だけ遅れてフェードインを始めるまでの時間（秒）")]
    [SerializeField] private float gemPanelFadeInDelay = 0.2f;
    [Tooltip("GemManagementPanel のフェードイン時間（秒）。0にすると即時表示")]
    [SerializeField] private float gemPanelFadeInDuration = 0.25f;

    [Header("SE")]
    [SerializeField] private AudioClip closeSE;
    [SerializeField] private AudioClip equipSE;
    [SerializeField] private AudioClip unequipSE;
    [SerializeField] private AudioClip sellSE;
    [SerializeField] private AudioClip sellButtonSE;
    [SerializeField] private AudioClip sellCancelSE;
    [SerializeField] private AudioClip slotFullSE;

    [Header("Background Animation")]
    [Tooltip("背景アニメーションのフレーム画像（順番に表示）\n1枚だけ設定した場合はアニメーションなし")]
    [SerializeField] private Sprite[] bgAnimFrames;
    [Tooltip("アニメーション再生速度（fps）")]
    [SerializeField] private float bgAnimFps = 6f;
    [Tooltip("明暗アニメーションの最小輝度（0〜1）")]
    [Range(0f, 1f)]
    [SerializeField] private float bgBrightnessMin = 0.7f;
    [Tooltip("明暗アニメーションの最大輝度（0〜1）")]
    [Range(0f, 1f)]
    [SerializeField] private float bgBrightnessMax = 1.0f;
    [Tooltip("明暗アニメーションの速度の最小値（Hz）")]
    [SerializeField] private float bgAnimSpeedMin = 0.2f;
    [Tooltip("明暗アニメーションの速度の最大値（Hz）")]
    [SerializeField] private float bgAnimSpeedMax = 0.7f;
    [Tooltip("速度が次のランダム値に変化するまでの時間（秒）")]
    [SerializeField] private float bgSpeedChangeInterval = 3f;

    [Header("Skill Preview HUD")]
    [SerializeField] private GemSkillPreviewHUD gemSkillPreviewHUD;

    [Header("HP Status HUD")]
    [SerializeField] private HPStatusHUDUI hpStatusHUD;

    [Header("SlowMotion HUD Visual")]
    [SerializeField] private SlowMotionHUDVisualUI slowMotionHUD;

    [Header("Debug")]
    [SerializeField] private bool debugMode = false;
    [SerializeField] private Button debugAddGemsButton;
    [Tooltip("デバッグボタンのサイズ（幅×高さ）")]
    [SerializeField] private Vector2 debugButtonSize = new Vector2(200f, 50f);
    [Tooltip("デバッグボタンの位置（Canvas中央基準）")]
    [SerializeField] private Vector2 debugButtonPosition = new Vector2(600f, -300f);
    [SerializeField] private Button debugSlotLevelButton;
    [Tooltip("スロットレベルDebugボタンのサイズ（幅×高さ）")]
    [SerializeField] private Vector2 debugSlotButtonSize = new Vector2(200f, 50f);
    [Tooltip("スロットレベルDebugボタンの位置（Canvas中央基準）")]
    [SerializeField] private Vector2 debugSlotButtonPosition = new Vector2(600f, -360f);
    [Tooltip("Debugボタンで設定するスロットレベル値")]
    [SerializeField] private int debugSlotLevel = 5;
    [SerializeField] private Button debugClearGemsButton;
    [Tooltip("ジェム全削除Debugボタンのサイズ（幅×高さ）")]
    [SerializeField] private Vector2 debugClearButtonSize = new Vector2(120f, 70f);
    [Tooltip("ジェム全削除Debugボタンの位置（Canvas中央基準）")]
    [SerializeField] private Vector2 debugClearButtonPosition = new Vector2(900f, 200f);
    [SerializeField] private Button debugGoldMaxButton;
    [Tooltip("Gold MaxDebugボタンのサイズ（幅×高さ）")]
    [SerializeField] private Vector2 debugGoldMaxButtonSize = new Vector2(120f, 70f);
    [Tooltip("Gold MaxDebugボタンの位置（Canvas中央基準）")]
    [SerializeField] private Vector2 debugGoldMaxButtonPosition = new Vector2(900f, 120f);
    [Tooltip("Gold Maxボタンで設定するゴールド値")]
    [SerializeField] private int debugGoldMaxValue = 99999;
    [Tooltip("ジェム使用回数無制限フラグのON/OFF切り替えDebugボタン")]
    [SerializeField] private Button debugUnlimitedGemsButton;
    [Tooltip("無制限DebugボタンのTextMeshProUGUI（ON/OFF表示更新用）")]
    [SerializeField] private TextMeshProUGUI debugUnlimitedGemsButtonText;
    [Tooltip("無制限Debugボタンのサイズ（幅×高さ）")]
    [SerializeField] private Vector2 debugUnlimitedGemsButtonSize = new Vector2(160f, 70f);
    [Tooltip("無制限Debugボタンの位置（Canvas中央基準）")]
    [SerializeField] private Vector2 debugUnlimitedGemsButtonPosition = new Vector2(900f, 40f);
    [Tooltip("残り使用回数1のジェムを追加するDebugボタン（出撃前警告のテスト用）")]
    [SerializeField] private Button debugAddLowUsesGemButton;
    [Tooltip("残り1回ジェム追加Debugボタンのサイズ（幅×高さ）")]
    [SerializeField] private Vector2 debugAddLowUsesGemButtonSize = new Vector2(160f, 70f);
    [Tooltip("残り1回ジェム追加Debugボタンの位置（Canvas中央基準）")]
    [SerializeField] private Vector2 debugAddLowUsesGemButtonPosition = new Vector2(900f, -40f);
    [Tooltip("残り1回ジェムをロールする際に使うエリアID")]
    [SerializeField] private string debugAddLowUsesGemAreaId = "Area_01";

    [Header("Action Buttons (共通装備/売却)")]
    [SerializeField] private Button sharedEquipButton;
    [SerializeField] private Image sharedEquipButtonImage;
    [SerializeField] private Sprite equipSprite;
    [SerializeField] private Sprite unequipSprite;
    [SerializeField] private Sprite equipBgNoSelectionSprite;
    [SerializeField] private Sprite equippedItemBgSprite;
    [SerializeField] private Button sharedSellButton;
    [SerializeField] private Image sharedSellBgImage;
    // ★以前はGemSell.png(暗い背景込みの1枚絵)向けの暗い無効化色だったが、
    //   ネオン枠.pngに変更後はこの色を掛けると枠がほぼ黒く潰れて見えなくなるため、
    //   枠の形が視認できる程度の明るさに調整
    [SerializeField] private Color sellBgDisabledColor = new Color(0.45f, 0.4f, 0.4f, 1f);

    [Header("Neon Frame + Icon (装備/売却/閉じるボタンの2層構成)")]
    [Tooltip("ボタン枠に使うネオン管フレーム画像（Assets/Art/AreaSelect/Shop/新ネオン枠.png、小サイズ表示向けに太い管・くっきりした継ぎ目で作り直したもの）")]
    [SerializeField] private Sprite neonFrameSprite;
    [SerializeField] private Sprite gemIconSprite;
    [SerializeField] private Sprite sellIconSprite;
    [SerializeField] private Sprite exitIconSprite;
    [SerializeField] private Image sharedEquipButtonIcon;
    [SerializeField] private Image sharedSellButtonIcon;
    [SerializeField] private Image closeButtonIcon;
    [Tooltip("装備ボタン下部の状態テキスト（装備/解除/選択してください）")]
    [SerializeField] private TextMeshProUGUI sharedEquipButtonStateText;
    [Tooltip("テキストありボタン（装備/売却）のアイコンサイズ（px）")]
    [SerializeField] private float actionIconSizeWithText = 74f;
    [Tooltip("テキストありボタンでアイコンを中央からどれだけ上にずらすか（px）")]
    [SerializeField] private float actionIconOffsetYWithText = 8f;
    [Tooltip("テキストなしボタン（閉じる）のアイコンサイズ（px）")]
    [SerializeField] private float actionIconSizeNoText = 92f;
    [Tooltip("装備ボタンの枠色：装備可能時")]
    [SerializeField] private Color equipFrameColor = new Color(0.55f, 1f, 0.65f, 1f);
    [Tooltip("装備ボタンの枠色：解除（既に装備中）時")]
    [SerializeField] private Color unequipFrameColor = new Color(1f, 0.6f, 0.45f, 1f);
    [Tooltip("装備ボタンの枠色：未選択時")]
    [SerializeField] private Color noSelectionFrameColor = new Color(0.5f, 0.5f, 0.55f, 1f);
    [Tooltip("選択中ジェムのハイライト色（パルスの暗い側）")]
    [SerializeField] private Color selectedHighlightColor = new Color(0.3f, 0.4f, 0.6f, 1f);
    [Tooltip("パルスアニメーションの明るい色（ハイライト色から変化する先）")]
    [SerializeField] private Color selectedPulseColor = new Color(0.6f, 0.75f, 1f, 1f);
    [Tooltip("パルスの速度（Hz）。1=1秒で1往復")]
    [SerializeField] private float selectedPulseSpeed = 2f;

    [Header("SE")]
    [Tooltip("ジェムアイテムが選択されたときのSE")]
    [SerializeField] private AudioClip gemSelectSE;

    [Header("Sell Confirmation Dialog")]
    [SerializeField] private GameObject sellConfirmPanel;
    [SerializeField] private TextMeshProUGUI sellConfirmText;
    [SerializeField] private Button sellConfirmYesBtn;
    [SerializeField] private Button sellConfirmNoBtn;

    // ========== Runtime State ==========
    private readonly List<GameObject> gemItemObjects = new List<GameObject>();
    private AudioSource audioSource;
    private Color sellBgOriginalColor;
    private Color sellIconOriginalColor;
    private int pendingSellIdx = -1;
    private int selectedGemIdx = -1;
    private bool isOpening = false;
    private bool isClosing = false;
    private Coroutine bgAnimCoroutine;
    private Coroutine bgBrightnessCoroutine;
    private Coroutine selectedPulseCoroutine;
    private Image dimPanelImage;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }

        if (sharedSellBgImage != null)
            sellBgOriginalColor = sharedSellBgImage.color;
        if (sharedSellButtonIcon != null)
            sellIconOriginalColor = sharedSellButtonIcon.color;

        if (closeButton != null)
            closeButton.onClick.AddListener(Close);

        if (slotLevelText != null)
        {
            var slotRowTrans = slotLevelText.transform.parent;
            var slotIconTrans = slotRowTrans.Find("SlotIcon");
            if (slotIconTrans != null)
            {
                var le = slotIconTrans.GetComponent<LayoutElement>() ?? slotIconTrans.gameObject.AddComponent<LayoutElement>();
                le.preferredWidth = 120f;
                le.preferredHeight = 120f;
            }
            var hlg = slotRowTrans.GetComponent<HorizontalLayoutGroup>();
            if (hlg != null)
                hlg.childForceExpandHeight = false;
            foreach (var btnName in new[] { "SharedEquipButton", "SharedSellButton", "CloseButton" })
            {
                var btnTrans = slotRowTrans.Find(btnName);
                if (btnTrans != null)
                {
                    var le = btnTrans.GetComponent<LayoutElement>() ?? btnTrans.gameObject.AddComponent<LayoutElement>();
                    le.preferredHeight = 60f;
                }
            }
        }

        if (sellConfirmYesBtn != null)
            sellConfirmYesBtn.onClick.AddListener(ConfirmSell);
        if (sellConfirmNoBtn != null)
            sellConfirmNoBtn.onClick.AddListener(OnSellConfirmNo);

        if (debugAddGemsButton != null)
        {
            debugAddGemsButton.gameObject.SetActive(debugMode);
            if (debugMode)
                debugAddGemsButton.onClick.AddListener(DebugAddAllGems);
        }

        if (debugSlotLevelButton != null)
        {
            debugSlotLevelButton.gameObject.SetActive(debugMode);
            if (debugMode)
                debugSlotLevelButton.onClick.AddListener(DebugSetSlotLevel);
        }

        if (debugClearGemsButton != null)
        {
            debugClearGemsButton.gameObject.SetActive(debugMode);
            if (debugMode)
                debugClearGemsButton.onClick.AddListener(DebugClearAllGems);
        }

        if (debugGoldMaxButton != null)
        {
            debugGoldMaxButton.gameObject.SetActive(debugMode);
            if (debugMode)
                debugGoldMaxButton.onClick.AddListener(DebugSetGoldMax);
        }

        if (debugUnlimitedGemsButton != null)
        {
            debugUnlimitedGemsButton.gameObject.SetActive(debugMode);
            if (debugMode)
            {
                debugUnlimitedGemsButton.onClick.AddListener(DebugToggleUnlimitedGemUses);
                UpdateDebugUnlimitedGemsButtonText();
            }
        }

        if (debugAddLowUsesGemButton != null)
        {
            debugAddLowUsesGemButton.gameObject.SetActive(debugMode);
            if (debugMode)
                debugAddLowUsesGemButton.onClick.AddListener(DebugAddLowUsesGem);
        }

        if (sharedEquipButton != null)
            sharedEquipButton.onClick.AddListener(OnSharedEquipClick);
        if (sharedSellButton != null)
            sharedSellButton.onClick.AddListener(OnSharedSellClick);

        if (dimPanel != null)
            dimPanelImage = dimPanel.GetComponent<Image>();

        if (hpStatusHUD == null)
            hpStatusHUD = FindFirstObjectByType<HPStatusHUDUI>();
        if (slowMotionHUD == null)
            slowMotionHUD = FindFirstObjectByType<SlowMotionHUDVisualUI>();

        HideAllPanels();
        InitGridLayout();
    }

    // ========== Grid Layout ==========

    /// <summary>Awake時にContentのVLGをGridLayoutGroupに切り替える</summary>
    private void InitGridLayout()
    {
        if (gemListContainer == null) return;
        if (gemListContainer.GetComponent<GridLayoutGroup>() != null) return; // 既にGLG

        var vlg = gemListContainer.GetComponent<VerticalLayoutGroup>();
        if (vlg != null)
        {
            vlg.enabled = false; // GLGと競合しないよう即座に無効化（Destroyは遅延するため）
#if UNITY_EDITOR
            DestroyImmediate(vlg);
#else
            Destroy(vlg);
#endif
        }
        gemListContainer.gameObject.AddComponent<GridLayoutGroup>();
    }

    /// <summary>グリッド設定・パネルサイズ・ScrollView高さをSerializeFieldから更新する</summary>
    private void UpdateGridSettings()
    {
        if (gemListContainer == null || gemPanel == null) return;

        float cellW = gemItemWidth > 0f ? gemItemWidth : 300f;
        int cols = Mathf.Max(1, gemGridColumns);

        // GridLayoutGroup 設定
        var glg = gemListContainer.GetComponent<GridLayoutGroup>();
        if (glg != null)
        {
            glg.padding      = new RectOffset(4, 4, 4, 4);
            glg.cellSize     = new Vector2(cellW, gemGridCellHeight);
            glg.spacing      = gemGridSpacing;
            glg.startCorner  = GridLayoutGroup.Corner.UpperLeft;
            glg.startAxis    = GridLayoutGroup.Axis.Horizontal;
            glg.childAlignment = TextAnchor.UpperLeft;
            glg.constraint   = GridLayoutGroup.Constraint.FixedColumnCount;
            glg.constraintCount = cols;
        }

        // パネル幅 = 列数×セル幅 + 列間スペース + GLGパディング(4+4) + パネルパディング(20+20)
        float panelW = cols * cellW + (cols - 1) * gemGridSpacing.x + 8f + 40f;

        // ScrollView表示高さ = 3行分 + 2行間スペース + GLGパディング(4+4)
        float viewH = 3f * gemGridCellHeight + 2f * gemGridSpacing.y + 8f;

        // パネル高さ = スロット行(72) + 区切り(2) + 警告行(常時確保・LowUsesWarningRowHeight)
        //            + ScrollView + VLGスペーシング(10×3、警告行追加で+1ギャップ) + パネルパディング(16+16)
        // ★警告行はcontainer自体が常時Active（表示/非表示はCanvasGroup.alphaで切替）のため、
        //   非表示時でもレイアウト上の高さを占有し続ける。ここで加算しないとScrollViewが
        //   その分だけ下にはみ出し、3行目が見切れる。
        float panelH = 72f + 2f + LowUsesWarningRowHeight + viewH + 30f + 32f;

        var panelRect = gemPanel.GetComponent<RectTransform>();
        if (panelRect != null)
        {
            panelRect.sizeDelta = new Vector2(panelW, panelH);

            // X位置：HUDを除いた領域の中央に配置
            // HUD幅をhudWidthとすると、利用可能領域中央のanchoredX = hudWidth/2
            float anchoredX = hudWidth / 2f;
            panelRect.anchoredPosition = new Vector2(anchoredX, panelRect.anchoredPosition.y);
        }

        // ScrollViewの高さも更新
        var scrollView = gemPanel.transform.Find("ScrollView");
        if (scrollView != null)
        {
            var svRect = scrollView.GetComponent<RectTransform>();
            if (svRect != null) svRect.sizeDelta = new Vector2(svRect.sizeDelta.x, viewH);
            var svLE = scrollView.GetComponent<LayoutElement>();
            if (svLE != null)
            {
                svLE.preferredHeight = viewH;
                svLE.flexibleHeight  = 0f;
            }
        }
    }

    private void PlaySE(AudioClip clip)
    {
        if (audioSource == null || clip == null) return;
        float vol = SoundSettingsManager.Instance != null ? SoundSettingsManager.Instance.SEVolume : 1f;
        audioSource.PlayOneShot(clip, vol);
    }

    // ========== Public API ==========

    /// <summary>ジェム管理パネルを開く</summary>
    public void Open()
    {
        if (isOpening) return;
        var areaMenu = FindObjectOfType<Game.UI.AreaSelectMenu>();
        if (areaMenu != null && areaMenu.IsTransitioning) return;
        StartCoroutine(OpenWithFade());
    }

    private IEnumerator OpenWithFade()
    {
        isOpening = true;
        yield return StartCoroutine(FadeScreen(0f, 1f));

        if (dimPanel != null) dimPanel.SetActive(true);
        SetHideWhileOpenActive(false);
        StartBgAnim();
        if (gemPanel != null)
        {
            gemPanel.SetActive(true);
            var cg = gemPanel.GetComponent<CanvasGroup>();
            if (cg == null) cg = gemPanel.AddComponent<CanvasGroup>();
            cg.alpha = (gemPanelFadeInDuration > 0f) ? 0f : 1f;
        }
        gemSkillPreviewHUD?.Show();
        hpStatusHUD?.Show();
        slowMotionHUD?.Show();
        RefreshGemList();

        yield return StartCoroutine(FadeScreen(1f, 0f));

        if (gemPanel != null && gemPanelFadeInDuration > 0f)
        {
            yield return new WaitForSecondsRealtime(gemPanelFadeInDelay);
            yield return StartCoroutine(FadeGemPanel(0f, 1f));
        }

        isOpening = false;
    }

    private IEnumerator FadeScreen(float from, float to)
    {
        if (openFadeDuration <= 0f) yield break;

        GameObject fadeObj = new GameObject("OpenFade");
        Canvas fadeCanvas = fadeObj.AddComponent<Canvas>();
        fadeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        fadeCanvas.sortingOrder = 9999;

        UnityEngine.UI.CanvasScaler scaler = fadeObj.AddComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        GameObject imageObj = new GameObject("FadeImage");
        imageObj.transform.SetParent(fadeObj.transform, false);

        Image fadeImage = imageObj.AddComponent<Image>();
        fadeImage.color = new Color(0f, 0f, 0f, from);

        RectTransform rt = imageObj.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;

        float elapsed = 0f;
        while (elapsed < openFadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            fadeImage.color = new Color(0f, 0f, 0f, Mathf.Lerp(from, to, elapsed / openFadeDuration));
            yield return null;
        }

        Destroy(fadeObj);
    }

    private IEnumerator FadeGemPanel(float from, float to)
    {
        if (gemPanel == null || gemPanelFadeInDuration <= 0f) yield break;
        var cg = gemPanel.GetComponent<CanvasGroup>();
        if (cg == null) yield break;

        float elapsed = 0f;
        while (elapsed < gemPanelFadeInDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            cg.alpha = Mathf.Lerp(from, to, elapsed / gemPanelFadeInDuration);
            yield return null;
        }

        cg.alpha = to;
    }

    /// <summary>ジェム管理パネルを閉じる</summary>
    public void Close()
    {
        if (isClosing) return;
        StartCoroutine(CloseWithFade());
    }

    private IEnumerator CloseWithFade()
    {
        isClosing = true;
        PlaySE(closeSE);
        yield return StartCoroutine(FadeScreen(0f, 1f));
        HideAllPanels();
        yield return StartCoroutine(FadeScreen(1f, 0f));
        FindObjectOfType<Game.UI.AreaSelectMenu>()?.ResetPanelTransition();
        isClosing = false;
    }

    /// <summary>
    /// dimPanel（AreaPanelの中身しか暗転できない）が届かない、Canvas直下にある要素
    /// （チュートリアルボタン等）を、ジェム画面の開閉に合わせて表示/非表示にする。
    /// </summary>
    private void SetHideWhileOpenActive(bool active)
    {
        if (hideWhileOpen == null) return;
        foreach (var go in hideWhileOpen)
        {
            if (go != null) go.SetActive(active);
        }
    }

    private void HideAllPanels()
    {
        if (selectedPulseCoroutine != null) { StopCoroutine(selectedPulseCoroutine); selectedPulseCoroutine = null; }
        StopBgAnim();
        if (dimPanel != null) dimPanel.SetActive(false);
        if (gemPanel != null) gemPanel.SetActive(false);
        SetHideWhileOpenActive(true);
        if (sellConfirmPanel != null) sellConfirmPanel.SetActive(false);
        pendingSellIdx = -1;
        selectedGemIdx = -1;
        gemSkillPreviewHUD?.Hide();
        hpStatusHUD?.Hide();
        slowMotionHUD?.Hide();
    }

    // ========== Background Animation ==========

    private void StartBgAnim()
    {
        if (dimPanelImage == null) return;

        // フレームアニメ（2枚以上あるときのみ）
        if (bgAnimFrames != null && bgAnimFrames.Length > 1)
        {
            if (bgAnimCoroutine != null) StopCoroutine(bgAnimCoroutine);
            bgAnimCoroutine = StartCoroutine(AnimateBg());
        }

        // 明暗アニメ（スプライトが設定されているときのみ）
        bool hasSprite = (bgAnimFrames != null && bgAnimFrames.Length > 0)
                         || dimPanelImage.sprite != null;
        if (hasSprite)
        {
            if (bgBrightnessCoroutine != null) StopCoroutine(bgBrightnessCoroutine);
            bgBrightnessCoroutine = StartCoroutine(AnimateBgBrightness());
        }
    }

    private void StopBgAnim()
    {
        if (bgAnimCoroutine != null)
        {
            StopCoroutine(bgAnimCoroutine);
            bgAnimCoroutine = null;
        }
        if (bgBrightnessCoroutine != null)
        {
            StopCoroutine(bgBrightnessCoroutine);
            bgBrightnessCoroutine = null;
        }
        if (dimPanelImage != null)
            dimPanelImage.color = Color.white;
    }

    private IEnumerator AnimateBg()
    {
        int frameIndex = 0;
        float interval = 1f / Mathf.Max(bgAnimFps, 0.1f);
        while (true)
        {
            dimPanelImage.sprite = bgAnimFrames[frameIndex % bgAnimFrames.Length];
            frameIndex++;
            yield return new WaitForSeconds(interval);
        }
    }

    private IEnumerator AnimateBgBrightness()
    {
        float t = 0f;
        float currentSpeed = Random.Range(bgAnimSpeedMin, bgAnimSpeedMax);
        float targetSpeed  = Random.Range(bgAnimSpeedMin, bgAnimSpeedMax);
        float speedTimer   = 0f;

        while (true)
        {
            float dt = Time.unscaledDeltaTime;

            speedTimer += dt;
            currentSpeed = Mathf.Lerp(currentSpeed, targetSpeed, dt * 1.5f);

            if (speedTimer >= bgSpeedChangeInterval)
            {
                targetSpeed = Random.Range(bgAnimSpeedMin, bgAnimSpeedMax);
                speedTimer  = 0f;
            }

            t += dt * currentSpeed;

            float brightness = Mathf.Lerp(bgBrightnessMin, bgBrightnessMax,
                                          (Mathf.Sin(t * Mathf.PI * 2f) + 1f) * 0.5f);
            dimPanelImage.color = new Color(brightness, brightness, brightness, 1f);
            yield return null;
        }
    }

    // ========== Gem List ==========

    private void RefreshGemList()
    {
        if (gemListContainer == null)
        {
            Debug.LogError("[GemManagementUI] gemListContainer is null! Re-run Setup Gem Management UI.");
            return;
        }

        // パルスコルーチンを先に停止（アイテム破棄前に止めないとMissingReferenceExceptionが発生する）
        if (selectedPulseCoroutine != null)
        {
            StopCoroutine(selectedPulseCoroutine);
            selectedPulseCoroutine = null;
        }

        // 既存アイテムをクリア（即時破棄してレイアウト計算から除外）
        foreach (var obj in gemItemObjects)
        {
            if (obj != null)
            {
                obj.transform.SetParent(null);
                Destroy(obj);
            }
        }
        gemItemObjects.Clear();

        UpdateGridSettings();

        var data = ProgressManager.Instance?.Data;
        if (data == null) { Debug.LogError("[GemManagementUI] ProgressManager data is null!"); return; }

        Debug.Log($"[GemManagementUI] RefreshGemList: inventory={data.gemInventory.Count}, container={gemListContainer.name}, containerActive={gemListContainer.gameObject.activeInHierarchy}");

        // スロット使用状況を更新
        int usedSlots = CalcUsedSlots(data);
        if (slotLevelText != null)
            slotLevelText.text = $"{usedSlots}/{data.slotLevel}";

        // 空メッセージ表示制御（常に非表示）
        if (emptyMessageText != null) emptyMessageText.gameObject.SetActive(false);

        // ジェムアイテムを生成
        for (int i = 0; i < data.gemInventory.Count; i++)
        {
            CreateGemItem(data.gemInventory[i], i, data);
        }

        // 選択状態を復元（インデックスがまだ有効なら再選択、無効なら解除）
        int prevSelected = selectedGemIdx;
        selectedGemIdx = -1;
        if (prevSelected >= 0 && prevSelected < gemItemObjects.Count)
            SelectGem(prevSelected, playSE: false);
        else
            UpdateSharedButtons();
    }

    private int CalcUsedSlots(ProgressData data)
    {
        int total = 0;
        foreach (int idx in data.equippedGemIndices)
        {
            if (idx < 0 || idx >= data.gemInventory.Count) continue;
            var gemDef = GemManager.Instance?.LoadGemDefinition(data.gemInventory[idx]);
            if (gemDef != null) total += gemDef.requiredSlots;
        }
        return total;
    }

    private void CreateGemItem(GemInstance gemInst, int inventoryIdx, ProgressData data)
    {
        GameObject itemObj;
        if (gemItemTemplate != null)
        {
            itemObj = Instantiate(gemItemTemplate, gemListContainer);
            itemObj.SetActive(true);
        }
        else
        {
            itemObj = CreateDefaultGemItem();
            itemObj.transform.SetParent(gemListContainer, false);
        }

        var gemDef = GemManager.Instance?.LoadGemDefinition(gemInst);
        bool isEquipped = data.equippedGemIndices.Contains(inventoryIdx);

        var nameText        = itemObj.transform.Find("TextContainer/NameRow/NameText")?.GetComponent<TextMeshProUGUI>();
        var slotDisplayText = itemObj.transform.Find("TextContainer/NameRow/SlotDisplayText")?.GetComponent<TextMeshProUGUI>();
        var skillIconsCont  = itemObj.transform.Find("TextContainer/SkillIconsContainer");
        var equippedBadge   = itemObj.transform.Find("TextContainer/NameRow/EquippedBadge")?.gameObject;
        var usesBadgeText   = itemObj.transform.Find("TextContainer/NameRow/GemIcon/UsesBadge/Text")?.GetComponent<TextMeshProUGUI>();


        if (gemDef != null)
        {
            if (nameText != null)
                nameText.text = gemDef.gemName;
            if (slotDisplayText != null)
                slotDisplayText.text = string.Format(slotDisplayFormat, gemDef.requiredSlots);

            if (usesBadgeText != null)
            {
                bool unlimited = GemManager.Instance != null && GemManager.Instance.HasUnlimitedGemUses;
                if (unlimited)
                {
                    usesBadgeText.text = "∞";
                    usesBadgeText.color = normalUsesBadgeColor;
                }
                else
                {
                    usesBadgeText.text = gemInst.remainingUses.ToString();
                    usesBadgeText.color = gemInst.remainingUses <= lowUsesThreshold
                        ? lowUsesBadgeColor
                        : normalUsesBadgeColor;
                }
            }

            if (skillIconsCont != null)
            {
                SkillDefinition baseDef = string.IsNullOrEmpty(gemInst.baseSkillName) ? null
                    : Resources.Load<SkillDefinition>($"GameData/Skills/{gemInst.baseSkillName}");
                SkillDefinition b1Def = string.IsNullOrEmpty(gemInst.bonusSkill1Name) ? null
                    : Resources.Load<SkillDefinition>($"GameData/Skills/{gemInst.bonusSkill1Name}");
                SkillDefinition b2Def = string.IsNullOrEmpty(gemInst.bonusSkill2Name) ? null
                    : Resources.Load<SkillDefinition>($"GameData/Skills/{gemInst.bonusSkill2Name}");

                PopulateSkillRow(skillIconsCont.Find("SkillRow_Base"),   baseDef);
                PopulateSkillRow(skillIconsCont.Find("SkillRow_Bonus1"), b1Def);
                PopulateSkillRow(skillIconsCont.Find("SkillRow_Bonus2"), b2Def);

                ApplyGemItemHeight(itemObj, skillIconsCont, baseDef, b1Def, b2Def);

                // TextContainer と SkillIconsContainer の VLG を無効化し、全て直接位置指定
                var textCont = itemObj.transform.Find("TextContainer");
                if (textCont != null)
                {
                    var tcVlg = textCont.GetComponent<VerticalLayoutGroup>();
                    if (tcVlg != null) tcVlg.enabled = false;

                    // NameRow を上端に固定
                    var nameRowTrans = textCont.Find("NameRow");
                    if (nameRowTrans != null)
                    {
                        var nameRowRect = nameRowTrans.GetComponent<RectTransform>();
                        if (nameRowRect != null)
                        {
                            nameRowRect.anchorMin        = new Vector2(0f, 1f);
                            nameRowRect.anchorMax        = new Vector2(1f, 1f);
                            nameRowRect.pivot            = new Vector2(0.5f, 1f);
                            nameRowRect.sizeDelta        = new Vector2(0f, 28f);
                            nameRowRect.anchoredPosition = new Vector2(0f, 0f);
                        }
                    }

                    // SkillIconsContainer を NameRow 直下に配置
                    float totalIconH = 0f;
                    string[] rowNames = { "SkillRow_Base", "SkillRow_Bonus1", "SkillRow_Bonus2" };
                    foreach (var rn in rowNames)
                    {
                        var r = skillIconsCont.Find(rn);
                        if (r != null && r.gameObject.activeSelf)
                        {
                            var rLE = r.GetComponent<LayoutElement>();
                            totalIconH += (rLE != null ? rLE.preferredHeight : skillRowHeight) + 4f;
                        }
                    }
                    if (totalIconH > 0f) totalIconH -= 4f; // 末尾spacing除去

                    var iconsRect = skillIconsCont.GetComponent<RectTransform>();
                    if (iconsRect != null)
                    {
                        iconsRect.anchorMin        = new Vector2(0f, 1f);
                        iconsRect.anchorMax        = new Vector2(1f, 1f);
                        iconsRect.pivot            = new Vector2(0.5f, 1f);
                        iconsRect.sizeDelta        = new Vector2(0f, totalIconH);
                        iconsRect.anchoredPosition = new Vector2(0f, -(28f + 6f));
                    }
                }

                // SkillIconsContainer の VLG を無効化し、行を上から直接配置
                var vlgSkill = skillIconsCont.GetComponent<VerticalLayoutGroup>();
                if (vlgSkill != null) vlgSkill.enabled = false;

                float yOffset = 0f;
                foreach (var rowName in new[] { "SkillRow_Base", "SkillRow_Bonus1", "SkillRow_Bonus2" })
                {
                    var row = skillIconsCont.Find(rowName);
                    if (row == null || !row.gameObject.activeSelf) continue;
                    var rowLE   = row.GetComponent<LayoutElement>();
                    var rowRect = row.GetComponent<RectTransform>();
                    if (rowRect == null) continue;
                    float rowH = rowLE != null ? rowLE.preferredHeight : skillRowHeight;
                    rowRect.anchorMin        = new Vector2(0f, 1f);
                    rowRect.anchorMax        = new Vector2(1f, 1f);
                    rowRect.pivot            = new Vector2(0.5f, 1f);
                    rowRect.sizeDelta        = new Vector2(0f, rowH);
                    rowRect.anchoredPosition = new Vector2(0f, -yOffset);
                    yOffset += rowH + 4f;
                }

                Debug.Log($"[GemMgr] 直接配置: yOffset={yOffset}");
            }
        }
        else
        {
            if (nameText != null) nameText.text = "（データ読み込み失敗）";
        }

        if (equippedBadge != null) equippedBadge.SetActive(false);

        // 装備中はItemBgのスプライトを切り替え
        var itemBgImg = GetItemBgImage(itemObj);
        if (itemBgImg != null && equippedItemBgSprite != null && isEquipped)
            itemBgImg.sprite = equippedItemBgSprite;

        // アイテム全体をクリックで選択（Transition.None で色変化はSelectGemが管理）
        int capturedIdx = inventoryIdx;
        var itemBtn = itemObj.GetComponent<Button>() ?? itemObj.AddComponent<Button>();
        itemBtn.transition = Selectable.Transition.None;
        itemBtn.onClick.RemoveAllListeners();
        itemBtn.onClick.AddListener(() => SelectGem(capturedIdx));

        gemItemObjects.Add(itemObj);
    }

    /// <summary>
    /// テンプレート用のスキル行を名前付きで生成する（Play前Inspector調整対応）
    /// </summary>
    private GameObject CreateSkillRow(string rowName, Transform parent)
    {
        var rowObj = new GameObject(rowName);
        rowObj.transform.SetParent(parent, false);

        // 行背景（カテゴリカラー）- PopulateSkillRow で色を上書き
        rowObj.AddComponent<Image>().color = new Color(0.15f, 0.15f, 0.15f, 0.5f);

        var rowLayout = rowObj.AddComponent<HorizontalLayoutGroup>();
        rowLayout.spacing = 6f;
        rowLayout.childAlignment = TextAnchor.MiddleLeft;
        rowLayout.childControlWidth = true;   // LayoutElement.preferredWidth をアイコン幅に使用
        rowLayout.childControlHeight = true;
        rowLayout.childForceExpandWidth = false;
        rowLayout.childForceExpandHeight = false; // iconDisplaySize.y を行高に使用

        var rowLE = rowObj.AddComponent<LayoutElement>();
        rowLE.preferredHeight = skillIconSize;

        // アイコン列コンテナ（固定幅でテキスト開始位置を統一）
        var iconContainerObj = new GameObject("IconContainer");
        iconContainerObj.transform.SetParent(rowObj.transform, false);
        var icLE = iconContainerObj.AddComponent<LayoutElement>();
        icLE.minWidth       = iconColumnWidth;
        icLE.preferredWidth  = iconColumnWidth;
        icLE.preferredHeight = skillIconSize;

        // アイコン画像（コンテナ内中央配置、iconDisplayOffset で個別調整可能）
        var iconObj = new GameObject("IconImage");
        iconObj.transform.SetParent(iconContainerObj.transform, false);
        var iconRect = iconObj.AddComponent<RectTransform>();
        iconRect.anchorMin       = new Vector2(0.5f, 0.5f);
        iconRect.anchorMax       = new Vector2(0.5f, 0.5f);
        iconRect.pivot           = new Vector2(0.5f, 0.5f);
        iconRect.sizeDelta       = new Vector2(skillIconSize, skillIconSize);
        iconRect.anchoredPosition = Vector2.zero;
        iconObj.AddComponent<Image>().color = new Color(0.3f, 0.3f, 0.3f, 0.5f);

        // スキル名テキスト
        var nameObj = new GameObject("SkillName");
        nameObj.transform.SetParent(rowObj.transform, false);
        var nameTMP = nameObj.AddComponent<TextMeshProUGUI>();
        nameTMP.text = "";
        nameTMP.fontSize = 20f;
        nameTMP.fontStyle = FontStyles.Bold;
        nameTMP.color = Color.black;
        nameTMP.alignment = TextAlignmentOptions.MidlineLeft;
        nameTMP.enableWordWrapping = false;
        nameObj.AddComponent<LayoutElement>().flexibleWidth = 1f;

        return rowObj;
    }

    /// <summary>
    /// 既存のスキル行にデータを設定する（skill が null なら非表示）
    /// </summary>
    private void PopulateSkillRow(Transform row, SkillDefinition skill)
    {
        if (row == null) return;
        bool hasSkill = skill != null;
        row.gameObject.SetActive(hasSkill);
        if (!hasSkill) return;

        var iconContainer = row.Find("IconContainer");
        var iconImg       = iconContainer?.Find("IconImage")?.GetComponent<Image>();
        var nameTMP       = row.Find("SkillName")?.GetComponent<TextMeshProUGUI>();

        if (iconImg != null)
        {
            iconImg.sprite = skill.icon;
            iconImg.color  = skill.icon != null ? Color.white : new Color(0.3f, 0.3f, 0.3f, 0.5f);

            // アイコンサイズとオフセットを適用
            var iconRect = iconImg.GetComponent<RectTransform>();
            if (iconRect != null)
            {
                iconRect.sizeDelta = skill.iconDisplaySize != Vector2.zero
                    ? skill.iconDisplaySize
                    : new Vector2(skillIconSize, skillIconSize);
                iconRect.anchoredPosition = skill.iconDisplayOffset;
            }

            // 行背景グラデーションをカテゴリで設定
            var rowBg = row.GetComponent<Image>();
            if (rowBg != null)
            {
                Color startColor = skill.category switch
                {
                    Game.Skills.SkillCategory.CategoryA => categoryAColor,
                    Game.Skills.SkillCategory.CategoryB => categoryBColor,
                    Game.Skills.SkillCategory.CategoryC => categoryCColor,
                    _ => new Color(0.15f, 0.15f, 0.15f, 0.5f)
                };
                if (useGradientBackground)
                {
                    rowBg.sprite = CreateHorizontalGradientSprite(startColor, gradientEndColor);
                    rowBg.type = Image.Type.Simple;
                    rowBg.color = Color.white;
                }
                else
                {
                    rowBg.sprite = null;
                    rowBg.color = startColor;
                }
            }

            // 行とコンテナの高さを統一（アイコン画像サイズは変えず、行間を揃える）
            var containerLE = iconContainer?.GetComponent<LayoutElement>();
            if (containerLE != null) containerLE.preferredHeight = skillRowHeight;
        }

        // 高さ・幅は iconImg の有無に関わらず適用
        var rowLE = row.GetComponent<LayoutElement>();
        if (rowLE != null) rowLE.preferredHeight = skillRowHeight;

        // 行の横幅（0 = 親コンテナ全幅、>0 = 固定幅）
        // childControlWidth=true のままだと ForceRebuildLayoutImmediate がsizeDeltaを上書きするため
        // childControlWidth=false にして RectTransform を直接指定する
        var parentVLG = row.parent?.GetComponent<VerticalLayoutGroup>();
        var rowRect = row.GetComponent<RectTransform>();
        if (skillRowWidth > 0f)
        {
            if (parentVLG != null)
            {
                parentVLG.childControlWidth = false;
                parentVLG.childForceExpandWidth = false;
            }
            if (rowRect != null)
                rowRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, skillRowWidth);
        }
        else
        {
            if (parentVLG != null)
            {
                parentVLG.childControlWidth = true;
                parentVLG.childForceExpandWidth = true;
            }
        }

        if (nameTMP != null)
            nameTMP.text = skill.skillName;
    }

    /// <summary>
    /// スキルの iconDisplaySize を元にジェム枠とアイコンコンテナの高さを動的に設定する
    /// </summary>
    private void ApplyGemItemHeight(GameObject itemObj, Transform skillIconsCont,
        SkillDefinition baseDef, SkillDefinition b1Def, SkillDefinition b2Def)
    {
        float IconH(SkillDefinition def) => def != null ? skillRowHeight : 0f;

        const float rowSpacing  = 4f;
        const float nameRowH    = 28f;
        const float contentGap  = 6f;
        const float paddingVert = 16f; // top 8 + bottom 8

        float totalIconH = 0f;
        if (baseDef != null) totalIconH += IconH(baseDef);
        if (b1Def   != null) totalIconH += rowSpacing + IconH(b1Def);
        if (b2Def   != null) totalIconH += rowSpacing + IconH(b2Def);
        if (totalIconH <= 0f) totalIconH = skillIconSize;

        // SkillIconsContainer の高さを更新
        var iconsLE = skillIconsCont?.GetComponent<LayoutElement>();
        if (iconsLE != null) iconsLE.preferredHeight = totalIconH;

        // GemItem の RectTransform と LayoutElement を両方更新（親VLGが childControlHeight=false のため両方必要）
        float totalH = nameRowH + contentGap + totalIconH + paddingVert;
        var itemRect = itemObj.GetComponent<RectTransform>();
        if (itemRect != null) itemRect.sizeDelta = new Vector2(itemRect.sizeDelta.x, totalH);
        var itemLE = itemObj.GetComponent<LayoutElement>();
        if (itemLE != null) itemLE.preferredHeight = totalH;
    }

    /// <summary>
    /// 左→右のグラデーションSpriteを生成（スキル行背景用）
    /// </summary>
    private Sprite CreateHorizontalGradientSprite(Color leftColor, Color rightColor)
    {
        const int width = 64;
        const int height = 4;
        var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode   = TextureWrapMode.Clamp;

        for (int x = 0; x < width; x++)
        {
            Color c = Color.Lerp(leftColor, rightColor, x / (float)(width - 1));
            for (int y = 0; y < height; y++)
                tex.SetPixel(x, y, c);
        }
        tex.Apply();

        return Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f);
    }

    private string GetSkillDisplayName(string assetName)
    {
        if (string.IsNullOrEmpty(assetName)) return "";
        var skill = Resources.Load<SkillDefinition>($"GameData/Skills/{assetName}");
        return skill != null ? skill.skillName : assetName;
    }

    // ========== Selection ==========

    private static readonly Color NormalItemColor = Color.white;

    private static Image GetItemBgImage(GameObject itemObj)
    {
        if (itemObj == null) return null;
        var child = itemObj.transform.Find("ItemBg");
        return child != null ? child.GetComponent<Image>() : itemObj.GetComponent<Image>();
    }

    private void SelectGem(int idx, bool playSE = true)
    {
        // 前の選択のハイライトを解除
        if (selectedPulseCoroutine != null)
        {
            StopCoroutine(selectedPulseCoroutine);
            selectedPulseCoroutine = null;
        }
        if (selectedGemIdx >= 0 && selectedGemIdx < gemItemObjects.Count)
        {
            var prevImg = GetItemBgImage(gemItemObjects[selectedGemIdx]);
            if (prevImg != null) prevImg.color = NormalItemColor;
        }

        // 同じアイテムを再タップ → 点滅を再起動して終了
        if (selectedGemIdx == idx)
        {
            if (idx >= 0 && idx < gemItemObjects.Count)
            {
                var img = GetItemBgImage(gemItemObjects[idx]);
                if (img != null)
                    selectedPulseCoroutine = StartCoroutine(SelectedPulseCoroutine(img));
            }
            return;
        }

        selectedGemIdx = idx;

        // 選択SE再生
        if (playSE && gemSelectSE != null)
        {
            if (SoundSettingsManager.Instance != null)
                SoundSettingsManager.Instance.PlaySE(audioSource, gemSelectSE);
            else
                audioSource.PlayOneShot(gemSelectSE);
        }

        // 新選択をパルスハイライト
        if (selectedGemIdx >= 0 && selectedGemIdx < gemItemObjects.Count)
        {
            var img = GetItemBgImage(gemItemObjects[selectedGemIdx]);
            if (img != null)
                selectedPulseCoroutine = StartCoroutine(SelectedPulseCoroutine(img));
        }

        UpdateSharedButtons();
    }

    private IEnumerator SelectedPulseCoroutine(Image img)
    {
        while (true)
        {
            float t = (Mathf.Sin(Time.unscaledTime * selectedPulseSpeed * Mathf.PI * 2f) + 1f) / 2f;
            img.color = Color.Lerp(selectedHighlightColor, selectedPulseColor, t);
            yield return null;
        }
    }

    private void UpdateSharedButtons()
    {
        bool hasSelection = selectedGemIdx >= 0 && selectedGemIdx < gemItemObjects.Count;
        var data = ProgressManager.Instance?.Data;
        bool isEquipped = hasSelection && data != null && data.equippedGemIndices.Contains(selectedGemIdx);

        if (sharedEquipButton != null)
            sharedEquipButton.interactable = hasSelection;

        bool canSell = hasSelection && !isEquipped;
        if (sharedSellButton != null)
            sharedSellButton.interactable = canSell;

        if (sharedSellBgImage != null)
            sharedSellBgImage.color = canSell ? sellBgOriginalColor : sellBgDisabledColor;
        if (sharedSellButtonIcon != null)
            sharedSellButtonIcon.color = canSell ? sellIconOriginalColor : sellBgDisabledColor;

        // ★枠(EquipBg)の色は元々ButtonHoverEffect（ホバー時のグレー点滅）が制御しており、
        //   指示されていない状態別の色分けを追加すると競合するため削除した。
        //   枠の色そのものは変更せず、ButtonHoverEffectにそのまま任せる。
        bool unequipState = hasSelection && isEquipped;
        if (sharedEquipButtonStateText != null)
        {
            // ★未選択時は何も表示しない（不要なテキストだったため削除）
            sharedEquipButtonStateText.text = !hasSelection ? "" : (unequipState ? "解除" : "装備");
        }

        UpdateLowUsesWarning(hasSelection, data);
    }

    /// <summary>
    /// 選択中ジェムの残り使用回数が少ない場合、警告テキストを表示する
    /// </summary>
    private void UpdateLowUsesWarning(bool hasSelection, ProgressData data)
    {
        if (lowUsesWarningText == null) return;
        GameObject container = lowUsesWarningContainer != null ? lowUsesWarningContainer : lowUsesWarningText.gameObject;

        bool unlimited = GemManager.Instance != null && GemManager.Instance.HasUnlimitedGemUses;
        if (!hasSelection || data == null || selectedGemIdx >= data.gemInventory.Count || unlimited)
        {
            SetLowUsesWarningActive(container, false);
            return;
        }

        int remaining = data.gemInventory[selectedGemIdx].remainingUses;
        if (remaining <= lowUsesThreshold)
        {
            lowUsesWarningText.text = string.Format(lowUsesWarningFormat, remaining);
            SetLowUsesWarningActive(container, true);
        }
        else
        {
            SetLowUsesWarningActive(container, false);
        }
    }

    // ★警告の表示/非表示をCanvasGroupのalphaだけで切り替える（containerのSetActiveは使わない）。
    //   これによりcontainer自体は常にActiveのままレイアウト上の縦幅を確保し続け、
    //   警告の出現/消滅で下の装備欄がズレなくなる。
    private void SetLowUsesWarningActive(GameObject container, bool active)
    {
        bool wasVisible = lowUsesWarningCanvasGroup != null
            ? lowUsesWarningCanvasGroup.alpha > 0.5f
            : container.activeSelf;

        if (lowUsesWarningCanvasGroup != null)
        {
            lowUsesWarningCanvasGroup.alpha = active ? 1f : 0f;
        }
        else
        {
            // CanvasGroup未生成（旧バージョンのまま）の場合のフォールバック
            container.SetActive(active);
        }

        if (active && !wasVisible)
        {
            if (lowUsesWarningPulseCoroutine != null) StopCoroutine(lowUsesWarningPulseCoroutine);
            lowUsesWarningPulseCoroutine = StartCoroutine(LowUsesWarningPulseLoop(container));
        }
        else if (!active && lowUsesWarningPulseCoroutine != null)
        {
            StopCoroutine(lowUsesWarningPulseCoroutine);
            lowUsesWarningPulseCoroutine = null;
            container.transform.localScale = Vector3.one;
            lowUsesWarningText.color = lowUsesBadgeColor;
            if (lowUsesWarningIcon != null) lowUsesWarningIcon.color = Color.white;
        }
    }

    // ★警告テキスト＋アイコンを、点滅（色）とパルス（拡大縮小）で強調し続ける
    private IEnumerator LowUsesWarningPulseLoop(GameObject container)
    {
        while (true)
        {
            float blinkT = (Mathf.Sin(Time.unscaledTime * lowUsesBlinkSpeed * Mathf.PI * 2f) + 1f) * 0.5f;
            Color blended = Color.Lerp(lowUsesBadgeColor, lowUsesBlinkColor, blinkT);
            lowUsesWarningText.color = blended;
            // ★アイコンはテキストと逆位相(白⇔赤)にして、互い違いにチカチカさせる
            if (lowUsesWarningIcon != null) lowUsesWarningIcon.color = Color.Lerp(lowUsesBlinkColor, lowUsesBadgeColor, blinkT);

            float pulseT = Mathf.Sin(Time.unscaledTime * lowUsesPulseSpeed * Mathf.PI * 2f);
            float scale = 1f + pulseT * lowUsesPulseAmount;
            container.transform.localScale = new Vector3(scale, scale, 1f);

            yield return null;
        }
    }

    private void OnSharedEquipClick()
    {
        if (selectedGemIdx < 0) return;
        OnEquipToggle(selectedGemIdx);
    }

    private void OnSharedSellClick()
    {
        if (selectedGemIdx < 0) return;
        PlaySE(sellButtonSE);
        ShowSellConfirmation(selectedGemIdx);
    }

    // ========== Sell Confirmation ==========

    private void ShowSellConfirmation(int inventoryIdx)
    {
        var data = ProgressManager.Instance?.Data;
        if (data == null || inventoryIdx < 0 || inventoryIdx >= data.gemInventory.Count) return;

        pendingSellIdx = inventoryIdx;

        int price = GemManager.Instance?.GetAdjustedSellPrice(data.gemInventory[inventoryIdx]) ?? 0;

        if (sellConfirmText != null)
            sellConfirmText.text = $"このジェムを{price}Gで売却しますか？";

        if (sellConfirmPanel != null)
        {
            sellConfirmPanel.transform.SetAsLastSibling(); // GemDimPanel等より前面に確実に移動
            sellConfirmPanel.SetActive(true);
        }
    }

    private void OnSellConfirmNo()
    {
        PlaySE(sellCancelSE);
        HideSellConfirmDialog();
    }

    private void HideSellConfirmDialog()
    {
        pendingSellIdx = -1;
        if (sellConfirmPanel != null)
            sellConfirmPanel.SetActive(false);
    }

    private void ConfirmSell()
    {
        if (pendingSellIdx < 0) return;
        int idx = pendingSellIdx;
        HideSellConfirmDialog();
        OnSell(idx);
    }

    // ========== Equip / Sell ==========

    private void OnEquipToggle(int inventoryIdx)
    {
        var data = ProgressManager.Instance?.Data;
        if (data == null) return;

        if (data.equippedGemIndices.Contains(inventoryIdx))
        {
            // 解除
            data.equippedGemIndices.Remove(inventoryIdx);
            PlaySE(unequipSE);
        }
        else
        {
            // スロット上限チェックしてから装備
            var gemInst = data.gemInventory[inventoryIdx];
            var gemDef = GemManager.Instance?.LoadGemDefinition(gemInst);
            if (gemDef == null) return;

            int usedSlots = CalcUsedSlots(data);
            if (usedSlots + gemDef.requiredSlots > data.slotLevel)
            {
                Debug.Log($"[GemManagementUI] スロット不足 ({usedSlots}+{gemDef.requiredSlots} > {data.slotLevel})");
                PlaySE(slotFullSE);
                return;
            }
            data.equippedGemIndices.Add(inventoryIdx);
            PlaySE(equipSE);
        }

        ProgressManager.Instance.Save();
        RefreshGemList();
        gemSkillPreviewHUD?.Refresh();
    }

    private void OnSell(int inventoryIdx)
    {
        var data = ProgressManager.Instance?.Data;
        if (data == null) return;
        if (inventoryIdx < 0 || inventoryIdx >= data.gemInventory.Count) return;

        // 装備中は売却不可
        if (data.equippedGemIndices.Contains(inventoryIdx))
        {
            Debug.LogWarning("[GemManagementUI] 装備中のジェムは売却できません。");
            return;
        }

        var gemInst = data.gemInventory[inventoryIdx];

        // 売却金額（残り使用回数に応じて調整済み）を PersistentGold に加算
        int adjustedPrice = GemManager.Instance?.GetAdjustedSellPrice(gemInst) ?? 0;
        if (adjustedPrice > 0)
            GoldManager.Instance?.AddPersistentGold(adjustedPrice);

        // インベントリから削除
        data.gemInventory.RemoveAt(inventoryIdx);

        // equippedGemIndicesのインデックスを修正（削除による番号ずれ対策）
        for (int i = 0; i < data.equippedGemIndices.Count; i++)
        {
            if (data.equippedGemIndices[i] > inventoryIdx)
                data.equippedGemIndices[i]--;
        }

        selectedGemIdx = -1; // 売却後は選択解除
        PlaySE(sellSE);
        ProgressManager.Instance.Save();
        RefreshGemList();
        gemSkillPreviewHUD?.Refresh();
    }

    // ========== Debug ==========

    private void DebugSetSlotLevel()
    {
        var data = ProgressManager.Instance?.Data;
        if (data == null) { Debug.LogError("[GemManagementUI] ProgressManager data is null!"); return; }

        data.slotLevel = debugSlotLevel;
        ProgressManager.Instance.Save();
        RefreshGemList();
        Debug.Log($"[GemManagementUI] Debug: slotLevel set to {debugSlotLevel}.");
    }

    private void DebugClearAllGems()
    {
        var data = ProgressManager.Instance?.Data;
        if (data == null) { Debug.LogError("[GemManagementUI] ProgressManager data is null!"); return; }

        data.gemInventory.Clear();
        data.equippedGemIndices.Clear();
        ProgressManager.Instance.Save();
        RefreshGemList();
        Debug.Log("[GemManagementUI] Debug: all gems cleared.");
    }

    /// <summary>出撃前警告のテスト用：残り使用回数1のジェムを1個ロールしてインベントリに追加する</summary>
    private void DebugAddLowUsesGem()
    {
        if (GemManager.Instance == null) { Debug.LogError("[GemManagementUI] GemManager not found!"); return; }

        var rolled = GemManager.Instance.RollGemsForArea(debugAddLowUsesGemAreaId, 1);
        if (rolled == null || rolled.Length == 0 || rolled[0] == null)
        {
            Debug.LogError($"[GemManagementUI] Debug: ジェムのロールに失敗しました（areaId={debugAddLowUsesGemAreaId}）。GemDefinitionが見つからない可能性があります。");
            return;
        }

        rolled[0].remainingUses = 1;
        bool added = GemManager.Instance.AddGemToInventory(rolled[0]);
        if (!added)
        {
            Debug.LogWarning("[GemManagementUI] Debug: インベントリが満杯のため追加できませんでした。");
            return;
        }

        RefreshGemList();
        Debug.Log("[GemManagementUI] Debug: 残り使用回数1のジェムを追加しました。");
    }

    private void DebugSetGoldMax()
    {
        if (GoldManager.Instance == null) { Debug.LogError("[GemManagementUI] GoldManager not found!"); return; }
        GoldManager.Instance.SetPersistentGold(debugGoldMaxValue);
        Debug.Log($"[GemManagementUI] Debug: PersistentGold set to {debugGoldMaxValue}.");
    }

    /// <summary>課金：ジェム使用回数無制限フラグをDebugでON/OFF切り替え</summary>
    private void DebugToggleUnlimitedGemUses()
    {
        var data = ProgressManager.Instance?.Data;
        if (data == null) { Debug.LogError("[GemManagementUI] ProgressManager data is null!"); return; }

        data.hasUnlimitedGemUses = !data.hasUnlimitedGemUses;
        ProgressManager.Instance.Save();
        UpdateDebugUnlimitedGemsButtonText();
        RefreshGemList();
        Debug.Log($"[GemManagementUI] Debug: hasUnlimitedGemUses = {data.hasUnlimitedGemUses}");
    }

    private void UpdateDebugUnlimitedGemsButtonText()
    {
        if (debugUnlimitedGemsButtonText == null) return;
        bool unlimited = ProgressManager.Instance?.Data?.hasUnlimitedGemUses ?? false;
        debugUnlimitedGemsButtonText.text = unlimited ? "無制限:ON" : "無制限:OFF";
    }

    private void DebugAddAllGems()
    {
        if (ProgressManager.Instance == null || GemManager.Instance == null)
        {
            Debug.LogError("[GemManagementUI] ProgressManager or GemManager not found.");
            return;
        }

        int added = 0;
        for (int i = 1; i <= 9; i++)
        {
            string areaId = $"Area_{i:D2}";
            if (GemManager.Instance.TryAddGemForArea(areaId, out _))
                added++;
            else
                Debug.LogWarning($"[GemManagementUI] Failed to add gem for {areaId} (inventory full or no definition)");
        }

        ProgressManager.Instance.Save();
        RefreshGemList();
        Debug.Log($"[GemManagementUI] Debug: Added {added}/9 gems.");
    }

    // ========== Default Gem Item Generator (Fallback) ==========

    private GameObject CreateDefaultGemItem()
    {
        // ルートアイテム
        var itemObj = new GameObject("GemItem");
        var itemRect = itemObj.AddComponent<RectTransform>();
        itemRect.sizeDelta = new Vector2(600f, 200f); // CreateGemItem で動的に上書きされる

        var itemLayout = itemObj.AddComponent<HorizontalLayoutGroup>();
        itemLayout.spacing = 8f;
        itemLayout.padding = new RectOffset(10, 10, 8, 8);
        itemLayout.childAlignment = TextAnchor.MiddleLeft;
        itemLayout.childControlWidth = true;
        itemLayout.childControlHeight = true;
        itemLayout.childForceExpandWidth = true;
        itemLayout.childForceExpandHeight = true;

        var itemBg = itemObj.AddComponent<Image>();
        itemBg.color = new Color(0.15f, 0.15f, 0.15f, 0.9f);

        var itemLE = itemObj.AddComponent<LayoutElement>();
        itemLE.preferredHeight = 180f;

        // テキストコンテナ（幅いっぱいに広げる）
        var textContainer = new GameObject("TextContainer");
        textContainer.transform.SetParent(itemObj.transform, false);

        var tcRect = textContainer.AddComponent<RectTransform>();
        tcRect.sizeDelta = new Vector2(500f, 164f);

        var tcLayout = textContainer.AddComponent<VerticalLayoutGroup>();
        tcLayout.spacing = 6f;
        tcLayout.childAlignment = TextAnchor.UpperLeft;
        tcLayout.childControlWidth = true;
        tcLayout.childControlHeight = true;
        tcLayout.childForceExpandWidth = true;
        tcLayout.childForceExpandHeight = false;

        var tcLE = textContainer.AddComponent<LayoutElement>();
        tcLE.flexibleWidth = 1f;

        // NameRow：ジェム名 ＋ 装備中バッジ（横並び）
        var nameRowObj = new GameObject("NameRow");
        nameRowObj.transform.SetParent(textContainer.transform, false);
        var nrLayout = nameRowObj.AddComponent<HorizontalLayoutGroup>();
        nrLayout.spacing = 6f;
        nrLayout.childAlignment = TextAnchor.MiddleLeft;
        nrLayout.childControlWidth = true;
        nrLayout.childControlHeight = true;
        nrLayout.childForceExpandWidth = false;
        nrLayout.childForceExpandHeight = true;
        var nrLE = nameRowObj.AddComponent<LayoutElement>();
        nrLE.preferredHeight = 28f;

        // 名前テキスト（NameRow 内、伸縮）
        var nameObj = new GameObject("NameText");
        nameObj.transform.SetParent(nameRowObj.transform, false);
        var nameTMP = nameObj.AddComponent<TextMeshProUGUI>();
        nameTMP.fontSize = 20f;
        nameTMP.color = Color.white;
        nameTMP.fontStyle = FontStyles.Bold;
        nameTMP.enableWordWrapping = false;
        nameTMP.alignment = TextAlignmentOptions.MidlineLeft;
        nameObj.AddComponent<LayoutElement>(); // minWidth/preferredWidth はTMPの自然サイズに任せる

        // スペーサー（NameTextとSlotDisplayTextの間を埋める）
        var spacerObj = new GameObject("Spacer");
        spacerObj.transform.SetParent(nameRowObj.transform, false);
        var spacerLE = spacerObj.AddComponent<LayoutElement>();
        spacerLE.flexibleWidth = 1f;

        // スロット表示テキスト（NameRow 内、右側固定）
        var slotDispObj = new GameObject("SlotDisplayText");
        slotDispObj.transform.SetParent(nameRowObj.transform, false);
        var slotDispTMP = slotDispObj.AddComponent<TextMeshProUGUI>();
        slotDispTMP.fontSize = 20f;
        slotDispTMP.color = Color.white;
        slotDispTMP.enableWordWrapping = false;
        slotDispTMP.alignment = TextAlignmentOptions.MidlineRight;
        var slotDispLE = slotDispObj.AddComponent<LayoutElement>();
        slotDispLE.minWidth = 80f;
        slotDispLE.preferredWidth = 80f;

        // 装備中バッジ（NameRow 内、右側固定）
        var badgeObj = new GameObject("EquippedBadge");
        badgeObj.transform.SetParent(nameRowObj.transform, false);
        var badgeTMP = badgeObj.AddComponent<TextMeshProUGUI>();
        badgeTMP.text = "[ 装備中 ]";
        badgeTMP.fontSize = 14f;
        badgeTMP.color = new Color(1f, 0.3f, 0.3f, 1f);
        badgeTMP.alignment = TextAlignmentOptions.MidlineRight;
        var badgeLE = badgeObj.AddComponent<LayoutElement>();
        badgeLE.minWidth = 80f;
        badgeLE.preferredWidth = 80f;
        badgeObj.SetActive(false);

        // スキルアイコンコンテナ（3行固定、Play前Inspector調整対応）
        var iconsContainerObj = new GameObject("SkillIconsContainer");
        iconsContainerObj.transform.SetParent(textContainer.transform, false);
        var iconsLayout = iconsContainerObj.AddComponent<VerticalLayoutGroup>();
        iconsLayout.spacing = 4f;
        iconsLayout.childAlignment = TextAnchor.UpperLeft;
        iconsLayout.childControlWidth = true;
        iconsLayout.childControlHeight = true;
        iconsLayout.childForceExpandWidth = true;
        iconsLayout.childForceExpandHeight = false;
        iconsContainerObj.AddComponent<LayoutElement>().preferredHeight = 128f; // CreateGemItem で動的に上書きされる

        // 3行を事前生成（Hierarchyに出てPlay前からInspectorで個別サイズ調整可能）
        CreateSkillRow("SkillRow_Base",   iconsContainerObj.transform);
        CreateSkillRow("SkillRow_Bonus1", iconsContainerObj.transform);
        CreateSkillRow("SkillRow_Bonus2", iconsContainerObj.transform);

        return itemObj;
    }

    private Button CreateItemButton(Transform parent, string name, string label, float height)
    {
        var btnObj = new GameObject(name);
        btnObj.transform.SetParent(parent, false);

        var btnRect = btnObj.AddComponent<RectTransform>();
        btnRect.sizeDelta = new Vector2(100f, height);

        var btnImage = btnObj.AddComponent<Image>();
        btnImage.color = new Color(0.25f, 0.25f, 0.35f, 1f);

        var btn = btnObj.AddComponent<Button>();

        var le = btnObj.AddComponent<LayoutElement>();
        le.preferredWidth = 100f;
        le.preferredHeight = height;

        var textObj = new GameObject("Text");
        textObj.transform.SetParent(btnObj.transform, false);

        var textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
        textRect.anchoredPosition = Vector2.zero;

        var tmp = textObj.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 20f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;

        return btn;
    }

    // ========== Editor Auto-Setup ==========

#if UNITY_EDITOR
    [ContextMenu("Setup HP Status HUD")]
    private void SetupHPStatusHUD()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) { Debug.LogError("[GemManagementUI] Canvas not found!"); return; }

        // 既存削除
        var existing = canvas.transform.Find("HPStatusHUD");
        if (existing != null) DestroyImmediate(existing.gameObject);

        const float rowWidth = 160f;
        const float rowHeight = 25f;
        const float rowSpacing = 5f;
        const float iconSize = 24f;
        const float fontSize = 14f;

        // HPStatusHUD 親オブジェクト（コンポーネントもここに置く）
        var hudObj = new GameObject("HPStatusHUD");
        hudObj.transform.SetParent(canvas.transform, false);

        var hudRect = hudObj.AddComponent<RectTransform>();
        hudRect.anchorMin = new Vector2(0f, 1f);
        hudRect.anchorMax = new Vector2(0f, 1f);
        hudRect.pivot = new Vector2(0.5f, 0.5f);
        hudRect.sizeDelta = new Vector2(rowWidth, rowHeight * 2f + rowSpacing);
        hudRect.anchoredPosition = new Vector2(120f, -148f);

        var vlg = hudObj.AddComponent<VerticalLayoutGroup>();
        vlg.childControlWidth = false;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = false;
        vlg.childForceExpandHeight = false;
        vlg.spacing = rowSpacing;
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.padding = new RectOffset(0, 0, 0, 0);

        // HPStatusHUDUI コンポーネントを同オブジェクトに追加
        var hpUI = hudObj.AddComponent<HPStatusHUDUI>();

        // PixelDancer HP 行
        var pdRow = CreateHPRowForHUD(hudObj.transform, "PixelDancerHPRow", rowWidth, rowHeight, iconSize, fontSize,
            out Image pdIcon, out TextMeshProUGUI pdText);

        // Floor HP 行
        var floorRow = CreateHPRowForHUD(hudObj.transform, "FloorHPRow", rowWidth, rowHeight, iconSize, fontSize,
            out Image floorIcon, out TextMeshProUGUI floorText);

        // HPStatusHUDUI フィールドを SerializedObject でアサイン
        var hpSO = new UnityEditor.SerializedObject(hpUI);
        hpSO.Update();
        hpSO.FindProperty("pixelDancerIcon").objectReferenceValue = pdIcon;
        hpSO.FindProperty("pixelDancerHPText").objectReferenceValue = pdText;
        hpSO.FindProperty("floorIcon").objectReferenceValue = floorIcon;
        hpSO.FindProperty("floorHPText").objectReferenceValue = floorText;
        hpSO.ApplyModifiedProperties();

        // 初期非表示（GemManagementUI.Open() で Show() する）
        hudObj.SetActive(false);

        // GemManagementUI の hpStatusHUD フィールドにアサイン
        var mySO = new UnityEditor.SerializedObject(this);
        mySO.Update();
        mySO.FindProperty("hpStatusHUD").objectReferenceValue = hpUI;
        mySO.ApplyModifiedProperties();

        UnityEditor.EditorUtility.SetDirty(this);
        UnityEditor.EditorUtility.SetDirty(hudObj);

        Debug.Log("[GemManagementUI] HPStatusHUD setup complete!\n" +
                  "次: HPStatusHUD > PixelDancerHPRow > Icon と FloorHPRow > Icon に Sprite をアサインしてください。\n" +
                  "HPStatusHUDUI の Fallback Values で 05_Game と同じ初期HP値を設定してください。");
    }

    private GameObject CreateHPRowForHUD(Transform parent, string rowName,
        float rowWidth, float rowHeight, float iconSize, float fontSize,
        out Image iconRef, out TextMeshProUGUI textRef)
    {
        float textWidth = rowWidth - iconSize - 5f;

        var row = new GameObject(rowName);
        row.transform.SetParent(parent, false);
        row.AddComponent<RectTransform>().sizeDelta = new Vector2(rowWidth, rowHeight);

        var hlg = row.AddComponent<HorizontalLayoutGroup>();
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.spacing = 5f;

        var iconObj = new GameObject("Icon");
        iconObj.transform.SetParent(row.transform, false);
        iconObj.AddComponent<RectTransform>().sizeDelta = new Vector2(iconSize, iconSize);
        iconRef = iconObj.AddComponent<Image>();
        iconRef.color = Color.white;
        iconRef.preserveAspect = true;

        var textObj = new GameObject("HPText");
        textObj.transform.SetParent(row.transform, false);
        textObj.AddComponent<RectTransform>().sizeDelta = new Vector2(textWidth, rowHeight);
        textRef = textObj.AddComponent<TextMeshProUGUI>();
        textRef.fontSize = fontSize;
        textRef.color = Color.white;
        textRef.alignment = TextAlignmentOptions.MidlineLeft;
        textRef.text = "5/5";

        return row;
    }

    [ContextMenu("Setup Sell Confirm Dialog")]
    private void SetupSellConfirmDialog()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) { Debug.LogError("[GemManagementUI] Canvas not found!"); return; }

        // 既存削除
        var existing = canvas.transform.Find("SellConfirmOverlay");
        if (existing != null) DestroyImmediate(existing.gameObject);

        // オーバーレイ（全画面暗転）
        var overlayObj = new GameObject("SellConfirmOverlay");
        overlayObj.transform.SetParent(canvas.transform, false);
        var overlayRect = overlayObj.AddComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.sizeDelta = Vector2.zero;
        var overlayImg = overlayObj.AddComponent<Image>();
        overlayImg.color = new Color(0f, 0f, 0f, 0.6f);
        overlayObj.SetActive(false);

        // ダイアログパネル
        var dialogObj = new GameObject("SellConfirmDialog");
        dialogObj.transform.SetParent(overlayObj.transform, false);
        var dialogRect = dialogObj.AddComponent<RectTransform>();
        dialogRect.anchorMin = new Vector2(0.5f, 0.5f);
        dialogRect.anchorMax = new Vector2(0.5f, 0.5f);
        dialogRect.sizeDelta = new Vector2(420f, 200f);
        // SkillHUD(Width:280)を除いた残りエリアの中央に配置: 280 / 2 = 140
        dialogRect.anchoredPosition = new Vector2(140f, 0f);
        var dialogBg = dialogObj.AddComponent<Image>();
        dialogBg.color = new Color(0.1f, 0.1f, 0.15f, 1f);
        var dialogLayout = dialogObj.AddComponent<VerticalLayoutGroup>();
        dialogLayout.spacing = 16f;
        dialogLayout.padding = new RectOffset(24, 24, 24, 24);
        dialogLayout.childAlignment = TextAnchor.MiddleCenter;
        dialogLayout.childControlWidth = true;
        dialogLayout.childControlHeight = true;
        dialogLayout.childForceExpandWidth = true;
        dialogLayout.childForceExpandHeight = false;

        // 確認テキスト
        var textObj = new GameObject("ConfirmText");
        textObj.transform.SetParent(dialogObj.transform, false);
        var confirmTMP = textObj.AddComponent<TextMeshProUGUI>();
        confirmTMP.text = "このジェムを売却しますか？\n¥0";
        confirmTMP.fontSize = 20f;
        confirmTMP.alignment = TextAlignmentOptions.Center;
        confirmTMP.color = Color.white;
        var textLE = textObj.AddComponent<LayoutElement>();
        textLE.preferredHeight = 64f;

        // ボタン行
        var btnRowObj = new GameObject("ButtonRow");
        btnRowObj.transform.SetParent(dialogObj.transform, false);
        var btnRowLayout = btnRowObj.AddComponent<HorizontalLayoutGroup>();
        btnRowLayout.spacing = 24f;
        btnRowLayout.childAlignment = TextAnchor.MiddleCenter;
        btnRowLayout.childControlWidth = false;
        btnRowLayout.childControlHeight = true;
        btnRowLayout.childForceExpandHeight = true;
        var btnRowLE = btnRowObj.AddComponent<LayoutElement>();
        btnRowLE.preferredHeight = 52f;

        // 売るボタン
        var yesBtn = CreateConfirmButton(btnRowObj.transform, "YesButton", "売　る", new Color(0.6f, 0.15f, 0.15f, 1f));

        // やめるボタン
        var noBtn = CreateConfirmButton(btnRowObj.transform, "NoButton", "やめる", new Color(0.25f, 0.25f, 0.35f, 1f));

        // SerializedObjectでアサイン
        var so = new UnityEditor.SerializedObject(this);
        so.Update();
        so.FindProperty("sellConfirmPanel").objectReferenceValue = overlayObj;
        so.FindProperty("sellConfirmText").objectReferenceValue = confirmTMP;
        so.FindProperty("sellConfirmYesBtn").objectReferenceValue = yesBtn;
        so.FindProperty("sellConfirmNoBtn").objectReferenceValue = noBtn;
        so.ApplyModifiedProperties();

        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log("[GemManagementUI] SellConfirmDialog created!");
    }

    [ContextMenu("Setup Confirm Bg Objects")]
    private void SetupConfirmBgObjects()
    {
        // SellConfirmDialog を探す
        if (sellConfirmPanel == null)
        {
            Debug.LogError("[GemManagementUI] sellConfirmPanel が未アサインです。先に Setup Sell Confirm Dialog を実行してください。");
            return;
        }
        var dialog = sellConfirmPanel.transform.Find("SellConfirmDialog");
        if (dialog == null)
        {
            Debug.LogError("[GemManagementUI] SellConfirmDialog が見つかりません。");
            return;
        }

        // ① SellConfirmBg（SellConfirmDialog直下、背面に配置）
        var existingDialogBg = dialog.Find("SellConfirmBg");
        if (existingDialogBg != null) DestroyImmediate(existingDialogBg.gameObject);

        var dialogBgObj = new GameObject("SellConfirmBg");
        dialogBgObj.transform.SetParent(dialog, false);
        var dialogBgRect = dialogBgObj.AddComponent<RectTransform>();
        dialogBgRect.anchorMin = Vector2.zero;
        dialogBgRect.anchorMax = Vector2.one;
        dialogBgRect.sizeDelta = Vector2.zero;
        dialogBgRect.anchoredPosition = Vector2.zero;
        var dialogBgImg = dialogBgObj.AddComponent<Image>();
        dialogBgImg.raycastTarget = false;
        dialogBgObj.transform.SetAsFirstSibling();
        Debug.Log("[GemManagementUI] SellConfirmBg を作成しました。");

        // ② YesBg（YesButton直下）
        if (sellConfirmYesBtn == null) { Debug.LogError("[GemManagementUI] sellConfirmYesBtn が未アサインです。"); return; }
        var existingYesBg = sellConfirmYesBtn.transform.Find("YesBg");
        if (existingYesBg != null) DestroyImmediate(existingYesBg.gameObject);

        var yesBgObj = new GameObject("YesBg");
        yesBgObj.transform.SetParent(sellConfirmYesBtn.transform, false);
        var yesBgRect = yesBgObj.AddComponent<RectTransform>();
        yesBgRect.anchorMin = new Vector2(0.5f, 0.5f);
        yesBgRect.anchorMax = new Vector2(0.5f, 0.5f);
        yesBgRect.sizeDelta = new Vector2(160f, 52f);
        yesBgRect.anchoredPosition = Vector2.zero;
        var yesBgImg = yesBgObj.AddComponent<Image>();
        yesBgImg.raycastTarget = false;
        yesBgObj.transform.SetAsFirstSibling();
        Debug.Log("[GemManagementUI] YesBg を作成しました。");

        // ③ NoBg（NoButton直下）
        if (sellConfirmNoBtn == null) { Debug.LogError("[GemManagementUI] sellConfirmNoBtn が未アサインです。"); return; }
        var existingNoBg = sellConfirmNoBtn.transform.Find("NoBg");
        if (existingNoBg != null) DestroyImmediate(existingNoBg.gameObject);

        var noBgObj = new GameObject("NoBg");
        noBgObj.transform.SetParent(sellConfirmNoBtn.transform, false);
        var noBgRect = noBgObj.AddComponent<RectTransform>();
        noBgRect.anchorMin = new Vector2(0.5f, 0.5f);
        noBgRect.anchorMax = new Vector2(0.5f, 0.5f);
        noBgRect.sizeDelta = new Vector2(160f, 52f);
        noBgRect.anchoredPosition = Vector2.zero;
        var noBgImg = noBgObj.AddComponent<Image>();
        noBgImg.raycastTarget = false;
        noBgObj.transform.SetAsFirstSibling();
        Debug.Log("[GemManagementUI] NoBg を作成しました。");

        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log("[GemManagementUI] Confirm Bg Objects セットアップ完了！");
    }

    private Button CreateConfirmButton(Transform parent, string name, string label, Color bgColor)
    {
        var btnObj = new GameObject(name);
        btnObj.transform.SetParent(parent, false);
        var btnRect = btnObj.AddComponent<RectTransform>();
        btnRect.sizeDelta = new Vector2(160f, 52f);
        var btnImg = btnObj.AddComponent<Image>();
        btnImg.color = bgColor;
        var btn = btnObj.AddComponent<Button>();
        btnObj.AddComponent<LayoutElement>().preferredWidth = 160f;

        var textObj = new GameObject("Text");
        textObj.transform.SetParent(btnObj.transform, false);
        var textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
        var tmp = textObj.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 20f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;

        return btn;
    }

    [ContextMenu("Setup Gem Item Template")]
    private void SetupGemItemTemplate()
    {
        // 既存のテンプレートを削除
        var existing = transform.Find("GemItemTemplate");
        if (existing != null)
        {
            DestroyImmediate(existing.gameObject);
            Debug.Log("[GemManagementUI] Removed existing GemItemTemplate.");
        }

        // テンプレートを生成（CreateDefaultGemItemと同じ構造）
        var templateObj = CreateDefaultGemItem();
        templateObj.name = "GemItemTemplate";
        templateObj.transform.SetParent(transform, false);
        templateObj.SetActive(false); // 非表示テンプレート

        // gemItemTemplateフィールドにアサイン
        var so = new UnityEditor.SerializedObject(this);
        so.Update();
        so.FindProperty("gemItemTemplate").objectReferenceValue = templateObj;
        so.ApplyModifiedProperties();

        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log("[GemManagementUI] GemItemTemplate created! Adjust font sizes in Inspector, then save.");
    }

    /// <summary>
    /// GemItemTemplate内のGemIcon（ジェムマーク画像）にLayoutElementが無く、
    /// HorizontalLayoutGroupがスプライトの元画像サイズ（612x408px）をそのまま使おうとして
    /// ジェム名の文字数次第で横長に歪んで見える問題を修正する。
    /// GemIconは削除・再生成せず、LayoutElementのmin/preferredサイズを固定するだけの非破壊修正。
    /// ★サイズは元画像の比率（612:408=1.5:1）を保ったまま、GemIconが入っているNameRowの
    /// 固定高さ（28px、CreateGemItem()参照）に収まる値（36x24）に固定する。
    /// 以前はGemIconの既存sizeDelta（50x40）をそのまま使っていたが、これはNameRowの高さより
    /// 大きくGemIconが上下にはみ出す原因になっていた（子のUsesBadgeがずれて見える不具合の元凶）。
    /// </summary>
    [ContextMenu("Fix GemIcon Size (歪み修正・サイズ固定)")]
    private void FixGemIconSize()
    {
        if (gemItemTemplate == null)
        {
            Debug.LogError("[GemManagementUI] gemItemTemplate is not assigned.");
            return;
        }

        var gemIconTrans = gemItemTemplate.transform.Find("TextContainer/NameRow/GemIcon");
        if (gemIconTrans == null)
        {
            Debug.LogError("[GemManagementUI] GemIcon not found under gemItemTemplate (TextContainer/NameRow/GemIcon).");
            return;
        }

        // NameRowの固定高さ(28px)に収まる範囲でできるだけ大きくしたサイズ（元画像比率612:408=1.5:1に近似）
        const float fixedWidth = 40f;
        const float fixedHeight = 27f;

        // ★実機調査の結果、親のHorizontalLayoutGroupはLayoutElementの値を反映しておらず、
        // RectTransform.sizeDelta の生の値がそのまま描画に使われていることが判明した。
        // そのため LayoutElement だけでなく RectTransform.sizeDelta 自体も直接書き換える。
        var gemIconRect = gemIconTrans.GetComponent<RectTransform>();
        if (gemIconRect != null)
            gemIconRect.sizeDelta = new Vector2(fixedWidth, fixedHeight);

        var le = gemIconTrans.GetComponent<LayoutElement>();
        if (le == null) le = gemIconTrans.gameObject.AddComponent<LayoutElement>();
        le.minWidth = fixedWidth;
        le.preferredWidth = fixedWidth;
        le.minHeight = fixedHeight;
        le.preferredHeight = fixedHeight;
        le.flexibleWidth = 0f;
        le.flexibleHeight = 0f;

        var image = gemIconTrans.GetComponent<Image>();
        if (image != null) image.preserveAspect = true;

        UnityEditor.EditorUtility.SetDirty(gemItemTemplate);
        Debug.Log($"[GemManagementUI] GemIcon size fixed to {fixedWidth}x{fixedHeight} (RectTransform.sizeDelta + LayoutElement両方を設定 + preserveAspect ON). ジェム名の長さに関わらずサイズが固定されます。");
    }

    /// <summary>
    /// GemIconの右下に、残り使用回数を表示する小さなバッジ（背景チップ＋数字）を追加する。
    /// GemIcon自体は削除・再生成せず、子として追加するだけの非破壊修正。
    /// 既に存在する場合は一旦削除してから作り直す（再実行可能）。
    /// </summary>
    [ContextMenu("Add Gem Uses Badge (残り使用回数バッジ追加)")]
    private void AddGemUsesBadge()
    {
        if (gemItemTemplate == null)
        {
            Debug.LogError("[GemManagementUI] gemItemTemplate is not assigned.");
            return;
        }

        var gemIconTrans = gemItemTemplate.transform.Find("TextContainer/NameRow/GemIcon");
        if (gemIconTrans == null)
        {
            Debug.LogError("[GemManagementUI] GemIcon not found under gemItemTemplate (TextContainer/NameRow/GemIcon).");
            return;
        }

        var existing = gemIconTrans.Find("UsesBadge");
        if (existing != null) DestroyImmediate(existing.gameObject);

        var badgeObj = new GameObject("UsesBadge");
        badgeObj.transform.SetParent(gemIconTrans, false);
        var badgeRect = badgeObj.AddComponent<RectTransform>();
        badgeRect.anchorMin = new Vector2(1f, 0f);
        badgeRect.anchorMax = new Vector2(1f, 0f);
        badgeRect.pivot = new Vector2(1f, 0f);
        badgeRect.sizeDelta = new Vector2(28f, 17f);
        badgeRect.anchoredPosition = new Vector2(2f, -2f);

        var badgeBg = badgeObj.AddComponent<Image>();
        badgeBg.color = new Color(0f, 0f, 0f, 0.75f);
        badgeBg.raycastTarget = false;

        var textObj = new GameObject("Text");
        textObj.transform.SetParent(badgeObj.transform, false);
        var textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;

        var tmp = textObj.AddComponent<TextMeshProUGUI>();
        tmp.text = "30";
        tmp.fontSize = 18f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = normalUsesBadgeColor;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        tmp.enableWordWrapping = false;

        var fontAsset = UnityEditor.AssetDatabase.FindAssets("t:TMP_FontAsset NotoSansJP-Regular")
            .Select(guid => UnityEditor.AssetDatabase.GUIDToAssetPath(guid))
            .Select(path => UnityEditor.AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>(path))
            .FirstOrDefault();
        if (fontAsset != null) tmp.font = fontAsset;

        UnityEditor.EditorUtility.SetDirty(gemItemTemplate);
        Debug.Log("[GemManagementUI] UsesBadge added under GemIcon (TextContainer/NameRow/GemIcon/UsesBadge/Text).");
    }

    /// <summary>
    /// 装備/売却/閉じるボタンを、AreaSelect本体と同じ「ネオン枠.png + アイコン単体画像」の
    /// 2層構成に作り直す。装備ボタンは装備/解除/未選択の状態を、枠の色＋下部テキストで表現する
    /// （以前はGemEquip/GemUnequip/GemEuip0の3種の画像差し替えだったが、アイコンが単体画像になった
    /// ため色とテキストに変更）。売却ボタンは既存の「売却」テキストを残す。閉じるボタンはEXITアイコン
    /// 自体に文字が含まれるため、既存の「閉じる」テキストは非表示にする。
    /// </summary>
    [ContextMenu("Rebuild Action Button Visuals (装備/売却/閉じるをネオン枠+アイコンに作り直す)")]
    private void RebuildActionButtonVisuals()
    {
        // ★再実行時に古い画像のまま更新されない事故を防ぐため、枠画像は常に最新パスを読み込み直す
        neonFrameSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/AreaSelect/Shop/新ネオン枠.png");
        if (gemIconSprite == null)
            gemIconSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/AreaSelect/ジェムアイコン.png");
        if (sellIconSprite == null)
            sellIconSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/AreaSelect/Shop/コイン袋アイコン.png");
        if (exitIconSprite == null)
            exitIconSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/AreaSelect/Shop/EXITアイコン.png");

        if (neonFrameSprite == null)
        {
            Debug.LogError("[GemManagementUI] 新ネオン枠.pngが見つかりません。");
            return;
        }

        // 装備ボタン
        if (sharedEquipButton != null && sharedEquipButtonImage != null)
        {
            sharedEquipButtonImage.sprite = neonFrameSprite;
            sharedEquipButtonImage.type = Image.Type.Simple;
            sharedEquipButtonImage.preserveAspect = false;
            sharedEquipButtonIcon = SetupActionIcon(sharedEquipButton.transform, "Icon", gemIconSprite, actionIconSizeWithText, actionIconOffsetYWithText);
            sharedEquipButtonStateText = SetupActionStateText(sharedEquipButton.transform, "StateText");
        }
        else
        {
            Debug.LogWarning("[GemManagementUI] sharedEquipButton/sharedEquipButtonImageが未設定のためスキップしました。");
        }

        // 売却ボタン（既存の「売却」テキストは残す）
        if (sharedSellButton != null && sharedSellBgImage != null)
        {
            sharedSellBgImage.sprite = neonFrameSprite;
            sharedSellBgImage.type = Image.Type.Simple;
            sharedSellBgImage.preserveAspect = false;
            sharedSellButtonIcon = SetupActionIcon(sharedSellButton.transform, "Icon", sellIconSprite, actionIconSizeWithText, actionIconOffsetYWithText);

            // ★SharedSellButtonのButtonHoverEffectがsharedSellBgImageと同じ画像を点滅対象にしているため、
            //   売却不可(interactable=false)の間はホバー効果を止めて、UpdateSharedButtons()の
            //   暗色設定と競合しないようにする（ShopUIの購入ボタンと同じ対応）。
            var sellHoverEffect = sharedSellButton.GetComponent<ButtonHoverEffect>();
            if (sellHoverEffect != null)
            {
                var so2 = new UnityEditor.SerializedObject(sellHoverEffect);
                so2.FindProperty("requireInteractable").boolValue = true;
                so2.ApplyModifiedPropertiesWithoutUndo();
            }
        }
        else
        {
            Debug.LogWarning("[GemManagementUI] sharedSellButton/sharedSellBgImageが未設定のためスキップしました。");
        }

        // 閉じるボタン（実際の枠画像は子の"CloseBg"にあり、ルート自身にはImageが無いため
        // "CloseBg"を直接探して差し替える。EXITアイコンに文字が含まれるため、
        // 既存の「閉じる」テキストは非表示にする）
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
                Debug.LogWarning("[GemManagementUI] CloseButtonの枠Imageが見つかりませんでした（CloseBg子/ルートImageともに無し）。");
            }
            closeButtonIcon = SetupActionIcon(closeButton.transform, "Icon", exitIconSprite, actionIconSizeNoText, 0f);

            var existingText = closeButton.transform.Find("Text");
            if (existingText != null) existingText.gameObject.SetActive(false);
        }
        else
        {
            Debug.LogWarning("[GemManagementUI] closeButtonが未設定のためスキップしました。");
        }

        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log("[GemManagementUI] Action button visuals rebuilt (neon frame + icon).");
    }

    // ★childNameのアイコンが既に存在する場合、サイズ・位置は一切変更しない（手動調整を保護するため）。
    //   新規作成の時だけデフォルトのサイズ・位置を設定する。
    private Image SetupActionIcon(Transform buttonTransform, string childName, Sprite iconSprite, float size, float offsetY)
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
            rt.anchoredPosition = new Vector2(0f, offsetY);
        }

        var img = iconGo.GetComponent<Image>();
        img.sprite = iconSprite;
        img.preserveAspect = true;
        img.raycastTarget = false;
        return img;
    }

    // ★既存の場合はサイズ・位置を変更しない（手動調整を保護するため）。新規作成時のみデフォルト配置する。
    private TextMeshProUGUI SetupActionStateText(Transform buttonTransform, string childName)
    {
        var existing = buttonTransform.Find(childName);
        bool isNew = existing == null;
        GameObject textGo = isNew ? new GameObject(childName, typeof(RectTransform)) : existing.gameObject;
        var rt = (RectTransform)textGo.transform;

        if (isNew)
        {
            rt.SetParent(buttonTransform, false);
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(0f, 22f);
            rt.anchoredPosition = new Vector2(0f, 2f);
        }

        var tmp = textGo.GetComponent<TextMeshProUGUI>();
        if (tmp == null) tmp = textGo.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = 18f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.raycastTarget = false;

        var fontAsset = UnityEditor.AssetDatabase.FindAssets("t:TMP_FontAsset NotoSansJP-Regular")
            .Select(guid => UnityEditor.AssetDatabase.GUIDToAssetPath(guid))
            .Select(path => UnityEditor.AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>(path))
            .FirstOrDefault();
        if (fontAsset != null) tmp.font = fontAsset;

        return tmp;
    }

    /// <summary>
    /// 装備/売却ボタン行（SlotActionRow）のすぐ下に、残り使用回数が少ない選択中ジェムへの
    /// 警告テキストを追加する。SlotActionRowの親（gemPanel）が持つVerticalLayoutGroupに
    /// 乗せるだけなので、既存レイアウトを壊さない。sharedEquipButtonの実参照から親を辿るため、
    /// Hierarchyパスのハードコードに依存しない。
    /// </summary>
    [ContextMenu("Add Low Uses Warning Text (残り回数少警告テキスト追加)")]
    private void AddLowUsesWarningText()
    {
        if (sharedEquipButton == null)
        {
            Debug.LogError("[GemManagementUI] sharedEquipButton is not assigned. Cannot locate SlotActionRow.");
            return;
        }

        var slotRow = sharedEquipButton.transform.parent;
        var panelParent = slotRow != null ? slotRow.parent : null;
        if (slotRow == null || panelParent == null)
        {
            Debug.LogError("[GemManagementUI] Could not resolve SlotActionRow/gemPanel from sharedEquipButton.");
            return;
        }

        var existing = panelParent.Find("LowUsesWarningText");
        if (existing != null) DestroyImmediate(existing.gameObject);

        // ★コンテナ（アイコン＋テキストを横並びにし、まとめて表示/非表示・パルスさせる）
        var warnObj = new GameObject("LowUsesWarningText");
        warnObj.transform.SetParent(panelParent, false);
        warnObj.transform.SetSiblingIndex(slotRow.GetSiblingIndex() + 1);
        lowUsesWarningContainer = warnObj;

        warnObj.AddComponent<LayoutElement>().preferredHeight = LowUsesWarningRowHeight;
        lowUsesWarningCanvasGroup = warnObj.AddComponent<CanvasGroup>();
        lowUsesWarningCanvasGroup.interactable = false;
        lowUsesWarningCanvasGroup.blocksRaycasts = false;
        var hlg = warnObj.AddComponent<HorizontalLayoutGroup>();
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.spacing = 6f;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;

        // ★警告アイコン（黄色い丸に！マーク、コードで生成。外部アセット不要）
        var iconObj = new GameObject("WarningIcon", typeof(RectTransform), typeof(Image));
        iconObj.transform.SetParent(warnObj.transform, false);
        var iconRt = (RectTransform)iconObj.transform;
        iconRt.sizeDelta = new Vector2(28f, 28f);
        lowUsesWarningIcon = iconObj.GetComponent<Image>();
        lowUsesWarningIcon.sprite = CreateWarningIconSprite();
        lowUsesWarningIcon.raycastTarget = false;
        // ★アイコンの見た目が良くないとのことで非表示に。参照は残すのでInspectorで
        //   WarningIconのGameObjectをActiveにすればいつでも復活できる。
        iconObj.SetActive(false);

        var textObj = new GameObject("Text", typeof(RectTransform));
        textObj.transform.SetParent(warnObj.transform, false);
        ((RectTransform)textObj.transform).sizeDelta = new Vector2(360f, 36f);

        lowUsesWarningText = textObj.AddComponent<TextMeshProUGUI>();
        lowUsesWarningText.text = string.Format(lowUsesWarningFormat, lowUsesThreshold);
        lowUsesWarningText.fontSize = 24f;
        lowUsesWarningText.color = lowUsesBadgeColor;
        lowUsesWarningText.alignment = TextAlignmentOptions.Center;

        var fontAsset = UnityEditor.AssetDatabase.FindAssets("t:TMP_FontAsset NotoSansJP-Regular")
            .Select(guid => UnityEditor.AssetDatabase.GUIDToAssetPath(guid))
            .Select(path => UnityEditor.AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>(path))
            .FirstOrDefault();
        if (fontAsset != null) lowUsesWarningText.font = fontAsset;

        // ★containerは常にActiveのままにしてレイアウト上の縦幅を最初から確保する。
        //   非表示状態はCanvasGroup.alpha=0で表現する（SetActive(false)は使わない）。
        warnObj.SetActive(true);
        lowUsesWarningCanvasGroup.alpha = 0f;

        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log("[GemManagementUI] LowUsesWarningText (icon + text) added below SlotActionRow.");
    }

    /// <summary>黄色い丸に！マークの警告アイコンをコードで生成する（外部アセット不要）</summary>
    private Sprite CreateWarningIconSprite()
    {
        const int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.hideFlags = HideFlags.DontSave;

        Color bg = new Color(1f, 0.82f, 0.1f, 1f);
        Color mark = new Color(0.2f, 0.08f, 0f, 1f);
        Vector2 center = new Vector2(size / 2f, size / 2f);
        float radius = size * 0.46f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                Color c = Color.clear;
                if (dist <= radius)
                {
                    c = bg;
                    float relX = (x - center.x) / radius;
                    float relY = (y - center.y) / radius; // 上がプラス
                    // ！マーク：棒は上寄り、点は下寄り（テクスチャ座標はy上向きがプラス）
                    bool inBar = Mathf.Abs(relX) < 0.12f && relY > -0.05f && relY < 0.5f;
                    bool inDot = Mathf.Abs(relX) < 0.12f && relY > -0.46f && relY < -0.25f;
                    if (inBar || inDot) c = mark;
                }
                tex.SetPixel(x, y, c);
            }
        }
        tex.Apply();

        var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        sprite.hideFlags = HideFlags.DontSave;
        return sprite;
    }

    // [ContextMenu("Setup Gem Management UI")] // 誤実行防止のため非表示
    private void SetupGemManagementUI()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) { Debug.LogError("[GemManagementUI] Canvas not found!"); return; }

        CreateDimPanel(canvas.transform);
        CreateGemPanel(canvas.transform);

        // SerializedObjectでInspector参照を設定
        var so = new UnityEditor.SerializedObject(this);
        so.Update();
        so.FindProperty("dimPanel").objectReferenceValue = dimPanel;
        so.FindProperty("gemPanel").objectReferenceValue = gemPanel;
        so.FindProperty("titleText").objectReferenceValue = titleText;
        so.FindProperty("slotLevelText").objectReferenceValue = slotLevelText;
        so.FindProperty("closeButton").objectReferenceValue = closeButton;
        so.FindProperty("gemListContainer").objectReferenceValue = gemListContainer;
        so.FindProperty("emptyMessageText").objectReferenceValue = emptyMessageText;
        so.FindProperty("sharedEquipButton").objectReferenceValue = sharedEquipButton;
        so.FindProperty("sharedSellButton").objectReferenceValue = sharedSellButton;
        so.ApplyModifiedProperties();
        UnityEditor.EditorUtility.SetDirty(this);

        Debug.Log("[GemManagementUI] Setup complete! Add a button to AreaSelect and call Open() from onClick.");
    }

    [ContextMenu("Setup Debug Add Gems Button")]
    private void SetupDebugAddGemsButton()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) { Debug.LogError("[GemManagementUI] Canvas not found!"); return; }

        // 既存削除（gemPanel内の古いものも含めて削除）
        var oldInPanel = gemPanel != null ? gemPanel.transform.Find("DebugAddGemsButton") : null;
        if (oldInPanel != null) DestroyImmediate(oldInPanel.gameObject);
        var oldInCanvas = canvas.transform.Find("DebugAddGemsButton");
        if (oldInCanvas != null) DestroyImmediate(oldInCanvas.gameObject);

        // Canvas直下に生成（パネルの外）
        var btnObj = new GameObject("DebugAddGemsButton");
        btnObj.transform.SetParent(canvas.transform, false);

        var btnRect = btnObj.AddComponent<RectTransform>();
        btnRect.anchorMin        = new Vector2(0.5f, 0.5f);
        btnRect.anchorMax        = new Vector2(0.5f, 0.5f);
        btnRect.pivot            = new Vector2(0.5f, 0.5f);
        btnRect.anchoredPosition = debugButtonPosition;
        btnRect.sizeDelta        = debugButtonSize;

        var btnImg = btnObj.AddComponent<Image>();
        btnImg.color = new Color(0.5f, 0.1f, 0.5f, 1f); // 紫：デバッグ用とわかるように

        var btn = btnObj.AddComponent<Button>();

        var textObj = new GameObject("Text");
        textObj.transform.SetParent(btnObj.transform, false);
        var textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
        var tmp = textObj.AddComponent<TMPro.TextMeshProUGUI>();
        tmp.text = "ジェム取得";
        tmp.fontSize = 20f;
        tmp.alignment = TMPro.TextAlignmentOptions.Center;
        tmp.color = Color.white;

        // Inspector にアサイン
        var so = new UnityEditor.SerializedObject(this);
        so.Update();
        so.FindProperty("debugAddGemsButton").objectReferenceValue = btn;
        so.ApplyModifiedProperties();

        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log("[GemManagementUI] Debug button created outside gemPanel! Adjust 'Debug Button Position/Size' in Inspector, then re-run this menu.");
    }

    [ContextMenu("Setup Debug Slot Level Button")]
    private void SetupDebugSlotLevelButton()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) { Debug.LogError("[GemManagementUI] Canvas not found!"); return; }

        // 既存削除
        var oldInCanvas = canvas.transform.Find("DebugSlotLevelButton");
        if (oldInCanvas != null) DestroyImmediate(oldInCanvas.gameObject);

        // Canvas直下に生成
        var btnObj = new GameObject("DebugSlotLevelButton");
        btnObj.transform.SetParent(canvas.transform, false);

        var btnRect = btnObj.AddComponent<RectTransform>();
        btnRect.anchorMin        = new Vector2(0.5f, 0.5f);
        btnRect.anchorMax        = new Vector2(0.5f, 0.5f);
        btnRect.pivot            = new Vector2(0.5f, 0.5f);
        btnRect.anchoredPosition = debugSlotButtonPosition;
        btnRect.sizeDelta        = debugSlotButtonSize;

        var btnImg = btnObj.AddComponent<Image>();
        btnImg.color = new Color(0.1f, 0.4f, 0.5f, 1f); // 青緑：全ジェム取得ボタンと区別

        var btn = btnObj.AddComponent<Button>();

        var textObj = new GameObject("Text");
        textObj.transform.SetParent(btnObj.transform, false);
        var textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
        var tmp = textObj.AddComponent<TMPro.TextMeshProUGUI>();
        tmp.text = "スロットUP";
        tmp.fontSize = 18f;
        tmp.alignment = TMPro.TextAlignmentOptions.Center;
        tmp.color = Color.white;

        var so = new UnityEditor.SerializedObject(this);
        so.Update();
        so.FindProperty("debugSlotLevelButton").objectReferenceValue = btn;
        so.ApplyModifiedProperties();

        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log($"[GemManagementUI] Debug slot button created! Adjust 'Debug Slot Button Position/Size' and 'Debug Slot Level' in Inspector, then re-run.");
    }

    [ContextMenu("Setup Debug Clear Gems Button")]
    private void SetupDebugClearGemsButton()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) { Debug.LogError("[GemManagementUI] Canvas not found!"); return; }

        var oldInCanvas = canvas.transform.Find("DebugClearGemsButton");
        if (oldInCanvas != null) DestroyImmediate(oldInCanvas.gameObject);

        var btnObj = new GameObject("DebugClearGemsButton");
        btnObj.transform.SetParent(canvas.transform, false);

        var btnRect = btnObj.AddComponent<RectTransform>();
        btnRect.anchorMin        = new Vector2(0.5f, 0.5f);
        btnRect.anchorMax        = new Vector2(0.5f, 0.5f);
        btnRect.pivot            = new Vector2(0.5f, 0.5f);
        btnRect.anchoredPosition = debugClearButtonPosition;
        btnRect.sizeDelta        = debugClearButtonSize;

        var btnImg = btnObj.AddComponent<Image>();
        btnImg.color = new Color(0.5f, 0.1f, 0.1f, 1f); // 赤：削除ボタンの識別用

        var btn = btnObj.AddComponent<Button>();

        var textObj = new GameObject("Text");
        textObj.transform.SetParent(btnObj.transform, false);
        var textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
        var tmp = textObj.AddComponent<TMPro.TextMeshProUGUI>();
        tmp.text = "ジェム全削除";
        tmp.fontSize = 18f;
        tmp.alignment = TMPro.TextAlignmentOptions.Center;
        tmp.color = Color.white;

        var so = new UnityEditor.SerializedObject(this);
        so.Update();
        so.FindProperty("debugClearGemsButton").objectReferenceValue = btn;
        so.ApplyModifiedProperties();

        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log("[GemManagementUI] Debug clear gems button created!");
    }

    [ContextMenu("Setup Debug Gold Max Button")]
    private void SetupDebugGoldMaxButton()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) { Debug.LogError("[GemManagementUI] Canvas not found!"); return; }

        var oldInCanvas = canvas.transform.Find("DebugGoldMaxButton");
        if (oldInCanvas != null) DestroyImmediate(oldInCanvas.gameObject);

        var btnObj = new GameObject("DebugGoldMaxButton");
        btnObj.transform.SetParent(canvas.transform, false);

        var btnRect = btnObj.AddComponent<RectTransform>();
        btnRect.anchorMin        = new Vector2(0.5f, 0.5f);
        btnRect.anchorMax        = new Vector2(0.5f, 0.5f);
        btnRect.pivot            = new Vector2(0.5f, 0.5f);
        btnRect.anchoredPosition = debugGoldMaxButtonPosition;
        btnRect.sizeDelta        = debugGoldMaxButtonSize;

        var btnImg = btnObj.AddComponent<Image>();
        btnImg.color = new Color(0.8f, 0.6f, 0.1f, 1f); // 金色：ゴールドボタンの識別用

        var btn = btnObj.AddComponent<Button>();

        var textObj = new GameObject("Text");
        textObj.transform.SetParent(btnObj.transform, false);
        var textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
        var tmp = textObj.AddComponent<TMPro.TextMeshProUGUI>();
        tmp.text = "Gold Max";
        tmp.fontSize = 18f;
        tmp.alignment = TMPro.TextAlignmentOptions.Center;
        tmp.color = Color.white;

        var so = new UnityEditor.SerializedObject(this);
        so.Update();
        so.FindProperty("debugGoldMaxButton").objectReferenceValue = btn;
        so.ApplyModifiedProperties();

        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log("[GemManagementUI] Debug Gold Max button created! Adjust position in Inspector to align with other debug buttons.");
    }

    [ContextMenu("Setup Debug Unlimited Gems Button")]
    private void SetupDebugUnlimitedGemsButton()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) { Debug.LogError("[GemManagementUI] Canvas not found!"); return; }

        var oldInCanvas = canvas.transform.Find("DebugUnlimitedGemsButton");
        if (oldInCanvas != null) DestroyImmediate(oldInCanvas.gameObject);

        var btnObj = new GameObject("DebugUnlimitedGemsButton");
        btnObj.transform.SetParent(canvas.transform, false);

        var btnRect = btnObj.AddComponent<RectTransform>();
        btnRect.anchorMin        = new Vector2(0.5f, 0.5f);
        btnRect.anchorMax        = new Vector2(0.5f, 0.5f);
        btnRect.pivot            = new Vector2(0.5f, 0.5f);
        btnRect.anchoredPosition = debugUnlimitedGemsButtonPosition;
        btnRect.sizeDelta        = debugUnlimitedGemsButtonSize;

        var btnImg = btnObj.AddComponent<Image>();
        btnImg.color = new Color(0.5f, 0.1f, 0.7f, 1f); // 紫：無制限ボタンの識別用

        var btn = btnObj.AddComponent<Button>();

        var textObj = new GameObject("Text");
        textObj.transform.SetParent(btnObj.transform, false);
        var textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
        var tmp = textObj.AddComponent<TMPro.TextMeshProUGUI>();
        tmp.text = "無制限:OFF";
        tmp.fontSize = 16f;
        tmp.alignment = TMPro.TextAlignmentOptions.Center;
        tmp.color = Color.white;

        var so = new UnityEditor.SerializedObject(this);
        so.Update();
        so.FindProperty("debugUnlimitedGemsButton").objectReferenceValue = btn;
        so.FindProperty("debugUnlimitedGemsButtonText").objectReferenceValue = tmp;
        so.ApplyModifiedProperties();

        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log("[GemManagementUI] Debug Unlimited Gems button created! Adjust position in Inspector to align with other debug buttons.");
    }

    [ContextMenu("Setup Debug Add Low Uses Gem Button")]
    private void SetupDebugAddLowUsesGemButton()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) { Debug.LogError("[GemManagementUI] Canvas not found!"); return; }

        var oldInCanvas = canvas.transform.Find("DebugAddLowUsesGemButton");
        if (oldInCanvas != null) DestroyImmediate(oldInCanvas.gameObject);

        var btnObj = new GameObject("DebugAddLowUsesGemButton");
        btnObj.transform.SetParent(canvas.transform, false);

        var btnRect = btnObj.AddComponent<RectTransform>();
        btnRect.anchorMin        = new Vector2(0.5f, 0.5f);
        btnRect.anchorMax        = new Vector2(0.5f, 0.5f);
        btnRect.pivot            = new Vector2(0.5f, 0.5f);
        btnRect.anchoredPosition = debugAddLowUsesGemButtonPosition;
        btnRect.sizeDelta        = debugAddLowUsesGemButtonSize;

        var btnImg = btnObj.AddComponent<Image>();
        btnImg.color = new Color(0.7f, 0.5f, 0.1f, 1f); // 橙：残り1回ジェム追加ボタンの識別用

        var btn = btnObj.AddComponent<Button>();

        var textObj = new GameObject("Text");
        textObj.transform.SetParent(btnObj.transform, false);
        var textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
        var tmp = textObj.AddComponent<TMPro.TextMeshProUGUI>();
        tmp.text = "残1回ジェム追加";
        tmp.fontSize = 16f;
        tmp.alignment = TMPro.TextAlignmentOptions.Center;
        tmp.color = Color.white;

        var so = new UnityEditor.SerializedObject(this);
        so.Update();
        so.FindProperty("debugAddLowUsesGemButton").objectReferenceValue = btn;
        so.ApplyModifiedProperties();

        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log("[GemManagementUI] Debug Add Low Uses Gem button created! Adjust position in Inspector to align with other debug buttons.");
    }

    [ContextMenu("Debug: Add Test Gems (Area01-09)")]
    private void AddTestGems()
    {
        if (ProgressManager.Instance == null || GemManager.Instance == null)
        {
            Debug.LogError("[GemManagementUI] ProgressManager or GemManager not found. Run in Play mode.");
            return;
        }

        int added = 0;
        for (int i = 1; i <= 9; i++)
        {
            string areaId = $"Area_{i:D2}";
            if (GemManager.Instance.TryAddGemForArea(areaId, out _))
                added++;
            else
                Debug.LogWarning($"[GemManagementUI] Failed to add gem for {areaId} (inventory full or no definition)");
        }

        ProgressManager.Instance.Save();
        RefreshGemList();
        Debug.Log($"[GemManagementUI] Added {added}/9 test gems.");
    }

    private void CreateDimPanel(Transform parent)
    {
        var dimObj = new GameObject("GemDimPanel");
        dimObj.transform.SetParent(parent, false);
        var rect = dimObj.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.sizeDelta = Vector2.zero;
        var img = dimObj.AddComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0.75f);
        dimPanel = dimObj;
        dimObj.SetActive(false);
    }

    private void CreateGemPanel(Transform parent)
    {
        // メインパネル
        var panelObj = new GameObject("GemManagementPanel");
        panelObj.transform.SetParent(parent, false);
        var panelRect = panelObj.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(680f, 900f);
        panelRect.anchoredPosition = Vector2.zero;
        var panelBg = panelObj.AddComponent<Image>();
        panelBg.color = new Color(0.08f, 0.08f, 0.12f, 0.97f);

        var panelLayout = panelObj.AddComponent<VerticalLayoutGroup>();
        panelLayout.spacing = 10f;
        panelLayout.padding = new RectOffset(20, 20, 16, 16);
        panelLayout.childAlignment = TextAnchor.UpperCenter;
        panelLayout.childControlWidth = true;
        panelLayout.childControlHeight = true;
        panelLayout.childForceExpandWidth = true;
        panelLayout.childForceExpandHeight = false;

        gemPanel = panelObj;

        // スロット表示 + 装備/売却/閉じるボタン行（タイトル行・区切り線は廃止）
        var slotRowObj = new GameObject("SlotActionRow");
        slotRowObj.transform.SetParent(panelObj.transform, false);
        var slotRowLayout = slotRowObj.AddComponent<HorizontalLayoutGroup>();
        slotRowLayout.spacing = 8f;
        slotRowLayout.childAlignment = TextAnchor.MiddleLeft;
        slotRowLayout.childControlWidth = true;
        slotRowLayout.childControlHeight = true;
        slotRowLayout.childForceExpandWidth = false;
        slotRowLayout.childForceExpandHeight = true;
        slotRowObj.AddComponent<LayoutElement>().preferredHeight = 72f;

        var slotObj = new GameObject("SlotLevelText");
        slotObj.transform.SetParent(slotRowObj.transform, false);
        slotLevelText = slotObj.AddComponent<TextMeshProUGUI>();
        slotLevelText.text = "スロット使用: 0 / 1";
        slotLevelText.fontSize = 34f;
        slotLevelText.alignment = TextAlignmentOptions.MidlineLeft;
        slotLevelText.color = new Color(0.7f, 0.9f, 1f, 1f);
        slotObj.AddComponent<LayoutElement>().flexibleWidth = 1f;

        // 装備/解除ボタン
        var equipBtnObj = new GameObject("SharedEquipButton");
        equipBtnObj.transform.SetParent(slotRowObj.transform, false);
        equipBtnObj.AddComponent<Image>().color = new Color(0.2f, 0.3f, 0.5f, 0f);
        sharedEquipButton = equipBtnObj.AddComponent<Button>();
        sharedEquipButton.interactable = false;
        var equipLE = equipBtnObj.AddComponent<LayoutElement>();
        equipLE.preferredWidth = 160f;

        // 売却ボタン
        var sellBtnObj = new GameObject("SharedSellButton");
        sellBtnObj.transform.SetParent(slotRowObj.transform, false);
        sellBtnObj.AddComponent<Image>().color = new Color(0.5f, 0.2f, 0.15f, 1f);
        sharedSellButton = sellBtnObj.AddComponent<Button>();
        sharedSellButton.interactable = false;
        var sellLE = sellBtnObj.AddComponent<LayoutElement>();
        sellLE.preferredWidth = 160f;
        var sellTextObj = new GameObject("Text");
        sellTextObj.transform.SetParent(sellBtnObj.transform, false);
        var sellTextRect = sellTextObj.AddComponent<RectTransform>();
        sellTextRect.anchorMin = Vector2.zero; sellTextRect.anchorMax = Vector2.one; sellTextRect.sizeDelta = Vector2.zero;
        var sellTMP = sellTextObj.AddComponent<TextMeshProUGUI>();
        sellTMP.text = "売却";
        sellTMP.fontSize = 24f;
        sellTMP.alignment = TextAlignmentOptions.Center;
        sellTMP.color = Color.white;

        // 閉じるボタン（ダークグレー、売却と色が被らないよう変更）
        var closeBtnObj = new GameObject("CloseButton");
        closeBtnObj.transform.SetParent(slotRowObj.transform, false);
        closeBtnObj.AddComponent<Image>().color = new Color(0.25f, 0.25f, 0.3f, 1f);
        closeButton = closeBtnObj.AddComponent<Button>();
        closeBtnObj.AddComponent<LayoutElement>().preferredWidth = 160f;
        var closeBtnTextObj = new GameObject("Text");
        closeBtnTextObj.transform.SetParent(closeBtnObj.transform, false);
        var closeBtnTextRect = closeBtnTextObj.AddComponent<RectTransform>();
        closeBtnTextRect.anchorMin = Vector2.zero; closeBtnTextRect.anchorMax = Vector2.one; closeBtnTextRect.sizeDelta = Vector2.zero;
        var closeBtnTMP = closeBtnTextObj.AddComponent<TextMeshProUGUI>();
        closeBtnTMP.text = "閉じる";
        closeBtnTMP.fontSize = 24f;
        closeBtnTMP.alignment = TextAlignmentOptions.Center;
        closeBtnTMP.color = Color.white;

        // 区切り線
        CreateSeparator(panelObj.transform);

        // ScrollView
        CreateScrollView(panelObj.transform);

        panelObj.SetActive(false);
    }

    private void CreateHeaderRow(Transform parent)
    {
        var headerObj = new GameObject("HeaderRow");
        headerObj.transform.SetParent(parent, false);
        // HorizontalLayoutGroupを使わずアンカーベースの絶対配置にする
        var headerLE = headerObj.AddComponent<LayoutElement>();
        headerLE.preferredHeight = 50f;

        // タイトルテキスト（左端〜右端140px手前まで横幅をストレッチ）
        var titleObj = new GameObject("TitleText");
        titleObj.transform.SetParent(headerObj.transform, false);
        var titleRect = titleObj.AddComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 0f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.offsetMin = Vector2.zero;
        titleRect.offsetMax = new Vector2(-140f, 0f);
        titleText = titleObj.AddComponent<TextMeshProUGUI>();
        titleText.text = "GEM MANAGEMENT";
        titleText.fontSize = 30f;
        titleText.fontStyle = FontStyles.Bold;
        titleText.alignment = TextAlignmentOptions.MidlineLeft;
        titleText.color = Color.white;
        titleText.enableWordWrapping = false;

        // 閉じるボタン（右端アンカー・縦央揃え）
        var closeBtnObj = new GameObject("CloseButton");
        closeBtnObj.transform.SetParent(headerObj.transform, false);
        var closeBtnRect = closeBtnObj.AddComponent<RectTransform>();
        closeBtnRect.anchorMin = new Vector2(1f, 0.5f);
        closeBtnRect.anchorMax = new Vector2(1f, 0.5f);
        closeBtnRect.sizeDelta = new Vector2(120f, 46f);
        closeBtnRect.anchoredPosition = new Vector2(-65f, 0f);
        var closeBtnImg = closeBtnObj.AddComponent<Image>();
        closeBtnImg.color = new Color(0.4f, 0.15f, 0.15f, 1f);
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
    }

    private void CreateSeparator(Transform parent)
    {
        var sepObj = new GameObject("Separator");
        sepObj.transform.SetParent(parent, false);
        var sepImg = sepObj.AddComponent<Image>();
        sepImg.color = new Color(0.3f, 0.3f, 0.4f, 1f);
        var sepLE = sepObj.AddComponent<LayoutElement>();
        sepLE.preferredHeight = 2f;
    }

    private void CreateScrollView(Transform parent)
    {
        // ScrollView
        var scrollObj = new GameObject("ScrollView");
        scrollObj.transform.SetParent(parent, false);
        var scrollRect = scrollObj.AddComponent<RectTransform>();
        scrollRect.sizeDelta = new Vector2(640f, 720f);
        var scroll = scrollObj.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        var scrollLE = scrollObj.AddComponent<LayoutElement>();
        scrollLE.preferredHeight = 720f;
        scrollLE.flexibleHeight = 1f;

        // Viewport
        var viewportObj = new GameObject("Viewport");
        viewportObj.transform.SetParent(scrollObj.transform, false);
        var viewportRect = viewportObj.AddComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.sizeDelta = Vector2.zero;
        viewportRect.anchoredPosition = Vector2.zero;
        viewportObj.AddComponent<RectMask2D>();

        // Content
        var contentObj = new GameObject("Content");
        contentObj.transform.SetParent(viewportObj.transform, false);
        var contentRect = contentObj.AddComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = new Vector2(0f, 0f);

        var contentLayout = contentObj.AddComponent<GridLayoutGroup>();
        contentLayout.padding = new RectOffset(4, 4, 4, 4);
        contentLayout.cellSize = new Vector2(300f, 270f);
        contentLayout.spacing = new Vector2(8f, 8f);
        contentLayout.startCorner = GridLayoutGroup.Corner.UpperLeft;
        contentLayout.startAxis = GridLayoutGroup.Axis.Horizontal;
        contentLayout.childAlignment = TextAnchor.UpperLeft;
        contentLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        contentLayout.constraintCount = 4;

        var csf = contentObj.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.content = contentRect;
        scroll.viewport = viewportRect;

        // 空メッセージ（Contentの先頭に配置 → ScrollView内でスクロール範囲に含まれる）
        var emptyObj = new GameObject("EmptyMessage");
        emptyObj.transform.SetParent(contentObj.transform, false);
        emptyMessageText = emptyObj.AddComponent<TextMeshProUGUI>();
        emptyMessageText.text = "所持ジェムがありません\nArea のStage3をクリアしてジェムを入手しましょう";
        emptyMessageText.fontSize = 18f;
        emptyMessageText.alignment = TextAlignmentOptions.Center;
        emptyMessageText.color = new Color(0.6f, 0.6f, 0.6f, 1f);
        var emptyLE = emptyObj.AddComponent<LayoutElement>();
        emptyLE.preferredHeight = 80f;
        emptyObj.SetActive(false);

        gemListContainer = contentObj.transform;
    }
#endif
}
