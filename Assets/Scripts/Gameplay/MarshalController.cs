using System.Collections;
using UnityEngine;

/// <summary>
/// Area08雑魚敵「Marshal」専用コントローラー。
/// Zephyr方式の操舵移動（Perlin Noiseのふわふわ旋回＋境界回避）をベースに、
/// 「ホバリング静止→ジェット噴射バースト」の緩急と機体バンクを追加。
/// 手のひら砲アタック中は全身スプライトごとプレイヤー方向を向く（片腕分のみ作成しflipXで左右使い回し）、
/// 肩のMissileは固定位置でランダムに交互/同時発射。
/// フレーム配列＋Editor Preview機構はArcGuardControllerと同じ構成。
/// </summary>
[RequireComponent(typeof(EnemyStats))]
[RequireComponent(typeof(EnemyDamageReceiver))]
public class MarshalController : MonoBehaviour
{
    public enum MarshalPreviewSprite
    {
        Idle1, Idle2, Idle3, Idle4, Idle5, Idle6, Idle7, IdleAnimate,
        Move1, Move2, Move3, Move4, MoveAnimate,
        AttackWindup, AttackThrust, AttackAnimate,
        BreathCharge1, BreathCharge2, BreathCharge3, BreathFire, BreathAnimate,
        Wing1, Wing2a, Wing2b, Wing2c, Wing3, Wing4a, Wing4b, WingAnimate,
        Bite1, Bite2, Bite3, BiteAnimate,
        Roar1, Roar2, Roar3, Roar4, Roar5, RoarAnimate,
        Hit1, HitAnimate
    }

    [System.Serializable]
    public class MarshalFrame
    {
        public Sprite  sprite;
        public Vector2 offset;
        [Tooltip("表示秒数。durationMaxを0より大きくすると、こちらの値は無視されてdurationMin〜durationMaxのランダムな秒数になる")]
        public float   duration;
        [Tooltip("表示秒数のランダム幅（最小）。durationMaxが0より大きい時だけ有効")]
        public float   durationMin;
        [Tooltip("表示秒数のランダム幅（最大）。0より大きい値を入れるとランダム表示秒数が有効になる")]
        public float   durationMax;
        [Tooltip("マズル位置（腕アタックスプライトのローカル座標。発射フレームのみ有効）")]
        public Vector2 muzzleOffset;
        [Tooltip("当たり判定オフセット（Polygon Collider2Dの形状はPrefab側固定、位置だけここで微調整する）")]
        public Vector2 colliderOffset;
        [Tooltip("スプライトのZ軸回転（度）")]
        public float   rotationZ;
    }

    // =========================================================
    // References
    // =========================================================

    [Header("References")]
    [SerializeField] private SpriteRenderer bodySpriteRenderer;
    [SerializeField] private PolygonCollider2D bodyCollider;
    [SerializeField] private Transform shoulderMuzzleL;
    [SerializeField] private Transform shoulderMuzzleR;
    [Tooltip("手のひらAttackの発射位置プレビュー用（任意）。Bodyの子に空Transformを置いてアサインすると、Play前でも選択中のAttackプレビューフレームのmuzzleOffsetに追従して動き、Gizmoで確認できます")]
    [SerializeField] private Transform palmMuzzlePreview;
    [SerializeField] private EnemyData enemyData;

    [Header("Debug")]
    [SerializeField] private bool showDebugLog = false;

    // =========================================================
    // Editor Preview
    // =========================================================

    [Header("Editor Preview")]
    [SerializeField] private MarshalPreviewSprite previewSprite = MarshalPreviewSprite.Idle1;

    [Header("Sprites - Idle（4枚ループ。配列の並び順=再生順で登録すること：Idle→Idle2→Idle4→Idle3の順）")]
    [NonReorderable]
    [SerializeField] private MarshalFrame[] idleFrames;

    [Header("Sprites - Move（片側のみ作成、逆方向はflipXで反転使用）")]
    [NonReorderable]
    [SerializeField] private MarshalFrame[] moveFrames;

    [Tooltip("ONの場合、moveFramesは使わずidleFramesを移動中もそのまま再生する")]
    [SerializeField] private bool reuseIdleFramesForMove = false;

    [Header("Sprites - Arm Attack（溜め→射出の2枚。左右腕とも同じ配列をflipXで使い回す）")]
    [Tooltip("index 0=溜め, index 1=射出。射出フレームに到達した時点で発射をトリガーする")]
    [NonReorderable]
    [SerializeField] private MarshalFrame[] attackFrames;
    [SerializeField] private int attackFireTriggerFrame = 1;

    [Header("Sprites - Breath（溜め3枚+ブレス1枚。配列順=溜め1→溜め2→溜め3→ブレスの順で登録すること）")]
    [NonReorderable]
    [SerializeField] private MarshalFrame[] breathFrames;

    [Header("Sprites - Wing（7枚。配列順=1→2a→2b→2c→3→4a→4bの順で登録すること）")]
    [NonReorderable]
    [SerializeField] private MarshalFrame[] wingFrames;

    [Header("Sprites - Bite（噛みつき3枚。配列順=1→2→3の順で登録すること）")]
    [NonReorderable]
    [SerializeField] private MarshalFrame[] biteFrames;

    [Header("Sprites - Roar（咆哮5枚。配列順=1→2→3→4→5の順で登録すること。4で弾のリングを発射）")]
    [NonReorderable]
    [SerializeField] private MarshalFrame[] roarFrames;

    [Header("Sprites - Hit（被弾）")]
    [NonReorderable]
    [SerializeField] private MarshalFrame[] hitFrames;

    // =========================================================
    // Fade In
    // =========================================================

    [Header("Fade In")]
    [SerializeField] private float stage12FadeInDuration = 1f;
    [SerializeField] private float stage3FadeInDuration = 3f;

    // =========================================================
    // Movement（Zephyr方式の操舵 + ホバリング静止/バースト移動の緩急 + 機体バンク）
    // =========================================================

    [Header("Movement")]
    [SerializeField] private float bodyRadius = 0.6f;
    [Tooltip("バースト時の基準速度（ワールド単位/秒）。実際の速度はこれにburstSpeedMultiplierを掛けた値")]
    [SerializeField] private float baseMoveSpeed = 1.2f;

    [Header("Wander（ふわふわ揺らぎ旋回。Perlin Noiseで滑らかにカーブする）")]
    [SerializeField] private float wanderTurnSpeed = 40f;
    [SerializeField] private float wanderNoiseFrequency = 0.25f;

    [Header("Sharp Turn（たまに混ざる急旋回）")]
    [SerializeField] private float sharpTurnIntervalMin = 3f;
    [SerializeField] private float sharpTurnIntervalMax = 6f;
    [SerializeField] private float sharpTurnAngleRange = 120f;
    [SerializeField] private float sharpTurnDuration = 0.25f;

    [Header("Hover / Burst（静止と噴射バーストの緩急。Zephyrとの差別化）")]
    [Tooltip("ホバリング静止時間（秒・最小/最大）")]
    [SerializeField] private float hoverDurationMin = 0.6f;
    [SerializeField] private float hoverDurationMax = 1.4f;
    [Tooltip("バースト移動時間（秒・最小/最大）")]
    [SerializeField] private float burstDurationMin = 0.5f;
    [SerializeField] private float burstDurationMax = 1f;
    [Tooltip("バースト中の速度（ホバリングの何倍か）")]
    [SerializeField] private float burstSpeedMultiplier = 4f;
    [Tooltip("バースト開始時、初速から最高速まで加速し終わるのにかかる時間（秒）")]
    [SerializeField] private float burstAccelDuration = 0.4f;
    [Tooltip("ホバリング中の微小な漂い速度（完全静止だと不自然なため）")]
    [SerializeField] private float hoverDriftSpeed = 0.15f;
    [Tooltip("バースト中も完全な直線にせず、この角度幅でランダムに揺らぐ（度）")]
    [SerializeField] private float burstWobbleDegrees = 12f;

    [Header("Banking（移動方向への機体傾き。Zephyrは傾けないため差別化になる）")]
    [SerializeField] private float bankAngleMax = 18f;
    [SerializeField] private float bankLerpSpeed = 6f;

    [Header("Screen Bounds")]
    [SerializeField] private float screenMargin = 0.5f;
    [SerializeField] private float skillHudPixelWidth = 280f;

    [Header("Floor Avoidance")]
    [SerializeField] private FloorHealth floorObject;
    [SerializeField] private float floorAvoidDistance = 1.5f;

    [Header("Boundary Avoidance（画面端・Floorに近づいたら内側へ操舵する）")]
    [SerializeField] private float boundaryAvoidMargin = 1.5f;
    [SerializeField] private float boundaryAvoidTurnSpeed = 180f;

    // =========================================================
    // Arm Attack（手のひら砲。攻撃中は全身スプライトごとプレイヤー方向を向く・左右交互発射）
    // =========================================================

    [Header("Arm Attack")]
    [Tooltip("アタック中、傾き目標角度へ本体を向ける回転速度（度/秒）。0=即時スナップ")]
    [SerializeField] private float attackRotSpeed = 300f;
    [Tooltip("アタック中の本体の傾き角度（度）。移動方向に合わせて左右どちらに傾くか決まる（右移動=右傾き、左移動=左傾き）")]
    [SerializeField] private float attackAngleClampDegrees = 45f;
    [Tooltip("弾種で個別指定が無い場合のフォールバックインデックス（Normal/Wave/SpiralはEnemyDataのFiring Routineで比率調整）")]
    [SerializeField] private int palmBulletTypeIndexFallback = 0;
    [Tooltip("左右交互で1発ずつ撃つ間隔（秒）")]
    [SerializeField] private float armFireInterval = 1.8f;

    // =========================================================
    // Shoulder Missile（肩。固定位置・ランダムで交互/同時）
    // =========================================================

    [Header("Shoulder Missile")]
    [SerializeField] private int missileBulletTypeIndex = 8;
    [SerializeField] private float missileFireInterval = 3.5f;
    [Tooltip("両肩同時発射になる確率（0〜1）。外れた場合は交互発射")]
    [Range(0f, 1f)]
    [SerializeField] private float missileSimultaneousChance = 0.35f;

    // =========================================================
    // Bullet Spawn 共通設定
    // =========================================================

