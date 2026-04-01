using UnityEngine;

/// <summary>
/// 敵ごとの撃破VFX設定値。EnemyData にフィールドとして持たせ、
/// useCustomDeathVfx=true の敵のみ DeathVFXSettings.ApplyConfig() で注入する。
/// false の敵は VFX_Explosion_A_RingSparks の Prefab 設定値をそのまま使う。
/// </summary>
[System.Serializable]
public class DeathVfxConfig
{
    [Header("Sparks - 射出")]
    [Tooltip("バースト粒数")]
    public int burstCount = 80;

    [Tooltip("広がり角度（度）。小さくすると集中する")]
    [Range(0f, 90f)]
    public float shapeAngle = 20f;

    [Tooltip("射出速度 最小")]
    public float startSpeedMin = 2f;

    [Tooltip("射出速度 最大")]
    public float startSpeedMax = 8f;

    [Header("Sparks - サイズ")]
    [Tooltip("初期サイズ 最小")]
    public float startSizeMin = 0.02f;

    [Tooltip("初期サイズ 最大")]
    public float startSizeMax = 0.25f;

    [Header("Sparks - 寿命・重力")]
    [Tooltip("粒の寿命 最小（秒）")]
    public float lifetimeMin = 0.2f;

    [Tooltip("粒の寿命 最大（秒）")]
    public float lifetimeMax = 0.6f;

    [Tooltip("重力（0=なし、正=下に落ちる）")]
    public float gravity = 0.3f;

    [Header("Sparks - 色")]
    [Tooltip("開始色 下限（爆発色: 赤橙など）")]
    public Color startColorMin = new Color(1f, 0.25f, 0f, 1f);

    [Tooltip("開始色 上限（爆発色: 白黄など）")]
    public Color startColorMax = new Color(1f, 0.95f, 0.6f, 1f);

    [Header("Ring - サイズ・色")]
    [Tooltip("Ringのスケール（大きくするほど広い爆発リングになる）")]
    public float ringScale = 6f;

    [Tooltip("DeathRingColorCycleのHue下限（0=赤）")]
    [Range(0f, 1f)]
    public float ringMinHue = 0f;

    [Tooltip("DeathRingColorCycleのHue上限（0.14=黄橙）")]
    [Range(0f, 1f)]
    public float ringMaxHue = 0.14f;

    [Tooltip("DeathRingColorCycleの循環速度")]
    public float ringCycleSpeed = 4f;
}
