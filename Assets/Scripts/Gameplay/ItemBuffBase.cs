using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 一定時間効くアイテム（ゴールドシューズ・アクセラレーター）の共通部分（2026/11/10）。
/// ・効果時間（ミッション報酬で延びる）。効果中にもう1つ取ったら残りを最初の長さに戻す（足し算はしない）
/// ・残り時間は実時間で減る（スローモーション中も普通の速さ）。3択・VS演出・中断メニューなど Time.timeScale が0の間は止まる
/// ・Stageが変わっても残り時間のぶん続く
/// ・ダンサーの上にアイコンと残り秒数（横だけダンサーを追い、縦は固定）。残り数秒で点滅。
///   効果中のアイテムが2つ以上ある時は、アイコンを横に並べて全体をダンサーの真上にそろえる（並び順は Icon Order の小さい順）
/// ・取った瞬間にダンサーから粒子がはじけ飛ぶ
/// ★ダンサーの左右の移動は PixelDancerController.LateUpdate で決まるので、使う側のクラスに [DefaultExecutionOrder(1000)] を付けて後に動かす
/// </summary>
public abstract class ItemBuffBase : MonoBehaviour
{
    private static readonly List<ItemBuffBase> s_all = new List<ItemBuffBase>();

    [Header("効果")]
    [Tooltip("線で取った時の効果時間（基礎値・秒）。ミッション報酬で +5秒／+10秒（MissionCatalog）。2026/11/12 30→20（シーンに前の値が残らないよう項目名も変えた）")]
    [Min(1f)] [SerializeField] protected float baseDuration20 = 20f;
    [Tooltip("円で囲んで取った時の倍率（スキルC4なし）。20秒×1.5＝30秒")]
    [Min(1f)] [SerializeField] protected float circleDurationMul = 1.5f;
    [Tooltip("円で囲んで取った時の倍率（スキルC4レベル1）。20秒×2＝40秒")]
    [Min(1f)] [SerializeField] protected float circleDurationMulC4Lv1 = 2f;
    [Tooltip("円で囲んで取った時の倍率（スキルC4レベル2）。20秒×3＝60秒")]
    [Min(1f)] [SerializeField] protected float circleDurationMulC4Lv2 = 3f;

    [Header("ダンサーの上のアイコンと残り秒数（子の Icon・Seconds）")]
    [SerializeField] protected SpriteRenderer iconRenderer;
    [SerializeField] protected TextMesh secondsText;
    [Tooltip("アイコンの横の位置：ダンサーの頭（絵が描かれている部分の中央）からの横のずれ（ワールド）")]
    [SerializeField] protected float iconOffsetX = 0f;
    [Tooltip("アイコンの縦の位置：ダンサーが立っている高さ（ダンサーの基準位置）からの高さ（ワールド）。縦はダンサーの動きを追わず固定")]
    [SerializeField] protected float iconHeight = 1f;
    [Tooltip("ダンサーの頭を追う速さ（大きいほどぴったり追う。0でなめらかにせず毎フレームぴったり）")]
    [Min(0f)] [SerializeField] protected float followSharpness = 14f;
    [Tooltip("アイコンの高さ（ワールド）。敵のデバフアイコン（共通配置）は0.36")]
    [Min(0.01f)] [SerializeField] protected float iconSize = 0.36f;
    [Tooltip("残り秒数の文字の高さ（ワールド）。敵のデバフは0.25")]
    [Min(0.01f)] [SerializeField] protected float numberHeight = 0.25f;
    [Tooltip("アイコンの上端から残り秒数の文字の中心までの距離（ワールド）。敵のデバフは0.12")]
    [SerializeField] protected float numberOffsetY = 0.12f;
    [SerializeField] protected Color numberColor = Color.white;
    [Tooltip("アイコンの描画順（文字はこれ+1）")]
    [SerializeField] protected int sortingOrder = 40;
    [Tooltip("ON：Play前のシーンで、ダンサーの上にアイコンと秒数を表示して位置を確認できる（Play中は効果中だけ表示）")]
    [SerializeField] protected bool previewInEditor = true;
    [Tooltip("効果中のアイテムが2つ以上ある時の並び順（小さい方が左）")]
    [SerializeField] protected int iconOrder = 0;
    [Tooltip("アイコンを横に並べる時の、アイコンとアイコンのすき間（ワールド）")]
    [Min(0f)] [SerializeField] protected float iconSpacing = 0.06f;

