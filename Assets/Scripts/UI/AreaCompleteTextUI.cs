using System.Collections;
using UnityEngine;

/// <summary>
/// AREA COMPLETE テキストをポップイン演出付きで表示する。
/// StageIntroController.PlayAreaComplete() から yield return StartCoroutine(Play()) で呼ばれる。
/// Hierarchy: Canvas > AreaCompleteTextUI (このコンポーネント + CanvasGroup) > Letter要素...
/// </summary>
public class AreaCompleteTextUI : MonoBehaviour
{
    [Header("Letters")]
    [Tooltip("ポップイン順に並べたRectTransform配列（1文字 or 1単語単位）")]
    [SerializeField] private GameObject[] letterElements;
    [Tooltip("各要素のポップイン間隔（秒）")]
    [SerializeField] private float letterStagger    = 0.05f;

    [Header("Bounce")]
    [Tooltip("ポップイン時のオーバーシュートスケール")]
    [SerializeField] private float bounceScale      = 1.35f;
    [Tooltip("1要素のバウンスイン時間（秒）")]
    [SerializeField] private float bounceInDuration = 0.25f;

    [Header("Timing")]
    [Tooltip("全文字表示後の維持時間（秒）")]
    [SerializeField] private float displayDuration  = 2.5f;
    [Tooltip("フェードアウト時間（秒）")]
    [SerializeField] private float fadeOutDuration  = 0.6f;

    private CanvasGroup canvasGroup;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        gameObject.SetActive(false);
    }

    public IEnumerator Play()
    {
        gameObject.SetActive(true);
        canvasGroup.alpha = 1f;

        // 全要素をスケール0で初期化
        foreach (var el in letterElements)
        {
            if (el == null) continue;
            var rt = el.GetComponent<RectTransform>();
            if (rt != null) rt.localScale = Vector3.zero;
        }

        // 1要素ずつポップイン
        foreach (var el in letterElements)
        {
            if (el != null)
            {
                var rt = el.GetComponent<RectTransform>();
                if (rt != null) StartCoroutine(BounceIn(rt));
            }
            yield return new WaitForSeconds(letterStagger);
        }

        // 最後のBounceIn完了を待つ
        yield return new WaitForSeconds(bounceInDuration);

        // 表示維持
        yield return new WaitForSeconds(displayDuration);

        // フェードアウト
        float elapsed = 0f;
        while (elapsed < fadeOutDuration)
        {
            elapsed += Time.deltaTime;
            canvasGroup.alpha = 1f - Mathf.Clamp01(elapsed / fadeOutDuration);
            yield return null;
        }
        canvasGroup.alpha = 0f;
        gameObject.SetActive(false);
    }

    private IEnumerator BounceIn(RectTransform rt)
    {
        float elapsed = 0f;
        while (elapsed < bounceInDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / bounceInDuration);
            float s = t < 0.6f
                ? Mathf.Lerp(0f, bounceScale, t / 0.6f)
                : Mathf.Lerp(bounceScale, 1f, (t - 0.6f) / 0.4f);
            rt.localScale = Vector3.one * s;
            yield return null;
        }
        rt.localScale = Vector3.one;
    }
}
