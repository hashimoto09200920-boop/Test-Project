using System.Collections;
using UnityEngine;
using Game.Skills;

[RequireComponent(typeof(Rigidbody2D))]
public partial class EnemyBullet : MonoBehaviour
{
    /// <summary>
    /// スローモーション対応のタイムスケール取得
    /// </summary>
    private float GetTimeScale()
    {
        return SlowMotionManager.Instance != null ? SlowMotionManager.Instance.TimeScale : 1f;
    }

    [SerializeField] private float speed = 6f;
    [SerializeField] private float lifeTime = 5f;

    [Header("Debug")]
    [SerializeField] private bool showDebugLog = false;

    [Header("Life")]
    [Tooltip("ON: lifeTime秒で消滅 / OFF: 時間経過では消滅しない")]
    [SerializeField] private bool useLifeTime = false;

    // =========================
    // ★追加：Speed Curve（時間加速）
    // =========================
    [Header("Speed Curve (Optional)")]
    [Tooltip("ON: baseSpeed を時間で変化させる（initial→max）。OFF: 固定 speed を baseSpeed とする。")]
    [SerializeField] private bool useSpeedCurve = false;

    [Tooltip("カーブ初速（baseSpeedの開始値）")]
    [SerializeField] private float curveInitialSpeed = 2f;

    [Tooltip("カーブ最大速（baseSpeedの到達値）")]
    [SerializeField] private float curveMaxSpeed = 8f;

    [Tooltip("初速→最大速までの秒数。0以下なら即到達。")]
    [SerializeField] private float curveDurationSeconds = 1.5f;

    [Tooltip("0〜1入力に対して0〜1を返す。縦が加速の進み具合。")]
    [SerializeField] private AnimationCurve speedCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    // ランタイム
    private float curveStartTime = -999f;
    private float baseSpeedNow;            // 現在のベース速度（固定 or カーブ）
    private float accelMultiplierNow = 1f; // Paddleの加速など、ベースに掛ける倍率（1が通常）
    private float accelCapBaseSpeed = 6f;  // 「maxCountでの上限」計算用の基準（固定speed or curveMaxSpeed）

    // =========================
    // ★既存：Turn Motion（前進ループ：前半=楕円/後半=円）
    // =========================
    [Header("Spiral Motion Runtime (Injected)")]
    [SerializeField] private bool spiralMotionEnabled = false;
    [SerializeField] private float spiralRadius = 0.5f;
    [SerializeField] private float spiralPeriod = 0.5f;
    [SerializeField] private bool spiralRotateSprite = true;
    private float spiralTime = 0f;
    private int spiralSign = 1; // +1=右回り / -1=左回り (ランダム決定)
    private Vector2 spiralForwardDir = Vector2.down;

    // =========================
    // ★Wave Motion（左右に揺れながら前進）
    // =========================
    [Header("Wave Motion Runtime (Injected)")]
    [SerializeField] private bool waveMotionEnabled = false;
    [SerializeField] private float waveAmplitude = 1f;
    [SerializeField] private float waveFrequency = 2f;
    private float waveTime = 0f;
    private Vector2 waveForwardDir = Vector2.down;

    private int debugFrameCount = 0; // フレームカウント（最初の20フレームだけログ）
    private string debugTag = ""; // 弾種別識別用（Parent/A/B/C）

    // =========================
    // ★MissileArc（ミサイル挙動）
    // =========================
    [Header("MissileArc Runtime (Injected)")]
    [SerializeField] private bool missileArcEnabled = false;
    [SerializeField] private float missileInitialSpeed = 0f;
    [SerializeField] private float missileStraightDuration = 0.3f;
    [SerializeField] private float missileCurveAngle = 90f;
    [SerializeField] private bool missileCurveRandomDirection = false;
    [SerializeField] private float missileCurveDuration = 0.5f;
    [SerializeField] private float missileFinalSpeed = 0f;
    [SerializeField] private bool missileUseSpeedCurve = false;
    [SerializeField] private float missileCurveInitialSpeed = 0f;
    [SerializeField] private float missileCurveFinalSpeed = 0f;
    [SerializeField] private AnimationCurve missileSpeedCurve = null;
    [SerializeField] private bool missileUseRandomOffset = false;
    [SerializeField] private float missileRandomOffsetRadius = 1f;

    private Coroutine missileArcCoroutine = null;

    // =========================
    // Paddle bounce limit
    // =========================
    // ★BulletTypeで設定（Prefabには表示されない）
    private bool usePaddleBounceLimit = false;  // デフォルトは無効
    private int paddleBounceLimit = 0;  // デフォルトは無制限

    private int remainingPaddleBounces;
    private float lastPaddleBounceTime = -999f;

    public int RemainingPaddleBounces => remainingPaddleBounces;

    private bool hasPaddleReflectedOnce = false;
    public bool HasPaddleReflectedOnce => hasPaddleReflectedOnce;

    // ホバー中は線に触れたら反射せず消滅する
    private bool destroyOnLineHit = false;
    public bool DestroyOnLineHit => destroyOnLineHit;
    public void SetDestroyOnLineHit(bool value) { destroyOnLineHit = value; }

    private int lastEnemyHitCountFrame = -999;
    private int lastWallBounceFrame = -999;

    // A8スキル: 敵ヒットごとダメージ加算
    private int a8EnemyHitCount  = 0;     // この弾が敵に当たった累計回数
    private int a8MaxAdditions   = 0;     // スキルレベルに応じた最大加算回数
    private float a8DamagePerHit = 1f;    // 1ヒットあたりの加算量（SkillDefinition.effectValueから）
    private int lastA8HitFrame   = -999;

