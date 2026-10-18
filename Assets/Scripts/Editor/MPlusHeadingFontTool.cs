using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

/// <summary>
/// 日本語のフォントを M PLUS 1 にする（見出し＝ExtraBold、説明文・長い文＝Medium）。
/// ・Assets/Fonts/MPLUS1-ExtraBold SDF.asset / MPLUS1-Medium SDF.asset：ゲーム内で使っている文字（翻訳表・シーン/Prefabの文字・データ・コード内の文字列）と英数字を
///   あらかじめ全部入れた「固定（Static）」の文字データ。ゲーム中に文字を作り足さない（OPPOのちらつき対策）
/// ・入っていない文字（記号など）は従来のNotoSansJPで表示される（予備フォント）
/// ・大きさ：漢字「国」の高さが従来のNotoSansJPと同じになるよう拡大率（Face Info > Scale）を合わせる（各所の文字サイズの値は変えない）
/// ・Assets/Resources/NumberFontSettings.asset の「日本語の見出し」「日本語の長文」に設定する（Mediumのファイルが無ければ見出しだけ）
/// ★今後、新しい文字（漢字など）を文言に使ったら、このメニューを実行し直すこと（入っていない文字はNotoSansJPで表示される）
/// </summary>
public static class MPlusHeadingFontTool
{
    private const string HeadingTtf = "Assets/Fonts/MPLUS1-ExtraBold.ttf";
    private const string HeadingAsset = "Assets/Fonts/MPLUS1-ExtraBold SDF.asset";
    private const string BodyTtf = "Assets/Fonts/MPLUS1-Medium.ttf";
    private const string BodyAsset = "Assets/Fonts/MPLUS1-Medium SDF.asset";
    private const string NotoAssetPath = "Assets/Fonts/NotoSansJP-Regular SDF.asset";
    private const string SettingsPath = "Assets/Resources/NumberFontSettings.asset";
    private const int AtlasSize = 2048;   // 文字の大きさ52＋余白5で1文字約62px → 2048×2048に約1080文字（足りなければ2枚目以降を作る）
    private const int SamplingSize = 52;
    private const int Padding = 5;
    private const char RefChar = '国';

    [MenuItem("Tools/日本語のフォント/M PLUS 1 の日本語フォントを作成・設定（見出し＋長文・固定）")]
    private static void Run()
    {
        var noto = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(NotoAssetPath);
        var settings = AssetDatabase.LoadAssetAtPath<NumberFontSettings>(SettingsPath);
        if (settings == null) { Debug.LogError("[MPlusHeadingFontTool] " + SettingsPath + " がありません"); return; }
        string chars = CollectCharacters();

        var heading = BuildStatic(HeadingTtf, HeadingAsset, chars, noto, "見出し（ExtraBold）");
        if (heading != null) settings.japaneseHeadingFont = heading;
        var body = AssetDatabase.LoadAssetAtPath<Font>(BodyTtf) != null ? BuildStatic(BodyTtf, BodyAsset, chars, noto, "長文（Medium）") : null;
        if (body != null) settings.japaneseBodyFont = body;
        else Debug.LogWarning("[MPlusHeadingFontTool] " + BodyTtf + " が無いため、長文用は作っていません");
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();
    }

