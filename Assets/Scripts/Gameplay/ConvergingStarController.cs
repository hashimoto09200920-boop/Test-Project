using UnityEngine;

/// <summary>
/// 画面外周付近からFarLayer中心へ吸い込まれていく粒子1個分の挙動（Area10 Final Stage背景用）。
/// ParticleSystemのVelocity/Size over Lifetimeは寿命（時間）基準のため、中心付近で発生した粒子が
/// 「もう十分近いのに寿命が尽きるまで消えない＝中心に居残る」問題が起きていた。
/// この実装は「進行度＝目標地点までの距離ベース」で管理し、到達した瞬間に必ず消滅させる。
/// </summary>
public class ConvergingStarController : MonoBehaviour
{
    private SpriteRenderer sr;
    private Vector3 startPos;
    private Vector3 targetPos; // 中心そのものではなく、中心から targetRadius だけ離れた収束ゾーン上の点
    private float totalDistance;
    private float traveled;
    private float speed;
    private float startSize;
    private float maxAlpha;
    private AnimationCurve speedCurve;

    private static Sprite cachedGlowSprite;

    private float TimeScale =>
        SlowMotionManager.Instance != null ? SlowMotionManager.Instance.TimeScale : 1f;

    /// <summary>
    /// center: 吸い込まれる中心点（FarLayer中心）。targetRadius: centerからこの距離だけ離れた場所で消滅する
    /// （＝収束ゾーンの半径。0にすると中心ぴったりまで到達する）。
    /// moveSpeedCurve: 経過の生の進行度(0〜1)→実際の移動位置(0〜1)。縮小率は 1-moveSpeedCurveの値 で自動計算する。
    /// </summary>
    public void Init(Vector3 startPosition, Vector3 center, float targetRadius, Color color, float speedValue,
        float size, float alphaMax, AnimationCurve moveSpeedCurve, string sortingLayerName, int sortingOrder)
    {
        sr = gameObject.AddComponent<SpriteRenderer>();
        sr.sprite = GetGlowSprite();
        sr.color = new Color(color.r, color.g, color.b, 0f);
        sr.sortingLayerName = sortingLayerName;
        sr.sortingOrder = sortingOrder;

        startPos = startPosition;
        Vector3 dirToCenter = (center - startPosition).sqrMagnitude > 0.0001f
            ? (center - startPosition).normalized
            : Vector3.right;
        targetPos = center - dirToCenter * Mathf.Max(0f, targetRadius);

        totalDistance = Mathf.Max(0.05f, Vector3.Distance(startPos, targetPos));
        traveled = 0f;
        speed = Mathf.Max(0.01f, speedValue);
        startSize = size;
        maxAlpha = alphaMax;
        speedCurve = moveSpeedCurve;

        transform.position = startPos;
        transform.localScale = Vector3.one * startSize;
    }

    private void Update()
    {
        traveled += speed * Time.deltaTime * TimeScale;
        float t = Mathf.Clamp01(traveled / totalDistance);

        // ★吸い込まれるスピード自体をカーブ化：tは経過の生の進行度（時間ベース）、
        //   tPosはspeedCurveで変換した「実際に移動させる位置の進行度」。
        //   最初はゆっくり、中心に近づくほど速く動くカーブにすると加速しながら吸い込まれる見た目になる。
        float tPos = Mathf.Clamp01(speedCurve.Evaluate(t));
        transform.position = Vector3.Lerp(startPos, targetPos, tPos);

        // ★縮小はSpeed Curveの数式的な逆（1-tPos）で自動計算する。カーブを2つ別々に持つと
        //   ユーザーがSpeed Curveだけ調整した時にズレるため、常に連動させる。
        float sizeMul = 1f - tPos;
        transform.localScale = Vector3.one * startSize * sizeMul;

        // 発生直後にふわっと現れ（0〜8%）、それ以外はサイズカーブに連動してフェードアウトする
        // （＝収束ゾーンに吸い込まれて縮みながら消えるのと同じタイミングで透明になる）
        float fadeIn = Mathf.InverseLerp(0f, 0.08f, t);
        float alpha = maxAlpha * Mathf.Min(fadeIn, sizeMul);
        Color c = sr.color;
        c.a = alpha;
        sr.color = c;

        if (t >= 1f)
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
}