    private int lastBulletContactFrame = -999;
    private int lastBulletContactOtherId = 0;

    [Header("Wall Bounce Count")]
    [Tooltip("このLayerに属するCollider2Dに当たったら、跳ね返り回数を1消費する。未設定(0)なら壁カウントしない。")]
    [SerializeField] private LayerMask wallLayersToCount = 0;

    // =========================
    // ★未反射弾の消滅設定
    // =========================
    [Header("Unreflected Bullet Disappear")]
    [Tooltip("ON: 未反射弾（白/赤線で反射していない）がプレイヤーorフロアに触れたら消滅する")]
    [SerializeField] private bool unreflectedDisappearOnPlayerFloorHit = true;

    // =========================
    // ★VFX/SE 分離：Feedback
    // =========================
    [Header("Feedback (VFX/SE)")]
    [Tooltip("VFX/SE担当コンポーネント。未設定なら GetComponent で取得する。")]
    [SerializeField] private EnemyBulletFeedback feedback;

    // =========================
    // Anti wall-parallel
    // =========================
    [Header("Wall Angle Clamp (Anti Wall-Parallel)")]
    [Tooltip("壁に当たった直後だけ、速度ベクトルがほぼ水平/垂直になるのを防ぐ（角度下限）")]
    [Range(0f, 45f)]
    [SerializeField] private float wallMinAngleDeg = 20f;

    [Tooltip("ON: 壁ヒット時だけ角度補正を適用する（白線/赤線/敵には適用しない）")]
    [SerializeField] private bool useWallAngleClamp = true;

    private int lastWallAngleClampFrame = -999;

    private bool isBeingDestroyed;

    private Rigidbody2D rb;
    private Vector2 direction = Vector2.down;
    private float timer;

    // C2 JustPenetration: FixedUpdate で物理ステップ前の速度を保存
    private Vector2 preCollisionVelocity;

    // C2 JustPenetration: この弾の残り貫通回数（0=不可、1=1回、-1=無制限）
    private int c2PenetrationsRemaining = 0;

    public bool IsReflected { get; private set; }

    private Collider2D bulletCol;
    private Collider2D ownerCol;  // 後方互換性のため残す（最初の1つ）
    private System.Collections.Generic.List<Collider2D> ownerColliders = new System.Collections.Generic.List<Collider2D>();

    [Header("Renderers (Visual / JustOverlay)")]
    [SerializeField] private SpriteRenderer visualRenderer;
    [SerializeField] private SpriteRenderer overlayRenderer;

    private SpriteRenderer sr;

    [Header("Accel")]
    [SerializeField] private float accelLerpSeconds = 0.06f;
    [SerializeField] private float accelCooldown = 0.10f;
    [SerializeField] private float speedLerp = 24f;

    private int accelCount;
    public int AccelMaxCountLast { get; private set; }
    private float lastAccelTime = -999f;

    public float TargetSpeed { get; private set; }

    [Header("Owner Collision Ignore")]
    [SerializeField] private bool ignoreOwnerCollision = true;
    [SerializeField] private float ignoreOwnerSeconds = 0.1f;

    private float ignoreOwnerUntil = -1f;

    // 未反射弾の物理判定無効化
    private float unreflectedCollisionDisableUntil = -1f;
    private bool unreflectedCollisionDisabled = false;

    public float DamageMultiplier { get; private set; } = 1f;

    // =========================================================
    // ★ブロック専用ダメージ（敵ダメージとは独立）
    // =========================================================
    public float BlockNormalDamage { get; private set; } = 1f;  // 通常反射弾のブロックダメージ
    public float BlockJustDamage { get; private set; } = 2f;    // Just反射弾のブロックダメージ

    [Header("Visual Rotation")]
    [Tooltip("スプライトを毎フレーム回転させる速度（度/秒）。0で無効。負の値で逆回転。")]
    [SerializeField] private float spriteRotateSpeed = 180f;

    [Header("Flash (Just)")]
    [SerializeField] private bool flashOnJust = true;
    [SerializeField] private float flashSeconds = 0.08f;
    [SerializeField] private Color flashColor = new Color(1f, 0.6f, 0.1f, 1f);

    [Header("D: Powered Visual (Overlay)")]
    [SerializeField] private Color poweredColor = new Color(1f, 0.35f, 0.1f, 1f);

    private Coroutine flashCo;

    [Header("Anti Stop (Low Speed Safety)")]
    [Tooltip("速度がこの値未満まで落ちたら、TargetSpeed で復帰させる（speed=1で0化する問題の保険）。")]
    [SerializeField] private float reviveMinSpeed = 0.20f;

    [Tooltip("復帰処理の最短間隔（秒）。0なら無制限。")]
    [SerializeField] private float reviveCooldownSeconds = 0.02f;

    private float lastReviveTime = -999f;
    private Vector2 lastNonZeroDir = Vector2.down;

    // =========================================================
    // ★弾×弾（ペア）を秒クールダウンで安定化
    // =========================================================
    [Header("Bullet vs Bullet Stabilizer")]
    [Tooltip("同じ弾ペア(A,B)の弾×弾判定を、この秒数以内は再処理しない。Enter/Stay/Trigger混在の揺れを潰す。")]
    [SerializeField] private float bulletPairCooldownSeconds = 0.08f;

    private static readonly System.Collections.Generic.Dictionary<ulong, float> s_pairNextAllowedTime
        = new System.Collections.Generic.Dictionary<ulong, float>(512);

