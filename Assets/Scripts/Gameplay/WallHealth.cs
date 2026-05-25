using UnityEngine;

[DisallowMultipleComponent]
public class WallHealth : MonoBehaviour
{
    [Header("HP")]
    [SerializeField] private int maxHp = 5;

    [Header("Damage by Bullet State")]
    [Tooltip("未反射弾（白/赤で一度も反射していない）")]
    [SerializeField] private int damageUnreflected = 0;

    [Tooltip("通常反射弾（白/赤で反射済み、DamageMultiplier==1）")]
    [SerializeField] private int damageNormalReflected = 1;

    [Tooltip("Just反射弾（DamageMultiplier>1）")]
    [SerializeField] private int damageJustReflected = 2;

    [Header("Break Visual / Collision")]
    [SerializeField] private bool disableColliderOnBreak = true;
    [SerializeField] private bool disableRendererOnBreak = true;

    [Header("Break VFX (reuse WallHitVFX)")]
    [Tooltip("既存の WallHitVFX.prefab を割り当て（未設定ならVFXなし）")]
    [SerializeField] private GameObject breakVfxPrefab;

    [Tooltip("未指定ならシーン内の ProjectileRoot を自動検索して親にする")]
    [SerializeField] private Transform vfxParent;

    [Header("Hit VFX (弾ヒット時・破壊されない場合)")]
    [Tooltip("弾がヒットしたが破壊されなかった時のVFX Prefab。未設定なら出ない。")]
    [SerializeField] private GameObject hitVfxPrefab;

    [Tooltip("ヒットVFXを破棄する秒数")]
    [SerializeField] private float hitVfxDestroySeconds = 0.35f;

    [Header("Hit SFX (弾ヒット時・破壊されない場合)")]
    [Tooltip("弾ヒットSE（3種ランダム）。未設定なら鳴らない。")]
    [SerializeField] private AudioClip[] hitClips = new AudioClip[3];

    [Range(0f, 1f)]
    [SerializeField] private float hitVolume = 1f;

    [Header("Break VFX/SFX")]
    [Header("Break SFX (Random 3 clips / Fixed volume)")]
    [Tooltip("破壊SE（3種ランダム）。サイズは1以上でも動くが、3推奨。")]
    [SerializeField] private AudioClip[] breakClips = new AudioClip[3];

    [Range(0f, 1f)]
    [SerializeField] private float breakVolume = 1f;

    [Tooltip("未指定なら自動でAudioSourceを追加して鳴らす（Play One Shot）")]
    [SerializeField] private AudioSource breakAudioSource;

    [Header("Debug")]
    [SerializeField] private bool logDebug = false;

    /// <summary>ブロック破壊時に発火（将来のアイテムドロップ用）引数は破壊位置</summary>
    public event System.Action<Vector3> OnBroken;

    /// <summary>ブロック破壊時にシーン全体へ通知（BlockItemManager購読用）</summary>
    public static event System.Action<Vector3> OnAnyBlockBroken;

    private int currentHp;
    private bool isBroken;

    // 同フレーム多重ヒット抑止（Stay/複数接触の連打対策）
    private int lastHitFrame = -999;
    private int lastBulletId = 0;

    private Collider2D cachedCol;
    private SpriteRenderer cachedRenderer;

    private enum BulletState
    {
        Unreflected,
        NormalReflected,
        JustReflected
    }

    private void Awake()
    {
        currentHp = Mathf.Max(0, maxHp);

        cachedCol = GetComponent<Collider2D>();
        cachedRenderer = GetComponent<SpriteRenderer>();

        // Break SE source
        if (breakAudioSource == null)
        {
            breakAudioSource = GetComponent<AudioSource>();
        }
        if (breakAudioSource == null)
        {
            breakAudioSource = gameObject.AddComponent<AudioSource>();
            breakAudioSource.playOnAwake = false;
            breakAudioSource.loop = false;
            breakAudioSource.spatialBlend = 0f; // 2D
        }

        // VFX parent auto（Hierarchy前提：05_Game > Gameplay > ProjectileRoot）
        if (vfxParent == null)
        {
            GameObject pr = GameObject.Find("ProjectileRoot");
            if (pr != null) vfxParent = pr.transform;
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (isBroken) return;
        if (collision == null || collision.collider == null) return;

        Vector3 p = collision.GetContact(0).point;
        HandleHit(collision.collider, p);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isBroken) return;
        if (other == null) return;

        // Triggerの場合は近似点（必要十分）
        Vector3 p = other.transform.position;
        HandleHit(other, p);
    }

    // =========================================================
    // ColliderProxy から転送されるイベント（子オブジェクトにCollider2Dを置く場合）
    // =========================================================
    public void OnChildCollisionEnter2D(Collision2D collision)
    {
        if (isBroken) return;
        if (collision == null || collision.collider == null) return;

        Vector3 p = collision.GetContact(0).point;
        HandleHit(collision.collider, p);
    }

    public void OnChildTriggerEnter2D(Collider2D other)
    {
        if (isBroken) return;
        if (other == null) return;

        Vector3 p = other.transform.position;
        HandleHit(other, p);
    }

    private void HandleHit(Collider2D other, Vector3 hitPoint)
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

        BulletState state = EvaluateBulletState(bullet);
        int dmg = GetDamage(state, bullet);

        if (logDebug)
        {
            Debug.Log($"[WallHealth] {name} Hit / state={state} dmg={dmg} hp={currentHp}", this);
        }

        if (dmg <= 0) return;

        currentHp -= dmg;

