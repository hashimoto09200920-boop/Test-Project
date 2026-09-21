using UnityEngine;

/// <summary>
/// GameSessionのAreaConfigからFarLayer・MidLayerスプライトとカメラ背景色を適用する。
/// Editor直接再生時はfallback設定を使用。
/// </summary>
[ExecuteAlways]
public class BackgroundManager : MonoBehaviour
{
    public static BackgroundManager Instance { get; private set; }

    [Header("Far Layer")]
    [SerializeField] private SpriteRenderer farLayer;
    [SerializeField] private FarLayerFade farLayerFade;
    [Tooltip("Editor直接Play時のフォールバック")]
    [SerializeField] private Sprite fallbackFarSprite;

    [Header("Mid Layer")]
    [SerializeField] private SpriteRenderer midLayer;
    [Tooltip("Editor直接Play時のフォールバック")]
    [SerializeField] private Sprite fallbackFogSprite;
    [Tooltip("AreaConfig.midLayerHideOnStage3がONの場合、Stage3切り替え時のMidLayerフェードアウト所要時間（秒）")]
    [SerializeField] private float midLayerFadeOutDuration = 1.0f;

    [Header("Extra Fog Slots")]
    [Tooltip("シーンに事前配置したExtraFogのSpriteRenderer（最大数分用意）")]
    [SerializeField] private SpriteRenderer[] extraFogSlots;

    [Header("Silhouette Layer")]
    [SerializeField] private SpriteRenderer silhouetteLayer;
    [Tooltip("Editor直接Play時のフォールバック")]
    [SerializeField] private Sprite fallbackSilhouetteSprite;

    [Header("Earth Layers (Area09 Cosmos専用)")]
    [Tooltip("Area09選択時のみ表示し、他Areaでは自動的に非表示にする")]
    [SerializeField] private GameObject earthMask;
    [SerializeField] private GameObject earthSurface;
    [SerializeField] private GameObject earthRimGlow;
    [SerializeField] private GameObject aurora;
    [SerializeField] private GameObject meteorEffect;

    [Header("Camera")]
    [SerializeField] private Camera targetCamera;

    [Header("Area10 Boss Rush (クロスフェード専用)")]
    [Tooltip("Area10ボスラッシュのボス切替時、MidLayerのフェード合計時間（秒）。FarLayer/SilhouetteはFarLayerFade/SilhouetteFade側の設定を使う")]
    [SerializeField] private float midLayerCrossfadeDuration = 1.0f;

    private Sprite farSpriteB;
    private Vector3 farScaleB;
    private Vector3 farPositionB;
    private Sprite silhouetteSpriteB;
    private Vector3 silhouetteScaleB;
    private Vector3 silhouettePositionB;
    private SilhouetteFade silhouetteFade;
    private CloudCycleFade silhouetteCycleFade;
    private CloudCycleFade midLayerCycleFade;
    private bool midLayerHideOnStage3Enabled;
    private Sprite[] silhouetteCyclePatterns;
    private Vector3[] silhouetteCycleOffsets;
    private float silhouetteCycleHoldDuration;
    private float silhouetteCycleFadeDuration;
    private float silhouetteCycleInitialFadeDuration;
    private float silhouetteCycleMaxAlpha = 1f;
    private bool silhouetteCyclePingPongDriftCached = false;
    private float silhouetteCycleDriftAmplitudeCached = 0.3f;
    private float silhouetteCycleDriftSpeedCached = -1f;
    private TimeOfDayFade farLayerCycleFade;
    private Sprite[] farLayerCyclePatterns;
    private float[] farLayerCycleHoldDurations;
    private float farLayerCycleFadeDuration;
    private float farLayerExtraScaleOverrideCached = 1f;
    // ★フェード完了を待ってから巡回を開始する予約コルーチン（StartFarLayerCycleAfterFade）。
    //   次のボスへ切り替わってApplyArea()がfarLayerCycleFade.StopCycle()を呼んだ後に
    //   この予約が遅れて実行されると、既に退場したはずの前のボスの巡回パターンが
    //   後から開始されてしまう（Area10ボスラッシュでボスを素早く倒した時に発生しうる）。
    //   ApplyArea()側で確実にキャンセルできるよう参照を保持しておく。
    private Coroutine pendingFarLayerCycleCoroutine;

    [Header("Preview (Editor Only)")]
    [Tooltip("SceneView確認用AreaConfig")]
    [SerializeField] private AreaConfig previewAreaConfig;
    [Tooltip("A=Stage1/2、B=Stage3のプレビュー切り替え")]
    [SerializeField] private bool previewStageB = false;
    [Tooltip("Stage3プレビュー中、Silhouette Cycle Patternsの何番目を表示するか（0始まり）。位置・サイズ確認用")]
    [SerializeField] private int previewCyclePatternIndex = 0;

#if UNITY_EDITOR
    private void Update()
    {
        if (Application.isPlaying) return;
        if (previewAreaConfig == null) return;

        if (silhouetteLayer != null)
        {
            if (previewStageB)
            {
                var cyclePatterns = previewAreaConfig.silhouetteCyclePatterns;
                var cycleOffsets = previewAreaConfig.silhouetteCycleOffsets;
                Vector3 offset = Vector3.zero;
                if (cyclePatterns != null && cyclePatterns.Length > 0)
                {
                    int idx = Mathf.Clamp(previewCyclePatternIndex, 0, cyclePatterns.Length - 1);
                    silhouetteLayer.sprite = cyclePatterns[idx];
                    if (cycleOffsets != null && idx < cycleOffsets.Length)
                        offset = cycleOffsets[idx];
                }
                else
                {
                    silhouetteLayer.sprite = previewAreaConfig.backgroundSilhouetteSpriteB;
                }
                silhouetteLayer.transform.localScale = previewAreaConfig.backgroundSilhouetteScaleB;
                silhouetteLayer.transform.localPosition = previewAreaConfig.backgroundSilhouettePositionB + offset;
            }
            else
            {
                silhouetteLayer.sprite = previewAreaConfig.backgroundSilhouetteSprite;
                silhouetteLayer.transform.localScale = previewAreaConfig.backgroundSilhouetteScaleA;
                silhouetteLayer.transform.localPosition = previewAreaConfig.backgroundSilhouettePositionA;
            }
        }

        if (farLayer != null)
        {
            if (previewStageB)
            {
                farLayer.sprite = previewAreaConfig.backgroundSpriteB;
                farLayer.transform.localScale = previewAreaConfig.backgroundSpriteBScale;
                farLayer.transform.localPosition = previewAreaConfig.backgroundSpriteBPosition;
            }
            else
            {
                farLayer.sprite = previewAreaConfig.backgroundSprite;
                farLayer.transform.localScale = Vector3.one;
                farLayer.transform.localPosition = Vector3.zero;
            }
        }

        if (midLayer != null)
        {
            midLayer.sprite = previewAreaConfig.backgroundFogSprite;
            midLayer.transform.localScale = previewAreaConfig.backgroundFogScale;
            midLayer.transform.localPosition = previewAreaConfig.backgroundFogPosition;

            Color mc = midLayer.color;
            mc.a = (previewStageB && previewAreaConfig.midLayerHideOnStage3) ? 0f : 1f;
            midLayer.color = mc;
        }

        // ★Area09(Cosmos)のEarthレイヤーの「Play前プレビュー用アクティブ化」は廃止した。
        //   このGameObjectのアクティブ状態はシーンファイルに保存されるため、
        //   Editor閉じ忘れ等のタイミングでアクティブなまま保存されると、
        //   全Areaで表示されてしまう重大な不具合になった実績がある。
        //   Area09の見た目確認は必ずPlayモードで行うこと（BackgroundManager.Start()が
        //   Area09選択時のみ正しくアクティブ化する）。
    }
#endif

