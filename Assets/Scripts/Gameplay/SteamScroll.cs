using UnityEngine;

/// <summary>
/// 蒸気・排煙スプライトを2枚縦に並べて上スクロールさせるコンポーネント。
/// RainScrollの逆方向 + 横揺れドリフトで工場排煙らしい動きを表現。
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class SteamScroll : MonoBehaviour
{
    [Tooltip("上昇速度（ワールド単位/秒）。1.0=ゆっくり、3.0=普通")]
    [Range(0.5f, 10.0f)]
    [SerializeField] private float scrollSpeed = 1.5f;

    [Tooltip("左右ドリフトの振幅（ワールド単位）。0=無効")]
    [Range(0f, 2.0f)]
    [SerializeField] private float driftAmplitude = 0.4f;

    [Tooltip("左右ドリフトの速さ（Hz）")]
    [Range(0.05f, 1.0f)]
    [SerializeField] private float driftFrequency = 0.15f;

    private SpriteRenderer sr;
    private Transform copyTransform;
    private float tileHeight;
    private float baseX;
    private float driftTime;
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
        driftTime += dt;

        transform.position += Vector3.up * scrollSpeed * dt;
        copyTransform.position += Vector3.up * scrollSpeed * dt;

        float sineX = baseX + Mathf.Sin(driftTime * driftFrequency * Mathf.PI * 2f) * driftAmplitude;
        transform.position = new Vector3(sineX, transform.position.y, transform.position.z);
        copyTransform.position = new Vector3(sineX, copyTransform.position.y, copyTransform.position.z);

        float camTop = Camera.main.transform.position.y + Camera.main.orthographicSize;

        if (transform.position.y - tileHeight * 0.5f > camTop)
            transform.position = new Vector3(sineX, copyTransform.position.y - tileHeight, transform.position.z);
        else if (copyTransform.position.y - tileHeight * 0.5f > camTop)
            copyTransform.position = new Vector3(sineX, transform.position.y - tileHeight, copyTransform.position.z);
    }

    private void Initialize()
    {
        tileHeight = sr.sprite.bounds.size.y * transform.lossyScale.y;

        Camera cam = Camera.main;
        float camBottom = cam.transform.position.y - cam.orthographicSize;
        transform.position = new Vector3(transform.position.x, camBottom + tileHeight * 0.5f, transform.position.z);
        baseX = transform.position.x;

        GameObject copy = new GameObject("Background_Mid_Steam_Copy");
        copy.transform.SetParent(transform.parent);
        copy.transform.position = new Vector3(transform.position.x, transform.position.y + tileHeight, transform.position.z);
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
