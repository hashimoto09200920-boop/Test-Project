using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// プレイヤーダンサーとフロアが、エネミーの未反射弾（ビーム等も含む）でダメージを受けた時の演出の管理役。
/// Assets/Resources/PlayerHitFX.prefab から1つだけ作られる（プレハブが無ければ何もしない＝従来どおり。メニュー「Tools/弾の見た目/11 …」で作成）。
/// 今の画面の揺れ・被弾SE・点滅はそのまま残し、その上に重ねる。ダンサー＝赤い「体への衝撃」、フロア＝オレンジ〜土色の「床が削れた」で見分けられるようにする。
/// ・共通：① 当たった点で弾の色の閃光と火花（旧VFX_BulletDestroyの代わり） ② 画面フラッシュの色分け（ダンサー＝赤のまま、フロア＝オレンジ）
/// ・ダンサー：D1 体が白→赤に光る／D2 体を囲む赤いリング／D3 赤〜マゼンタの粒が散る／D4 当たった点のギザギザの衝撃マーク
/// ・フロア：F1 床に沿ってひびが走る／F2 床の破片と砂ぼこり／F3 床に沿った横長の衝撃波／F4 床全体がオレンジに明滅
/// </summary>
[DisallowMultipleComponent]
public class PlayerHitFXManager : MonoBehaviour
{
    [Header("全体")]
    [Tooltip("OFFにすると全ての演出を止める（従来どおり）")]
    public bool fxEnabled = true;
    [Tooltip("1フレームに出す着弾演出（①）の上限")]
    public int maxImpactsPerFrame = 3;

    [Header("共有のパーティクル・画像（メニューで自動設定）")]
    public ParticleSystem sparkle;
    [Tooltip("重力で落ちる破片（アルファ）")]
    public ParticleSystem debris;
    [Tooltip("砂ぼこり（アルファ）")]
    public ParticleSystem smoke;
    public Sprite glowSprite;
    public Sprite ringSprite;
    [Tooltip("加算の画像用マテリアル")]
    public Material additiveSpriteMaterial;
    [Tooltip("線（ひび）のマテリアル（加算）")]
    public Material lineMaterial;
    [Tooltip("演出をダンサー・フロアの絵より手前に出す量")]
    public int sortingOrderOffset = 5;

    [Header("① 着弾（弾の色の閃光と火花）")]
    public bool impactEnabled = true;
    [Tooltip("弾の色が取れない時の色")]
    public Color impactFallbackColor = new Color(0.85f, 0.15f, 0.2f, 1f);
    [Tooltip("弾の色をどれだけ明るくするか")]
    [Range(0f, 1f)] public float impactBrighten = 0.3f;
    public float impactFlashSize = 0.8f;
    public int impactSparkCount = 8;
    public float impactSparkSpeed = 3f;

    [Header("② 画面フラッシュの色分け")]
    public bool screenFlashColorEnabled = true;
    [Tooltip("フロアが削られた時の画面フラッシュの色（ダンサーは従来どおり赤）")]
    public Color floorScreenFlashColor = new Color(1f, 0.55f, 0.1f, 0f);

    [Header("ダンサー（赤い体への衝撃）")]
    public Color dancerColor = new Color(1f, 0.15f, 0.2f, 1f);
    public Color dancerColor2 = new Color(1f, 0.25f, 0.85f, 1f);
    [Tooltip("D1 体が光る時間")]
    public float dancerFlashSeconds = 0.22f;
    [Range(0f, 1f)] public float dancerFlashAlpha = 0.9f;
    [Tooltip("D2 リングの大きさ（ダンサーの大きさに対する倍率）")]
    public float dancerRingSizeMul = 1.3f;
    public float dancerRingDuration = 0.28f;
    [Tooltip("D3 散る粒の数")]
    public int dancerParticleCount = 16;
    public float dancerParticleSpeed = 3.5f;
    [Tooltip("D4 衝撃マークのとげの数")]
    public int dancerImpactSpikes = 6;
    public float dancerImpactSize = 0.9f;
    public float dancerImpactDuration = 0.16f;