    [Header("Particles")]
    [Tooltip("インデックス = areaNumber。各エリアのParticleSystemを設定（不要なエリアはNone）")]
    [SerializeField] private ParticleSystem[] areaParticles;

    [Header("Stage Intro")]
    [Tooltip("StageIntroControllerを使う場合ON — Start時のParticle自動起動を抑制しStageIntroControllerが制御する")]
    [SerializeField] private bool suppressParticleOnStart = false;
    private ParticleSystem activeAreaParticle;

    /// <summary>StageIntroControllerのStep3でParticleを起動する</summary>
    public void ActivateAreaParticle()
    {
        if (activeAreaParticle != null)
            activeAreaParticle.gameObject.SetActive(true);
    }

    public bool IsTransitioning =>
        (farLayerFade != null && farLayerFade.IsTransitioning) ||
        (silhouetteFade != null && silhouetteFade.IsTransitioning);

    private Vector3 baseFarScale;
    private Vector3 baseFarPosition;

    private void Awake()
    {
        Instance = this;

        // ★AreaConfig.backgroundSpriteScale/PositionOffsetで上書きする前の、
        //   シーン本来のFar Layerスケール・位置を保持しておく（上書き無し時の基準値）
        if (farLayer != null)
        {
            baseFarScale = farLayer.transform.localScale;
            baseFarPosition = farLayer.transform.localPosition;
        }
    }

    private void OnEnable()
    {
        EnemySpawner.OnStageStarted += OnStageStarted;
    }

    private void OnDisable()
    {
        EnemySpawner.OnStageStarted -= OnStageStarted;
    }

    private void OnStageStarted(int stageIndex)
    {
        // ★Area10ボスラッシュは背景切替を全てArea10BossRushController経由（Cross/Applyfade系）で
        //   個別に行うため、この汎用Stage3切替（＝直前にキャッシュされたfarSpriteB等を使う）が
        //   競合して誤ったAreaの背景を一瞬表示してしまう。disableAutoStage3BackgroundSwap=trueなら無効化する。
        if (GameSession.HasValidArea() && GameSession.SelectedArea.disableAutoStage3BackgroundSwap)
            return;

        if (stageIndex >= 2)
        {
            ApplyStageBBackground();
        }
    }

    /// <summary>
    /// 現在キャッシュされているB（Stage3）系スプライト・設定をFar/Silhouette/MidLayerに適用する。
    /// 元はOnStageStarted(stageIndex>=2)専用だったロジックを、Area10ボスラッシュ
    /// （ApplyAreaInstant/CrossfadeAllLayersToArea）からも呼べるよう切り出したもの。
    /// </summary>
    private void ApplyStageBBackground()
    {
        {
            if (farSpriteB != null)
            {
                if (farLayerFade != null)
                {
                    farLayerFade.TransitionToSprite(farSpriteB, farScaleB, farPositionB, farLayerExtraScaleOverrideCached);

                    if (farLayerCyclePatterns != null && farLayerCyclePatterns.Length >= 2 && farLayerCycleFade != null)
                        pendingFarLayerCycleCoroutine = StartCoroutine(StartFarLayerCycleAfterFade(farLayerCyclePatterns, farLayerCycleHoldDurations, farLayerCycleFadeDuration, farLayerCycleFade));
                }
                else if (farLayer != null)
                {
                    farLayer.sprite = farSpriteB;
                    farLayer.transform.localScale = farScaleB;
                    farLayer.transform.localPosition = farPositionB;
                }
            }
            bool useSilhouetteCycle = silhouetteCyclePatterns != null && silhouetteCyclePatterns.Length >= 2 && silhouetteCycleFade != null;

            if (useSilhouetteCycle)
            {
                // CloudCycleFadeが独自にフェードイン（Initial Fade Duration）を行うため、
                // SilhouetteFade側の遷移（フェードアウト→差し替え→フェードイン）は不要かつ
                // 二重の待ち時間になってしまう。スプライト/位置だけ即座に切り替え、
                // フェードはCloudCycleFadeに完全に任せる
                if (silhouetteFade != null)
                    silhouetteFade.enabled = false;
                if (silhouetteLayer != null && silhouetteSpriteB != null)
                {
                    silhouetteLayer.sprite = silhouetteSpriteB;
                    silhouetteLayer.transform.localScale = silhouetteScaleB;
                    silhouetteLayer.transform.localPosition = silhouettePositionB;
                }
                silhouetteCycleFade.StartCycle(silhouetteCyclePatterns, silhouetteCycleOffsets, silhouetteCycleHoldDuration, silhouetteCycleFadeDuration, silhouetteCycleInitialFadeDuration, silhouetteCycleMaxAlpha, silhouetteCyclePingPongDriftCached, silhouetteCycleDriftAmplitudeCached, silhouetteCycleDriftSpeedCached);
            }
            else if (silhouetteFade != null && silhouetteSpriteB != null)
            {
                silhouetteFade.TransitionToSprite(silhouetteSpriteB, silhouetteScaleB, silhouettePositionB);
            }

            if (midLayerHideOnStage3Enabled && midLayer != null)
                StartCoroutine(FadeOutMidLayer());
        }
    }

