using System.Reflection;
using UnityEngine;

/// <summary>
/// ドリル弾（Pinned Reflect）1発ごとに付ける演出の部品（Tsukuyomi・NeonDancerが発射時に付ける）。
/// 弾の状態（飛んでいる／刺さっている／抜けた／消えた）を読み取り、共有の管理役DrillFXManagerに演出を頼む。
/// ・通常：① 螺旋の風切り ② ダイヤモンドダスト ③ 削りの火花と震え・ヒットの輪 ④ 溜まっていく輝き ⑤ 砕け散る
/// ・強化：⑥ 紅いオーラ ⑦ 出現時の紋章 ⑧ 赤い残像 ⑨ 刺さっている間の紅い稲妻
/// ★刺さっている状態はPinnedReflectBulletから読むだけで、ドリルの動き・当たり判定には一切触れない
///   （ヒット数等はprivateのため読み取る）。震えは見た目の子（Visual）だけを動かし、消える時に元に戻す。
/// プールで使い回される弾に残らないよう、弾が消える時に借りた画像・線を返して自分自身を外す。
/// </summary>
[DisallowMultipleComponent]
public class DrillFX : MonoBehaviour
{
    private EnemyBullet bullet;
    private PinnedReflectBullet pinned;
    private Rigidbody2D rb;
    private SpriteRenderer visual;
    private Vector3 visualBaseLocalPos;
    private DrillFXManager mgr;
    private bool enhanced;

    private SpriteRenderer[] rings;
    private SpriteRenderer glow;
    private SpriteRenderer aura;
    private SpriteRenderer[] ghosts;
    private LineRenderer[] bolts;

    // 残像用の過去の位置・向き・絵（環状バッファ）
    private const int HistorySize = 32;
    private readonly Vector3[] histPos = new Vector3[HistorySize];
    private readonly Quaternion[] histRot = new Quaternion[HistorySize];
    private readonly Sprite[] histSprite = new Sprite[HistorySize];
    private readonly float[] histTime = new float[HistorySize];
    private int histHead;
    private int histCount;

    private bool wasPinned;
    private bool everPinned;
    private int lastHits;
    private float dustAcc, sparkAcc, boltTimer, clock;
    private Vector3 lastPos;
    private bool initialized;

    private static FieldInfo s_currentHits, s_requiredHits, s_pinnedRequiredHits, s_toEnemyMode, s_pinnedDirection;

    /// <summary>ドリル弾に付ける（発射時に呼ぶ）。managerPrefabが空なら何もしない</summary>
    public static DrillFX Attach(EnemyBullet b, DrillFXManager managerPrefab)
    {
        if (b == null || managerPrefab == null) return null;
        var m = DrillFXManager.GetOrCreate(managerPrefab);
        if (m == null) return null;
        var fx = b.GetComponent<DrillFX>();
        if (fx == null) fx = b.gameObject.AddComponent<DrillFX>();
        fx.Init(b, m);
        return fx;
    }

    private void Init(EnemyBullet b, DrillFXManager m)
    {
        bullet = b;
        mgr = m;
        pinned = b.GetComponent<PinnedReflectBullet>();
        rb = b.GetComponent<Rigidbody2D>();
        Transform v = b.transform.Find("Visual");
        visual = v != null ? v.GetComponent<SpriteRenderer>() : b.GetComponentInChildren<SpriteRenderer>();
        if (visual != null) visualBaseLocalPos = visual.transform.localPosition;
        lastPos = b.transform.position;
        initialized = true;

        if (s_currentHits == null)
        {
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var t = typeof(PinnedReflectBullet);
            s_currentHits = t.GetField("currentHits", flags);
            s_requiredHits = t.GetField("requiredHits", flags);
            s_pinnedRequiredHits = t.GetField("pinnedRequiredHits", flags);
            s_toEnemyMode = t.GetField("pinnedToEnemyMode", flags);
            s_pinnedDirection = t.GetField("pinnedDirection", flags);
        }

        int layer = visual != null ? visual.sortingLayerID : 0, order = visual != null ? visual.sortingOrder : 0;
        rings = new SpriteRenderer[Mathf.Max(0, mgr.ringCount)];
        for (int i = 0; i < rings.Length; i++) { rings[i] = mgr.RentSprite(mgr.ringSprite, layer, order - 1); rings[i].enabled = false; }
        glow = mgr.RentSprite(mgr.glowSprite, layer, order - 2);
        glow.enabled = false;
    }

