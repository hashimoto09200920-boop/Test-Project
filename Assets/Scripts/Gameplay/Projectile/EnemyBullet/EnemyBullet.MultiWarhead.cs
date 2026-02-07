using System.Collections;
using UnityEngine;

public partial class EnemyBullet
{
    public void ApplyMultiWarhead(
        bool enable,
        float slowSeconds,
        float slowSpeed,
        Sprite parentSprite,
        bool parentUseSpeedCurve,
        float parentInitialSpeed,
        float parentMaxSpeed,
        float parentCurveDuration,
        AnimationCurve parentSpeedCurve,
        AudioClip parentVanishSe,
        GameObject parentVanishVfx,
        float childOffsetX,
        float childFinalSpeed,
        bool childUseRandomOffset,
        float childRandomOffsetRadius,
        float childA_Delay,
        float childA_DelayMax,
        AudioClip childA_SpawnSe,
        GameObject childA_SpawnVfx,
        Sprite childA_Sprite,
        float childA_LifeTime,
        float childB_Delay,
        float childB_DelayMax,
        AudioClip childB_SpawnSe,
        GameObject childB_SpawnVfx,
        Sprite childB_Sprite,
        float childB_LifeTime,
        EnemyBullet bulletPrefab,
        Transform projectileRoot
    )
    {
        multiWarheadEnabled = enable;
        multiSlowSeconds = Mathf.Max(0.01f, slowSeconds);
        multiSlowSpeed = Mathf.Max(0f, slowSpeed);
        multiParentSprite = parentSprite;
        multiParentUseSpeedCurve = parentUseSpeedCurve;
        multiParentInitialSpeed = parentInitialSpeed;
        multiParentMaxSpeed = parentMaxSpeed;
        multiParentCurveDuration = parentCurveDuration;
        multiParentSpeedCurve = parentSpeedCurve;
        multiParentVanishSe = parentVanishSe;
        multiParentVanishVfx = parentVanishVfx;
        multiChildOffsetX = childOffsetX;
        multiChildFinalSpeed = childFinalSpeed;
        multiChildUseRandomOffset = childUseRandomOffset;
        multiChildRandomOffsetRadius = childRandomOffsetRadius;

        multiChildA_Delay = Mathf.Max(0f, childA_Delay);
        multiChildA_DelayMax = Mathf.Max(childA_Delay, childA_DelayMax);
        multiChildA_SpawnSe = childA_SpawnSe;
        multiChildA_SpawnVfx = childA_SpawnVfx;
        multiChildA_Sprite = childA_Sprite;
        multiChildA_LifeTime = Mathf.Max(0.1f, childA_LifeTime);

        multiChildB_Delay = Mathf.Max(0f, childB_Delay);
        multiChildB_DelayMax = Mathf.Max(childB_Delay, childB_DelayMax);
        multiChildB_SpawnSe = childB_SpawnSe;
        multiChildB_SpawnVfx = childB_SpawnVfx;
        multiChildB_Sprite = childB_Sprite;
        multiChildB_LifeTime = Mathf.Max(0.1f, childB_LifeTime);

        if (multiWarheadEnabled && multiSlowSeconds > 0f && multiWarheadCo == null)
        {
            // 親弾Sprite適用
            if (multiParentSprite != null)
            {
                SetSpriteOverride(multiParentSprite);
            }

            // 親弾SpeedCurve適用
            if (multiParentUseSpeedCurve)
            {
                ApplySpeedCurve(multiParentInitialSpeed, multiParentMaxSpeed, multiParentCurveDuration, multiParentSpeedCurve);
            }
            else
            {
                speed = multiSlowSpeed;
                if (rb != null)
                {
                    rb.linearVelocity = direction.normalized * multiSlowSpeed;
                }
            }

            multiWarheadCo = StartCoroutine(MultiWarheadRoutine(bulletPrefab, projectileRoot));
        }
    }

