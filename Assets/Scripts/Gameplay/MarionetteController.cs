using UnityEngine;

/// <summary>
/// 背景演出用マリオネット1体分の挙動。
/// 天井の固定アンカーから降りてきて(Descending)、その場で揺れながら留まり(Hanging)、
/// 最後に引き上げられながら透明化+縮小してフェードアウトする(Ascending)。
/// 糸はArea5ボスFingers(DollController)と同じ紫→藍→ミントグリーンのグラデーションLineRendererで表現する。
/// 室内演出のためシルエット化はせず、実際のDoll_Idle/Jerk C系スプライトの色をそのまま使う。
/// </summary>
public class MarionetteController : MonoBehaviour
{
    private enum Phase { Descending, Hanging, Ascending }
    private Phase phase;
    private float phaseTimer;

    private SpriteRenderer spriteRenderer;
    private LineRenderer stringRenderer;
    private Gradient baseStringGradient;

    // ★ApplyStringAlpha()で毎フレームnewしていた分の使い回し用キャッシュ（GCアロケーション対策）
    private GradientColorKey[] _cachedStringColorKeys;
    private GradientAlphaKey[] _cachedStringBaseAlphaKeys;
    private GradientAlphaKey[] _scaledStringAlphaKeysBuffer;
    private Gradient _scaledStringGradientReusable;

    private Vector3 anchorPos;   // 天井側の固定接続点（画面上端よりさらに上）
    private float targetY;
    private float descendDuration;
    private float hangDuration;
    private float fadeOutDuration; // 引き上げ+フェード+縮小にかける時間
    private float minScaleAtFadeEnd;

    private float swayAmplitude;
    private float swayFrequency;
    private float swayPhase;

    private float stringSagAmount;
    private int stringSegments;
    private Vector2 stringAttachOffset; // スプライトごとに違う「糸が繋がって見える位置」のローカルオフセット
    private int stringSortingOrderOffset;

    private Vector3 initialLocalScale;
    private float baseAlpha;

    private float TimeScale =>
        SlowMotionManager.Instance != null ? SlowMotionManager.Instance.TimeScale : 1f;

    public void Init(Vector3 anchorWorldPos, float targetYPos,
        float descendDur, float hangDur, float retractFadeDur, float minFadeScale,
        float swayAmp, float swayFreq,
        Gradient stringGradient, float stringWidth, int stringSegs, float stringSag, float stringAlpha,
        Vector2 stringOffset, int sortingOrderOffset)
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        baseAlpha = spriteRenderer != null ? spriteRenderer.color.a : 1f;

        anchorPos = anchorWorldPos;
        targetY = targetYPos;
        descendDuration = Mathf.Max(0.05f, descendDur);
        hangDuration = Mathf.Max(0f, hangDur);
        fadeOutDuration = Mathf.Max(0.05f, retractFadeDur);
        minScaleAtFadeEnd = minFadeScale;

        swayAmplitude = swayAmp;
        swayFrequency = swayFreq;
        swayPhase = Random.Range(0f, Mathf.PI * 2f);

        stringSagAmount = stringSag;
        stringSegments = Mathf.Max(2, stringSegs);
        stringAttachOffset = stringOffset;
        stringSortingOrderOffset = sortingOrderOffset;

        initialLocalScale = transform.localScale;

        transform.position = new Vector3(anchorPos.x, anchorPos.y, 0f);
        phase = Phase.Descending;
        phaseTimer = 0f;

