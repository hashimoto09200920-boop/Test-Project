using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ブロックアイテム（Gold・Life）を取った時の演出の管理役。Assets/Resources/ItemFX.prefab から1つだけ作られる
/// （プレハブが無ければ何もしない＝従来どおり。メニュー「Tools/弾の見た目/7 …」で作成）。
/// 今の「膨らんで縮みながらフェードアウト」と取得SE・数値ポップアップはそのまま残し、その上に重ねる。
/// ・取った瞬間（BlockItem.Collect）：① 閃光とリング ③ キラッと光る星 ④ 周りから光の粒が吸い込まれる
/// ・消える瞬間（効果が入る時）：② 光の粒が弾ける ⑦ Gold＝Gold表示へ飛ぶ（着くとGold表示が弾む） ⑧ Life＝ダンサーと床へ飛ぶ（着くと回復エフェクト）
/// ・⑤ 円で取った時（量が増える）は二重リング・粒の増加・虹色の輝き ⑥ 続けて取るほど大きく明るく
/// ・⑨ 数値の強調・⑩ ポップアップの使い回しは BlockItemPopupText / BlockItemManager 側
/// ・負荷対策：パーティクルは共有（Emitで出す）、画像は貸し出しで使い回し
/// </summary>
[DisallowMultipleComponent]
public class ItemFXManager : MonoBehaviour
{
    [Header("全体")]
    [Tooltip("OFFにすると全ての演出を止める（従来どおり）")]
    public bool fxEnabled = true;

    [Header("共有のパーティクル・画像（メニューで自動設定）")]
    public ParticleSystem sparkle;
    public Sprite glowSprite;
    public Sprite ringSprite;
    public Sprite starSprite;
    [Tooltip("加算の画像用マテリアル")]
    public Material additiveSpriteMaterial;
    [Tooltip("演出の並び順（アイテムより手前に）")]
    public string sortingLayerName = "Default";
    public int sortingOrder = 40;

    [Header("色")]
    public Color goldColor = new Color(1f, 0.85f, 0.2f, 1f);
    [Tooltip("Lifeの色（数値ポップアップのLifeの色に合わせた赤系）")]
    public Color lifeColor = new Color(1f, 0.35f, 0.4f, 1f);

    [Header("① 閃光とリング")]
    public bool flashRingEnabled = true;
    public float flashSize = 1.2f;
    public float flashDuration = 0.12f;
    [Tooltip("閃光の明るさ（0〜1）。目が痛くならないよう控えめに")]
    [Range(0f, 1f)] public float flashAlpha = 0.12f;
    public float ringSize = 1.6f;
    public float ringDuration = 0.28f;

    [Header("② 光の粒が弾ける")]
    public bool burstEnabled = true;
    public int burstCount = 16;
    public float burstSpeed = 3f;
    public float burstSize = 0.08f;

    [Header("③ キラッと光る星")]
    public bool starEnabled = true;
    public float starSize = 1.3f;
    public float starDuration = 0.35f;
    public float starSpin = 180f;

    [Header("④ 吸い込み")]
    public bool implodeEnabled = true;
    public int implodeCount = 12;
    [Tooltip("どれだけ離れた所から吸い込むか")]
    public float implodeRadius = 0.9f;
    [Tooltip("吸い込みにかける時間（アイテムが膨らむ時間に近い値）")]
    public float implodeSeconds = 0.15f;

    [Header("⑤ 円で取った時の特別版")]
    public bool circleSpecialEnabled = true;
    [Tooltip("円で取った時の大きさの倍率")]
    public float circleScale = 1.4f;
    [Tooltip("円で取った時の粒の量の倍率")]
    public float circleParticleMul = 1.6f;
    [Range(0f, 1f)] public float circleRainbowSaturation = 0.6f;

    [Header("⑥ 連続取得の盛り上がり")]
    public bool comboEnabled = true;
    public float comboWindow = 0.8f;
    public float comboStep = 0.12f;
    public int comboMaxSteps = 5;

    [Header("⑦ Gold：Gold表示へ飛んでいく")]
    public bool goldFlyEnabled = true;
    public int goldFlyCount = 5;
    public float goldFlySeconds = 0.55f;
    public float goldFlySize = 0.28f;
    [Tooltip("弧の高さ（飛ぶ距離に対する割合）")]
    public float goldFlyArc = 0.35f;
    [Tooltip("Gold表示が弾む大きさ")]
    public float goldHudPunchScale = 1.35f;

