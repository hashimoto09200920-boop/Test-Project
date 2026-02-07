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

    private AudioSource audioSource;
    private AudioSource bgmAudioSource;
    private bool isTransitioning = false;
    private static GameObject persistentBGMObject;

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
    }

    private void Start()
    {
        // GameSessionをリセット
        GameSession.Reset();

        // BGMを再生
        PlayBGM();

        // シーン開始時にフェードイン
        StartCoroutine(FadeInOnStart());
    }

    /// <summary>
    /// エリアを選択してゲームを開始
    /// </summary>
    /// <param name="areaIndex">エリアのインデックス（0始まり）</param>
    public void SelectArea(int areaIndex)
    {
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

        // ゲームシーンに遷移
        StartCoroutine(LoadGameSceneWithSE());
    }

    /// <summary>
    /// AreaConfigを直接指定してゲームを開始
    /// </summary>
    public void SelectArea(AreaConfig area)
    {
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

        StartCoroutine(LoadGameSceneWithSE());
    }

    /// <summary>
    /// SEを再生してからゲームシーンをロード
    /// </summary>
    private System.Collections.IEnumerator LoadGameSceneWithSE()
    {
        if (string.IsNullOrEmpty(gameSceneName))
        {
            Debug.LogError("[AreaSelectManager] Game scene name is not set!");
            yield break;
        }

        // SE再生
        if (buttonClickSE != null && audioSource != null)
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
    private System.Collections.IEnumerator FadeInOnStart()
    {
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
        Destroy(fadeObj);
    }

    /// <summary>
    /// BGMを再生する
    /// </summary>
    private void PlayBGM()
    {
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
        bgmAudioSource.volume = bgmVolume;
        bgmAudioSource.Play();

        Debug.Log($"[AreaSelectManager] BGM started: {bgm.name}, Volume: {bgmVolume}");
    }
}
