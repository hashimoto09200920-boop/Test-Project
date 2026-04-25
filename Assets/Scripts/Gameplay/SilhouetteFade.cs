using System.Collections;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class SilhouetteFade : MonoBehaviour
{
    [Tooltip("フェードの最小アルファ（0=完全透明）")]
    [Range(0f, 1f)]
    [SerializeField] private float minAlpha = 0.3f;

    [Tooltip("フェードの最大アルファ（1=完全不透明）")]
    [Range(0f, 1f)]
    [SerializeField] private float maxAlpha = 1.0f;

    [Tooltip("フェードの速さ（Hz）。0.01=非常にゆっくり、0.2=ゆっくり、1.0=速い")]
    [Range(0.01f, 2.0f)]
    [SerializeField] private float cycleSpeed = 0.3f;

    [Tooltip("スプライト切り替え時のフェード時間（秒）")]
    [Range(0.1f, 3.0f)]
    [SerializeField] private float transitionDuration = 1.0f;

    private SpriteRenderer sr;
    private float time;
    private bool transitioning;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
    }

    private void Update()
    {
        if (transitioning) return;

        time += Time.deltaTime;
        float t = (Mathf.Sin(time * cycleSpeed * Mathf.PI * 2f) + 1f) * 0.5f;
        Color c = sr.color;
        c.a = Mathf.Lerp(minAlpha, maxAlpha, t);
        sr.color = c;
    }

    public void TransitionToSprite(Sprite newSprite, Vector3 newScale, Vector3 newPosition)
    {
        StartCoroutine(TransitionRoutine(newSprite, newScale, newPosition));
    }

    private IEnumerator TransitionRoutine(Sprite newSprite, Vector3 newScale, Vector3 newPosition)
    {
        transitioning = true;

        // フェードアウト
        float elapsed = 0f;
        float startAlpha = sr.color.a;
        while (elapsed < transitionDuration)
        {
            elapsed += Time.deltaTime;
            Color c = sr.color;
            c.a = Mathf.Lerp(startAlpha, 0f, elapsed / transitionDuration);
            sr.color = c;
            yield return null;
        }

        // スプライト切り替え・スケール・ポジション適用
        sr.sprite = newSprite;
        transform.localScale = newScale;
        transform.localPosition = newPosition;

        // フェードイン
        elapsed = 0f;
        while (elapsed < transitionDuration)
        {
            elapsed += Time.deltaTime;
            Color c = sr.color;
            c.a = Mathf.Lerp(0f, maxAlpha, elapsed / transitionDuration);
            sr.color = c;
            yield return null;
        }

        // 通常ループ再開（位相をリセット）
        time = 0f;
        transitioning = false;
    }
}
