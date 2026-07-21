using System.Collections;
using UnityEngine;
using TMPro;

[RequireComponent(typeof(Rigidbody2D))]
public class PixelDancerController : MonoBehaviour
{
    [Header("HP")]
    [SerializeField] private int initialHP = 99;
    private int currentHP;

    private const string PrefsKeyInitialHP = "PixelDancer_InitialHP";

    /// <summary>AreaSelectなどゲーム外シーンから参照するHP値（PlayerPrefsから自動読み取り）</summary>
    public static int SavedInitialHP => PlayerPrefs.GetInt(PrefsKeyInitialHP, 99);

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

    [Header("Heal VFX/SE (C3: SelfHeal)")]
    [SerializeField] private GameObject healVfxPrefab;
    [SerializeField] private float healVfxDestroySeconds = 1.0f;
    [SerializeField] private AudioClip healSeClip;
    [Range(0f, 1f)]
    [SerializeField] private float healSeVolume = 1f;

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

    [System.Serializable]
    public class CollapseFrame
    {
        public Sprite sprite;
        public float offsetX  = 0f;
        public float offsetY  = 0f;
        public float duration = 0.1f;
    }

    [System.Serializable]
    public class SoulFrame
    {
        public Sprite sprite;
        public float offsetX  = 0f;
        public float offsetY  = 0f;
        public float duration = 0.15f;
    }

    [Header("Soul")]
    [SerializeField] private Transform soulTransform;
    [SerializeField] private SpriteRenderer soulSpriteRenderer;
    [NonReorderable]
    [SerializeField] private CollapseFrame[] collapseFrames;
    [Tooltip("倒れるアニメのY位置を固定する（ダンス中に空中にいてもずれないようにする）")]
    [SerializeField] private bool useFixedCollapseY = false;
    [Tooltip("倒れるアニメ開始時のY位置（useFixedCollapseY=trueのとき使用）")]
    [SerializeField] private float fixedCollapseY = 0f;
    [NonReorderable]
    [SerializeField] private SoulFrame[] soulFrames;

    [Header("Editor Preview Collapse (Play前確認用)")]
    [Tooltip("-1=オフ、0以上=そのインデックスのフレームをSceneに表示")]
    [SerializeField] private int previewCollapseFrame = -1;
    [Tooltip("チェックでアニメーションをEditorで再生（各フレームのDuration使用・ループ）")]
    [SerializeField] private bool previewAnimate = false;
    [Tooltip("プレビューの向きを左右反転する")]
    [SerializeField] private bool previewFlipX = false;
    [HideInInspector] [SerializeField] private Vector3 editorCollapseBasePos;
    [HideInInspector] [SerializeField] private bool editorCollapseBaseCaptured = false;

    [Header("Editor Preview Soul (Play前確認用)")]
    [Tooltip("-1=オフ、0以上=そのインデックスのフレームをSceneに表示")]
    [SerializeField] private int previewSoulFrame = -1;
    [Tooltip("チェックでSoulアニメーションをEditorで再生（ループ）")]
    [SerializeField] private bool previewSoulAnimate = false;
    [Tooltip("SoulプレビューのX反転")]
    [SerializeField] private bool previewSoulFlipX = false;
    [HideInInspector] [SerializeField] private Vector3 editorSoulBasePos;
    [HideInInspector] [SerializeField] private bool editorSoulBaseCaptured = false;

#if UNITY_EDITOR
    private double _editorAnimLastTime;
    private int    _editorAnimFrame;
    private bool   _editorAnimRunning;
    private double _editorSoulAnimLastTime;
    private int    _editorSoulAnimFrame;
    private bool   _editorSoulAnimRunning;
#endif

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
    private Coroutine soulAnimCo;
    private Vector2 fallDirection;
    private int currentFallSign = 1;
    private Vector2 _soulPurePos;
    private Vector3 _soulAnimOffset;

    private float autoMoveCenterX;
    private float autoMoveTargetX;
    private float autoMoveWaitUntil;
    private bool autoMoveInitialized;
    private float autoMoveCurrentX;
    private bool autoMoveXReady;
    private bool isFacingLeft;
    private bool isInvertedFrame = false;

    public void OnInvertedFrameStart() => isInvertedFrame = true;
    public void OnInvertedFrameEnd()   => isInvertedFrame = false;

    private float initialPositionY;

    public static bool IsPlayerDeadGlobal { get; private set; }
    public static bool IsDownGlobal { get; private set; }

