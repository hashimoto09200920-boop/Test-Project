using UnityEngine;

/// <summary>
/// Area2ボス「Condor」専用：左右の翼（WingRight / WingLeft）の羽ばたきアニメーションを常に同時に再生させる部品。
///
/// ★原因：翼はそれぞれ別のAnimatorで動いている。共有のEnemySpriteSwapperはスローモーション中にAnimatorの速さを
///   SlowMotionManager.TimeScaleへ変えるが、対象はGetComponentInChildren<Animator>()で最初に見つかった1つ（WingRight）だけ。
///   そのためスローモーション中は左の翼だけ通常速度で羽ばたき、使うたびに左右のタイミングがずれていく。
///   共有コードは変えず（他エネミーに影響させないため）、この部品で左の翼を右の翼に合わせる：
/// ・速さ：左右とも毎フレームSlowMotionManager.TimeScale（EnemySpriteSwapperが右の翼に設定する値と同じ）
/// ・タイミング：左の翼の再生位置が右の翼とずれていたら、右の翼の再生位置に揃える
/// 配置はメニュー「Tools/Condor/翼の羽ばたきを左右同期」で行う（Condor.prefabのルートに追加）。
/// </summary>
[DefaultExecutionOrder(100)] // EnemySpriteSwapper（既定0）が右の翼の速さを設定した後に、両方の翼の速さを上書きする
[DisallowMultipleComponent]
public class CondorWingSync : MonoBehaviour
{
    /// <summary>羽ばたきの速さの倍率（特殊攻撃の溜め中に速く羽ばたかせる。CondorSpecialAttackが設定する）</summary>
    public float SpeedMultiplier { get; set; } = 1f;

    [Tooltip("基準にする翼（WingRight。EnemySpriteSwapperが速さを変えている方）")]
    [SerializeField] private Animator leaderWing;
    [Tooltip("基準の翼に合わせる翼（WingLeft）")]
    [SerializeField] private Animator[] followerWings;
    [Tooltip("再生位置（0〜1の割合）がこれ以上ずれていたら揃える")]
    [SerializeField] private float resyncThreshold = 0.01f;

    private void Awake()
    {
        // 右の翼はEnemySpriteSwapperがkeepAnimatorStateOnDisable=trueにしているため、左の翼も同じにしておく
        if (followerWings == null) return;
        foreach (var f in followerWings) if (f != null) f.keepAnimatorStateOnDisable = true;
    }

    private void Update()
    {
        if (leaderWing == null || followerWings == null) return;
        float speed = (SlowMotionManager.Instance != null ? SlowMotionManager.Instance.TimeScale : 1f) * Mathf.Max(0f, SpeedMultiplier);
        leaderWing.speed = speed;
        foreach (var f in followerWings) if (f != null) f.speed = speed;
    }

    private void LateUpdate()
    {
        if (leaderWing == null || followerWings == null) return;
        if (!leaderWing.isActiveAndEnabled || leaderWing.runtimeAnimatorController == null) return;

        AnimatorStateInfo leaderState = leaderWing.GetCurrentAnimatorStateInfo(0);
        float leaderPhase = Mathf.Repeat(leaderState.normalizedTime, 1f);

        foreach (var f in followerWings)
        {
            if (f == null || !f.isActiveAndEnabled || f.runtimeAnimatorController == null) continue;
            AnimatorStateInfo st = f.GetCurrentAnimatorStateInfo(0);
            float phase = Mathf.Repeat(st.normalizedTime, 1f);
            float diff = Mathf.Abs(phase - leaderPhase);
            diff = Mathf.Min(diff, 1f - diff); // ループの境目（0.99と0.01など）はずれていない扱い
            if (st.fullPathHash != leaderState.fullPathHash || diff > resyncThreshold)
            {
                f.Play(leaderState.fullPathHash, 0, leaderPhase);
                f.Update(0f); // 今のフレームの見た目にすぐ反映する
            }
        }
    }
}
