using System.Collections;
using UnityEngine;
using Game.Skills;

public partial class EnemyBullet
{
    public void ApplySpeedCurve(float initialSpeed, float maxSpeed, float durationSeconds, AnimationCurve curve)
    {
        useSpeedCurve = true;

        curveInitialSpeed = Mathf.Max(0.01f, initialSpeed);
        curveMaxSpeed = Mathf.Max(0.01f, maxSpeed);

        curveDurationSeconds = durationSeconds;
        speedCurve = (curve != null) ? curve : AnimationCurve.Linear(0f, 0f, 1f, 1f);

        accelCapBaseSpeed = curveMaxSpeed;

        curveStartTime = Time.time;

        RefreshBaseSpeedAndTargetSpeed();
    }

    public void ClearSpeedCurve()
    {
        useSpeedCurve = false;
        curveStartTime = Time.time;

        accelCapBaseSpeed = speed;

        RefreshBaseSpeedAndTargetSpeed();
    }

    private void RefreshBaseSpeedAndTargetSpeed()
    {
        baseSpeedNow = GetBaseSpeedNow();

        float nextTarget = baseSpeedNow * Mathf.Max(0.01f, accelMultiplierNow);
        TargetSpeed = Mathf.Max(0.01f, nextTarget);
    }

    private float GetBaseSpeedNow()
    {
        if (!useSpeedCurve)
        {
            return Mathf.Max(0.01f, speed);
        }

        float init = Mathf.Max(0.01f, curveInitialSpeed);
        float max = Mathf.Max(0.01f, curveMaxSpeed);

        float dur = curveDurationSeconds;
        if (dur <= 0.0001f)
        {
            return max;
        }

        float t = (Time.time - curveStartTime) / dur;
        t = Mathf.Clamp01(t);

        float k = (speedCurve != null) ? speedCurve.Evaluate(t) : t;
        k = Mathf.Clamp01(k);

        return Mathf.Lerp(init, max, k);
    }

    public void BumpSpeedCurveStartToMinSpeed(float minSpeed)
    {
        if (!useSpeedCurve) return;
        if (curveInitialSpeed >= minSpeed) return;
        float range = curveMaxSpeed - curveInitialSpeed;
        if (range <= 0.0001f) return;
        float t = Mathf.Clamp01((minSpeed - curveInitialSpeed) / range);
        if (curveDurationSeconds > 0)
            curveStartTime = Time.time - t * curveDurationSeconds;
        RefreshBaseSpeedAndTargetSpeed();
        ApplyVelocity();
    }

    public void ApplyAcceleration(float multiplier, int maxCount)
    {
        float now = Time.time;
        if (now - lastAccelTime < accelCooldown) return;

        lastAccelTime = now;

        accelCount = Mathf.Min(accelCount + 1, Mathf.Max(0, maxCount));
        AccelMaxCountLast = Mathf.Max(0, maxCount);

        float m = Mathf.Max(0.01f, multiplier);
        accelMultiplierNow = Mathf.Max(0.01f, accelMultiplierNow * m);

        float capBase = Mathf.Max(0.01f, accelCapBaseSpeed);
        float cap = capBase * Mathf.Max(1, maxCount);

        RefreshBaseSpeedAndTargetSpeed();
        if (TargetSpeed > cap) TargetSpeed = cap;

        if (accelLerpSeconds <= 0f) ApplyVelocity();
    }

    public void ApplyJustReflect(float damageMultiplier, PaddleDot.LineType lineType)
    {
        DamageMultiplier = Mathf.Max(DamageMultiplier, Mathf.Max(1.0f, damageMultiplier));

        // C2: ジャスト弾になった時点でこの弾の貫通残数を設定（弾ごとに独立）
        if (SkillManager.Instance != null)
            c2PenetrationsRemaining = SkillManager.Instance.GetJustPenetrationCount(); // 0/1/-1

        ApplyVisualByState();

        if (feedback != null) feedback.OnJustReflect(lineType);

        if (flashOnJust && sr != null)
        {
            if (flashCo != null)
            {
                StopCoroutine(flashCo);
                flashCo = null;
            }
            flashCo = StartCoroutine(FlashRoutine());
        }
    }

    private IEnumerator FlashRoutine()
    {
        if (sr == null) yield break;

        Color prev = sr.color;
        sr.color = flashColor;

        yield return new WaitForSeconds(flashSeconds);

        ApplyVisualByState();

        flashCo = null;
        sr.color = prev;
    }

    private void ApplyVisualByState()
    {
        bool powered = (DamageMultiplier > 1.0001f);

        if (overlayRenderer != null)
        {
            overlayRenderer.enabled = powered;
            overlayRenderer.color = poweredColor;
        }
    }
}
