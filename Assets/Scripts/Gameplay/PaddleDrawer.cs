using UnityEngine;
using System.Collections;
using UnityEngine.EventSystems;

public class PaddleDrawer : MonoBehaviour
{
    public static PaddleDrawer Instance { get; private set; }

    [Header("Prefab / Parent")]
    [SerializeField] private PaddleDot paddleDotPrefab;
    [SerializeField] private Transform paddleRoot;

    [Header("Draw Settings")]
    [SerializeField] private float dotSpacing = 0.25f;
    [SerializeField] private float zDepth = 0f;

    [Header("Managers")]
    [SerializeField] private PaddleCostManager costManager;
    [SerializeField] private StrokeManager strokeManager;

    [Header("Audio (Sources)")]
    [SerializeField] private AudioSource drawLoopSource;     // ループSE用（※使う場合）
    [SerializeField] private AudioSource sfxOneShotSource;   // 点SE/NG用
    [SerializeField] private AudioSource paddleHitSource;    // ヒットSE専用（遅れ/落ち対策）

    [Header("Audio (Clips)")]
    [SerializeField] private AudioClip normalDrawLoopClip;
    [SerializeField] private AudioClip redDrawLoopClip;
    [SerializeField] private AudioClip cantDrawClip;

    [Header("Audio (Dot Tick SE)")]
    [SerializeField] private bool useDotTickSE = true;
    [SerializeField] private AudioClip normalDotTickClip;
    [SerializeField] private AudioClip redDotTickClip;
    [SerializeField, Range(0f, 1f)] private float dotTickVolume = 1f;
    [SerializeField, Range(0f, 0.2f)] private float dotTickMinInterval = 0.04f;

    [Header("Audio (NG Tick SE)")]
    [SerializeField] private bool useNgTickSE = true;
    [SerializeField, Range(0f, 0.2f)] private float ngTickInterval = 0.06f;
    [SerializeField, Range(0f, 1f)] private float ngTickVolume = 1f;
    [SerializeField, Range(0f, 0.5f)] private float ngBurstSecondsOnInterrupt = 0.18f;

    [Header("Audio (Loop/OneShot Volumes)")]
    [SerializeField, Range(0f, 1f)] private float drawLoopVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float cantDrawVolume = 1f;

    [Header("Audio (Bullet Hit Paddle SE)")]
    [SerializeField] private AudioClip[] commonPaddleHitClips; // ★通常（白線/赤線 共通）ランダム候補
    [SerializeField] private AudioClip commonJustPaddleHitClip; // ★ジャスト（白線/赤線 共通）
    [SerializeField] private AudioClip normalPaddleHitClip; // 白線ヒットSE（フォールバック）
    [SerializeField] private AudioClip redPaddleHitClip;    // 赤線ヒットSE（フォールバック）
    [SerializeField, Range(0f, 1f)] private float paddleHitVolume = 1f;
    [SerializeField, Range(0f, 0.2f)] private float paddleHitMinInterval = 0.01f; // ★落ち対策で弱め

    [Header("Mode / Tap")]
    [SerializeField] private float doubleTapMaxInterval = 0.35f;
    [SerializeField] private float doubleTapMaxDistancePixels = 40f;
    [SerializeField] private float dragStartMovePixels = 8f;

    [Header("Cost Multipliers")]
    [SerializeField] private float normalCostMultiplier = 1f;
    [SerializeField] private float redCostMultiplier = 1.5f;

    [Header("Circle Rule (delegated to Stroke)")]
    [SerializeField] private float circleCloseDistance = 0.5f;
    [SerializeField] private float circleExtraLifeSeconds = 0f;
    [SerializeField] private int circleMinDots = 8;
    [SerializeField] private float circleMinPerimeter = 1.0f;
    [SerializeField] private float circleMinBoundsSize = 0.5f;
    [SerializeField] private float circleMaxAspect = 2.0f;

    [Header("Just Reflect (for dot config)")]
    [SerializeField] private float justWindowSeconds = 0.20f;
    [SerializeField] private float justDamageMultiplier = 1.50f;

    [Header("Lifetime Overrides (Skill System)")]
    [Tooltip("白線のLifetime上書き（-1で無効＝Prefabの値を使用）")]
    [SerializeField] private float normalLifetimeOverride = -1f;
    [Tooltip("赤線のLifetime上書き（-1で無効＝Prefabの値を使用）")]
    [SerializeField] private float redLifetimeOverride = 2f;

    [Header("Debug")]
    [SerializeField] private bool showCircleDebug = true;

    // =========================
    // Draw VFX (Per Dot)
    // =========================
    [Header("Draw VFX (Per Dot)")]
    [Tooltip("白線：点生成ごとに出すVFX（Particle System推奨）。未設定なら出ない。")]
    [SerializeField] private GameObject whiteDotVfxPrefab;

    [Tooltip("白線：残像VFX（任意）。未設定なら出ない。")]
    [SerializeField] private GameObject whiteAfterimageVfxPrefab;

