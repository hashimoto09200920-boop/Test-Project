using UnityEngine;

public partial class EnemyBullet
{
    private void ResetPaddleBounceRemaining()
    {
        if (!usePaddleBounceLimit || paddleBounceLimit <= 0)
        {
            remainingPaddleBounces = -1;
            if (showDebugLog)
            {
                Debug.Log($"[弾生成] Bounce Limit無効 (remaining=-1)");
            }
        }
        else
        {
            remainingPaddleBounces = paddleBounceLimit;
            if (showDebugLog)
            {
                Debug.Log($"[弾生成] Bounce Limit={paddleBounceLimit}, remaining={remainingPaddleBounces}");
            }
        }

        lastPaddleBounceTime = -999f;
        lastPaddleReflectTime = -999f;
        lastEnemyHitCountFrame = -999;
        lastWallBounceFrame = -999;
        lastBulletContactFrame = -999;
        lastBulletContactOtherId = 0;
        lastWallAngleClampFrame = -999;
    }

    // 短時間内での多段ヒット防止（線引き中の複数PaddleDot生成による連続消費を防ぐ）
    // PaddleDotが衝突処理に入る前に呼ぶ。
    // クールダウン内は false（スキップ）、クールダウン経過後は true（処理続行）
    private const float paddleReflectCooldown = 0.08f;
    private float lastPaddleReflectTime = -999f;
    public bool TryAcquirePaddleReflectThisFrame()
    {
        if (Time.unscaledTime - lastPaddleReflectTime < paddleReflectCooldown) return false;
        lastPaddleReflectTime = Time.unscaledTime;
        return true;
    }

    public void ConfigurePaddleBounceLimit(int limit)
    {
        paddleBounceLimit = limit;

        // BulletTypeから呼ばれた時、自動的に有効/無効を設定
        usePaddleBounceLimit = (limit > 0);

        ResetPaddleBounceRemaining();
    }

    /// <summary>
    /// Wave弾・Spiral弾が線で跳ね返った時、跳ね返る向きを「見た目の進路を線の面で跳ね返した向き」に直す（速さは物理の反射後のまま）
    /// </summary>
    private void RedirectWaveSpiralReflectToForward(Vector2 before, Vector2 after)
    {
        Vector2 forward = waveMotionEnabled ? waveForwardDir : spiralForwardDir;
        if (forward.sqrMagnitude < 0.0001f) return;
        Vector2 change = after - before;                 // 跳ね返りで変わった分＝線の面に垂直な向き
        if (change.sqrMagnitude < 0.0001f) return;       // 向きが変わっていない（求められない）時は物理の結果のまま
        Vector2 normal = change.normalized;              // 線から弾の側へ向かう向き
        forward = forward.normalized;
        // 進路が線へ向かっている時だけ跳ね返す（進路がもう線から離れる向きなら、そのまま進路の向きへ）
        Vector2 dir = Vector2.Dot(forward, normal) < 0f ? Vector2.Reflect(forward, normal).normalized : forward;
        float speed = after.magnitude > 0.0001f ? after.magnitude : Mathf.Max(0.01f, TargetSpeed) * GetTimeScale();
        rb.linearVelocity = dir * speed;
        direction = dir;
        lastNonZeroDir = dir;
    }

    /// <summary>
    /// アイテム「マルチバウンド」の効果中に線で跳ね返した時（PaddleDot。この跳ね返りの回数を数える前に呼ぶ）。
    /// 跳ね返せる回数の残りに、この弾の上限と同じ数を足す＝上限が2倍になったのと同じ。
    /// 1つの弾に1回だけ。上限が無い弾（無制限）には何もしない。効果が切れても、この弾は増えたまま（2026/11/11）
    /// </summary>
    public void ApplyBounceLimitBoost()
    {
        if (bounceLimitBoosted) return;
        if (!usePaddleBounceLimit || paddleBounceLimit <= 0 || remainingPaddleBounces < 0) return;
        bounceLimitBoosted = true;
        remainingPaddleBounces += paddleBounceLimit;
    }

