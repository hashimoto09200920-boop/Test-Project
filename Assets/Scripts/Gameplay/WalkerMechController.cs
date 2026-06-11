using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Area03 Stage2 中ボス「WalkerMech」の移動・攻撃・アニメーション制御。
/// 移動はSpawnPoint間のジャンプ。攻撃はジャンプ中に両砲台から射撃。
/// </summary>
public class WalkerMechController : MonoBehaviour
{
    // =========================================================
    // ポーズ設定
    // =========================================================
    [System.Serializable]
    public class PoseConfig
    {
        public Sprite sprite;
        [Tooltip("BodyのlocalPosition.Y オフセット")]
        public float localY = 0f;
        [Tooltip("左砲台のBodyからのローカル座標")]
        public Vector2 cannonLPos = new Vector2(-0.5f, 0.5f);
        [Tooltip("右砲台のBodyからのローカル座標")]
        public Vector2 cannonRPos = new Vector2(0.5f, 0.5f);
        [Tooltip("シールドエフェクトのBodyからのローカル座標オフセット")]
        public Vector2 shieldOffset = Vector2.zero;
    }

    [Header("Body")]
    [Tooltip("Bodyオブジェクトの Transform")]
    [SerializeField] private Transform bodyTransform;
    [Tooltip("Bodyオブジェクトの SpriteRenderer")]
    [SerializeField] private SpriteRenderer bodyRenderer;

    [Header("Poses")]
    [SerializeField] private PoseConfig poseIdle   = new PoseConfig();
    [SerializeField] private PoseConfig poseCharge = new PoseConfig();
    [SerializeField] private PoseConfig poseJump1  = new PoseConfig();
    [SerializeField] private PoseConfig poseJump2  = new PoseConfig();
    [SerializeField] private PoseConfig poseJump3  = new PoseConfig();

    // =========================================================
    // 砲台
    // =========================================================
    [Header("Cannons")]
    [SerializeField] private Transform cannonL;
    [SerializeField] private Transform cannonR;
    [SerializeField] private Transform muzzleL;
    [SerializeField] private Transform muzzleR;

    [Tooltip("砲台スプライトの銃口方向補正（砲身が下向き = 90）")]
    [SerializeField] private float cannonBarrelAngleOffset = 90f;
    [Tooltip("砲台の回転速度（度/秒）。0 = 即時スナップ")]
    [SerializeField] private float cannonRotSpeed = 180f;

    // =========================================================
    // ジャンプ設定
    // =========================================================
    [Header("Jump Settings")]
    [Tooltip("ジャンプ前の溜め時間（秒）")]
    [SerializeField] private float chargeTime = 0.3f;
    [Tooltip("溜め後・離地前の一瞬 Idle 時間（秒）")]
    [SerializeField] private float idleBeforeJumpTime = 0.1f;
    [Tooltip("ジャンプ移動にかかる時間（秒）")]
    [SerializeField] private float jumpDuration = 1.0f;
    [Tooltip("ジャンプ放物線の最大高さ（ワールド単位）")]
    [SerializeField] private float jumpArcHeight = 3f;
    [Tooltip("着地後の硬直時間（秒）")]
    [SerializeField] private float landingStiffnessTime = 0.4f;
    [Tooltip("着地硬直後のIdle待機時間（秒）。この間Body→次のChargeへ移行")]
    [SerializeField] private float idleAfterLandingTime = 0.3f;

    [Header("Jump Pose Timings (0-1 normalized)")]
    [Tooltip("Jump1 → Jump2 に切り替わる正規化時間（上昇側）")]
    [Range(0f, 0.49f)]
    [SerializeField] private float jump1To2Fraction = 0.2f;
    [Tooltip("Jump2 → Jump3 に切り替わる正規化時間（上昇側）")]
    [Range(0f, 0.49f)]
    [SerializeField] private float jump2To3Fraction = 0.4f;

