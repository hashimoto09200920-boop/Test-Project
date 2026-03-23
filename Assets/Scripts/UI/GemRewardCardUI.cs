using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using Game.Gems;
using Game.Skills;
using Game.Progress;

/// <summary>
/// GemRewardUI で表示する1枚のジェムカードUI
/// Phase1（選択）・Phase2（結果）両方で使用する
/// </summary>
public class GemRewardCardUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler
{
    public enum CardState { Normal, Selected, Dimmed, ResultSelected, ResultUnselected }

    [Header("References")]
    [SerializeField] private Image cardBgImage;
    [SerializeField] private TextMeshProUGUI gemNameText;
    [SerializeField] private Transform skillContainer;

    [Header("Entrance Settings")]
    [SerializeField] private float entranceStartScale = 0.8f;

    [Header("Hover Settings")]
    [SerializeField] private float hoverScale    = 1.05f;
    [SerializeField] private float hoverDuration = 0.08f;
    [SerializeField] private AudioClip hoverSE;

    [Header("Glow")]
    [SerializeField] private Image glowImage;
    [SerializeField] private float glowPulseMin      = 0.15f;
    [SerializeField] private float glowPulseMax      = 0.30f;
    [SerializeField] private float glowPulseDuration = 1.2f;

    [Header("Selected Blink")]
    [SerializeField] private float blinkInterval = 0.3f;
    [SerializeField] private float blinkAlpha    = 0.4f;

    [Header("Gem Icon Settings")]
    [SerializeField] private float gemIconSize = 160f;

    [Header("Skill Row Settings")]
    [SerializeField] private float skillIconSize = 48f;
    [SerializeField] private float skillRowHeight = 60f;
    [SerializeField] private float skillFontSize = 22f;
    [SerializeField] private Color skillNameColor = Color.white;

    [Header("State Colors")]
    [SerializeField] private Color normalBgColor            = new Color(0.15f, 0.15f, 0.25f, 1f);
    [SerializeField] private Color selectedBgColor          = new Color(0.25f, 0.38f, 0.55f, 1f);
    [SerializeField] private Color dimmedBgColor            = new Color(0.07f, 0.07f, 0.12f, 1f);
    [SerializeField] private Color resultSelectedBgColor    = new Color(0.25f, 0.38f, 0.55f, 1f);
    [SerializeField] private Color resultUnselectedBgColor  = new Color(0.10f, 0.10f, 0.15f, 1f);

    private CanvasGroup canvasGroup;
    private Image gemIconImage;
    private Coroutine blinkCoroutine;
    private Coroutine scaleCoroutine;
    private Coroutine hoverSECoroutine;
    private AudioSource audioSource;
    private bool tapDetected = false;

