using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// スローモーションアクティブスキルのコアシステム
/// カスタムタイムスケールを管理し、スローモーション中のみ効果を発揮
/// </summary>
public class SlowMotionManager : MonoBehaviour
{
    public static SlowMotionManager Instance { get; private set; }

    [Header("Slow Motion Settings")]
    [Tooltip("スローモーション倍率（0.2 = 20%の速度）")]
    [SerializeField] private float slowMotionTimeScale = 0.2f;

    [Tooltip("最大効果時間（秒）- ベース値")]
    [SerializeField] private float baseMaxDuration = 3f;

    [Header("Recovery Settings")]
    [Tooltip("通常回復速度（秒/秒）- スローモーション外での回復速度 - ベース値")]
    [SerializeField] private float baseNormalRecoveryRate = 0.1f;

    [Tooltip("ペナルティ遅延時間（秒）- 使い切った場合の待機時間")]
    [SerializeField] private float penaltyDelay = 3f;

    [Tooltip("ペナルティ時の遅い回復速度（秒/秒）")]
    [SerializeField] private float penaltyRecoveryRate = 0.05f;

    [Tooltip("ペナルティ復帰時の初期ゲージ割合（0〜1）。0.5で半分から回復開始。")]
    [SerializeField, Range(0f, 1f)] private float recoveryStartRatio = 0.5f;

    [Header("Visual Effects")]
    [Tooltip("画面効果（色調/彩度/ビネット/クロマティックアバレーション）が通常⇄フル効果に切り替わる速さ（1秒あたりの進捗。1なら約1秒、2なら約0.5秒で切り替わる）")]
    [SerializeField] private float visualEffectsTransitionSpeed = 1.5f;

    [Tooltip("スローモーション中の色調変更（HDR対応）")]
    [SerializeField] private Color slowMotionTint = new Color(0.7f, 0.9f, 1.2f, 1f);

    [Tooltip("スローモーション中のビネット強度（0-1）。画面の四隅を暗く落とす効果")]
    [SerializeField, Range(0f, 1f)] private float vignetteIntensity = 0.4f;

    [Tooltip("ビネットの境界のぼかし具合（0-1）。大きいほど暗くなる境界がなだらかになる")]
    [SerializeField, Range(0f, 1f)] private float vignetteSmoothness = 0.4f;

    [Tooltip("スローモーション中のクロマティックアバレーション強度（0-1）。画面端に近い部分でRGBの色がわずかにズレて見える効果（歪みではなく色ズレ）")]
    [SerializeField, Range(0f, 1f)] private float chromaticAberrationIntensity = 0.8f;

    [Tooltip("スローモーション中の彩度（-100で完全モノクロ、0で彩度変化なし）")]
    [SerializeField, Range(-100f, 0f)] private float slowMotionSaturation = -65f;

    [Header("Audio")]
    [Tooltip("スローモーション開始SE")]
    [SerializeField] private AudioClip slowMotionStartClip;

    [Tooltip("スローモーション終了SE")]
    [SerializeField] private AudioClip slowMotionEndClip;

    [Tooltip("使い切った時のSE")]
    [SerializeField] private AudioClip depletedClip;

    [Tooltip("回復開始SE")]
    [SerializeField] private AudioClip recoveryStartClip;

    [Tooltip("スローモーション中ループSE（スロー中ずっと鳴り続ける）")]
    [SerializeField] private AudioClip slowMotionLoopClip;

    [Tooltip("ペナルティ遅延中ループSE（回復停止中ずっと鳴り続ける）")]
    [SerializeField] private AudioClip penaltyLoopClip;

    [Tooltip("SE音量（ワンショット）")]
    [SerializeField, Range(0f, 1f)] private float seVolume = 1f;

    [Tooltip("ループSE音量")]
    [SerializeField, Range(0f, 1f)] private float loopSeVolume = 0.5f;

    [Header("BGM Low Pass Filter")]
    [Tooltip("ON: スローモーション中、BGMをこもった音（Low Pass Filter）にする")]
    [SerializeField] private bool useBgmLowPassFilter = true;

    [Tooltip("スローモーション中のカットオフ周波数（Hz）。低いほどこもって聞こえる")]
    [SerializeField] private float lowPassCutoffFrequency = 800f;