    // FarLayerFadeのフェード完了（Background Sprite B切り替え完了）を待ってから
    // TimeOfDayFadeによる時間帯クロスフェード巡回を開始する（1枚目=Bを引き継ぐ）。
    // ★patterns等はスケジュールした瞬間の値を引数で受け取ること。メンバー変数
    //   （farLayerCyclePatterns等）をこのコルーチン再開時に読むと、待機中に次のボスへの
    //   ApplyArea()が先に走って値が上書きされ、違うAreaの巡回パターンで始まってしまう
    //   （Area10ボスラッシュでボスを素早く倒した時に発生しうるレースコンディション）。
    private System.Collections.IEnumerator StartFarLayerCycleAfterFade(Sprite[] patterns, float[] holdDurations, float fadeDuration, TimeOfDayFade targetCycleFade)
    {
        FarLayerFade waitTarget = farLayerFade;
        yield return new WaitUntil(() => waitTarget == null || !waitTarget.IsTransitioning);
        if (targetCycleFade != null)
            targetCycleFade.StartCycle(patterns, holdDurations, fadeDuration);
    }

    private System.Collections.IEnumerator FadeOutMidLayer()
    {
        float elapsed = 0f;
        float startAlpha = midLayer.color.a;
        while (elapsed < midLayerFadeOutDuration)
        {
            elapsed += Time.deltaTime;
            Color c = midLayer.color;
            c.a = Mathf.Lerp(startAlpha, 0f, elapsed / midLayerFadeOutDuration);
            midLayer.color = c;
            yield return null;
        }
        Color final = midLayer.color;
        final.a = 0f;
        midLayer.color = final;
    }

    /// <summary>
    /// Area10ボスラッシュ専用：Far/Mid/Silhouette全レイヤーを指定AreaConfigの背景にクロスフェードする。
    /// 既存のOnStageStarted経路（Area1〜9のStage3背景切替）とは完全に独立しており、他Areaの挙動には影響しない。
    /// </summary>
    public void CrossfadeAllLayersToArea(AreaConfig sourceArea, System.Action onComplete = null)
    {
        StartCoroutine(CrossfadeAllLayersRoutine(sourceArea, onComplete));
    }

    private System.Collections.IEnumerator CrossfadeAllLayersRoutine(AreaConfig sourceArea, System.Action onComplete)
    {
        if (sourceArea == null)
        {
            onComplete?.Invoke();
            yield break;
        }

        // ★ApplyArea()で、そのボスの出身Areaとしての完全な状態（MidLayerのスクロールモード、
        //   Area固有パーティクル、Area09のEarth/Auroraレイヤー、カメラ背景色、各種巡回パターンの
        //   キャッシュ）を一通り同期する。以前はFar/Silhouette/MidLayerのスプライト差し替えだけを
        //   個別に行っていたため、これらが前のボスの設定のまま取り残されるバグがあった。
        // applyAVariant=falseで、A（Stage1/2用）スプライトへは一切切り替えず、直接B系のキャッシュだけ更新する
        // （さもないと、A→Bのフェード開始前に一瞬Aがそのまま見えてしまう。Area2以降のボスで発生していた不具合）
        ApplyArea(sourceArea, false);
        // ApplyArea()はsuppressParticleOnStart時にパーティクルを隠したままにする仕様
        // （Stage1のみ、StageIntroController側の演出タイミングで見せるための設計）。
        // ボスラッシュの2体目以降はそのような専用演出が無いため、ここで確実に見せる。
        ActivateAreaParticle();

        // ★Area10のボスは各Areaの「Stage3（ボス専用）背景」を使うが、Area9のようにそもそもBを
        //   設定していないAreaもある（既存のApplyStageBBackground()と同じく「Bが無ければ何もしない
        //   ＝Aのまま維持」という既存Areaの仕様に合わせ、Bが未設定ならAにフォールバックする）
        Sprite farTarget = sourceArea.backgroundSpriteB != null ? sourceArea.backgroundSpriteB : sourceArea.backgroundSprite;
        Vector3 farTargetScale = sourceArea.backgroundSpriteB != null ? sourceArea.backgroundSpriteBScale : sourceArea.backgroundSpriteScale;
        Vector3 farTargetPosition = sourceArea.backgroundSpriteB != null ? sourceArea.backgroundSpriteBPosition : sourceArea.backgroundSpritePositionOffset;
        if (farLayerFade != null)
        {
            farLayerFade.TransitionToSprite(farTarget, farTargetScale, farTargetPosition, sourceArea.farLayerExtraScaleOverride);
            // ★Area8のように、Stage3のFar Layerが単一のBスプライトではなく複数枚を巡回させる
            //   演出（farLayerCyclePatterns、TimeOfDayFade）で構成されているAreaがある。
            //   既存のApplyStageBBackground()と同じく、Bへのフェード完了を待ってから巡回を開始する
            if (sourceArea.farLayerCyclePatterns != null && sourceArea.farLayerCyclePatterns.Length >= 2 && farLayerCycleFade != null)
                pendingFarLayerCycleCoroutine = StartCoroutine(StartFarLayerCycleAfterFade(sourceArea.farLayerCyclePatterns, sourceArea.farLayerCycleHoldDurations, sourceArea.farLayerCycleFadeDuration, farLayerCycleFade));
        }
        else if (farLayer != null)
        {
            farLayer.sprite = farTarget;
            farLayer.transform.localScale = farTargetScale;
            farLayer.transform.localPosition = farTargetPosition;
        }

        // ★Area8のように、Stage3のシルエットが単一のBスプライトではなく、複数枚を巡回させる
        //   演出（silhouetteCyclePatterns、CloudCycleFade）で構成されているAreaがある。
        //   既存のApplyStageBBackground()はこれを考慮しているが、Area10用のこの経路は
        //   単純なTransitionToSpriteしか行っておらず、巡回演出が再現されていなかった。
        bool useSilhouetteCycleForCrossfade = silhouetteCyclePatterns != null && silhouetteCyclePatterns.Length >= 2 && silhouetteCycleFade != null;
        Debug.Log($"[BackgroundManager] Silhouette branch for {sourceArea.areaName}: useCycle={useSilhouetteCycleForCrossfade}, " +
                  $"patternsLen={(silhouetteCyclePatterns != null ? silhouetteCyclePatterns.Length : -1)}, cycleFadeNull={silhouetteCycleFade == null}, " +
                  $"baseSprite(before)={(silhouetteLayer != null && silhouetteLayer.sprite != null ? silhouetteLayer.sprite.name : "null")}, " +
                  $"baseRendererEnabled(before)={(silhouetteLayer != null ? silhouetteLayer.enabled : (bool?)null)}");
        if (useSilhouetteCycleForCrossfade)
        {
            // CloudCycleFade自身が初回フェードイン（Initial Fade Duration）を行うため、
            // SilhouetteFade側の遷移は不要かつ二重の待ちになる。スプライトはCloudCycleFadeに任せる
            if (silhouetteFade != null)
                silhouetteFade.enabled = false;
            // ★AreaConfigのbackgroundSilhouetteScaleB/PositionB（実際の設定値。Area8は{4,4,4}/{1,-5,0}）を
            //   Transformへ適用していなかったため、前のボスのscale/positionが残ったまま巡回が始まり
            //   雲の大きさ・位置がズレていた（既存のApplyStageBBackground()には同じ処理があった）
            if (silhouetteLayer != null)
            {
                silhouetteLayer.transform.localScale = silhouetteScaleB;
                silhouetteLayer.transform.localPosition = silhouettePositionB;
            }
            silhouetteCycleFade.StartCycle(silhouetteCyclePatterns, silhouetteCycleOffsets, silhouetteCycleHoldDuration, silhouetteCycleFadeDuration, silhouetteCycleInitialFadeDuration, silhouetteCycleMaxAlpha, silhouetteCyclePingPongDriftCached, silhouetteCycleDriftAmplitudeCached, silhouetteCycleDriftSpeedCached);
            // ★CloudCycleFade.StartCycle()はbaseRenderer.enabled=falseにするだけでスプライト自体は
            //   書き換えないため、Inspector上は前のボス（例：Area7）のスプライト名が残り続ける。
            //   実際の描画には影響しない（enabled=falseで非表示）が、念のためここで明示的に
            //   無効化とスプライトのクリアを再度保証する（何らかの理由で有効なまま残るケースの対策）。
            if (silhouetteLayer != null)
            {
                silhouetteLayer.enabled = false;
                silhouetteLayer.sprite = null;
            }
            Debug.Log($"[BackgroundManager] Silhouette StartCycle called for {sourceArea.areaName}: " +
                      $"baseRendererEnabled(after)={(silhouetteLayer != null ? silhouetteLayer.enabled : (bool?)null)}");
        }
        else
        {
            if (silhouetteFade != null)
                silhouetteFade.enabled = true;
            // ★前のボスがCloudCycleFade（Area8等）でsilhouetteLayer.enabled=falseにしていた場合、
            //   ここで確実にtrueへ戻す。さもないと以降のボスのシルエットが永久に非表示のままになる。
            if (silhouetteLayer != null)
                silhouetteLayer.enabled = true;

            Sprite silhouetteTarget = sourceArea.backgroundSilhouetteSpriteB != null ? sourceArea.backgroundSilhouetteSpriteB : sourceArea.backgroundSilhouetteSprite;
            Vector3 silhouetteTargetScale = sourceArea.backgroundSilhouetteSpriteB != null ? sourceArea.backgroundSilhouetteScaleB : sourceArea.backgroundSilhouetteScaleA;
            Vector3 silhouetteTargetPosition = sourceArea.backgroundSilhouetteSpriteB != null ? sourceArea.backgroundSilhouettePositionB : sourceArea.backgroundSilhouettePositionA;
            if (silhouetteFade != null)
                silhouetteFade.TransitionToSprite(silhouetteTarget, silhouetteTargetScale, silhouetteTargetPosition);
            else if (silhouetteLayer != null)
                silhouetteLayer.sprite = silhouetteTarget;
        }

        if (midLayer != null)
            yield return StartCoroutine(CrossfadeMidLayer(sourceArea));

        yield return new WaitUntil(() =>
            (farLayerFade == null || !farLayerFade.IsTransitioning) &&
            (silhouetteFade == null || !silhouetteFade.IsTransitioning));

        if (targetCamera != null)
            targetCamera.backgroundColor = sourceArea.backgroundColor;

        onComplete?.Invoke();
    }

