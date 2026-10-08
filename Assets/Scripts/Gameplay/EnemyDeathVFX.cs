using System.Collections;
using UnityEngine;

/// <summary>撃破エフェクトの色テーマ（EnemyDataのDeathVfxConfig.themeで選ぶ。並びはEnemyDeathVFX.themesの並びと一致させる）</summary>
public enum DeathVfxTheme
{
    Fire,        // ① 炎
    Crimson,     // ② 紅蓮
    NeonCyan,    // ③ ネオン水色
    NeonPurple,  // ④ ネオン紫
    NeonRainbow, // ⑤ ネオン虹（NeonDancer専用・特別に派手）
    Toxic,       // ⑥ 毒
    Mech,        // ⑦ 機械
    Ruins,       // ⑧ 遺跡
    Shadow,      // ⑨ 闇
    Moonlight,   // ⑩ 月光
}

/// <summary>
/// 敵の撃破エフェクト（VFX_EnemyDeath.prefab のルートに付ける。プレハブはメニュー「Tools/撃破エフェクト/…」で作成）。
/// 層を重ねて「爆発の流れ」を作る：閃光 → 火球 → 火花・衝撃波（2重）・破片 → 煙・残り火（余韻）。
/// ・各層の形・寿命・動きは子のParticle Systemの設定（Play前にInspectorで調整可）、数・遅れ・色はこのコンポーネントで調整する
/// ・色はテーマ（10種）で一括変更。エネミーごとのテーマはEnemyDataのDeathVfxConfig.theme
/// ・爆発の大きさはDeathVfxConfig.ringScale（従来の撃破リングの大きさ。雑魚3〜4／中型5／ボス8〜9）を基準に自動で変える
/// ・火花の数・速さ・大きさ・寿命・重力はDeathVfxConfigの従来の値を引き継ぐ
/// ・ボス（DeathVfxConfig.bossChain）：大爆発の直後、体のあちこちで小爆発が続けて起きる（連鎖爆発）
/// ・ネオン虹テーマ：9色の多重リングと光の筋を追加し、火花・残り火を9色にして特別に派手にする
/// ・同じ瞬間に大量に撃破された時は、余韻の層（煙・破片・残り火・2つ目のリング）を省いて負荷を抑える
/// ・EnemyStatsの「一定秒数後に削除」は使わず、全ての層が消えきったら自分で消える
/// </summary>
[DisallowMultipleComponent]
public class EnemyDeathVFX : MonoBehaviour
{
    [System.Serializable]
    public class ThemeColors
    {
        public string label;
        [Tooltip("閃光・火球の芯の色")] public Color core = Color.white;
        [Tooltip("火球の中間の色")] public Color mid = new Color(1f, 0.75f, 0.25f);
        [Tooltip("火球の外側（消える直前）の色")] public Color outer = new Color(0.9f, 0.25f, 0.05f);
        [Tooltip("火花の色（この2色の間でランダム）")] public Color sparkA = new Color(1f, 0.95f, 0.6f);
        public Color sparkB = new Color(1f, 0.55f, 0.1f);
        [Tooltip("衝撃波リングの色")] public Color ring = new Color(1f, 0.7f, 0.3f);
        [Tooltip("煙の色（アルファ＝濃さ）")] public Color smoke = new Color(0.18f, 0.16f, 0.15f, 0.55f);
        [Tooltip("残り火の色")] public Color ember = new Color(1f, 0.6f, 0.2f);
        [Tooltip("ONなら火花・残り火・リング・光の筋をRainbow Colorsの9色にし、9色の多重リングと光の筋を追加する")]
        public bool rainbow;
    }

    [Header("Layers（子のParticle System。メニューで自動設定）")]
    [SerializeField] private ParticleSystem flash;
    [SerializeField] private ParticleSystem fireball;
    [SerializeField] private ParticleSystem sparks;
    [SerializeField] private ParticleSystem ringFast;
    [SerializeField] private ParticleSystem ringSlow;
    [SerializeField] private ParticleSystem debris;
    [SerializeField] private ParticleSystem smoke;
    [SerializeField] private ParticleSystem embers;
    [SerializeField] private ParticleSystem rainbowRings;
    [SerializeField] private ParticleSystem starRays;

