using System.Collections;
using UnityEngine;

public partial class EnemyBullet
{
    // =========================================================
    // Warp API（EnemyShooter から呼ぶ）
    // =========================================================
    public void ApplyWarp(
        bool enable,
        float disappearAfterSeconds,
        float reappearAfterSeconds,
        float offsetXRange,
        GameObject disappearVfxPrefab,
        GameObject reappearVfxPrefab,
        AudioClip disappearSe,
        AudioClip reappearSe
    )
    {
        warpEnabled = enable;
        warpDisappearAfterSeconds = Mathf.Max(0.01f, disappearAfterSeconds);
        warpReappearAfterSeconds = Mathf.Max(0.01f, reappearAfterSeconds);
        warpOffsetXRange = Mathf.Max(0f, offsetXRange);

        warpDisappearVfxPrefab = disappearVfxPrefab;
        warpReappearVfxPrefab = reappearVfxPrefab;
        warpDisappearSe = disappearSe;
        warpReappearSe = reappearSe;

        // Awake後に呼ばれるため、ここでコルーチン開始
        if (warpEnabled && warpDisappearAfterSeconds > 0f && warpCo == null)
        {
            warpCo = StartCoroutine(WarpRoutine());
        }
    }

    // =========================================================
    // Warp Routine（消滅→ワープ→出現）
    // =========================================================
    private IEnumerator WarpRoutine()
    {
        // 1) 発射後、warpDisappearAfterSeconds 秒待つ
        float disappearWait = Mathf.Max(0.01f, warpDisappearAfterSeconds);
        yield return new WaitForSeconds(disappearWait);

        if (this == null || warpDone) yield break;

        // 2) 消滅前の位置と方向を記録
        Vector2 beforePos = transform.position;
        Vector2 beforeDir = direction;

        // 3) 消滅処理（Collider OFF / Sprite OFF）
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;

        if (visualRenderer != null) visualRenderer.enabled = false;
        if (overlayRenderer != null) overlayRenderer.enabled = false;

        // 4) 消滅VFX/SE
        if (feedback != null)
        {
            feedback.OnWarpDisappear(beforePos, warpDisappearVfxPrefab, warpDisappearSe);
        }

        // 5) warpReappearAfterSeconds 秒待つ
        float reappearWait = Mathf.Max(0.01f, warpReappearAfterSeconds);
        yield return new WaitForSeconds(reappearWait);

        if (this == null || warpDone) yield break;

        // 6) 出現位置を計算（Xだけ±rangeで横方向にワープ）
        float range = Mathf.Max(0f, warpOffsetXRange);
        float offsetX = (range > 0f) ? Random.Range(-range, range) : 0f;
        Vector2 afterPos = new Vector2(beforePos.x + offsetX, beforePos.y);

        transform.position = afterPos;

        // 7) 出現後の進行方向は「フロア方向へランダム」
        PixelDancerController dancer = Object.FindFirstObjectByType<PixelDancerController>();
        if (dancer != null)
        {
            float centerX = dancer.transform.position.x;
            float centerY = dancer.transform.position.y;
            float dancerRange = 3f;

            System.Reflection.FieldInfo rangeField = typeof(PixelDancerController).GetField("autoMoveRange", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (rangeField != null)
            {
                object val = rangeField.GetValue(dancer);
                if (val is float f) dancerRange = f;
            }

            float targetX = centerX + Random.Range(-dancerRange, dancerRange);
            Vector2 target = new Vector2(targetX, centerY);
            Vector2 dir = (target - afterPos).normalized;

            if (dir.sqrMagnitude > 0.0001f)
            {
                direction = dir;
            }
            else
            {
                direction = Vector2.down;
            }
        }
        else
        {
            direction = Vector2.down;
        }

        lastNonZeroDir = direction.normalized;

        // Wave/Spiral の現在方向も更新
        waveForwardDir = lastNonZeroDir;
        spiralForwardDir = lastNonZeroDir;

        // 8) 出現処理（Collider ON / Sprite ON）
        if (col != null) col.enabled = true;

        if (visualRenderer != null) visualRenderer.enabled = true;
        if (overlayRenderer != null && IsPoweredNow) overlayRenderer.enabled = true;

        // 9) 出現VFX/SE
        if (feedback != null)
        {
            feedback.OnWarpReappear(afterPos, warpReappearVfxPrefab, warpReappearSe);
        }

        // 10) 速度を再適用（Rigidbody2Dの速度を復元）
        if (rb != null)
        {
            float timeScale = GetTimeScale();
            rb.linearVelocity = direction.normalized * TargetSpeed * timeScale;
        }

        warpDone = true;
    }
}
