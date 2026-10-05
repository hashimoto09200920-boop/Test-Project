using System.Collections;
using UnityEngine;

/// <summary>
/// Area10クリア時だけ、プレイヤー（PixelDancer）のFinishポーズ（Finish_10）にNeonDancerの後半移行と同じ虹色演出一式を出す部品。
/// - 体の虹色発光（外側へ重ねる層つき）
/// - リングの波紋
/// - 立ちのぼるArea1〜9の9色の光の粒
/// - 締めの一撃（大きなリング＋外へ弾ける光の粒）
/// StageIntroController.PlayAreaComplete(rainbowFx) から呼ばれる。EnemySpawnerはArea10（IsBossRushArea）の時だけ渡すため、Area1〜9には影響しない。
/// 配置はメニュー「Tools/NeonDancer/6 プレイヤー/Area10クリア時の虹色Finishを追加」で行う（数値はNeonDancerControllerからコピー）。
/// </summary>
[DisallowMultipleComponent]
public class PlayerFinishRainbowFX : MonoBehaviour
{
    [Header("参照")]
    [Tooltip("発光用のSpriteRenderer（PixelDancer > FinishRainbowGlow、加算合成）")]
    [SerializeField] private SpriteRenderer bodyGlowRenderer;
    [Tooltip("立ちのぼる光の粒・締めの光の粒（PixelDancer > FinishRainbowSparks）")]
    [SerializeField] private ParticleSystem sparkParticles;

    [Header("虹色の発光")]
    [Tooltip("虹色に光る秒数")]
    [SerializeField] private float rainbowGlowDuration = 3f;
    [Tooltip("虹色が1秒に何周するか")]
    [SerializeField] private float rainbowCycleSpeed = 1.5f;
    [Tooltip("発光の明滅（パルス）の回数/秒")]
    [SerializeField] private float rainbowPulseFrequency = 2f;
    [Range(0f, 1f)] [SerializeField] private float rainbowGlowMaxAlpha = 0.9f;
    [Tooltip("発光の大きさ（体に対する倍率）")]
    [SerializeField] private float rainbowGlowScale = 1.08f;
    [Tooltip("光の重ね掛け：外側へ重ねる発光の層の大きさ（体に対する倍率）。Rainbow Glow Scaleの層の外側に、この数だけ層を追加する")]
    [SerializeField] private float[] rainbowGlowExtraLayerScales = { 1.2f, 1.38f };
    [Tooltip("光の重ね掛け：外側の各層の濃さ（内側の層に対する倍率。外ほど薄く）")]
    [SerializeField] private float[] rainbowGlowExtraLayerAlphas = { 0.5f, 0.25f };

    [Header("リングの波紋")]
    [Tooltip("リングの画像")]
    [SerializeField] private Sprite rainbowRingSprite;
    [Tooltip("何秒ごとに1つ出すか")]
    [SerializeField] private float rainbowRingInterval = 0.5f;
    [Tooltip("直径（ワールド単位）の出始め/最後")]
    [SerializeField] private Vector2 rainbowRingSize = new Vector2(0.8f, 3.2f);
    [Tooltip("広がりきるまでの秒数")]
    [SerializeField] private float rainbowRingDuration = 0.7f;
    [Range(0f, 1f)] [SerializeField] private float rainbowRingMaxAlpha = 0.8f;

    [Header("立ちのぼる光の粒（色はArea1〜9の9色）")]
    [Tooltip("1秒あたりの数")]
    [SerializeField] private float rainbowSparkRate = 30f;
    [Tooltip("上へ昇る速さ（最小/最大）")]
    [SerializeField] private Vector2 rainbowSparkRiseSpeed = new Vector2(0.8f, 1.8f);
    [Tooltip("寿命（最小/最大、秒）と大きさ（最小/最大）")]
    [SerializeField] private Vector2 rainbowSparkLifetime = new Vector2(0.8f, 1.4f);
    [SerializeField] private Vector2 rainbowSparkSize = new Vector2(0.06f, 0.14f);

    [Header("締めの一撃")]
    [Tooltip("最後に広がる大きなリングの直径（ワールド単位）と秒数")]
    [SerializeField] private float rainbowFinalRingSize = 6f;
    [SerializeField] private float rainbowFinalRingDuration = 0.6f;
    [Tooltip("弾ける光の粒の数と速さ（最小/最大）")]
    [SerializeField] private int rainbowFinalSparkCount = 50;
    [SerializeField] private Vector2 rainbowFinalSparkSpeed = new Vector2(3f, 6f);

