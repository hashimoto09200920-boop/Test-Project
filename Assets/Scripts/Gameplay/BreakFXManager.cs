using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ブロック等が壊れた時の演出の管理役。Assets/Resources/BreakFX.prefab から1つだけ作られる
/// （プレハブが無ければ何もしない＝従来の破壊VFX。メニュー「Tools/弾の見た目/8 …」で作成・各プレハブへ種類を設定）。
/// ・A 本物のブロック（WallHealthのBreak Fx Style＝Block、Golemの岩）：A1 破片（ブロック自身の絵）／A2 砂ぼこり／A3 小石／A4 ひびと閃光／
///   A5 壊した弾の色のリング／A6 ジャストの特別版／A7 アイテムが出た所の光の柱とキラキラ／A8 連続破壊／A9 画面の揺れ
/// ・B 機械・エネルギー系（Style＝Machine：Bit・WeakPoint・NeonDancerのフロアとライト）：B1 縮んでから爆発／B2 ショートの稲妻／
///   B3 金属片／B4 火花の雨／B5 黒煙／B6 エネミーの色（WallHealthのBreak Fx Color）／B7 二重リングと揺れ
/// ・C GravePoleのアタックブロック：Countdown弾の新しい爆発（BulletFXManager）を使い回し、プレイヤーが壊した時は弾の色＋破片
/// ・D GravePoleのオーブ：D1 光の柱／D2 脈打つリング／D3 ダンサーへ光が飛んで光る／D4 発光中に回る光／D5 終わりにしぼむ
/// ・E Shamanの霊火（Style＝Flame）：E1 火の粉が舞い上がり青白い煙になって消える
/// ・負荷対策：パーティクルは共有（Emitで出す）、画像・線は貸し出しで使い回し、1フレームの数に上限
/// </summary>
[DisallowMultipleComponent]
public class BreakFXManager : MonoBehaviour
{
    [Header("全体")]
    [Tooltip("OFFにすると全ての演出を止める（従来の破壊VFX）")]
    public bool fxEnabled = true;
    [Tooltip("1フレームに出す破壊演出の上限（Fortressのブロックがまとめて壊れた時の重なり防止）")]
    public int maxBreaksPerFrame = 4;
    [Tooltip("同時に動いている単発演出の上限")]
    public int maxActiveAnims = 120;

    [Header("共有のパーティクル・画像（メニューで自動設定）")]
    public ParticleSystem sparkle;
    [Tooltip("煙（アルファ）")]
    public ParticleSystem smoke;
    [Tooltip("重力で落ちる小石（アルファ）")]
    public ParticleSystem pebbles;
    [Tooltip("重力で落ちる火花（加算）")]
    public ParticleSystem sparkRain;
    public Sprite glowSprite;
    public Sprite ringSprite;
    public Sprite metalSprite;
    [Tooltip("加算の画像用マテリアル")]
    public Material additiveSpriteMaterial;
    [Tooltip("線（ひび・稲妻）のマテリアル（加算）")]
    public Material lineMaterial;
    [Tooltip("並び順（壊れた物の手前に出す量）")]
    public int sortingOrderOffset = 5;

    [Header("色")]
    public Color normalColor = new Color(0.35f, 0.95f, 1f, 1f);
    public Color justColor = new Color(1f, 0.5f, 0.12f, 1f);
    [Tooltip("弾の色が分からない時（爆発で壊れた等）のリングの色")]
    public Color neutralColor = new Color(1f, 0.95f, 0.85f, 1f);
    public Color dustColor = new Color(0.62f, 0.58f, 0.52f, 0.6f);
    public Color pebbleColor = new Color(0.55f, 0.5f, 0.45f, 1f);
    [Tooltip("機械系でWallHealthのBreak Fx Colorが未設定の時の色")]
    public Color machineColor = new Color(0.6f, 0.85f, 1f, 1f);
    [Tooltip("霊火でWallHealthのBreak Fx Colorが未設定の時の色")]
    public Color flameColor = new Color(0.55f, 0.85f, 1f, 1f);

    [Header("A1 破片（ブロック自身の絵を小さくして飛ばす）")]
    public bool blockShardsEnabled = true;
    public int blockShardCount = 5;
    [Tooltip("破片の大きさ（ブロックの大きさに対する割合）")]
    public float blockShardSizeMul = 0.32f;
    public float blockShardSpeed = 3.5f;
    public float blockShardGravity = 9f;
    public float blockShardDuration = 0.7f;

    [Header("A2 砂ぼこり")]
    public bool dustEnabled = true;
    public int dustCount = 5;

    [Header("A3 小石")]
    public bool pebblesEnabled = true;
    public int pebbleCount = 10;
    public float pebbleSpeed = 3f;

    [Header("A4 ひびと閃光")]
    public bool crackFlashEnabled = true;
    public int crackCount = 4;
    public float crackWidth = 0.05f;
    public float crackDuration = 0.12f;

    [Header("A5 壊した弾の色のリング")]
    public bool blockRingEnabled = true;
    public float blockRingDuration = 0.25f;

    [Header("A6 ジャストで壊した時の特別版")]
    public bool justSpecialEnabled = true;
    public float justShardMul = 1.6f;
    public float justSpeedMul = 1.4f;
    public int justSparkCount = 12;

