using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Game.UI;

/// <summary>
/// ポーズメニューUIの管理
/// 4つのパネル（Main/Confirm/Sound/Help）を切り替え
/// Play前のInspectorで全て調整可能
/// </summary>
public class PauseMenuUI : MonoBehaviour
{
    [Header("Debug")]
    [SerializeField] private bool showDebugLog = false;

    [Header("Panel References")]
    [Tooltip("暗転パネル（半透明黒）")]
    [SerializeField] private GameObject dimPanel;

    [Tooltip("メインパネル（Resume/Retire/Sound/Help）")]
    [SerializeField] private GameObject mainPanel;

    [Tooltip("確認パネル（Retire確認: Yes/No）")]
    [SerializeField] private GameObject confirmPanel;

    [Tooltip("サウンドパネル（音量調整）")]
    [SerializeField] private GameObject soundPanel;

    [Tooltip("ヘルプパネル（操作説明）")]
    [SerializeField] private GameObject helpPanel;

    [Tooltip("インプットパネル（操作モード設定）")]
    [SerializeField] private GameObject inputPanel;

    [Header("Main Panel Buttons")]
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button retireButton;
    [SerializeField] private Button soundButton;
    [SerializeField] private Button inputButton;
    [SerializeField] private Button helpButton;

    [Header("Confirm Panel Buttons")]
    [SerializeField] private Button confirmYesButton;
    [SerializeField] private Button confirmNoButton;

    [Header("Sound Panel")]
    [SerializeField] private Slider bgmVolumeSlider;
    [SerializeField] private Slider seVolumeSlider;
    [SerializeField] private TextMeshProUGUI bgmVolumeText;
    [SerializeField] private TextMeshProUGUI seVolumeText;
    [SerializeField] private Button soundBackButton;

    [Header("Help Panel")]
    [SerializeField] private TextMeshProUGUI helpText;
    [SerializeField] private Button helpBackButton;

    [Header("Input Panel")]
    [SerializeField] private Button inputBackButton;
    [Tooltip("SlowMotionUIManagerへの参照（自動取得）")]
    [SerializeField] private SlowMotionUIManager slowMotionUIManager;
    [Tooltip("ホールドモードトグル（InputPanel内）")]
    [SerializeField] private UnityEngine.UI.Toggle holdModeToggle;

    [Header("Dim Panel Settings")]
    [Tooltip("暗転パネルの色（半透明黒）")]
    [SerializeField] private Color dimColor = new Color(0f, 0f, 0f, 0.7f);

    private PauseManager pauseManager;
    private SoundSettingsManager soundSettingsManager;
    private Game.UI.SceneController sceneController;

    private void Awake()
    {
        // ボタンイベントを登録
        SetupButtonEvents();

        // スライダーイベントを登録
        SetupSliderEvents();

        // 初期状態は非表示
        HideAllPanels();
    }

    private void Start()
    {
        // マネージャー参照を取得（Start()で取得することで、他のManagerが初期化される時間を確保）
        pauseManager = PauseManager.Instance;
        soundSettingsManager = SoundSettingsManager.Instance;
        sceneController = FindFirstObjectByType<Game.UI.SceneController>();

        if (slowMotionUIManager == null)
            slowMotionUIManager = FindFirstObjectByType<SlowMotionUIManager>();

        // イベントを購読
        SubscribeToEvents();

        if (pauseManager == null)
        {
            Debug.LogError("[PauseMenuUI] PauseManager not found! Make sure PauseManager exists in the scene.");
        }

        if (soundSettingsManager == null)
        {
            Debug.LogWarning("[PauseMenuUI] SoundSettingsManager not found!");
        }
    }

    private void OnEnable()
    {
        // イベントを購読
        SubscribeToEvents();
    }

    /// <summary>
    /// PauseManagerのイベントを購読
    /// </summary>
    private void SubscribeToEvents()
    {
        // マネージャーがnullなら再取得
        if (pauseManager == null)
        {
            pauseManager = PauseManager.Instance;
        }

        if (pauseManager != null)
        {
            // 既存のイベントを解除してから再登録（二重登録を防ぐ）
            pauseManager.OnPauseStarted -= OnPauseStarted;
            pauseManager.OnPauseEnded -= OnPauseEnded;

            pauseManager.OnPauseStarted += OnPauseStarted;
            pauseManager.OnPauseEnded += OnPauseEnded;
        }
    }

    private void OnDisable()
    {
        // PauseManagerのイベントを解除
        if (pauseManager != null)
        {
            pauseManager.OnPauseStarted -= OnPauseStarted;
            pauseManager.OnPauseEnded -= OnPauseEnded;
        }
    }

    /// <summary>
    /// ボタンイベントを設定
    /// </summary>
    private void SetupButtonEvents()
    {
        // Main Panel
        if (resumeButton != null)
            resumeButton.onClick.AddListener(OnResumeButtonClicked);

        if (retireButton != null)
            retireButton.onClick.AddListener(OnRetireButtonClicked);

        if (soundButton != null)
            soundButton.onClick.AddListener(OnSoundButtonClicked);

        if (inputButton != null)
            inputButton.onClick.AddListener(OnInputButtonClicked);

        if (helpButton != null)
            helpButton.onClick.AddListener(OnHelpButtonClicked);

        // Confirm Panel
        if (confirmYesButton != null)
            confirmYesButton.onClick.AddListener(OnConfirmYesButtonClicked);

        if (confirmNoButton != null)
            confirmNoButton.onClick.AddListener(OnConfirmNoButtonClicked);

        // Sound Panel
        if (soundBackButton != null)
            soundBackButton.onClick.AddListener(OnSoundBackButtonClicked);

        // Help Panel
        if (helpBackButton != null)
            helpBackButton.onClick.AddListener(OnHelpBackButtonClicked);

        // Input Panel
        if (inputBackButton != null)
            inputBackButton.onClick.AddListener(OnInputBackButtonClicked);

        if (holdModeToggle != null)
            holdModeToggle.onValueChanged.AddListener(OnHoldModeToggleChanged);
    }

    /// <summary>
    /// スライダーイベントを設定
    /// </summary>
    private void SetupSliderEvents()
    {
        if (bgmVolumeSlider != null)
        {
            bgmVolumeSlider.onValueChanged.AddListener(OnBGMVolumeChanged);
        }

        if (seVolumeSlider != null)
        {
            seVolumeSlider.onValueChanged.AddListener(OnSEVolumeChanged);
        }
    }

    /// <summary>
    /// ポーズ開始時の処理
    /// </summary>
    private void OnPauseStarted()
    {
        if (showDebugLog) Debug.Log("[PauseMenuUI] OnPauseStarted called. Showing main panel...");
        ShowMainPanel();
    }

    /// <summary>
    /// ポーズ解除時の処理
    /// </summary>
    private void OnPauseEnded()
    {
        if (showDebugLog) Debug.Log("[PauseMenuUI] OnPauseEnded called. Hiding all panels...");
        HideAllPanels();
    }

    /// <summary>
    /// 全パネルを非表示
    /// </summary>
    private void HideAllPanels()
    {
        if (dimPanel != null) dimPanel.SetActive(false);
        if (mainPanel != null) mainPanel.SetActive(false);
        if (confirmPanel != null) confirmPanel.SetActive(false);
        if (soundPanel != null) soundPanel.SetActive(false);
        if (helpPanel != null) helpPanel.SetActive(false);
        if (inputPanel != null) inputPanel.SetActive(false);

        if (showDebugLog) Debug.Log($"[PauseMenuUI] Panels hidden - dimPanel:{dimPanel != null}, mainPanel:{mainPanel != null}");
    }

    /// <summary>
    /// メインパネルを表示
    /// </summary>
    private void ShowMainPanel()
    {
        if (showDebugLog) Debug.Log($"[PauseMenuUI] ShowMainPanel called - dimPanel:{dimPanel != null}, mainPanel:{mainPanel != null}");

        HideAllPanels();
        if (dimPanel != null) dimPanel.SetActive(true);
        if (mainPanel != null) mainPanel.SetActive(true);

        if (showDebugLog) Debug.Log("[PauseMenuUI] Main panel should be visible now");
    }

