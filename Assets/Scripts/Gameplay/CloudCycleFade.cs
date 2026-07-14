using UnityEngine;
using System.Collections;

// 複数の雲パターンを、A→B→C→…→Aとループしながらクロスフェードし続ける演出。
// 各パターンごとに個別の位置オフセットを持てるよう、表示は2つの子SpriteRenderer
// （layerA/layerB）で行う。自身のGameObjectのtransformは共通の基準位置（Position B）
// として使い、各子にパターンごとのオフセットを乗せる。
// オフセットは必ずワールド座標基準（transform.position + offset）で適用する。
// 親（このGameObject）のlocalScaleが1でない場合、子のlocalPositionにオフセットを
// 入れると親のスケールが掛かって意図しない大きさになってしまうため。
//
// 各パターンは表示されている間（フェードイン〜保持〜フェードアウト）だけ、
// ゆっくり横に流れつつ上下に揺れる。表示時間が区切られている（次のパターンに
// 切り替わって消える）ため、無限スクロールのような継ぎ目対策は不要——だが、
// 表示時間が長い（＝ドリフト距離が伸びる）と、1枚のスプライトだけでは
// 移動した分だけ元画像の端（絵が途切れる境界）が見えてしまう。
// これを隠すため、各レイヤーは移動方向と逆側（＝絵が途切れかけている側）に
// 同じスプライトのコピーをスプライト幅ぶん並べて追従させ、隙間を埋める。
[RequireComponent(typeof(SpriteRenderer))]
public class CloudCycleFade : MonoBehaviour
{
    [Header("Drift / Sway")]
    [Tooltip("横方向にゆっくり流れる速度（ワールド単位/秒）")]
    [Range(0f, 2f)]
    [SerializeField] private float horizontalDriftSpeed = 0.15f;
    [Tooltip("上下に揺れる振幅（ワールド単位）")]
    [Range(0f, 1f)]
    [SerializeField] private float verticalSwayAmplitude = 0.15f;
    [Tooltip("上下に揺れる速さ（Hz）")]
    [Range(0.01f, 1f)]
    [SerializeField] private float verticalSwayFrequency = 0.15f;

    private SpriteRenderer baseRenderer;
    private SpriteRenderer layerA, layerADup;
    private SpriteRenderer layerB, layerBDup;
    private Sprite[] patterns;
    private Vector3[] offsets;
    private float holdDuration;
    private float fadeDuration;
    private Coroutine cycleCoroutine;

    private Vector3 basePosA, basePosB;
    private float activeTimeA, activeTimeB;
    private float driftDirA, driftDirB; // +1=右、-1=左
    private float spriteWidthA, spriteWidthB;

    private float TimeScale =>
        SlowMotionManager.Instance != null ? SlowMotionManager.Instance.TimeScale : 1f;

    /// <summary>
    /// クロスフェード巡回を開始する。patternsが2枚未満なら何もしない。
    /// offsetsはpatternsと同じ順番のパターン別位置オフセット（ワールド座標基準、null/不足分は0扱い）。
    /// </summary>
    public void StartCycle(Sprite[] cyclePatterns, Vector3[] cycleOffsets, float hold, float fade)
    {
        patterns = cyclePatterns;
        offsets = cycleOffsets;
        holdDuration = Mathf.Max(0.1f, hold);
        fadeDuration = Mathf.Max(0.05f, fade);

        if (patterns == null || patterns.Length < 2) return;

        if (baseRenderer == null) baseRenderer = GetComponent<SpriteRenderer>();
        baseRenderer.enabled = false;

        if (layerA == null) layerA = CreateLayer("Background_Silhouette_CycleLayerA");
        if (layerADup == null) layerADup = CreateLayer("Background_Silhouette_CycleLayerA_Dup");
        if (layerB == null) layerB = CreateLayer("Background_Silhouette_CycleLayerB");
        if (layerBDup == null) layerBDup = CreateLayer("Background_Silhouette_CycleLayerB_Dup");

        SetLayerPattern(layerA, ref basePosA, ref activeTimeA, ref driftDirA, ref spriteWidthA, 0);
        layerA.color = new Color(1f, 1f, 1f, 0f);
        layerADup.color = new Color(1f, 1f, 1f, 0f);

        int nextIdx = 1 % patterns.Length;
        SetLayerPattern(layerB, ref basePosB, ref activeTimeB, ref driftDirB, ref spriteWidthB, nextIdx);
        layerB.color = new Color(1f, 1f, 1f, 0f);
        layerBDup.color = new Color(1f, 1f, 1f, 0f);

        if (cycleCoroutine != null) StopCoroutine(cycleCoroutine);
        cycleCoroutine = StartCoroutine(CycleRoutine(true));
    }

    public void StopCycle()
    {
        if (cycleCoroutine != null)
        {
            StopCoroutine(cycleCoroutine);
            cycleCoroutine = null;
        }
        if (baseRenderer != null) baseRenderer.enabled = true;
        if (layerA != null) layerA.color = new Color(1f, 1f, 1f, 0f);
        if (layerADup != null) layerADup.color = new Color(1f, 1f, 1f, 0f);
        if (layerB != null) layerB.color = new Color(1f, 1f, 1f, 0f);
        if (layerBDup != null) layerBDup.color = new Color(1f, 1f, 1f, 0f);
    }

