using UnityEngine;

/// <summary>
/// ジャスト反射弾に追従する貫通VFX。
/// 全パラメータ（サイズ・寿命・速度・形状）はこのコンポーネントのInspectorで調整。
/// 子PSのMain moduleは直接使用しない（シミュレーション空間=Worldのみ必要）。
/// </summary>
public class JustBulletVFX : MonoBehaviour
{
    // =============================================
    // A: Arrowhead（矢じり）
    // ★形状調整：AngleDeg↓で鋭く、Spread↓で短く、StepsPerArm↓で粒を減らす
    // ★サイズ：ArrowheadStartSize で粒1つのサイズを調整
    // =============================================
    [Header("A: Arrowhead（矢じり）")]
    [Tooltip("矢じり形状の粒を出すParticleSystem（SimulationSpace=World, PlayOnAwake=OFF, Looping=ON, Emission Rate=0）。")]
    [SerializeField] private ParticleSystem arrowheadPS;

    [Tooltip("粒1つのサイズ（ワールド単位）。★大きすぎる場合はここを小さくする。")]
    [SerializeField] private float arrowheadStartSize = 0.05f;

    [Tooltip("粒の寿命（秒）。短いほど残像が薄い。")]
    [SerializeField] private float arrowheadLifetime = 0.10f;

    [Tooltip("矢じり両腕の開き角度（度）。★小さくすると先端が鋭くなり根元が細くなる。")]
    [SerializeField, Range(5f, 70f)] private float arrowheadAngleDeg = 30f;

    [Tooltip("腕の先端までの長さ（ワールド単位）。★小さくすると腕が短くなる。")]
    [SerializeField] private float arrowheadSpread = 0.15f;

    [Tooltip("弾中心から先端までの前方オフセット（ワールド単位）。")]
    [SerializeField] private float arrowheadForwardOffset = 0.2f;

    [Tooltip("片腕あたりの粒数。★少ないほど根元が薄くなる。")]
    [SerializeField, Range(1, 6)] private int arrowheadStepsPerArm = 3;

    // =============================================
    // B: Flash Ring（ジャスト成立瞬間の一発リング）
    // ★速度：FlashRingSpeed で調整
    // ★サイズ：FlashRingStartSize で調整
    // =============================================
    [Header("B: Flash Ring（一発リング）")]
    [Tooltip("フラッシュリングを出すParticleSystem（SimulationSpace=World, PlayOnAwake=OFF, Looping=OFF, Emission Rate=0）。")]
    [SerializeField] private ParticleSystem flashRingPS;

    [Tooltip("粒1つのサイズ（ワールド単位）。")]
    [SerializeField] private float flashRingStartSize = 0.08f;

    [Tooltip("粒の寿命（秒）。リングが広がる時間に合わせる。")]
    [SerializeField] private float flashRingLifetime = 0.30f;

    [Tooltip("粒の速度（ワールド単位/秒）。大きいほどリングが速く広がる。")]
    [SerializeField] private float flashRingSpeed = 1.5f;

    [Tooltip("リングを構成する粒数。")]
    [SerializeField, Range(4, 24)] private int flashRingCount = 12;

    // =============================================
    // C: Drill Spiral（螺旋ドリル）
    // ★サイズ：DrillStartSize で調整
    // =============================================
    [Header("C: Drill Spiral（螺旋）")]
    [Tooltip("螺旋ドリルを出すParticleSystem（SimulationSpace=World, PlayOnAwake=OFF, Looping=ON, Emission Rate=0）。")]
    [SerializeField] private ParticleSystem drillPS;

    [Tooltip("粒1つのサイズ（ワールド単位）。")]
    [SerializeField] private float drillStartSize = 0.05f;

    [Tooltip("粒の寿命（秒）。")]
    [SerializeField] private float drillLifetime = 0.15f;

    [Tooltip("螺旋の腕数（等間隔配置）。")]
    [SerializeField, Range(2, 6)] private int drillArmCount = 3;