    [Tooltip("通常時のカットオフ周波数（Hz）。22000でフィルタ無しとほぼ同じ")]
    [SerializeField] private float normalCutoffFrequency = 22000f;

    [Tooltip("カットオフ周波数の切り替わる速さ（Hz/秒）。値を上げるほどすぐにこもる（0.2秒程度で切り替わる値がデフォルト）")]
    [SerializeField] private float lowPassTransitionSpeed = 100000f;

    [Header("Camera Zoom (Bullet Time)")]
    [Tooltip("ON: スローモーション中、カメラを少しズームインする")]
    [SerializeField] private bool useCameraZoom = true;

    [Tooltip("スローモーション中のOrthographic Size倍率（0.92なら8%ズームイン）")]
    [SerializeField, Range(0.5f, 1f)] private float zoomTargetSizeMultiplier = 0.92f;

    [Tooltip("ズームの変化速度（Orthographic Size単位/秒）。値を下げるほどゆっくりズームする")]
    [SerializeField] private float zoomTransitionSpeed = 0.4f;

    [Header("Afterimage Trail")]
    [Tooltip("ON: スローモーション中、画面内の弾・敵に薄い残像を残す")]
    [SerializeField] private bool useAfterimageTrail = true;

    [Tooltip("残像を対象とするレイヤー（弾・敵）")]
    [SerializeField] private LayerMask afterimageTargetLayers;

    [Tooltip("何秒おきに画面内をサンプリングして残像を生成するか。短くしすぎない（負荷対策）")]
    [SerializeField] private float afterimageSampleInterval = 0.05f;

    [Tooltip("1回のサンプリングで生成する残像の最大数。弾が多い場面でも負荷が急増しないための上限")]
    [SerializeField] private int afterimageMaxSpawnsPerSample = 6;

    [Tooltip("残像プールの固定サイズ。これを使い回すだけで、実行中に新規生成/破棄は一切行わない")]
    [SerializeField] private int afterimagePoolSize = 60;

    [Tooltip("残像の開始不透明度（0-1）")]
    [SerializeField, Range(0f, 1f)] private float afterimageStartAlpha = 0.4f;

    [Tooltip("残像が消えるまでの時間（秒）。長くするほどトレイルが長く見える")]
    [SerializeField] private float afterimageFadeDuration = 0.4f;

    [Tooltip("残像にかける発光色ティント（本体の色とブレンドする）")]
    [SerializeField] private Color afterimageTintColor = Color.white;

    [Tooltip("発光色ティントの強さ（0=本体の色そのまま、1=完全にティント色）")]
    [SerializeField, Range(0f, 1f)] private float afterimageTintStrength = 0.6f;

    [Tooltip("残像が生成された瞬間の拡大率（本体サイズに対する倍率）")]
    [SerializeField] private float afterimageStartScaleMultiplier = 1.25f;

    [Tooltip("残像が消える直前の縮小率（本体サイズに対する倍率）。開始→終了で滑らかに変化する")]
    [SerializeField] private float afterimageEndScaleMultiplier = 0.7f;

    [Tooltip("スローモーション中、トレイルの長さをさらに何倍に伸ばすか（1なら通常速度と同じ長さになるよう補正するだけ、2ならその倍の長さに見えるようにする）")]
    [SerializeField] private float afterimageSlowMoStretchFactor = 2f;

    [Header("References")]
    [SerializeField] private AudioSource audioSource;

    [Tooltip("ループSE専用AudioSource（未設定時は自動生成）")]
    [SerializeField] private AudioSource loopAudioSource;

    [SerializeField] private Volume postProcessVolume;

    // Runtime state
    private bool isSlowMotionActive = false;
    private float currentDuration; // 残り効果時間
    private bool isDepleted = false; // 使い切ったかどうか
    private float penaltyTimer = 0f; // ペナルティタイマー
    private bool wasSkillSelectionShowing = false; // スキル選択画面の前フレーム状態
    private float visualEffectsBlend = 0f; // 画面効果の現在の進捗（0=通常、1=フル効果）。UpdateVisualEffects()が毎フレーム目標値へ近づける

    // Skill bonus values (スキルによる加算値)
    private float maxDurationBonus = 0f;
    private float normalRecoveryRateBonus = 0f;

