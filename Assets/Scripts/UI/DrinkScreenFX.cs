using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Game.Gems;
using Game.Progress;
using Game.Shop;
using Game.Skills;

/// <summary>
/// ドリンク購入画面（04_Drink）の見やすさ改善（1回目）。ShopUI/DrinkCardUIの処理には手を入れず、見た目だけを足す。
/// ① スキル行：ジェム画面と同じ（カテゴリ色＋グラデーション＋左端の縦線＋上縁の光）
/// ② 価格を大きく・所持ゴールドで買えないカードは価格を赤く＋カードを暗く＋「ゴールド不足」
/// ③ 効果の種類の札（確定 +2／ランダム +2）。説明文の「効果：〜」の行は消す（Inspectorで切替可）
/// ④ 購入枠：「0/3」の右に3つの枠を並べ、買ったドリンクの絵を購入順に入れる
/// ⑤ 選択中：光る枠＋四隅のカギ括弧＋わずかな拡大 ⑥ 左のスキル欄：選んだドリンクで上がるマスを紫で点滅
/// ⑦ ページの点：丸い光る点にし、今のページを大きく
/// ★この画面はOPPO端末でチカチカした前歴があるため、毎フレーム書き換えるのは選択中の枠の明滅と⑥の点滅だけにし、
///   それ以外は「選択・ページ・購入・所持ゴールド」が変わった時だけ書き換える。
/// 部品はメニュー「Tools/ドリンク画面/見やすさ改善（1回目）を組み込む（04_Drink）」でPlay前に生成する
/// </summary>
public class DrinkScreenFX : MonoBehaviour
{
    [Header("参照（メニューで自動設定）")]
    [SerializeField] private ShopUI shop;
    [SerializeField] private GemSkillPreviewHUD previewHUD;
    [SerializeField] private RectTransform purchaseSlots;
    [SerializeField] private Image[] slotIcons;
    [SerializeField] private Sprite dotSprite;
    [Tooltip("ドリンク名の文字色")]
    [SerializeField] private Color drinkNameColor = Color.white;

    [Header("① スキル行の左端の縦線（ジェム画面と同じ考え方）")]
    [SerializeField] private float accentSaturationScale = 1.6f;
    [SerializeField, Range(0f, 1f)] private float accentBrightness = 1f;
    [SerializeField, Range(0f, 0.3f)] private float accentLumaDiff = 0.05f;
    [SerializeField, Range(0.5f, 1f)] private float rowDisplayBrightness = 0.9f;

    [Header("② 価格・ゴールド不足")]
    [SerializeField] private Color priceColor = new Color(1f, 0.88f, 0.4f, 1f);
    [SerializeField] private Color priceShortColor = new Color(1f, 0.35f, 0.35f, 1f);

    [Header("② ゴールド不足の表示")]
    [Tooltip("カードが奥から迫ってくる演出（DrinkScreenFXAnim）。この演出が終わってからゴールド不足の表示を出す。未設定なら同じオブジェクト・シーンから自動で探す")]
    [SerializeField] private DrinkScreenFXAnim cardIntroAnim;
    [Tooltip("ゴールド不足の表示がフェードインする秒数（迫ってくる演出が終わった後）")]
    [SerializeField] private float noGoldFadeInSeconds = 0.25f;
    [Tooltip("はんこ（FX_NoGold > Stamp）を押す動き：表示し始めの大きさの倍率（1で動きなし）")]
    [SerializeField] private float noGoldStampStartScale = 1.7f;
    [Tooltip("はんこを押す動きの秒数（大きい状態から元の大きさに戻るまで。1回だけ動いて止まる）")]
    [SerializeField] private float noGoldStampSeconds = 0.2f;

