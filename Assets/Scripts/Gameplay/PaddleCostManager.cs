using UnityEngine;

public class PaddleCostManager : MonoBehaviour
{
    [Header("Left Line Cost (Length-based)")]
    [SerializeField] private float leftMaxCost = 20f;
    [SerializeField] private float leftRecoverPerSecond = 4f;
    [SerializeField] private float leftMinCostToDraw = 0.01f;

    [Header("Right Red Line Cost (Length-based)")]
    [SerializeField] private float redMaxCost = 6f;
    [SerializeField] private float redRecoverPerSecond = 2f;
    [SerializeField] private float redMinCostToDraw = 0.01f;

    [Header("Recover Pause While Holding")]
    [SerializeField] private bool pauseLeftRecoverWhileDrawingLeft = false;  // 基本OFF推奨（白線は描いてない時に回復）
    [SerializeField] private bool pauseRedRecoverWhileDrawingRed = true;     // 赤線は「描いてる間は回復しない」推奨

    [Header("Debug")]
    [SerializeField] private bool showLog = false;

    // ---- public props ----
    public float LeftMaxCost => leftMaxCost;
    public float LeftCurrentCost { get; private set; }
    public float LeftRecoverPerSecond => leftRecoverPerSecond;

    public float RedMaxCost => redMaxCost;
    public float RedCurrentCost { get; private set; }
    public float RedRecoverPerSecond => redRecoverPerSecond;

    // PaddleDrawer から「いま描画中か」を渡してもらう
    private bool isDrawingLeft;
    private bool isDrawingRed;

    private void Awake()
    {
        leftMaxCost = Mathf.Max(0f, leftMaxCost);
        redMaxCost = Mathf.Max(0f, redMaxCost);

        LeftCurrentCost = leftMaxCost;
        RedCurrentCost = redMaxCost;
    }

    private void Update()
    {
        // 回復は「描いていない時」が基本（赤線は pause をON推奨）
        bool canRecoverLeft = !pauseLeftRecoverWhileDrawingLeft || !isDrawingLeft;
        bool canRecoverRed = !pauseRedRecoverWhileDrawingRed || !isDrawingRed;

        if (canRecoverLeft) RecoverLeft(Time.deltaTime);
        if (canRecoverRed) RecoverRed(Time.deltaTime);
    }

    // PaddleDrawer から毎フレーム呼ぶ
    public void SetDrawingState(bool drawingLeft, bool drawingRed)
    {
        isDrawingLeft = drawingLeft;
        isDrawingRed = drawingRed;
    }

    // ---------- Left ----------
    private void RecoverLeft(float dt)
    {
        if (leftRecoverPerSecond <= 0f) return;

        LeftCurrentCost += leftRecoverPerSecond * dt;
        if (LeftCurrentCost > leftMaxCost) LeftCurrentCost = leftMaxCost;
    }

    public bool CanConsumeLeft(float length)
    {
        if (length <= 0f) return LeftCurrentCost >= leftMinCostToDraw;
        return LeftCurrentCost >= length && LeftCurrentCost >= leftMinCostToDraw;
    }

    public bool TryConsumeLeft(float length)
    {
        length = Mathf.Max(0f, length);

        if (!CanConsumeLeft(length)) return false;

        LeftCurrentCost -= length;
        if (LeftCurrentCost < 0f) LeftCurrentCost = 0f;

        if (showLog)
        {
            Debug.Log($"[PaddleCost:Left] -{length:0.00} => {LeftCurrentCost:0.00}/{leftMaxCost:0.00}");
        }

        return true;
    }

    // ---------- Red ----------
    private void RecoverRed(float dt)
    {
        if (redRecoverPerSecond <= 0f) return;

        RedCurrentCost += redRecoverPerSecond * dt;
        if (RedCurrentCost > redMaxCost) RedCurrentCost = redMaxCost;
    }

    public bool CanConsumeRed(float length)
    {
        if (length <= 0f) return RedCurrentCost >= redMinCostToDraw;
        return RedCurrentCost >= length && RedCurrentCost >= redMinCostToDraw;
    }

    public bool TryConsumeRed(float length)
    {
        length = Mathf.Max(0f, length);

        if (!CanConsumeRed(length)) return false;

        RedCurrentCost -= length;
        if (RedCurrentCost < 0f) RedCurrentCost = 0f;

        if (showLog)
        {
            Debug.Log($"[PaddleCost:Red] -{length:0.00} => {RedCurrentCost:0.00}/{redMaxCost:0.00}");
        }

        return true;
    }

    // =========================================================
    // Skill System Setters
    // =========================================================

    /// <summary>
    /// 白線の最大値を設定（スキルシステム用）
    /// </summary>
    public void SetLeftMaxCost(float value)
    {
        leftMaxCost = Mathf.Max(0f, value);
        // 現在値が最大値を超えている場合は調整
        if (LeftCurrentCost > leftMaxCost)
        {
            LeftCurrentCost = leftMaxCost;
        }
    }

    /// <summary>
    /// 赤線の最大値を設定（スキルシステム用）
    /// </summary>
    public void SetRedMaxCost(float value)
    {
        redMaxCost = Mathf.Max(0f, value);
        // 現在値が最大値を超えている場合は調整
        if (RedCurrentCost > redMaxCost)
        {
            RedCurrentCost = redMaxCost;
        }
    }

    /// <summary>
    /// 白線の回復量を設定（スキルシステム用）
    /// </summary>
    public void SetLeftRecoverPerSecond(float value)
    {
        leftRecoverPerSecond = Mathf.Max(0f, value);
    }

    /// <summary>
    /// 赤線の回復量を設定（スキルシステム用）
    /// </summary>
    public void SetRedRecoverPerSecond(float value)
    {
        redRecoverPerSecond = Mathf.Max(0f, value);
    }
}
