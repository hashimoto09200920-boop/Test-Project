using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class StageCutInUI : MonoBehaviour
{
    // =========================================================
    // SE
    // =========================================================
    [Header("SE - Stage 1 (Slash Reveal)")]
    [Tooltip("斬撃音")] [SerializeField] private AudioClip se_slash;
    [Tooltip("テキスト出現音（3種からランダム）")] [SerializeField] private AudioClip[] se_stage1TextVariants = new AudioClip[3];

    [Header("SE - Stage 2 (Slash Reveal)")]
    [Tooltip("斬撃音")] [SerializeField] private AudioClip se_burst;
    [Tooltip("テキスト出現音（3種からランダム）")] [SerializeField] private AudioClip[] se_impactVariants = new AudioClip[3];

    [Header("SE - Stage 3 (Slash Reveal)")]
    [Tooltip("斬撃音")] [SerializeField] private AudioClip se_slashS3;
    [Tooltip("テキスト出現音（3種からランダム）")] [SerializeField] private AudioClip[] se_stage3TextVariants = new AudioClip[3];

    // =========================================================
    // UI References
    // =========================================================
    [Header("UI References - Root")]
    [SerializeField] private GameObject cutInRoot;

    [Header("UI References - Stage 1 (Slash)")]
    [SerializeField] private Image stage1BlackBg;
    [SerializeField] private RectTransform slashLine;
    [Tooltip("斬撃ラインを起点に左右へ浸食するオーバーレイ（シアン）")]
    [SerializeField] private Image stage1InvasionOverlay;
    [Tooltip("浸食の上から黒に塗り戻すオーバーレイ")]
    [SerializeField] private Image stage1RevertOverlay;
    [SerializeField] private TextMeshProUGUI stage1Text;

    [Header("UI References - Stage 2 (Slash)")]
    [SerializeField] private Image stage2BlackBg;
    [SerializeField] private RectTransform slashLine2;
    [Tooltip("斬撃ラインを起点に左右へ浸食するオーバーレイ（マゼンタ）")]
    [SerializeField] private Image stage2InvasionOverlay;
    [Tooltip("浸食の上から黒に塗り戻すオーバーレイ")]
    [SerializeField] private Image stage2RevertOverlay;
    [SerializeField] private TextMeshProUGUI stage2Text;

    [Header("UI References - Stage 3 (Slash)")]
    [SerializeField] private Image stage3BlackBg;
    [SerializeField] private RectTransform slashLine3;
    [Tooltip("斬撃ラインを起点に左右へ浸食するオーバーレイ（レッド）")]
    [SerializeField] private Image stage3InvasionOverlay;
    [Tooltip("浸食の上から黒に塗り戻すオーバーレイ")]
    [SerializeField] private Image stage3RevertOverlay;
    [SerializeField] private TextMeshProUGUI stage3Text;

    // =========================================================
    // Settings - Stage 1
    // =========================================================
    [Header("Settings - Stage 1")]
    [Tooltip("テキスト内容")] [SerializeField] private string s1TextContent = "STAGE  1";
    [Tooltip("テキストカラー")] [SerializeField] private Color s1TextColor = new Color(1f, 1f, 1f, 1f);
    [Tooltip("②浸食オーバーレイの色")] [SerializeField] private Color s1InvasionColor = new Color(0f, 1f, 1f, 1f);
    [Tooltip("斬撃角度の最小値（度）")] [SerializeField] private float s1SlashAngleMin = -75f;
    [Tooltip("斬撃角度の最大値（度）")] [SerializeField] private float s1SlashAngleMax = -15f;

    // Timing - Stage 1
    // =========================================================
    [Header("Timing - Stage 1")]
    [Tooltip("黒背景フェードイン時間")] [SerializeField] private float s1FadeInBg = 0.2f;
    [Tooltip("①黒背景アルファ（低いほど背後のゲームが透けて見える）")] [Range(0f, 1f)] [SerializeField] private float s1BgAlphaTarget = 0.4f;
    [Tooltip("斬撃ラインが伸びる時間")] [SerializeField] private float s1ScaleSlash = 0.1f;
    [Tooltip("斬撃後の溜め時間")] [SerializeField] private float s1ChargeHold = 0.2f;
    [Tooltip("斬撃消去ワイプ時間（逆方向）")] [SerializeField] private float s1EraseSlash = 0.15f;
    [Tooltip("浸食展開にかかる時間")] [SerializeField] private float s1InvasionDuration = 0.4f;
    [Tooltip("②浸食オーバーレイ（シアン）アルファ（0=透明・1=不透明）")] [Range(0f, 1f)] [SerializeField] private float s1InvasionAlpha = 0.7f;
    [Tooltip("黒戻し展開にかかる時間")] [SerializeField] private float s1RevertDuration = 0.3f;
    [Tooltip("③黒戻しアニメーション中の黒アルファ（0=透明・1=不透明）")] [Range(0f, 1f)] [SerializeField] private float s1RevertDuringAlpha = 0.7f;
    [Tooltip("黒戻し完了後のフェードダウン時間")] [SerializeField] private float s1RevertFadeDown = 0.15f;
    [Tooltip("④黒戻し完了後の黒アルファ（テキスト表示中・0=透明・1=不透明）")] [Range(0f, 1f)] [SerializeField] private float s1RevertFinalAlpha = 0.4f;
    [Tooltip("テキストフェードイン")] [SerializeField] private float s1FadeInText = 0.15f;
    [Tooltip("テキスト保持時間")] [SerializeField] private float s1Hold = 0.65f;
    [Tooltip("テキストフェードアウト")] [SerializeField] private float s1FadeOutText = 0.2f;
    [Tooltip("斬撃ラインフェードアウト")] [SerializeField] private float s1FadeOutSlash = 0.15f;
    [Tooltip("黒背景・浸食オーバーレイ 同時フェードアウト")] [SerializeField] private float s1FadeOutBg = 0.3f;

    // =========================================================
    // Settings - Stage 2
    // =========================================================
    [Header("Settings - Stage 2")]
    [Tooltip("テキスト内容")] [SerializeField] private string s2TextContent = "STAGE  2";
    [Tooltip("テキストカラー")] [SerializeField] private Color s2TextColor = new Color(1f, 0.84f, 0f, 1f);
    [Tooltip("②浸食オーバーレイの色")] [SerializeField] private Color s2InvasionColor = new Color(1f, 0f, 1f, 1f);
    [Tooltip("斬撃角度の最小値（度）")] [SerializeField] private float s2SlashAngleMin = -75f;
    [Tooltip("斬撃角度の最大値（度）")] [SerializeField] private float s2SlashAngleMax = -15f;

    // =========================================================
    // Timing - Stage 2
    // =========================================================
    [Header("Timing - Stage 2")]
    [Tooltip("黒背景フェードイン時間")] [SerializeField] private float s2FadeInBg = 0.2f;
    [Tooltip("①黒背景アルファ（低いほど背後のゲームが透けて見える）")] [Range(0f, 1f)] [SerializeField] private float s2BgAlphaTarget = 0.4f;
    [Tooltip("斬撃ラインが伸びる時間")] [SerializeField] private float s2ScaleSlash = 0.1f;
    [Tooltip("斬撃後の溜め時間")] [SerializeField] private float s2ChargeHold = 0.2f;
    [Tooltip("斬撃消去ワイプ時間（逆方向）")] [SerializeField] private float s2EraseSlash = 0.15f;
    [Tooltip("浸食展開にかかる時間")] [SerializeField] private float s2InvasionDuration = 0.4f;
    [Tooltip("②浸食オーバーレイ（マゼンタ）アルファ（0=透明・1=不透明）")] [Range(0f, 1f)] [SerializeField] private float s2InvasionAlpha = 0.7f;
    [Tooltip("黒戻し展開にかかる時間")] [SerializeField] private float s2RevertDuration = 0.3f;
    [Tooltip("③黒戻しアニメーション中の黒アルファ（0=透明・1=不透明）")] [Range(0f, 1f)] [SerializeField] private float s2RevertDuringAlpha = 0.7f;
    [Tooltip("黒戻し完了後のフェードダウン時間")] [SerializeField] private float s2RevertFadeDown = 0.15f;
    [Tooltip("④黒戻し完了後の黒アルファ（テキスト表示中・0=透明・1=不透明）")] [Range(0f, 1f)] [SerializeField] private float s2RevertFinalAlpha = 0.4f;
    [Tooltip("テキストフェードイン")] [SerializeField] private float s2FadeInText = 0.15f;
    [Tooltip("テキストフェードアウト")] [SerializeField] private float s2FadeOutText = 0.2f;
    [Tooltip("黒背景・浸食オーバーレイ 同時フェードアウト")] [SerializeField] private float s2FadeOutBg = 0.3f;

    // =========================================================
    // Settings - Stage 3
    // =========================================================
    [Header("Settings - Stage 3")]
    [Tooltip("テキスト内容")] [SerializeField] private string s3TextContent = "STAGE  3";
    [Tooltip("テキストカラー")] [SerializeField] private Color s3TextColor = new Color(1f, 1f, 1f, 1f);
    [Tooltip("②浸食オーバーレイの色")] [SerializeField] private Color s3InvasionColor = new Color(1f, 0.1f, 0.1f, 1f);
    [Tooltip("斬撃角度の最小値（度）")] [SerializeField] private float s3SlashAngleMin = -75f;
    [Tooltip("斬撃角度の最大値（度）")] [SerializeField] private float s3SlashAngleMax = -15f;

    // =========================================================
    // Timing - Stage 3
    // =========================================================
    [Header("Timing - Stage 3")]
    [Tooltip("黒背景フェードイン時間")] [SerializeField] private float s3FadeInBg = 0.2f;
    [Tooltip("①黒背景アルファ（低いほど背後のゲームが透けて見える）")] [Range(0f, 1f)] [SerializeField] private float s3BgAlphaTarget = 0.4f;
    [Tooltip("斬撃ラインが伸びる時間")] [SerializeField] private float s3ScaleSlash = 0.1f;
    [Tooltip("斬撃後の溜め時間")] [SerializeField] private float s3ChargeHold = 0.2f;
    [Tooltip("斬撃消去ワイプ時間（逆方向）")] [SerializeField] private float s3EraseSlash = 0.15f;
    [Tooltip("浸食展開にかかる時間")] [SerializeField] private float s3InvasionDuration = 0.4f;
    [Tooltip("②浸食オーバーレイ（レッド）アルファ（0=透明・1=不透明）")] [Range(0f, 1f)] [SerializeField] private float s3InvasionAlpha = 0.7f;
    [Tooltip("黒戻し展開にかかる時間")] [SerializeField] private float s3RevertDuration = 0.3f;
    [Tooltip("③黒戻しアニメーション中の黒アルファ（0=透明・1=不透明）")] [Range(0f, 1f)] [SerializeField] private float s3RevertDuringAlpha = 0.7f;
    [Tooltip("黒戻し完了後のフェードダウン時間")] [SerializeField] private float s3RevertFadeDown = 0.15f;
    [Tooltip("④黒戻し完了後の黒アルファ（テキスト表示中・0=透明・1=不透明）")] [Range(0f, 1f)] [SerializeField] private float s3RevertFinalAlpha = 0.4f;
    [Tooltip("テキストフェードイン")] [SerializeField] private float s3FadeInText = 0.15f;
    [Tooltip("テキスト保持時間")] [SerializeField] private float s3Hold = 0.65f;
    [Tooltip("テキストフェードアウト")] [SerializeField] private float s3FadeOutText = 0.2f;
    [Tooltip("斬撃ラインフェードアウト")] [SerializeField] private float s3FadeOutSlash = 0.15f;
    [Tooltip("黒背景・浸食オーバーレイ 同時フェードアウト")] [SerializeField] private float s3FadeOutBg = 0.3f;

    // =========================================================
    // カラー定数
    // =========================================================
    private static readonly Color CyanColor    = new Color(0f, 1f, 1f, 1f);
    private static readonly Color MagentaColor = new Color(1f, 0f, 1f, 1f);
    private static readonly Color RedColor     = new Color(1f, 0.1f, 0.1f, 1f);

    // =========================================================
    // Runtime
    // =========================================================
    private enum TextAnimStyle { FlipIn, SlideIn, StampIn, ScalePunch }
    private TextAnimStyle[] stageTextStyles;

    private AudioSource audioSource;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;

        AssignTextStyles();

        if (cutInRoot != null)
            cutInRoot.SetActive(false);
    }

    // =========================================================
    // Public API
    // =========================================================
    public IEnumerator ShowCutIn(int stageIndex)
    {
        if (cutInRoot == null) yield break;

        Time.timeScale = 0f;
        cutInRoot.SetActive(true);
        ResetAllElements();

        switch (stageIndex)
        {
            case 0: yield return StartCoroutine(PlayStage1CutIn()); break;
            case 1: yield return StartCoroutine(PlayStage2CutIn()); break;
            case 2: yield return StartCoroutine(PlayStage3CutIn()); break;
        }

        cutInRoot.SetActive(false);
        Time.timeScale = 1f;
    }

    // =========================================================
    // Stage 1 — Slash Reveal
    // =========================================================
    private IEnumerator PlayStage1CutIn()
    {
        // 黒背景フェードイン（浸食と同じ透過度まで）
        yield return StartCoroutine(FadeImage(stage1BlackBg, 0f, s1BgAlphaTarget, s1FadeInBg));

        // ランダム角度を決定し斬撃・浸食・黒戻しオーバーレイに適用
        float slashAngle = Random.Range(s1SlashAngleMin, s1SlashAngleMax);
        Quaternion slashRot = Quaternion.Euler(0f, 0f, slashAngle);
        if (slashLine != null)              slashLine.localRotation = slashRot;
        if (stage1InvasionOverlay != null)  stage1InvasionOverlay.rectTransform.localRotation = slashRot;
        if (stage1RevertOverlay != null)    stage1RevertOverlay.rectTransform.localRotation = slashRot;

        // 斬撃ライン
        if (slashLine != null)
        {
            slashLine.gameObject.SetActive(true);
            PlaySE(se_slash);
            yield return StartCoroutine(ScaleX(slashLine, 0f, 1f, s1ScaleSlash));
        }

        // 溜め時間
        yield return new WaitForSecondsRealtime(s1ChargeHold);

        // 斬撃消去ワイプ（逆方向：X 1→0）
        if (slashLine != null)
            yield return StartCoroutine(ScaleX(slashLine, 1f, 0f, s1EraseSlash));

        // テキスト準備（②開始前にセットアップ）
        if (stage1Text != null)
        {
            stage1Text.rectTransform.anchoredPosition = new Vector2(140f, 0f);
            stage1Text.text = s1TextContent;
            InitTextForAnim(stage1Text, stageTextStyles[0], s1TextColor);
            stage1Text.gameObject.SetActive(true);
            PlaySERandom(se_stage1TextVariants);
        }

        // ②浸食演出 + テキストイン演出 同時進行
        if (stage1Text != null)
            StartCoroutine(AnimateTextIn(stage1Text, stageTextStyles[0], s1FadeInText));
        yield return StartCoroutine(PlayStage1Invasion());

        // ③黒戻し演出 + テキストアウト演出 同時進行
        if (stage1Text != null)
            StartCoroutine(AnimateTextOut(stage1Text, stageTextStyles[0], s1FadeOutText));
        yield return StartCoroutine(PlayStage1Revert());

        // 黒背景 + 浸食オーバーレイ 同時フェードアウト
        yield return StartCoroutine(FadeStage1BgOut());
    }

    /// <summary>
    /// 斬撃ラインを起点に、全画面シアンオーバーレイを中心から左右へ展開する。
    /// 同時に黒背景アルファを下げてプレイ画面を透かす。
    /// </summary>
    private IEnumerator PlayStage1Invasion()
    {
        if (stage1InvasionOverlay != null)
        {
            stage1InvasionOverlay.color = new Color(s1InvasionColor.r, s1InvasionColor.g, s1InvasionColor.b, 0f);
            // localScale.y=0 で斜め線の上に平たく重なった状態（見えない）からスタート
            stage1InvasionOverlay.rectTransform.localScale = new Vector3(1f, 0f, 1f);
            stage1InvasionOverlay.gameObject.SetActive(true);
        }

        float elapsed = 0f;

        while (elapsed < s1InvasionDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / s1InvasionDuration);

            // localScale.y を拡大 → ローカルY軸方向（斜め線に垂直 = 右斜め上/左斜め下）へ広がる
            if (stage1InvasionOverlay != null)
            {
                stage1InvasionOverlay.rectTransform.localScale = new Vector3(1f, t, 1f);
                stage1InvasionOverlay.color = new Color(s1InvasionColor.r, s1InvasionColor.g, s1InvasionColor.b, Mathf.Lerp(0f, s1InvasionAlpha, t));
            }

            // シアンが広がるのと同時に BlackBg をフェードアウト（重ならないよう置き換え）
            if (stage1BlackBg != null)
                stage1BlackBg.color = new Color(0f, 0f, 0f, Mathf.Lerp(s1BgAlphaTarget, 0f, t));

            yield return null;
        }

        // 確定値をセット
        if (stage1InvasionOverlay != null)
        {
            stage1InvasionOverlay.rectTransform.localScale = Vector3.one;
            stage1InvasionOverlay.color = new Color(s1InvasionColor.r, s1InvasionColor.g, s1InvasionColor.b, s1InvasionAlpha);
        }
        if (stage1BlackBg != null)
            stage1BlackBg.color = new Color(0f, 0f, 0f, 0f);
    }

    /// <summary>
    /// 浸食と同じ起点・方向で黒オーバーレイを展開し、浸食エリアを黒に塗り戻す。
    /// </summary>
    private IEnumerator PlayStage1Revert()
    {
        if (stage1RevertOverlay != null)
        {
            stage1RevertOverlay.color = new Color(0f, 0f, 0f, s1RevertDuringAlpha);
            stage1RevertOverlay.rectTransform.localScale = new Vector3(1f, 0f, 1f);
            stage1RevertOverlay.gameObject.SetActive(true);
        }

        float elapsed = 0f;
        while (elapsed < s1RevertDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / s1RevertDuration);

            if (stage1RevertOverlay != null)
                stage1RevertOverlay.rectTransform.localScale = new Vector3(1f, t, 1f);

            if (stage1InvasionOverlay != null)
                stage1InvasionOverlay.rectTransform.localScale = new Vector3(1f, 1f - t, 1f);

            yield return null;
        }

        if (stage1RevertOverlay != null)
        {
            stage1RevertOverlay.rectTransform.localScale = Vector3.one;
            stage1RevertOverlay.color = new Color(0f, 0f, 0f, s1RevertDuringAlpha);
        }

        // シアンを非表示（黒で完全に覆われているので見た目に変化なし）
        if (stage1InvasionOverlay != null)
            stage1InvasionOverlay.gameObject.SetActive(false);

        // 全画面真っ黒 → s1RevertFinalAlpha まで素早くフェードダウン（インライン実装）
        float fd = 0f;
        while (fd < s1RevertFadeDown)
        {
            fd += Time.unscaledDeltaTime;
            float ft = Mathf.Clamp01(fd / s1RevertFadeDown);
            if (stage1RevertOverlay != null)
                stage1RevertOverlay.color = new Color(0f, 0f, 0f, Mathf.Lerp(s1RevertDuringAlpha, s1RevertFinalAlpha, ft));
            yield return null;
        }
        if (stage1RevertOverlay != null)
            stage1RevertOverlay.color = new Color(0f, 0f, 0f, s1RevertFinalAlpha);
    }

    /// <summary>
    /// 黒背景と浸食オーバーレイを同時にフェードアウト。
    /// </summary>
    private IEnumerator FadeStage1BgOut()
    {
        float bgAlphaStart  = stage1BlackBg        != null ? stage1BlackBg.color.a        : 0f;
        float invAlphaStart = stage1InvasionOverlay != null ? stage1InvasionOverlay.color.a : 0f;
        float revAlphaStart = stage1RevertOverlay   != null ? stage1RevertOverlay.color.a   : 0f;

        float elapsed = 0f;
        while (elapsed < s1FadeOutBg)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / s1FadeOutBg);

            if (stage1BlackBg != null)
                stage1BlackBg.color = new Color(0f, 0f, 0f, Mathf.Lerp(bgAlphaStart, 0f, t));
            if (stage1InvasionOverlay != null)
                stage1InvasionOverlay.color = new Color(s1InvasionColor.r, s1InvasionColor.g, s1InvasionColor.b, Mathf.Lerp(invAlphaStart, 0f, t));
            if (stage1RevertOverlay != null)
                stage1RevertOverlay.color = new Color(0f, 0f, 0f, Mathf.Lerp(revAlphaStart, 0f, t));

            yield return null;
        }

        if (stage1BlackBg        != null) stage1BlackBg.color        = new Color(0f, 0f, 0f, 0f);
        if (stage1InvasionOverlay != null) stage1InvasionOverlay.color = new Color(s1InvasionColor.r, s1InvasionColor.g, s1InvasionColor.b, 0f);
        if (stage1RevertOverlay   != null) stage1RevertOverlay.color   = new Color(0f, 0f, 0f, 0f);
    }

    // =========================================================
    // Stage 2 — Slash Reveal
    // =========================================================
    private IEnumerator PlayStage2CutIn()
    {
        yield return StartCoroutine(FadeImage(stage2BlackBg, 0f, s2BgAlphaTarget, s2FadeInBg));

        float slashAngle2 = Random.Range(s2SlashAngleMin, s2SlashAngleMax);
        Quaternion slashRot2 = Quaternion.Euler(0f, 0f, slashAngle2);
        if (slashLine2 != null)             slashLine2.localRotation = slashRot2;
        if (stage2InvasionOverlay != null)  stage2InvasionOverlay.rectTransform.localRotation = slashRot2;
        if (stage2RevertOverlay != null)    stage2RevertOverlay.rectTransform.localRotation = slashRot2;

        if (slashLine2 != null)
        {
            slashLine2.gameObject.SetActive(true);
            PlaySE(se_burst);
            yield return StartCoroutine(ScaleX(slashLine2, 0f, 1f, s2ScaleSlash));
        }

        yield return new WaitForSecondsRealtime(s2ChargeHold);

        if (slashLine2 != null)
            yield return StartCoroutine(ScaleX(slashLine2, 1f, 0f, s2EraseSlash));

        if (stage2Text != null)
        {
            stage2Text.rectTransform.anchoredPosition = new Vector2(140f, 0f);
            stage2Text.text = s2TextContent;
            InitTextForAnim(stage2Text, stageTextStyles[1], s2TextColor);
            stage2Text.gameObject.SetActive(true);
            PlaySERandom(se_impactVariants);
        }

        if (stage2Text != null)
            StartCoroutine(AnimateTextIn(stage2Text, stageTextStyles[1], s2FadeInText));
        yield return StartCoroutine(PlayStage2Invasion());

        if (stage2Text != null)
            StartCoroutine(AnimateTextOut(stage2Text, stageTextStyles[1], s2FadeOutText));
        yield return StartCoroutine(PlayStage2Revert());

        yield return StartCoroutine(FadeStage2BgOut());
    }

    private IEnumerator PlayStage2Invasion()
    {
        if (stage2InvasionOverlay != null)
        {
            stage2InvasionOverlay.color = new Color(s2InvasionColor.r, s2InvasionColor.g, s2InvasionColor.b, 0f);
            stage2InvasionOverlay.rectTransform.localScale = new Vector3(1f, 0f, 1f);
            stage2InvasionOverlay.gameObject.SetActive(true);
        }

        float elapsed = 0f;
        while (elapsed < s2InvasionDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / s2InvasionDuration);

            if (stage2InvasionOverlay != null)
            {
                stage2InvasionOverlay.rectTransform.localScale = new Vector3(1f, t, 1f);
                stage2InvasionOverlay.color = new Color(s2InvasionColor.r, s2InvasionColor.g, s2InvasionColor.b, Mathf.Lerp(0f, s2InvasionAlpha, t));
            }
            if (stage2BlackBg != null)
                stage2BlackBg.color = new Color(0f, 0f, 0f, Mathf.Lerp(s2BgAlphaTarget, 0f, t));

            yield return null;
        }

        if (stage2InvasionOverlay != null)
        {
            stage2InvasionOverlay.rectTransform.localScale = Vector3.one;
            stage2InvasionOverlay.color = new Color(s2InvasionColor.r, s2InvasionColor.g, s2InvasionColor.b, s2InvasionAlpha);
        }
        if (stage2BlackBg != null)
            stage2BlackBg.color = new Color(0f, 0f, 0f, 0f);
    }

    private IEnumerator PlayStage2Revert()
    {
        if (stage2RevertOverlay != null)
        {
            stage2RevertOverlay.color = new Color(0f, 0f, 0f, s2RevertDuringAlpha);
            stage2RevertOverlay.rectTransform.localScale = new Vector3(1f, 0f, 1f);
            stage2RevertOverlay.gameObject.SetActive(true);
        }

        float elapsed = 0f;
        while (elapsed < s2RevertDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / s2RevertDuration);

            if (stage2RevertOverlay != null)
                stage2RevertOverlay.rectTransform.localScale = new Vector3(1f, t, 1f);
            if (stage2InvasionOverlay != null)
                stage2InvasionOverlay.rectTransform.localScale = new Vector3(1f, 1f - t, 1f);

            yield return null;
        }

        if (stage2RevertOverlay != null)
        {
            stage2RevertOverlay.rectTransform.localScale = Vector3.one;
            stage2RevertOverlay.color = new Color(0f, 0f, 0f, s2RevertDuringAlpha);
        }
        if (stage2InvasionOverlay != null)
            stage2InvasionOverlay.gameObject.SetActive(false);

        float fd = 0f;
        while (fd < s2RevertFadeDown)
        {
            fd += Time.unscaledDeltaTime;
            float ft = Mathf.Clamp01(fd / s2RevertFadeDown);
            if (stage2RevertOverlay != null)
                stage2RevertOverlay.color = new Color(0f, 0f, 0f, Mathf.Lerp(s2RevertDuringAlpha, s2RevertFinalAlpha, ft));
            yield return null;
        }
        if (stage2RevertOverlay != null)
            stage2RevertOverlay.color = new Color(0f, 0f, 0f, s2RevertFinalAlpha);
    }

    private IEnumerator FadeStage2BgOut()
    {
        float bgAlphaStart  = stage2BlackBg        != null ? stage2BlackBg.color.a        : 0f;
        float invAlphaStart = stage2InvasionOverlay != null ? stage2InvasionOverlay.color.a : 0f;
        float revAlphaStart = stage2RevertOverlay   != null ? stage2RevertOverlay.color.a   : 0f;

        float elapsed = 0f;
        while (elapsed < s2FadeOutBg)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / s2FadeOutBg);

            if (stage2BlackBg != null)
                stage2BlackBg.color = new Color(0f, 0f, 0f, Mathf.Lerp(bgAlphaStart, 0f, t));
            if (stage2InvasionOverlay != null)
                stage2InvasionOverlay.color = new Color(s2InvasionColor.r, s2InvasionColor.g, s2InvasionColor.b, Mathf.Lerp(invAlphaStart, 0f, t));
            if (stage2RevertOverlay != null)
                stage2RevertOverlay.color = new Color(0f, 0f, 0f, Mathf.Lerp(revAlphaStart, 0f, t));

            yield return null;
        }

        if (stage2BlackBg        != null) stage2BlackBg.color        = new Color(0f, 0f, 0f, 0f);
        if (stage2InvasionOverlay != null) stage2InvasionOverlay.color = new Color(s2InvasionColor.r, s2InvasionColor.g, s2InvasionColor.b, 0f);
        if (stage2RevertOverlay   != null) stage2RevertOverlay.color   = new Color(0f, 0f, 0f, 0f);
    }

    // =========================================================
    // Stage 3 — Slash Reveal
    // =========================================================
    private IEnumerator PlayStage3CutIn()
    {
        yield return StartCoroutine(FadeImage(stage3BlackBg, 0f, s3BgAlphaTarget, s3FadeInBg));

        float slashAngle3 = Random.Range(s3SlashAngleMin, s3SlashAngleMax);
        Quaternion slashRot3 = Quaternion.Euler(0f, 0f, slashAngle3);
        if (slashLine3 != null)             slashLine3.localRotation = slashRot3;
        if (stage3InvasionOverlay != null)  stage3InvasionOverlay.rectTransform.localRotation = slashRot3;
        if (stage3RevertOverlay != null)    stage3RevertOverlay.rectTransform.localRotation = slashRot3;

        if (slashLine3 != null)
        {
            slashLine3.gameObject.SetActive(true);
            PlaySE(se_slashS3);
            yield return StartCoroutine(ScaleX(slashLine3, 0f, 1f, s3ScaleSlash));
        }

        yield return new WaitForSecondsRealtime(s3ChargeHold);

        if (slashLine3 != null)
            yield return StartCoroutine(ScaleX(slashLine3, 1f, 0f, s3EraseSlash));

        if (stage3Text != null)
        {
            stage3Text.rectTransform.anchoredPosition = new Vector2(140f, 0f);
            stage3Text.text = s3TextContent;
            InitTextForAnim(stage3Text, stageTextStyles[2], s3TextColor);
            stage3Text.gameObject.SetActive(true);
            PlaySERandom(se_stage3TextVariants);
        }

        if (stage3Text != null)
            StartCoroutine(AnimateTextIn(stage3Text, stageTextStyles[2], s3FadeInText));
        yield return StartCoroutine(PlayStage3Invasion());

        if (stage3Text != null)
            StartCoroutine(AnimateTextOut(stage3Text, stageTextStyles[2], s3FadeOutText));
        yield return StartCoroutine(PlayStage3Revert());

        yield return StartCoroutine(FadeStage3BgOut());
    }

    private IEnumerator PlayStage3Invasion()
    {
        if (stage3InvasionOverlay != null)
        {
            stage3InvasionOverlay.color = new Color(s3InvasionColor.r, s3InvasionColor.g, s3InvasionColor.b, 0f);
            stage3InvasionOverlay.rectTransform.localScale = new Vector3(1f, 0f, 1f);
            stage3InvasionOverlay.gameObject.SetActive(true);
        }

        float elapsed = 0f;
        while (elapsed < s3InvasionDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / s3InvasionDuration);

            if (stage3InvasionOverlay != null)
            {
                stage3InvasionOverlay.rectTransform.localScale = new Vector3(1f, t, 1f);
                stage3InvasionOverlay.color = new Color(s3InvasionColor.r, s3InvasionColor.g, s3InvasionColor.b, Mathf.Lerp(0f, s3InvasionAlpha, t));
            }
            if (stage3BlackBg != null)
                stage3BlackBg.color = new Color(0f, 0f, 0f, Mathf.Lerp(s3BgAlphaTarget, 0f, t));

            yield return null;
        }

        if (stage3InvasionOverlay != null)
        {
            stage3InvasionOverlay.rectTransform.localScale = Vector3.one;
            stage3InvasionOverlay.color = new Color(s3InvasionColor.r, s3InvasionColor.g, s3InvasionColor.b, s3InvasionAlpha);
        }
        if (stage3BlackBg != null)
            stage3BlackBg.color = new Color(0f, 0f, 0f, 0f);
    }

    private IEnumerator PlayStage3Revert()
    {
        if (stage3RevertOverlay != null)
        {
            stage3RevertOverlay.color = new Color(0f, 0f, 0f, s3RevertDuringAlpha);
            stage3RevertOverlay.rectTransform.localScale = new Vector3(1f, 0f, 1f);
            stage3RevertOverlay.gameObject.SetActive(true);
        }

        float elapsed = 0f;
        while (elapsed < s3RevertDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / s3RevertDuration);

            if (stage3RevertOverlay != null)
                stage3RevertOverlay.rectTransform.localScale = new Vector3(1f, t, 1f);
            if (stage3InvasionOverlay != null)
                stage3InvasionOverlay.rectTransform.localScale = new Vector3(1f, 1f - t, 1f);

            yield return null;
        }

        if (stage3RevertOverlay != null)
        {
            stage3RevertOverlay.rectTransform.localScale = Vector3.one;
            stage3RevertOverlay.color = new Color(0f, 0f, 0f, s3RevertDuringAlpha);
        }
        if (stage3InvasionOverlay != null)
            stage3InvasionOverlay.gameObject.SetActive(false);

        float fd = 0f;
        while (fd < s3RevertFadeDown)
        {
            fd += Time.unscaledDeltaTime;
            float ft = Mathf.Clamp01(fd / s3RevertFadeDown);
            if (stage3RevertOverlay != null)
                stage3RevertOverlay.color = new Color(0f, 0f, 0f, Mathf.Lerp(s3RevertDuringAlpha, s3RevertFinalAlpha, ft));
            yield return null;
        }
        if (stage3RevertOverlay != null)
            stage3RevertOverlay.color = new Color(0f, 0f, 0f, s3RevertFinalAlpha);
    }

    private IEnumerator FadeStage3BgOut()
    {
        float bgAlphaStart  = stage3BlackBg        != null ? stage3BlackBg.color.a        : 0f;
        float invAlphaStart = stage3InvasionOverlay != null ? stage3InvasionOverlay.color.a : 0f;
        float revAlphaStart = stage3RevertOverlay   != null ? stage3RevertOverlay.color.a   : 0f;

        float elapsed = 0f;
        while (elapsed < s3FadeOutBg)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / s3FadeOutBg);

            if (stage3BlackBg != null)
                stage3BlackBg.color = new Color(0f, 0f, 0f, Mathf.Lerp(bgAlphaStart, 0f, t));
            if (stage3InvasionOverlay != null)
                stage3InvasionOverlay.color = new Color(s3InvasionColor.r, s3InvasionColor.g, s3InvasionColor.b, Mathf.Lerp(invAlphaStart, 0f, t));
            if (stage3RevertOverlay != null)
                stage3RevertOverlay.color = new Color(0f, 0f, 0f, Mathf.Lerp(revAlphaStart, 0f, t));

            yield return null;
        }

        if (stage3BlackBg        != null) stage3BlackBg.color        = new Color(0f, 0f, 0f, 0f);
        if (stage3InvasionOverlay != null) stage3InvasionOverlay.color = new Color(s3InvasionColor.r, s3InvasionColor.g, s3InvasionColor.b, 0f);
        if (stage3RevertOverlay   != null) stage3RevertOverlay.color   = new Color(0f, 0f, 0f, 0f);
    }

    // =========================================================
    // テキスト演出
    // =========================================================
    private void AssignTextStyles()
    {
        var all = (TextAnimStyle[])System.Enum.GetValues(typeof(TextAnimStyle));
        stageTextStyles = new TextAnimStyle[3];
        if (all.Length >= 3)
        {
            var pool = (TextAnimStyle[])all.Clone();
            for (int i = pool.Length - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                var tmp = pool[i]; pool[i] = pool[j]; pool[j] = tmp;
            }
            for (int i = 0; i < 3; i++) stageTextStyles[i] = pool[i];
        }
        else
        {
            for (int i = 0; i < 3; i++)
                stageTextStyles[i] = all[Random.Range(0, all.Length)];
        }
    }

    private void InitTextForAnim(TextMeshProUGUI text, TextAnimStyle style, Color textColor)
    {
        switch (style)
        {
            case TextAnimStyle.FlipIn:
                text.color = new Color(textColor.r, textColor.g, textColor.b, 1f);
                text.rectTransform.localScale = new Vector3(1f, 0f, 1f);
                break;
            case TextAnimStyle.SlideIn:
                text.color = new Color(textColor.r, textColor.g, textColor.b, 1f);
                text.rectTransform.anchoredPosition = new Vector2(-1600f, 0f);
                break;
            case TextAnimStyle.StampIn:
                text.color = new Color(textColor.r, textColor.g, textColor.b, 1f);
                text.rectTransform.localScale = Vector3.one;
                text.rectTransform.anchoredPosition = new Vector2(140f, 700f);
                break;
            case TextAnimStyle.ScalePunch:
                text.color = new Color(textColor.r, textColor.g, textColor.b, 1f);
                text.rectTransform.localScale = new Vector3(2f, 2f, 1f);
                break;
            default:
                text.color = new Color(textColor.r, textColor.g, textColor.b, 0f);
                text.rectTransform.localScale = Vector3.one;
                break;
        }
    }

    private IEnumerator AnimateTextIn(TextMeshProUGUI text, TextAnimStyle style, float duration)
    {
        if (text == null) yield break;
        switch (style)
        {
            case TextAnimStyle.FlipIn:   yield return StartCoroutine(FlipYScale(text.rectTransform, 0f, 1f, duration)); break;
            case TextAnimStyle.SlideIn:  yield return StartCoroutine(SlideTextIn(text.rectTransform, duration));         break;
            case TextAnimStyle.StampIn:  yield return StartCoroutine(StampTextIn(text.rectTransform, duration));         break;
            case TextAnimStyle.ScalePunch: yield return StartCoroutine(ScalePunchIn(text.rectTransform, duration));      break;
            default:                     yield return StartCoroutine(FadeTMP(text, 0f, 1f, duration));                   break;
        }
    }

    private IEnumerator AnimateTextOut(TextMeshProUGUI text, TextAnimStyle style, float duration)
    {
        if (text == null) yield break;
        switch (style)
        {
            case TextAnimStyle.FlipIn:   yield return StartCoroutine(FlipYScale(text.rectTransform, 1f, 0f, duration)); break;
            case TextAnimStyle.SlideIn:  yield return StartCoroutine(SlideTextOut(text.rectTransform, duration));        break;
            case TextAnimStyle.StampIn:  yield return StartCoroutine(StampTextOut(text.rectTransform, duration));        break;
            case TextAnimStyle.ScalePunch: yield return StartCoroutine(ScalePunchOut(text.rectTransform, duration));     break;
            default:                     yield return StartCoroutine(FadeTMP(text, 1f, 0f, duration));                  break;
        }
    }

    private IEnumerator FlipYScale(RectTransform rt, float from, float to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            rt.localScale = new Vector3(1f, Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration)), 1f);
            yield return null;
        }
        rt.localScale = new Vector3(1f, to, 1f);
    }

    private IEnumerator ScalePunchIn(RectTransform rt, float duration)
    {
        float actualDuration = duration * 2.5f;
        float elapsed = 0f;
        while (elapsed < actualDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / actualDuration);
            float s;
            if (t < 0.7f)
            {
                float t1 = t / 0.7f;
                s = Mathf.Lerp(2f, 0.9f, t1 * t1); // ease-in: 2.0→0.9 (勢いよく縮む)
            }
            else
            {
                float t2 = (t - 0.7f) / 0.3f;
                s = Mathf.Lerp(0.9f, 1f, t2); // linear: 0.9→1.0 (バウンス確定)
            }
            rt.localScale = new Vector3(s, s, 1f);
            yield return null;
        }
        rt.localScale = Vector3.one;
    }

    private IEnumerator ScalePunchOut(RectTransform rt, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float s = 1f - t * t; // ease-in: 1.0→0 (加速しながら消える)
            rt.localScale = new Vector3(s, s, 1f);
            yield return null;
        }
        rt.localScale = Vector3.zero;
    }

    private IEnumerator SlideTextIn(RectTransform rt, float duration)
    {
        Vector2 from = rt.anchoredPosition; // -1600 (InitTextForAnimで設定済み)
        Vector2 to   = new Vector2(140f, 0f);
        float actualDuration = duration * 2.5f;
        float elapsed = 0f;
        while (elapsed < actualDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            rt.anchoredPosition = Vector2.LerpUnclamped(from, to, Mathf.Clamp01(elapsed / actualDuration));
            yield return null;
        }
        rt.anchoredPosition = to;
    }

    private IEnumerator StampTextIn(RectTransform rt, float duration)
    {
        Vector2 startPos  = rt.anchoredPosition; // (140, 700) — InitTextForAnimで設定済み
        Vector2 targetPos = new Vector2(140f, 0f);
        float dropDuration   = duration * 2.0f;
        float bounceDuration = duration * 0.5f;

        // Phase 1: 完全表示状態で落下 (scaleYは常に1)
        float elapsed = 0f;
        while (elapsed < dropDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / dropDuration);
            rt.anchoredPosition = Vector2.LerpUnclamped(startPos, targetPos, t * t); // ease-in (重力加速)
            yield return null;
        }
        rt.anchoredPosition = targetPos;

        // Phase 2: 着地バウンス (scaleY: 1 → 1.2 → 1)
        elapsed = 0f;
        while (elapsed < bounceDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / bounceDuration);
            float s = t < 0.5f
                ? Mathf.Lerp(1f, 1.2f, t / 0.5f)
                : Mathf.Lerp(1.2f, 1f, (t - 0.5f) / 0.5f);
            rt.localScale = new Vector3(1f, s, 1f);
            yield return null;
        }
        rt.localScale = Vector3.one;
    }

    private IEnumerator StampTextOut(RectTransform rt, float duration)
    {
        Vector2 from = rt.anchoredPosition; // (140, 0)
        Vector2 to   = new Vector2(140f, -50f);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = t * t; // ease-in (下に加速しながら消える)
            rt.anchoredPosition = Vector2.LerpUnclamped(from, to, eased);
            rt.localScale = new Vector3(1f, 1f - eased, 1f);
            yield return null;
        }
        rt.localScale = new Vector3(1f, 0f, 1f);
        rt.anchoredPosition = to;
    }

    private IEnumerator SlideTextOut(RectTransform rt, float duration)
    {
        Vector2 from = rt.anchoredPosition; // 140 (SlideTextIn完了後)
        Vector2 to   = new Vector2(1600f, 0f);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = t * t; // ease-in: じわっと加速して抜ける
            rt.anchoredPosition = Vector2.LerpUnclamped(from, to, eased);
            yield return null;
        }
        rt.anchoredPosition = to;
    }

    // =========================================================
    // Reset
    // =========================================================
    private void ResetAllElements()
    {
        // Stage 1
        if (stage1BlackBg != null) stage1BlackBg.color = new Color(0f, 0f, 0f, 0f);
        if (slashLine != null)
        {
            slashLine.gameObject.SetActive(false);
            slashLine.localScale = new Vector3(0f, 1f, 1f);
            var img = slashLine.GetComponent<Image>();
            if (img != null) img.color = new Color(img.color.r, img.color.g, img.color.b, 1f);
        }
        if (stage1InvasionOverlay != null)
        {
            stage1InvasionOverlay.gameObject.SetActive(false);
            stage1InvasionOverlay.color = new Color(s1InvasionColor.r, s1InvasionColor.g, s1InvasionColor.b, 0f);
            stage1InvasionOverlay.rectTransform.localScale = new Vector3(1f, 0f, 1f);
        }
        if (stage1RevertOverlay != null)
        {
            stage1RevertOverlay.gameObject.SetActive(false);
            stage1RevertOverlay.color = new Color(0f, 0f, 0f, 0f);
            stage1RevertOverlay.rectTransform.localScale = new Vector3(1f, 0f, 1f);
        }
        if (stage1Text != null) stage1Text.gameObject.SetActive(false);

        // Stage 2
        if (stage2BlackBg != null) stage2BlackBg.color = new Color(0f, 0f, 0f, 0f);
        if (slashLine2 != null)
        {
            slashLine2.gameObject.SetActive(false);
            slashLine2.localScale = new Vector3(0f, 1f, 1f);
            var img = slashLine2.GetComponent<Image>();
            if (img != null) img.color = new Color(img.color.r, img.color.g, img.color.b, 1f);
        }
        if (stage2InvasionOverlay != null)
        {
            stage2InvasionOverlay.gameObject.SetActive(false);
            stage2InvasionOverlay.color = new Color(s2InvasionColor.r, s2InvasionColor.g, s2InvasionColor.b, 0f);
            stage2InvasionOverlay.rectTransform.localScale = new Vector3(1f, 0f, 1f);
        }
        if (stage2RevertOverlay != null)
        {
            stage2RevertOverlay.gameObject.SetActive(false);
            stage2RevertOverlay.color = new Color(0f, 0f, 0f, 0f);
            stage2RevertOverlay.rectTransform.localScale = new Vector3(1f, 0f, 1f);
        }
        if (stage2Text != null) stage2Text.gameObject.SetActive(false);

        // Stage 3
        if (stage3BlackBg != null) stage3BlackBg.color = new Color(0f, 0f, 0f, 0f);
        if (slashLine3 != null)
        {
            slashLine3.gameObject.SetActive(false);
            slashLine3.localScale = new Vector3(0f, 1f, 1f);
            var img = slashLine3.GetComponent<Image>();
            if (img != null) img.color = new Color(img.color.r, img.color.g, img.color.b, 1f);
        }
        if (stage3InvasionOverlay != null)
        {
            stage3InvasionOverlay.gameObject.SetActive(false);
            stage3InvasionOverlay.color = new Color(s3InvasionColor.r, s3InvasionColor.g, s3InvasionColor.b, 0f);
            stage3InvasionOverlay.rectTransform.localScale = new Vector3(1f, 0f, 1f);
        }
        if (stage3RevertOverlay != null)
        {
            stage3RevertOverlay.gameObject.SetActive(false);
            stage3RevertOverlay.color = new Color(0f, 0f, 0f, 0f);
            stage3RevertOverlay.rectTransform.localScale = new Vector3(1f, 0f, 1f);
        }
        if (stage3Text != null) stage3Text.gameObject.SetActive(false);
    }

    // =========================================================
    // SE
    // =========================================================
    private void PlaySE(AudioClip clip)
    {
        if (clip == null || audioSource == null) return;
        float vol = SoundSettingsManager.Instance != null
            ? SoundSettingsManager.Instance.SEVolume
            : 1f;
        audioSource.PlayOneShot(clip, vol);
    }

    private void PlaySERandom(AudioClip[] clips)
    {
        if (clips == null) return;
        int count = 0;
        foreach (var c in clips) if (c != null) count++;
        if (count == 0) return;
        int target = Random.Range(0, count);
        int cur = 0;
        foreach (var c in clips)
        {
            if (c == null) continue;
            if (cur == target) { PlaySE(c); return; }
            cur++;
        }
    }

    // =========================================================
    // アニメーション Helpers
    // =========================================================
    private IEnumerator FadeImage(Image image, float from, float to, float duration)
    {
        if (image == null) yield break;
        float elapsed = 0f;
        Color c = image.color;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            image.color = new Color(c.r, c.g, c.b, Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration)));
            yield return null;
        }
        image.color = new Color(c.r, c.g, c.b, to);
    }

    private IEnumerator FadeTMP(TextMeshProUGUI text, float from, float to, float duration)
    {
        if (text == null) yield break;
        float elapsed = 0f;
        Color c = text.color;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            text.color = new Color(c.r, c.g, c.b, Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration)));
            yield return null;
        }
        text.color = new Color(c.r, c.g, c.b, to);
    }

    private IEnumerator FadeCanvasGroup(CanvasGroup cg, float from, float to, float duration)
    {
        if (cg == null) yield break;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            cg.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }
        cg.alpha = to;
    }

    private IEnumerator ScaleX(RectTransform rt, float from, float to, float duration)
    {
        if (rt == null) yield break;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            rt.localScale = new Vector3(Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration)), 1f, 1f);
            yield return null;
        }
        rt.localScale = new Vector3(to, 1f, 1f);
    }

    // =========================================================
    // Editor
    // =========================================================
