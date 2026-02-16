using UnityEngine;
using System.Collections;

public class EnemyShooter : MonoBehaviour
{
    [Header("Bullet")]
    [SerializeField] private EnemyBullet bulletPrefab;
    [SerializeField] private Transform projectileRoot;

    [Header("Fire Settings")]
    [SerializeField] private float fireInterval = 1.5f;
    [SerializeField] private Vector2 fireDirection = Vector2.down;

    [Header("Bullet Params")]
    [SerializeField] private float bulletSpeed = 6f;
    [SerializeField] private float bulletLifeTime = 5f;

    [Header("Fire FX (Optional)")]
    [SerializeField] private AudioClip fireSE;
    [Range(0f, 1f)]
    [SerializeField] private float fireSEVolume = 1f;
    [SerializeField] private GameObject fireVfxPrefab;
    [SerializeField] private float vfxFallbackDestroySeconds = 2f;

    [Header("Bullet Visual Override (Optional)")]
    [SerializeField] private Sprite bulletSpriteOverride;

    [Header("Spawn Fix")]
    [SerializeField] private float muzzleOffset = 0.6f;
    [SerializeField] private float ignoreOwnerTime = 0.15f;

    private float timer;

    public void SetProjectileRoot(Transform root) => projectileRoot = root;
    public void SetBulletPrefab(EnemyBullet prefab) => bulletPrefab = prefab;

    public void ApplyShoot(float interval, Vector2 direction, float bSpeed, float bLifeTime)
    {
        fireInterval = interval;
        fireDirection = direction;
        bulletSpeed = bSpeed;
        bulletLifeTime = bLifeTime;
    }

    public void ApplyFireFx(AudioClip se, float seVolume, GameObject vfxPrefab, Sprite bulletSprite)
    {
        fireSE = se;
        fireSEVolume = Mathf.Clamp01(seVolume);
        fireVfxPrefab = vfxPrefab;
        bulletSpriteOverride = bulletSprite;
    }

    // =========================================================
    // EnemyData（弾タイプ）注入
    // =========================================================
    [Header("Bullet Types (Injected from EnemyData)")]
    [SerializeField] private EnemyData enemyData;

    public void SetEnemyData(EnemyData data)
    {
        enemyData = data;
        SyncWeightedChancesToTypes();

        currentTypeIndex = -1;
        rrIndex = -1;
        sequenceTypeIndex = 0;
        sequenceRemainingShots = 0;

        ResetFirePauseCycle();

        nextShotWaitSeconds = -1f;
        timer = 0f;
        isTelegraphing = false;

        // EnemyStats参照取得（初回のみ）
        if (enemyStats == null)
        {
            enemyStats = GetComponent<EnemyStats>();
        }

        // HP-Based Routine Switchingの初期化
        InitializeHpBasedBulletRoutine();
    }

    public EnemyData GetEnemyData()
    {
        return enemyData;
    }

    public enum BulletSelectMode
    {
        Random,
        RoundRobin,
        WeightedChance,
        Sequence,      // 順番発射（EnemyData.firingRoutine使用）
        Probability    // 割合発射（EnemyData.firingRoutine使用）
    }

    [Header("Bullet Select Mode")]
    [SerializeField] private BulletSelectMode selectMode = BulletSelectMode.Random;

    [Header("Weighted Chance (only when mode=WeightedChance)")]
    [Tooltip("enemyData.bulletTypes と同じ要素数に自動調整される。値は相対比。0以下は選ばれない。")]
    [SerializeField] private float[] weightedChances;

    private int rrIndex = -1;

    // =========================================================
    // Sequence Firing Routine (順番発射)
    // =========================================================
    private int sequenceTypeIndex = 0;  // 現在の順番発射タイプインデックス
    private int sequenceRemainingShots = 0;  // 現在のタイプの残り発射回数

    // =========================================================
    // HP-Based Routine Switching
    // =========================================================
    private EnemyStats enemyStats;
    private bool hasSwitchedToLowHpBulletRoutine = false;  // 一度切り替わったら戻らない
    private EnemyData.BulletFiringRoutine currentBulletFiringRoutine;

    // =========================================================
    // Fire/Pause Cycle
    // =========================================================
    private bool cycleEnabled = false;
    private bool cycleIsFiringPhase = true; // true=撃つ / false=撃たない
    private float cycleFireSeconds = 0f;
    private float cyclePauseSeconds = 0f;
    private float cyclePhaseEndTime = -999f;

    private int currentTypeIndex = -1;
    private float nextShotWaitSeconds = -1f;

    // Telegraph中ロック
    private bool isTelegraphing = false;

    // LineRenderer用マテリアル（キャッシュ）
    private static Material cachedLineMat;

    private void Update()
    {
        if (bulletPrefab == null || projectileRoot == null) return;

        // FloorHealth破壊中 or プレイヤー死亡中は新規発射しない（すべての処理をスキップ）
        if (FloorHealth.IsBrokenGlobal || PixelDancerController.IsPlayerDeadGlobal) return;

        // HP-Based Routine Switchingのチェック
        CheckHpAndSwitchBulletRoutine();

        EnsureTypeSelectedIfAny();
        UpdateFirePauseCycle();

        if (cycleEnabled && !cycleIsFiringPhase)
        {
            timer = 0f;
            return;
        }

        if (isTelegraphing)
        {
            timer = 0f;
            return;
        }

        if (nextShotWaitSeconds <= 0f) nextShotWaitSeconds = ComputeNextShotWaitSeconds();

        timer += Time.deltaTime;
        if (timer < nextShotWaitSeconds) return;

        timer = 0f;
        Fire();
        nextShotWaitSeconds = ComputeNextShotWaitSeconds();
    }

