using System.Collections;
using UnityEngine;

/// <summary>
/// GravePole ボスのオーブギミック（ブロック扱い・HP 無限）。
///
/// ■ 仕様
///   - 反射弾のブロックダメージを蓄積し、閾値到達で 60 秒間「発光」状態になる
///   - 発光中は GravePoleEnemy に通知し、本体への HP ダメージを 3 倍にする
///   - 発光終了後はカウンターをリセットし再度蓄積を開始する
///   - Phase 1 : 閾値 4、移動なし
///   - Phase 2 : 閾値 6、Wave 上下移動を開始
/// </summary>
[DisallowMultipleComponent]
public class OrbGimmick : MonoBehaviour
{
    [Header("ダメージ閾値")]
    [SerializeField] private int activateThresholdPhase1 = 4;
    [SerializeField] private int activateThresholdPhase2 = 6;

    [Header("発光時間")]
    [SerializeField] private float glowDuration = 60f;

    [Header("発光ビジュアル")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color glowColor   = new Color(1f, 0.85f, 0.1f, 1f);

    [Tooltip("発光中に再生するパーティクル等（任意）")]
    [SerializeField] private GameObject glowVfxPrefab;

    [Header("左右揺れ（常時）")]
    [Tooltip("横方向の揺れ幅（ワールド単位）。0で無効。")]
    [SerializeField] private float xSwingAmplitude = 0.3f;
    [SerializeField] private float xSwingSpeed     = 0.8f;

    [Header("回転")]
    [Tooltip("回転速度（度/秒）。0で無効。")]
    [SerializeField] private float rotationSpeed          = 60f;
    [Tooltip("回転方向を切り替える間隔（秒）")]
    [SerializeField] private float rotationSwitchInterval = 3f;

    [Header("フェーズ2 上下Wave移動")]
    [SerializeField] private float waveAmplitude            = 1.5f;
    [Tooltip("左右のOrbで振れ幅を変えるランダム幅（±この値）。0で固定。")]
    [SerializeField] private float waveAmplitudeRandomRange = 0.5f;
    [SerializeField] private float waveSpeed                = 1.2f;

    [Header("ヒット SE（通常時）")]
    [Tooltip("WallHealthと同様にPrefabにAudioSourceを追加しここに設定する")]
    [SerializeField] private AudioSource hitSeSource;
    [SerializeField] private AudioClip[] hitClips = new AudioClip[3];
    [Range(0f, 1f)]
    [SerializeField] private float hitVolume = 0.8f;

    [Header("ヒット SE（発光中）")]
    [SerializeField] private AudioClip[] glowHitClips = new AudioClip[3];
    [Range(0f, 1f)]
    [SerializeField] private float glowHitVolume = 0.8f;

    [Header("ヒット VFX（通常時）")]
    [SerializeField] private GameObject hitVfxPrefab;

    [Header("ヒット VFX（発光中）")]
    [SerializeField] private GameObject glowHitVfxPrefab;

    [Header("蓄積ダメージ表示 (TextMesh)")]
    [SerializeField] private bool    showDamageLabel = true;
    [Tooltip("Orbの上に表示するオフセット（ワールド単位）")]
    [SerializeField] private Vector3 labelOffset    = new Vector3(0f, 0.8f, 0f);
    [SerializeField] private Color   labelColor     = Color.white;
    [SerializeField] private int     labelFontSize  = 60;

    [Header("同フレーム多重ヒット防止")]
    [SerializeField] private float hitCooldown = 0.1f;

    // =========================================================
    // ランタイム
    // =========================================================

    private int   phase              = 1;
    private int   accumulatedDamage  = 0;
    private bool  isGlowing          = false;
    private float nextHitAllowedTime = 0f;

    private Vector3 basePosition;
    private float   xSwingTime        = 0f;
    private float   waveTime          = 0f;
    private bool    phase2WaveActive  = false;
    private float   actualWaveAmplitude = 0f;
    private float   rotationDir       = 1f;   // +1=反時計回り、-1=時計回り
    private float   rotationTimer     = 0f;

    private SpriteRenderer cachedRenderer;
    private GameObject     activeGlowVfx;
    private TextMesh       damageLabel;

    public GravePoleEnemy Boss { get; set; }

    // =========================================================
    // Unity ライフサイクル
    // =========================================================

    private void Awake()
    {
        cachedRenderer = GetComponent<SpriteRenderer>();

        // AudioSource が Inspector で未設定なら自動取得
        if (hitSeSource == null) hitSeSource = GetComponent<AudioSource>();
        if (hitSeSource == null)
        {
            hitSeSource = gameObject.AddComponent<AudioSource>();
            hitSeSource.playOnAwake  = false;
            hitSeSource.loop         = false;
            hitSeSource.spatialBlend = 0f;
        }
    }

    private void Start()
    {
        basePosition = transform.position;

        if (cachedRenderer != null)
            cachedRenderer.color = normalColor;

        if (showDamageLabel) CreateDamageLabel();
        RefreshLabel();
    }

    private void Update()
    {
        UpdateMovement();
        UpdateRotation();
    }

    // =========================================================
    // フェーズ切り替え
    // =========================================================

    public void SetPhase(int p)
    {
        phase = p;
        if (phase == 2 && !phase2WaveActive)
        {
            phase2WaveActive    = true;
            basePosition        = transform.position;
            // 左右で異なる振れ幅になるようランダム決定
            actualWaveAmplitude = waveAmplitude
                + Random.Range(-waveAmplitudeRandomRange, waveAmplitudeRandomRange);
            actualWaveAmplitude = Mathf.Max(0f, actualWaveAmplitude);
        }
        RefreshLabel();
    }

    // =========================================================
    // 衝突検出
    // =========================================================

    private void OnTriggerEnter2D(Collider2D other)        => HandleHit(other);
    private void OnCollisionEnter2D(Collision2D collision) => HandleHit(collision.collider);

    private void HandleHit(Collider2D other)
    {
        if (other == null) return;
        if (Time.time < nextHitAllowedTime) return;

        EnemyBullet bullet = other.GetComponent<EnemyBullet>()
                          ?? other.GetComponentInParent<EnemyBullet>();
        if (bullet == null) return;

        if (!bullet.HasPaddleReflectedOnce && bullet.DamageMultiplier <= 1.0001f) return;

        nextHitAllowedTime = Time.time + hitCooldown;

        // 発光中はSEとVFXのみ、ダメージ蓄積はしない
        if (isGlowing)
        {
            PlayGlowHitSe();
            SpawnVfx(glowHitVfxPrefab, other.transform.position);
            return;
        }

        float rawDmg = (bullet.DamageMultiplier > 1.0001f)
            ? bullet.BlockJustDamage
            : bullet.BlockNormalDamage;
        int hitDmg = Mathf.Max(1, Mathf.RoundToInt(rawDmg));
        accumulatedDamage += hitDmg;

        PlayHitSe();
        SpawnVfx(hitVfxPrefab, other.transform.position);
        RefreshLabel();

        int threshold = (phase == 1) ? activateThresholdPhase1 : activateThresholdPhase2;
        if (accumulatedDamage >= threshold)
            StartCoroutine(GlowRoutine());
    }

    // =========================================================
    // 発光シーケンス
    // =========================================================

    private IEnumerator GlowRoutine()
    {
        isGlowing        = true;
        accumulatedDamage = 0;

        if (cachedRenderer != null) cachedRenderer.color = glowColor;
        if (damageLabel != null)    damageLabel.text      = "!";

        if (glowVfxPrefab != null)
            activeGlowVfx = Instantiate(glowVfxPrefab, transform.position,
                                         Quaternion.identity, transform);

        Boss?.OnOrbActivated(this);

        yield return new WaitForSeconds(glowDuration);

        isGlowing = false;

        if (cachedRenderer != null) cachedRenderer.color = normalColor;
        if (activeGlowVfx != null) { Destroy(activeGlowVfx); activeGlowVfx = null; }

        Boss?.OnOrbDeactivated(this);
        RefreshLabel();
    }

    // =========================================================
    // SE
    // =========================================================

    private void SpawnVfx(GameObject prefab, Vector3 pos)
    {
        if (prefab == null) return;
        Instantiate(prefab, pos, Quaternion.identity);
    }

    private void PlayGlowHitSe()
    {
        if (hitSeSource == null || glowHitClips == null) return;

        int valid = 0;
        foreach (var c in glowHitClips) if (c != null) valid++;
        if (valid == 0) return;

        int pick = UnityEngine.Random.Range(0, valid);
        foreach (var c in glowHitClips)
        {
            if (c == null) continue;
            if (pick == 0)
            {
                float vol = glowHitVolume *
                    (SoundSettingsManager.Instance != null
                        ? SoundSettingsManager.Instance.SEVolume : 1f);
                hitSeSource.PlayOneShot(c, vol);
                return;
            }
            pick--;
        }
    }

    private void PlayHitSe()
    {
        if (hitSeSource == null || hitClips == null) return;

        int valid = 0;
        foreach (var c in hitClips) if (c != null) valid++;
        if (valid == 0) return;

        int pick = UnityEngine.Random.Range(0, valid);
        foreach (var c in hitClips)
        {
            if (c == null) continue;
            if (pick == 0)
            {
                float vol = hitVolume *
                    (SoundSettingsManager.Instance != null
                        ? SoundSettingsManager.Instance.SEVolume : 1f);
                hitSeSource.PlayOneShot(c, vol);
                return;
            }
            pick--;
        }
    }

    // =========================================================
    // 蓄積ダメージ表示（TextMesh）
    // =========================================================

    private void CreateDamageLabel()
    {
        var go = new GameObject("DamageLabel");
        go.transform.SetParent(transform);
        go.transform.localPosition = labelOffset;

        damageLabel               = go.AddComponent<TextMesh>();
        damageLabel.anchor        = TextAnchor.MiddleCenter;
        damageLabel.alignment     = TextAlignment.Center;
        damageLabel.fontSize      = labelFontSize;
        damageLabel.characterSize = 0.05f;
        damageLabel.color         = labelColor;
    }

    private void RefreshLabel()
    {
        if (damageLabel == null) return;
        if (isGlowing)
        {
            damageLabel.text = "!";
            return;
        }
        int threshold = (phase == 1) ? activateThresholdPhase1 : activateThresholdPhase2;
        damageLabel.text = $"{accumulatedDamage}/{threshold}";
    }

    // =========================================================
    // 移動 / 回転
    // =========================================================

    private void UpdateMovement()
    {
        float offsetX = 0f;
        float offsetY = 0f;

        if (xSwingAmplitude > 0f)
        {
            xSwingTime += Time.deltaTime;
            offsetX = Mathf.Sin(xSwingTime * xSwingSpeed) * xSwingAmplitude;
        }

        if (phase2WaveActive)
        {
            waveTime += Time.deltaTime;
            offsetY = Mathf.Sin(waveTime * waveSpeed) * actualWaveAmplitude;
        }

        transform.position = basePosition + new Vector3(offsetX, offsetY, 0f);
    }

    private void UpdateRotation()
    {
        if (rotationSpeed <= 0f) return;

        rotationTimer += Time.deltaTime;
        if (rotationTimer >= rotationSwitchInterval)
        {
            rotationDir   = -rotationDir;
            rotationTimer = 0f;
        }

        // UnityのZ回転：正=反時計回り（画面手前から見て）
        transform.Rotate(0f, 0f, rotationSpeed * rotationDir * Time.deltaTime);
    }
}
