using UnityEngine;

/// <summary>
/// 「敵の線」の当たり判定（Area10最終ボスNeonDancerの後半で使用）。
/// - 未反射の弾：素通り（Collider2DをPhantomBlockレイヤーに置くことで、Physics2Dの衝突設定によりUnreflectedBulletとは衝突しない）
/// - プレイヤーが反射させた弾：物理的に跳ね返った後、EnemyBullet.RevertToUnreflected()で未反射の弾に戻す
///   （再びプレイヤー・Floorに当たり、プレイヤーの線でまた反射できる）
/// - ビーム：EnemyBeamBulletがこのコンポーネントを見て、未反射のビームは素通り、反射済みのビームは反射して未反射に戻す
/// ★ボスの階層の外に置くこと（中に置くとビーム・爆発がボス本体の一部として扱う）
/// </summary>
[DisallowMultipleComponent]
public class EnemyLineReflector : MonoBehaviour
{
    /// <summary>実体化している間だけtrue（予告中・消えた後はfalse。falseの間は何もしない）</summary>
    public bool IsSolid { get; set; }

    /// <summary>反射済みの弾を跳ね返して未反射に戻した時に発火（見た目の変更などに使う）</summary>
    public event System.Action<EnemyBullet> OnBulletReverted;

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!IsSolid || collision == null || collision.collider == null) return;
        EnemyBullet bullet = collision.collider.GetComponentInParent<EnemyBullet>();
        if (bullet == null || !bullet.IsReflected) return;

        // 衝突コールバックの時点で物理は跳ね返り済み。その向きで未反射に戻す
        Vector2 v = collision.rigidbody != null ? collision.rigidbody.linearVelocity : Vector2.zero;
        Vector2 dir;
        if (v.sqrMagnitude > 0.0001f) dir = v.normalized;
        else
        {
            Vector2 n = collision.contactCount > 0 ? collision.GetContact(0).normal : Vector2.down;
            dir = -n;
        }
        bullet.RevertToUnreflected(dir);
        OnBulletReverted?.Invoke(bullet);
    }
}
