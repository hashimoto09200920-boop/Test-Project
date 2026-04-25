using UnityEngine;

/// <summary>
/// Turtle専用の左右転がり移動コンポーネント。
/// EnemyMoverを使わず、このコンポーネントで移動と回転を制御する。
/// HPが減るほど移動速度・回転速度が上がるエンレイジ機能付き。
/// </summary>
public class TurtleRoller : MonoBehaviour
{
    // =========================================================
    // Inspector 設定
    // =========================================================

    [Header("移動設定")]
    [Tooltip("片道の移動距離（Unity単位）")]
    [SerializeField] private float moveDistance = 3f;

    [Tooltip("片道の移動にかかる時間（秒）")]
    [SerializeField] private float moveDuration = 1.5f;

    [Tooltip("端で止まる時間（秒）")]
    [SerializeField] private float stopDuration = 0.8f;

    [Header("開始方向")]
    [Tooltip("ON: スポーン位置が中央より左なら右へ、右なら左へ転がる（画面中央=X:0を基準）\nOFF: 下の startGoingRight で手動設定")]
    [SerializeField] private bool usePositionBasedDirection = true;

    [Tooltip("usePositionBasedDirection=OFF のときに有効。チェックON=右から開始、OFF=左から開始")]
    [SerializeField] private bool startGoingRight = true;

    [Header("イージング")]
    [Tooltip("移動の加減速カーブ。横軸=進行割合(0→1)、縦軸=位置割合(0→1)")]
    [SerializeField] private AnimationCurve moveCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("回転設定")]
    [Tooltip("ONにするとspriteRadiusから自動計算（転がり感が出る）。OFFで手動設定。")]
    [SerializeField] private bool autoCalcRotation = true;

    [Tooltip("自動計算用のスプライトの見た目の半径（Unity単位）。スプライトの表示サイズの半分程度を設定。")]
    [SerializeField] private float spriteRadius = 0.5f;

    [Tooltip("手動設定時の回転速度（度/秒）。autoCalcRotation=OFFのときに有効。")]
    [SerializeField] private float manualRotationSpeed = 180f;

    [Header("エンレイジ設定（HP減少で加速）")]
    [Tooltip("HP0になったときの速度倍率。1.0で無効（加速なし）。")]
    [SerializeField] private float enrageMaxSpeedMultiplier = 2.5f;

    // =========================================================
    // Runtime 変数
    // =========================================================
    private float originX;          // スポーン位置のX（折り返し基準）
    private float fromX;            // 現在の移動の開始X
    private float toX;              // 現在の移動の目標X
    private float moveTimer;
    private float stopTimer;
    private bool isMoving = false;
    private int direction;
    private float currentZAngle = 0f;

    private EnemyStats enemyStats;

    // =========================================================
    // Unity ライフサイクル
    // =========================================================
    private void Start()
    {
        enemyStats = GetComponent<EnemyStats>();

        if (usePositionBasedDirection)
            direction = (transform.position.x <= 0f) ? 1 : -1;  // 左側なら右へ、右側なら左へ
        else
            direction = startGoingRight ? 1 : -1;

        originX = transform.position.x;
        fromX = originX;
        toX = originX + moveDistance * direction;

        // 最初は停止状態から開始
        isMoving = false;
        stopTimer = 0f;
    }

    private void Update()
    {
        float speedMult = GetEnrageSpeedMultiplier();

        if (isMoving)
            UpdateMoving(speedMult);
        else
            UpdateStopped(speedMult);
    }

    // =========================================================
    // 移動中の更新
    // =========================================================
    private void UpdateMoving(float speedMult)
    {
        float adjustedDuration = moveDuration / speedMult;
        moveTimer += Time.deltaTime * GetTimeScale();
        float t = Mathf.Clamp01(moveTimer / adjustedDuration);
        float easedT = moveCurve.Evaluate(t);

        float prevX = transform.position.x;
        float newX = Mathf.Lerp(fromX, toX, easedT);
        transform.position = new Vector3(newX, transform.position.y, transform.position.z);

        // 回転（移動量に応じて転がる）
        float deltaX = newX - prevX;
        float rotDelta;
        if (autoCalcRotation && spriteRadius > 0f)
        {
            // 転がり: 移動距離 / 半径 = 回転角度（ラジアン）
            rotDelta = -(deltaX / spriteRadius) * Mathf.Rad2Deg;
        }
        else
        {
            rotDelta = -Mathf.Sign(deltaX) * manualRotationSpeed * Time.deltaTime * GetTimeScale() * speedMult;
        }
        currentZAngle += rotDelta;
        transform.rotation = Quaternion.Euler(0f, 0f, currentZAngle);

        if (t >= 1f)
        {
            // 移動完了 → 停止へ
            isMoving = false;
            stopTimer = 0f;
            direction = -direction;
            fromX = transform.position.x;
            toX = fromX + moveDistance * direction;
        }
    }

    // =========================================================
    // 停止中の更新
    // =========================================================
    private void UpdateStopped(float speedMult)
    {
        float adjustedStop = stopDuration / speedMult;
        stopTimer += Time.deltaTime * GetTimeScale();
        if (stopTimer >= adjustedStop)
        {
            isMoving = true;
            moveTimer = 0f;
        }
    }

    // =========================================================
    // エンレイジ：HPが低いほど速度が上がる
    // =========================================================
    private float GetTimeScale() =>
        SlowMotionManager.Instance != null ? SlowMotionManager.Instance.TimeScale : 1f;

    private float GetEnrageSpeedMultiplier()
    {
        if (enemyStats == null || enemyStats.MaxHP <= 0) return 1f;
        float hpRatio = (float)enemyStats.HP / enemyStats.MaxHP; // 1.0(全快) → 0.0(瀕死)
        float t = 1f - hpRatio; // 0.0(全快) → 1.0(瀕死)
        return Mathf.Lerp(1f, enrageMaxSpeedMultiplier, t);
    }
}
