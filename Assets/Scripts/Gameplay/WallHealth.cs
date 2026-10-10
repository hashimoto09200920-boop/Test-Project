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

    [Tooltip("破壊VFXを破棄する秒数（プレハブごとの実際の再生時間に余裕を持たせた値にする）")]
    [SerializeField] private float breakVfxDestroySeconds = 1.8f;

    [Tooltip("未指定ならシーン内の ProjectileRoot を自動検索して親にする")]
    [SerializeField] private Transform vfxParent;

    public enum BreakFxStyle { Block, Machine, Flame, Legacy }

    [Header("Break FX（新しい破壊演出 BreakFXManager。メニュー「Tools/弾の見た目/8」で種類と色を設定）")]
    [Tooltip("Block＝本物のブロック（破片・砂ぼこり）／Machine＝機械・エネルギー系（ショートして爆発）／Flame＝霊火（火の粉と煙）／Legacy＝従来の Break Vfx Prefab")]
    [SerializeField] private BreakFxStyle breakFxStyle = BreakFxStyle.Block;
    [Tooltip("Machine・Flameの色（アルファ0なら管理役の既定色）")]
    [SerializeField] private Color breakFxColor = new Color(0f, 0f, 0f, 0f);
    private int lastHitFxState; // 壊した弾：0＝不明 1＝ノーマル 2＝ジャスト

    [Header("Hit VFX (弾ヒット時・破壊されない場合)")]
    [Tooltip("弾がヒットしたが破壊されなかった時のVFX Prefab。未設定なら出ない。")]
    [SerializeField] private GameObject hitVfxPrefab;

    [Tooltip("ヒットVFXを破棄する秒数")]
    [SerializeField] private float hitVfxDestroySeconds = 0.35f;

    [Header("Hit SFX (弾ヒット時・破壊されない場合)")]
    [Tooltip("通常反射弾ヒットSE（3種ランダム）。未設定なら鳴らない。")]
    [SerializeField] private AudioClip[] hitClips = new AudioClip[3];

    [Tooltip("Just（強化）反射弾ヒットSE（3種ランダム）。未設定ならHit Clipsを代わりに使う。")]
    [SerializeField] private AudioClip[] justHitClips = new AudioClip[3];

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
    public static event System.Action<Vector3, bool> OnAnyBlockBroken;

    /// <summary>このブロックを破壊したときアイテムをドロップするか。StageBlockSpawnerがtrueに設定する。</summary>
    public bool dropItems = false;

    /// <summary>
    /// Result画面の「ブロック破壊数」にカウントするか。
    /// ★WallHealthはFortress/ArcGuard尻尾/Marshal・Dragon/Obelisk/Zephyr/IronNest/Golem/Bit等、
    ///   エネミー側の破壊可能パーツにも共用されているため、デフォルトはfalse。
    ///   本来の「ブロック」ギミック（StageBlockSpawner生成分）だけtrueに設定する。
    /// </summary>
    public bool countsAsScoreBlock = false;

    private int currentHp;
    private bool isBroken;

    /// <summary>Break()済みか（Break()はGameObjectをDestroyしない。Collider/Rendererを無効化するだけ）</summary>
    public bool IsBroken => isBroken;

    // 同フレーム多重ヒット抑止（Stay/複数接触の連打対策）
    private int lastHitFrame = -999;
    private int lastBulletId = 0;

    // ドリル弾（PinnedReflectBullet）が留まっている間の留まり先の目印（壊れた瞬間に破棄して、ドリル弾に直進を再開させる）
    private WallHealthPinTarget pinTarget;

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
        HandleHit(collision.collider, p, collision.GetContact(0).normal);
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
        HandleHit(collision.collider, p, collision.GetContact(0).normal);
    }

    public void OnChildTriggerEnter2D(Collider2D other)
    {
        if (isBroken) return;
        if (other == null) return;

        Vector3 p = other.transform.position;
        HandleHit(other, p);
    }

    private void HandleHit(Collider2D other, Vector3 hitPoint, Vector2? contactNormal = null)
    {
        if (isBroken) return;
        if (other == null) return;

        EnemyBullet bullet = other.GetComponent<EnemyBullet>();
        if (bullet == null) bullet = other.GetComponentInParent<EnemyBullet>(true);
        if (bullet == null) return;

        int bulletId = bullet.GetInstanceID();
        if (Time.frameCount == lastHitFrame && bulletId == lastBulletId) return;
        lastHitFrame = Time.frameCount;
        lastBulletId = bulletId;

        BulletState state = EvaluateBulletState(bullet);

        // ★ドリル弾（PinnedReflectBullet。現在はTsukuyomi・NeonDancerのドリルのみ）を反射した弾は、
        //   線・敵・プレイヤー・Floorと同じく、ブロックにも留まって規定回数ヒットする（1回ごとのダメージは通常の1回分と同じ）。
        //   規定回数を与えきったら弾は消える。途中でブロックが壊れたら、留まるのをやめて元の向きへ直進を再開する。
        //   ドリル弾以外の弾には一切影響しない
        PinnedReflectBullet pinned = bullet.CachedPinnedReflect;
        if (pinned != null && state != BulletState.Unreflected)
        {
            if (pinTarget == null) pinTarget = gameObject.AddComponent<WallHealthPinTarget>();
            Vector2 normal;
            if (contactNormal.HasValue && contactNormal.Value.sqrMagnitude > 0.0001f) normal = contactNormal.Value;
            else
            {
                Rigidbody2D brb = bullet.GetComponent<Rigidbody2D>();
                normal = (brb != null && brb.linearVelocity.sqrMagnitude > 0.0001f) ? brb.linearVelocity.normalized : (Vector2)bullet.transform.right;
            }
            EnemyBullet pinnedBullet = bullet;
            if (pinned.TryPinToEnemy(pinTarget, (d, mul, pos) => ApplyPinnedHit(pinnedBullet, pos), bullet, normal, hitPoint, bullet.DamageValue, bullet.DamageMultiplier))
            {
                return;
            }
        }

        int dmg = GetDamage(state, bullet);
        lastHitFxState = FxState(state);

        if (logDebug)
        {
            Debug.Log($"[WallHealth] {name} Hit / state={state} dmg={dmg} hp={currentHp}", this);
        }

        if (dmg <= 0) return;

        SessionStats.AddBlockDamage(dmg);
        currentHp -= dmg;

        if (currentHp <= 0)
        {
            Break(hitPoint);
        }
        else
        {
            PlayHit(hitPoint, state, bullet);
        }
    }

    // ドリル弾が留まっている間の1ヒット分（通常の1回分と同じダメージ・SE/VFX・破壊判定）
    private void ApplyPinnedHit(EnemyBullet bullet, Vector3 hitPoint)
    {
        if (isBroken || bullet == null) return;
        BulletState state = EvaluateBulletState(bullet);
        int dmg = GetDamage(state, bullet);
        if (dmg <= 0) return;
        lastHitFxState = FxState(state);

        SessionStats.AddBlockDamage(dmg);
        currentHp -= dmg;
        if (currentHp <= 0) Break(hitPoint);
        else PlayHit(hitPoint, state, bullet);
    }

    private static int FxState(BulletState s) => s == BulletState.JustReflected ? 2 : s == BulletState.NormalReflected ? 1 : 0;

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

    private void PlayHit(Vector3 hitPoint, BulletState state, EnemyBullet bullet = null)
    {
        // 反射弾（ドリル以外）のヒットは、線で反射した時と同じ演出（ReflectedBulletFXManager）を出し、旧VFX（Hit Vfx Prefab）は出さない
        bool newFx = bullet != null && state != BulletState.Unreflected && bullet.CachedPinnedReflect == null && ReflectedBulletFXManager.HandlesBlockHit;
        if (newFx) ReflectedBulletFXManager.NotifyBlockHit(bullet, hitPoint);

        // VFX
        if (!newFx && hitVfxPrefab != null)
        {
            GameObject vfx = HitVfxPool.Rent(hitVfxPrefab, vfxParent, hitPoint);
            vfx.transform.SetPositionAndRotation(hitPoint, Quaternion.identity);
            if (vfxParent != null) vfx.transform.SetParent(vfxParent, true);
            vfx.SetActive(true);
            HitVfxPool.ReturnLater(hitVfxPrefab, vfx, hitVfxDestroySeconds);
        }

        // SE（Just反射弾はJust Hit Clips優先。未設定ならHit Clipsにフォールバック）
        AudioClip clip = (state == BulletState.JustReflected) ? PickRandomClip(justHitClips) : null;
        if (clip == null) clip = PickRandomClip(hitClips);

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

    // =========================================================
    // ★追加：破壊済みの状態からHP・Collider・Rendererを復元する
    //  - Break()はGameObjectをDestroyしないため、リスポーンするBlock系ユニット
    //    （ObeliskのBit等）が同じインスタンスのまま再有効化するために使う
    // =========================================================
    public void ResetHealth()
    {
        isBroken = false;
        currentHp = Mathf.Max(0, maxHp);

        if (cachedRenderer != null) cachedRenderer.enabled = true;

        if (cachedCol != null) cachedCol.enabled = true;
        foreach (Collider2D col in GetComponentsInChildren<Collider2D>())
            col.enabled = true;

        if (logDebug)
        {
            Debug.Log($"[WallHealth] {name} ResetHealth / hp={currentHp}", this);
        }
    }

    private void Break(Vector3 hitPoint)
    {
        if (isBroken) return;

        isBroken = true;
        currentHp = 0;
        // 留まっているドリル弾に「留まり先が無くなった」ことを伝える（次のフレームで直進を再開する）
        if (pinTarget != null) { Destroy(pinTarget); pinTarget = null; }
        if (countsAsScoreBlock) SessionStats.AddBlockDestroy();
        OnBroken?.Invoke(hitPoint);
        OnAnyBlockBroken?.Invoke(hitPoint, dropItems);

        // 新しい破壊演出（BreakFXManager）が出せた時は旧VFX（Break Vfx Prefab）を出さない
        bool newFx = breakFxStyle != BreakFxStyle.Legacy &&
                     BreakFXManager.TryPlayWallBreak((int)breakFxStyle, cachedRenderer, transform, hitPoint, lastHitFxState, breakFxColor);
        lastHitFxState = 0;

        // VFX（WallHitVFX流用）
        if (!newFx && breakVfxPrefab != null)
        {
            GameObject vfx = HitVfxPool.Rent(breakVfxPrefab, vfxParent, hitPoint);
            vfx.transform.SetPositionAndRotation(hitPoint, Quaternion.identity);
            if (vfxParent != null) vfx.transform.SetParent(vfxParent, true);
            vfx.SetActive(true);
            HitVfxPool.ReturnLater(breakVfxPrefab, vfx, breakVfxDestroySeconds);
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

    /// <summary>アルファフェードで自身をDestroyする（Area10ボスラッシュのボス切替専用。既存のStartBlinkAndDestroyとは別の消去手段）。</summary>
    public void StartFadeOutAndDestroy(float duration)
    {
        if (isBroken)
        {
            Destroy(gameObject);
            return;
        }
        StartCoroutine(FadeOutThenDestroyCoroutine(duration));
    }

    private System.Collections.IEnumerator FadeOutThenDestroyCoroutine(float duration)
    {
        if (cachedRenderer != null && duration > 0f)
        {
            Color startColor = cachedRenderer.color;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float alpha = Mathf.Lerp(startColor.a, 0f, elapsed / duration);
                cachedRenderer.color = new Color(startColor.r, startColor.g, startColor.b, alpha);
                yield return null;
            }
        }
        Destroy(gameObject);
    }

    // =========================================================
    // 撃破時の崩落演出（FortressEnemy等、ボス本体撃破時にまとめて使用）
    // =========================================================

    [Header("Collapse（ボス撃破時の崩落演出）")]
    [Tooltip("崩れ始めるまでのランダムな遅延の最大値（秒）。ブロックごとにバラけさせてカスケード状に崩れさせる")]
    [SerializeField] private float collapseMaxStartDelay = 0.3f;
    [Tooltip("崩れ始めてから消えるまでの時間（秒）")]
    [SerializeField] private float collapseFallDuration = 1.2f;
    [Tooltip("落下の重力加速度（Unity単位/秒²）")]
    [SerializeField] private float collapseGravity = 18f;
    [Tooltip("回転速度の範囲（度/秒）。ブロックごとにこの範囲内でランダム、向きも左右ランダム")]
    [SerializeField] private float collapseRotationSpeedMin = 90f;
    [SerializeField] private float collapseRotationSpeedMax = 360f;
    [Tooltip("消える直前にフェードアウトする時間（秒）")]
    [SerializeField] private float collapseFadeOutDuration = 0.25f;

    private bool isCollapsing;

    /// <summary>
    /// 即Destroyせず、ランダムな遅延→重力落下＋回転→フェードアウトしてから消える「建物崩壊」演出。
    /// ボス本体の撃破時、生成済みブロック群をまとめて処理する想定（通常の点滅/フェード消去とは別ルート）。
    /// </summary>
    public void CollapseAndDestroy()
    {
        if (isCollapsing) return;
        isCollapsing = true;
        StopAllCoroutines();
        StartCoroutine(CollapseCoroutine());
    }

    private System.Collections.IEnumerator CollapseCoroutine()
    {
        float delay = Random.Range(0f, collapseMaxStartDelay);
        if (delay > 0f) yield return new WaitForSeconds(delay);

        float rotSpeed = Random.Range(collapseRotationSpeedMin, collapseRotationSpeedMax) * (Random.value < 0.5f ? -1f : 1f);
        float fallSpeed = 0f;
        float elapsed = 0f;
        Color startColor = cachedRenderer != null ? cachedRenderer.color : Color.white;

        while (elapsed < collapseFallDuration)
        {
            elapsed += Time.deltaTime;
            fallSpeed += collapseGravity * Time.deltaTime;
            transform.position += Vector3.down * fallSpeed * Time.deltaTime;
            transform.Rotate(0f, 0f, rotSpeed * Time.deltaTime);

            if (cachedRenderer != null && elapsed > collapseFallDuration - collapseFadeOutDuration)
            {
                float fadeT = (elapsed - (collapseFallDuration - collapseFadeOutDuration)) / collapseFadeOutDuration;
                float alpha = Mathf.Lerp(startColor.a, 0f, Mathf.Clamp01(fadeT));
                cachedRenderer.color = new Color(startColor.r, startColor.g, startColor.b, alpha);
            }
            yield return null;
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
    // ★追加：Beam（EnemyBulletを介さないダメージ源）からブロックダメージを受け取る入口
    //  - 未反射区間=damageUnreflected、反射後区間=SkillManagerのBlockNormal/JustDamageを使用
    //  - 既存のHandleHit/GetDamageと同じ判定基準（EvaluateBulletStateのState別ダメージ）を踏襲
    // =========================================================
    public void ApplyBeamDamage(bool isUnreflected, bool isJust, Vector3 hitPoint)
    {
        if (isBroken) return;

        int dmg;
        BulletState state;
        if (isUnreflected)
        {
            state = BulletState.Unreflected;
            dmg = damageUnreflected;
        }
        else
        {
            float normalDmg = 1f, justDmg = 2f;
            if (Game.Skills.SkillManager.Instance != null)
            {
                Game.Skills.SkillManager.Instance.GetBlockDamage(out normalDmg, out justDmg);
            }
            state = isJust ? BulletState.JustReflected : BulletState.NormalReflected;
            dmg = Mathf.RoundToInt(isJust ? justDmg : normalDmg);
        }

        lastHitFxState = FxState(state);
        if (logDebug)
        {
            Debug.Log($"[WallHealth] {name} BeamHit / state={state} dmg={dmg} hp={currentHp}", this);
        }

        if (dmg <= 0) return;

        SessionStats.AddBlockDamage(dmg);
        currentHp -= dmg;

        if (currentHp <= 0)
        {
            Break(hitPoint);
        }
        else
        {
            PlayHit(hitPoint, state);
        }
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
