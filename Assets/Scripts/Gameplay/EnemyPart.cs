using UnityEngine;

/// <summary>
/// 敵のパーツコンポーネント（Body / WeakPoint など）
/// 将来的に複数パーツ構成の敵や、弾の発射位置調整にも対応
/// </summary>
public class EnemyPart : MonoBehaviour
{
    // =========================================================
    // パーツの役割
    // =========================================================
    public enum PartRole
    {
        Body,        // 物理判定のみ（ダメージなし）
        WeakPoint,   // ダメージ判定あり
        Head,        // 将来: 頭パーツ
        Tail,        // 将来: 尻尾パーツ
        FirePoint    // 将来: 弾の発射位置
    }

    [Header("Part Settings")]
    [Tooltip("このパーツの役割")]
    public PartRole role = PartRole.Body;

    [Tooltip("ON: このパーツに弾が当たった時、ダメージを与える。OFF: ダメージなし（反射のみ）")]
    public bool enableDamage = false;

    [Tooltip("ダメージ倍率（1.0 = 通常、2.0 = 2倍ダメージ）")]
    [Range(0.1f, 10f)]
    public float damageMultiplier = 1.0f;

    [Header("Hit SE (Optional)")]
    [Tooltip("通常ヒットSE（3本推奨）。空要素は無視される。未設定なら親のEnemyDamageReceiverのSEを使用")]
    [SerializeField] private AudioClip[] normalHitClips = new AudioClip[3];

    [Tooltip("Just（強化）ヒットSE（3本推奨）。空要素は無視される。未設定なら親のSEを使用")]
    [SerializeField] private AudioClip[] justHitClips = new AudioClip[3];

    [Tooltip("未反射弾ヒットSE（1本）。未設定なら親のSEを使用")]
    [SerializeField] private AudioClip notReflectedHitClip;

    [Tooltip("ヒットSE音量")]
    [Range(0f, 1f)]
    [SerializeField] private float hitSeVolume = 1f;

    [Tooltip("ヒットSEの最短間隔（秒）")]
    [SerializeField] private float hitSeMinIntervalSeconds = 0.06f;

    // =========================================================
    // 内部参照
    // =========================================================
    private EnemyStats enemyStats;
    private EnemyHitFeedback enemyHitFeedback;
    private EnemyDamageReceiver enemyDamageReceiver;
    private AudioSource audioSource;
    private float lastHitSeTime = -999f;

    [Header("Debug (読み取り専用)")]
    [Tooltip("デバッグログを表示する（ヒット情報の確認用）")]
    [SerializeField] private bool debugShowHitInfo = true;

    private void Awake()
    {
        // 親オブジェクトからコンポーネント参照を取得
        // ★EnemyPartProxy がある場合（サブパーツ）は、そこからメインパーツの参照を取得
        EnemyPartProxy proxy = GetComponentInParent<EnemyPartProxy>();
        if (proxy != null)
        {
            // サブパーツ（体、尻尾など）の場合
            enemyStats = proxy.GetMainStats();
            enemyHitFeedback = proxy.GetMainFeedback();
            enemyDamageReceiver = proxy.GetMainDamageReceiver();
        }
        else
        {
            // メインパーツ（頭など）の場合、または単体敵の場合
            enemyStats = GetComponentInParent<EnemyStats>();
            enemyHitFeedback = GetComponentInParent<EnemyHitFeedback>();
            enemyDamageReceiver = GetComponentInParent<EnemyDamageReceiver>();
        }

        // AudioSource 取得/追加
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = 0f;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        EnemyBullet bullet = collision.collider.GetComponent<EnemyBullet>();
        if (bullet == null) return;

        // 未反射弾の処理
        if (!bullet.IsReflected)
        {
            TryPlayNotReflectedHitSe();
            if (debugShowHitInfo)
            {
                Debug.Log($"[EnemyPart] {role} hit by unreflected bullet (no damage)");
            }
            return;
        }

        // 反射弾の処理
        if (enableDamage && enemyStats != null)
        {
            // ★敵に当たった時も「跳ね返り回数」を1回消費
            bullet.RegisterEnemyHitAsBounce();

            // ダメージ倍率を計算（Just倍率 × パーツ倍率）
            float justMul = Mathf.Max(1f, bullet.DamageMultiplier);
            bool isPowered = justMul > 1.0001f;

            int baseDamage = bullet.DamageValue;
            float totalMul = justMul * damageMultiplier;
            int finalDamage = Mathf.Max(1, Mathf.RoundToInt(baseDamage * totalMul));

            // ダメージ適用
            enemyStats.Damage(finalDamage);

            // ヒットSE再生
            TryPlayReflectedHitSe(isPowered);

            // ヒットフィードバック（エフェクト・画面揺れ等）
            if (enemyHitFeedback != null)
            {
                Vector3 hitPos = transform.position;
                if (collision.contactCount > 0)
                {
                    hitPos = collision.GetContact(0).point;
                }
                enemyHitFeedback.PlayHitFeedback(finalDamage, isPowered, hitPos);
            }

            if (debugShowHitInfo)
            {
                Debug.Log($"[EnemyPart] {role} hit by reflected bullet: {finalDamage} damage (Just: {justMul}x, Part: {damageMultiplier}x)");
            }
        }
        else
        {
            // enableDamage = false の場合、反射のみ（ダメージなし）
            // 弾の反射・消滅処理は EnemyBullet 側で自動的に行われる
            if (debugShowHitInfo)
            {
                Debug.Log($"[EnemyPart] {role} hit by reflected bullet (no damage - body part)");
            }
        }

        // 弾の破壊は EnemyBullet 側で処理される（PaddleBounceLimit等）
    }

