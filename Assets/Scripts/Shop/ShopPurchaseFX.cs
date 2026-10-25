using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Shop
{
    /// <summary>
    /// 課金ショップ（07_Shop）の購入演出（2026/10/24）。部品はメニュー「Tools/ショップ/07_Shop に購入演出を組み込む」で作る。
    /// ・虹の雫：買ったパネルのアイコンから、雫が1個ずつ流れるように右上の所持数へ吸い込まれ、1個届くたびに所持数が +1 して弾む
    ///   （確認のポップアップは出さない。ポップアップは AreaSelect の初回報酬だけの演出）
    /// ・プレミアムパス：パネルのチケットがスタミナの表示へ吸い込まれ、着いたら金色の輪が2重に広がって光の粒がはじけ、
    ///   「数/最大」がくるっと回りながら「∞」に変わる。その後しばらく虹色に光って落ち着く
    /// 画面全体を光らせる演出は入れない（目に負担がかかるため）。毎フレームの処理は演出中だけ。動きは全て実時間。
    /// </summary>
    public class ShopPurchaseFX : MonoBehaviour
    {
        [Header("所持数の表示")]
        [SerializeField] private InfiniteStoneHUD stoneHud;
        [SerializeField] private StaminaHUD staminaHud;

        [Header("演出の部品")]
        [Tooltip("飛んでいく雫・チケット（×10 でも同時に飛ぶのは数個なので、数個を使い回す）")]
        [SerializeField] private Image[] flyers = new Image[0];
        [Tooltip("スタミナの表示で弾ける輪・光の粒（プレミアムパス）")]
        [SerializeField] private Image[] burstRings = new Image[0];
        [SerializeField] private Image[] burstSparks = new Image[0];
        [Tooltip("スタミナの表示を1回横切る光の筋（プレミアムパス）")]
        [SerializeField] private Image shine;

        [Header("吸い込まれる動き")]
        [Tooltip("1個が所持数に届くまでの時間（秒）")]
        [SerializeField] private float flySeconds = 0.6f;
        [Tooltip("弧の高さ（上へふくらむ量）")]
        [SerializeField] private float flyArcHeight = 90f;
        [Tooltip("虹の雫を複数買った時、1個ずつ飛ばす間隔（秒）")]
        [SerializeField] private float flyInterval = 0.25f;
        [Tooltip("飛び始めの大きさ（パネルのアイコンに対する倍率）")]
        [Range(0.2f, 1f)] [SerializeField] private float flyStartScale = 0.6f;
        [Tooltip("飛びながら回る角度（度）")]
        [SerializeField] private float flySpin = 25f;
        [SerializeField] private float arrivePunchScale = 1.35f;
        [SerializeField] private float arrivePunchSeconds = 0.25f;

        [Header("プレミアムパス：スタミナが∞になる演出")]
        [SerializeField] private Color gold = new Color(1f, 0.82f, 0.3f, 1f);
        [SerializeField] private float burstRingSeconds = 0.7f;
        [Tooltip("2つ目の輪が広がり始めるまでの遅れ（秒）")]
        [SerializeField] private float burstRingDelay = 0.15f;
        [SerializeField] private float burstRingSize = 90f;
        [SerializeField] private float burstRingEndScale = 3.2f;
        [SerializeField] private float burstSparkDistance = 130f;
        [SerializeField] private float burstSparkSize = 24f;
        [Tooltip("「数/最大」がくるっと回って「∞」に変わる時間（秒）")]
        [SerializeField] private float spinSeconds = 0.45f;
        [SerializeField] private float revealPunchScale = 1.6f;
        [Tooltip("∞ に変わった後、虹色に光り続ける時間（秒）")]
        [SerializeField] private float rainbowGlowSeconds = 2f;
        [SerializeField] private float shineSeconds = 0.5f;

        [Header("SE（無ければ鳴らさない）")]
        [SerializeField] private AudioClip arriveSE;
        [SerializeField] private AudioClip unlimitedSE;

        /// <summary>演出中（最後の1個が届いて落ち着くまで）。この間はショップのパネル・EXIT を押せない</summary>
        public bool IsPlaying { get; private set; }

        private AudioSource audioSource;
        private RectTransform Root => (RectTransform)transform;

        private void Awake()
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null) { audioSource = gameObject.AddComponent<AudioSource>(); audioSource.playOnAwake = false; }
            HideAll();
        }

        /// <summary>虹の雫を count 個買った時。from＝買ったパネルのアイコン（ここから飛ぶ）、sprite＝飛ぶ雫の画像（×3・×10 でも雫1個の画像）</summary>
        public void PlayStone(int count, Image from, Sprite sprite)
        {
            Restart();
            count = Mathf.Max(1, count);
            int before = Mathf.Max(0, InfiniteStoneManager.GetPersistentCount() - count);
            if (stoneHud != null) stoneHud.HoldDisplay(before); // 雫が届くまで、買う前の数のまま
            StartCoroutine(Fly(from, sprite, count, stoneHud != null ? stoneHud.IconRect : null,
                n => { if (stoneHud != null) stoneHud.ShowStep(before + n, arrivePunchScale, arrivePunchSeconds, n >= count); },
                null));
        }

        /// <summary>プレミアムパスを買った時。from＝プレミアムパスのパネルのアイコン</summary>
        public void PlayPremium(Image from)
        {
            Restart();
            if (staminaHud != null) staminaHud.HoldDisplay(StaminaHUD.CountLabel()); // チケットが届くまで「数/最大」のまま
            StartCoroutine(Fly(from, null, 1, staminaHud != null ? staminaHud.IconRect : null, null, UnlimitedReveal));
        }

        private void Restart()
        {
            if (!IsPlaying) return;
            StopAllCoroutines();
            HideAll();
            if (stoneHud != null) stoneHud.ReleaseHold();
            if (staminaHud != null) staminaHud.ReleaseHold();
        }

        // パネルのアイコンから target へ、count 個を間隔をあけて1個ずつ飛ばす。届くたびに onArrive(届いた数)、全部届いたら after。
        // sprite＝飛ぶ画像（null ならパネルのアイコンと同じ画像）
        private IEnumerator Fly(Image from, Sprite sprite, int count, RectTransform target, System.Action<int> onArrive, System.Func<IEnumerator> after)
        {
            IsPlaying = true;
            transform.SetAsLastSibling();
            Vector2 start = from != null ? (Vector2)Root.InverseTransformPoint(from.rectTransform.position) : Vector2.zero;
            float startSize = 96f;
            if (from != null)
            {
                var c = new Vector3[4]; from.rectTransform.GetWorldCorners(c);
                startSize = Root.InverseTransformVector(c[1] - c[0]).magnitude * flyStartScale;
            }
            Vector2 end = start + new Vector2(0f, 400f);
            float endScale = 0.3f;
            if (target != null)
            {
                end = Root.InverseTransformPoint(target.position);
                var c = new Vector3[4]; target.GetWorldCorners(c);
                float h = Root.InverseTransformVector(c[1] - c[0]).magnitude;
                endScale = startSize > 0f ? Mathf.Clamp(h / startSize, 0.05f, 1f) : 0.3f;
            }
            if (sprite == null && from != null) sprite = from.sprite;

            int launched = 0, arrived = 0;
            float next = 0f;
            var active = new List<(Image img, float time)>();
            while (arrived < count)
            {
                float dt = Time.unscaledDeltaTime;
                next -= dt;
                if (launched < count && next <= 0f)
                {
                    var img = FreeFlyer();
                    if (img != null)
                    {
                        img.sprite = sprite;
                        img.preserveAspect = true;
                        img.rectTransform.sizeDelta = Vector2.one * startSize;
                        img.rectTransform.anchoredPosition = start;
                        img.rectTransform.localScale = Vector3.one;
                        img.rectTransform.localRotation = Quaternion.identity;
                        img.color = Color.white;
                        img.gameObject.SetActive(true);
                        active.Add((img, 0f));
                        launched++;
                        next = flyInterval;
                    }
                }
                for (int i = active.Count - 1; i >= 0; i--)
                {
                    var f = active[i];
                    f.time += dt;
                    float k = flySeconds > 0f ? Mathf.Clamp01(f.time / flySeconds) : 1f;
                    // 最初はゆっくり、所持数に近づくほど速く（吸い込まれるように）
                    float e = k * k;
                    float side = Mathf.Sin(k * Mathf.PI);
                    f.img.rectTransform.anchoredPosition = Vector2.Lerp(start, end, e) + new Vector2(0f, side * flyArcHeight);
                    f.img.rectTransform.localScale = Vector3.one * Mathf.Lerp(1f, endScale, e);
                    f.img.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -flySpin * k);
                    active[i] = f;
                    if (k >= 1f)
                    {
                        f.img.gameObject.SetActive(false);
                        active.RemoveAt(i);
                        arrived++;
                        Play(arriveSE);
                        onArrive?.Invoke(arrived);
                    }
                }
                yield return null;
            }
            if (after != null) yield return StartCoroutine(after());
            HideAll();
            IsPlaying = false;
        }

        private Image FreeFlyer()
        {
            foreach (var f in flyers) if (f != null && !f.gameObject.activeSelf) return f;
            return null;
        }

        // ───────── プレミアムパス：スタミナが∞になる ─────────
        private IEnumerator UnlimitedReveal()
        {
            Play(unlimitedSE);
            var target = staminaHud != null ? staminaHud.IconRect : null;
            Vector2 center = target != null ? (Vector2)Root.InverseTransformPoint(target.position) : Vector2.zero;
            var text = staminaHud != null ? staminaHud.CountText : null;
            var iconImg = staminaHud != null ? staminaHud.Icon : null;
            Vector3 textBase = text != null ? text.transform.localScale : Vector3.one;
            Quaternion textRot = text != null ? text.transform.localRotation : Quaternion.identity;
            Color textColor = text != null ? text.color : Color.white;
            Color iconColor = iconImg != null ? iconImg.color : Color.white;
            Vector3 iconBase = iconImg != null ? iconImg.transform.localScale : Vector3.one;

            for (int i = 0; i < burstRings.Length; i++)
            {
                if (burstRings[i] == null) continue;
                var rt = burstRings[i].rectTransform;
                rt.sizeDelta = Vector2.one * burstRingSize; rt.anchoredPosition = center; rt.localScale = Vector3.zero;
                burstRings[i].gameObject.SetActive(true);
            }
            var dirs = new Vector2[burstSparks.Length];
            for (int i = 0; i < burstSparks.Length; i++)
            {
                if (burstSparks[i] == null) continue;
                float a = (i / (float)burstSparks.Length) * Mathf.PI * 2f + Random.Range(-0.15f, 0.15f);
                dirs[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Random.Range(0.75f, 1f);
                var rt = burstSparks[i].rectTransform;
                rt.sizeDelta = Vector2.one * burstSparkSize; rt.anchoredPosition = center;
                burstSparks[i].gameObject.SetActive(true);
            }
            if (shine != null) shine.gameObject.SetActive(false);

            bool revealed = false;
            float total = Mathf.Max(burstRingSeconds + burstRingDelay * Mathf.Max(0, burstRings.Length - 1), spinSeconds) + rainbowGlowSeconds;
            float t = 0f;
            while (t < total)
            {
                t += Time.unscaledDeltaTime;
                // 金色の輪（2重）
                for (int i = 0; i < burstRings.Length; i++)
                {
                    if (burstRings[i] == null || !burstRings[i].gameObject.activeSelf || t < burstRingDelay * i) continue;
                    float rk = burstRingSeconds > 0f ? Mathf.Clamp01((t - burstRingDelay * i) / burstRingSeconds) : 1f;
                    burstRings[i].rectTransform.localScale = Vector3.one * Mathf.Lerp(0.3f, burstRingEndScale, 1f - (1f - rk) * (1f - rk));
                    var c = Color.Lerp(gold, Color.white, i * 0.35f); c.a = 0.9f * (1f - rk);
                    burstRings[i].color = c;
                    if (rk >= 1f) burstRings[i].gameObject.SetActive(false);
                }
                // 光の粒
                float sk = burstRingSeconds > 0f ? Mathf.Clamp01(t / burstRingSeconds) : 1f;
                float se = 1f - (1f - sk) * (1f - sk);
                for (int i = 0; i < burstSparks.Length; i++)
                {
                    if (burstSparks[i] == null || !burstSparks[i].gameObject.activeSelf) continue;
                    burstSparks[i].rectTransform.anchoredPosition = center + dirs[i] * burstSparkDistance * se;
                    burstSparks[i].rectTransform.localScale = Vector3.one * (1f - 0.6f * sk);
                    burstSparks[i].rectTransform.localRotation = Quaternion.Euler(0f, 0f, sk * 200f * (i % 2 == 0 ? 1f : -1f));
                    var c = Color.HSVToRGB(Mathf.Repeat(i / (float)burstSparks.Length + t, 1f), 0.35f, 1f); c.a = 1f - sk;
                    burstSparks[i].color = c;
                    if (sk >= 1f) burstSparks[i].gameObject.SetActive(false);
                }
                // 「数/最大」がくるっと回りながら小さくなり、半分で「∞」に変わって大きく弾む
                if (text != null)
                {
                    float k = spinSeconds > 0f ? Mathf.Clamp01(t / spinSeconds) : 1f;
                    if (!revealed && k >= 0.5f) { revealed = true; if (staminaHud != null) staminaHud.ReleaseHold(); }
                    float scale = k < 0.5f ? Mathf.Lerp(1f, 0.2f, k / 0.5f)
                        : k < 1f ? Mathf.Lerp(0.2f, revealPunchScale, (k - 0.5f) / 0.5f)
                        : Mathf.Lerp(revealPunchScale, 1f, Mathf.Clamp01((t - spinSeconds) / 0.25f));
                    text.transform.localScale = textBase * scale;
                    text.transform.localRotation = textRot * Quaternion.Euler(0f, 0f, -360f * k);
                }
                // ∞ に変わった後：アイコンと文字が虹色に光り、ふわっと弾み続けて落ち着く。途中で光の筋が1回横切る
                if (revealed)
                {
                    float fade = 1f - Mathf.Clamp01((t - spinSeconds) / Mathf.Max(0.01f, rainbowGlowSeconds));
                    Color rb = Color.HSVToRGB(Mathf.Repeat(t * 1.2f, 1f), 0.6f, 1f);
                    if (text != null) text.color = Color.Lerp(textColor, rb, fade);
                    if (iconImg != null)
                    {
                        iconImg.color = Color.Lerp(iconColor, rb, fade * 0.8f);
                        iconImg.transform.localScale = iconBase * (1f + 0.15f * fade * Mathf.Abs(Mathf.Sin(t * 6f)));
                    }
                    if (shine != null)
                    {
                        float sh = shineSeconds > 0f ? (t - spinSeconds) / shineSeconds : 1f;
                        if (sh >= 0f && sh <= 1f)
                        {
                            if (!shine.gameObject.activeSelf) shine.gameObject.SetActive(true);
                            shine.rectTransform.anchoredPosition = Vector2.Lerp(center + new Vector2(-60f, 0f), center + new Vector2(200f, 0f), sh);
                            var c = Color.white; c.a = 0.75f * Mathf.Sin(sh * Mathf.PI);
                            shine.color = c;
                        }
                        else if (shine.gameObject.activeSelf) shine.gameObject.SetActive(false);
                    }
                }
                yield return null;
            }
            if (staminaHud != null && !revealed) staminaHud.ReleaseHold();
            if (text != null) { text.transform.localScale = textBase; text.transform.localRotation = textRot; text.color = textColor; }
            if (iconImg != null) { iconImg.color = iconColor; iconImg.transform.localScale = iconBase; }
        }

        private void HideAll()
        {
            foreach (var f in flyers) if (f != null) f.gameObject.SetActive(false);
            foreach (var r in burstRings) if (r != null) r.gameObject.SetActive(false);
            foreach (var s in burstSparks) if (s != null) s.gameObject.SetActive(false);
            if (shine != null) shine.gameObject.SetActive(false);
        }

        private void Play(AudioClip clip)
        {
            if (clip == null || audioSource == null) return;
            float vol = SoundSettingsManager.Instance != null ? SoundSettingsManager.Instance.SEVolume : 1f;
            audioSource.PlayOneShot(clip, vol);
        }

        private void OnDisable()
        {
            // 画面を出る時などに途中で止まった場合：所持数・スタミナの表示を元に戻す
            if (!IsPlaying) return;
            IsPlaying = false;
            if (stoneHud != null) stoneHud.ReleaseHold();
            if (staminaHud != null) staminaHud.ReleaseHold();
        }
    }
}
