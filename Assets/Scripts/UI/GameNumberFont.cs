using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// ゲーム中の「数字だけを表示する文字」を、数字用フォント（Bungee）に切り替える共通の窓口。
/// 設定は Assets/Resources/NumberFontSettings.asset（メニュー「Tools/数字のフォント/…」で作成・更新）。
/// ・大きさ：元のフォント（NotoSansJP・LiberationSans・旧式TextMeshの標準Arial等）で「0」を描いた時の高さと、Bungeeの「0」の高さを比べ、
///   同じ高さになる倍率を返す（フォントの組み合わせごとに1回だけ計算して覚えておく）。
///   文字サイズを1回だけ決める表示は Apply が自動で掛ける。表示のたびに文字サイズを設定し直す所は、Apply の戻り値（倍率）を覚えて自分で掛ける
/// ・縁取りなどの素材（マテリアル）が付いた文字は、その素材を数字用フォントの文字画像に差し替えた写しを1回だけ作って使い回す
/// ・設定アセットが無い／OFFの時は何もしない（倍率は1）＝従来どおり
/// ・負荷：切り替えは表示を作る時に1回だけ。毎フレームの処理は増えない
/// </summary>
public static class GameNumberFont
{
    private static NumberFontSettings s_settings;
    private static bool s_loaded;
    private static readonly Dictionary<(Material, TMP_FontAsset), Material> s_variants = new Dictionary<(Material, TMP_FontAsset), Material>();
    private static readonly Dictionary<(TMP_FontAsset, TMP_FontAsset), float> s_capRatio = new Dictionary<(TMP_FontAsset, TMP_FontAsset), float>();
    private static readonly Dictionary<TMP_FontAsset, float> s_tmpRatio = new Dictionary<TMP_FontAsset, float>();
    private static readonly Dictionary<Font, float> s_legacyRatio = new Dictionary<Font, float>();
    private static Font s_builtinLegacy;

    private static NumberFontSettings Settings
    {
        get
        {
            if (!s_loaded || (s_settings == null && Application.isEditor))
            {
                s_settings = Resources.Load<NumberFontSettings>("NumberFontSettings");
                s_loaded = true;
            }
            return s_settings;
        }
    }

    /// <summary>数字用フォント（TMP）が使える状態か</summary>
    public static bool Enabled
    {
        get { var s = Settings; return s != null && s.enabledNumberFont && s.tmpFont != null; }
    }

    /// <summary>旧式TextMesh用の数字フォントが使える状態か</summary>
    public static bool LegacyEnabled
    {
        get { var s = Settings; return s != null && s.enabledNumberFont && s.legacyFont != null; }
    }

    public static TMP_FontAsset TmpFont => Enabled ? Settings.tmpFont : null;

    /// <summary>英語の見出し・ボタン・短いラベル用（Bungee）。使えない時はnull</summary>
    public static TMP_FontAsset EnglishHeadingFont
    {
        get { var s = Settings; return s != null && s.enabledNumberFont && s.useTmpFontForEnglishHeadings ? s.tmpFont : null; }
    }

    /// <summary>英語の説明文・長い文用（Rajdhani）。使えない時はnull</summary>
    public static TMP_FontAsset EnglishBodyFont
    {
        get { var s = Settings; return s != null && s.enabledNumberFont ? s.englishBodyFont : null; }
    }

    /// <summary>日本語の見出し・ボタン・短いラベル用（Dela Gothic One）。使えない時はnull</summary>
    public static TMP_FontAsset JapaneseHeadingFont
    {
        get { var s = Settings; return s != null && s.useJapaneseHeadingFont ? s.japaneseHeadingFont : null; }
    }

    /// <summary>日本語の見出しフォントにする時に太字を外すか</summary>
    public static bool RemoveBoldForJapaneseHeading
    {
        get { var s = Settings; return s == null || s.removeBoldForJapaneseHeading; }
    }

    private static readonly Dictionary<(TMP_FontAsset, TMP_FontAsset), float> s_kanjiRatio = new Dictionary<(TMP_FontAsset, TMP_FontAsset), float>();

    /// <summary>
    /// 元のフォント（null＝TMPの既定フォント）の漢字「国」と同じ高さにするために、日本語用フォントの文字サイズに掛ける倍率（組み合わせごとに1回だけ計算）。
    /// 元のフォントに漢字が無い（英語専用フォント等）時は1
    /// </summary>
    public static float KanjiHeightRatio(TMP_FontAsset from, TMP_FontAsset to)
    {
        if (from == null) from = TMP_Settings.defaultFontAsset;
        if (from == null || to == null || from == to) return 1f;
        if (s_kanjiRatio.TryGetValue((from, to), out float r)) return r;
        const char c = '国';
        r = 1f;
        if (from.HasCharacter(c, false, true) && to.HasCharacter(c, false, true))
        {
            float a = CharHeight(from, c), b = CharHeight(to, c);
            if (a > 0f && b > 0f) r = a / b;
        }
        s_kanjiRatio[(from, to)] = r;
        return r;
    }

