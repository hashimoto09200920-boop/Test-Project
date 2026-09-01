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

    // ★s_bulletLastHandledFrameはstatic(アプリ全体で永続)かつ、弾のInstanceIDは使い回されないため、
    //   削除処理が無いとKillZoneを通過した弾の数だけエントリが際限なく増え続けるリークになっていた。
    //   同フレーム内の多重処理を防ぐという役目上、数フレームより古いエントリは二度と使われないため、
    //   一定間隔で古いエントリをまとめて間引く。
    private static int s_lastPruneFrame = -999;
    private const int PruneIntervalFrames = 600;
    private const int StaleAfterFrames = 120;
    private static readonly System.Collections.Generic.List<int> s_pruneScratch
        = new System.Collections.Generic.List<int>(64);

    private static void PruneStaleFrameGuardsIfNeeded(int currentFrame)
    {
        if (currentFrame - s_lastPruneFrame < PruneIntervalFrames) return;
        s_lastPruneFrame = currentFrame;

        s_pruneScratch.Clear();
        foreach (var kvp in s_bulletLastHandledFrame)
        {
            if (currentFrame - kvp.Value > StaleAfterFrames) s_pruneScratch.Add(kvp.Key);
        }
        for (int i = 0; i < s_pruneScratch.Count; i++)
        {
            s_bulletLastHandledFrame.Remove(s_pruneScratch[i]);
        }
    }

    private bool TryAcquireBulletFrameGuard(EnemyBullet bullet)
    {
        if (bullet == null) return false;

        int id = bullet.GetInstanceID();
        int f = Time.frameCount;

        PruneStaleFrameGuardsIfNeeded(f);

        int last;
        if (s_bulletLastHandledFrame.TryGetValue(id, out last))
        {
            if (last == f) return false;
        }

        s_bulletLastHandledFrame[id] = f;
        return true;
    }
}
