using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Area各Stage1開始時のイントロ演出を管理する。
/// EnemySpawner.WaveSystemRoutineからPlayIntro()が呼び出される。
/// </summary>
[DefaultExecutionOrder(100)]
public class StageIntroController : MonoBehaviour
{
    [Header("── Debug ──")]
    [Tooltip("ONにするとイントロをスキップしてすぐカットインへ")]
    [SerializeField] private bool skipIntro = false;
    [Tooltip("-1でランダム。0以上の値を指定するとそのインデックスのポーズを固定表示（テスト用）")]
    [SerializeField] private int debugPoseIndex = -1;

    [Header("── Step 2: Floor ──")]
    [SerializeField] private SpriteRenderer floorRenderer;
    [SerializeField] private float delayStep2 = 0.4f;
    [SerializeField] private float fadeStep2  = 0.8f;

    [Header("── Step 3: StartPose ──")]
    [SerializeField] private SpriteRenderer startPoseRenderer;
    [Tooltip("ランダム選択するシルエットスプライト一覧")]
    [SerializeField] private Sprite[] startPoseSilhouettes;
    [Tooltip("ポーズごとのX位置オフセット（startPoseSilhouettesと同インデックス）")]
    [SerializeField] private float[] poseOffsetX;
    [Tooltip("ポーズごとのY位置オフセット（startPoseSilhouettesと同インデックス）")]
    [SerializeField] private float[] poseOffsetY;
    [SerializeField] private float delayStep3 = 0.5f;
    [SerializeField] private float fadeStep3  = 0.8f;

    [Header("── Step 4: Partner ──")]
    [Tooltip("PartnerLeft の SpriteRenderer")]
    [SerializeField] private SpriteRenderer partnerRendererLeft;
    [Tooltip("PartnerRight の SpriteRenderer")]
    [SerializeField] private SpriteRenderer partnerRendererRight;
    [Tooltip("半透明値（弾が見えるよう低めに）")]
    [SerializeField, Range(0f, 1f)] private float partnerTargetAlpha = 0.55f;
    [Tooltip("X/Y方向の揺れ幅（Unity単位）")]
    [SerializeField] private Vector2 floatAmplitude = new Vector2(0.04f, 0.08f);
    [Tooltip("X/Y方向の揺れ周期（Hz）")]
    [SerializeField] private Vector2 floatFrequency = new Vector2(0.7f, 1.05f);

    [Header("── Step 5: Spotlight ──")]
    [Tooltip("PartnerLeft > BeamRoot の Transform")]
    [SerializeField] private Transform beamRootLeft;
    [Tooltip("PartnerRight > BeamRoot の Transform")]
    [SerializeField] private Transform beamRootRight;
    [Tooltip("Partnerの中心からBeamRootまでのY距離（ローカル）。顔の高さに合わせて調整")]
    [SerializeField] private float beamFaceLocalY = -2.55f;
    [Tooltip("PartnerLeft > BeamRoot > BeamSr の SpriteRenderer")]
    [SerializeField] private SpriteRenderer beamSrLeft;
    [Tooltip("PartnerRight > BeamRoot > BeamSr の SpriteRenderer")]
    [SerializeField] private SpriteRenderer beamSrRight;
    [Tooltip("PixelDancer足元の FloorSpot GO の SpriteRenderer")]
    [SerializeField] private SpriteRenderer floorSpotRenderer;
    [SerializeField, Range(0f, 1f)] private float floorSpotMinAlpha = 0.5f;
    [SerializeField, Range(0f, 1f)] private float floorSpotMaxAlpha = 0.8f;
    [SerializeField, Range(0f, 1f)] private float introBeamAlpha      = 0.55f;
    [SerializeField, Range(0f, 1f)] private float introFloorSpotAlpha = 0.8f;
    [Tooltip("ビームの照射距離倍率。小さくするほど遠くまで照射される（0.88=PixelDancer位置でちょうど消える）")]
    [SerializeField, Range(0.3f, 1.0f)] private float beamReachRatio = 0.88f;
    [SerializeField] private float delayStep5        = 0.5f;
    [SerializeField] private float beamFadeInDuration = 0.3f;
    [Tooltip("ダンス開始時のビーム遷移時間（秒）")]
    [SerializeField] private float beamTransitionDuration = 0.3f;
    [SerializeField] private AudioClip lightOnSE;
    [SerializeField, Range(0f, 1f)] private float lightOnSEVolume = 1f;
    [SerializeField] private AudioSource seSource;