    private void Update()
    {
        if (cycleCoroutine == null) return;

        float dt = Time.deltaTime * TimeScale;
        activeTimeA += dt;
        activeTimeB += dt;

        ApplyDrift(layerA, layerADup, basePosA, activeTimeA, driftDirA, spriteWidthA);
        ApplyDrift(layerB, layerBDup, basePosB, activeTimeB, driftDirB, spriteWidthB);
    }

    private void ApplyDrift(SpriteRenderer layer, SpriteRenderer dup, Vector3 basePos, float t, float dir, float spriteWidth)
    {
        if (layer == null) return;
        float x = basePos.x + dir * horizontalDriftSpeed * t;
        float y = basePos.y + Mathf.Sin(t * verticalSwayFrequency * Mathf.PI * 2f) * verticalSwayAmplitude;
        layer.transform.position = new Vector3(x, y, basePos.z);

        if (dup == null) return;
        // 移動方向と逆側（絵が途切れかけている側）にコピーを並べて隙間を埋める
        dup.transform.position = new Vector3(x - dir * spriteWidth, y, basePos.z);
        if (dup.color.a != layer.color.a)
        {
            Color c = dup.color;
            c.a = layer.color.a;
            dup.color = c;
        }
    }

    private Vector3 GetOffset(int index)
    {
        if (offsets == null || index < 0 || index >= offsets.Length) return Vector3.zero;
        return offsets[index];
    }

    private void SetLayerPattern(SpriteRenderer layer, ref Vector3 basePos, ref float activeTime, ref float driftDir, ref float spriteWidth, int patternIndex)
    {
        layer.sprite = patterns[patternIndex];
        basePos = transform.position + GetOffset(patternIndex);
        activeTime = 0f;
        driftDir = (Random.value < 0.5f) ? -1f : 1f;
        spriteWidth = (layer.sprite != null) ? layer.sprite.bounds.size.x * layer.transform.lossyScale.x : 0f;
        layer.transform.position = basePos;

        SpriteRenderer dup = (layer == layerA) ? layerADup : layerBDup;
        if (dup != null)
        {
            dup.sprite = layer.sprite;
            dup.transform.position = new Vector3(basePos.x - driftDir * spriteWidth, basePos.y, basePos.z);
        }
    }

    private SpriteRenderer CreateLayer(string name)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, true);
        go.transform.localScale = Vector3.one;
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sortingLayerID = baseRenderer.sortingLayerID;
        sr.sortingOrder = baseRenderer.sortingOrder;
        sr.material = baseRenderer.material;
        return sr;
    }

    private IEnumerator CycleRoutine(bool initialFadeIn = false)
    {
        int nextIndex = 1 % patterns.Length;
        bool aIsFront = true; // 現在フルアルファで表示されている方

        if (initialFadeIn)
        {
            float elapsedIn = 0f;
            while (elapsedIn < fadeDuration)
            {
                elapsedIn += Time.deltaTime * TimeScale;
                float t = Mathf.Clamp01(elapsedIn / fadeDuration);
                layerA.color = new Color(1f, 1f, 1f, t);
                layerADup.color = new Color(1f, 1f, 1f, t);
                yield return null;
            }
            layerA.color = new Color(1f, 1f, 1f, 1f);
            layerADup.color = new Color(1f, 1f, 1f, 1f);
        }

        while (true)
        {
            // 保持時間
            float held = 0f;
            while (held < holdDuration)
            {
                held += Time.deltaTime * TimeScale;
                yield return null;
            }

            // 次のパターンを裏側レイヤーにセットしてクロスフェード
            SpriteRenderer front = aIsFront ? layerA : layerB;
            SpriteRenderer frontDup = aIsFront ? layerADup : layerBDup;
            SpriteRenderer back = aIsFront ? layerB : layerA;
            SpriteRenderer backDup = aIsFront ? layerBDup : layerADup;

            if (aIsFront)
                SetLayerPattern(layerB, ref basePosB, ref activeTimeB, ref driftDirB, ref spriteWidthB, nextIndex);
            else
                SetLayerPattern(layerA, ref basePosA, ref activeTimeA, ref driftDirA, ref spriteWidthA, nextIndex);

            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime * TimeScale;
                float t = Mathf.Clamp01(elapsed / fadeDuration);
                front.color = new Color(1f, 1f, 1f, 1f - t);
                frontDup.color = new Color(1f, 1f, 1f, 1f - t);
                back.color = new Color(1f, 1f, 1f, t);
                backDup.color = new Color(1f, 1f, 1f, t);
                yield return null;
            }
            front.color = new Color(1f, 1f, 1f, 0f);
            frontDup.color = new Color(1f, 1f, 1f, 0f);
            back.color = new Color(1f, 1f, 1f, 1f);
            backDup.color = new Color(1f, 1f, 1f, 1f);

            aIsFront = !aIsFront;
            nextIndex = (nextIndex + 1) % patterns.Length;
        }
    }

    private void OnDisable()
    {
        StopCycle();
    }
}