    // Post-processing components
    private ColorAdjustments colorAdjustments;
    private Vignette vignette;
    private ChromaticAberration chromaticAberration;

    // BGM Low Pass Filter
    private AudioLowPassFilter bgmLowPassFilter; // 見つかるまではnullのまま。UpdateBgmLowPassFilter()が毎フレーム探し直す

    // Camera Zoom
    private Camera mainCamera;
    private float baseOrthographicSize = -1f; // 未取得は-1。初回UpdateCameraZoom()でCamera.mainから取得する

    // Afterimage Trail（固定サイズプールのみ使用。実行中の新規Instantiate/Destroyは一切行わない）
    private struct AfterimageSlot
    {
        public GameObject go;
        public SpriteRenderer sr;
        public float remaining;
        public float baseAlpha;
        public Vector3 baseScale; // 複製元の等倍スケール（拡大率をかける前の値）
    }
    private AfterimageSlot[] afterimagePool;
    private int afterimageNextSlot;
    private float afterimageSampleTimer;
    private ContactFilter2D afterimageContactFilter;
    private readonly List<Collider2D> afterimageOverlapResults = new List<Collider2D>(32);
    private readonly List<SpriteRenderer> afterimageRendererScratch = new List<SpriteRenderer>(8); // GetComponentsInChildrenの使い回しバッファ

    // Public properties
    public bool IsSlowMotionActive => isSlowMotionActive;
    public float CurrentDuration => currentDuration;
    public float MaxDuration => baseMaxDuration + maxDurationBonus;
    public float NormalizedDuration => MaxDuration > 0 ? currentDuration / MaxDuration : 0f;
    public float NormalRecoveryRate => baseNormalRecoveryRate + normalRecoveryRateBonus;
    public bool IsInPenaltyDelay => isDepleted && penaltyTimer > 0f;

    /// <summary>
    /// ズーム前の基準Orthographic Size。未取得（-1）ならfalseを返す。
    /// CameraAnchoredTransform側が、カメラズーム演出中も本来の基準サイズで位置計算できるようにするための参照用
    /// </summary>
    public bool TryGetBaseOrthographicSize(out float size)
    {
        size = baseOrthographicSize;
        return useCameraZoom && baseOrthographicSize >= 0f;
    }

    /// <summary>
    /// カスタムタイムスケール（スローモーション中のみ0.2、それ以外は1.0）
    /// </summary>
    public float TimeScale => isSlowMotionActive ? slowMotionTimeScale : 1f;

    private static float MasterSEVolume => SoundSettingsManager.Instance != null ? SoundSettingsManager.Instance.SEVolume : 1f;

    /// <summary>スローモーションが開始された瞬間に発火（チュートリアル等の検知用）</summary>
    public event System.Action OnSlowMotionStarted;
    /// <summary>スローモーションゲージを使い切ってオーバーヒートした瞬間に発火（チュートリアル等の検知用）</summary>
    public event System.Action OnDepleted;

    private void Start()
    {
        // PauseManagerのポーズ/再開イベントを購読
        if (PauseManager.Instance != null)
        {
            PauseManager.Instance.OnPauseStarted += OnGamePaused;
            PauseManager.Instance.OnPauseEnded += OnGameResumed;
        }
    }

    private void OnGamePaused()
    {
        // スローモーション中の場合は停止（ポーズ解除後に意図せず継続しないようにする）
        // ホールドモード中にポーズした場合、ポーズ中にキー/ボタンを離してもStopSlowMotionが
        // 呼ばれないため、ここで明示的に停止する
        if (isSlowMotionActive)
            StopSlowMotion();
        else if (loopAudioSource != null && loopAudioSource.isPlaying)
            loopAudioSource.Pause();
    }

    private void OnGameResumed()
    {
        // スキル選択画面表示中は再開しない
        if (Game.UI.SkillSelectionUI.IsShowing) return;

        bool shouldResume = isSlowMotionActive || (isDepleted && penaltyTimer > 0f);
        if (shouldResume && loopAudioSource != null && loopAudioSource.clip != null)
            loopAudioSource.UnPause();
    }

