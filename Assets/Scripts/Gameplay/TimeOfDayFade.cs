using UnityEngine;
using System.Collections;

// FarLayer用の時間帯クロスフェード演出。昼→夕方→夜→早朝→(昼に戻る)…と
// ループしながら、パターンごとに個別の保持時間でクロスフェードし続ける。
// CloudCycleFadeと違い、FarLayerは画面全体を覆う静止グラデーションのため、
// ドリフト/Sway・端の隙間隠しコピーは不要（常に画面を覆っているため）。
[RequireComponent(typeof(SpriteRenderer))]
public class TimeOfDayFade : MonoBehaviour
{
    private SpriteRenderer baseRenderer;
    private SpriteRenderer layerA;
    private SpriteRenderer layerB;
    private Sprite[] patterns;
    private float[] holdDurations; // patternsと同じ順番・同じ数。パターンごとの保持時間（秒）
    private float fadeDuration;
    private float overlap = 1f; // 1=前面と背面が全期間重なって同時に見える(従来通り)。0=前面が消え切ってから背面が現れる(重なりなし)
    private Coroutine cycleCoroutine;

    private float TimeScale =>
        SlowMotionManager.Instance != null ? SlowMotionManager.Instance.TimeScale : 1f;

    /// <summary>
    /// クロスフェード巡回を開始する。patternsが2枚未満なら何もしない。
    /// holdDurationsはpatternsと同じ順番・同じ数（不足分は最後の値を使い回す）。
    /// overlapDegree: 1=従来通り前面・背面のαが常に合計100%になる完全な重なり方。
    /// 0にすると前面が完全に消えてから背面が現れ始める（重なる瞬間＝両方の星が同時に薄く見える瞬間がなくなる）。
    /// 省略時は1（既存の呼び出し元の見え方を変えない）。
    /// </summary>
    public void StartCycle(Sprite[] cyclePatterns, float[] cycleHoldDurations, float fade, float overlapDegree = 1f)
    {
        patterns = cyclePatterns;
        holdDurations = cycleHoldDurations;
        fadeDuration = Mathf.Max(0.05f, fade);
        overlap = Mathf.Clamp01(overlapDegree);

        Debug.Log($"[TimeOfDayFade] StartCycle called on {gameObject.name}: patterns={(patterns == null ? "null" : patterns.Length.ToString())} fade={fadeDuration} overlap={overlap}");

        if (patterns == null || patterns.Length < 2) return;

        if (baseRenderer == null) baseRenderer = GetComponent<SpriteRenderer>();
        baseRenderer.enabled = false;

        if (layerA == null) layerA = CreateLayer("Background_Far_TimeOfDayLayerA");
        if (layerB == null) layerB = CreateLayer("Background_Far_TimeOfDayLayerB");

        // layerAは既にFarLayerFade等で表示済みの1枚目（Day）を引き継ぐ想定のため、
        // フェードインさせず最初から不透明で開始する
        layerA.sprite = patterns[0];
        layerA.transform.position = transform.position;
        layerA.transform.localScale = Vector3.one;
        layerA.color = new Color(1f, 1f, 1f, 1f);

        int nextIdx = 1 % patterns.Length;
        layerB.sprite = patterns[nextIdx];
        layerB.transform.position = transform.position;
        layerB.transform.localScale = Vector3.one;
        layerB.color = new Color(1f, 1f, 1f, 0f);

        if (cycleCoroutine != null) StopCoroutine(cycleCoroutine);
        cycleCoroutine = StartCoroutine(CycleRoutine());
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
        if (layerB != null) layerB.color = new Color(1f, 1f, 1f, 0f);
    }

    private float GetHoldDuration(int index)
    {
        if (holdDurations == null || holdDurations.Length == 0) return 4f;
        int i = Mathf.Clamp(index, 0, holdDurations.Length - 1);
        return Mathf.Max(0.1f, holdDurations[i]);
    }

    // ★同名の子が既に存在すればそれを再利用する（新規生成しない）。
    //   [ExecuteAlways]環境下でStartCycle()が意図せず複数回呼ばれても、
    //   重複したレイヤーがシーンに増殖しないようにするための安全対策。
    private SpriteRenderer CreateLayer(string name)
    {
        Transform existing = transform.Find(name);
        if (existing != null)
        {
            SpriteRenderer existingSR = existing.GetComponent<SpriteRenderer>();
            if (existingSR != null) return existingSR;
        }

        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, true);
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sortingLayerID = baseRenderer.sortingLayerID;
        sr.sortingOrder = baseRenderer.sortingOrder;
        sr.material = baseRenderer.material;
        return sr;
    }

    private IEnumerator CycleRoutine()
    {
        int currentIndex = 0;
        int nextIndex = 1 % patterns.Length;
        bool aIsFront = true;

        while (true)
        {
            float held = 0f;
            float holdDuration = GetHoldDuration(currentIndex);
            while (held < holdDuration)
            {
                held += Time.deltaTime * TimeScale;
                yield return null;
            }

            SpriteRenderer front = aIsFront ? layerA : layerB;
            SpriteRenderer back = aIsFront ? layerB : layerA;
            back.sprite = patterns[nextIndex];

            yield return StartCoroutine(Fade(back, front, fadeDuration));

            aIsFront = !aIsFront;
            currentIndex = nextIndex;
            nextIndex = (nextIndex + 1) % patterns.Length;
        }
    }

    // inをフェードイン、outをフェードアウト。overlapが1未満の場合、outが先に多く消えてから
    // inが現れ始めるようにタイミングをずらす（両方が同時に高い不透明度で重なる瞬間を減らすため）
    private IEnumerator Fade(SpriteRenderer inLayer, SpriteRenderer outLayer, float duration)
    {
        // outは[0, outEnd]の間でフェードアウト、inは[inStart, duration]の間でフェードイン。
        // overlap=1: outEnd=duration, inStart=0（従来通り全期間重なる）
        // overlap=0: outEnd=duration/2, inStart=duration/2（重なりゼロ、outが消え切ってからinが現れる）
        float outEnd = duration * (0.5f + overlap * 0.5f);
        float inStart = duration * (0.5f - overlap * 0.5f);
        float inSpan = Mathf.Max(0.001f, duration - inStart);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime * TimeScale;
            float outT = Mathf.Clamp01(elapsed / outEnd);
            float inT = Mathf.Clamp01((elapsed - inStart) / inSpan);
            inLayer.color = new Color(1f, 1f, 1f, inT);
            if (outLayer != null) outLayer.color = new Color(1f, 1f, 1f, 1f - outT);
            yield return null;
        }
        inLayer.color = new Color(1f, 1f, 1f, 1f);
        if (outLayer != null) outLayer.color = new Color(1f, 1f, 1f, 0f);
    }

    private void OnDisable()
    {
        StopCycle();
    }
}
