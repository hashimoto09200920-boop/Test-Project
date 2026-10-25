using System.Collections.Generic;
using System.Linq;
using GoogleMobileAds.Ump.Api;
using UnityEngine;

namespace Game.Shop
{
    /// <summary>
    /// 広告の同意（Google の UMP）（2026/10/24）。ゲーム起動時に自動で1つだけ作られ、シーンをまたいで動く。
    /// ・ヨーロッパ（EU・イギリス・スイス）の利用者には、最初の1回だけ「広告のためにデータを使ってよいか」の同意画面を出す
    ///   （画面の文面は AdMob の「プライバシーとメッセージ」で作ったもの）。それ以外の国の利用者には何も出ない
    /// ・同意の確認が終わるまで広告を準備しない（RewardedAdManager は IsReady／OnReady を待ってから AdMob を初期化する）
    /// ・同意をあとから変える画面（プライバシー設定）が必要な利用者かどうかを PrivacyOptionsRequired で返す（タイトルのサウンド設定のボタン用）
    /// プレミアムパスを持っている人は広告を見ないので、同意の確認もしない。
    /// </summary>
    public class AdConsentManager : MonoBehaviour
    {
        public static AdConsentManager Instance { get; private set; }

        /// <summary>同意の確認が終わった（同意画面を出した場合は閉じた後）</summary>
        public static bool IsReady { get; private set; }

        /// <summary>広告を準備してよいか（同意の確認が終わっていて、Google の判定で広告を出せる）</summary>
        public static bool CanRequestAds => IsReady && ConsentInformation.CanRequestAds();

        /// <summary>同意をあとから変える画面（プライバシー設定）のボタンを出す必要があるか（主にヨーロッパの利用者）</summary>
        public static bool PrivacyOptionsRequired =>
            IsReady && ConsentInformation.PrivacyOptionsRequirementStatus == PrivacyOptionsRequirementStatus.Required;

        /// <summary>同意の確認が終わった時・同意が変わった時</summary>
        public static event System.Action OnReady;

        private readonly System.Collections.Concurrent.ConcurrentQueue<System.Action> mainThread = new System.Collections.Concurrent.ConcurrentQueue<System.Action>();
        private bool gathering;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreate()
        {
            if (Instance != null) return;
            var go = new GameObject("AdConsentManager");
            DontDestroyOnLoad(go);
            go.AddComponent<AdConsentManager>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            // プレミアムパスなら広告を見ないので確認しない。ただしテスト用の設定（Consent Debug EEA）が ON の時は、
            // ベータ用（スタミナ無制限＝プレミアム扱い）の実機でも同意画面を確かめられるよう確認する
            if (IapManager.HasPremiumPass && !AdSettings.Load().ConsentDebugEnabled) { Debug.Log("[AdConsentManager] プレミアムパスのため、広告の同意の確認はしません"); return; }
            Gather();
        }

        /// <summary>同意の状態を Google に問い合わせ、必要なら同意画面を出す</summary>
        public void Gather()
        {
            if (gathering) return;
            gathering = true;
            var settings = AdSettings.Load();
            var request = new ConsentRequestParameters { TagForUnderAgeOfConsent = false };
            if (settings.ConsentDebugEnabled)
            {
                // テスト用：ヨーロッパにいるものとして同意画面を確認する（Use Test Ads が ON の間だけ）
                request.ConsentDebugSettings = new ConsentDebugSettings
                {
                    DebugGeography = DebugGeography.EEA,
                    // 貼り付けた時に前後に空白が入っても一致するよう、空白を取り除く
                    TestDeviceHashedIds = (settings.consentTestDeviceHashedIds ?? new string[0]).Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).ToList(),
                };
                // 前回の判定（例：日本＝同意画面は不要）が端末に残っていると、それが使われて同意画面が出ないため、テスト中は毎回消してから判定する
                ConsentInformation.Reset();
                Debug.Log("[AdConsentManager] テスト用：ヨーロッパにいるものとして同意を確認します");
            }

            ConsentInformation.Update(request, updateError => mainThread.Enqueue(() =>
            {
                if (updateError != null)
                {
                    // 通信が無いなど。前回の同意の状態で続ける（広告を出せるかは CanRequestAds が判定する）
                    Debug.LogWarning($"[AdConsentManager] 同意の状態を取得できませんでした：{updateError.Message}");
                    Finish();
                    return;
                }
                ConsentForm.LoadAndShowConsentFormIfRequired(formError => mainThread.Enqueue(() =>
                {
                    if (formError != null) Debug.LogWarning($"[AdConsentManager] 同意画面を出せませんでした：{formError.Message}");
                    Finish();
                }));
            }));
        }

        private void Finish()
        {
            gathering = false;
            IsReady = true;
            Debug.Log($"[AdConsentManager] 同意の確認が終わりました（広告を準備できる={ConsentInformation.CanRequestAds()}、プライバシー設定のボタンが必要={ConsentInformation.PrivacyOptionsRequirementStatus == PrivacyOptionsRequirementStatus.Required}）");
            OnReady?.Invoke();
        }

        /// <summary>同意をあとから変える画面（プライバシー設定）を出す。閉じたら done(true)、出せなかったら done(false)</summary>
        public void ShowPrivacyOptions(System.Action<bool> done = null)
        {
            ConsentForm.ShowPrivacyOptionsForm(error => mainThread.Enqueue(() =>
            {
                if (error != null) Debug.LogWarning($"[AdConsentManager] プライバシー設定の画面を出せませんでした：{error.Message}");
                else OnReady?.Invoke(); // 同意が変わった可能性があるので知らせる
                done?.Invoke(error == null);
            }));
        }

        // UMP の知らせはメインスレッド以外で届くことがあるため、次のフレームでメインスレッドに戻して実行する
        private void Update()
        {
            while (mainThread.TryDequeue(out var a)) a?.Invoke();
        }

#if UNITY_EDITOR
        [UnityEditor.MenuItem("Tools/ショップ/広告の同意をリセット（テスト用）")]
        private static void ResetConsentForTest()
        {
            ConsentInformation.Reset();
            Debug.Log("[AdConsentManager] 広告の同意をリセットしました。次に Play した時、もう一度同意画面の判定をします");
        }
#endif
    }
}
