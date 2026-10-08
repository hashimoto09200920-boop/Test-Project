using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ビーム（EnemyBeamBullet）の見た目を豪華にする部品。EnemyData.BulletType.beamStyleが設定されたビームの発射時に、
/// EnemyBeamBulletが自動で付ける（プレハブに付ける必要は無い）。
/// ビームの各区間（反射で折れ曲がるセグメント）の本体の線を毎フレーム読み取り、その上に見た目を重ねるだけで、
/// 当たり判定・ダメージ・反射には一切影響しない。重ねる物はセグメントの子に作るので、区間が消えると一緒に消える。
/// ① 白い芯・外側の光 ② 流れる模様 ③ 太さの揺らぎ ④ 発射口・反射点・着弾点のフレア ⑤ 発射の瞬間の太さ ⑥ 着弾点の火花
/// ＋ 反射した瞬間の閃光 ／ 反射後は色が変わる ／ 照射の終わりは発射口側から消える ／ 陽炎・火の粉（Dragon）／ 回路の稲妻（Obelisk）
/// ★EnemyBeamBulletが本体の線の色（フェード・明滅）を書いた後に上書きするため、実行順を後ろにしている
/// </summary>
[DefaultExecutionOrder(100)]
[DisallowMultipleComponent]
public class BeamStyleFX : MonoBehaviour
{
    private class Decor
    {
        public LineRenderer core, glow, flow, haze;
        public LineRenderer[] bolts;
        public SpriteRenderer endFlare;
        public GradientColorKey[] baseColors;
        public GradientColorKey[] tinted;
        public float boltTimer;
        public readonly Gradient mainGrad = new Gradient();
        public readonly Dictionary<LineRenderer, Gradient> grads = new Dictionary<LineRenderer, Gradient>();
    }

    private class Flash
    {
        public SpriteRenderer sr;
        public float t = -1f;
        public float size;
    }

    private EnemyBeamBullet beam;
    private BeamStyle st;
    private EnemyData.BulletType bt;
    private float elapsed;

    private readonly Dictionary<LineRenderer, Decor> decors = new Dictionary<LineRenderer, Decor>();
    private readonly List<LineRenderer> staleKeys = new List<LineRenderer>();
    private SpriteRenderer originFlare;
    private ParticleSystem impact, embers;
    private ParticleSystem[] particleList;
    private float lastParticleSpeed = float.NaN;
    private float impactAcc, emberAcc;
    private readonly List<Flash> flashes = new List<Flash>();
    private readonly List<(Vector3 pos, float time)> recentFlashes = new List<(Vector3, float)>();

    // 区間ごとの「発射口からの距離の割合」（照射の終わりに発射口側から消す計算用）
    private readonly Dictionary<int, Vector2> chainRange = new Dictionary<int, Vector2>();

    // 流れる模様・陽炎のマテリアル（スタイルごとに実行時の複製を1つだけ作り、全てのビームで共有して模様をずらす）
    private static readonly Dictionary<Material, Material> s_scrollMats = new Dictionary<Material, Material>();
    private static float s_flowClock;
    private static int s_flowClockFrame = -1;

    private static readonly GradientColorKey[] s_twoColor = new GradientColorKey[2];
    private static readonly GradientAlphaKey[] s_alpha4 = new GradientAlphaKey[4];
    private static readonly GradientAlphaKey[] s_alphaScratch = new GradientAlphaKey[4];

    public void Init(EnemyBeamBullet beamBullet, BeamStyle style, EnemyData.BulletType bulletType)
    {
        beam = beamBullet;
        st = style;
        bt = bulletType;
        elapsed = 0f;

        var list = new List<ParticleSystem>();
        if (st.impactSparksPrefab != null)
        {
            impact = Instantiate(st.impactSparksPrefab, transform);
            impact.transform.localPosition = Vector3.zero;
            // 火花の色はビームの色（少し白っぽく）
            var im = impact.main;
            Color bc = bt != null ? bt.beamColor : Color.white;
            im.startColor = new ParticleSystem.MinMaxGradient(Color.Lerp(bc, Color.white, 0.45f), Color.Lerp(bc, Color.white, 0.8f));
            list.Add(impact);
        }
        if (st.embersPrefab != null)
        {
            embers = Instantiate(st.embersPrefab, transform);
            embers.transform.localPosition = Vector3.zero;
            list.Add(embers);
        }
        foreach (var ps in list)
        {
            var em = ps.emission; em.enabled = false; // Emitで出す
            if (!ps.isPlaying) ps.Play(false);
        }
        particleList = list.ToArray();
    }

