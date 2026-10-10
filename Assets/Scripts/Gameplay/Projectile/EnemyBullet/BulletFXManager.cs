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
/// ・Countdown弾の爆発：①半径に比例 ②閃光 ③火球 ④衝撃波リング ⑤火花 ⑥黒煙 ⑦光の筋 ⑧画面の揺れ ⑨反射済みは色を変える（旧VFX_Explosion_A_RingSparksの代わり）
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
    [Tooltip("分裂の閃光の大きさの倍率（リングの大きさ・火花の数）")]
    public float warheadSplitScale = 1.6f;
    [Tooltip("分裂した子弾にも、親弾と同じ色のオーラと出現の「ポン」を付ける")]
    public bool warheadChildFxEnabled = true;

    [Header("Smoke：漂う煙（暗い背景でも見える紫がかった灰色）")]
    [Tooltip("煙の量（個/秒）")]
    public float smokeTrailRate = 24f;
    [Tooltip("煙の色。以前の黒に近い色は暗い背景（NeonDancer等）でほとんど見えなかった")]
    public Color smokeTrailColor = new Color(0.5f, 0.42f, 0.62f, 0.7f);
    [Tooltip("煙1つの大きさ（弾の大きさに対する倍率）")]
    public float smokeTrailSizeMul = 1.1f;
    [Tooltip("弾の周りのオーラがゆっくり脈打つ速さ・強さ")]
    public float smokePulseSpeed = 3f;
    [Range(0f, 1f)] public float smokePulseAmount = 0.3f;

    [Header("Countdown弾の爆発（旧VFX_Explosion_A_RingSparksの代わりに出す）")]
    [Tooltip("ONで新しい爆発演出を出し、旧VFX（EnemyBullet.prefab > EnemyBulletFeedback > Explosion Vfx Prefab）は出さない。OFFで旧VFXに戻る")]
    public bool explosionFxEnabled = true;
    [Tooltip("1フレームに出す爆発演出の上限（超えた分は演出なし）")]
    public int maxExplosionsPerFrame = 3;
    [Tooltip("① 大きさの基準にする爆発半径。爆発半径がこの値の時に下の各サイズになり、半径に比例して大きくなる")]
    public float explosionReferenceRadius = 1.25f;
    [Header("② 閃光")]
    public bool explosionFlashEnabled = true;
    public float explosionFlashSize = 2.2f;
    public float explosionFlashDuration = 0.1f;
    [Header("③ 火球（膨らみながら暗い赤へ）")]
    public bool explosionFireballEnabled = true;
    public int explosionFireballCount = 3;
    public float explosionFireballSize = 1.6f;
    public float explosionFireballDuration = 0.4f;
    [Header("④ 衝撃波リング（爆発半径ちょうどまで広がる）")]
    public bool explosionRingEnabled = true;
    public float explosionRingDuration = 0.28f;
    [Header("⑤ 放射状の火花・破片")]
    public bool explosionSparksEnabled = true;
    public int explosionSparkCount = 22;
    [Tooltip("火花の速さ（半径1あたり）")]
    public float explosionSparkSpeed = 6f;
    public float explosionSparkSize = 0.09f;
    [Header("⑥ 黒煙")]
    public bool explosionSmokeEnabled = true;
    public int explosionSmokeCount = 7;
    public float explosionSmokeSize = 0.55f;
    [Header("⑦ 光の筋")]
    public bool explosionRaysEnabled = true;
    public int explosionRayCount = 8;
    public float explosionRayWidth = 0.12f;
    public float explosionRayDuration = 0.25f;
    [Header("⑧ 画面の小さな揺れ")]
    public bool explosionShakeEnabled = true;
    public float explosionShakeDuration = 0.12f;
    public float explosionShakeMagnitude = 0.06f;
    [Tooltip("揺れの最短間隔（秒）")]
    public float explosionShakeMinInterval = 0.3f;
    [Header("色（敵の爆発）")]
    public Color explosionFlashColor = new Color(1f, 0.95f, 0.8f, 1f);
    public Color explosionFireColor = new Color(1f, 0.55f, 0.15f, 1f);
    public Color explosionFireEndColor = new Color(0.55f, 0.07f, 0.04f, 1f);
    public Color explosionSmokeColor = new Color(0.1f, 0.09f, 0.09f, 0.75f);
    [Header("⑨ 反射済みの弾の爆発の色（ノーマル／ジャスト）")]
    public bool explosionReflectedColorEnabled = true;
    public Color explosionReflectedNormalColor = new Color(0.35f, 0.95f, 1f, 1f);
    public Color explosionReflectedNormalEndColor = new Color(0.1f, 0.3f, 0.75f, 1f);
    public Color explosionReflectedJustColor = new Color(1f, 0.6f, 0.15f, 1f);
    public Color explosionReflectedJustEndColor = new Color(1f, 0.2f, 0.1f, 1f);
    [Tooltip("反射済みの爆発の煙（敵の黒煙の代わりに明るい煙を少なめに出す）")]
    public Color explosionReflectedSmokeColor = new Color(0.75f, 0.8f, 0.85f, 0.4f);

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

    /// <summary>
    /// Countdown弾が爆発した時（EnemyBulletFeedback.OnExplosion）。新しい爆発演出を出したらtrue（旧VFXは出さない）。
    /// プレハブが無い・OFFの時はfalse（旧VFXを出す）
    /// </summary>
    public static bool TryPlayExplosion(EnemyBullet b, Vector3 pos)
    {
        if (b == null) return false;
        var m = Instance;
        if (m == null || !m.fxEnabled || !m.explosionFxEnabled) return false;
        m.PlayExplosion(b, pos);
        return true;
    }

    /// <summary>未反射弾の色（オーラの色）。登録されていなければfalse（弾同士の衝突演出で使う。ReflectedBulletFXManager）</summary>
    public static bool TryGetBulletColor(EnemyBullet b, out Color color)
    {
        color = Color.white;
        var m = s_instance;
        if (m == null || b == null || !m.byBullet.TryGetValue(b, out Entry e)) return false;
        color = e.auraColor;
        return true;
    }

    /// <summary>
    /// 位置を指定してCountdown弾と同じ爆発演出を出す（GravePoleのアタックブロック。BreakFXManager）。
    /// colorMode：0＝敵の爆発 1＝反射済み・ノーマル 2＝反射済み・ジャスト。出したらtrue
    /// </summary>
    public static bool TryPlayExplosionAt(Vector3 pos, float radius, int colorMode, int layer, int order)
    {
        var m = Instance;
        if (m == null || !m.fxEnabled || !m.explosionFxEnabled) return false;
        if (m.explosionFrame != Time.frameCount) { m.explosionFrame = Time.frameCount; m.explosionCountThisFrame = 0; }
        if (m.explosionCountThisFrame >= m.maxExplosionsPerFrame) return true;
        m.explosionCountThisFrame++;
        m.PlayExplosionCore(pos, radius, colorMode > 0, colorMode == 2, layer, order);
        return true;
    }

    /// <summary>trueの間に登録されたTelegraph弾は「線の上を走る光」を出さない（予告線なしで撃つ時に、撃つ側がtrueにしてから撃ち、終わったらfalseに戻す）</summary>
    public static bool SkipTelegraphStreak;

    /// <summary>
    /// Warhead（MultiWarhead）の分裂した子弾を登録する（EnemyBullet.MultiWarhead）。子弾は通常の撃ち方を通らないため、
    /// ここで親弾と同じ色（未反射トレイルの色）のオーラと出現の「ポン」を付ける
    /// </summary>
    public static void RegisterWarheadChild(EnemyBullet child, Color baseColor)
    {
        if (child == null) return;
        var m = Instance;
        if (m == null || !m.fxEnabled || !m.warheadChildFxEnabled) return;
        m.Add(child, null, Kind.Normal, baseColor);
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

    private class Anim { public SpriteRenderer sr; public LineRenderer lr; public float t, dur, s0, s1, width; public Color c, c2; public bool useC2, ray; public Vector3 a, b; }
    private int explosionFrame = -1, explosionCountThisFrame;
    private float lastExplosionShakeTime = -999f;
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

    private void Add(EnemyBullet b, EnemyData.BulletType bt, Kind? kindOverride = null, Color? colorOverride = null)
    {
        if (byBullet.ContainsKey(b)) Remove(byBullet[b]);
        var tag = b.GetComponent<BulletFXTag>();
        if (tag == null) tag = b.gameObject.AddComponent<BulletFXTag>();
        tag.bullet = b;

        var e = new Entry { b = b, tf = b.transform, kind = kindOverride ?? Classify(bt), seed = Random.value * 10f };
        Transform v = b.transform.Find("Visual");
        e.visual = v != null ? v.GetComponent<SpriteRenderer>() : null;
        e.visualTf = e.visual != null ? e.visual.transform : null;
        e.visualBaseScale = e.visualTf != null ? e.visualTf.localScale : Vector3.one;
        Transform tr = b.transform.Find("Trail");
        e.trail = tr != null ? tr.GetComponent<TrailRenderer>() : null;
        e.rb = b.GetComponent<Rigidbody2D>();
        e.lastPos = e.tf.position;
        e.explodeAt = bt != null && bt.useCountdownExplosion ? Mathf.Max(0.1f, bt.explosionDelaySeconds) : 0f;
        Color baseC = colorOverride ?? (bt != null && bt.useUnreflectedTrail ? bt.unreflectedTrailColor : (bt != null && bt.useColorOverride ? bt.colorOverride : DefaultColor(e.kind)));
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
        // ★予告線を出さずに撃つTelegraph弾（NeonDancerの6way）は、線の上を走る光も出さない（線が無いのに光だけ出ると、発射前の線のように見えるため）
        if (e.kind == Kind.Telegraph && lineMaterial != null && !SkipTelegraphStreak)
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
            float ws = Mathf.Max(0.1f, warheadSplitScale);
            EmitBurst(sparkle, e.lastPos, Mathf.RoundToInt(warheadSplitSparks * ws), warheadOrbitColor, 3f * Mathf.Sqrt(ws));
            SpawnRingAnim(e.lastPos, 0.2f * ws, 1.2f * ws, 0.24f, warheadOrbitColor, e);
            SpawnRingAnim(e.lastPos, 0.1f * ws, 0.7f * ws, 0.18f, Color.Lerp(warheadOrbitColor, Color.white, 0.6f), e);
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
                    case Kind.Smoke:
                        pulse = 1f + smokePulseAmount * Mathf.Sin(e.age * smokePulseSpeed + e.seed);
                        break;
                }
                float stretch = 1f + Mathf.Min(stretchMax - 1f, speed * stretchPerSpeed);
                if (e.kind == Kind.Rapid) stretch *= rapidStretchMul;
                if (e.kind == Kind.SpeedCurve) stretch *= 1f + Mathf.Clamp01(speed / 12f);
                // 伸ばしたオーラは、先端の位置を伸ばす前と同じ（弾の少し前）に固定し、後ろへだけ伸ばす（以前は中心のままで、伸びた分の半分が弾より前に飛び出していた）
                float auraLen = size * auraSizeMul * pulse * stretch;
                float front = size * auraSizeMul * pulse * 0.5f;
                SetSprite(e.aura, pos + (Vector3)(dir * (front - auraLen * 0.5f)), dir, auraLen, size * auraSizeMul * pulse * (e.kind == Kind.Rapid ? 0.6f : 1f));
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
            float rate = e.kind == Kind.Missile ? missileSmokeRate : e.kind == Kind.Smoke ? smokeTrailRate : 0f;
            if (rate > 0f)
            {
                e.emitAcc += rate * thin * dt;
                while (e.emitAcc >= 1f)
                {
                    e.emitAcc -= 1f;
                    var ep = new ParticleSystem.EmitParams
                    {
                        position = pos - (Vector3)(dir * size * 0.5f) + (Vector3)(Random.insideUnitCircle * size * 0.2f),
                        startColor = e.kind == Kind.Missile ? missileSmokeColor : smokeTrailColor,
                        applyShapeToPosition = true,
                    };
                    if (e.kind == Kind.Smoke)
                    {
                        ep.startSize = size * smokeTrailSizeMul * Random.Range(0.7f, 1.3f);
                        ep.rotation = Random.Range(0f, 360f);
                    }
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

    // ---------- Countdown弾の爆発 ----------
    private void PlayExplosion(EnemyBullet b, Vector3 pos)
    {
        if (explosionFrame != Time.frameCount) { explosionFrame = Time.frameCount; explosionCountThisFrame = 0; }
        if (explosionCountThisFrame >= maxExplosionsPerFrame) return;
        explosionCountThisFrame++;

        bool reflected0 = b.IsReflected || b.HasPaddleReflectedOnce;
        bool just0 = b.DamageMultiplier > 1.0001f;
        Transform v0 = b.transform.Find("Visual");
        var vsr0 = v0 != null ? v0.GetComponent<SpriteRenderer>() : null;
        PlayExplosionCore(pos, b.ExplosionRadius, reflected0, just0, vsr0 != null ? vsr0.sortingLayerID : 0, vsr0 != null ? vsr0.sortingOrder + 5 : 20);
    }

    private void PlayExplosionCore(Vector3 pos, float radiusIn, bool reflectedIn, bool justIn, int layer, int order)
    {
        float radius = Mathf.Max(0.2f, radiusIn);
        float k = radius / Mathf.Max(0.1f, explosionReferenceRadius); // ① 半径に比例

        // ⑨ 反射済みの弾は色を変える
        bool reflected = explosionReflectedColorEnabled && reflectedIn;
        bool just = justIn;
        Color fire = reflected ? (just ? explosionReflectedJustColor : explosionReflectedNormalColor) : explosionFireColor;
        Color fireEnd = reflected ? (just ? explosionReflectedJustEndColor : explosionReflectedNormalEndColor) : explosionFireEndColor;
        Color flash = reflected ? Color.Lerp(fire, Color.white, 0.7f) : explosionFlashColor;
        Color smokeC = reflected ? explosionReflectedSmokeColor : explosionSmokeColor;

        // ⑥ 黒煙（先に出して火球の下に）
        if (explosionSmokeEnabled && smoke != null)
        {
            int n = reflected ? Mathf.Max(1, explosionSmokeCount / 2) : explosionSmokeCount;
            for (int i = 0; i < n; i++)
            {
                Vector2 d = Random.insideUnitCircle;
                var ep = new ParticleSystem.EmitParams
                {
                    position = pos + (Vector3)(d * radius * 0.35f),
                    velocity = (Vector3)(d * radius * 0.8f + Vector2.up * 0.25f),
                    startColor = smokeC,
                    startSize = explosionSmokeSize * k * Random.Range(0.7f, 1.2f),
                    startLifetime = Random.Range(0.7f, 1.0f),
                    rotation = Random.Range(0f, 360f),
                };
                smoke.Emit(ep, 1);
            }
        }
        // ③ 火球
        if (explosionFireballEnabled)
        {
            for (int i = 0; i < explosionFireballCount; i++)
            {
                Vector3 p = pos + (Vector3)(Random.insideUnitCircle * radius * 0.25f * (i == 0 ? 0f : 1f));
                float sz = explosionFireballSize * k * (i == 0 ? 1f : Random.Range(0.55f, 0.8f));
                var sr = RentSprite(glowSprite, additiveSpriteMaterial, layer, order);
                sr.transform.position = p;
                anims.Add(new Anim { sr = sr, dur = explosionFireballDuration * Random.Range(0.85f, 1.1f), s0 = sz * 0.35f, s1 = sz, c = fire, c2 = fireEnd, useC2 = true });
            }
        }
        // ④ 衝撃波リング（リング画像の輪は直径の0.4倍の半径なので、直径＝半径×2.5で爆発半径ちょうど）
        if (explosionRingEnabled && ringSprite != null)
        {
            var sr = RentSprite(ringSprite, additiveSpriteMaterial, layer, order + 1);
            sr.transform.position = pos;
            anims.Add(new Anim { sr = sr, dur = explosionRingDuration, s0 = radius * 0.5f, s1 = radius * 2.5f, c = Color.Lerp(fire, Color.white, 0.3f) });
            var sr2 = RentSprite(ringSprite, additiveSpriteMaterial, layer, order + 1);
            sr2.transform.position = pos;
            anims.Add(new Anim { sr = sr2, dur = explosionRingDuration * 0.75f, s0 = radius * 0.2f, s1 = radius * 1.6f, c = new Color(1f, 1f, 1f, 0.7f) });
        }
        // ② 閃光
        if (explosionFlashEnabled)
        {
            var sr = RentSprite(glowSprite, additiveSpriteMaterial, layer, order + 2);
            sr.transform.position = pos;
            float fs = explosionFlashSize * k;
            anims.Add(new Anim { sr = sr, dur = explosionFlashDuration, s0 = fs, s1 = fs * 1.3f, c = flash });
        }
        // ⑦ 光の筋
        if (explosionRaysEnabled && lineMaterial != null)
        {
            float off = Random.Range(0f, 360f);
            for (int i = 0; i < explosionRayCount; i++)
            {
                float ang = (off + i * 360f / Mathf.Max(1, explosionRayCount) + Random.Range(-10f, 10f)) * Mathf.Deg2Rad;
                Vector3 d = new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), 0f);
                var lr = RentLine(layer, order + 2);
                anims.Add(new Anim { lr = lr, ray = true, dur = explosionRayDuration, a = pos, b = pos + d * radius * Random.Range(0.9f, 1.4f), width = explosionRayWidth * Mathf.Sqrt(k), c = Color.Lerp(fire, Color.white, i % 2 == 0 ? 0.3f : 0.65f) });
            }
        }
        // ⑤ 放射状の火花・破片
        if (explosionSparksEnabled && sparkle != null)
        {
            Color sc = Color.Lerp(fire, Color.white, 0.35f);
            for (int i = 0; i < explosionSparkCount; i++)
            {
                float ang = Random.Range(0f, Mathf.PI * 2f);
                var ep = new ParticleSystem.EmitParams
                {
                    position = pos,
                    velocity = new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), 0f) * explosionSparkSpeed * radius * Random.Range(0.4f, 1.1f),
                    startColor = i % 3 == 0 ? fireEnd : sc,
                    startSize = explosionSparkSize * Random.Range(0.6f, 1.4f),
                    startLifetime = Random.Range(0.25f, 0.5f),
                };
                sparkle.Emit(ep, 1);
            }
        }
        // ⑧ 画面の小さな揺れ
        if (explosionShakeEnabled && Time.unscaledTime - lastExplosionShakeTime >= explosionShakeMinInterval)
        {
            lastExplosionShakeTime = Time.unscaledTime;
            CameraShake.Shake(explosionShakeDuration, explosionShakeMagnitude * Mathf.Clamp(k, 0.5f, 2f));
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
                Color c = a.useC2 ? Color.Lerp(a.c, a.c2, k) : a.c; c.a *= 1f - k; a.sr.color = c;
            }
            if (a.lr != null)
            {
                if (a.ray)
                {
                    // 爆発の光の筋：中心から外へ伸びながら、根元から消えていく
                    Color rc = a.c; rc.a *= 1f - k;
                    a.lr.SetPosition(0, Vector3.Lerp(a.a, a.b, Mathf.Clamp01(k * 1.3f - 0.3f)));
                    a.lr.SetPosition(1, Vector3.Lerp(a.a, a.b, eased));
                    a.lr.startWidth = a.width * (1f - k);
                    a.lr.endWidth = a.width * 0.3f * (1f - k);
                    a.lr.startColor = rc; a.lr.endColor = new Color(rc.r, rc.g, rc.b, 0f);
                    continue;
                }
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
