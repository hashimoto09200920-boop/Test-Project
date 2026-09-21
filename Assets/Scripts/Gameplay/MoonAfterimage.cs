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
    [SerializeField, Range(0f, 1f)] private float startAlpha = 0.05f;

    [Tooltip("消える直前、月本体に対して何倍まで広がるか（衝撃波のように外側へ膨らみながら消える）")]
    [SerializeField] private float maxScaleMultiplier = 1.05f;

    [Tooltip("輪郭（縁取り）の色")]
    [SerializeField] private Color outlineColor = new Color(1f, 0.95f, 0.75f, 1f);

    [Tooltip("輪郭の太さ（分身本体に対する拡大率。1.06なら6%はみ出た分が縁取りとして見える）")]
    [SerializeField] private float outlineScaleMargin = 1.06f;

    [Tooltip("輪郭の不透明度倍率（本体のstartAlphaに掛け合わせる。1なら本体と同じ透明度になる）")]
    [SerializeField] private float outlineAlphaMultiplier = 1f;

    private struct GhostSlot
    {
        public SpriteRenderer sr;
        public SpriteRenderer outlineSr; // 本体より一回り大きい単色コピー。はみ出た分だけが輪郭として見える
        public float remaining;
        public Vector3 baseScale; // 出現時点（月本体と同じ大きさ）の基準スケール
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

            GameObject outlineGo = new GameObject($"MoonAfterimageOutline_{i}");
            outlineGo.transform.SetParent(transform.parent, true);
            SpriteRenderer outlineSr = outlineGo.AddComponent<SpriteRenderer>();
            outlineSr.sprite = source.sprite;
            outlineSr.sharedMaterial = source.sharedMaterial;
            outlineSr.sortingLayerID = source.sortingLayerID;
            outlineSr.sortingOrder = source.sortingOrder - 2; // 本体分身よりさらに背面。はみ出た分だけ縁取りとして見える
            outlineSr.enabled = false;

            pool[i] = new GhostSlot { sr = sr, outlineSr = outlineSr, remaining = 0f };
        }
    }

    private void Update()
    {
        float pulse = pulseSource.CurrentPulse;
        float visAlpha = visibility != null ? visibility.VisibilityAlpha : 1f;

        // パルスがしきい値を上向きに超えた瞬間（鳴動のピーク）だけ1体出現させる。
        // ★フェードイン完了(=1)を条件にすると、フェードインの4秒間に最初の鳴動が重なった時だけ
        //   残像が出ないことがあったため、「多少でも見え始めていればOK」に緩めてある
        if (previousPulse < triggerThreshold && pulse >= triggerThreshold && visAlpha > 0f)
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

        Transform outline = pool[slot].outlineSr.transform;
        outline.SetPositionAndRotation(transform.position, transform.rotation);
        outline.localScale = transform.lossyScale;

        pool[slot].sr.color = new Color(1f, 1f, 1f, startAlpha);
        pool[slot].sr.enabled = true;
        pool[slot].outlineSr.color = outlineColor;
        pool[slot].outlineSr.enabled = true;
        pool[slot].remaining = fadeDuration;
        pool[slot].baseScale = transform.lossyScale;
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
                pool[i].outlineSr.enabled = false;
                continue;
            }

            float t = pool[i].remaining / fadeDuration;
            Color c = pool[i].sr.color;
            c.a = startAlpha * t * visAlpha;
            pool[i].sr.color = c;

            // 出現時(t=1)は月と同じ大きさ、消える直前(t=0)はmaxScaleMultiplier倍まで
            // 外側へ膨らむ。衝撃波が広がって消えていくような「鳴動感」を出す
            float scaleMul = Mathf.Lerp(maxScaleMultiplier, 1f, t);
            Vector3 currentScale = pool[i].baseScale * scaleMul;
            pool[i].sr.transform.localScale = currentScale;

            // 輪郭は本体分身より一回り大きく保つ（本体と同じスケールに縁取り分の余白を掛ける）
            Color oc = outlineColor;
            oc.a = Mathf.Clamp01(startAlpha * outlineAlphaMultiplier) * t * visAlpha;
            pool[i].outlineSr.color = oc;
            pool[i].outlineSr.transform.localScale = currentScale * outlineScaleMargin;
        }
    }
}
