using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Buttonにアタッチするだけでホバーエフェクト（拡大・SE・点滅）を追加するコンポーネント。
    /// GraphicRaycaster が Canvas にある前提。
    /// </summary>
    public class ButtonHoverEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler
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
        [Tooltip("blinkTargetに加えて、他にも点滅させたいImageがあればここに追加する（複数指定可）")]
        [SerializeField] private Image[] additionalBlinkTargets;
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
        private Coroutine hoverSECoroutine;
        private Vector3 originalScale;
        private Image[] blinkTargets;
        private Color[] capturedColors;
        private bool initialized = false;
        private bool tapDetected = false;
        private bool held = false;

        private void Awake()
        {
            originalScale = transform.localScale;

            // blinkTarget(単数・旧仕様との互換用)とadditionalBlinkTargets(複数)をまとめて1つの配列にする。
            // どちらも未設定の場合のみ、このGameObjectのImageを自動取得する（元の挙動を維持）。
            var list = new List<Image>();
            if (blinkTarget != null) list.Add(blinkTarget);
            if (additionalBlinkTargets != null)
            {
                foreach (var img in additionalBlinkTargets)
                    if (img != null && !list.Contains(img)) list.Add(img);
            }
            if (list.Count == 0)
            {
                var fallback = GetComponent<Image>();
                if (fallback != null) list.Add(fallback);
            }
            blinkTargets = list.ToArray();
            CaptureColors();

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
            StopHoverSE();
            tapDetected = false;
            held = false;
            transform.localScale = originalScale;
            RestoreColor();
        }

        /// <summary>
        /// スマホのタップ確定待ち（TouchTapToConfirm）用。true中はOnPointerExitが来ても
        /// 拡大・点滅を解除しない。ホバーしたときと同じ見た目を、指を離した後も保持する。
        /// </summary>
        public void SetHeld(bool value)
        {
            if (held == value) return;
            held = value;

            if (held)
            {
                if (!IsEffectActive()) return;
                StartScaleTo(originalScale * hoverScale);
                if (hoverSE != null && audioSource != null)
                {
                    float vol = hoverSEVolume * (SoundSettingsManager.Instance != null ? SoundSettingsManager.Instance.SEVolume : 1f);
                    audioSource.PlayOneShot(hoverSE, vol);
                }
                CaptureColors();
                StopBlink();
                blinkCoroutine = StartCoroutine(BlinkCoroutine());
            }
            else
            {
                StartScaleTo(originalScale);
                StopBlink();
                RestoreColor();
            }
        }

        private bool IsEffectActive()
        {
            if (requireInteractable && button != null && !button.interactable)
                return false;
            return true;
        }

        private static bool IsTouchEvent(PointerEventData eventData)
        {
            return eventData is ExtendedPointerEventData extended && extended.pointerType == UIPointerType.Touch;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            // ★タッチ由来のイベントはTouchTapToConfirm側のSetHeldだけに処理させる。
            //   ここでも処理してしまうと、同じタップに対してCaptureColors→ブリンク開始が
            //   二重に走り、片方が汚染された色を「元の色」として記憶してしまう。
            if (IsTouchEvent(eventData)) return;

            if (!IsEffectActive()) return;
            tapDetected = false;

            // 拡大
            StartScaleTo(originalScale * hoverScale);

            // SE再生（1フレーム後に実行。同フレームにOnPointerDownが来た場合＝タップとしてスキップ）
            if (hoverSE != null && audioSource != null)
            {
                StopHoverSE();
                hoverSECoroutine = StartCoroutine(PlayHoverSEDelayed());
            }

            // 点滅直前の実際の色をキャプチャしてから開始
            CaptureColors();
            StopBlink();
            blinkCoroutine = StartCoroutine(BlinkCoroutine());
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            tapDetected = true;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            // ★タッチ由来のイベントはTouchTapToConfirm側のSetHeldだけに処理させる（OnPointerEnterと対称）
            if (IsTouchEvent(eventData)) return;

            // held中（タップ確定待ち）はタッチ終了時の自動Exitで解除しない
            if (held) return;

            // IsEffectActiveに関わらず常にクリーンアップする
            tapDetected = false;
            StopHoverSE();
            StartScaleTo(originalScale);

            // 点滅停止・色を元に戻す
            StopBlink();
            RestoreColor();
        }

        private void CaptureColors()
        {
            if (blinkTargets == null) return;
            capturedColors = new Color[blinkTargets.Length];
            for (int i = 0; i < blinkTargets.Length; i++)
            {
                if (blinkTargets[i] != null) capturedColors[i] = blinkTargets[i].color;
            }
        }

        private void RestoreColor()
        {
            if (blinkTargets == null || capturedColors == null) return;
            for (int i = 0; i < blinkTargets.Length; i++)
            {
                if (blinkTargets[i] != null) blinkTargets[i].color = capturedColors[i];
            }
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

        private void StopHoverSE()
        {
            if (hoverSECoroutine != null)
            {
                StopCoroutine(hoverSECoroutine);
                hoverSECoroutine = null;
            }
        }

        private IEnumerator PlayHoverSEDelayed()
        {
            yield return null; // 1フレーム待つ（同フレームのOnPointerDownを検知するため）
            if (!tapDetected)
            {
                float vol = hoverSEVolume * (SoundSettingsManager.Instance != null ? SoundSettingsManager.Instance.SEVolume : 1f);
                audioSource.PlayOneShot(hoverSE, vol);
            }
            hoverSECoroutine = null;
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
            if (blinkTargets == null || blinkTargets.Length == 0) yield break;
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
                for (int i = 0; i < blinkTargets.Length; i++)
                {
                    if (blinkTargets[i] != null) blinkTargets[i].color = Color.Lerp(capturedColors[i], blinkColor, t);
                }
                yield return null;
            }
        }
    }
}
