using UnityEngine;

/// <summary>
/// Area8の背景演出：岩/氷の欠片1個分の挙動。
/// VortexScroll（MidLayerの雲）と同じ考え方（ランダムな速度をランダムな時間だけ維持し続ける
/// 緩急のある上昇速度＋その上昇速度に連動した左右ウェーブ）を、小さな欠片にも適用する。
/// 宙を漂う欠片のため、常に緩やかに回転させる。
/// 一定時間後は透明化+縮小しながらフェードアウトして消える（他の演出と同じ方式）。
/// </summary>
public class DebrisController : MonoBehaviour
{
    private SpriteRenderer spriteRenderer;

    private float baseX;
    private float wavePhase;
    private float waveAmplitude;
    private float waveFrequency;

    // 上昇速度の緩急（VortexScrollと同じ：ランダム目標速度をランダムな時間だけ維持する）
    private float minSpeed;
    private float maxSpeed;
    private float minHoldDuration;
    private float maxHoldDuration;
    private float speedTransitionRate;
    private float currentSpeed;
    private float targetSpeed;
    private float holdTimer;

    // 宙を漂う回転
    private float rotationSpeed;

    // 一定時間後のフェードアウト（縮小しながら消える）
    private float visibleDuration;
    private float fadeOutDuration;
    private float minScaleAtFadeEnd;
    private float age;
    private Vector3 initialLocalScale;
    private float initialAlpha;

    private float TimeScale =>
        SlowMotionManager.Instance != null ? SlowMotionManager.Instance.TimeScale : 1f;

    public void Init(float minSpd, float maxSpd, float minHold, float maxHold, float transitionRate,
        float waveAmp, float waveFreq, float rotSpeedRange,
        float visibleDur, float fadeOutDur, float minFadeScale)
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        initialAlpha = spriteRenderer != null ? spriteRenderer.color.a : 1f;

        baseX = transform.position.x;
        wavePhase = Random.Range(0f, 360f);
        waveAmplitude = waveAmp;
        waveFrequency = waveFreq;

        minSpeed = minSpd;
        maxSpeed = maxSpd;
        minHoldDuration = minHold;
        maxHoldDuration = maxHold;
        speedTransitionRate = transitionRate;
        currentSpeed = minSpeed;
        targetSpeed = Random.Range(minSpeed, maxSpeed);
        holdTimer = Random.Range(minHoldDuration, maxHoldDuration);

        rotationSpeed = Random.Range(-rotSpeedRange, rotSpeedRange);

        visibleDuration = visibleDur;
        fadeOutDuration = Mathf.Max(0.05f, fadeOutDur);
        minScaleAtFadeEnd = minFadeScale;
        age = 0f;

        initialLocalScale = transform.localScale;
        transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
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

        // 上昇速度の緩急：維持時間が切れたら新しい目標速度を抽選し直す（VortexScrollと同じ方式）
        holdTimer -= dt;
        if (holdTimer <= 0f)
        {
            targetSpeed = Random.Range(minSpeed, maxSpeed);
            holdTimer = Random.Range(minHoldDuration, maxHoldDuration);
        }
        currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, speedTransitionRate * dt);

        // 上昇速度に連動した左右ウェーブ（速いほど揺れも大きく速く感じる、VortexScrollと同じ式）
        wavePhase += currentSpeed * dt * waveFrequency;
        float waveX = Mathf.Sin(wavePhase * Mathf.Deg2Rad) * waveAmplitude;

        Vector3 pos = transform.position;
        pos.y += currentSpeed * dt;
        pos.x = baseX + waveX;
        transform.position = pos;

        transform.Rotate(0f, 0f, rotationSpeed * dt);

        if (UpdateFadeOut()) return;
        CheckDespawn();
    }

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

    private void CheckDespawn()
    {
        Camera cam = Camera.main;
        if (cam == null) return;
        Vector3 viewportPos = cam.WorldToViewportPoint(transform.position);
        if (viewportPos.y > 1.3f)
        {
            Destroy(gameObject);
        }
    }
}
