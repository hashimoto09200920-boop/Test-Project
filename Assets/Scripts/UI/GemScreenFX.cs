using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Game.Gems;
using Game.Progress;

/// <summary>
/// ジェム画面（02_Gem）の見やすさ改善（1回目）。GemManagementUIの処理には手を入れず、作り直されたカードに見た目だけを足す。
/// ① スキル行：カテゴリ色を土台に、明るさのグラデーション＋左端の濃い縦線＋上縁の光（UIHorizontalGradientと子のFX_Accent/FX_Gloss）
/// ② カード右上：必要スロット数を◆の数（FX_SlotPips）、残り回数をバッジ（FX_UsesPill。3回以下は赤く脈打つ／無限は虹色）
/// ③ 装備中の札（FX_EquippedTag） ④ スロットのゲージ＋選んだジェムを装備/解除したらどうなるかの点滅（FX_SlotGauge）
/// ⑤ 選択中の光る枠＋四隅のカギ括弧＋わずかな拡大（FX_SelectFrame） ⑥ 所持数（FX_InventoryText）
/// ⑦ 左のスキル欄：選んだジェムを装備すると増えるマスを緑、外すと減るマスを赤で点滅
/// 部品はメニュー「Tools/ジェム画面/見やすさ改善（1回目）を組み込む」でPlay前に生成する
/// </summary>
public class GemScreenFX : MonoBehaviour
{
    [Header("参照（メニューで自動設定）")]
    [SerializeField] private GemManagementUI gemUI;
    [SerializeField] private GemSkillPreviewHUD previewHUD;
    [SerializeField] private Image[] slotCells;
    [SerializeField] private Image[] slotGlows;
    [SerializeField] private TextMeshProUGUI inventoryText;
    [SerializeField] private RectTransform inventoryBadge;
    [SerializeField] private Image inventoryBadgeRim;

    [Header("① スキル行の左端の縦線（カテゴリ色を少し鮮やかに・明るくした色）")]
    [Tooltip("行の色の鮮やかさを何倍にするか")]
    [SerializeField] private float accentSaturationScale = 1.6f;
    [Tooltip("縦線の明るさ（0〜1）。行より暗いと、紫のように隣のカードの枠線と並んで二重線に見えるため、明るくする")]
    [SerializeField, Range(0f, 1f)] private float accentBrightness = 1f;
    [Tooltip("縦線と行の、目に見える明るさの差（0〜1）。大きいと縦線が別の線として浮き、カードの枠線と並んで二重線に見える。" +
        "紫で違和感が無かった差（約0.05）に全カテゴリを揃える")]
    [SerializeField, Range(0f, 0.3f)] private float accentLumaDiff = 0.05f;
    [Tooltip("行の画面上の明るさ＝設定色×この倍率。行は少し透明で暗いカードに重なるため、設定色より約1割暗く表示される（不透明の縦線との差はこの明るさで比べる）")]
    [SerializeField, Range(0.5f, 1f)] private float rowDisplayBrightness = 0.9f;

    [Header("② 残り回数のバッジ")]
    [SerializeField] private int lowUsesThreshold = 3;
    [SerializeField] private Color pillNormalColor = new Color(0.12f, 0.45f, 0.65f, 0.95f);
    [SerializeField] private Color pillLowColor = new Color(0.9f, 0.15f, 0.2f, 1f);
    [SerializeField] private float pillLowPulseSpeed = 4f;
    [SerializeField] private float pillInfiniteCycleSpeed = 0.35f;
    [Tooltip("「∞」の文字の大きさ（数字より大きく）")]
    [SerializeField] private float infiniteFontSize = 28f;
    [Tooltip("「∞」の文字色（背景が明るい虹色のため濃い色）")]
    [SerializeField] private Color infiniteTextColor = new Color(0.07f, 0.06f, 0.2f, 1f);

    [Header("③ 装備中")]
    [Tooltip("装備中のカードの名前の色")]
    [SerializeField] private Color equippedNameColor = new Color(1f, 0.72f, 0.3f, 1f);