    /// <summary>
    /// 元のフォント（null＝TMPの既定フォント）の大文字「H」と同じ高さにするために、英語用フォントの文字サイズに掛ける倍率（組み合わせごとに1回だけ計算）
    /// </summary>
    public static float CapHeightRatio(TMP_FontAsset from, TMP_FontAsset to)
    {
        if (from == null) from = TMP_Settings.defaultFontAsset;
        if (from == null || to == null || from == to) return 1f;
        if (s_capRatio.TryGetValue((from, to), out float r)) return r;
        float a = CharHeight(from, 'H'), b = CharHeight(to, 'H');
        r = a > 0f && b > 0f ? a / b : 1f;
        s_capRatio[(from, to)] = r;
        return r;
    }

    // ===================== TMP =====================

    /// <summary>
    /// TMPの文字を数字用フォントにする。戻り値＝元のフォントと数字の高さをそろえる倍率（すでに数字用・使えない時は1）。
    /// adjustSize=true なら今の文字サイズにその倍率を掛ける（文字サイズを1回だけ決める表示用）。
    /// 今付いている素材（縁取り等）は数字用の写しに置き換える
    /// </summary>
    public static float Apply(TMP_Text t, bool adjustSize = true)
    {
        if (t == null || !Enabled) return 1f;
        var font = Settings.tmpFont;
        if (t.font == font) return 1f;
        // 日本語の見出しフォント（Dela Gothic One）に切り替え中なら、元のフォント・大きさに戻してから数字用にする（数字だけの表示は数字用フォントを優先）
        var sw = t.GetComponent<LanguageFontSwitch>();
        if (sw != null) sw.ReleaseForNumberFont();
        TMP_FontAsset original = t.font;
        float mul = SizeMultiplierFrom(original);
        Material src = t.fontSharedMaterial;
        bool srcIsDefault = src == null || (original != null && src == original.material);
        t.font = font;
        t.fontSharedMaterial = srcIsDefault ? font.material : MaterialFor(src);
        if (adjustSize && !Mathf.Approximately(mul, 1f))
        {
            if (t.enableAutoSizing) { t.fontSizeMin *= mul; t.fontSizeMax *= mul; }
            else t.fontSize *= mul;
        }
        return mul;
    }

    /// <summary>元のフォントの「0」と同じ高さにするために、数字用フォントの文字サイズに掛ける倍率（1回だけ計算）</summary>
    public static float SizeMultiplierFrom(TMP_FontAsset original)
    {
        if (original == null || !Enabled) return 1f;
        var font = Settings.tmpFont;
        if (original == font) return 1f;
        if (s_tmpRatio.TryGetValue(original, out float r)) return r;
        float a = DigitHeight(original), b = DigitHeight(font);
        r = a > 0f && b > 0f ? a / b : 1f;
        s_tmpRatio[original] = r;
        return r;
    }

    private static float DigitHeight(TMP_FontAsset fa) => CharHeight(fa, '0');

    private static float CharHeight(TMP_FontAsset fa, char c)
    {
        var fi = fa.faceInfo;
        if (fi.pointSize <= 0f) return 0f;
        float s = fi.scale > 0f ? fi.scale : 1f;
        // 動的フォント（NotoSansJP等）にその文字がまだ無ければここで追加される（1回だけ）
        if (fa.HasCharacter(c, false, true) && fa.characterLookupTable.TryGetValue(c, out TMP_Character ch) && ch.glyph != null)
            return ch.glyph.metrics.height / fi.pointSize * s;
        return fi.capLine / fi.pointSize * s;
    }

    /// <summary>
    /// 数字用フォントの文字に付ける素材を返す。元の素材（従来フォント用の縁取り等）を、数字用フォントの文字画像に差し替えた写し（初回だけ作る）。
    /// 数字用フォントが使えない時や、すでに数字用の素材なら、そのまま返す
    /// </summary>
    public static Material MaterialFor(Material src) => Enabled ? MaterialFor(src, Settings.tmpFont) : src;

