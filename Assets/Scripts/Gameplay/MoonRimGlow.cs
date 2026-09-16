using UnityEngine;

// 月の輪郭を、同じスプライトを少しずつ拡大率と不透明度を変えて重ねることで
// なだらかなグラデーションの発光に見せる演出（専用のぼかしシェーダーを使わない疑似グロー）。
// レイヤー1枚だけだと拡大分がそのまま帯状の「輪郭線」に見えてしまうため、
// 複数枚を薄く重ねて段差を目立たなくしている。
[RequireComponent(typeof(SpriteRenderer))]
public class MoonRimGlow : MonoBehaviour
{
    [Tooltip("発光レイヤーの枚数。多いほど滑らかなグラデーションになる")]
    [SerializeField] private int layerCount = 6;

    [Tooltip("最も外側のレイヤーの拡大率（本体を1とする）")]
    [SerializeField] private float maxScale = 1.08f;

    [Tooltip("発光色")]
    [SerializeField] private Color glowColor = new Color(1f, 0.95f, 0.75f, 1f);

    [Tooltip("最も内側（本体に一番近い）レイヤーの、明滅の最も明るい時点での不透明度。外側に行くほど自動的に薄くなり0へ近づく")]
    [SerializeField, Range(0f, 1f)] private float innerAlpha = 0.3f;

    [Tooltip("明滅の最も暗い時点での明るさ倍率（0-1）。1にすると明滅せず常に一定の明るさになる")]
    [SerializeField, Range(0f, 1f)] private float dimMultiplier = 0.35f;

    [Tooltip("ぼんやり明暗する1周期の時間（秒）。ゆっくり大きい値ほど「呼吸」らしくなる")]
    [SerializeField] private float breathePeriodSeconds = 5f;

    private Area09MoonController visibility;
    private SpriteRenderer[] glowRenderers;

    private void Awake()
    {
        visibility = GetComponent<Area09MoonController>();
        SpriteRenderer source = GetComponent<SpriteRenderer>();

        glowRenderers = new SpriteRenderer[layerCount];
        for (int i = 0; i < layerCount; i++)
        {
            float t = (i + 1f) / layerCount;
            float scale = Mathf.Lerp(1f, maxScale, t);

            GameObject go = new GameObject($"MoonRimGlow_{i}");
            go.transform.SetParent(transform, false);
            go.transform.localScale = Vector3.one * scale;

            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = source.sprite;
            sr.sharedMaterial = source.sharedMaterial;
            sr.sortingLayerID = source.sortingLayerID;
            sr.sortingOrder = source.sortingOrder - 1 - i; // 外側のレイヤーほどさらに背面
            glowRenderers[i] = sr;
        }
    }

    private void Update()
    {
        float visAlpha = visibility != null ? visibility.VisibilityAlpha : 1f;

        // ぼんやり明暗を繰り返す「呼吸」（0〜1の滑らかな正弦波）
        float breathe = (Mathf.Sin(Time.time * (2f * Mathf.PI / breathePeriodSeconds)) + 1f) * 0.5f;
        float brightnessMul = Mathf.Lerp(dimMultiplier, 1f, breathe);

        for (int i = 0; i < glowRenderers.Length; i++)
        {
            float t = (i + 1f) / glowRenderers.Length;
            float layerAlpha = Mathf.Lerp(innerAlpha, 0f, t); // 外側のレイヤーほど薄い
            Color c = glowColor;
            c.a = layerAlpha * brightnessMul * visAlpha;
            glowRenderers[i].color = c;
        }
    }
}