    private bool HasBulletTypes()
    {
        return enemyData != null && enemyData.bulletTypes != null && enemyData.bulletTypes.Length > 0;
    }

    private void EnsureTypeSelectedIfAny()
    {
        if (!HasBulletTypes()) return;
        if (currentTypeIndex >= 0 && currentTypeIndex < enemyData.bulletTypes.Length) return;

        ApplyTypeSettingsIfChanged(PickBulletTypeIndex(enemyData.bulletTypes.Length));
    }

    private void UpdateFirePauseCycle()
    {
        if (!cycleEnabled) return;

        float now = Time.time;
        if (now < cyclePhaseEndTime) return;

        if (cycleIsFiringPhase && cyclePauseSeconds > 0f)
        {
            cycleIsFiringPhase = false;
            cyclePhaseEndTime = now + cyclePauseSeconds;

            timer = 0f;
            nextShotWaitSeconds = -1f;
            return;
        }

        cycleIsFiringPhase = true;
        cyclePhaseEndTime = now + Mathf.Max(0.01f, cycleFireSeconds);

        timer = 0f;
        nextShotWaitSeconds = -1f;
    }

    private void ResetFirePauseCycle()
    {
        cycleEnabled = false;
        cycleIsFiringPhase = true;
        cycleFireSeconds = 0f;
        cyclePauseSeconds = 0f;
        cyclePhaseEndTime = -999f;
    }

    private void ApplyTypeSettingsIfChanged(int nextIndex)
    {
        if (!HasBulletTypes()) return;
        if (nextIndex < 0 || nextIndex >= enemyData.bulletTypes.Length) return;
        if (nextIndex == currentTypeIndex) return;

        currentTypeIndex = nextIndex;

        EnemyData.BulletType t = enemyData.bulletTypes[currentTypeIndex];
        if (t == null)
        {
            ResetFirePauseCycle();
            return;
        }

        if (t.useFireIntervalOverride && t.fireIntervalOverride > 0f)
        {
            fireInterval = Mathf.Max(0.01f, t.fireIntervalOverride);
        }

        if (!(t.useFirePauseCycle && t.fireCycleSeconds > 0f))
        {
            ResetFirePauseCycle();
        }
        else
        {
            cycleEnabled = true;
            cycleFireSeconds = Mathf.Max(0.01f, t.fireCycleSeconds);
            cyclePauseSeconds = Mathf.Max(0f, t.pauseCycleSeconds);

            cycleIsFiringPhase = true;
            cyclePhaseEndTime = Time.time + cycleFireSeconds;
        }

        timer = 0f;
        nextShotWaitSeconds = -1f;
    }

    private float ComputeNextShotWaitSeconds()
    {
        float baseInterval = Mathf.Max(0.01f, fireInterval);
        EnemyData.BulletType t = GetCurrentBulletTypeOrNull();
        if (t == null) return baseInterval;

        if (t.useFireIntervalOverride && t.fireIntervalOverride > 0f)
        {
            baseInterval = Mathf.Max(0.01f, t.fireIntervalOverride);
        }

        if (!t.useFireIntervalRandom) return baseInterval;

        float range = Mathf.Max(0f, t.fireIntervalRandomRangeSeconds);
        float result = baseInterval + ((range > 0f) ? Random.Range(-range, range) : 0f);

        float min = Mathf.Max(0.01f, t.fireIntervalMinSeconds);
        if (result < min) result = min;

        return Mathf.Max(0.01f, result);
    }

    private EnemyData.BulletType GetCurrentBulletTypeOrNull()
    {
        if (!HasBulletTypes()) return null;
        if (currentTypeIndex < 0 || currentTypeIndex >= enemyData.bulletTypes.Length) return null;
        return enemyData.bulletTypes[currentTypeIndex];
    }

