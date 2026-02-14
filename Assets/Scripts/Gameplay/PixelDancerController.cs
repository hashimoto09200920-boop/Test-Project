using System.Collections;
using UnityEngine;
using TMPro;

[RequireComponent(typeof(Rigidbody2D))]
public class PixelDancerController : MonoBehaviour
{
    [Header("HP")]
    [SerializeField] private int initialHP = 5;
    private int currentHP;

    [Header("Down")]
    [SerializeField] private int maxDown = 5;
    private int currentDown;

    [Header("Fall")]
    [SerializeField] private float fallSpeedBase = 3f;
    [SerializeField] private float fallSpeedAddPerDown = 1f;
    [SerializeField] private float fallAngleMinDegrees = 15f;
    [SerializeField] private float fallAngleMaxDegrees = 40f;
    [SerializeField] private float hopHeight = 1.5f;
    [SerializeField] private float hopDuration = 0.3f;

    [Header("Hop Arc")]
    [SerializeField] private float hopHorizontal = 1f;
    [SerializeField] private float hopArcHeight = 0.5f;
    [SerializeField] private bool randomizeLeftRight = true;

    [Header("Visual")]
    [SerializeField] private float blinkSeconds = 0.5f;
    [SerializeField] private float blinkInterval = 0.1f;
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Header("Hit")]
    [SerializeField] private float hitInvincibleSeconds = 0f;
    [SerializeField] private AudioClip hitSeClip;
    [SerializeField] private float hitSeVolume = 1f;
    [SerializeField] private GameObject hitVfxPrefab;
    [SerializeField] private float hitVfxDestroySeconds = 2f;

    [Header("Respawn (Future)")]
    [SerializeField] private Transform respawnPoint;

    [Header("Rescue Feedback")]
    [SerializeField] private float rescueDelaySeconds = 0f;
    [SerializeField] private float rescueInvincibleSeconds = 0.6f;
    [SerializeField] private AudioClip rescueSeClip;
    [SerializeField] private float rescueSeVolume = 1f;
    [SerializeField] private GameObject rescueVfxPrefab;
    [SerializeField] private float rescueVfxDestroySeconds = 2f;

    [Header("Auto Move")]
    [SerializeField] private bool enableAutoMove = true;
    [SerializeField] private float autoMoveSpeed = 2.0f;
    [SerializeField] private float autoMoveRange = 3.0f;
    [SerializeField] private float autoMoveArriveThreshold = 0.05f;
    [SerializeField] private float autoMoveWaitMin = 0.1f;
    [SerializeField] private float autoMoveWaitMax = 0.4f;

    [Header("Game Over")]
    [SerializeField] private float gameOverYMargin = 0.1f;

    [Header("UI")]
    [SerializeField] private TMP_Text hpText;

    [Header("References")]
    [SerializeField] private Collider2D bodyCollider;
    [SerializeField] private Rigidbody2D rb2d;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private Animator animator;

    private Camera mainCamera;
    private bool isFalling = false;
    private bool isInvincible = false;
    private Coroutine blinkCo;
    private Coroutine invincibleCo;
    private Vector2 fallDirection;
    private int currentFallSign = 1;

    private float autoMoveCenterX;
    private float autoMoveTargetX;
    private float autoMoveWaitUntil;
    private bool autoMoveInitialized;

    private float initialPositionY;

    public static bool IsPlayerDeadGlobal { get; private set; }

    public bool IsFalling => isFalling;
    public float AutoMoveRange => autoMoveRange;

    private void Awake()
    {
        rb2d = GetComponent<Rigidbody2D>();
        if (bodyCollider == null) bodyCollider = GetComponent<Collider2D>();
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        if (animator == null) animator = GetComponent<Animator>();

        mainCamera = Camera.main;
        if (mainCamera == null) mainCamera = FindFirstObjectByType<Camera>();

        currentHP = initialHP;
        currentDown = 0;
        isFalling = false;
        isInvincible = false;

        IsPlayerDeadGlobal = false;

        autoMoveInitialized = false;

        initialPositionY = transform.position.y;

        UpdateHPText();
    }

    private void Update()
    {
        AutoMoveUpdate();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isInvincible) return;
        if (isFalling) return;
        if (IsPlayerDeadGlobal) return;
        if (FloorHealth.IsBrokenGlobal) return;

        EnemyBullet bullet = other.GetComponentInParent<EnemyBullet>();
        if (bullet == null) return;

        int dmg = bullet.DamageValue;
        TakeDamage(dmg);
    }

    private void TakeDamage(int damage)
    {
        currentHP = Mathf.Max(0, currentHP - damage);
        UpdateHPText();

        // C3スキル：セルフヒールタイマーをリセット
        if (Game.Skills.SkillManager.Instance != null)
        {
            Game.Skills.SkillManager.Instance.ResetSelfHealTimer();
        }

        PlayHitFeedback();

        if (currentHP <= 0)
        {
            StartFall();
        }
        else
        {
            StartInvincible(hitInvincibleSeconds);
        }
    }

    private void PlayHitFeedback()
    {
        if (hitSeClip != null && audioSource != null)
        {
            audioSource.PlayOneShot(hitSeClip, hitSeVolume);
        }

        if (hitVfxPrefab != null)
        {
            GameObject vfx = Instantiate(hitVfxPrefab, transform.position, Quaternion.identity);
            if (hitVfxDestroySeconds > 0f)
            {
                Destroy(vfx, hitVfxDestroySeconds);
            }
        }

        if (blinkSeconds > 0f)
        {
            if (blinkCo != null) StopCoroutine(blinkCo);
            blinkCo = StartCoroutine(BlinkCoroutine());
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

    private void StartFall()
    {
        if (isFalling)
        {
            return;
        }

        IsPlayerDeadGlobal = true;

        currentDown = Mathf.Min(maxDown, currentDown + 1);
        UpdateHPText();

        // 左右ランダム決定
        if (randomizeLeftRight)
        {
            currentFallSign = Random.value < 0.5f ? -1 : 1;
        }
        else
        {
            currentFallSign = 1;
        }

        // 落下角度ランダム決定
        float minAngle = fallAngleMinDegrees;
        float maxAngle = fallAngleMaxDegrees;
        if (minAngle > maxAngle)
        {
            float temp = minAngle;
            minAngle = maxAngle;
            maxAngle = temp;
        }
        float angleDeg = Random.Range(minAngle, maxAngle);
        float angleRad = angleDeg * Mathf.Deg2Rad;

        // 落下方向計算
        fallDirection = new Vector2(currentFallSign * Mathf.Sin(angleRad), -Mathf.Cos(angleRad)).normalized;

        isFalling = true;

        // ゲームオーバーシーケンス開始（タイマー停止）
        if (GameManager.Instance != null)
        {
            GameManager.Instance.StartGameOverSequence();
        }

        StartCoroutine(FallSequence());
    }

    private IEnumerator FallSequence()
    {
        Vector2 startPos = transform.position;
        Vector2 hopTarget = startPos + Vector2.up * hopHeight + Vector2.right * (currentFallSign * hopHorizontal);

        float hopElapsed = 0f;
        while (hopElapsed < hopDuration)
        {
            hopElapsed += Time.deltaTime;
            float t = hopElapsed / hopDuration;
            t = 1f - (1f - t) * (1f - t);

            Vector2 basePos = Vector2.Lerp(startPos, hopTarget, t);
            Vector2 arc = Vector2.up * (4f * hopArcHeight * t * (1f - t));
            transform.position = basePos + arc;

            yield return null;
        }

        float fallSpeed = fallSpeedBase + currentDown * fallSpeedAddPerDown;
        Vector2 velocity = fallDirection * fallSpeed;

        if (rb2d != null)
        {
            rb2d.linearVelocity = velocity;
        }

        while (isFalling)
        {
            if (mainCamera != null)
            {
                Vector3 viewportPos = mainCamera.WorldToViewportPoint(transform.position);
                if (viewportPos.y < -gameOverYMargin)
                {
                    OnGameOver();
                    yield break;
                }
            }

            if (rb2d != null)
            {
                rb2d.linearVelocity = velocity;
            }

            yield return null;
        }
    }

    private void OnGameOver()
    {
        isFalling = false;

        // GameManagerに通知
        if (GameManager.Instance != null)
        {
            GameManager.Instance.TriggerGameOver();
        }
    }

    private void StartInvincible(float seconds)
    {
        if (seconds <= 0f) return;

        // 既存の無敵コルーチンを停止
        if (invincibleCo != null)
        {
            StopCoroutine(invincibleCo);
            invincibleCo = null;
        }

        invincibleCo = StartCoroutine(InvincibleCoroutine(seconds));
    }

    private IEnumerator InvincibleCoroutine(float seconds)
    {
        isInvincible = true;
        yield return new WaitForSeconds(seconds);
        isInvincible = false;
        invincibleCo = null;
    }

    private void UpdateHPText()
    {
        if (hpText == null) return;
        hpText.text = $"HP: {currentHP}  Down: {currentDown}";
    }

    public void RescueFromCircle()
    {
        if (!isFalling) return;

        StartCoroutine(RescueSequence());
    }

    private IEnumerator RescueSequence()
    {
        isFalling = false;

        if (rb2d != null)
        {
            rb2d.linearVelocity = Vector2.zero;
        }

        // 既存の無敵コルーチンを停止して、新しい無敵状態を開始
        if (invincibleCo != null)
        {
            StopCoroutine(invincibleCo);
            invincibleCo = null;
        }
        isInvincible = true;

        // 救済後の目標位置
        float centerX = (respawnPoint != null) ? respawnPoint.position.x : 0f;
        float z = (respawnPoint != null) ? respawnPoint.position.z : transform.position.z;
        Vector3 targetPosition = new Vector3(centerX, initialPositionY, z);

        // 移動アニメーション
        if (rescueDelaySeconds > 0f)
        {
            Vector3 startPosition = transform.position;
            float elapsed = 0f;

            while (elapsed < rescueDelaySeconds)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / rescueDelaySeconds);

                // イージング（ease-out）
                t = 1f - (1f - t) * (1f - t);

                transform.position = Vector3.Lerp(startPosition, targetPosition, t);
                yield return null;
            }
        }

        // 最終位置を確定
        transform.position = targetPosition;
        autoMoveInitialized = false;

        IsPlayerDeadGlobal = false;

        // ゲームオーバーシーケンスを解除（タイマー再開）
        if (GameManager.Instance != null)
        {
            GameManager.Instance.ClearGameOverSequence();
        }

        currentHP = initialHP;
        UpdateHPText();

        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = true;
        }

        // 救出フィードバック
        if (rescueSeClip != null && audioSource != null)
        {
            audioSource.PlayOneShot(rescueSeClip, rescueSeVolume);
        }

        if (rescueVfxPrefab != null)
        {
            GameObject vfx = Instantiate(rescueVfxPrefab, transform.position, Quaternion.identity);
            if (rescueVfxDestroySeconds > 0f)
            {
                Destroy(vfx, rescueVfxDestroySeconds);
            }
        }

        FloorHealth floor = FindFirstObjectByType<FloorHealth>();
        if (floor != null)
        {
            floor.RestoreHP();
        }

        // 復帰後の無敵時間を設定（移動時間を含めた合計時間）
        float totalInvincibleTime = rescueDelaySeconds + rescueInvincibleSeconds;
        invincibleCo = StartCoroutine(InvincibleCoroutine(totalInvincibleTime));
    }

    private void AutoMoveUpdate()
    {
        if (!enableAutoMove) return;
        if (isFalling) return;

        float range = Mathf.Max(0f, autoMoveRange);
        float waitMin = Mathf.Min(autoMoveWaitMin, autoMoveWaitMax);
        float waitMax = Mathf.Max(autoMoveWaitMin, autoMoveWaitMax);

        float centerX = respawnPoint != null ? respawnPoint.position.x : transform.position.x;

        if (!autoMoveInitialized || Mathf.Abs(autoMoveCenterX - centerX) > 0.01f)
        {
            autoMoveCenterX = centerX;
            autoMoveTargetX = autoMoveCenterX + Random.Range(-range, range);
            autoMoveInitialized = true;
            autoMoveWaitUntil = 0f;
        }

        if (Time.time < autoMoveWaitUntil) return;

        float currentX = transform.position.x;
        float threshold = Mathf.Max(0.001f, autoMoveArriveThreshold);

        if (Mathf.Abs(currentX - autoMoveTargetX) <= threshold)
        {
            autoMoveTargetX = autoMoveCenterX + Random.Range(-range, range);
            autoMoveWaitUntil = Time.time + Random.Range(waitMin, waitMax);
            return;
        }

        float timeScale = SlowMotionManager.Instance != null ? SlowMotionManager.Instance.TimeScale : 1f;
        float newX = Mathf.MoveTowards(currentX, autoMoveTargetX, autoMoveSpeed * timeScale * Time.deltaTime);
        Vector3 currentPos = transform.position;
        Vector3 newPos = new Vector3(newX, currentPos.y, currentPos.z);

        if (rb2d != null)
        {
            rb2d.MovePosition(new Vector2(newX, currentPos.y));
        }
        else
        {
            transform.position = newPos;
        }
    }

    public void ForceFallByFloorBreak()
    {
        if (isFalling) return;
        currentHP = 0;
        StartFall();
    }

    public void ApplyExplosionDamage(int damage)
    {
        if (isInvincible) return;
        if (isFalling) return;

        int dmg = Mathf.Max(0, damage);
        if (dmg <= 0) return;

        TakeDamage(dmg);
    }

    // =========================================================
    // Skill System Getters/Setters
    // =========================================================

    public int InitialHP => initialHP;
    public int MaxHP => initialHP; // MaxHPはInitialHPと同じ
    public int CurrentHP => currentHP;

    /// <summary>
    /// 初期HPを設定（スキルシステム用）
    /// </summary>
    public void SetInitialHP(int value)
    {
        initialHP = Mathf.Max(1, value);
        // 現在のHPも増加（最大値まで）
        currentHP = Mathf.Min(currentHP + value - initialHP, initialHP);
        UpdateHPText();
    }

    /// <summary>
    /// HPを回復（C3スキル用）
    /// </summary>
    public void Heal(int amount)
    {
        if (isFalling) return;
        currentHP = Mathf.Min(currentHP + amount, initialHP);
        UpdateHPText();
    }
}
