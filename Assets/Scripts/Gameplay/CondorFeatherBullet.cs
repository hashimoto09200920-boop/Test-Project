using UnityEngine;

/// <summary>
/// Area2ボス「Condor」の「羽根の一斉射撃」の弾にだけ、発射時に付ける部品（Condor専用）。
/// 弾の見た目（Visual）を進行方向へ向ける（羽根の画像は先端が真上向き＝進行方向から-90°）。
/// 反射された後も、進行方向（跳ね返った向き）へ向き続ける。
/// ★弾はプールで他のエネミーにも再利用されるため、弾が消える時に見た目の向きを必ず元に戻し、自分自身を外す。
/// </summary>
[DisallowMultipleComponent]
public class CondorFeatherBullet : MonoBehaviour
{
    private Transform visual;
    private Rigidbody2D rb;
    private Quaternion originalLocalRotation;
    private bool armed;

    public void Arm(EnemyBullet b, Vector2 initialDir)
    {
        visual = b.transform.Find("Visual");
        rb = b.GetComponent<Rigidbody2D>();
        if (visual == null) return;
        originalLocalRotation = visual.localRotation;
        armed = true;
        Face(initialDir);
    }

    private void LateUpdate()
    {
        if (!armed || visual == null || rb == null) return;
        Vector2 v = rb.linearVelocity;
        if (v.sqrMagnitude > 0.0001f) Face(v); // 止まっている間（一時停止等）は向きを保つ
    }

    private void Face(Vector2 dir)
    {
        if (dir.sqrMagnitude < 0.0001f) return;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
        visual.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    private void OnDisable()
    {
        if (armed && visual != null) visual.localRotation = originalLocalRotation;
        armed = false;
        Destroy(this);
    }
}