    [Header("フロア（床が削れた）")]
    public Color floorColor = new Color(1f, 0.55f, 0.15f, 1f);
    public Color floorDebrisColor = new Color(0.6f, 0.45f, 0.3f, 1f);
    public Color floorDustColor = new Color(0.55f, 0.48f, 0.4f, 0.55f);
    [Tooltip("F1 ひびの本数（左右合計）")]
    public int floorCrackCount = 4;
    public float floorCrackLength = 1.4f;
    public float floorCrackWidth = 0.05f;
    public float floorCrackSeconds = 0.45f;
    [Tooltip("F2 破片の数")]
    public int floorDebrisCount = 12;
    public float floorDebrisSpeed = 3.5f;
    public int floorDustCount = 4;
    [Tooltip("F3 横長の衝撃波の幅")]
    public float floorWaveWidth = 3f;
    [Tooltip("F3 衝撃波の縦の潰れ具合（幅に対する割合）")]
    [Range(0.05f, 1f)] public float floorWaveFlatten = 0.22f;
    public float floorWaveDuration = 0.32f;
    [Tooltip("F4 床全体が明滅する時間")]
    public float floorFlashSeconds = 0.2f;
    [Range(0f, 1f)] public float floorFlashAlpha = 0.55f;

    // ---------- 共有インスタンス ----------
    private static PlayerHitFXManager s_instance;
    private static bool s_triedLoad;

    public static PlayerHitFXManager Instance
    {
        get
        {
            if (s_instance != null) return s_instance;
            if (s_triedLoad) return null;
            s_triedLoad = true;
            var prefab = Resources.Load<PlayerHitFXManager>("PlayerHitFX");
            if (prefab == null) return null;
            s_instance = Instantiate(prefab);
            s_instance.name = "PlayerHitFX";
            return s_instance;
        }
    }

    private static bool Ready(out PlayerHitFXManager m) { m = Instance; return m != null && m.fxEnabled; }

    // ---------- 外から呼ぶ入口 ----------
    /// <summary>未反射弾がダンサー／フロアに当たって消える時（EnemyBullet）。出したらtrue（旧VFX_BulletDestroyを出さない）</summary>
    public static bool NotifyBulletImpact(EnemyBullet b, Vector3 pos, bool isDancer)
    {
        if (b == null || !Ready(out var m) || !m.impactEnabled) return false;
        if (isDancer) { m.lastDancerHitPos = pos; m.lastDancerHitTime = Time.time; }
        else { m.lastFloorHitPos = pos; m.lastFloorHitTime = Time.time; }
        if (m.impactFrame != Time.frameCount) { m.impactFrame = Time.frameCount; m.impactCount = 0; }
        if (m.impactCount >= m.maxImpactsPerFrame) return true;
        m.impactCount++;
        Color c = BulletFXManager.TryGetBulletColor(b, out Color auraC) ? auraC : m.impactFallbackColor;
        c = Color.Lerp(c, Color.white, m.impactBrighten); c.a = 1f;
        Transform v = b.transform.Find("Visual");
        var vsr = v != null ? v.GetComponent<SpriteRenderer>() : null;
        var rb = b.GetComponent<Rigidbody2D>();
        Vector2 dir = rb != null && rb.linearVelocity.sqrMagnitude > 0.0001f ? rb.linearVelocity.normalized : Vector2.down;
        m.PlayImpact(pos, c, dir, vsr != null ? vsr.sortingLayerID : 0, vsr != null ? vsr.sortingOrder + 3 : 30);
        return true;
    }

    /// <summary>
    /// 当たった位置だけを先に知らせる（ビーム・ドリル・床側の弾の判定から。ダメージ処理より前に呼ぶ）。
    /// これが無いと、ダメージ演出は代わりの位置（床の中心・画像の上端）に出てしまう
    /// </summary>
    public static void NotifyHitPoint(Vector3 pos, bool isDancer)
    {
        var m = Instance;
        if (m == null) return;
        if (isDancer) { m.lastDancerHitPos = pos; m.lastDancerHitTime = Time.time; }
        else { m.lastFloorHitPos = pos; m.lastFloorHitTime = Time.time; }
    }

    /// <summary>ダンサーがダメージを受けた時（PixelDancerController.PlayHitFeedback）</summary>
    public static void NotifyDancerDamaged(SpriteRenderer dancerSr, Vector3 center)
    {
        if (!Ready(out var m)) return;
        Vector3 hit = Time.time - m.lastDancerHitTime < 0.15f ? m.lastDancerHitPos : center;
        m.PlayDancer(dancerSr, center, hit);
    }

    /// <summary>フロアがダメージを受けた時（FloorHealth）。画面フラッシュもここで出す（色分け）。出したらtrue（呼び出し側は従来のフラッシュを出さない）</summary>
    public static bool NotifyFloorDamaged(SpriteRenderer floorSr, Vector3 fallbackPos)
    {
        if (!Ready(out var m)) return false;
        Vector3 hit = Time.time - m.lastFloorHitTime < 0.15f ? m.lastFloorHitPos : fallbackPos;
        if (floorSr != null && Time.time - m.lastFloorHitTime >= 0.15f)
        {
            Bounds b = floorSr.bounds;
            hit = new Vector3(Mathf.Clamp(fallbackPos.x, b.min.x, b.max.x), b.max.y, 0f);
        }
        m.PlayFloor(floorSr, hit);
        if (m.screenFlashColorEnabled) DamageFlashUI.Flash(m.floorScreenFlashColor);
        else DamageFlashUI.Flash();
        return true;
    }

