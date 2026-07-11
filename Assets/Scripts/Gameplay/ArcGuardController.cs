using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ArcGuardController : MonoBehaviour
{
    public enum ArcGuardPreviewSprite
    {
        Idle1, Idle2, Idle3, Idle4, IdleAnimate,
        Slide1, Slide2, Slide3, Slide4, Slide5, Slide6, Slide7, Slide8, Slide9, SlideAnimate,
        JumpWindup1, JumpWindup2, JumpWindup3,
        JumpAir1, JumpAir2,
        JumpFall1, JumpFall2, JumpFall3,
        JumpLanding1, JumpLanding2, JumpLanding3, JumpAnimate,
        Claw2H_1, Claw2H_2, Claw2H_3, Claw2H_4, Claw2H_5, Claw2H_6, Claw2H_7, Claw2HAnimate,
        Claw1HLeft_1, Claw1HLeft_2, Claw1HLeft_3, Claw1HLeft_4, Claw1HLeft_5, Claw1HLeft_6, Claw1HLeftAnimate,
        Claw1HRight_1, Claw1HRight_2, Claw1HRight_3, Claw1HRightAnimate,
        ClawMark1, ClawMark2, ClawMark3, ClawMark4, ClawMarkAnimate,
        Roar1, Roar2, Roar3, RoarAnimate
    }

    [System.Serializable]
    public class ArcGuardFrame
    {
        public Sprite  sprite;
        public Vector2 offset;
        [Tooltip("表示秒数")]
        public float   duration;
        [Tooltip("マズル位置（firePointFaceのローカル座標。本体弾を発射するフレームのみ有効）")]
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
    [Tooltip("尾の子オブジェクトにアタッチしたコンポーネント（未指定なら子から自動検索）")]
    [SerializeField] private ArcGuardTailHealth   tailHealth;
    [SerializeField] private ArcGuardTailAnimator tailAnimator;

    [Header("Editor Preview")]
    [SerializeField] private ArcGuardPreviewSprite previewSprite = ArcGuardPreviewSprite.Idle1;

    [Header("Sprites - Idle")]
    [NonReorderable]
    [SerializeField] private ArcGuardFrame[] idleFrames;

    [Header("Sprites - Slide（溜め→バースト移動→着地余韻を1配列で）")]
    [NonReorderable]
    [SerializeField] private ArcGuardFrame[] slideFrames;

    [Header("Sprites - Jump（滞空2枚＋落下3枚のみ。溜め/着地硬直はslideFramesを流用）")]
    [NonReorderable]
    [SerializeField] private ArcGuardFrame[] jumpFrames;

    [Header("Sprites - Claw Two-Hand（ジャンプ着地後、flipX使用）")]
    [NonReorderable]
    [SerializeField] private ArcGuardFrame[] claw2HFrames;

    [Header("Sprites - Claw One-Hand Left（SP04/07着地時。flipX不使用・専用素材）")]
    [NonReorderable]
    [SerializeField] private ArcGuardFrame[] claw1HLeftFrames;

    [Header("Sprites - Claw One-Hand Right（SP06/09着地時。flipX不使用・専用素材）")]
    [NonReorderable]
    [SerializeField] private ArcGuardFrame[] claw1HRightFrames;

    [Header("Sprites - Roar（後半フェーズ限定、flipX使用）")]
    [NonReorderable]
    [SerializeField] private ArcGuardFrame[] roarFrames;

    [Header("Slide")]
    [Tooltip("溜め/着地 各コマの表示秒数（フレームのDurationが0の場合のフォールバック）")]
    [SerializeField] private float slidePoseFrameDuration = 0.08f;
    [SerializeField] private float slideMoveDurationOneStep = 0.3f;
    [SerializeField] private float slideMoveDurationTwoStep = 0.45f;
    [Tooltip("移動後、次の行動までのIdle待機秒数（最小/最大）")]
    [SerializeField] private float slideIdleDurationMin = 0.4f;
    [SerializeField] private float slideIdleDurationMax = 0.8f;
    [Tooltip("2マス移動の抽選確率（後半フェーズのみ有効、0〜1）")]
    [SerializeField] private float twoStepChance = 0.4f;
    [Tooltip("Idleを挟まず連続移動する確率（後半フェーズのみ、0〜1）")]
    [SerializeField] private float chainMoveChanceBackPhase = 0.25f;
    [Tooltip("着地余韻フェーズが移動する距離の割合（0〜1）。バースト移動はこの分を差し引いた距離で目標手前まで進み、残りを着地余韻中に中→低速で減速しながら詰める")]
    [SerializeField] private float slideLandingDistanceRatio = 0.15f;
    [Tooltip("着地余韻フェーズの所要時間（秒）")]
    [SerializeField] private float slideLandingDuration = 0.3f;
    [Tooltip("着地余韻中の速度カーブ。0=フェーズ開始(中速)、1=フェーズ終了(低速で目標地点に収束)")]
    [SerializeField] private AnimationCurve slideLandingSpeedCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Jump")]
    [Tooltip("スライドの代わりにジャンプする確率（全フェーズ、0〜1）")]
    [SerializeField] private float jumpChance = 0.25f;
    [Tooltip("後半フェーズのみ: 2マス分離れた列へジャンプする確率（0=常に隣接列のみ、0〜1）。前半フェーズは常に隣接列のみ")]
    [SerializeField] private float jumpTwoStepChance = 0.4f;
    [SerializeField] private float jumpMoveDuration = 0.35f;
    [Tooltip("中間地点までの瞬間移動が全体のどの割合を占めるか（0〜1）。残りを落下区間に使う")]
    [SerializeField] private float jumpHopRatio = 0.25f;
    [Tooltip("滞空〜落下のjumpFramesでDurationが未設定(0)の場合のフォールバック秒数（短くしてポンと弾けるように見せる）")]
    [SerializeField] private float jumpAirFrameDuration = 0.05f;

    [Header("Claw Attack")]
    [Tooltip("スライド着地先が画面端(SP04/06/07/09)の時、片手爪が発生する確率（0〜1、スライダーではなく直接数値入力）")]
    [SerializeField] private float claw1HChance = 0.35f;
    [SerializeField] private float clawPoseFrameDuration = 0.1f;
    [SerializeField] private int   claw2HBulletTypeIndex = 2;
    [SerializeField] private int   claw1HBulletTypeIndex = 2;
    [Tooltip("claw1HLeftFramesの何コマ目（0始まり）を表示した時点で爪痕・弾を出し始めるか。配列の長さ以上にすると従来通りポーズ終了後に発射")]
    [SerializeField] private int   claw1HFireTriggerFrame = 2;
    [Tooltip("claw2HFramesの何コマ目（0始まり）を表示した時点で爪痕・弾を出し始めるか。配列の長さ以上にすると従来通りポーズ終了後に発射")]
    [SerializeField] private int   claw2HFireTriggerFrame = 2;
    [Tooltip("Floorオブジェクトへの参照。未設定の場合はシーンからFloorHealthコンポーネントで自動検索する（両手爪の弾がFloor上のランダム位置を1発ずつ狙う用）")]
    [SerializeField] private FloorHealth floorObject;

    [Header("Claw Mark Effect（GuardBeastのClawMark1〜4スプライトを流用）")]
    [Tooltip("爪痕エフェクト。GuardBeastのClawMark1〜4スプライトをそのまま流用する想定")]
    [NonReorderable]
    [SerializeField] private ArcGuardFrame[] clawMarkFrames;
    [Tooltip("爪痕コマ送りの表示秒数（各フレームのMuzzle Offset位置から1発ずつ発射）")]
    [SerializeField] private float clawMarkFrameDuration = 0.05f;

    [Header("Claw Mark Position Preview（Play前に位置確認用）")]
    [Tooltip("ONにすると、選択中のClawMarkFrameを本体に重ねてPlay前のSceneに表示する（生データは右向きデフォルトなので、左手用にflipして表示する）")]
    [SerializeField] private bool showClawMarkPreview = false;
    [Tooltip("プレビューするclawMarkFramesのインデックス（0始まり）")]
    [SerializeField] private int  clawMarkPreviewIndex = 0;

    [Header("Roar（後半フェーズ限定）")]
    [SerializeField] private int   roarBulletCount = 16;
    [SerializeField] private int   roarBulletTypeIndex = 0;
    [SerializeField] private float roarPoseFrameDuration = 0.15f;

    [Header("Body Bullet（移動中のみ発生・専用Attackスプライト無し）")]
    [SerializeField] private bool useBodyBulletDuringMove = true;

    [Header("Phase Transition / Back Phase Special")]
    [Tooltip("後半フェーズへ切り替わるHP%（1〜99）")]
    [SerializeField] private float phaseTransitionHpThreshold = 70f;
    [Tooltip("この回数の移動（スライド/ジャンプ）ごとに、通常Idleの代わりに咆哮/尾薙ぎ払いを抽選（後半フェーズのみ）")]
    [SerializeField] private int   movesPerSpecialTrigger = 6;
    [SerializeField] private float roarProbabilityBack = 50f;
    [SerializeField] private float tailSweepProbabilityBack = 50f;

    // ======================================================
    // Grid adjacency
    // SP01-09を index 0-8（行優先）。Row0(0,1,2)=ジャンプゾーン、Row1(3,4,5)/Row2(6,7,8)=スライドゾーン
    // ======================================================

    private static readonly int[][] slideOneStep = new int[][]
    {
        null,          // 0 SP01 (ジャンプゾーンなので未使用)
        null,          // 1 SP02
        null,          // 2 SP03
        new int[]{4,7},  // 3 SP04
        new int[]{3,5,6,8}, // 4 SP05
        new int[]{4,7},  // 5 SP06
        new int[]{7,4},  // 6 SP07
        new int[]{6,8,3,5}, // 7 SP08
        new int[]{7,4},  // 8 SP09
    };

    private static readonly int[][] slideTwoStep = new int[][]
    {
        null, null, null,
        new int[]{5},  // 3 SP04 → SP06
        null,           // 4 SP05
        new int[]{3},  // 5 SP06 → SP04
        new int[]{8},  // 6 SP07 → SP09
        null,           // 7 SP08
        new int[]{6},  // 8 SP09 → SP07
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
    private int  _cameFromGridIdx = -1;
    private bool _isMoving;
    private int  _moveCount;

    private Coroutine _mainLoopCoroutine;
    private Coroutine _bodyBulletCoroutine;

    private readonly List<GameObject> activeClawMarkVisuals = new List<GameObject>();

    // ======================================================
    // Lifecycle
    // ======================================================

    private void Awake()
    {
        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (enemyMover == null)     enemyMover     = GetComponent<EnemyMover>();
        if (enemyStats == null)     enemyStats     = GetComponent<EnemyStats>();
        if (enemyShooter == null)   enemyShooter   = GetComponent<EnemyShooter>();
        if (spriteSwapper == null)  spriteSwapper  = GetComponent<EnemySpriteSwapper>();
        if (tailHealth == null)     tailHealth     = GetComponentInChildren<ArcGuardTailHealth>();
        if (tailAnimator == null)   tailAnimator   = GetComponentInChildren<ArcGuardTailAnimator>();

        if (enemyMover != null)
            enemyMover.suppressMovement = true;

        if (enemyShooter != null)
            enemyShooter.enabled = false;

        if (tailHealth != null)
        {
            tailHealth.OnTailBroken   += HandleTailBroken;
            tailHealth.OnTailRestored += HandleTailRestored;
        }
    }

    private void OnDestroy()
    {
        isDead = true;
        if (tailHealth != null)
        {
            tailHealth.OnTailBroken   -= HandleTailBroken;
            tailHealth.OnTailRestored -= HandleTailRestored;
        }
        foreach (var go in activeClawMarkVisuals)
            if (go != null) Destroy(go);
        activeClawMarkVisuals.Clear();
    }

    private void Start()
    {
        if (!Application.isPlaying) return;

        if (enemyShooter != null)
        {
            _enemyData = enemyShooter.GetEnemyData();
            if (bulletPrefab == null)   bulletPrefab   = enemyShooter.GetBulletPrefab();
            if (projectileRoot == null) projectileRoot = enemyShooter.GetProjectileRoot();
        }

        if (tailAnimator != null)
            tailAnimator.Initialize(_enemyData, SpawnBullet);

        _spawner = FindFirstObjectByType<EnemySpawner>();
        _currentGridIdx = FindNearestSlideZoneIndex();

        ApplyFrame(GetIdleFrame(0));
        _mainLoopCoroutine   = StartCoroutine(MainLoop());
        _bodyBulletCoroutine = StartCoroutine(BodyBulletLoop());
    }

    private void Update()
    {
        if (!Application.isPlaying) return;
        CheckPhaseTransition();
    }

    private int FindNearestSlideZoneIndex()
    {
        if (_spawner == null) return 4;
        float minDist = float.MaxValue;
        int best = 4;
        foreach (int i in new[] { 3, 4, 5, 6, 7, 8 })
        {
            Transform sp = _spawner.GetSpawnPoint(i);
            if (sp == null) continue;
            float dist = Vector3.Distance(transform.position, sp.position);
            if (dist < minDist) { minDist = dist; best = i; }
        }
        return best;
    }

    private void HandleTailBroken()
    {
        if (tailAnimator != null) tailAnimator.SetMode(ArcGuardTailAnimator.TailMode.Frozen);
    }

    private void HandleTailRestored()
    {
        // 復活した瞬間、本体が今どの状態（Idle/スライド中か、ジャンプ/爪/咆哮中か）にいるかに応じて
        // 正しいモードへ戻す。ジャンプ/爪/咆哮の最中に復活した場合はそのままFrozenを維持する。
        SetNonTailState(_inNonTailState);
    }

    // 現在ジャンプ/爪攻撃/咆哮など「尾Idle非表示」の特殊状態にいるかどうか
    private bool _inNonTailState;

    private void SetNonTailState(bool value)
    {
        _inNonTailState = value;
        if (tailAnimator == null) return;
        if (tailHealth != null && tailHealth.IsBroken) { tailAnimator.SetMode(ArcGuardTailAnimator.TailMode.Frozen); return; }
        tailAnimator.SetMode(value ? ArcGuardTailAnimator.TailMode.Frozen : ArcGuardTailAnimator.TailMode.IdleSway);
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
            Debug.Log($"[ArcGuardController] Phase→Back HP%={enemyStats.GetHpPercentage():F1} threshold={phaseTransitionHpThreshold} frame={Time.frameCount}", this);
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
    // Idle呼吸ループ
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
            ArcGuardFrame f = idleFrames[idx % idleFrames.Length];
            ApplyFrame(f);
            float frameDur = (f != null && f.duration > 0f) ? f.duration : 0.15f;
            frameDur = Mathf.Min(frameDur, totalDuration - elapsed);
            yield return WaitScaled(frameDur);
            elapsed += frameDur;
            idx++;
        }
    }

    // ======================================================
    // Main Loop（スライド/ジャンプ → 爪/特殊技トリガー）
    // ======================================================

    private IEnumerator MainLoop()
    {
        while (true)
        {
            bool doJump = Random.value < jumpChance;

            if (doJump)
            {
                yield return StartCoroutine(JumpOnce());
                _moveCount++;
                yield return StartCoroutine(ClawTwoHandRoutine());
                continue; // ジャンプ後は必ずスライド/ジャンプの分岐へ戻る（Idleを挟まない）
            }

            yield return StartCoroutine(SlideOnce());
            _moveCount++;

            bool isEdge = IsEdgeColumn(_currentGridIdx);
            bool didClaw1H = false;
            if (isEdge)
            {
                float roll = Random.value;
                bool success = roll < claw1HChance;
                Debug.Log($"[ArcGuardController] Claw1H roll SP{_currentGridIdx + 1:D2} roll={roll:F2} chance={claw1HChance} success={success} frame={Time.frameCount}", this);
                if (success)
                {
                    didClaw1H = true;
                    yield return StartCoroutine(ClawOneHandRoutine(IsLeftEdge(_currentGridIdx)));
                }
            }

            if (didClaw1H) continue;

            if (_phase == Phase.Back && movesPerSpecialTrigger > 0 && _moveCount % movesPerSpecialTrigger == 0)
            {
                yield return StartCoroutine(PlayBackPhaseSpecial());
                continue;
            }

            bool chain = _phase == Phase.Back && Random.value < chainMoveChanceBackPhase;
            if (!chain)
            {
                float idleDur = Random.Range(slideIdleDurationMin, slideIdleDurationMax);
                yield return StartCoroutine(PlayIdleLoop(idleDur));
            }
        }
    }

    // ------------------------------------------------------
    // Slide
    // ------------------------------------------------------

    private IEnumerator SlideOnce()
    {
        int nextIdx = PickNextSlideIndex(_currentGridIdx, _cameFromGridIdx, out bool isTwoStep);
        Transform sp = _spawner != null ? _spawner.GetSpawnPoint(nextIdx) : null;
        Vector3 targetPos = sp != null ? sp.position : transform.position;
        float moveDuration = isTwoStep ? slideMoveDurationTwoStep : slideMoveDurationOneStep;

        UpdateFacing(targetPos);
        SetNonTailState(false); // スライド中は尾Idle表示

        int total = slideFrames != null ? slideFrames.Length : 0;
        int windupCount = total / 3;
        int landingCount = total / 3;
        int burstStart = windupCount;
        int burstCount = Mathf.Max(0, total - windupCount - landingCount);

        // 溜め
        for (int i = 0; i < windupCount; i++)
        {
            ApplyFrame(slideFrames[i]);
            yield return WaitScaled(FrameDurationOr(slideFrames[i], slidePoseFrameDuration));
        }

        // バースト移動（高速・一定）: 着地余韻分の距離を残して目標手前まで進む
        // ※移動の進み具合(position)とフレーム切替(各Frameの Duration)は別タイマーで独立管理する
        Vector3 startPos = transform.position;
        Vector3 burstEndPos = Vector3.Lerp(startPos, targetPos, 1f - slideLandingDistanceRatio);
        float elapsed = 0f;
        int burstFrameIdx = burstCount > 0 ? burstStart : -1;
        float burstFrameTimer = 0f;
        if (burstFrameIdx >= 0) ApplyFrame(slideFrames[burstFrameIdx]);
        _isMoving = true;
        while (elapsed < moveDuration)
        {
            if (isDead) yield break;
            float dt = Time.deltaTime * GetTimeScale() * GetSpeedMul();
            elapsed += dt;
            float ratio = Mathf.Clamp01(elapsed / moveDuration);
            transform.position = Vector3.Lerp(startPos, burstEndPos, ratio);

            if (burstCount > 0 && burstFrameIdx < burstStart + burstCount - 1)
            {
                burstFrameTimer += dt;
                float frameDur = FrameDurationOr(slideFrames[burstFrameIdx], slidePoseFrameDuration);
                if (burstFrameTimer >= frameDur)
                {
                    burstFrameTimer -= frameDur;
                    burstFrameIdx++;
                    ApplyFrame(slideFrames[burstFrameIdx]);
                }
            }
            yield return null;
        }
        transform.position = burstEndPos;
        _cameFromGridIdx = _currentGridIdx;
        _currentGridIdx = nextIdx;

        // 着地余韻（隙）: 中→低速へ減速しながら残り距離を詰め、最後にぴったり目標地点へ収束
        int landingStart = burstStart + burstCount;
        int landingFrameIdx = landingCount > 0 ? landingStart : -1;
        float landingFrameTimer = 0f;
        if (landingFrameIdx >= 0) ApplyFrame(slideFrames[landingFrameIdx]);
        float landingElapsed = 0f;
        while (landingElapsed < slideLandingDuration)
        {
            if (isDead) yield break;
            float dt = Time.deltaTime * GetTimeScale() * GetSpeedMul();
            landingElapsed += dt;
            float ratio = Mathf.Clamp01(landingElapsed / slideLandingDuration);
            float curved = slideLandingSpeedCurve.Evaluate(ratio);
            transform.position = Vector3.Lerp(burstEndPos, targetPos, curved);

            if (landingCount > 0 && landingFrameIdx < landingStart + landingCount - 1)
            {
                landingFrameTimer += dt;
                float frameDur = FrameDurationOr(slideFrames[landingFrameIdx], slidePoseFrameDuration);
                if (landingFrameTimer >= frameDur)
                {
                    landingFrameTimer -= frameDur;
                    landingFrameIdx++;
                    ApplyFrame(slideFrames[landingFrameIdx]);
                }
            }
            yield return null;
        }
        _isMoving = false;
        transform.position = targetPos;
    }

    private int PickNextSlideIndex(int currentIdx, int cameFromIdx, out bool isTwoStep)
    {
        int[] twoStep = (currentIdx >= 0 && currentIdx < slideTwoStep.Length) ? slideTwoStep[currentIdx] : null;
        bool canTwoStep = _phase == Phase.Back && twoStep != null && twoStep.Length > 0;
        bool useTwoStep = canTwoStep && Random.value < twoStepChance;

        if (useTwoStep)
        {
            var candidates = FilterExcluding(twoStep, cameFromIdx);
            if (candidates.Count > 0)
            {
                isTwoStep = true;
                int picked = candidates[Random.Range(0, candidates.Count)];
                Debug.Log($"[ArcGuardController] 2-step slide SP{currentIdx + 1:D2}→SP{picked + 1:D2} phase={_phase} HP%={(enemyStats != null ? enemyStats.GetHpPercentage() : -1f):F1} frame={Time.frameCount}", this);
                return picked;
            }
        }

        isTwoStep = false;
        int[] oneStep = (currentIdx >= 0 && currentIdx < slideOneStep.Length) ? slideOneStep[currentIdx] : null;
        var oneStepCandidates = FilterExcluding(oneStep, cameFromIdx);
        if (oneStepCandidates.Count == 0) return currentIdx;
        return oneStepCandidates[Random.Range(0, oneStepCandidates.Count)];
    }

    private static System.Collections.Generic.List<int> FilterExcluding(int[] source, int exclude)
    {
        var list = new System.Collections.Generic.List<int>();
        if (source == null) return list;
        foreach (int v in source)
            if (v != exclude) list.Add(v);
        return list;
    }

    private bool IsEdgeColumn(int idx) => (idx % 3) == 0 || (idx % 3) == 2;
    private bool IsLeftEdge(int idx)   => (idx % 3) == 0;

    // ------------------------------------------------------
    // Jump
    // ------------------------------------------------------

    private IEnumerator JumpOnce()
    {
        int col = _currentGridIdx % 3;
        var allCandidates = new System.Collections.Generic.List<int> { 0, 1, 2 };
        allCandidates.Remove(col); // 真上を除外（ジャンプゾーンのindexは列番号そのもの）

        // 前半は隣接列(1マス分)のみ、後半は隣接列に加えて2マス分離れた列も確率で許可
        var oneAway = allCandidates.FindAll(c => Mathf.Abs(c - col) == 1);
        var twoAway = allCandidates.FindAll(c => Mathf.Abs(c - col) == 2);
        bool canTwoStep = _phase == Phase.Back && twoAway.Count > 0;
        bool useTwoStep = canTwoStep && Random.value < jumpTwoStepChance;
        var pickFrom = useTwoStep ? twoAway : oneAway;
        int targetIdx = pickFrom[Random.Range(0, pickFrom.Count)];
        Debug.Log($"[ArcGuardController] Jump SP{_currentGridIdx + 1:D2}→SP{targetIdx + 1:D2} phase={_phase} HP%={(enemyStats != null ? enemyStats.GetHpPercentage() : -1f):F1} frame={Time.frameCount}", this);

        Transform sp = _spawner != null ? _spawner.GetSpawnPoint(targetIdx) : null;
        Vector3 targetPos = sp != null ? sp.position : transform.position;

        UpdateFacing(targetPos);
        SetNonTailState(true); // ジャンプ中は尾Idle非表示

        // ①溜め: スライドの溜めフレームを流用
        int slideTotal = slideFrames != null ? slideFrames.Length : 0;
        int slideWindupCount = slideTotal / 3;
        int slideLandingCount = slideTotal / 3;
        for (int i = 0; i < slideWindupCount; i++)
        {
            ApplyFrame(slideFrames[i]);
            yield return WaitScaled(FrameDurationOr(slideFrames[i], slidePoseFrameDuration));
        }

        Vector3 startPos = transform.position;
        Vector3 midPos = Vector3.Lerp(startPos, targetPos, jumpHopRatio);

        // ②③滞空〜落下: 瞬間移動で中間地点へ切り替えた直後、止まらずそのまま連続移動しながらjumpFrames全体をコマ送り
        // （移動(position)とフレーム切替(各Frameの Duration)は別タイマーで独立管理）
        transform.position = midPos;
        int jumpTotal = jumpFrames != null ? jumpFrames.Length : 0;
        float fallDuration = Mathf.Max(0.05f, jumpMoveDuration * (1f - jumpHopRatio));
        float elapsed = 0f;
        int fallFrameIdx = jumpTotal > 0 ? 0 : -1;
        float fallFrameTimer = 0f;
        if (fallFrameIdx >= 0) ApplyFrame(jumpFrames[fallFrameIdx]);
        _isMoving = true;
        while (elapsed < fallDuration)
        {
            if (isDead) yield break;
            float dt = Time.deltaTime * GetTimeScale() * GetSpeedMul();
            elapsed += dt;
            float ratio = Mathf.Clamp01(elapsed / fallDuration);
            transform.position = Vector3.Lerp(midPos, targetPos, ratio);

            if (jumpTotal > 0 && fallFrameIdx < jumpTotal - 1)
            {
                fallFrameTimer += dt;
                float frameDur = FrameDurationOr(jumpFrames[fallFrameIdx], jumpAirFrameDuration);
                if (fallFrameTimer >= frameDur)
                {
                    fallFrameTimer -= frameDur;
                    fallFrameIdx++;
                    ApplyFrame(jumpFrames[fallFrameIdx]);
                }
            }
            yield return null;
        }
        _isMoving = false;
        transform.position = targetPos;
        _cameFromGridIdx = _currentGridIdx;
        _currentGridIdx = targetIdx;

        // ④着地硬直（隙）: スライドの着地余韻フレームを流用
        int slideLandingStart = slideTotal - slideLandingCount;
        for (int i = slideLandingStart; i < slideTotal; i++)
        {
            ApplyFrame(slideFrames[i]);
            yield return WaitScaled(FrameDurationOr(slideFrames[i], slidePoseFrameDuration));
        }
    }

    // ------------------------------------------------------
    // Claw
    // ------------------------------------------------------

    // ジャンプ着地後、必ず発生。同じ列の1つ前 or 2つ前のスライド地点へ移動しながら両手爪。
    private IEnumerator ClawTwoHandRoutine()
    {
        int col = _currentGridIdx % 3; // ジャンプ着地直後なので現在地はジャンプゾーン(0,1,2)
        int oneBack = col + 3;
        int twoBack = col + 6;
        int targetIdx = Random.value < 0.5f ? oneBack : twoBack;

        Transform sp = _spawner != null ? _spawner.GetSpawnPoint(targetIdx) : null;
        Vector3 targetPos = sp != null ? sp.position : transform.position;

        UpdateFacing(targetPos);
        SetNonTailState(true); // 爪攻撃中は尾Idle非表示・攻撃停止

        // 両手なので左右両方の爪痕を同時に展開する（1手=4発 x 2 = 8発）。移動と並行するためyieldしない。
        // 両手爪はプレイヤーを直接狙うと全弾が同じ位置に集中するため、Floor上のランダム位置を1発ずつ狙う。
        yield return StartCoroutine(PlayClawFrames(claw2HFrames, true, claw2HFireTriggerFrame, () =>
        {
            StartCoroutine(PlayClawMarkVisual(false, claw2HBulletTypeIndex, true));
            StartCoroutine(PlayClawMarkVisual(true, claw2HBulletTypeIndex, true));
        }));

        // 移動しながら攻撃継続
        Vector3 startPos = transform.position;
        float elapsed = 0f;
        _isMoving = true;
        while (elapsed < slideMoveDurationOneStep)
        {
            if (isDead) yield break;
            elapsed += Time.deltaTime * GetTimeScale() * GetSpeedMul();
            float ratio = Mathf.Clamp01(elapsed / slideMoveDurationOneStep);
            transform.position = Vector3.Lerp(startPos, targetPos, ratio);
            yield return null;
        }
        _isMoving = false;
        transform.position = targetPos;
        _cameFromGridIdx = _currentGridIdx;
        _currentGridIdx = targetIdx;
    }

    // スライド着地先が画面端の時、確率で発生。正面向きの左爪素材を、右側の時はflipXで反転して使う。
    private IEnumerator ClawOneHandRoutine(bool isLeft)
    {
        SetNonTailState(true);
        bool prevFlip = spriteRenderer != null && spriteRenderer.flipX;

        Coroutine markCo = null;
        yield return StartCoroutine(PlayClawFrames(claw1HLeftFrames, !isLeft, claw1HFireTriggerFrame,
            () => { markCo = StartCoroutine(PlayClawMarkVisual(isLeft, claw1HBulletTypeIndex, true)); }));
        if (markCo != null) yield return markCo;

        if (spriteRenderer != null) spriteRenderer.flipX = prevFlip;
        SetNonTailState(false);
    }

    // framesを再生しながら、fireTriggerFrameIndex番目のコマを表示した時点でonFireTriggerを呼ぶ
    // （それまでに達しなければ最後に必ず1回呼ぶ。frames が空でも呼ぶ）
    private IEnumerator PlayClawFrames(ArcGuardFrame[] frames, bool useFlip, int fireTriggerFrameIndex, System.Action onFireTrigger)
    {
        if (spriteRenderer != null) spriteRenderer.flipX = useFlip;
        bool fired = false;
        if (frames != null)
        {
            for (int i = 0; i < frames.Length; i++)
            {
                var f = frames[i];
                if (f == null) continue;
                ApplyFrame(f);
                if (!fired && i >= fireTriggerFrameIndex)
                {
                    fired = true;
                    onFireTrigger?.Invoke();
                }
                yield return WaitScaled(FrameDurationOr(f, clawPoseFrameDuration));
            }
        }
        if (!fired) onFireTrigger?.Invoke();
    }

    // 爪痕（Claw Mark）を1回だけ再生し、各フレームのMuzzle Offsetからそのフレームのタイミングで1発ずつ発射する
    // （GuardBeastController.ClawSwipeRoutineと同じ方式。GuardBeastのClawMark1〜4スプライトを流用する想定）
    // aimRandomFloor: trueの場合、プレイヤーではなくFloor上のランダム位置を1発ずつ狙う（両手爪用。全弾が同じ位置に集中しないようにする）
    private IEnumerator PlayClawMarkVisual(bool flip, int bulletTypeIndex, bool aimRandomFloor = false)
    {
        if (clawMarkFrames == null || clawMarkFrames.Length == 0) yield break;

        Vector3 basePos = firePointFace != null ? firePointFace.position : transform.position;

        GameObject go = new GameObject("ClawMarkVisual");
        go.transform.SetPositionAndRotation(basePos, Quaternion.identity);
        if (projectileRoot != null) go.transform.SetParent(projectileRoot, true);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.flipX = flip;
        if (spriteRenderer != null)
        {
            sr.sortingLayerID = spriteRenderer.sortingLayerID;
            sr.sortingOrder   = spriteRenderer.sortingOrder + 1; // 本体より確実に手前に描画する
        }

        activeClawMarkVisuals.Add(go);

        EnemyData.BulletType bt = GetBulletType(bulletTypeIndex);

        for (int i = 0; i < clawMarkFrames.Length; i++)
        {
            if (isDead || go == null) yield break;

            ArcGuardFrame frame = clawMarkFrames[i];
            if (frame == null) continue;

            sr.sprite = frame.sprite;
            float x = flip ? -frame.offset.x : frame.offset.x;
            go.transform.position = basePos + new Vector3(x, frame.offset.y, 0f);
            float z = flip ? -frame.rotationZ : frame.rotationZ;
            go.transform.rotation = Quaternion.Euler(0f, 0f, z);

            float mx = flip ? -frame.muzzleOffset.x : frame.muzzleOffset.x;
            Vector3 muzzlePos = basePos + new Vector3(mx, frame.muzzleOffset.y, 0f);

            if (bt != null)
            {
                Vector2 dir;
                if (aimRandomFloor)
                {
                    Vector3 targetPos = GetRandomFloorTargetPosition();
                    dir = ((Vector2)(targetPos - muzzlePos));
                    dir = (dir.sqrMagnitude > 0.0001f) ? dir.normalized : Vector2.down;
                }
                else
                {
                    GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
                    dir = playerObj != null
                        ? ((Vector2)(playerObj.transform.position - muzzlePos)).normalized
                        : Vector2.down;
                }
                SpawnBullet(muzzlePos, dir, bt);
            }

            yield return WaitScaled(FrameDurationOr(frame, clawMarkFrameDuration));
        }

        activeClawMarkVisuals.Remove(go);
        if (go != null) Destroy(go);
    }

    // ------------------------------------------------------
    // Back Phase Special（Roar / Tail Sweep）
    // ------------------------------------------------------

    private IEnumerator PlayBackPhaseSpecial()
    {
        // 尾が破壊中はTail Sweepを選ばない（壊れた尾で攻撃しない）
        bool tailAvailable = tailHealth == null || !tailHealth.IsBroken;
        if (!tailAvailable)
        {
            yield return StartCoroutine(RoarRoutine());
            yield break;
        }

        float total = Mathf.Max(0.0001f, roarProbabilityBack + tailSweepProbabilityBack);
        float r = Random.value * total;

        if (r <= roarProbabilityBack)
            yield return StartCoroutine(RoarRoutine());
        else
            yield return StartCoroutine(TailSweepRoutine());
    }

    private IEnumerator RoarRoutine()
    {
        SetNonTailState(true);

        if (roarFrames != null)
        {
            foreach (var f in roarFrames)
            {
                if (f == null) continue;
                ApplyFrame(f);
                yield return WaitScaled(FrameDurationOr(f, roarPoseFrameDuration));
            }
        }

        FireRoarRing();

        SetNonTailState(false);
    }

    private void FireRoarRing()
    {
        EnemyData.BulletType bt = GetBulletType(roarBulletTypeIndex);
        if (bt == null || bulletPrefab == null || projectileRoot == null || roarBulletCount <= 0) return;

        Vector3 origin = firePointFace != null ? firePointFace.position : transform.position;
        for (int i = 0; i < roarBulletCount; i++)
        {
            float angle = (360f / roarBulletCount) * i * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            SpawnBullet(origin, dir, bt);
        }
    }

    private IEnumerator TailSweepRoutine()
    {
        // 尾薙ぎ払い中は本体はIdleのまま、尾だけが専用アニメーションで動く。
        // モード切替自体はArcGuardTailAnimator.PlaySweepRoutine()が内部で行う（開始時に自動停止・終了時にIdleSwayへ復帰）ため、
        // ここでは他状態からの復帰判定用に _inNonTailState のブックキーピングだけ行う。
        if (tailAnimator == null) yield break;
        _inNonTailState = true;
        yield return StartCoroutine(tailAnimator.PlaySweepRoutine());
        _inNonTailState = false;
    }

    // ------------------------------------------------------
    // Body Bullet（移動中のみ）
    // ------------------------------------------------------

    private IEnumerator BodyBulletLoop()
    {
        while (true)
        {
            if (isDead) yield break;

            if (!useBodyBulletDuringMove || !_isMoving)
            {
                yield return null;
                continue;
            }

            EnemyData.BulletType firedType = FireBodyBullet();
            yield return WaitScaled(GetBodyBulletInterval(firedType));
        }
    }

    private float GetBodyBulletInterval(EnemyData.BulletType bt)
    {
        float interval = (_enemyData != null && _enemyData.fireInterval > 0f) ? _enemyData.fireInterval : 1.5f;
        if (bt != null && bt.useFireIntervalOverride && bt.fireIntervalOverride > 0f)
            interval = bt.fireIntervalOverride;
        return Mathf.Max(0.05f, interval);
    }

    private EnemyData.BulletType FireBodyBullet()
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

    private EnemyData.BulletType GetBulletType(int index)
    {
        if (_enemyData == null || _enemyData.bulletTypes == null) return null;
        if (index < 0 || index >= _enemyData.bulletTypes.Length) return null;
        return _enemyData.bulletTypes[index];
    }

    // ------------------------------------------------------
    // Bullet spawn helper
    // ------------------------------------------------------

    // Floor上のランダムなX位置を狙う（GyrorbController.GetRandomFloorTargetPositionと同じ方式）
    private Vector3 GetRandomFloorTargetPosition()
    {
        if (floorObject == null)
            floorObject = FindFirstObjectByType<FloorHealth>();

        if (floorObject != null)
        {
            Collider2D col = floorObject.GetComponent<Collider2D>();
            if (col != null)
            {
                float x = Random.Range(col.bounds.min.x, col.bounds.max.x);
                return new Vector3(x, col.bounds.max.y, 0f);
            }
            return floorObject.transform.position;
        }

        // Floor未検出時のフォールバック: 画面下端付近のランダムX
        float halfH = Camera.main.orthographicSize;
        float halfW = halfH * Camera.main.aspect;
        Vector3 camPos = Camera.main.transform.position;
        float fx = Random.Range(camPos.x - halfW, camPos.x + halfW);
        return new Vector3(fx, camPos.y - halfH, 0f);
    }

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

    // ======================================================
    // Frame apply
    // ======================================================

    private void UpdateFacing(Vector3 targetPos)
    {
        if (spriteRenderer == null) return;
        float dx = targetPos.x - transform.position.x;
        if (dx > 0.001f)       spriteRenderer.flipX = true;
        else if (dx < -0.001f) spriteRenderer.flipX = false;
        if (tailAnimator != null) tailAnimator.SetFlip(spriteRenderer.flipX);
    }

    private void ApplyFrame(ArcGuardFrame frame)
    {
        if (frame == null || spriteRenderer == null) return;
        if (frame.sprite != null)
        {
            spriteRenderer.sprite = frame.sprite;
            // EnemySpriteSwapperが被弾ヒットフラッシュ終了後に古いスプライトへ巻き戻すのを防ぐため、
            // 「通常スプライト」のキャッシュを常に最新のフレームへ同期させる
            if (spriteSwapper != null) spriteSwapper.SetBaseSprite(frame.sprite);
        }
        ApplyOffset(frame.offset);
        ApplyCollider(frame);
        ApplyMuzzleOffset(frame);
        ApplyRotation(spriteRenderer.transform, frame.rotationZ, spriteRenderer.flipX);
    }

    private static void ApplyRotation(Transform t, float rotationZ, bool flip)
    {
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

    private void ApplyCollider(ArcGuardFrame frame)
    {
        if (bodyCollider == null || frame == null) return;
        Vector2 size = frame.colliderSize;
        Vector2 offset = frame.colliderOffset;
        if (spriteRenderer != null && spriteRenderer.flipX) offset.x = -offset.x;

        if (size.sqrMagnitude > 0.0001f)
            bodyCollider.size = size;
        bodyCollider.offset = offset;
    }

    private void ApplyMuzzleOffset(ArcGuardFrame frame)
    {
        if (firePointFace == null || frame == null) return;
        float x = (spriteRenderer != null && spriteRenderer.flipX) ? -frame.muzzleOffset.x : frame.muzzleOffset.x;
        firePointFace.localPosition = new Vector3(x, frame.muzzleOffset.y, firePointFace.localPosition.z);
    }

    private ArcGuardFrame GetIdleFrame(int index)
    {
        if (idleFrames == null || index < 0 || index >= idleFrames.Length) return null;
        return idleFrames[index];
    }

    private static float FrameDurationOr(ArcGuardFrame f, float fallback)
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
        DestroyClawMarkPreview();
#endif
    }

#if UNITY_EDITOR
    private void OnEditorTickRefresh()
    {
        if (this == null || Application.isPlaying) return;
        if (_editorAnimRunning) return;
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
        UpdateClawMarkPreview();
        UnityEditor.SceneView.RepaintAll();
    }
#endif

    private void OnValidate()
    {
        if (Application.isPlaying) return;
#if UNITY_EDITOR
        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (spriteRenderer == null) return;

        bool isAnim = previewSprite == ArcGuardPreviewSprite.IdleAnimate      ||
                      previewSprite == ArcGuardPreviewSprite.SlideAnimate     ||
                      previewSprite == ArcGuardPreviewSprite.JumpAnimate      ||
                      previewSprite == ArcGuardPreviewSprite.Claw2HAnimate    ||
                      previewSprite == ArcGuardPreviewSprite.Claw1HLeftAnimate ||
                      previewSprite == ArcGuardPreviewSprite.Claw1HRightAnimate ||
                      previewSprite == ArcGuardPreviewSprite.ClawMarkAnimate ||
                      previewSprite == ArcGuardPreviewSprite.RoarAnimate;

        if (isAnim)
        {
            int animType = previewSprite == ArcGuardPreviewSprite.IdleAnimate      ? 0
                         : previewSprite == ArcGuardPreviewSprite.SlideAnimate     ? 1
                         : previewSprite == ArcGuardPreviewSprite.JumpAnimate      ? 2
                         : previewSprite == ArcGuardPreviewSprite.Claw2HAnimate    ? 3
                         : previewSprite == ArcGuardPreviewSprite.Claw1HLeftAnimate ? 4
                         : previewSprite == ArcGuardPreviewSprite.Claw1HRightAnimate ? 5
                         : previewSprite == ArcGuardPreviewSprite.ClawMarkAnimate ? 6 : 7;
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

        UpdateClawMarkPreview();
        UnityEditor.SceneView.RepaintAll();
#endif
    }

#if UNITY_EDITOR
    private GameObject    _clawMarkPreviewObj;
    private SpriteRenderer _clawMarkPreviewSr;

    // clawMarkFrames[clawMarkPreviewIndex]を、本体に重ねてPlay前のSceneに表示する（左手用にflip=true固定）
    private void UpdateClawMarkPreview()
    {
        if (!showClawMarkPreview || clawMarkFrames == null || clawMarkFrames.Length == 0)
        {
            if (_clawMarkPreviewObj != null) _clawMarkPreviewObj.SetActive(false);
            return;
        }

        if (_clawMarkPreviewObj == null)
        {
            _clawMarkPreviewObj = new GameObject("__ClawMarkPreview (Editor Only, not saved)");
            _clawMarkPreviewObj.hideFlags = HideFlags.DontSave;
            _clawMarkPreviewSr = _clawMarkPreviewObj.AddComponent<SpriteRenderer>();
        }

        int idx = Mathf.Clamp(clawMarkPreviewIndex, 0, clawMarkFrames.Length - 1);
        ArcGuardFrame frame = clawMarkFrames[idx];
        if (frame == null) { _clawMarkPreviewObj.SetActive(false); return; }

        _clawMarkPreviewObj.SetActive(true);
        _clawMarkPreviewObj.transform.SetParent(transform, false);

        const bool flip = true; // 左手（claw1HLeftFrames）用プレビュー固定。生データは右向きデフォルト
        _clawMarkPreviewSr.sprite = frame.sprite;
        _clawMarkPreviewSr.flipX = flip;
        if (spriteRenderer != null)
        {
            _clawMarkPreviewSr.sortingLayerID = spriteRenderer.sortingLayerID;
            _clawMarkPreviewSr.sortingOrder   = spriteRenderer.sortingOrder + 1;
        }

        Vector3 basePos = firePointFace != null ? firePointFace.position : transform.position;
        float x = flip ? -frame.offset.x : frame.offset.x;
        _clawMarkPreviewObj.transform.position = basePos + new Vector3(x, frame.offset.y, 0f);
        float z = flip ? -frame.rotationZ : frame.rotationZ;
        _clawMarkPreviewObj.transform.rotation = Quaternion.Euler(0f, 0f, z);
    }

    private void DestroyClawMarkPreview()
    {
        if (_clawMarkPreviewObj != null) DestroyImmediate(_clawMarkPreviewObj);
        _clawMarkPreviewObj = null;
        _clawMarkPreviewSr = null;
    }
#endif

#if UNITY_EDITOR
    private bool   _editorAnimRunning;
    private double _editorAnimLastTime;
    private int    _editorAnimFrameIdx;
    private int    _editorAnimType; // 0=Idle,1=Slide,2=Jump,3=Claw2H,4=Claw1HLeft,5=Claw1HRight,6=ClawMark,7=Roar

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

    private ArcGuardFrame[] GetEditorAnimFrames()
    {
        switch (_editorAnimType)
        {
            case 0: return idleFrames;
            case 1: return slideFrames;
            case 2: return GetJumpFullSequenceFrames();
            case 3: return claw2HFrames;
            case 4: return claw1HLeftFrames;
            case 5: return claw1HRightFrames;
            case 6: return clawMarkFrames;
            default: return roarFrames;
        }
    }

    // Jump用のAnimateプレビュー: 溜め(slideFrames流用)→滞空/落下(jumpFrames)→着地硬直(slideFrames流用)を1本に連結
    private ArcGuardFrame[] GetJumpFullSequenceFrames()
    {
        int slideTotal = slideFrames != null ? slideFrames.Length : 0;
        int windupCount = slideTotal / 3;
        int landingStart = SlideLandingStartIndex();

        var list = new System.Collections.Generic.List<ArcGuardFrame>();
        for (int i = 0; i < windupCount; i++) list.Add(GetArrayFrame(slideFrames, i));
        if (jumpFrames != null) list.AddRange(jumpFrames);
        for (int i = landingStart; i < slideTotal; i++) list.Add(GetArrayFrame(slideFrames, i));
        return list.ToArray();
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
        {
            StopEditorAnim();
            DestroyClawMarkPreview();
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (Application.isPlaying) return;

        if (firePointFace != null)
        {
            Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.9f);
            Gizmos.DrawSphere(firePointFace.position, 0.06f);
            var labelStyle = new GUIStyle();
            labelStyle.normal.textColor = Color.red;
            UnityEditor.Handles.Label(firePointFace.position + Vector3.up * 0.15f, "FirePoint_Face", labelStyle);
        }

        var cf = GetPreviewFrame(previewSprite);
        if (cf != null && cf.colliderSize.sqrMagnitude > 0.0001f)
        {
            Vector3 bodyPos = transform.position + new Vector3(cf.offset.x, cf.offset.y, 0f);
            Vector3 colliderCenter = bodyPos + new Vector3(cf.colliderOffset.x, cf.colliderOffset.y, 0f);
            Gizmos.color = new Color(0.15f, 0.95f, 0.15f, 0.9f);
            Gizmos.DrawWireCube(colliderCenter, new Vector3(cf.colliderSize.x, cf.colliderSize.y, 0.05f));
        }
    }
#endif

    private ArcGuardFrame GetPreviewFrame(ArcGuardPreviewSprite ps)
    {
        switch (ps)
        {
            case ArcGuardPreviewSprite.Idle1: return GetArrayFrame(idleFrames, 0);
            case ArcGuardPreviewSprite.Idle2: return GetArrayFrame(idleFrames, 1);
            case ArcGuardPreviewSprite.Idle3: return GetArrayFrame(idleFrames, 2);
            case ArcGuardPreviewSprite.Idle4: return GetArrayFrame(idleFrames, 3);
            case ArcGuardPreviewSprite.Slide1: return GetArrayFrame(slideFrames, 0);
            case ArcGuardPreviewSprite.Slide2: return GetArrayFrame(slideFrames, 1);
            case ArcGuardPreviewSprite.Slide3: return GetArrayFrame(slideFrames, 2);
            case ArcGuardPreviewSprite.Slide4: return GetArrayFrame(slideFrames, 3);
            case ArcGuardPreviewSprite.Slide5: return GetArrayFrame(slideFrames, 4);
            case ArcGuardPreviewSprite.Slide6: return GetArrayFrame(slideFrames, 5);
            case ArcGuardPreviewSprite.Slide7: return GetArrayFrame(slideFrames, 6);
            case ArcGuardPreviewSprite.Slide8: return GetArrayFrame(slideFrames, 7);
            case ArcGuardPreviewSprite.Slide9: return GetArrayFrame(slideFrames, 8);
            case ArcGuardPreviewSprite.JumpWindup1: return GetArrayFrame(slideFrames, 0);
            case ArcGuardPreviewSprite.JumpWindup2: return GetArrayFrame(slideFrames, 1);
            case ArcGuardPreviewSprite.JumpWindup3: return GetArrayFrame(slideFrames, 2);
            case ArcGuardPreviewSprite.JumpAir1: return GetArrayFrame(jumpFrames, 0);
            case ArcGuardPreviewSprite.JumpAir2: return GetArrayFrame(jumpFrames, 1);
            case ArcGuardPreviewSprite.JumpFall1: return GetArrayFrame(jumpFrames, 2);
            case ArcGuardPreviewSprite.JumpFall2: return GetArrayFrame(jumpFrames, 3);
            case ArcGuardPreviewSprite.JumpFall3: return GetArrayFrame(jumpFrames, 4);
            case ArcGuardPreviewSprite.JumpLanding1: return GetArrayFrame(slideFrames, SlideLandingStartIndex());
            case ArcGuardPreviewSprite.JumpLanding2: return GetArrayFrame(slideFrames, SlideLandingStartIndex() + 1);
            case ArcGuardPreviewSprite.JumpLanding3: return GetArrayFrame(slideFrames, SlideLandingStartIndex() + 2);
            case ArcGuardPreviewSprite.Claw2H_1: return GetArrayFrame(claw2HFrames, 0);
            case ArcGuardPreviewSprite.Claw2H_2: return GetArrayFrame(claw2HFrames, 1);
            case ArcGuardPreviewSprite.Claw2H_3: return GetArrayFrame(claw2HFrames, 2);
            case ArcGuardPreviewSprite.Claw2H_4: return GetArrayFrame(claw2HFrames, 3);
            case ArcGuardPreviewSprite.Claw2H_5: return GetArrayFrame(claw2HFrames, 4);
            case ArcGuardPreviewSprite.Claw2H_6: return GetArrayFrame(claw2HFrames, 5);
            case ArcGuardPreviewSprite.Claw2H_7: return GetArrayFrame(claw2HFrames, 6);
            case ArcGuardPreviewSprite.Claw1HLeft_1: return GetArrayFrame(claw1HLeftFrames, 0);
            case ArcGuardPreviewSprite.Claw1HLeft_2: return GetArrayFrame(claw1HLeftFrames, 1);
            case ArcGuardPreviewSprite.Claw1HLeft_3: return GetArrayFrame(claw1HLeftFrames, 2);
            case ArcGuardPreviewSprite.Claw1HLeft_4: return GetArrayFrame(claw1HLeftFrames, 3);
            case ArcGuardPreviewSprite.Claw1HLeft_5: return GetArrayFrame(claw1HLeftFrames, 4);
            case ArcGuardPreviewSprite.Claw1HLeft_6: return GetArrayFrame(claw1HLeftFrames, 5);
            case ArcGuardPreviewSprite.Claw1HRight_1: return GetArrayFrame(claw1HRightFrames, 0);
            case ArcGuardPreviewSprite.Claw1HRight_2: return GetArrayFrame(claw1HRightFrames, 1);
            case ArcGuardPreviewSprite.Claw1HRight_3: return GetArrayFrame(claw1HRightFrames, 2);
            case ArcGuardPreviewSprite.ClawMark1: return GetArrayFrame(clawMarkFrames, 0);
            case ArcGuardPreviewSprite.ClawMark2: return GetArrayFrame(clawMarkFrames, 1);
            case ArcGuardPreviewSprite.ClawMark3: return GetArrayFrame(clawMarkFrames, 2);
            case ArcGuardPreviewSprite.ClawMark4: return GetArrayFrame(clawMarkFrames, 3);
            case ArcGuardPreviewSprite.Roar1: return GetArrayFrame(roarFrames, 0);
            case ArcGuardPreviewSprite.Roar2: return GetArrayFrame(roarFrames, 1);
            case ArcGuardPreviewSprite.Roar3: return GetArrayFrame(roarFrames, 2);
            default: return null;
        }
    }

    private static ArcGuardFrame GetArrayFrame(ArcGuardFrame[] arr, int index)
    {
        if (arr == null || index < 0 || index >= arr.Length) return null;
        return arr[index];
    }

    // slideFramesの末尾（着地余韻区間）の開始indexを返す。JumpのLanding区間プレビュー・流用先で共通利用
    private int SlideLandingStartIndex()
    {
        int total = slideFrames != null ? slideFrames.Length : 0;
        return total - total / 3;
    }
}
