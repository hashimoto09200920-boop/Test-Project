using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Stage3開始直後・ボス出現前に表示する「VS」演出。
/// NeonDancerが画面左から、ボスが画面右から中央付近まで移動して静止（VSバッジ表示）→
/// その後さらに交差して反対側の画面端へ抜けていく（NeonDancerは右へ、ボスは左へ）。
/// EnemySpawner.WaveSystemRoutineから、Stage3開始時にAreaConfig.vsBossSpriteが
/// 設定されている場合のみPlayIntro()が呼ばれる（未設定エリアは演出自体スキップ）。
/// </summary>
[DefaultExecutionOrder(100)]
public class VsIntroUI : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private GameObject introRoot;

    [System.Serializable]
    public class DancerPoseEntry
    {
        public Sprite sprite;
        [Tooltip("このポーズだけの表示サイズ倍率（1=補正なし）")]
        public float scale = 1f;
        [Tooltip("このポーズだけの位置補正（px）")]
        public Vector2 positionOffset = Vector2.zero;
    }

    [Header("NeonDancer")]
    [SerializeField] private Image dancerImage;
    [Tooltip("複数登録すると毎回ランダムで1枚選ばれる。ポーズ毎にサイズ・位置を個別調整できる")]
    [SerializeField] private DancerPoseEntry[] dancerPoses;
    [Tooltip("登場開始時のX位置（anchoredPosition.x・画面左オフスクリーン）")]
    [SerializeField] private float dancerEnterX = -1510f;
    [Tooltip("中央で静止する時のX位置")]
    [SerializeField] private float dancerHoldX = -510f;
    [Tooltip("退場完了時のX位置（画面右オフスクリーン）")]
    [SerializeField] private float dancerExitX = 1400f;

    [Header("Boss")]
    [SerializeField] private Image bossImage;
    [Tooltip("登場開始時のX位置（画面右オフスクリーン）")]
    [SerializeField] private float bossEnterX = 1500f;
    [Tooltip("中央で静止する時のX位置")]
    [SerializeField] private float bossHoldX = 610f;
    [Tooltip("退場完了時のX位置（画面左オフスクリーン）")]
    [SerializeField] private float bossExitX = -1500f;

    [Header("VS Badge")]
    [SerializeField] private Image vsBadgeImage;
    [SerializeField] private Vector2 vsBadgeAnchoredPosition = Vector2.zero;

    [Header("Effects - Speed Lines (静止位置到達と同時)")]
    [SerializeField] private RectTransform speedLineBurst;
    [SerializeField] private CanvasGroup speedLineBurstGroup;
    [Tooltip("生成する線の本数（Setup実行時のみ反映）")]
    [SerializeField] private int speedLineCount = 10;
    [SerializeField] private Color speedLineColor = Color.white;
    [SerializeField] private float speedLineScaleFrom = 0.3f;
    [SerializeField] private float speedLineScaleTo = 1.4f;
    [SerializeField] private float speedLineDuration = 0.25f;

    [Header("Effects - Rim Flash (静止位置到達と同時)")]
    [Tooltip("NeonDancer側のリムフラッシュ色")]
    [SerializeField] private Color dancerRimColor = new Color(0.3f, 0.9f, 1f, 0.85f);
    [Tooltip("Boss側はDebugセクションのDebug Test Boss Theme Color、または実ゲーム内ではAreaConfig.vsBossThemeColorを使う")]
    [SerializeField] private float rimFlashScale = 1.15f;
    [SerializeField] private float rimFlashDuration = 0.25f;

    [Header("Effects - VS Badge Wobble (ポップ表示と同時)")]
    [SerializeField] private float vsBadgeWobbleAngle = 8f;
    [SerializeField] private float vsBadgeWobbleDuration = 0.2f;

    [Header("Effects - Glitch Flash (静止位置到達と同時)")]
    [Tooltip("赤/シアンのRGBずらしコピーを一瞬表示する時間（秒）")]
    [SerializeField] private float glitchDuration = 0.1f;
    [Tooltip("ずらす距離（px）")]
    [SerializeField] private float glitchOffset = 8f;

    [Header("Effects - Name Plates (静止中)")]
    [Tooltip("スライドする土台。位置操作はここに対して行う")]
    [SerializeField] private RectTransform dancerNamePlate;
    [Tooltip("NeonDancerのネームプレート画像を表示するImage")]
    [SerializeField] private Image dancerNamePlateImage;
    [Tooltip("NeonDancerのネームプレートに使う画像（固定・全エリア共通）。白ベース画像を想定し、Dancer Name Plate Colorで着色する。未設定ならネームプレート自体を表示しない")]
    [SerializeField] private Sprite dancerNamePlateSprite;
    [Tooltip("Dancer Name Plate Spriteに適用する着色（白ベース画像を正確な色にするため）")]
    [SerializeField] private Color dancerNamePlateColor = new Color(0.3f, 0.9f, 1f, 1f);

    [SerializeField] private RectTransform bossNamePlate;
    [Tooltip("Bossのネームプレート画像（AreaConfig.vsBossNameSpriteが実際には使われる）。未設定ならネームプレート自体を表示しない")]
    [SerializeField] private Image bossNamePlateImage;

    [Tooltip("キャラの静止位置から、ネームプレートが横にずれる距離（px）")]
    [SerializeField] private float namePlateOffsetX = 40f;
    [Tooltip("足元から見たネームプレートの高さ（px）")]
    [SerializeField] private float namePlateY = 40f;
    [Tooltip("スライドインしてくる距離（px）")]
    [SerializeField] private float namePlateSlideDistance = 220f;
    [SerializeField] private float namePlateSlideDuration = 0.25f;
    [Tooltip("スライドイン到達時のオーバーシュート量（px、進行方向にさらに行き過ぎてから戻る）")]
    [SerializeField] private float namePlateOvershoot = 20f;
    [SerializeField] private float namePlateBounceDuration = 0.12f;
    [Tooltip("静止表示中に続ける微弱な明滅（呼吸）の最小アルファ")]
    [Range(0f, 1f)] [SerializeField] private float namePlateBreatheMinAlpha = 0.85f;
    [Tooltip("呼吸明滅の速さ（Hz目安）")]
    [SerializeField] private float namePlateBreatheSpeed = 1.5f;
    [Tooltip("到達時に飛び散る火花の数")]
    [SerializeField] private int namePlateSparkCount = 6;
    [SerializeField] private float namePlateSparkSize = 8f;
    [SerializeField] private float namePlateSparkLifetime = 0.3f;
    [Tooltip("火花が飛び散る範囲（px）")]
    [SerializeField] private float namePlateSparkSpread = 40f;
    [Tooltip("NeonDancerの火花色プール（Area1〜10のカラー。火花1個ごとにランダムで1色選ぶ）")]
    [SerializeField]
    private Color[] dancerSparkColorPalette =
    {
        new Color(0.608f, 0.561f, 0.780f), // Area1
        new Color(0.298f, 0.686f, 0.490f), // Area2
        new Color(0.553f, 0.600f, 0.682f), // Area3
        new Color(0.878f, 0.478f, 0.247f), // Area4
        new Color(0.698f, 0.227f, 0.322f), // Area5
        new Color(0.878f, 0.690f, 0.310f), // Area6
        new Color(0.310f, 0.561f, 0.878f), // Area7
        new Color(0.373f, 0.839f, 0.839f), // Area8
        new Color(0.639f, 0.682f, 0.878f), // Area9
        new Color(0.910f, 0.790f, 0.420f), // Area10
    };

    [Header("Effects - Screen Shake (静止位置到達と同時)")]
    [SerializeField] private float shakeDuration = 0.2f;
    [Tooltip("シェイクの振幅（px）")]
    [SerializeField] private float shakeMagnitude = 10f;

    [Header("Effects - Background Darken (静止中)")]
    [SerializeField] private Image darkenOverlay;
    [Range(0f, 1f)] [SerializeField] private float darkenAlpha = 0.5f;
    [SerializeField] private float darkenFadeDuration = 0.15f;

    [Header("Effects - Impact Pulse (静止位置到達と同時)")]
    [Tooltip("NeonDancer/Bossへかける拡大パルスの倍率")]
    [SerializeField] private float pulseScale = 1.08f;
    [SerializeField] private float pulseDuration = 0.2f;
    [Tooltip("パルス中に一瞬混ぜる発光色")]
    [SerializeField] private Color pulseFlashColor = Color.white;

    [Header("Effects - Exit Afterimage (交差退場中)")]
    [SerializeField] private bool afterimageEnabled = true;
    [SerializeField] private RectTransform afterimageLayer;
    [Tooltip("残像を生成する間隔（秒）")]
    [SerializeField] private float afterimageInterval = 0.04f;
    [Range(0f, 1f)] [SerializeField] private float afterimageStartAlpha = 0.35f;
    [SerializeField] private float afterimageFadeDuration = 0.25f;

    [Header("Effects - VS Punch (静止位置到達と同時)")]
    [Tooltip("VS演出全体（introRoot）へかける拡大パンチの倍率")]
    [SerializeField] private float introPunchScale = 1.05f;
    [SerializeField] private float introPunchDuration = 0.15f;

    [Header("Timing")]
    [Tooltip("登場〜中央静止位置まで移動する時間（秒）")]
    [SerializeField] private float enterDuration = 0.5f;
    [Tooltip("登場移動のイージング強度。大きいほど静止位置直前で急激に減速する（1=等速に近い、3〜4=はっきりした失速感）")]
    [SerializeField] private float enterEasePower = 3f;
    [Tooltip("中央で静止している時間（秒）")]
    [SerializeField] private float holdDuration = 0.5f;
    [Tooltip("交差して画面端へ抜けるまでの時間（秒）")]
    [SerializeField] private float exitDuration = 0.5f;
    [Tooltip("退場移動のイージング強度")]
    [SerializeField] private float exitEasePower = 2f;
    [Tooltip("VSバッジのフェードアウト時間（Exit開始と同時に発火）")]
    [SerializeField] private float vsBadgeFadeOutDuration = 0.15f;

    [Header("SE")]
    [Tooltip("登場時に鳴らすSE（3種からランダムで1つ再生）")]
    [SerializeField] private AudioClip[] enterSEVariants = new AudioClip[3];
    [SerializeField] private AudioSource seSource;

    [Header("Debug")]
    [Tooltip("Play中に「Debug: Play VS Intro Now」で試す時に使うAreaConfig。ここのVs Boss Sprite/Vs Boss Name Sprite/Vs Boss Theme Colorを本番と全く同じ経路で読む（個別のDebug用フィールドは持たない）")]
    [SerializeField] private AreaConfig debugTestAreaConfig;
    [Tooltip("ONにするとPlay前のGame ViewでもNeonDancer/Boss/VSバッジを「静止位置」に常時表示し、位置・サイズ調整をリアルタイムに確認できる。確認が終わったらOffに戻すこと")]
    [SerializeField] private bool previewHoldPositionInEditMode = false;
    [Tooltip("プレビュー中にDancer Posesの何番目を表示するか（0始まり）。ポーズ毎のScale/Position Offsetをこの番号で確認・調整する")]
    [SerializeField] private int previewDancerPoseIndex = 0;
    [Tooltip("-1でランダム（本番と同じ）。0以上を指定すると、Play中・Debug再生時に必ずそのDancer Posesの番号が選ばれる（動作確認用）")]
    [SerializeField] private int debugForceDancerPoseIndex = -1;

    private static float MasterSEVolume => SoundSettingsManager.Instance != null ? SoundSettingsManager.Instance.SEVolume : 1f;

