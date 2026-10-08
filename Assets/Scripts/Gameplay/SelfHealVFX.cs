using System.Collections;
using UnityEngine;

/// <summary>
/// スキルC3「セルフリストア」（Skill_C3_SelfHeal）で回復した時のエフェクト。
/// プレイヤー（PixelDancerController）と床（FloorHealth）にそれぞれ1つずつ子として常駐させ、回復のたびにPlay()で粒を出す
/// （回復のたびにプレハブを作って消すことはしない。メニュー「Tools/回復エフェクト/…」で作成・設定）。
/// ・プレイヤー：② 光の柱 ③ 立ちのぼる光の粒 ④ 十字のきらめき ⑤ 体がほのかに光る（同じ絵を加算で重ねる。本体の色は触らない）
/// ・床　　　　：① 床の楕円に沿った光の輪 ⑥ 中央から左右へ走る光の波 ③ 立ちのぼる光の粒 ④ 十字のきらめき
/// 色は従来の回復エフェクト（HealVfxColorizer）と同じ緑・水色・白。各層の形・寿命は子のParticle System、数・時間はここで調整する。
/// </summary>
[DisallowMultipleComponent]
public class SelfHealVFX : MonoBehaviour
{
    public enum Target { Player, Floor }

    [Header("対象")]
    [SerializeField] private Target target = Target.Player;
    [Tooltip("位置と大きさの基準（プレイヤー＝体の絵、床＝床の絵）。⑤の体の発光はこの絵を写す")]
    [SerializeField] private SpriteRenderer source;

    [Header("Layers（子のParticle System。メニューで自動設定。使わない層は空でよい）")]
    [SerializeField] private ParticleSystem pillar;     // ② 光の柱（プレイヤー）
    [SerializeField] private ParticleSystem motes;      // ③ 立ちのぼる光の粒
    [SerializeField] private ParticleSystem crosses;    // ④ 十字のきらめき
    [SerializeField] private ParticleSystem floorRing;  // ① 床の光の輪（床）
    [SerializeField] private ParticleSystem wave;       // ⑥ 左右へ走る光の波（床）
    [Tooltip("⑤ 体がほのかに光る（プレイヤー）。sourceの絵を写す加算のSpriteRenderer")]
    [SerializeField] private SpriteRenderer bodyGlow;

    [Header("数")]
    [SerializeField] private int moteCount = 18;
    [SerializeField] private int crossCount = 5;
    [Tooltip("床の光の輪の数（少しずつ遅れて出る）")]
    [SerializeField] private int floorRingCount = 2;
    [SerializeField] private float floorRingInterval = 0.1f;

    [Header("② 光の柱（プレイヤー）")]
    [Tooltip("体の高さに対する柱の高さの倍率")]
    [SerializeField] private float pillarHeightMul = 1.6f;
    [Tooltip("体の幅に対する柱の幅の倍率")]
    [SerializeField] private float pillarWidthMul = 0.7f;

    [Header("① 床の光の輪")]
    [Tooltip("床の絵の幅・高さに対する輪の大きさの倍率")]
    [SerializeField] private Vector2 floorRingSizeMul = new Vector2(1.05f, 1.05f);

    [Header("⑥ 光の波（床）")]
    [Tooltip("中央から端まで走る秒数")]
    [SerializeField] private float waveDuration = 0.35f;
    [Tooltip("走る間に光を置く回数（片側）")]
    [SerializeField] private int waveSteps = 10;
    [Tooltip("1回に置く光の粒の数（片側）")]
    [SerializeField] private int wavePerStep = 2;

    [Header("⑤ 体の発光（プレイヤー）")]
    [SerializeField] private Color bodyGlowColor = new Color(0.55f, 1f, 0.6f, 1f);
    [Range(0f, 1f)] [SerializeField] private float bodyGlowMaxAlpha = 0.6f;
    [SerializeField] private float bodyGlowDuration = 0.45f;

    [Header("色（従来の回復エフェクトと同じ）")]
    [SerializeField] private Color colorA = new Color(0.502f, 1f, 0.502f);
    [Range(0f, 1f)] [SerializeField] private float weightA = 0.7f;
    [SerializeField] private Color colorB = Color.cyan;
    [Range(0f, 1f)] [SerializeField] private float weightB = 0.2f;
    [SerializeField] private Color colorC = Color.white;

    private Coroutine glowCo;
    private Coroutine floorCo;

    private void Awake()
    {
        ParticleSystem.MinMaxGradient mix = WeightedColors();
        foreach (var ps in new[] { pillar, motes, crosses, floorRing, wave })
        {
            if (ps == null) continue;
            var em = ps.emission;
            em.enabled = false; // 全てEmitで出す
            var main = ps.main;
            main.startColor = mix;
            if (!ps.isPlaying) ps.Play(false);
        }
        if (pillar != null) { var m = pillar.main; m.startColor = new ParticleSystem.MinMaxGradient(Color.Lerp(colorA, Color.white, 0.4f)); }
        if (floorRing != null) { var m = floorRing.main; m.startColor = new ParticleSystem.MinMaxGradient(Color.Lerp(colorA, colorB, 0.35f)); }
        if (bodyGlow != null) { bodyGlow.enabled = false; }
    }

    /// <summary>回復した時に呼ぶ</summary>
    public void Play()
    {
        if (!isActiveAndEnabled) return;
        Bounds b = GetBounds();
        if (target == Target.Player) PlayPlayer(b);
        else PlayFloor(b);
    }

    private Bounds GetBounds()
    {
        if (source != null && source.sprite != null) return source.bounds;
        return new Bounds(transform.position, new Vector3(0.6f, 1.0f, 0f));
    }