        SetupString(stringGradient, stringWidth, stringAlpha);
        UpdateString();
    }

    private void SetupString(Gradient gradient, float width, float alphaBaseline)
    {
        // stringGradient(Inspector編集用の元データ)はそのままに、
        // baseStringGradientは不透明度の基準値(alphaBaseline)を焼き込んだ複製として保持する
        GradientAlphaKey[] srcAlpha = gradient.alphaKeys;
        GradientAlphaKey[] scaledAlpha = new GradientAlphaKey[srcAlpha.Length];
        for (int i = 0; i < srcAlpha.Length; i++)
            scaledAlpha[i] = new GradientAlphaKey(srcAlpha[i].alpha * alphaBaseline, srcAlpha[i].time);

        baseStringGradient = new Gradient();
        baseStringGradient.SetKeys(gradient.colorKeys, scaledAlpha);

        // ★ここで1回だけ確保し、ApplyStringAlpha()では使い回す（毎フレームnewしない）
        _cachedStringColorKeys = baseStringGradient.colorKeys;
        _cachedStringBaseAlphaKeys = baseStringGradient.alphaKeys;
        _scaledStringAlphaKeysBuffer = new GradientAlphaKey[_cachedStringBaseAlphaKeys.Length];
        for (int i = 0; i < _scaledStringAlphaKeysBuffer.Length; i++)
            _scaledStringAlphaKeysBuffer[i].time = _cachedStringBaseAlphaKeys[i].time;
        _scaledStringGradientReusable = new Gradient();

        GameObject stringGo = new GameObject("MarionetteString");
        // ドール本体（フェード時に縮小する）に巻き込まれないよう、糸は独立オブジェクトにする
        stringRenderer = stringGo.AddComponent<LineRenderer>();
        stringRenderer.positionCount = stringSegments;
        stringRenderer.useWorldSpace = true;
        stringRenderer.widthMultiplier = 1f;
        stringRenderer.startWidth = width;
        stringRenderer.endWidth = width;
        stringRenderer.numCapVertices = 4;
        stringRenderer.colorGradient = baseStringGradient;
        stringRenderer.material = new Material(Shader.Find("Sprites/Default"));
        if (spriteRenderer != null)
        {
            stringRenderer.sortingLayerName = spriteRenderer.sortingLayerName;
            // 人形を必ず糸より前面にするため、意図的に大きめのオフセットを取る（Inspectorで調整可）
            stringRenderer.sortingOrder = spriteRenderer.sortingOrder + stringSortingOrderOffset;
        }
    }

    /// <summary>
    /// Stage切り替え等で、Hangingを待たずに即座に引き上げ(Ascending)フェーズへ移行させる
    /// （外部のSpawnerから呼ぶ）。既にAscending中なら何もしない
    /// </summary>
    public void ForceFadeOut()
    {
        if (phase == Phase.Ascending) return;
        phase = Phase.Ascending;
        phaseTimer = 0f;
    }

    private void Update()
    {
        float dt = Time.deltaTime * TimeScale;
        phaseTimer += dt;
        swayPhase += dt * swayFrequency * Mathf.PI * 2f;
        float swayX = Mathf.Sin(swayPhase) * swayAmplitude;

        float y;
        float stringAlphaMul = 1f;

        switch (phase)
        {
            case Phase.Descending:
            {
                float t = Mathf.Clamp01(phaseTimer / descendDuration);
                float eased = 1f - (1f - t) * (1f - t); // ease-out
                y = Mathf.Lerp(anchorPos.y, targetY, eased);
                if (t >= 1f) { phase = Phase.Hanging; phaseTimer = 0f; }
                break;
            }
            case Phase.Hanging:
            {
                y = targetY;
                if (phaseTimer >= hangDuration) { phase = Phase.Ascending; phaseTimer = 0f; }
                break;
            }
            default: // Ascending
            {
                float t = Mathf.Clamp01(phaseTimer / fadeOutDuration);
                float eased = t * t; // ease-in（引き上げが加速していく感じ）
                y = Mathf.Lerp(targetY, anchorPos.y, eased);
                stringAlphaMul = 1f - t;

                if (spriteRenderer != null)
                {
                    Color c = spriteRenderer.color;
                    c.a = baseAlpha * (1f - t);
                    spriteRenderer.color = c;
                }
                transform.localScale = initialLocalScale * Mathf.Lerp(1f, minScaleAtFadeEnd, t);

                if (t >= 1f)
                {
                    if (stringRenderer != null) Destroy(stringRenderer.gameObject);
                    Destroy(gameObject);
                    return;
                }
                break;
            }
        }

        transform.position = new Vector3(anchorPos.x + swayX, y, 0f);
        UpdateString();
        ApplyStringAlpha(stringAlphaMul);
    }

    private void UpdateString()
    {
        if (stringRenderer == null) return;

        Vector3 top = anchorPos;
        // stringAttachOffsetはドールのローカル空間のオフセット。TransformPointで反転/縮小に自動追従させる
        Vector3 bottom = transform.TransformPoint(new Vector3(stringAttachOffset.x, stringAttachOffset.y, 0f));
        for (int i = 0; i < stringSegments; i++)
        {
            float t = (stringSegments <= 1) ? 0f : (float)i / (stringSegments - 1);
            Vector3 p = Vector3.Lerp(top, bottom, t);
            p.y -= stringSagAmount * 4f * t * (1f - t); // 中間がたわむ
            // sortingOrderに加え、Z位置でも人形より奥に押し出す二重対策（TransparencySortMode差異の保険）
            p.z = 0.05f;
            stringRenderer.SetPosition(i, p);
        }
    }

    private void ApplyStringAlpha(float alphaMul)
    {
        if (stringRenderer == null || baseStringGradient == null || alphaMul >= 0.999f) return;

        // ★以前はここで毎フレームGradientAlphaKey[]とGradientをnewしており、フェード中(数秒間)
        //   ずっとGCアロケーションが発生し続けていた。使い回し用バッファの値だけ書き換える。
        for (int i = 0; i < _scaledStringAlphaKeysBuffer.Length; i++)
            _scaledStringAlphaKeysBuffer[i].alpha = _cachedStringBaseAlphaKeys[i].alpha * alphaMul;

        _scaledStringGradientReusable.SetKeys(_cachedStringColorKeys, _scaledStringAlphaKeysBuffer);
        stringRenderer.colorGradient = _scaledStringGradientReusable;
    }

    private void OnDestroy()
    {
        if (stringRenderer != null) Destroy(stringRenderer.gameObject);
    }
}