    public bool IsFalling => isFalling;
    public float AutoMoveRange => autoMoveRange;
    public Transform SoulTransform => soulTransform;

    private void OnEnable()
    {
        Game.Skills.SkillManager.OnSelfHealPixelDancer += PlayHealVfx;
        EnemySpawner.OnFinalBossDefeated += OnFinalBossDefeated;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
#endif
    }

    private void OnDisable()
    {
        Game.Skills.SkillManager.OnSelfHealPixelDancer -= PlayHealVfx;
        EnemySpawner.OnFinalBossDefeated -= OnFinalBossDefeated;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        StopEditorAnim();
        StopEditorSoulAnim();
        // SoulをInactiveに戻してPlay開始時の初期状態を保証する
        if (soulTransform != null && soulTransform.gameObject.activeSelf)
            soulTransform.gameObject.SetActive(false);
        editorSoulBaseCaptured = false;
#endif
    }

    private void OnFinalBossDefeated()
    {
        isInvincible = true;
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

    private void OnValidate()
    {
        if (Application.isPlaying) return;

#if UNITY_EDITOR
        // ── Collapse アニメプレビュー ──
        if (previewAnimate)
        {
            StartEditorAnim();
        }
        else
        {
            StopEditorAnim();

            if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();

            if (previewCollapseFrame < 0)
            {
                if (editorCollapseBaseCaptured)
                    transform.position = editorCollapseBasePos;
                editorCollapseBaseCaptured = false;
                if (spriteRenderer != null) spriteRenderer.flipX = false;
            }
            else if (collapseFrames != null && previewCollapseFrame < collapseFrames.Length)
            {
                if (!editorCollapseBaseCaptured)
                {
                    editorCollapseBasePos = transform.position;
                    editorCollapseBaseCaptured = true;
                }
                var cf = collapseFrames[previewCollapseFrame];
                if (cf != null)
                {
                    if (cf.sprite != null) spriteRenderer.sprite = cf.sprite;
                    float xOff = previewFlipX ? -cf.offsetX : cf.offsetX;
                    transform.position = editorCollapseBasePos + new Vector3(xOff, cf.offsetY, 0f);
                    spriteRenderer.flipX = previewFlipX;
                }
            }
        }

        // ── Soul アニメプレビュー ──
        HandleEditorSoulPreview();
#endif
    }

#if UNITY_EDITOR
    private void HandleEditorSoulPreview()
    {
        if (soulTransform == null) return;
        if (soulSpriteRenderer == null)
            soulSpriteRenderer = soulTransform.GetComponent<SpriteRenderer>();
        if (soulSpriteRenderer == null) return;

        bool wantActive = previewSoulAnimate || previewSoulFrame >= 0;

        if (!wantActive)
        {
            // プレビューOFF：SoulをInactiveに戻す
            if (soulTransform.gameObject.activeSelf)
                soulTransform.gameObject.SetActive(false);
            editorSoulBaseCaptured = false;
            StopEditorSoulAnim();
            return;
        }

        // プレビューON：SoulをActiveにして表示
        if (!soulTransform.gameObject.activeSelf)
            soulTransform.gameObject.SetActive(true);
        soulSpriteRenderer.enabled = true;

        if (previewSoulAnimate)
        {
            StartEditorSoulAnim();
            return;
        }

        StopEditorSoulAnim();

        if (soulFrames == null || previewSoulFrame >= soulFrames.Length) return;

        if (!editorSoulBaseCaptured)
        {
            editorSoulBasePos = soulTransform.position;
            editorSoulBaseCaptured = true;
        }

        var sf = soulFrames[previewSoulFrame];
        if (sf == null) return;
        if (sf.sprite != null) soulSpriteRenderer.sprite = sf.sprite;
        soulSpriteRenderer.flipX = previewSoulFlipX;
        float xOff = previewSoulFlipX ? -sf.offsetX : sf.offsetX;
        soulTransform.position = editorSoulBasePos + new Vector3(xOff, sf.offsetY, 0f);
    }
#endif

    private void Awake()
    {
        rb2d = GetComponent<Rigidbody2D>();
        if (bodyCollider == null) bodyCollider = GetComponent<Collider2D>();
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        if (animator == null) animator = GetComponent<Animator>();

        mainCamera = Camera.main;
        if (mainCamera == null) mainCamera = FindFirstObjectByType<Camera>();

        PlayerPrefs.SetInt(PrefsKeyInitialHP, initialHP);

        currentHP = initialHP;
        currentDown = 0;
        isFalling = false;
        IsDownGlobal = false;
        isInvincible = false;

        IsPlayerDeadGlobal = false;

        autoMoveInitialized = false;

        initialPositionY = transform.position.y;

        UpdateHPText();
    }

    private void Start()
    {
        // CameraAnchoredTransformがautoMoveと競合してカメラシェイク時に位置を上書きするため無効化
        // 初期配置はOnEnable時点で完了しているので位置は維持される
        var cat = GetComponent<CameraAnchoredTransform>();
        if (cat != null) cat.enabled = false;

        // CameraAnchoredTransformが配置した後の位置でinitialPositionYを更新
        initialPositionY = transform.position.y;
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
        if (bullet.HasPaddleReflectedOnce) return;

        int dmg = bullet.DamageValue;
        TakeDamage(dmg);
    }

    // =========================================================
    // ★追加：Beam（EnemyBulletを介さないダメージ源）から未反射区間のダメージを受け取る入口
    // OnTriggerEnter2Dと同じ無敵/落下/グローバル停止判定を経てからTakeDamageを呼ぶ
    // =========================================================
    public bool ApplyBeamDamage(int damage)
    {
        if (isInvincible) return false;
        if (isFalling) return false;
        if (IsPlayerDeadGlobal) return false;
        if (FloorHealth.IsBrokenGlobal) return false;

        TakeDamage(damage);
        return true;
    }

    private void TakeDamage(int damage)
    {
        SessionStats.AddDamageTaken(damage);
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
        // カメラシェイク＋画面フラッシュ
        CameraShake.Shake();
        DamageFlashUI.Flash();

        if (hitSeClip != null && audioSource != null)
        {
            // SoundSettingsManagerのSE音量を適用
            float finalVolume = hitSeVolume * (SoundSettingsManager.Instance != null ? SoundSettingsManager.Instance.SEVolume : 1f);
            audioSource.PlayOneShot(hitSeClip, finalVolume);
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
        if (isFalling) return;
        isFalling = true;
        IsPlayerDeadGlobal = true;

        if (bodyCollider != null) bodyCollider.enabled = false;
        currentDown = Mathf.Min(maxDown, currentDown + 1);
        SessionStats.AddDown();
        UpdateHPText();

        // 点滅を止めて本体スプライトを確実に表示
        if (blinkCo != null) { StopCoroutine(blinkCo); blinkCo = null; }
        if (spriteRenderer != null) spriteRenderer.enabled = true;

        // ダンス停止
        var animCtrl = GetComponent<PixelDancerAnimController>();
        if (animCtrl != null) animCtrl.StopDance();

        // Animatorを無効化してスプライトフレームアニメに切り替え
        if (animator != null) animator.enabled = false;

        // 倒れるアニメーション再生
        if (collapseFrames != null && collapseFrames.Length > 0)
            StartCoroutine(PlayCollapseAnim());

        // 本体はその場で静止（Kinematicに）
        if (rb2d != null)
        {
            rb2d.linearVelocity = Vector2.zero;
            rb2d.bodyType = RigidbodyType2D.Kinematic;
        }

        // 魂を本体位置でデタッチして起動
        _soulAnimOffset = Vector3.zero;
        if (soulTransform != null)
        {
            soulTransform.SetParent(null);
            soulTransform.position = transform.position;
            soulTransform.gameObject.SetActive(true);
            // Editorプレビューでdisabledになっている場合を考慮して明示的に有効化
            if (soulSpriteRenderer != null) soulSpriteRenderer.enabled = true;
        }

        // 魂アニメーション開始
        if (soulFrames != null && soulFrames.Length > 0)
            soulAnimCo = StartCoroutine(PlaySoulAnim());

        // 左右ランダム決定
        currentFallSign = randomizeLeftRight ? (Random.value < 0.5f ? -1 : 1) : 1;

        // 落下角度ランダム決定
        float minAngle = Mathf.Min(fallAngleMinDegrees, fallAngleMaxDegrees);
        float maxAngle = Mathf.Max(fallAngleMinDegrees, fallAngleMaxDegrees);
        float angleRad = Random.Range(minAngle, maxAngle) * Mathf.Deg2Rad;
        fallDirection = new Vector2(currentFallSign * Mathf.Sin(angleRad), -Mathf.Cos(angleRad)).normalized;

        IsDownGlobal = true;

        if (GameManager.Instance != null)
            GameManager.Instance.StartGameOverSequence();

        StartCoroutine(SoulFallSequence());
    }

    private IEnumerator SoulFallSequence()
    {
        if (soulTransform == null) { OnGameOver(); yield break; }

        // ホップフェーズ（斜め上に飛び上がる）
        Vector2 startPos = soulTransform.position;
        Vector2 hopTarget = startPos + Vector2.up * hopHeight + Vector2.right * (currentFallSign * hopHorizontal);

        float hopElapsed = 0f;
        while (hopElapsed < hopDuration)
        {
            hopElapsed += Time.deltaTime;
            float t = hopElapsed / hopDuration;
            t = 1f - (1f - t) * (1f - t);

            _soulPurePos = Vector2.Lerp(startPos, hopTarget, t) + Vector2.up * (4f * hopArcHeight * t * (1f - t));
            soulTransform.position = new Vector3(
                _soulPurePos.x + _soulAnimOffset.x,
                _soulPurePos.y + _soulAnimOffset.y,
                soulTransform.position.z);

            yield return null;
        }

        // 落下フェーズ（緩やかに斜め下へ）
        // ホップ終了時点の純粋な移動座標を引き継ぐ（オフセット分を除外）
        float fallSpeed = fallSpeedBase + currentDown * fallSpeedAddPerDown;
        Vector2 velocity = fallDirection * fallSpeed;

        while (isFalling)
        {
            _soulPurePos += velocity * Time.deltaTime;
            soulTransform.position = new Vector3(
                _soulPurePos.x + _soulAnimOffset.x,
                _soulPurePos.y + _soulAnimOffset.y,
                soulTransform.position.z);

            if (mainCamera != null)
            {
                Vector3 viewportPos = mainCamera.WorldToViewportPoint(soulTransform.position);
                if (viewportPos.y < -gameOverYMargin)
                {
                    OnGameOver();
                    yield break;
                }
            }

            yield return null;
        }
    }

    private IEnumerator PlayCollapseAnim()
    {
        if (spriteRenderer == null || collapseFrames == null) yield break;

        Vector3 basePos = transform.position;
        if (useFixedCollapseY) basePos.y = fixedCollapseY;

        // isFalling=true以降LateUpdateはスキップされるため、ここで向きを確定
        bool flipped = spriteRenderer.flipX;

        for (int i = 0; i < collapseFrames.Length; i++)
        {
            CollapseFrame frame = collapseFrames[i];
            if (frame.sprite != null) spriteRenderer.sprite = frame.sprite;
            float xOffset = flipped ? -frame.offsetX : frame.offsetX;
            transform.position = basePos + new Vector3(xOffset, frame.offsetY, 0f);
            yield return new WaitForSeconds(frame.duration);
        }
        // 最終フレームをそのまま表示し続ける（倒れた静止画）
    }

    private IEnumerator PlaySoulAnim()
    {
        if (soulSpriteRenderer == null && soulTransform != null)
            soulSpriteRenderer = soulTransform.GetComponent<SpriteRenderer>();
        if (soulSpriteRenderer == null || soulFrames == null || soulFrames.Length == 0) yield break;

        int idx = 0;
        while (soulTransform != null && soulTransform.gameObject.activeSelf)
        {
            SoulFrame frame = soulFrames[idx];
            if (frame.sprite != null) soulSpriteRenderer.sprite = frame.sprite;
            _soulAnimOffset = new Vector3(frame.offsetX, frame.offsetY, 0f);
            idx = (idx + 1) % soulFrames.Length;
            yield return new WaitForSeconds(frame.duration);
        }

        _soulAnimOffset = Vector3.zero;
        soulAnimCo = null;
    }

    private void OnGameOver()
    {
        isFalling = false;
        IsDownGlobal = false;

        if (soulAnimCo != null) { StopCoroutine(soulAnimCo); soulAnimCo = null; }
        _soulAnimOffset = Vector3.zero;

        if (soulTransform != null)
        {
            soulTransform.gameObject.SetActive(false);
            soulTransform.SetParent(transform);
            soulTransform.localPosition = Vector3.zero;
        }

        if (GameManager.Instance != null)
            GameManager.Instance.TriggerGameOver();
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
        IsDownGlobal = false;
        if (bodyCollider != null) bodyCollider.enabled = true;

        if (soulAnimCo != null) { StopCoroutine(soulAnimCo); soulAnimCo = null; }
        _soulAnimOffset = Vector3.zero;

        if (invincibleCo != null) { StopCoroutine(invincibleCo); invincibleCo = null; }
        isInvincible = true;

        // 魂 → 本体位置へ吸収（rescueDelaySeconds で lerp）
        if (soulTransform != null && rescueDelaySeconds > 0f)
        {
            Vector3 soulStart = soulTransform.position;
            float elapsed = 0f;
            while (elapsed < rescueDelaySeconds)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / rescueDelaySeconds);
                t = 1f - (1f - t) * (1f - t);
                soulTransform.position = Vector3.Lerp(soulStart, transform.position, t);
                yield return null;
            }
        }

        // 魂を非アクティブ化して本体の子に戻す
        if (soulTransform != null)
        {
            soulTransform.gameObject.SetActive(false);
            soulTransform.SetParent(transform);
            soulTransform.localPosition = Vector3.zero;
        }

        // 本体のrb2dをDynamicに戻す
        if (rb2d != null)
        {
            rb2d.bodyType = RigidbodyType2D.Dynamic;
            rb2d.linearVelocity = Vector2.zero;
        }

        // Animatorを再有効化
        if (animator != null) animator.enabled = true;

        IsPlayerDeadGlobal = false;
        autoMoveInitialized = false;

        if (GameManager.Instance != null)
            GameManager.Instance.ClearGameOverSequence();

        currentHP = initialHP;
        UpdateHPText();

        if (spriteRenderer != null) spriteRenderer.enabled = true;

        // 救出フィードバック
        if (rescueSeClip != null && audioSource != null)
            audioSource.PlayOneShot(rescueSeClip, rescueSeVolume);

        if (rescueVfxPrefab != null)
        {
            GameObject vfx = Instantiate(rescueVfxPrefab, transform.position, Quaternion.identity);
            if (rescueVfxDestroySeconds > 0f) Destroy(vfx, rescueVfxDestroySeconds);
        }

        FloorHealth floor = FindFirstObjectByType<FloorHealth>();
        if (floor != null) floor.RestoreHP();

        // ダンス再開
        var animCtrl = GetComponent<PixelDancerAnimController>();
        if (animCtrl != null) animCtrl.ResumeDance();

        invincibleCo = StartCoroutine(InvincibleCoroutine(rescueInvincibleSeconds));
    }