    [Header("残り数秒の点滅")]
    [Min(0f)] [SerializeField] protected float blinkLastSeconds = 5f;
    [Min(0.1f)] [SerializeField] protected float blinkPerSecond = 3f;
    [Range(0f, 1f)] [SerializeField] protected float blinkMinAlpha = 0.2f;

    [Header("効果が切れた時")]
    [Tooltip("効果が切れた時の音。未設定なら鳴らさない")]
    [SerializeField] protected AudioClip endSE;
    [Range(0f, 1f)] [SerializeField] protected float endSEVolume = 1f;

    [Header("粒子（取った瞬間など）")]
    [Tooltip("粒子の画像（柔らかい丸）")]
    [SerializeField] protected Sprite footGlowSprite;
    [Tooltip("粒子の色")]
    [SerializeField] protected Color[] footGlowColors = { new Color(1f, 0.82f, 0.2f, 1f), new Color(0.3f, 1f, 1f, 1f), new Color(1f, 0.35f, 1f, 1f) };

    [Header("取った瞬間：ダンサーから粒子がはじけ飛ぶ")]
    [Min(0)] [SerializeField] protected int startBurstCount = 30;
    [Tooltip("飛び出す速さ（ワールド/秒）の最小・最大")]
    [SerializeField] protected Vector2 startBurstSpeed = new Vector2(5f, 10f);
    [Tooltip("勢いが落ちる強さ（大きいほど早く止まる＝一瞬だけ勢いよく）")]
    [Min(0f)] [SerializeField] protected float startBurstDamping = 6f;
    [Min(0.05f)] [SerializeField] protected float startBurstLife = 0.55f;
    [Min(0.01f)] [SerializeField] protected float startBurstSize = 0.22f;

    protected float remaining;
    protected float currentDuration;
    private PixelDancerController dancer;
    private AudioSource audioSource;
    private int lastShownSecs = int.MinValue;
    private bool smoothHas;
    private Vector3 smoothAnchor;
    private float standY;
    private bool standYHas;

    public bool IsActive => remaining > 0f;
    public float Remaining => remaining;
    /// <summary>線で取った時の効果時間（秒）。基礎値にミッション報酬（+5秒／+10秒）を足した値</summary>
    public float Duration => GetDuration(baseDuration20);

    /// <summary>
    /// 円で囲んで取った時の倍率。スキルC4（円の時間延長）の取得数は、ゴールドの円の倍率
    /// （SkillManager.GetBlockItemCircleMultiplier：C4なし2倍・レベル1で3倍・レベル2で4倍）から読む
    /// </summary>
    private float CircleMul()
    {
        int mul = Game.Skills.SkillManager.Instance != null ? Game.Skills.SkillManager.Instance.GetBlockItemCircleMultiplier() : 2;
        if (mul >= 4) return circleDurationMulC4Lv2;
        if (mul == 3) return circleDurationMulC4Lv1;
        return circleDurationMul;
    }

    /// <summary>ログの見出し（例：GoldenShoes）</summary>
    protected abstract string LogTag { get; }
    /// <summary>ミッション報酬を反映した効果時間</summary>
    protected abstract float GetDuration(float baseSeconds);
    /// <summary>このクラスの Instance を登録・解除する</summary>
    protected abstract void SetInstance(bool register);
    protected virtual void OnActivated(bool wasActive) { }
    protected virtual void OnEnded() { }
    /// <summary>効果中、毎フレーム（LateUpdate・アイコンを置いた後）</summary>
    protected virtual void OnActiveLateUpdate(float dt) { }
    /// <summary>効果が切れた瞬間に、ダンサーからも粒子を散らすか</summary>
    protected virtual bool DancerEndBurst => false;

    protected virtual void OnEnable()
    {
        if (!s_all.Contains(this)) s_all.Add(this);
        if (Application.isPlaying) SetInstance(true);
    }

    protected virtual void OnDisable()
    {
        s_all.Remove(this);
        SetInstance(false);
    }

    protected virtual void Awake()
    {
        if (!Application.isPlaying) return;
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
        if (secondsText != null) secondsText.characterSize *= GameNumberFont.Apply(secondsText);
        SetIconVisible(false);
    }

