using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Purchasing;

namespace Game.Shop
{
    /// <summary>
    /// Google Play の課金（Unity IAP 5.x）（2026/10/23）。ゲーム起動時に自動で1つだけ作られ、シーンをまたいで動く。
    /// ・起動時にストアへつなぎ、IapSettings の商品（プレミアムパス・虹の雫×1/×3/×10）の値段を取得する
    /// ・購入が完了したら、効果（プレミアムパス＝スタミナ無限、虹の雫＝所持数加算）を付けて保存してから、Google に受け取りを伝える（ConfirmPurchase）
    ///   → 付与の前にアプリが落ちても、次回起動時に Google から未処理の購入として届き直すので、取りこぼさない
    /// ・プレミアムパス（買い切り）は、起動時に購入履歴を取り直して自動で戻す（入れ直し・機種変更）。「購入を復元」ボタンからも取り直せる
    /// エディタでは Unity IAP の疑似ストア（Fake Store）で動くので、お金はかからない。
    /// </summary>
    public class IapManager : MonoBehaviour
    {
        public static IapManager Instance { get; private set; }

        /// <summary>ストアにつながり、商品の値段が取れた</summary>
        public bool IsReady { get; private set; }

        /// <summary>
        /// ストアへの接続・商品の取得に失敗した（通信が無い・商品が未登録など）。つなぎ直しを始めると false に戻る。
        /// 返事が来ないまま ConnectTimeoutSeconds 秒たった時も失敗とみなす
        /// </summary>
        public bool ConnectFailed => failed || (!IsReady && connectStartedAt >= 0f && Time.realtimeSinceStartup - connectStartedAt > ConnectTimeoutSeconds);
        public const float ConnectTimeoutSeconds = 10f;
        private bool failed;
        private float connectStartedAt = -1f;

        /// <summary>商品の値段などが更新された（ショップ画面の表示更新用）</summary>
        public event System.Action OnProductsUpdated;
        /// <summary>購入が完了して効果を付けた（商品ID）</summary>
        public event System.Action<string> OnPurchaseGranted;
        /// <summary>購入が失敗・キャンセルされた（商品ID、理由）</summary>
        public event System.Action<string, string> OnPurchaseFailedEvent;
        /// <summary>支払いが後から行われる購入（コンビニ払いなど）を受け付けた（商品ID）。支払いが済むと OnPurchaseGranted が届く（次回起動時のこともある）</summary>
        public event System.Action<string> OnPurchaseDeferredEvent;

        /// <summary>
        /// 購入の「受け取り」（ConfirmPurchase）が Google に届くのを待っている間。この間に同じ商品をもう一度買うと、
        /// Google は「まだ持っている」と扱って断る（サーバーエラー）ため、ショップは押せないままにする。
        /// 届かない場合に備え、ConfirmTimeoutSeconds 秒たったら待つのをやめる
        /// </summary>
        public bool IsConfirming => awaitingConfirm > 0 && Time.realtimeSinceStartup - lastConfirmRequestAt < ConfirmTimeoutSeconds;
        public const float ConfirmTimeoutSeconds = 15f;
        /// <summary>受け取りが Google に届いた（商品ID）</summary>
        public event System.Action<string> OnPurchaseConfirmedEvent;
        private int awaitingConfirm;
        private float lastConfirmRequestAt;

        private StoreController store;
        private IapSettings settings;
        private bool connecting;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreate()
        {
            if (Instance != null) return;
            var go = new GameObject("IapManager");
            DontDestroyOnLoad(go);
            go.AddComponent<IapManager>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            settings = IapSettings.Load();
            Connect();
        }

        /// <summary>プレミアムパスを持っているか（保存データ。スタミナ無限のフラグと同じ）</summary>
        public static bool HasPremiumPass => StaminaManager.GetPersistentUnlimited();

