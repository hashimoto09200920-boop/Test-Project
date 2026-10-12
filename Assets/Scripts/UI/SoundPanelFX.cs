using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// SOUND画面（Titleと05_Game中断メニューのSoundPanel、チュートリアルも05_Gameと共通）を派手にする演出。
/// ①ネオンのゲージ（UIHorizontalGradient＋先端の光） ③つまみ（光る玉＋♪/スピーカー、ドラッグ中の拡大・火花）
/// ④数値の弾み・MUTE/MAX ⑤音波アイコン ⑥イコライザー（実際に鳴っている音に反応） ⑦SEの試し鳴らし＋波紋
/// ⑧開く時のポップ＋光の帯（ロゴのまたたきは既存のTitleNeonEffect） ⑨背景の音符 ⑩枠を走る光。
/// 部品はメニュー「Tools/サウンド画面/演出を作成（Title・05_Game）」でPlay前に生成・参照設定する。
/// 中断メニューはTime.timeScale=0で開くため、すべて実時間（unscaled）で動かす
/// </summary>
public class SoundPanelFX : MonoBehaviour
{
    [Header("参照（メニューで自動設定）")]
    [SerializeField] private Slider bgmSlider;
    [SerializeField] private Slider seSlider;
    [SerializeField] private TextMeshProUGUI bgmText;
    [SerializeField] private TextMeshProUGUI seText;
    [SerializeField] private RectTransform frameRect;

    [Header("① ゲージ先端の光")]
    [SerializeField] private Image bgmFillTip;
    [SerializeField] private Image seFillTip;
    [SerializeField] private float fillTipPulseSpeed = 4f;

    [Header("③ つまみ")]
    [SerializeField] private RectTransform bgmHandle;
    [SerializeField] private RectTransform seHandle;
    [SerializeField] private float handleDragScale = 1.25f;
    [SerializeField] private float handlePulseSpeed = 7f;
    [SerializeField] private Image[] sparks;
    [SerializeField] private float sparkLife = 0.45f;
    [Tooltip("ドラッグ中、値がこの量動くごとに火花を1つ出す")]
    [SerializeField] private float sparkPerValue = 0.025f;
    [SerializeField] private Color bgmSparkColor = new Color(0.4f, 0.95f, 1f, 1f);
    [SerializeField] private Color seSparkColor = new Color(1f, 0.8f, 0.3f, 1f);

    [Header("④ 数値")]
    [SerializeField] private Color muteColor = new Color(0.55f, 0.58f, 0.65f, 1f);
    [SerializeField] private Color maxColorA = new Color(0.4f, 1f, 1f, 1f);
    [SerializeField] private Color maxColorB = new Color(1f, 0.5f, 1f, 1f);
    [SerializeField] private float valuePunchScale = 1.22f;
    [SerializeField] private float valuePunchSeconds = 0.2f;

    [Header("⑤ 音波アイコン（音量に合わせて0〜3本）")]
    [SerializeField] private Image[] bgmWaves;
    [SerializeField] private Image[] seWaves;
    [SerializeField] private Color waveColor = new Color(0.6f, 0.95f, 1f, 1f);

    [Header("⑥ イコライザー（実際に鳴っている音に反応。音が取れない時はゆっくり揺れる）")]
    [SerializeField] private RectTransform[] eqBars;
    [SerializeField] private float eqMinHeight = 3f;
    [SerializeField] private float eqMaxHeight = 34f;
    [SerializeField] private float eqGain = 60f;
    [SerializeField] private float eqSmoothing = 12f;

    [Header("⑦ SEの試し鳴らし・波紋")]
    [SerializeField] private AudioClip sePreviewClip;
    [SerializeField, Range(0f, 1f)] private float sePreviewVolume = 1f;
    [SerializeField] private Image[] ripples;
    [SerializeField] private float rippleSeconds = 0.5f;
    [SerializeField] private float rippleMaxSize = 150f;

    [Header("⑧ 開く時")]
    [SerializeField] private float openPopSeconds = 0.28f;
    [SerializeField] private RectTransform sweepBar;
    [SerializeField] private Image sweepImage;
    [SerializeField] private float sweepSeconds = 0.55f;