    [Header("Bullet Spawn")]
    [SerializeField] private EnemyBullet bulletPrefab;
    [SerializeField] private EnemyBeamBullet beamBulletPrefab;
    [SerializeField] private Transform projectileRoot;
    [SerializeField] private float fallbackBulletSpeed = 6f;
    [SerializeField] private float fallbackBulletLifeTime = 5f;
    [SerializeField] private float ignoreOwnerTime = 0.15f;

    [Header("Fire SE")]
    [SerializeField] private AudioClip palmFireSE;
    [Range(0f, 1f)] [SerializeField] private float palmFireSEVolume = 1f;
    [SerializeField] private AudioClip missileFireSE;
    [Range(0f, 1f)] [SerializeField] private float missileFireSEVolume = 1f;
    [SerializeField] private AudioClip breathFireSE;
    [Range(0f, 1f)] [SerializeField] private float breathFireSEVolume = 1f;
    [SerializeField] private AudioClip biteFireSE;
    [Range(0f, 1f)] [SerializeField] private float biteFireSEVolume = 1f;
    [SerializeField] private AudioClip wingFireSE;
    [Range(0f, 1f)] [SerializeField] private float wingFireSEVolume = 1f;
    [SerializeField] private AudioClip roarFireSE;
    [Range(0f, 1f)] [SerializeField] private float roarFireSEVolume = 1f;

    // =========================================================
    // Breath Attack（溜め3枚→ブレス。Single/Double/扇ブレスの3種）
    // =========================================================

    [Header("Breath Attack")]
    [Tooltip("EnemyDataのbulletTypes内、Beam設定（useBeam=ON）にしたブレス用弾種のインデックス")]
    [SerializeField] private int breathBulletTypeIndex = 14;
    [Tooltip("ダブルブレス：1発目と2発目の角度オフセット（度）。重なって相殺して見えるのを防ぐため、この半分ずつ左右にずらす")]
    [SerializeField] private float doubleBreathAngleOffsetDeg = 10f;
    [Tooltip("ダブルブレス：1発目から2発目までの間隔（秒）")]
    [SerializeField] private float doubleBreathInterval = 0.15f;
    [Tooltip("扇ブレス：掃射する角度範囲（度）。プレイヤー方向を中心にこの半分ずつ左右へ振る")]
    [SerializeField] private float fanSweepAngleRangeDeg = 60f;
    [Tooltip("扇ブレス：端から端まで掃射しきるのにかかる秒数")]
    [SerializeField] private float fanSweepDuration = 1.2f;

    // =========================================================
    // Bite Attack（噛みつき3枚）
    // =========================================================

    [Header("Bite Attack")]
    [Tooltip("EnemyDataのbulletTypes内、MultiShot設定（useMultiShot=ON）にした噛みつき用弾種のインデックス")]
    [SerializeField] private int biteBulletTypeIndex = 6;

    // =========================================================
    // Wing Attack（ウィング7枚。振り下ろし中の3フレームでMuzzle位置をずらしながら両翼から発射）
    // =========================================================

    [Header("Wing Attack")]
    [Tooltip("EnemyDataのbulletTypes内、Spiral設定（useSpiralMotion=ON）にしたウィング用弾種のインデックス")]
    [SerializeField] private int wingBulletTypeIndex = 4;

    // =========================================================
    // Roar Attack（咆哮5枚。ArcGuardのRoarRoutine/FireRoarRingと同じ考え方で、
    // 咆哮4（index 3）で全方位にリング状の弾を発射する）
    // =========================================================

    [Header("Roar Attack")]
    [Tooltip("EnemyDataのbulletTypes内、咆哮リング用弾種のインデックス")]
    [SerializeField] private int roarBulletTypeIndex = 13;
    [Tooltip("リング1周あたりに撃つ弾数")]
    [SerializeField] private int roarBulletCount = 16;
    [Tooltip("リングを何回繰り返すか（2回以上だと角度をずらして密度を上げられる）")]
    [SerializeField] private int roarRingRepeatCount = 1;
    [Tooltip("リング繰り返し時の角度オフセット（度）")]
    [SerializeField] private float roarRingRepeatAngleOffsetDeg = 11.25f;
    [Tooltip("リング繰り返し時の間隔（秒）")]
    [SerializeField] private float roarRingRepeatInterval = 0.2f;

    // =========================================================
    // Attack Pattern（本番の攻撃パターン制御。HP70%でFront→Backへ切り替わる。
    // ArcGuard/GuardBeastと同じPhase方式。
    // Front: ③×2→②×1→③×1のループ、攻撃breathInterruptCountFront回ごとにシングルブレスを割り込み発動
    // Back : ③×2→②×1→③×1→④×1のループ、攻撃breathInterruptCountBack回ごとにダブル/扇左/扇右をランダムで割り込み発動
    // 割り込みブレスは攻撃回数に含まない。割り込み後は次の予定攻撃から再開する
    // =========================================================

    private enum DragonPhase { Front, Back }
    private enum MainAttackKind { Bite, Wing, Roar }

    private static readonly MainAttackKind[] AttackSequenceFront =
        { MainAttackKind.Bite, MainAttackKind.Bite, MainAttackKind.Wing, MainAttackKind.Bite };
    private static readonly MainAttackKind[] AttackSequenceBack =
        { MainAttackKind.Bite, MainAttackKind.Bite, MainAttackKind.Wing, MainAttackKind.Bite, MainAttackKind.Roar };

    [Header("Attack Pattern（本番の攻撃パターン制御）")]
    [Tooltip("このHP%以下になるとFront→Backフェーズへ切り替わる")]
    [SerializeField] private float phaseTransitionHpThreshold = 70f;
    [Tooltip("Frontフェーズ：通常攻撃が何回終わるごとにブレスを割り込み発動するか")]
    [SerializeField] private int breathInterruptCountFront = 3;
    [Tooltip("Backフェーズ：通常攻撃が何回終わるごとにブレスを割り込み発動するか")]
    [SerializeField] private int breathInterruptCountBack = 2;
    [Tooltip("各攻撃の後、次の攻撃までの間隔（秒・最小/最大）")]
    [SerializeField] private float attackPatternIntervalMin = 0.4f;
    [SerializeField] private float attackPatternIntervalMax = 0.8f;

    // =========================================================
    // インスタンス変数
    // =========================================================

    private EnemyStats stats;
    private EnemyDamageReceiver damageReceiver;
    private EnemyMover enemyMover;
    private PixelDancerController player;
    private bool isDead;

    private float SlowMultiplier => (enemyMover != null) ? enemyMover.SpeedMultiplier : 1f;

    private float headingDeg;
    private float noiseSeed;
    private float sharpTurnTimer;
    private bool isSharpTurning;
    private float sharpTurnElapsed;
    private float sharpTurnStartHeading;
    private float sharpTurnTargetHeading;

    private bool isBursting;
    private float hoverBurstTimer;
    private float currentBankAngle;
    private float currentMoveSpeed;

    private float screenXMin, screenXMax, screenYMin, screenYMax;
    private float floorAvoidY = float.MinValue;

    // 各攻撃コルーチンが自分の専用フラグだけを書き込み、isAttackingはその論理和として算出する。
    // 共有フラグに直接書き込む方式だと、ある攻撃の終了処理が別の攻撃中のisAttackingを
    // 誤ってfalseに倒し、アニメーションが割り込まれる不具合が起きるため
    private bool isArmAttacking;
    private bool isAttacking => isArmAttacking || isBreathAttacking || isBiteAttacking || isWingAttacking || isRoarAttacking;
    private bool armNextIsLeft = true;
    private float armFireTimer;
    private Coroutine armAttackCoroutine;

    private bool armCycleEnabled;
    private bool armCycleIsFiringPhase = true;
    private float armCycleFireSeconds;
    private float armCyclePauseSeconds;
    private float armCyclePhaseEndTime = -999f;
    private EnemyData.BulletType armCurrentBulletType;

    private float missileFireTimer;

    private bool isBreathAttacking;
    private EnemyBeamBullet activeBreathBeam;

    private bool isBiteAttacking;
    private bool isWingAttacking;
    private bool isRoarAttacking;

    private DragonPhase dragonPhase = DragonPhase.Front;
    private bool dragonPhaseTransitioned;
    private int attackSequenceIndex;
    private int attacksSinceBreathInterrupt;
    private Coroutine attackPatternCoroutine;

    private bool missileCycleEnabled;
    private bool missileCycleIsFiringPhase = true;
    private float missileCycleFireSeconds;
    private float missileCyclePauseSeconds;
    private float missileCyclePhaseEndTime = -999f;

    private int idleFrameIndex;
    private float idleFrameTimer;
    private float idleFrameCurrentDuration;
    private int moveFrameIndex;
    private float moveFrameTimer;
    private float moveFrameCurrentDuration;

    private Coroutine fadeInCoroutine;
    private Coroutine hitCoroutine;

    // =========================================================
    // Unity ライフサイクル
    // =========================================================

    private void Awake()
    {
        stats = GetComponent<EnemyStats>();
        damageReceiver = GetComponent<EnemyDamageReceiver>();
        enemyMover = GetComponentInParent<EnemyMover>();
        if (enemyMover != null) enemyMover.suppressMovement = true;

        if (bodySpriteRenderer == null) bodySpriteRenderer = GetComponentInChildren<SpriteRenderer>();

        if (stats != null)
        {
            stats.onKilled += HandleKilled;
        }

        EnemyPart bodyPart = bodySpriteRenderer != null ? bodySpriteRenderer.GetComponent<EnemyPart>() : null;
        if (bodyPart != null) bodyPart.OnHitWithDamage += HandleHitWithDamage;

        if (projectileRoot == null)
        {
            GameObject pr = GameObject.Find("ProjectileRoot");
            if (pr != null) projectileRoot = pr.transform;
        }
    }

    private void Start()
    {
        EnemyShooter es = GetComponent<EnemyShooter>();
        if (es != null)
        {
            if (enemyData == null) enemyData = es.GetEnemyData();
            if (bulletPrefab == null) bulletPrefab = es.GetBulletPrefab();
            if (beamBulletPrefab == null) beamBulletPrefab = es.GetBeamBulletPrefab();
            if (projectileRoot == null) projectileRoot = es.GetProjectileRoot();
            es.enabled = false;
        }

        player = FindObjectOfType<PixelDancerController>();
    }