    [Header("⑧ Life：ダンサーと床へ飛んでいく")]
    public bool lifeFlyEnabled = true;
    [Tooltip("1か所あたりの粒の数")]
    public int lifeFlyCount = 4;
    public float lifeFlySeconds = 0.5f;
    public float lifeFlySize = 0.26f;
    public float lifeFlyArc = 0.3f;
    [Tooltip("着いた時に既存の回復エフェクト（SelfHealVFX）を出す")]
    public bool lifePlayHealVfx = true;

    [Header("⑪ Gold：コインが弾けて飛び散ってから、回りながらGold表示へ（⑦の光の粒の代わりに飛ぶ）")]
    [Tooltip("ON：取った所からコインの絵がばらまかれ、くるくる回りながらGold表示へ吸い込まれる。Coin Spriteが未設定なら従来の⑦のまま")]
    public bool coinBurstEnabled = true;
    [Tooltip("コインの絵（メニューでBlockItemManagerのGoldアイコンを自動設定）")]
    public Sprite coinSprite;
    public int coinCount = 7;
    [Tooltip("ばらまく勢い")]
    public float coinScatterSpeed = 3.2f;
    [Tooltip("ばらまかれている時間（この後Gold表示へ飛ぶ）")]
    public float coinScatterSeconds = 0.28f;
    [Tooltip("ばらまかれている間の重力（下向きはマイナス）")]
    public float coinGravity = -9f;
    public float coinSize = 0.34f;
    [Tooltip("コインが回る速さ（度/秒。横に潰れて見える回転）")]
    public float coinSpinSpeed = 900f;
    [Tooltip("ばらまいた後、Gold表示まで飛ぶ秒数")]
    public float coinFlySeconds = 0.5f;

    [Header("⑫ Life：ハートが分かれて飛び、着いた所に回復の輪（⑧の光の粒の代わりに飛ぶ）")]
    [Tooltip("ON：ハートの絵が小さく分かれてダンサーと床へ飛ぶ。Heart Spriteが未設定なら従来の⑧のまま")]
    public bool heartSplitEnabled = true;
    [Tooltip("ハートの絵（メニューでBlockItemManagerのLifeアイコンを自動設定）")]
    public Sprite heartSprite;
    [Tooltip("1か所あたりのハートの数")]
    public int heartCount = 3;
    public float heartScatterSpeed = 2.4f;
    public float heartScatterSeconds = 0.18f;
    public float heartSize = 0.3f;
    public float heartFlySeconds = 0.45f;
    [Tooltip("着いた所に広がる回復の輪の色")]
    public Color healRingColor = new Color(0.45f, 1f, 0.6f, 1f);
    public float healRingSize = 1.6f;
    public float healRingDuration = 0.4f;

    [Header("⑬ 円で取った時：円の中心から大きな虹色の輪＋画面が一瞬光る")]
    public bool circleBonusEnabled = true;
    [Tooltip("輪の大きさ（円の半径に対する倍率）")]
    public float circleBonusRingScale = 2.4f;
    public float circleBonusRingDuration = 0.5f;
    [Tooltip("画面が一瞬光る色（DamageFlashUIを使う）")]
    public Color circleBonusFlashColor = new Color(1f, 0.9f, 0.45f, 1f);
    [Tooltip("画面が光る強さ（0で光らない。被ダメージの赤い光より弱くする）")]
    [Range(0f, 1f)] public float circleBonusFlashAlpha = 0f;

    // ---------- 共有インスタンス ----------
    private static ItemFXManager s_instance;
    private static bool s_triedLoad;

    public static ItemFXManager Instance
    {
        get
        {
            if (s_instance != null) return s_instance;
            if (s_triedLoad) return null;
            s_triedLoad = true;
            var prefab = Resources.Load<ItemFXManager>("ItemFX");
            if (prefab == null) return null;
            s_instance = Instantiate(prefab);
            s_instance.name = "ItemFX";
            return s_instance;
        }
    }