    /// <summary>強化ドリルにする（出現時の紋章・紅いオーラ・赤い残像・刺さっている間の稲妻）</summary>
    public void SetEnhanced()
    {
        if (!initialized || enhanced) return;
        enhanced = true;
        int layer = visual != null ? visual.sortingLayerID : 0, order = visual != null ? visual.sortingOrder : 0;
        aura = mgr.RentSprite(mgr.glowSprite, layer, order - 3);
        ghosts = new SpriteRenderer[Mathf.Max(0, mgr.afterimageCount)];
        for (int i = 0; i < ghosts.Length; i++)
        {
            ghosts[i] = mgr.RentSprite(null, layer, order - 1);
            ghosts[i].sharedMaterial = visual != null ? visual.sharedMaterial : ghosts[i].sharedMaterial; // 結晶の絵そのままの見た目
            ghosts[i].enabled = false;
        }
        bolts = new LineRenderer[Mathf.Max(0, mgr.boltCount)];
        for (int i = 0; i < bolts.Length; i++) { bolts[i] = mgr.RentLine(layer, order + 2); bolts[i].gameObject.SetActive(false); }
        mgr.SpawnSigil(bullet.transform.position, layer, order + 3);
    }

    private void LateUpdate()
    {
        if (!initialized || bullet == null || mgr == null) return;
        float dt = SlowMoTime.DeltaTime;
        clock += dt;
        Vector3 pos = bullet.transform.position;
        float visAlpha = visual != null ? visual.color.a : 1f;
        float size = visual != null ? Mathf.Max(visual.bounds.size.x, visual.bounds.size.y) : 0.5f;
        Vector2 vel = rb != null ? rb.linearVelocity : (Vector2)(pos - lastPos) / Mathf.Max(0.0001f, Time.deltaTime);
        Vector2 dir = vel.sqrMagnitude > 0.0001f ? vel.normalized : (visual != null ? (Vector2)visual.transform.up : Vector2.down);
        bool moving = vel.sqrMagnitude > 0.04f;
        bool pinnedNow = pinned != null && pinned.IsPinned;

        RecordHistory(pos);

        // ---------- 飛んでいる間：① 螺旋の風切り ② ダイヤモンドダスト ----------
        bool showRings = moving && !pinnedNow && visAlpha > 0.5f;
        for (int i = 0; i < rings.Length; i++)
        {
            var r = rings[i];
            if (r == null) continue;
            r.enabled = showRings;
            if (!showRings) continue;
            float ph = Mathf.Repeat(clock / Mathf.Max(0.05f, mgr.ringCycleSeconds) + i / (float)rings.Length, 1f);
            r.transform.position = pos + (Vector3)(dir * size * (0.35f - ph * mgr.ringTravelMul));
            float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            r.transform.rotation = Quaternion.Euler(0f, 0f, ang);
            float w = size * mgr.ringWidthMul * Mathf.Lerp(0.7f, 1.15f, ph);
            float s = r.sprite != null ? Mathf.Max(0.01f, r.sprite.bounds.size.x) : 1f;
            r.transform.localScale = new Vector3(w * mgr.ringThickness / s, w / s, 1f); // 進む方向（x）に薄い楕円
            Color c = enhanced ? Color.Lerp(mgr.ringColor, mgr.auraColor, 0.6f) : mgr.ringColor;
            c.a = mgr.ringAlpha * Mathf.Sin(ph * Mathf.PI) * visAlpha;
            r.color = c;
        }
        if (moving && !pinnedNow && visAlpha > 0.5f)
        {
            dustAcc += mgr.dustRate * dt;
            while (dustAcc >= 1f)
            {
                dustAcc -= 1f;
                Vector2 jitter = Random.insideUnitCircle * size * 0.3f;
                mgr.EmitDust(pos - (Vector3)(dir * size * 0.3f) + (Vector3)jitter, enhanced ? Color.Lerp(mgr.dustColor, mgr.auraColor, 0.5f) : mgr.dustColor);
            }
        }

        // ---------- 刺さっている間：③ 削りの火花と震え・ヒットの輪 ④ 溜まっていく輝き ----------
        int hits = 0, required = 1;
        if (pinned != null && s_currentHits != null)
        {
            hits = (int)s_currentHits.GetValue(pinned);
            bool toEnemy = s_toEnemyMode != null && (bool)s_toEnemyMode.GetValue(pinned);
            required = toEnemy && s_pinnedRequiredHits != null ? (int)s_pinnedRequiredHits.GetValue(pinned) : (int)s_requiredHits.GetValue(pinned);
            required = Mathf.Max(1, required);
        }
        if (pinnedNow)
        {
            everPinned = true;
            Vector2 pinDir = s_pinnedDirection != null ? (Vector2)s_pinnedDirection.GetValue(pinned) : dir;
            if (pinDir.sqrMagnitude < 0.0001f) pinDir = dir;
            Vector3 tip = pos + (Vector3)(pinDir.normalized * size * 0.4f);
            float backAngle = Mathf.Atan2(-pinDir.y, -pinDir.x) * Mathf.Rad2Deg; // 刺さった所から手前へ飛び散る
            sparkAcc += mgr.sparkRate * dt;
            int sc = Mathf.FloorToInt(sparkAcc);
            if (sc > 0)
            {
                sparkAcc -= sc;
                mgr.EmitBurst(mgr.sparks, tip, sc, enhanced ? Color.Lerp(mgr.sparkColor, mgr.auraColor, 0.6f) : mgr.sparkColor, mgr.sparkSpeed, backAngle, mgr.sparkSpreadDeg);
            }
            if (visual != null)
                visual.transform.localPosition = visualBaseLocalPos + (Vector3)(Random.insideUnitCircle * mgr.shakeAmount / Mathf.Max(0.01f, bullet.transform.lossyScale.x));
            if (hits > lastHits)
                mgr.SpawnHitRing(tip, size, enhanced ? mgr.auraColor : mgr.glowColor, visual != null ? visual.sortingLayerID : 0, visual != null ? visual.sortingOrder + 1 : 0);
        }
        else if (visual != null)
        {
            visual.transform.localPosition = visualBaseLocalPos;
        }
        if (wasPinned && !pinnedNow && bullet.gameObject.activeInHierarchy)
            mgr.SpawnShards(pos, mgr.shardCountOnRelease, enhanced ? mgr.auraColor : mgr.shardColor); // 抜けた・反射した瞬間
        lastHits = pinnedNow ? hits : 0;
        wasPinned = pinnedNow;

        if (glow != null)
        {
            float target = pinnedNow ? mgr.glowMaxAlpha * Mathf.Clamp01((hits + 1f) / required) : 0f;
            Color gc = enhanced ? Color.Lerp(mgr.glowColor, mgr.auraColor, 0.5f) : mgr.glowColor;
            float a = Mathf.MoveTowards(glow.color.a, target * visAlpha, dt * 4f);
            gc.a = a;
            glow.color = gc;
            glow.enabled = a > 0.001f;
            glow.transform.position = pos;
            DrillFXManager.SetSpriteSize(glow, size * mgr.glowSizeMul * (1f + 0.08f * Mathf.Sin(clock * 20f)));
        }

        // ---------- 強化：⑥ 紅いオーラ ⑧ 赤い残像 ⑨ 紅い稲妻 ----------
        if (enhanced)
        {
            if (aura != null)
            {
                float p = (Mathf.Sin(clock * mgr.auraPulseSpeed) + 1f) * 0.5f;
                Color ac = mgr.auraColor;
                ac.a = Mathf.Lerp(mgr.auraAlphaMin, mgr.auraAlphaMax, p) * visAlpha;
                aura.color = ac;
                aura.transform.position = pos;
                DrillFXManager.SetSpriteSize(aura, size * mgr.auraSizeMul * Mathf.Lerp(0.9f, 1.15f, p));
            }
            UpdateGhosts(moving && !pinnedNow, visAlpha);
            UpdateBolts(pinnedNow, pos, size, dt);
        }

        lastPos = pos;
    }

