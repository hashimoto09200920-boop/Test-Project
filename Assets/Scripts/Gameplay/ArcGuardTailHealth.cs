using UnityEngine;

// 尾専用の独立HPコンポーネント。WallHealth（被弾ダメージ判定・Break演出）と
// EnemyShield（破壊後にfullRecoveryTimerで自動全回復）のハイブリッド。
// 尾は本体とは別GameObject（別SpriteRenderer/Collider2D）に取り付ける想定。
//
// 注意: このコンポーネントは尾のスプライト内容（sprite/offset/回転）には一切触れない。
// スプライト内容の所有者は ArcGuardTailAnimator。このコンポーネントは
// spriteRenderer.enabled のON/OFFとColliderの有効/無効だけを切り替える
// （WallHealthのdisableRendererOnBreak/disableColliderOnBreakと同じ役割分担）。
// ArcGuardControllerがOnTailBroken/OnTailRestoredを購読し、
// ArcGuardTailAnimator側のFrozen（破壊）ポーズ表示に反映する想定。
[DisallowMultipleComponent]
public class ArcGuardTailHealth : MonoBehaviour
{
    // ======================================================
    // Inspector fields
    // ======================================================

    [Header("References")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Collider2D     hitCollider;
    [Tooltip("ArcGuardTailAnimatorのTail Collider Segmentsと同じ3個を割り当てる。破壊/復活時にhitColliderと一緒に有効/無効を切り替える")]
    [SerializeField] private Collider2D[]   additionalHitColliders;

    [Header("HP")]
    [SerializeField] private int maxHp = 10;

    // 通常反射弾・Just反射弾は SkillManager が管理する BlockNormalDamage/BlockJustDamage を使用する（WallHealthと同じ）
    [Header("Damage by Bullet State")]
    [Tooltip("未反射弾（白/赤で一度も反射していない）")]
    [SerializeField] private int damageUnreflected = 0;

    [Header("Recovery（仮値・後で数値調整）")]
    [Tooltip("破壊されてから自動全回復するまでの秒数")]
    [SerializeField] private float fullRecoveryTime = 5f;

    [Header("Break Visual / Collision")]
    [SerializeField] private bool disableRendererOnBreak = true;
    [SerializeField] private bool disableColliderOnBreak = true;

    [Header("Break VFX / SFX")]
    [SerializeField] private GameObject  breakVfxPrefab;
    [Tooltip("未指定ならシーン内の ProjectileRoot を自動検索して親にする")]
    [SerializeField] private Transform   vfxParent;
    [SerializeField] private AudioClip[] breakClips = new AudioClip[3];
    [Range(0f, 1f)]
    [SerializeField] private float       breakVolume = 1f;
    [Tooltip("未指定なら自動でAudioSourceを追加して鳴らす")]
    [SerializeField] private AudioSource breakAudioSource;

    [Header("Hit VFX / SFX（破壊されない場合）")]
    [SerializeField] private GameObject  hitVfxPrefab;
    [SerializeField] private float       hitVfxDestroySeconds = 0.35f;
    [Tooltip("通常反射弾ヒット時のSE（3本推奨、ランダム再生）")]
    [SerializeField] private AudioClip[] hitClipsNormal = new AudioClip[3];
    [Tooltip("Just反射弾ヒット時のSE（3本推奨、ランダム再生）")]
    [SerializeField] private AudioClip[] hitClipsJust = new AudioClip[3];
    [Range(0f, 1f)]
    [SerializeField] private float       hitVolume = 1f;

    [Header("Debug")]
    [SerializeField] private bool logDebug = false;

    // ======================================================
    // Events / Public state
    // ======================================================

    /// <summary>尾のBlock HPが0になった時に発火。ArcGuardControllerはこれを見て尾攻撃・尾Idle表示を止める。</summary>
    public event System.Action OnTailBroken;
    /// <summary>尾が自動全回復した時に発火。</summary>
    public event System.Action OnTailRestored;

    public bool IsBroken   => isBroken;
    public int  CurrentHp  => currentHp;
    public int  MaxHp      => maxHp;

    private enum BulletState { Unreflected, NormalReflected, JustReflected }

    private int   currentHp;
    private bool  isBroken;
    private float recoveryTimer;

    // 同フレーム多重ヒット抑止
    private int lastHitFrame = -999;
    private int lastBulletId;

    // ======================================================
    // Lifecycle
    // ======================================================

    private void Awake()
    {
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        if (hitCollider == null)    hitCollider    = GetComponent<Collider2D>();

        currentHp = Mathf.Max(1, maxHp);
        isBroken  = false;

        if (breakAudioSource == null) breakAudioSource = GetComponent<AudioSource>();
        if (breakAudioSource == null)
        {
            breakAudioSource = gameObject.AddComponent<AudioSource>();
            breakAudioSource.playOnAwake = false;
            breakAudioSource.loop = false;
            breakAudioSource.spatialBlend = 0f;
        }

        if (vfxParent == null)
        {
            GameObject pr = GameObject.Find("ProjectileRoot");
            if (pr != null) vfxParent = pr.transform;
        }
    }

    private void Update()
    {
        if (!isBroken) return;

        recoveryTimer += Time.deltaTime;
        if (recoveryTimer >= fullRecoveryTime)
        {
            Restore();
        }
    }

    /// <summary>Instantiate後にHPを上書きする（EnemyData等からのArea別HP設定用）</summary>
    public void SetMaxHp(int hp)
    {
        maxHp = Mathf.Max(1, hp);
        currentHp = maxHp;
    }

    // ======================================================
    // Hit detection
    // ======================================================

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isBroken || other == null) return;
        HandleHit(other, other.transform.position);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (isBroken) return;
        if (collision == null || collision.collider == null) return;
        HandleHit(collision.collider, collision.GetContact(0).point);
    }

    private void HandleHit(Collider2D other, Vector3 hitPoint)
    {
        if (isBroken || other == null) return;

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
            Debug.Log($"[ArcGuardTailHealth] {name} Hit / state={state} dmg={dmg} hp={currentHp}", this);

        if (dmg <= 0) return;

        currentHp -= dmg;

        if (currentHp <= 0)
            Break(hitPoint);
        else
            PlayHit(hitPoint, state == BulletState.JustReflected);
    }

    private BulletState EvaluateBulletState(EnemyBullet bullet)
    {
        if (bullet.DamageMultiplier > 1.0001f) return BulletState.JustReflected;
        if (bullet.HasPaddleReflectedOnce) return BulletState.NormalReflected;
        return BulletState.Unreflected;
    }

    private int GetDamage(BulletState state, EnemyBullet bullet)
    {
        switch (state)
        {
            case BulletState.Unreflected:     return damageUnreflected;
            case BulletState.NormalReflected: return Mathf.RoundToInt(bullet.BlockNormalDamage);
            case BulletState.JustReflected:   return Mathf.RoundToInt(bullet.BlockJustDamage);
            default: return 0;
        }
    }

    // ======================================================
    // Break / Restore
    // ======================================================

    private void Break(Vector3 hitPoint)
    {
        if (isBroken) return;

        isBroken = true;
        currentHp = 0;
        recoveryTimer = 0f;

        if (disableRendererOnBreak && spriteRenderer != null)
            spriteRenderer.enabled = false;
        if (disableColliderOnBreak)
        {
            if (hitCollider != null) hitCollider.enabled = false;
            if (additionalHitColliders != null)
                foreach (var c in additionalHitColliders)
                    if (c != null) c.enabled = false;
        }

        if (breakVfxPrefab != null)
        {
            GameObject vfx = Instantiate(breakVfxPrefab, hitPoint, Quaternion.identity);
            if (vfxParent != null) vfx.transform.SetParent(vfxParent, true);
        }

        AudioClip clip = PickRandomClip(breakClips);
        if (clip != null && breakAudioSource != null)
        {
            float vol = breakVolume * (SoundSettingsManager.Instance != null ? SoundSettingsManager.Instance.SEVolume : 1f);
            breakAudioSource.PlayOneShot(clip, vol);
        }

        OnTailBroken?.Invoke();
    }

    private void Restore()
    {
        isBroken = false;
        currentHp = maxHp;
        recoveryTimer = 0f;

        if (spriteRenderer != null) spriteRenderer.enabled = true;
        if (hitCollider != null)    hitCollider.enabled = true;
        // 各セグメントの本来の有効/無効は次のApplyFrameで即座に上書きされるため、ここでは一律ONに戻すだけでよい
        if (additionalHitColliders != null)
            foreach (var c in additionalHitColliders)
                if (c != null) c.enabled = true;

        OnTailRestored?.Invoke();
    }

    private void PlayHit(Vector3 hitPoint, bool isJust)
    {
        if (hitVfxPrefab != null)
        {
            GameObject vfx = Instantiate(hitVfxPrefab, hitPoint, Quaternion.identity);
            if (vfxParent != null) vfx.transform.SetParent(vfxParent, true);
            if (hitVfxDestroySeconds > 0f) Destroy(vfx, hitVfxDestroySeconds);
        }

        AudioClip clip = PickRandomClip(isJust ? hitClipsJust : hitClipsNormal);
        if (clip != null && breakAudioSource != null)
        {
            float vol = hitVolume * (SoundSettingsManager.Instance != null ? SoundSettingsManager.Instance.SEVolume : 1f);
            breakAudioSource.PlayOneShot(clip, vol);
        }
    }

    private AudioClip PickRandomClip(AudioClip[] clips)
    {
        if (clips == null || clips.Length == 0) return null;

        int valid = 0;
        for (int i = 0; i < clips.Length; i++) if (clips[i] != null) valid++;
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
}
