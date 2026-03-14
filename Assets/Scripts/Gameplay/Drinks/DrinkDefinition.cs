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

        [Tooltip("アイコンの表示サイズ（0,0の場合はデフォルト160x160を使用）")]
        public Vector2 iconDisplaySize = Vector2.zero;

        [Header("効果モード")]
        [Tooltip("固定: 設定された全スキルが確定で上昇\n変動: ランダム選択")]
        public bool isFixed = true;

        [Header("効果 - 対象スキル（最大3つ）")]
        [Tooltip("候補スキル 枠1")]
        public SkillDefinition targetSkill1;

        [Tooltip("候補スキル 枠2（空欄でもOK）")]
        public SkillDefinition targetSkill2;

        [Tooltip("候補スキル 枠3（空欄でもOK）")]
        public SkillDefinition targetSkill3;

        [Header("【固定】設定")]
        [Tooltip("固定モード時: 全対象スキルの上昇ポイント")]
        public int levelUpCount = 1;

        [Header("【変動】設定")]
        [Tooltip("変動モード時: 対象スキルからランダムで選ぶ回数（重複あり）")]
        [Min(1)]
        public int selectionCount = 1;

        [Tooltip("変動モード時: 選択されたスキルの最小上昇ポイント")]
        public int minPoint = 0;

        [Tooltip("変動モード時: 選択されたスキルの最大上昇ポイント")]
        public int maxPoint = 1;

        [Header("価格")]
        [Tooltip("購入価格（永続ゴールド）")]
        [Min(0)]
        public int price = 100;

        [Header("SE")]
        [Tooltip("購入時に再生するSE（未設定の場合はShopUIのbuySEを使用）")]
        public AudioClip purchaseSE;
    }
}
