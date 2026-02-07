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
        /// スキルリストを更新
        /// </summary>
        private void RefreshSkillList()
        {
            if (skillListText == null) return;

            SkillManager skillManager = SkillManager.Instance;
            if (skillManager == null || skillManager.ActiveSkills == null || skillManager.ActiveSkills.Count == 0)
            {
                skillListText.text = "■カテゴリA\n(なし)\n\n■カテゴリB\n(なし)";
                return;
            }

            // カテゴリ別にスキルを分類
            var categoryA = skillManager.ActiveSkills
                .Where(s => s.category == SkillCategory.CategoryA)
                .ToList();

            var categoryB = skillManager.ActiveSkills
                .Where(s => s.category == SkillCategory.CategoryB)
                .ToList();

            // テキストを構築
            string text = "■カテゴリA\n";
            if (categoryA.Count > 0)
            {
                // 同じスキルをグループ化してカウント
                var groupedA = categoryA.GroupBy(s => s.skillName)
                    .Select(g => new { Name = g.Key, Count = g.Count() });

                foreach (var group in groupedA)
                {
                    if (group.Count > 1)
                    {
                        text += $"{group.Name}×{group.Count}\n";
                    }
                    else
                    {
                        text += $"{group.Name}\n";
                    }
                }
            }
            else
            {
                text += "(なし)\n";
            }

            text += "\n■カテゴリB\n";
            if (categoryB.Count > 0)
            {
                // 同じスキルをグループ化してカウント
                var groupedB = categoryB.GroupBy(s => s.skillName)
                    .Select(g => new { Name = g.Key, Count = g.Count() });

                foreach (var group in groupedB)
                {
                    if (group.Count > 1)
                    {
                        text += $"{group.Name}×{group.Count}\n";
                    }
                    else
                    {
                        text += $"{group.Name}\n";
                    }
                }
            }
            else
            {
                text += "(なし)";
            }

            skillListText.text = text;

            if (showLog)
            {
                Debug.Log($"[SkillListUI] Updated: A={categoryA.Count}, B={categoryB.Count}");
            }
        }
    }
}
