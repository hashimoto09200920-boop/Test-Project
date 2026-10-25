using System.Collections.Generic;
using System.Linq;
using System.Text;
using Game.Gems;
using Game.Skills;
using UnityEditor;
using UnityEngine;

/// <summary>
/// テスト用（2026/10/25）：ジェムのスキル抽選に偏りが無いかを確かめる。
/// ゲームで実際に使っている抽選（GemManager.RollGemsForArea → RollGem）を、Area01〜10 のジェムごとに1万回呼び、
/// スキルごとに「付いたジェムの数（何%か）」を Console に出す。セーブデータ・インベントリには何も追加しない。
/// </summary>
public static class GemSkillRollStatsTool
{
    private const int RollsPerArea = 10000;

    [MenuItem("Tools/ジェム/スキル抽選の回数を確認（各Area 1万個）")]
    private static void Run()
    {
        var skills = Resources.LoadAll<SkillDefinition>("GameData/Skills").OrderBy(s => s.name).ToArray();
        var nameOf = skills.ToDictionary(s => s.name, s => $"{s.skillName}（{s.name}）");

        // 抽選処理だけを使うため、一時的に GemManager を作る（エディタの再生前なので Awake は動かず、シングルトンにも登録されない）
        var go = new GameObject("GemSkillRollStats_Temp") { hideFlags = HideFlags.HideAndDontSave };
        var manager = go.AddComponent<GemManager>();
        var sb = new StringBuilder();
        sb.AppendLine($"[GemSkillRollStats] ジェムのスキル抽選（各Area {RollsPerArea}個、ゲームと同じ抽選コード）");

        try
        {
            for (int area = 1; area <= 10; area++)
            {
                string areaId = $"Area_{area:D2}";
                if (EditorUtility.DisplayCancelableProgressBar("ジェムのスキル抽選を確認中", $"{areaId}（{RollsPerArea}個）", (area - 1) / 10f)) break;

                var gems = manager.RollGemsForArea(areaId, RollsPerArea);
                if (gems.Length == 0) { sb.AppendLine($"■ {areaId}：ジェムの設定が見つかりません"); continue; }

                var total = new Dictionary<string, int>();
                var asBase = new Dictionary<string, int>();
                var asBonus = new Dictionary<string, int>();
                foreach (var g in gems)
                {
                    Count(total, asBase, g.baseSkillName);
                    Count(total, asBonus, g.bonusSkill1Name);
                    Count(total, asBonus, g.bonusSkill2Name);
                }

                sb.AppendLine($"■ {areaId}（{gems.Length}個）　スキル名：付いたジェムの割合（ベース／ボーナス）");
                foreach (var kv in total.OrderByDescending(k => k.Value))
                {
                    asBase.TryGetValue(kv.Key, out int b);
                    asBonus.TryGetValue(kv.Key, out int bo);
                    string label = nameOf.TryGetValue(kv.Key, out var n) ? n : kv.Key;
                    sb.AppendLine($"　{label}：{100f * kv.Value / gems.Length:0.0}%（ベース {100f * b / gems.Length:0.0}%／ボーナス {100f * bo / gems.Length:0.0}%）");
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            Object.DestroyImmediate(go);
        }

        Debug.Log(sb.ToString());
    }

    private static void Count(Dictionary<string, int> total, Dictionary<string, int> part, string skillName)
    {
        if (string.IsNullOrEmpty(skillName)) return;
        total.TryGetValue(skillName, out int t); total[skillName] = t + 1;
        part.TryGetValue(skillName, out int p); part[skillName] = p + 1;
    }
}