    [Header("④ スロットのゲージ")]
    [SerializeField] private Color cellEmptyColor = new Color(0.18f, 0.22f, 0.32f, 0.85f);
    [SerializeField] private Color cellUsedColor = new Color(1f, 0.6f, 0.15f, 1f);
    [Tooltip("選んだジェムを装備した時に使うマス（点滅）")]
    [SerializeField] private Color cellAddColor = new Color(0.4f, 1f, 1f, 1f);
    [Tooltip("上限を超えて装備できない時（点滅）")]
    [SerializeField] private Color cellOverColor = new Color(1f, 0.25f, 0.25f, 1f);
    [Tooltip("装備中のジェムを選んだ時、外すと空くマス（点滅）")]
    [SerializeField] private Color cellRemoveColor = new Color(1f, 1f, 1f, 1f);
    [SerializeField] private float cellBlinkSpeed = 5f;
    [SerializeField] private Color slotTextOverColor = new Color(1f, 0.35f, 0.35f, 1f);
    [Tooltip("使用中のマスのグラデーション（左端→右端）")]
    [SerializeField] private Color cellUsedColorEnd = new Color(1f, 0.88f, 0.3f, 1f);
    [Tooltip("使用中のマスの上を光が走る間隔（秒）")]
    [SerializeField] private float sweepInterval = 2.4f;
    [SerializeField] private float sweepSeconds = 0.7f;
    [SerializeField, Range(0f, 1f)] private float glowAlpha = 0.5f;
    [Tooltip("満タン（使用数＝上限）の時、全体が脈打つ色")]
    [SerializeField] private Color fullPulseColor = new Color(1f, 0.95f, 0.55f, 1f);
    [Tooltip("所持数が上限の時の色（数字・枠。これ以上ジェムを入手できない）")]
    [SerializeField] private Color inventoryFullColor = new Color(1f, 0.6f, 0.2f, 1f);
    [Tooltip("所持数のバッジ：「所持」の文字色")]
    [SerializeField] private Color inventoryLabelColor = new Color(0.55f, 0.85f, 1f, 1f);
    [Tooltip("所持数のバッジ：数字の色（通常時）")]
    [SerializeField] private Color inventoryNumberColor = Color.white;
    [Tooltip("所持数のバッジ：「/32」の色")]
    [SerializeField] private Color inventoryMaxColor = new Color(0.62f, 0.7f, 0.85f, 1f);
    [Tooltip("所持数のバッジ：枠の色（通常時）")]
    [SerializeField] private Color inventoryRimColor = new Color(0.4f, 0.9f, 1f, 0.8f);

    [Header("⑤ 選択中")]
    [SerializeField] private float selectedScale = 1.04f;
    [SerializeField] private float selectFramePulseSpeed = 3f;

    [Header("⑦ 左のスキル欄の先出し")]
    [SerializeField] private Color previewAddColor = new Color(0.35f, 1f, 0.45f, 1f);
    [SerializeField] private Color previewRemoveColor = new Color(1f, 0.3f, 0.3f, 1f);
    [SerializeField] private float previewBlinkSpeed = 5f;

    private const string KeyEquipped = "gem.ui.equipped";
    private const string KeyOwnedLabel = "gem.ui.ownedLabel";

    /// <summary>スロットのゲージのマス（演出の2回目GemScreenFXAnimが粒の行き先に使う）</summary>
    public Image[] SlotCells => slotCells;
    /// <summary>trueの間はカードの拡大縮小に触らない（2回目の演出がカードを動かしている間）</summary>
    public bool SuppressScale { get; set; }

    private TextMeshProUGUI slotLevelText;
    private Color slotTextBaseColor = Color.white;
    private readonly Dictionary<string, Transform> cardCache = new Dictionary<string, Transform>();
    private readonly List<(Image img, Color orig)> previewTiles = new List<(Image, Color)>();
    private bool previewIsAdd;
    private int previewKeySel = int.MinValue;
    private int previewKeyEquipCount = -1;
    private int lastSelected = -2;
    private bool inventoryFull;

    // ★OPPO端末のちらつき対策：毎フレームのメモリ確保・Resources.Loadをなくすためのキャッシュ（見た目は変えない）
    //   ・選択中の光る枠の画像（選択が変わった時・カードが作り直された時だけ取り直す）
    //   ・装備中ジェムのマス割り当て（装備の組み合わせ・スロット数・所持数が変わった時だけ計算し直す）
    //   ・残り回数バッジの部品（カードごとに一度だけ探す）
    private GameObject selFrameItem;
    private Image[] selFrameImages;
    private int allocKey = int.MinValue;
    private int allocUsed;
    private readonly List<(int idx, int start, int req)> allocList = new List<(int, int, int)>();
    private int addReqSel = int.MinValue, addReqKey = int.MinValue, addReqCached;
    private struct PillRefs { public Transform pill; public Image bg; public TextMeshProUGUI txt; }
    private readonly Dictionary<GameObject, PillRefs> pillCache = new Dictionary<GameObject, PillRefs>();

