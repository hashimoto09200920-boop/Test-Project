using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using Game.Progress;

/// <summary>
/// エリア選択画面の管理
/// プレイヤーがエリアを選択してゲームシーンに遷移する
/// </summary>
public class AreaSelectManager : MonoBehaviour
{
    [Header("Area Configurations")]
    [Tooltip("選択可能なエリア設定のリスト")]
    [SerializeField] private AreaConfig[] availableAreas;

    [Header("Scene Settings")]
    [Tooltip("ゲームシーンの名前")]
    [SerializeField] private string gameSceneName = "05_Game";

    [Header("Sound Effects")]
    [Tooltip("ボタンクリック時の効果音")]
    [SerializeField] private AudioClip buttonClickSE;

    [Header("Background Music")]
    [Tooltip("AreaSelect画面で再生するBGM")]
    [SerializeField] private AudioClip bgm;
    [Tooltip("BGMの音量")]
    [SerializeField] [Range(0f, 1f)] private float bgmVolume = 0.2f;

    [Header("Input Block")]
    [Tooltip("シーンロード後に入力を受け付けない秒数（連打対策）")]
    [SerializeField] private float inputBlockDuration = 0.5f;

    [Header("Debug")]
    [Tooltip("ONにするとBGMを再生しない（デバッグ用）")]
    [SerializeField] private bool debugDisableBGM = false;

    [Tooltip("デバッグ用：次の未開放Areaを1つだけアンロックするボタン（Setup Debug Unlock Next Area Buttonで自動生成可能）")]
    [SerializeField] private UnityEngine.UI.Button debugUnlockNextAreaButton;

    [Tooltip("デバッグ用：Area10のRankバッジ表示を確認するため、クリックする度にランクを切り替えるボタン（Setup Debug Area10 Rank Buttonで自動生成可能）")]
    [SerializeField] private UnityEngine.UI.Button debugArea10RankButton;

    [Header("Test Area (Editor Only)")]
    [Tooltip("F1で起動するテスト用エリア。Area0Config.assetをセット。")]
    [SerializeField] private AreaConfig testAreaConfig;

    [Header("Tutorial")]
    [Tooltip("「チュートリアルを見る」ボタン（Setup View Tutorial Buttonで自動生成可能）")]
    [SerializeField] private UnityEngine.UI.Button viewTutorialButton;

    [Header("Gem Lifecycle (使用回数システム)")]
    [Tooltip("ジェム消滅通知・出撃前確認ダイアログ")]
    [SerializeField] private Game.UI.GemLifecycleUI gemLifecycleUI;

    private AudioSource audioSource;
    private AudioSource bgmAudioSource;
    private bool isTransitioning = false;
    private static GameObject persistentBGMObject;
    private float _sceneLoadTime;

    private void Update()
    {
#if UNITY_EDITOR
        if (Input.GetKeyDown(KeyCode.F1) && testAreaConfig != null && !isTransitioning)
        {
            Debug.Log("[AreaSelectManager] F1: Launching test area");
            SelectTestArea();
        }
#endif
    }

    private void SelectTestArea()
    {
        if (testAreaConfig == null) return;
        isTransitioning = true;
        GameSession.SelectedArea = testAreaConfig;
        GameSession.RemainingLives = 3;
        GameSession.CurrentScore = 0;
        GameSession.WasExplicitlySet = true;
        GameSession.SelectedStageNumber = 1;
        StartCoroutine(LoadGameSceneWithSE());
    }

    private void Awake()
    {
        // SE用のAudioSource
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }

        // BGMはpersistentBGMObjectで管理するため、ここでは作成しない

        // シーンロード時刻を記録（入力ブロック判定用）
        _sceneLoadTime = Time.realtimeSinceStartup;

        if (viewTutorialButton != null)
        {
            viewTutorialButton.onClick.AddListener(LaunchTutorial);
        }

        if (debugUnlockNextAreaButton != null)
        {
            debugUnlockNextAreaButton.onClick.AddListener(OnClickDebugUnlockNextArea);
        }

