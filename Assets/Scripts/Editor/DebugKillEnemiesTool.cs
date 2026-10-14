using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 【確認用】Play中に、今いる敵（ボス含む）を即時撃破するメニュー。
/// ・Die()を直接呼ばず、残りHPぶんのダメージ（EnemyStats.Damage）を与えて倒す。
///   通常の撃破と同じ流れ（撃破演出・SE・ゴールド・撃破数・ステージ進行・ツクヨミ→スサノオの連動）になる。
///   シールドがある敵は、シールドに吸われた分を追加で与えて必ずHPを0にする。
/// ・NeonDancerは「今のフェーズのHP」だけ削る：前半なら後半移行（ダウン→魂の救出）へ、後半なら撃破。
///   後半移行の演出中は無敵のため何もしない。
/// 既存のゲームコードは変更しない（NeonDancerControllerの非公開フラグはリフレクションで読むだけ）。
/// </summary>
public static class DebugKillEnemiesTool
{
    [MenuItem("Tools/Debug/今いる敵を即時撃破（ボス含む）")]
    private static void KillAll()
    {
        var enemies = Object.FindObjectsByType<EnemyStats>(FindObjectsSortMode.None);
        FieldInfo transitioningField = typeof(NeonDancerController).GetField("isTransitioning", BindingFlags.Instance | BindingFlags.NonPublic);
        int killed = 0, phased = 0, skipped = 0;

        foreach (var e in enemies)
        {
            if (e == null || !e.isActiveAndEnabled || e.HP <= 0) continue;

            var nd = e.GetComponent<NeonDancerController>();
            if (nd != null)
            {
                bool transitioning = transitioningField != null && (bool)transitioningField.GetValue(nd);
                if (transitioning || nd.PhaseHp <= 0) { skipped++; continue; }
                bool wasPhase2 = nd.IsPhase2;
                for (int i = 0; i < 8 && e != null && nd.PhaseHp > 0; i++) e.Damage(nd.PhaseHp);
                if (wasPhase2) killed++; else phased++;
                continue;
            }

            // 通常の敵：シールドに吸われても倒れるまで繰り返す（スサノオ等はダメージがツクヨミへ回り、連動して倒れる）
            for (int i = 0; i < 8 && e != null && e.HP > 0; i++) e.Damage(e.HP);
            killed++;
        }

        Debug.Log($"[DebugKillEnemies] 撃破={killed}体  NeonDancer前半→後半移行={phased}  スキップ（移行演出中など）={skipped}");
    }

    [MenuItem("Tools/Debug/今いる敵を即時撃破（ボス含む）", true)]
    private static bool KillAllValidate() => Application.isPlaying;
}
