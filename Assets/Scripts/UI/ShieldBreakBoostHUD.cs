using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Game.Skills;

/// <summary>
/// B7「ブレイクダメージ」スキルのブースト中HUD表示。
/// シールド破壊後の一定時間、アイコン＋残り時間テキストを画面左上に表示する。
///
/// 使い方:
/// 1. HierarchyでCanvasの適切な位置にGameObjectを作成（ゴールドアイコンの下）
/// 2. このコンポーネントをアタッチ
/// 3. BoostIcon（Image）とBoostText（TextMeshProUGUI）の子オブジェクトをInspectorで参照設定
/// </summary>
public class ShieldBreakBoostHUD : MonoBehaviour
{
    [Header("References")]
    [Tooltip("アイコン表示用Image")]
    [SerializeField] private Image boostIcon;
    [Tooltip("テキスト表示用TextMeshProUGUI")]
    [SerializeField] private TextMeshProUGUI boostText;

    [Header("Icon Settings")]
    [Tooltip("アイコンのサイズ（RectTransform.sizeDelta）")]
    [SerializeField] private Vector2 iconSize = new Vector2(40f, 40f);
    [Tooltip("アイコンの位置オフセット（RectTransform.anchoredPosition）")]
    [SerializeField] private Vector2 iconOffset = new Vector2(0f, 0f);

    [Header("Text Settings")]
    [Tooltip("テキストのフォントサイズ")]
    [SerializeField] private float textFontSize = 20f;
    [Tooltip("テキストの位置オフセット（RectTransform.anchoredPosition）")]
    [SerializeField] private Vector2 textOffset = new Vector2(48f, 0f);
    [Tooltip("テキストの表示フォーマット。{0}=残り秒数、{1}=倍率")]
    [SerializeField] private string textFormat = "BREAK ×{1:F1}  {0:F1}s";
    [Tooltip("テキストの色")]
    [SerializeField] private Color textColor = new Color(1f, 0.5f, 0.1f, 1f);

    [Header("Animation")]
    [Tooltip("表示時のフェードイン時間（秒）")]
    [SerializeField] private float fadeInDuration = 0.15f;
    [Tooltip("残り時間がこの秒数以下になると点滅する")]
    [SerializeField] private float blinkThreshold = 2f;
    [Tooltip("点滅速度（Hz）")]
    [SerializeField] private float blinkSpeed = 4f;

    private CanvasGroup canvasGroup;
    private RectTransform iconRect;
    private RectTransform textRect;
    private float currentAlpha = 0f;
    private bool wasActiveLastFrame = false;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        if (boostIcon != null)  iconRect = boostIcon.GetComponent<RectTransform>();
        if (boostText != null)  textRect = boostText.GetComponent<RectTransform>();

        ApplyInspectorSettings();
        canvasGroup.alpha = 0f;
        gameObject.SetActive(true);
    }

    private void Update()
    {
        if (SkillManager.Instance == null)
        {
            canvasGroup.alpha = 0f;
            return;
        }

        bool isActive = SkillManager.Instance.IsShieldBreakBoostActive;
        float remaining = SkillManager.Instance.ShieldBreakBoostTimeRemaining;
        float multiplier = SkillManager.Instance.ShieldBreakBoostMultiplier;

        // フェードイン
        if (isActive)
        {
            if (!wasActiveLastFrame)
                currentAlpha = 0f;

            currentAlpha = Mathf.MoveTowards(currentAlpha, 1f, Time.deltaTime / Mathf.Max(0.001f, fadeInDuration));
        }
        else
        {
            currentAlpha = 0f;
        }
        wasActiveLastFrame = isActive;

        // 点滅（残り時間が少ない場合）
        float displayAlpha = currentAlpha;
        if (isActive && remaining <= blinkThreshold)
        {
            float blink = Mathf.Abs(Mathf.Sin(Time.time * blinkSpeed * Mathf.PI));
            displayAlpha *= Mathf.Lerp(0.3f, 1f, blink);
        }

        canvasGroup.alpha = displayAlpha;

        // テキスト更新
        if (isActive && boostText != null)
        {
            boostText.text = string.Format(textFormat, remaining, multiplier);
        }
    }

    private void ApplyInspectorSettings()
    {
        if (iconRect != null)
        {
            iconRect.sizeDelta = iconSize;
            iconRect.anchoredPosition = iconOffset;
        }
        if (textRect != null)
        {
            textRect.anchoredPosition = textOffset;
        }
        if (boostText != null)
        {
            boostText.fontSize = textFontSize;
            boostText.color = textColor;
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Inspector変更をリアルタイム反映
        if (!Application.isPlaying) return;
        if (iconRect == null && boostIcon != null) iconRect = boostIcon.GetComponent<RectTransform>();
        if (textRect == null && boostText != null) textRect  = boostText.GetComponent<RectTransform>();
        ApplyInspectorSettings();
    }
#endif
}