    [Header("数（大きさ1の時。火花はDeathVfxConfigのBurst Count×Spark Count Mul）")]
    [SerializeField] private int fireballCount = 7;
    [SerializeField] private int debrisCount = 10;
    [SerializeField] private int smokeCount = 6;
    [SerializeField] private int emberCount = 14;
    [Tooltip("火花の数＝DeathVfxConfigのBurst Count×この倍率")]
    [SerializeField] private float sparkCountMul = 0.6f;
    [Tooltip("火花の大きさ＝DeathVfxConfigのStart Size×この倍率（細長く伸ばして表示するため少し太めにする）")]
    [SerializeField] private float sparkSizeMul = 1.6f;

    [Header("遅れ（秒）")]
    [SerializeField] private float ringSlowDelay = 0.04f;
    [SerializeField] private float smokeDelay = 0.08f;
    [SerializeField] private float emberDelay = 0.15f;

    [Header("大きさ")]
    [Tooltip("DeathVfxConfigのRing Scaleがこの値の時に大きさ1（Ring Scale 4なら0.67倍、9なら1.5倍）")]
    [SerializeField] private float sizeReferenceRingScale = 6f;
    [SerializeField] private float minSizeScale = 0.45f;
    [SerializeField] private float maxSizeScale = 1.8f;

    [Header("ボスの連鎖爆発（DeathVfxConfig.bossChain）")]
    [Tooltip("大爆発の後に続く小爆発の数")]
    [SerializeField] private int chainCount = 5;
    [Tooltip("小爆発の間隔（秒）")]
    [SerializeField] private float chainInterval = 0.12f;
    [Tooltip("最初の小爆発までの秒数")]
    [SerializeField] private float chainFirstDelay = 0.12f;
    [Tooltip("小爆発が起きる範囲の半径（大きさ1の時、ワールド単位）")]
    [SerializeField] private float chainRadius = 0.9f;
    [Tooltip("小爆発の大きさ（大爆発に対する倍率）")]
    [SerializeField] private float chainSizeMul = 0.45f;
    [Tooltip("ボスの大爆発の大きさの倍率")]
    [SerializeField] private float bossMainSizeMul = 1.25f;
    [Tooltip("小爆発のSE（任意）")]
    [SerializeField] private AudioClip chainSE;
    [Range(0f, 1f)] [SerializeField] private float chainSEVolume = 0.6f;

    [Header("ネオン虹（特別に派手）")]
    [Tooltip("多重リング・火花・残り火・光の筋に使う9色（Area1〜9の色）")]
    [SerializeField] private Color[] rainbowColors =
    {
        new Color(0.71f, 0.65f, 1f), new Color(0.35f, 0.95f, 0.6f), new Color(0.75f, 0.85f, 1f),
        new Color(1f, 0.55f, 0.25f), new Color(1f, 0.3f, 0.5f), new Color(1f, 0.8f, 0.3f),
        new Color(0.35f, 0.65f, 1f), new Color(0.4f, 1f, 1f), new Color(0.8f, 0.85f, 1f),
    };
    [Tooltip("9色のリングを1つずつ出す間隔（秒）")]
    [SerializeField] private float rainbowRingInterval = 0.05f;
    [SerializeField] private int starRayCount = 18;
    [Tooltip("ネオン虹の時の火花・残り火の数の倍率")]
    [SerializeField] private float rainbowCountMul = 2f;
    [Tooltip("ネオン虹の時、少し遅れてもう一度火球を出す（秒。0以下で出さない）")]
    [SerializeField] private float rainbowSecondBurstDelay = 0.18f;

    [Header("負荷対策（同じ瞬間に大量に撃破された時）")]
    [Tooltip("この秒数の間に")]
    [SerializeField] private float liteWindowSeconds = 0.15f;
    [Tooltip("この数を超えて出たら、余韻の層（煙・破片・残り火・2つ目のリング）を省き、火花を半分にする")]
    [SerializeField] private int liteThreshold = 3;

    [Header("色テーマ（並びはDeathVfxThemeと同じ：炎/紅蓮/ネオン水色/ネオン紫/ネオン虹/毒/機械/遺跡/闇/月光）")]
    [SerializeField] private ThemeColors[] themes = DefaultThemes();

    [Header("Preview（Play中に右クリックメニュー「プレビュー再生」で確認）")]
    [SerializeField] private DeathVfxTheme previewTheme = DeathVfxTheme.Fire;
    [SerializeField] private float previewRingScale = 6f;
    [SerializeField] private bool previewBossChain = false;

    private bool played;
    private static float s_windowStart = -999f;
    private static int s_windowCount;
    private static int s_lastChainFrame = -1;

    private static float MasterSEVolume => SoundSettingsManager.Instance != null ? SoundSettingsManager.Instance.SEVolume : 1f;

