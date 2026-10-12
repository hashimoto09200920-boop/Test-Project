using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Game.Gems;
using Game.Progress;

/// <summary>
/// ジェム画面（02_Gem）の演出（2回目）。GemManagementUIの処理には手を入れず、通知を受けて見た目だけを足す。
/// ⑧ 装備：カードが光り、光の粒がスロットのゲージへ飛んでマスが点灯／解除：マスから粒がカードへ戻る
/// ⑨ 上部の大きなジェムアイコン（SlotIcon）のきらめき（※カードのジェムアイコンは1回目で非表示にしたため）
/// ⑩ 画面を開いた時、カードが少しずつずれてポンと出る ⑪ 背景の星のまたたき＋光の筋
/// ⑫ 売却：コインが所持ゴールドの表示へ飛ぶ＋「+〇G」／無限化：虹色の輪が広がる
/// 部品はメニュー「Tools/ジェム画面/演出（2回目）を組み込む（02_Gem）」でPlay前に生成する。時間はすべて実時間
/// </summary>
[DefaultExecutionOrder(-10)] // GemScreenFX（カードの選択拡大）より先に動き、カードを動かしている間は拡大処理を止めてもらう
public class GemScreenFXAnim : MonoBehaviour
{
    [Header("参照（メニューで自動設定）")]
    [SerializeField] private GemManagementUI gemUI;
    [SerializeField] private GemScreenFX screenFX;
    [Tooltip("粒・輪・コイン・文字を描く最前面の入れ物（GemManagementPanelの子）")]
    [SerializeField] private RectTransform frontRoot;
    [SerializeField] private Image[] particles;
    [SerializeField] private Image[] rings;
    [SerializeField] private TextMeshProUGUI[] popTexts;
    [SerializeField] private Sprite coinSprite;
    [SerializeField] private Sprite glowSprite;

    [Header("⑧ 装備・解除")]
    [SerializeField] private int particlesPerCell = 6;
    [SerializeField] private float flySeconds = 0.5f;
    [SerializeField] private Color equipParticleColor = new Color(1f, 0.75f, 0.3f, 1f);
    [SerializeField] private Color unequipParticleColor = new Color(0.6f, 0.9f, 1f, 1f);
    [SerializeField] private float cardFlashSeconds = 0.4f;
    [SerializeField] private float cardPunchScale = 1.1f;

    [Header("⑨ 上部のジェムアイコンのきらめき")]
    [SerializeField] private RectTransform slotIcon;
    [SerializeField] private Image slotIconGlow;
    [SerializeField] private Image[] slotIconTwinkles;
    [SerializeField] private float twinkleInterval = 1.6f;

    [Header("⑩ 開いた時のカード")]
    [SerializeField] private float introStagger = 0.035f;
    [SerializeField] private float introSeconds = 0.28f;
    [SerializeField] private float introMaxDelay = 0.6f;

    [Header("⑪ 背景")]
    [SerializeField] private RectTransform bgRoot;
    [SerializeField] private Image[] bgStars;
    [SerializeField] private Image[] bgStreaks;
    [SerializeField] private float streakInterval = 2.2f;
    [SerializeField] private float streakSeconds = 1.2f;
    [Tooltip("背景の更新回数（1秒あたり）。描き直しを抑えるため控えめに")]
    [SerializeField] private float bgUpdateRate = 30f;

    [Header("⑫ 売却・無限化")]
    [SerializeField] private int coinCount = 10;
    [SerializeField] private float coinFlySeconds = 0.7f;
    [SerializeField] private float ringSeconds = 0.7f;
    [SerializeField] private float ringMaxSize = 420f;
    [Tooltip("無限化の虹色の輪の最大の大きさ（カードの上に収まる大きさ）")]
    [SerializeField] private float infiniteRingMaxSize = 280f;
    [Tooltip("無限化の虹色の粒の飛ぶ速さ（最小〜最大）。カードの上に収まるよう控えめに")]
    [SerializeField] private Vector2 infiniteSparkSpeed = new Vector2(90f, 150f);

