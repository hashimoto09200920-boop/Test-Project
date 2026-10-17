using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Game.Gems;
using Game.Progress;
using Game.Shop;
using Game.Skills;

/// <summary>
/// ドリンク購入画面（04_Drink）の演出（2回目）。ShopUIの処理には手を入れず、購入の通知を受けて見た目だけを足す。
/// ⑧ 購入：ドリンクの絵がカードから購入枠へ飛び込み、光の粒が弾ける→左のスキル欄の上がったマスへ粒が飛ぶ
/// ⑩ 所持ゴールドの数字が減っていくカウントダウン＋コインがゴールドの表示からカードへ飛ぶ
/// ⑪ 画面を開いた時・ページを替えた時、カードが順にポンと出る
/// （⑨店主の反応・⑫背景はユーザー判断で不要となり削除）
/// ★この画面はOPPO端末でチカチカした前歴があるため、動くのは購入などの短い時間だけにする。⑪はInspectorで止められる。
///  部品はメニュー「Tools/ドリンク画面/演出（2回目）を組み込む（04_Drink）」でPlay前に生成する
/// </summary>
[DefaultExecutionOrder(10)] // DrinkScreenFX（購入枠の絵を出す）より後に動き、飛び込むまで絵を隠す
public class DrinkScreenFXAnim : MonoBehaviour
{
    [Header("参照（メニューで自動設定）")]
    [SerializeField] private ShopUI shop;
    [SerializeField] private DrinkScreenFX screenFX;
    [SerializeField] private GemSkillPreviewHUD previewHUD;
    [Tooltip("粒・飛ぶ絵・コイン・吹き出しを描く最前面の入れ物（ShopPanelの子）")]
    [SerializeField] private RectTransform frontRoot;
    [SerializeField] private Image[] particles;
    [SerializeField] private Image flyingDrink;
    [SerializeField] private Sprite glowSprite;
    [SerializeField] private Sprite coinSprite;

    [Header("⑧ 購入")]
    [SerializeField] private float drinkFlySeconds = 0.55f;
    [SerializeField] private Color burstColorA = new Color(0.95f, 0.3f, 0.6f, 1f);
    [SerializeField] private Color burstColorB = new Color(0.45f, 0.95f, 1f, 1f);
    [SerializeField] private Color skillParticleColor = new Color(0.85f, 0.45f, 1f, 1f);
    [SerializeField] private float skillFlySeconds = 0.55f;


    [Header("⑩ ゴールド")]
    [SerializeField] private float goldCountSeconds = 0.6f;
    [SerializeField] private int coinCount = 6;
    [SerializeField] private float coinFlySeconds = 0.5f;

    [Header("⑪ カードの登場")]
    [Tooltip("OFFにするとカードの登場演出を出さない（OPPO等でチカチカする場合）")]
    [SerializeField] private bool enableCardIntro = true;
    [SerializeField] private float introStagger = 0.05f;
    [SerializeField] private float introSeconds = 0.25f;


    private struct P
    {
        public bool alive; public float age, life, delay;
        public Vector2 from, to, ctrl; public float size0, size1; public Color color; public bool coin;
    }
    private P[] ps;

    // ⑧ 飛んでいるドリンク
    private float drinkAge = -1f; private Vector2 drinkFrom, drinkTo; private int drinkSlot = -1;
    private List<(SkillDefinition skill, int points)> pendingBoosts;
    // ⑩
    private float goldAge = -1f; private int goldFrom, goldTo; private TextMeshProUGUI goldText; private GoldHUD goldHud;
    // ⑪
    private float introStart = -1f; private int lastPage = -1; private bool cardsDirty;
    private bool panelWasOpen;

    private void Awake()
    {
        ps = new P[particles != null ? particles.Length : 0];
        HideAll();
    }

    private void OnEnable()
    {
        if (shop == null) return;
        shop.OnDrinkPurchased += HandlePurchased;
        shop.OnDrinkCardsRefreshed += HandleCardsRefreshed;
    }

