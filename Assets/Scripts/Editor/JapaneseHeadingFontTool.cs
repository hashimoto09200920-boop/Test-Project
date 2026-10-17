using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

/// <summary>
/// 日本語の見出し・ボタン・短いラベルに使うフォント（Dela Gothic One）を作成・設定する。
/// ・Assets/Fonts/DelaGothicOne-Regular SDF.asset：表示する時に使った文字だけを作り足す方式（Dynamic。従来のNotoSansJPと同じ）。
///   文字画像が一杯になったら2枚目以降を自動で足す（Multi Atlas）。ビルド時は作り足した文字を消して容量を抑える（Clear Dynamic Data On Build）
/// ・大きさ：漢字「国」の高さが従来のNotoSansJPと同じになるよう、拡大率（Face Info > Scale）を自動で合わせる（各所の文字サイズの値は変えない）
/// ・入っていない文字（①〜⑬・＋・⇄・─ など）は従来のNotoSansJPで表示される（予備フォントに登録）
/// ・Assets/Resources/NumberFontSettings.asset の「日本語表示の時の見出し」に設定する（OFFにすると従来のフォントに戻る）
/// 切り替えるのは LanguageFontSwitch（種類＝Heading）が付いた文字だけ。
/// Dela Gothic One は SIL Open Font License（ゲームへの同梱・販売可）。
/// </summary>
public static class JapaneseHeadingFontTool
{
    private const string TtfPath = "Assets/Fonts/DelaGothicOne-Regular.ttf";
    private const string FontAssetPath = "Assets/Fonts/DelaGothicOne-Regular SDF.asset";
    private const string NotoAssetPath = "Assets/Fonts/NotoSansJP-Regular SDF.asset";
    private const string SettingsPath = "Assets/Resources/NumberFontSettings.asset";
    private const string AssetName = "DelaGothicOne-Regular SDF";
    private const int AtlasSize = 1024;   // 文字の大きさ72＋余白8で1文字約88px → 1枚に約130文字。足りなければ自動で2枚目以降
    private const int SamplingSize = 72;
    private const int Padding = 8;
    private const char RefChar = '国';

    [MenuItem("Tools/日本語のフォント/Dela Gothic Oneの見出しフォントを作成・設定")]
    private static void Run()
    {
        var ttf = AssetDatabase.LoadAssetAtPath<Font>(TtfPath);
        if (ttf == null) { Debug.LogError("[JapaneseHeadingFontTool] " + TtfPath + " が見つかりません"); return; }
        var noto = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(NotoAssetPath);
        var settings = AssetDatabase.LoadAssetAtPath<NumberFontSettings>(SettingsPath);
        if (settings == null) { Debug.LogError("[JapaneseHeadingFontTool] " + SettingsPath + " がありません。先に「Tools/数字のフォント/Bungeeの数字フォントを作成・設定」を実行してください"); return; }

        var fa = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
        if (fa == null)
        {
            fa = TMP_FontAsset.CreateFontAsset(ttf, SamplingSize, Padding, GlyphRenderMode.SDFAA, AtlasSize, AtlasSize, AtlasPopulationMode.Dynamic, true);
            if (fa == null) { Debug.LogError("[JapaneseHeadingFontTool] フォントアセットを作れませんでした"); return; }
            fa.name = AssetName;
            AssetDatabase.CreateAsset(fa, FontAssetPath);
            var atlas = fa.atlasTexture;
            if (atlas != null) { atlas.name = AssetName + " Atlas"; AssetDatabase.AddObjectToAsset(atlas, fa); }
            var mat = fa.material;
            if (mat != null) { mat.name = AssetName + " Material"; AssetDatabase.AddObjectToAsset(mat, fa); }
            EditorUtility.SetDirty(fa);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(FontAssetPath);
            fa = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
        }

        // ===== 大きさ：漢字「国」の高さを従来のNotoSansJPと同じに =====
        float scale = 1f;
        if (noto != null && noto.HasCharacter(RefChar, false, true) && fa.HasCharacter(RefChar, false, true))
        {
            float notoH = CharHeight(noto, RefChar, false), myH = CharHeight(fa, RefChar, true);
            if (notoH > 0f && myH > 0f) scale = notoH / myH;
        }
        else Debug.LogWarning("[JapaneseHeadingFontTool] 「国」の高さが取れなかったため、大きさの補正をしていません（Scale=1）");
        var fi = fa.faceInfo; fi.scale = scale; fa.faceInfo = fi;

        // 入っていない文字は従来のフォントで出す
        if (noto != null)
        {
            if (fa.fallbackFontAssetTable == null) fa.fallbackFontAssetTable = new List<TMP_FontAsset>();
            if (!fa.fallbackFontAssetTable.Contains(noto)) fa.fallbackFontAssetTable.Add(noto);
        }
        // ビルド時は作り足した文字を消す（NotoSansJPと同じ設定）
        var so = new SerializedObject(fa);
        var clear = so.FindProperty("m_ClearDynamicDataOnBuild");
        if (clear != null) { clear.boolValue = true; so.ApplyModifiedPropertiesWithoutUndo(); }
        EditorUtility.SetDirty(fa);

        settings.japaneseHeadingFont = fa;
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();

        Debug.Log($"[JapaneseHeadingFontTool] 日本語の見出しフォントを設定しました：{FontAssetPath}（{AtlasSize}×{AtlasSize}・表示時に作り足す方式・複数枚可）" +
                  $"、大きさの補正 Scale={scale:F3}（「{RefChar}」の高さを従来のフォントにそろえた）、設定={SettingsPath}");
    }

    /// <summary>そのフォントで文字 c を描いた時の高さ（文字サイズ1あたり）。取れなければ大文字の高さ（Cap Line）で代用</summary>
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
