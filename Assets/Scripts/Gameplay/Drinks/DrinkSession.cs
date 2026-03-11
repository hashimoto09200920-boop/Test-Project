using System.Collections.Generic;

namespace Game.Shop
{
    /// <summary>
    /// ゲームセッション中の有効なドリンクブーストを管理する静的クラス。
    /// 購入直後のゲームプレイが終了（ゲームオーバー/Stage3クリア/Retire/終了）した後、
    /// 03_AreaSelect に戻った時点でリセットされる。
    /// </summary>
    public static class DrinkSession
    {
        // スキルのアセット名 → 追加レベル数（取得回数）
        private static readonly Dictionary<string, int> boosts = new Dictionary<string, int>();

        /// <summary>現在有効なドリンクブースト（スキルアセット名 → 追加レベル数）</summary>
        public static IReadOnlyDictionary<string, int> ActiveBoosts => boosts;

        /// <summary>このAreaSelectセッションで購入したドリンクの回数</summary>
        public static int PurchaseCount { get; private set; } = 0;

        /// <summary>ドリンクブーストを追加する（購入時に呼ぶ）</summary>
        public static void AddBoost(string skillAssetName, int count)
        {
            if (string.IsNullOrEmpty(skillAssetName) || count <= 0) return;
            if (!boosts.ContainsKey(skillAssetName))
                boosts[skillAssetName] = 0;
            boosts[skillAssetName] += count;
        }

        /// <summary>購入回数を1増やす（購入時に呼ぶ）</summary>
        public static void IncrementPurchaseCount()
        {
            PurchaseCount++;
        }

        /// <summary>全ブーストと購入回数をリセットする（03_AreaSelect ロード時に呼ぶ）</summary>
        public static void Reset()
        {
            boosts.Clear();
            PurchaseCount = 0;
        }

        /// <summary>現在有効なブーストがあるかどうか</summary>
        public static bool HasAnyBoost => boosts.Count > 0;
    }
}