    private void OnDisable()
    {
        if (shop == null) return;
        shop.OnDrinkPurchased -= HandlePurchased;
        shop.OnDrinkCardsRefreshed -= HandleCardsRefreshed;
    }

    private void HideAll()
    {
        if (particles != null) foreach (var p in particles) if (p != null) p.enabled = false;
        if (flyingDrink != null) flyingDrink.enabled = false;
    }

    private void HandleCardsRefreshed() { cardsDirty = true; }

    /// <summary>
    /// カードが奥から迫ってくる演出（⑪）の最中か（これから始まる予定も含む）。
    /// ゴールド不足の表示（DrinkScreenFX）を、この演出が終わってからフェードインで出すために使う
    /// </summary>
    public bool IsCardIntroPlaying =>
        enableCardIntro && shop != null && (introStart >= 0f || cardsDirty || shop.CurrentPage != lastPage);

    // ================= 購入 =================
    private void HandlePurchased(DrinkDefinition drink, GameObject card, int goldBefore, List<(SkillDefinition skill, int points)> boosts)
    {
        if (frontRoot == null) return;
        // ⑧ ドリンクの絵：カードの絵の位置→購入枠（今回の購入は購入順の最後）
        drinkSlot = DrinkSession.PurchasedOrder.Count - 1;
        var icons = screenFX != null ? screenFX.SlotIcons : null;
        var slotIcon = icons != null && drinkSlot >= 0 && drinkSlot < icons.Length ? icons[drinkSlot] : null;
        var cardIcon = card != null ? card.transform.Find("DrinkIcon") : null;
        if (flyingDrink != null && slotIcon != null)
        {
            drinkFrom = ToLocal(cardIcon != null ? cardIcon.position : card.transform.position);
            drinkTo = ToLocal(slotIcon.transform.position);
            flyingDrink.sprite = drink.icon;
            flyingDrink.preserveAspect = true;
            drinkAge = 0f;
        }
        pendingBoosts = boosts;

        // ⑩ ゴールド：カウントダウン＋コインが所持ゴールドの表示からカードへ
        if (goldHud == null) FindGold();
        goldFrom = goldBefore; goldTo = GoldManager.Instance != null ? GoldManager.Instance.PersistentGold : goldBefore;
        goldAge = goldText != null ? 0f : -1f;
        if (goldHud != null)
        {
            goldHud.Punch(1.2f);
            Vector2 from = ToLocal(goldHud.transform.Find("GoldIcon") != null ? goldHud.transform.Find("GoldIcon").position : goldHud.transform.position);
            Vector2 to = ToLocal(card != null ? card.transform.position : goldHud.transform.position);
            for (int i = 0; i < coinCount; i++)
            {
                Vector2 ctrl = Vector2.Lerp(from, to, 0.5f) + new Vector2(Random.Range(-140f, 140f), Random.Range(60f, 200f));
                Spawn(new P { from = from, to = to + Random.insideUnitCircle * 40f, ctrl = ctrl, life = coinFlySeconds, delay = i * 0.05f, size0 = 30f, size1 = 18f, color = Color.white, coin = true });
            }
        }
    }

    private void FindGold()
    {
        goldHud = FindFirstObjectByType<GoldHUD>();
        goldText = goldHud != null ? goldHud.transform.Find("GoldText")?.GetComponent<TextMeshProUGUI>() : null;
    }

    // ================= 毎フレーム =================
    private void LateUpdate()
    {
        if (shop == null || shop.ShopPanelObject == null) return;
        bool open = shop.ShopPanelObject.activeInHierarchy;
        if (open && !panelWasOpen) { introStart = -1f; lastPage = -1; }
        if (!open && panelWasOpen) HideAll();
        panelWasOpen = open;
        if (!open) return;
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
        float now = Time.unscaledTime;

        // ⑪ 開いた時・ページを替えた時
        if (enableCardIntro && (cardsDirty || shop.CurrentPage != lastPage)) { introStart = now; cardsDirty = false; lastPage = shop.CurrentPage; }
        if (introStart >= 0f) ApplyIntro(now - introStart);

        UpdateDrink(dt);
        UpdateParticles(dt);
        UpdateGold(dt);
    }

