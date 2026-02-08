using UnityEngine;

/// <summary>
/// 敵のデバッグ情報表示（ルーチン、移動情報など）
/// ※Shield/HP表示は EnemyHealthDisplay.cs に実装
/// </summary>
[RequireComponent(typeof(EnemyStats))]
public class EnemyHpDebugText : MonoBehaviour
{
    [Header("Debug Display Settings")]
    [Tooltip("デバッグ情報の表示位置オフセット")]
    [SerializeField] private Vector3 offset = new Vector3(0f, 0.6f, 0f);
    [Tooltip("デバッグテキストのフォントサイズ")]
    [SerializeField] private int fontSize = 60;

    [Header("Routine Debug Display")]
    [Tooltip("ON: 行動ルーチンのデバッグ情報（RoutineType/Element）を表示する。OFF: 非表示")]
    [SerializeField] private bool showRoutineDebug = true;
    [Tooltip("デバッグテキストの右方向オフセット")]
    [SerializeField] private float routineDebugOffsetX = 1.5f;

    private EnemyShooter shooter;
    private EnemyMover mover;

    private TextMesh routineDebugTextMesh;
    private GameObject routineDebugTextObject;
    private TextMesh moveDebugTextMesh;
    private GameObject moveDebugTextObject;
    private TextMesh distanceDebugTextMesh;
    private GameObject distanceDebugTextObject;

    private void Awake()
    {
        shooter = GetComponent<EnemyShooter>();
        mover = GetComponent<EnemyMover>();

        // ===== 行動ルーチンデバッグテキスト =====
        routineDebugTextObject = new GameObject("RoutineDebug_Text");
        routineDebugTextObject.transform.SetParent(transform);
        routineDebugTextObject.transform.localPosition = offset + new Vector3(routineDebugOffsetX, 0f, 0f);

        routineDebugTextMesh = routineDebugTextObject.AddComponent<TextMesh>();
        routineDebugTextMesh.anchor = TextAnchor.MiddleCenter;
        routineDebugTextMesh.alignment = TextAlignment.Center;
        routineDebugTextMesh.fontSize = fontSize;
        routineDebugTextMesh.characterSize = 0.05f;
        routineDebugTextMesh.color = Color.yellow;
        routineDebugTextMesh.text = "";

        // ===== 移動ルーチンデバッグテキスト =====
        moveDebugTextObject = new GameObject("MoveDebug_Text");
        moveDebugTextObject.transform.SetParent(transform);
        moveDebugTextObject.transform.localPosition = offset + new Vector3(routineDebugOffsetX, -0.3f, 0f);

        moveDebugTextMesh = moveDebugTextObject.AddComponent<TextMesh>();
        moveDebugTextMesh.anchor = TextAnchor.MiddleCenter;
        moveDebugTextMesh.alignment = TextAlignment.Center;
        moveDebugTextMesh.fontSize = fontSize;
        moveDebugTextMesh.characterSize = 0.05f;
        moveDebugTextMesh.color = Color.cyan;
        moveDebugTextMesh.text = "";

        // ===== 移動距離デバッグテキスト =====
        distanceDebugTextObject = new GameObject("DistanceDebug_Text");
        distanceDebugTextObject.transform.SetParent(transform);
        distanceDebugTextObject.transform.localPosition = offset + new Vector3(routineDebugOffsetX, -0.6f, 0f);

        distanceDebugTextMesh = distanceDebugTextObject.AddComponent<TextMesh>();
        distanceDebugTextMesh.anchor = TextAnchor.MiddleCenter;
        distanceDebugTextMesh.alignment = TextAlignment.Center;
        distanceDebugTextMesh.fontSize = fontSize;
        distanceDebugTextMesh.characterSize = 0.05f;
        distanceDebugTextMesh.color = Color.magenta;
        distanceDebugTextMesh.text = "";

        // 初期表示状態を設定
        routineDebugTextObject.SetActive(showRoutineDebug);
        moveDebugTextObject.SetActive(false);  // 初期状態は非表示（EnemyDataの設定で制御）
        distanceDebugTextObject.SetActive(false);  // 初期状態は非表示（EnemyDataの設定で制御）
    }

