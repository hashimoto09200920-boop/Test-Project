using UnityEngine;

/// <summary>
/// 背景演出用の巡回ドローン1体分の挙動。
/// CrowControllerと違い、旋回角速度そのものがランダムな間隔で変わり続ける
/// （終始不規則な弧を描く）。進行方向の水平成分に応じて毎フレーム左右反転を更新する。
/// 一定時間後は透明化+縮小しながらフェードアウトして消える（Crow/Butterflyと同じ方式）。
/// 画面外に十分出たら安全策として自動でDestroyする。
/// </summary>
public class DroneFlybyController : MonoBehaviour
{
    private SpriteRenderer spriteRenderer;
    private Sprite[] frames;
    private float frameInterval;
    private float frameTimer;
    private int frameIndex;

    private Vector3 direction;
    private float baseSpeed;

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

    // 不規則な弧（旋回角速度自体がランダムな間隔で変わり続ける）
    private float turnRateRangeMax;
    private float turnChangeIntervalMin;
    private float turnChangeIntervalMax;
    private float turnTransitionRate;
    private float currentTurnRate;
    private float targetTurnRate;
    private float turnChangeTimer;
    private float turnChangeInterval;

    // 左右反転（進行方向の水平成分に応じて毎フレーム更新）
    private bool facingRight;
    private Vector3 initialLocalScale;
    private float currentScaleMul = 1f;

    // 一定時間後のフェードアウト（縮小しながら消える）
    private float visibleDuration;
    private float fadeOutDuration;
    private float minScaleAtFadeEnd;
    private float age;
    private float initialAlpha;
    private float fadeAlphaMultiplier = 1f;

    // 赤く点滅する目（カメラ部分）。シルエット本体とは別レイヤーで加算的に発光させる
    // コマごとにカメラの向きが違う絵のため、オフセットもコマ単位で持つ
    private SpriteRenderer eyeLightRenderer;
    private Vector2[] eyeLocalOffsets;
    private Color eyeLightBaseColor;
    private float eyeLightMinAlpha;
    private float eyeLightMaxAlpha;
    private float eyeLightPulseFrequency;
    private float eyeLightPhase;
    private static Sprite cachedGlowSprite;

    private float TimeScale =>
        SlowMotionManager.Instance != null ? SlowMotionManager.Instance.TimeScale : 1f;

    public void Init(Sprite[] flapFrames, float frameRate, Vector3 initialDirection, float speed,
        float speedMulMin, float speedMulMax, float speedChangeIntervalMinVal, float speedChangeIntervalMaxVal, float speedTransRate,
        float turnRateMax, float turnChangeIntervalMinVal, float turnChangeIntervalMaxVal, float turnTransRate,
        float visibleDur, float fadeOutDur, float minFadeScale,
        Vector2[] eyeOffsetsPerFrame, float eyeSize, Color eyeColor, float eyeMinAlpha, float eyeMaxAlpha, float eyePulseFrequency)
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        initialAlpha = spriteRenderer != null ? spriteRenderer.color.a : 1f;
        frames = flapFrames;
        frameInterval = frameRate > 0f ? 1f / frameRate : 0.1f;
        frameIndex = (frames != null && frames.Length > 0) ? Random.Range(0, frames.Length) : 0;
        if (frames != null && frames.Length > 0 && spriteRenderer != null)
            spriteRenderer.sprite = frames[frameIndex];

        direction = initialDirection.normalized;
        baseSpeed = speed;

        speedMultiplierMin = speedMulMin;
        speedMultiplierMax = speedMulMax;
        speedChangeIntervalMin = speedChangeIntervalMinVal;
        speedChangeIntervalMax = speedChangeIntervalMaxVal;
        speedTransitionRate = speedTransRate;
        currentSpeedMultiplier = Random.Range(speedMultiplierMin, speedMultiplierMax);
        PickNextSpeedTarget();

        turnRateRangeMax = turnRateMax;
        turnChangeIntervalMin = turnChangeIntervalMinVal;
        turnChangeIntervalMax = turnChangeIntervalMaxVal;
        turnTransitionRate = turnTransRate;
        currentTurnRate = Random.Range(-turnRateRangeMax, turnRateRangeMax);
        PickNextTurnTarget();

        visibleDuration = visibleDur;
        fadeOutDuration = Mathf.Max(0.05f, fadeOutDur);
        minScaleAtFadeEnd = minFadeScale;
        age = 0f;

        facingRight = direction.x >= 0f;
        Vector3 s = transform.localScale;
        s.x = Mathf.Abs(s.x) * (facingRight ? 1f : -1f);
        initialLocalScale = s;
        transform.localScale = initialLocalScale;

