using System.Collections;
using Game.Skills;
using UnityEngine;

[RequireComponent(typeof(CircleCollider2D))]
[RequireComponent(typeof(Rigidbody2D))]
public class BlockItem : MonoBehaviour
{
    // ★設定（AreaConfigの「アイテムの出る割合」）に番号で保存されるので、途中に挿入せず末尾に足すこと
    public enum ItemType { Gold, Life, GoldBag, TreasureChest, GoldenShoes, GoldBolt, MultiBound, SpiralShot, BlastShot, SplitShot, FoodTicket } // FoodTicket：お食事券（2026/11/11） // SpiralShot：スパイラルショット（跳ね返した弾がドリルになる・3発）（2026/11/11）

    /// <summary>弾の性質を変えるアイテムか（スパイラルショット等）。取ると画面左上に残り回数を出す</summary>
    public static bool IsShotKind(ItemType t) => t == ItemType.SpiralShot || t == ItemType.BlastShot || t == ItemType.SplitShot;

    /// <summary>弾の性質を変えるアイテムの種類（ShotItemManager 用）</summary>
    public static ShotItemType ShotTypeOf(ItemType t) => t == ItemType.SpiralShot ? ShotItemType.Spiral : t == ItemType.BlastShot ? ShotItemType.Blast : t == ItemType.SplitShot ? ShotItemType.Split : ShotItemType.None; // GoldenShoes：ゴールドシューズ（線の消費が一定時間半分）・GoldBolt：アクセラレーター（跳ね返した弾が一定時間1.5倍速）（2026/11/10）

    /// <summary>一定時間効くアイテムか（ゴールドシューズ・アクセラレーター）</summary>
    public static bool IsBuffKind(ItemType t) => t == ItemType.GoldenShoes || t == ItemType.GoldBolt || t == ItemType.MultiBound; // MultiBound：マルチバウンド（跳ね返せる回数が2倍）（2026/11/11）

    /// <summary>Goldがもらえる種類か（コイン・金貨袋・宝箱）</summary>
    public static bool IsGoldKind(ItemType t) => t == ItemType.Gold || t == ItemType.GoldBag || t == ItemType.TreasureChest;

    /// <summary>取った瞬間に切り替える画像（宝箱の「開いた絵」）。無ければ切り替えない</summary>
    [HideInInspector] public Sprite collectSprite;

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
        if (PaddleDot.FromCollider(other) != null) // 線の区間（新方式）にも対応
            Collect(isCircle: false);
    }

    /// <summary>パッシブ「アイテムマグネット」：Stageクリア時に、線で取った時と同じ取り方で取る（BlockItemManager）</summary>
    public void CollectByMagnet()
    {
        if (collected) return;
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
        if (collectSprite != null && spriteRenderer != null) spriteRenderer.sprite = collectSprite; // 宝箱：取った瞬間に「開いた絵」へ
        MissionProgressManager.OnItemCollected(); // ミッション：アイテムを取った数
        BlockItemManager.Instance?.OnItemCollectionStarted(this);
        ItemFXManager.NotifyCollectStart(itemType, transform.position, isCircle); // 取った瞬間の演出（ItemFXManager）
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

        ItemFXManager.NotifyCollected(itemType, transform.position, isCircle); // 消える瞬間の演出（ItemFXManager）
        BlockItemManager.Instance?.ApplyEffect(itemType, amount * multiplier, transform.position, icon, collectSE, isCircle);

        if (collectVfxPrefab != null)
            Instantiate(collectVfxPrefab, transform.position, Quaternion.identity);

        Destroy(gameObject);
    }

    public bool IsCollected => collected;

    /// <summary>収集されずにフィールドへ残ったアイテムを、効果適用せずアルファフェードで消す（Area10ボスラッシュのボス切替専用）。</summary>
    public void FadeOutAndDestroy(float duration)
    {
        if (collected) return;
        collected = true;
        if (triggerCol != null) triggerCol.enabled = false;
        BlockItemManager.Instance?.OnItemCollectionStarted(this);
        StartCoroutine(FadeOutOnlyRoutine(duration));
    }

    private IEnumerator FadeOutOnlyRoutine(float duration)
    {
        Color startColor = spriteRenderer != null ? spriteRenderer.color : Color.white;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            if (spriteRenderer != null)
            {
                Color c = spriteRenderer.color;
                c.a = Mathf.Lerp(startColor.a, 0f, elapsed / duration);
                spriteRenderer.color = c;
            }
            yield return null;
        }
        Destroy(gameObject);
    }
}