    [Header("── Step 6: Silhouette → Full ──")]
    [Tooltip("startPoseSilhouettesと同インデックスの通常スプライト")]
    [SerializeField] private Sprite[] startPosesFull;

    [Header("── Beat Pulse（ダンス開始後） ──")]
    [Tooltip("パルス周波数（Hz）。1.2 ≈ 120BPM÷2")]
    [SerializeField] private float beatPulseFrequency = 1.2f;
    [SerializeField, Range(0f, 1f)] private float beatPulseMinAlpha = 0.35f;
    [SerializeField, Range(0f, 1f)] private float beatPulseMaxAlpha = 0.55f;

    [Header("── Vignette ──")]
    [SerializeField] private Image vignetteOverlay;
    [SerializeField, Range(0f, 1f)] private float vignetteAlpha = 0.45f;
    [SerializeField] private float vignetteFadeOutDuration    = 1.5f;
    [SerializeField] private float vignetteOutDelayAfterBGM   = 0.5f;

    [Header("── Step 7: BGM ──")]
    [SerializeField] private GameplayBgmRandomPlayer bgmPlayer;
    [SerializeField] private float delayStep7 = 0.3f;

    [Header("── PixelDancer ──")]
    [Tooltip("イントロ中は非表示にし、カットイン完了時に表示する")]
    [SerializeField] private SpriteRenderer pixelDancerRenderer;

    [System.Serializable]
    public class FinishFrame
    {
        public Sprite sprite;
        public float offsetX  = 0f;
        public float offsetY  = 0f;
        public float duration = 0.083f;
    }

    [Header("── Area Complete ──")]
    [Tooltip("Finish_1〜11のフレーム設定（Sprite/Offset/Duration）")]
    [NonReorderable]
    [SerializeField] private FinishFrame[] finishFrames;
    [Tooltip("Stage3クリア後、タイムスローが始まるまでの待機（秒）")]
    [SerializeField] private float delayBeforeTimeSlow    = 0f;
    [Tooltip("タイムスロー中の Time.timeScale 倍率")]
    [SerializeField, Range(0.01f, 1f)] private float timeSlowScale = 0.15f;
    [Tooltip("タイムスロー継続時間（実時間・秒）")]
    [SerializeField] private float timeSlowRealDuration   = 1.5f;
    [Tooltip("タイムスロー終了後、Finishアニメ開始までの待機（秒）")]
    [SerializeField] private float delayBeforeFinishAnim  = 2f;
    [Tooltip("Finishアニメ終了後、テキスト表示開始までの待機（秒）")]
    [SerializeField] private float delayBeforeText        = 1f;
    [Tooltip("ビームフラッシュを発火するフレームインデックス（0始まり）")]
    [SerializeField] private int   beamFlashFrameIndex    = 9;
    [Tooltip("ビーム収束後の最小alpha")]
    [SerializeField, Range(0f, 1f)] private float beamConvergeTargetAlpha = 0.05f;
    [Tooltip("フロアスポット収束後のalpha")]
    [SerializeField, Range(0f, 1f)] private float floorSpotConvergeTargetAlpha = 0.05f;
    [Tooltip("ビームフラッシュの持続時間（秒）。-1で永続（ビートパルス再開しない）")]
    [SerializeField] private float beamFlashDuration      = 0.15f;
    [Tooltip("フラッシュ後にbeamConvergeTargetAlphaまで収束する時間（秒）")]
    [SerializeField] private float beamConvergeDuration   = 0.5f;
    [Tooltip("Finishアニメの基準Y位置を固定する（ダンス中に空中にいても地面から開始させる）")]
    [SerializeField] private bool useFixedFinishY = false;
    [Tooltip("Finishアニメ開始時のY位置（useFixedFinishY=trueのとき使用）")]
    [SerializeField] private float fixedFinishY = 0f;
    [Tooltip("AreaCompleteテキスト演出UI")]
    [SerializeField] private AreaCompleteTextUI areaCompleteTextUI;

