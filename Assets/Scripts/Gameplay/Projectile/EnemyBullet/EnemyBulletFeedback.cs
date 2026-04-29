using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class EnemyBulletFeedback : MonoBehaviour
{
    // EnemyBullet側の vfxParent を受け取ってフォールバックに使う
    [Header("Shared Parent (Fallback)")]
    [Tooltip("各Parentが未設定のときに使うVFX親（EnemyBullet側の vfxParent を注入して使う想定）。未設定ならワールド直下。")]
    [SerializeField] private Transform defaultVfxParent;

    // =========================
    // Paddle Hit VFX
    // =========================
    [Header("Paddle Hit VFX (White/Red Common)")]
    [Tooltip("白線/赤線で反射した瞬間に生成するVFX Prefab（共通）。未設定なら出ない。")]
    [SerializeField] private GameObject paddleHitVfxPrefab;

    [Tooltip("PaddleヒットVFXをこの親の下に生成する。未設定なら defaultVfxParent を使い、両方未設定ならワールド直下。")]
    [SerializeField] private Transform paddleHitVfxParent;

    [Tooltip("PaddleヒットVFXを破棄する秒数（Prefab側が Stop Action=Destroy なら短くてもOK）")]
    [SerializeField] private float paddleHitVfxDestroySeconds = 0.25f;

    [Tooltip("PaddleヒットVFXの最短間隔（秒）。点群接触などによる過密を抑える。")]
    [SerializeField] private float paddleHitVfxMinIntervalSeconds = 0.06f;

    private float lastPaddleHitVfxTime = -999f;

    // =========================
    // Wall Hit SE + VFX
    // =========================
    [Header("Wall Hit SE")]
    [Tooltip("壁ヒットSE（その1）。未設定なら鳴らない。")]
    [SerializeField] private AudioClip wallHitClipA;

    [Tooltip("壁ヒットSE（その2）。未設定ならAのみ。")]
    [SerializeField] private AudioClip wallHitClipB;

    [Tooltip("壁ヒットSE音量（固定）")]
    [Range(0f, 1f)]
    [SerializeField] private float wallHitVolume = 1f;

    [Tooltip("壁ヒット（SE+VFX）の最短間隔（秒）。壁沿いに走った時の超連打を防止する。")]
    [SerializeField] private float wallHitMinIntervalSeconds = 0.08f;

    private float lastWallHitFeedbackTime = -999f;

    [Header("Wall Hit VFX")]
    [Tooltip("壁ヒット時に生成するVFX Prefab（Particle System 推奨）。未設定なら出ない。")]
    [SerializeField] private GameObject wallHitVfxPrefab;

    [Tooltip("壁ヒットVFXをこの親の下に生成する。未設定なら defaultVfxParent を使い、両方未設定ならワールド直下。")]
    [SerializeField] private Transform wallHitVfxParent;

    [Tooltip("壁ヒットVFXを破棄する秒数（Prefab側が Stop Action=Destroy なら短くてもOK）")]
    [SerializeField] private float wallHitVfxDestroySeconds = 0.35f;

    // =========================
    // Enemy Hit VFX (Normal)
    // =========================
    [Header("Enemy Hit VFX (Normal)")]
    [Tooltip("通常時（DamageMultiplier==1）の敵ヒットで出すVFX。未設定なら出ない。")]
    [SerializeField] private GameObject enemyHitVfxPrefab;

    [Tooltip("通常敵ヒットVFXをこの親の下に生成する。未設定なら defaultVfxParent を使い、両方未設定ならワールド直下。")]
    [SerializeField] private Transform enemyHitVfxParent;

    [Tooltip("通常敵ヒットVFXを破棄する秒数（Prefab側が Stop Action=Destroy なら短くてもOK）")]
    [SerializeField] private float enemyHitVfxDestroySeconds = 0.25f;

    [Tooltip("通常敵ヒットVFXの最短間隔（秒）。多重接触での過密発生を抑える。")]
    [SerializeField] private float enemyHitVfxMinIntervalSeconds = 0.06f;

    private float lastEnemyHitVfxTime = -999f;

    // =========================
    // Just Bullet VFX (Arrowhead)
    // =========================
    [Header("Just Bullet VFX (Arrowhead)")]
    [Tooltip("ジャスト反射成立時に弾にアタッチするVFX Prefab（JustBulletVFXコンポーネント必須）。未設定なら出ない。")]
    [SerializeField] private GameObject justBulletVfxPrefab;

    private JustBulletVFX activeJustVfx;

    // =========================
    // Just Powered VFX
    // =========================
    [Header("Just Powered VFX (Wall + Enemy While Powered)")]
    [Tooltip("Just成立後（DamageMultiplier>1）の間、壁/敵ヒット時に出す“強め”VFX。未設定なら出ない。")]
    [SerializeField] private GameObject justPoweredVfxPrefab;

    [Tooltip("Just強めVFXをこの親の下に生成する。未設定なら defaultVfxParent を使い、両方未設定ならワールド直下。")]
    [SerializeField] private Transform justPoweredVfxParent;

    [Tooltip("Just強めVFXを破棄する秒数（Prefab側が Stop Action=Destroy なら短くてもOK）")]
    [SerializeField] private float justPoweredVfxDestroySeconds = 0.35f;

    [Tooltip("Just強めVFXの最短間隔（秒）。Powered中の壁沿い/連続ヒットでも重くならないよう抑制。")]
    [SerializeField] private float justPoweredVfxMinIntervalSeconds = 0.08f;

    private float lastJustPoweredVfxTime = -999f;

    // =========================
    // Destroy SE
    // =========================
    [Header("Destroy SE")]
    [Tooltip("弾が壊れた時（VFX付き消滅時）に鳴らすSE（その1）")]
    [SerializeField] private AudioClip destroyClipA;

    [Tooltip("弾が壊れた時（VFX付き消滅時）に鳴らすSE（その2）")]
    [SerializeField] private AudioClip destroyClipB;

    [Tooltip("弾が壊れた時（VFX付き消滅時）に鳴らすSE（その3）")]
    [SerializeField] private AudioClip destroyClipC;

    [Tooltip("消滅SE音量（固定）")]
    [Range(0f, 1f)]
    [SerializeField] private float destroyVolume = 1f;

    [Tooltip("消滅SE用のAudioSourceを、この親の下に生成する（任意）。未設定ならワールド直下。")]
    [SerializeField] private Transform destroySeParent;

    [Tooltip("消滅SE用オブジェクトを破棄する追加猶予秒（クリップ長 + この秒数で破棄）")]
    [SerializeField] private float destroySeExtraDestroySeconds = 0.10f;

    private bool destroySePlayed = false;

    // =========================
    // Disappear VFX (Sprite Animation) + Pool
    // =========================
    [Header("Disappear VFX (Sprite Animation)")]
    [Tooltip("Paddle bounce limit に達して消える瞬間に生成するPrefab（SpriteRenderer + Animator）")]
    [SerializeField] private GameObject disappearVfxPrefab;

    [Tooltip("VFXをこの親の下に生成する（任意）。未設定なら defaultVfxParent を使う。")]
    [SerializeField] private Transform disappearVfxParent;

    [Tooltip("生成したVFXを破棄する秒数（アニメ長に合わせる。例：0.2〜0.35）")]
    [SerializeField] private float disappearVfxDestroySeconds = 0.25f;

    // =========================
    // Unreflected Disappear VFX/SE
    // =========================
    [Header("Unreflected Disappear VFX/SE")]
    [Tooltip("未反射弾がPlayer/Floorに触れて消える時のVFX Prefab")]
    [SerializeField] private GameObject unreflectedDisappearVfxPrefab;

    [Tooltip("未反射弾消滅VFXをこの親の下に生成する（任意）")]
    [SerializeField] private Transform unreflectedDisappearVfxParent;

    [Tooltip("未反射弾消滅VFXを破棄する秒数")]
    [SerializeField] private float unreflectedDisappearVfxDestroySeconds = 0.25f;

    [Tooltip("未反射弾消滅SE（その1）")]
    [SerializeField] private AudioClip unreflectedDisappearClipA;

    [Tooltip("未反射弾消滅SE（その2）")]
    [SerializeField] private AudioClip unreflectedDisappearClipB;

    [Range(0f, 1f)]
    [SerializeField] private float unreflectedDisappearVolume = 0.8f;

    [Tooltip("未反射弾消滅SE用の親（任意）")]
    [SerializeField] private Transform unreflectedDisappearSeParent;

    [Tooltip("未反射弾消滅SE用オブジェクトの破棄猶予（clip.length + これ）")]
    [SerializeField] private float unreflectedDisappearSeExtraDestroySeconds = 0.10f;

    // =========================
    // Countdown Beep SE
    // =========================
    [Header("Countdown Beep SE")]
    [Tooltip("カウントダウン中のBeep SE（点滅と同期）")]
    [SerializeField] private AudioClip countdownBeepClip;

    [Tooltip("Beep SEの基本音量")]
    [Range(0f, 1f)]
    [SerializeField] private float countdownBeepVolume = 0.6f;

    [Header("Countdown Beep Timing")]
    [Tooltip("Beep開始までの残り秒数（点滅開始と同期）")]
    [SerializeField] private float beepStartRemainingSeconds = 1.2f;

    [Tooltip("Beep最大間隔（秒）- 開始直後")]
    [SerializeField] private float beepIntervalMax = 0.5f;

    [Tooltip("Beep最短間隔（秒）- 爆発直前")]
    [SerializeField] private float beepIntervalMin = 0.08f;

    [Tooltip("Beep間隔の加速カーブ（0→1で間隔が縮む）")]
    [SerializeField] private AnimationCurve beepAccelCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Countdown Beep Limits")]
    [Tooltip("Beep同時再生の最大数（グローバル）")]
    [SerializeField] private int maxConcurrentBeeps = 6;

    [Tooltip("Beep再生のグローバルクールダウン（秒）")]
    [SerializeField] private float beepGlobalCooldown = 0.04f;

    // Beep ランタイム
    private bool beepActive = false;
    private float beepNextTime = -999f;
    private AudioSource beepAudioSource;

    // Beep グローバル制御（静的）- 再生終了時刻のリストで管理
    private static readonly System.Collections.Generic.List<float> s_beepEndTimes = new System.Collections.Generic.List<float>(16);
    private static float s_lastBeepTime = -999f;

    // =========================
    // Explosion VFX / SE
    // =========================
    [Header("Explosion VFX / SE")]
    [Tooltip("爆発VFX Prefab（未設定なら出ない）")]
    [SerializeField] private GameObject explosionVfxPrefab;

    [Tooltip("爆発VFXをこの親の下に生成（任意）。未設定なら defaultVfxParent を使う。")]
    [SerializeField] private Transform explosionVfxParent;

    [Tooltip("爆発VFXを破棄する秒数（Prefab側が Destroy なら短くてもOK）")]
    [SerializeField] private float explosionVfxDestroySeconds = 0.6f;

    [Tooltip("爆発SE（1本）。未設定なら鳴らない。")]
    [SerializeField] private AudioClip explosionSeClip;

    [Range(0f, 1f)]
    [SerializeField] private float explosionSeVolume = 1f;

    [Tooltip("爆発SEを鳴らす親（任意）。未設定ならワールド直下。")]
    [SerializeField] private Transform explosionSeParent;

    [Tooltip("爆発SE用オブジェクトの破棄猶予（clip.length + これ）")]
    [SerializeField] private float explosionSeExtraDestroySeconds = 0.10f;

    // =========================
    // Warp Disappear/Reappear VFX/SE
    // =========================
    [Header("Warp Disappear/Reappear VFX/SE (Optional)")]
    [Tooltip("ワープ消滅VFX/SEの最短間隔（秒）")]
    [SerializeField] private float warpVfxMinIntervalSeconds = 0.05f;

    [Tooltip("ワープ消滅VFXをこの親の下に生成（任意）")]
    [SerializeField] private Transform warpDisappearVfxParent;

    [Tooltip("ワープ消滅VFXを破棄する秒数")]
    [SerializeField] private float warpDisappearVfxDestroySeconds = 0.25f;

    [Tooltip("ワープ出現VFXをこの親の下に生成（任意）")]
    [SerializeField] private Transform warpReappearVfxParent;

    [Tooltip("ワープ出現VFXを破棄する秒数")]
    [SerializeField] private float warpReappearVfxDestroySeconds = 0.25f;

    [Tooltip("ワープSE音量")]
    [Range(0f, 1f)]
    [SerializeField] private float warpSeVolume = 0.8f;

    [Tooltip("ワープSE用の親（任意）")]
    [SerializeField] private Transform warpSeParent;

    [Tooltip("ワープSE用オブジェクトの破棄猶予（clip.length + これ）")]
    [SerializeField] private float warpSeExtraDestroySeconds = 0.10f;

    private float lastWarpVfxTime = -999f;

    // =========================
    // Reflect Trail (TrailRenderer)
    // =========================
    [Header("Reflect Trail")]
    [Tooltip("反射後に表示するTrailRenderer（EnemyBullet子のTrailオブジェクトにアタッチして割り当て）。未設定なら機能しない。")]
    [SerializeField] private TrailRenderer reflectTrail;

    [Header("Reflect Trail Settings")]
    [Tooltip("Play開始時にこのスクリプトの設定値をTrailRendererに自動適用する")]
    [SerializeField] private bool applyTrailSettingsOnStart = true;

    [Header("Reflect Trail Settings - Normal")]
    [Tooltip("通常反射：軌跡が残る時間（秒）")]
    [SerializeField] private float normalTrailTime = 0.30f;

    [Tooltip("通常反射：弾頭側の軌跡幅")]
    [SerializeField] private float normalTrailWidthStart = 0.12f;

    [Tooltip("通常反射：末尾の軌跡幅（0で先細り）")]
    [SerializeField] private float normalTrailWidthEnd = 0f;

    [Tooltip("通常反射：頂点生成の最小移動距離（小さいほど滑らか、重い）")]
    [SerializeField] private float normalTrailMinVertexDistance = 0.05f;

    [Header("Reflect Trail Settings - Just")]
    [Tooltip("Just反射：軌跡が残る時間（秒）")]
    [SerializeField] private float justTrailTime = 0.45f;

    [Tooltip("Just反射：弾頭側の軌跡幅")]
    [SerializeField] private float justTrailWidthStart = 0.20f;

    [Tooltip("Just反射：末尾の軌跡幅（0で先細り）")]
    [SerializeField] private float justTrailWidthEnd = 0f;

    [Tooltip("Just反射：頂点生成の最小移動距離（小さいほど滑らか、重い）")]
    [SerializeField] private float justTrailMinVertexDistance = 0.05f;

    [Header("Reflect Particles (Sparkle)")]
    [Tooltip("反射後に出す光の粒エフェクト（ReflectParticles子オブジェクトのParticleSystem）。未設定なら自動検索。")]
    [SerializeField] private ParticleSystem reflectParticles;

    [Tooltip("ON: Play開始時にSimulationSpace・StartSpeed・ColorOverLifetimeをコードで自動設定する。OFF: ParticleSystemコンポーネントの設定値をそのまま使う。")]
    [SerializeField] private bool applyParticleBaseSettingsOnStart = true;

    [Header("Reflect Particles - Normal")]
    [Tooltip("通常反射：パーティクル発生レート（個/秒）")]
    [SerializeField] private float normalParticleEmissionRate = 30f;

    [Tooltip("通常反射：パーティクルのサイズ")]
    [SerializeField] private float normalParticleSize = 0.07f;

    [Tooltip("通常反射：パーティクルの寿命（秒）")]
    [SerializeField] private float normalParticleLifetime = 0.25f;

    [Header("Reflect Particles - Just")]
    [Tooltip("Just反射：パーティクル発生レート（個/秒）")]
    [SerializeField] private float justParticleEmissionRate = 60f;

    [Tooltip("Just反射：パーティクルのサイズ")]
    [SerializeField] private float justParticleSize = 0.12f;

    [Tooltip("Just反射：パーティクルの寿命（秒）")]
    [SerializeField] private float justParticleLifetime = 0.30f;

    [Header("Reflect Particles - Trail Position")]
    [Tooltip("通常反射：弾の後ろ（Trail側）へのオフセット距離。")]
    [SerializeField] private float normalTrailSideOffset = 0.3f;

    [Tooltip("Just反射：弾の後ろ（Trail側）へのオフセット距離。")]
    [SerializeField] private float justTrailSideOffset = 0.3f;

    [Header("Reflect Particles - Cone Shape - Normal")]
    [Tooltip("通常反射：Coneのベース半径。横幅の広さを決める。小さいほど細い。元のPrefab値は1。")]
    [SerializeField] private float normalParticleConeRadius = 0.3f;

    [Tooltip("通常反射：Trail方向へのパーティクル速度。大きいほど長いストリークになる。0だと静止。")]
    [SerializeField] private float normalParticleTrailSpeed = 2f;

    [Header("Reflect Particles - Cone Shape - Just")]
    [Tooltip("Just反射：Coneのベース半径。横幅の広さを決める。小さいほど細い。元のPrefab値は1。")]
    [SerializeField] private float justParticleConeRadius = 0.4f;

    [Tooltip("Just反射：Trail方向へのパーティクル速度。大きいほど長いストリークになる。0だと静止。")]
    [SerializeField] private float justParticleTrailSpeed = 3f;

    // ランタイム
    private bool trailJustActive = false;
    private Rigidbody2D rb;

    // 未反射Trail（Start()より前に呼ばれるためパラメータを保存してStart()で適用）
    private bool pendingUnreflectedTrail = false;
    private Color pendingTrailColor;
    private float pendingTrailTime;
    private float pendingTrailWidthStart;
    private float pendingTrailWidthEnd;

    // =========================================================
    // Unity ライフサイクル
    // =========================================================
    private void Awake()
    {
        // 高負荷時にStart()が1フレーム以上遅延する場合、ReflectParticlesがplayOnAwake=trueで
        // デフォルト白色のまま発火してしまうバグを防ぐ。Awake()で即座にStop&Clearする。
        if (reflectParticles == null)
        {
            Transform t = transform.Find("ReflectParticles");
            if (t != null) reflectParticles = t.GetComponent<ParticleSystem>();
        }
        if (reflectParticles != null)
        {
            reflectParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    private void Start()
    {
        rb = GetComponent<Rigidbody2D>();

        if (reflectTrail == null)
            reflectTrail = GetComponentInChildren<TrailRenderer>(true);

        if (reflectParticles == null)
        {
            Transform t = transform.Find("ReflectParticles");
            if (t != null) reflectParticles = t.GetComponent<ParticleSystem>();
        }

        if (applyTrailSettingsOnStart && reflectTrail != null)
            reflectTrail.emitting = false;

        if (pendingUnreflectedTrail && reflectTrail != null)
            ApplyUnreflectedTrailImmediate();

        if (reflectParticles != null)
            InitReflectParticles();
    }

    private void LateUpdate()
    {
        if (reflectParticles == null || !reflectParticles.isPlaying) return;
        if (rb == null) return;

        Vector2 vel = rb.linearVelocity;
        if (vel.sqrMagnitude < 0.0001f) return;

        Vector2 dir = vel.normalized;
        // Trail側（進行方向の逆）に発生地点をオフセット
        float offset = trailJustActive ? justTrailSideOffset : normalTrailSideOffset;
        reflectParticles.transform.position = transform.position - (Vector3)(dir * offset);
        // ConeのY+軸をTrail方向（進行方向の逆）に向ける
        float angle = Mathf.Atan2(-dir.y, -dir.x) * Mathf.Rad2Deg;
        reflectParticles.transform.rotation = Quaternion.Euler(0f, 0f, angle - 90f);
    }

    // =========================================================
    // Trail セットアップ
    // =========================================================

    private void ApplyTrailRendererSettings(bool isJust = false)
    {
        if (reflectTrail == null) return;

        float time      = isJust ? justTrailTime             : normalTrailTime;
        float widthS    = isJust ? justTrailWidthStart        : normalTrailWidthStart;
        float widthE    = isJust ? justTrailWidthEnd          : normalTrailWidthEnd;
        float minVertex = isJust ? justTrailMinVertexDistance : normalTrailMinVertexDistance;

        reflectTrail.time = Mathf.Max(0.01f, time);
        reflectTrail.minVertexDistance = Mathf.Max(0.01f, minVertex);

        AnimationCurve widthCurve = new AnimationCurve(
            new Keyframe(0f, Mathf.Max(0f, widthS)),
            new Keyframe(1f, Mathf.Max(0f, widthE))
        );
        reflectTrail.widthCurve = widthCurve;
    }

    /// <summary>
    /// 2色からTrail用グラデーションを生成する。
    /// 構造: 色A(alpha=1) → 色B(alpha=1) at 60% → 色B(alpha=0) at 100%
    /// </summary>
    private static Gradient BuildReflectGradient(Color c1, Color c2)
    {
        var g = new Gradient();
        g.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(c1, 0f),
                new GradientColorKey(c2, 0.6f),
                new GradientColorKey(c2, 1f)
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(1f, 0.6f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        return g;
    }

    /// <summary>Play開始時にParticleSystemの基本設定を適用し、発生を停止状態にする。</summary>
    private void InitReflectParticles()
    {
        if (reflectParticles == null) return;

        if (applyParticleBaseSettingsOnStart)
        {
            var main = reflectParticles.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startSpeed = normalParticleTrailSpeed;

            // alpha フェードアウト（startColor に乗算される）
            var col = reflectParticles.colorOverLifetime;
            col.enabled = true;
            var fadeGradient = new Gradient();
            fadeGradient.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) }
            );
            col.color = new ParticleSystem.MinMaxGradient(fadeGradient);

            // ConeのRadiusを適用（ShapeTypeはPrefabのConeをそのまま維持）
            var shape = reflectParticles.shape;
            shape.radius = Mathf.Max(0f, normalParticleConeRadius);
        }

        var emission = reflectParticles.emission;
        emission.rateOverTime = 0f;

        reflectParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }

    // パーティクル色の順番選択インデックス（全弾グローバル共有）
    private static int s_normalParticleColorIndex = 0;
    private static int s_justParticleColorIndex   = 0;

    /// <summary>反射時にParticleSystemの色・サイズ・発生レートを適用して再生する。</summary>
    private void ApplyParticleSettings(bool isJust)
    {
        if (reflectParticles == null) return;

        Color[] palette = isJust
            ? (PaddleDrawer.Instance != null ? PaddleDrawer.Instance.RedStrokeBaseColors    : null)
            : (PaddleDrawer.Instance != null ? PaddleDrawer.Instance.NormalStrokeBaseColors : null);

        Color fallbackA = isJust ? Color.white : Color.white;
        Color fallbackB = isJust ? new Color(1f, 0.5f, 0f) : Color.cyan;

        ref int index = ref (isJust ? ref s_justParticleColorIndex : ref s_normalParticleColorIndex);
        PickTwoSequentialColors(palette, ref index, fallbackA, fallbackB, out Color c1, out Color c2);

        var main = reflectParticles.main;
        main.startLifetime = isJust ? justParticleLifetime : normalParticleLifetime;
        main.startSize     = isJust ? justParticleSize     : normalParticleSize;
        main.startColor    = new ParticleSystem.MinMaxGradient(c1, c2);
        main.startSpeed    = isJust ? justParticleTrailSpeed : normalParticleTrailSpeed;

        var shape = reflectParticles.shape;
        shape.radius = Mathf.Max(0f, isJust ? justParticleConeRadius : normalParticleConeRadius);

        var emission = reflectParticles.emission;
        emission.rateOverTime = isJust ? justParticleEmissionRate : normalParticleEmissionRate;

        if (!reflectParticles.isPlaying)
            reflectParticles.Play();
    }

    /// <summary>
    /// パレットから2色を順番に選択する（グローバル共有インデックスで毎回1つ進む）。
    /// 例: index=0 → [0,1]、index=1 → [1,2]、末尾でループ。
    /// </summary>
    private static void PickTwoSequentialColors(Color[] palette, ref int index,
                                                Color fallbackA, Color fallbackB,
                                                out Color c1, out Color c2)
    {
        if (palette == null || palette.Length == 0)
        {
            c1 = fallbackA;
            c2 = fallbackB;
            return;
        }

        if (palette.Length == 1)
        {
            c1 = c2 = palette[0];
            if (c1.a <= 0f) { c1.a = 1f; c2.a = 1f; }
            index = 0;
            return;
        }

        int i1 = index % palette.Length;
        int i2 = (index + 1) % palette.Length;
        index = (index + 1) % palette.Length; // 次回は1つ進む

        c1 = palette[i1];
        c2 = palette[i2];
        if (c1.a <= 0f) c1.a = 1f;
        if (c2.a <= 0f) c2.a = 1f;
    }

    private static void PickTwoDifferentColors(Color[] palette, Color fallbackA, Color fallbackB,
                                               out Color c1, out Color c2)
    {
        if (palette == null || palette.Length == 0)
        {
            c1 = fallbackA;
            c2 = fallbackB;
            return;
        }

        if (palette.Length == 1)
        {
            c1 = palette[0];
            c2 = palette[0];
            if (c1.a <= 0f) c1.a = 1f;
            if (c2.a <= 0f) c2.a = 1f;
            return;
        }

        int i1 = Random.Range(0, palette.Length);
        int i2;
        do { i2 = Random.Range(0, palette.Length); } while (i2 == i1);

        c1 = palette[i1];
        c2 = palette[i2];
        if (c1.a <= 0f) c1.a = 1f;
        if (c2.a <= 0f) c2.a = 1f;
    }

    // =========================================================
    // API（EnemyBullet から呼ぶ）
    // =========================================================
    public void SetDefaultVfxParent(Transform parent)
    {
        defaultVfxParent = parent;
    }

    public void SetUnreflectedTrail(Color color, float time, float widthStart, float widthEnd)
    {
        pendingUnreflectedTrail = true;
        pendingTrailColor = color;
        pendingTrailTime = time;
        pendingTrailWidthStart = widthStart;
        pendingTrailWidthEnd = widthEnd;
    }

    private void ApplyUnreflectedTrailImmediate()
    {
        reflectTrail.time = Mathf.Max(0.01f, pendingTrailTime);
        reflectTrail.minVertexDistance = 0.05f;
        AnimationCurve widthCurve = new AnimationCurve(
            new Keyframe(0f, Mathf.Max(0f, pendingTrailWidthStart)),
            new Keyframe(1f, Mathf.Max(0f, pendingTrailWidthEnd))
        );
        reflectTrail.widthCurve = widthCurve;
        Gradient g = new Gradient();
        g.SetKeys(
            new GradientColorKey[] { new GradientColorKey(pendingTrailColor, 0f), new GradientColorKey(pendingTrailColor, 1f) },
            new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) }
        );
        reflectTrail.colorGradient = g;
        reflectTrail.emitting = true;
    }

    public void OnPaddleReflect(Vector3 position)
    {
        TrySpawnPaddleHitVfx(position);
        EnableReflectTrail();
    }

    /// <summary>Just反射成立時に呼ぶ。Trail・ParticlesをJust設定・Just色に切り替える。</summary>
    public void OnJustReflect(PaddleDot.LineType lineType)
    {
        trailJustActive = true;

        Color[] palette = PaddleDrawer.Instance != null ? PaddleDrawer.Instance.RedStrokeBaseColors : null;
        PickTwoDifferentColors(palette, Color.white, new Color(1f, 0.5f, 0f), out Color c1, out Color c2);

        if (reflectTrail != null)
        {
            reflectTrail.emitting = true;
            ApplyTrailRendererSettings(isJust: true);
            reflectTrail.colorGradient = BuildReflectGradient(c1, c2);
        }

        ApplyParticleSettings(isJust: true);

        // JustBulletVFX（矢じり）を生成して弾にアタッチ
        if (justBulletVfxPrefab != null && activeJustVfx == null)
        {
            Color[] vfxPalette = PaddleDrawer.Instance != null
                ? (lineType == PaddleDot.LineType.Normal
                    ? PaddleDrawer.Instance.NormalStrokeBaseColors
                    : PaddleDrawer.Instance.RedStrokeBaseColors)
                : null;

            GameObject vfxGo = Instantiate(justBulletVfxPrefab, transform.position, Quaternion.identity, transform);
            activeJustVfx = vfxGo.GetComponent<JustBulletVFX>();
            if (activeJustVfx != null)
                activeJustVfx.Initialize(rb, vfxPalette);
        }
    }

    private void EnableReflectTrail()
    {
        if (trailJustActive)
        {
            if (reflectTrail != null) { reflectTrail.Clear(); reflectTrail.emitting = true; }
            return;
        }

        Color[] palette = PaddleDrawer.Instance != null ? PaddleDrawer.Instance.NormalStrokeBaseColors : null;
        PickTwoDifferentColors(palette, Color.white, Color.cyan, out Color c1, out Color c2);

        if (reflectTrail != null)
        {
            reflectTrail.Clear();
            reflectTrail.emitting = true;
            ApplyTrailRendererSettings(isJust: false);
            reflectTrail.colorGradient = BuildReflectGradient(c1, c2);
        }

        ApplyParticleSettings(isJust: false);
    }

    public void OnWallHit(Vector3 position, bool isPowered)
    {
        TryPlayWallHitFeedback(position);
        if (isPowered) TrySpawnJustPoweredVfx(position);
    }

    public void OnEnemyHit(Vector3 position, bool isPowered)
    {
        if (isPowered) TrySpawnJustPoweredVfx(position);
        else TrySpawnEnemyHitVfx(position);
    }

    public void PlayDisappearVfx(Vector3 position)
    {
        if (disappearVfxPrefab == null) return;

        Transform parent = (disappearVfxParent != null) ? disappearVfxParent : defaultVfxParent;

        GameObject vfx = DisappearVfxPool.Rent(disappearVfxPrefab, parent, position);
        if (vfx == null) return;

        vfx.transform.SetPositionAndRotation(position, Quaternion.identity);

        if (parent != null) vfx.transform.SetParent(parent, false);
        else vfx.transform.SetParent(null, true);

        vfx.SetActive(true);

        Animator anim = vfx.GetComponentInChildren<Animator>(true);
        if (anim != null)
        {
            anim.Rebind();
            anim.Update(0f);
            anim.Play(0, 0, 0f);
        }

        float seconds = disappearVfxDestroySeconds;
        if (seconds <= 0f) DisappearVfxPool.ReturnLater(disappearVfxPrefab, vfx, 0f);
        else DisappearVfxPool.ReturnLater(disappearVfxPrefab, vfx, seconds);
    }

    public void PlayDestroySeOnce(Vector3 position)
    {
        if (destroySePlayed) return;
        destroySePlayed = true;

        int count = 0;
        if (destroyClipA != null) count++;
        if (destroyClipB != null) count++;
        if (destroyClipC != null) count++;
        if (count <= 0) return;

        int pick = Random.Range(0, count);
        AudioClip clip = null;

        if (destroyClipA != null)
        {
            if (pick == 0) clip = destroyClipA;
            pick--;
        }
        if (clip == null && destroyClipB != null)
        {
            if (pick == 0) clip = destroyClipB;
            pick--;
        }
        if (clip == null && destroyClipC != null)
        {
            clip = destroyClipC;
        }

        if (clip == null) return;

        GameObject go = new GameObject("EnemyBullet_DestroySE");
        if (destroySeParent != null) go.transform.SetParent(destroySeParent, false);
        go.transform.position = position;

        AudioSource a = go.AddComponent<AudioSource>();
        a.playOnAwake = false;
        a.loop = false;
        a.spatialBlend = 0f;

        // SoundSettingsManagerのSE音量を適用
        float finalVolume = destroyVolume * (SoundSettingsManager.Instance != null ? SoundSettingsManager.Instance.SEVolume : 1f);
        a.PlayOneShot(clip, finalVolume);

        float life = Mathf.Max(0.01f, clip.length + Mathf.Max(0f, destroySeExtraDestroySeconds));
        Destroy(go, life);
    }

    public void OnExplosion(Vector3 position)
    {
        StopCountdownBeep();
        SpawnExplosionVfx(position);
        PlayExplosionSe(position);
    }

    public void OnUnreflectedDisappear(Vector3 position)
    {
        SpawnUnreflectedDisappearVfx(position);
        PlayUnreflectedDisappearSe(position);
    }

    public void StopEmitters()
    {
        if (reflectTrail != null) reflectTrail.emitting = false;
        if (reflectParticles != null) reflectParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        if (activeJustVfx != null) activeJustVfx.Stop();
    }

    public void OnWarpDisappear(Vector3 position, GameObject vfxPrefab, AudioClip se)
    {
        TrySpawnWarpVfx(position, vfxPrefab, warpDisappearVfxParent, warpDisappearVfxDestroySeconds);
        PlayWarpSe(position, se);
    }

    public void OnWarpReappear(Vector3 position, GameObject vfxPrefab, AudioClip se)
    {
        TrySpawnWarpVfx(position, vfxPrefab, warpReappearVfxParent, warpReappearVfxDestroySeconds);
        PlayWarpSe(position, se);
    }

    // =========================================================
    // Countdown Beep API
    // =========================================================

    /// <summary>
    /// カウントダウンBeepの更新（EnemyBullet.Explosion から毎フレーム呼ばれる）
    /// </summary>
    /// <param name="remainingSeconds">爆発までの残り秒数</param>
    /// <param name="blinkStartSeconds">点滅開始の残り秒数閾値</param>
    public void TickCountdownBeep(float remainingSeconds, float blinkStartSeconds)
    {
        if (countdownBeepClip == null) return;

        // 点滅開始前は何もしない
        float startThreshold = Mathf.Max(0f, beepStartRemainingSeconds);
        if (remainingSeconds > startThreshold)
        {
            beepActive = false;
            beepNextTime = -999f;
            return;
        }

        // Beep開始
        if (!beepActive)
        {
            beepActive = true;
            beepNextTime = Time.unscaledTime; // 即座に最初のBeep
        }

        float now = Time.unscaledTime;
        if (now < beepNextTime) return;

        // 間隔計算（残り時間に応じて加速）
        float t = (startThreshold <= 0.0001f) ? 1f : Mathf.Clamp01(1f - (remainingSeconds / startThreshold));
        float curveT = beepAccelCurve.Evaluate(t);

        float intervalMax = Mathf.Max(0.01f, beepIntervalMax);
        float intervalMin = Mathf.Max(0.01f, beepIntervalMin);
        float interval = Mathf.Lerp(intervalMax, intervalMin, curveT);

        // 次のBeep時刻を設定
        beepNextTime = now + interval;

        // Beep再生を試みる
        TryPlayCountdownBeep();
    }

    /// <summary>
    /// カウントダウンBeepを停止（爆発時または破棄時に呼ぶ）
    /// </summary>
    public void StopTrailEmitting()
    {
        if (reflectTrail != null)
        {
            reflectTrail.emitting = false;
            reflectTrail.Clear();
        }
    }

    public void StopCountdownBeep()
    {
        beepActive = false;
        beepNextTime = -999f;
    }

    private void TryPlayCountdownBeep()
    {
        if (countdownBeepClip == null) return;

        float now = Time.unscaledTime;

        // グローバルクールダウンチェック
        float cooldown = Mathf.Max(0f, beepGlobalCooldown);
        if (cooldown > 0f && (now - s_lastBeepTime) < cooldown) return;

        // 終了済みのBeepをリストから削除
        CleanupExpiredBeeps(now);

        // 同時再生数チェック（終了時刻リストのサイズで判定）
        int maxBeeps = Mathf.Max(1, maxConcurrentBeeps);
        if (s_beepEndTimes.Count >= maxBeeps) return;

        s_lastBeepTime = now;

        // AudioSourceの遅延初期化
        EnsureBeepAudioSource();
        if (beepAudioSource == null) return;

        // 音量減衰（同時再生数に応じて）
        float baseVol = Mathf.Clamp01(countdownBeepVolume);
        float adjustedVol = baseVol / Mathf.Sqrt(Mathf.Max(1f, s_beepEndTimes.Count + 1));

        // SoundSettingsManagerのSE音量を適用
        float finalVolume = adjustedVol * (SoundSettingsManager.Instance != null ? SoundSettingsManager.Instance.SEVolume : 1f);

        // PlayOneShotで再生（GameObjectを生成しない）
        beepAudioSource.PlayOneShot(countdownBeepClip, finalVolume);

        // 終了時刻をリストに追加（コルーチン不要）
        float clipLength = Mathf.Max(0.01f, countdownBeepClip.length);
        s_beepEndTimes.Add(now + clipLength);
    }

    private static void CleanupExpiredBeeps(float now)
    {
        // 終了時刻を過ぎたエントリを削除（後ろから削除して効率化）
        for (int i = s_beepEndTimes.Count - 1; i >= 0; i--)
        {
            if (s_beepEndTimes[i] <= now)
            {
                s_beepEndTimes.RemoveAt(i);
            }
        }
    }

    private void EnsureBeepAudioSource()
    {
        if (beepAudioSource != null) return;

        beepAudioSource = gameObject.AddComponent<AudioSource>();
        beepAudioSource.playOnAwake = false;
        beepAudioSource.loop = false;
        beepAudioSource.spatialBlend = 0f;
    }

    // =========================================================
    // Internal
    // =========================================================
    private Transform ResolveParent(Transform preferred)
    {
        if (preferred != null) return preferred;
        if (defaultVfxParent != null) return defaultVfxParent;
        return null;
    }

    private void TrySpawnPaddleHitVfx(Vector3 pos)
    {
        if (paddleHitVfxPrefab == null) return;

        float minInterval = Mathf.Max(0f, paddleHitVfxMinIntervalSeconds);
        float now = Time.unscaledTime;
        if (minInterval > 0f)
        {
            if ((now - lastPaddleHitVfxTime) < minInterval) return;
        }
        lastPaddleHitVfxTime = now;

        Transform parent = ResolveParent(paddleHitVfxParent);

        GameObject vfx = Instantiate(paddleHitVfxPrefab, pos, Quaternion.identity, parent);
        if (vfx == null) return;

        float sec = paddleHitVfxDestroySeconds;
        if (sec > 0f) Destroy(vfx, sec);
    }

    private void TrySpawnEnemyHitVfx(Vector3 pos)
    {
        if (enemyHitVfxPrefab == null) return;

        float minInterval = Mathf.Max(0f, enemyHitVfxMinIntervalSeconds);
        float now = Time.unscaledTime;
        if (minInterval > 0f)
        {
            if ((now - lastEnemyHitVfxTime) < minInterval) return;
        }
        lastEnemyHitVfxTime = now;

        Transform parent = ResolveParent(enemyHitVfxParent);

        GameObject vfx = Instantiate(enemyHitVfxPrefab, pos, Quaternion.identity, parent);
        if (vfx == null) return;

        float sec = enemyHitVfxDestroySeconds;
        if (sec > 0f) Destroy(vfx, sec);
    }

    private void TrySpawnJustPoweredVfx(Vector3 pos)
    {
        if (justPoweredVfxPrefab == null) return;

        float minInterval = Mathf.Max(0f, justPoweredVfxMinIntervalSeconds);
        float now = Time.unscaledTime;
        if (minInterval > 0f)
        {
            if ((now - lastJustPoweredVfxTime) < minInterval) return;
        }
        lastJustPoweredVfxTime = now;

        Transform parent = ResolveParent(justPoweredVfxParent);

        GameObject vfx = Instantiate(justPoweredVfxPrefab, pos, Quaternion.identity, parent);
        if (vfx == null) return;

        float sec = justPoweredVfxDestroySeconds;
        if (sec > 0f) Destroy(vfx, sec);
    }

    private void TryPlayWallHitFeedback(Vector3 pos)
    {
        float minInterval = Mathf.Max(0f, wallHitMinIntervalSeconds);
        float now = Time.unscaledTime;
        if (minInterval > 0f)
        {
            if ((now - lastWallHitFeedbackTime) < minInterval) return;
        }
        lastWallHitFeedbackTime = now;

        // VFX
        if (wallHitVfxPrefab != null)
        {
            Transform parent = ResolveParent(wallHitVfxParent);

            GameObject vfx = Instantiate(wallHitVfxPrefab, pos, Quaternion.identity, parent);
            if (vfx != null)
            {
                float sec = wallHitVfxDestroySeconds;
                if (sec > 0f) Destroy(vfx, sec);
            }
        }

        // SE
        AudioClip clip = null;
        if (wallHitClipA != null && wallHitClipB != null)
        {
            clip = (Random.value < 0.5f) ? wallHitClipA : wallHitClipB;
        }
        else if (wallHitClipA != null) clip = wallHitClipA;
        else if (wallHitClipB != null) clip = wallHitClipB;

        if (clip == null) return;

        GameObject go = new GameObject("EnemyBullet_WallSE");
        go.transform.position = pos;

        AudioSource a = go.AddComponent<AudioSource>();
        a.playOnAwake = false;
        a.loop = false;
        a.spatialBlend = 0f;

        // SoundSettingsManagerのSE音量を適用
        float finalVolume = wallHitVolume * (SoundSettingsManager.Instance != null ? SoundSettingsManager.Instance.SEVolume : 1f);
        a.PlayOneShot(clip, finalVolume);

        float life = Mathf.Max(0.01f, clip.length + 0.05f);
        Destroy(go, life);
    }

    private void SpawnExplosionVfx(Vector3 pos)
    {
        if (explosionVfxPrefab == null) return;

        Transform parent = ResolveParent(explosionVfxParent);

        GameObject vfx = Instantiate(explosionVfxPrefab, pos, Quaternion.identity, parent);
        if (vfx == null) return;

        float sec = explosionVfxDestroySeconds;
        if (sec > 0f) Destroy(vfx, sec);
    }

    private void PlayExplosionSe(Vector3 pos)
    {
        if (explosionSeClip == null) return;

        GameObject go = new GameObject("EnemyBullet_ExplosionSE");
        if (explosionSeParent != null) go.transform.SetParent(explosionSeParent, false);
        go.transform.position = pos;

        AudioSource a = go.AddComponent<AudioSource>();
        a.playOnAwake = false;
        a.loop = false;
        a.spatialBlend = 0f;

        // SoundSettingsManagerのSE音量を適用
        float finalVolume = explosionSeVolume * (SoundSettingsManager.Instance != null ? SoundSettingsManager.Instance.SEVolume : 1f);
        a.PlayOneShot(explosionSeClip, finalVolume);

        float life = Mathf.Max(0.01f, explosionSeClip.length + Mathf.Max(0f, explosionSeExtraDestroySeconds));
        Destroy(go, life);
    }

    private void SpawnUnreflectedDisappearVfx(Vector3 pos)
    {
        if (unreflectedDisappearVfxPrefab == null) return;

        Transform parent = ResolveParent(unreflectedDisappearVfxParent);

        GameObject vfx = Instantiate(unreflectedDisappearVfxPrefab, pos, Quaternion.identity, parent);
        if (vfx == null) return;

        float sec = unreflectedDisappearVfxDestroySeconds;
        if (sec > 0f) Destroy(vfx, sec);
    }

    private void PlayUnreflectedDisappearSe(Vector3 pos)
    {
        AudioClip clip = null;
        if (unreflectedDisappearClipA != null && unreflectedDisappearClipB != null)
        {
            clip = (Random.value < 0.5f) ? unreflectedDisappearClipA : unreflectedDisappearClipB;
        }
        else if (unreflectedDisappearClipA != null) clip = unreflectedDisappearClipA;
        else if (unreflectedDisappearClipB != null) clip = unreflectedDisappearClipB;

        if (clip == null) return;

        GameObject go = new GameObject("EnemyBullet_UnreflectedDisappearSE");
        if (unreflectedDisappearSeParent != null) go.transform.SetParent(unreflectedDisappearSeParent, false);
        go.transform.position = pos;

        AudioSource a = go.AddComponent<AudioSource>();
        a.playOnAwake = false;
        a.loop = false;
        a.spatialBlend = 0f;

        // SoundSettingsManagerのSE音量を適用
        float finalVolume = unreflectedDisappearVolume * (SoundSettingsManager.Instance != null ? SoundSettingsManager.Instance.SEVolume : 1f);
        a.PlayOneShot(clip, finalVolume);

        float life = Mathf.Max(0.01f, clip.length + Mathf.Max(0f, unreflectedDisappearSeExtraDestroySeconds));
        Destroy(go, life);
    }

    private void TrySpawnWarpVfx(Vector3 pos, GameObject vfxPrefab, Transform parent, float destroySeconds)
    {
        if (vfxPrefab == null) return;

        float minInterval = Mathf.Max(0f, warpVfxMinIntervalSeconds);
        float now = Time.unscaledTime;
        if (minInterval > 0f)
        {
            if ((now - lastWarpVfxTime) < minInterval) return;
        }
        lastWarpVfxTime = now;

        Transform resolvedParent = ResolveParent(parent);

        GameObject vfx = Instantiate(vfxPrefab, pos, Quaternion.identity, resolvedParent);
        if (vfx == null) return;

        float sec = destroySeconds;
        if (sec > 0f) Destroy(vfx, sec);
    }

    private void PlayWarpSe(Vector3 pos, AudioClip clip)
    {
        if (clip == null) return;

        GameObject go = new GameObject("EnemyBullet_WarpSE");
        if (warpSeParent != null) go.transform.SetParent(warpSeParent, false);
        go.transform.position = pos;

        AudioSource a = go.AddComponent<AudioSource>();
        a.playOnAwake = false;
        a.loop = false;
        a.spatialBlend = 0f;

        // SoundSettingsManagerのSE音量を適用
        float finalVolume = warpSeVolume * (SoundSettingsManager.Instance != null ? SoundSettingsManager.Instance.SEVolume : 1f);
        a.PlayOneShot(clip, finalVolume);

        float life = Mathf.Max(0.01f, clip.length + Mathf.Max(0f, warpSeExtraDestroySeconds));
        Destroy(go, life);
    }

    // =========================================================
    // Pool（Disappear VFX）
    // =========================================================
    private static class DisappearVfxPool
    {
        private static readonly System.Collections.Generic.Dictionary<GameObject, System.Collections.Generic.Queue<GameObject>> pools
            = new System.Collections.Generic.Dictionary<GameObject, System.Collections.Generic.Queue<GameObject>>(4);

        private static PoolRunner runner;

        public static GameObject Rent(GameObject prefab, Transform parent, Vector3 worldPos)
        {
            if (prefab == null) return null;

            EnsureRunner(parent);

            System.Collections.Generic.Queue<GameObject> q;
            if (!pools.TryGetValue(prefab, out q) || q == null)
            {
                q = new System.Collections.Generic.Queue<GameObject>(16);
                pools[prefab] = q;
            }

            while (q.Count > 0)
            {
                GameObject obj = q.Dequeue();
                if (obj != null) return obj;
            }

            return Object.Instantiate(prefab, worldPos, Quaternion.identity, parent);
        }

        public static void ReturnLater(GameObject prefab, GameObject instance, float seconds)
        {
            if (prefab == null || instance == null) return;

            EnsureRunner(instance.transform.parent);

            runner.StartCoroutine(ReturnCo(prefab, instance, seconds));
        }

        private static IEnumerator ReturnCo(GameObject prefab, GameObject instance, float seconds)
        {
            if (seconds > 0f) yield return new WaitForSeconds(seconds);

            if (instance == null) yield break;

            instance.SetActive(false);

            System.Collections.Generic.Queue<GameObject> q;
            if (!pools.TryGetValue(prefab, out q) || q == null)
            {
                q = new System.Collections.Generic.Queue<GameObject>(16);
                pools[prefab] = q;
            }

            q.Enqueue(instance);
        }

        private static void EnsureRunner(Transform preferredParent)
        {
            if (runner != null) return;

            GameObject go = new GameObject("EnemyBullet_DisappearVfxPoolRunner");
            if (preferredParent != null) go.transform.SetParent(preferredParent, false);
            Object.DontDestroyOnLoad(go);
            runner = go.AddComponent<PoolRunner>();
        }

        private class PoolRunner : MonoBehaviour { }
    }
}
