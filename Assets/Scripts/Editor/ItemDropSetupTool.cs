using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// アイテムの出る割合（AreaConfig > Stage Block Config > Item Drop Weights）を、今の設定から作るメニュー（2026/11/10）。
/// ・一覧が空のAreaConfig：今の「Gold Drop Rate」から、コイン＝その値、ハート＝100−その値、金貨袋・宝箱＝0 で作る（今と同じ出方のまま）
/// ・一覧があるAreaConfig：足りない種類だけ重み0で足す（入れた値は変えない）
/// 金貨袋・宝箱を出すには、作った後にInspectorで重みを入れる
/// </summary>
public static class ItemDropSetupTool
{
    [MenuItem("Tools/アイテム/アイテムの出る割合を今の設定から作る（全AreaConfig）")]
    private static void Build()
    {
        var log = new System.Text.StringBuilder();
        var all = (BlockItem.ItemType[])System.Enum.GetValues(typeof(BlockItem.ItemType));
        foreach (var guid in AssetDatabase.FindAssets("t:AreaConfig"))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var cfg = AssetDatabase.LoadAssetAtPath<AreaConfig>(path);
            if (cfg == null || cfg.stageBlockConfig == null) continue;
            var sb = cfg.stageBlockConfig;
            Undo.RecordObject(cfg, "アイテムの出る割合");
            if (sb.itemDropWeights == null) sb.itemDropWeights = new List<ItemDropWeight>();

            if (sb.itemDropWeights.Count == 0)
            {
                float gold = Mathf.Clamp(sb.goldDropRate, 0f, 100f);
                sb.itemDropWeights.Add(new ItemDropWeight { type = BlockItem.ItemType.Life, weight = 100f - gold });
                sb.itemDropWeights.Add(new ItemDropWeight { type = BlockItem.ItemType.Gold, weight = gold });
                log.AppendLine($"・{cfg.name}：作成（ハート {100f - gold}／コイン {gold}）");
            }
            int added = 0;
            foreach (var t in all)
            {
                if (sb.itemDropWeights.Exists(w => w != null && w.type == t)) continue;
                sb.itemDropWeights.Add(new ItemDropWeight { type = t, weight = 0f });
                added++;
            }
            if (added > 0) log.AppendLine($"・{cfg.name}：足りない種類を {added} 個、重み0で足しました");
            EditorUtility.SetDirty(cfg);
        }
        AssetDatabase.SaveAssets();
        if (log.Length == 0) log.AppendLine("変更はありませんでした（全AreaConfigに全種類あります）");
        log.AppendLine("金貨袋（GoldBag）・宝箱（TreasureChest）を出すには、各AreaConfigの「Item Drop Weights」で重みを入れてください。");
        Debug.Log("[ItemDropSetupTool]\n" + log);
        EditorUtility.DisplayDialog("アイテムの出る割合", log.ToString(), "OK");
    }
}