#if UNITY_EDITOR
    private void OnValidate()
    {
        UnityEditor.EditorApplication.delayCall += RefreshEditorPreview;
    }

    /// <summary>AreaConfig.OnValidateから呼ばれる。今Debug Test Area Configとして見ているものが変更されたら更新する</summary>
    public void NotifyAreaConfigChangedInEditor(AreaConfig changedConfig)
    {
        if (changedConfig != null && changedConfig == debugTestAreaConfig)
            RefreshEditorPreview();
    }

    private void RefreshEditorPreview()
    {
        if (this == null || Application.isPlaying) return;
        if (introRoot == null || dancerImage == null || bossImage == null) return;

        introRoot.SetActive(previewHoldPositionInEditMode);
        if (!previewHoldPositionInEditMode) return;

        float bossScale = 1f;
        Vector2 bossOffset = Vector2.zero;
        if (debugTestAreaConfig != null && debugTestAreaConfig.vsBossSprite != null)
        {
            bossImage.sprite = debugTestAreaConfig.vsBossSprite;
            bossScale = debugTestAreaConfig.vsBossScale;
            bossOffset = debugTestAreaConfig.vsBossPositionOffset;
        }
        bossImage.transform.localScale = Vector3.one * Mathf.Max(0.01f, bossScale);

        SetImageAlpha(dancerImage, 1f);
        SetImageAlpha(bossImage, 1f);
        Vector2 previewOffset = ApplyDancerPose(previewDancerPoseIndex);
        dancerImage.rectTransform.anchoredPosition = new Vector2(dancerHoldX + previewOffset.x, previewOffset.y);
        bossImage.rectTransform.anchoredPosition = new Vector2(bossHoldX + bossOffset.x, bossOffset.y);

        if (vsBadgeImage != null)
        {
            vsBadgeImage.gameObject.SetActive(true);
            vsBadgeImage.rectTransform.anchoredPosition = vsBadgeAnchoredPosition;
            SetImageAlpha(vsBadgeImage, 1f);
        }
        if (dancerNamePlate != null)
        {
            SetNamePlateContent(dancerNamePlate, dancerNamePlateImage, dancerNamePlateSprite, dancerNamePlateColor);
            SetX(dancerNamePlate, dancerHoldX - namePlateOffsetX);
            Vector2 dp = dancerNamePlate.anchoredPosition; dp.y = namePlateY; dancerNamePlate.anchoredPosition = dp;
        }
        if (bossNamePlate != null)
        {
            SetNamePlateContent(bossNamePlate, bossNamePlateImage, debugTestAreaConfig != null ? debugTestAreaConfig.vsBossNameSprite : null);
            SetX(bossNamePlate, bossHoldX + namePlateOffsetX);
            Vector2 bp = bossNamePlate.anchoredPosition; bp.y = namePlateY; bossNamePlate.anchoredPosition = bp;
        }

        UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
    }
