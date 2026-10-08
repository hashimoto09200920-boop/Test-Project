using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 敵の未反射弾（ビーム・ドリル以外の全弾種）を派手にする演出の管理役。Assets/Resources/BulletFX.prefab から1つだけ作られる
/// （プレハブが無ければ何もしない＝従来の見た目。メニュー「Tools/弾の見た目/…」で作成）。
/// 弾は撃たれた時（EnemyShooter.ApplyBulletTypeToEnemyBullet）に登録され、この管理役が登録された全弾を1回のループでまとめて更新する。
/// 反射された・消えた弾は登録を外し、変えた見た目を元に戻す（弾の動き・当たり判定・色には一切触れない）。
/// ・共通：① 暗いオーラ（進む方向へ伸びて③速さの伸びも表す） ② 出現の「ポン」
/// ・弾種ごと：Normal 鼓動／Slow 重たい残像／Speed Curve 加速で伸びてTrailが太く／Wave 波の頂点で光の粒／Rapid 細長い針
///   Multi 発射の閃光／Telegraph 線の上を走る光／Missile 煙の尾と噴射の火／Countdown 速まる鼓動と最後の膨らみ
///   MultiWarhead 回る光2つと分裂の閃光／Smoke 漂う黒煙／Warp 消える・現れる時の輪と明滅
/// ・負荷対策：パーティクルは共有（Emitで出す）、画像と線は貸し出しで使い回し、弾が多い時は演出を省く・粒を減らす
/// </summary>
[DisallowMultipleComponent]
public class BulletFXManager : MonoBehaviour
{
    public enum Kind { Normal, Slow, SpeedCurve, Wave, Rapid, Multi, Telegraph, Missile, Countdown, MultiWarhead, Smoke, Warp, Spiral }

    [Header("全体")]
    [Tooltip("OFFにすると全ての演出を止める（従来の見た目）")]
    public bool fxEnabled = true;
    [Tooltip("画面の弾（演出中）がこの数を超えたら、新しく出る弾ではオーラ・粒などの演出を省く")]
    public int maxFancyBullets = 80;
    [Tooltip("粒（パーティクル）の量を減らし始める弾の数")]
    public int particleThinStart = 40;

    [Header("共有のパーティクル・画像（メニューで自動設定）")]
    public ParticleSystem sparkle;
    public ParticleSystem smoke;
    public Sprite glowSprite;
    public Sprite ringSprite;
    [Tooltip("加算の画像用マテリアル（噴射の火・閃光・輪）")]
    public Material additiveSpriteMaterial;
    [Tooltip("線（Telegraphの走る光）のマテリアル（加算）")]
    public Material lineMaterial;

    [Header("① 暗いオーラ（色は弾の未反射Trailの色。無ければ弾種の色）")]
    [Tooltip("オーラの大きさ（弾の大きさに対する倍率）")]
    public float auraSizeMul = 2.1f;
    [Range(0f, 1f)] public float auraAlpha = 0.55f;
    [Tooltip("オーラの色をどれだけ暗くするか（0＝そのまま、1＝黒）")]
    [Range(0f, 1f)] public float auraDarken = 0.25f;
    [Tooltip("③ 速さ1あたりの伸び（進む方向）")]
    public float stretchPerSpeed = 0.06f;
    public float stretchMax = 2.2f;

    [Header("② 出現の「ポン」")]
    public float popDuration = 0.16f;
    public float popOvershoot = 1.35f;

    [Header("Normal：鼓動")]
    public float normalPulseSpeed = 6f;
    [Range(0f, 1f)] public float normalPulseAmount = 0.35f;

    [Header("Slow：重たい残像")]
    public int slowGhostCount = 2;
    public float slowGhostDelay = 0.09f;
    [Range(0f, 1f)] public float slowGhostAlpha = 0.4f;

    [Header("Speed Curve：加速で伸びてTrailが太く")]
    public float speedCurveTrailWidthMax = 1.8f;

