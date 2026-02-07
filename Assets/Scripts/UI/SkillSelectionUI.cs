using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Game.Skills;

namespace Game.UI
{
    /// <summary>
    /// スキル選択画面（ヴァンパイアサバイバー風3択）
    /// </summary>
    public class SkillSelectionUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject selectionPanel;
        [SerializeField] private SkillCardUI[] skillCards; // 3枚のカード
        [SerializeField] private TMP_Text titleText;

        [Header("Skill Pools")]
        [Tooltip("カテゴリAのスキル一覧（Stage 1クリア後）")]
        [SerializeField] private SkillDefinition[] categoryASkills;

        [Tooltip("カテゴリBのスキル一覧（Stage 2クリア後）")]
        [SerializeField] private SkillDefinition[] categoryBSkills;

        [Header("Settings")]
        [SerializeField] private bool showLog = true;

        private SkillCategory currentCategory;
        private int remainingSelections;
        private System.Action onAllSelectionsComplete;

        private void Awake()
        {
            if (selectionPanel != null)
            {
                selectionPanel.SetActive(false);
            }
        }

        /// <summary>
        /// スキル選択を開始
        /// </summary>
        /// <param name="category">カテゴリ（A or B）</param>
        /// <param name="selectionCount">選択回数</param>
        /// <param name="onComplete">全選択完了時のコールバック</param>
        public void StartSkillSelection(SkillCategory category, int selectionCount, System.Action onComplete)
        {
            if (selectionCount <= 0)
            {
                // 選択回数が0の場合はスキップ
                onComplete?.Invoke();
                return;
            }

            currentCategory = category;
            remainingSelections = selectionCount;
            onAllSelectionsComplete = onComplete;

            if (showLog)
            {
                Debug.Log($"[SkillSelectionUI] Starting skill selection: Category={category}, Count={selectionCount}");
            }

            // ゲームをポーズ
            Time.timeScale = 0f;

            // 最初の選択を表示
            ShowNextSelection();
        }

        /// <summary>
        /// 次のスキル選択を表示
        /// </summary>
        private void ShowNextSelection()
        {
            if (remainingSelections <= 0)
            {
                // 全選択完了
                CompleteSelection();
                return;
            }

            // ランダムに3つのスキルを選択
            SkillDefinition[] availableSkills = currentCategory == SkillCategory.CategoryA
                ? categoryASkills
                : categoryBSkills;

            if (availableSkills == null || availableSkills.Length == 0)
            {
                Debug.LogError($"[SkillSelectionUI] No skills available for category {currentCategory}");
                CompleteSelection();
                return;
            }

            List<SkillDefinition> selectedSkills = GetRandomSkills(availableSkills, 3);

            if (showLog)
            {
                Debug.Log($"[SkillSelectionUI] Selected {selectedSkills.Count} skills:");
                foreach (var skill in selectedSkills)
                {
                    if (skill != null)
                        Debug.Log($"  - {skill.skillName}: {skill.description} ({skill.effectValue})");
                    else
                        Debug.LogError("  - NULL SKILL!");
                }
            }

            // UIを更新
            if (titleText != null)
            {
                string categoryName = currentCategory == SkillCategory.CategoryA ? "A" : "B";
                titleText.text = $"スキル選択 ({remainingSelections} 回残り) - カテゴリ {categoryName}";
            }

            // カードを表示
            for (int i = 0; i < skillCards.Length; i++)
            {
                if (i < selectedSkills.Count && selectedSkills[i] != null)
                {
                    if (showLog)
                        Debug.Log($"[SkillSelectionUI] Setting up card {i}: {selectedSkills[i].skillName}");
                    skillCards[i].SetupCard(selectedSkills[i], OnSkillSelected);
                    skillCards[i].gameObject.SetActive(true);
                }
                else
                {
                    skillCards[i].gameObject.SetActive(false);
                }
            }

            // パネルを表示
            if (selectionPanel != null)
            {
                selectionPanel.SetActive(true);
            }
        }

        /// <summary>
        /// ランダムにスキルを選択（重複なし）
        /// </summary>
        private List<SkillDefinition> GetRandomSkills(SkillDefinition[] pool, int count)
        {
            List<SkillDefinition> result = new List<SkillDefinition>();
            List<SkillDefinition> tempPool = new List<SkillDefinition>(pool);

            count = Mathf.Min(count, tempPool.Count);

            for (int i = 0; i < count; i++)
            {
                int randomIndex = Random.Range(0, tempPool.Count);
                result.Add(tempPool[randomIndex]);
                tempPool.RemoveAt(randomIndex);
            }

            return result;
        }

        /// <summary>
        /// スキルが選択された時のコールバック
        /// </summary>
        private void OnSkillSelected(SkillDefinition skill)
        {
            if (showLog)
            {
                Debug.Log($"[SkillSelectionUI] Skill selected: {skill.skillName}");
            }

            // SkillManager に追加
            if (SkillManager.Instance != null)
            {
                SkillManager.Instance.AddSkill(skill);
            }

            // パネルを非表示
            if (selectionPanel != null)
            {
                selectionPanel.SetActive(false);
            }

            // 残り回数を減らす
            remainingSelections--;

            // 次の選択へ（少し遅延を入れる）
            StartCoroutine(ShowNextSelectionDelayed(0.2f));
        }

        /// <summary>
        /// 遅延して次の選択を表示
        /// </summary>
        private IEnumerator ShowNextSelectionDelayed(float delay)
        {
            yield return new WaitForSecondsRealtime(delay);
            ShowNextSelection();
        }

        /// <summary>
        /// 全選択完了
        /// </summary>
        private void CompleteSelection()
        {
            if (showLog)
            {
                Debug.Log("[SkillSelectionUI] All selections complete");
            }

            // パネルを非表示
            if (selectionPanel != null)
            {
                selectionPanel.SetActive(false);
            }

            // ゲームのポーズを解除
            Time.timeScale = 1f;

            // コールバックを呼ぶ
            onAllSelectionsComplete?.Invoke();
            onAllSelectionsComplete = null;
        }
    }
}