    private System.Collections.IEnumerator CrossfadeMidLayer(AreaConfig sourceArea)
    {
        float half = Mathf.Max(0.01f, midLayerCrossfadeDuration * 0.5f);

        Debug.Log($"[BackgroundManager] CrossfadeMidLayer START: sourceArea={sourceArea.areaName}(#{sourceArea.areaNumber}), " +
                  $"hideOnStage3={sourceArea.midLayerHideOnStage3}, midLayer.sprite={(midLayer.sprite != null ? midLayer.sprite.name : "null")}, " +
                  $"startAlpha={midLayer.color.a}, driftEnabled={midLayer.GetComponent<DriftScroll>()?.enabled}");

        // ★MidLayer（雲等）にはStage3専用のBスプライトが存在しない。
        //   そのAreaがmidLayerHideOnStage3=trueならStage3では非表示のままにし（alpha 0で維持）、
        //   falseならそのAreaの唯一のFogスプライトを使う（Stage1/2/3共通）。
        // ★hideOnStage3の場合、フェードアウトのLerpループを回す前に真っ先に前のボスの
        //   スクロールモード（Rain/Steam/Drift等）を無効化する。これらはUpdate()内でsr.color
        //   （=midLayer.color）を毎フレーム自前で上書きするため（例：DriftScrollのアルファパルス演出）、
        //   有効なままフェードアウトのLerpと同時に走らせると、フェードアウトの0.5秒間ずっと
        //   前のボスの絵がパルスして見えてしまっていた（無効化をフェードアウト後に回していたのが原因）。
        if (sourceArea.midLayerHideOnStage3)
            ApplyMidLayerScrollMode(AreaConfig.MidLayerScrollMode.None);

        float elapsed = 0f;
        float startAlpha = midLayer.color.a;
        while (elapsed < half)
        {
            elapsed += Time.deltaTime;
            Color c = midLayer.color;
            c.a = Mathf.Lerp(startAlpha, 0f, elapsed / half);
            midLayer.color = c;
            yield return null;
        }

        if (sourceArea.midLayerHideOnStage3)
        {
            // ★念のため、フェードアウト完了時点のalphaを確実に0へ強制する
            //   （以降このAreaの間は誰もmidLayer.colorを上書きしないことを保証する）。
            if (midLayer != null)
            {
                Color c = midLayer.color;
                c.a = 0f;
                midLayer.color = c;
                // ★alpha=0で数学的には不可視だが、Inspector上に前のボスのスプライト名が
                //   残り続けるのは紛らわしく、何らかの理由でalphaが崩れた際の見た目のリスクにもなる。
                //   念のためスプライト自体も明示的にクリアしておく。
                midLayer.sprite = null;
            }
            Debug.Log($"[BackgroundManager] CrossfadeMidLayer HIDDEN(END): sourceArea={sourceArea.areaName}, " +
                      $"finalAlpha={midLayer.color.a}, sprite={(midLayer.sprite != null ? midLayer.sprite.name : "null")}, " +
                      $"driftEnabled={midLayer.GetComponent<DriftScroll>()?.enabled}");
            yield break;
        }

        midLayer.transform.localScale = sourceArea.backgroundFogScale;
        midLayer.transform.localPosition = sourceArea.backgroundFogPosition;

        // ★このAreaのMidLayerが複数枚を巡回させる演出（midLayerCyclePatterns）で
        //   構成されている場合は、単純な1枚固定ではなくCloudCycleFadeで巡回させる
        bool useMidLayerCycleFade = sourceArea.midLayerCyclePatterns != null && sourceArea.midLayerCyclePatterns.Length >= 2
            && midLayer.GetComponent<CloudCycleFade>() != null;
        if (useMidLayerCycleFade)
        {
            ApplyMidLayerScrollMode(AreaConfig.MidLayerScrollMode.None);
            midLayerCycleFade = midLayer.GetComponent<CloudCycleFade>();
            midLayerCycleFade.StartCycle(sourceArea.midLayerCyclePatterns, sourceArea.midLayerCycleOffsets, sourceArea.midLayerCycleHoldDuration, sourceArea.midLayerCycleFadeDuration, sourceArea.midLayerCycleInitialFadeDuration, 1f, false, 0.3f, sourceArea.midLayerCycleDriftSpeed);
        }
        else
        {
            if (midLayerCycleFade != null) { midLayerCycleFade.StopCycle(); midLayerCycleFade = null; }
            midLayer.sprite = sourceArea.backgroundFogSprite;

            // ★スプライトを実際に切り替えたこのタイミングで初めてスクロールモードも切り替える
            //   （早く切り替えすぎると、まだ古い絵のままの状態でスクロールコンポーネントが
            //   初期化されてしまい、複製タイルに古いボスの絵が焼き付いて残ってしまう）
            ApplyMidLayerScrollMode(sourceArea.midLayerScrollMode);
            // ★既に同じスクロールモードが有効だったケース（連続で同じモードのボスが続く場合）に備えて、
            //   複製タイル側も念のため最新スプライトに同期する
            RefreshActiveMidLayerScroll(sourceArea.midLayerScrollMode);
        }

        elapsed = 0f;
        while (elapsed < half)
        {
            elapsed += Time.deltaTime;
            Color c = midLayer.color;
            c.a = Mathf.Lerp(0f, 1f, elapsed / half);
            midLayer.color = c;
            yield return null;
        }
        Color final = midLayer.color;
        final.a = 1f;
        midLayer.color = final;
    }