    // AreaSelectの9色（NeonDancerと同じ）
    private static readonly Color[] AreaColors =
    {
        new Color32(0x9B, 0x8F, 0xC7, 0xFF), // Area1
        new Color32(0x4C, 0xAF, 0x7D, 0xFF), // Area2
        new Color32(0x8D, 0x99, 0xAE, 0xFF), // Area3
        new Color32(0xE0, 0x7A, 0x3F, 0xFF), // Area4
        new Color32(0xB2, 0x3A, 0x52, 0xFF), // Area5
        new Color32(0xE0, 0xB0, 0x4F, 0xFF), // Area6
        new Color32(0x4F, 0x8F, 0xE0, 0xFF), // Area7
        new Color32(0x5F, 0xD6, 0xD6, 0xFF), // Area8
        new Color32(0xA3, 0xAE, 0xE0, 0xFF), // Area9
    };

    private SpriteRenderer body;

    /// <summary>Finish_10を表示した瞬間に呼ぶ。虹色の発光が終わるまで待つ（締めの一撃は投げっぱなし）</summary>
    public IEnumerator Play(SpriteRenderer bodyRenderer)
    {
        body = bodyRenderer;
        if (bodyGlowRenderer == null || body == null || rainbowGlowDuration <= 0f) yield break;
        Transform gt = bodyGlowRenderer.transform;

        // 光の重ね掛け：発光を複製して外側に層を重ねる（外ほど大きく薄く。演出が終わったら消す）
        int layerCount = rainbowGlowExtraLayerScales != null ? rainbowGlowExtraLayerScales.Length : 0;
        var layers = new SpriteRenderer[layerCount];
        for (int i = 0; i < layerCount; i++)
        {
            var go = new GameObject("FinishRainbowGlowLayer" + (i + 2));
            go.transform.SetParent(body.transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sharedMaterial = bodyGlowRenderer.sharedMaterial;
            sr.sortingLayerID = bodyGlowRenderer.sortingLayerID;
            sr.sortingOrder = bodyGlowRenderer.sortingOrder;
            go.transform.localScale = Vector3.one * rainbowGlowExtraLayerScales[i];
            layers[i] = sr;
        }

        float t = 0f;
        float ringTimer = 0f;
        float sparkCarry = 0f;
        while (t < rainbowGlowDuration)
        {
            t += Time.deltaTime;
            bodyGlowRenderer.sprite = body.sprite;
            bodyGlowRenderer.flipX = body.flipX;
            bodyGlowRenderer.flipY = body.flipY;
            gt.localScale = Vector3.one * rainbowGlowScale;
            float pulse = 0.5f + 0.5f * Mathf.Sin(t * rainbowPulseFrequency * Mathf.PI * 2f);
            Color hue = Color.HSVToRGB(Mathf.Repeat(t * rainbowCycleSpeed, 1f), 0.85f, 1f);
            float fade = Mathf.Clamp01(Mathf.Min(t, rainbowGlowDuration - t) / 0.25f); // 出だしと終わりはなめらかに
            float a = rainbowGlowMaxAlpha * Mathf.Lerp(0.35f, 1f, pulse) * fade;
            hue.a = a;
            bodyGlowRenderer.color = hue;
            bodyGlowRenderer.enabled = true;
            for (int i = 0; i < layerCount; i++)
            {
                if (layers[i] == null) continue;
                layers[i].sprite = body.sprite;
                layers[i].flipX = body.flipX;
                layers[i].flipY = body.flipY;
                float la = (rainbowGlowExtraLayerAlphas != null && i < rainbowGlowExtraLayerAlphas.Length) ? rainbowGlowExtraLayerAlphas[i] : 0.3f;
                // 外側の層は色を少しずらして、虹色が重なって見えるようにする
                Color lc = Color.HSVToRGB(Mathf.Repeat(t * rainbowCycleSpeed + 0.12f * (i + 1), 1f), 0.85f, 1f);
                lc.a = a * la;
                layers[i].color = lc;
            }

            // リングの波紋
            ringTimer -= Time.deltaTime;
            if (ringTimer <= 0f && t < rainbowGlowDuration - rainbowRingDuration * 0.5f)
            {
                ringTimer = Mathf.Max(0.05f, rainbowRingInterval);
                StartCoroutine(RingRoutine(rainbowRingSize.x, rainbowRingSize.y, rainbowRingDuration, rainbowRingMaxAlpha, hue));
            }

            // 立ちのぼる光の粒
            if (sparkParticles != null)
            {
                sparkCarry += rainbowSparkRate * Time.deltaTime;
                int n = Mathf.FloorToInt(sparkCarry);
                sparkCarry -= n;
                for (int i = 0; i < n; i++) EmitSpark(false);
            }
            yield return null;
        }
        Color off = bodyGlowRenderer.color; off.a = 0f;
        bodyGlowRenderer.color = off;
        bodyGlowRenderer.enabled = false;
        foreach (var l in layers) if (l != null) Destroy(l.gameObject);

        // 締めの一撃：大きなリングと、外へ弾ける光の粒
        StartCoroutine(RingRoutine(rainbowRingSize.x, rainbowFinalRingSize, rainbowFinalRingDuration, 1f, Color.white));
        if (sparkParticles != null)
            for (int i = 0; i < Mathf.Max(0, rainbowFinalSparkCount); i++) EmitSpark(true);
    }

    // リングを1つ、体の中心から広げながら消す
    private IEnumerator RingRoutine(float startSize, float endSize, float duration, float maxAlpha, Color color)
    {
        if (rainbowRingSprite == null || bodyGlowRenderer == null || body == null) yield break;
        var go = new GameObject("FinishRainbowRing");
        go.transform.position = body.bounds.center;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = rainbowRingSprite;
        sr.sharedMaterial = bodyGlowRenderer.sharedMaterial; // 加算合成
        sr.sortingLayerID = bodyGlowRenderer.sortingLayerID;
        sr.sortingOrder = bodyGlowRenderer.sortingOrder + 1;
        float spriteSize = Mathf.Max(0.0001f, rainbowRingSprite.bounds.size.x);
        float t = 0f;
        float dur = Mathf.Max(0.01f, duration);
        while (t < dur)
        {
            if (go == null) yield break;
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / dur);
            float eased = 1f - (1f - k) * (1f - k);
            go.transform.localScale = Vector3.one * (Mathf.Lerp(startSize, endSize, eased) / spriteSize);
            Color c = color; c.a = maxAlpha * (1f - k);
            sr.color = c;
            yield return null;
        }
        if (go != null) Destroy(go);
    }

