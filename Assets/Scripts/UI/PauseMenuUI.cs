using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// ポーズメニューUIの管理
/// 4つのパネル（Main/Confirm/Sound/Help）を切り替え
/// Play前のInspectorで全て調整可能
/// </summary>
public class PauseMenuUI : MonoBehaviour
{
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
        Debug.Log("[PauseMenuUI] OnPauseStarted called. Showing main panel...");
        ShowMainPanel();
    }

    /// <summary>
    /// ポーズ解除時の処理
    /// </summary>
    private void OnPauseEnded()
    {
        Debug.Log("[PauseMenuUI] OnPauseEnded called. Hiding all panels...");
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

        Debug.Log($"[PauseMenuUI] Panels hidden - dimPanel:{dimPanel != null}, mainPanel:{mainPanel != null}");
    }

    /// <summary>
    /// メインパネルを表示
    /// </summary>
    private void ShowMainPanel()
    {
        Debug.Log($"[PauseMenuUI] ShowMainPanel called - dimPanel:{dimPanel != null}, mainPanel:{mainPanel != null}");

        HideAllPanels();
        if (dimPanel != null) dimPanel.SetActive(true);
        if (mainPanel != null) mainPanel.SetActive(true);

        Debug.Log("[PauseMenuUI] Main panel should be visible now");
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
            Debug.Log("[PauseMenuUI] Calling BackToAreaSelect()...");
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

        inputButton = CreateButton(mainPanel.transform, "InputButton", "INPUT", 60f);
        inputButton.transform.SetSiblingIndex(insertIndex);

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

        Debug.Log("[PauseMenuUI] InputPanel と InputButton を追加しました。");
    }

    /// <summary>
    /// Editor拡張：ポーズメニューUIを自動生成
    /// </summary>
    [ContextMenu("Setup Pause Menu UI")]
    private void SetupPauseMenuUI()
    {
        Debug.Log("[PauseMenuUI] Setting up Pause Menu UI...");

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

        Debug.Log("[PauseMenuUI] Pause Menu UI setup complete!");

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

        Debug.Log("[PauseMenuUI] All references set via SerializedObject");
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

        Debug.Log("[PauseMenuUI] Dim panel created");
    }

    private void CreateMainPanel(Transform parent)
    {
        GameObject mainObj = new GameObject("MainPanel");
        mainObj.transform.SetParent(parent, false);

        RectTransform mainRect = mainObj.AddComponent<RectTransform>();
        mainRect.anchorMin = new Vector2(0.5f, 0.5f);
        mainRect.anchorMax = new Vector2(0.5f, 0.5f);
        mainRect.sizeDelta = new Vector2(400f, 500f);
        mainRect.anchoredPosition = Vector2.zero;

        Image mainBg = mainObj.AddComponent<Image>();
        mainBg.color = new Color(0.1f, 0.1f, 0.1f, 0.95f);

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
        CreateText(mainObj.transform, "TitleText", "PAUSED", 48, TextAlignmentOptions.Center, 80f);

        // ボタンを作成
        resumeButton = CreateButton(mainObj.transform, "ResumeButton", "RESUME", 60f);
        retireButton = CreateButton(mainObj.transform, "RetireButton", "RETIRE", 60f);
        soundButton = CreateButton(mainObj.transform, "SoundButton", "SOUND", 60f);
        helpButton = CreateButton(mainObj.transform, "HelpButton", "HELP", 60f);

        mainPanel = mainObj;
        mainObj.SetActive(false);

        Debug.Log("[PauseMenuUI] Main panel created");
    }

    private void CreateConfirmPanel(Transform parent)
    {
        GameObject confirmObj = new GameObject("ConfirmPanel");
        confirmObj.transform.SetParent(parent, false);

        RectTransform confirmRect = confirmObj.AddComponent<RectTransform>();
        confirmRect.anchorMin = new Vector2(0.5f, 0.5f);
        confirmRect.anchorMax = new Vector2(0.5f, 0.5f);
        confirmRect.sizeDelta = new Vector2(500f, 300f);
        confirmRect.anchoredPosition = Vector2.zero;

        Image confirmBg = confirmObj.AddComponent<Image>();
        confirmBg.color = new Color(0.1f, 0.1f, 0.1f, 0.95f);

        VerticalLayoutGroup layout = confirmObj.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 30f;
        layout.padding = new RectOffset(40, 40, 40, 40);
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        CreateText(confirmObj.transform, "ConfirmText", "Return to Area Select?", 32, TextAlignmentOptions.Center, 100f);

        // Yes/Noボタンを横並びにするためのコンテナを作成
        GameObject buttonContainer = new GameObject("ButtonContainer");
        buttonContainer.transform.SetParent(confirmObj.transform, false);

        RectTransform buttonContainerRect = buttonContainer.AddComponent<RectTransform>();
        buttonContainerRect.sizeDelta = new Vector2(400f, 80f);

        HorizontalLayoutGroup hLayout = buttonContainer.AddComponent<HorizontalLayoutGroup>();
        hLayout.spacing = 40f;
        hLayout.childAlignment = TextAnchor.MiddleCenter;
        hLayout.childControlWidth = true;
        hLayout.childControlHeight = true;
        hLayout.childForceExpandWidth = true;
        hLayout.childForceExpandHeight = true;

        confirmYesButton = CreateButton(buttonContainer.transform, "YesButton", "YES", 80f);
        confirmNoButton = CreateButton(buttonContainer.transform, "NoButton", "NO", 80f);

        confirmPanel = confirmObj;
        confirmObj.SetActive(false);

        Debug.Log("[PauseMenuUI] Confirm panel created");
    }

    private void CreateSoundPanel(Transform parent)
    {
        GameObject soundObj = new GameObject("SoundPanel");
        soundObj.transform.SetParent(parent, false);

        RectTransform soundRect = soundObj.AddComponent<RectTransform>();
        soundRect.anchorMin = new Vector2(0.5f, 0.5f);
        soundRect.anchorMax = new Vector2(0.5f, 0.5f);
        soundRect.sizeDelta = new Vector2(500f, 400f);
        soundRect.anchoredPosition = Vector2.zero;

        Image soundBg = soundObj.AddComponent<Image>();
        soundBg.color = new Color(0.1f, 0.1f, 0.1f, 0.95f);

        VerticalLayoutGroup layout = soundObj.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 30f;
        layout.padding = new RectOffset(40, 40, 40, 40);
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        CreateText(soundObj.transform, "TitleText", "SOUND SETTINGS", 36, TextAlignmentOptions.Center, 60f);

        // BGMスライダー
        bgmVolumeText = CreateText(soundObj.transform, "BGMVolumeText", "BGM: 100%", 28, TextAlignmentOptions.Center, 40f);
        bgmVolumeSlider = CreateSlider(soundObj.transform, "BGMSlider");

        // SEスライダー
        seVolumeText = CreateText(soundObj.transform, "SEVolumeText", "SE: 100%", 28, TextAlignmentOptions.Center, 40f);
        seVolumeSlider = CreateSlider(soundObj.transform, "SESlider");

        // 戻るボタン
        soundBackButton = CreateButton(soundObj.transform, "BackButton", "BACK", 60f);

        soundPanel = soundObj;
        soundObj.SetActive(false);

        Debug.Log("[PauseMenuUI] Sound panel created");
    }

    private void CreateInputPanel(Transform parent)
    {
        GameObject inputObj = new GameObject("InputPanel");
        inputObj.transform.SetParent(parent, false);

        RectTransform inputRect = inputObj.AddComponent<RectTransform>();
        inputRect.anchorMin = new Vector2(0.5f, 0.5f);
        inputRect.anchorMax = new Vector2(0.5f, 0.5f);
        inputRect.sizeDelta = new Vector2(500f, 300f);
        inputRect.anchoredPosition = Vector2.zero;

        Image inputBg = inputObj.AddComponent<Image>();
        inputBg.color = new Color(0.1f, 0.1f, 0.1f, 0.95f);

        VerticalLayoutGroup layout = inputObj.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 30f;
        layout.padding = new RectOffset(40, 40, 40, 40);
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        CreateText(inputObj.transform, "TitleText", "INPUT SETTINGS", 36, TextAlignmentOptions.Center, 60f);

        // ホールドモードトグル行
        holdModeToggle = CreateToggleRow(inputObj.transform, "HoldModeToggle", "ホールド操作", 50f);

        // 戻るボタン
        inputBackButton = CreateButton(inputObj.transform, "BackButton", "BACK", 60f);

        inputPanel = inputObj;
        inputObj.SetActive(false);

        Debug.Log("[PauseMenuUI] Input panel created");
    }

    private void CreateHelpPanel(Transform parent)
    {
        GameObject helpObj = new GameObject("HelpPanel");
        helpObj.transform.SetParent(parent, false);

        RectTransform helpRect = helpObj.AddComponent<RectTransform>();
        helpRect.anchorMin = new Vector2(0.5f, 0.5f);
        helpRect.anchorMax = new Vector2(0.5f, 0.5f);
        helpRect.sizeDelta = new Vector2(600f, 500f);
        helpRect.anchoredPosition = Vector2.zero;

        Image helpBg = helpObj.AddComponent<Image>();
        helpBg.color = new Color(0.1f, 0.1f, 0.1f, 0.95f);

        VerticalLayoutGroup layout = helpObj.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 20f;
        layout.padding = new RectOffset(40, 40, 40, 40);
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        CreateText(helpObj.transform, "TitleText", "HELP", 36, TextAlignmentOptions.Center, 60f);

        // ヘルプテキスト（操作説明）
        helpText = CreateText(helpObj.transform, "HelpText",
            "Game Controls:\n\n" +
            "• Draw lines to reflect bullets\n" +
            "• Draw circles to rescue player\n" +
            "• ESC: Pause/Resume\n\n" +
            "(More details coming soon...)",
            24, TextAlignmentOptions.Left, 300f);

        // 戻るボタン
        helpBackButton = CreateButton(helpObj.transform, "BackButton", "BACK", 60f);

        helpPanel = helpObj;
        helpObj.SetActive(false);

        Debug.Log("[PauseMenuUI] Help panel created");
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

        LayoutElement layoutElement = textObj.AddComponent<LayoutElement>();
        layoutElement.preferredHeight = height;

        return tmp;
    }

    private Button CreateButton(Transform parent, string name, string text, float height)
    {
        GameObject btnObj = new GameObject(name);
        btnObj.transform.SetParent(parent, false);

        RectTransform btnRect = btnObj.AddComponent<RectTransform>();
        btnRect.sizeDelta = new Vector2(200f, height);

        Image btnImage = btnObj.AddComponent<Image>();
        btnImage.color = new Color(0.3f, 0.3f, 0.3f, 1f);

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
