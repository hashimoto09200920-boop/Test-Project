using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// エネミーの予告線（弾道の予兆線）を派手にする共通の管理役。Assets/Resources/TelegraphFX.prefab から1つだけ作られる
/// （メニュー「Tools/弾の見た目/15 …」で作成）。予告線を出す全ての場所（EnemyShooter・WalkerMech・Zephyr・IronNest後半のNM・雷雲）から使う。
/// ・使い方（各所）：RentLine で線を借りる（⑧ 使い回し）→ Track で登録 → 毎フレーム Progress で進み具合（0〜1）を伝える → ReleaseLine で返す（撃った時は fired＝true）
/// ・各所の線の位置・色・濃さ（フェード・点滅）の計算は今までどおり各所が行い、この管理役はその上に飾りを重ねる：
///   ① 芯と外側の光 ② 流れる光 ③ 先端に向かって薄く ④ 発射口のチャージ光 ⑤ 照準リングが縮む ⑥ 直前の「来る！」 ⑦ 撃った瞬間に弾ける
/// ・プレハブが無い時は、RentLine は新しく作り、ReleaseLine は消すだけ（従来どおり）
/// </summary>
[DisallowMultipleComponent]
public class TelegraphFXManager : MonoBehaviour
{
    [Header("全体")]
    [Tooltip("OFFにすると飾りを出さない（線の使い回しは続ける）")]
    public bool fxEnabled = true;

    [Header("画像・マテリアル（メニューで自動設定）")]
    public Sprite glowSprite;
    public Sprite ringSprite;
    [Tooltip("加算の画像用マテリアル")]
    public Material additiveSpriteMaterial;
    [Tooltip("ぼかした線のマテリアル（加算）")]
    public Material lineMaterial;

    [Header("① 芯と外側の光")]
    public bool glowEnabled = true;
    [Tooltip("外側の光の太さ（予告線の太さに対する倍率）")]
    public float glowWidthMul = 7f;
    [Range(0f, 1f)] public float glowAlpha = 0.45f;

    [Header("② 流れる光")]
    public bool flowEnabled = true;
    public int flowCount = 3;
    [Tooltip("流れる速さ（ワールド単位/秒）")]
    public float flowSpeed = 14f;
    [Tooltip("流れる範囲（発射口からの長さ。予告線がこれより短ければ予告線の長さ）")]
    public float flowRange = 9f;
    public float flowSize = 0.25f;

    [Header("③ 先端に向かって薄く")]
    public bool tipFadeEnabled = true;
    [Tooltip("先端の濃さ（発射口側に対する割合）")]
    [Range(0f, 1f)] public float tipAlpha = 0.12f;

    [Header("④ 発射口のチャージ光")]
    public bool chargeEnabled = true;
    public float chargeSizeStart = 0.25f;
    public float chargeSizeEnd = 0.8f;

    [Header("⑤ 照準リングが縮む")]
    public bool ringEnabled = true;
    public float ringSizeStart = 1.5f;
    public float ringSizeEnd = 0.25f;

    [Header("⑥ 直前の「来る！」")]
    public bool finalFlashEnabled = true;
    [Tooltip("発射の何秒前から光るか")]
    public float finalFlashSeconds = 0.1f;
    [Tooltip("その間の線の太さの倍率")]
    public float finalFlashWidthMul = 2.5f;

    [Header("⑦ 撃った瞬間に弾ける")]
    public bool fireStreakEnabled = true;
    public float fireStreakDuration = 0.16f;
    [Tooltip("弾ける光が伸びる長さ（予告線がこれより短ければ予告線の長さ）")]
    public float fireStreakLength = 8f;
    [Tooltip("弾ける光の太さ（予告線の太さに対する倍率）")]
    public float fireStreakWidthMul = 3f;

    // ---------- 共有インスタンス ----------
    private static TelegraphFXManager s_instance;
    private static bool s_triedLoad;

    public static TelegraphFXManager Instance
    {
        get
        {
            if (s_instance != null) return s_instance;
            if (s_triedLoad) return null;
            s_triedLoad = true;
            var prefab = Resources.Load<TelegraphFXManager>("TelegraphFX");
            if (prefab == null) return null;
            s_instance = Instantiate(prefab);
            s_instance.name = "TelegraphFX";
            return s_instance;
        }
    }