    private void LateUpdate()
    {
        if (beam == null || st == null) return;
        float dt = SlowMoTime.DeltaTime;
        elapsed += dt;
        AdvanceFlowClock();
        SlowMoTime.ParticleSpeed(particleList, ref lastParticleSpeed);

        int n = beam.VisualSegmentCount;
        ComputeChainRanges(n);

        float burst = 1f;
        if (st.fireBurstDuration > 0f && elapsed < st.fireBurstDuration)
        {
            float k = elapsed / st.fireBurstDuration;
            burst = Mathf.Lerp(st.fireBurstWidthMul, 1f, 1f - (1f - k) * (1f - k));
        }
        float pulse = 1f;
        if (bt != null && bt.beamPulseEnabled)
            pulse = Mathf.Lerp(bt.beamPulseMinAlphaMultiplier, 1f, (Mathf.Sin(Time.time * bt.beamPulseSpeed) + 1f) * 0.5f);

        float totalLen = 0f;
        for (int i = 0; i < n; i++)
        {
            if (!beam.GetVisualSegment(i, out LineRenderer line, out bool reflected, out bool openEnd, out bool hasNext, out float fade)) continue;
            Vector3 a = line.GetPosition(0), b = line.GetPosition(1);
            float len = Vector3.Distance(a, b);
            totalLen += len;

            if (!decors.TryGetValue(line, out Decor d))
            {
                d = CreateDecor(line);
                decors[line] = d;
                // 反射した瞬間の閃光（反射後の区間が新しくできた時、その始点で。掃射で毎フレーム作り直される区間は近い位置ではまとめる）
                if (st.useReflectFlash && reflected && i != 0) TryReflectFlash(a, line.startWidth);
            }

            float baseW = line.startWidth;
            float wob = 1f + st.wobbleAmplitude * (Mathf.Sin(elapsed * st.wobbleFrequency + i * 1.7f) * 0.7f + Mathf.Sin(elapsed * st.wobbleFrequency * 2.3f + i) * 0.3f);
            float wMul = wob * burst;
            line.widthMultiplier = wMul;

            // 照射の終わり：発射口側から消える（フェードの進み具合に合わせて、消えた所より先だけ表示）
            float tailP = (st.tailToTipFade && fade < 0.999f) ? 1f - fade : 0f;
            Vector2 range = chainRange.TryGetValue(i, out Vector2 r) ? r : new Vector2(0f, 1f);
            float uniformAlpha = (st.tailToTipFade ? 1f : fade) * pulse;
            BuildAlphaKeys(tailP, range, uniformAlpha);

            // 本体の線：元の色（反射後は白っぽく）
            if (d.baseColors == null) d.baseColors = line.colorGradient.colorKeys;
            var ck = d.baseColors;
            if (d.tinted == null || d.tinted.Length != ck.Length) d.tinted = new GradientColorKey[ck.Length];
            var tinted = d.tinted;
            for (int k = 0; k < ck.Length; k++)
                tinted[k] = new GradientColorKey(reflected && st.tintReflected ? Color.Lerp(ck[k].color, st.reflectedColor, st.reflectedTintAmount) : ck[k].color, ck[k].time);
            d.mainGrad.SetKeys(tinted, s_alpha4);
            line.colorGradient = d.mainGrad;

            Color beamColor = tinted.Length > 0 ? tinted[0].color : Color.white;

            SetLine(d, d.glow, a, b, baseW * st.glowWidthMul * wMul, beamColor, st.glowAlpha);
            SetLine(d, d.core, a, b, baseW * st.coreWidthMul * wMul, st.coreColor, 1f);
            SetLine(d, d.flow, a, b, baseW * st.flowWidthMul * wMul, Color.Lerp(beamColor, Color.white, 0.35f), st.flowAlpha);
            SetLine(d, d.haze, a, b, baseW * st.hazeWidthMul * (0.85f + 0.15f * wob), st.hazeColor, st.hazeAlpha);

            // 稲妻：一定間隔で形を描き替える
            if (d.bolts != null)
            {
                d.boltTimer -= dt;
                bool redraw = d.boltTimer <= 0f;
                if (redraw) d.boltTimer = Mathf.Max(0.01f, st.boltRefreshInterval);
                foreach (var bolt in d.bolts)
                {
                    if (bolt == null) continue;
                    if (redraw) DrawBolt(bolt, a, b);
                    bolt.startWidth = bolt.endWidth = st.boltWidth;
                    ApplyGradient(d, bolt, st.boltColor, 1f);
                }
            }

            // フレア：反射点・着弾点（伸びきった先端以外）
            if (d.endFlare != null)
            {
                bool show = !openEnd;
                d.endFlare.enabled = show;
                if (show) SetFlare(d.endFlare, b, baseW, beamColor, i, uniformAlpha * (tailP >= range.y ? 0f : 1f));
            }

            // 着弾点の火花（続きの無い、何かに当たって止まった区間の先端）
            if (impact != null && !openEnd && !hasNext && fade > 0.5f)
            {
                impactAcc += st.impactSparkRate * dt;
                int c = Mathf.FloorToInt(impactAcc);
                if (c > 0)
                {
                    impactAcc -= c;
                    var ep = new ParticleSystem.EmitParams { position = b, applyShapeToPosition = true };
                    impact.Emit(ep, c);
                }
            }
        }

        // 発射口のフレア
        if (st.useFlares && n > 0 && beam.GetVisualSegment(0, out LineRenderer l0, out _, out _, out _, out float f0))
        {
            if (originFlare == null) originFlare = CreateFlare(transform, l0);
            float tail = (st.tailToTipFade && f0 < 0.999f) ? 0f : 1f; // 発射口側から消えるので、終わり始めたら真っ先に消す
            Color c0 = (decors.TryGetValue(l0, out Decor d0) && d0.baseColors != null && d0.baseColors.Length > 0) ? d0.baseColors[0].color : Color.white;
            SetFlare(originFlare, l0.GetPosition(0), l0.startWidth * 1.3f, c0, 0, (st.tailToTipFade ? tail : f0) * pulse);
        }

        // 火の粉：ビーム全体のどこかから
        if (embers != null && totalLen > 0.01f)
        {
            emberAcc += st.emberRate * dt;
            while (emberAcc >= 1f)
            {
                emberAcc -= 1f;
                Vector3 p = RandomPointOnBeam(n, totalLen);
                var ep = new ParticleSystem.EmitParams { position = p, applyShapeToPosition = true };
                embers.Emit(ep, 1);
            }
        }

        UpdateFlashes(dt);
        PruneStale();
    }