        SetupEyeLight(eyeOffsetsPerFrame, eyeSize, eyeColor, eyeMinAlpha, eyeMaxAlpha, eyePulseFrequency);
    }

    /// <summary>
    /// 目（カメラ部分）を赤く点滅させる発光ドットを子オブジェクトとして追加する。
    /// 本体はシルエット用に黒でtintされているため、本体と同じSpriteRendererの色では
    /// 赤を表現できない（黒×赤=黒になる）。別レイヤーの加算的な発光ドットで表現する。
    /// コマごとにカメラの向きが違う絵のため、フレーム切り替え時に位置も追従させる。
    /// </summary>
    private void SetupEyeLight(Vector2[] offsetsPerFrame, float size, Color color, float minAlpha, float maxAlpha, float pulseFrequency)
    {
        if (size <= 0f) return;

        eyeLocalOffsets = offsetsPerFrame;

        GameObject eyeGo = new GameObject("EyeLight");
        eyeGo.transform.SetParent(transform, false);
        eyeGo.transform.localPosition = GetEyeOffsetForFrame(frameIndex);
        eyeGo.transform.localScale = Vector3.one * size;

        eyeLightRenderer = eyeGo.AddComponent<SpriteRenderer>();
        eyeLightRenderer.sprite = GetGlowSprite();
        eyeLightBaseColor = color;
        eyeLightRenderer.color = color;
        if (spriteRenderer != null)
        {
            eyeLightRenderer.sortingLayerName = spriteRenderer.sortingLayerName;
            eyeLightRenderer.sortingOrder = spriteRenderer.sortingOrder + 1;
        }

        eyeLightMinAlpha = minAlpha;
        eyeLightMaxAlpha = maxAlpha;
        eyeLightPulseFrequency = pulseFrequency;
        eyeLightPhase = Random.Range(0f, Mathf.PI * 2f);
    }

    private Vector3 GetEyeOffsetForFrame(int index)
    {
        if (eyeLocalOffsets == null || eyeLocalOffsets.Length == 0) return new Vector3(0f, 0f, -0.01f);
        Vector2 offset = eyeLocalOffsets[Mathf.Clamp(index, 0, eyeLocalOffsets.Length - 1)];
        return new Vector3(offset.x, offset.y, -0.01f);
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

    private void PickNextSpeedTarget()
    {
        targetSpeedMultiplier = Random.Range(speedMultiplierMin, speedMultiplierMax);
        speedChangeInterval = Random.Range(speedChangeIntervalMin, speedChangeIntervalMax);
        speedChangeTimer = 0f;
    }

    private void PickNextTurnTarget()
    {
        targetTurnRate = Random.Range(-turnRateRangeMax, turnRateRangeMax);
        turnChangeInterval = Random.Range(turnChangeIntervalMin, turnChangeIntervalMax);
        turnChangeTimer = 0f;
    }

    private void Update()
    {
        float dt = Time.deltaTime * TimeScale;
        age += dt;

        // 速度の緩急
        speedChangeTimer += dt;
        if (speedChangeTimer >= speedChangeInterval) PickNextSpeedTarget();
        currentSpeedMultiplier = Mathf.MoveTowards(currentSpeedMultiplier, targetSpeedMultiplier, speedTransitionRate * dt);

        // 不規則な弧：旋回角速度そのものがランダムな間隔で変わり続ける
        turnChangeTimer += dt;
        if (turnChangeTimer >= turnChangeInterval) PickNextTurnTarget();
        currentTurnRate = Mathf.MoveTowards(currentTurnRate, targetTurnRate, turnTransitionRate * dt);
        direction = Quaternion.Euler(0f, 0f, currentTurnRate * dt) * direction;

        float currentSpeed = baseSpeed * currentSpeedMultiplier;
        transform.position += direction * currentSpeed * dt;

        UpdateFacing();
        UpdateFrameAnimation(dt);
        UpdateEyeLight(dt);
        if (UpdateFadeOut()) return;
        CheckDespawn();
    }

    private void UpdateEyeLight(float dt)
    {
        if (eyeLightRenderer == null) return;

        eyeLightPhase += dt * eyeLightPulseFrequency * Mathf.PI * 2f;
        float t = (Mathf.Sin(eyeLightPhase) + 1f) * 0.5f;
        float alpha = Mathf.Lerp(eyeLightMinAlpha, eyeLightMaxAlpha, t) * fadeAlphaMultiplier;
        eyeLightRenderer.color = new Color(eyeLightBaseColor.r, eyeLightBaseColor.g, eyeLightBaseColor.b, alpha);
    }

    private void UpdateFacing()
    {
        bool shouldFaceRight = direction.x >= 0f;
        if (shouldFaceRight == facingRight) return;
        facingRight = shouldFaceRight;
        initialLocalScale.x = Mathf.Abs(initialLocalScale.x) * (facingRight ? 1f : -1f);
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
            if (eyeLightRenderer != null)
                eyeLightRenderer.transform.localPosition = GetEyeOffsetForFrame(frameIndex);
        }
    }

    /// <summary>
    /// visibleDuration経過後、fadeOutDurationかけて透明化+縮小し、遠ざかって消えるように見せる。
    /// フェード完了時にDestroyした場合はtrueを返す。
    /// </summary>
    private bool UpdateFadeOut()
    {
        if (age >= visibleDuration)
        {
            float fadeT = Mathf.Clamp01((age - visibleDuration) / fadeOutDuration);
            fadeAlphaMultiplier = 1f - fadeT;

            if (spriteRenderer != null)
            {
                Color c = spriteRenderer.color;
                c.a = initialAlpha * fadeAlphaMultiplier;
                spriteRenderer.color = c;
            }

            currentScaleMul = Mathf.Lerp(1f, minScaleAtFadeEnd, fadeT);

            if (fadeT >= 1f)
            {
                Destroy(gameObject);
                return true;
            }
        }

        transform.localScale = initialLocalScale * currentScaleMul;
        return false;
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
