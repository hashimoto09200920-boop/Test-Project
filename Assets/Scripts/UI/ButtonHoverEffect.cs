using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Buttonにアタッチするだけでホバーエフェクト（拡大・SE・点滅）を追加するコンポーネント。
    /// GraphicRaycaster が Canvas にある前提。
    /// </summary>
    public class ButtonHoverEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("Scale")]
        [Tooltip("ホバー時の拡大倍率（例: 1.05 = 5%拡大）")]
        [SerializeField] private float hoverScale = 1.05f;
        [Tooltip("拡大縮小アニメーションの時間（秒）")]
        [SerializeField] private float hoverScaleDuration = 0.1f;

        [Header("SE")]
        [Tooltip("ホバー時に鳴らすSE")]
        [SerializeField] private AudioClip hoverSE;
        [Tooltip("ホバーSEの音量（SoundSettingsManagerのSEVolumeに掛け合わせる）")]
        [SerializeField] [Range(0f, 1f)] private float hoverSEVolume = 1f;

        [Header("Blink")]
        [Tooltip("点滅させるImage（未設定の場合、このGameObjectのImageを自動取得）")]
        [SerializeField] private Image blinkTarget;
        [Tooltip("点滅速度（1秒あたりのサイクル数）")]
        [SerializeField] private float blinkSpeed = 2f;
        [Tooltip("点滅時にブレンドする色（元の色からこの色に変化する）")]
        [SerializeField] private Color blinkColor = new Color(1f, 0.85f, 0.2f, 1f);
        [Tooltip("点滅の強さ（0=変化なし, 1=blinkColorに完全に変化）")]
        [SerializeField] [Range(0f, 1f)] private float blinkIntensity = 0.8f;

        [Header("Interactable Check")]
        [Tooltip("ONにすると、ButtonのInteractableがfalseの時はホバーエフェクトを無効にする")]
        [SerializeField] private bool requireInteractable = false;

        private Button button;
        private AudioSource audioSource;
        private Coroutine blinkCoroutine;
        private Coroutine scaleCoroutine;
        private Vector3 originalScale;
        private Color capturedColor;
        private bool initialized = false;

        private void Awake()
        {
            originalScale = transform.localScale;

            if (blinkTarget == null)
                blinkTarget = GetComponent<Image>();
            if (blinkTarget != null)
                capturedColor = blinkTarget.color;

            button = GetComponent<Button>();

            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
            }

            initialized = true;
        }

        private void OnDisable()
        {
            if (!initialized) return;
            StopBlink();
            StopScale();
            transform.localScale = originalScale;
            RestoreColor();
        }

        private bool IsEffectActive()
        {
            if (requireInteractable && button != null && !button.interactable)
                return false;
            return true;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!IsEffectActive()) return;
            // 拡大
            StartScaleTo(originalScale * hoverScale);

            // SE再生（SoundSettingsManagerで音量補正）
            if (hoverSE != null && audioSource != null)
            {
                float vol = hoverSEVolume * (SoundSettingsManager.Instance != null ? SoundSettingsManager.Instance.SEVolume : 1f);
                audioSource.PlayOneShot(hoverSE, vol);
            }

            // 点滅直前の実際の色をキャプチャしてから開始
            if (blinkTarget != null)
                capturedColor = blinkTarget.color;
            StopBlink();
            blinkCoroutine = StartCoroutine(BlinkCoroutine());
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            // IsEffectActiveに関わらず常にクリーンアップする
            StartScaleTo(originalScale);

            // 点滅停止・色を元に戻す
            StopBlink();
            RestoreColor();
        }

        private void RestoreColor()
        {
            if (blinkTarget == null) return;
            blinkTarget.color = capturedColor;
        }

        private void StartScaleTo(Vector3 target)
        {
            StopScale();
            scaleCoroutine = StartCoroutine(ScaleCoroutine(target));
        }

        private void StopBlink()
        {
            if (blinkCoroutine != null)
            {
                StopCoroutine(blinkCoroutine);
                blinkCoroutine = null;
            }
        }

        private void StopScale()
        {
            if (scaleCoroutine != null)
            {
                StopCoroutine(scaleCoroutine);
                scaleCoroutine = null;
            }
        }

        private IEnumerator ScaleCoroutine(Vector3 target)
        {
            Vector3 start = transform.localScale;
            float elapsed = 0f;
            while (elapsed < hoverScaleDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                transform.localScale = Vector3.Lerp(start, target, elapsed / hoverScaleDuration);
                yield return null;
            }
            transform.localScale = target;
            scaleCoroutine = null;
        }

        private IEnumerator BlinkCoroutine()
        {
            if (blinkTarget == null) yield break;
            while (true)
            {
                // インタラクタブル状態が変化したら自動停止
                if (!IsEffectActive())
                {
                    RestoreColor();
                    blinkCoroutine = null;
                    yield break;
                }
                float t = (Mathf.Sin(Time.unscaledTime * blinkSpeed * Mathf.PI * 2f) + 1f) * 0.5f * blinkIntensity;
                blinkTarget.color = Color.Lerp(capturedColor, blinkColor, t);
                yield return null;
            }
        }
    }
}
