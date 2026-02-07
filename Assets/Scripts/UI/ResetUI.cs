using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Game.Progress;

public class ResetUI : MonoBehaviour
{
    [Header("UI 参照")]
    [Tooltip("Yes ボタン（必須）")]
    public Button yesButton;
    [Tooltip("No ボタン（必須）")]
    public Button noButton;

    [Header("メッセージ表示（任意）")]
    [Tooltip("リセット完了メッセージを表示するパネル（任意）。未設定ならログのみ表示して即戻る。")]
    public GameObject messagePanel;
    [Tooltip("メッセージ本文（Legacy Text 推奨／任意）。未設定でも動作します。")]
    public Text messageText;
    [Tooltip("メッセージOKボタン（任意）。未設定の場合は『画面のどこかクリック』で戻ります。")]
    public Button messageOkButton;

    [Header("文言切替")]
    [Tooltip("ON: InspectorのMessageTextに書いた文字列をそのまま使う（上書きしない）。OFF: 下の固定文言で上書き。")]
    public bool useInspectorText = true;
    [TextArea(1, 4)]
    [Tooltip("useInspectorText=OFFのときに表示する固定文言")]
    public string messageOnReset = "データを初期化しました。タイトルへ戻ります。";

    [Header("遷移先（未設定なら 01_Title に戻る）")]
    public string backSceneName = "01_Title";

    private bool _waitingAnyClickToBack;
    private bool isTransitioning = false;

    private void Awake()
    {
        // ボタン購読
        if (yesButton != null)
        {
            yesButton.onClick.RemoveListener(OnClickYes);
            yesButton.onClick.AddListener(OnClickYes);
        }
        if (noButton != null)
        {
            noButton.onClick.RemoveListener(OnClickNo);
            noButton.onClick.AddListener(OnClickNo);
        }
        if (messageOkButton != null)
        {
            messageOkButton.onClick.RemoveListener(BackToPrevScene);
            messageOkButton.onClick.AddListener(BackToPrevScene);
        }

        // 初期は非表示
        if (messagePanel != null) messagePanel.SetActive(false);
    }

    private void Start()
    {
        // シーン開始時にフェードイン
        StartCoroutine(FadeInOnStart());
    }

    private void Update()
    {
        // OKボタンが無い場合のフォールバック：「どこかクリックで戻る」
        if (_waitingAnyClickToBack && messageOkButton == null)
        {
            if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1))
            {
                _waitingAnyClickToBack = false;
                BackToPrevScene();
            }
        }
    }

    // ===== ボタンハンドラ =====

    private void OnClickYes()
    {
        // 既に遷移中なら何もしない（連打防止）
        if (isTransitioning) return;

        isTransitioning = true;

        // セーブデータのリセット
        var pm = ProgressManager.Instance;
        if (pm != null)
        {
            pm.ResetAll(); // または pm.ResetProgress();
            Debug.Log("[ResetUI] ResetAll done.");
        }
        else
        {
            Debug.LogWarning("[ResetUI] ProgressManager.Instance が見つかりません。PlayerPrefs を全削除します.");
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
        }

        // メッセージ表示
        if (messagePanel != null)
        {
            if (!useInspectorText && messageText != null)
            {
                // 固定文言で上書き（従来仕様）
                messageText.text = messageOnReset;
            }
            // useInspectorText = true の場合は Inspector の文字列をそのまま使う（上書きしない）

            messagePanel.SetActive(true);
            _waitingAnyClickToBack = true; // OKボタンが無ければ任意クリックで戻る
        }
        else
        {
            // メッセージ領域が無ければ即戻る
            BackToPrevScene();
        }
    }

    private void OnClickNo()
    {
        // 既に遷移中なら何もしない（連打防止）
        if (isTransitioning) return;

        // 何もせず以前のシーンへ戻る
        BackToPrevScene();
    }

    // ===== 遷移共通化 =====

    private void BackToPrevScene()
    {
        // 既に遷移中なら何もしない（連打防止）
        if (isTransitioning) return;

        isTransitioning = true;
        var sceneName = string.IsNullOrWhiteSpace(backSceneName) ? "01_Title" : backSceneName;
        StartCoroutine(FadeOutAndLoadScene(sceneName));
    }

    /// <summary>
    /// フェードアウトしながらシーン遷移
    /// </summary>
    private System.Collections.IEnumerator FadeOutAndLoadScene(string sceneName)
    {
        Debug.Log($"[ResetUI] Fading out and loading scene: {sceneName}");

        // フェード用の黒い画像を作成
        GameObject fadeObj = new GameObject("FadeOut");
        Canvas fadeCanvas = fadeObj.AddComponent<Canvas>();
        fadeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        fadeCanvas.sortingOrder = 9999; // 最前面に表示

        CanvasScaler scaler = fadeObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        GameObject imageObj = new GameObject("FadeImage");
        imageObj.transform.SetParent(fadeObj.transform, false);

        Image fadeImage = imageObj.AddComponent<Image>();
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
        SceneManager.LoadScene(sceneName);
    }

    /// <summary>
    /// シーン開始時にフェードイン
    /// </summary>
    private System.Collections.IEnumerator FadeInOnStart()
    {
        Debug.Log("[ResetUI] Starting fade in");

        // フェード用の黒い画像を作成
        GameObject fadeObj = new GameObject("FadeIn");
        Canvas fadeCanvas = fadeObj.AddComponent<Canvas>();
        fadeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        fadeCanvas.sortingOrder = 9999; // 最前面に表示

        CanvasScaler scaler = fadeObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        GameObject imageObj = new GameObject("FadeImage");
        imageObj.transform.SetParent(fadeObj.transform, false);

        Image fadeImage = imageObj.AddComponent<Image>();
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
}
