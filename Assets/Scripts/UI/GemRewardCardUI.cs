using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using Game.Gems;
using Game.Skills;
using Game.Progress;
#if UNITY_EDITOR
using UnityEditor;
#endif

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

    [Header("Hover Highlight")]
    [Tooltip("ホバー中に背景色をこの色までブレンドする（スケールと同時進行）。normalBgColorよりはっきり明るく・違う色味にすること")]
    [SerializeField] private Color hoverBgColor = new Color(0.45f, 0.78f, 0.82f, 1f);
    [Tooltip("ホバー中に縁取りとして光らせるImage（Setup Hover Glowで自動生成）")]
    [SerializeField] private Image hoverGlowImage;
    [Tooltip("縁取りグローの色。加算合成シェーダーはアルファが実質2乗で効くため、暗い色や低アルファだと" +
        "ほぼ見えなくなる。白すぎると目立たないため、薄めのシアン等はっきり明るい色にすること")]
    [SerializeField] private Color hoverGlowColor = new Color(0.4f, 0.95f, 0.95f, 1f);
    [Tooltip("縁取りグローの最大アルファ")]
    [SerializeField] private float hoverGlowMaxAlpha = 1f;
    [Tooltip("縁取りグローの、カード外周からのはみ出し量(px)。Setup Hover Glow実行時のサイズ計算に使用")]
    [SerializeField] private float hoverGlowPadding = 40f;
    [Tooltip("他のカードがホバーされている間、自分をこの倍率まで暗くする（乗算）")]
    [SerializeField] private float siblingDimMultiplier = 0.55f;
    [Tooltip("他のカードがホバーされている間、自分をこの倍率まで縮小する")]
    [SerializeField] private float siblingShrinkScale = 0.95f;

    [Header("Gem Settings")]
    [Tooltip("フラッシュ前に表示するSprite（ジェム未公開状態）")]
    [SerializeField] private Sprite gemHiddenSprite;
    [Tooltip("フラッシュ後に GemImage のSpriteと差し替えるジェム画像")]
    [SerializeField] private Sprite gemRevealSprite;
    [Tooltip("GemImage のサイズ（Width/Height）。Play前Inspectorで調整可。")]
    [SerializeField] private Vector2 gemImageSize = new Vector2(160f, 160f);
    private Image chestImage; // Setup()内で自動生成

    [Header("Gem SE")]
    [SerializeField] private AudioClip gemFlashSE;

    [Header("Gem Animation")]
    [Tooltip("スプライト切り替え時に再生するパーティクルPrefab")]
    [SerializeField] private ParticleSystem gemRevealParticlePrefab;
    [Tooltip("Gem Reveal Sprite に切り替わった後の待機時間（秒）")]
    [SerializeField] private float gemRevealHoldDuration = 1.0f;

    [Header("Selected Blink")]
    [SerializeField] private float blinkInterval = 0.3f;
    [SerializeField] private float blinkAlpha    = 0.4f;

    [Header("Skill Reveal Animation")]
    [SerializeField] private float revealDropDistance   = 150f;
    [SerializeField] private float revealDropDuration   = 0.25f;
    [SerializeField] private float revealBounceHeight   = 12f;
    [SerializeField] private float revealBounceDuration = 0.12f;
    [Tooltip("スキル間の開始間隔（秒）")]
    [SerializeField] private float revealSkillDelay     = 0.25f;
    [Tooltip("バウンド着地時のSE（カテゴリA）")]
    [SerializeField] private AudioClip revealSE_A;
    [Tooltip("バウンド着地時のSE（カテゴリB）")]
    [SerializeField] private AudioClip revealSE_B;
    [Tooltip("バウンド着地時のSE（カテゴリC）")]
    [SerializeField] private AudioClip revealSE_C;

    [Header("Skill Row Settings")]
    [SerializeField] private float skillRowHeight = 70f;
    [Tooltip("IconContainerの固定幅。全行のアイコン列幅を統一してSkillNameの開始X位置を揃える")]
    [SerializeField] private float iconContainerWidth = 80f;
    [Tooltip("全スキル行のアイコン位置をまとめてオフセット（skill.iconDisplayOffsetに加算）")]
    [SerializeField] private Vector2 iconPositionOffset = Vector2.zero;
    [SerializeField] private float skillFontSize  = 22f;
    [SerializeField] private TMPro.FontStyles skillFontStyle = TMPro.FontStyles.Normal;
    [Tooltip("スキル名テキストの位置オフセット")]
    [SerializeField] private Vector2 skillNameOffset = Vector2.zero;
    [SerializeField] private Color skillNameColor = new Color(0.1f, 0.1f, 0.1f, 1f);

    // GemManagementUIと同一のカテゴリカラー（03_AreaSelect.unity実測値）
    // SerializeFieldにしないことでシーン保存値に上書きされるのを防ぐ
    private static readonly Color _categoryAColor       = new Color(0.7019608f, 0.8980392f, 0.8980392f, 0.8f);
    private static readonly Color _categoryBColor       = new Color(0.7882353f, 0.7019608f, 0.8980392f, 0.8f);
    private static readonly Color _categoryCColor       = new Color(0.8980392f, 0.7882353f, 0.6274510f, 0.8f);
    private static readonly Color _defaultSkillRowColor = new Color(0.15f,      0.15f,      0.15f,      0.5f);

    [Header("State Colors")]
    [SerializeField] private Color normalBgColor            = new Color(0.15f, 0.15f, 0.25f, 1f);
    [SerializeField] private Color selectedBgColor          = new Color(0.25f, 0.38f, 0.55f, 1f);
    [SerializeField] private Color dimmedBgColor            = new Color(0.07f, 0.07f, 0.12f, 1f);
    [SerializeField] private Color resultSelectedBgColor    = new Color(0.25f, 0.38f, 0.55f, 1f);
    [SerializeField] private Color resultUnselectedBgColor  = new Color(0.10f, 0.10f, 0.15f, 1f);

    // Gem Reveal 後のスキル表示用（Setup() で保存）
    private SkillDefinition _storedBaseDef;
    private SkillDefinition _storedBonus1Def;
    private SkillDefinition _storedBonus2Def;

    private CanvasGroup canvasGroup;
    private Coroutine blinkCoroutine;
    private Coroutine scaleCoroutine;
    private Coroutine hoverSECoroutine;
    private Coroutine bgColorCoroutine;
    private Coroutine glowCoroutine;
    private AudioSource audioSource;
    private bool tapDetected = false;

    /// <summary>SetStateで設定された「地の色」。ホバーの色ブレンドはここを起点/復帰先にする</summary>
    private Color baseBgColor;
    /// <summary>自分を含む同じ選択画面の全カード。ホバー時に他カードへ相対ハイライトを伝えるために使う</summary>
    private GemRewardCardUI[] siblingCards;
    /// <summary>他カードのホバーにより自分が相対的に暗くなっている状態か</summary>
    private bool isRelativeDimmed;

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
    public void Setup(GemInstance gem, GemDefinition def, SkillDefinition baseSkill, SkillDefinition bonus1, SkillDefinition bonus2, bool showSkills = true)
    {
        // スキルデータを保存（OpenChestCoroutine でのスキル表示に使用）
        _storedBaseDef  = baseSkill;
        _storedBonus1Def = bonus1;
        _storedBonus2Def = bonus2;

        // 既存スキル行を削除
        if (skillContainer != null)
            foreach (Transform child in skillContainer)
                Destroy(child.gameObject);

        if (gemNameText != null)
            gemNameText.text = def != null ? def.GetLocalizedName() : "";

        if (def == null) return;

        if (showSkills)
        {
            if (skillContainer != null)
                skillContainer.gameObject.SetActive(true);

            if (baseSkill != null) CreateSkillRow(baseSkill);
            if (bonus1 != null)     CreateSkillRow(bonus1);
            if (bonus2 != null)     CreateSkillRow(bonus2);
        }
        else
        {
            if (skillContainer != null)
                skillContainer.gameObject.SetActive(false);
        }

        // ジェム画像を自動生成して Hidden Sprite でセット
        if (gemHiddenSprite != null)
        {
            var chestObj = new GameObject("GemImage");
            chestObj.transform.SetParent(transform, false);
            var chestRt = chestObj.AddComponent<RectTransform>();
            chestRt.anchorMin = new Vector2(0.5f, 0.5f);
            chestRt.anchorMax = new Vector2(0.5f, 0.5f);
            chestRt.pivot = new Vector2(0.5f, 0.5f);
            chestRt.anchoredPosition = new Vector2(0f, -30f);
            chestRt.sizeDelta = gemImageSize;
            chestImage = chestObj.AddComponent<Image>();
            chestImage.sprite = gemHiddenSprite;
            chestImage.preserveAspect = true;

            // ★GemImage(280x280)はカード中央付近に大きく配置されるため、showSkills=trueで
            //   スキル行も同時表示すると縦方向に重なる。デフォルトでは最後に生成され最前面に
            //   描画されてしまい、スキル行の一部が隠れて「隙間」のように見えるバグがあった。
            //   CardBgの直後(最背面寄り)に下げ、スキル行より必ず背面に描画されるようにする。
            chestObj.transform.SetSiblingIndex(1);
        }
    }

    /// <summary>
    /// デバッグ用：スキル行のレイアウトが確定した数フレーム後に、各行の実際のPosY・Heightを
    /// Consoleへ出力する（隙間/重なりの原因調査用の一時ログ）。
    /// ★GameObjectが非アクティブな間はStartCoroutineできないため、呼び出し側(GemRewardUI)で
    ///   phase1Panel.SetActive(true)より後に呼ぶこと。
    /// </summary>
    public IEnumerator LogSkillRowLayoutDelayed()
    {
        yield return null;
        yield return null;
        if (skillContainer == null) yield break;

        Debug.Log($"[GemRewardCardUI] ===== {gameObject.name} skill row layout dump =====");
        int i = 0;
        foreach (Transform child in skillContainer)
        {
            var rt = child as RectTransform;
            if (rt == null) continue;
            var nameTf = rt.Find("SkillNameContainer/SkillName");
            var tmp = nameTf != null ? nameTf.GetComponent<TextMeshProUGUI>() : null;
            string skillName = tmp != null ? tmp.text : "?";
            var le = rt.GetComponent<LayoutElement>();
            Debug.Log($"[GemRewardCardUI]   [{i}] name='{skillName}' childType={child.name} anchoredPosY={rt.anchoredPosition.y:F2} rect.height={rt.rect.height:F2} sizeDelta.y={rt.sizeDelta.y:F2} LE.preferredHeight={(le != null ? le.preferredHeight : -1f):F2} pivotY={rt.pivot.y:F2} anchorMinY={rt.anchorMin.y:F2} anchorMaxY={rt.anchorMax.y:F2}");
            i++;
        }
        var vlg = skillContainer.GetComponent<VerticalLayoutGroup>();
        if (vlg != null)
        {
            Debug.Log($"[GemRewardCardUI]   skillContainer VLG: spacing={vlg.spacing:F2} padding=({vlg.padding.top},{vlg.padding.bottom}) childControlHeight={vlg.childControlHeight} childForceExpandHeight={vlg.childForceExpandHeight} childAlignment={vlg.childAlignment}");
        }
    }

    private void CreateSkillRow(SkillDefinition skill)
    {
        var row = new GameObject("SkillRow");
        row.transform.SetParent(skillContainer, false);

        // 行背景（カテゴリカラー）
        var rowBg = row.AddComponent<Image>();
        rowBg.color = skill.category switch
        {
            SkillCategory.CategoryA => _categoryAColor,
            SkillCategory.CategoryB => _categoryBColor,
            SkillCategory.CategoryC => _categoryCColor,
            _                       => _defaultSkillRowColor,
        };

        var hlg = row.AddComponent<HorizontalLayoutGroup>();
        hlg.childAlignment         = TextAnchor.MiddleLeft;
        hlg.childControlWidth      = true;
        hlg.childControlHeight     = true;
        hlg.childForceExpandWidth  = false;
        hlg.childForceExpandHeight = false;
        hlg.spacing = 8f;

        // アイコンサイズ：skill.iconDisplaySize を使用（SkillHUD の CategoryX_Grid > IconImage と同一）
        Vector2 iconSize   = skill.iconDisplaySize != Vector2.zero ? skill.iconDisplaySize : new Vector2(45f, 45f);
        Vector2 iconOffset = skill.iconDisplaySize != Vector2.zero ? skill.iconDisplayOffset : Vector2.zero;

        // ★行の実際の高さ(アイコンがskillRowHeightより大きい場合はそちらを優先)を先に確定し、
        //   外側VLG(skillContainer)が行の間隔を計算する際に使うLayoutElement.preferredHeightと
        //   実際の描画高さ(rowRT.sizeDelta.y)を必ず一致させる。ズレるとSpacing=0でも隙間/めり込みが出る。
        float rowH = Mathf.Max(skillRowHeight, iconSize.y);

        var le = row.AddComponent<LayoutElement>();
        le.preferredHeight = rowH;
        le.flexibleWidth   = 1f;

        // VLG は childControlHeight=false のため row の RectTransform.sizeDelta.y は自動設定されない
        // → 明示的に設定しないと高さ0のまま HLG 内の子も全て高さ0になる
        var rowRT = row.GetComponent<RectTransform>();
        rowRT.sizeDelta = new Vector2(rowRT.sizeDelta.x, rowH);

        // IconContainer：HLGがサイズを読む器
        var iconContainer = new GameObject("IconContainer");
        iconContainer.transform.SetParent(row.transform, false);
        var containerLE = iconContainer.AddComponent<LayoutElement>();
        containerLE.preferredWidth  = iconContainerWidth;
        containerLE.preferredHeight = iconSize.y;

        // Icon：中心アンカーにして sizeDelta = 絶対サイズ（GemManagementUI と同パターン）
        var iconObj  = new GameObject("Icon");
        iconObj.transform.SetParent(iconContainer.transform, false);
        var iconRect = iconObj.AddComponent<RectTransform>();
        iconRect.anchorMin        = new Vector2(0.5f, 0.5f);
        iconRect.anchorMax        = new Vector2(0.5f, 0.5f);
        iconRect.pivot            = new Vector2(0.5f, 0.5f);
        iconRect.anchoredPosition = iconOffset + iconPositionOffset;
        iconRect.sizeDelta        = iconSize;
        var iconImg = iconObj.AddComponent<Image>();
        iconImg.preserveAspect = false;
        if (skill.icon != null) iconImg.sprite = skill.icon;

        // スキル名コンテナ：HLGがサイズを読む器
        var nameContainer = new GameObject("SkillNameContainer");
        nameContainer.transform.SetParent(row.transform, false);
        var nameContainerLE = nameContainer.AddComponent<LayoutElement>();
        nameContainerLE.preferredWidth  = 280f;
        nameContainerLE.preferredHeight = skillRowHeight;

        // スキル名テキスト：中心アンカーにして anchoredPosition でオフセット適用
        var nameObj  = new GameObject("SkillName");
        nameObj.transform.SetParent(nameContainer.transform, false);
        var nameRect = nameObj.AddComponent<RectTransform>();
        nameRect.anchorMin        = new Vector2(0.5f, 0.5f);
        nameRect.anchorMax        = new Vector2(0.5f, 0.5f);
        nameRect.pivot            = new Vector2(0.5f, 0.5f);
        nameRect.sizeDelta        = new Vector2(280f, skillRowHeight);
        nameRect.anchoredPosition = skillNameOffset;
        var tmp = nameObj.AddComponent<TextMeshProUGUI>();
        tmp.text               = skill.GetLocalizedName();
        tmp.fontSize           = skillFontSize;
        tmp.fontStyle          = skillFontStyle;
        tmp.color              = skillNameColor;
        tmp.alignment          = TextAlignmentOptions.MidlineLeft;
        tmp.enableWordWrapping = false;
    }

    public void SetState(CardState state)
    {
        SetState(state, preserveHoverHighlight: false);
    }

    /// <summary>
    /// preserveHoverHighlight=trueの場合、背景色ブレンド・縁取りグロー・スケールには一切触れず、
    /// ホバー中の見た目をそのまま維持する。カード選択確定(Selected)時に、確認画面の間も
    /// ホバー時の演出を消したくない場合に使う（点滅(StartBlink)は従来通りCanvasGroup.alphaで
    /// 別系統に動くため、この指定と無関係に機能する）。
    /// </summary>
    public void SetState(CardState state, bool preserveHoverHighlight)
    {
        baseBgColor = state switch
        {
            CardState.Normal            => normalBgColor,
            CardState.Selected          => selectedBgColor,
            CardState.Dimmed            => dimmedBgColor,
            CardState.ResultSelected    => resultSelectedBgColor,
            CardState.ResultUnselected  => resultUnselectedBgColor,
            _                           => normalBgColor,
        };

        if (canvasGroup != null)
            canvasGroup.alpha = (state == CardState.Dimmed) ? 0.45f : 1f;

        if (preserveHoverHighlight) return;

        if (cardBgImage != null) cardBgImage.color = baseBgColor;

        // ★状態遷移をまたいでホバー由来の色ブレンド/グロー/相対縮小が残らないよう、都度リセットする
        if (bgColorCoroutine != null) { StopCoroutine(bgColorCoroutine); bgColorCoroutine = null; }
        if (glowCoroutine != null) { StopCoroutine(glowCoroutine); glowCoroutine = null; }
        if (scaleCoroutine != null) { StopCoroutine(scaleCoroutine); scaleCoroutine = null; }
        if (hoverGlowImage != null)
        {
            var c = hoverGlowImage.color;
            hoverGlowImage.color = new Color(c.r, c.g, c.b, 0f);
        }
        isRelativeDimmed = false;
    }

    public void SetAlpha(float alpha)
    {
        if (canvasGroup != null) canvasGroup.alpha = alpha;
    }

    /// <summary>
    /// 同じ選択画面の全カード（自分を含む）を登録する。ホバー時に自分以外へ相対ハイライトを伝えるために使う。
    /// </summary>
    public void SetSiblingCards(GemRewardCardUI[] cards)
    {
        siblingCards = cards;
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

    /// <summary>現在のalphaから0まで、指定時間でフェードアウトする（付与スキル表示を消す時などに使用）</summary>
    public IEnumerator FadeOutCoroutine(float duration)
    {
        if (canvasGroup == null) yield break;
        float elapsed = 0f;
        float startAlpha = canvasGroup.alpha;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, elapsed / duration);
            yield return null;
        }
        canvasGroup.alpha = 0f;
    }

    // =====================================================
    // Chest Open
    // =====================================================

    /// <summary>ジェム取得演出（Gemスプライトに切り替え → 待機 → スキル表示）</summary>
    /// <param name="waitForTap">trueなら演出後にタップ待機。falseなら演出完了後すぐに返る。</param>
    public IEnumerator OpenChestCoroutine(bool waitForTap = true)
    {
        // Gemスプライトに切り替え → パーティクル再生 → 待機
        if (chestImage != null && gemRevealSprite != null)
            chestImage.sprite = gemRevealSprite;

        if (gemRevealParticlePrefab != null)
        {
            var ps = Instantiate(gemRevealParticlePrefab, transform);
            ps.transform.localPosition = Vector3.zero;
            ps.Play();
            PlayCardSE(gemFlashSE);
        }

        if (gemRevealHoldDuration > 0f)
            yield return new WaitForSecondsRealtime(gemRevealHoldDuration);

        // スキル表示（アニメーション付き）
        if (skillContainer != null)
        {
            foreach (Transform child in skillContainer)
                Destroy(child.gameObject);

            // 全行を alpha=0 で生成（レイアウト領域は確保済み）
            var rows = new System.Collections.Generic.List<SkillRowAnimData>();
            if (_storedBaseDef != null)   rows.Add(CreateSkillRowForReveal(_storedBaseDef));
            if (_storedBonus1Def != null) rows.Add(CreateSkillRowForReveal(_storedBonus1Def));
            if (_storedBonus2Def != null) rows.Add(CreateSkillRowForReveal(_storedBonus2Def));

            // GemImage を上部へ移動し、SkillContainer をその下に配置（重なり防止）
            var gemRT   = chestImage != null ? chestImage.GetComponent<RectTransform>() : null;
            var skillRT = skillContainer.GetComponent<RectTransform>();
            var cardRT  = GetComponent<RectTransform>();

            if (gemRT != null && skillRT != null && cardRT != null)
            {
                float cardHalfH = cardRT.sizeDelta.y * 0.5f;
                float gemHalfH  = gemImageSize.y * 0.5f;
                float usableTop  = cardHalfH - 72f;
                float gemCenterY = usableTop - gemHalfH - 8f;
                gemRT.anchoredPosition = new Vector2(0f, gemCenterY);

                float gemBottom   = gemCenterY - gemHalfH;
                skillRT.offsetMax = new Vector2(skillRT.offsetMax.x, gemBottom - 8f - cardHalfH);
            }

            skillContainer.gameObject.SetActive(true);

            // スキルごとにスタガーで落下アニメ開始
            var skillDefs = new System.Collections.Generic.List<SkillDefinition>();
            if (_storedBaseDef   != null) skillDefs.Add(_storedBaseDef);
            if (_storedBonus1Def != null) skillDefs.Add(_storedBonus1Def);
            if (_storedBonus2Def != null) skillDefs.Add(_storedBonus2Def);

            float skillStart = 0f;
            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                AudioClip se = skillDefs[i].category switch
                {
                    SkillCategory.CategoryA => revealSE_A,
                    SkillCategory.CategoryB => revealSE_B,
                    SkillCategory.CategoryC => revealSE_C,
                    _                       => null,
                };
                StartCoroutine(DropElementCoroutine(row.rowRT,  Vector2.zero,     row.rowCG,  skillStart, se));
                StartCoroutine(DropElementCoroutine(row.iconRT, row.iconFinalPos, row.iconCG, skillStart));
                StartCoroutine(DropElementCoroutine(row.nameRT, row.nameFinalPos, row.nameCG, skillStart));
                skillStart += revealSkillDelay;
            }

            // 全スキルの落下アニメ完了まで待機してからクリック受付
            if (rows.Count > 0)
            {
                float lastStart = revealSkillDelay * (rows.Count - 1);
                yield return new WaitForSecondsRealtime(lastStart + revealDropDuration + revealBounceDuration);
            }

            LogRevealRowLayout();
        }

        // タップ待機（Phase2フロー用。ResultScreen直行時は不要）
        if (waitForTap)
        {
            while (true)
            {
                if (Input.GetMouseButtonDown(0)) break;
                if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began) break;
                yield return null;
            }
        }
    }

    /// <summary>
    /// デバッグ用：OpenChestCoroutineの落下アニメ完了直後に、RowWrapper(外側の積み上げ)と
    /// SkillRow(アニメで動く内側、最終的にVector2.zeroに戻っているはず)の実際の値をConsoleへ出力する。
    /// </summary>
    private void LogRevealRowLayout()
    {
        if (skillContainer == null) return;
        Debug.Log($"[GemRewardCardUI] ===== {gameObject.name} REVEAL row layout dump =====");
        int i = 0;
        foreach (Transform wrapperTf in skillContainer)
        {
            var wrapperRT = wrapperTf as RectTransform;
            if (wrapperRT == null) continue;
            var rowTf = wrapperTf.Find("SkillRow");
            var rowRT = rowTf as RectTransform;
            var nameTf = rowTf != null ? rowTf.Find("SkillNameContainer/SkillName") : null;
            var tmp = nameTf != null ? nameTf.GetComponent<TextMeshProUGUI>() : null;
            string skillName = tmp != null ? tmp.text : "?";
            var wrapperLE = wrapperRT.GetComponent<LayoutElement>();
            Debug.Log($"[GemRewardCardUI]   [{i}] name='{skillName}' Wrapper: anchoredPosY={wrapperRT.anchoredPosition.y:F2} height={wrapperRT.rect.height:F2} LE.preferredHeight={(wrapperLE != null ? wrapperLE.preferredHeight : -1f):F2} | SkillRow(内側): anchoredPos={(rowRT != null ? rowRT.anchoredPosition : Vector2.one * -999f)} height={(rowRT != null ? rowRT.rect.height : -1f):F2} alpha={(rowTf != null ? rowTf.GetComponent<CanvasGroup>()?.alpha : -1f)}");
            i++;
        }
    }

    // =====================================================
    // Skill Reveal Animation
    // =====================================================

    private struct SkillRowAnimData
    {
        public RectTransform rowRT;
        public CanvasGroup   rowCG;
        public RectTransform iconRT;
        public CanvasGroup   iconCG;
        public Vector2       iconFinalPos;
        public RectTransform nameRT;
        public CanvasGroup   nameCG;
        public Vector2       nameFinalPos;
    }

    /// <summary>
    /// アニメーション用スキル行を生成（全要素alpha=0で開始）。
    /// SkillRowをRowWrapper内に配置することでVLG管理外のY位置アニメを可能にする。
    /// </summary>
    private SkillRowAnimData CreateSkillRowForReveal(SkillDefinition skill)
    {
        var data = new SkillRowAnimData();
        Vector2 iconSize   = skill.iconDisplaySize != Vector2.zero ? skill.iconDisplaySize : new Vector2(45f, 45f);
        Vector2 iconOffset = skill.iconDisplaySize != Vector2.zero ? skill.iconDisplayOffset : Vector2.zero;
        float rowH = Mathf.Max(skillRowHeight, iconSize.y);

        // RowWrapper: VLGの子（LE）。SkillRowはこの中で自由にY位置アニメできる
        // ★型を指定しないとRectTransformが付かず(LayoutElementは自動追加しない)、
        //   外側のVerticalLayoutGroupがRectTransformの無い子を正しく積み上げられないバグがあった。
        var rowWrapper = new GameObject("RowWrapper", typeof(RectTransform));
        rowWrapper.transform.SetParent(skillContainer, false);
        var wrapperLE = rowWrapper.AddComponent<LayoutElement>();
        wrapperLE.preferredHeight = rowH;
        wrapperLE.flexibleWidth   = 1f;

        // ★外側VLGはchildControlHeight=falseのため、RowWrapper自身のRectTransform.sizeDelta.yは
        //   自動設定されない。LayoutElement.preferredHeightと必ず一致させる。
        var wrapperRT = (RectTransform)rowWrapper.transform;
        wrapperRT.sizeDelta = new Vector2(wrapperRT.sizeDelta.x, rowH);

        // SkillRow: 横stretch・縦中央アンカーでRowWrapper内に収まりつつanchoredPosition.yでアニメ可能
        var row   = new GameObject("SkillRow");
        row.transform.SetParent(rowWrapper.transform, false);
        var rowBg = row.AddComponent<Image>();
        rowBg.color = skill.category switch
        {
            SkillCategory.CategoryA => _categoryAColor,
            SkillCategory.CategoryB => _categoryBColor,
            SkillCategory.CategoryC => _categoryCColor,
            _                       => _defaultSkillRowColor,
        };
        var hlg = row.AddComponent<HorizontalLayoutGroup>();
        hlg.childAlignment         = TextAnchor.MiddleLeft;
        hlg.childControlWidth      = true;
        hlg.childControlHeight     = true;
        hlg.childForceExpandWidth  = false;
        hlg.childForceExpandHeight = false;
        hlg.spacing = 8f;
        var rowRT = row.GetComponent<RectTransform>();
        rowRT.anchorMin        = new Vector2(0f, 0.5f);
        rowRT.anchorMax        = new Vector2(1f, 0.5f);
        rowRT.pivot            = new Vector2(0.5f, 0.5f);
        rowRT.sizeDelta        = new Vector2(0f, rowH);
        rowRT.anchoredPosition = Vector2.zero;
        data.rowRT = rowRT;
        data.rowCG = row.AddComponent<CanvasGroup>();
        data.rowCG.alpha = 0f;

        // IconContainer
        var iconContainer = new GameObject("IconContainer");
        iconContainer.transform.SetParent(row.transform, false);
        var containerLE = iconContainer.AddComponent<LayoutElement>();
        containerLE.preferredWidth  = iconContainerWidth;
        containerLE.preferredHeight = iconSize.y;

        // Icon
        var iconObj  = new GameObject("Icon");
        iconObj.transform.SetParent(iconContainer.transform, false);
        var iconRect = iconObj.AddComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0.5f, 0.5f);
        iconRect.anchorMax = new Vector2(0.5f, 0.5f);
        iconRect.pivot     = new Vector2(0.5f, 0.5f);
        iconRect.sizeDelta = iconSize;
        data.iconFinalPos       = iconOffset + iconPositionOffset;
        iconRect.anchoredPosition = data.iconFinalPos;
        var iconImg = iconObj.AddComponent<Image>();
        iconImg.preserveAspect = false;
        if (skill.icon != null) iconImg.sprite = skill.icon;
        data.iconRT = iconRect;
        data.iconCG = iconObj.AddComponent<CanvasGroup>();
        data.iconCG.alpha = 0f;

        // SkillNameContainer
        var nameContainer = new GameObject("SkillNameContainer");
        nameContainer.transform.SetParent(row.transform, false);
        var nameContainerLE = nameContainer.AddComponent<LayoutElement>();
        nameContainerLE.preferredWidth  = 280f;
        nameContainerLE.preferredHeight = skillRowHeight;

        // SkillName
        var nameObj  = new GameObject("SkillName");
        nameObj.transform.SetParent(nameContainer.transform, false);
        var nameRect = nameObj.AddComponent<RectTransform>();
        nameRect.anchorMin = new Vector2(0.5f, 0.5f);
        nameRect.anchorMax = new Vector2(0.5f, 0.5f);
        nameRect.pivot     = new Vector2(0.5f, 0.5f);
        nameRect.sizeDelta = new Vector2(280f, skillRowHeight);
        data.nameFinalPos       = skillNameOffset;
        nameRect.anchoredPosition = data.nameFinalPos;
        var tmp = nameObj.AddComponent<TextMeshProUGUI>();
        tmp.text               = skill.GetLocalizedName();
        tmp.fontSize           = skillFontSize;
        tmp.fontStyle          = skillFontStyle;
        tmp.color              = skillNameColor;
        tmp.alignment          = TextAlignmentOptions.MidlineLeft;
        tmp.enableWordWrapping = false;
        data.nameRT = nameRect;
        data.nameCG = nameObj.AddComponent<CanvasGroup>();
        data.nameCG.alpha = 0f;

        return data;
    }

    /// <summary>1要素の落下+バウンドアニメ（delay後に開始、alpha 0→1 と同時）。seClip指定時はバウンド着地でSE再生。</summary>
    private IEnumerator DropElementCoroutine(RectTransform rt, Vector2 finalPos, CanvasGroup cg, float delay, AudioClip seClip = null)
    {
        if (delay > 0f) yield return new WaitForSecondsRealtime(delay);

        Vector2 startPos = finalPos + new Vector2(0f, revealDropDistance);
        rt.anchoredPosition = startPos;
        if (cg != null) cg.alpha = 1f;

        // 落下（ease-in quad）
        float elapsed = 0f;
        while (elapsed < revealDropDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / revealDropDuration);
            rt.anchoredPosition = Vector2.Lerp(startPos, finalPos, t * t);
            yield return null;
        }
        rt.anchoredPosition = finalPos;

        // バウンド着地時にSE再生
        PlayCardSE(seClip);

        // バウンド（上に跳ねて戻る）
        Vector2 bouncePos = finalPos + new Vector2(0f, revealBounceHeight);
        float half = revealBounceDuration * 0.5f;
        elapsed = 0f;
        while (elapsed < half)
        {
            elapsed += Time.unscaledDeltaTime;
            rt.anchoredPosition = Vector2.Lerp(finalPos, bouncePos, Mathf.Clamp01(elapsed / half));
            yield return null;
        }
        elapsed = 0f;
        while (elapsed < half)
        {
            elapsed += Time.unscaledDeltaTime;
            rt.anchoredPosition = Vector2.Lerp(bouncePos, finalPos, Mathf.Clamp01(elapsed / half));
            yield return null;
        }
        rt.anchoredPosition = finalPos;
    }

    private void PlayCardSE(AudioClip clip)
    {
        if (clip == null || audioSource == null) return;
        float vol = SoundSettingsManager.Instance != null ? SoundSettingsManager.Instance.SEVolume : 1f;
        audioSource.PlayOneShot(clip, vol);
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
        BlendBgColor(hoverBgColor);
        FadeGlow(hoverGlowMaxAlpha);
        NotifySiblings(true);
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
        BlendBgColor(baseBgColor);
        FadeGlow(0f);
        NotifySiblings(false);
    }

    /// <summary>自分以外の全登録カードへ、ホバー状態の開始/終了を伝える</summary>
    private void NotifySiblings(bool hovering)
    {
        if (siblingCards == null) return;
        foreach (var sibling in siblingCards)
        {
            if (sibling == null || sibling == this) continue;
            sibling.SetRelativeDim(hovering);
        }
    }

    /// <summary>他のカードがホバーされたことを受けて、自分を相対的に暗く・少し縮小する（またはそれを解除する）</summary>
    private void SetRelativeDim(bool dimmed)
    {
        if (!HoverEnabled) return; // 選択済み等でホバーが無効な間は触らない
        isRelativeDimmed = dimmed;
        ScaleTo(dimmed ? siblingShrinkScale : 1f);
        BlendBgColor(dimmed ? MultiplyColor(baseBgColor, siblingDimMultiplier) : baseBgColor);
    }

    private static Color MultiplyColor(Color c, float mult) => new Color(c.r * mult, c.g * mult, c.b * mult, c.a);

    private void BlendBgColor(Color target)
    {
        if (cardBgImage == null) return;
        if (bgColorCoroutine != null) StopCoroutine(bgColorCoroutine);
        bgColorCoroutine = StartCoroutine(BlendBgColorCoroutine(target));
    }

    private IEnumerator BlendBgColorCoroutine(Color target)
    {
        float elapsed = 0f;
        Color from = cardBgImage.color;
        while (elapsed < hoverDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            cardBgImage.color = Color.Lerp(from, target, elapsed / hoverDuration);
            yield return null;
        }
        cardBgImage.color = target;
        bgColorCoroutine = null;
    }

    private void FadeGlow(float targetAlpha)
    {
        if (hoverGlowImage == null) return;
        if (glowCoroutine != null) StopCoroutine(glowCoroutine);
        glowCoroutine = StartCoroutine(FadeGlowCoroutine(targetAlpha));
    }

    private IEnumerator FadeGlowCoroutine(float targetAlpha)
    {
        float elapsed = 0f;
        Color from = hoverGlowImage.color;
        Color to = new Color(from.r, from.g, from.b, targetAlpha);
        while (elapsed < hoverDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            hoverGlowImage.color = Color.Lerp(from, to, elapsed / hoverDuration);
            yield return null;
        }
        hoverGlowImage.color = to;
        glowCoroutine = null;
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

#if UNITY_EDITOR
    /// <summary>
    /// ホバー時の縁取りグロー用に、CardBgより奥にSoftGlowCircle.png(加算合成)のImageを1枚追加する。
    /// カード本体(このGameObjectのRectTransform)のサイズにhoverGlowPadding分の余白を足したサイズで配置するため、
    /// 不透明なCardBgに隠れて縁のはみ出し部分だけがホバー中に光って見える。
    /// 既存オブジェクトがあれば再利用するだけの非破壊的な処理（再実行しても安全）。
    /// </summary>
    [ContextMenu("Setup Hover Glow (SoftGlowCircle+加算合成マテリアルを追加)")]
    private void SetupHoverGlow()
    {
        var glowSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Generated/UI/SoftGlowCircle.png");
        var glowMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Generated/UI/UIAdditiveGlow.mat");
        if (glowSprite == null || glowMat == null)
        {
            Debug.LogError($"[GemRewardCardUI] 素材が見つかりません。SoftGlowCircle={glowSprite != null}, UIAdditiveGlow={glowMat != null}");
            return;
        }

        var cardRt = GetComponent<RectTransform>();
        if (cardRt == null) return;

        var existing = transform.Find("HoverGlow");
        GameObject glowGo = existing != null ? existing.gameObject : new GameObject("HoverGlow", typeof(RectTransform), typeof(Image));
        var glowRt = (RectTransform)glowGo.transform;
        glowRt.SetParent(transform, false);
        glowRt.SetAsFirstSibling(); // CardBgより奥（背面）に描画する
        glowRt.anchorMin = glowRt.anchorMax = new Vector2(0.5f, 0.5f);
        glowRt.pivot = new Vector2(0.5f, 0.5f);
        glowRt.anchoredPosition = Vector2.zero;
        glowRt.sizeDelta = cardRt.sizeDelta + new Vector2(hoverGlowPadding * 2f, hoverGlowPadding * 2f);

        var glowImg = glowGo.GetComponent<Image>();
        glowImg.sprite = glowSprite;
        glowImg.type = Image.Type.Simple;
        glowImg.material = glowMat;
        glowImg.color = new Color(hoverGlowColor.r, hoverGlowColor.g, hoverGlowColor.b, 0f); // 初期は透明
        glowImg.raycastTarget = false;

        var so = new SerializedObject(this);
        so.FindProperty("hoverGlowImage").objectReferenceValue = glowImg;
        so.ApplyModifiedProperties();

        EditorUtility.SetDirty(gameObject);
        Debug.Log("[GemRewardCardUI] HoverGlowを追加しました。");
    }

    /// <summary>skillRowHeightを70に設定する（GemCard0/1/2それぞれで実行すること）</summary>
    [ContextMenu("Fix Skill Row Height (70に統一)")]
    private void FixSkillRowHeight()
    {
        skillRowHeight = 70f;
        EditorUtility.SetDirty(this);
        Debug.Log($"[GemRewardCardUI] {gameObject.name}のskillRowHeightを70に設定しました。");
    }
#endif
}
