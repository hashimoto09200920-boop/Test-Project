using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tsukuyomi・NeonDancerのドリル弾（通常・強化）を派手にする演出の共有の管理役（DrillFX.prefabのルートに付ける）。
/// パーティクル（ダイヤモンドダスト・削りの火花・破片）は画面に1つずつだけ置いたものから各ドリルの位置に粒を出し、
/// 画像（螺旋の輪・輝き・オーラ・残像）と稲妻の線は使い回す（ドリル1発ごとに作ったり消したりしない）。
/// 弾1発ごとの部品DrillFXがこの管理役に演出を頼む。最初に使われた時にDrillFX.prefabから1つだけ作られる。
/// 設定は全てPlay前にDrillFX.prefabのInspectorで調整できる（メニュー「Tools/ドリルの見た目/…」で作成）。
/// </summary>
[DisallowMultipleComponent]
public class DrillFXManager : MonoBehaviour
{
    [Header("共有のパーティクル（メニューで自動設定）")]
    public ParticleSystem dust;
    public ParticleSystem sparks;
    public ParticleSystem shards;

    [Header("画像・マテリアル（メニューで自動設定）")]
    public Sprite ringSprite;
    public Sprite glowSprite;
    public Sprite sigilSprite;
    [Tooltip("画像用の加算マテリアル")]
    public Material spriteMaterial;
    [Tooltip("稲妻の線のマテリアル（加算）")]
    public Material boltMaterial;

    [Header("① 螺旋の風切り（飛んでいる間）")]
    public int ringCount = 2;
    [Tooltip("輪が切っ先から後ろへ流れて消えるまでの秒数")]
    public float ringCycleSeconds = 0.3f;
    [Tooltip("輪の幅（弾の大きさに対する倍率）")]
    public float ringWidthMul = 1.3f;
    [Tooltip("輪のつぶれ具合（進む方向の厚み。小さいほど薄い楕円）")]
    [Range(0.05f, 1f)] public float ringThickness = 0.3f;
    [Tooltip("輪が流れる長さ（弾の大きさに対する倍率）")]
    public float ringTravelMul = 1.6f;
    public Color ringColor = new Color(0.7f, 0.9f, 1f, 1f);
    [Range(0f, 1f)] public float ringAlpha = 0.7f;

    [Header("② ダイヤモンドダスト（飛んでいる間）")]
    [Tooltip("1秒に出す粒の数")]
    public float dustRate = 28f;
    public Color dustColor = new Color(0.8f, 0.92f, 1f, 1f);

    [Header("③ 削りの火花と震え（刺さっている間）")]
    [Tooltip("1秒に出す火花の数")]
    public float sparkRate = 45f;
    public float sparkSpeed = 4f;
    [Tooltip("火花が飛び散る広がり（度）")]
    public float sparkSpreadDeg = 70f;
    public Color sparkColor = new Color(0.75f, 0.92f, 1f, 1f);
    [Tooltip("刺さっている間の震えの大きさ（ワールド単位）")]
    public float shakeAmount = 0.035f;
    [Tooltip("ヒットのたびに広がる光の輪の大きさ（弾の大きさに対する倍率）")]
    public float hitRingSizeMul = 2.6f;
    public float hitRingDuration = 0.25f;

    [Header("④ 溜まっていく輝き（刺さっている間）")]
    public float glowSizeMul = 2.4f;
    [Range(0f, 1f)] public float glowMaxAlpha = 0.95f;
    public Color glowColor = new Color(0.6f, 0.85f, 1f, 1f);

    [Header("⑤ 砕け散る（消える時・刺さった所から抜けた時）")]
    public int shardCountOnVanish = 14;
    public int shardCountOnRelease = 6;
    public Color shardColor = new Color(0.75f, 0.9f, 1f, 1f);

    [Header("⑥ 紅いオーラ（強化ドリル）")]
    public Color auraColor = new Color(1f, 0.2f, 0.25f, 1f);
    public float auraSizeMul = 2.8f;
    public float auraPulseSpeed = 7f;
    [Range(0f, 1f)] public float auraAlphaMin = 0.35f;
    [Range(0f, 1f)] public float auraAlphaMax = 0.8f;

