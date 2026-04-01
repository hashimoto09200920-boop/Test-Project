using UnityEngine;

/// <summary>
/// 敵撃破VFX（VFX_Explosion_A_RingSparks）のパラメータをInspectorで一元管理する。
/// VFX_Explosion_A_RingSparks ルートにアタッチし、Awake()でPS_SparksとRingに設定を適用する。
/// Play前にInspectorで全パラメータを調整可能。
/// </summary>
public class DeathVFXSettings : MonoBehaviour
{
    // -------------------------------------------------------
    // Sparks
    // -------------------------------------------------------
    [Header("Sparks - 射出")]
    [Tooltip("バースト粒数")]
    [SerializeField] private int burstCount = 80;

    [Tooltip("広がり角度（度）。小さくすると集中する")]
    [Range(0f, 90f)]
    [SerializeField] private float shapeAngle = 20f;

    [Tooltip("射出速度 最小")]
    [SerializeField] private float startSpeedMin = 2f;

    [Tooltip("射出速度 最大")]
    [SerializeField] private float startSpeedMax = 8f;

    [Header("Sparks - サイズ")]
    [Tooltip("初期サイズ 最小")]
    [SerializeField] private float startSizeMin = 0.02f;

    [Tooltip("初期サイズ 最大")]
    [SerializeField] private float startSizeMax = 0.25f;

    [Header("Sparks - 寿命・重力")]
    [Tooltip("粒の寿命 最小（秒）")]
    [SerializeField] private float lifetimeMin = 0.2f;

    [Tooltip("粒の寿命 最大（秒）")]
    [SerializeField] private float lifetimeMax = 0.6f;

    [Tooltip("重力（0=なし、正=下に落ちる）")]
    [SerializeField] private float gravity = 0.3f;

    [Header("Sparks - 色")]
    [Tooltip("開始色 下限（爆発色: 赤橙など）")]
    [SerializeField] private Color startColorMin = new Color(1f, 0.25f, 0f, 1f);

    [Tooltip("開始色 上限（爆発色: 白黄など）")]
    [SerializeField] private Color startColorMax = new Color(1f, 0.95f, 0.6f, 1f);

    // -------------------------------------------------------
    // Ring
    // -------------------------------------------------------
    [Header("Ring - サイズ・色")]
    [Tooltip("Ringのスケール（大きくするほど広い爆発リングになる）")]
    [SerializeField] private float ringScale = 6f;

    [Tooltip("DeathRingColorCycleのHue下限（0=赤）")]
    [Range(0f, 1f)]
    [SerializeField] private float ringMinHue = 0f;

    [Tooltip("DeathRingColorCycleのHue上限（0.14=黄橙）")]
    [Range(0f, 1f)]
    [SerializeField] private float ringMaxHue = 0.14f;

    [Tooltip("DeathRingColorCycleの循環速度")]
    [SerializeField] private float ringCycleSpeed = 4f;

    // -------------------------------------------------------
    // 適用
    // -------------------------------------------------------
    private void Awake()
    {
        ApplySettings();
    }

    private void ApplySettings()
    {
        ApplySparks();
        ApplyRing();
    }

    /// <summary>
    /// EnemyData.useCustomDeathVfx=true の敵のみ呼ばれる。
    /// 各フィールドを config の値で上書きしてから再適用する。
    /// </summary>
    public void ApplyConfig(DeathVfxConfig config)
    {
        if (config == null) return;

        // Awake()でPS稼働済みのため、一度全停止してからパラメータを上書きし再生する
        foreach (var ps in GetComponentsInChildren<ParticleSystem>(true))
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        burstCount     = config.burstCount;
        shapeAngle     = config.shapeAngle;
        startSpeedMin  = config.startSpeedMin;
        startSpeedMax  = config.startSpeedMax;
        startSizeMin   = config.startSizeMin;
        startSizeMax   = config.startSizeMax;
        lifetimeMin    = config.lifetimeMin;
        lifetimeMax    = config.lifetimeMax;
        gravity        = config.gravity;
        startColorMin  = config.startColorMin;
        startColorMax  = config.startColorMax;
        ringScale      = config.ringScale;
        ringMinHue     = config.ringMinHue;
        ringMaxHue     = config.ringMaxHue;
        ringCycleSpeed = config.ringCycleSpeed;

        ApplySettings();

        foreach (var ps in GetComponentsInChildren<ParticleSystem>(true))
            ps.Play();
    }

