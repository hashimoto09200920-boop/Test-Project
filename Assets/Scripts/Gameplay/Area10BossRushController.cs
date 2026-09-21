using System.Collections;
using UnityEngine;

/// <summary>
/// Area10（最終Area・ボスラッシュ）専用の演出オーケストレーター。
/// Stage1〜3、計9体の既存ボス（Area1〜9のボスを再利用）が1体ずつ切り替わるたびに、
/// BGM・背景・アイテムブロックのフェード演出をまとめて行う。
/// EnemySpawnerからのみ参照される（Area1〜9のEnemySpawnerではこのフィールドをnullのままにしておくこと。
/// nullなら何も呼ばれないため既存Areaの挙動には一切影響しない）。
/// </summary>
public class Area10BossRushController : MonoBehaviour
{
    [System.Serializable]
    public class BossRushEntry
    {
        [Tooltip("Inspector視認用のラベル（例：Stage1-Boss1 (Area1: GravePole)）")]
        public string label;

        [Tooltip("BGM/背景/ブロック画像の参照先Area番号（1〜9）")]
        public int sourceAreaNumber;

        [Tooltip("参照先AreaのAreaConfig（BGM/背景/ブロック画像の取得元。Assets/Data/AreaConfigs/AreaXConfig.assetを設定する）")]
        public AreaConfig sourceAreaConfig;

        [Tooltip("このボスの戦闘中だけ有効化するシーン上のGameObject（例：Area4ボスのランタン等、ボス固有の専用ギミック）。" +
                 "このボスに切り替わった時にSetActive(true)、次のボスに切り替わる時にSetActive(false)される")]
        public GameObject[] extraObjectsToActivate;

        [Tooltip("Area9ボス専用：Area09MoonController（月・地球演出）。このボスの枠にだけ設定する。他のボスは空欄のままでよい")]
        public Area09MoonController moonControllerOverride;
    }

    [Header("参照")]
    [SerializeField] private EnemySpawner enemySpawner;
    [SerializeField] private GameplayBgmRandomPlayer bgmPlayer;
    [SerializeField] private StageBlockSpawner blockSpawner;
    [Tooltip("デバッグでStage2/3のボスから開始する時、プレイヤーキャラクター（PixelDancer）を" +
             "有効化するために使う（通常はStageIntroController.OnCutInComplete()がStage1でのみ行う処理）")]
    [SerializeField] private StageIntroController stageIntroController;

    [Header("ボス構成（Stage1〜3、各3体、計9体。並び順がそのまま進行順になる）")]
    [SerializeField] private BossRushEntry[] bossEntries = new BossRushEntry[9];

    [Header("タイミング設定（Play前Inspectorで調整可能）")]
    [Tooltip("BGMフェードアウト時間（秒）")]
    [SerializeField] private float bgmFadeOutDuration = 1.0f;

    [Tooltip("現在のアイテムブロックのフェードアウト時間（秒）")]
    [SerializeField] private float blockFadeOutDuration = 0.5f;

    [Tooltip("Final Stage後半フェーズBGM（30_Area10_B）へのフェード時間（秒）")]
    [SerializeField] private float finalStagePhase2BgmFadeDuration = 1.0f;

    [Tooltip("次のボスが出現する際のフェードイン時間（秒）")]
    [SerializeField] private float nextBossFadeInDuration = 2.0f;

    /// <summary>
    /// デバッグ開始ボスIndex。毎回このControllerのInspectorを開くのは手間なため、
    /// Area10Config.asset側のフィールド（AreaConfig.debugStartBossIndex）をそのまま参照する。
    /// EnemySpawner側からも同じ値を参照するための公開アクセサ。
    /// </summary>
    public int DebugStartBossIndex =>
        (enemySpawner != null && enemySpawner.CurrentAreaConfig != null) ? enemySpawner.CurrentAreaConfig.debugStartBossIndex : -1;

    /// <summary>bossEntriesの総数（EnemySpawner側の範囲チェック用）。</summary>
    public int BossEntryCount => bossEntries != null ? bossEntries.Length : 0;

