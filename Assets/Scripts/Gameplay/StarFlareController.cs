using UnityEngine;

/// <summary>
/// Area9の背景演出：星が一瞬強く瞬く（フレア）1回分の挙動。
/// 移動はせず、その場で素早く明るくなってからゆっくり消えるだけ。
/// 4方向に伸びるスパーク形状+中心の柔らかい発光を手続き的に生成する（追加素材不要）。
/// </summary>
public class StarFlareController : MonoBehaviour
{
    private SpriteRenderer glowRenderer;
    private SpriteRenderer sparkRenderer;

    private float flashInDuration;
    private float holdDuration;
    private float fadeOutDuration;
    private float maxAlpha;
    private float age;

    private static Sprite cachedGlowSprite;
    private static Sprite cachedSparkSprite;

    private float TimeScale =>
        SlowMotionManager.Instance != null ? SlowMotionManager.Instance.TimeScale : 1f;

    public void Init(Color color, float size, float flashIn, float hold, float fadeOut, float alphaMax,
        string sortingLayerName, int sortingOrder)
    {
        flashInDuration = Mathf.Max(0.02f, flashIn);
        holdDuration = hold;
        fadeOutDuration = Mathf.Max(0.05f, fadeOut);
        maxAlpha = alphaMax;
        age = 0f;

        GameObject glowGo = new GameObject("Glow");
        glowGo.transform.SetParent(transform, false);
        glowGo.transform.localScale = Vector3.one * size * 1.6f;
        glowRenderer = glowGo.AddComponent<SpriteRenderer>();
        glowRenderer.sprite = GetGlowSprite();
        glowRenderer.color = new Color(color.r, color.g, color.b, 0f);
        glowRenderer.sortingLayerName = sortingLayerName;
        glowRenderer.sortingOrder = sortingOrder;

        GameObject sparkGo = new GameObject("Spark");
        sparkGo.transform.SetParent(transform, false);
        sparkGo.transform.localScale = Vector3.one * size;
        sparkRenderer = sparkGo.AddComponent<SpriteRenderer>();
        sparkRenderer.sprite = GetSparkSprite();
        sparkRenderer.color = new Color(1f, 1f, 1f, 0f);
        sparkRenderer.sortingLayerName = sortingLayerName;
        sparkRenderer.sortingOrder = sortingOrder + 1;
    }

    private void Update()
    {
        float dt = Time.deltaTime * TimeScale;
        age += dt;

        float alphaMul;
        if (age < flashInDuration)
        {
            alphaMul = age / flashInDuration;
        }
        else if (age < flashInDuration + holdDuration)
        {
            alphaMul = 1f;
        }
        else
        {
            float fadeT = (age - flashInDuration - holdDuration) / fadeOutDuration;
            alphaMul = 1f - Mathf.Clamp01(fadeT);
        }

        if (glowRenderer != null)
        {
            Color c = glowRenderer.color;
            c.a = maxAlpha * alphaMul;
            glowRenderer.color = c;
        }
        if (sparkRenderer != null)
        {
            Color c = sparkRenderer.color;
            c.a = alphaMul;
            sparkRenderer.color = c;
        }

        if (age >= flashInDuration + holdDuration + fadeOutDuration)
            Destroy(gameObject);
    }

    private static Sprite GetGlowSprite()
    {
        if (cachedGlowSprite != null) return cachedGlowSprite;

        const int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Vector2 center = new Vector2(size / 2f, size / 2f);
        float radius = size / 2f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), center) / radius;
                float a = Mathf.Clamp01(1f - d);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
            }
        }
        tex.Apply();
        cachedGlowSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        return cachedGlowSprite;
    }

    private static Sprite GetSparkSprite()
    {
        if (cachedSparkSprite != null) return cachedSparkSprite;

        const int size = 64;
        Vector2 center = new Vector2(size / 2f, size / 2f);
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 p = new Vector2(x, y) - center;
                float dist = p.magnitude / (size / 2f);
                float angle = Mathf.Atan2(p.y, p.x);

                // 4方向スパーク：軸方向（0,90,180,270度）ほど遠くまで伸びる形状
                float axisAlign = Mathf.Pow(Mathf.Abs(Mathf.Cos(angle * 2f)), 6f);
                float reach = Mathf.Lerp(0.15f, 1f, axisAlign);
                float a = Mathf.Clamp01(1f - dist / reach);
                a = Mathf.Pow(a, 1.5f);

                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();
        cachedSparkSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        return cachedSparkSprite;
    }
}