    [Tooltip("赤線：点生成ごとに出すVFX（Particle System推奨）。未設定なら出ない。")]
    [SerializeField] private GameObject redDotVfxPrefab;

    [Tooltip("赤線：1フレ遅れ残像VFX（未設定なら出ない）。")]
    [SerializeField] private GameObject redAfterimageVfxPrefab;

    [Tooltip("VFXをこの親の下に生成する（推奨：ProjectileRoot）。未設定なら自動で ProjectileRoot を探す。")]
    [SerializeField] private Transform drawVfxParent;

    [Tooltip("生成したVFXを破棄する秒数（Particle側が Stop Action=Destroy なら 0 でもOK）。")]
    [SerializeField] private float drawVfxDestroySeconds = 0.35f;

    [Tooltip("赤線残像を「1フレ後」に出す。ON推奨。")]
    [SerializeField] private bool useRedAfterimageOneFrameDelay = true;

    [Tooltip("残像位置の微小ランダムオフセット（ワールド座標）。0ならズラさない。")]
    [SerializeField] private float afterimageRandomOffset = 0.03f;

    // =========================
    // Just Star VFX
    // =========================
    [Header("Just Star VFX (On Paddle Hit)")]
    [Tooltip("白線のJust成立時に出す星型VFX（Particle System推奨）。未設定なら出ない。")]
    [SerializeField] private GameObject justStarWhiteVfxPrefab;

    [Tooltip("赤線のJust成立時に出す星型VFX（Particle System推奨）。未設定なら出ない。")]
    [SerializeField] private GameObject justStarRedVfxPrefab;

    [Tooltip("Just星型VFXを破棄する秒数（Particle側が Stop Action=Destroy なら 0 でもOK）。")]
    [SerializeField] private float justStarDestroySeconds = 0.25f;

    [Tooltip("Just星型の位置を微小ランダムでズラす（ワールド座標）。0ならズラさない。")]
    [SerializeField] private float justStarRandomOffset = 0.02f;

    // =========================
    // Paddle Color Palette (Stroke Base Color)
    // =========================
    [Header("Paddle Color Palette (Stroke Base Color)")]
    [Tooltip("白線（Normal）の基準色候補。Stroke開始時にここから1色ランダム選択。未設定なら白。")]
    [SerializeField] private Color[] normalStrokeBaseColors;

    [Tooltip("赤線（RedAccel）の基準色候補。Stroke開始時にここから1色ランダム選択。未設定なら赤。")]
    [SerializeField] private Color[] redStrokeBaseColors;

    // ★変更：jitter を白/赤で分ける（今回）
    [Header("Dot Value Jitter (Per LineType)")]
    [Tooltip("白線（Normal）のDot明度ゆらぎ量（生成時1回のみ）。0なら揺らさない。例：0.06")]
    [SerializeField, Range(0f, 0.30f)] private float normalDotValueJitter = 0.06f;

    [Tooltip("赤線（RedAccel）のDot明度ゆらぎ量（生成時1回のみ）。0なら揺らさない。例：0.10")]
    [SerializeField, Range(0f, 0.30f)] private float redDotValueJitter = 0.10f;

    // =========================
    // Hardness (Per LineType)
    // =========================
    [Header("Hardness (Per LineType)")]
    [Tooltip("白線（Normal）の硬度。初期は 1。")]
    [SerializeField] private int normalHardness = 1;

    [Tooltip("赤線（RedAccel）の硬度。初期は 1。")]
    [SerializeField] private int redHardness = 1;

    // =========================
    // Accel Multiplier (Per LineType)
    // =========================
    [Header("Accel Multiplier (Per LineType)")]
    [Tooltip("白線（Normal）の反射時加速倍率。基礎は 1.0（加速なし）、スキルで上昇。")]
    [SerializeField] private float normalAccelMultiplier = 1.0f;

    [Tooltip("赤線（RedAccel）の反射時加速倍率。基礎は 1.1、スキルで上昇。")]
    [SerializeField] private float redAccelMultiplier = 1.1f;

    [Tooltip("反射時の最大加速回数。初期は 5。")]
    [SerializeField] private int accelMaxCount = 5;

    // =========================
    // Line Break VFX
    // =========================
    [Header("Line Break VFX (On Penetration)")]
    [Tooltip("白線が貫通で壊れた時に出すVFX。未設定なら出ない。")]
    [SerializeField] private GameObject lineBreakWhiteVfxPrefab;

    [Tooltip("赤線が貫通で壊れた時に出すVFX。未設定なら出ない。")]
    [SerializeField] private GameObject lineBreakRedVfxPrefab;

    [Tooltip("破壊VFXを破棄する秒数（Particle側が Stop Action=Destroy なら 0 でもOK）。")]
    [SerializeField] private float lineBreakDestroySeconds = 0.40f;

