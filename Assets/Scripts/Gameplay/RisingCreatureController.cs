using UnityEngine;

/// <summary>
/// Area7の背景演出：Area8のMarshal/Dragonが上昇気流に乗って上空へ昇っていく1体分の挙動。
/// 実際のMarshalController/DragonController（=MarshalController共有）は一切使わず、
/// 見た目の「Move」アニメーションで実際に使われているスプライトのみを流用する軽量な専用コントローラー
/// （Marshal=Burst1-4.png、Dragon=reuseIdleFramesForMove仕様によりIdle1/3/5/6/7/8.pngが実体）。
/// SwayStyle.Irregular = Marshal用（不規則な左右揺れ）
/// SwayStyle.Serpentine = Dragon用（滑らかなS字グライド＋バンク回転）
/// </summary>
public class RisingCreatureController : MonoBehaviour
{
    public enum SwayStyle { Irregular, Serpentine }

    private SpriteRenderer spriteRenderer;
    private Sprite[] frames;
    private float frameInterval;
    private float frameTimer;
    private int frameIndex;

    private float riseSpeed;
    private float baseX;
    private SwayStyle swayStyle;

    // Irregular（Marshal）：ランダムな間隔で目標オフセットが変わり、なめらかに追従する
    private float swayAmplitude;
    private float swayChangeIntervalMin;
    private float swayChangeIntervalMax;
    private float swayTransitionRate;
    private float currentSwayOffset;
    private float targetSwayOffset;
    private float swayChangeTimer;
    private float swayChangeInterval;

    // Serpentine（Dragon）：滑らかなsin波のS字グライド＋それに連動したバンク回転
    private float serpentineAmplitude;
    private float serpentineFrequency;
    private float serpentinePhase;
    private float bankAmplitude;

    // 一定時間後のフェードアウト（縮小しながら消える＝さらに遠ざかっていくように見せる）
    private float visibleDuration;
    private float fadeOutDuration;
    private float minScaleAtFadeEnd;
    private float age;
    private Vector3 initialLocalScale;
    private float initialAlpha;

    private float TimeScale =>
        SlowMotionManager.Instance != null ? SlowMotionManager.Instance.TimeScale : 1f;

    public void Init(Sprite[] moveFrames, float frameRate, float speed,
        SwayStyle style,
        float swayAmp, float swayChangeMin, float swayChangeMax, float swayTransRate,
        float serpAmp, float serpFreq, float bankAmp,
        float visibleDur, float fadeOutDur, float minFadeScale)
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        initialAlpha = spriteRenderer != null ? spriteRenderer.color.a : 1f;

        frames = moveFrames;
        frameInterval = frameRate > 0f ? 1f / frameRate : 0.15f;
        frameIndex = (frames != null && frames.Length > 0) ? Random.Range(0, frames.Length) : 0;
        if (frames != null && frames.Length > 0 && spriteRenderer != null)
            spriteRenderer.sprite = frames[frameIndex];

        riseSpeed = speed;
        baseX = transform.position.x;
        swayStyle = style;

        swayAmplitude = swayAmp;
        swayChangeIntervalMin = swayChangeMin;
        swayChangeIntervalMax = swayChangeMax;
        swayTransitionRate = swayTransRate;
        PickNextSwayTarget();

        serpentineAmplitude = serpAmp;
        serpentineFrequency = serpFreq;
        serpentinePhase = Random.Range(0f, Mathf.PI * 2f);
        bankAmplitude = bankAmp;

        visibleDuration = visibleDur;
        fadeOutDuration = Mathf.Max(0.05f, fadeOutDur);
        minScaleAtFadeEnd = minFadeScale;
        age = 0f;

        initialLocalScale = transform.localScale;
    }

    private void PickNextSwayTarget()
    {
        targetSwayOffset = Random.Range(-swayAmplitude, swayAmplitude);
        swayChangeInterval = Random.Range(swayChangeIntervalMin, swayChangeIntervalMax);
        swayChangeTimer = 0f;
    }

    private void Update()
    {
        float dt = Time.deltaTime * TimeScale;
        age += dt;

        // 上昇
        Vector3 pos = transform.position;
        pos.y += riseSpeed * dt;

        if (swayStyle == SwayStyle.Irregular)
        {
            // Marshal：不規則な左右揺れ（ランダム目標+なめらか追従、Bat/Tumbleweedと同じ方式）
            swayChangeTimer += dt;
            if (swayChangeTimer >= swayChangeInterval) PickNextSwayTarget();
            currentSwayOffset = Mathf.MoveTowards(currentSwayOffset, targetSwayOffset, swayTransitionRate * dt);
            pos.x = baseX + currentSwayOffset;
        }
        else
        {
            // Dragon：滑らかなS字グライド＋バンク回転
            serpentinePhase += dt * serpentineFrequency * Mathf.PI * 2f;
            pos.x = baseX + Mathf.Sin(serpentinePhase) * serpentineAmplitude;
            float bankAngle = Mathf.Cos(serpentinePhase) * bankAmplitude;
            transform.rotation = Quaternion.Euler(0f, 0f, bankAngle);
        }

        transform.position = pos;

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
}
