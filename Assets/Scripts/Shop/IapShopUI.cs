using System.Collections;
using Game.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.Shop
{
    /// <summary>
    /// 課金ショップ画面（07_Shop）（2026/10/23）。プレミアムパス・虹の雫×1/×3/×10 の4枚のカードを並べ、押すと Google Play の購入画面を出す。
    /// 画面の部品（カード・説明枠・ボタン）はメニュー「Tools/ショップ/07_Shop シーンを作成（初回のみ）」で作り、ここで参照する。
    /// ・値段はストア（IapManager）から取る。取れない間は IapSettings の予備の値段を出し、カードは押せない
    /// ・プレミアムパスを持っていれば「購入済み」で押せない。虹の雫は買うと上限（99個）を超える時に押せない
    /// ・プレミアムパスの復元（入れ直し・機種変更）は起動時に IapManager が自動で行うため、「購入を復元」ボタンは置かない（Google Play では不要）
    /// </summary>
    public class IapShopUI : MonoBehaviour
    {
        [System.Serializable]
        public class ProductCard
        {
            [Tooltip("Google Play の商品ID（premium_pass / stone_1 / stone_3 / stone_10）")]
            public string productId;
            public Button button;
            public TextMeshProUGUI nameText;
            public TextMeshProUGUI priceText;
            [Tooltip("「購入済み」「所持上限」の時に出す印（無くてもよい）")]
            public GameObject purchasedMark;
            public TextMeshProUGUI purchasedText;
            [Tooltip("（使わない。以前はカード全体を半透明にしていたが、背景が透けるため中身だけ暗くする方式に変更）")]
            public CanvasGroup canvasGroup;
            [Tooltip("商品のアイコン。空なら子の「Icon」を使う")]
            public Image icon;

            // 暗くする前の色（Awakeで覚える）
            [System.NonSerialized] public Color iconColor, nameColor, priceColor;
            // パネルの枠（ホバーで点滅する画像）と、その本来の色
            [System.NonSerialized] public Image frame;
            [System.NonSerialized] public Color frameColor;
        }

        [Header("カード（左から：プレミアムパス・虹の雫×1・×3・×10）")]
        [SerializeField] private ProductCard[] cards = new ProductCard[0];

        [Header("文字")]
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI premiumDescText;
        [Tooltip("虹の雫3商品の共通の説明枠")]
        [SerializeField] private TextMeshProUGUI stoneDescText;
        [Tooltip("説明枠の高さ（2行分。英語は長いので折り返して2行にする。日本語は1行のまま、枠だけ英語と同じ高さにそろえる）。元の枠より低い値なら元の高さのまま")]
        [UnityEngine.Serialization.FormerlySerializedAs("englishDescBoxHeight")]
        [Min(0f)] [SerializeField] private float descBoxHeight = 150f;
        [Tooltip("「ストアに接続中…」「購入が完了しました」などを出す")]
        [SerializeField] private TextMeshProUGUI statusText;

        [Header("ボタン")]
        [SerializeField] private Button closeButton;

        [Header("購入演出（無ければ下の文字だけで知らせる）")]
        [SerializeField] private ShopPurchaseFX purchaseFx;

        [Header("押せない時（購入済み・所持上限・ストア未接続）")]
        [Tooltip("アイコン・商品名・値段に掛ける色（暗くする）。カードの背景の枠と「購入済み」の帯は暗くしない")]
        [SerializeField] private Color disabledContentColor = new Color(0.4f, 0.4f, 0.45f, 1f);

        [Header("状態メッセージ")]
        [Tooltip("購入完了などのメッセージを出しておく秒数（0＝消さない）")]
        [Min(0f)] [SerializeField] private float statusShowSeconds = 3f;

        [Header("購入の結果が届かない時の安全策（画面から出られなくなるのを防ぐ）")]
        [Tooltip("購入画面（Google Play）が閉じてアプリに戻ってから、この秒数たっても結果が届かなければ、パネルと EXIT をまた押せるようにする")]
        [Min(1f)] [SerializeField] private float busyTimeoutAfterReturn = 10f;
        [Tooltip("アプリに戻った知らせが無くても、パネルを押してからこの秒数たったら押せるようにする（エディタのテスト用ストアなど）")]
        [Min(1f)] [SerializeField] private float busyTimeoutMax = 120f;

        [Header("戻り先")]
        [SerializeField] private string returnSceneName = "03_AreaSelect";
        [SerializeField] private float closeFadeDuration = 0.4f;

        [Header("SE")]
        [SerializeField] private AudioClip selectSE;
        [SerializeField] private AudioClip purchasedSE;
        [SerializeField] private AudioClip failedSE;
        [SerializeField] private AudioClip closeSE;

        private AudioSource audioSource;
        private IapSettings settings;
        private bool busy;      // 購入・復元の処理中（全部のボタンを押せなくする）
        private bool closing;
        private Coroutine statusCoroutine;
        private bool showingConnecting;
        private IapManager subscribed;
        private bool lastConnectFailed;
        private float lastResultAt = -10f;  // 購入の結果（完了・失敗・後払い・時間切れ）が届いた時刻
        private const float ClickGuardAfterResult = 0.5f;
        private float busyStartedAt;        // パネルを押した時刻
        private float returnedAt = -1f;     // 購入画面から戻った時刻（-1＝まだ戻っていない）

        // 購入画面（Google Play）が前に出るとアプリはフォーカスを失い、閉じると戻る
        private void OnApplicationFocus(bool focus)
        {
            if (busy && focus) returnedAt = Time.realtimeSinceStartup;
        }

        // 結果が届かないまま時間がたったら、押せる状態に戻す
        private void CheckBusyTimeout()
        {
            if (!busy) return;
            float now = Time.realtimeSinceStartup;
            bool timedOut = (returnedAt >= 0f && now - returnedAt > busyTimeoutAfterReturn) || now - busyStartedAt > busyTimeoutMax;
            if (!timedOut) return;
            Debug.LogWarning("[IapShopUI] 購入の結果が届かないため、ボタンを押せる状態に戻しました");
            busy = false;
            ResetCardHover();
            SetStatus("", false);
            Refresh();
        }

        // 返事が来ないまま時間切れになった時は知らせが届かないので、ここで気付いて表示を切り替える
        private void Update()
        {
            var iap = IapManager.Instance;
            if (iap != null && !iap.IsReady && iap.ConnectFailed != lastConnectFailed) Refresh();
            CheckBusyTimeout();
        }

        private void Awake()
        {
            settings = IapSettings.Load();
            foreach (var c in cards)
            {
                if (c == null) continue;
                if (c.icon == null && c.button != null) c.icon = c.button.transform.Find("Icon")?.GetComponent<Image>();
                if (c.icon != null) c.iconColor = c.icon.color;
                if (c.nameText != null) c.nameColor = c.nameText.color;
                if (c.priceText != null) c.priceColor = c.priceText.color;
                if (c.button != null) c.frame = c.button.GetComponent<Image>();
                if (c.frame != null) c.frameColor = c.frame.color;
            }
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
            }

            foreach (var c in cards)
            {
                if (c == null || c.button == null) continue;
                var card = c;
                card.button.onClick.AddListener(() => OnClickCard(card));
            }
            if (closeButton != null) closeButton.onClick.AddListener(OnClickClose);
        }

        private void OnEnable()
        {
            Subscribe();
            ApplyTexts();
            Refresh();
        }

        // 07_Shop から直接Playした時は OnEnable の時点で IapManager がまだ無いため、Start でもう一度試す
        private void Start()
        {
            Subscribe();
            Refresh();
        }

        private void Subscribe()
        {
            var iap = IapManager.Instance;
            if (iap == null || subscribed == iap) return;
            iap.OnProductsUpdated += Refresh;
            iap.OnPurchaseGranted += OnGranted;
            iap.OnPurchaseFailedEvent += OnFailed;
            iap.OnPurchaseDeferredEvent += OnDeferred;
            subscribed = iap;
            if (!iap.IsReady || iap.ConnectFailed) iap.Connect(); // 起動時につながらなかった場合のつなぎ直し
        }

        private void OnDisable()
        {
            if (subscribed == null) return;
            subscribed.OnProductsUpdated -= Refresh;
            subscribed.OnPurchaseGranted -= OnGranted;
            subscribed.OnPurchaseFailedEvent -= OnFailed;
            subscribed.OnPurchaseDeferredEvent -= OnDeferred;
            subscribed = null;
        }

        // 説明枠の元の位置・高さ（日本語の時の状態。最初に1回だけ覚える）
        private Vector4? premiumDescBase, stoneDescBase;

        /// <summary>枠を2行分の高さにする（上端はそのままで下へ伸ばす。日本語・英語とも同じ高さ）。英語は折り返して2行、日本語は1行</summary>
        private void ApplyDescLayout(TextMeshProUGUI text, bool english, ref Vector4? baseRect)
        {
            if (text == null) return;
            var box = text.transform.parent as RectTransform;
            if (box == null) return;
            if (baseRect == null) baseRect = new Vector4(box.anchoredPosition.x, box.anchoredPosition.y, box.sizeDelta.x, box.sizeDelta.y);
            var b = baseRect.Value;
            float h = Mathf.Max(b.w, descBoxHeight);
            box.sizeDelta = new Vector2(b.z, h);
            box.anchoredPosition = new Vector2(b.x, b.y - (h - b.w) * box.pivot.y); // 上端の位置を変えない
            text.textWrappingMode = english ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
        }

        private static string L(string key, string ja) => LocalizationManager.GetStatic(key, ja);

        /// <summary>言語で変わる固定の文字（商品名・説明・ボタン）</summary>
        private void ApplyTexts()
        {
            bool en = LocalizationManager.CurrentLanguageStatic == GameLanguage.English;
            ApplyDescLayout(premiumDescText, en, ref premiumDescBase);
            ApplyDescLayout(stoneDescText, en, ref stoneDescBase);
            if (titleText != null) titleText.text = L("shop.iap.title", "プリズム・バザール");
            if (premiumDescText != null) premiumDescText.text = L("shop.iap.premium.desc", "広告なし＆スタミナ無限");
            if (stoneDescText != null) stoneDescText.text = L("shop.iap.stone.desc", "ジェム1つの使用回数を無限にする");

            foreach (var c in cards)
            {
                if (c == null || c.nameText == null) continue;
                if (c.productId == settings.premiumPassId) c.nameText.text = L("shop.iap.premium.name", "プレミアムパス");
                else
                {
                    var s = settings.FindStone(c.productId);
                    c.nameText.text = string.Format(L("shop.iap.stone.name", "虹の雫 ×{0}"), s != null ? s.amount : 0);
                }
            }
        }

        /// <summary>値段・押せるか・購入済みの表示を今の状態に合わせる</summary>
        private void Refresh()
        {
            if (closeButton != null) closeButton.interactable = !busy;
            var iap = IapManager.Instance;
            bool ready = iap != null && iap.IsReady;

            foreach (var c in cards)
            {
                if (c == null) continue;
                bool isPremium = c.productId == settings.premiumPassId;
                bool owned = isPremium && IapManager.HasPremiumPass;
                var stone = isPremium ? null : settings.FindStone(c.productId);
                bool maxed = stone != null && InfiniteStoneManager.GetPersistentCount() + stone.amount > InfiniteStoneManager.MaxCount;
                bool canBuy = !busy && ready && iap.CanBuy(c.productId);

                if (c.priceText != null)
                {
                    c.priceText.text = iap != null ? iap.GetPriceText(c.productId) : settings.GetFallbackPrice(c.productId);
                    c.priceText.gameObject.SetActive(!owned);
                }
                if (c.purchasedMark != null) c.purchasedMark.SetActive(owned || maxed);
                if (c.purchasedText != null)
                    c.purchasedText.text = owned ? L("shop.iap.purchased", "購入済み") : maxed ? L("shop.iap.maxed", "所持上限") : "";
                if (c.button != null) c.button.interactable = canBuy;
                bool dim = !(ready && !owned && !maxed);
                if (c.canvasGroup != null) c.canvasGroup.alpha = 1f; // 背景は透けさせない
                if (c.icon != null) c.icon.color = dim ? c.iconColor * disabledContentColor : c.iconColor;
                if (c.nameText != null) c.nameText.color = dim ? c.nameColor * disabledContentColor : c.nameColor;
                if (c.priceText != null) c.priceText.color = dim ? c.priceColor * disabledContentColor : c.priceColor;
            }

            bool failed = iap != null && iap.ConnectFailed;
            lastConnectFailed = failed;
            if ((!ready || failed) && !busy)
            {
                // つながらなかった時は「接続中」のまま待たせず、失敗したことを伝える（画面を開き直すとつなぎ直す）
                SetStatus(failed ? L("shop.iap.connectFailed", "ストアに接続できませんでした。通信環境を確認してください")
                                 : L("shop.iap.connecting", "ストアに接続中…"), false);
                showingConnecting = true;
            }
            else if (ready && !failed && showingConnecting) { SetStatus("", false); showingConnecting = false; }
        }

        private void OnClickCard(ProductCard card)
        {
            if (busy || closing || IapManager.Instance == null) return;
            if (purchaseFx != null && purchaseFx.IsPlaying) return; // 購入演出の間は押せない
            if (IapManager.Instance.IsConfirming) return; // 前の購入の受け取りが Google に届くまでは押せない（届く前に同じ商品を買うとサーバーエラーになる）
            // 購入画面の「Buy」を押したクリックが、その下のパネルにも届いて勝手に次の購入が始まることがある
            //（エディタのテスト用ストアで確認。結果が届いた直後に同じクリックで2回目の購入が始まり、止まっていた）。結果の直後は受け付けない
            if (Time.realtimeSinceStartup - lastResultAt < ClickGuardAfterResult) return;
            if (!IapManager.Instance.CanBuy(card.productId)) { Refresh(); return; }
            Play(selectSE);
            // 購入画面の間は、選んだパネルを拡大したままにする（マウスが外れても縮まない＝どれを選んだか分かる）。
            // ButtonHoverEffect の「押さえている」状態（スマホのタップと同じ）にし、結果が届いたら ResetCardHover で解除する。
            // 先に色を本来の色に戻してから（点滅途中の色を「元の色」として覚えないように）
            RestoreFrameColors();
            var selectedHover = card.button != null ? card.button.GetComponent<Game.UI.ButtonHoverEffect>() : null;
            if (selectedHover != null) selectedHover.SetHeld(true, playSE: false);
            busy = true;
            busyStartedAt = Time.realtimeSinceStartup;
            returnedAt = -1f;
            // Google Play の購入画面が開いている間は、パネルも EXIT も押せない（結果が届いたら戻す）。
            // まだ買っていない段階なので「処理中」などの文字は出さない
            SetStatus("", false);
            Refresh();
            RestoreFrameColors(); // 押せない間に点滅途中の色のまま止まらないよう、元の色にしておく
            IapManager.Instance.Buy(card.productId);
        }

        private void OnGranted(string productId)
        {
            busy = false;
            lastResultAt = Time.realtimeSinceStartup;
            ResetCardHover();
            Play(purchasedSE);
            if (purchaseFx != null)
            {
                // 演出で知らせる（虹の雫は1個ずつ所持数へ、プレミアムパスはスタミナが∞に）
                SetStatus("", false);
                // 買ったパネルのアイコンから、所持数（スタミナ）へ吸い込まれる
                Image from = null;
                foreach (var c in cards) if (c != null && c.productId == productId) { from = c.icon; break; }
                if (productId == settings.premiumPassId) purchaseFx.PlayPremium(from);
                else
                {
                    var stone = settings.FindStone(productId);
                    purchaseFx.PlayStone(stone != null ? stone.amount : 1, from, SingleStoneSprite());
                }
            }
            else
            {
                string msg = productId == settings.premiumPassId
                    ? L("shop.iap.thanks.premium", "プレミアムパスを購入しました")
                    : L("shop.iap.thanks", "購入が完了しました");
                SetStatus(msg, true);
            }
            Refresh();
        }

        // 飛ぶ雫の画像：×3・×10 を買っても、雫1個（×1 のパネル）の画像にする
        private Sprite SingleStoneSprite()
        {
            foreach (var c in cards)
            {
                if (c == null || c.icon == null) continue;
                var s = settings.FindStone(c.productId);
                if (s != null && s.amount == 1) return c.icon.sprite;
            }
            return null;
        }

        private void OnFailed(string productId, string reason)
        {
            busy = false;
            lastResultAt = Time.realtimeSinceStartup;
            ResetCardHover();
            // 自分でキャンセルした時は何も言わない
            if (reason == "UserCancelled") SetStatus("", false);
            else
            {
                Play(failedSE);
                SetStatus(L("shop.iap.failed", "購入できませんでした"), true);
            }
            Refresh();
        }

        // 後払い（コンビニ払いなど）：支払いが済むまで商品は届かないので、案内を出してボタンを押せる状態に戻す
        private void OnDeferred(string productId)
        {
            busy = false;
            lastResultAt = Time.realtimeSinceStartup;
            ResetCardHover();
            SetStatus(L("shop.iap.deferred", "支払いが完了すると受け取れます"), true);
            Refresh();
        }

        // スマホで触れて拡大したカードは指を離しても拡大のまま残るため、購入画面から戻ったら元の大きさへ戻す
        // ・スマホ：触れて拡大したパネルは「押さえている」状態（held）のまま残り、次に触れても拡大・点滅しなくなるので、それも解除する
        // ・PC：購入画面が閉じた時、マウスはパネルの上に乗ったまま（入り直していない）なので、そのままだと点滅が始まらない。
        //   次のフレーム（押せる状態に戻った後）に、マウスの下にあるパネルのホバーをやり直す
        private void ResetCardHover()
        {
            // 先に押せる状態へ戻す。押せないままだと ButtonHoverEffect が点滅途中の色を元に戻さず（requireInteractable）、
            // その色を「元の色」として覚えてしまい、パネルの色が点滅途中のまま固まる
            Refresh();
            foreach (var c in cards)
            {
                var hover = c != null && c.button != null ? c.button.GetComponent<Game.UI.ButtonHoverEffect>() : null;
                if (hover == null) continue;
                hover.SetHeld(false, playSE: false);
                hover.SmoothReset();
            }
            // ButtonHoverEffect は、押せない間にマウスが外れたパネルの色を戻さないまま「ホバー終了」扱いにするため、
            // 上のリセットでは戻らない色がある（その色を次のホバーで「元の色」として覚えてしまい、色が固まる）。最後に必ず本来の色へ戻す
            RestoreFrameColors();
            StartCoroutine(RehoverUnderMouseNextFrame());
        }

        /// <summary>全パネルの枠を本来の色に戻す（点滅途中の色で固まるのを防ぐ）</summary>
        private void RestoreFrameColors()
        {
            foreach (var c in cards)
                if (c != null && c.frame != null) c.frame.color = c.frameColor;
        }

        private IEnumerator RehoverUnderMouseNextFrame()
        {
            yield return null;
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse == null || busy) yield break;
            Vector2 pos = mouse.position.ReadValue();
            var canvas = GetComponentInParent<Canvas>();
            Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            foreach (var c in cards)
            {
                if (c == null || c.button == null || !c.button.interactable) continue;
                var rt = c.button.transform as RectTransform;
                if (rt == null || !RectTransformUtility.RectangleContainsScreenPoint(rt, pos, cam)) continue;
                var hover = c.button.GetComponent<Game.UI.ButtonHoverEffect>();
                if (hover != null && !hover.IsEnlarged)
                    hover.OnPointerEnter(new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current));
                break;
            }
        }

        private void SetStatus(string msg, bool autoHide)
        {
            if (statusText == null) return;
            if (statusCoroutine != null) { StopCoroutine(statusCoroutine); statusCoroutine = null; }
            statusText.text = msg;
            if (autoHide && statusShowSeconds > 0f && !string.IsNullOrEmpty(msg)) statusCoroutine = StartCoroutine(HideStatusLater());
        }

        private IEnumerator HideStatusLater()
        {
            yield return new WaitForSecondsRealtime(statusShowSeconds);
            statusCoroutine = null;
            if (statusText != null) statusText.text = "";
            Refresh();
        }

        private void Play(AudioClip clip)
        {
            if (clip == null || audioSource == null) return;
            float vol = SoundSettingsManager.Instance != null ? SoundSettingsManager.Instance.SEVolume : 1f;
            audioSource.PlayOneShot(clip, vol);
        }

        private void OnClickClose()
        {
            if (closing || busy) return;
            if (purchaseFx != null && purchaseFx.IsPlaying) return; // 購入演出の間は押せない
            if (IapManager.Instance != null && IapManager.Instance.IsConfirming) return; // 受け取りが Google に届くまでは出ない
            if (Time.realtimeSinceStartup - lastResultAt < ClickGuardAfterResult) return; // 購入画面のボタンのクリックが届いた分は無視
            closing = true;
            StartCoroutine(FadeOutAndReturn());
        }

        // AreaSelectMenu.FadeOutAndLoadScene と同じ：黒へフェードしてから戻る
        private IEnumerator FadeOutAndReturn()
        {
            Play(closeSE);
            var fadeObj = new GameObject("FadeOut");
            var fadeCanvas = fadeObj.AddComponent<Canvas>();
            fadeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            fadeCanvas.sortingOrder = 9999;
            var imageObj = new GameObject("FadeImage");
            imageObj.transform.SetParent(fadeObj.transform, false);
            var fadeImage = imageObj.AddComponent<Image>();
            fadeImage.color = new Color(0, 0, 0, 0);
            var rt = imageObj.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;

            float duration = Mathf.Max(closeFadeDuration, closeSE != null ? closeSE.length : 0f);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                fadeImage.color = new Color(0, 0, 0, Mathf.Clamp01(elapsed / Mathf.Max(duration, 0.01f)));
                yield return null;
            }
            SceneManager.LoadScene(returnSceneName);
        }
    }
}