    // 子のParticle Systemの元の倍率（大きさを変える時の基準）
    private struct Base { public float size, speed, radius; }
    private readonly System.Collections.Generic.Dictionary<ParticleSystem, Base> bases = new System.Collections.Generic.Dictionary<ParticleSystem, Base>();

    private void Start()
    {
        // EnemyStatsから呼ばれなかった（他の仕組みでInstantiateされた）場合も、そのまま再生する
        if (!played) Play(null);
    }

    /// <summary>再生する（EnemyStatsが撃破時に呼ぶ）。configがnullなら炎テーマ・大きさ1</summary>
    public void Play(DeathVfxConfig config)
    {
        if (played) return;
        played = true;

        DeathVfxTheme theme = config != null ? config.theme : DeathVfxTheme.Fire;
        ThemeColors tc = GetTheme(theme);
        float scale = config != null && sizeReferenceRingScale > 0f
            ? Mathf.Clamp(config.ringScale / sizeReferenceRingScale, minSizeScale, maxSizeScale)
            : 1f;

        // 同じ瞬間に大量に撃破された時は軽くする
        if (Time.time - s_windowStart > liteWindowSeconds) { s_windowStart = Time.time; s_windowCount = 0; }
        s_windowCount++;
        bool lite = s_windowCount > Mathf.Max(1, liteThreshold);

        // ボスの連鎖爆発：同じフレームに複数出た時（体の複数の位置に出す敵）は最初の1つだけ
        bool chain = config != null && config.bossChain && Time.frameCount != s_lastChainFrame;
        if (chain) s_lastChainFrame = Time.frameCount;

        ApplySparkConfig(config);
        CacheBases();
        ApplyColors(tc);

        float mainScale = scale * (chain ? bossMainSizeMul : 1f);
        StartCoroutine(PlayRoutine(tc, mainScale, lite, chain));

        // 全ての粒が消えきったら自分を消す（EnemyStatsの「一定秒数後に削除」は使わない）
        float total = MaxLifetime() + Mathf.Max(emberDelay, smokeDelay, ringSlowDelay);
        if (chain) total += chainFirstDelay + chainInterval * Mathf.Max(0, chainCount - 1);
        if (tc.rainbow) total += Mathf.Max(rainbowRingInterval * (rainbowColors != null ? rainbowColors.Length : 0), rainbowSecondBurstDelay);
        Destroy(gameObject, total + 0.3f);
    }

    private IEnumerator PlayRoutine(ThemeColors tc, float s, bool lite, bool chain)
    {
        Vector3 p = transform.position;
        float countMul = tc.rainbow ? rainbowCountMul : 1f;

        // ① 閃光 ② 火球 ③ 火花 ④ 衝撃波（1つ目） ⑤ 破片
        Burst(flash, p, s, 1);
        Burst(fireball, p, s, Mathf.RoundToInt(fireballCount * (lite ? 0.6f : 1f)));
        Burst(sparks, p, s, Mathf.RoundToInt(sparkBaseCount * countMul * (lite ? 0.5f : 1f)));
        Burst(ringFast, p, s, 1);
        if (!lite) Burst(debris, p, s, debrisCount);

        // ネオン虹：光の筋
        if (tc.rainbow) Burst(starRays, p, s, starRayCount);

        float t = 0f;
        // ④ 衝撃波（2つ目）
        if (!lite) { yield return Wait(ringSlowDelay - t); t = ringSlowDelay; Burst(ringSlow, p, s, 1); }
        // ⑥ 煙
        if (!lite) { yield return Wait(smokeDelay - t); t = Mathf.Max(t, smokeDelay); Burst(smoke, p, s, smokeCount); }
        // ⑦ 残り火
        if (!lite) { yield return Wait(emberDelay - t); t = Mathf.Max(t, emberDelay); Burst(embers, p, s, Mathf.RoundToInt(emberCount * countMul)); }

        // ネオン虹：9色の多重リング＋少し遅れてもう一度火球
        if (tc.rainbow) StartCoroutine(RainbowRoutine(p, s));

        // ボスの連鎖爆発（大爆発の後、体のあちこちで小爆発）
        if (chain)
        {
            yield return Wait(chainFirstDelay - t);
            for (int i = 0; i < chainCount; i++)
            {
                Vector2 off = Random.insideUnitCircle * chainRadius * s;
                Vector3 cp = p + new Vector3(off.x, off.y, 0f);
                float cs = s * chainSizeMul * Random.Range(0.8f, 1.2f);
                Burst(flash, cp, cs, 1);
                Burst(fireball, cp, cs, 3);
                Burst(sparks, cp, cs, Mathf.RoundToInt(sparkBaseCount * 0.3f * countMul));
                Burst(ringFast, cp, cs, 1);
                if (chainSE != null && chainSEVolume > 0f && SeSimultaneousGuard.TryAllow("EnemyDeathVFX_Chain"))
                    AudioOneShotPool.Play(chainSE, chainSEVolume * MasterSEVolume, cp, null, 0.1f);
                if (i < chainCount - 1) yield return Wait(chainInterval);
            }
        }
    }