    private void Start()
    {
        // ★このクラスは[ExecuteAlways]のため、Start()はPlay中でなくEditor編集中にも
        //   発火することがある（ドメインリロード等のタイミング次第）。
        //   ここから下は実際のゲームプレイ用ロジック（TimeOfDayFade.StartCycle()による
        //   動的オブジェクト生成を含む）のため、Play中以外は絶対に実行しない。
        //   過去に、Edit中にこのロジックが誤発火し、GameSession.SelectedAreaが
        //   直前のPlayテストの値(Area09)を保持したままだったため、動的生成された
        //   レイヤーが大量にシーンへ焼き込まれる重大な不具合が発生した。
        if (!Application.isPlaying) return;

        if (targetCamera == null) targetCamera = Camera.main;

        foreach (var ps in areaParticles)
            if (ps != null) ps.gameObject.SetActive(false);

        foreach (var slot in extraFogSlots)
            if (slot != null) slot.gameObject.SetActive(false);

        if (GameSession.HasValidArea())
        {
            ApplyArea(GameSession.SelectedArea);
        }
        else
        {
            if (farLayer != null && fallbackFarSprite != null)
                farLayer.sprite = fallbackFarSprite;
            if (midLayer != null && fallbackFogSprite != null)
                midLayer.sprite = fallbackFogSprite;
            if (silhouetteLayer != null && fallbackSilhouetteSprite != null)
                silhouetteLayer.sprite = fallbackSilhouetteSprite;

            if (areaParticles.Length > 0 && areaParticles[0] != null)
                areaParticles[0].gameObject.SetActive(true);
        }
    }

    /// <summary>
    /// Area10ボスラッシュの最初のボス専用：Start()と全く同じ「即時（フェード無し）」の
    /// 背景・シルエット・MidLayer・Area固有パーティクルの適用を、任意のAreaConfigに対して行う。
    /// Start()の通常経路（GameSession.SelectedArea基準）とは独立しているため、既存Areaの挙動には影響しない。
    /// </summary>
    public void ApplyAreaInstant(AreaConfig area)
    {
        if (area == null) return;
        // applyAVariant=false：Area10のボスは常にB（Stage3）系のみを使うため、Aは一切経由しない
        ApplyArea(area, false);
        // Area10のボスは全て各Areaの「Stage3（ボス専用）背景」を使うため、A適用直後にBへ即座に上書きする
        // （ApplyStageBBackground()はFarLayerFade/SilhouetteFadeの1秒前後のフェード演出付きなので、
        //   フェード無しにしたいここでは使わず、直接プロパティを設定する専用版を使う）
        ApplyStageBBackgroundInstant(area);
    }