    [Header("A7 アイテムが出た所の光の柱とキラキラ")]
    public bool itemPillarEnabled = true;
    public float itemPillarHeight = 2.2f;
    public float itemPillarDuration = 0.55f;
    public Color itemGoldColor = new Color(1f, 0.85f, 0.2f, 1f);
    public Color itemLifeColor = new Color(1f, 0.35f, 0.4f, 1f);

    [Header("A8 連続破壊の盛り上がり")]
    public bool comboEnabled = true;
    public float comboWindow = 0.6f;
    public float comboStep = 0.1f;
    public int comboMaxSteps = 5;

    [Header("A9 画面の小さな揺れ（ブロック）")]
    public bool blockShakeEnabled = true;
    [Tooltip("OFFなら大きいブロック（下の大きさ以上）だけ揺らす")]
    public bool shakeAllBlocks = false;
    [Tooltip("大きいブロックとみなす大きさ（ワールド単位の長い辺）")]
    public float bigBlockSize = 1.2f;
    public float blockShakeDuration = 0.1f;
    public float blockShakeMagnitude = 0.05f;
    public float shakeMinInterval = 0.25f;

    [Header("B 機械・エネルギー系")]
    [Tooltip("B1 縮む時間（秒）")]
    public float machineImplodeSeconds = 0.08f;
    public float machineFlashSize = 1.8f;
    [Tooltip("閃光・リングの大きさの基準にする大きさの上限（ワールド単位）。横に長いエネミーダンサーのフロア等で、画面を覆うほど大きな閃光にならないように")]
    public float machineGlowMaxSize = 1.6f;
    [Tooltip("閃光（縮む光・爆発の光）の明るさ（0〜1）。まぶしすぎる時は下げる")]
    [Range(0f, 1f)] public float machineFlashAlpha = 0.55f;
    [Tooltip("閃光の白っぽさ（0＝エネミーの色のまま、1＝真っ白）")]
    [Range(0f, 1f)] public float machineFlashWhite = 0.3f;
    public int machineBoltCount = 4;
    public float machineBoltLength = 1.2f;
    public float machineBoltWidth = 0.05f;
    public int machineMetalCount = 7;
    public float machineMetalSize = 0.22f;
    public float machineMetalSpeed = 4.5f;
    public int machineSparkRainCount = 18;
    public float machineSparkRainSpeed = 4f;
    public int machineSmokeCount = 4;
    public Color machineSmokeColor = new Color(0.1f, 0.1f, 0.12f, 0.7f);
    public bool machineShakeEnabled = true;
    public float machineShakeDuration = 0.14f;
    public float machineShakeMagnitude = 0.07f;

    [Header("C GravePoleのアタックブロック")]
    [Tooltip("爆発の半径（Countdown弾の新しい爆発の大きさ）")]
    public float attackBlockExplosionRadius = 1.4f;

    [Header("D GravePoleのオーブ")]
    public float orbPillarHeight = 3f;
    public float orbPillarDuration = 0.6f;
    public int orbPulseCount = 3;
    public float orbPulseInterval = 0.15f;
    public float orbPulseSize = 2.2f;
    public int orbFlyCount = 5;
    public float orbFlySeconds = 0.55f;
    public float orbFlySize = 0.3f;
    [Tooltip("光が着いた時にダンサーが光る時間")]
    public float dancerFlashSeconds = 0.25f;
    public int orbOrbiterCount = 4;
    public float orbOrbitRadius = 0.8f;
    public float orbOrbitSpeed = 200f;
    public float orbEndDuration = 0.35f;

    [Header("E Shamanの霊火")]
    public int flameEmberCount = 18;
    public float flameEmberSpeed = 2f;
    public int flameSmokeCount = 5;
    public Color flameSmokeColor = new Color(0.7f, 0.85f, 1f, 0.45f);

    // ---------- 共有インスタンス ----------
    private static BreakFXManager s_instance;
    private static bool s_triedLoad;

    public static BreakFXManager Instance
    {
        get
        {
            if (s_instance != null) return s_instance;
            if (s_triedLoad) return null;
            s_triedLoad = true;
            var prefab = Resources.Load<BreakFXManager>("BreakFX");
            if (prefab == null) return null;
            s_instance = Instantiate(prefab);
            s_instance.name = "BreakFX";
            return s_instance;
        }
    }

    private static bool Ready(out BreakFXManager m) { m = Instance; return m != null && m.fxEnabled; }

    // ---------- 外から呼ぶ入口（出したらtrue＝呼び出し側は旧VFXを出さない） ----------
    /// <summary>WallHealthが壊れた時。style：0＝Block 1＝Machine 2＝Flame。hitState：0＝不明 1＝ノーマル 2＝ジャスト</summary>
    public static bool TryPlayWallBreak(int style, SpriteRenderer sr, Transform tf, Vector3 hitPoint, int hitState, Color customColor)
    {
        if (!Ready(out var m)) return false;
        if (!m.AllowBreak()) return true;
        Vector3 center; float size; GetShape(sr, tf, out center, out size);
        int layer = sr != null ? sr.sortingLayerID : 0, order = (sr != null ? sr.sortingOrder : 10) + m.sortingOrderOffset;
        if (style == 1) m.PlayMachine(center, size, customColor.a > 0.01f ? customColor : m.machineColor, layer, order);
        else if (style == 2) m.PlayFlame(center, size, customColor.a > 0.01f ? customColor : m.flameColor, layer, order);
        else m.PlayBlock(center, size, sr, hitPoint, hitState, layer, order);
        return true;
    }