    private void AutoMoveUpdate()
    {
        if (!enableAutoMove) return;
        if (isFalling) return;
        if (IsPlayerDeadGlobal) return;

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

        autoMoveCurrentX = newX;
        autoMoveXReady = true;

        if (newX < currentX) isFacingLeft = true;
        else if (newX > currentX) isFacingLeft = false;
    }

    private void LateUpdate()
    {
        if (!enableAutoMove) return;
        if (isFalling) return;
        if (IsPlayerDeadGlobal) return;
        if (!autoMoveXReady) return;

        Vector3 pos = transform.position;
        pos.x = autoMoveCurrentX;
        transform.position = pos;
        if (rb2d != null) rb2d.position = new Vector2(autoMoveCurrentX, rb2d.position.y);

        if (spriteRenderer != null)
        {
            if (isFacingLeft)
            {
                spriteRenderer.flipX = !isInvertedFrame;
                spriteRenderer.flipY = isInvertedFrame;
            }
            else
            {
                spriteRenderer.flipX = false;
                spriteRenderer.flipY = false;
            }
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
        // 最大値のみ変更。現在値は新しい最大値を超えないようクランプするだけ
        currentHP = Mathf.Min(currentHP, initialHP);
        UpdateHPText();
    }

    /// <summary>
    /// HPを現在の最大値まで全回復（ゲーム開始時専用）
    /// </summary>
    public void RestoreToFullHP()
    {
        currentHP = initialHP;
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

    // =========================================================
    // Editor Preview Animation (UNITY_EDITOR only)
    // =========================================================

#if UNITY_EDITOR
    private void StartEditorAnim()
    {
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        if (collapseFrames == null || collapseFrames.Length == 0) return;

        if (!editorCollapseBaseCaptured)
        {
            editorCollapseBasePos = transform.position;
            editorCollapseBaseCaptured = true;
        }

        if (!_editorAnimRunning)
        {
            _editorAnimFrame = 0;
            _editorAnimLastTime = UnityEditor.EditorApplication.timeSinceStartup;
            _editorAnimRunning = true;
            UnityEditor.EditorApplication.update += OnEditorUpdate;
        }

        ApplyEditorFrame(_editorAnimFrame);
    }

    private void StopEditorAnim()
    {
        if (!_editorAnimRunning) return;
        _editorAnimRunning = false;
        UnityEditor.EditorApplication.update -= OnEditorUpdate;
    }

    private void OnEditorUpdate()
    {
        if (this == null || !_editorAnimRunning || collapseFrames == null || collapseFrames.Length == 0)
        {
            StopEditorAnim();
            return;
        }

        double now = UnityEditor.EditorApplication.timeSinceStartup;
        double frameDuration = collapseFrames[_editorAnimFrame].duration;

        if (now - _editorAnimLastTime >= frameDuration)
        {
            _editorAnimLastTime = now;
            _editorAnimFrame = (_editorAnimFrame + 1) % collapseFrames.Length;
            ApplyEditorFrame(_editorAnimFrame);
        }
    }

    private void ApplyEditorFrame(int index)
    {
        if (collapseFrames == null || index >= collapseFrames.Length) return;
        var frame = collapseFrames[index];
        if (frame == null) return;

        if (spriteRenderer != null)
        {
            if (frame.sprite != null) spriteRenderer.sprite = frame.sprite;
            spriteRenderer.flipX = previewFlipX;
        }

        float xOff = previewFlipX ? -frame.offsetX : frame.offsetX;
        transform.position = editorCollapseBasePos + new Vector3(xOff, frame.offsetY, 0f);

        UnityEditor.SceneView.RepaintAll();
    }

    private void OnPlayModeStateChanged(UnityEditor.PlayModeStateChange state)
    {
        if (state == UnityEditor.PlayModeStateChange.ExitingEditMode)
        {
            StopEditorAnim();
            StopEditorSoulAnim();
            if (soulTransform != null && soulTransform.gameObject.activeSelf)
                soulTransform.gameObject.SetActive(false);
            editorSoulBaseCaptured = false;
        }
    }

    private void StartEditorSoulAnim()
    {
        if (soulSpriteRenderer == null && soulTransform != null)
            soulSpriteRenderer = soulTransform.GetComponent<SpriteRenderer>();
        if (soulSpriteRenderer == null || soulFrames == null || soulFrames.Length == 0) return;

        if (!editorSoulBaseCaptured)
        {
            editorSoulBasePos = soulTransform.position;
            editorSoulBaseCaptured = true;
        }

        soulSpriteRenderer.enabled = true;

        if (!_editorSoulAnimRunning)
        {
            _editorSoulAnimFrame = 0;
            _editorSoulAnimLastTime = UnityEditor.EditorApplication.timeSinceStartup;
            _editorSoulAnimRunning = true;
            UnityEditor.EditorApplication.update += OnEditorSoulUpdate;
        }

        ApplyEditorSoulFrame(_editorSoulAnimFrame);
    }

    private void StopEditorSoulAnim()
    {
        if (!_editorSoulAnimRunning) return;
        _editorSoulAnimRunning = false;
        UnityEditor.EditorApplication.update -= OnEditorSoulUpdate;
        // SoulのActiveはHandleEditorSoulPreviewが管理するためここでは触らない
    }

    private void OnEditorSoulUpdate()
    {
        if (this == null || !_editorSoulAnimRunning || soulFrames == null || soulFrames.Length == 0)
        {
            StopEditorSoulAnim();
            return;
        }

        double now = UnityEditor.EditorApplication.timeSinceStartup;
        if (now - _editorSoulAnimLastTime >= soulFrames[_editorSoulAnimFrame].duration)
        {
            _editorSoulAnimLastTime = now;
            _editorSoulAnimFrame = (_editorSoulAnimFrame + 1) % soulFrames.Length;
            ApplyEditorSoulFrame(_editorSoulAnimFrame);
        }
    }

    private void ApplyEditorSoulFrame(int index)
    {
        if (soulFrames == null || index >= soulFrames.Length || soulTransform == null) return;
        var frame = soulFrames[index];
        if (frame == null) return;

        if (soulSpriteRenderer != null)
        {
            if (frame.sprite != null) soulSpriteRenderer.sprite = frame.sprite;
            soulSpriteRenderer.flipX = previewSoulFlipX;
        }

        float xOff = previewSoulFlipX ? -frame.offsetX : frame.offsetX;
        soulTransform.position = editorSoulBasePos + new Vector3(xOff, frame.offsetY, 0f);

        UnityEditor.SceneView.RepaintAll();
    }
#endif
}
