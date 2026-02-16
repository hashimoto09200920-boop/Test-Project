using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// ウェーブシステムのタイマーとステージ情報を表示するUI
/// </summary>
public class WaveTimerUI : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("タイマー表示用のTextMeshPro")]
    [SerializeField] private TextMeshProUGUI timerText;

    [Tooltip("ウェーブ段階表示用のTextMeshPro（Wave 1/3など）")]
    [SerializeField] private TextMeshProUGUI stageText;

    [Tooltip("配置パターン名表示用のTextMeshPro（デバッグ用）")]
    [SerializeField] private TextMeshProUGUI formationText;

    [Header("Circular Timer Gauge (New)")]
    [Tooltip("円形タイマーゲージ用のImage（Image Type: Filled, Radial 360）")]
    [SerializeField] private Image timerGaugeImage;

    [Tooltip("円形タイマーゲージの背景Image（オプション）")]
    [SerializeField] private Image timerGaugeBackground;

    [Tooltip("ドーナツ型の内側円（中心の黒い部分）")]
    private Image timerGaugeInner;

    [Tooltip("メインゲージのRectTransform（パルスアニメーション用）")]
    private RectTransform timerGaugeRect;

    [Header("Pause Button")]
    [Tooltip("中断ボタン（ゲージ中央に配置）")]
    [SerializeField] private Button pauseButton;

    [Tooltip("ポーズボタンのサイズ")]
    [SerializeField] private float pauseButtonSize = 64f;

    [Tooltip("ポーズボタンのアイコン（||マーク）")]
    [SerializeField] private Sprite pauseButtonIcon;

    [Header("EnemySpawner Reference")]
    [Tooltip("情報を取得するEnemySpawner")]
    [SerializeField] private EnemySpawner enemySpawner;

    [Header("Display Settings")]
    [Tooltip("配置パターン名を表示するか（デバッグ用）")]
    [SerializeField] private bool showFormationName = true;

    [Tooltip("数字のタイマーを表示するか（falseで円ゲージのみ）")]
    [SerializeField] private bool showTimerText = true;

    [Tooltip("Wave表記（Wave 1/3など）を表示するか")]
    [SerializeField] private bool showStageText = true;

    [Header("Text Position Settings (Play前の画面で調整可能)")]
    [Tooltip("タイマーテキストの位置（Anchored Position）")]
    [SerializeField] private Vector2 timerTextPosition = new Vector2(-120f, -120f);

    [Tooltip("Wave表記テキストの位置（Anchored Position）")]
    [SerializeField] private Vector2 stageTextPosition = new Vector2(0f, -50f);

    [Tooltip("Formation表記テキストの位置（Anchored Position）")]
    [SerializeField] private Vector2 formationTextPosition = new Vector2(0f, -80f);

    [Header("Gauge Position & Size Settings")]
    [Tooltip("ゲージの位置（Anchored Position）")]
    [SerializeField] private Vector2 gaugePosition = new Vector2(-120f, -120f);

    [Tooltip("ゲージの外径（Width & Height）")]
    [SerializeField] private float gaugeOuterSize = 120f;

    [Tooltip("ゲージの内径（ドーナツの穴のサイズ）")]
    [SerializeField] private float gaugeInnerSize = 80f;

    [Header("Gauge Color Settings")]
    [Tooltip("ネオンスペクトラムカラーの遷移時間（秒）")]
    [SerializeField] private float colorTransitionDuration = 30f;

    [Header("Pulse Animation Settings")]
    [Tooltip("パルスアニメーションを有効にするか")]
    [SerializeField] private bool enablePulseAnimation = true;

    [Tooltip("パルスの振幅（1.0 = 100%サイズ、1.1 = 110%サイズ）")]
    [SerializeField] private float pulseAmplitude = 0.08f;

    [Tooltip("パルスの速度（大きいほど速い）")]
    [SerializeField] private float pulseSpeed = 2.5f;

    [Tooltip("ネオンスペクトラムカラーパターン（7色・HDR対応）")]
    [SerializeField] private Color[] neonColors = new Color[]
    {
        new Color(0.5f, 2.5f, 4.5f, 1f),    // ネオンブルー（強い発光）
        new Color(0.5f, 4.0f, 3.5f, 1f),    // ネオンシアン（強い発光）
        new Color(0.5f, 4.0f, 2.0f, 1f),    // エメラルドグリーン（強い発光）
        new Color(1.0f, 4.0f, 0.5f, 1f),    // ネオングリーン（強い発光）
        new Color(3.5f, 4.0f, 0.5f, 1f),    // ネオンイエロー（強い発光）
        new Color(4.5f, 0.5f, 2.5f, 1f),    // ネオンマゼンタ（強い発光）
        new Color(4.5f, 0.5f, 1.0f, 1f)     // ネオンレッド（強い発光）
    };

    private void Start()
    {
        Debug.Log($"[WaveTimerUI] Start() called. timerText={timerText}, timerGaugeImage={timerGaugeImage}");

        // EnemySpawner の設定を検証（警告のみ）
        if (enemySpawner != null)
        {
            int totalStages = enemySpawner.GetTotalStageCount();

            if (totalStages == 0)
            {
                Debug.LogWarning("[WaveTimerUI] Total Stages is 0. Please check if 'Use Wave System' is enabled and 'Wave Stages' are configured in EnemySpawner.");
            }
        }
        else
        {
            Debug.LogWarning("[WaveTimerUI] EnemySpawner reference is missing!");
        }

        // 円形ゲージが未設定の場合は自動生成
        if (timerGaugeImage == null && timerText != null)
        {
            Debug.Log("[WaveTimerUI] Creating circular gauge...");
            CreateCircularGauge();
        }
        else
        {
            Debug.LogWarning($"[WaveTimerUI] Gauge creation skipped. timerGaugeImage={timerGaugeImage}, timerText={timerText}");
        }

        // Inspector設定値でテキスト位置を適用（Play前の画面で調整可能）
        ApplyTextPositions();

        // ポーズボタンのイベント登録
        if (pauseButton != null)
        {
            pauseButton.onClick.AddListener(OnPauseButtonClicked);
        }
    }

    /// <summary>
    /// Inspector設定値でテキスト位置を適用
    /// </summary>
    private void ApplyTextPositions()
    {
        if (timerText != null)
        {
            RectTransform timerRect = timerText.GetComponent<RectTransform>();
            if (timerRect != null)
            {
                timerRect.anchoredPosition = timerTextPosition;
            }
        }

        if (stageText != null)
        {
            RectTransform stageRect = stageText.GetComponent<RectTransform>();
            if (stageRect != null)
            {
                stageRect.anchoredPosition = stageTextPosition;
            }
        }

        if (formationText != null)
        {
            RectTransform formationRect = formationText.GetComponent<RectTransform>();
            if (formationRect != null)
            {
                formationRect.anchoredPosition = formationTextPosition;
            }
        }

        Debug.Log($"[WaveTimerUI] Text positions applied - Timer:{timerTextPosition}, Stage:{stageTextPosition}, Formation:{formationTextPosition}");
    }

    /// <summary>
    /// 円形タイマーゲージを動的に生成（ドーナツ型）
    /// </summary>
    private void CreateCircularGauge()
    {
        // TimerTextの親を取得
        Transform parentTransform = timerText.transform.parent;
        if (parentTransform == null)
        {
            Debug.LogWarning("[WaveTimerUI] Cannot create gauge: TimerText has no parent!");
            return;
        }

        Debug.Log($"[WaveTimerUI] Parent found: {parentTransform.name}");

        // Inspector設定値を使用
        Debug.Log($"[WaveTimerUI] Using Inspector settings - Position: {gaugePosition}, Outer: {gaugeOuterSize}, Inner: {gaugeInnerSize}");

        // 円形のスプライトを生成
        Sprite circleSprite = CreateCircleSprite();
        Debug.Log($"[WaveTimerUI] Circle sprite created: {circleSprite}");

        // === 背景円の作成 ===
        GameObject bgObject = new GameObject("TimerGaugeBackground");
        bgObject.transform.SetParent(parentTransform, false);

        RectTransform bgRect = bgObject.AddComponent<RectTransform>();
        bgRect.anchoredPosition = gaugePosition; // Inspector設定値
        bgRect.sizeDelta = new Vector2(gaugeOuterSize, gaugeOuterSize); // Inspector設定値
        bgRect.anchorMin = new Vector2(1f, 1f); // 右上にアンカー
        bgRect.anchorMax = new Vector2(1f, 1f);
        bgRect.pivot = new Vector2(0.5f, 0.5f);

        Image bgImage = bgObject.AddComponent<Image>();
        bgImage.sprite = circleSprite;
        bgImage.color = new Color(0.2f, 0.2f, 0.2f, 0.4f); // ダークグレー半透明
        bgImage.type = Image.Type.Filled;
        bgImage.fillMethod = Image.FillMethod.Radial360;
        bgImage.fillOrigin = (int)Image.Origin360.Top;
        bgImage.fillClockwise = false; // falseで時計回りに減る
        bgImage.fillAmount = 1f;

        timerGaugeBackground = bgImage;
        Debug.Log($"[WaveTimerUI] Background created at {bgRect.anchoredPosition}");

        // === メインゲージの作成 ===
        GameObject gaugeObject = new GameObject("TimerGauge");
        gaugeObject.transform.SetParent(parentTransform, false);

        RectTransform gaugeRect = gaugeObject.AddComponent<RectTransform>();
        gaugeRect.anchoredPosition = gaugePosition; // Inspector設定値
        gaugeRect.sizeDelta = new Vector2(gaugeOuterSize, gaugeOuterSize); // Inspector設定値
        gaugeRect.anchorMin = new Vector2(1f, 1f); // 右上にアンカー
        gaugeRect.anchorMax = new Vector2(1f, 1f);
        gaugeRect.pivot = new Vector2(0.5f, 0.5f);

        Image gaugeImage = gaugeObject.AddComponent<Image>();
        gaugeImage.sprite = circleSprite;
        gaugeImage.color = neonColors[0]; // 初期色（ネオンブルー）
        gaugeImage.type = Image.Type.Filled;
        gaugeImage.fillMethod = Image.FillMethod.Radial360;
        gaugeImage.fillOrigin = (int)Image.Origin360.Top;
        gaugeImage.fillClockwise = false; // falseで時計回りに減る
        gaugeImage.fillAmount = 1f;

        timerGaugeImage = gaugeImage;
        timerGaugeRect = gaugeRect;
        Debug.Log($"[WaveTimerUI] Main gauge created at {gaugeRect.anchoredPosition}, color={gaugeImage.color}, fillClockwise=false");

        // === ドーナツ型の内側円作成（背景色で塗りつぶし） ===
        GameObject innerObject = new GameObject("InnerCircle");
        innerObject.transform.SetParent(parentTransform, false);

        RectTransform innerRect = innerObject.AddComponent<RectTransform>();
        innerRect.anchoredPosition = gaugePosition; // Inspector設定値
        innerRect.sizeDelta = new Vector2(gaugeInnerSize, gaugeInnerSize); // Inspector設定値
        innerRect.anchorMin = new Vector2(1f, 1f); // 右上にアンカー
        innerRect.anchorMax = new Vector2(1f, 1f);
        innerRect.pivot = new Vector2(0.5f, 0.5f);

        Image innerImage = innerObject.AddComponent<Image>();
        innerImage.sprite = circleSprite;
        innerImage.color = new Color(0.05f, 0.05f, 0.05f, 1f); // 非常に濃いグレー（ほぼ黒）
        timerGaugeInner = innerImage;
        Debug.Log($"[WaveTimerUI] Inner circle created at {innerRect.anchoredPosition}");

        // === Hierarchyの表示順序調整（背景→ゲージ→内側円→テキストの順） ===
        // 背景を一番後ろに
        int timerTextIndex = timerText.transform.GetSiblingIndex();
        bgObject.transform.SetSiblingIndex(timerTextIndex);

        // ゲージを背景の次に
        gaugeObject.transform.SetSiblingIndex(timerTextIndex + 1);

        // 内側円をゲージの次に
        innerObject.transform.SetSiblingIndex(timerTextIndex + 2);

        // TimerTextを最前面に移動し、位置も同期
        if (timerText != null)
        {
            timerText.transform.SetSiblingIndex(timerTextIndex + 3);

            // TimerTextの位置はInspector設定値を使用（ApplyTextPositions()で適用）
            // ここでは設定しない（Start()の後半で適用される）
        }

        Debug.Log($"[WaveTimerUI] Sibling indices - BG:{bgObject.transform.GetSiblingIndex()}, Gauge:{gaugeObject.transform.GetSiblingIndex()}, Inner:{innerObject.transform.GetSiblingIndex()}, Text:{timerText.transform.GetSiblingIndex()}");

        Debug.Log("[WaveTimerUI] Circular gauge created successfully!");
        Debug.Log($"[WaveTimerUI] Settings applied - Position:{gaugePosition}, OuterSize:{gaugeOuterSize}, InnerSize:{gaugeInnerSize}, Thickness:{(gaugeOuterSize - gaugeInnerSize) / 2f}");
    }

    /// <summary>
    /// 円形のスプライトを生成（Radial Filledで使用）
    /// </summary>
    private Sprite CreateCircleSprite()
    {
        int size = 128;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);

        Vector2 center = new Vector2(size / 2f, size / 2f);
        float radius = size / 2f;

        // 円形のテクスチャを生成
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

        // スプライトを生成
        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0, 0, size, size),
            new Vector2(0.5f, 0.5f),
            100f,
            0,
            SpriteMeshType.FullRect,
            Vector4.zero,
            false
        );

        return sprite;
    }

    private void Update()
    {
        if (enemySpawner == null) return;

        // タイマー更新
        UpdateTimerDisplay();

        // ステージ表示更新
        UpdateStageDisplay();

        // 配置パターン名表示更新
        if (showFormationName)
        {
            UpdateFormationDisplay();
        }
    }

    /// <summary>
    /// タイマー表示を更新
    /// </summary>
    private void UpdateTimerDisplay()
    {
        // Stage 3（index == 2）以降は完全に非表示
        int currentStageIndex = enemySpawner.GetCurrentStageIndex();
        if (currentStageIndex >= 2) // Stage 3以降（クリア後含む）
        {
            if (timerText != null) timerText.enabled = false;
            if (timerGaugeImage != null) timerGaugeImage.enabled = false;
            if (timerGaugeBackground != null) timerGaugeBackground.enabled = false;
            if (timerGaugeInner != null) timerGaugeInner.enabled = false;
            // パルスアニメーションをリセット
            if (timerGaugeRect != null) timerGaugeRect.localScale = Vector3.one;
            return;
        }

        // Stage 1・2ではゲージを必ず表示（他で非表示にされていても毎フレーム復帰）
        if (timerText != null) timerText.enabled = false; // 常に非表示（ユーザーリクエスト）
        if (timerGaugeImage != null)
        {
            timerGaugeImage.enabled = true;
            if (!timerGaugeImage.gameObject.activeInHierarchy)
            {
                timerGaugeImage.gameObject.SetActive(true);
                // 親が非アクティブだと表示されないため、親も有効化
                Transform p = timerGaugeImage.transform.parent;
                if (p != null && !p.gameObject.activeSelf)
                    p.gameObject.SetActive(true);
            }
        }
        if (timerGaugeBackground != null)
        {
            timerGaugeBackground.enabled = true;
            if (!timerGaugeBackground.gameObject.activeInHierarchy)
                timerGaugeBackground.gameObject.SetActive(true);
        }
        if (timerGaugeInner != null)
        {
            timerGaugeInner.enabled = true;
            if (!timerGaugeInner.gameObject.activeInHierarchy)
                timerGaugeInner.gameObject.SetActive(true);
        }

        float remainingTime = enemySpawner.GetStageRemainingTime();
        float timeLimit = enemySpawner.GetCurrentStageTimeLimit();

        // 時間制限がない場合は非表示
        if (timeLimit <= 0)
        {
            if (timerText != null) timerText.text = "";
            if (timerGaugeImage != null) timerGaugeImage.fillAmount = 0f;
            return;
        }

        // 残り時間を0以上にクランプ（負の値を防ぐ）
        remainingTime = Mathf.Max(0f, remainingTime);

        // === 数字のタイマー表示 ===
        if (timerText != null && showTimerText)
        {
            int minutes = Mathf.FloorToInt(remainingTime / 60f);
            int seconds = Mathf.FloorToInt(remainingTime % 60f);
            timerText.text = $"{minutes}:{seconds:00}";

            // 残り時間が少なくなったら色を変える
            if (remainingTime <= 30f)
            {
                timerText.color = Color.red;
            }
            else if (remainingTime <= 60f)
            {
                timerText.color = Color.yellow;
            }
            else
            {
                timerText.color = Color.white;
            }
        }
        else if (timerText != null)
        {
            timerText.text = ""; // 非表示設定の場合
        }

        // === 円形ゲージ表示 ===
        if (timerGaugeImage != null)
        {
            // fillAmountを1.0→0.0に減らす（時計回りで減少）
            float fillAmount = remainingTime / timeLimit;
            timerGaugeImage.fillAmount = fillAmount;

            // ネオンスペクトラムカラーの適用（全体時間で色変化）
            Color neonColor = GetNeonColorForTime(remainingTime, timeLimit);
            timerGaugeImage.color = neonColor;

            // パルスアニメーション（最後のフェイズのみ）
            if (enablePulseAnimation && timerGaugeRect != null)
            {
                // 最後のフェイズかどうかを判定（7色なので最後の1/7）
                float normalizedTime = 1f - (remainingTime / timeLimit);
                int colorCount = neonColors.Length;
                float lastPhaseStart = (float)(colorCount - 2) / (colorCount - 1); // 6/7 = 0.857...

                if (normalizedTime >= lastPhaseStart)
                {
                    // 最後のフェイズ：パルスアニメーション有効
                    float pulse = 1f + Mathf.Sin(Time.time * pulseSpeed) * pulseAmplitude;
                    timerGaugeRect.localScale = Vector3.one * pulse;
                }
                else
                {
                    // それ以外：通常サイズ
                    timerGaugeRect.localScale = Vector3.one;
                }
            }

            // 10秒ごとにログ出力（デバッグ用）
            if (Mathf.FloorToInt(remainingTime) % 10 == 0 && remainingTime > 0)
            {
                Debug.Log($"[WaveTimerUI] Gauge update - fillAmount:{fillAmount:F2}, color:{neonColor}, remainingTime:{remainingTime:F1}");
            }
        }
    }

    /// <summary>
    /// 残り時間に基づいてネオンスペクトラムカラーを取得
    /// </summary>
    private Color GetNeonColorForTime(float remainingTime, float timeLimit)
    {
        if (neonColors == null || neonColors.Length < 2)
        {
            return Color.white; // フォールバック
        }

        // 全体時間に対する進行度を計算（1.0→0.0）
        float normalizedTime = 1f - (remainingTime / timeLimit);

        // 5色の間を補間（0.0〜1.0を4つのセグメントに分割）
        int colorCount = neonColors.Length;
        float segmentSize = 1f / (colorCount - 1);
        int startIndex = Mathf.FloorToInt(normalizedTime / segmentSize);
        int endIndex = Mathf.Min(startIndex + 1, colorCount - 1);

        // セグメント内での位置（0〜1）
        float segmentProgress = (normalizedTime - startIndex * segmentSize) / segmentSize;

        // 2色間を線形補間
        Color resultColor = Color.Lerp(neonColors[startIndex], neonColors[endIndex], segmentProgress);

        // デバッグ用（10秒ごと）
        if (Mathf.FloorToInt(remainingTime) % 10 == 0 && remainingTime > 0)
        {
            Debug.Log($"[WaveTimerUI] Color calc - remaining:{remainingTime:F1}/{timeLimit:F0}, normalized:{normalizedTime:F2}, segment:{startIndex}->{endIndex}, progress:{segmentProgress:F2}, color:{resultColor}");
        }

        return resultColor;
    }

    /// <summary>
    /// ステージ表示を更新
    /// </summary>
    private void UpdateStageDisplay()
    {
        if (stageText == null) return;

        // Inspector設定で表示/非表示を制御
        if (!showStageText)
        {
            stageText.text = "";
            return;
        }

        // 他スクリプトやTMPで無効化されていても、表示ONのときは毎フレーム有効に戻す（Stage2で消える不具合対策）
        stageText.enabled = true;

        int currentStage = enemySpawner.GetCurrentStageIndex() + 1;  // 1始まりに変換
        int totalStages = enemySpawner.GetTotalStageCount();

        // 全ステージクリア後は最大ステージ数で固定
        currentStage = Mathf.Min(currentStage, totalStages);

        stageText.text = $"Wave {currentStage}/{totalStages}";
    }

    /// <summary>
    /// 配置パターン名表示を更新
    /// </summary>
    private void UpdateFormationDisplay()
    {
        if (formationText == null) return;

        string formationName = enemySpawner.GetCurrentFormationName();

        if (string.IsNullOrEmpty(formationName))
        {
            formationText.text = "";
        }
        else
        {
            formationText.text = $"Formation: {formationName}";
        }
    }

    /// <summary>
    /// ポーズボタンクリック時の処理
    /// </summary>
    private void OnPauseButtonClicked()
    {
        PauseManager pauseManager = PauseManager.Instance;
        if (pauseManager != null)
        {
            // ポーズ切り替え（既にポーズ中なら解除、そうでなければポーズ）
            if (pauseManager.IsPaused)
            {
                pauseManager.Resume();
            }
            else
            {
                pauseManager.Pause();
            }
        }
        else
        {
            Debug.LogWarning("[WaveTimerUI] PauseManager not found!");
        }
    }

