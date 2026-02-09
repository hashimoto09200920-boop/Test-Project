using UnityEngine;
using UnityEditor;
using Game.Skills;
using System.Linq;

namespace Game.Editor
{
    /// <summary>
    /// SkillDefinitionのCustom Editor
    /// 全スキルの出現率合計を表示
    /// </summary>
    [CustomEditor(typeof(SkillDefinition))]
    public class SkillDefinitionEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            // 通常のInspectorを描画
            DrawDefaultInspector();

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            EditorGUILayout.Space(5);

            // 全スキルの出現率を計算
            var allSkills = Resources.LoadAll<SkillDefinition>("GameData/Skills");
            if (allSkills == null || allSkills.Length == 0)
            {
                EditorGUILayout.HelpBox("スキルアセットが見つかりません。", MessageType.Warning);
                return;
            }

            // 現在のスキル
            SkillDefinition currentSkill = (SkillDefinition)target;

            // ヘッダー
            EditorGUILayout.LabelField("出現率統計", EditorStyles.boldLabel);

            // Stage1とStage2の統計を両方表示
            DrawStageStatistics("Stage 1", allSkills, currentSkill, true);
            EditorGUILayout.Space(5);
            DrawStageStatistics("Stage 2", allSkills, currentSkill, false);

            EditorGUILayout.Space(5);

            // 詳細情報ボタン
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Stage1の全スキル出現率を表示"))
            {
                ShowAllSkillWeights(allSkills, true);
            }
            if (GUILayout.Button("Stage2の全スキル出現率を表示"))
            {
                ShowAllSkillWeights(allSkills, false);
            }
            EditorGUILayout.EndHorizontal();

            // 均等配分ボタン
            EditorGUILayout.Space(5);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Stage1を均等配分"))
            {
                if (EditorUtility.DisplayDialog("確認", "Stage1の全スキル出現率を均等に設定しますか？", "はい", "キャンセル"))
                {
                    SetEqualWeights(allSkills, true);
                }
            }
            if (GUILayout.Button("Stage2を均等配分"))
            {
                if (EditorUtility.DisplayDialog("確認", "Stage2の全スキル出現率を均等に設定しますか？", "はい", "キャンセル"))
                {
                    SetEqualWeights(allSkills, false);
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// ステージ別の統計を表示
        /// </summary>
        private void DrawStageStatistics(string stageName, SkillDefinition[] allSkills, SkillDefinition currentSkill, bool isStage1)
        {
            // カテゴリ別に集計
            var categoryA = allSkills.Where(s => s.category == SkillCategory.CategoryA).ToList();
            var categoryB = allSkills.Where(s => s.category == SkillCategory.CategoryB).ToList();
            var categoryC = allSkills.Where(s => s.category == SkillCategory.CategoryC).ToList();

            float totalWeight = isStage1
                ? allSkills.Sum(s => s.spawnWeight)
                : allSkills.Sum(s => s.spawnWeightStage2);

            float categoryAWeight = isStage1
                ? categoryA.Sum(s => s.spawnWeight)
                : categoryA.Sum(s => s.spawnWeightStage2);

            float categoryBWeight = isStage1
                ? categoryB.Sum(s => s.spawnWeight)
                : categoryB.Sum(s => s.spawnWeightStage2);

            float categoryCWeight = isStage1
                ? categoryC.Sum(s => s.spawnWeight)
                : categoryC.Sum(s => s.spawnWeightStage2);

            float currentWeight = isStage1 ? currentSkill.spawnWeight : currentSkill.spawnWeightStage2;
            float currentPercentage = totalWeight > 0 ? (currentWeight / totalWeight) * 100f : 0f;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(stageName, EditorStyles.boldLabel);

            // 現在のスキルの割合
            EditorGUILayout.LabelField($"このスキルの出現率: {currentPercentage:F1}%");

            EditorGUILayout.Space(3);

            // カテゴリ別の統計
            EditorGUILayout.LabelField("カテゴリ別:", EditorStyles.miniLabel);
            EditorGUILayout.LabelField($"  A ({categoryA.Count}種): {categoryAWeight:F1} ({(totalWeight > 0 ? (categoryAWeight / totalWeight) * 100f : 0f):F1}%)");
            EditorGUILayout.LabelField($"  B ({categoryB.Count}種): {categoryBWeight:F1} ({(totalWeight > 0 ? (categoryBWeight / totalWeight) * 100f : 0f):F1}%)");
            EditorGUILayout.LabelField($"  C ({categoryC.Count}種): {categoryCWeight:F1} ({(totalWeight > 0 ? (categoryCWeight / totalWeight) * 100f : 0f):F1}%)");

            EditorGUILayout.Space(3);

            // 全体の合計
            EditorGUILayout.LabelField($"合計: {totalWeight:F1} / 100.0", EditorStyles.boldLabel);

            // 100%との差分を表示
            float difference = totalWeight - 100f;
            if (Mathf.Abs(difference) > 0.01f)
            {
                string diffText = difference > 0 ? $"+{difference:F1}" : $"{difference:F1}";
                EditorGUILayout.HelpBox($"合計が100%ではありません (差分: {diffText})", MessageType.Warning);
            }
            else
            {
                EditorGUILayout.HelpBox("合計100%に設定 ✓", MessageType.Info);
            }

            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// 全スキルの出現率をコンソールに表示
        /// </summary>
        private void ShowAllSkillWeights(SkillDefinition[] allSkills, bool isStage1)
        {
            string stageName = isStage1 ? "Stage1" : "Stage2";
            float totalWeight = isStage1
                ? allSkills.Sum(s => s.spawnWeight)
                : allSkills.Sum(s => s.spawnWeightStage2);

            Debug.Log($"===== {stageName}の全スキル出現率 =====");

            var grouped = allSkills.OrderBy(s => s.category).ThenBy(s => s.name);
            foreach (var skill in grouped)
            {
                float weight = isStage1 ? skill.spawnWeight : skill.spawnWeightStage2;
                float percentage = totalWeight > 0 ? (weight / totalWeight) * 100f : 0f;
                Debug.Log($"{skill.name}: {weight:F1} ({percentage:F1}%)");
            }

            Debug.Log($"\n合計: {totalWeight:F1} / 100.0");
        }

        /// <summary>
        /// 全スキルを均等配分
        /// </summary>
        private void SetEqualWeights(SkillDefinition[] allSkills, bool isStage1)
        {
            float equalWeight = 100f / allSkills.Length;

            foreach (var skill in allSkills)
            {
                if (isStage1)
                {
                    skill.spawnWeight = equalWeight;
                }
                else
                {
                    skill.spawnWeightStage2 = equalWeight;
                }
                EditorUtility.SetDirty(skill);
            }

            AssetDatabase.SaveAssets();
            string stageName = isStage1 ? "Stage1" : "Stage2";
            Debug.Log($"[{stageName}] 全{allSkills.Length}スキルを均等配分しました ({equalWeight:F2}% ずつ)");
        }
    }
}
