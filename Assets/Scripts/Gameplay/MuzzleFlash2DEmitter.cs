using UnityEngine;

/// <summary>
/// 2D専用マズルフラッシュエミッター。
/// ParticleSystem ShapeモジュールはZ方向にも粒子を放射するため使用しない。
/// EnemyShooterが Emit(fireDir) を呼び出すと、XY平面上の指定角度範囲に粒子を放射する。
/// </summary>
public class MuzzleFlash2DEmitter : MonoBehaviour
{
    [HideInInspector] public int   sparkCount   = 7;
    [HideInInspector] public float halfAngleDeg = 70f;
    [HideInInspector] public float speedMin     = 2f;
    [HideInInspector] public float speedMax     = 5f;
    [HideInInspector] public float lifetimeMin  = 0.5f;
    [HideInInspector] public float lifetimeMax  = 0.8f;
    [HideInInspector] public float sizeMin      = 0.06f;
    [HideInInspector] public float sizeMax      = 0.10f;

    private ParticleSystem sparksPS;

    private void Awake()
    {
        var t = transform.Find("Sparks");
        if (t != null) sparksPS = t.GetComponent<ParticleSystem>();
    }

    public void Emit(Vector2 fireDir)
    {
        if (sparksPS == null || fireDir.sqrMagnitude < 0.0001f) return;

        fireDir = fireDir.normalized;
        var p = new ParticleSystem.EmitParams();

        for (int i = 0; i < sparkCount; i++)
        {
            float rad = Random.Range(-halfAngleDeg, halfAngleDeg) * Mathf.Deg2Rad;
            float cos = Mathf.Cos(rad);
            float sin = Mathf.Sin(rad);

            // 2D回転：fireDirをrad度回転
            Vector2 dir = new Vector2(
                fireDir.x * cos - fireDir.y * sin,
                fireDir.x * sin + fireDir.y * cos
            );

            float speed       = Random.Range(speedMin, speedMax);
            p.velocity        = new Vector3(dir.x, dir.y, 0f) * speed;
            p.startSize       = Random.Range(sizeMin, sizeMax);
            p.startLifetime   = Random.Range(lifetimeMin, lifetimeMax);
            p.startColor      = Color.white;
            p.position        = transform.position;

            sparksPS.Emit(p, 1);
        }
    }
}
