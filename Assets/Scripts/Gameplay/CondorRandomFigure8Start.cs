using System.Reflection;
using UnityEngine;

/// <summary>
/// Area2ボス「Condor」専用：8の字移動（Figure8）を始める時の向きを左右ランダムにする部品。
///
/// ★共有のEnemyMoverの8の字は、開始時に角度0から x = 中心 + sin(角度)×幅 で動くため、必ず右から動き出す。
///   共有コードは変えず（他エネミーに影響させないため）、8の字が始まった直後（角度0のまま）に、
///   一定の確率で角度をπ（180°）にする。sin(π+t) = -sin(t)、sin(2π+2t) = sin(2t) なので、
///   開始位置は同じまま（位置は飛ばない）、左右だけ反転した8の字（左から動き出す）になる。
/// ・判定は「別の移動から8の字に切り替わって角度が0に戻った時」に1回だけ（8の字が続いている間は向きを変えない）
/// 配置はメニュー「Tools/Condor/8の字移動の開始方向を左右ランダムにする」で行う（Condor.prefabのルートに追加）。
/// </summary>
[DisallowMultipleComponent]
public class CondorRandomFigure8Start : MonoBehaviour
{
    [Tooltip("8の字を左から始める確率（%）。50＝左右半々")]
    [Range(0f, 100f)] [SerializeField] private float leftStartChancePercent = 50f;

    private static FieldInfo s_angleField;
    private static FieldInfo s_moveTypeField;
    private static bool s_fieldsResolved;

    private EnemyMover mover;
    private bool decidedThisFigure8;

    private void Awake()
    {
        mover = GetComponent<EnemyMover>();
        if (!s_fieldsResolved)
        {
            s_fieldsResolved = true;
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            s_angleField = typeof(EnemyMover).GetField("figure8Angle", flags);
            s_moveTypeField = typeof(EnemyMover).GetField("currentMoveType", flags);
            if (s_angleField == null || s_moveTypeField == null)
                Debug.LogWarning("[CondorRandomFigure8Start] EnemyMoverのfigure8Angle / currentMoveTypeが見つかりません（開始方向は右のまま）");
        }
    }

    // EnemyMover.Update（移動パターンの切り替えと8の字の移動）の後に判定する
    private void LateUpdate()
    {
        if (mover == null || !mover.enabled || s_angleField == null || s_moveTypeField == null) return;

        var mt = s_moveTypeField.GetValue(mover) as EnemyData.MoveType;
        if (mt == null || mt.patternType != EnemyData.MoveType.PatternType.Figure8)
        {
            decidedThisFigure8 = false; // 8の字以外の移動中：次に8の字が始まった時にもう一度決める
            return;
        }
        if (decidedThisFigure8) return;

        float angle = (float)s_angleField.GetValue(mover);
        if (angle != 0f)
        {
            // 角度が0でない＝すでに動き出している（このコンポーネントの追加前から8の字が続いていた等）。向きは変えない
            decidedThisFigure8 = true;
            return;
        }

        decidedThisFigure8 = true;
        if (Random.Range(0f, 100f) < leftStartChancePercent)
            s_angleField.SetValue(mover, Mathf.PI);
    }
}
