using UnityEngine;

public partial class EnemyBullet
{
    private void ResetPaddleBounceRemaining()
    {
        if (!usePaddleBounceLimit || paddleBounceLimit <= 0)
        {
            remainingPaddleBounces = -1;
        }
        else
        {
            remainingPaddleBounces = paddleBounceLimit;
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

            if (remainingPaddleBounces <= 0)
            {
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
        if (!hasPaddleReflectedOnce) return;

        // VFX/SE 分離（EnemyHit VFX / JustPowered VFX）
        if (feedback != null) feedback.OnEnemyHit(transform.position, IsPoweredNow);

        if (!usePaddleBounceLimit) return;
        if (paddleBounceLimit <= 0) return;

        if (Time.frameCount == lastEnemyHitCountFrame) return;
        lastEnemyHitCountFrame = Time.frameCount;

        if (remainingPaddleBounces > 0)
        {
            remainingPaddleBounces--;

            if (remainingPaddleBounces <= 0)
            {
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
        if (!hasPaddleReflectedOnce) return;

        if (!usePaddleBounceLimit) return;
        if (paddleBounceLimit <= 0) return;
        if (remainingPaddleBounces <= 0) return;

        if (Time.frameCount == lastWallBounceFrame) return;
        lastWallBounceFrame = Time.frameCount;

        remainingPaddleBounces--;

        if (remainingPaddleBounces <= 0)
        {
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

        if (remainingPaddleBounces <= 0)
        {
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
