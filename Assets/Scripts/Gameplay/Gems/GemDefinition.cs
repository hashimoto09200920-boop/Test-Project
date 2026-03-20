using System;
using UnityEngine;
using Game.Skills;

namespace Game.Gems
{
    /// <summary>
    /// スキルカテゴリの複数選択用 Flags 列挙体
    /// Inspector でチェックボックス複数選択が可能
    /// </summary>
    [Flags]
    public enum SkillCategoryFlags
    {
        None      = 0,
        CategoryA = 1 << 0,
        CategoryB = 1 << 1,
        CategoryC = 1 << 2,
    }

    /// <summary>
    /// ジェムの入手エリア
    /// </summary>
    public enum GemAreaId
    {
        Area_01 = 1,
        Area_02 = 2,
        Area_03 = 3,
        Area_04 = 4,
        Area_05 = 5,
        Area_06 = 6,
        Area_07 = 7,
        Area_08 = 8,
        Area_09 = 9,
    }

    /// <summary>
    /// ジェムの定義（ScriptableObject）
    /// Inspector で全パラメータを調整可能
    /// </summary>
    [CreateAssetMenu(fileName = "Gem_", menuName = "Game/Gems/Gem Definition", order = 1)]
    public class GemDefinition : ScriptableObject
    {
        [Header("Basic Info")]
        [Tooltip("ジェムの表示名")]
        public string gemName = "New Gem";

        [Tooltip("ジェムのアイコン画像")]
        public Sprite icon;

        [Header("Base Skill")]
        [Tooltip("このジェムに常に付属する基本スキル（必ず1つ付与、+1取得回数）")]
        public SkillDefinition baseSkill;

        [Header("Bonus Skill 1")]
        [Tooltip("追加スキル1のカテゴリ（複数選択可）\n選択したカテゴリのスキルからランダムに1つ付与")]
        public SkillCategoryFlags bonusSkill1Category = SkillCategoryFlags.None;

        [Tooltip("追加スキル1の付与確率（0〜100%）")]
        [Range(0f, 100f)]
        public float bonusSkill1Chance = 50f;

        [Header("Bonus Skill 2")]
        [Tooltip("追加スキル2のカテゴリ（複数選択可）\n選択したカテゴリのスキルからランダムに1つ付与")]
        public SkillCategoryFlags bonusSkill2Category = SkillCategoryFlags.None;

        [Tooltip("追加スキル2の付与確率（0〜100%）")]
        [Range(0f, 100f)]
        public float bonusSkill2Chance = 25f;

        [Header("Slot & Economy")]
        [Tooltip("装備するために必要なスロット数（1〜3）")]
        [Range(1, 3)]
        public int requiredSlots = 1;

        [Tooltip("売却時の獲得ゴールド")]
        [Range(0, 9999)]
        public int sellPrice = 100;

        [Header("Area")]
        [Tooltip("このジェムが入手できるエリア（Stage3クリア時に入手）")]
        public GemAreaId dropArea = GemAreaId.Area_01;
    }
}
