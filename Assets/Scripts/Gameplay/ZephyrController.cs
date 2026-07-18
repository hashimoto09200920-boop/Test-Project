using System.Collections;
using UnityEngine;

/// <summary>
/// Area08雑魚敵「Zephyr」専用コントローラー。
/// Gyrorb方式の画面全体を使った不規則なバウンド移動（Floor回避込み）をベースに、
/// 本体バルカン砲（プレイヤー追尾回転+Rapid弾）と左右浮遊ビット（交互Telegraph弾）を追加した複合武装。
/// EnemyShooterは無効化し、射撃・照準は本コントローラーが独自に行う（WalkerMechControllerと同じ方式）。
/// </summary>
[RequireComponent(typeof(EnemyStats))]
[RequireComponent(typeof(EnemyDamageReceiver))]
public class ZephyrController : MonoBehaviour
{
    // =========================================================
    // References
    // =========================================================

    [Header("References")]
    [Tooltip("本体スプライト（子オブジェクトBody）。未設定なら子から自動取得する")]
    [SerializeField] private SpriteRenderer bodySpriteRenderer;
    [Tooltip("弾種定義（EnemyShooterに設定済みのものを自動取得する。手動設定も可）")]
    [SerializeField] private EnemyData enemyData;

    [Header("Debug")]
    [SerializeField] private bool showDebugLog = false;

    // =========================================================
    // Fade In（他エネミーのStage別フェードインと同じ値を維持すること）
    // =========================================================

    [Header("Fade In")]
    [Tooltip("Stage1/2 のフェードイン時間（秒）。EnemySpawnerのstage12FadeInDurationと合わせる")]
    [SerializeField] private float stage12FadeInDuration = 1f;
    [Tooltip("Stage3 のフェードイン時間（秒）。EnemySpawnerのstage3FadeInDurationと合わせる")]
    [SerializeField] private float stage3FadeInDuration = 3f;

    // =========================================================
    // Movement（滑らかな操舵による曲線移動。本体スプライトは回転させない）
    // =========================================================

    [Header("Movement")]
    [Tooltip("基本移動速度（ワールド単位/秒）")]
    [SerializeField] private float moveSpeed = 2.2f;
    [Tooltip("画面端バウンス判定に使う本体の半径")]
    [SerializeField] private float bodyRadius = 0.5f;

    [Header("Wander（ふわふわ揺らぎ旋回。Perlin Noiseで滑らかにカーブする）")]
    [Tooltip("揺らぎによる最大旋回速度（度/秒）")]
    [SerializeField] private float wanderTurnSpeed = 45f;
    [Tooltip("揺らぎの周波数（大きいほど細かく揺れる）")]
    [SerializeField] private float wanderNoiseFrequency = 0.3f;

    [Header("Sharp Turn（たまに混ざる急旋回）")]
    [Tooltip("急旋回までの間隔（秒・最小）")]
    [SerializeField] private float sharpTurnIntervalMin = 3f;
    [Tooltip("急旋回までの間隔（秒・最大）")]
    [SerializeField] private float sharpTurnIntervalMax = 6f;
    [Tooltip("急旋回時に方向を変える角度の範囲（度。±この範囲でランダム）")]
    [SerializeField] private float sharpTurnAngleRange = 120f;
    [Tooltip("急旋回にかかる時間（秒。通常のWanderより素早く向きを変える）")]
    [SerializeField] private float sharpTurnDuration = 0.25f;

    [Header("Boundary Avoidance（画面端・Floorに近づいたら内側へ操舵する）")]
    [Tooltip("この距離まで近づいたら回避操舵を始める（ワールド単位）")]
    [SerializeField] private float boundaryAvoidMargin = 1.5f;
    [Tooltip("境界回避の旋回速度（度/秒）。Wanderより強めに設定する")]
    [SerializeField] private float boundaryAvoidTurnSpeed = 180f;

