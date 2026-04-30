using UnityEngine;

/// <summary>
/// 雨スプライトを2枚縦に並べて下スクロールさせるコンポーネント。
/// FogScrollと同じTransform移動方式。mainTextureOffsetはSpriteシェーダーで無効なため使用しない。
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class RainScroll : MonoBehaviour
{
    [Tooltip("落下速度（ワールド単位/秒）。3.0=普通、6.0=強雨")]
    [Range(0.5f, 15.0f)]
    [SerializeField] private float scrollSpeed = 5.0f;

    private SpriteRenderer sr;
    private Transform copyTransform;
    private float tileHeight;
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

        float moveY = scrollSpeed * Time.deltaTime * TimeScale;
        transform.position += Vector3.down * moveY;
        copyTransform.position += Vector3.down * moveY;

        float camBottom = Camera.main.transform.position.y - Camera.main.orthographicSize;

        if (transform.position.y + tileHeight * 0.5f < camBottom)
            transform.position = new Vector3(transform.position.x, copyTransform.position.y + tileHeight, transform.position.z);
        else if (copyTransform.position.y + tileHeight * 0.5f < camBottom)
            copyTransform.position = new Vector3(copyTransform.position.x, transform.position.y + tileHeight, copyTransform.position.z);
    }

    private void Initialize()
    {
        tileHeight = sr.sprite.bounds.size.y * transform.lossyScale.y;

        float camTop = Camera.main.transform.position.y + Camera.main.orthographicSize;
        transform.position = new Vector3(transform.position.x, camTop - tileHeight * 0.5f, transform.position.z);

        GameObject copy = new GameObject("Background_Mid_Rain_Copy");
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
