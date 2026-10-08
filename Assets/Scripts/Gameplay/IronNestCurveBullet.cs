using UnityEngine;

/// <summary>
/// Area3ボス「IronNest」のNM03（後半の3way Countdown）の左右の弾にだけ、発射時に付ける部品（IronNest専用）。
/// 始点 → 曲がりの頂点（制御点）→ 着弾点 の2次ベジェ曲線に沿って、大きく弧を描きながら着弾点へ向かう。
/// 弾の速さはそのまま使い、進む距離に合わせて曲線上の位置を進め、その方向へ弾の向きを毎フレーム合わせる。
/// ・着弾点に着いたら、最後の向きのまま直進する（Countdownの爆発は弾自身のタイマー）
/// ・反射されたら（プレイヤーの線で跳ね返されたら）曲がるのをやめて、そのまま直進する
/// ・プールで再利用される弾に残らないよう、弾が消える時に自分自身を外す
/// </summary>
[DisallowMultipleComponent]
public class IronNestCurveBullet : MonoBehaviour
{
    private EnemyBullet bullet;
    private Vector2 p0, p1, p2;
    private float curveLength;
    private float traveled;
    private Vector3 lastPos;
    private bool armed;

    /// <summary>曲線のおおよその長さ（速さの調整に使う）</summary>
    public static float ApproxLength(Vector2 a, Vector2 control, Vector2 b, int steps = 16)
    {
        float len = 0f;
        Vector2 prev = a;
        for (int i = 1; i <= steps; i++)
        {
            Vector2 p = Bezier(a, control, b, i / (float)steps);
            len += Vector2.Distance(prev, p);
            prev = p;
        }
        return len;
    }

    public void Arm(EnemyBullet b, Vector2 start, Vector2 control, Vector2 end)
    {
        bullet = b;
        p0 = start; p1 = control; p2 = end;
        curveLength = Mathf.Max(0.01f, ApproxLength(p0, p1, p2));
        traveled = 0f;
        lastPos = transform.position;
        armed = true;
        Vector2 d = Tangent(0f);
        if (d.sqrMagnitude > 0.0001f) bullet.SetDirection(d);
    }

    private void Update()
    {
        if (!armed || bullet == null) return;
        if (bullet.IsReflected || bullet.HasPaddleReflectedOnce)
        {
            armed = false; // 反射されたら直進
            return;
        }

        Vector3 pos = transform.position;
        traveled += ((Vector2)(pos - lastPos)).magnitude;
        lastPos = pos;

        float t = traveled / curveLength;
        if (t >= 1f)
        {
            armed = false; // 着弾点に着いた：最後の向きのまま直進
            return;
        }
        // 少し先の曲線上の点へ向ける（位置のずれも少しずつ吸収する）
        float ahead = Mathf.Min(1f, t + 0.05f);
        Vector2 target = Bezier(p0, p1, p2, ahead);
        Vector2 d = target - (Vector2)pos;
        if (d.sqrMagnitude > 0.0001f) bullet.SetDirection(d.normalized);
    }

    private Vector2 Tangent(float t)
    {
        return (2f * (1f - t) * (p1 - p0) + 2f * t * (p2 - p1)).normalized;
    }

    private static Vector2 Bezier(Vector2 a, Vector2 c, Vector2 b, float t)
    {
        float u = 1f - t;
        return u * u * a + 2f * u * t * c + t * t * b;
    }

    private void OnDisable()
    {
        armed = false;
        Destroy(this);
    }
}