    // ---- 粒（汎用） ----
    private struct P
    {
        public bool alive; public float age, life, delay;
        public Vector2 from, to, ctrl; public float size0, size1; public Color color; public bool rainbow; public bool coin;
        public int onArriveCell; public bool radial; public Vector2 vel;
    }
    private P[] ps;
    private float[] ringAge; private Vector2[] ringPos; private bool[] ringRainbow;
    private float[] popAge;
    private readonly Dictionary<int, float> cellFlash = new Dictionary<int, float>();
    private readonly Dictionary<int, float> cardFlash = new Dictionary<int, float>(); // カード番号→経過秒
    private int pendingEquipIdx = -1; private bool pendingEquipOn; private List<(int start, int req)> pendingFreed;
    private int pendingInfiniteIdx = -1;
    private Dictionary<int, (int start, int req)> lastAlloc = new Dictionary<int, (int, int)>();
    private bool panelWasOpen;
    private float introStart = -1f;
    private float bgAcc;
    private float nextTwinkle;
    private Transform goldTarget;

    private void Awake()
    {
        ps = new P[particles != null ? particles.Length : 0];
        ringAge = new float[rings != null ? rings.Length : 0]; ringPos = new Vector2[ringAge.Length]; ringRainbow = new bool[ringAge.Length];
        for (int i = 0; i < ringAge.Length; i++) ringAge[i] = -1f;
        popAge = new float[popTexts != null ? popTexts.Length : 0];
        for (int i = 0; i < popAge.Length; i++) popAge[i] = -1f;
        HideAll();
    }

    private void OnEnable()
    {
        if (gemUI == null) return;
        gemUI.OnGemListRefreshed += HandleRefreshed;
        gemUI.OnGemEquipChanged += HandleEquipChanged;
        gemUI.OnBeforeGemSold += HandleSold;
        gemUI.OnGemMadeInfinite += HandleInfinite;
    }

    private void OnDisable()
    {
        if (gemUI == null) return;
        gemUI.OnGemListRefreshed -= HandleRefreshed;
        gemUI.OnGemEquipChanged -= HandleEquipChanged;
        gemUI.OnBeforeGemSold -= HandleSold;
        gemUI.OnGemMadeInfinite -= HandleInfinite;
    }

    private void HideAll()
    {
        if (particles != null) foreach (var p in particles) if (p != null) p.enabled = false;
        if (rings != null) foreach (var r in rings) if (r != null) r.enabled = false;
        if (popTexts != null) foreach (var t in popTexts) if (t != null) t.enabled = false;
    }

    // ================= 通知 =================
    private void HandleEquipChanged(int idx, bool equipped)
    {
        pendingEquipIdx = idx;
        pendingEquipOn = equipped;
        pendingFreed = null;
        pendingEquipPos = CardPosBeforeRefresh(idx);
        // 解除：外す前のマスの位置（前フレームの割り当て）を覚えておく
        if (!equipped && lastAlloc.TryGetValue(idx, out var a)) pendingFreed = new List<(int, int)> { a };
    }

    private void HandleSold(int idx, int price)
    {
        var items = gemUI.GemItemObjects;
        if (items == null || idx < 0 || idx >= items.Count || items[idx] == null) return;
        Vector2 from = ToLocal(items[idx].transform.position);
        if (goldTarget == null) FindGoldTarget();
        Vector2 to = goldTarget != null ? ToLocal(goldTarget.position) : from + new Vector2(300f, 300f);
        int n = Mathf.Max(1, coinCount);
        for (int i = 0; i < n; i++)
        {
            Vector2 start = from + Random.insideUnitCircle * 40f;
            Vector2 ctrl = Vector2.Lerp(start, to, 0.4f) + new Vector2(Random.Range(-120f, 120f), Random.Range(80f, 220f));
            Spawn(new P { from = start, to = to, ctrl = ctrl, life = coinFlySeconds, delay = i * 0.04f, size0 = 30f, size1 = 18f, color = Color.white, coin = true, onArriveCell = -2 });
        }
        if (price > 0) Pop(from, $"+{price}G", new Color(1f, 0.85f, 0.3f, 1f));
        SpawnRing(from, false);
    }