    private void OnEnable()
    {
        CacheFloorY();
        RefreshScreenBounds();

        headingDeg = Random.Range(0f, 360f);
        noiseSeed = Random.Range(0f, 1000f);

        sharpTurnTimer = Random.Range(sharpTurnIntervalMin, sharpTurnIntervalMax);
        isSharpTurning = false;

        isBursting = false;
        hoverBurstTimer = Random.Range(hoverDurationMin, hoverDurationMax);
        currentBankAngle = 0f;
        currentMoveSpeed = hoverDriftSpeed;

        armFireTimer = Random.Range(0f, armFireInterval);
        armCycleEnabled = false;
        armCycleIsFiringPhase = true;
        armCyclePhaseEndTime = -999f;
        armCurrentBulletType = null;
        missileFireTimer = Random.Range(0f, missileFireInterval);
        missileCycleEnabled = false;
        missileCycleIsFiringPhase = true;
        missileCyclePhaseEndTime = -999f;
        isArmAttacking = false;

        isBreathAttacking = false;
        isBiteAttacking = false;
        isWingAttacking = false;
        isRoarAttacking = false;

        dragonPhase = DragonPhase.Front;
        dragonPhaseTransitioned = false;
        attackSequenceIndex = 0;
        attacksSinceBreathInterrupt = 0;

        isDead = false;

        idleFrameIndex = 0;
        idleFrameTimer = 0f;
        moveFrameIndex = 0;
        moveFrameTimer = 0f;
        ApplyIdleFrame(0);

        if (fadeInCoroutine != null) StopCoroutine(fadeInCoroutine);
        fadeInCoroutine = StartCoroutine(FadeInBody());

        // Bite/Wing/Roar/Breath用のフレームが1つも設定されていない場合（Marshal等、Dragon用の
        // 攻撃パターンを持たない既存エネミー）は、このループ自体を起動しない。起動すると
        // 空配列アクセスで例外が起きコルーチンが異常終了し、isXAttackingがtrueのまま
        // 戻らなくなって移動が永久停止するバグになる
        bool hasAttackPatternFrames =
            (biteFrames != null && biteFrames.Length > 0) ||
            (wingFrames != null && wingFrames.Length > 0) ||
            (roarFrames != null && roarFrames.Length > 0) ||
            (breathFrames != null && breathFrames.Length > 0);

        if (attackPatternCoroutine != null) StopCoroutine(attackPatternCoroutine);
        if (hasAttackPatternFrames)
        {
            attackPatternCoroutine = StartCoroutine(AttackPatternLoop());
        }

#if UNITY_EDITOR
        UnityEditor.EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        UnityEditor.EditorApplication.update += OnEditorTickRefresh;
#endif
    }

