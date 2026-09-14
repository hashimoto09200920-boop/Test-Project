using System.Collections;
using UnityEngine;

/// <summary>
/// Area09 Stage3 中ボス「スサノオ」（弟）専用コントローラー。
/// 兄「ツクヨミ」とは別Prefab・別コントローラーで、HPは共有しない（それぞれ独自のEnemyStatsを持ち、
/// 片方が0になったらもう片方も強制的に倒す「連動方式」。連動処理は今後実装予定）。
/// TsukuyomiControllerは共有せず、本ファイルに専用実装している。
/// Idleループ再生、画面中央左右を使った「重心移動型」の移動、太鼓を振りかぶって叩きワープ弾を
/// 召喚する攻撃、後半フェーズ限定で一定間隔で発動する螺旋弾の大技を実装している。
/// </summary>
[RequireComponent(typeof(EnemyStats))]
public class SusanooController : MonoBehaviour
{
    private enum PreviewSprite
    {
        Idle1, Idle2, Idle3, Idle4, Idle5, Idle6,
        Idle7, Idle8, Idle9, Idle10, Idle11, Idle12,
        IdleAnimate,
        Attack1, Attack2, Attack3, Attack4, Attack5, Attack6, AttackAnimate,
        Spiral1, Spiral2, Spiral3, Spiral4, Spiral5, Spiral6, SpiralAnimate
    }

    [System.Serializable]
    public class SusanooFrame
    {
        public Sprite sprite;
        [Tooltip("表示秒数")]
        public float duration = 0.15f;
        [Tooltip("このコマの表示位置の微調整（ローカル座標オフセット）。生成画像ごとの位置ズレ補正用")]
        public Vector2 offset = Vector2.zero;
        [Tooltip("このコマの表示サイズの微調整（X/Y独立の拡縮倍率、基準は1）。生成画像ごとのサイズズレ補正用")]
        public Vector2 scale = Vector2.one;
    }

    // =========================================================
    // References
    // =========================================================

    [Header("References")]
    [SerializeField] private SpriteRenderer bodySpriteRenderer;
    [Tooltip("三勾玉の足場のSpriteRenderer（子オブジェクト想定）。スプライト自体はInspectorで直接割り当てる" +
             "（Susanooの足場は1枚絵の静止画のため、Tsukuyomiのboard Framesのような配列は使わない）")]
    [SerializeField] private SpriteRenderer boardSpriteRenderer;
    [SerializeField] private EnemyStats enemyStats;
    [Tooltip("B4デバフ（Skill_B4_EnemySpeedDown）が正しく反映されるように速度倍率のみ参照する。" +
             "実際の移動はEnemyMoverではなくこのコントローラーが行うため、Awake()でsuppressMovement=trueにする")]
    [SerializeField] private EnemyMover enemyMover;
    [SerializeField] private EnemySpriteSwapper spriteSwapper;
    [Tooltip("太鼓を叩いてワープ弾を召喚する演出はこのコントローラーが手動で行うため、" +
             "EnemyShooter自体の自動発射ループはAwake()で無効化する。EnemyData/BulletPrefab/" +
             "ProjectileRootの参照元としてのみ利用する")]
    [SerializeField] private EnemyShooter enemyShooter;
    [SerializeField] private EnemyBullet bulletPrefab;
    [SerializeField] private Transform projectileRoot;
    [SerializeField] private float ignoreOwnerTime = 0.15f;
    [Tooltip("吸収モードの発動条件（被弾回数）・ダメージ無効化に使う。SusanooはEnemyData側でWeakPoint System " +
             "（useWeakPointSystem）が有効で、実際のダメージ処理は本体（ルート）のEnemyDamageReceiverではなく" +
             "子オブジェクト「Body」のEnemyPartで行われるため、そちらを参照する。未設定ならGetComponentInChildrenで自動取得")]
    [SerializeField] private EnemyPart bodyEnemyPart;

    [Header("Absorb Mode（反射弾吸収モード。Area05ボスFingersの後半仕様と同じ。ダメージを一定回数受けると発動し、" +
             "体が赤く光って一定時間、反射弾を受けるとダメージの代わりにHPが回復する）")]
    [Tooltip("何回ダメージを受けたら吸収モードに入るか")]
    [SerializeField] private int absorbModeHitThreshold = 5;
    [Tooltip("吸収モードが継続する秒数")]
    [SerializeField] private float absorbModeDuration = 8f;
    [Tooltip("吸収モード中、反射弾を受けるごとに回復するHP量")]
    [SerializeField] private float absorbModeHealAmount = 20f;
    [Tooltip("吸収モード中の体の色（赤く光る等）")]
    [SerializeField] private Color absorbModeTintColor = new Color(1f, 0.3f, 0.3f, 1f);
    [Tooltip("吸収モード中、反射弾を吸収した瞬間に鳴らすSE")]
    [SerializeField] private AudioClip absorbHitSe;
    [Range(0f, 1f)]
    [SerializeField] private float absorbHitSeVolume = 1f;

    [Header("Fade In（EnemyStats.FadeInはEnemyStatsと同じGameObject上のSpriteRendererしか対象にできないため、" +
             "bodySpriteRendererが子オブジェクト「Body」側にあるSusanooには効かない。専用に実装している）")]
    [Tooltip("出現時、bodySpriteRenderer/boardSpriteRendererを透明から不透明にフェードインさせる秒数")]
    [SerializeField] private float initialFadeInDuration = 4f;