    [Header("Wave：波の頂点で光の粒")]
    public int waveSparkCount = 3;

    [Header("Rapid：細長い針")]
    public float rapidStretchMul = 2f;

    [Header("Multi：発射の閃光")]
    public int multiFlashSparks = 6;
    public float multiFlashSize = 0.9f;

    [Header("Telegraph：線の上を走る光")]
    public float telegraphStreakLength = 2.5f;
    public float telegraphStreakDuration = 0.22f;
    public float telegraphStreakWidth = 0.12f;

    [Header("Missile：煙の尾と噴射の火")]
    public float missileSmokeRate = 18f;
    public Color missileSmokeColor = new Color(0.55f, 0.55f, 0.58f, 0.55f);
    public Color missileFlameColor = new Color(1f, 0.55f, 0.15f, 0.9f);
    public float missileFlameSizeMul = 1.1f;

    [Header("Countdown：速まる鼓動と最後の膨らみ")]
    public float countdownPulseStart = 3f;
    public float countdownPulseEnd = 16f;
    public Color countdownFinalColor = new Color(1f, 0.15f, 0.1f, 1f);
    [Tooltip("爆発の何秒前から膨らむか")]
    public float countdownSwellSeconds = 0.35f;

    [Header("MultiWarhead：回る光と分裂の閃光")]
    public int warheadOrbitCount = 2;
    public float warheadOrbitRadiusMul = 0.9f;
    public float warheadOrbitSpeed = 540f;
    public Color warheadOrbitColor = new Color(1f, 0.6f, 0.2f, 0.9f);
    public int warheadSplitSparks = 10;

    [Header("Smoke：漂う黒煙")]
    public float smokeRate = 10f;
    public Color smokeColor = new Color(0.12f, 0.12f, 0.14f, 0.6f);

    [Header("Warp：消える・現れる時の輪と明滅")]
    public float warpRingSize = 1.4f;
    public float warpRingDuration = 0.3f;
    public Color warpRingColor = new Color(0.55f, 0.45f, 1f, 1f);
    [Range(0f, 1f)] public float warpFlickerAmount = 0.5f;

    // ---------- 共有インスタンス ----------
    private static BulletFXManager s_instance;
    private static bool s_triedLoad;

    /// <summary>今ある管理役（無ければnull。読み込みを起こさない。弾が消える時に使う）</summary>
    public static BulletFXManager Existing => s_instance;

    public static BulletFXManager Instance
    {
        get
        {
            if (s_instance != null) return s_instance;
            if (s_triedLoad) return null;
            s_triedLoad = true;
            var prefab = Resources.Load<BulletFXManager>("BulletFX");
            if (prefab == null) return null;
            s_instance = Instantiate(prefab);
            s_instance.name = "BulletFX";
            return s_instance;
        }
    }

    /// <summary>撃たれた弾を登録する（EnemyShooter.ApplyBulletTypeToEnemyBulletから呼ぶ）</summary>
    public static void Register(EnemyBullet b, EnemyData.BulletType bt)
    {
        if (b == null || bt == null || bt.useBeam || bt.usePinnedReflect) return;
        var m = Instance;
        if (m == null || !m.fxEnabled) return;
        m.Add(b, bt);
    }

    // ---------- 登録された弾 ----------
    private class Entry
    {
        public EnemyBullet b;
        public Transform tf, visualTf;
        public SpriteRenderer visual;
        public TrailRenderer trail;
        public Rigidbody2D rb;
        public Kind kind;
        public bool fancy;
        public Vector3 visualBaseScale;
        public float age, emitAcc, seed, explodeAt;
        public Color auraColor;
        public SpriteRenderer aura, flame;
        public SpriteRenderer[] ghosts, orbits;
        public Vector3[] hist; public float[] histT; public int histHead;
        public Vector2 lastDir; public float lastCross;
        public bool lastVisible = true;
        public Vector3 lastPos;
    }