    private void OnDisable()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        UnityEditor.EditorApplication.update -= OnEditorTickRefresh;
        StopEditorAnim();
#endif
    }

    private void OnDestroy()
    {
        isDead = true;
        if (stats != null) stats.onKilled -= HandleKilled;
        // Bite/Wing/Roar/各種BreathはStartCoroutine()で個別に起動されており、
        // attackPatternCoroutineを止めるだけでは今まさに実行中のものまでは止まらないため、
        // StopAllCoroutines()で自身の全コルーチンを確実に止める
        StopAllCoroutines();
        DestroyActiveBreathBeam();
    }

    private void HandleKilled()
    {
        isDead = true;
        StopAllCoroutines();
        DestroyActiveBreathBeam();
    }

    // 発射済みのBreath BeamはDragon本体とは別のGameObjectとして自分の寿命で動き続けるため、
    // 倒された（またはDragon自体が何らかの理由で破棄された）タイミングで明示的に後始末する。
    // Destroy()はそのフレームの終わりまで実際には破棄されないため、内部のダメージ判定コルーチンが
    // 同じフレーム中にもう1回だけ実行されてしまうことがある。StopAllCoroutines()で先に止めておく
    private void DestroyActiveBreathBeam()
    {
        if (activeBreathBeam == null) return;
        activeBreathBeam.StopAllCoroutines();
        Destroy(activeBreathBeam.gameObject);
        activeBreathBeam = null;
    }

    private void HandleHitWithDamage(EnemyBullet bullet, int damage)
    {
        if (isDead) return;
        // 攻撃ポーズ中はヒットスプライトで上書きしない。
        // 特にBreath/Roarの発射保持中はSpriteを再セットせず待つだけの実装なので、
        // ここで割り込むと誰も攻撃スプライトに戻さないまま被弾スプライトが残ってしまう
        if (isAttacking) return;
        if (hitFrames == null || hitFrames.Length == 0) return;

        if (hitCoroutine != null) StopCoroutine(hitCoroutine);
        hitCoroutine = StartCoroutine(PlayHitOnce());
    }

    private float GetTimeScale() =>
        SlowMotionManager.Instance != null ? SlowMotionManager.Instance.TimeScale : 1f;

    private void Update()
    {
        if (isDead) return;

        float dt = Time.deltaTime * GetTimeScale() * SlowMultiplier;

        // ブレス発射中（溜め〜ビームのLifetimeが残っている間）は移動せずその場に停滞する
        bool suppressMoveForBreath = isBreathAttacking || activeBreathBeam != null;
        if (!suppressMoveForBreath) ApplyMove(dt);

        if (isAttacking)
        {
            ApplyAttackTilt();
        }
        else
        {
            ApplyBanking(dt);
            TickBodyAnimation(dt);
        }

        TickArmAttack(dt);
        TickMissile(dt);
    }

    // =========================================================
    // フェードイン（Zephyr/Gyrorbと同じ方式）
    // =========================================================

    private IEnumerator FadeInBody()
    {
        // stageIndex取得より前に、まず1フレーム目からアルファ0にしておく。
        // ここをyield return null;の後に回すと、出現直後の1フレームだけ不透明のまま描画されてしまい、
        // 一瞬点滅して見えるバグになる
        if (bodySpriteRenderer != null)
        {
            Color initial = bodySpriteRenderer.color;
            bodySpriteRenderer.color = new Color(initial.r, initial.g, initial.b, 0f);
        }

        yield return null;

        int stageIndex = stats?.GetSpawner()?.GetCurrentStageIndex() ?? 0;
        float fadeInDuration = (stageIndex == 2) ? stage3FadeInDuration : stage12FadeInDuration;

        if (bodySpriteRenderer == null || fadeInDuration <= 0f) yield break;

        Color original = bodySpriteRenderer.color;

        float elapsed = 0f;
        while (elapsed < fadeInDuration)
        {
            elapsed += Time.deltaTime * GetTimeScale();
            float alpha = Mathf.Clamp01(elapsed / fadeInDuration);
            bodySpriteRenderer.color = new Color(original.r, original.g, original.b, alpha);
            yield return null;
        }

        bodySpriteRenderer.color = new Color(original.r, original.g, original.b, 1f);
    }

    // =========================================================
    // 画面境界・Floor回避
    // =========================================================

    private void CacheFloorY()
    {
        if (floorObject == null)
            floorObject = FindObjectOfType<FloorHealth>();

        if (floorObject != null)
        {
            Collider2D col = floorObject.GetComponent<Collider2D>();
            float floorTopY = col != null ? col.bounds.max.y : floorObject.transform.position.y;
            floorAvoidY = floorTopY + floorAvoidDistance;
        }
    }

    private void RefreshScreenBounds()
    {
        if (Camera.main == null) return;

        float halfH = Camera.main.orthographicSize;
        float halfW = halfH * Camera.main.aspect;
        Vector3 camPos = Camera.main.transform.position;

        float skillHudWorldOffset = 0f;
        if (skillHudPixelWidth > 0f && Screen.width > 0)
        {
            float worldUnitsPerPixel = (halfW * 2f) / Screen.width;
            skillHudWorldOffset = skillHudPixelWidth * worldUnitsPerPixel;
        }

        screenXMin = camPos.x - halfW + screenMargin + bodyRadius + skillHudWorldOffset;
        screenXMax = camPos.x + halfW - screenMargin - bodyRadius;
        screenYMin = camPos.y - halfH + screenMargin + bodyRadius;
        screenYMax = camPos.y + halfH - screenMargin - bodyRadius;
    }

    // =========================================================
    // 移動：ホバリング静止 ⇔ バースト移動（Zephyrとの差別化）
    // =========================================================

    private void ApplyMove(float dt)
    {
        hoverBurstTimer -= dt;
        if (hoverBurstTimer <= 0f)
        {
            isBursting = !isBursting;
            if (isBursting)
            {
                hoverBurstTimer = Random.Range(burstDurationMin, burstDurationMax);
                // バースト開始時に方向を決め直す（多少の急旋回も混ざる）
                UpdateHeading(0f, forcePick: true);
            }
            else
            {
                hoverBurstTimer = Random.Range(hoverDurationMin, hoverDurationMax);
            }
        }

        UpdateHeading(dt, forcePick: false);

        Vector2 avoidSteer = ComputeBoundaryAvoidSteer();
        if (avoidSteer.sqrMagnitude > 0.0001f)
        {
            float avoidTargetAngle = Mathf.Atan2(avoidSteer.y, avoidSteer.x) * Mathf.Rad2Deg;
            headingDeg = Mathf.MoveTowardsAngle(headingDeg, avoidTargetAngle, boundaryAvoidTurnSpeed * dt);
        }

        float burstTopSpeed = baseMoveSpeed * burstSpeedMultiplier;
        float targetSpeed = isBursting ? burstTopSpeed : hoverDriftSpeed;
        float accelPerSecond = burstTopSpeed / Mathf.Max(0.01f, burstAccelDuration);
        currentMoveSpeed = Mathf.MoveTowards(currentMoveSpeed, targetSpeed, accelPerSecond * dt);

        // バースト中も完全な直線にせず、少し揺らぎを混ぜる
        float appliedHeading = headingDeg;
        if (isBursting)
        {
            float wobble = (Mathf.PerlinNoise(noiseSeed + 50f, Time.time * wanderNoiseFrequency * 2f) * 2f - 1f) * burstWobbleDegrees;
            appliedHeading += wobble;
        }

        Vector2 moveDir = new Vector2(Mathf.Cos(appliedHeading * Mathf.Deg2Rad), Mathf.Sin(appliedHeading * Mathf.Deg2Rad));
        Vector3 pos = transform.position + (Vector3)(moveDir * currentMoveSpeed * dt);

        float yMin = (floorAvoidY > float.MinValue) ? Mathf.Max(screenYMin, floorAvoidY) : screenYMin;
        pos.x = Mathf.Clamp(pos.x, screenXMin, screenXMax);
        pos.y = Mathf.Clamp(pos.y, yMin, screenYMax);

        transform.position = pos;
    }

    private void UpdateHeading(float dt, bool forcePick)
    {
        if (forcePick)
        {
            headingDeg = Random.Range(0f, 360f);
            return;
        }

        sharpTurnTimer -= dt;
        if (!isSharpTurning && sharpTurnTimer <= 0f)
        {
            isSharpTurning = true;
            sharpTurnElapsed = 0f;
            sharpTurnStartHeading = headingDeg;
            sharpTurnTargetHeading = headingDeg + Random.Range(-sharpTurnAngleRange, sharpTurnAngleRange);
        }

        if (isSharpTurning)
        {
            sharpTurnElapsed += dt;
            float t = Mathf.Clamp01(sharpTurnElapsed / Mathf.Max(0.01f, sharpTurnDuration));
            headingDeg = Mathf.LerpAngle(sharpTurnStartHeading, sharpTurnTargetHeading, t);

            if (t >= 1f)
            {
                isSharpTurning = false;
                sharpTurnTimer = Random.Range(sharpTurnIntervalMin, sharpTurnIntervalMax);
            }
        }
        else
        {
            float noise = Mathf.PerlinNoise(noiseSeed, Time.time * wanderNoiseFrequency) * 2f - 1f;
            headingDeg += noise * wanderTurnSpeed * dt;
        }
    }

    private Vector2 ComputeBoundaryAvoidSteer()
    {
        Vector3 p = transform.position;
        float yMin = (floorAvoidY > float.MinValue) ? Mathf.Max(screenYMin, floorAvoidY) : screenYMin;
        float margin = Mathf.Max(0.01f, boundaryAvoidMargin);

        Vector2 steer = Vector2.zero;

        float distLeft = p.x - screenXMin;
        float distRight = screenXMax - p.x;
        float distBottom = p.y - yMin;
        float distTop = screenYMax - p.y;

        if (distLeft < margin) steer.x += (margin - distLeft) / margin;
        if (distRight < margin) steer.x -= (margin - distRight) / margin;
        if (distBottom < margin) steer.y += (margin - distBottom) / margin;
        if (distTop < margin) steer.y -= (margin - distTop) / margin;

        return steer;
    }

    // 移動方向へ機体をバンク（傾き）させる。Zephyrは傾けないためここが差別化点
    private void ApplyBanking(float dt)
    {
        if (bodySpriteRenderer == null) return;

        float targetBank = 0f;
        if (isBursting)
        {
            float headingRad = headingDeg * Mathf.Deg2Rad;
            float sideways = Mathf.Cos(headingRad); // 左右方向成分
            targetBank = -sideways * bankAngleMax;
        }

        currentBankAngle = Mathf.Lerp(currentBankAngle, targetBank, bankLerpSpeed * dt);
        bodySpriteRenderer.transform.localRotation = Quaternion.Euler(0f, 0f, currentBankAngle);
    }

    // =========================================================
    // Body アニメーション（ホバリング中=Idleループ、バースト中=Moveループ）
    // =========================================================

    private void TickBodyAnimation(float dt)
    {
        // reuseIdleFramesForMove時は、ホバリング⇔バースト切り替えの瞬間に再生位置が飛んで
        // カクつかないよう、常にIdle側の再生位置(index/timer)だけで駆動する
        if (reuseIdleFramesForMove)
        {
            TickIdleFrames(dt);
            if (isBursting) ApplyMoveFacingFlip();
            return;
        }

        if (isBursting) TickMoveFrames(dt);
        else TickIdleFrames(dt);
    }

    // 移動方向で本体を左右反転（片側のみ作成、反転で使い回す）
    private void ApplyMoveFacingFlip()
    {
        if (bodySpriteRenderer == null) return;
        float headingRad = headingDeg * Mathf.Deg2Rad;
        float dirX = Mathf.Cos(headingRad);
        if (Mathf.Abs(dirX) > 0.05f) bodySpriteRenderer.flipX = dirX < 0f;
    }

    private void TickIdleFrames(float dt)
    {
        if (idleFrames == null || idleFrames.Length == 0) return;

        idleFrameTimer += dt;
        if (idleFrameTimer >= idleFrameCurrentDuration)
        {
            idleFrameTimer -= idleFrameCurrentDuration;
            idleFrameIndex++;
            ApplyIdleFrame(idleFrameIndex);
        }
    }

    // Moveアニメーションで実際に使う配列（reuseIdleFramesForMove=ONならidleFramesをそのまま流用する）
    private MarshalFrame[] ActiveMoveFrames => reuseIdleFramesForMove ? idleFrames : moveFrames;

    private void TickMoveFrames(float dt)
    {
        MarshalFrame[] frames = ActiveMoveFrames;
        if (frames == null || frames.Length == 0) return;

        moveFrameTimer += dt;
        if (moveFrameTimer >= moveFrameCurrentDuration)
        {
            moveFrameTimer -= moveFrameCurrentDuration;
            moveFrameIndex++;
            ApplyMoveFrame(moveFrameIndex);
        }

        ApplyMoveFacingFlip();
    }

    private void ApplyIdleFrame(int index)
    {
        if (idleFrames == null || idleFrames.Length == 0) return;
        int len = idleFrames.Length;
        MarshalFrame f = idleFrames[((index % len) + len) % len];
        idleFrameCurrentDuration = FrameDurationOr(f, 0.2f);
        ApplyBodyFrame(f);
    }

    private void ApplyMoveFrame(int index)
    {
        MarshalFrame[] frames = ActiveMoveFrames;
        if (frames == null || frames.Length == 0) return;
        int len = frames.Length;
        MarshalFrame f = frames[((index % len) + len) % len];
        moveFrameCurrentDuration = FrameDurationOr(f, 0.15f);
        ApplyBodyFrame(f);
    }

    private void ApplyBodyFrame(MarshalFrame f)
    {
        if (f == null || bodySpriteRenderer == null) return;
        if (f.sprite != null) bodySpriteRenderer.sprite = f.sprite;
        ApplyOffset(bodySpriteRenderer, f.offset);
        ApplyCollider(f);
        ApplyMuzzlePreview(f);
    }

    private IEnumerator PlayHitOnce()
    {
        if (bodySpriteRenderer == null) yield break;
        Sprite original = bodySpriteRenderer.sprite;

        foreach (var f in hitFrames)
        {
            if (isDead) yield break;
            if (f == null) continue;
            if (f.sprite != null) bodySpriteRenderer.sprite = f.sprite;
            ApplyOffset(bodySpriteRenderer, f.offset);
            yield return WaitScaled(FrameDurationOr(f, 0.1f));
        }

        if (!isDead && bodySpriteRenderer != null) bodySpriteRenderer.sprite = original;
    }

    private IEnumerator WaitScaled(float seconds)
    {
        float t = 0f;
        while (t < seconds)
        {
            if (isDead) yield break;
            t += Time.deltaTime * GetTimeScale();
            yield return null;
        }
    }

    private static void ApplyOffset(SpriteRenderer sr, Vector2 offset)
    {
        if (sr == null) return;
        Transform t = sr.transform;
        float x = sr.flipX ? -offset.x : offset.x;
        Vector3 local = t.localPosition;
        t.localPosition = new Vector3(x, offset.y, local.z);
    }

    private void ApplyCollider(MarshalFrame frame)
    {
        if (bodyCollider == null || frame == null) return;
        Vector2 offset = frame.colliderOffset;
        if (bodySpriteRenderer != null && bodySpriteRenderer.flipX) offset.x = -offset.x;
        bodyCollider.offset = offset;
    }

    // palmMuzzlePreviewをプレビュー/現在フレームのmuzzleOffsetへ追従させる（ArcGuardのfirePointFaceと同じ方式）
    private void ApplyMuzzlePreview(MarshalFrame frame)
    {
        if (palmMuzzlePreview == null || frame == null) return;
        float x = (bodySpriteRenderer != null && bodySpriteRenderer.flipX) ? -frame.muzzleOffset.x : frame.muzzleOffset.x;
        palmMuzzlePreview.localPosition = new Vector3(x, frame.muzzleOffset.y, palmMuzzlePreview.localPosition.z);
    }

    private static float FrameDurationOr(MarshalFrame f, float fallback)
    {
        if (f == null) return fallback;
        if (f.durationMax > 0f)
        {
            float lo = Mathf.Min(f.durationMin, f.durationMax);
            float hi = Mathf.Max(f.durationMin, f.durationMax);
            return Random.Range(lo, hi);
        }
        return f.duration > 0f ? f.duration : fallback;
    }

    // =========================================================
    // 本体の傾き（アタック中のみ）
    // =========================================================

    // アタック中の本体の傾き。ApplyBankingと同じ「移動方向の左右成分」で符号を決める（右移動=右傾き、左移動=左傾き）
    private void ApplyAttackTilt()
    {
        if (bodySpriteRenderer == null) return;

        Transform t = bodySpriteRenderer.transform;
        float headingRad = headingDeg * Mathf.Deg2Rad;
        float sideways = Mathf.Cos(headingRad);
        float targetAngle = -sideways * attackAngleClampDegrees;

        if (attackRotSpeed <= 0f)
        {
            t.localRotation = Quaternion.Euler(0f, 0f, targetAngle);
        }
        else
        {
            t.localRotation = Quaternion.RotateTowards(
                t.localRotation,
                Quaternion.Euler(0f, 0f, targetAngle),
                attackRotSpeed * Time.deltaTime * GetTimeScale());
        }
    }

    // =========================================================
    // 腕アタック（左右交互。全身アタックスプライトの溜め→射出2フレームを再生し、射出フレームで発射）
    // =========================================================

    private void TickArmAttack(float dt)
    {
        if (IsFireBlockedGlobally()) return;
        if (isBreathAttacking || isBiteAttacking || isWingAttacking || isRoarAttacking) return;
        if (attackFrames == null || attackFrames.Length == 0) return;
        if (bulletPrefab == null || projectileRoot == null) return;

        // 発射タイミングはBullet types（Fire Interval / Fire Cycle）のみで決める。
        // アタックスプライトのDurationは見た目の表示時間であり、発射間隔には一切影響させない。
        UpdateArmFirePauseCycle();
        if (armCycleEnabled && !armCycleIsFiringPhase) return;

        armFireTimer -= dt;
        if (armFireTimer > 0f) return;

        // 弾種はバースト開始時（StartNewArmBurst）で1回だけ抽選済み。同じバースト中はここで再抽選しない
        EnemyData.BulletType bt = armCurrentBulletType;
        armFireTimer = GetFireInterval(bt, armFireInterval);

        bool fireLeft = armNextIsLeft;
        armNextIsLeft = !armNextIsLeft;

        // 前のアタック演出がDuration分まだ表示中でも、発射タイミングが来たら即座に切り替えて撃つ
        if (armAttackCoroutine != null) StopCoroutine(armAttackCoroutine);
        armAttackCoroutine = StartCoroutine(PlayBodyAttack(fireLeft, bt));
    }

    // 新しいバースト（＝新しい弾種）を開始する。ランダム抽選が発生するのはここだけ
    private void StartNewArmBurst()
    {
        armCurrentBulletType = PickPalmBulletType();
        ApplyArmFirePauseCycleConfig(armCurrentBulletType);
        armFireTimer = 0f; // バースト最初の1発はすぐ撃てるようにする
    }

    // EnemyShooter.UpdateFirePauseCycleと同じフェーズ管理（撃つ→止まる→撃つ、を交互に繰り返す）。
    // Fire Phaseが終わるタイミングで次のバーストの弾種を抽選し直す（StartNewArmBurst）
    private void UpdateArmFirePauseCycle()
    {
        if (armCurrentBulletType == null && !armCycleEnabled)
        {
            // まだ一度も弾種を抽選していない（起動直後 or 前回の抽選が失敗）
            StartNewArmBurst();
            return;
        }

        if (!armCycleEnabled) return; // この弾種はFire/Pause Cycle未使用。ずっと同じ弾種で撃ち続ける

        float now = Time.time;
        if (now < armCyclePhaseEndTime) return;

        if (armCycleIsFiringPhase && armCyclePauseSeconds > 0f)
        {
            armCycleIsFiringPhase = false;
            armCyclePhaseEndTime = now + armCyclePauseSeconds;
            armFireTimer = 0f;
            return;
        }

        // Fire Phase終了 → 次のバーストとして新しい弾種を抽選し直す
        StartNewArmBurst();
    }

    // 新しいバーストの弾種に応じてFire/Pause Cycle設定を反映する
    private void ApplyArmFirePauseCycleConfig(EnemyData.BulletType bt)
    {
        if (bt == null || !bt.useFirePauseCycle || bt.fireCycleSeconds <= 0f)
        {
            armCycleEnabled = false;
            return;
        }

        armCycleEnabled = true;
        armCycleFireSeconds = Mathf.Max(0.01f, bt.fireCycleSeconds);
        armCyclePauseSeconds = Mathf.Max(0f, bt.pauseCycleSeconds);
        armCycleIsFiringPhase = true;
        armCyclePhaseEndTime = Time.time + armCycleFireSeconds;
    }

    private IEnumerator PlayBodyAttack(bool isLeft, EnemyData.BulletType bt)
    {
        if (bodySpriteRenderer == null) yield break;

        isArmAttacking = true;
        // 片方（右手攻撃）だけ作成し、左手はflipXで反転使用
        bodySpriteRenderer.flipX = isLeft;

        bool fired = false;
        // 配列の長さ変更後もインデックス指定ミスで発射が遅延しないよう、範囲内にクランプする
        int triggerIdx = Mathf.Clamp(attackFireTriggerFrame, 0, attackFrames.Length - 1);

        for (int i = 0; i < attackFrames.Length; i++)
        {
            if (isDead) break;
            MarshalFrame f = attackFrames[i];
            if (f == null) continue;

            if (f.sprite != null) bodySpriteRenderer.sprite = f.sprite;
            ApplyOffset(bodySpriteRenderer, f.offset);
            ApplyCollider(f);
            ApplyMuzzlePreview(f);

            if (!fired && i >= triggerIdx)
            {
                fired = true;
                FirePalm(f, bt);
            }

            yield return WaitScaled(FrameDurationOr(f, 0.12f));
        }

        if (!fired && !isDead)
        {
            FirePalm(attackFrames[attackFrames.Length - 1], bt);
        }

        if (!isDead && bodySpriteRenderer != null)
        {
            bodySpriteRenderer.transform.localRotation = Quaternion.identity;
        }
        isArmAttacking = false;
    }

    private void FirePalm(MarshalFrame frame, EnemyData.BulletType bt)
    {
        if (IsFireBlockedGlobally()) return;
        if (bodySpriteRenderer == null) return;

        float x = bodySpriteRenderer.flipX ? -frame.muzzleOffset.x : frame.muzzleOffset.x;
        Vector3 spawnPos = bodySpriteRenderer.transform.TransformPoint(new Vector3(x, frame.muzzleOffset.y, 0f));

        Vector2 dir = ComputeAimDirection(spawnPos, bt);
        SpawnBullet(spawnPos, dir, bt);
        PlayFireSE(palmFireSE, palmFireSEVolume, spawnPos);
    }

    // EnemyDataのBullet Firing Routines配列（他エネミーと同じ標準の仕組み）でNormal/Wave/Spiralの比率をAsset側から調整できるようにする。
    // Marshalはbullet ROutineAboveThresholdのスロットのみ参照する（HPによる切り替えは行わない）
    private EnemyData.BulletType PickPalmBulletType()
    {
        if (enemyData == null || enemyData.bulletTypes == null || enemyData.bulletTypes.Length == 0) return null;

        EnemyData.BulletFiringRoutine routine = GetPalmFiringRoutine();
        if (routine != null &&
            routine.routineType == EnemyData.BulletFiringRoutine.RoutineType.Probability &&
            routine.probabilityEntries != null && routine.probabilityEntries.Length > 0)
        {
            int idx = PickProbabilityBulletIndex(routine);
            if (idx >= 0 && idx < enemyData.bulletTypes.Length) return enemyData.bulletTypes[idx];
        }

        return GetBulletType(palmBulletTypeIndexFallback);
    }

    private EnemyData.BulletFiringRoutine GetPalmFiringRoutine()
    {
        if (enemyData == null || !enemyData.useHpBasedRoutineSwitch || enemyData.bulletFiringRoutines == null) return null;

        int routineIndex = (int)enemyData.bulletRoutineAboveThreshold;
        if (routineIndex < 0 || routineIndex >= enemyData.bulletFiringRoutines.Length) return null;
        return enemyData.bulletFiringRoutines[routineIndex];
    }

    private int PickProbabilityBulletIndex(EnemyData.BulletFiringRoutine routine)
    {
        float total = 0f;
        foreach (var e in routine.probabilityEntries)
            total += Mathf.Max(0f, e.probabilityPercentage);
        if (total <= 0f) return 0;

        float r = Random.value * total;
        float acc = 0f;
        foreach (var e in routine.probabilityEntries)
        {
            acc += Mathf.Max(0f, e.probabilityPercentage);
            if (r <= acc)
                return Mathf.Clamp(e.bulletTypeIndex, 0, enemyData.bulletTypes.Length - 1);
        }
        var last = routine.probabilityEntries[routine.probabilityEntries.Length - 1];
        return Mathf.Clamp(last.bulletTypeIndex, 0, enemyData.bulletTypes.Length - 1);
    }

    // =========================================================
    // Breath（溜め3枚→ブレスの1枚を保持しつつMuzzle位置からBeamを発射）
    // =========================================================

    // 溜め1〜3を再生する（Single/Double/扇ブレス共通）
    private IEnumerator PlayBreathCharge()
    {
        int chargeCount = Mathf.Min(3, breathFrames != null ? breathFrames.Length : 0);
        for (int i = 0; i < chargeCount; i++)
        {
            if (isDead) break;
            MarshalFrame f = breathFrames[i];
            if (f == null) continue;

            if (f.sprite != null) bodySpriteRenderer.sprite = f.sprite;
            ApplyOffset(bodySpriteRenderer, f.offset);
            ApplyCollider(f);
            ApplyMuzzlePreview(f);

            yield return WaitScaled(FrameDurationOr(f, 0.3f));
        }
    }

    // ブレス（射出）フレームに切り替え、以降のFinishBreathHoldで戻す
    private void ApplyBreathFireFrame()
    {
        MarshalFrame fireFrame = breathFrames[3];
        if (fireFrame == null) return;
        if (fireFrame.sprite != null) bodySpriteRenderer.sprite = fireFrame.sprite;
        ApplyOffset(bodySpriteRenderer, fireFrame.offset);
        ApplyCollider(fireFrame);
        ApplyMuzzlePreview(fireFrame);
    }

    private void FinishBreath()
    {
        if (!isDead && bodySpriteRenderer != null)
        {
            bodySpriteRenderer.transform.localRotation = Quaternion.identity;
        }
        isBreathAttacking = false;
    }

    private IEnumerator SingleBreathRoutine()
    {
        if (bodySpriteRenderer == null) yield break;

        isBreathAttacking = true;
        yield return StartCoroutine(PlayBreathCharge());

        if (!isDead)
        {
            ApplyBreathFireFrame();
            FireBreath(breathFrames[3], 0f);

            // Duration値はSingle/Double/扇ブレスで共通のため使わず、実際のビームのLifetime（フェードアウト込み）が
            // 尽きて破棄されるまでブレススプライトを表示し続ける
            yield return null;
            while (!isDead && activeBreathBeam != null)
            {
                yield return null;
            }
        }

        FinishBreath();
    }

    // ダブルブレス：溜めは1回だけ。1発目と2発目を角度をずらして連続発射する（重なって相殺して見えるのを防ぐ）
    private IEnumerator DoubleBreathRoutine()
    {
        if (bodySpriteRenderer == null) yield break;

        isBreathAttacking = true;
        yield return StartCoroutine(PlayBreathCharge());

        if (!isDead)
        {
            ApplyBreathFireFrame();
            float half = doubleBreathAngleOffsetDeg * 0.5f;

            FireBreath(breathFrames[3], -half);
            yield return WaitScaled(doubleBreathInterval);

            if (!isDead) FireBreath(breathFrames[3], half);

            yield return null;
            while (!isDead && activeBreathBeam != null)
            {
                yield return null;
            }
        }

        FinishBreath();
    }

    // 扇ブレス：溜め後、1本のビームをプレイヤー方向を中心にfanSweepAngleRangeDeg分だけ左右へ旋回させながら撃つ
    private IEnumerator FanSweepBreathRoutine(bool leftToRight)
    {
        if (bodySpriteRenderer == null) yield break;

        isBreathAttacking = true;
        yield return StartCoroutine(PlayBreathCharge());

        if (!isDead)
        {
            ApplyBreathFireFrame();
            MarshalFrame fireFrame = breathFrames[3];

            float half = fanSweepAngleRangeDeg * 0.5f;
            float startAngle = leftToRight ? -half : half;
            float endAngle = leftToRight ? half : -half;

            FireBreath(fireFrame, startAngle);

            if (activeBreathBeam != null)
            {
                // 掃射の中心方向は発射時点のプレイヤー位置で固定する（毎フレーム再照準すると、
                // プレイヤーが動いた分だけ端の角度がズレて掃射範囲が安定しないため）
                float x = bodySpriteRenderer.flipX ? -fireFrame.muzzleOffset.x : fireFrame.muzzleOffset.x;
                Vector3 muzzlePos = bodySpriteRenderer.transform.TransformPoint(new Vector3(x, fireFrame.muzzleOffset.y, 0f));
                EnemyData.BulletType bt = GetBulletType(breathBulletTypeIndex);
                Vector2 fixedBaseDir = ComputeAimDirection(muzzlePos, bt);

                float elapsed = 0f;
                float duration = Mathf.Max(0.01f, fanSweepDuration);
                while (!isDead && activeBreathBeam != null && elapsed < duration)
                {
                    elapsed += Time.deltaTime * GetTimeScale();
                    float t = Mathf.Clamp01(elapsed / duration);
                    float angle = Mathf.Lerp(startAngle, endAngle, t);
                    activeBreathBeam.UpdateOriginDirection(RotateDir(fixedBaseDir, angle));
                    yield return null;
                }
            }

            while (!isDead && activeBreathBeam != null)
            {
                yield return null;
            }
        }

        FinishBreath();
    }

    private void FireBreath(MarshalFrame frame, float angleOffsetDeg)
    {
        if (IsFireBlockedGlobally()) return;
        if (bodySpriteRenderer == null || beamBulletPrefab == null) return;

        EnemyData.BulletType bt = GetBulletType(breathBulletTypeIndex);
        if (bt == null) return;

        float x = bodySpriteRenderer.flipX ? -frame.muzzleOffset.x : frame.muzzleOffset.x;
        Vector3 spawnPos = bodySpriteRenderer.transform.TransformPoint(new Vector3(x, frame.muzzleOffset.y, 0f));

        Vector2 dir = ComputeAimDirection(spawnPos, bt);
        if (Mathf.Abs(angleOffsetDeg) > 0.0001f) dir = RotateDir(dir, angleOffsetDeg);

        Collider2D[] ownerColliders = GetComponentsInChildren<Collider2D>();
        activeBreathBeam = EnemyShooter.SpawnBeamBullet(beamBulletPrefab, spawnPos, dir, bt, ownerColliders, projectileRoot);
        PlayFireSE(breathFireSE, breathFireSEVolume, spawnPos);
    }

    // =========================================================
    // Bite（噛みつき3枚。噛みつき2でMultiShot弾を発射）
    // =========================================================

    private IEnumerator BiteAttackRoutine()
    {
        if (bodySpriteRenderer == null) yield break;

        isBiteAttacking = true;

        for (int i = 0; i < biteFrames.Length; i++)
        {
            if (isDead) break;
            MarshalFrame f = biteFrames[i];
            if (f == null) continue;

            if (f.sprite != null) bodySpriteRenderer.sprite = f.sprite;
            ApplyOffset(bodySpriteRenderer, f.offset);
            ApplyCollider(f);
            ApplyMuzzlePreview(f);

            // 噛みつき2（突進フレーム）で発射する
            if (!isDead && i == 1) FireBite(f);

            yield return WaitScaled(FrameDurationOr(f, 0.15f));
        }

        if (!isDead && bodySpriteRenderer != null)
        {
            bodySpriteRenderer.transform.localRotation = Quaternion.identity;
        }

        isBiteAttacking = false;
    }

    private void FireBite(MarshalFrame frame)
    {
        if (IsFireBlockedGlobally()) return;
        if (bodySpriteRenderer == null || bulletPrefab == null) return;

        EnemyData.BulletType bt = GetBulletType(biteBulletTypeIndex);
        if (bt == null) return;

        float x = bodySpriteRenderer.flipX ? -frame.muzzleOffset.x : frame.muzzleOffset.x;
        Vector3 spawnPos = bodySpriteRenderer.transform.TransformPoint(new Vector3(x, frame.muzzleOffset.y, 0f));
        Vector2 baseDir = ComputeAimDirection(spawnPos, bt);

        int shots = bt.useMultiShot ? Mathf.Max(1, bt.shotsPerFire) : 1;
        float half = bt.spreadAngleDeg * 0.5f;
        float spawnOffset = bt.useMultiShot ? bt.multiShotSpawnOffset : 0f;

        for (int i = 0; i < shots; i++)
        {
            float ang = (half > 0.0001f) ? Random.Range(-half, half) : 0f;
            Vector2 dir = RotateDir(baseDir, ang);

            Vector3 offsetPos = spawnPos;
            if (spawnOffset > 0.0001f && shots > 1)
            {
                Vector2 perpendicular = new Vector2(-dir.y, dir.x);
                float offsetDist = (i - (shots - 1) * 0.5f) * spawnOffset;
                offsetPos += (Vector3)(perpendicular * offsetDist);
            }

            SpawnBullet(offsetPos, dir, bt);
        }

        PlayFireSE(biteFireSE, biteFireSEVolume, spawnPos);
    }

    private static Vector2 RotateDir(Vector2 v, float degrees)
    {
        float rad = degrees * Mathf.Deg2Rad;
        float s = Mathf.Sin(rad);
        float c = Mathf.Cos(rad);
        return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
    }

    // =========================================================
    // Wing（ウィング7枚。振り下ろし中の3フレーム=Wing2a/2b/2cでMuzzle位置をずらしながら
    // 両翼から1発ずつSpiral弾を発射する。ArcGuardのClawTwoHandRoutineと同じ考え方で、
    // フレームごとのMuzzle Offsetをそのまま発射位置に使う（左右は muzzleOffset.x の符号反転で対称に取る）
    // =========================================================

    private IEnumerator WingAttackRoutine()
    {
        if (bodySpriteRenderer == null) yield break;

        isWingAttacking = true;

        for (int i = 0; i < wingFrames.Length; i++)
        {
            if (isDead) break;
            MarshalFrame f = wingFrames[i];
            if (f == null) continue;

            if (f.sprite != null) bodySpriteRenderer.sprite = f.sprite;
            ApplyOffset(bodySpriteRenderer, f.offset);
            ApplyCollider(f);
            ApplyMuzzlePreview(f);

            // ウィング2a/2b/2c（振り下ろし中）でMuzzle位置をずらしながら両翼から1発ずつ発射する
            if (!isDead && (i == 1 || i == 2 || i == 3)) FireWingPair(f);

            yield return WaitScaled(FrameDurationOr(f, 0.15f));
        }

        if (!isDead && bodySpriteRenderer != null)
        {
            bodySpriteRenderer.transform.localRotation = Quaternion.identity;
        }

        isWingAttacking = false;
    }

    private void FireWingPair(MarshalFrame frame)
    {
        if (IsFireBlockedGlobally()) return;
        if (bodySpriteRenderer == null || bulletPrefab == null) return;

        EnemyData.BulletType bt = GetBulletType(wingBulletTypeIndex);
        if (bt == null) return;

        Vector3 rightMuzzlePos = bodySpriteRenderer.transform.TransformPoint(new Vector3(frame.muzzleOffset.x, frame.muzzleOffset.y, 0f));
        Vector3 leftMuzzlePos = bodySpriteRenderer.transform.TransformPoint(new Vector3(-frame.muzzleOffset.x, frame.muzzleOffset.y, 0f));

        Vector2 rightDir = ComputeAimDirection(rightMuzzlePos, bt);
        Vector2 leftDir = ComputeAimDirection(leftMuzzlePos, bt);

        SpawnBullet(rightMuzzlePos, rightDir, bt);
        SpawnBullet(leftMuzzlePos, leftDir, bt);

        PlayFireSE(wingFireSE, wingFireSEVolume, rightMuzzlePos);
    }

    // =========================================================
    // Roar（咆哮5枚。咆哮4（index 3）で全方位リング状に弾を発射する。
    // ArcGuardのRoarRoutine/FireRoarRingと同じ考え方
    // =========================================================

    private IEnumerator RoarAttackRoutine()
    {
        if (bodySpriteRenderer == null) yield break;

        isRoarAttacking = true;

        bool fired = false;
        for (int i = 0; i < roarFrames.Length; i++)
        {
            if (isDead) break;
            MarshalFrame f = roarFrames[i];
            if (f == null) continue;

            if (f.sprite != null) bodySpriteRenderer.sprite = f.sprite;
            ApplyOffset(bodySpriteRenderer, f.offset);
            ApplyCollider(f);
            ApplyMuzzlePreview(f);

            // 咆哮4（index 3）でリングを発射する
            if (!isDead && i == 3 && !fired)
            {
                fired = true;
                StartCoroutine(FireRoarRingRepeat(f));
            }

            yield return WaitScaled(FrameDurationOr(f, 0.2f));
        }

        if (!isDead && bodySpriteRenderer != null)
        {
            bodySpriteRenderer.transform.localRotation = Quaternion.identity;
        }

        isRoarAttacking = false;
    }

    private IEnumerator FireRoarRingRepeat(MarshalFrame frame)
    {
        int repeats = Mathf.Max(1, roarRingRepeatCount);
        for (int r = 0; r < repeats; r++)
        {
            FireRoarRing(frame, r * roarRingRepeatAngleOffsetDeg);
            if (r < repeats - 1)
                yield return WaitScaled(roarRingRepeatInterval);
        }
    }

    private void FireRoarRing(MarshalFrame frame, float angleOffsetDeg)
    {
        if (IsFireBlockedGlobally()) return;
        if (bodySpriteRenderer == null || bulletPrefab == null || roarBulletCount <= 0) return;

        EnemyData.BulletType bt = GetBulletType(roarBulletTypeIndex);
        if (bt == null) return;

        Vector3 origin = bodySpriteRenderer.transform.TransformPoint(new Vector3(frame.muzzleOffset.x, frame.muzzleOffset.y, 0f));

        for (int i = 0; i < roarBulletCount; i++)
        {
            float angle = ((360f / roarBulletCount) * i + angleOffsetDeg) * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            SpawnBullet(origin, dir, bt);
        }

        PlayFireSE(roarFireSE, roarFireSEVolume, origin);
    }

    // =========================================================
    // Attack Pattern（本番の攻撃パターン制御。詳細はフィールド宣言部のコメント参照）
    // =========================================================

    private IEnumerator AttackPatternLoop()
    {
        while (!isDead)
        {
            if (IsFireBlockedGlobally())
            {
                yield return null;
                continue;
            }

            CheckDragonPhaseTransition();

            int interruptCount = (dragonPhase == DragonPhase.Front) ? breathInterruptCountFront : breathInterruptCountBack;
            if (interruptCount > 0 && attacksSinceBreathInterrupt >= interruptCount)
            {
                attacksSinceBreathInterrupt = 0;
                yield return StartCoroutine(PlayInterruptBreath());
            }
            else
            {
                MainAttackKind[] sequence = (dragonPhase == DragonPhase.Front) ? AttackSequenceFront : AttackSequenceBack;
                MainAttackKind kind = sequence[attackSequenceIndex % sequence.Length];
                yield return StartCoroutine(PlayMainAttack(kind));
                attackSequenceIndex++;
                attacksSinceBreathInterrupt++;
            }

            if (!isDead)
            {
                float pause = Random.Range(attackPatternIntervalMin, attackPatternIntervalMax);
                yield return WaitScaled(pause);
            }
        }
    }

    private IEnumerator PlayMainAttack(MainAttackKind kind)
    {
        switch (kind)
        {
            case MainAttackKind.Bite: yield return StartCoroutine(BiteAttackRoutine()); break;
            case MainAttackKind.Wing: yield return StartCoroutine(WingAttackRoutine()); break;
            case MainAttackKind.Roar: yield return StartCoroutine(RoarAttackRoutine()); break;
        }
    }

    // Front: シングルブレス固定。Back: ダブル/扇左/扇右からランダム
    private IEnumerator PlayInterruptBreath()
    {
        if (dragonPhase == DragonPhase.Front)
        {
            yield return StartCoroutine(SingleBreathRoutine());
        }
        else
        {
            int pick = Random.Range(0, 3);
            if (pick == 0) yield return StartCoroutine(DoubleBreathRoutine());
            else if (pick == 1) yield return StartCoroutine(FanSweepBreathRoutine(true));
            else yield return StartCoroutine(FanSweepBreathRoutine(false));
        }
    }

    private void CheckDragonPhaseTransition()
    {
        if (dragonPhaseTransitioned || dragonPhase == DragonPhase.Back) return;
        if (stats == null) return;
        if (stats.GetHpPercentage() <= phaseTransitionHpThreshold)
        {
            dragonPhase = DragonPhase.Back;
            dragonPhaseTransitioned = true;
        }
    }

    // =========================================================
    // 肩のMissile（固定位置。ランダムで交互/同時）
    // =========================================================

    private void TickMissile(float dt)
    {
        if (IsFireBlockedGlobally()) return;
        if (shoulderMuzzleL == null || shoulderMuzzleR == null) return;
        if (bulletPrefab == null || projectileRoot == null) return;

        EnemyData.BulletType bt = GetBulletType(missileBulletTypeIndex);

        UpdateMissileFirePauseCycle(bt);
        if (missileCycleEnabled && !missileCycleIsFiringPhase) return;

        missileFireTimer -= dt;
        if (missileFireTimer > 0f) return;

        missileFireTimer = GetFireInterval(bt, missileFireInterval);
        bool simultaneous = Random.value < missileSimultaneousChance;

        if (simultaneous)
        {
            FireMissile(shoulderMuzzleL, bt);
            FireMissile(shoulderMuzzleR, bt);
        }
        else
        {
            Transform muzzle = Random.value < 0.5f ? shoulderMuzzleL : shoulderMuzzleR;
            FireMissile(muzzle, bt);
        }
    }

    // EnemyShooter.UpdateFirePauseCycleと同じフェーズ管理（撃つ→止まる→撃つ、を交互に繰り返す）
    private void UpdateMissileFirePauseCycle(EnemyData.BulletType bt)
    {
        if (bt == null || !bt.useFirePauseCycle || bt.fireCycleSeconds <= 0f)
        {
            missileCycleEnabled = false;
            return;
        }

        if (!missileCycleEnabled)
        {
            missileCycleEnabled = true;
            missileCycleFireSeconds = Mathf.Max(0.01f, bt.fireCycleSeconds);
            missileCyclePauseSeconds = Mathf.Max(0f, bt.pauseCycleSeconds);
            missileCycleIsFiringPhase = true;
            missileCyclePhaseEndTime = Time.time + missileCycleFireSeconds;
            return;
        }

        float now = Time.time;
        if (now < missileCyclePhaseEndTime) return;

        if (missileCycleIsFiringPhase && missileCyclePauseSeconds > 0f)
        {
            missileCycleIsFiringPhase = false;
            missileCyclePhaseEndTime = now + missileCyclePauseSeconds;
            missileFireTimer = 0f;
            return;
        }

        missileCycleIsFiringPhase = true;
        missileCyclePhaseEndTime = now + missileCycleFireSeconds;
        missileFireTimer = 0f;
    }

    private void FireMissile(Transform muzzle, EnemyData.BulletType bt)
    {
        Vector3 spawnPos = muzzle.position;
        Vector2 dir = ComputeAimDirection(spawnPos, bt);
        SpawnBullet(spawnPos, dir, bt);
        PlayFireSE(missileFireSE, missileFireSEVolume, spawnPos);
    }

    // =========================================================
    // 弾スポーン共通処理
    // =========================================================

    private bool IsFireBlockedGlobally()
    {
        return FloorHealth.IsBrokenGlobal || PixelDancerController.IsPlayerDeadGlobal || PixelDancerController.IsDownGlobal;
    }

    private EnemyData.BulletType GetBulletType(int index)
    {
        if (enemyData == null || enemyData.bulletTypes == null) return null;
        if (index < 0 || index >= enemyData.bulletTypes.Length) return null;
        return enemyData.bulletTypes[index];
    }

    // 弾種のFire Interval Override設定を反映する（Zephyrと同じロジック。Marshalではこれが抜けていた）
    private float GetFireInterval(EnemyData.BulletType bt, float fallbackInterval)
    {
        if (bt == null) return fallbackInterval;

        float interval = fallbackInterval;
        if (bt.useFireIntervalOverride && bt.fireIntervalOverride > 0f)
            interval = bt.fireIntervalOverride;

        if (bt.useFireIntervalRandom)
        {
            float range = Mathf.Max(0f, bt.fireIntervalRandomRangeSeconds);
            interval += (range > 0f) ? Random.Range(-range, range) : 0f;
            interval = Mathf.Max(bt.fireIntervalMinSeconds, interval);
        }

        return Mathf.Max(0.05f, interval);
    }

    // プレイヤー・Floor可動域を含めた照準方向（Zephyr/EnemyShooter.ComputeFinalDirectionと同じロジック）
    private Vector2 ComputeAimDirection(Vector3 spawnPos, EnemyData.BulletType type)
    {
        Vector2 fallback = Vector2.down;
        if (player == null) return fallback;

        if (type == null || type.aimMode == EnemyData.BulletType.AimMode.UseFireDirection
            || type.aimMode == EnemyData.BulletType.AimMode.TowardPlayer)
        {
            Vector2 d = (Vector2)player.transform.position - (Vector2)spawnPos;
            return d.sqrMagnitude > 0.0001f ? d.normalized : fallback;
        }

        float range = player.AutoMoveRange;
        float targetX = player.transform.position.x + Random.Range(-range, range);
        Vector2 target = new Vector2(targetX, player.transform.position.y);
        Vector2 dir = target - (Vector2)spawnPos;
        return dir.sqrMagnitude > 0.0001f ? dir.normalized : fallback;
    }

    private void SpawnBullet(Vector3 spawnPos, Vector2 dir, EnemyData.BulletType bt)
    {
        if (bulletPrefab == null || projectileRoot == null) return;

        if (showDebugLog) Debug.Log($"[MarshalController] SpawnBullet: pos={spawnPos}, dir={dir}, bt={(bt != null ? bt.name : "NULL")}");

        EnemyBullet bullet = Instantiate(bulletPrefab, spawnPos, Quaternion.identity, projectileRoot);
        bullet.SetDirection(dir);

        if (bt != null)
            EnemyShooter.ApplyBulletTypeToEnemyBullet(bullet, bt, fallbackBulletSpeed, fallbackBulletLifeTime, null, bulletPrefab, projectileRoot);
        else
            bullet.ApplyBullet(fallbackBulletSpeed, fallbackBulletLifeTime);

        foreach (Collider2D col in GetComponentsInChildren<Collider2D>())
        {
            if (col != null) bullet.SetOwnerCollisionIgnore(col, ignoreOwnerTime);
        }

        if (enemyData != null && enemyData.unreflectedBulletCollisionDisableTime > 0f)
            bullet.SetUnreflectedCollisionDisable(enemyData.unreflectedBulletCollisionDisableTime);
    }

    private void PlayFireSE(AudioClip clip, float volume, Vector3 pos)
    {
        if (clip == null) return;
        float vol = volume * (SoundSettingsManager.Instance != null ? SoundSettingsManager.Instance.SEVolume : 1f);
        AudioSource.PlayClipAtPoint(clip, pos, vol);
    }

    // =========================================================
    // Editor Preview（ArcGuardControllerと同じ構成）
    // =========================================================

    private static MarshalFrame GetArrayFrame(MarshalFrame[] arr, int index)
    {
        if (arr == null || index < 0 || index >= arr.Length) return null;
        return arr[index];
    }

    private MarshalFrame GetPreviewFrame(MarshalPreviewSprite ps)
    {
        switch (ps)
        {
            case MarshalPreviewSprite.Idle1: return GetArrayFrame(idleFrames, 0);
            case MarshalPreviewSprite.Idle2: return GetArrayFrame(idleFrames, 1);
            case MarshalPreviewSprite.Idle3: return GetArrayFrame(idleFrames, 2);
            case MarshalPreviewSprite.Idle4: return GetArrayFrame(idleFrames, 3);
            case MarshalPreviewSprite.Idle5: return GetArrayFrame(idleFrames, 4);
            case MarshalPreviewSprite.Idle6: return GetArrayFrame(idleFrames, 5);
            case MarshalPreviewSprite.Idle7: return GetArrayFrame(idleFrames, 6);
            case MarshalPreviewSprite.Move1: return GetArrayFrame(moveFrames, 0);
            case MarshalPreviewSprite.Move2: return GetArrayFrame(moveFrames, 1);
            case MarshalPreviewSprite.Move3: return GetArrayFrame(moveFrames, 2);
            case MarshalPreviewSprite.Move4: return GetArrayFrame(moveFrames, 3);
            case MarshalPreviewSprite.AttackWindup: return GetArrayFrame(attackFrames, 0);
            case MarshalPreviewSprite.AttackThrust: return GetArrayFrame(attackFrames, 1);
            case MarshalPreviewSprite.BreathCharge1: return GetArrayFrame(breathFrames, 0);
            case MarshalPreviewSprite.BreathCharge2: return GetArrayFrame(breathFrames, 1);
            case MarshalPreviewSprite.BreathCharge3: return GetArrayFrame(breathFrames, 2);
            case MarshalPreviewSprite.BreathFire: return GetArrayFrame(breathFrames, 3);
            case MarshalPreviewSprite.Wing1: return GetArrayFrame(wingFrames, 0);
            case MarshalPreviewSprite.Wing2a: return GetArrayFrame(wingFrames, 1);
            case MarshalPreviewSprite.Wing2b: return GetArrayFrame(wingFrames, 2);
            case MarshalPreviewSprite.Wing2c: return GetArrayFrame(wingFrames, 3);
            case MarshalPreviewSprite.Wing3: return GetArrayFrame(wingFrames, 4);
            case MarshalPreviewSprite.Wing4a: return GetArrayFrame(wingFrames, 5);
            case MarshalPreviewSprite.Wing4b: return GetArrayFrame(wingFrames, 6);
            case MarshalPreviewSprite.Bite1: return GetArrayFrame(biteFrames, 0);
            case MarshalPreviewSprite.Bite2: return GetArrayFrame(biteFrames, 1);
            case MarshalPreviewSprite.Bite3: return GetArrayFrame(biteFrames, 2);
            case MarshalPreviewSprite.Roar1: return GetArrayFrame(roarFrames, 0);
            case MarshalPreviewSprite.Roar2: return GetArrayFrame(roarFrames, 1);
            case MarshalPreviewSprite.Roar3: return GetArrayFrame(roarFrames, 2);
            case MarshalPreviewSprite.Roar4: return GetArrayFrame(roarFrames, 3);
            case MarshalPreviewSprite.Roar5: return GetArrayFrame(roarFrames, 4);
            case MarshalPreviewSprite.Hit1: return GetArrayFrame(hitFrames, 0);
            default: return null;
        }
    }

