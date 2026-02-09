using UnityEngine;
using System;

/// <summary>
/// 敵のシールドシステム
/// - シールドはHPとは別の耐久値で、先に消費される
/// - 破壊されていない場合: 一定時間後に徐々に回復（被弾でリセット）
/// - 破壊された場合: 一定時間後に全回復（被弾でリセットされない）
/// - EnemyData から設定を読み取る
/// </summary>
public class EnemyShield : MonoBehaviour
{
    // ===== 設定値（EnemyData から適用される） =====
    private bool enableShield;
    private float shieldPercentage;
    private float gradualRecoveryDelay;
    private float gradualRecoveryRate;
    private float fullRecoveryTime;
    private GameObject shieldBreakEffectPrefab;
    private GameObject shieldActiveEffectPrefab;
    private float effectDestroySeconds;
    private AudioClip shieldBreakSeClip;
    private AudioClip shieldRestoreSeClip;
    private float seVolume;

    // ===== イベント =====
    /// <summary>シールドが破壊された時に発火</summary>
    public event Action OnShieldBroken;

    /// <summary>シールドが全回復した時に発火</summary>
    public event Action OnShieldRestored;

    // ===== 状態 =====
    private int maxShield;
    private int currentShield;
    private bool isBroken;

    private float gradualRecoveryTimer;
    private float fullRecoveryTimer;
    private float accumulatedRecovery; // 累積回復量（小数点以下を追跡）

    private GameObject activeEffectInstance;

    // ===== プロパティ =====
    public bool IsEnabled => enableShield;
    public int CurrentShield => currentShield;
    public int MaxShield => maxShield;
    public bool IsBroken => isBroken;
    /// <summary>シールド回復進行度（0.0～1.0）。破壊状態の場合は全回復までの進行度を返す</summary>
    public float RecoveryProgress => (isBroken && fullRecoveryTime > 0) ? Mathf.Clamp01(fullRecoveryTimer / fullRecoveryTime) : 1f;

    private EnemyStats stats;

    private void Awake()
    {
        stats = GetComponent<EnemyStats>();
    }

    /// <summary>
    /// EnemyData からシールド設定を適用する
    /// </summary>
    public void ApplyShieldData(EnemyData data)
    {
        if (data == null) return;

        // 設定を適用
        enableShield = data.enableShield;
        shieldPercentage = data.shieldPercentage;
        gradualRecoveryDelay = data.gradualRecoveryDelay;
        gradualRecoveryRate = data.gradualRecoveryRate;
        fullRecoveryTime = data.fullRecoveryTime;
        shieldBreakEffectPrefab = data.shieldBreakEffectPrefab;
        shieldActiveEffectPrefab = data.shieldActiveEffectPrefab;
        effectDestroySeconds = data.shieldEffectDestroySeconds;
        shieldBreakSeClip = data.shieldBreakSeClip;
        shieldRestoreSeClip = data.shieldRestoreSeClip;
        seVolume = data.shieldSeVolume;

        // ★スキルによるシールド回復時間遅延を適用
        if (Game.Skills.SkillManager.Instance != null)
        {
            float delayMultiplier = Game.Skills.SkillManager.Instance.GetShieldRecoveryDelayMultiplier();
            gradualRecoveryDelay *= delayMultiplier;
            fullRecoveryTime *= delayMultiplier;
        }

        if (!enableShield) return;

        // maxShieldをHPから計算
        if (stats != null)
        {
            maxShield = Mathf.Max(1, Mathf.RoundToInt(stats.MaxHP * shieldPercentage));
            currentShield = maxShield;
        }

        // バリアエフェクトを生成
        if (shieldActiveEffectPrefab != null)
        {
            activeEffectInstance = Instantiate(shieldActiveEffectPrefab, transform.position, Quaternion.identity, transform);
        }
    }