    [Header("⑦ 出現時の紋章（強化ドリル）")]
    [Tooltip("紋章の大きさ（ワールド単位の直径）")]
    public float sigilSize = 2.4f;
    public float sigilDuration = 0.9f;
    public Color sigilColor = new Color(1f, 0.25f, 0.3f, 1f);
    public int sigilSparkleCount = 18;

    [Header("⑧ 赤い残像（強化ドリル）")]
    public int afterimageCount = 3;
    [Tooltip("残像1体あたりの遅れ（秒）")]
    public float afterimageDelay = 0.045f;
    [Range(0f, 1f)] public float afterimageAlpha = 0.5f;
    public Color afterimageColor = new Color(1f, 0.3f, 0.35f, 1f);

    [Header("⑨ 紅い稲妻（強化ドリルが刺さっている間）")]
    public int boltCount = 2;
    public Vector2 boltLength = new Vector2(0.5f, 1.1f);
    public float boltWidth = 0.035f;
    public float boltRefreshInterval = 0.06f;
    public Color boltColor = new Color(1f, 0.35f, 0.4f, 1f);

    // ---------- 共有インスタンス ----------
    private static DrillFXManager s_instance;

    /// <summary>シーンに1つだけの管理役を返す（無ければprefabから作る）</summary>
    public static DrillFXManager GetOrCreate(DrillFXManager prefab)
    {
        if (s_instance != null) return s_instance;
        if (prefab == null) return null;
        s_instance = Instantiate(prefab);
        s_instance.name = prefab.name;
        return s_instance;
    }

    private ParticleSystem[] particleList;
    private float lastParticleSpeed = float.NaN;

    private readonly Stack<SpriteRenderer> freeSprites = new Stack<SpriteRenderer>();
    private readonly Stack<LineRenderer> freeLines = new Stack<LineRenderer>();

    private class Anim
    {
        public SpriteRenderer sr;
        public float t, duration, startSize, endSize, spin;
        public Color color;
    }
    private readonly List<Anim> anims = new List<Anim>();

    private void Awake()
    {
        particleList = new[] { dust, sparks, shards };
        foreach (var ps in particleList)
        {
            if (ps == null) continue;
            var em = ps.emission; em.enabled = false; // 全てEmitで出す
            if (!ps.isPlaying) ps.Play(false);
        }
    }

    private void OnDestroy()
    {
        if (s_instance == this) s_instance = null;
    }

    private void LateUpdate()
    {
        SlowMoTime.ParticleSpeed(particleList, ref lastParticleSpeed);
        float dt = SlowMoTime.DeltaTime;
        for (int i = anims.Count - 1; i >= 0; i--)
        {
            var a = anims[i];
            if (a.sr == null) { anims.RemoveAt(i); continue; }
            a.t += dt;
            float k = a.t / Mathf.Max(0.01f, a.duration);
            if (k >= 1f) { ReleaseSprite(a.sr); anims.RemoveAt(i); continue; }
            float eased = 1f - (1f - k) * (1f - k);
            float size = Mathf.Lerp(a.startSize, a.endSize, eased);
            SetSpriteSize(a.sr, size);
            a.sr.transform.rotation = Quaternion.Euler(0f, 0f, a.t * a.spin);
            Color c = a.color; c.a *= 1f - k;
            a.sr.color = c;
        }
    }

    // ---------- 画像・線の貸し出し ----------
    public SpriteRenderer RentSprite(Sprite sprite, int sortingLayerId, int sortingOrder)
    {
        SpriteRenderer sr = null;
        while (freeSprites.Count > 0 && sr == null) sr = freeSprites.Pop();
        if (sr == null)
        {
            var go = new GameObject("DrillFX_Sprite");
            go.transform.SetParent(transform, false);
            sr = go.AddComponent<SpriteRenderer>();
        }
        // 残像は弾のマテリアルに差し替えて使うため、借りるたびに加算マテリアルに戻す
        if (spriteMaterial != null) sr.sharedMaterial = spriteMaterial;
        sr.sprite = sprite;
        sr.sortingLayerID = sortingLayerId;
        sr.sortingOrder = sortingOrder;
        sr.transform.rotation = Quaternion.identity;
        sr.gameObject.SetActive(true);
        sr.enabled = true;
        return sr;
    }

