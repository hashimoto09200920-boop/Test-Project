using System.Collections;
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

        runner.StartCoroutine(ReturnCo(prefab, instance, seconds));
    }

    private static IEnumerator ReturnCo(GameObject prefab, GameObject instance, float seconds)
    {
        if (seconds > 0f) yield return new WaitForSeconds(seconds);

        if (instance == null) yield break;

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

    private class PoolRunner : MonoBehaviour { }
}
