using UnityEngine;

public partial class EnemyBullet
{
    public void SetDirection(Vector2 dir)
    {
        if (dir.sqrMagnitude > 0.0001f)
        {
            direction = dir.normalized;
            lastNonZeroDir = direction;
        }

        spiralForwardDir = (direction.sqrMagnitude > 0.0001f) ? direction.normalized : Vector2.down;
        waveForwardDir = (direction.sqrMagnitude > 0.0001f) ? direction.normalized : Vector2.down;

        ApplyVelocity();
    }

    public void ApplyBullet(float newSpeed, float newLifeTime)
    {
        speed = Mathf.Max(0.01f, newSpeed);
        lifeTime = Mathf.Max(0.05f, newLifeTime);

        if (!useSpeedCurve) accelCapBaseSpeed = speed;

        accelMultiplierNow = 1f;
        curveStartTime = Time.time;

        RefreshBaseSpeedAndTargetSpeed();
        ApplyVelocity();

        ResetPaddleBounceRemaining();
        hasPaddleReflectedOnce = false;

        lastWallAngleClampFrame = -999;

        lastReviveTime = -999f;
        if (direction.sqrMagnitude > 0.0001f) lastNonZeroDir = direction.normalized;

        if (spiralForwardDir.sqrMagnitude <= 0.0001f)
        {
            spiralForwardDir = (lastNonZeroDir.sqrMagnitude > 0.0001f) ? lastNonZeroDir : Vector2.down;
        }

        if (waveForwardDir.sqrMagnitude <= 0.0001f)
        {
            waveForwardDir = (lastNonZeroDir.sqrMagnitude > 0.0001f) ? lastNonZeroDir : Vector2.down;
        }

        reflectOverrideUntil = -999f;

        explosionInitDone = false;
        explosionRingCreated = false;
        explosionBlinkVisible = true;
        explosionNextBlinkToggleTime = -999f;
    }

    public void ApplySpiralMotion(float radius, float period, bool rotateSprite)
    {
        spiralMotionEnabled = true;
        spiralRadius = Mathf.Max(0.1f, radius);
        spiralPeriod = Mathf.Max(0.1f, period);
        spiralRotateSprite = rotateSprite;
        spiralTime = 0f;
        spiralSign = (Random.value < 0.5f) ? -1 : 1; // ランダム方向

        Vector2 d = (direction.sqrMagnitude > 0.0001f) ? direction.normalized : Vector2.down;
        spiralForwardDir = d;

        ApplyVelocity();
    }

    public void ClearSpiralMotion()
    {
        spiralMotionEnabled = false;
        spiralTime = 0f;

        if (direction.sqrMagnitude > 0.0001f) lastNonZeroDir = direction.normalized;
    }

    public void ApplyWaveMotion(float amplitude, float frequency)
    {
        waveMotionEnabled = true;
        waveAmplitude = Mathf.Max(0.1f, amplitude);
        waveFrequency = Mathf.Max(0.1f, frequency);
        waveTime = 0f; // 常に中央(0度)から開始

        Vector2 d = (direction.sqrMagnitude > 0.0001f) ? direction.normalized : Vector2.down;
        waveForwardDir = d;

        ApplyVelocity();
    }

    public void ClearWaveMotion()
    {
        waveMotionEnabled = false;
        waveTime = 0f;

        if (direction.sqrMagnitude > 0.0001f) lastNonZeroDir = direction.normalized;
    }

    // Compatibility: 旧API呼び出しをクリアに変換
    public void ApplyTurnMotion(
        float rateDegPerSec,
        int directionSign,
        bool useRandomSign,
        float baseForwardShare,
        float ellipseForwardAmpShare,
        float ellipseSideAmpShare,
        float circleAmpShare,
        float blendWidthDeg
    )
    {
        ClearSpiralMotion();
    }

    public void ClearTurnMotion()
    {
        ClearSpiralMotion();
    }

