using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Area10最終ボス「NeonDancer（Player分身）」の統括コントローラー（ルート＝Dancer本体に付ける）。
///
/// ■構成
///   ルート（このGameObject）がDancer本体として左右にランダムウォークする。
///   ND_Body（子）：フレームごとのスプライト/位置/回転/当たり判定（EnemyPartでダメージを受ける）
///   ND_Stage（子）：Floor/Light/足元スポット/発射台P1・P3/画面フラッシュ。Start()でルートから切り離して固定し、
///                   EnemyStats.RegisterSubPart()で本体撃破時に一緒に破棄する。
///   ND_Turret_P2（子）：Dancerの左右から撃つ発射台（Dancerと一緒に動く）
///
/// ■HP
///   EnemyStats.Damage()には致死を止めるフックが無いため、前半は「前半HP＋予備（HpReserve、非常に大きい値）」を1本のプールにし、
///   「残りHP ≦ 予備」になった瞬間を前半終了として扱う（1撃で前半HPを大きく超えるダメージでも撃破されない）。
///   後半開始時にEnemyStats.ApplyMaxHp(後半HP)で入れ直すため、前半の超過ダメージは後半に持ち越さない。
///   EnemyDataのmaxHp＝前半HP（スポナーがHP→シールドの順に初期化するため、前半のシールド量は前半HP基準）。
///   HPバーはNeonDancerHealthDisplayがフェーズ内の値で描く。
///
/// ■前半HP0 → 後半（PhaseTransitionRoutine）
///   ① 攻撃停止・前半の弾/ワームホール/煙幕を消去・無敵・デバフ解除 → ダウンアニメ → 魂が放出されて画面下へ落ちる（プレイヤーと同じ挙動）
///   ② 魂を円で救出できなければArea終了（クリア扱いにせず、Result・ジェム画面を出さずにフェードアウトしてAreaSelectへ）。
///      救出したらBGM切替・プレイヤー側（ダンサー・スポットライト・フロア）停止
///   ③ 魂がダウン中のDancerに重なる → ④ 魂が消え、Finishアニメ（最後のコマで停止）
///   ⑤ 虹色の発光パルス → HPバーが満タンまで伸びる・Floor/Light全快・シールド満タン → ⑥ プレイヤー側再開・後半開始
/// </summary>
public class NeonDancerController : MonoBehaviour
{
    public enum NeonDancerPreviewMode
    {
        Dance1, Dance2, Dance3, Dance4,
        Dance1Animate, Dance2Animate, Dance3Animate, Dance4Animate
    }

    [System.Serializable]
    public class NeonDancerFrame
    {
        public Sprite  sprite;
        public Vector2 offset;
        [Tooltip("表示秒数（0以下ならDance Fallback Frame Durationを使う）")]
        public float   duration;
        [Tooltip("マズル位置（Muzzle Pointのローカル座標。後半フェーズ用の予備。現状は未使用）")]
        public Vector2 muzzleOffset;
        [Tooltip("当たり判定サイズ（0,0のときは変更しない）")]
        public Vector2 colliderSize;
        [Tooltip("当たり判定オフセット")]
        public Vector2 colliderOffset;
        [Tooltip("スプライトのZ軸回転（度）")]
        public float   rotationZ;
    }

    // ======================================================
    // Inspector
    // ======================================================

