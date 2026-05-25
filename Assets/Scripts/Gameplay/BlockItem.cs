using System.Collections;
using Game.Skills;
using UnityEngine;

[RequireComponent(typeof(CircleCollider2D))]
[RequireComponent(typeof(Rigidbody2D))]
public class BlockItem : MonoBehaviour
{
    public enum ItemType { Gold, Life }

    [HideInInspector] public ItemType itemType;
    [HideInInspector] public int amount;
    [HideInInspector] public Sprite icon;
    [HideInInspector] public AudioClip collectSE;
    [HideInInspector] public float baseScale = 1f;

    [Header("Float")]
    [SerializeField] private float floatAmplitude = 0.12f;
    [SerializeField] private float floatSpeed = 1.5f;

    [Header("Collect Animation")]
    [SerializeField] private float expandScale = 1.8f;
    [SerializeField] private float expandDuration = 0.12f;
    [SerializeField] private float fadeDuration = 0.25f;
    [SerializeField] private GameObject collectVfxPrefab;

    private bool collected;
    private Vector3 basePosition;
    private CircleCollider2D triggerCol;
    private SpriteRenderer spriteRenderer;

    private void Awake()
    {
        basePosition = transform.position;
        spriteRenderer = GetComponent<SpriteRenderer>();

        triggerCol = GetComponent<CircleCollider2D>();
        triggerCol.isTrigger = true;

        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;
        rb.interpolation = RigidbodyInterpolation2D.None;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Discrete;
    }

    // Start runs after BlockItemManager has set all HideInInspector fields
    private void Start()
    {
        transform.localScale = Vector3.one * baseScale;
        StartCoroutine(FloatLoop());
    }

    private IEnumerator FloatLoop()
    {
        float offset = Random.Range(0f, Mathf.PI * 2f);
        while (!collected)
        {
            float y = Mathf.Sin(Time.time * floatSpeed + offset) * floatAmplitude;
            transform.position = basePosition + new Vector3(0f, y, 0f);
            yield return null;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (collected) return;
        if (other.GetComponent<PaddleDot>() != null)
            Collect(isCircle: false);
    }

    public void CollectFromCircle(Vector3 circleCenter)
    {
        if (collected) return;
        Collect(isCircle: true);
    }

    private void Collect(bool isCircle)
    {
        if (collected) return;
        collected = true;
        triggerCol.enabled = false;
        BlockItemManager.Instance?.OnItemCollectionStarted(this);
        StartCoroutine(CollectAnimation(isCircle));
    }

    private IEnumerator CollectAnimation(bool isCircle)
    {
        float peak = baseScale * expandScale;

        // 拡大フェーズ
        float t = 0f;
        while (t < expandDuration)
        {
            t += Time.deltaTime;
            float s = Mathf.Lerp(baseScale, peak, t / expandDuration);
            transform.localScale = Vector3.one * s;
            yield return null;
        }

        // 縮小 + フェードアウトフェーズ
        t = 0f;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            float progress = t / fadeDuration;
            transform.localScale = Vector3.one * Mathf.Lerp(peak, 0f, progress);
            if (spriteRenderer != null)
            {
                Color c = spriteRenderer.color;
                c.a = 1f - progress;
                spriteRenderer.color = c;
            }
            yield return null;
        }

        // 効果適用
        int multiplier = isCircle
            ? (SkillManager.Instance != null ? SkillManager.Instance.GetBlockItemCircleMultiplier() : 2)
            : 1;

        BlockItemManager.Instance?.ApplyEffect(itemType, amount * multiplier, transform.position, icon, collectSE);

        if (collectVfxPrefab != null)
            Instantiate(collectVfxPrefab, transform.position, Quaternion.identity);

        Destroy(gameObject);
    }

    public bool IsCollected => collected;
}