    [Header("③ 効果の種類の札")]
    [SerializeField] private Color fixedBadgeColor = new Color(1f, 0.78f, 0.25f, 1f);
    [Tooltip("札の文字の色（固定・ランダム共通）")]
    [SerializeField] private Color fixedBadgeTextColor = new Color(0.2f, 0.1f, 0f, 1f);
    [Tooltip("ランダムの札の色（スキル行の薄紫と被らないネオンピンク）")]
    [SerializeField] private Color randomBadgePinkColor = new Color(0.95f, 0.25f, 0.55f, 1f);
    [Tooltip("札の文字の影の色（固定・ランダム共通）")]
    [SerializeField] private Color fixedBadgeShadowColor = new Color(1f, 1f, 1f, 0.45f);
    [Tooltip("ONにすると、説明文の「効果：〜」（英語は Effect: 〜）の行を消す（札と内容が重なるため）")]
    [SerializeField] private bool hideEffectLineInFlavor = true;

    [Header("④ 購入枠")]
    [SerializeField] private Color slotEmptyColor = new Color(1f, 1f, 1f, 0f);

    [Header("⑤ 選択中")]
    [SerializeField] private float selectedScale = 1.04f;
    [SerializeField] private float selectFramePulseSpeed = 3f;
    [Tooltip("選択中の光る枠＋カギ括弧（カード一覧より手前・表示範囲の外に1つだけ置き、選んだカードへ移動する。" +
        "カードの中に置くと、隣のカードの背景画像の下に隠れたり、一覧の表示範囲で切れたりするため）")]
    [SerializeField] private RectTransform selectOverlay;
    [Tooltip("カードの中心から見た、光る枠の中心の位置（カードの枠線＝背景画像の中の線に合わせた値。メニューで自動設定）")]
    [SerializeField] private Vector2 selectFrameOffset;

    [Header("⑥ 左のスキル欄の先出し")]
    [SerializeField] private Color previewColor = new Color(0.8f, 0.45f, 1f, 1f);
    [SerializeField] private float previewBlinkSpeed = 5f;

    [Header("⑦ ページの点")]
    [SerializeField] private Color dotActiveColor = new Color(0.45f, 0.95f, 1f, 1f);
    [SerializeField] private Color dotInactiveColor = new Color(0.5f, 0.55f, 0.65f, 0.6f);
    [SerializeField] private float dotSize = 20f;
    [SerializeField] private float dotActiveScale = 1.35f;
    [Tooltip("今のページの点の点滅の速さ")]
    [SerializeField] private float dotBlinkSpeed = 4f;

    private const string KeyFixed = "drink.badge.fixed";
    private const string KeyRandom = "drink.badge.random";
    private const string KeyRandomRange = "drink.badge.randomRange";
    private const string KeyNoGold = "drink.noGold";

    /// <summary>購入枠の絵（2回目の演出DrinkScreenFXAnimが、ドリンクが飛び込むまで隠すのに使う）</summary>
    public Image[] SlotIcons => slotIcons;

    private GameObject lastSelected;
    private int lastPage = -1;
    private int lastGold = int.MinValue;
    private int lastPurchaseCount = -1;
    private bool dirtyCards = true;
    private readonly List<(Image img, Color orig)> previewTiles = new List<(Image, Color)>();
    private readonly Dictionary<string, Transform> cardCache = new Dictionary<string, Transform>();
    private GameObject previewKey;
    private bool slotsMoved;

    private void OnEnable() { if (shop != null) shop.OnDrinkCardsRefreshed += MarkCardsDirty; }
    private void OnDisable() { if (shop != null) shop.OnDrinkCardsRefreshed -= MarkCardsDirty; RestorePreview(); }
    private void MarkCardsDirty() { dirtyCards = true; }