    private void Update()
    {
        if (!enableShield) return;

        // バリアエフェクトの表示/非表示
        if (activeEffectInstance != null)
        {
            activeEffectInstance.SetActive(currentShield > 0);
        }

        if (isBroken)
        {
            // ②全回復処理（被弾でリセットされない）
            fullRecoveryTimer += Time.deltaTime;
            if (fullRecoveryTimer >= fullRecoveryTime)
            {
                RestoreFullShield();
            }
        }
        else
        {
            // ①徐々に回復処理（被弾でリセット）
            if (currentShield < maxShield)
            {
                gradualRecoveryTimer += Time.deltaTime;
                if (gradualRecoveryTimer >= gradualRecoveryDelay)
                {
                    // 毎秒回復（累積方式）
                    accumulatedRecovery += gradualRecoveryRate * Time.deltaTime;

                    // 累積が1以上になったら実際に回復
                    if (accumulatedRecovery >= 1f)
                    {
                        int recoveryPoints = Mathf.FloorToInt(accumulatedRecovery);
                        currentShield = Mathf.Min(maxShield, currentShield + recoveryPoints);
                        accumulatedRecovery -= recoveryPoints; // 整数部分を消費
                    }
                }
            }
            else
            {
                // シールドが満タンならタイマーと累積をリセット
                gradualRecoveryTimer = 0f;
                accumulatedRecovery = 0f;
            }
        }
    }

    /// <summary>
    /// シールドにダメージを与える
    /// </summary>
    /// <param name="damage">ダメージ量</param>
    /// <returns>シールドで吸収できなかった残りのダメージ</returns>
    public int ApplyDamage(int damage)
    {
        if (!enableShield || currentShield <= 0)
        {
            return damage; // シールドなし → 全ダメージをHPに
        }

        // ★スキルによるシールドダメージ倍率を適用（反射弾→シールドのみ）
        int shieldDamage = damage;
        if (Game.Skills.SkillManager.Instance != null)
        {
            float shieldDmgMul = Game.Skills.SkillManager.Instance.GetShieldDamageMultiplier();
            shieldDamage = Mathf.Max(1, Mathf.RoundToInt(damage * shieldDmgMul));
        }

        // 被弾したので徐々に回復タイマーと累積をリセット
        if (!isBroken)
        {
            gradualRecoveryTimer = 0f;
            accumulatedRecovery = 0f;
        }

        int remainingDamage = 0;

        if (shieldDamage >= currentShield)
        {
            // シールド破壊
            remainingDamage = damage - currentShield; // ★HPへのダメージは元のダメージから計算
            currentShield = 0;

            if (!isBroken)
            {
                BreakShield();
            }
        }
        else
        {
            // シールドでダメージ吸収
            currentShield -= shieldDamage;
        }

        return remainingDamage;
    }

    private void BreakShield()
    {
        isBroken = true;
        fullRecoveryTimer = 0f;

        // エフェクト
        if (shieldBreakEffectPrefab != null)
        {
            GameObject effect = Instantiate(shieldBreakEffectPrefab, transform.position, Quaternion.identity);
            if (effectDestroySeconds > 0f)
            {
                Destroy(effect, effectDestroySeconds);
            }
        }

        // SE
        if (shieldBreakSeClip != null)
        {
            AudioSource.PlayClipAtPoint(shieldBreakSeClip, transform.position, seVolume);
        }

        // イベント発火
        OnShieldBroken?.Invoke();
    }

    private void RestoreFullShield()
    {
        currentShield = maxShield;
        isBroken = false;
        fullRecoveryTimer = 0f;
        gradualRecoveryTimer = 0f;
        accumulatedRecovery = 0f;

        // SE
        if (shieldRestoreSeClip != null)
        {
            AudioSource.PlayClipAtPoint(shieldRestoreSeClip, transform.position, seVolume);
        }

        // イベント発火
        OnShieldRestored?.Invoke();
    }

    private void OnDestroy()
    {
        // バリアエフェクトを削除
        if (activeEffectInstance != null)
        {
            Destroy(activeEffectInstance);
        }
    }
}
