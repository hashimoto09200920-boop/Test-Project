using UnityEngine;

/// <summary>
/// 背景演出用のコウモリ1体分の飛行挙動。
/// ベースの直進+速度緩急+フェード間際の弧はCrowControllerと同じ。
/// それに加えて、進行方向とは別にY方向へ不規則なフラッター（ランダムな間隔で目標オフセットが
/// 変わり続ける揺れ）を足すことで、カラスとは違う「バットらしい」不規則な上下運動にする。
/// </summary>
public class BatFlybyController : MonoBehaviour
{
    private SpriteRenderer spriteRenderer;
    private Sprite[] frames;
    private float frameInterval;
    private float frameTimer;
    private int frameIndex;

    private Vector3 direction;      // 斜め角度を含む単位ベクトル（turnRateDegPerSecでゆっくり弧を描く）
    private float baseSpeed;
    private float turnRateDegPerSec; // 符号付き。進行方向を毎秒この角度だけ回転させ、緩やかな弧を描く
    private float turnStartTime;     // age がこの値以上になったら旋回を開始する（フェード開始の少し前）

    // 速度の緩急（ランダムな間隔で目標倍率が変わり、なめらかに追従する）
    private float speedMultiplierMin;
    private float speedMultiplierMax;
    private float speedChangeIntervalMin;
    private float speedChangeIntervalMax;
    private float speedTransitionRate;
    private float currentSpeedMultiplier = 1f;
    private float targetSpeedMultiplier = 1f;
    private float speedChangeTimer;
    private float speedChangeInterval;

    // 不規則な上下フラッター（バットらしい動き。ランダムな間隔で目標オフセットが変わり続ける）
    private float flutterAmplitude;
    private float flutterChangeIntervalMin;
    private float flutterChangeIntervalMax;
    private float flutterTransitionRate;
    private float currentFlutterOffset;
    private float targetFlutterOffset;
    private float flutterChangeTimer;
    private float flutterChangeInterval;

    // 一定時間後のフェードアウト（縮小しながら遠ざかって消える）
    private float visibleDuration;
    private float fadeOutDuration;
    private float minScaleAtFadeEnd;
    private float age;
    private Vector3 initialLocalScale;
    private float initialAlpha;

    private float TimeScale =>
        SlowMotionManager.Instance != null ? SlowMotionManager.Instance.TimeScale : 1f;

    public void Init(Sprite[] flapFrames, float frameRate, Vector3 flyDirection, float speed,
        float speedMulMin, float speedMulMax, float changeIntervalMin, float changeIntervalMax,
        float transitionRate, bool facingRight, float visibleDur, float fadeOutDur, float minFadeScale,
        float turnRate, float turnLeadTime,
        float flutterAmp, float flutterChangeMin, float flutterChangeMax, float flutterTransRate)
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        initialAlpha = spriteRenderer != null ? spriteRenderer.color.a : 1f;
        frames = flapFrames;
        frameInterval = frameRate > 0f ? 1f / frameRate : 0.1f;
        frameIndex = (frames != null && frames.Length > 0) ? Random.Range(0, frames.Length) : 0;
        if (frames != null && frames.Length > 0 && spriteRenderer != null)
            spriteRenderer.sprite = frames[frameIndex];

        direction = flyDirection.normalized;
        baseSpeed = speed;
        turnRateDegPerSec = turnRate;

        speedMultiplierMin = speedMulMin;
        speedMultiplierMax = speedMulMax;
        speedChangeIntervalMin = changeIntervalMin;
        speedChangeIntervalMax = changeIntervalMax;
        speedTransitionRate = transitionRate;
        currentSpeedMultiplier = Random.Range(speedMultiplierMin, speedMultiplierMax);
        PickNextSpeedTarget();

        flutterAmplitude = flutterAmp;
        flutterChangeIntervalMin = flutterChangeMin;
        flutterChangeIntervalMax = flutterChangeMax;
        flutterTransitionRate = flutterTransRate;
        PickNextFlutterTarget();

