using System.Collections;
using UnityEngine;

/// <summary>
/// Area09 Stage3 中ボス「ツクヨミ」（兄）専用コントローラー。
/// 三日月ボードに乗って画面上側エリアをゆったり漂いながら、弓で矢（直線 or カーブ追尾）を放つ。
/// 弟「スサノオ」とは別Prefab・別コントローラーで、HPは共有しない（それぞれ独自のEnemyStatsを持ち、
/// 片方が0になったらもう片方も強制的に倒す「連動方式」。連動処理はスサノオ側で実装する）。
/// 既存コントローラークラスは共有せず、本ファイルに専用実装している。
/// 人物本体(bodySpriteRenderer)と三日月ボード(boardSpriteRenderer)は別のSpriteRendererとして
/// 独立させ、それぞれ別のアニメーション（人物：待機/構え/放つ、ボード：浮遊ループ）を再生する。
/// </summary>
[RequireComponent(typeof(EnemyStats))]
public class TsukuyomiController : MonoBehaviour
{
    private enum PreviewSprite
    {
        Idle1, Idle2, Idle3, Idle4, Idle5, Idle6, IdleAnimate,
        Draw1, Draw2, Draw3, Draw4, DrawAnimate,
        Release1, Release2, ReleaseAnimate,
        Board1, Board2, BoardAnimate,
        DrillSpinAnimate,
        StraightSummon1, StraightSummon2, StraightSummon3, StraightSummon4, StraightSummonAnimate,
        CurveLeftSummon1, CurveLeftSummon2, CurveLeftSummon3, CurveLeftSummon4, CurveLeftSummonAnimate,
        CurveRightSummon1, CurveRightSummon2, CurveRightSummon3, CurveRightSummon4, CurveRightSummon5, CurveRightSummonAnimate
    }

    [System.Serializable]
    public class TsukuyomiFrame
    {
        public Sprite sprite;
        [Tooltip("表示秒数")]
        public float duration = 0.15f;
        [Tooltip("このコマの表示位置の微調整（ローカル座標オフセット）。生成画像ごとの位置ズレ補正用")]
        public Vector2 offset = Vector2.zero;
        [Tooltip("このコマの表示サイズの微調整（X/Y独立の拡縮倍率、基準は1）。生成画像ごとのサイズズレ補正用")]
        public Vector2 scale = Vector2.one;
        [Tooltip("このフレームで矢を発射する場合の、矢の発射位置（bodySpriteRendererのローカル座標オフセット）。" +
                 "Release Fire Frameで指定したコマでのみ使用される")]
        public Vector2 muzzleOffset;
    }

    /// <summary>
    /// Straight/Curve共通の複数弾召喚パターンのインターフェース。何発をどの位置に召喚し、何秒後に
    /// それぞれ発射するかを1パターンとして定義する。複数パターンをweightで重み付きランダム抽選する。
    /// ResolveOffset()は「ランダム化する軸」がStraight（Y方向）とCurve（X方向）で異なるため、
    /// 実装クラス側で毎回計算し直す（呼ぶたびに新しい乱数を引く想定）。
    /// </summary>
    public interface IMultiSummonPattern
    {
        float Weight { get; }
        /// <summary>要素数がこのパターンの弾数になる</summary>
        Vector2[] SummonOffsets { get; }
        float[] FireDelaysFromPrevious { get; }
        Vector2 ResolveOffset(int index);
    }

    /// <summary>
    /// Straight弾専用の複数弾召喚パターン。X方向は固定、Y方向はSummon Offset Y Min/Maxの範囲内で
    /// 弾ごとに毎回ランダムに決定する。
    /// </summary>
    [System.Serializable]
    public class StraightFirePattern : IMultiSummonPattern
    {
        [Tooltip("このパターンが選ばれる重み（他のパターンとの相対値。大きいほど選ばれやすい）")]
        public float weight = 1f;
        [Tooltip("各弾を頭上に召喚するローカル座標オフセット（bodySpriteRenderer基準）。要素数がこのパターンの弾数になる。" +
                 "Y値はここでは使わず、下のSummon Offset Y Min/Maxの範囲内で弾ごとに毎回ランダムに決定する")]
        [NonReorderable]
        public Vector2[] summonOffsets;
        [Tooltip("召喚位置のY方向のランダム範囲の最小値（ローカル座標、bodySpriteRenderer基準）")]
        public float summonOffsetYMin = 1.3f;
        [Tooltip("召喚位置のY方向のランダム範囲の最大値（ローカル座標、bodySpriteRenderer基準）")]
        public float summonOffsetYMax = 1.7f;
        [Tooltip("各弾の発射タイミング（秒）。1発目は召喚してからの秒数、2発目以降は直前の弾を発射してからの秒数")]
        [NonReorderable]
        public float[] fireDelaysFromPrevious;

        public float Weight => weight;
        public Vector2[] SummonOffsets => summonOffsets;
        public float[] FireDelaysFromPrevious => fireDelaysFromPrevious;
        public Vector2 ResolveOffset(int index) => new Vector2(summonOffsets[index].x, Random.Range(summonOffsetYMin, summonOffsetYMax));
    }

    /// <summary>
    /// Curve弾専用の複数弾召喚パターン。Straightとは逆に、Y方向は固定、X方向はSummon Offset X Min/Max
    /// の範囲内で弾ごとに毎回ランダムに決定する。
    /// </summary>
    [System.Serializable]
    public class CurveFirePattern : IMultiSummonPattern
    {
        [Tooltip("このパターンが選ばれる重み（他のパターンとの相対値。大きいほど選ばれやすい）")]
        public float weight = 1f;
        [Tooltip("各弾を頭上に召喚するローカル座標オフセット（bodySpriteRenderer基準）。要素数がこのパターンの弾数になる。" +
                 "X値はここでは使わず、下のSummon Offset X Min/Maxの範囲内で弾ごとに毎回ランダムに決定する")]
        [NonReorderable]
        public Vector2[] summonOffsets;
        [Tooltip("召喚位置のX方向のランダム範囲の最小値（ローカル座標、bodySpriteRenderer基準）")]
        public float summonOffsetXMin = -0.6f;
        [Tooltip("召喚位置のX方向のランダム範囲の最大値（ローカル座標、bodySpriteRenderer基準）")]
        public float summonOffsetXMax = 0.6f;
        [Tooltip("各弾の発射タイミング（秒）。1発目は召喚してからの秒数、2発目以降は直前の弾を発射してからの秒数")]
        [NonReorderable]
        public float[] fireDelaysFromPrevious;

