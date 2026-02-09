using UnityEngine;
using Game.Skills;
using System.Collections.Generic;

namespace Game.Testing
{
    /// <summary>
    /// スキルテスト用のプリセット（保存/読み込み用ScriptableObject）
    /// </summary>
    [CreateAssetMenu(fileName = "SkillTestPreset_", menuName = "Game/Testing/Skill Test Preset", order = 1)]
    public class SkillTestPreset : ScriptableObject
    {
        [System.Serializable]
        public class SkillLevelData
        {
            public string skillAssetName; // スキルアセット名（例: "Skill_A1_LeftMaxCostUp"）
            public int level; // 取得回数（0 = 未取得）
        }

        [Header("Preset Info")]
        [TextArea(2, 3)]
        public string description = "スキルテスト用プリセット";

        [Header("Skill Levels")]
        public List<SkillLevelData> skillLevels = new List<SkillLevelData>();
    }
}