    [Header("⑨ 背景の音符")]
    [SerializeField] private Image[] notes;
    [SerializeField] private Sprite[] noteSprites;
    [SerializeField, Range(0f, 1f)] private float noteMaxAlpha = 0.3f;
    [SerializeField] private Vector2 noteRiseSpeed = new Vector2(35f, 70f);
    [SerializeField] private Vector2 noteLife = new Vector2(3f, 5.5f);

    [Header("⑩ 枠を走る光")]
    [SerializeField] private RectTransform[] runnerDots;
    [Tooltip("枠（SoundBg）の外周から内側へのずらし量。画像の見た目の枠線に合わせて調整")]
    [SerializeField] private Vector2 runnerInset = new Vector2(80f, 55f);
    [SerializeField] private float runnerSpeed = 520f;
    [SerializeField] private float runnerSpacing = 14f;

    // ---- 状態 ----
    private float lastBgm = -1f, lastSe = -1f;
    private bool synced;
    private bool bgmDown, seDown;
    private float bgmSparkAcc, seSparkAcc;
    private float bgmPunch = -1f, sePunch = -1f;
    private Color bgmTextBase = Color.white, seTextBase = Color.white;
    private float openTimer = -1f;
    private float sweepTimer = -1f;
    private float runnerDist;
    private AudioSource previewSource;
    private readonly float[] spectrum = new float[64];
    private float[] eqLevels;

    private struct Particle { public float age, life; public Vector2 pos, vel; }
    private Particle[] sparkState, noteState;
    private float[] rippleAge;
    private float noteSpawnAcc;

    private void Awake()
    {
        if (bgmText != null) bgmTextBase = bgmText.color;
        if (seText != null) seTextBase = seText.color;
        previewSource = gameObject.AddComponent<AudioSource>();
        previewSource.playOnAwake = false;
        previewSource.spatialBlend = 0f;
        previewSource.ignoreListenerPause = true; // 中断中でも試し鳴らしが聞こえるように
        sparkState = new Particle[sparks != null ? sparks.Length : 0];
        noteState = new Particle[notes != null ? notes.Length : 0];
        rippleAge = new float[ripples != null ? ripples.Length : 0];
        eqLevels = new float[eqBars != null ? eqBars.Length : 0];
    }

    private void OnEnable()
    {
        synced = false; // 開いた直後にスライダーへ現在値が入る分は「操作」として扱わない
        bgmDown = seDown = false;
        openTimer = 0f;
        sweepTimer = 0f;
        for (int i = 0; i < sparkState?.Length; i++) { sparkState[i].age = sparkState[i].life = 0f; if (sparks[i] != null) sparks[i].enabled = false; }
        for (int i = 0; i < rippleAge?.Length; i++) { rippleAge[i] = -1f; if (ripples[i] != null) ripples[i].enabled = false; }
        for (int i = 0; i < noteState?.Length; i++) { noteState[i].life = 0f; if (notes[i] != null) notes[i].enabled = false; }
        // 背景の音符は最初から何個か浮かべておく
        for (int i = 0; i < noteState?.Length; i += 2) SpawnNote(i, Random.Range(0f, 0.6f));
        transform.localScale = Vector3.one * 0.85f;
    }

    private void OnDisable()
    {
        transform.localScale = Vector3.one;
    }

    /// <summary>SoundSliderPointerRelayから：スライダーを押した／離した</summary>
    public void NotifySliderPointer(bool isSE, bool down)
    {
        if (isSE) seDown = down; else bgmDown = down;
        if (down) return;
        SpawnRipple(isSE ? seHandle : bgmHandle, isSE ? seSparkColor : bgmSparkColor);
        if (isSE && sePreviewClip != null && previewSource != null)
        {
            float vol = SoundSettingsManager.Instance != null ? SoundSettingsManager.Instance.SEVolume : (seSlider != null ? seSlider.value : 1f);
            if (vol > 0.001f) previewSource.PlayOneShot(sePreviewClip, vol * sePreviewVolume);
        }
    }