    [Header("References")]
    [Tooltip("ND_Body のSpriteRenderer")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [Tooltip("ND_Body のBoxCollider2D（フレームごとにサイズ/オフセットを書き換える）")]
    [SerializeField] private BoxCollider2D bodyCollider;
    [Tooltip("ND_Body のEnemyPart（後半移行演出中だけダメージ無効にする）")]
    [SerializeField] private EnemyPart bodyPart;
    [SerializeField] private EnemyMover enemyMover;
    [SerializeField] private EnemyStats enemyStats;
    [SerializeField] private EnemyShooter enemyShooter;
    [SerializeField] private EnemySpriteSwapper spriteSwapper;
    [Tooltip("後半フェーズ用の予備（フレームのMuzzle Offsetを適用する先）。未設定なら何もしない")]
    [SerializeField] private Transform muzzlePoint;

    [Header("References - Stage（Start時にルートから切り離して固定する）")]
    [Tooltip("ND_Stage（Floor/Light/足元スポット/発射台P1・P3/画面フラッシュの親）")]
    [SerializeField] private Transform stageRoot;
    [Tooltip("ND_Floor の当たり判定（Dancerが歩く範囲の基準）")]
    [SerializeField] private Collider2D floorCollider;
    [Tooltip("ND_Floor のNeonDancerBarrier")]
    [SerializeField] private NeonDancerBarrier floorBarrier;
    [Tooltip("ND_LightLeft / ND_LightRight")]
    [SerializeField] private NeonDancerLight[] lights;
    [Tooltip("Dancer足元のスポット（ND_FloorSpot）")]
    [SerializeField] private SpriteRenderer floorSpotRenderer;
    [Tooltip("後半移行時の画面フラッシュ（ND_TransitionFlash）")]
    [SerializeField] private SpriteRenderer transitionFlashRenderer;

    [Header("References - Turret / Bullet")]
    [Tooltip("ND_Turret_P1 / P2 / P3")]
    [SerializeField] private NeonDancerTurret[] turrets;
    [Tooltip("ワームホールのPrefab（NeonDancer_Wormhole）")]
    [SerializeField] private NeonDancerWormhole wormholePrefab;
    [Tooltip("ワームホールを事前生成しておく数（足りなければ自動で追加生成）")]
    [SerializeField] private int wormholePoolSize = 6;
    [Tooltip("Bullet Type側のSpeed/Life Timeが0の時に使う弾速")]
    [SerializeField] private float bulletSpeed = 6f;
    [Tooltip("Bullet Type側のSpeed/Life Timeが0の時に使う寿命（秒）")]
    [SerializeField] private float bulletLifeTime = 5f;
    [Tooltip("発射直後、自分（Dancer/Floor/Light）との衝突を無視する秒数")]
    [SerializeField] private float ignoreOwnerTime = 0.15f;

    [Header("Beamの予告（警告SE・吸い込みエフェクト。予兆線はBullet TypeのUse TelegraphがONの弾だけ）")]
    [Tooltip("Beam（または予兆線付きの弾）のワームホールが出た瞬間（溜め開始）に鳴らす警告SE（Bitの溜め開始SE「ロボットの目が光る」）")]
    [SerializeField] private AudioClip telegraphSE;
    [Range(0f, 1f)] [SerializeField] private float telegraphSEVolume = 1f;

    [Header("Drill（ドリル反射の弾だけに付ける回転コマ。TsukuyomiControllerと同じ方式）")]
    [Tooltip("Pinned Reflect（ドリル反射）の弾にだけ使う弾プレハブ（Tsukuyomiと同じEnemyBullet_Tsukuyomi）。\n" +
             "線に留まって中間ヒットするたびのエフェクトは、弾プレハブのEnemyBulletFeedback > Paddle Hit Vfx Prefabから出るため。未設定なら通常の弾プレハブを使う")]
    [SerializeField] private EnemyBullet drillBulletPrefab;
    [Tooltip("Pinned Reflect（ドリル反射）が有効なBullet Typeの弾にだけ、このコマを切り替えて軸回転して見せる（Tsukuyomi_Bullet_DrillSpin1〜8）")]
    [SerializeField] private Sprite[] drillSpinFrames;
    [Tooltip("drillSpinFrames全体を1秒に何周させるか")]
    [SerializeField] private float drillSpinRotationsPerSecond = 2f;

    [Header("Editor Preview")]
    [Tooltip("Dance1〜4：そのパターンの1コマを表示（下のPreview Frameで何コマ目かを選ぶ）\nDanceNAnimate：そのパターンを連続再生")]
    [SerializeField] private NeonDancerPreviewMode previewMode = NeonDancerPreviewMode.Dance1;
    [Tooltip("Dance1〜4選択時に表示するコマ番号（0始まり。コマ数を超えた値は最後のコマになる）")]
    [Range(0, 200)]
    [SerializeField] private int previewFrameIndex = 0;

    [Header("Dance Import（プレイヤーのダンスクリップから取り込み）")]
    [Tooltip("Dance1〜4へ取り込む元のAnimationClip（パルクール/スピン/キック/新技）。\n" +
             "右上の︙メニュー →「ダンスクリップから取り込み」で、スプライト・表示時間・上下位置・回転を取り込む（当たり判定は既存値を保持）。\n" +
             "※プレイヤーのクリップは読み取るだけで変更しない")]
    [SerializeField] private AnimationClip[] sourceDanceClips = new AnimationClip[4];

    [Header("Auto Collider（スプライトの絵の範囲から当たり判定を自動設定）")]
    [Tooltip("絵が描かれている範囲（不透明部分の外接矩形）に対する当たり判定の大きさの割合（幅, 高さ）。\n" +
             "右上の︙メニュー →「当たり判定を自動設定（未設定のコマのみ／全コマ上書き）」で使う")]
    [SerializeField] private Vector2 autoColliderScale = new Vector2(0.85f, 0.85f);

    [Header("Sprites - Dance 1")]
    [NonReorderable]
    [SerializeField] private NeonDancerFrame[] dance1Frames;
    [Header("Sprites - Dance 2")]
    [NonReorderable]
    [SerializeField] private NeonDancerFrame[] dance2Frames;
    [Header("Sprites - Dance 3")]
    [NonReorderable]
    [SerializeField] private NeonDancerFrame[] dance3Frames;
    [Header("Sprites - Dance 4")]
    [NonReorderable]
    [SerializeField] private NeonDancerFrame[] dance4Frames;
    [Tooltip("フレームのDurationが0以下の時の表示秒数")]
    [SerializeField] private float danceFallbackFrameDuration = 0.05f;

    [Header("Walk（Floorの範囲内で左右ランダムウォーク）")]
    [SerializeField] private float moveSpeed = 2f;
    [Tooltip("Floorの左右端から内側へ空けるマージン（ワールド単位）")]
    [SerializeField] private float moveEdgeMargin = 0.4f;
    [SerializeField] private float moveArriveThreshold = 0.05f;
    [Tooltip("目標地点に着いてから次の目標へ歩き出すまでの待ち時間（最小/最大、秒）")]
    [SerializeField] private float moveWaitMin = 0.1f;
    [SerializeField] private float moveWaitMax = 0.4f;

    [Header("Floor Spot")]
    [Range(0f, 1f)] [SerializeField] private float floorSpotAlpha = 0.3f;
    [Tooltip("Lightが全滅/復活した時の足元スポットのフェード秒数")]
    [SerializeField] private float floorSpotFadeDuration = 0.3f;

    [Header("Phase")]
    [Tooltip("後半フェーズの最大HP（前半HPはEnemyDataのMax Hp）")]
    [SerializeField] private int phase2MaxHp = 300;

    [System.Serializable]
    public class NeonDancerPoseFrame
    {
        public Sprite sprite;
        public float offsetX;
        public float offsetY;
        public float duration = 0.1f;
    }

    [Header("Phase Transition ①：ダウン（プレイヤーと同じ。メニュー「後半移行の画像・数値をプレイヤーからコピー」で設定）")]
    [Tooltip("前半HP0の瞬間に鳴らすSE（任意）")]
    [SerializeField] private AudioClip transitionSE;
    [Range(0f, 1f)] [SerializeField] private float transitionSEVolume = 1f;
    [Tooltip("前半の弾がフェードアウトして消えるまでの秒数")]
    [SerializeField] private float bulletFadeOutDuration = 0.5f;
    [Tooltip("倒れるアニメ（プレイヤーのCollapse Frames）。オフセットは立ち姿勢（ダンス1コマ目）の位置からの差")]
    [NonReorderable]
    [SerializeField] private NeonDancerPoseFrame[] downFrames;
    [Tooltip("プレイヤーの画像に対するボスの大きさの比率（オフセットに掛ける。通常1）")]
    [SerializeField] private float poseOffsetScale = 1f;

    [Header("Phase Transition ①：魂（プレイヤーと同じ落下）")]
    [Tooltip("魂のSpriteRenderer（ND_Stage > ND_Soul）")]
    [SerializeField] private SpriteRenderer soulRenderer;
    [Tooltip("魂のアニメ（プレイヤーのSoul Frames）")]
    [NonReorderable]
    [SerializeField] private NeonDancerPoseFrame[] soulFrames;
    [Tooltip("飛び上がる距離（プレイヤーのHop Height）。頂点が画面上端を越える場合は下のSoul Hop Top Marginで抑える")]
    [SerializeField] private float soulHopHeight = 1.5f;
    [SerializeField] private float soulHopDuration = 0.3f;
    [Tooltip("（未使用：飛び上がる向きはSoul Hop Angle Min/Maxで決める）")]
    [SerializeField] private float soulHopHorizontal = 1f;
    [SerializeField] private float soulHopArcHeight = 0.5f;
    [Tooltip("飛び上がる向きの、真上からの角度の範囲（度）。左右はランダム（落下と同じ側）")]
    [SerializeField] private float soulHopAngleMin = 10f;
    [SerializeField] private float soulHopAngleMax = 30f;
    [Tooltip("飛び上がりの頂点を、画面上端からこの距離（ワールド単位）より上に行かせない（ボスは画面上側にいるため）")]
    [SerializeField] private float soulHopTopMargin = 1.0f;
    [Tooltip("落下速度（プレイヤーのFall Speed Base）")]
    [SerializeField] private float soulFallSpeed = 1.5f;
    [SerializeField] private float soulFallAngleMin = 15f;
    [SerializeField] private float soulFallAngleMax = 40f;
    [Tooltip("画面下端からこの割合（ビューポート）だけ外へ出たら救出失敗（プレイヤーのGame Over Y Margin）")]
    [SerializeField] private float soulOffscreenMargin = 0.1f;

    [Header("Phase Transition ①：魂のガイド（円で囲む対象だと分かるように、落下中の魂の周りで脈動するリング）")]
    [Tooltip("ガイドのリング（ND_Soul > ND_SoulGuide、加算合成）")]
    [SerializeField] private SpriteRenderer soulGuideRenderer;
    [Tooltip("リングの直径（ワールド単位）の最小/最大（脈動）")]
    [SerializeField] private Vector2 soulGuideSize = new Vector2(1.4f, 1.9f);
    [Tooltip("リングの不透明度の最小/最大（脈動）")]
    [SerializeField] private Vector2 soulGuideAlpha = new Vector2(0.35f, 0.9f);
    [Tooltip("脈動の回数/秒")]
    [SerializeField] private float soulGuidePulseFrequency = 1.5f;
    [SerializeField] private Color soulGuideColor = Color.white;

    [Header("Phase Transition ②：救出失敗（Area終了）")]
    [Tooltip("魂が画面外に出てからフェードアウトを始めるまでの秒数")]
    [SerializeField] private float failFadeDelay = 0.5f;
    [Tooltip("黒へフェードアウトする秒数")]
    [SerializeField] private float failFadeDuration = 0.5f;
    [Tooltip("魂が画面外へ消えた瞬間に鳴らすSE（任意）")]
    [SerializeField] private AudioClip soulLostSE;
    [Range(0f, 1f)] [SerializeField] private float soulLostSEVolume = 1f;

    [Header("Phase Transition ②③：救出成功 → 魂がDancerに重なる")]
    [Tooltip("救出した瞬間に鳴らすSE（プレイヤーの魂を救出した時と同じ。メニューでコピー）")]
    [SerializeField] private AudioClip rescueSE;
    [Range(0f, 1f)] [SerializeField] private float rescueSEVolume = 1f;
    [Tooltip("救出した瞬間に魂の位置へ出すVFX（プレイヤーと同じ。未設定なら出さない）")]
    [SerializeField] private GameObject rescueVfxPrefab;
    [SerializeField] private float rescueVfxSeconds = 2f;
    [Tooltip("魂がDancerへ向かう間に引く虹色の光の粒（ND_Stage > ND_SoulTrail）")]
    [SerializeField] private ParticleSystem soulTrailParticles;
    [Tooltip("光の粒の1秒あたりの発生数")]
    [SerializeField] private float soulTrailRate = 45f;
    [Tooltip("光の粒の寿命（最小/最大、秒）")]
    [SerializeField] private Vector2 soulTrailLifetime = new Vector2(0.5f, 0.9f);
    [Tooltip("光の粒の大きさ（最小/最大）")]
    [SerializeField] private Vector2 soulTrailSize = new Vector2(0.08f, 0.18f);
    [Tooltip("虹色が1秒に何周するか")]
    [SerializeField] private float soulTrailHueSpeed = 0.8f;
    [Tooltip("魂がダウン中のDancerに重なるまでの秒数")]
    [SerializeField] private float soulMergeDuration = 5f;
    [Tooltip("重なった後、魂がフェードアウトする秒数")]
    [SerializeField] private float soulFadeOutDuration = 0.5f;

    [Header("Phase Transition ④：Finishアニメ（プレイヤーのFinish_1〜10）")]
    [Tooltip("Finishアニメ（最後のコマはFinish Hold Secondsだけ止まる）。オフセットは立ち姿勢の位置からの差")]
    [NonReorderable]
    [SerializeField] private NeonDancerPoseFrame[] finishFrames;
    [Tooltip("最後のコマで止まる秒数")]
    [SerializeField] private float finishHoldSeconds = 0.5f;
    [Tooltip("ON：Finishアニメを左右反転して表示する（プレイヤーと鏡合わせ）")]
    [SerializeField] private bool finishFlipX = true;

    [Header("Phase Transition ⑤：虹色の発光 → HPバー満タン")]
    [Tooltip("発光用のSpriteRenderer（ND_Body > ND_BodyGlow、加算合成）")]
    [SerializeField] private SpriteRenderer bodyGlowRenderer;
    [Tooltip("虹色に光る秒数")]
    [SerializeField] private float rainbowGlowDuration = 3f;
    [Tooltip("虹色が1秒に何周するか")]
    [SerializeField] private float rainbowCycleSpeed = 1.5f;
    [Tooltip("発光の明滅（パルス）の回数/秒")]
    [SerializeField] private float rainbowPulseFrequency = 2f;
    [Range(0f, 1f)] [SerializeField] private float rainbowGlowMaxAlpha = 0.9f;
    [Tooltip("発光の大きさ（体に対する倍率）")]
    [SerializeField] private float rainbowGlowScale = 1.08f;
    [Tooltip("HPバーが0から満タンまで伸びる秒数")]
    [SerializeField] private float hpRefillDuration = 2f;
    [Tooltip("HPバーが満タンになった瞬間（Floor/Light全快）に鳴らすSE（任意）")]
    [SerializeField] private AudioClip phase2ReadySE;
    [Range(0f, 1f)] [SerializeField] private float phase2ReadySEVolume = 1f;

    // ======================================================
    // Runtime state
    // ======================================================

    private static readonly Color[] WormholeColors =
    {
        new Color32(0x9B, 0x8F, 0xC7, 0xFF), // Area1
        new Color32(0x4C, 0xAF, 0x7D, 0xFF), // Area2
        new Color32(0x8D, 0x99, 0xAE, 0xFF), // Area3
        new Color32(0xE0, 0x7A, 0x3F, 0xFF), // Area4
        new Color32(0xB2, 0x3A, 0x52, 0xFF), // Area5
        new Color32(0xE0, 0xB0, 0x4F, 0xFF), // Area6
        new Color32(0x4F, 0x8F, 0xE0, 0xFF), // Area7
        new Color32(0x5F, 0xD6, 0xD6, 0xFF), // Area8
        new Color32(0xA3, 0xAE, 0xE0, 0xFF), // Area9
    };

    private bool started;
    private bool isDead;
    private bool isPhase2;
    private bool isTransitioning;
    private int  phase1MaxHp;

    // 前半HPの下に持たせる予備（前半のダメージで撃破されないようにする）
    private const int HpReserve = 1000000;

    private bool displayOverrideActive;
    private int  displayOverrideHp;

    private EnemyData   _enemyData;
    private EnemyBullet bulletPrefab;
    private EnemyBeamBullet beamBulletPrefab;
    private Transform   projectileRoot;
    private Collider2D[] ownerColliders = new Collider2D[0];

    private readonly List<NeonDancerWormhole> wormholePool = new List<NeonDancerWormhole>();
    private int lastWormholeColorIndex = -1;

    private Coroutine[] turretCoroutines;
    private readonly List<GameObject> activeTelegraphLines = new List<GameObject>();
    private static Material s_telegraphLineMat;

    private NeonDancerFrame currentFrame;
    private bool facingLeft;

    private bool  walkReady;
    private float walkMinX, walkMaxX;
    private float walkTargetX;
    private float walkWaitRemaining;

    private float floorSpotY;
    private float floorSpotAlphaCurrent;

    private Coroutine danceCoroutine;

    private Transform cachedPlayer;
    private FloorHealth cachedPlayerFloor;

    private static float TimeScale => SlowMotionManager.Instance != null ? SlowMotionManager.Instance.TimeScale : 1f;
    private float SpeedMul => enemyMover != null ? enemyMover.SpeedMultiplier : 1f;
    private static float MasterSEVolume => SoundSettingsManager.Instance != null ? SoundSettingsManager.Instance.SEVolume : 1f;
    private static bool IsPaused => PauseManager.Instance != null && PauseManager.Instance.IsPaused;

    // ======================================================
    // Public API（HP表示・Light・後半実装用）
    // ======================================================

    public bool IsPhase2 => isPhase2;
    public bool IsTransitioning => isTransitioning;
    /// <summary>現在フェーズの最大HP（前半＝EnemyDataのMax Hp、後半＝phase2MaxHp）</summary>
    public int PhaseMaxHp => isPhase2 ? phase2MaxHp : Mathf.Max(1, phase1MaxHp);
    /// <summary>現在フェーズ内のHP（前半は「合計HP−予備」）</summary>
    public int PhaseHp
    {
        get
        {
            int hp = enemyStats != null ? enemyStats.HP : 0;
            return isPhase2 ? Mathf.Max(0, hp) : Mathf.Max(0, hp - HpReserve);
        }
    }
    /// <summary>HPバーに表示する値（後半移行演出中は0→満タンへ伸びる演出値）</summary>
    public int DisplayPhaseHp => displayOverrideActive ? displayOverrideHp : PhaseHp;
    public int DisplayPhaseMaxHp => PhaseMaxHp;
    public bool IsAtPhaseMaxHp => PhaseHp >= PhaseMaxHp;
    public bool CanSelfHeal => started && !isDead && !isTransitioning && !introHold && enemyStats != null && enemyStats.HP > 0;
    /// <summary>Lightのビームが照らす位置（Dancerの体）</summary>
    public Vector3 BeamTargetPosition => spriteRenderer != null ? spriteRenderer.transform.position : transform.position;

    /// <summary>現在フェーズの最大HPを超えないように回復する（Light Selfheal用）</summary>
    public void HealPhaseClamped(int amount)
    {
        if (amount <= 0 || enemyStats == null || !CanSelfHeal) return;
        int cap = isPhase2 ? phase2MaxHp : enemyStats.MaxHP;
        int room = cap - enemyStats.HP;
        if (room <= 0) return;
        enemyStats.Heal(Mathf.Min(amount, room));
    }

    // ======================================================
    // Lifecycle
    // ======================================================

    private void Awake()
    {
        if (!Application.isPlaying) return;

        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (enemyMover == null)     enemyMover     = GetComponent<EnemyMover>();
        if (enemyStats == null)     enemyStats     = GetComponent<EnemyStats>();
        if (enemyShooter == null)   enemyShooter   = GetComponent<EnemyShooter>();
        if (spriteSwapper == null)  spriteSwapper  = GetComponent<EnemySpriteSwapper>();

        if (enemyMover != null)   enemyMover.suppressMovement = true;
        if (enemyShooter != null) enemyShooter.enabled = false;

        if (lights != null)
            foreach (var l in lights) if (l != null) l.SetController(this);

        // Prefab本来の子を記録（スポナーが後から付ける子＝シールドの泡エフェクト等を登場演出中だけ隠すため）
        prefabChildren.Clear();
        foreach (Transform c in transform) prefabChildren.Add(c);
    }

    private void Start()
    {
        if (!Application.isPlaying) return;

        if (enemyShooter != null)
        {
            _enemyData       = enemyShooter.GetEnemyData();
            bulletPrefab     = enemyShooter.GetBulletPrefab();
            beamBulletPrefab = enemyShooter.GetBeamBulletPrefab();
            projectileRoot   = enemyShooter.GetProjectileRoot();
        }
        if (projectileRoot == null)
        {
            GameObject pr = GameObject.Find("ProjectileRoot");
            if (pr != null) projectileRoot = pr.transform;
        }

        // ★スポナーのApplyEnemyData（HP→シールドの順）が済んだ後なので、ここで前半HP＋予備に広げる
        if (enemyStats != null)
        {
            phase1MaxHp = enemyStats.MaxHP;
            enemyStats.ApplyMaxHp(phase1MaxHp + HpReserve);
        }
        if (spriteRenderer != null) bodyBaseLocalScale = spriteRenderer.transform.localScale;
        if (soulRenderer != null) soulRenderer.gameObject.SetActive(false);
        if (soulGuideRenderer != null) SetRendererAlpha(soulGuideRenderer, 0f);
        if (bodyGlowRenderer != null) SetRendererAlpha(bodyGlowRenderer, 0f);
        StrokeManager.OnStrokeCreated += TrackStroke;
        StrokeManager.OnCircleFormedAnywhere += HandleCircleFormed;

        // ★スポナーのLayer一括設定（SetLayerRecursively）が済んだ後に切り離す
        if (stageRoot != null)
        {
            stageRoot.SetParent(transform.parent, true);
            if (enemyStats != null) enemyStats.RegisterSubPart(stageRoot.gameObject);
        }

        CollectOwnerColliders();
        SetupWalkRange();
        SetupFloorSpot();
        BuildWormholePool();
        if (transitionFlashRenderer != null) SetRendererAlpha(transitionFlashRenderer, 0f);

        ApplyFrame(GetFirstAvailableFrame());

        // ★Area10 Final Stageの登場演出中は、ダンス・移動・攻撃を始めず非表示で待つ（IntroRelease()で開始）
        if (RequestIntroHold)
        {
            RequestIntroHold = false;
            EnterIntroHold();
        }
        else
        {
            danceCoroutine = StartCoroutine(DanceLoop());
            StartTurretLoops();
        }

        started = true;
    }

    private void OnDestroy()
    {
        isDead = true;
        ClearTelegraphLines();
        StrokeManager.OnStrokeCreated -= TrackStroke;
        StrokeManager.OnCircleFormedAnywhere -= HandleCircleFormed;
        // ★演出の途中で破棄された場合も、プレイヤー側の停止・入力無効を残さない
        if (playerFrozen) FreezePlayerSide(false);
        if (inputDisabledByTransition) SetPlayerInputEnabled(true);
        if (selfHealPausedByTransition) SetSelfHealPaused(false);
    }

    private void Update()
    {
        if (!Application.isPlaying || !started || isDead) return;

        CheckPhaseTransition();
        UpdateWalk();
    }

    private void LateUpdate()
    {
        if (!Application.isPlaying || !started || isDead) return;
        UpdateFloorSpot();
    }

    // ======================================================
    // Setup helpers
    // ======================================================

    private void CollectOwnerColliders()
    {
        var list = new List<Collider2D>(GetComponentsInChildren<Collider2D>(true));
        if (stageRoot != null) list.AddRange(stageRoot.GetComponentsInChildren<Collider2D>(true));
        ownerColliders = list.ToArray();
    }

    private void SetupWalkRange()
    {
        float x = transform.position.x;
        walkMinX = walkMaxX = x;
        if (floorCollider != null)
        {
            Bounds b = floorCollider.bounds;
            walkMinX = b.min.x + moveEdgeMargin;
            walkMaxX = b.max.x - moveEdgeMargin;
            if (walkMinX > walkMaxX) walkMinX = walkMaxX = b.center.x;
        }
        walkTargetX = Mathf.Clamp(x, walkMinX, walkMaxX);
        walkWaitRemaining = 0f;
        walkReady = true;
    }

    private void SetupFloorSpot()
    {
        if (floorSpotRenderer == null) return;
        floorSpotY = floorSpotRenderer.transform.position.y;
        floorSpotAlphaCurrent = floorSpotAlpha;
        SetRendererAlpha(floorSpotRenderer, floorSpotAlphaCurrent);
    }

    private void BuildWormholePool()
    {
        if (wormholePrefab == null) return;
        Transform parent = stageRoot != null ? stageRoot : transform.parent;
        for (int i = 0; i < Mathf.Max(0, wormholePoolSize); i++)
            wormholePool.Add(Instantiate(wormholePrefab, transform.position, Quaternion.identity, parent));
    }

    private NeonDancerWormhole RentWormhole()
    {
        if (wormholePrefab == null) return null;
        foreach (var w in wormholePool)
            if (w != null && !w.IsBusy) return w;

        Transform parent = stageRoot != null ? stageRoot : transform.parent;
        var created = Instantiate(wormholePrefab, transform.position, Quaternion.identity, parent);
        wormholePool.Add(created);
        return created;
    }

    private Color PickNextWormholeColor()
    {
        int idx = Random.Range(0, WormholeColors.Length);
        if (WormholeColors.Length > 1 && idx == lastWormholeColorIndex)
            idx = (idx + Random.Range(1, WormholeColors.Length)) % WormholeColors.Length;
        lastWormholeColorIndex = idx;
        return WormholeColors[idx];
    }

    // ======================================================
    // Walk
    // ======================================================

    private void UpdateWalk()
    {
        if (!walkReady || isTransitioning || introHold) return;

        float dt = Time.deltaTime * TimeScale;
        if (walkWaitRemaining > 0f)
        {
            walkWaitRemaining -= dt;
            return;
        }

        float x = transform.position.x;
        if (Mathf.Abs(x - walkTargetX) <= Mathf.Max(0.001f, moveArriveThreshold))
        {
            walkTargetX = Random.Range(walkMinX, walkMaxX);
            walkWaitRemaining = Random.Range(Mathf.Min(moveWaitMin, moveWaitMax), Mathf.Max(moveWaitMin, moveWaitMax));
            return;
        }

        float newX = Mathf.MoveTowards(x, walkTargetX, moveSpeed * SpeedMul * dt);
        if (newX < x) SetFacing(true);
        else if (newX > x) SetFacing(false);

        Vector3 p = transform.position;
        p.x = newX;
        transform.position = p;
    }

    private void SetFacing(bool left)
    {
        if (spriteRenderer == null || facingLeft == left) return;
        facingLeft = left;
        spriteRenderer.flipX = left;
        if (currentFrame != null) ApplyFrame(currentFrame); // offset/当たり判定/回転の左右反転を即反映
    }

    // ======================================================
    // Floor spot
    // ======================================================

    private void UpdateFloorSpot()
    {
        if (floorSpotRenderer == null) return;

        Vector3 p = floorSpotRenderer.transform.position;
        floorSpotRenderer.transform.position = new Vector3(transform.position.x, floorSpotY, p.z);

        bool anyLit = false;
        if (lights != null)
            foreach (var l in lights) if (l != null && l.IsLit) { anyLit = true; break; }

        if (introHold && !introSpotLit) anyLit = false; // 登場演出：点灯するまで足元スポットは消しておく
        float target = anyLit ? floorSpotAlpha : 0f;
        float speed = floorSpotFadeDuration > 0f ? floorSpotAlpha / floorSpotFadeDuration : float.MaxValue;
        floorSpotAlphaCurrent = Mathf.MoveTowards(floorSpotAlphaCurrent, target, speed * Time.deltaTime);
        SetRendererAlpha(floorSpotRenderer, floorSpotAlphaCurrent);
    }

    private static void SetRendererAlpha(SpriteRenderer sr, float a)
    {
        Color c = sr.color; c.a = a; sr.color = c;
        sr.enabled = a > 0.001f;
    }

    // ======================================================
    // Area10 Final Stage登場演出（Area10FinalIntroControllerが操作する）
    //   プレイヤー側の開始演出（StageIntroController.PlayIntro）と鏡合わせに、
    //   Floor → シルエット＋Light本体 → 点灯（色付き・ビーム・足元スポット）の順で見せ、ゲーム開始で動き出す。
    // ======================================================

    /// <summary>スポーン前にtrueにしておくと、次にStart()したNeonDancerが登場演出待ちになる（Start()でfalseに戻す）</summary>
    public static bool RequestIntroHold;

    // 演出が途中で止まった場合（シーン遷移等）に、次のプレイへ持ち越さない
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => RequestIntroHold = false;

    public bool IsIntroHold => introHold;

    private bool introHold;
    private bool introSpotLit;
    private bool introPoseActive;
    private bool introPoseFlipX;
    private float introPoseScaleFactor = 1f;
    private float introPoseXOffset;
    private float introPoseGapAboveFloor;
    private Vector3 bodyBaseLocalScale = Vector3.one;
    private EnemyShield introShield;
    private NeonDancerHealthDisplay introHealthDisplay;
    private readonly List<Transform> prefabChildren = new List<Transform>();
    private readonly List<GameObject> introHiddenObjects = new List<GameObject>();

    private SpriteRenderer FloorSr => floorBarrier != null ? floorBarrier.GetComponent<SpriteRenderer>() : null;

    private void EnterIntroHold()
    {
        introHold = true;
        introSpotLit = false;
        introPoseActive = false;
        if (spriteRenderer != null) bodyBaseLocalScale = spriteRenderer.transform.localScale;

        // シールドの泡エフェクト（EnemyShieldが毎フレーム表示を切り替える）は、点灯までEnemyShieldごと止めて隠す
        introShield = GetComponent<EnemyShield>();
        if (introShield != null) introShield.enabled = false;
        introHiddenObjects.Clear();
        foreach (Transform c in transform)
        {
            if (prefabChildren.Contains(c) || !c.gameObject.activeSelf) continue;
            c.gameObject.SetActive(false);
            introHiddenObjects.Add(c.gameObject);
        }

        introHealthDisplay = GetComponent<NeonDancerHealthDisplay>();
        if (introHealthDisplay != null) introHealthDisplay.SetIntroHidden(true);

        if (spriteRenderer != null) SetRendererAlpha(spriteRenderer, 0f);
        var floorSr = FloorSr;
        if (floorSr != null) SetRendererAlpha(floorSr, 0f);
        if (lights != null)
            foreach (var l in lights)
            {
                if (l == null) continue;
                var sr = l.GetComponent<SpriteRenderer>();
                if (sr != null) SetRendererAlpha(sr, 0f);
                l.IntroHideBeam();
            }
        floorSpotAlphaCurrent = 0f;
        if (floorSpotRenderer != null) SetRendererAlpha(floorSpotRenderer, 0f);
    }

    /// <summary>登場演出 Step2：プレイヤーのFloorと同時にND_Floorをフェードインする</summary>
    public void IntroFadeInFloor(float duration)
    {
        if (!introHold) return;
        var floorSr = FloorSr;
        if (floorSr != null) StartCoroutine(FadeRendererAlpha(floorSr, 1f, duration));
    }

    /// <summary>
    /// 登場演出 Step3：プレイヤーのシルエットと同時に、同じポーズを左右反転したシルエットとLight本体をフェードインする。
    /// poseScaleFactor：ダンス画像の大きさに対するポーズ画像の大きさの比（プレイヤー側と同じ比率にする）
    /// xOffset：Dancer位置からのポーズのX位置（ワールド）／gapAboveFloor：Floor上端からポーズ画像の下端までの高さ（ワールド）
    /// </summary>
    public void IntroShowPose(Sprite silhouette, bool flipX, float poseScaleFactor, float xOffset, float gapAboveFloor, float duration)
    {
        if (!introHold || spriteRenderer == null) return;
        introPoseActive = true;
        introPoseFlipX = flipX;
        introPoseScaleFactor = poseScaleFactor > 0f ? poseScaleFactor : 1f;
        introPoseXOffset = xOffset;
        introPoseGapAboveFloor = gapAboveFloor;
        ApplyIntroPose(silhouette);

        StartCoroutine(FadeRendererAlpha(spriteRenderer, 1f, duration));
        if (lights != null)
            foreach (var l in lights)
            {
                if (l == null) continue;
                var sr = l.GetComponent<SpriteRenderer>();
                if (sr != null) StartCoroutine(FadeRendererAlpha(sr, 1f, duration));
            }
    }

    /// <summary>登場演出 Step5：プレイヤーの点灯と同時に色付きのポーズへ切り替え、ビーム・足元スポット・シールドを点ける</summary>
    public void IntroLightOn(Sprite fullPose, float spotlightFadeDuration)
    {
        if (!introHold) return;
        if (introPoseActive && fullPose != null) ApplyIntroPose(fullPose);
        if (lights != null)
            foreach (var l in lights) if (l != null) l.IntroBeamOn(spotlightFadeDuration);
        introSpotLit = true;
        RestoreIntroHiddenObjects();
    }

    /// <summary>ゲーム開始：ダンス・移動・攻撃を始める（演出の途中が省略された場合も表示を全て戻す）</summary>
    public void IntroRelease()
    {
        if (!introHold) return;
        introHold = false;
        introPoseActive = false;
        introSpotLit = true;

        if (spriteRenderer != null)
        {
            spriteRenderer.transform.localScale = bodyBaseLocalScale;
            SetRendererAlpha(spriteRenderer, 1f);
            facingLeft = spriteRenderer.flipX;
        }
        var floorSr = FloorSr;
        if (floorSr != null) SetRendererAlpha(floorSr, 1f);
        if (lights != null)
            foreach (var l in lights)
            {
                if (l == null) continue;
                var sr = l.GetComponent<SpriteRenderer>();
                if (sr != null) SetRendererAlpha(sr, 1f);
                l.IntroBeamOn(0.3f);
            }
        RestoreIntroHiddenObjects();
        if (introHealthDisplay != null) introHealthDisplay.SetIntroHidden(false);

        ApplyFrame(GetFirstAvailableFrame());
        walkTargetX = Mathf.Clamp(transform.position.x, walkMinX, walkMaxX);
        walkWaitRemaining = 0f;
        danceCoroutine = StartCoroutine(DanceLoop());
        StartTurretLoops();
    }

    private void RestoreIntroHiddenObjects()
    {
        foreach (var go in introHiddenObjects) if (go != null) go.SetActive(true);
        introHiddenObjects.Clear();
        if (introShield != null) introShield.enabled = true;
    }

    // ポーズ画像を、プレイヤー側と同じ「Floor上端からの高さ」・同じ大きさの比率で表示する
    private void ApplyIntroPose(Sprite pose)
    {
        if (pose == null || spriteRenderer == null) return;
        var t = spriteRenderer.transform;
        spriteRenderer.sprite = pose;
        spriteRenderer.flipX = introPoseFlipX;
        t.localRotation = Quaternion.identity;
        t.localScale = bodyBaseLocalScale * introPoseScaleFactor;

        float floorTop;
        var floorSr = FloorSr;
        if (floorSr != null && floorSr.sprite != null)
            floorTop = floorSr.transform.position.y + floorSr.sprite.bounds.max.y * floorSr.transform.lossyScale.y;
        else if (floorCollider != null)
            floorTop = floorCollider.bounds.max.y;
        else
            floorTop = transform.position.y;

        float bottomFromPivot = pose.bounds.min.y * t.lossyScale.y;
        t.position = new Vector3(transform.position.x + introPoseXOffset, floorTop + introPoseGapAboveFloor - bottomFromPivot, t.position.z);
    }

    private IEnumerator FadeRendererAlpha(SpriteRenderer sr, float target, float duration)
    {
        float start = sr.color.a;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            SetRendererAlpha(sr, Mathf.Lerp(start, target, elapsed / duration));
            yield return null;
        }
        SetRendererAlpha(sr, target);
    }

