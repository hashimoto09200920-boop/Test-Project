using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// EnemyBullet専用のオブジェクトプール。EnemyBulletFeedback.DisappearVfxPoolと同じ設計を踏襲する。
/// ★まだどこからも呼ばれていない（Stage 4でInstantiate/Destroy箇所を置き換える際に接続する）。
/// Get()で取り出した弾のリセット処理はEnemyBullet.OnEnable()側の責務とし、このクラス自体は
/// 再利用の器（Instantiate/SetActive/親付け替え）だけを担当する。
/// </summary>
public static class EnemyBulletPool
{
    private static readonly Dictionary<EnemyBullet, Queue<EnemyBullet>> pools
        = new Dictionary<EnemyBullet, Queue<EnemyBullet>>(4);

    // ★DisappearVfxPoolと同じく、対象がEnemyBullet.prefab/EnemyBullet_Tsukuyomi.prefabの
    //   2種類のみのため、runnerは1つで共有する（プレハブ毎に分ける必要はない）。
    private static PoolRunner runner;
    private static bool sceneHookRegistered;

    // ★poolsはstatic(アプリ全体で永続)のため、シーンをまたいで生きているとエリア(シーン)を
    //   切り替えるたびに「シーンごと破棄されて既に存在しないインスタンス」への参照がQueueに
    //   溜まり続けるメモリリークになる。DisappearVfxPoolと同じく、シーンunload時にpools自体を
    //   空にしてリークを断つ。
    private static void EnsureSceneHook()
    {
        if (sceneHookRegistered) return;
        sceneHookRegistered = true;
        SceneManager.sceneUnloaded += _ => ClearPool();
    }

    private static void ClearPool()
    {
        pools.Clear();
        // runnerが乗っているGameObjectはシーンローカルの親の子であるため、
        // シーンunload時にUnity側で既に破棄されている（参照だけ残してnullに戻す）
        runner = null;
    }

    /// <summary>
    /// プールから弾を1体取り出す。プール内に再利用可能なインスタンスが無ければ新規Instantiateする。
    /// 取り出した弾はSetActive(true)されるため、EnemyBullet/EnemyBulletFeedbackのOnEnable()が
    /// 自動的に発火し、状態のリセットはそちらに委ねられる。
    /// </summary>
    public static EnemyBullet Get(EnemyBullet prefab, Vector3 position, Quaternion rotation, Transform parent)
    {
        if (prefab == null) return null;

        Queue<EnemyBullet> q;
        if (pools.TryGetValue(prefab, out q) && q != null)
        {
            while (q.Count > 0)
            {
                EnemyBullet existing = q.Dequeue();
                if (existing != null)
                {
                    Transform t = existing.transform;
                    t.SetParent(parent, false);
                    t.SetPositionAndRotation(position, rotation);
                    existing.gameObject.SetActive(true);
                    existing.SetSourcePrefab(prefab);
                    return existing;
                }
            }
        }

        EnemyBullet created = Object.Instantiate(prefab, position, rotation, parent);
        created.SetSourcePrefab(prefab);
        return created;
    }

    /// <summary>
    /// 弾をプールへ返却する（非表示化してQueueに戻すだけ。破棄はしない）。
    /// ★呼び出し側はこれを呼んだ後、その弾のフィールド等に一切触れないこと
    ///   （他の発射元に再利用され、全く別の弾として動き出す可能性があるため）。
    /// </summary>
    public static void Release(EnemyBullet prefab, EnemyBullet instance)
    {
        if (prefab == null || instance == null) return;

        EnsureRunner(instance.transform.parent);

        instance.gameObject.SetActive(false);
        instance.transform.SetParent(runner.transform, false);

        Queue<EnemyBullet> q;
        if (!pools.TryGetValue(prefab, out q) || q == null)
        {
            q = new Queue<EnemyBullet>(16);
            pools[prefab] = q;
        }
        q.Enqueue(instance);
    }

    private static void EnsureRunner(Transform preferredParent)
    {
        EnsureSceneHook();
        if (runner != null) return;

        GameObject go = new GameObject("EnemyBulletPoolRunner");
        // ★シーンローカルの親(preferredParent)の子にするため、DontDestroyOnLoadは呼ばない
        //   （非ルートオブジェクトへのDontDestroyOnLoadはUnity上そもそも機能しない）。
        //   このrunner・プール中身は該当シーンと運命を共にし、シーンunload時にClearPool()で
        //   静的な参照側も後始末する設計にした（DisappearVfxPoolと同じ）。
        //
        // ★重要：preferredParentにはprojectileRootが渡ってくるが、runnerをその「直下」に
        //   置いてはいけない。EnemySpawner.FadeOutAllBullets()/ClearAllBullets()は
        //   projectileRoot直下の子を非再帰的に走査し、EnemyBulletコンポーネントを持たない
        //   子（＝runner自身）を「弾ではない」と判定して問答無用でDestroyしてしまうため、
        //   スキル選択のたびにプールごと中身の弾も丸ごと消えてしまう不具合があった。
        //   projectileRootの一つ上の階層に置くことで、その走査から見えないようにする。
        Transform runnerParent = preferredParent != null ? preferredParent.parent : null;
        if (runnerParent != null) go.transform.SetParent(runnerParent, false);
        runner = go.AddComponent<PoolRunner>();
    }

    private class PoolRunner : MonoBehaviour { }
}