    public void ApplyArcTurn(float rateDegPerSec, int directionSign, bool useRandomSign, float forwardShare, float sideAmpShare)
    {
        ClearWaveMotion();
    }

    public void ApplyArcTurn(
        float rateDegPerSec,
        int directionSign,
        bool useRandomSign,
        float baseForwardShare,
        float ellipseForwardAmpShare,
        float ellipseSideAmpShare,
        float circleAmpShare,
        float blendWidthDeg
    )
    {
        ClearWaveMotion();
    }

    public void ClearArcTurn()
    {
        ClearWaveMotion();
    }

    private Vector2 ComputeWaveVelocity(Vector2 forwardDir, float targetSpeed, float dt)
    {
        waveTime += dt;

        Vector2 f = (forwardDir.sqrMagnitude > 0.0001f) ? forwardDir.normalized : Vector2.down;
        Vector2 side = new Vector2(-f.y, f.x); // 左側が正

        // Sin波で左右に揺れる
        float angle = 2f * Mathf.PI * waveFrequency * waveTime;

        // 横方向の最大速度を前進速度の一定比率に制限（振幅ベース）
        // amplitude は「前進速度に対する横揺れ速度の比率」として解釈
        // 例: amplitude=0.5, targetSpeed=4.0 → 横方向最大速度 = 2.0
        float maxSideSpeed = targetSpeed * waveAmplitude;

        // 横方向の速度（-maxSideSpeed ~ +maxSideSpeed を往復）
        // Sin の微分は Cos なので、位相をずらす
        float sideVelocity = maxSideSpeed * Mathf.Sin(angle);

        // 前進速度は一定、横揺れ速度を加算
        Vector2 v = f * targetSpeed + side * sideVelocity;

        if (v.sqrMagnitude < 0.0001f)
        {
            v = f * Mathf.Max(0.01f, targetSpeed);
        }

        return v;
    }

    private Vector2 ComputeSpiralVelocity(Vector2 forwardDir, float targetSpeed, float dt)
    {
        spiralTime += dt;

        // 速度連動: 速いほど回転も速くなる
        float speedFactor = Mathf.Clamp(targetSpeed / 5f, 0.5f, 2f); // 基準速度5で1倍
        float effectivePeriod = spiralPeriod / speedFactor;

        float angularVelocity = (2f * Mathf.PI / effectivePeriod) * spiralSign;
        float angle = angularVelocity * spiralTime;

        Vector2 f = (forwardDir.sqrMagnitude > 0.0001f) ? forwardDir.normalized : Vector2.down;
        Vector2 side = new Vector2(-f.y, f.x); // 左方向が正

        // 円運動の接線速度 = radius * angularSpeed (前進速度に対する比率として解釈)
        // radius は「前進速度に対する円軌道半径の比率」として扱う
        // 例: radius=0.5, targetSpeed=5.0, period=0.5 → 円周速度 = 2.5 * 2π/0.5 ≈ 31 (大きすぎる)
        // → radius を実際の半径ではなく、速度比率として扱う方が直感的
        float circleSpeed = targetSpeed * spiralRadius;

        // 円軌道の速度成分（angle=0で右側、90度で下、180度で左側、270度で上）
        // Cos成分が横方向、Sin成分が前進方向
        Vector2 circleVelocity = side * (circleSpeed * Mathf.Cos(angle)) +
                                 f * (circleSpeed * Mathf.Sin(angle));

        // 前進速度 + 円軌道の速度
        Vector2 v = f * targetSpeed + circleVelocity;

        if (v.sqrMagnitude < 0.0001f)
        {
            v = f * Mathf.Max(0.01f, targetSpeed);
        }

        // Sprite回転
        if (spiralRotateSprite && visualRenderer != null)
        {
            float rotationDeg = angle * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, rotationDeg);
        }