    // ★s_pairNextAllowedTimeはstatic(アプリ全体で永続)かつ、弾同士が衝突するたびにエントリが
    //   増える一方で削除処理が無かったため、際限なく増え続けるリークになっていた
    //   （特に多数の弾を同時に反射・衝突させるバーストで急速に増加する）。
    //   期限切れ（クールダウンが過ぎ二度と参照されない）エントリを一定間隔で自動的に間引く。
    private static float s_pairCooldownLastPruneTime = 0f;
    private const float PairCooldownPruneInterval = 5f;
    private static readonly System.Collections.Generic.List<ulong> s_pairCooldownPruneScratch
        = new System.Collections.Generic.List<ulong>(64);

    private static void PruneExpiredPairCooldownsIfNeeded(float now)
    {
        if (now - s_pairCooldownLastPruneTime < PairCooldownPruneInterval) return;
        s_pairCooldownLastPruneTime = now;

        s_pairCooldownPruneScratch.Clear();
        foreach (var kvp in s_pairNextAllowedTime)
        {
            if (kvp.Value < now) s_pairCooldownPruneScratch.Add(kvp.Key);
        }
        for (int i = 0; i < s_pairCooldownPruneScratch.Count; i++)
        {
            s_pairNextAllowedTime.Remove(s_pairCooldownPruneScratch[i]);
        }
    }

    // =========================================================
    // ★追加：反射直後だけ「物理反射（Rigidbodyの速度）」を優先する
    // =========================================================
    [Header("Reflect Override (Physics First)")]
    [Tooltip("Paddle反射直後、この秒数だけ ApplyVelocity による速度上書きを止める（PhysicsMaterialの反射を潰さない）。")]
    [SerializeField] private float reflectOverrideSeconds = 0.08f;

    private float reflectOverrideUntil = -999f;

    // =========================================================
    // ★修正：EnemyShooter が呼ぶ SetSpriteOverride を必ず提供（CS1061対策）
    // =========================================================
    private Sprite originalVisualSprite;
    private bool originalVisualSpriteCached = false;

    // =========================================================
    // ★VFX Parent（旧 EnemyBullet 側で持っていた参照は維持：他の仕組みが使う可能性があるため）
    // =========================================================
    [Header("VFX Parent (Shared)")]
    [Tooltip("VFXを ProjectileRoot 配下にしたい場合に指定（任意）")]
    [SerializeField] private Transform vfxParent;

    public float CurrentSpeed
    {
        get
        {
            if (rb == null) return 0f;
            return rb.linearVelocity.magnitude;
        }
    }

    public int AccelCount { get { return accelCount; } }

    private bool IsPoweredNow => (DamageMultiplier > 1.0001f);

    private int damageValue = 1;
    public int DamageValue => damageValue;

    public void SetDamage(int d)
    {
        damageValue = Mathf.Max(0, d);
    }

    // =========================================================
    // ★Warp（消滅→ワープ→出現）
    // =========================================================
    [Header("Warp (Injected from BulletType)")]
    [SerializeField] private bool warpEnabled = false;
    [SerializeField] private float warpDisappearAfterSeconds = 1.0f;
    [SerializeField] private float warpReappearAfterSeconds = 0.5f;
    [SerializeField] private float warpOffsetXRange = 3.0f;
    [SerializeField] private float warpOffsetYMin = 0f;
    [SerializeField] private float warpOffsetYMax = 0f;

    [SerializeField] private GameObject warpDisappearVfxPrefab;
    [SerializeField] private GameObject warpReappearVfxPrefab;
    [SerializeField] private AudioClip warpDisappearSe;
    [SerializeField] private AudioClip warpReappearSe;
    [SerializeField] private float warpDisappearSeStartOffsetSeconds = 0f;
    [SerializeField] private float warpReappearSeStartOffsetSeconds = 0f;

    private bool warpDone = false;
    private Coroutine warpCo;

    // =========================================================
    // ★MultiWarhead（多弾頭弾）
    // =========================================================
    [Header("MultiWarhead (Injected from BulletType)")]
    [SerializeField] private bool multiWarheadEnabled = false;
    [SerializeField] private float multiSlowSeconds = 1.5f;
    [SerializeField] private float multiSlowSpeed = 2f;
    [SerializeField] private Sprite multiParentSprite;
    [SerializeField] private bool multiParentUseSpeedCurve = false;
    [SerializeField] private float multiParentInitialSpeed = 2f;
    [SerializeField] private float multiParentMaxSpeed = 2f;
    [SerializeField] private float multiParentCurveDuration = 1f;
    [SerializeField] private AnimationCurve multiParentSpeedCurve;
    [SerializeField] private AudioClip multiParentVanishSe;
    [SerializeField] private GameObject multiParentVanishVfx;
    [SerializeField] private float multiChildOffsetX = 0.3f;
    [SerializeField] private float multiChildFinalSpeed = 8f;
    [SerializeField] private bool multiChildUseRandomOffset = false;
    [SerializeField] private float multiChildRandomOffsetRadius = 1f;

    [SerializeField] private float multiChildA_Delay = 0f;
    [SerializeField] private float multiChildA_DelayMax = 0f;
    [SerializeField] private AudioClip multiChildA_SpawnSe;
    [SerializeField] private GameObject multiChildA_SpawnVfx;
    [SerializeField] private Sprite multiChildA_Sprite;
    [SerializeField] private float multiChildA_LifeTime = 5f;
    [SerializeField] private Color multiChildA_TrailColor = new Color(1f, 0.65f, 0.1f, 0.7490196f);
    [SerializeField] private float multiChildA_TrailTime = 0.3f;
    [SerializeField] private float multiChildA_TrailWidthStart = 0.1f;
    [SerializeField] private float multiChildA_TrailWidthEnd = 0f;

