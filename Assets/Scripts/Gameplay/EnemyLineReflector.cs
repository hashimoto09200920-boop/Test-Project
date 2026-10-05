using UnityEngine;

/// <summary>
/// 「敵の線」の当たり判定（Area10最終ボスNeonDancerの後半で使用）。
/// - 未反射の弾：素通り（Collider2DをPhantomBlockレイヤーに置くことで、Physics2Dの衝突設定によりUnreflectedBulletとは衝突しない）
/// - プレイヤーが反射させた弾：物理的に跳ね返った後、EnemyBullet.RevertToUnreflected()で未反射の弾に戻す
///   （再びプレイヤー・Floorに当たり、プレイヤーの線でまた反射できる）
/// - 当たった反射弾の貫通力（Penetration）を蓄積し、Break Penetration Threshold以上になったら線が壊れる
///   （壊した弾は跳ね返らずに反射済みのまま直進する。プレイヤーの線を弾が貫通した時と同じ考え方）
/// - ビーム：EnemyBeamBulletがこのコンポーネントを見て、未反射のビームは素通り、反射済みのビームは反射して未反射に戻す
/// ★ボスの階層の外に置くこと（中に置くとビーム・爆発がボス本体の一部として扱う）
/// </summary>
[DisallowMultipleComponent]
public class EnemyLineReflector : MonoBehaviour
{
    /// <summary>実体化している間だけtrue（予告中・消えた後・壊れた後はfalse。falseの間は何もしない）</summary>
    public bool IsSolid { get; set; }

    /// <summary>この値以上の貫通力が蓄積したら線が壊れる（0以下なら壊れない）</summary>
    public int BreakPenetrationThreshold { get; set; }
    public int AccumulatedPenetration { get; private set; }

    /// <summary>反射済みの弾を跳ね返して未反射に戻した時（弾・当たった位置・跳ね返った向き）</summary>
    public event System.Action<EnemyBullet, Vector3, Vector2> OnBulletReverted;
    /// <summary>反射済みのビームを跳ね返した時（当たった位置・跳ね返った向き）。EnemyBeamBulletから呼ばれる</summary>
    public event System.Action<Vector3, Vector2> OnBeamReflected;
    /// <summary>貫通力の蓄積で線が壊れた時（壊れた位置）</summary>
    public event System.Action<Vector3> OnBroken;

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!IsSolid || collision == null || collision.collider == null) return;
        EnemyBullet bullet = collision.collider.GetComponentInParent<EnemyBullet>();
        if (bullet == null || !bullet.IsReflected) return;

        Vector3 hitPos = collision.contactCount > 0 ? (Vector3)collision.GetContact(0).point : bullet.transform.position;
        Vector2 normal = collision.contactCount > 0 ? collision.GetContact(0).normal : Vector2.down;

        // 衝突コールバックの時点で物理は跳ね返り済み
        Vector2 v = collision.rigidbody != null ? collision.rigidbody.linearVelocity : Vector2.zero;
        Vector2 bounced = v.sqrMagnitude > 0.0001f ? v.normalized : -normal;

        // 貫通力の蓄積 → 壊れる
        int pen = bullet.CachedPenetration != null ? Mathf.Max(0, bullet.CachedPenetration.Penetration) : 1;
        AccumulatedPenetration += pen;
        if (BreakPenetrationThreshold > 0 && AccumulatedPenetration >= BreakPenetrationThreshold)
        {
            IsSolid = false;
            // 壊した弾は跳ね返らず、反射済みのまま元の向きへ直進する（跳ね返り前の向き＝跳ね返った向きを法線で折り返す）
            Vector2 straight = Vector2.Reflect(bounced, normal).normalized;
            bullet.SetDirection(straight);
            OnBroken?.Invoke(hitPos);
            return;
        }

        bullet.RevertToUnreflected(bounced);
        OnBulletReverted?.Invoke(bullet, hitPos, bounced);
    }

    /// <summary>EnemyBeamBulletが反射済みのビームをこの線で跳ね返した時に呼ぶ（反射のSE・エフェクト用）</summary>
    public void NotifyBeamReflected(Vector3 point, Vector2 reflectDir)
    {
        OnBeamReflected?.Invoke(point, reflectDir);
    }
}