    private IEnumerator RainbowRoutine(Vector3 p, float s)
    {
        if (rainbowRings != null && rainbowColors != null)
        {
            var ep = new ParticleSystem.EmitParams { position = p, applyShapeToPosition = true };
            for (int i = 0; i < rainbowColors.Length; i++)
            {
                SetScale(rainbowRings, s * (0.8f + 0.18f * i));
                ep.startColor = rainbowColors[i];
                rainbowRings.Emit(ep, 1);
                yield return Wait(rainbowRingInterval);
            }
        }
        if (rainbowSecondBurstDelay > 0f)
        {
            yield return Wait(Mathf.Max(0f, rainbowSecondBurstDelay - rainbowRingInterval * (rainbowColors != null ? rainbowColors.Length : 0)));
            Burst(flash, p, s * 0.8f, 1);
            Burst(fireball, p, s * 0.9f, fireballCount);
            Burst(sparks, p, s, Mathf.RoundToInt(sparkBaseCount * rainbowCountMul * 0.5f));
        }
    }

    private static IEnumerator Wait(float seconds)
    {
        if (seconds > 0f) yield return new WaitForSeconds(seconds);
    }

    // ---------- 粒を出す ----------
    private int sparkBaseCount = 48;

    private void Burst(ParticleSystem ps, Vector3 pos, float scale, int count)
    {
        if (ps == null || count <= 0) return;
        SetScale(ps, scale);
        var ep = new ParticleSystem.EmitParams { position = pos, applyShapeToPosition = true };
        ps.Emit(ep, count);
    }

    private void SetScale(ParticleSystem ps, float scale)
    {
        if (!bases.TryGetValue(ps, out Base b)) return;
        var main = ps.main;
        main.startSizeMultiplier = b.size * scale;
        main.startSpeedMultiplier = b.speed * scale;
        var shape = ps.shape;
        if (shape.enabled) shape.radius = b.radius * scale;
    }

    private void CacheBases()
    {
        bases.Clear();
        foreach (var ps in new[] { flash, fireball, sparks, ringFast, ringSlow, debris, smoke, embers, rainbowRings, starRays })
        {
            if (ps == null || bases.ContainsKey(ps)) continue;
            var main = ps.main;
            bases[ps] = new Base { size = main.startSizeMultiplier, speed = main.startSpeedMultiplier, radius = ps.shape.radius };
            // 発生は全てEmitで行う（自動の発生は止め、粒の更新だけ行う）
            var em = ps.emission;
            em.enabled = false;
            if (!ps.isPlaying) ps.Play(false);
        }
    }

    private float MaxLifetime()
    {
        float m = 0.5f;
        foreach (var ps in new[] { flash, fireball, sparks, ringFast, ringSlow, debris, smoke, embers, rainbowRings, starRays })
        {
            if (ps == null) continue;
            m = Mathf.Max(m, ps.main.startLifetime.constantMax, ps.main.startLifetime.constant);
        }
        return m;
    }

