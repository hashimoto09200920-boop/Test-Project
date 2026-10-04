using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Area10 Final Stage開始演出（ボス9撃破後 → 最終ボス「NeonDancer」戦の開始まで）。
/// EnemySpawnerがFinal Stageの開始時にPlayRoutine()を呼ぶ（Area10BossRushController.finalIntroに設定されている時だけ）。
///
/// ■流れ
///   ② 次元移動（dimensionDuration秒）：開始の瞬間のゲーム画面（UIを除く）を取り込み、中心の巨大ワームホールへ
///      渦を巻いて吸い込まれるように歪め、グリッチ（色ずれ・横ずれ・走査線・ブロックノイズ）を重ねて暗転する。
///      同時に前のBGMをフェードアウト、ブロック・アイテム・弾を消去、月をOFF、裏で背景をArea10へ切り替える。
///      プレイヤー側（Floor・Partner・スポットライト・Dancer）も非表示にする（画面の取り込み画像ごと吸い込まれて見える）。
///   ③ 暗転明け：ボスを「待機状態（非表示・動かない）」でスポーンし、Final Stageの背景をフェードインする。
///   ③④ プレイヤー側のArea開始演出（StageIntroController.PlayIntro）をもう一度再生し、ボス側も鏡合わせに同期させる
///      （Floor → 同じポーズを左右反転したシルエット＋Light本体 → 点灯で色付き・ビーム・足元スポット）。
///      29_Area10_Aは点灯後にPlayIntro()内のBGM開始処理で再生される。
///   ⑤ Final Stageカットイン → ⑥ VS演出 → プレイヤーのDancer表示・ボスが動き出してゲーム開始。
///
/// ★背景の切替・BGMの準備はArea10BossRushController.PrepareFinalStageForIntroRoutine()、
///   プレイヤー側の演出は既存のStageIntroController.PlayIntro()をそのまま使う（この演出専用の通知だけ追加）。
/// </summary>
public class Area10FinalIntroController : MonoBehaviour
{
    private static readonly Color[] AreaColors =
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

    [Header("参照")]
    [SerializeField] private Area10BossRushController bossRushController;
    [SerializeField] private StageIntroController stageIntroController;
    [Tooltip("画面を取り込むカメラ（未設定ならMain Camera）")]
    [SerializeField] private Camera targetCamera;

    [Header("描画順（Sorting Layer「Default」。HUDのCanvas（Order 1200）より下・ゲーム画面より上）")]
    [Tooltip("取り込み画面の描画順。ワームホール（外側/内側/中心の穴）と光の粒はこの+1〜+4で描く")]
    [SerializeField] private int baseSortingOrder = 1100;

    [Header("② 次元移動 - 全体")]
    [Tooltip("次元移動の長さ（秒）")]
    [SerializeField] private float dimensionDuration = 2f;
    [Tooltip("前のBGMがフェードアウトする秒数")]
    [SerializeField] private float bgmFadeOutDuration = 1.5f;
    [Tooltip("次元移動の開始と同時に鳴らすSE（任意）")]
    [SerializeField] private AudioClip dimensionSE;
    [Range(0f, 1f)] [SerializeField] private float dimensionSEVolume = 1f;

