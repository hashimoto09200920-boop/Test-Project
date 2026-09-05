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
        Area_10 = 10,
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
        [Tooltip("このジェムに常に付属する基本スキルのカテゴリ。入手時にこのカテゴリ内から1つランダムに選ばれ、必ず付与される（+1取得回数）。付与自体の100%は変わらず、選ばれるスキルだけがランダムになる")]
        public SkillCategory baseSkillCategory = SkillCategory.CategoryA;

        [Header("Bonus Skill 1")]
        [Tooltip("追加スキル1の付与確率（0〜100%）")]
        [Range(0f, 100f)]
        public float bonusSkill1Chance = 50f;

        [Header("Bonus Skill 2")]
        [Tooltip("追加スキル2の付与確率（0〜100%）")]
        [Range(0f, 100f)]
        public float bonusSkill2Chance = 25f;

        [Header("Bonus Skill Category Weights")]
        [Tooltip("ボーナススキル（1・2共通）の付与が確定した際、どのカテゴリから選ぶかの比率。A+B+Cの合計が100になるようにする（このジェム専用の設定。他Areaのジェムには影響しない）")]
        [Range(0f, 100f)]
        public float categoryAWeight = 33.3f;
        [Range(0f, 100f)]
        public float categoryBWeight = 33.3f;
        [Range(0f, 100f)]
        public float categoryCWeight = 33.4f;

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

        [Header("Uses (使用回数)")]
        [Tooltip("入手時点の残り使用回数の初期値。装備中のみ1プレイ毎に1減少し、0になると消滅する（回復手段なし）")]
        [Range(1, 999)]
        public int maxUses = 30;

        /// <summary>
        /// ジェム名をLocalizationManagerの現在言語で取得する。未登録/未設定時はgemName(日本語)を返す。
        /// キーはアセット名(例: Gem_Area01)を使う。
        /// </summary>
        public string GetLocalizedName()
        {
            return Game.Localization.LocalizationManager.GetStatic($"gem.{name}.name", gemName);
        }
    }
}
