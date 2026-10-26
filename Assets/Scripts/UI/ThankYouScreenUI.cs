using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

namespace Game.UI
{
    /// <summary>
    /// Area10を初めてクリアした時だけ、ジェム取得・リザルトの後、AreaSelectへ戻る前に「THANK YOU FOR PLAYING」を表示する（05_Game）。
    /// ・流れ：リザルトを閉じる → GameResultUI.AutoReturn() がここへ回す → 背景画像がフェードイン → タイトルがネオン管らしくチカチカ点灯
    ///   → タップ／クリックで3秒かけて黒へフェードアウトしながら03_AreaSelectへ
    /// ・BGMには一切触らない（Area10の曲が最初から再生し直されず、そのまま流れ続ける）。シーンを切り替えずに05_Gameの上へ重ねるのはこのため
    /// ・初回かどうかは、EnemySpawnerがArea10の最終ステージを「初めて」クリア済みにした時に立てる印（PendingFirstArea10Clear）で判断する
    /// ・時間はすべて実時間（リザルト中にTime.timeScaleが止まっていても進む）
    /// 部品はメニュー「Tools/Thank you画面/05_Gameに作成」でPlay前に生成・参照設定する。背景画像は Background の Image に設定する。
    /// </summary>
    public class ThankYouScreenUI : MonoBehaviour
    {
        /// <summary>Area10の最終ステージを初めてクリアした時にEnemySpawnerが立てる。表示したら下ろす</summary>
        public static bool PendingFirstArea10Clear;

        private static ThankYouScreenUI instance;

        [Header("参照（メニューで自動設定）")]
        [SerializeField] private Canvas canvas;
        [SerializeField] private CanvasGroup rootGroup;
        [SerializeField] private Image background;
        [Tooltip("タイトルの入れ物（点灯のチカチカ・呼吸はここの明るさで動かす）")]
        [SerializeField] private CanvasGroup titleGroup;
        [Tooltip("タイトルを画像（ネオン管の文字画像など）にする場合はここに画像を設定する。設定するとTitleTextは隠れる")]
        [SerializeField] private Image titleImage;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private Image titleGlow;
        [SerializeField] private Image fadeImage;

        [Header("表示")]
        [Tooltip("背景・タイトル全体が現れるまでの秒数")]
        [SerializeField] private float fadeInSeconds = 1.5f;
        [Tooltip("背景が出てからタイトルが点き始めるまでの秒数")]
        [SerializeField] private float titleDelay = 0.6f;
        [Tooltip("点灯前のチカチカの回数")]
        [SerializeField] private int titleFlickerCount = 4;
        [SerializeField] private Vector2 titleFlickerSeconds = new Vector2(0.04f, 0.14f);
        [SerializeField, Range(0f, 1f)] private float titleDimAlpha = 0.15f;
        [Tooltip("点灯後の明るさの呼吸（±）。0で止まる")]
        [SerializeField, Range(0f, 0.5f)] private float titleBreathAmount = 0.08f;
        [SerializeField] private float titleBreathSpeed = 0.5f;

        [Header("戻る")]
        [Tooltip("タイトルが点き終わってから、タップ／クリックを受け付けるまでの秒数（うっかり飛ばさないように）")]
        [SerializeField] private float inputDelayAfterTitle = 1f;
        [Tooltip("タップ／クリックしてから黒へフェードアウトし終わるまでの秒数")]
        [SerializeField] private float fadeOutSeconds = 3f;
        [SerializeField] private string areaSelectSceneName = "03_AreaSelect";

        [Header("テスト")]
        [Tooltip("ONにすると、初回かどうかに関係なく、どのAreaのクリアでも表示する（確認用。必ずOFFに戻すこと）")]
        [SerializeField] private bool debugShowOnAnyClear = false;

        private enum State { Hidden, Showing, Waiting, Leaving }
        private State state = State.Hidden;
        private float t;
        private float[] flickerTimes;
        private float inputReadyTime;
        private Color titleGlowBase = Color.white;