    private int globalBossIndex = -1;

    /// <summary>
    /// このGameObject自体はシーン常駐のため、Awake()はArea1〜9をプレイした時も必ず呼ばれる。
    /// GameSession.SelectedAreaが実際にボスラッシュ用（skillSelectionCountOverride>0＝Area10）かを
    /// 必ず確認すること。確認せずGameSession.IsBossRushActive=trueを無条件に立てると、
    /// Area1〜9でも背景演出（鳥・雨・霧等）が誤って無効化されてしまう（実際に発生した不具合）。
    /// </summary>
    private bool IsThisAreaBossRush =>
        GameSession.SelectedArea != null && GameSession.SelectedArea.skillSelectionCountOverride > 0;

    private void Awake()
    {
        if (IsThisAreaBossRush)
            GameSession.IsBossRushActive = true;
    }

    private void OnDestroy()
    {
        if (!IsThisAreaBossRush) return;

        // Area10終了時に実効Area番号の上書きを残さない（Final Stage等、後続処理への影響を避ける）
        if (GameSession.BossRushEffectiveAreaNumber.HasValue)
            GameSession.BossRushEffectiveAreaNumber = null;
        GameSession.IsBossRushActive = false;
    }

    /// <summary>
    /// Stage1開始時、最初のボス（bossEntries[0]）の背景・BGM・アイテムブロックを
    /// フェード無しで即座に適用する。StageIntroController.PlayIntro()（床・ダンサー・スポットライト演出）が
    /// 始まる前に呼ぶことで、通常のArea1開始時と同じ体験になる（開始直後に背景が真っ暗になる問題の対策）。
    /// 呼び出し後はglobalBossIndex=0として扱われるため、この後PrepareNextBossRoutine()が呼ばれると
    /// 正しくbossEntries[1]（2体目）から進行する。
    /// </summary>
    public void ApplySetupForFirstBossInstant()
    {
        if (bossEntries == null || bossEntries.Length == 0) return;

        // ★デバッグ用：Area10Config.debugStartBossIndexが設定されていれば、ボス1ではなく指定したボスから開始する
        int debugIndex = DebugStartBossIndex;

        // ★debugIndex == bossEntries.Length（＝9体分の枠のさらに1つ先）は、
        //   「Final Stageの直前から開始する」という特別なデバッグ指定として扱う。
        //   参照する次ボスが存在しないため、通常のボス背景ではなくArea10Config自身の
        //   背景（Far/Mid/Silhouette）を即座に適用し、globalBossIndexをbossEntries.Lengthに
        //   進めておく（この後EnemySpawner側がStage4のフォーメーションを正しくスポーンする）。
        if (debugIndex == bossEntries.Length)
        {
            Debug.Log($"[Area10BossRushController] ★FINAL STAGE DEBUG PATH ENTERED★ debugIndex={debugIndex}, bossEntries.Length={bossEntries.Length}");
            globalBossIndex = debugIndex;

            AreaConfig area10ConfigForDebug = enemySpawner != null ? enemySpawner.CurrentAreaConfig : null;
            if (area10ConfigForDebug == null)
            {
                Debug.LogError("[Area10BossRushController] FINAL STAGE DEBUG: area10ConfigForDebug is NULL, aborting background setup!");
                return;
            }
            Debug.Log($"[Area10BossRushController] FINAL STAGE DEBUG: using area={area10ConfigForDebug.name}, " +
                      $"backgroundSprite={(area10ConfigForDebug.backgroundSprite != null ? area10ConfigForDebug.backgroundSprite.name : "null")}, " +
                      $"backgroundSpriteScale={area10ConfigForDebug.backgroundSpriteScale}, " +
                      $"farLayerExtraScaleOverride={area10ConfigForDebug.farLayerExtraScaleOverride}, " +
                      $"backgroundFogScale={area10ConfigForDebug.backgroundFogScale}");

            GameSession.BossRushEffectiveAreaNumber = area10ConfigForDebug.areaNumber;

            if (BackgroundManager.Instance != null)
            {
                BackgroundManager.Instance.ApplyAreaInstant(area10ConfigForDebug);
                BackgroundManager.Instance.ActivateAreaParticle();
            }
            else
            {
                Debug.LogError("[Area10BossRushController] FINAL STAGE DEBUG: BackgroundManager.Instance is NULL!");
            }

            if (stageIntroController != null)
                StartCoroutine(ActivatePixelDancerAfterAllStart());

            if (bgmPlayer != null)
                bgmPlayer.FadeOutAndSwitchToAreaClipIndex(10, 0, 0f);

            return;
        }

        int startIndex = (debugIndex >= 0 && debugIndex < bossEntries.Length) ? debugIndex : 0;
        globalBossIndex = startIndex;

        BossRushEntry first = bossEntries[startIndex];
        if (first == null || first.sourceAreaConfig == null) return;

        // CrowFlybySpawner等の背景演出、EnemyShooter/EnemyBeamBulletの貫通力補正が
        // 正しく「Area1として」動作するよう、実効Area番号を上書きする
        GameSession.BossRushEffectiveAreaNumber = first.sourceAreaNumber;

        if (BackgroundManager.Instance != null)
        {
            BackgroundManager.Instance.ApplyAreaInstant(first.sourceAreaConfig);

            // ★BackgroundManagerはsuppressParticleOnStart=trueの間、Areaパーティクルを生成はするが
            //   非表示のままにする仕様（通常はStageIntroController.PlayIntro()冒頭のActivateAreaParticle()で
            //   表示する）。PlayIntro()はStage1（currentStageIndex==0）専用のため、デバッグで
            //   Stage2/3のボス（startIndex>=3）から開始した場合は誰も呼ばずパーティクルが
            //   非表示のままになってしまう。ここで直接表示する
            //   （Stage1から開始する場合はPlayIntro()側が表示するので、ここでは呼ばない）。
            if (startIndex >= 3)
                BackgroundManager.Instance.ActivateAreaParticle();
        }

        // ★プレイヤーキャラクター（PixelDancer）はStageIntroController.Start()内の
        //   SetupInitialState()で非表示にされており、通常はOnCutInComplete()（Stage1専用、
        //   カットイン完了時にEnemySpawnerから呼ばれる）で初めて有効化される。
        //   StageIntroControllerは[DefaultExecutionOrder(100)]によりEnemySpawner.Start()
        //   （＝このApplySetupForFirstBossInstant()の呼び出し元）より後にStart()が実行されるため、
        //   ここで即座にOnCutInComplete()を呼んでも、直後に走るSetupInitialState()で
        //   再度非表示に戻されてしまう（実際に発生した不具合）。1フレーム待ってから呼ぶことで、
        //   全てのStart()完了後に確実に実行されるようにする
        //   （StageIntroController.PlayIntro()冒頭の「yield return null; // 全Start()完了を保証」と同じ対策）。
        if (startIndex >= 3 && stageIntroController != null)
            StartCoroutine(ActivatePixelDancerAfterAllStart());

        // ★ここでは曲リストの準備だけ行い、再生はしない。
        //   実際の再生はStageIntroController.PlayIntro()側の既存のPlayRandom()呼び出し
        //   （スポットライトが点いてダンサーが見えるタイミング）に任せる。
        if (bgmPlayer != null)
        {
            bgmPlayer.SwitchToPickedClipWithoutPlaying(first.sourceAreaNumber);

            // ★9体分のBGMを開始直後にまとめて先読みしておく。ただし3曲全部ではなく、
            //   各エリアにつきランダムに選んだ1曲だけを先読みする（このボスが倒されるまでは
            //   選ばれたその1曲だけを流し続ける仕様のため、残り2曲は最初から不要）。
            //   3曲×9体＝27曲ではなく1曲×9体＝9曲になるため、負荷が1/3で済む。
            if (bossEntries != null)
            {
                foreach (var entry in bossEntries)
                    if (entry != null)
                        bgmPlayer.PreloadPickedClipForArea(entry.sourceAreaNumber);
            }

            // ★StageIntroController.PlayIntro()（BGM再生のトリガー）はStage1（currentStageIndex==0）
            //   専用で、デバッグでStage2/3のボス（startIndex>=3）から開始した場合は呼ばれない。
            //   その場合誰も再生を開始しないままになってしまうため、ここで直接再生する
            //   （Stage1から開始する場合はPlayIntro()側が再生するので、二重再生を避けてここでは呼ばない）。
            if (startIndex >= 3)
                bgmPlayer.PlayRandom();
        }

        if (blockSpawner != null)
        {
            StageBlockConfig sourceBlockConfig = first.sourceAreaConfig.stageBlockConfig;
            AreaConfig area10Config = enemySpawner != null ? enemySpawner.CurrentAreaConfig : null;

            if (sourceBlockConfig != null && area10Config != null && area10Config.stageBlockConfig != null
                && area10Config.stageBlockConfig.blockCountPerStage != null && area10Config.stageBlockConfig.blockCountPerStage.Length > 0)
            {
                // ★デバッグ開始位置に応じた正しいStage分のブロック数設定を使う（常にStage1用[0]を使うと、
                //   デバッグでボス4以降から開始した時にStage1のブロック数のままになってしまう）
                int stageIndexForBlocks = Mathf.Clamp(startIndex / 3, 0, area10Config.stageBlockConfig.blockCountPerStage.Length - 1);
                BlockCountRange countRange = area10Config.stageBlockConfig.blockCountPerStage[stageIndexForBlocks];
                Sprite[] sprites = { sourceBlockConfig.blockSprite, sourceBlockConfig.blockSprite2, sourceBlockConfig.blockSprite3, sourceBlockConfig.blockSprite4 };
                blockSpawner.SpawnWaveForBoss(countRange, sprites, sourceBlockConfig.blockHp);
            }
        }

        SetExtraObjectsActive(first, true);
    }

