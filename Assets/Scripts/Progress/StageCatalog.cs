using System.Linq;
using UnityEngine;

namespace Game.Progress
{
    /// <summary>
    /// StageDef のカタログ（ScriptableObject）。
    /// 1つのアセットに複数の StageDef を登録して使う。
    /// </summary>
    [CreateAssetMenu(
        fileName = "StageCatalog",
        menuName = "GameData/StageCatalog",
        order = 1210)]
    public class StageCatalog : ScriptableObject
    {
        [Tooltip("登録する全ステージ定義")]
        public StageDef[] stages;

        /// <summary>ユーティリティ：キー一致検索（Editor での目視検証など向け）</summary>
        public StageDef Find(string areaId, int stageNumber)
        {
            if (stages == null) return null;
            return stages.FirstOrDefault(s =>
                s != null &&
                s.areaId == areaId &&
                s.stageNumber == stageNumber);
        }
    }
}
