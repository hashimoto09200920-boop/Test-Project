using System;
using UnityEngine;

namespace Game.Progress
{
    /// <summary>
    /// 個々のステージ定義（ScriptableObject）。
    /// 必須：areaId + stageNumber（キー）
    /// メタ：表示名・説明・難易度・報酬・タグ・ボスフラグなど
    /// </summary>
    [CreateAssetMenu(
        fileName = "StageDef_",
        menuName = "GameData/StageDef",
        order = 1200)]
    public class StageDef : ScriptableObject
    {
        [Header("識別子（キー）")]
        [Tooltip("Area_01 / Area_02 など ProgressIds.Area_* に合わせる")]
        public string areaId;

        [Tooltip("ステージ番号（Area 内で一意）")]
        public int stageNumber = 1;

        [Header("表示")]
        public string displayName = "Stage 1";
        [TextArea(2, 4)]
        public string description = "";

        [Header("メタ情報（任意）")]
        [Tooltip("推奨戦力や推奨レベルなどの目安")]
        public int recommendedLevel = 1;

        [Tooltip("想定クリア報酬（目安のGold）")]
        public int rewardGold = 0;

        [Tooltip("任意タグ（例：Tutorial/Boss/Timed など）")]
        public string[] tags;

        [Tooltip("ボス戦かどうか")]
        public bool isBoss = false;

        // キーとして扱う複合ID（ログや辞書キーに使う）
        public string GetKey() => $"{areaId}#{stageNumber}";

        private void OnValidate()
        {
            if (stageNumber < 1) stageNumber = 1;
            if (recommendedLevel < 1) recommendedLevel = 1;
            if (rewardGold < 0) rewardGold = 0;
        }
    }
}
