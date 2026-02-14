using UnityEngine;
using UnityEditor;
using Game.Skills;
using System.Linq;
using System.Collections.Generic;

namespace Game.Editor
{
    /// <summary>
    /// 全スキルの出現率を一括管理するEditorWindow
    /// </summary>
    public class SkillSpawnRateEditor : EditorWindow
    {
        private Vector2 scrollPosition;
        private SkillDefinition[] allSkills;
        private Dictionary<SkillDefinition, float> tempWeightsStage1 = new Dictionary<SkillDefinition, float>();
        private Dictionary<SkillDefinition, float> tempWeightsStage2 = new Dictionary<SkillDefinition, float>();
        private int selectedStageTab = 0; // 0 = Stage1, 1 = Stage2

        /// <summary>
        /// 選択中のステージの重みDictionaryを取得
        /// </summary>
        private Dictionary<SkillDefinition, float> tempWeights
        {
            get { return selectedStageTab == 0 ? tempWeightsStage1 : tempWeightsStage2; }
        }

        [MenuItem("Tools/Game/Skill Spawn Rate Editor")]
        public static void ShowWindow()
        {
            var window = GetWindow<SkillSpawnRateEditor>("スキル出現率エディター");
            window.minSize = new Vector2(600, 400);
        }

        private void OnEnable()
        {
            LoadSkills();
        }

        private void LoadSkills()
        {
            allSkills = Resources.LoadAll<SkillDefinition>("GameData/Skills")
                .OrderBy(s => s.category)
                .ThenBy(s => s.effectType) // effectTypeの数値順でソート（A1→A2→...→A10の順）
                .ToArray();

            // 一時的な重みを初期化（Stage1とStage2）
            tempWeightsStage1.Clear();
            tempWeightsStage2.Clear();
            foreach (var skill in allSkills)
            {
                tempWeightsStage1[skill] = skill.spawnWeight;
                tempWeightsStage2[skill] = skill.spawnWeightStage2;
            }
        }

