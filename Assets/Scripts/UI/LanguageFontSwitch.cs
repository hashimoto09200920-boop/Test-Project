using Game.Localization;
using TMPro;
using UnityEngine;

/// <summary>
/// 英語表示の時は、この文字（TMP）のフォントを英語用に切り替える。日本語表示の時は、見出しだけ日本語の見出し用フォントに切り替える（無い・OFFなら元のフォント・素材・大きさに戻す）。
/// ・Heading：見出し・ボタン・短いラベル → 英語：Bungee（数字用と同じフォント）／日本語：Dela Gothic One（NumberFontSettingsで設定・OFF可）
///   数字用フォント（Bungee）にされた文字（数字だけの表示）は、日本語の時もBungeeのまま
/// ・Body：説明文・長い文 → Rajdhani
/// ・Always Use English Font：言語に関係なく常に英語用フォント（RESULT・NEXT など英語固定の文字用）
/// 大きさ：英語用フォントは大文字の高さが従来のフォント（NotoSansJP）と同じになるよう補正済み。元が別のフォントの時は、ここで高さをそろえる倍率を掛ける。
/// 他の処理が後からフォント・素材・文字サイズを設定し直しても（スキル選択カード・チュートリアル等）、ずれた時だけ直す（参照と数値を比べるだけの軽い処理）。
/// 設定は Assets/Resources/NumberFontSettings.asset（無い・OFFなら何もしない）。メニュー「Tools/英語のフォント/…」で対象の文字に一括で付ける。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(TMP_Text))]
public class LanguageFontSwitch : MonoBehaviour
{
    public enum Kind { Heading, Body }

    [Tooltip("Heading：見出し・ボタン・短いラベル（Bungee）／Body：説明文・長い文（Rajdhani）")]
    public Kind kind = Kind.Body;

    [Tooltip("ON：言語に関係なく常に英語用フォントにする（RESULT・NEXT など英語固定の文字用）")]
    public bool alwaysUseEnglishFont;

    [Tooltip("英語用フォントの時だけ文字サイズに掛ける倍率（1＝日本語と同じ高さ。英語の方が横に長く枠からはみ出す所だけ小さくする）")]
    [Range(0.5f, 1.5f)]
    public float englishSizeScale = 1f;

    [Tooltip("ON：日本語表示の時、見出し（Heading）を日本語の見出し用フォント（Dela Gothic One）にする。OFF：日本語の時は元のフォントのまま")]
    public bool useJapaneseHeadingFont = true;

    [Tooltip("日本語の見出し用フォント（Dela Gothic One）の時だけ文字サイズに掛ける倍率（1＝従来と同じ漢字の高さ。カタカナが大きく見える所だけ小さくする）")]
    [Range(0.5f, 1.5f)]
    public float japaneseSizeScale = 1f;

    private TMP_Text text;
    private bool captured;
    private TMP_FontAsset originalFont;
    private Material originalMaterial;
    private bool originalMaterialIsDefault;

    // 今、英語用フォントを当てているか。当てている時の文字サイズ（他から変えられたか見分ける）
    private bool switched;
    private TMP_FontAsset targetFont;
    private float ratio = 1f;
    private float lastSize, lastMin, lastMax;
    private bool boldRemoved;

    private void Awake() { text = GetComponent<TMP_Text>(); }

    private void OnEnable()
    {
        if (LocalizationManager.Instance != null) LocalizationManager.Instance.OnLanguageChanged += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        if (LocalizationManager.Instance != null) LocalizationManager.Instance.OnLanguageChanged -= Refresh;
    }

    private bool WantEnglishFont => alwaysUseEnglishFont || LocalizationManager.CurrentLanguageStatic == GameLanguage.English;

    private TMP_FontAsset ResolveTarget()
    {
        if (WantEnglishFont) return kind == Kind.Heading ? GameNumberFont.EnglishHeadingFont : GameNumberFont.EnglishBodyFont;
        // 日本語：見出しだけ。数字用フォント（Bungee）にされている文字はそのまま
        if (kind != Kind.Heading || !useJapaneseHeadingFont) return null;
        TMP_FontAsset baseFont = switched ? originalFont : text.font;
        if (baseFont != null && baseFont == GameNumberFont.TmpFont) return null;
        return GameNumberFont.JapaneseHeadingFont;
    }

    private bool IsJapaneseHeading(TMP_FontAsset f) => f != null && f == GameNumberFont.JapaneseHeadingFont && f != GameNumberFont.TmpFont;

    /// <summary>数字用フォントにする直前に呼ばれる（GameNumberFont.Apply）。日本語の見出しフォントに切り替え中なら元に戻す</summary>
    public void ReleaseForNumberFont()
    {
        if (switched && IsJapaneseHeading(targetFont)) Restore();
    }

    /// <summary>今の言語に合わせてフォントを切り替える（言語が変わった時・有効になった時）</summary>
    public void Refresh()
    {
        if (text == null) text = GetComponent<TMP_Text>();
        if (text == null) return;
        TMP_FontAsset target = ResolveTarget();
        if (target != null) SwitchTo(target);
        else Restore();
    }