    [Header("Speed Loop（遅→速→遅を周期的に繰り返す）")]
    [SerializeField] private float speedLoopDuration = 3f;
    [SerializeField] private AnimationCurve speedLoopCurve = new AnimationCurve(
        new Keyframe(0f,   0f),
        new Keyframe(0.5f, 1f),
        new Keyframe(1f,   0f)
    );
    [SerializeField] private float speedLoopMinMultiplier = 0.4f;
    [SerializeField] private float speedLoopMaxMultiplier = 1.4f;

    [Header("Screen Bounds")]
    [Tooltip("画面端からの安全マージン（ワールド単位）")]
    [SerializeField] private float screenMargin = 0.5f;
    [Tooltip("左側SkillHUDのピクセル幅（この分だけ移動範囲から除外）")]
    [SerializeField] private float skillHudPixelWidth = 280f;

    [Header("Floor Avoidance")]
    [Tooltip("Floorオブジェクトへの参照。未設定の場合はシーンからFloorHealthコンポーネントで自動検索する")]
    [SerializeField] private FloorHealth floorObject;
    [Tooltip("Floorの上端からこの距離以内に近づかないようにする（ワールド単位）")]
    [SerializeField] private float floorAvoidDistance = 1.5f;

    // =========================================================
    // Vulcan Cannon（本体・プレイヤー追尾回転・Rapid弾）
    // =========================================================

    [Header("Vulcan Cannon")]
    [Tooltip("回転する砲身スプライトのTransform")]
    [SerializeField] private Transform cannonTransform;
    [Tooltip("発射位置（cannonTransformの子にして砲身先端に配置する）")]
    [SerializeField] private Transform cannonMuzzle;
    [Tooltip("砲身スプライトの銃口方向補正（WalkerMechと同じ既定90）")]
    [SerializeField] private float cannonBarrelAngleOffset = 90f;
    [Tooltip("砲台の回転速度（度/秒）。0=即時スナップ")]
    [SerializeField] private float cannonRotSpeed = 240f;
    [Tooltip("enemyData.bulletTypesのインデックス（Vulcan/Rapid弾。標準テンプレートの「Rapid」= index 5）")]
    [SerializeField] private int vulcanBulletTypeIndex = 5;
    [Tooltip("弾種で個別指定が無い場合のフォールバック発射間隔（秒）")]
    [SerializeField] private float vulcanFireInterval = 0.15f;

    // =========================================================
    // Bit Turrets（左右浮遊・独立ふわふわ・交互Telegraph弾）
    // =========================================================

    [Header("Bit Turrets")]
    [Tooltip("左ビットのRoot Transform（SpriteRenderer+Collider2D+WallHealthを持つ子オブジェクト）")]
    [SerializeField] private Transform bitL;
    [Tooltip("右ビットのRoot Transform")]
    [SerializeField] private Transform bitR;
    [Tooltip("本体からの左ビット基準オフセット（揺れの中心位置）")]
    [SerializeField] private Vector2 bitLBaseOffset = new Vector2(-1.2f, 0.3f);
    [Tooltip("本体からの右ビット基準オフセット（揺れの中心位置）")]
    [SerializeField] private Vector2 bitRBaseOffset = new Vector2(1.2f, 0.3f);
    [Tooltip("ふわふわ揺れの横幅（ワールド単位）")]
    [SerializeField] private float bitSwayAmplitudeX = 0.12f;
    [Tooltip("ふわふわ揺れの縦幅（ワールド単位）")]
    [SerializeField] private float bitSwayAmplitudeY = 0.18f;
    [Tooltip("ふわふわ揺れの速さ（Hz）")]
    [SerializeField] private float bitSwayFrequency = 0.6f;
    [Tooltip("ビットスプライトの武器ポイント方向補正（下向き=90、Cannonと同じ規則）")]
    [SerializeField] private float bitBarrelAngleOffset = 90f;
    [Tooltip("ビットの回転速度（度/秒）。0=即時スナップ")]
    [SerializeField] private float bitRotSpeed = 240f;
    [Tooltip("ビット本体の発射位置ローカルオフセット（下端の武器ポイント）")]
    [SerializeField] private Vector2 bitMuzzleLocalOffset = new Vector2(0f, -0.3f);
    [Tooltip("enemyData.bulletTypesのインデックス（ビットのTelegraph高速弾。標準テンプレートの「Telegraph」= index 7）")]
    [SerializeField] private int bitBulletTypeIndex = 7;
    [Tooltip("弾種で個別指定が無い場合のフォールバック発射間隔（秒・左右交互で1発ずつ）")]
    [SerializeField] private float bitFireInterval = 2.5f;

