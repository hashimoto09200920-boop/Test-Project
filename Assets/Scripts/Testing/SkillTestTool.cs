using UnityEngine;
using Game.Skills;
using System.Collections.Generic;
using System.Linq;

namespace Game.Testing
{
    /// <summary>
    /// スキルテスト用ツール
    /// - 全スキルのレベルを一括設定
    /// - プリセット保存/読み込み
    /// - リアルタイム適用（プレイ中）
    /// </summary>
    public class SkillTestTool : MonoBehaviour
    {
        [System.Serializable]
        public class SkillLevelSetting
        {
            public SkillDefinition skill;
            [Range(0, 10)] public int level = 0; // 0 = 未取得, 1-10 = 取得回数

            // リアルタイム適用用（内部）
            [HideInInspector] public int lastAppliedLevel = -1;
        }

        [Header("Category A (攻撃・リソース系)")]
        public List<SkillLevelSetting> categoryA = new List<SkillLevelSetting>();

        [Header("Category B (防御・耐久系)")]
        public List<SkillLevelSetting> categoryB = new List<SkillLevelSetting>();

        [Header("Category C (特殊効果系)")]
        public List<SkillLevelSetting> categoryC = new List<SkillLevelSetting>();

        [Header("Settings")]
        [SerializeField] private bool applyOnStart = true;
        [SerializeField] private bool realtimeApply = true; // プレイ中にスライダー変更で即座に反映

        [Header("Preset")]
        [SerializeField] private SkillTestPreset currentPreset;

        private void Start()
        {
            if (applyOnStart)
            {
                ApplyAllSkills();
            }
        }

        private void Update()
        {
            // リアルタイム適用（プレイ中のみ）
            if (realtimeApply && Application.isPlaying)
            {
                CheckAndApplyChanges();
            }
        }

        /// <summary>
        /// スライダー変更を検知して差分のみ適用
        /// </summary>
        private void CheckAndApplyChanges()
        {
            var allSettings = categoryA.Concat(categoryB).Concat(categoryC);

            foreach (var setting in allSettings)
            {
                if (setting.skill == null) continue;

                // レベルが変更された場合のみ適用
                if (setting.level != setting.lastAppliedLevel)
                {
                    ApplySingleSkillDelta(setting);
                    setting.lastAppliedLevel = setting.level;
                }
            }
        }

        /// <summary>
        /// 単一スキルの差分を適用
        /// </summary>
        private void ApplySingleSkillDelta(SkillLevelSetting setting)
        {
            var manager = SkillManager.Instance;
            if (manager == null) return;

            int currentCount = manager.GetSkillAcquisitionCount(setting.skill);
            int targetLevel = setting.level;

            if (targetLevel > currentCount)
            {
                // レベルアップ：差分だけ追加
                for (int i = currentCount; i < targetLevel; i++)
                {
                    manager.AddSkill(setting.skill);
                }
                Debug.Log($"[SkillTestTool] Added {setting.skill.skillName} (Level: {currentCount} → {targetLevel})");
            }
            else if (targetLevel < currentCount)
            {
                // レベルダウン：全スキルをクリアして再適用
                Debug.Log($"[SkillTestTool] Level decreased detected. Re-applying all skills...");
                ApplyAllSkills();
            }
        }

        /// <summary>
        /// 全スキルを適用（Context Menuから手動実行可能）
        /// </summary>
        [ContextMenu("Apply All Skills")]
        public void ApplyAllSkills()
        {
            var manager = SkillManager.Instance;
            if (manager == null)
            {
                Debug.LogWarning("[SkillTestTool] SkillManager not found. Please start Play mode.");
                return;
            }

            // 全スキルをクリア
            manager.ClearAllSkills();

            // 各カテゴリのスキルを適用
            ApplyCategory(categoryA);
            ApplyCategory(categoryB);
            ApplyCategory(categoryC);

            // 適用済みレベルを更新
            UpdateLastAppliedLevels();

            Debug.Log("[SkillTestTool] Applied all skills");
        }

