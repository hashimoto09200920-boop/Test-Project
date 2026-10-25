using UnityEngine;
using System.Collections;
using System.Collections.Generic;
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
    [Tooltip("白線・赤線を引く音を、クリップの先頭からこの秒数だけ進めた位置から鳴らす（音の出だしが実際の線より遅く聞こえるのを詰める。2026/10/25）。0で先頭から")]
    [SerializeField, Range(0f, 0.5f)] private float dotTickStartOffsetSeconds = 0.045f;

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
    [Tooltip("SEファイル先頭の無音区間をスキップして早く聞こえるようにする秒数")]
    [SerializeField, Range(0f, 0.5f)] private float paddleHitSeStartOffsetSeconds = 0.1f;

    [Header("Mode / Tap")]
    [SerializeField] private float doubleTapMaxInterval = 0.35f;
    [SerializeField] private float doubleTapMaxDistancePixels = 40f;
    [SerializeField] private float dragStartMovePixels = 8f;

    [Header("HUD Touch Ignore（HUD内のタッチは線に使わない）")]
    [Tooltip("HUDの範囲（05_Game > Canvas > SkillHUD）。この中で触れ始めたタッチは、離すまで線を引く指として使わない" +
             "（HUD内はスローモーションのボタンなどのUIだけが反応する）。スマホを持つ手のひらがHUDに触れ、その指が線を引く指に選ばれて" +
             "本当に描きたい指が無視される不具合の対策（2026/10/25・OPPOで確認）。未設定ならHUDによる除外はしない")]
    [SerializeField] private RectTransform hudRect;

    [Header("Long Press (Red Line)")]
    [Tooltip("長押し判定時間（秒）。この時間押し続けると赤線モードに切り替わる。")]
    [SerializeField] private float longPressSeconds = 0.3f;
    [Tooltip("長押し成立時のSE。")]
    [SerializeField] private AudioClip longPressActivateClip;
    [SerializeField, Range(0f, 1f)] private float longPressActivateVolume = 1f;
    [Tooltip("長押し成立時のパーティクル（リング拡散）。未設定なら出ない。")]
    [SerializeField] private GameObject longPressActivateVfxPrefab;
    [Tooltip("長押しVFXを破棄する秒数。")]
    [SerializeField] private float longPressActivateVfxDestroySeconds = 0.5f;

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
    [Tooltip("Just反射の猶予時間（ベース値。白線はこの値をそのまま使用）")]
    [SerializeField] private float justWindowSeconds = 0.15f;
    [Tooltip("赤線のみに加算されるJust猶予時間ボーナス（白線との差別化。白線には適用しない）")]
    [SerializeField] private float redJustWindowBonus = 0.15f;
    [Tooltip("白線のJustダメージ倍率")]
    [SerializeField] private float justDamageMultiplier = 1.50f;
    [Tooltip("赤線のJustダメージ倍率（白線との差別化のため高めに設定）")]
    [SerializeField] private float redJustDamageMultiplier = 2.5f;

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
    // Normal Reflect VFX
    // =========================
    [Header("Normal Reflect VFX (On Paddle Hit)")]
    [Tooltip("白線の通常反射時に出すVFX（Particle System推奨）。未設定なら出ない。")]
    [SerializeField] private GameObject normalReflectWhiteVfxPrefab;

    [Tooltip("赤線の通常反射時に出すVFX（Particle System推奨）。未設定なら出ない。")]
    [SerializeField] private GameObject normalReflectRedVfxPrefab;

    [Tooltip("通常反射VFXを破棄する秒数（Particle側が Stop Action=Destroy なら 0 でもOK）。")]
    [SerializeField] private float normalReflectDestroySeconds = 3.5f;

    [Tooltip("通常反射VFXの位置を微小ランダムでズラす（ワールド座標）。0ならズラさない。")]
    [SerializeField] private float normalReflectRandomOffset = 0.02f;

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
    // Circle Formed VFX
    // =========================
    [Header("Circle Formed VFX")]
    [Tooltip("白線で円成立した時のVFX（CircleFormedVFXコンポーネント必須）。未設定なら出ない。")]
    [SerializeField] private GameObject circleFormedWhiteVfxPrefab;

    [Tooltip("赤線で円成立した時のVFX（CircleFormedVFXコンポーネント必須）。未設定なら出ない。")]
    [SerializeField] private GameObject circleFormedRedVfxPrefab;

    [Tooltip("円成立VFXを破棄する秒数。")]
    [SerializeField] private float circleFormedVfxDestroySeconds = 3f;

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

    // ③⑤ 操作反転フラグ
    private bool _lineTypeReversed = false;
    private bool _coordReversed    = false;

    public void SetLineTypeReversed(bool value) => _lineTypeReversed = value;
    public void SetCoordReversed(bool value)    => _coordReversed    = value;

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

    // 長押し判定
    private float pointerDownTime;
    private bool longPressArmed;   // 長押し判定中（指が動いていない）
    private bool longPressTriggered; // 今回のドラッグで長押し成立済み

    private bool isPreparingRed;
    private bool isDrawingNormal;
    private bool isDrawingRed;
    private bool blockedThisDrag;
    // UIボタン上でPointerDownが発生した場合、そのドラッグをブロックするフラグ
    private bool isBlockedByUI;

    // スローモーションボタンをホールド中、描画用として選んだ指のfingerId（-1=未選択）。
    // 毎フレーム座標で選び直すと、描画中の指がボタン表示範囲を一瞬でも横切った時に
    // 全ての指が「ボタン上」判定になり入力が丸ごと欠落、指がボタン範囲を抜けた瞬間に
    // 溜まった移動距離を一直線で結んでしまう（ワープしたような誤爆直線）不具合の原因になる。
    // 一度選んだ指は、実際に離れるまで座標に関わらず同じ指を使い続けることで回避する。
    private int drawingFingerId = -1;
    // 線を引く指として選んだ時の、その指の位置（乗り換えの判定用：ここからほとんど動いていない指は手のひら等とみなす）
    private Vector2 drawingFingerStartPos;

    // HUD内で触れ始めたため、離すまで線に使わない指のfingerId（hudRect）
    private readonly HashSet<int> hudIgnoredFingers = new HashSet<int>();

    private Stroke currentNormalStroke;
    private Stroke currentRedStroke;

    private Vector3 lastNormalPos;
    private Vector3 lastRedPos;
    private Vector3 _strokeStartRaw;

    private float lastDotTickTime;

    private bool isNgTickActive;
    private float nextNgTickTime;
    private float ngTickEndTime;

    private float lastPaddleHitTime;

    // Strokeごとの基準色
    private Color currentNormalBaseColor = Color.white;
    private Color currentRedBaseColor = Color.red;

    // HUDの範囲が未設定なら、05_Game > Canvas > SkillHUD を自動で入れる（エディタではInspectorにも反映される）
    private void AutoAssignHudRect()
    {
        if (hudRect != null) return;
        // 同じシーンの中から「Canvas の直下の SkillHUD」を探す（Canvas という名前のオブジェクトが複数あっても取り違えない）
        foreach (var root in gameObject.scene.GetRootGameObjects())
        {
            foreach (var rt in root.GetComponentsInChildren<RectTransform>(true))
            {
                if (rt.name == "SkillHUD" && rt.parent != null && rt.parent.name == "Canvas") { hudRect = rt; return; }
            }
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (Application.isPlaying || hudRect != null) return;
        // OnValidate中はシーンの検索が不安定なため、次のエディタ更新で設定する
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this == null || hudRect != null) return;
            AutoAssignHudRect();
            if (hudRect != null)
            {
                UnityEditor.EditorUtility.SetDirty(this);
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
                Debug.Log("[PaddleDrawer] Hud Rect に SkillHUD を自動で設定しました。シーンを保存してください。", this);
            }
        };
    }
