using UnityEngine;

// 鳴動（HeartbeatPulse）のピークに合わせて、月の分身（残像）を一瞬出現させてフェードアウトさせる。
// スローモーション中に弾・敵に出る残像（SlowMotionManagerのAfterimage Trail）と同じ
// 「分身が現れて消える」表現を月に適用したもの。
// 固定サイズのプールのみを使い、実行中の新規Instantiate/Destroyは一切行わない。
[RequireComponent(typeof(HeartbeatPulse))]
[RequireComponent(typeof(SpriteRenderer))]
public class MoonAfterimage : MonoBehaviour
{
    [Tooltip("分身が出現するトリガーとなるパルス強度のしきい値（0-1）。パルスがこの値を上回った瞬間に1体出現する")]
    [SerializeField, Range(0f, 1f)] private float triggerThreshold = 0.8f;

    [Tooltip("分身プールの固定数")]
    [SerializeField] private int poolSize = 3;

    [Tooltip("分身が消えるまでの時間（秒）")]
    [SerializeField] private float fadeDuration = 0.6f;

    [Tooltip("分身の出現時の不透明度")]
    [SerializeField, Range(0f, 1f)] private float startAlpha = 0.5f;

    private struct GhostSlot
    {
        public SpriteRenderer sr;
        public float remaining;
    }

    private HeartbeatPulse pulseSource;
    private Area09MoonController visibility;
    private GhostSlot[] pool;
    private int nextSlot;
    private float previousPulse;

    private void Awake()
    {
        pulseSource = GetComponent<HeartbeatPulse>();
        visibility = GetComponent<Area09MoonController>();
        SpriteRenderer source = GetComponent<SpriteRenderer>();

        pool = new GhostSlot[poolSize];
        for (int i = 0; i < poolSize; i++)
        {
            // ★月本体（transform）の子にはしない。月自体が鳴動でスケールし続けるため、
            //   子にすると既に出した残像まで一緒に伸縮してしまう。残像は出現した瞬間の
            //   見た目のまま止まっていてほしいので、月と同じ親（無ければルート）に置く
            GameObject go = new GameObject($"MoonAfterimage_{i}");
            go.transform.SetParent(transform.parent, true);

            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = source.sprite;
            sr.sharedMaterial = source.sharedMaterial;
            sr.sortingLayerID = source.sortingLayerID;
            sr.sortingOrder = source.sortingOrder - 1;
            sr.enabled = false;
            pool[i] = new GhostSlot { sr = sr, remaining = 0f };
        }
    }

    private void Update()
    {
        float pulse = pulseSource.CurrentPulse;
        float visAlpha = visibility != null ? visibility.VisibilityAlpha : 1f;

        // パルスがしきい値を上向きに超えた瞬間（鳴動のピーク）だけ1体出現させる
        if (previousPulse < triggerThreshold && pulse >= triggerThreshold && visAlpha > 0.99f)
        {
            SpawnGhost();
        }
        previousPulse = pulse;

        UpdateFades(visAlpha);
    }

    private void SpawnGhost()
    {
        int slot = nextSlot;
        nextSlot = (nextSlot + 1) % pool.Length;

        Transform ghost = pool[slot].sr.transform;
        ghost.SetPositionAndRotation(transform.position, transform.rotation);
        ghost.localScale = transform.lossyScale;

        pool[slot].sr.color = new Color(1f, 1f, 1f, startAlpha);
        pool[slot].sr.enabled = true;
        pool[slot].remaining = fadeDuration;
    }

    private void UpdateFades(float visAlpha)
    {
        for (int i = 0; i < pool.Length; i++)
        {
            if (pool[i].remaining <= 0f) continue;

            pool[i].remaining -= Time.deltaTime;
            if (pool[i].remaining <= 0f)
            {
                pool[i].remaining = 0f;
                pool[i].sr.enabled = false;
                continue;
            }

            float t = pool[i].remaining / fadeDuration;
            Color c = pool[i].sr.color;
            c.a = startAlpha * t * visAlpha;
            pool[i].sr.color = c;
        }
    }
}
