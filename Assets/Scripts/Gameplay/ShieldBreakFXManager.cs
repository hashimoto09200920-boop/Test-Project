using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// エネミーのシールドが壊れた時の演出の管理役。Assets/Resources/ShieldFX.prefab から1つだけ作られる
/// （プレハブが無ければ何もしない＝従来のVFX_ShieldBreak。メニュー「Tools/弾の見た目/6 …」で作成）。
/// EnemyShield.BreakShield から呼ばれ、出せた時は旧VFX（EnemyData > Shield Break Effect Prefab）を出さない。
/// ・① 泡の大きさ・位置に合わせる ② 泡が膨らんで割れる ③ ガラス片 ④ ひび割れ ⑤ 閃光と二重の衝撃波リング
///   ⑥ 光の粒 ⑧ 画面の小さな揺れ ⑩ 使い回し（⑨ 割れている間の漂う破片はユーザー指示で削除）（生成・破棄なし）
/// ・シールドがある間の泡（VFX_ShieldActive）の演出は ShieldActiveFX（泡のプレハブに付ける部品）が担当する
/// ・負荷対策：パーティクルは共有（Emitで出す）、画像・線・文字は貸し出しで使い回し、1フレームの数と同時数に上限
/// </summary>
[DisallowMultipleComponent]
public class ShieldBreakFXManager : MonoBehaviour
{
    [Header("全体")]
    [Tooltip("OFFにすると旧VFX（VFX_ShieldBreak）に戻る")]
    public bool fxEnabled = true;
    [Tooltip("1フレームに出すシールド破壊演出の上限")]
    public int maxBreaksPerFrame = 2;

    [Header("共有のパーティクル・画像（メニューで自動設定）")]
    public ParticleSystem sparkle;
    public Sprite glowSprite;
    public Sprite ringSprite;
    public Sprite shardSprite;
    [Tooltip("加算の画像用マテリアル")]
    public Material additiveSpriteMaterial;
    [Tooltip("線（ひび）のマテリアル（加算）")]
    public Material lineMaterial;

    [Header("① 泡の大きさ・位置")]
    [Tooltip("Shield Active Effect Scale が1の時の泡の半径（VFX_ShieldActiveの光の粒の円が半径1）")]
    public float shieldRadiusPerScale = 1f;
    [Tooltip("並び順（エネミーの絵より手前に出す量）")]
    public int sortingOrderOffset = 5;

    [Header("色")]
    public Color shieldColor = new Color(0.45f, 0.8f, 1f, 1f);
    public Color shieldColor2 = new Color(0.75f, 0.95f, 1f, 1f);

    [Header("② 泡が膨らんで割れる")]
    public bool popEnabled = true;
    public float popDuration = 0.12f;
    public float popScale = 1.25f;

    [Header("③ ガラス片")]
    public bool shardsEnabled = true;
    public int shardCount = 14;
    [Tooltip("破片の大きさ（泡の半径に対する割合）")]
    public float shardSizeMul = 0.22f;
    [Tooltip("破片の速さ（泡の半径1あたり）")]
    public float shardSpeed = 4f;
    public float shardDuration = 0.55f;
    public float shardGravity = 3f;

    [Header("④ ひび割れ")]
    public bool cracksEnabled = true;
    public int crackCount = 6;
    public float crackWidth = 0.05f;
    public float crackDuration = 0.16f;

    [Header("⑤ 閃光と二重の衝撃波リング")]
    public bool flashRingEnabled = true;
    public float flashDuration = 0.1f;
    public float ringDuration = 0.3f;
    [Tooltip("外側のリングがどこまで広がるか（泡の半径に対する倍率）")]
    public float ringReach = 1.9f;

    [Header("⑥ 光の粒")]
    public bool particlesEnabled = true;
    public int particleCount = 24;
    public float particleSpeed = 3f;
    public float particleSize = 0.08f;

    [Header("⑧ 画面の小さな揺れ")]
    public bool shakeEnabled = true;
    public float shakeDuration = 0.12f;
    public float shakeMagnitude = 0.06f;
    public float shakeMinInterval = 0.3f;

    // ---------- 共有インスタンス ----------
    private static ShieldBreakFXManager s_instance;
    private static bool s_triedLoad;

    public static ShieldBreakFXManager Instance
    {
        get
        {
            if (s_instance != null) return s_instance;
            if (s_triedLoad) return null;
            s_triedLoad = true;
            var prefab = Resources.Load<ShieldBreakFXManager>("ShieldFX");
            if (prefab == null) return null;
            s_instance = Instantiate(prefab);
            s_instance.name = "ShieldFX";
            return s_instance;
        }
    }