    // ---------- 内部 ----------
    private class Anim
    {
        public SpriteRenderer sr; public LineRenderer lr; public float t, dur, s0, s1, aspect = 1f, rot, width; public Color c, c2; public bool useC2, holdThenFade;
        public SpriteRenderer copySrc; // D1・F4：元の絵に合わせて光らせる
    }
    private readonly List<Anim> anims = new List<Anim>();
    private readonly Stack<SpriteRenderer> freeSprites = new Stack<SpriteRenderer>();
    private readonly Stack<LineRenderer> freeLines = new Stack<LineRenderer>();
    private ParticleSystem[] particleList;
    private float lastParticleSpeed = float.NaN;
    private Vector3 lastDancerHitPos, lastFloorHitPos;
    private float lastDancerHitTime = -999f, lastFloorHitTime = -999f;
    private int impactFrame = -1, impactCount;
    private static readonly Vector3[] s_pts = new Vector3[6];

    private void Awake()
    {
        if (s_instance == null) s_instance = this;
        particleList = new[] { sparkle, debris, smoke };
        foreach (var ps in particleList)
        {
            if (ps == null) continue;
            var em = ps.emission; em.enabled = false;
            if (!ps.isPlaying) ps.Play(false);
        }
    }

    private void OnDestroy()
    {
        if (s_instance == this) { s_instance = null; s_triedLoad = false; }
    }

    // ① 着弾
    private void PlayImpact(Vector3 pos, Color c, Vector2 dir, int layer, int order)
    {
        AddSprite(glowSprite, pos, 0f, impactFlashSize * 0.5f, impactFlashSize, 1f, 0.12f, c, layer, order);
        AddSprite(glowSprite, pos, 0f, impactFlashSize * 0.25f, impactFlashSize * 0.5f, 1f, 0.08f, Color.white, layer, order + 1);
        if (sparkle != null)
        {
            float baseAng = Mathf.Atan2(-dir.y, -dir.x) * Mathf.Rad2Deg; // 跳ね返るように、来た向きの逆側へ
            var ep = new ParticleSystem.EmitParams { position = pos, startSize = 0.07f };
            for (int i = 0; i < impactSparkCount; i++)
            {
                float a = (baseAng + Random.Range(-70f, 70f)) * Mathf.Deg2Rad;
                ep.velocity = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * impactSparkSpeed * Random.Range(0.4f, 1.1f);
                ep.startColor = Color.Lerp(c, Color.white, Random.Range(0f, 0.4f));
                sparkle.Emit(ep, 1);
            }
        }
    }