    // ── runtime state ──
    private int     selectedPoseIndex  = -1;
    private Vector3 basePosePos;
    private bool    isTracking        = false;
    private bool    isPulsing         = false;
    private Vector3 beamSmoothFrom;
    private float   beamSmoothTimer   = 0f;
    private float   floorSpotFixedY   = 0f;

    private static float MasterSEVolume => SoundSettingsManager.Instance != null ? SoundSettingsManager.Instance.SEVolume : 1f;

    // ──────────────────────────────────────────
    // Lifecycle
    // ──────────────────────────────────────────

    private void OnEnable()
    {
        EnemySpawner.OnFinalBossDefeated += OnFinalBossDefeated;
    }

    private void OnDisable()
    {
        EnemySpawner.OnFinalBossDefeated -= OnFinalBossDefeated;
    }

    private void OnFinalBossDefeated()
    {
        SessionStats.StopTimer();
        StartCoroutine(DoTimeSlow());
    }

    private void Start()
    {
        SetupInitialState();
    }

    private void Update()
    {
        if (isPulsing) UpdateBeatPulse();
    }

    private void LateUpdate()
    {
        // PixelDancerController.LateUpdate（AutoMove）の後に実行
        if (!isTracking || pixelDancerRenderer == null) return;

        if (isPulsing) beamSmoothTimer += Time.deltaTime;

        Vector3 target = pixelDancerRenderer.transform.position;

        // 1. ビーム方向 + Partner回転（起点を顔位置に毎フレーム固定）
        Transform partnerTransL = partnerRendererLeft  != null ? partnerRendererLeft.transform  : null;
        Transform partnerTransR = partnerRendererRight != null ? partnerRendererRight.transform : null;
        Vector3 originL = CalcBeamOrigin(partnerTransL, beamRootLeft);
        Vector3 originR = CalcBeamOrigin(partnerTransR, beamRootRight);
        if (beamRootLeft  != null) PointBeamAt(partnerTransL, beamRootLeft,  target, originL);
        if (beamRootRight != null) PointBeamAt(partnerTransR, beamRootRight, target, originR);

        // 2. スケール（顔の固定起点から距離計算、均一スケールで先端を伸ばす）
        if (beamRootLeft  != null && beamSrLeft  != null) InitBeamScale(beamRootLeft,  beamSrLeft,  target, originL);
        if (beamRootRight != null && beamSrRight != null) InitBeamScale(beamRootRight, beamSrRight, target, originR);

        // 4. FloorSpot: XのみPixelDancerに追従、YはfloorSpotFixedYで床面固定
        if (floorSpotRenderer != null && floorSpotRenderer.gameObject.activeSelf)
            floorSpotRenderer.transform.position = new Vector3(target.x, floorSpotFixedY, target.z);
    }

    // ──────────────────────────────────────────
    // メインシーケンス（EnemySpawnerから呼び出される）
    // ──────────────────────────────────────────