#if UNITY_EDITOR
    /// <summary>
    /// Editor拡張：ポーズボタンを自動生成
    /// </summary>
    [ContextMenu("Setup Pause Button")]
    private void SetupPauseButton()
    {
        Debug.Log("[WaveTimerUI] Setting up Pause Button...");

        // Canvasを探す
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("[WaveTimerUI] Canvas not found!");
            return;
        }

        // TimerText（またはゲージ）の親を探す
        Transform parentTransform = null;
        if (timerGaugeImage != null)
        {
            parentTransform = timerGaugeImage.transform.parent;
        }
        else if (timerText != null)
        {
            parentTransform = timerText.transform.parent;
        }

        if (parentTransform == null)
        {
            Debug.LogError("[WaveTimerUI] Cannot find parent transform!");
            return;
        }

        // ポーズボタンを作成
        GameObject buttonObj = new GameObject("PauseButton");
        buttonObj.transform.SetParent(parentTransform, false);

        RectTransform buttonRect = buttonObj.AddComponent<RectTransform>();
        buttonRect.anchoredPosition = gaugePosition; // ゲージと同じ位置
        buttonRect.sizeDelta = new Vector2(pauseButtonSize, pauseButtonSize);
        buttonRect.anchorMin = new Vector2(1f, 1f); // 右上にアンカー
        buttonRect.anchorMax = new Vector2(1f, 1f);
        buttonRect.pivot = new Vector2(0.5f, 0.5f);

        // 円形スプライトを生成
        Sprite circleSprite = CreateCircleSprite();

        Image buttonImage = buttonObj.AddComponent<Image>();
        buttonImage.sprite = circleSprite;
        buttonImage.color = new Color(0.2f, 0.2f, 0.2f, 0.8f); // ダークグレー

        Button btn = buttonObj.AddComponent<Button>();
        pauseButton = btn;

        // ポーズアイコン（||）を作成
        CreatePauseIcon(buttonObj.transform);

        // Hierarchy順序調整（ボタンを最前面に）
        buttonObj.transform.SetAsLastSibling();

        Debug.Log("[WaveTimerUI] Pause button created successfully!");

        // SerializedObjectを使ってInspectorフィールドを正しく設定
        UnityEditor.SerializedObject serializedObject = new UnityEditor.SerializedObject(this);
        serializedObject.Update();

        UnityEditor.SerializedProperty pauseButtonProp = serializedObject.FindProperty("pauseButton");
        pauseButtonProp.objectReferenceValue = pauseButton;

        serializedObject.ApplyModifiedProperties();
        UnityEditor.EditorUtility.SetDirty(this);

        Debug.Log("[WaveTimerUI] Pause button reference set via SerializedObject");
    }

    /// <summary>
    /// ポーズアイコン（||）を作成
    /// </summary>
    private void CreatePauseIcon(Transform parent)
    {
        float iconWidth = pauseButtonSize * 0.4f;
        float iconHeight = pauseButtonSize * 0.5f;
        float barWidth = iconWidth * 0.3f;
        float barSpacing = iconWidth * 0.4f;

        // 左のバー
        GameObject leftBar = new GameObject("LeftBar");
        leftBar.transform.SetParent(parent, false);

        RectTransform leftBarRect = leftBar.AddComponent<RectTransform>();
        leftBarRect.anchoredPosition = new Vector2(-barSpacing / 2f, 0f);
        leftBarRect.sizeDelta = new Vector2(barWidth, iconHeight);

        Image leftBarImage = leftBar.AddComponent<Image>();
        leftBarImage.color = Color.white;

        // 右のバー
        GameObject rightBar = new GameObject("RightBar");
        rightBar.transform.SetParent(parent, false);

        RectTransform rightBarRect = rightBar.AddComponent<RectTransform>();
        rightBarRect.anchoredPosition = new Vector2(barSpacing / 2f, 0f);
        rightBarRect.sizeDelta = new Vector2(barWidth, iconHeight);

        Image rightBarImage = rightBar.AddComponent<Image>();
        rightBarImage.color = Color.white;

        Debug.Log("[WaveTimerUI] Pause icon (||) created");
    }
#endif
}