    // ダンサー
    private void PlayDancer(SpriteRenderer dsr, Vector3 center, Vector3 hit)
    {
        int layer = dsr != null ? dsr.sortingLayerID : 0, order = (dsr != null ? dsr.sortingOrder : 10) + sortingOrderOffset;
        float size = dsr != null && dsr.sprite != null ? Mathf.Max(0.4f, Mathf.Max(dsr.bounds.size.x, dsr.bounds.size.y)) : 1f;
        if (dsr != null) center = dsr.bounds.center;

        // D1 体が白→赤に光る
        if (dsr != null && dsr.sprite != null)
        {
            var sr = RentSprite(dsr.sprite, dsr.sortingLayerID, dsr.sortingOrder + 1);
            CopyTransform(sr, dsr);
            anims.Add(new Anim { sr = sr, dur = dancerFlashSeconds, c = new Color(1f, 1f, 1f, dancerFlashAlpha), c2 = new Color(dancerColor.r, dancerColor.g, dancerColor.b, dancerFlashAlpha), useC2 = true, copySrc = dsr });
        }
        // D2 体を囲む赤いリング
        AddSprite(ringSprite, center, 0f, size * 0.6f, size * dancerRingSizeMul * 1.25f, 1f, dancerRingDuration, dancerColor, layer, order);
        // D3 赤〜マゼンタの粒
        if (sparkle != null)
        {
            var ep = new ParticleSystem.EmitParams();
            for (int i = 0; i < dancerParticleCount; i++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f);
                ep.position = center + (Vector3)(Random.insideUnitCircle * size * 0.25f);
                ep.velocity = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * dancerParticleSpeed * Random.Range(0.4f, 1.1f);
                ep.startColor = Color.Lerp(dancerColor, dancerColor2, Random.value);
                ep.startSize = Random.Range(0.06f, 0.12f);
                sparkle.Emit(ep, 1);
            }
        }
        // D4 当たった点のギザギザの衝撃マーク（とげ＝細長い光を放射状に）
        float off = Random.Range(0f, 360f);
        for (int i = 0; i < dancerImpactSpikes; i++)
        {
            float ang = off + i * 360f / Mathf.Max(1, dancerImpactSpikes) + Random.Range(-12f, 12f);
            float l = dancerImpactSize * (i % 2 == 0 ? 1f : 0.6f);
            Vector3 p = hit + Quaternion.Euler(0f, 0f, ang) * Vector3.right * l * 0.35f;
            AddSprite(glowSprite, p, ang, l * 0.4f, l * 0.8f, 0.18f, dancerImpactDuration, Color.Lerp(dancerColor, Color.white, 0.4f), layer, order + 2);
        }
        AddSprite(glowSprite, hit, 0f, dancerImpactSize * 0.5f, dancerImpactSize * 0.8f, 1f, dancerImpactDuration, Color.white, layer, order + 2);
    }

    // フロア
    private void PlayFloor(SpriteRenderer fsr, Vector3 hit)
    {
        int layer = fsr != null ? fsr.sortingLayerID : 0, order = (fsr != null ? fsr.sortingOrder : 10) + sortingOrderOffset;

        // F4 床全体がオレンジに明滅
        if (fsr != null && fsr.sprite != null)
        {
            var sr = RentSprite(fsr.sprite, fsr.sortingLayerID, fsr.sortingOrder + 1);
            CopyTransform(sr, fsr);
            anims.Add(new Anim { sr = sr, dur = floorFlashSeconds, c = new Color(floorColor.r, floorColor.g, floorColor.b, floorFlashAlpha), copySrc = fsr });
        }
        // F3 床に沿った横長の衝撃波
        AddSprite(ringSprite, hit, 0f, floorWaveWidth * 0.2f, floorWaveWidth * 1.25f, floorWaveFlatten, floorWaveDuration, floorColor, layer, order);
        AddSprite(glowSprite, hit, 0f, floorWaveWidth * 0.3f, floorWaveWidth * 0.7f, floorWaveFlatten * 0.8f, floorWaveDuration * 0.7f, new Color(floorColor.r, floorColor.g, floorColor.b, 0.6f), layer, order);
        // F1 床に沿ってひびが走る（左右へ、少し残ってから消える）
        if (lineMaterial != null)
        {
            for (int i = 0; i < floorCrackCount; i++)
            {
                float side = i % 2 == 0 ? 1f : -1f;
                float len = floorCrackLength * Random.Range(0.5f, 1f);
                Vector3 d = new Vector3(side, Random.Range(-0.12f, 0.05f), 0f).normalized * len;
                Vector3 n = new Vector3(-d.y, d.x, 0f).normalized * 0.08f;
                for (int k = 0; k < s_pts.Length; k++)
                {
                    float kk = k / (float)(s_pts.Length - 1);
                    s_pts[k] = hit + d * kk + (k == 0 ? Vector3.zero : n * Random.Range(-1f, 1f));
                }
                var lr = RentLine(layer, order + 1);
                lr.positionCount = s_pts.Length;
                lr.SetPositions(s_pts);
                anims.Add(new Anim { lr = lr, dur = floorCrackSeconds, width = floorCrackWidth, c = Color.Lerp(floorColor, Color.white, 0.4f), holdThenFade = true });
            }
        }
        // F2 床の破片と砂ぼこり
        if (debris != null)
        {
            var ep = new ParticleSystem.EmitParams();
            for (int i = 0; i < floorDebrisCount; i++)
            {
                ep.position = hit + new Vector3(Random.Range(-0.3f, 0.3f), 0f, 0f);
                ep.velocity = new Vector3(Random.Range(-0.8f, 0.8f), Random.Range(0.6f, 1.2f), 0f) * floorDebrisSpeed;
                ep.startColor = Color.Lerp(floorDebrisColor, floorColor, Random.Range(0f, 0.4f));
                ep.startSize = Random.Range(0.05f, 0.1f);
                debris.Emit(ep, 1);
            }
        }
        if (smoke != null)
        {
            var ep = new ParticleSystem.EmitParams();
            for (int i = 0; i < floorDustCount; i++)
            {
                ep.position = hit + new Vector3(Random.Range(-0.4f, 0.4f), Random.Range(0f, 0.15f), 0f);
                ep.velocity = new Vector3(Random.Range(-0.4f, 0.4f), Random.Range(0.2f, 0.5f), 0f);
                ep.startColor = floorDustColor;
                ep.startSize = Random.Range(0.35f, 0.6f);
                ep.startLifetime = Random.Range(0.6f, 0.9f);
                ep.rotation = Random.Range(0f, 360f);
                smoke.Emit(ep, 1);
            }
        }
    }

    // ---------- 毎フレーム ----------
    private void LateUpdate()
    {
        SlowMoTime.ParticleSpeed(particleList, ref lastParticleSpeed);
        float dt = Time.deltaTime;
        for (int i = anims.Count - 1; i >= 0; i--)
        {
            var a = anims[i];
            a.t += dt;
            float k = a.t / Mathf.Max(0.01f, a.dur);
            if (k >= 1f || (a.copySrc != null && !a.copySrc.enabled))
            {
                if (a.sr != null) ReleaseSprite(a.sr);
                if (a.lr != null) ReleaseLine(a.lr);
                anims.RemoveAt(i);
                continue;
            }
            Color col = a.useC2 ? Color.Lerp(a.c, a.c2, Mathf.Clamp01(k * 2f)) : a.c;
            float fade = a.holdThenFade ? (k < 0.5f ? 1f : 1f - (k - 0.5f) / 0.5f) : 1f - k;
            col.a *= fade;
            if (a.copySrc != null) { CopyTransform(a.sr, a.copySrc); a.sr.color = col; continue; }
            if (a.sr != null)
            {
                float eased = 1f - (1f - k) * (1f - k);
                float s = Mathf.Lerp(a.s0, a.s1, eased);
                a.sr.transform.rotation = Quaternion.Euler(0f, 0f, a.rot);
                float bx = a.sr.sprite != null ? Mathf.Max(0.01f, a.sr.sprite.bounds.size.x) : 1f;
                a.sr.transform.localScale = new Vector3(s / bx, s * a.aspect / bx, 1f);
                a.sr.color = col;
            }
            if (a.lr != null)
            {
                a.lr.startWidth = a.lr.endWidth = a.width;
                a.lr.startColor = col; a.lr.endColor = new Color(col.r, col.g, col.b, col.a * 0.3f);
            }
        }
    }

    // ---------- 共通 ----------
    private void AddSprite(Sprite sprite, Vector3 pos, float rot, float s0, float s1, float aspect, float dur, Color c, int layer, int order)
    {
        if (sprite == null) return;
        var sr = RentSprite(sprite, layer, order);
        sr.transform.position = pos;
        anims.Add(new Anim { sr = sr, dur = dur, s0 = s0, s1 = s1, aspect = aspect, rot = rot, c = c });
    }

    private static void CopyTransform(SpriteRenderer dst, SpriteRenderer src)
    {
        dst.sprite = src.sprite;
        dst.flipX = src.flipX; dst.flipY = src.flipY;
        dst.transform.SetPositionAndRotation(src.transform.position, src.transform.rotation);
        dst.transform.localScale = src.transform.lossyScale;
    }

    private SpriteRenderer RentSprite(Sprite sprite, int layer, int order)
    {
        SpriteRenderer sr = null;
        while (freeSprites.Count > 0 && sr == null) sr = freeSprites.Pop();
        if (sr == null)
        {
            var go = new GameObject("PlayerHitFX_Sprite");
            go.transform.SetParent(transform, false);
            sr = go.AddComponent<SpriteRenderer>();
        }
        if (additiveSpriteMaterial != null) sr.sharedMaterial = additiveSpriteMaterial;
        sr.sprite = sprite;
        sr.flipX = sr.flipY = false;
        sr.sortingLayerID = layer;
        sr.sortingOrder = order;
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

    private LineRenderer RentLine(int layer, int order)
    {
        LineRenderer lr = null;
        while (freeLines.Count > 0 && lr == null) lr = freeLines.Pop();
        if (lr == null)
        {
            var go = new GameObject("PlayerHitFX_Line");
            go.transform.SetParent(transform, false);
            lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.numCapVertices = 2;
            lr.alignment = LineAlignment.View;
            if (lineMaterial != null) lr.sharedMaterial = lineMaterial;
        }
        lr.sortingLayerID = layer;
        lr.sortingOrder = order;
        lr.gameObject.SetActive(true);
        return lr;
    }

    private void ReleaseLine(LineRenderer lr)
    {
        if (lr == null) return;
        lr.gameObject.SetActive(false);
        freeLines.Push(lr);
    }
}
