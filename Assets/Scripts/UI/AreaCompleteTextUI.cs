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
        InitCanvasGroup();
        canvasGroup.alpha = 0f;
    }

    private void InitCanvasGroup()
    {
        if (canvasGroup != null) return;
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    public IEnumerator Play()
    {
        // Awake() が未実行の場合（親が非アクティブで遅延）に備えて遅延初期化
        InitCanvasGroup();
        canvasGroup.alpha = 1f;

        // 全要素のRectTransformを収集しスケール0で初期化
        if (letterElements == null || letterElements.Length == 0)
        {
            yield return new WaitForSeconds(displayDuration);
        }
        else
        {
            var rts = new RectTransform[letterElements.Length];
            for (int i = 0; i < letterElements.Length; i++)
            {
                if (letterElements[i] != null)
                {
                    rts[i] = letterElements[i].GetComponent<RectTransform>();
                    if (rts[i] != null) rts[i].localScale = Vector3.zero;
                }
            }

            // サブコルーチンを使わず1ループで全文字を並列アニメーション
            // (StartCoroutine を this に対して呼ぶと非アクティブ時に失敗するため)
            float totalAnimTime = (letterElements.Length - 1) * letterStagger + bounceInDuration;
            float elapsed = 0f;
            while (elapsed < totalAnimTime)
            {
                elapsed += Time.deltaTime;
                for (int i = 0; i < rts.Length; i++)
                {
                    if (rts[i] == null) continue;
                    float t = Mathf.Clamp01((elapsed - i * letterStagger) / bounceInDuration);
                    if (t <= 0f) continue;
                    float s = t < 0.6f
                        ? Mathf.Lerp(0f, bounceScale, t / 0.6f)
                        : Mathf.Lerp(bounceScale, 1f, (t - 0.6f) / 0.4f);
                    rts[i].localScale = Vector3.one * s;
                }
                yield return null;
            }
            for (int i = 0; i < rts.Length; i++)
            {
                if (rts[i] != null) rts[i].localScale = Vector3.one;
            }

            yield return new WaitForSeconds(displayDuration);
        }

        // フェードアウト
        float elapsed2 = 0f;
        while (elapsed2 < fadeOutDuration)
        {
            elapsed2 += Time.deltaTime;
            canvasGroup.alpha = 1f - Mathf.Clamp01(elapsed2 / fadeOutDuration);
            yield return null;
        }
        canvasGroup.alpha = 0f;
    }
}