    private readonly List<Entry> entries = new List<Entry>();
    private readonly Dictionary<EnemyBullet, Entry> byBullet = new Dictionary<EnemyBullet, Entry>();
    private readonly Stack<SpriteRenderer> freeSprites = new Stack<SpriteRenderer>();
    private readonly Stack<LineRenderer> freeLines = new Stack<LineRenderer>();
    private ParticleSystem[] particleList;
    private float lastParticleSpeed = float.NaN;
    private int fancyCount;

    private class Anim { public SpriteRenderer sr; public LineRenderer lr; public float t, dur, s0, s1; public Color c; public Vector3 a, b; }
    private readonly List<Anim> anims = new List<Anim>();
    private readonly List<(Vector3 pos, float time)> recentFlashes = new List<(Vector3, float)>();

    private void Awake()
    {
        if (s_instance == null) s_instance = this;
        particleList = new[] { sparkle, smoke };
        foreach (var ps in particleList)
        {
            if (ps == null) continue;
            var em = ps.emission; em.enabled = false;
            if (!ps.isPlaying) ps.Play(false);
        }
    }

    private void OnDestroy()
    {
        if (s_instance == this) { s_instance = null; s_triedLoad = false; }
    }

    private static Kind Classify(EnemyData.BulletType bt)
    {
        if (bt.useWarp) return Kind.Warp;
        if (bt.useSmokeGrenade) return Kind.Smoke;
        if (bt.useMultiWarhead) return Kind.MultiWarhead;
        if (bt.useCountdownExplosion) return Kind.Countdown;
        if (bt.useMissileArc) return Kind.Missile;
        if (bt.useTelegraph) return Kind.Telegraph;
        if (bt.useMultiShot) return Kind.Multi;
        if (bt.useWaveMotion) return Kind.Wave;
        if (bt.useSpiralMotion) return Kind.Spiral;
        if (bt.useSpeedCurve) return Kind.SpeedCurve;
        string n = bt.name ?? "";
        if (n.IndexOf("Rapid", System.StringComparison.OrdinalIgnoreCase) >= 0) return Kind.Rapid;
        if (n.IndexOf("Slow", System.StringComparison.OrdinalIgnoreCase) >= 0) return Kind.Slow;
        return Kind.Normal;
    }

    private static Color DefaultColor(Kind k)
    {
        switch (k)
        {
            case Kind.Slow: return new Color(0.55f, 0.1f, 0.6f);
            case Kind.SpeedCurve: return new Color(0.6f, 0.8f, 0.1f);
            case Kind.Wave: return new Color(0.35f, 0.3f, 0.85f);
            case Kind.Rapid: return new Color(0.9f, 0.1f, 0.1f);
            case Kind.Multi: return new Color(0.8f, 0.4f, 0.1f);
            case Kind.Missile: return new Color(0.6f, 0.6f, 0.65f);
            case Kind.MultiWarhead: return new Color(0.45f, 0.1f, 0.6f);
            case Kind.Smoke: return new Color(0.3f, 0.35f, 0.3f);
            case Kind.Warp: return new Color(0.4f, 0.3f, 0.9f);
            case Kind.Spiral: return new Color(0.1f, 0.5f, 0.25f);
            default: return new Color(0.6f, 0.05f, 0.05f);
        }
    }