    /// <summary>
    /// ApplyStageBBackground()のフェード無し版。Area10ボスラッシュの最初のボス専用。
    /// FarLayerFade/SilhouetteFadeのTransitionToSprite（内部でフェードする）を経由せず、
    /// スプライト・スケール・位置を直接設定する。
    /// </summary>
    private void ApplyStageBBackgroundInstant(AreaConfig area)
    {
        // ★Area9のように、そもそもB（Stage3専用）を設定していないAreaもある
        //   （通常プレイではStage1/2のA画像をそのまま使い続け、その上に月・地球等の
        //   専用オブジェクトを追加表示するだけの設計。既存のApplyStageBBackground()も
        //   「Bが無ければ何もしない＝Aのまま維持」という仕様になっている）。
        //   Bが未設定ならAにフォールバックする。
        if (farLayer != null)
        {
            farLayer.sprite = area.backgroundSpriteB != null ? area.backgroundSpriteB : area.backgroundSprite;
            farLayer.transform.localScale = area.backgroundSpriteB != null ? area.backgroundSpriteBScale : area.backgroundSpriteScale;
            farLayer.transform.localPosition = area.backgroundSpriteB != null ? area.backgroundSpriteBPosition : area.backgroundSpritePositionOffset;

            // ★Area8のように、Far Layerが複数枚を巡回させる演出（farLayerCyclePatterns）で
            //   構成されている場合は、即座に巡回を開始する（フェード無し版のためここでは待たない）
            if (area.farLayerCyclePatterns != null && area.farLayerCyclePatterns.Length >= 2 && farLayerCycleFade != null)
                farLayerCycleFade.StartCycle(area.farLayerCyclePatterns, area.farLayerCycleHoldDurations, area.farLayerCycleFadeDuration);

            // ★上の直接代入（localScale = backgroundSpriteScale）はBackgroundFitter2Dのキャッシュ判定
            //   （スプライト・extraScale・カメラが前回と同じなら再計算しない）をすり抜けて上書きしてしまい、
            //   farLayerExtraScaleOverrideが永久に反映されなくなる。ここで再度SetExtraScaleを呼び、
            //   Cover計算＋extraScaleの結果で確実に最終値を確定させる。
            BackgroundFitter2D farFitterInstant = farLayer.GetComponent<BackgroundFitter2D>();
            if (farFitterInstant != null)
                farFitterInstant.SetExtraScale(area.farLayerExtraScaleOverride);
        }

        // ★このAreaのStage3シルエットが複数枚を巡回させる演出（silhouetteCyclePatterns）で
        //   構成されている場合は、単純な1枚固定ではなくCloudCycleFadeで巡回させる
        bool useSilhouetteCycleInstant = area.silhouetteCyclePatterns != null && area.silhouetteCyclePatterns.Length >= 2
            && silhouetteLayer != null && silhouetteLayer.GetComponent<CloudCycleFade>() != null;
        if (useSilhouetteCycleInstant)
        {
            if (silhouetteFade != null) silhouetteFade.enabled = false;
            // ★area.backgroundSilhouetteScaleB/PositionB（実際の設定値）をTransformへ適用してからでないと、
            //   CloudCycleFadeの子レイヤーはこの親のlossyScale/positionをそのまま引き継ぐため、
            //   デフォルト値のまま巡回が始まり雲の大きさ・位置がズレる
            silhouetteLayer.transform.localScale = area.backgroundSilhouetteScaleB;
            silhouetteLayer.transform.localPosition = area.backgroundSilhouettePositionB;
            silhouetteCycleFade = silhouetteLayer.GetComponent<CloudCycleFade>();
            silhouetteCycleFade.StartCycle(area.silhouetteCyclePatterns, area.silhouetteCycleOffsets, area.silhouetteCycleHoldDuration, area.silhouetteCycleFadeDuration, area.silhouetteCycleInitialFadeDuration, area.silhouetteCycleMaxAlpha, area.silhouetteCyclePingPongDrift, area.silhouetteCycleDriftAmplitude, area.silhouetteCycleDriftSpeed);
        }
        else if (silhouetteLayer != null)
        {
            if (silhouetteFade != null) silhouetteFade.enabled = true;
            silhouetteLayer.sprite = area.backgroundSilhouetteSpriteB != null ? area.backgroundSilhouetteSpriteB : area.backgroundSilhouetteSprite;
            silhouetteLayer.transform.localScale = area.backgroundSilhouetteSpriteB != null ? area.backgroundSilhouetteScaleB : area.backgroundSilhouetteScaleA;
            silhouetteLayer.transform.localPosition = area.backgroundSilhouetteSpriteB != null ? area.backgroundSilhouettePositionB : area.backgroundSilhouettePositionA;
        }

        // ★このAreaのMidLayerが複数枚を巡回させる演出（midLayerCyclePatterns）で
        //   構成されている場合は、単純な1枚固定ではなくCloudCycleFadeで巡回させる
        bool useMidLayerCycleInstant = area.midLayerCyclePatterns != null && area.midLayerCyclePatterns.Length >= 2
            && midLayer != null && midLayer.GetComponent<CloudCycleFade>() != null;
        if (useMidLayerCycleInstant && !area.midLayerHideOnStage3)
        {
            ApplyMidLayerScrollMode(AreaConfig.MidLayerScrollMode.None);
            midLayer.transform.localScale = area.backgroundFogScale;
            midLayer.transform.localPosition = area.backgroundFogPosition;
            midLayerCycleFade = midLayer.GetComponent<CloudCycleFade>();
            midLayerCycleFade.StartCycle(area.midLayerCyclePatterns, area.midLayerCycleOffsets, area.midLayerCycleHoldDuration, area.midLayerCycleFadeDuration, area.midLayerCycleInitialFadeDuration, 1f, false, 0.3f, area.midLayerCycleDriftSpeed);
            Color cc = midLayer.color;
            cc.a = 1f;
            midLayer.color = cc;
        }
        else if (midLayer != null)
        {
            if (midLayerCycleFade != null) { midLayerCycleFade.StopCycle(); midLayerCycleFade = null; }
            if (!area.midLayerHideOnStage3)
            {
                midLayer.sprite = area.backgroundFogSprite;
                midLayer.transform.localScale = area.backgroundFogScale;
                midLayer.transform.localPosition = area.backgroundFogPosition;

                ApplyMidLayerScrollMode(area.midLayerScrollMode);
                RefreshActiveMidLayerScroll(area.midLayerScrollMode);
            }
            Color c = midLayer.color;
            c.a = area.midLayerHideOnStage3 ? 0f : 1f;
            midLayer.color = c;
        }
    }

    private void ApplyArea(AreaConfig area) => ApplyArea(area, true);