#if UNITY_EDITOR
    [ContextMenu("Setup: Stage3 Slash Objects")]
    private void SetupStage3SlashObjects()
    {
        if (cutInRoot == null)
        {
            Debug.LogError("[StageCutInUI] cutInRoot が未設定です。");
            return;
        }

        Transform root = cutInRoot.transform;
        var so = new UnityEditor.SerializedObject(this);
        so.Update();

        // 既存Stage3オブジェクトを削除
        foreach (string n in new[] { "BlackBg3", "InvasionOverlay3", "RevertOverlay3", "SlashLine3", "Stage3Text" })
        {
            Transform old = root.Find(n);
            if (old != null) DestroyImmediate(old.gameObject);
        }

        // BlackBg3
        Image blackBg3 = CreateFullScreenImage(root, "BlackBg3", new Color(0f, 0f, 0f, 0f));

        // InvasionOverlay3（レッド、-45°）
        GameObject invasionObj3 = new GameObject("InvasionOverlay3");
        invasionObj3.transform.SetParent(root, false);
        RectTransform invasionRect3 = invasionObj3.AddComponent<RectTransform>();
        invasionRect3.anchorMin        = new Vector2(0.5f, 0.5f);
        invasionRect3.anchorMax        = new Vector2(0.5f, 0.5f);
        invasionRect3.pivot            = new Vector2(0.5f, 0.5f);
        invasionRect3.sizeDelta        = new Vector2(2800f, 2800f);
        invasionRect3.anchoredPosition = Vector2.zero;
        invasionRect3.localRotation    = Quaternion.Euler(0f, 0f, -45f);
        invasionRect3.localScale       = new Vector3(1f, 0f, 1f);
        Image invasionImg3 = invasionObj3.AddComponent<Image>();
        invasionImg3.color = new Color(RedColor.r, RedColor.g, RedColor.b, 0f);
        invasionImg3.raycastTarget = false;
        invasionObj3.SetActive(false);

        // RevertOverlay3（黒、-45°）
        GameObject revertObj3 = new GameObject("RevertOverlay3");
        revertObj3.transform.SetParent(root, false);
        RectTransform revertRect3 = revertObj3.AddComponent<RectTransform>();
        revertRect3.anchorMin        = new Vector2(0.5f, 0.5f);
        revertRect3.anchorMax        = new Vector2(0.5f, 0.5f);
        revertRect3.pivot            = new Vector2(0.5f, 0.5f);
        revertRect3.sizeDelta        = new Vector2(2800f, 2800f);
        revertRect3.anchoredPosition = Vector2.zero;
        revertRect3.localRotation    = Quaternion.Euler(0f, 0f, -45f);
        revertRect3.localScale       = new Vector3(1f, 0f, 1f);
        Image revertImg3 = revertObj3.AddComponent<Image>();
        revertImg3.color = new Color(0f, 0f, 0f, 0f);
        revertImg3.raycastTarget = false;
        revertObj3.SetActive(false);

        // SlashLine3（レッド、-45°）
        GameObject slashObj3 = CreateRectChild(root, "SlashLine3");
        Image slashImg3 = slashObj3.AddComponent<Image>();
        slashImg3.color = RedColor;
        slashImg3.raycastTarget = false;
        RectTransform slashRect3 = slashObj3.GetComponent<RectTransform>();
        slashRect3.anchorMin        = new Vector2(0.5f, 0.5f);
        slashRect3.anchorMax        = new Vector2(0.5f, 0.5f);
        slashRect3.sizeDelta        = new Vector2(2800f, 6f);
        slashRect3.anchoredPosition = Vector2.zero;
        slashRect3.localRotation    = Quaternion.Euler(0f, 0f, -45f);
        slashRect3.localScale       = new Vector3(0f, 1f, 1f);
        slashObj3.SetActive(false);

        // Stage3Text
        GameObject text3Obj = CreateRectChild(root, "Stage3Text");
        TextMeshProUGUI text3TMP = text3Obj.AddComponent<TextMeshProUGUI>();
        text3TMP.text          = "STAGE  3";
        text3TMP.fontSize      = 120f;
        text3TMP.fontStyle     = FontStyles.Bold;
        text3TMP.alignment     = TextAlignmentOptions.Center;
        text3TMP.color         = Color.white;
        text3TMP.raycastTarget = false;
        RectTransform t3rect = text3Obj.GetComponent<RectTransform>();
        t3rect.anchorMin        = new Vector2(0.5f, 0.5f);
        t3rect.anchorMax        = new Vector2(0.5f, 0.5f);
        t3rect.sizeDelta        = new Vector2(1200f, 200f);
        t3rect.anchoredPosition = new Vector2(140f, 0f);
        text3Obj.AddComponent<Game.UI.TMPAutoFontMaterial>();
        text3Obj.SetActive(false);

        // フィールドに自動アサイン
        so.FindProperty("stage3BlackBg").objectReferenceValue         = blackBg3;
        so.FindProperty("stage3InvasionOverlay").objectReferenceValue = invasionImg3;
        so.FindProperty("stage3RevertOverlay").objectReferenceValue   = revertImg3;
        so.FindProperty("slashLine3").objectReferenceValue            = slashRect3;
        so.FindProperty("stage3Text").objectReferenceValue            = text3TMP;
        so.ApplyModifiedProperties();
        UnityEditor.EditorUtility.SetDirty(this);

        Debug.Log("[StageCutInUI] Stage3 Slash オブジェクト生成完了。");
    }

    [ContextMenu("Setup: Stage2 Slash Objects")]
    private void SetupStage2SlashObjects()
    {
        if (cutInRoot == null)
        {
            Debug.LogError("[StageCutInUI] cutInRoot が未設定です。");
            return;
        }

        Transform root = cutInRoot.transform;
        var so = new UnityEditor.SerializedObject(this);
        so.Update();

        // 旧オブジェクト（VelocityLinesRoot, FlashOverlay）を削除
        Transform oldVel   = root.Find("VelocityLinesRoot");
        Transform oldFlash = root.Find("FlashOverlay");
        if (oldVel   != null) DestroyImmediate(oldVel.gameObject);
        if (oldFlash != null) DestroyImmediate(oldFlash.gameObject);

        // BlackBg2
        Image blackBg2 = CreateFullScreenImage(root, "BlackBg2", new Color(0f, 0f, 0f, 0f));

        // InvasionOverlay2（マゼンタ、-45°）
        GameObject invasionObj2 = new GameObject("InvasionOverlay2");
        invasionObj2.transform.SetParent(root, false);
        RectTransform invasionRect2 = invasionObj2.AddComponent<RectTransform>();
        invasionRect2.anchorMin = new Vector2(0.5f, 0.5f);
        invasionRect2.anchorMax = new Vector2(0.5f, 0.5f);
        invasionRect2.pivot     = new Vector2(0.5f, 0.5f);
        invasionRect2.sizeDelta = new Vector2(2800f, 2800f);
        invasionRect2.anchoredPosition = Vector2.zero;
        invasionRect2.localRotation = Quaternion.Euler(0f, 0f, -45f);
        invasionRect2.localScale    = new Vector3(1f, 0f, 1f);
        Image invasionImg2 = invasionObj2.AddComponent<Image>();
        invasionImg2.color = new Color(MagentaColor.r, MagentaColor.g, MagentaColor.b, 0f);
        invasionImg2.raycastTarget = false;
        invasionObj2.SetActive(false);

        // RevertOverlay2（黒、-45°）
        GameObject revertObj2 = new GameObject("RevertOverlay2");
        revertObj2.transform.SetParent(root, false);
        RectTransform revertRect2 = revertObj2.AddComponent<RectTransform>();
        revertRect2.anchorMin = new Vector2(0.5f, 0.5f);
        revertRect2.anchorMax = new Vector2(0.5f, 0.5f);
        revertRect2.pivot     = new Vector2(0.5f, 0.5f);
        revertRect2.sizeDelta = new Vector2(2800f, 2800f);
        revertRect2.anchoredPosition = Vector2.zero;
        revertRect2.localRotation = Quaternion.Euler(0f, 0f, -45f);
        revertRect2.localScale    = new Vector3(1f, 0f, 1f);
        Image revertImg2 = revertObj2.AddComponent<Image>();
        revertImg2.color = new Color(0f, 0f, 0f, 0f);
        revertImg2.raycastTarget = false;
        revertObj2.SetActive(false);

        // SlashLine2（マゼンタ、-45°）
        GameObject slashObj2 = CreateRectChild(root, "SlashLine2");
        Image slashImg2 = slashObj2.AddComponent<Image>();
        slashImg2.color = MagentaColor;
        slashImg2.raycastTarget = false;
        RectTransform slashRect2 = slashObj2.GetComponent<RectTransform>();
        slashRect2.anchorMin = new Vector2(0.5f, 0.5f);
        slashRect2.anchorMax = new Vector2(0.5f, 0.5f);
        slashRect2.sizeDelta = new Vector2(2800f, 6f);
        slashRect2.anchoredPosition = Vector2.zero;
        slashRect2.localRotation = Quaternion.Euler(0f, 0f, -45f);
        slashRect2.localScale    = new Vector3(0f, 1f, 1f);
        slashObj2.SetActive(false);

        // Stage2Text が未生成なら作成
        Transform existingText2 = root.Find("Stage2Text");
        if (existingText2 == null)
        {
            GameObject text2Obj = CreateRectChild(root, "Stage2Text");
            TextMeshProUGUI text2TMP = text2Obj.AddComponent<TextMeshProUGUI>();
            text2TMP.text      = "STAGE  2";
            text2TMP.fontSize  = 120f;
            text2TMP.fontStyle = FontStyles.Bold;
            text2TMP.alignment = TextAlignmentOptions.Center;
            text2TMP.color     = new Color(MagentaColor.r, MagentaColor.g, MagentaColor.b, 1f);
            text2TMP.raycastTarget = false;
            RectTransform t2rect = text2Obj.GetComponent<RectTransform>();
            t2rect.anchorMin = new Vector2(0.5f, 0.5f);
            t2rect.anchorMax = new Vector2(0.5f, 0.5f);
            t2rect.sizeDelta = new Vector2(1200f, 200f);
            t2rect.anchoredPosition = new Vector2(140f, 0f);
            text2Obj.AddComponent<Game.UI.TMPAutoFontMaterial>();
            text2Obj.SetActive(false);
            so.FindProperty("stage2Text").objectReferenceValue = text2TMP;
        }

        so.FindProperty("stage2BlackBg").objectReferenceValue         = blackBg2;
        so.FindProperty("stage2InvasionOverlay").objectReferenceValue = invasionImg2;
        so.FindProperty("stage2RevertOverlay").objectReferenceValue   = revertImg2;
        so.FindProperty("slashLine2").objectReferenceValue            = slashRect2;
        so.ApplyModifiedProperties();
        UnityEditor.EditorUtility.SetDirty(this);

        Debug.Log("[StageCutInUI] Stage2 Slash オブジェクト生成完了。VelocityLinesRoot・FlashOverlay を削除しました。");
    }
    /// <summary>
    /// 既存Hierarchyに RevertOverlay1 を追加し参照を設定する。
    /// InvasionOverlay1 の直後（SlashLine の直前）に挿入される。
    /// </summary>
    [ContextMenu("Add Missing: Stage1 Revert Overlay")]
    private void AddRevertOverlay()
    {
        if (cutInRoot == null)
        {
            Debug.LogError("[StageCutInUI] cutInRoot が未設定です。");
            return;
        }

        Transform root = cutInRoot.transform;

        GameObject revertObj = new GameObject("RevertOverlay1");
        revertObj.transform.SetParent(root, false);
        RectTransform revertRect = revertObj.AddComponent<RectTransform>();
        revertRect.anchorMin = new Vector2(0.5f, 0.5f);
        revertRect.anchorMax = new Vector2(0.5f, 0.5f);
        revertRect.pivot = new Vector2(0.5f, 0.5f);
        revertRect.sizeDelta = new Vector2(2800f, 2800f);
        revertRect.anchoredPosition = Vector2.zero;
        revertRect.localRotation = Quaternion.Euler(0f, 0f, -45f);
        revertRect.localScale = new Vector3(1f, 0f, 1f);
        Image revertImg = revertObj.AddComponent<Image>();
        revertImg.color = new Color(0f, 0f, 0f, 0f);
        revertImg.raycastTarget = false;
        revertObj.SetActive(false);

        // SlashLine の直前（InvasionOverlay1 の直後）に挿入
        if (slashLine != null)
            revertObj.transform.SetSiblingIndex(slashLine.GetSiblingIndex());

        var so = new UnityEditor.SerializedObject(this);
        so.Update();
        so.FindProperty("stage1RevertOverlay").objectReferenceValue = revertImg;
        so.ApplyModifiedProperties();
        UnityEditor.EditorUtility.SetDirty(this);

        Debug.Log("[StageCutInUI] RevertOverlay1 を追加しました。");
    }

    /// <summary>
    /// 既存Hierarchyに InvasionOverlay1 を追加し参照を設定する。
    /// SlashLine の直前（BlackBg1 の直後）に挿入される。
    /// </summary>
    [ContextMenu("Add Missing: Stage1 Invasion Overlay")]
    private void AddInvasionOverlay()
    {
        if (cutInRoot == null)
        {
            Debug.LogError("[StageCutInUI] cutInRoot が未設定です。");
            return;
        }

        Transform root = cutInRoot.transform;

        // InvasionOverlay1（斬撃ラインと同角度 -45°、中央配置、localScale.y=0 で斜め線上にフラットな状態から開始）
        GameObject invasionObj = new GameObject("InvasionOverlay1");
        invasionObj.transform.SetParent(root, false);
        RectTransform invasionRect = invasionObj.AddComponent<RectTransform>();
        invasionRect.anchorMin = new Vector2(0.5f, 0.5f);
        invasionRect.anchorMax = new Vector2(0.5f, 0.5f);
        invasionRect.pivot = new Vector2(0.5f, 0.5f);
        invasionRect.sizeDelta = new Vector2(2800f, 2800f);
        invasionRect.anchoredPosition = Vector2.zero;
        invasionRect.localRotation = Quaternion.Euler(0f, 0f, -45f);
        invasionRect.localScale = new Vector3(1f, 0f, 1f);
        Image invasionImg = invasionObj.AddComponent<Image>();
        invasionImg.color = new Color(CyanColor.r, CyanColor.g, CyanColor.b, 0f);
        invasionImg.raycastTarget = false;
        invasionObj.SetActive(false);

        // SlashLine の直前に挿入（BlackBg の直後、SlashLine の手前）
        if (slashLine != null)
            invasionObj.transform.SetSiblingIndex(slashLine.GetSiblingIndex());

        // 参照を設定
        var so = new UnityEditor.SerializedObject(this);
        so.Update();
        so.FindProperty("stage1InvasionOverlay").objectReferenceValue = invasionImg;
        so.ApplyModifiedProperties();
        UnityEditor.EditorUtility.SetDirty(this);

        Debug.Log("[StageCutInUI] InvasionOverlay1 を追加しました。");
    }

    /// <summary>
    /// 既存Hierarchyに Stage2Text と BlackBg3 を追加し参照を設定する。
    /// stage1BlackBg と stage1Text は Inspector で手動アサインが必要。
    /// </summary>
    [ContextMenu("Add Missing: Stage2Text and BlackBg3")]
    private void AddMissingElements()
    {
        if (cutInRoot == null)
        {
            Debug.LogError("[StageCutInUI] cutInRoot が未設定です。");
            return;
        }

        Transform root = cutInRoot.transform;
        var so = new UnityEditor.SerializedObject(this);
        so.Update();

        // Stage2Text（マゼンタ）
        GameObject stage2TextObj = CreateRectChild(root, "Stage2Text");
        TextMeshProUGUI stage2TMP = stage2TextObj.AddComponent<TextMeshProUGUI>();
        stage2TMP.text = "STAGE  2";
        stage2TMP.fontSize = 120f;
        stage2TMP.fontStyle = FontStyles.Bold;
        stage2TMP.alignment = TextAlignmentOptions.Center;
        stage2TMP.color = new Color(MagentaColor.r, MagentaColor.g, MagentaColor.b, 1f);
        stage2TMP.raycastTarget = false;
        RectTransform s2tr = stage2TextObj.GetComponent<RectTransform>();
        s2tr.anchorMin = new Vector2(0.5f, 0.5f);
        s2tr.anchorMax = new Vector2(0.5f, 0.5f);
        s2tr.sizeDelta = new Vector2(1200f, 200f);
        s2tr.anchoredPosition = Vector2.zero;
        stage2TextObj.AddComponent<Game.UI.TMPAutoFontMaterial>();
        stage2TextObj.SetActive(false);

        // BlackBg3（Stage3用・全画面・透明）
        Image blackBg3Img = CreateFullScreenImage(root, "BlackBg3", new Color(0f, 0f, 0f, 0f));

        so.FindProperty("stage2Text").objectReferenceValue    = stage2TMP;
        so.FindProperty("stage3BlackBg").objectReferenceValue = blackBg3Img;
        so.ApplyModifiedProperties();
        UnityEditor.EditorUtility.SetDirty(this);

        Debug.Log("[StageCutInUI] Stage2Text と BlackBg3 を追加しました。\n" +
                  "【Inspector で手動アサインが必要】\n" +
                  "  stage1BlackBg ← Hierarchy の BlackBg\n" +
                  "  stage1Text    ← Hierarchy の StageText");
    }

    [ContextMenu("Setup Cut-In UI")]
    private void SetupCutInUI()
    {
        Canvas canvas = GetComponent<Canvas>();
        if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9998;

        UnityEngine.UI.CanvasScaler scaler = GetComponent<UnityEngine.UI.CanvasScaler>();
        if (scaler == null) scaler = gameObject.AddComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        if (GetComponent<UnityEngine.UI.GraphicRaycaster>() == null)
            gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        if (GetComponent<AudioSource>() == null)
            gameObject.AddComponent<AudioSource>();

        GameObject root = CreateFullScreenChild(transform, "CutInRoot");
        root.SetActive(false);

        // --- Stage 1 ---
        Image blackBg1Img = CreateFullScreenImage(root.transform, "BlackBg1", new Color(0f, 0f, 0f, 0f));

        // InvasionOverlay1（斬撃ラインと同角度 -45°、中央配置、localScale.y=0 で斜め線上にフラットな状態から開始）
        GameObject invasionObj = new GameObject("InvasionOverlay1");
        invasionObj.transform.SetParent(root.transform, false);
        RectTransform invasionSetupRect = invasionObj.AddComponent<RectTransform>();
        invasionSetupRect.anchorMin = new Vector2(0.5f, 0.5f);
        invasionSetupRect.anchorMax = new Vector2(0.5f, 0.5f);
        invasionSetupRect.pivot = new Vector2(0.5f, 0.5f);
        invasionSetupRect.sizeDelta = new Vector2(2800f, 2800f);
        invasionSetupRect.anchoredPosition = Vector2.zero;
        invasionSetupRect.localRotation = Quaternion.Euler(0f, 0f, -45f);
        invasionSetupRect.localScale = new Vector3(1f, 0f, 1f);
        Image invasionImg = invasionObj.AddComponent<Image>();
        invasionImg.color = new Color(CyanColor.r, CyanColor.g, CyanColor.b, 0f);
        invasionImg.raycastTarget = false;
        invasionObj.SetActive(false);

        // RevertOverlay1（浸食と同じ角度 -45°、黒で塗り戻す）
        GameObject revertObj = new GameObject("RevertOverlay1");
        revertObj.transform.SetParent(root.transform, false);
        RectTransform revertSetupRect = revertObj.AddComponent<RectTransform>();
        revertSetupRect.anchorMin = new Vector2(0.5f, 0.5f);
        revertSetupRect.anchorMax = new Vector2(0.5f, 0.5f);
        revertSetupRect.pivot = new Vector2(0.5f, 0.5f);
        revertSetupRect.sizeDelta = new Vector2(2800f, 2800f);
        revertSetupRect.anchoredPosition = Vector2.zero;
        revertSetupRect.localRotation = Quaternion.Euler(0f, 0f, -45f);
        revertSetupRect.localScale = new Vector3(1f, 0f, 1f);
        Image revertImg = revertObj.AddComponent<Image>();
        revertImg.color = new Color(0f, 0f, 0f, 0f);
        revertImg.raycastTarget = false;
        revertObj.SetActive(false);

        GameObject slashObj = CreateRectChild(root.transform, "SlashLine");
        Image slashImg = slashObj.AddComponent<Image>();
        slashImg.color = CyanColor;
        slashImg.raycastTarget = false;
        RectTransform slashRect = slashObj.GetComponent<RectTransform>();
        slashRect.anchorMin = new Vector2(0.5f, 0.5f);
        slashRect.anchorMax = new Vector2(0.5f, 0.5f);
        slashRect.sizeDelta = new Vector2(2800f, 6f);
        slashRect.anchoredPosition = Vector2.zero;
        slashRect.localRotation = Quaternion.Euler(0f, 0f, -45f);
        slashRect.localScale = new Vector3(0f, 1f, 1f);
        slashObj.SetActive(false);

        GameObject stage1TextObj = CreateRectChild(root.transform, "Stage1Text");
        TextMeshProUGUI stage1TMP = stage1TextObj.AddComponent<TextMeshProUGUI>();
        stage1TMP.text = "STAGE  1";
        stage1TMP.fontSize = 120f;
        stage1TMP.fontStyle = FontStyles.Bold;
        stage1TMP.alignment = TextAlignmentOptions.Center;
        stage1TMP.color = new Color(CyanColor.r, CyanColor.g, CyanColor.b, 1f);
        stage1TMP.raycastTarget = false;
        RectTransform stage1TextRect = stage1TextObj.GetComponent<RectTransform>();
        stage1TextRect.anchorMin = new Vector2(0.5f, 0.5f);
        stage1TextRect.anchorMax = new Vector2(0.5f, 0.5f);
        stage1TextRect.sizeDelta = new Vector2(1200f, 200f);
        stage1TextRect.anchoredPosition = Vector2.zero;
        stage1TextObj.AddComponent<Game.UI.TMPAutoFontMaterial>();
        stage1TextObj.SetActive(false);

        // --- Stage 2 ---
        GameObject velRootObj = CreateRectChild(root.transform, "VelocityLinesRoot");
        RectTransform velRect = velRootObj.GetComponent<RectTransform>();
        velRect.anchorMin = new Vector2(0.5f, 0.5f);
        velRect.anchorMax = new Vector2(0.5f, 0.5f);
        velRect.sizeDelta = Vector2.zero;
        velRect.anchoredPosition = Vector2.zero;
        CanvasGroup velCG = velRootObj.AddComponent<CanvasGroup>();
        velCG.alpha = 0f;
        velCG.interactable = false;
        velCG.blocksRaycasts = false;

        float[] lineAngles = { 0f, 22.5f, 45f, 67.5f, 90f, 112.5f, 135f, 157.5f };
        Color velColor = new Color(0.8f, 1f, 1f, 0.85f);
        foreach (float angle in lineAngles)
        {
            GameObject lineObj = CreateRectChild(velRootObj.transform, $"VelLine_{angle:F0}");
            Image lineImg = lineObj.AddComponent<Image>();
            lineImg.color = velColor;
            lineImg.raycastTarget = false;
            RectTransform lineRect = lineObj.GetComponent<RectTransform>();
            lineRect.anchorMin = new Vector2(0.5f, 0.5f);
            lineRect.anchorMax = new Vector2(0.5f, 0.5f);
            lineRect.sizeDelta = new Vector2(2400f, 3f);
            lineRect.anchoredPosition = Vector2.zero;
            lineRect.localRotation = Quaternion.Euler(0f, 0f, angle);
        }
        velRootObj.SetActive(false);

        Image flashImg = CreateFullScreenImage(root.transform, "FlashOverlay", new Color(1f, 1f, 1f, 0f));
        flashImg.gameObject.SetActive(false);

        GameObject stage2TextObj = CreateRectChild(root.transform, "Stage2Text");
        TextMeshProUGUI stage2TMP = stage2TextObj.AddComponent<TextMeshProUGUI>();
        stage2TMP.text = "STAGE  2";
        stage2TMP.fontSize = 120f;
        stage2TMP.fontStyle = FontStyles.Bold;
        stage2TMP.alignment = TextAlignmentOptions.Center;
        stage2TMP.color = new Color(MagentaColor.r, MagentaColor.g, MagentaColor.b, 1f);
        stage2TMP.raycastTarget = false;
        RectTransform stage2TextRect = stage2TextObj.GetComponent<RectTransform>();
        stage2TextRect.anchorMin = new Vector2(0.5f, 0.5f);
        stage2TextRect.anchorMax = new Vector2(0.5f, 0.5f);
        stage2TextRect.sizeDelta = new Vector2(1200f, 200f);
        stage2TextRect.anchoredPosition = Vector2.zero;
        stage2TextObj.AddComponent<Game.UI.TMPAutoFontMaterial>();
        stage2TextObj.SetActive(false);

        // --- Stage 3 ---
        Image blackBg3Img = CreateFullScreenImage(root.transform, "BlackBg3", new Color(0f, 0f, 0f, 0f));

        GameObject invasionObj3 = new GameObject("InvasionOverlay3");
        invasionObj3.transform.SetParent(root.transform, false);
        RectTransform invasionRect3 = invasionObj3.AddComponent<RectTransform>();
        invasionRect3.anchorMin        = new Vector2(0.5f, 0.5f);
        invasionRect3.anchorMax        = new Vector2(0.5f, 0.5f);
        invasionRect3.pivot            = new Vector2(0.5f, 0.5f);
        invasionRect3.sizeDelta        = new Vector2(2800f, 2800f);
        invasionRect3.anchoredPosition = Vector2.zero;
        invasionRect3.localRotation    = Quaternion.Euler(0f, 0f, -45f);
        invasionRect3.localScale       = new Vector3(1f, 0f, 1f);
        Image invasionImg3 = invasionObj3.AddComponent<Image>();
        invasionImg3.color = new Color(RedColor.r, RedColor.g, RedColor.b, 0f);
        invasionImg3.raycastTarget = false;
        invasionObj3.SetActive(false);

        GameObject revertObj3 = new GameObject("RevertOverlay3");
        revertObj3.transform.SetParent(root.transform, false);
        RectTransform revertRect3 = revertObj3.AddComponent<RectTransform>();
        revertRect3.anchorMin        = new Vector2(0.5f, 0.5f);
        revertRect3.anchorMax        = new Vector2(0.5f, 0.5f);
        revertRect3.pivot            = new Vector2(0.5f, 0.5f);
        revertRect3.sizeDelta        = new Vector2(2800f, 2800f);
        revertRect3.anchoredPosition = Vector2.zero;
        revertRect3.localRotation    = Quaternion.Euler(0f, 0f, -45f);
        revertRect3.localScale       = new Vector3(1f, 0f, 1f);
        Image revertImg3 = revertObj3.AddComponent<Image>();
        revertImg3.color = new Color(0f, 0f, 0f, 0f);
        revertImg3.raycastTarget = false;
        revertObj3.SetActive(false);

        GameObject slashObj3 = CreateRectChild(root.transform, "SlashLine3");
        Image slashImg3 = slashObj3.AddComponent<Image>();
        slashImg3.color = RedColor;
        slashImg3.raycastTarget = false;
        RectTransform slashRect3 = slashObj3.GetComponent<RectTransform>();
        slashRect3.anchorMin        = new Vector2(0.5f, 0.5f);
        slashRect3.anchorMax        = new Vector2(0.5f, 0.5f);
        slashRect3.sizeDelta        = new Vector2(2800f, 6f);
        slashRect3.anchoredPosition = Vector2.zero;
        slashRect3.localRotation    = Quaternion.Euler(0f, 0f, -45f);
        slashRect3.localScale       = new Vector3(0f, 1f, 1f);
        slashObj3.SetActive(false);

        GameObject stage3TextObj = CreateRectChild(root.transform, "Stage3Text");
        TextMeshProUGUI stage3TMP = stage3TextObj.AddComponent<TextMeshProUGUI>();
        stage3TMP.text          = "STAGE  3";
        stage3TMP.fontSize      = 120f;
        stage3TMP.fontStyle     = FontStyles.Bold;
        stage3TMP.alignment     = TextAlignmentOptions.Center;
        stage3TMP.color         = Color.white;
        stage3TMP.raycastTarget = false;
        RectTransform stage3TextRect = stage3TextObj.GetComponent<RectTransform>();
        stage3TextRect.anchorMin        = new Vector2(0.5f, 0.5f);
        stage3TextRect.anchorMax        = new Vector2(0.5f, 0.5f);
        stage3TextRect.sizeDelta        = new Vector2(1200f, 200f);
        stage3TextRect.anchoredPosition = new Vector2(140f, 0f);
        stage3TextObj.AddComponent<Game.UI.TMPAutoFontMaterial>();
        stage3TextObj.SetActive(false);

        var so = new UnityEditor.SerializedObject(this);
        so.Update();
        so.FindProperty("cutInRoot").objectReferenceValue              = root;
        so.FindProperty("stage1BlackBg").objectReferenceValue          = blackBg1Img;
        so.FindProperty("stage1InvasionOverlay").objectReferenceValue  = invasionImg;
        so.FindProperty("stage1RevertOverlay").objectReferenceValue    = revertImg;
        so.FindProperty("slashLine").objectReferenceValue              = slashRect;
        so.FindProperty("stage1Text").objectReferenceValue             = stage1TMP;
        so.FindProperty("stage2Text").objectReferenceValue             = stage2TMP;
        so.FindProperty("stage3BlackBg").objectReferenceValue          = blackBg3Img;
        so.FindProperty("stage3InvasionOverlay").objectReferenceValue  = invasionImg3;
        so.FindProperty("stage3RevertOverlay").objectReferenceValue    = revertImg3;
        so.FindProperty("slashLine3").objectReferenceValue             = slashRect3;
        so.FindProperty("stage3Text").objectReferenceValue             = stage3TMP;
        so.ApplyModifiedProperties();
        UnityEditor.EditorUtility.SetDirty(this);

        Debug.Log("[StageCutInUI] Setup完了。InspectorでSEクリップを設定してください。");
    }

    private GameObject CreateFullScreenChild(Transform parent, string name)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        RectTransform rt = obj.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;
        return obj;
    }

    private Image CreateFullScreenImage(Transform parent, string name, Color color)
    {
        GameObject obj = CreateFullScreenChild(parent, name);
        Image img = obj.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    private GameObject CreateRectChild(Transform parent, string name)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        obj.AddComponent<RectTransform>();
        return obj;
    }
#endif
}
