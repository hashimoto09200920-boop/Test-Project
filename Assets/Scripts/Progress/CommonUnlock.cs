using System;
using UnityEngine;

namespace Game.Progress
{
    /// <summary>
    /// 「特定エリアの特定ステージをクリア」を表す解放条件。
    /// Area/Unit の両方から共通で参照します。
    /// </summary>
    [Serializable]
    public class StageUnlockCond
    {
        [Tooltip("解放条件とする Area ID（例：Area_01）")]
        public string areaId = AreaIds.Area_01;

        [Tooltip("解放条件とする Stage 番号（例：1）")]
        public int stageNumber = 1;
    }
}