    private void LateUpdate()
    {
        if (shop == null || shop.ShopPanelObject == null || !shop.ShopPanelObject.activeInHierarchy) return;

        // ④ 購入枠を「0/3」の右へ（「0/3」の文字はShopUIが起動時に作るため、作られた後で一番後ろへ回す）
        // ★ShopUIはAwakeで、この入れ物の先頭以外の子を全部非表示にする（購入枠も巻き込まれる）ため、ここで表示し直す
        if (!slotsMoved && purchaseSlots != null) { purchaseSlots.gameObject.SetActive(true); purchaseSlots.SetAsLastSibling(); slotsMoved = true; }

        int gold = GoldManager.Instance != null ? GoldManager.Instance.PersistentGold : 0;
        int pc = DrinkSession.PurchaseCount;
        var sel = shop.SelectedCardObj;
        bool selChanged = sel != lastSelected;

        if (dirtyCards) { ApplyCardsStatic(); dirtyCards = false; lastGold = int.MinValue; lastSelected = null; selChanged = true; lastPage = -1; }
        if (gold != lastGold || pc != lastPurchaseCount) { ApplyAffordability(gold); ApplySlots(); lastGold = gold; lastPurchaseCount = pc; previewKey = null; }
        if (shop.CurrentPage != lastPage) { ApplyDots(); lastPage = shop.CurrentPage; }
        if (selChanged) { ApplySelection(sel); lastSelected = sel; }

        UpdateNoGoldFade();
        UpdateSelectionPulse(sel);
        UpdatePreview(sel);
        UpdateDotBlink();
    }

    // ================= カードの見た目（作り直されるたび） =================
    private void ApplyCardsStatic()
    {
        var cards = shop.DrinkCardObjects;
        if (cards == null) return;
        string fixedFmt = Loc(KeyFixed, "固定 +{0}");
        string randFmt = Loc(KeyRandom, "ランダム +{0}");
        string randRangeFmt = Loc(KeyRandomRange, "ランダム +{0}〜{1}");
        foreach (var card in cards)
        {
            if (card == null) continue;
            var ui = card.GetComponent<DrinkCardUI>();
            var drink = ui != null ? ui.Drink : null;

            // ドリンク名の影（DrinkCardUIが入れた名前を同じ文字で写す）
            var nameT = card.transform.Find("NamePriceRow/DrinkNameText")?.GetComponent<TextMeshProUGUI>();
            var nameS = card.transform.Find("NamePriceRow/FX_NameShadow")?.GetComponent<TextMeshProUGUI>();
            if (nameT != null && nameS != null)
            {
                // 自動サイズも写す（影だけ大きいまま本体からはみ出し、黒い文字に見えるのを防ぐ）
                nameT.color = drinkNameColor;
                // 影は名前の奥（手前にあると名前が影の色で塗られ、縁だけ明るい黒い文字に見える）
                int ni = nameT.transform.GetSiblingIndex();
                if (nameS.transform.GetSiblingIndex() > ni) nameS.transform.SetSiblingIndex(ni);
                nameS.text = nameT.text;
                nameS.enableAutoSizing = nameT.enableAutoSizing;
                nameS.fontSizeMin = nameT.fontSizeMin;
                nameS.fontSizeMax = nameT.fontSizeMax;
                nameS.fontSize = nameT.fontSize;
            }

            // ① スキル行の左端の縦線
            var skills = card.transform.Find("SkillsContainer");
            if (skills != null)
                for (int r = 1; r <= 3; r++)
                {
                    var row = skills.Find("SkillRow_" + r);
                    if (row == null || !row.gameObject.activeSelf) continue;
                    var bg = row.GetComponent<Image>();
                    var acc = row.Find("FX_Accent")?.GetComponent<Image>();
                    if (bg != null && acc != null) acc.color = Accent(bg.color);
                }

            // ③ 効果の種類の札
            var badge = card.transform.Find("FX_EffectBadge");
            if (badge != null && drink != null)
            {
                badge.gameObject.SetActive(true);
                var bimg = badge.GetComponent<Image>();
                var btxt = badge.Find("Text")?.GetComponent<TextMeshProUGUI>();
                var bsh = badge.Find("Shadow")?.GetComponent<TextMeshProUGUI>();
                string text;
                if (drink.isFixed) text = SafeFormat(fixedFmt, drink.levelUpCount, 0);
                else
                {
                    int lo = drink.selectionCount * drink.minPoint, hi = drink.selectionCount * drink.maxPoint;
                    text = lo == hi ? SafeFormat(randFmt, hi, 0) : SafeFormat(randRangeFmt, lo, hi);
                }
                text = GameNumberFont.WrapNumbers(text); // ★「+2」の部分だけ数字用フォント（Bungee）に（日本語はそのまま）
                if (bimg != null) bimg.color = drink.isFixed ? fixedBadgeColor : randomBadgePinkColor;
                if (btxt != null) { btxt.text = text; btxt.color = fixedBadgeTextColor; }
                if (bsh != null) { bsh.text = text; bsh.color = fixedBadgeShadowColor; }
            }

            // ③ 説明文の「効果：〜」の行（ONの時だけ消す）
            if (hideEffectLineInFlavor)
            {
                var flavor = card.transform.Find("FlavorTextContainer/FlavorText")?.GetComponent<TextMeshProUGUI>();
                if (flavor != null) flavor.text = RemoveEffectLine(flavor.text);
            }

            var frame = card.transform.Find("FX_SelectFrame");
            if (frame != null) frame.gameObject.SetActive(false);
            card.transform.localScale = Vector3.one;
        }
    }