#endif

    private void Awake()
    {
        Instance = this;
        AutoAssignHudRect();

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

    // 無効化された時に、出しそびれた残像を次に有効になった時に古い位置で出さないよう捨てる（従来のコルーチンも停止で消えていた）
    private void OnDisable()
    {
        pendingRedAfterimages.Clear();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        FlushPendingRedAfterimages();

        bool inputBlockedThisFrame = Game.UI.SkillSelectionUI.IsShowing
            || (PauseManager.Instance != null && PauseManager.Instance.IsPaused)
            || (GameManager.Instance != null && GameManager.Instance.IsGameOver);

        if (inputBlockedThisFrame)
        {
            // ★スキル選択ポップアップ表示中・ポーズ中はReadPointer()自体が呼ばれず
            //   「指が画面から離れたか」を一切検知できない。実際にはポップアップを閉じる・
            //   中断メニューを操作するために描画中の指を離すのがほとんどなので、線を引いている
            //   最中にこれらが割り込むと、Finish()が一生呼ばれないストロークが残り続け、
            //   StrokeManagerの本数だけが永久に埋まったままになる（見た目に線が無いのに
            //   引けなくなる不具合の原因だった）。割り込みが始まった瞬間に、描画中の
            //   ストロークを確実に終了させておく。
            if (isDrawingRed) EndRed();
            else if (isDrawingNormal) EndNormal();
            return;
        }

        costManager?.SetDrawingState(isDrawingNormal, isDrawingRed);
        UpdateNgTick();

        if (!ReadPointer(out PointerState state, out Vector2 pos))
        {
            return;
        }

        pointerPos = pos;

        if (waitingSecondTap && Time.time - firstTapUpTime > doubleTapMaxInterval)
            waitingSecondTap = false;

        if (state == PointerState.Down)
        {
            pointerDownPos = pos;
            pointerDownTime = Time.time;
            blockedThisDrag = false;
            longPressArmed = true;
            longPressTriggered = false;

            StopNgTick();

            // ダブルタップ判定（既存コード・無効化せず残す）
            bool canArmRed =
                waitingSecondTap &&
                Time.time - firstTapUpTime <= doubleTapMaxInterval &&
                Vector2.Distance(pos, firstTapUpPos) <= doubleTapMaxDistancePixels;

            // ダブルタップ成立時も長押しと同じ発動演出（VFX/SE）を出す
            if (canArmRed) OnLongPressActivated(pos);

            // ③糸接続中は白赤を反転（通常=白デフォ、反転=赤デフォ。ダブルタップも逆転）
            isPreparingRed = _lineTypeReversed ? !canArmRed : canArmRed;
            waitingSecondTap = false;
        }
        else if (state == PointerState.Held)
        {
            float moved = Vector2.Distance(pos, pointerDownPos);

            // 指が動いたら長押し判定をキャンセル
            if (longPressArmed && moved >= dragStartMovePixels)
                longPressArmed = false;

            // 長押し成立チェック（③反転中は赤デフォなので長押しで白に切り替え）
            bool longPressReady = _lineTypeReversed ? isPreparingRed : !isPreparingRed;
            if (longPressArmed && !longPressTriggered && longPressReady &&
                Time.time - pointerDownTime >= longPressSeconds)
            {
                longPressTriggered = true;
                longPressArmed = false;
                isPreparingRed = !_lineTypeReversed;
                OnLongPressActivated(pos);
            }

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
            longPressArmed = false;
            longPressTriggered = false;

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
        PlayPaddleHitSE(type, isJust, false);
    }

    /// <param name="ignoreFrameLimit">
    /// trueなら「同一フレームの重複は1回」「最短間隔」の制限をかけず、呼ばれた回数分を重ねて鳴らす。
    /// ★Beam専用（PaddleDot.EvaluateExternalHitからのみtrue）。Beamは何回反射しても1フレームで軌道を組み立てるため、
    ///   同時発射された大量の弾向けの制限（9/23のフリーズ対策）にかかると反射回数分鳴らなくなる。通常の弾は従来どおりfalse。
    /// </param>
    public void PlayPaddleHitSE(PaddleDot.LineType type, bool isJust, bool ignoreFrameLimit)
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
        if (!ignoreFrameLimit)
        {
            if (paddleHitMinInterval > 0f && now - lastPaddleHitTime < paddleHitMinInterval) return;

            // ★同一フレーム内で多数の弾を同時に反射すると、この後のPlayOneShotが何度も重なって
            //   音が割れたり、聴感上ピッチが上がったように聞こえたりするため、「ほぼ同時（同一フレーム）」の
            //   重複だけを1回に制限する（1フレームでもズレていれば必ず鳴るので、ずらして当てた場合は各々鳴る）
            if (!SeSimultaneousGuard.TryAllow("PaddleHit")) return;
        }

        lastPaddleHitTime = now;

        // SoundSettingsManagerのSE音量を適用
        float finalVolume = paddleHitVolume * (SoundSettingsManager.Instance != null ? SoundSettingsManager.Instance.SEVolume : 1f);

        // ★SEファイル先頭の無音区間ぶん聞こえが遅れるため、再生開始位置をずらして早く聞こえるようにする
        float offset = Mathf.Clamp(paddleHitSeStartOffsetSeconds, 0f, Mathf.Max(0f, clip.length - 0.01f));
        if (ignoreFrameLimit)
        {
            // Beam用：1つのAudioSourceのPlay()だと同一フレームの複数回が上書きされて1回分しか鳴らないため、
            //   使い回しの単発SE再生で呼ばれた回数分を重ねて鳴らす（開始位置ずらしも同じ値を適用）
            AudioOneShotPool.PlayWithOffset(clip, finalVolume, offset, Vector3.zero, null, 0.1f);
        }
        else if (offset > 0f)
        {
            src.clip = clip;
            src.volume = finalVolume;
            src.time = offset;
            src.Play();
        }
        else
        {
            src.PlayOneShot(clip, finalVolume);
        }
    }

    // PaddleDot から呼ぶ「通常反射VFX」
    public void SpawnNormalReflectVfx(PaddleDot.LineType type, Vector3 worldPos, Vector2 reflectDir)
    {
        SpawnNormalReflectVfx(type, worldPos, reflectDir, false);
    }

    /// <param name="ignoreFrameLimit">trueなら「同一フレームの同種VFXは1回」の制限をかけない（Beam専用。PlayPaddleHitSEと同じ理由）</param>
    public void SpawnNormalReflectVfx(PaddleDot.LineType type, Vector3 worldPos, Vector2 reflectDir, bool ignoreFrameLimit)
    {
        GameObject prefab = null;

        if (type == PaddleDot.LineType.Normal) prefab = normalReflectWhiteVfxPrefab;
        else if (type == PaddleDot.LineType.RedAccel) prefab = normalReflectRedVfxPrefab;

        if (prefab == null) return;

        // ★至近距離で大量の弾が同時多発的に反射すると、同一フレーム内でこのInstantiateが
        //   何十〜何百回も走りGC負荷でフリーズする問題への対策（PlayPaddleHitSEと同じ既存ガードを流用）。
        //   同一フレーム内の同種VFXは1回に制限する（元に戻す場合はこのif文を削除するだけでよい）。
        if (!ignoreFrameLimit && !SeSimultaneousGuard.TryAllow(GuardKey(s_normalReflectVfxKeys, "NormalReflectVfx_", type))) return;

        Vector3 p = worldPos;
        p.z = zDepth;

        float ofs = Mathf.Max(0f, normalReflectRandomOffset);
        if (ofs > 0f)
        {
            p.x += Random.Range(-ofs, ofs);
            p.y += Random.Range(-ofs, ofs);
        }

        GameObject vfx = HitVfxPool.Rent(prefab, drawVfxParent, p);
        if (vfx == null) return;

        vfx.transform.SetPositionAndRotation(p, Quaternion.identity);
        vfx.transform.SetParent(drawVfxParent, false);
        vfx.SetActive(true);

        HitVfxPool.ReturnLater(prefab, vfx, normalReflectDestroySeconds);
    }

    // PaddleDot から呼ぶ「Just星型VFX」
    public void SpawnJustStarVfx(PaddleDot.LineType type, Vector3 worldPos)
    {
        SpawnJustStarVfx(type, worldPos, false);
    }

    /// <param name="ignoreFrameLimit">trueなら「同一フレームの同種VFXは1回」の制限をかけない（Beam専用。PlayPaddleHitSEと同じ理由）</param>
    public void SpawnJustStarVfx(PaddleDot.LineType type, Vector3 worldPos, bool ignoreFrameLimit)
    {
        GameObject prefab = null;

        if (type == PaddleDot.LineType.Normal) prefab = justStarWhiteVfxPrefab;
        else if (type == PaddleDot.LineType.RedAccel) prefab = justStarRedVfxPrefab;

        if (prefab == null) return;

        // ★同一フレーム内の同種VFX大量発生対策（SpawnNormalReflectVfxと同じ仕組み。元に戻す場合はこのif文を削除するだけ）
        if (!ignoreFrameLimit && !SeSimultaneousGuard.TryAllow(GuardKey(s_justStarVfxKeys, "JustStarVfx_", type))) return;

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

    public void SpawnCircleVfx(PaddleDot.LineType type, List<Vector3> dotPositions, Vector3 center)
    {
        GameObject prefab = (type == PaddleDot.LineType.Normal) ? circleFormedWhiteVfxPrefab : circleFormedRedVfxPrefab;
        if (prefab == null) return;

        Transform parent = drawVfxParent;
        GameObject vfx = HitVfxPool.Rent(prefab, parent, center);
        if (vfx == null) return;

        vfx.transform.SetPositionAndRotation(center, Quaternion.identity);
        vfx.transform.SetParent(parent, false);
        vfx.SetActive(true);

        CircleFormedVFX vfxScript = vfx.GetComponent<CircleFormedVFX>();
        if (vfxScript != null)
        {
            Color strokeColor = (type == PaddleDot.LineType.Normal) ? currentNormalBaseColor : currentRedBaseColor;
            vfxScript.Play(dotPositions, center, strokeColor);
        }

        HitVfxPool.ReturnLater(prefab, vfx, circleFormedVfxDestroySeconds);
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

        // ★見た目として即壊す。以前はStroke自体をDestroyしていたが、それだと子である
        //   全DotがDestroyに巻き込まれプールへ戻らないため、先に個別にプールへ返却する。
        //   Finish(0f)の時点でaliveDotCount==0ならStroke自身は既にCompleteStroke()経由で
        //   破棄済みのため、その場合ReleaseAllDots()は何もしない（安全）。
        stroke.ReleaseAllDots();
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

        // まずVFX（既存メソッド）。新しい線の破壊演出（ReflectedBulletFXManager）が有効な時は出さない
        if (!ReflectedBulletFXManager.HandlesLineBreak) SpawnLineBreakVfx(type, worldPos);

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
        float normalExtraLifeBonus = Game.Skills.SkillManager.Instance != null ? Game.Skills.SkillManager.Instance.GetCircleExtraLifeExtension() : 0f;
        currentNormalStroke?.ConfigureCircleRule(circleCloseDistance, circleExtraLifeSeconds + normalExtraLifeBonus, circleMinDots, circleMinPerimeter, circleMinBoundsSize, circleMaxAspect);

        StopDrawLoop();

        isDrawingNormal = true;

        _strokeStartRaw = GetWorldRaw(pointerPos);
        Vector3 p = _strokeStartRaw;
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

        lastNormalPos = prev;
    }

    private void OnLongPressActivated(Vector2 screenPos)
    {
        // SE（paddleHitSourceを優先。ドットティックSEと干渉しないため）
        AudioSource longPressSrc = paddleHitSource != null ? paddleHitSource : sfxOneShotSource;
        if (longPressActivateClip != null && longPressSrc != null)
        {
            float vol = longPressActivateVolume * (SoundSettingsManager.Instance != null ? SoundSettingsManager.Instance.SEVolume : 1f);
            longPressSrc.PlayOneShot(longPressActivateClip, vol);
        }

        // パーティクル（リング拡散）
        if (longPressActivateVfxPrefab != null)
        {
            Vector3 worldPos = GetWorld(screenPos);
            GameObject vfx = Instantiate(longPressActivateVfxPrefab, worldPos, Quaternion.identity, drawVfxParent);

            // パレットをVFXスクリプトに渡す
            var vfxScript = vfx.GetComponent<LongPressActivateVFX>();
            if (vfxScript != null && redStrokeBaseColors != null && redStrokeBaseColors.Length > 0)
                vfxScript.SetPalette(redStrokeBaseColors);

            if (longPressActivateVfxDestroySeconds > 0f)
                Destroy(vfx, longPressActivateVfxDestroySeconds);
        }
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
        float redExtraLifeBonus = Game.Skills.SkillManager.Instance != null ? Game.Skills.SkillManager.Instance.GetCircleExtraLifeExtension() : 0f;
        currentRedStroke?.ConfigureCircleRule(circleCloseDistance, circleExtraLifeSeconds + redExtraLifeBonus, circleMinDots, circleMinPerimeter, circleMinBoundsSize, circleMaxAspect);

        StopDrawLoop();

        isDrawingRed = true;

        _strokeStartRaw = GetWorldRaw(pointerPos);
        Vector3 p = _strokeStartRaw;
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

        lastRedPos = prev;
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
        // 元の AudioSource（sfxOneShotSource）で鳴らす。先頭を詰めた音（dotTickStartOffsetSeconds 分を切り落とした複製）を使う
        src.PlayOneShot(GetTrimmedDotTickClip(clip), finalVolume);
    }

    // 先頭を dotTickStartOffsetSeconds 秒だけ切り落とした音の複製（元の音ごとに1回だけ作る）
    private readonly Dictionary<AudioClip, AudioClip> trimmedDotTickClips = new Dictionary<AudioClip, AudioClip>();
    private float trimmedDotTickOffset = -1f;

    private AudioClip GetTrimmedDotTickClip(AudioClip clip)
    {
        if (dotTickStartOffsetSeconds <= 0f) return clip;
        if (!Mathf.Approximately(trimmedDotTickOffset, dotTickStartOffsetSeconds)) { trimmedDotTickClips.Clear(); trimmedDotTickOffset = dotTickStartOffsetSeconds; }
        if (trimmedDotTickClips.TryGetValue(clip, out var cached) && cached != null) return cached;

        // 音のデータがまだ読み込まれていない時は、今回は元の音のまま鳴らす（次回以降に複製を作る）
        if (clip.loadState != AudioDataLoadState.Loaded) { clip.LoadAudioData(); return clip; }

        int start = Mathf.RoundToInt(dotTickStartOffsetSeconds * clip.frequency);
        int remain = clip.samples - start;
        if (start <= 0 || remain <= 0) return clip;

        var data = new float[remain * clip.channels];
        if (!clip.GetData(data, start)) return clip; // 読み出せない設定の音なら元の音のまま
        var trimmed = AudioClip.Create(clip.name + "_trim", remain, clip.channels, clip.frequency, false);
        trimmed.SetData(data, 0);
        trimmedDotTickClips[clip] = trimmed;
        return trimmed;
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
        PaddleDot dot = PaddleDotPool.Get(paddleDotPrefab, p, Quaternion.identity, parent);

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
        // ★赤線差別化：赤線のみJust猶予時間にボーナスを加算（白線は変更なし）
        if (type == PaddleDot.LineType.RedAccel)
        {
            effectiveJustWindow += redJustWindowBonus;
        }

        // ★赤線差別化：Justダメージ倍率も白線/赤線で個別の値を使う
        float effectiveJustDamageMultiplier = (type == PaddleDot.LineType.Normal) ? justDamageMultiplier : redJustDamageMultiplier;

        dot.Configure(type, accelMul, accelMaxCount, effectiveJustWindow, effectiveJustDamageMultiplier, baseColor, jitter, h);

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
                    // ★負荷軽減：点ごとにコルーチンを作らず、次のフレームのUpdate冒頭でまとめて出す（従来と同じ「1フレーム遅れ」）
                    pendingRedAfterimages.Add(new PendingAfterimage { pos = worldPos, frame = Time.frameCount });
                }
                else
                {
                    SpawnVfx(redAfterimageVfxPrefab, worldPos, drawVfxDestroySeconds);
                }
            }

            return;
        }
    }

    // ★負荷軽減：反射のたびに「接頭辞＋線の種類」の文字列を作らず、種類ごとに1回だけ作って使い回す（キーの中身は従来と同じ）
    private static readonly Dictionary<int, string> s_normalReflectVfxKeys = new Dictionary<int, string>();
    private static readonly Dictionary<int, string> s_justStarVfxKeys = new Dictionary<int, string>();

    private static string GuardKey(Dictionary<int, string> cache, string prefix, PaddleDot.LineType type)
    {
        int k = (int)type;
        if (!cache.TryGetValue(k, out string key))
        {
            key = prefix + type;
            cache[k] = key;
        }
        return key;
    }

    private struct PendingAfterimage
    {
        public Vector3 pos;
        public int frame;
    }

    private readonly List<PendingAfterimage> pendingRedAfterimages = new List<PendingAfterimage>(64);

    /// <summary>前のフレーム以前に積まれた赤線の残像VFXを出す（このフレームに積まれた分は次のフレームへ持ち越す）</summary>
    private void FlushPendingRedAfterimages()
    {
        int count = pendingRedAfterimages.Count;
        if (count == 0) return;
        int now = Time.frameCount;
        int write = 0;
        for (int i = 0; i < count; i++)
        {
            PendingAfterimage a = pendingRedAfterimages[i];
            if (a.frame < now) SpawnRedAfterimage(a.pos);
            else pendingRedAfterimages[write++] = a;
        }
        pendingRedAfterimages.RemoveRange(write, count - write);
    }

    private void SpawnRedAfterimage(Vector3 worldPos)
    {
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

        GameObject vfx = HitVfxPool.Rent(prefab, parent, worldPos);
        if (vfx == null) return;

        vfx.transform.SetPositionAndRotation(worldPos, Quaternion.identity);
        vfx.transform.SetParent(parent, false);
        vfx.SetActive(true);

        HitVfxPool.ReturnLater(prefab, vfx, destroySeconds);
    }

    private Vector3 GetWorldRaw(Vector2 screenPos)
    {
        if (cam == null) cam = Camera.main;
        Vector3 s = screenPos;
        s.z = -cam.transform.position.z;
        Vector3 w = cam.ScreenToWorldPoint(s);
        w.z = zDepth;
        return w;
    }

    private Vector3 GetWorld(Vector2 screenPos)
    {
        Vector3 w = GetWorldRaw(screenPos);
        if (_coordReversed)
        {
            // 始点(_strokeStartRaw)を基準に移動方向を反転（始点の位置は変えない）
            w.x = 2f * _strokeStartRaw.x - w.x;
            w.y = 2f * _strokeStartRaw.y - w.y;
        }
        return w;
    }

    private bool ReadPointer(out PointerState state, out Vector2 pos)
    {
        state = PointerState.None;
        pos = Vector2.zero;

        UpdateHudIgnoredFingers();

        // ★重要：「描画用として追跡中の指(drawingFingerId)」が既にある場合、touchCountが0で
        //   あっても最優先でここをチェックする。従来はtouchCount>0の時だけ追跡ロジックが動き、
        //   端末がTouchPhase.Ended/Canceledを一度も報告しないままtouchCountがいきなり0に
        //   落ちるケース（一部Android機種の既知の挙動）で、指が消えたことを一切検知できなかった。
        //   その場合EndNormal()/EndRed()が呼ばれずStroke.Finish()も呼ばれないため、
        //   StrokeManager.ActiveStrokesCountがそのストローク分だけ永久に減らなくなり
        //   （見た目に線が無いのに本数上限に張り付いて新しい線が引けなくなる不具合の原因）。
        if (drawingFingerId >= 0)
        {
            Touch? trackedTouch = null;
            for (int i = 0; i < Input.touchCount; i++)
            {
                if (Input.GetTouch(i).fingerId == drawingFingerId)
                {
                    trackedTouch = Input.GetTouch(i);
                    break;
                }
            }

            if (trackedTouch == null)
            {
                // 追跡中の指が、Ended/Canceledの報告なしに消えた。取りこぼし防止のため
                // ここで明示的にUp扱いにし、ストロークを確実に終了させる。
                // 新しい座標は取得できないため直前の座標をそのまま使う。
                pos = pointerPos;
                state = PointerState.Up;
                drawingFingerId = -1;
                return true;
            }

            Touch tTracked = trackedTouch.Value;
            pos = tTracked.position;

            // ★乗り換え（2026/10/25）：選んでいる指が、選んでから線を引き始める距離（dragStartMovePixels）も動いておらず
            //   線も描いていない（＝画面の端に触れたままの手のひら等）時に、新しく触れた指があれば、そちらを線を引く指にする。
            //   以前は最初に選んだ指を離すまで使い続けたため、触れたままの手のひらが選ばれると、なぞった指が全部無視されていた。
            //   線を描いている途中・動いている指は乗り換えないので、線がワープする不具合（drawingFingerIdの説明参照）は起きない
            if (!isDrawingNormal && !isDrawingRed && tTracked.phase != TouchPhase.Ended && tTracked.phase != TouchPhase.Canceled
                && Vector2.Distance(tTracked.position, drawingFingerStartPos) < dragStartMovePixels
                && TryFindNewDrawingFinger(drawingFingerId, out Touch newer))
            {
                drawingFingerId = newer.fingerId;
                drawingFingerStartPos = newer.position;
                pos = newer.position;
                state = PointerState.Down;
                return true;
            }

            if (tTracked.phase == TouchPhase.Began) { state = PointerState.Down; return true; }
            if (tTracked.phase == TouchPhase.Moved || tTracked.phase == TouchPhase.Stationary) { state = PointerState.Held; return true; }
            if (tTracked.phase == TouchPhase.Ended || tTracked.phase == TouchPhase.Canceled)
            {
                state = PointerState.Up;
                drawingFingerId = -1;
                return true;
            }
            return false;
        }

        if (Input.touchCount > 0)
        {
            // ホールドモードでスローモーションボタンをホールド中の場合、
            // ボタンを押している指以外を描画入力に使用する。
            // ★以前はeventData.pointerId(新Input SystemのUIモジュールが発行するID)と
            //   Input.GetTouch().fingerId(レガシーInput Managerのタッチ指ID)を比較していたが、
            //   この2つは別々のID体系で一致する保証が無く「スローモーション中に線が描けない」
            //   不具合の原因になっていたため、一度は座標ベース（毎フレーム「ボタン範囲外の指」を
            //   座標だけで選び直す）判定に変更した。しかしそれも、描画中の指の軌道がボタン表示範囲を
            //   一瞬でも横切ると全ての指が「ボタン上」判定になり入力が丸ごと欠落し、範囲を抜けた瞬間に
            //   その間の移動分を一直線で結んでしまう（ワープしたような誤爆直線）別の不具合を生んでいた。
            //   現在は「一度選んだ指はfingerId（Input.touches内で完結する単一ID体系）で固定し、
            //   実際に離れるまで座標に関わらず同じ指を使い続ける」方式にしている。
            bool slowHolding = SlowMotionUIManager.Instance != null
                               && SlowMotionUIManager.Instance.UseHoldMode
                               && SlowMotionUIManager.Instance.IsHoldingButton;

            // 新たに描画用の指を選ぶ（スローモーションホールド中はボタン上の指・HUD内で触れ始めた指を除外する）。
            // ★今触れたばかりの指を優先する（2026/10/25）。触れたままの手のひら等より、なぞり始めた指を選ぶため
            Touch? found = null;
            for (int pass = 0; pass < 2 && found == null; pass++)
            {
                for (int i = 0; i < Input.touchCount; i++)
                {
                    Touch candidate = Input.GetTouch(i);
                    if (pass == 0 && candidate.phase != TouchPhase.Began) continue;
                    if (slowHolding && SlowMotionUIManager.Instance.IsScreenPointOnButton(candidate.position))
                        continue;
                    if (hudIgnoredFingers.Contains(candidate.fingerId))
                        continue;

                    found = candidate;
                    break;
                }
            }

            if (found == null) return false; // 使える指がなければ描画しない
            Touch t = found.Value;
            drawingFingerId = t.fingerId;
            drawingFingerStartPos = t.position;

            pos = t.position;

            // ★前から触れていた指を途中で選んだ時も「触れ始め」として扱う（2026/10/25）。
            //   以前はそのまま「触れている途中」として扱い、前の指の触れ始めの位置と比べてしまうため、
            //   選ばれた手のひらの位置から勝手に線が始まることがあった
            if (t.phase == TouchPhase.Began || t.phase == TouchPhase.Moved || t.phase == TouchPhase.Stationary) { state = PointerState.Down; return true; }
            if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled)
            {
                state = PointerState.Up;
                drawingFingerId = -1;
                return true;
            }
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
            if (holdingUI || IsScreenPointOnHud(pos))
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

    /// <summary>
    /// 乗り換え先になる「今触れたばかりの指」を探す（今の指・HUD内で触れ始めた指・スローモーションのボタンを押している指は除く）
    /// </summary>
    private bool TryFindNewDrawingFinger(int currentFingerId, out Touch result)
    {
        result = default;
        bool slowHolding = SlowMotionUIManager.Instance != null
                           && SlowMotionUIManager.Instance.UseHoldMode
                           && SlowMotionUIManager.Instance.IsHoldingButton;
        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch c = Input.GetTouch(i);
            if (c.fingerId == currentFingerId || c.phase != TouchPhase.Began) continue;
            if (hudIgnoredFingers.Contains(c.fingerId)) continue;
            if (slowHolding && SlowMotionUIManager.Instance.IsScreenPointOnButton(c.position)) continue;
            result = c;
            return true;
        }
        return false;
    }

    /// <summary>
    /// HUD内で触れ始めた指を、離すまで「線に使わない指」として覚える。
    /// 触れ始めの位置で決めるので、HUDから外へ指（手のひら）がずれてきても線を引く指には選ばれない
    /// </summary>
    private void UpdateHudIgnoredFingers()
    {
        int count = Input.touchCount;
        if (count == 0) { hudIgnoredFingers.Clear(); return; }

        for (int i = 0; i < count; i++)
        {
            Touch t = Input.GetTouch(i);
            if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled)
            {
                hudIgnoredFingers.Remove(t.fingerId);
            }
            else if (t.phase == TouchPhase.Began)
            {
                if (IsScreenPointOnHud(t.position))
                {
                    hudIgnoredFingers.Add(t.fingerId);
                }
                else hudIgnoredFingers.Remove(t.fingerId);
            }
        }
    }

    private bool IsScreenPointOnHud(Vector2 screenPos)
    {
        if (hudRect == null || !hudRect.gameObject.activeInHierarchy) return false;
        Canvas canvas = hudRect.GetComponentInParent<Canvas>();
        Camera uiCam = (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay) ? canvas.worldCamera : null;
        return RectTransformUtility.RectangleContainsScreenPoint(hudRect, screenPos, uiCam);
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
    public float RedJustDamageMultiplier => redJustDamageMultiplier;
    public Color[] NormalStrokeBaseColors => normalStrokeBaseColors;
    public Color[] RedStrokeBaseColors => redStrokeBaseColors;
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
    /// 赤線のJust反射ダメージ倍率を設定（スキルシステム用）
    /// </summary>
    public void SetRedJustDamageMultiplier(float value)
    {
        redJustDamageMultiplier = Mathf.Max(1f, value);
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
