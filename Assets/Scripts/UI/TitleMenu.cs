using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace Game.UI
{
    /// <summary>ボタンとハンドラの紐付け方法</summary>
    public enum BindListenersMode
    {
        /// <summary>各ボタンの GameObject 名（Start/Reset/Quit）でハンドラを決める</summary>
        ByName,
        /// <summary>Inspector の Start/Reset/Quit スロット順でハンドラを決める</summary>
        BySlot,
        /// <summary>buttonsGroup の子の並び順（上＝Start, 中央＝Reset, 下＝Quit）。画像に変えても安定。</summary>
        ByOrder
    }

    /// <summary>
    /// 01_Title の Start / Reset / Quit を司る最小メニュー。
    /// Inspector でボタンと遷移先シーン名を割当。
    /// </summary>
    [DisallowMultipleComponent]
    public class TitleMenu : MonoBehaviour
    {
        [Header("Buttons (必須)")]
        public Button startButton;
        public Button resetButton;
        public Button quitButton;

        [Header("Options")]
        /// <summary>ByName=GameObject名で紐付け / BySlot=Inspectorスロット順 / ByOrder=ButtonsGroupの子の並び順（上→Start, 中央→Reset, 下→Quit）</summary>
        public BindListenersMode bindListenersMode = BindListenersMode.ByOrder;

        [Tooltip("BindListenersMode.ByOrder のとき使用。この Transform の子 0,1,2 の Button に順に Start / Reset / Quit を割り当てます。")]
        public Transform buttonsGroup;

        [Header("Scene Names")]
        public string areaSelectSceneName = "03_AreaSelect";
        public string resetSceneName = "06_Reset";

        [Header("Sound Effects")]
        [Tooltip("ボタンクリック時の効果音")]
        public AudioClip buttonClickSE;

        private AudioSource audioSource;
        private bool isTransitioning = false;

        private void Awake()
        {
            // AudioSourceを取得または作成
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
            }

            // ボタンにリスナーを登録
            switch (bindListenersMode)
            {
                case BindListenersMode.ByName:
                    BindListenersByName();
                    break;
                case BindListenersMode.BySlot:
                    BindListenersBySlot();
                    break;
                case BindListenersMode.ByOrder:
                    BindListenersByOrder();
                    break;
            }
        }

        private void Start()
        {
            // シーン開始時にフェードイン
            StartCoroutine(FadeInOnStart());
        }

        /// <summary>
        /// ButtonsGroup の子の並び順でハンドラを登録する（上＝Start, 中央＝Reset, 下＝Quit）。
        /// 画像に変えたり GameObject 名がずれていても、表示順だけで正しく動く。
        /// </summary>
        private void BindListenersByOrder()
        {
            if (buttonsGroup == null)
            {
                Debug.LogWarning("[TitleMenu] BindListenersMode.ByOrder ですが buttonsGroup が未設定です。ByName で代用します。");
                BindListenersByName();
                return;
            }
            int count = buttonsGroup.childCount;
            if (count < 3)
            {
                Debug.LogWarning($"[TitleMenu] buttonsGroup の子が 3 未満です（現在 {count}）。");
            }
            for (int i = 0; i < Mathf.Min(3, count); i++)
            {
                var child = buttonsGroup.GetChild(i);
                var button = child.GetComponent<Button>();
                if (button == null)
                {
                    Debug.LogWarning($"[TitleMenu] buttonsGroup の子 {i} '{child.name}' に Button がありません。");
                    continue;
                }
                button.onClick.RemoveAllListeners();
                if (i == 0)
                {
                    button.onClick.AddListener(OnClickStart);
                    Debug.Log($"[Awake] 表示 1 番目 '{child.name}' → OnClickStart（並び順で紐付け）");
                }
                else if (i == 1)
                {
                    button.onClick.AddListener(OnClickReset);
                    Debug.Log($"[Awake] 表示 2 番目 '{child.name}' → OnClickReset（並び順で紐付け）");
                }
                else
                {
                    button.onClick.AddListener(OnClickQuit);
                    Debug.Log($"[Awake] 表示 3 番目 '{child.name}' → OnClickQuit（並び順で紐付け）");
                }
            }
        }

        /// <summary>
        /// 各ボタンの GameObject 名に応じてハンドラを登録する。
        /// Inspector でどのスロットにどのボタンを入れても、名前で正しく動作する。
        /// </summary>
        private void BindListenersByName()
        {
            var buttons = new[] { startButton, resetButton, quitButton };
            foreach (var button in buttons)
            {
                if (button == null) continue;
                button.onClick.RemoveAllListeners();
                var name = button.gameObject.name;
                if (name.Contains("Start"))
                {
                    button.onClick.AddListener(OnClickStart);
                    Debug.Log($"[Awake] '{name}' → OnClickStart を登録（名前で紐付け）");
                }
                else if (name.Contains("Reset"))
                {
                    button.onClick.AddListener(OnClickReset);
                    Debug.Log($"[Awake] '{name}' → OnClickReset を登録（名前で紐付け）");
                }
                else if (name.Contains("Quit"))
                {
                    button.onClick.AddListener(OnClickQuit);
                    Debug.Log($"[Awake] '{name}' → OnClickQuit を登録（名前で紐付け）");
                }
                else
                {
                    Debug.LogWarning($"[TitleMenu] ボタン '{name}' は Start/Reset/Quit のいずれにも一致しません。");
                }
            }
        }

        /// <summary>
        /// Inspector のスロット順でハンドラを登録する（従来どおり）。
        /// </summary>
        private void BindListenersBySlot()
        {
            if (startButton != null)
            {
                startButton.onClick.RemoveAllListeners();
                startButton.onClick.AddListener(OnClickStart);
                Debug.Log($"[Awake] startButton スロット '{startButton.gameObject.name}' → OnClickStart");
            }
            if (resetButton != null)
            {
                resetButton.onClick.RemoveAllListeners();
                resetButton.onClick.AddListener(OnClickReset);
                Debug.Log($"[Awake] resetButton スロット '{resetButton.gameObject.name}' → OnClickReset");
            }
            if (quitButton != null)
            {
                quitButton.onClick.RemoveAllListeners();
                quitButton.onClick.AddListener(OnClickQuit);
                Debug.Log($"[Awake] quitButton スロット '{quitButton.gameObject.name}' → OnClickQuit");
            }
        }

        private void OnClickStart()
        {
            // 既に遷移中なら何もしない（連打防止）
            if (isTransitioning) return;

            Debug.Log(">>> OnClickStart() が呼ばれました → 03_AreaSelect へ遷移");
            if (!Application.isPlaying) return;
            if (string.IsNullOrWhiteSpace(areaSelectSceneName))
            {
                Debug.LogWarning("[TitleMenu] areaSelectSceneName is empty.");
                return;
            }

            isTransitioning = true;
            StartCoroutine(FadeOutAndLoadScene(areaSelectSceneName));
        }

        private void OnClickReset()
        {
            // 既に遷移中なら何もしない（連打防止）
            if (isTransitioning) return;

            Debug.Log(">>> OnClickReset() が呼ばれました → 06_Reset へ遷移");
            if (!Application.isPlaying) return;
            if (string.IsNullOrWhiteSpace(resetSceneName))
            {
                Debug.LogWarning("[TitleMenu] resetSceneName is empty.");
                return;
            }

            isTransitioning = true;
            StartCoroutine(LoadSceneWithDelayAndSE(resetSceneName));
        }

        private void OnClickQuit()
        {
            // 既に遷移中なら何もしない（連打防止）
            if (isTransitioning) return;

            Debug.Log(">>> OnClickQuit() が呼ばれました → Quit");
            if (!Application.isPlaying) return;

            isTransitioning = true;
            StartCoroutine(QuitWithDelay());
        }

        /// <summary>
        /// ボタンクリック時の効果音を再生
        /// </summary>
        private void PlayButtonSE()
        {
            if (buttonClickSE != null && audioSource != null)
            {
                audioSource.PlayOneShot(buttonClickSE);
            }
        }

        /// <summary>
        /// SEを再生してからシーン遷移
        /// </summary>
        private System.Collections.IEnumerator LoadSceneWithDelayAndSE(string sceneName)
        {
            PlayButtonSE();
            yield return new WaitForSeconds(0.2f);
            SceneManager.LoadScene(sceneName);
        }

        /// <summary>
        /// SEを再生してからアプリケーション終了
        /// </summary>
        private System.Collections.IEnumerator QuitWithDelay()
        {
            PlayButtonSE();
            yield return new WaitForSeconds(0.2f);
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        /// <summary>
        /// フェードアウトしながらシーン遷移
        /// </summary>
        private System.Collections.IEnumerator FadeOutAndLoadScene(string sceneName)
        {
            PlayButtonSE();
            yield return new WaitForSeconds(0.2f);

            Debug.Log($"[TitleMenu] Fading out and loading scene: {sceneName}");

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
            SceneManager.LoadScene(sceneName);
        }

        /// <summary>
        /// シーン開始時にフェードイン
        /// </summary>
        private System.Collections.IEnumerator FadeInOnStart()
        {
            Debug.Log("[TitleMenu] Starting fade in");

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
    }
}
