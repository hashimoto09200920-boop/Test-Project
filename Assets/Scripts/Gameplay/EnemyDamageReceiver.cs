using UnityEngine;

public class EnemyDamageReceiver : MonoBehaviour
{
    [SerializeField] private int damagePerHit = 1;
    [SerializeField] private bool destroyBulletOnHit = true;

    [Header("Debug (読み取り専用)")]
    [Tooltip("デバッグログを表示する（WeakPoint System の動作確認用）")]
    [SerializeField] private bool showDebugLog = false;

    private EnemyStats stats;
    private EnemyHitFeedback feedback;
    private EnemyData enemyData;

    // =========================
    // Enemy Hit SE (Normal / Just / Not Reflected)
    // =========================
    [Header("Enemy Hit SE")]
    [Tooltip("通常ヒットSE（3本推奨）。空要素は無視される。")]
    [SerializeField] private AudioClip[] normalHitClips = new AudioClip[3];

    [Tooltip("Just（強化）ヒットSE（3本推奨）。空要素は無視される。")]
    [SerializeField] private AudioClip[] justHitClips = new AudioClip[3];

    [Header("Enemy Hit SE (Not Reflected Bullet)")]
    [Tooltip("未反射弾（IsReflected=false）が敵に当たった時のSE（1本）。未設定なら鳴らない。")]
    [SerializeField] private AudioClip notReflectedHitClip;

    [Tooltip("未反射弾ヒットSE音量（固定）")]
    [Range(0f, 1f)]
    [SerializeField] private float notReflectedHitVolume = 1f;

    [Tooltip("敵ヒットSE用AudioSource（未設定なら自動で取得/追加する）")]
    [SerializeField] private AudioSource enemyHitSeSource;

    [Tooltip("反射弾ヒットSE音量（固定）")]
    [Range(0f, 1f)]
    [SerializeField] private float enemyHitSeVolume = 1f;

    [Tooltip("敵ヒットSEの最短間隔（秒）。めり込み等の多重ヒットで連打にならないよう抑制する。")]
    [SerializeField] private float enemyHitSeMinIntervalSeconds = 0.06f;

    private float lastEnemyHitSeTime = -999f;

    private void Awake()
    {
        stats = GetComponent<EnemyStats>();
        if (stats == null)
        {
            stats = gameObject.AddComponent<EnemyStats>();
        }

        feedback = GetComponent<EnemyHitFeedback>(); // 無ければ演出なしで動作

        // EnemyData を取得（WeakPoint System 判定用）
        EnemyShooter shooter = GetComponent<EnemyShooter>();
        if (shooter != null)
        {
            enemyData = shooter.GetEnemyData();
        }

        // Enemy Hit SE Source
        if (enemyHitSeSource == null)
        {
            enemyHitSeSource = GetComponent<AudioSource>();
        }
        if (enemyHitSeSource == null)
        {
            enemyHitSeSource = gameObject.AddComponent<AudioSource>();
        }

        // 安全な初期値（2D）
        enemyHitSeSource.playOnAwake = false;
        enemyHitSeSource.loop = false;
        enemyHitSeSource.spatialBlend = 0f;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        Debug.Log($"[EnemyDamageReceiver] OnCollisionEnter2D called for {gameObject.name}");
        EnemyBullet bullet = collision.collider.GetComponent<EnemyBullet>();
        if (bullet == null) return;

        // ★WeakPoint System 判定用に enemyData を取得（Awake で取得できない場合があるため）
        if (enemyData == null)
        {
            EnemyShooter shooter = GetComponent<EnemyShooter>();
            if (shooter != null)
            {
                enemyData = shooter.GetEnemyData();
            }
        }

        // ★WeakPoint System が有効な場合、親オブジェクトではダメージ処理しない
        // （子オブジェクトの EnemyPart がダメージ処理を行う）
        if (enemyData != null && enemyData.useWeakPointSystem)
        {
            if (showDebugLog)
            {
                Debug.Log($"[EnemyDamageReceiver] WeakPoint System ON - Body hit ignored (no damage to parent)");
            }
            // 弾の反射・消滅処理は EnemyBullet 側で自動的に行われる
            return;
        }

        if (showDebugLog)
        {
            bool hasEnemyData = enemyData != null;
            bool useWeakPoint = hasEnemyData && enemyData.useWeakPointSystem;
            Debug.Log($"[EnemyDamageReceiver] Processing damage - HasEnemyData: {hasEnemyData}, UseWeakPoint: {useWeakPoint}");
        }

        // ★未反射弾：ノーダメージ（既存仕様）だが、SEは鳴らす
        if (!bullet.IsReflected)
        {
            TryPlayNotReflectedEnemyHitSe();
            return;
        }

        // ★追加：敵に当たった時も「跳ね返り回数」を1回消費（白/赤で跳ね返した弾のみ、判定はEnemyBullet側）
        bullet.RegisterEnemyHitAsBounce();

        // ⑤：ジャスト（強化）倍率を反映
        float mul = Mathf.Max(1f, bullet.DamageMultiplier);
        bool isPowered = mul > 1.0001f;

        int finalDamage = Mathf.Max(1, Mathf.RoundToInt(damagePerHit * mul));

        // ★デバッグログ：ダメージ計算の詳細を出力
        Debug.Log($"[EnemyDamageReceiver] damagePerHit={damagePerHit}, DamageMultiplier={bullet.DamageMultiplier}, mul={mul}, finalDamage={finalDamage}, isPowered={isPowered}, enemy={gameObject.name}");

        stats.Damage(finalDamage);

        // ★敵ヒットSE（通常/Justで切替、3種ランダム、連打防止あり）
        TryPlayReflectedEnemyHitSe(isPowered);

        // ★A/B/C：命中フィードバック（Just/強化時は強めに）
        if (feedback != null)
        {
            // 命中座標：Collisionの接触点が取れればそれを使う（自然）
            Vector3 hitPos = transform.position;
            if (collision.contactCount > 0)
            {
                hitPos = collision.GetContact(0).point;
            }
            feedback.PlayHitFeedback(finalDamage, isPowered, hitPos);
        }

        if (destroyBulletOnHit)
        {
            Destroy(bullet.gameObject);
        }
    }