    /// <summary>アイテムを取った瞬間（BlockItem.Collect）</summary>
    public static void NotifyCollectStart(BlockItem.ItemType type, Vector3 pos, bool isCircle)
    {
        var m = Instance;
        if (m == null || !m.fxEnabled) return;
        m.OnCollectStart(type, pos, isCircle);
    }

    /// <summary>アイテムが消えて効果が入る瞬間（BlockItem.CollectAnimationの最後）</summary>
    public static void NotifyCollected(BlockItem.ItemType type, Vector3 pos, bool isCircle)
    {
        var m = Instance;
        if (m == null || !m.fxEnabled) return;
        m.OnCollected(type, pos, isCircle);
    }

    /// <summary>⑬ 円でアイテムを1つ以上取った時（BlockItemManager.CollectItemsInCircleから、円1つにつき1回）</summary>
    public static void NotifyCircleBonus(Vector3 center, float radius)
    {
        var m = Instance;
        if (m == null || !m.fxEnabled || !m.circleBonusEnabled) return;
        float size = Mathf.Max(0.5f, radius) * 2f * m.circleBonusRingScale;
        m.AddAnim(m.ringSprite, center, size * 0.15f, size, m.circleBonusRingDuration, Color.white, 0f, 0f, true);
        m.AddAnim(m.ringSprite, center, size * 0.08f, size * 0.7f, m.circleBonusRingDuration * 0.85f, Color.white, 0f, 0f, true);
        Color gc = Color.Lerp(m.circleBonusFlashColor, Color.white, 0.3f); gc.a = m.flashAlpha;
        m.AddAnim(m.glowSprite, center, size * 0.2f, size * 0.6f, m.circleBonusRingDuration * 0.6f, gc, 0f, 0f, false);
        if (m.circleBonusFlashAlpha > 0f) DamageFlashUI.Flash(m.circleBonusFlashColor, m.circleBonusFlashAlpha);
    }

    // ---------- 内部 ----------
    private class Anim { public SpriteRenderer sr; public float t, dur, s0, s1, rot, spin; public Color c; public bool rainbow; }
    private class Flyer
    {
        public SpriteRenderer sr; public Vector3 a, ctrl; public float t, dur, size, delay; public Color c; public System.Func<Vector3> target; public System.Action onArrive;
        // ⑪⑫：最初にばらまかれてから飛ぶ（scatter秒）。flip＝コインのように横に潰れて回る。trail＝尾を引く粒
        public Vector3 start, vel; public float scatter, gravity, spin; public bool flip, trail = true;
    }

    private readonly List<Anim> anims = new List<Anim>();
    private readonly List<Flyer> flyers = new List<Flyer>();
    private readonly Stack<SpriteRenderer> freeSprites = new Stack<SpriteRenderer>();
    private int combo;
    private float lastCollectTime = -999f;
    private float currentScale = 1f;

    private void Awake()
    {
        if (s_instance == null) s_instance = this;
        if (sparkle != null)
        {
            var em = sparkle.emission; em.enabled = false;
            if (!sparkle.isPlaying) sparkle.Play(false);
        }
    }

    private void OnDestroy()
    {
        if (s_instance == this) { s_instance = null; s_triedLoad = false; }
    }

    private Color ColorOf(BlockItem.ItemType type) => type == BlockItem.ItemType.Gold ? goldColor : lifeColor;

    private Color Rainbow(float h) { Color c = Color.HSVToRGB(Mathf.Repeat(h, 1f), circleRainbowSaturation, 1f); c.a = 1f; return c; }