    private IEnumerator MultiWarheadRoutine(EnemyBullet bulletPrefab, Transform projectileRoot)
    {
        // 1) 親弾の低速移動時間
        float slowWait = Mathf.Max(0.01f, multiSlowSeconds);
        yield return new WaitForSeconds(slowWait);

        if (this == null || multiWarheadDone) yield break;

        // 2) 親弾消滅位置を記録
        Vector2 vanishPos = transform.position;

        // 3) 親弾を非表示にする（まだ破棄しない）
        if (visualRenderer != null) visualRenderer.enabled = false;
        if (overlayRenderer != null) overlayRenderer.enabled = false;
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;

        // 4) 親弾消滅VFX/SE（消えたタイミング）
        if (multiParentVanishSe != null)
        {
            AudioSource.PlayClipAtPoint(multiParentVanishSe, vanishPos, 1f);
        }

        if (multiParentVanishVfx != null)
        {
            GameObject vfx = Instantiate(multiParentVanishVfx, vanishPos, Quaternion.identity, projectileRoot);
            Destroy(vfx, 2f);
        }

        // 5) 子弾A位置 = 親弾消滅位置 - X offset (左側)
        Vector2 childA_Pos = new Vector2(vanishPos.x - multiChildOffsetX, vanishPos.y);

        // 6) 子弾B位置 = 親弾消滅位置 + X offset (右側)
        Vector2 childB_Pos = new Vector2(vanishPos.x + multiChildOffsetX, vanishPos.y);

        // 7) 子弾A/Bを生成（遅延付き、親弾を残したまま）
        if (bulletPrefab != null && projectileRoot != null)
        {
            // ランダム遅延計算
            float actualDelayA = (multiChildA_DelayMax > multiChildA_Delay)
                ? Random.Range(multiChildA_Delay, multiChildA_DelayMax)
                : multiChildA_Delay;

            float actualDelayB = (multiChildB_DelayMax > multiChildB_Delay)
                ? Random.Range(multiChildB_Delay, multiChildB_DelayMax)
                : multiChildB_Delay;

            Debug.Log($"[MultiWarhead] Spawning MissileArc children | A=left curve (negative angle) delayA={actualDelayA:F3}s | B=right curve (positive angle) delayB={actualDelayB:F3}s");

            StartCoroutine(SpawnMissileArcChildRoutine(
                "A",
                childA_Pos,
                actualDelayA,
                multiChildA_Sprite,
                multiChildA_LifeTime,
                -1, // Left curve (negative angle)
                multiChildA_SpawnSe,
                multiChildA_SpawnVfx,
                bulletPrefab,
                projectileRoot
            ));

            StartCoroutine(SpawnMissileArcChildRoutine(
                "B",
                childB_Pos,
                actualDelayB,
                multiChildB_Sprite,
                multiChildB_LifeTime,
                1, // Right curve (positive angle)
                multiChildB_SpawnSe,
                multiChildB_SpawnVfx,
                bulletPrefab,
                projectileRoot
            ));
        }

        // 8) 最大遅延時間まで待ってから親弾を消滅
        float maxDelay = Mathf.Max(multiChildA_DelayMax, multiChildB_DelayMax);
        yield return new WaitForSeconds(maxDelay + 0.1f);

        multiWarheadDone = true;
        Destroy(gameObject);
    }

    private IEnumerator SpawnMissileArcChildRoutine(
        string childName,
        Vector2 spawnPos,
        float delay,
        Sprite sprite,
        float lifeTime,
        int curveSign,
        AudioClip spawnSe,
        GameObject spawnVfx,
        EnemyBullet bulletPrefab,
        Transform projectileRoot
    )
    {
        // 遅延待ち
        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }

        if (bulletPrefab == null || projectileRoot == null) yield break;

        // 出現SE/VFX
        if (spawnSe != null)
        {
            AudioSource.PlayClipAtPoint(spawnSe, spawnPos, 1f);
        }

        if (spawnVfx != null)
        {
            GameObject vfx = Instantiate(spawnVfx, spawnPos, Quaternion.identity, projectileRoot);
            Destroy(vfx, 2f);
        }

        // 子弾生成
        EnemyBullet child = Instantiate(bulletPrefab, spawnPos, Quaternion.identity, projectileRoot);

        // デバッグタグ設定
        child.SetDebugTag(childName);

        // Sprite適用
        if (sprite != null)
        {
            child.SetSpriteOverride(sprite);
        }

        // 親弾のPenetrationを子弾にコピー
        BulletPenetration parentPen = GetComponent<BulletPenetration>();
        BulletPenetration childPen = child.GetComponent<BulletPenetration>();
        if (parentPen != null && childPen != null)
        {
            childPen.SetPenetration(parentPen.Penetration);
        }

        // 初期方向は下向き（プレイヤーホーミングはMissileArcが自動で行う）
        Vector2 initialDir = Vector2.down;
        child.SetDirection(initialDir);

        // 基準速度とライフタイムを設定（MissileArcはこの速度を基準に初期/最終速度を計算）
        float baseSpeed = (multiChildFinalSpeed > 0f) ? multiChildFinalSpeed : 5f;
        child.ApplyBullet(baseSpeed, lifeTime);

        // 親弾のMissileArc設定をコピーして適用
        // curveSign: -1=左膨らみ(A), +1=右膨らみ(B)
        float signedCurveAngle = missileCurveAngle * curveSign;

        child.ApplyMissileArc(
            missileInitialSpeed,
            missileStraightDuration,
            signedCurveAngle,
            false, // Random direction は無効（A/B固定）
            missileCurveDuration,
            missileFinalSpeed,
            missileUseSpeedCurve,
            missileCurveInitialSpeed,
            missileCurveFinalSpeed,
            missileSpeedCurve,
            multiChildUseRandomOffset,
            multiChildRandomOffsetRadius
        );

        int childPenValue = (childPen != null) ? childPen.Penetration : 0;
        Debug.Log($"[MultiWarhead] Child {childName} spawned | curveAngle={signedCurveAngle:F1}° | speed={baseSpeed:F2} | bounces={child.RemainingPaddleBounces}/{paddleBounceLimit} | penetration={childPenValue}");
    }
}
