using System.Linq;
using Game.Localization;
using Game.Shop;
using Game.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 課金ショップ画面（07_Shop）の作成と、03_AreaSelect への入口ボタンの追加（2026/10/23）。
/// ・07_Shop：04_Drink をコピーして（カメラ・Canvas・黒帯・マネージャー類をそのまま使うため）、フード画面の部品を消し、
///   ショップの部品（背景・4枚のカード・説明枠・購入を復元・閉じる）を作る。既に 07_Shop があれば何もしない
/// ・入口ボタン：03_AreaSelect のフード画面ボタン（ShopButton）を複製して「IapShopButton」を作り、AreaSelectMenu に設定する。
///   既にあれば何もしない。シーンは保存しないので、確認してから自分で保存する
/// ・見た目の更新：作成済みの 07_Shop のカード・説明枠の大きさ・文字サイズ・アイコンを、このファイルの設定値に合わせ直す
///   （「購入を復元」ボタンの削除、スタミナ・ゴールド表示の追加もここで行う。2026/10/23）
/// ・どれも翻訳表にショップ・広告の文言を追加する（何度実行しても同じ結果）
/// </summary>
public static class IapShopSetupTool
{
    private const string SourceScenePath = "Assets/Scenes/04_Drink.unity";
    private const string ShopScenePath = "Assets/Scenes/07_Shop.unity";
    private const string AreaSelectScenePath = "Assets/Scenes/03_AreaSelect.unity";
    private const string LocPath = "Assets/Resources/GameData/LocalizationTable.asset";

    private const string FontPath = "Assets/Fonts/NotoSansJP-Regular SDF.asset";
    private const string BgPath = "Assets/Art/AreaSelect/Shop/ネオンの宝物庫.png";
    private const string CardBgPath = "Assets/Art/AreaSelect/Shop/DrinkMenu.png";
    private const string NeonFramePath = "Assets/Art/AreaSelect/Shop/新ネオン枠.png";
    private const string ExitIconPath = "Assets/Art/AreaSelect/Shop/EXITアイコン.png";
    private const string PremiumIconPath = "Assets/Art/AreaSelect/Shop/プレミアムパスのアイコン.png";
    private const string StoneIconPath = "Assets/Art/AreaSelect/無限化アイコン.png";
    // 虹の雫×3・×10の専用アイコン（無ければ×1と同じアイコンのまま）
    private const string Stone3IconPath = "Assets/Art/AreaSelect/虹の雫3個のアイコン.png";
    private const string Stone10IconPath = "Assets/Art/AreaSelect/虹の雫10個のアイコン.png";
    private const string EntryIconPath = "Assets/Art/AreaSelect/Shop/ショップのボタンのアイコン.png";
    private const string SelectSEPath = "Assets/Audio/Title/決定ボタン.mp3";
    private const string PurchasedSEPath = "Assets/Audio/Gold/お金を落とす2.mp3";
    private const string FailedSEPath = "Assets/Audio/GEM/ビープ音4.mp3";
    private const string HoverSEPath = "Assets/Audio/GEM/カーソル移動1.mp3";
    private const string TitleMaterialPath = "Assets/Art/AreaSelect/Shop/PrismBazaarTitle.mat";
    private const string GemScenePath = "Assets/Scenes/02_Gem.unity";
    private const float CardHoverScale = 1.06f; // カードのホバー拡大率（PCはマウスを乗せた時、スマホは触れた時）

    // 04_Drink から残すCanvas直下の部品（右上のスタミナ・ゴールド・虹の雫の所持数。それ以外のフード画面の部品は消す）
    private static readonly string[] HudNames = { "StaminaHUD", "GoldHUD", "InfiniteStoneHUD" };

    // ── 見た目の設定値（「07_Shop の見た目を更新」で反映）──
    private const float PremiumWidth = 460f;    // プレミアムパスのカードと説明枠の横幅（説明文が1行に収まる幅）
    private const float StoneWidth = 360f;      // 虹の雫のカードの横幅
    private const float CardHeight = 470f;
    private const float CardY = 40f;
    // パネルのアイコンの上下位置（パネルの中央から。元は 20＝商品名に近かった → 5 → まだ上寄りだったため -8 に下げた）
    private const float CardIconY = -8f;
    private const float PremiumGap = 90f;       // プレミアムパスと虹の雫の間
    private const float StoneGap = 30f;         // 虹の雫どうしの間
    private const float DescHeight = 100f;
    private const float DescGap = 20f;          // カードと説明枠の間
    private const float NameFontSize = 44f;
    private const float NameEnglishSizeScale = 0.7f; // 英語表示の時の商品名の倍率（いちばん長い「PRISM DROP ×10」が収まる大きさ）
    private const float PriceFontSize = 56f;
    private const float DescFontSize = 34f;
    private const float MarkFontSize = 44f;
    private const float TitleFontSize = 80f;
    private const float StatusFontSize = 40f;
    // 枠画像（DrinkMenu.png）の端を伸ばさない幅（9スライス）。横長の説明枠でも枠線の位置がカードと同じになる
    private const int FrameBorderPx = 60;
    private const float FramePixelsPerUnitMultiplier = 1.5f; // 60px → 40 の太さで表示

    private static readonly (string key, string ja, string en)[] LocEntries =
    {
        ("shop.iap.title", "プリズム・バザール", "PRISM BAZAAR"),
        ("shop.iap.premium.name", "プレミアムパス", "Premium Pass"),
        ("shop.iap.premium.desc", "広告なし＆スタミナ無限", "No Ads & Unlimited Stamina"),
        ("shop.iap.stone.name", "虹の雫 ×{0}", "Prism Drop ×{0}"),
        ("shop.iap.stone.desc", "ジェム1つの使用回数を無限にする", "Makes one gem's uses unlimited"),
        ("shop.iap.purchased", "購入済み", "Purchased"),
        ("shop.iap.maxed", "所持上限", "Max Owned"),
        ("shop.iap.connecting", "ストアに接続中…", "Connecting to store…"),
        ("shop.iap.connectFailed", "ストアに接続できませんでした。通信環境を確認してください", "Couldn't connect to the store. Please check your connection."),
        ("shop.iap.processing", "購入処理中…", "Processing…"),
        ("shop.iap.thanks.premium", "プレミアムパスを購入しました", "Premium Pass purchased!"),
        ("shop.iap.thanks", "購入が完了しました", "Purchase complete!"),
        ("shop.iap.failed", "購入できませんでした", "Purchase failed"),
        ("shop.iap.deferred", "支払いが完了すると受け取れます", "You'll receive it once payment is complete"),
        ("stamina.ad.loading", "広告を読み込み中…", "Loading ad…"),
        ("stamina.ad.failed",
            "広告を準備できませんでした。\n通信環境を確認して、もう一度お試しください。\nショップでプレミアムパスを見ますか？",
            "Couldn't load the ad.\nPlease check your connection and try again.\nOpen the shop to see the Premium Pass?"),
    };