    /// <summary>Golemの岩が壊れた時</summary>
    public static bool TryPlayRockBreak(SpriteRenderer sr, Vector3 hitPoint, bool just)
    {
        if (!Ready(out var m)) return false;
        if (!m.AllowBreak()) return true;
        GetShape(sr, sr != null ? sr.transform : null, out Vector3 center, out float size);
        if (sr == null) center = hitPoint;
        int layer = sr != null ? sr.sortingLayerID : 0, order = (sr != null ? sr.sortingOrder : 10) + m.sortingOrderOffset;
        m.PlayBlock(center, size, sr, hitPoint, just ? 2 : 1, layer, order);
        return true;
    }

    /// <summary>アタックブロックが起爆した時。byPlayer＝プレイヤーが壊した（just＝ジャスト弾で）</summary>
    public static bool TryPlayAttackBlock(SpriteRenderer sr, Vector3 pos, bool byPlayer, bool just)
    {
        if (!Ready(out var m)) return false;
        int layer = sr != null ? sr.sortingLayerID : 0, order = (sr != null ? sr.sortingOrder : 10) + m.sortingOrderOffset;
        int colorMode = byPlayer ? (just ? 2 : 1) : 0;
        if (!BulletFXManager.TryPlayExplosionAt(pos, m.attackBlockExplosionRadius, colorMode, layer, order)) return false;
        if (byPlayer && m.blockShardsEnabled && sr != null)
        {
            GetShape(sr, sr.transform, out Vector3 c, out float size);
            m.SpawnBlockShards(c, size, sr, pos, just, layer, order);
        }
        return true;
    }

    /// <summary>ブロックからアイテムが出た時（BlockItemManager）</summary>
    public static void NotifyItemSpawned(Vector3 pos, bool gold)
    {
        if (!Ready(out var m) || !m.itemPillarEnabled) return;
        m.PlayItemPillar(pos, gold ? m.itemGoldColor : m.itemLifeColor);
    }

    /// <summary>GravePoleのオーブが発光した時</summary>
    public static void NotifyOrbActivated(Transform orb, Color color, SpriteRenderer orbSr)
    {
        if (orb == null || !Ready(out var m)) return;
        m.OnOrbActivated(orb, color, orbSr);
    }

    /// <summary>GravePoleのオーブの発光が終わった時</summary>
    public static void NotifyOrbDeactivated(Transform orb)
    {
        var m = s_instance;
        if (m == null || orb == null) return;
        m.OnOrbDeactivated(orb);
    }

    // ---------- 内部 ----------
    private class Anim
    {
        public SpriteRenderer sr; public LineRenderer lr; public float t, dur, s0, s1, aspect = 1f, rot; public Color c;
        public Transform followSrc; public SpriteRenderer copySrc; // ダンサーの発光
    }
    private class Shard { public SpriteRenderer sr; public Vector3 pos, vel; public float rot, spin, t, dur, size, gravity; public Color c; }
    private class Flyer { public SpriteRenderer sr; public Vector3 a, ctrl; public float t, dur, size, delay; public Color c; public System.Func<Vector3> target; public System.Action onArrive; }
    private class Orbiters { public Transform orb; public SpriteRenderer[] srs; public float angle; public Color c; }
    private struct Delayed { public float time; public System.Action act; }

    private readonly List<Anim> anims = new List<Anim>();
    private readonly List<Shard> shards = new List<Shard>();
    private readonly List<Flyer> flyers = new List<Flyer>();
    private readonly List<Orbiters> orbiters = new List<Orbiters>();
    private readonly List<Delayed> delayed = new List<Delayed>();
    private readonly Stack<SpriteRenderer> freeAdd = new Stack<SpriteRenderer>();
    private readonly Stack<SpriteRenderer> freeNormal = new Stack<SpriteRenderer>();
    private readonly Stack<LineRenderer> freeLines = new Stack<LineRenderer>();
    private ParticleSystem[] particleList;
    private float lastParticleSpeed = float.NaN;
    private Material defaultSpriteMaterial;
    private int breakFrame = -1, breakCount, combo;
    private float lastBreakTime = -999f, lastShakeTime = -999f, clock;
    private static readonly Vector3[] s_pts = new Vector3[5];

    private void Awake()
    {
        if (s_instance == null) s_instance = this;
        particleList = new[] { sparkle, smoke, pebbles, sparkRain };
        foreach (var ps in particleList)
        {
            if (ps == null) continue;
            var em = ps.emission; em.enabled = false;
            if (!ps.isPlaying) ps.Play(false);
        }
        // 破片（ブロック自身の絵）は普通のスプライト描画にする。新しいSpriteRendererの既定マテリアルを覚えておく
        var tmp = new GameObject("BreakFX_MatProbe");
        tmp.transform.SetParent(transform, false);
        defaultSpriteMaterial = tmp.AddComponent<SpriteRenderer>().sharedMaterial;
        Destroy(tmp);
    }

    private void OnDestroy()
    {
        if (s_instance == this) { s_instance = null; s_triedLoad = false; }
    }

