using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// ボタンにホバー拡大・ホバー中点滅・SE再生エフェクトを追加するコンポーネント
/// YesButton / NoButton などにアタッチして使用する
/// </summary>
public class UIButtonEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Hover")]
    [SerializeField] private float hoverScale    = 1.08f;
    [SerializeField] private float hoverDuration = 0.08f;
    [SerializeField] private AudioClip hoverSE;

    [Header("Blink (ホバー中)")]
    [SerializeField] private bool  blinkEnabled  = true;
    [SerializeField] private float blinkInterval = 0.3f;

    [Header("Sibling Dim")]
    [Tooltip("ホバー時に暗くする相手ボタン（YesButton ↔ NoButton）")]
    [SerializeField] private UIButtonEffect siblingButton;
    [SerializeField] private float siblingDimAlpha = 0.4f;

    private Coroutine   scaleCoroutine;
    private Coroutine   blinkCoroutine;
    private CanvasGroup canvasGroup;
    private AudioSource audioSource;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake  = false;
            audioSource.spatialBlend = 0f;
        }
    }

    public void OnPointerEnter(PointerEventData _)
    {
        ScaleTo(hoverScale);

        if (hoverSE != null && audioSource != null)
        {
            float vol = SoundSettingsManager.Instance != null ? SoundSettingsManager.Instance.SEVolume : 1f;
            audioSource.PlayOneShot(hoverSE, vol);
        }

        if (blinkEnabled)
        {
            if (blinkCoroutine != null) StopCoroutine(blinkCoroutine);
            blinkCoroutine = StartCoroutine(BlinkCoroutine());
        }

        siblingButton?.SetDimmed(true, siblingDimAlpha);
    }

    public void OnPointerExit(PointerEventData _)
    {
        ScaleTo(1f);

        if (blinkCoroutine != null)
        {
            StopCoroutine(blinkCoroutine);
            blinkCoroutine = null;
        }
        canvasGroup.alpha = 1f;

        siblingButton?.SetDimmed(false, siblingDimAlpha);
    }

    /// <summary>sibling から呼ばれる。暗くする／元に戻す</summary>
    public void SetDimmed(bool dimmed, float dimAlpha)
    {
        if (canvasGroup != null)
            canvasGroup.alpha = dimmed ? dimAlpha : 1f;
    }

    // =====================================================
    // Blink
    // =====================================================

    private IEnumerator BlinkCoroutine()
    {
        while (true)
        {
            canvasGroup.alpha = 0.4f;
            yield return new WaitForSecondsRealtime(blinkInterval);
            canvasGroup.alpha = 1f;
            yield return new WaitForSecondsRealtime(blinkInterval);
        }
    }

    // =====================================================
    // Scale
    // =====================================================

    private void ScaleTo(float target)
    {
        if (scaleCoroutine != null) StopCoroutine(scaleCoroutine);
        scaleCoroutine = StartCoroutine(ScaleCoroutine(target));
    }

    private IEnumerator ScaleCoroutine(float target)
    {
        float elapsed = 0f;
        Vector3 from  = transform.localScale;
        Vector3 to    = Vector3.one * target;
        while (elapsed < hoverDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            transform.localScale = Vector3.Lerp(from, to, elapsed / hoverDuration);
            yield return null;
        }
        transform.localScale = to;
    }
}
