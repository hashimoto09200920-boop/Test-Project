using UnityEngine;

public class KillZone : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("ON: EnemyBullet を消す / OFF: 何もしない（テスト用）")]
    [SerializeField] private bool destroyEnemyBullets = true;

    [Header("Safety")]
    [Tooltip("同フレーム多重処理の保険（Enter/Stayが混在しても安全側に倒す）")]
    [SerializeField] private bool useFrameGuard = true;

    private int lastHandledFrame = -999;

    private void Reset()
    {
        // 付けた瞬間に Trigger 推奨状態に寄せる（Unity 6のInspector上でも確認してね）
        BoxCollider2D box = GetComponent<BoxCollider2D>();
        if (box == null) box = gameObject.AddComponent<BoxCollider2D>();
        box.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        Handle(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        // 高速弾でEnterを取りこぼすケースの保険（不要ならOFFでOK）
        Handle(other);
    }

    private void Handle(Collider2D other)
    {
        if (!destroyEnemyBullets) return;
        if (other == null) return;

        // ★最重要：EnemyBullet を親も含めて探す（構造変更に強い）
        EnemyBullet bullet = other.GetComponentInParent<EnemyBullet>();
        if (bullet == null) return;

        // =========================================================
        // ★追加：弾ごとのフレームガード（同フレームに複数弾が入っても全部消せる）
        // 既存の lastHandledFrame は残す（互換）だが、実運用はこっちが安全
        // =========================================================
        if (useFrameGuard)
        {
            if (!TryAcquireBulletFrameGuard(bullet))
            {
                return;
            }
        }

        // 互換：旧ガードも残すが、弾ごとガードが通った後に更新するだけにする
        lastHandledFrame = Time.frameCount;

        // EnemyBullet側のDestroy演出などは今の仕様に合わせて
        // ここでは確実に消すことを優先
        Destroy(bullet.gameObject);
    }

    // =========================================================
    // ★ここから末尾追加
    // =========================================================
    private static readonly System.Collections.Generic.Dictionary<int, int> s_bulletLastHandledFrame
        = new System.Collections.Generic.Dictionary<int, int>(512);

    private bool TryAcquireBulletFrameGuard(EnemyBullet bullet)
    {
        if (bullet == null) return false;

        int id = bullet.GetInstanceID();
        int f = Time.frameCount;

        int last;
        if (s_bulletLastHandledFrame.TryGetValue(id, out last))
        {
            if (last == f) return false;
        }

        s_bulletLastHandledFrame[id] = f;
        return true;
    }
}
