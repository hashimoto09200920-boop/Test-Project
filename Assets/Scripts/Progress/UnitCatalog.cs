using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Progress
{
    public enum UnitType
    {
        Basic,
        Relic
    }

    [CreateAssetMenu(fileName = "UnitDef", menuName = "Game/Progress/UnitDef", order = 20)]
    public class UnitDef : ScriptableObject
    {
        [Header("ID と表示名")]
        [Tooltip("内部ID（例：UB1, UR1）。Progress の owned～ と一致させる。")]
        public string unitId = "UB1";

        [Tooltip("UI 等に表示する名称（例：Swordman, Relic Golem など）")]
        public string displayName = "Unit Name";

        [Header("種別")]
        public UnitType unitType = UnitType.Basic;

        [Header("価格（Shop/Relic 購入用・任意）")]
        [Min(0)]
        public int priceGold = 0;

        [Header("解放条件（任意）：特定ステージをクリアで解放など（AND条件）")]
        public List<StageUnlockCond> unlockByStages = new List<StageUnlockCond>();

        // 必要になれば拡張:
        // public Sprite icon;
        // public int hp, atk, def;
    }

    [CreateAssetMenu(fileName = "UnitCatalog", menuName = "Game/Progress/UnitCatalog", order = 21)]
    public class UnitCatalog : ScriptableObject
    {
        [Tooltip("ゲームに登場する全ユニット")]
        public List<UnitDef> units = new List<UnitDef>();
    }
}
