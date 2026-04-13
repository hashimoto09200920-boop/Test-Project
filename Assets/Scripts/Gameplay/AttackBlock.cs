using System.Collections;
using UnityEngine;

/// <summary>
/// GravePole ボス専用アタックブロック。
/// 破壊不可。タイマー後に N 回点滅 → 爆発エフェクト → 弾発射 → 自滅。
/// GravePoleEnemy から SetPhase() / SetFuseSeconds() でフェーズ・起爆時間を受け取る。
///
/// ■ フェーズ別弾仕様
///   Phase 1 : Normal 弾 1 発（プレイヤー範囲内ランダム照準）
///   Phase 2 : 50% 確率で Normal 1 発 / Multi 3 発扇形
/// </summary>
[DisallowMultipleComponent]
public class AttackBlock : MonoBehaviour
{
    [Header("HP（反射弾で破壊可能）")]
    [Tooltip("0以下にするとHP無限（破壊不可・従来動作）")]
    [SerializeField] private int maxHp = 0;

    [Header("ヒット SE")]
    [Tooltip("PrefabにAudioSourceを追加しここに設定する")]
    [SerializeField] private AudioSource hitSeSource;
    [SerializeField] private AudioClip[] hitClips = new AudioClip[3];
    [Range(0f, 1f)]
    [SerializeField] private float hitVolume = 0.8f;

    [Header("ヒット VFX")]
    [SerializeField] private GameObject hitVfxPrefab;
    [SerializeField] private float hitVfxDestroySeconds = 0.35f;

    [Header("HP表示 (TextMesh)")]
    [SerializeField] private bool    showHpLabel     = true;
    [SerializeField] private Vector3 hpLabelOffset   = new Vector3(0f, 0.6f, 0f);
    [SerializeField] private Color   hpLabelColor    = Color.white;
    [SerializeField] private int     hpLabelFontSize = 60;

    [Header("起爆タイミング")]
    [Tooltip("配置から爆発までの合計秒数（点滅時間を含む）")]
    [SerializeField] private float fuseSeconds = 10f;

    [Tooltip("点滅回数")]
    [SerializeField] private int blinkCount = 3;

    [Tooltip("点滅 1 回の ON / OFF それぞれの時間（秒）")]
    [SerializeField] private float blinkInterval = 0.5f;

    [Header("爆発 VFX / SE")]
    [SerializeField] private GameObject explosionVfxPrefab;
    [SerializeField] private float vfxDestroySeconds = 1.5f;
    [SerializeField] private AudioClip explosionClip;
    [Range(0f, 1f)]
    [SerializeField] private float explosionVolume = 1f;

    [Header("弾設定")]
    [Tooltip("発射する EnemyBullet の Prefab")]
    [SerializeField] private EnemyBullet bulletPrefab;
    [SerializeField] private float bulletSpeed = 5f;
    [SerializeField] private float bulletLifeTime = 8f;
    [SerializeField] private int bulletDamage = 1;

    [Header("Multi ショット（Phase 2）")]
    [SerializeField] private int multiShotCount = 3;
    [Tooltip("扇形の半角（度）")]
    [SerializeField] private float multiShotHalfSpread = 25f;

    [Tooltip("Phase 2 で Multi ショットを選ぶ確率（0〜1）")]
    [SerializeField] private float multiShotProbability = 0.5f;

    // =========================================================
    // ランタイム
    // =========================================================

    private int phase = 1;
    private bool hasDetonated = false;
    private SpriteRenderer cachedRenderer;
    private Transform projectileRoot;

    private int      currentHp;
    private TextMesh hpLabel;
    private int      lastHitFrame  = -999;
    private int      lastBulletId  = 0;

    // =========================================================
    // Unity ライフサイクル
    // =========================================================

    private void Awake()
    {
        cachedRenderer = GetComponent<SpriteRenderer>();
        currentHp = Mathf.Max(0, maxHp);

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
        EnemySpawner spawner = FindFirstObjectByType<EnemySpawner>();
        if (spawner != null)
            projectileRoot = spawner.ProjectileRoot;

        if (projectileRoot == null)
            Debug.LogWarning("[AttackBlock] EnemySpawner.ProjectileRoot が null。EnemySpawnerのInspectorでprojectileRootを設定してください。");

        if (maxHp > 0 && showHpLabel) CreateHpLabel();

        StartCoroutine(FuseRoutine());
    }