    /// <summary>元の素材（縁取り等）を、指定フォントの文字画像に差し替えた写し（初回だけ作って使い回す）</summary>
    public static Material MaterialFor(Material src, TMP_FontAsset font)
    {
        if (src == null || font == null) return src;
        if (src == font.material) return src;
        Texture atlas = font.atlasTexture;
        if (atlas != null && src.HasProperty(ShaderUtilities.ID_MainTex) && src.GetTexture(ShaderUtilities.ID_MainTex) == atlas) return src;
        if (s_variants.TryGetValue((src, font), out Material v) && v != null) return v;

        v = new Material(src) { name = src.name + " (" + font.name + ")" };
        Material fm = font.material;
        if (atlas != null) v.SetTexture(ShaderUtilities.ID_MainTex, atlas);
        CopyFloat(fm, v, ShaderUtilities.ID_TextureWidth);
        CopyFloat(fm, v, ShaderUtilities.ID_TextureHeight);
        CopyFloat(fm, v, ShaderUtilities.ID_GradientScale);
        CopyFloat(fm, v, ShaderUtilities.ID_WeightNormal);
        CopyFloat(fm, v, ShaderUtilities.ID_WeightBold);
        s_variants[(src, font)] = v;
        return v;
    }

    private static void CopyFloat(Material from, Material to, int id)
    {
        if (from != null && from.HasProperty(id) && to.HasProperty(id)) to.SetFloat(id, from.GetFloat(id));
    }

    // ===================== 日本語と数字が混ざった文字（「ランダム +2」「所持 10/32」など） =====================

    private static bool s_registered;
    private static readonly System.Text.StringBuilder s_sb = new System.Text.StringBuilder(64);
    private const string NumberChars = "0123456789+-/:%.,";

    /// <summary>
    /// 文字列の中の数字の部分（「+2」「10」「/32」など、数字を含む連続した数字・記号）だけを、TMPのリッチテキスト（&lt;font&gt;）で数字用フォントにする。
    /// 日本語・英語の部分は元のフォントのまま。&lt;color=#…&gt; などのタグの中は書き換えない。使えない時はそのまま返す。
    /// 数字用フォントは数字の高さが従来のフォント（NotoSansJP）と同じになるよう補正済みなので、混ざっても大きさはそろう
    /// </summary>
    public static string WrapNumbers(string text)
    {
        if (string.IsNullOrEmpty(text) || !Enabled) return text;
        var font = Settings.tmpFont;
        if (!s_registered)
        {
            // <font="名前">で見つけられるよう、数字用フォントを登録しておく（1回だけ）
            MaterialReferenceManager.AddFontAsset(font);
            s_registered = true;
        }
        string open = "<font=\"" + font.name + "\">";
        var sb = s_sb; sb.Clear();
        int i = 0, n = text.Length;
        while (i < n)
        {
            char c = text[i];
            if (c == '<')
            {
                // タグはそのまま写す
                int end = text.IndexOf('>', i);
                if (end < 0) { sb.Append(text, i, n - i); break; }
                sb.Append(text, i, end - i + 1);
                i = end + 1;
                continue;
            }
            if (NumberChars.IndexOf(c) >= 0)
            {
                int j = i; bool hasDigit = false;
                while (j < n && NumberChars.IndexOf(text[j]) >= 0) { if (char.IsDigit(text[j])) hasDigit = true; j++; }
                if (hasDigit) { sb.Append(open).Append(text, i, j - i).Append("</font>"); }
                else sb.Append(text, i, j - i);
                i = j;
                continue;
            }
            sb.Append(c);
            i++;
        }
        return sb.ToString();
    }

    // ===================== 旧式TextMesh =====================

    /// <summary>
    /// 旧式TextMeshを数字用フォントにする。戻り値＝元のフォントと数字の高さをそろえる倍率（characterSizeに掛ける）。
    /// characterSize は呼び出し側で掛けること（毎フレーム設定し直す所があるため）。使えない時・すでに数字用なら1
    /// </summary>
    public static float Apply(TextMesh tm)
    {
        if (tm == null || !LegacyEnabled) return 1f;
        var font = Settings.legacyFont;
        if (tm.font == font) return 1f;
        float mul = LegacySizeMultiplierFrom(tm.font);
        tm.font = font;
        var r = tm.GetComponent<MeshRenderer>();
        if (r != null) r.sharedMaterial = font.material;
        return mul;
    }

    /// <summary>元の旧式フォント（null＝Unity標準のフォント）の「0」と同じ高さにする倍率（1回だけ計算）</summary>
    public static float LegacySizeMultiplierFrom(Font original)
    {
        if (!LegacyEnabled) return 1f;
        var font = Settings.legacyFont;
        if (original == null)
        {
            if (s_builtinLegacy == null) s_builtinLegacy = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            original = s_builtinLegacy;
        }
        if (original == null || original == font) return 1f;
        if (s_legacyRatio.TryGetValue(original, out float r)) return r;
        const int size = 100;
        r = 1f;
        original.RequestCharactersInTexture("0", size);
        font.RequestCharactersInTexture("0", size);
        if (original.GetCharacterInfo('0', out CharacterInfo a, size) && font.GetCharacterInfo('0', out CharacterInfo b, size) && b.glyphHeight > 0)
            r = (float)a.glyphHeight / b.glyphHeight;
        s_legacyRatio[original] = r;
        return r;
    }
}