        private void ApplyCategory(List<SkillLevelSetting> settings)
        {
            var manager = SkillManager.Instance;
            if (manager == null) return;

            foreach (var setting in settings)
            {
                if (setting.skill == null) continue;

                for (int i = 0; i < setting.level; i++)
                {
                    manager.AddSkill(setting.skill);
                }
            }
        }

        private void UpdateLastAppliedLevels()
        {
            foreach (var setting in categoryA) setting.lastAppliedLevel = setting.level;
            foreach (var setting in categoryB) setting.lastAppliedLevel = setting.level;
            foreach (var setting in categoryC) setting.lastAppliedLevel = setting.level;
        }

        /// <summary>
        /// 全スキルレベルをリセット
        /// </summary>
        [ContextMenu("Reset All Levels")]
        public void ResetAllLevels()
        {
            foreach (var setting in categoryA) setting.level = 0;
            foreach (var setting in categoryB) setting.level = 0;
            foreach (var setting in categoryC) setting.level = 0;

            if (Application.isPlaying)
            {
                ApplyAllSkills();
            }

            Debug.Log("[SkillTestTool] Reset all levels to 0");
        }

        /// <summary>
        /// Resources配下の全スキルを自動追加
        /// </summary>
        [ContextMenu("Auto-populate All Skills")]
        public void AutoPopulateSkills()
        {
            categoryA.Clear();
            categoryB.Clear();
            categoryC.Clear();

            var allSkills = Resources.LoadAll<SkillDefinition>("GameData/Skills");

            foreach (var skill in allSkills)
            {
                var setting = new SkillLevelSetting { skill = skill, level = 0, lastAppliedLevel = 0 };

                switch (skill.category)
                {
                    case SkillCategory.CategoryA:
                        categoryA.Add(setting);
                        break;
                    case SkillCategory.CategoryB:
                        categoryB.Add(setting);
                        break;
                    case SkillCategory.CategoryC:
                        categoryC.Add(setting);
                        break;
                }
            }

            // スキル名でソート
            categoryA = categoryA.OrderBy(s => s.skill.name).ToList();
            categoryB = categoryB.OrderBy(s => s.skill.name).ToList();
            categoryC = categoryC.OrderBy(s => s.skill.name).ToList();

            Debug.Log($"[SkillTestTool] Auto-populated: A={categoryA.Count}, B={categoryB.Count}, C={categoryC.Count}");
        }

        /// <summary>
        /// 現在の設定をプリセットに保存
        /// </summary>
        [ContextMenu("Save to Preset")]
        public void SaveToPreset()
        {
            if (currentPreset == null)
            {
                Debug.LogWarning("[SkillTestTool] No preset assigned. Create a preset first.");
                return;
            }

            currentPreset.skillLevels.Clear();

            var allSettings = categoryA.Concat(categoryB).Concat(categoryC);
            foreach (var setting in allSettings)
            {
                if (setting.skill == null) continue;

                currentPreset.skillLevels.Add(new SkillTestPreset.SkillLevelData
                {
                    skillAssetName = setting.skill.name,
                    level = setting.level
                });
            }

#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(currentPreset);
#endif

            Debug.Log($"[SkillTestTool] Saved to preset: {currentPreset.name}");
        }

        /// <summary>
        /// プリセットから設定を読み込み
        /// </summary>
        [ContextMenu("Load from Preset")]
        public void LoadFromPreset()
        {
            if (currentPreset == null)
            {
                Debug.LogWarning("[SkillTestTool] No preset assigned.");
                return;
            }

            // まず全レベルをリセット
            foreach (var setting in categoryA) setting.level = 0;
            foreach (var setting in categoryB) setting.level = 0;
            foreach (var setting in categoryC) setting.level = 0;

            // プリセットから読み込み
            foreach (var data in currentPreset.skillLevels)
            {
                var allSettings = categoryA.Concat(categoryB).Concat(categoryC);
                var setting = allSettings.FirstOrDefault(s => s.skill != null && s.skill.name == data.skillAssetName);

                if (setting != null)
                {
                    setting.level = data.level;
                }
            }

            if (Application.isPlaying)
            {
                ApplyAllSkills();
            }

            Debug.Log($"[SkillTestTool] Loaded from preset: {currentPreset.name}");
        }
    }
}