    // ---------- 外から呼ぶ入口 ----------
    /// <summary>予告線を借りる（⑧ 使い回し）。設定（マテリアル・太さ・色・位置）は呼び出し側が今までどおり行う</summary>
    public static GameObject RentLine(string name, Transform parent, out LineRenderer lr)
    {
        var m = Instance;
        GameObject go = null;
        if (m != null)
        {
            while (m.freeTelegraphs.Count > 0 && go == null) go = m.freeTelegraphs.Pop();
        }
        if (go == null)
        {
            go = new GameObject(name);
            lr = go.AddComponent<LineRenderer>();
        }
        else lr = go.GetComponent<LineRenderer>();
        go.name = name;
        go.transform.SetParent(parent, false);
        go.SetActive(true);
        lr.enabled = true;
        return go;
    }

    /// <summary>借りた予告線に飾りを付ける。seconds＝予告の長さ（⑥の「直前」の判定に使う）</summary>
    public static void Track(LineRenderer lr, float seconds)
    {
        var m = Instance;
        if (m == null || lr == null || !m.fxEnabled) return;
        m.AddDeco(lr, seconds);
    }

    /// <summary>予告の進み具合（0＝出た直後 → 1＝撃つ瞬間）。各所のループで毎フレーム呼ぶ</summary>
    public static void Progress(LineRenderer lr, float k)
    {
        var m = s_instance;
        if (m == null || lr == null) return;
        if (m.decoByLine.TryGetValue(lr, out Deco d)) d.progress = Mathf.Clamp01(k);
    }

    /// <summary>予告線を返す。fired＝撃った（⑦ 弾ける光を出す）。エネミーが倒れた等で撃たずに消す時は false</summary>
    public static void ReleaseLine(GameObject go, bool fired)
    {
        if (go == null) return;
        var m = s_instance;
        var lr = go.GetComponent<LineRenderer>();
        if (m == null)
        {
            Object.Destroy(go);
            return;
        }
        if (lr != null && m.decoByLine.TryGetValue(lr, out Deco d))
        {
            if (fired && m.fxEnabled && m.fireStreakEnabled) m.SpawnStreak(d, lr);
            m.RemoveDeco(d);
        }
        go.SetActive(false);
        go.transform.SetParent(m.transform, false);
        m.freeTelegraphs.Push(go);
    }

    // ---------- 内部 ----------
    private class Deco
    {
        public LineRenderer lr;
        public LineRenderer glow;
        public SpriteRenderer[] flows;
        public SpriteRenderer charge, ring;
        public float seconds, progress, baseWidth, clock;
    }
    private class Streak { public LineRenderer lr; public SpriteRenderer flash; public Vector3 a, b; public float t, width; public Color c; }

    private readonly Stack<GameObject> freeTelegraphs = new Stack<GameObject>();
    private readonly Stack<LineRenderer> freeLines = new Stack<LineRenderer>();
    private readonly Stack<SpriteRenderer> freeSprites = new Stack<SpriteRenderer>();
    private readonly List<Deco> decos = new List<Deco>();
    private readonly Dictionary<LineRenderer, Deco> decoByLine = new Dictionary<LineRenderer, Deco>();
    private readonly List<Streak> streaks = new List<Streak>();

    private void Awake()
    {
        if (s_instance == null) s_instance = this;
    }

    private void OnDestroy()
    {
        if (s_instance == this) { s_instance = null; s_triedLoad = false; }
    }

    private void AddDeco(LineRenderer lr, float seconds)
    {
        if (decoByLine.TryGetValue(lr, out Deco old)) RemoveDeco(old);
        var d = new Deco { lr = lr, seconds = Mathf.Max(0.01f, seconds), baseWidth = lr.startWidth };
        int layer = lr.sortingLayerID, order = lr.sortingOrder;
        if (glowEnabled && lineMaterial != null) d.glow = RentDecoLine(layer, order - 1);
        if (flowEnabled && flowCount > 0 && glowSprite != null)
        {
            d.flows = new SpriteRenderer[flowCount];
            for (int i = 0; i < flowCount; i++) d.flows[i] = RentSprite(glowSprite, layer, order + 1);
        }
        if (chargeEnabled && glowSprite != null) d.charge = RentSprite(glowSprite, layer, order + 2);
        if (ringEnabled && ringSprite != null) d.ring = RentSprite(ringSprite, layer, order + 2);
        decos.Add(d);
        decoByLine[lr] = d;
    }

