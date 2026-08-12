using System;
using System.Collections.Generic;

namespace Game.Progress
{
    [Serializable]
    public class ProgressData
    {
        public int version = 1;

        // ジェム装備スロットレベル（初期1、各AreaのStage3初回クリアごとに+1、最大10）
        public int slotLevel = 1;

        // 所持ジェムリスト（上限30個）
        public List<GemInstance> gemInventory = new List<GemInstance>();

        // 装備中ジェムのインベントリインデックスリスト
        public List<int> equippedGemIndices = new List<int>();

        // ジェム使用回数システムの遡及マイグレーション済みフラグ（初回のみtrueにする）
        public bool gemUsesMigrated = false;

        // 課金：ジェム使用回数無制限フラグ（購入すると true、以後ジェムの使用回数を一切消費しない）
        public bool hasUnlimitedGemUses = false;

        // 従来の選択中データ
        public string selectedAreaId  = ProgressIds.Area_01;
        public string unitBasicId     = "Unit_01";
        public string unitRelicId     = "Unit_01";
        public int    gold            = 0;
        public string selectedRelicId = "Relic_00";

        // ★所持している Unit 一覧（重複なし）
        public List<string> ownedBasicUnitIds = new List<string>();
        public List<string> ownedRelicUnitIds = new List<string>();

        // Area/Stage のクリア進捗
        public List<AreaProgress> areas = new List<AreaProgress>();

        public AreaProgress GetOrCreateArea(string areaId)
        {
            var ap = areas.Find(a => a.areaId == areaId);
            if (ap == null)
            {
                ap = new AreaProgress { areaId = areaId };
                areas.Add(ap);
            }
            return ap;
        }

        // ★追加：クリア済みステージ一覧を返す
        public List<int> GetClearedStages(string areaId)
        {
            return GetOrCreateArea(areaId).clearedStages;
        }
    }

    [Serializable]
    public class AreaProgress
    {
        public string areaId;
        public List<int> clearedStages = new List<int>();

        // このAreaでクリア時に達成した過去最高ランク（S/A/B/C/D/E、未達成なら空文字）
        public string bestRank = "";

        public bool IsCleared(int stage) => clearedStages.Contains(stage);

        public bool MarkCleared(int stage)
        {
            if (clearedStages.Contains(stage)) return false;
            clearedStages.Add(stage);
            return true;
        }
    }
}
