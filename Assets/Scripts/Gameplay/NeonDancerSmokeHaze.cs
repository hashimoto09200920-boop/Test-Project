using UnityEngine;

/// <summary>
/// NeonDancer専用の煙幕（NeonDancer_SmokeParticle）に「ネオンの霧」を重ねる部品。
/// 光る粒子（加算合成）は後ろが透けて視界を遮らないため、その下に通常の半透明合成の大きくぼやけた塊を
/// 敷き詰め、後ろの弾やエネミーを見えにくくする（元の砂煙と同じ「覆い隠す」役割）。
///
/// ★共有のSmokeCloudは改修しない。外から読める情報だけで同期する：
///   ・広がり：SmokeCloudの範囲判定（CircleCollider2D）の半径
///   ・透明度：SmokeCloudが煙パーティクルに毎フレーム設定するColor over Lifetimeのアルファ（フェードイン/アウト・円消去の早いフェード）
///   ・放出停止：煙パーティクルの放出が止まったら霧も止める
/// </summary>
[DisallowMultipleComponent]
public class NeonDancerSmokeHaze : MonoBehaviour
{
    [Tooltip("霧のパーティクル（Area1〜5用・Area6〜10用）")]
    [SerializeField] private ParticleSystem[] hazeSystems;
    [Tooltip("透明度を読み取る元（SmokeCloudが管理する煙パーティクル。未指定ならルートのParticleSystem）")]
    [SerializeField] private ParticleSystem smokeParticle;
    [Tooltip("霧1つ1つの最大不透明度（重なるほど濃くなる）")]
    [Range(0f, 1f)] [SerializeField] private float hazeAlpha = 0.45f;
    [Tooltip("霧の描画順（光る粒子(1000)より奥、他の物より手前）")]
    [SerializeField] private int sortingOrder = 999;

    private CircleCollider2D trigger;
    private Gradient grad;
    private GradientColorKey[] colorKeys;
    private GradientAlphaKey[] alphaKeys;
    private bool stopped;
    // ★負荷軽減：霧の透明度・広がりは値が変わったフレームだけ設定する（毎フレーム同じグラデーションを送り直さない）
    private float lastAppliedAlpha = float.NaN;
    private float lastAppliedRadius = float.NaN;

    private void Awake()
    {
        if (smokeParticle == null) smokeParticle = GetComponent<ParticleSystem>();
        trigger = GetComponent<CircleCollider2D>();

        grad = new Gradient();
        colorKeys = new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) };
        alphaKeys = new GradientAlphaKey[4];

        if (hazeSystems == null) return;
        foreach (var ps in hazeSystems)
        {
            if (ps == null) continue;
            var r = ps.GetComponent<ParticleSystemRenderer>();
            if (r != null) { r.sortingLayerName = "Default"; r.sortingOrder = sortingOrder; }
            ApplyAlpha(ps, 0f);
        }
    }

    // SmokeCloud.Initialize()はInstantiate直後（このStartより前）に呼ばれる
    private void Start()
    {
        if (hazeSystems == null) return;
        foreach (var ps in hazeSystems)
        {
            if (ps == null) continue;
            SyncRadius(ps);
            ps.Play();
        }
    }

    private void LateUpdate()
    {
        if (hazeSystems == null) return;
        float cloudAlpha = ReadCloudAlpha();
        bool smokeEmitting = smokeParticle == null || smokeParticle.isEmitting;

        float a = hazeAlpha * cloudAlpha;
        bool alphaChanged = a != lastAppliedAlpha;
        float radius = trigger != null ? Mathf.Max(0.1f, trigger.radius) : float.NaN;
        bool radiusChanged = trigger != null && radius != lastAppliedRadius;

        foreach (var ps in hazeSystems)
        {
            if (ps == null) continue;
            if (radiusChanged) SyncRadius(ps);
            if (alphaChanged) ApplyAlpha(ps, cloudAlpha);
            if (!smokeEmitting && !stopped) ps.Stop(false, ParticleSystemStopBehavior.StopEmitting);
        }
        if (alphaChanged) lastAppliedAlpha = a;
        if (radiusChanged) lastAppliedRadius = radius;
        if (!smokeEmitting) stopped = true;
    }

    private void SyncRadius(ParticleSystem ps)
    {
        if (trigger == null) return;
        var shape = ps.shape;
        shape.radius = Mathf.Max(0.1f, trigger.radius);
    }

    private float ReadCloudAlpha()
    {
        if (smokeParticle == null) return 1f;
        var col = smokeParticle.colorOverLifetime;
        if (!col.enabled) return 1f;
        Gradient g = col.color.gradient;
        if (g == null) return 1f;
        GradientAlphaKey[] keys = g.alphaKeys; // ★負荷軽減：alphaKeysは読むたびに配列が作られるので1回だけ読む
        if (keys == null || keys.Length == 0) return 1f;
        return keys[0].alpha; // SmokeCloudは全キー同じアルファ（クラウド全体の透明度）を設定している
    }

    // 霧1つ1つ：生まれてふわっと現れ、消える時にふわっと消える × クラウド全体の透明度
    private void ApplyAlpha(ParticleSystem ps, float cloudAlpha)
    {
        float a = hazeAlpha * cloudAlpha;
        alphaKeys[0] = new GradientAlphaKey(0f, 0f);
        alphaKeys[1] = new GradientAlphaKey(a, 0.25f);
        alphaKeys[2] = new GradientAlphaKey(a, 0.75f);
        alphaKeys[3] = new GradientAlphaKey(0f, 1f);
        grad.SetKeys(colorKeys, alphaKeys);
        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = new ParticleSystem.MinMaxGradient(grad);
    }
}