    private void Add(EnemyBullet b, EnemyData.BulletType bt)
    {
        if (byBullet.ContainsKey(b)) Remove(byBullet[b]);
        var tag = b.GetComponent<BulletFXTag>();
        if (tag == null) tag = b.gameObject.AddComponent<BulletFXTag>();
        tag.bullet = b;

        var e = new Entry { b = b, tf = b.transform, kind = Classify(bt), seed = Random.value * 10f };
        Transform v = b.transform.Find("Visual");
        e.visual = v != null ? v.GetComponent<SpriteRenderer>() : null;
        e.visualTf = e.visual != null ? e.visual.transform : null;
        e.visualBaseScale = e.visualTf != null ? e.visualTf.localScale : Vector3.one;
        Transform tr = b.transform.Find("Trail");
        e.trail = tr != null ? tr.GetComponent<TrailRenderer>() : null;
        e.rb = b.GetComponent<Rigidbody2D>();
        e.lastPos = e.tf.position;
        e.explodeAt = bt.useCountdownExplosion ? Mathf.Max(0.1f, bt.explosionDelaySeconds) : 0f;
        Color baseC = bt.useUnreflectedTrail ? bt.unreflectedTrailColor : (bt.useColorOverride ? bt.colorOverride : DefaultColor(e.kind));
        baseC.a = 1f;
        e.auraColor = Color.Lerp(baseC, Color.black, auraDarken);

        e.fancy = fancyCount < maxFancyBullets && e.visual != null;
        if (e.fancy)
        {
            fancyCount++;
            int layer = e.visual.sortingLayerID, order = e.visual.sortingOrder;
            e.aura = RentSprite(glowSprite, e.visual.sharedMaterial, layer, order - 1);
            if (e.kind == Kind.Slow && slowGhostCount > 0)
            {
                e.ghosts = new SpriteRenderer[slowGhostCount];
                for (int i = 0; i < e.ghosts.Length; i++) { e.ghosts[i] = RentSprite(null, e.visual.sharedMaterial, layer, order - 1); e.ghosts[i].enabled = false; }
                e.hist = new Vector3[32]; e.histT = new float[32];
            }
            if (e.kind == Kind.MultiWarhead && warheadOrbitCount > 0)
            {
                e.orbits = new SpriteRenderer[warheadOrbitCount];
                for (int i = 0; i < e.orbits.Length; i++) e.orbits[i] = RentSprite(glowSprite, additiveSpriteMaterial, layer, order + 1);
            }
            if (e.kind == Kind.Missile) e.flame = RentSprite(glowSprite, additiveSpriteMaterial, layer, order - 1);
        }

        // 出現時の演出
        Vector3 p = e.tf.position;
        if (e.kind == Kind.Multi && TryFlashOnce(p))
        {
            EmitBurst(sparkle, p, multiFlashSparks, Color.Lerp(e.auraColor, Color.white, 0.4f), 2.5f);
            SpawnRingAnim(p, multiFlashSize * 0.3f, multiFlashSize, 0.18f, Color.Lerp(e.auraColor, Color.white, 0.5f), e);
        }
        if (e.kind == Kind.Telegraph && lineMaterial != null)
        {
            Vector2 dir = e.rb != null && e.rb.linearVelocity.sqrMagnitude > 0.0001f ? e.rb.linearVelocity.normalized : Vector2.down;
            var lr = RentLine(e.visual != null ? e.visual.sortingLayerID : 0, e.visual != null ? e.visual.sortingOrder + 1 : 0);
            anims.Add(new Anim { lr = lr, dur = telegraphStreakDuration, a = p, b = p + (Vector3)(dir * telegraphStreakLength), c = Color.Lerp(e.auraColor, Color.white, 0.6f) });
        }

        entries.Add(e);
        byBullet[b] = e;
    }

    /// <summary>弾が消えた時（BulletFXTag.OnDisable）に呼ばれる</summary>
    public void OnBulletDisabled(EnemyBullet b)
    {
        if (b == null || !byBullet.TryGetValue(b, out Entry e)) return;
        // MultiWarhead：画面内で反射されずに消えた＝分裂した瞬間の閃光
        if (e.kind == Kind.MultiWarhead && e.fancy && !(b.IsReflected || b.HasPaddleReflectedOnce) && IsOnScreen(e.lastPos))
        {
            EmitBurst(sparkle, e.lastPos, warheadSplitSparks, warheadOrbitColor, 3f);
            SpawnRingAnim(e.lastPos, 0.2f, 1.2f, 0.2f, warheadOrbitColor, e);
        }
        Remove(e);
    }