    /// <summary>
    /// アイテムを取った（BlockItemManager.ApplyEffect）。効果中なら残りを最初の長さに戻す。
    /// 効果時間＝（基礎値＋ミッション報酬）×（円で取った時の倍率。線で取った時は1）（2026/11/12）
    /// </summary>
    public void Activate(bool isCircle = false)
    {
        bool wasActive = IsActive;
        currentDuration = Duration * (isCircle ? CircleMul() : 1f);
        remaining = currentDuration;
        lastShownSecs = int.MinValue;
        smoothHas = false;
        PlayDancerBurst(1f); // 取った瞬間：ダンサーから粒子がはじけ飛ぶ
        OnActivated(wasActive);
        Debug.Log($"[{LogTag}] 効果開始：{currentDuration}秒{(isCircle ? $"（円で取った：×{CircleMul()}）" : "")}{(wasActive ? "・残り時間を戻した" : "")}");
    }

    private void End()
    {
        remaining = 0f;
        SetIconVisible(false);
        if (endSE != null && audioSource != null)
        {
            float vol = SoundSettingsManager.Instance != null ? SoundSettingsManager.Instance.SEVolume : 1f;
            audioSource.PlayOneShot(endSE, vol * endSEVolume);
        }
        if (DancerEndBurst) PlayDancerBurst(0.6f);
        OnEnded();
        Debug.Log($"[{LogTag}] 効果終了");
    }

    /// <summary>止まっていない時の実時間（スローモーション中も普通の速さ。timeScaleが0の間は0）</summary>
    protected static float RunDt => Time.timeScale > 0f ? Time.unscaledDeltaTime : 0f;

    protected virtual void Update()
    {
        if (!Application.isPlaying) return;
        if (remaining <= 0f) return;
        remaining -= RunDt;
        if (remaining <= 0f) End();
    }

    protected virtual void LateUpdate()
    {
        if (!Application.isPlaying)
        {
#if UNITY_EDITOR
            UpdateEditorPreview();
#endif
            return;
        }
        float dt = RunDt;
        // 立っている高さは、ゲーム開始直後（どのダンスも高さ-2.9付近から始まる）に1回だけ覚える
        if (!standYHas && Dancer != null) { standY = Dancer.transform.position.y; standYHas = true; }
        if (IsActive)
        {
            UpdateIcon();
            OnActiveLateUpdate(dt);
        }
        UpdateGlows(dt);
    }

    protected PixelDancerController Dancer
    {
        get
        {
            // ★見えている本体（Playerタグ）を優先。シーンには非表示のPixelDancerがもう1つある
            if (dancer == null || !dancer.isActiveAndEnabled)
            {
                var p = GameObject.FindGameObjectWithTag("Player");
                dancer = p != null ? p.GetComponent<PixelDancerController>() : null;
                if (dancer == null) dancer = FindFirstObjectByType<PixelDancerController>();
            }
            return dancer;
        }
    }

    protected static SpriteRenderer BodyOf(PixelDancerController d) => d.BodyRenderer != null ? d.BodyRenderer : d.GetComponent<SpriteRenderer>();

    private void SetIconVisible(bool v)
    {
        if (iconRenderer != null && iconRenderer.gameObject.activeSelf != v) iconRenderer.gameObject.SetActive(v);
        if (secondsText != null && secondsText.gameObject.activeSelf != v) secondsText.gameObject.SetActive(v);
    }

    /// <summary>アイコンを出しているか（並べ方の計算用）。Play中は効果中、Play前はプレビューON</summary>
    private bool ShowsIcon => Application.isPlaying ? IsActive : (previewInEditor && isActiveAndEnabled);

    private static readonly List<ItemBuffBase> s_shown = new List<ItemBuffBase>();

    /// <summary>
    /// 横に並べる時の、自分の横のずれ。アイコンを出している物を Icon Order の順に並べ、全体の中央を0にする。
    /// アイコンごとの大きさ（Icon Size）の違いを考えて並べる。すき間は、出ている物の Icon Spacing の一番大きい値
    /// </summary>
    private float SlotOffsetX()
    {
        s_shown.Clear();
        foreach (var b in s_all) if (b != null && b.ShowsIcon) s_shown.Add(b);
        if (s_shown.Count <= 1 || !s_shown.Contains(this)) return 0f;
        s_shown.Sort((a, b) => a.iconOrder != b.iconOrder ? a.iconOrder.CompareTo(b.iconOrder) : a.GetInstanceID().CompareTo(b.GetInstanceID()));
        float gap = 0f, total = 0f;
        foreach (var b in s_shown) { gap = Mathf.Max(gap, b.iconSpacing); total += b.iconSize; }
        total += gap * (s_shown.Count - 1);
        float left = -total * 0.5f;
        foreach (var b in s_shown)
        {
            if (b == this) return left + b.iconSize * 0.5f;
            left += b.iconSize + gap;
        }
        return 0f;
    }