    public IEnumerator PlayIntro()
    {
        if (skipIntro) yield break;

        yield return null; // 全Start()完了を保証
        SetupInitialState();

        BackgroundManager.Instance?.ActivateAreaParticle();

        // Vignette 即表示
        if (vignetteOverlay != null)
            SetImageAlpha(vignetteOverlay, vignetteAlpha);

        // Step 2: Floor フェードイン
        yield return new WaitForSeconds(delayStep2);
        if (floorRenderer != null)
            yield return StartCoroutine(FadeInSR(floorRenderer, 1f, fadeStep2));

        // Step 3: StartPose（シルエット）+ Partner 同時フェードイン
        yield return new WaitForSeconds(delayStep3);
        if (startPoseRenderer != null && startPoseSilhouettes != null && startPoseSilhouettes.Length > 0)
        {
            selectedPoseIndex = (debugPoseIndex >= 0 && debugPoseIndex < startPoseSilhouettes.Length)
                ? debugPoseIndex
                : Random.Range(0, startPoseSilhouettes.Length);
            startPoseRenderer.sprite = startPoseSilhouettes[selectedPoseIndex];
            float offsetX = (poseOffsetX != null && selectedPoseIndex < poseOffsetX.Length)
                ? poseOffsetX[selectedPoseIndex] : 0f;
            float offsetY = (poseOffsetY != null && selectedPoseIndex < poseOffsetY.Length)
                ? poseOffsetY[selectedPoseIndex] : 0f;
            startPoseRenderer.transform.position = basePosePos + new Vector3(offsetX, offsetY, 0f);
            startPoseRenderer.gameObject.SetActive(true);
        }
        if (partnerRendererLeft != null)
        {
            partnerRendererLeft.gameObject.SetActive(true);
            StartCoroutine(FadeInSR(partnerRendererLeft, partnerTargetAlpha, fadeStep3));
        }
        if (partnerRendererRight != null)
        {
            partnerRendererRight.gameObject.SetActive(true);
            StartCoroutine(FadeInSR(partnerRendererRight, partnerTargetAlpha, fadeStep3));
        }
        if (startPoseRenderer != null)
            yield return StartCoroutine(FadeInSR(startPoseRenderer, 1f, fadeStep3));

        // Step 5: スポットライト点灯
        yield return new WaitForSeconds(delayStep5);
        if (lightOnSE != null && seSource != null)
            seSource.PlayOneShot(lightOnSE, lightOnSEVolume * MasterSEVolume);

        // SE直後: シルエット → 通常スプライト切り替え
        if (startPoseRenderer != null
            && startPosesFull != null
            && selectedPoseIndex >= 0
            && selectedPoseIndex < startPosesFull.Length
            && startPosesFull[selectedPoseIndex] != null)
        {
            startPoseRenderer.sprite = startPosesFull[selectedPoseIndex];
        }

        yield return StartCoroutine(ActivateSpotlights());

        // Step 7: BGM 開始
        yield return new WaitForSeconds(delayStep7);
        if (bgmPlayer != null)
            bgmPlayer.PlayRandom();

        // Vignette フェードアウト（非ブロッキング）
        if (vignetteOverlay != null)
            StartCoroutine(FadeOutImage(vignetteOverlay, vignetteFadeOutDuration, vignetteOutDelayAfterBGM));
    }

    /// <summary>カットイン完了時にEnemySpawnerから呼ばれる。StartPose非表示・PixelDancer有効化・ビートパルス開始。</summary>
    public void OnCutInComplete()
    {
        if (startPoseRenderer != null) startPoseRenderer.gameObject.SetActive(false);

        if (pixelDancerRenderer != null)
        {
            // アニメ開始前の位置を記録してスムーズブレンドの起点にする
            beamSmoothFrom = pixelDancerRenderer.transform.position;
            beamSmoothTimer = 0f;
            pixelDancerRenderer.gameObject.SetActive(true);
        }

        isPulsing = true;
    }

    /// <summary>
    /// floorRendererはSetupInitialState()でalpha=0にされ、通常はPlayIntro()内のフェードインで戻る仕様。
    /// PlayIntro()自体を省略するケース（Tutorial等）向けに、フロアの表示だけ即座に戻す
    /// </summary>
    public void RevealFloorInstant()
    {
        if (floorRenderer != null) SetSRAlpha(floorRenderer, 1f);
    }

