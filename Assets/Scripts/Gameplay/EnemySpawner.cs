using System.Collections;
using UnityEngine;
using Game.Progress;

public class EnemySpawner : MonoBehaviour
{
    public static event System.Action<int> OnStageStarted;
    public static event System.Action<int> OnStageCleared;
    public static event System.Action OnFinalBossDefeated;
    /// <summary>
    /// ボスが1体倒される度に発火する（スローモーション等の見た目の演出専用）。
    /// 既存Area1〜9は唯一のボスが最終ボスと同一のため、OnFinalBossDefeatedと同時に1回だけ発火し挙動は変わらない。
    /// Area10ボスラッシュのみ、9体全ての撃破それぞれで発火する（OnFinalBossDefeatedは9体目の撃破時のみ発火）。
    /// </summary>
    public static event System.Action OnBossDefeatedEffect;

    public AreaConfig CurrentAreaConfig => areaConfig;
    // =========================================================
    // Enemy Spawn Entry（重み付き確率選択用 - Legacy）
    // =========================================================
    [System.Serializable]
    public class EnemySpawnEntry
    {
        [Header("Enemy Configuration")]
        [Tooltip("出現させる敵のデータ（EnemyData）")]
        public EnemyData enemyData;

        [Header("Spawn Weight")]
        [Tooltip("この敵の出現確率の重み（0～100）。他の敵との相対的な確率になります。\n" +
                 "例: 敵A=70, 敵B=30 → 敵Aが70%、敵Bが30%の確率で出現\n" +
                 "合計が100%でなくても動作します（相対確率として扱います）")]
        [Range(0f, 100f)]
        public float weight = 50f;
    }

    // =========================================================
    // Enemy Formation（配置パターン）
    // =========================================================
    [System.Serializable]
    public class EnemyFormation
    {
        [Header("Formation Info")]
        [Tooltip("配置パターンの名前（デバッグ用・UI表示用）\n例: 「Pincer Attack」「Top Heavy」「Boss Formation」")]
        public string formationName = "Formation";

        [Header("Formation Entries")]
        [Tooltip("この配置パターンで出現させる敵のリスト\n各エントリーで「どのスポーンポイントに」「どの敵を」配置するかを設定")]
        public FormationEntry[] entries;
    }

    [System.Serializable]
    public class FormationEntry
    {
        [Header("Spawn Location")]
        [Tooltip("Spawn Pointsのインデックス（0始まり）\nEnemySpawnerのSpawn Points配列を参照します")]
        public int spawnPointIndex = 0;

        [Header("Enemy Type")]
        [Tooltip("この位置に配置する敵のデータ")]
        public EnemyData enemyData;
    }

    // =========================================================
    // Wave Stage（ウェーブ段階）
    // =========================================================
    [System.Serializable]
    public class WaveStage
    {
        [Header("Stage Info")]
        [Tooltip("段階の名前（UI表示用）\n例: 「Stage 1」「Stage 2」「Boss Stage」")]
        public string stageName = "Stage";

        [Header("Formations")]
        [Tooltip("この段階で使用する配置パターンのリスト\nランダムに選択され、使用済みは除外されます")]
        public EnemyFormation[] formations;

        [Header("Clear Conditions")]
        [Tooltip("制限時間（秒）\n0以下なら時間制限なし")]
        public float timeLimit = 180f;

        [Tooltip("true = 時間経過でクリア（敵が残っていてもOK）\nfalse = 全ての敵を倒す必要がある")]
        public bool clearOnTimeExpired = true;

        [Tooltip("true = Formationを全て使い切ったらループさせず打ち止めにする（Area10のボスラッシュ専用。既存Areaはfalseのままにすること）")]
        public bool stopAfterFormationsExhausted = false;

        [Tooltip("true = Formationをランダムではなく配列の並び順（インデックスの小さい順）で選択する（Area10のボスラッシュ専用。既存Areaはfalseのままにすること）")]
        public bool useSequentialFormationOrder = false;

        [Tooltip("このStageだけのスキル選択カード枚数の上書き（Area10のボスラッシュ専用）。0以下ならAreaConfig側のSkill Selection Count Overrideの値をそのまま使う。" +
                 "例：Stage1（ボス1-3）だけ枚数を変えたい場合はここに値を入れる")]
        public int skillSelectionCountOverride = 0;

        [Tooltip("true = このStageだけスキル選択カードを一切出さない（Area10のFinal Stage専用。既存Areaはfalseのままにすること）")]
        public bool disableSkillSelection = false;
    }

    [Header("Prefab / Parent")]
    [SerializeField] private GameObject enemyPrefab;
    [SerializeField] private Transform enemyRoot;

    [Header("Shooting Wiring")]
    [SerializeField] private Transform projectileRoot;
    [SerializeField] private EnemyBullet enemyBulletPrefab;
    [Tooltip("Bullet TypeのUse Beam=ONの弾種を発射する時に使うPrefab（enemyBulletPrefabのBeam版）")]
    [SerializeField] private EnemyBeamBullet enemyBeamBulletPrefab;

    /// <summary>AttackBlock など EnemyShooter 以外のスクリプトが参照するための公開アクセサ。</summary>
    public Transform ProjectileRoot => projectileRoot;

    [Header("Enemy Types (Weighted Random)")]
    [Tooltip("複数の敵を設定し、weightで出現確率を調整します。\n" +
             "例: Snake(weight=70), Sniper(weight=30) → Snakeが70%、Sniperが30%の確率で出現\n" +
             "ウェーブシステムなど、将来の拡張に対応した設計です")]
    [SerializeField] private EnemySpawnEntry[] enemyTypes;

    [Header("Legacy (後方互換性・非推奨)")]
    [Tooltip("【非推奨】後方互換性のため残されています。新しく設定する場合は「Enemy Types」を使用してください")]
    [SerializeField] private EnemyData enemyData;

    [Header("Spawn Points")]
    [SerializeField] private Transform[] spawnPoints;

    [Header("Spawn Settings (Legacy)")]
    [Tooltip("【非推奨】ウェーブシステム未使用時のみ有効\nウェーブシステム使用時はWave Stagesの設定が優先されます")]
    [SerializeField] private int maxAliveEnemies = 3;
    [SerializeField] private float startDelay = 1.0f;
    [SerializeField] private float spawnInterval = 2.0f;

    // =========================================================
    // Wave System
    // =========================================================
    [Header("Wave System")]
    [Tooltip("ON: ウェーブシステムを使用する（3段階制）\nOFF: 従来のスポーンシステムを使用")]
    [SerializeField] private bool useWaveSystem = false;

    [Header("Area Configuration")]
    [Tooltip("ON: AreaConfigから設定を読み込む\nOFF: 直接Wave Stagesを設定")]
    [SerializeField] private bool useAreaConfig = false;

    [Tooltip("使用するエリア設定（GameSessionから自動設定される場合もあります）")]
    [SerializeField] private AreaConfig areaConfig;

    [Tooltip("ウェーブの各段階を設定します\n例: Stage 1, Stage 2, Boss Stage\nArea Config使用時は上書きされます")]
    [SerializeField] private WaveStage[] waveStages;

    [Header("Wave Timing")]
    [Tooltip("Formation切り替え時の待機時間（秒）\n敵を全て倒してからスキル選択画面が開くまでの時間")]
    [SerializeField] private float formationTransitionDelay = 2f;

    [Tooltip("スキル選択完了後、次の敵がスポーンするまでの待機時間（秒）")]
    [SerializeField] private float postSkillSelectionSpawnDelay = 1f;

    [Tooltip("Stage切り替え時、Background遷移完了後に敵がスポーンするまでの追加待機時間（秒）")]
    [SerializeField] private float backgroundClearDelay = 1f;

    [Header("Fade In Settings")]
    [Tooltip("Stage1/2 の敵フェードイン時間（秒）")]
    [SerializeField] private float stage12FadeInDuration = 1f;

    [Tooltip("Stage3 の敵フェードイン時間（秒）。ボス用に長めに設定")]
    [SerializeField] private float stage3FadeInDuration = 3f;

    [Tooltip("Stage1 で複数敵配置時、1体ずつ出現させる間隔（秒）")]
    [SerializeField] private float formationEnemyStaggerDelay = 1f;

    [Header("Wave Debug")]
    [Tooltip("デバッグモード: 特定の段階から開始できます")]
    [SerializeField] private bool debugMode = false;