    // =========================
    // ★追加：Line Break SE（描画状態と独立）
    // =========================
    [Header("Audio (Line Break SE)")]
    [Tooltip("線が壊れた時のSE。未設定なら鳴らない。")]
    [SerializeField] private AudioClip lineBreakClip;

    [Tooltip("LineBreak SE音量（固定）")]
    [SerializeField, Range(0f, 1f)] private float lineBreakVolume = 1f;

    [Tooltip("LineBreak SEの最短間隔（秒）。0なら無制限。")]
    [SerializeField, Range(0f, 0.2f)] private float lineBreakMinInterval = 0f;

    [Tooltip("LineBreak SEを鳴らす専用AudioSource（推奨：PaddleDrawer本体の別AudioSource）。未設定なら paddleHitSource→sfxOneShot→drawLoop の順で使う。")]
    [SerializeField] private AudioSource lineBreakSource;

    private float lastLineBreakTime = -999f;

    // ★同フレーム多段（Dot群が同時に当たる等）を1回にまとめる
    private int lastLineBreakFrame = -999999;

    private enum PointerState { None, Down, Held, Up }

    private Camera cam;
    private Vector2 pointerPos;
    private Vector2 pointerDownPos;

    private bool waitingSecondTap;
    private float firstTapUpTime;
    private Vector2 firstTapUpPos;

    private bool isPreparingRed;
    private bool isDrawingNormal;
    private bool isDrawingRed;
    private bool blockedThisDrag;
    // UIボタン上でPointerDownが発生した場合、そのドラッグをブロックするフラグ
    private bool isBlockedByUI;

    private Stroke currentNormalStroke;
    private Stroke currentRedStroke;

    private Vector3 lastNormalPos;
    private Vector3 lastRedPos;

    private float lastDotTickTime;

    private bool isNgTickActive;
    private float nextNgTickTime;
    private float ngTickEndTime;

    private float lastPaddleHitTime;

    // Strokeごとの基準色
    private Color currentNormalBaseColor = Color.white;
    private Color currentRedBaseColor = Color.red;