    /// <summary>
    /// StageIntroController.Start()（[DefaultExecutionOrder(100)]により全Start()の最後の方で実行される）
    /// が完了するのを1フレーム待ってから、プレイヤーキャラクター（PixelDancer）を有効化する。
    /// デバッグでStage2/3のボスから開始する時専用（詳細はApplySetupForFirstBossInstant()内のコメント参照）。
    /// </summary>
    private IEnumerator ActivatePixelDancerAfterAllStart()
    {
        Debug.Log($"[Area10BossRushController] ActivatePixelDancerAfterAllStart() scheduled at frame {Time.frameCount}, stageIntroController={(stageIntroController != null ? stageIntroController.name : "NULL")}");
        yield return null; // 全Start()完了を保証
        Debug.Log($"[Area10BossRushController] ActivatePixelDancerAfterAllStart() resuming at frame {Time.frameCount}, calling OnCutInComplete()");
        if (stageIntroController != null)
            stageIntroController.OnCutInComplete();
    }

    private static void SetExtraObjectsActive(BossRushEntry entry, bool active)
    {
        if (entry == null) return;

        if (entry.extraObjectsToActivate != null)
            foreach (var go in entry.extraObjectsToActivate)
                if (go != null) go.SetActive(active);

        if (entry.moonControllerOverride != null)
        {
            if (active) entry.moonControllerOverride.ActivateForBossRush();
            else entry.moonControllerOverride.DeactivateForBossRush();
        }
    }

