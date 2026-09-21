using System.Collections;
using UnityEngine;

/// <summary>
/// その場で明滅を繰り返す星1個分の挙動（Area10最終ステージ背景用）。
/// StarFlareController（Area9・1回だけ光って消える）と違い、flash in→hold→fade out→待機→再度flash inを
/// 無限ループする。小さい星（柔らかい丸グロー）と大きい星（放射状スパーク、腕の本数をランダムにして形状にバリエーションを持たせる）の
/// どちらも手続き的にテクスチャ生成するため追加素材は不要。
/// </summary>
public class TwinkleStarController : MonoBehaviour
{
    private SpriteRenderer sr;

    private float flashInDuration;
    private float holdDuration;
    private float fadeOutDuration;
    private float waitMin;
    private float waitMax;
    private float maxAlpha;
    private Coroutine loopCoroutine;

    private static readonly System.Collections.Generic.Dictionary<int, Sprite> cachedSparkSprites =
        new System.Collections.Generic.Dictionary<int, Sprite>();
    private static Sprite cachedGlowSprite;

    private float TimeScale =>
        SlowMotionManager.Instance != null ? SlowMotionManager.Instance.TimeScale : 1f;

    /// <summary>小さい星（柔らかい丸グロー）として初期化する。</summary>
    public void InitSmall(Color color, float size, float flashIn, float hold, float fadeOut,
        float waitBetweenMin, float waitBetweenMax, float alphaMax, float initialDelay,
        string sortingLayerName, int sortingOrder)
    {
        sr = gameObject.AddComponent<SpriteRenderer>();
        sr.sprite = GetGlowSprite();
        sr.color = new Color(color.r, color.g, color.b, 0f);
        sr.sortingLayerName = sortingLayerName;
        sr.sortingOrder = sortingOrder;
        transform.localScale = Vector3.one * size;

        StartLoop(flashIn, hold, fadeOut, waitBetweenMin, waitBetweenMax, alphaMax, initialDelay);
    }

    /// <summary>大きい星（放射状スパーク、軸数をランダムにして形状バリエーションを出す）として初期化する。</summary>
    public void InitBig(Color color, float size, int axisCount, float sharpness, float flashIn, float hold, float fadeOut,
        float waitBetweenMin, float waitBetweenMax, float alphaMax, float initialDelay,
        string sortingLayerName, int sortingOrder)
    {
        sr = gameObject.AddComponent<SpriteRenderer>();
        sr.sprite = GetSparkSprite(axisCount, sharpness);
        sr.color = new Color(color.r, color.g, color.b, 0f);
        sr.sortingLayerName = sortingLayerName;
        sr.sortingOrder = sortingOrder;
        transform.localScale = Vector3.one * size;
        transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));

        StartLoop(flashIn, hold, fadeOut, waitBetweenMin, waitBetweenMax, alphaMax, initialDelay);
    }

    private void StartLoop(float flashIn, float hold, float fadeOut, float waitBetweenMin, float waitBetweenMax,
        float alphaMax, float initialDelay)
    {
        flashInDuration = Mathf.Max(0.02f, flashIn);
        holdDuration = hold;
        fadeOutDuration = Mathf.Max(0.05f, fadeOut);
        waitMin = waitBetweenMin;
        waitMax = waitBetweenMax;
        maxAlpha = alphaMax;

        loopCoroutine = StartCoroutine(TwinkleLoop(initialDelay));
    }

    private IEnumerator TwinkleLoop(float initialDelay)
    {
        yield return WaitSeconds(initialDelay);

        while (true)
        {
            float age = 0f;
            while (age < flashInDuration)
            {
                age += Time.deltaTime * TimeScale;
                SetAlpha(maxAlpha * Mathf.Clamp01(age / flashInDuration));
                yield return null;
            }

            yield return WaitSeconds(holdDuration);
            SetAlpha(maxAlpha);

            age = 0f;
            while (age < fadeOutDuration)
            {
                age += Time.deltaTime * TimeScale;
                SetAlpha(maxAlpha * (1f - Mathf.Clamp01(age / fadeOutDuration)));
                yield return null;
            }
            SetAlpha(0f);

            yield return WaitSeconds(Random.Range(waitMin, waitMax));
        }
    }

    private IEnumerator WaitSeconds(float seconds)
    {
        float elapsed = 0f;
        while (elapsed < seconds)
        {
            elapsed += Time.deltaTime * TimeScale;
            yield return null;
        }
    }

    private void SetAlpha(float a)
    {
        if (sr == null) return;
        Color c = sr.color;
        c.a = a;
        sr.color = c;
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

    // ★axisCount本の腕を持つ放射状スパークを手続き的に生成する。
    //   axisCount/sharpnessの組み合わせごとにキャッシュし、同じ組み合わせなら再生成しない。
    private static Sprite GetSparkSprite(int axisCount, float sharpness)
    {
        int key = axisCount * 1000 + Mathf.RoundToInt(sharpness * 10f);
        if (cachedSparkSprites.TryGetValue(key, out Sprite cached) && cached != null) return cached;

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

                // axisCount方向スパーク：軸方向ほど遠くまで伸びる形状
                float axisAlign = Mathf.Pow(Mathf.Abs(Mathf.Cos(angle * axisCount / 2f)), sharpness);
                float reach = Mathf.Lerp(0.15f, 1f, axisAlign);
                float a = Mathf.Clamp01(1f - dist / reach);
                a = Mathf.Pow(a, 1.5f);

                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();
        Sprite sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        cachedSparkSprites[key] = sprite;
        return sprite;
    }
}