    // ---------- プレイヤー ----------
    private void PlayPlayer(Bounds b)
    {
        // ② 光の柱：足元から体の上まで
        if (pillar != null)
        {
            float h = b.size.y * pillarHeightMul, w = Mathf.Max(0.2f, b.size.x * pillarWidthMul);
            var ep = new ParticleSystem.EmitParams
            {
                position = new Vector3(b.center.x, b.min.y + h * 0.5f, 0f),
                startSize3D = new Vector3(w, h, 1f),
            };
            pillar.Emit(ep, 1);
        }
        // ③ 立ちのぼる光の粒：体の幅の範囲から
        EmitInBox(motes, new Vector3(b.center.x, b.min.y + b.size.y * 0.3f, 0f), new Vector2(b.size.x * 0.8f, b.size.y * 0.6f), moteCount);
        // ④ 十字のきらめき：体のまわり
        EmitInBox(crosses, b.center, new Vector2(b.size.x * 1.2f, b.size.y * 0.9f), crossCount);
        // ⑤ 体がほのかに光る
        if (bodyGlow != null && source != null)
        {
            if (glowCo != null) StopCoroutine(glowCo);
            glowCo = StartCoroutine(BodyGlowRoutine());
        }
    }

    private IEnumerator BodyGlowRoutine()
    {
        float t = 0f;
        bodyGlow.enabled = true;
        while (t < bodyGlowDuration)
        {
            // 本体の絵（ダンスで毎フレーム変わる）と向きをそのまま写す
            bodyGlow.sprite = source.sprite;
            bodyGlow.flipX = source.flipX;
            bodyGlow.flipY = source.flipY;
            bool visible = source.enabled && source.color.a > 0.01f;
            float k = t / Mathf.Max(0.01f, bodyGlowDuration);
            float a = bodyGlowMaxAlpha * Mathf.Sin(k * Mathf.PI) * (visible ? source.color.a : 0f);
            var c = bodyGlowColor; c.a = a;
            bodyGlow.color = c;
            t += Time.deltaTime;
            yield return null;
        }
        bodyGlow.enabled = false;
        glowCo = null;
    }

    // ---------- 床 ----------
    private void PlayFloor(Bounds b)
    {
        // ③ 立ちのぼる光の粒：床の幅いっぱい
        EmitInBox(motes, new Vector3(b.center.x, b.center.y, 0f), new Vector2(b.size.x * 0.9f, b.size.y * 0.5f), moteCount);
        // ④ 十字のきらめき：床の上
        EmitInBox(crosses, new Vector3(b.center.x, b.center.y + b.size.y * 0.3f, 0f), new Vector2(b.size.x * 0.9f, b.size.y * 0.8f), crossCount);
        if (floorCo != null) StopCoroutine(floorCo);
        floorCo = StartCoroutine(FloorRoutine(b));
    }

    private IEnumerator FloorRoutine(Bounds b)
    {
        // ① 床の楕円に沿った光の輪（少しずつ遅れて2重）
        if (floorRing != null)
        {
            for (int i = 0; i < floorRingCount; i++)
            {
                var ep = new ParticleSystem.EmitParams
                {
                    position = new Vector3(b.center.x, b.center.y, 0f),
                    startSize3D = new Vector3(b.size.x * floorRingSizeMul.x, b.size.y * floorRingSizeMul.y, 1f),
                };
                floorRing.Emit(ep, 1);
                if (i < floorRingCount - 1) yield return new WaitForSeconds(floorRingInterval);
            }
        }
        // ⑥ 中央から左右の端へ走る光の波
        if (wave != null && waveSteps > 0)
        {
            float half = b.extents.x * 0.95f;
            float step = waveDuration / waveSteps;
            for (int i = 1; i <= waveSteps; i++)
            {
                float x = half * i / waveSteps;
                for (int side = -1; side <= 1; side += 2)
                {
                    var ep = new ParticleSystem.EmitParams { position = new Vector3(b.center.x + side * x, b.center.y, 0f), applyShapeToPosition = true };
                    wave.Emit(ep, Mathf.Max(1, wavePerStep));
                }
                yield return new WaitForSeconds(step);
            }
        }
        floorCo = null;
    }

    // ---------- 共通 ----------
    private static void EmitInBox(ParticleSystem ps, Vector3 center, Vector2 size, int count)
    {
        if (ps == null || count <= 0) return;
        var ep = new ParticleSystem.EmitParams();
        for (int i = 0; i < count; i++)
        {
            ep.position = center + new Vector3(Random.Range(-0.5f, 0.5f) * size.x, Random.Range(-0.5f, 0.5f) * size.y, 0f);
            ps.Emit(ep, 1);
        }
    }

    // 緑70%・水色20%・白10%（従来のHealVfxColorizerと同じ割合）をランダムに選ぶ色
    private ParticleSystem.MinMaxGradient WeightedColors()
    {
        float wA = Mathf.Clamp01(weightA), wB = Mathf.Clamp01(weightB);
        if (wA + wB > 1f) { float tot = wA + wB; wA /= tot; wB /= tot; }
        var g = new Gradient();
        g.mode = GradientMode.Fixed;
        g.SetKeys(
            new[] { new GradientColorKey(colorA, Mathf.Max(0.001f, wA)), new GradientColorKey(colorB, Mathf.Clamp(wA + wB, 0.002f, 0.999f)), new GradientColorKey(colorC, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        var m = new ParticleSystem.MinMaxGradient(g);
        m.mode = ParticleSystemGradientMode.RandomColor;
        return m;
    }

    [ContextMenu("テスト再生（Play中）")]
    private void TestPlay()
    {
        if (Application.isPlaying) Play();
    }
}