    // =========================================================
    // Bullet Spawn 共通設定
    // =========================================================

    [Header("Bullet Spawn")]
    [SerializeField] private EnemyBullet bulletPrefab;
    [SerializeField] private Transform projectileRoot;
    [Tooltip("弾種にspeed指定が無い場合のフォールバック速度")]
    [SerializeField] private float fallbackBulletSpeed = 6f;
    [Tooltip("弾種にlifeTime指定が無い場合のフォールバック寿命（秒）")]
    [SerializeField] private float fallbackBulletLifeTime = 5f;
    [Tooltip("自分のColliderを弾に無視させる時間（秒）")]
    [SerializeField] private float ignoreOwnerTime = 0.15f;

    [Header("Fire SE")]
    [Tooltip("バルカン砲の発射音")]
    [SerializeField] private AudioClip vulcanFireSE;
    [Range(0f, 1f)]
    [SerializeField] private float vulcanFireSEVolume = 1f;
    [Tooltip("ビットの発射音")]
    [SerializeField] private AudioClip bitFireSE;
    [Range(0f, 1f)]
    [SerializeField] private float bitFireSEVolume = 1f;

    // =========================================================
    // インスタンス変数
    // =========================================================

    private EnemyStats stats;
    private EnemyDamageReceiver damageReceiver;
    private EnemyMover enemyMover;
    private PixelDancerController player;

    private WallHealth bitLHealth;
    private WallHealth bitRHealth;
    private bool bitLBroken;
    private bool bitRBroken;
    private bool isDead;

    private float SlowMultiplier => (enemyMover != null) ? enemyMover.SpeedMultiplier : 1f;

    private float headingDeg;
    private float noiseSeed;
    private float speedLoopTimer;

    private float sharpTurnTimer;
    private bool isSharpTurning;
    private float sharpTurnElapsed;
    private float sharpTurnStartHeading;
    private float sharpTurnTargetHeading;

    private float screenXMin, screenXMax, screenYMin, screenYMax;
    private float floorAvoidY = float.MinValue;

    private float vulcanFireTimer;
    private float bitFireTimer;
    private bool bitNextIsLeft = true;
    private bool isBitTelegraphing;
    private float swayPhaseL;
    private float swayPhaseR;

    // Fire/Pause Cycle（BulletType.useFirePauseCycle対応。EnemyShooterと同じ考え方）
    private bool vulcanCycleInitialized;
    private bool vulcanCycleOn;
    private bool vulcanCycleIsFiringPhase = true;
    private float vulcanCyclePhaseEndTime;

    private bool bitCycleInitialized;
    private bool bitCycleOn;
    private bool bitCycleIsFiringPhase = true;
    private float bitCyclePhaseEndTime;

    private Material cachedLineMat;
    private Coroutine fadeInCoroutine;
    private GameObject activeBitTelegraphLine;

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

        if (bitL != null) bitLHealth = bitL.GetComponent<WallHealth>();
        if (bitR != null) bitRHealth = bitR.GetComponent<WallHealth>();
        if (bitLHealth != null) bitLHealth.OnBroken += _ => bitLBroken = true;
        if (bitRHealth != null) bitRHealth.OnBroken += _ => bitRBroken = true;

        if (stats != null) stats.onKilled += HandleKilled;

