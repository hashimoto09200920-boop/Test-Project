using System.Collections.Generic;
using UnityEngine;

namespace Game.Progress
{
    [CreateAssetMenu(fileName = "AreaCatalog", menuName = "Game/Progress/AreaCatalog", order = 11)]
    public class AreaCatalog : ScriptableObject
    {
        [Tooltip("ゲーム内に存在する Area 一覧")]
        public List<AreaDef> areas = new List<AreaDef>();
    }
}
