using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GuardBeastController : MonoBehaviour
{
    public enum GuardBeastPreviewSprite
    {
        Idle1, Idle2, Idle3, Idle4, IdleAnimate,
        Bound1, Bound2, Bound3, Bound4, Bound5, Bound6, Bound7, Bound8, Bound9, Bound10, BoundAnimate,
        Attack1_1, Attack1_2, Attack1_3, Attack1Animate,
        Attack2_1, Attack2_2, Attack2_3, Attack2Animate,
        Attack3_1, Attack3_2, Attack3_3, Attack3Animate,
        ClawMark1, ClawMark2, ClawMark3, ClawMark4, ClawMarkAnimate,
        ThunderPreview
    }

    [System.Serializable]
    public class GuardBeastFrame
    {
        public Sprite  sprite;
        public Vector2 offset;
        [Tooltip("表示秒数")]
        public float   duration;
        [Tooltip("マズル位置（firePointFaceのローカル座標。攻撃フレームのみ有効）")]
        public Vector2 muzzleOffset;
        [Tooltip("当たり判定サイズ（0,0のときは変更しない）")]
        public Vector2 colliderSize;
        [Tooltip("当たり判定オフセット")]
        public Vector2 colliderOffset;
        [Tooltip("スプライトのZ軸回転（度）")]
        public float   rotationZ;
    }

    // ======================================================
    // Inspector fields
    // ======================================================

    [Header("References")]
    [SerializeField] private SpriteRenderer     spriteRenderer;
    [SerializeField] private BoxCollider2D      bodyCollider;
    [SerializeField] private EnemyMover         enemyMover;
    [SerializeField] private EnemyStats         enemyStats;
    [SerializeField] private EnemyShooter       enemyShooter;
    [SerializeField] private EnemySpriteSwapper spriteSwapper;
    [SerializeField] private Transform          firePointFace;
    [SerializeField] private EnemyBullet        bulletPrefab;
    [SerializeField] private Transform          projectileRoot;
    [SerializeField] private float              bulletSpeed = 6f;
    [SerializeField] private float              bulletLifeTime = 5f;
    [SerializeField] private float              ignoreOwnerTime = 0.15f;

    [Header("Editor Preview")]
    [SerializeField] private GuardBeastPreviewSprite previewSprite = GuardBeastPreviewSprite.Idle1;

    [Header("Sprites")]
    [NonReorderable]
    [SerializeField] private GuardBeastFrame[] idleFrames;
    [NonReorderable]
    [SerializeField] private GuardBeastFrame[] boundFrames;      // 10: 溜め1-5, 跳躍1-3, 着地1-2
    [NonReorderable]
    [SerializeField] private GuardBeastFrame[] attack1Frames;    // 3: 正面咆哮
    [NonReorderable]
    [SerializeField] private GuardBeastFrame[] attack2Frames;    // 3: 見上げ咆哮
    [NonReorderable]
    [SerializeField] private GuardBeastFrame[] attack3Frames;    // 3: 爪の薙ぎ払い
    [NonReorderable]
    [SerializeField] private GuardBeastFrame[] clawMarkFrames;   // 4: 爪痕エフェクト

    [Header("Movement - Bound")]
    [Tooltip("溜め/着地 各コマの表示秒数")]
    [SerializeField] private float boundPoseFrameDuration = 0.08f;
    [Tooltip("跳躍中（移動）の所要時間（秒）: 1マス分の移動")]
    [SerializeField] private float boundMoveDurationOneStep = 0.35f;
    [Tooltip("跳躍中（移動）の所要時間（秒）: 2マス分の移動")]
    [SerializeField] private float boundMoveDurationTwoStep = 0.5f;
    [Tooltip("着地後、次のバウンドまでの静止時間（秒）の最小値")]
    [SerializeField] private float boundStopDurationMin = 0.15f;
    [Tooltip("着地後、次のバウンドまでの静止時間（秒）の最大値")]
    [SerializeField] private float boundStopDurationMax = 0.3f;
    [Tooltip("この回数バウンドするごとに攻撃①②③のいずれかを発動")]
    [SerializeField] private int boundsPerAttackTrigger = 6;

    [Header("Attack1 - Roar Ring")]
    [Tooltip("フレームのDurationが未設定(0)の場合のフォールバック秒数")]
    [SerializeField] private float attack1PoseFrameDuration = 0.15f;
    [Tooltip("360度リング弾幕の発射数")]
    [SerializeField] private int ringBulletCount = 16;
    [Tooltip("EnemyData.bulletTypes のインデックス（リング弾が使う弾種）")]
    [SerializeField] private int attack1BulletTypeIndex = 0;
    [Tooltip("リングを連続発射する回数（1=1回のみ）")]
    [SerializeField] private int ringRepeatCount = 1;
    [Tooltip("連続発射時、リングとリングの間隔（秒）")]
    [SerializeField] private float ringRepeatInterval = 0.2f;
    [Tooltip("連続発射時、1回ごとにリング全体を回転させる角度（度）")]
    [SerializeField] private float ringRepeatAngleOffsetDeg = 11.25f;

    [Header("Attack2 - Thunder Cloud")]
    [SerializeField] private float attack2PoseFrameDuration = 0.2f;
    [SerializeField] private GameObject thunderCloudPrefab;
    [Tooltip("雷雲の出現位置（ワールド座標）")]
    [SerializeField] private Vector2 thunderCloudPosition;
    [Tooltip("EnemyData.bulletTypes のインデックス（雷雲が使う弾種）")]
    [SerializeField] private int thunderBulletTypeIndex = 7;
    [Tooltip("雷雲出現位置プレビュー用 SpriteRenderer（Prefab内のThunderPreviewオブジェクト）")]
    [SerializeField] private SpriteRenderer thunderPreviewRenderer;
    [Tooltip("雷雲本体のアニメーション（古代紋様の横雷紋/2/3など。Shamanのthunder Framesと同じ構成）")]
    [NonReorderable]
    [SerializeField] private ShamanController.ShamanFrame[] thunderFrames;

    [Header("Attack3 - Claw Swipe")]
    [SerializeField] private float attack3PoseFrameDuration = 0.12f;
    [Tooltip("爪痕コマ送りの表示秒数（各フレームのMuzzle Offset位置から1発ずつ発射）")]
    [SerializeField] private float clawMarkFrameDuration = 0.05f;
    [Tooltip("EnemyData.bulletTypes のインデックス（爪攻撃が使う弾種）")]
    [SerializeField] private int attack3BulletTypeIndex = 2;

    [Header("Attack4 - Face Bullet (Moving)")]
    [SerializeField] private bool useFaceBulletDuringMove = true;

    [Header("Phase Transition")]
    [Tooltip("後半フェーズ移行HP閾値（0〜100%）")]
    [Range(1f, 99f)]
    [SerializeField] private float phaseTransitionHpThreshold = 70f;
    [Tooltip("後半フェーズでの攻撃①②③抽選確率（合計100推奨）")]
    [SerializeField] private float attack1ProbabilityBack = 20f;
    [SerializeField] private float attack2ProbabilityBack = 20f;
    [SerializeField] private float attack3ProbabilityBack = 60f;

    // ======================================================
    // Grid adjacency (SP01-SP09、行優先。SP08(index7)は着地対象から常に除外)
    // ======================================================

    private static readonly int[][] oneStepMoves = new int[][]
    {
        new int[] { 1, 4 },             // 0 SP01
        new int[] { 0, 2, 3, 5 },       // 1 SP02
        new int[] { 1, 4 },             // 2 SP03
        new int[] { 1, 4 },             // 3 SP04 (SP08除外)
        new int[] { 0, 2, 3, 5, 6, 8 }, // 4 SP05
        new int[] { 1, 4 },             // 5 SP06 (SP08除外)
        new int[] { 4 },                // 6 SP07 (SP08除外)
        null,                            // 7 SP08 (着地不可のため未使用)
        new int[] { 4 },                // 8 SP09 (SP08除外)
    };

    private static readonly int[][] twoStepMoves = new int[][]
    {
        new int[] { 2, 8 }, // 0 SP01 → SP03(横一列) / SP09(対角)
        null,                // 1 SP02
        new int[] { 0, 6 }, // 2 SP03 → SP01(横一列) / SP07(対角)
        new int[] { 5 },     // 3 SP04 → SP06(横一列)
        null,                // 4 SP05
        new int[] { 3 },     // 5 SP06 → SP04(横一列)
        new int[] { 2, 8 }, // 6 SP07 → SP03(対角) / SP09(横一列、SP08をまたぐ)
        null,                // 7 SP08
        new int[] { 0, 6 }, // 8 SP09 → SP01(対角) / SP07(横一列、SP08をまたぐ)
    };

    // ======================================================
    // Runtime state
    // ======================================================

    private enum Phase { Front, Back }
    private Phase _phase = Phase.Front;
    private bool  _phaseTransitioned;

    private EnemySpawner _spawner;
    private EnemyData    _enemyData;
    private bool         isDead;

    private int  _currentGridIdx = 4;
    private bool _isBoundMoving;
    private int  _boundCount;

    private Coroutine _mainLoopCoroutine;
    private Coroutine _faceBulletCoroutine;

    private readonly List<GameObject> activeClawMarkVisuals = new List<GameObject>();

    // ======================================================
    // Lifecycle
    // ======================================================

    private void Awake()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        if (enemyMover == null)   enemyMover   = GetComponent<EnemyMover>();
        if (enemyStats == null)  enemyStats   = GetComponent<EnemyStats>();
        if (enemyShooter == null) enemyShooter = GetComponent<EnemyShooter>();
        if (spriteSwapper == null) spriteSwapper = GetComponent<EnemySpriteSwapper>();

        if (enemyMover != null)
            enemyMover.suppressMovement = true;

        if (enemyShooter != null)
            enemyShooter.enabled = false;
    }

    private void OnDestroy()
    {
        isDead = true;
        foreach (var go in activeClawMarkVisuals)
            if (go != null) Destroy(go);
        activeClawMarkVisuals.Clear();
    }

    private void Start()
    {
        if (!Application.isPlaying) return;

        if (thunderPreviewRenderer != null)
            thunderPreviewRenderer.enabled = false;

        if (enemyShooter != null)
        {
            _enemyData = enemyShooter.GetEnemyData();
            if (bulletPrefab == null)   bulletPrefab   = enemyShooter.GetBulletPrefab();
            if (projectileRoot == null) projectileRoot = enemyShooter.GetProjectileRoot();
        }

        _spawner = FindFirstObjectByType<EnemySpawner>();
        _currentGridIdx = FindNearestGridIndex();

        ApplyFrame(GetIdleFrame(0));
        _mainLoopCoroutine   = StartCoroutine(MainLoop());
        _faceBulletCoroutine = StartCoroutine(FaceBulletLoop());
    }

    private void Update()
    {
        if (!Application.isPlaying) return;
        CheckPhaseTransition();
    }

    private int FindNearestGridIndex()
    {
        if (_spawner == null) return 4;
        float minDist = float.MaxValue;
        int best = 4;
        for (int i = 0; i < 9; i++)
        {
            if (i == 7) continue; // SP08は候補外
            Transform sp = _spawner.GetSpawnPoint(i);
            if (sp == null) continue;
            float dist = Vector3.Distance(transform.position, sp.position);
            if (dist < minDist) { minDist = dist; best = i; }
        }
        return best;
    }

    // ======================================================
    // Phase
    // ======================================================

    private void CheckPhaseTransition()
    {
        if (_phaseTransitioned || _phase == Phase.Back) return;
        if (enemyStats == null) return;
        if (enemyStats.GetHpPercentage() <= phaseTransitionHpThreshold)
        {
            _phase = Phase.Back;
            _phaseTransitioned = true;
        }
    }

    // ======================================================
    // Slow motion helpers
    // ======================================================

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

    // ======================================================
    // Idle呼吸ループ（バウンド停止中に再生）
    // ======================================================

    private IEnumerator PlayIdleLoop(float totalDuration)
    {
        if (idleFrames == null || idleFrames.Length == 0)
        {
            yield return WaitScaled(totalDuration);
            yield break;
        }

        float elapsed = 0f;
        int idx = 0;
        while (elapsed < totalDuration)
        {
            if (isDead) yield break;
            GuardBeastFrame f = idleFrames[idx % idleFrames.Length];
            ApplyFrame(f);
            float frameDur = (f != null && f.duration > 0f) ? f.duration : 0.15f;
            frameDur = Mathf.Min(frameDur, totalDuration - elapsed);
            yield return WaitScaled(frameDur);
            elapsed += frameDur;
            idx++;
        }
    }

    // ======================================================
    // Main Loop（バウンド移動 → 攻撃トリガー）
    // ======================================================

    private IEnumerator MainLoop()
    {
        while (true)
        {
            yield return StartCoroutine(BoundOnce());
            _boundCount++;

            if (boundsPerAttackTrigger > 0 && _boundCount % boundsPerAttackTrigger == 0)
            {
                yield return StartCoroutine(PlayTriggeredAttack());
            }
            else
            {
                float stopDuration = Random.Range(boundStopDurationMin, boundStopDurationMax);
                yield return StartCoroutine(PlayIdleLoop(stopDuration));
            }
        }
    }

    private IEnumerator BoundOnce()
    {
        int nextIdx = PickNextGridIndex(_currentGridIdx, out bool isTwoStep);
        Transform sp = _spawner != null ? _spawner.GetSpawnPoint(nextIdx) : null;
        Vector3 targetPos = sp != null ? sp.position : transform.position;
        float moveDuration = isTwoStep ? boundMoveDurationTwoStep : boundMoveDurationOneStep;

        UpdateFacing(targetPos);

        // 溜め（Bound1-5）: 静止
        for (int i = 0; i < 5 && boundFrames != null && i < boundFrames.Length; i++)
        {
            ApplyFrame(boundFrames[i]);
            yield return WaitScaled(boundPoseFrameDuration);
        }

        // 跳躍（Bound6-8）: 移動しながらコマ送り
        Vector3 startPos = transform.position;
        float elapsed = 0f;
        int jumpFrameIdx = 5;
        _isBoundMoving = true;
        while (elapsed < moveDuration)
        {
            if (isDead) yield break;
            elapsed += Time.deltaTime * GetTimeScale() * GetSpeedMul();
            float ratio = Mathf.Clamp01(elapsed / moveDuration);
            transform.position = Vector3.Lerp(startPos, targetPos, ratio);

            int wantIdx = 5 + Mathf.Min(2, (int)(ratio * 3f));
            if (wantIdx != jumpFrameIdx)
            {
                jumpFrameIdx = wantIdx;
                if (boundFrames != null && jumpFrameIdx < boundFrames.Length)
                    ApplyFrame(boundFrames[jumpFrameIdx]);
            }
            yield return null;
        }
        _isBoundMoving = false;
        transform.position = targetPos;
        _currentGridIdx = nextIdx;

        // 着地（Bound9-10）: 静止
        for (int i = 8; i < 10 && boundFrames != null && i < boundFrames.Length; i++)
        {
            ApplyFrame(boundFrames[i]);
            yield return WaitScaled(boundPoseFrameDuration);
        }
    }

    private int PickNextGridIndex(int currentIdx, out bool isTwoStep)
    {
        int[] twoStep = (currentIdx >= 0 && currentIdx < twoStepMoves.Length) ? twoStepMoves[currentIdx] : null;
        bool canTwoStep = twoStep != null && twoStep.Length > 0;
        bool useTwoStep = canTwoStep && Random.value < 0.5f;

        if (useTwoStep)
        {
            isTwoStep = true;
            return twoStep[Random.Range(0, twoStep.Length)];
        }

        isTwoStep = false;
        int[] oneStep = (currentIdx >= 0 && currentIdx < oneStepMoves.Length) ? oneStepMoves[currentIdx] : null;
        if (oneStep == null || oneStep.Length == 0) return currentIdx;
        return oneStep[Random.Range(0, oneStep.Length)];
    }

    private void UpdateFacing(Vector3 targetPos)
    {
        if (spriteRenderer == null) return;
        float dx = targetPos.x - transform.position.x;
        // GuardBeastは左向き基準の素材のため、右移動時にflipXする
        if (dx > 0.001f)       spriteRenderer.flipX = true;
        else if (dx < -0.001f) spriteRenderer.flipX = false;
    }

    // ======================================================
    // 攻撃トリガー（HP70%で①のみ→①②③抽選に切替）
    // ======================================================

    private IEnumerator PlayTriggeredAttack()
    {
        if (_phase == Phase.Front)
        {
            yield return StartCoroutine(Attack1_RoarRing());
            yield break;
        }

        float total = Mathf.Max(0.0001f, attack1ProbabilityBack + attack2ProbabilityBack + attack3ProbabilityBack);
        float r = Random.value * total;
        if (r <= attack1ProbabilityBack)
            yield return StartCoroutine(Attack1_RoarRing());
        else if (r <= attack1ProbabilityBack + attack2ProbabilityBack)
            yield return StartCoroutine(Attack2_ThunderCloud());
        else
            yield return StartCoroutine(Attack3_ClawSwipe());
    }

    // ------------------------------------------------------
    // Attack1: 正面咆哮（360度リング弾幕）
    // ------------------------------------------------------

    private IEnumerator Attack1_RoarRing()
    {
        if (attack1Frames != null && attack1Frames.Length > 0) { ApplyFrame(attack1Frames[0]); yield return WaitScaled(FrameDurationOr(attack1Frames[0], attack1PoseFrameDuration)); }
        if (attack1Frames != null && attack1Frames.Length > 1) { ApplyFrame(attack1Frames[1]); yield return WaitScaled(FrameDurationOr(attack1Frames[1], attack1PoseFrameDuration)); }
        if (attack1Frames != null && attack1Frames.Length > 2) { ApplyFrame(attack1Frames[2]); }

        yield return StartCoroutine(FireRoarRingRepeat());
        yield return WaitScaled(FrameDurationOr(attack1Frames != null && attack1Frames.Length > 2 ? attack1Frames[2] : null, attack1PoseFrameDuration));

        // 戻り（②→①の折り返し）
        if (attack1Frames != null && attack1Frames.Length > 3) { ApplyFrame(attack1Frames[3]); yield return WaitScaled(FrameDurationOr(attack1Frames[3], attack1PoseFrameDuration)); }
        if (attack1Frames != null && attack1Frames.Length > 4) { ApplyFrame(attack1Frames[4]); yield return WaitScaled(FrameDurationOr(attack1Frames[4], attack1PoseFrameDuration)); }
    }

    private IEnumerator FireRoarRingRepeat()
    {
        int repeats = Mathf.Max(1, ringRepeatCount);
        for (int r = 0; r < repeats; r++)
        {
            FireRoarRing(r * ringRepeatAngleOffsetDeg);
            if (r < repeats - 1)
                yield return WaitScaled(ringRepeatInterval);
        }
    }

    private void FireRoarRing(float angleOffsetDeg)
    {
        EnemyData.BulletType bt = GetBulletType(attack1BulletTypeIndex);
        if (bt == null || bulletPrefab == null || projectileRoot == null || ringBulletCount <= 0) return;

        Vector3 origin = firePointFace != null ? firePointFace.position : transform.position;
        for (int i = 0; i < ringBulletCount; i++)
        {
            float angle = ((360f / ringBulletCount) * i + angleOffsetDeg) * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            SpawnBullet(origin, dir, bt);
        }
    }

    // ------------------------------------------------------
    // Attack2: 見上げ咆哮（ShamanController.SpawnThunderCloud()と同じ仕組み）
    // ------------------------------------------------------

    private IEnumerator Attack2_ThunderCloud()
    {
        if (attack2Frames != null && attack2Frames.Length > 0) { ApplyFrame(attack2Frames[0]); yield return WaitScaled(FrameDurationOr(attack2Frames[0], attack2PoseFrameDuration)); }
        if (attack2Frames != null && attack2Frames.Length > 1) { ApplyFrame(attack2Frames[1]); yield return WaitScaled(FrameDurationOr(attack2Frames[1], attack2PoseFrameDuration)); }
        if (attack2Frames != null && attack2Frames.Length > 2) { ApplyFrame(attack2Frames[2]); }

        SpawnThunderCloud();
        yield return WaitScaled(FrameDurationOr(attack2Frames != null && attack2Frames.Length > 2 ? attack2Frames[2] : null, attack2PoseFrameDuration));

        // 戻り（②→①の折り返し）
        if (attack2Frames != null && attack2Frames.Length > 3) { ApplyFrame(attack2Frames[3]); yield return WaitScaled(FrameDurationOr(attack2Frames[3], attack2PoseFrameDuration)); }
        if (attack2Frames != null && attack2Frames.Length > 4) { ApplyFrame(attack2Frames[4]); yield return WaitScaled(FrameDurationOr(attack2Frames[4], attack2PoseFrameDuration)); }
    }

    private void SpawnThunderCloud()
    {
        if (thunderCloudPrefab == null) return;

        Vector3 pos = new Vector3(thunderCloudPosition.x, thunderCloudPosition.y, transform.position.z);
        GameObject go = Instantiate(thunderCloudPrefab, pos, Quaternion.identity);
        ThunderCloud cloud = go.GetComponent<ThunderCloud>();
        if (cloud == null) { Destroy(go); return; }

        EnemyData.BulletType bt = GetBulletType(thunderBulletTypeIndex);
        if (bulletPrefab != null)
            cloud.SetBulletType(bt, bulletPrefab, projectileRoot);

        if (thunderFrames != null && thunderFrames.Length > 0)
            cloud.SetFrames(thunderFrames);

        cloud.Activate();
    }

    // ------------------------------------------------------
    // Attack3: 爪の薙ぎ払い（4本の爪痕、0.1秒間隔）
    // ------------------------------------------------------

    private IEnumerator Attack3_ClawSwipe()
    {
        if (attack3Frames != null && attack3Frames.Length > 0) { ApplyFrame(attack3Frames[0]); yield return WaitScaled(FrameDurationOr(attack3Frames[0], attack3PoseFrameDuration)); }
        if (attack3Frames != null && attack3Frames.Length > 1) { ApplyFrame(attack3Frames[1]); yield return WaitScaled(FrameDurationOr(attack3Frames[1], attack3PoseFrameDuration)); }
        if (attack3Frames != null && attack3Frames.Length > 2) { ApplyFrame(attack3Frames[2]); }

        yield return StartCoroutine(ClawSwipeRoutine());
        yield return WaitScaled(FrameDurationOr(attack3Frames != null && attack3Frames.Length > 2 ? attack3Frames[2] : null, attack3PoseFrameDuration));
    }

    // 爪痕（Claw Mark Frames）を1回だけ再生し、各フレームの Muzzle Offset から
    // そのフレームのタイミングで1発ずつ発射する（4フレーム = 4発、1本の爪痕として1回だけ再生）
    private IEnumerator ClawSwipeRoutine()
    {
        if (clawMarkFrames == null || clawMarkFrames.Length == 0) yield break;

        bool flip = spriteRenderer != null && spriteRenderer.flipX;
        Vector3 basePos = firePointFace != null ? firePointFace.position : transform.position;

        GameObject go = new GameObject("ClawMarkVisual");
        go.transform.SetPositionAndRotation(basePos, Quaternion.identity);
        if (projectileRoot != null) go.transform.SetParent(projectileRoot, true);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.flipX = flip;
        if (spriteRenderer != null) sr.sortingLayerID = spriteRenderer.sortingLayerID;

        activeClawMarkVisuals.Add(go);

        EnemyData.BulletType bt = GetBulletType(attack3BulletTypeIndex);

        for (int i = 0; i < clawMarkFrames.Length; i++)
        {
            if (isDead || go == null) yield break;

            GuardBeastFrame frame = clawMarkFrames[i];
            sr.sprite = frame.sprite;
            ApplyClawMarkFrame(go.transform, frame, basePos, flip);

            float mx = flip ? -frame.muzzleOffset.x : frame.muzzleOffset.x;
            Vector3 muzzlePos = basePos + new Vector3(mx, frame.muzzleOffset.y, 0f);

            if (bt != null && bulletPrefab != null && projectileRoot != null)
            {
                Vector2 dir = ComputeTowardRandomPlayerRangeDir(muzzlePos);
                SpawnBullet(muzzlePos, dir, bt);
            }

            yield return WaitScaled(clawMarkFrameDuration);
        }

        activeClawMarkVisuals.Remove(go);
        if (go != null) Destroy(go);
    }

    private static void ApplyClawMarkFrame(Transform t, GuardBeastFrame frame, Vector3 basePos, bool flip)
    {
        if (t == null || frame == null) return;
        float x = flip ? -frame.offset.x : frame.offset.x;
        t.position = basePos + new Vector3(x, frame.offset.y, 0f);
        ApplyRotation(t, frame.rotationZ, flip);
    }

    private Vector2 ComputeTowardRandomPlayerRangeDir(Vector3 spawnPos)
    {
        PixelDancerController dancer = FindFirstObjectByType<PixelDancerController>();
        if (dancer == null) return Vector2.down;

        float centerX = dancer.transform.position.x;
        float centerY = dancer.transform.position.y;
        float range = dancer.AutoMoveRange;

        float targetX = centerX + Random.Range(-range, range);
        Vector2 target = new Vector2(targetX, centerY);
        Vector2 dir = (target - (Vector2)spawnPos).normalized;
        return (dir.sqrMagnitude > 0.0001f) ? dir : Vector2.down;
    }

    // ------------------------------------------------------
    // Attack4: 顔からの弾（移動中）
    // ------------------------------------------------------

    private IEnumerator FaceBulletLoop()
    {
        while (true)
        {
            if (isDead) yield break;

            if (!useFaceBulletDuringMove || !_isBoundMoving)
            {
                yield return null;
                continue;
            }

            EnemyData.BulletType firedType = FireFaceBullet();
            yield return WaitScaled(GetFaceBulletInterval(firedType));
        }
    }

    private float GetFaceBulletInterval(EnemyData.BulletType bt)
    {
        float interval = (_enemyData != null && _enemyData.fireInterval > 0f) ? _enemyData.fireInterval : 1.5f;
        if (bt != null && bt.useFireIntervalOverride && bt.fireIntervalOverride > 0f)
            interval = bt.fireIntervalOverride;
        return Mathf.Max(0.05f, interval);
    }

    private EnemyData.BulletType FireFaceBullet()
    {
        EnemyData.BulletType bt = PickBulletType();
        if (bt == null || bulletPrefab == null || projectileRoot == null) return bt;

        Vector3 firePos = firePointFace != null ? firePointFace.position : transform.position;
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        Vector2 dir = playerObj != null
            ? ((Vector2)(playerObj.transform.position - firePos)).normalized
            : Vector2.down;

        if (bt.useMultiShot && bt.shotsPerFire > 1)
        {
            int shots   = Mathf.Max(1, bt.shotsPerFire);
            float half  = Mathf.Clamp(bt.spreadAngleDeg, 0f, 180f) * 0.5f;
            float delay = bt.multiShotLaunchDelay;

            if (delay > 0.0001f)
                StartCoroutine(FireMultiDelayed(firePos, dir, shots, half, bt, bt.multiShotSpawnOffset, delay));
            else
                FireMulti(firePos, dir, shots, half, bt, bt.multiShotSpawnOffset);
        }
        else
        {
            SpawnBullet(firePos, dir, bt);
        }

        return bt;
    }

    private void FireMulti(Vector3 firePos, Vector2 fireDir, int shots, float half, EnemyData.BulletType bt, float spawnOffset)
    {
        for (int i = 0; i < shots; i++)
        {
            float ang   = (half > 0.0001f) ? Random.Range(-half, half) : 0f;
            Vector2 dir = RotateVec(fireDir, ang);
            if (dir.sqrMagnitude <= 0.0001f) dir = fireDir;

            Vector3 pos = firePos;
            if (spawnOffset > 0.0001f && shots > 1)
            {
                Vector2 perp = new Vector2(-dir.y, dir.x);
                pos += (Vector3)(perp * ((i - (shots - 1) * 0.5f) * spawnOffset));
            }
            SpawnBullet(pos, dir, bt);
        }
    }

    private IEnumerator FireMultiDelayed(Vector3 firePos, Vector2 fireDir, int shots, float half, EnemyData.BulletType bt, float spawnOffset, float launchDelay)
    {
        for (int i = 0; i < shots; i++)
        {
            float ang   = (half > 0.0001f) ? Random.Range(-half, half) : 0f;
            Vector2 dir = RotateVec(fireDir, ang);
            if (dir.sqrMagnitude <= 0.0001f) dir = fireDir;

            Vector3 pos = firePos;
            if (spawnOffset > 0.0001f && shots > 1)
            {
                Vector2 perp = new Vector2(-dir.y, dir.x);
                pos += (Vector3)(perp * ((i - (shots - 1) * 0.5f) * spawnOffset));
            }
            SpawnBullet(pos, dir, bt);

            if (i < shots - 1)
                yield return WaitScaled(launchDelay);
        }
    }

    private static Vector2 RotateVec(Vector2 v, float angleDeg)
    {
        float rad = angleDeg * Mathf.Deg2Rad;
        float cos = Mathf.Cos(rad);
        float sin = Mathf.Sin(rad);
        return new Vector2(v.x * cos - v.y * sin, v.x * sin + v.y * cos);
    }

    // ------------------------------------------------------
    // BulletFiringRoutine選択ロジック（EnemyShooter無効化時の正規コピー方式）
    // ------------------------------------------------------

    private EnemyData.BulletType PickBulletType()
    {
        if (_enemyData == null || _enemyData.bulletTypes == null || _enemyData.bulletTypes.Length == 0)
            return null;

        EnemyData.BulletFiringRoutine routine = GetActiveBulletRoutine();
        if (routine != null &&
            routine.routineType == EnemyData.BulletFiringRoutine.RoutineType.Probability &&
            routine.probabilityEntries != null && routine.probabilityEntries.Length > 0)
        {
            int idx = PickProbabilityBulletIndex(routine);
            if (idx >= 0 && idx < _enemyData.bulletTypes.Length)
                return _enemyData.bulletTypes[idx];
        }
        return _enemyData.bulletTypes[0];
    }

    private EnemyData.BulletFiringRoutine GetActiveBulletRoutine()
    {
        if (_enemyData == null || _enemyData.bulletFiringRoutines == null) return null;
        bool isLowHp = _enemyData.useHpBasedRoutineSwitch &&
                       enemyStats != null &&
                       enemyStats.GetHpPercentage() <= _enemyData.hpThresholdPercentage;
        int routineIndex = isLowHp
            ? (int)_enemyData.bulletRoutineBelowThreshold
            : (int)_enemyData.bulletRoutineAboveThreshold;
        if (routineIndex < 0 || routineIndex >= _enemyData.bulletFiringRoutines.Length) return null;
        return _enemyData.bulletFiringRoutines[routineIndex];
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
                return Mathf.Clamp(e.bulletTypeIndex, 0, _enemyData.bulletTypes.Length - 1);
        }
        var last = routine.probabilityEntries[routine.probabilityEntries.Length - 1];
        return Mathf.Clamp(last.bulletTypeIndex, 0, _enemyData.bulletTypes.Length - 1);
    }

    // ------------------------------------------------------
    // Bullet spawn helper
    // ------------------------------------------------------

    private void SpawnBullet(Vector3 pos, Vector2 dir, EnemyData.BulletType bt)
    {
        if (FloorHealth.IsBrokenGlobal || PixelDancerController.IsPlayerDeadGlobal) return;
        if (bulletPrefab == null || projectileRoot == null) return;

        EnemyBullet bullet = Instantiate(bulletPrefab, pos, Quaternion.identity, projectileRoot);
        bullet.SetDirection(dir);

        if (bt != null)
            EnemyShooter.ApplyBulletTypeToEnemyBullet(bullet, bt, bulletSpeed, bulletLifeTime, null, bulletPrefab, projectileRoot);
        else
            bullet.ApplyBullet(bulletSpeed, bulletLifeTime);

        foreach (Collider2D col in GetComponentsInChildren<Collider2D>())
            if (col != null)
                bullet.SetOwnerCollisionIgnore(col, ignoreOwnerTime);
    }

    private EnemyData.BulletType GetBulletType(int index)
    {
        if (_enemyData == null || _enemyData.bulletTypes == null) return null;
        if (index < 0 || index >= _enemyData.bulletTypes.Length) return null;
        return _enemyData.bulletTypes[index];
    }

    // ======================================================
    // Frame apply
    // ======================================================

    private void ApplyFrame(GuardBeastFrame f)
    {
        if (f == null || spriteRenderer == null) return;
        if (f.sprite != null) spriteRenderer.sprite = f.sprite;
        ApplyOffset(f.offset);
        ApplyCollider(f);
        ApplyMuzzleOffset(f);
        ApplyRotation(spriteRenderer.transform, f.rotationZ, spriteRenderer.flipX);
    }

    private static void ApplyRotation(Transform t, float rotationZ, bool flip)
    {
        if (t == null) return;
        float z = flip ? -rotationZ : rotationZ;
        t.localRotation = Quaternion.Euler(0f, 0f, z);
    }

    private void ApplyOffset(Vector2 offset)
    {
        if (spriteRenderer == null) return;
        var t = spriteRenderer.transform;
        float x = spriteRenderer.flipX ? -offset.x : offset.x;
        Vector3 local = t.localPosition;
        t.localPosition = new Vector3(x, offset.y, local.z);
    }

    private void ApplyCollider(GuardBeastFrame frame)
    {
        if (bodyCollider == null || frame == null) return;
        Vector2 size = frame.colliderSize;
        Vector2 offset = frame.colliderOffset;
        if (spriteRenderer != null && spriteRenderer.flipX) offset.x = -offset.x;

        if (size.sqrMagnitude > 0.0001f)
            bodyCollider.size = size;
        bodyCollider.offset = offset;
    }

    private void ApplyMuzzleOffset(GuardBeastFrame frame)
    {
        if (firePointFace == null || frame == null) return;
        float x = (spriteRenderer != null && spriteRenderer.flipX) ? -frame.muzzleOffset.x : frame.muzzleOffset.x;
        firePointFace.localPosition = new Vector3(x, frame.muzzleOffset.y, firePointFace.localPosition.z);
    }

    private GuardBeastFrame GetIdleFrame(int index)
    {
        if (idleFrames == null || index < 0 || index >= idleFrames.Length) return null;
        return idleFrames[index];
    }

    private static float FrameDurationOr(GuardBeastFrame f, float fallback)
    {
        return (f != null && f.duration > 0f) ? f.duration : fallback;
    }

    // ======================================================
    // Editor Preview
    // ======================================================

    private void OnEnable()
    {
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

#if UNITY_EDITOR
    // OnValidateがネストした配列要素（Frame構造体のフィールド）の編集で
    // 確実に発火しないケースがあるため、静止プレビュー中は毎ティック再適用して確実に反映する
    private void OnEditorTickRefresh()
    {
        if (this == null || Application.isPlaying) return;
        if (_editorAnimRunning) return;
        if (previewSprite == GuardBeastPreviewSprite.ThunderPreview) return;
        if (spriteRenderer == null) return;

        var f = GetPreviewFrame(previewSprite);
        if (f == null) return;

        if (f.sprite != null) spriteRenderer.sprite = f.sprite;
        ApplyOffset(f.offset);
        ApplyCollider(f);
        ApplyMuzzleOffset(f);
        ApplyRotation(spriteRenderer.transform, f.rotationZ, spriteRenderer.flipX);
        if (bodyCollider != null)
            UnityEditor.EditorUtility.SetDirty(bodyCollider);
        UnityEditor.SceneView.RepaintAll();
    }

    [ContextMenu("Force Refresh Preview")]
    private void ForceRefreshPreview()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (spriteRenderer == null) return;

        var f = GetPreviewFrame(previewSprite);
        if (f == null) return;

        if (f.sprite != null) spriteRenderer.sprite = f.sprite;
        ApplyOffset(f.offset);
        ApplyCollider(f);
        ApplyMuzzleOffset(f);
        ApplyRotation(spriteRenderer.transform, f.rotationZ, spriteRenderer.flipX);

        UnityEditor.EditorUtility.SetDirty(bodyCollider);
        UnityEditor.SceneView.RepaintAll();
        Debug.Log($"[GuardBeastController] Force Refresh Preview: colliderSize={f.colliderSize}, colliderOffset={f.colliderOffset}, bodyCollider.size={bodyCollider?.size}, bodyCollider.offset={bodyCollider?.offset}");
    }
