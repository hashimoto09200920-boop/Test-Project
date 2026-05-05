using System.Collections;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class FarLayerFade : MonoBehaviour
{
    [Tooltip("スプライト切り替え時のフェード時間（秒）")]
    [Range(0.1f, 3.0f)]
    [SerializeField] private float transitionDuration = 1.0f;

    private SpriteRenderer sr;

    public bool IsTransitioning { get; private set; }

    private void Awake() => sr = GetComponent<SpriteRenderer>();

    public void TransitionToSprite(Sprite newSprite, Vector3 newScale, Vector3 newPosition)
    {
        StartCoroutine(TransitionRoutine(newSprite, newScale, newPosition));
    }

    private IEnumerator TransitionRoutine(Sprite newSprite, Vector3 newScale, Vector3 newPosition)
    {
        IsTransitioning = true;

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

        // スプライト切り替え
        sr.sprite = newSprite;
        transform.localScale = newScale;
        transform.localPosition = newPosition;

        // フェードイン
        elapsed = 0f;
        while (elapsed < transitionDuration)
        {
            elapsed += Time.deltaTime;
            Color c = sr.color;
            c.a = Mathf.Lerp(0f, 1f, elapsed / transitionDuration);
            sr.color = c;
            yield return null;
        }

        Color final = sr.color;
        final.a = 1f;
        sr.color = final;
        IsTransitioning = false;
    }
}
