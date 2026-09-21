using System.Collections;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class FarLayerFade : MonoBehaviour
{
    [Tooltip("スプライト切り替え時のフェード時間（秒）")]
    [Range(0.1f, 3.0f)]
    [SerializeField] private float transitionDuration = 1.0f;

    private SpriteRenderer sr;
    private BackgroundFitter2D fitter;

    public bool IsTransitioning { get; private set; }

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        fitter = GetComponent<BackgroundFitter2D>();
    }

    public void TransitionToSprite(Sprite newSprite, Vector3 newScale, Vector3 newPosition, float extraScale = 1f)
    {
        StartCoroutine(TransitionRoutine(newSprite, newScale, newPosition, extraScale));
    }

    private IEnumerator TransitionRoutine(Sprite newSprite, Vector3 newScale, Vector3 newPosition, float extraScale)
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

        // ★BackgroundFitter2Dが付いている場合、上のlocalScale直接代入だけだと
        //   Fitter側のキャッシュ判定に引っかからず、Cover計算＋extraScaleが反映されないまま
        //   ずっとnewScaleの生値で固定されてしまう。ここで明示的に再計算させて確定させる。
        if (fitter != null)
            fitter.SetExtraScale(extraScale);

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