    // ──────────────────────────────────────────
    // Spotlight
    // ──────────────────────────────────────────

    /// <summary>スケール自動計算 → 追跡開始 → フェードイン</summary>
    private IEnumerator ActivateSpotlights()
    {
        if (pixelDancerRenderer != null)
        {
            Vector3 target = pixelDancerRenderer.transform.position;
            Transform ptL = partnerRendererLeft  != null ? partnerRendererLeft.transform  : null;
            Transform ptR = partnerRendererRight != null ? partnerRendererRight.transform : null;
            Vector3 originL = CalcBeamOrigin(ptL, beamRootLeft);
            Vector3 originR = CalcBeamOrigin(ptR, beamRootRight);
            // RotationをInitBeamScaleより先に確定させる（beamRoot.rotationを使用するため）
            if (beamRootLeft  != null) PointBeamAt(ptL, beamRootLeft,  target, originL);
            if (beamRootRight != null) PointBeamAt(ptR, beamRootRight, target, originR);
            InitBeamScale(beamRootLeft,  beamSrLeft,  target, originL);
            InitBeamScale(beamRootRight, beamSrRight, target, originR);
            InitFloorSpot(target);
        }

        // 追跡開始（フェードイン中から常にPixelDancerを向く）
        isTracking = true;

        // ビームとフロアスポットを同時フェードイン
        if (beamSrLeft  != null) StartCoroutine(FadeInSR(beamSrLeft,  introBeamAlpha, beamFadeInDuration));
        if (beamSrRight != null) StartCoroutine(FadeInSR(beamSrRight, introBeamAlpha, beamFadeInDuration));
        if (floorSpotRenderer != null)
        {
            floorSpotRenderer.gameObject.SetActive(true); // SetActive(false)から復帰
            SetSRAlpha(floorSpotRenderer, 0f);
            StartCoroutine(FadeInSR(floorSpotRenderer, introFloorSpotAlpha, beamFadeInDuration));
        }

        yield return new WaitForSeconds(beamFadeInDuration);
    }

    /// <summary>顔の固定起点からPixelDancerまでの距離に合わせてビームを伸ばす。スプライト上端を常にoriginに固定する</summary>
    private void InitBeamScale(Transform beamRoot, SpriteRenderer beamSr, Vector3 target, Vector3 origin)
    {
        if (beamRoot == null || beamSr == null || beamSr.sprite == null) return;
        float dist = Vector3.Distance(origin, target);
        float spriteH = beamSr.sprite.bounds.size.y;
        float effectiveH = spriteH * beamReachRatio;
        if (effectiveH <= 0f) return;
        float scaleY = dist / effectiveH;

        beamRoot.localScale = Vector3.one;
        beamSr.transform.localScale = new Vector3(scaleY, scaleY, 1f);

        // bounds.max.y = pivotから上端までのオフセット（sprite local units）
        // ワールド座標で「pivotの位置 = origin - 上端オフセット」となるよう直接設定
        // → スプライト上端（光源）が常にoriginに固定される（pivot位置・親Transformに依存しない）
        float topOffset = beamSr.sprite.bounds.max.y * scaleY;
        beamSr.transform.position = origin - beamRoot.rotation * new Vector3(0f, topOffset, 0f);
    }

