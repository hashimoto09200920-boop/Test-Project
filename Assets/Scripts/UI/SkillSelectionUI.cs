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
        [SerializeField] private Button skipButton; // スキップボタン（全スキル上限時に表示）
        [Tooltip("SkillCard1/2/3 の日本語表示用。未設定だと既存フォントのまま（文字化けの原因になりうる）")]
        [SerializeField] private TMP_FontAsset japaneseFontAsset;

        [Header("Skill Pools")]
        [Tooltip("カテゴリAのスキル一覧（Stage 1クリア後）")]
        [SerializeField] private SkillDefinition[] categoryASkills;

        [Tooltip("カテゴリBのスキル一覧（Stage 2クリア後）")]
        [SerializeField] private SkillDefinition[] categoryBSkills;

        [Tooltip("カテゴリCのスキル一覧（Stage 3クリア後）")]
        [SerializeField] private SkillDefinition[] categoryCSkills;

        [Header("Messages")]
        [Tooltip("全スキルが上限に達した時のメッセージ")]
        [SerializeField] private string noSkillsMessage = "全てのスキルが上限に達しています";

        [Tooltip("通常のスキル選択時のメッセージ（{0}=残り回数）")]
        [SerializeField] private string normalSelectionMessage = "スキル選択 ({0} 回残り)";

        [Header("Settings")]
        [SerializeField] private bool showLog = true;
        [Tooltip("テスト用：全スキルが上限に達した状態をシミュレート")]
        [SerializeField] private bool testSkipButtonMode = false;

        private SkillCategory currentCategory;
        private int remainingSelections;
        private int currentStageIndex; // 0=Stage1, 1=Stage2, 2=Stage3
        private System.Action onAllSelectionsComplete;

        private void Awake()
        {
            if (selectionPanel != null)
            {
                selectionPanel.SetActive(false);
            }

            // スキップボタンのセットアップ
            if (skipButton != null)
            {
                skipButton.onClick.AddListener(OnSkipButtonClicked);
                skipButton.gameObject.SetActive(false);
            }

            // SkillCard1/2/3 に日本語フォントを適用（文字化け・文字抜け防止）
            if (japaneseFontAsset != null && skillCards != null)
            {
                foreach (var card in skillCards)
                {
                    if (card != null)
                        card.SetFont(japaneseFontAsset);
                }
            }
        }

        /// <summary>
        /// スキル選択を開始
        /// </summary>
        /// <param name="category">カテゴリ（A/B/C/All）</param>
        /// <param name="selectionCount">選択回数</param>
        /// <param name="onComplete">全選択完了時のコールバック</param>
        /// <param name="stageIndex">現在のステージインデックス（0=Stage1, 1=Stage2, 2=Stage3）デフォルト0</param>
        public void StartSkillSelection(SkillCategory category, int selectionCount, System.Action onComplete, int stageIndex = 0)
        {
            if (selectionCount <= 0)
            {
                // 選択回数が0の場合はスキップ
                onComplete?.Invoke();
                return;
            }

            currentCategory = category;
            remainingSelections = selectionCount;
            currentStageIndex = stageIndex;
            onAllSelectionsComplete = onComplete;

            if (showLog)
            {
                Debug.Log($"[SkillSelectionUI] Starting skill selection: Category={category}, Count={selectionCount}, StageIndex={stageIndex}");
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
            SkillDefinition[] availableSkills;
            if (currentCategory == SkillCategory.All)
            {
                // 全カテゴリを結合
                List<SkillDefinition> allSkills = new List<SkillDefinition>();
                if (categoryASkills != null) allSkills.AddRange(categoryASkills);
                if (categoryBSkills != null) allSkills.AddRange(categoryBSkills);
                if (categoryCSkills != null) allSkills.AddRange(categoryCSkills);
                availableSkills = allSkills.ToArray();
            }
            else if (currentCategory == SkillCategory.CategoryA)
            {
                availableSkills = categoryASkills;
            }
            else if (currentCategory == SkillCategory.CategoryB)
            {
                availableSkills = categoryBSkills;
            }
            else // CategoryC
            {
                availableSkills = categoryCSkills;
            }

            if (availableSkills == null || availableSkills.Length == 0)
            {
                Debug.LogError($"[SkillSelectionUI] No skills available for category {currentCategory}");
                CompleteSelection();
                return;
            }

            List<SkillDefinition> selectedSkills = GetRandomSkills(availableSkills, 3, currentStageIndex);

            // スキルが1つも選択できない場合、スキップボタンを表示
            bool noSkillsAvailable = selectedSkills.Count == 0;

            if (showLog)
            {
                if (noSkillsAvailable)
                {
                    Debug.Log($"[SkillSelectionUI] No skills available - showing skip button");
                }
                else
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
            }

            // UIを更新
            if (titleText != null)
            {
                if (noSkillsAvailable)
                {
                    titleText.text = noSkillsMessage;
                }
                else
                {
                    titleText.text = string.Format(normalSelectionMessage, remainingSelections);
                }
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

            // スキップボタンの表示切替
            if (skipButton != null)
            {
                skipButton.gameObject.SetActive(noSkillsAvailable);
            }

            // パネルを表示
            if (selectionPanel != null)
            {
                selectionPanel.SetActive(true);
            }
        }

        /// <summary>
        /// ランダムにスキルを選択（重複なし、取得上限チェック、重み付き確率）
        /// </summary>
        /// <param name="pool">スキルプール</param>
        /// <param name="count">選択数</param>
        /// <param name="stageIndex">ステージインデックス（0=Stage1でspawnWeight使用, 1=Stage2でspawnWeightStage2使用）</param>
        private List<SkillDefinition> GetRandomSkills(SkillDefinition[] pool, int count, int stageIndex)
        {
            List<SkillDefinition> result = new List<SkillDefinition>();

            // テストモード：スキップボタン表示テスト用
            if (testSkipButtonMode)
            {
                if (showLog)
                {
                    Debug.Log("[SkillSelectionUI] TEST MODE: Simulating all skills maxed out");
                }
                return result; // 空のリストを返してスキップボタンを強制表示
            }

            // 取得可能なスキルのみをフィルタリング
            List<SkillDefinition> availableSkills = new List<SkillDefinition>();
            foreach (var skill in pool)
            {
                if (skill != null && SkillManager.Instance != null && SkillManager.Instance.CanAcquireSkill(skill))
                {
                    availableSkills.Add(skill);
                }
            }

            if (availableSkills.Count == 0)
            {
                if (showLog)
                {
                    Debug.Log("[SkillSelectionUI] No available skills (all maxed out)");
                }
                return result; // 空のリストを返す
            }

            count = Mathf.Min(count, availableSkills.Count);

            // 重み付きランダム選択
            for (int i = 0; i < count; i++)
            {
                SkillDefinition selected = SelectSkillByWeight(availableSkills, stageIndex);
                if (selected != null)
                {
                    result.Add(selected);
                    availableSkills.Remove(selected); // 重複防止
                }
            }

            return result;
        }

        /// <summary>
        /// 重み付き確率でスキルを1つ選択
        /// </summary>
        /// <param name="skills">スキルリスト</param>
        /// <param name="stageIndex">ステージインデックス（0=Stage1, 1=Stage2）</param>
        private SkillDefinition SelectSkillByWeight(List<SkillDefinition> skills, int stageIndex)
        {
            if (skills.Count == 0) return null;

            // 総重みを計算（Stage1ならspawnWeight、Stage2ならspawnWeightStage2）
            float totalWeight = 0f;
            foreach (var skill in skills)
            {
                if (skill != null)
                {
                    float weight = (stageIndex == 0) ? skill.spawnWeight : skill.spawnWeightStage2;
                    totalWeight += weight;
                }
            }

            if (totalWeight <= 0f)
            {
                // 全ての重みが0の場合は均等確率で選択
                int randomIndex = Random.Range(0, skills.Count);
                return skills[randomIndex];
            }

            // ランダム値を取得（0 ~ totalWeight）
            float randomValue = Random.Range(0f, totalWeight);

            // 累積重みと比較して選択
            float currentWeight = 0f;
            foreach (var skill in skills)
            {
                if (skill == null) continue;

                float weight = (stageIndex == 0) ? skill.spawnWeight : skill.spawnWeightStage2;
                currentWeight += weight;

                if (randomValue <= currentWeight)
                {
                    return skill;
                }
            }

            // フォールバック（通常ここには到達しない）
            return skills[skills.Count - 1];
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

            // ランダム範囲が有効な場合、コピーを作成して値を決定
            SkillDefinition skillToAdd = skill;
            if (skill.useRandomRange)
            {
                skillToAdd = ScriptableObject.Instantiate(skill);
                skillToAdd.name = skill.name; // 元のnameを保持（Cloneサフィックスを防ぐ）
                skillToAdd.effectValue = skill.GetRandomizedEffectValue();

                if (showLog)
                {
                    Debug.Log($"[SkillSelectionUI] Randomized value: {skillToAdd.effectValue:F2} (range: {skill.effectValueMin:F2}~{skill.effectValueMax:F2})");
                }
            }

            // SkillManager に追加
            if (SkillManager.Instance != null)
            {
                SkillManager.Instance.AddSkill(skillToAdd);
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

        /// <summary>
        /// スキップボタンがクリックされた時の処理
        /// </summary>
        private void OnSkipButtonClicked()
        {
            if (showLog)
            {
                Debug.Log("[SkillSelectionUI] Skip button clicked");
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
    }
}