    // =========================================================
    // 攻撃設定
    // =========================================================
    [Header("Attack")]
    [SerializeField] private EnemyBullet bulletPrefab;
    [SerializeField] private Transform projectileRoot;
    [SerializeField] private float fireInterval = 1.2f;
    [SerializeField] private float bulletSpeed = 6f;
    [SerializeField] private float bulletLifeTime = 5f;
    [Tooltip("自分の Collider を弾に無視させる時間（秒）")]
    [SerializeField] private float ignoreOwnerTime = 0.15f;
    [SerializeField] private AudioClip fireSE;
    [Range(0f, 1f)]
    [SerializeField] private float fireSEVolume = 1f;

    // =========================================================
    // SpawnPoint グループ（EnemySpawnerのspawnPoints配列インデックス）
    // =========================================================
    [Header("Spawn Point Groups (EnemySpawner インデックス)")]
    [Tooltip("Pattern1 左グループ（例: SP04=インデックス3）")]
    [SerializeField] private int[] p1LeftIndices  = { 3 };
    [Tooltip("Pattern1 右グループ（例: SP06=インデックス5）")]
    [SerializeField] private int[] p1RightIndices = { 5 };
    [Tooltip("Pattern2 左グループ（例: SP01=0, SP04=3）")]
    [SerializeField] private int[] p2LeftIndices  = { 0, 3 };
    [Tooltip("Pattern2 右グループ（例: SP03=2, SP06=5）")]
    [SerializeField] private int[] p2RightIndices = { 2, 5 };

    [Tooltip("Pattern2 に切り替わる HP%（この値以下で切り替え）")]
    [Range(1f, 99f)]
    [SerializeField] private float phase2HpPercent = 50f;

    // =========================================================
    // ランタイム
    // =========================================================
    private EnemyStats enemyStats;
    private EnemySpawner enemySpawner;
    private PixelDancerController player;
    private EnemyData enemyData;
    private bool isOnLeftSide = true;
    private bool isJumping = false;
    private float fireTimer = 0f;
    private bool isDead = false;
    private bool isTelegraphing = false;
    private Material cachedLineMat;
    private Transform shieldEffectTransform;
    private Vector2 currentShieldOffset;
    private EnemyMover enemyMover;
    private readonly List<GameObject> activeTelegraphLines = new List<GameObject>();

    // =========================================================
    // Unity ライフサイクル
    // =========================================================
    private void Awake()
    {
        enemyStats   = GetComponent<EnemyStats>();
        enemySpawner = FindObjectOfType<EnemySpawner>();
        player       = FindObjectOfType<PixelDancerController>();
        enemyMover = GetComponent<EnemyMover>();
        if (enemyMover != null) enemyMover.suppressMovement = true;
        if (projectileRoot == null && enemySpawner != null)
            projectileRoot = enemySpawner.ProjectileRoot;

        if (projectileRoot == null)
        {
            GameObject pr = GameObject.Find("ProjectileRoot");
            if (pr != null) projectileRoot = pr.transform;
        }
    }

    private void Start()
    {
        // EnemyShooterはWalkerMechControllerが射撃・砲台回転を全て制御するため無効化
        // (EnemyShooterのrotateTowardPlayerがルート全体を回転させるバグを防ぐ)
        EnemyShooter es = GetComponent<EnemyShooter>();
        if (es != null)
        {
            enemyData = es.GetEnemyData();
            es.enabled = false;
        }

        TryFindShieldEffect();

        SetPose(poseIdle);
        isOnLeftSide = (Random.value < 0.5f);
        StartCoroutine(MainRoutine());
    }

    private void Update()
    {
        if (isDead) return;
        RotateCannonsTowardPlayer();

        fireTimer -= Time.deltaTime * GetTimeScale();
        if (fireTimer <= 0f && !isTelegraphing)
        {
            EnemyData.BulletType btL = PickBulletType();
            EnemyData.BulletType btR = PickBulletType();
            bool telegraphL = btL != null && btL.useTelegraph && btL.telegraphSeconds > 0f;
            bool telegraphR = btR != null && btR.useTelegraph && btR.telegraphSeconds > 0f;
            fireTimer = GetEffectiveFireInterval(btL, btR);

            if (telegraphL || telegraphR)
            {
                isTelegraphing = true;
                StartCoroutine(FireBothWithTelegraph(btL, btR, telegraphL, telegraphR));
            }
            else
            {
                FireFromMuzzle(muzzleL, btL);
                FireFromMuzzle(muzzleR, btR);
            }
        }
    }