    [Header("Debug")]
    [SerializeField] private bool showDebugLog = false;

    [Header("Editor Preview（Play前にInspectorでスプライト確認）")]
    [SerializeField] private PreviewSprite previewSprite = PreviewSprite.Idle1;

    // =========================================================
    // Sprites
    // =========================================================

    [Header("Sprites - Idle（荒々しい重心移動を伴う待機、ループ再生。太鼓は叩かない）")]
    [NonReorderable]
    [SerializeField] private SusanooFrame[] idleFrames;

    [Header("Sprites - Attack（太鼓を振りかぶって叩き、ワープ弾を召喚する。1回だけ再生してIdleへ戻る）")]
    [NonReorderable]
    [SerializeField] private SusanooFrame[] attackFrames;
    [Tooltip("attackFramesの何コマ目(0始まり)を表示した時点で、バチが太鼓に当たりワープ弾を召喚するか")]
    [SerializeField] private int attackFireFrame = 3;
    [Tooltip("弾を召喚するワールド座標のオフセット（太鼓の中心付近を想定、bodySpriteRenderer基準）")]
    [SerializeField] private Vector2 attackMuzzleOffset = Vector2.zero;

    [Header("Warp Bullet Count（前半/後半で1回の攻撃の発射数を変える）")]
    [Tooltip("前半フェーズでの発射数の最小値")]
    [SerializeField] private int warpShotCountFrontMin = 1;
    [Tooltip("前半フェーズでの発射数の最大値")]
    [SerializeField] private int warpShotCountFrontMax = 2;
    [Tooltip("後半フェーズでの発射数の最小値")]
    [SerializeField] private int warpShotCountBackMin = 2;
    [Tooltip("後半フェーズでの発射数の最大値")]
    [SerializeField] private int warpShotCountBackMax = 3;
    [Tooltip("複数発射する時、それぞれの狙う方向をランダムにずらす角度の範囲（度）。" +
             "0だと全弾が完全に同じ方向・位置に重なって見分けがつかなくなる")]
    [SerializeField] private float warpShotSpreadAngle = 15f;

    [Header("Warp Timing Pattern（発射タイミングのパターン。Weightで発生率を調整）")]
    [Tooltip("「全弾同時に発射（＝同じタイミングでワープする）」パターンが選ばれる重み")]
    [SerializeField] private float warpTimingSimultaneousWeight = 1f;
    [Tooltip("「1発ずつ発射をずらす」パターンが選ばれる重み")]
    [SerializeField] private float warpTimingStaggeredWeight = 1f;
    [Tooltip("ずらし発射パターンの時、1発ごとの発射間隔のランダム範囲の最小値（秒）。" +
             "例えば0.1〜0.4なら、次の弾を発射するまでの間隔が毎回この範囲内でランダムに決まる")]
    [SerializeField] private float warpStaggerDelayMin = 0.1f;
    [Tooltip("ずらし発射パターンの時、1発ごとの発射間隔のランダム範囲の最大値（秒）")]
    [SerializeField] private float warpStaggerDelayMax = 0.4f;

    [Header("Sprites - Spiral（後半限定の大技。太鼓を手放し宙で回転させ、螺旋状に弾をばら撒く）")]
    [NonReorderable]
    [SerializeField] private SusanooFrame[] spiralFrames;
    [Tooltip("spiralFramesの何コマ目(0始まり)から弾を撃ち始めるか（太鼓が最高速で回転するピークのコマを想定）")]
    [SerializeField] private int spiralFireFrame = 2;

    [Header("Spiral Bullet（後半限定の大技のパラメータ）")]
    [Tooltip("enemyData.bulletTypesの中で「螺旋弾」に対応する要素番号（ワープ弾とは別の要素を想定）")]
    [SerializeField] private int spiralBulletTypeIndex = 0;
    [Tooltip("大技を発動する間隔（秒）。後半フェーズに入った後、この間隔で自動的に発動し続ける")]
    [SerializeField] private float spiralAttackInterval = 60f;
    [Tooltip("1回の発動で撃つ弾数")]
    [SerializeField] private int spiralBulletCount = 32;
    [Tooltip("1発ごとに発射角度を回転させる量（度）")]
    [SerializeField] private float spiralAngleStepDeg = 22.5f;
    [Tooltip("1発ごとの発射間隔（秒）")]
    [SerializeField] private float spiralFireInterval = 0.05f;

    // =========================================================
    // Attack Cycle
    // =========================================================

    [Header("Attack Cycle")]
    [SerializeField] private float attackIntervalMin = 2.5f;
    [SerializeField] private float attackIntervalMax = 4f;

    [Header("Phase（HP閾値による前半/後半の切り替え）")]
    [Tooltip("HP割合(%)がこの値を下回ったら後半フェーズへ移行する（一度移行したら前半には戻らない）")]
    [SerializeField] private float phaseTransitionHpThreshold = 70f;

    // =========================================================
    // Movement - Weighted Shift（重心移動型：目標地点へ移動→一瞬静止（溜め）→次の目標へ）
    // =========================================================