#if UNITY_EDITOR
    private void OnEditorTickRefresh()
    {
        if (this == null || Application.isPlaying) return;
        if (_editorAnimRunning) return;
        if (bodySpriteRenderer == null) return;

        var f = GetPreviewFrame(previewSprite);
        if (f == null) return;

        if (f.sprite != null) bodySpriteRenderer.sprite = f.sprite;
        ApplyOffset(bodySpriteRenderer, f.offset);
        ApplyCollider(f);
        ApplyMuzzlePreview(f);
        if (bodyCollider != null) UnityEditor.EditorUtility.SetDirty(bodyCollider);
        UnityEditor.SceneView.RepaintAll();
    }
#endif

    private void OnValidate()
    {
        if (Application.isPlaying) return;
#if UNITY_EDITOR
        if (bodySpriteRenderer == null)
            bodySpriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (bodySpriteRenderer == null) return;

        bool isAnim = previewSprite == MarshalPreviewSprite.IdleAnimate
                   || previewSprite == MarshalPreviewSprite.MoveAnimate
                   || previewSprite == MarshalPreviewSprite.AttackAnimate
                   || previewSprite == MarshalPreviewSprite.BreathAnimate
                   || previewSprite == MarshalPreviewSprite.WingAnimate
                   || previewSprite == MarshalPreviewSprite.BiteAnimate
                   || previewSprite == MarshalPreviewSprite.RoarAnimate
                   || previewSprite == MarshalPreviewSprite.HitAnimate;

        if (isAnim)
        {
            int animType = previewSprite == MarshalPreviewSprite.IdleAnimate ? 0
                         : previewSprite == MarshalPreviewSprite.MoveAnimate ? 1
                         : previewSprite == MarshalPreviewSprite.AttackAnimate ? 2
                         : previewSprite == MarshalPreviewSprite.BreathAnimate ? 3
                         : previewSprite == MarshalPreviewSprite.WingAnimate ? 4
                         : previewSprite == MarshalPreviewSprite.BiteAnimate ? 5
                         : previewSprite == MarshalPreviewSprite.RoarAnimate ? 6
                         : 7;
            if (_editorAnimRunning && _editorAnimType != animType) StopEditorAnim();
            StartEditorAnim(animType);
        }
        else
        {
            StopEditorAnim();
            var f = GetPreviewFrame(previewSprite);
            if (f != null)
            {
                if (f.sprite != null) bodySpriteRenderer.sprite = f.sprite;
                ApplyOffset(bodySpriteRenderer, f.offset);
                ApplyCollider(f);
                ApplyMuzzlePreview(f);
            }
        }

        UnityEditor.SceneView.RepaintAll();
#endif
    }