    /// <summary>
    /// Final Stage後半フェーズ専用BGM（30_Area10_B）へ切り替える。
    /// ★Final Stage本編ボスは未実装のため、現時点ではどこからも呼ばれていない。
    ///   本編ボス実装時、HP閾値等の後半フェーズ突入タイミングでこのメソッドを呼ぶこと。
    ///   それまでの動作確認用に、Play中このコンポーネントを右クリック→
    ///   「Debug: Switch to Final Stage Phase2 BGM」で手動実行できる。
    /// </summary>
    [ContextMenu("Debug: Switch to Final Stage Phase2 BGM")]
    public void SwitchToFinalStagePhase2Bgm()
    {
        if (bgmPlayer != null)
            bgmPlayer.FadeOutAndSwitchToAreaClipIndex(10, 1, finalStagePhase2BgmFadeDuration);
    }

    /// <summary>
    /// 次のボスへの切り替え演出（BGMフェードアウト→次BGM、ブロックフェードアウト、
    /// 背景フェードアウト→フェードイン）を行い、完了まで待機する。
    /// EnemySpawnerのSpawnFormation()呼び出し直前から呼ばれる想定。
    /// </summary>
    /// <param name="forceTargetIndex">
    /// 指定した場合、現在のglobalBossIndexに関わらずこのインデックスへ強制的に同期する。
    /// Stage境界（Stage開始時）で使う：時間切れで前Stageの残りボスがスキップされていても、
    /// 新Stageの最初のボスへ正しく合わせるため（例：Stage3開始時は常にboss index 6へ強制）。
    /// 未指定（null）ならStage内の通常の1体ずつの前進として扱う。
    /// </param>
    public IEnumerator PrepareNextBossRoutine(int? forceTargetIndex = null)
    {
        int newIndex = forceTargetIndex ?? (globalBossIndex + 1);

        // ★Final Stageへの遷移（9体のボスラッシュを抜けた直後、newIndexがbossEntries.Lengthと
        //   ちょうど一致するタイミング）は、参照する次ボスが存在しないため、Area10Config自身の
        //   背景（Far/Mid/Silhouette）へクロスフェードする。BGMはFinal Stage専用の固定2曲
        //   （29_Area10_A=前半, 30_Area10_B=後半）のうち前半をここでフェード切替する
        //   （後半への切替はFinal Stage本編ボスの実装時、SwitchToFinalStagePhase2Bgm()を呼ぶ）。
        //   直前のボス（Area9）のアイテムブロックは残ったままだと表示され続けてしまうため、
        //   Final Stageはブロック無し仕様としてフェードアウトのみ行う（新しいブロックの再生成はしない）。
        if (bossEntries != null && newIndex == bossEntries.Length)
        {
            if (bgmPlayer != null)
                bgmPlayer.FadeOutAndSwitchToAreaClipIndex(10, 0, bgmFadeOutDuration);

            if (globalBossIndex >= 0 && globalBossIndex < bossEntries.Length)
                SetExtraObjectsActive(bossEntries[globalBossIndex], false);

            globalBossIndex = newIndex;

            // ★念のための保険：デバッグ開始等でglobalBossIndexの追跡がズレていた場合でも、
            //   Final Stageでは月（Area09MoonController）が絶対に映り込まないよう、
            //   全ボス枠を走査して確実に非アクティブ化する（Area9ボスのextraObjects全般も同様）。
            foreach (var entry in bossEntries)
            {
                if (entry == null) continue;
                if (entry.extraObjectsToActivate != null)
                    foreach (var go in entry.extraObjectsToActivate)
                        if (go != null) go.SetActive(false);
                if (entry.moonControllerOverride != null)
                    entry.moonControllerOverride.DeactivateForBossRush();
            }

            AreaConfig area10Config = enemySpawner != null ? enemySpawner.CurrentAreaConfig : null;
            if (area10Config == null) yield break;

            if (blockSpawner != null)
                blockSpawner.FadeOutCurrentWave(blockFadeOutDuration);
            if (BlockItemManager.Instance != null)
                BlockItemManager.Instance.FadeOutAllItems(blockFadeOutDuration);

            bool finalBackgroundDone = false;
            if (BackgroundManager.Instance != null)
                BackgroundManager.Instance.CrossfadeAllLayersToArea(area10Config, () => finalBackgroundDone = true);
            else
                finalBackgroundDone = true;

            yield return new WaitUntil(() => finalBackgroundDone);
            yield break;
        }

        // ★移動先のインデックスが範囲外（例：Stage3の9体目より先、bossEntriesの想定を超える場合）の
        //   場合は何もせず抜ける。ここでチェックする前に現在のボスの演出（Area09MoonController等）を
        //   非アクティブにしてしまうと、「移動先が無い」と分かった時には既に消してしまった後になり、
        //   月がすぐ消えるような不具合になっていた。
        if (bossEntries == null || newIndex < 0 || newIndex >= bossEntries.Length)
            yield break;

        if (globalBossIndex >= 0 && globalBossIndex < bossEntries.Length)
            SetExtraObjectsActive(bossEntries[globalBossIndex], false);

        globalBossIndex = newIndex;

        BossRushEntry next = bossEntries[globalBossIndex];
        if (next == null || next.sourceAreaConfig == null)
            yield break;

        // 背景演出スクリプト・貫通力補正が次ボスの出身Areaとして正しく動作するよう、実効Area番号を上書きする
        GameSession.BossRushEffectiveAreaNumber = next.sourceAreaNumber;

        // 1. BGMフェードアウト→次ボスのBGM開始（ApplySetupForFirstBossInstant()で
        //    先読み済みの「ランダムに選ばれた1曲」に切り替え、そのボスが倒されるまで流し続ける）
        if (bgmPlayer != null)
            bgmPlayer.FadeOutAndSwitchToPickedClip(next.sourceAreaNumber, bgmFadeOutDuration);

        // 2. 現在のアイテムブロック、およびブロックを壊して出現していた未収集アイテムをフェードアウト
        if (blockSpawner != null)
            blockSpawner.FadeOutCurrentWave(blockFadeOutDuration);
        if (BlockItemManager.Instance != null)
            BlockItemManager.Instance.FadeOutAllItems(blockFadeOutDuration);

        // 3. 背景クロスフェード（フェードアウト→フェードイン）。完了まで待つ
        bool backgroundDone = false;
        if (BackgroundManager.Instance != null)
            BackgroundManager.Instance.CrossfadeAllLayersToArea(next.sourceAreaConfig, () => backgroundDone = true);
        else
            backgroundDone = true;

        yield return new WaitUntil(() => backgroundDone);

        // 4. 背景フェードイン完了と同時に、次ボスのアイテムブロックをフェードインで生成する
        //    （出現数はArea10Config自身のBlockCountPerStage、画像とHPはボスの出身AreaConfigの設定値を使う）
        if (blockSpawner != null)
        {
            StageBlockConfig sourceBlockConfig = next.sourceAreaConfig.stageBlockConfig;
            AreaConfig area10Config = enemySpawner != null ? enemySpawner.CurrentAreaConfig : null;

            if (sourceBlockConfig != null && area10Config != null && area10Config.stageBlockConfig != null
                && area10Config.stageBlockConfig.blockCountPerStage != null)
            {
                int stageIndexForBoss = Mathf.Clamp(globalBossIndex / 3, 0, area10Config.stageBlockConfig.blockCountPerStage.Length - 1);
                BlockCountRange countRange = area10Config.stageBlockConfig.blockCountPerStage[stageIndexForBoss];
                Sprite[] sprites = { sourceBlockConfig.blockSprite, sourceBlockConfig.blockSprite2, sourceBlockConfig.blockSprite3, sourceBlockConfig.blockSprite4 };
                blockSpawner.SpawnWaveForBoss(countRange, sprites, sourceBlockConfig.blockHp);
            }
        }

        SetExtraObjectsActive(next, true);
    }

    /// <summary>
    /// SpawnFormation()直後、スポーンされたばかりのボスのフェードイン秒数を上書きする
    /// （共有Prefabのシリアライズ値自体は変更しないので、他Areaでの見た目には影響しない）。
    /// </summary>
    public void ApplyFadeInOverrideToNewSpawns(Transform enemyRoot)
    {
        if (enemyRoot == null) return;
        foreach (var stats in enemyRoot.GetComponentsInChildren<EnemyStats>())
            stats.SetFadeInDuration(nextBossFadeInDuration);
    }
}