        private void Awake()
        {
            instance = this;
            PendingFirstArea10Clear = false; // 05_Gameに入るたびに下ろす（前回の残りで別Areaのクリア時に出ないように）
            if (titleGlow != null) titleGlowBase = titleGlow.color;
            HideAll();
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        /// <summary>
        /// GameResultUI.AutoReturn()の最初に呼ぶ。表示する時はtrue（呼び出し元はAreaSelectへ戻る処理をしない。戻るのはこちらが行う）
        /// </summary>
        public static bool TryShowInsteadOfReturn()
        {
            if (instance == null) return false;
            if (!PendingFirstArea10Clear && !instance.debugShowOnAnyClear) return false;
            PendingFirstArea10Clear = false;
            instance.Show();
            return true;
        }

        private void HideAll()
        {
            state = State.Hidden;
            if (canvas != null) canvas.enabled = false;
            if (rootGroup != null) { rootGroup.alpha = 0f; rootGroup.blocksRaycasts = false; }
            if (fadeImage != null) { fadeImage.color = new Color(0f, 0f, 0f, 0f); fadeImage.enabled = false; }
        }

        private void Show()
        {
            bool useImage = titleImage != null && titleImage.sprite != null;
            if (titleImage != null) titleImage.gameObject.SetActive(useImage);
            if (titleText != null) titleText.gameObject.SetActive(!useImage);

            if (background != null) background.enabled = background.sprite != null; // 画像が未設定の間は黒の下地だけ
            // 背景画像の縦横比に合わせて、画面を覆う大きさにする（はみ出た分は切れる）。生成した画像の比率がまちまちでも歪まないように
            var fitter = background != null ? background.GetComponent<AspectRatioFitter>() : null;
            if (fitter != null && background.sprite != null && background.sprite.rect.height > 0f)
                fitter.aspectRatio = background.sprite.rect.width / background.sprite.rect.height;
            if (canvas != null) canvas.enabled = true;
            if (rootGroup != null) { rootGroup.alpha = 0f; rootGroup.blocksRaycasts = true; }
            if (titleGroup != null) titleGroup.alpha = 0f;

            int count = Mathf.Max(0, titleFlickerCount) * 2 + 1; // 明→暗をcount回、最後に点灯
            flickerTimes = new float[count];
            float ft = fadeInSeconds + titleDelay;
            for (int i = 0; i < count; i++) { flickerTimes[i] = ft; ft += Random.Range(titleFlickerSeconds.x, titleFlickerSeconds.y); }
            inputReadyTime = flickerTimes[count - 1] + inputDelayAfterTitle;

            t = 0f;
            state = State.Showing;
        }

        private void Update()
        {
            if (state == State.Hidden) return;
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            t += dt;

            if (rootGroup != null && state != State.Leaving) rootGroup.alpha = Mathf.Clamp01(t / Mathf.Max(0.01f, fadeInSeconds));
            UpdateTitle();

            if (state == State.Showing && t >= inputReadyTime) state = State.Waiting;
            if (state == State.Waiting && PressedThisFrame()) StartCoroutine(LeaveRoutine());
        }

        private void UpdateTitle()
        {
            if (titleGroup == null || flickerTimes == null) return;
            int idx = -1;
            for (int k = 0; k < flickerTimes.Length; k++) if (t >= flickerTimes[k]) idx = k;
            float a;
            if (idx < 0) a = 0f;
            else if (idx < flickerTimes.Length - 1) a = idx % 2 == 0 ? 1f : titleDimAlpha;
            else a = 1f + Mathf.Sin((t - flickerTimes[flickerTimes.Length - 1]) * titleBreathSpeed * Mathf.PI * 2f) * titleBreathAmount;
            titleGroup.alpha = Mathf.Clamp01(a);
            if (titleGlow != null) { Color c = titleGlowBase; c.a = titleGlowBase.a * Mathf.Clamp01(a); titleGlow.color = c; }
        }

        private static bool PressedThisFrame()
        {
            // ★スマホの素早いタップは、触れた・離したが同じフレームに届くと下の Input System の判定で取りこぼすことがある（2026/10/26）。
            //   ゲーム中の線の入力（PaddleDrawer）と同じ旧 Input の「触れ始め」とクリックも見る
            for (int i = 0; i < Input.touchCount; i++)
                if (Input.GetTouch(i).phase == UnityEngine.TouchPhase.Began) return true;
            if (Input.GetMouseButtonDown(0)) return true;

            var p = Pointer.current;
            if (p != null && p.press.wasPressedThisFrame) return true;
            var ts = Touchscreen.current;
            return ts != null && ts.primaryTouch.press.wasPressedThisFrame;
        }

        private System.Collections.IEnumerator LeaveRoutine()
        {
            state = State.Leaving;
            if (fadeImage != null) fadeImage.enabled = true;
            float e = 0f, dur = Mathf.Max(0.01f, fadeOutSeconds);
            while (e < dur)
            {
                e += Time.unscaledDeltaTime;
                if (fadeImage != null) fadeImage.color = new Color(0f, 0f, 0f, Mathf.Clamp01(e / dur));
                yield return null;
            }
            // GameResultUI.FadeOutAndReturnToMenuと同じ後始末
            GameSession.Reset();
            Game.Shop.DrinkSession.Reset();
            Time.timeScale = 1f;
            SceneManager.LoadScene(areaSelectSceneName);
        }

#if UNITY_EDITOR
        [ContextMenu("テスト表示（Play中のみ）")]
        private void DebugShowNow()
        {
            if (!Application.isPlaying) { Debug.LogWarning("[ThankYouScreenUI] Play中に実行してください"); return; }
            Show();
        }
#endif
    }
}