    private void Fire()
    {
        if (FloorHealth.IsBrokenGlobal) return;

        EnemyData.BulletType type = null;
        if (HasBulletTypes())
        {
            int idx = PickBulletTypeIndex(enemyData.bulletTypes.Length);
            ApplyTypeSettingsIfChanged(idx);
            type = GetCurrentBulletTypeOrNull();
        }

        Vector2 baseDir = ComputeBaseDirection(type);

        // ★プレイヤーの方向を向くように回転（rotateTowardPlayer=trueの場合）
        // 回転はエネミーの中心位置から計算する（muzzleOffsetを適用する前）
        bool shouldRotate = enemyData != null && enemyData.rotateTowardPlayer &&
                            type != null && type.aimMode == EnemyData.BulletType.AimMode.TowardPlayer;

        Debug.Log($"[EnemyShooter] Fire: shouldRotate={shouldRotate}, rotateTowardPlayer={enemyData?.rotateTowardPlayer}, aimMode={type?.aimMode}");

        if (shouldRotate)
        {
            // エネミーの中心位置からプレイヤーへの方向を計算して回転
            Vector2 rotationDir = ComputeFinalDirection(transform.position, baseDir, type);
            RotateTowardDirection(rotationDir);
        }

        // muzzleOffsetをエネミーのローカル座標（回転を考慮）で適用
        // エネミーの下方向（回転後）に向かってオフセットを適用する
        Vector3 localDown = transform.TransformDirection(Vector3.down);
        Vector3 spawnPos = transform.position + localDown * muzzleOffset;

        // ★プレイヤーへの方向を計算（弾の発射方向用）
        Vector2 finalDir = ComputeFinalDirection(spawnPos, baseDir, type);

        int shots = 1;
        float spread = 0f;
        if (type != null && type.useMultiShot)
        {
            shots = Mathf.Max(1, type.shotsPerFire);
            spread = Mathf.Clamp(type.spreadAngleDeg, 0f, 180f);
        }

        float half = spread * 0.5f;

        if (type != null && type.useTelegraph && type.telegraphSeconds > 0f)
        {
            isTelegraphing = true;
            // ★finalDirを使用（プレイヤーへの方向）
            StartCoroutine(FireWithTelegraphRoutine(spawnPos, BuildShotDirs(finalDir, shots, half), type));
            return;
        }

        PlayFireFx(spawnPos);
        // ★finalDirを使用（プレイヤーへの方向）
        SpawnShots(spawnPos, finalDir, shots, half, type);
    }

    private Vector2[] BuildShotDirs(Vector2 baseDir, int shots, float half)
    {
        Vector2[] dirs = new Vector2[shots];
        for (int i = 0; i < shots; i++)
        {
            float ang = (half > 0.0001f) ? Random.Range(-half, half) : 0f;
            Vector2 dir = RotateVector2(baseDir, ang);
            if (dir.sqrMagnitude <= 0.0001f) dir = baseDir;
            dirs[i] = dir.normalized;
        }
        return dirs;
    }

    private void SpawnShots(Vector3 spawnPos, Vector2 baseDir, int shots, float half, EnemyData.BulletType type)
    {
        // Multi Shot spawn offset and launch delay support
        float spawnOffset = 0f;
        float launchDelay = 0f;
        if (type != null && type.useMultiShot)
        {
            spawnOffset = type.multiShotSpawnOffset;
            launchDelay = type.multiShotLaunchDelay;
        }

        // If no launch delay, spawn all bullets immediately
        if (launchDelay <= 0.0001f)
        {
            for (int i = 0; i < shots; i++)
            {
                Vector2 finalDir = ComputeFinalDirection(spawnPos, baseDir, type);
                float ang = (half > 0.0001f) ? Random.Range(-half, half) : 0f;
                Vector2 dir = RotateVector2(finalDir, ang);
                if (dir.sqrMagnitude <= 0.0001f) dir = finalDir;

                // Calculate offset position perpendicular to direction
                Vector3 offsetPos = spawnPos;
                if (spawnOffset > 0.0001f && shots > 1)
                {
                    Vector2 perpendicular = new Vector2(-dir.y, dir.x);
                    float offsetDist = (i - (shots - 1) * 0.5f) * spawnOffset;
                    offsetPos += (Vector3)(perpendicular * offsetDist);
                }

                SpawnBulletOne(offsetPos, dir, type);
            }
        }
        else
        {
            // Launch with delay between each bullet
            StartCoroutine(SpawnShotsDelayedRoutine(spawnPos, baseDir, shots, half, type, spawnOffset, launchDelay));
        }
    }

    private IEnumerator SpawnShotsDelayedRoutine(Vector3 spawnPos, Vector2 baseDir, int shots, float half, EnemyData.BulletType type, float spawnOffset, float launchDelay)
    {
        for (int i = 0; i < shots; i++)
        {
            Vector2 finalDir = ComputeFinalDirection(spawnPos, baseDir, type);
            float ang = (half > 0.0001f) ? Random.Range(-half, half) : 0f;
            Vector2 dir = RotateVector2(finalDir, ang);
            if (dir.sqrMagnitude <= 0.0001f) dir = finalDir;

            // Calculate offset position perpendicular to direction
            Vector3 offsetPos = spawnPos;
            if (spawnOffset > 0.0001f && shots > 1)
            {
                Vector2 perpendicular = new Vector2(-dir.y, dir.x);
                float offsetDist = (i - (shots - 1) * 0.5f) * spawnOffset;
                offsetPos += (Vector3)(perpendicular * offsetDist);
            }

            SpawnBulletOne(offsetPos, dir, type);

            // Wait for delay before next bullet (skip delay on last bullet)
            if (i < shots - 1)
            {
                yield return new WaitForSeconds(launchDelay);
            }
        }
    }