    private void Awake()
    {
        if (slotCells != null && slotCells.Length > 0 && slotCells[0] != null)
        {
            slotLevelText = slotCells[0].GetComponentInParent<TextMeshProUGUI>(true);
            if (slotLevelText != null) slotTextBaseColor = slotLevelText.color;
        }
    }

    private void OnEnable()
    {
        if (gemUI != null) gemUI.OnGemListRefreshed += ApplyCards;
    }

    private void OnDisable()
    {
        if (gemUI != null) gemUI.OnGemListRefreshed -= ApplyCards;
        RestorePreviewTiles();
    }

    // ================= カードの見た目（作り直されるたび） =================
    private void ApplyCards()
    {
        var data = ProgressManager.Instance?.Data;
        var items = gemUI != null ? gemUI.GemItemObjects : null;
        if (data == null || items == null) return;
        // カードが作り直されたので、カードの部品を覚えたキャッシュを捨てる
        pillCache.Clear();
        selFrameItem = null; selFrameImages = null;
        allocKey = int.MinValue; addReqKey = int.MinValue;
        string equippedLabel = Game.Localization.LocalizationManager.GetStatic(KeyEquipped, "装備中");
        string slotLabel = Game.Localization.LocalizationManager.GetStatic("gem.ui.slot", "スロット");

        for (int i = 0; i < items.Count && i < data.gemInventory.Count; i++)
        {
            var item = items[i];
            if (item == null) continue;
            var inst = data.gemInventory[i];
            var def = GemManager.Instance?.LoadGemDefinition(inst);
            bool equipped = data.equippedGemIndices.Contains(i);

            // ③ 装備中の札（文字と影の両方に同じ文言を入れる）
            var tag = item.transform.Find("FX_EquippedTag");
            if (tag != null)
            {
                tag.gameObject.SetActive(equipped);
                foreach (var t in tag.GetComponentsInChildren<TextMeshProUGUI>(true)) t.text = equippedLabel;
            }
            // ④' 装備中のカードは名前もオレンジに（カードは毎回見本から作り直されるため、未装備は見本の色のまま）
            if (equipped)
            {
                var nameText = item.transform.Find("TextContainer/NameRow/NameText")?.GetComponent<TextMeshProUGUI>();
                if (nameText != null) nameText.color = equippedNameColor;
            }

            // ② 必要スロット数：上のゲージと同じマスを必要な数だけ（左に「スロット」の文字）
            var pips = item.transform.Find("TextContainer/NameRow/FX_SlotPips");
            if (pips != null)
            {
                int req = def != null ? def.requiredSlots : 0;
                for (int p = 0; p < 3; p++)
                {
                    var pip = pips.Find("Pip" + p)?.GetComponent<Image>();
                    if (pip == null) continue;
                    pip.gameObject.SetActive(p < req);
                    pip.color = cellUsedColor; // ★全マス同じ色（グラデーションにすると2マスが別の色に見えるため）
                }
                var label = pips.Find("Label") as RectTransform;
                if (label != null)
                {
                    label.anchoredPosition = new Vector2(-(Mathf.Max(1, req) * 18f) - 4f, 0f); // 一番左のマスの左に詰める
                    var lt = label.GetComponent<TextMeshProUGUI>();
                    if (lt != null) lt.text = slotLabel;
                }
            }

            // ① スキル行の左端の縦線：行の背景色（カテゴリ色）を濃くした色
            var icons = item.transform.Find("TextContainer/SkillIconsContainer");
            if (icons != null)
            {
                foreach (var rn in RowNames)
                {
                    var row = icons.Find(rn);
                    if (row == null || !row.gameObject.activeSelf) continue;
                    var bg = row.GetComponent<Image>();
                    var accent = row.Find("FX_Accent")?.GetComponent<Image>();
                    if (bg != null && accent != null) accent.color = Vivid(bg.color);
                }
            }

            var frame = item.transform.Find("FX_SelectFrame");
            if (frame != null) frame.gameObject.SetActive(false);
            item.transform.localScale = Vector3.one;
        }

        // ⑥ 所持数
        if (inventoryText != null)
        {
            // 「所持」は小さめの水色、数字は大きく太字、「/32」は小さめの灰青（数字を一番目立たせる）
            inventoryFull = data.gemInventory.Count >= GemManager.MaxInventory;
            string label = Game.Localization.LocalizationManager.GetStatic(KeyOwnedLabel, "所持");
            string num = ColorUtility.ToHtmlStringRGB(inventoryFull ? inventoryFullColor : inventoryNumberColor);
            inventoryText.color = Color.white;
            // ★文字の大きさは全部同じ（ユーザー指示）。色だけ分ける
            inventoryText.text = $"<color=#{ColorUtility.ToHtmlStringRGB(inventoryLabelColor)}>{label}</color> " +
                                 $"<color=#{num}>{data.gemInventory.Count}</color>" +
                                 $"<color=#{ColorUtility.ToHtmlStringRGB(inventoryMaxColor)}>/{GemManager.MaxInventory}</color>";
            if (inventoryBadgeRim != null) inventoryBadgeRim.color = inventoryFull ? inventoryFullColor : inventoryRimColor;
        }

        lastSelected = -2;          // 選択表示を付け直す
        previewKeySel = int.MinValue; // 左のスキル欄の先出しも計算し直す
    }

