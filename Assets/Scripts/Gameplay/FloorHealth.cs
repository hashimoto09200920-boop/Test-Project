using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class FloorHealth : MonoBehaviour
{
    [Header("HP")]
    [SerializeField] private int maxHp = 98;

    private const string PrefsKeyMaxHP = "FloorHealth_MaxHP";

    /// <summary>AreaSelectなどゲーム外シーンから参照するHP値（PlayerPrefsから自動読み取り）</summary>
    public static int SavedMaxHP => PlayerPrefs.GetInt(PrefsKeyMaxHP, 98);

    [Header("Damage by Bullet")]
    [SerializeField] private int damagePerHit = 1;

    [Header("Blink")]
    [SerializeField] private float blinkSeconds = 0.3f;
    [SerializeField] private float blinkInterval = 0.08f;

    [Header("Hit SE")]
    [SerializeField] private AudioClip hitSeClip;
    [Range(0f, 1f)]
    [SerializeField] private float hitSeVolume = 0.5f;

    [Header("Heal VFX/SE (C3: SelfHeal)")]
    [SerializeField] private GameObject healVfxPrefab;
    [SerializeField] private float healVfxDestroySeconds = 1.0f;
    [SerializeField] private AudioClip healSeClip;
    [Range(0f, 1f)]
    [SerializeField] private float healSeVolume = 1f;

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
    private bool isProtected;
    private int lastHitFrame = -999;
    private int lastBulletId = 0;
    private Collider2D cachedCol;
    private Coroutine blinkCo;

    public static bool IsBrokenGlobal { get; private set; }
    public int CurrentHP => currentHp;
    public int MaxHP => maxHp;

    /// <summary>実際にダメージが適用された直後（Break判定より前）に発火。引数はダメージ量。チュートリアル等の検知用</summary>
    public event System.Action<int> OnDamaged;

    private void OnEnable()
    {
        Game.Skills.SkillManager.OnSelfHealFloor += PlayHealVfx;
        EnemySpawner.OnFinalBossDefeated += OnFinalBossDefeated;
    }

    private void OnDisable()
    {
        Game.Skills.SkillManager.OnSelfHealFloor -= PlayHealVfx;
        EnemySpawner.OnFinalBossDefeated -= OnFinalBossDefeated;
    }

    private void OnFinalBossDefeated()
    {
        isProtected = true;
    }

    private void PlayHealVfx()
    {
        if (healVfxPrefab != null)
        {
            GameObject vfx = Instantiate(healVfxPrefab, transform.position, Quaternion.identity);
            if (healVfxDestroySeconds > 0f) Destroy(vfx, healVfxDestroySeconds);
        }

        if (healSeClip != null)
        {
            float vol = healSeVolume * (SoundSettingsManager.Instance != null ? SoundSettingsManager.Instance.SEVolume : 1f);
            GameObject go = new GameObject("HealSE");
            go.transform.position = transform.position;
            AudioSource a = go.AddComponent<AudioSource>();
            a.playOnAwake = false;
            a.spatialBlend = 0f;
            a.PlayOneShot(healSeClip, vol);
            Destroy(go, healSeClip.length + 0.1f);
        }
    }

    private void Awake()
    {
        PlayerPrefs.SetInt(PrefsKeyMaxHP, maxHp);

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
        if (isProtected) return;
        if (other == null) return;

        EnemyBullet bullet = other.GetComponent<EnemyBullet>();
        if (bullet == null) bullet = other.GetComponentInParent<EnemyBullet>();
        if (bullet == null) return;
        if (bullet.HasPaddleReflectedOnce) return;

        // ★「ドリル反射」弾（PinnedReflectBullet）は、線・敵と同じ「めり込みながら規定回数ヒット」の
        //   特性をフロアに対しても引き継ぐ。留まっている間は通常の1回ダメージ処理を行わず、
        //   規定回数に達したらPinnedReflectBullet側で弾自体を消滅させる。
        //   Trigger/Collisionいずれの経路でも接触法線が使えるとは限らないため、弾の速度方向を
        //   「めり込む方向」の代わりとして使う（線・敵のcontact normalと同じ意味＝元の進行方向）
        PinnedReflectBullet pinned = bullet.GetComponent<PinnedReflectBullet>();
        if (pinned != null)
        {
            Rigidbody2D bulletRb = bullet.GetComponent<Rigidbody2D>();
            Vector2 embedDir = (bulletRb != null && bulletRb.linearVelocity.sqrMagnitude > 0.0001f)
                ? bulletRb.linearVelocity.normalized
                : (Vector2)bullet.transform.right;
            Vector3 pinHitPos = bullet.transform.position;
            if (pinned.TryPinToEnemy(this, (dmg, mul, pos) => ApplyBeamDamage(Mathf.RoundToInt(dmg * damagePerHit)), bullet, embedDir, pinHitPos, bullet.DamageValue, 1f))
            {
                return;
            }
        }

        int bulletId = bullet.GetInstanceID();
        if (Time.frameCount == lastHitFrame && bulletId == lastBulletId) return;
        lastHitFrame = Time.frameCount;
        lastBulletId = bulletId;

        int dmg = Mathf.Max(0, bullet.DamageValue * damagePerHit);
        if (dmg <= 0) return;

        SessionStats.AddDamageTaken(dmg);
        // ★Break()はIsPlayerDeadGlobal中は早期returnしてcurrentHpを0にクランプしないため、
        //   ここで直接0未満にならないようにする（レーザー等の多段ヒットでマイナス表示になる不具合対策）
        currentHp = Mathf.Max(0, currentHp - dmg);
        OnDamaged?.Invoke(dmg);

        // C3スキル：セルフヒールタイマーをリセット
        if (Game.Skills.SkillManager.Instance != null)
        {
            Game.Skills.SkillManager.Instance.ResetSelfHealTimer();
        }

        // Hit SE
        if (hitSeClip != null && audioSource != null)
        {
            // SoundSettingsManagerのSE音量を適用
            float finalVolume = hitSeVolume * (SoundSettingsManager.Instance != null ? SoundSettingsManager.Instance.SEVolume : 1f);
            audioSource.PlayOneShot(hitSeClip, finalVolume);
        }

        // カメラシェイク＋画面フラッシュ
        CameraShake.Shake();
        DamageFlashUI.Flash();

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
        if (PixelDancerController.IsPlayerDeadGlobal) return;
        isBroken = true;
        currentHp = 0;

        IsBrokenGlobal = true;

        if (breakVfxPrefab != null)
        {
            Instantiate(breakVfxPrefab, transform.position, Quaternion.identity);
        }

        if (breakSeClip != null && audioSource != null)
        {
            float finalVolume = breakSeVolume * (SoundSettingsManager.Instance != null ? SoundSettingsManager.Instance.SEVolume : 1f);
            audioSource.PlayOneShot(breakSeClip, finalVolume);
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

    // =========================================================
    // ★追加：Beam（EnemyBulletを介さないダメージ源）から未反射区間のダメージを受け取る入口
    // HandleHitと同じisProtected判定を経てから適用する（ApplyExplosionDamageと違いisProtected中は無効）
    // =========================================================
    public bool ApplyBeamDamage(int damage)
    {
        if (isBroken) return false;
        if (isProtected) return false;

        int dmg = Mathf.Max(0, damage);
        if (dmg <= 0) return false;

        SessionStats.AddDamageTaken(dmg);
        currentHp = Mathf.Max(0, currentHp - dmg);

        if (Game.Skills.SkillManager.Instance != null)
        {
            Game.Skills.SkillManager.Instance.ResetSelfHealTimer();
        }

        if (hitSeClip != null && audioSource != null)
        {
            float finalVolume = hitSeVolume * (SoundSettingsManager.Instance != null ? SoundSettingsManager.Instance.SEVolume : 1f);
            audioSource.PlayOneShot(hitSeClip, finalVolume);
        }

        CameraShake.Shake();
        DamageFlashUI.Flash();

        if (blinkSeconds > 0f)
        {
            if (blinkCo != null) StopCoroutine(blinkCo);
            blinkCo = StartCoroutine(BlinkCoroutine());
        }

        if (currentHp <= 0)
        {
            Break();
        }

        return true;
    }

    public void ApplyExplosionDamage(int damage)
    {
        if (isBroken) return;
        int dmg = Mathf.Max(0, damage);
        if (dmg <= 0) return;

        SessionStats.AddDamageTaken(dmg);
        currentHp = Mathf.Max(0, currentHp - dmg);

        // C3スキル：セルフヒールタイマーをリセット
        if (Game.Skills.SkillManager.Instance != null)
        {
            Game.Skills.SkillManager.Instance.ResetSelfHealTimer();
        }

        // Hit SE
        if (hitSeClip != null && audioSource != null)
        {
            // SoundSettingsManagerのSE音量を適用
            float finalVolume = hitSeVolume * (SoundSettingsManager.Instance != null ? SoundSettingsManager.Instance.SEVolume : 1f);
            audioSource.PlayOneShot(hitSeClip, finalVolume);
        }

        // カメラシェイク＋画面フラッシュ
        CameraShake.Shake();
        DamageFlashUI.Flash();

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
        // 最大値のみ変更。現在値は新しい最大値を超えないようクランプするだけ
        currentHp = Mathf.Min(currentHp, maxHp);
    }

    /// <summary>
    /// HPを現在の最大値まで全回復（ゲーム開始時専用）
    /// </summary>
    public void RestoreToFullHP()
    {
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