    [Tooltip("デバッグ用開始段階（0始まり）\n0=Stage 1, 1=Stage 2, 2=Stage 3")]
    [SerializeField] private int debugStartStage = 0;

    [Tooltip("ONにすると敵撃破のたびにログを出す。敵の大量同時撃破時、無条件ログはEditor上でスタックトレース取得コストが積み重なりフリーズの原因になりうるため、既定でOFF")]
    [SerializeField] private bool showDebugLog = false;

    [Header("UI References")]
    [Tooltip("ステージクリアメッセージを表示するUI")]
    [SerializeField] private StageClearUI stageClearUI;

    [Tooltip("全ステージクリア時のリザルト画面UI")]
    [SerializeField] private GameResultUI gameResultUI;

    [Tooltip("Stage3クリア後のジェム選択UI")]
    [SerializeField] private GemRewardUI gemRewardUI;

    [Tooltip("スキル選択UI")]
    [SerializeField] private Game.UI.SkillSelectionUI skillSelectionUI;

    [Tooltip("Stage開始前のカットイン演出UI（未設定時はカットインをスキップ）")]
    [SerializeField] private StageCutInUI stageCutInUI;

    [Tooltip("Stage1開始前のイントロ演出（未設定時はスキップ）")]
    [SerializeField] private StageIntroController stageIntroController;

    [Tooltip("Stage3開始直後・ボス出現前のVS演出（未設定、またはAreaConfig.vsBossSprite未設定時はスキップ）")]
    [SerializeField] private VsIntroUI vsIntroUI;

    [Tooltip("Area10ボスラッシュ専用の演出コントローラー。EnemySpawnerは全Area共通の1コンポーネントのため、" +
             "この参照は設定後は他Areaプレイ時もnullのままにならない。実際にボスラッシュ処理を発動してよいかは" +
             "必ずIsBossRushAreaプロパティ（areaConfig側の判定も含む）で確認すること")]
    [SerializeField] private Area10BossRushController bossRushController;

    /// <summary>
    /// 現在のAreaが本当にArea10ボスラッシュかどうか。bossRushController参照の有無だけでなく、
    /// 現在ロード中のAreaConfig自体がボスラッシュ用に設定されているか（skillSelectionCountOverride>0）も
    /// 必ず確認する。この判定が無いと、Boss Rush Controller欄を設定した瞬間から
    /// 既存Area1〜9でもボスラッシュ演出（背景・BGM・ブロック等）が誤発動してしまう
    /// （実際にこの不具合が発生したため、必ずこのプロパティ経由で判定すること）。
    /// </summary>
    private bool IsBossRushArea =>
        bossRushController != null && areaConfig != null && areaConfig.skillSelectionCountOverride > 0;


    // =========================================================
    // Wave System - Runtime Variables
    // =========================================================
    private int currentStageIndex = 0;
    private WaveStage currentStage;
    private System.Collections.Generic.List<int> usedFormationIndices = new System.Collections.Generic.List<int>();
    private float stageRemainingTime;
    private bool stageClearFlag = false;

    private int aliveCount;
    private int rrIndex = -1;
    // ★直近のSpawnFormation()呼び出しで実際に何体スポーンできたか（entries全滅・enemyData未設定の
    //   プレースホルダー等で1体も出現しなかった場合の誤判定防止に使う。Final Stageのような
    //   「敵データ未設定のプレースホルダー」formationはaliveCountが最初から0のままになるため、
    //   これを「敵を全滅させた」と誤判定してしまうと、本来存在しないボスの撃破時カード選択・
    //   BGM/背景切替が誤って走ってしまう）
    private int lastSpawnedEnemyCount = 0;

    // =========================================================
    // Enemy Kill Tracking (for Skill System)
    // =========================================================
    private int[] enemyKillsPerStage = new int[3]; // Stage 0, 1, 2 (= Stage 1, 2, 3)

    private void Start()
    {
#if UNITY_EDITOR
        if (GemRewardUI.DebugSkipGameplay) return;
#endif

        // 敵撃破数を初期化（念のため明示的に0にする）
        for (int i = 0; i < enemyKillsPerStage.Length; i++)
        {
            enemyKillsPerStage[i] = 0;
        }
        Debug.Log($"[EnemySpawner] Enemy kill counts initialized: [{enemyKillsPerStage[0]}, {enemyKillsPerStage[1]}, {enemyKillsPerStage[2]}]");

        // Area Config または GameSession からの設定読み込み
        LoadAreaConfiguration();

        // ★Area10ボスラッシュ専用：最初のボスの背景/BGM/ブロックを、下のFadeInOnStart()（黒画面から
        //   0.5秒でフェードイン）より前に確定させる。Area1〜9はBackgroundManager.Start()が
        //   Awake()順で既に本物の背景を設定済みのため、このフェードインで正しい絵がそのまま現れる。
        //   一方Area10Config自体にはFar/Mid/Silhouetteの実データが無いため、この処理を
        //   startDelay（デフォルト1秒）後のWaveSystemRoutine内で行っていた時は、
        //   フェードイン（0.5秒）が先に完了して一瞬何も無い画面が見えてしまい、
        //   その後startDelay経過時にフェード無しで背景が急に出現する不具合になっていた。
        if (IsBossRushArea && bossRushController != null)
            bossRushController.ApplySetupForFirstBossInstant();

        // シーン開始時にフェードイン
        StartCoroutine(FadeInOnStart());

        bool ok = true;

        if (enemyPrefab == null) { Debug.LogError("[EnemySpawner] enemyPrefab is not set."); ok = false; }
        if (enemyRoot == null) { Debug.LogError("[EnemySpawner] enemyRoot is not set."); ok = false; }
        if (projectileRoot == null) { Debug.LogError("[EnemySpawner] projectileRoot is not set."); ok = false; }
        if (enemyBulletPrefab == null) { Debug.LogError("[EnemySpawner] enemyBulletPrefab is not set."); ok = false; }

        // Spawn Points は Wave System と Legacy System の両方で必要
        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogError("[EnemySpawner] spawnPoints is empty. Please set Spawn Points.");
            ok = false;
        }

        // ウェーブシステムを使用する場合のバリデーション
        if (useWaveSystem)
        {
            if (waveStages == null || waveStages.Length == 0)
            {
                Debug.LogError("[EnemySpawner] Wave System is enabled but no Wave Stages are set.");
                ok = false;
            }
        }
        else
        {
            // レガシーシステムのバリデーション
            bool hasEnemyData = (enemyTypes != null && enemyTypes.Length > 0) || enemyData != null;
            if (!hasEnemyData)
            {
                Debug.LogError("[EnemySpawner] No enemy data set. Please set either 'Enemy Types' or 'Enemy Data' (legacy).");
                ok = false;
            }
        }

        if (!ok) return;

        // ウェーブシステムの初期化
        if (useWaveSystem)
        {
            InitializeWaveSystem();
        }
        else
        {
            Debug.LogWarning("[EnemySpawner] Wave System is DISABLED. Legacy spawn system will be used.");
        }