    private void LateUpdate()
    {
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
        float now = Time.unscaledTime;

        float bgm = bgmSlider != null ? bgmSlider.value : 0f;
        float se = seSlider != null ? seSlider.value : 0f;
        if (!synced)
        {
            synced = true;
            lastBgm = bgm; lastSe = se;
        }
        else
        {
            TrackChange(bgm, ref lastBgm, ref bgmPunch, ref bgmSparkAcc, bgmDown, bgmHandle, bgmSparkColor);
            TrackChange(se, ref lastSe, ref sePunch, ref seSparkAcc, seDown, seHandle, seSparkColor);
        }

        UpdateOpen(dt);
        UpdateValueText(bgmText, bgm, bgmTextBase, ref bgmPunch, dt, now);
        UpdateValueText(seText, se, seTextBase, ref sePunch, dt, now);
        UpdateHandle(bgmHandle, bgmDown, now);
        UpdateHandle(seHandle, seDown, now);
        UpdateFillTip(bgmFillTip, bgm, now);
        UpdateFillTip(seFillTip, se, now + 1.3f);
        UpdateWaves(bgmWaves, bgm, now);
        UpdateWaves(seWaves, se, now + 0.7f);
        UpdateEqualizer(bgm, dt, now);
        UpdateSparks(dt);
        UpdateRipples(dt);
        UpdateNotes(dt, now);
        UpdateRunner(dt);
    }

    // ---------- ④ 数値 ----------
    private void TrackChange(float v, ref float last, ref float punch, ref float sparkAcc, bool down, RectTransform handle, Color sparkColor)
    {
        float diff = Mathf.Abs(v - last);
        if (diff < 0.0001f) return;
        punch = 0f;
        if (down)
        {
            sparkAcc += diff;
            int n = 0;
            while (sparkAcc >= sparkPerValue && n < 3) { sparkAcc -= sparkPerValue; SpawnSpark(handle, sparkColor); n++; }
        }
        last = v;
    }

    private void UpdateValueText(TextMeshProUGUI t, float v, Color baseColor, ref float punch, float dt, float now)
    {
        if (t == null) return;
        int pct = Mathf.RoundToInt(v * 100f);
        // ★元の表示（": 50%"）はTitleMenu/PauseMenuUIが書く。0%と100%の時だけここで差し替える
        if (pct <= 0) { t.text = ": MUTE"; t.color = muteColor; }
        else if (pct >= 100) { t.text = ": MAX"; t.color = Color.Lerp(maxColorA, maxColorB, 0.5f + 0.5f * Mathf.Sin(now * 5f)); }
        else { t.text = $": {pct}%"; t.color = baseColor; }

        float s = 1f;
        if (punch >= 0f)
        {
            punch += dt;
            float k = punch / Mathf.Max(0.01f, valuePunchSeconds);
            if (k >= 1f) punch = -1f;
            else s = Mathf.Lerp(valuePunchScale, 1f, k);
        }
        t.rectTransform.localScale = Vector3.one * s;
    }

    // ---------- ③ つまみ ----------
    private void UpdateHandle(RectTransform h, bool down, float now)
    {
        if (h == null) return;
        float target = down ? handleDragScale * (1f + 0.05f * Mathf.Sin(now * handlePulseSpeed)) : 1f;
        float s = Mathf.Lerp(h.localScale.x, target, 1f - Mathf.Exp(-18f * Time.unscaledDeltaTime));
        h.localScale = Vector3.one * s;
    }

    private void SpawnSpark(RectTransform from, Color c)
    {
        if (from == null || sparks == null) return;
        for (int i = 0; i < sparks.Length; i++)
        {
            if (sparks[i] == null || sparkState[i].age < sparkState[i].life) continue;
            Vector2 p = transform.InverseTransformPoint(from.position);
            float ang = Random.Range(0f, Mathf.PI * 2f);
            sparkState[i] = new Particle { age = 0f, life = sparkLife * Random.Range(0.7f, 1.2f), pos = p, vel = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * Random.Range(60f, 160f) + Vector2.up * 40f };
            sparks[i].color = c;
            sparks[i].enabled = true;
            return;
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
            p.vel *= 1f - 3f * dt;
            p.pos += p.vel * dt;
            float k = p.age / p.life;
            var rt = sparks[i].rectTransform;
            rt.anchoredPosition = p.pos;
            rt.localScale = Vector3.one * Mathf.Lerp(1f, 0.2f, k);
            Color c = sparks[i].color; c.a = 1f - k; sparks[i].color = c;
        }
    }