        if (projectileRoot == null)
        {
            GameObject pr = GameObject.Find("ProjectileRoot");
            if (pr != null) projectileRoot = pr.transform;
        }
    }

    private void Start()
    {
        // EnemyShooterはZephyrControllerが射撃・砲台回転を全て制御するため無効化
        EnemyShooter es = GetComponent<EnemyShooter>();
        if (es != null)
        {
            if (enemyData == null) enemyData = es.GetEnemyData();
            if (bulletPrefab == null) bulletPrefab = es.GetBulletPrefab();
            if (projectileRoot == null) projectileRoot = es.GetProjectileRoot();
            es.enabled = false;
        }

        player = FindObjectOfType<PixelDancerController>();

        if (showDebugLog)
        {
            Debug.Log($"[ZephyrController] Start: enemyData={(enemyData != null ? enemyData.name : "NULL")}, " +
                $"bulletTypesCount={(enemyData != null && enemyData.bulletTypes != null ? enemyData.bulletTypes.Length : -1)}, " +
                $"bulletPrefab={(bulletPrefab != null ? bulletPrefab.name : "NULL")}, " +
                $"projectileRoot={(projectileRoot != null ? projectileRoot.name : "NULL")}, " +
                $"cannonMuzzle={(cannonMuzzle != null ? cannonMuzzle.name : "NULL")}, " +
                $"player={(player != null ? player.name : "NULL")}");
        }
    }

    private void OnEnable()
    {
        CacheFloorY();
        RefreshScreenBounds();

        headingDeg = Random.Range(0f, 360f);
        noiseSeed = Random.Range(0f, 1000f);
        speedLoopTimer = 0f;

        sharpTurnTimer = Random.Range(sharpTurnIntervalMin, sharpTurnIntervalMax);
        isSharpTurning = false;

        vulcanFireTimer = Random.Range(0f, vulcanFireInterval);
        bitFireTimer = Random.Range(0f, bitFireInterval);
        swayPhaseL = Random.Range(0f, Mathf.PI * 2f);
        swayPhaseR = Random.Range(0f, Mathf.PI * 2f);

        isDead = false;
        bitLBroken = false;
        bitRBroken = false;
        isBitTelegraphing = false;

        vulcanCycleInitialized = false;
        bitCycleInitialized = false;

        if (fadeInCoroutine != null) StopCoroutine(fadeInCoroutine);
        fadeInCoroutine = StartCoroutine(FadeInBody());
    }

    private void OnDestroy()
    {
        isDead = true;
        if (stats != null) stats.onKilled -= HandleKilled;

        // Telegraph中に本体が破棄されるとコルーチンが再開できず予兆線が消し忘れられるため、
        // OnDestroyで確実に消す（EnemyShooter.OnDestroyと同じ考え方）
        if (activeBitTelegraphLine != null)
        {
            Destroy(activeBitTelegraphLine);
            activeBitTelegraphLine = null;
        }
    }

    private void HandleKilled()
    {
        isDead = true;
        if (bitL != null) Destroy(bitL.gameObject);
        if (bitR != null) Destroy(bitR.gameObject);
    }

    private float GetTimeScale() =>
        SlowMotionManager.Instance != null ? SlowMotionManager.Instance.TimeScale : 1f;

    private void Update()
    {
        if (isDead) return;

        float dt = Time.deltaTime * GetTimeScale() * SlowMultiplier;

        ApplyMove(dt);
        UpdateBitSway();
        RotateCannonTowardPlayer();
        RotateBitsTowardPlayer();
        TickVulcan(dt);
        TickBit(dt);
    }

    // =========================================================
    // フェードイン（Gyrorbと同じ方式）
    // =========================================================

    private IEnumerator FadeInBody()
    {
        yield return null;

        int stageIndex = stats?.GetSpawner()?.GetCurrentStageIndex() ?? 0;
        float fadeInDuration = (stageIndex == 2) ? stage3FadeInDuration : stage12FadeInDuration;

        if (bodySpriteRenderer == null || fadeInDuration <= 0f) yield break;

        Color original = bodySpriteRenderer.color;
        bodySpriteRenderer.color = new Color(original.r, original.g, original.b, 0f);

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
    // 移動（Perlin Noiseによる滑らかな操舵カーブ + たまに混ざる急旋回。本体スプライトの回転は行わない）
    // =========================================================

    private void ApplyMove(float dt)
    {
        speedLoopTimer += dt;
        float loopT = (speedLoopDuration > 0.0001f)
            ? Mathf.Repeat(speedLoopTimer / speedLoopDuration, 1f)
            : 0f;
        float loopWeight = speedLoopCurve.Evaluate(loopT);
        float speedFactor = Mathf.Lerp(speedLoopMinMultiplier, speedLoopMaxMultiplier, loopWeight);

        UpdateHeading(dt);

        // 画面端・Floorに近づいたら内側へ滑らかに操舵する
        Vector2 avoidSteer = ComputeBoundaryAvoidSteer();
        if (avoidSteer.sqrMagnitude > 0.0001f)
        {
            float avoidTargetAngle = Mathf.Atan2(avoidSteer.y, avoidSteer.x) * Mathf.Rad2Deg;
            headingDeg = Mathf.MoveTowardsAngle(headingDeg, avoidTargetAngle, boundaryAvoidTurnSpeed * dt);
        }

        Vector2 moveDir = new Vector2(Mathf.Cos(headingDeg * Mathf.Deg2Rad), Mathf.Sin(headingDeg * Mathf.Deg2Rad));
        Vector3 pos = transform.position + (Vector3)(moveDir * moveSpeed * speedFactor * dt);

        // 最終セーフティ：操舵が間に合わず境界を超えそうな場合のみクランプする
        float yMin = (floorAvoidY > float.MinValue) ? Mathf.Max(screenYMin, floorAvoidY) : screenYMin;
        pos.x = Mathf.Clamp(pos.x, screenXMin, screenXMax);
        pos.y = Mathf.Clamp(pos.y, yMin, screenYMax);

        transform.position = pos;
    }

    // 進行方向の更新：通常はPerlin Noiseで滑らかに揺らぎ、一定間隔でたまに急旋回を混ぜる
    private void UpdateHeading(float dt)
    {
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

    // 画面端・Floorに近い分だけ内側方向への操舵ベクトルを返す（近いほど強く働く）
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

    // =========================================================
    // ビットのふわふわ揺れ（本体からの固定オフセット + 独立サイン波）
    // =========================================================

    private void UpdateBitSway()
    {
        float t = Time.time;

        if (bitL != null)
        {
            float sx = Mathf.Sin(t * bitSwayFrequency * Mathf.PI * 2f + swayPhaseL) * bitSwayAmplitudeX;
            float sy = Mathf.Cos(t * bitSwayFrequency * Mathf.PI * 2f * 0.7f + swayPhaseL) * bitSwayAmplitudeY;
            bitL.position = transform.position + (Vector3)(bitLBaseOffset + new Vector2(sx, sy));
        }

        if (bitR != null)
        {
            float sx = Mathf.Sin(t * bitSwayFrequency * Mathf.PI * 2f + swayPhaseR) * bitSwayAmplitudeX;
            float sy = Mathf.Cos(t * bitSwayFrequency * Mathf.PI * 2f * 0.7f + swayPhaseR) * bitSwayAmplitudeY;
            bitR.position = transform.position + (Vector3)(bitRBaseOffset + new Vector2(sx, sy));
        }
    }

    // =========================================================
    // 砲台回転（WalkerMech方式）
    // =========================================================

    private void RotateCannonTowardPlayer()
    {
        if (cannonTransform == null || player == null) return;

        Vector2 dir = (Vector2)player.transform.position - (Vector2)cannonTransform.position;
        float targetAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + cannonBarrelAngleOffset;

        if (cannonRotSpeed <= 0f)
        {
            cannonTransform.rotation = Quaternion.Euler(0f, 0f, targetAngle);
        }
        else
        {
            cannonTransform.rotation = Quaternion.RotateTowards(
                cannonTransform.rotation,
                Quaternion.Euler(0f, 0f, targetAngle),
                cannonRotSpeed * Time.deltaTime * GetTimeScale());
        }
    }

    // =========================================================
    // ビット回転（Cannonと同じくプレイヤー追尾。武器ポイントを狙い方向へ向ける）
    // =========================================================

    private void RotateBitsTowardPlayer()
    {
        RotateBitTowardPlayer(bitL);
        RotateBitTowardPlayer(bitR);
    }

    private void RotateBitTowardPlayer(Transform bit)
    {
        if (bit == null || player == null) return;

        Vector2 dir = (Vector2)player.transform.position - (Vector2)bit.position;
        float targetAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + bitBarrelAngleOffset;

        if (bitRotSpeed <= 0f)
        {
            bit.rotation = Quaternion.Euler(0f, 0f, targetAngle);
        }
        else
        {
            bit.rotation = Quaternion.RotateTowards(
                bit.rotation,
                Quaternion.Euler(0f, 0f, targetAngle),
                bitRotSpeed * Time.deltaTime * GetTimeScale());
        }
    }

    // =========================================================
    // Vulcan Cannon 射撃（Rapid弾・Telegraphなし）
    // =========================================================

    private void TickVulcan(float dt)
    {
        if (IsFireBlockedGlobally())
        {
            if (showDebugLog) Debug.Log("[ZephyrController] TickVulcan blocked: IsFireBlockedGlobally=true");
            return;
        }
        if (cannonMuzzle == null || bulletPrefab == null || projectileRoot == null)
        {
            if (showDebugLog) Debug.Log($"[ZephyrController] TickVulcan blocked: cannonMuzzle={(cannonMuzzle == null ? "NULL" : "OK")}, bulletPrefab={(bulletPrefab == null ? "NULL" : "OK")}, projectileRoot={(projectileRoot == null ? "NULL" : "OK")}");
            return;
        }

        EnemyData.BulletType bt = GetBulletType(vulcanBulletTypeIndex);

        if (!UpdateFireCycle(bt, ref vulcanCycleInitialized, ref vulcanCycleOn, ref vulcanCycleIsFiringPhase, ref vulcanCyclePhaseEndTime))
        {
            vulcanFireTimer = 0f;
            return;
        }

        vulcanFireTimer -= dt;
        if (vulcanFireTimer > 0f) return;

        vulcanFireTimer = GetFireInterval(bt, vulcanFireInterval);

        if (showDebugLog) Debug.Log($"[ZephyrController] TickVulcan firing: bt={(bt != null ? bt.name : "NULL")}");

        Vector3 spawnPos = cannonMuzzle.position;
        Vector2 dir = ComputeAimDirection(spawnPos, bt);
        SpawnBullet(spawnPos, dir, bt);
        PlayFireSE(vulcanFireSE, vulcanFireSEVolume, spawnPos);
    }

    // =========================================================
    // Bit Turret 射撃（交互・Telegraph高速弾）
    // =========================================================

    private void TickBit(float dt)
    {
        if (IsFireBlockedGlobally())
        {
            if (showDebugLog) Debug.Log("[ZephyrController] TickBit blocked: IsFireBlockedGlobally=true");
            return;
        }
        if (isBitTelegraphing) return;
        if (bitLBroken && bitRBroken) return;
        if (bulletPrefab == null || projectileRoot == null)
        {
            if (showDebugLog) Debug.Log($"[ZephyrController] TickBit blocked: bulletPrefab={(bulletPrefab == null ? "NULL" : "OK")}, projectileRoot={(projectileRoot == null ? "NULL" : "OK")}");
            return;
        }

        EnemyData.BulletType bt = GetBulletType(bitBulletTypeIndex);

        if (!UpdateFireCycle(bt, ref bitCycleInitialized, ref bitCycleOn, ref bitCycleIsFiringPhase, ref bitCyclePhaseEndTime))
        {
            bitFireTimer = 0f;
            return;
        }

        bitFireTimer -= dt;
        if (bitFireTimer > 0f) return;

        bitFireTimer = GetFireInterval(bt, bitFireInterval);

        if (showDebugLog) Debug.Log($"[ZephyrController] TickBit firing: bt={(bt != null ? bt.name : "NULL")}");

        bool fireLeft = bitNextIsLeft;
        if (fireLeft && bitLBroken) fireLeft = false;
        else if (!fireLeft && bitRBroken) fireLeft = true;
        bitNextIsLeft = !bitNextIsLeft;

        Transform bitTransform = fireLeft ? bitL : bitR;
        if (bitTransform == null) return;

        if (bt != null && bt.useTelegraph && bt.telegraphSeconds > 0f)
        {
            isBitTelegraphing = true;
            StartCoroutine(TelegraphAndFireBit(bitTransform, bt));
        }
        else
        {
            Vector3 spawnPos = bitTransform.TransformPoint(bitMuzzleLocalOffset);
            Vector2 dir = ComputeAimDirection(spawnPos, bt);
            SpawnBullet(spawnPos, dir, bt);
            PlayFireSE(bitFireSE, bitFireSEVolume, spawnPos);
        }
    }

    private IEnumerator TelegraphAndFireBit(Transform bitTransform, EnemyData.BulletType bt)
    {
        float seconds   = Mathf.Max(0.01f, bt.telegraphSeconds);
        float len       = Mathf.Max(0.1f,  bt.telegraphLength);
        float width     = Mathf.Max(0.001f, bt.telegraphWidth);
        Color baseColor = bt.telegraphColor;
        bool fade       = bt.telegraphFadeOut;
        bool useBlink   = bt.telegraphUseBlink;
        int blinkCount  = Mathf.Max(0, bt.telegraphBlinkCount);
        float blinkMin  = Mathf.Clamp01(bt.telegraphBlinkMinAlphaMul);

        Vector3 pos = bitTransform.TransformPoint(bitMuzzleLocalOffset);
        // 狙い方向はTelegraph開始時に1回だけ決定する（毎フレーム再抽選すると予兆線と着弾方向がズレるため）
        Vector2 dir = ComputeAimDirection(pos, bt);

        CreateTelegraphLine(pos, dir, len, width, baseColor, out GameObject lineGo, out LineRenderer lr);
        activeBitTelegraphLine = lineGo;

        float elapsed = 0f;
        while (elapsed < seconds)
        {
            if (isDead || this == null)
            {
                if (lineGo != null) Destroy(lineGo);
                activeBitTelegraphLine = null;
                isBitTelegraphing = false;
                yield break;
            }

            Vector3 curPos = bitTransform.TransformPoint(bitMuzzleLocalOffset);

            if (lr != null)
            {
                lr.SetPosition(0, curPos);
                lr.SetPosition(1, curPos + (Vector3)(dir * len));

                float a = baseColor.a;
                if (useBlink && blinkCount > 0)
                {
                    float k = Mathf.Clamp01(elapsed / seconds);
                    int segs = blinkCount * 2;
                    int seg = Mathf.Min(segs - 1, Mathf.FloorToInt(k * segs));
                    a = baseColor.a * ((seg % 2 == 1) ? blinkMin : 1f);
                }
                else if (fade)
                {
                    a = Mathf.Lerp(baseColor.a, 0f, Mathf.Clamp01(elapsed / seconds));
                }
                lr.startColor = new Color(baseColor.r, baseColor.g, baseColor.b, a);
                lr.endColor   = lr.startColor;
            }

            yield return null;
            elapsed += Time.deltaTime * GetTimeScale();
        }

        if (lineGo != null) Destroy(lineGo);
        activeBitTelegraphLine = null;
        isBitTelegraphing = false;

        if (isDead) yield break;
        if (IsFireBlockedGlobally()) yield break;

        // 予兆線に表示していたdirをそのまま撃つ（着弾方向を一致させる）
        Vector3 finalPos = bitTransform.TransformPoint(bitMuzzleLocalOffset);
        SpawnBullet(finalPos, dir, bt);
        PlayFireSE(bitFireSE, bitFireSEVolume, finalPos);
    }

    private void CreateTelegraphLine(Vector3 spawnPos, Vector2 dir, float length, float width, Color color,
        out GameObject lineGo, out LineRenderer lr)
    {
        lineGo = new GameObject("TelegraphLine");
        if (projectileRoot != null) lineGo.transform.SetParent(projectileRoot, false);
        lineGo.transform.position = spawnPos;

        lr = lineGo.AddComponent<LineRenderer>();

        if (cachedLineMat == null)
        {
            Shader sh = Shader.Find("Sprites/Default");
            if (sh != null) cachedLineMat = new Material(sh);
        }
        if (cachedLineMat != null) lr.material = cachedLineMat;

        lr.positionCount = 2;
        lr.useWorldSpace = true;
        lr.SetPosition(0, spawnPos);
        lr.SetPosition(1, spawnPos + (Vector3)(dir.normalized * length));
        lr.startWidth = width;
        lr.endWidth = width;
        lr.startColor = color;
        lr.endColor = color;
        lr.numCapVertices = 4;
        lr.numCornerVertices = 2;
        lr.alignment = LineAlignment.View;
        lr.textureMode = LineTextureMode.Stretch;
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

    // Fire/Pause Cycle（BulletType.useFirePauseCycle対応。EnemyShooter.UpdateFirePauseCycleと同じ考え方）
    // 戻り値: true=発射フェーズ中（発射してよい） / false=休止フェーズ中（発射しない）
    private bool UpdateFireCycle(EnemyData.BulletType bt, ref bool initialized, ref bool cycleOn, ref bool isFiringPhase, ref float phaseEndTime)
    {
        if (!initialized)
        {
            initialized = true;
            if (bt != null && bt.useFirePauseCycle && bt.fireCycleSeconds > 0f)
            {
                cycleOn = true;
                isFiringPhase = true;
                phaseEndTime = Time.time + Mathf.Max(0.01f, bt.fireCycleSeconds);
            }
            else
            {
                cycleOn = false;
            }
        }

        if (!cycleOn) return true;

        float now = Time.time;
        if (now >= phaseEndTime)
        {
            if (isFiringPhase && bt.pauseCycleSeconds > 0f)
            {
                isFiringPhase = false;
                phaseEndTime = now + bt.pauseCycleSeconds;
            }
            else
            {
                isFiringPhase = true;
                phaseEndTime = now + Mathf.Max(0.01f, bt.fireCycleSeconds);
            }
        }

        return isFiringPhase;
    }

    // プレイヤー・Floor可動域を含めた照準方向（EnemyShooter.ComputeFinalDirectionと同じロジック）
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

        // TowardRandomPointInPlayerRange：プレイヤーの可動域内（Floorも含む）のランダムな点を狙う
        float range = player.AutoMoveRange;
        float targetX = player.transform.position.x + Random.Range(-range, range);
        Vector2 target = new Vector2(targetX, player.transform.position.y);
        Vector2 dir = target - (Vector2)spawnPos;
        return dir.sqrMagnitude > 0.0001f ? dir.normalized : fallback;
    }

    private void SpawnBullet(Vector3 spawnPos, Vector2 dir, EnemyData.BulletType bt)
    {
        if (bulletPrefab == null || projectileRoot == null) return;

        if (showDebugLog) Debug.Log($"[ZephyrController] SpawnBullet: pos={spawnPos}, dir={dir}, bt={(bt != null ? bt.name : "NULL")}");

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
    // Editor Gizmo（Play前のSceneビューで発射位置を確認できるようにする）
    // =========================================================
#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (cannonMuzzle != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(cannonMuzzle.position, 0.15f);
            UnityEditor.Handles.color = Color.yellow;
            UnityEditor.Handles.Label(cannonMuzzle.position + Vector3.up * 0.2f, "Vulcan Muzzle");
        }

        if (bitL != null)
        {
            Vector3 posL = bitL.TransformPoint(bitMuzzleLocalOffset);
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(posL, 0.12f);
            UnityEditor.Handles.color = Color.cyan;
            UnityEditor.Handles.Label(posL + Vector3.up * 0.2f, "Bit L Muzzle");
        }

        if (bitR != null)
        {
            Vector3 posR = bitR.TransformPoint(bitMuzzleLocalOffset);
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(posR, 0.12f);
            UnityEditor.Handles.color = Color.cyan;
            UnityEditor.Handles.Label(posR + Vector3.up * 0.2f, "Bit R Muzzle");
        }
    }
#endif
}