    private void RecordHistory(Vector3 pos)
    {
        histHead = (histHead + 1) % HistorySize;
        histPos[histHead] = pos;
        histRot[histHead] = visual != null ? visual.transform.rotation : Quaternion.identity;
        histSprite[histHead] = visual != null ? visual.sprite : null;
        histTime[histHead] = clock;
        histCount = Mathf.Min(HistorySize, histCount + 1);
    }

    private void UpdateGhosts(bool show, float visAlpha)
    {
        if (ghosts == null) return;
        for (int i = 0; i < ghosts.Length; i++)
        {
            var g = ghosts[i];
            if (g == null) continue;
            float wantTime = clock - mgr.afterimageDelay * (i + 1);
            int idx = -1;
            for (int k = 0; k < histCount; k++)
            {
                int j = (histHead - k + HistorySize) % HistorySize;
                if (histTime[j] <= wantTime) { idx = j; break; }
            }
            bool on = show && idx >= 0 && histSprite[idx] != null;
            g.enabled = on;
            if (!on) continue;
            g.sprite = histSprite[idx];
            g.transform.position = histPos[idx];
            g.transform.rotation = histRot[idx];
            if (visual != null) g.transform.localScale = visual.transform.lossyScale;
            Color c = mgr.afterimageColor;
            c.a = mgr.afterimageAlpha * (1f - i / (float)(ghosts.Length + 1)) * visAlpha;
            g.color = c;
        }
    }

