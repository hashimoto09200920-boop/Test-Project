using UnityEngine;

public class FloorNeonAnimator : MonoBehaviour
{
    [SerializeField] Sprite[]       patterns       = new Sprite[3];
    [SerializeField] SpriteRenderer secondRenderer;
    [SerializeField] float          holdDuration   = 3f;
    [SerializeField] float          fadeDuration   = 0.5f;

    SpriteRenderer srA, srB;
    int   currentIdx;
    float timer;
    bool  transitioning;
    bool  started;

    void Start()
    {
        srA = GetComponent<SpriteRenderer>();
        srB = secondRenderer;

        srB.sortingLayerID = srA.sortingLayerID;
        srB.sortingOrder   = srA.sortingOrder;
        srB.material       = srA.material;

        currentIdx    = 0;
        timer         = 0f;
        transitioning = false;
        started       = false;

        if (patterns.Length > 0 && patterns[0] != null)
            srA.sprite = patterns[0];
        SetAlpha(srA, 1f);
        SetAlpha(srB, 0f);

        EnemySpawner.OnStageStarted += HandleStageStarted;
    }

    void OnDestroy()
    {
        EnemySpawner.OnStageStarted -= HandleStageStarted;
    }

    void HandleStageStarted(int _)
    {
        if (started) return;
        started = true;
        timer   = 0f;
        EnemySpawner.OnStageStarted -= HandleStageStarted;
    }

    void Update()
    {
        if (!started) return;
        timer += Time.deltaTime;

        if (!transitioning)
        {
            if (timer >= holdDuration)
                BeginTransition();
        }
        else
        {
            float t = Mathf.Clamp01(timer / fadeDuration);
            SetAlpha(srA, 1f - t);
            SetAlpha(srB, t);

            if (timer >= fadeDuration)
                EndTransition();
        }
    }

    void BeginTransition()
    {
        timer         = 0f;
        transitioning = true;
        int nextIdx   = (currentIdx + 1) % patterns.Length;
        if (patterns[nextIdx] != null)
            srB.sprite = patterns[nextIdx];
        SetAlpha(srB, 0f);
    }

    void EndTransition()
    {
        currentIdx    = (currentIdx + 1) % patterns.Length;
        SetAlpha(srA, 0f);
        SetAlpha(srB, 1f);
        (srA, srB)    = (srB, srA); // 参照を交換して次のトランジションに備える
        timer         = 0f;
        transitioning = false;
    }

    void SetAlpha(SpriteRenderer sr, float a)
    {
        Color c = sr.color;
        c.a      = a;
        sr.color = c;
    }
}
