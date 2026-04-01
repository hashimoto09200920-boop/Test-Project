using UnityEngine;

/// <summary>
/// VFX_EnemyHit_Normal にアタッチして、グラデーション・色・サイズ等を
/// Play前のInspectorで調整できるようにするコンポーネント。
///
/// 使い方:
///   1. Inspector でパラメータを設定する
///   2. 右クリックメニュー「Apply VFX Settings」を実行して Prefab に焼き込む
///   3. Play時は Awake() で自動適用される
/// </summary>
[RequireComponent(typeof(ParticleSystem))]
[DisallowMultipleComponent]
public class EnemyHitVfxController : MonoBehaviour
{
    // =============================================
    // Color
    // =============================================
    [Header("Color over Lifetime")]
    [Tooltip("パーティクルが生まれてから消えるまでの色グラデーション。\n最初を鮮やかな色、最後をアルファ0にすると自然にフェードアウトする。")]
    [SerializeField] private Gradient colorGradient = CreateDefaultGradient();

    [Header("Start Color (HDR)")]
    [Tooltip("パーティクル生成時の初期色。HDR対応（intensity > 1 で発光感が出る）。\n※ Color over Lifetime と乗算されるので、白にしてグラデーションのみで制御しても良い。")]
    [ColorUsage(true, true)]
    [SerializeField] private Color startColor = Color.white;

    // =============================================
    // Size
    // =============================================
    [Header("Size")]
    [Tooltip("パーティクルの初期サイズ。0.1〜0.3 くらいが弾ヒット向け。")]
    [SerializeField] private float startSize = 0.18f;

    [Tooltip("Size over Lifetime を使うか")]
    [SerializeField] private bool useSizeOverLifetime = true;

    [Tooltip("Size over Lifetime カーブ。右下がりにすると消えながら縮む。")]
    [SerializeField] private AnimationCurve sizeOverLifetimeCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);

    // =============================================
    // Motion
    // =============================================
    [Header("Motion")]
    [Tooltip("パーティクルの初期速度。大きいほど広がりが速い。")]
    [SerializeField] private float startSpeed = 4f;

    [Tooltip("パーティクルの寿命（秒）。")]
    [SerializeField] private float startLifetime = 0.22f;

    // =============================================
    // Emission (Burst)
    // =============================================
    [Header("Burst")]
    [Tooltip("1回のバーストで出るパーティクル数。")]
    [SerializeField] private int burstCount = 10;

    // =============================================
    // Shape
    // =============================================
    [Header("Shape")]
    [Tooltip("放出形状の半径。0 に近いほど1点から広がる。")]
    [SerializeField] private float shapeRadius = 0.01f;

    // =============================================
    // Internal
    // =============================================
    private ParticleSystem _ps;

    private void Awake()
    {
        ApplySettings();
    }

    /// <summary>
    /// Inspector で設定した値を ParticleSystem に適用する。
    /// Play前に右クリック → Apply VFX Settings で Prefab に焼き込める。
    /// </summary>
    [ContextMenu("Apply VFX Settings")]
    public void ApplySettings()
    {
        if (_ps == null) _ps = GetComponent<ParticleSystem>();
        if (_ps == null) return;

        // --- Main ---
        var main = _ps.main;
        main.startColor    = new ParticleSystem.MinMaxGradient(startColor);
        main.startSize     = startSize;
        main.startSpeed    = startSpeed;
        main.startLifetime = startLifetime;

        // --- Color over Lifetime ---
        var col = _ps.colorOverLifetime;
        col.enabled = true;
        col.color   = new ParticleSystem.MinMaxGradient(colorGradient);

        // --- Size over Lifetime ---
        var sizeOL = _ps.sizeOverLifetime;
        sizeOL.enabled = useSizeOverLifetime;
        if (useSizeOverLifetime)
        {
            sizeOL.size = new ParticleSystem.MinMaxCurve(1f, sizeOverLifetimeCurve);
        }

        // --- Burst Emission ---
        var emission = _ps.emission;
        emission.enabled = true;
        emission.SetBursts(new[]
        {
            new ParticleSystem.Burst(0f, (short)burstCount)
        });

        // --- Shape ---
        var shape = _ps.shape;
        shape.radius = shapeRadius;
    }

    // =============================================
    // Default gradient factory (static)
    // =============================================
    private static Gradient CreateDefaultGradient()
    {
        var g = new Gradient();
        g.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(new Color(1.0f, 0.9f, 0.3f), 0.00f),   // 明るい黄
                new GradientColorKey(new Color(1.0f, 0.4f, 0.1f), 0.40f),   // オレンジ
                new GradientColorKey(new Color(0.8f, 0.2f, 0.8f), 0.75f),   // 紫
                new GradientColorKey(new Color(0.3f, 0.3f, 1.0f), 1.00f),   // 青
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(1.0f, 0.00f),
                new GradientAlphaKey(0.8f, 0.40f),
                new GradientAlphaKey(0.0f, 1.00f),
            }
        );
        return g;
    }
}
