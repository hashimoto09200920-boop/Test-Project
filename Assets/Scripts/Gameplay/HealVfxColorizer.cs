using UnityEngine;

/// <summary>
/// HealVFX Prefabにアタッチ。Start()時に重み付きランダムカラーで粒子を手動発射する。
/// ParticleSystemのEmission Burstは0にしておく（HealVfxSetupで自動設定）。
/// </summary>
[RequireComponent(typeof(ParticleSystem))]
public class HealVfxColorizer : MonoBehaviour
{
    [Header("Emit")]
    [Tooltip("一度に発射する粒子数。")]
    [SerializeField] private int burstCount = 8;

    [Tooltip("発射位置の広がり半径（ワールド単位）。")]
    [SerializeField] private float spreadRadius = 0.15f;

    [Header("Color A（緑）")]
    [SerializeField] private Color colorA = new Color(0.502f, 1f, 0.502f);
    [Tooltip("Color A の出現割合（0〜1）。")]
    [SerializeField, Range(0f, 1f)] private float weightA = 0.7f;

    [Header("Color B（シアン）")]
    [SerializeField] private Color colorB = Color.cyan;
    [Tooltip("Color B の出現割合（0〜1）。残り(1-A-B)がColor Cになる。")]
    [SerializeField, Range(0f, 1f)] private float weightB = 0.2f;

    [Header("Color C（白）")]
    [SerializeField] private Color colorC = Color.white;

    private void Start()
    {
        var ps = GetComponent<ParticleSystem>();

        Debug.Log($"[HealVfxColorizer] Start: Clear前のparticleCount={ps.particleCount}");

        // PSのburst等で既に発射された粒子をクリア
        ps.Clear();

        var ep = new ParticleSystem.EmitParams();
        ep.velocity = Vector3.zero;

        for (int i = 0; i < burstCount; i++)
        {
            ep.position = transform.position + (Vector3)(Random.insideUnitCircle * spreadRadius);
            ps.Emit(ep, 1);
        }

        var particles = new ParticleSystem.Particle[ps.particleCount];
        int count = ps.GetParticles(particles);
        Debug.Log($"[HealVfxColorizer] Emit後のcount={count}");

        for (int i = 0; i < count; i++)
        {
            particles[i].startColor = PickColor();
        }
        ps.SetParticles(particles, count);
    }

    private Color PickColor()
    {
        float wA = Mathf.Clamp01(weightA);
        float wB = Mathf.Clamp01(weightB);
        if (wA + wB > 1f)
        {
            float total = wA + wB;
            wA /= total;
            wB /= total;
        }

        float r = Random.value;
        if (r < wA) return colorA;
        if (r < wA + wB) return colorB;
        return colorC;
    }
}