    /// <summary>
    /// AreaConfigの内容を全レイヤーに適用する。applyAVariant=falseの場合、Far/SilhouetteのA
    /// （Stage1/2用）スプライト・スケール・位置の即時適用、Stage1/2用の巡回パターン開始、および
    /// MidLayerのスプライト・スケール・位置の即時適用をスキップする（スクロールモードの有効/無効・
    /// パーティクル・Earthレイヤー・カメラ色などは通常通り適用する）。
    /// Area10ボスラッシュ専用：これらの見た目は呼び出し元（CrossfadeAllLayersToArea）が
    /// 自前のフェード処理で切り替えるため、ここで即座に上書きしてしまうとフェードより先に
    /// 絵が変わってしまう（MidLayerが「既に新しい絵になったものをフェードする」誤動作になっていた）。
    /// </summary>
    private void ApplyArea(AreaConfig area, bool applyAVariant)
    {
        {
            // ★Earth/Auroraレイヤーは常時シーンに存在するオブジェクトのため、
            //   Area09以外を選んだ時に映り込まないよう明示的に非表示にする
            bool showEarthLayers = area.areaNumber == 9;
            if (earthMask != null) earthMask.SetActive(showEarthLayers);
            if (earthSurface != null) earthSurface.SetActive(showEarthLayers);
            if (earthRimGlow != null) earthRimGlow.SetActive(showEarthLayers);
            if (aurora != null) aurora.SetActive(showEarthLayers);
            if (meteorEffect != null) meteorEffect.SetActive(showEarthLayers);

            if (applyAVariant && farLayer != null)
            {
                farLayer.sprite = area.backgroundSprite;
                farLayer.transform.localScale = area.backgroundSpriteScale != Vector3.zero
                    ? area.backgroundSpriteScale
                    : baseFarScale;
                farLayer.transform.localPosition = baseFarPosition + area.backgroundSpritePositionOffset;
            }

            // ★Far Layer（BackgroundFitter2D）はCoverモードで画面を覆うが、解像度によっては
            //   端に隙間（黒帯）が出ることがある。AreaConfig側でこのAreaだけextraScaleを
            //   上書きできるようにする（Area1で実際に下端に隙間が出たため追加）。
            if (farLayer != null)
            {
                BackgroundFitter2D farFitter = farLayer.GetComponent<BackgroundFitter2D>();
                if (farFitter != null)
                    farFitter.SetExtraScale(area.farLayerExtraScaleOverride);
            }
            farSpriteB = area.backgroundSpriteB;
            farScaleB = area.backgroundSpriteBScale;
            farPositionB = area.backgroundSpriteBPosition;
            farLayerExtraScaleOverrideCached = area.farLayerExtraScaleOverride;

            farLayerCycleFade = farLayer != null ? farLayer.GetComponent<TimeOfDayFade>() : null;
            farLayerCyclePatterns = area.farLayerCyclePatterns;
            farLayerCycleHoldDurations = area.farLayerCycleHoldDurations;
            farLayerCycleFadeDuration = area.farLayerCycleFadeDuration;
            // ★前のボスの「フェード完了を待ってから巡回開始」予約がまだ実行されていなければここで確実に破棄する。
            //   さもないと、このApplyArea()（＝新しいボスへの切替）より後にその予約が実行され、
            //   既に退場したはずの前のボスの巡回パターンが新しいボスの背景に上書きされてしまう
            //   （Area10ボスラッシュでボスを素早く倒した時に発生しうるレースコンディション）。
            if (pendingFarLayerCycleCoroutine != null)
            {
                StopCoroutine(pendingFarLayerCycleCoroutine);
                pendingFarLayerCycleCoroutine = null;
            }
            if (farLayerCycleFade != null)
                farLayerCycleFade.StopCycle();

            // Stage1/2用の巡回パターン（Area09宇宙背景の星空変化演出など）。
            // Stage3への遷移（OnStageStarted）で上のfarLayerCyclePatternsに切り替わるまで継続する
            Debug.Log($"[BackgroundManager] Stage1 cycle check: farLayerCycleFade={(farLayerCycleFade != null)}, " +
                      $"patternsNull={(area.farLayerCyclePatternsStage1 == null)}, " +
                      $"patternsLength={(area.farLayerCyclePatternsStage1 != null ? area.farLayerCyclePatternsStage1.Length : -1)}");
            if (applyAVariant && farLayerCycleFade != null && area.farLayerCyclePatternsStage1 != null && area.farLayerCyclePatternsStage1.Length >= 2)
                farLayerCycleFade.StartCycle(area.farLayerCyclePatternsStage1, area.farLayerCycleHoldDurationsStage1, area.farLayerCycleFadeDurationStage1, area.farLayerCycleOverlapStage1);
            midLayerHideOnStage3Enabled = area.midLayerHideOnStage3;
            if (midLayer != null)
            {
                if (applyAVariant)
                {
                    midLayer.sprite = area.backgroundFogSprite;
                    midLayer.transform.localScale = area.backgroundFogScale;
                    midLayer.transform.localPosition = area.backgroundFogPosition;

                    // ★Area10ボスラッシュ専用：midLayerのスプライトを実行中に差し替えるため、
                    //   2枚並べ方式のスクロール系コンポーネントの複製タイル側も同じスプライトに更新する
                    //   （さもないと2枚のタイルの絵が食い違い、継ぎ目が見えてしまう）
                    RefreshActiveMidLayerScroll(area.midLayerScrollMode);

                    // ★スプライトの差し替えと同時にスクロールモードも切り替える（Area1〜9の通常経路）。
                    //   Area10ボスラッシュ（applyAVariant=false）ではここでは切り替えない。
                    //   スプライトの実切替がCrossfadeMidLayer側に遅延しているため、ここで先に
                    //   スクロールを有効化すると、まだ古い絵のままのタイミングで初期化されてしまい、
                    //   複製タイルに古いボスの絵が焼き付いたまま残る不具合があった。
                    ApplyMidLayerScrollMode(area.midLayerScrollMode);

                    // ★このalpha=1強制は applyAVariant（Area1〜9の通常経路）専用。
                    //   Area10ボスラッシュ（applyAVariant=false）でこれが条件外にあった時は、
                    //   CrossfadeMidLayer()がまだ非表示フェードを始めてもいないうちに
                    //   ここで強制的にalpha=1へ戻してしまい、前のボスの絵（例：MidLayer_Area07）が
                    //   一瞬フルアルファで見えてしまっていた。
                    Color mc = midLayer.color;
                    mc.a = 1f;
                    midLayer.color = mc;
                }
            }

            {
                SpriteRenderer midSR = midLayer != null ? midLayer.GetComponent<SpriteRenderer>() : null;
                for (int i = 0; i < extraFogSlots.Length; i++)
                {
                    if (extraFogSlots[i] == null) continue;

                    bool hasData = area.extraFogLayers != null
                        && i < area.extraFogLayers.Length
                        && area.extraFogLayers[i].sprite != null;

                    if (hasData)
                    {
                        var data = area.extraFogLayers[i];
                        extraFogSlots[i].gameObject.SetActive(true);
                        extraFogSlots[i].sprite = data.sprite;
                        extraFogSlots[i].transform.localPosition = data.position;
                        extraFogSlots[i].transform.localScale = data.scale;
                        if (midSR != null)
                        {
                            extraFogSlots[i].material = midSR.material;
                            extraFogSlots[i].sortingLayerID = midSR.sortingLayerID;
                            extraFogSlots[i].sortingOrder = midSR.sortingOrder + data.sortingOrderOffset;
                        }
                        var fog = extraFogSlots[i].GetComponent<FogScroll>();
                        if (fog != null)
                        {
                            fog.SetScrollParameters(data.scrollSpeed, data.waveAmplitude, data.waveFrequency);
                            // ★midLayerと同じく、Area10ボスラッシュでスプライトが差し替わった時に
                            //   複製タイル側が古い絵のまま残らないよう同期する（継ぎ目防止）
                            fog.RefreshSprite();
                        }
                    }
                    else
                    {
                        // ★新しいAreaがこのスロットを使わない場合は必ず非アクティブ化する。
                        //   以前はここが無く、前のAreaが使っていたExtraFogLayerが次のAreaに
                        //   引き継がれずに残り続けるバグがあった（Area10のボス切替で発生しうる）。
                        extraFogSlots[i].gameObject.SetActive(false);
                    }
                }
            }

            if (silhouetteLayer != null)
            {
                if (applyAVariant)
                {
                    silhouetteLayer.sprite = area.backgroundSilhouetteSprite;
                    silhouetteLayer.transform.localScale = area.backgroundSilhouetteScaleA;
                    silhouetteLayer.transform.localPosition = area.backgroundSilhouettePositionA;
                }
                silhouetteSpriteB = area.backgroundSilhouetteSpriteB;
                silhouetteScaleB = area.backgroundSilhouetteScaleB;
                silhouettePositionB = area.backgroundSilhouettePositionB;
                silhouetteFade = silhouetteLayer.GetComponent<SilhouetteFade>();
                if (silhouetteFade != null)
                    silhouetteFade.SetAlwaysFullAlpha(area.silhouetteAlwaysFullAlpha);

                silhouetteCycleFade = silhouetteLayer.GetComponent<CloudCycleFade>();
                silhouetteCyclePatterns = area.silhouetteCyclePatterns;
                silhouetteCycleOffsets = area.silhouetteCycleOffsets;
                silhouetteCycleHoldDuration = area.silhouetteCycleHoldDuration;
                silhouetteCycleFadeDuration = area.silhouetteCycleFadeDuration;
                silhouetteCycleInitialFadeDuration = area.silhouetteCycleInitialFadeDuration;
                silhouetteCycleMaxAlpha = area.silhouetteCycleMaxAlpha;
                silhouetteCyclePingPongDriftCached = area.silhouetteCyclePingPongDrift;
                silhouetteCycleDriftAmplitudeCached = area.silhouetteCycleDriftAmplitude;
                silhouetteCycleDriftSpeedCached = area.silhouetteCycleDriftSpeed;
                if (silhouetteCycleFade != null)
                    silhouetteCycleFade.StopCycle();
            }
            if (targetCamera != null)
                targetCamera.backgroundColor = area.backgroundColor;

            int idx = area.areaNumber;
            ParticleSystem newParticle = (idx >= 0 && idx < areaParticles.Length) ? areaParticles[idx] : null;
            // ★ApplyArea()がAreaを跨いで複数回呼ばれるケース（Area10ボスラッシュのボス切替）で、
            //   前のボスのパーティクルが有効なまま残らないよう、切り替わる時は明示的に消す
            if (activeAreaParticle != null && activeAreaParticle != newParticle)
                activeAreaParticle.gameObject.SetActive(false);

            if (newParticle != null)
            {
                activeAreaParticle = newParticle;
                if (!suppressParticleOnStart)
                    newParticle.gameObject.SetActive(true);
            }
            else
            {
                activeAreaParticle = null;
            }
        }
    }