    [Header("Movement - Weighted Shift（画面中央・中央左右を使った重心移動型の移動）")]
    [Tooltip("移動範囲の中心（ワールド座標）")]
    [SerializeField] private Vector2 wanderAreaCenter = new Vector2(0f, 0.5f);
    [Tooltip("移動範囲の半径（X, Y）。X方向は画面左右端付近まで、Y方向はツクヨミの浮遊範囲より下・" +
             "ダンサーが動く下部エリアより上に収まるように設定する")]
    [SerializeField] private Vector2 wanderAreaHalfExtents = new Vector2(7.5f, 1.0f);
    [Tooltip("目標地点へ移動する速さ（Unity単位/秒）")]
    [SerializeField] private float moveSpeed = 1.8f;
    [Tooltip("目標地点に到達したとみなす距離")]
    [SerializeField] private float arriveThreshold = 0.15f;
    [Tooltip("目標地点に到達してから次の目標を決めるまでの静止（溜め）時間の最小値（秒）")]
    [SerializeField] private float pauseSecondsMin = 0.4f;
    [Tooltip("目標地点に到達してから次の目標を決めるまでの静止（溜め）時間の最大値（秒）")]
    [SerializeField] private float pauseSecondsMax = 1.2f;

    // =========================================================
    // Movement - Board Tilt（ボードの左右の傾き。Tsukuyomiと同じサイン波方式）
    // =========================================================

    [Header("Movement - Board Tilt（ボードの左右の傾き。Tsukuyomiと同じ方式）")]
    [Tooltip("ボードスプライトが左右に傾く角度の振れ幅（度）")]
    [SerializeField] private float boardTiltAmplitude = 6f;
    [Tooltip("ボードの傾きの周波数（1秒あたりの往復回数）")]
    [SerializeField] private float boardTiltFrequency = 0.4f;

    // =========================================================
    // Runtime state
    // =========================================================

    private EnemyData enemyData;
    private Vector2 currentBodyFrameOffset;
    private Coroutine idleLoopCoroutine;
    private Coroutine movementCoroutine;
    private Coroutine attackCoroutine;
    private Coroutine spiralLoopCoroutine;
    private Coroutine absorbModeCoroutine;

    /// <summary>出現時のフェードインが完了するまでtrueにならない。移動・攻撃系ループの起動を待たせるためのガード</summary>
    private bool spawnFadeInComplete;

    private int absorbHitCount;
    private bool isAbsorbModeActive;

    private float boardTiltPhase;

    // ★HP閾値によるフェーズ切り替え（TsukuyomiControllerと同じ「一度きりフラグ＋現在フェーズ確認」の
    //   二重ガードで、回復等でHPが閾値を跨ぎ直しても前半へ逆戻りしないようにする
    private enum Phase { Front, Back }
    private Phase phase = Phase.Front;
    private bool phaseTransitioned;

    private float TimeScale =>
        SlowMotionManager.Instance != null ? SlowMotionManager.Instance.TimeScale : 1f;

    private float SpeedMultiplier => enemyMover != null ? enemyMover.SpeedMultiplier : 1f;

    // =========================================================
    // Unity ライフサイクル
    // =========================================================

    private void Awake()
    {
        if (enemyStats == null) enemyStats = GetComponent<EnemyStats>();
        if (bodySpriteRenderer == null) bodySpriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteSwapper == null) spriteSwapper = GetComponent<EnemySpriteSwapper>();

        if (enemyMover == null) enemyMover = GetComponent<EnemyMover>();
        // 実際の移動はこのコントローラーが独自に行うため、EnemyMover自体の移動処理は止める
        // （B4デバフの倍率はSpeedMultiplierプロパティ経由でMovementLoop側から参照する）
        if (enemyMover != null) enemyMover.suppressMovement = true;

        if (enemyShooter == null) enemyShooter = GetComponent<EnemyShooter>();
        // 発射はこのコントローラーが演出に合わせて手動で行うため、自動発射ループは止める
        if (enemyShooter != null) enemyShooter.enabled = false;

