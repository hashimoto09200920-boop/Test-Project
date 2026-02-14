using UnityEngine;

public partial class EnemyBullet
{
    /// <summary>
    /// Player/Floor接触時の処理
    /// 未反射弾：消滅 / カウントダウン弾：即爆発
    /// </summary>
    private bool TryHandlePlayerOrFloorHit(Collider2D col)
    {
        if (col == null) return false;
        if (isBeingDestroyed) return false;

        // Player判定
        PixelDancerController player = col.GetComponent<PixelDancerController>();
        if (player == null) player = col.GetComponentInParent<PixelDancerController>();

        // Floor判定
        FloorHealth floor = col.GetComponent<FloorHealth>();
        if (floor == null) floor = col.GetComponentInParent<FloorHealth>();

        bool hitPlayerOrFloor = (player != null || floor != null);
        if (!hitPlayerOrFloor) return false;

        // カウントダウン弾の場合は即爆発
        if (useCountdownExplosion && !explosionTriggered)
        {
            TriggerExplosion();
            return true;
        }

        // 未反射弾の場合は消滅
        if (unreflectedDisappearOnPlayerFloorHit && !hasPaddleReflectedOnce)
        {
            if (feedback != null) feedback.OnUnreflectedDisappear(transform.position);

            isBeingDestroyed = true;
            Destroy(gameObject);
            return true;
        }

        return false;
    }

    private static ulong MakePairKey(EnemyBullet a, EnemyBullet b)
    {
        int ida = (a != null) ? a.GetInstanceID() : 0;
        int idb = (b != null) ? b.GetInstanceID() : 0;

        uint lo = (uint)Mathf.Min(ida, idb);
        uint hi = (uint)Mathf.Max(ida, idb);
        return ((ulong)lo << 32) | (ulong)hi;
    }

    private bool TryAcquirePairCooldown(EnemyBullet other, float cooldownSeconds)
    {
        if (other == null) return false;
        if (other == this) return false;

        float now = Time.unscaledTime;
        float cd = Mathf.Max(0f, cooldownSeconds);

        ulong key = MakePairKey(this, other);

        float next;
        if (s_pairNextAllowedTime.TryGetValue(key, out next))
        {
            if (now < next) return false;
        }

        s_pairNextAllowedTime[key] = now + cd;
        return true;
    }

    private bool IsWallCollider(Collider2D col)
    {
        if (col == null) return false;
        if (wallLayersToCount == 0) return false;

        Transform t = col.transform;
        for (int i = 0; i < 8 && t != null; i++)
        {
            int layer = t.gameObject.layer;
            if ((wallLayersToCount.value & (1 << layer)) != 0)
            {
                return true;
            }
            t = t.parent;
        }

        return false;
    }

    private bool IsEnemyCollider(Collider2D col)
    {
        if (col == null) return false;

        // EnemyDamageReceiverコンポーネントがあるかチェック
        EnemyDamageReceiver enemy = col.GetComponent<EnemyDamageReceiver>();
        if (enemy != null) return true;

        // 親オブジェクトもチェック
        Transform t = col.transform.parent;
        for (int i = 0; i < 4 && t != null; i++)
        {
            enemy = t.GetComponent<EnemyDamageReceiver>();
            if (enemy != null) return true;
            t = t.parent;
        }

        return false;
    }

    private EnemyBullet GetOtherBulletFromCollider(Collider2D col)
    {
        if (col == null) return null;

        EnemyBullet b = col.GetComponent<EnemyBullet>();
        if (b != null) return b;

        Transform t = col.transform.parent;
        for (int i = 0; i < 4 && t != null; i++)
        {
            b = t.GetComponent<EnemyBullet>();
            if (b != null) return b;
            t = t.parent;
        }

        return null;
    }