    // ---------- ⑪ ----------
    private void ApplyIntro(float t)
    {
        var cards = shop.DrinkCardObjects;
        if (cards == null) return;
        bool done = true; int n = 0;
        foreach (var c in cards)
        {
            if (c == null || !c.activeInHierarchy) continue;
            float k = Mathf.Clamp01((t - n * introStagger) / Mathf.Max(0.05f, introSeconds));
            n++;
            if (k < 1f) done = false;
            float s = k <= 0f ? 0.6f : k < 0.7f ? Mathf.Lerp(0.6f, 1.04f, k / 0.7f) : Mathf.Lerp(1.04f, 1f, (k - 0.7f) / 0.3f);
            c.transform.localScale = Vector3.one * s;
        }
        if (done)
        {
            foreach (var c in cards) if (c != null) c.transform.localScale = Vector3.one;
            introStart = -1f;
        }
    }

    // ---------- ⑧ ----------
    private void UpdateDrink(float dt)
    {
        var icons = screenFX != null ? screenFX.SlotIcons : null;
        var slotIcon = icons != null && drinkSlot >= 0 && drinkSlot < icons.Length ? icons[drinkSlot] : null;
        if (drinkAge < 0f || flyingDrink == null) return;
        drinkAge += dt;
        float k = drinkAge / Mathf.Max(0.05f, drinkFlySeconds);
        if (slotIcon != null) { Color c = slotIcon.color; c.a = 0f; slotIcon.color = c; } // 飛び込むまで購入枠の絵は隠す
        if (k >= 1f)
        {
            drinkAge = -1f;
            flyingDrink.enabled = false;
            if (slotIcon != null) { slotIcon.color = Color.white; ArriveBurst(ToLocal(slotIcon.transform.position)); }
            SendToSkills(slotIcon != null ? ToLocal(slotIcon.transform.position) : drinkTo);
            return;
        }
        flyingDrink.enabled = true;
        float e = k * k * (3f - 2f * k);
        Vector2 ctrl = Vector2.Lerp(drinkFrom, drinkTo, 0.5f) + new Vector2(0f, 220f);
        Vector2 a = Vector2.Lerp(drinkFrom, ctrl, e), b = Vector2.Lerp(ctrl, drinkTo, e);
        flyingDrink.rectTransform.anchoredPosition = Vector2.Lerp(a, b, e);
        float size = Mathf.Lerp(120f, 48f, e);
        flyingDrink.rectTransform.sizeDelta = new Vector2(size, size);
        flyingDrink.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(k * Mathf.PI) * 25f);
    }

    private void ArriveBurst(Vector2 at)
    {
        for (int i = 0; i < 12; i++)
        {
            float ang = i / 12f * Mathf.PI * 2f;
            Vector2 to = at + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * Random.Range(50f, 80f);
            Spawn(new P { from = at, to = to, ctrl = Vector2.Lerp(at, to, 0.5f), life = 0.4f, size0 = 18f, size1 = 4f, color = i % 2 == 0 ? burstColorA : burstColorB });
        }
    }

    /// <summary>購入枠から、左のスキル欄の上がったマスへ光の粒を飛ばす</summary>
    private void SendToSkills(Vector2 from)
    {
        if (pendingBoosts == null || previewHUD == null) return;
        var gemCounts = CalcGemSkillCounts();
        var totals = new Dictionary<string, int>();
        foreach (var (sk, pts) in pendingBoosts) if (sk != null) { totals.TryGetValue(sk.name, out int c); totals[sk.name] = c + pts; }
        foreach (var kv in totals)
        {
            var card = FindDeep(previewHUD.transform, "SkillCard_" + kv.Key);
            var tiles = card != null ? card.Find("ProgressTiles") : null;
            if (tiles == null) continue;
            gemCounts.TryGetValue(kv.Key, out int gemLvl);
            DrinkSession.ActiveBoosts.TryGetValue(kv.Key, out int shopLvl); // 購入後の値
            int end = Mathf.Min(gemLvl + shopLvl, tiles.childCount), start = Mathf.Max(0, end - kv.Value);
            for (int t = start; t < end; t++)
            {
                Vector2 to = ToLocal(tiles.GetChild(t).position);
                for (int k = 0; k < 2; k++)
                    Spawn(new P { from = from + Random.insideUnitCircle * 10f, to = to, ctrl = Vector2.Lerp(from, to, 0.5f) + new Vector2(Random.Range(-80f, 80f), Random.Range(60f, 160f)),
                        life = skillFlySeconds, delay = (t - start) * 0.06f + k * 0.03f, size0 = 18f, size1 = 7f, color = skillParticleColor });
            }
        }
        pendingBoosts = null;
    }

    // ---------- 粒 ----------
    private void Spawn(P p)
    {
        for (int i = 0; i < ps.Length; i++)
        {
            if (ps[i].alive || particles[i] == null) continue;
            p.alive = true; p.age = -p.delay; ps[i] = p;
            particles[i].sprite = p.coin && coinSprite != null ? coinSprite : glowSprite;
            particles[i].enabled = false;
            return;
        }
    }

    private void UpdateParticles(float dt)
    {
        for (int i = 0; i < ps.Length; i++)
        {
            if (!ps[i].alive) continue;
            ref P p = ref ps[i];
            var img = particles[i];
            p.age += dt;
            if (p.age < 0f) continue;
            float k = p.age / Mathf.Max(0.05f, p.life);
            if (k >= 1f) { p.alive = false; img.enabled = false; if (p.coin && goldHud == null) { } continue; }
            img.enabled = true;
            float e = k * k * (3f - 2f * k);
            Vector2 a = Vector2.Lerp(p.from, p.ctrl, e), b = Vector2.Lerp(p.ctrl, p.to, e);
            img.rectTransform.anchoredPosition = Vector2.Lerp(a, b, e);
            float size = Mathf.Lerp(p.size0, p.size1, k);
            img.rectTransform.sizeDelta = new Vector2(size, size);
            Color c = p.color; c.a = p.coin ? 1f : Mathf.Clamp01((1f - k) * 1.5f);
            img.color = c;
        }
    }

    // ---------- ⑩ ----------
    private void UpdateGold(float dt)
    {
        if (goldAge < 0f || goldText == null) return;
        goldAge += dt;
        float k = Mathf.Clamp01(goldAge / Mathf.Max(0.05f, goldCountSeconds));
        int v = Mathf.RoundToInt(Mathf.Lerp(goldFrom, goldTo, 1f - (1f - k) * (1f - k)));
        goldText.text = v.ToString(); // GoldHUDと同じ書式（区切りなし）
        if (k >= 1f) goldAge = -1f;
    }

    // ================= 小物 =================
    private Vector2 ToLocal(Vector3 world) => frontRoot != null ? (Vector2)frontRoot.InverseTransformPoint(world) : Vector2.zero;

    private static Transform FindDeep(Transform r, string n)
    {
        if (r.name == n) return r;
        for (int i = 0; i < r.childCount; i++) { var x = FindDeep(r.GetChild(i), n); if (x != null) return x; }
        return null;
    }

    private static Dictionary<string, int> CalcGemSkillCounts()
    {
        var result = new Dictionary<string, int>();
        var data = ProgressManager.Instance?.Data; var gm = GemManager.Instance;
        if (data == null || gm == null) return result;
        int used = 0;
        foreach (int idx in data.equippedGemIndices)
        {
            if (idx < 0 || idx >= data.gemInventory.Count) continue;
            var def = gm.LoadGemDefinition(data.gemInventory[idx]);
            if (def == null || used + def.requiredSlots > data.slotLevel) continue;
            used += def.requiredSlots;
            foreach (var sk in new[] { gm.LoadBaseSkill(data.gemInventory[idx]), gm.LoadBonusSkill1(data.gemInventory[idx]), gm.LoadBonusSkill2(data.gemInventory[idx]) })
                if (sk != null) { result.TryGetValue(sk.name, out int c); result[sk.name] = c + 1; }
        }
        return result;
    }
}
