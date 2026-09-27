using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace Game.UI
{
    /// <summary>
    /// 03_AreaSelect のエリア選択メニュー
    /// </summary>
    [DisallowMultipleComponent]
    public class AreaSelectMenu : MonoBehaviour
    {
        [Header("Navigation")]
        public Button backButton;
        public Button gemManagementButton;
        public Button shopButton;

        [Header("Scene Names")]
        public string stageSelectSceneName = "04_StageSelect";
        public string titleSceneName = "01_Title";
        public string gemSceneName = "02_Gem";
        public string drinkSceneName = "04_Drink";

        [Header("Sound Effects")]
        public AudioClip buttonClickSE;

        private AudioSource audioSource;
        private bool isTransitioning = false;

        private void Awake()
        {
            // ★ドリンク購入回数・ブーストのリセットは、DrinkSession側で「05_Game→03_AreaSelect」の
            //   シーン遷移を直接検知して行うようにしたため、ここでは何もしない
            //   （Gem/Drink/Tutorial/TitleからAreaSelectへ戻る経路と確実に区別するため）。

            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
            }

            // BACKボタンのリスナー登録
            if (backButton != null)
            {
                backButton.onClick.RemoveAllListeners();
                backButton.onClick.AddListener(OnClickBack);
            }

            // GemManagementButton / ShopButton のSEリスナー登録（連打防止付き）
            if (gemManagementButton != null)
                gemManagementButton.onClick.AddListener(OnClickGemManagement);
            if (shopButton != null)
                shopButton.onClick.AddListener(OnClickShop);
        }

        private void OnClickGemManagement()
        {
            if (isTransitioning) return;
            isTransitioning = true;
            // ★OPPO Reno11Aチカチカ不具合対策：Gem画面は02_Gemシーンへ分離。
            //   SE再生はFadeOutAndLoadScene内で行われるためここでは呼ばない。
            ButtonHoverEffect.InputLocked = true;
            StartCoroutine(FadeOutAndLoadScene(gemSceneName));
        }

        private void OnClickShop()
        {
            if (isTransitioning) return;
            isTransitioning = true;
            // ★OPPO Reno11Aチカチカ不具合対策：Drink画面は04_Drinkシーンへ分離。
            //   SE再生はFadeOutAndLoadScene内で行われるためここでは呼ばない。
            ButtonHoverEffect.InputLocked = true;
            StartCoroutine(FadeOutAndLoadScene(drinkSceneName));
        }

        /// <summary>
        /// パネルを閉じた時にGemManagementUI/ShopUIから呼ぶ（連打防止フラグのリセット）
        /// </summary>
        public bool IsTransitioning => isTransitioning;

        public void ResetPanelTransition()
        {
            isTransitioning = false;
        }

        /// <summary>
        /// BACKボタンがクリックされた時の処理
        /// </summary>
        private void OnClickBack()
        {
            // 既に遷移中なら何もしない（連打防止）
            if (isTransitioning) return;

            isTransitioning = true;
            ButtonHoverEffect.InputLocked = true;
            Debug.Log("[AreaSelectMenu] Back to title");
            StartCoroutine(FadeOutAndLoadScene(titleSceneName));
        }

        /// <summary>
        /// ボタンクリック時の効果音を再生
        /// </summary>
        private void PlayButtonSE()
        {
            if (buttonClickSE == null)
            {
                Debug.LogWarning("[AreaSelectMenu] buttonClickSE is null! Please assign SE in Inspector.");
                return;
            }

            if (audioSource == null)
            {
                Debug.LogError("[AreaSelectMenu] audioSource is null!");
                return;
            }

            float vol = SoundSettingsManager.Instance != null ? SoundSettingsManager.Instance.SEVolume : 1f;
            Debug.Log($"[AreaSelectMenu] Playing SE: {buttonClickSE.name}, volume: {vol}");
            audioSource.PlayOneShot(buttonClickSE, vol);
        }

        /// <summary>
        /// SEを再生し、黒画面へフェードアウトしてからシーン遷移
        /// （TitleMenu.FadeOutAndLoadScene()等と同じ構成）
        /// </summary>
        private System.Collections.IEnumerator FadeOutAndLoadScene(string sceneName)
        {
            // ★このオーバーレイは見た目のフェード用。他ボタン・Areaノードのホバー拡大/SEの停止は
            //   ButtonHoverEffect.InputLocked（isTransitioning=trueの直後で設定済み）が担当するため、
            //   ここではGraphicRaycasterを付けていない（レイキャスト自体はブロックしない）。
            GameObject fadeObj = new GameObject("FadeOut");
            Canvas fadeCanvas = fadeObj.AddComponent<Canvas>();
            fadeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            fadeCanvas.sortingOrder = 9999;

            CanvasScaler scaler = fadeObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            GameObject imageObj = new GameObject("FadeImage");
            imageObj.transform.SetParent(fadeObj.transform, false);

            Image fadeImage = imageObj.AddComponent<Image>();
            fadeImage.color = new Color(0, 0, 0, 0); // 黒、透明から開始（見た目専用。ブロック目的では使わない）

            RectTransform rectTransform = imageObj.GetComponent<RectTransform>();
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.sizeDelta = Vector2.zero;

            PlayButtonSE();

            // SEの長さに応じた待機時間（最低0.5秒）
            // ★Time.timeScale=0(ポーズ相当)の間でも待機が進むよう、timeScaleの影響を受けない
            //   WaitForSecondsRealtimeを使う。
            float waitTime = 0.5f;
            if (buttonClickSE != null)
            {
                waitTime = Mathf.Max(buttonClickSE.length, 0.5f);
            }
            yield return new WaitForSecondsRealtime(waitTime);

            Debug.Log($"[AreaSelectMenu] Fading out and loading scene: {sceneName}");

            // ★Time.deltaTimeだとTime.timeScale=0の時にフェードが進まず固まってしまうため、
            //   timeScaleの影響を受けないunscaledDeltaTimeを使う。
            float duration = 0.5f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                fadeImage.color = new Color(0, 0, 0, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }

            SceneManager.LoadScene(sceneName);
        }
    }
}