    // ───────────────────────── 07_Shop ─────────────────────────

    [MenuItem("Tools/ショップ/07_Shop シーンを作成（初回のみ）")]
    private static void CreateShopScene()
    {
        if (System.IO.File.Exists(ShopScenePath))
        {
            EditorUtility.DisplayDialog("07_Shop 作成", $"{ShopScenePath} は既にあります。上書きしないため何もしません。", "OK");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (!AssetDatabase.CopyAsset(SourceScenePath, ShopScenePath))
        {
            EditorUtility.DisplayDialog("07_Shop 作成", "04_Drink のコピーに失敗しました。", "OK");
            return;
        }

        var scene = EditorSceneManager.OpenScene(ShopScenePath, OpenSceneMode.Single);
        var canvas = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Canvas");
        if (canvas == null)
        {
            EditorUtility.DisplayDialog("07_Shop 作成", "Canvas が見つかりません（07_Shop は作成済み・中身は 04_Drink のまま）。", "OK");
            return;
        }

        // フード画面の部品を消す
        var removed = new System.Collections.Generic.List<string>();
        for (int i = canvas.transform.childCount - 1; i >= 0; i--)
        {
            var child = canvas.transform.GetChild(i).gameObject;
            if (HudNames.Contains(child.name)) continue;
            removed.Add(child.name);
            Object.DestroyImmediate(child);
        }

        var root = BuildShopUI(canvas.transform);
        ApplyLayout((RectTransform)root.transform);
        BringHudsToFront(canvas.transform);

        AddToBuildSettings(ShopScenePath);
        AddLocEntries();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = root;
        Debug.Log($"[IapShopSetupTool] 07_Shop を作成しました。消したフード画面の部品：{string.Join(", ", removed)}");
        EditorUtility.DisplayDialog("07_Shop 作成", "07_Shop を作成して保存しました（Build Settings にも追加済み）。\n見た目は Canvas > IapShop の各部品で調整できます。", "OK");
    }

    private static GameObject BuildShopUI(Transform canvas)
    {
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        var bgSprite = LoadSprite(BgPath);
        var cardSprite = LoadSprite(CardBgPath);
        var frameSprite = LoadSprite(NeonFramePath);

        var root = NewUI("IapShop", canvas);
        Stretch(root);
        var ui = root.gameObject.AddComponent<IapShopUI>();
        root.gameObject.AddComponent<AudioSource>().playOnAwake = false;

        // 背景（画面を隙間なく覆う。はみ出した分は切れる）
        var bg = NewUI("Background", root);
        Stretch(bg);
        AddImage(bg, bgSprite, Color.white).raycastTarget = false;
        var fitter = bg.gameObject.AddComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        fitter.aspectRatio = bgSprite != null ? bgSprite.rect.width / bgSprite.rect.height : 1.5f;

        var dim = NewUI("Dim", root);
        Stretch(dim);
        AddImage(dim, null, new Color(0f, 0f, 0f, 0.35f)).raycastTarget = false;

        var title = NewText("TitleText", root, font, 80, "プリズム・バザール", LanguageFontSwitch.Kind.Heading, TextAlignmentOptions.Center);
        Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -90f), new Vector2(800f, 120f));

        // カード：プレミアムパスだけ少し離して左に、虹の雫3枚を並べる
        var settings = IapSettings.Load();
        const float cardW = 360f, cardH = 470f, cardY = 40f;
        float[] xs = { -615f, -165f, 225f, 615f };
        var ids = new System.Collections.Generic.List<string> { settings.premiumPassId };
        ids.AddRange(settings.stoneProducts.Where(s => s != null).Select(s => s.productId));

