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
    [SerializeField] private float skillRowHeight = 80f;
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
    private AudioSource audioSource;
    private bool tapDetected = false;

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
            gemNameText.text = def != null ? def.gemName : "";

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

        var le = row.AddComponent<LayoutElement>();
        le.preferredHeight = skillRowHeight;
        le.flexibleWidth   = 1f;

        // アイコンサイズ：skill.iconDisplaySize を使用（SkillHUD の CategoryX_Grid > IconImage と同一）
        Vector2 iconSize   = skill.iconDisplaySize != Vector2.zero ? skill.iconDisplaySize : new Vector2(45f, 45f);
        Vector2 iconOffset = skill.iconDisplaySize != Vector2.zero ? skill.iconDisplayOffset : Vector2.zero;

        // VLG は childControlHeight=false のため row の RectTransform.sizeDelta.y は自動設定されない
        // → 明示的に設定しないと高さ0のまま HLG 内の子も全て高さ0になる
        var rowRT = row.GetComponent<RectTransform>();
        rowRT.sizeDelta = new Vector2(rowRT.sizeDelta.x, Mathf.Max(skillRowHeight, iconSize.y));

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
        nameContainerLE.preferredWidth  = 220f;
        nameContainerLE.preferredHeight = skillRowHeight;

        // スキル名テキスト：中心アンカーにして anchoredPosition でオフセット適用
        var nameObj  = new GameObject("SkillName");
        nameObj.transform.SetParent(nameContainer.transform, false);
        var nameRect = nameObj.AddComponent<RectTransform>();
        nameRect.anchorMin        = new Vector2(0.5f, 0.5f);
        nameRect.anchorMax        = new Vector2(0.5f, 0.5f);
        nameRect.pivot            = new Vector2(0.5f, 0.5f);
        nameRect.sizeDelta        = new Vector2(220f, skillRowHeight);
        nameRect.anchoredPosition = skillNameOffset;
        var tmp = nameObj.AddComponent<TextMeshProUGUI>();
        tmp.text               = skill.skillName;
        tmp.fontSize           = skillFontSize;
        tmp.fontStyle          = skillFontStyle;
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
        var rowWrapper = new GameObject("RowWrapper");
        rowWrapper.transform.SetParent(skillContainer, false);
        var wrapperLE = rowWrapper.AddComponent<LayoutElement>();
        wrapperLE.preferredHeight = rowH;
        wrapperLE.flexibleWidth   = 1f;

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
        nameContainerLE.preferredWidth  = 220f;
        nameContainerLE.preferredHeight = skillRowHeight;

        // SkillName
        var nameObj  = new GameObject("SkillName");
        nameObj.transform.SetParent(nameContainer.transform, false);
        var nameRect = nameObj.AddComponent<RectTransform>();
        nameRect.anchorMin = new Vector2(0.5f, 0.5f);
        nameRect.anchorMax = new Vector2(0.5f, 0.5f);
        nameRect.pivot     = new Vector2(0.5f, 0.5f);
        nameRect.sizeDelta = new Vector2(220f, skillRowHeight);
        data.nameFinalPos       = skillNameOffset;
        nameRect.anchoredPosition = data.nameFinalPos;
        var tmp = nameObj.AddComponent<TextMeshProUGUI>();
        tmp.text               = skill.skillName;
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