    private static string RemoveEffectLine(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        var lines = new List<string>(s.Replace("\r\n", "\n").Split('\n'));
        lines.RemoveAll(l => l.StartsWith("効果") || l.StartsWith("Effect"));
        return string.Join("\n", lines);
    }

    // ---------- ② 買えるかどうか ----------
    private void ApplyAffordability(int gold)
    {
        var cards = shop.DrinkCardObjects;
        if (cards == null) return;
        bool limitReached = DrinkSession.PurchaseCount >= shop.DrinkLimit;
        string noGold = Loc(KeyNoGold, "ゴールド不足");
        foreach (var card in cards)
        {
            if (card == null) continue;
            var drink = card.GetComponent<DrinkCardUI>()?.Drink;
            if (drink == null) continue;
            bool purchased = DrinkSession.IsPurchased(drink.name);
            bool shortGold = !purchased && gold < drink.price;
            var price = card.transform.Find("NamePriceRow/PriceText")?.GetComponent<TextMeshProUGUI>();
            if (price != null) price.color = shortGold ? priceShortColor : priceColor;
            var ng = card.transform.Find("FX_NoGold");
            if (ng != null)
            {
                bool show = shortGold && !limitReached; // 購入上限に達している時は全部買えないので、ゴールド不足とは出さない
                // ★表示・非表示の切り替えはUpdateNoGoldFade（毎フレーム）で行う：カードが迫ってくる演出の最中は出さず、
                //   終わってからフェードインする（演出中にカード全体を覆う半透明の板が大きさを変えながら描き直され続けると、
                //   スマホでちらついていたため）。ここでは「出すべきか」だけを決める
                noGoldWanted[ng.gameObject] = show;
                if (!show) HideNoGold(ng.gameObject);
                // 以前の「ゴールド不足」の文字（残っていれば。今は非表示）には言語ごとの文言を入れる
                foreach (var nm in s_noGoldTextNames)
                {
                    var tt = ng.transform.Find(nm)?.GetComponent<TextMeshProUGUI>();
                    if (tt != null) tt.text = noGold;
                }
                // 足りない金額（数字なので言語に関係ない）
                if (show)
                {
                    var sh = ng.transform.Find("Stamp/Shortage")?.GetComponent<TextMeshProUGUI>();
                    GameNumberFont.Apply(sh); // ★数字用フォント（Bungee）に切り替え（初回だけ。数字の高さは従来と同じに補正）
                    if (sh != null) sh.text = "-" + (drink.price - gold).ToString();
                }
            }
        }
    }

    // ---------- ② ゴールド不足の表示（迫ってくる演出の後にフェードイン） ----------
    private readonly Dictionary<GameObject, bool> noGoldWanted = new Dictionary<GameObject, bool>();
    private readonly Dictionary<GameObject, float> noGoldAlpha = new Dictionary<GameObject, float>();
    private readonly List<GameObject> noGoldKeys = new List<GameObject>();
    private readonly Dictionary<GameObject, float> noGoldShownTime = new Dictionary<GameObject, float>();