        var cardsParent = NewUI("Cards", root);
        Place(cardsParent, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1600f, 600f));

        var so = new SerializedObject(ui);
        var cardsProp = so.FindProperty("cards");
        cardsProp.arraySize = Mathf.Min(ids.Count, xs.Length);
        for (int i = 0; i < cardsProp.arraySize; i++)
        {
            bool premium = i == 0;
            var card = NewUI(premium ? "Card_PremiumPass" : $"Card_{ids[i]}", cardsParent);
            Place(card, new Vector2(0.5f, 0.5f), new Vector2(xs[i], cardY), new Vector2(cardW, cardH));
            var cardImg = AddImage(card, cardSprite, Color.white);
            var btn = card.gameObject.AddComponent<Button>();
            btn.targetGraphic = cardImg;
            var colors = btn.colors;
            colors.highlightedColor = new Color(0.85f, 1f, 1f, 1f);
            colors.pressedColor = new Color(0.6f, 0.75f, 1f, 1f);
            colors.disabledColor = Color.white; // 暗さは CanvasGroup で付ける
            btn.colors = colors;
            var group = card.gameObject.AddComponent<CanvasGroup>();

            var nameText = NewText("NameText", card, font, 44, premium ? "プレミアムパス" : "虹の雫", LanguageFontSwitch.Kind.Heading, TextAlignmentOptions.Center);
            Place(nameText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(cardW - 50f, 70f));

            var icon = NewUI("Icon", card);
            Place(icon, new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(220f, 220f));
            AddImage(icon, LoadSprite(premium ? PremiumIconPath : StoneIconPath), Color.white).raycastTarget = false;

            var price = NewText("PriceText", card, font, 56, "¥0", LanguageFontSwitch.Kind.Heading, TextAlignmentOptions.Center);
            price.color = new Color(1f, 0.92f, 0.45f, 1f);
            Place(price.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 75f), new Vector2(cardW - 50f, 80f));

            // 購入済み・所持上限の帯
            var mark = NewUI("PurchasedMark", card);
            Place(mark, new Vector2(0.5f, 0f), new Vector2(0f, 75f), new Vector2(cardW - 40f, 90f));
            // 帯の暗い板は付けない（値段の場所だけ暗く見えるため不要。文字だけ出す）
            var markText = NewText("Text", mark, font, 44, "購入済み", LanguageFontSwitch.Kind.Heading, TextAlignmentOptions.Center);
            Stretch(markText.rectTransform);
            markText.color = new Color(0.55f, 1f, 1f, 1f);
            mark.gameObject.SetActive(false);

            var el = cardsProp.GetArrayElementAtIndex(i);
            el.FindPropertyRelative("productId").stringValue = ids[i];
            el.FindPropertyRelative("button").objectReferenceValue = btn;
            el.FindPropertyRelative("nameText").objectReferenceValue = nameText;
            el.FindPropertyRelative("priceText").objectReferenceValue = price;
            el.FindPropertyRelative("purchasedMark").objectReferenceValue = mark.gameObject;
            el.FindPropertyRelative("purchasedText").objectReferenceValue = markText;
            el.FindPropertyRelative("canvasGroup").objectReferenceValue = group;
        }

        // 説明枠：プレミアムパスの下に1つ、虹の雫3枚の下に共通の1つ
        float descY = cardY - cardH * 0.5f - 75f;
        var premiumDesc = NewDescBox("PremiumDesc", cardsParent, font, cardSprite, new Vector2(xs[0], descY), new Vector2(cardW, 110f), "広告なし＆スタミナ無限");
        var stoneDesc = NewDescBox("StoneDesc", cardsParent, font, cardSprite, new Vector2((xs[1] + xs[3]) * 0.5f, descY), new Vector2(xs[3] - xs[1] + cardW, 110f), "ジェム1つの使用回数を無限にする");

        var status = NewText("StatusText", root, font, 40, "", LanguageFontSwitch.Kind.Body, TextAlignmentOptions.Center);
        Place(status.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 90f), new Vector2(1000f, 70f));
        status.color = new Color(0.55f, 1f, 1f, 1f);

        // 閉じる（右下。フード画面と同じネオン枠＋EXITアイコン）
        var close = NewUI("CloseButton", root);
        Place(close, new Vector2(1f, 0f), new Vector2(-150f, 150f), new Vector2(200f, 200f));
        var closeImg = AddImage(close, frameSprite, Color.white);
        var closeBtn = close.gameObject.AddComponent<Button>();
        closeBtn.targetGraphic = closeImg;
        var closeIcon = NewUI("Icon", close);
        Place(closeIcon, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(148f, 74f));
        AddImage(closeIcon, LoadSprite(ExitIconPath), Color.white).raycastTarget = false;

        so.FindProperty("titleText").objectReferenceValue = title;
        so.FindProperty("premiumDescText").objectReferenceValue = premiumDesc;
        so.FindProperty("stoneDescText").objectReferenceValue = stoneDesc;
        so.FindProperty("statusText").objectReferenceValue = status;
        so.FindProperty("closeButton").objectReferenceValue = closeBtn;
        so.FindProperty("selectSE").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(SelectSEPath);
        so.FindProperty("purchasedSE").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(PurchasedSEPath);
        so.FindProperty("failedSE").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(FailedSEPath);
        so.FindProperty("closeSE").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(SelectSEPath);
        so.ApplyModifiedPropertiesWithoutUndo();
        return root.gameObject;
    }

    private static TextMeshProUGUI NewDescBox(string name, Transform parent, TMP_FontAsset font, Sprite bg, Vector2 pos, Vector2 size, string text)
    {
        var box = NewUI(name, parent);
        Place(box, new Vector2(0.5f, 0.5f), pos, size);
        AddImage(box, bg, Color.white).raycastTarget = false;
        var t = NewText("Text", box, font, 36, text, LanguageFontSwitch.Kind.Body, TextAlignmentOptions.Center);
        Stretch(t.rectTransform);
        t.margin = new Vector4(24f, 8f, 24f, 8f);
        return t;
    }

    [MenuItem("Tools/ショップ/07_Shop の見た目を更新")]
    private static void UpdateShopScene()
    {
        if (!System.IO.File.Exists(ShopScenePath))
        {
            EditorUtility.DisplayDialog("07_Shop 更新", "07_Shop がありません。先に「07_Shop シーンを作成（初回のみ）」を実行してください。", "OK");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != ShopScenePath) scene = EditorSceneManager.OpenScene(ShopScenePath, OpenSceneMode.Single);

        var canvas = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Canvas");
        var root = canvas != null ? canvas.transform.Find("IapShop") as RectTransform : null;
        if (root == null) { EditorUtility.DisplayDialog("07_Shop 更新", "Canvas > IapShop が見つかりません。", "OK"); return; }

        var log = new System.Text.StringBuilder();

        // 「購入を復元」ボタンは不要（起動時に IapManager が自動で復元する）
        var restore = root.Find("RestoreButton");
        if (restore != null) { Object.DestroyImmediate(restore.gameObject); log.AppendLine("RestoreButton を削除"); }

        // 右上のスタミナ・ゴールド表示が無ければ 04_Drink から複製（位置・見た目もフード画面と同じ）
        var missing = HudNames.Where(h => canvas.transform.Find(h) == null).ToArray();
        if (missing.Length > 0)
        {
            var drink = EditorSceneManager.OpenScene(SourceScenePath, OpenSceneMode.Additive);
            var drinkCanvas = drink.GetRootGameObjects().FirstOrDefault(g => g.name == "Canvas");
            foreach (var h in missing)
            {
                var src = drinkCanvas != null ? drinkCanvas.transform.Find(h) : null;
                if (src == null) { log.AppendLine($"{h}：04_Drink に無いため追加できません"); continue; }
                var clone = Object.Instantiate(src.gameObject);
                clone.name = src.name;
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(clone, scene);
                clone.transform.SetParent(canvas.transform, false);
                log.AppendLine($"{h} を 04_Drink から複製");
            }
            EditorSceneManager.CloseScene(drink, true); // 04_Drink は保存しない（変更なし）
        }
        BringHudsToFront(canvas.transform);
        if (UseInputSystemModule(scene)) log.AppendLine("EventSystem の入力を Input System 用に変更（スマホで触れた時の拡大に必要）");

        ApplyLayout(root);
        AddLocEntries();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        log.AppendLine("カード・説明枠の大きさ、文字サイズ、アイコン、タイトルを更新");
        Debug.Log("[IapShopSetupTool] 07_Shop を更新しました\n" + log);
        EditorUtility.DisplayDialog("07_Shop 更新", "07_Shop を更新して保存しました。内容は Console に出力しています。", "OK");
    }

    /// <summary>カード・説明枠の位置と大きさ、文字サイズ、アイコンを設定値に合わせる</summary>
    private static void ApplyLayout(RectTransform root)
    {
        EnsureFrameBorder(CardBgPath);
        var cardSprite = LoadSprite(CardBgPath);

        var cardsParent = root.Find("Cards") as RectTransform;
        if (cardsParent == null) { Debug.LogWarning("[IapShopSetupTool] IapShop > Cards が見つかりません。"); return; }

        // 左端から：プレミアムパス｜間｜虹の雫×3枚
        float total = PremiumWidth + PremiumGap + StoneWidth * 3f + StoneGap * 2f;
        float left = -total * 0.5f;
        float premiumX = left + PremiumWidth * 0.5f;
        float stoneLeft = left + PremiumWidth + PremiumGap;
        float stoneRight = stoneLeft + StoneWidth * 3f + StoneGap * 2f;
        cardsParent.sizeDelta = new Vector2(total, CardHeight + DescGap + DescHeight + 40f);

        var premium = cardsParent.Find("Card_PremiumPass") as RectTransform;
        if (premium != null) LayoutCard(premium, premiumX, PremiumWidth, cardSprite, null);

        var settings = IapSettings.Load();
        int idx = 0;
        foreach (var s in settings.stoneProducts.Where(p => p != null))
        {
            float x = stoneLeft + StoneWidth * 0.5f + idx * (StoneWidth + StoneGap);
            idx++;
            var card = cardsParent.Find($"Card_{s.productId}") as RectTransform;
            if (card == null) { Debug.LogWarning($"[IapShopSetupTool] Card_{s.productId} が見つかりません。"); continue; }
            string iconPath = s.amount >= 10 ? Stone10IconPath : s.amount >= 3 ? Stone3IconPath : null;
            LayoutCard(card, x, StoneWidth, cardSprite, iconPath);
        }

        float descY = CardY - CardHeight * 0.5f - DescGap - DescHeight * 0.5f;
        LayoutDesc(cardsParent.Find("PremiumDesc") as RectTransform, premiumX, PremiumWidth, descY, cardSprite);
        LayoutDesc(cardsParent.Find("StoneDesc") as RectTransform, (stoneLeft + stoneRight) * 0.5f, stoneRight - stoneLeft, descY, cardSprite);

        SetFont(root.Find("TitleText"), TitleFontSize);
        SetFont(root.Find("StatusText"), StatusFontSize);
        var title = root.Find("TitleText")?.GetComponent<TextMeshProUGUI>();
        if (title != null)
        {
            title.text = "プリズム・バザール";
            // 派手なタイトル：光る縁取り（素材）＋虹色に流れる文字と光の筋（IapShopTitleFX）
            var mat = EnsureTitleMaterial();
            if (mat != null) title.fontSharedMaterial = mat;
            var fx = title.GetComponent<IapShopTitleFX>();
            if (fx == null) fx = title.gameObject.AddComponent<IapShopTitleFX>();
            ApplyTitleColorsB(fx);
            RemoveTitleOutline(mat);
        }
        // EXIT ボタンにも背景の板を付ける
        AddFrameFills(root.gameObject);
        EnsureExitHover(root.Find("CloseButton"));
    }

    private static void LayoutCard(RectTransform card, float x, float width, Sprite frame, string iconPath)
    {
        card.anchoredPosition = new Vector2(x, CardY);
        card.sizeDelta = new Vector2(width, CardHeight);
        SetFrame(card.GetComponent<Image>(), frame);
        Resize(card.Find("NameText"), width - 50f);
        Resize(card.Find("PriceText"), width - 50f);
        Resize(card.Find("PurchasedMark"), width - 40f);
        if (card.Find("Icon") is RectTransform iconRt) iconRt.anchoredPosition = new Vector2(iconRt.anchoredPosition.x, CardIconY);
        // 「購入済み」「所持上限」の暗い板は不要（値段の場所だけ暗く見える）→ 板を外して文字だけにする
        var markImg = card.Find("PurchasedMark")?.GetComponent<Image>();
        if (markImg != null) Object.DestroyImmediate(markImg);
        SetFont(card.Find("NameText"), NameFontSize);
        // 英語は見出し用フォント（Bungee）が横に広く、商品名がはみ出すので英語の時だけ小さくする（4枚とも同じ倍率）
        var nameSwitch = card.Find("NameText")?.GetComponent<LanguageFontSwitch>();
        if (nameSwitch != null) nameSwitch.englishSizeScale = NameEnglishSizeScale;
        SetFont(card.Find("PriceText"), PriceFontSize);
        SetFont(card.Find("PurchasedMark/Text"), MarkFontSize);
        EnsureHover(card.gameObject);
        if (iconPath == null) return;
        var sprite = System.IO.File.Exists(iconPath) ? AssetDatabase.LoadAssetAtPath<Sprite>(iconPath) : null;
        var icon = card.Find("Icon")?.GetComponent<Image>();
        if (sprite == null) Debug.Log($"[IapShopSetupTool] 専用アイコンがまだ無いため×1と同じアイコンのままです：{iconPath}");
        else if (icon != null) icon.sprite = sprite;
    }

    // ホバー拡大：PC＝マウスを乗せると拡大（ButtonHoverEffect）、スマホ＝触れた瞬間に拡大（TouchTapEnlarge）。AreaSelect のボタンと同じ部品
    private static void EnsureHover(GameObject card)
    {
        var hover = card.GetComponent<ButtonHoverEffect>();
        if (hover == null) hover = card.AddComponent<ButtonHoverEffect>();
        if (card.GetComponent<TouchTapEnlarge>() == null) card.AddComponent<TouchTapEnlarge>();
        var so = new SerializedObject(hover);
        so.FindProperty("hoverScale").floatValue = CardHoverScale;
        so.FindProperty("requireInteractable").boolValue = true; // 購入済み・所持上限・接続前のカードは拡大しない
        var se = so.FindProperty("hoverSE");
        if (se.objectReferenceValue == null) se.objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(HoverSEPath);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // EXIT ボタンのホバー拡大：フード画面（04_Drink）の EXIT と同じ設定（1.1倍・枠の点滅・カーソル移動2の音・押したら拡大のまま）
    private const string ExitHoverSEPath = "Assets/Audio/GEM/カーソル移動2.mp3";
    private static void EnsureExitHover(Transform close)
    {
        if (close == null) return;
        var hover = close.GetComponent<ButtonHoverEffect>();
        if (hover == null) hover = close.gameObject.AddComponent<ButtonHoverEffect>();
        if (close.GetComponent<TouchTapEnlarge>() == null) close.gameObject.AddComponent<TouchTapEnlarge>();
        var so = new SerializedObject(hover);
        so.FindProperty("hoverScale").floatValue = 1.1f;
        so.FindProperty("hoverSE").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(ExitHoverSEPath);
        so.FindProperty("blinkTarget").objectReferenceValue = close.Find("FrameBg")?.GetComponent<Image>();
        so.FindProperty("blinkSpeed").floatValue = 1f;
        so.FindProperty("blinkColor").colorValue = new Color(0.39215687f, 0.39215687f, 0.39215687f, 1f);
        so.FindProperty("blinkIntensity").floatValue = 0.8f;
        so.FindProperty("requireInteractable").boolValue = true; // 購入画面を開いている間（押せない間）は拡大しない
        so.FindProperty("lockAfterClick").boolValue = true;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // 04_Drink 由来の EventSystem は旧入力（StandaloneInputModule）のため、スマホのタッチ判定（TouchTapEnlarge）が効かない。
    // 03_AreaSelect と同じ InputSystemUIInputModule に替える
    private static bool UseInputSystemModule(UnityEngine.SceneManagement.Scene scene)
    {
        var es = scene.GetRootGameObjects().Select(g => g.GetComponentInChildren<UnityEngine.EventSystems.EventSystem>(true)).FirstOrDefault(e => e != null);
        if (es == null || es.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>() != null) return false;
        var old = es.GetComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        if (old != null) Object.DestroyImmediate(old);
        var module = es.gameObject.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        module.AssignDefaultActions();
        return true;
    }

    private static void LayoutDesc(RectTransform box, float x, float width, float y, Sprite frame)
    {
        if (box == null) return;
        box.anchoredPosition = new Vector2(x, y);
        box.sizeDelta = new Vector2(width, DescHeight);
        SetFrame(box.GetComponent<Image>(), frame);
        SetFont(box.Find("Text"), DescFontSize);
    }

    private static void Resize(Transform t, float width)
    {
        if (t is RectTransform rt) rt.sizeDelta = new Vector2(width, rt.sizeDelta.y);
    }

    // 文字サイズを固定する（枠に合わせて自動で縮むと、商品ごとに大きさがばらつくため）
    private static void SetFont(Transform t, float size)
    {
        var tmp = t != null ? t.GetComponent<TextMeshProUGUI>() : null;
        if (tmp == null) return;
        tmp.enableAutoSizing = false;
        tmp.fontSize = size;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
    }

    private static void SetFrame(Image img, Sprite frame)
    {
        if (img == null) return;
        if (frame != null) img.sprite = frame;
        img.type = Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = FramePixelsPerUnitMultiplier;
        img.fillCenter = true;
    }

    // DrinkMenu.png に端の幅（9スライス）を設定する。フード画面のカードは「Simple」で表示しているので見た目は変わらない
    private static void EnsureFrameBorder(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return;
        var want = new Vector4(FrameBorderPx, FrameBorderPx, FrameBorderPx, FrameBorderPx);
        if (importer.spriteBorder == want) return;
        importer.spriteBorder = want;
        importer.SaveAndReimport();
        Debug.Log($"[IapShopSetupTool] {path} に端の幅 {FrameBorderPx}px を設定しました");
    }

    private static void BringHudsToFront(Transform canvas)
    {
        foreach (var h in HudNames)
        {
            var t = canvas.Find(h);
            if (t != null) t.SetAsLastSibling();
        }
    }

    // タイトルの色を案B（ビビッドな虹色）にする（2026/10/24）。最初の版の色（Area の9色）のままの時だけ置き換え、Inspector で変えた色は残す
    private static void ApplyTitleColorsB(IapShopTitleFX fx)
    {
        var so = new SerializedObject(fx);
        var colors = so.FindProperty("colors");
        bool isOldDefault = colors.arraySize == 9 && colors.GetArrayElementAtIndex(0).colorValue == (Color)new Color32(0x9B, 0x8F, 0xC7, 0xFF);
        if (!isOldDefault) return;
        var b = IapShopTitleFX.DefaultColors();
        colors.arraySize = b.Length;
        for (int i = 0; i < b.Length; i++) colors.GetArrayElementAtIndex(i).colorValue = b[i];
        so.FindProperty("brighten").floatValue = IapShopTitleFX.DefaultBrighten;
        so.ApplyModifiedPropertiesWithoutUndo();
        Debug.Log("[IapShopSetupTool] タイトルの色をビビッドな虹色（案B）にしました");
    }

    // タイトルの暗い縁取りを消す（2026/10/24。虹色がくっきり見えるように。周りの光＝Glowは残す）。最初に作った太さ(0.15)のままの時だけ
    private static void RemoveTitleOutline(Material mat)
    {
        if (mat == null) return;
        ShaderUtilities.GetShaderPropertyIDs();
        if (!mat.HasProperty(ShaderUtilities.ID_OutlineWidth) || !Mathf.Approximately(mat.GetFloat(ShaderUtilities.ID_OutlineWidth), 0.15f)) return;
        mat.SetFloat(ShaderUtilities.ID_OutlineWidth, 0f);
        ShaderUtilities.UpdateShaderRatios(mat);
        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();
        Debug.Log("[IapShopSetupTool] タイトルの縁取りを消しました（" + TitleMaterialPath + " の Outline > Thickness）");
    }

    // タイトル用の素材（光る縁取り）。無ければ文字の素材から作る。あれば変えない（Inspector で調整した値を残す）
    private static Material EnsureTitleMaterial()
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(TitleMaterialPath);
        if (mat != null) return mat;
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (font == null || font.material == null) return null;
        ShaderUtilities.GetShaderPropertyIDs(); // ID_xxx は初期化しないと 0 のまま
        mat = new Material(font.material) { name = "PrismBazaarTitle" };
        mat.SetFloat(ShaderUtilities.ID_FaceDilate, 0.1f);
        mat.SetColor(ShaderUtilities.ID_OutlineColor, new Color(0.18f, 0.02f, 0.30f, 1f));
        mat.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.15f);
        mat.EnableKeyword(ShaderUtilities.Keyword_Glow);
        mat.SetColor(ShaderUtilities.ID_GlowColor, new Color(1f, 0.35f, 0.95f, 0.75f));
        mat.SetFloat(ShaderUtilities.ID_GlowOffset, 0.1f);
        mat.SetFloat(ShaderUtilities.ID_GlowInner, 0.05f);
        mat.SetFloat(ShaderUtilities.ID_GlowOuter, 0.45f);
        mat.SetFloat(ShaderUtilities.ID_GlowPower, 0.8f);
        ShaderUtilities.UpdateShaderRatios(mat);
        AssetDatabase.CreateAsset(mat, TitleMaterialPath);
        Debug.Log($"[IapShopSetupTool] タイトル用の素材を作りました：{TitleMaterialPath}（光る縁取りの色・太さはこの素材の Inspector で調整）");
        return mat;
    }

    // ───────────────────────── ネオン枠ボタンの背景の板 ─────────────────────────
    // ネオン枠（新ネオン枠.png）は中が透明で、背景が明るい・ごちゃごちゃした画面だとボタンが見づらい。
    // 枠の内側にカードと同じ紺色の板（Fill）を1枚足す。枠より奥に置くので枠の見た目・点滅はそのまま。
    // 板の色・内側への寄せ幅は各ボタンの子「Fill」の Image / Rect Transform で調整できる
    private static readonly Color FillColor = new Color(0.04f, 0.07f, 0.22f, 0.85f);
    private const float FillInset = 0.17f; // 枠の外周から内側へ（ボタンの大きさに対する割合）。枠の丸い角からはみ出さない値

    [MenuItem("Tools/ショップ/ネオン枠のボタンに背景の板を付ける（02_Gem・04_Drink・07_Shop）")]
    private static void AddFillsToAllScenes()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var log = new System.Text.StringBuilder();
        foreach (var path in new[] { GemScenePath, SourceScenePath, ShopScenePath })
        {
            if (!System.IO.File.Exists(path)) { log.AppendLine($"{path}：ありません"); continue; }
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            int count = 0;
            foreach (var go in scene.GetRootGameObjects()) count += AddFrameFills(go);
            if (count > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            log.AppendLine($"{System.IO.Path.GetFileNameWithoutExtension(path)}：{count}個のボタンに付けました");
        }
        Debug.Log("[IapShopSetupTool] ネオン枠ボタンの背景の板\n" + log);
        EditorUtility.DisplayDialog("背景の板", log.ToString(), "OK");
    }

    /// <summary>配下のネオン枠ボタンに背景の板を付ける（既に付いていれば何もしない）。付けた数を返す</summary>
    private static int AddFrameFills(GameObject root)
    {
        var frame = AssetDatabase.LoadAssetAtPath<Sprite>(NeonFramePath);
        if (frame == null) return 0;
        int count = 0;
        foreach (var button in root.GetComponentsInChildren<Button>(true))
        {
            var bt = button.transform;
            if (bt.Find("Fill") != null) continue;
            var rootImg = button.GetComponent<Image>();
            Image frameImg = null;
            if (rootImg != null && rootImg.sprite == frame)
            {
                // 枠がボタン自身にある（07_Shop の CloseButton）→ 枠を子「FrameBg」へ移し、板をその奥に置く
                var bg = NewUI("FrameBg", bt);
                Stretch(bg);
                frameImg = AddImage(bg, frame, rootImg.color);
                frameImg.raycastTarget = false;
                rootImg.sprite = null;
                rootImg.color = new Color(1f, 1f, 1f, 0f); // 押せる範囲としてだけ残す
                bg.SetAsFirstSibling();
            }
            else
            {
                for (int i = 0; i < bt.childCount; i++)
                {
                    var img = bt.GetChild(i).GetComponent<Image>();
                    if (img != null && img.sprite == frame) { frameImg = img; break; }
                }
            }
            if (frameImg == null) continue;

            var fill = NewUI("Fill", bt);
            fill.anchorMin = new Vector2(FillInset, FillInset);
            fill.anchorMax = new Vector2(1f - FillInset, 1f - FillInset);
            fill.offsetMin = Vector2.zero;
            fill.offsetMax = Vector2.zero;
            // 枠の画像と同じ位置・大きさの範囲に合わせる（枠の子が親より小さい場合もあるため）
            var frt = frameImg.rectTransform;
            if (frt.parent == bt)
            {
                fill.anchorMin = Vector2.Lerp(frt.anchorMin, frt.anchorMax, FillInset);
                fill.anchorMax = Vector2.Lerp(frt.anchorMin, frt.anchorMax, 1f - FillInset);
                fill.offsetMin = frt.offsetMin;
                fill.offsetMax = frt.offsetMax;
                if (frt.anchorMin == frt.anchorMax)
                {
                    // 枠が固定サイズ（アンカーが1点）の時：中心と大きさから内側の範囲を出す
                    fill.anchorMin = fill.anchorMax = frt.anchorMin;
                    fill.pivot = frt.pivot;
                    fill.anchoredPosition = frt.anchoredPosition;
                    fill.sizeDelta = frt.sizeDelta * (1f - FillInset * 2f);
                }
            }
            var fillImg = AddImage(fill, null, FillColor);
            fillImg.raycastTarget = false;
            fill.SetSiblingIndex(frameImg.transform.GetSiblingIndex()); // 枠のすぐ奥
            Undo.RegisterCreatedObjectUndo(fill.gameObject, "Add Button Fill");
            count++;
        }
        return count;
    }

    // ───────────────────────── 購入演出（2026/10/24） ─────────────────────────
    // 07_Shop に、虹の雫・プレミアムパスの購入演出（ShopPurchaseFX）の部品を作る。
    // 「FX_PurchaseReward」（画面全体の入れ物）の下に、飛ぶ雫・チケット、スタミナで弾ける輪と光の粒、光の筋を作る。
    // 確認のポップアップは出さない（AreaSelect の初回報酬だけの演出）。以前の版で作った通知パネル・パネルの上に出る石などが残っていれば消す。
    // 何度実行しても安全（既にある部品は作り直さず、画像と参照だけ合わせる）。動きの数値は FX_PurchaseReward の ShopPurchaseFX で調整する
    private const int FxSparkCount = 12;
    private const int FxFlyerCount = 5;
    private const int FxBurstRingCount = 2;
    private const string GlowSpritePath = "Assets/Art/UI/GemFX/Gem_Glow.png";
    private const string RingSpritePath = "Assets/Art/UI/GemFX/Gem_Ring.png";
    private const string StarSpritePath = "Assets/Art/UI/GemFX/Gem_Star.png";
    private static readonly string[] OldFxParts = { "Glow", "Ring", "Icon" };

    [MenuItem("Tools/ショップ/07_Shop に購入演出を組み込む")]
    private static void SetupPurchaseFX()
    {
        var glowSp = AssetDatabase.LoadAssetAtPath<Sprite>(GlowSpritePath);
        var ringSp = AssetDatabase.LoadAssetAtPath<Sprite>(RingSpritePath);
        var starSp = AssetDatabase.LoadAssetAtPath<Sprite>(StarSpritePath);
        if (glowSp == null || ringSp == null || starSp == null) { Debug.LogError("[IapShopSetupTool] Assets/Art/UI/GemFX の Gem_Glow / Gem_Ring / Gem_Star のどれかが見つかりません"); return; }
        if (!System.IO.File.Exists(ShopScenePath)) { EditorUtility.DisplayDialog("購入演出", "07_Shop がありません。", "OK"); return; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != ShopScenePath) scene = EditorSceneManager.OpenScene(ShopScenePath, OpenSceneMode.Single);
        var canvas = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Canvas");
        var shopUI = canvas != null ? canvas.GetComponentInChildren<IapShopUI>(true) : null;
        if (canvas == null || shopUI == null) { EditorUtility.DisplayDialog("購入演出", "07_Shop に Canvas / IapShopUI が見つかりません。", "OK"); return; }
        var log = new System.Text.StringBuilder();

        // 以前の版の通知パネルは不要
        var oldPanel = canvas.transform.Find("PurchaseNoticePanel");
        if (oldPanel != null) { Object.DestroyImmediate(oldPanel.gameObject); log.AppendLine("以前の版の通知パネル（PurchaseNoticePanel）を削除"); }

        var fxT = canvas.transform.Find("FX_PurchaseReward") as RectTransform;
        if (fxT == null) { fxT = NewUI("FX_PurchaseReward", canvas.transform); log.AppendLine("FX_PurchaseReward を作成"); }
        Stretch(fxT);
        fxT.SetAsLastSibling();
        // 以前の版のパネルの上に出る石・光・輪・光の粒は不要
        foreach (var name in OldFxParts.Concat(Enumerable.Range(0, FxSparkCount).Select(i => "Spark" + i)))
        {
            var old = fxT.Find(name);
            if (old != null) Object.DestroyImmediate(old.gameObject);
        }
        var fx = fxT.GetComponent<ShopPurchaseFX>();
        if (fx == null) fx = fxT.gameObject.AddComponent<ShopPurchaseFX>();
        if (fxT.GetComponent<AudioSource>() == null) fxT.gameObject.AddComponent<AudioSource>().playOnAwake = false;

        // 並び順：飛ぶ雫 → スタミナの輪・光の粒 → 光の筋（後ろほど手前）
        var flyers = new Image[FxFlyerCount];
        for (int i = 0; i < FxFlyerCount; i++) { FxPart(fxT, "Flyer" + i, null, out flyers[i]); flyers[i].preserveAspect = true; }
        var burstRings = new Image[FxBurstRingCount];
        for (int i = 0; i < FxBurstRingCount; i++) FxPart(fxT, "BurstRing" + i, ringSp, out burstRings[i]);
        var burstSparks = new Image[FxSparkCount];
        for (int i = 0; i < FxSparkCount; i++) FxPart(fxT, "BurstSpark" + i, starSp, out burstSparks[i]);
        var shineT = FxPart(fxT, "Shine", glowSp, out Image shineImg);
        shineT.sizeDelta = new Vector2(46f, 150f);
        shineT.localRotation = Quaternion.Euler(0f, 0f, -20f);
        foreach (Transform c in flyers.Select(f => f.transform).Concat(burstRings.Select(b => b.transform))
                     .Concat(burstSparks.Select(b => b.transform)).Concat(new Transform[] { shineT }))
            c.SetAsLastSibling();

        var stoneHud = canvas.GetComponentInChildren<InfiniteStoneHUD>(true);
        var staminaHud = canvas.GetComponentInChildren<StaminaHUD>(true);
        var so = new SerializedObject(fx);
        so.FindProperty("stoneHud").objectReferenceValue = stoneHud;
        so.FindProperty("staminaHud").objectReferenceValue = staminaHud;
        SetArray(so.FindProperty("flyers"), flyers);
        SetArray(so.FindProperty("burstRings"), burstRings);
        SetArray(so.FindProperty("burstSparks"), burstSparks);
        so.FindProperty("shine").objectReferenceValue = shineImg;
        so.ApplyModifiedPropertiesWithoutUndo();

        var uso = new SerializedObject(shopUI);
        uso.FindProperty("purchaseFx").objectReferenceValue = fx;
        uso.ApplyModifiedPropertiesWithoutUndo();

        if (stoneHud == null) log.AppendLine("⚠ InfiniteStoneHUD が無いため、雫は所持数へ飛ばず上へ消えます");
        if (staminaHud == null) log.AppendLine("⚠ StaminaHUD が無いため、プレミアムパスの∞の演出は出ません");
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        log.AppendLine("購入演出の部品と参照を設定して保存しました");
        Debug.Log("[IapShopSetupTool] 07_Shop の購入演出\n" + log);
        EditorUtility.DisplayDialog("購入演出", log.ToString(), "OK");
    }

    // 演出の部品の画像（クリックは通さない）。既にあれば使い回す。最初は非表示（演出の時だけ表示する）
    private static RectTransform FxPart(RectTransform parent, string name, Sprite sprite, out Image img)
    {
        var t = parent.Find(name) as RectTransform;
        if (t == null) t = NewUI(name, parent);
        t.anchorMin = t.anchorMax = t.pivot = new Vector2(0.5f, 0.5f);
        img = t.GetComponent<Image>();
        if (img == null) img = t.gameObject.AddComponent<Image>();
        if (sprite != null) img.sprite = sprite;
        img.raycastTarget = false;
        t.gameObject.SetActive(false);
        return t;
    }

    private static void SetArray<T>(SerializedProperty prop, T[] items) where T : Object
    {
        prop.arraySize = items.Length;
        for (int i = 0; i < items.Length; i++) prop.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
    }

    // ───────────────────────── 03_AreaSelect の入口ボタン ─────────────────────────

    [MenuItem("Tools/ショップ/03_AreaSelect に課金ショップの入口ボタンを追加")]
    private static void AddEntryButton()
    {
        var active = EditorSceneManager.GetActiveScene();
        if (active.path != AreaSelectScenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            active = EditorSceneManager.OpenScene(AreaSelectScenePath, OpenSceneMode.Single);
        }

        var menu = Object.FindFirstObjectByType<AreaSelectMenu>(FindObjectsInactive.Include);
        if (menu == null) { EditorUtility.DisplayDialog("入口ボタン追加", "AreaSelectMenu が見つかりません。", "OK"); return; }
        if (menu.iapShopButton != null)
        {
            EditorUtility.DisplayDialog("入口ボタン追加", $"既に設定済みです（{menu.iapShopButton.name}）。何もしません。", "OK");
            return;
        }
        if (menu.shopButton == null) { EditorUtility.DisplayDialog("入口ボタン追加", "AreaSelectMenu の Shop Button（フード画面）が未設定のため、複製元がありません。", "OK"); return; }

        var src = menu.shopButton.gameObject;
        var clone = (GameObject)Object.Instantiate(src, src.transform.parent);
        clone.name = "IapShopButton";
        Undo.RegisterCreatedObjectUndo(clone, "Add IapShopButton");
        clone.transform.SetSiblingIndex(src.transform.GetSiblingIndex() + 1);

        // 仮の位置：フード画面ボタンの左隣（Inspector の Rect Transform で調整する）
        var rt = (RectTransform)clone.transform;
        var srcRt = (RectTransform)src.transform;
        rt.anchoredPosition = srcRt.anchoredPosition + new Vector2(-260f, 0f);

        // 複製元のクリック先（シーンに保存されたもの）は消す。押した時の処理は AreaSelectMenu がコードで付ける
        var btn = clone.GetComponent<Button>();
        var bso = new SerializedObject(btn);
        bso.FindProperty("m_OnClick.m_PersistentCalls.m_Calls").arraySize = 0;
        bso.ApplyModifiedPropertiesWithoutUndo();

        var img = clone.transform.Find("ShopImage")?.GetComponent<Image>();
        if (img != null) img.sprite = LoadSprite(EntryIconPath);
        else Debug.LogWarning("[IapShopSetupTool] 複製したボタンに ShopImage が無いため、アイコンは手動で差し替えてください。");

        var mso = new SerializedObject(menu);
        mso.FindProperty("iapShopButton").objectReferenceValue = btn;
        mso.ApplyModifiedProperties();

        AddLocEntries();
        EditorSceneManager.MarkSceneDirty(active);
        Selection.activeGameObject = clone;
        Debug.Log($"[IapShopSetupTool] IapShopButton を追加しました（{GetPath(clone.transform)}）。シーンは未保存です。");
        EditorUtility.DisplayDialog("入口ボタン追加",
            "IapShopButton を追加しました（フード画面ボタンの左隣に仮置き）。\n位置を確認・調整してから、シーンを保存してください。", "OK");
    }

    // 戻るボタンをジェムボタンの対角（左下）へ移し、空いた右下（元の戻るボタンの位置）に課金ショップの入口を置く（2026/10/23）
    [MenuItem("Tools/ショップ/03_AreaSelect の戻るボタンを左下・ショップ入口を右下へ移動")]
    private static void MoveEntryAndBackButtons()
    {
        var active = EditorSceneManager.GetActiveScene();
        if (active.path != AreaSelectScenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            active = EditorSceneManager.OpenScene(AreaSelectScenePath, OpenSceneMode.Single);
        }
        var menu = Object.FindFirstObjectByType<AreaSelectMenu>(FindObjectsInactive.Include);
        if (menu == null || menu.backButton == null || menu.iapShopButton == null || menu.gemManagementButton == null)
        {
            EditorUtility.DisplayDialog("ボタン移動", "AreaSelectMenu の Back Button / Gem Management Button / Iap Shop Button のどれかが未設定です。", "OK");
            return;
        }
        var back = (RectTransform)menu.backButton.transform;
        var shop = (RectTransform)menu.iapShopButton.transform;
        var gem = (RectTransform)menu.gemManagementButton.transform;
        if (back.anchoredPosition.x < 0f)
        {
            EditorUtility.DisplayDialog("ボタン移動", "戻るボタンは既に左側にあります。何もしません。", "OK");
            return;
        }
        Undo.RecordObjects(new Object[] { back, shop }, "Move Back/Shop Buttons");
        var oldBack = back.anchoredPosition;
        // 対角：ジェムボタンのX座標を左右反転、Yは元の戻るボタンと同じ高さ
        back.anchoredPosition = new Vector2(-gem.anchoredPosition.x, oldBack.y);
        shop.anchoredPosition = oldBack;
        EditorUtility.SetDirty(back);
        EditorUtility.SetDirty(shop);
        EditorSceneManager.MarkSceneDirty(active);
        Debug.Log($"[IapShopSetupTool] BackButton → {back.anchoredPosition}、IapShopButton → {shop.anchoredPosition}（シーンは未保存）");
        EditorUtility.DisplayDialog("ボタン移動", $"戻るボタンを左下 {back.anchoredPosition}、ショップ入口を右下 {shop.anchoredPosition} に移動しました。\n確認してからシーンを保存してください（Ctrl+Z で戻せます）。", "OK");
    }

    // ───────────────────────── 共通 ─────────────────────────

    private static void AddLocEntries()
    {
        var table = AssetDatabase.LoadAssetAtPath<LocalizationTable>(LocPath);
        if (table == null) { Debug.LogWarning("[IapShopSetupTool] 翻訳表が見つかりません。"); return; }
        Undo.RecordObject(table, "Add Shop Localization");
        foreach (var (key, ja, en) in LocEntries)
        {
            var e = table.entries.Find(x => x != null && x.key == key);
            if (e == null) { e = new LocalizationTable.Entry { key = key }; table.entries.Add(e); }
            e.japanese = ja; e.english = en;
        }
        EditorUtility.SetDirty(table);
        AssetDatabase.SaveAssets();
    }

    private static void AddToBuildSettings(string path)
    {
        var scenes = EditorBuildSettings.scenes.ToList();
        if (scenes.Any(s => s.path == path)) return;
        scenes.Add(new EditorBuildSettingsScene(path, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    private static Sprite LoadSprite(string path)
    {
        var s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (s == null) Debug.LogWarning($"[IapShopSetupTool] 画像が見つかりません：{path}");
        return s;
    }

    private static RectTransform NewUI(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static void Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    private static Image AddImage(RectTransform rt, Sprite sprite, Color color)
    {
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.preserveAspect = false;
        return img;
    }

    private static TextMeshProUGUI NewText(string name, Transform parent, TMP_FontAsset font, float size, string text, LanguageFontSwitch.Kind kind, TextAlignmentOptions align)
    {
        var rt = NewUI(name, parent);
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) { t.font = font; t.fontSharedMaterial = font.material; }
        t.fontSize = size;
        t.enableAutoSizing = true;
        t.fontSizeMin = size * 0.5f;
        t.fontSizeMax = size;
        t.text = text;
        t.alignment = align;
        t.color = Color.white;
        t.raycastTarget = false;
        rt.gameObject.AddComponent<LanguageFontSwitch>().kind = kind;
        return t;
    }

    private static string GetPath(Transform t) => t.parent == null ? t.name : GetPath(t.parent) + "/" + t.name;
}
