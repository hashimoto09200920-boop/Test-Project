using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
#if UNITY_EDITOR
using System.Linq;
#endif

namespace Game.UI
{
    /// <summary>
    /// ジェム使用回数システム関連の通知・確認ダイアログ。
    /// ・ジェムが使用回数0で消滅した際の通知（OKのみ）
    /// ・出撃前、装備中ジェムがこのプレイで消滅する場合の確認（はい/いいえ）
    /// </summary>
    [DisallowMultipleComponent]
    public class GemLifecycleUI : MonoBehaviour
    {
        [Header("Depletion Notice Panel (消滅通知・OKのみ)")]
        [SerializeField] private GameObject noticePanel;
        [SerializeField] private TextMeshProUGUI noticeText;
        [SerializeField] private Button noticeOkButton;

        [Header("Pre-Launch Confirm Panel (出撃前確認・はい/いいえ)")]
        [SerializeField] private GameObject confirmPanel;
        [SerializeField] private TextMeshProUGUI confirmText;
        [SerializeField] private Button confirmYesButton;
        [SerializeField] private Button confirmNoButton;
        [Tooltip("確認パネルの背景画像(Box)。中断メニューと同じMainBg.pngを流用する")]
        [SerializeField] private Image confirmBoxImage;

        [Header("SE")]
        [Tooltip("警告パネル表示時・いいえボタン押下時に鳴らすSE。Areaノードクリック音(AreaSelectManagerのbuttonClickSE)と同じクリップを設定する想定。" +
            "はいボタンは既にAreaSelectManager.LoadGameSceneWithSE()側で同じSEが鳴るため、ここでは重複再生しない")]
        [SerializeField] private AudioClip confirmSE;

        [Header("Warning Text Emphasis (GemManagementUIのLowUsesWarningTextと同じ演出)")]
        [Tooltip("点滅時にブレンドする色（通常色からこの色へ往復する）")]
        [SerializeField] private Color confirmTextBlinkColor = Color.white;
        [Tooltip("点滅速度（1秒あたりのサイクル数）")]
        [SerializeField] private float confirmTextBlinkSpeed = 1.5f;
        [Tooltip("拡大縮小パルスの振れ幅（0.06なら94%〜106%の間で変化）")]
        [SerializeField] private float confirmTextPulseAmount = 0.06f;
        [Tooltip("拡大縮小パルスの速さ（1秒あたりのサイクル数）")]
        [SerializeField] private float confirmTextPulseSpeed = 0.5f;

        private Color confirmTextOriginalColor;
        private Coroutine confirmTextPulseCoroutine;
        private AudioSource audioSource;

        private Action pendingOnYes;
        private Action pendingOnCancel;

        private void Awake()
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
            }

            if (noticeOkButton != null) noticeOkButton.onClick.AddListener(HideNotice);
            if (confirmYesButton != null) confirmYesButton.onClick.AddListener(OnConfirmYes);
            if (confirmNoButton != null) confirmNoButton.onClick.AddListener(OnConfirmNo);

            if (noticePanel != null) noticePanel.SetActive(false);
            if (confirmPanel != null) confirmPanel.SetActive(false);

