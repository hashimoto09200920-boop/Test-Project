using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.UI
{
    /// <summary>
    /// タイトルBGMを管理し、シーン間で継続再生する
    /// 05_Gameシーンに入ったらBGMを停止する
    /// </summary>
    public class TitleBGMManager : MonoBehaviour
    {
        private static TitleBGMManager instance;
        private AudioSource audioSource;

        [Header("Settings")]
        [Tooltip("BGMを停止するシーン名")]
        [SerializeField] private string stopSceneName = "05_Game";

        /// <summary>
        /// TitleBGMManagerが存在し、BGMが再生中かどうか
        /// </summary>
        public static bool IsPlaying
        {
            get
            {
                return instance != null && instance.audioSource != null && instance.audioSource.isPlaying;
            }
        }

        private void Awake()
        {
            // AudioSourceの自動再生を無効化（重複再生を防ぐ）
            audioSource = GetComponent<AudioSource>();
            if (audioSource != null)
            {
                // 既に自動再生されている場合は停止
                if (audioSource.isPlaying)
                {
                    audioSource.Stop();
                    Debug.Log("[TitleBGMManager] Stopped auto-playing AudioSource");
                }
                audioSource.playOnAwake = false;
            }

            // シングルトンパターン
            if (instance == null)
            {
                instance = this;
                DontDestroyOnLoad(gameObject);

                // AreaSelectの永続BGMが既に再生中かチェック
                GameObject persistentBGM = GameObject.Find("AreaSelectBGM_Persistent");
                bool otherBGMPlaying = false;

                if (persistentBGM != null)
                {
                    AudioSource persistentSource = persistentBGM.GetComponent<AudioSource>();
                    if (persistentSource != null && persistentSource.isPlaying)
                    {
                        otherBGMPlaying = true;
                        Debug.Log("[TitleBGMManager] AreaSelect BGM is playing. Keeping it playing.");
                    }
                }

                // 他のBGMが再生中でない場合のみ再生開始
                if (!otherBGMPlaying && audioSource != null && !audioSource.isPlaying)
                {
                    audioSource.Play();
                }

                // シーン切り替え時のイベント登録
                SceneManager.sceneLoaded += OnSceneLoaded;

                Debug.Log("[TitleBGMManager] BGM manager initialized and started");
            }
            else
            {
                // 既に存在する場合は自分を削除
                Debug.Log("[TitleBGMManager] Duplicate instance destroyed");
                Destroy(gameObject);
            }
        }

        private void OnDestroy()
        {
            // イベント登録解除
            if (instance == this)
            {
                SceneManager.sceneLoaded -= OnSceneLoaded;
            }
        }

        /// <summary>
        /// シーンがロードされた時の処理
        /// </summary>
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Debug.Log($"[TitleBGMManager] Scene loaded: {scene.name}");

            // 05_Gameシーンに入ったらBGMを停止して自分を削除
            if (scene.name == stopSceneName)
            {
                Debug.Log("[TitleBGMManager] Stopping BGM and destroying manager");

                if (audioSource != null && audioSource.isPlaying)
                {
                    audioSource.Stop();
                }

                // AreaSelectの永続BGMも削除
                GameObject persistentBGM = GameObject.Find("AreaSelectBGM_Persistent");
                if (persistentBGM != null)
                {
                    Debug.Log("[TitleBGMManager] Destroying AreaSelect persistent BGM");
                    Destroy(persistentBGM);
                }

                instance = null;
                Destroy(gameObject);
            }
        }
    }
}