#if UNITY_EDITOR
    private bool _editorAnimRunning;
    private double _editorAnimLastTime;
    private int _editorAnimFrameIdx;
    private int _editorAnimType; // 0=Idle,1=Move,2=Attack,3=Breath,4=Wing,5=Bite,6=Roar,7=Hit

    private void StartEditorAnim(int animType)
    {
        _editorAnimType = animType;
        _editorAnimFrameIdx = 0;
        _editorAnimLastTime = UnityEditor.EditorApplication.timeSinceStartup;
        if (!_editorAnimRunning)
        {
            _editorAnimRunning = true;
            UnityEditor.EditorApplication.update += OnEditorUpdate;
        }
        var frames = GetEditorAnimFrames();
        if (frames != null && frames.Length > 0 && frames[0] != null && bodySpriteRenderer != null)
        {
            bodySpriteRenderer.sprite = frames[0].sprite;
            ApplyOffset(bodySpriteRenderer, frames[0].offset);
            ApplyCollider(frames[0]);
            ApplyMuzzlePreview(frames[0]);
        }
    }

    private void StopEditorAnim()
    {
        if (!_editorAnimRunning) return;
        _editorAnimRunning = false;
        UnityEditor.EditorApplication.update -= OnEditorUpdate;
    }

    private MarshalFrame[] GetEditorAnimFrames()
    {
        switch (_editorAnimType)
        {
            case 0: return idleFrames;
            case 1: return ActiveMoveFrames;
            case 2: return attackFrames;
            case 3: return breathFrames;
            case 4: return wingFrames;
            case 5: return biteFrames;
            case 6: return roarFrames;
            default: return hitFrames;
        }
    }

    private void OnEditorUpdate()
    {
        if (this == null || !_editorAnimRunning) { StopEditorAnim(); return; }

        var frames = GetEditorAnimFrames();
        if (frames == null || frames.Length == 0) { StopEditorAnim(); return; }

        var current = frames[_editorAnimFrameIdx % frames.Length];
        double dur = FrameDurationOr(current, 0.15f);
        double now = UnityEditor.EditorApplication.timeSinceStartup;
        if (now - _editorAnimLastTime >= dur)
        {
            _editorAnimLastTime = now;
            _editorAnimFrameIdx = (_editorAnimFrameIdx + 1) % frames.Length;
            var next = frames[_editorAnimFrameIdx];
            if (next != null && next.sprite != null && bodySpriteRenderer != null)
            {
                bodySpriteRenderer.sprite = next.sprite;
                ApplyOffset(bodySpriteRenderer, next.offset);
                ApplyCollider(next);
                ApplyMuzzlePreview(next);
            }
        }
    }

#endif

#if UNITY_EDITOR
    private void OnPlayModeStateChanged(UnityEditor.PlayModeStateChange state)
    {
        if (state == UnityEditor.PlayModeStateChange.ExitingEditMode)
        {
            StopEditorAnim();
        }
    }
#endif

    // =========================================================
    // Muzzle Gizmo（Play前のScene viewで発射位置を確認するため）
    // =========================================================

    private void OnDrawGizmosSelected()
    {
        if (shoulderMuzzleL != null)
        {
            Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.9f); // 青=L
            Gizmos.DrawSphere(shoulderMuzzleL.position, 0.08f);
        }
        if (shoulderMuzzleR != null)
        {
            Gizmos.color = new Color(1f, 0.4f, 0.15f, 0.9f); // 橙=R
            Gizmos.DrawSphere(shoulderMuzzleR.position, 0.08f);
        }
        if (palmMuzzlePreview != null)
        {
            Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.9f); // 黄=手のひらAttack
            Gizmos.DrawSphere(palmMuzzlePreview.position, 0.06f);
        }
    }
}
