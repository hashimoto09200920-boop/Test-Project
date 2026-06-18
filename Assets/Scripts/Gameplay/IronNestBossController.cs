using UnityEngine;
using System.Collections;

/// <summary>
/// IronNest ボスコントローラー
/// 各NMは IronNestNM の Timing 設定で独立動作する。
/// このコントローラーは初回出現のタイミングだけ個別にずらす。
/// </summary>
public class IronNestBossController : MonoBehaviour
{
    [Header("NM References")]
    [SerializeField] private IronNestNM nm01;
    [SerializeField] private IronNestNM nm02;
    [SerializeField] private IronNestNM nm03;

    [Header("Initial Delay (seconds)")]
    [Tooltip("NM01 の初回出現までの遅延（秒）")]
    [SerializeField] private float nm01InitialDelay = 0f;

    [Tooltip("NM02 の初回出現までの遅延（秒）")]
    [SerializeField] private float nm02InitialDelay = 2f;

    [Tooltip("NM03 の初回出現までの遅延（秒）")]
    [SerializeField] private float nm03InitialDelay = 4f;

    [Header("Fade In")]
    [Tooltip("出現時フェードイン時間（秒）。EnemySpawnerのstage3FadeInDurationに合わせる）")]
    [SerializeField] private float initialFadeInDuration = 3f;
    [Tooltip("フェードイン対象のSpriteRenderer（本体BodyのSRを設定）")]
    [SerializeField] private SpriteRenderer[] fadeInRenderers;

    private void Awake()
    {
        if (nm01 != null) nm01.StopAutoLoop();
        if (nm02 != null) nm02.StopAutoLoop();
        if (nm03 != null) nm03.StopAutoLoop();
    }

    private void Start()
    {
        StartCoroutine(FadeInThenStart());
    }

    private IEnumerator FadeInThenStart()
    {
        if (fadeInRenderers != null && fadeInRenderers.Length > 0 && initialFadeInDuration > 0f)
        {
            foreach (var sr in fadeInRenderers)
            {
                if (sr == null) continue;
                var c = sr.color;
                sr.color = new Color(c.r, c.g, c.b, 0f);
            }

            float elapsed = 0f;
            while (elapsed < initialFadeInDuration)
            {
                elapsed += Time.deltaTime;
                float alpha = Mathf.Clamp01(elapsed / initialFadeInDuration);
                foreach (var sr in fadeInRenderers)
                {
                    if (sr == null) continue;
                    var c = sr.color;
                    sr.color = new Color(c.r, c.g, c.b, alpha);
                }
                yield return null;
            }

            foreach (var sr in fadeInRenderers)
            {
                if (sr == null) continue;
                var c = sr.color;
                sr.color = new Color(c.r, c.g, c.b, 1f);
            }
        }

        if (nm01 != null) nm01.StartLoop(nm01InitialDelay);
        if (nm02 != null) nm02.StartLoop(nm02InitialDelay);
        if (nm03 != null) nm03.StartLoop(nm03InitialDelay);
    }
}