    /// <summary>使う文字を全部入れた固定の文字データを作る（毎回作り直す）。大きさの補正・予備フォントの登録まで行う</summary>
    private static TMP_FontAsset BuildStatic(string ttfPath, string assetPath, string chars, TMP_FontAsset noto, string label)
    {
        var ttf = AssetDatabase.LoadAssetAtPath<Font>(ttfPath);
        if (ttf == null) { Debug.LogError("[MPlusHeadingFontTool] " + ttfPath + " が見つかりません"); return null; }
        string name = Path.GetFileNameWithoutExtension(assetPath);
        if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath) != null) AssetDatabase.DeleteAsset(assetPath);
        var fa = TMP_FontAsset.CreateFontAsset(ttf, SamplingSize, Padding, GlyphRenderMode.SDFAA, AtlasSize, AtlasSize, AtlasPopulationMode.Dynamic, true);
        if (fa == null) { Debug.LogError("[MPlusHeadingFontTool] フォントアセットを作れませんでした：" + name); return null; }
        fa.name = name;
        fa.TryAddCharacters(chars, out string missing);
        AssetDatabase.CreateAsset(fa, assetPath);
        var textures = fa.atlasTextures;
        for (int i = 0; i < textures.Length; i++)
        {
            if (textures[i] == null) continue;
            textures[i].name = name + " Atlas" + (i == 0 ? "" : " " + i);
            AssetDatabase.AddObjectToAsset(textures[i], fa);
        }
        var mat = fa.material;
        if (mat != null) { mat.name = name + " Material"; AssetDatabase.AddObjectToAsset(mat, fa); }
        fa.atlasPopulationMode = AtlasPopulationMode.Static; // ゲーム中に文字を作り足さない
        EditorUtility.SetDirty(fa);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(assetPath);
        fa = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);

        // 大きさ：漢字「国」の高さを従来のNotoSansJPと同じに
        float scale = 1f;
        if (noto != null && noto.HasCharacter(RefChar, false, true) && fa.characterLookupTable.ContainsKey(RefChar))
        {
            float notoH = CharHeight(noto, RefChar, false), myH = CharHeight(fa, RefChar, true);
            if (notoH > 0f && myH > 0f) scale = notoH / myH;
        }
        var fi = fa.faceInfo; fi.scale = scale; fa.faceInfo = fi;
        if (noto != null)
        {
            if (fa.fallbackFontAssetTable == null) fa.fallbackFontAssetTable = new List<TMP_FontAsset>();
            if (!fa.fallbackFontAssetTable.Contains(noto)) fa.fallbackFontAssetTable.Add(noto);
        }
        EditorUtility.SetDirty(fa);

        int missingCount = string.IsNullOrEmpty(missing) ? 0 : missing.Length;
        Debug.Log($"[MPlusHeadingFontTool] 日本語の{label}を M PLUS 1（固定）にしました：{assetPath}　" +
                  $"入れた文字 {chars.Length - missingCount}字／文字画像 {textures.Length}枚／大きさの補正 Scale={scale:F3}" +
                  (missingCount > 0 ? $"／フォントに無い文字 {missingCount}字（NotoSansJPで表示）：{missing}" : ""));
        return fa;
    }

    /// <summary>ゲーム内で使っている文字を集める：英数字・記号（ASCII）＋翻訳表・データ（.asset）・シーン/Prefabの文字・コード内の文字列（Editor以外）</summary>
    private static string CollectCharacters()
    {
        var set = new HashSet<char>();
        for (int c = 32; c <= 126; c++) set.Add((char)c);
        foreach (char c in "ー・、。「」『』（）！？：…～〜％＋－×÷　") set.Add(c);
        var unicode = new Regex(@"\\u([0-9A-Fa-f]{4})");
        foreach (string file in Directory.GetFiles("Assets", "*.*", SearchOption.AllDirectories))
        {
            if (file.Replace('\\', '/').StartsWith("Assets/Fonts/")) continue; // 文字データ自身（大きい）は見ない
            string ext = Path.GetExtension(file).ToLowerInvariant();
            bool isCode = ext == ".cs" && !file.Replace('\\', '/').Contains("/Editor/");
            if (ext != ".asset" && ext != ".unity" && ext != ".prefab" && !isCode) continue;
            string text;
            try { text = File.ReadAllText(file, Encoding.UTF8); } catch { continue; }
            if (isCode)
            {
                // コードはコメントの文字（説明文の漢字が大量にある）を入れないよう、行コメントを除いて文字列（"…"）の中身だけを見る
                var lit = new StringBuilder();
                string noComment = Regex.Replace(text, @"//[^\n]*", "");
                foreach (Match m in Regex.Matches(noComment, @"""(?:[^""\\\n]|\\.)*""")) lit.Append(m.Value);
                text = lit.ToString();
            }
            else
            {
                // YAMLの \uXXXX を文字に戻す
                text = unicode.Replace(text, m => ((char)System.Convert.ToInt32(m.Groups[1].Value, 16)).ToString());
            }
            foreach (char c in text)
                if (c >= 0x3000 && c <= 0x9FFF || c >= 0xFF00 && c <= 0xFFEF) set.Add(c); // かな・カタカナ・漢字・全角記号
        }
        var sb = new StringBuilder(set.Count);
        foreach (char c in set) sb.Append(c);
        return sb.ToString();
    }

    private static float CharHeight(TMP_FontAsset fa, char c, bool ignoreScale)
    {
        var fi = fa.faceInfo;
        if (fi.pointSize <= 0f) return 0f;
        float s = ignoreScale ? 1f : (fi.scale > 0f ? fi.scale : 1f);
        if (fa.characterLookupTable != null && fa.characterLookupTable.TryGetValue(c, out TMP_Character ch) && ch.glyph != null)
            return ch.glyph.metrics.height / fi.pointSize * s;
        return fi.capLine / fi.pointSize * s;
    }
}