    private void Awake()
    {
        // Singleton setup
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Initialize
        currentDuration = MaxDuration; // プロパティを使用（ベース値+ボーナス）
        isDepleted = false;
        penaltyTimer = 0f;

        // Get or create AudioSource（ワンショット用）
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
                audioSource = gameObject.AddComponent<AudioSource>();
        }

        // Get or create loopAudioSource（ループ用）
        if (loopAudioSource == null)
        {
            loopAudioSource = gameObject.AddComponent<AudioSource>();
            loopAudioSource.loop = true;
            loopAudioSource.playOnAwake = false;
        }

        // Get post-processing components
        if (postProcessVolume != null && postProcessVolume.profile != null)
        {
            postProcessVolume.profile.TryGet(out colorAdjustments);
            postProcessVolume.profile.TryGet(out vignette);
            postProcessVolume.profile.TryGet(out chromaticAberration);
        }

        InitAfterimagePool();
    }

    /// <summary>
    /// 残像プールを固定サイズで一度だけ生成する。以後の実行中はここで作ったオブジェクトを
    /// 使い回すだけで、新規Instantiate/Destroyは一切行わない（長時間戦闘でも負荷を一定に保つため）
    /// </summary>
    private void InitAfterimagePool()
    {
        if (!useAfterimageTrail || afterimagePoolSize <= 0) return;

        afterimagePool = new AfterimageSlot[afterimagePoolSize];
        for (int i = 0; i < afterimagePoolSize; i++)
        {
            // ★親を持たせない（SlowMotionManagerのtransformにscaleが入っていた場合、
            //   下のSpawnAfterimage()で行うlocalScale=lossyScaleの代入が二重スケールになるのを防ぐため）
            GameObject go = new GameObject($"SlowMotionAfterimage_{i}");
            go.SetActive(false);
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            afterimagePool[i] = new AfterimageSlot { go = go, sr = sr, remaining = 0f, baseAlpha = 0f };
        }

        afterimageContactFilter = new ContactFilter2D();
        afterimageContactFilter.SetLayerMask(afterimageTargetLayers);
        afterimageContactFilter.useTriggers = true;
    }

    private void Update()
    {
        // スキル選択画面の表示/非表示切り替えを検出してループSEを制御
        bool isSkillSelectionShowing = Game.UI.SkillSelectionUI.IsShowing;
        if (isSkillSelectionShowing != wasSkillSelectionShowing)
        {
            wasSkillSelectionShowing = isSkillSelectionShowing;
            if (isSkillSelectionShowing)
            {
                // スキル選択画面が開いた → ループSEを一時停止
                if (loopAudioSource != null && loopAudioSource.isPlaying)
                    loopAudioSource.Pause();
            }
            else
            {
                // スキル選択画面が閉じた → 必要ならループSEを再開
                bool shouldResume = isSlowMotionActive || (isDepleted && penaltyTimer > 0f);
                if (shouldResume && loopAudioSource != null && loopAudioSource.clip != null)
                    loopAudioSource.UnPause();
            }
        }

        if (isSlowMotionActive)
        {
            UpdateSlowMotion();
        }
        else
        {
            UpdateRecovery();
        }

        UpdateBgmLowPassFilter();
        UpdateCameraZoom();
        UpdateAfterimageTrail();
        UpdateVisualEffects();
    }

    /// <summary>
    /// スローモーション中、カメラのOrthographic Sizeを少しだけ縮めてズームインさせる（バレットタイム演出）。
    /// Camera.mainはシーンごとに変わりうるためキャッシュが無くなったら探し直す
    /// </summary>
    private void UpdateCameraZoom()
    {
        if (!useCameraZoom) return;

        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera == null) return;
            baseOrthographicSize = mainCamera.orthographicSize;
        }

        if (baseOrthographicSize < 0f) baseOrthographicSize = mainCamera.orthographicSize;

        float targetSize = isSlowMotionActive ? baseOrthographicSize * zoomTargetSizeMultiplier : baseOrthographicSize;
        mainCamera.orthographicSize = Mathf.MoveTowards(
            mainCamera.orthographicSize, targetSize, zoomTransitionSpeed * Time.unscaledDeltaTime);
    }

    /// <summary>
    /// 残像トレイルの更新入口。フェード処理は常時行い（消えかけの残像を最後まで消す）、
    /// 新規サンプリングはスローモーション中のみ、かつ一定間隔でしか行わない
    /// </summary>
    private void UpdateAfterimageTrail()
    {
        if (!useAfterimageTrail || afterimagePool == null) return;

        // ★弾・敵の移動量自体がTimeScaleで動かされているため、サンプリング間隔と
        //   フェード時間も同じTimeScaleで進めることで、スロー中は「実時間に対して間隔が
        //   間延びする」＝結果的にトレイルの長さが速度に引っ張られず一定に保たれる。
        //   （実時間のままだと、スロー中は弾がほとんど動かないうちに次のサンプルを撮ってしまい
        //   影同士が重なって短く見えていた）
        //   さらにafterimageSlowMoStretchFactorで割ることで、スロー中は通常時と同じ長さに
        //   揃えるだけでなく、その分だけ明確に長く見えるようにする
        float timeScaleForTrail = isSlowMotionActive ? (TimeScale / Mathf.Max(0.01f, afterimageSlowMoStretchFactor)) : 1f;
        float dt = Time.unscaledDeltaTime * timeScaleForTrail;

        UpdateAfterimageFades(dt);

        if (!isSlowMotionActive)
        {
            afterimageSampleTimer = 0f;
            return;
        }

        afterimageSampleTimer += dt;
        if (afterimageSampleTimer < afterimageSampleInterval) return;
        afterimageSampleTimer = 0f;

        SampleAndSpawnAfterimages();
    }

    /// <summary>
    /// プール内の残像を毎フレーム減衰させ、消え切ったものは非表示に戻す。
    /// 固定サイズ配列を舐めるだけなのでGCアロケーションは発生しない
    /// </summary>
    private void UpdateAfterimageFades(float dt)
    {
        for (int i = 0; i < afterimagePool.Length; i++)
        {
            if (afterimagePool[i].remaining <= 0f) continue;

            afterimagePool[i].remaining -= dt;
            if (afterimagePool[i].remaining <= 0f)
            {
                afterimagePool[i].remaining = 0f;
                afterimagePool[i].go.SetActive(false);
                continue;
            }

            float t = Mathf.Clamp01(afterimagePool[i].remaining / afterimageFadeDuration);
            Color c = afterimagePool[i].sr.color;
            c.a = afterimagePool[i].baseAlpha * t;
            afterimagePool[i].sr.color = c;

            // 生成直後(t=1)は拡大率、消える直前(t=0)は縮小率になるよう補間する
            float scaleMul = Mathf.Lerp(afterimageEndScaleMultiplier, afterimageStartScaleMultiplier, t);
            afterimagePool[i].go.transform.localScale = afterimagePool[i].baseScale * scaleMul;
        }
    }

    /// <summary>
    /// 画面内（カメラのビュー範囲）の弾・敵をサンプリングし、その場に薄い残像を残す。
    /// 1回あたりの生成数はafterimageMaxSpawnsPerSampleで頭打ちにし、弾幕が濃い場面でも
    /// 負荷が跳ね上がらないようにする（Physics2D.OverlapAreaも使い回しListでGC無しに実行）
    /// </summary>
    private void SampleAndSpawnAfterimages()
    {
        if (mainCamera == null) mainCamera = Camera.main;
        if (mainCamera == null) return;

        Vector2 center = mainCamera.transform.position;
        float halfHeight = mainCamera.orthographicSize;
        float halfWidth = halfHeight * mainCamera.aspect;
        Vector2 pointA = center - new Vector2(halfWidth, halfHeight);
        Vector2 pointB = center + new Vector2(halfWidth, halfHeight);

        afterimageOverlapResults.Clear();
        Physics2D.OverlapArea(pointA, pointB, afterimageContactFilter, afterimageOverlapResults);

        int found = afterimageOverlapResults.Count;
        if (found == 0) return;

        int spawnCount = Mathf.Min(afterimageMaxSpawnsPerSample, found);
        for (int i = 0; i < spawnCount; i++)
        {
            int pick = (found <= afterimageMaxSpawnsPerSample) ? i : Random.Range(0, found);
            Collider2D col = afterimageOverlapResults[pick];
            if (col == null) continue;

            SpriteRenderer source = FindDisplaySpriteRenderer(col);
            if (source == null) continue;

            SpawnAfterimage(source);
        }
    }

    /// <summary>
    /// 対象の実際に表示されているSpriteRendererを探す。
    /// GetComponentInChildren（単体版）は非表示の子を先に拾ってしまうことがあるため、
    /// 子要素を全部見て「有効かつスプライトを持つ最初の1枚」を採用する
    /// （GetComponentsInChildrenの非alloc版で使い回しListに詰めるためGCは発生しない）
    /// </summary>
    private SpriteRenderer FindDisplaySpriteRenderer(Collider2D col)
    {
        col.GetComponentsInChildren(false, afterimageRendererScratch);
        for (int i = 0; i < afterimageRendererScratch.Count; i++)
        {
            SpriteRenderer sr = afterimageRendererScratch[i];
            if (sr != null && sr.enabled && sr.sprite != null)
                return sr;
        }
        return null;
    }

    /// <summary>
    /// プールの次のスロット（古いものから順に再利用。新規生成はしない）に、
    /// 対象のスプライト・位置・向き・色を複製して薄く表示する
    /// </summary>
    private void SpawnAfterimage(SpriteRenderer source)
    {
        int slot = afterimageNextSlot;
        afterimageNextSlot = (afterimageNextSlot + 1) % afterimagePool.Length;

        Transform st = source.transform;
        GameObject go = afterimagePool[slot].go;
        SpriteRenderer sr = afterimagePool[slot].sr;

        go.transform.SetPositionAndRotation(st.position, st.rotation);
        Vector3 baseScale = st.lossyScale;
        go.transform.localScale = baseScale * afterimageStartScaleMultiplier;

        sr.sprite = source.sprite;
        sr.flipX = source.flipX;
        sr.flipY = source.flipY;
        sr.sortingLayerID = source.sortingLayerID;
        sr.sortingOrder = source.sortingOrder - 1;
        // ★マテリアルも複製元と同じものを使う（未設定だとUnityのデフォルトマテリアルのままになり、
        //   加算合成等の特殊なマテリアルを使うスプライトが正しく見えない/透明に近く見えなくなる原因になる）
        sr.sharedMaterial = source.sharedMaterial;

        // ★本体の色そのままだと「薄い分身」に見えて気づきにくいため、発光色にティントして
        //   軌跡エフェクトらしいはっきりした見た目にする
        Color c = Color.Lerp(source.color, afterimageTintColor, afterimageTintStrength);
        c.a = afterimageStartAlpha;
        sr.color = c;

        afterimagePool[slot].remaining = afterimageFadeDuration;
        afterimagePool[slot].baseAlpha = afterimageStartAlpha;
        afterimagePool[slot].baseScale = baseScale;
        go.SetActive(true);
    }

    /// <summary>
    /// BGMのLow Pass Filterを、スローモーション中かどうかに応じて滑らかに切り替える。
    /// GameplayBgmRandomPlayerはシーンごとに新規生成されるため、キャッシュが無くなったら探し直す
    /// （AudioLowPassFilterコンポーネント自体が無ければ実行時に追加する。カットオフ22000ならフィルタ無しとほぼ同じ挙動）
    /// </summary>
    private void UpdateBgmLowPassFilter()
    {
        if (!useBgmLowPassFilter) return;

        if (bgmLowPassFilter == null)
        {
            GameplayBgmRandomPlayer bgmPlayer = FindFirstObjectByType<GameplayBgmRandomPlayer>();
            if (bgmPlayer == null) return;

            bgmLowPassFilter = bgmPlayer.GetComponent<AudioLowPassFilter>();
            if (bgmLowPassFilter == null)
                bgmLowPassFilter = bgmPlayer.gameObject.AddComponent<AudioLowPassFilter>();
            bgmLowPassFilter.cutoffFrequency = normalCutoffFrequency;
        }

        // ★スキルカード3択画面（Time.timeScale=0で全体ポーズ中）もカードが配られ始める瞬間に
        //   IsShowingがtrueになるため、スローモーション中と同じ「こもった音」を適用する
        bool shouldMuffle = isSlowMotionActive || Game.UI.SkillSelectionUI.IsShowing;
        float targetFrequency = shouldMuffle ? lowPassCutoffFrequency : normalCutoffFrequency;
        bgmLowPassFilter.cutoffFrequency = Mathf.MoveTowards(
            bgmLowPassFilter.cutoffFrequency, targetFrequency, lowPassTransitionSpeed * Time.unscaledDeltaTime);
    }

    /// <summary>
    /// スローモーションを開始/終了トグル
    /// </summary>
    public void ToggleSlowMotion()
    {
        if (isSlowMotionActive)
        {
            // スローモーション中 → 終了
            StopSlowMotion();
        }
        else
        {
            // スローモーション外 → 開始
            if (currentDuration > 0f)
            {
                StartSlowMotion();
            }
        }
    }

    /// <summary>
    /// スローモーション開始
    /// </summary>
    public void StartSlowMotion()
    {
        if (currentDuration <= 0f) return;

        isSlowMotionActive = true;
        OnSlowMotionStarted?.Invoke();

        // SE再生
        PlaySound(slowMotionStartClip);
        PlayLoopSound(slowMotionLoopClip);

        // 画面効果はUpdate()内のUpdateVisualEffects()が毎フレーム徐々に近づけていく

        Debug.Log($"[SlowMotionManager] Slow motion started. TimeScale: {TimeScale}");
    }

    /// <summary>
    /// スローモーション終了
    /// </summary>
    public void StopSlowMotion()
    {
        isSlowMotionActive = false;

        // スローモーションループSE停止
        StopLoopSound();

        // SE再生
        PlaySound(slowMotionEndClip);

        // 画面効果はUpdate()内のUpdateVisualEffects()が毎フレーム徐々に戻していく

        // 使い切った場合の処理
        if (currentDuration <= 0f && !isDepleted)
        {
            isDepleted = true;
            penaltyTimer = penaltyDelay;
            SessionStats.AddOverheat();
            OnDepleted?.Invoke();
            PlaySound(depletedClip);
            PlayLoopSound(penaltyLoopClip); // ペナルティループSE開始
            Debug.Log($"[SlowMotionManager] Depleted! Penalty delay: {penaltyDelay}s");
        }

        Debug.Log($"[SlowMotionManager] Slow motion stopped. TimeScale: {TimeScale}");
    }

    /// <summary>
    /// スローモーション中の更新
    /// </summary>
    private void UpdateSlowMotion()
    {
        // 効果時間を減らす（実時間で減少）
        currentDuration -= Time.deltaTime;

        if (currentDuration <= 0f)
        {
            currentDuration = 0f;
            StopSlowMotion();
        }
    }

    /// <summary>
    /// 回復処理
    /// </summary>
    private void UpdateRecovery()
    {
        // ペナルティタイマー処理
        if (isDepleted && penaltyTimer > 0f)
        {
            penaltyTimer -= Time.deltaTime;

            if (penaltyTimer <= 0f)
            {
                penaltyTimer = 0f;
                isDepleted = false; // 一定時間経過でペナルティ解除（全回復を待たない）
                currentDuration = MaxDuration * recoveryStartRatio;
                StopLoopSound();   // ペナルティループSE停止
                PlaySound(recoveryStartClip);
                Debug.Log("[SlowMotionManager] Penalty ended, recovery started");
            }

            return; // ペナルティ中は回復しない
        }

        // 回復処理（ペナルティ解除後は常にNormalRecoveryRate）
        if (currentDuration < MaxDuration)
        {
            currentDuration += NormalRecoveryRate * Time.deltaTime;

            if (currentDuration >= MaxDuration)
                currentDuration = MaxDuration;
        }
    }

    /// <summary>
    /// 画面効果（色調・彩度・ビネット・クロマティックアバレーション）を、スローモーションの
    /// ON/OFFに応じて毎フレーム徐々に近づける。visualEffectsBlend（0=通常, 1=フル効果）という
    /// 単一の進捗値を介して全パラメータを同時にLerpすることで、瞬時切り替えの急激さを無くす
    /// </summary>
    private void UpdateVisualEffects()
    {
        if (postProcessVolume == null) return;

        float target = isSlowMotionActive ? 1f : 0f;
        visualEffectsBlend = Mathf.MoveTowards(visualEffectsBlend, target, visualEffectsTransitionSpeed * Time.unscaledDeltaTime);

        if (colorAdjustments != null)
        {
            // ★overrideStateも明示的にtrueにする。Inspector側でパラメータのチェックボックスを
            //   有効化し忘れていても、スクリプトからのvalue変更が反映されるようにするため
            colorAdjustments.colorFilter.overrideState = true;
            colorAdjustments.colorFilter.value = Color.Lerp(Color.white, slowMotionTint, visualEffectsBlend);
            colorAdjustments.saturation.overrideState = true;
            colorAdjustments.saturation.value = Mathf.Lerp(0f, slowMotionSaturation, visualEffectsBlend);
            colorAdjustments.active = true;
        }

        if (vignette != null)
        {
            vignette.intensity.overrideState = true;
            vignette.intensity.value = Mathf.Lerp(0f, vignetteIntensity, visualEffectsBlend);
            vignette.smoothness.overrideState = true;
            vignette.smoothness.value = vignetteSmoothness;
            vignette.active = true;
        }

        if (chromaticAberration != null)
        {
            chromaticAberration.intensity.overrideState = true;
            chromaticAberration.intensity.value = Mathf.Lerp(0f, chromaticAberrationIntensity, visualEffectsBlend);
            chromaticAberration.active = true;
        }
    }

    /// <summary>
    /// SE再生
    /// </summary>
    private void PlaySound(AudioClip clip)
    {
        if (clip != null && audioSource != null)
            audioSource.PlayOneShot(clip, seVolume * MasterSEVolume);
    }

    private void PlayLoopSound(AudioClip clip)
    {
        if (loopAudioSource == null) return;
        StopLoopSound();
        if (clip == null) return;
        loopAudioSource.clip = clip;
        loopAudioSource.volume = loopSeVolume * MasterSEVolume;
        loopAudioSource.Play();
    }

    private void StopLoopSound()
    {
        if (loopAudioSource != null && loopAudioSource.isPlaying)
            loopAudioSource.Stop();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        // PauseManagerのイベント購読解除
        if (PauseManager.Instance != null)
        {
            PauseManager.Instance.OnPauseStarted -= OnGamePaused;
            PauseManager.Instance.OnPauseEnded -= OnGameResumed;
        }
    }

    // ===== Skill System Integration =====

    /// <summary>
    /// スキルによる最大持続時間ボーナスを追加
    /// </summary>
    public void AddMaxDurationBonus(float bonus)
    {
        maxDurationBonus += bonus;

        // 現在のゲージ量もボーナス分増やす（スキル取得時に即座に反映）
        currentDuration += bonus;

        // MaxDurationを超えないようにClamp
        if (currentDuration > MaxDuration)
        {
            currentDuration = MaxDuration;
        }

        Debug.Log($"[SlowMotionManager] MaxDuration bonus added: +{bonus}s (Total MaxDuration: {MaxDuration}s, Current: {currentDuration:F2}s)");
    }

    /// <summary>
    /// スキルによる通常回復速度ボーナスを追加
    /// </summary>
    public void AddNormalRecoveryRateBonus(float bonus)
    {
        normalRecoveryRateBonus += bonus;
        Debug.Log($"[SlowMotionManager] NormalRecoveryRate bonus added: +{bonus} (Total: {NormalRecoveryRate} sec/sec)");
    }

    /// <summary>
    /// スキルボーナスをリセット（SkillManagerから呼ばれる）
    /// </summary>
    public void ResetSkillBonuses()
    {
        maxDurationBonus = 0f;
        normalRecoveryRateBonus = 0f;

        // ★リセット後のMaxDurationに合わせてcurrentDurationを調整
        if (currentDuration > MaxDuration)
        {
            currentDuration = MaxDuration;
        }

        Debug.Log($"[SlowMotionManager] Skill bonuses reset. MaxDuration: {MaxDuration}s, Current: {currentDuration:F2}s");
    }

#if UNITY_EDITOR
    /// <summary>
    /// Inspector表示用のデバッグ情報
    /// </summary>
    [ContextMenu("Debug Info")]
    private void DebugInfo()
    {
        Debug.Log($"[SlowMotionManager] Active: {isSlowMotionActive}, Duration: {currentDuration:F2}/{MaxDuration:F2}, TimeScale: {TimeScale}, Depleted: {isDepleted}, Penalty: {penaltyTimer:F2}");
    }
#endif
}