        public float Weight => weight;
        public Vector2[] SummonOffsets => summonOffsets;
        public float[] FireDelaysFromPrevious => fireDelaysFromPrevious;
        public Vector2 ResolveOffset(int index) => new Vector2(Random.Range(summonOffsetXMin, summonOffsetXMax), summonOffsets[index].y);
    }

    // =========================================================
    // References
    // =========================================================

    [Header("References")]
    [SerializeField] private SpriteRenderer bodySpriteRenderer;
    [Tooltip("三日月ボードのSpriteRenderer（子オブジェクト想定）")]
    [SerializeField] private SpriteRenderer boardSpriteRenderer;
    [SerializeField] private EnemyStats enemyStats;
    [Tooltip("B4デバフ（Skill_B4_EnemySpeedDown）が正しく反映されるように速度倍率のみ参照する。" +
             "実際の移動はEnemyMoverではなくこのコントローラーが行うため、Awake()でsuppressMovement=trueにする")]
    [SerializeField] private EnemyMover enemyMover;
    [Tooltip("矢の発射はこのコントローラーがアニメーションに合わせて手動で行うため、" +
             "EnemyShooter自体の自動発射ループはAwake()で無効化する。EnemyData/BulletPrefab/" +
             "ProjectileRootの参照元としてのみ利用する")]
    [SerializeField] private EnemyShooter enemyShooter;
    [SerializeField] private EnemySpriteSwapper spriteSwapper;
    [SerializeField] private EnemyBullet bulletPrefab;
    [SerializeField] private Transform projectileRoot;
    [SerializeField] private float ignoreOwnerTime = 0.15f;

    [Header("Fade In（EnemyStats.FadeInはEnemyStatsと同じGameObject上のSpriteRendererしか対象にできないため、" +
             "bodySpriteRendererが子オブジェクト「Body」側にあるTsukuyomiには効かない。専用に実装している）")]
    [Tooltip("出現時、bodySpriteRenderer/boardSpriteRendererを透明から不透明にフェードインさせる秒数")]
    [SerializeField] private float initialFadeInDuration = 4f;

    [Header("Debug")]
    [SerializeField] private bool showDebugLog = false;

    [Header("Editor Preview（Play前にInspectorでスプライト確認）")]
    [SerializeField] private PreviewSprite previewSprite = PreviewSprite.Idle1;

    // =========================================================
    // Sprites
    // =========================================================

    [Header("Sprites - Idle（浮遊しながらの待機、ループ再生）")]
    [NonReorderable]
    [SerializeField] private TsukuyomiFrame[] idleFrames;

    [Header("Sprites - Draw（弓を構えて狙いを定める。1回だけ再生してReleaseへ）")]
    [NonReorderable]
    [SerializeField] private TsukuyomiFrame[] drawFrames;

    [Header("Sprites - Release（矢を放つ。指定コマで実際に発射）")]
    [NonReorderable]
    [SerializeField] private TsukuyomiFrame[] releaseFrames;
    [Tooltip("releaseFramesの何コマ目(0始まり)を表示した時点で矢を発射するか")]
    [SerializeField] private int releaseFireFrame = 0;

    [Header("Sprites - Board（三日月ボード。人物本体とは独立してループ再生）")]
    [NonReorderable]
    [SerializeField] private TsukuyomiFrame[] boardFrames;

    [Header("Sprites - Straight Summon（合掌して弾を召喚する専用アニメーション。Straightが選ばれた時だけ" +
             "drawFrames/releaseFramesの代わりに再生され、1コマ目の表示と同時に弾を召喚する）")]
    [NonReorderable]
    [SerializeField] private TsukuyomiFrame[] straightSummonFrames;

    [Header("Sprites - Curve Left Summon（片手を左へ伸ばして弾を召喚する専用アニメーション。Curveが選ばれ、" +
             "かつ左カーブに決まった時だけdrawFrames/releaseFramesの代わりに再生される）")]
    [NonReorderable]
    [SerializeField] private TsukuyomiFrame[] curveLeftSummonFrames;

    [Header("Sprites - Curve Right Summon（片手を右へ伸ばして弾を召喚する専用アニメーション。Curveが選ばれ、" +
             "かつ右カーブに決まった時だけdrawFrames/releaseFramesの代わりに再生される）")]
    [NonReorderable]
    [SerializeField] private TsukuyomiFrame[] curveRightSummonFrames;

    // =========================================================
    // Movement - Wander（画面上側エリア内をゆったり漂う）
    // =========================================================

    [Header("Movement - Wander（画面上側エリア内をゆったり漂う）")]
    [Tooltip("漂う速さ（Unity単位/秒）")]
    [SerializeField] private float wanderSpeed = 0.6f;
    [Tooltip("進行方向のふらつき旋回速度（度/秒）")]
    [SerializeField] private float wanderTurnSpeed = 25f;
    [Tooltip("ノイズをサンプリングする時間の速さ。大きいほど方向転換の周期が短くせわしなくなる")]
    [SerializeField] private float wanderNoiseFrequency = 0.15f;
    [Tooltip("漂う範囲の中心（ワールド座標）")]
    [SerializeField] private Vector2 wanderAreaCenter = new Vector2(0f, 3.2f);
    [Tooltip("漂う範囲の半径（X, Y）。この範囲を超えそうになったら中心方向へ操舵を寄せる")]
    [SerializeField] private Vector2 wanderAreaHalfExtents = new Vector2(3.5f, 1.2f);
    [Tooltip("範囲を超えそうな時に中心方向へ向きを戻す旋回速度（度/秒）")]
    [SerializeField] private float wanderBoundsTurnSpeed = 180f;

    [Header("Movement - Bob（人物の上下ゆらぎ）")]
    [Tooltip("体スプライトが上下に揺れる振れ幅（Unity単位）")]
    [SerializeField] private float bobAmplitude = 0.12f;
    [Tooltip("体の上下ゆれの周波数（1秒あたりの往復回数）")]
    [SerializeField] private float bobFrequency = 0.5f;

    [Header("Movement - Board Tilt（ボードの左右の傾き）")]
    [Tooltip("ボードスプライトが左右に傾く角度の振れ幅（度）")]
    [SerializeField] private float boardTiltAmplitude = 6f;
    [Tooltip("ボードの傾きの周波数（1秒あたりの往復回数）")]
    [SerializeField] private float boardTiltFrequency = 0.4f;

    // =========================================================
    // Attack Cycle
    // =========================================================

    [Header("Attack Cycle")]
    [SerializeField] private float attackIntervalMin = 2.5f;
    [SerializeField] private float attackIntervalMax = 4f;

