using UnityEngine;

/// <summary>
/// 弾発射時にピクセルを前方に散らすVFX。
/// すべての設定はPlay前のInspectorで調整可能。
/// EnemyShooterが Play(fireDir) を呼ぶと即座に放出し、lifetimeMax後に自動消滅。
/// </summary>
public class FirePixelVFX : MonoBehaviour
{
    [Header("ピクセル数・方向")]
    [Tooltip("放出するピクセル数")]
    [SerializeField] private int pixelCount = 10;
    [Tooltip("前方への広がり半角（70 = 前方140°）")]
    [SerializeField] private float halfAngleDeg = 70f;

    [Header("速度")]
    [SerializeField] private float speedMin = 4f;
    [SerializeField] private float speedMax = 9f;

    [Header("寿命（秒）")]
    [SerializeField] private float lifetimeMin = 0.15f;
    [SerializeField] private float lifetimeMax = 0.40f;

    [Header("サイズ")]
    [SerializeField] private float sizeMin = 0.04f;
    [SerializeField] private float sizeMax = 0.12f;

    [Header("色")]
    [SerializeField] private Color color = Color.white;

    [Header("物理")]
    [Tooltip("重力係数（0=重力なし、正=下向き落下）")]
    [SerializeField] private float gravityScale = 0f;

    [Header("表示")]
    [SerializeField] private string sortingLayerName = "Default";
    [SerializeField] private int sortingOrder = 10;
    [Tooltip("ピクセル用マテリアル（Sprites/DefaultやParticles/Unlitなど）")]
    [SerializeField] private Material pixelMaterial;

    private ParticleSystem ps;

    private void Awake()
    {
        ps = GetComponent<ParticleSystem>();
        if (ps == null) ps = gameObject.AddComponent<ParticleSystem>();
        ConfigurePS();
    }

    private void ConfigurePS()
    {
        var main = ps.main;
        main.loop            = false;
        main.playOnAwake     = false;
        main.startSpeed      = 0f;
        main.startLifetime   = new ParticleSystem.MinMaxCurve(lifetimeMin, lifetimeMax);
        main.startSize       = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
        main.startColor      = new ParticleSystem.MinMaxGradient(color);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = gravityScale;
        main.maxParticles    = pixelCount + 5;

        var emission = ps.emission;
        emission.enabled      = true;
        emission.rateOverTime = 0f;
        emission.SetBursts(new ParticleSystem.Burst[0]);

        var shape = ps.shape;
        shape.enabled = false;

        var colorOL = ps.colorOverLifetime;
        colorOL.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 0.5f) },
            new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.5f), new GradientAlphaKey(0f, 1f) }
        );
        colorOL.color = new ParticleSystem.MinMaxGradient(grad);

        var sizeOL = ps.sizeOverLifetime;
        sizeOL.enabled = true;
        sizeOL.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 1f),
            new Keyframe(1f, 0.1f)
        ));

        var ren = ps.GetComponent<ParticleSystemRenderer>();
        ren.renderMode       = ParticleSystemRenderMode.Billboard;
        ren.sortingLayerName = sortingLayerName;
        ren.sortingOrder     = sortingOrder;
        if (pixelMaterial != null) ren.sharedMaterial = pixelMaterial;
    }

    public void Play(Vector2 fireDir) => PlayInternal(fireDir);

    private void PlayInternal(Vector2 fireDir)
    {
        if (ps == null) return;
        if (fireDir.sqrMagnitude < 0.0001f) fireDir = Vector2.up;
        fireDir = fireDir.normalized;

        var main = ps.main;
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(color.r, color.g, color.b, 1f));

        if (!ps.isPlaying) ps.Play();

        var p = new ParticleSystem.EmitParams();
        for (int i = 0; i < pixelCount; i++)
        {
            float angle = Random.Range(-halfAngleDeg, halfAngleDeg) * Mathf.Deg2Rad;
            float cos   = Mathf.Cos(angle);
            float sin   = Mathf.Sin(angle);
            Vector2 dir = new Vector2(
                fireDir.x * cos - fireDir.y * sin,
                fireDir.x * sin + fireDir.y * cos
            );
            p.velocity      = new Vector3(dir.x, dir.y, 0f) * Random.Range(speedMin, speedMax);
            p.startSize     = Random.Range(sizeMin, sizeMax);
            p.startLifetime = Random.Range(lifetimeMin, lifetimeMax);
            p.position      = transform.position;
            ps.Emit(p, 1);
        }

        Destroy(gameObject, lifetimeMax + 0.1f);
    }
}