        visibleDuration = visibleDur;
        fadeOutDuration = Mathf.Max(0.05f, fadeOutDur);
        minScaleAtFadeEnd = minFadeScale;
        age = 0f;
        turnStartTime = Mathf.Max(0f, visibleDuration - turnLeadTime);

        // 素材は右向きに飛ぶ絵として作成されている前提。左向きなら水平反転する
        Vector3 s = transform.localScale;
        s.x = Mathf.Abs(s.x) * (facingRight ? 1f : -1f);
        transform.localScale = s;
        initialLocalScale = s;
    }

    private void PickNextSpeedTarget()
    {
        targetSpeedMultiplier = Random.Range(speedMultiplierMin, speedMultiplierMax);
        speedChangeInterval = Random.Range(speedChangeIntervalMin, speedChangeIntervalMax);
        speedChangeTimer = 0f;
    }

    private void PickNextFlutterTarget()
    {
        targetFlutterOffset = Random.Range(-flutterAmplitude, flutterAmplitude);
        flutterChangeInterval = Random.Range(flutterChangeIntervalMin, flutterChangeIntervalMax);
        flutterChangeTimer = 0f;
    }

    private void Update()
    {
        float dt = Time.deltaTime * TimeScale;
        age += dt;

        // 速度の緩急：ランダムな間隔で目標倍率を変え、なめらかに追従させる
        speedChangeTimer += dt;
        if (speedChangeTimer >= speedChangeInterval)
        {
            PickNextSpeedTarget();
        }
        currentSpeedMultiplier = Mathf.MoveTowards(currentSpeedMultiplier, targetSpeedMultiplier, speedTransitionRate * dt);

        // 進行方向をゆっくり一定方向に回転させ、緩やかな弧を描く軌道にする
        // （フェードアウト開始の少し前からのみ旋回を始める。ベースはCrowと同じ）
        if (turnRateDegPerSec != 0f && age >= turnStartTime)
            direction = Quaternion.Euler(0f, 0f, turnRateDegPerSec * dt) * direction;

        float currentSpeed = baseSpeed * currentSpeedMultiplier;
        transform.position += direction * currentSpeed * dt;

        // 不規則な上下フラッター（バットらしさ）。差分ベースで適用し、直進成分とは独立させる
        transform.position -= new Vector3(0f, currentFlutterOffset, 0f);
        flutterChangeTimer += dt;
        if (flutterChangeTimer >= flutterChangeInterval)
        {
            PickNextFlutterTarget();
        }
        currentFlutterOffset = Mathf.MoveTowards(currentFlutterOffset, targetFlutterOffset, flutterTransitionRate * dt);
        transform.position += new Vector3(0f, currentFlutterOffset, 0f);

        UpdateFrameAnimation(dt);
        if (UpdateFadeOut()) return;
        CheckDespawn();
    }

    /// <summary>
    /// visibleDuration経過後、fadeOutDurationかけて透明化+縮小し、遠ざかって消えるように見せる。
    /// フェード完了時にDestroyした場合はtrueを返す。
    /// </summary>
    private bool UpdateFadeOut()
    {
        if (age < visibleDuration) return false;

        float fadeT = Mathf.Clamp01((age - visibleDuration) / fadeOutDuration);

        if (spriteRenderer != null)
        {
            Color c = spriteRenderer.color;
            c.a = initialAlpha * (1f - fadeT);
            spriteRenderer.color = c;
        }

        transform.localScale = initialLocalScale * Mathf.Lerp(1f, minScaleAtFadeEnd, fadeT);

        if (fadeT >= 1f)
        {
            Destroy(gameObject);
            return true;
        }
        return false;
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

    private void CheckDespawn()
    {
        Camera cam = Camera.main;
        if (cam == null) return;
        Vector3 viewportPos = cam.WorldToViewportPoint(transform.position);
        if (viewportPos.x < -0.3f || viewportPos.x > 1.3f || viewportPos.y < -0.3f || viewportPos.y > 1.3f)
        {
            Destroy(gameObject);
        }
    }
}