    private void Capture()
    {
        if (captured) return;
        captured = true;
        originalFont = text.font;
        originalMaterial = text.fontSharedMaterial;
        originalMaterialIsDefault = originalMaterial == null || (originalFont != null && originalMaterial == originalFont.material);
    }

    private void SwitchTo(TMP_FontAsset target)
    {
        if (switched && text.font == target) return;
        // 別の切り替え先（英語⇔日本語の見出し）へ変える時は、いったん元に戻してから当て直す
        if (switched && targetFont != target) Restore();
        if (!switched)
        {
            // 元の状態を覚える（他の処理で元のフォントが入れ直された後なら、その時の状態を元とみなす）
            if (!captured || text.font != targetFont) { captured = false; Capture(); }
        }
        targetFont = target;
        bool jp = IsJapaneseHeading(target);
        ratio = jp ? GameNumberFont.KanjiHeightRatio(originalFont, target) * japaneseSizeScale : GameNumberFont.CapHeightRatio(originalFont, target) * englishSizeScale;
        float baseSize = switched ? lastSize / Mathf.Max(0.0001f, ratio) : text.fontSize;
        float baseMin = switched ? lastMin / Mathf.Max(0.0001f, ratio) : text.fontSizeMin;
        float baseMax = switched ? lastMax / Mathf.Max(0.0001f, ratio) : text.fontSizeMax;
        text.font = target;
        text.fontSharedMaterial = originalMaterialIsDefault ? target.material : GameNumberFont.MaterialFor(originalMaterial, target);
        ApplySize(baseSize, baseMin, baseMax);
        if (jp && GameNumberFont.RemoveBoldForJapaneseHeading && (text.fontStyle & FontStyles.Bold) != 0)
        {
            text.fontStyle &= ~FontStyles.Bold;
            boldRemoved = true;
        }
        switched = true;
    }

    private void ApplySize(float baseSize, float baseMin, float baseMax)
    {
        if (Mathf.Approximately(ratio, 1f))
        {
            lastSize = text.fontSize; lastMin = text.fontSizeMin; lastMax = text.fontSizeMax;
            return;
        }
        if (text.enableAutoSizing) { text.fontSizeMin = baseMin * ratio; text.fontSizeMax = baseMax * ratio; }
        text.fontSize = baseSize * ratio;
        lastSize = text.fontSize; lastMin = text.fontSizeMin; lastMax = text.fontSizeMax;
    }

    private void Restore()
    {
        if (!switched) return;
        switched = false;
        if (boldRemoved) { text.fontStyle |= FontStyles.Bold; boldRemoved = false; }
        float baseSize = text.fontSize / Mathf.Max(0.0001f, ratio);
        float baseMin = text.fontSizeMin / Mathf.Max(0.0001f, ratio);
        float baseMax = text.fontSizeMax / Mathf.Max(0.0001f, ratio);
        if (originalFont != null) text.font = originalFont;
        if (originalMaterial != null) text.fontSharedMaterial = originalMaterial;
        if (!Mathf.Approximately(ratio, 1f))
        {
            if (text.enableAutoSizing) { text.fontSizeMin = baseMin; text.fontSizeMax = baseMax; }
            text.fontSize = baseSize;
        }
        ratio = 1f;
    }

    /// <summary>
    /// 他の処理が後からフォント・素材・文字サイズを設定し直した時だけ直す。英語用フォントを当てている時しか何もしない（参照と数値の比較だけ）
    /// </summary>
    private void LateUpdate()
    {
        if (!switched || text == null || targetFont == null) return;
        if (text.font != targetFont)
        {
            // 元のフォントが入れ直された（スキル選択カードのSetFont等）→ その状態を元として当て直す
            switched = false;
            captured = false;
            SwitchTo(targetFont);
            return;
        }
        Material m = text.fontSharedMaterial;
        if (m == null || (targetFont.atlasTexture != null && m.HasProperty(ShaderUtilities.ID_MainTex) && m.GetTexture(ShaderUtilities.ID_MainTex) != targetFont.atlasTexture))
        {
            // 素材だけ元に戻された（Awakeで font.material を入れ直す処理等）
            text.fontSharedMaterial = originalMaterialIsDefault ? targetFont.material : GameNumberFont.MaterialFor(originalMaterial, targetFont);
        }
        // 自動サイズの文字はTMPが毎回fontSizeを計算し直すため、上限・下限が変えられた時だけ見る（fontSizeで見ると倍率が重ね掛けされる）
        bool sizeChanged = text.enableAutoSizing
            ? !Mathf.Approximately(text.fontSizeMax, lastMax) || !Mathf.Approximately(text.fontSizeMin, lastMin)
            : !Mathf.Approximately(text.fontSize, lastSize);
        if (!Mathf.Approximately(ratio, 1f) && sizeChanged)
        {
            // 文字サイズを設定し直された（チュートリアルの行など）→ 新しい値を元として高さをそろえ直す
            ApplySize(text.fontSize, text.fontSizeMin, text.fontSizeMax);
        }
    }
}
