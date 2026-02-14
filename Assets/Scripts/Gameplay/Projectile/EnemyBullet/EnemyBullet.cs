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
    private int lastPaddleBounceFrame = -999;

    public int RemainingPaddleBounces => remainingPaddleBounces;

    private bool hasPaddleReflectedOnce = false;
    public bool HasPaddleReflectedOnce => hasPaddleReflectedOnce;

    private int lastEnemyHitCountFrame = -999;
    private int lastWallBounceFrame = -999;

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

    [SerializeField] private GameObject warpDisappearVfxPrefab;
    [SerializeField] private GameObject warpReappearVfxPrefab;
    [SerializeField] private AudioClip warpDisappearSe;
    [SerializeField] private AudioClip warpReappearSe;

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

    [SerializeField] private float multiChildB_Delay = 0.1f;
    [SerializeField] private float multiChildB_DelayMax = 0.1f;
    [SerializeField] private AudioClip multiChildB_SpawnSe;
    [SerializeField] private GameObject multiChildB_SpawnVfx;
    [SerializeField] private Sprite multiChildB_Sprite;
    [SerializeField] private float multiChildB_LifeTime = 5f;

    private bool multiWarheadDone = false;
    private Coroutine multiWarheadCo;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        bulletCol = GetComponent<Collider2D>();

        // ★弾は最初 UnreflectedBullet Layer（敵と衝突しない）
        int unreflectedLayer = LayerMask.NameToLayer("UnreflectedBullet");
        if (unreflectedLayer == -1)
        {
            Debug.LogError("[EnemyBullet] Layer 'UnreflectedBullet' NOT FOUND! Create it in: Edit > Project Settings > Tags and Layers");
        }
        else
        {
            gameObject.layer = unreflectedLayer;
            Debug.Log($"[EnemyBullet] Awake - Set layer to UnreflectedBullet (index: {unreflectedLayer})");
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

        ResetPaddleBounceRemaining();

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

        // Explosion runtime init flags (in Explosion partial)
        explosionInitDone = false;
        explosionRingCreated = false;
        explosionBlinkVisible = true;
        explosionNextBlinkToggleTime = -999f;

        // Warp runtime init
        warpDone = false;
        warpCo = null;

        // ブロックダメージをSkillManagerから取得
        if (SkillManager.Instance != null)
        {
            SkillManager.Instance.GetBlockDamage(out float normalDmg, out float justDmg);
            BlockNormalDamage = normalDmg;
            BlockJustDamage = justDmg;
        }
    }

    public void SetOwnerCollisionIgnore(Collider2D owner, float seconds)
    {
        if (!ignoreOwnerCollision) return;
        if (bulletCol == null || owner == null) return;

        Debug.Log($"[EnemyBullet] SetOwnerCollisionIgnore called: {owner.gameObject.name}");

        // ★複数パーツ敵対応：複数の Collider を無視できるようにする
        if (ownerCol == null)
        {
            ownerCol = owner;  // 後方互換性のため、最初の1つは ownerCol に保存
        }

        if (!ownerColliders.Contains(owner))
        {
            ownerColliders.Add(owner);
            Debug.Log($"[EnemyBullet] Added to ownerColliders. Total: {ownerColliders.Count}");
        }

        ignoreOwnerUntil = Time.time + Mathf.Max(0.01f, seconds);
        Physics2D.IgnoreCollision(bulletCol, owner, true);
        Debug.Log($"[EnemyBullet] IgnoreCollision set TRUE until {ignoreOwnerUntil}");
    }

    public void SetUnreflectedCollisionDisable(float seconds)
    {
        if (seconds <= 0f) return;

        unreflectedCollisionDisableUntil = Time.time + seconds;
        unreflectedCollisionDisabled = true;
        // Collider2Dは無効化しない（白線・赤線との判定は有効のまま）
    }

    public void MarkReflected()
    {
        IsReflected = true;

        // ★未反射弾→反射弾：Layerを変更して敵との物理衝突を有効化
        int unreflectedLayer = LayerMask.NameToLayer("UnreflectedBullet");
        int reflectedLayer = LayerMask.NameToLayer("ReflectedBullet");

        Debug.Log($"[EnemyBullet] MarkReflected called. Current layer: {gameObject.layer}, UnreflectedLayer: {unreflectedLayer}, ReflectedLayer: {reflectedLayer}");

        if (reflectedLayer == -1)
        {
            Debug.LogError("[EnemyBullet] Layer 'ReflectedBullet' NOT FOUND! Create it in: Edit > Project Settings > Tags and Layers");
            return;
        }

        if (gameObject.layer == unreflectedLayer)
        {
            gameObject.layer = reflectedLayer;
            Debug.Log($"[EnemyBullet] Layer changed: UnreflectedBullet ({unreflectedLayer}) → ReflectedBullet ({reflectedLayer})");
        }
        else
        {
            Debug.LogWarning($"[EnemyBullet] Current layer ({gameObject.layer}) is not UnreflectedBullet ({unreflectedLayer}), skipping layer change");
        }
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
                Destroy(gameObject);
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
                        Debug.Log($"[BulletVelLog] id={GetInstanceID()} tag={debugTag} Revive | where=Update.AntiStop | vel={rb.linearVelocity}");
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