    /// <summary>フロアスポットをPixelDancer足元に配置し、ビーム幅に合わせてサイズを自動設定する</summary>
    private void InitFloorSpot(Vector3 target)
    {
        if (floorSpotRenderer == null || floorSpotRenderer.sprite == null) return;

        // PixelDancerスプライトの下端（足元）に配置する
        float halfH = (pixelDancerRenderer != null && pixelDancerRenderer.sprite != null)
            ? pixelDancerRenderer.sprite.bounds.extents.y
            : 0f;
        Vector3 feetPos = new Vector3(target.x, target.y - halfH, target.z);
        floorSpotFixedY = feetPos.y; // 床面Y座標を固定（以降LateUpdateでXのみ追従）
        floorSpotRenderer.transform.position = feetPos;

        // スポット直径 ≈ 平均照射距離 × sin(28°) × 2
        float distL = beamRootLeft  != null ? Vector3.Distance(beamRootLeft.position,  target) : 0f;
        float distR = beamRootRight != null ? Vector3.Distance(beamRootRight.position, target) : 0f;
        int   count = (distL > 0f ? 1 : 0) + (distR > 0f ? 1 : 0);
        if (count == 0) return;
        float avgDist      = (distL + distR) / count;
        float spotDiameter = avgDist * Mathf.Sin(28f * Mathf.Deg2Rad) * 2f;
        float spriteSizeX  = floorSpotRenderer.sprite.bounds.size.x;
        if (spriteSizeX <= 0f) return;
        float scale = spotDiameter / spriteSizeX;
        floorSpotRenderer.transform.localScale = new Vector3(scale, scale, 1f);
    }

    /// <summary>Partnerのワールド座標 + beamFaceLocalY でビーム起点を計算</summary>
    private Vector3 CalcBeamOrigin(Transform partnerTrans, Transform beamRoot)
    {
        if (partnerTrans != null)
            return new Vector3(partnerTrans.position.x, partnerTrans.position.y + beamFaceLocalY, partnerTrans.position.z);
        return beamRoot != null ? beamRoot.position : Vector3.zero;
    }

    /// <summary>BeamRootとPartnerをtargetPos方向へ向け、BeamRootを顔の位置に固定する</summary>
    private void PointBeamAt(Transform partnerTrans, Transform beamRoot, Vector3 target, Vector3 origin)
    {
        Vector3 effectiveTarget;
        if (isPulsing && beamSmoothTimer < beamTransitionDuration)
        {
            float t = Mathf.Clamp01(beamSmoothTimer / beamTransitionDuration);
            t = t * t * (3f - 2f * t);
            effectiveTarget = Vector3.Lerp(beamSmoothFrom, target, t);
        }
        else
        {
            effectiveTarget = target;
        }

        Vector3    dir   = effectiveTarget - origin;
        float      angle = Mathf.Atan2(dir.x, -dir.y) * Mathf.Rad2Deg;
        Quaternion rot   = Quaternion.Euler(0f, 0f, angle);

        if (partnerTrans != null) partnerTrans.rotation = rot;
        // Partnerの回転でBeamRootが動いてしまうため、毎フレーム顔の位置に強制復元してから回転
        beamRoot.position = origin;
        beamRoot.rotation = rot;
    }

    /// <summary>ビームのalphaをsin波でゆっくり揺らす（beat pulse）</summary>
    private void UpdateBeatPulse()
    {
        float pulse = (Mathf.Sin(Time.time * beatPulseFrequency * Mathf.PI * 2f) + 1f) * 0.5f;
        float alpha = Mathf.Lerp(beatPulseMinAlpha, beatPulseMaxAlpha, pulse);
        if (beamSrLeft        != null) SetSRAlpha(beamSrLeft,        alpha);
        if (beamSrRight       != null) SetSRAlpha(beamSrRight,       alpha);
        if (floorSpotRenderer != null) SetSRAlpha(floorSpotRenderer, Mathf.Lerp(floorSpotMinAlpha, floorSpotMaxAlpha, pulse));
    }

    // ──────────────────────────────────────────
    // 初期化
    // ──────────────────────────────────────────