    // ---------- 区間の重ね物 ----------
    private Decor CreateDecor(LineRenderer line)
    {
        var d = new Decor();
        int baseOrder = line.sortingOrder;
        if (st.useHaze && st.hazeMaterial != null) d.haze = NewLine(line, "Haze", ScrollMat(st.hazeMaterial), baseOrder - 2, true);
        if (st.useGlow && st.softLineMaterial != null) d.glow = NewLine(line, "Glow", st.softLineMaterial, baseOrder - 1, false);
        if (st.useFlow && st.flowMaterial != null) d.flow = NewLine(line, "Flow", ScrollMat(st.flowMaterial), baseOrder + 1, true);
        if (st.useCore && st.softLineMaterial != null) d.core = NewLine(line, "Core", st.softLineMaterial, baseOrder + 2, false);
        if (st.useLightning && st.softLineMaterial != null && st.boltCount > 0)
        {
            d.bolts = new LineRenderer[st.boltCount];
            for (int k = 0; k < st.boltCount; k++)
                d.bolts[k] = NewLine(line, "Bolt", st.softLineMaterial, baseOrder + 3, false);
        }
        if (st.useFlares) d.endFlare = CreateFlare(line.transform, line);
        return d;
    }

    private static LineRenderer NewLine(LineRenderer src, string name, Material mat, int order, bool tile)
    {
        var go = new GameObject(name);
        go.transform.SetParent(src.transform, false);
        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.positionCount = 2;
        lr.sharedMaterial = mat;
        lr.sortingLayerID = src.sortingLayerID;
        lr.sortingOrder = order;
        lr.numCapVertices = 4;
        lr.numCornerVertices = 2;
        lr.alignment = LineAlignment.View;
        lr.textureMode = tile ? LineTextureMode.Tile : LineTextureMode.Stretch;
        return lr;
    }

    private void SetLine(Decor d, LineRenderer lr, Vector3 a, Vector3 b, float width, Color c, float alpha)
    {
        if (lr == null) return;
        lr.SetPosition(0, a);
        lr.SetPosition(1, b);
        lr.startWidth = lr.endWidth = width;
        ApplyGradient(d, lr, c, alpha);
    }

    // 本体の線と同じ不透明度（照射の終わりのフェードを含む）を、色cとalphaで掛けて設定する
    private static void ApplyGradient(Decor d, LineRenderer lr, Color c, float alpha)
    {
        if (!d.grads.TryGetValue(lr, out Gradient g)) { g = new Gradient(); d.grads[lr] = g; }
        s_twoColor[0] = new GradientColorKey(c, 0f);
        s_twoColor[1] = new GradientColorKey(c, 1f);
        for (int k = 0; k < 4; k++) s_alphaScratch[k] = new GradientAlphaKey(s_alpha4[k].alpha * alpha * c.a, s_alpha4[k].time);
        g.SetKeys(s_twoColor, s_alphaScratch);
        lr.colorGradient = g;
    }