        public async void Connect()
        {
            if (connecting || (IsReady && !failed)) return; // 買える商品が取れなかった時はつなぎ直す
            IsReady = false;
            connecting = true;
            failed = false;
            connectStartedAt = Time.realtimeSinceStartup;
            OnProductsUpdated?.Invoke();
            try
            {
                // つなぎ直し（ショップ画面を開いた時など）でイベントを二重に登録しないよう、登録は最初の1回だけ
                //（二重だと購入1回で2回付与されてしまう）
                if (store == null)
                {
                    store = UnityIAPServices.StoreController();
                    store.OnPurchasePending += OnPurchasePending;
                    store.OnPurchaseFailed += OnPurchaseFailed;
                    store.OnPurchaseDeferred += OnPurchaseDeferred;
                    // 受け取り（ConfirmPurchase）の完了の知らせ。登録しないと、前の購入の受け取りが終わらない扱いになり、次の購入が止まることがある
                    store.OnPurchaseConfirmed += OnPurchaseConfirmed;
                    store.OnProductsFetched += OnProductsFetched;
                    store.OnProductsFetchFailed += f => { Debug.LogWarning($"[IapManager] 商品の取得に失敗：{f.FailureReason}"); SetFailed(); };
                    store.OnPurchasesFetched += OnPurchasesFetched;
                    store.OnStoreDisconnected += d => { IsReady = false; Debug.LogWarning($"[IapManager] ストアから切断：{d.Message}"); SetFailed(); };
                }

                await store.Connect();
                var defs = new List<ProductDefinition> { new ProductDefinition(settings.premiumPassId, ProductType.NonConsumable) };
                foreach (var s in settings.stoneProducts)
                    if (s != null && !string.IsNullOrEmpty(s.productId)) defs.Add(new ProductDefinition(s.productId, ProductType.Consumable));
                store.FetchProducts(defs);
            }
            catch (System.Exception e)
            {
                SetFailed();
                Debug.LogWarning($"[IapManager] ストアへの接続に失敗：{e.Message}");
            }
            finally { connecting = false; }
        }

        private void SetFailed()
        {
            failed = true;
            OnProductsUpdated?.Invoke();
        }

        private void OnProductsFetched(List<Product> products)
        {
            IsReady = true;
            // 返事は来たが買える商品が1つも無い（Play Console に未登録・別パッケージのテスト版など）も失敗として伝える
            failed = !products.Any(p => p != null && p.availableToPurchase);
            if (failed) Debug.LogWarning("[IapManager] 買える商品がありません（Play Console の登録・パッケージ名を確認）");
            Debug.Log($"[IapManager] 商品を取得：{string.Join(", ", products.Select(p => $"{p.definition.id}={p.metadata.localizedPriceString}"))}");
            store.FetchPurchases(); // 購入履歴（プレミアムパスの復元・未処理の購入）を取り直す
            OnProductsUpdated?.Invoke();
        }

        private void OnPurchasesFetched(Orders orders)
        {
            bool premiumInHistory = false;
            foreach (var o in orders.ConfirmedOrders)
                foreach (var item in o.CartOrdered.Items())
                    if (item.Product != null && item.Product.definition.id == settings.premiumPassId)
                    {
                        premiumInHistory = true;
                        if (!HasPremiumPass)
                        {
                            StaminaManager.SetPersistentUnlimited(true);
                            Debug.Log("[IapManager] 購入履歴からプレミアムパスを復元しました");
                            OnPurchaseGranted?.Invoke(settings.premiumPassId);
                        }
                    }
            // 受け取り待ち（購入直後など）のプレミアムパスも「持っている」扱いにする（外さない）
            foreach (var o in orders.PendingOrders)
                foreach (var item in o.CartOrdered.Items())
                    if (item.Product != null && item.Product.definition.id == settings.premiumPassId) premiumInHistory = true;

#if !UNITY_EDITOR
            // 払い戻しなどで Google の購入履歴からプレミアムパスが消えていたら、端末の記録も外す（2026/10/25）。
            // ここは購入履歴を受け取れた時だけ呼ばれる（通信が無い時は OnPurchasesFetchFailed になり、何も変えない）。
            // ベータ用ビルドは常にスタミナ無制限（BuildFlavorConfig.IsBeta）なので対象外。
            // エディタの疑似ストアは購入を覚えていないため、エディタでは外さない（デバッグボタンで付けた状態が消えないように）
            if (!premiumInHistory && HasPremiumPass && !BuildFlavorConfig.IsBeta)
            {
                StaminaManager.SetPersistentUnlimited(false);
                Debug.Log("[IapManager] 購入履歴にプレミアムパスが無いため、プレミアムパスを外しました（払い戻しなど）");
            }
#endif
            OnProductsUpdated?.Invoke();
        }

