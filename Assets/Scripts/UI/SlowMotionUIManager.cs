using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// スローモーションUIマネージャー（ボタン+横長バーゲージ）
/// gaugeImage（SlowMotionGauge）を横棒として流用。追加オブジェクト不要。
/// Editor拡張でPlay前にHierarchy生成、Inspector調整可能
/// </summary>
public class SlowMotionUIManager : MonoBehaviour
{
    [Header("References")]
    [Tooltip("SlowMotionManagerへの参照")]
    [SerializeField] private SlowMotionManager slowMotionManager;

    [Header("UI Components")]
    [Tooltip("スローモーションボタン")]
    [SerializeField] private Button slowMotionButton;

    [Tooltip("横棒バーのフィル部分（Image Type: Filled, Horizontal）")]
    [SerializeField] private Image gaugeImage;

    [Tooltip("横棒バーの背景")]
    [SerializeField] private Image gaugeBackground;

    [Tooltip("ゲージ内装飾（横棒モードでは通常不要）")]
    [SerializeField] private Image gaugeInner;

    [Tooltip("ペナルティ表示テキスト（オプション）")]
    [SerializeField] private TextMeshProUGUI penaltyText;

    [Header("Bar Size Settings")]
    [Tooltip("バーの幅（px）")]
    [SerializeField] private float barWidth = 160f;

    [Tooltip("バーの高さ（px）")]
    [SerializeField] private float barHeight = 14f;

    [Header("Gauge Color Settings")]
    [Tooltip("ゲージ満タン時の色（HDR対応）")]
    [SerializeField] private Color gaugeFullColor = new Color(1.0f, 2.5f, 5.0f, 1f);

