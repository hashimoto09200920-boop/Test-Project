using System.Collections;
using System.Collections.Generic;
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
    [Tooltip("落下開始のY上方オフセット（px）")]
    [SerializeField] private float dropStartOffsetY = 600f;
    [Tooltip("落下にかかる時間（秒）")]
    [SerializeField] private float dropDuration = 0.35f;
    [Tooltip("バウンドの高さ（px）")]
    [SerializeField] private float bounceHeight = 30f;
    [Tooltip("バウンドにかかる時間（秒）")]
    [SerializeField] private float bounceDuration = 0.15f;

    [Header("Hover Settings")]
    [SerializeField] private float hoverScale    = 1.05f;
    [SerializeField] private float hoverDuration = 0.08f;
    [SerializeField] private AudioClip hoverSE;

    [Header("Glow")]
    [SerializeField] private Image glowImage;
    [SerializeField] private float glowPulseMin      = 0.15f;
    [SerializeField] private float glowPulseMax      = 0.30f;
    [SerializeField] private float glowPulseDuration = 1.2f;

    [Header("Chest Settings")]
    [Tooltip("閉じた宝箱のSprite")]
    [SerializeField] private Sprite chestClosedSprite;
    [Tooltip("開いた宝箱のSprite")]
    [SerializeField] private Sprite chestOpenSprite;
    [Tooltip("宝箱画像のサイズ（Width/Height）。Play前Inspectorで調整可。")]
    [SerializeField] private Vector2 chestImageSize = new Vector2(160f, 160f);
    private Image chestImage; // Setup()内で自動生成

    [Header("Chest Open SE")]
    [SerializeField] private AudioClip chestShakeSE;
    [SerializeField] private AudioClip chestGlowSE;
    [SerializeField] private AudioClip chestOpenSE;
    [SerializeField] private AudioClip chestFlashSE;
    [SerializeField] private AudioClip chestGemAppearSE;

    [Header("Chest Open Animation")]
    [Tooltip("揺れの横幅（px）")]
    [SerializeField] private float chestShakeAmount   = 10f;
    [Tooltip("揺れの時間（秒）")]
    [SerializeField] private float chestShakeDuration = 0.35f;
    [Tooltip("発光ビルドアップの時間（秒）")]
    [SerializeField] private float chestGlowDuration  = 0.4f;
    [Tooltip("開蓋後の静止時間（秒）")]
    [SerializeField] private float chestOpenHold      = 0.2f;
    [Tooltip("フラッシュの時間（秒）")]
    [SerializeField] private float chestFlashDuration = 0.25f;
    [Tooltip("Gem出現フェードインの時間（秒）")]
    [SerializeField] private float chestGemRevealDuration = 0.35f;

    [Header("Selected Blink")]
    [SerializeField] private float blinkInterval = 0.3f;
    [SerializeField] private float blinkAlpha    = 0.4f;

    [Header("Gem Icon Settings")]
    [SerializeField] private float gemIconSize = 160f;

    [Header("Skill Row Settings")]
    [SerializeField] private float skillIconSize  = 48f;
    [SerializeField] private float skillRowHeight = 60f;
    [SerializeField] private float skillFontSize  = 22f;
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
    private Dictionary<SkillDefinition, Vector2> iconSizeLookup;

    /// <summary>Phase2用アイコンサイズルックアップをセット（Setup()の前に呼ぶ）</summary>
    public void SetIconSizeLookup(Dictionary<SkillDefinition, Vector2> lookup)
    {
        iconSizeLookup = lookup;
    }

    /// <summary>true の間だけホバー拡大が有効（Phase1選択中のみ）</summary>
    public bool HoverEnabled { get; set; }

    /// <summary>EntranceCoroutine の落下+バウンド合計時間（秒）。ホバー有効化タイミング計算用。</summary>
    public float EntranceTotalDuration => dropDuration + bounceDuration;

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

        // 宝箱画像を自動生成して閉じた状態でセット
        if (chestClosedSprite != null)
        {
            var chestObj = new GameObject("ChestImage");
            chestObj.transform.SetParent(transform, false);
            var chestRt = chestObj.AddComponent<RectTransform>();
            chestRt.anchorMin = new Vector2(0.5f, 0.5f);
            chestRt.anchorMax = new Vector2(0.5f, 0.5f);
            chestRt.pivot = new Vector2(0.5f, 0.5f);
            chestRt.anchoredPosition = new Vector2(0f, -30f);
            chestRt.sizeDelta = chestImageSize;
            chestImage = chestObj.AddComponent<Image>();
            chestImage.sprite = chestClosedSprite;
            chestImage.preserveAspect = true;

            // ChestImage生成後にGemIconを非表示（宝箱開封まで隠す）
            if (gemIconImage != null)
                gemIconImage.gameObject.SetActive(false);
        }
    }

    private void CreateSkillRow(SkillDefinition skill)
    {
        var row = new GameObject("SkillRow");
        row.transform.SetParent(skillContainer, false);

        var hlg = row.AddComponent<HorizontalLayoutGroup>();
        hlg.childAlignment         = TextAnchor.MiddleLeft;
        hlg.childControlWidth      = true;
        hlg.childControlHeight     = true;
        hlg.childForceExpandWidth  = false;
        hlg.childForceExpandHeight = false;
        hlg.spacing = 8f;

        var le = row.AddComponent<LayoutElement>();
        le.preferredHeight = skillRowHeight;
        le.flexibleWidth   = 1f;

        // アイコンサイズ：skill.iconDisplaySize を優先（GemManagementUI・SkillHUDと同一）
        // ルックアップで上書きがあればそちらを使用
        Vector2 iconSize;
        Vector2 iconOffset;
        if (iconSizeLookup != null && iconSizeLookup.TryGetValue(skill, out var lookedUp) && lookedUp != Vector2.zero)
        {
            iconSize   = lookedUp;
            iconOffset = Vector2.zero;
        }
        else if (skill.iconDisplaySize != Vector2.zero)
        {
            iconSize   = skill.iconDisplaySize;
            iconOffset = skill.iconDisplayOffset;
        }
        else
        {
            iconSize   = new Vector2(skillIconSize, skillIconSize);
            iconOffset = Vector2.zero;
        }

        // IconContainer：HLGがサイズを読む器
        var iconContainer = new GameObject("IconContainer");
        iconContainer.transform.SetParent(row.transform, false);
        var containerLE = iconContainer.AddComponent<LayoutElement>();
        containerLE.preferredWidth  = iconSize.x;
        containerLE.preferredHeight = iconSize.y;

        // Icon：中心アンカーにして sizeDelta = 絶対サイズ（GemManagementUI と同パターン）
        var iconObj  = new GameObject("Icon");
        iconObj.transform.SetParent(iconContainer.transform, false);
        var iconRect = iconObj.AddComponent<RectTransform>();
        iconRect.anchorMin        = new Vector2(0.5f, 0.5f);
        iconRect.anchorMax        = new Vector2(0.5f, 0.5f);
        iconRect.pivot            = new Vector2(0.5f, 0.5f);
        iconRect.anchoredPosition = iconOffset;
        iconRect.sizeDelta        = iconSize;
        var iconImg = iconObj.AddComponent<Image>();
        iconImg.preserveAspect = true;
        if (skill.icon != null) iconImg.sprite = skill.icon;

        // スキル名
        var nameObj = new GameObject("SkillName");
        nameObj.transform.SetParent(row.transform, false);
        var nameLE = nameObj.AddComponent<LayoutElement>();
        nameLE.preferredWidth  = 220f;
        nameLE.preferredHeight = skillRowHeight;
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

    /// <summary>上から落下してバウンドして着地する演出</summary>
    public IEnumerator EntranceCoroutine(float delay, float duration = 0.25f)
    {
        var rt = GetComponent<RectTransform>();
        if (rt == null) yield break;

        // レイアウト確定まで非表示で待機
        transform.localScale = Vector3.one;
        if (canvasGroup != null) canvasGroup.alpha = 0f;

        // 最低1フレーム待ってレイアウトを確定させてから、追加delayを待つ
        yield return null;
        if (delay > 0f) yield return new WaitForSecondsRealtime(delay);

        // レイアウト確定後に着地位置を取得
        Vector2 landPos = rt.anchoredPosition;
        Vector2 startPos = landPos + new Vector2(0f, dropStartOffsetY);
        rt.anchoredPosition = startPos;

        if (canvasGroup != null) canvasGroup.alpha = 1f;

        // 落下（ease-in: 重力感）
        float elapsed = 0f;
        while (elapsed < dropDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / dropDuration);
            float e = t * t; // ease-in quad
            rt.anchoredPosition = Vector2.Lerp(startPos, landPos, e);
            yield return null;
        }
        rt.anchoredPosition = landPos;

        // バウンド（上に跳ねて戻る）
        Vector2 bouncePos = landPos + new Vector2(0f, bounceHeight);
        elapsed = 0f;
        float halfBounce = bounceDuration * 0.5f;
        while (elapsed < halfBounce)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / halfBounce);
            rt.anchoredPosition = Vector2.Lerp(landPos, bouncePos, t);
            yield return null;
        }
        elapsed = 0f;
        while (elapsed < halfBounce)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / halfBounce);
            rt.anchoredPosition = Vector2.Lerp(bouncePos, landPos, t);
            yield return null;
        }
        rt.anchoredPosition = landPos;
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
    // Chest Open
    // =====================================================

    /// <summary>宝箱開封演出（②揺れ→③発光→④開く→⑤フラッシュ→⑥Gem出現）</summary>
    public IEnumerator OpenChestCoroutine()
    {
        var rt = GetComponent<RectTransform>();

        // ② 揺れ
        PlayCardSE(chestShakeSE);
        if (rt != null)
        {
            Vector2 origin = rt.anchoredPosition;
            float elapsed = 0f;
            while (elapsed < chestShakeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float dampen = 1f - elapsed / chestShakeDuration;
                float offsetX = Mathf.Sin(elapsed * 55f) * chestShakeAmount * dampen;
                rt.anchoredPosition = origin + new Vector2(offsetX, 0f);
                yield return null;
            }
            rt.anchoredPosition = origin;
        }

        // ③ 発光
        PlayCardSE(chestGlowSE);
        if (glowImage != null)
        {
            glowImage.gameObject.SetActive(true);
            float elapsed = 0f;
            while (elapsed < chestGlowDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / chestGlowDuration);
                var c = glowImage.color;
                c.a = Mathf.Lerp(0f, 1f, t);
                glowImage.color = c;
                yield return null;
            }
        }

        // ④ 箱が開く
        PlayCardSE(chestOpenSE);
        if (chestImage != null && chestOpenSprite != null)
            chestImage.sprite = chestOpenSprite;
        yield return new WaitForSecondsRealtime(chestOpenHold);

        // ⑤ フラッシュ（カード上に白オーバーレイ）
        PlayCardSE(chestFlashSE);
        var flashObj = new GameObject("ChestFlash");
        flashObj.transform.SetParent(transform, false);
        var flashRt = flashObj.AddComponent<RectTransform>();
        flashRt.anchorMin = Vector2.zero;
        flashRt.anchorMax = Vector2.one;
        flashRt.offsetMin = Vector2.zero;
        flashRt.offsetMax = Vector2.zero;
        var flashImg = flashObj.AddComponent<Image>();
        flashImg.color = new Color(1f, 1f, 1f, 0f);
        flashImg.raycastTarget = false;
        {
            float elapsed = 0f;
            float half = chestFlashDuration * 0.4f;
            while (elapsed < half)
            {
                elapsed += Time.unscaledDeltaTime;
                flashImg.color = new Color(1f, 1f, 1f, elapsed / half);
                yield return null;
            }
            elapsed = 0f;
            float fadeOut = chestFlashDuration * 0.6f;
            while (elapsed < fadeOut)
            {
                elapsed += Time.unscaledDeltaTime;
                flashImg.color = new Color(1f, 1f, 1f, 1f - elapsed / fadeOut);
                yield return null;
            }
        }
        Destroy(flashObj);

        // ⑥ Gem出現
        PlayCardSE(chestGemAppearSE);
        if (chestImage != null) chestImage.gameObject.SetActive(false);
        if (glowImage != null) glowImage.gameObject.SetActive(false);
        if (gemIconImage != null)
        {
            gemIconImage.gameObject.SetActive(true);
            var iconCg = gemIconImage.GetComponent<CanvasGroup>();
            if (iconCg == null) iconCg = gemIconImage.gameObject.AddComponent<CanvasGroup>();
            iconCg.alpha = 0f;
            gemIconImage.transform.localScale = Vector3.one * 0.6f;
            float elapsed = 0f;
            while (elapsed < chestGemRevealDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / chestGemRevealDuration);
                float e = 1f - Mathf.Pow(1f - t, 3f); // ease-out cubic
                iconCg.alpha = e;
                gemIconImage.transform.localScale = Vector3.Lerp(Vector3.one * 0.6f, Vector3.one, e);
                yield return null;
            }
            iconCg.alpha = 1f;
            gemIconImage.transform.localScale = Vector3.one;
        }
    }

    private void PlayCardSE(AudioClip clip)
    {
        if (clip == null || audioSource == null) return;
        float vol = SoundSettingsManager.Instance != null ? SoundSettingsManager.Instance.SEVolume : 1f;
        audioSource.PlayOneShot(clip, vol);
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
