using UnityEngine;

public partial class EnemyBullet
{
    private void ResetPaddleBounceRemaining()
    {
        if (!usePaddleBounceLimit || paddleBounceLimit <= 0)
        {
            remainingPaddleBounces = -1;
            Debug.Log($"[弾生成] Bounce Limit無効 (remaining=-1)");
        }
        else
        {
            remainingPaddleBounces = paddleBounceLimit;
            Debug.Log($"[弾生成] Bounce Limit={paddleBounceLimit}, remaining={remainingPaddleBounces}");
        }

        lastPaddleBounceFrame = -999;
        lastEnemyHitCountFrame = -999;
        lastWallBounceFrame = -999;
        lastBulletContactFrame = -999;
        lastBulletContactOtherId = 0;
        lastWallAngleClampFrame = -999;
    }

    public void ConfigurePaddleBounceLimit(int limit)
    {
        paddleBounceLimit = limit;

        // BulletTypeから呼ばれた時、自動的に有効/無効を設定
        usePaddleBounceLimit = (limit > 0);

        ResetPaddleBounceRemaining();
    }

    public void RegisterPaddleBounce(PaddleDot.LineType lineType)
    {
        if (isBeingDestroyed) return;

        ClearArcTurn();
        ClearTurnMotion();

        float sec = Mathf.Max(0f, reflectOverrideSeconds);
        if (sec > 0f)
        {
            reflectOverrideUntil = Time.unscaledTime + sec;
        }
        else
        {
            reflectOverrideUntil = -999f;
        }

        hasPaddleReflectedOnce = true;

        // VFX/SE 分離（PaddleHit VFX）
        if (feedback != null) feedback.OnPaddleReflect(transform.position);

        if (!usePaddleBounceLimit) return;
        if (paddleBounceLimit <= 0) return;

        if (Time.frameCount == lastPaddleBounceFrame) return;
        lastPaddleBounceFrame = Time.frameCount;

        if (remainingPaddleBounces > 0)
        {
            remainingPaddleBounces--;
            Debug.Log($"[Paddle反射] 残り: {remainingPaddleBounces}/{paddleBounceLimit}");

            if (remainingPaddleBounces <= 0)
            {
                Debug.Log($"[Paddle反射] 弾を破棄! remaining={remainingPaddleBounces}");
                if (feedback != null)
                {
                    feedback.PlayDisappearVfx(transform.position);
                    feedback.PlayDestroySeOnce(transform.position);
                }
                isBeingDestroyed = true;
                Destroy(gameObject);
            }
        }
    }

    public void RegisterEnemyHitAsBounce()
    {
        if (isBeingDestroyed) return;
        // ★修正: パドル反射前でも敵ヒットをカウント
        // if (!hasPaddleReflectedOnce) return;

        // VFX/SE 分離（EnemyHit VFX / JustPowered VFX）
        if (feedback != null) feedback.OnEnemyHit(transform.position, IsPoweredNow);

        if (!usePaddleBounceLimit) return;
        if (paddleBounceLimit <= 0) return;

        if (Time.frameCount == lastEnemyHitCountFrame) return;
        lastEnemyHitCountFrame = Time.frameCount;

        if (remainingPaddleBounces > 0)
        {
            remainingPaddleBounces--;
            Debug.Log($"[敵ヒット] 残り: {remainingPaddleBounces}/{paddleBounceLimit}");

            if (remainingPaddleBounces <= 0)
            {
                Debug.Log($"[敵ヒット] 弾を破棄! remaining={remainingPaddleBounces}");
                if (feedback != null)
                {
                    feedback.PlayDisappearVfx(transform.position);
                    feedback.PlayDestroySeOnce(transform.position);
                }
                isBeingDestroyed = true;
                Destroy(gameObject);
            }
        }
    }

    private void RegisterWallBounce()
    {
        if (isBeingDestroyed) return;
        // ★修正: パドル反射前でも壁反射をカウント
        // if (!hasPaddleReflectedOnce) return;

        if (!usePaddleBounceLimit) return;
        if (paddleBounceLimit <= 0) return;

        if (Time.frameCount == lastWallBounceFrame) return;
        lastWallBounceFrame = Time.frameCount;

        remainingPaddleBounces--;
        Debug.Log($"[壁反射] 残り: {remainingPaddleBounces}/{paddleBounceLimit}");

        if (remainingPaddleBounces <= 0)
        {
            Debug.Log($"[壁反射] 弾を破棄! remaining={remainingPaddleBounces}");
            if (feedback != null)
            {
                feedback.PlayDisappearVfx(transform.position);
                feedback.PlayDestroySeOnce(transform.position);
            }
            isBeingDestroyed = true;
            Destroy(gameObject);
        }
    }

    private void RegisterBulletContactBounce(EnemyBullet other)
    {
        if (isBeingDestroyed) return;
        if (!hasPaddleReflectedOnce) return;
        if (other == null) return;
        if (other == this) return;

        if (!usePaddleBounceLimit) return;
        if (paddleBounceLimit <= 0) return;
        if (remainingPaddleBounces <= 0) return;

        int otherId = other.GetInstanceID();
        if (Time.frameCount == lastBulletContactFrame && otherId == lastBulletContactOtherId) return;

        lastBulletContactFrame = Time.frameCount;
        lastBulletContactOtherId = otherId;

        remainingPaddleBounces--;
        Debug.Log($"[弾同士接触] 残り: {remainingPaddleBounces}/{paddleBounceLimit}");

        if (remainingPaddleBounces <= 0)
        {
            Debug.Log($"[弾同士接触] 弾を破棄! remaining={remainingPaddleBounces}");
            if (feedback != null)
            {
                feedback.PlayDisappearVfx(transform.position);
                feedback.PlayDestroySeOnce(transform.position);
            }
            isBeingDestroyed = true;
            Destroy(gameObject);
        }
    }

    private bool TryHandleBulletVsBulletDisappear(EnemyBullet other)
    {
        if (isBeingDestroyed) return true;
        if (other == null) return false;
        if (other == this) return false;

        bool aRef = (hasPaddleReflectedOnce || IsReflected);
        bool bRef = (other.hasPaddleReflectedOnce || other.IsReflected);

        if (!aRef && !bRef) return false;

        if (!TryAcquirePairCooldown(other, bulletPairCooldownSeconds))
        {
            return true;
        }

        bool aJust = (DamageMultiplier > 1.0001f);
        bool bJust = (other.DamageMultiplier > 1.0001f);

        if (aRef && bRef)
        {
            DestroyByBulletContact();
            other.DestroyByBulletContact();
            return true;
        }

        if (aRef && !bRef)
        {
            if (aJust) other.DestroyByBulletContact();
            else
            {
                DestroyByBulletContact();
                other.DestroyByBulletContact();
            }
            return true;
        }

        if (!aRef && bRef)
        {
            if (bJust) DestroyByBulletContact();
            else
            {
                DestroyByBulletContact();
                other.DestroyByBulletContact();
            }
            return true;
        }

        return false;
    }

    private void DestroyByBulletContact()
    {
        if (isBeingDestroyed) return;

        if (feedback != null)
        {
            feedback.PlayDisappearVfx(transform.position);
            feedback.PlayDestroySeOnce(transform.position);
        }
        isBeingDestroyed = true;
        Destroy(gameObject);
    }
}
