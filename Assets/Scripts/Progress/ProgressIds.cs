using System;

namespace Game.Progress
{
    // ID を文字列で管理（増減が容易）
    public static class ProgressIds
    {
        public const string Area_01 = "Area_01";
        public const string Area_02 = "Area_02";
        // 必要になったら増やす
    }

    /// <summary>
    /// Area ごとのステージ番号一覧を返す静的ユーティリティ。
    /// 旧: ProgressIds.StageCatalog → 新: ProgressIds.StageIndex
    /// </summary>
    public static class StageIndex
    {
        /// <summary>Area ID に対応するステージ番号配列を返す</summary>
        public static int[] GetStagesForArea(string areaId)
        {
            switch (areaId)
            {
                case ProgressIds.Area_01: return new[] { 1, 2, 3 };
                case ProgressIds.Area_02: return new[] { 1, 2, 3, 4 };
                default: return Array.Empty<int>();
            }
        }
    }
}