    private void ApplyWallAngleClampIfNeeded()
    {
        if (!useWallAngleClamp) return;
        if (rb == null) return;
        if (isBeingDestroyed) return;

        if (Time.frameCount == lastWallAngleClampFrame) return;
        lastWallAngleClampFrame = Time.frameCount;

        Vector2 v = rb.linearVelocity;
        float mag = v.magnitude;
        if (mag < 0.0001f) return;

        float aDeg = Mathf.Clamp(wallMinAngleDeg, 0f, 45f);
        if (aDeg <= 0.0001f) return;

        float minRatio = Mathf.Sin(aDeg * Mathf.Deg2Rad);
        float minComp = mag * minRatio;

        float ax = Mathf.Abs(v.x);
        float ay = Mathf.Abs(v.y);

        if (ax < minComp)
        {
            float sx = (v.x >= 0f) ? 1f : -1f;
            if (Mathf.Approximately(v.x, 0f)) sx = (Random.value < 0.5f) ? -1f : 1f;
            v.x = sx * minComp;
        }
        if (ay < minComp)
        {
            float sy = (v.y >= 0f) ? 1f : -1f;
            if (Mathf.Approximately(v.y, 0f)) sy = (Random.value < 0.5f) ? -1f : 1f;
            v.y = sy * minComp;
        }

        v = v.normalized * mag;
        float timeScale = GetTimeScale();
        rb.linearVelocity = v * timeScale;

        bool isWave = waveMotionEnabled;
        bool isSpiral = spiralMotionEnabled;

        if (!(isWave || isSpiral))
        {
            direction = v.normalized;
            if (direction.sqrMagnitude > 0.0001f) lastNonZeroDir = direction;
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision == null || collision.collider == null) return;

        // 未反射弾の敵への当たり判定が無効な間は、敵との衝突だけをスキップ
        if (unreflectedCollisionDisabled && !IsReflected && IsEnemyCollider(collision.collider))
        {
            return;
        }

        // Player/Floor接触時の処理
        if (TryHandlePlayerOrFloorHit(collision.collider)) return;

        // ★煙幕弾の衝突処理（反射後のみ）
        if (IsSmokeGrenadeActive && HasSmokeGrenadeReflected)
        {
            OnSmokeGrenadeCollision(transform.position);
        }

        if (IsWallCollider(collision.collider))
        {
            if (feedback != null) feedback.OnWallHit(transform.position, IsPoweredNow);

            ApplyWallAngleClampIfNeeded();
            RegisterWallBounce();
            return;
        }

        EnemyBullet other = GetOtherBulletFromCollider(collision.collider);
        if (other != null)
        {
            if (TryHandleBulletVsBulletDisappear(other)) return;
            RegisterBulletContactBounce(other);
        }
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        if (collision == null || collision.collider == null) return;

        // 未反射弾の敵への当たり判定が無効な間は、敵との衝突だけをスキップ
        if (unreflectedCollisionDisabled && !IsReflected && IsEnemyCollider(collision.collider))
        {
            return;
        }

        if (IsWallCollider(collision.collider))
        {
            if (feedback != null) feedback.OnWallHit(transform.position, IsPoweredNow);

            ApplyWallAngleClampIfNeeded();
            RegisterWallBounce();
            return;
        }

        EnemyBullet other = GetOtherBulletFromCollider(collision.collider);
        if (other != null)
        {
            if (TryHandleBulletVsBulletDisappear(other)) return;
            RegisterBulletContactBounce(other);
        }
    }

    private void OnTriggerEnter2D(Collider2D otherCol)
    {
        if (otherCol == null) return;

        // 未反射弾の敵への当たり判定が無効な間は、敵との衝突だけをスキップ
        if (unreflectedCollisionDisabled && !IsReflected && IsEnemyCollider(otherCol))
        {
            return;
        }

        // Player/Floor接触時の処理
        if (TryHandlePlayerOrFloorHit(otherCol)) return;

        if (IsWallCollider(otherCol))
        {
            if (feedback != null) feedback.OnWallHit(transform.position, IsPoweredNow);

            ApplyWallAngleClampIfNeeded();
            RegisterWallBounce();
            return;
        }

        EnemyBullet other = GetOtherBulletFromCollider(otherCol);
        if (other != null)
        {
            if (TryHandleBulletVsBulletDisappear(other)) return;
            RegisterBulletContactBounce(other);
        }
    }

    private void OnTriggerStay2D(Collider2D otherCol)
    {
        if (otherCol == null) return;

        // 未反射弾の敵への当たり判定が無効な間は、敵との衝突だけをスキップ
        if (unreflectedCollisionDisabled && !IsReflected && IsEnemyCollider(otherCol))
        {
            return;
        }

        if (IsWallCollider(otherCol))
        {
            if (feedback != null) feedback.OnWallHit(transform.position, IsPoweredNow);

            ApplyWallAngleClampIfNeeded();
            RegisterWallBounce();
            return;
        }

        EnemyBullet other = GetOtherBulletFromCollider(otherCol);
        if (other != null)
        {
            if (TryHandleBulletVsBulletDisappear(other)) return;
            RegisterBulletContactBounce(other);
        }
    }
}