    // ======================================================
    // Dance
    // ======================================================

    private IEnumerator DanceLoop()
    {
        int last = -1;
        var candidates = new List<int>(4);
        while (!isDead)
        {
            candidates.Clear();
            for (int i = 0; i < 4; i++)
            {
                var arr = GetDanceFrames(i);
                if (arr != null && arr.Length > 0 && (i != last)) candidates.Add(i);
            }
            if (candidates.Count == 0 && last >= 0) candidates.Add(last); // 1パターンしか無い場合
            if (candidates.Count == 0) { yield return WaitScaled(0.5f); continue; }

            int pick = candidates[Random.Range(0, candidates.Count)];
            last = pick;

            var frames = GetDanceFrames(pick);
            for (int i = 0; i < frames.Length; i++)
            {
                if (isDead) yield break;
                ApplyFrame(frames[i]);
                yield return WaitScaled(FrameDurationOr(frames[i], danceFallbackFrameDuration));
            }
        }
    }

    private IEnumerator WaitScaled(float seconds)
    {
        float t = 0f;
        while (t < seconds)
        {
            if (isDead) yield break;
            t += Time.deltaTime * TimeScale;
            yield return null;
        }
    }

    private NeonDancerFrame[] GetDanceFrames(int index)
    {
        switch (index)
        {
            case 0: return dance1Frames;
            case 1: return dance2Frames;
            case 2: return dance3Frames;
            case 3: return dance4Frames;
            default: return null;
        }
    }

