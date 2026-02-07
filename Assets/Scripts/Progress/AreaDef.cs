using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Progress
{
    [Serializable]
    public class AreaStage
    {
        [Tooltip("ステージ番号（例：1, 2, 3）")]
        public int number = 1;

        [Tooltip("UI等に出す表示名（例：Stage 1）")]
        public string displayName = "Stage 1";
    }

    [CreateAssetMenu(fileName = "AreaDef", menuName = "Game/Progress/AreaDef", order = 10)]
    public class AreaDef : ScriptableObject
    {
        [Header("識別子")]
        [Tooltip("Area の内部ID（例：Area_01）。Progress 上のIDと一致させること。")]
        public string areaId = "Area_01";

        [Header("この Area に含まれるステージ")]
        public List<AreaStage> stages = new List<AreaStage>
        {
            new AreaStage{ number = 1, displayName = "Stage 1" },
            new AreaStage{ number = 2, displayName = "Stage 2" },
            new AreaStage{ number = 3, displayName = "Stage 3" },
        };

        [Header("この Area 自体の解放条件（任意）")]
        [Tooltip("全部満たしたらこの Area が解放される（AND条件）。空なら常時解放。")]
        public List<StageUnlockCond> unlockByStages = new List<StageUnlockCond>();
    }
}