    private void HideNoGold(GameObject ng)
    {
        if (ng.activeSelf) ng.SetActive(false);
        noGoldAlpha[ng] = 0f;
        noGoldShownTime[ng] = 0f;
    }

    private void UpdateNoGoldFade()
    {
        if (noGoldWanted.Count == 0) return;
        if (cardIntroAnim == null) cardIntroAnim = GetComponent<DrinkScreenFXAnim>();
        if (cardIntroAnim == null) cardIntroAnim = FindFirstObjectByType<DrinkScreenFXAnim>();
        bool introPlaying = cardIntroAnim != null && cardIntroAnim.IsCardIntroPlaying;
        float dt = Time.unscaledDeltaTime;

        noGoldKeys.Clear();
        foreach (var kv in noGoldWanted) noGoldKeys.Add(kv.Key);
        foreach (var ng in noGoldKeys)
        {
            if (ng == null) { noGoldWanted.Remove(ng); noGoldAlpha.Remove(ng); continue; } // カードが作り直されて消えた
            if (!noGoldWanted[ng]) continue;
            if (introPlaying) { HideNoGold(ng); continue; } // 迫ってくる演出の最中は出さない

            noGoldAlpha.TryGetValue(ng, out float a);
            noGoldShownTime.TryGetValue(ng, out float shownT);
            if (a >= 1f && shownT >= noGoldStampSeconds && ng.activeSelf) continue; // 表示し終わっている（以降は動かさない）
            var cg = ng.GetComponent<CanvasGroup>();
            if (cg == null) { cg = ng.AddComponent<CanvasGroup>(); cg.blocksRaycasts = false; cg.interactable = false; } // クリックは従来どおり下のカードへ通す
            if (!ng.activeSelf)
            {
                // ★カードの中で一番手前に出す（以前は後から作られた「ランダム／固定」の札より後ろに描かれていた）
                ng.transform.SetAsLastSibling();
                ng.SetActive(true);
            }
            a = noGoldFadeInSeconds > 0f ? Mathf.Min(1f, a + dt / noGoldFadeInSeconds) : 1f;
            noGoldAlpha[ng] = a;
            cg.alpha = a;

            // はんこを押す動き：大きい状態から元の大きさへ（最後は少しだけ縮みすぎてから戻る）。1回だけで、終わったら止まる
            shownT += dt;
            noGoldShownTime[ng] = shownT;
            var stamp = ng.transform.Find("Stamp");
            if (stamp != null)
            {
                float k = noGoldStampSeconds > 0f ? Mathf.Clamp01(shownT / noGoldStampSeconds) : 1f;
                float s = k < 0.75f
                    ? Mathf.Lerp(noGoldStampStartScale, 0.94f, 1f - (1f - k / 0.75f) * (1f - k / 0.75f))
                    : Mathf.Lerp(0.94f, 1f, (k - 0.75f) / 0.25f);
                stamp.localScale = Vector3.one * (k >= 1f ? 1f : s);
            }
        }
    }

    private static readonly string[] s_noGoldTextNames = { "Text", "Shadow", "Stamp/Text", "Stamp/Shadow" };

    // ---------- ④ 購入枠 ----------
    private void ApplySlots()
    {
        if (slotIcons == null) return;
        var order = DrinkSession.PurchasedOrder;
        var cards = shop.DrinkCardObjects;
        for (int i = 0; i < slotIcons.Length; i++)
        {
            var icon = slotIcons[i];
            if (icon == null) continue;
            var slotRoot = icon.transform.parent;
            if (slotRoot != null) slotRoot.gameObject.SetActive(i < shop.DrinkLimit);
            Sprite sp = null;
            if (i < order.Count && cards != null)
                foreach (var c in cards)
                {
                    var d = c != null ? c.GetComponent<DrinkCardUI>()?.Drink : null;
                    if (d != null && d.name == order[i]) { sp = d.icon; break; }
                }
            icon.sprite = sp;
            icon.color = sp != null ? Color.white : slotEmptyColor;
            icon.preserveAspect = true;
        }
    }