    /// <summary>true の間だけホバー拡大が有効（Phase1選択中のみ）</summary>
    public bool HoverEnabled { get; set; }

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
        }
    }

    /// <summary>
    /// ジェムデータをカードに反映する
    /// showSkills=false のとき: スキル行を非表示にしてジェムアイコンを大きく表示
    /// </summary>
    public void Setup(GemInstance gem, GemDefinition def, SkillDefinition bonus1, SkillDefinition bonus2, bool showSkills = true)
    {
        // 既存スキル行を削除
        if (skillContainer != null)
            foreach (Transform child in skillContainer)
                Destroy(child.gameObject);

        // 既存のジェムアイコンを削除
        if (gemIconImage != null)
        {
            Destroy(gemIconImage.gameObject);
            gemIconImage = null;
        }

        if (gemNameText != null)
            gemNameText.text = def != null ? def.gemName : "";

        if (def == null) return;

        if (showSkills)
        {
            if (skillContainer != null)
                skillContainer.gameObject.SetActive(true);

            if (def.baseSkill != null) CreateSkillRow(def.baseSkill);
            if (bonus1 != null)        CreateSkillRow(bonus1);
            if (bonus2 != null)        CreateSkillRow(bonus2);
        }
        else
        {
            if (skillContainer != null)
                skillContainer.gameObject.SetActive(false);

            if (def.icon != null)
            {
                var iconObj = new GameObject("GemIcon");
                iconObj.transform.SetParent(transform, false);
                var rt = iconObj.AddComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot     = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(0f, -30f);
                rt.sizeDelta = new Vector2(gemIconSize, gemIconSize);
                gemIconImage = iconObj.AddComponent<Image>();
                gemIconImage.sprite = def.icon;
                gemIconImage.preserveAspect = true;
            }
        }
    }

    private void CreateSkillRow(SkillDefinition skill)
    {
        var row = new GameObject("SkillRow");
        row.transform.SetParent(skillContainer, false);

        var hlg = row.AddComponent<HorizontalLayoutGroup>();
        hlg.childAlignment       = TextAnchor.MiddleLeft;
        hlg.childControlWidth    = false;
        hlg.childControlHeight   = false;
        hlg.childForceExpandWidth  = false;
        hlg.childForceExpandHeight = false;
        hlg.spacing = 8f;

        var le = row.AddComponent<LayoutElement>();
        le.preferredHeight = skillRowHeight;
        le.flexibleWidth   = 1f;

        // アイコン
        var iconObj  = new GameObject("Icon");
        iconObj.transform.SetParent(row.transform, false);
        var iconRect = iconObj.AddComponent<RectTransform>();
        iconRect.sizeDelta = new Vector2(skillIconSize, skillIconSize);
        var iconImg = iconObj.AddComponent<Image>();
        iconImg.preserveAspect = true;
        if (skill.icon != null) iconImg.sprite = skill.icon;

        // スキル名
        var nameObj  = new GameObject("SkillName");
        nameObj.transform.SetParent(row.transform, false);
        var nameRect = nameObj.AddComponent<RectTransform>();
        nameRect.sizeDelta = new Vector2(220f, skillRowHeight);
        var tmp = nameObj.AddComponent<TextMeshProUGUI>();
        tmp.text               = skill.skillName;
        tmp.fontSize           = skillFontSize;
        tmp.color              = skillNameColor;
        tmp.alignment          = TextAlignmentOptions.MidlineLeft;
        tmp.enableWordWrapping = false;
    }

    public void SetState(CardState state)
    {
        if (cardBgImage != null)
        {
            cardBgImage.color = state switch
            {
                CardState.Normal            => normalBgColor,
                CardState.Selected          => selectedBgColor,
                CardState.Dimmed            => dimmedBgColor,
                CardState.ResultSelected    => resultSelectedBgColor,
                CardState.ResultUnselected  => resultUnselectedBgColor,
                _                           => normalBgColor,
            };
        }

        if (canvasGroup != null)
            canvasGroup.alpha = (state == CardState.Dimmed) ? 0.45f : 1f;
    }

    public void SetAlpha(float alpha)
    {
        if (canvasGroup != null) canvasGroup.alpha = alpha;
    }

    // =====================================================
    // Entrance / Exit
    // =====================================================

    /// <summary>entranceStartScale + alpha0 から1.0へ ease-out で登場する</summary>
    public IEnumerator EntranceCoroutine(float delay, float duration = 0.25f)
    {
        transform.localScale = Vector3.one * entranceStartScale;
        if (canvasGroup != null) canvasGroup.alpha = 0f;

        if (delay > 0f) yield return new WaitForSecondsRealtime(delay);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float e = 1f - Mathf.Pow(1f - t, 3f); // ease-out cubic
            transform.localScale          = Vector3.Lerp(Vector3.one * entranceStartScale, Vector3.one, e);
            if (canvasGroup != null) canvasGroup.alpha = e;
            yield return null;
        }
        transform.localScale = Vector3.one;
        if (canvasGroup != null) canvasGroup.alpha = 1f;

    }

    /// <summary>スケール + alpha を Dimmed 状態までアニメーションで縮める</summary>
    public IEnumerator DimExitCoroutine(float duration = 0.2f)
    {
        float elapsed    = 0f;
        float startAlpha = canvasGroup != null ? canvasGroup.alpha : 1f;
        const float targetAlpha = 0.45f;
        Vector3 startScale = transform.localScale;
        Vector3 targetScale = Vector3.one * 0.9f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            if (canvasGroup != null)
                canvasGroup.alpha      = Mathf.Lerp(startAlpha, targetAlpha, t);
            transform.localScale   = Vector3.Lerp(startScale, targetScale, t);
            yield return null;
        }
        if (canvasGroup != null) canvasGroup.alpha = targetAlpha;
        transform.localScale = targetScale;
    }

    // =====================================================
    // Glow
    // =====================================================

    /// <summary>グローの表示を開始しパルスアニメを起動する</summary>
    public void StartGlow()
    {
        if (glowImage == null) return;
        glowImage.gameObject.SetActive(true);
        StartCoroutine(GlowPulseCoroutine());
    }

    public void StopGlow()
    {
        if (glowImage == null) return;
        StopAllCoroutines();
        glowImage.gameObject.SetActive(false);
    }

    private IEnumerator GlowPulseCoroutine()
    {
        while (true)
        {
            float elapsed = 0f;
            while (elapsed < glowPulseDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / glowPulseDuration;
                float alpha = Mathf.Lerp(glowPulseMin, glowPulseMax, Mathf.Sin(t * Mathf.PI));
                var c = glowImage.color;
                c.a = alpha;
                glowImage.color = c;
                yield return null;
            }
        }
    }

    // =====================================================
    // Selected Blink
    // =====================================================

    public void StartBlink()
    {
        if (blinkCoroutine != null) StopCoroutine(blinkCoroutine);
        blinkCoroutine = StartCoroutine(BlinkCoroutine());
    }

    public void StopBlink()
    {
        if (blinkCoroutine != null)
        {
            StopCoroutine(blinkCoroutine);
            blinkCoroutine = null;
        }
        if (canvasGroup != null) canvasGroup.alpha = 1f;
    }

    private IEnumerator BlinkCoroutine()
    {
        while (true)
        {
            if (canvasGroup != null) canvasGroup.alpha = blinkAlpha;
            yield return new WaitForSecondsRealtime(blinkInterval);
            if (canvasGroup != null) canvasGroup.alpha = 1f;
            yield return new WaitForSecondsRealtime(blinkInterval);
        }
    }

    // =====================================================
    // Bounce
    // =====================================================

    /// <summary>フェードイン完了後に呼ぶ。スケールをオーバーシュートさせてから元に戻す</summary>
    public IEnumerator BounceCoroutine(float overshoot = 1.12f, float duration = 0.25f)
    {
        // 0 → overshoot → 1.0
        float half = duration * 0.5f;

        float elapsed = 0f;
        while (elapsed < half)
        {
            elapsed += Time.unscaledDeltaTime;
            transform.localScale = Vector3.one * Mathf.Lerp(1f, overshoot, elapsed / half);
            yield return null;
        }
        elapsed = 0f;
        while (elapsed < half)
        {
            elapsed += Time.unscaledDeltaTime;
            transform.localScale = Vector3.one * Mathf.Lerp(overshoot, 1f, elapsed / half);
            yield return null;
        }
        transform.localScale = Vector3.one;
    }

    // =====================================================
    // Hover Scale
    // =====================================================

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!HoverEnabled) return;
        if (canvasGroup != null && canvasGroup.alpha < 1f) return; // Entrance中は無視
        tapDetected = false;
        ScaleTo(hoverScale);
        // SE再生（1フレーム後に実行。同フレームにOnPointerDownが来た場合＝タップとしてスキップ）
        if (hoverSE != null && audioSource != null)
        {
            if (hoverSECoroutine != null) StopCoroutine(hoverSECoroutine);
            hoverSECoroutine = StartCoroutine(PlayHoverSEDelayed());
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        tapDetected = true;
    }

    public void OnPointerExit(PointerEventData _)
    {
        if (!HoverEnabled) return;
        tapDetected = false;
        if (hoverSECoroutine != null)
        {
            StopCoroutine(hoverSECoroutine);
            hoverSECoroutine = null;
        }
        ScaleTo(1f);
    }

    private IEnumerator PlayHoverSEDelayed()
    {
        yield return null; // 1フレーム待つ（同フレームのOnPointerDownを検知するため）
        if (!tapDetected)
        {
            float vol = SoundSettingsManager.Instance != null ? SoundSettingsManager.Instance.SEVolume : 1f;
            audioSource.PlayOneShot(hoverSE, vol);
        }
        hoverSECoroutine = null;
    }

    /// <summary>スケールを元に戻す（ホバー無効化時にも呼ぶ）</summary>
    public void ResetScale()
    {
        ScaleTo(1f);
    }

    private void ScaleTo(float target)
    {
        if (scaleCoroutine != null) StopCoroutine(scaleCoroutine);
        scaleCoroutine = StartCoroutine(ScaleCoroutine(target));
    }

    private IEnumerator ScaleCoroutine(float target)
    {
        float elapsed  = 0f;
        Vector3 from   = transform.localScale;
        Vector3 to     = Vector3.one * target;
        while (elapsed < hoverDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            transform.localScale = Vector3.Lerp(from, to, elapsed / hoverDuration);
            yield return null;
        }
        transform.localScale = to;
    }
}