        StartCoroutine(SpawnRoutine());
    }

    /// <summary>
    /// エリア設定を読み込む
    /// </summary>
    private void LoadAreaConfiguration()
    {
        // GameSessionから自動読み込み（AreaSelectから明示的に設定された場合のみ）
        if (useAreaConfig && GameSession.HasValidArea())
        {
            areaConfig = GameSession.SelectedArea;
            Debug.Log($"[EnemySpawner] Loaded area config from GameSession: {areaConfig.GetDisplayName()}");
        }
        // GameSessionが無効な場合（直接05_Gameを再生した場合）、Area1をデフォルトとして読み込む
        else if (useAreaConfig)
        {
            AreaConfig defaultArea = Resources.Load<AreaConfig>("GameData/AreaDef_Area_01");
            if (defaultArea != null && defaultArea.IsValid())
            {
                areaConfig = defaultArea;
                Debug.Log($"[EnemySpawner] No valid GameSession. Loaded default Area1: {areaConfig.GetDisplayName()}");
            }
            else
            {
                Debug.LogWarning("[EnemySpawner] Could not load default Area1 config from Resources/GameData/AreaDef_Area_01");
            }
        }

        // Area Configが指定されている場合、そこからWave Stagesを読み込む
        if (useAreaConfig && areaConfig != null)
        {
            if (areaConfig.IsValid())
            {
                waveStages = areaConfig.waveStages;
                Debug.Log($"[EnemySpawner] Using Area Config: {areaConfig.GetDisplayName()}, Stages: {waveStages.Length}");
            }
            else
            {
                Debug.LogError($"[EnemySpawner] Area Config '{areaConfig.name}' is invalid!");
            }
        }
    }

    /// <summary>
    /// 外部からエリア設定を設定する（ランタイム用）
    /// </summary>
    public void SetAreaConfig(AreaConfig config)
    {
        if (config == null)
        {
            Debug.LogError("[EnemySpawner] SetAreaConfig: config is null!");
            return;
        }

        if (!config.IsValid())
        {
            Debug.LogError($"[EnemySpawner] SetAreaConfig: config '{config.name}' is invalid!");
            return;
        }

        areaConfig = config;
        waveStages = config.waveStages;
        useAreaConfig = true;

        Debug.Log($"[EnemySpawner] Area config set to: {config.GetDisplayName()}");
    }

    /// <summary>
    /// ウェーブシステムの初期化
    /// </summary>
    private void InitializeWaveSystem()
    {
        // デバッグモードなら指定段階から開始
        if (debugMode && debugStartStage >= 0 && debugStartStage < waveStages.Length)
        {
            currentStageIndex = debugStartStage;
        }
        else
        {
            currentStageIndex = 0;
        }

        usedFormationIndices.Clear();

        // ★Area10ボスラッシュ専用デバッグ機能：Area10BossRushController.DebugStartBossIndex
        //   （Inspector上で0=ボス1〜8=ボス9を指定）が設定されていれば、そのボスが属するStageから
        //   開始し、同じStage内でそれより前のボスのFormationを「使用済み」として事前にマークしておく
        //   （useSequentialFormationOrder=trueのため、PickUnusedFormation()は残った中で一番若い
        //   インデックスを選ぶ＝結果的に指定したボスのFormationが次に選ばれる）。
        //   通常プレイ（DebugStartBossIndex=-1、デフォルト）ではこのブロックは実行されず、
        //   既存の動作（currentStageIndex=0またはdebugStartStage、usedFormationIndices空）のまま変わらない。
        //   Area10以外（bossRushController==null or IsBossRushArea==false）でも同様に無関係。
        if (IsBossRushArea && bossRushController != null && bossRushController.DebugStartBossIndex >= 0
            && bossRushController.BossEntryCount > 0)
        {
            // ★上限はBossEntryCount（9体の枠の1つ先）まで許容する。これは「Final Stage直前から開始」を
            //   表す特別値（Area10BossRushController.ApplySetupForFirstBossInstant側で対応済み）。
            int debugIndex = Mathf.Clamp(bossRushController.DebugStartBossIndex, 0, bossRushController.BossEntryCount);
            currentStageIndex = Mathf.Clamp(debugIndex / 3, 0, waveStages.Length - 1);
            int formationIndexWithinStage = debugIndex % 3;
            for (int i = 0; i < formationIndexWithinStage; i++)
                usedFormationIndices.Add(i);
        }

        currentStage = waveStages[currentStageIndex];
        stageRemainingTime = currentStage.timeLimit;
        stageClearFlag = false;
    }

    private IEnumerator SpawnRoutine()
    {
        // イントロ開始前から線を引けないようにする
        if (PaddleDrawer.Instance != null) PaddleDrawer.Instance.enabled = false;
        SlowMotionUIManager.Instance?.SetInputEnabled(false);
        // ★Play開始直後、startDelayの待機中〜カットインが実際に始まるまでの間は
        //   WaveSystemRoutine側のブロック処理にまだ到達しておらず、中断メニューを開けてしまう
        //   不具合があった。ここでも先にブロックしておく（解除はWaveSystemRoutine側の
        //   イントロ/カットイン終了時、レガシーモードは下のelseブロックで行う）
        PauseManager.Instance?.SetPauseBlocked(true);

        yield return new WaitForSeconds(startDelay);

        if (useWaveSystem)
        {
            yield return StartCoroutine(WaveSystemRoutine());
        }
        else
        {
            // レガシーモードでは即有効化
            if (PaddleDrawer.Instance != null) PaddleDrawer.Instance.enabled = true;
            SlowMotionUIManager.Instance?.SetInputEnabled(true);
            PauseManager.Instance?.SetPauseBlocked(false);
            yield return StartCoroutine(LegacySpawnRoutine());
        }
    }

    /// <summary>
    /// レガシーシステムのスポーンルーチン（従来の動作）
    /// </summary>
    private IEnumerator LegacySpawnRoutine()
    {
        while (true)
        {
            if (aliveCount < maxAliveEnemies)
            {
                Transform sp = PickSpawnPointRoundRobin();
                SpawnAt(sp);
            }

            yield return new WaitForSeconds(spawnInterval);
        }
    }

    /// <summary>
    /// ウェーブシステムのメインル ーチン
    /// </summary>
    private IEnumerator WaveSystemRoutine()
    {
        // ★このメソッドが最初に回る1周目かどうか（Area10ボスラッシュのデバッグ開始ボス機能で使う）。
        //   InitializeWaveSystem()は呼び出し直前にcurrentStageIndex/usedFormationIndicesを
        //   既に正しく設定済み（デバッグ開始ボスの分だけ手前のFormationを使用済みマーク済み）のため、
        //   1周目でここのusedFormationIndices.Clear()や下のPrepareNextBossRoutine(currentStageIndex*3)を
        //   実行してしまうと、その事前設定を上書き・無効化してしまう
        //   （デバッグでボス4以降から開始してもボス1やStageの最初のボスに戻ってしまう不具合になる）。
        //   2周目以降（実際に前のStageから遷移してきた時）は今まで通りクリア・同期を行う。
        bool isFirstStageIteration = true;

        while (currentStageIndex < waveStages.Length)
        {
            currentStage = waveStages[currentStageIndex];
            stageRemainingTime = currentStage.timeLimit;
            if (!isFirstStageIteration)
                usedFormationIndices.Clear();
            stageClearFlag = false;

            // ★Area10ボスラッシュ最初のボスの背景/BGM/ブロック即時適用はStart()側（FadeInOnStart()より前）
            //   に移動済み。ここで再度呼ぶと即時適用（背景・ブロック生成等）が二重に走ってしまうため呼ばない。

            // ★スタミナ消費：Area1〜10のStage1開始時のみ。F1テストエリア・チュートリアルは対象外。
            //   イントロ/カットイン演出より前に実行すること。演出中に強制終了された場合、
            //   消費処理自体がまだ実行されていない状態になり「消費されなかった」ように見えるバグがあった。
            if (currentStageIndex == 0 && !GameSession.IsTestArea && !GameSession.IsInTutorial)
            {
                StaminaManager.Instance?.TryConsume();
            }

            // Stage1のみ: イントロ演出（カットインの前に実行）
            if (currentStageIndex == 0 && stageIntroController != null)
            {
                PauseManager.Instance?.SetPauseBlocked(true);
                if (PaddleDrawer.Instance != null) PaddleDrawer.Instance.enabled = false;
                SlowMotionUIManager.Instance?.SetInputEnabled(false);
                yield return StartCoroutine(stageIntroController.PlayIntro());
                PauseManager.Instance?.SetPauseBlocked(false);
            }

            // Stage開始前カットイン演出（ポーズを一時ブロック）
            if (stageCutInUI != null)
            {
                PauseManager.Instance?.SetPauseBlocked(true);
                if (currentStageIndex > 0)
                {
                    if (PaddleDrawer.Instance != null) PaddleDrawer.Instance.enabled = false;
                    SlowMotionUIManager.Instance?.SetInputEnabled(false);
                }
                yield return StartCoroutine(stageCutInUI.ShowCutIn(currentStageIndex));
                PauseManager.Instance?.SetPauseBlocked(false);
            }
            // Stage1のみ: カットイン完了後にStartPose非表示 + PixelDancer有効化
            if (currentStageIndex == 0 && stageIntroController != null)
            {
                stageIntroController.OnCutInComplete();
                if (PaddleDrawer.Instance != null) PaddleDrawer.Instance.enabled = true;
                SlowMotionUIManager.Instance?.SetInputEnabled(true);
            }
            else if (currentStageIndex > 0)
            {
                // Stage2/3: カットイン終了後（カットインなしの場合も含む）に有効化
                if (PaddleDrawer.Instance != null) PaddleDrawer.Instance.enabled = true;
                SlowMotionUIManager.Instance?.SetInputEnabled(true);
            }

            OnStageStarted?.Invoke(currentStageIndex);

            // Stage2以降: Background遷移完了を待機してからエネミースポーン
            if (currentStageIndex >= 1)
            {
                if (BackgroundManager.Instance != null)
                    yield return new WaitUntil(() => !BackgroundManager.Instance.IsTransitioning);
                yield return new WaitForSeconds(backgroundClearDelay);
            }

            // Stage3のみ: ボス出現前のVS演出（AreaConfigにvsBossSpriteが設定されている場合のみ）
            if (currentStageIndex == 2 && vsIntroUI != null && areaConfig != null && areaConfig.vsBossSprite != null)
            {
                PauseManager.Instance?.SetPauseBlocked(true);
                if (PaddleDrawer.Instance != null) PaddleDrawer.Instance.enabled = false;
                SlowMotionUIManager.Instance?.SetInputEnabled(false);
                yield return StartCoroutine(vsIntroUI.PlayIntro(areaConfig.vsBossSprite, areaConfig.vsBossNameSprite, areaConfig.vsBossThemeColor, areaConfig.vsBossScale, areaConfig.vsBossPositionOffset));
                if (PaddleDrawer.Instance != null) PaddleDrawer.Instance.enabled = true;
                SlowMotionUIManager.Instance?.SetInputEnabled(true);
                PauseManager.Instance?.SetPauseBlocked(false);
            }

            // Area10ボスラッシュ：次ボスのBGM/背景/ブロック演出を先に完了させる
            // （Stage1の最初のボスは上のApplySetupForFirstBossInstant()で済んでいるためスキップ。Area10以外はnullなので無関係）。
            // forceTargetIndex=currentStageIndex*3を渡すことで、前Stageが時間切れで残りボスをスキップして
            // 終わった場合でも、新Stageの最初のボスへ正しく同期する（背景が前Stage最後のボスのままにならないようにする）。
            // ★1周目（isFirstStageIteration）はApplySetupForFirstBossInstant()側で既に正しいボス
            //   （通常はボス1、デバッグ開始ボス指定時はそのボス）の背景/BGM/ブロックが適用済みのため、
            //   ここで強制的にStageの先頭ボス（currentStageIndex*3）へ同期し直してはいけない。
            if (IsBossRushArea && currentStageIndex > 0 && !isFirstStageIteration)
                yield return StartCoroutine(bossRushController.PrepareNextBossRoutine(currentStageIndex * 3));

            isFirstStageIteration = false;

            // 最初の配置パターンをスポーン
            yield return StartCoroutine(SpawnFormation());

            // Area10ボスラッシュ：スポーン直後のボスにフェードイン秒数を上書きする（共有Prefab自体は変更しない）
            if (IsBossRushArea)
                bossRushController.ApplyFadeInOverrideToNewSpawns(enemyRoot);

            // ★Final Stageのような「enemyData未設定のプレースホルダー」formationは、上のSpawnFormation()を
            //   呼んでも1体もスポーンされずaliveCountが0のままになる。これを「敵を全滅させた」と
            //   誤判定すると、本来存在しないボスの撃破時カード選択・BGM/背景切替が誤って走ってしまう
            //   （Final Stage到達時に発生した不具合）。何も出現しなかった場合は、このStageで
            //   これ以上できることが無いと判断し、カード等を出さず静かに終了させる。
            if (lastSpawnedEnemyCount == 0 && aliveCount <= 0)
            {
                stageClearFlag = true;
            }

            // 段階クリアまでループ
            while (!stageClearFlag)
            {
                // タイマー更新（時間制限がある場合）
                // ゲームオーバー進行中はタイマーを停止
                bool isGameOverInProgress = GameManager.Instance != null && GameManager.Instance.IsGameOverInProgress();

                if (currentStage.timeLimit > 0 && !isGameOverInProgress)
                {
                    float timeScale = SlowMotionManager.Instance != null ? SlowMotionManager.Instance.TimeScale : 1f;
                    stageRemainingTime -= Time.deltaTime * timeScale;

                    // 時間切れチェック
                    if (stageRemainingTime <= 0)
                    {
                        if (currentStage.clearOnTimeExpired)
                        {
                            // 時間経過でクリア
                            ClearRemainingEnemies(isKilled: false);  // 時間経過による消滅
                            FadeOutAllBullets(0.5f);  // 画面上の弾をフェードアウトして削除
                            stageClearFlag = true;
                        }
                        else
                        {
                            // 時間切れだが、まだクリアしていない（全滅が必要）
                            stageRemainingTime = 0;
                        }
                    }
                }

                // 敵が全滅したら次の配置パターンをスポーン
                if (aliveCount <= 0 && !stageClearFlag)
                {
                    bool hasMoreFormations = HasNextFormation();

                    // 全滅後の待機（スキル選択画面が開くまでの間）
                    yield return new WaitForSeconds(formationTransitionDelay);

                    // Formation切り替え時のスキル選択（Stage 1と2のみ・先にUIを出す。
                    // skillSelectionCountOverride>0のArea＝Area10ボスラッシュはStage3相当でも選択を出す）
                    bool isBossRushArea = areaConfig != null && areaConfig.skillSelectionCountOverride > 0;
                    bool allowSkillSelectionThisStage = ((currentStageIndex == 0 || currentStageIndex == 1) || isBossRushArea)
                        && !(currentStage != null && currentStage.disableSkillSelection);
                    // Area10ボスラッシュは、Stage内の最後（3体目）のボスを倒した時もカードを出す
                    // （既存Areaはformationが残っていない時は出さない仕様のまま変えない）
                    if (allowSkillSelectionThisStage && (hasMoreFormations || isBossRushArea) && skillSelectionUI != null)
                    {
                        FadeOutAllBullets(0.5f);  // スキル選択前に残弾をフェードアウト

                        // スキル選択回数：通常は1回。AreaConfig側でオーバーライドが設定されていればその回数を使う（Area10専用）。
                        // さらにWaveStage側にStage毎の上書き値（skillSelectionCountOverride>0）が設定されていれば、
                        // そちらを優先する（ボス1-3/4-6/7-9でカード枚数を変えたい場合に使う）
                        int selectionCount;
                        if (currentStage != null && currentStage.skillSelectionCountOverride > 0)
                            selectionCount = currentStage.skillSelectionCountOverride;
                        else if (areaConfig != null && areaConfig.skillSelectionCountOverride > 0)
                            selectionCount = areaConfig.skillSelectionCountOverride;
                        else
                            selectionCount = 1;

                        // Stage2相当で50/50重みブレンドを使うか（Area10ボスラッシュ専用）
                        bool useBlendedWeights = currentStageIndex == 1
                            && areaConfig != null && areaConfig.useStage2BlendedSkillWeights;

                        // スキル選択開始（全スキルから選択、StageIndexを渡す）
                        bool skillSelectionComplete = false;
                        skillSelectionUI.StartSkillSelection(Game.Skills.SkillCategory.All, selectionCount, () =>
                        {
                            skillSelectionComplete = true;
                        }, currentStageIndex, useBlendedWeights);

                        // スキル選択完了まで待機
                        yield return new WaitUntil(() => skillSelectionComplete);

                        // スキル選択完了後、敵スポーンまでの追加待機
                        if (postSkillSelectionSpawnDelay > 0f)
                            yield return new WaitForSeconds(postSkillSelectionSpawnDelay);
                    }

                    // Area10ボスラッシュ：次ボスのBGM/背景/ブロック演出を先に完了させる（Area10以外は発動しない）
                    if (IsBossRushArea && hasMoreFormations)
                        yield return StartCoroutine(bossRushController.PrepareNextBossRoutine());

                    // スキル選択後に敵をスポーン
                    yield return StartCoroutine(SpawnFormation());

                    // Area10ボスラッシュ：スポーン直後のボスにフェードイン秒数を上書きする（共有Prefab自体は変更しない）
                    if (IsBossRushArea && hasMoreFormations)
                        bossRushController.ApplyFadeInOverrideToNewSpawns(enemyRoot);

                    // 配置パターンがなく、時間制限もない（または clearOnTimeExpired=false）場合はクリア。
                    // stopAfterFormationsExhausted=true（Area10ボスラッシュ）の場合は、時間制限の設定に関わらず
                    // Formationを使い切った時点で即座にクリアする（既存Areaはfalseなので影響なし）
                    if (!hasMoreFormations)
                    {
                        if (currentStage.timeLimit <= 0 || !currentStage.clearOnTimeExpired || currentStage.stopAfterFormationsExhausted)
                        {
                            FadeOutAllBullets(0.5f);  // 画面上の弾をフェードアウトして削除
                            stageClearFlag = true;
                        }
                    }
                }

                yield return null;
            }

            OnStageCleared?.Invoke(currentStageIndex);

            // ステージクリアメッセージ表示
            if (stageClearUI != null)
            {
                stageClearUI.ShowStageClear(currentStageIndex + 1);
            }

            // 【旧仕様：Stageクリア時のスキル選択】コメントアウト（万が一の時に戻すため）
            // スキル選択（Stage 1 と Stage 2 クリア後のみ）
            //if (currentStageIndex == 0 || currentStageIndex == 1)
            //{
            //    // Stage 1 クリア後 → カテゴリA
            //    // Stage 2 クリア後 → カテゴリB
            //    Game.Skills.SkillCategory category = currentStageIndex == 0
            //        ? Game.Skills.SkillCategory.CategoryA
            //        : Game.Skills.SkillCategory.CategoryB;
            //
            //    int killCount = GetEnemyKillCount(currentStageIndex);
            //
            //    Debug.Log($"[EnemySpawner] Stage {currentStageIndex + 1} cleared. Enemy kills: {killCount}");
            //
            //    if (killCount > 0 && skillSelectionUI != null)
            //    {
            //        Debug.Log($"[EnemySpawner] Starting skill selection: Category={category}, Count={killCount}");
            //        // スキル選択開始（完了まで待機）
            //        bool skillSelectionComplete = false;
            //        skillSelectionUI.StartSkillSelection(category, killCount, () =>
            //        {
            //            skillSelectionComplete = true;
            //        });
            //
            //        // スキル選択完了まで待機
            //        yield return new WaitUntil(() => skillSelectionComplete);
            //    }
            //    else if (killCount == 0)
            //    {
            //        Debug.Log($"[EnemySpawner] Skipping skill selection (no enemies killed)");
            //    }
            //}

            // 次の段階へ
            currentStageIndex++;

            if (currentStageIndex < waveStages.Length)
            {
                if (stageCutInUI != null)
                {
                    // StageClearメッセージを見せてからカットインへ（実時間1.5秒待機）
                    // この時点からポーズをブロック（カットイン終了時に解除）
                    PauseManager.Instance?.SetPauseBlocked(true);
                    yield return new WaitForSecondsRealtime(1.5f);
                    // カットインは次のループ冒頭で実行されるためここでは何もしない
                }
                else
                {
                    // カットイン未設定時は従来どおり3秒待機
                    yield return new WaitForSeconds(3f);
                }
            }
        }

        // ボス撃破後はライン入力・ポーズを無効化・中断ボタンUIを非表示
        PauseManager.Instance?.SetPauseBlocked(true);
        FindFirstObjectByType<WaveTimerUI>()?.SetPauseButtonVisible(false);
        if (PaddleDrawer.Instance != null) PaddleDrawer.Instance.enabled = false;
        SlowMotionUIManager.Instance?.SetInputEnabled(false);

        // Area Complete演出（タイムスロー→Finishアニメ→テキスト）
        if (stageIntroController != null)
            yield return StartCoroutine(stageIntroController.PlayAreaComplete());
        else
            yield return new WaitForSeconds(3f);

        // ステージクリアを ProgressManager に保存
        if (ProgressManager.Instance != null)
        {
            string targetAreaId = ProgressManager.Instance.Data.selectedAreaId;

            if (!string.IsNullOrEmpty(targetAreaId))
            {
                // 全ステージクリア時は最終ステージ（Stage 3）を必ずクリア扱いにする
                // これにより次のエリアが確実にアンロックされる
                int finalStage = 3; // デフォルトは3

                // AreaDB から最終ステージ番号を取得
                var stageNumbers = AreaDB.Instance?.GetStageNumbers(targetAreaId);
                if (stageNumbers != null && stageNumbers.Length > 0)
                {
                    finalStage = stageNumbers[stageNumbers.Length - 1];
                }

                bool changed = ProgressManager.Instance.MarkStageCleared(targetAreaId, finalStage);
                Debug.Log($"[EnemySpawner] {targetAreaId} Stage {finalStage} (final stage) cleared and saved. (new? {changed})");

                // ★無限化の石：課金導線とは別に、Area2/5/8の初回クリア時だけお試しで1個ずつ付与する
                //   （最大3個。MarkStageClearedのnew判定により各エリア一度きりなので、これ以上は増えない）
                if (changed && (targetAreaId == "Area_02" || targetAreaId == "Area_05" || targetAreaId == "Area_08"))
                {
                    InfiniteStoneManager.Instance?.Add(1);
                    SessionStats.AddInfiniteStoneEarned(1);
                    Debug.Log($"[EnemySpawner] {targetAreaId} first clear: InfiniteStone +1 granted.");
                }
            }
            else
            {
                Debug.LogError("[EnemySpawner] Cannot save stage clear: selectedAreaId is empty!");
            }
        }
        else
        {
            Debug.LogError("[EnemySpawner] Cannot save stage clear: ProgressManager.Instance is null!");
        }

        // Stage3クリア：セッションゴールドを永続ゴールドに加算
        GoldManager.Instance?.TransferSessionGoldToPersistent();

        // ★Areaボス撃破後、リザルト/ジェム選択画面に移ってもセルフヒール(C3)が動き続けて
        //   HPが回復し続けてしまうため、ここで明示的に止める
        Game.Skills.SkillManager.Instance?.StopSelfHeal();

        // ジェム選択UI → 終了後に GameResultUI へ続く
        string clearedAreaId = ProgressManager.Instance?.Data?.selectedAreaId ?? "";
        if (gemRewardUI != null)
        {
            gemRewardUI.Open(clearedAreaId);
        }
        else if (gameResultUI != null)
        {
            gameResultUI.ShowAllClearResult();
        }
    }

    /// <summary>
    /// 配置パターンから敵をスポーンする
    /// </summary>
    private IEnumerator SpawnFormation()
    {
        lastSpawnedEnemyCount = 0;

        if (currentStage == null || currentStage.formations == null || currentStage.formations.Length == 0)
        {
            Debug.LogWarning($"[EnemySpawner] SpawnFormation() failed: currentStage={currentStage != null}, formations={(currentStage != null && currentStage.formations != null ? currentStage.formations.Length.ToString() : "null")}");
            yield break;
        }

        // 未使用の配置パターンを選択
        EnemyFormation selectedFormation = PickUnusedFormation();
        if (selectedFormation == null)
        {
            Debug.LogWarning("[EnemySpawner] SpawnFormation() failed: No unused formations available");
            yield break;
        }

        Debug.Log($"[EnemySpawner] SpawnFormation() spawning: {selectedFormation.formationName}, entries={selectedFormation.entries?.Length ?? 0}");

        if (selectedFormation.entries == null)
            yield break;

        // 有効なエントリーのインデックスを収集
        var validIndices = new System.Collections.Generic.List<int>();
        for (int i = 0; i < selectedFormation.entries.Length; i++)
        {
            var entry = selectedFormation.entries[i];
            if (entry == null || entry.enemyData == null) continue;
            if (spawnPoints == null || entry.spawnPointIndex < 0 || entry.spawnPointIndex >= spawnPoints.Length)
            {
                Debug.LogWarning($"[EnemySpawner] Invalid spawn point index {entry.spawnPointIndex} in formation: {selectedFormation.formationName} (Available: 0-{spawnPoints?.Length - 1 ?? -1})");
                continue;
            }
            if (spawnPoints[entry.spawnPointIndex] == null)
            {
                Debug.LogWarning($"[EnemySpawner] Spawn Point at index {entry.spawnPointIndex} is null in formation: {selectedFormation.formationName}");
                continue;
            }
            validIndices.Add(i);
        }

        // Stage1 かつ複数敵の場合: ランダム順でスタガー出現
        bool stagger = currentStageIndex == 0 && validIndices.Count > 1;
        if (stagger)
        {
            for (int i = validIndices.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                int tmp = validIndices[i];
                validIndices[i] = validIndices[j];
                validIndices[j] = tmp;
            }
        }

        int spawnedCount = 0;
        for (int k = 0; k < validIndices.Count; k++)
        {
            var entry = selectedFormation.entries[validIndices[k]];
            SpawnAtWithData(spawnPoints[entry.spawnPointIndex], entry.enemyData);
            spawnedCount++;
            if (stagger && k < validIndices.Count - 1)
                yield return new WaitForSeconds(formationEnemyStaggerDelay);
        }

        Debug.Log($"[EnemySpawner] SpawnFormation() completed: spawned {spawnedCount} enemies");
        lastSpawnedEnemyCount = spawnedCount;
    }

    /// <summary>
    /// 次のFormationが利用可能かどうかを確認する（usedFormationIndicesを消費しない）
    /// </summary>
    private bool HasNextFormation()
    {
        if (currentStage == null || currentStage.formations == null || currentStage.formations.Length == 0)
            return false;

        // Stage 0/1: 全使用済みでもプールをリセットして再利用するため常にtrue
        // （ただしstopAfterFormationsExhausted=trueの場合はStage0/1でも打ち止めにする。Area10のボスラッシュ専用）
        if ((currentStageIndex == 0 || currentStageIndex == 1) && !currentStage.stopAfterFormationsExhausted)
            return true;

        // Stage 2、またはstopAfterFormationsExhausted=trueの場合: 未使用のFormationが残っているかチェック
        for (int i = 0; i < currentStage.formations.Length; i++)
        {
            if (!usedFormationIndices.Contains(i))
                return true;
        }
        return false;
    }

    /// <summary>
    /// 未使用の配置パターンをランダムに選択
    /// </summary>
    private EnemyFormation PickUnusedFormation()
    {
        if (currentStage.formations.Length == 0) return null;

        // 未使用のフォーメーションのインデックスリストを作成
        System.Collections.Generic.List<int> availableIndices = new System.Collections.Generic.List<int>();
        for (int i = 0; i < currentStage.formations.Length; i++)
        {
            if (!usedFormationIndices.Contains(i))
            {
                availableIndices.Add(i);
            }
        }

        // 全て使用済みの場合の処理
        if (availableIndices.Count == 0)
        {
            // Stage 1と2のみリセットして再利用（Stage 3は時間制限が無いためリセットしない。
            // stopAfterFormationsExhausted=trueの場合はStage0/1でもリセットしない。Area10のボスラッシュ専用）
            if ((currentStageIndex == 0 || currentStageIndex == 1) && !currentStage.stopAfterFormationsExhausted)
            {
                Debug.Log($"[EnemySpawner] All formations used in Stage {currentStageIndex + 1}. Resetting formation pool.");
                usedFormationIndices.Clear();

                // 再度利用可能なインデックスリストを作成
                for (int i = 0; i < currentStage.formations.Length; i++)
                {
                    availableIndices.Add(i);
                }
            }
            else
            {
                // Stage 3は全て使い切ったらnullを返す
                Debug.Log($"[EnemySpawner] All formations used in Stage {currentStageIndex + 1}. No more formations available.");
                return null;
            }
        }

        // 選択（Area10ボスラッシュ等、順番が重要な場合はインデックスの小さい順。それ以外は従来通りランダム）
        int randomIndex;
        if (currentStage.useSequentialFormationOrder)
        {
            availableIndices.Sort();
            randomIndex = availableIndices[0];
        }
        else
        {
            randomIndex = availableIndices[Random.Range(0, availableIndices.Count)];
        }
        usedFormationIndices.Add(randomIndex);

        EnemyFormation selectedFormation = currentStage.formations[randomIndex];
        Debug.Log($"[EnemySpawner] Formation selected: Index={randomIndex}, Name={selectedFormation.formationName}, Available={availableIndices.Count}/{currentStage.formations.Length}");

        return selectedFormation;
    }

    /// <summary>
    /// 残っている敵を全て消滅させる
    /// </summary>
    /// <param name="isKilled">true=倒された, false=時間経過で消滅</param>
    private void ClearRemainingEnemies(bool isKilled)
    {
        if (enemyRoot != null)
        {
            // 各敵のEnemyStatsを取得してDie()を呼び出す
            // これにより、撃破と時間経過で異なるエフェクトを再生できる
            foreach (Transform child in enemyRoot)
            {
                if (child != null)
                {
                    EnemyStats stats = child.GetComponent<EnemyStats>();
                    if (stats != null)
                    {
                        stats.Die(isKilled);
                    }
                    else
                    {
                        // EnemyStatsがない場合は直接破棄
                        Destroy(child.gameObject);
                    }
                }
            }
        }

        aliveCount = 0;
    }

    /// <summary>
    /// 画面上の全ての弾を当たり判定無効化＋フェードアウトして消滅させる。
    /// </summary>
    private void FadeOutAllBullets(float duration)
    {
        if (projectileRoot == null) return;
        var children = new System.Collections.Generic.List<Transform>();
        foreach (Transform child in projectileRoot)
            children.Add(child);
        foreach (Transform child in children)
        {
            if (child == null) continue;
            var bullet = child.GetComponent<EnemyBullet>();
            if (bullet != null)
                bullet.StartFadeAndDestroy(duration);
            else
                Destroy(child.gameObject);
        }
    }

    /// <summary>
    /// 画面上の全ての弾を削除する
    /// </summary>
    private void ClearAllBullets()
    {
        if (projectileRoot != null)
        {
            // まず全ての子オブジェクトをリストに集める
            System.Collections.Generic.List<Transform> children = new System.Collections.Generic.List<Transform>();
            foreach (Transform child in projectileRoot)
            {
                children.Add(child);
            }

            // リストから削除
            foreach (Transform child in children)
            {
                if (child != null)
                {
                    Destroy(child.gameObject);
                }
            }
        }
    }

    private Transform PickSpawnPointRoundRobin()
    {
        rrIndex++;
        if (rrIndex >= spawnPoints.Length) rrIndex = 0;
        return spawnPoints[rrIndex];
    }

    /// <summary>
    /// 重み付き確率選択でEnemyDataを取得
    /// enemyTypesが設定されていない場合は、レガシーのenemyDataを使用（後方互換性）
    /// </summary>
    private EnemyData PickEnemyDataByWeight()
    {
        // enemyTypesが設定されていない場合は、レガシーのenemyDataを使用
        if (enemyTypes == null || enemyTypes.Length == 0)
        {
            return enemyData;
        }

        // 総重みを計算
        float totalWeight = 0f;
        foreach (var entry in enemyTypes)
        {
            if (entry != null && entry.enemyData != null)
            {
                totalWeight += entry.weight;
            }
        }

        if (totalWeight <= 0f)
        {
            Debug.LogWarning("[EnemySpawner] Total weight is 0. Using first enemy data.");
            return enemyTypes[0]?.enemyData ?? enemyData;
        }

        // ランダム値を取得（0 ~ totalWeight）
        float randomValue = Random.Range(0f, totalWeight);

        // 重みに基づいて選択
        float currentWeight = 0f;
        foreach (var entry in enemyTypes)
        {
            if (entry == null || entry.enemyData == null) continue;

            currentWeight += entry.weight;
            if (randomValue <= currentWeight)
            {
                return entry.enemyData;
            }
        }

        // フォールバック（通常ここには到達しない）
        return enemyTypes[0]?.enemyData ?? enemyData;
    }

    /// <summary>
    /// レガシーシステム用：重み付き確率選択で敵をスポーン
    /// </summary>
    private void SpawnAt(Transform spawnPoint)
    {
        if (spawnPoint == null) return;

        // 重み付き確率選択でEnemyDataを取得
        EnemyData selectedData = PickEnemyDataByWeight();
        if (selectedData == null)
        {
            Debug.LogError("[EnemySpawner] Failed to pick enemy data. Skipping spawn.");
            return;
        }

        SpawnAtWithData(spawnPoint, selectedData);
    }

    /// <summary>
    /// ★追加：ウェーブシステム外（ボスの増援召喚等）から、指定位置に指定EnemyDataの敵をスポーンする公開入口。
    /// 既存のSpawnAtWithData（Layer設定・ApplyEnemyData・フェードイン設定・aliveCount加算まで全て含む）をそのまま使う。
    /// 生成したGameObjectを返す（呼び出し側で生存確認等に使うため）。
    /// </summary>
    public GameObject SpawnEnemyAt(Transform spawnPoint, EnemyData data)
    {
        return SpawnAtWithData(spawnPoint, data);
    }

    /// <summary>
    /// 指定されたEnemyDataで敵をスポーン（ウェーブシステム用）
    /// </summary>
    private GameObject SpawnAtWithData(Transform spawnPoint, EnemyData data)
    {
        if (spawnPoint == null || data == null) return null;

        GameObject prefabToSpawn = (data.prefabOverride != null) ? data.prefabOverride : enemyPrefab;
        GameObject enemy = Instantiate(prefabToSpawn, spawnPoint.position, Quaternion.identity, enemyRoot);
        enemy.name = $"Enemy_{Time.frameCount}_{data.name}";

        // ★敵と全ての子オブジェクトを Enemy Layer に設定
        SetLayerRecursively(enemy, LayerMask.NameToLayer("Enemy"));

        ApplyEnemyData(enemy, data);

        // EnemyStatsにSpawnerへの参照を設定
        EnemyStats stats = enemy.GetComponent<EnemyStats>();
        if (stats != null)
        {
            stats.SetSpawner(this);
            float fadeInDur = (currentStageIndex == 2) ? stage3FadeInDuration : stage12FadeInDuration;
            stats.SetFadeInDuration(fadeInDur);
        }

        aliveCount++;
        return enemy;
    }

    /// <summary>
    /// スライム分裂用スポーン。
    /// プレハブから正しく生成し、EnemyData を全コンポーネントに注入する。
    /// aliveCount も加算するため、倒したら正しく波クリア判定される。
    /// </summary>
    /// <param name="data">元スライムの EnemyData</param>
    /// <param name="worldPosition">出現ワールド座標</param>
    /// <param name="hp">引き継ぐ HP（分裂前の現在 HP）</param>
    /// <param name="scaleMultiplier">ベーススケールへの乗算倍率</param>
    public void SpawnCloneSlime(EnemyData data, Vector3 worldPosition, int hp, float scaleMultiplier)
    {
        if (data == null) return;

        GameObject prefabToSpawn = (data.prefabOverride != null) ? data.prefabOverride : enemyPrefab;
        if (prefabToSpawn == null) return;

        GameObject clone = Instantiate(prefabToSpawn, worldPosition, Quaternion.identity, enemyRoot);
        clone.name = $"SlimeClone_{Time.frameCount}";

        SetLayerRecursively(clone, LayerMask.NameToLayer("Enemy"));

        // 通常スポーンと同じ注入処理（sprite, scale, shooter, shield 等）
        ApplyEnemyData(clone, data);

        // HP を分裂前の現在 HP で上書き（ApplyEnemyData は data.maxHp をセットするため）
        EnemyStats cloneStats = clone.GetComponent<EnemyStats>();
        if (cloneStats != null)
        {
            cloneStats.ApplyMaxHp(hp);
            cloneStats.SetSpawner(this);
        }

        // SlimeEnemy のスケール管理を設定
        // ApplyEnemyData が spriteScale を localScale に適用した後の値がベーススケール
        SlimeEnemy cloneSlime = clone.GetComponent<SlimeEnemy>();
        if (cloneSlime != null)
        {
            cloneSlime.baseLocalScale = clone.transform.localScale;
            cloneSlime.baseScaleInitialized = true;
            cloneSlime.SetScaleMultiplier(scaleMultiplier);
        }

        aliveCount++;
    }

    /// <summary>
    /// GyroWard の分裂時に呼ばれる。Instantiate(gameObject) ではなくプレハブから正しく生成する
    /// （SpawnCloneSlime と同じ考え方）。
    /// </summary>
    /// <param name="data">元GyroWardのEnemyData</param>
    /// <param name="worldPosition">出現ワールド座標</param>
    /// <param name="hp">引き継ぐHP（分裂前の現在HP）</param>
    /// <param name="lissajousPhaseOffsetDeg">Lissajous移動の位相（度）。分裂のたびにずらして軌道の重なりを防ぐ</param>
    public void SpawnCloneGyroWard(EnemyData data, Vector3 worldPosition, int hp, float lissajousPhaseOffsetDeg)
    {
        if (data == null) return;

        GameObject prefabToSpawn = (data.prefabOverride != null) ? data.prefabOverride : enemyPrefab;
        if (prefabToSpawn == null) return;

        GameObject clone = Instantiate(prefabToSpawn, worldPosition, Quaternion.identity, enemyRoot);
        clone.name = $"GyroWardClone_{Time.frameCount}";

        SetLayerRecursively(clone, LayerMask.NameToLayer("Enemy"));

        // 通常スポーンと同じ注入処理（sprite, scale, shooter, shield 等）
        ApplyEnemyData(clone, data);

        // HP を分裂前の現在 HP で上書き（ApplyEnemyData は data.maxHp をセットするため）
        EnemyStats cloneStats = clone.GetComponent<EnemyStats>();
        if (cloneStats != null)
        {
            cloneStats.ApplyMaxHp(hp);
            cloneStats.SetSpawner(this);
        }

        GyroWardController cloneController = clone.GetComponent<GyroWardController>();
        if (cloneController != null)
        {
            cloneController.SetLissajousPhaseOffsetDeg(lissajousPhaseOffsetDeg);
        }

        aliveCount++;
    }

    /// <summary>
    /// EnemySpawner 管理外の敵（GravePoleEnemy が生成する Drone 等）に
    /// EnemyData を適用するための公開メソッド。
    /// aliveCount・SetSpawner は呼ばない（波クリア条件に影響させない）。
    /// </summary>
    public void ApplyEnemyDataExternal(GameObject enemy, EnemyData data)
    {
        if (enemy == null || data == null) return;
        SetLayerRecursively(enemy, LayerMask.NameToLayer("Enemy"));
        ApplyEnemyData(enemy, data);
    }

    /// <summary>
    /// 敵が破壊された時に呼ばれる（EnemyStatsから呼ばれる）
    /// </summary>
    public void OnEnemyDestroyed()
    {
        aliveCount--;
        if (aliveCount < 0) aliveCount = 0;

        // 最終ステージで最後の敵が倒された瞬間に通知（ボスDestroy前）。
        // Area10はFinal Stageのプレースホルダーを末尾に追加しているため、実際のボスが全滅する
        // Stageのインデックスがwaveステージ配列の最後（waveStages.Length-1）と一致しない。
        // finalBossStageIndexOverrideが設定されていればそちらを使う。
        int finalStageIndexForDefeatCheck = (areaConfig != null && areaConfig.finalBossStageIndexOverride >= 0)
            ? areaConfig.finalBossStageIndexOverride
            : waveStages.Length - 1;

        // ★既存Area1〜9は「Stage3でaliveCount==0」＝唯一のボスが倒された瞬間＝真の最終ボスと必ず一致する。
        //   Area10ボスラッシュはStage3だけでボス7/8/9の3体がいるため、この条件だけだと3体とも
        //   「最終ボス撃破」扱いになってしまい、9体目（本当の最終ボス）以外でも
        //   タイマー停止・プレイヤー無敵化・床の保護が誤発動してしまう不具合があった。
        bool isBossRushBossKill = IsBossRushArea && aliveCount == 0;
        bool isNormalFinalBossKill = !IsBossRushArea && aliveCount == 0 && currentStageIndex == finalStageIndexForDefeatCheck;

        // ボス撃破の見た目の演出（スローモーション等）は、Area10ボスラッシュなら9体全て、
        // 既存Areaなら唯一の最終ボスの時だけ発火する
        if (isBossRushBossKill || isNormalFinalBossKill)
            OnBossDefeatedEffect?.Invoke();

        // 真に「このAreaの最後のボス」が倒された時だけ発火する（タイマー停止・無敵化等の一回限りの副作用用）。
        // Area10ボスラッシュはStage3かつ残りFormationが無い（＝9体目）時のみ該当する
        bool isTrueRunFinalBoss = isNormalFinalBossKill
            || (isBossRushBossKill && currentStageIndex == finalStageIndexForDefeatCheck && !HasNextFormation());
        if (isTrueRunFinalBoss)
            OnFinalBossDefeated?.Invoke();

        // 敵撃破数をカウント（スキルシステム用）
        if (currentStageIndex >= 0 && currentStageIndex < enemyKillsPerStage.Length)
        {
            enemyKillsPerStage[currentStageIndex]++;
            if (showDebugLog)
                Debug.Log($"[EnemySpawner] OnEnemyDestroyed() - Stage {currentStageIndex + 1} kill count: {enemyKillsPerStage[currentStageIndex]} (aliveCount: {aliveCount})");
        }
    }

    private void SetLayerRecursively(GameObject obj, int layer)
    {
        if (layer == -1)
        {
            Debug.LogError($"[EnemySpawner] Layer 'Enemy' NOT FOUND! Create it in: Edit > Project Settings > Tags and Layers");
            return;
        }
        obj.layer = layer;
        foreach (Transform child in obj.transform)
        {
            SetLayerRecursively(child.gameObject, layer);
        }
    }

    private void ApplyEnemyData(GameObject enemy, EnemyData data)
    {
        // Visual
        SpriteRenderer sr = enemy.GetComponent<SpriteRenderer>();
        if (sr != null && data.sprite != null) sr.sprite = data.sprite;
        enemy.transform.localScale = new Vector3(data.spriteScale.x, data.spriteScale.y, 1f);

        // HP
        EnemyStats stats = enemy.GetComponent<EnemyStats>();
        if (stats == null) stats = enemy.AddComponent<EnemyStats>();
        int resolvedHp = data.maxHp;
        if (areaConfig != null && data.areaHpOverrides != null)
        {
            int areaIdx = areaConfig.areaNumber - 1;
            if (areaIdx >= 0 && areaIdx < data.areaHpOverrides.Length && data.areaHpOverrides[areaIdx] > 0)
                resolvedHp = data.areaHpOverrides[areaIdx];
        }
        stats.ApplyMaxHp(resolvedHp);
        stats.ApplyGoldReward(data.goldReward);

        // Move
        EnemyMover mover = enemy.GetComponent<EnemyMover>();
        if (mover != null)
        {
            // ★追加：EnemyData を Mover に渡す（moveTypes に使用）
            mover.SetEnemyData(data);
            // フォールバック：従来の設定も適用（moveTypesが空の場合に使用）
            mover.ApplyMove(data.moveSpeed, data.moveRange);
        }

        // Shoot
        EnemyShooter shooter = enemy.GetComponent<EnemyShooter>();
        if (shooter != null)
        {
            shooter.SetProjectileRoot(projectileRoot);
            shooter.SetBulletPrefab(enemyBulletPrefab);
            shooter.SetBeamBulletPrefab(enemyBeamBulletPrefab);

            // ★追加：EnemyData を Shooter に渡す（bulletTypes / 選択モード等に使用）
            shooter.SetEnemyData(data);

            shooter.ApplyShoot(data.fireInterval, data.fireDirection, data.bulletSpeed, data.bulletLifeTime);

            // ★追加：発射SE / VFX / 弾Sprite
            shooter.ApplyFireFx(data.fireSE, data.fireSEVolume, data.fireVfxPrefab, data.bulletSpriteOverride);
        }

        // 子オブジェクトのEnemyShooter初期化（IronNestのNM01/02/03など複数砲台を持つボス向け）
        foreach (var childShooter in enemy.GetComponentsInChildren<EnemyShooter>(true))
        {
            if (childShooter == shooter) continue;
            childShooter.SetProjectileRoot(projectileRoot);
            childShooter.SetBulletPrefab(enemyBulletPrefab);
            childShooter.SetBeamBulletPrefab(enemyBeamBulletPrefab);
            var childData = childShooter.GetEnemyData();
            if (childData != null)
            {
                childShooter.SetEnemyData(childData);
                childShooter.ApplyFireFx(childData.fireSE, childData.fireSEVolume, childData.fireVfxPrefab, childData.bulletSpriteOverride);
            }
        }

        // Death VFX Override
        stats.SetDeathEffectPrefab(data.deathEffectPrefabOverride);
        stats.ApplyDeathVfxConfig(data.useCustomDeathVfx, data.deathVfxConfig);

        // Shield
        EnemyShield shield = enemy.GetComponent<EnemyShield>();
        if (shield != null)
        {
            shield.ApplyShieldData(data);

            // ★シールド破壊イベントをSkillManagerにサブスクライブ
            if (Game.Skills.SkillManager.Instance != null)
            {
                Game.Skills.SkillManager.Instance.SubscribeToEnemyShield(shield);
            }
        }
    }

    public void NotifyEnemyDead()
    {
        aliveCount--;
        if (aliveCount < 0) aliveCount = 0;

        // 敵撃破数のカウントはOnEnemyDestroyed()で行うため、ここでは行わない
        // （両方で行うと2重カウントになってしまう）
    }

    // =========================================================
    // Public Methods for WaveTimerUI
    // =========================================================

    /// <summary>
    /// 現在の段階の残り時間を取得
    /// </summary>
    public float GetStageRemainingTime()
    {
        return stageRemainingTime;
    }

    /// <summary>
    /// 現在の段階の制限時間を取得
    /// </summary>
    public float GetCurrentStageTimeLimit()
    {
        if (currentStage == null) return 0f;
        return currentStage.timeLimit;
    }

    /// <summary>
    /// 現在の段階インデックスを取得（0始まり）
    /// </summary>
    public int GetCurrentStageIndex()
    {
        return currentStageIndex;
    }

    /// <summary>
    /// 総段階数を取得
    /// </summary>
    public int GetTotalStageCount()
    {
        if (waveStages == null) return 0;
        return waveStages.Length;
    }

    /// <summary>
    /// 現在の配置パターン名を取得（デバッグ用）
    /// </summary>
    public string GetCurrentFormationName()
    {
        // 最後に使用されたフォーメーションの名前を返す
        if (currentStage == null)
        {
            return "";
        }

        if (currentStage.formations == null)
        {
            return "";
        }

        if (usedFormationIndices.Count == 0)
        {
            return "";
        }

        int lastUsedIndex = usedFormationIndices[usedFormationIndices.Count - 1];
        if (lastUsedIndex >= 0 && lastUsedIndex < currentStage.formations.Length)
        {
            string formationName = currentStage.formations[lastUsedIndex].formationName;
            return formationName;
        }

        return "";
    }

    // =========================================================
    // Public Methods for Skill System
    // =========================================================

    /// <summary>
    /// 指定ステージの敵撃破数を取得
    /// </summary>
    /// <param name="stageIndex">ステージインデックス（0=Stage1, 1=Stage2, 2=Stage3）</param>
    public int GetEnemyKillCount(int stageIndex)
    {
        if (stageIndex < 0 || stageIndex >= enemyKillsPerStage.Length)
            return 0;
        return enemyKillsPerStage[stageIndex];
    }

    /// <summary>
    /// 指定ステージの敵撃破数をリセット
    /// </summary>
    /// <param name="stageIndex">ステージインデックス（0=Stage1, 1=Stage2, 2=Stage3）</param>
    public void ResetEnemyKillCount(int stageIndex)
    {
        if (stageIndex >= 0 && stageIndex < enemyKillsPerStage.Length)
        {
            enemyKillsPerStage[stageIndex] = 0;
        }
    }

    /// <summary>
    /// 全ステージの敵撃破数をリセット
    /// </summary>
    public void ResetAllEnemyKillCounts()
    {
        for (int i = 0; i < enemyKillsPerStage.Length; i++)
        {
            enemyKillsPerStage[i] = 0;
        }
    }

    /// <summary>
    /// シーン開始時にフェードイン
    /// </summary>
    private System.Collections.IEnumerator FadeInOnStart()
    {
        Debug.Log("[EnemySpawner] Starting fade in");

        // フェード用の黒い画像を作成
        GameObject fadeObj = new GameObject("FadeIn");
        Canvas fadeCanvas = fadeObj.AddComponent<Canvas>();
        fadeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        fadeCanvas.sortingOrder = 9999; // 最前面に表示

        UnityEngine.UI.CanvasScaler scaler = fadeObj.AddComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        GameObject imageObj = new GameObject("FadeImage");
        imageObj.transform.SetParent(fadeObj.transform, false);

        UnityEngine.UI.Image fadeImage = imageObj.AddComponent<UnityEngine.UI.Image>();
        fadeImage.color = new Color(0, 0, 0, 1); // 黒、完全不透明から開始

        RectTransform rectTransform = imageObj.GetComponent<RectTransform>();
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.sizeDelta = Vector2.zero;

        // フェードイン処理（0.5秒）
        float duration = 0.5f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float alpha = 1f - Mathf.Clamp01(elapsed / duration); // 1から0へ
            fadeImage.color = new Color(0, 0, 0, alpha);
            yield return null;
        }

        // 完全に透明になったらフェードオブジェクトを削除
        Destroy(fadeObj);
    }

    /// <summary>
    /// ZPattern移動用：指定インデックスのSpawnPointのTransformを返す。範囲外はnull。
    /// </summary>
    public Transform GetSpawnPoint(int index)
    {
        if (spawnPoints == null || index < 0 || index >= spawnPoints.Length) return null;
        return spawnPoints[index];
    }
}