    /// <summary>
    /// 確認パネルを表示
    /// </summary>
    private void ShowConfirmPanel()
    {
        HideAllPanels();
        if (dimPanel != null) dimPanel.SetActive(true);
        if (confirmPanel != null) confirmPanel.SetActive(true);
    }

    /// <summary>
    /// サウンドパネルを表示
    /// </summary>
    private void ShowSoundPanel()
    {
        HideAllPanels();
        if (dimPanel != null) dimPanel.SetActive(true);
        if (soundPanel != null) soundPanel.SetActive(true);

        // 現在の音量をスライダーに反映
        if (soundSettingsManager != null)
        {
            if (bgmVolumeSlider != null)
            {
                bgmVolumeSlider.value = soundSettingsManager.BGMVolume;
                UpdateBGMVolumeText(soundSettingsManager.BGMVolume);
            }

            if (seVolumeSlider != null)
            {
                seVolumeSlider.value = soundSettingsManager.SEVolume;
                UpdateSEVolumeText(soundSettingsManager.SEVolume);
            }
        }

    }

    /// <summary>
    /// インプットパネルを表示
    /// </summary>
    private void ShowInputPanel()
    {
        HideAllPanels();
        if (dimPanel != null) dimPanel.SetActive(true);
        if (inputPanel != null) inputPanel.SetActive(true);

        // 現在のホールドモード設定をトグルに反映
        if (holdModeToggle != null && slowMotionUIManager != null)
            holdModeToggle.SetIsOnWithoutNotify(slowMotionUIManager.UseHoldMode);
    }

    /// <summary>
    /// ヘルプパネルを表示
    /// </summary>
    private void ShowHelpPanel()
    {
        HideAllPanels();
        if (dimPanel != null) dimPanel.SetActive(true);
        if (helpPanel != null) helpPanel.SetActive(true);
    }

    // ===== Button Callbacks =====

    private void OnResumeButtonClicked()
    {
        if (pauseManager != null)
        {
            pauseManager.PlayButtonClickSound();
            pauseManager.Resume();
        }
    }

    private void OnRetireButtonClicked()
    {
        if (pauseManager != null)
        {
            pauseManager.PlayButtonClickSound();
        }
        ShowConfirmPanel();
    }

    private void OnSoundButtonClicked()
    {
        if (pauseManager != null)
        {
            pauseManager.PlayButtonClickSound();
        }
        ShowSoundPanel();
    }

    private void OnInputButtonClicked()
    {
        if (pauseManager != null)
            pauseManager.PlayButtonClickSound();
        ShowInputPanel();
    }

    private void OnHelpButtonClicked()
    {
        if (pauseManager != null)
        {
            pauseManager.PlayButtonClickSound();
        }
        ShowHelpPanel();
    }

    private void OnConfirmYesButtonClicked()
    {
        if (pauseManager != null)
        {
            pauseManager.PlayConfirmSound();
        }

        // ゲームを終了してAreaSelectへ戻る
        Time.timeScale = 1f; // Time.timeScaleを復元してからシーン遷移

        // SceneControllerを再取得（念のため）
        if (sceneController == null)
        {
            sceneController = FindFirstObjectByType<Game.UI.SceneController>();
        }

        if (sceneController != null)
        {
            if (showDebugLog) Debug.Log("[PauseMenuUI] Calling BackToAreaSelect()...");
            sceneController.BackToAreaSelect();
        }
        else
        {
            Debug.LogError("[PauseMenuUI] SceneController not found! Please add SceneController component to a GameObject in the scene.");
            // パネルを非表示にして、ゲームを続行可能にする
            HideAllPanels();
        }
    }

    private void OnConfirmNoButtonClicked()
    {
        if (pauseManager != null)
        {
            pauseManager.PlayCancelSound();
        }
        ShowMainPanel();
    }

    private void OnSoundBackButtonClicked()
    {
        if (pauseManager != null)
        {
            pauseManager.PlayCancelSound();
        }
        ShowMainPanel();
    }

    private void OnHelpBackButtonClicked()
    {
        if (pauseManager != null)
        {
            pauseManager.PlayCancelSound();
        }
        ShowMainPanel();
    }

    private void OnInputBackButtonClicked()
    {
        if (pauseManager != null)
            pauseManager.PlayCancelSound();
        ShowMainPanel();
    }

    // ===== Slider Callbacks =====

    private void OnBGMVolumeChanged(float value)
    {
        if (soundSettingsManager != null)
        {
            soundSettingsManager.BGMVolume = value;
            UpdateBGMVolumeText(value);
        }
    }

    private void OnSEVolumeChanged(float value)
    {
        if (soundSettingsManager != null)
        {
            soundSettingsManager.SEVolume = value;
            UpdateSEVolumeText(value);
        }
    }

    private void UpdateBGMVolumeText(float value)
    {
        if (bgmVolumeText != null)
        {
            bgmVolumeText.text = $"BGM: {Mathf.RoundToInt(value * 100)}%";
        }
    }

    private void UpdateSEVolumeText(float value)
    {
        if (seVolumeText != null)
        {
            seVolumeText.text = $"SE: {Mathf.RoundToInt(value * 100)}%";
        }
    }

    private void OnHoldModeToggleChanged(bool isOn)
    {
        if (slowMotionUIManager != null)
            slowMotionUIManager.SetHoldMode(isOn);
    }

#if UNITY_EDITOR
    // ★ヘルプ画面の本文。チュートリアルでは説明していない要素（スキル選択・円の効果・魂救済の注意点・
    // ジェム/ドリンク）を補完する内容にしている
    private const string HelpSectionTitleTagOpen = "<color=#FFD94C><size=28>";
    private const string HelpSectionTitleTagClose = "</size></color>";

    private const string HelpTextContent =
        HelpSectionTitleTagOpen + "赤線の特徴" + HelpSectionTitleTagClose + "\n" +
        "持続時間・反射加速・硬度が白線を上回る性能を持つ。\n" +
        "消費が大きく、回復時間も長いことから常用はできないが、白線との併用や重要な局面で使うと効果的。\n\n" +
        HelpSectionTitleTagOpen + "円の様々な効果" + HelpSectionTitleTagClose + "\n" +
        "反射した弾と敵を同じ円で囲むと、短時間で複数回のダメージを与えることができる。\n" +
        "HP0になった際は、抜け出した魂を円で囲むと復活できる。\n" +
        "特定の弾の効果を消したり、ブロックから出現するゴールドやハートの取得量が増える。\n\n" +
        HelpSectionTitleTagOpen + "魂の救済の注意点" + HelpSectionTitleTagClose + "\n" +
        "救済するごとにHP全快で復活できるが、再度魂が抜けだした際は落下速度が上がるため、救済が困難になる。\n\n" +
        HelpSectionTitleTagOpen + "ジェムの取得と効果" + HelpSectionTitleTagClose + "\n" +
        "スキル効果を得られる不思議な宝石。\n" +
        "エリアセレクトで着脱ができる。\n" +
        "新しいエリアを開放する毎に装備上限値が上昇する。\n" +
        "プレイする度に使用可能回数が減り、0になると壊れて消滅する。\n\n" +
        HelpSectionTitleTagOpen + "ドリンクの購入と効果" + HelpSectionTitleTagClose + "\n" +
        "1プレイ限りの一時的なスキルブーストが得られる飲み物。\n" +
        "エリアセレクトで購入でき、最大3回まで購入可能だが、同じドリンクは複数購入できない。";

