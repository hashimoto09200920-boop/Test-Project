using UnityEngine;

/// <summary>
/// Droneの動きアニメーションコンポーネント。
/// 楕円ドリフト（X/Y別周波数のsin波）＋連続スピン（時計回り⇔反時計回り交互）。
/// Play前のInspectorで全パラメータ調整可能。
/// </summary>
public class DroneAnimation : MonoBehaviour
{
    [Header("Drift（楕円ドリフト）")]
    [Tooltip("X方向の移動幅（ワールド単位）。例: 0.2 = ±0.2動く")]
    [Range(0f, 2f)]
    public float driftAmplitudeX = 0.2f;

    [Tooltip("Y方向の移動幅（ワールド単位）。例: 0.15 = ±0.15動く")]
    [Range(0f, 2f)]
    public float driftAmplitudeY = 0.15f;

    [Tooltip("X方向のドリフト周波数（1秒あたりの往復回数）")]
    [Range(0.1f, 5f)]
    public float driftFrequencyX = 0.7f;

    [Tooltip("Y方向のドリフト周波数（1秒あたりの往復回数）。Xと差をつけると楕円になる")]
    [Range(0.1f, 5f)]
    public float driftFrequencyY = 1.1f;

    [Header("Spin（連続スピン）")]
    [Tooltip("回転速度（度/秒）")]
    [Range(0f, 720f)]
    public float spinSpeed = 120f;

    [Tooltip("1方向に回転し続ける時間（秒）。経過後に逆回転に切り替わる")]
    [Range(0.1f, 10f)]
    public float spinDuration = 1.5f;

    [Tooltip("方向転換時の揺り戻し時間（秒）。0にするとすぐ切り替わる")]
    [Range(0f, 1f)]
    public float spinReverseBlend = 0.2f;

    [Header("Random Start Phase")]
    [Tooltip("ON: 開始位相をランダムにする（複数Droneが同じ動きにならないように）")]
    public bool useRandomStartPhase = true;

    private EnemyMover enemyMover;
    private float startPhase;
    private Vector3 driftOffset = Vector3.zero;
    private float accumulatedTime;

    private float spinTimer;
    private float currentSpinDirection = 1f; // 1=時計回り, -1=反時計回り
    private float currentAngle;
    private float blendTimer;
    private bool isBlending;

    private float GetTimeScale() =>
        SlowMotionManager.Instance != null ? SlowMotionManager.Instance.TimeScale : 1f;

    private void Start()
    {
        enemyMover = GetComponentInParent<EnemyMover>();
        startPhase = useRandomStartPhase ? Random.Range(0f, Mathf.PI * 2f) : 0f;
        accumulatedTime = startPhase;
        spinTimer = useRandomStartPhase ? Random.Range(0f, spinDuration) : 0f;
        currentSpinDirection = Random.value < 0.5f ? 1f : -1f;
        currentAngle = 0f;
    }

    private void Update()
    {
        float slowMul = (enemyMover != null) ? enemyMover.SpeedMultiplier : 1f;
        accumulatedTime += Time.deltaTime * GetTimeScale() * slowMul;
        float t = accumulatedTime;

        // 楕円ドリフト（差分ベース）
        transform.localPosition -= driftOffset;
        float newX = Mathf.Sin(t * driftFrequencyX * Mathf.PI * 2f) * driftAmplitudeX;
        float newY = Mathf.Sin(t * driftFrequencyY * Mathf.PI * 2f) * driftAmplitudeY;
        driftOffset = new Vector3(newX, newY, 0f);
        transform.localPosition += driftOffset;

        // 連続スピン＋方向転換
        UpdateSpin(slowMul);
    }

    private void UpdateSpin(float slowMul)
    {
        float dt = Time.deltaTime * GetTimeScale() * slowMul;
        spinTimer += dt;

        if (isBlending)
        {
            // 方向転換中: 速度をゼロに落としてから逆方向へ
            blendTimer += dt;
            float blendT = spinReverseBlend > 0f ? Mathf.Clamp01(blendTimer / spinReverseBlend) : 1f;
            // 前半で減速、後半で加速（反転方向へ）
            float blendedSpeed = spinSpeed * Mathf.Lerp(-1f, 1f, blendT) * currentSpinDirection;
            currentAngle += blendedSpeed * dt;

            if (blendTimer >= spinReverseBlend)
            {
                isBlending = false;
                spinTimer = 0f;
            }
        }
        else
        {
            currentAngle += spinSpeed * currentSpinDirection * dt;

            if (spinTimer >= spinDuration)
            {
                currentSpinDirection = -currentSpinDirection;
                if (spinReverseBlend > 0f)
                {
                    isBlending = true;
                    blendTimer = 0f;
                }
                else
                {
                    spinTimer = 0f;
                }
            }
        }

        transform.localRotation = Quaternion.Euler(0f, 0f, currentAngle);
    }

    private void OnDestroy()
    {
        transform.localPosition -= driftOffset;
        driftOffset = Vector3.zero;
    }
}
