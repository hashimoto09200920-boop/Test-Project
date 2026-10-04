using UnityEngine;

/// <summary>
/// NeonDancer（Area10最終ボス）のFloor/Light共通「バリア」付属コンポーネント。
/// 被弾・破壊（すり抜け化）・破壊SE/VFXは同じGameObjectの既存WallHealthにそのまま任せ、
/// ここでは「破壊後、一定時間で全快する」「破壊中の暗色化」「破壊/復活の通知」だけを担当する。
/// ★Block判定をWallHealthに任せる理由：反射Beam・爆発はWallHealth以外のColliderに当たると親を辿って
///   EnemyDamageReceiverを探すため、独自部品にするとFloor越しにDancer本体へダメージが通ってしまう。
/// ★WallHealth側は disableRendererOnBreak=false（見た目は消さない）、disableColliderOnBreak=true（すり抜け化）にする。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(WallHealth))]
public class NeonDancerBarrier : MonoBehaviour
{
    [Header("Recovery")]
    [Tooltip("破壊されてから全快するまでの秒数（スローモーション中はその分ゆっくり進む）")]
    [SerializeField] private float fullRecoveryTime = 30f;

    [Header("Break Visual")]
    [Tooltip("ON: 破壊中はSpriteRendererの色を暗くする（Floor用）")]
    [SerializeField] private bool darkenOnBreak = false;
    [Tooltip("暗くするSpriteRenderer（未指定なら同じGameObjectから自動取得）")]
    [SerializeField] private SpriteRenderer targetRenderer;
    [Tooltip("破壊中に元の色へ乗算する色")]
    [SerializeField] private Color brokenColorMultiplier = new Color(0.35f, 0.35f, 0.35f, 1f);

    [Header("Restore SE（任意）")]
    [Tooltip("全快した瞬間に鳴らすSE。未設定なら鳴らない")]
    [SerializeField] private AudioClip restoreSE;
    [Range(0f, 1f)] [SerializeField] private float restoreSEVolume = 1f;

    /// <summary>Block HPが0になった瞬間に発火</summary>
    public event System.Action OnBarrierBroken;
    /// <summary>全快した瞬間に発火（自動回復・ResetToFullの両方）</summary>
    public event System.Action OnBarrierRestored;

    public bool IsBroken => isBroken;
    /// <summary>破壊中の全快までの進行度（0〜1）。未破壊時は1</summary>
    public float RecoveryProgress => isBroken && fullRecoveryTime > 0f ? Mathf.Clamp01(recoveryTimer / fullRecoveryTime) : 1f;

    private WallHealth wallHealth;
    private bool isBroken;
    private float recoveryTimer;
    private Color originalColor = Color.white;

    private static float TimeScale => SlowMotionManager.Instance != null ? SlowMotionManager.Instance.TimeScale : 1f;
    private static float MasterSEVolume => SoundSettingsManager.Instance != null ? SoundSettingsManager.Instance.SEVolume : 1f;

    private void Awake()
    {
        wallHealth = GetComponent<WallHealth>();
        if (targetRenderer == null) targetRenderer = GetComponent<SpriteRenderer>();
        if (targetRenderer != null) originalColor = targetRenderer.color;
        wallHealth.OnBroken += HandleBroken;
    }

    private void OnDestroy()
    {
        if (wallHealth != null) wallHealth.OnBroken -= HandleBroken;
    }

    private void Update()
    {
        if (!isBroken) return;
        recoveryTimer += Time.deltaTime * TimeScale;
        if (recoveryTimer >= fullRecoveryTime)
            Restore();
    }

    private void HandleBroken(Vector3 hitPoint)
    {
        isBroken = true;
        recoveryTimer = 0f;
        if (darkenOnBreak && targetRenderer != null)
            targetRenderer.color = originalColor * brokenColorMultiplier;
        OnBarrierBroken?.Invoke();
    }

    /// <summary>後半フェーズ移行時など、外部から即座に全快させる（未破壊ならHPだけ満タンに戻す）</summary>
    public void ResetToFull()
    {
        if (isBroken) Restore();
        else wallHealth.ResetHealth();
    }

    private void Restore()
    {
        wallHealth.ResetHealth();
        isBroken = false;
        recoveryTimer = 0f;
        if (darkenOnBreak && targetRenderer != null)
            targetRenderer.color = originalColor;

        if (restoreSE != null)
            AudioOneShotPool.Play(restoreSE, restoreSEVolume * MasterSEVolume, transform.position, null, 0.1f);

        OnBarrierRestored?.Invoke();
    }
}
