using UnityEngine;

/// <summary>
/// 霧スプライトを2枚並べて左スクロールさせるコンポーネント。
/// 初期化を初回Update()に遅延させることでBackgroundManagerの
/// Start()完了後に確実にスプライトを読み取る。
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class FogScroll : MonoBehaviour
{
    [Tooltip("スクロール速度（ワールド単位/秒）。1.0=穏やか、3.0=明らかに動く、6.0=速い")]
    [Range(0.5f, 10.0f)]
    [SerializeField] private float scrollSpeed = 2.0f;

    [Tooltip("上下揺らぎの振幅（ワールド単位）。0=無効")]
    [Range(0f, 1.0f)]
    [SerializeField] private float waveAmplitude = 0.15f;

    [Tooltip("上下揺らぎの速さ（Hz）")]
    [Range(0.1f, 2.0f)]
    [SerializeField] private float waveFrequency = 0.3f;

    private SpriteRenderer sr;
    private Transform copyTransform;
    private float tileWidth;
    private float baseY;
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
        // 全Start()完了後の初回Updateで初期化（BackgroundManager設定済みを保証）
        if (!initialized)
        {
            if (sr.sprite == null) return; // スプライト未設定のエリアはスクロールしない
            Initialize();
        }

        if (copyTransform == null) return;

        float dt = Time.deltaTime * TimeScale;
        waveTime += dt;

        float move = scrollSpeed * dt;
        transform.position += Vector3.left * move;
        copyTransform.position += Vector3.left * move;

        float sineY = baseY + Mathf.Sin(waveTime * waveFrequency * Mathf.PI * 2f) * waveAmplitude;
        transform.position = new Vector3(transform.position.x, sineY, transform.position.z);
        copyTransform.position = new Vector3(copyTransform.position.x, sineY, copyTransform.position.z);

        float camLeft = Camera.main.transform.position.x - Camera.main.orthographicSize * Camera.main.aspect;

        if (transform.position.x + tileWidth * 0.5f < camLeft)
            transform.position = new Vector3(copyTransform.position.x + tileWidth, sineY, transform.position.z);
        else if (copyTransform.position.x + tileWidth * 0.5f < camLeft)
            copyTransform.position = new Vector3(transform.position.x + tileWidth, sineY, copyTransform.position.z);
    }

    private void Initialize()
    {
        tileWidth = sr.sprite.bounds.size.x * transform.lossyScale.x;

        Camera cam = Camera.main;
        float camLeft = cam.transform.position.x - cam.orthographicSize * cam.aspect;
        transform.position = new Vector3(camLeft + tileWidth * 0.5f, transform.position.y, transform.position.z);
        baseY = transform.position.y;

        GameObject copy = new GameObject("Background_Mid_Copy");
        copy.transform.SetParent(transform.parent);
        copy.transform.position = new Vector3(transform.position.x + tileWidth, transform.position.y, transform.position.z);
        copy.transform.localScale = transform.localScale;

        SpriteRenderer copySR = copy.AddComponent<SpriteRenderer>();
        copySR.sprite = sr.sprite;
        copySR.material = sr.material;
        copySR.sortingLayerID = sr.sortingLayerID;
        copySR.sortingOrder = sr.sortingOrder;

        copyTransform = copy.transform;
        initialized = true;
    }

    private void OnDestroy()
    {
        if (copyTransform != null)
            Destroy(copyTransform.gameObject);
    }
}