    [SerializeField] private float multiChildB_Delay = 0.1f;
    [SerializeField] private float multiChildB_DelayMax = 0.1f;
    [SerializeField] private AudioClip multiChildB_SpawnSe;
    [SerializeField] private GameObject multiChildB_SpawnVfx;
    [SerializeField] private Sprite multiChildB_Sprite;
    [SerializeField] private float multiChildB_LifeTime = 5f;
    [SerializeField] private Color multiChildB_TrailColor = new Color(1f, 0.65f, 0.1f, 0.7490196f);
    [SerializeField] private float multiChildB_TrailTime = 0.3f;
    [SerializeField] private float multiChildB_TrailWidthStart = 0.1f;
    [SerializeField] private float multiChildB_TrailWidthEnd = 0f;

    private bool multiWarheadDone = false;
    private Coroutine multiWarheadCo;

    // ★プーリング再利用時の復元用：プレハブ本来のデフォルト値（Awake()で一度だけキャッシュ）
    private int originalDamageValue;
    private Color originalVisualColor = Color.white;
    private int originalVisualSortingOrder;
    private CircleCollider2D circleCollider2D;
    private float originalCircleRadius;

    // ★この弾がEnemyBulletPool経由で生成された場合、そのプレハブ参照を保持する。
    //   段階的移行中は「プール経由の発射元」と「まだ従来通りInstantiateしている発射元」が
    //   混在するため、これがnullの間は従来通りDestroy()する（EnemyBulletPool.Get()内で設定される）
    private EnemyBullet sourcePrefab;
    public void SetSourcePrefab(EnemyBullet prefab) => sourcePrefab = prefab;

