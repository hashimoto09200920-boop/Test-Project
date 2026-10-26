using UnityEngine;

namespace Game.Shop
{
    /// <summary>
    /// 課金ショップの商品設定（2026/10/23）。Assets/Resources/IapSettings.asset に置く。
    /// 商品ID は Play Console のアプリ内アイテムと完全に同じにすること（登録後は変更不可）。
    /// 値段は Play Console の値段が画面に出る（ここの fallbackPrice はストアから値段を取れない時だけ使う）。
    /// メニュー「Tools/ショップ/課金ショップの設定を開く（無ければ作成）」で作成・選択する。
    /// </summary>
    [CreateAssetMenu(fileName = "IapSettings", menuName = "Game/Shop/Iap Settings")]
    public class IapSettings : ScriptableObject
    {
        public const string ResourcePath = "IapSettings";

        [Header("プレミアムパス（買い切り。広告なし＋スタミナ無限）")]
        public string premiumPassId = "premium_pass";
        [Tooltip("予備の値段（日本語表示）。実機では Google Play の値段（その人の国の通貨）を出すので、これはエディタ・ストアにつながらない時だけ")]
        public string premiumPassFallbackPrice = "¥480";
        [Tooltip("予備の値段（英語表示）")]
        public string premiumPassFallbackPriceEn = "$2.99";
        [Tooltip("払い戻しなどで Google の購入履歴からプレミアムパスが消えた時、この回数だけ続けて「無い」と確認できたら端末のプレミアムパスを外す（通常は起動ごとに1回確認）。" +
                 "Google Play の一時的な不調で、買った人のプレミアムパスが誤って外れるのを防ぐため（2026/10/26）")]
        [Min(1)] public int premiumRevokeAfterMissingCount = 3;

        [Header("虹の雫（消費型）")]
        public StoneProduct[] stoneProducts =
        {
            new StoneProduct { productId = "stone_1", amount = 1, fallbackPrice = "¥160", fallbackPriceEn = "$0.99" },
            new StoneProduct { productId = "stone_3", amount = 3, fallbackPrice = "¥400", fallbackPriceEn = "$2.49" },
            new StoneProduct { productId = "stone_10", amount = 10, fallbackPrice = "¥980", fallbackPriceEn = "$5.99" },
        };

        [System.Serializable]
        public class StoneProduct
        {
            public string productId;
            [Min(1)] public int amount = 1;
            [Tooltip("予備の値段（日本語表示）")]
            public string fallbackPrice;
            [Tooltip("予備の値段（英語表示）")]
            public string fallbackPriceEn;
        }

        /// <summary>英語の予備の値段が空の時に入れる値（「課金ショップの設定を開く」メニューで補う）</summary>
        public static string DefaultFallbackPriceEn(string productId)
        {
            switch (productId)
            {
                case "premium_pass": return "$2.99";
                case "stone_1": return "$0.99";
                case "stone_3": return "$2.49";
                case "stone_10": return "$5.99";
                default: return "";
            }
        }

        /// <summary>今の言語の予備の値段（エディタ・ストアにつながらない時に表示）</summary>
        public string GetFallbackPrice(string productId)
        {
            bool en = Game.Localization.LocalizationManager.CurrentLanguageStatic == Game.Localization.GameLanguage.English;
            // 英語の欄が空なら既定のドルの値段（設定アセットに英語の欄が無い古い状態でも$で出す）
            if (productId == premiumPassId) return en ? (string.IsNullOrEmpty(premiumPassFallbackPriceEn) ? DefaultFallbackPriceEn(productId) : premiumPassFallbackPriceEn) : premiumPassFallbackPrice;
            var s = FindStone(productId);
            if (s == null) return "";
            return en ? (string.IsNullOrEmpty(s.fallbackPriceEn) ? DefaultFallbackPriceEn(productId) : s.fallbackPriceEn) : s.fallbackPrice;
        }

        private static IapSettings cached;
        public static IapSettings Load()
        {
            if (cached == null) cached = Resources.Load<IapSettings>(ResourcePath);
            if (cached == null) cached = CreateInstance<IapSettings>(); // アセットが無い時は既定値で動かす
            return cached;
        }

        public StoneProduct FindStone(string productId)
        {
            foreach (var s in stoneProducts) if (s != null && s.productId == productId) return s;
            return null;
        }
    }
}
