using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class DriftScroll : MonoBehaviour
{
    [Header("Scroll")]
    [Tooltip("基本スクロール速度（ワールド単位/秒）")]
    [Range(0.1f, 5.0f)]
    [SerializeField] private float scrollSpeed = 0.5f;

    [Header("Speed Variation")]
    [Tooltip("速度変化の幅（この値分だけ増減する）")]
    [Range(0f, 3.0f)]
    [SerializeField] private float speedVariation = 0.3f;

    [Tooltip("速度変化の周期（Hz）")]
    [Range(0.01f, 0.5f)]
    [SerializeField] private float speedWaveFrequency = 0.05f;

    [Header("Alpha Pulse")]
    [Tooltip("透明度の最小値")]
    [Range(0f, 1f)]
    [SerializeField] private float minAlpha = 0.3f;

    [Tooltip("透明度の最大値")]
    [Range(0f, 1f)]
    [SerializeField] private float maxAlpha = 0.9f;

    [Tooltip("透明度変化の周期（Hz）")]
    [Range(0.01f, 0.5f)]
    [SerializeField] private float pulseFrequency = 0.08f;

    [Header("Vertical Wave")]
    [Tooltip("上下揺らぎの振幅（ワールド単位）。0=無効")]
    [Range(0f, 1.0f)]
    [SerializeField] private float waveAmplitude = 0.1f;

    [Tooltip("上下揺らぎの速さ（Hz）")]
    [Range(0.05f, 1.0f)]
    [SerializeField] private float waveFrequency = 0.15f;

    [Header("Second Layer (Dual Speed)")]
    [Tooltip("第2レイヤーを有効にする")]
    [SerializeField] private bool enableSecondLayer = true;

    [Tooltip("第2レイヤーの速度倍率（1より小さいと遅く＝奥に見える）")]
    [Range(0.1f, 2.0f)]
    [SerializeField] private float layer2SpeedMultiplier = 0.4f;

    [Tooltip("第2レイヤーの透明度倍率（アルファパルスにこの値を掛ける）")]
    [Range(0f, 1f)]
    [SerializeField] private float layer2AlphaMultiplier = 0.5f;

    [Tooltip("第2レイヤーのアルファパルス位相オフセット（0〜1）")]
    [Range(0f, 1f)]
    [SerializeField] private float layer2PulseOffset = 0.5f;

    // Layer 1
    private SpriteRenderer sr;
    private SpriteRenderer copySR;
    private Transform copyTransform;
    private float tileWidth;
    private float baseY;

    // Layer 2
    private SpriteRenderer layer2SR;
    private SpriteRenderer layer2CopySR;
    private Transform layer2Transform;
    private Transform layer2CopyTransform;

    private float waveTime;
    private bool initialized;

    private float TimeScale =>
        SlowMotionManager.Instance != null ? SlowMotionManager.Instance.TimeScale : 1f;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
    }

    private void Update()
    {
        if (!initialized)
        {
            if (sr.sprite == null) return;
            Initialize();
        }

        if (copyTransform == null) return;

        float dt = Time.deltaTime * TimeScale;
        waveTime += dt;

        // 速度変化
        float currentSpeed = scrollSpeed + Mathf.Sin(waveTime * speedWaveFrequency * Mathf.PI * 2f) * speedVariation;
        currentSpeed = Mathf.Max(0.05f, currentSpeed);

        // 上下揺れ
        float sineY = baseY + Mathf.Sin(waveTime * waveFrequency * Mathf.PI * 2f) * waveAmplitude;

        // アルファパルス（Layer1）
        float alpha1 = Mathf.Lerp(minAlpha, maxAlpha, (Mathf.Sin(waveTime * pulseFrequency * Mathf.PI * 2f) + 1f) * 0.5f);

        // --- Layer 1 ---
        float move1 = currentSpeed * dt;
        transform.position += Vector3.left * move1;
        copyTransform.position += Vector3.left * move1;

        transform.position = new Vector3(transform.position.x, sineY, transform.position.z);
        copyTransform.position = new Vector3(copyTransform.position.x, sineY, copyTransform.position.z);

        sr.color = new Color(1f, 1f, 1f, alpha1);
        if (copySR != null) copySR.color = new Color(1f, 1f, 1f, alpha1);

        float camLeft = Camera.main.transform.position.x - Camera.main.orthographicSize * Camera.main.aspect;
        if (transform.position.x + tileWidth * 0.5f < camLeft)
            transform.position = new Vector3(copyTransform.position.x + tileWidth, sineY, transform.position.z);
        else if (copyTransform.position.x + tileWidth * 0.5f < camLeft)
            copyTransform.position = new Vector3(transform.position.x + tileWidth, sineY, copyTransform.position.z);

        // --- Layer 2 ---
        if (!enableSecondLayer || layer2Transform == null) return;

        float speed2 = currentSpeed * layer2SpeedMultiplier;
        float move2 = speed2 * dt;
        layer2Transform.position += Vector3.left * move2;
        layer2CopyTransform.position += Vector3.left * move2;

        float sineY2 = baseY + Mathf.Sin((waveTime + 1f) * waveFrequency * Mathf.PI * 2f) * waveAmplitude;
        layer2Transform.position = new Vector3(layer2Transform.position.x, sineY2, layer2Transform.position.z);
        layer2CopyTransform.position = new Vector3(layer2CopyTransform.position.x, sineY2, layer2CopyTransform.position.z);

        float alpha2 = Mathf.Lerp(minAlpha, maxAlpha,
            (Mathf.Sin((waveTime + layer2PulseOffset / pulseFrequency) * pulseFrequency * Mathf.PI * 2f) + 1f) * 0.5f)
            * layer2AlphaMultiplier;
        if (layer2SR != null) layer2SR.color = new Color(1f, 1f, 1f, alpha2);
        if (layer2CopySR != null) layer2CopySR.color = new Color(1f, 1f, 1f, alpha2);

        if (layer2Transform.position.x + tileWidth * 0.5f < camLeft)
            layer2Transform.position = new Vector3(layer2CopyTransform.position.x + tileWidth, sineY2, layer2Transform.position.z);
        else if (layer2CopyTransform.position.x + tileWidth * 0.5f < camLeft)
            layer2CopyTransform.position = new Vector3(layer2Transform.position.x + tileWidth, sineY2, layer2CopyTransform.position.z);
    }

    private void Initialize()
    {
        tileWidth = sr.sprite.bounds.size.x * transform.lossyScale.x;

        Camera cam = Camera.main;
        float camLeft = cam.transform.position.x - cam.orthographicSize * cam.aspect;
        transform.position = new Vector3(camLeft + tileWidth * 0.5f, transform.position.y, transform.position.z);
        baseY = transform.position.y;

        // Layer 1 コピー
        GameObject copy1 = new GameObject("Background_Mid_Copy");
        copy1.transform.SetParent(transform.parent);
        copy1.transform.position = new Vector3(transform.position.x + tileWidth, transform.position.y, transform.position.z);
        copy1.transform.localScale = transform.localScale;
        copySR = copy1.AddComponent<SpriteRenderer>();
        copySR.sprite = sr.sprite;
        copySR.material = sr.material;
        copySR.sortingLayerID = sr.sortingLayerID;
        copySR.sortingOrder = sr.sortingOrder;
        copyTransform = copy1.transform;

        if (!enableSecondLayer) { initialized = true; return; }

        // Layer 2 本体（奥に配置）
        GameObject l2 = new GameObject("Background_Mid_Layer2");
        l2.transform.SetParent(transform.parent);
        l2.transform.position = new Vector3(camLeft + tileWidth * 0.5f, transform.position.y, transform.position.z);
        l2.transform.localScale = transform.localScale;
        layer2SR = l2.AddComponent<SpriteRenderer>();
        layer2SR.sprite = sr.sprite;
        layer2SR.material = sr.material;
        layer2SR.sortingLayerID = sr.sortingLayerID;
        layer2SR.sortingOrder = sr.sortingOrder - 1;
        layer2Transform = l2.transform;

        // Layer 2 コピー
        GameObject l2Copy = new GameObject("Background_Mid_Layer2_Copy");
        l2Copy.transform.SetParent(transform.parent);
        l2Copy.transform.position = new Vector3(camLeft + tileWidth * 1.5f, transform.position.y, transform.position.z);
        l2Copy.transform.localScale = transform.localScale;
        layer2CopySR = l2Copy.AddComponent<SpriteRenderer>();
        layer2CopySR.sprite = sr.sprite;
        layer2CopySR.material = sr.material;
        layer2CopySR.sortingLayerID = sr.sortingLayerID;
        layer2CopySR.sortingOrder = sr.sortingOrder - 1;
        layer2CopyTransform = l2Copy.transform;

        initialized = true;
    }

    private void OnDestroy()
    {
        if (copyTransform != null) Destroy(copyTransform.gameObject);
        if (layer2Transform != null) Destroy(layer2Transform.gameObject);
        if (layer2CopyTransform != null) Destroy(layer2CopyTransform.gameObject);
    }
}