    /// <summary>
    /// 弾を消滅させる（内部・外部を問わず全ての「この弾を消す」箇所から呼ぶ共通の出口）。
    /// プール経由で生成された弾はプールへ返却し、そうでなければ従来通りDestroy()する。
    /// 外部スクリプトは、この弾に対して直接Destroy(bullet.gameObject)する代わりに
    /// bullet.ReleaseOrDestroySelf()を呼ぶこと。
    /// </summary>
    public void ReleaseOrDestroySelf()
    {
        if (sourcePrefab != null)
        {
            EnemyBulletPool.Release(sourcePrefab, this);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Awake()
    {
        // ★ここでキャッシュする値は「このインスタンス本来のデフォルト値」。Awake()は
        //   真の初回生成時にしか呼ばれないため、プーリングで何度再利用されてもこの値は
        //   不変で、OnEnable()側で毎回ここへ復元する基準になる。
        if (visualRenderer == null)
        {
            Transform v = transform.Find("Visual");
            if (v != null) visualRenderer = v.GetComponent<SpriteRenderer>();
        }
        if (visualRenderer != null)
        {
            originalVisualColor = visualRenderer.color;
            originalVisualSortingOrder = visualRenderer.sortingOrder;
        }

        originalDamageValue = damageValue;

        circleCollider2D = GetComponent<CircleCollider2D>();
        if (circleCollider2D != null) originalCircleRadius = circleCollider2D.radius;

        // ★負荷軽減：BulletPenetration/PinnedReflectBulletの有無はプレハブ構成で決まり
        //   プーリングで再利用してもコンポーネント構成自体は変わらないため、Awake()で一度だけ
        //   キャッシュする。PaddleDot等が衝突のたびにGetComponent()し直していたのを解消する目的。
        //   （円で大量の弾を同時に反射させた時、弾の数だけGetComponentが重複発生していた）
        cachedPenetration = GetComponent<BulletPenetration>();
        cachedPinnedReflect = GetComponent<PinnedReflectBullet>();
    }

    private BulletPenetration cachedPenetration;
    private PinnedReflectBullet cachedPinnedReflect;

    // ★負荷軽減：LayerMask.NameToLayer()は文字列検索のため、弾の生成/再利用・反射のたびに
    //   毎回呼ぶと積み重なる（円で大量の弾を同時に反射させる時に顕著）。レイヤー番号は
    //   実行中変わらないため、アプリ全体で1回だけ解決してstaticにキャッシュする。
    private static int s_unreflectedLayer = -1;
    private static int s_reflectedLayer = -1;
    private static bool s_layersCached = false;

    private static void EnsureLayerCache()
    {
        if (s_layersCached) return;
        s_layersCached = true;
        s_unreflectedLayer = LayerMask.NameToLayer("UnreflectedBullet");
        s_reflectedLayer = LayerMask.NameToLayer("ReflectedBullet");
    }

    /// <summary>このインスタンスのBulletPenetration（無ければnull）。Awake時に一度だけ取得したキャッシュ</summary>
    public BulletPenetration CachedPenetration => cachedPenetration;
    /// <summary>このインスタンスのPinnedReflectBullet（無ければnull）。Awake時に一度だけ取得したキャッシュ</summary>
    public PinnedReflectBullet CachedPinnedReflect => cachedPinnedReflect;

    /// <summary>
    /// BulletPenetration/PinnedReflectBulletはEnemyShooter/TutorialFlowController等から
    /// 発射後に動的にAddComponentされることがあるため、Awake()のキャッシュだけでは追いつかない。
    /// 追加した直後に呼んでキャッシュを更新すること。
    /// </summary>
    public void RefreshCachedPenetration() => cachedPenetration = GetComponent<BulletPenetration>();
    public void RefreshCachedPinnedReflect() => cachedPinnedReflect = GetComponent<PinnedReflectBullet>();

    private void OnEnable()
    {
        rb = GetComponent<Rigidbody2D>();
        bulletCol = GetComponent<Collider2D>();

        if (bulletCol != null) bulletCol.enabled = true;
        if (rb != null) rb.simulated = true;

        destroyOnLineHit = false;

        // ★動的に後付けされる専用コンポーネントは、前回の生涯のものが残っていると
        //   二重動作の原因になるため、プーリング再利用のたびに必ず取り除く。
        //   ★重要：ここはDestroy()ではなくDestroyImmediate()を使う。Destroy()はフレーム末まで
        //   実際には反映されないため、この直後に発射側（EnemyShooter等）が
        //   AddComponent<PinnedReflectBullet>()で新しいものを追加すると、同一GameObjectに
        //   「まもなく消える古い方」と「新しい本物」が一瞬だけ共存してしまう。この状態で
        //   GetComponent()（RefreshCachedPinnedReflect含む）を呼ぶと、追加順で先にある
        //   古い方を拾ってキャッシュしてしまい、フレーム末に古い方が実際に破棄された瞬間
        //   キャッシュだけnullになり、新しい本物のコンポーネントが誰にも参照されず
        //   ドリル反射が機能しなくなる不具合があった。DestroyImmediate()で即座に消し切ることで、
        //   この「一瞬だけ新旧共存する」窓自体を無くす。
        PinnedReflectBullet pinnedToRemove = GetComponent<PinnedReflectBullet>();
        if (pinnedToRemove != null) DestroyImmediate(pinnedToRemove);
        DrillSpinBullet drillToRemove = GetComponent<DrillSpinBullet>();
        if (drillToRemove != null) DestroyImmediate(drillToRemove);
        PendingSummonBullet pendingToRemove = GetComponent<PendingSummonBullet>();
        if (pendingToRemove != null) DestroyImmediate(pendingToRemove);

        // ★オーナー（発射元）との衝突無視設定を解除してからリストを空にする
        if (bulletCol != null)
        {
            foreach (Collider2D ownerColToRestore in ownerColliders)
            {
                if (ownerColToRestore != null) Physics2D.IgnoreCollision(bulletCol, ownerColToRestore, false);
            }
        }
        ownerColliders.Clear();
        ownerCol = null;
        ignoreOwnerUntil = -1f;

        // ★外部からの購読は前回の生涯のものが残っている可能性があるため必ずクリアする
        OnReflected = null;
        OnPenetratedLine = null;
        OnJustReflect = null;

        smokeGrenadeEnabled = false;
        smokeGrenadeHasReflectedOnce = false;

        c2PenetrationsRemaining = 0;

        multiWarheadDone = false;
        multiWarheadHidden = false;

        // ★重要：EnemyShooter.SpawnBulletOne()等の呼び出し順は
        //   Get() → SetDirection(dir) → ApplyBulletTypeToEnemyBullet()（ここでClearSpiralMotion/
        //   ClearWaveMotionが呼ばれる）という順番のため、SetDirection内のApplyVelocity()が
        //   実行される時点ではまだ弾種側のクリア処理が済んでいない。前回の生涯でspiral/wave移動
        //   が有効なまま残っていると、SetDirection直後のApplyVelocity()が本来の直進方向ではなく
        //   古いspiral/wave計算を使ってしまい、明後日の方向へ発射される不具合になる。
        //   ここで早めにfalseへ戻しておくことで、この後SetDirectionが呼ばれても正しい直進速度が
        //   計算されるようにする
        spiralMotionEnabled = false;
        waveMotionEnabled = false;
        spiralTime = 0f;
        waveTime = 0f;

        // ★同じ理由：useSpeedCurveも前回の生涯の値が残っていると、この直後に呼ばれる
        //   RefreshBaseSpeedAndTargetSpeed()が古いカーブ設定を使って誤った速度を計算してしまう
        useSpeedCurve = false;

        // ★同じ理由でさらに重大：useCountdownExplosion/warpEnabled/multiWarheadEnabled/
        //   missileArcEnabledは、ApplyBulletTypeToEnemyBullet()経由でしかfalseに戻されない。
        //   弾種(bt)がnullでApplyBulletTypeToEnemyBulletそのものが呼ばれない経路（一部の
        //   コントローラーのフォールバック、Tutorial等）を通ると、前回の生涯でカウントダウン爆弾・
        //   ワープ弾・マルチ弾頭だった弾が、普通の弾として再利用された時にそのまま古い挙動を
        //   引き継いでしまい、発射直後に勝手に爆発・ワープ消滅・分裂消滅する
        //   （フロア/ダンサーに当たる前に消え、被ダメージが発生しない不具合の原因）
        useCountdownExplosion = false;
        warpEnabled = false;
        multiWarheadEnabled = false;
        missileArcEnabled = false;
        if (missileArcCoroutine != null) { StopCoroutine(missileArcCoroutine); missileArcCoroutine = null; }

        transform.localScale = Vector3.one;
        transform.rotation = Quaternion.identity;

        LastReflectedByStroke = null;

        // ★弾は最初 UnreflectedBullet Layer（敵と衝突しない）
        EnsureLayerCache();
        int unreflectedLayer = s_unreflectedLayer;
        if (unreflectedLayer == -1)
        {
            Debug.LogError("[EnemyBullet] Layer 'UnreflectedBullet' NOT FOUND! Create it in: Edit > Project Settings > Tags and Layers");
        }
        else
        {
            gameObject.layer = unreflectedLayer;
            if (showDebugLog)
            {
                Debug.Log($"[EnemyBullet] Awake - Set layer to UnreflectedBullet (index: {unreflectedLayer})");
            }
        }

        if (visualRenderer == null)
        {
            Transform v = transform.Find("Visual");
            if (v != null) visualRenderer = v.GetComponent<SpriteRenderer>();
        }
        if (overlayRenderer == null)
        {
            Transform o = transform.Find("JustOverlay");
            if (o != null) overlayRenderer = o.GetComponentInChildren<SpriteRenderer>();
        }

        sr = overlayRenderer != null ? overlayRenderer : visualRenderer;
        if (sr == null) sr = GetComponent<SpriteRenderer>();

        if (overlayRenderer != null)
        {
            overlayRenderer.enabled = false;
        }

        if (visualRenderer != null)
        {
            visualRenderer.enabled = true;
            visualRenderer.color = originalVisualColor;
            visualRenderer.sortingOrder = originalVisualSortingOrder;
        }

        // ★弾種側で上書き指定が無い場合に備え、先にプレハブ本来のスプライト/ダメージ値/
        //   当たり判定半径へ戻しておく（この後の弾種適用が上書きすれば置き換わる）
        SetSpriteOverride(null);
        damageValue = originalDamageValue;
        if (circleCollider2D != null) circleCollider2D.radius = originalCircleRadius;

        unreflectedCollisionDisabled = false;
        unreflectedCollisionDisableUntil = -1f;

        debugFrameCount = 0;

        timer = 0f;

        accelMultiplierNow = 1f;
        baseSpeedNow = speed;
        accelCapBaseSpeed = speed;

        curveStartTime = Time.time;

        TargetSpeed = speed;

        IsReflected = false;

        accelCount = 0;
        AccelMaxCountLast = 0;
        lastAccelTime = -999f;

        DamageMultiplier = 1f;

        isBeingDestroyed = false;
        hasPaddleReflectedOnce = false;

        // ★弾種側で上書き指定が無い場合、固定のデフォルト（無制限）へ戻す。
        //   ResetPaddleBounceRemaining()はこの2つのフィールドを参照するため、必ず先に行う
        usePaddleBounceLimit = false;
        paddleBounceLimit = 0;

        ResetPaddleBounceRemaining();

        if (multiWarheadCo != null) { StopCoroutine(multiWarheadCo); multiWarheadCo = null; }
        if (flashCo != null) { StopCoroutine(flashCo); flashCo = null; }

        lastWallAngleClampFrame = -999;

        lastNonZeroDir = (direction.sqrMagnitude > 0.0001f) ? direction.normalized : Vector2.down;

        spiralForwardDir = lastNonZeroDir;
        waveForwardDir = lastNonZeroDir;

        reflectOverrideUntil = -999f;

        RefreshBaseSpeedAndTargetSpeed();

        // Feedback
        if (feedback == null) feedback = GetComponent<EnemyBulletFeedback>();

        ApplyVelocity();
        ApplyVisualByState();

        // 白フラッシュ防止：Animatorを強制的に初期フレームへ進める
        // モバイルで多数の弾が同時InstantiateされるとAnimatorのUpdate遅延でsprite=nullのまま
        // 白いデフォルトquadが1フレーム表示される問題を防ぐ
        // enabled=falseのAnimatorはUpdate()でもスプライトを上書きするため、有効時のみ実行する
        Animator bulletAnim = GetComponentInChildren<Animator>();
        if (bulletAnim != null && bulletAnim.enabled)
        {
            bulletAnim.Update(0f);
        }

        // Explosion runtime init flags (in Explosion partial)
        explosionInitDone = false;
        explosionRingCreated = false;
        explosionBlinkVisible = true;
        explosionBlinkToggleRemaining = -1f;

        // Warp runtime init
        warpDone = false;
        warpCo = null;

        // ブロックダメージをSkillManagerから取得
        if (SkillManager.Instance != null)
        {
            SkillManager.Instance.GetBlockDamage(out float normalDmg, out float justDmg);
            BlockNormalDamage = normalDmg;
            BlockJustDamage = justDmg;

            // A8: ダメージ加算の最大回数と1ヒットあたり加算量を取得
            a8MaxAdditions = SkillManager.Instance.GetA8MaxAdditions();
            a8DamagePerHit = SkillManager.Instance.GetA8DamagePerHit();
        }

        // A8状態リセット
        a8EnemyHitCount = 0;
        lastA8HitFrame  = -999;
        damageBeforeA8  = -1;
    }

    public void SetOwnerCollisionIgnore(Collider2D owner, float seconds)
    {
        if (!ignoreOwnerCollision) return;
        if (bulletCol == null || owner == null) return;

        if (showDebugLog)
        {
            Debug.Log($"[EnemyBullet] SetOwnerCollisionIgnore called: {owner.gameObject.name}");
        }

        // ★複数パーツ敵対応：複数の Collider を無視できるようにする
        if (ownerCol == null)
        {
            ownerCol = owner;  // 後方互換性のため、最初の1つは ownerCol に保存
        }

        if (!ownerColliders.Contains(owner))
        {
            ownerColliders.Add(owner);
            if (showDebugLog)
            {
                Debug.Log($"[EnemyBullet] Added to ownerColliders. Total: {ownerColliders.Count}");
            }
        }

        ignoreOwnerUntil = Time.time + Mathf.Max(0.01f, seconds);
        Physics2D.IgnoreCollision(bulletCol, owner, true);
        if (showDebugLog)
        {
            Debug.Log($"[EnemyBullet] IgnoreCollision set TRUE until {ignoreOwnerUntil}");
        }
    }

    public void SetUnreflectedCollisionDisable(float seconds)
    {
        if (seconds <= 0f) return;

        unreflectedCollisionDisableUntil = Time.time + seconds;
        unreflectedCollisionDisabled = true;
        // Collider2Dは無効化しない（白線・赤線との判定は有効のまま）
    }

    /// <summary>当たり判定を即時無効化し、フェードアウトして消滅する。</summary>
    public void StartFadeAndDestroy(float duration)
    {
        if (isBeingDestroyed) return;
        isBeingDestroyed = true;
        if (rb != null) rb.simulated = false;
        if (bulletCol != null) bulletCol.enabled = false;
        if (feedback != null) feedback.StopEmitters();
        StartCoroutine(FadeAndDestroyCoroutine(duration));
    }

    private IEnumerator FadeAndDestroyCoroutine(float duration)
    {
        float elapsed = 0f;
        float vAlpha = visualRenderer != null ? visualRenderer.color.a : 1f;
        float oAlpha = (overlayRenderer != null && overlayRenderer.enabled) ? overlayRenderer.color.a : 1f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            if (visualRenderer != null)
            {
                Color c = visualRenderer.color;
                c.a = vAlpha * (1f - t);
                visualRenderer.color = c;
            }
            if (overlayRenderer != null && overlayRenderer.enabled)
            {
                Color c = overlayRenderer.color;
                c.a = oAlpha * (1f - t);
                overlayRenderer.color = c;
            }
            yield return null;
        }
        ReleaseOrDestroySelf();
    }

    /// <summary>この弾が反射された瞬間に発火（敵にヒットしたかどうかは問わない）</summary>
    public event System.Action OnReflected;

    /// <summary>直近でこの弾を反射したStroke（線）。PaddleDotがMarkReflected()の直前にセットする</summary>
    public Stroke LastReflectedByStroke { get; private set; }
    public void SetReflectedByStroke(Stroke stroke) => LastReflectedByStroke = stroke;

    /// <summary>この弾の貫通力が線の硬度を上回り、反射されずに線を破って通過した瞬間に発火</summary>
    public event System.Action OnPenetratedLine;
    public void NotifyPenetratedLine() => OnPenetratedLine?.Invoke();

    public void MarkReflected()
    {
        IsReflected = true;
        OnReflected?.Invoke();

        // ★未反射弾→反射弾：Layerを変更して敵との物理衝突を有効化
        EnsureLayerCache();
        int unreflectedLayer = s_unreflectedLayer;
        int reflectedLayer = s_reflectedLayer;

        if (showDebugLog)
        {
            Debug.Log($"[EnemyBullet] MarkReflected called. Current layer: {gameObject.layer}, UnreflectedLayer: {unreflectedLayer}, ReflectedLayer: {reflectedLayer}");
        }

        if (reflectedLayer == -1)
        {
            Debug.LogError("[EnemyBullet] Layer 'ReflectedBullet' NOT FOUND! Create it in: Edit > Project Settings > Tags and Layers");
            return;
        }

        if (gameObject.layer == unreflectedLayer)
        {
            gameObject.layer = reflectedLayer;
            if (showDebugLog)
            {
                Debug.Log($"[EnemyBullet] Layer changed: UnreflectedBullet ({unreflectedLayer}) → ReflectedBullet ({reflectedLayer})");
            }
        }
        else
        {
            // ★既に反射済みの弾にパドルの線が再度触れるのは正常な状況（バグではない）ため、
            //   警告は出さない。以前は無条件でDebug.LogWarning（Editor上ではスタックトレース取得のコストが
            //   重い）を出していたため、多数の弾を一度に反射した際にこの分岐が短時間に連発し、
            //   数秒単位のフリーズを引き起こしていた
            if (showDebugLog)
            {
                Debug.Log($"[EnemyBullet] Current layer ({gameObject.layer}) is not UnreflectedBullet ({unreflectedLayer}), skipping layer change");
            }
        }
    }

    public void SetVisualColor(Color color)
    {
        if (visualRenderer != null) visualRenderer.color = color;
    }

    public void SetVisualSortingOrder(int order)
    {
        if (visualRenderer != null) visualRenderer.sortingOrder = order;
    }

    public void SetUnreflectedTrail(Color color, float time, float widthStart, float widthEnd)
    {
        if (feedback != null) feedback.SetUnreflectedTrail(color, time, widthStart, widthEnd);
    }

    public void SetSpriteOverride(Sprite sprite)
    {
        if (visualRenderer == null)
        {
            Transform v = transform.Find("Visual");
            if (v != null) visualRenderer = v.GetComponent<SpriteRenderer>();
        }
        if (visualRenderer == null) visualRenderer = GetComponentInChildren<SpriteRenderer>();
        if (visualRenderer == null) return;

        if (!originalVisualSpriteCached)
        {
            originalVisualSprite = visualRenderer.sprite;
            originalVisualSpriteCached = true;
        }

        if (sprite == null)
        {
            visualRenderer.sprite = originalVisualSprite;
            return;
        }

        visualRenderer.sprite = sprite;
    }

    public void SetDebugTag(string tag)
    {
        debugTag = tag;
    }

    private void FixedUpdate()
    {
        if (rb != null) preCollisionVelocity = rb.linearVelocity;
    }

    private void Update()
    {
        if (ignoreOwnerCollision && bulletCol != null)
        {
            if (Time.time > ignoreOwnerUntil)
            {
                // ★複数パーツ敵対応：全ての owner Collider の無視を解除
                foreach (Collider2D col in ownerColliders)
                {
                    if (col != null)
                    {
                        Physics2D.IgnoreCollision(bulletCol, col, false);
                    }
                }
                ownerColliders.Clear();
                ownerCol = null;
                ignoreOwnerUntil = -1f;
            }
        }

        // 未反射弾の物理判定無効化の時間管理
        if (unreflectedCollisionDisabled)
        {
            if (Time.time > unreflectedCollisionDisableUntil)
            {
                unreflectedCollisionDisabled = false;
                unreflectedCollisionDisableUntil = -1f;
            }
        }

        if (useLifeTime)
        {
            float timeScale = GetTimeScale();
            timer += Time.deltaTime * timeScale;
            if (timer >= lifeTime)
            {
                isBeingDestroyed = true;
                ReleaseOrDestroySelf();
                return;
            }
        }

        if (!isBeingDestroyed)
        {
            RefreshBaseSpeedAndTargetSpeed();
        }

        bool isWave = waveMotionEnabled;
        bool isSpiral = spiralMotionEnabled;

        bool physicsFirst = false;
        if (!isBeingDestroyed)
        {
            float nowU = Time.unscaledTime;
            if (nowU < reflectOverrideUntil)
            {
                physicsFirst = true;

                if (rb != null)
                {
                    Vector2 vPhys = rb.linearVelocity;
                    if (vPhys.sqrMagnitude > 0.0001f)
                    {
                        lastNonZeroDir = vPhys.normalized;
                        direction = lastNonZeroDir;
                    }
                }
            }
            else
            {
                if (reflectOverrideUntil > -998f)
                {
                    reflectOverrideUntil = -999f;

                    if (rb != null)
                    {
                        Vector2 vPhys = rb.linearVelocity;
                        if (vPhys.sqrMagnitude > 0.0001f)
                        {
                            direction = vPhys.normalized;
                            lastNonZeroDir = direction;
                        }
                    }
                }
            }
        }

        if (rb != null && !isBeingDestroyed)
        {
            Vector2 v0 = rb.linearVelocity;

            if (v0.sqrMagnitude > 0.0001f)
            {
                lastNonZeroDir = v0.normalized;

                if (!(isWave || isSpiral) && !physicsFirst)
                {
                    direction = lastNonZeroDir;
                }
            }

            float min = Mathf.Max(0f, reviveMinSpeed);
            if (min > 0f)
            {
                float minSqr = min * min;
                if (v0.sqrMagnitude < minSqr)
                {
                    float now = Time.unscaledTime;
                    float cd = Mathf.Max(0f, reviveCooldownSeconds);
                    if (cd <= 0f || (now - lastReviveTime) >= cd)
                    {
                        lastReviveTime = now;

                        Vector2 dir = (lastNonZeroDir.sqrMagnitude > 0.0001f) ? lastNonZeroDir : Vector2.down;

                        float ts = Mathf.Max(0.01f, TargetSpeed);
                        float timeScale = GetTimeScale();
                        rb.linearVelocity = dir.normalized * ts * timeScale;
                        if (showDebugLog)
                        {
                            Debug.Log($"[BulletVelLog] id={GetInstanceID()} tag={debugTag} Revive | where=Update.AntiStop | vel={rb.linearVelocity}");
                        }
                    }
                }
            }
        }

        if (rb != null && !isBeingDestroyed && !(isWave || isSpiral) && !physicsFirst)
        {
            Vector2 v = rb.linearVelocity;
            if (v.sqrMagnitude > 0.0001f)
            {
                float cur = v.magnitude;
                float timeScale = GetTimeScale();
                float next = Mathf.Lerp(cur, TargetSpeed, Time.deltaTime * timeScale * speedLerp);
                rb.linearVelocity = v.normalized * next;
                // Debug.Log($"[BulletVelLog] id={GetInstanceID()} tag={debugTag} SpeedLerp | where=Update.SpeedLerp | vel={rb.linearVelocity}");
            }
        }

        if (rb != null && !isBeingDestroyed && !physicsFirst)
        {
            // ★MissileArc有効時はコルーチン内で速度制御するため、ここでは呼ばない
            // ★Wave/Spiral有効時は毎フレーム速度更新が必要なので、ApplyVelocityを呼ぶ
            bool isMissileArc = missileArcEnabled;

            if (!isMissileArc)
            {
                ApplyVelocity();
            }
        }

        // デバッグ：Update()終了時のvelocity確認（最初の20フレームのみ）
        if (rb != null && debugFrameCount < 20)
        {
            debugFrameCount++;
            // Debug.Log($"[BulletVelLog] id={GetInstanceID()} tag={debugTag} END Update | vel={rb.linearVelocity} | frame={debugFrameCount}");
        }

        if (visualRenderer != null && spriteRotateSpeed != 0f && !isBeingDestroyed)
        {
            visualRenderer.transform.Rotate(0f, 0f, spriteRotateSpeed * Time.deltaTime);
        }
    }

    private void LateUpdate()
    {
        // vfxParent 注入（Awakeで feedback取得している前提。LateUpdateで確実化）
        if (feedback != null) feedback.SetDefaultVfxParent(vfxParent);

        EnsureExplosionInit();
        TickCountdownExplosion();

        TickExplosionRing();
        TickExplosionBlink();
        TickCountdownBeepSe();
    }

    private void OnDestroy()
    {
        DestroyExplosionRing();

        // Beep SE停止
        if (feedback != null) feedback.StopCountdownBeep();
    }
}
