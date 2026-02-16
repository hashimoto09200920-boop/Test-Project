using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// スローモーションUIマネージャー（ボタン+円形ゲージ）
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

    [Tooltip("円形ゲージImage（Image Type: Filled, Radial 360）")]
    [SerializeField] private Image gaugeImage;

    [Tooltip("ゲージ背景Image")]
    [SerializeField] private Image gaugeBackground;

    [Tooltip("ゲージ内側円（ドーナツ型の穴）")]
    [SerializeField] private Image gaugeInner;

    [Tooltip("ペナルティ表示テキスト（オプション）")]
    [SerializeField] private TextMeshProUGUI penaltyText;

    [Header("Gauge Position & Size Settings")]
    [Tooltip("ゲージとボタンの位置（Anchored Position）- CategoryA_Gridの上に配置")]
    [SerializeField] private Vector2 gaugePosition = new Vector2(100f, -400f);

    [Tooltip("ゲージの外径（Width & Height）")]
    [SerializeField] private float gaugeOuterSize = 100f;

    [Tooltip("ゲージの内径（ドーナツの穴のサイズ）")]
    [SerializeField] private float gaugeInnerSize = 70f;

    [Tooltip("ボタンのサイズ（ゲージの内側に配置）")]
    [SerializeField] private float buttonSize = 60f;

    [Header("Gauge Color Settings")]
    [Tooltip("ゲージ満タン時の色（時間をイメージ：ブルー系）")]
    [SerializeField] private Color gaugeFullColor = new Color(1.0f, 2.5f, 5.0f, 1f); // HDR対応・強い発光

    [Tooltip("ゲージ空時の色")]
    [SerializeField] private Color gaugeEmptyColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);

    [Tooltip("ゲージ背景色（グレーアウト部分）")]
    [SerializeField] private Color gaugeBackgroundColor = new Color(0.2f, 0.2f, 0.2f, 0.4f);

    [Tooltip("ペナルティ中のゲージ色（赤系）")]
    [SerializeField] private Color gaugePenaltyColor = new Color(3.0f, 0.5f, 0.5f, 1f); // HDR対応・強い発光

    [Header("Button Settings")]
    [Tooltip("ボタンアイコン（スローモーション中は別のアイコンに変更予定）")]
    [SerializeField] private Sprite buttonIconNormal;

    [Tooltip("スローモーション中のボタンアイコン")]
    [SerializeField] private Sprite buttonIconActive;

    [Tooltip("ボタン色（通常時）")]
    [SerializeField] private Color buttonColorNormal = new Color(0.8f, 0.8f, 0.8f, 1f);

    [Tooltip("ボタン色（スローモーション中）")]
    [SerializeField] private Color buttonColorActive = new Color(0.3f, 0.7f, 1.5f, 1f);

    private Image buttonImage;

    private void Start()
    {
        // SlowMotionManager参照を取得
        if (slowMotionManager == null)
        {
            slowMotionManager = SlowMotionManager.Instance;
        }

        // ボタンクリックイベントを登録
        if (slowMotionButton != null)
        {
            slowMotionButton.onClick.AddListener(OnSlowMotionButtonClicked);
            buttonImage = slowMotionButton.GetComponent<Image>();
        }

        // 初期状態を更新
        UpdateUI();
    }

    private void Update()
    {
        UpdateUI();
    }

    /// <summary>
    /// UIを更新
    /// </summary>
    private void UpdateUI()
    {
        if (slowMotionManager == null) return;

        // ゲージ更新
        if (gaugeImage != null)
        {
            float normalized = slowMotionManager.NormalizedDuration;
            gaugeImage.fillAmount = normalized;

            // ゲージ色の更新（満タン→空、ペナルティ中は赤）
            bool isPenalty = slowMotionManager.CurrentDuration <= 0f && !slowMotionManager.IsSlowMotionActive;
            Color targetColor = isPenalty ? gaugePenaltyColor : Color.Lerp(gaugeEmptyColor, gaugeFullColor, normalized);
            gaugeImage.color = targetColor;
        }

        // ボタン更新
        if (slowMotionButton != null)
        {
            // スローモーション中はボタンの色とアイコンを変更
            if (buttonImage != null)
            {
                buttonImage.color = slowMotionManager.IsSlowMotionActive ? buttonColorActive : buttonColorNormal;

                if (buttonIconNormal != null && buttonIconActive != null)
                {
                    buttonImage.sprite = slowMotionManager.IsSlowMotionActive ? buttonIconActive : buttonIconNormal;
                }
            }

            // ゲージが空の場合はボタンを無効化
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

    /// <summary>
    /// ボタンクリック時の処理
    /// </summary>
    private void OnSlowMotionButtonClicked()
    {
        // スキル選択画面表示中は無効化
        if (Game.UI.SkillSelectionUI.IsShowing) return;

        // ポーズ中は無効化
        if (PauseManager.Instance != null && PauseManager.Instance.IsPaused) return;

        if (slowMotionManager != null)
        {
            slowMotionManager.ToggleSlowMotion();
        }
    }

#if UNITY_EDITOR
    /// <summary>
    /// Inspectorから実行：ボタンとゲージを自動生成
    /// </summary>
    [ContextMenu("Setup Slow Motion UI")]
    private void SetupSlowMotionUI()
    {
        Debug.Log("[SlowMotionUIManager] Setting up Slow Motion UI...");

        // Canvasを探す
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("[SlowMotionUIManager] Canvas not found!");
            return;
        }

        // SkillHUD/CategoryA_Gridを探す
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

        // CategoryA_GridのRectTransformを取得して、その上に配置
        RectTransform categoryARect = categoryAGrid.GetComponent<RectTransform>();
        if (categoryARect == null)
        {
            Debug.LogError("[SlowMotionUIManager] CategoryA_Grid does not have RectTransform!");
            return;
        }

        // CategoryA_Gridの位置を基準に、ゲージ位置を計算
        // CategoryA_Gridの上（中央）に配置
        Vector2 calculatedPosition = new Vector2(
            categoryARect.anchoredPosition.x + categoryARect.sizeDelta.x / 2f, // CategoryA_Gridの中央X
            categoryARect.anchoredPosition.y - 100f // CategoryA_Gridの上100px
        );

        // Inspector設定値を使用（自動計算は無効化）
        Vector2 finalPosition = gaugePosition;
        Debug.Log($"[SlowMotionUIManager] Using Inspector position: {gaugePosition}");

        // 円形スプライト生成
        Sprite circleSprite = CreateCircleSprite();

        // === 背景円の作成 ===
        GameObject bgObject = new GameObject("SlowMotionGaugeBackground");
        bgObject.transform.SetParent(canvas.transform, false);

        RectTransform bgRect = bgObject.AddComponent<RectTransform>();
        bgRect.anchoredPosition = finalPosition;
        bgRect.sizeDelta = new Vector2(gaugeOuterSize, gaugeOuterSize);
        bgRect.anchorMin = new Vector2(0f, 1f); // 左上にアンカー
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
        Debug.Log($"[SlowMotionUIManager] Background created at {bgRect.anchoredPosition}");

        // === メインゲージの作成 ===
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
        gImage.fillClockwise = false; // 時計回りに減る
        gImage.fillAmount = 1f;

        gaugeImage = gImage;
        Debug.Log($"[SlowMotionUIManager] Main gauge created at {gaugeRect.anchoredPosition}");

        // === 内側円の作成 ===
        GameObject innerObject = new GameObject("SlowMotionGaugeInner");
        innerObject.transform.SetParent(canvas.transform, false);

        RectTransform innerRect = innerObject.AddComponent<RectTransform>();
        innerRect.anchoredPosition = finalPosition;
        innerRect.sizeDelta = new Vector2(gaugeInnerSize, gaugeInnerSize);
        innerRect.anchorMin = new Vector2(0f, 1f);
        innerRect.anchorMax = new Vector2(0f, 1f);
        innerRect.pivot = new Vector2(0.5f, 0.5f);

        Image innerImage = innerObject.AddComponent<Image>();
        // シアングロー：中心が淡いシアン、外側が黒
        Sprite gradientSprite = CreateRadialGradientSprite(
            new Color(0.2f, 0.6f, 0.6f, 1f), // 中心：淡いシアン
            new Color(0.05f, 0.05f, 0.05f, 1f) // 外側：黒
        );
        innerImage.sprite = gradientSprite;
        innerImage.color = Color.white; // グラデーションをそのまま表示
        gaugeInner = innerImage;
        Debug.Log($"[SlowMotionUIManager] Inner circle created at {innerRect.anchoredPosition}");

        // === ボタンの作成 ===
        GameObject buttonObject = new GameObject("SlowMotionButton");
        buttonObject.transform.SetParent(canvas.transform, false);

        RectTransform buttonRect = buttonObject.AddComponent<RectTransform>();
        buttonRect.anchoredPosition = finalPosition;
        buttonRect.sizeDelta = new Vector2(buttonSize, buttonSize);
        buttonRect.anchorMin = new Vector2(0f, 1f);
        buttonRect.anchorMax = new Vector2(0f, 1f);
        buttonRect.pivot = new Vector2(0.5f, 0.5f);

        Image btnImage = buttonObject.AddComponent<Image>();
        btnImage.sprite = circleSprite; // 円形スプライトを設定
        btnImage.color = buttonColorNormal;

        Button btn = buttonObject.AddComponent<Button>();
        slowMotionButton = btn;
        buttonImage = btnImage;

        Debug.Log($"[SlowMotionUIManager] Button created at {buttonRect.anchoredPosition}");

        // === Hierarchy順序調整（背景→ゲージ→内側円→ボタンの順）===
        bgObject.transform.SetAsLastSibling();
        gaugeObject.transform.SetAsLastSibling();
        innerObject.transform.SetAsLastSibling();
        buttonObject.transform.SetAsLastSibling(); // ボタンが一番前（見える）

        Debug.Log("[SlowMotionUIManager] Slow Motion UI setup complete!");

        // Inspectorを更新
        UnityEditor.EditorUtility.SetDirty(this);
    }

    /// <summary>
    /// 円形スプライトを生成
    /// </summary>
    private Sprite CreateCircleSprite()
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
                Color color = distance <= radius ? Color.white : Color.clear;
                texture.SetPixel(x, y, color);
            }
        }

        texture.Apply();

        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0, 0, size, size),
            new Vector2(0.5f, 0.5f),
            100f
        );

        return sprite;
    }

    /// <summary>
    /// 放射状グラデーションスプライトを生成
    /// </summary>
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

                if (distance <= radius)
                {
                    // 中心からの距離に応じて色を補間
                    float t = distance / radius; // 0 (中心) to 1 (外側)
                    Color color = Color.Lerp(centerColor, edgeColor, t);
                    texture.SetPixel(x, y, color);
                }
                else
                {
                    texture.SetPixel(x, y, Color.clear);
                }
            }
        }

        texture.Apply();

        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0, 0, size, size),
            new Vector2(0.5f, 0.5f),
            100f
        );

        return sprite;
    }
#endif
}
