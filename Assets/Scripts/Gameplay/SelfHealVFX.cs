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
    [Tooltip("床の絵の幅・高さに対する輪の大きさの倍率。輪の画像は明るい部分が中心から約85％、外側の光が約94％の位置にあるため、床の見えている範囲（FloorNeonは横約96％・縦約86％）からはみ出さない値にする")]
    [SerializeField] private Vector2 floorRingSizeMul = new Vector2(0.98f, 0.88f);
    [Tooltip("床の楕円の中心の、床の絵の中心からのずれ（床の絵の幅・高さに対する割合。＋で右・上）。床の絵に楕円以外の光（下の光の帯など）が含まれる時に使う。右クリックメニューで自動設定")]
    [SerializeField] private Vector2 floorEllipseCenterOffset = Vector2.zero;
    [Tooltip("⑥ 光の波が走る幅（床の絵の幅に対する割合）。右クリックメニューで床の楕円の幅に合わせる")]
    [SerializeField] private float floorWaveWidthMul = 0.95f;
    [Tooltip("（右クリックメニュー「床の光の輪を床の楕円に合わせる」用）輪の画像で、明るい輪の線がある位置（画像の半分の大きさに対する割合）")]
    [SerializeField] private float floorRingPeakRadius = 0.85f;
    [Tooltip("（同上）輪の明るい線を楕円の縁に対してどこに置くか（1＝縁に重なる、小さいほど内側）")]
    [Range(0.5f, 1.2f)] [SerializeField] private float floorRingFitMargin = 0.97f;
    [Tooltip("（同上）床の絵で「楕円」とみなす濃さ（0〜255）。これより薄い光（周りのにじみ・下の光の帯）は楕円に含めない")]
    [Range(1, 254)] [SerializeField] private int floorEllipseAlphaThreshold = 200;

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
        // 床の楕円の中心（床の絵の中心から Floor Ellipse Center Offset だけずらす）
        Vector3 ec = new Vector3(b.center.x + b.size.x * floorEllipseCenterOffset.x, b.center.y + b.size.y * floorEllipseCenterOffset.y, 0f);
        // ① 床の楕円に沿った光の輪（少しずつ遅れて2重）
        if (floorRing != null)
        {
            for (int i = 0; i < floorRingCount; i++)
            {
                var ep = new ParticleSystem.EmitParams
                {
                    position = ec,
                    startSize3D = new Vector3(b.size.x * floorRingSizeMul.x, b.size.y * floorRingSizeMul.y, 1f),
                };
                floorRing.Emit(ep, 1);
                if (i < floorRingCount - 1) yield return new WaitForSeconds(floorRingInterval);
            }
        }
        // ⑥ 中央から左右の端へ走る光の波
        if (wave != null && waveSteps > 0)
        {
            float half = b.size.x * floorWaveWidthMul * 0.5f;
            float step = waveDuration / waveSteps;
            for (int i = 1; i <= waveSteps; i++)
            {
                float x = half * i / waveSteps;
                for (int side = -1; side <= 1; side += 2)
                {
                    var ep = new ParticleSystem.EmitParams { position = new Vector3(ec.x + side * x, ec.y, 0f), applyShapeToPosition = true };
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

#if UNITY_EDITOR
    /// <summary>
    /// ① 床の光の輪・⑥ 光の波を、床の絵の中の「楕円」に合わせる。
    /// 床の絵（source）のPNGを読み、濃さが Floor Ellipse Alpha Threshold 以上の部分（楕円の本体と縁）を楕円とみなして、
    /// その中心のずれ（Floor Ellipse Center Offset）、輪の大きさ（Floor Ring Size Mul：輪の明るい線が楕円の縁に重なる大きさ）、
    /// 光の波の幅（Floor Wave Width Mul）を自動で設定する。周りのにじみや楕円の下の光の帯は含めない
    /// </summary>
    [ContextMenu("床の光の輪を床の楕円に合わせる")]
    private void FitFloorRingToEllipseMenu()
    {
        FitFloorRingToEllipse(out string msg);
        UnityEditor.EditorUtility.DisplayDialog("SelfHealVFX", msg + "\nシーン・プレハブを保存してください。", "OK");
    }

    /// <summary>
    /// 床の楕円に合わせる処理本体（右クリックメニューと、メニュー「Tools/回復エフェクト/5 …」から呼ぶ）。成功したらtrue。
    /// message に結果（または失敗の理由）を入れる。保存は呼び出し側で行う
    /// </summary>
    public bool FitFloorRingToEllipse(out string message)
    {
        if (target != Target.Floor) { message = name + "：床用（Target＝Floor）ではありません"; return false; }
        if (source == null || source.sprite == null) { message = name + "：Source（床の絵のSpriteRenderer）が設定されていません"; return false; }
        Sprite sp = source.sprite;
        string path = UnityEditor.AssetDatabase.GetAssetPath(sp.texture);
        if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path)) { message = name + "：床の画像ファイルが見つかりません（" + path + "）"; return false; }
        // 画像ファイルを直接読む（取り込み設定の Read/Write を変えずに済むように）
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!tex.LoadImage(System.IO.File.ReadAllBytes(path))) { DestroyImmediate(tex); message = name + "：床の画像を読めませんでした（" + path + "）"; return false; }
        float k = sp.texture.width > 0 ? tex.width / (float)sp.texture.width : 1f; // 取り込み時に縮小されている場合の補正
        Rect r = sp.rect;
        int x0 = Mathf.RoundToInt(r.x * k), y0 = Mathf.RoundToInt(r.y * k);
        int w = Mathf.Max(1, Mathf.RoundToInt(r.width * k)), h = Mathf.Max(1, Mathf.RoundToInt(r.height * k));
        Color32[] px = tex.GetPixels32();
        int minX = int.MaxValue, maxX = -1, minY = int.MaxValue, maxY = -1;
        for (int y = y0; y < y0 + h && y < tex.height; y++)
            for (int x = x0; x < x0 + w && x < tex.width; x++)
            {
                if (px[y * tex.width + x].a < floorEllipseAlphaThreshold) continue;
                if (x < minX) minX = x; if (x > maxX) maxX = x;
                if (y < minY) minY = y; if (y > maxY) maxY = y;
            }
        DestroyImmediate(tex);
        if (maxX < 0) { message = name + "：楕円とみなせる部分が見つかりませんでした（Floor Ellipse Alpha Threshold を下げてください）"; return false; }

        // 楕円の大きさ・中心（床の絵の幅・高さに対する割合。テクスチャのyは下が0）
        float fx = (maxX - minX + 1) / (float)w, fy = (maxY - minY + 1) / (float)h;
        float cxFrac = ((minX + maxX + 1) * 0.5f - x0) / w - 0.5f;
        float cyFrac = ((minY + maxY + 1) * 0.5f - y0) / h - 0.5f;
        if (source.flipX) cxFrac = -cxFrac;
        if (source.flipY) cyFrac = -cyFrac;
        float peak = Mathf.Max(0.1f, floorRingPeakRadius);

        UnityEditor.Undo.RecordObject(this, "床の光の輪を床の楕円に合わせる");
        floorEllipseCenterOffset = new Vector2(Round3(cxFrac), Round3(cyFrac));
        floorRingSizeMul = new Vector2(Round3(fx / peak * floorRingFitMargin), Round3(fy / peak * floorRingFitMargin));
        floorWaveWidthMul = Round3(fx * 0.95f);
        UnityEditor.EditorUtility.SetDirty(this);
        UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(this);
        if (gameObject.scene.IsValid()) UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
        UnityEditor.SceneView.RepaintAll();
        message = $"{name}（床の絵：{sp.name}）楕円 幅{fx * 100f:F0}％・高さ{fy * 100f:F0}％・中心のずれ({cxFrac * 100f:F0}％, {cyFrac * 100f:F0}％) → " +
                  $"Ring Size Mul {floorRingSizeMul} / Center Offset {floorEllipseCenterOffset} / Wave Width Mul {floorWaveWidthMul}";
        Debug.Log("[SelfHealVFX] " + message, this);
        return true;
    }

    private static float Round3(float v) => (float)System.Math.Round(v, 3);
#endif
}
