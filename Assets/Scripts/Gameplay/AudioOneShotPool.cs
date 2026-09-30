using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 弾のヒット/消滅/発射時などに単発SEを鳴らすための、AudioSource付きGameObjectの使い回しプール。
/// 以前はSEを鳴らすたびに new GameObject() + AddComponent&lt;AudioSource&gt;() を行い、
/// 再生終了後にDestroyしていた。弾・エフェクトと同じ理由でInstantiate/Destroyの発生源に
/// なっていたため、EnemyBulletPool/HitVfxPoolと同じ設計でプール化する。
/// SE専用のため、プレハブ単位ではなく単一のプールで全種類のクリップを使い回す。
/// </summary>
public static class AudioOneShotPool
{
    private static readonly Queue<AudioSource> pool = new Queue<AudioSource>(8);

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
        pool.Clear();
        runner = null;
    }

    private static AudioSource Rent()
    {
        while (pool.Count > 0)
        {
            AudioSource a = pool.Dequeue();
            if (a != null) return a;
        }

        GameObject go = new GameObject("AudioOneShotPlayer");
        AudioSource created = go.AddComponent<AudioSource>();
        created.playOnAwake = false;
        created.loop = false;
        created.spatialBlend = 0f;
        return created;
    }

    /// <summary>指定位置でクリップを1回再生する（PlayOneShot相当）。extraSecondsは再生後の余韻秒数。</summary>
    public static void Play(AudioClip clip, float volume, Vector3 position, Transform parent, float extraSeconds = 0f)
    {
        if (clip == null) return;

        AudioSource a = Rent();
        a.transform.SetParent(parent, false);
        a.transform.position = position;
        a.gameObject.SetActive(true);
        a.PlayOneShot(clip, volume);

        float life = Mathf.Max(0.01f, clip.length + Mathf.Max(0f, extraSeconds));
        ReturnLater(a, life);
    }

    /// <summary>再生開始位置をstartOffsetSecondsだけずらして再生する（クリップ先頭の無音区間スキップ用）。</summary>
    public static void PlayWithOffset(AudioClip clip, float volume, float startOffsetSeconds, Vector3 position, Transform parent, float extraSeconds = 0f)
    {
        if (clip == null) return;

        AudioSource a = Rent();
        a.transform.SetParent(parent, false);
        a.transform.position = position;
        a.gameObject.SetActive(true);

        float offset = Mathf.Clamp(startOffsetSeconds, 0f, Mathf.Max(0f, clip.length - 0.01f));
        if (offset > 0f)
        {
            a.clip = clip;
            a.volume = volume;
            a.time = offset;
            a.Play();
        }
        else
        {
            a.PlayOneShot(clip, volume);
        }

        float life = Mathf.Max(0.01f, clip.length - offset + Mathf.Max(0f, extraSeconds));
        ReturnLater(a, life);
    }

    private static void ReturnLater(AudioSource a, float seconds)
    {
        EnsureRunner(a.transform.parent);
        runner.StartCoroutine(ReturnCo(a, seconds));
    }

    private static IEnumerator ReturnCo(AudioSource a, float seconds)
    {
        yield return new WaitForSeconds(seconds);

        if (a == null) yield break;

        a.Stop();
        a.clip = null;
        a.gameObject.SetActive(false);
        a.transform.SetParent(runner.transform, false);

        pool.Enqueue(a);
    }

    private static void EnsureRunner(Transform preferredParent)
    {
        EnsureSceneHook();
        if (runner != null) return;

        GameObject go = new GameObject("AudioOneShotPoolRunner");
        Transform runnerParent = preferredParent != null ? preferredParent.parent : null;
        if (runnerParent != null) go.transform.SetParent(runnerParent, false);
        runner = go.AddComponent<PoolRunner>();
    }

    private class PoolRunner : MonoBehaviour { }
}