    private void LateUpdate()
    {
        if (isDead) return;
        if (shieldEffectTransform == null) TryFindShieldEffect();
        if (shieldEffectTransform == null || bodyTransform == null) return;

        Vector3 p = shieldEffectTransform.position;
        p.x = bodyTransform.position.x + currentShieldOffset.x;
        p.y = bodyTransform.position.y + currentShieldOffset.y;
        shieldEffectTransform.position = p;
    }

    private void OnDestroy()
    {
        isDead = true;
        foreach (var line in activeTelegraphLines)
            if (line != null) Destroy(line);
        activeTelegraphLines.Clear();
    }

    private float GetTimeScale() => SlowMotionManager.Instance != null ? SlowMotionManager.Instance.TimeScale : 1f;
    private float GetSpeedMul()  => enemyMover != null ? enemyMover.SpeedMultiplier : 1f;

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

    // ShieldActiveVfxController コンポーネントでシールドエフェクトを特定
    private void TryFindShieldEffect()
    {
        ShieldActiveVfxController vfx = GetComponentInChildren<ShieldActiveVfxController>(true);
        if (vfx != null)
            shieldEffectTransform = vfx.transform;
    }

    // =========================================================
    // メインルーティン
    // =========================================================
    private IEnumerator MainRoutine()
    {
        while (!isDead && IsAlive())
        {
            Transform nextTarget = PickNextTarget();
            if (nextTarget == null) yield break;

            yield return StartCoroutine(ExecuteJump(nextTarget));
            isOnLeftSide = !isOnLeftSide;
        }
    }

    private bool IsAlive() => enemyStats == null || enemyStats.HP > 0;

    private Transform PickNextTarget()
    {
        bool isPhase2 = enemyStats != null && enemyStats.GetHpPercentage() <= phase2HpPercent;

        int[] indices = isPhase2
            ? (isOnLeftSide ? p2RightIndices : p2LeftIndices)
            : (isOnLeftSide ? p1RightIndices : p1LeftIndices);

        if (indices == null || indices.Length == 0) return null;

        int idx = indices[Random.Range(0, indices.Length)];
        return enemySpawner != null ? enemySpawner.GetSpawnPoint(idx) : null;
    }

    // =========================================================
    // ジャンプ実行
    // =========================================================
    private IEnumerator ExecuteJump(Transform target)
    {
        // 1. 溜めポーズ
        SetPose(poseCharge);
        yield return StartCoroutine(WaitScaled(chargeTime));

        // 2. 離地直前の一瞬 Idle
        SetPose(poseIdle);
        yield return StartCoroutine(WaitScaled(idleBeforeJumpTime));

        // 3. ジャンプアーク移動
        isJumping = true;
        fireTimer = 0f;

        Vector3 fromPos = transform.position;
        Vector3 toPos   = target.position;
        float elapsed   = 0f;

        while (elapsed < jumpDuration)
        {
            if (isDead) yield break;

            elapsed += Time.deltaTime * GetTimeScale() * GetSpeedMul();
            float t = Mathf.Clamp01(elapsed / jumpDuration);

            // 水平補間 + 放物線アーク
            Vector3 pos = Vector3.Lerp(fromPos, toPos, t);
            pos.y += jumpArcHeight * Mathf.Sin(t * Mathf.PI);
            transform.position = pos;

            // ポーズ更新（t=0.5 がピーク・左右対称）
            float tSym = t <= 0.5f ? t * 2f : (1f - t) * 2f;
            UpdateJumpPose(tSym);

            yield return null;
        }

        transform.position = toPos;
        isJumping = false;

        // 4. 着地: Idle → 溜め（着地硬直）→ Idle（可視待機）
        SetPose(poseIdle);
        yield return StartCoroutine(WaitScaled(0.05f));
        SetPose(poseCharge);
        yield return StartCoroutine(WaitScaled(landingStiffnessTime));
        SetPose(poseIdle);
        yield return StartCoroutine(WaitScaled(idleAfterLandingTime));
    }