    private void HandleInfinite(int idx)
    {
        pendingInfiniteIdx = idx;
        pendingInfinitePos = CardPosBeforeRefresh(idx);
    }

    // ★作り直した直後のカードは、並べる処理（GridLayoutGroup）がまだ動いておらず位置が正しくない。
    //   通知を受けた時点（作り直す前）の、正しく並んでいる古いカードの位置を使う（無限化・装備では並び順は変わらない）
    private Vector2 pendingEquipPos, pendingInfinitePos;

    private Vector2 CardPosBeforeRefresh(int idx)
    {
        var items = gemUI != null ? gemUI.GemItemObjects : null;
        if (items == null || idx < 0 || idx >= items.Count || items[idx] == null) return Vector2.zero;
        return ToLocal(items[idx].transform.position);
    }

    private void HandleRefreshed()
    {
        var items = gemUI.GemItemObjects;
        var data = ProgressManager.Instance?.Data;
        if (items == null || data == null) return;

        if (pendingEquipIdx >= 0 && pendingEquipIdx < items.Count && items[pendingEquipIdx] != null)
        {
            var card = items[pendingEquipIdx].transform;
            Vector2 cardPos = pendingEquipPos;
            cardFlash[pendingEquipIdx] = 0f;
            if (pendingEquipOn)
            {
                // 装備：カード→新しく点灯するマスへ
                var alloc = CalcAlloc(data);
                if (alloc.TryGetValue(pendingEquipIdx, out var a))
                    for (int c = a.start; c < a.start + a.req; c++) FlyToCell(cardPos, c, equipParticleColor);
            }
            else if (pendingFreed != null)
            {
                // 解除：空いたマス→カードへ
                foreach (var (start, req) in pendingFreed)
                    for (int c = start; c < start + req; c++)
                    {
                        var cell = Cell(c);
                        if (cell == null) continue;
                        Vector2 from = ToLocal(cell.transform.position);
                        for (int k = 0; k < particlesPerCell; k++)
                            Spawn(new P { from = from + Random.insideUnitCircle * 6f, to = cardPos + Random.insideUnitCircle * 50f,
                                ctrl = Vector2.Lerp(from, cardPos, 0.5f) + new Vector2(Random.Range(-90f, 90f), Random.Range(-40f, 120f)),
                                life = flySeconds, delay = k * 0.02f, size0 = 20f, size1 = 6f, color = unequipParticleColor, onArriveCell = -1 });
                    }
            }
        }
        pendingEquipIdx = -1;

        if (pendingInfiniteIdx >= 0 && pendingInfiniteIdx < items.Count && items[pendingInfiniteIdx] != null)
        {
            // ★対象のカードの上に収める（輪はカードの大きさまで、粒も近くまでしか飛ばさない）。カード自体も虹色に光らせる
            Vector2 pos = pendingInfinitePos;
            SpawnRing(pos, true);
            for (int k = 0; k < 16; k++)
            {
                float ang = k / 16f * Mathf.PI * 2f;
                Vector2 v = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * Random.Range(infiniteSparkSpeed.x, infiniteSparkSpeed.y);
                Spawn(new P { from = pos, vel = v, radial = true, life = 0.7f, size0 = 22f, size1 = 4f, rainbow = true, onArriveCell = -1 });
            }
            cardFlash[pendingInfiniteIdx] = 0f;
            rainbowFlash.Add(pendingInfiniteIdx);
        }
        pendingInfiniteIdx = -1;

        // ⑩ 開いた直後の最初の作り直し：カードを順番にポンと出す
        if (introStart < 0f && !panelWasOpenAtRefresh) { introStart = Time.unscaledTime; ApplyIntro(0f); }
        panelWasOpenAtRefresh = true;
    }

    private bool panelWasOpenAtRefresh;