        if (debugArea10RankButton != null)
        {
            debugArea10RankButton.onClick.AddListener(OnClickDebugArea10Rank);
        }
    }

    private void Start()
    {
        // GameSessionをリセット
        GameSession.Reset();
        // ドリンクブーストをリセット（前回ゲームの効果を消す）
        Game.Shop.DrinkSession.Reset();

        // ★ジェム使用回数システム：使用回数が0になったジェムをここで消滅させる（消滅通知の表示は不要になったため削除）
        Game.Gems.GemManager.Instance?.RemoveDepletedGemsAndGetInfo();

        // 初回起動時（チュートリアル未読了）は、AreaSelect画面を一切見せずに黒幕を敷いたまま
        // 直接チュートリアルへ遷移する（フェードインでAreaSelectが一瞬透けて見えるのを防ぐため）
        if (!TutorialProgress.HasSeenTutorial)
        {
            CreateOpaqueBlackOverlay();
            LaunchTutorialInternal(playClickSE: false);
            return;
        }

        // ★シーン開始時のフェードイン（2回目以降の通常表示）
        StartCoroutine(FadeInOnStart());

        // BGMを再生
        PlayBGM();
    }

    /// <summary>
    /// チュートリアルを05_Game上で起動する（「チュートリアルを見る」ボタンから呼ぶ想定。連打防止ガードあり）
    /// </summary>
    public void LaunchTutorial()
    {
        if (Time.realtimeSinceStartup - _sceneLoadTime < inputBlockDuration) return;
        if (isTransitioning) return;

        LaunchTutorialInternal();
    }

    /// <summary>
    /// チュートリアル起動の実処理（ガード無し）。
    /// Start()からの初回自動起動は、シーン読み込み直後で連打防止ガードに引っかかるため、
    /// ガード判定を経由せずこちらを直接呼ぶ。LaunchTutorial()（ボタン用）はガード通過後にここへ委譲する。
    /// </summary>
    private void LaunchTutorialInternal(bool playClickSE = true)
    {
        // 背景は常にArea01を使う（testAreaConfigはF1デバッグ切り替え専用のため、ここでは参照しない。
        // 参照するとF1テスト用に設定中のAreaが混入し、意図しない背景になる）
        // TutorialFlowControllerがAwake()でEnemySpawnerを無効化するため、実際に敵が湧くことはない
        AreaConfig tutorialArea = (availableAreas != null && availableAreas.Length > 0) ? availableAreas[0] : null;

        if (tutorialArea == null)
        {
            Debug.LogError("[AreaSelectManager] LaunchTutorial: 有効なAreaConfigが1つも設定されていません。");
            return;
        }

        isTransitioning = true;

        GameSession.SelectedArea = tutorialArea;
        GameSession.RemainingLives = 3;
        GameSession.CurrentScore = 0;
        GameSession.WasExplicitlySet = true;
        GameSession.SelectedStageNumber = 1;
        GameSession.StartInTutorialMode = true;

        Debug.Log("[AreaSelectManager] Launching tutorial");
        StartCoroutine(LoadGameSceneWithSE(playClickSE));
    }

    /// <summary>
    /// エリアを選択してゲームを開始
    /// </summary>
    /// <param name="areaIndex">エリアのインデックス（0始まり）</param>
    public void SelectArea(int areaIndex)
    {
        if (Time.realtimeSinceStartup - _sceneLoadTime < inputBlockDuration) return;
        // 既に遷移中なら何もしない（連打防止）
        if (isTransitioning) return;

        if (availableAreas == null || areaIndex < 0 || areaIndex >= availableAreas.Length)
        {
            Debug.LogError($"[AreaSelectManager] Invalid area index: {areaIndex}");
            return;
        }

        AreaConfig selectedArea = availableAreas[areaIndex];
        if (selectedArea == null)
        {
            Debug.LogError($"[AreaSelectManager] Area at index {areaIndex} is null!");
            return;
        }

        if (!selectedArea.IsValid())
        {
            Debug.LogError($"[AreaSelectManager] Area '{selectedArea.name}' is not valid!");
            return;
        }

        isTransitioning = true;

        // GameSessionに選択されたエリアを設定
        GameSession.SelectedArea = selectedArea;
        GameSession.RemainingLives = 3;
        GameSession.CurrentScore = 0;
        GameSession.WasExplicitlySet = true;  // AreaSelectから明示的に設定されたことをマーク

        // エリアIDを生成して ProgressManager に保存
        string areaId = $"Area_{selectedArea.areaNumber:D2}";
        if (ProgressManager.Instance != null)
        {
            ProgressManager.Instance.Data.selectedAreaId = areaId;
            ProgressManager.Instance.Save(); // 保存を忘れずに
        }

        // AreaConfig の waveStages 数から実際のステージ数を取得
        int maxStage = selectedArea.waveStages != null ? selectedArea.waveStages.Length : 3;

        // ランダムにステージ番号を選択（1 から maxStage まで）
        int selectedStage = Random.Range(1, maxStage + 1);
        GameSession.SelectedStageNumber = selectedStage;

        Debug.Log($"[AreaSelectManager] Selected: {selectedArea.GetDisplayName()}, Area ID: {areaId}, Stage: {selectedStage}/{maxStage}");

        // ゲームシーンに遷移（装備中に残り1回のジェムがあれば先に確認）
        LaunchGameSceneWithGemCheck();
    }

    /// <summary>
    /// AreaConfigを直接指定してゲームを開始
    /// </summary>
    public void SelectArea(AreaConfig area)
    {
        if (Time.realtimeSinceStartup - _sceneLoadTime < inputBlockDuration) return;
        // 既に遷移中なら何もしない（連打防止）
        if (isTransitioning) return;

        if (area == null || !area.IsValid())
        {
            Debug.LogError("[AreaSelectManager] Invalid area config!");
            return;
        }

        isTransitioning = true;

        GameSession.SelectedArea = area;
        GameSession.RemainingLives = 3;
        GameSession.CurrentScore = 0;
        GameSession.WasExplicitlySet = true;  // AreaSelectから明示的に設定されたことをマーク

        // エリアIDを生成して ProgressManager に保存
        string areaId = $"Area_{area.areaNumber:D2}";
        if (ProgressManager.Instance != null)
        {
            ProgressManager.Instance.Data.selectedAreaId = areaId;
            ProgressManager.Instance.Save(); // 保存を忘れずに
        }

        // AreaConfig の waveStages 数から実際のステージ数を取得
        int maxStage = area.waveStages != null ? area.waveStages.Length : 3;

        // ランダムにステージ番号を選択（1 から maxStage まで）
        int selectedStage = Random.Range(1, maxStage + 1);
        GameSession.SelectedStageNumber = selectedStage;

        Debug.Log($"[AreaSelectManager] Selected: {area.GetDisplayName()}, Area ID: {areaId}, Stage: {selectedStage}/{maxStage}");

        LaunchGameSceneWithGemCheck();
    }

    /// <summary>
    /// 装備中ジェムに残り使用回数1（＝このプレイで消滅する）のものがあれば先に確認ダイアログを出す。
    /// 無ければそのままゲームシーンへ遷移。
    /// </summary>
    private void LaunchGameSceneWithGemCheck()
    {
        var lastUseGems = Game.Gems.GemManager.Instance?.GetEquippedGemsAtLastUse();
        if (lastUseGems != null && lastUseGems.Count > 0 && gemLifecycleUI != null)
        {
            gemLifecycleUI.ShowPreLaunchConfirm(
                lastUseGems,
                onYes: () => StartCoroutine(LoadGameSceneWithSE()),
                onCancel: () => { isTransitioning = false; });
        }
        else
        {
            StartCoroutine(LoadGameSceneWithSE());
        }
    }

    /// <summary>
    /// SEを再生してからゲームシーンをロード
    /// </summary>
    private System.Collections.IEnumerator LoadGameSceneWithSE(bool playClickSE = true)
    {
        if (string.IsNullOrEmpty(gameSceneName))
        {
            Debug.LogError("[AreaSelectManager] Game scene name is not set!");
            yield break;
        }

        // SE再生（初回チュートリアル自動起動時は、実際のボタン押下が無いため鳴らさない）
        if (playClickSE && buttonClickSE != null && audioSource != null)
        {
            audioSource.PlayOneShot(buttonClickSE);
        }

        // SEが再生されるまでの短い待機時間
        yield return new WaitForSeconds(0.2f);

        GameSession.LogCurrentSession();

        Debug.Log($"[AreaSelectManager] Fading out and loading game scene: {gameSceneName}");

        // フェード用の黒い画像を作成
        GameObject fadeObj = new GameObject("FadeOut");
        Canvas fadeCanvas = fadeObj.AddComponent<Canvas>();
        fadeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        fadeCanvas.sortingOrder = 9999; // 最前面に表示

        UnityEngine.UI.CanvasScaler scaler = fadeObj.AddComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        GameObject imageObj = new GameObject("FadeImage");
        imageObj.transform.SetParent(fadeObj.transform, false);

        UnityEngine.UI.Image fadeImage = imageObj.AddComponent<UnityEngine.UI.Image>();
        fadeImage.color = new Color(0, 0, 0, 0); // 黒、透明から開始

        RectTransform rectTransform = imageObj.GetComponent<RectTransform>();
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.sizeDelta = Vector2.zero;

        // フェードアウト処理（0.5秒）
        float duration = 0.5f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Clamp01(elapsed / duration);
            fadeImage.color = new Color(0, 0, 0, alpha);
            yield return null;
        }

        // 完全に黒くなったらシーン遷移
        SceneManager.LoadScene(gameSceneName);
    }

    /// <summary>
    /// タイトルに戻る
    /// </summary>
    public void BackToTitle()
    {
        // 既に遷移中なら何もしない（連打防止）
        if (isTransitioning) return;

        isTransitioning = true;
        GameSession.Reset();
        StartCoroutine(FadeOutAndBackToTitle());
    }

    // =========================================================
    // Inspector / UI用の便利メソッド
    // =========================================================

    /// <summary>
    /// Area 1を選択（Buttonから呼び出し用）
    /// </summary>
    public void SelectArea1() => SelectArea(0);

    /// <summary>
    /// Area 2を選択（Buttonから呼び出し用）
    /// </summary>
    public void SelectArea2() => SelectArea(1);

    /// <summary>
    /// Area 3を選択（Buttonから呼び出し用）
    /// </summary>
    public void SelectArea3() => SelectArea(2);

    /// <summary>
    /// Area 4を選択（Buttonから呼び出し用）
    /// </summary>
    public void SelectArea4() => SelectArea(3);

    /// <summary>
    /// Area 5を選択（Buttonから呼び出し用）
    /// </summary>
    public void SelectArea5() => SelectArea(4);

    /// <summary>
    /// Area 6を選択（Buttonから呼び出し用）
    /// </summary>
    public void SelectArea6() => SelectArea(5);

    /// <summary>
    /// Area 7を選択（Buttonから呼び出し用）
    /// </summary>
    public void SelectArea7() => SelectArea(6);

    /// <summary>
    /// Area 8を選択（Buttonから呼び出し用）
    /// </summary>
    public void SelectArea8() => SelectArea(7);

    /// <summary>
    /// Area 9を選択（Buttonから呼び出し用）
    /// </summary>
    public void SelectArea9() => SelectArea(8);

    /// <summary>
    /// Area 10を選択（Buttonから呼び出し用）
    /// </summary>
    public void SelectArea10() => SelectArea(9);

    /// <summary>
    /// フェードアウトしながらタイトルシーンに遷移
    /// </summary>
    private System.Collections.IEnumerator FadeOutAndBackToTitle()
    {
        // SE再生
        if (buttonClickSE != null && audioSource != null)
        {
            audioSource.PlayOneShot(buttonClickSE);
        }

        // SEが再生されるまでの短い待機時間
        yield return new WaitForSeconds(0.2f);

        Debug.Log("[AreaSelectManager] Fading out and returning to Title");

        // フェード用の黒い画像を作成
        GameObject fadeObj = new GameObject("FadeOut");
        Canvas fadeCanvas = fadeObj.AddComponent<Canvas>();
        fadeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        fadeCanvas.sortingOrder = 9999; // 最前面に表示

        UnityEngine.UI.CanvasScaler scaler = fadeObj.AddComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        GameObject imageObj = new GameObject("FadeImage");
        imageObj.transform.SetParent(fadeObj.transform, false);

        UnityEngine.UI.Image fadeImage = imageObj.AddComponent<UnityEngine.UI.Image>();
        fadeImage.color = new Color(0, 0, 0, 0); // 黒、透明から開始

        RectTransform rectTransform = imageObj.GetComponent<RectTransform>();
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.sizeDelta = Vector2.zero;

        // フェードアウト処理（0.5秒）
        float duration = 0.5f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Clamp01(elapsed / duration);
            fadeImage.color = new Color(0, 0, 0, alpha);
            yield return null;
        }

        // 完全に黒くなったらシーン遷移
        SceneManager.LoadScene("01_Title");
    }

    /// <summary>
    /// シーン開始時にフェードイン
    /// </summary>
    /// <summary>
    /// 初回チュートリアル自動起動時に、AreaSelectのUIを一切見せないための不透明な黒幕を即座に敷く。
    /// フェードなし・即座にalpha=1で表示するため、このフレームでAreaSelectが描画されることはない。
    /// LoadGameSceneWithSE()によるシーン遷移時に自動的に破棄される。
    /// </summary>
    private void CreateOpaqueBlackOverlay()
    {
        GameObject fadeObj = new GameObject("TutorialSkipBlackOverlay");
        Canvas fadeCanvas = fadeObj.AddComponent<Canvas>();
        fadeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        fadeCanvas.sortingOrder = short.MaxValue; // 他の黒幕(sortingOrder=9999)より確実に手前

        UnityEngine.UI.CanvasScaler scaler = fadeObj.AddComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        GameObject imageObj = new GameObject("BlackImage");
        imageObj.transform.SetParent(fadeObj.transform, false);

        UnityEngine.UI.Image fadeImage = imageObj.AddComponent<UnityEngine.UI.Image>();
        fadeImage.color = Color.black;

        RectTransform rectTransform = imageObj.GetComponent<RectTransform>();
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.sizeDelta = Vector2.zero;
    }

    private System.Collections.IEnumerator FadeInOnStart()
    {
        Debug.Log($"[AreaFXDebug] FadeInOnStart begin t={Time.unscaledTime:F4}");
        Debug.Log("[AreaSelectManager] Starting fade in");

        // フェード用の黒い画像を作成
        GameObject fadeObj = new GameObject("FadeIn");
        Canvas fadeCanvas = fadeObj.AddComponent<Canvas>();
        fadeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        fadeCanvas.sortingOrder = 9999; // 最前面に表示

        UnityEngine.UI.CanvasScaler scaler = fadeObj.AddComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        GameObject imageObj = new GameObject("FadeImage");
        imageObj.transform.SetParent(fadeObj.transform, false);

        UnityEngine.UI.Image fadeImage = imageObj.AddComponent<UnityEngine.UI.Image>();
        fadeImage.color = new Color(0, 0, 0, 1); // 黒、完全不透明から開始

        RectTransform rectTransform = imageObj.GetComponent<RectTransform>();
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.sizeDelta = Vector2.zero;

        // フェードイン処理（0.5秒）
        float duration = 0.5f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float alpha = 1f - Mathf.Clamp01(elapsed / duration); // 1から0へ
            fadeImage.color = new Color(0, 0, 0, alpha);
            yield return null;
        }

        // 完全に透明になったらフェードオブジェクトを削除
        Debug.Log($"[AreaFXDebug] FadeInOnStart end (fully transparent) t={Time.unscaledTime:F4}");
        Destroy(fadeObj);
    }

    /// <summary>
    /// BGMを再生する
    /// </summary>
    private void PlayBGM()
    {
        if (debugDisableBGM)
        {
            Debug.Log("[AreaSelectManager] BGM disabled (debugDisableBGM = true)");
            return;
        }

        // TitleBGMManagerが既にBGMを再生している場合はスキップ
        if (Game.UI.TitleBGMManager.IsPlaying)
        {
            Debug.Log("[AreaSelectManager] TitleBGMManager is already playing BGM. Skipping.");
            return;
        }

        // 既に永続BGMが再生中の場合はスキップ
        if (persistentBGMObject != null)
        {
            AudioSource existingSource = persistentBGMObject.GetComponent<AudioSource>();
            if (existingSource != null && existingSource.isPlaying)
            {
                Debug.Log("[AreaSelectManager] Persistent BGM is already playing. Skipping.");
                return;
            }
        }

        if (bgm == null)
        {
            Debug.LogWarning("[AreaSelectManager] BGM is not assigned.");
            return;
        }

        // BGM専用の永続Objectを作成
        if (persistentBGMObject == null)
        {
            persistentBGMObject = new GameObject("AreaSelectBGM_Persistent");
            bgmAudioSource = persistentBGMObject.AddComponent<AudioSource>();
            bgmAudioSource.playOnAwake = false;
            bgmAudioSource.loop = true;
            DontDestroyOnLoad(persistentBGMObject);

            Debug.Log("[AreaSelectManager] Created persistent BGM object");
        }
        else
        {
            bgmAudioSource = persistentBGMObject.GetComponent<AudioSource>();
        }

        if (bgmAudioSource == null)
        {
            Debug.LogError("[AreaSelectManager] BGM AudioSource is null!");
            return;
        }

        bgmAudioSource.clip = bgm;
        bgmAudioSource.volume = PlayerPrefs.GetFloat("BGMVolume", bgmVolume);
        bgmAudioSource.Play();

        Debug.Log($"[AreaSelectManager] BGM started: {bgm.name}, Volume: {bgmVolume}");
    }

    /// <summary>
    /// デバッグ用：Area_01〜Area_10のうち、最初に見つかった未開放Areaを1つだけアンロックする。
    /// 押すたびに次の未開放Areaが1つずつ解放される（例：Area2〜10未開放なら1回目でArea2のみ、2回目でArea3のみ…）。
    /// 全て開放済みなら何もしない。
    /// </summary>
    private void OnClickDebugUnlockNextArea()
    {
        if (!Application.isPlaying) return;

        if (AreaDB.Instance == null || AreaDB.Instance.Catalog == null || ProgressManager.Instance == null)
        {
            Debug.LogWarning("[AreaSelectManager] (DEBUG) AreaDBまたはProgressManagerが未初期化のためアンロックできません。");
            return;
        }

        for (int i = 1; i <= 10; i++)
        {
            string areaId = $"Area_{i:D2}";
            if (UnlockRules.IsAreaUnlocked(areaId)) continue;

            UnlockAreaForDebug(areaId);

            // 全StageButtonの表示を即時更新
            foreach (var sb in FindObjectsOfType<Game.UI.StageButton>())
            {
                sb.UpdateLockStatus();
            }

            Debug.Log($"[AreaSelectManager] (DEBUG) {areaId} をアンロックしました。");
            return;
        }

        Debug.Log("[AreaSelectManager] (DEBUG) 全エリアが既にアンロック済みです。");
    }

    /// <summary>
    /// デバッグ用：Area10のRankバッジ表示を確認するため、クリックする度に
    /// 「なし→E→D→C→B→A→S→なし…」とランクを切り替え、AreaConstellationFXのバッジ表示を即座に更新する。
    /// </summary>
    private static readonly string[] DebugRankCycle = { "", "E", "D", "C", "B", "A", "S" };

    private void OnClickDebugArea10Rank()
    {
        if (!Application.isPlaying || ProgressManager.Instance == null) return;

        var pm = ProgressManager.Instance;
        string current = pm.GetAreaBestRank("Area_10");
        int idx = System.Array.IndexOf(DebugRankCycle, current);
        string next = DebugRankCycle[(idx + 1) % DebugRankCycle.Length];
        pm.DebugSetAreaBestRank("Area_10", next);

        var fx = FindObjectOfType<Game.UI.AreaConstellationFX>();
        if (fx != null) fx.RefreshRankBadges();

        Debug.Log($"[AreaSelectManager] (DEBUG) Area_10のbestRankを\"{next}\"に切り替えました。");
    }

    /// <summary>
    /// 指定Areaの解放条件（unlockByStages / requireAllAreasRankA）を強制的に満たす。
    /// UnlockRules.IsAreaUnlockedが見ている条件と対になるよう実装する。
    /// </summary>
    private void UnlockAreaForDebug(string areaId)
    {
        var area = AreaDB.Instance.Catalog.areas?.FirstOrDefault(a => a != null && a.areaId == areaId);
        if (area == null) return;

        var pm = ProgressManager.Instance;

        if (area.unlockByStages != null)
        {
            foreach (var c in area.unlockByStages)
            {
                if (string.IsNullOrEmpty(c.areaId) || c.stageNumber <= 0) continue;
                pm.MarkStageCleared(c.areaId, c.stageNumber);
            }
        }

        if (area.requireAllAreasRankA)
        {
            for (int i = 1; i <= 9; i++)
            {
                string targetAreaId = $"Area_{i:D2}";
                if (!ProgressManager.IsRankAOrBetter(pm.GetAreaBestRank(targetAreaId)))
                {
                    pm.DebugSetAreaBestRank(targetAreaId, "A");
                }
            }
        }
    }