        return v;
    }

    private void ApplyVelocity()
    {
        if (rb == null) return;

        bool isWave = waveMotionEnabled;
        bool isSpiral = spiralMotionEnabled;

        if (isWave && !isBeingDestroyed)
        {
            Vector2 f = (waveForwardDir.sqrMagnitude > 0.0001f) ? waveForwardDir.normalized : Vector2.down;
            Vector2 v = ComputeWaveVelocity(f, TargetSpeed, Time.deltaTime * GetTimeScale());
            rb.linearVelocity = v;

            if (v.sqrMagnitude > 0.0001f) lastNonZeroDir = v.normalized;
            return;
        }

        if (isSpiral && !isBeingDestroyed)
        {
            Vector2 f = (spiralForwardDir.sqrMagnitude > 0.0001f) ? spiralForwardDir.normalized : Vector2.down;
            Vector2 v = ComputeSpiralVelocity(f, TargetSpeed, Time.deltaTime * GetTimeScale());
            rb.linearVelocity = v;

            if (v.sqrMagnitude > 0.0001f) lastNonZeroDir = v.normalized;
            return;
        }

        // Straight fallthrough
        Vector2 dir = (direction.sqrMagnitude > 0.0001f) ? direction.normalized : Vector2.down;
        float timeScale = GetTimeScale();
        rb.linearVelocity = dir * TargetSpeed * timeScale;

        if (dir.sqrMagnitude > 0.0001f) lastNonZeroDir = dir;
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // MissileArc（ミサイル起動 → 直進）
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    public void ApplyMissileArc(
        float initialSpeed,
        float straightDuration,
        float curveAngle,
        bool curveRandomDirection,
        float curveDuration,
        float finalSpeed,
        bool useSpeedCurve,
        float curveInitialSpeed,
        float curveFinalSpeed,
        AnimationCurve speedCurve,
        bool useRandomOffset,
        float randomOffsetRadius
    )
    {
        missileArcEnabled = true;
        missileInitialSpeed = initialSpeed;
        missileStraightDuration = Mathf.Max(0f, straightDuration);
        missileCurveAngle = Mathf.Clamp(curveAngle, -180f, 180f); // -180〜180に制限（負=左、正=右）
        missileCurveRandomDirection = curveRandomDirection;
        missileCurveDuration = Mathf.Max(0.1f, curveDuration);
        missileFinalSpeed = finalSpeed;
        missileUseSpeedCurve = useSpeedCurve;
        missileCurveInitialSpeed = curveInitialSpeed;
        missileCurveFinalSpeed = curveFinalSpeed;
        missileSpeedCurve = speedCurve;
        missileUseRandomOffset = useRandomOffset;
        missileRandomOffsetRadius = Mathf.Max(0f, randomOffsetRadius);

        if (missileArcCoroutine != null)
        {
            StopCoroutine(missileArcCoroutine);
        }
        // ★遅延なしで即座に開始（direction は既に SetDirection() で設定済み）
        missileArcCoroutine = StartCoroutine(MissileArcRoutine());
    }

    public void ClearMissileArc()
    {
        missileArcEnabled = false;
        if (missileArcCoroutine != null)
        {
            StopCoroutine(missileArcCoroutine);
            missileArcCoroutine = null;
        }
    }

    private System.Collections.IEnumerator MissileArcRoutine()
    {
        if (rb == null) yield break;

        // 1フレーム待機して direction が確実に設定されるのを待つ
        yield return null;

        // ★MissileArcは常にプレイヤー追尾なので、発射時の現在のプレイヤー位置を取得
        PixelDancerController player = Object.FindObjectOfType<PixelDancerController>();
        Vector2 baseDir = Vector2.down;
        if (player != null)
        {
            Vector2 toPlayer = (Vector2)player.transform.position - (Vector2)transform.position;
            if (toPlayer.sqrMagnitude > 0.0001f)
            {
                baseDir = toPlayer.normalized;
            }
        }

        // ★カーブ方向決定（ランダムオプションがONの場合のみランダム化）
        float actualCurveAngle = missileCurveAngle;
        if (missileCurveRandomDirection)
        {
            float curveSign = (Random.value < 0.5f) ? -1f : 1f;
            actualCurveAngle = missileCurveAngle * curveSign;
        }

        Debug.Log($"[MissileArc] START | id={GetInstanceID()} | baseDir={baseDir} | curveAngle={actualCurveAngle:F1} | randomDir={missileCurveRandomDirection}");

        // 速度計算（SpeedCurve使用時は専用の速度値、通常時は従来の速度値）
        float initialSpd = (missileInitialSpeed > 0f) ? missileInitialSpeed : speed * 0.5f;
        float finalSpd = (missileFinalSpeed > 0f) ? missileFinalSpeed : speed * 1.5f;

        if (missileUseSpeedCurve)
        {
            initialSpd = (missileCurveInitialSpeed > 0f) ? missileCurveInitialSpeed : speed * 0.5f;
            finalSpd = (missileCurveFinalSpeed > 0f) ? missileCurveFinalSpeed : speed * 1.5f;
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Phase 1: 初期直進（遅い）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        float straightElapsed = 0f;
        while (straightElapsed < missileStraightDuration)
        {
            straightElapsed += Time.deltaTime * GetTimeScale();

            // ★Phase 1でもプレイヤー追尾
            PixelDancerController p1 = Object.FindObjectOfType<PixelDancerController>();
            Vector2 toP1 = baseDir;
            if (p1 != null)
            {
                Vector2 vec = (Vector2)p1.transform.position - (Vector2)transform.position;
                if (vec.sqrMagnitude > 0.0001f)
                {
                    toP1 = vec.normalized;
                }
            }

            float timeScale = GetTimeScale();
            rb.linearVelocity = toP1 * initialSpd * timeScale;
            direction = toP1;

            yield return null;
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Phase 2: 円弧 + player追尾
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        Debug.Log($"[MissileArc] Phase2 START | id={GetInstanceID()} | missileCurveDuration={missileCurveDuration:F4}");

        float curveElapsed = 0f;

        // ★Phase 2開始時の方向（Phase 1終了時の最新direction）
        Vector2 curveStartDir = (direction.sqrMagnitude > 0.0001f) ? direction.normalized : baseDir;

        PixelDancerController lastPlayerInPhase2 = null;
        int phase2FrameCount = 0;

        while (curveElapsed < missileCurveDuration)
        {
            phase2FrameCount++;
            curveElapsed += Time.deltaTime * GetTimeScale();
            float t = Mathf.Clamp01(curveElapsed / missileCurveDuration);

            // 毎フレームplayer方向を取得
            PixelDancerController p2 = Object.FindObjectOfType<PixelDancerController>();
            lastPlayerInPhase2 = p2; // Phase2最終フレームのPlayer参照を記録

            if (phase2FrameCount <= 3)
            {
                Debug.Log($"[MissileArc] Phase2 Loop frame={phase2FrameCount} | curveElapsed={curveElapsed:F4} | t={t:F4} | player={(p2 != null ? "Found" : "NULL")}");
            }

            Vector2 toPlayerDir = curveStartDir;
            if (p2 != null)
            {
                Vector2 toPlayer = (Vector2)p2.transform.position - (Vector2)transform.position;
                if (toPlayer.sqrMagnitude > 0.0001f)
                {
                    toPlayerDir = toPlayer.normalized;
                }
            }

            // ★プレイヤー方向への追尾（円弧なし）
            Vector2 trackingDir = toPlayerDir;

            // ★円弧の膨らみ（0→1→0）- Sin波で途中だけ膨らむ
            float arcStrength = Mathf.Sin(Mathf.PI * t) * (actualCurveAngle / 30f);

            // 垂直ベクトル（円弧用）- プレイヤー方向に対して垂直
            Vector2 perpendicular = new Vector2(-trackingDir.y, trackingDir.x);

            // 最終方向 = プレイヤー追尾方向 + 円弧オフセット
            // t=0 または t=1 では arcStrength=0 なので、完全にプレイヤー方向
            Vector2 currentDir = (trackingDir + perpendicular * arcStrength).normalized;

            // 現在の速度（initialSpd → finalSpd へ加速）
            float currentSpd;
            if (missileUseSpeedCurve && missileSpeedCurve != null)
            {
                float curveValue = missileSpeedCurve.Evaluate(t);
                currentSpd = Mathf.Lerp(initialSpd, finalSpd, curveValue);
            }
            else
            {
                currentSpd = Mathf.Lerp(initialSpd, finalSpd, t);
            }

            float timeScale = GetTimeScale();
            rb.linearVelocity = currentDir * currentSpd * timeScale;
            direction = currentDir;

            yield return null;
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Phase 3: 高速直進（最終player方向）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        Debug.Log($"[MissileArc] Phase2 END | id={GetInstanceID()} | totalFrames={phase2FrameCount} | direction={direction} | vel={rb.linearVelocity} | lastPlayerInPhase2={(lastPlayerInPhase2 != null ? "Found" : "NULL")}");

        // ★Phase 2終了後、最終的にplayer方向へ確実に向ける
        // Phase 2の最終フレームで見つかったプレイヤーを優先使用
        PixelDancerController finalPlayer = lastPlayerInPhase2;
        if (finalPlayer == null)
        {
            // Phase2で見つからなかった場合のみ再検索
            finalPlayer = Object.FindObjectOfType<PixelDancerController>();
        }

        Debug.Log($"[MissileArc] Phase3 ENTRY | id={GetInstanceID()} | finalPlayer={(finalPlayer != null ? "Found" : "NULL")} | usedLastPlayerRef={(lastPlayerInPhase2 != null)}");

        if (finalPlayer != null)
        {
            Vector2 playerPos = (Vector2)finalPlayer.transform.position;
            Vector2 targetPos = playerPos;

            // ★ランダムオフセット適用
            if (missileUseRandomOffset && missileRandomOffsetRadius > 0f)
            {
                // 円形ランダム分布（均一分布）
                float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
                float distance = Random.Range(0f, missileRandomOffsetRadius);
                Vector2 offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
                targetPos = playerPos + offset;

                Debug.Log($"[MissileArc] Phase3 RandomOffset | id={GetInstanceID()} | offset={offset} | targetPos={targetPos}");
            }

            Vector2 finalToTarget = targetPos - (Vector2)transform.position;
            Debug.Log($"[MissileArc] Phase3 ToPlayer | id={GetInstanceID()} | toTarget={finalToTarget} | sqrMag={finalToTarget.sqrMagnitude:F4}");

            if (finalToTarget.sqrMagnitude > 0.0001f)
            {
                direction = finalToTarget.normalized;
                lastNonZeroDir = direction;

                // ★即座にターゲット方向にvelocityを設定
                float timeScale = GetTimeScale();
                rb.linearVelocity = direction * finalSpd * timeScale;

                Debug.Log($"[MissileArc] Phase3 START | id={GetInstanceID()} | finalDir={direction} | vel={rb.linearVelocity} | finalSpd={finalSpd}");
            }
            else
            {
                Debug.LogWarning($"[MissileArc] Phase3 SKIP (sqrMag too small) | id={GetInstanceID()}");
            }
        }
        else
        {
            Debug.LogWarning($"[MissileArc] Phase3 SKIP (Player not found) | id={GetInstanceID()}");
            if (direction.sqrMagnitude > 0.0001f)
            {
                direction = direction.normalized;
                lastNonZeroDir = direction;
            }
        }

        missileArcEnabled = false;
        missileArcCoroutine = null;

        // 既存直進ロジックに合流（finalSpd で継続）
        speed = finalSpd;
        ApplyVelocity();
    }
}
