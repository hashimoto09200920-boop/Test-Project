using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class FloorHealth : MonoBehaviour
{
    [Header("HP")]
    [SerializeField] private int maxHp = 10;

    [Header("Damage by Bullet")]
    [SerializeField] private int damagePerHit = 1;

    [Header("Blink")]
    [SerializeField] private float blinkSeconds = 0.3f;
    [SerializeField] private float blinkInterval = 0.08f;

    [Header("Hit SE")]
    [SerializeField] private AudioClip hitSeClip;
    [Range(0f, 1f)]
    [SerializeField] private float hitSeVolume = 0.5f;

    [Header("Break VFX/SE")]
    [SerializeField] private GameObject breakVfxPrefab;
    [SerializeField] private AudioClip breakSeClip;
    [Range(0f, 1f)]
    [SerializeField] private float breakSeVolume = 1f;

    [Header("Break Action")]
    [SerializeField] private bool disableColliderOnBreak = false;
    [SerializeField] private bool disableRendererOnBreak = true;

    [Header("Auto Fit to Player Range")]
    [SerializeField] private bool autoFitWidth = true;
    [SerializeField] private float widthPadding = 0.5f;

    [Header("References")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private AudioSource audioSource;

    private int currentHp;
    private bool isBroken;
    private int lastHitFrame = -999;
    private int lastBulletId = 0;
    private Collider2D cachedCol;
    private Coroutine blinkCo;

    public static bool IsBrokenGlobal { get; private set; }
    public int CurrentHP => currentHp;
    public int MaxHP => maxHp;

    private void Awake()
    {
        currentHp = Mathf.Max(0, maxHp);
        cachedCol = GetComponent<Collider2D>();
        IsBrokenGlobal = false;

        // FloorVisual子オブジェクトのSpriteRendererを取得
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            }
        }

        // AudioSource自動追加（WallHealthと同様）
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.loop = false;
            audioSource.spatialBlend = 0f; // 2D
        }
    }


    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (isBroken) return;
        if (collision == null || collision.collider == null) return;
        HandleHit(collision.collider);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isBroken) return;
        if (other == null) return;
        HandleHit(other);
    }

    private void HandleHit(Collider2D other)
    {
        if (isBroken) return;
        if (other == null) return;

        EnemyBullet bullet = other.GetComponent<EnemyBullet>();
        if (bullet == null) bullet = other.GetComponentInParent<EnemyBullet>();
        if (bullet == null) return;

        int bulletId = bullet.GetInstanceID();
        if (Time.frameCount == lastHitFrame && bulletId == lastBulletId) return;
        lastHitFrame = Time.frameCount;
        lastBulletId = bulletId;

        int dmg = Mathf.Max(0, bullet.DamageValue * damagePerHit);
        if (dmg <= 0) return;

        currentHp -= dmg;

        // C3スキル：セルフヒールタイマーをリセット
        if (Game.Skills.SkillManager.Instance != null)
        {
            Game.Skills.SkillManager.Instance.ResetSelfHealTimer();
        }

        // Hit SE
        if (hitSeClip != null && audioSource != null)
        {
            audioSource.PlayOneShot(hitSeClip, hitSeVolume);
        }

        if (blinkSeconds > 0f)
        {
            if (blinkCo != null) StopCoroutine(blinkCo);
            blinkCo = StartCoroutine(BlinkCoroutine());
        }

        if (currentHp <= 0)
        {
            Break();
        }
    }

    private IEnumerator BlinkCoroutine()
    {
        if (spriteRenderer == null) yield break;

        float elapsed = 0f;
        bool visible = true;

        while (elapsed < blinkSeconds)
        {
            visible = !visible;
            spriteRenderer.enabled = visible;
            yield return new WaitForSeconds(blinkInterval);
            elapsed += blinkInterval;
        }

        spriteRenderer.enabled = true;
        blinkCo = null;
    }

    private void Break()
    {
        if (isBroken) return;
        isBroken = true;
        currentHp = 0;

        IsBrokenGlobal = true;

        if (breakVfxPrefab != null)
        {
            Instantiate(breakVfxPrefab, transform.position, Quaternion.identity);
        }

        if (breakSeClip != null && audioSource != null)
        {
            audioSource.PlayOneShot(breakSeClip, breakSeVolume);
        }

        if (disableRendererOnBreak && spriteRenderer != null)
        {
            spriteRenderer.enabled = false;
        }

        if (disableColliderOnBreak && cachedCol != null)
        {
            cachedCol.enabled = false;
        }

        PixelDancerController dancer = FindFirstObjectByType<PixelDancerController>();
        if (dancer != null)
        {
            dancer.ForceFallByFloorBreak();
        }
    }

    public void RestoreHP()
    {
        isBroken = false;
        currentHp = maxHp;

        IsBrokenGlobal = false;

        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = true;
        }

        if (cachedCol != null)
        {
            cachedCol.enabled = true;
        }
    }

    public void ApplyExplosionDamage(int damage)
    {
        if (isBroken) return;
        int dmg = Mathf.Max(0, damage);
        if (dmg <= 0) return;

        currentHp -= dmg;

        // C3スキル：セルフヒールタイマーをリセット
        if (Game.Skills.SkillManager.Instance != null)
        {
            Game.Skills.SkillManager.Instance.ResetSelfHealTimer();
        }

        // Hit SE
        if (hitSeClip != null && audioSource != null)
        {
            audioSource.PlayOneShot(hitSeClip, hitSeVolume);
        }

        if (blinkSeconds > 0f)
        {
            if (blinkCo != null) StopCoroutine(blinkCo);
            blinkCo = StartCoroutine(BlinkCoroutine());
        }

        if (currentHp <= 0)
        {
            Break();
        }
    }

    // =========================================================
    // Skill System Setters
    // =========================================================

    /// <summary>
    /// 最大HPを設定（スキルシステム用）
    /// </summary>
    public void SetMaxHP(int value)
    {
        maxHp = Mathf.Max(1, value);
        // スキル取得時は満タン状態から開始
        currentHp = maxHp;
    }

    /// <summary>
    /// HPを回復（C3スキル用）
    /// </summary>
    public void Heal(int amount)
    {
        if (isBroken) return;
        currentHp = Mathf.Min(currentHp + amount, maxHp);
    }
}