    // 9色の光の粒を1つ出す（burst＝締めの一撃で全方位へ弾ける／通常は体の周りから上へ昇る）
    private void EmitSpark(bool burst)
    {
        Bounds b = body.bounds;
        Vector3 pos;
        Vector2 vel;
        if (burst)
        {
            pos = b.center;
            vel = Random.insideUnitCircle.normalized * Random.Range(Mathf.Min(rainbowFinalSparkSpeed.x, rainbowFinalSparkSpeed.y), Mathf.Max(rainbowFinalSparkSpeed.x, rainbowFinalSparkSpeed.y));
        }
        else
        {
            pos = new Vector3(Random.Range(b.min.x, b.max.x), Random.Range(b.min.y, b.max.y), 0f);
            vel = new Vector2(Random.Range(-0.2f, 0.2f), Random.Range(Mathf.Min(rainbowSparkRiseSpeed.x, rainbowSparkRiseSpeed.y), Mathf.Max(rainbowSparkRiseSpeed.x, rainbowSparkRiseSpeed.y)));
        }
        var ep = new ParticleSystem.EmitParams
        {
            position = pos,
            velocity = vel,
            startLifetime = Random.Range(Mathf.Min(rainbowSparkLifetime.x, rainbowSparkLifetime.y), Mathf.Max(rainbowSparkLifetime.x, rainbowSparkLifetime.y)),
            startSize = Random.Range(Mathf.Min(rainbowSparkSize.x, rainbowSparkSize.y), Mathf.Max(rainbowSparkSize.x, rainbowSparkSize.y)),
            startColor = AreaColors[Random.Range(0, AreaColors.Length)],
            applyShapeToPosition = false,
        };
        if (!sparkParticles.isPlaying) sparkParticles.Play();
        sparkParticles.Emit(ep, 1);
    }
}