    private void OnCollectStart(BlockItem.ItemType type, Vector3 pos, bool isCircle)
    {
        // ⑥ 連続取得
        float now = Time.unscaledTime;
        combo = comboEnabled && now - lastCollectTime <= comboWindow ? combo + 1 : 1;
        lastCollectTime = now;
        bool special = isCircle && circleSpecialEnabled;
        currentScale = (comboEnabled ? 1f + Mathf.Min(combo - 1, comboMaxSteps) * comboStep : 1f) * (special ? circleScale : 1f);
        float s = currentScale;
        Color col = ColorOf(type);

        // ① 閃光とリング（⑤ 円取りは虹色の二重リング）
        if (flashRingEnabled)
        {
            Color fc = Color.Lerp(col, Color.white, 0.35f); fc.a = flashAlpha;
            AddAnim(glowSprite, pos, flashSize * s * 0.6f, flashSize * s, flashDuration, fc, 0f, 0f, false);
            AddAnim(ringSprite, pos, ringSize * s * 0.2f, ringSize * s, ringDuration, col, 0f, 0f, false);
            if (special) AddAnim(ringSprite, pos, ringSize * s * 0.1f, ringSize * s * 1.35f, ringDuration * 1.2f, Rainbow(Random.value), 0f, 0f, true);
        }
        // ③ キラッと光る星（2枚を少しずらして回転）
        if (starEnabled && starSprite != null)
        {
            AddAnim(starSprite, pos, starSize * s * 0.3f, starSize * s, starDuration, Color.Lerp(col, Color.white, 0.5f), Random.Range(0f, 90f), starSpin, special);
            AddAnim(starSprite, pos, starSize * s * 0.2f, starSize * s * 0.6f, starDuration * 0.8f, Color.white, 45f, -starSpin * 1.5f, false);
        }
        // ④ 吸い込み
        if (implodeEnabled && sparkle != null)
        {
            int n = Mathf.RoundToInt(implodeCount * (special ? circleParticleMul : 1f));
            float r = implodeRadius * s;
            float life = Mathf.Max(0.05f, implodeSeconds);
            for (int i = 0; i < n; i++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f);
                Vector3 d = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                float rr = r * Random.Range(0.7f, 1f);
                var ep = new ParticleSystem.EmitParams
                {
                    position = pos + d * rr,
                    velocity = -d * rr / life,
                    startLifetime = life,
                    startSize = burstSize * Random.Range(0.7f, 1.2f),
                    startColor = special ? Rainbow(i / (float)n) : Color.Lerp(col, Color.white, Random.Range(0f, 0.5f)),
                };
                sparkle.Emit(ep, 1);
            }
        }
    }

    private void OnCollected(BlockItem.ItemType type, Vector3 pos, bool isCircle)
    {
        bool special = isCircle && circleSpecialEnabled;
        float s = currentScale;
        Color col = ColorOf(type);

        // ② 光の粒が弾ける
        if (burstEnabled && sparkle != null)
        {
            int n = Mathf.RoundToInt(burstCount * (special ? circleParticleMul : 1f) * Mathf.Min(s, 2f));
            for (int i = 0; i < n; i++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f);
                var ep = new ParticleSystem.EmitParams
                {
                    position = pos,
                    velocity = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * burstSpeed * s * Random.Range(0.4f, 1.1f),
                    startSize = burstSize * Random.Range(0.7f, 1.4f),
                    startColor = special ? Rainbow(Random.value) : Color.Lerp(col, Color.white, Random.Range(0f, 0.5f)),
                };
                sparkle.Emit(ep, 1);
            }
        }

        if (type == BlockItem.ItemType.Gold)
        {
            // ⑦ Gold表示へ飛んでいく（最初の1粒が着いた時にGold表示を弾ませる）
            var hud = GoldHUD.SessionInstance;
            if (goldFlyEnabled && hud != null && coinBurstEnabled && coinSprite != null)
            {
                // ⑪ コインがばらまかれてから、回りながらGold表示へ（最初の1枚が着いた時にGold表示を弾ませる）
                bool punched = false;
                int n = Mathf.RoundToInt(coinCount * (special ? circleParticleMul : 1f));
                for (int i = 0; i < n; i++)
                {
                    float ang = Random.Range(20f, 160f) * Mathf.Deg2Rad; // 上向きに散らす
                    Vector3 v = new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), 0f) * coinScatterSpeed * Random.Range(0.6f, 1.1f) * Mathf.Min(s, 1.6f);
                    AddScatterFlyer(coinSprite, false, pos, v, coinScatterSeconds, coinGravity, () => hud != null ? hud.IconWorldPosition() : pos,
                        coinFlySeconds, coinSize * Mathf.Min(s, 1.4f), goldFlyArc, i * 0.02f, Color.white, coinSpinSpeed * Random.Range(0.8f, 1.2f), true,
                        () => { if (!punched && hud != null) { punched = true; hud.Punch(goldHudPunchScale); } });
                }
                // ③ Gold表示の数字は、コインが着き始める頃から増やし始める
                hud.DelayCountUp(coinScatterSeconds + coinFlySeconds * 0.8f);
            }
            else if (goldFlyEnabled && hud != null)
            {
                bool punched = false;
                for (int i = 0; i < goldFlyCount; i++)
                {
                    AddFlyer(pos, () => hud != null ? hud.IconWorldPosition() : pos, goldFlySeconds, goldFlySize * Mathf.Min(s, 1.6f), goldFlyArc, i * 0.04f,
                        special ? Rainbow(i / (float)Mathf.Max(1, goldFlyCount)) : col,
                        () => { if (!punched && hud != null) { punched = true; hud.Punch(goldHudPunchScale); } });
                }
            }
        }
        else if (lifeFlyEnabled)
        {
            // ⑧ ダンサーと床へ飛んでいく（着いたら既存の回復エフェクト）
            var playerGo = GameObject.FindGameObjectWithTag("Player");
            var dancer = playerGo != null ? playerGo.GetComponent<PixelDancerController>() : null;
            var floor = FindFirstObjectByType<FloorHealth>();
            if (heartSplitEnabled && heartSprite != null)
            {
                // ⑫ ハートが分かれて飛び、着いた所で回復の輪＋既存の回復エフェクト
                if (dancer != null && !PixelDancerController.IsPlayerDeadGlobal)
                {
                    Transform dt = dancer.transform;
                    bool played = false;
                    for (int i = 0; i < heartCount; i++)
                        AddHeart(pos, () => dt != null ? dt.position : pos, i, special, lifeFlyArc,
                            () => { if (!played) { played = true; HealRing(dt != null ? dt.position : pos); if (lifePlayHealVfx && dancer != null) dancer.PlayHealEffect(); } });
                }
                if (floor != null && !FloorHealth.IsBrokenGlobal)
                {
                    Transform ft = floor.transform;
                    bool played = false;
                    for (int i = 0; i < heartCount; i++)
                        AddHeart(pos, () => ft != null ? ft.position : pos, i, special, -lifeFlyArc,
                            () => { if (!played) { played = true; HealRing(ft != null ? ft.position : pos); if (lifePlayHealVfx && floor != null) floor.PlayHealEffect(); } });
                }
                return;
            }
            if (dancer != null && !PixelDancerController.IsPlayerDeadGlobal)
            {
                Transform dt = dancer.transform;
                bool played = false;
                for (int i = 0; i < lifeFlyCount; i++)
                    AddFlyer(pos, () => dt != null ? dt.position : pos, lifeFlySeconds, lifeFlySize, lifeFlyArc, i * 0.05f, special ? Rainbow(i * 0.2f) : col,
                        () => { if (!played && lifePlayHealVfx && dancer != null) { played = true; dancer.PlayHealEffect(); } });
            }
            if (floor != null && !FloorHealth.IsBrokenGlobal)
            {
                Transform ft = floor.transform;
                bool played = false;
                for (int i = 0; i < lifeFlyCount; i++)
                    AddFlyer(pos, () => ft != null ? ft.position : pos, lifeFlySeconds * 1.1f, lifeFlySize, -lifeFlyArc, 0.03f + i * 0.05f, special ? Rainbow(0.5f + i * 0.2f) : col,
                        () => { if (!played && lifePlayHealVfx && floor != null) { played = true; floor.PlayHealEffect(); } });
            }
        }
    }

    // ---------- 毎フレーム ----------
    private void LateUpdate()
    {
        float dt = Time.deltaTime;
        for (int i = anims.Count - 1; i >= 0; i--)
        {
            var a = anims[i];
            a.t += dt;
            float k = a.t / Mathf.Max(0.01f, a.dur);
            if (k >= 1f) { ReleaseSprite(a.sr); anims.RemoveAt(i); continue; }
            float eased = 1f - (1f - k) * (1f - k);
            float sz = Mathf.Lerp(a.s0, a.s1, eased);
            a.rot += a.spin * dt;
            a.sr.transform.rotation = Quaternion.Euler(0f, 0f, a.rot);
            SetSize(a.sr, sz);
            Color c = a.rainbow ? Rainbow(a.t * 2f) : a.c;
            c.a = (a.rainbow ? 1f : a.c.a) * (1f - k);
            a.sr.color = c;
        }

        for (int i = flyers.Count - 1; i >= 0; i--)
        {
            var f = flyers[i];
            f.t += dt;
            float tt = f.t - f.delay;
            if (tt < 0f) { f.sr.enabled = false; continue; }
            f.sr.enabled = true;
            // ⑪⑫：ばらまかれている間（重力つきで散る）
            if (f.scatter > 0f && tt < f.scatter)
            {
                Vector3 sp = f.start + f.vel * tt + 0.5f * f.gravity * tt * tt * Vector3.up;
                f.sr.transform.position = sp;
                SetSize(f.sr, f.size);
                if (f.flip) FlipScale(f.sr, f.t * f.spin);
                Color sc = f.c; sc.a = 1f; f.sr.color = sc;
                continue;
            }
            float flyT = f.scatter > 0f ? tt - f.scatter : tt;
            float k = Mathf.Clamp01(flyT / Mathf.Max(0.05f, f.dur));
            Vector3 b = f.target != null ? f.target() : f.a;
            float e = k * k * (3f - 2f * k); // ゆっくり出て速く着く
            Vector3 p = (1f - e) * (1f - e) * f.a + 2f * (1f - e) * e * f.ctrl + e * e * b;
            f.sr.transform.position = p;
            SetSize(f.sr, f.size * Mathf.Lerp(1f, 0.6f, k));
            if (f.flip) FlipScale(f.sr, f.t * f.spin);
            Color c = f.c; c.a = Mathf.Lerp(1f, 0.85f, k);
            f.sr.color = c;
            // 尾を引く粒
            if (f.trail && sparkle != null && Random.value < 0.5f)
            {
                var ep = new ParticleSystem.EmitParams { position = p, startSize = f.size * 0.25f, startColor = c, velocity = Vector3.zero, startLifetime = 0.25f };
                sparkle.Emit(ep, 1);
            }
            if (sparkle != null && !f.trail && Random.value < 0.35f)
            {
                // コイン・ハートは、絵の色ではなくアイテムの色の粒を引く
                var ep2 = new ParticleSystem.EmitParams { position = p, startSize = f.size * 0.2f, startColor = f.flip ? goldColor : lifeColor, velocity = Vector3.zero, startLifetime = 0.2f };
                sparkle.Emit(ep2, 1);
            }
            if (k >= 1f)
            {
                f.onArrive?.Invoke();
                ReleaseSprite(f.sr);
                flyers.RemoveAt(i);
            }
        }
    }

    // ---------- 共通 ----------
    private void AddAnim(Sprite sprite, Vector3 pos, float s0, float s1, float dur, Color c, float rot, float spin, bool rainbow)
    {
        if (sprite == null) return;
        var sr = RentSprite(sprite);
        sr.transform.position = pos;
        anims.Add(new Anim { sr = sr, dur = dur, s0 = s0, s1 = s1, c = c, rot = rot, spin = spin, rainbow = rainbow });
    }

    private void AddFlyer(Vector3 from, System.Func<Vector3> target, float dur, float size, float arc, float delay, Color c, System.Action onArrive)
    {
        if (glowSprite == null) return;
        Vector3 to = target();
        Vector3 d = to - from;
        Vector3 n = new Vector3(-d.y, d.x, 0f).normalized;
        float side = Random.Range(0.6f, 1.4f) * (Random.value < 0.5f ? 1f : -1f) * Mathf.Sign(arc == 0f ? 1f : arc);
        Vector3 ctrl = from + d * 0.35f + n * d.magnitude * Mathf.Abs(arc) * side + (Vector3)(Random.insideUnitCircle * 0.3f);
        var sr = RentSprite(glowSprite);
        sr.enabled = false;
        flyers.Add(new Flyer { sr = sr, a = from, ctrl = ctrl, dur = dur * Random.Range(0.9f, 1.1f), size = size, delay = delay, c = c, target = target, onArrive = onArrive });
    }

    /// <summary>⑪⑫ 最初にばらまかれ、その後ターゲットへ飛ぶ絵（コイン・ハート）</summary>
    private void AddScatterFlyer(Sprite sprite, bool additive, Vector3 from, Vector3 vel, float scatter, float gravity, System.Func<Vector3> target,
        float dur, float size, float arc, float delay, Color c, float spin, bool flip, System.Action onArrive)
    {
        if (sprite == null) return;
        Vector3 scatterEnd = from + vel * scatter + 0.5f * gravity * scatter * scatter * Vector3.up;
        Vector3 to = target();
        Vector3 d = to - scatterEnd;
        Vector3 n = new Vector3(-d.y, d.x, 0f).normalized;
        float side = Random.Range(0.5f, 1.2f) * (Random.value < 0.5f ? 1f : -1f) * Mathf.Sign(arc == 0f ? 1f : arc);
        Vector3 ctrl = scatterEnd + d * 0.35f + n * d.magnitude * Mathf.Abs(arc) * side;
        var sr = RentSprite(sprite, additive);
        sr.enabled = false;
        flyers.Add(new Flyer
        {
            sr = sr, a = scatterEnd, ctrl = ctrl, dur = dur * Random.Range(0.9f, 1.1f), size = size, delay = delay, c = c, target = target, onArrive = onArrive,
            start = from, vel = vel, scatter = scatter, gravity = gravity, spin = spin, flip = flip, trail = false,
        });
    }

    private void AddHeart(Vector3 from, System.Func<Vector3> target, int i, bool special, float arc, System.Action onArrive)
    {
        float ang = (90f + Random.Range(-70f, 70f)) * Mathf.Deg2Rad;
        Vector3 v = new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), 0f) * heartScatterSpeed * Random.Range(0.7f, 1.1f);
        Color c = special ? Color.Lerp(Color.white, Rainbow(i * 0.25f), 0.35f) : Color.white;
        AddScatterFlyer(heartSprite, false, from, v, heartScatterSeconds, coinGravity * 0.5f, target, heartFlySeconds, heartSize, arc, i * 0.04f, c, 0f, false, onArrive);
    }

    private void HealRing(Vector3 pos)
    {
        AddAnim(ringSprite, pos, healRingSize * 0.2f, healRingSize, healRingDuration, healRingColor, 0f, 0f, false);
        AddAnim(glowSprite, pos, healRingSize * 0.3f, healRingSize * 0.7f, healRingDuration * 0.7f, Color.Lerp(healRingColor, Color.white, 0.4f), 0f, 0f, false);
    }

    /// <summary>コインが回って見えるよう、横の大きさだけ潰す（表と裏が入れ替わる回転の見え方）</summary>
    private static void FlipScale(SpriteRenderer sr, float deg)
    {
        Vector3 ls = sr.transform.localScale;
        ls.x *= Mathf.Max(0.12f, Mathf.Abs(Mathf.Cos(deg * Mathf.Deg2Rad)));
        sr.transform.localScale = ls;
    }

    private static void SetSize(SpriteRenderer sr, float size)
    {
        float s = sr.sprite != null ? Mathf.Max(0.01f, sr.sprite.bounds.size.x) : 1f;
        sr.transform.localScale = new Vector3(size / s, size / s, 1f);
    }

    private Material defaultSpriteMaterial;

    private SpriteRenderer RentSprite(Sprite sprite) => RentSprite(sprite, true);

    /// <param name="additive">true＝加算（光）。false＝普通の絵（コイン・ハート。加算だと白っぽく抜けるため）</param>
    private SpriteRenderer RentSprite(Sprite sprite, bool additive)
    {
        SpriteRenderer sr = null;
        while (freeSprites.Count > 0 && sr == null) sr = freeSprites.Pop();
        if (sr == null)
        {
            var go = new GameObject("ItemFX_Sprite");
            go.transform.SetParent(transform, false);
            sr = go.AddComponent<SpriteRenderer>();
            if (defaultSpriteMaterial == null) defaultSpriteMaterial = sr.sharedMaterial; // 新しく作った時の標準マテリアルを覚えておく
        }
        if (additive) { if (additiveSpriteMaterial != null) sr.sharedMaterial = additiveSpriteMaterial; }
        else if (defaultSpriteMaterial != null) sr.sharedMaterial = defaultSpriteMaterial;
        sr.sprite = sprite;
        sr.sortingLayerName = sortingLayerName;
        sr.sortingOrder = sortingOrder;
        sr.color = Color.white;
        sr.transform.rotation = Quaternion.identity;
        sr.gameObject.SetActive(true);
        sr.enabled = true;
        return sr;
    }

    private void ReleaseSprite(SpriteRenderer sr)
    {
        if (sr == null) return;
        sr.gameObject.SetActive(false);
        freeSprites.Push(sr);
    }
}