    [Tooltip("ゲージ空時の色")]
    [SerializeField] private Color gaugeEmptyColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);

    [Tooltip("ゲージ背景色")]
    [SerializeField] private Color gaugeBackgroundColor = new Color(0.2f, 0.2f, 0.2f, 0.4f);

    [Tooltip("ペナルティ中のゲージ色（赤系）")]
    [SerializeField] private Color gaugePenaltyColor = new Color(3.0f, 0.5f, 0.5f, 1f);

    [Tooltip("ペナルティ点滅速度（Hz）")]
    [SerializeField] private float penaltyBlinkFrequency = 3f;

    [Header("Input Mode")]
    [Tooltip("ONにするとホールドモード。OFFはトグルモード（PC向け）。")]
    [SerializeField] private bool useHoldMode = false;

    [Header("Button Settings")]
    [SerializeField] private Sprite buttonIconNormal;
    [SerializeField] private Sprite buttonIconActive;
    [SerializeField] private Color buttonColorNormal = new Color(0.8f, 0.8f, 0.8f, 1f);
    [SerializeField] private Color buttonColorActive = new Color(0.3f, 0.7f, 1.5f, 1f);

    // SetupSlowMotionUI用（HideInInspector：通常は非表示）
    [HideInInspector] [SerializeField] private Vector2 gaugePosition = new Vector2(100f, -400f);
    [HideInInspector] [SerializeField] private float gaugeOuterSize = 100f;
    [HideInInspector] [SerializeField] private float gaugeInnerSize = 70f;
    [HideInInspector] [SerializeField] private float buttonSize = 60f;

    private Image buttonImage;
    private const string PlayerPrefsKey = "SlowMotionHoldMode";

    public bool UseHoldMode => useHoldMode;

    private void Start()
    {
        if (slowMotionManager == null)
            slowMotionManager = SlowMotionManager.Instance;

        useHoldMode = PlayerPrefs.GetInt(PlayerPrefsKey, 0) == 1;

        // バーをアクティブ化（Play前に非アクティブだった場合に対応）
        if (gaugeImage != null)
        {
            gaugeImage.gameObject.SetActive(true);
            // sprite=nullだとFilled typeのfillAmountが機能しないため白矩形スプライトを保証
            if (gaugeImage.sprite == null)
                gaugeImage.sprite = CreateWhiteRectSprite();
        }
        if (gaugeBackground != null) gaugeBackground.gameObject.SetActive(true);

        // バーサイズをRectTransformに適用
        ApplyBarSize();

        if (slowMotionButton != null)
        {
            buttonImage = slowMotionButton.GetComponent<Image>();
            RegisterButtonEvents();
        }

        UpdateUI();
    }

    private void ApplyBarSize()
    {
        if (gaugeImage != null)
            gaugeImage.rectTransform.sizeDelta = new Vector2(barWidth, barHeight);
        if (gaugeBackground != null)
            gaugeBackground.rectTransform.sizeDelta = new Vector2(barWidth, barHeight);
    }

    private void Update()
    {
        UpdateUI();
    }

    private void UpdateUI()
    {
        if (slowMotionManager == null) return;

        float normalized = slowMotionManager.NormalizedDuration;
        bool isInPenaltyDelay = slowMotionManager.IsInPenaltyDelay;

        // フィルバー色（fillAmount=0になるペナルティ中はfillは見えないのでemptyColor固定）
        Color fillColor = Color.Lerp(gaugeEmptyColor, gaugeFullColor, normalized);

        // 背景色（ペナルティ遅延中のみ赤点滅、スキル選択画面中は点滅停止）
        Color bgColor;
        bool isUISuspended = Game.UI.SkillSelectionUI.IsShowing ||
                             (PauseManager.Instance != null && PauseManager.Instance.IsPaused);
        if (isInPenaltyDelay && !isUISuspended)
        {
            float blink = Mathf.Abs(Mathf.Sin(Time.unscaledTime * penaltyBlinkFrequency * Mathf.PI));
            bgColor = Color.Lerp(gaugeBackgroundColor, gaugePenaltyColor, blink);
        }
        else if (isInPenaltyDelay)
        {
            bgColor = gaugePenaltyColor; // 点滅停止中は赤色固定
        }
        else
        {
            bgColor = gaugeBackgroundColor;
        }

        // バーゲージ更新
        if (gaugeImage != null)
        {
            gaugeImage.fillAmount = normalized;
            gaugeImage.color = fillColor;
        }

        // 背景色更新
        if (gaugeBackground != null)
        {
            gaugeBackground.color = bgColor;
        }

        // ボタン更新
        if (slowMotionButton != null)
        {
            if (buttonImage != null)
            {
                buttonImage.color = slowMotionManager.IsSlowMotionActive ? buttonColorActive : buttonColorNormal;
                if (buttonIconNormal != null && buttonIconActive != null)
                    buttonImage.sprite = slowMotionManager.IsSlowMotionActive ? buttonIconActive : buttonIconNormal;
            }
            slowMotionButton.interactable = slowMotionManager.CurrentDuration > 0f || slowMotionManager.IsSlowMotionActive;
        }

        // ペナルティテキスト更新（オプション）
        if (penaltyText != null)
        {
            if (slowMotionManager.CurrentDuration <= 0f && !slowMotionManager.IsSlowMotionActive)
            {
                penaltyText.text = "Recharging...";
                penaltyText.enabled = true;
            }
            else
            {
                penaltyText.enabled = false;
            }
        }
    }

    private void RegisterButtonEvents()
    {
        if (slowMotionButton == null) return;

        slowMotionButton.onClick.RemoveAllListeners();
        EventTrigger trigger = slowMotionButton.gameObject.GetComponent<EventTrigger>();
        if (trigger != null) trigger.triggers.Clear();

        if (useHoldMode)
        {
            if (trigger == null)
                trigger = slowMotionButton.gameObject.AddComponent<EventTrigger>();

            var pointerDown = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
            pointerDown.callback.AddListener(_ => OnSlowMotionButtonDown());
            trigger.triggers.Add(pointerDown);

            var pointerUp = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
            pointerUp.callback.AddListener(_ => OnSlowMotionButtonUp());
            trigger.triggers.Add(pointerUp);
        }
        else
        {
            slowMotionButton.onClick.AddListener(OnSlowMotionButtonClicked);
        }
    }

    public void SetHoldMode(bool holdMode)
    {
        useHoldMode = holdMode;
        PlayerPrefs.SetInt(PlayerPrefsKey, holdMode ? 1 : 0);
        if (slowMotionManager != null && slowMotionManager.IsSlowMotionActive)
            slowMotionManager.StopSlowMotion();
        RegisterButtonEvents();
    }

    private void OnSlowMotionButtonClicked()
    {
        if (Game.UI.SkillSelectionUI.IsShowing) return;
        if (PauseManager.Instance != null && PauseManager.Instance.IsPaused) return;
        if (slowMotionManager != null)
            slowMotionManager.ToggleSlowMotion();
    }

    private void OnSlowMotionButtonDown()
    {
        if (Game.UI.SkillSelectionUI.IsShowing) return;
        if (PauseManager.Instance != null && PauseManager.Instance.IsPaused) return;
        if (slowMotionManager != null)
            slowMotionManager.StartSlowMotion();
    }

    private void OnSlowMotionButtonUp()
    {
        if (slowMotionManager != null && slowMotionManager.IsSlowMotionActive)
            slowMotionManager.StopSlowMotion();
    }

    /// <summary>
    /// Filled typeが機能するためのシンプルな白矩形スプライトを生成
    /// sprite=nullだとfillAmountが視覚的に機能しないため必須
    /// </summary>
    private static Sprite CreateWhiteRectSprite()
    {
        Texture2D tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        Color[] colors = new Color[16];
        for (int i = 0; i < 16; i++) colors[i] = Color.white;
        tex.SetPixels(colors);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        ApplyBarSize();
        if (gaugeImage != null) UnityEditor.EditorUtility.SetDirty(gaugeImage.gameObject);
        if (gaugeBackground != null) UnityEditor.EditorUtility.SetDirty(gaugeBackground.gameObject);
    }

    /// <summary>
    /// SlowMotionGauge（元の円形ゲージ）を横長バーとして設定する
    /// ContextMenu → "Convert Gauge to Horizontal Bar" を実行
    /// </summary>
    [ContextMenu("Convert Gauge to Horizontal Bar")]
    private void ConvertToHorizontalBar()
    {
        if (gaugeImage == null)
        {
            Debug.LogError("[SlowMotionUIManager] gaugeImage が未設定です。");
            return;
        }

        // アクティブ化（Play前に非アクティブだった場合に対応）
        gaugeImage.gameObject.SetActive(true);
        if (gaugeBackground != null) gaugeBackground.gameObject.SetActive(true);
        UnityEditor.EditorUtility.SetDirty(gaugeImage.gameObject);
        if (gaugeBackground != null) UnityEditor.EditorUtility.SetDirty(gaugeBackground.gameObject);

        // フィルバー設定
        // sprite=nullだとImage.Type.FilledのfillAmountが機能しないため、白矩形スプライトを設定
        gaugeImage.sprite = CreateWhiteRectSprite();
        gaugeImage.type = Image.Type.Filled;
        gaugeImage.fillMethod = Image.FillMethod.Horizontal;
        gaugeImage.fillOrigin = (int)Image.OriginHorizontal.Left;
        gaugeImage.fillAmount = 1f;
        gaugeImage.color = gaugeFullColor;
        gaugeImage.rectTransform.sizeDelta = new Vector2(barWidth, barHeight);
        UnityEditor.EditorUtility.SetDirty(gaugeImage.gameObject);

        // 背景バー設定
        if (gaugeBackground != null)
        {
            gaugeBackground.sprite = CreateWhiteRectSprite();
            gaugeBackground.type = Image.Type.Simple;
            gaugeBackground.color = gaugeBackgroundColor;
            gaugeBackground.rectTransform.sizeDelta = new Vector2(barWidth, barHeight);
            gaugeBackground.rectTransform.anchoredPosition = gaugeImage.rectTransform.anchoredPosition;

            // 描画順確認: BG（低インデックス）→ Fill（高インデックス）が正しい順序
            int bgIdx = gaugeBackground.transform.GetSiblingIndex();
            int fillIdx = gaugeImage.transform.GetSiblingIndex();
            if (bgIdx > fillIdx)
            {
                // BGが前面にある → BGをfillの下に移動
                gaugeBackground.transform.SetSiblingIndex(fillIdx);
            }
            UnityEditor.EditorUtility.SetDirty(gaugeBackground.gameObject);
        }

        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log($"[SlowMotionUIManager] 横棒バーに変換完了。{barWidth}x{barHeight}px  " +
                  $"gaugeImage sibling={gaugeImage.transform.GetSiblingIndex()}, " +
                  $"gaugeBackground sibling={gaugeBackground?.transform.GetSiblingIndex()}");

        if (gaugeInner != null && gaugeInner.gameObject.activeSelf)
            Debug.Log("[SlowMotionUIManager] gaugeInner がアクティブです。横棒モードでは不要なため Hierarchy から非アクティブ化を検討してください。");
    }

    /// <summary>
    /// Inspectorから実行：ボタンとゲージを自動生成
    /// </summary>
    [ContextMenu("Setup Slow Motion UI")]
    private void SetupSlowMotionUI()
    {
        Debug.Log("[SlowMotionUIManager] Setting up Slow Motion UI...");

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("[SlowMotionUIManager] Canvas not found!");
            return;
        }

        Transform skillHUD = canvas.transform.Find("SkillHUD");
        if (skillHUD == null)
        {
            Debug.LogError("[SlowMotionUIManager] SkillHUD not found! Make sure SkillHUDManager is set up first.");
            return;
        }

        Transform categoryAGrid = skillHUD.Find("CategoryA_Grid");
        if (categoryAGrid == null)
        {
            Debug.LogError("[SlowMotionUIManager] CategoryA_Grid not found!");
            return;
        }

        Vector2 finalPosition = gaugePosition;
        Sprite circleSprite = CreateCircleSprite();

        // === 背景円 ===
        GameObject bgObject = new GameObject("SlowMotionGaugeBackground");
        bgObject.transform.SetParent(canvas.transform, false);
        RectTransform bgRect = bgObject.AddComponent<RectTransform>();
        bgRect.anchoredPosition = finalPosition;
        bgRect.sizeDelta = new Vector2(gaugeOuterSize, gaugeOuterSize);
        bgRect.anchorMin = new Vector2(0f, 1f);
        bgRect.anchorMax = new Vector2(0f, 1f);
        bgRect.pivot = new Vector2(0.5f, 0.5f);
        Image bgImage = bgObject.AddComponent<Image>();
        bgImage.sprite = circleSprite;
        bgImage.color = gaugeBackgroundColor;
        bgImage.type = Image.Type.Filled;
        bgImage.fillMethod = Image.FillMethod.Radial360;
        bgImage.fillOrigin = (int)Image.Origin360.Top;
        bgImage.fillClockwise = true;
        bgImage.fillAmount = 1f;
        gaugeBackground = bgImage;

        // === メインゲージ ===
        GameObject gaugeObject = new GameObject("SlowMotionGauge");
        gaugeObject.transform.SetParent(canvas.transform, false);
        RectTransform gaugeRect = gaugeObject.AddComponent<RectTransform>();
        gaugeRect.anchoredPosition = finalPosition;
        gaugeRect.sizeDelta = new Vector2(gaugeOuterSize, gaugeOuterSize);
        gaugeRect.anchorMin = new Vector2(0f, 1f);
        gaugeRect.anchorMax = new Vector2(0f, 1f);
        gaugeRect.pivot = new Vector2(0.5f, 0.5f);
        Image gImage = gaugeObject.AddComponent<Image>();
        gImage.sprite = circleSprite;
        gImage.color = gaugeFullColor;
        gImage.type = Image.Type.Filled;
        gImage.fillMethod = Image.FillMethod.Radial360;
        gImage.fillOrigin = (int)Image.Origin360.Top;
        gImage.fillClockwise = false;
        gImage.fillAmount = 1f;
        gaugeImage = gImage;

        // === 内側円 ===
        GameObject innerObject = new GameObject("SlowMotionGaugeInner");
        innerObject.transform.SetParent(canvas.transform, false);
        RectTransform innerRect = innerObject.AddComponent<RectTransform>();
        innerRect.anchoredPosition = finalPosition;
        innerRect.sizeDelta = new Vector2(gaugeInnerSize, gaugeInnerSize);
        innerRect.anchorMin = new Vector2(0f, 1f);
        innerRect.anchorMax = new Vector2(0f, 1f);
        innerRect.pivot = new Vector2(0.5f, 0.5f);
        Image innerImage = innerObject.AddComponent<Image>();
        Sprite gradientSprite = CreateRadialGradientSprite(
            new Color(0.2f, 0.6f, 0.6f, 1f),
            new Color(0.05f, 0.05f, 0.05f, 1f)
        );
        innerImage.sprite = gradientSprite;
        innerImage.color = Color.white;
        gaugeInner = innerImage;

        // === ボタン ===
        GameObject buttonObject = new GameObject("SlowMotionButton");
        buttonObject.transform.SetParent(canvas.transform, false);
        RectTransform buttonRect = buttonObject.AddComponent<RectTransform>();
        buttonRect.anchoredPosition = finalPosition;
        buttonRect.sizeDelta = new Vector2(buttonSize, buttonSize);
        buttonRect.anchorMin = new Vector2(0f, 1f);
        buttonRect.anchorMax = new Vector2(0f, 1f);
        buttonRect.pivot = new Vector2(0.5f, 0.5f);
        Image btnImage = buttonObject.AddComponent<Image>();
        btnImage.sprite = circleSprite;
        btnImage.color = buttonColorNormal;
        Button btn = buttonObject.AddComponent<Button>();
        slowMotionButton = btn;
        buttonImage = btnImage;

        // 描画順: BG < Gauge < Inner < Button
        bgObject.transform.SetAsLastSibling();
        gaugeObject.transform.SetAsLastSibling();
        innerObject.transform.SetAsLastSibling();
        buttonObject.transform.SetAsLastSibling();

        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log("[SlowMotionUIManager] Setup complete! 次に ContextMenu → 'Convert Gauge to Horizontal Bar' を実行してください。");
    }

    private Sprite CreateCircleSprite()
    {
        int size = 128;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Vector2 center = new Vector2(size / 2f, size / 2f);
        float radius = size / 2f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                texture.SetPixel(x, y, Vector2.Distance(new Vector2(x, y), center) <= radius ? Color.white : Color.clear);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    private Sprite CreateRadialGradientSprite(Color centerColor, Color edgeColor)
    {
        int size = 128;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Vector2 center = new Vector2(size / 2f, size / 2f);
        float radius = size / 2f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                texture.SetPixel(x, y, distance <= radius
                    ? Color.Lerp(centerColor, edgeColor, distance / radius)
                    : Color.clear);
            }
        }
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }
#endif
}