    // 照射の終わり（tailP：発射口からの消えた割合）と区間の範囲から、区間に沿った不透明度の4点を作る（s_alpha4に入れる）
    private static void BuildAlphaKeys(float tailP, Vector2 range, float a)
    {
        float span = Mathf.Max(0.0001f, range.y - range.x);
        if (tailP <= range.x)
        {
            s_alpha4[0] = new GradientAlphaKey(a, 0f); s_alpha4[1] = new GradientAlphaKey(a, 0.33f);
            s_alpha4[2] = new GradientAlphaKey(a, 0.66f); s_alpha4[3] = new GradientAlphaKey(a, 1f);
        }
        else if (tailP >= range.y)
        {
            s_alpha4[0] = new GradientAlphaKey(0f, 0f); s_alpha4[1] = new GradientAlphaKey(0f, 0.33f);
            s_alpha4[2] = new GradientAlphaKey(0f, 0.66f); s_alpha4[3] = new GradientAlphaKey(0f, 1f);
        }
        else
        {
            float k = Mathf.Clamp01((tailP - range.x) / span);
            float k2 = Mathf.Min(1f, k + 0.08f);
            s_alpha4[0] = new GradientAlphaKey(0f, 0f); s_alpha4[1] = new GradientAlphaKey(0f, Mathf.Max(0.001f, k));
            s_alpha4[2] = new GradientAlphaKey(a, Mathf.Max(k + 0.001f, k2)); s_alpha4[3] = new GradientAlphaKey(a, 1f);
        }
    }

    // 発射口（0番の区間）から続き（next）をたどって、各区間の「全体の長さに対する始点・終点の割合」を求める
    private void ComputeChainRanges(int n)
    {
        chainRange.Clear();
        if (!st.tailToTipFade || n == 0) return;
        float total = 0f;
        int idx = 0, guard = 0;
        var order = new List<(int, float)>();
        while (idx >= 0 && idx < n && guard++ < 64)
        {
            if (!beam.GetVisualSegment(idx, out LineRenderer l, out _, out _, out _, out _)) break;
            float len = Vector3.Distance(l.GetPosition(0), l.GetPosition(1));
            order.Add((idx, len));
            total += len;
            idx = beam.GetNextVisualSegmentIndex(idx);
        }
        if (total <= 0.0001f) return;
        float acc = 0f;
        foreach (var (i, len) in order)
        {
            chainRange[i] = new Vector2(acc / total, (acc + len) / total);
            acc += len;
        }
    }

    private void DrawBolt(LineRenderer bolt, Vector3 a, Vector3 b)
    {
        Vector3 ab = b - a;
        float len = ab.magnitude;
        int pts = Mathf.Clamp(Mathf.CeilToInt(len / Mathf.Max(0.05f, st.boltSegmentLength)) + 1, 3, 32);
        bolt.positionCount = pts;
        Vector3 dir = len > 0.0001f ? ab / len : Vector3.right;
        Vector3 perp = new Vector3(-dir.y, dir.x, 0f);
        for (int k = 0; k < pts; k++)
        {
            float t = k / (float)(pts - 1);
            float j = (k == 0 || k == pts - 1) ? 0f : Random.Range(-st.boltJitter, st.boltJitter) * Mathf.Sin(t * Mathf.PI);
            bolt.SetPosition(k, a + ab * t + perp * j);
        }
    }

