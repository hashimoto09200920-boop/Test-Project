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
    public class ButtonHoverEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerClickHandler
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

        [Header("Lock After Click")]
        [Tooltip("ONにすると、クリック後もホバー拡大・点滅した見た目のまま固定する（画面遷移するボタン向け）。\n" +
                 "OFF（デフォルト）だと従来通り、ポインタが離れた時点で元に戻る。\n" +
                 "★他の見た目制御（例：選択状態に応じた枠の色分け）と同じImageを対象にしていると競合するため、\n" +
                 "　本当に「クリック後に画面が変わって戻ってこない」ボタンだけでONにすること。")]
        [SerializeField] private bool lockAfterClick = false;

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
        private bool lockedAfterClick = false;
        private TouchTapToConfirm ownTouchTapToConfirm;

        /// <summary>現在ホバー拡大した見た目になっているか（PC:ホバー中／スマホ:1タップ目で確定待ち中）。外部から参照用。</summary>
        public bool IsEnlarged { get; private set; }

        /// <summary>クリック後も拡大・点滅した見た目のまま固定する設定か。外部から参照用（TouchTapToConfirm等）。</summary>
        public bool LockAfterClick => lockAfterClick;

        /// <summary>
        /// true の間、全ButtonHoverEffectインスタンスでOnPointerEnter/SetHeldそのものを完全に無効化する
        /// （拡大・点滅・SEすべて発火しない）。Title起動直後の入力ブロック期間、Language/Soundパネル表示中、
        /// AreaSelectの各種画面遷移中・確認ダイアログ表示中など、「クリックだけでなくホバー演出自体も
        /// 背後のボタンで一切起きてほしくない」ケースで使う。
        /// 各ブロック処理の開始時にtrue、ブロック解除時にfalseへ戻すこと。既定はfalse。
        /// </summary>
        public static bool InputLocked = false;

        /// <summary>
        /// nullでない間、このTransformの子孫「以外」のButtonHoverEffectでOnPointerEnter/SetHeldを無効化する
        /// （子孫自身は通常通りホバー拡大・SEが機能する）。Sound/Languageパネル、確認ダイアログ(はい/いいえ)等、
        /// 「パネル自身のボタンは使わせつつ、背後のボタンだけブロックしたい」モーダルUIで使う。
        /// InputLockedとは違い、こちらは対象を選んでブロックする（InputLockedは例外なく全ブロック）。
        /// パネルを表示した瞬間にそのパネルのTransformを設定し、閉じた瞬間にnullへ戻すこと。
        /// </summary>
        public static Transform ModalPanelRoot;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticFlagsOnGameStart()
        {
            InputLocked = false;
            ModalPanelRoot = null;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= ResetStaticFlagsOnSceneLoaded;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += ResetStaticFlagsOnSceneLoaded;
        }

        /// <summary>
        /// ★保険：シーン遷移をまたいでこれらの状態がtrue/非null のまま残ると、遷移先シーンの
        /// ホバー拡大・SEが（原因不明のまま）一切機能しなくなるという重大な不具合になる。
        /// 呼び出し元でのリセット漏れ・例外による戻し漏れに備え、シーンがロードされる度に
        /// 必ず初期状態へ強制リセットする。
        /// </summary>
        private static void ResetStaticFlagsOnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            InputLocked = false;
            ModalPanelRoot = null;
        }

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

            ownTouchTapToConfirm = GetComponent<TouchTapToConfirm>();

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
            lockedAfterClick = false;
            IsEnlarged = false;
            transform.localScale = originalScale;

            // ★OnPointerExitと同じ理由：無効化されている間は色を戻さない
            if (requireInteractable && button != null && !button.interactable) return;

            RestoreColor();
        }

        /// <summary>
        /// lockAfterClick(Inspectorのデザイン既定値)の設定有無に関わらず、このタップに限って
        /// 強制的に「クリック後も拡大表示を維持する」状態にする。画面遷移を開始する側のコードから
        /// 直接呼ぶ想定（Editor拡張「Apply Hover Effect To Buttons」の実行し忘れでlockAfterClickが
        /// falseのままでも確実に効かせたい場合に使う）。
        /// </summary>
        public void LockEnlargedThroughTransition()
        {
            lockedAfterClick = true;
        }

        /// <summary>
        /// クリック後に拡大・点滅状態のまま固定（lockedAfterClick）されたボタンを、外部から強制的に
        /// 元の見た目に戻す。GemManagementUI/ShopUI等、クリック先の画面を閉じてAreaSelectへ戻るタイミングで
        /// 呼ぶ想定（これらのボタンはパネル表示中もSetActive(false)にならず、OnDisableが発火しないため）。
        /// </summary>
        public void ForceReset()
        {
            if (!initialized) return;
            StopBlink();
            StopScale();
            StopHoverSE();
            tapDetected = false;
            held = false;
            lockedAfterClick = false;
            IsEnlarged = false;
            transform.localScale = originalScale;
            RestoreColor();
        }

        /// <summary>
        /// ForceReset()と違い、拡大→通常サイズへの縮小をOnPointerExit/SetHeld(false)と同じ
        /// 滑らかなアニメーション(ScaleCoroutine)で行う。確認ダイアログを閉じた時など、
        /// 「カーソルが外れた/何もない場所をタップした時」と同じ自然な戻り方を、
        /// ポインタの状態に関わらず外部から明示的に発生させたい場合に使う（瞬時にスナップさせない）。
        /// 既に通常サイズなら何もしない。
        /// </summary>
        public void SmoothReset()
        {
            if (!initialized) return;
            if (!IsEnlarged && !lockedAfterClick) return;

            tapDetected = false;
            lockedAfterClick = false;
            StopHoverSE();
            IsEnlarged = false;
            StartScaleTo(originalScale);
            StopBlink();

            // ★他の箇所と同じ理由：無効化されている間は色を戻さない
            if (requireInteractable && button != null && !button.interactable) return;

            RestoreColor();
        }

        /// <summary>
        /// スマホのタップ確定待ち（TouchTapToConfirm）用。true中はOnPointerExitが来ても
        /// 拡大・点滅を解除しない。ホバーしたときと同じ見た目を、指を離した後も保持する。
        /// </summary>
        public void SetHeld(bool value, bool playSE = true)
        {
            if (held == value) return;
            held = value;

            if (held)
            {
                if (!IsEffectActive() || IsBlockedByModal()) return;
                IsEnlarged = true;
                StartScaleTo(originalScale * hoverScale);
                if (playSE && hoverSE != null && audioSource != null)
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
                IsEnlarged = false;
                StartScaleTo(originalScale);
                StopBlink();

                // ★他の箇所と同じ理由：無効化されている間は色を戻さない
                if (requireInteractable && button != null && !button.interactable) return;

                RestoreColor();
            }
        }

        private bool IsEffectActive()
        {
            if (requireInteractable && button != null && !button.interactable)
                return false;
            return true;
        }

        /// <summary>
        /// InputLocked（例外なく全ブロック）またはModalPanelRoot（自分がその子孫でない場合のみブロック）
        /// によって、このボタンのホバー演出が現在ブロックされているかどうか。
        /// </summary>
        private bool IsBlockedByModal()
        {
            if (InputLocked) return true;
            if (ModalPanelRoot != null && !transform.IsChildOf(ModalPanelRoot)) return true;
            return false;
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

            // ★Title起動直後の1秒ブロック・画面遷移中(InputLocked)や、
            //   モーダルパネル表示中に背後にいる場合(ModalPanelRoot)など、ホバー演出自体を
            //   丸ごと止めたい期間。ここで即returnすることで拡大・点滅・SEすべてが発火しない。
            if (IsBlockedByModal()) return;

            if (!IsEffectActive()) return;
            tapDetected = false;
            // ★新しいホバーサイクルの開始なので、前回クリック時のロックは解除する
            lockedAfterClick = false;
            IsEnlarged = true;

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

            // ★TouchTapToConfirmを自前で持たないボタン（Areaノード等）向けの保険。
            //   これが無いと、別のボタンがTouchTapToConfirmで確定待ち(armed/拡大中)の時に
            //   このボタンを直接タップすると、確定前なのにそのままonClickが発火してしまっていた。
            //   自前でTouchTapToConfirmを持つボタンは、そちら側のOnPointerDownが同じ役割を
            //   既に果たしているため、ここでは何もしない（二重処理による自分自身の1回目タップ
            //   無効化を防ぐ）。
            if (ownTouchTapToConfirm == null && IsTouchEvent(eventData) && TouchTapToConfirm.DismissIfOtherArmed(gameObject))
            {
                SuppressThisTapClick();
            }
        }

        private void SuppressThisTapClick()
        {
            if (button == null) return;
            button.interactable = false;
            StartCoroutine(RestoreInteractableNextFrame());
        }

        private IEnumerator RestoreInteractableNextFrame()
        {
            yield return null; // このタップのPointerClick判定が終わるまで待ってから戻す
            if (button != null) button.interactable = true;
        }

        /// <summary>
        /// クリック確定時に呼ばれる。以降のOnPointerExitでは拡大・点滅を解除しないようにし、
        /// 画面遷移のフェード等でポインタが外れた扱いになっても、ホバー拡大した見た目のまま維持する。
        /// ★以前はButton.onClickにリスナー登録して検知していたが、TitleMenu等が起動時に
        ///   button.onClick.RemoveAllListeners()を呼ぶ実装になっており、Awakeの実行順序次第で
        ///   このリスナーごと消されてlockAfterClickが一切効かなくなる不具合があった
        ///   （Start押下でStart自身の拡大が解除される不具合の直接の原因）。
        ///   IPointerClickHandlerはEventSystemがGameObject上の全実装先へ直接ディスパッチするため、
        ///   Button.onClickのリスナー一覧とは完全に独立しており、この問題が起きない。
        /// </summary>
        public void OnPointerClick(PointerEventData eventData)
        {
            if (lockAfterClick) lockedAfterClick = true;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            // ★タッチ由来のイベントはTouchTapToConfirm側のSetHeldだけに処理させる（OnPointerEnterと対称）
            if (IsTouchEvent(eventData)) return;

            // held中（タップ確定待ち）はタッチ終了時の自動Exitで解除しない
            if (held) return;

            // ★クリック直後（画面遷移のフェード等でポインタが外れた扱いになる場合）は、
            //   拡大・点滅状態を維持したままにする。次にホバーが始まった時点でロックは解除される。
            if (lockedAfterClick) return;

            // IsEffectActiveに関わらず常にクリーンアップする
            tapDetected = false;
            StopHoverSE();
            IsEnlarged = false;
            StartScaleTo(originalScale);
            StopBlink();

            // ★無効化されている間(requireInteractable=ON かつ button.interactable=false)は
            //   色を戻さない。外部スクリプトが無効化状態を示す色(グレーアウト等)を設定している
            //   場合、ここで元の色に戻すと無条件に上書きしてしまうため。
            if (requireInteractable && button != null && !button.interactable) return;

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
                    // ★無効化されている間(requireInteractable=ON かつ button.interactable=false)は
                    //   色を戻さない。外部スクリプトが無効化状態の色を設定している場合、
                    //   ここで元の色に戻すと上書きしてしまうため。
                    bool disabledByInteractable = requireInteractable && button != null && !button.interactable;
                    if (!disabledByInteractable) RestoreColor();
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