    private void ApplySparks()
    {
        Transform sparksTf = transform.Find("PS_Sparks");
        if (sparksTf == null) return;

        ParticleSystem ps = sparksTf.GetComponent<ParticleSystem>();
        if (ps == null) return;

        var main = ps.main;
        main.startLifetime  = new ParticleSystem.MinMaxCurve(lifetimeMin, lifetimeMax);
        main.startSpeed     = new ParticleSystem.MinMaxCurve(startSpeedMin, startSpeedMax);
        main.startSize      = new ParticleSystem.MinMaxCurve(startSizeMin, startSizeMax);
        main.gravityModifier = new ParticleSystem.MinMaxCurve(gravity);
        main.startColor     = new ParticleSystem.MinMaxGradient(startColorMin, startColorMax);

        var emission = ps.emission;
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, burstCount) });

        var shape = ps.shape;
        shape.angle = shapeAngle;
    }

    private void ApplyRing()
    {
        Transform ringTf = transform.Find("Ring");
        if (ringTf == null) return;

        // Ring の Animator が localScale を毎フレーム上書きするため、
        // localScale 直接設定では効かない。
        // AnimatorOverrideController でスケールキーフレームを係数倍した
        // 新クリップに差し替えることで正しく反映させる。
        Animator animator = ringTf.GetComponent<Animator>();
        if (animator != null && animator.runtimeAnimatorController != null)
        {
            ApplyRingScale(animator, ringScale);
        }

        DeathRingColorCycle cycler = ringTf.GetComponent<DeathRingColorCycle>();
        if (cycler != null)
            cycler.SetConfig(ringMinHue, ringMaxHue, ringCycleSpeed);
    }

    /// <summary>
    /// Ring_Explosion.anim のスケールキーフレームを scale 倍した新クリップを
    /// AnimatorOverrideController 経由で差し替える。
    /// 元クリップの値: scale (0.2→3)、alpha (1→0)、duration 0.8333s
    /// </summary>
    private static void ApplyRingScale(Animator animator, float scale)
    {
        // AnimatorOverrideControllerが既にセットされている場合、
        // その基底（オリジナルのAnimatorController）を取得する。
        // OverrideControllerを重ねるとクリップ名が消えてスケールが元に戻るバグを防ぐ。
        RuntimeAnimatorController baseController = animator.runtimeAnimatorController;
        while (baseController is AnimatorOverrideController aoc)
            baseController = aoc.runtimeAnimatorController;

        AnimationClip[] origClips = baseController.animationClips;
        if (origClips == null || origClips.Length == 0) return;

        // 元アニメーションのキーフレーム値（Ring_Explosion.anim から）
        const float startS   = 0.2f;
        const float endS     = 3f;
        const float duration = 0.8333333f;

        float s0 = startS * scale;
        float s1 = endS   * scale;

        // スケール・alpha カーブを構築（元と同じ2キー補間）
        AnimationCurve curveX = BuildCurve(duration, s0, s1);
        AnimationCurve curveY = BuildCurve(duration, s0, s1);
        AnimationCurve curveZ = BuildCurve(duration, 1f, 1f); // Z は常に1

        AnimationCurve curveAlpha = BuildCurve(duration, 1f, 0f); // フェードアウト

        AnimationClip newClip = new AnimationClip { legacy = false };
        newClip.SetCurve("", typeof(Transform),      "localScale.x", curveX);
        newClip.SetCurve("", typeof(Transform),      "localScale.y", curveY);
        newClip.SetCurve("", typeof(Transform),      "localScale.z", curveZ);
        newClip.SetCurve("", typeof(SpriteRenderer), "m_Color.a",    curveAlpha);

        var overrideController = new AnimatorOverrideController(baseController);
        overrideController[origClips[0].name] = newClip;
        animator.runtimeAnimatorController = overrideController;
    }

    private static AnimationCurve BuildCurve(float duration, float startVal, float endVal)
    {
        return new AnimationCurve(
            new Keyframe(0f,       startVal, 0f, 0f),
            new Keyframe(duration, endVal,   0f, 0f)
        );
    }
}
