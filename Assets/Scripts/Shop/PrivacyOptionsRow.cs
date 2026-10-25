using System.Collections;
using Game.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Shop
{
    /// <summary>
    /// タイトルのサウンド設定パネルの一番下の行（戻るボタン｜プライバシー設定ボタン）（2026/10/24）。
    /// 部品はメニュー「Tools/タイトル画面/サウンド設定にプライバシー設定のボタンを追加（01_Title）」で作る。
    /// ・ヨーロッパの利用者など、広告の同意をあとから変える画面が必要な時だけ、戻るボタンを小さくして右にプライバシー設定のボタンを並べる
    /// ・それ以外の利用者には、今までどおりの戻るボタンだけを出す
    /// ボタンの大きさは「入れ物（Slot）」の拡大率で変える（ボタン自身の大きさを変えると、ホバー拡大の元の大きさがずれるため）。
    /// </summary>
    [ExecuteAlways]
    public class PrivacyOptionsRow : MonoBehaviour
    {
        [SerializeField] private RectTransform backSlot;
        [SerializeField] private RectTransform privacySlot;
        [SerializeField] private Button privacyButton;
        [Tooltip("2つ並べる時のボタンの大きさ（元の戻るボタンに対する倍率）")]
        [Range(0.2f, 1f)] [SerializeField] private float pairScale = 0.5f;
        [Tooltip("2つ並べる時の、行の中央から各ボタンの中央までの距離")]
        [SerializeField] private float pairOffset = 170f;
        [Tooltip("エディタで、2つ並んだ時の見た目を Play 前に確認する（ゲームの動きには影響しない）")]
        [SerializeField] private bool previewPairInEditor = false;

        [Header("プライバシー設定を開けなかった時の表示")]
        [Tooltip("「プライバシー設定を開けませんでした…」を出す板（ふだんは非表示）")]
        [SerializeField] private GameObject failMessage;
        [SerializeField] private TextMeshProUGUI failMessageText;
        [Tooltip("表示しておく秒数")]
        [Min(0.5f)] [SerializeField] private float failMessageSeconds = 3f;
        [Tooltip("エディタで、開けなかった時の表示を Play 前に確認する")]
        [SerializeField] private bool previewFailMessageInEditor = false;

        private Coroutine failRoutine;

        private void OnEnable()
        {
            if (Application.isPlaying)
            {
                AdConsentManager.OnReady += Refresh;
                if (privacyButton != null) { privacyButton.onClick.RemoveListener(OnClickPrivacy); privacyButton.onClick.AddListener(OnClickPrivacy); }
            }
            Refresh();
        }

        private void OnDisable()
        {
            if (Application.isPlaying) AdConsentManager.OnReady -= Refresh;
            if (failRoutine != null) { StopCoroutine(failRoutine); failRoutine = null; }
            if (Application.isPlaying && failMessage != null) failMessage.SetActive(false);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!Application.isPlaying) UnityEditor.EditorApplication.delayCall += () => { if (this != null) Refresh(); };
        }
#endif

        private void Refresh()
        {
            if (failMessage != null && !Application.isPlaying)
            {
                failMessage.SetActive(previewFailMessageInEditor);
                if (failMessageText != null) failMessageText.text = FailText();
            }
            bool pair = Application.isPlaying ? AdConsentManager.PrivacyOptionsRequired : previewPairInEditor;
            if (privacySlot != null)
            {
                privacySlot.gameObject.SetActive(pair);
                privacySlot.localScale = Vector3.one * pairScale;
                privacySlot.anchoredPosition = new Vector2(pairOffset, 0f);
            }
            if (backSlot != null)
            {
                backSlot.localScale = pair ? Vector3.one * pairScale : Vector3.one;
                backSlot.anchoredPosition = pair ? new Vector2(-pairOffset, 0f) : Vector2.zero;
            }
        }

        private void OnClickPrivacy()
        {
            if (AdConsentManager.Instance == null) { ShowFail(); return; }
            AdConsentManager.Instance.ShowPrivacyOptions(ok => { if (!ok) ShowFail(); });
        }

        private static string FailText() => LocalizationManager.GetStatic("privacy.openFailed", "プライバシー設定を開けませんでした。\n通信環境を確認してください");

        // 画面を出せなかった時：押しても何も起きないように見えないよう、短い案内を数秒だけ出す
        private void ShowFail()
        {
            if (failMessage == null || !isActiveAndEnabled) return;
            if (failRoutine != null) StopCoroutine(failRoutine);
            failRoutine = StartCoroutine(FailRoutine());
        }

        private IEnumerator FailRoutine()
        {
            if (failMessageText != null) failMessageText.text = FailText();
            failMessage.SetActive(true);
            yield return new WaitForSecondsRealtime(failMessageSeconds);
            failMessage.SetActive(false);
            failRoutine = null;
        }
    }
}