    private static readonly string[] RowNames = { "SkillRow_Base", "SkillRow_Bonus1", "SkillRow_Bonus2" };

    private Color Vivid(Color c)
    {
        Color.RGBToHSV(c, out float h, out float s, out float v);
        Color vivid = Color.HSVToRGB(h, Mathf.Clamp01(s * accentSaturationScale), Mathf.Max(v, accentBrightness));
        // ★行の「画面上の色」（設定色×rowDisplayBrightness）から、明るさの差がaccentLumaDiffになるところまで鮮やかな色へ寄せる。
        //   設定色どうしで比べると、行だけ画面上で暗く見える分、縦線が浮いて二重線に見えていた（水色・黄で発生）
        Color shown = new Color(c.r * rowDisplayBrightness, c.g * rowDisplayBrightness, c.b * rowDisplayBrightness, 1f);
        float rowL = Luma(shown), vividL = Luma(vivid);
        float t = vividL > rowL + 0.0001f ? Mathf.Clamp01(accentLumaDiff / (vividL - rowL)) : 1f;
        Color o = Color.Lerp(shown, vivid, t);
        o.a = 1f;
        return o;
    }

    /// <summary>装備の組み合わせ・スロット数・所持数から作る目印（メモリ確保なし）。変わった時だけマス割り当てを計算し直すために使う</summary>
    public static int AllocKey(ProgressData d)
    {
        unchecked
        {
            int h = d.slotLevel * 31 + d.gemInventory.Count;
            h = h * 31 + d.equippedGemIndices.Count;
            for (int i = 0; i < d.equippedGemIndices.Count; i++) h = h * 31 + d.equippedGemIndices[i] + 1;
            return h;
        }
    }

    private static float Luma(Color c) => 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;

    // ================= 毎フレーム =================
    private void LateUpdate()
    {
        if (gemUI == null || gemUI.GemPanel == null || !gemUI.GemPanel.activeInHierarchy) return;
        var data = ProgressManager.Instance?.Data;
        var items = gemUI.GemItemObjects;
        if (data == null || items == null) return;
        float now = Time.unscaledTime;
        int sel = gemUI.SelectedGemIndex;
        if (sel >= items.Count) sel = -1;

        UpdateSelection(items, sel, now);
        UpdatePills(items, data, now);
        UpdateGauge(data, sel, now);
        UpdateHudPreview(data, sel, now);
        UpdateInventoryBadge(now);
    }

    /// <summary>所持数が上限の時だけ、枠が脈打つ</summary>
    private void UpdateInventoryBadge(float now)
    {
        if (!inventoryFull || inventoryBadgeRim == null) return;
        float k = 0.5f + 0.5f * Mathf.Sin(now * 4f);
        Color c = inventoryFullColor; c.a = Mathf.Lerp(0.45f, 1f, k);
        inventoryBadgeRim.color = c;
    }

