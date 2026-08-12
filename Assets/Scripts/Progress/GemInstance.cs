using System;

namespace Game.Progress
{
    /// <summary>
    /// 所持ジェムの1個分のデータ（セーブ用）
    /// ScriptableObject は JSON 保存できないため、アセット名（string）で参照する
    /// </summary>
    [Serializable]
    public class GemInstance
    {
        /// <summary>GemDefinition アセットの名前（Resources.Load のキー）</summary>
        public string gemDefinitionName = "";

        /// <summary>入手時にロールした基本スキルの SkillDefinition アセット名（GemDefinition.baseSkillCategory内からランダム選出）</summary>
        public string baseSkillName = "";

        /// <summary>入手時にロールした追加スキル1の SkillDefinition アセット名（空文字 = 付かなかった）</summary>
        public string bonusSkill1Name = "";

        /// <summary>入手時にロールした追加スキル2の SkillDefinition アセット名（空文字 = 付かなかった）</summary>
        public string bonusSkill2Name = "";

        /// <summary>残り使用回数。装備中のみ1プレイ毎に1減少し、0になると消滅する（回復手段なし）</summary>
        public int remainingUses = 0;
    }
}
