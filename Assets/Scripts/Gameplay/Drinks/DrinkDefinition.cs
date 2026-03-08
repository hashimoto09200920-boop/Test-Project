using UnityEngine;
using Game.Skills;

namespace Game.Shop
{
    /// <summary>
    /// ショップで販売するドリンクの定義 ScriptableObject
    /// Resources/GameData/Drinks/ に配置する
    /// </summary>
    [CreateAssetMenu(fileName = "Drink_", menuName = "Game/Shop/DrinkDefinition")]
    public class DrinkDefinition : ScriptableObject
    {
        [Header("基本情報")]
        [Tooltip("ショップに表示するドリンク名")]
        public string drinkName = "ドリンク";

        [Tooltip("ショップに表示する説明文（空欄の場合は自動生成）")]
        [TextArea(2, 4)]
        public string description = "";

        [Tooltip("ドリンクのアイコン画像")]
        public Sprite icon;

        [Header("効果 - 対象スキル（最大3つ）")]
        [Tooltip("候補スキル 枠1")]
        public SkillDefinition targetSkill1;

        [Tooltip("候補スキル 枠2（空欄でもOK）")]
        public SkillDefinition targetSkill2;

        [Tooltip("候補スキル 枠3（空欄でもOK）")]
        public SkillDefinition targetSkill3;

        [Tooltip("設定した候補スキルの中からランダムで選ぶ数\n候補数より大きい場合は全候補を選択")]
        [Min(1)]
        public int selectionCount = 1;

        [Tooltip("選択された各スキルを何レベルアップするか")]
        [Min(1)]
        public int levelUpCount = 1;

        [Header("価格")]
        [Tooltip("購入価格（永続ゴールド）")]
        [Min(0)]
        public int price = 100;
    }
}