    [Header("Phase（HP閾値による前半/後半の切り替え）")]
    [Tooltip("HP割合(%)がこの値を下回ったら後半フェーズへ移行する（一度移行したら前半には戻らない）")]
    [SerializeField] private float phaseTransitionHpThreshold = 70f;

    [Header("Bullet Spin Animation（結晶ドリルの回転コマ送り）")]
    [Tooltip("ドリル弾の回転を表現するコマ送りスプライト（8枚推奨）。" +
             "1枚絵のZ回転だと扇風機のような見た目になるため、コマ送りで軸回転しているように見せる")]
    [NonReorderable]
    [SerializeField] private Sprite[] drillSpinFrames;
    [Tooltip("drillSpinFrames全体を1周させる速さ（1秒間に何周するか）")]
    [SerializeField] private float drillSpinRotationsPerSecond = 2f;

    [Header("Straight Multi-Summon Pattern（直進弾の複数召喚パターン）")]
    [Tooltip("enemyData.bulletTypesの中で「Straight」に対応する要素番号。この番号が選ばれた時だけ" +
             "下のstraightFirePatternsによる複数召喚演出を行う（それ以外の弾種は従来通り即発射）")]
    [SerializeField] private int straightBulletTypeIndex = 15;
    [Tooltip("Straightが選ばれた時、この中から重み付きランダムで1パターン選んで実行する")]
    [NonReorderable]
    [SerializeField] private StraightFirePattern[] straightFirePatterns;
    [Tooltip("前半フェーズで選択可能なパターン数（配列の先頭からこの数まで）。後半フェーズでは配列の全パターンが選択可能になる")]
    [SerializeField] private int straightFrontPhaseCount = 2;
    [Tooltip("召喚された弾がフェードイン（透明→不透明）する秒数")]
    [SerializeField] private float summonFadeInSeconds = 0.5f;
    [Tooltip("弾が召喚された瞬間に鳴らすSE")]
    [SerializeField] private AudioClip summonSeClip;
    [Range(0f, 1f)]
    [SerializeField] private float summonSeVolume = 1f;
    [Tooltip("召喚された弾が実際に発射される瞬間に鳴らすSE")]
    [SerializeField] private AudioClip launchSeClip;
    [Range(0f, 1f)]
    [SerializeField] private float launchSeVolume = 1f;

    [Header("Curve Multi-Summon Pattern（曲がる弾の複数召喚パターン）")]
    [Tooltip("enemyData.bulletTypesの中で「Curve」に対応する要素番号。この番号が選ばれた時だけ" +
             "下のCurve Fire Patterns Left/Rightによる複数召喚演出を行う（Straightと同じ仕組み）")]
    [SerializeField] private int curveBulletTypeIndex = 16;
    [Tooltip("Curveが選ばれた時、左右をまず50%ずつの確率で決め、そちらの配列の中から重み付きランダムで" +
             "1パターン選んで実行する。フェードイン秒数・召喚/発射SEはStraightと共通のものを使う")]
    [NonReorderable]
    [SerializeField] private CurveFirePattern[] curveFirePatternsLeft;
    [NonReorderable]
    [SerializeField] private CurveFirePattern[] curveFirePatternsRight;
    [Tooltip("前半フェーズで選択可能なパターン数（Left/Right共通、配列の先頭からこの数まで）。後半フェーズでは配列の全パターンが選択可能になる")]
    [SerializeField] private int curveFrontPhaseCount = 1;

    [Header("Enhanced Bullet（後半限定・強化弾）")]
    [Tooltip("後半フェーズでStraight/Curveが選ばれた時、この確率(0〜1)で強化弾になる。" +
             "既に画面上に強化弾が1発でも存在する間は、この確率に関わらず絶対に抽選しない")]
    [Range(0f, 1f)]
    [SerializeField] private float enhancedBulletChance = 0.2f;
    [Tooltip("強化弾のPinned Reflect Required Hitsに加算する固定値")]
    [SerializeField] private int enhancedRequiredHitsBonus = 5;
    [Tooltip("強化弾の見た目の拡大率（通常サイズに対する倍率）")]
    [SerializeField] private float enhancedScaleMultiplier = 1.3f;
    [Tooltip("強化弾に乗せる色味（紅色オーラ等）")]
    [SerializeField] private Color enhancedTintColor = new Color(1f, 0.25f, 0.25f, 1f);
    [Tooltip("強化弾専用トレイルの色（フェード先は自動的に透明になる）")]
    [SerializeField] private Color enhancedTrailColor = new Color(1f, 0.2f, 0.2f, 0.8f);
    [Tooltip("強化弾専用トレイルが残る秒数")]
    [SerializeField] private float enhancedTrailTime = 0.3f;
    [Tooltip("強化弾専用トレイルの太さ（先端）")]
    [SerializeField] private float enhancedTrailWidth = 0.15f;

    // =========================================================
    // Runtime state
    // =========================================================

    private EnemyData enemyData;
    private float headingDeg;
    private float noiseSeed;
    private float bobPhase;
    private float boardTiltPhase;
    private Vector2 currentBodyFrameOffset;
    private Vector2 currentBoardFrameOffset;

    // ★HP閾値によるフェーズ切り替え（GuardBeast/ArcGuard等の既存実装と同じ「一度きりフラグ＋
    //   現在フェーズ確認」の二重ガードで、回復等でHPが閾値を跨ぎ直しても前半へ逆戻りしないようにする
    private enum Phase { Front, Back }
    private Phase phase = Phase.Front;
    private bool phaseTransitioned;

    // ★強化弾は画面上に同時に1発までしか存在してはいけない。UnityのUnityEngine.Objectは
    //   破棄されると==nullがtrueになる仕様を利用し、破棄後は自動的に「いない」扱いに戻る
    //   （明示的なクリア処理は不要）
    private EnemyBullet currentEnhancedBullet;

    private Coroutine attackCoroutine;
    private Coroutine idleLoopCoroutine;
    private Coroutine boardLoopCoroutine;

    /// <summary>出現時のフェードインが完了するまでtrueにならない。徘徊移動・攻撃開始を待たせるためのガード</summary>
    private bool spawnFadeInComplete;

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
        if (enemyShooter == null) enemyShooter = GetComponent<EnemyShooter>();

        if (enemyMover == null) enemyMover = GetComponent<EnemyMover>();
        // 実際の移動はこのコントローラーが独自に行うため、EnemyMover自体の移動処理は止める
        // （B4デバフの倍率はSpeedMultiplierプロパティ経由でApplyWander側から参照する）
        if (enemyMover != null) enemyMover.suppressMovement = true;

        // 発射はこのコントローラーが演出に合わせて手動で行うため、自動発射ループは止める
        if (enemyShooter != null) enemyShooter.enabled = false;

