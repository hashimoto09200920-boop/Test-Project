using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// PaddleDot専用のオブジェクトプール。EnemyBulletPool/HitVfxPoolと同じ設計を踏襲する。
/// Get()で取り出したDotのリセット処理はPaddleDot.OnEnable()側の責務とし、このクラス自体は
/// 再利用の器（Instantiate/SetActive/親付け替え）だけを担当する。
/// </summary>
public static class PaddleDotPool
{
    private static readonly Dictionary<PaddleDot, Queue<PaddleDot>> pools
        = new Dictionary<PaddleDot, Queue<PaddleDot>>(4);

    private static PoolRunner runner;
    private static bool sceneHookRegistered;

    private static void EnsureSceneHook()
    {
        if (sceneHookRegistered) return;
        sceneHookRegistered = true;
        SceneManager.sceneUnloaded += _ => ClearPool();
    }

    private static void ClearPool()
    {
        pools.Clear();
        runner = null;
    }

    /// <summary>
    /// プールからDotを1個取り出す。プール内に再利用可能なインスタンスが無ければ新規Instantiateする。
    /// 取り出したDotはSetActive(true)されるため、PaddleDot.OnEnable()が自動的に発火し、
    /// 状態のリセットはそちらに委ねられる。
    /// </summary>
    public static PaddleDot Get(PaddleDot prefab, Vector3 position, Quaternion rotation, Transform parent)
    {
        if (prefab == null) return null;

        if (pools.TryGetValue(prefab, out Queue<PaddleDot> q) && q != null)
        {
            while (q.Count > 0)
            {
                PaddleDot existing = q.Dequeue();
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

        PaddleDot created = Object.Instantiate(prefab, position, rotation, parent);
        created.SetSourcePrefab(prefab);
        return created;
    }

    /// <summary>
    /// Dotをプールへ返却する（非表示化してQueueに戻すだけ。破棄はしない）。
    /// ★呼び出し側はこれを呼んだ後、そのDotのフィールド等に一切触れないこと
    ///   （別の線に再利用され、全く別のDotとして動き出す可能性があるため）。
    /// </summary>
    public static void Release(PaddleDot prefab, PaddleDot instance)
    {
        if (prefab == null || instance == null) return;

        EnsureRunner(instance.transform.parent);

        instance.gameObject.SetActive(false);
        instance.transform.SetParent(runner.transform, false);

        if (!pools.TryGetValue(prefab, out Queue<PaddleDot> q) || q == null)
        {
            q = new Queue<PaddleDot>(32);
            pools[prefab] = q;
        }
        q.Enqueue(instance);
    }

    private static void EnsureRunner(Transform preferredParent)
    {
        EnsureSceneHook();
        if (runner != null) return;

        GameObject go = new GameObject("PaddleDotPoolRunner");
        // ★preferredParentには個々のStroke.transformが渡ってくるが、Strokeは1本の線が
        //   終わるたびに頻繁にDestroyされるため、runnerをその「直下」に置いてはいけない。
        //   Stroke.transform.parent（＝paddleRoot、Strokeより一つ上の安定した階層）に
        //   置くことで、特定のStroke破棄に巻き込まれないようにする。
        Transform runnerParent = preferredParent != null ? preferredParent.parent : null;
        if (runnerParent != null) go.transform.SetParent(runnerParent, false);
        runner = go.AddComponent<PoolRunner>();
    }

    private class PoolRunner : MonoBehaviour { }
}
