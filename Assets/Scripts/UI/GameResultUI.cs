using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

/// <summary>
/// 全ステージクリア時のリザルト画面を表示するUI
/// リトライボタンを含む
/// </summary>
public class GameResultUI : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("リザルトメッセージ表示用のTextMeshPro")]
    [SerializeField] private TextMeshProUGUI messageText;

    [Tooltip("リトライボタン")]
    [SerializeField] private Button retryButton;

    [Header("Messages")]
    [Tooltip("全ステージクリア時のメッセージ")]
    [SerializeField] private string allClearMessage = "All Stages Clear!\nThank you for playing!\n\nClick to return to menu...";

    [Tooltip("ゲームオーバー時のメッセージ")]
    [SerializeField] private string gameOverMessage = "Game Over";

    [Header("Scene Settings")]
    [Tooltip("メニューシーンの名前")]
    [SerializeField] private string menuSceneName = "03_AreaSelect";

    private bool isAllClearShown = false;

    private void Start()
    {
        // 初期状態では非表示
        gameObject.SetActive(false);

        // リトライボタンのクリックイベントを登録
        if (retryButton != null)
        {
            retryButton.onClick.AddListener(OnRetryButtonClicked);
        }
        else
        {
            Debug.LogWarning("[GameResultUI] Retry button is not assigned.");
        }
    }

    private void Update()
    {
        // 全ステージクリア時、画面クリックでメニューに戻る
        if (isAllClearShown && Input.GetMouseButtonDown(0))
        {
            Debug.Log("[GameResultUI] Click detected, returning to menu...");
            ReturnToMenu();
        }
    }

    /// <summary>
    /// 全ステージクリア時のリザルト画面を表示
    /// </summary>
    public void ShowAllClearResult()
    {
        if (messageText != null)
        {
            // メッセージを確実に設定
            messageText.text = "All Stages Clear!\nThank you for playing!\n\nClick to return to menu...";
        }

        isAllClearShown = true;
        gameObject.SetActive(true);

        Debug.Log("[GameResultUI] ShowAllClearResult called, isAllClearShown = true");
    }

    /// <summary>
    /// ゲームオーバー時のリザルト画面を表示
    /// </summary>
    public void ShowGameOverResult()
    {
        if (messageText != null)
        {
            messageText.text = gameOverMessage;
        }

        isAllClearShown = false;
        gameObject.SetActive(true);
    }

    /// <summary>
    /// リトライボタンがクリックされた時の処理
    /// </summary>
    private void OnRetryButtonClicked()
    {
        // 現在のシーンをリロード
        string currentSceneName = SceneManager.GetActiveScene().name;
        SceneManager.LoadScene(currentSceneName);
    }

    /// <summary>
    /// メニュー画面に戻る
    /// </summary>
    private void ReturnToMenu()
    {
        // フェードアウトしながらメニューに戻る
        StartCoroutine(FadeOutAndReturnToMenu());
    }

    /// <summary>
    /// フェードアウトしながらメニューシーンに遷移
    /// </summary>
    private System.Collections.IEnumerator FadeOutAndReturnToMenu()
    {
        // GameSessionをリセット
        GameSession.Reset();

        Debug.Log($"[GameResultUI] Fading out and returning to menu: {menuSceneName}");

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
        SceneManager.LoadScene(menuSceneName);
    }
}