    /// <summary>シールドが壊れた時（EnemyShield.BreakShield）。演出を出したらtrue（旧VFXは出さない）</summary>
    public static bool TryPlay(EnemyShield shield)
    {
        if (shield == null) return false;
        var m = Instance;
        if (m == null || !m.fxEnabled) return false;
        m.Play(shield);
        return true;
    }

    // ---------- 内部 ----------
    private class Anim { public SpriteRenderer sr; public LineRenderer lr; public float t, dur, s0, s1; public Color c; }
    private class Shard { public SpriteRenderer sr; public Vector3 pos, vel; public float rot, spin, t, dur, size; public Color c; }

    private readonly List<Anim> anims = new List<Anim>();
    private readonly List<Shard> shards = new List<Shard>();
    private readonly Stack<SpriteRenderer> freeSprites = new Stack<SpriteRenderer>();
    private readonly Stack<LineRenderer> freeLines = new Stack<LineRenderer>();
    private ParticleSystem[] particleList;
    private float lastParticleSpeed = float.NaN;
    private int breakFrame = -1, breakCount;
    private float lastShakeTime = -999f;
    private static readonly Vector3[] s_crackPts = new Vector3[5];

    private void Awake()
    {
        if (s_instance == null) s_instance = this;
        particleList = new[] { sparkle };
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

    private void Play(EnemyShield shield)
    {
        if (breakFrame != Time.frameCount) { breakFrame = Time.frameCount; breakCount = 0; }
        if (breakCount >= maxBreaksPerFrame) return;
        breakCount++;

        // ① 泡の大きさ・位置
        Vector3 c = shield.EffectCenter;
        float r = Mathf.Max(0.2f, shield.EffectScale * shieldRadiusPerScale);
        var esr = shield.GetComponentInChildren<SpriteRenderer>();
        int layer = esr != null ? esr.sortingLayerID : 0, order = (esr != null ? esr.sortingOrder : 0) + sortingOrderOffset;
        const float RingDiameterPerRadius = 2.5f; // リング画像の輪は直径の0.4倍の半径

        // ② 泡が膨らんで割れる（泡の輪が膨らみながら白く光って消える＋淡い面）
        if (popEnabled)
        {
            AddSprite(ringSprite, c, r * RingDiameterPerRadius, r * RingDiameterPerRadius * popScale, popDuration, Color.Lerp(shieldColor, Color.white, 0.6f), layer, order);
            AddSprite(glowSprite, c, r * 2f, r * 2f * popScale, popDuration, new Color(shieldColor2.r, shieldColor2.g, shieldColor2.b, 0.45f), layer, order - 1);
        }
        // ⑤ 閃光と二重の衝撃波リング
        if (flashRingEnabled)
        {
            AddSprite(glowSprite, c, r * 1.2f, r * 1.6f, flashDuration, Color.white, layer, order + 2);
            AddSprite(ringSprite, c, r * RingDiameterPerRadius, r * RingDiameterPerRadius * ringReach, ringDuration, shieldColor, layer, order + 1);
            AddSprite(ringSprite, c, r * RingDiameterPerRadius * 0.3f, r * RingDiameterPerRadius * 1.3f, ringDuration * 0.8f, new Color(1f, 1f, 1f, 0.75f), layer, order + 1);
        }
        // ④ ひび割れ
        if (cracksEnabled && lineMaterial != null)
        {
            float off = Random.Range(0f, 360f);
            for (int i = 0; i < crackCount; i++)
            {
                float a = (off + i * 360f / Mathf.Max(1, crackCount) + Random.Range(-15f, 15f)) * Mathf.Deg2Rad;
                Vector3 d = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * r * Random.Range(0.75f, 1f);
                Vector3 n = new Vector3(-d.y, d.x, 0f).normalized * d.magnitude * 0.12f;
                for (int k = 0; k < s_crackPts.Length; k++)
                {
                    float kk = k / (float)(s_crackPts.Length - 1);
                    s_crackPts[k] = c + d * kk + (k == 0 ? Vector3.zero : n * Random.Range(-1f, 1f));
                }
                var lr = RentLine(layer, order + 2);
                lr.positionCount = s_crackPts.Length;
                lr.SetPositions(s_crackPts);
                anims.Add(new Anim { lr = lr, dur = crackDuration, c = Color.Lerp(shieldColor2, Color.white, 0.5f) });
            }
        }
        // ③ ガラス片（泡の円周から外へ）
        if (shardsEnabled && shardSprite != null)
        {
            for (int i = 0; i < shardCount; i++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f);
                Vector3 dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                var sr = RentSprite(shardSprite, layer, order + 1);
                shards.Add(new Shard
                {
                    sr = sr, pos = c + dir * r * Random.Range(0.8f, 1f), vel = dir * shardSpeed * r * Random.Range(0.5f, 1.1f),
                    rot = Random.Range(0f, 360f), spin = Random.Range(-720f, 720f), dur = shardDuration * Random.Range(0.75f, 1.15f),
                    size = r * shardSizeMul * Random.Range(0.6f, 1.3f), c = Color.Lerp(shieldColor, shieldColor2, Random.value),
                });
            }
        }
        // ⑥ 光の粒（円周から外へ）
        if (particlesEnabled && sparkle != null)
        {
            var ep = new ParticleSystem.EmitParams { startSize = particleSize };
            for (int i = 0; i < particleCount; i++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f);
                Vector3 dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                ep.position = c + dir * r;
                ep.velocity = dir * particleSpeed * Random.Range(0.4f, 1.1f);
                ep.startColor = Color.Lerp(shieldColor, Color.white, Random.Range(0.2f, 0.7f));
                sparkle.Emit(ep, 1);
            }
        }
        // ⑧ 画面の小さな揺れ
        if (shakeEnabled && Time.unscaledTime - lastShakeTime >= shakeMinInterval)
        {
            lastShakeTime = Time.unscaledTime;
            CameraShake.Shake(shakeDuration, shakeMagnitude);
        }
    }

    // ---------- 毎フレーム ----------
    private void LateUpdate()
    {
        SlowMoTime.ParticleSpeed(particleList, ref lastParticleSpeed);
        float dt = SlowMoTime.DeltaTime;

        for (int i = anims.Count - 1; i >= 0; i--)
        {
            var a = anims[i];
            a.t += dt;
            float k = a.t / Mathf.Max(0.01f, a.dur);
            if (k >= 1f)
            {
                if (a.sr != null) ReleaseSprite(a.sr);
                if (a.lr != null) ReleaseLine(a.lr);
                anims.RemoveAt(i);
                continue;
            }
            float eased = 1f - (1f - k) * (1f - k);
            Color col = a.c; col.a *= 1f - k;
            if (a.sr != null)
            {
                float s = Mathf.Lerp(a.s0, a.s1, eased);
                SetSize(a.sr, s, s);
                a.sr.color = col;
            }
            if (a.lr != null)
            {
                a.lr.startWidth = a.lr.endWidth = crackWidth * (1f - k * 0.5f);
                a.lr.startColor = col; a.lr.endColor = new Color(col.r, col.g, col.b, col.a * 0.4f);
            }
        }

        for (int i = shards.Count - 1; i >= 0; i--)
        {
            var s = shards[i];
            s.t += dt;
            float k = s.t / Mathf.Max(0.01f, s.dur);
            if (k >= 1f) { ReleaseSprite(s.sr); shards.RemoveAt(i); continue; }
            s.vel += Vector3.down * shardGravity * dt;
            s.vel *= 1f - Mathf.Clamp01(1.5f * dt);
            s.pos += s.vel * dt;
            s.rot += s.spin * dt;
            s.sr.transform.SetPositionAndRotation(s.pos, Quaternion.Euler(0f, 0f, s.rot));
            SetSize(s.sr, s.size, s.size);
            Color col = s.c; col.a = 1f - k * k;
            s.sr.color = col;
        }

    }

    // ---------- 共通 ----------
    private void AddSprite(Sprite sprite, Vector3 pos, float s0, float s1, float dur, Color c, int layer, int order)
    {
        if (sprite == null) return;
        var sr = RentSprite(sprite, layer, order);
        sr.transform.SetPositionAndRotation(pos, Quaternion.identity);
        anims.Add(new Anim { sr = sr, dur = dur, s0 = s0, s1 = s1, c = c });
    }

    private static void SetSize(SpriteRenderer sr, float w, float h)
    {
        float s = sr.sprite != null ? Mathf.Max(0.01f, sr.sprite.bounds.size.x) : 1f;
        sr.transform.localScale = new Vector3(w / s, h / s, 1f);
    }

    private SpriteRenderer RentSprite(Sprite sprite, int layer, int order)
    {
        SpriteRenderer sr = null;
        while (freeSprites.Count > 0 && sr == null) sr = freeSprites.Pop();
        if (sr == null)
        {
            var go = new GameObject("ShieldFX_Sprite");
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

    private LineRenderer RentLine(int layer, int order)
    {
        LineRenderer lr = null;
        while (freeLines.Count > 0 && lr == null) lr = freeLines.Pop();
        if (lr == null)
        {
            var go = new GameObject("ShieldFX_Line");
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
