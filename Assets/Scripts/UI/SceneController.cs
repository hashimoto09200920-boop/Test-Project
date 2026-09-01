using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.UI
{
    /// <summary>
    /// シンプルなシーン遷移ユーティリティ。
    /// 残すシーン：01_Title / 03_AreaSelect / 05_Game / 06_Reset
    /// 以外への遷移メソッドは持たない（02_Menu 等を呼べない設計）。
    /// </summary>
    public class SceneController : MonoBehaviour
    {
        [Header("Scene Names (Build Profiles に登録必須)")]
        public string titleScene      = "01_Title";
        public string areaSelectScene = "03_AreaSelect";
        public string gameScene       = "05_Game";
        public string resetScene      = "06_Reset";

        private bool isTransitioning = false;

        // ===== Forward =====
        public void GoToTitle()
        {
            // 既に遷移中なら何もしない（連打防止）
            if (isTransitioning) return;

            isTransitioning = true;
            LoadSafe(titleScene);
        }

        public void GoToAreaSelect()
        {
            // 既に遷移中なら何もしない（連打防止）
            if (isTransitioning) return;

            isTransitioning = true;
            LoadSafe(areaSelectScene);
        }

        public void GoToGame()
        {
            // 既に遷移中なら何もしない（連打防止）
            if (isTransitioning) return;

            isTransitioning = true;
            LoadSafe(gameScene);
        }

        public void GoToReset()
        {
            // 既に遷移中なら何もしない（連打防止）
            if (isTransitioning) return;

            isTransitioning = true;
            LoadSafe(resetScene);
        }

        // ===== Backward (戻り) =====
        public void BackToTitle()
        {
            // 既に遷移中なら何もしない（連打防止）
            if (isTransitioning) return;

            isTransitioning = true;
            StartCoroutine(FadeOutAndLoadScene(titleScene));
        }

        public void BackToAreaSelect()
        {
            // 既に遷移中なら何もしない（連打防止）
            if (isTransitioning) return;

            isTransitioning = true;
            StartCoroutine(FadeOutAndLoadScene(areaSelectScene));
        }

        /// <summary>
        /// メニュー（タイトル）に戻る（旧ボタンとの互換性用）
        /// </summary>
        public void GoToMenu()
        {
            // 既に遷移中なら何もしない（連打防止）
            if (isTransitioning) return;

            isTransitioning = true;
            StartCoroutine(FadeOutAndLoadScene(titleScene));
        }

        // ===== Quit =====
        public void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ===== Common Loader =====
        private void LoadSafe(string sceneName)
        {
            if (!Application.isPlaying) return;
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                Debug.LogWarning("[SceneController] sceneName is empty.");
                return;
            }

            // 現在のプロファイルに登録されているかは Build Profiles 側で管理。
            // ここでは単純にロード。登録されていない場合は Unity が例外/エラーを出す。
            // ★同期LoadSceneはメインスレッドを止めて遷移時にガクつく原因になるため非同期にする
            Debug.Log($"[SceneController] LoadSceneAsync('{sceneName}')");
            SceneManager.LoadSceneAsync(sceneName);
        }

        /// <summary>
        /// フェードアウトしながらシーン遷移
        /// </summary>
        private System.Collections.IEnumerator FadeOutAndLoadScene(string sceneName)
        {
            if (!Application.isPlaying) yield break;
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                Debug.LogWarning("[SceneController] sceneName is empty.");
                yield break;
            }

            Debug.Log($"[SceneController] Fading out and loading scene: {sceneName}");

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
            // ★Time.deltaTimeだとTime.timeScale=0(ポーズ中)の時にフェードが進まなくなってしまうため、
            //   timeScaleの影響を受けないunscaledDeltaTimeを使う。これによりポーズを解除せずに
            //   シーン遷移を待てるようになり、遷移完了前に敵が動き出す不具合を防げる。
            float duration = 0.5f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float alpha = Mathf.Clamp01(elapsed / duration);
                fadeImage.color = new Color(0, 0, 0, alpha);
                yield return null;
            }

            // ★SceneController自身はDontDestroyOnLoadされていないため、シーン遷移(LoadSceneAsync)が
            //   このオブジェクトごと自分を破棄してしまい、このコルーチンの続き(isDone後の処理)が
            //   実行されないまま強制終了する。timeScaleの復元をここでの後処理に頼ると、
            //   一度もTime.timeScale=1に戻らず、遷移先シーンが真っ黒に固まったまま進まなくなる不具合が
            //   あったため、GameObjectの生死に依存しないSceneManager.sceneLoadedイベントで復元する。
            SceneManager.sceneLoaded += RestoreTimeScaleOnSceneLoaded;

            // 完全に黒くなったらシーン遷移（非同期。メインスレッドを止めない）
            SceneManager.LoadSceneAsync(sceneName);
        }

        private static void RestoreTimeScaleOnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Time.timeScale = 1f;
            SceneManager.sceneLoaded -= RestoreTimeScaleOnSceneLoaded;
        }
    }
}