        if (bodyEnemyPart == null) bodyEnemyPart = GetComponentInChildren<EnemyPart>();
    }

    private void Start()
    {
        if (!Application.isPlaying) return;

        // EnemySpawnerはInstantiate直後（Awake実行後）にshooter.SetProjectileRoot()等で値を注入するため、
        // Awake()で読み取ると注入前の値（null）を掴んでしまう。TsukuyomiController等と同様にStart()で読み取る
        if (enemyShooter != null)
        {
            enemyData = enemyShooter.GetEnemyData();
            if (bulletPrefab == null) bulletPrefab = enemyShooter.GetBulletPrefab();
            if (projectileRoot == null) projectileRoot = enemyShooter.GetProjectileRoot();
        }

        if (projectileRoot == null)
        {
            GameObject pr = GameObject.Find("ProjectileRoot");
            if (pr != null) projectileRoot = pr.transform;
        }
    }

    private void OnEnable()
    {
        StartIdleFrameLoop();

        boardTiltPhase = Random.Range(0f, Mathf.PI * 2f);

        StartCoroutine(LinkToTsukuyomi());

        if (bodyEnemyPart != null) bodyEnemyPart.OnHitWithDamage += OnReflectedBulletHitForAbsorb;

        spawnFadeInComplete = false;
        StartCoroutine(InitialFadeInThenActivate());
    }

    /// <summary>
    /// フェードインが完了するまで、移動（重心移動）・通常攻撃・螺旋弾の大技を一切開始させない。
    /// フェードイン完了後にまとめて各ループを起動する
    /// </summary>
    private IEnumerator InitialFadeInThenActivate()
    {
        yield return StartCoroutine(InitialFadeIn());
        spawnFadeInComplete = true;

        if (movementCoroutine != null) StopCoroutine(movementCoroutine);
        movementCoroutine = StartCoroutine(MovementLoop());

        if (attackCoroutine != null) StopCoroutine(attackCoroutine);
        attackCoroutine = StartCoroutine(AttackLoop());

        if (spiralLoopCoroutine != null) StopCoroutine(spiralLoopCoroutine);
        spiralLoopCoroutine = StartCoroutine(SpiralAttackLoop());
    }

    /// <summary>
    /// 出現時のフェードイン。EnemyStats.FadeIn()はEnemyStats自身のGameObject（ルート）のSpriteRendererしか
    /// 見ないため、子オブジェクト「Body」にあるbodySpriteRenderer（と足場のboardSpriteRenderer）には効かない。
    /// BossHandController（Area05 Fingers）と同じ方式で、このコントローラー側で直接フェードさせる
    /// </summary>
    private IEnumerator InitialFadeIn()
    {
        if (initialFadeInDuration <= 0f) yield break;

        Color bodyColor = bodySpriteRenderer != null ? bodySpriteRenderer.color : Color.white;
        Color boardColor = boardSpriteRenderer != null ? boardSpriteRenderer.color : Color.white;

        if (bodySpriteRenderer != null) bodySpriteRenderer.color = new Color(bodyColor.r, bodyColor.g, bodyColor.b, 0f);
        if (boardSpriteRenderer != null) boardSpriteRenderer.color = new Color(boardColor.r, boardColor.g, boardColor.b, 0f);

        float elapsed = 0f;
        while (elapsed < initialFadeInDuration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Clamp01(elapsed / initialFadeInDuration);
            if (bodySpriteRenderer != null) bodySpriteRenderer.color = new Color(bodyColor.r, bodyColor.g, bodyColor.b, alpha);
            if (boardSpriteRenderer != null) boardSpriteRenderer.color = new Color(boardColor.r, boardColor.g, boardColor.b, alpha);
            yield return null;
        }

        if (bodySpriteRenderer != null) bodySpriteRenderer.color = bodyColor;
        if (boardSpriteRenderer != null) boardSpriteRenderer.color = boardColor;
    }

    /// <summary>
    /// TsukuyomiとSusanooはEnemySpawnerによって別々にInstantiateされる独立したPrefabのため、
    /// Prefab上で直接お互いの参照をドラッグ設定できない。そのため実行時にシーン内のTsukuyomiControllerを
    /// 探し出し、SusanooのEnemyStats.Damage/HealをTsukuyomi側へ転送するようリンクする（＝HPプール共有。
    /// シールド消費もTsukuyomi側のGetComponent&lt;EnemyShield&gt;()で行われるため自動的に共有される）。
    /// 兄弟のSpawnタイミングが読めない（Susanooより後にTsukuyomiがSpawnされる可能性がある）ため、
    /// 固定回数で諦めず、見つかるまで待ち続ける
    /// </summary>
    private IEnumerator LinkToTsukuyomi()
    {
        TsukuyomiController tsukuyomi = null;
        while (tsukuyomi == null)
        {
            tsukuyomi = FindFirstObjectByType<TsukuyomiController>();
            if (tsukuyomi == null) yield return new WaitForSeconds(0.2f);
        }

        EnemyStats tsukuyomiStats = tsukuyomi.GetComponent<EnemyStats>();
        if (tsukuyomiStats == null || enemyStats == null)
        {
            Debug.LogWarning("[SusanooController] LinkToTsukuyomi failed: EnemyStats component missing", this);
            yield break;
        }

        enemyStats.SetDamageRedirectTarget(tsukuyomiStats);
        tsukuyomiStats.AddLinkedDeathTarget(enemyStats);

        Debug.Log("[SusanooController][DBG] Linked HP pool to Tsukuyomi", this);
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        idleLoopCoroutine = null;
        movementCoroutine = null;
        attackCoroutine = null;
        spiralLoopCoroutine = null;
        absorbModeCoroutine = null;

        if (bodyEnemyPart != null)
        {
            bodyEnemyPart.OnHitWithDamage -= OnReflectedBulletHitForAbsorb;
            bodyEnemyPart.OnHitWhileSuppressed -= OnAbsorbHit;
            bodyEnemyPart.suppressDamage = false;
        }
        isAbsorbModeActive = false;
    }

    // =========================================================
    // Absorb Mode（反射弾吸収モード。Area05ボスFingersの後半仕様と同じ）
    // =========================================================

    /// <summary>
    /// 反射弾がSusanoo（Body＝EnemyPart）に当たるたびに呼ばれる。吸収モード中はカウントしない
    /// （既に発動済みのため）。規定回数に達したら吸収モードへ移行する
    /// </summary>
    private void OnReflectedBulletHitForAbsorb(EnemyBullet bullet, int finalDamage)
    {
        if (isAbsorbModeActive) return;
        if (phase != Phase.Back) return;

        absorbHitCount++;
        if (absorbHitCount >= absorbModeHitThreshold)
        {
            absorbHitCount = 0;
            TriggerAbsorbMode();
        }
    }

    private void TriggerAbsorbMode()
    {
        if (isAbsorbModeActive) return;
        isAbsorbModeActive = true;

        if (bodyEnemyPart != null)
        {
            bodyEnemyPart.suppressDamage = true;
            bodyEnemyPart.OnHitWhileSuppressed += OnAbsorbHit;
        }

        if (bodySpriteRenderer != null) bodySpriteRenderer.color = absorbModeTintColor;

        if (absorbModeCoroutine != null) StopCoroutine(absorbModeCoroutine);
        absorbModeCoroutine = StartCoroutine(AbsorbModeRoutine());

        if (showDebugLog) Debug.Log("[SusanooController] Absorb Mode triggered", this);
    }

    private IEnumerator AbsorbModeRoutine()
    {
        yield return new WaitForSeconds(absorbModeDuration);
        EndAbsorbMode();
    }

    private void EndAbsorbMode()
    {
        isAbsorbModeActive = false;
        absorbModeCoroutine = null;

        if (bodyEnemyPart != null)
        {
            bodyEnemyPart.suppressDamage = false;
            bodyEnemyPart.OnHitWhileSuppressed -= OnAbsorbHit;
        }

        if (bodySpriteRenderer != null) bodySpriteRenderer.color = Color.white;

        if (showDebugLog) Debug.Log("[SusanooController] Absorb Mode ended", this);
    }

    private void OnAbsorbHit(EnemyBullet bullet)
    {
        if (enemyStats != null) enemyStats.Heal(Mathf.RoundToInt(absorbModeHealAmount));

        if (bullet == null) return;

        // ★Fingers（Area05ボス）のOnEyeHitDuringBurstと同じ方式：吸収SEを弾の位置で鳴らし、弾を消滅させる
        if (absorbHitSe != null)
        {
            float vol = absorbHitSeVolume * (SoundSettingsManager.Instance != null ? SoundSettingsManager.Instance.SEVolume : 1f);
            AudioSource.PlayClipAtPoint(absorbHitSe, bullet.transform.position, vol);
        }
        Destroy(bullet.gameObject);
    }

    private void Update()
    {
        float dt = Time.deltaTime * TimeScale;
        ApplyBoardTilt(dt);
        CheckPhaseTransition();
    }

    /// <summary>
    /// HP割合がphaseTransitionHpThreshold(%)を下回ったら後半フェーズへ移行する。
    /// phaseTransitioned（一度きりフラグ）とphase==Backの二重ガードにより、
    /// セルフヒール等でHPが閾値を跨ぎ直しても前半フェーズへは戻らない。
    /// </summary>
    private void CheckPhaseTransition()
    {
        if (phaseTransitioned || phase == Phase.Back) return;
        if (enemyStats == null) return;
        if (enemyStats.GetHpPercentage() < phaseTransitionHpThreshold)
        {
            phase = Phase.Back;
            phaseTransitioned = true;
        }
    }

    private void ApplyBoardTilt(float dt)
    {
        boardTiltPhase += dt * boardTiltFrequency * Mathf.PI * 2f;
        if (boardSpriteRenderer == null) return;
        float z = Mathf.Sin(boardTiltPhase) * boardTiltAmplitude;
        boardSpriteRenderer.transform.localRotation = Quaternion.Euler(0f, 0f, z);
    }

    // =========================================================
    // Movement（重心移動型：目標地点へ移動→溜め→次の目標へ、を繰り返す）
    // =========================================================

    private IEnumerator MovementLoop()
    {
        while (true)
        {
            Vector2 target = PickRandomTarget();

            while (Vector2.Distance(transform.position, target) > arriveThreshold)
            {
                float dt = Time.deltaTime * TimeScale;
                Vector2 dir = (target - (Vector2)transform.position).normalized;
                transform.position += (Vector3)(dir * moveSpeed * SpeedMultiplier * dt);
                yield return null;
            }

            float pauseSeconds = Random.Range(pauseSecondsMin, pauseSecondsMax);
            if (showDebugLog) Debug.Log($"[SusanooController] Reached target={target} pause={pauseSeconds}", this);
            yield return new WaitForSeconds(pauseSeconds);
        }
    }

    private Vector2 PickRandomTarget()
    {
        float x = wanderAreaCenter.x + Random.Range(-wanderAreaHalfExtents.x, wanderAreaHalfExtents.x);
        float y = wanderAreaCenter.y + Random.Range(-wanderAreaHalfExtents.y, wanderAreaHalfExtents.y);
        return new Vector2(x, y);
    }

    // =========================================================
    // Idle ループ再生
    // =========================================================

    private void StartIdleFrameLoop()
    {
        if (idleLoopCoroutine != null) StopCoroutine(idleLoopCoroutine);
        idleLoopCoroutine = StartCoroutine(FrameLoop(idleFrames));
    }

    private IEnumerator FrameLoop(SusanooFrame[] frames)
    {
        if (frames == null || frames.Length == 0) yield break;
        int i = 0;
        while (true)
        {
            SusanooFrame f = frames[i % frames.Length];
            ApplyBodySprite(f);
            yield return new WaitForSeconds(Mathf.Max(0.01f, f != null ? f.duration : 0.15f));
            i++;
        }
    }

    private void ApplyBodySprite(SusanooFrame f)
    {
        if (bodySpriteRenderer == null || f == null || f.sprite == null) return;
        bodySpriteRenderer.sprite = f.sprite;
        bodySpriteRenderer.transform.localScale = new Vector3(f.scale.x, f.scale.y, 1f);
        currentBodyFrameOffset = f.offset;
        Vector3 lp = bodySpriteRenderer.transform.localPosition;
        lp.x = f.offset.x;
        lp.y = f.offset.y;
        bodySpriteRenderer.transform.localPosition = lp;
        // EnemySpriteSwapperが被弾ヒットフラッシュ終了後に古いスプライトへ巻き戻すのを防ぐため、
        // 「通常スプライト」のキャッシュを常に最新のフレームへ同期させる
        if (spriteSwapper != null) spriteSwapper.SetBaseSprite(f.sprite);
    }

    // =========================================================
    // Attack Cycle（太鼓を振りかぶって叩き、ワープ弾を召喚する）
    // =========================================================

    private IEnumerator AttackLoop()
    {
        yield return new WaitForSeconds(Random.Range(attackIntervalMin, attackIntervalMax));
        while (true)
        {
            yield return StartCoroutine(RunAttack());
            yield return new WaitForSeconds(Random.Range(attackIntervalMin, attackIntervalMax));
        }
    }

    private IEnumerator RunAttack()
    {
        if (idleLoopCoroutine != null)
        {
            StopCoroutine(idleLoopCoroutine);
            idleLoopCoroutine = null;
        }

        if (attackFrames != null)
        {
            for (int i = 0; i < attackFrames.Length; i++)
            {
                SusanooFrame f = attackFrames[i];
                ApplyBodySprite(f);
                if (i == attackFireFrame) StartCoroutine(FireWarpBulletsRoutine());
                yield return new WaitForSeconds(Mathf.Max(0.01f, f != null ? f.duration : 0.1f));
            }
        }

        StartIdleFrameLoop();
    }

    // =========================================================
    // Spiral Attack（後半限定の大技。一定間隔で自動発動する）
    // =========================================================

    /// <summary>
    /// 後半フェーズに入るまで待機し、入った後はspiralAttackIntervalごとに自動的に大技を発動し続ける
    /// </summary>
    private IEnumerator SpiralAttackLoop()
    {
        while (phase != Phase.Back) yield return null;

        while (true)
        {
            yield return new WaitForSeconds(spiralAttackInterval);
            yield return StartCoroutine(RunSpiralAttack());
        }
    }

    private IEnumerator RunSpiralAttack()
    {
        // ★通常のワープ弾攻撃と演出が同時に走らないよう、一旦止めてから螺旋弾のアニメーションへ切り替える
        if (attackCoroutine != null)
        {
            StopCoroutine(attackCoroutine);
            attackCoroutine = null;
        }
        if (idleLoopCoroutine != null)
        {
            StopCoroutine(idleLoopCoroutine);
            idleLoopCoroutine = null;
        }

        if (spiralFrames != null)
        {
            for (int i = 0; i < spiralFrames.Length; i++)
            {
                SusanooFrame f = spiralFrames[i];
                ApplyBodySprite(f);
                if (i == spiralFireFrame) StartCoroutine(FireSpiralBulletsRoutine());
                yield return new WaitForSeconds(Mathf.Max(0.01f, f != null ? f.duration : 0.1f));
            }
        }

        StartIdleFrameLoop();

        // ★通常のワープ弾攻撃ループを再開する
        if (attackCoroutine != null) StopCoroutine(attackCoroutine);
        attackCoroutine = StartCoroutine(AttackLoop());
    }

    /// <summary>
    /// 発射角を少しずつ回転させながら弾を撃ち出し、渦巻き状の弾幕を形成する
    /// </summary>
    private IEnumerator FireSpiralBulletsRoutine()
    {
        if (FloorHealth.IsBrokenGlobal || PixelDancerController.IsPlayerDeadGlobal) yield break;
        if (bulletPrefab == null || projectileRoot == null || enemyData == null) yield break;
        if (enemyData.bulletTypes == null || spiralBulletTypeIndex < 0 || spiralBulletTypeIndex >= enemyData.bulletTypes.Length) yield break;

        EnemyData.BulletType bt = enemyData.bulletTypes[spiralBulletTypeIndex];
        if (bt == null) yield break;

        Vector3 muzzleWorldPos = bodySpriteRenderer != null
            ? bodySpriteRenderer.transform.position + (Vector3)attackMuzzleOffset
            : transform.position;

        Vector2 baseDir = ComputeAimDirection(muzzleWorldPos);
        float angle = 0f;

        for (int i = 0; i < spiralBulletCount; i++)
        {
            Vector2 dir = Quaternion.Euler(0f, 0f, angle) * baseDir;

            EnemyBullet bullet = Instantiate(bulletPrefab, muzzleWorldPos, Quaternion.identity, projectileRoot);
            // fallbackSpeed/fallbackLifetimeはBullet Types側のSpeed/Life Timeが未設定(0以下)の時だけ使われる保険値
            EnemyShooter.ApplyBulletTypeToEnemyBullet(bullet, bt, 1f, 5f, null, bulletPrefab, projectileRoot);
            bullet.SetDirection(dir);

            foreach (Collider2D col in GetComponentsInChildren<Collider2D>())
                if (col != null) bullet.SetOwnerCollisionIgnore(col, ignoreOwnerTime);

            angle += spiralAngleStepDeg;

            if (i < spiralBulletCount - 1)
                yield return new WaitForSeconds(spiralFireInterval);
        }

        if (showDebugLog) Debug.Log($"[SusanooController] FireSpiralBullets count={spiralBulletCount} step={spiralAngleStepDeg}", this);
    }

    /// <summary>
    /// 太鼓を叩いた瞬間にワープ弾を発射する。前半/後半フェーズで発射数を変え、複数発射する場合は
    /// 完全に同じ方向・位置へ重ならないよう、狙う方向をそれぞれ少しずつランダムにずらす。
    /// さらに発射タイミングのパターンをWeightで重み付き抽選し、「全弾同時（＝同じタイミングでワープ）」か
    /// 「1発ずつ発射間隔をランダムにずらす（＝ワープのタイミングも自然にずれる）」かを選ぶ。
    /// </summary>
    private IEnumerator FireWarpBulletsRoutine()
    {
        if (FloorHealth.IsBrokenGlobal || PixelDancerController.IsPlayerDeadGlobal) yield break;
        if (bulletPrefab == null || projectileRoot == null || enemyData == null) yield break;
        if (enemyData.bulletTypes == null || enemyData.bulletTypes.Length == 0) yield break;

        int idx = enemyShooter != null ? enemyShooter.PickBulletTypeIndex(enemyData.bulletTypes.Length) : 0;
        if (idx < 0 || idx >= enemyData.bulletTypes.Length) idx = 0;
        EnemyData.BulletType bt = enemyData.bulletTypes[idx];
        if (bt == null) yield break;

        int shotCount = phase == Phase.Back
            ? Random.Range(warpShotCountBackMin, warpShotCountBackMax + 1)
            : Random.Range(warpShotCountFrontMin, warpShotCountFrontMax + 1);

        bool staggered = PickWarpTimingIsStaggered();

        Vector3 muzzleWorldPos = bodySpriteRenderer != null
            ? bodySpriteRenderer.transform.position + (Vector3)attackMuzzleOffset
            : transform.position;

        Vector2 baseDir = ComputeAimDirection(muzzleWorldPos);

        for (int i = 0; i < shotCount; i++)
        {
            float angleOffset = shotCount > 1 ? Random.Range(-warpShotSpreadAngle, warpShotSpreadAngle) : 0f;
            Vector2 dir = Quaternion.Euler(0f, 0f, angleOffset) * baseDir;

            EnemyBullet bullet = Instantiate(bulletPrefab, muzzleWorldPos, Quaternion.identity, projectileRoot);
            // fallbackSpeed/fallbackLifetimeはBullet Types側のSpeed/Life Timeが未設定(0以下)の時だけ使われる保険値
            EnemyShooter.ApplyBulletTypeToEnemyBullet(bullet, bt, 1f, 5f, null, bulletPrefab, projectileRoot);
            bullet.SetDirection(dir);

            foreach (Collider2D col in GetComponentsInChildren<Collider2D>())
                if (col != null) bullet.SetOwnerCollisionIgnore(col, ignoreOwnerTime);

            // ★ずらし発射パターンの場合、次の弾を発射するまでの間隔をMin〜Maxの範囲で毎回ランダムに決める。
            //   各弾はワープ発射時のこの瞬間から自分自身のwarpDisappearAfterSeconds等を数え始めるため、
            //   発射タイミングをずらすだけで、そのままワープするタイミングも自然にずれる
            if (staggered && i < shotCount - 1)
            {
                float delay = Random.Range(warpStaggerDelayMin, warpStaggerDelayMax);
                yield return new WaitForSeconds(delay);
            }
        }

        if (showDebugLog) Debug.Log($"[SusanooController] FireWarpBullet idx={idx} phase={phase} shotCount={shotCount} staggered={staggered}", this);
    }

    /// <summary>
    /// Warp Timing Simultaneous/Staggeredの2パターンをWeightで重み付き抽選する。
    /// ただしStaggeredパターンは後半フェーズ限定。前半フェーズは常にSimultaneousになる
    /// </summary>
    private bool PickWarpTimingIsStaggered()
    {
        if (phase != Phase.Back) return false;

        float simultaneousWeight = Mathf.Max(0f, warpTimingSimultaneousWeight);
        float staggeredWeight = Mathf.Max(0f, warpTimingStaggeredWeight);
        float total = simultaneousWeight + staggeredWeight;
        if (total <= 0f) return false;

        float roll = Random.Range(0f, total);
        return roll >= simultaneousWeight;
    }

    private Vector2 ComputeAimDirection(Vector3 spawnPos)
    {
        PixelDancerController player = FindFirstObjectByType<PixelDancerController>();
        if (player == null) return Vector2.down;
        Vector2 dir = (Vector2)player.transform.position - (Vector2)spawnPos;
        return dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector2.down;
    }

    // =========================================================
    // Editor Preview
    // =========================================================

    private void OnValidate()
    {
        if (Application.isPlaying) return;
        if (bodySpriteRenderer == null) bodySpriteRenderer = GetComponent<SpriteRenderer>();

#if UNITY_EDITOR
        switch (previewSprite)
        {
            case PreviewSprite.IdleAnimate: StartEditorAnim(idleFrames); return;
            case PreviewSprite.AttackAnimate: StartEditorAnim(attackFrames); return;
            case PreviewSprite.SpiralAnimate: StartEditorAnim(spiralFrames); return;
        }
        StopEditorAnim();
#endif

        switch (previewSprite)
        {
            case PreviewSprite.Idle1: ApplyPreview(idleFrames, 0); break;
            case PreviewSprite.Idle2: ApplyPreview(idleFrames, 1); break;
            case PreviewSprite.Idle3: ApplyPreview(idleFrames, 2); break;
            case PreviewSprite.Idle4: ApplyPreview(idleFrames, 3); break;
            case PreviewSprite.Idle5: ApplyPreview(idleFrames, 4); break;
            case PreviewSprite.Idle6: ApplyPreview(idleFrames, 5); break;
            case PreviewSprite.Idle7: ApplyPreview(idleFrames, 6); break;
            case PreviewSprite.Idle8: ApplyPreview(idleFrames, 7); break;
            case PreviewSprite.Idle9: ApplyPreview(idleFrames, 8); break;
            case PreviewSprite.Idle10: ApplyPreview(idleFrames, 9); break;
            case PreviewSprite.Idle11: ApplyPreview(idleFrames, 10); break;
            case PreviewSprite.Idle12: ApplyPreview(idleFrames, 11); break;
            case PreviewSprite.Attack1: ApplyPreview(attackFrames, 0); break;
            case PreviewSprite.Attack2: ApplyPreview(attackFrames, 1); break;
            case PreviewSprite.Attack3: ApplyPreview(attackFrames, 2); break;
            case PreviewSprite.Attack4: ApplyPreview(attackFrames, 3); break;
            case PreviewSprite.Attack5: ApplyPreview(attackFrames, 4); break;
            case PreviewSprite.Attack6: ApplyPreview(attackFrames, 5); break;
            case PreviewSprite.Spiral1: ApplyPreview(spiralFrames, 0); break;
            case PreviewSprite.Spiral2: ApplyPreview(spiralFrames, 1); break;
            case PreviewSprite.Spiral3: ApplyPreview(spiralFrames, 2); break;
            case PreviewSprite.Spiral4: ApplyPreview(spiralFrames, 3); break;
            case PreviewSprite.Spiral5: ApplyPreview(spiralFrames, 4); break;
            case PreviewSprite.Spiral6: ApplyPreview(spiralFrames, 5); break;
        }

#if UNITY_EDITOR
        // Transform.localPositionへの反映だけではScene ViewがPlay前に再描画されないため、
        // 既存コントローラー（TsukuyomiController等）と同様に明示的に再描画を指示する
        UnityEditor.SceneView.RepaintAll();
#endif
    }

    private void ApplyPreview(SusanooFrame[] frames, int index)
    {
        if (frames == null || index < 0 || index >= frames.Length) return;
        ApplyBodySprite(frames[index]);
    }