    private void OnCollisionEnter2D(Collision2D col)
    {
        if (maxHp <= 0 || hasDetonated) return;
        Vector3 hitPoint = col.contactCount > 0
            ? (Vector3)col.GetContact(0).point
            : transform.position;
        HandleBulletHit(col.collider, hitPoint);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (maxHp <= 0 || hasDetonated) return;
        HandleBulletHit(other, other.transform.position);
    }

    private void HandleBulletHit(Collider2D other, Vector3 hitPoint)
    {
        if (other == null) return;

        EnemyBullet bullet = other.GetComponent<EnemyBullet>()
                          ?? other.GetComponentInParent<EnemyBullet>();
        if (bullet == null) return;

        int bulletId = bullet.GetInstanceID();
        if (Time.frameCount == lastHitFrame && bulletId == lastBulletId) return;
        lastHitFrame = Time.frameCount;
        lastBulletId = bulletId;

        bool isJust = bullet.DamageMultiplier > 1.0001f;
        int dmg = isJust
            ? Mathf.Max(1, Mathf.RoundToInt(bullet.BlockJustDamage))
            : (bullet.HasPaddleReflectedOnce ? Mathf.Max(1, Mathf.RoundToInt(bullet.BlockNormalDamage)) : 0);
        if (dmg <= 0) return;

        currentHp -= dmg;
        PlayHitSe();
        SpawnHitVfx(hitPoint);
        RefreshHpLabel();

        if (currentHp <= 0)
            Detonate();
    }

    // =========================================================
    // 外部 API（GravePoleEnemy から呼び出し）
    // =========================================================

    public void SetPhase(int p)          => phase = p;
    public void SetFuseSeconds(float s)  => fuseSeconds = Mathf.Max(0.1f, s);

    // =========================================================
    // 起爆シーケンス
    // =========================================================

    private IEnumerator FuseRoutine()
    {
        float blinkTotalDuration = blinkCount * blinkInterval * 2f;
        float waitBeforeBlink = Mathf.Max(0f, fuseSeconds - blinkTotalDuration);
        yield return new WaitForSeconds(waitBeforeBlink);

        // N 回点滅
        for (int i = 0; i < blinkCount; i++)
        {
            if (cachedRenderer != null) cachedRenderer.enabled = false;
            yield return new WaitForSeconds(blinkInterval);
            if (cachedRenderer != null) cachedRenderer.enabled = true;
            yield return new WaitForSeconds(blinkInterval);
        }

        Detonate();
    }

    private void Detonate()
    {
        if (hasDetonated) return;
        hasDetonated = true;

        // 爆発 VFX（演出のみ・ダメージなし）
        if (explosionVfxPrefab != null)
        {
            GameObject vfx = Instantiate(explosionVfxPrefab, transform.position, Quaternion.identity);
            if (projectileRoot != null) vfx.transform.SetParent(projectileRoot, true);
            if (vfxDestroySeconds > 0f) Destroy(vfx, vfxDestroySeconds);
        }

        // 爆発 SE
        if (explosionClip != null)
        {
            float vol = explosionVolume *
                (SoundSettingsManager.Instance != null ? SoundSettingsManager.Instance.SEVolume : 1f);
            AudioSource.PlayClipAtPoint(explosionClip, transform.position, vol);
        }

        // 弾発射
        FireBullets();

        Destroy(gameObject);
    }

    // =========================================================
    // 弾発射
    // =========================================================

