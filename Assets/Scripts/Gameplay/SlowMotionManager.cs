using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// スローモーションアクティブスキルのコアシステム
/// カスタムタイムスケールを管理し、スローモーション中のみ効果を発揮
/// </summary>
public class SlowMotionManager : MonoBehaviour
{
    public static SlowMotionManager Instance { get; private set; }

    [Header("Slow Motion Settings")]
    [Tooltip("スローモーション倍率（0.2 = 20%の速度）")]
    [SerializeField] private float slowMotionTimeScale = 0.2f;

    [Tooltip("最大効果時間（秒）- ベース値")]
    [SerializeField] private float baseMaxDuration = 3f;

    [Header("Recovery Settings")]
    [Tooltip("通常回復速度（秒/秒）- スローモーション外での回復速度 - ベース値")]
    [SerializeField] private float baseNormalRecoveryRate = 0.1f;

    [Tooltip("ペナルティ遅延時間（秒）- 使い切った場合の待機時間")]
    [SerializeField] private float penaltyDelay = 3f;

    [Tooltip("ペナルティ時の遅い回復速度（秒/秒）")]
    [SerializeField] private float penaltyRecoveryRate = 0.05f;

    [Header("Visual Effects")]
    [Tooltip("スローモーション中の色調変更（HDR対応）")]
    [SerializeField] private Color slowMotionTint = new Color(0.7f, 0.9f, 1.2f, 1f);

    [Tooltip("スローモーション中のビネット強度（0-1）")]
    [SerializeField, Range(0f, 1f)] private float vignetteIntensity = 0.3f;

    [Tooltip("スローモーション中のクロマティックアバレーション強度（0-1）")]
    [SerializeField, Range(0f, 1f)] private float chromaticAberrationIntensity = 0.5f;

    [Header("Audio")]
    [Tooltip("スローモーション開始SE")]
    [SerializeField] private AudioClip slowMotionStartClip;

    [Tooltip("スローモーション終了SE")]
    [SerializeField] private AudioClip slowMotionEndClip;

    [Tooltip("使い切った時のSE")]
    [SerializeField] private AudioClip depletedClip;

    [Tooltip("回復開始SE")]
    [SerializeField] private AudioClip recoveryStartClip;

    [Tooltip("SE音量")]
    [SerializeField, Range(0f, 1f)] private float seVolume = 1f;

    [Header("References")]
    [SerializeField] private AudioSource audioSource;

    [SerializeField] private Volume postProcessVolume;

    // Runtime state
    private bool isSlowMotionActive = false;
    private float currentDuration; // 残り効果時間
    private bool isDepleted = false; // 使い切ったかどうか
    private float penaltyTimer = 0f; // ペナルティタイマー

    // Skill bonus values (スキルによる加算値)
    private float maxDurationBonus = 0f;
    private float normalRecoveryRateBonus = 0f;

    // Post-processing components
    private ColorAdjustments colorAdjustments;
    private Vignette vignette;
    private ChromaticAberration chromaticAberration;

    // Public properties
    public bool IsSlowMotionActive => isSlowMotionActive;
    public float CurrentDuration => currentDuration;
    public float MaxDuration => baseMaxDuration + maxDurationBonus;
    public float NormalizedDuration => MaxDuration > 0 ? currentDuration / MaxDuration : 0f;
    public float NormalRecoveryRate => baseNormalRecoveryRate + normalRecoveryRateBonus;

    /// <summary>
    /// カスタムタイムスケール（スローモーション中のみ0.2、それ以外は1.0）
    /// </summary>
    public float TimeScale => isSlowMotionActive ? slowMotionTimeScale : 1f;

    private void Awake()
    {
        // Singleton setup
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Initialize
        currentDuration = MaxDuration; // プロパティを使用（ベース値+ボーナス）
        isDepleted = false;
        penaltyTimer = 0f;

        // Get or create AudioSource
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
        }

