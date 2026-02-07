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

        [Header("Scene Names")]
        public string stageSelectSceneName = "04_StageSelect";
        public string titleSceneName = "01_Title";

        [Header("Sound Effects")]
        public AudioClip buttonClickSE;

        private AudioSource audioSource;
        private bool isTransitioning = false;

        private void Awake()
        {
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
        }

        /// <summary>
        /// BACKボタンがクリックされた時の処理
        /// </summary>
        private void OnClickBack()
        {
            // 既に遷移中なら何もしない（連打防止）
            if (isTransitioning) return;

            isTransitioning = true;
            Debug.Log("[AreaSelectMenu] Back to title");
            StartCoroutine(LoadSceneWithDelayAndSE(titleSceneName));
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

            Debug.Log($"[AreaSelectMenu] Playing SE: {buttonClickSE.name}, volume: {audioSource.volume}");
            audioSource.PlayOneShot(buttonClickSE);
        }

        /// <summary>
        /// SEを再生してからシーン遷移
        /// </summary>
        private System.Collections.IEnumerator LoadSceneWithDelayAndSE(string sceneName)
        {
            PlayButtonSE();

            // SEの長さに応じた待機時間（最低0.5秒）
            float waitTime = 0.5f;
            if (buttonClickSE != null)
            {
                waitTime = Mathf.Max(buttonClickSE.length, 0.5f);
            }

            yield return new WaitForSeconds(waitTime);
            SceneManager.LoadScene(sceneName);
        }
    }
}