    // ---------- フレア・閃光 ----------
    private SpriteRenderer CreateFlare(Transform parent, LineRenderer src)
    {
        if (st.flareSprite == null) return null;
        var go = new GameObject("Flare");
        go.transform.SetParent(parent, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = st.flareSprite;
        if (st.flareMaterial != null) sr.sharedMaterial = st.flareMaterial;
        sr.sortingLayerID = src.sortingLayerID;
        sr.sortingOrder = src.sortingOrder + 4;
        return sr;
    }

    private void SetFlare(SpriteRenderer sr, Vector3 pos, float beamWidth, Color c, int seed, float alpha)
    {
        if (sr == null) return;
        sr.transform.position = pos;
        float spriteSize = sr.sprite != null ? Mathf.Max(0.01f, sr.sprite.bounds.size.x) : 1f;
        float pulse = 1f + 0.2f * Mathf.Sin(elapsed * st.flarePulseSpeed + seed * 2.1f);
        float size = beamWidth * st.flareSizeMul * pulse / spriteSize;
        sr.transform.localScale = Vector3.one * size;
        sr.transform.rotation = Quaternion.Euler(0f, 0f, elapsed * 90f + seed * 37f);
        Color fc = Color.Lerp(c, Color.white, 0.5f);
        fc.a = st.flareAlpha * Mathf.Clamp01(alpha);
        sr.color = fc;
    }

    private void TryReflectFlash(Vector3 pos, float beamWidth)
    {
        float now = Time.time;
        for (int i = recentFlashes.Count - 1; i >= 0; i--)
        {
            if (now - recentFlashes[i].time > 0.3f) { recentFlashes.RemoveAt(i); continue; }
            if (Vector3.Distance(recentFlashes[i].pos, pos) < 0.6f) return; // 同じ場所で続けて光らせない（掃射中の作り直し対策）
        }
        recentFlashes.Add((pos, now));
        if (st.flareSprite == null) return;
        Flash f = null;
        foreach (var x in flashes) if (x.t < 0f) { f = x; break; }
        if (f == null)
        {
            if (flashes.Count >= 4) return;
            var go = new GameObject("ReflectFlash");
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = st.flareSprite;
            if (st.flareMaterial != null) sr.sharedMaterial = st.flareMaterial;
            sr.sortingLayerName = "Default";
            sr.sortingOrder = 1050;
            f = new Flash { sr = sr };
            flashes.Add(f);
        }
        f.t = 0f;
        f.size = beamWidth * st.reflectFlashSizeMul;
        f.sr.transform.position = pos;
        f.sr.enabled = true;
    }

    private void UpdateFlashes(float dt)
    {
        foreach (var f in flashes)
        {
            if (f.t < 0f || f.sr == null) continue;
            f.t += dt;
            float k = f.t / Mathf.Max(0.01f, st.reflectFlashDuration);
            if (k >= 1f) { f.t = -1f; f.sr.enabled = false; continue; }
            float spriteSize = f.sr.sprite != null ? Mathf.Max(0.01f, f.sr.sprite.bounds.size.x) : 1f;
            f.sr.transform.localScale = Vector3.one * (f.size * Mathf.Lerp(0.4f, 1.2f, 1f - (1f - k) * (1f - k)) / spriteSize);
            f.sr.color = new Color(1f, 1f, 1f, 1f - k);
        }
    }

    // ---------- その他 ----------
    private Vector3 RandomPointOnBeam(int n, float totalLen)
    {
        float r = Random.value * totalLen;
        for (int i = 0; i < n; i++)
        {
            if (!beam.GetVisualSegment(i, out LineRenderer l, out _, out _, out _, out _)) continue;
            Vector3 a = l.GetPosition(0), b = l.GetPosition(1);
            float len = Vector3.Distance(a, b);
            if (r <= len) return Vector3.Lerp(a, b, len > 0f ? r / len : 0f);
            r -= len;
        }
        return transform.position;
    }

    private Material ScrollMat(Material template)
    {
        if (!s_scrollMats.TryGetValue(template, out Material m) || m == null)
        {
            m = new Material(template) { name = template.name + " (Beam Runtime)" };
            s_scrollMats[template] = m;
        }
        return m;
    }

    // 模様を流す時計は全ビーム共通で1フレームに1回だけ進め、自分のスタイルのマテリアルには毎フレーム反映する
    private void AdvanceFlowClock()
    {
        if (s_flowClockFrame != Time.frameCount)
        {
            s_flowClockFrame = Time.frameCount;
            s_flowClock += SlowMoTime.DeltaTime;
        }
        if (st.useFlow && st.flowMaterial != null)
        {
            var m = ScrollMat(st.flowMaterial);
            m.mainTextureScale = new Vector2(1f / Mathf.Max(0.05f, st.flowTileLength), 1f);
            m.mainTextureOffset = new Vector2(-s_flowClock * st.flowSpeed, 0f);
        }
        if (st.useHaze && st.hazeMaterial != null)
        {
            var m = ScrollMat(st.hazeMaterial);
            m.mainTextureScale = new Vector2(1f / Mathf.Max(0.05f, st.hazeTileLength), 1f);
            m.mainTextureOffset = new Vector2(-s_flowClock * st.hazeSpeed, 0f);
        }
    }

    private void PruneStale()
    {
        staleKeys.Clear();
        foreach (var kv in decors) if (kv.Key == null) staleKeys.Add(kv.Key);
        foreach (var k in staleKeys) decors.Remove(k);
    }
}
