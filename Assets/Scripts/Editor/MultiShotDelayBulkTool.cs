using UnityEditor;
using UnityEngine;

/// <summary>
/// 全エネミーのMulti弾（Bullet TypesでUse Multi ShotがONの弾）の「1発ずつずらす間隔」（Multi Shot Launch Delay）を一括で設定する。
/// 値を直接設定するので、再実行しても同じ値になる。
/// </summary>
public static class MultiShotDelayBulkTool
{
    private const float TargetDelay = 0.2f;

    [MenuItem("Tools/Enemies/全エネミーのMulti弾の発射間隔を0.2秒に")]
    private static void SetAllMultiShotDelay()
    {
        string[] guids = AssetDatabase.FindAssets("t:EnemyData");
        var targets = new System.Collections.Generic.List<(EnemyData data, int index)>();
        var listText = new System.Text.StringBuilder();
        foreach (string g in guids)
        {
            var data = AssetDatabase.LoadAssetAtPath<EnemyData>(AssetDatabase.GUIDToAssetPath(g));
            if (data == null || data.bulletTypes == null) continue;
            for (int i = 0; i < data.bulletTypes.Length; i++)
            {
                var t = data.bulletTypes[i];
                if (t == null || !t.useMultiShot) continue;
                if (Mathf.Approximately(t.multiShotLaunchDelay, TargetDelay)) continue;
                targets.Add((data, i));
                listText.AppendLine($"{data.name} [{i}] {t.name}：{t.multiShotLaunchDelay:0.###} → {TargetDelay}");
            }
        }
        if (targets.Count == 0)
        {
            EditorUtility.DisplayDialog("Multi弾の発射間隔", $"変更が必要な弾はありません（すべて{TargetDelay}秒です）。", "OK");
            return;
        }
        if (!EditorUtility.DisplayDialog("Multi弾の発射間隔",
                $"Use Multi ShotがONの弾 {targets.Count}種類の Multi Shot Launch Delay を {TargetDelay}秒にします。\n（一覧はConsoleに出力します）\n続けますか？",
                "変更する", "キャンセル"))
            return;

        foreach (var (data, index) in targets)
        {
            Undo.RecordObject(data, "Set Multi Shot Launch Delay");
            data.bulletTypes[index].multiShotLaunchDelay = TargetDelay;
            EditorUtility.SetDirty(data);
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"[MultiShotDelayBulkTool] Multi Shot Launch Delayを{TargetDelay}秒に変更しました（{targets.Count}種類）\n{listText}");
        EditorUtility.DisplayDialog("Multi弾の発射間隔", $"{targets.Count}種類を変更しました。", "OK");
    }
}
