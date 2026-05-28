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

    [Header("── Step 2: Floor ──")]
    [SerializeField] private SpriteRenderer floorRenderer;
    [SerializeField] private float delayStep2 = 0.4f;
    [SerializeField] private float fadeStep2  = 0.8f;

    [Header("── Step 3: StartPose ──")]
    [SerializeField] private SpriteRenderer startPoseRenderer;
    [Tooltip("ランダム選択するシルエットスプライト一覧")]
    [SerializeField] private Sprite[] startPoseSilhouettes;
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
    [SerializeField, Range(0f, 1f)] private float beatPulseMinAlpha = 0.75f;
    [SerializeField, Range(0f, 1f)] private float beatPulseMaxAlpha = 1.0f;

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

    // ── runtime state ──
    private int     selectedPoseIndex = -1;
    private bool    isTracking        = false;
    private bool    isPulsing         = false;
    private Vector3 beamSmoothFrom;
    private float   beamSmoothTimer   = 0f;
    private float   floorSpotFixedY   = 0f;

    // ──────────────────────────────────────────
    // Lifecycle
    // ──────────────────────────────────────────

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
            selectedPoseIndex = Random.Range(0, startPoseSilhouettes.Length);
            startPoseRenderer.sprite = startPoseSilhouettes[selectedPoseIndex];
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
            seSource.PlayOneShot(lightOnSE, lightOnSEVolume);

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
        if (beamSrLeft  != null) StartCoroutine(FadeInSR(beamSrLeft,  1f,   beamFadeInDuration));
        if (beamSrRight != null) StartCoroutine(FadeInSR(beamSrRight, 1f,   beamFadeInDuration));
        if (floorSpotRenderer != null)
        {
            floorSpotRenderer.gameObject.SetActive(true); // SetActive(false)から復帰
            SetSRAlpha(floorSpotRenderer, 0f);
            StartCoroutine(FadeInSR(floorSpotRenderer, 0.8f, beamFadeInDuration));
        }

        yield return new WaitForSeconds(beamFadeInDuration);
    }

    /// <summary>顔の固定起点からPixelDancerまでの距離に合わせてビームを伸ばす。スプライト上端を常にoriginに固定する</summary>
    private void InitBeamScale(Transform beamRoot, SpriteRenderer beamSr, Vector3 target, Vector3 origin)
    {
        if (beamRoot == null || beamSr == null || beamSr.sprite == null) return;
        float dist = Vector3.Distance(origin, target);
        float spriteH = beamSr.sprite.bounds.size.y;
        float effectiveH = spriteH * 0.88f;
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
        if (floorSpotRenderer != null) SetSRAlpha(floorSpotRenderer, alpha * 0.8f);
    }

    // ──────────────────────────────────────────
    // 初期化
    // ──────────────────────────────────────────

    private void SetupInitialState()
    {
        if (floorRenderer != null) SetSRAlpha(floorRenderer, 0f);

        if (startPoseRenderer != null)
        {
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
}