    /// <summary>
    /// 既存のHierarchyにInputPanelとInputButtonを追加する
    /// </summary>
    [ContextMenu("Add Input Panel")]
    private void AddInputPanelToExistingHierarchy()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) { Debug.LogError("[PauseMenuUI] Canvas not found!"); return; }

        if (mainPanel == null) { Debug.LogError("[PauseMenuUI] mainPanel が未設定です。"); return; }

        // 重複防止
        if (inputPanel != null) { Debug.LogWarning("[PauseMenuUI] InputPanel は既に存在します。"); return; }

        // === InputPanel を作成 ===
        CreateInputPanel(canvas.transform);

        // === MainPanelにINPUTボタンを追加（HELPボタンの前に挿入）===
        Transform helpBtn = mainPanel.transform.Find("HelpButton");
        int insertIndex = helpBtn != null ? helpBtn.GetSiblingIndex() : mainPanel.transform.childCount;

        inputButton = CreateButton(mainPanel.transform, "InputButton", "INPUT", 60f, createBg: true);
        inputButton.transform.SetSiblingIndex(insertIndex);

        // InputButton配下にInputIconを追加（InputBgの上に重ねる）
        GameObject inputIconObj = new GameObject("InputIcon");
        inputIconObj.transform.SetParent(inputButton.transform, false);
        RectTransform inputIconRect = inputIconObj.AddComponent<RectTransform>();
        inputIconRect.anchorMin = new Vector2(0.5f, 0.5f);
        inputIconRect.anchorMax = new Vector2(0.5f, 0.5f);
        inputIconRect.sizeDelta = new Vector2(100f, 60f);
        inputIconRect.anchoredPosition = Vector2.zero;
        inputIconObj.AddComponent<Image>().raycastTarget = false;
        LayoutElement inputIconLayout = inputIconObj.AddComponent<LayoutElement>();
        inputIconLayout.ignoreLayout = true;

        // InputButtonにホバーエフェクトを追加（blinkTargetはInputBg）
        var inputHoverAdd = inputButton.gameObject.AddComponent<ButtonHoverEffect>();
        var inputBgImageAdd = inputButton.transform.Find("InputBg")?.GetComponent<Image>();
        if (inputBgImageAdd != null)
        {
            var soInput = new UnityEditor.SerializedObject(inputHoverAdd);
            soInput.FindProperty("blinkTarget").objectReferenceValue = inputBgImageAdd;
            soInput.ApplyModifiedProperties();
        }

        // MainPanelの高さを拡張（ボタン1つ分: 60 + spacing 20）
        RectTransform mainRect = mainPanel.GetComponent<RectTransform>();
        if (mainRect != null)
            mainRect.sizeDelta += new Vector2(0f, 80f);

        // SerializedObjectで参照を設定
        UnityEditor.SerializedObject so = new UnityEditor.SerializedObject(this);
        so.Update();
        so.FindProperty("inputPanel").objectReferenceValue = inputPanel;
        so.FindProperty("inputButton").objectReferenceValue = inputButton;
        so.FindProperty("inputBackButton").objectReferenceValue = inputBackButton;
        so.FindProperty("holdModeToggle").objectReferenceValue = holdModeToggle;
        so.ApplyModifiedProperties();
        UnityEditor.EditorUtility.SetDirty(this);

        if (showDebugLog) Debug.Log("[PauseMenuUI] InputPanel と InputButton を追加しました。");
    }

    /// <summary>
    /// Editor拡張：ポーズメニューUIを自動生成
    /// </summary>
    [ContextMenu("Setup Pause Menu UI")]
    private void SetupPauseMenuUI()
    {
        if (showDebugLog) Debug.Log("[PauseMenuUI] Setting up Pause Menu UI...");

        // Canvasを探す
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("[PauseMenuUI] Canvas not found!");
            return;
        }

        // 暗転パネルを作成
        CreateDimPanel(canvas.transform);

        // メインパネルを作成
        CreateMainPanel(canvas.transform);

        // 確認パネルを作成
        CreateConfirmPanel(canvas.transform);

        // サウンドパネルを作成
        CreateSoundPanel(canvas.transform);

        // ヘルプパネルを作成
        CreateHelpPanel(canvas.transform);

        if (showDebugLog) Debug.Log("[PauseMenuUI] Pause Menu UI setup complete!");

        // SerializedObjectを使ってInspectorフィールドを正しく設定
        UnityEditor.SerializedObject serializedObject = new UnityEditor.SerializedObject(this);
        serializedObject.Update();

        UnityEditor.SerializedProperty dimPanelProp = serializedObject.FindProperty("dimPanel");
        UnityEditor.SerializedProperty mainPanelProp = serializedObject.FindProperty("mainPanel");
        UnityEditor.SerializedProperty confirmPanelProp = serializedObject.FindProperty("confirmPanel");
        UnityEditor.SerializedProperty soundPanelProp = serializedObject.FindProperty("soundPanel");
        UnityEditor.SerializedProperty helpPanelProp = serializedObject.FindProperty("helpPanel");
        UnityEditor.SerializedProperty resumeButtonProp = serializedObject.FindProperty("resumeButton");
        UnityEditor.SerializedProperty retireButtonProp = serializedObject.FindProperty("retireButton");
        UnityEditor.SerializedProperty soundButtonProp = serializedObject.FindProperty("soundButton");
        UnityEditor.SerializedProperty helpButtonProp = serializedObject.FindProperty("helpButton");
        UnityEditor.SerializedProperty confirmYesButtonProp = serializedObject.FindProperty("confirmYesButton");
        UnityEditor.SerializedProperty confirmNoButtonProp = serializedObject.FindProperty("confirmNoButton");
        UnityEditor.SerializedProperty bgmVolumeSliderProp = serializedObject.FindProperty("bgmVolumeSlider");
        UnityEditor.SerializedProperty seVolumeSliderProp = serializedObject.FindProperty("seVolumeSlider");
        UnityEditor.SerializedProperty bgmVolumeTextProp = serializedObject.FindProperty("bgmVolumeText");
        UnityEditor.SerializedProperty seVolumeTextProp = serializedObject.FindProperty("seVolumeText");
        UnityEditor.SerializedProperty soundBackButtonProp = serializedObject.FindProperty("soundBackButton");
        UnityEditor.SerializedProperty helpTextProp = serializedObject.FindProperty("helpText");
        UnityEditor.SerializedProperty helpBackButtonProp = serializedObject.FindProperty("helpBackButton");
        UnityEditor.SerializedProperty holdModeToggleProp = serializedObject.FindProperty("holdModeToggle");

        dimPanelProp.objectReferenceValue = dimPanel;
        mainPanelProp.objectReferenceValue = mainPanel;
        confirmPanelProp.objectReferenceValue = confirmPanel;
        soundPanelProp.objectReferenceValue = soundPanel;
        helpPanelProp.objectReferenceValue = helpPanel;
        resumeButtonProp.objectReferenceValue = resumeButton;
        retireButtonProp.objectReferenceValue = retireButton;
        soundButtonProp.objectReferenceValue = soundButton;
        helpButtonProp.objectReferenceValue = helpButton;
        confirmYesButtonProp.objectReferenceValue = confirmYesButton;
        confirmNoButtonProp.objectReferenceValue = confirmNoButton;
        bgmVolumeSliderProp.objectReferenceValue = bgmVolumeSlider;
        seVolumeSliderProp.objectReferenceValue = seVolumeSlider;
        bgmVolumeTextProp.objectReferenceValue = bgmVolumeText;
        seVolumeTextProp.objectReferenceValue = seVolumeText;
        soundBackButtonProp.objectReferenceValue = soundBackButton;
        helpTextProp.objectReferenceValue = helpText;
        helpBackButtonProp.objectReferenceValue = helpBackButton;
        holdModeToggleProp.objectReferenceValue = holdModeToggle;

        serializedObject.ApplyModifiedProperties();
        UnityEditor.EditorUtility.SetDirty(this);

        if (showDebugLog) Debug.Log("[PauseMenuUI] All references set via SerializedObject");
    }

    // ★既存UIの削除・再生成は行わず、タイトル等の文言だけをコード側の最新値に合わせて更新する安全なメニュー
    [ContextMenu("Update UI Texts (文言だけ更新・削除再生成なし)")]
    private void UpdateUITexts()
    {
        UpdateChildText(mainPanel, "TitleText", "中断メニュー", bold: true);
        UpdateChildText(confirmPanel, "ConfirmText", "エリアセレクトに戻りますか？");
        UpdateChildText(soundPanel, "TitleText", "サウンド設定", bold: true);
        UpdateChildText(inputPanel, "TitleText", "スローモーション操作設定", bold: true);
        UpdateChildText(helpPanel, "TitleText", "ヘルプ", bold: true);

        if (helpText != null)
        {
            helpText.text = HelpTextContent;
            UnityEditor.EditorUtility.SetDirty(helpText);
        }

        UnityEditor.EditorUtility.SetDirty(this);
        if (showDebugLog) Debug.Log("[PauseMenuUI] UpdateUITexts: 文言を更新しました。", this);
    }

    // ★調査中に行った2回の変更（RetireBgの縮小 → サイズ復元+raycastTarget有効化）が
    // いずれも事態を悪化させたため、最初の状態（サイズ730x410・中央配置・raycastTarget=false）へ完全に戻す。
    // これ以上の推測での修正は行わず、まず既知の正常な状態へ戻すことを優先する
    [ContextMenu("Revert Retire Button Changes (調査前の状態に戻す)")]
    private void RevertRetireButtonChanges()
    {
        if (retireButton == null)
        {
            Debug.LogWarning("[PauseMenuUI] RevertRetireButtonChanges: retireButton が未設定です。", this);
            return;
        }

        Transform retireBg = retireButton.transform.Find("RetireBg");
        if (retireBg == null)
        {
            Debug.LogWarning("[PauseMenuUI] RevertRetireButtonChanges: RetireBg が見つかりませんでした。", this);
            return;
        }

        RectTransform bgRect = retireBg.GetComponent<RectTransform>();
        if (bgRect != null)
        {
            bgRect.anchorMin = new Vector2(0.5f, 0.5f);
            bgRect.anchorMax = new Vector2(0.5f, 0.5f);
            bgRect.sizeDelta = new Vector2(730f, 410f);
            bgRect.anchoredPosition = Vector2.zero;
            UnityEditor.EditorUtility.SetDirty(bgRect);
        }

        Image bgImage = retireBg.GetComponent<Image>();
        if (bgImage != null)
        {
            bgImage.raycastTarget = false;
            UnityEditor.EditorUtility.SetDirty(bgImage);
        }

        UnityEditor.EditorUtility.SetDirty(this);
        if (showDebugLog) Debug.Log("[PauseMenuUI] RevertRetireButtonChanges: 調査前の状態に戻しました。", this);
    }

    // ★既存シーンの平坦な（スクロールしない）HelpTextだけを、スクロール可能な構造に安全に置き換える。
    // HelpPanel以下の他要素（TitleText/BackButton等）やPauseMenuUI全体の再生成は一切行わない
    [ContextMenu("Upgrade Help Text To Scrollable (ヘルプ本文だけスクロール対応に置き換え)")]
    private void UpgradeHelpTextToScrollable()
    {
        if (helpPanel == null)
        {
            Debug.LogWarning("[PauseMenuUI] UpgradeHelpTextToScrollable: helpPanel が未設定です。", this);
            return;
        }

        // ★再実行しても安全なように、旧「HelpText」・前回作成済みの「HelpScrollView」どちらも探して置き換える
        Transform old = helpPanel.transform.Find("HelpScrollView");
        if (old == null) old = helpPanel.transform.Find("HelpText");
        int siblingIndex = old != null ? old.GetSiblingIndex() : 1;
        if (old != null)
        {
            DestroyImmediate(old.gameObject);
        }

        helpText = CreateScrollableHelpText(helpPanel.transform, siblingIndex, 360f);

        // ★タイトルを上端・戻るボタンを下端に固定し、間の余白を全てスクロール領域に割り当てる
        VerticalLayoutGroup helpLayout = helpPanel.GetComponent<VerticalLayoutGroup>();
        if (helpLayout != null)
        {
            helpLayout.spacing = 15f;
            helpLayout.padding = new RectOffset(40, 40, 15, 15);
            helpLayout.childAlignment = TextAnchor.UpperCenter;
            helpLayout.childControlHeight = true;
            UnityEditor.EditorUtility.SetDirty(helpLayout);
        }

        // ★エディタ上でも即座に見た目へ反映されるよう、レイアウトを強制的に再計算する
        UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(helpPanel.GetComponent<RectTransform>());

        UnityEditor.EditorUtility.SetDirty(this);
        UnityEditor.EditorUtility.SetDirty(helpPanel);
        if (showDebugLog) Debug.Log("[PauseMenuUI] UpgradeHelpTextToScrollable: HelpTextをスクロール対応構造に置き換えました。", this);
    }

    private void UpdateChildText(GameObject panel, string childName, string newText, bool bold = false)
    {
        if (panel == null)
        {
            Debug.LogWarning($"[PauseMenuUI] UpdateUITexts: パネル参照が未設定のため{childName}を更新できませんでした。", this);
            return;
        }

        Transform child = panel.transform.Find(childName);
        if (child == null)
        {
            Debug.LogWarning($"[PauseMenuUI] UpdateUITexts: {panel.name}の子に{childName}が見つかりませんでした。", this);
            return;
        }

        TextMeshProUGUI tmp = child.GetComponent<TextMeshProUGUI>();
        if (tmp != null)
        {
            tmp.text = newText;
            if (bold) tmp.fontStyle = FontStyles.Bold;
            UnityEditor.EditorUtility.SetDirty(tmp);
        }
    }

    private void CreateDimPanel(Transform parent)
    {
        GameObject dimObj = new GameObject("DimPanel");
        dimObj.transform.SetParent(parent, false);

        RectTransform dimRect = dimObj.AddComponent<RectTransform>();
        dimRect.anchorMin = Vector2.zero;
        dimRect.anchorMax = Vector2.one;
        dimRect.sizeDelta = Vector2.zero;
        dimRect.anchoredPosition = Vector2.zero;

        Image dimImage = dimObj.AddComponent<Image>();
        dimImage.color = dimColor;

        dimPanel = dimObj;
        dimObj.SetActive(false);

        if (showDebugLog) Debug.Log("[PauseMenuUI] Dim panel created");
    }

    private void CreateMainPanel(Transform parent)
    {
        GameObject mainObj = new GameObject("MainPanel");
        mainObj.transform.SetParent(parent, false);

        RectTransform mainRect = mainObj.AddComponent<RectTransform>();
        mainRect.anchorMin = new Vector2(0.5f, 0.5f);
        mainRect.anchorMax = new Vector2(0.5f, 0.5f);
        mainRect.sizeDelta = new Vector2(400f, 500f);
        mainRect.anchoredPosition = new Vector2(140f, 0f); // SkillHUD(280px)を除外したエリアの中央

        // 背景画像用の子オブジェクト（MainBg）を作成
        GameObject mainBgObj = new GameObject("MainBg");
        mainBgObj.transform.SetParent(mainObj.transform, false);
        RectTransform mainBgRect = mainBgObj.AddComponent<RectTransform>();
        mainBgRect.anchorMin = Vector2.zero;
        mainBgRect.anchorMax = Vector2.one;
        mainBgRect.sizeDelta = Vector2.zero;
        mainBgRect.anchoredPosition = Vector2.zero;
        Image mainBg = mainBgObj.AddComponent<Image>();
        mainBg.color = new Color(0.1f, 0.1f, 0.1f, 0.95f);
        LayoutElement mainBgLayout = mainBgObj.AddComponent<LayoutElement>();
        mainBgLayout.ignoreLayout = true;

        // VerticalLayoutGroupを追加
        VerticalLayoutGroup layout = mainObj.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 20f;
        layout.padding = new RectOffset(40, 40, 40, 40);
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        // タイトルテキストを作成
        CreateText(mainObj.transform, "TitleText", "中断メニュー", 48, TextAlignmentOptions.Center, 80f).fontStyle = FontStyles.Bold;

        // ボタンを作成
        resumeButton = CreateButton(mainObj.transform, "ResumeButton", "RESUME", 60f, createBg: true);

        // ResumeButton配下にResumeIconを追加（ResumeBgの上に重ねる）
        GameObject resumeIconObj = new GameObject("ResumeIcon");
        resumeIconObj.transform.SetParent(resumeButton.transform, false);
        RectTransform resumeIconRect = resumeIconObj.AddComponent<RectTransform>();
        resumeIconRect.anchorMin = new Vector2(0.5f, 0.5f);
        resumeIconRect.anchorMax = new Vector2(0.5f, 0.5f);
        resumeIconRect.sizeDelta = new Vector2(100f, 60f);
        resumeIconRect.anchoredPosition = Vector2.zero;
        resumeIconObj.AddComponent<Image>().raycastTarget = false;
        LayoutElement resumeIconLayout = resumeIconObj.AddComponent<LayoutElement>();
        resumeIconLayout.ignoreLayout = true;

        // ResumeButtonにホバーエフェクトを追加（blinkTargetはResumeBg）
        var resumeHover = resumeButton.gameObject.AddComponent<ButtonHoverEffect>();
        var resumeBgImage = resumeButton.transform.Find("ResumeBg")?.GetComponent<Image>();
        if (resumeBgImage != null)
        {
            var so = new UnityEditor.SerializedObject(resumeHover);
            so.FindProperty("blinkTarget").objectReferenceValue = resumeBgImage;
            so.ApplyModifiedProperties();
        }

        retireButton = CreateButton(mainObj.transform, "RetireButton", "RETIRE", 60f, createBg: true);

        // RetireButton配下にRetireIconを追加（RetireBgの上に重ねる）
        GameObject retireIconObj = new GameObject("RetireIcon");
        retireIconObj.transform.SetParent(retireButton.transform, false);
        RectTransform retireIconRect = retireIconObj.AddComponent<RectTransform>();
        retireIconRect.anchorMin = new Vector2(0.5f, 0.5f);
        retireIconRect.anchorMax = new Vector2(0.5f, 0.5f);
        retireIconRect.sizeDelta = new Vector2(100f, 60f);
        retireIconRect.anchoredPosition = Vector2.zero;
        retireIconObj.AddComponent<Image>().raycastTarget = false;
        LayoutElement retireIconLayout = retireIconObj.AddComponent<LayoutElement>();
        retireIconLayout.ignoreLayout = true;

        // RetireButtonにホバーエフェクトを追加（blinkTargetはRetireBg）
        var retireHover = retireButton.gameObject.AddComponent<ButtonHoverEffect>();
        var retireBgImage = retireButton.transform.Find("RetireBg")?.GetComponent<Image>();
        if (retireBgImage != null)
        {
            var soRetire = new UnityEditor.SerializedObject(retireHover);
            soRetire.FindProperty("blinkTarget").objectReferenceValue = retireBgImage;
            soRetire.ApplyModifiedProperties();
        }

        soundButton = CreateButton(mainObj.transform, "SoundButton", "SOUND", 60f, createBg: true);

        // SoundButton配下にSoundIconを追加（SoundBgの上に重ねる）
        GameObject soundIconObj = new GameObject("SoundIcon");
        soundIconObj.transform.SetParent(soundButton.transform, false);
        RectTransform soundIconRect = soundIconObj.AddComponent<RectTransform>();
        soundIconRect.anchorMin = new Vector2(0.5f, 0.5f);
        soundIconRect.anchorMax = new Vector2(0.5f, 0.5f);
        soundIconRect.sizeDelta = new Vector2(100f, 60f);
        soundIconRect.anchoredPosition = Vector2.zero;
        soundIconObj.AddComponent<Image>().raycastTarget = false;
        LayoutElement soundIconLayout = soundIconObj.AddComponent<LayoutElement>();
        soundIconLayout.ignoreLayout = true;

        // SoundButtonにホバーエフェクトを追加（blinkTargetはSoundBg）
        var soundHover = soundButton.gameObject.AddComponent<ButtonHoverEffect>();
        var soundBgImage = soundButton.transform.Find("SoundBg")?.GetComponent<Image>();
        if (soundBgImage != null)
        {
            var soSound = new UnityEditor.SerializedObject(soundHover);
            soSound.FindProperty("blinkTarget").objectReferenceValue = soundBgImage;
            soSound.ApplyModifiedProperties();
        }

        helpButton = CreateButton(mainObj.transform, "HelpButton", "HELP", 60f, createBg: true);

        // HelpButton配下にHelpIconを追加（HelpBgの上に重ねる）
        GameObject helpIconObj = new GameObject("HelpIcon");
        helpIconObj.transform.SetParent(helpButton.transform, false);
        RectTransform helpIconRect = helpIconObj.AddComponent<RectTransform>();
        helpIconRect.anchorMin = new Vector2(0.5f, 0.5f);
        helpIconRect.anchorMax = new Vector2(0.5f, 0.5f);
        helpIconRect.sizeDelta = new Vector2(100f, 60f);
        helpIconRect.anchoredPosition = Vector2.zero;
        helpIconObj.AddComponent<Image>().raycastTarget = false;
        LayoutElement helpIconLayout = helpIconObj.AddComponent<LayoutElement>();
        helpIconLayout.ignoreLayout = true;

        // HelpButtonにホバーエフェクトを追加（blinkTargetはHelpBg）
        var helpHover = helpButton.gameObject.AddComponent<ButtonHoverEffect>();
        var helpBgImage = helpButton.transform.Find("HelpBg")?.GetComponent<Image>();
        if (helpBgImage != null)
        {
            var soHelp = new UnityEditor.SerializedObject(helpHover);
            soHelp.FindProperty("blinkTarget").objectReferenceValue = helpBgImage;
            soHelp.ApplyModifiedProperties();
        }

        mainPanel = mainObj;
        mainObj.SetActive(false);

        if (showDebugLog) Debug.Log("[PauseMenuUI] Main panel created");
    }

    private void CreateConfirmPanel(Transform parent)
    {
        GameObject confirmObj = new GameObject("ConfirmPanel");
        confirmObj.transform.SetParent(parent, false);

        RectTransform confirmRect = confirmObj.AddComponent<RectTransform>();
        confirmRect.anchorMin = new Vector2(0.5f, 0.5f);
        confirmRect.anchorMax = new Vector2(0.5f, 0.5f);
        confirmRect.sizeDelta = new Vector2(500f, 300f);
        confirmRect.anchoredPosition = new Vector2(140f, 0f); // SkillHUD(280px)を除外したエリアの中央

        // 背景画像用の子オブジェクト（ConfirmBg）を作成
        GameObject confirmBgObj = new GameObject("ConfirmBg");
        confirmBgObj.transform.SetParent(confirmObj.transform, false);
        RectTransform confirmBgRect = confirmBgObj.AddComponent<RectTransform>();
        confirmBgRect.anchorMin = Vector2.zero;
        confirmBgRect.anchorMax = Vector2.one;
        confirmBgRect.sizeDelta = Vector2.zero;
        confirmBgRect.anchoredPosition = Vector2.zero;
        Image confirmBg = confirmBgObj.AddComponent<Image>();
        confirmBg.color = new Color(0.1f, 0.1f, 0.1f, 0.95f);
        LayoutElement confirmBgLayout = confirmBgObj.AddComponent<LayoutElement>();
        confirmBgLayout.ignoreLayout = true;

        VerticalLayoutGroup layout = confirmObj.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 30f;
        layout.padding = new RectOffset(40, 40, 40, 40);
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        TextMeshProUGUI confirmText = CreateText(confirmObj.transform, "ConfirmText", "エリアセレクトに戻りますか？", 32, TextAlignmentOptions.Center, 100f);
        RectTransform confirmTextRect = confirmText.GetComponent<RectTransform>();
        confirmTextRect.anchorMin = new Vector2(0.5f, 0.5f);
        confirmTextRect.anchorMax = new Vector2(0.5f, 0.5f);
        confirmTextRect.anchoredPosition = new Vector2(0f, 60f);
        confirmText.GetComponent<LayoutElement>().ignoreLayout = true;

        // Yes/Noボタンを横並びにするためのコンテナを作成
        GameObject buttonContainer = new GameObject("ButtonContainer");
        buttonContainer.transform.SetParent(confirmObj.transform, false);

        RectTransform buttonContainerRect = buttonContainer.AddComponent<RectTransform>();
        buttonContainerRect.anchorMin = new Vector2(0.5f, 0.5f);
        buttonContainerRect.anchorMax = new Vector2(0.5f, 0.5f);
        buttonContainerRect.sizeDelta = new Vector2(400f, 80f);
        buttonContainerRect.anchoredPosition = new Vector2(0f, -60f);
        LayoutElement buttonContainerLayout = buttonContainer.AddComponent<LayoutElement>();
        buttonContainerLayout.ignoreLayout = true;

        HorizontalLayoutGroup hLayout = buttonContainer.AddComponent<HorizontalLayoutGroup>();
        hLayout.spacing = 40f;
        hLayout.childAlignment = TextAnchor.MiddleCenter;
        hLayout.childControlWidth = true;
        hLayout.childControlHeight = true;
        hLayout.childForceExpandWidth = false;
        hLayout.childForceExpandHeight = false;

        confirmYesButton = CreateButton(buttonContainer.transform, "YesButton", "YES", 80f, 160f, createBg: true);
        confirmNoButton = CreateButton(buttonContainer.transform, "NoButton", "NO", 80f, 160f, createBg: true);

        confirmPanel = confirmObj;
        confirmObj.SetActive(false);

        if (showDebugLog) Debug.Log("[PauseMenuUI] Confirm panel created");
    }

    private void CreateSoundPanel(Transform parent)
    {
        GameObject soundObj = new GameObject("SoundPanel");
        soundObj.transform.SetParent(parent, false);

        RectTransform soundRect = soundObj.AddComponent<RectTransform>();
        soundRect.anchorMin = new Vector2(0.5f, 0.5f);
        soundRect.anchorMax = new Vector2(0.5f, 0.5f);
        soundRect.sizeDelta = new Vector2(500f, 400f);
        soundRect.anchoredPosition = new Vector2(140f, 0f); // SkillHUD(280px)を除外したエリアの中央

        // 背景画像用の子オブジェクト（SoundBg）を作成
        GameObject soundBgObj = new GameObject("SoundBg");
        soundBgObj.transform.SetParent(soundObj.transform, false);
        RectTransform soundBgRect = soundBgObj.AddComponent<RectTransform>();
        soundBgRect.anchorMin = Vector2.zero;
        soundBgRect.anchorMax = Vector2.one;
        soundBgRect.sizeDelta = Vector2.zero;
        soundBgRect.anchoredPosition = Vector2.zero;
        Image soundBg = soundBgObj.AddComponent<Image>();
        soundBg.color = new Color(0.1f, 0.1f, 0.1f, 0.95f);
        LayoutElement soundBgLayout = soundBgObj.AddComponent<LayoutElement>();
        soundBgLayout.ignoreLayout = true;

        VerticalLayoutGroup layout = soundObj.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 30f;
        layout.padding = new RectOffset(40, 40, 40, 40);
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        CreateText(soundObj.transform, "TitleText", "サウンド設定", 36, TextAlignmentOptions.Center, 60f).fontStyle = FontStyles.Bold;

        // BGMスライダー
        bgmVolumeText = CreateText(soundObj.transform, "BGMVolumeText", "BGM: 100%", 28, TextAlignmentOptions.Center, 40f);
        bgmVolumeSlider = CreateSlider(soundObj.transform, "BGMSlider");

        // SEスライダー
        seVolumeText = CreateText(soundObj.transform, "SEVolumeText", "SE: 100%", 28, TextAlignmentOptions.Center, 40f);
        seVolumeSlider = CreateSlider(soundObj.transform, "SESlider");

        // 戻るボタン
        soundBackButton = CreateButton(soundObj.transform, "BackButton", "BACK", 60f, createBg: true);

        // BackButton配下にBackIconを追加（BackBgの上に重ねる）
        GameObject backIconObj = new GameObject("BackIcon");
        backIconObj.transform.SetParent(soundBackButton.transform, false);
        RectTransform backIconRect = backIconObj.AddComponent<RectTransform>();
        backIconRect.anchorMin = new Vector2(0.5f, 0.5f);
        backIconRect.anchorMax = new Vector2(0.5f, 0.5f);
        backIconRect.sizeDelta = new Vector2(100f, 60f);
        backIconRect.anchoredPosition = Vector2.zero;
        backIconObj.AddComponent<Image>().raycastTarget = false;
        LayoutElement backIconLayout = backIconObj.AddComponent<LayoutElement>();
        backIconLayout.ignoreLayout = true;

        soundPanel = soundObj;
        soundObj.SetActive(false);

        if (showDebugLog) Debug.Log("[PauseMenuUI] Sound panel created");
    }

    private void CreateInputPanel(Transform parent)
    {
        GameObject inputObj = new GameObject("InputPanel");
        inputObj.transform.SetParent(parent, false);

        RectTransform inputRect = inputObj.AddComponent<RectTransform>();
        inputRect.anchorMin = new Vector2(0.5f, 0.5f);
        inputRect.anchorMax = new Vector2(0.5f, 0.5f);
        inputRect.sizeDelta = new Vector2(500f, 300f);
        inputRect.anchoredPosition = new Vector2(140f, 0f); // SkillHUD(280px)を除外したエリアの中央

        // 背景画像用の子オブジェクト（InputBg）を作成
        GameObject inputBgObj = new GameObject("InputBg");
        inputBgObj.transform.SetParent(inputObj.transform, false);
        RectTransform inputBgRect = inputBgObj.AddComponent<RectTransform>();
        inputBgRect.anchorMin = Vector2.zero;
        inputBgRect.anchorMax = Vector2.one;
        inputBgRect.sizeDelta = Vector2.zero;
        inputBgRect.anchoredPosition = Vector2.zero;
        Image inputBg = inputBgObj.AddComponent<Image>();
        inputBg.color = new Color(0.1f, 0.1f, 0.1f, 0.95f);
        LayoutElement inputBgLayout = inputBgObj.AddComponent<LayoutElement>();
        inputBgLayout.ignoreLayout = true;

        VerticalLayoutGroup layout = inputObj.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 30f;
        layout.padding = new RectOffset(40, 40, 40, 40);
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        CreateText(inputObj.transform, "TitleText", "スローモーション操作設定", 36, TextAlignmentOptions.Center, 60f).fontStyle = FontStyles.Bold;

        // ホールドモードトグル行
        holdModeToggle = CreateToggleRow(inputObj.transform, "HoldModeToggle", "ホールド操作", 50f);

        // 戻るボタン
        inputBackButton = CreateButton(inputObj.transform, "BackButton", "BACK", 60f, createBg: true);

        // BackButton配下にBackIconを追加（BackBgの上に重ねる）
        GameObject inputBackIconObj = new GameObject("BackIcon");
        inputBackIconObj.transform.SetParent(inputBackButton.transform, false);
        RectTransform inputBackIconRect = inputBackIconObj.AddComponent<RectTransform>();
        inputBackIconRect.anchorMin = new Vector2(0.5f, 0.5f);
        inputBackIconRect.anchorMax = new Vector2(0.5f, 0.5f);
        inputBackIconRect.sizeDelta = new Vector2(100f, 60f);
        inputBackIconRect.anchoredPosition = Vector2.zero;
        inputBackIconObj.AddComponent<Image>().raycastTarget = false;
        LayoutElement inputBackIconLayout = inputBackIconObj.AddComponent<LayoutElement>();
        inputBackIconLayout.ignoreLayout = true;

        inputPanel = inputObj;
        inputObj.SetActive(false);

        if (showDebugLog) Debug.Log("[PauseMenuUI] Input panel created");
    }

    private void CreateHelpPanel(Transform parent)
    {
        GameObject helpObj = new GameObject("HelpPanel");
        helpObj.transform.SetParent(parent, false);

        RectTransform helpRect = helpObj.AddComponent<RectTransform>();
        helpRect.anchorMin = new Vector2(0.5f, 0.5f);
        helpRect.anchorMax = new Vector2(0.5f, 0.5f);
        helpRect.sizeDelta = new Vector2(600f, 500f);
        helpRect.anchoredPosition = new Vector2(140f, 0f); // SkillHUD(280px)を除外したエリアの中央

        // 背景画像用の子オブジェクト（HelpBg）を作成
        GameObject helpBgObj = new GameObject("HelpBg");
        helpBgObj.transform.SetParent(helpObj.transform, false);
        RectTransform helpBgRect = helpBgObj.AddComponent<RectTransform>();
        helpBgRect.anchorMin = Vector2.zero;
        helpBgRect.anchorMax = Vector2.one;
        helpBgRect.sizeDelta = Vector2.zero;
        helpBgRect.anchoredPosition = Vector2.zero;
        Image helpBg = helpBgObj.AddComponent<Image>();
        helpBg.color = new Color(0.1f, 0.1f, 0.1f, 0.95f);
        LayoutElement helpBgLayout = helpBgObj.AddComponent<LayoutElement>();
        helpBgLayout.ignoreLayout = true;

        VerticalLayoutGroup layout = helpObj.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 15f;
        // ★タイトルを上端・戻るボタンを下端に固定し、その間の余白を全てスクロール領域に割り当てる
        layout.padding = new RectOffset(40, 40, 15, 15);
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        CreateText(helpObj.transform, "TitleText", "ヘルプ", 36, TextAlignmentOptions.Center, 60f).fontStyle = FontStyles.Bold;

        // ヘルプテキスト（操作説明）：フォントサイズは固定のまま、
        // 本文が枠に収まらない分はドラッグで下にスライドして続きを読めるようにする
        helpText = CreateScrollableHelpText(helpObj.transform, 1, 360f);

        // 戻るボタン
        helpBackButton = CreateButton(helpObj.transform, "BackButton", "BACK", 60f, createBg: true);

        // BackButton配下にBackIconを追加（BackBgの上に重ねる）
        GameObject helpBackIconObj = new GameObject("BackIcon");
        helpBackIconObj.transform.SetParent(helpBackButton.transform, false);
        RectTransform helpBackIconRect = helpBackIconObj.AddComponent<RectTransform>();
        helpBackIconRect.anchorMin = new Vector2(0.5f, 0.5f);
        helpBackIconRect.anchorMax = new Vector2(0.5f, 0.5f);
        helpBackIconRect.sizeDelta = new Vector2(100f, 60f);
        helpBackIconRect.anchoredPosition = Vector2.zero;
        helpBackIconObj.AddComponent<Image>().raycastTarget = false;
        LayoutElement helpBackIconLayout = helpBackIconObj.AddComponent<LayoutElement>();
        helpBackIconLayout.ignoreLayout = true;

        helpPanel = helpObj;
        helpObj.SetActive(false);

        if (showDebugLog) Debug.Log("[PauseMenuUI] Help panel created");
    }

    /// <summary>
    /// ヘルプ本文用のスクロール可能なテキスト表示を作る。表示枠サイズ・フォントサイズは変えず、
    /// 本文が枠に収まらない分をドラッグで下にスライドして見られるようにする（CreateTextは他の固定ラベルと共用のため触らない）
    /// </summary>
    private TextMeshProUGUI CreateScrollableHelpText(Transform parent, int siblingIndex, float height = 360f)
    {
        // ScrollView本体（表示枠）
        GameObject scrollViewObj = new GameObject("HelpScrollView");
        scrollViewObj.transform.SetParent(parent, false);
        scrollViewObj.transform.SetSiblingIndex(siblingIndex);

        RectTransform scrollViewRect = scrollViewObj.AddComponent<RectTransform>();
        scrollViewRect.sizeDelta = new Vector2(400f, height);
        LayoutElement scrollViewLayout = scrollViewObj.AddComponent<LayoutElement>();
        scrollViewLayout.preferredHeight = height;
        // ★タイトル・戻るボタン以外の余白を全てこのスクロール領域が吸収して伸びるようにする
        // （親VerticalLayoutGroup側でchildControlHeight=trueにしておく必要がある）
        scrollViewLayout.flexibleHeight = 1f;

        ScrollRect scrollRect = scrollViewObj.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 20f;

        // Viewport（表示枠外を隠すマスク）
        // ★RectMask2Dだけではドラッグ入力を受け取れない（Raycast対象のGraphicが無いとEventSystemに拾われない）ため、
        // 透明なImageをraycastTarget=trueで追加し、ドラッグでスクロールできるようにする
        GameObject viewportObj = new GameObject("Viewport");
        viewportObj.transform.SetParent(scrollViewObj.transform, false);
        RectTransform viewportRect = viewportObj.AddComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.sizeDelta = Vector2.zero;
        viewportRect.pivot = new Vector2(0.5f, 1f);
        viewportObj.AddComponent<RectMask2D>();
        Image viewportImage = viewportObj.AddComponent<Image>();
        viewportImage.color = new Color(0f, 0f, 0f, 0f);
        viewportImage.raycastTarget = true;

        // Content（実際の本文の高さぶん伸びるコンテナ）
        GameObject contentObj = new GameObject("Content");
        contentObj.transform.SetParent(viewportObj.transform, false);
        RectTransform contentRect = contentObj.AddComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.sizeDelta = Vector2.zero;
        contentRect.anchoredPosition = Vector2.zero;

        VerticalLayoutGroup contentLayout = contentObj.AddComponent<VerticalLayoutGroup>();
        contentLayout.childControlWidth = true;
        contentLayout.childControlHeight = true;
        contentLayout.childForceExpandWidth = true;
        contentLayout.childForceExpandHeight = false;
        // ★最後の行が戻るボタンと被って見えなくなる事故防止に、末尾へ余白を確保して最後まで確実にスクロールできるようにする
        contentLayout.padding = new RectOffset(0, 0, 0, 150);

        ContentSizeFitter contentFitter = contentObj.AddComponent<ContentSizeFitter>();
        contentFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // HelpText本体（フォントサイズ24はそのまま。折り返し幅はContentの幅に追従する）
        GameObject helpTextObj = new GameObject("HelpText");
        helpTextObj.transform.SetParent(contentObj.transform, false);
        RectTransform helpTextRect = helpTextObj.AddComponent<RectTransform>();
        helpTextRect.anchorMin = new Vector2(0f, 1f);
        helpTextRect.anchorMax = new Vector2(1f, 1f);
        helpTextRect.pivot = new Vector2(0.5f, 1f);

        TextMeshProUGUI tmp = helpTextObj.AddComponent<TextMeshProUGUI>();
        tmp.text = HelpTextContent;
        tmp.fontSize = 24;
        tmp.alignment = TextAlignmentOptions.TopLeft;
        tmp.color = Color.white;
        tmp.raycastTarget = false;
        tmp.enableWordWrapping = true;

        scrollRect.viewport = viewportRect;
        scrollRect.content = contentRect;

        return tmp;
    }

    private TextMeshProUGUI CreateText(Transform parent, string name, string text, int fontSize, TextAlignmentOptions alignment, float height)
    {
        GameObject textObj = new GameObject(name);
        textObj.transform.SetParent(parent, false);

        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.sizeDelta = new Vector2(400f, height);

        TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.alignment = alignment;
        tmp.color = Color.white;
        tmp.raycastTarget = false; // ラベルなのでクリック不要

        LayoutElement layoutElement = textObj.AddComponent<LayoutElement>();
        layoutElement.preferredHeight = height;

        return tmp;
    }

    private Button CreateButton(Transform parent, string name, string text, float height, float width = 200f, bool createBg = false)
    {
        GameObject btnObj = new GameObject(name);
        btnObj.transform.SetParent(parent, false);

        RectTransform btnRect = btnObj.AddComponent<RectTransform>();
        btnRect.sizeDelta = new Vector2(width, height);

        // 背景画像用の子オブジェクトを作成（createBg=trueの場合）
        if (createBg)
        {
            string bgName = name.Replace("Button", "Bg");
            GameObject bgObj = new GameObject(bgName);
            bgObj.transform.SetParent(btnObj.transform, false);
            RectTransform bgRect = bgObj.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;
            bgRect.anchoredPosition = Vector2.zero;
            Image bgImage = bgObj.AddComponent<Image>();
            bgImage.color = new Color(0.3f, 0.3f, 0.3f, 1f);
            bgImage.raycastTarget = false; // ビジュアルのみ、クリック不要
            LayoutElement bgLayout = bgObj.AddComponent<LayoutElement>();
            bgLayout.ignoreLayout = true;
        }

        Image btnImage = btnObj.AddComponent<Image>();
        btnImage.color = Color.clear; // 本体のImageは透明（背景はBgオブジェクトで管理）

        Button btn = btnObj.AddComponent<Button>();

        // ボタンテキスト
        GameObject btnTextObj = new GameObject("Text");
        btnTextObj.transform.SetParent(btnObj.transform, false);

        RectTransform btnTextRect = btnTextObj.AddComponent<RectTransform>();
        btnTextRect.anchorMin = Vector2.zero;
        btnTextRect.anchorMax = Vector2.one;
        btnTextRect.sizeDelta = Vector2.zero;
        btnTextRect.anchoredPosition = Vector2.zero;

        TextMeshProUGUI btnText = btnTextObj.AddComponent<TextMeshProUGUI>();
        btnText.text = text;
        btnText.fontSize = 28;
        btnText.alignment = TextAlignmentOptions.Center;
        btnText.color = Color.white;

        LayoutElement layoutElement = btnObj.AddComponent<LayoutElement>();
        layoutElement.preferredWidth = width;
        layoutElement.preferredHeight = height;

        return btn;
    }

    private UnityEngine.UI.Toggle CreateToggleRow(Transform parent, string name, string labelText, float height)
    {
        // 横並びコンテナ
        GameObject rowObj = new GameObject(name);
        rowObj.transform.SetParent(parent, false);

        RectTransform rowRect = rowObj.AddComponent<RectTransform>();
        rowRect.sizeDelta = new Vector2(400f, height);

        HorizontalLayoutGroup hLayout = rowObj.AddComponent<HorizontalLayoutGroup>();
        hLayout.spacing = 20f;
        hLayout.childAlignment = TextAnchor.MiddleCenter;
        hLayout.childControlWidth = false;
        hLayout.childControlHeight = true;
        hLayout.childForceExpandWidth = false;
        hLayout.childForceExpandHeight = true;

        LayoutElement rowLayout = rowObj.AddComponent<LayoutElement>();
        rowLayout.preferredHeight = height;

        // ラベル
        GameObject labelObj = new GameObject("Label");
        labelObj.transform.SetParent(rowObj.transform, false);

        RectTransform labelRect = labelObj.AddComponent<RectTransform>();
        labelRect.sizeDelta = new Vector2(250f, height);

        TextMeshProUGUI labelTmp = labelObj.AddComponent<TextMeshProUGUI>();
        labelTmp.text = labelText;
        labelTmp.fontSize = 24;
        labelTmp.alignment = TextAlignmentOptions.MidlineLeft;
        labelTmp.color = Color.white;

        // トグル本体
        GameObject toggleObj = new GameObject("Toggle");
        toggleObj.transform.SetParent(rowObj.transform, false);

        RectTransform toggleRect = toggleObj.AddComponent<RectTransform>();
        toggleRect.sizeDelta = new Vector2(50f, 50f);

        UnityEngine.UI.Toggle toggle = toggleObj.AddComponent<UnityEngine.UI.Toggle>();

        // 背景
        GameObject bgObj = new GameObject("Background");
        bgObj.transform.SetParent(toggleObj.transform, false);

        RectTransform bgRect = bgObj.AddComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;

        Image bgImage = bgObj.AddComponent<Image>();
        bgImage.color = new Color(0.3f, 0.3f, 0.3f, 1f);
        toggle.targetGraphic = bgImage;

        // チェックマーク（ON時の色）
        GameObject checkObj = new GameObject("Checkmark");
        checkObj.transform.SetParent(bgObj.transform, false);

        RectTransform checkRect = checkObj.AddComponent<RectTransform>();
        checkRect.anchorMin = new Vector2(0.1f, 0.1f);
        checkRect.anchorMax = new Vector2(0.9f, 0.9f);
        checkRect.sizeDelta = Vector2.zero;

        Image checkImage = checkObj.AddComponent<Image>();
        checkImage.color = new Color(0.3f, 0.7f, 1f, 1f);
        toggle.graphic = checkImage;

        return toggle;
    }

    private Slider CreateSlider(Transform parent, string name)
    {
        GameObject sliderObj = new GameObject(name);
        sliderObj.transform.SetParent(parent, false);

        RectTransform sliderRect = sliderObj.AddComponent<RectTransform>();
        sliderRect.sizeDelta = new Vector2(400f, 30f);

        Slider slider = sliderObj.AddComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 1f;

        // Background
        GameObject bgObj = new GameObject("Background");
        bgObj.transform.SetParent(sliderObj.transform, false);

        RectTransform bgRect = bgObj.AddComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;

        Image bgImage = bgObj.AddComponent<Image>();
        bgImage.color = new Color(0.2f, 0.2f, 0.2f, 1f);

        // Fill Area
        GameObject fillAreaObj = new GameObject("Fill Area");
        fillAreaObj.transform.SetParent(sliderObj.transform, false);

        RectTransform fillAreaRect = fillAreaObj.AddComponent<RectTransform>();
        fillAreaRect.anchorMin = Vector2.zero;
        fillAreaRect.anchorMax = Vector2.one;
        fillAreaRect.sizeDelta = Vector2.zero;

        // Fill
        GameObject fillObj = new GameObject("Fill");
        fillObj.transform.SetParent(fillAreaObj.transform, false);

        RectTransform fillRect = fillObj.AddComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.sizeDelta = Vector2.zero;

        Image fillImage = fillObj.AddComponent<Image>();
        fillImage.color = new Color(0.3f, 0.7f, 1f, 1f);

        slider.fillRect = fillRect;

        // Handle Slide Area
        GameObject handleAreaObj = new GameObject("Handle Slide Area");
        handleAreaObj.transform.SetParent(sliderObj.transform, false);

        RectTransform handleAreaRect = handleAreaObj.AddComponent<RectTransform>();
        handleAreaRect.anchorMin = Vector2.zero;
        handleAreaRect.anchorMax = Vector2.one;
        handleAreaRect.sizeDelta = Vector2.zero;

        // Handle
        GameObject handleObj = new GameObject("Handle");
        handleObj.transform.SetParent(handleAreaObj.transform, false);

        RectTransform handleRect = handleObj.AddComponent<RectTransform>();
        handleRect.sizeDelta = new Vector2(20f, 30f);

        Image handleImage = handleObj.AddComponent<Image>();
        handleImage.color = Color.white;

        slider.handleRect = handleRect;

        LayoutElement layoutElement = sliderObj.AddComponent<LayoutElement>();
        layoutElement.preferredHeight = 30f;

        return slider;
    }
#endif
}