    private void UpdateJumpPose(float tSym)
    {
        // tSym: 0(離地/着地) → 1(頂点)
        float f1 = jump1To2Fraction * 2f;
        float f2 = jump2To3Fraction * 2f;

        if (tSym < f1)
            SetPose(poseJump1);
        else if (tSym < f2)
            SetPose(poseJump2);
        else
            SetPose(poseJump3);
    }

    // =========================================================
    // ポーズ設定
    // =========================================================
    private void SetPose(PoseConfig pose)
    {
        if (pose == null) return;
        if (bodyRenderer != null && pose.sprite != null)
            bodyRenderer.sprite = pose.sprite;
        if (bodyTransform != null)
        {
            Vector3 lp = bodyTransform.localPosition;
            lp.y = pose.localY;
            bodyTransform.localPosition = lp;
        }
        if (cannonL != null)
        {
            Vector3 lp = cannonL.localPosition;
            lp.x = pose.cannonLPos.x;
            lp.y = pose.cannonLPos.y;
            cannonL.localPosition = lp;
        }
        if (cannonR != null)
        {
            Vector3 rp = cannonR.localPosition;
            rp.x = pose.cannonRPos.x;
            rp.y = pose.cannonRPos.y;
            cannonR.localPosition = rp;
        }
        currentShieldOffset = pose.shieldOffset;
    }

    // =========================================================
    // 弾タイプ選択（BulletFiringRoutine の Probability に対応）
    // =========================================================
    private float GetEffectiveFireInterval(EnemyData.BulletType btL, EnemyData.BulletType btR)
    {
        // useFireIntervalOverride が有効な方を優先（btL → btR → global fireInterval の順）
        EnemyData.BulletType bt = (btL != null && btL.useFireIntervalOverride) ? btL
                                : (btR != null && btR.useFireIntervalOverride) ? btR
                                : null;

        if (bt == null) return fireInterval;

        float interval = bt.fireIntervalOverride;

        if (bt.useFireIntervalRandom)
            interval = Mathf.Max(bt.fireIntervalMinSeconds,
                                 Random.Range(bt.fireIntervalMinSeconds,
                                              bt.fireIntervalOverride + bt.fireIntervalRandomRangeSeconds));

        return Mathf.Max(0.05f, interval);
    }

    private EnemyData.BulletType PickBulletType()
    {
        if (enemyData == null || enemyData.bulletTypes == null || enemyData.bulletTypes.Length == 0)
            return null;

        EnemyData.BulletFiringRoutine routine = GetActiveBulletRoutine();
        if (routine != null &&
            routine.routineType == EnemyData.BulletFiringRoutine.RoutineType.Probability &&
            routine.probabilityEntries != null && routine.probabilityEntries.Length > 0)
        {
            int idx = PickProbabilityBulletIndex(routine);
            if (idx >= 0 && idx < enemyData.bulletTypes.Length)
                return enemyData.bulletTypes[idx];
        }

        return enemyData.bulletTypes[0];
    }