    private void Remove(Entry e)
    {
        if (e.fancy) fancyCount = Mathf.Max(0, fancyCount - 1);
        if (e.visualTf != null) e.visualTf.localScale = e.visualBaseScale;
        if (e.trail != null) e.trail.widthMultiplier = 1f;
        ReleaseSprite(e.aura); ReleaseSprite(e.flame);
        if (e.ghosts != null) foreach (var g in e.ghosts) ReleaseSprite(g);
        if (e.orbits != null) foreach (var o in e.orbits) ReleaseSprite(o);
        e.aura = e.flame = null; e.ghosts = e.orbits = null;
        entries.Remove(e);
        if (e.b != null) byBullet.Remove(e.b);
        else
        {
            // 破棄済みの弾のキーを掃除
            var dead = new List<EnemyBullet>();
            foreach (var kv in byBullet) if (kv.Key == null) dead.Add(kv.Key);
            foreach (var k in dead) byBullet.Remove(k);
        }
    }

    // ---------- 毎フレーム ----------
    private void LateUpdate()
    {
        SlowMoTime.ParticleSpeed(particleList, ref lastParticleSpeed);
        float dt = SlowMoTime.DeltaTime;
        float thin = entries.Count <= particleThinStart ? 1f : Mathf.Clamp01(1f - (entries.Count - particleThinStart) / (float)Mathf.Max(1, maxFancyBullets));

        for (int i = entries.Count - 1; i >= 0; i--)
        {
            var e = entries[i];
            if (e.b == null || !e.b.gameObject.activeInHierarchy || e.b.IsReflected || e.b.HasPaddleReflectedOnce)
            {
                Remove(e); // 反射された弾は演出をやめて元に戻す（反射弾の見た目はプレイヤー側の演出）
                continue;
            }
            if (!fxEnabled) { Remove(e); continue; }
            UpdateEntry(e, dt, thin);
        }
        UpdateAnims(dt);
    }

