using System;
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

        private Action pendingOnYes;
        private Action pendingOnCancel;

        private void Awake()
        {
            if (noticeOkButton != null) noticeOkButton.onClick.AddListener(HideNotice);
            if (confirmYesButton != null) confirmYesButton.onClick.AddListener(OnConfirmYes);
            if (confirmNoButton != null) confirmNoButton.onClick.AddListener(OnConfirmNo);

            if (noticePanel != null) noticePanel.SetActive(false);
            if (confirmPanel != null) confirmPanel.SetActive(false);
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

            var sb = new StringBuilder();
            sb.AppendLine("装備中の以下のジェムは、このプレイで消滅します。");
            foreach (var m in messages) sb.AppendLine($"・{m}");
            sb.AppendLine();
            sb.Append("プレイしますか？");

            confirmText.text = sb.ToString();
            confirmPanel.transform.SetAsLastSibling();
            confirmPanel.SetActive(true);
        }

        private void OnConfirmYes()
        {
            if (confirmPanel != null) confirmPanel.SetActive(false);
            var cb = pendingOnYes;
            pendingOnYes = null;
            pendingOnCancel = null;
            cb?.Invoke();
        }

        private void OnConfirmNo()
        {
            if (confirmPanel != null) confirmPanel.SetActive(false);
            var cb = pendingOnCancel;
            pendingOnYes = null;
            pendingOnCancel = null;
            cb?.Invoke();
        }

#if UNITY_EDITOR
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