#endif

    private void Awake()
    {
        if (seSource == null) seSource = GetComponent<AudioSource>();
        if (seSource == null) seSource = gameObject.AddComponent<AudioSource>();
        seSource.playOnAwake = false;

        if (introRoot != null) introRoot.SetActive(false);
    }

    private Sprite currentBossNameSprite;
    private Color currentBossThemeColor;
    private Coroutine dancerBreatheCo;
    private Coroutine bossBreatheCo;

    /// <summary>
    /// VS演出を再生する。bossSpriteがnullの場合は何もせず即終了する
    /// （呼び出し側でAreaConfig.vsBossSprite未設定エリアをスキップする判定にも使える）。
    /// bossNameSpriteはネームプレート画像（省略可。未設定ならボス側のネームプレートは表示しない）。
    /// bossThemeColorはボス側の火花などに使うテーマカラー（省略時はDebug Test Area Configの値を使う）。
    /// bossScale/bossPositionOffsetはこのボスだけの表示サイズ・位置補正（省略時は補正なし。他エリアには影響しない）。
    /// </summary>
    public IEnumerator PlayIntro(Sprite bossSprite, Sprite bossNameSprite = null, Color? bossThemeColor = null, float bossScale = 1f, Vector2 bossPositionOffset = default)
    {
        if (introRoot == null || bossSprite == null || dancerImage == null || bossImage == null)
            yield break;

        currentBossNameSprite = bossNameSprite;
        currentBossThemeColor = bossThemeColor ?? (debugTestAreaConfig != null ? debugTestAreaConfig.vsBossThemeColor : Color.white);
        Time.timeScale = 0f;
        introRoot.SetActive(true);

        bossImage.sprite = bossSprite;
        int poseIndex;
        if (debugForceDancerPoseIndex >= 0 && dancerPoses != null && debugForceDancerPoseIndex < dancerPoses.Length)
            poseIndex = debugForceDancerPoseIndex;
        else
            poseIndex = (dancerPoses != null && dancerPoses.Length > 0) ? Random.Range(0, dancerPoses.Length) : -1;
        Vector2 dancerOffset = ApplyDancerPose(poseIndex);
        float effectiveDancerEnterX = dancerEnterX + dancerOffset.x;
        float effectiveDancerHoldX = dancerHoldX + dancerOffset.x;
        float effectiveDancerExitX = dancerExitX + dancerOffset.x;

        float effectiveBossEnterX = bossEnterX + bossPositionOffset.x;
        float effectiveBossHoldX = bossHoldX + bossPositionOffset.x;
        float effectiveBossExitX = bossExitX + bossPositionOffset.x;

        SetImageAlpha(dancerImage, 1f);
        SetImageAlpha(bossImage, 1f);
        bossImage.transform.localScale = Vector3.one * Mathf.Max(0.01f, bossScale);
        introRoot.transform.localScale = Vector3.one;
        ((RectTransform)introRoot.transform).anchoredPosition = Vector2.zero;
        dancerImage.rectTransform.anchoredPosition = new Vector2(effectiveDancerEnterX, dancerOffset.y);
        bossImage.rectTransform.anchoredPosition = new Vector2(effectiveBossEnterX, bossPositionOffset.y);

        if (vsBadgeImage != null)
        {
            vsBadgeImage.rectTransform.anchoredPosition = vsBadgeAnchoredPosition;
            vsBadgeImage.transform.localRotation = Quaternion.identity;
            SetImageAlpha(vsBadgeImage, 0f);
            vsBadgeImage.gameObject.SetActive(false);
        }
        if (speedLineBurst != null) speedLineBurst.gameObject.SetActive(false);
        if (darkenOverlay != null)
            SetImageAlpha(darkenOverlay, 0f);
        if (dancerNamePlate != null) dancerNamePlate.gameObject.SetActive(false);
        if (bossNamePlate != null) bossNamePlate.gameObject.SetActive(false);
        StopBreathing();
        ClearAfterimages();

        PlayRandomEnterSE();

        // Enter: オフスクリーン → 中央静止位置（enterEasePowerが大きいほど到達直前で失速する）
        yield return StartCoroutine(MoveBoth(
            dancerImage.rectTransform, effectiveDancerEnterX, effectiveDancerHoldX,
            bossImage.rectTransform, effectiveBossEnterX, effectiveBossHoldX,
            enterDuration, enterEasePower));

        // Hold: VSバッジ表示 + 衝突演出一式（シェイク・暗転・パルス・パンチ・グリッチ・ネームプレート）
        if (vsBadgeImage != null)
        {
            vsBadgeImage.gameObject.SetActive(true);
            SetImageAlpha(vsBadgeImage, 1f);
        }
        StartCoroutine(ShakeRoot(shakeDuration, shakeMagnitude));
        StartCoroutine(PunchScale(introRoot.transform, introPunchScale, introPunchDuration));
        StartCoroutine(ImpactPulse(dancerImage, pulseScale, pulseDuration));
        StartCoroutine(ImpactPulse(bossImage, pulseScale, pulseDuration));
        StartCoroutine(PlaySpeedLineBurst());
        StartCoroutine(RimFlash(dancerImage, dancerRimColor, rimFlashScale, rimFlashDuration));
        StartCoroutine(RimFlash(bossImage, currentBossThemeColor, rimFlashScale, rimFlashDuration));
        StartCoroutine(GlitchFlash(dancerImage, glitchDuration, glitchOffset));
        StartCoroutine(GlitchFlash(bossImage, glitchDuration, glitchOffset));
        if (vsBadgeImage != null)
            StartCoroutine(WobbleRotate(vsBadgeImage.transform, vsBadgeWobbleAngle, vsBadgeWobbleDuration));
        if (darkenOverlay != null)
            StartCoroutine(FadeImage(darkenOverlay, 0f, darkenAlpha, darkenFadeDuration));
        if (dancerNamePlate != null)
        {
            SetNamePlateContent(dancerNamePlate, dancerNamePlateImage, dancerNamePlateSprite, dancerNamePlateColor);
            StartCoroutine(SlideInNamePlate(dancerNamePlate, dancerNamePlateImage, dancerHoldX - namePlateOffsetX - namePlateSlideDistance, dancerHoldX - namePlateOffsetX, namePlateSlideDuration, true));
        }
        if (bossNamePlate != null)
        {
            SetNamePlateContent(bossNamePlate, bossNamePlateImage, currentBossNameSprite);
            StartCoroutine(SlideInNamePlate(bossNamePlate, bossNamePlateImage, bossHoldX + namePlateOffsetX + namePlateSlideDistance, bossHoldX + namePlateOffsetX, namePlateSlideDuration, false));
        }

        yield return new WaitForSecondsRealtime(holdDuration);

        // Exit: 交差して反対側の画面端へ（残像付き）
        if (vsBadgeImage != null)
            StartCoroutine(FadeImage(vsBadgeImage, 1f, 0f, vsBadgeFadeOutDuration));
        if (darkenOverlay != null)
            StartCoroutine(FadeImage(darkenOverlay, darkenAlpha, 0f, darkenFadeDuration));
        if (afterimageEnabled)
        {
            StartCoroutine(SpawnAfterimages(dancerImage, exitDuration));
            StartCoroutine(SpawnAfterimages(bossImage, exitDuration));
        }
        if (dancerNamePlate != null) dancerNamePlate.gameObject.SetActive(false);
        if (bossNamePlate != null) bossNamePlate.gameObject.SetActive(false);
        StopBreathing();

        yield return StartCoroutine(MoveBoth(
            dancerImage.rectTransform, effectiveDancerHoldX, effectiveDancerExitX,
            bossImage.rectTransform, effectiveBossHoldX, effectiveBossExitX,
            exitDuration, exitEasePower));

        ClearAfterimages();
        introRoot.SetActive(false);
        Time.timeScale = 1f;
    }

    private IEnumerator MoveBoth(RectTransform a, float aFrom, float aTo, RectTransform b, float bFrom, float bTo, float duration, float easePower)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float linear = Mathf.Clamp01(elapsed / duration);
            float t = 1f - Mathf.Pow(1f - linear, Mathf.Max(1f, easePower));
            SetX(a, Mathf.Lerp(aFrom, aTo, t));
            SetX(b, Mathf.Lerp(bFrom, bTo, t));
            yield return null;
        }
        SetX(a, aTo);
        SetX(b, bTo);
    }

    /// <summary>spriteが未設定ならネームプレート自体を非表示にする（画像専用・文字フォールバック無し）。tintColorを指定すると白ベース画像を正確な色に着色する</summary>
    private void SetNamePlateContent(RectTransform container, Image img, Sprite sprite, Color? tintColor = null)
    {
        bool show = sprite != null && img != null;
        container.gameObject.SetActive(show);
        if (!show) return;

        Vector2 pos = container.anchoredPosition;
        pos.y = namePlateY;
        container.anchoredPosition = pos;

        img.sprite = sprite;
        img.color = tintColor ?? Color.white;
        img.gameObject.SetActive(true);
    }

    /// <summary>スライドイン→(到達と同時に火花)→オーバーシュート→静止中の呼吸明滅、まで一連で再生する</summary>
    private IEnumerator SlideInNamePlate(RectTransform container, Image img, float fromX, float toX, float duration, bool isDancerSide)
    {
        if (container == null) yield break;

        // 1. スライドイン
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = 1f - Mathf.Pow(1f - Mathf.Clamp01(elapsed / duration), 3f);
            SetX(container, Mathf.Lerp(fromX, toX, t));
            yield return null;
        }
        SetX(container, toX);

        // 到達と同時に火花（Dancer側はArea1〜10のカラーからランダム、Boss側はボスカラー固定）
        if (namePlateSparkCount > 0)
        {
            Color[] palette = isDancerSide
                ? (dancerSparkColorPalette != null && dancerSparkColorPalette.Length > 0 ? dancerSparkColorPalette : new[] { Color.white })
                : new[] { currentBossThemeColor };
            SpawnNamePlateSparks(container, palette);
        }

        // 2. オーバーシュート（進行方向にさらに行き過ぎてから戻る）
        if (namePlateOvershoot > 0f && namePlateBounceDuration > 0f)
        {
            float dir = Mathf.Sign(toX - fromX);
            float overshootX = toX + dir * namePlateOvershoot;
            float half = namePlateBounceDuration * 0.5f;
            elapsed = 0f;
            while (elapsed < half)
            {
                elapsed += Time.unscaledDeltaTime;
                SetX(container, Mathf.Lerp(toX, overshootX, Mathf.Clamp01(elapsed / half)));
                yield return null;
            }
            elapsed = 0f;
            while (elapsed < half)
            {
                elapsed += Time.unscaledDeltaTime;
                SetX(container, Mathf.Lerp(overshootX, toX, Mathf.Clamp01(elapsed / half)));
                yield return null;
            }
            SetX(container, toX);
        }

        if (img != null) SetImageAlpha(img, 1f);

        // 3. 静止表示中はごく僅かな明滅（呼吸）を続ける
        if (img != null)
        {
            var breatheCo = StartCoroutine(BreatheNamePlate(img));
            if (isDancerSide) dancerBreatheCo = breatheCo;
            else bossBreatheCo = breatheCo;
        }
    }

    private IEnumerator BreatheNamePlate(Image img)
    {
        while (true)
        {
            if (img == null) yield break;
            float wave = (Mathf.Sin(Time.unscaledTime * namePlateBreatheSpeed * Mathf.PI * 2f) + 1f) * 0.5f;
            SetImageAlpha(img, Mathf.Lerp(namePlateBreatheMinAlpha, 1f, wave));
            yield return null;
        }
    }

    private void StopBreathing()
    {
        if (dancerBreatheCo != null) { StopCoroutine(dancerBreatheCo); dancerBreatheCo = null; }
        if (bossBreatheCo != null) { StopCoroutine(bossBreatheCo); bossBreatheCo = null; }
    }

    private void SpawnNamePlateSparks(RectTransform container, Color[] colorPalette)
    {
        for (int i = 0; i < namePlateSparkCount; i++)
        {
            var go = new GameObject("Spark", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(container, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = Vector2.one * namePlateSparkSize;
            rt.anchoredPosition = new Vector2(
                Random.Range(-namePlateSparkSpread, namePlateSparkSpread),
                Random.Range(-namePlateSparkSpread * 0.3f, namePlateSparkSpread * 0.3f));

            var img = go.GetComponent<Image>();
            img.color = colorPalette[Random.Range(0, colorPalette.Length)];
            img.raycastTarget = false;

            Vector2 dir = new Vector2(Random.Range(-1f, 1f), Random.Range(0.3f, 1f)).normalized;
            StartCoroutine(AnimateSpark(go, rt, dir));
        }
    }

    private IEnumerator AnimateSpark(GameObject go, RectTransform rt, Vector2 dir)
    {
        Vector2 startPos = rt.anchoredPosition;
        Vector2 endPos = startPos + dir * (namePlateSparkSpread * 0.6f);
        var img = go.GetComponent<Image>();
        Color baseColor = img.color;
        float elapsed = 0f;
        while (elapsed < namePlateSparkLifetime)
        {
            if (img == null) yield break;
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / namePlateSparkLifetime);
            rt.anchoredPosition = Vector2.Lerp(startPos, endPos, t);
            float s = Mathf.Lerp(1f, 0.2f, t);
            rt.localScale = new Vector3(s, s, 1f);
            Color c = baseColor;
            c.a = Mathf.Lerp(baseColor.a, 0f, t);
            img.color = c;
            yield return null;
        }
        if (go != null) Destroy(go);
    }

    /// <summary>NeonDancer/Bossの画像を赤/シアンにずらしたコピーで一瞬重ね、グリッチ風に見せる</summary>
    private IEnumerator GlitchFlash(Image source, float duration, float offset)
    {
        if (source == null || source.sprite == null) yield break;
        var redGo = CreateOffsetCopy(source, new Color(1f, 0.15f, 0.15f, 0.55f), new Vector2(offset, 0f));
        var cyanGo = CreateOffsetCopy(source, new Color(0.15f, 0.9f, 1f, 0.55f), new Vector2(-offset, 0f));
        yield return new WaitForSecondsRealtime(duration);
        if (redGo != null) Destroy(redGo);
        if (cyanGo != null) Destroy(cyanGo);
    }

    private GameObject CreateOffsetCopy(Image source, Color tint, Vector2 offset)
    {
        var go = new GameObject("Glitch", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(source.transform.parent, false);
        go.transform.SetSiblingIndex(source.transform.GetSiblingIndex());

        var srcRt = source.rectTransform;
        var rt = (RectTransform)go.transform;
        rt.anchorMin = srcRt.anchorMin;
        rt.anchorMax = srcRt.anchorMax;
        rt.pivot = srcRt.pivot;
        rt.sizeDelta = srcRt.sizeDelta;
        rt.anchoredPosition = srcRt.anchoredPosition + offset;
        rt.localScale = srcRt.localScale;

        var img = go.GetComponent<Image>();
        img.sprite = source.sprite;
        img.preserveAspect = true;
        img.raycastTarget = false;
        img.color = tint;
        return go;
    }

    private IEnumerator ShakeRoot(float duration, float magnitude)
    {
        if (introRoot == null) yield break;
        var rt = (RectTransform)introRoot.transform;
        Vector2 basePos = rt.anchoredPosition;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float decay = 1f - Mathf.Clamp01(elapsed / duration);
            float x = Random.Range(-1f, 1f) * magnitude * decay;
            float y = Random.Range(-1f, 1f) * magnitude * decay;
            rt.anchoredPosition = basePos + new Vector2(x, y);
            yield return null;
        }
        rt.anchoredPosition = basePos;
    }

    private IEnumerator PunchScale(Transform t, float peakScale, float duration)
    {
        if (t == null) yield break;
        float half = duration * 0.5f;
        yield return LerpScale(t, 1f, peakScale, half);
        yield return LerpScale(t, peakScale, 1f, half);
        t.localScale = Vector3.one;
    }

    private IEnumerator LerpScale(Transform t, float from, float to, float duration)
    {
        if (duration <= 0f) yield break;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float s = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
            t.localScale = new Vector3(s, s, 1f);
            yield return null;
        }
    }

    private IEnumerator ImpactPulse(Image img, float peakScale, float duration)
    {
        if (img == null) yield break;
        Color baseColor = img.color;
        Transform t = img.transform;
        // ★キャラごとの基準スケール（Dancer Posesのscale等）を1.0固定で潰さないよう、
        //   パルス開始時点の実際のlocalScaleを基準にして拡大・復帰する
        float baseScale = t.localScale.x;
        float half = duration * 0.5f;
        float elapsed = 0f;
        while (elapsed < half)
        {
            elapsed += Time.unscaledDeltaTime;
            float f = Mathf.Clamp01(elapsed / half);
            float s = Mathf.Lerp(baseScale, baseScale * peakScale, f);
            t.localScale = new Vector3(s, s, 1f);
            img.color = Color.Lerp(baseColor, pulseFlashColor, f);
            yield return null;
        }
        elapsed = 0f;
        while (elapsed < half)
        {
            elapsed += Time.unscaledDeltaTime;
            float f = Mathf.Clamp01(elapsed / half);
            float s = Mathf.Lerp(baseScale * peakScale, baseScale, f);
            t.localScale = new Vector3(s, s, 1f);
            img.color = Color.Lerp(pulseFlashColor, baseColor, f);
            yield return null;
        }
        t.localScale = new Vector3(baseScale, baseScale, 1f);
        img.color = baseColor;
    }

    private IEnumerator PlaySpeedLineBurst()
    {
        if (speedLineBurst == null) yield break;
        speedLineBurst.gameObject.SetActive(true);
        speedLineBurst.localScale = Vector3.one * speedLineScaleFrom;
        if (speedLineBurstGroup != null) speedLineBurstGroup.alpha = 1f;

        float elapsed = 0f;
        while (elapsed < speedLineDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / speedLineDuration);
            float s = Mathf.Lerp(speedLineScaleFrom, speedLineScaleTo, t);
            speedLineBurst.localScale = Vector3.one * s;
            if (speedLineBurstGroup != null) speedLineBurstGroup.alpha = Mathf.Lerp(1f, 0f, t);
            yield return null;
        }
        speedLineBurst.gameObject.SetActive(false);
    }

    /// <summary>キャラのシルエットをテーマカラーで塗ったコピーを一瞬だけ背後に出し、フェード消滅させる</summary>
    private IEnumerator RimFlash(Image source, Color flashColor, float peakScale, float duration)
    {
        if (source == null || source.sprite == null) yield break;

        var go = new GameObject("RimFlash", typeof(RectTransform), typeof(Image));
        Transform parent = source.transform.parent;
        go.transform.SetParent(parent, false);
        go.transform.SetSiblingIndex(source.transform.GetSiblingIndex());

        var srcRt = source.rectTransform;
        var rt = (RectTransform)go.transform;
        rt.anchorMin = srcRt.anchorMin;
        rt.anchorMax = srcRt.anchorMax;
        rt.pivot = srcRt.pivot;
        rt.sizeDelta = srcRt.sizeDelta;
        rt.anchoredPosition = srcRt.anchoredPosition;

        var img = go.GetComponent<Image>();
        img.sprite = source.sprite;
        img.preserveAspect = true;
        img.raycastTarget = false;
        img.color = flashColor;

        // ★元画像側のポーズ別スケール（Dancer Posesのscale等）を1.0で潰さないよう、
        //   sourceの実際のlocalScaleを基準にする
        float baseScale = source.transform.localScale.x;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float s = Mathf.Lerp(baseScale, baseScale * peakScale, t);
            rt.localScale = new Vector3(s, s, 1f);
            Color c = flashColor;
            c.a = Mathf.Lerp(flashColor.a, 0f, t);
            img.color = c;
            yield return null;
        }
        Destroy(go);
    }

    private IEnumerator WobbleRotate(Transform t, float angle, float duration)
    {
        if (t == null) yield break;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float f = Mathf.Clamp01(elapsed / duration);
            float damped = angle * (1f - f) * Mathf.Sin(f * Mathf.PI * 3f);
            t.localRotation = Quaternion.Euler(0f, 0f, damped);
            yield return null;
        }
        t.localRotation = Quaternion.identity;
    }

    private readonly System.Collections.Generic.List<GameObject> activeAfterimages = new System.Collections.Generic.List<GameObject>();

    private IEnumerator SpawnAfterimages(Image source, float duration)
    {
        if (!afterimageEnabled || source == null || afterimageLayer == null) yield break;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            SpawnAfterimage(source);
            yield return new WaitForSecondsRealtime(afterimageInterval);
            elapsed += afterimageInterval;
        }
    }

    private void SpawnAfterimage(Image source)
    {
        var go = new GameObject("Afterimage", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(afterimageLayer, false);
        var rt = (RectTransform)go.transform;
        var srcRt = source.rectTransform;
        rt.anchorMin = srcRt.anchorMin;
        rt.anchorMax = srcRt.anchorMax;
        rt.pivot = srcRt.pivot;
        rt.sizeDelta = srcRt.sizeDelta;
        rt.anchoredPosition = srcRt.anchoredPosition;
        rt.localScale = srcRt.localScale;

        var img = go.GetComponent<Image>();
        img.sprite = source.sprite;
        img.preserveAspect = true;
        img.raycastTarget = false;
        Color c = source.color;
        c.a = afterimageStartAlpha;
        img.color = c;

        activeAfterimages.Add(go);
        StartCoroutine(FadeAndDestroyAfterimage(go, img, afterimageFadeDuration));
    }

    private IEnumerator FadeAndDestroyAfterimage(GameObject go, Image img, float duration)
    {
        if (img == null) yield break;
        float startA = img.color.a;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            // ClearAfterimages()等、別経路で先に破棄されていたら安全に終了する
            // （破棄済みImageへアクセスするとMissingReferenceExceptionになるため）
            if (img == null) yield break;
            elapsed += Time.unscaledDeltaTime;
            SetImageAlpha(img, Mathf.Lerp(startA, 0f, Mathf.Clamp01(elapsed / duration)));
            yield return null;
        }
        activeAfterimages.Remove(go);
        if (go != null) Destroy(go);
    }

    private void ClearAfterimages()
    {
        foreach (var go in activeAfterimages)
            if (go != null) Destroy(go);
        activeAfterimages.Clear();
    }

    private IEnumerator FadeImage(Image img, float from, float to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            SetImageAlpha(img, Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration)));
            yield return null;
        }
        SetImageAlpha(img, to);
    }

    private void SetX(RectTransform rt, float x)
    {
        Vector2 pos = rt.anchoredPosition;
        pos.x = x;
        rt.anchoredPosition = pos;
    }

    /// <summary>
    /// dancerPoses[index]のスプライト・拡大率をdancerImageに適用し、その位置補正(positionOffset)を返す。
    /// indexが不正な場合はスケールを1に戻し、Vector2.zero（補正なし）を返す。
    /// </summary>
    private Vector2 ApplyDancerPose(int index)
    {
        if (dancerPoses == null || index < 0 || index >= dancerPoses.Length || dancerPoses[index] == null)
        {
            dancerImage.transform.localScale = Vector3.one;
            return Vector2.zero;
        }

        var pose = dancerPoses[index];
        if (pose.sprite != null) dancerImage.sprite = pose.sprite;
        dancerImage.transform.localScale = Vector3.one * Mathf.Max(0.01f, pose.scale);
        return pose.positionOffset;
    }

    /// <summary>Play中にStage3まで進まなくても、サイズ・タイミング調整のためVS演出だけを即座に試せる。</summary>
    [ContextMenu("Debug: Play VS Intro Now")]
    private void DebugPlayVsIntroNow()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[VsIntroUI] Play中のみ実行できます。");
            return;
        }
        if (debugTestAreaConfig == null || debugTestAreaConfig.vsBossSprite == null)
        {
            Debug.LogWarning("[VsIntroUI] Debug Test Area Config、またはそのVs Boss Spriteが未設定です。");
            return;
        }
        StopAllCoroutines();
        Time.timeScale = 1f; // 前回のPlayIntroが途中で中断されTime.timeScale=0のままの場合の保険
        StartCoroutine(PlayIntro(debugTestAreaConfig.vsBossSprite, debugTestAreaConfig.vsBossNameSprite, debugTestAreaConfig.vsBossThemeColor, debugTestAreaConfig.vsBossScale, debugTestAreaConfig.vsBossPositionOffset));
    }

    private void SetImageAlpha(Image img, float a)
    {
        Color c = img.color;
        c.a = a;
        img.color = c;
    }

    private void PlayRandomEnterSE()
    {
        if (enterSEVariants == null || seSource == null) return;
        int count = 0;
        foreach (var c in enterSEVariants) if (c != null) count++;
        if (count == 0) return;
        int target = Random.Range(0, count);
        int cur = 0;
        foreach (var c in enterSEVariants)
        {
            if (c == null) continue;
            if (cur == target)
            {
                seSource.PlayOneShot(c, MasterSEVolume);
                return;
            }
            cur++;
        }
    }