    private void Awake()
    {
        Instance = this;

        cam = Camera.main;

        normalCostMultiplier = Mathf.Max(0f, normalCostMultiplier);
        redCostMultiplier = Mathf.Max(0f, redCostMultiplier);

        normalHardness = Mathf.Max(0, normalHardness);
        redHardness = Mathf.Max(0, redHardness);

        if (drawLoopSource != null)
        {
            drawLoopSource.playOnAwake = false;
            drawLoopSource.loop = true;
            drawLoopSource.spatialBlend = 0f;
            drawLoopSource.volume = drawLoopVolume;
        }

        if (sfxOneShotSource != null)
        {
            sfxOneShotSource.playOnAwake = false;
            sfxOneShotSource.loop = false;
            sfxOneShotSource.spatialBlend = 0f;
        }

        if (paddleHitSource != null)
        {
            paddleHitSource.playOnAwake = false;
            paddleHitSource.loop = false;
            paddleHitSource.spatialBlend = 0f;
        }

        // ★追加：LineBreakSource を使う場合も 2D固定
        if (lineBreakSource != null)
        {
            lineBreakSource.playOnAwake = false;
            lineBreakSource.loop = false;
            lineBreakSource.spatialBlend = 0f;
        }

        // VFX parent auto（Hierarchy前提：05_Game > Gameplay > ProjectileRoot）
        if (drawVfxParent == null)
        {
            GameObject pr = GameObject.Find("ProjectileRoot");
            if (pr != null) drawVfxParent = pr.transform;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        // スキル選択画面表示中は入力を無効化
        if (Game.UI.SkillSelectionUI.IsShowing) return;

        // ポーズ中は入力を無効化
        if (PauseManager.Instance != null && PauseManager.Instance.IsPaused) return;

        costManager?.SetDrawingState(isDrawingNormal, isDrawingRed);
        UpdateNgTick();

        if (!ReadPointer(out PointerState state, out Vector2 pos)) return;

        pointerPos = pos;

        if (waitingSecondTap && Time.time - firstTapUpTime > doubleTapMaxInterval)
            waitingSecondTap = false;

        if (state == PointerState.Down)
        {
            pointerDownPos = pos;
            blockedThisDrag = false;

            StopNgTick();

            bool canArmRed =
                waitingSecondTap &&
                Time.time - firstTapUpTime <= doubleTapMaxInterval &&
                Vector2.Distance(pos, firstTapUpPos) <= doubleTapMaxDistancePixels;

            isPreparingRed = canArmRed;
            waitingSecondTap = false;
        }
        else if (state == PointerState.Held)
        {
            float moved = Vector2.Distance(pos, pointerDownPos);

            if (isPreparingRed)
            {
                if (!isDrawingRed && !blockedThisDrag && moved >= dragStartMovePixels)
                    BeginRed();

                if (isDrawingRed)
                    DrawRed();
            }
            else
            {
                if (!isDrawingNormal && !blockedThisDrag && moved >= dragStartMovePixels)
                    BeginNormal();

                if (isDrawingNormal)
                    DrawNormal();
            }
        }
        else if (state == PointerState.Up)
        {
            StopNgTick();

            if (isDrawingRed) EndRed();
            else if (isDrawingNormal) EndNormal();
            else
            {
                waitingSecondTap = true;
                firstTapUpTime = Time.time;
                firstTapUpPos = pos;
            }
        }

        costManager?.SetDrawingState(isDrawingNormal, isDrawingRed);
    }

    // ===== ヒットSE（PaddleDot から呼ぶ）=====
    public void PlayPaddleHitSE(PaddleDot.LineType type)
    {
        PlayPaddleHitSE(type, false);
    }

    public void PlayPaddleHitSE(PaddleDot.LineType type, bool isJust)
    {
        AudioSource src = paddleHitSource != null ? paddleHitSource
                        : (sfxOneShotSource != null ? sfxOneShotSource : drawLoopSource);
        if (src == null) return;

        AudioClip clip = null;

        if (isJust)
        {
            clip = commonJustPaddleHitClip;
        }
        else
        {
            if (commonPaddleHitClips != null && commonPaddleHitClips.Length > 0)
            {
                int index = Random.Range(0, commonPaddleHitClips.Length);
                clip = commonPaddleHitClips[index];
            }
        }

        if (clip == null)
        {
            if (type == PaddleDot.LineType.Normal) clip = normalPaddleHitClip;
            else if (type == PaddleDot.LineType.RedAccel) clip = redPaddleHitClip;
        }

        if (clip == null) return;

        float now = Time.time;
        if (paddleHitMinInterval > 0f && now - lastPaddleHitTime < paddleHitMinInterval) return;

        lastPaddleHitTime = now;

        // SoundSettingsManagerのSE音量を適用
        float finalVolume = paddleHitVolume * (SoundSettingsManager.Instance != null ? SoundSettingsManager.Instance.SEVolume : 1f);
        src.PlayOneShot(clip, finalVolume);
    }

    // PaddleDot から呼ぶ「Just星型VFX」
    public void SpawnJustStarVfx(PaddleDot.LineType type, Vector3 worldPos)
    {
        GameObject prefab = null;

        if (type == PaddleDot.LineType.Normal) prefab = justStarWhiteVfxPrefab;
        else if (type == PaddleDot.LineType.RedAccel) prefab = justStarRedVfxPrefab;

        if (prefab == null) return;

        Vector3 p = worldPos;
        p.z = zDepth;

        float ofs = Mathf.Max(0f, justStarRandomOffset);
        if (ofs > 0f)
        {
            p.x += Random.Range(-ofs, ofs);
            p.y += Random.Range(-ofs, ofs);
        }

        SpawnVfx(prefab, p, justStarDestroySeconds);
    }

    // ★追加：線破壊VFX（貫通時）
    public void SpawnLineBreakVfx(PaddleDot.LineType type, Vector3 worldPos)
    {
        GameObject prefab = null;

        if (type == PaddleDot.LineType.Normal) prefab = lineBreakWhiteVfxPrefab;
        else if (type == PaddleDot.LineType.RedAccel) prefab = lineBreakRedVfxPrefab;

        if (prefab == null) return;

        Vector3 p = worldPos;
        p.z = zDepth;

        SpawnVfx(prefab, p, lineBreakDestroySeconds);
    }

    // ★追加：Strokeを即破断（PaddleDotから呼ぶ）
    public void ForceBreakStroke(Stroke stroke, PaddleDot.LineType type, Vector3 worldPos)
    {
        if (stroke == null) return;

        // もし描画中のStrokeなら、描画状態も止める
        if (stroke == currentNormalStroke)
        {
            StopDrawLoop();
            isDrawingNormal = false;
            currentNormalStroke = null;
        }
        else if (stroke == currentRedStroke)
        {
            StopDrawLoop();
            isDrawingRed = false;
            currentRedStroke = null;
        }

        // 終点は破断位置に寄せる（Stroke側が持っている場合だけ反映）
        stroke.SetEndPos(worldPos);

        // Strokeの終了処理（StrokeManagerの本数管理に乗せる意図）
        stroke.Finish(0f);

        // 見た目として即壊すため、Stroke自体も即破棄
        Destroy(stroke.gameObject);
    }

    // =========================
    // ★追加：LineBreak通知（CS1061の根本修正）
    // =========================
    public void NotifyLineBreakThisFrame(PaddleDot.LineType type, Vector3 worldPos)
    {
        // 同フレーム多段は1回にまとめる（Dot群が同時に当たる等）
        int f = Time.frameCount;
        if (f == lastLineBreakFrame) return;
        lastLineBreakFrame = f;

        // まずVFX（既存メソッド）
        SpawnLineBreakVfx(type, worldPos);

        // SE（描画状態と独立）
        PlayLineBreakSe();
    }

    private void PlayLineBreakSe()
    {
        if (lineBreakClip == null) return;

        float now = Time.unscaledTime;
        float minI = Mathf.Max(0f, lineBreakMinInterval);
        if (minI > 0f)
        {
            if ((now - lastLineBreakTime) < minI) return;
        }
        lastLineBreakTime = now;

        AudioSource src = lineBreakSource != null ? lineBreakSource
                        : (paddleHitSource != null ? paddleHitSource
                        : (sfxOneShotSource != null ? sfxOneShotSource : drawLoopSource));
        if (src == null) return;

        // SoundSettingsManagerのSE音量を適用
        float finalVolume = lineBreakVolume * (SoundSettingsManager.Instance != null ? SoundSettingsManager.Instance.SEVolume : 1f);
        src.PlayOneShot(lineBreakClip, finalVolume);
    }

    private void BeginNormal()
    {
        if (strokeManager != null && !strokeManager.CanStartStroke())
        {
            OnCantStartDraw();
            blockedThisDrag = true;
            return;
        }
        if (costManager != null && !costManager.CanConsumeLeft(0f))
        {
            OnCantStartDraw();
            blockedThisDrag = true;
            return;
        }

        StopNgTick();

        // Stroke開始時に基準色決定（白線）
        currentNormalBaseColor = PickStrokeBaseColor(PaddleDot.LineType.Normal);

        currentNormalStroke = strokeManager?.CreateStroke(paddleRoot, PaddleDot.LineType.Normal);
        // ★C4: 円判定猶予時間延長スキルを適用
        float normalCircleGate = GetEffectiveLifetime(PaddleDot.LineType.Normal);
        if (Game.Skills.SkillManager.Instance != null)
        {
            normalCircleGate += Game.Skills.SkillManager.Instance.GetCircleTimeExtension();
        }
        currentNormalStroke?.SetCircleGateSeconds(normalCircleGate);
        currentNormalStroke?.ConfigureCircleRule(circleCloseDistance, circleExtraLifeSeconds, circleMinDots, circleMinPerimeter, circleMinBoundsSize, circleMaxAspect);

        StopDrawLoop();

        isDrawingNormal = true;

        Vector3 p = GetWorld(pointerPos);
        lastNormalPos = p;
        currentNormalStroke?.SetStartPos(p);
        currentNormalStroke?.MarkStrokeStartNow();

        SpawnDot(p, PaddleDot.LineType.Normal, currentNormalStroke);
    }

    private void EndNormal()
    {
        StopDrawLoop();
        isDrawingNormal = false;

        currentNormalStroke?.SetEndPos(GetWorld(pointerPos));
        currentNormalStroke?.Finish(GetEffectiveLifetime(PaddleDot.LineType.Normal));
        currentNormalStroke = null;
    }

    private void DrawNormal()
    {
        Vector3 now = GetWorld(pointerPos);
        float dist = Vector3.Distance(now, lastNormalPos);
        if (dist < dotSpacing) return;

        int steps = Mathf.FloorToInt(dist / dotSpacing);
        Vector3 dir = (now - lastNormalPos).normalized;

        Vector3 prev = lastNormalPos;

        for (int i = 0; i < steps; i++)
        {
            Vector3 p = prev + dir * dotSpacing;
            float cost = Vector3.Distance(p, prev) * normalCostMultiplier;

            if (costManager != null && !costManager.TryConsumeLeft(cost))
            {
                OnInterruptedDraw();
                EndNormal();
                return;
            }

            SpawnDot(p, PaddleDot.LineType.Normal, currentNormalStroke);
            prev = p;
        }

        lastNormalPos = now;
    }

    private void BeginRed()
    {
        if (strokeManager != null && !strokeManager.CanStartStroke())
        {
            OnCantStartDraw();
            blockedThisDrag = true;
            return;
        }
        if (costManager != null && !costManager.CanConsumeRed(0f))
        {
            OnCantStartDraw();
            blockedThisDrag = true;
            return;
        }

        StopNgTick();

        // Stroke開始時に基準色決定（赤線）
        currentRedBaseColor = PickStrokeBaseColor(PaddleDot.LineType.RedAccel);

        currentRedStroke = strokeManager?.CreateStroke(paddleRoot, PaddleDot.LineType.RedAccel);
        // ★C4: 円判定猶予時間延長スキルを適用
        float redCircleGate = GetEffectiveLifetime(PaddleDot.LineType.RedAccel);
        if (Game.Skills.SkillManager.Instance != null)
        {
            redCircleGate += Game.Skills.SkillManager.Instance.GetCircleTimeExtension();
        }
        currentRedStroke?.SetCircleGateSeconds(redCircleGate);
        currentRedStroke?.ConfigureCircleRule(circleCloseDistance, circleExtraLifeSeconds, circleMinDots, circleMinPerimeter, circleMinBoundsSize, circleMaxAspect);

        StopDrawLoop();

        isDrawingRed = true;

        Vector3 p = GetWorld(pointerPos);
        lastRedPos = p;
        currentRedStroke?.SetStartPos(p);
        currentRedStroke?.MarkStrokeStartNow();

        SpawnDot(p, PaddleDot.LineType.RedAccel, currentRedStroke);
    }

    private void EndRed()
    {
        StopDrawLoop();
        isDrawingRed = false;

        currentRedStroke?.SetEndPos(GetWorld(pointerPos));
        currentRedStroke?.Finish(GetEffectiveLifetime(PaddleDot.LineType.RedAccel));
        currentRedStroke = null;
    }

    private void DrawRed()
    {
        Vector3 now = GetWorld(pointerPos);
        float dist = Vector3.Distance(now, lastRedPos);
        if (dist < dotSpacing) return;

        int steps = Mathf.FloorToInt(dist / dotSpacing);
        Vector3 dir = (now - lastRedPos).normalized;

        Vector3 prev = lastRedPos;

        for (int i = 0; i < steps; i++)
        {
            Vector3 p = prev + dir * dotSpacing;
            float cost = Vector3.Distance(p, prev) * redCostMultiplier;

            if (costManager != null && !costManager.TryConsumeRed(cost))
            {
                OnInterruptedDraw();
                EndRed();
                return;
            }

            SpawnDot(p, PaddleDot.LineType.RedAccel, currentRedStroke);
            prev = p;
        }

        lastRedPos = now;
    }

    private void TryPlayDotTick(PaddleDot.LineType type)
    {
        if (!useDotTickSE) return;

        AudioSource src = sfxOneShotSource != null ? sfxOneShotSource : drawLoopSource;
        if (src == null) return;

        AudioClip clip = null;
        if (type == PaddleDot.LineType.Normal) clip = normalDotTickClip;
        else if (type == PaddleDot.LineType.RedAccel) clip = redDotTickClip;
        if (clip == null) return;

        float now = Time.time;
        if (dotTickMinInterval > 0f && now - lastDotTickTime < dotTickMinInterval) return;

        lastDotTickTime = now;

        // SoundSettingsManagerのSE音量を適用
        float finalVolume = dotTickVolume * (SoundSettingsManager.Instance != null ? SoundSettingsManager.Instance.SEVolume : 1f);
        src.PlayOneShot(clip, finalVolume);
    }

    private void OnCantStartDraw()
    {
        if (!useNgTickSE)
        {
            PlayCantDrawOneShot();
            return;
        }
        StartNgTickContinuous();
    }

    private void OnInterruptedDraw()
    {
        if (!useNgTickSE)
        {
            PlayCantDrawOneShot();
            return;
        }
        StartNgTickBurst(ngBurstSecondsOnInterrupt);
    }

    private void PlayCantDrawOneShot()
    {
        if (cantDrawClip == null) return;

        AudioSource src = sfxOneShotSource != null ? sfxOneShotSource : drawLoopSource;
        if (src == null) return;

        // SoundSettingsManagerのSE音量を適用
        float finalVolume = cantDrawVolume * (SoundSettingsManager.Instance != null ? SoundSettingsManager.Instance.SEVolume : 1f);
        src.PlayOneShot(cantDrawClip, finalVolume);
    }

    private void StartNgTickContinuous()
    {
        if (cantDrawClip == null) return;
        isNgTickActive = true;
        ngTickEndTime = 0f;
        nextNgTickTime = 0f;
    }

    private void StartNgTickBurst(float seconds)
    {
        if (cantDrawClip == null) return;
        isNgTickActive = true;
        ngTickEndTime = Time.time + Mathf.Max(0f, seconds);
        nextNgTickTime = 0f;
    }

    private void StopNgTick()
    {
        isNgTickActive = false;
        ngTickEndTime = 0f;
        nextNgTickTime = 0f;
    }

    private void UpdateNgTick()
    {
        if (!isNgTickActive) return;

        if (ngTickEndTime > 0f && Time.time >= ngTickEndTime)
        {
            StopNgTick();
            return;
        }

        if (Time.time < nextNgTickTime) return;

        AudioSource src = sfxOneShotSource != null ? sfxOneShotSource : drawLoopSource;
        if (src == null) return;

        // SoundSettingsManagerのSE音量を適用
        float finalVolume = ngTickVolume * (SoundSettingsManager.Instance != null ? SoundSettingsManager.Instance.SEVolume : 1f);
        src.PlayOneShot(cantDrawClip, finalVolume);

        float interval = Mathf.Max(0.01f, ngTickInterval);
        nextNgTickTime = Time.time + interval;
    }

    private void StopDrawLoop()
    {
        if (drawLoopSource == null) return;
        if (drawLoopSource.isPlaying) drawLoopSource.Stop();
    }

    private void SpawnDot(Vector3 worldPos, PaddleDot.LineType type, Stroke stroke)
    {
        if (paddleDotPrefab == null) return;

        Vector3 p = worldPos;
        p.z = zDepth;

        Transform parent = stroke != null ? stroke.transform : paddleRoot;
        PaddleDot dot = Instantiate(paddleDotPrefab, p, Quaternion.identity, parent);

        // Strokeの基準色
        Color baseColor = (type == PaddleDot.LineType.Normal) ? currentNormalBaseColor : currentRedBaseColor;

        // ★白/赤で jitter を切り替える
        float jitter = (type == PaddleDot.LineType.Normal) ? normalDotValueJitter : redDotValueJitter;

        // ★硬度（白/赤）
        int h = (type == PaddleDot.LineType.Normal) ? normalHardness : redHardness;

        // ★加速倍率（白/赤）
        float accelMul = (type == PaddleDot.LineType.Normal) ? normalAccelMultiplier : redAccelMultiplier;

        // ★C1: ジャスト反射猶予時間延長スキルを適用
        float effectiveJustWindow = justWindowSeconds;
        if (Game.Skills.SkillManager.Instance != null)
        {
            effectiveJustWindow += Game.Skills.SkillManager.Instance.GetJustWindowExtension();
        }

        dot.Configure(type, accelMul, accelMaxCount, effectiveJustWindow, justDamageMultiplier, baseColor, jitter, h);

        // ★Lifetime設定（白線/赤線で異なる維持時間）
        dot.LifeTime = GetEffectiveLifetime(type);

        TryPlayDotTick(type);

        // 点ごとVFX（白/赤 + 赤のみ1フレ遅れ残像）
        SpawnDrawVfxPerDot(p, type);
    }

    private void SpawnDrawVfxPerDot(Vector3 worldPos, PaddleDot.LineType type)
    {
        if (type == PaddleDot.LineType.Normal)
        {
            if (whiteDotVfxPrefab != null)
            {
                SpawnVfx(whiteDotVfxPrefab, worldPos, drawVfxDestroySeconds);
            }

            if (whiteAfterimageVfxPrefab != null)
            {
                SpawnVfx(whiteAfterimageVfxPrefab, worldPos, drawVfxDestroySeconds);
            }

            return;
        }

        if (type == PaddleDot.LineType.RedAccel)
        {
            if (redDotVfxPrefab != null)
            {
                SpawnVfx(redDotVfxPrefab, worldPos, drawVfxDestroySeconds);
            }

            if (redAfterimageVfxPrefab != null)
            {
                if (useRedAfterimageOneFrameDelay)
                {
                    StartCoroutine(SpawnRedAfterimageNextFrame(worldPos));
                }
                else
                {
                    SpawnVfx(redAfterimageVfxPrefab, worldPos, drawVfxDestroySeconds);
                }
            }

            return;
        }
    }

    private IEnumerator SpawnRedAfterimageNextFrame(Vector3 worldPos)
    {
        yield return null;

        Vector3 p = worldPos;

        float ofs = Mathf.Max(0f, afterimageRandomOffset);
        if (ofs > 0f)
        {
            p.x += Random.Range(-ofs, ofs);
            p.y += Random.Range(-ofs, ofs);
        }

        SpawnVfx(redAfterimageVfxPrefab, p, drawVfxDestroySeconds);
    }

    private void SpawnVfx(GameObject prefab, Vector3 worldPos, float destroySeconds)
    {
        if (prefab == null) return;

        Transform parent = drawVfxParent;

        GameObject vfx = Instantiate(prefab, worldPos, Quaternion.identity, parent);
        if (vfx == null) return;

        float sec = destroySeconds;
        if (sec > 0f)
        {
            Destroy(vfx, sec);
        }
    }

    private Vector3 GetWorld(Vector2 screenPos)
    {
        if (cam == null) cam = Camera.main;

        Vector3 s = screenPos;
        s.z = -cam.transform.position.z;
        Vector3 w = cam.ScreenToWorldPoint(s);
        w.z = zDepth;
        return w;
    }

    private bool ReadPointer(out PointerState state, out Vector2 pos)
    {
        state = PointerState.None;
        pos = Vector2.zero;

        if (Input.touchCount > 0)
        {
            // ホールドモードでスローモーションボタンをホールド中の場合、
            // Touch 0 はスローモーション用に占有されているため Touch 1 を描画入力に使用する
            bool slowHolding = SlowMotionUIManager.Instance != null
                               && SlowMotionUIManager.Instance.UseHoldMode
                               && SlowMotionUIManager.Instance.IsHoldingButton;

            int touchIndex;
            if (slowHolding)
            {
                if (Input.touchCount <= 1) return false; // 2本目の指がなければ描画しない
                touchIndex = 1;
            }
            else
            {
                touchIndex = 0;
            }

            Touch t = Input.GetTouch(touchIndex);
            pos = t.position;

            if (t.phase == TouchPhase.Began) { state = PointerState.Down; return true; }
            if (t.phase == TouchPhase.Moved || t.phase == TouchPhase.Stationary) { state = PointerState.Held; return true; }
            if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled) { state = PointerState.Up; return true; }
            return false;
        }

        pos = Input.mousePosition;

        if (Input.GetMouseButtonDown(0))
        {
            // ホールドモードでUIボタン（タッチ/マウス）を押している間のみブロック
            // スペースキー等のキーボードホールドはIsHoldingButton=falseなのでブロックしない
            bool holdingUI = SlowMotionUIManager.Instance != null
                             && SlowMotionUIManager.Instance.UseHoldMode
                             && SlowMotionUIManager.Instance.IsHoldingButton;
            if (holdingUI)
            {
                isBlockedByUI = true;
                return false;
            }
            isBlockedByUI = false;
            state = PointerState.Down;
            return true;
        }
        if (Input.GetMouseButton(0))
        {
            if (isBlockedByUI) return false;
            state = PointerState.Held;
            return true;
        }
        if (Input.GetMouseButtonUp(0))
        {
            isBlockedByUI = false;
            state = PointerState.Up;
            return true;
        }

        return false;
    }