    private IEnumerator FireWithTelegraphRoutine(Vector3 spawnPos, Vector2[] dirs, EnemyData.BulletType type)
    {
        if (dirs == null || dirs.Length <= 0)
        {
            isTelegraphing = false;
            yield break;
        }

        float seconds = Mathf.Max(0.01f, type.telegraphSeconds);
        float len = Mathf.Max(0.1f, type.telegraphLength);
        float width = Mathf.Max(0.001f, type.telegraphWidth);
        Color baseColor = type.telegraphColor;

        bool fade = type.telegraphFadeOut;
        bool unscaled = type.telegraphUseUnscaledTime;

        bool useBlink = type.telegraphUseBlink;
        int blinkCount = Mathf.Max(0, type.telegraphBlinkCount);
        float blinkMinMul = Mathf.Clamp01(type.telegraphBlinkMinAlphaMul);

        // Multi Shot spawn offset and launch delay support
        float spawnOffset = 0f;
        float launchDelay = 0f;
        if (type != null && type.useMultiShot)
        {
            spawnOffset = type.multiShotSpawnOffset;
            launchDelay = type.multiShotLaunchDelay;
        }

        GameObject[] lines = new GameObject[dirs.Length];
        LineRenderer[] lrs = new LineRenderer[dirs.Length];
        Vector3[] offsetPositions = new Vector3[dirs.Length];

        // Create telegraph lines with offset positions
        for (int i = 0; i < dirs.Length; i++)
        {
            Vector3 offsetPos = spawnPos;
            if (spawnOffset > 0.0001f && dirs.Length > 1)
            {
                Vector2 perpendicular = new Vector2(-dirs[i].y, dirs[i].x);
                float offsetDist = (i - (dirs.Length - 1) * 0.5f) * spawnOffset;
                offsetPos += (Vector3)(perpendicular * offsetDist);
            }
            offsetPositions[i] = offsetPos;
            CreateTelegraphLine(offsetPos, dirs[i], len, width, baseColor, out lines[i], out lrs[i]);
        }

        float start = unscaled ? Time.unscaledTime : Time.time;

        while (true)
        {
            float now = unscaled ? Time.unscaledTime : Time.time;
            float t = now - start;
            if (t >= seconds) break;

            float a = baseColor.a;

            if (useBlink && blinkCount > 0)
            {
                float k = Mathf.Clamp01(t / seconds);
                int segments = blinkCount * 2;
                int seg = Mathf.Min(segments - 1, Mathf.FloorToInt(k * segments));
                bool isDark = (seg % 2 == 1);
                a = baseColor.a * (isDark ? blinkMinMul : 1f);
            }
            else if (fade)
            {
                float k = Mathf.Clamp01(t / seconds);
                a = Mathf.Lerp(baseColor.a, 0f, k);
            }

            Color c = new Color(baseColor.r, baseColor.g, baseColor.b, a);
            for (int i = 0; i < lrs.Length; i++)
            {
                if (lrs[i] == null) continue;
                lrs[i].startColor = c;
                lrs[i].endColor = c;
            }

            yield return null;
            if (this == null) yield break;
        }

        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i] != null) Destroy(lines[i]);
        }

        PlayFireFx(spawnPos);

        // Spawn bullets with launch delay if enabled
        if (launchDelay > 0.0001f)
        {
            for (int i = 0; i < dirs.Length; i++)
            {
                SpawnBulletOne(offsetPositions[i], dirs[i], type);

                // Wait for delay before next bullet (skip delay on last bullet)
                if (i < dirs.Length - 1)
                {
                    yield return new WaitForSeconds(launchDelay);
                }
            }
        }
        else
        {
            // Spawn all bullets immediately
            for (int i = 0; i < dirs.Length; i++)
            {
                SpawnBulletOne(offsetPositions[i], dirs[i], type);
            }
        }

        isTelegraphing = false;
    }

    private void CreateTelegraphLine(
        Vector3 spawnPos,
        Vector2 dir,
        float length,
        float width,
        Color color,
        out GameObject lineGo,
        out LineRenderer lr
    )
    {
        lineGo = new GameObject("TelegraphLine");
        lineGo.transform.SetParent(projectileRoot, false);
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

        Vector3 p0 = spawnPos;
        Vector3 p1 = spawnPos + (Vector3)(dir.normalized * length);

        lr.SetPosition(0, p0);
        lr.SetPosition(1, p1);

        lr.startWidth = width;
        lr.endWidth = width;

        lr.startColor = color;
        lr.endColor = color;

        lr.numCapVertices = 4;
        lr.numCornerVertices = 2;
        lr.alignment = LineAlignment.View;
        lr.textureMode = LineTextureMode.Stretch;
    }

    private void PlayFireFx(Vector3 spawnPos)
    {
        if (fireSE != null && fireSEVolume > 0f)
        {
            // SoundSettingsManagerのSE音量を適用
            float finalVolume = fireSEVolume * (SoundSettingsManager.Instance != null ? SoundSettingsManager.Instance.SEVolume : 1f);
            AudioSource.PlayClipAtPoint(fireSE, spawnPos, finalVolume);
        }

        if (fireVfxPrefab != null)
        {
            GameObject vfx = Instantiate(fireVfxPrefab, spawnPos, Quaternion.identity, projectileRoot);
            AutoDestroyVfx(vfx);
        }
    }

    private void SpawnBulletOne(Vector3 spawnPos, Vector2 dir, EnemyData.BulletType type)
    {
        EnemyBullet bullet = Instantiate(bulletPrefab, spawnPos, Quaternion.identity, projectileRoot);

        if (bulletSpriteOverride != null) bullet.SetSpriteOverride(bulletSpriteOverride);

        bullet.SetDirection(dir);
        bullet.ApplyBullet(bulletSpeed, bulletLifeTime);

        if (type != null) ApplyBulletTypeToBullet(bullet, type);

        // ★複数パーツ敵対応：親と全ての子の Collider を無視
        Collider2D[] allColliders = GetComponentsInChildren<Collider2D>();
        Debug.Log($"[EnemyShooter] Found {allColliders.Length} colliders to ignore");
        foreach (Collider2D col in allColliders)
        {
            if (col != null)
            {
                Debug.Log($"[EnemyShooter] Ignoring collider: {col.gameObject.name}");
                bullet.SetOwnerCollisionIgnore(col, ignoreOwnerTime);
            }
        }

        // 未反射弾の物理判定無効化を設定
        if (enemyData != null && enemyData.unreflectedBulletCollisionDisableTime > 0f)
        {
            bullet.SetUnreflectedCollisionDisable(enemyData.unreflectedBulletCollisionDisableTime);
        }
    }

    private static Vector2 RotateVector2(Vector2 v, float degrees)
    {
        float rad = degrees * Mathf.Deg2Rad;
        float s = Mathf.Sin(rad);
        float c = Mathf.Cos(rad);
        return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
    }

    private void ApplyBulletTypeToBullet(EnemyBullet bullet, EnemyData.BulletType t)
    {
        if (bullet == null || t == null) return;

        float s = (t.speed > 0f) ? t.speed : bulletSpeed;
        float lt = (t.lifeTime > 0f) ? t.lifeTime : bulletLifeTime;
        bullet.ApplyBullet(s, lt);

        bullet.SetDamage(Mathf.Max(0, t.damage));

        if (t.spriteOverride != null) bullet.SetSpriteOverride(t.spriteOverride);
        else if (bulletSpriteOverride != null) bullet.SetSpriteOverride(bulletSpriteOverride);

        if (t.paddleBounceLimit >= 0) bullet.ConfigurePaddleBounceLimit(t.paddleBounceLimit);

        if (t.penetration >= 0)
        {
            BulletPenetration pen = bullet.GetComponent<BulletPenetration>();
            if (pen != null) pen.SetPenetration(t.penetration);
        }

        if (t.circleRadius > 0f)
        {
            CircleCollider2D cc = bullet.GetComponent<CircleCollider2D>();
            if (cc != null) cc.radius = t.circleRadius;
        }

        if (t.useScaleOverride) bullet.transform.localScale = new Vector3(t.scaleOverride.x, t.scaleOverride.y, 1f);

        if (t.useColorOverride)
        {
            SpriteRenderer sr = bullet.GetComponentInChildren<SpriteRenderer>();
            if (sr != null) sr.color = t.colorOverride;
        }

        if (t.useWaveMotion)
        {
            bullet.ApplyWaveMotion(t.waveAmplitude, t.waveFrequency);
        }
        else
        {
            bullet.ClearWaveMotion();
        }

        if (t.useSpiralMotion)
        {
            bullet.ApplySpiralMotion(t.spiralRadius, t.spiralPeriod, t.spiralRotateSprite);
        }
        else
        {
            bullet.ClearSpiralMotion();
        }

        if (t.useSpeedCurve)
        {
            float init = (t.initialSpeed > 0f) ? t.initialSpeed : s;
            float max = (t.maxSpeed > 0f) ? t.maxSpeed : s;
            if (max < init) max = init;

            float dur = t.curveDuration;
            AnimationCurve curve = (t.speedCurve != null) ? t.speedCurve : AnimationCurve.Linear(0f, 0f, 1f, 1f);
            bullet.ApplySpeedCurve(init, max, dur, curve);
        }
        else
        {
            bullet.ClearSpeedCurve();
        }

        bullet.ApplyCountdownExplosion(
            t.useCountdownExplosion,
            t.explosionDelaySeconds,
            t.explosionRadius,
            t.explosionDamageToEnemy,
            t.explosionDamageToWall
        );

        bullet.ApplyWarp(
            t.useWarp,
            t.warpDisappearAfterSeconds,
            t.warpReappearAfterSeconds,
            t.warpOffsetXRange,
            t.warpDisappearVfxPrefab,
            t.warpReappearVfxPrefab,
            t.warpDisappearSe,
            t.warpReappearSe
        );

        bullet.ApplyMultiWarhead(
            t.useMultiWarhead,
            t.multiSlowSeconds,
            t.multiSlowSpeed,
            t.multiParentSprite,
            t.multiParentUseSpeedCurve,
            t.multiParentInitialSpeed,
            t.multiParentMaxSpeed,
            t.multiParentCurveDuration,
            t.multiParentSpeedCurve,
            t.multiParentVanishSe,
            t.multiParentVanishVfx,
            t.multiChildOffsetX,
            t.multiChildFinalSpeed,
            t.multiChildUseRandomOffset,
            t.multiChildRandomOffsetRadius,
            t.multiChildA_Delay,
            t.multiChildA_DelayMax,
            t.multiChildA_SpawnSe,
            t.multiChildA_SpawnVfx,
            t.multiChildA_Sprite,
            t.multiChildA_LifeTime,
            t.multiChildB_Delay,
            t.multiChildB_DelayMax,
            t.multiChildB_SpawnSe,
            t.multiChildB_SpawnVfx,
            t.multiChildB_Sprite,
            t.multiChildB_LifeTime,
            bulletPrefab,
            projectileRoot
        );

        // ★MissileArc 適用
        if (t.useMissileArc)
        {
            bullet.ApplyMissileArc(
                t.missileInitialSpeed,
                t.missileStraightDuration,
                Mathf.Abs(t.missileCurveAngle), // 常に絶対値（符号はランダムフラグ次第）
                t.missileCurveRandomDirection,
                t.missileCurveDuration,
                t.missileFinalSpeed,
                t.missileUseSpeedCurve,
                t.missileCurveInitialSpeed,
                t.missileCurveFinalSpeed,
                t.missileSpeedCurve,
                t.missileUseRandomOffset,
                t.missileRandomOffsetRadius
            );
        }
        else
        {
            bullet.ClearMissileArc();
        }

        // ★デバッグテキスト設定を適用
        EnemyBulletDebugText debugText = bullet.GetComponent<EnemyBulletDebugText>();
        if (debugText != null)
        {
            debugText.SetDebugTextEnabled(t.showDebugText);
        }

        // ★煙幕弾設定を適用
        if (t.useSmokeGrenade)
        {
            bullet.ApplySmokeGrenade(
                true,
                t.smokeRadius,
                t.smokeDuration,
                t.smokeExpansionSpeed,
                t.smokeParticlePrefab,
                t.smokeReflectSE,
                t.smokeCircleDissolveFx,
                t.smokeCircleDissolveSE,
                t.smokeCloudCircleDissolveSE
            );
        }
    }

    private int PickBulletTypeIndex(int count)
    {
        if (count <= 0) return -1;

        // HP-Based Routine Switching または 従来の行動ルーチンを取得
        EnemyData.BulletFiringRoutine routine = GetCurrentBulletFiringRoutine();
        if (routine != null)
        {
            switch (routine.routineType)
            {
                case EnemyData.BulletFiringRoutine.RoutineType.Sequence:
                    return PickSequenceIndex(count);

                case EnemyData.BulletFiringRoutine.RoutineType.Probability:
                    return PickProbabilityIndex(count);

                default:
                    break;
            }
        }

        // 従来の選択モード
        switch (selectMode)
        {
            case BulletSelectMode.RoundRobin:
                rrIndex++;
                if (rrIndex >= count) rrIndex = 0;
                return rrIndex;

            case BulletSelectMode.WeightedChance:
                SyncWeightedChancesToTypes();
                return PickWeightedIndex(count);

            case BulletSelectMode.Random:
            default:
                return Random.Range(0, count);
        }
    }

    private int PickSequenceIndex(int count)
    {
        EnemyData.BulletFiringRoutine routine = GetCurrentBulletFiringRoutine();
        if (routine == null) return Random.Range(0, count);
        if (routine.sequenceEntries == null || routine.sequenceEntries.Length == 0)
            return Random.Range(0, count);

        // 残り発射回数が0以下なら、次のエントリに進む
        if (sequenceRemainingShots <= 0)
        {
            sequenceTypeIndex++;
            if (sequenceTypeIndex >= routine.sequenceEntries.Length)
            {
                sequenceTypeIndex = 0;  // ループ
            }

            // 新しいエントリの発射回数を決定
            var entry = routine.sequenceEntries[sequenceTypeIndex];
            int min = Mathf.Max(1, entry.minShots);
            int max = Mathf.Max(min, entry.maxShots);
            sequenceRemainingShots = Random.Range(min, max + 1);
        }

        // 発射回数を減らす
        sequenceRemainingShots--;

        // エントリが指定するBullet Typesのインデックスを返す
        var currentEntry = routine.sequenceEntries[sequenceTypeIndex];
        int bulletTypeIdx = Mathf.Clamp(currentEntry.bulletTypeIndex, 0, count - 1);
        return bulletTypeIdx;
    }

    private int PickProbabilityIndex(int count)
    {
        EnemyData.BulletFiringRoutine routine = GetCurrentBulletFiringRoutine();
        if (routine == null) return Random.Range(0, count);
        if (routine.probabilityEntries == null || routine.probabilityEntries.Length == 0)
            return Random.Range(0, count);

        // 確率の合計を計算
        float total = 0f;
        for (int i = 0; i < routine.probabilityEntries.Length; i++)
        {
            total += Mathf.Max(0f, routine.probabilityEntries[i].probabilityPercentage);
        }

        if (total <= 0.0001f) return Random.Range(0, count);

        // ランダム値を生成して確率に基づいて選択
        float r = Random.value * total;
        float acc = 0f;
        for (int i = 0; i < routine.probabilityEntries.Length; i++)
        {
            acc += Mathf.Max(0f, routine.probabilityEntries[i].probabilityPercentage);
            if (r <= acc)
            {
                // 選択されたエントリが指定するBullet Typesのインデックスを返す
                int bulletTypeIdx = Mathf.Clamp(routine.probabilityEntries[i].bulletTypeIndex, 0, count - 1);
                return bulletTypeIdx;
            }
        }

        // フォールバック：最後のエントリのインデックスを使用
        if (routine.probabilityEntries.Length > 0)
        {
            int bulletTypeIdx = Mathf.Clamp(routine.probabilityEntries[routine.probabilityEntries.Length - 1].bulletTypeIndex, 0, count - 1);
            return bulletTypeIdx;
        }

        return Random.Range(0, count);
    }

    private void SyncWeightedChancesToTypes()
    {
        if (!HasBulletTypes()) return;

        int n = enemyData.bulletTypes.Length;
        if (weightedChances != null && weightedChances.Length == n) return;

        float[] next = new float[n];

        if (weightedChances != null)
        {
            int copy = Mathf.Min(weightedChances.Length, n);
            for (int i = 0; i < copy; i++) next[i] = weightedChances[i];
        }

        for (int i = 0; i < n; i++)
        {
            if (next[i] <= 0f) next[i] = 1f;
        }

        weightedChances = next;
    }

    private int PickWeightedIndex(int count)
    {
        if (weightedChances == null || weightedChances.Length < count) return Random.Range(0, count);

        float sum = 0f;
        for (int i = 0; i < count; i++) sum += Mathf.Max(0f, weightedChances[i]);

        if (sum <= 0.0001f) return Random.Range(0, count);

        float r = Random.value * sum;
        float acc = 0f;
        for (int i = 0; i < count; i++)
        {
            acc += Mathf.Max(0f, weightedChances[i]);
            if (r <= acc) return i;
        }

        return count - 1;
    }

    private void AutoDestroyVfx(GameObject vfx)
    {
        if (vfx == null) return;

        ParticleSystem ps = vfx.GetComponent<ParticleSystem>();
        if (ps != null)
        {
            var main = ps.main;

            float life;
            var lt = main.startLifetime;
            if (lt.mode == ParticleSystemCurveMode.Constant) life = lt.constant;
            else if (lt.mode == ParticleSystemCurveMode.TwoConstants) life = lt.constantMax;
            else life = lt.constantMax;

            float seconds = Mathf.Max(0.1f, main.duration + life);
            Destroy(vfx, seconds);
            return;
        }

        Destroy(vfx, Mathf.Max(0.1f, vfxFallbackDestroySeconds));
    }

    private Vector2 ComputeBaseDirection(EnemyData.BulletType type)
    {
        return (fireDirection.sqrMagnitude > 0.0001f) ? fireDirection.normalized : Vector2.down;
    }

    private Vector2 ComputeFinalDirection(Vector3 spawnPos, Vector2 baseDir, EnemyData.BulletType type)
    {
        if (type == null || type.aimMode == EnemyData.BulletType.AimMode.UseFireDirection)
        {
            return baseDir;
        }

        if (type.aimMode == EnemyData.BulletType.AimMode.TowardPlayer)
        {
            // プレイヤー（Playerタグ）を探す
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj == null)
            {
                // Playerタグが見つからない場合、PixelDancerControllerで探す
                PixelDancerController playerDancer = FindFirstObjectByType<PixelDancerController>();
                if (playerDancer != null)
                {
                    playerObj = playerDancer.gameObject;
                }
            }

            if (playerObj != null)
            {
                // プレイヤーへの方向を計算
                Vector2 targetPos = playerObj.transform.position;
                Vector2 dirToPlayer = (targetPos - (Vector2)spawnPos).normalized;
                Debug.Log($"[EnemyShooter] ComputeFinalDirection: Found player at {targetPos}, enemy at {spawnPos}, dirToPlayer={dirToPlayer}");
                return (dirToPlayer.sqrMagnitude > 0.0001f) ? dirToPlayer : baseDir;
            }

            Debug.LogWarning($"[EnemyShooter] ComputeFinalDirection: Player not found! Make sure Pixceldancer has 'Player' tag.");
            return baseDir;
        }

        // TowardRandomPointInPlayerRange
        PixelDancerController dancer = FindFirstObjectByType<PixelDancerController>();
        if (dancer == null) return baseDir;

        float centerX = dancer.transform.position.x;
        float centerY = dancer.transform.position.y;
        float range = 3f;

        System.Reflection.FieldInfo rangeField = typeof(PixelDancerController).GetField("autoMoveRange", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        if (rangeField != null)
        {
            object val = rangeField.GetValue(dancer);
            if (val is float f) range = f;
        }

        float targetX = centerX + Random.Range(-range, range);
        Vector2 target = new Vector2(targetX, centerY);
        Vector2 dir = (target - (Vector2)spawnPos).normalized;

        return (dir.sqrMagnitude > 0.0001f) ? dir : baseDir;
    }

    private void RotateTowardDirection(Vector2 direction)
    {
        if (direction.sqrMagnitude <= 0.0001f)
        {
            Debug.Log($"[EnemyShooter] RotateTowardDirection: direction is zero, skipping rotation");
            return;
        }

        // 2D回転：方向ベクトルから角度を計算
        // Atan2は右向き(1,0)を0度とするので、スプライトが下向き(-90度)をデフォルトとする場合は90度足す
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + 90f;

        Debug.Log($"[EnemyShooter] RotateTowardDirection: direction={direction}, angle={angle}°, current rotation={transform.rotation.eulerAngles.z}°");

        // Z軸のみ回転（2D）
        transform.rotation = Quaternion.Euler(0, 0, angle);

        Debug.Log($"[EnemyShooter] After rotation: new rotation={transform.rotation.eulerAngles.z}°");
    }

    /// <summary>
    /// ワープ後などにプレイヤー方向に回転させるための公開メソッド
    /// rotateTowardPlayerフラグとTowardPlayerモードが有効な場合のみ回転する
    /// </summary>
    public void RotateTowardPlayerIfNeeded()
    {
        if (enemyData == null || !enemyData.rotateTowardPlayer) return;

        // 現在のBulletTypeを取得
        EnemyData.BulletType type = GetCurrentBulletTypeOrNull();
        if (type == null || type.aimMode != EnemyData.BulletType.AimMode.TowardPlayer) return;

        // エネミーの中心位置からプレイヤーへの方向を計算
        Vector2 baseDir = (fireDirection.sqrMagnitude > 0.0001f) ? fireDirection.normalized : Vector2.down;
        Vector2 rotationDir = ComputeFinalDirection(transform.position, baseDir, type);

        Debug.Log($"[EnemyShooter] RotateTowardPlayerIfNeeded called from external (e.g., Warp)");
        RotateTowardDirection(rotationDir);
    }

    // =========================================================
    // Debug Info (デバッグ情報取得)
    // =========================================================
    public struct DebugRoutineInfo
    {
        public bool isUsingRoutine;
        public EnemyData.BulletFiringRoutine.RoutineType? routineType;
        public int currentElementIndex;
        public string currentBulletTypeName;
    }

    public DebugRoutineInfo GetDebugRoutineInfo()
    {
        DebugRoutineInfo info = new DebugRoutineInfo();

        EnemyData.BulletFiringRoutine routine = GetCurrentBulletFiringRoutine();
        if (enemyData != null && routine != null)
        {
            info.isUsingRoutine = true;
            info.routineType = routine.routineType;
            info.currentElementIndex = currentTypeIndex;

            if (currentTypeIndex >= 0 && currentTypeIndex < enemyData.bulletTypes.Length && enemyData.bulletTypes[currentTypeIndex] != null)
            {
                info.currentBulletTypeName = enemyData.bulletTypes[currentTypeIndex].name;
            }
            else
            {
                info.currentBulletTypeName = "None";
            }
        }
        else
        {
            info.isUsingRoutine = false;
            info.routineType = null;
            info.currentElementIndex = currentTypeIndex;
            
            if (currentTypeIndex >= 0 && HasBulletTypes() && currentTypeIndex < enemyData.bulletTypes.Length && enemyData.bulletTypes[currentTypeIndex] != null)
            {
                info.currentBulletTypeName = enemyData.bulletTypes[currentTypeIndex].name;
            }
            else
            {
                info.currentBulletTypeName = "None";
            }
        }
        
        return info;
    }

    // =========================================================
    // HP-Based Routine Switching (HP％に応じたルーチン切り替え)
    // =========================================================
    private void InitializeHpBasedBulletRoutine()
    {
        if (enemyData == null || !enemyData.useHpBasedRoutineSwitch) return;
        if (enemyStats == null) return;

        // 初期状態では高HP用ルーチンを使用
        int routineIndex = (int)enemyData.bulletRoutineAboveThreshold;
        if (routineIndex >= 0 && routineIndex < enemyData.bulletFiringRoutines.Length)
        {
            currentBulletFiringRoutine = enemyData.bulletFiringRoutines[routineIndex];
        }
    }

    private void CheckHpAndSwitchBulletRoutine()
    {
        if (enemyData == null || !enemyData.useHpBasedRoutineSwitch) return;
        if (enemyStats == null) return;
        if (hasSwitchedToLowHpBulletRoutine) return;  // 一度切り替わったら戻らない

        // HP％を計算
        float hpPercentage = enemyStats.GetHpPercentage();

        // HP閾値を下回ったら低HP用ルーチンに切り替え
        if (hpPercentage < enemyData.hpThresholdPercentage)
        {
            hasSwitchedToLowHpBulletRoutine = true;
            int routineIndex = (int)enemyData.bulletRoutineBelowThreshold;
            if (routineIndex >= 0 && routineIndex < enemyData.bulletFiringRoutines.Length)
            {
                currentBulletFiringRoutine = enemyData.bulletFiringRoutines[routineIndex];
            }

            // ルーチン切り替え時にSequenceインデックスをリセット
            sequenceTypeIndex = 0;
            sequenceRemainingShots = 0;
        }
    }

    // 現在使用中のルーチンを取得
    private EnemyData.BulletFiringRoutine GetCurrentBulletFiringRoutine()
    {
        if (enemyData == null) return null;

        // HP-Based Routine Switchingが有効な場合
        if (enemyData.useHpBasedRoutineSwitch && currentBulletFiringRoutine != null)
        {
            return currentBulletFiringRoutine;
        }

        // 従来のルーチン
        if (enemyData.useFiringRoutine)
        {
            return enemyData.firingRoutine;
        }

        return null;
    }
}
