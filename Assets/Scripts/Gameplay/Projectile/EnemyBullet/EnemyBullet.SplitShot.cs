using UnityEngine;

/// <summary>
/// アイテム「スプリットショット」（2026/11/11）：跳ね返した弾を、同じ見た目・速さ・ダメージの弾に分ける。
/// この弾（親）の向きも変えて、全部で count 発を扇形に並べる（ShotItemManager が呼ぶ）。
/// 分かれた弾は、跳ね返した弾として扱う（敵に当たる・跳ね返り回数の残りも親と同じ）。もう一度分かれることはない
/// </summary>
public partial class EnemyBullet
{
    /// <summary>
    /// この弾を count 発に分ける（この弾も含む）。stepDeg＝隣どうしの角度。damageMul＝分かれた全部の弾のダメージ倍率。
    /// 新しく出した弾を返す（この弾は含まない）。プールの元の弾が分からない弾は分けない（空を返す）
    /// </summary>
    public System.Collections.Generic.List<EnemyBullet> SplitInto(int count, float stepDeg, float damageMul)
    {
        var made = new System.Collections.Generic.List<EnemyBullet>();
        if (count <= 1 || sourcePrefab == null || isBeingDestroyed || !gameObject.activeInHierarchy) return made;

        Vector2 baseDir = rb != null && rb.linearVelocity.sqrMagnitude > 0.0001f ? rb.linearVelocity.normalized
                        : (lastNonZeroDir.sqrMagnitude > 0.0001f ? lastNonZeroDir.normalized : Vector2.up);

        SpriteRenderer myVisual = visualRenderer;
        if (myVisual == null) { Transform v = transform.Find("Visual"); if (v != null) myVisual = v.GetComponent<SpriteRenderer>(); }

        for (int i = 0; i < count; i++)
        {
            float ang = (i - (count - 1) * 0.5f) * stepDeg;
            Vector2 dir = Rotate(baseDir, ang);
            if (i == 0)
            {
                // 1発目はこの弾自身の向きを変える
                SetDirection(dir);
                continue;
            }
            EnemyBullet c = EnemyBulletPool.Get(sourcePrefab, transform.position, transform.rotation, transform.parent);
            if (c == null) continue;
            c.transform.localScale = transform.localScale;
            if (myVisual != null)
            {
                c.SetSpriteOverride(myVisual.sprite);
                // ★見た目の大きさ・色は写さない（親は反射の演出で一時的に大きく・色付きになっていることがあり、写すと子弾が大きいまま・色付きのまま残る）
            }

            c.ApplyBullet(speed, lifeTime);
            c.SetDamage(damageValue);
            c.usePaddleBounceLimit = usePaddleBounceLimit;
            c.paddleBounceLimit = paddleBounceLimit;
            c.remainingPaddleBounces = remainingPaddleBounces;
            c.bounceLimitBoosted = bounceLimitBoosted;
            c.boltSpeedMul = boltSpeedMul;
            c.accelMultiplierNow = accelMultiplierNow;
            c.DamageMultiplier = DamageMultiplier;

            var myPen = GetComponent<BulletPenetration>();
            var cPen = c.GetComponent<BulletPenetration>();
            if (myPen != null && cPen != null) cPen.SetPenetration(myPen.Penetration);

            c.ShotKind = ShotItemType.Split;
            c.MarkReflected();
            c.hasPaddleReflectedOnce = true;
            c.SetDirection(dir);
            c.RefreshBaseSpeedAndTargetSpeed();
            c.ApplyVelocity();
            made.Add(c);
            // 跳ね返した弾の演出に登録する（音・ヒットの演出は出さない）。ダメージ倍率を掛ける前に登録する
            ReflectedBulletFXManager.RegisterSplitChild(c, DamageMultiplier > 1.0001f);
        }

        if (damageMul > 1.0001f)
        {
            DamageMultiplier *= damageMul;
            foreach (var c in made) c.DamageMultiplier *= damageMul;
        }
        return made;
    }

    private static Vector2 Rotate(Vector2 v, float deg)
    {
        float r = deg * Mathf.Deg2Rad, cs = Mathf.Cos(r), sn = Mathf.Sin(r);
        return new Vector2(v.x * cs - v.y * sn, v.x * sn + v.y * cs);
    }

    /// <summary>この弾の当たり判定（分かれた弾どうしが重なって当たらないようにするため）</summary>
    public Collider2D BulletCollider => bulletCol;
}