    // ---------- ⑦ ページの点 ----------
    private void ApplyDots()
    {
        var cont = shop.PageDotsContainer;
        if (cont == null) return;
        for (int i = 0; i < cont.childCount; i++)
        {
            var img = cont.GetChild(i).GetComponent<Image>();
            if (img == null) continue;
            if (dotSprite != null && img.sprite != dotSprite) img.sprite = dotSprite;
            bool active = i == shop.CurrentPage;
            img.color = active ? dotActiveColor : dotInactiveColor;
            img.rectTransform.sizeDelta = new Vector2(dotSize, dotSize);
            img.rectTransform.localScale = Vector3.one * (active ? dotActiveScale : 1f);
        }
    }

    /// <summary>今のページの点を点滅させる（1つだけなので負荷は小さい）</summary>
    private void UpdateDotBlink()
    {
        var cont = shop.PageDotsContainer;
        if (cont == null || !cont.gameObject.activeInHierarchy) return;
        int p = shop.CurrentPage;
        if (p < 0 || p >= cont.childCount) return;
        var img = cont.GetChild(p).GetComponent<Image>();
        if (img == null) return;
        float k = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * dotBlinkSpeed);
        Color c = dotActiveColor; c.a = Mathf.Lerp(0.45f, 1f, k);
        img.color = c;
        img.rectTransform.localScale = Vector3.one * dotActiveScale * Mathf.Lerp(0.9f, 1.08f, k);
    }

    // ---------- ⑤ 選択中 ----------
    private void ApplySelection(GameObject sel)
    {
        var cards = shop.DrinkCardObjects;
        if (cards == null) return;
        foreach (var card in cards)
        {
            if (card == null) continue;
            bool on = card == sel;
            var f = card.transform.Find("FX_SelectFrame");
            if (f != null) f.gameObject.SetActive(false); // 旧版（カードの中の枠）は使わない
            card.transform.localScale = Vector3.one * (on ? selectedScale : 1f); // ★瞬時に切り替える（毎フレーム大きさを変えない）
        }
        if (selectOverlay != null)
        {
            // ★光る枠はShopPanelの直下に置く（カードの見本の中にあると、見本が非表示のため表示されない）
            var panel = shop.ShopPanelObject != null ? shop.ShopPanelObject.transform : null;
            if (panel != null && selectOverlay.parent != panel) selectOverlay.SetParent(panel, true);
            bool show = sel != null;
            if (selectOverlay.gameObject.activeSelf != show) selectOverlay.gameObject.SetActive(show);
            if (show)
            {
                // ★カードの基準点（pivot）は上辺の中央のため、基準点ではなく矩形の中心から計算する（基準点からだと高さの半分だけ上にずれた）
                var cardRt = sel.transform as RectTransform;
                Vector2 center = cardRt != null ? cardRt.rect.center : Vector2.zero;
                selectOverlay.position = sel.transform.TransformPoint(center + selectFrameOffset);
                selectOverlay.SetAsLastSibling();
            }
        }
    }

    private void UpdateSelectionPulse(GameObject sel)
    {
        if (sel == null || selectOverlay == null) return;
        var f = selectOverlay;
        float a = 0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * selectFramePulseSpeed * Mathf.PI);
        foreach (var img in f.GetComponentsInChildren<Image>())
        {
            Color c = img.color; c.a = a; img.color = c;
        }
    }

    // ---------- ⑥ 左のスキル欄の先出し ----------
    private void UpdatePreview(GameObject sel)
    {
        if (previewHUD == null) return;
        bool stale = previewTiles.Count > 0 && previewTiles[0].img == null;
        if (sel != previewKey || stale)
        {
            RestorePreview();
            BuildPreview(sel);
            previewKey = sel;
        }
        if (previewTiles.Count == 0) return;
        float k = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * previewBlinkSpeed);
        foreach (var (img, orig) in previewTiles)
            if (img != null) img.color = Color.Lerp(orig, previewColor, k);
    }

    private void RestorePreview()
    {
        foreach (var (img, orig) in previewTiles) if (img != null) img.color = orig;
        previewTiles.Clear();
    }

    private void BuildPreview(GameObject sel)
    {
        var drink = sel != null ? sel.GetComponent<DrinkCardUI>()?.Drink : null;
        if (drink == null || DrinkSession.IsPurchased(drink.name)) return;
        var gemCounts = CalcGemSkillCounts();
        var targets = new List<SkillDefinition>();
        if (drink.targetSkill1 != null) targets.Add(drink.targetSkill1);
        if (drink.targetSkill2 != null) targets.Add(drink.targetSkill2);
        if (drink.targetSkill3 != null) targets.Add(drink.targetSkill3);
        // 確定：各スキル +levelUpCount ／ ランダム：各候補スキルに入りうる最大（1回分の最大値）を点滅
        int gain = drink.isFixed ? drink.levelUpCount : Mathf.Max(1, drink.maxPoint);
        foreach (var sk in targets)
        {
            var card = FindCard(sk.name);
            var tiles = card != null ? card.Find("ProgressTiles") : null;
            if (tiles == null) continue;
            gemCounts.TryGetValue(sk.name, out int gemLvl);
            DrinkSession.ActiveBoosts.TryGetValue(sk.name, out int shopLvl);
            int from = gemLvl + shopLvl;
            for (int t = from; t < from + gain && t < tiles.childCount; t++)
            {
                var img = tiles.GetChild(t).GetComponent<Image>();
                if (img != null) previewTiles.Add((img, img.color));
            }
        }
    }

    private static Dictionary<string, int> CalcGemSkillCounts()
    {
        var result = new Dictionary<string, int>();
        var data = ProgressManager.Instance?.Data;
        var gm = GemManager.Instance;
        if (data == null || gm == null) return result;
        int used = 0;
        foreach (int idx in data.equippedGemIndices)
        {
            if (idx < 0 || idx >= data.gemInventory.Count) continue;
            var def = gm.LoadGemDefinition(data.gemInventory[idx]);
            if (def == null || used + def.requiredSlots > data.slotLevel) continue;
            used += def.requiredSlots;
            Add(result, gm.LoadBaseSkill(data.gemInventory[idx])?.name);
            Add(result, gm.LoadBonusSkill1(data.gemInventory[idx])?.name);
            Add(result, gm.LoadBonusSkill2(data.gemInventory[idx])?.name);
        }
        return result;
    }

    private static void Add(Dictionary<string, int> d, string k) { if (string.IsNullOrEmpty(k)) return; d.TryGetValue(k, out int c); d[k] = c + 1; }

    private Transform FindCard(string skillName)
    {
        if (cardCache.TryGetValue(skillName, out var t) && t != null) return t;
        t = FindDeep(previewHUD.transform, "SkillCard_" + skillName);
        cardCache[skillName] = t;
        return t;
    }

    private static Transform FindDeep(Transform r, string n)
    {
        if (r.name == n) return r;
        for (int i = 0; i < r.childCount; i++) { var x = FindDeep(r.GetChild(i), n); if (x != null) return x; }
        return null;
    }

    // ---------- 小物 ----------
    private Color Accent(Color c)
    {
        Color.RGBToHSV(c, out float h, out float s, out float v);
        Color vivid = Color.HSVToRGB(h, Mathf.Clamp01(s * accentSaturationScale), Mathf.Max(v, accentBrightness));
        Color shown = new Color(c.r * rowDisplayBrightness, c.g * rowDisplayBrightness, c.b * rowDisplayBrightness, 1f);
        float rowL = Luma(shown), vivL = Luma(vivid);
        float t = vivL > rowL + 0.0001f ? Mathf.Clamp01(accentLumaDiff / (vivL - rowL)) : 1f;
        Color o = Color.Lerp(shown, vivid, t); o.a = 1f;
        return o;
    }
    private static float Luma(Color c) => 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;
    private static string Loc(string k, string fb) => Game.Localization.LocalizationManager.GetStatic(k, fb);
    private static string SafeFormat(string f, int a, int b) { try { return string.Format(f, a, b); } catch (System.FormatException) { return $"+{a}"; } }
}