#endif

    private void OnValidate()
    {
        if (Application.isPlaying) return;
#if UNITY_EDITOR
        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (spriteRenderer == null) return;

        if (previewSprite == GuardBeastPreviewSprite.ThunderPreview)
        {
            StopEditorAnim();
            UnityEditor.SceneView.RepaintAll();
            return;
        }

        bool isAnim = previewSprite == GuardBeastPreviewSprite.IdleAnimate    ||
                      previewSprite == GuardBeastPreviewSprite.BoundAnimate   ||
                      previewSprite == GuardBeastPreviewSprite.Attack1Animate ||
                      previewSprite == GuardBeastPreviewSprite.Attack2Animate ||
                      previewSprite == GuardBeastPreviewSprite.Attack3Animate ||
                      previewSprite == GuardBeastPreviewSprite.ClawMarkAnimate;

        if (isAnim)
        {
            int animType = previewSprite == GuardBeastPreviewSprite.IdleAnimate    ? 0
                         : previewSprite == GuardBeastPreviewSprite.BoundAnimate   ? 1
                         : previewSprite == GuardBeastPreviewSprite.Attack1Animate ? 2
                         : previewSprite == GuardBeastPreviewSprite.Attack2Animate ? 3
                         : previewSprite == GuardBeastPreviewSprite.Attack3Animate ? 4 : 5;
            if (_editorAnimRunning && _editorAnimType != animType) StopEditorAnim();
            StartEditorAnim(animType);
        }
        else
        {
            StopEditorAnim();
            var f = GetPreviewFrame(previewSprite);
            if (f != null)
            {
                if (f.sprite != null) spriteRenderer.sprite = f.sprite;
                ApplyOffset(f.offset);
                ApplyCollider(f);
                ApplyMuzzleOffset(f);
                ApplyRotation(spriteRenderer.transform, f.rotationZ, spriteRenderer.flipX);
            }
        }

        UnityEditor.SceneView.RepaintAll();
#endif
    }