    public void RegisterPaddleBounce(PaddleDot.LineType lineType)
    {
        if (isBeingDestroyed) return;

        // C2 JustPenetration: パドル反射は物理エンジンが同一ステップ内で速度を変更した後に
        // このメソッドが呼ばれる。FixedUpdate時点のpreCollisionVelocityは反射前の古い値のままなので、
        // ここで反射後の実速度に更新しておかないと、直後に別の弾へ接触した際
        // TryApplyC2PenetrationVelocity()が反射前の（間違った）方向へ速度を戻してしまう
        // ★Wave弾・Spiral弾（2026/11/09）：揺れ・回転しながら進むため、当たった瞬間の向きは見た目の進路から傾いている。
        //   物理の反射はその傾いた向きを跳ね返すので、狙った方向に返らなかった。
        //   見た目の進路（waveForwardDir / spiralForwardDir）を線の面で跳ね返した向きに直す。
        //   線の面の向きは、当たる前（FixedUpdateで記録した preCollisionVelocity）と後の速度の差から求める
        if (rb != null && (waveMotionEnabled || spiralMotionEnabled))
            RedirectWaveSpiralReflectToForward(preCollisionVelocity, rb.linearVelocity);

        if (rb != null) preCollisionVelocity = rb.linearVelocity;

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

        // ★このバウンスでちょうどバウンス上限に達して弾が消える場合は、通常の反射VFX/SEを省略する
        //   （以前はバウンス数チェックより先にfeedback.OnPaddleReflect()を無条件で呼んでいたため、
        //   消える直前の弾にも反射VFX/SEを出してしまっていた。至近距離で大量の弾が同時多発的に
        //   バウンス上限へ達する状況でのVFX/SE発生量を減らすため、判定を先に行う。
        //   弾が消える条件・タイミング自体は変更していない＝消える瞬間の演出をPlayDisappearVfx等に一本化するだけ）
        bool limitActive = usePaddleBounceLimit && paddleBounceLimit > 0;
        bool cooldownElapsed = !limitActive || (Time.unscaledTime - lastPaddleBounceTime >= paddleReflectCooldown);
        bool willDestroyThisBounce = limitActive && cooldownElapsed && remainingPaddleBounces == 1;

        // VFX/SE 分離（PaddleHit VFX）
        if (!willDestroyThisBounce && feedback != null) feedback.OnPaddleReflect(transform.position);

        if (!limitActive) return;
        if (!cooldownElapsed) return;
        lastPaddleBounceTime = Time.unscaledTime;

        if (remainingPaddleBounces > 0)
        {
            remainingPaddleBounces--;
            if (showDebugLog)
            {
                Debug.Log($"[Paddle反射] 残り: {remainingPaddleBounces}/{paddleBounceLimit}");
            }

            if (remainingPaddleBounces <= 0)
            {
                if (showDebugLog)
                {
                    Debug.Log($"[Paddle反射] 弾を破棄! remaining={remainingPaddleBounces}");
                }
                if (feedback != null)
                {
                    feedback.PlayDisappearVfx(transform.position);
                    feedback.PlayDestroySeOnce(transform.position);
                }
                isBeingDestroyed = true;
                ReleaseOrDestroySelf();
            }
        }
    }

    public void RegisterEnemyHitAsBounce()
    {
        if (isBeingDestroyed) return;
        // アイテム「ブラストショット」：跳ね返した弾が敵に当たったら爆発（2026/11/11）
        if (ShotKind == ShotItemType.Blast && !blastShotDone) ShotItemManager.OnBlastHit(this);
        // ★修正: パドル反射前でも敵ヒットをカウント
        // if (!hasPaddleReflectedOnce) return;

        // ★RegisterPaddleBounceと同じ理由で、このヒットでバウンス上限に達して弾が消える場合は
        //   通常のヒットVFX/SEを省略する（弾が消える条件・タイミング自体は変更していない）
        bool limitActive = usePaddleBounceLimit && paddleBounceLimit > 0;
        bool frameAlreadyCounted = limitActive && Time.frameCount == lastEnemyHitCountFrame;
        bool willDestroyThisBounce = limitActive && !frameAlreadyCounted && remainingPaddleBounces == 1;

        // 反射弾の着弾演出（ReflectedBulletFXManager）。この当たりで弾が消える場合も出す。
        // 出せた時は旧VFX（EnemyHit VFX / JustPowered VFX）を出さない
        bool newHitFx = ReflectedBulletFXManager.NotifyEnemyHit(this, transform.position, IsPoweredNow);

        // VFX/SE 分離（EnemyHit VFX / JustPowered VFX）
        if (!newHitFx && !willDestroyThisBounce && feedback != null) feedback.OnEnemyHit(transform.position, IsPoweredNow);

        ApplyA8HitBonus();

        if (!limitActive) return;
        if (frameAlreadyCounted) return;
        lastEnemyHitCountFrame = Time.frameCount;

        if (remainingPaddleBounces > 0)
        {
            remainingPaddleBounces--;
            if (showDebugLog)
            {
                Debug.Log($"[敵ヒット] 残り: {remainingPaddleBounces}/{paddleBounceLimit}");
            }

            if (remainingPaddleBounces <= 0)
            {
                if (showDebugLog)
                {
                    Debug.Log($"[敵ヒット] 弾を破棄! remaining={remainingPaddleBounces}");
                }
                if (feedback != null)
                {
                    feedback.PlayDisappearVfx(transform.position);
                    feedback.PlayDestroySeOnce(transform.position);
                }
                isBeingDestroyed = true;
                ReleaseOrDestroySelf();
            }
        }
    }