    public void ReleaseSprite(SpriteRenderer sr)
    {
        if (sr == null) return;
        sr.enabled = false;
        sr.gameObject.SetActive(false);
        freeSprites.Push(sr);
    }

    public LineRenderer RentLine(int sortingLayerId, int sortingOrder)
    {
        LineRenderer lr = null;
        while (freeLines.Count > 0 && lr == null) lr = freeLines.Pop();
        if (lr == null)
        {
            var go = new GameObject("DrillFX_Bolt");
            go.transform.SetParent(transform, false);
            lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.numCapVertices = 2;
            lr.alignment = LineAlignment.View;
            if (boltMaterial != null) lr.sharedMaterial = boltMaterial;
        }
        lr.sortingLayerID = sortingLayerId;
        lr.sortingOrder = sortingOrder;
        lr.gameObject.SetActive(true);
        return lr;
    }

    public void ReleaseLine(LineRenderer lr)
    {
        if (lr == null) return;
        lr.gameObject.SetActive(false);
        freeLines.Push(lr);
    }

    public static void SetSpriteSize(SpriteRenderer sr, float worldSize)
    {
        float s = sr.sprite != null ? Mathf.Max(0.01f, sr.sprite.bounds.size.x) : 1f;
        sr.transform.localScale = Vector3.one * (worldSize / s);
    }

    // ---------- 単発の演出 ----------
    /// <summary>ヒットのたびに広がる光の輪</summary>
    public void SpawnHitRing(Vector3 pos, float bulletSize, Color color, int layerId, int order)
    {
        if (ringSprite == null) return;
        var sr = RentSprite(ringSprite, layerId, order);
        sr.transform.position = pos;
        anims.Add(new Anim { sr = sr, duration = hitRingDuration, startSize = bulletSize * 0.6f, endSize = bulletSize * hitRingSizeMul, color = color, spin = 0f });
    }

    /// <summary>強化ドリルの出現時の紋章</summary>
    public void SpawnSigil(Vector3 pos, int layerId, int order)
    {
        if (sigilSprite != null)
        {
            var sr = RentSprite(sigilSprite, layerId, order);
            sr.transform.position = pos;
            anims.Add(new Anim { sr = sr, duration = sigilDuration, startSize = sigilSize * 0.4f, endSize = sigilSize, color = sigilColor, spin = 120f });
        }
        EmitBurst(sparks, pos, sigilSparkleCount, sigilColor, 2.5f, 0f, 360f);
    }

    /// <summary>砕け散る破片</summary>
    public void SpawnShards(Vector3 pos, int count, Color color)
    {
        EmitBurst(shards, pos, count, color, 3f, 0f, 360f);
    }

    public void EmitDust(Vector3 pos, Color color)
    {
        if (dust == null) return;
        var ep = new ParticleSystem.EmitParams { position = pos, applyShapeToPosition = true, startColor = color };
        dust.Emit(ep, 1);
    }

    /// <summary>baseAngleDegを中心に±spread/2の範囲へ、speedで飛ばす</summary>
    public void EmitBurst(ParticleSystem ps, Vector3 pos, int count, Color color, float speed, float baseAngleDeg, float spreadDeg)
    {
        if (ps == null || count <= 0) return;
        var ep = new ParticleSystem.EmitParams { position = pos, startColor = color };
        for (int i = 0; i < count; i++)
        {
            float ang = (baseAngleDeg + Random.Range(-spreadDeg * 0.5f, spreadDeg * 0.5f)) * Mathf.Deg2Rad;
            ep.velocity = new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), 0f) * speed * Random.Range(0.5f, 1.2f);
            ps.Emit(ep, 1);
        }
    }
}
