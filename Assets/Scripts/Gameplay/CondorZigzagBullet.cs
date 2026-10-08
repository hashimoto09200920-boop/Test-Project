using UnityEngine;

/// <summary>
/// Area2ボス「Condor」の「雷の羽ばたき」の弾にだけ、発射時に付ける部品（Condor専用）。
/// 稲妻のように、一定の距離ごとに左右へ鋭く折れ曲がりながら進む（基準の向きを中心に±角度で交互に曲がる）。
/// ・反射されたら（プレイヤーの線で跳ね返されたら）ジグザグをやめて、そのまま直進する
/// ・壁などで向きが変わった場合は、変わった後の向きを新しい基準にしてジグザグを続ける
/// ・プールで再利用される弾に残らないよう、弾が消える時に自分自身を外す
/// </summary>
[DisallowMultipleComponent]
public class CondorZigzagBullet : MonoBehaviour
{
    private EnemyBullet bullet;
    private Rigidbody2D rb;
    private Vector2 baseDir;
    private float angleDeg;
    private float segmentLength;
    private int sign;
    private float traveled;
    private Vector3 lastPos;
    private Vector2 expectedDir;
    private bool armed;

    // 向きが外から変えられた（壁で跳ね返った等）とみなす角度差
    private const float ExternalTurnThresholdDeg = 8f;

    /// <param name="angle">基準の向きから左右に曲がる角度（度）</param>
    /// <param name="segment">1回曲がるまでに進む距離（ワールド単位）</param>
    public void Arm(EnemyBullet b, Vector2 dir, float angle, float segment)
    {
        bullet = b;
        rb = b.GetComponent<Rigidbody2D>();
        baseDir = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector2.down;
        angleDeg = angle;
        segmentLength = Mathf.Max(0.05f, segment);
        sign = Random.value < 0.5f ? 1 : -1;
        traveled = segmentLength * 0.5f; // 最初は半分の距離で曲がる（基準の線を中心に左右へ振れる）
        lastPos = transform.position;
        armed = true;
        ApplyDir();
    }

    private void ApplyDir()
    {
        expectedDir = Rotate(baseDir, sign * angleDeg);
        bullet.SetDirection(expectedDir);
    }

    private void Update()
    {
        if (!armed || bullet == null) return;

        // 反射されたらジグザグをやめて直進（ユーザー指定）
        if (bullet.IsReflected || bullet.HasPaddleReflectedOnce)
        {
            armed = false;
            return;
        }

        // 壁などで向きが変わっていたら、その向きを新しい基準にする（曲がり分を戻して基準を求める）
        if (rb != null && rb.linearVelocity.sqrMagnitude > 0.0001f)
        {
            Vector2 cur = rb.linearVelocity.normalized;
            if (Vector2.Angle(cur, expectedDir) > ExternalTurnThresholdDeg)
            {
                baseDir = Rotate(cur, -sign * angleDeg);
                expectedDir = cur;
            }
        }

        Vector3 p = transform.position;
        traveled += ((Vector2)(p - lastPos)).magnitude;
        lastPos = p;
        if (traveled >= segmentLength)
        {
            traveled -= segmentLength;
            sign = -sign;
            ApplyDir();
        }
    }

    private void OnDisable()
    {
        armed = false;
        Destroy(this);
    }

    private static Vector2 Rotate(Vector2 v, float deg)
    {
        return (Vector2)(Quaternion.Euler(0f, 0f, deg) * v);
    }
}