    private void SetupInitialState()
    {
        if (floorRenderer != null) SetSRAlpha(floorRenderer, 0f);

        if (startPoseRenderer != null)
        {
            basePosePos = startPoseRenderer.transform.position;
            SetSRAlpha(startPoseRenderer, 0f);
            startPoseRenderer.gameObject.SetActive(false);
        }
        if (partnerRendererLeft != null)
        {
            partnerRendererLeft.gameObject.SetActive(false);
            SetSRAlpha(partnerRendererLeft, 0f);
        }
        if (partnerRendererRight != null)
        {
            partnerRendererRight.gameObject.SetActive(false);
            SetSRAlpha(partnerRendererRight, 0f);
        }

        if (beamSrLeft  != null) SetSRAlpha(beamSrLeft,  0f);
        if (beamSrRight != null) SetSRAlpha(beamSrRight, 0f);
        // FloorSpotはPartnerの子ではないのでSetActive(false)で確実に非表示にする
        if (floorSpotRenderer != null)
        {
            SetSRAlpha(floorSpotRenderer, 0f);
            floorSpotRenderer.gameObject.SetActive(false);
        }

        if (vignetteOverlay     != null) SetImageAlpha(vignetteOverlay, 0f);
        if (pixelDancerRenderer != null) pixelDancerRenderer.gameObject.SetActive(false);

        isTracking        = false;
        isPulsing         = false;
        beamSmoothTimer   = 0f;
        selectedPoseIndex = -1;
    }

    // ──────────────────────────────────────────
    // 外部公開
    // ──────────────────────────────────────────

    public void SetSpotlightsVisible(bool visible)
    {
        if (beamSrLeft  != null) SetSRAlpha(beamSrLeft,  visible ? introBeamAlpha : 0f);
        if (beamSrRight != null) SetSRAlpha(beamSrRight, visible ? introBeamAlpha : 0f);
        if (floorSpotRenderer != null)
        {
            if (visible)
            {
                floorSpotRenderer.gameObject.SetActive(true);
                SetSRAlpha(floorSpotRenderer, introFloorSpotAlpha);
            }
            else
            {
                SetSRAlpha(floorSpotRenderer, 0f);
                floorSpotRenderer.gameObject.SetActive(false);
            }
        }
    }

    // ──────────────────────────────────────────
    // ヘルパー
    // ──────────────────────────────────────────

