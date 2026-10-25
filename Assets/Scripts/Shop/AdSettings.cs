using UnityEngine;

namespace Game.Shop
{
    /// <summary>
    /// リワード広告（AdMob）の設定（2026/10/23）。Assets/Resources/AdSettings.asset に置く。
    /// アプリ ID は Assets/GoogleMobileAds/Resources/GoogleMobileAdsSettings.asset（Assets > Google Mobile Ads > Settings）で設定する。
    /// ★開発中に本物の広告を自分で見たり押したりすると AdMob アカウントが停止されることがあるため、
    ///   「Use Test Ads」がONの間は Google のテスト用広告を出す。製品版を出す前にOFFにする（エディタでは常にテスト用）。
    /// メニュー「Tools/ショップ/広告の設定を開く（無ければ作成）」で作成・選択する。
    /// </summary>
    [CreateAssetMenu(fileName = "AdSettings", menuName = "Game/Shop/Ad Settings")]
    public class AdSettings : ScriptableObject
    {
        public const string ResourcePath = "AdSettings";
        /// <summary>Google 公式のテスト用リワード広告ユニット ID（Android）</summary>
        public const string TestRewardedUnitId = "ca-app-pub-3940256099942544/5224354917";

        [Tooltip("本番のリワード広告ユニット ID（AdMob の stamina_reward）")]
        public string rewardedUnitId = "ca-app-pub-4320979989698750/4298908390";

        [Tooltip("ONの間はテスト用の広告を出す（開発中・テスト中は必ずON。製品版を出す前にOFF）")]
        public bool useTestAds = true;

        [Header("ヨーロッパ向けの同意画面（Google の UMP）のテスト")]
        [Tooltip("ON：ヨーロッパにいるものとして同意画面を出す（テスト用。実機で Use Test Ads が ON の間だけ効く。エディタと製品版では効かない）")]
        public bool consentDebugEEA = false;
        [Tooltip("実機で同意画面をテストする時の端末の ID（Logcat に出る「Use new ConsentDebugSettings.Builder().addTestDeviceHashedId(\"…\")」の … の部分）。エディタでは不要")]
        public string[] consentTestDeviceHashedIds = new string[0];

        /// <summary>
        /// 同意画面のテスト設定を使うか（製品版＝Use Test Ads が OFF の時は必ず使わない）。
        /// エディタでは使わない（エディタの同意画面は Google の仮の画像で確認にならない上、Play のたびに出てゲームを遊べなくなるため。2026/10/25）
        /// </summary>
        public bool ConsentDebugEnabled => consentDebugEEA && useTestAds && !Application.isEditor;

        [Tooltip("広告がまだ準備できていない時に「広告を読み込み中…」で待つ最大秒数")]
        [Min(0f)] public float maxWaitSeconds = 5f;

        public string RewardedUnitId
        {
            get
            {
#if UNITY_EDITOR
                return TestRewardedUnitId;
#else
                return useTestAds ? TestRewardedUnitId : rewardedUnitId;
#endif
            }
        }

        private static AdSettings cached;
        public static AdSettings Load()
        {
            if (cached == null) cached = Resources.Load<AdSettings>(ResourcePath);
            if (cached == null) cached = CreateInstance<AdSettings>();
            return cached;
        }
    }
}