#if UNITY_EDITOR
    /// <summary>
    /// availableAreas配列にArea10Config.assetを10番目の要素として追加する。
    /// 既に10個以上ある場合は何もしない（重複追加防止・再実行安全）。
    /// </summary>
    [ContextMenu("Add Area10Config to availableAreas (Area10を追加)")]
    private void AddArea10ToAvailableAreas()
    {
        var area10Config = UnityEditor.AssetDatabase.LoadAssetAtPath<AreaConfig>("Assets/Data/AreaConfigs/Area10Config.asset");
        if (area10Config == null)
        {
            Debug.LogError("[AreaSelectManager] Area10Config.asset が見つかりません（Assets/Data/AreaConfigs/Area10Config.asset）。");
            return;
        }

        var so = new UnityEditor.SerializedObject(this);
        so.Update();
        var prop = so.FindProperty("availableAreas");

        if (prop.arraySize >= 10)
        {
            Debug.LogWarning($"[AreaSelectManager] availableAreasは既に{prop.arraySize}個あります。Area10は追加済みの可能性があります。");
            return;
        }

        int newIndex = prop.arraySize;
        prop.arraySize = newIndex + 1;
        prop.GetArrayElementAtIndex(newIndex).objectReferenceValue = area10Config;
        so.ApplyModifiedProperties();

        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log($"[AreaSelectManager] availableAreas[{newIndex}] に Area10Config を追加しました。");
    }

    /// <summary>
    /// Editor拡張：シーン内のCanvas直下に「チュートリアル」ボタンを自動生成する。
    /// 既存の他UIと重なっている場合は、生成後にScene上で自由に位置調整してよい（左上に仮配置）。
    /// </summary>
    [ContextMenu("Setup View Tutorial Button (Canvas直下にボタンを自動生成)")]
    private void SetupViewTutorialButton()
    {
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("[AreaSelectManager] Canvas not found in scene!");
            return;
        }

        Transform existing = canvas.transform.Find("ViewTutorialButton");
        if (existing != null) DestroyImmediate(existing.gameObject);

        GameObject btnObj = new GameObject("ViewTutorialButton", typeof(RectTransform));
        btnObj.transform.SetParent(canvas.transform, false);

        var rect = btnObj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(40f, -40f);
        rect.sizeDelta = new Vector2(240f, 80f);

        var img = btnObj.AddComponent<UnityEngine.UI.Image>();
        img.color = new Color(0.2f, 0.2f, 0.25f, 0.9f);

        var btn = btnObj.AddComponent<UnityEngine.UI.Button>();
        btn.targetGraphic = img;

        GameObject labelObj = new GameObject("Label", typeof(RectTransform));
        labelObj.transform.SetParent(btnObj.transform, false);
        var labelRect = labelObj.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.sizeDelta = Vector2.zero;

        var label = labelObj.AddComponent<TMPro.TextMeshProUGUI>();
        label.text = "チュートリアル";
        label.fontSize = 28;
        label.alignment = TMPro.TextAlignmentOptions.Center;
        label.color = Color.white;

        viewTutorialButton = btn;

        UnityEditor.SerializedObject so = new UnityEditor.SerializedObject(this);
        so.Update();
        so.FindProperty("viewTutorialButton").objectReferenceValue = viewTutorialButton;
        so.ApplyModifiedProperties();
        UnityEditor.EditorUtility.SetDirty(this);

        Debug.Log("[AreaSelectManager] SetupViewTutorialButton: ボタンを生成し、参照をアサインしました。位置は左上に仮配置しているので、既存UIと重なる場合はScene上で動かしてください。", this);
    }

    /// <summary>
    /// Editor拡張：シーン内のCanvas直下に、デバッグ用「次の未開放Areaを1つアンロックする」ボタンを自動生成する。
    /// 押すたびにOnClickDebugUnlockNextArea()経由で最初に見つかった未開放Areaを1つだけ解放する。
    /// 本番ボタンと混同しないよう右上に小さく控えめな見た目で配置する（再実行しても安全）。
    /// </summary>
    [ContextMenu("Setup Debug Unlock Next Area Button (次の未開放Areaを1つアンロックするボタンを配置)")]
    private void SetupDebugUnlockNextAreaButton()
    {
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("[AreaSelectManager] Canvas not found in scene!");
            return;
        }

        Transform existing = canvas.transform.Find("DebugUnlockNextAreaButton");
        GameObject btnObj = existing != null ? existing.gameObject : new GameObject("DebugUnlockNextAreaButton", typeof(RectTransform));
        btnObj.transform.SetParent(canvas.transform, false);

        var rect = btnObj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(-40f, -40f);
        rect.sizeDelta = new Vector2(220f, 70f);

        var img = btnObj.GetComponent<UnityEngine.UI.Image>();
        if (img == null) img = btnObj.AddComponent<UnityEngine.UI.Image>();
        img.color = new Color(0.1f, 0.2f, 0.35f, 0.85f);

        var btn = btnObj.GetComponent<UnityEngine.UI.Button>();
        if (btn == null) btn = btnObj.AddComponent<UnityEngine.UI.Button>();
        btn.targetGraphic = img;

        Transform labelTf = btnObj.transform.Find("Label");
        GameObject labelObj = labelTf != null ? labelTf.gameObject : new GameObject("Label", typeof(RectTransform));
        labelObj.transform.SetParent(btnObj.transform, false);
        var labelRect = labelObj.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.sizeDelta = Vector2.zero;
        labelRect.anchoredPosition = Vector2.zero;

        var label = labelObj.GetComponent<TMPro.TextMeshProUGUI>();
        if (label == null) label = labelObj.AddComponent<TMPro.TextMeshProUGUI>();
        label.text = "次のArea\nアンロック(DEBUG)";
        label.fontSize = 20;
        label.alignment = TMPro.TextAlignmentOptions.Center;
        label.color = new Color(0.7f, 0.85f, 1f, 1f);
        label.raycastTarget = false;

        debugUnlockNextAreaButton = btn;

        UnityEditor.SerializedObject so = new UnityEditor.SerializedObject(this);
        so.Update();
        so.FindProperty("debugUnlockNextAreaButton").objectReferenceValue = debugUnlockNextAreaButton;
        so.ApplyModifiedProperties();
        UnityEditor.EditorUtility.SetDirty(btnObj);

        Debug.Log("[AreaSelectManager] SetupDebugUnlockNextAreaButton: ボタンを生成し、参照をアサインしました。右上に仮配置しているので、既存UIと重なる場合はScene上で動かしてください。", this);
    }

    /// <summary>
    /// デバッグ用：クリックする度にArea10のbestRankを切り替え、Rankバッジの見た目を確認できるボタンを配置する。
    /// 「次のAreaアンロック(DEBUG)」ボタンのすぐ下に並べる（再実行しても安全）。
    /// </summary>
    [ContextMenu("Setup Debug Area10 Rank Button (Area10のRankバッジ表示を確認するボタンを配置)")]
    private void SetupDebugArea10RankButton()
    {
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("[AreaSelectManager] Canvas not found in scene!");
            return;
        }

        Transform existing = canvas.transform.Find("DebugArea10RankButton");
        GameObject btnObj = existing != null ? existing.gameObject : new GameObject("DebugArea10RankButton", typeof(RectTransform));
        btnObj.transform.SetParent(canvas.transform, false);

        var rect = btnObj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(-40f, -120f);
        rect.sizeDelta = new Vector2(220f, 70f);

        var img = btnObj.GetComponent<UnityEngine.UI.Image>();
        if (img == null) img = btnObj.AddComponent<UnityEngine.UI.Image>();
        img.color = new Color(0.1f, 0.2f, 0.35f, 0.85f);

        var btn = btnObj.GetComponent<UnityEngine.UI.Button>();
        if (btn == null) btn = btnObj.AddComponent<UnityEngine.UI.Button>();
        btn.targetGraphic = img;

        Transform labelTf = btnObj.transform.Find("Label");
        GameObject labelObj = labelTf != null ? labelTf.gameObject : new GameObject("Label", typeof(RectTransform));
        labelObj.transform.SetParent(btnObj.transform, false);
        var labelRect = labelObj.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.sizeDelta = Vector2.zero;
        labelRect.anchoredPosition = Vector2.zero;

        var label = labelObj.GetComponent<TMPro.TextMeshProUGUI>();
        if (label == null) label = labelObj.AddComponent<TMPro.TextMeshProUGUI>();
        label.text = "Area10 Rank\n切替(DEBUG)";
        label.fontSize = 20;
        label.alignment = TMPro.TextAlignmentOptions.Center;
        label.color = new Color(0.7f, 0.85f, 1f, 1f);
        label.raycastTarget = false;

        debugArea10RankButton = btn;

        UnityEditor.SerializedObject so = new UnityEditor.SerializedObject(this);
        so.Update();
        so.FindProperty("debugArea10RankButton").objectReferenceValue = debugArea10RankButton;
        so.ApplyModifiedProperties();
        UnityEditor.EditorUtility.SetDirty(btnObj);

        Debug.Log("[AreaSelectManager] SetupDebugArea10RankButton: ボタンを生成し、参照をアサインしました。「次のAreaアンロック(DEBUG)」の下に仮配置しているので、既存UIと重なる場合はScene上で動かしてください。", this);
    }
#endif
}