    private void FlyToCell(Vector2 from, int cellIdx, Color col)
    {
        var cell = Cell(cellIdx);
        if (cell == null) return;
        Vector2 to = ToLocal(cell.transform.position);
        for (int k = 0; k < particlesPerCell; k++)
        {
            Vector2 st = from + Random.insideUnitCircle * 50f;
            Spawn(new P { from = st, to = to + Random.insideUnitCircle * 4f,
                ctrl = Vector2.Lerp(st, to, 0.5f) + new Vector2(Random.Range(-100f, 100f), Random.Range(60f, 180f)),
                life = flySeconds, delay = k * 0.025f, size0 = 22f, size1 = 8f, color = col, onArriveCell = cellIdx });
        }
    }

    // ================= 毎フレーム =================
    private void LateUpdate()
    {
        if (gemUI == null || gemUI.GemPanel == null) return;
        bool open = gemUI.GemPanel.activeInHierarchy;
        if (open && !panelWasOpen) ResetBg();
        // ★閉じた時に「開いた直後の演出」を待つ状態へ戻す（開く時は一覧の作り直しの通知の方が先に来ることがあるため、開いた側では戻さない）
        if (!open && panelWasOpen) { HideAll(); introStart = -1f; panelWasOpenAtRefresh = false; }
        panelWasOpen = open;
        if (!open) return;

        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
        float now = Time.unscaledTime;
        var data = ProgressManager.Instance?.Data;
        if (data != null) lastAlloc = CalcAlloc(data);

        if (introStart >= 0f) ApplyIntro(now - introStart);
        UpdateCardFlash(dt);
        if (screenFX != null) screenFX.SuppressScale = introStart >= 0f || punching;
        UpdateParticles(dt);
        UpdateRings(dt);
        UpdatePops(dt);
        UpdateCellFlash(dt);
        UpdateSlotIcon(now);
        bgAcc += dt;
        if (bgAcc >= 1f / Mathf.Max(5f, bgUpdateRate)) { UpdateBg(now); bgAcc = 0f; }
    }

    // ---------- ⑩ 開いた時 ----------
    private void ApplyIntro(float t)
    {
        var items = gemUI.GemItemObjects;
        if (items == null) return;
        bool done = true;
        for (int i = 0; i < items.Count; i++)
        {
            var it = items[i];
            if (it == null) continue;
            float local = t - Mathf.Min(i * introStagger, introMaxDelay);
            float k = Mathf.Clamp01(local / Mathf.Max(0.05f, introSeconds));
            if (k < 1f) done = false;
            float s = k <= 0f ? 0.001f : k < 0.7f ? Mathf.Lerp(0.6f, 1.08f, k / 0.7f) : Mathf.Lerp(1.08f, 1f, (k - 0.7f) / 0.3f);
            it.transform.localScale = Vector3.one * s;
        }
        if (done) introStart = -2f; // 終了（以降は選択の拡大処理に任せる）
    }

    // ---------- ⑧ カードの光 ----------
    private bool punching;
    private readonly HashSet<int> rainbowFlash = new HashSet<int>();
    private static readonly Color flashBaseColor = new Color(1f, 0.95f, 0.8f, 1f);

    private void UpdateCardFlash(float dt)
    {
        punching = false;
        if (cardFlash.Count == 0) return;
        var items = gemUI.GemItemObjects;
        var keys = new List<int>(cardFlash.Keys);
        foreach (int idx in keys)
        {
            float age = cardFlash[idx] + dt;
            cardFlash[idx] = age;
            float k = age / Mathf.Max(0.05f, cardFlashSeconds);
            Transform card = items != null && idx < items.Count && items[idx] != null ? items[idx].transform : null;
            var flash = card != null ? card.Find("FX_Flash")?.GetComponent<Image>() : null;
            if (k >= 1f)
            {
                if (flash != null) flash.enabled = false;
                cardFlash.Remove(idx);
                rainbowFlash.Remove(idx);
                continue;
            }
            if (flash != null)
            {
                flash.enabled = true;
                Color c = rainbowFlash.Contains(idx) ? Color.HSVToRGB(Mathf.Repeat(age * 1.6f, 1f), 0.55f, 1f) : flashBaseColor;
                c.a = (1f - k) * 0.75f; flash.color = c;
            }
            if (card != null && k < 0.35f) { card.localScale = Vector3.one * Mathf.Lerp(cardPunchScale, 1f, k / 0.35f); punching = true; }
        }
    }

