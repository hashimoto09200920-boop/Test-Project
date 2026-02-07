using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// シーン開始時にフェードイン効果を自動的に実行する
    /// このスクリプトをシーンの任意のGameObjectにアタッチするだけで動作する
    /// </summary>
    public class SceneFadeIn : MonoBehaviour
    {
        [Header("Fade Settings")]
        [Tooltip("フェードインの時間（秒）")]
        [SerializeField] private float fadeDuration = 0.5f;

        [Tooltip("フェード開始前の待機時間（秒）")]
        [SerializeField] private float initialDelay = 0f;

        private void Start()
        {
            StartCoroutine(FadeInRoutine());
        }

        private System.Collections.IEnumerator FadeInRoutine()
        {
            // 初期待機時間
            if (initialDelay > 0f)
            {
                yield return new WaitForSeconds(initialDelay);
            }

            Debug.Log("[SceneFadeIn] Starting fade in effect");

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

            // フェードイン処理（黒から透明へ）
            float elapsed = 0f;

            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                float alpha = 1f - Mathf.Clamp01(elapsed / fadeDuration); // 1から0へ
                fadeImage.color = new Color(0, 0, 0, alpha);
                yield return null;
            }

            // 完全に透明になったらフェードオブジェクトを削除
            fadeImage.color = new Color(0, 0, 0, 0);
            Destroy(fadeObj);

            Debug.Log("[SceneFadeIn] Fade in complete");
        }
    }
}
