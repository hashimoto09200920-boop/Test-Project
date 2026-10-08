using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 【確認用】「ダウンした瞬間にステージの最後の敵（Areaボス）を倒した」状況をPlay中に再現するメニュー。
/// ステージクリア時の魂の自動救出（EnemySpawner.AutoRescueIfDownRoutine）の確認用。
/// ・プレイヤー（Playerタグ）を、PixelDancerControllerの既存デバッグ機能（DebugForceKill）で即ダウンさせる
/// ・同じフレームで、今いる敵（EnemyStats）をすべて倒す（Die(true)。プレイヤーに倒された扱い）
/// ★その時点でまだ出てくる敵（残りのFormation）がある場合はステージクリアにならないので、
///   各ステージの最後の敵／Areaボスが出ている時に使うこと。
/// 既存のゲームコードは変更しない（PixelDancerControllerのprivateなデバッグ機能をリフレクションで呼ぶだけ）。
/// </summary>
public static class DebugDownAtStageClearTool
{
    [MenuItem("Tools/Debug/ダウンと同時に今いる敵を全滅（ステージクリア時の自動救出の確認用）")]
    private static void DownAndKillAll()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        var dancer = playerObj != null ? playerObj.GetComponent<PixelDancerController>() : null;
        if (dancer == null)
        {
            Debug.LogWarning("[DebugDownAtStageClear] プレイヤー（Playerタグ）のPixelDancerControllerが見つかりません");
            return;
        }

        // ① プレイヤーを即ダウン（既にダウン中・無敵中ならPixelDancerController側が警告を出して何もしない）
        MethodInfo forceKill = typeof(PixelDancerController).GetMethod("DebugForceKill", BindingFlags.Instance | BindingFlags.NonPublic);
        if (forceKill == null)
        {
            Debug.LogWarning("[DebugDownAtStageClear] PixelDancerController.DebugForceKillが見つかりません");
            return;
        }
        forceKill.Invoke(dancer, null);

        // ② 今いる敵をすべて倒す
        var enemies = Object.FindObjectsByType<EnemyStats>(FindObjectsSortMode.None);
        int count = 0;
        foreach (var e in enemies)
        {
            if (e == null || !e.isActiveAndEnabled) continue;
            e.Die(true);
            count++;
        }
        Debug.Log($"[DebugDownAtStageClear] プレイヤーをダウンさせ、敵{count}体を倒しました（ダウン中={PixelDancerController.IsDownGlobal}）");
    }

    [MenuItem("Tools/Debug/ダウンと同時に今いる敵を全滅（ステージクリア時の自動救出の確認用）", true)]
    private static bool DownAndKillAllValidate() => Application.isPlaying;
}