#if UNITY_EDITOR
    private bool editorAnimRunning;
    private double editorAnimLastTime;
    private int editorAnimFrameIdx;
    private SusanooFrame[] editorAnimFrames;

    private void StartEditorAnim(SusanooFrame[] frames)
    {
        editorAnimFrames = frames;
        editorAnimFrameIdx = 0;
        editorAnimLastTime = UnityEditor.EditorApplication.timeSinceStartup;
        if (!editorAnimRunning)
        {
            editorAnimRunning = true;
            UnityEditor.EditorApplication.update += OnEditorUpdate;
        }
        if (frames != null && frames.Length > 0) ApplyPreview(frames, 0);
    }

    private void StopEditorAnim()
    {
        if (!editorAnimRunning) return;
        editorAnimRunning = false;
        UnityEditor.EditorApplication.update -= OnEditorUpdate;
    }

    private void OnEditorUpdate()
    {
        if (this == null || !editorAnimRunning) { StopEditorAnim(); return; }
        if (editorAnimFrames == null || editorAnimFrames.Length == 0) { StopEditorAnim(); return; }

        double dur = Mathf.Max(0.01f, editorAnimFrames[editorAnimFrameIdx % editorAnimFrames.Length].duration);
        double now = UnityEditor.EditorApplication.timeSinceStartup;
        if (now - editorAnimLastTime >= dur)
        {
            editorAnimLastTime = now;
            editorAnimFrameIdx = (editorAnimFrameIdx + 1) % editorAnimFrames.Length;
            ApplyPreview(editorAnimFrames, editorAnimFrameIdx);
            UnityEditor.SceneView.RepaintAll();
        }
    }

    private void OnDestroy()
    {
        StopEditorAnim();
    }
#endif

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.7f, 0.7f, 0.5f);
        Gizmos.DrawWireCube(wanderAreaCenter, new Vector3(wanderAreaHalfExtents.x * 2f, wanderAreaHalfExtents.y * 2f, 0f));

        // ★Attack Muzzle Offsetの実際の召喚位置をPlay前でもScene view上で確認できるようにする
        Vector3 basePos = bodySpriteRenderer != null ? bodySpriteRenderer.transform.position : transform.position;
        Vector3 muzzleWorldPos = basePos + (Vector3)attackMuzzleOffset;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(muzzleWorldPos, 0.15f);
        Gizmos.DrawLine(basePos, muzzleWorldPos);
    }
}