    // ---------- ⑤ 選択中 ----------
    private void UpdateSelection(IReadOnlyList<GameObject> items, int sel, float now)
    {
        if (sel != lastSelected)
        {
            for (int i = 0; i < items.Count; i++)
            {
                var it = items[i];
                if (it == null) continue;
                var f = it.transform.Find("FX_SelectFrame");
                if (f != null) f.gameObject.SetActive(i == sel);
            }
            lastSelected = sel;
        }
        float dt = Time.unscaledDeltaTime;
        for (int i = 0; i < items.Count && !SuppressScale; i++)
        {
            var it = items[i];
            if (it == null) continue;
            float target = i == sel ? selectedScale : 1f;
            float cur = it.transform.localScale.x;
            if (Mathf.Approximately(cur, target)) continue; // ★変化が無い時は触らない（毎フレームの描き直しを起こさない）
            float s = Mathf.Abs(cur - target) < 0.001f ? target : Mathf.Lerp(cur, target, 1f - Mathf.Exp(-16f * dt));
            it.transform.localScale = Vector3.one * s;
        }
        if (sel >= 0 && items[sel] != null)
        {
            if (selFrameItem != items[sel] || selFrameImages == null)
            {
                selFrameItem = items[sel];
                var f = selFrameItem.transform.Find("FX_SelectFrame");
                selFrameImages = f != null ? f.GetComponentsInChildren<Image>(true) : new Image[0];
            }
            float a = 0.6f + 0.4f * Mathf.Sin(now * selectFramePulseSpeed * Mathf.PI);
            for (int k = 0; k < selFrameImages.Length; k++)
            {
                var img = selFrameImages[k];
                if (img == null || !img.gameObject.activeInHierarchy) continue; // 以前（GetComponentsInChildren(false)）と同じく表示中のものだけ
                Color c = img.color; c.a = a; img.color = c;
            }
        }
    }

    // ---------- ② 残り回数のバッジ ----------
    private float lastPillUpdate = -1f;
    private float pillBaseFontSize = -1f;
    private Color pillBaseTextColor = Color.white;

    private void UpdatePills(IReadOnlyList<GameObject> items, ProgressData data, float now)
    {
        // ★全カードの色を毎フレーム変えると描き直しが毎フレーム起きるため、1秒に20回までにする（無制限ON時は全カードが虹色になるため）
        if (now - lastPillUpdate < 0.05f) return;
        lastPillUpdate = now;
        bool unlimitedAll = GemManager.Instance != null && GemManager.Instance.HasUnlimitedGemUses;
        for (int i = 0; i < items.Count && i < data.gemInventory.Count; i++)
        {
            var it = items[i];
            if (it == null) continue;
            if (!pillCache.TryGetValue(it, out var refs))
            {
                refs.pill = it.transform.Find("TextContainer/NameRow/FX_UsesPill");
                refs.bg = refs.pill != null ? refs.pill.GetComponent<Image>() : null;
                refs.txt = refs.pill != null ? refs.pill.GetComponentInChildren<TextMeshProUGUI>(true) : null;
                pillCache[it] = refs;
            }
            var pill = refs.pill;
            if (pill == null) continue;
            var bg = refs.bg;
            var txt = refs.txt;
            var inst = data.gemInventory[i];
            bool inf = inst.isInfinite || unlimitedAll;
            if (txt != null)
            {
                if (pillBaseFontSize < 0f) { pillBaseFontSize = txt.fontSize; pillBaseTextColor = txt.color; }
                string want = inf ? "∞" : inst.remainingUses.ToString();
                if (txt.text != want) txt.text = want;
                // ★「∞」は字形が小さいため大きく、背景が明るい虹色になるため濃い色の文字にする
                float size = inf ? infiniteFontSize : pillBaseFontSize;
                if (!Mathf.Approximately(txt.fontSize, size)) txt.fontSize = size;
                txt.color = inf ? infiniteTextColor : pillBaseTextColor;
            }
            if (bg == null) continue;
            if (inf)
            {
                float h = Mathf.Repeat(now * pillInfiniteCycleSpeed + i * 0.13f, 1f);
                Color c = Color.HSVToRGB(h, 0.65f, 0.95f); c.a = 0.95f;
                bg.color = c;
            }
            else if (inst.remainingUses <= lowUsesThreshold)
            {
                float k = 0.5f + 0.5f * Mathf.Sin(now * pillLowPulseSpeed);
                bg.color = Color.Lerp(pillLowColor * 0.65f, pillLowColor, k);
                pill.localScale = Vector3.one * (1f + 0.08f * k);
            }
            else
            {
                bg.color = pillNormalColor;
                if (pill.localScale != Vector3.one) pill.localScale = Vector3.one; // 変化が無い時は触らない
            }
        }
    }

