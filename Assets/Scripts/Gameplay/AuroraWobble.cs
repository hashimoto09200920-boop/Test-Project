using UnityEngine;
using System.Collections;

/// <summary>
/// オーロラSpriteに緩やかな上下の揺らぎ・横方向の伸縮・アルファの明滅を加えて、
/// 揺らめいているように見せる。
///
/// auroraPatternsを2枚以上設定すると、2枚の子SpriteRenderer(layerA/layerB)を使って
/// パターンを巡回しながらクロスフェードする。揺らぎ・伸縮は親(このGameObject)の
/// transformに適用するため子にもそのまま反映される。明滅アルファは「クロスフェードの
/// 重み×明滅アルファ」を各レイヤーに乗算することで、クロスフェード中も明滅を維持する。
/// クロスフェードは前面と背面のタイミングをずらせるようにしてあり(overlap)、
/// 両方が同時に高い不透明度で重なって白っぽく見える問題を避けられる。
/// ★ランダムな位置・サイズのジッターは入れない（過去に不具合の原因になったため廃止）。
/// パターンごとの位置調整はこのコンポーネントでは行わず、EarthLayerFitter側
/// （auroraXOffset等、他の位置調整項目と同じ場所）に集約している。
/// 空/1枚以下なら従来通り自分自身のSpriteRendererだけで明滅させる。
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class AuroraWobble : MonoBehaviour
{
    [Header("上下の揺らぎ")]
    [Tooltip("上下に揺れる幅（ワールド単位）")]
    [SerializeField] private float verticalAmplitude = 0.08f;
    [Tooltip("上下に揺れる速さ（Hz）")]
    [SerializeField] private float verticalSpeed = 0.25f;

    [Header("横方向の伸縮（呼吸するような幅の変化）")]
    [Tooltip("横幅が伸縮する割合（0.03=±3%）")]
    [SerializeField] private float stretchAmplitude = 0.03f;
    [Tooltip("伸縮の速さ（Hz）")]
    [SerializeField] private float stretchSpeed = 0.18f;

    [Header("明滅（アルファ）")]
    [SerializeField, Range(0f, 1f)] private float alphaMin = 0.75f;
    [SerializeField, Range(0f, 1f)] private float alphaMax = 1f;
    [Tooltip("明滅の速さ（Hz）")]
    [SerializeField] private float alphaSpeed = 0.35f;

    [Header("パターン巡回（2枚以上で有効）")]
    [Tooltip("2枚以上設定すると、この順番でクロスフェードを巡回する")]
    [SerializeField] private Sprite[] auroraPatterns;
    [Tooltip("各パターンを表示し続ける時間（秒）")]
    [SerializeField] private float patternHoldDuration = 6f;
    [Tooltip("次のパターンへクロスフェードする時間（秒）")]
    [SerializeField] private float patternFadeDuration = 2f;
    [Range(0f, 1f)]
    [Tooltip("クロスフェード中、前面と背面が重なって同時に見える度合い。1=常に合計100%になる完全な重なり" +
             "（両方のオーロラが同時に薄く見えて明るくなりやすい）。0=前面が消え切ってから背面が現れる（重なりなし）")]
    [SerializeField] private float patternCrossfadeOverlap = 0.4f;

    private SpriteRenderer sr;
    private SpriteRenderer layerA;
    private SpriteRenderer layerB;
    private float weightA = 1f;
    private float weightB = 0f;
    private bool multiPatternActive;

    private Vector3 basePosition;
    private Vector3 baseScale;
    private float t;

    private float TimeScale =>
        SlowMotionManager.Instance != null ? SlowMotionManager.Instance.TimeScale : 1f;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        basePosition = transform.localPosition;
        baseScale = transform.localScale;
        // ★複数のオーロラを並べても全部同じ動きにならないよう、開始位相をランダムにする
        t = Random.Range(0f, 100f);
    }

    private void Start()
    {
        if (auroraPatterns != null && auroraPatterns.Length >= 2)
        {
            multiPatternActive = true;
            sr.enabled = false;

            layerA = CreateLayer("AuroraLayerA");
            layerB = CreateLayer("AuroraLayerB");
            layerA.sprite = auroraPatterns[0];
            layerB.sprite = auroraPatterns[1 % auroraPatterns.Length];
            weightA = 1f;
            weightB = 0f;

            StartCoroutine(CycleRoutine());
        }
    }

    // ★同名の子が既に存在すればそれを再利用する（新規生成しない）。重複生成防止の安全対策。
    private SpriteRenderer CreateLayer(string name)
    {
        Transform existing = transform.Find(name);
        if (existing != null)
        {
            SpriteRenderer existingLayer = existing.GetComponent<SpriteRenderer>();
            if (existingLayer != null) return existingLayer;
        }

        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.localScale = Vector3.one;
        go.transform.localRotation = Quaternion.identity;
        SpriteRenderer layer = go.AddComponent<SpriteRenderer>();
        layer.sortingLayerID = sr.sortingLayerID;
        layer.sortingOrder = sr.sortingOrder;
        layer.material = sr.material;
        layer.maskInteraction = sr.maskInteraction;
        return layer;
    }

    private IEnumerator CycleRoutine()
    {
        int currentIndex = 0;
        int nextIndex = 1 % auroraPatterns.Length;
        bool aIsFront = true;

        while (true)
        {
            float held = 0f;
            while (held < patternHoldDuration)
            {
                held += Time.deltaTime * TimeScale;
                yield return null;
            }

            SpriteRenderer front = aIsFront ? layerA : layerB;
            SpriteRenderer back = aIsFront ? layerB : layerA;
            back.sprite = auroraPatterns[nextIndex];

            float duration = Mathf.Max(0.05f, patternFadeDuration);
            float overlap = Mathf.Clamp01(patternCrossfadeOverlap);
            float outEnd = duration * (0.5f + overlap * 0.5f);
            float inStart = duration * (0.5f - overlap * 0.5f);
            float inSpan = Mathf.Max(0.001f, duration - inStart);

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime * TimeScale;
                float outT = Mathf.Clamp01(elapsed / outEnd);
                float inT = Mathf.Clamp01((elapsed - inStart) / inSpan);
                if (aIsFront) { weightA = 1f - outT; weightB = inT; }
                else { weightB = 1f - outT; weightA = inT; }
                yield return null;
            }
            if (aIsFront) { weightA = 0f; weightB = 1f; }
            else { weightB = 0f; weightA = 1f; }

            aIsFront = !aIsFront;
            currentIndex = nextIndex;
            nextIndex = (nextIndex + 1) % auroraPatterns.Length;
        }
    }

    private void Update()
    {
        t += Time.deltaTime * TimeScale;

        float y = Mathf.Sin(t * verticalSpeed * Mathf.PI * 2f) * verticalAmplitude;
        transform.localPosition = basePosition + new Vector3(0f, y, 0f);

        float stretch = 1f + Mathf.Sin(t * stretchSpeed * Mathf.PI * 2f) * stretchAmplitude;
        transform.localScale = new Vector3(baseScale.x * stretch, baseScale.y, baseScale.z);

        float alphaWave = (Mathf.Sin(t * alphaSpeed * Mathf.PI * 2f) + 1f) * 0.5f;
        float breathingAlpha = Mathf.Lerp(alphaMin, alphaMax, alphaWave);

        if (multiPatternActive)
        {
            SetAlpha(layerA, breathingAlpha * weightA);
            SetAlpha(layerB, breathingAlpha * weightB);
        }
        else
        {
            SetAlpha(sr, breathingAlpha);
        }
    }

    private static void SetAlpha(SpriteRenderer renderer, float alpha)
    {
        Color c = renderer.color;
        c.a = alpha;
        renderer.color = c;
    }
}
