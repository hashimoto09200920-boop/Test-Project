using System.Linq;

namespace Game.Progress
{
    /// <summary>
    /// Area / Unit の“解放条件を満たしているか”を Progress に基づいて判定するユーティリティ。
    /// - Area の入場可否ガード
    /// - Unit の購入/選択可否ガード
    /// に使います。
    /// </summary>
    public static class UnlockRules
    {
        /// <summary>
        /// Area の解放可否を判定。
        /// - AreaDef.unlockByStages が空なら「常時解放」
        /// - 1つ以上あれば「AND 条件」（全て満たしたら解放）
        /// </summary>
        public static bool IsAreaUnlocked(string areaId)
        {
            // フェイルセーフ：データ未ロード時はロックしない
            if (AreaDB.Instance == null || AreaDB.Instance.Catalog == null) return true;

            var area = AreaDB.Instance.Catalog.areas?
                .FirstOrDefault(a => a != null && a.areaId == areaId);
            if (area == null) return true; // 未定義はロックしない

            var conds = area.unlockByStages;
            if (conds == null || conds.Count == 0) return true; // 条件なし＝常時解放

            if (ProgressManager.Instance == null) return false; // Progressが無ければ判定不能→ロック
            var pm = ProgressManager.Instance;

            // AND条件：全ての (AreaX, StageY) をクリア済みなら解放
            foreach (var c in conds)
            {
                if (string.IsNullOrEmpty(c.areaId) || c.stageNumber <= 0) return false;
                if (!pm.IsStageCleared(c.areaId, c.stageNumber)) return false;
            }
            return true;
        }

        /// <summary>
        /// Unit の“解放条件（unlockByStages）を満たしているか”を判定。
        /// ※所持済みかどうかは ProgressManager の IsBasicOwned/IsRelicOwned を使用してください。
        /// </summary>
        public static bool IsUnitUnlockedByConditions(string unitId)
        {
            if (UnitDB.Instance == null || UnitDB.Instance.Catalog == null) return true;

            var def = UnitDB.Instance.GetUnitById(unitId);
            if (def == null) return true; // 未定義はロックしない

            var conds = def.unlockByStages;
            if (conds == null || conds.Count == 0) return true;

            if (ProgressManager.Instance == null) return false;
            var pm = ProgressManager.Instance;

            foreach (var c in conds)
            {
                if (string.IsNullOrEmpty(c.areaId) || c.stageNumber <= 0) return false;
                if (!pm.IsStageCleared(c.areaId, c.stageNumber)) return false;
            }
            return true;
        }
    }
}