#if UNITY_EDITOR
    private bool   _editorAnimRunning;
    private double _editorAnimLastTime;
    private int    _editorAnimFrameIdx;
    private int    _editorAnimType; // 0=Idle,1=Bound,2=Attack1,3=Attack2,4=Attack3,5=ClawMark

    private void StartEditorAnim(int animType)
    {
        _editorAnimType     = animType;
        _editorAnimFrameIdx = 0;
        _editorAnimLastTime = UnityEditor.EditorApplication.timeSinceStartup;
        if (!_editorAnimRunning)
        {
            _editorAnimRunning = true;
            UnityEditor.EditorApplication.update += OnEditorUpdate;
        }
        var frames = GetEditorAnimFrames();
        if (frames != null && frames.Length > 0 && frames[0] != null && spriteRenderer != null)
        {
            spriteRenderer.sprite = frames[0].sprite;
            ApplyOffset(frames[0].offset);
            ApplyCollider(frames[0]);
            ApplyMuzzleOffset(frames[0]);
            ApplyRotation(spriteRenderer.transform, frames[0].rotationZ, spriteRenderer.flipX);
        }
    }

    private void StopEditorAnim()
    {
        if (!_editorAnimRunning) return;
        _editorAnimRunning = false;
        UnityEditor.EditorApplication.update -= OnEditorUpdate;
    }

    private GuardBeastFrame[] GetEditorAnimFrames()
    {
        switch (_editorAnimType)
        {
            case 0: return idleFrames;
            case 1: return boundFrames;
            case 2: return attack1Frames;
            case 3: return attack2Frames;
            case 4: return attack3Frames;
            default: return clawMarkFrames;
        }
    }

    private void OnEditorUpdate()
    {
        if (this == null || !_editorAnimRunning) { StopEditorAnim(); return; }

        var frames = GetEditorAnimFrames();
        if (frames == null || frames.Length == 0) { StopEditorAnim(); return; }

        var current = frames[_editorAnimFrameIdx % frames.Length];
        double dur = (current != null && current.duration > 0f) ? current.duration : 0.15;
        double now = UnityEditor.EditorApplication.timeSinceStartup;
        if (now - _editorAnimLastTime >= dur)
        {
            _editorAnimLastTime = now;
            _editorAnimFrameIdx = (_editorAnimFrameIdx + 1) % frames.Length;
            var next = frames[_editorAnimFrameIdx];
            if (next != null && next.sprite != null && spriteRenderer != null)
            {
                spriteRenderer.sprite = next.sprite;
                ApplyOffset(next.offset);
                ApplyCollider(next);
                ApplyMuzzleOffset(next);
                ApplyRotation(spriteRenderer.transform, next.rotationZ, spriteRenderer.flipX);
            }
            UnityEditor.SceneView.RepaintAll();
        }
    }

    private void OnPlayModeStateChanged(UnityEditor.PlayModeStateChange state)
    {
        if (state == UnityEditor.PlayModeStateChange.ExitingEditMode)
            StopEditorAnim();
    }

    private void OnDrawGizmosSelected()
    {
        if (Application.isPlaying) return;

        // firePointFace（muzzleOffset反映先）の現在位置を常に表示
        if (firePointFace != null)
        {
            Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.9f);
            Gizmos.DrawSphere(firePointFace.position, 0.06f);
            var labelStyle = new GUIStyle();
            labelStyle.normal.textColor = Color.red;
            UnityEditor.Handles.Label(firePointFace.position + Vector3.up * 0.15f, "FirePoint_Face", labelStyle);
        }

        // Collider preview（現在のプレビューフレームの当たり判定を自前でGizmo描画する。
        // Unity標準のCollider2Dギズモはキャッシュが更新されないことがあるため、
        // ShamanControllerと同様にフレームデータから直接毎回描画する）
        if (previewSprite != GuardBeastPreviewSprite.ThunderPreview)
        {
            var cf = GetPreviewFrame(previewSprite);
            if (cf != null && cf.colliderSize.sqrMagnitude > 0.0001f)
            {
                Vector3 bodyPos = transform.position + new Vector3(cf.offset.x, cf.offset.y, 0f);
                Vector3 colliderCenter = bodyPos + new Vector3(cf.colliderOffset.x, cf.colliderOffset.y, 0f);
                Gizmos.color = new Color(0.15f, 0.95f, 0.15f, 0.9f);
                Gizmos.DrawWireCube(colliderCenter, new Vector3(cf.colliderSize.x, cf.colliderSize.y, 0.05f));
            }
        }

        if (previewSprite != GuardBeastPreviewSprite.ThunderPreview) return;

        Vector3 pos = new Vector3(thunderCloudPosition.x, thunderCloudPosition.y, transform.position.z);
        Gizmos.color = new Color(0.6f, 0.6f, 1f, 0.9f);
        Gizmos.DrawWireSphere(pos, 0.5f);
        UnityEditor.Handles.Label(pos + Vector3.up * 0.6f, $"ThunderCloud ({thunderCloudPosition.x:F2}, {thunderCloudPosition.y:F2})");
    }