    // ---------- ④ スロットのゲージ ----------
    private void UpdateGauge(ProgressData data, int sel, float now)
    {
        if (slotCells == null || slotCells.Length == 0) return;
        int level = data.slotLevel;

        // 装備中ジェムのマス割り当て（GemManagerと同じく、上限を超える分は数えない）。組み合わせが変わった時だけ計算し直す
        int key = AllocKey(data);
        if (key != allocKey)
        {
            allocKey = key;
            allocList.Clear();
            allocUsed = 0;
            foreach (int idx in data.equippedGemIndices)
            {
                if (idx < 0 || idx >= data.gemInventory.Count) continue;
                var def = GemManager.Instance?.LoadGemDefinition(data.gemInventory[idx]);
                if (def == null || allocUsed + def.requiredSlots > level) continue;
                allocList.Add((idx, allocUsed, def.requiredSlots));
                allocUsed += def.requiredSlots;
            }
        }
        int used = allocUsed, selStart = -1, selReq = 0;
        for (int k = 0; k < allocList.Count; k++)
            if (allocList[k].idx == sel) { selStart = allocList[k].start; selReq = allocList[k].req; break; }

        bool selEquipped = sel >= 0 && data.equippedGemIndices.Contains(sel);
        if (sel != addReqSel || key != addReqKey)
        {
            addReqSel = sel; addReqKey = key; addReqCached = 0;
            if (sel >= 0 && !selEquipped && sel < data.gemInventory.Count)
            {
                var def = GemManager.Instance?.LoadGemDefinition(data.gemInventory[sel]);
                addReqCached = def != null ? def.requiredSlots : 0;
            }
        }
        int addReq = addReqCached;
        bool over = addReq > 0 && used + addReq > level;
        float blink = 0.5f + 0.5f * Mathf.Sin(now * cellBlinkSpeed);
        bool full = used >= level && level > 0;
        float fullK = full ? 0.5f + 0.5f * Mathf.Sin(now * 3f) : 0f;
        // 使用中のマスの上を左から右へ走る光（sweepPos：今光っているマスの位置）
        float cyc = Mathf.Repeat(now, Mathf.Max(0.5f, sweepInterval));
        float sweepPos = cyc < sweepSeconds ? Mathf.Lerp(-1.5f, used + 0.5f, cyc / sweepSeconds) : -100f;

        for (int c = 0; c < slotCells.Length; c++)
        {
            var cell = slotCells[c];
            if (cell == null) continue;
            bool visible = c < level;
            if (cell.gameObject.activeSelf != visible) cell.gameObject.SetActive(visible);
            var glow = slotGlows != null && c < slotGlows.Length ? slotGlows[c] : null;
            if (glow != null && glow.gameObject.activeSelf != visible) glow.gameObject.SetActive(visible);
            if (!visible) continue;

            float sweep = c < used ? Mathf.Exp(-(c - sweepPos) * (c - sweepPos) / 0.6f) : 0f;
            Color usedCol = Color.Lerp(cellUsedColor, cellUsedColorEnd, level > 1 ? c / (float)(level - 1) : 0f);
            usedCol = Color.Lerp(usedCol, Color.white, sweep * 0.7f);
            if (full) usedCol = Color.Lerp(usedCol, fullPulseColor, fullK * 0.5f);
            if (glow != null)
            {
                Color gc = usedCol;
                gc.a = c < used ? Mathf.Clamp01(glowAlpha * (0.7f + 0.6f * sweep + 0.4f * fullK)) : 0f;
                glow.color = gc;
            }

            Color col = c < used ? usedCol : cellEmptyColor;
            if (selEquipped && c >= selStart && c < selStart + selReq)
                col = Color.Lerp(cellUsedColor, cellRemoveColor, blink);       // 外すと空くマス
            else if (addReq > 0 && !over && c >= used && c < used + addReq)
                col = Color.Lerp(cellEmptyColor, cellAddColor, blink);         // 装備すると使うマス
            else if (over && c >= used)
                col = Color.Lerp(cellEmptyColor, cellOverColor, blink);        // 上限オーバー
            cell.color = col;
        }

        if (slotLevelText != null)
            slotLevelText.color = over ? Color.Lerp(slotTextBaseColor, slotTextOverColor, blink) : slotTextBaseColor;
    }