#if UNITY_EDITOR
    /// <summary>
    /// StageCutInCanvas（Stage開始カットインと同じCanvas・sortingOrder）の下に、
    /// VS演出用のRoot/NeonDancer/Boss/VSBadgeのRectTransformをPlay前に自動生成する。
    /// 再実行しても既存のものを再利用する（Image参照が消えたりしない）。
    /// </summary>
    [ContextMenu("Setup VS Intro UI (Play前にHierarchyを生成)")]
    private void SetupVsIntroUI()
    {
        var cutInCanvasTf = GameObject.Find("StageCutInCanvas")?.transform;
        Transform parent = cutInCanvasTf != null ? cutInCanvasTf : transform;
        if (cutInCanvasTf == null)
            Debug.LogWarning("[VsIntroUI] StageCutInCanvasが見つからないため、このオブジェクト直下に生成します。");

        var rootTf = parent.Find("VsIntroRoot");
        GameObject rootGo = rootTf != null ? rootTf.gameObject : new GameObject("VsIntroRoot", typeof(RectTransform));
        rootGo.transform.SetParent(parent, false);
        var rootRt = (RectTransform)rootGo.transform;
        rootRt.anchorMin = Vector2.zero;
        rootRt.anchorMax = Vector2.one;
        rootRt.sizeDelta = Vector2.zero;
        rootRt.anchoredPosition = Vector2.zero;
        introRoot = rootGo;

        darkenOverlay = CreateOrGetImage(rootRt, "DarkenOverlay", Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero);
        darkenOverlay.rectTransform.anchorMin = Vector2.zero;
        darkenOverlay.rectTransform.anchorMax = Vector2.one;
        darkenOverlay.rectTransform.sizeDelta = Vector2.zero;
        darkenOverlay.color = new Color(0f, 0f, 0f, 0f);
        darkenOverlay.raycastTarget = false;

        var afterimageLayerTf = rootRt.Find("AfterimageLayer");
        GameObject afterimageLayerGo = afterimageLayerTf != null ? afterimageLayerTf.gameObject : new GameObject("AfterimageLayer", typeof(RectTransform));
        afterimageLayerGo.transform.SetParent(rootRt, false);
        afterimageLayer = (RectTransform)afterimageLayerGo.transform;
        afterimageLayer.anchorMin = Vector2.zero;
        afterimageLayer.anchorMax = Vector2.one;
        afterimageLayer.sizeDelta = Vector2.zero;

        bossImage   = CreateOrGetImage(rootRt, "Boss",       new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(900f, 1050f));
        dancerImage = CreateOrGetImage(rootRt, "NeonDancer", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(700f, 900f));

        SetupSpeedLineBurst(rootRt);

        dancerNamePlate = CreateOrGetNamePlate(rootRt, "DancerNamePlate", out dancerNamePlateImage);
        bossNamePlate   = CreateOrGetNamePlate(rootRt, "BossNamePlate",   out bossNamePlateImage);

        vsBadgeImage = CreateOrGetImage(rootRt, "VsBadge",   new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(320f, 320f));
        vsBadgeImage.rectTransform.anchoredPosition = vsBadgeAnchoredPosition;

        // 描画順（奥→手前）：暗転オーバーレイ → 残像レイヤー → Boss → NeonDancer → スピードライン → ネームプレート → VSバッジ
        darkenOverlay.transform.SetSiblingIndex(0);
        afterimageLayer.SetSiblingIndex(1);
        bossImage.transform.SetSiblingIndex(2);
        dancerImage.transform.SetSiblingIndex(3);
        speedLineBurst.SetSiblingIndex(4);
        dancerNamePlate.transform.SetSiblingIndex(5);
        bossNamePlate.transform.SetSiblingIndex(6);
        vsBadgeImage.transform.SetSiblingIndex(7);

        UnityEditor.EditorUtility.SetDirty(this);
        UnityEditor.EditorUtility.SetDirty(rootGo);
        Debug.Log("[VsIntroUI] VsIntroRoot / DarkenOverlay / AfterimageLayer / NeonDancer / Boss / SpeedLineBurst / VsBadge を生成し、参照をアサインしました。");
    }

    /// <summary>
    /// 放射状の線をspeedLineCount本、均等な角度で配置したコンテナを生成する（既存の場合は本数を揃え直す）。
    /// 位置・スケールはVSバッジと同じ画面中央（0,0）に配置される。
    /// </summary>
    private void SetupSpeedLineBurst(RectTransform rootRt)
    {
        var existingTf = rootRt.Find("SpeedLineBurst");
        bool burstIsNew = existingTf == null;
        GameObject burstGo = burstIsNew ? new GameObject("SpeedLineBurst", typeof(RectTransform)) : existingTf.gameObject;
        burstGo.transform.SetParent(rootRt, false);
        var burstRt = (RectTransform)burstGo.transform;
        if (burstIsNew)
        {
            burstRt.anchorMin = burstRt.anchorMax = new Vector2(0.5f, 0.5f);
            burstRt.pivot = new Vector2(0.5f, 0.5f);
            burstRt.sizeDelta = Vector2.zero;
            burstRt.anchoredPosition = Vector2.zero;
        }

        var group = burstGo.GetComponent<CanvasGroup>();
        if (group == null) group = burstGo.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;

        // 既存の線を全部作り直す（本数変更に対応するため）
        for (int i = burstRt.childCount - 1; i >= 0; i--)
            SafeDestroy(burstRt.GetChild(i).gameObject);

        for (int i = 0; i < speedLineCount; i++)
        {
            var lineGo = new GameObject($"Line{i}", typeof(RectTransform), typeof(Image));
            lineGo.transform.SetParent(burstRt, false);
            var lineRt = (RectTransform)lineGo.transform;
            lineRt.anchorMin = lineRt.anchorMax = new Vector2(0.5f, 0.5f);
            lineRt.pivot = new Vector2(0.5f, 0f);
            lineRt.sizeDelta = new Vector2(6f, 260f);
            lineRt.anchoredPosition = Vector2.zero;
            lineRt.localRotation = Quaternion.Euler(0f, 0f, 360f / speedLineCount * i);

            var lineImg = lineGo.GetComponent<Image>();
            lineImg.color = speedLineColor;
            lineImg.raycastTarget = false;
        }

        burstGo.SetActive(false);
        speedLineBurst = burstRt;
        speedLineBurstGroup = group;
    }

    private static void SafeDestroy(GameObject go)
    {
        if (go == null) return;
        if (Application.isPlaying) Destroy(go);
        else DestroyImmediate(go);
    }

    /// <summary>
    /// 既存なら再利用してサイズ・位置を維持する（再実行で調整済みの値を壊さないため）。
    /// 新規生成時のみanchor/pivot/sizeDeltaの初期値を適用する。
    /// </summary>
    private Image CreateOrGetImage(RectTransform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 sizeDelta)
    {
        var existing = parent.Find(name);
        bool isNew = existing == null;
        GameObject go = isNew ? new GameObject(name, typeof(RectTransform), typeof(Image)) : existing.gameObject;
        go.transform.SetParent(parent, false);
        if (isNew)
        {
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.sizeDelta = sizeDelta;
        }
        var img = go.GetComponent<Image>();
        if (img == null) img = go.AddComponent<Image>();
        img.preserveAspect = true;
        img.raycastTarget = false;
        return img;
    }

    /// <summary>
    /// ネームプレートの土台（コンテナ）+ 画像用Image + 文字用Textを生成する。
    /// 既存なら再利用してサイズ・位置を維持する（Imageと同じ方針）。
    /// </summary>
    private RectTransform CreateOrGetNamePlate(RectTransform parent, string name, out Image img)
    {
        var existing = parent.Find(name);
        bool isNew = existing == null;
        GameObject go = isNew ? new GameObject(name, typeof(RectTransform)) : existing.gameObject;
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        if (isNew)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(600f, 120f);
        }

        // ★以前のバージョンでは土台自体にTextMeshProUGUIを直接付けていたため、
        //   その頃に生成済みのオブジェクトを再利用すると古いTextが残って画像と重なって見える。
        //   画像専用に作り直した今は不要なので、土台に直接付いているTextは削除する。
        var staleText = go.GetComponent<TMPro.TextMeshProUGUI>();
        if (staleText != null)
        {
            if (Application.isPlaying) Destroy(staleText);
            else DestroyImmediate(staleText);
        }
        // 一時期は子オブジェクト"Text"としても作っていたため、そちらの残骸も削除する
        var staleTextChild = rt.Find("Text");
        if (staleTextChild != null) SafeDestroy(staleTextChild.gameObject);

        var imgTf = rt.Find("Image");
        GameObject imgGo = imgTf != null ? imgTf.gameObject : new GameObject("Image", typeof(RectTransform), typeof(Image));
        imgGo.transform.SetParent(rt, false);
        var imgRt = (RectTransform)imgGo.transform;
        imgRt.anchorMin = Vector2.zero;
        imgRt.anchorMax = Vector2.one;
        imgRt.sizeDelta = Vector2.zero;
        img = imgGo.GetComponent<Image>();
        if (img == null) img = imgGo.AddComponent<Image>();
        img.preserveAspect = true;
        img.raycastTarget = false;

        go.SetActive(false);
        return rt;
    }
#endif
}
