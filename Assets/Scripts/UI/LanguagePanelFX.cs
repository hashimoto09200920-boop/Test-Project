using UnityEngine;
using UnityEngine.UI;
using Game.Localization;

namespace Game.UI
{
    /// <summary>
    /// Title画面②：LANGUAGE画面をSOUND画面（SoundPanelFX）と同じ見た目にそろえる。
    /// ・開く時のポップ／枠を走る光（SoundPanelFXと同じ動き。上から下りる光の帯はユーザー判断で不要となり削除）
    /// ・言語を切り替えた瞬間、選んだボタンから光の粒が弾ける
    /// （選択中の光る枠・✓のバッジはユーザー判断で不要となり削除）
    /// 部品はメニュー「Tools/タイトル画面/見やすさ改善（1回目）を組み込む（01_Title）」でPlay前に生成・参照設定する。
    /// ボタンの色分け（選択中=明るい／未選択=暗い）は従来どおりTitleMenu側が行う。
    /// </summary>
    public class LanguagePanelFX : MonoBehaviour
    {
        [Header("参照（メニューで自動設定）")]
        [SerializeField] private RectTransform frameRect;
        [SerializeField] private RectTransform japaneseButton;
        [SerializeField] private RectTransform englishButton;

        [Header("切り替えた瞬間の光の粒")]
        [SerializeField] private Image[] sparks;
        [SerializeField] private float sparkLife = 0.5f;
        [SerializeField] private Vector2 sparkSpeed = new Vector2(180f, 420f);
        [SerializeField] private Color sparkColor = new Color(0.55f, 1f, 1f, 1f);

        [Header("開く時")]
        [SerializeField] private float openPopSeconds = 0.28f;

        [Header("枠を走る光")]
        [SerializeField] private RectTransform[] runnerDots;
        [Tooltip("枠（LanguageBg）の外周から内側へのずらし量。画像の見た目の枠線に合わせて調整")]
        [SerializeField] private Vector2 runnerInset = new Vector2(80f, 42f);
        [SerializeField] private float runnerSpeed = 520f;
        [SerializeField] private float runnerSpacing = 14f;

        private bool synced;
        private GameLanguage lastLang;
        private float openTimer = -1f;
        private float runnerDist;
        private struct Particle { public float age, life; public Vector2 pos, vel; }
        private Particle[] sparkState;

        private void Awake()
        {
            sparkState = new Particle[sparks != null ? sparks.Length : 0];
        }

        private void OnEnable()
        {
            synced = false;
            openTimer = 0f;
            for (int i = 0; i < sparkState?.Length; i++) { sparkState[i].life = 0f; if (sparks[i] != null) sparks[i].enabled = false; }
            transform.localScale = Vector3.one * 0.85f;
        }

        private void OnDisable()
        {
            transform.localScale = Vector3.one;
        }

        private void LateUpdate()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            float now = Time.unscaledTime;

            var lang = LocalizationManager.Instance != null ? LocalizationManager.Instance.CurrentLanguage : GameLanguage.Japanese;
            if (!synced || lang != lastLang)
            {
                bool changed = synced;
                synced = true;
                lastLang = lang;
                if (changed) Burst(lang == GameLanguage.English ? englishButton : japaneseButton);
            }

            UpdateOpen(dt);
            UpdateSparks(dt);
            UpdateRunner(dt);
        }

        // ---------- 光の粒 ----------
        private void Burst(RectTransform at)
        {
            if (at == null || sparks == null) return;
            Vector2 p = transform.InverseTransformPoint(at.position);
            for (int i = 0; i < sparks.Length; i++)
            {
                if (sparks[i] == null) continue;
                float ang = (i / (float)sparks.Length) * Mathf.PI * 2f + Random.Range(-0.2f, 0.2f);
                sparkState[i] = new Particle
                {
                    age = 0f,
                    life = sparkLife * Random.Range(0.75f, 1.15f),
                    pos = p,
                    vel = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * Random.Range(sparkSpeed.x, sparkSpeed.y),
                };
                sparks[i].color = sparkColor;
                sparks[i].enabled = true;
            }
        }

        private void UpdateSparks(float dt)
        {
            for (int i = 0; sparks != null && i < sparks.Length; i++)
            {
                if (sparks[i] == null || !sparks[i].enabled) continue;
                ref Particle p = ref sparkState[i];
                p.age += dt;
                if (p.age >= p.life) { sparks[i].enabled = false; continue; }
                p.vel *= Mathf.Max(0f, 1f - 3.5f * dt);
                p.pos += p.vel * dt;
                float k = p.age / p.life;
                sparks[i].rectTransform.anchoredPosition = p.pos;
                sparks[i].rectTransform.localScale = Vector3.one * Mathf.Lerp(1.2f, 0.3f, k);
                Color c = sparkColor; c.a = sparkColor.a * (1f - k); sparks[i].color = c;
            }
        }

        // ---------- 開く時（SoundPanelFXと同じ動き） ----------
        private void UpdateOpen(float dt)
        {
            if (openTimer >= 0f)
            {
                openTimer += dt;
                float k = openTimer / Mathf.Max(0.05f, openPopSeconds);
                float s = k < 0.6f ? Mathf.Lerp(0.85f, 1.05f, k / 0.6f) : Mathf.Lerp(1.05f, 1f, (k - 0.6f) / 0.4f);
                if (k >= 1f) { s = 1f; openTimer = -1f; }
                transform.localScale = Vector3.one * s;
            }
        }

        // ---------- 枠を走る光（SoundPanelFXと同じ動き） ----------
        private Rect FrameRectLocal(Vector2 inset)
        {
            RectTransform src = frameRect != null ? frameRect : (RectTransform)transform;
            Rect r = src.rect;
            Vector2 min = transform.InverseTransformPoint(src.TransformPoint(r.min));
            Vector2 max = transform.InverseTransformPoint(src.TransformPoint(r.max));
            return Rect.MinMaxRect(min.x + inset.x, min.y + inset.y, max.x - inset.x, max.y - inset.y);
        }

        private void UpdateRunner(float dt)
        {
            if (runnerDots == null || runnerDots.Length == 0) return;
            Rect r = FrameRectLocal(runnerInset);
            float perim = 2f * (r.width + r.height);
            if (perim <= 1f) return;
            runnerDist = Mathf.Repeat(runnerDist + runnerSpeed * dt, perim);
            for (int i = 0; i < runnerDots.Length; i++)
            {
                if (runnerDots[i] == null) continue;
                runnerDots[i].anchoredPosition = PointOnRect(r, Mathf.Repeat(runnerDist - i * runnerSpacing, perim));
            }
        }

        private static Vector2 PointOnRect(Rect r, float d)
        {
            if (d < r.width) return new Vector2(r.xMin + d, r.yMax);
            d -= r.width;
            if (d < r.height) return new Vector2(r.xMax, r.yMax - d);
            d -= r.height;
            if (d < r.width) return new Vector2(r.xMax - d, r.yMin);
            d -= r.width;
            return new Vector2(r.xMin, r.yMin + d);
        }
    }
}