    // ---------- ⑦ 左のスキル欄の先出し ----------
    private void UpdateHudPreview(ProgressData data, int sel, float now)
    {
        if (previewHUD == null) return;
        // スキル欄の更新（装備・売却時）でマスが作り直されたら、付け直す
        bool stale = previewTiles.Count > 0 && previewTiles[0].img == null;
        if (sel != previewKeySel || data.equippedGemIndices.Count != previewKeyEquipCount || stale)
        {
            RestorePreviewTiles();
            BuildPreview(data, sel);
            previewKeySel = sel;
            previewKeyEquipCount = data.equippedGemIndices.Count;
        }
        if (previewTiles.Count == 0) return;
        float k = 0.5f + 0.5f * Mathf.Sin(now * previewBlinkSpeed);
        Color target = previewIsAdd ? previewAddColor : previewRemoveColor;
        foreach (var (img, orig) in previewTiles)
            if (img != null) img.color = Color.Lerp(orig, target, k);
    }

    private void RestorePreviewTiles()
    {
        foreach (var (img, orig) in previewTiles)
            if (img != null) img.color = orig;
        previewTiles.Clear();
    }

    private void BuildPreview(ProgressData data, int sel)
    {
        if (sel < 0 || sel >= data.gemInventory.Count || GemManager.Instance == null) return;
        var gm = GemManager.Instance;
        var inst = data.gemInventory[sel];
        bool equipped = data.equippedGemIndices.Contains(sel);

        // 今の各スキルのジェム由来レベル（GemSkillPreviewHUDと同じ数え方）
        var gemCounts = new Dictionary<string, int>();
        int used = 0;
        bool selCounted = false;
        foreach (int idx in data.equippedGemIndices)
        {
            if (idx < 0 || idx >= data.gemInventory.Count) continue;
            var d = gm.LoadGemDefinition(data.gemInventory[idx]);
            if (d == null || used + d.requiredSlots > data.slotLevel) continue;
            used += d.requiredSlots;
            if (idx == sel) selCounted = true;
            AddCount(gemCounts, gm.LoadBaseSkill(data.gemInventory[idx])?.name);
            AddCount(gemCounts, gm.LoadBonusSkill1(data.gemInventory[idx])?.name);
            AddCount(gemCounts, gm.LoadBonusSkill2(data.gemInventory[idx])?.name);
        }

        var def = gm.LoadGemDefinition(inst);
        if (def == null) return;
        if (!equipped && used + def.requiredSlots > data.slotLevel) return; // 上限オーバーで装備できない
        if (equipped && !selCounted) return;                               // 上限超過で効いていない装備
        previewIsAdd = !equipped;

        var gains = new Dictionary<string, int>();
        AddCount(gains, gm.LoadBaseSkill(inst)?.name);
        AddCount(gains, gm.LoadBonusSkill1(inst)?.name);
        AddCount(gains, gm.LoadBonusSkill2(inst)?.name);

        foreach (var kv in gains)
        {
            var card = FindCard(kv.Key);
            var tilesRoot = card != null ? card.Find("ProgressTiles") : null;
            if (tilesRoot == null) continue;
            gemCounts.TryGetValue(kv.Key, out int gemLvl);
            Game.Shop.DrinkSession.ActiveBoosts.TryGetValue(kv.Key, out int shopLvl);
            int from, to;
            if (previewIsAdd) { from = gemLvl + shopLvl; to = from + kv.Value; } // 増えるマス（今埋まっている次から）
            else { to = gemLvl; from = gemLvl - kv.Value; }                     // 減るマス（ジェム由来の最後から）
            for (int t = Mathf.Max(0, from); t < to && t < tilesRoot.childCount; t++)
            {
                var img = tilesRoot.GetChild(t).GetComponent<Image>();
                if (img != null) previewTiles.Add((img, img.color));
            }
        }
    }

    private Transform FindCard(string skillName)
    {
        if (string.IsNullOrEmpty(skillName)) return null;
        if (cardCache.TryGetValue(skillName, out var t) && t != null) return t;
        t = FindDeep(previewHUD.transform, "SkillCard_" + skillName);
        cardCache[skillName] = t;
        return t;
    }

    private static Transform FindDeep(Transform root, string name)
    {
        if (root.name == name) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            var r = FindDeep(root.GetChild(i), name);
            if (r != null) return r;
        }
        return null;
    }

    private static void AddCount(Dictionary<string, int> d, string key)
    {
        if (string.IsNullOrEmpty(key)) return;
        d.TryGetValue(key, out int c);
        d[key] = c + 1;
    }
}