    private void RemoveDeco(Deco d)
    {
        if (d.glow != null) ReleaseDecoLine(d.glow);
        if (d.flows != null) foreach (var f in d.flows) ReleaseSprite(f);
        ReleaseSprite(d.charge); ReleaseSprite(d.ring);
        if (d.lr != null) { d.lr.startWidth = d.lr.endWidth = d.baseWidth; decoByLine.Remove(d.lr); }
        decos.Remove(d);
    }

    private void LateUpdate()
    {
        float dt = Time.deltaTime;
        for (int i = decos.Count - 1; i >= 0; i--)
        {
            var d = decos[i];
            var lr = d.lr;
            if (lr == null || !lr.gameObject.activeInHierarchy || !fxEnabled)
            {
                if (lr == null)
                {
                    // 呼び出し側が直接Destroyした時：飾りだけ片付ける
                    if (d.glow != null) ReleaseDecoLine(d.glow);
                    if (d.flows != null) foreach (var f in d.flows) ReleaseSprite(f);
                    ReleaseSprite(d.charge); ReleaseSprite(d.ring);
                    decos.RemoveAt(i);
                    var dead = new List<LineRenderer>();
                    foreach (var kv in decoByLine) if (kv.Key == null) dead.Add(kv.Key);
                    foreach (var deadKey in dead) decoByLine.Remove(deadKey);
                }
                else RemoveDeco(d);
                continue;
            }
            d.clock += dt;
            Vector3 p0 = lr.GetPosition(0), p1 = lr.GetPosition(1);
            Vector3 v = p1 - p0; float len = v.magnitude;
            Vector3 dir = len > 0.0001f ? v / len : Vector3.down;
            Color col = lr.startColor;            // 呼び出し側が決めた色・濃さ（フェード・点滅込み）
            float a = col.a;
            float k = d.progress;
            bool final = finalFlashEnabled && (1f - k) * d.seconds <= finalFlashSeconds;

            // ⑥ 直前の「来る！」：太く白く
            if (final)
            {
                lr.startWidth = lr.endWidth = d.baseWidth * finalFlashWidthMul;
                Color w = Color.Lerp(col, Color.white, 0.7f); w.a = Mathf.Max(a, 0.9f);
                lr.startColor = w;
                col = w; a = w.a;
            }
            else lr.startWidth = lr.endWidth = d.baseWidth;
            // ③ 先端に向かって薄く
            lr.endColor = tipFadeEnabled ? new Color(col.r, col.g, col.b, a * tipAlpha) : col;

            // ① 外側の光
            if (d.glow != null)
            {
                d.glow.SetPosition(0, p0); d.glow.SetPosition(1, p1);
                float gw = d.baseWidth * glowWidthMul * (final ? 1.5f : 1f);
                d.glow.startWidth = d.glow.endWidth = gw;
                Color gc = col; gc.a = a * glowAlpha;
                d.glow.startColor = gc; d.glow.endColor = new Color(gc.r, gc.g, gc.b, gc.a * (tipFadeEnabled ? tipAlpha * 0.5f : 1f));
            }
            // ② 流れる光
            if (d.flows != null)
            {
                float range = Mathf.Min(len, Mathf.Max(0.5f, flowRange));
                for (int j = 0; j < d.flows.Length; j++)
                {
                    var f = d.flows[j];
                    float dist = Mathf.Repeat(d.clock * flowSpeed + j * range / d.flows.Length, range);
                    f.transform.position = p0 + dir * dist;
                    SetSize(f, flowSize);
                    Color fc = Color.Lerp(col, Color.white, 0.5f); fc.a = a * (1f - dist / range);
                    f.color = fc;
                }
            }
            // ④ 発射口のチャージ光
            if (d.charge != null)
            {
                d.charge.transform.position = p0;
                float s = Mathf.Lerp(chargeSizeStart, chargeSizeEnd, k) * (final ? 1.4f : 1f);
                SetSize(d.charge, s);
                Color cc = Color.Lerp(col, Color.white, 0.4f); cc.a = Mathf.Lerp(0.35f, 1f, k);
                d.charge.color = cc;
            }
            // ⑤ 照準リングが縮む
            if (d.ring != null)
            {
                d.ring.transform.position = p0;
                SetSize(d.ring, Mathf.Lerp(ringSizeStart, ringSizeEnd, k * k));
                Color rc = col; rc.a = Mathf.Lerp(0.3f, 0.95f, k);
                d.ring.color = rc;
            }
        }

        // ⑦ 撃った瞬間に弾ける光
        for (int i = streaks.Count - 1; i >= 0; i--)
        {
            var s = streaks[i];
            s.t += dt;
            float k = s.t / Mathf.Max(0.01f, fireStreakDuration);
            if (k >= 1f) { ReleaseDecoLine(s.lr); ReleaseSprite(s.flash); streaks.RemoveAt(i); continue; }
            float head = 1f - (1f - k) * (1f - k);
            float tail = Mathf.Clamp01(k * 1.4f - 0.3f);
            s.lr.SetPosition(0, Vector3.Lerp(s.a, s.b, tail));
            s.lr.SetPosition(1, Vector3.Lerp(s.a, s.b, head));
            s.lr.startWidth = s.width * 0.4f * (1f - k); s.lr.endWidth = s.width * (1f - k * 0.5f);
            Color c = s.c; c.a *= 1f - k;
            s.lr.startColor = new Color(c.r, c.g, c.b, 0f); s.lr.endColor = c;
            if (s.flash != null)
            {
                s.flash.transform.position = s.a;
                SetSize(s.flash, Mathf.Lerp(0.9f, 0.3f, k));
                s.flash.color = c;
            }
        }
    }