    // ---------- 従来の火花の設定（DeathVfxConfig）を引き継ぐ ----------
    private void ApplySparkConfig(DeathVfxConfig c)
    {
        if (sparks == null) return;
        int burst = c != null ? c.burstCount : 80;
        sparkBaseCount = Mathf.Max(1, Mathf.RoundToInt(burst * sparkCountMul));
        if (c == null) return;
        var main = sparks.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(c.lifetimeMin, Mathf.Max(c.lifetimeMin, c.lifetimeMax) + 0.15f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(c.startSpeedMin * 1.4f, c.startSpeedMax * 1.6f);
        main.startSize = new ParticleSystem.MinMaxCurve(c.startSizeMin * sparkSizeMul, c.startSizeMax * sparkSizeMul);
        main.gravityModifier = new ParticleSystem.MinMaxCurve(c.gravity);
    }

    // ---------- 色 ----------
    private ThemeColors GetTheme(DeathVfxTheme theme)
    {
        int i = (int)theme;
        if (themes != null && i >= 0 && i < themes.Length && themes[i] != null) return themes[i];
        var d = DefaultThemes();
        return d[Mathf.Clamp(i, 0, d.Length - 1)];
    }

    private void ApplyColors(ThemeColors tc)
    {
        Gradient rainbow = RainbowGradient();

        SetStartColor(flash, new ParticleSystem.MinMaxGradient(tc.core));
        SetStartColor(fireball, new ParticleSystem.MinMaxGradient(Color.white));
        if (fireball != null)
        {
            var col = fireball.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(tc.core, 0f), new GradientColorKey(tc.mid, 0.3f), new GradientColorKey(tc.outer, 0.75f), new GradientColorKey(tc.outer * 0.5f, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.9f, 0.4f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(g);
        }
        SetStartColor(sparks, tc.rainbow && rainbow != null ? RandomFrom(rainbow) : new ParticleSystem.MinMaxGradient(tc.sparkA, tc.sparkB));
        SetStartColor(ringFast, new ParticleSystem.MinMaxGradient(tc.rainbow ? Color.white : tc.ring));
        SetStartColor(ringSlow, new ParticleSystem.MinMaxGradient(tc.ring));
        SetStartColor(debris, new ParticleSystem.MinMaxGradient(Color.Lerp(tc.outer, Color.black, 0.45f), Color.Lerp(tc.mid, Color.black, 0.25f)));
        SetStartColor(smoke, new ParticleSystem.MinMaxGradient(tc.smoke, Color.Lerp(tc.smoke, Color.black, 0.3f)));
        SetStartColor(embers, tc.rainbow && rainbow != null ? RandomFrom(rainbow) : new ParticleSystem.MinMaxGradient(tc.ember, tc.mid));
        if (starRays != null && rainbow != null) SetStartColor(starRays, RandomFrom(rainbow));
    }

    private static void SetStartColor(ParticleSystem ps, ParticleSystem.MinMaxGradient c)
    {
        if (ps == null) return;
        var main = ps.main;
        main.startColor = c;
    }

    private static ParticleSystem.MinMaxGradient RandomFrom(Gradient g)
    {
        var m = new ParticleSystem.MinMaxGradient(g);
        m.mode = ParticleSystemGradientMode.RandomColor;
        return m;
    }

    private Gradient RainbowGradient()
    {
        if (rainbowColors == null || rainbowColors.Length == 0) return null;
        int n = Mathf.Min(8, rainbowColors.Length); // Gradientのキーは最大8個
        var keys = new GradientColorKey[n];
        for (int i = 0; i < n; i++)
        {
            int src = n == rainbowColors.Length ? i : Mathf.RoundToInt(i * (rainbowColors.Length - 1) / (float)(n - 1));
            keys[i] = new GradientColorKey(rainbowColors[src], n == 1 ? 0f : i / (float)(n - 1));
        }
        var g = new Gradient();
        g.mode = GradientMode.Fixed;
        g.SetKeys(keys, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        return g;
    }

    public static ThemeColors[] DefaultThemes()
    {
        ThemeColors T(string label, Color core, Color mid, Color outer, Color sA, Color sB, Color ring, Color smoke, Color ember, bool rainbow = false)
            => new ThemeColors { label = label, core = core, mid = mid, outer = outer, sparkA = sA, sparkB = sB, ring = ring, smoke = smoke, ember = ember, rainbow = rainbow };
        return new[]
        {
            T("① 炎", new Color(1f, 1f, 0.9f), new Color(1f, 0.75f, 0.25f), new Color(0.9f, 0.25f, 0.05f),
              new Color(1f, 0.95f, 0.6f), new Color(1f, 0.55f, 0.1f), new Color(1f, 0.7f, 0.3f), new Color(0.18f, 0.16f, 0.15f, 0.55f), new Color(1f, 0.6f, 0.2f)),
            T("② 紅蓮", new Color(1f, 0.95f, 0.85f), new Color(1f, 0.45f, 0.15f), new Color(0.7f, 0.05f, 0.05f),
              new Color(1f, 0.6f, 0.3f), new Color(0.95f, 0.15f, 0.1f), new Color(1f, 0.3f, 0.2f), new Color(0.2f, 0.06f, 0.06f, 0.6f), new Color(1f, 0.35f, 0.15f)),
            T("③ ネオン水色", new Color(0.9f, 1f, 1f), new Color(0.3f, 0.9f, 1f), new Color(0.1f, 0.3f, 0.9f),
              new Color(0.7f, 1f, 1f), new Color(0.2f, 0.7f, 1f), new Color(0.4f, 0.9f, 1f), new Color(0.05f, 0.08f, 0.18f, 0.55f), new Color(0.4f, 0.85f, 1f)),
            T("④ ネオン紫", new Color(1f, 0.9f, 1f), new Color(1f, 0.35f, 0.85f), new Color(0.45f, 0.15f, 0.9f),
              new Color(1f, 0.5f, 0.9f), new Color(0.6f, 0.3f, 1f), new Color(0.9f, 0.4f, 1f), new Color(0.12f, 0.05f, 0.18f, 0.55f), new Color(1f, 0.45f, 0.9f)),
            T("⑤ ネオン虹", Color.white, new Color(0.75f, 0.65f, 1f), new Color(0.3f, 0.4f, 1f),
              Color.white, Color.white, new Color(0.85f, 0.8f, 1f), new Color(0.08f, 0.05f, 0.15f, 0.5f), Color.white, true),
            T("⑥ 毒", new Color(0.95f, 1f, 0.85f), new Color(0.6f, 1f, 0.2f), new Color(0.15f, 0.5f, 0.1f),
              new Color(0.85f, 1f, 0.4f), new Color(0.4f, 0.9f, 0.2f), new Color(0.6f, 1f, 0.3f), new Color(0.08f, 0.15f, 0.05f, 0.55f), new Color(0.7f, 1f, 0.3f)),
            T("⑦ 機械", new Color(1f, 1f, 0.9f), new Color(1f, 0.65f, 0.2f), new Color(0.8f, 0.25f, 0.05f),
              new Color(0.6f, 1f, 1f), new Color(0.2f, 0.75f, 1f), new Color(1f, 0.7f, 0.35f), new Color(0.1f, 0.1f, 0.1f, 0.65f), new Color(1f, 0.6f, 0.25f)),
            T("⑧ 遺跡", new Color(1f, 1f, 0.85f), new Color(1f, 0.82f, 0.4f), new Color(0.6f, 0.42f, 0.15f),
              new Color(0.5f, 1f, 0.9f), new Color(0.2f, 0.85f, 0.75f), new Color(1f, 0.85f, 0.5f), new Color(0.35f, 0.28f, 0.18f, 0.55f), new Color(1f, 0.8f, 0.4f)),
            T("⑨ 闇", new Color(0.9f, 1f, 1f), new Color(0.3f, 0.95f, 0.85f), new Color(0.4f, 0.15f, 0.6f),
              new Color(0.7f, 0.4f, 1f), new Color(0.4f, 0.2f, 0.8f), new Color(0.5f, 0.9f, 0.9f), new Color(0.07f, 0.03f, 0.1f, 0.65f), new Color(0.6f, 0.4f, 1f)),
            T("⑩ 月光", Color.white, new Color(0.75f, 0.85f, 1f), new Color(0.4f, 0.4f, 0.85f),
              new Color(0.9f, 0.95f, 1f), new Color(0.6f, 0.7f, 1f), new Color(0.85f, 0.9f, 1f), new Color(0.08f, 0.08f, 0.2f, 0.5f), new Color(0.8f, 0.85f, 1f)),
        };
    }

    // ---------- プレビュー ----------
    [ContextMenu("プレビュー再生（Play中のみ）")]
    private void PreviewPlay()
    {
        if (!Application.isPlaying) { Debug.LogWarning("[EnemyDeathVFX] プレビューはPlay中のみ"); return; }
        var go = Instantiate(gameObject, transform.position, Quaternion.identity);
        var v = go.GetComponent<EnemyDeathVFX>();
        v.Play(new DeathVfxConfig { theme = previewTheme, ringScale = previewRingScale, bossChain = previewBossChain });
    }

    /// <summary>メニューのテスト再生用</summary>
    public static void SpawnPreview(GameObject prefab, Vector3 pos, DeathVfxTheme theme, float ringScale, bool bossChain)
    {
        if (prefab == null) return;
        var go = Instantiate(prefab, pos, Quaternion.identity);
        var v = go.GetComponent<EnemyDeathVFX>();
        if (v != null) v.Play(new DeathVfxConfig { theme = theme, ringScale = ringScale, bossChain = bossChain, burstCount = 120 });
    }
}
