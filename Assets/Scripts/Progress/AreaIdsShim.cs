using System;

namespace Game.Progress
{
    /// <summary>
    /// 互換レイヤ（Shim）。
    /// 既存コードが参照している AreaIds を ProgressIds に委譲することで、
    /// 大量のファイルを触らずにビルドエラーを解消する。
    /// </summary>
    public static class AreaIds
    {
        public const string Area_01 = ProgressIds.Area_01;
        public const string Area_02 = ProgressIds.Area_02;
        // 必要に応じて増やすときは ProgressIds 側を先に追加し、ここで委譲定義を足す。
    }
}