    private void UpdateIcon()
    {
        var d = Dancer;
        bool show = d != null && !PixelDancerController.IsPlayerDeadGlobal;
        SetIconVisible(show);
        if (!show) { smoothHas = false; return; }

        if (!standYHas) { standY = d.transform.position.y; standYHas = true; }
        Vector3 anchor = FollowPoint(d);
        anchor.y = standY; // 縦は追わない
        if (!smoothHas || followSharpness <= 0f) { smoothAnchor = anchor; smoothHas = true; }
        else smoothAnchor = Vector3.Lerp(smoothAnchor, anchor, 1f - Mathf.Exp(-followSharpness * Time.unscaledDeltaTime));
        PlaceIcon(smoothAnchor);

        int secs = Mathf.CeilToInt(remaining);
        if (secondsText != null && secs != lastShownSecs)
        {
            lastShownSecs = secs;
            secondsText.text = secs.ToString(); // 敵のデバフと同じく「s」は付けない
        }

        // 残り数秒で点滅（止まっている間は点滅も止める）
        float a = 1f;
        if (remaining <= blinkLastSeconds && Time.timeScale > 0f)
            a = Mathf.Lerp(blinkMinAlpha, 1f, 0.5f + 0.5f * Mathf.Cos(Time.unscaledTime * blinkPerSecond * Mathf.PI * 2f));
        SetAlpha(a);
    }

    /// <summary>アイコンの横の位置の基準＝ダンサーの「絵が描かれている部分」の中央。絵が無ければダンサーの位置</summary>
    private static Vector3 FollowPoint(PixelDancerController d)
    {
        if (!VisibleBounds(BodyOf(d), out Vector2 min, out Vector2 max)) return d.transform.position;
        return new Vector3((min.x + max.x) * 0.5f, max.y, d.transform.position.z);
    }

    /// <summary>
    /// 絵が描かれている部分（透明でない所）の範囲（ワールド）。
    /// ★ダンサーのコマ画像は204×408の枠の中で絵が動くので、枠（bounds）ではなく、透明部分を除いた形（Sprite.vertices。Mesh Type＝Tight）で測る
    /// </summary>
    protected static bool VisibleBounds(SpriteRenderer body, out Vector2 min, out Vector2 max)
    {
        min = max = Vector2.zero;
        if (body == null || body.sprite == null) return false;
        var verts = body.sprite.vertices;
        if (verts == null || verts.Length == 0) return false;
        var t = body.transform;
        float sx = body.flipX ? -1f : 1f, sy = body.flipY ? -1f : 1f;
        for (int i = 0; i < verts.Length; i++)
        {
            Vector2 w = t.TransformPoint(new Vector3(verts[i].x * sx, verts[i].y * sy, 0f));
            if (i == 0) { min = max = w; continue; }
            min = Vector2.Min(min, w);
            max = Vector2.Max(max, w);
        }
        return true;
    }

    /// <summary>並んで出ているアイコンの中で一番大きい Icon Size（自分だけなら自分の大きさ）</summary>
    private float RowIconSize()
    {
        float m = iconSize;
        foreach (var b in s_all) if (b != null && b.ShowsIcon) m = Mathf.Max(m, b.iconSize);
        return m;
    }

    private void PlaceIcon(Vector3 dancerPos)
    {
        // dancerPos：x＝頭の横の位置、y＝ダンサーが立っている高さ
        Vector3 iconPos = new Vector3(dancerPos.x + iconOffsetX + SlotOffsetX(), dancerPos.y + iconHeight, dancerPos.z - 0.05f);
        if (iconRenderer != null)
        {
            var t = iconRenderer.transform;
            t.SetPositionAndRotation(iconPos, Quaternion.identity);
            float h = iconRenderer.sprite != null && iconRenderer.sprite.bounds.size.y > 0.0001f ? iconRenderer.sprite.bounds.size.y : 1f;
            t.localScale = Vector3.one * (iconSize / h);
            iconRenderer.sortingOrder = sortingOrder;
        }
        if (secondsText != null)
        {
            EnemyHealthBarLayoutSettings.ApplyUnifiedText(secondsText, numberHeight);
            secondsText.anchor = TextAnchor.MiddleCenter;
            secondsText.alignment = TextAlignment.Center;
            var t = secondsText.transform;
            // ★数字の高さは、並んでいるアイコンの中で一番大きいアイコンの上端にそろえる（アイコンの大きさが違っても数字の高さがずれないように）
            t.SetPositionAndRotation(new Vector3(iconPos.x, iconPos.y + RowIconSize() * 0.5f + numberOffsetY, iconPos.z - 0.01f), Quaternion.identity);
            t.localScale = Vector3.one;
            var mr = secondsText.GetComponent<MeshRenderer>();
            if (mr != null) mr.sortingOrder = sortingOrder + 1;
        }
    }

