using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using Game.Skills;
using System.Collections.Generic;
using System.Linq;

namespace Game.UI
{
    /// <summary>
    /// 取得したスキルのリストを画面右下に表示（05_Game専用）
    /// </summary>
    public class SkillListUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI skillListText;

        [Header("Settings")]
        [SerializeField] private bool showLog = false;

        private void Start()
        {
            // 05_Gameシーン以外では非表示
            if (SceneManager.GetActiveScene().name != "05_Game")
            {
                gameObject.SetActive(false);
                return;
            }

            // 05_Gameシーンでは表示
            gameObject.SetActive(true);

            // 初期表示
            RefreshSkillList();
        }

        private void Update()
        {
            // 05_Gameシーン以外では何もしない
            if (SceneManager.GetActiveScene().name != "05_Game")
            {
                return;
            }

            // フレーム毎に更新（スキル取得時に即座に反映）
            RefreshSkillList();
        }

        /// <summary>
        /// スキルリストを更新（効果値表示付き）
        /// </summary>
        private void RefreshSkillList()
        {
            if (skillListText == null) return;

            SkillManager skillManager = SkillManager.Instance;
            if (skillManager == null || skillManager.ActiveSkills == null || skillManager.ActiveSkills.Count == 0)
            {
                skillListText.text = "■カテゴリA\n(なし)\n\n■カテゴリB\n(なし)\n\n■カテゴリC\n(なし)";
                return;
            }

            // カテゴリ別にスキルを分類
            var categoryA = skillManager.ActiveSkills
                .Where(s => s.category == SkillCategory.CategoryA)
                .ToList();

            var categoryB = skillManager.ActiveSkills
                .Where(s => s.category == SkillCategory.CategoryB)
                .ToList();

            var categoryC = skillManager.ActiveSkills
                .Where(s => s.category == SkillCategory.CategoryC)
                .ToList();

            // テキストを構築
            string text = "■カテゴリA\n";
            text += BuildCategoryText(categoryA, skillManager);

            text += "\n■カテゴリB\n";
            text += BuildCategoryText(categoryB, skillManager);

            text += "\n■カテゴリC\n";
            text += BuildCategoryText(categoryC, skillManager);

            skillListText.text = text;

            if (showLog)
            {
                Debug.Log($"[SkillListUI] Updated: A={categoryA.Count}, B={categoryB.Count}, C={categoryC.Count}");
            }
        }

        /// <summary>
        /// カテゴリごとのテキストを構築（コンパクト表示）
        /// </summary>
        private string BuildCategoryText(List<SkillDefinition> skills, SkillManager manager)
        {
            if (skills.Count == 0)
            {
                return "(なし)\n";
            }

            string text = "";

            // 同じスキルをグループ化してカウント
            var grouped = skills.GroupBy(s => s.name)
                .Select(g => new { AssetName = g.Key, Count = g.Count() })
                .OrderBy(g => g.AssetName) // アセット名順にソート
                .ToList();

            // スキル番号とレベルのリストを作成
            var skillEntries = new List<string>();
            foreach (var group in grouped)
            {
                // スキル番号を抽出（例: Skill_A1_LeftMaxCostUp → A1）
                string skillNum = GetSkillNumber(group.AssetName);
                string entry = $"{skillNum}_{group.Count}";
                skillEntries.Add(entry);
            }

            // 2つずつペアにして1行にまとめる
            for (int i = 0; i < skillEntries.Count; i += 2)
            {
                if (i + 1 < skillEntries.Count)
                {
                    // ペアあり
                    text += $"{skillEntries[i]}/{skillEntries[i + 1]}\n";
                }
                else
                {
                    // 最後の1つ
                    text += $"{skillEntries[i]}\n";
                }
            }

            return text;
        }

        /// <summary>
        /// スキルアセット名から番号を抽出
        /// 例: Skill_A1_LeftMaxCostUp → A1
        /// </summary>
        private string GetSkillNumber(string assetName)
        {
            if (string.IsNullOrEmpty(assetName)) return "";

            // "Skill_" を除去
            string name = assetName.Replace("Skill_", "");

            // アンダースコアで分割
            string[] parts = name.Split('_');
            if (parts.Length < 1) return name;

            // カテゴリ+番号 (例: A1, B3, C2)
            return parts[0];
        }

    }
}