    // ---------- 粒 ----------
    private void Spawn(P p)
    {
        for (int i = 0; i < ps.Length; i++)
        {
            if (ps[i].alive || particles[i] == null) continue;
            p.alive = true; p.age = -p.delay;
            ps[i] = p;
            particles[i].sprite = p.coin && coinSprite != null ? coinSprite : glowSprite;
            particles[i].enabled = false; // 待ち時間中は非表示
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
            if (k >= 1f)
            {
                p.alive = false; img.enabled = false;
                if (p.onArriveCell >= 0) cellFlash[p.onArriveCell] = 0f;
                else if (p.onArriveCell == -2) PunchGold();
                continue;
            }
            img.enabled = true;
            Vector2 pos;
            if (p.radial) pos = p.from + p.vel * (p.age - 0.5f * p.age * p.age * 1.2f);
            else
            {
                float e = k * k * (3f - 2f * k);
                Vector2 a = Vector2.Lerp(p.from, p.ctrl, e), b = Vector2.Lerp(p.ctrl, p.to, e);
                pos = Vector2.Lerp(a, b, e);
            }
            img.rectTransform.anchoredPosition = pos;
            float size = Mathf.Lerp(p.size0, p.size1, k);
            img.rectTransform.sizeDelta = new Vector2(size, size);
            Color c = p.rainbow ? Color.HSVToRGB(Mathf.Repeat(i * 0.07f + p.age * 1.5f, 1f), 0.7f, 1f) : p.color;
            c.a = p.coin ? 1f : Mathf.Clamp01((1f - k) * 1.4f);
            img.color = c;
        }
    }