    private void SetDanceFrames(int index, NeonDancerFrame[] frames)
    {
        switch (index)
        {
            case 0: dance1Frames = frames; break;
            case 1: dance2Frames = frames; break;
            case 2: dance3Frames = frames; break;
            case 3: dance4Frames = frames; break;
        }
    }

    private NeonDancerFrame GetFirstAvailableFrame()
    {
        for (int i = 0; i < 4; i++)
        {
            var arr = GetDanceFrames(i);
            if (arr != null && arr.Length > 0) return arr[0];
        }
        return null;
    }

    private static float FrameDurationOr(NeonDancerFrame f, float fallback)
    {
        return (f != null && f.duration > 0f) ? f.duration : Mathf.Max(0.01f, fallback);
    }

    // ======================================================
    // Frame apply（ArcGuardControllerと同じ方式。flipX時はoffset/muzzle/collider/rotationZの4項目を反転）
    // ======================================================

    private void ApplyFrame(NeonDancerFrame frame)
    {
        if (frame == null || spriteRenderer == null) return;
        currentFrame = frame;
        if (frame.sprite != null)
        {
            spriteRenderer.sprite = frame.sprite;
            // EnemySpriteSwapperが被弾フラッシュ後に古いスプライトへ巻き戻すのを防ぐ
            if (spriteSwapper != null && Application.isPlaying) spriteSwapper.SetBaseSprite(frame.sprite);
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
        if (t == transform) return; // ルートに直接付いている場合は位置を動かさない（移動と競合するため）
        float x = spriteRenderer.flipX ? -offset.x : offset.x;
        t.localPosition = new Vector3(x, offset.y, t.localPosition.z);
    }

    private void ApplyCollider(NeonDancerFrame frame)
    {
        if (bodyCollider == null || frame == null) return;
        Vector2 offset = frame.colliderOffset;
        if (spriteRenderer != null && spriteRenderer.flipX) offset.x = -offset.x;
        if (frame.colliderSize.sqrMagnitude > 0.0001f)
            bodyCollider.size = frame.colliderSize;
        bodyCollider.offset = offset;
    }

    private void ApplyMuzzleOffset(NeonDancerFrame frame)
    {
        if (muzzlePoint == null || frame == null) return;
        float x = (spriteRenderer != null && spriteRenderer.flipX) ? -frame.muzzleOffset.x : frame.muzzleOffset.x;
        muzzlePoint.localPosition = new Vector3(x, frame.muzzleOffset.y, muzzlePoint.localPosition.z);
    }

    // ======================================================
    // Turrets
    // ======================================================

    private void StartTurretLoops()
    {
        if (turrets == null) return;
        if (turretCoroutines == null || turretCoroutines.Length != turrets.Length)
            turretCoroutines = new Coroutine[turrets.Length];
        for (int i = 0; i < turrets.Length; i++)
        {
            if (turrets[i] == null) continue;
            if (turretCoroutines[i] != null) StopCoroutine(turretCoroutines[i]);
            turretCoroutines[i] = StartCoroutine(TurretLoop(i));
        }
    }

    private void StopTurretLoops()
    {
        if (turretCoroutines == null) return;
        for (int i = 0; i < turretCoroutines.Length; i++)
        {
            if (turretCoroutines[i] != null) StopCoroutine(turretCoroutines[i]);
            turretCoroutines[i] = null;
        }
        // ★発射ループを途中で止めると、予兆線と「紐づけ待ち」のワームホールが残るため片付ける
        ClearTelegraphLines();
        foreach (var w in wormholePool)
        {
            if (w == null || !w.IsBusy) continue;
            w.StopChargeEffect();
            w.HoldWhile(null);
        }
    }

    private void ClearTelegraphLines()
    {
        foreach (var line in activeTelegraphLines) if (line != null) Destroy(line);
        activeTelegraphLines.Clear();
    }

    private IEnumerator TurretLoop(int turretIdx)
    {
        NeonDancerTurret t = turrets[turretIdx];
        if (!t.HasBulletChoices) yield break;

        yield return WaitScaled(Random.Range(Mathf.Min(t.InitialDelayMin, t.InitialDelayMax), Mathf.Max(t.InitialDelayMin, t.InitialDelayMax)));

        while (!isDead)
        {
            Vector3 pos = t.GetRandomSpawnPosition();

            // ★1発ごとに確率（%）で1種類を抽選。ワームホールを出す前に決めておく
            //   （Beamは撃った後も消えるまでワームホールを出し続けるため、出す時点で種類が必要）
            int typeIndex = t.PickBulletTypeIndex();
            EnemyData.BulletType pickedBt = GetBulletType(typeIndex);
            bool isBeam = pickedBt != null && pickedBt.useBeam;
            // ★Beam：溜め開始で警告SE＋吸い込みエフェクト（Bitと同じ予告）
            // ★予兆線：Bullet TypeのUse TelegraphがONの弾だけ、溜め完了で狙いを固定して表示し、消えたらその方向へ撃つ
            bool telegraph = pickedBt != null && pickedBt.useTelegraph && pickedBt.telegraphSeconds > 0f;
            bool warn = isBeam || telegraph;
            bool holdWormhole = isBeam || telegraph;

            NeonDancerWormhole wh = RentWormhole();
            if (wh != null)
            {
                wh.Play(pos, PickNextWormholeColor(), t.WormholeSize, t.ChargeDuration, t.ShrinkDuration, t.SpinSpeedStart, t.SpinSpeedEnd, holdWormhole);
                if (warn) wh.PlayChargeEffect();
            }
            if (warn && telegraphSE != null && SeSimultaneousGuard.TryAllow("NeonDancerController_TelegraphSE"))
                AudioOneShotPool.Play(telegraphSE, telegraphSEVolume * MasterSEVolume, pos, null, 0.1f);

            if (wh != null)
            {
                while (!wh.IsChargeComplete)
                {
                    if (isDead) yield break;
                    yield return null;
                }
            }
            else
            {
                yield return WaitScaled(t.ChargeDuration);
            }

            Vector2? lockedDir = null;
            if (telegraph)
            {
                Vector2 d = ComputeAimDirection(pos, t.FireDirection, pickedBt); // ★ここで狙いを固定
                lockedDir = d;
                yield return TelegraphRoutine(pos, d, pickedBt);
                if (isDead) yield break;
            }
            if (wh != null) wh.StopChargeEffect();

            Object fired = FireFromTurret(t, typeIndex, pos, lockedDir);
            // Beam/予告付きはワームホールを保持していたので解放する（BeamならBeamが消えるまで表示し続ける）
            if (wh != null && holdWormhole) wh.HoldWhile(isBeam ? fired : null);

            yield return WaitScaled(Random.Range(Mathf.Min(t.FireIntervalMin, t.FireIntervalMax), Mathf.Max(t.FireIntervalMin, t.FireIntervalMax)));
        }
    }

    // ★予兆線：EnemyShooter.FireWithTelegraphRoutine/CreateTelegraphLineと同じ見た目・設定項目（長さ/太さ/色/フェード/点滅）。
    //   待ち時間は他の発射台処理と同じくスローモーション（TimeScale）に追従する（Telegraph Use Unscaled TimeがONなら実時間）
    private IEnumerator TelegraphRoutine(Vector3 pos, Vector2 dir, EnemyData.BulletType bt)
    {
        float seconds = Mathf.Max(0.01f, bt.telegraphSeconds);
        float len     = Mathf.Max(0.1f, bt.telegraphLength);
        float width   = Mathf.Max(0.001f, bt.telegraphWidth);
        Color baseColor = bt.telegraphColor;

        var go = new GameObject("ND_TelegraphLine");
        if (projectileRoot != null) go.transform.SetParent(projectileRoot, false);
        go.transform.position = pos;
        var lr = go.AddComponent<LineRenderer>();
        if (s_telegraphLineMat == null)
        {
            Shader sh = Shader.Find("Sprites/Default");
            if (sh != null) s_telegraphLineMat = new Material(sh);
        }
        if (s_telegraphLineMat != null) lr.sharedMaterial = s_telegraphLineMat;
        lr.positionCount = 2;
        lr.useWorldSpace = true;
        lr.SetPosition(0, pos);
        lr.SetPosition(1, pos + (Vector3)(dir.normalized * len));
        lr.startWidth = width;
        lr.endWidth = width;
        lr.startColor = baseColor;
        lr.endColor = baseColor;
        lr.numCapVertices = 4;
        lr.numCornerVertices = 2;
        lr.alignment = LineAlignment.View;
        lr.textureMode = LineTextureMode.Stretch;
        activeTelegraphLines.Add(go);

        int blinkCount = Mathf.Max(0, bt.telegraphBlinkCount);
        float blinkMin = Mathf.Clamp01(bt.telegraphBlinkMinAlphaMul);
        float t = 0f;
        while (t < seconds)
        {
            if (isDead) break;
            float k = Mathf.Clamp01(t / seconds);
            float a = baseColor.a;
            if (bt.telegraphUseBlink && blinkCount > 0)
            {
                int segments = blinkCount * 2;
                int seg = Mathf.Min(segments - 1, Mathf.FloorToInt(k * segments));
                a = baseColor.a * ((seg % 2 == 1) ? blinkMin : 1f);
            }
            else if (bt.telegraphFadeOut)
            {
                a = Mathf.Lerp(baseColor.a, 0f, k);
            }
            Color c = new Color(baseColor.r, baseColor.g, baseColor.b, a);
            lr.startColor = c;
            lr.endColor = c;

            yield return null;
            t += bt.telegraphUseUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime * TimeScale;
        }

        activeTelegraphLines.Remove(go);
        if (go != null) Destroy(go);
    }

    private EnemyData.BulletType GetBulletType(int index)
    {
        if (_enemyData == null || _enemyData.bulletTypes == null) return null;
        if (index < 0 || index >= _enemyData.bulletTypes.Length) return null;
        return _enemyData.bulletTypes[index];
    }

    /// <summary>発射した弾（BeamならEnemyBeamBullet、通常弾ならEnemyBullet）を返す。撃たなかった場合はnull</summary>
    private Object FireFromTurret(NeonDancerTurret t, int typeIndex, Vector3 pos, Vector2? dirOverride = null)
    {
        if (isDead || isTransitioning) return null;
        // ★標準のEnemyShooterと同じ停止条件（ゲームオーバー/プレイヤーダウン中は撃たない）
        if (FloorHealth.IsBrokenGlobal || PixelDancerController.IsPlayerDeadGlobal || PixelDancerController.IsDownGlobal) return null;

        EnemyData.BulletType bt = GetBulletType(typeIndex);
        if (bt == null)
        {
            Debug.LogWarning($"[NeonDancerController] {t.name}: Bullet Type index {typeIndex} がEnemyDataにありません（EnemyData_NeonDancerのBullet Typesを確認）", this);
            return null;
        }

        // 予兆線で狙いを固定した場合はその方向へ撃つ
        Vector2 dir = dirOverride ?? ComputeAimDirection(pos, t.FireDirection, bt);
        Object fired = null;

        if (bt.useBeam)
        {
            if (beamBulletPrefab == null) return null;
            fired = EnemyShooter.SpawnBeamBullet(beamBulletPrefab, pos, dir, bt, ownerColliders, projectileRoot);
        }
        else
        {
            EnemyBullet usePrefab = (bt.usePinnedReflect && drillBulletPrefab != null) ? drillBulletPrefab : bulletPrefab;
            if (usePrefab == null) return null;
            EnemyBullet bullet = EnemyBulletPool.Get(usePrefab, pos, Quaternion.identity, projectileRoot);
            bullet.SetDirection(dir);
            fired = bullet;
            // ★Bullet TypeのSprite Override優先 → EnemyDataのBullet Sprite Override（EnemyShooterと同じ優先順位）
            Sprite fallbackSprite = _enemyData != null ? _enemyData.bulletSpriteOverride : null;
            EnemyShooter.ApplyBulletTypeToEnemyBullet(bullet, bt, bulletSpeed, bulletLifeTime, fallbackSprite, usePrefab, projectileRoot);
            foreach (Collider2D col in ownerColliders)
                if (col != null) bullet.SetOwnerCollisionIgnore(col, ignoreOwnerTime);

            // ★⑤煙幕弾：Just反射されたら煙幕を一切出さない（NeonDancer専用仕様）。
            //   PaddleDotは「反射時に煙を出す→Just判定→OnJustReflect」を同じフレームで行うため、
            //   OnJustReflectの時点で①この弾の煙幕機能を止め（以後の衝突でも出さない）②このフレームに出た煙と反射SEを消す
            if (bt.useSmokeGrenade)
            {
                // ★共通の煙幕弾は威力0にされる（EnemyBullet.ApplySmokeGrenade）。NeonDancerの⑤は
                //   プレイヤー/床に当たったらダメージも与える仕様のため、Bullet TypeのDamageを設定し直す
                if (bt.damage > 0) bullet.SetDamage(bt.damage);

                EnemyBullet smokeBullet = bullet;
                smokeBullet.OnJustReflect += () => SuppressSmokeOnJust(smokeBullet);
                // ★反射せずにプレイヤー側のDancer/床に当たった時も煙幕を出す（共有コードでは出ない）
                bullet.gameObject.AddComponent<NeonDancerSmokeBullet>().Arm(bullet);
            }

            // ★Drillの見た目はBullet Typeではなく回転コマで出す（TsukuyomiController.SpawnConfiguredBulletと同じ）
            if (bt.usePinnedReflect && drillSpinFrames != null && drillSpinFrames.Length > 0)
            {
                DrillSpinBullet spin = bullet.gameObject.AddComponent<DrillSpinBullet>();
                spin.Configure(drillSpinFrames, drillSpinRotationsPerSecond);
            }
            if (bt.usePinnedReflect)
            {
                // ★反射していないドリルがダンサー/床に当たったら必ず消す（残り1ヒットで当たった時に跳ね返るのを防ぐ）
                bullet.gameObject.AddComponent<NeonDancerDrillBullet>().Arm(bullet);
                // 【確認用・一時的】⑨Drillが線に留まっているかをEditor.logへ記録（確認が済んだら削除）
                bullet.gameObject.AddComponent<NeonDancerDrillDiag>().Arm(bullet);
            }
        }

        PlayFireFx(t, bt, pos, dir);
        return fired;
    }

    private static void SuppressSmokeOnJust(EnemyBullet bullet)
    {
        if (bullet == null) return;
        // 煙幕機能を止める（enable=falseの時、他の引数は使われない）
        bullet.ApplySmokeGrenade(false, 1f, 1f, 0.1f, null, null, null, null, null);
        int removed = NeonDancerSmokeMarker.DestroySpawnedThisFrameNear(bullet.transform.position, 1.5f);
        // 反射時にEnemyBullet.SmokeGrenadeが同じフレームに生成した反射SEも止める
        if (removed > 0)
        {
            GameObject se = GameObject.Find("SmokeGrenade_ReflectSE");
            if (se != null) Destroy(se);
        }
        Debug.Log($"[NeonDancerSmoke] ⑤Just反射：煙幕を無効化（このフレームの煙を{removed}個消去）");
    }

    private void PlayFireFx(NeonDancerTurret t, EnemyData.BulletType bt, Vector3 pos, Vector2 dir)
    {
        // SE：Bullet TypeのFire SE Override → 発射台のFire SE → EnemyDataのFire SE（EnemyShooter.PlayFireFxと同じく、
        //   Overrideが空ならEnemyData全体の発射SEにフォールバックする）
        AudioClip se; float vol;
        if (bt.fireSEOverride != null)  { se = bt.fireSEOverride; vol = bt.fireSEOverrideVolume; }
        else if (t.FireSE != null)      { se = t.FireSE;          vol = t.FireSEVolume; }
        else                            { se = _enemyData != null ? _enemyData.fireSE : null; vol = _enemyData != null ? _enemyData.fireSEVolume : 1f; }
        // ★3台が同じフレームに撃つと音が重なって大きくなるため、1フレーム1回に制限する
        if (se != null && vol > 0f && SeSimultaneousGuard.TryAllow("NeonDancerController_FireSE"))
            AudioOneShotPool.Play(se, vol * MasterSEVolume, pos, null, 0.1f);

        if (bt.fireVfxPrefab != null)
        {
            GameObject vfx = HitVfxPool.Rent(bt.fireVfxPrefab, projectileRoot, pos);
            vfx.transform.SetPositionAndRotation(pos, Quaternion.identity);
            vfx.SetActive(true);
            var pixelVfx = vfx.GetComponent<FirePixelVFX>();
            if (pixelVfx != null)
            {
                pixelVfx.Play(dir);
                HitVfxPool.ReturnLater(bt.fireVfxPrefab, vfx, pixelVfx.AutoReturnSeconds);
            }
            else
            {
                HitVfxPool.ReturnLater(bt.fireVfxPrefab, vfx, 2f);
            }
        }
    }

    // ★EnemyShooter.ComputeFinalDirection（private）と同じ4モードを移植。
    //   シーンにPixelDancerControllerが2つ（本体と演出用の複製）あるため、プレイヤーはPlayerタグで探す
    private Vector2 ComputeAimDirection(Vector3 spawnPos, Vector2 baseDir, EnemyData.BulletType type)
    {
        switch (type.aimMode)
        {
            case EnemyData.BulletType.AimMode.UseFireDirection:
                return baseDir;

            case EnemyData.BulletType.AimMode.TowardPlayer:
            {
                Transform player = GetPlayer();
                if (player == null) return baseDir;
                Vector2 d = ((Vector2)player.position - (Vector2)spawnPos).normalized;
                return d.sqrMagnitude > 0.0001f ? d : baseDir;
            }

            case EnemyData.BulletType.AimMode.TowardRandomPointOnFloor:
            {
                if (cachedPlayerFloor == null) cachedPlayerFloor = FindFirstObjectByType<FloorHealth>();
                Collider2D fc = cachedPlayerFloor != null ? cachedPlayerFloor.GetComponent<Collider2D>() : null;
                if (fc == null) return baseDir;
                Bounds b = fc.bounds;
                float tx;
                if (Random.Range(0f, 100f) < type.floorOutOfRangeChancePercent)
                {
                    float margin = Mathf.Max(0f, type.floorOutOfRangeMargin);
                    tx = Random.value < 0.5f ? b.min.x - margin : b.max.x + margin;
                }
                else tx = Random.Range(b.min.x, b.max.x);
                Vector2 d = (new Vector2(tx, b.center.y) - (Vector2)spawnPos).normalized;
                return d.sqrMagnitude > 0.0001f ? d : baseDir;
            }

            default: // TowardRandomPointInPlayerRange
            {
                Transform player = GetPlayer();
                if (player == null) return baseDir;
                PixelDancerController pdc = player.GetComponent<PixelDancerController>();
                float range = pdc != null ? pdc.AutoMoveRange : 3f;
                Vector2 target = new Vector2(player.position.x + Random.Range(-range, range), player.position.y);
                Vector2 d = (target - (Vector2)spawnPos).normalized;
                return d.sqrMagnitude > 0.0001f ? d : baseDir;
            }
        }
    }

    private Transform GetPlayer()
    {
        if (cachedPlayer == null)
        {
            GameObject go = GameObject.FindGameObjectWithTag("Player");
            if (go != null) cachedPlayer = go.transform;
        }
        return cachedPlayer;
    }

    // ======================================================
    // Phase transition
    // ======================================================

    private void CheckPhaseTransition()
    {
        if (isPhase2 || isTransitioning || enemyStats == null) return;
        int hp = enemyStats.HP;
        if (hp > 0 && hp <= HpReserve)
            StartCoroutine(PhaseTransitionRoutine());
    }

    private bool soulFalling;
    private bool soulRescued;
    private bool playerFrozen;
    private bool inputDisabledByTransition;
    private Vector3 soulAnimOffset;
    private readonly List<Stroke> trackedStrokes = new List<Stroke>();
    private readonly HashSet<Stroke> evaluatedCircles = new HashSet<Stroke>();

    private IEnumerator PhaseTransitionRoutine()
    {
        isTransitioning = true;
        Debug.Log($"[NeonDancerController] 前半HP0 → ダウン HP={enemyStats.HP} frame={Time.frameCount}", this);

        // ---------- ① 攻撃停止・片付け・無敵・デバフ解除 ----------
        // C3 SelfHeal：前半HP0から後半開始まで、回復までの秒数カウントを止める
        SetSelfHealPaused(true);
        StopTurretLoops();
        foreach (var w in wormholePool) if (w != null) w.ForceShrink();
        FadeOutEnemyBullets();
        DissolveAllSmoke();
        if (bodyPart != null) bodyPart.enableDamage = false;
        ClearDebuffs();
        displayOverrideActive = true;
        displayOverrideHp = 0;

        if (transitionSE != null)
            AudioOneShotPool.Play(transitionSE, transitionSEVolume * MasterSEVolume, BeamTargetPosition, null, 0.1f);

        // ダウンアニメ（ダンスを止めて倒れる）
        if (danceCoroutine != null) { StopCoroutine(danceCoroutine); danceCoroutine = null; }
        bool downFlip = spriteRenderer != null && spriteRenderer.flipX;
        Coroutine downCo = StartCoroutine(PlayPoseFrames(downFrames, downFlip, -1f));

        // 魂の放出・落下（円で囲まれたら救出）
        yield return SoulFallRoutine(downFlip);
        if (downCo != null) StopCoroutine(downCo);
        ApplyLastPoseFrame(downFrames, downFlip);

        if (!soulRescued)
        {
            // ---------- ② 救出失敗：Area終了 ----------
            yield return FailAndReturnToAreaSelect();
            yield break;
        }

        // ---------- ② 救出成功 ----------
        Debug.Log($"[NeonDancerController] 魂を救出 → 後半移行演出 frame={Time.frameCount}", this);
        Vector3 rescuePos = soulRenderer != null ? soulRenderer.transform.position : BeamTargetPosition;
        if (rescueSE != null)
            AudioOneShotPool.Play(rescueSE, rescueSEVolume * MasterSEVolume, rescuePos, null, 0.1f);
        if (rescueVfxPrefab != null)
        {
            GameObject vfx = Instantiate(rescueVfxPrefab, rescuePos, Quaternion.identity);
            if (rescueVfxSeconds > 0f) Destroy(vfx, rescueVfxSeconds);
        }
        if (soulGuideRenderer != null) SetRendererAlpha(soulGuideRenderer, 0f);
        Area10BossRushController bossRush = FindFirstObjectByType<Area10BossRushController>();
        if (bossRush != null) bossRush.SwitchToFinalStagePhase2Bgm();
        else Debug.Log("[NeonDancerController] Area10BossRushControllerが見つからないためBGM切替をスキップ", this);
        SetPlayerInputEnabled(false);
        FreezePlayerSide(true);

        // ---------- ③ 魂がダウン中のDancerに重なる ----------
        if (soulRenderer != null && spriteRenderer != null)
        {
            Vector3 from = soulRenderer.transform.position - soulAnimOffset;
            Vector3 to = spriteRenderer.bounds.center;
            float t = 0f;
            float dur = Mathf.Max(0.01f, soulMergeDuration);
            float trailCarry = 0f;
            if (soulTrailParticles != null) soulTrailParticles.Play();
            while (t < dur)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / dur);
                k = k * k * (3f - 2f * k);
                Vector3 pos = Vector3.Lerp(from, to, k);
                soulRenderer.transform.position = new Vector3(pos.x + soulAnimOffset.x, pos.y + soulAnimOffset.y, soulRenderer.transform.position.z);

                // 虹色の光の粒の尾
                if (soulTrailParticles != null)
                {
                    trailCarry += soulTrailRate * Time.deltaTime;
                    int n = Mathf.FloorToInt(trailCarry);
                    trailCarry -= n;
                    for (int i = 0; i < n; i++) EmitSoulTrail(soulRenderer.transform.position, t);
                }
                yield return null;
            }

            // ---------- ④ 魂がフェードアウト ----------
            yield return FadeRendererAlpha(soulRenderer, 0f, soulFadeOutDuration);
            soulRenderer.gameObject.SetActive(false);
        }

