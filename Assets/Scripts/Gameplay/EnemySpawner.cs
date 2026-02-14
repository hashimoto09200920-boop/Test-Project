using System.Collections;
using UnityEngine;
using Game.Progress;

public class EnemySpawner : MonoBehaviour
{
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
    }

    [Header("Prefab / Parent")]
    [SerializeField] private GameObject enemyPrefab;
    [SerializeField] private Transform enemyRoot;

    [Header("Shooting Wiring")]
    [SerializeField] private Transform projectileRoot;
    [SerializeField] private EnemyBullet enemyBulletPrefab;

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
    [Tooltip("Formation切り替え時の待機時間（秒）\n敵を全て倒してから次のFormationが出るまでの時間")]
    [SerializeField] private float formationTransitionDelay = 2f;

    [Header("Wave Debug")]
    [Tooltip("デバッグモード: 特定の段階から開始できます")]
    [SerializeField] private bool debugMode = false;

    [Tooltip("デバッグ用開始段階（0始まり）\n0=Stage 1, 1=Stage 2, 2=Stage 3")]
    [SerializeField] private int debugStartStage = 0;

    [Header("UI References")]
    [Tooltip("ステージクリアメッセージを表示するUI")]
    [SerializeField] private StageClearUI stageClearUI;

    [Tooltip("全ステージクリア時のリザルト画面UI")]
    [SerializeField] private GameResultUI gameResultUI;

    [Tooltip("スキル選択UI")]
    [SerializeField] private Game.UI.SkillSelectionUI skillSelectionUI;

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

    // =========================================================
    // Enemy Kill Tracking (for Skill System)
    // =========================================================
    private int[] enemyKillsPerStage = new int[3]; // Stage 0, 1, 2 (= Stage 1, 2, 3)

    private void Start()
    {
        // 敵撃破数を初期化（念のため明示的に0にする）
        for (int i = 0; i < enemyKillsPerStage.Length; i++)
        {
            enemyKillsPerStage[i] = 0;
        }
        Debug.Log($"[EnemySpawner] Enemy kill counts initialized: [{enemyKillsPerStage[0]}, {enemyKillsPerStage[1]}, {enemyKillsPerStage[2]}]");

        // シーン開始時にフェードイン
        StartCoroutine(FadeInOnStart());

        // Area Config または GameSession からの設定読み込み
        LoadAreaConfiguration();

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

        currentStage = waveStages[currentStageIndex];
        stageRemainingTime = currentStage.timeLimit;
        usedFormationIndices.Clear();
        stageClearFlag = false;
    }

    private IEnumerator SpawnRoutine()
    {
        yield return new WaitForSeconds(startDelay);

        if (useWaveSystem)
        {
            // ウェーブシステムのルーチン
            yield return StartCoroutine(WaveSystemRoutine());
        }
        else
        {
            // レガシーシステムのルーチン
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
        while (currentStageIndex < waveStages.Length)
        {
            currentStage = waveStages[currentStageIndex];
            stageRemainingTime = currentStage.timeLimit;
            usedFormationIndices.Clear();
            stageClearFlag = false;

            // 最初の配置パターンをスポーン
            SpawnFormation();

            // 段階クリアまでループ
            while (!stageClearFlag)
            {
                // タイマー更新（時間制限がある場合）
                // ゲームオーバー進行中はタイマーを停止
                bool isGameOverInProgress = GameManager.Instance != null && GameManager.Instance.IsGameOverInProgress();

                if (currentStage.timeLimit > 0 && !isGameOverInProgress)
                {
                    stageRemainingTime -= Time.deltaTime;

                    // 時間切れチェック
                    if (stageRemainingTime <= 0)
                    {
                        if (currentStage.clearOnTimeExpired)
                        {
                            // 時間経過でクリア
                            ClearRemainingEnemies(isKilled: false);  // 時間経過による消滅
                            ClearAllBullets();  // 画面上の弾を全て削除
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
                    // Formation切り替え前に待機
                    yield return new WaitForSeconds(formationTransitionDelay);

                    bool hasMoreFormations = SpawnFormation();

                    // Formation切り替え時のスキル選択（Stage 1と2のみ）
                    if ((currentStageIndex == 0 || currentStageIndex == 1) && hasMoreFormations && skillSelectionUI != null)
                    {
                        Debug.Log($"[EnemySpawner] Formation switched. Starting skill selection: Category=All, StageIndex={currentStageIndex}");

                        // スキル選択開始（1回のみ、全スキルから選択、StageIndexを渡す）
                        bool skillSelectionComplete = false;
                        skillSelectionUI.StartSkillSelection(Game.Skills.SkillCategory.All, 1, () =>
                        {
                            skillSelectionComplete = true;
                        }, currentStageIndex);

                        // スキル選択完了まで待機
                        yield return new WaitUntil(() => skillSelectionComplete);
                    }

                    // 配置パターンがなく、時間制限もない（または clearOnTimeExpired=false）場合はクリア
                    if (!hasMoreFormations)
                    {
                        if (currentStage.timeLimit <= 0 || !currentStage.clearOnTimeExpired)
                        {
                            ClearAllBullets();  // 画面上の弾を全て削除
                            stageClearFlag = true;
                        }
                    }
                }

                yield return null;
            }

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
                // 次の段階への準備時間（3秒待機）
                yield return new WaitForSeconds(3f);
            }
        }

        // 全ステージクリアメッセージ表示
        if (stageClearUI != null)
        {
            stageClearUI.ShowAllStagesClear();
        }

        // メッセージ表示時間を待ってからリザルト画面を表示
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

        // リザルト画面を表示（リトライボタン付き）
        if (gameResultUI != null)
        {
            gameResultUI.ShowAllClearResult();
        }
    }

    /// <summary>
    /// 配置パターンから敵をスポーンする
    /// </summary>
    /// <returns>スポーン成功したらtrue、パターンがなければfalse</returns>
    private bool SpawnFormation()
    {
        if (currentStage == null || currentStage.formations == null || currentStage.formations.Length == 0)
        {
            Debug.LogWarning($"[EnemySpawner] SpawnFormation() failed: currentStage={currentStage != null}, formations={(currentStage != null && currentStage.formations != null ? currentStage.formations.Length.ToString() : "null")}");
            return false;
        }

        // 未使用の配置パターンを選択
        EnemyFormation selectedFormation = PickUnusedFormation();
        if (selectedFormation == null)
        {
            Debug.LogWarning("[EnemySpawner] SpawnFormation() failed: No unused formations available");
            return false;
        }

        Debug.Log($"[EnemySpawner] SpawnFormation() spawning: {selectedFormation.formationName}, entries={selectedFormation.entries?.Length ?? 0}");

        // 配置パターンのエントリーごとに敵をスポーン
        if (selectedFormation.entries != null)
        {
            int spawnedCount = 0;
            foreach (var entry in selectedFormation.entries)
            {
                if (entry == null || entry.enemyData == null) continue;

                // Spawn Point Indexの検証
                if (spawnPoints == null || entry.spawnPointIndex < 0 || entry.spawnPointIndex >= spawnPoints.Length)
                {
                    Debug.LogWarning($"[EnemySpawner] Invalid spawn point index {entry.spawnPointIndex} in formation: {selectedFormation.formationName} (Available: 0-{spawnPoints?.Length - 1 ?? -1})");
                    continue;
                }

                Transform spawnTransform = spawnPoints[entry.spawnPointIndex];
                if (spawnTransform == null)
                {
                    Debug.LogWarning($"[EnemySpawner] Spawn Point at index {entry.spawnPointIndex} is null in formation: {selectedFormation.formationName}");
                    continue;
                }

                SpawnAtWithData(spawnTransform, entry.enemyData);
                spawnedCount++;
            }
            Debug.Log($"[EnemySpawner] SpawnFormation() completed: spawned {spawnedCount} enemies");
        }

        return true;
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
            // Stage 1と2のみリセットして再利用（Stage 3は時間制限が無いためリセットしない）
            if (currentStageIndex == 0 || currentStageIndex == 1)
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

        // ランダムに選択
        int randomIndex = availableIndices[Random.Range(0, availableIndices.Count)];
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
    /// 指定されたEnemyDataで敵をスポーン（ウェーブシステム用）
    /// </summary>
    private void SpawnAtWithData(Transform spawnPoint, EnemyData data)
    {
        if (spawnPoint == null || data == null) return;

        GameObject enemy = Instantiate(enemyPrefab, spawnPoint.position, Quaternion.identity, enemyRoot);
        enemy.name = $"Enemy_{Time.frameCount}_{data.name}";

        // ★敵と全ての子オブジェクトを Enemy Layer に設定
        SetLayerRecursively(enemy, LayerMask.NameToLayer("Enemy"));

        ApplyEnemyData(enemy, data);

        // EnemyStatsにSpawnerへの参照を設定
        EnemyStats stats = enemy.GetComponent<EnemyStats>();
        if (stats != null)
        {
            stats.SetSpawner(this);
        }

        aliveCount++;
    }

    /// <summary>
    /// 敵が破壊された時に呼ばれる（EnemyStatsから呼ばれる）
    /// </summary>
    public void OnEnemyDestroyed()
    {
        aliveCount--;
        if (aliveCount < 0) aliveCount = 0;

        // 敵撃破数をカウント（スキルシステム用）
        if (currentStageIndex >= 0 && currentStageIndex < enemyKillsPerStage.Length)
        {
            enemyKillsPerStage[currentStageIndex]++;
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
        stats.ApplyMaxHp(data.maxHp);

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

            // ★追加：EnemyData を Shooter に渡す（bulletTypes / 選択モード等に使用）
            shooter.SetEnemyData(data);

            shooter.ApplyShoot(data.fireInterval, data.fireDirection, data.bulletSpeed, data.bulletLifeTime);

            // ★追加：発射SE / VFX / 弾Sprite
            shooter.ApplyFireFx(data.fireSE, data.fireSEVolume, data.fireVfxPrefab, data.bulletSpriteOverride);
        }

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
}