    private void SetAlpha(float a)
    {
        if (iconRenderer != null) { var c = iconRenderer.color; c.a = a; iconRenderer.color = c; }
        if (secondsText != null) { var c = numberColor; c.a *= a; secondsText.color = c; }
    }

    // ---------------- 光の粒（足元の光・はじける粒子で共通・使い回し） ----------------

    private class Glow { public SpriteRenderer sr, core; public float age, life, hold, size, damping; public Color color; public Vector2 vel; }
    private readonly List<Glow> glows = new List<Glow>();
    private Transform glowRoot;

    protected Color GlowColor(int i) => footGlowColors != null && footGlowColors.Length > 0 ? footGlowColors[((i % footGlowColors.Length) + footGlowColors.Length) % footGlowColors.Length] : Color.white;

    /// <summary>光の粒を1つ出す</summary>
    protected void SpawnGlow(Vector3 pos, int layerId, int order, Color color, float size, float life, float hold, Vector2 vel, float damping, float coreRatio)
    {
        if (footGlowSprite == null) return;
        Glow f = null;
        foreach (var x in glows) if (x.sr != null && !x.sr.gameObject.activeSelf) { f = x; break; }
        if (f == null)
        {
            if (glowRoot == null) glowRoot = new GameObject(LogTag + "Glow").transform;
            var go = new GameObject("Glow");
            go.transform.SetParent(glowRoot, false);
            var coreGo = new GameObject("Core");
            coreGo.transform.SetParent(go.transform, false);
            f = new Glow { sr = go.AddComponent<SpriteRenderer>(), core = coreGo.AddComponent<SpriteRenderer>() };
            glows.Add(f);
        }
        f.sr.gameObject.SetActive(true);
        f.sr.sprite = footGlowSprite;
        f.sr.sortingLayerID = layerId;
        f.sr.sortingOrder = order - 1;
        f.core.sprite = footGlowSprite;
        f.core.sortingLayerID = layerId;
        f.core.sortingOrder = order;
        f.core.enabled = coreRatio > 0f;
        f.core.transform.localScale = Vector3.one * coreRatio;
        f.color = color;
        f.size = size;
        f.age = 0f;
        f.life = life;
        f.hold = hold;
        f.vel = vel;
        f.damping = damping;
        f.sr.transform.position = pos;
        ApplyGlow(f, 0f);
    }

    /// <summary>ダンサーの絵の中心から、全方向へ勢いよく粒子をはじけ飛ばす（countMul＝数の倍率）</summary>
    protected void PlayDancerBurst(float countMul)
    {
        int count = Mathf.RoundToInt(startBurstCount * countMul);
        if (footGlowSprite == null || count <= 0) return;
        var d = Dancer;
        if (d == null || PixelDancerController.IsPlayerDeadGlobal) return;
        var body = BodyOf(d);
        if (body == null || !VisibleBounds(body, out Vector2 bmin, out Vector2 bmax)) return;
        Vector3 center = new Vector3((bmin.x + bmax.x) * 0.5f, (bmin.y + bmax.y) * 0.5f, d.transform.position.z - 0.02f);
        for (int i = 0; i < count; i++)
        {
            float ang = (i + Random.Range(0f, 0.8f)) / count * Mathf.PI * 2f; // 全方向へまんべんなく
            Vector2 dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
            // ダンサーより手前に出す
            SpawnGlow(center, body.sortingLayerID, body.sortingOrder + 2, GlowColor(Random.Range(0, 1000)), startBurstSize * Random.Range(0.7f, 1.3f),
                startBurstLife * Random.Range(0.8f, 1.2f), 0.2f, dir * Random.Range(startBurstSpeed.x, startBurstSpeed.y), startBurstDamping, 0.45f);
        }
    }