        // ★ボードの基準位置をInspector設定値からキャッシュしておく（BoardFrames未使用時のフォールバック）。
        //   ApplyBoardSprite()が呼ばれた場合はそちらでこの値が上書きされる
        if (boardSpriteRenderer != null) currentBoardFrameOffset = boardSpriteRenderer.transform.localPosition;
    }

    private void Start()
    {
        if (!Application.isPlaying) return;

        // EnemySpawnerはInstantiate直後（Awake実行後）にshooter.SetProjectileRoot()等で値を注入するため、
        // Awake()で読み取ると注入前の値（null）を掴んでしまう。ArcGuardController等と同様にStart()で読み取る
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
        noiseSeed = Random.Range(0f, 1000f);
        headingDeg = Random.Range(0f, 360f);
        bobPhase = Random.Range(0f, Mathf.PI * 2f);
        boardTiltPhase = Random.Range(0f, Mathf.PI * 2f);

        StartIdleFrameLoop();
        StartBoardFrameLoop();

        spawnFadeInComplete = false;
        StartCoroutine(InitialFadeInThenActivate());
    }

    /// <summary>
    /// フェードインが完了するまで、徘徊移動（ApplyWander、Update()側でガード）・通常攻撃を
    /// 一切開始させない。フェードイン完了後に攻撃ループを起動する
    /// </summary>
    private IEnumerator InitialFadeInThenActivate()
    {
        yield return StartCoroutine(InitialFadeIn());
        spawnFadeInComplete = true;

        if (attackCoroutine != null) StopCoroutine(attackCoroutine);
        attackCoroutine = StartCoroutine(AttackLoop());
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

    private void OnDisable()
    {
        StopAllCoroutines();
        attackCoroutine = null;
        idleLoopCoroutine = null;
        boardLoopCoroutine = null;
    }

    private void Update()
    {
        float dt = Time.deltaTime * TimeScale;
        if (spawnFadeInComplete) ApplyWander(dt);
        ApplyBob(dt);
        ApplyBoardTilt(dt);
        CheckPhaseTransition();
    }

    /// <summary>
    /// HP割合がphaseTransitionHpThreshold(%)を下回ったら後半フェーズへ移行する。
    /// phaseTransitioned（一度きりフラグ）とphase==Backの二重ガードにより、
    /// セルフヒール等でHPが閾値を跨ぎ直しても前半フェーズへは戻らない
    /// （GuardBeastController等の既存実装と同じガード方式）。
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

    // =========================================================
    // Movement
    // =========================================================

    private void ApplyWander(float dt)
    {
        float noise = Mathf.PerlinNoise(noiseSeed, Time.time * wanderNoiseFrequency) * 2f - 1f;
        headingDeg += noise * wanderTurnSpeed * dt;

        Vector2 pos = transform.position;
        Vector2 toCenter = wanderAreaCenter - pos;

        if (Mathf.Abs(toCenter.x) > wanderAreaHalfExtents.x || Mathf.Abs(toCenter.y) > wanderAreaHalfExtents.y)
        {
            float pullAngle = Mathf.Atan2(toCenter.y, toCenter.x) * Mathf.Rad2Deg;
            headingDeg = Mathf.MoveTowardsAngle(headingDeg, pullAngle, wanderBoundsTurnSpeed * dt);
        }

        Vector2 moveDir = new Vector2(Mathf.Cos(headingDeg * Mathf.Deg2Rad), Mathf.Sin(headingDeg * Mathf.Deg2Rad));
        transform.position = pos + moveDir * wanderSpeed * SpeedMultiplier * dt;
    }

    private void ApplyBob(float dt)
    {
        bobPhase += dt * bobFrequency * Mathf.PI * 2f;
        float bobOffset = Mathf.Sin(bobPhase) * bobAmplitude;

        if (bodySpriteRenderer != null)
        {
            Vector3 lp = bodySpriteRenderer.transform.localPosition;
            lp.x = currentBodyFrameOffset.x;
            lp.y = currentBodyFrameOffset.y + bobOffset;
            bodySpriteRenderer.transform.localPosition = lp;
        }

        // ★ボードは体と別オブジェクトのため、同じbobOffsetを与えないと体だけが上下に動いて
        //   ボードは静止したままになり、足がボードに対してズレて見えてしまう（実際にあった不具合）。
        //   同じbobPhase由来の値を使うことで、体とボードが常に完全に同じ上下量で動く
        if (boardSpriteRenderer != null)
        {
            Vector3 blp = boardSpriteRenderer.transform.localPosition;
            blp.x = currentBoardFrameOffset.x;
            blp.y = currentBoardFrameOffset.y + bobOffset;
            boardSpriteRenderer.transform.localPosition = blp;
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
    // Idle / Board ループ再生
    // =========================================================

    private void StartIdleFrameLoop()
    {
        if (idleLoopCoroutine != null) StopCoroutine(idleLoopCoroutine);
        idleLoopCoroutine = StartCoroutine(FrameLoop(idleFrames, ApplyBodySprite));
    }

    private void StartBoardFrameLoop()
    {
        if (boardLoopCoroutine != null) StopCoroutine(boardLoopCoroutine);
        boardLoopCoroutine = StartCoroutine(FrameLoop(boardFrames, ApplyBoardSprite));
    }

    private IEnumerator FrameLoop(TsukuyomiFrame[] frames, System.Action<TsukuyomiFrame> apply)
    {
        if (frames == null || frames.Length == 0) yield break;
        int i = 0;
        while (true)
        {
            TsukuyomiFrame f = frames[i % frames.Length];
            apply(f);
            yield return new WaitForSeconds(Mathf.Max(0.01f, f != null ? f.duration : 0.15f));
            i++;
        }
    }

    private void ApplyBodySprite(TsukuyomiFrame f)
    {
        if (bodySpriteRenderer == null || f == null || f.sprite == null) return;
        bodySpriteRenderer.sprite = f.sprite;
        bodySpriteRenderer.transform.localScale = new Vector3(f.scale.x, f.scale.y, 1f);
        // ApplyBob()が毎フレームlocalPositionを上書きするため、このコマのオフセットは
        // キャッシュしておいてApplyBob側で毎フレーム合成する（Play中の上下ゆらぎと合成するため）
        currentBodyFrameOffset = f.offset;
        // Play前のScene編集中はUpdate()（ApplyBob）が回らないため、ここでも直接位置に反映する。
        // これによりInspectorでoffsetを変更した瞬間にSceneへ反映される
        Vector3 lp = bodySpriteRenderer.transform.localPosition;
        lp.x = f.offset.x;
        lp.y = f.offset.y;
        bodySpriteRenderer.transform.localPosition = lp;
        // EnemySpriteSwapperが被弾ヒットフラッシュ終了後に古いスプライトへ巻き戻すのを防ぐため、
        // 「通常スプライト」のキャッシュを常に最新のフレームへ同期させる
        if (spriteSwapper != null) spriteSwapper.SetBaseSprite(f.sprite);
    }

    private void ApplyBoardSprite(TsukuyomiFrame f)
    {
        if (boardSpriteRenderer == null || f == null || f.sprite == null) return;
        boardSpriteRenderer.sprite = f.sprite;
        boardSpriteRenderer.transform.localScale = new Vector3(f.scale.x, f.scale.y, 1f);
        // ApplyBob()が毎フレームlocalPositionを上書きするため、このコマのオフセットは
        // キャッシュしておいてApplyBob側で毎フレーム合成する（体と同じ上下ゆらぎ量を適用するため）
        currentBoardFrameOffset = f.offset;
        boardSpriteRenderer.transform.localPosition = f.offset;
    }

    // =========================================================
    // Attack Cycle（構え→放つ→待機に戻る）
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

        // ★どの弾種を撃つかは、drawFrames/releaseFramesかstraightSummonFramesかを選ぶために
        //   ここで先に決定しておく必要がある（以前はFireArrow内部で決めていたが、それだと
        //   再生するアニメーションの種類をRunAttack側で判断できなかった）
        int idx = -1;
        EnemyData.BulletType bt = null;
        if (enemyShooter != null && enemyData != null && enemyData.bulletTypes != null && enemyData.bulletTypes.Length > 0)
        {
            idx = enemyShooter.PickBulletTypeIndex(enemyData.bulletTypes.Length);
            if (idx < 0 || idx >= enemyData.bulletTypes.Length) idx = 0;
            bt = enemyData.bulletTypes[idx];
        }

        // ★Curveが選ばれた場合、左右をここで一度だけ決めておく。従来はFireArrow内部で
        //   決めていたが、それだとRunAttack側で「左カーブ専用アニメーションを再生すべきか」を
        //   判断できず、アニメーションと実際の弾の曲がる向きがズレる恐れがあるため、
        //   ここで決めた値をFireArrow側にも強制的に渡す
        bool? curveIsLeft = idx == curveBulletTypeIndex ? (bool?)(Random.value < 0.5f) : null;

        // ★Straightが選ばれた時だけ、合掌して弾を召喚する専用アニメーションを再生する。
        //   1コマ目を表示するのと同時（＝アニメーション開始と同じタイミング）に弾を召喚する
        if (idx == straightBulletTypeIndex && straightSummonFrames != null && straightSummonFrames.Length > 0)
        {
            for (int i = 0; i < straightSummonFrames.Length; i++)
            {
                TsukuyomiFrame f = straightSummonFrames[i];
                ApplyBodySprite(f);
                if (i == 0) FireArrow(idx, bt, Vector2.zero);
                yield return new WaitForSeconds(Mathf.Max(0.01f, f != null ? f.duration : 0.1f));
            }

            StartIdleFrameLoop();
            yield break;
        }

        // ★Curveが選ばれ、かつ左右どちらかに決まった時、それぞれ対応する専用の召喚アニメーションを再生する。
        //   対応する絵が未設定の場合は下のdrawFrames/releaseFramesにフォールバックする
        if (curveIsLeft == true && curveLeftSummonFrames != null && curveLeftSummonFrames.Length > 0)
        {
            for (int i = 0; i < curveLeftSummonFrames.Length; i++)
            {
                TsukuyomiFrame f = curveLeftSummonFrames[i];
                ApplyBodySprite(f);
                if (i == 0) FireArrow(idx, bt, Vector2.zero, curveIsLeft);
                yield return new WaitForSeconds(Mathf.Max(0.01f, f != null ? f.duration : 0.1f));
            }

            StartIdleFrameLoop();
            yield break;
        }

        if (curveIsLeft == false && curveRightSummonFrames != null && curveRightSummonFrames.Length > 0)
        {
            for (int i = 0; i < curveRightSummonFrames.Length; i++)
            {
                TsukuyomiFrame f = curveRightSummonFrames[i];
                ApplyBodySprite(f);
                if (i == 0) FireArrow(idx, bt, Vector2.zero, curveIsLeft);
                yield return new WaitForSeconds(Mathf.Max(0.01f, f != null ? f.duration : 0.1f));
            }

            StartIdleFrameLoop();
            yield break;
        }

        if (drawFrames != null)
        {
            foreach (TsukuyomiFrame f in drawFrames)
            {
                ApplyBodySprite(f);
                yield return new WaitForSeconds(Mathf.Max(0.01f, f != null ? f.duration : 0.1f));
            }
        }

        if (releaseFrames != null)
        {
            for (int i = 0; i < releaseFrames.Length; i++)
            {
                TsukuyomiFrame f = releaseFrames[i];
                ApplyBodySprite(f);
                if (i == releaseFireFrame) FireArrow(idx, bt, f != null ? f.muzzleOffset : Vector2.zero, curveIsLeft);
                yield return new WaitForSeconds(Mathf.Max(0.01f, f != null ? f.duration : 0.1f));
            }
        }

        StartIdleFrameLoop();
    }

    private void FireArrow(int idx, EnemyData.BulletType bt, Vector2 muzzleLocalOffset, bool? forcedCurveIsLeft = null)
    {
        if (FloorHealth.IsBrokenGlobal || PixelDancerController.IsPlayerDeadGlobal) return;
        if (bulletPrefab == null || projectileRoot == null || enemyData == null) return;
        if (bt == null) return;

        // ★強化弾の抽選：後半フェーズでStraight/Curveが選ばれた時のみ、1回の攻撃(FireArrow呼び出し)
        //   ごとに1回だけ抽選する。既に画面上に強化弾が1発でも存在する間は絶対に抽選しない
        bool isStraightOrCurve = (idx == straightBulletTypeIndex || idx == curveBulletTypeIndex);
        bool rollEnhanced = phase == Phase.Back && isStraightOrCurve
            && currentEnhancedBullet == null && Random.value < enhancedBulletChance;

        // ★Straightが選ばれた時だけ、頭上に複数弾を召喚してから時間差で発射する専用パターンへ分岐する。
        //   それ以外の弾種（Curve等）は従来通りその場で即発射する。
        //   前半フェーズは配列先頭のstraightFrontPhaseCount個だけを選択肢にする
        if (idx == straightBulletTypeIndex && straightFirePatterns != null && straightFirePatterns.Length > 0)
        {
            StraightFirePattern[] eligibleStraightPatterns = GetPhaseFilteredPatterns(straightFirePatterns, straightFrontPhaseCount);
            StartCoroutine(RunMultiSummon(bt, idx, eligibleStraightPatterns, null, null, rollEnhanced));
            return;
        }

        // ★Curveが選ばれた時も同様に複数召喚パターンへ分岐する。まず左右を50%ずつで決め、
        //   その側のパターン配列が設定されている場合だけ実行する（未設定なら従来通り即発射）。
        //   こちらも前半フェーズは配列先頭のcurveFrontPhaseCount個だけを選択肢にする
        if (idx == curveBulletTypeIndex)
        {
            // ★左右は原則RunAttack側で既に決定済みの値（forcedCurveIsLeft）をそのまま使う。
            //   直接FireArrowが呼ばれる経路（forcedCurveIsLeft未指定）のために、その場合だけ自前で抽選する
            bool isLeft = forcedCurveIsLeft ?? (Random.value < 0.5f);
            CurveFirePattern[] curvePatternsFull = isLeft ? curveFirePatternsLeft : curveFirePatternsRight;
            CurveFirePattern[] curvePatterns = GetPhaseFilteredPatterns(curvePatternsFull, curveFrontPhaseCount);
            if (curvePatterns != null && curvePatterns.Length > 0)
            {
                // ★召喚直後：ApplyBulletTypeToEnemyBullet()が自動で開始してしまうMissile Arcを
                //   一旦止める（待機中にタイマーが進んでしまい、発射前に軌道計算が終わってしまう不具合対策）。
                //   実際に発射される瞬間（onLaunch）に、正しい左右の符号で改めて開始する
                StartCoroutine(RunMultiSummon(bt, idx, curvePatterns,
                    spawnedBullet => spawnedBullet.ClearMissileArc(),
                    spawnedBullet => ForceMissileArcDirection(spawnedBullet, bt, isLeft),
                    rollEnhanced));
                return;
            }
        }

        Vector3 muzzleWorldPos = bodySpriteRenderer != null
            ? bodySpriteRenderer.transform.TransformPoint(muzzleLocalOffset)
            : transform.position;

        Vector2 dir = ComputeAimDirection(muzzleWorldPos);

        EnemyBullet bullet = SpawnConfiguredBullet(bt, muzzleWorldPos);
        bullet.SetDirection(dir);
        if (rollEnhanced) ApplyEnhancedBulletEffects(bullet, bt);

        if (showDebugLog) Debug.Log($"[TsukuyomiController] FireArrow idx={idx} useMissileArc={bt.useMissileArc} dir={dir}", this);
    }

    /// <summary>
    /// 弾のInstantiate〜BulletType適用〜オーナー衝突無視〜ドリル回転コマ演出の付与までをまとめた共通処理。
    /// 即発射・時間差発射（Straight複数召喚）の両方から呼ばれる。方向(SetDirection)は呼び出し側で設定する。
    /// </summary>
    private EnemyBullet SpawnConfiguredBullet(EnemyData.BulletType bt, Vector3 worldPos)
    {
        EnemyBullet bullet = Instantiate(bulletPrefab, worldPos, Quaternion.identity, projectileRoot);
        // fallbackSpeed/fallbackLifetimeはBullet Types側のSpeed/Life Timeが未設定(0以下)の時だけ使われる保険値。
        // Bullet Types側で必ず設定する運用のため、Inspectorに重複項目は出さず安全な固定値を直接渡す
        EnemyShooter.ApplyBulletTypeToEnemyBullet(bullet, bt, 1f, 5f, null, bulletPrefab, projectileRoot);

        foreach (Collider2D col in GetComponentsInChildren<Collider2D>())
            if (col != null) bullet.SetOwnerCollisionIgnore(col, ignoreOwnerTime);

        if (drillSpinFrames != null && drillSpinFrames.Length > 0)
        {
            DrillSpinBullet spin = bullet.gameObject.AddComponent<DrillSpinBullet>();
            spin.Configure(drillSpinFrames, drillSpinRotationsPerSecond);
        }

        return bullet;
    }

    /// <summary>
    /// 渡されたpatterns配列から重み付きランダムで1パターンを選び、そのパターンが定義する数の弾を
    /// 発射順に1発ずつ召喚する（全弾を最初に同時召喚するのではなく、直前の弾が発射されたタイミングで
    /// 次の弾を召喚する）。各弾は着弾判定・当たり判定を持たない待機状態のままPendingSummonBulletに
    /// 制御を委ね、自分自身の召喚から Fire Delays From Previous[i] 秒後に本当に発射される
    /// （狙う方向は発射の瞬間に毎回計算し直す＝既存のFireArrowと同じ仕様を維持）。
    /// Straight/Curve共通の処理で、StraightはonSpawned/onLaunch=null、Curveは召喚直後に
    /// Missile Arcを一旦止める処理をonSpawned、実際の発射時にカーブ方向を確定させてMissile Arcを
    /// 開始する処理をonLaunch経由で差し込む。
    /// allowEnhancedがtrueの場合、パターンの弾数が決まった時点でその中の1発だけをランダムに選び、
    /// 強化弾にする（同じ攻撃内で2発以上が強化弾になることはない）。
    /// </summary>
    private IEnumerator RunMultiSummon<T>(EnemyData.BulletType bt, int idx, T[] patterns,
        System.Action<EnemyBullet> onSpawned, System.Action<EnemyBullet> onLaunch, bool allowEnhanced = false) where T : class, IMultiSummonPattern
    {
        T pattern = PickPattern(patterns);
        if (pattern == null || pattern.SummonOffsets == null || pattern.SummonOffsets.Length == 0) yield break;

        int enhancedShotIndex = allowEnhanced ? Random.Range(0, pattern.SummonOffsets.Length) : -1;

        for (int i = 0; i < pattern.SummonOffsets.Length; i++)
        {
            float delay = (pattern.FireDelaysFromPrevious != null && i < pattern.FireDelaysFromPrevious.Length)
                ? Mathf.Max(0f, pattern.FireDelaysFromPrevious[i])
                : 0f;

            // ★ランダム化する軸（Straight=Y、Curve=X）はパターン側のResolveOffset()に委ねる。
            //   呼ぶたびに新しい乱数を引くため、同じパターン内の複数弾がそれぞれ違う値になる
            Vector2 offset = pattern.ResolveOffset(i);

            // ★TransformPoint()は使わない：発射(release)フレームはbodySpriteRendererのlocalScaleを
            //   (0,0)にする演出になっており、TransformPointだとscale=0でどのoffsetも同じ座標へ
            //   潰れてしまう（実際に発生した不具合）。回転は使っていない階層なので、ワールド座標に
            //   オフセットをそのまま加算するだけで正しい位置になる
            Vector3 summonWorldPos = bodySpriteRenderer != null
                ? bodySpriteRenderer.transform.position + (Vector3)offset
                : transform.position;

            EnemyBullet bullet = SpawnConfiguredBullet(bt, summonWorldPos);
            onSpawned?.Invoke(bullet);
            if (i == enhancedShotIndex) ApplyEnhancedBulletEffects(bullet, bt);

            PendingSummonBullet pending = bullet.gameObject.AddComponent<PendingSummonBullet>();
            pending.Configure(delay, summonFadeInSeconds, () => ComputeAimDirection(bullet.transform.position),
                summonSeClip, summonSeVolume, launchSeClip, launchSeVolume, onLaunch);

            if (showDebugLog) Debug.Log($"[TsukuyomiController] RunMultiSummon idx={idx} shot={i} delay={delay}", this);

            // ★次の弾は、この弾が実際に発射されるタイミング（＝この待ち時間が経過した時）に召喚する
            yield return new WaitForSeconds(delay);
        }
    }

    /// <summary>
    /// 現在のフェーズに応じて選択可能なパターンだけに絞った配列を返す。前半フェーズは配列先頭の
    /// frontPhaseCount個だけ、後半フェーズ（phase==Back）では配列全体を返す。
    /// </summary>
    private T[] GetPhaseFilteredPatterns<T>(T[] patterns, int frontPhaseCount) where T : class, IMultiSummonPattern
    {
        if (patterns == null) return null;
        if (phase == Phase.Back) return patterns;

        int count = Mathf.Clamp(frontPhaseCount, 0, patterns.Length);
        if (count == patterns.Length) return patterns;

        T[] slice = new T[count];
        System.Array.Copy(patterns, slice, count);
        return slice;
    }

    private T PickPattern<T>(T[] patterns) where T : class, IMultiSummonPattern
    {
        if (patterns == null || patterns.Length == 0) return null;

        float totalWeight = 0f;
        foreach (T p in patterns)
            if (p != null) totalWeight += Mathf.Max(0f, p.Weight);

        if (totalWeight <= 0f) return patterns[0];

        float roll = Random.Range(0f, totalWeight);
        float acc = 0f;
        foreach (T p in patterns)
        {
            if (p == null) continue;
            acc += Mathf.Max(0f, p.Weight);
            if (roll <= acc) return p;
        }
        return patterns[patterns.Length - 1];
    }

    /// <summary>
    /// Curve Multi-Summonで召喚した弾のカーブ方向を、左右どちらかへ強制する。
    /// EnemyShooter.ApplyBulletTypeToEnemyBullet()はmissileCurveAngleを常にAbs()で渡すため
    /// （符号情報が失われる）、Bullet Type側のmissileCurveRandomDirectionがfalseの場合は常に右カーブに
    /// なってしまう。ここでBullet Typeの他の設定値はそのまま維持しつつ、符号だけ左右で明示的に
    /// 指定してApplyMissileArc()を呼び直す（呼び直しても既存のMissileArcコルーチンは安全に上書きされる）。
    /// </summary>
    private void ForceMissileArcDirection(EnemyBullet bullet, EnemyData.BulletType bt, bool isLeft)
    {
        if (bullet == null || bt == null || !bt.useMissileArc) return;

        float signedAngle = Mathf.Abs(bt.missileCurveAngle) * (isLeft ? -1f : 1f);
        bullet.ApplyMissileArc(bt.missileInitialSpeed, bt.missileStraightDuration, signedAngle, false,
            bt.missileCurveDuration, bt.missileFinalSpeed, bt.missileUseSpeedCurve,
            bt.missileCurveInitialSpeed, bt.missileCurveFinalSpeed, bt.missileSpeedCurve,
            bt.missileUseRandomOffset, bt.missileRandomOffsetRadius);
    }

    /// <summary>
    /// 後半フェーズ限定の「強化弾」演出。Pinned Reflect Required Hitsに固定値を加算し、
    /// 見た目をサイズ拡大＋色味変更＋専用トレイルで通常弾と区別できるようにする。
    /// 呼んだ弾をcurrentEnhancedBulletとして記憶し、次に抽選する時にまだ生きていれば
    /// （UnityのUnityEngine.Objectは破棄後==nullがtrueになる仕様のまま）新規抽選をブロックする。
    /// </summary>
    private void ApplyEnhancedBulletEffects(EnemyBullet bullet, EnemyData.BulletType bt)
    {
        if (bullet == null || bt == null) return;

        currentEnhancedBullet = bullet;

        PinnedReflectBullet pinned = bullet.GetComponent<PinnedReflectBullet>();
        if (pinned != null)
        {
            pinned.Configure(bt.pinnedReflectRequiredHits + enhancedRequiredHitsBonus, bt.pinnedReflectHitInterval,
                bt.pinnedReflectSpinWhilePinned, bt.pinnedReflectSpinSpeed, bt.pinnedReflectCreepSpeed);
        }

        bullet.transform.localScale *= enhancedScaleMultiplier;
        bullet.SetVisualColor(enhancedTintColor);

        // ★自作のTrailRenderer+Materialを新規作成するとシェーダー/マテリアルの相性で正しく描画されない
        //   リスクがあるため使わない。弾プレハブに既にあるTrailRenderer（EnemyBulletFeedback側で
        //   グラデーション適用済みで動作実績がある）をSetUnreflectedTrail()経由でそのまま再利用する
        bullet.SetUnreflectedTrail(enhancedTrailColor, enhancedTrailTime, enhancedTrailWidth, 0f);

        if (showDebugLog) Debug.Log($"[TsukuyomiController] ApplyEnhancedBulletEffects requiredHits={bt.pinnedReflectRequiredHits + enhancedRequiredHitsBonus}", this);
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
            case PreviewSprite.IdleAnimate: StartEditorAnim(idleFrames, false); return;
            case PreviewSprite.DrawAnimate: StartEditorAnim(drawFrames, false); return;
            case PreviewSprite.ReleaseAnimate: StartEditorAnim(releaseFrames, false); return;
            case PreviewSprite.BoardAnimate: StartEditorAnim(boardFrames, true); return;
            case PreviewSprite.DrillSpinAnimate: StartDrillSpinEditorAnim(); return;
            case PreviewSprite.StraightSummonAnimate: StartEditorAnim(straightSummonFrames, false); return;
            case PreviewSprite.CurveLeftSummonAnimate: StartEditorAnim(curveLeftSummonFrames, false); return;
            case PreviewSprite.CurveRightSummonAnimate: StartEditorAnim(curveRightSummonFrames, false); return;
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
            case PreviewSprite.Draw1: ApplyPreview(drawFrames, 0); break;
            case PreviewSprite.Draw2: ApplyPreview(drawFrames, 1); break;
            case PreviewSprite.Draw3: ApplyPreview(drawFrames, 2); break;
            case PreviewSprite.Draw4: ApplyPreview(drawFrames, 3); break;
            case PreviewSprite.Release1: ApplyPreview(releaseFrames, 0); break;
            case PreviewSprite.Release2: ApplyPreview(releaseFrames, 1); break;
            case PreviewSprite.Board1: ApplyPreview(boardFrames, 0, true); break;
            case PreviewSprite.Board2: ApplyPreview(boardFrames, 1, true); break;
            case PreviewSprite.StraightSummon1: ApplyPreview(straightSummonFrames, 0); break;
            case PreviewSprite.StraightSummon2: ApplyPreview(straightSummonFrames, 1); break;
            case PreviewSprite.StraightSummon3: ApplyPreview(straightSummonFrames, 2); break;
            case PreviewSprite.StraightSummon4: ApplyPreview(straightSummonFrames, 3); break;
            case PreviewSprite.CurveLeftSummon1: ApplyPreview(curveLeftSummonFrames, 0); break;
            case PreviewSprite.CurveLeftSummon2: ApplyPreview(curveLeftSummonFrames, 1); break;
            case PreviewSprite.CurveLeftSummon3: ApplyPreview(curveLeftSummonFrames, 2); break;
            case PreviewSprite.CurveLeftSummon4: ApplyPreview(curveLeftSummonFrames, 3); break;
            case PreviewSprite.CurveRightSummon1: ApplyPreview(curveRightSummonFrames, 0); break;
            case PreviewSprite.CurveRightSummon2: ApplyPreview(curveRightSummonFrames, 1); break;
            case PreviewSprite.CurveRightSummon3: ApplyPreview(curveRightSummonFrames, 2); break;
            case PreviewSprite.CurveRightSummon4: ApplyPreview(curveRightSummonFrames, 3); break;
            case PreviewSprite.CurveRightSummon5: ApplyPreview(curveRightSummonFrames, 4); break;
        }

#if UNITY_EDITOR
        // Transform.localPositionへの反映だけではScene ViewがPlay前に再描画されないため、
        // 既存コントローラー（ArcGuardController等）と同様に明示的に再描画を指示する
        UnityEditor.SceneView.RepaintAll();
#endif
    }

    private void ApplyPreview(TsukuyomiFrame[] frames, int index, bool board = false)
    {
        if (frames == null || index < 0 || index >= frames.Length) return;
        if (board) ApplyBoardSprite(frames[index]);
        else ApplyBodySprite(frames[index]);
    }

#if UNITY_EDITOR
    private bool editorAnimRunning;
    private double editorAnimLastTime;
    private int editorAnimFrameIdx;
    private TsukuyomiFrame[] editorAnimFrames;
    private bool editorAnimIsBoard;
    private bool editorAnimIsDrillSpin;
    private Sprite[] editorDrillFrames;
    private double editorDrillFrameInterval;

    private void StartEditorAnim(TsukuyomiFrame[] frames, bool isBoard)
    {
        editorAnimIsDrillSpin = false;
        editorAnimFrames = frames;
        editorAnimIsBoard = isBoard;
        editorAnimFrameIdx = 0;
        editorAnimLastTime = UnityEditor.EditorApplication.timeSinceStartup;
        if (!editorAnimRunning)
        {
            editorAnimRunning = true;
            UnityEditor.EditorApplication.update += OnEditorUpdate;
        }
        if (frames != null && frames.Length > 0) ApplyPreview(frames, 0, isBoard);
    }

    // ドリル弾のコマ送りは専用配列（TsukuyomiFrameではなく単純なSprite[]）かつ、
    // 全コマ固定間隔（1 / (回転数/秒 * コマ数)）なので、既存のTsukuyomiFrameベースのループとは
    // 別経路でEditorApplication.updateを共有する
    private void StartDrillSpinEditorAnim()
    {
        editorAnimIsDrillSpin = true;
        editorDrillFrames = drillSpinFrames;
        editorDrillFrameInterval = (editorDrillFrames != null && editorDrillFrames.Length > 0 && drillSpinRotationsPerSecond > 0f)
            ? 1.0 / (drillSpinRotationsPerSecond * editorDrillFrames.Length)
            : 0.05;
        editorAnimFrameIdx = 0;
        editorAnimLastTime = UnityEditor.EditorApplication.timeSinceStartup;
        if (!editorAnimRunning)
        {
            editorAnimRunning = true;
            UnityEditor.EditorApplication.update += OnEditorUpdate;
        }
        if (editorDrillFrames != null && editorDrillFrames.Length > 0 && bodySpriteRenderer != null)
            bodySpriteRenderer.sprite = editorDrillFrames[0];
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

        if (editorAnimIsDrillSpin)
        {
            if (editorDrillFrames == null || editorDrillFrames.Length == 0 || bodySpriteRenderer == null)
            {
                StopEditorAnim();
                return;
            }
            double nowDrill = UnityEditor.EditorApplication.timeSinceStartup;
            if (nowDrill - editorAnimLastTime >= editorDrillFrameInterval)
            {
                editorAnimLastTime = nowDrill;
                editorAnimFrameIdx = (editorAnimFrameIdx + 1) % editorDrillFrames.Length;
                bodySpriteRenderer.sprite = editorDrillFrames[editorAnimFrameIdx];
                UnityEditor.SceneView.RepaintAll();
            }
            return;
        }

        if (editorAnimFrames == null || editorAnimFrames.Length == 0) { StopEditorAnim(); return; }

        double dur = Mathf.Max(0.01f, editorAnimFrames[editorAnimFrameIdx % editorAnimFrames.Length].duration);
        double now = UnityEditor.EditorApplication.timeSinceStartup;
        if (now - editorAnimLastTime >= dur)
        {
            editorAnimLastTime = now;
            editorAnimFrameIdx = (editorAnimFrameIdx + 1) % editorAnimFrames.Length;
            ApplyPreview(editorAnimFrames, editorAnimFrameIdx, editorAnimIsBoard);
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
        Gizmos.color = new Color(0.7f, 0.7f, 1f, 0.5f);
        Gizmos.DrawWireCube(wanderAreaCenter, new Vector3(wanderAreaHalfExtents.x * 2f, wanderAreaHalfExtents.y * 2f, 0f));
    }
}