    [Tooltip("螺旋の回転半径（ワールド単位）。")]
    [SerializeField] private float drillRadius = 0.1f;

    [Tooltip("螺旋の回転速度（度/秒）。")]
    [SerializeField] private float drillDegPerSec = 360f;

    [Tooltip("弾中心からの前方オフセット（ワールド単位）。")]
    [SerializeField] private float drillForwardOffset = 0.1f;

    // =============================================
    // D: Shock Wave Cone（周期衝撃波コーン）
    // ★速度：ShockwaveSpeed で調整
    // ★サイズ：ShockwaveStartSize で調整
    // =============================================
    [Header("D: Shock Wave Cone（周期衝撃波）")]
    [Tooltip("衝撃波コーンを出すParticleSystem（SimulationSpace=World, PlayOnAwake=OFF, Looping=ON, Emission Rate=0）。")]
    [SerializeField] private ParticleSystem shockwavePS;

    [Tooltip("粒1つのサイズ（ワールド単位）。")]
    [SerializeField] private float shockwaveStartSize = 0.06f;

    [Tooltip("粒の寿命（秒）。")]
    [SerializeField] private float shockwaveLifetime = 0.20f;

    [Tooltip("粒の速度（ワールド単位/秒）。大きいほど衝撃波が速く広がる。")]
    [SerializeField] private float shockwaveSpeed = 2.0f;

    [Tooltip("衝撃波の発射間隔（秒）。")]
    [SerializeField] private float shockwaveIntervalSec = 0.25f;

    [Tooltip("衝撃波一発あたりの粒数。")]
    [SerializeField, Range(3, 12)] private int shockwaveCount = 5;

    [Tooltip("コーンの半角（度）。大きいほど横に広がる。")]
    [SerializeField] private float shockwaveHalfAngleDeg = 20f;

    // =============================================
    // Runtime
    // =============================================
    private Rigidbody2D parentRb;
    private Color[] colorPalette;
    private float drillAngle = 0f;
    private float shockwaveTimer = 0f;

    public void Initialize(Rigidbody2D rb, Color[] palette)
    {
        parentRb = rb;
        colorPalette = palette;
        EmitFlashRing();
    }

    public void Stop()
    {
        StopPS(arrowheadPS);
        StopPS(flashRingPS);
        StopPS(drillPS);
        StopPS(shockwavePS);
    }

    private static void StopPS(ParticleSystem ps)
    {
        if (ps != null) ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }

    private Color GetRandomColor()
    {
        if (colorPalette == null || colorPalette.Length == 0) return Color.white;
        Color c = colorPalette[Random.Range(0, colorPalette.Length)];
        if (c.a <= 0f) c.a = 1f;
        return c;
    }

    private void Update()
    {
        if (parentRb == null) return;

        Vector2 vel = parentRb.linearVelocity;
        if (vel.sqrMagnitude < 0.0001f) return;

        Vector2 fwd = vel.normalized;
        Vector3 pos = parentRb.transform.position;

        drillAngle += drillDegPerSec * Time.deltaTime;

        EmitArrowhead(pos, fwd);
        EmitDrill(pos, fwd);

        shockwaveTimer += Time.deltaTime;
        if (shockwaveTimer >= shockwaveIntervalSec)
        {
            shockwaveTimer = 0f;
            EmitShockwave(pos, fwd);
        }
    }