    private void TryPlayNotReflectedEnemyHitSe()
    {
        if (notReflectedHitClip == null) return;

        // 連打防止（未反射にも適用：敵1体単位）
        float minInterval = Mathf.Max(0f, enemyHitSeMinIntervalSeconds);
        float now = Time.unscaledTime;
        if (minInterval > 0f)
        {
            if ((now - lastEnemyHitSeTime) < minInterval) return;
        }

        lastEnemyHitSeTime = now;

        if (enemyHitSeSource == null) return;
        enemyHitSeSource.PlayOneShot(notReflectedHitClip, notReflectedHitVolume);
    }

    private void TryPlayReflectedEnemyHitSe(bool isPowered)
    {
        float minInterval = Mathf.Max(0f, enemyHitSeMinIntervalSeconds);
        float now = Time.unscaledTime;
        if (minInterval > 0f)
        {
            if ((now - lastEnemyHitSeTime) < minInterval) return;
        }

        AudioClip clip = PickRandomClip(isPowered ? justHitClips : normalHitClips);
        if (clip == null) return;

        lastEnemyHitSeTime = now;

        if (enemyHitSeSource == null) return;
        enemyHitSeSource.PlayOneShot(clip, enemyHitSeVolume);
    }

    private static AudioClip PickRandomClip(AudioClip[] clips)
    {
        if (clips == null || clips.Length <= 0) return null;

        // nullを除外したインデックス選択（軽量）
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

    // =========================================================
    // ★追加：爆発（範囲）ダメージを外部から受け取る正式ルート
    //  - EnemyBullet の爆発から呼ぶ
    // =========================================================
    public void ApplyExplosionDamage(int damage, Vector3 hitPos)
    {
        // ★WeakPoint System 判定用に enemyData を取得（念のため）
        if (enemyData == null)
        {
            EnemyShooter shooter = GetComponent<EnemyShooter>();
            if (shooter != null)
            {
                enemyData = shooter.GetEnemyData();
            }
        }

        // WeakPoint System が有効な場合、親オブジェクトでは爆発ダメージも受けない
        if (enemyData != null && enemyData.useWeakPointSystem)
        {
            return;
        }

        int dmg = Mathf.Max(0, damage);
        if (dmg <= 0) return;

        if (stats == null)
        {
            stats = GetComponent<EnemyStats>();
            if (stats == null) stats = gameObject.AddComponent<EnemyStats>();
        }

        stats.Damage(dmg);

        // 爆発は「Just/通常」の概念と別扱い（合意済み：そのままダメージ適用）
        // 演出は"通常扱い"で出す（必要なら後で爆発専用に拡張可能）
        if (feedback != null)
        {
            feedback.PlayHitFeedback(dmg, false, hitPos);
        }
    }

    // =========================================================
    // ★EnemyPart から呼ばれる公開メソッド（親の SE を使用）
    // =========================================================
    public void TryPlayNotReflectedEnemyHitSeFromPart()
    {
        TryPlayNotReflectedEnemyHitSe();
    }

    public void TryPlayReflectedEnemyHitSeFromPart(bool isPowered)
    {
        TryPlayReflectedEnemyHitSe(isPowered);
    }
}