    private Color PickStrokeBaseColor(PaddleDot.LineType type)
    {
        Color fallback = (type == PaddleDot.LineType.RedAccel) ? Color.red : Color.white;

        Color[] palette = (type == PaddleDot.LineType.RedAccel) ? redStrokeBaseColors : normalStrokeBaseColors;
        if (palette == null || palette.Length == 0) return fallback;

        int idx = Random.Range(0, palette.Length);
        Color c = palette[idx];

        if (c.a <= 0f) c.a = 1f;
        return c;
    }

    // =========================================================
    // Skill System Getters/Setters
    // =========================================================

    public int NormalHardness => normalHardness;
    public int RedHardness => redHardness;
    public float JustWindowSeconds => justWindowSeconds;
    public float JustDamageMultiplier => justDamageMultiplier;
    public float NormalAccelMultiplier => normalAccelMultiplier;
    public float RedAccelMultiplier => redAccelMultiplier;
    public int AccelMaxCount => accelMaxCount;

    /// <summary>
    /// 有効なLifetimeを取得（オーバーライドがあればそれを、なければPrefabの値）
    /// </summary>
    private float GetEffectiveLifetime(PaddleDot.LineType type)
    {
        if (type == PaddleDot.LineType.Normal)
        {
            return normalLifetimeOverride >= 0f ? normalLifetimeOverride : paddleDotPrefab.LifeTime;
        }
        else
        {
            return redLifetimeOverride >= 0f ? redLifetimeOverride : paddleDotPrefab.LifeTime;
        }
    }