    private void ApplyGlow(Glow f, float t)
    {
        // はっきり見えたまま hold まで、その後に薄くなりながら少し小さくなる
        float k = t <= f.hold ? 0f : (t - f.hold) / Mathf.Max(0.0001f, 1f - f.hold);
        var c = f.color; c.a *= 1f - k;
        f.sr.color = c;
        if (f.core != null) f.core.color = new Color(1f, 1f, 1f, c.a);
        float w = f.sr.sprite != null && f.sr.sprite.bounds.size.x > 0.0001f ? f.sr.sprite.bounds.size.x : 1f;
        f.sr.transform.localScale = Vector3.one * (f.size * Mathf.Lerp(1f, 0.5f, k) / w);
    }

    private void UpdateGlows(float dt)
    {
        if (dt <= 0f) return;
        foreach (var f in glows)
        {
            if (f.sr == null || !f.sr.gameObject.activeSelf) continue;
            f.age += dt;
            float t = f.age / Mathf.Max(0.0001f, f.life);
            if (t >= 1f) { f.sr.gameObject.SetActive(false); continue; }
            if (f.vel.sqrMagnitude > 0f)
            {
                f.sr.transform.position += (Vector3)(f.vel * dt);
                f.vel *= Mathf.Exp(-f.damping * dt);
            }
            ApplyGlow(f, t);
        }
    }

    protected virtual void OnDestroy()
    {
        if (glowRoot != null) Destroy(glowRoot.gameObject);
    }

#if UNITY_EDITOR
    protected virtual void OnValidate()
    {
        if (!Application.isPlaying) UnityEditor.EditorApplication.delayCall += () => { if (this != null) UpdateEditorPreview(); };
    }

    /// <summary>Play前：ダンサーの上にアイコンと秒数を出して位置を確認できるようにする</summary>
    protected void UpdateEditorPreview()
    {
        var d = FindFirstObjectByType<PixelDancerController>();
        bool show = previewInEditor && d != null;
        SetIconVisible(show);
        if (!show) return;
        Vector3 p = FollowPoint(d);
        p.y = d.transform.position.y;
        PlaceIcon(p);
        if (secondsText != null) secondsText.text = Mathf.CeilToInt(baseDuration20).ToString();
        SetAlpha(1f);
    }

    /// <summary>画像を読む（Multipleのまま。絵の部分だけを切り出したスプライトを使う）。無ければエラーを出してnull</summary>
    public static Sprite LoadSpriteForEditor(string path)
    {
        MergeSlicesIntoOne(path);
        foreach (var o in UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path))
            if (o is Sprite sp) return sp;
        Debug.LogError("[ItemBuff] " + path + " のスプライトが見つかりません（画像の置き場所と、Texture Type が Sprite か確認してください）");
        return null;
    }

    /// <summary>
    /// Multiple で絵が離れた部品ごとに分けて切り出されている時（例：アクセラレーターの3つの矢印ときらめきが4つに分かれた）、
    /// 全部を囲む1枚にまとめる。絵の部分だけを切り出した形（余白なし）は保つ
    /// </summary>
    /// <summary>実際のまとめる処理は Editor の SpriteSliceMergeTool（Sprite Editor のデータを使うので Editor 側に置く）が入れる</summary>
    public static System.Func<string, bool> MergeSlicesHook;

    private static void MergeSlicesIntoOne(string path)
    {
        if (MergeSlicesHook != null) MergeSlicesHook(path);
    }

    /// <summary>アイコンの画像を設定する（右クリックメニューから）</summary>
    protected void SetIconSpriteFromPath(string path)
    {
        var sp = LoadSpriteForEditor(path);
        if (sp == null || iconRenderer == null) return;
        UnityEditor.Undo.RecordObject(iconRenderer, "アイコンの画像を設定");
        iconRenderer.sprite = sp;
        UnityEditor.EditorUtility.SetDirty(iconRenderer);
        UpdateEditorPreview();
        if (gameObject.scene.IsValid()) UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
        UnityEditor.SceneView.RepaintAll();
        Debug.Log($"[{LogTag}] アイコンの画像を設定しました（{path}）。シーンを保存してください。");
    }

    /// <summary>Setupツールから参照をつなぐ（共通部分）</summary>
    public void EditorWireBase(SpriteRenderer icon, TextMesh seconds, Sprite glow, int order)
    {
        iconRenderer = icon;
        secondsText = seconds;
        if (footGlowSprite == null) footGlowSprite = glow;
        iconOrder = order;
    }
#endif
}