    private bool AllowBreak()
    {
        if (breakFrame != Time.frameCount) { breakFrame = Time.frameCount; breakCount = 0; }
        if (breakCount >= maxBreaksPerFrame) return false;
        breakCount++;
        return true;
    }

    private static void GetShape(SpriteRenderer sr, Transform tf, out Vector3 center, out float size)
    {
        if (sr != null && sr.sprite != null)
        {
            Bounds b = sr.bounds;
            center = b.center;
            size = Mathf.Max(0.2f, Mathf.Max(b.size.x, b.size.y));
            return;
        }
        center = tf != null ? tf.position : Vector3.zero;
        size = 0.6f;
    }

    private float ComboScale()
    {
        float now = Time.unscaledTime;
        combo = comboEnabled && now - lastBreakTime <= comboWindow ? combo + 1 : 1;
        lastBreakTime = now;
        return comboEnabled ? 1f + Mathf.Min(combo - 1, comboMaxSteps) * comboStep : 1f;
    }

    private void Shake(float dur, float mag)
    {
        if (Time.unscaledTime - lastShakeTime < shakeMinInterval) return;
        lastShakeTime = Time.unscaledTime;
        CameraShake.Shake(dur, mag);
    }

    // ---------- A 本物のブロック ----------
    private void PlayBlock(Vector3 c, float size, SpriteRenderer sr, Vector3 hitPoint, int hitState, int layer, int order)
    {
        float scale = ComboScale();
        bool just = hitState == 2;
        Color ringC = hitState == 2 ? justColor : hitState == 1 ? normalColor : neutralColor;

        if (blockShardsEnabled && sr != null) SpawnBlockShards(c, size * scale, sr, hitPoint, just && justSpecialEnabled, layer, order);

        if (dustEnabled && smoke != null)
        {
            for (int i = 0; i < dustCount; i++)
            {
                Vector2 d = Random.insideUnitCircle;
                var ep = new ParticleSystem.EmitParams
                {
                    position = c + (Vector3)(d * size * 0.3f), velocity = (Vector3)(d * size * 0.7f + Vector2.up * 0.2f),
                    startColor = dustColor, startSize = size * 0.6f * scale * Random.Range(0.7f, 1.2f), startLifetime = Random.Range(0.6f, 0.9f), rotation = Random.Range(0f, 360f),
                };
                smoke.Emit(ep, 1);
            }
        }
        if (pebblesEnabled && pebbles != null)
        {
            for (int i = 0; i < pebbleCount; i++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f);
                var ep = new ParticleSystem.EmitParams
                {
                    position = c, velocity = new Vector3(Mathf.Cos(a), Mathf.Sin(a) * 0.8f + 0.5f, 0f) * pebbleSpeed * scale * Random.Range(0.4f, 1.1f),
                    startColor = Color.Lerp(pebbleColor, Color.white, Random.Range(0f, 0.25f)), startSize = Random.Range(0.04f, 0.09f) * Mathf.Sqrt(size),
                };
                pebbles.Emit(ep, 1);
            }
        }
        if (crackFlashEnabled)
        {
            AddSprite(glowSprite, c, size * 0.9f * scale, size * 1.3f * scale, 0.1f, Color.Lerp(ringC, Color.white, 0.7f), layer, order + 2, true);
            if (lineMaterial != null)
            {
                float off = Random.Range(0f, 360f);
                for (int i = 0; i < crackCount; i++)
                {
                    float ang = (off + i * 360f / Mathf.Max(1, crackCount) + Random.Range(-20f, 20f)) * Mathf.Deg2Rad;
                    Vector3 d = new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), 0f) * size * 0.55f * Random.Range(0.7f, 1f);
                    AddJagged(hitPoint, hitPoint + d, crackDuration, crackWidth, Color.Lerp(ringC, Color.white, 0.6f), layer, order + 2);
                }
            }
        }
        if (blockRingEnabled) AddSprite(ringSprite, c, size * 0.4f * scale, size * 2.2f * scale, blockRingDuration, ringC, layer, order + 1, true);
        if (just && justSpecialEnabled && sparkle != null) Burst(sparkle, c, justSparkCount, justColor, 4.5f * scale, 0.09f);
        if (blockShakeEnabled && (shakeAllBlocks || size >= bigBlockSize)) Shake(blockShakeDuration, blockShakeMagnitude);
    }

    private void SpawnBlockShards(Vector3 c, float size, SpriteRenderer src, Vector3 hitPoint, bool just, int layer, int order)
    {
        int n = Mathf.RoundToInt(blockShardCount * (just ? justShardMul : 1f));
        Vector3 away = c - hitPoint; away.z = 0f;
        Vector2 baseDir = away.sqrMagnitude > 0.0001f ? (Vector2)away.normalized : Vector2.up;
        for (int i = 0; i < n; i++)
        {
            if (shards.Count >= maxActiveAnims) break;
            Vector2 dir = (baseDir * 0.6f + Random.insideUnitCircle).normalized;
            var sr = RentSprite(src.sprite, false, layer, order);
            sr.color = src.color;
            sr.flipX = src.flipX;
            shards.Add(new Shard
            {
                sr = sr, pos = c + (Vector3)(Random.insideUnitCircle * size * 0.3f),
                vel = (Vector3)(dir * blockShardSpeed * (just ? justSpeedMul : 1f) * Random.Range(0.5f, 1.1f) + Vector2.up * 1.5f),
                rot = Random.Range(0f, 360f), spin = Random.Range(-540f, 540f), dur = blockShardDuration * Random.Range(0.8f, 1.2f),
                size = size * blockShardSizeMul * Random.Range(0.6f, 1.2f), gravity = blockShardGravity, c = src.color,
            });
        }
    }

    private void PlayItemPillar(Vector3 pos, Color col)
    {
        int layer = 0, order = 45;
        var sr = RentSprite(glowSprite, true, layer, order);
        sr.transform.SetPositionAndRotation(pos + Vector3.up * itemPillarHeight * 0.35f, Quaternion.Euler(0f, 0f, 90f));
        anims.Add(new Anim { sr = sr, dur = itemPillarDuration, s0 = itemPillarHeight * 0.4f, s1 = itemPillarHeight, aspect = 0.22f, c = col, rot = 90f });
        AddSprite(ringSprite, pos, 0.3f, 1.4f, 0.35f, col, layer, order, true);
        if (sparkle != null)
        {
            for (int i = 0; i < 10; i++)
            {
                var ep = new ParticleSystem.EmitParams
                {
                    position = pos + new Vector3(Random.Range(-0.25f, 0.25f), Random.Range(-0.1f, 0.4f), 0f),
                    velocity = new Vector3(Random.Range(-0.3f, 0.3f), Random.Range(1f, 2.5f), 0f),
                    startColor = Color.Lerp(col, Color.white, Random.Range(0.2f, 0.7f)), startSize = Random.Range(0.05f, 0.1f),
                };
                sparkle.Emit(ep, 1);
            }
        }
    }

    // ---------- B 機械・エネルギー系 ----------
    private void PlayMachine(Vector3 c, float size, Color col, int layer, int order)
    {
        float scale = ComboScale();
        // B1 縮んでから爆発（縮む光→少し遅れて爆発）
        float gs = Mathf.Min(size, Mathf.Max(0.1f, machineGlowMaxSize)); // 閃光の大きさの基準（大きい物でもまぶしくならないよう上限）
        Color ic = Color.Lerp(col, Color.white, machineFlashWhite); ic.a = machineFlashAlpha;
        AddSprite(glowSprite, c, gs * 1.4f, gs * 0.3f, machineImplodeSeconds, ic, layer, order + 2, false);
        float delay = Mathf.Max(0f, machineImplodeSeconds);
        delayed.Add(new Delayed { time = clock + delay, act = () => MachineExplode(c, size * scale, col, layer, order) });
    }

    private void MachineExplode(Vector3 c, float size, Color col, int layer, int order)
    {
        // 閃光・リングは大きさの上限付き（横に長いフロア等で画面を覆うほどの閃光にならないように）。明るさも抑える
        float gs = Mathf.Min(size, Mathf.Max(0.1f, machineGlowMaxSize));
        Color fc = Color.Lerp(col, Color.white, machineFlashWhite); fc.a = machineFlashAlpha;
        AddSprite(glowSprite, c, gs * machineFlashSize * 0.5f, gs * machineFlashSize, 0.14f, fc, layer, order + 2, true);
        // B7 二重リング
        AddSprite(ringSprite, c, gs * 0.4f, gs * 2.6f, 0.3f, col, layer, order + 1, true);
        AddSprite(ringSprite, c, gs * 0.2f, gs * 1.6f, 0.24f, new Color(1f, 1f, 1f, machineFlashAlpha), layer, order + 1, true);
        // B2 ショートの稲妻
        if (lineMaterial != null)
        {
            float off = Random.Range(0f, 360f);
            for (int i = 0; i < machineBoltCount; i++)
            {
                float ang = (off + i * 360f / Mathf.Max(1, machineBoltCount) + Random.Range(-25f, 25f)) * Mathf.Deg2Rad;
                Vector3 to = c + new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), 0f) * machineBoltLength * Mathf.Sqrt(size) * Random.Range(0.6f, 1.1f);
                AddJagged(c, to, 0.1f, machineBoltWidth, Color.Lerp(col, Color.white, 0.5f), layer, order + 2);
            }
        }
        // B3 金属片
        if (metalSprite != null)
        {
            for (int i = 0; i < machineMetalCount; i++)
            {
                if (shards.Count >= maxActiveAnims) break;
                float a = Random.Range(0f, Mathf.PI * 2f);
                var sr = RentSprite(metalSprite, false, layer, order);
                Color mc = Color.Lerp(new Color(0.75f, 0.78f, 0.82f, 1f), col, 0.25f);
                shards.Add(new Shard
                {
                    sr = sr, pos = c, vel = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * machineMetalSpeed * Random.Range(0.5f, 1.1f) + Vector3.up,
                    rot = Random.Range(0f, 360f), spin = Random.Range(-900f, 900f), dur = Random.Range(0.5f, 0.8f),
                    size = machineMetalSize * Mathf.Sqrt(size) * Random.Range(0.6f, 1.3f), gravity = 7f, c = mc,
                });
            }
        }
        // B4 火花の雨（上へ吹き出してから落ちる）
        if (sparkRain != null)
        {
            for (int i = 0; i < machineSparkRainCount; i++)
            {
                var ep = new ParticleSystem.EmitParams
                {
                    position = c, velocity = new Vector3(Random.Range(-1f, 1f), Random.Range(0.6f, 1.4f), 0f) * machineSparkRainSpeed,
                    startColor = Color.Lerp(col, new Color(1f, 0.85f, 0.4f), Random.Range(0.3f, 0.8f)), startSize = Random.Range(0.04f, 0.08f),
                };
                sparkRain.Emit(ep, 1);
            }
        }
        // B5 黒煙
        if (smoke != null)
        {
            for (int i = 0; i < machineSmokeCount; i++)
            {
                var ep = new ParticleSystem.EmitParams
                {
                    position = c + (Vector3)(Random.insideUnitCircle * size * 0.25f), velocity = new Vector3(Random.Range(-0.2f, 0.2f), Random.Range(0.4f, 0.9f), 0f),
                    startColor = machineSmokeColor, startSize = size * 0.6f * Random.Range(0.7f, 1.2f), startLifetime = Random.Range(0.7f, 1.0f), rotation = Random.Range(0f, 360f),
                };
                smoke.Emit(ep, 1);
            }
        }
        if (machineShakeEnabled) Shake(machineShakeDuration, machineShakeMagnitude);
    }

    // ---------- E Shamanの霊火 ----------
    private void PlayFlame(Vector3 c, float size, Color col, int layer, int order)
    {
        AddSprite(glowSprite, c, size * 0.8f, size * 1.4f, 0.18f, Color.Lerp(col, Color.white, 0.4f), layer, order + 1, true);
        if (sparkle != null)
        {
            for (int i = 0; i < flameEmberCount; i++)
            {
                var ep = new ParticleSystem.EmitParams
                {
                    position = c + (Vector3)(Random.insideUnitCircle * size * 0.3f),
                    velocity = new Vector3(Random.Range(-0.6f, 0.6f), Random.Range(0.6f, 1.4f), 0f) * flameEmberSpeed,
                    startColor = Color.Lerp(col, Color.white, Random.Range(0f, 0.5f)), startSize = Random.Range(0.04f, 0.09f), startLifetime = Random.Range(0.4f, 0.8f),
                };
                sparkle.Emit(ep, 1);
            }
        }
        if (smoke != null)
        {
            for (int i = 0; i < flameSmokeCount; i++)
            {
                var ep = new ParticleSystem.EmitParams
                {
                    position = c + (Vector3)(Random.insideUnitCircle * size * 0.2f), velocity = new Vector3(Random.Range(-0.15f, 0.15f), Random.Range(0.5f, 1f), 0f),
                    startColor = flameSmokeColor, startSize = size * 0.6f * Random.Range(0.7f, 1.2f), startLifetime = Random.Range(0.7f, 1.1f), rotation = Random.Range(0f, 360f),
                };
                smoke.Emit(ep, 1);
            }
        }
    }

    // ---------- D GravePoleのオーブ ----------
    private void OnOrbActivated(Transform orb, Color col, SpriteRenderer orbSr)
    {
        col.a = 1f;
        int layer = orbSr != null ? orbSr.sortingLayerID : 0, order = (orbSr != null ? orbSr.sortingOrder : 10) + sortingOrderOffset;
        Vector3 p = orb.position;
        // D1 光の柱
        var sr = RentSprite(glowSprite, true, layer, order);
        sr.transform.SetPositionAndRotation(p + Vector3.up * orbPillarHeight * 0.4f, Quaternion.Euler(0f, 0f, 90f));
        anims.Add(new Anim { sr = sr, dur = orbPillarDuration, s0 = orbPillarHeight * 0.3f, s1 = orbPillarHeight, aspect = 0.25f, c = col, rot = 90f });
        // D2 脈打つリング
        for (int i = 0; i < orbPulseCount; i++)
        {
            float t = clock + i * orbPulseInterval;
            Transform o = orb;
            delayed.Add(new Delayed { time = t, act = () => { if (o != null) AddSprite(ringSprite, o.position, 0.3f, orbPulseSize, 0.35f, col, layer, order, true); } });
        }
        // D3 ダンサーへ光が飛んで、着いたらダンサーが光る
        var playerGo = GameObject.FindGameObjectWithTag("Player");
        if (playerGo != null && glowSprite != null)
        {
            Transform dt = playerGo.transform;
            var dsr = playerGo.GetComponentInChildren<SpriteRenderer>();
            bool flashed = false;
            for (int i = 0; i < orbFlyCount; i++)
                AddFlyer(p, () => dt != null ? dt.position : p, orbFlySeconds, orbFlySize, 0.35f, i * 0.05f, col,
                    () => { if (!flashed && dsr != null) { flashed = true; FlashSprite(dsr, col); } });
        }
        // D4 発光中に回る光
        OnOrbDeactivatedSilent(orb);
        if (orbOrbiterCount > 0)
        {
            var ob = new Orbiters { orb = orb, srs = new SpriteRenderer[orbOrbiterCount], c = col };
            for (int i = 0; i < orbOrbiterCount; i++) ob.srs[i] = RentSprite(glowSprite, true, layer, order);
            orbiters.Add(ob);
        }
    }

    private void OnOrbDeactivated(Transform orb)
    {
        for (int i = orbiters.Count - 1; i >= 0; i--)
        {
            if (orbiters[i].orb != orb) continue;
            // D5 しぼんで消える
            Color col = orbiters[i].c;
            int layer = orbiters[i].srs.Length > 0 && orbiters[i].srs[0] != null ? orbiters[i].srs[0].sortingLayerID : 0;
            int order = orbiters[i].srs.Length > 0 && orbiters[i].srs[0] != null ? orbiters[i].srs[0].sortingOrder : 20;
            AddSprite(glowSprite, orb.position, orbOrbitRadius * 2.4f, 0.1f, orbEndDuration, col, layer, order, false);
            foreach (var s in orbiters[i].srs) ReleaseSprite(s, true);
            orbiters.RemoveAt(i);
        }
    }

    private void OnOrbDeactivatedSilent(Transform orb)
    {
        for (int i = orbiters.Count - 1; i >= 0; i--)
        {
            if (orbiters[i].orb != orb) continue;
            foreach (var s in orbiters[i].srs) ReleaseSprite(s, true);
            orbiters.RemoveAt(i);
        }
    }

    private void FlashSprite(SpriteRenderer src, Color col)
    {
        if (src == null || src.sprite == null) return;
        var sr = RentSprite(src.sprite, true, src.sortingLayerID, src.sortingOrder + 1);
        CopyTransform(sr, src);
        Color c = Color.Lerp(col, Color.white, 0.5f); c.a = 0.85f;
        anims.Add(new Anim { sr = sr, dur = dancerFlashSeconds, c = c, followSrc = src.transform, copySrc = src });
    }

    private static void CopyTransform(SpriteRenderer dst, SpriteRenderer src)
    {
        dst.sprite = src.sprite;
        dst.flipX = src.flipX; dst.flipY = src.flipY;
        dst.transform.SetPositionAndRotation(src.transform.position, src.transform.rotation);
        dst.transform.localScale = src.transform.lossyScale;
    }

    // ---------- 毎フレーム ----------
    private void LateUpdate()
    {
        SlowMoTime.ParticleSpeed(particleList, ref lastParticleSpeed);
        float dt = SlowMoTime.DeltaTime;
        clock += dt;

        for (int i = delayed.Count - 1; i >= 0; i--)
        {
            if (delayed[i].time > clock) continue;
            var act = delayed[i].act;
            delayed.RemoveAt(i);
            act?.Invoke();
        }

        for (int i = anims.Count - 1; i >= 0; i--)
        {
            var a = anims[i];
            a.t += dt;
            float k = a.t / Mathf.Max(0.01f, a.dur);
            if (k >= 1f || (a.copySrc != null && (a.followSrc == null || !a.copySrc.enabled)))
            {
                if (a.sr != null) ReleaseSprite(a.sr, true);
                if (a.lr != null) ReleaseLine(a.lr);
                anims.RemoveAt(i);
                continue;
            }
            Color col = a.c; col.a *= 1f - k;
            if (a.copySrc != null) { CopyTransform(a.sr, a.copySrc); a.sr.color = col; continue; }
            float eased = 1f - (1f - k) * (1f - k);
            if (a.sr != null)
            {
                float s = Mathf.Lerp(a.s0, a.s1, eased);
                a.sr.transform.rotation = Quaternion.Euler(0f, 0f, a.rot);
                SetSize(a.sr, s, s * a.aspect);
                a.sr.color = col;
            }
            if (a.lr != null)
            {
                a.lr.startWidth = a.lr.endWidth = a.s0 * (1f - k * 0.5f);
                a.lr.startColor = col; a.lr.endColor = new Color(col.r, col.g, col.b, col.a * 0.4f);
            }
        }

        for (int i = shards.Count - 1; i >= 0; i--)
        {
            var s = shards[i];
            s.t += dt;
            float k = s.t / Mathf.Max(0.01f, s.dur);
            if (k >= 1f) { ReleaseSprite(s.sr, s.sr != null && s.sr.sharedMaterial == additiveSpriteMaterial); shards.RemoveAt(i); continue; }
            s.vel += Vector3.down * s.gravity * dt;
            s.pos += s.vel * dt;
            s.rot += s.spin * dt;
            s.sr.transform.SetPositionAndRotation(s.pos, Quaternion.Euler(0f, 0f, s.rot));
            SetSize(s.sr, s.size, s.size);
            Color col = s.c; col.a *= k < 0.6f ? 1f : 1f - (k - 0.6f) / 0.4f;
            s.sr.color = col;
        }

        for (int i = flyers.Count - 1; i >= 0; i--)
        {
            var f = flyers[i];
            f.t += dt;
            float tt = f.t - f.delay;
            if (tt < 0f) { f.sr.enabled = false; continue; }
            f.sr.enabled = true;
            float k = Mathf.Clamp01(tt / Mathf.Max(0.05f, f.dur));
            Vector3 b = f.target != null ? f.target() : f.a;
            float e = k * k * (3f - 2f * k);
            Vector3 p = (1f - e) * (1f - e) * f.a + 2f * (1f - e) * e * f.ctrl + e * e * b;
            f.sr.transform.position = p;
            SetSize(f.sr, f.size * Mathf.Lerp(1f, 0.6f, k), f.size * Mathf.Lerp(1f, 0.6f, k));
            f.sr.color = f.c;
            if (k >= 1f) { f.onArrive?.Invoke(); ReleaseSprite(f.sr, true); flyers.RemoveAt(i); }
        }

        for (int i = orbiters.Count - 1; i >= 0; i--)
        {
            var ob = orbiters[i];
            if (ob.orb == null || !ob.orb.gameObject.activeInHierarchy) { foreach (var s in ob.srs) ReleaseSprite(s, true); orbiters.RemoveAt(i); continue; }
            ob.angle += orbOrbitSpeed * dt;
            for (int j = 0; j < ob.srs.Length; j++)
            {
                float a = (ob.angle + j * 360f / ob.srs.Length) * Mathf.Deg2Rad;
                var sr = ob.srs[j];
                sr.transform.position = ob.orb.position + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * orbOrbitRadius;
                float sz = 0.22f * (1f + 0.25f * Mathf.Sin(clock * 8f + j));
                SetSize(sr, sz, sz);
                sr.color = ob.c;
            }
        }
    }

    // ---------- 共通 ----------
    private void AddSprite(Sprite sprite, Vector3 pos, float s0, float s1, float dur, Color c, int layer, int order, bool eased)
    {
        if (sprite == null || anims.Count >= maxActiveAnims) return;
        var sr = RentSprite(sprite, true, layer, order);
        sr.transform.position = pos;
        anims.Add(new Anim { sr = sr, dur = dur, s0 = s0, s1 = s1, c = c });
    }

    private void AddJagged(Vector3 from, Vector3 to, float dur, float width, Color c, int layer, int order)
    {
        if (lineMaterial == null || anims.Count >= maxActiveAnims) return;
        Vector3 d = to - from;
        Vector3 n = new Vector3(-d.y, d.x, 0f).normalized * d.magnitude * 0.15f;
        for (int k = 0; k < s_pts.Length; k++)
        {
            float kk = k / (float)(s_pts.Length - 1);
            s_pts[k] = from + d * kk + (k == 0 ? Vector3.zero : n * Random.Range(-1f, 1f));
        }
        var lr = RentLine(layer, order);
        lr.positionCount = s_pts.Length;
        lr.SetPositions(s_pts);
        anims.Add(new Anim { lr = lr, dur = dur, s0 = width, c = c });
    }

    private void AddFlyer(Vector3 from, System.Func<Vector3> target, float dur, float size, float arc, float delay, Color c, System.Action onArrive)
    {
        Vector3 to = target();
        Vector3 d = to - from;
        Vector3 n = new Vector3(-d.y, d.x, 0f).normalized;
        float side = Random.Range(0.6f, 1.4f) * (Random.value < 0.5f ? 1f : -1f);
        Vector3 ctrl = from + d * 0.35f + n * d.magnitude * arc * side;
        var sr = RentSprite(glowSprite, true, 0, 60);
        sr.enabled = false;
        flyers.Add(new Flyer { sr = sr, a = from, ctrl = ctrl, dur = dur * Random.Range(0.9f, 1.1f), size = size, delay = delay, c = c, target = target, onArrive = onArrive });
    }

    private void Burst(ParticleSystem ps, Vector3 pos, int count, Color color, float speed, float size)
    {
        var ep = new ParticleSystem.EmitParams { position = pos, startColor = color, startSize = size };
        for (int i = 0; i < count; i++)
        {
            float a = Random.Range(0f, Mathf.PI * 2f);
            ep.velocity = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * speed * Random.Range(0.4f, 1.1f);
            ps.Emit(ep, 1);
        }
    }

    private static void SetSize(SpriteRenderer sr, float w, float h)
    {
        Vector3 b = sr.sprite != null ? sr.sprite.bounds.size : Vector3.one;
        sr.transform.localScale = new Vector3(w / Mathf.Max(0.01f, b.x), h / Mathf.Max(0.01f, b.x), 1f);
    }

    private SpriteRenderer RentSprite(Sprite sprite, bool additive, int layer, int order)
    {
        var pool = additive ? freeAdd : freeNormal;
        SpriteRenderer sr = null;
        while (pool.Count > 0 && sr == null) sr = pool.Pop();
        if (sr == null)
        {
            var go = new GameObject(additive ? "BreakFX_Glow" : "BreakFX_Piece");
            go.transform.SetParent(transform, false);
            sr = go.AddComponent<SpriteRenderer>();
            sr.sharedMaterial = additive ? additiveSpriteMaterial : defaultSpriteMaterial;
        }
        sr.sprite = sprite;
        sr.flipX = sr.flipY = false;
        sr.sortingLayerID = layer;
        sr.sortingOrder = order;
        sr.color = Color.white;
        sr.transform.rotation = Quaternion.identity;
        sr.gameObject.SetActive(true);
        sr.enabled = true;
        return sr;
    }

    private void ReleaseSprite(SpriteRenderer sr, bool additive)
    {
        if (sr == null) return;
        sr.gameObject.SetActive(false);
        (sr.sharedMaterial == additiveSpriteMaterial ? freeAdd : freeNormal).Push(sr);
    }

    private LineRenderer RentLine(int layer, int order)
    {
        LineRenderer lr = null;
        while (freeLines.Count > 0 && lr == null) lr = freeLines.Pop();
        if (lr == null)
        {
            var go = new GameObject("BreakFX_Line");
            go.transform.SetParent(transform, false);
            lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
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
}
