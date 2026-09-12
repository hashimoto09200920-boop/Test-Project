using UnityEngine;

/// <summary>
/// 背景演出用のタンブルウィード（回転草）1個分の挙動。
/// 地面の高さ（baseY）に沿って水平に転がり、移動距離に連動して実際にスプライトを回転させる
/// （コマ送りアニメ不要。回転そのものが「転がって見える」表現になる）。
/// 風の強弱で速度に緩急（Crow/Batと同じ「ランダム間隔で目標倍率が変わる」方式）を付け、
/// 地面の凹凸で跳ねるような上下バウンドを加える。
/// 一定時間後は透明化+縮小しながらフェードアウトして消える（他の演出と同じ方式）。
/// </summary>
public class TumbleweedController : MonoBehaviour
{
    private SpriteRenderer spriteRenderer;

    private Vector3 direction; // 若干の角度を含む単位ベクトル（turnRateDegPerSecでフェード間際に弧を描く、Crowと同じ方式）
    private float baseSpeed;
    private float currentBaseY; // バウンドを乗せる前の基準Y（角度移動ぶん徐々に変化する）

    private float turnRateDegPerSec;
    private float turnStartTime;

    // 風の緩急（ランダムな間隔で目標倍率が変わり、なめらかに追従する）
    private float speedMultiplierMin;
    private float speedMultiplierMax;
    private float speedChangeIntervalMin;
    private float speedChangeIntervalMax;
    private float speedTransitionRate;
    private float currentSpeedMultiplier = 1f;
    private float targetSpeedMultiplier = 1f;
    private float speedChangeTimer;
    private float speedChangeInterval;

    // 転がる回転（移動距離に連動）
    private float rotationDegPerUnit;
    private float currentRotationZ;

    // 地面の凹凸で跳ねるバウンド
    private float bounceAmplitude;
    private float bounceFrequency;
    private float bouncePhase;

    // 一定時間後のフェードアウト（縮小しながら消える）
    private float visibleDuration;
    private float fadeOutDuration;
    private float minScaleAtFadeEnd;
    private float age;
    private Vector3 initialLocalScale;
    private float initialAlpha;

    private float TimeScale =>
        SlowMotionManager.Instance != null ? SlowMotionManager.Instance.TimeScale : 1f;

    public void Init(Vector3 initialDirection, float speed, float groundY,
        float speedMulMin, float speedMulMax, float changeIntervalMin, float changeIntervalMax, float transitionRate,
        float rotationPerUnit, float bounceAmp, float bounceFreq,
        float visibleDur, float fadeOutDur, float minFadeScale,
        float turnRate, float turnLeadTime)
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        initialAlpha = spriteRenderer != null ? spriteRenderer.color.a : 1f;

        direction = initialDirection.normalized;
        baseSpeed = speed;
        currentBaseY = groundY;
        turnRateDegPerSec = turnRate;

        speedMultiplierMin = speedMulMin;
        speedMultiplierMax = speedMulMax;
        speedChangeIntervalMin = changeIntervalMin;
        speedChangeIntervalMax = changeIntervalMax;
        speedTransitionRate = transitionRate;
        currentSpeedMultiplier = Random.Range(speedMultiplierMin, speedMultiplierMax);
        PickNextSpeedTarget();

        rotationDegPerUnit = rotationPerUnit;
        currentRotationZ = Random.Range(0f, 360f);

        bounceAmplitude = bounceAmp;
        bounceFrequency = bounceFreq;
        bouncePhase = Random.Range(0f, Mathf.PI * 2f);

        visibleDuration = visibleDur;
        fadeOutDuration = Mathf.Max(0.05f, fadeOutDur);
        minScaleAtFadeEnd = minFadeScale;
        age = 0f;
        turnStartTime = Mathf.Max(0f, visibleDuration - turnLeadTime);

        initialLocalScale = transform.localScale;
        transform.rotation = Quaternion.Euler(0f, 0f, currentRotationZ);
    }

    private void PickNextSpeedTarget()
    {
        targetSpeedMultiplier = Random.Range(speedMultiplierMin, speedMultiplierMax);
        speedChangeInterval = Random.Range(speedChangeIntervalMin, speedChangeIntervalMax);
        speedChangeTimer = 0f;
    }

    private void Update()
    {
        float dt = Time.deltaTime * TimeScale;
        age += dt;

        // 風の緩急：ランダムな間隔で目標倍率を変え、なめらかに追従させる
        speedChangeTimer += dt;
        if (speedChangeTimer >= speedChangeInterval) PickNextSpeedTarget();
        currentSpeedMultiplier = Mathf.MoveTowards(currentSpeedMultiplier, targetSpeedMultiplier, speedTransitionRate * dt);

        // フェードアウト開始の少し前からのみ、進行方向をゆっくり回転させ弧を描く（Crowと同じ方式）
        if (turnRateDegPerSec != 0f && age >= turnStartTime)
            direction = Quaternion.Euler(0f, 0f, turnRateDegPerSec * dt) * direction;

        float currentSpeed = baseSpeed * currentSpeedMultiplier;
        Vector3 delta = direction * currentSpeed * dt;
        transform.position += new Vector3(delta.x, 0f, 0f);
        currentBaseY += delta.y;

        // 転がる回転：水平移動距離に比例させ、進行方向で回転向きが正しくなるようにする
        currentRotationZ -= delta.x * rotationDegPerUnit;

        // 地面の凹凸で跳ねるバウンド（常に上向きのホップ）
        bouncePhase += dt * bounceFrequency * Mathf.PI * 2f;
        float bounceY = Mathf.Abs(Mathf.Sin(bouncePhase)) * bounceAmplitude;

        Vector3 pos = transform.position;
        pos.y = currentBaseY + bounceY;
        transform.position = pos;
        transform.rotation = Quaternion.Euler(0f, 0f, currentRotationZ);

        if (UpdateFadeOut()) return;
        CheckDespawn();
    }

    /// <summary>
    /// visibleDuration経過後、fadeOutDurationかけて透明化+縮小し、消えるように見せる。
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

        // 回転は保ったままスケールだけ縮める
        Vector3 s = initialLocalScale * Mathf.Lerp(1f, minScaleAtFadeEnd, fadeT);
        transform.localScale = s;

        if (fadeT >= 1f)
        {
            Destroy(gameObject);
            return true;
        }
        return false;
    }

    private void CheckDespawn()
    {
        Camera cam = Camera.main;
        if (cam == null) return;
        Vector3 viewportPos = cam.WorldToViewportPoint(transform.position);
        if (viewportPos.x < -0.3f || viewportPos.x > 1.3f)
        {
            Destroy(gameObject);
        }
    }
}