        /// <summary>
        /// 商品の表示用の値段。実機は Google Play の値段（その人の Google アカウントの国の通貨。日本なら￥、アメリカなら$）。
        /// エディタの疑似ストアは「$0.01」の仮の値段しか返さないので、エディタとストアにつながらない時は IapSettings の予備の値段（言語別）を出す
        /// </summary>
        public string GetPriceText(string productId)
        {
#if !UNITY_EDITOR
            var p = store != null && IsReady ? store.GetProductById(productId) : null;
            if (p != null && p.availableToPurchase && !string.IsNullOrEmpty(p.metadata.localizedPriceString)) return p.metadata.localizedPriceString;
#endif
            return settings.GetFallbackPrice(productId);
        }

        /// <summary>今その商品を買えるか（つながっていない・持っている・上限を超える時は false）</summary>
        public bool CanBuy(string productId)
        {
            if (!IsReady || store == null) return false;
            var p = store.GetProductById(productId);
            if (p == null || !p.availableToPurchase) return false;
            if (productId == settings.premiumPassId) return !HasPremiumPass;
            var s = settings.FindStone(productId);
            return s != null && InfiniteStoneManager.GetPersistentCount() + s.amount <= InfiniteStoneManager.MaxCount;
        }

        public void Buy(string productId)
        {
            if (!CanBuy(productId)) { OnPurchaseFailedEvent?.Invoke(productId, "unavailable"); return; }
            Debug.Log($"[IapManager] 購入開始：{productId}");
            store.PurchaseProduct(productId);
        }


        /// <summary>「購入を復元」：Google Play から購入履歴を取り直す（プレミアムパスが戻る）</summary>
        public void Restore(System.Action<bool> done)
        {
            if (store == null) { done?.Invoke(false); return; }
            store.RestoreTransactions((ok, err) =>
            {
                Debug.Log($"[IapManager] 購入の復元：{(ok ? "成功" : "失敗 " + err)}");
                if (ok) store.FetchPurchases();
                done?.Invoke(ok);
            });
        }

        private void OnPurchasePending(PendingOrder order)
        {
            foreach (var item in order.CartOrdered.Items())
            {
                if (item.Product == null) continue;
                string id = item.Product.definition.id;
                if (id == settings.premiumPassId)
                {
                    StaminaManager.SetPersistentUnlimited(true);
                }
                else
                {
                    var s = settings.FindStone(id);
                    if (s == null) { Debug.LogWarning($"[IapManager] 不明な商品：{id}"); continue; }
                    InfiniteStoneManager.AddPersistent(s.amount);
                }
                Debug.Log($"[IapManager] 購入完了・付与：{id}");
                OnPurchaseGranted?.Invoke(id);
            }
            // 付与（保存）してから受け取りを伝える。届くまではショップを押せないようにする（IsConfirming）
            awaitingConfirm++;
            lastConfirmRequestAt = Time.realtimeSinceStartup;
            store.ConfirmPurchase(order);
            OnProductsUpdated?.Invoke();
        }

        private void OnPurchaseConfirmed(Order order)
        {
            string id = order?.CartOrdered.Items().FirstOrDefault()?.Product?.definition.id ?? "";
            awaitingConfirm = Mathf.Max(0, awaitingConfirm - 1);
            if (order is FailedOrder failed) Debug.LogWarning($"[IapManager] 受け取りに失敗：{id} {failed.FailureReason} {failed.Details}");
            else Debug.Log($"[IapManager] 受け取り完了：{id}");
            OnPurchaseConfirmedEvent?.Invoke(id);
        }

        // 後払い（コンビニ払いなど）：まだお金を受け取っていないので何も付けない。支払いが済むと OnPurchasePending で届く
        private void OnPurchaseDeferred(DeferredOrder order)
        {
            string id = order.CartOrdered.Items().FirstOrDefault()?.Product?.definition.id ?? "";
            Debug.Log($"[IapManager] 支払い待ち（後払い）：{id}");
            OnPurchaseDeferredEvent?.Invoke(id);
        }

        private void OnPurchaseFailed(FailedOrder order)
        {
            string id = order.CartOrdered.Items().FirstOrDefault()?.Product?.definition.id ?? "";
            Debug.Log($"[IapManager] 購入失敗：{id} {order.FailureReason} {order.Details}");
            OnPurchaseFailedEvent?.Invoke(id, order.FailureReason.ToString());
        }
    }
}