        // Finishアニメ（最後のコマで止まる）
        yield return PlayPoseFrames(finishFrames, finishFlipX, finishHoldSeconds);

        // ---------- ⑤ 虹色の発光 → HPバー満タン・Floor/Light全快・シールド満タン ----------
        yield return RainbowGlowRoutine();

        isPhase2 = true;
        if (enemyStats != null) enemyStats.ApplyMaxHp(Mathf.Max(1, phase2MaxHp)); // 前半の超過ダメージは持ち越さない
        var shield = GetComponent<EnemyShield>();
        if (shield != null) shield.ResetForNewPhase(); // 後半HP基準で満タン・B8解除
        if (floorBarrier != null) floorBarrier.ResetToFull();
        if (lights != null)
            foreach (var l in lights) if (l != null && l.Barrier != null) l.Barrier.ResetToFull();

        float elapsed = 0f;
        float refill = Mathf.Max(0.01f, hpRefillDuration);
        // Area10背景：HPバーが伸びるのと同時に、Final StageのFar（一番奥）の透明度を1まで上げる
        Area10StarFieldSpawner starField = FindFirstObjectByType<Area10StarFieldSpawner>();
        if (starField != null) starField.FadeFinalStageFarAlpha(1f, refill);
        while (elapsed < refill)
        {
            elapsed += Time.deltaTime;
            displayOverrideHp = Mathf.RoundToInt(phase2MaxHp * Mathf.Clamp01(elapsed / refill));
            yield return null;
        }
        displayOverrideActive = false;
        if (phase2ReadySE != null)
            AudioOneShotPool.Play(phase2ReadySE, phase2ReadySEVolume * MasterSEVolume, BeamTargetPosition, null, 0.1f);