    private IEnumerator FadeInSR(SpriteRenderer sr, float targetAlpha, float duration)
    {
        Color c = sr.color;
        float startAlpha = c.a;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            c.a = Mathf.Lerp(startAlpha, targetAlpha, elapsed / duration);
            sr.color = c;
            yield return null;
        }
        c.a = targetAlpha;
        sr.color = c;
    }

    private IEnumerator FadeOutImage(Image img, float duration, float delay = 0f)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);
        Color c = img.color;
        float startAlpha = c.a;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            c.a = Mathf.Lerp(startAlpha, 0f, elapsed / duration);
            img.color = c;
            yield return null;
        }
        c.a = 0f;
        img.color = c;
    }

    private void SetSRAlpha(SpriteRenderer sr, float a)
    {
        Color c = sr.color; c.a = a; sr.color = c;
    }

    private void SetImageAlpha(Image img, float a)
    {
        Color c = img.color; c.a = a; img.color = c;
    }

    // ──────────────────────────────────────────
    // Area Complete（Stage3クリア後演出）
    // ──────────────────────────────────────────

    /// <summary>ボス撃破エフェクト中にEnemySpawnerから呼ばれる。タイムスローのみ担当。</summary>
    public IEnumerator DoTimeSlow()
    {
        if (delayBeforeTimeSlow > 0f)
            yield return new WaitForSeconds(delayBeforeTimeSlow);
        Time.timeScale = timeSlowScale;
        yield return new WaitForSecondsRealtime(timeSlowRealDuration);
        Time.timeScale = 1f;
    }

    [ContextMenu("Debug: Simulate Jump Inverted → Finish")]
    private void DebugSimulateInvertedFinish()
    {
        if (pixelDancerRenderer != null)
        {
            pixelDancerRenderer.flipX = false;
            pixelDancerRenderer.flipY = true;
        }
        StartCoroutine(PlayAreaComplete());
    }

    public IEnumerator PlayAreaComplete()
    {
        if (pixelDancerRenderer != null && !pixelDancerRenderer.gameObject.activeSelf)
            pixelDancerRenderer.gameObject.SetActive(true);

        if (delayBeforeFinishAnim > 0f)
            yield return new WaitForSeconds(delayBeforeFinishAnim);

        if (pixelDancerRenderer != null)
        {
            PixelDancerAnimController animCtrl = pixelDancerRenderer.GetComponentInParent<PixelDancerAnimController>();
            if (animCtrl != null) animCtrl.enabled = false;
            Animator anim = pixelDancerRenderer.GetComponentInParent<Animator>();
            if (anim != null)
            {
                anim.enabled = false;
                anim.transform.rotation   = Quaternion.identity;
                anim.transform.localScale = Vector3.one;
            }
            PixelDancerController moveCtrl = pixelDancerRenderer.GetComponentInParent<PixelDancerController>();
            if (moveCtrl != null) moveCtrl.enabled = false;
            pixelDancerRenderer.flipX = false;
            pixelDancerRenderer.flipY = false;
        }

        if (finishFrames == null || finishFrames.Length == 0 || pixelDancerRenderer == null)
        {
            yield return new WaitForSeconds(2f);
            if (areaCompleteTextUI != null) yield return StartCoroutine(areaCompleteTextUI.Play());
            else yield return new WaitForSeconds(3f);
            yield break;
        }

        Vector3 baseDancerPos = pixelDancerRenderer.transform.position;
        if (useFixedFinishY) baseDancerPos.y = fixedFinishY;

        isPulsing = false;

        for (int i = 0; i < finishFrames.Length; i++)
        {
            FinishFrame frame = finishFrames[i];
            if (frame.sprite != null) pixelDancerRenderer.sprite = frame.sprite;
            pixelDancerRenderer.transform.position = baseDancerPos + new Vector3(frame.offsetX, frame.offsetY, 0f);
            if (i == beamFlashFrameIndex) StartCoroutine(FlashBeams());
            yield return new WaitForSeconds(frame.duration);
        }

        yield return new WaitForSeconds(delayBeforeText);

        if (areaCompleteTextUI != null) yield return StartCoroutine(areaCompleteTextUI.Play());
        else yield return new WaitForSeconds(3f);
    }

    private IEnumerator ConvergeBeams(float duration)
    {
        if (duration <= 0f) yield break;
        float startL  = beamSrLeft        != null ? beamSrLeft.color.a        : 0f;
        float startR  = beamSrRight       != null ? beamSrRight.color.a       : 0f;
        float startF  = floorSpotRenderer != null ? floorSpotRenderer.color.a : 0f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            if (beamSrLeft        != null) SetSRAlpha(beamSrLeft,        Mathf.Lerp(startL, beamConvergeTargetAlpha, t));
            if (beamSrRight       != null) SetSRAlpha(beamSrRight,       Mathf.Lerp(startR, beamConvergeTargetAlpha, t));
            if (floorSpotRenderer != null) SetSRAlpha(floorSpotRenderer, Mathf.Lerp(startF, floorSpotConvergeTargetAlpha, t));
            yield return null;
        }
    }

    private IEnumerator FlashBeams()
    {
        if (beamFlashDuration < 0f)
        {
            if (beamSrLeft        != null) SetSRAlpha(beamSrLeft,        beamConvergeTargetAlpha);
            if (beamSrRight       != null) SetSRAlpha(beamSrRight,       beamConvergeTargetAlpha);
            if (floorSpotRenderer != null) SetSRAlpha(floorSpotRenderer, floorSpotConvergeTargetAlpha);
            yield break;
        }
        if (beamSrLeft        != null) SetSRAlpha(beamSrLeft,        1f);
        if (beamSrRight       != null) SetSRAlpha(beamSrRight,       1f);
        if (floorSpotRenderer != null) SetSRAlpha(floorSpotRenderer, 1f);
        yield return new WaitForSeconds(beamFlashDuration);
        yield return StartCoroutine(ConvergeBeams(beamConvergeDuration));
    }
}
