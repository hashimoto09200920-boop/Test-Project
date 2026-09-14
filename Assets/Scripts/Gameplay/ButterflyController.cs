using UnityEngine;

/// <summary>
/// 背景演出用の蝶1体分の挙動。
/// 出現地点（anchor）を中心に、X/Y別周波数のsin波（DroneAnimationと同じ「静止画+lissajous drift」方式）
/// でその場をひらひらと漂う。3枚の羽ばたきコマをループ再生。
/// 一定時間経過後、透明化+縮小しながらフェードアウトして消える（CrowControllerと同じ方式）。
/// </summary>
public class ButterflyController : MonoBehaviour
{
    private SpriteRenderer spriteRenderer;
    private Sprite[] frames;
    private float frameInterval;
    private float frameTimer;
    private int frameIndex;

    // その場でひらひら漂う動き（出現地点からの差分オフセット）
    private float driftAmplitudeX;
    private float driftAmplitudeY;
    private float driftFrequencyX;
    private float driftFrequencyY;
    private float driftPhaseX;
    private float driftPhaseY;
    private Vector3 driftOffset = Vector3.zero;

    // 一定時間後のフェードアウト（縮小しながら消える）
    private float visibleDuration;
    private float fadeOutDuration;
    private float minScaleAtFadeEnd;
    private float age;
    private Vector3 initialLocalScale;
    private float initialAlpha;

    private float TimeScale =>
        SlowMotionManager.Instance != null ? SlowMotionManager.Instance.TimeScale : 1f;

    public void Init(Sprite[] flutterFrames, float frameRate,
        float ampX, float ampY, float freqX, float freqY,
        float visibleDur, float fadeOutDur, float minFadeScale)
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        initialAlpha = spriteRenderer != null ? spriteRenderer.color.a : 1f;
        frames = flutterFrames;
        frameInterval = frameRate > 0f ? 1f / frameRate : 0.1f;
        frameIndex = (frames != null && frames.Length > 0) ? Random.Range(0, frames.Length) : 0;
        if (frames != null && frames.Length > 0 && spriteRenderer != null)
            spriteRenderer.sprite = frames[frameIndex];

        driftAmplitudeX = ampX;
        driftAmplitudeY = ampY;
        driftFrequencyX = freqX;
        driftFrequencyY = freqY;
        driftPhaseX = Random.Range(0f, Mathf.PI * 2f);
        driftPhaseY = Random.Range(0f, Mathf.PI * 2f);

        visibleDuration = visibleDur;
        fadeOutDuration = Mathf.Max(0.05f, fadeOutDur);
        minScaleAtFadeEnd = minFadeScale;
        age = 0f;

        // ランダムに左右反転（同じ絵の使い回し感を減らす）
        Vector3 s = transform.localScale;
        if (Random.value < 0.5f) s.x = -s.x;
        transform.localScale = s;
        initialLocalScale = s;
    }

    /// <summary>Stage切り替え等で即座にフェードアウトを開始させる（外部のSpawnerから呼ぶ）</summary>
    public void ForceFadeOut()
    {
        if (age < visibleDuration) age = visibleDuration;
    }

    private void Update()
    {
        float dt = Time.deltaTime * TimeScale;
        age += dt;

        driftPhaseX += dt * driftFrequencyX * Mathf.PI * 2f;
        driftPhaseY += dt * driftFrequencyY * Mathf.PI * 2f;

        // 差分ベースのドリフト（DroneAnimationと同じ手法）
        transform.position -= driftOffset;
        float dx = Mathf.Sin(driftPhaseX) * driftAmplitudeX;
        float dy = Mathf.Sin(driftPhaseY * 1.3f) * driftAmplitudeY; // X/Yで周波数比をずらしリサージュ状の不規則な軌跡にする
        driftOffset = new Vector3(dx, dy, 0f);
        transform.position += driftOffset;

        UpdateFrameAnimation(dt);
        UpdateFadeOut();
    }

    private void UpdateFrameAnimation(float dt)
    {
        if (frames == null || frames.Length == 0 || spriteRenderer == null) return;

        frameTimer += dt;
        while (frameTimer >= frameInterval)
        {
            frameTimer -= frameInterval;
            frameIndex = (frameIndex + 1) % frames.Length;
            spriteRenderer.sprite = frames[frameIndex];
        }
    }

    private void UpdateFadeOut()
    {
        if (age < visibleDuration) return;

        float fadeT = Mathf.Clamp01((age - visibleDuration) / fadeOutDuration);

        if (spriteRenderer != null)
        {
            Color c = spriteRenderer.color;
            c.a = initialAlpha * (1f - fadeT);
            spriteRenderer.color = c;
        }

        transform.localScale = initialLocalScale * Mathf.Lerp(1f, minScaleAtFadeEnd, fadeT);

        if (fadeT >= 1f)
            Destroy(gameObject);
    }

    private void OnDestroy()
    {
        transform.position -= driftOffset;
        driftOffset = Vector3.zero;
    }
}