    // =========================================================
    // 爆発ダメージを受け取る（外部から呼ばれる）
    // =========================================================
    public void ApplyExplosionDamage(int damage, Vector3 hitPos)
    {
        if (!enableDamage) return; // ダメージ無効パーツは爆発も無効
        if (enemyStats == null) return;

        int finalDamage = Mathf.Max(1, Mathf.RoundToInt(damage * damageMultiplier));
        enemyStats.Damage(finalDamage);

        if (enemyHitFeedback != null)
        {
            enemyHitFeedback.PlayHitFeedback(finalDamage, false, hitPos);
        }

        if (debugShowHitInfo)
        {
            Debug.Log($"[EnemyPart] {role} explosion damage: {finalDamage} (Part: {damageMultiplier}x)");
        }
    }

    // =========================================================
    // SE再生
    // =========================================================
    private void TryPlayNotReflectedHitSe()
    {
        // パーツ固有のSEがあればそれを使用、なければ親のSEを使用
        AudioClip clip = notReflectedHitClip;
        if (clip == null && enemyDamageReceiver != null)
        {
            // 親のSEを使用（EnemyDamageReceiverから取得）
            enemyDamageReceiver.TryPlayNotReflectedEnemyHitSeFromPart();
            return;
        }

        if (clip == null) return;

        // 連打防止
        float now = Time.unscaledTime;
        if (hitSeMinIntervalSeconds > 0f && (now - lastHitSeTime) < hitSeMinIntervalSeconds)
        {
            return;
        }

        lastHitSeTime = now;

        if (audioSource != null)
        {
            audioSource.PlayOneShot(clip, hitSeVolume);
        }
    }

    private void TryPlayReflectedHitSe(bool isPowered)
    {
        // パーツ固有のSEがあればそれを使用、なければ親のSEを使用
        AudioClip[] clips = isPowered ? justHitClips : normalHitClips;
        AudioClip clip = PickRandomClip(clips);

        if (clip == null && enemyDamageReceiver != null)
        {
            // 親のSEを使用
            enemyDamageReceiver.TryPlayReflectedEnemyHitSeFromPart(isPowered);
            return;
        }

        if (clip == null) return;

        // 連打防止
        float now = Time.unscaledTime;
        if (hitSeMinIntervalSeconds > 0f && (now - lastHitSeTime) < hitSeMinIntervalSeconds)
        {
            return;
        }

        lastHitSeTime = now;

        if (audioSource != null)
        {
            audioSource.PlayOneShot(clip, hitSeVolume);
        }
    }

    private static AudioClip PickRandomClip(AudioClip[] clips)
    {
        if (clips == null || clips.Length <= 0) return null;

        int validCount = 0;
        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i] != null) validCount++;
        }
        if (validCount <= 0) return null;

        int pick = Random.Range(0, validCount);
        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i] == null) continue;
            if (pick == 0) return clips[i];
            pick--;
        }

        return null;
    }
}
