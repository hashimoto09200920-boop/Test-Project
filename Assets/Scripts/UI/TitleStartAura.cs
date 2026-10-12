using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Title画面①：STARTボタンだけを主役として目立たせる。
    /// ボタン枠の外側にオレンジの光の縁取りをつけてゆっくり脈打たせ、枠の線の上を光の粒が2つ回り続ける。
    /// ボタンが押せない間（起動時のフェードイン中・SOUND/LANGUAGE画面を開いている間）は消えている。
    /// 部品はメニュー「Tools/タイトル画面/見やすさ改善（1回目）を組み込む（01_Title）」でPlay前に生成・参照設定する。
    /// ★動かすのは色と位置だけ（レイアウトの再計算は起こさない）。
    /// </summary>
    public class TitleStartAura : MonoBehaviour
    {
        [Header("参照（メニューで自動設定）")]
        [SerializeField] private Button button;
        [SerializeField] private Image aura;
        [SerializeField] private Image[] runners;

        [Header("光の縁取り")]
        [SerializeField] private Color auraColor = new Color(1f, 0.6f, 0.22f, 1f);
        [SerializeField, Range(0f, 1f)] private float auraMinAlpha = 0.35f;
        [SerializeField, Range(0f, 1f)] private float auraMaxAlpha = 0.95f;
        [Tooltip("脈打つ速さ（1秒あたりの回数）")]
        [SerializeField] private float auraPulsePerSecond = 0.55f;
        [Tooltip("ボタンが押せるようになってから縁取りが現れ始めるまでの秒数（ボタンの点灯演出と重ならないように）")]
        [SerializeField] private float appearDelay = 0.6f;
        [Tooltip("現れる／消える時のフェード秒数")]
        [SerializeField] private float fadeSeconds = 0.5f;

        [Header("枠を回る光")]
        [SerializeField] private Color runnerColor = new Color(1f, 0.88f, 0.65f, 1f);
        [Tooltip("1秒あたりに進む距離(px)")]
        [SerializeField] private float runnerSpeed = 360f;
        [Tooltip("光が通る線の位置：ボタンの外周から内側へのずらし量(px)。ボタン画像の外側の線に合わせた値")]
        [SerializeField] private float runnerInset = 3.5f;

        private float fade;
        private float readyTime = -1f;
        private float runnerDist;
        private bool shown = true;

        private void OnEnable()
        {
            fade = 0f;
            readyTime = -1f;
            SetShown(false);
        }

        private void LateUpdate()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            float now = Time.unscaledTime;

            // IsInteractable：ボタン自身に加え、親のCanvasGroupで操作を止めている間もfalseになる
            bool ready = button == null || (button.IsInteractable() && button.gameObject.activeInHierarchy);
            if (ready) { if (readyTime < 0f) readyTime = now; }
            else readyTime = -1f;
            bool want = ready && now - readyTime >= appearDelay;

            fade = Mathf.MoveTowards(fade, want ? 1f : 0f, dt / Mathf.Max(0.05f, fadeSeconds));
            if (fade <= 0f) { SetShown(false); return; }
            SetShown(true);

            float pulse = 0.5f + 0.5f * Mathf.Sin(now * auraPulsePerSecond * Mathf.PI * 2f);
            if (aura != null)
            {
                Color c = auraColor;
                c.a = auraColor.a * fade * Mathf.Lerp(auraMinAlpha, auraMaxAlpha, pulse);
                aura.color = c;
            }

            if (runners == null || runners.Length == 0) return;
            Rect r = ((RectTransform)transform).rect;
            float w = Mathf.Max(1f, r.width - runnerInset * 2f), h = Mathf.Max(1f, r.height - runnerInset * 2f);
            float perim = PillPerimeter(w, h);
            runnerDist = Mathf.Repeat(runnerDist + runnerSpeed * dt, perim);
            for (int i = 0; i < runners.Length; i++)
            {
                if (runners[i] == null) continue;
                // 粒は周の反対側に等間隔で置く
                float d = Mathf.Repeat(runnerDist + perim * i / runners.Length, perim);
                runners[i].rectTransform.anchoredPosition = r.center + PointOnPill(w, h, d);
                Color c = runnerColor;
                c.a = runnerColor.a * fade;
                runners[i].color = c;
            }
        }

        private void SetShown(bool on)
        {
            if (shown == on) return;
            shown = on;
            if (aura != null) aura.enabled = on;
            if (runners != null) foreach (var im in runners) if (im != null) im.enabled = on;
        }

        private static float PillPerimeter(float w, float h)
        {
            float r = Mathf.Min(w, h) * 0.5f;
            return 2f * Mathf.Max(0f, w - 2f * r) + 2f * Mathf.PI * r;
        }

        /// <summary>角丸の両端が半円になった長方形（ピル形）の外周上の点。左上から時計回りに距離dだけ進んだ位置（中心基準）</summary>
        private static Vector2 PointOnPill(float w, float h, float d)
        {
            float r = Mathf.Min(w, h) * 0.5f;
            float half = Mathf.Max(0f, w * 0.5f - r);
            float straight = half * 2f, arc = Mathf.PI * r;
            if (d < straight) return new Vector2(-half + d, r);
            d -= straight;
            if (d < arc) { float a = Mathf.PI * 0.5f - d / r; return new Vector2(half + Mathf.Cos(a) * r, Mathf.Sin(a) * r); }
            d -= arc;
            if (d < straight) return new Vector2(half - d, -r);
            d -= straight;
            { float a = -Mathf.PI * 0.5f - d / r; return new Vector2(-half + Mathf.Cos(a) * r, Mathf.Sin(a) * r); }
        }
    }
}