    // ---------- ① 先端の光 ----------
    private void UpdateFillTip(Image tip, float v, float now)
    {
        if (tip == null) return;
        bool on = v > 0.005f;
        if (tip.enabled != on) tip.enabled = on;
        if (!on) return;
        Color c = tip.color; c.a = 0.65f + 0.35f * Mathf.Sin(now * fillTipPulseSpeed); tip.color = c;
    }

    // ---------- ⑤ 音波 ----------
    private void UpdateWaves(Image[] waves, float v, float now)
    {
        if (waves == null) return;
        int count = v <= 0.001f ? 0 : Mathf.Clamp(Mathf.CeilToInt(v * waves.Length), 1, waves.Length);
        for (int i = 0; i < waves.Length; i++)
        {
            if (waves[i] == null) continue;
            bool on = i < count;
            if (waves[i].enabled != on) waves[i].enabled = on;
            if (!on) continue;
            Color c = waveColor;
            c.a = 0.55f + 0.45f * Mathf.Clamp01(Mathf.Sin(now * 4f - i * 0.9f) * 0.5f + 0.5f);
            waves[i].color = c;
        }
    }

    // ---------- ⑥ イコライザー ----------
    private void UpdateEqualizer(float bgmVolume, float dt, float now)
    {
        if (eqBars == null || eqBars.Length == 0) return;
        float sum = 0f;
        try { AudioListener.GetSpectrumData(spectrum, 0, FFTWindow.BlackmanHarris); }
        catch { System.Array.Clear(spectrum, 0, spectrum.Length); }
        for (int i = 0; i < spectrum.Length; i++) sum += spectrum[i];
        bool hasSignal = sum > 0.0005f;

        int n = eqBars.Length;
        for (int b = 0; b < n; b++)
        {
            float level;
            if (hasSignal)
            {
                // 低音〜高音を対数的にまとめる（バーbの担当範囲）
                int from = Mathf.FloorToInt(Mathf.Pow(2f, b * 6f / n)) - 1;
                int to = Mathf.Max(from + 1, Mathf.FloorToInt(Mathf.Pow(2f, (b + 1) * 6f / n)) - 1);
                float s = 0f;
                for (int k = from; k < to && k < spectrum.Length; k++) s += spectrum[k];
                level = Mathf.Clamp01(Mathf.Sqrt(s * eqGain * (1f + b * 0.35f)));
            }
            else
            {
                // 音が取れない時（中断中でBGMが止まっている等）は、音量に合わせてゆっくり揺れる
                level = bgmVolume * (0.25f + 0.2f * Mathf.Sin(now * 2.2f + b * 0.8f));
            }
            eqLevels[b] = Mathf.Lerp(eqLevels[b], level, 1f - Mathf.Exp(-eqSmoothing * dt));
            var rt = eqBars[b];
            if (rt == null) continue;
            rt.sizeDelta = new Vector2(rt.sizeDelta.x, Mathf.Lerp(eqMinHeight, eqMaxHeight, eqLevels[b]));
        }
    }

    // ---------- ⑦ 波紋 ----------
    private void SpawnRipple(RectTransform at, Color c)
    {
        if (at == null || ripples == null) return;
        for (int i = 0; i < ripples.Length; i++)
        {
            if (ripples[i] == null || rippleAge[i] >= 0f) continue;
            rippleAge[i] = 0f;
            ripples[i].rectTransform.anchoredPosition = transform.InverseTransformPoint(at.position);
            ripples[i].color = c;
            ripples[i].enabled = true;
            return;
        }
    }