        // Get post-processing components
        if (postProcessVolume != null && postProcessVolume.profile != null)
        {
            postProcessVolume.profile.TryGet(out colorAdjustments);
            postProcessVolume.profile.TryGet(out vignette);
            postProcessVolume.profile.TryGet(out chromaticAberration);
        }
    }

    private void Update()
    {
        if (isSlowMotionActive)
        {
            UpdateSlowMotion();
        }
        else
        {
            UpdateRecovery();
        }
    }

    /// <summary>
    /// スローモーションを開始/終了トグル
    /// </summary>
    public void ToggleSlowMotion()
    {
        if (isSlowMotionActive)
        {
            // スローモーション中 → 終了
            StopSlowMotion();
        }
        else
        {
            // スローモーション外 → 開始
            if (currentDuration > 0f)
            {
                StartSlowMotion();
            }
        }
    }

    /// <summary>
    /// スローモーション開始
    /// </summary>
    private void StartSlowMotion()
    {
        if (currentDuration <= 0f) return;

        isSlowMotionActive = true;

        // SE再生
        PlaySound(slowMotionStartClip);

        // 画面効果適用
        ApplyVisualEffects(true);

        Debug.Log($"[SlowMotionManager] Slow motion started. TimeScale: {TimeScale}");
    }

    /// <summary>
    /// スローモーション終了
    /// </summary>
    private void StopSlowMotion()
    {
        isSlowMotionActive = false;

        // SE再生
        PlaySound(slowMotionEndClip);

        // 画面効果解除
        ApplyVisualEffects(false);

        // 使い切った場合の処理
        if (currentDuration <= 0f && !isDepleted)
        {
            isDepleted = true;
            penaltyTimer = penaltyDelay;
            PlaySound(depletedClip);
            Debug.Log($"[SlowMotionManager] Depleted! Penalty delay: {penaltyDelay}s");
        }

        Debug.Log($"[SlowMotionManager] Slow motion stopped. TimeScale: {TimeScale}");
    }

    /// <summary>
    /// スローモーション中の更新
    /// </summary>
    private void UpdateSlowMotion()
    {
        // 効果時間を減らす（実時間で減少）
        currentDuration -= Time.deltaTime;

        if (currentDuration <= 0f)
        {
            currentDuration = 0f;
            StopSlowMotion();
        }
    }

    /// <summary>
    /// 回復処理
    /// </summary>
    private void UpdateRecovery()
    {
        // ペナルティタイマー処理
        if (isDepleted && penaltyTimer > 0f)
        {
            penaltyTimer -= Time.deltaTime;

            if (penaltyTimer <= 0f)
            {
                penaltyTimer = 0f;
                PlaySound(recoveryStartClip);
                Debug.Log("[SlowMotionManager] Penalty ended, recovery started");
            }

            return; // ペナルティ中は回復しない
        }

        // 回復処理
        if (currentDuration < MaxDuration)
        {
            float recoveryRate = isDepleted ? penaltyRecoveryRate : NormalRecoveryRate;
            currentDuration += recoveryRate * Time.deltaTime;

            if (currentDuration >= MaxDuration)
            {
                currentDuration = MaxDuration;
                isDepleted = false; // 完全回復したらペナルティ解除
            }
        }
    }

    /// <summary>
    /// 画面効果の適用/解除
    /// </summary>
    private void ApplyVisualEffects(bool enable)
    {
        if (postProcessVolume == null) return;

        if (enable)
        {
            // 色調変更
            if (colorAdjustments != null)
            {
                colorAdjustments.colorFilter.value = slowMotionTint;
                colorAdjustments.active = true;
            }

            // ビネット
            if (vignette != null)
            {
                vignette.intensity.value = vignetteIntensity;
                vignette.active = true;
            }

            // クロマティックアバレーション
            if (chromaticAberration != null)
            {
                chromaticAberration.intensity.value = chromaticAberrationIntensity;
                chromaticAberration.active = true;
            }
        }
        else
        {
            // 効果を元に戻す
            if (colorAdjustments != null)
            {
                colorAdjustments.colorFilter.value = Color.white;
                colorAdjustments.active = false;
            }

            if (vignette != null)
            {
                vignette.intensity.value = 0f;
                vignette.active = false;
            }

            if (chromaticAberration != null)
            {
                chromaticAberration.intensity.value = 0f;
                chromaticAberration.active = false;
            }
        }
    }

    /// <summary>
    /// SE再生
    /// </summary>
    private void PlaySound(AudioClip clip)
    {
        if (clip != null && audioSource != null)
        {
            audioSource.PlayOneShot(clip, seVolume);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    // ===== Skill System Integration =====

    /// <summary>
    /// スキルによる最大持続時間ボーナスを追加
    /// </summary>
    public void AddMaxDurationBonus(float bonus)
    {
        maxDurationBonus += bonus;

        // 現在のゲージ量もボーナス分増やす（スキル取得時に即座に反映）
        currentDuration += bonus;

        // MaxDurationを超えないようにClamp
        if (currentDuration > MaxDuration)
        {
            currentDuration = MaxDuration;
        }

        Debug.Log($"[SlowMotionManager] MaxDuration bonus added: +{bonus}s (Total MaxDuration: {MaxDuration}s, Current: {currentDuration:F2}s)");
    }

    /// <summary>
    /// スキルによる通常回復速度ボーナスを追加
    /// </summary>
    public void AddNormalRecoveryRateBonus(float bonus)
    {
        normalRecoveryRateBonus += bonus;
        Debug.Log($"[SlowMotionManager] NormalRecoveryRate bonus added: +{bonus} (Total: {NormalRecoveryRate} sec/sec)");
    }

    /// <summary>
    /// スキルボーナスをリセット（SkillManagerから呼ばれる）
    /// </summary>
    public void ResetSkillBonuses()
    {
        maxDurationBonus = 0f;
        normalRecoveryRateBonus = 0f;

        // ★リセット後のMaxDurationに合わせてcurrentDurationを調整
        if (currentDuration > MaxDuration)
        {
            currentDuration = MaxDuration;
        }

        Debug.Log($"[SlowMotionManager] Skill bonuses reset. MaxDuration: {MaxDuration}s, Current: {currentDuration:F2}s");
    }

#if UNITY_EDITOR
    /// <summary>
    /// Inspector表示用のデバッグ情報
    /// </summary>
    [ContextMenu("Debug Info")]
    private void DebugInfo()
    {
        Debug.Log($"[SlowMotionManager] Active: {isSlowMotionActive}, Duration: {currentDuration:F2}/{MaxDuration:F2}, TimeScale: {TimeScale}, Depleted: {isDepleted}, Penalty: {penaltyTimer:F2}");
    }
#endif
}
