using UnityEngine;

/// <summary>
/// NeonDancerが撃った⑤煙幕弾にだけ発射時に付ける部品（NeonDancer専用仕様）。
/// 反射していない⑤がプレイヤー側のDancer（PixelDancerController）/床（FloorHealth）に当たった時にも煙幕を出す。
///
/// ★共有のEnemyBullet.TryHandlePlayerOrFloorHitは、反射前の弾がプレイヤー/床に当たると弾を消すだけで煙を出さない。
///   共有コードは改修せず、弾が消える瞬間（OnDisable＝プール返却時に必ず呼ばれる）に
///   「未反射」かつ「プレイヤー/床と重なっている」なら、その位置に煙幕を出す。
///   （同じ衝突処理の中で割り込むと、弾側が先に非アクティブ化した場合に呼ばれない恐れがあるため）
/// プール再利用される弾に残らないよう、消える時に自分自身を外す。
/// </summary>
[DisallowMultipleComponent]
public class NeonDancerSmokeBullet : MonoBehaviour
{
    private EnemyBullet bullet;
    private Collider2D bulletCol;
    private bool armed;

    private static readonly System.Collections.Generic.List<Collider2D> s_hits = new System.Collections.Generic.List<Collider2D>(8);

    public void Arm(EnemyBullet b)
    {
        bullet = b;
        bulletCol = b != null ? b.GetComponent<Collider2D>() : null;
        armed = true;
    }

    private void OnDisable()
    {
        if (armed) TrySpawnSmokeOnPlayerHit();
        armed = false;
        Destroy(this);
    }

    private void TrySpawnSmokeOnPlayerHit()
    {
        if (bullet == null) return;
        if (!gameObject.scene.isLoaded) return;          // シーン終了時の破棄では出さない
        if (bullet.IsReflected) return;                  // 反射済み（通常反射は反射時に煙が出る／Just反射は出さない）
        if (!bullet.IsSmokeGrenadeActive) return;

        Vector2 pos = transform.position;
        float bulletRadius = 0.1f;
        if (bulletCol is CircleCollider2D cc) bulletRadius = cc.radius * Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.y));

        // ★弾は床/プレイヤーの表面に「触れた」瞬間に消えるため、中心は表面から弾の半径ぶん離れている。
        //   速い弾は表面から少し手前で接触判定されることもあるので、重なり判定ではなく
        //   「表面までの距離（ClosestPoint）−弾の半径」が許容値以内かで判定する
        const float hitTolerance = 0.25f;
        const float searchRadius = 1.0f;

        s_hits.Clear();
        int n = Physics2D.OverlapCircle(pos, searchRadius, new ContactFilter2D().NoFilter(), s_hits);
        Collider2D nearest = null;
        float nearestGap = float.MaxValue;
        for (int i = 0; i < n && i < s_hits.Count; i++)
        {
            Collider2D c = s_hits[i];
            if (c == null || !c.enabled) continue;
            // 共有のTryHandlePlayerOrFloorHitと同じ判定（GetComponentInParentは使わない）
            if (c.GetComponent<PixelDancerController>() == null && c.GetComponent<FloorHealth>() == null) continue;

            float gap = Vector2.Distance(c.ClosestPoint(pos), pos) - bulletRadius; // 0以下＝重なっている
            if (gap < nearestGap) { nearestGap = gap; nearest = c; }
        }

        if (nearest == null) return; // 近くに床/プレイヤーが無い（寿命切れ等で消えた）

        if (nearestGap <= hitTolerance)
        {
            // 反射時と同じ処理で煙幕を出す（煙幕プレハブ・煙が出る瞬間のSEはEnemyDataの⑤の設定）
            bullet.OnSmokeGrenadeReflected(pos);
#if UNITY_EDITOR
            // ★負荷軽減：確認用ログはEditorだけで出す（実機ビルドでは文字列生成・スタックトレース記録の負荷を出さない）
            Debug.Log($"[NeonDancerSmoke] 未反射の⑤が{nearest.name}に当たったため煙幕を生成 pos=({pos.x:F2},{pos.y:F2}) 表面までの距離={nearestGap:F3}");
#endif
        }
        else
        {
#if UNITY_EDITOR
            Debug.Log($"[NeonDancerSmoke] 未反射の⑤が{nearest.name}の近くで消えたが距離が許容値外のため煙幕なし pos=({pos.x:F2},{pos.y:F2}) 表面までの距離={nearestGap:F3}");
#endif
        }
    }
}