    private void FireBullets()
    {
        if (bulletPrefab == null)   { Debug.LogWarning("[AttackBlock] FireBullets: bulletPrefab が null"); return; }
        if (projectileRoot == null) { Debug.LogWarning("[AttackBlock] FireBullets: projectileRoot が null"); return; }
        if (FloorHealth.IsBrokenGlobal || PixelDancerController.IsPlayerDeadGlobal) return;

        bool useMulti = (phase == 2) && (Random.value < multiShotProbability);
        int shots     = useMulti ? multiShotCount : 1;
        float half    = useMulti ? multiShotHalfSpread : 0f;

        Vector2 baseDir = ComputeAimDirection();

        for (int i = 0; i < shots; i++)
        {
            float ang  = (half > 0.0001f) ? Random.Range(-half, half) : 0f;
            Vector2 dir = RotateVector2(baseDir, ang);
            if (dir.sqrMagnitude <= 0.0001f) dir = baseDir;

            EnemyBullet bullet = Instantiate(bulletPrefab, transform.position,
                                             Quaternion.identity, projectileRoot);
            bullet.SetDirection(dir.normalized);
            bullet.ApplyBullet(bulletSpeed, bulletLifeTime);
            bullet.SetDamage(bulletDamage);
        }
    }

    /// <summary>
    /// EnemyShooter.ComputeFinalDirection の
    /// TowardRandomPointInPlayerRange と同等の照準計算。
    /// 20% の確率でプレイヤー直撃を狙い、80% はプレイヤー移動範囲内のランダム点を狙う。
    /// </summary>
    private Vector2 ComputeAimDirection()
    {
        Vector2 defaultDir = Vector2.down;

        PixelDancerController dancer = FindFirstObjectByType<PixelDancerController>();
        if (dancer == null) return defaultDir;

        // 稀に（20%）Floor 外（プレイヤー直撃）を狙う
        if (Random.value < 0.2f)
        {
            Vector2 dir = ((Vector2)dancer.transform.position - (Vector2)transform.position).normalized;
            return (dir.sqrMagnitude > 0.0001f) ? dir : defaultDir;
        }

        // 通常：プレイヤー移動範囲内のランダム点を狙う
        float range = 3f;
        System.Reflection.FieldInfo fi = typeof(PixelDancerController).GetField(
            "autoMoveRange",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        if (fi != null && fi.GetValue(dancer) is float f) range = f;

        float targetX = dancer.transform.position.x + Random.Range(-range, range);
        Vector2 target = new Vector2(targetX, dancer.transform.position.y);
        Vector2 d = (target - (Vector2)transform.position).normalized;
        return (d.sqrMagnitude > 0.0001f) ? d : defaultDir;
    }

    private static Vector2 RotateVector2(Vector2 v, float degrees)
    {
        float rad = degrees * Mathf.Deg2Rad;
        float s   = Mathf.Sin(rad);
        float c   = Mathf.Cos(rad);
        return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
    }

    // =========================================================
    // SE / VFX / HP表示
    // =========================================================

    private void PlayHitSe()
    {
        if (hitSeSource == null || hitClips == null) return;
        int valid = 0;
        foreach (var c in hitClips) if (c != null) valid++;
        if (valid == 0) return;

        int pick = Random.Range(0, valid);
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

    private void SpawnHitVfx(Vector3 hitPoint)
    {
        if (hitVfxPrefab == null) return;
        GameObject vfx = Instantiate(hitVfxPrefab, hitPoint, Quaternion.identity);
        if (projectileRoot != null) vfx.transform.SetParent(projectileRoot, true);
        if (hitVfxDestroySeconds > 0f) Destroy(vfx, hitVfxDestroySeconds);
    }

    private void CreateHpLabel()
    {
        var go = new GameObject("HP_Label");
        go.transform.SetParent(transform);
        go.transform.localPosition = hpLabelOffset;

        hpLabel               = go.AddComponent<TextMesh>();
        hpLabel.anchor        = TextAnchor.MiddleCenter;
        hpLabel.alignment     = TextAlignment.Center;
        hpLabel.fontSize      = hpLabelFontSize;
        hpLabel.characterSize = 0.05f;
        hpLabel.color         = hpLabelColor;
        hpLabel.text          = currentHp.ToString();
    }

    private void RefreshHpLabel()
    {
        if (hpLabel == null) return;
        hpLabel.text = Mathf.Max(0, currentHp).ToString();
    }
}