    private void UpdateBolts(bool show, Vector3 pos, float size, float dt)
    {
        if (bolts == null) return;
        boltTimer -= dt;
        bool redraw = boltTimer <= 0f;
        if (redraw) boltTimer = Mathf.Max(0.02f, mgr.boltRefreshInterval);
        foreach (var lr in bolts)
        {
            if (lr == null) continue;
            if (lr.gameObject.activeSelf != show) lr.gameObject.SetActive(show);
            if (!show) continue;
            lr.startWidth = mgr.boltWidth;
            lr.endWidth = mgr.boltWidth * 0.3f;
            lr.startColor = mgr.boltColor;
            lr.endColor = new Color(mgr.boltColor.r, mgr.boltColor.g, mgr.boltColor.b, 0f);
            if (!redraw) continue;
            int pts = 6;
            lr.positionCount = pts;
            float ang = Random.Range(0f, Mathf.PI * 2f);
            Vector3 d = new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), 0f);
            Vector3 perp = new Vector3(-d.y, d.x, 0f);
            float len = Random.Range(mgr.boltLength.x, mgr.boltLength.y);
            for (int k = 0; k < pts; k++)
            {
                float t = k / (float)(pts - 1);
                float j = k == 0 ? 0f : Random.Range(-0.15f, 0.15f) * len;
                lr.SetPosition(k, pos + d * (size * 0.3f + len * t) + perp * j);
            }
        }
    }

    private void OnDisable()
    {
        if (!initialized) { Destroy(this); return; }
        // ⑤ 砕け散る：刺さって削り終えて消えた時など（画面外に出て消えた時は出さない）
        if (mgr != null && bullet != null && (everPinned || IsOnScreen(lastPos)))
            mgr.SpawnShards(lastPos, mgr.shardCountOnVanish, enhanced ? mgr.auraColor : mgr.shardColor);
        if (visual != null) visual.transform.localPosition = visualBaseLocalPos;
        if (mgr != null)
        {
            if (rings != null) foreach (var r in rings) mgr.ReleaseSprite(r);
            mgr.ReleaseSprite(glow);
            mgr.ReleaseSprite(aura);
            if (ghosts != null) foreach (var g in ghosts) mgr.ReleaseSprite(g);
            if (bolts != null) foreach (var b in bolts) mgr.ReleaseLine(b);
        }
        initialized = false;
        Destroy(this);
    }

    private static bool IsOnScreen(Vector3 p)
    {
        var cam = Camera.main;
        if (cam == null) return true;
        Vector3 v = cam.WorldToViewportPoint(p);
        return v.x > -0.05f && v.x < 1.05f && v.y > -0.05f && v.y < 1.05f;
    }
}