    /// <summary>
    /// 白線のHardnessを設定（スキルシステム用）
    /// </summary>
    public void SetNormalHardness(int value)
    {
        normalHardness = Mathf.Max(0, value);
    }

    /// <summary>
    /// 赤線のHardnessを設定（スキルシステム用）
    /// </summary>
    public void SetRedHardness(int value)
    {
        redHardness = Mathf.Max(0, value);
    }

    /// <summary>
    /// 白線のLifetimeを設定（スキルシステム用）
    /// </summary>
    public void SetNormalLifetime(float value)
    {
        normalLifetimeOverride = Mathf.Max(0f, value);
    }

    /// <summary>
    /// 赤線のLifetimeを設定（スキルシステム用）
    /// </summary>
    public void SetRedLifetime(float value)
    {
        redLifetimeOverride = Mathf.Max(0f, value);
    }

    /// <summary>
    /// 白線のLifetimeを取得（UI表示用）
    /// </summary>
    public float NormalLifetime => normalLifetimeOverride >= 0f ? normalLifetimeOverride : (paddleDotPrefab != null ? paddleDotPrefab.LifeTime : 0f);

    /// <summary>
    /// 赤線のLifetimeを取得（UI表示用）
    /// </summary>
    public float RedLifetime => redLifetimeOverride >= 0f ? redLifetimeOverride : (paddleDotPrefab != null ? paddleDotPrefab.LifeTime : 0f);

    /// <summary>
    /// Just反射のウィンドウ時間を設定（スキルシステム用）
    /// </summary>
    public void SetJustWindowSeconds(float value)
    {
        justWindowSeconds = Mathf.Max(0f, value);
    }

    /// <summary>
    /// Just反射のダメージ倍率を設定（スキルシステム用）
    /// </summary>
    public void SetJustDamageMultiplier(float value)
    {
        justDamageMultiplier = Mathf.Max(1f, value);
    }

    /// <summary>
    /// 白線の反射時加速倍率を設定（スキルシステム用）
    /// </summary>
    public void SetNormalAccelMultiplier(float value)
    {
        normalAccelMultiplier = Mathf.Max(0.01f, value);
    }

    /// <summary>
    /// 赤線の反射時加速倍率を設定（スキルシステム用）
    /// </summary>
    public void SetRedAccelMultiplier(float value)
    {
        redAccelMultiplier = Mathf.Max(0.01f, value);
    }

    /// <summary>
    /// 反射時の最大加速回数を設定（スキルシステム用）
    /// </summary>
    public void SetAccelMaxCount(int value)
    {
        accelMaxCount = Mathf.Max(0, value);
    }
}