    private void UpdateEntry(Entry e, float dt, float thin)
    {
        e.age += dt;
        Vector3 pos = e.tf.position;
        Vector2 vel = e.rb != null ? e.rb.linearVelocity : (Vector2)(pos - e.lastPos) / Mathf.Max(0.0001f, Time.deltaTime);
        float speed = vel.magnitude;
        Vector2 dir = speed > 0.01f ? vel / speed : (e.lastDir.sqrMagnitude > 0f ? e.lastDir : Vector2.down);
        bool visible = e.visual != null && e.visual.enabled && e.visual.color.a > 0.05f;
        float size = e.visual != null ? Mathf.Max(0.05f, Mathf.Max(e.visual.bounds.size.x, e.visual.bounds.size.y)) : 0.3f;

        // ② 出現の「ポン」（弾の絵の大きさだけ。終わったら元の大きさに戻る）
        if (e.visualTf != null)
        {
            if (e.age < popDuration)
            {
                float k = e.age / popDuration;
                float s = k < 0.6f ? Mathf.Lerp(0.3f, popOvershoot, k / 0.6f) : Mathf.Lerp(popOvershoot, 1f, (k - 0.6f) / 0.4f);
                float extra = CountdownSwell(e);
                e.visualTf.localScale = e.visualBaseScale * s * extra;
            }
            else
            {
                e.visualTf.localScale = e.visualBaseScale * CountdownSwell(e);
            }
        }

        // ① 暗いオーラ（③ 速さの伸び：進む方向へ伸ばす）
        if (e.aura != null)
        {
            e.aura.enabled = visible;
            if (visible)
            {
                float pulse = 1f, alpha = auraAlpha;
                Color c = e.auraColor;
                switch (e.kind)
                {
                    case Kind.Normal:
                        pulse = 1f + normalPulseAmount * Mathf.Sin(e.age * normalPulseSpeed + e.seed);
                        break;
                    case Kind.Countdown:
                    {
                        float p = e.explodeAt > 0f ? Mathf.Clamp01(e.age / e.explodeAt) : 0f;
                        float freq = Mathf.Lerp(countdownPulseStart, countdownPulseEnd, p * p);
                        pulse = 1f + 0.4f * Mathf.Sin(e.age * freq * Mathf.PI * 2f * 0.25f + e.seed);
                        c = Color.Lerp(c, countdownFinalColor, p);
                        alpha = Mathf.Lerp(auraAlpha, 0.9f, p);
                        break;
                    }
                    case Kind.Warp:
                        alpha *= 1f - warpFlickerAmount * Mathf.PerlinNoise(e.age * 12f, e.seed);
                        break;
                }
                float stretch = 1f + Mathf.Min(stretchMax - 1f, speed * stretchPerSpeed);
                if (e.kind == Kind.Rapid) stretch *= rapidStretchMul;
                if (e.kind == Kind.SpeedCurve) stretch *= 1f + Mathf.Clamp01(speed / 12f);
                SetSprite(e.aura, pos - (Vector3)(dir * size * 0.15f * (stretch - 1f)), dir, size * auraSizeMul * pulse * stretch, size * auraSizeMul * pulse * (e.kind == Kind.Rapid ? 0.6f : 1f));
                c.a = alpha;
                e.aura.color = c;
            }
        }

        // Speed Curve：加速するほどTrailを太く
        if (e.kind == Kind.SpeedCurve && e.trail != null)
            e.trail.widthMultiplier = Mathf.Lerp(1f, speedCurveTrailWidthMax, Mathf.Clamp01(speed / 12f));

        // Slow：重たい残像
        if (e.ghosts != null) UpdateGhosts(e, visible);

        // MultiWarhead：周りを回る光
        if (e.orbits != null)
        {
            for (int i = 0; i < e.orbits.Length; i++)
            {
                var o = e.orbits[i];
                if (o == null) continue;
                o.enabled = visible;
                if (!visible) continue;
                float ang = (e.age * warheadOrbitSpeed + i * 360f / e.orbits.Length) * Mathf.Deg2Rad;
                Vector3 op = pos + new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), 0f) * size * warheadOrbitRadiusMul;
                SetSprite(o, op, Vector2.right, size * 0.55f, size * 0.55f);
                o.color = warheadOrbitColor;
            }
        }

        // Missile：噴射の火と煙の尾
        if (e.flame != null)
        {
            e.flame.enabled = visible;
            if (visible)
            {
                float flick = 0.85f + 0.3f * Mathf.PerlinNoise(e.age * 20f, e.seed);
                SetSprite(e.flame, pos - (Vector3)(dir * size * 0.55f), dir, size * missileFlameSizeMul * 1.6f * flick, size * missileFlameSizeMul * 0.8f * flick);
                e.flame.color = missileFlameColor;
            }
        }
        if (e.fancy && visible && speed > 0.1f)
        {
            float rate = e.kind == Kind.Missile ? missileSmokeRate : e.kind == Kind.Smoke ? smokeRate : 0f;
            if (rate > 0f)
            {
                e.emitAcc += rate * thin * dt;
                while (e.emitAcc >= 1f)
                {
                    e.emitAcc -= 1f;
                    var ep = new ParticleSystem.EmitParams
                    {
                        position = pos - (Vector3)(dir * size * 0.5f) + (Vector3)(Random.insideUnitCircle * size * 0.2f),
                        startColor = e.kind == Kind.Missile ? missileSmokeColor : smokeColor,
                        applyShapeToPosition = true,
                    };
                    if (smoke != null) smoke.Emit(ep, 1);
                }
            }
        }

        // Wave：波の頂点（曲がる向きが変わった瞬間）で光の粒
        if (e.kind == Kind.Wave && e.fancy && speed > 0.1f)
        {
            float cross = e.lastDir.x * dir.y - e.lastDir.y * dir.x;
            if (Mathf.Abs(cross) > 0.0005f)
            {
                if (e.lastCross != 0f && Mathf.Sign(cross) != Mathf.Sign(e.lastCross) && visible)
                    EmitBurst(sparkle, pos, Mathf.Max(1, Mathf.RoundToInt(waveSparkCount * thin)), Color.Lerp(e.auraColor, Color.white, 0.45f), 1.2f);
                e.lastCross = cross;
            }
        }

        // Warp：消える瞬間・現れる瞬間の輪
        if (e.kind == Kind.Warp)
        {
            bool vis = e.visual != null && e.visual.enabled;
            if (vis != e.lastVisible && e.fancy)
                SpawnRingAnim(pos, warpRingSize * (vis ? 1.2f : 0.2f), warpRingSize * (vis ? 0.3f : 1.2f), warpRingDuration, warpRingColor, e);
            e.lastVisible = vis;
        }

        e.lastDir = dir;
        e.lastPos = pos;
    }

    // Countdown：爆発の直前に弾の絵を膨らませる
    private float CountdownSwell(Entry e)
    {
        if (e.kind != Kind.Countdown || e.explodeAt <= 0f || countdownSwellSeconds <= 0f) return 1f;
        float remain = e.explodeAt - e.age;
        if (remain > countdownSwellSeconds) return 1f;
        return Mathf.Lerp(1.35f, 1f, Mathf.Clamp01(remain / countdownSwellSeconds));
    }

    private void UpdateGhosts(Entry e, bool visible)
    {
        e.histHead = (e.histHead + 1) % e.hist.Length;
        e.hist[e.histHead] = e.tf.position;
        e.histT[e.histHead] = e.age;
        for (int i = 0; i < e.ghosts.Length; i++)
        {
            var g = e.ghosts[i];
            if (g == null) continue;
            float want = e.age - slowGhostDelay * (i + 1);
            int idx = -1;
            for (int k = 0; k < e.hist.Length; k++)
            {
                int j = (e.histHead - k + e.hist.Length) % e.hist.Length;
                if (e.histT[j] <= want && (e.histT[j] > 0f || k > 0)) { idx = j; break; }
            }
            bool on = visible && idx >= 0 && want > 0f;
            g.enabled = on;
            if (!on) continue;
            g.sprite = e.visual.sprite;
            g.transform.position = e.hist[idx];
            g.transform.rotation = e.visualTf.rotation;
            g.transform.localScale = e.visualTf.lossyScale;
            Color c = Color.Lerp(e.auraColor, Color.white, 0.2f);
            c.a = slowGhostAlpha * (1f - i / (float)(e.ghosts.Length + 1));
            g.color = c;
        }
    }

    // ---------- 単発のアニメ（閃光・輪・走る光） ----------
    private void SpawnRingAnim(Vector3 pos, float s0, float s1, float dur, Color c, Entry e)
    {
        if (ringSprite == null) return;
        int layer = e != null && e.visual != null ? e.visual.sortingLayerID : 0;
        int order = e != null && e.visual != null ? e.visual.sortingOrder + 2 : 0;
        var sr = RentSprite(ringSprite, additiveSpriteMaterial, layer, order);
        sr.transform.position = pos;
        anims.Add(new Anim { sr = sr, dur = dur, s0 = s0, s1 = s1, c = c });
    }

    private void UpdateAnims(float dt)
    {
        for (int i = anims.Count - 1; i >= 0; i--)
        {
            var a = anims[i];
            a.t += dt;
            float k = a.t / Mathf.Max(0.01f, a.dur);
            if (k >= 1f)
            {
                if (a.sr != null) ReleaseSprite(a.sr);
                if (a.lr != null) ReleaseLine(a.lr);
                anims.RemoveAt(i);
                continue;
            }
            float eased = 1f - (1f - k) * (1f - k);
            if (a.sr != null)
            {
                float s = Mathf.Lerp(a.s0, a.s1, eased);
                SetSprite(a.sr, a.sr.transform.position, Vector2.right, s, s);
                Color c = a.c; c.a *= 1f - k; a.sr.color = c;
            }
            if (a.lr != null)
            {
                // 走る光：線の先端から弾の方へ縮みながら消える
                a.lr.SetPosition(0, Vector3.Lerp(a.a, a.b, eased));
                a.lr.SetPosition(1, a.b);
                a.lr.startWidth = a.lr.endWidth = telegraphStreakWidth * (1f - k * 0.5f);
                Color c = a.c; c.a *= 1f - k;
                a.lr.startColor = c; a.lr.endColor = new Color(c.r, c.g, c.b, 0f);
            }
        }
    }

    // ---------- 共通 ----------
    private static void SetSprite(SpriteRenderer sr, Vector3 pos, Vector2 dir, float lengthWorld, float widthWorld)
    {
        sr.transform.position = pos;
        sr.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
        float s = sr.sprite != null ? Mathf.Max(0.01f, sr.sprite.bounds.size.x) : 1f;
        sr.transform.localScale = new Vector3(lengthWorld / s, widthWorld / s, 1f);
    }

    private bool TryFlashOnce(Vector3 pos)
    {
        float now = Time.time;
        for (int i = recentFlashes.Count - 1; i >= 0; i--)
        {
            if (now - recentFlashes[i].time > 0.1f) { recentFlashes.RemoveAt(i); continue; }
            if (Vector3.Distance(recentFlashes[i].pos, pos) < 0.6f) return false; // 同時に出た複数の弾では1回だけ
        }
        recentFlashes.Add((pos, now));
        return true;
    }

    private static void EmitBurst(ParticleSystem ps, Vector3 pos, int count, Color color, float speed)
    {
        if (ps == null || count <= 0) return;
        var ep = new ParticleSystem.EmitParams { position = pos, startColor = color };
        for (int i = 0; i < count; i++)
        {
            float ang = Random.Range(0f, Mathf.PI * 2f);
            ep.velocity = new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), 0f) * speed * Random.Range(0.5f, 1.2f);
            ps.Emit(ep, 1);
        }
    }

    private SpriteRenderer RentSprite(Sprite sprite, Material mat, int layer, int order)
    {
        SpriteRenderer sr = null;
        while (freeSprites.Count > 0 && sr == null) sr = freeSprites.Pop();
        if (sr == null)
        {
            var go = new GameObject("BulletFX_Sprite");
            go.transform.SetParent(transform, false);
            sr = go.AddComponent<SpriteRenderer>();
        }
        if (mat != null) sr.sharedMaterial = mat;
        sr.sprite = sprite;
        sr.sortingLayerID = layer;
        sr.sortingOrder = order;
        sr.color = Color.white;
        sr.gameObject.SetActive(true);
        sr.enabled = true;
        return sr;
    }

    private void ReleaseSprite(SpriteRenderer sr)
    {
        if (sr == null) return;
        sr.enabled = false;
        sr.gameObject.SetActive(false);
        freeSprites.Push(sr);
    }

    private LineRenderer RentLine(int layer, int order)
    {
        LineRenderer lr = null;
        while (freeLines.Count > 0 && lr == null) lr = freeLines.Pop();
        if (lr == null)
        {
            var go = new GameObject("BulletFX_Line");
            go.transform.SetParent(transform, false);
            lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.positionCount = 2;
            lr.numCapVertices = 2;
            lr.alignment = LineAlignment.View;
            if (lineMaterial != null) lr.sharedMaterial = lineMaterial;
        }
        lr.sortingLayerID = layer;
        lr.sortingOrder = order;
        lr.gameObject.SetActive(true);
        return lr;
    }

    private void ReleaseLine(LineRenderer lr)
    {
        if (lr == null) return;
        lr.gameObject.SetActive(false);
        freeLines.Push(lr);
    }

    private static bool IsOnScreen(Vector3 p)
    {
        var cam = Camera.main;
        if (cam == null) return true;
        Vector3 v = cam.WorldToViewportPoint(p);
        return v.x > -0.05f && v.x < 1.05f && v.y > -0.05f && v.y < 1.05f;
    }
}