    [Header("② 次元移動 - 画面の歪み（横軸＝経過割合0〜1）")]
    [Tooltip("取り込み画面を表示するSpriteRenderer（マテリアル：Sprites/Area10DimensionWarp）")]
    [SerializeField] private SpriteRenderer warpRenderer;
    [Tooltip("吸い込みの強さ（大きいほど画面が中心へ縮む）")]
    [SerializeField] private AnimationCurve suckCurve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.4f, 0.25f), new Keyframe(0.85f, 1.6f), new Keyframe(1f, 4f));
    [Tooltip("渦の回転量（ラジアン。中心ほど強く回る）")]
    [SerializeField] private AnimationCurve twistCurve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(1f, 7f));
    [Tooltip("横ずれグリッチの強さ（0〜1）")]
    [SerializeField] private AnimationCurve glitchCurve = new AnimationCurve(new Keyframe(0f, 0.15f), new Keyframe(0.12f, 0.8f), new Keyframe(0.25f, 0.15f), new Keyframe(0.5f, 0.6f), new Keyframe(0.7f, 1f), new Keyframe(1f, 1f));
    [Tooltip("色ずれ（RGB分離）の強さ（0〜1）")]
    [SerializeField] private AnimationCurve rgbSplitCurve = new AnimationCurve(new Keyframe(0f, 0.3f), new Keyframe(0.7f, 1f), new Keyframe(1f, 1f));
    [Tooltip("走査線の濃さ（0〜1）")]
    [SerializeField] private AnimationCurve scanlineCurve = new AnimationCurve(new Keyframe(0f, 0.3f), new Keyframe(1f, 0.8f));
    [Tooltip("ブロックノイズの量（0〜1。1の時にBlock Noise Max Coverageの割合のマスがブロックになる）")]
    [SerializeField] private AnimationCurve blockNoiseCurve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.3f, 0.2f), new Keyframe(0.8f, 0.7f), new Keyframe(1f, 1f));
    [Tooltip("ブロックノイズのマスの縦の数（横の数は画面の縦横比から自動計算＝正方形のマス）。大きいほどブロックが小さい")]
    [SerializeField] private int blockNoiseRows = 30;
    [Tooltip("Block Noise Curveが1の時に、ブロックになるマスの割合（0〜1）")]
    [Range(0f, 1f)] [SerializeField] private float blockNoiseMaxCoverage = 0.08f;
    [Tooltip("ブロックの濃さ（0〜1）。色はArea1〜9の9色からランダム")]
    [Range(0f, 1f)] [SerializeField] private float blockNoiseOpacity = 0.85f;
    [Tooltip("暗転（0〜1。1で真っ黒）")]
    [SerializeField] private AnimationCurve darkenCurve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.7f, 0.1f), new Keyframe(1f, 1f));
    [Tooltip("グリッチの模様を切り替える間隔（秒）")]
    [SerializeField] private float glitchJitterInterval = 0.05f;

    [Header("② 次元移動 - 巨大ワームホール（NeonDancerのワームホール画像）")]
    [SerializeField] private Transform wormholeRoot;
    [Tooltip("9色で色が巡回するレイヤー（外側の渦・内側の渦）")]
    [SerializeField] private SpriteRenderer[] wormholeTintedLayers;
    [Tooltip("逆回転させるレイヤー（内側の渦）")]
    [SerializeField] private Transform[] wormholeCounterSpinLayers;
    [Tooltip("中心の黒い穴")]
    [SerializeField] private SpriteRenderer wormholeVoid;
    [Tooltip("画面の対角線に対するワームホールの最大の大きさ（1で対角線と同じ）")]
    [SerializeField] private float wormholeCoverRatio = 1.3f;
    [Tooltip("ワームホール（渦）の大きさ（最大に対する割合）")]
    [SerializeField] private AnimationCurve wormholeScaleCurve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.3f, 0.25f), new Keyframe(0.7f, 0.6f), new Keyframe(1f, 1.2f));
    [Tooltip("中心の黒い穴の大きさ（ワームホールの大きさに対する割合）")]
    [SerializeField] private AnimationCurve voidScaleCurve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.6f, 0.3f), new Keyframe(1f, 1.4f));
    [Tooltip("ワームホールの不透明度")]
    [SerializeField] private AnimationCurve wormholeAlphaCurve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.15f, 1f), new Keyframe(1f, 1f));
    [Tooltip("回転の速さ（度/秒）")]
    [SerializeField] private float wormholeSpinSpeed = 300f;
    [Tooltip("内側の渦の逆回転の速さ（外側に対する倍率）")]
    [SerializeField] private float wormholeCounterSpinRatio = 1.3f;
    [Tooltip("9色（Area1〜9）を1秒に何色進めるか")]
    [SerializeField] private float wormholeColorCycleSpeed = 6f;

    [Header("② 次元移動 - 9色の光の粒（中心へ渦を巻いて吸い込まれる）")]
    [SerializeField] private ParticleSystem sparkleParticles;
    [Tooltip("1秒あたりの発生数（横軸＝経過割合0〜1）")]
    [SerializeField] private AnimationCurve sparkleRateCurve = new AnimationCurve(new Keyframe(0f, 60f), new Keyframe(0.6f, 160f), new Keyframe(0.85f, 40f), new Keyframe(1f, 0f));
    [Tooltip("中心に届くまでの秒数（最小/最大）")]
    [SerializeField] private Vector2 sparkleLifetime = new Vector2(0.5f, 0.9f);
    [SerializeField] private Vector2 sparkleSize = new Vector2(0.08f, 0.22f);
    [Tooltip("渦の強さ（中心へ向かう速さに対する、横向きの速さの比率）")]
    [SerializeField] private float sparkleSwirl = 0.8f;

    [Header("③ 暗転明け・登場")]
    [Tooltip("暗転してから背景のフェードインを始めるまでの最短の秒数（背景の切替が終わっていなければ終わるまで待つ）")]
    [SerializeField] private float blackHoldDuration = 0.3f;
    [Tooltip("背景の切替を待つ最大秒数（これを超えたら先へ進む）")]
    [SerializeField] private float maxBackgroundWait = 6f;
    [Tooltip("Final Stageの背景がフェードインする秒数")]
    [SerializeField] private float backgroundFadeInDuration = 1.0f;
    [Tooltip("背景のフェードイン後、Floor・シルエットの登場演出を始めるまでの秒数")]
    [SerializeField] private float delayBeforeStageIntro = 0.3f;

    [Header("⑤⑥ カットイン・VS")]
    [Tooltip("点灯（BGM開始）からFinal Stageカットインまでの秒数")]
    [SerializeField] private float delayBeforeCutIn = 0.8f;

    [System.Serializable]
    public class VsBossPose
    {
        public Sprite sprite;
        [Tooltip("このポーズの表示サイズ倍率（VsIntroUIのBoss画像のScale）")]
        public float scale = 1f;
        [Tooltip("このポーズの位置補正（px、VsIntroUIのBoss画像のanchoredPosition）")]
        public Vector2 positionOffset = Vector2.zero;
    }

    [Tooltip("VSのボス画像（毎回ランダムに1枚選ぶ。プレイヤー側のポーズとは別に選ぶ）。\n" +
             "ND_VsImageSettingsの「画像を生成してArea10Configに設定」で、プレイヤー側の各ポーズと同じ大きさ・足元の高さになる値が自動設定される。\n" +
             "空ならArea10ConfigのVs Boss Sprite / Scale / Position Offsetを使う")]
    [SerializeField] private VsBossPose[] vsBossPoses;

    private Material warpMaterial;
    private RenderTexture captureRT;
    private NeonDancerController boss;
    private float dancerCenterX;

    private static readonly int CaptureTexId = Shader.PropertyToID("_CaptureTex");
    private static readonly int SuckId       = Shader.PropertyToID("_Suck");
    private static readonly int TwistId      = Shader.PropertyToID("_Twist");
    private static readonly int GlitchId     = Shader.PropertyToID("_Glitch");
    private static readonly int RgbSplitId   = Shader.PropertyToID("_RGBSplit");
    private static readonly int ScanlineId   = Shader.PropertyToID("_Scanline");
    private static readonly int BlockNoiseId = Shader.PropertyToID("_BlockNoise");
    private static readonly int BlockGridId  = Shader.PropertyToID("_BlockGrid");
    private static readonly int BlockCoverageId = Shader.PropertyToID("_BlockCoverage");
    private static readonly int BlockOpacityId  = Shader.PropertyToID("_BlockOpacity");
    private static readonly int DarkenId     = Shader.PropertyToID("_Darken");
    private static readonly int SeedId       = Shader.PropertyToID("_Seed");
    private static readonly int AspectId     = Shader.PropertyToID("_Aspect");
    private static readonly int FlipYId      = Shader.PropertyToID("_FlipY");

    private static float MasterSEVolume => SoundSettingsManager.Instance != null ? SoundSettingsManager.Instance.SEVolume : 1f;

    private void Awake()
    {
        if (warpRenderer != null)
        {
            warpMaterial = warpRenderer.material; // 個別インスタンス（共有マテリアルを書き換えない）
            warpRenderer.gameObject.SetActive(false);
        }
        if (wormholeRoot != null) wormholeRoot.gameObject.SetActive(false);
        ApplySortingOrders();
    }

    private void OnDestroy()
    {
        ReleaseCapture();
        if (warpMaterial != null) Destroy(warpMaterial);
    }

    private void ApplySortingOrders()
    {
        if (warpRenderer != null) SetSorting(warpRenderer, baseSortingOrder);
        if (wormholeTintedLayers != null)
            for (int i = 0; i < wormholeTintedLayers.Length; i++)
                if (wormholeTintedLayers[i] != null) SetSorting(wormholeTintedLayers[i], baseSortingOrder + 1 + i);
        if (wormholeVoid != null) SetSorting(wormholeVoid, baseSortingOrder + 3);
        if (sparkleParticles != null)
        {
            var r = sparkleParticles.GetComponent<ParticleSystemRenderer>();
            if (r != null) { r.sortingLayerName = "Default"; r.sortingOrder = baseSortingOrder + 4; }
        }
    }

    private static void SetSorting(Renderer r, int order)
    {
        r.sortingLayerName = "Default";
        r.sortingOrder = order;
    }

    // ======================================================
    // Main
    // ======================================================

    /// <summary>
    /// EnemySpawnerから呼ばれる。spawnFormationは途中（暗転中）でボスをスポーンするために使う。
    /// 線の入力・スローモーション・中断の無効化/有効化はEnemySpawner側で行う。
    /// </summary>
    public IEnumerator PlayRoutine(System.Func<IEnumerator> spawnFormation, StageCutInUI cutIn, int stageIndex, VsIntroUI vsIntro, AreaConfig areaConfig)
    {
        Debug.Log($"[Area10FinalIntro] 開始 frame={Time.frameCount}");
        bool replayStageIntro = stageIntroController != null && !stageIntroController.IsIntroSkipped;

        // ---------- ② 次元移動 ----------
        yield return CaptureScreen();

        if (replayStageIntro) stageIntroController.HideForReintro();
        MovePlayerDancerToCenter();

        bool backgroundDone = false;
        if (bossRushController != null)
            StartCoroutine(RunAndFlag(bossRushController.PrepareFinalStageForIntroRoutine(bgmFadeOutDuration), () => backgroundDone = true));
        else
            backgroundDone = true;

        if (dimensionSE != null)
            AudioOneShotPool.Play(dimensionSE, dimensionSEVolume * MasterSEVolume, Vector3.zero, null, 0f);

        yield return DimensionRoutine();

        // ---------- ③ 暗転明け ----------
        float waited = 0f;
        while ((waited < blackHoldDuration || !backgroundDone) && waited < Mathf.Max(blackHoldDuration, maxBackgroundWait))
        {
            waited += Time.deltaTime;
            yield return null;
        }
        if (!backgroundDone) Debug.LogWarning("[Area10FinalIntro] 背景の切替が待ち時間内に終わらなかったため先へ進みます");

        // ボスを待機状態でスポーン（暗転中なので見えない）
        NeonDancerController.RequestIntroHold = true;
        if (spawnFormation != null) yield return spawnFormation();
        yield return null; // ボスのStart()（待機状態への移行）を待つ
        NeonDancerController.RequestIntroHold = false; // NeonDancerが出なかった場合に次へ持ち越さない
        boss = FindHeldBoss();
        Debug.Log($"[Area10FinalIntro] ボス={(boss != null ? boss.name : "なし（NeonDancer以外/未登録）")}");

        // Final Stageの背景をフェードイン（暗転した取り込み画面を透明にしていく）
        yield return FadeWarpAlpha(1f, 0f, backgroundFadeInDuration);
        HideDimensionVisuals();

        yield return new WaitForSeconds(delayBeforeStageIntro);

        // ---------- ③④ プレイヤー側の開始演出＋ボス側の鏡合わせ ----------
        if (replayStageIntro)
        {
            stageIntroController.OnIntroFloorFadeStarted += HandleFloorFadeStarted;
            stageIntroController.OnIntroPoseFadeStarted += HandlePoseFadeStarted;
            stageIntroController.OnIntroLightOn += HandleLightOn;
            try
            {
                yield return stageIntroController.StartCoroutine(stageIntroController.PlayIntro());
            }
            finally
            {
                stageIntroController.OnIntroFloorFadeStarted -= HandleFloorFadeStarted;
                stageIntroController.OnIntroPoseFadeStarted -= HandlePoseFadeStarted;
                stageIntroController.OnIntroLightOn -= HandleLightOn;
            }
        }

        // ---------- ⑤ Final Stageカットイン ----------
        yield return new WaitForSeconds(delayBeforeCutIn);
        if (cutIn != null)
            yield return StartCoroutine(cutIn.ShowCutIn(stageIndex));

        // ---------- ⑥ VS ----------
        VsBossPose vsPose = PickVsBossPose(areaConfig);
        if (vsIntro != null && areaConfig != null && vsPose != null)
            yield return StartCoroutine(vsIntro.PlayIntro(vsPose.sprite, areaConfig.vsBossNameSprite, areaConfig.vsBossThemeColor, vsPose.scale, vsPose.positionOffset));
        else
            Debug.Log("[Area10FinalIntro] VSのボス画像（Vs Boss Poses / Area10ConfigのVs Boss Sprite）が未設定のためVS演出は省略");

        // ---------- ゲーム開始 ----------
        if (stageIntroController != null) stageIntroController.OnCutInComplete();
        if (boss != null) boss.IntroRelease();
        boss = null;
        Debug.Log($"[Area10FinalIntro] 終了（ゲーム開始） frame={Time.frameCount}");
    }

    private static IEnumerator RunAndFlag(IEnumerator routine, System.Action onDone)
    {
        yield return routine;
        onDone?.Invoke();
    }

    private VsBossPose PickVsBossPose(AreaConfig areaConfig)
    {
        if (vsBossPoses != null)
        {
            int valid = 0;
            foreach (var p in vsBossPoses) if (p != null && p.sprite != null) valid++;
            if (valid > 0)
            {
                int pick = Random.Range(0, valid);
                foreach (var p in vsBossPoses)
                {
                    if (p == null || p.sprite == null) continue;
                    if (pick-- == 0) return p;
                }
            }
        }
        if (areaConfig == null || areaConfig.vsBossSprite == null) return null;
        return new VsBossPose { sprite = areaConfig.vsBossSprite, scale = areaConfig.vsBossScale, positionOffset = areaConfig.vsBossPositionOffset };
    }

    private static NeonDancerController FindHeldBoss()
    {
        foreach (var nd in FindObjectsByType<NeonDancerController>(FindObjectsSortMode.None))
            if (nd != null && nd.IsIntroHold) return nd;
        return null;
    }

    // プレイヤーのDancerを開始位置（自動移動の中心＝Respawn Point）へ戻す。非表示中に動かすのでワープは見えない
    private void MovePlayerDancerToCenter()
    {
        dancerCenterX = 0f;
        if (stageIntroController == null || stageIntroController.PixelDancerRenderer == null) return;
        Transform t = stageIntroController.PixelDancerRenderer.transform;
        var pdc = t.GetComponent<PixelDancerController>();
        dancerCenterX = pdc != null ? pdc.AutoMoveCenterXForIntro : t.position.x;
        t.position = new Vector3(dancerCenterX, t.position.y, t.position.z);
    }

    // ======================================================
    // ③④ ボス側の同期
    // ======================================================

    private void HandleFloorFadeStarted()
    {
        if (boss != null) boss.IntroFadeInFloor(stageIntroController.FloorFadeDuration);
    }

    private void HandlePoseFadeStarted()
    {
        if (boss == null) return;
        SpriteRenderer pose   = stageIntroController.StartPoseRenderer;
        SpriteRenderer floor  = stageIntroController.FloorRenderer;
        SpriteRenderer dancer = stageIntroController.PixelDancerRenderer;
        Sprite silhouette = stageIntroController.CurrentStartPoseSilhouette;
        if (pose == null || silhouette == null || dancer == null) return;

        // プレイヤー側の「ダンス画像に対するポーズ画像の大きさ」「Dancer中心からのX位置」「Floor上端からの高さ」を
        // ダンス画像の大きさを1とした単位で測り、ボス側はそれを左右反転して同じ比率で再現する
        float dancerScale = Mathf.Max(0.0001f, Mathf.Abs(dancer.transform.lossyScale.y));
        float poseScale = Mathf.Abs(pose.transform.lossyScale.y);
        float poseBottom = pose.transform.position.y + silhouette.bounds.min.y * poseScale;
        float floorTop = (floor != null && floor.sprite != null)
            ? floor.transform.position.y + floor.sprite.bounds.max.y * Mathf.Abs(floor.transform.lossyScale.y)
            : poseBottom;

        float scaleFactor = poseScale / dancerScale;
        float xOffset = -(pose.transform.position.x - dancerCenterX) / dancerScale;
        float gap = (poseBottom - floorTop) / dancerScale;
        boss.IntroShowPose(silhouette, !pose.flipX, scaleFactor, xOffset, gap, stageIntroController.PoseFadeDuration);
    }

    private void HandleLightOn()
    {
        if (boss != null) boss.IntroLightOn(stageIntroController.CurrentStartPoseFull, stageIntroController.SpotlightFadeDuration);
    }

    // ======================================================
    // ② 画面の取り込み
    // ======================================================

    private IEnumerator CaptureScreen()
    {
        Camera cam = targetCamera != null ? targetCamera : Camera.main;
        if (cam == null || warpRenderer == null || warpMaterial == null)
        {
            Debug.LogWarning("[Area10FinalIntro] カメラまたはWarp Rendererが未設定のため、画面の取り込みを省略します");
            yield break;
        }

        ReleaseCapture();
        captureRT = RenderTexture.GetTemporary(Screen.width, Screen.height, 24, RenderTextureFormat.ARGB32);
        bool flipY = false;

        // UI（HUDのCanvas＝UIレイヤー）を除いたゲーム画面だけをカメラで描画して取り込む
        var request = new UniversalRenderPipeline.SingleCameraRequest();
        if (RenderPipeline.SupportsRenderRequest(cam, request))
        {
            request.destination = captureRT;
            int uiLayer = LayerMask.NameToLayer("UI");
            int originalMask = cam.cullingMask;
            if (uiLayer >= 0) cam.cullingMask = originalMask & ~(1 << uiLayer);
            try { RenderPipeline.SubmitRenderRequest(cam, request); }
            finally { cam.cullingMask = originalMask; }
        }
        else
        {
            // 予備：画面全体（UI込み）を取り込む
            Debug.LogWarning("[Area10FinalIntro] カメラ単体の描画に対応していないため、画面全体（UI込み）を取り込みます");
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshotIntoRenderTexture(captureRT);
            flipY = SystemInfo.graphicsUVStartsAtTop;
        }

        warpMaterial.SetTexture(CaptureTexId, captureRT);
        warpMaterial.SetFloat(FlipYId, flipY ? 1f : 0f);
        warpMaterial.SetFloat(AspectId, cam.aspect);
        int rows = Mathf.Max(1, blockNoiseRows);
        warpMaterial.SetVector(BlockGridId, new Vector4(Mathf.Max(1f, Mathf.Round(rows * cam.aspect)), rows, 0f, 0f));
        warpMaterial.SetFloat(BlockCoverageId, blockNoiseMaxCoverage);
        warpMaterial.SetFloat(BlockOpacityId, blockNoiseOpacity);
        SetWarpParams(0f);

        // カメラの表示範囲ぴったりに合わせる
        Transform wt = warpRenderer.transform;
        Vector3 cp = cam.transform.position;
        wt.position = new Vector3(cp.x, cp.y, wt.position.z);
        wt.rotation = Quaternion.identity;
        float h = cam.orthographicSize * 2f;
        float w = h * cam.aspect;
        Vector2 spriteSize = warpRenderer.sprite != null ? (Vector2)warpRenderer.sprite.bounds.size : Vector2.one;
        wt.localScale = new Vector3(w / Mathf.Max(0.0001f, spriteSize.x), h / Mathf.Max(0.0001f, spriteSize.y), 1f);

        SetWarpAlpha(1f);
        warpRenderer.gameObject.SetActive(true);
    }

    private void ReleaseCapture()
    {
        if (captureRT != null)
        {
            if (warpMaterial != null) warpMaterial.SetTexture(CaptureTexId, null);
            RenderTexture.ReleaseTemporary(captureRT);
            captureRT = null;
        }
    }

    // ======================================================
    // ② 次元移動のアニメーション
    // ======================================================

    private IEnumerator DimensionRoutine()
    {
        Camera cam = targetCamera != null ? targetCamera : Camera.main;
        Vector3 center = cam != null ? new Vector3(cam.transform.position.x, cam.transform.position.y, 0f) : Vector3.zero;
        float halfH = cam != null ? cam.orthographicSize : 5f;
        float halfW = cam != null ? halfH * cam.aspect : halfH * 1.7778f;
        float diag = Mathf.Sqrt(halfW * halfW + halfH * halfH) * 2f;

        float wormholeSpriteSize = 1f;
        if (wormholeTintedLayers != null && wormholeTintedLayers.Length > 0 && wormholeTintedLayers[0] != null && wormholeTintedLayers[0].sprite != null)
            wormholeSpriteSize = Mathf.Max(0.0001f, wormholeTintedLayers[0].sprite.bounds.size.x);
        float voidSpriteSize = (wormholeVoid != null && wormholeVoid.sprite != null) ? Mathf.Max(0.0001f, wormholeVoid.sprite.bounds.size.x) : 1f;

        if (wormholeRoot != null)
        {
            wormholeRoot.position = center;
            wormholeRoot.localScale = Vector3.zero;
            wormholeRoot.gameObject.SetActive(true);
        }
        if (sparkleParticles != null)
        {
            sparkleParticles.Clear();
            sparkleParticles.Play();
        }

        float elapsed = 0f;
        float jitterTimer = 0f;
        float spin = 0f;
        float emitCarry = 0f;
        float duration = Mathf.Max(0.01f, dimensionDuration);
        while (elapsed < duration)
        {
            float dt = Time.deltaTime;
            elapsed += dt;
            float u = Mathf.Clamp01(elapsed / duration);

            jitterTimer -= dt;
            if (jitterTimer <= 0f)
            {
                jitterTimer = glitchJitterInterval;
                if (warpMaterial != null) warpMaterial.SetFloat(SeedId, Random.Range(0f, 100f));
            }
            SetWarpParams(u);

            // 巨大ワームホール
            spin += wormholeSpinSpeed * dt;
            if (wormholeRoot != null)
            {
                float size = diag * wormholeCoverRatio * Mathf.Max(0f, wormholeScaleCurve.Evaluate(u));
                wormholeRoot.localScale = Vector3.one * (size / wormholeSpriteSize);
                wormholeRoot.localRotation = Quaternion.Euler(0f, 0f, -spin);
            }
            if (wormholeCounterSpinLayers != null)
                foreach (var t in wormholeCounterSpinLayers)
                    if (t != null) t.localRotation = Quaternion.Euler(0f, 0f, spin * (1f + wormholeCounterSpinRatio)); // 親の回転を打ち消して逆回転
            float a = Mathf.Clamp01(wormholeAlphaCurve.Evaluate(u));
            Color cycle = CycleColor(elapsed * wormholeColorCycleSpeed);
            if (wormholeTintedLayers != null)
                foreach (var sr in wormholeTintedLayers)
                    if (sr != null) sr.color = new Color(cycle.r, cycle.g, cycle.b, a);
            if (wormholeVoid != null)
            {
                float vs = Mathf.Max(0f, voidScaleCurve.Evaluate(u)) * (wormholeSpriteSize / voidSpriteSize);
                wormholeVoid.transform.localScale = Vector3.one * vs;
                wormholeVoid.color = new Color(0f, 0f, 0f, a);
            }

            // 9色の光の粒
            if (sparkleParticles != null)
            {
                emitCarry += Mathf.Max(0f, sparkleRateCurve.Evaluate(u)) * dt;
                int count = Mathf.FloorToInt(emitCarry);
                emitCarry -= count;
                for (int i = 0; i < count; i++) EmitSparkle(center, halfW, halfH);
            }

            yield return null;
        }
        SetWarpParams(1f);
    }

    private void EmitSparkle(Vector3 center, float halfW, float halfH)
    {
        Vector3 pos = center + new Vector3(Random.Range(-halfW, halfW), Random.Range(-halfH, halfH), 0f);
        Vector3 toCenter = center - pos;
        float life = Random.Range(Mathf.Min(sparkleLifetime.x, sparkleLifetime.y), Mathf.Max(sparkleLifetime.x, sparkleLifetime.y));
        Vector3 inward = toCenter / Mathf.Max(0.05f, life);
        Vector3 tangent = new Vector3(-toCenter.y, toCenter.x, 0f) / Mathf.Max(0.05f, life) * sparkleSwirl;

        var ep = new ParticleSystem.EmitParams
        {
            position = pos,
            velocity = inward + tangent,
            startLifetime = life,
            startSize = Random.Range(Mathf.Min(sparkleSize.x, sparkleSize.y), Mathf.Max(sparkleSize.x, sparkleSize.y)),
            startColor = AreaColors[Random.Range(0, AreaColors.Length)],
            applyShapeToPosition = false,
        };
        sparkleParticles.Emit(ep, 1);
    }

    private static Color CycleColor(float t)
    {
        int n = AreaColors.Length;
        float f = Mathf.Repeat(t, n);
        int i = Mathf.FloorToInt(f);
        return Color.Lerp(AreaColors[i % n], AreaColors[(i + 1) % n], f - i);
    }

    private void SetWarpParams(float u)
    {
        if (warpMaterial == null) return;
        warpMaterial.SetFloat(SuckId, suckCurve.Evaluate(u));
        warpMaterial.SetFloat(TwistId, twistCurve.Evaluate(u));
        warpMaterial.SetFloat(GlitchId, Mathf.Clamp01(glitchCurve.Evaluate(u)));
        warpMaterial.SetFloat(RgbSplitId, Mathf.Clamp01(rgbSplitCurve.Evaluate(u)));
        warpMaterial.SetFloat(ScanlineId, Mathf.Clamp01(scanlineCurve.Evaluate(u)));
        warpMaterial.SetFloat(BlockNoiseId, Mathf.Clamp01(blockNoiseCurve.Evaluate(u)));
        warpMaterial.SetFloat(DarkenId, Mathf.Clamp01(darkenCurve.Evaluate(u)));
    }

    private void SetWarpAlpha(float a)
    {
        if (warpRenderer == null) return;
        Color c = warpRenderer.color; c.a = a; warpRenderer.color = c;
    }

    private IEnumerator FadeWarpAlpha(float from, float to, float duration)
    {
        if (warpRenderer == null || !warpRenderer.gameObject.activeSelf) yield break;
        // 暗転した状態（真っ黒）のまま透明にしていく＝背景がフェードインして見える
        if (warpMaterial != null) warpMaterial.SetFloat(DarkenId, 1f);
        if (wormholeRoot != null) wormholeRoot.gameObject.SetActive(false);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            SetWarpAlpha(Mathf.Lerp(from, to, elapsed / duration));
            yield return null;
        }
        SetWarpAlpha(to);
    }

    private void HideDimensionVisuals()
    {
        if (warpRenderer != null) warpRenderer.gameObject.SetActive(false);
        if (wormholeRoot != null) wormholeRoot.gameObject.SetActive(false);
        if (sparkleParticles != null) sparkleParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ReleaseCapture();
    }
}