    /// <summary>
    /// A8スキル: 敵ヒットごとに基礎ダメージ加算（同フレーム多重ヒット防止）。
    /// 加算量はSkillDefinition.effectValue（Inspectorで調整可能）、端数はRoundToInt。
    /// RegisterEnemyHitAsBounce()から呼ばれる通常経路に加え、「ドリル反射」弾
    /// （PinnedReflectBullet）が刺さっている間の中間ヒットからも、VFXやBounceLimit消費を
    /// 伴わずにA8のカウント加算だけを行いたいため、単独で呼べるメソッドとして分離している
    /// </summary>
    public void ApplyA8HitBonus()
    {
        if (a8MaxAdditions > 0 && a8EnemyHitCount < a8MaxAdditions)
        {
            if (Time.frameCount != lastA8HitFrame)
            {
                lastA8HitFrame = Time.frameCount;
                if (a8EnemyHitCount == 0) damageBeforeA8 = damageValue; // 未反射に戻す時（RevertToUnreflected）にA8分を取り消すため
                a8EnemyHitCount++;
                damageValue = Mathf.RoundToInt(1f + a8EnemyHitCount * a8DamagePerHit);
            }
        }
    }

    private void RegisterWallBounce()
    {
        if (isBeingDestroyed) return;
        // アイテム「ブラストショット」：画面の端の壁に当たっても爆発（2026/11/12）
        NotifyBlastShotObstacleHit();

        // C2 JustPenetration: 壁反射も同一物理ステップ内で速度が変わった後に呼ばれるため、
        // RegisterPaddleBounceと同じ理由でpreCollisionVelocityを更新しておく
        if (rb != null) preCollisionVelocity = rb.linearVelocity;

        // ★修正: パドル反射前でも壁反射をカウント
        // if (!hasPaddleReflectedOnce) return;

        if (!usePaddleBounceLimit) return;
        if (paddleBounceLimit <= 0) return;

        if (Time.frameCount == lastWallBounceFrame) return;
        lastWallBounceFrame = Time.frameCount;

        remainingPaddleBounces--;
        if (showDebugLog)
        {
            Debug.Log($"[壁反射] 残り: {remainingPaddleBounces}/{paddleBounceLimit}");
        }

        if (remainingPaddleBounces <= 0)
        {
            if (showDebugLog)
            {
                Debug.Log($"[壁反射] 弾を破棄! remaining={remainingPaddleBounces}");
            }
            if (feedback != null)
            {
                feedback.PlayDisappearVfx(transform.position);
                feedback.PlayDestroySeOnce(transform.position);
            }
            isBeingDestroyed = true;
            ReleaseOrDestroySelf();
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
        if (showDebugLog)
        {
            Debug.Log($"[弾同士接触] 残り: {remainingPaddleBounces}/{paddleBounceLimit}");
        }

        if (remainingPaddleBounces <= 0)
        {
            if (showDebugLog)
            {
                Debug.Log($"[弾同士接触] 弾を破棄! remaining={remainingPaddleBounces}");
            }
            if (feedback != null)
            {
                feedback.PlayDisappearVfx(transform.position);
                feedback.PlayDestroySeOnce(transform.position);
            }
            isBeingDestroyed = true;
            ReleaseOrDestroySelf();
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

        // ★ドリルの力関係（2026/11/06）：反射したドリル ＞ 反射していないドリル ＞ 反射弾。強い方が貫通して飛び続け、弱い方が消える
        int drillRankA = DrillClashRank(this, aRef);
        int drillRankB = DrillClashRank(other, bRef);
        if (IsDrillClashPair(drillRankA, drillRankB))
        {
            HandleDrillClash(other, drillRankA, drillRankB);
            return true;
        }

        if (!TryAcquirePairCooldown(other, bulletPairCooldownSeconds))
        {
            // 相手側コールバックで既に処理済みだが、貫通する側の速度復元はこちら側でも必要。
            // 反射済みドリル反射弾はC2スキルの有無に関わらず必ず貫通するため、強制的に速度復元する
            bool aJust2 = (DamageMultiplier > 1.0001f);
            bool selfIsReflectedDrill2 = GetComponent<PinnedReflectBullet>() != null;
            if (aRef && !bRef && !isBeingDestroyed)
            {
                if (selfIsReflectedDrill2) RestoreVelocityForPenetration();
                else if (aJust2) TryApplyC2PenetrationVelocity();
            }
            return true;
        }

        bool aJust = (DamageMultiplier > 1.0001f);
        bool bJust = (other.DamageMultiplier > 1.0001f);

        if (aRef && bRef)
        {
            // 反射弾同士は速度を交換して跳ね返る
            if (rb != null && other.rb != null)
            {
                Vector2 velA = rb.linearVelocity;
                Vector2 velB = other.rb.linearVelocity;
                rb.linearVelocity = velB;
                other.rb.linearVelocity = velA;
                direction = velB.normalized;
                if (direction.sqrMagnitude > 0.0001f) lastNonZeroDir = direction;
                other.direction = velA.normalized;
                if (other.direction.sqrMagnitude > 0.0001f) other.lastNonZeroDir = other.direction;
            }
            return true;
        }

        if (aRef && !bRef)
        {
            // ★ドリル反射弾（PinnedReflectBullet）は未反射の間、反射弾と接触しても消滅しない。
            //   逆にこちら側（反射済み）がドリル反射弾の場合は、C2スキルの有無に関わらず必ず
            //   未反射弾を貫通する（通常の反射弾はJust＋C2スキル所持時のみ貫通する従来仕様のまま）
            bool otherIsDrill = other.GetComponent<PinnedReflectBullet>() != null;
            bool selfIsReflectedDrill = GetComponent<PinnedReflectBullet>() != null;

            if (!otherIsDrill)
            {
                // 弾同士の衝突演出（ReflectedBulletFXManager）。出せた時は旧VFX（Disappear Vfx Prefab）を出さない（SEは鳴らす）
                bool clashFx = ReflectedBulletFXManager.NotifyBulletClash(this, other, selfIsReflectedDrill || aJust);
                if (selfIsReflectedDrill)
                {
                    other.DestroyByBulletContact(playVfx: !clashFx);
                    RestoreVelocityForPenetration();
                }
                else if (aJust)
                {
                    other.DestroyByBulletContact(playVfx: !clashFx);
                    TryApplyC2PenetrationVelocity();
                }
                else
                {
                    DestroyByBulletContact(playVfx: !clashFx);
                    other.DestroyByBulletContact(playFeedback: false);
                }
            }
            return true;
        }

        if (!aRef && bRef)
        {
            // ★ドリル反射弾（PinnedReflectBullet）は未反射の間、反射弾と接触しても消滅しない。
            //   逆に相手側（反射済み）がドリル反射弾の場合は、C2スキルの有無に関わらず必ず
            //   こちら（未反射弾）を貫通する
            bool selfIsDrill = GetComponent<PinnedReflectBullet>() != null;
            bool otherIsReflectedDrill = other.GetComponent<PinnedReflectBullet>() != null;

            if (!selfIsDrill)
            {
                // 弾同士の衝突演出（ReflectedBulletFXManager）。出せた時は旧VFX（Disappear Vfx Prefab）を出さない（SEは鳴らす）
                bool clashFx = ReflectedBulletFXManager.NotifyBulletClash(other, this, otherIsReflectedDrill || bJust);
                if (otherIsReflectedDrill)
                {
                    DestroyByBulletContact(playVfx: !clashFx);
                    other.RestoreVelocityForPenetration();
                }
                else if (bJust)
                {
                    DestroyByBulletContact(playVfx: !clashFx);
                }
                else
                {
                    DestroyByBulletContact(playVfx: !clashFx);
                    other.DestroyByBulletContact(playFeedback: false);
                }
            }
            return true;
        }

        return false;
    }

    // =========================================================
    // ドリルの力関係（2026/11/06）
    //   3＝反射したドリル ／ 2＝反射していないドリル ／ 1＝反射弾（ドリル以外） ／ 0＝反射していない弾（ドリル以外）
    //   対象は「2×1」「3×2」「3×1」の3つだけ。「3×0」（反射したドリルが普通の未反射弾を貫通）は従来どおり下の処理に任せる
    // =========================================================
    private static int DrillClashRank(EnemyBullet b, bool reflected)
    {
        bool drill = b.GetComponent<PinnedReflectBullet>() != null;
        if (drill) return reflected ? 3 : 2;
        return reflected ? 1 : 0;
    }

    private static bool IsDrillClashPair(int rankA, int rankB)
    {
        int hi = Mathf.Max(rankA, rankB);
        int lo = Mathf.Min(rankA, rankB);
        return hi >= 2 && lo >= 1 && hi > lo;
    }

    private void HandleDrillClash(EnemyBullet other, int selfRank, int otherRank)
    {
        EnemyBullet winner = selfRank > otherRank ? this : other;
        EnemyBullet loser = winner == this ? other : this;

        if (!TryAcquirePairCooldown(other, bulletPairCooldownSeconds))
        {
            // 相手側の通知で処理済み。物理エンジンが跳ね返した分を、貫通する側（こちらが勝った時）だけ元の速さと向きに戻す
            if (winner == this && !isBeingDestroyed) RestoreVelocityForPenetration();
            return;
        }

        int loserRank = Mathf.Min(selfRank, otherRank);
        int winnerRank = Mathf.Max(selfRank, otherRank);

        if (loserRank == 2)
        {
            // 反射していないドリルが、反射したドリルに負けた：③砕け散る演出（DrillFXが消える時に自動で出す）。旧VFXは出さない（消滅SEは鳴らす）
            loser.DestroyByBulletContact(playVfx: false);
        }
        else if (winnerRank == 2)
        {
            // 反射弾が、反射していないドリルに負けた：線に刺さった時と同じヒット演出とSE（PaddleDot.PlayPinnedHitTickと同じ中身。線が無いので普通の線のSE）
            Vector3 hitPos = (winner.transform.position + loser.transform.position) * 0.5f;
            PaddleDrawer.Instance?.PlayPaddleHitSE(PaddleDot.LineType.Normal, false);
            if (winner.feedback != null) winner.feedback.PlayPaddleHitVfxOnly(hitPos);
            loser.DestroyByBulletContact(playFeedback: false);
        }
        else
        {
            // 反射弾が、反射したドリルに負けた：反射弾が消える時の従来の演出とSE
            loser.DestroyByBulletContact();
        }

        // 貫通する側は、物理エンジンに跳ね返される前の速さと向きに戻して飛び続ける（曲がるドリルは曲がる軌道のまま）
        if (!winner.isBeingDestroyed) winner.RestoreVelocityForPenetration();
    }

    private void TryApplyC2PenetrationVelocity()
    {
        if (c2PenetrationsRemaining == 0) return;
        if (c2PenetrationsRemaining > 0) c2PenetrationsRemaining--; // -1=無制限はそのまま

        RestoreVelocityForPenetration();
    }

    /// <summary>
    /// 貫通時に衝突前の速度へ強制的に復元する（C2スキルの貫通回数を消費しない版）。
    /// 反射済みドリル反射弾がC2スキルの有無に関わらず必ず未反射弾を貫通する仕様のために使う
    /// </summary>
    private void RestoreVelocityForPenetration()
    {
        if (rb != null) rb.linearVelocity = preCollisionVelocity;
        if (preCollisionVelocity.sqrMagnitude > 0.0001f)
        {
            direction = preCollisionVelocity.normalized;
            lastNonZeroDir = direction;
        }
    }

    /// <param name="playVfx">falseなら消滅VFXだけ出さない（SEは鳴らす。弾同士の衝突で新しい演出を出した時）</param>
    private void DestroyByBulletContact(bool playFeedback = true, bool playVfx = true)
    {
        if (isBeingDestroyed) return;

        if (playFeedback && feedback != null)
        {
            if (playVfx) feedback.PlayDisappearVfx(transform.position);
            feedback.PlayDestroySeOnce(transform.position);
        }
        isBeingDestroyed = true;
        ReleaseOrDestroySelf();
    }

    public void PlayDestroyFeedbackAndDestroy()
    {
        if (isBeingDestroyed) return;
        if (feedback != null)
        {
            feedback.PlayDisappearVfx(transform.position);
            feedback.PlayDestroySeOnce(transform.position);
        }
        isBeingDestroyed = true;
        ReleaseOrDestroySelf();
    }
}