        // ---------- ⑥ プレイヤー側再開・後半開始 ----------
        FreezePlayerSide(false);
        SetPlayerInputEnabled(true);
        SetSelfHealPaused(false);
        RestoreBodyForDance();
        if (bodyPart != null) bodyPart.enableDamage = true;
        isTransitioning = false;
        Debug.Log($"[NeonDancerController] 後半開始 HP={enemyStats.HP} frame={Time.frameCount}", this);
        EnterPhase2Behaviour();
    }

    /// <summary>
    /// 後半フェーズの行動開始。★後半の攻撃パターンは未実装のため、暫定で前半と同じ発射台・移動を継続する。
    /// 後半実装時はここを差し替える。
    /// </summary>
    private void EnterPhase2Behaviour()
    {
        StartTurretLoops();
    }

    // ---------- ① 片付け ----------

    private void FadeOutEnemyBullets()
    {
        if (projectileRoot == null) return;
        var children = new List<Transform>();
        foreach (Transform child in projectileRoot) children.Add(child);
        foreach (Transform child in children)
        {
            if (child == null) continue;
            var bullet = child.GetComponent<EnemyBullet>();
            if (bullet != null) bullet.StartFadeAndDestroy(bulletFadeOutDuration);
            else Destroy(child.gameObject); // Beam等
        }
    }

    private static void DissolveAllSmoke()
    {
        foreach (var smoke in FindObjectsByType<SmokeCloud>(FindObjectsSortMode.None))
            if (smoke != null) smoke.DissolveByCircle(smoke.transform.position, 1000f);
    }

    // B4（スロー）・B7（シールド破壊後のダメージ増加）を解除する。B8（シールド回復停止）は後半開始時にシールドごと解除
    private void ClearDebuffs()
    {
        if (enemyMover != null && enemyMover.IsSlowed) enemyMover.ApplySlowEffect(1f, 0f);
        var shield = GetComponent<EnemyShield>();
        if (shield != null && Game.Skills.SkillManager.Instance != null)
            Game.Skills.SkillManager.Instance.CancelShieldBreakBoost(shield);
    }

    // ---------- 体のポーズ（ダウン・Finish） ----------

    // 立ち姿勢（ダンス1コマ目）の位置を基準に、プレイヤーと同じオフセットでポーズ画像を表示する
    private void ApplyPoseFrame(NeonDancerPoseFrame f, bool flip)
    {
        if (f == null || spriteRenderer == null) return;
        if (f.sprite != null)
        {
            spriteRenderer.sprite = f.sprite;
            if (spriteSwapper != null) spriteSwapper.SetBaseSprite(f.sprite);
        }
        spriteRenderer.flipX = flip;
        var t = spriteRenderer.transform;
        if (t == transform) return;
        var stand = GetFirstAvailableFrame();
        Vector2 standOffset = stand != null ? stand.offset : Vector2.zero;
        float sx = flip ? -standOffset.x : standOffset.x;
        float ox = (flip ? -f.offsetX : f.offsetX) * poseOffsetScale;
        t.localRotation = Quaternion.identity;
        t.localScale = bodyBaseLocalScale;
        t.localPosition = new Vector3(sx + ox, standOffset.y + f.offsetY * poseOffsetScale, t.localPosition.z);
    }

    private void ApplyLastPoseFrame(NeonDancerPoseFrame[] frames, bool flip)
    {
        if (frames != null && frames.Length > 0) ApplyPoseFrame(frames[frames.Length - 1], flip);
    }

    // lastHoldSeconds＜0なら最後のコマもそのコマのDurationで待つ
    private IEnumerator PlayPoseFrames(NeonDancerPoseFrame[] frames, bool flip, float lastHoldSeconds)
    {
        if (frames == null) yield break;
        for (int i = 0; i < frames.Length; i++)
        {
            var f = frames[i];
            if (f == null) continue;
            ApplyPoseFrame(f, flip);
            float wait = (i == frames.Length - 1 && lastHoldSeconds >= 0f) ? lastHoldSeconds : Mathf.Max(0.01f, f.duration);
            float t = 0f;
            while (t < wait) { t += Time.deltaTime; yield return null; }
        }
    }

    private void RestoreBodyForDance()
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.transform.localScale = bodyBaseLocalScale;
            facingLeft = spriteRenderer.flipX;
        }
        if (bodyGlowRenderer != null) SetRendererAlpha(bodyGlowRenderer, 0f);
        ApplyFrame(GetFirstAvailableFrame());
        walkTargetX = Mathf.Clamp(transform.position.x, walkMinX, walkMaxX);
        walkWaitRemaining = 0f;
        if (danceCoroutine != null) StopCoroutine(danceCoroutine);
        danceCoroutine = StartCoroutine(DanceLoop());
    }

    // ---------- 魂 ----------

    private IEnumerator SoulFallRoutine(bool facingLeftAtDown)
    {
        soulRescued = false;
        if (soulRenderer == null)
        {
            Debug.LogWarning("[NeonDancerController] Soul Renderer未設定のため、魂の演出を省略して救出成功として扱います", this);
            soulRescued = true;
            yield break;
        }

        // 魂を体の位置で出す
        Transform soul = soulRenderer.transform;
        // Dancerの体（画像の中心）から放出する
        Vector2 startPos = (spriteRenderer != null && spriteRenderer.enabled) ? (Vector2)spriteRenderer.bounds.center
                         : spriteRenderer != null ? (Vector2)spriteRenderer.transform.position : (Vector2)transform.position;
        soul.position = new Vector3(startPos.x, startPos.y, soul.position.z);
        Color c = soulRenderer.color; c.a = 1f; soulRenderer.color = c;
        soulRenderer.enabled = true;
        soulRenderer.gameObject.SetActive(true);
        soulAnimOffset = Vector3.zero;
        Coroutine animCo = (soulFrames != null && soulFrames.Length > 0) ? StartCoroutine(SoulAnimRoutine()) : null;

        // プレイヤーと同じ：左右ランダム・落下角度ランダム
        int sign = Random.value < 0.5f ? -1 : 1;
        float minA = Mathf.Min(soulFallAngleMin, soulFallAngleMax);
        float maxA = Mathf.Max(soulFallAngleMin, soulFallAngleMax);
        float rad = Random.Range(minA, maxA) * Mathf.Deg2Rad;
        Vector2 fallDir = new Vector2(sign * Mathf.Sin(rad), -Mathf.Cos(rad)).normalized;

        soulFalling = true;
        Camera cam = Camera.main;

        // ホップ（ランダムな角度で斜め上に飛び上がる。頂点が画面上端の余白より上に行かないよう距離を抑える）
        float hopMinA = Mathf.Min(soulHopAngleMin, soulHopAngleMax);
        float hopMaxA = Mathf.Max(soulHopAngleMin, soulHopAngleMax);
        float hopRad = Random.Range(hopMinA, hopMaxA) * Mathf.Deg2Rad;
        Vector2 hopDir = new Vector2(sign * Mathf.Sin(hopRad), Mathf.Cos(hopRad));
        float hopDist = soulHopHeight;
        if (cam != null && hopDir.y > 0.0001f)
        {
            float topY = cam.transform.position.y + cam.orthographicSize - soulHopTopMargin - soulHopArcHeight;
            float maxDist = (topY - startPos.y) / hopDir.y;
            hopDist = Mathf.Clamp(hopDist, 0f, Mathf.Max(0f, maxDist));
        }
        Vector2 hopTarget = startPos + hopDir * hopDist;
        Vector2 pure = startPos;
        float hop = 0f;
        while (hop < soulHopDuration && !soulRescued)
        {
            hop += Time.deltaTime;
            float t = Mathf.Clamp01(hop / Mathf.Max(0.01f, soulHopDuration));
            t = 1f - (1f - t) * (1f - t);
            pure = Vector2.Lerp(startPos, hopTarget, t) + Vector2.up * (4f * soulHopArcHeight * t * (1f - t));
            soul.position = new Vector3(pure.x + soulAnimOffset.x, pure.y + soulAnimOffset.y, soul.position.z);
            UpdateSoulGuide(hop);
            yield return null;
        }

        // 落下（斜め下へ）。画面下端の外に出たら失敗
        Vector2 velocity = fallDir * soulFallSpeed;
        float guideTime = hop;
        while (!soulRescued)
        {
            pure += velocity * Time.deltaTime;
            soul.position = new Vector3(pure.x + soulAnimOffset.x, pure.y + soulAnimOffset.y, soul.position.z);
            guideTime += Time.deltaTime;
            UpdateSoulGuide(guideTime);
            if (cam != null && cam.WorldToViewportPoint(soul.position).y < -soulOffscreenMargin)
                break;
            yield return null;
        }

        soulFalling = false;
        if (soulGuideRenderer != null) SetRendererAlpha(soulGuideRenderer, 0f);
        if (!soulRescued)
        {
            if (animCo != null) StopCoroutine(animCo);
            soulRenderer.gameObject.SetActive(false);
            if (soulLostSE != null)
                AudioOneShotPool.Play(soulLostSE, soulLostSEVolume * MasterSEVolume, soul.position, null, 0.1f);
        }
        // 救出時は魂のアニメを続けたまま③へ（Dancerに重なってから消す）
    }

    // 落下中の魂の周りでリングを脈動させる（リングは魂の子なので一緒に動く）
    private void UpdateSoulGuide(float time)
    {
        if (soulGuideRenderer == null || soulGuideRenderer.sprite == null) return;
        float pulse = 0.5f + 0.5f * Mathf.Sin(time * soulGuidePulseFrequency * Mathf.PI * 2f);
        float size = Mathf.Lerp(soulGuideSize.x, soulGuideSize.y, pulse);
        float spriteSize = Mathf.Max(0.0001f, soulGuideRenderer.sprite.bounds.size.x);
        Transform gt = soulGuideRenderer.transform;
        Vector3 parentScale = gt.parent != null ? gt.parent.lossyScale : Vector3.one;
        float sx = Mathf.Abs(parentScale.x) > 0.0001f ? size / spriteSize / Mathf.Abs(parentScale.x) : 1f;
        float sy = Mathf.Abs(parentScale.y) > 0.0001f ? size / spriteSize / Mathf.Abs(parentScale.y) : 1f;
        gt.localScale = new Vector3(sx, sy, 1f);
        Color c = soulGuideColor;
        c.a = soulGuideColor.a * Mathf.Lerp(soulGuideAlpha.x, soulGuideAlpha.y, 1f - pulse); // 広がるほど薄く
        soulGuideRenderer.color = c;
        soulGuideRenderer.enabled = true;
    }

    private void EmitSoulTrail(Vector3 pos, float time)
    {
        Color col = Color.HSVToRGB(Mathf.Repeat(time * soulTrailHueSpeed + Random.Range(0f, 0.15f), 1f), 0.8f, 1f);
        var ep = new ParticleSystem.EmitParams
        {
            position = pos + (Vector3)(Random.insideUnitCircle * 0.12f),
            velocity = (Vector3)(Random.insideUnitCircle * 0.25f),
            startLifetime = Random.Range(Mathf.Min(soulTrailLifetime.x, soulTrailLifetime.y), Mathf.Max(soulTrailLifetime.x, soulTrailLifetime.y)),
            startSize = Random.Range(Mathf.Min(soulTrailSize.x, soulTrailSize.y), Mathf.Max(soulTrailSize.x, soulTrailSize.y)),
            startColor = col,
            applyShapeToPosition = false,
        };
        soulTrailParticles.Emit(ep, 1);
    }

    private IEnumerator SoulAnimRoutine()
    {
        int idx = 0;
        while (soulRenderer != null && soulRenderer.gameObject.activeSelf)
        {
            var f = soulFrames[idx];
            if (f != null)
            {
                if (f.sprite != null) soulRenderer.sprite = f.sprite;
                soulAnimOffset = new Vector3(f.offsetX, f.offsetY, 0f);
            }
            idx = (idx + 1) % soulFrames.Length;
            float t = 0f;
            float wait = f != null ? Mathf.Max(0.01f, f.duration) : 0.1f;
            while (t < wait) { t += Time.deltaTime; yield return null; }
        }
        soulAnimOffset = Vector3.zero;
    }

    // 円の判定：StrokeManagerの既存の通知を購読するだけ（共有コードは変更しない）。
    // 円が成立した瞬間に、新しく円になった線の範囲に魂が入っていれば救出
    private void TrackStroke(Stroke stroke)
    {
        trackedStrokes.RemoveAll(st => st == null);
        evaluatedCircles.RemoveWhere(st => st == null);
        if (stroke != null) trackedStrokes.Add(stroke);
    }

    private void HandleCircleFormed()
    {
        foreach (var st in trackedStrokes)
        {
            if (st == null || !st.IsCircle || !st.HasCircleBounds || evaluatedCircles.Contains(st)) continue;
            evaluatedCircles.Add(st);
            if (!soulFalling || soulRescued || soulRenderer == null) continue;
            Vector3 p = soulRenderer.transform.position;
            p.z = st.CircleBounds.center.z;
            if (st.CircleBounds.Contains(p)) soulRescued = true;
        }
    }

    // ---------- ② 救出失敗：Area終了（クリア扱いにしない・Result/ジェム画面なし） ----------

    private IEnumerator FailAndReturnToAreaSelect()
    {
        Debug.Log($"[NeonDancerController] 魂を救出できず → Area終了（Result・ジェム画面なし） frame={Time.frameCount}", this);
        SetPlayerInputEnabled(false);

        float t = 0f;
        while (t < failFadeDelay) { t += Time.unscaledDeltaTime; yield return null; }

        // GameManagerのゲームオーバー時と同じ黒フェード（Result画面は経由しない）
        GameObject fadeObj = new GameObject("NeonDancerFailFadeOut");
        Canvas fadeCanvas = fadeObj.AddComponent<Canvas>();
        fadeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        fadeCanvas.sortingOrder = 9999;
        var scaler = fadeObj.AddComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        GameObject imageObj = new GameObject("FadeImage");
        imageObj.transform.SetParent(fadeObj.transform, false);
        var fadeImage = imageObj.AddComponent<UnityEngine.UI.Image>();
        fadeImage.color = new Color(0f, 0f, 0f, 0f);
        RectTransform rt = imageObj.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;

        float dur = Mathf.Max(0.01f, failFadeDuration);
        t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            fadeImage.color = new Color(0f, 0f, 0f, Mathf.Clamp01(t / dur));
            yield return null;
        }

        Time.timeScale = 1f;
        inputDisabledByTransition = false; // シーン移動時のOnDestroyで、破棄途中の他オブジェクトの入力設定を触らない
        selfHealPausedByTransition = false;
        GameSession.Reset();
        Game.Shop.DrinkSession.Reset();
        UnityEngine.SceneManagement.SceneManager.LoadScene("03_AreaSelect");
    }

    // ---------- ⑤ 虹色の発光 ----------

    private IEnumerator RainbowGlowRoutine()
    {
        if (bodyGlowRenderer == null || spriteRenderer == null || rainbowGlowDuration <= 0f) yield break;
        Transform gt = bodyGlowRenderer.transform;
        float t = 0f;
        while (t < rainbowGlowDuration)
        {
            t += Time.deltaTime;
            bodyGlowRenderer.sprite = spriteRenderer.sprite;
            bodyGlowRenderer.flipX = spriteRenderer.flipX;
            gt.localScale = Vector3.one * rainbowGlowScale;
            float pulse = 0.5f + 0.5f * Mathf.Sin(t * rainbowPulseFrequency * Mathf.PI * 2f);
            Color hue = Color.HSVToRGB(Mathf.Repeat(t * rainbowCycleSpeed, 1f), 0.85f, 1f);
            float fade = Mathf.Clamp01(Mathf.Min(t, rainbowGlowDuration - t) / 0.25f); // 出だしと終わりはなめらかに
            hue.a = rainbowGlowMaxAlpha * Mathf.Lerp(0.35f, 1f, pulse) * fade;
            bodyGlowRenderer.color = hue;
            bodyGlowRenderer.enabled = true;
            yield return null;
        }
        SetRendererAlpha(bodyGlowRenderer, 0f);
    }

    // ---------- プレイヤー側の停止/再開・入力 ----------

    private Animator frozenAnimator;
    private PixelDancerAnimController frozenAnimCtrl;
    private PixelDancerController frozenMoveCtrl;
    private StageIntroController frozenStageIntro;
    private readonly List<FloorNeonAnimator> frozenFloors = new List<FloorNeonAnimator>();

    // プレイヤー側のダンサー（アニメ・自動移動）・スポットライト（脈動・追尾）・フロア（ネオン模様の切替）を止める/再開する
    private void FreezePlayerSide(bool freeze)
    {
        if (freeze)
        {
            if (playerFrozen) return;
            playerFrozen = true;
            frozenStageIntro = FindFirstObjectByType<StageIntroController>();
            SpriteRenderer dancer = frozenStageIntro != null ? frozenStageIntro.PixelDancerRenderer : null;
            if (dancer != null)
            {
                frozenAnimator = dancer.GetComponent<Animator>();
                frozenAnimCtrl = dancer.GetComponent<PixelDancerAnimController>();
                frozenMoveCtrl = dancer.GetComponent<PixelDancerController>();
                if (frozenAnimCtrl != null) frozenAnimCtrl.StopDance();
                if (frozenAnimator != null) frozenAnimator.speed = 0f;
                if (frozenMoveCtrl != null && frozenMoveCtrl.enabled) frozenMoveCtrl.enabled = false; else frozenMoveCtrl = null;
            }
            if (frozenStageIntro != null && frozenStageIntro.enabled) frozenStageIntro.enabled = false; else frozenStageIntro = null;
            frozenFloors.Clear();
            foreach (var f in FindObjectsByType<FloorNeonAnimator>(FindObjectsSortMode.None))
                if (f != null && f.enabled) { f.enabled = false; frozenFloors.Add(f); }
        }
        else
        {
            if (!playerFrozen) return;
            playerFrozen = false;
            if (frozenAnimator != null) frozenAnimator.speed = 1f;
            if (frozenMoveCtrl != null) frozenMoveCtrl.enabled = true;
            if (frozenAnimCtrl != null) frozenAnimCtrl.ResumeDance();
            if (frozenStageIntro != null) frozenStageIntro.enabled = true;
            foreach (var f in frozenFloors) if (f != null) f.enabled = true;
            frozenFloors.Clear();
            frozenAnimator = null; frozenAnimCtrl = null; frozenMoveCtrl = null; frozenStageIntro = null;
        }
    }

    private bool selfHealPausedByTransition;

    private void SetSelfHealPaused(bool paused)
    {
        selfHealPausedByTransition = paused;
        if (Game.Skills.SkillManager.Instance != null) Game.Skills.SkillManager.Instance.SetSelfHealPaused(paused);
    }

    // 救出〜後半開始までは線・スローモーション・一時停止を無効にする
    private void SetPlayerInputEnabled(bool enabled)
    {
        inputDisabledByTransition = !enabled;
        if (PaddleDrawer.Instance != null) PaddleDrawer.Instance.enabled = enabled;
        SlowMotionUIManager.Instance?.SetInputEnabled(enabled);
        PauseManager.Instance?.SetPauseBlocked(!enabled);
    }

    // ======================================================
    // Editor Preview（ArcGuardControllerと同じ方式）
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

    private bool IsAnimatePreview(out int danceIndex)
    {
        switch (previewMode)
        {
            case NeonDancerPreviewMode.Dance1Animate: danceIndex = 0; return true;
            case NeonDancerPreviewMode.Dance2Animate: danceIndex = 1; return true;
            case NeonDancerPreviewMode.Dance3Animate: danceIndex = 2; return true;
            case NeonDancerPreviewMode.Dance4Animate: danceIndex = 3; return true;
            default: danceIndex = (int)previewMode; return false;
        }
    }

    private NeonDancerFrame GetPreviewFrame()
    {
        IsAnimatePreview(out int danceIndex);
        var frames = GetDanceFrames(danceIndex);
        if (frames == null || frames.Length == 0) return null;
        return frames[Mathf.Clamp(previewFrameIndex, 0, frames.Length - 1)];
    }