#endif

    private GuardBeastFrame GetPreviewFrame(GuardBeastPreviewSprite ps)
    {
        switch (ps)
        {
            case GuardBeastPreviewSprite.Idle1: return GetIdleFrame(0);
            case GuardBeastPreviewSprite.Idle2: return GetIdleFrame(1);
            case GuardBeastPreviewSprite.Idle3: return GetIdleFrame(2);
            case GuardBeastPreviewSprite.Idle4: return GetIdleFrame(3);

            case GuardBeastPreviewSprite.Bound1: return GetArrayFrame(boundFrames, 0);
            case GuardBeastPreviewSprite.Bound2: return GetArrayFrame(boundFrames, 1);
            case GuardBeastPreviewSprite.Bound3: return GetArrayFrame(boundFrames, 2);
            case GuardBeastPreviewSprite.Bound4: return GetArrayFrame(boundFrames, 3);
            case GuardBeastPreviewSprite.Bound5: return GetArrayFrame(boundFrames, 4);
            case GuardBeastPreviewSprite.Bound6: return GetArrayFrame(boundFrames, 5);
            case GuardBeastPreviewSprite.Bound7: return GetArrayFrame(boundFrames, 6);
            case GuardBeastPreviewSprite.Bound8: return GetArrayFrame(boundFrames, 7);
            case GuardBeastPreviewSprite.Bound9: return GetArrayFrame(boundFrames, 8);
            case GuardBeastPreviewSprite.Bound10: return GetArrayFrame(boundFrames, 9);

            case GuardBeastPreviewSprite.Attack1_1: return GetArrayFrame(attack1Frames, 0);
            case GuardBeastPreviewSprite.Attack1_2: return GetArrayFrame(attack1Frames, 1);
            case GuardBeastPreviewSprite.Attack1_3: return GetArrayFrame(attack1Frames, 2);

            case GuardBeastPreviewSprite.Attack2_1: return GetArrayFrame(attack2Frames, 0);
            case GuardBeastPreviewSprite.Attack2_2: return GetArrayFrame(attack2Frames, 1);
            case GuardBeastPreviewSprite.Attack2_3: return GetArrayFrame(attack2Frames, 2);

            case GuardBeastPreviewSprite.Attack3_1: return GetArrayFrame(attack3Frames, 0);
            case GuardBeastPreviewSprite.Attack3_2: return GetArrayFrame(attack3Frames, 1);
            case GuardBeastPreviewSprite.Attack3_3: return GetArrayFrame(attack3Frames, 2);

            case GuardBeastPreviewSprite.ClawMark1: return GetArrayFrame(clawMarkFrames, 0);
            case GuardBeastPreviewSprite.ClawMark2: return GetArrayFrame(clawMarkFrames, 1);
            case GuardBeastPreviewSprite.ClawMark3: return GetArrayFrame(clawMarkFrames, 2);
            case GuardBeastPreviewSprite.ClawMark4: return GetArrayFrame(clawMarkFrames, 3);

            default: return null;
        }
    }

    private static GuardBeastFrame GetArrayFrame(GuardBeastFrame[] arr, int index)
    {
        if (arr == null || index < 0 || index >= arr.Length) return null;
        return arr[index];
    }
}