    private void UpdateRipples(float dt)
    {
        for (int i = 0; ripples != null && i < ripples.Length; i++)
        {
            if (ripples[i] == null || rippleAge[i] < 0f) continue;
            rippleAge[i] += dt;
            float k = rippleAge[i] / Mathf.Max(0.05f, rippleSeconds);
            if (k >= 1f) { rippleAge[i] = -1f; ripples[i].enabled = false; continue; }
            float size = Mathf.Lerp(30f, rippleMaxSize, 1f - (1f - k) * (1f - k));
            ripples[i].rectTransform.sizeDelta = new Vector2(size, size);
            Color c = ripples[i].color; c.a = 1f - k; ripples[i].color = c;
        }
    }

    // ---------- ⑧ 開く時 ----------
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
        if (sweepBar != null)
        {
            if (sweepTimer >= 0f)
            {
                sweepTimer += dt;
                float k = sweepTimer / Mathf.Max(0.05f, sweepSeconds);
                if (k >= 1f) { sweepTimer = -1f; sweepBar.gameObject.SetActive(false); }
                else
                {
                    if (!sweepBar.gameObject.activeSelf) sweepBar.gameObject.SetActive(true);
                    Rect r = frameRect != null ? frameRect.rect : ((RectTransform)transform).rect;
                    Vector2 c = frameRect != null ? (Vector2)transform.InverseTransformPoint(frameRect.TransformPoint(r.center)) : r.center;
                    sweepBar.anchoredPosition = new Vector2(c.x, Mathf.Lerp(c.y + r.height * 0.42f, c.y - r.height * 0.42f, k));
                    sweepBar.sizeDelta = new Vector2(r.width * 0.72f, sweepBar.sizeDelta.y);
                    if (sweepImage != null) { Color col = sweepImage.color; col.a = Mathf.Sin(k * Mathf.PI) * 0.55f; sweepImage.color = col; }
                }
            }
            else if (sweepBar.gameObject.activeSelf) sweepBar.gameObject.SetActive(false);
        }
    }

    // ---------- ⑨ 音符 ----------
    private void SpawnNote(int i, float startAge01)
    {
        if (notes[i] == null) return;
        Rect r = InnerRect(new Vector2(160f, 140f));
        float life = Random.Range(noteLife.x, noteLife.y);
        noteState[i] = new Particle
        {
            life = life,
            age = life * startAge01,
            pos = new Vector2(Random.Range(r.xMin, r.xMax), r.yMin + (r.height * startAge01 * 0.8f)),
            vel = new Vector2(Random.Range(0f, Mathf.PI * 2f), Random.Range(noteRiseSpeed.x, noteRiseSpeed.y)), // x=揺れの位相, y=上昇速度
        };
        if (noteSprites != null && noteSprites.Length > 0) notes[i].sprite = noteSprites[Random.Range(0, noteSprites.Length)];
        notes[i].enabled = true;
    }

    private void UpdateNotes(float dt, float now)
    {
        if (notes == null) return;
        noteSpawnAcc += dt;
        for (int i = 0; i < notes.Length; i++)
        {
            if (notes[i] == null) continue;
            ref Particle p = ref noteState[i];
            if (p.life <= 0f || p.age >= p.life)
            {
                notes[i].enabled = false;
                if (noteSpawnAcc > 0.45f) { noteSpawnAcc = 0f; SpawnNote(i, 0f); }
                continue;
            }
            p.age += dt;
            p.pos.y += p.vel.y * dt;
            float k = p.age / p.life;
            var rt = notes[i].rectTransform;
            rt.anchoredPosition = new Vector2(p.pos.x + Mathf.Sin(now * 1.3f + p.vel.x) * 14f, p.pos.y);
            rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(now * 1.7f + p.vel.x) * 12f);
            Color c = notes[i].color; c.a = noteMaxAlpha * Mathf.Clamp01(Mathf.Min(k / 0.2f, (1f - k) / 0.3f)); notes[i].color = c;
        }
    }

    // ---------- ⑩ 枠を走る光 ----------
    private Rect InnerRect(Vector2 inset)
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
        Rect r = InnerRect(runnerInset);
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