    private EnemyData.BulletFiringRoutine GetActiveBulletRoutine()
    {
        if (enemyData == null || enemyData.bulletFiringRoutines == null) return null;

        bool isLowHp = enemyData.useHpBasedRoutineSwitch &&
                       enemyStats != null &&
                       enemyStats.GetHpPercentage() <= enemyData.hpThresholdPercentage;

        int routineIndex = isLowHp
            ? (int)enemyData.bulletRoutineBelowThreshold
            : (int)enemyData.bulletRoutineAboveThreshold;

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
    // 砲台回転
    // =========================================================
    private void RotateCannonsTowardPlayer()
    {
        if (player == null) return;
        Vector3 playerPos = player.transform.position;
        RotateCannon(cannonL, playerPos);
        RotateCannon(cannonR, playerPos);
    }

    private void RotateCannon(Transform cannon, Vector3 targetPos)
    {
        if (cannon == null) return;
        Vector2 dir = targetPos - cannon.position;
        float targetAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + cannonBarrelAngleOffset;

        if (cannonRotSpeed <= 0f)
        {
            cannon.rotation = Quaternion.Euler(0f, 0f, targetAngle);
        }
        else
        {
            cannon.rotation = Quaternion.RotateTowards(
                cannon.rotation,
                Quaternion.Euler(0f, 0f, targetAngle),
                cannonRotSpeed * Time.deltaTime * GetTimeScale());
        }
    }

    // =========================================================
    // 射撃
    // =========================================================
    private void FireFromMuzzle(Transform muzzle, EnemyData.BulletType bt)
    {
        if (muzzle == null || bulletPrefab == null || projectileRoot == null) return;
        if (FloorHealth.IsBrokenGlobal || PixelDancerController.IsPlayerDeadGlobal) return;

        Vector3 firePos = muzzle.position;
        Vector2 fireDir = player != null
            ? ((Vector2)(player.transform.position - firePos)).normalized
            : Vector2.down;

        SpawnBullet(firePos, fireDir, bt);
    }

    private void SpawnBullet(Vector3 firePos, Vector2 fireDir, EnemyData.BulletType bt)
    {
        if (bt != null && bt.useMultiShot && bt.shotsPerFire > 1)
        {
            int shots       = Mathf.Max(1, bt.shotsPerFire);
            float half      = Mathf.Clamp(bt.spreadAngleDeg, 0f, 180f) * 0.5f;
            float offset    = bt.multiShotSpawnOffset;
            float delay     = bt.multiShotLaunchDelay;

            if (delay > 0.0001f)
                StartCoroutine(SpawnMultiDelayed(firePos, fireDir, shots, half, bt, offset, delay));
            else
                SpawnMulti(firePos, fireDir, shots, half, bt, offset);
        }
        else
        {
            SpawnOne(firePos, fireDir, bt);
        }
    }

    private void SpawnMulti(Vector3 firePos, Vector2 fireDir, int shots, float half, EnemyData.BulletType bt, float spawnOffset)
    {
        for (int i = 0; i < shots; i++)
        {
            float ang    = (half > 0.0001f) ? Random.Range(-half, half) : 0f;
            Vector2 dir  = RotateVec(fireDir, ang);
            if (dir.sqrMagnitude <= 0.0001f) dir = fireDir;

            Vector3 pos = firePos;
            if (spawnOffset > 0.0001f && shots > 1)
            {
                Vector2 perp = new Vector2(-dir.y, dir.x);
                pos += (Vector3)(perp * ((i - (shots - 1) * 0.5f) * spawnOffset));
            }
            SpawnOne(pos, dir, bt);
        }
    }

    private IEnumerator SpawnMultiDelayed(Vector3 firePos, Vector2 fireDir, int shots, float half, EnemyData.BulletType bt, float spawnOffset, float launchDelay)
    {
        for (int i = 0; i < shots; i++)
        {
            float ang    = (half > 0.0001f) ? Random.Range(-half, half) : 0f;
            Vector2 dir  = RotateVec(fireDir, ang);
            if (dir.sqrMagnitude <= 0.0001f) dir = fireDir;

            Vector3 pos = firePos;
            if (spawnOffset > 0.0001f && shots > 1)
            {
                Vector2 perp = new Vector2(-dir.y, dir.x);
                pos += (Vector3)(perp * ((i - (shots - 1) * 0.5f) * spawnOffset));
            }
            SpawnOne(pos, dir, bt);

            if (i < shots - 1)
                yield return StartCoroutine(WaitScaled(launchDelay));
        }
    }

    private void SpawnOne(Vector3 firePos, Vector2 fireDir, EnemyData.BulletType bt)
    {
        EnemyBullet bullet = Instantiate(bulletPrefab, firePos, Quaternion.identity, projectileRoot);
        bullet.SetDirection(fireDir);

        if (bt != null)
            EnemyShooter.ApplyBulletTypeToEnemyBullet(bullet, bt, bulletSpeed, bulletLifeTime, null, bulletPrefab, projectileRoot);
        else
            bullet.ApplyBullet(bulletSpeed, bulletLifeTime);

        foreach (Collider2D col in GetComponentsInChildren<Collider2D>())
        {
            if (col != null)
                bullet.SetOwnerCollisionIgnore(col, ignoreOwnerTime);
        }

        if (fireSE != null)
        {
            float vol = fireSEVolume * (SoundSettingsManager.Instance != null
                ? SoundSettingsManager.Instance.SEVolume : 1f);
            AudioSource.PlayClipAtPoint(fireSE, firePos, vol);
        }
    }

    private static Vector2 RotateVec(Vector2 v, float degrees)
    {
        float rad = degrees * Mathf.Deg2Rad;
        float cos = Mathf.Cos(rad), sin = Mathf.Sin(rad);
        return new Vector2(cos * v.x - sin * v.y, sin * v.x + cos * v.y);
    }

    // =========================================================
    // Telegraph（予兆線）付き射撃
    // =========================================================
    private IEnumerator FireBothWithTelegraph(EnemyData.BulletType btL, EnemyData.BulletType btR, bool telegraphL, bool telegraphR)
    {
        // Telegraph が必要な砲台はコルーチン、不要な砲台は即時発射
        if (!telegraphL) FireFromMuzzle(muzzleL, btL);
        if (!telegraphR) FireFromMuzzle(muzzleR, btR);

        Coroutine cL = telegraphL ? StartCoroutine(TelegraphAndFire(muzzleL, btL)) : null;
        Coroutine cR = telegraphR ? StartCoroutine(TelegraphAndFire(muzzleR, btR)) : null;
        if (cL != null) yield return cL;
        if (cR != null) yield return cR;
        isTelegraphing = false;
    }

    private IEnumerator TelegraphAndFire(Transform muzzle, EnemyData.BulletType bt)
    {
        if (muzzle == null || bt == null) yield break;

        float seconds  = Mathf.Max(0.01f, bt.telegraphSeconds);
        float len      = Mathf.Max(0.1f,  bt.telegraphLength);
        float width    = Mathf.Max(0.001f, bt.telegraphWidth);
        Color baseColor = bt.telegraphColor;
        bool fade      = bt.telegraphFadeOut;
        bool useBlink  = bt.telegraphUseBlink;
        int blinkCount = Mathf.Max(0, bt.telegraphBlinkCount);
        float blinkMin = Mathf.Clamp01(bt.telegraphBlinkMinAlphaMul);

        Vector3 pos = muzzle.position;
        Vector2 dir = player != null
            ? ((Vector2)(player.transform.position - pos)).normalized
            : Vector2.down;

        CreateTelegraphLine(pos, dir, len, width, baseColor, out GameObject lineGo, out LineRenderer lr);
        activeTelegraphLines.Add(lineGo);

        float elapsed = 0f;
        while (true)
        {
            if (elapsed >= seconds) break;

            if (isDead || this == null)
            {
                activeTelegraphLines.Remove(lineGo);
                if (lineGo != null) Destroy(lineGo);
                yield break;
            }

            // 砲台が回転・移動するので毎フレーム追従
            Vector3 curPos = muzzle.position;
            Vector2 curDir = player != null
                ? ((Vector2)(player.transform.position - curPos)).normalized
                : Vector2.down;

            if (lr != null)
            {
                lr.SetPosition(0, curPos);
                lr.SetPosition(1, curPos + (Vector3)(curDir * len));

                float a = baseColor.a;
                if (useBlink && blinkCount > 0)
                {
                    float k = Mathf.Clamp01(elapsed / seconds);
                    int segs = blinkCount * 2;
                    int seg  = Mathf.Min(segs - 1, Mathf.FloorToInt(k * segs));
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

        activeTelegraphLines.Remove(lineGo);
        if (lineGo != null) Destroy(lineGo);
        if (isDead) yield break;

        Vector3 finalPos = muzzle.position;
        Vector2 finalDir = player != null
            ? ((Vector2)(player.transform.position - finalPos)).normalized
            : Vector2.down;
        SpawnBullet(finalPos, finalDir, bt);
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
        lr.startWidth      = width;
        lr.endWidth        = width;
        lr.startColor      = color;
        lr.endColor        = color;
        lr.numCapVertices  = 4;
        lr.numCornerVertices = 2;
        lr.alignment       = LineAlignment.View;
        lr.textureMode     = LineTextureMode.Stretch;
    }
}
