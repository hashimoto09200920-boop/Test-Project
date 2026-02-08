using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class EnemyStats : MonoBehaviour
{
    [SerializeField] private int maxHp = 3;
    private int hp;

    public int HP => hp;
    public int MaxHP => maxHp;

    // =========================================================
    // Death Effects
    // =========================================================
    [Header("Death Effects")]
    [Tooltip("撃破時のエフェクトプレハブ（爆発など）")]
    [SerializeField] private GameObject deathEffectPrefab;

    [Tooltip("時間経過消滅時のエフェクトプレハブ（フェードアウトなど）")]
    [SerializeField] private GameObject expireEffectPrefab;

    [Tooltip("エフェクトの自動削除時間（秒）")]
    [SerializeField] private float effectDestroySeconds = 2f;

    [Header("Fade Settings")]
    [Tooltip("出現時のフェードイン時間（秒）\n0以下で無効化")]
    [SerializeField] private float fadeInDuration = 0.5f;

    [Tooltip("時間経過消滅時のフェードアウト時間（秒）")]
    [SerializeField] private float fadeOutDuration = 1f;

    [Header("Death SE")]
    [Tooltip("撃破時のSE")]
    [SerializeField] private AudioClip deathSeClip;

    [Tooltip("時間経過消滅時のSE")]
    [SerializeField] private AudioClip expireSeClip;

    [Tooltip("SEの音量")]
    [SerializeField] private float seVolume = 1f;

    // =========================================================
    // EnemySpawner通知用
    // =========================================================
    private EnemySpawner spawner;

    public void SetSpawner(EnemySpawner spawner)
    {
        this.spawner = spawner;
    }

    // =========================================================
    // サブパーツ管理（複数パーツ敵用）
    // =========================================================
    private List<GameObject> subParts = new List<GameObject>();

    // HP％を取得（0～100）
    public float GetHpPercentage()
    {
        if (maxHp <= 0) return 0f;
        return ((float)hp / maxHp) * 100f;
    }

    private void Awake()
    {
        // 手置きテストやEnemySpawner未使用でも破綻しないよう初期化
        maxHp = Mathf.Max(1, maxHp);
        hp = maxHp;
    }

    private void Start()
    {
        // フェードイン効果を開始
        if (fadeInDuration > 0f)
        {
            StartCoroutine(FadeIn());
        }
    }

    public void ApplyMaxHp(int value)
    {
        maxHp = Mathf.Max(1, value);
        hp = maxHp;
    }

    public void Damage(int amount)
    {
        // ★シールドがあればシールドから消費
        EnemyShield shield = GetComponent<EnemyShield>();
        int damageToHp = amount;

        if (shield != null && shield.IsEnabled)
        {
            damageToHp = shield.ApplyDamage(amount);
        }

        // 残りのダメージをHPに適用
        hp -= Mathf.Max(0, damageToHp);
        if (hp <= 0)
        {
            Die(isKilled: true);
        }
    }

    /// <summary>
    /// 敵を消滅させる
    /// </summary>
    /// <param name="isKilled">true=プレイヤーに倒された, false=時間経過で消滅</param>
    public void Die(bool isKilled)
    {
        if (isKilled)
        {
            // プレイヤーに倒された場合: エフェクトとSEを再生
            if (deathEffectPrefab != null)
            {
                GameObject effect = Instantiate(deathEffectPrefab, transform.position, Quaternion.identity);
                if (effectDestroySeconds > 0f)
                {
                    Destroy(effect, effectDestroySeconds);
                }
            }

            if (deathSeClip != null)
            {
                AudioSource.PlayClipAtPoint(deathSeClip, transform.position, seVolume);
            }

            // EnemySpawnerに通知
            if (spawner != null)
            {
                spawner.OnEnemyDestroyed();
            }

            // サブパーツを全て破壊
            foreach (GameObject subPart in subParts)
            {
                if (subPart != null)
                {
                    Destroy(subPart);
                }
            }

            // メインパーツを破壊
            Destroy(gameObject);
        }
        else
        {
            // 時間経過で消滅: フェードアウトアニメーション
            StartCoroutine(FadeOutAndDestroy());
        }
    }

    /// <summary>
    /// フェードインするコルーチン
    /// </summary>
    private IEnumerator FadeIn()
    {
        SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null) yield break;

        Color originalColor = spriteRenderer.color;

        // 初期状態は透明
        spriteRenderer.color = new Color(originalColor.r, originalColor.g, originalColor.b, 0f);

        float elapsed = 0f;
        while (elapsed < fadeInDuration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(0f, 1f, elapsed / fadeInDuration);
            spriteRenderer.color = new Color(originalColor.r, originalColor.g, originalColor.b, alpha);
            yield return null;
        }

        // 完全に不透明にする
        spriteRenderer.color = new Color(originalColor.r, originalColor.g, originalColor.b, 1f);
    }

    /// <summary>
    /// フェードアウトして消滅するコルーチン
    /// </summary>
    private IEnumerator FadeOutAndDestroy()
    {
        // EnemySpawnerに通知（時間経過による消滅なので撃破数にはカウントしない）
        if (spawner != null)
        {
            spawner.NotifyEnemyDead();
        }

        // 攻撃と移動を停止
        EnemyShooter shooter = GetComponent<EnemyShooter>();
        if (shooter != null)
        {
            shooter.enabled = false;
        }

        EnemyMover mover = GetComponent<EnemyMover>();
        if (mover != null)
        {
            mover.enabled = false;
        }

        // SpriteRendererを取得（メインオブジェクトから）
        SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();

        if (spriteRenderer != null && fadeOutDuration > 0f)
        {
            float elapsed = 0f;
            Color originalColor = spriteRenderer.color;

            while (elapsed < fadeOutDuration)
            {
                elapsed += Time.deltaTime;
                float alpha = Mathf.Lerp(1f, 0f, elapsed / fadeOutDuration);
                spriteRenderer.color = new Color(originalColor.r, originalColor.g, originalColor.b, alpha);
                yield return null;
            }

            // 完全に透明にする
            spriteRenderer.color = new Color(originalColor.r, originalColor.g, originalColor.b, 0f);
        }

        // サブパーツを全て破壊
        foreach (GameObject subPart in subParts)
        {
            if (subPart != null)
            {
                Destroy(subPart);
            }
        }

        // メインパーツを破壊
        Destroy(gameObject);
    }

    /// <summary>
    /// サブパーツを登録（EnemyPartProxy から呼ばれる）
    /// メインパーツが破壊される時、サブパーツも一緒に破壊される
    /// </summary>
    public void RegisterSubPart(GameObject subPart)
    {
        if (subPart != null && !subParts.Contains(subPart))
        {
            subParts.Add(subPart);
        }
    }
}