    /// <summary>
    /// midLayerのスクロールモード（Fog/Rain/Steam/GroundFog/Drift/Vortex/None）を切り替える。
    /// スプライトの実際の切替と同じタイミングで呼ぶこと（ApplyArea()内の通常経路では即座に、
    /// Area10ボスラッシュではCrossfadeMidLayer/ApplyStageBBackgroundInstant側でスプライト切替と
    /// 同時に呼ぶ）。ここを早く呼びすぎると、まだ古いスプライトのままの状態でスクロール
    /// コンポーネントが初期化されてしまい、複製タイルに古いボスの絵が焼き付いて残ってしまう。
    /// </summary>
    private void ApplyMidLayerScrollMode(AreaConfig.MidLayerScrollMode mode)
    {
        if (midLayer == null) return;

        var fogScroll = midLayer.GetComponent<FogScroll>();
        var rainScroll = midLayer.GetComponent<RainScroll>();
        var steamScroll = midLayer.GetComponent<SteamScroll>();
        var groundFogScroll = midLayer.GetComponent<GroundFogScroll>();
        var driftScroll = midLayer.GetComponent<DriftScroll>();
        var vortexScroll = midLayer.GetComponent<VortexScroll>();

        if (fogScroll != null) fogScroll.enabled = mode == AreaConfig.MidLayerScrollMode.Fog;
        if (rainScroll != null) rainScroll.enabled = mode == AreaConfig.MidLayerScrollMode.Rain;
        if (steamScroll != null) steamScroll.enabled = mode == AreaConfig.MidLayerScrollMode.Steam;
        if (groundFogScroll != null) groundFogScroll.enabled = mode == AreaConfig.MidLayerScrollMode.GroundFog;
        if (driftScroll != null) driftScroll.enabled = mode == AreaConfig.MidLayerScrollMode.Drift;
        if (vortexScroll != null) vortexScroll.enabled = mode == AreaConfig.MidLayerScrollMode.Vortex;
    }

    /// <summary>
    /// Area10ボスラッシュ専用：現在有効な（有効になろうとしている）スクロールモードに対応する
    /// コンポーネントのRefreshSprite()を呼び、複製タイルを最新スプライトに同期する。
    /// </summary>
    private void RefreshActiveMidLayerScroll(AreaConfig.MidLayerScrollMode mode)
    {
        if (midLayer == null) return;

        switch (mode)
        {
            case AreaConfig.MidLayerScrollMode.Fog:
                midLayer.GetComponent<FogScroll>()?.RefreshSprite();
                break;
            case AreaConfig.MidLayerScrollMode.Rain:
                midLayer.GetComponent<RainScroll>()?.RefreshSprite();
                break;
            case AreaConfig.MidLayerScrollMode.Steam:
                midLayer.GetComponent<SteamScroll>()?.RefreshSprite();
                break;
            case AreaConfig.MidLayerScrollMode.Drift:
                midLayer.GetComponent<DriftScroll>()?.RefreshSprite();
                break;
        }
    }
}