        private void OnGUI()
        {
            if (allSkills == null || allSkills.Length == 0)
            {
                EditorGUILayout.HelpBox("スキルアセットが見つかりません。", MessageType.Warning);
                if (GUILayout.Button("再読み込み"))
                {
                    LoadSkills();
                }
                return;
            }

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("全スキルの出現率管理", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            // ステージタブ
            string[] tabNames = { "Stage 1", "Stage 2" };
            selectedStageTab = GUILayout.Toolbar(selectedStageTab, tabNames, GUILayout.Height(25));

            EditorGUILayout.Space(5);

            // 統計情報
            DrawStatistics();

            EditorGUILayout.Space(10);

            // スキルリスト
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            DrawSkillList();
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(10);

            // ボタン群
            DrawButtons();
        }

        private void DrawStatistics()
        {
            float totalWeight = tempWeights.Values.Sum();

            var categoryA = allSkills.Where(s => s.category == SkillCategory.CategoryA).ToList();
            var categoryB = allSkills.Where(s => s.category == SkillCategory.CategoryB).ToList();
            var categoryC = allSkills.Where(s => s.category == SkillCategory.CategoryC).ToList();

            float categoryAWeight = categoryA.Sum(s => tempWeights[s]);
            float categoryBWeight = categoryB.Sum(s => tempWeights[s]);
            float categoryCWeight = categoryC.Sum(s => tempWeights[s]);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // 合計表示
            GUIStyle boldStyle = new GUIStyle(EditorStyles.boldLabel);
            boldStyle.fontSize = 14;

            if (Mathf.Abs(totalWeight - 100f) > 0.01f)
            {
                boldStyle.normal.textColor = Color.red;
                EditorGUILayout.LabelField($"合計: {totalWeight:F1} / 100.0 ⚠", boldStyle);
            }
            else
            {
                boldStyle.normal.textColor = Color.green;
                EditorGUILayout.LabelField($"合計: {totalWeight:F1} / 100.0 ✓", boldStyle);
            }

            EditorGUILayout.Space(5);

            // カテゴリ別
            EditorGUILayout.LabelField("カテゴリ別:", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"  カテゴリA ({categoryA.Count}種): {categoryAWeight:F1} ({(totalWeight > 0 ? (categoryAWeight / totalWeight) * 100f : 0f):F1}%)");
            EditorGUILayout.LabelField($"  カテゴリB ({categoryB.Count}種): {categoryBWeight:F1} ({(totalWeight > 0 ? (categoryBWeight / totalWeight) * 100f : 0f):F1}%)");
            EditorGUILayout.LabelField($"  カテゴリC ({categoryC.Count}種): {categoryCWeight:F1} ({(totalWeight > 0 ? (categoryCWeight / totalWeight) * 100f : 0f):F1}%)");

            EditorGUILayout.EndVertical();
        }

        private void DrawSkillList()
        {
            SkillCategory currentCategory = SkillCategory.CategoryA;
            bool firstCategory = true;

            foreach (var skill in allSkills)
            {
                // カテゴリ変更時にヘッダー表示
                if (skill.category != currentCategory || firstCategory)
                {
                    currentCategory = skill.category;
                    firstCategory = false;

                    EditorGUILayout.Space(10);
                    EditorGUILayout.LabelField($"■ カテゴリ{currentCategory}", EditorStyles.boldLabel);
                }

                // スキル行
                EditorGUILayout.BeginHorizontal();

                // スキル名（クリックで選択）
                if (GUILayout.Button(skill.name, EditorStyles.label, GUILayout.Width(250)))
                {
                    Selection.activeObject = skill;
                }

                // 出現率スライダー
                float newWeight = EditorGUILayout.Slider(tempWeights[skill], 0f, 100f);
                if (newWeight != tempWeights[skill])
                {
                    tempWeights[skill] = newWeight;
                }

                // 割合表示
                float totalWeight = tempWeights.Values.Sum();
                float percentage = totalWeight > 0 ? (tempWeights[skill] / totalWeight) * 100f : 0f;
                EditorGUILayout.LabelField($"{tempWeights[skill]:F1} ({percentage:F1}%)", GUILayout.Width(100));

                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawButtons()
        {
            EditorGUILayout.BeginHorizontal();

            // 適用ボタン
            if (GUILayout.Button("適用", GUILayout.Height(30)))
            {
                ApplyWeights();
            }

            // リセットボタン
            if (GUILayout.Button("リセット", GUILayout.Height(30)))
            {
                LoadSkills();
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            EditorGUILayout.BeginHorizontal();

            // 均等配分ボタン
            if (GUILayout.Button("均等配分 (4.76% ずつ)", GUILayout.Height(25)))
            {
                if (EditorUtility.DisplayDialog("確認", "全スキルを均等配分しますか？", "はい", "キャンセル"))
                {
                    SetEqualWeights();
                }
            }

            // カテゴリ別均等配分ボタン
            if (GUILayout.Button("カテゴリ別均等配分", GUILayout.Height(25)))
            {
                if (EditorUtility.DisplayDialog("確認", "各カテゴリ内で均等配分しますか？", "はい", "キャンセル"))
                {
                    SetCategoryEqualWeights();
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        private void ApplyWeights()
        {
            foreach (var skill in allSkills)
            {
                // Stage1とStage2の両方を保存
                if (tempWeightsStage1.ContainsKey(skill))
                {
                    skill.spawnWeight = tempWeightsStage1[skill];
                }
                if (tempWeightsStage2.ContainsKey(skill))
                {
                    skill.spawnWeightStage2 = tempWeightsStage2[skill];
                }
                EditorUtility.SetDirty(skill);
            }

            AssetDatabase.SaveAssets();
            string stageName = selectedStageTab == 0 ? "Stage1" : "Stage2";
            Debug.Log($"[SkillSpawnRateEditor] {stageName}の全{allSkills.Length}スキルの出現率を適用しました");
        }

        private void SetEqualWeights()
        {
            float equalWeight = 100f / allSkills.Length;

            var targetDict = selectedStageTab == 0 ? tempWeightsStage1 : tempWeightsStage2;
            foreach (var skill in allSkills)
            {
                targetDict[skill] = equalWeight;
            }

            string stageName = selectedStageTab == 0 ? "Stage1" : "Stage2";
            Debug.Log($"[SkillSpawnRateEditor] {stageName}の均等配分を設定しました ({equalWeight:F2}% ずつ)");
        }

        private void SetCategoryEqualWeights()
        {
            var categoryA = allSkills.Where(s => s.category == SkillCategory.CategoryA).ToList();
            var categoryB = allSkills.Where(s => s.category == SkillCategory.CategoryB).ToList();
            var categoryC = allSkills.Where(s => s.category == SkillCategory.CategoryC).ToList();

            // カテゴリ別に均等配分（仮に33.3%ずつ配分）
            float categoryATotal = 33.3f;
            float categoryBTotal = 33.3f;
            float categoryCTotal = 33.4f; // 合計100%になるように調整

            float categoryAWeight = categoryATotal / categoryA.Count;
            float categoryBWeight = categoryBTotal / categoryB.Count;
            float categoryCWeight = categoryCTotal / categoryC.Count;

            var targetDict = selectedStageTab == 0 ? tempWeightsStage1 : tempWeightsStage2;

            foreach (var skill in categoryA)
            {
                targetDict[skill] = categoryAWeight;
            }

            foreach (var skill in categoryB)
            {
                targetDict[skill] = categoryBWeight;
            }

            foreach (var skill in categoryC)
            {
                targetDict[skill] = categoryCWeight;
            }

            string stageName = selectedStageTab == 0 ? "Stage1" : "Stage2";
            Debug.Log($"[SkillSpawnRateEditor] {stageName}のカテゴリ別均等配分を設定しました");
        }
    }
}