            // ★元は左上揃えのため、枠のサイズを大きくするとテキストが左上に寄って余白が目立つ・
            //   はみ出して見えるため、中央揃えに変更する。
            if (confirmText != null)
            {
                confirmText.alignment = TextAlignmentOptions.Center;
                confirmTextOriginalColor = confirmText.color;
            }
        }

        /// <summary>消滅したジェムを一覧表示する通知（OKのみ）。メッセージが無ければ何もしない。</summary>
        public void ShowDepletionNotice(List<string> messages)
        {
            if (messages == null || messages.Count == 0) return;
            if (noticePanel == null || noticeText == null)
            {
                Debug.LogWarning("[GemLifecycleUI] noticePanel/noticeText not assigned.");
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine("以下のジェムは使用回数が0になり、消滅しました。");
            foreach (var m in messages) sb.AppendLine($"・{m}");

            noticeText.text = sb.ToString().TrimEnd();
            noticePanel.transform.SetAsLastSibling();
            noticePanel.SetActive(true);
        }

        private void HideNotice()
        {
            if (noticePanel != null) noticePanel.SetActive(false);
        }

        /// <summary>
        /// 装備中で残り1回のジェムを一覧表示し、はい/いいえで確認する。
        /// 対象が無い、またはパネル未設定の場合は確認を挟まずそのまま onYes を呼ぶ。
        /// </summary>
        public void ShowPreLaunchConfirm(List<string> messages, Action onYes, Action onCancel = null)
        {
            if (messages == null || messages.Count == 0) { onYes?.Invoke(); return; }
            if (confirmPanel == null || confirmText == null)
            {
                Debug.LogWarning("[GemLifecycleUI] confirmPanel/confirmText not assigned.");
                onYes?.Invoke();
                return;
            }

            pendingOnYes = onYes;
            pendingOnCancel = onCancel;

            // ★対象ジェムの一覧は表示しない、固定の短いメッセージのみ
            confirmText.text = "今回のプレイで消失するジェムを装備してます。\nこのままプレイしますか？";
            confirmPanel.transform.SetAsLastSibling();
            confirmPanel.SetActive(true);
            PlayConfirmSE();

            if (confirmTextPulseCoroutine != null) StopCoroutine(confirmTextPulseCoroutine);
            confirmTextPulseCoroutine = StartCoroutine(ConfirmTextPulseLoop());
        }

        private void PlayConfirmSE()
        {
            if (confirmSE != null && audioSource != null) audioSource.PlayOneShot(confirmSE);
        }

        private void StopConfirmTextPulse()
        {
            if (confirmTextPulseCoroutine != null)
            {
                StopCoroutine(confirmTextPulseCoroutine);
                confirmTextPulseCoroutine = null;
            }
            if (confirmText != null)
            {
                confirmText.color = confirmTextOriginalColor;
                confirmText.transform.localScale = Vector3.one;
            }
        }

        // ★GemManagementUIのLowUsesWarningPulseLoopと同じ考え方：色の点滅＋拡大縮小パルスで強調する
        private IEnumerator ConfirmTextPulseLoop()
        {
            while (true)
            {
                float blinkT = (Mathf.Sin(Time.unscaledTime * confirmTextBlinkSpeed * Mathf.PI * 2f) + 1f) * 0.5f;
                confirmText.color = Color.Lerp(confirmTextOriginalColor, confirmTextBlinkColor, blinkT);

                float pulseT = Mathf.Sin(Time.unscaledTime * confirmTextPulseSpeed * Mathf.PI * 2f);
                float scale = 1f + pulseT * confirmTextPulseAmount;
                confirmText.transform.localScale = new Vector3(scale, scale, 1f);

                yield return null;
            }
        }

        private void OnConfirmYes()
        {
            StopConfirmTextPulse();
            if (confirmPanel != null) confirmPanel.SetActive(false);
            var cb = pendingOnYes;
            pendingOnYes = null;
            pendingOnCancel = null;
            cb?.Invoke();
        }

        private void OnConfirmNo()
        {
            // ★はいボタンはAreaSelectManager.LoadGameSceneWithSE()側で同じSEが鳴るが、
            //   いいえボタンはそのまま閉じるだけで何も鳴らないため、ここで明示的に鳴らす
            PlayConfirmSE();
            StopConfirmTextPulse();
            if (confirmPanel != null) confirmPanel.SetActive(false);
            var cb = pendingOnCancel;
            pendingOnYes = null;
            pendingOnCancel = null;
            cb?.Invoke();
        }

#if UNITY_EDITOR
        /// <summary>
        /// 確認パネル(GemPreLaunchConfirmPanel)の背景・はい/いいえボタンに、中断メニューで使っている
        /// 汎用素材(MainBg.png / YES.png / NO.png)を適用する。BuildPanel()と違い、既存オブジェクトの
        /// 画像・サイズだけを差し替える非破壊的な処理（再実行しても安全）。
        /// </summary>
        [ContextMenu("Apply Pause Menu Style To Confirm Panel (中断メニューの背景・YES/NO画像を適用)")]
        private void ApplyPauseMenuStyleToConfirmPanel()
        {
            var bg = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/中断画面/MainBg.png");
            var yes = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/中断画面/YES.png");
            var no = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/中断画面/NO.png");

            if (bg == null || yes == null || no == null)
            {
                Debug.LogError($"[GemLifecycleUI] 素材が見つかりません。MainBg={bg != null}, YES={yes != null}, NO={no != null}");
                return;
            }

            if (confirmBoxImage == null && confirmPanel != null)
            {
                var boxTf = confirmPanel.transform.Find("Box");
                if (boxTf != null) confirmBoxImage = boxTf.GetComponent<Image>();
            }
            if (confirmBoxImage != null)
            {
                confirmBoxImage.sprite = bg;
                confirmBoxImage.type = Image.Type.Simple;
                confirmBoxImage.color = Color.white;
            }
            else
            {
                Debug.LogWarning("[GemLifecycleUI] confirmBoxImageが見つかりませんでした。");
            }

            ApplyYesNoButtonStyle(confirmYesButton, yes);
            ApplyYesNoButtonStyle(confirmNoButton, no);

            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log("[GemLifecycleUI] 確認パネルに中断メニューのスタイルを適用しました。");
        }

        private void ApplyYesNoButtonStyle(Button button, Sprite sprite)
        {
            if (button == null) return;
            // ★YES.png/NO.pngは正方形キャンバス(450x450)の中央に横長のピルボタンが描かれた画像。
            //   ボタン本体(当たり判定、Layout Elementでサイズ管理)に直接貼ると、Preserve Aspectが
            //   正方形全体を当たり判定の「高さ」に合わせて縮小してしまい、実際のピル部分が
            //   ごく小さく見えてしまう。中断メニューのYes/Noボタンと同じく、当たり判定より
            //   大きい正方形の子オブジェクト(Bg)に画像を持たせる構成にする。
            var rootImg = button.GetComponent<Image>();
            if (rootImg != null) rootImg.color = new Color(1f, 1f, 1f, 0f); // 当たり判定自体は透明化

            var bgTf = button.transform.Find("Bg");
            GameObject bgGo = bgTf != null ? bgTf.gameObject : new GameObject("Bg", typeof(RectTransform), typeof(Image));
            var bgRt = (RectTransform)bgGo.transform;
            bgRt.SetParent(button.transform, false);
            bgRt.SetAsFirstSibling(); // Textより奥に
            bgRt.anchorMin = bgRt.anchorMax = new Vector2(0.5f, 0.5f);
            bgRt.pivot = new Vector2(0.5f, 0.5f);
            bgRt.sizeDelta = new Vector2(450f, 450f);

            var bgImg = bgGo.GetComponent<Image>();
            bgImg.sprite = sprite;
            bgImg.type = Image.Type.Simple;
            bgImg.color = Color.white;
            bgImg.preserveAspect = true;
            bgImg.raycastTarget = false;

            // ★YES.png/NO.pngには文字が焼き込まれているため、既存の「はい/いいえ」テキストは
            //   二重表示を避けるため非表示にする。
            var textTf = button.transform.Find("Text");
            if (textTf != null) textTf.gameObject.SetActive(false);
        }

        /// <summary>
        /// はい/いいえボタンに、中断メニュー(エリアセレクトに戻りますか？)のYes/Noボタンと
        /// 全く同じホバーエフェクト(拡大1.05倍・カーソル移動2.mp3・グレー点滅)を適用する。
        /// ButtonHoverEffectが未追加なら追加し、既存なら値を上書きする（再実行しても安全）。
        /// </summary>
        [ContextMenu("Apply Pause Menu Hover Effect To Yes/No Buttons (中断メニューと同じホバーSE・拡大・点滅を適用)")]
        private void ApplyPauseMenuHoverEffectToConfirmButtons()
        {
            var hoverSE = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/GEM/カーソル移動2.mp3");
            if (hoverSE == null)
            {
                Debug.LogError("[GemLifecycleUI] hoverSE(Assets/Audio/GEM/カーソル移動2.mp3)が見つかりません。");
                return;
            }

            ApplyHoverEffectToButton(confirmYesButton, hoverSE);
            ApplyHoverEffectToButton(confirmNoButton, hoverSE);

            Debug.Log("[GemLifecycleUI] はい/いいえボタンに中断メニューと同じホバーエフェクト(拡大・SE・点滅)を適用しました。");
        }

        private void ApplyHoverEffectToButton(Button button, AudioClip hoverSE)
        {
            if (button == null) return;

            // ★点滅対象は当たり判定(透明化済み)ではなく、YES.png/NO.pngを表示している"Bg"子。
            var bgTf = button.transform.Find("Bg");
            if (bgTf == null)
            {
                Debug.LogWarning($"[GemLifecycleUI] {button.name}にBgが見つかりません。先に「Apply Pause Menu Style To Confirm Panel」を実行してください。");
                return;
            }
            var bgImg = bgTf.GetComponent<Image>();

            var hover = button.GetComponent<ButtonHoverEffect>();
            if (hover == null) hover = button.gameObject.AddComponent<ButtonHoverEffect>();

            // ★ButtonHoverEffectのフィールドは全てprivateのため、SerializedObject経由で設定する。
            //   中断メニューのYesButton/NoButtonに実際に設定されている値と完全に一致させる。
            var so = new UnityEditor.SerializedObject(hover);
            so.FindProperty("hoverScale").floatValue = 1.05f;
            so.FindProperty("hoverScaleDuration").floatValue = 0.1f;
            so.FindProperty("hoverSE").objectReferenceValue = hoverSE;
            so.FindProperty("hoverSEVolume").floatValue = 1f;
            so.FindProperty("blinkTarget").objectReferenceValue = bgImg;
            so.FindProperty("blinkSpeed").floatValue = 1f;
            so.FindProperty("blinkColor").colorValue = new Color(0.39215687f, 0.39215687f, 0.39215687f, 1f);
            so.FindProperty("blinkIntensity").floatValue = 0.8f;
            so.FindProperty("requireInteractable").boolValue = false;
            so.ApplyModifiedProperties();

            UnityEditor.EditorUtility.SetDirty(button.gameObject);
        }

        /// <summary>
        /// 警告パネル表示時・いいえボタン押下時に鳴らすSEに、Areaノードクリック音
        /// （AreaSelectManagerのbuttonClickSEと同じ「決定ボタン.mp3」）を設定する。
        /// </summary>
        [ContextMenu("Assign Confirm SE (Areaノードクリック音と同じSEを設定)")]
        private void AssignConfirmSE()
        {
            var clip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Title/決定ボタン.mp3");
            if (clip == null)
            {
                Debug.LogError("[GemLifecycleUI] SE(Assets/Audio/Title/決定ボタン.mp3)が見つかりません。");
                return;
            }

            confirmSE = clip;
            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log("[GemLifecycleUI] confirmSEに決定ボタン.mp3を設定しました。");
        }

        /// <summary>
        /// メッセージが固定の短い2行文になったため、ContentSizeFitterによる動的な自動調整はやめて、
        /// この文言に合う固定サイズを直接設定する（Box/本文テキスト/はい・いいえボタン）。
        /// 既存オブジェクトの数値を直接書き換えるだけの非破壊的な処理（再実行しても安全）。
        /// </summary>
        [ContextMenu("Fix Confirm Panel Sizes (枠・本文・ボタンのサイズを固定文言に合わせて調整)")]
        private void FixConfirmPanelSizes()
        {
            if (confirmPanel == null) { Debug.LogWarning("[GemLifecycleUI] confirmPanelが未設定です。"); return; }

            var boxTf = confirmPanel.transform.Find("Box") as RectTransform;
            if (boxTf == null) { Debug.LogWarning("[GemLifecycleUI] Boxが見つかりませんでした。"); return; }

            // ★動的自動調整(ContentSizeFitter)は不安定だったため外し、固定文言に合わせた固定サイズにする
            var oldBoxFitter = boxTf.GetComponent<ContentSizeFitter>();
            if (oldBoxFitter != null) DestroyImmediate(oldBoxFitter);
            boxTf.sizeDelta = new Vector2(1100f, 420f);

            if (confirmText != null)
            {
                var oldTextFitter = confirmText.GetComponent<ContentSizeFitter>();
                if (oldTextFitter != null) DestroyImmediate(oldTextFitter);

                var textLE = confirmText.GetComponent<LayoutElement>();
                if (textLE != null) textLE.preferredHeight = 160f;
                confirmText.alignment = TextAlignmentOptions.Center;
                confirmText.fontSize = 40f;
            }

            SetButtonLayoutSize(confirmYesButton, 220f, 90f);
            SetButtonLayoutSize(confirmNoButton, 220f, 90f);

            UnityEditor.EditorUtility.SetDirty(confirmPanel);
            Debug.Log("[GemLifecycleUI] Box(900x320)・本文・ボタン(220x90)のサイズを固定しました。");
        }

        private void SetButtonLayoutSize(Button button, float width, float height)
        {
            if (button == null) return;
            var le = button.GetComponent<LayoutElement>();
            if (le == null) le = button.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = width;
            le.preferredHeight = height;
        }

        [ContextMenu("Setup Gem Lifecycle UI")]
        private void SetupUI()
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null) { Debug.LogError("[GemLifecycleUI] Canvas not found!"); return; }

            noticePanel = BuildPanel(canvas.transform, "GemDepletionNoticePanel", out noticeText,
                new[] { "OK" }, out var noticeButtons);
            noticeOkButton = noticeButtons[0];

            confirmPanel = BuildPanel(canvas.transform, "GemPreLaunchConfirmPanel", out confirmText,
                new[] { "はい", "いいえ" }, out var confirmButtons);
            confirmYesButton = confirmButtons[0];
            confirmNoButton = confirmButtons[1];

            noticePanel.SetActive(false);
            confirmPanel.SetActive(false);

            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log("[GemLifecycleUI] Setup complete! ボタンのonClickは自動配線済みです。");
        }

        private GameObject BuildPanel(Transform canvasTransform, string panelName, out TextMeshProUGUI text, string[] buttonLabels, out Button[] buttons)
        {
            var existing = canvasTransform.Find(panelName);
            if (existing != null) DestroyImmediate(existing.gameObject);

            var panelObj = new GameObject(panelName);
            panelObj.transform.SetParent(canvasTransform, false);
            var panelRect = panelObj.AddComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.sizeDelta = Vector2.zero;

            var dimImg = panelObj.AddComponent<Image>();
            dimImg.color = new Color(0f, 0f, 0f, 0.75f);

            var boxObj = new GameObject("Box");
            boxObj.transform.SetParent(panelObj.transform, false);
            var boxRect = boxObj.AddComponent<RectTransform>();
            boxRect.anchorMin = new Vector2(0.5f, 0.5f);
            boxRect.anchorMax = new Vector2(0.5f, 0.5f);
            boxRect.sizeDelta = new Vector2(700f, 420f);
            boxRect.anchoredPosition = Vector2.zero;
            var boxImg = boxObj.AddComponent<Image>();
            boxImg.color = new Color(0.1f, 0.1f, 0.12f, 0.97f);

            var boxLayout = boxObj.AddComponent<VerticalLayoutGroup>();
            boxLayout.padding = new RectOffset(30, 30, 30, 30);
            boxLayout.spacing = 20f;
            boxLayout.childAlignment = TextAnchor.UpperCenter;
            boxLayout.childControlWidth = true;
            boxLayout.childControlHeight = false;
            boxLayout.childForceExpandWidth = true;
            boxLayout.childForceExpandHeight = false;

            TMP_FontAsset fontAsset = UnityEditor.AssetDatabase.FindAssets("t:TMP_FontAsset NotoSansJP-Regular")
                .Select(guid => UnityEditor.AssetDatabase.GUIDToAssetPath(guid))
                .Select(path => UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path))
                .FirstOrDefault();

            var textObj = new GameObject("MessageText");
            textObj.transform.SetParent(boxObj.transform, false);
            text = textObj.AddComponent<TextMeshProUGUI>();
            text.fontSize = 26f;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.TopLeft;
            text.enableWordWrapping = true;
            if (fontAsset != null) text.font = fontAsset;
            var textLE = textObj.AddComponent<LayoutElement>();
            textLE.preferredHeight = 280f;
            textLE.flexibleWidth = 1f;

            var btnRowObj = new GameObject("ButtonRow");
            btnRowObj.transform.SetParent(boxObj.transform, false);
            var btnRowLayout = btnRowObj.AddComponent<HorizontalLayoutGroup>();
            btnRowLayout.spacing = 20f;
            btnRowLayout.childAlignment = TextAnchor.MiddleCenter;
            btnRowLayout.childControlWidth = true;
            btnRowLayout.childControlHeight = true;
            btnRowLayout.childForceExpandWidth = false;
            btnRowLayout.childForceExpandHeight = false;
            var btnRowLE = btnRowObj.AddComponent<LayoutElement>();
            btnRowLE.preferredHeight = 60f;

            buttons = new Button[buttonLabels.Length];
            for (int i = 0; i < buttonLabels.Length; i++)
            {
                var btnObj = new GameObject($"Button_{buttonLabels[i]}");
                btnObj.transform.SetParent(btnRowObj.transform, false);
                var btnImg = btnObj.AddComponent<Image>();
                btnImg.color = new Color(0.25f, 0.25f, 0.35f, 1f);
                var btn = btnObj.AddComponent<Button>();
                var btnLE = btnObj.AddComponent<LayoutElement>();
                btnLE.preferredWidth = 180f;
                btnLE.preferredHeight = 60f;

                var btnTextObj = new GameObject("Text");
                btnTextObj.transform.SetParent(btnObj.transform, false);
                var btnTextRect = btnTextObj.AddComponent<RectTransform>();
                btnTextRect.anchorMin = Vector2.zero;
                btnTextRect.anchorMax = Vector2.one;
                btnTextRect.sizeDelta = Vector2.zero;
                var btnText = btnTextObj.AddComponent<TextMeshProUGUI>();
                btnText.text = buttonLabels[i];
                btnText.fontSize = 24f;
                btnText.alignment = TextAlignmentOptions.Center;
                btnText.color = Color.white;
                if (fontAsset != null) btnText.font = fontAsset;

                buttons[i] = btn;
            }

            return panelObj;
        }
#endif
    }
}