    private void UpdateCellFlash(float dt)
    {
        if (cellFlash.Count == 0 || screenFX == null) return;
        var keys = new List<int>(cellFlash.Keys);
        foreach (int c in keys)
        {
            float age = cellFlash[c] + dt;
            cellFlash[c] = age;
            var cell = Cell(c);
            if (cell == null) { cellFlash.Remove(c); continue; }
            float k = age / 0.3f;
            if (k >= 1f) { cell.rectTransform.localScale = Vector3.one; cellFlash.Remove(c); continue; }
            cell.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.45f, 1f, k);
        }
    }

    // ---------- 輪・文字 ----------
    private void SpawnRing(Vector2 pos, bool rainbow)
    {
        for (int i = 0; i < ringAge.Length; i++)
        {
            if (ringAge[i] >= 0f || rings[i] == null) continue;
            ringAge[i] = 0f; ringPos[i] = pos; ringRainbow[i] = rainbow;
            rings[i].enabled = true;
            rings[i].rectTransform.anchoredPosition = pos;
            return;
        }
    }

    private void UpdateRings(float dt)
    {
        for (int i = 0; i < ringAge.Length; i++)
        {
            if (ringAge[i] < 0f) continue;
            ringAge[i] += dt;
            float k = ringAge[i] / Mathf.Max(0.05f, ringSeconds);
            if (k >= 1f) { ringAge[i] = -1f; rings[i].enabled = false; continue; }
            float size = Mathf.Lerp(60f, ringRainbow[i] ? infiniteRingMaxSize : ringMaxSize * 0.6f, 1f - (1f - k) * (1f - k));
            rings[i].rectTransform.sizeDelta = new Vector2(size, size);
            Color c = ringRainbow[i] ? Color.HSVToRGB(Mathf.Repeat(k * 1.5f, 1f), 0.6f, 1f) : new Color(1f, 0.85f, 0.35f, 1f);
            c.a = 1f - k;
            rings[i].color = c;
        }
    }

    private void Pop(Vector2 pos, string text, Color col)
    {
        for (int i = 0; i < popAge.Length; i++)
        {
            if (popAge[i] >= 0f || popTexts[i] == null) continue;
            popAge[i] = 0f;
            popTexts[i].enabled = true;
            popTexts[i].text = text;
            popTexts[i].color = col;
            popTexts[i].rectTransform.anchoredPosition = pos;
            return;
        }
    }

    private void UpdatePops(float dt)
    {
        for (int i = 0; i < popAge.Length; i++)
        {
            if (popAge[i] < 0f) continue;
            popAge[i] += dt;
            float k = popAge[i] / 1.1f;
            var t = popTexts[i];
            if (k >= 1f) { popAge[i] = -1f; t.enabled = false; continue; }
            t.rectTransform.anchoredPosition += new Vector2(0f, 70f * dt);
            t.rectTransform.localScale = Vector3.one * (k < 0.15f ? Mathf.Lerp(0.5f, 1.2f, k / 0.15f) : Mathf.Lerp(1.2f, 1f, Mathf.Clamp01((k - 0.15f) / 0.15f)));
            Color c = t.color; c.a = k < 0.7f ? 1f : 1f - (k - 0.7f) / 0.3f; t.color = c;
        }
    }

    private Vector3 goldBaseScale = Vector3.one;
    private float goldPunch = -1f;

    private void PunchGold()
    {
        if (goldTarget == null) return;
        if (goldPunch < 0f) goldBaseScale = goldTarget.localScale;
        goldPunch = 0f;
    }

    private void FindGoldTarget()
    {
        var hud = FindFirstObjectByType<GoldHUD>();
        if (hud == null) return;
        var icon = hud.transform.Find("GoldIcon");
        goldTarget = icon != null ? icon : hud.transform;
    }

    // ---------- ⑨ 上部のジェムアイコン ----------
    private void UpdateSlotIcon(float now)
    {
        if (goldPunch >= 0f && goldTarget != null)
        {
            goldPunch += Time.unscaledDeltaTime;
            float k = goldPunch / 0.25f;
            if (k >= 1f) { goldTarget.localScale = goldBaseScale; goldPunch = -1f; }
            else goldTarget.localScale = goldBaseScale * Mathf.Lerp(1.3f, 1f, k);
        }
        if (slotIconGlow != null)
        {
            Color c = slotIconGlow.color; c.a = 0.35f + 0.25f * Mathf.Sin(now * 2.2f); slotIconGlow.color = c;
        }
        if (slotIconTwinkles == null || slotIcon == null) return;
        if (now >= nextTwinkle)
        {
            nextTwinkle = now + twinkleInterval * Random.Range(0.6f, 1.2f);
            foreach (var tw in slotIconTwinkles)
            {
                if (tw == null || tw.enabled) continue;
                Rect r = slotIcon.rect;
                tw.rectTransform.anchoredPosition = new Vector2(Random.Range(r.xMin * 0.6f, r.xMax * 0.6f), Random.Range(r.yMin * 0.5f, r.yMax * 0.6f));
                tw.enabled = true;
                twinkleStart[tw] = now;
                break;
            }
        }
        foreach (var tw in slotIconTwinkles)
        {
            if (tw == null || !tw.enabled) continue;
            float k = (now - (twinkleStart.TryGetValue(tw, out float s) ? s : now)) / 0.6f;
            if (k >= 1f) { tw.enabled = false; continue; }
            float sc = Mathf.Sin(k * Mathf.PI);
            tw.rectTransform.localScale = Vector3.one * sc;
            tw.rectTransform.localRotation = Quaternion.Euler(0f, 0f, k * 90f);
        }
    }
    private readonly Dictionary<Image, float> twinkleStart = new Dictionary<Image, float>();

    // ---------- ⑪ 背景 ----------
    private float[] starPhase; private float[] streakStart; private Vector2[] streakFrom, streakTo;

    private void ResetBg()
    {
        if (bgRoot == null) return;
        Rect r = bgRoot.rect;
        if (bgStars != null)
        {
            starPhase = new float[bgStars.Length];
            for (int i = 0; i < bgStars.Length; i++)
            {
                if (bgStars[i] == null) continue;
                starPhase[i] = Random.Range(0f, 10f);
                bgStars[i].rectTransform.anchoredPosition = new Vector2(Random.Range(r.xMin, r.xMax), Random.Range(r.yMin, r.yMax));
            }
        }
        if (bgStreaks != null)
        {
            streakStart = new float[bgStreaks.Length]; streakFrom = new Vector2[bgStreaks.Length]; streakTo = new Vector2[bgStreaks.Length];
            for (int i = 0; i < bgStreaks.Length; i++) { streakStart[i] = Time.unscaledTime + i * streakInterval * 0.5f; PlanStreak(i, r); }
        }
    }

    /// <summary>背景の光の線（四隅の斜めの線）に沿って光が流れる</summary>
    private void PlanStreak(int i, Rect r)
    {
        int corner = Random.Range(0, 4);
        float sx = corner % 2 == 0 ? -1f : 1f, sy = corner < 2 ? 1f : -1f;
        Vector2 c = new Vector2(sx * r.width * Random.Range(0.25f, 0.42f), sy * r.height * Random.Range(0.2f, 0.38f));
        Vector2 dir = new Vector2(-sx, sy).normalized * (Random.value < 0.5f ? 1f : -1f);
        streakFrom[i] = c - dir * 160f; streakTo[i] = c + dir * 160f;
        if (bgStreaks[i] != null) bgStreaks[i].rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
    }

    private void UpdateBg(float now)
    {
        if (bgRoot == null) return;
        if (bgStars != null && starPhase != null)
            for (int i = 0; i < bgStars.Length; i++)
            {
                if (bgStars[i] == null) continue;
                float a = Mathf.Clamp01(0.15f + 0.85f * Mathf.Pow(0.5f + 0.5f * Mathf.Sin(now * 1.3f + starPhase[i]), 3f));
                Color c = bgStars[i].color; c.a = a; bgStars[i].color = c;
            }
        if (bgStreaks != null && streakStart != null)
        {
            Rect r = bgRoot.rect;
            for (int i = 0; i < bgStreaks.Length; i++)
            {
                var s = bgStreaks[i];
                if (s == null) continue;
                float t = now - streakStart[i];
                if (t < 0f) { s.enabled = false; continue; }
                float k = t / Mathf.Max(0.1f, streakSeconds);
                if (k >= 1f) { streakStart[i] = now + streakInterval * Random.Range(0.6f, 1.4f); PlanStreak(i, r); s.enabled = false; continue; }
                s.enabled = true;
                s.rectTransform.anchoredPosition = Vector2.Lerp(streakFrom[i], streakTo[i], k);
                Color c = s.color; c.a = Mathf.Sin(k * Mathf.PI) * 0.8f; s.color = c;
            }
        }
    }

    // ================= 小物 =================
    private Image Cell(int c)
    {
        var cells = screenFX != null ? screenFX.SlotCells : null;
        return cells != null && c >= 0 && c < cells.Length ? cells[c] : null;
    }

    private Vector2 ToLocal(Vector3 world) => frontRoot != null ? (Vector2)frontRoot.InverseTransformPoint(world) : Vector2.zero;

    /// <summary>装備中ジェムのマスの割り当て（GemScreenFX・GemManagerと同じく上限を超える分は数えない）</summary>
    private static Dictionary<int, (int start, int req)> CalcAlloc(ProgressData data)
    {
        var d = new Dictionary<int, (int, int)>();
        int used = 0;
        foreach (int idx in data.equippedGemIndices)
        {
            if (idx < 0 || idx >= data.gemInventory.Count) continue;
            var def = GemManager.Instance?.LoadGemDefinition(data.gemInventory[idx]);
            if (def == null || used + def.requiredSlots > data.slotLevel) continue;
            d[idx] = (used, def.requiredSlots);
            used += def.requiredSlots;
        }
        return d;
    }
}