    private void SpawnStreak(Deco d, LineRenderer lr)
    {
        if (lineMaterial == null) return;
        Vector3 p0 = lr.GetPosition(0), p1 = lr.GetPosition(1);
        Vector3 v = p1 - p0; float len = v.magnitude;
        Vector3 dir = len > 0.0001f ? v / len : Vector3.down;
        Color c = Color.Lerp(lr.startColor, Color.white, 0.6f); c.a = 1f;
        var s = new Streak
        {
            lr = RentDecoLine(lr.sortingLayerID, lr.sortingOrder + 1),
            flash = glowSprite != null ? RentSprite(glowSprite, lr.sortingLayerID, lr.sortingOrder + 2) : null,
            a = p0, b = p0 + dir * Mathf.Min(len, Mathf.Max(0.5f, fireStreakLength)),
            width = d.baseWidth * fireStreakWidthMul, c = c,
        };
        streaks.Add(s);
    }

    // ---------- 共通 ----------
    private static void SetSize(SpriteRenderer sr, float size)
    {
        float s = sr.sprite != null ? Mathf.Max(0.01f, sr.sprite.bounds.size.x) : 1f;
        sr.transform.localScale = new Vector3(size / s, size / s, 1f);
    }

    private SpriteRenderer RentSprite(Sprite sprite, int layer, int order)
    {
        SpriteRenderer sr = null;
        while (freeSprites.Count > 0 && sr == null) sr = freeSprites.Pop();
        if (sr == null)
        {
            var go = new GameObject("TelegraphFX_Sprite");
            go.transform.SetParent(transform, false);
            sr = go.AddComponent<SpriteRenderer>();
        }
        if (additiveSpriteMaterial != null) sr.sharedMaterial = additiveSpriteMaterial;
        sr.sprite = sprite;
        sr.sortingLayerID = layer;
        sr.sortingOrder = order;
        sr.color = Color.white;
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

    private LineRenderer RentDecoLine(int layer, int order)
    {
        LineRenderer lr = null;
        while (freeLines.Count > 0 && lr == null) lr = freeLines.Pop();
        if (lr == null)
        {
            var go = new GameObject("TelegraphFX_Line");
            go.transform.SetParent(transform, false);
            lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.positionCount = 2;
            lr.numCapVertices = 4;
            lr.alignment = LineAlignment.View;
            if (lineMaterial != null) lr.sharedMaterial = lineMaterial;
        }
        lr.sortingLayerID = layer;
        lr.sortingOrder = order;
        lr.gameObject.SetActive(true);
        return lr;
    }

    private void ReleaseDecoLine(LineRenderer lr)
    {
        if (lr == null) return;
        lr.gameObject.SetActive(false);
        freeLines.Push(lr);
    }
}
