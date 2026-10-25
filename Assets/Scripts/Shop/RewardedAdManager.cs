using System.Collections;
using GoogleMobileAds.Api;
using UnityEngine;

namespace Game.Shop
{
    /// <summary>
    /// リワード広告（AdMob）（2026/10/23）。ゲーム起動時に自動で1つだけ作られ、シーンをまたいで動く。
    /// 起動時に広告を1本準備しておき、ShowForReward で再生する。最後まで見たら onRewarded、見られなかったら onFailed。
    /// 再生が終わったら次の1本を準備し直す。プレミアムパスを持っている人には広告を準備しない。
    /// ヨーロッパの利用者の同意の確認（AdConsentManager）が終わってから、AdMob を初期化する（2026/10/24）。
    /// </summary>
    public class RewardedAdManager : MonoBehaviour
    {
        public static RewardedAdManager Instance { get; private set; }

        private RewardedAd ad;
        private bool initialized;
        private bool loading;
        private AdSettings settings;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreate()
        {
            if (Instance != null) return;
            var go = new GameObject("RewardedAdManager");
            DontDestroyOnLoad(go);
            go.AddComponent<RewardedAdManager>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            settings = AdSettings.Load();
            if (IapManager.HasPremiumPass) return; // プレミアムパスなら広告は使わない
            AdConsentManager.OnReady += InitializeIfAllowed;
            InitializeIfAllowed();
        }

        private void OnDestroy()
        {
            AdConsentManager.OnReady -= InitializeIfAllowed;
        }

        private bool initializing;

        // 同意の確認が終わっていて、広告を出してよい時だけ AdMob を初期化する
        private void InitializeIfAllowed()
        {
            if (initialized || initializing || !AdConsentManager.CanRequestAds) return;
            initializing = true;
            MobileAds.Initialize(_ =>
            {
                initialized = true;
                initializing = false;
                Debug.Log("[RewardedAdManager] AdMob を初期化しました");
                Load();
            });
        }

        public bool IsReady => ad != null && ad.CanShowAd();

        private void Load()
        {
            if (!initialized || loading || IsReady) return;
            loading = true;
            RewardedAd.Load(settings.RewardedUnitId, new AdRequest(), (RewardedAd loaded, LoadAdError error) =>
            {
                loading = false;
                if (error != null || loaded == null)
                {
                    Debug.LogWarning($"[RewardedAdManager] 広告の読み込みに失敗：{error}");
                    return;
                }
                ad = loaded;
                Debug.Log("[RewardedAdManager] 広告を準備しました");
            });
        }

        /// <summary>
        /// 広告を再生する。準備できていなければ最大 maxWaitSeconds 待つ（その間 onWaiting(true)）。
        /// 最後まで見たら onRewarded。見られなかったら onFailed（引数 true＝広告を準備・再生できなかった、false＝途中で閉じた）。
        /// </summary>
        public void ShowForReward(System.Action onRewarded, System.Action<bool> onFailed, System.Action<bool> onWaiting = null)
        {
            StartCoroutine(ShowRoutine(onRewarded, onFailed, onWaiting));
        }

        private IEnumerator ShowRoutine(System.Action onRewarded, System.Action<bool> onFailed, System.Action<bool> onWaiting)
        {
            if (!initialized) InitializeIfAllowed(); // 同意の確認がまだなら、終わるのを下で待つ
            else Load();

            if (!IsReady)
            {
                onWaiting?.Invoke(true);
                float t = 0f;
                while (!IsReady && t < settings.maxWaitSeconds)
                {
                    t += Time.unscaledDeltaTime;
                    if (!initialized) InitializeIfAllowed();
                    else if (!loading) Load();
                    yield return null;
                }
                onWaiting?.Invoke(false);
            }
            if (!IsReady) { onFailed?.Invoke(true); yield break; }

            bool rewarded = false;
            var showing = ad;
            ad = null;
            showing.OnAdFullScreenContentClosed += () => MainThread(() =>
            {
                showing.Destroy();
                if (rewarded) onRewarded?.Invoke(); else onFailed?.Invoke(false);
                Load();
            });
            showing.OnAdFullScreenContentFailed += err => MainThread(() =>
            {
                Debug.LogWarning($"[RewardedAdManager] 広告の再生に失敗：{err}");
                showing.Destroy();
                onFailed?.Invoke(true);
                Load();
            });
            showing.Show(_ => rewarded = true);
        }

        // 広告SDKのコールバックはメインスレッド以外で呼ばれることがあるため、次のフレームでメインスレッドに戻して実行する
        private readonly System.Collections.Concurrent.ConcurrentQueue<System.Action> mainThreadQueue = new System.Collections.Concurrent.ConcurrentQueue<System.Action>();
        private void MainThread(System.Action a) => mainThreadQueue.Enqueue(a);
        private void Update()
        {
            while (mainThreadQueue.TryDequeue(out var a)) a?.Invoke();
        }
    }
}