#if UNITY_EDITOR
    private void OnEditorTickRefresh()
    {
        if (this == null || Application.isPlaying) return;
        if (_editorAnimRunning || spriteRenderer == null) return;

        var f = GetPreviewFrame();
        if (f == null) return;
        ApplyFrame(f);
        if (bodyCollider != null) UnityEditor.EditorUtility.SetDirty(bodyCollider);
        UnityEditor.SceneView.RepaintAll();
    }
#endif

    private void OnValidate()
    {
        if (Application.isPlaying) return;
#if UNITY_EDITOR
        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (spriteRenderer == null) return;

        if (IsAnimatePreview(out int danceIndex))
        {
            if (_editorAnimRunning && _editorAnimDance != danceIndex) StopEditorAnim();
            StartEditorAnim(danceIndex);
        }
        else
        {
            StopEditorAnim();
            var f = GetPreviewFrame();
            if (f != null) ApplyFrame(f);
        }
        UnityEditor.SceneView.RepaintAll();
#endif
    }

#if UNITY_EDITOR
    private bool   _editorAnimRunning;
    private double _editorAnimLastTime;
    private int    _editorAnimFrameIdx;
    private int    _editorAnimDance;

    private void StartEditorAnim(int danceIndex)
    {
        _editorAnimDance    = danceIndex;
        _editorAnimFrameIdx = 0;
        _editorAnimLastTime = UnityEditor.EditorApplication.timeSinceStartup;
        if (!_editorAnimRunning)
        {
            _editorAnimRunning = true;
            UnityEditor.EditorApplication.update += OnEditorUpdate;
        }
        var frames = GetDanceFrames(danceIndex);
        if (frames != null && frames.Length > 0) ApplyFrame(frames[0]);
    }

    private void StopEditorAnim()
    {
        if (!_editorAnimRunning) return;
        _editorAnimRunning = false;
        UnityEditor.EditorApplication.update -= OnEditorUpdate;
    }

    private void OnEditorUpdate()
    {
        if (this == null || !_editorAnimRunning) { StopEditorAnim(); return; }
        var frames = GetDanceFrames(_editorAnimDance);
        if (frames == null || frames.Length == 0) { StopEditorAnim(); return; }

        var current = frames[_editorAnimFrameIdx % frames.Length];
        double dur = FrameDurationOr(current, danceFallbackFrameDuration);
        double now = UnityEditor.EditorApplication.timeSinceStartup;
        if (now - _editorAnimLastTime >= dur)
        {
            _editorAnimLastTime = now;
            _editorAnimFrameIdx = (_editorAnimFrameIdx + 1) % frames.Length;
            ApplyFrame(frames[_editorAnimFrameIdx]);
            UnityEditor.SceneView.RepaintAll();
        }
    }

    private void OnPlayModeStateChanged(UnityEditor.PlayModeStateChange state)
    {
        if (state == UnityEditor.PlayModeStateChange.ExitingEditMode) StopEditorAnim();
    }

    // ★当たり判定のプレビューはColliderの再描画に頼らず、自前でGizmo描画する（メモリ記載の確定パターン）
    private void OnDrawGizmosSelected()
    {
        if (spriteRenderer == null) return;
        var cf = Application.isPlaying ? currentFrame : (_editorAnimRunning ? currentFrame : GetPreviewFrame());
        if (cf == null) return;

        Vector2 size = cf.colliderSize.sqrMagnitude > 0.0001f ? cf.colliderSize : (bodyCollider != null ? bodyCollider.size : Vector2.zero);
        if (size.sqrMagnitude <= 0.0001f) return;
        Vector2 off = cf.colliderOffset;
        if (spriteRenderer.flipX) off.x = -off.x;

        Transform body = spriteRenderer.transform;
        Matrix4x4 prev = Gizmos.matrix;
        Gizmos.matrix = Matrix4x4.TRS(body.position, body.rotation, body.lossyScale);
        Gizmos.color = new Color(0.15f, 0.95f, 0.15f, 0.9f);
        Gizmos.DrawWireCube(new Vector3(off.x, off.y, 0f), new Vector3(size.x, size.y, 0.05f));
        Gizmos.matrix = prev;
    }

    // ======================================================
    // プレイヤーのダンスクリップから取り込み（クリップは読み取るだけで変更しない）
    // ======================================================

    // ======================================================
    // 当たり判定の自動設定（スプライトのTightメッシュ頂点＝絵の不透明部分の外接矩形から算出）
    // ======================================================

    [ContextMenu("当たり判定を自動設定（未設定のコマのみ）")]
    private void AutoColliderEmptyOnly() => AutoFitColliders(false);

    [ContextMenu("当たり判定を自動設定（全コマ上書き）")]
    private void AutoColliderOverwriteAll()
    {
        if (!UnityEditor.EditorUtility.DisplayDialog("当たり判定を自動設定",
                "全ダンスの全コマの当たり判定（Collider Size / Collider Offset）を、スプライトの絵の範囲から作り直します。\n手で調整した値も上書きされます。続けますか？",
                "上書きする", "キャンセル"))
            return;
        AutoFitColliders(true);
    }

    private void AutoFitColliders(bool overwrite)
    {
        UnityEditor.Undo.RecordObject(this, "NeonDancer Auto Collider");
        int changed = 0, skipped = 0;
        for (int d = 0; d < 4; d++)
        {
            var frames = GetDanceFrames(d);
            if (frames == null) continue;
            foreach (var f in frames)
            {
                if (f == null || f.sprite == null) continue;
                if (!overwrite && f.colliderSize.sqrMagnitude > 0.0001f) { skipped++; continue; }

                Vector2[] v = f.sprite.vertices; // ピボット基準・ユニット単位
                if (v == null || v.Length == 0) continue;
                Vector2 min = v[0], max = v[0];
                for (int i = 1; i < v.Length; i++) { min = Vector2.Min(min, v[i]); max = Vector2.Max(max, v[i]); }

                Vector2 size = max - min;
                f.colliderSize   = new Vector2(size.x * autoColliderScale.x, size.y * autoColliderScale.y);
                f.colliderOffset = (min + max) * 0.5f;
                changed++;
            }
        }
        UnityEditor.EditorUtility.SetDirty(this);
        if (UnityEditor.PrefabUtility.IsPartOfPrefabInstance(this))
            UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(this);
        UnityEditor.SceneView.RepaintAll();
        Debug.Log($"[NeonDancerController] 当たり判定を自動設定：{changed}コマ設定、{skipped}コマは設定済みのため保持", this);
    }

    [ContextMenu("ダンスクリップから取り込み")]
    private void ImportDanceFramesFromClips()
    {
        bool hasExisting = false;
        for (int i = 0; i < 4; i++)
        {
            var arr = GetDanceFrames(i);
            if (sourceDanceClips != null && i < sourceDanceClips.Length && sourceDanceClips[i] != null && arr != null && arr.Length > 0)
                hasExisting = true;
        }
        if (hasExisting && !UnityEditor.EditorUtility.DisplayDialog(
                "ダンスクリップから取り込み",
                "既に設定済みのフレームがあります。\nスプライト・表示時間・上下位置・回転を上書きします（同じコマ番号の当たり判定とMuzzle Offsetは保持します）。\n続けますか？",
                "上書きする", "キャンセル"))
            return;

        UnityEditor.Undo.RecordObject(this, "NeonDancer Import Dance Frames");

        int imported = 0;
        for (int i = 0; i < 4; i++)
        {
            AnimationClip clip = (sourceDanceClips != null && i < sourceDanceClips.Length) ? sourceDanceClips[i] : null;
            if (clip == null) continue;

            UnityEditor.EditorCurveBinding? spriteBinding = null;
            foreach (var b in UnityEditor.AnimationUtility.GetObjectReferenceCurveBindings(clip))
                if (b.propertyName == "m_Sprite" && b.type == typeof(SpriteRenderer)) { spriteBinding = b; break; }
            if (!spriteBinding.HasValue)
            {
                Debug.LogWarning($"[NeonDancerController] {clip.name}: スプライトのカーブが見つからないためスキップ", this);
                continue;
            }

            AnimationCurve posY = null, rotZ = null;
            foreach (var b in UnityEditor.AnimationUtility.GetCurveBindings(clip))
            {
                if (b.path != "" || b.type != typeof(Transform)) continue;
                if (b.propertyName == "m_LocalPosition.y") posY = UnityEditor.AnimationUtility.GetEditorCurve(clip, b);
                else if (b.propertyName.StartsWith("localEulerAngles") && b.propertyName.EndsWith(".z")) rotZ = UnityEditor.AnimationUtility.GetEditorCurve(clip, b);
            }

            var keys = UnityEditor.AnimationUtility.GetObjectReferenceCurve(clip, spriteBinding.Value);
            if (keys == null || keys.Length == 0) continue;
            var old = GetDanceFrames(i);
            float baseY = posY != null ? posY.Evaluate(0f) : 0f;
            double step = clip.frameRate > 0f ? 1.0 / clip.frameRate : 0.05;

            // ★スプライト切替の瞬間だけでなく、クリップのフレームレート刻みで回転・上下位置を読み取る
            //   （パルクールは同じスプライトのまま回転・上下が補間で変化するため）。
            //   スプライト・回転・上下が直前と同じ区間は1コマにまとめる。
            var list = new System.Collections.Generic.List<NeonDancerFrame>();
            int sampleCount = Mathf.Max(1, Mathf.RoundToInt((float)(clip.length / step)));
            int keyIdx = 0;
            for (int s = 0; s < sampleCount; s++)
            {
                float t = (float)(s * step);
                while (keyIdx + 1 < keys.Length && keys[keyIdx + 1].time <= t + 0.0001f) keyIdx++;
                Sprite sp = keys[keyIdx].value as Sprite;
                float rz = rotZ != null ? rotZ.Evaluate(t) : 0f;
                float oy = posY != null ? posY.Evaluate(t) - baseY : 0f;

                NeonDancerFrame last = list.Count > 0 ? list[list.Count - 1] : null;
                if (last != null && last.sprite == sp && Mathf.Abs(last.rotationZ - rz) < 0.01f && Mathf.Abs(last.offset.y - oy) < 0.0005f)
                {
                    last.duration += (float)step;
                    continue;
                }
                list.Add(new NeonDancerFrame { sprite = sp, duration = (float)step, offset = new Vector2(0f, oy), rotationZ = rz });
            }

            var frames = list.ToArray();
            // 同じコマ番号の当たり判定・Muzzleは保持（取り込み直しても手調整した値を失わない）
            for (int k = 0; k < frames.Length; k++)
            {
                if (old != null && k < old.Length && old[k] != null)
                {
                    frames[k].colliderSize   = old[k].colliderSize;
                    frames[k].colliderOffset = old[k].colliderOffset;
                    frames[k].muzzleOffset   = old[k].muzzleOffset;
                }
            }
            SetDanceFrames(i, frames);
            imported++;
            Debug.Log($"[NeonDancerController] {clip.name} → Dance{i + 1}: {frames.Length}コマ取り込み", this);
        }

        UnityEditor.EditorUtility.SetDirty(this);
        if (UnityEditor.PrefabUtility.IsPartOfPrefabInstance(this))
            UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(this);
        UnityEditor.SceneView.RepaintAll();
        Debug.Log($"[NeonDancerController] ダンスクリップ取り込み完了：{imported}パターン", this);
    }
#endif
}