        if (currentHp <= 0)
        {
            Break(hitPoint);
        }
        else
        {
            PlayHit(hitPoint);
        }
    }

    private BulletState EvaluateBulletState(EnemyBullet bullet)
    {
        // Just反射：DamageMultiplier > 1
        if (bullet.DamageMultiplier > 1.0001f) return BulletState.JustReflected;

        // 通常反射：白/赤線で一度でも反射した弾
        if (bullet.HasPaddleReflectedOnce) return BulletState.NormalReflected;

        // 未反射
        return BulletState.Unreflected;
    }

    private int GetDamage(BulletState state, EnemyBullet bullet)
    {
        switch (state)
        {
            case BulletState.Unreflected:
                // 未反射弾はダメージなし（固定値を使用）
                return damageUnreflected;
            case BulletState.NormalReflected:
                // 通常反射弾：弾のブロック専用ダメージを使用
                return Mathf.RoundToInt(bullet.BlockNormalDamage);
            case BulletState.JustReflected:
                // Just反射弾：弾のブロックJustダメージを使用
                return Mathf.RoundToInt(bullet.BlockJustDamage);
            default:
                return 0;
        }
    }

    private void PlayHit(Vector3 hitPoint)
    {
        // VFX
        if (hitVfxPrefab != null)
        {
            GameObject vfx = Instantiate(hitVfxPrefab, hitPoint, Quaternion.identity);
            if (vfxParent != null) vfx.transform.SetParent(vfxParent, true);
            if (hitVfxDestroySeconds > 0f) Destroy(vfx, hitVfxDestroySeconds);
        }

        // SE
        AudioClip clip = PickRandomClip(hitClips);
        if (clip != null && breakAudioSource != null)
        {
            float finalVolume = hitVolume * (SoundSettingsManager.Instance != null ? SoundSettingsManager.Instance.SEVolume : 1f);
            breakAudioSource.PlayOneShot(clip, finalVolume);
        }
    }

    /// <summary>Instantiate後にHPを上書きする（StageBlockSpawnerのArea別HP設定用）</summary>
    public void SetMaxHp(int hp)
    {
        maxHp = Mathf.Max(1, hp);
        currentHp = maxHp;
    }

    private void Break(Vector3 hitPoint)
    {
        if (isBroken) return;

        isBroken = true;
        currentHp = 0;
        OnBroken?.Invoke(hitPoint);
        OnAnyBlockBroken?.Invoke(hitPoint);

        // VFX（WallHitVFX流用）
        if (breakVfxPrefab != null)
        {
            GameObject vfx = Instantiate(breakVfxPrefab, hitPoint, Quaternion.identity);
            if (vfxParent != null) vfx.transform.SetParent(vfxParent, true);
        }

        // SE（3種ランダム / 音量固定）
        AudioClip clip = PickRandomClip(breakClips);
        if (clip != null && breakAudioSource != null)
        {
            float finalBreakVolume = breakVolume * (SoundSettingsManager.Instance != null ? SoundSettingsManager.Instance.SEVolume : 1f);
            breakAudioSource.PlayOneShot(clip, finalBreakVolume);
        }

        // 見た目を消す（本体）
        if (disableRendererOnBreak && cachedRenderer != null)
        {
            cachedRenderer.enabled = false;
        }

        // 当たり判定を消す（ルート本体 + 子オブジェクトのCollider2Dを全て無効化）
        // Block_Orbitのように当たり判定が子オブジェクト(ColliderObj)にある場合も考慮する
        if (disableColliderOnBreak)
        {
            if (cachedCol != null)
                cachedCol.enabled = false;

            foreach (Collider2D col in GetComponentsInChildren<Collider2D>())
                col.enabled = false;
        }
    }

    // =========================================================
    // インターバル消滅前の点滅
    // =========================================================

    /// <summary>blinkCount回点滅してから自身をDestroyする。FortressEnemyのInterval消去で使用。</summary>
    public void StartBlinkAndDestroy(int blinkCount, float blinkInterval)
    {
        if (isBroken)
        {
            Destroy(gameObject);
            return;
        }
        StartCoroutine(BlinkThenDestroyCoroutine(blinkCount, blinkInterval));
    }

    private System.Collections.IEnumerator BlinkThenDestroyCoroutine(int blinkCount, float blinkInterval)
    {
        for (int i = 0; i < blinkCount; i++)
        {
            if (cachedRenderer != null) cachedRenderer.enabled = false;
            yield return new WaitForSeconds(blinkInterval);
            if (cachedRenderer != null) cachedRenderer.enabled = true;
            yield return new WaitForSeconds(blinkInterval);
        }
        Destroy(gameObject);
    }

    private AudioClip PickRandomClip(AudioClip[] clips)
    {
        if (clips == null || clips.Length == 0) return null;

        int valid = 0;
        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i] != null) valid++;
        }
        if (valid == 0) return null;

        int pick = Random.Range(0, valid);
        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i] == null) continue;
            if (pick == 0) return clips[i];
            pick--;
        }
        return null;
    }

    // =========================================================
    // ★追加：爆発（範囲）ダメージを外部から受け取る入口
    //  - EnemyBullet の爆発から呼ぶ
    //  - 既存の Break() ロジックを流用
    // =========================================================
    public void ApplyExplosionDamage(int damage, Vector3 hitPoint)
    {
        if (isBroken) return;

        int dmg = Mathf.Max(0, damage);
        if (dmg <= 0) return;

        currentHp -= dmg;

        if (logDebug)
        {
            Debug.Log($"[WallHealth] {name} ExplosionHit dmg={dmg} hp={currentHp}", this);
        }

        if (currentHp <= 0)
        {
            Break(hitPoint);
        }
    }
}