    // =============================================
    // A: Arrowhead
    // =============================================
    private void EmitArrowhead(Vector3 center, Vector2 fwd)
    {
        if (arrowheadPS == null) return;

        Vector2 right = new Vector2(-fwd.y, fwd.x);
        float angleRad = arrowheadAngleDeg * Mathf.Deg2Rad;
        float cosA = Mathf.Cos(angleRad);
        float sinA = Mathf.Sin(angleRad);

        var ep = new ParticleSystem.EmitParams();
        ep.velocity = Vector3.zero;
        ep.startSize = Mathf.Max(0.001f, arrowheadStartSize);
        ep.startLifetime = Mathf.Max(0.01f, arrowheadLifetime);

        // ∧の頂点：両腕が合流する単一の先端点
        Vector3 tip = center + (Vector3)(fwd * arrowheadForwardOffset);

        // 先端粒子（1粒）
        ep.position = tip;
        ep.startColor = GetRandomColor();
        arrowheadPS.Emit(ep, 1);

        for (int arm = 0; arm < 2; arm++)
        {
            float sign = (arm == 0) ? 1f : -1f;
            // 先端から後方＋側方へ伸びる方向
            Vector2 armDir = (-fwd * cosA + right * (sign * sinA)).normalized;

            for (int i = 0; i < arrowheadStepsPerArm; i++)
            {
                float t = (i + 1f) / arrowheadStepsPerArm;
                ep.position = tip + (Vector3)(armDir * (t * arrowheadSpread));
                ep.startColor = GetRandomColor();
                arrowheadPS.Emit(ep, 1);
            }
        }
    }

    // =============================================
    // B: Flash Ring（Initialize時に一発）
    // =============================================
    private void EmitFlashRing()
    {
        if (flashRingPS == null || parentRb == null) return;

        Vector3 pos = parentRb.transform.position;

        var ep = new ParticleSystem.EmitParams();
        ep.startSize = Mathf.Max(0.001f, flashRingStartSize);
        ep.startLifetime = Mathf.Max(0.01f, flashRingLifetime);

        for (int i = 0; i < flashRingCount; i++)
        {
            float angle = i * (Mathf.PI * 2f / flashRingCount);
            Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            ep.position = pos;
            ep.velocity = (Vector3)(dir * flashRingSpeed);
            ep.startColor = GetRandomColor();
            flashRingPS.Emit(ep, 1);
        }
    }

    // =============================================
    // C: Drill Spiral
    // =============================================
    private void EmitDrill(Vector3 center, Vector2 fwd)
    {
        if (drillPS == null) return;

        Vector2 right = new Vector2(-fwd.y, fwd.x);
        var ep = new ParticleSystem.EmitParams();
        ep.velocity = Vector3.zero;
        ep.startSize = Mathf.Max(0.001f, drillStartSize);
        ep.startLifetime = Mathf.Max(0.01f, drillLifetime);

        for (int i = 0; i < drillArmCount; i++)
        {
            float a = (drillAngle + i * (360f / drillArmCount)) * Mathf.Deg2Rad;
            Vector2 offset = right * (Mathf.Cos(a) * drillRadius)
                           + fwd  * (Mathf.Sin(a) * drillRadius * 0.4f);
            ep.position = center + (Vector3)(fwd * drillForwardOffset) + (Vector3)offset;
            ep.startColor = GetRandomColor();
            drillPS.Emit(ep, 1);
        }
    }

    // =============================================
    // D: Shock Wave Cone
    // =============================================
    private void EmitShockwave(Vector3 center, Vector2 fwd)
    {
        if (shockwavePS == null) return;

        Vector2 right = new Vector2(-fwd.y, fwd.x);

        var ep = new ParticleSystem.EmitParams();
        ep.startSize = Mathf.Max(0.001f, shockwaveStartSize);
        ep.startLifetime = Mathf.Max(0.01f, shockwaveLifetime);
        float halfRad = shockwaveHalfAngleDeg * Mathf.Deg2Rad;

        for (int i = 0; i < shockwaveCount; i++)
        {
            float t = (shockwaveCount <= 1) ? 0f : (float)i / (shockwaveCount - 1);
            float angle = Mathf.Lerp(-halfRad, halfRad, t);
            Vector2 dir = (fwd * Mathf.Cos(angle) + right * Mathf.Sin(angle)).normalized;
            ep.position = center;
            ep.velocity = (Vector3)(dir * shockwaveSpeed);
            ep.startColor = GetRandomColor();
            shockwavePS.Emit(ep, 1);
        }
    }
}