    private void LateUpdate()
    {
        // ===== 行動ルーチンデバッグ情報を更新 =====
        if (showRoutineDebug && routineDebugTextMesh != null && shooter != null)
        {
            var info = shooter.GetDebugRoutineInfo();

            if (info.isUsingRoutine && info.routineType.HasValue)
            {
                string routineTypeStr = info.routineType.Value == EnemyData.BulletFiringRoutine.RoutineType.Sequence ? "Seq" : "Prob";
                string bulletTypeName = !string.IsNullOrEmpty(info.currentBulletTypeName) ? info.currentBulletTypeName : $"Type{info.currentElementIndex}";
                routineDebugTextMesh.text = $"{routineTypeStr} Elem:{info.currentElementIndex} ({bulletTypeName})";
            }
            else if (info.currentElementIndex >= 0)
            {
                string bulletTypeName = !string.IsNullOrEmpty(info.currentBulletTypeName) ? info.currentBulletTypeName : $"Type{info.currentElementIndex}";
                routineDebugTextMesh.text = $"Elem:{info.currentElementIndex} ({bulletTypeName})";
            }
            else
            {
                routineDebugTextMesh.text = "";
            }
        }
        else if (routineDebugTextMesh != null)
        {
            routineDebugTextMesh.text = "";
        }

        // ===== 移動ルーチンデバッグ情報を更新 =====
        if (moveDebugTextMesh != null && mover != null)
        {
            var moveInfo = mover.GetDebugMoveRoutineInfo();

            // デバッグ表示のON/OFFを確認（EnemyDataから取得）
            bool showMoveDebug = false;
            EnemyData enemyData = null;
            if (mover != null)
            {
                enemyData = mover.GetEnemyData();
            }
            else if (shooter != null)
            {
                enemyData = shooter.GetEnemyData();
            }

            if (enemyData != null && enemyData.moveTypes != null && moveInfo.currentElementIndex >= 0 &&
                moveInfo.currentElementIndex < enemyData.moveTypes.Length &&
                enemyData.moveTypes[moveInfo.currentElementIndex] != null)
            {
                showMoveDebug = enemyData.moveTypes[moveInfo.currentElementIndex].showDebugText;
            }

            moveDebugTextObject.SetActive(showMoveDebug);

            if (showMoveDebug)
            {
                if (moveInfo.isUsingRoutine && moveInfo.routineType.HasValue)
                {
                    string routineTypeStr = moveInfo.routineType.Value == EnemyData.MoveFiringRoutine.RoutineType.Sequence ? "MSeq" : "MProb";
                    string moveTypeName = !string.IsNullOrEmpty(moveInfo.currentMoveTypeName) ? moveInfo.currentMoveTypeName : $"Type{moveInfo.currentElementIndex}";
                    moveDebugTextMesh.text = $"{routineTypeStr} Elem:{moveInfo.currentElementIndex} ({moveTypeName})";
                }
                else if (moveInfo.currentElementIndex >= 0)
                {
                    string moveTypeName = !string.IsNullOrEmpty(moveInfo.currentMoveTypeName) ? moveInfo.currentMoveTypeName : $"Type{moveInfo.currentElementIndex}";
                    moveDebugTextMesh.text = $"MElem:{moveInfo.currentElementIndex} ({moveTypeName})";
                }
                else
                {
                    moveDebugTextMesh.text = "";
                }
            }
            else
            {
                moveDebugTextMesh.text = "";
            }
        }
        else if (moveDebugTextMesh != null)
        {
            moveDebugTextMesh.text = "";
        }

        // ===== 移動距離デバッグ情報を更新 =====
        if (distanceDebugTextMesh != null && mover != null)
        {
            var distanceInfo = mover.GetDebugDistanceInfo();

            // デバッグ表示のON/OFFを確認（EnemyDataから取得）
            bool showDistanceDebug = false;
            EnemyData enemyData = null;
            if (mover != null)
            {
                enemyData = mover.GetEnemyData();
            }

            // Horizontal/Vertical/Hopping/Warpパターンの場合のみ表示
            if ((distanceInfo.isHorizontalOrVertical || distanceInfo.isHopping || distanceInfo.isWarp) && enemyData != null && enemyData.moveTypes != null)
            {
                var moveInfo = mover.GetDebugMoveRoutineInfo();
                if (moveInfo.currentElementIndex >= 0 &&
                    moveInfo.currentElementIndex < enemyData.moveTypes.Length &&
                    enemyData.moveTypes[moveInfo.currentElementIndex] != null)
                {
                    showDistanceDebug = enemyData.moveTypes[moveInfo.currentElementIndex].showDebugText;
                }
            }

            distanceDebugTextObject.SetActive(showDistanceDebug);

            if (showDistanceDebug && distanceInfo.isHorizontalOrVertical)
            {
                // 表示例: "Dist: 2.5/+1.2/RX3.0/RY2.0" (目標/現在/最大X/最大Y)
                // 現在位置は符号付き（右/上が+、左/下が-）
                string currentSign = distanceInfo.currentOffset >= 0 ? "+" : "";
                distanceDebugTextMesh.text = $"Dist: {distanceInfo.targetDistance:F1}/{currentSign}{distanceInfo.currentOffset:F1}/RX{distanceInfo.maxRangeX:F1}/RY{distanceInfo.maxRangeY:F1}";
            }
            else if (showDistanceDebug && distanceInfo.isHopping)
            {
                // 表示例: "Hop: X+2.1/Y-1.3/RX5.0/RY3.0" (X方向オフセット/Y方向オフセット/最大X/最大Y)
                string xSign = distanceInfo.offsetX >= 0 ? "+" : "";
                string ySign = distanceInfo.offsetY >= 0 ? "+" : "";
                distanceDebugTextMesh.text = $"Hop: X{xSign}{distanceInfo.offsetX:F1}/Y{ySign}{distanceInfo.offsetY:F1}/RX{distanceInfo.maxRangeX:F1}/RY{distanceInfo.maxRangeY:F1}";
            }
            else if (showDistanceDebug && distanceInfo.isWarp)
            {
                // 表示例: "Warp: X+2.1/Y-1.3/RX8.0/RY4.0" (X方向オフセット/Y方向オフセット/最大X/最大Y)
                string xSign = distanceInfo.offsetX >= 0 ? "+" : "";
                string ySign = distanceInfo.offsetY >= 0 ? "+" : "";
                distanceDebugTextMesh.text = $"Warp: X{xSign}{distanceInfo.offsetX:F1}/Y{ySign}{distanceInfo.offsetY:F1}/RX{distanceInfo.maxRangeX:F1}/RY{distanceInfo.maxRangeY:F1}";
            }
            else
            {
                distanceDebugTextMesh.text = "";
            }
        }
        else if (distanceDebugTextMesh != null)
        {
            distanceDebugTextMesh.text = "";
        }
    }

    public void SetRoutineDebugEnabled(bool enabled)
    {
        showRoutineDebug = enabled;
        if (routineDebugTextObject != null)
        {
            routineDebugTextObject.SetActive(enabled);
        }
    }
}
