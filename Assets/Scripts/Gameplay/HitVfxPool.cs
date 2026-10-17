using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 弾のヒット/破壊エフェクト（VFX_EnemyHit_Normal, WallHitSpark_Orange等）共通のオブジェクトプール。
/// EnemyBulletFeedback内のDisappearVfxPoolと同じ仕組みを、複数ファイルから呼べるように汎用化したもの。
/// ★対象プレハブのParticleSystemはStop ActionをNoneにしておくこと。Destroyのままだと、
///   再生が自然に終わった時点でUnity自身がGameObjectを破棄してしまい、プールに返却する前に消えて
///   プーリングが機能しなくなる（FixVfxHitStopActionToolで変更済みのプレハブのみ対応）。
/// </summary>
public static class HitVfxPool
{
    private static readonly Dictionary<GameObject, Queue<GameObject>> pools
        = new Dictionary<GameObject, Queue<GameObject>>(8);

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

    /// <summary>プールから取り出す。無ければ新規Instantiate。再利用時の位置/回転/親/SetActiveは呼び出し側で行う。</summary>
    public static GameObject Rent(GameObject prefab, Transform parent, Vector3 worldPos)
    {
        if (prefab == null) return null;

        EnsureRunner(parent);

        if (!pools.TryGetValue(prefab, out Queue<GameObject> q) || q == null)
        {
            q = new Queue<GameObject>(8);
            pools[prefab] = q;
        }

        while (q.Count > 0)
        {
            GameObject obj = q.Dequeue();
            if (obj != null) return obj;
        }

        return Object.Instantiate(prefab, worldPos, Quaternion.identity, parent);
    }

    /// <summary>seconds秒後にSetActive(false)にしてプールへ戻す。</summary>
    public static void ReturnLater(GameObject prefab, GameObject instance, float seconds)
    {
        if (prefab == null || instance == null) return;

        EnsureRunner(instance.transform.parent);

        // ★負荷軽減：以前は返却1回ごとにコルーチン＋new WaitForSecondsを作っていた（線を描く間は点ごとのVFXで毎秒数千回）。
        //   返却予定をリストに積み、runnerのUpdateでまとめて返す。時間の数え方は従来のWaitForSecondsと同じ
        //   スケール時間（Time.time。一時停止中は進まない）。0秒以下は従来どおりその場で返す
        if (seconds <= 0f)
        {
            ReturnNow(prefab, instance);
            return;
        }
        runner.Schedule(prefab, instance, Time.time + seconds);
    }

    private static void ReturnNow(GameObject prefab, GameObject instance)
    {
        if (instance == null) return;

        instance.SetActive(false);
        // ★プール待機中に、親側の「直下の子を一括破棄」系処理（エリア切替時のクリア等）に
        //   巻き込まれないよう、常にプール専用のrunner配下へ退避させる
        instance.transform.SetParent(runner.transform, false);

        if (!pools.TryGetValue(prefab, out Queue<GameObject> q) || q == null)
        {
            q = new Queue<GameObject>(8);
            pools[prefab] = q;
        }
        q.Enqueue(instance);
    }

    private static void EnsureRunner(Transform preferredParent)
    {
        EnsureSceneHook();
        if (runner != null) return;

        GameObject go = new GameObject("HitVfxPoolRunner");
        Transform runnerParent = preferredParent != null ? preferredParent.parent : null;
        if (runnerParent != null) go.transform.SetParent(runnerParent, false);
        runner = go.AddComponent<PoolRunner>();
    }

    private class PoolRunner : MonoBehaviour
    {
        private struct Pending
        {
            public GameObject prefab;
            public GameObject instance;
            public float returnAt;
        }

        private readonly List<Pending> pending = new List<Pending>(64);

        public void Schedule(GameObject prefab, GameObject instance, float returnAt)
        {
            pending.Add(new Pending { prefab = prefab, instance = instance, returnAt = returnAt });
        }

        private void Update()
        {
            if (pending.Count == 0) return;
            float now = Time.time;
            // 返却時刻に達したものを登録順に返し、残りは順番を保ったまま前へ詰める
            int write = 0;
            int count = pending.Count;
            for (int i = 0; i < count; i++)
            {
                Pending p = pending[i];
                if (now >= p.returnAt)
                {
                    ReturnNow(p.prefab, p.instance);
                }
                else
                {
                    pending[write++] = p;
                }
            }
            // ReturnNow中に新しく積まれた分（i >= count）はそのまま後ろに残す
            if (write < count) pending.RemoveRange(write, count - write);
        }
    }
}
