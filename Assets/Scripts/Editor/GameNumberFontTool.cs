using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

/// <summary>
/// ゲーム全体の「数字だけの文字」に使う数字用フォント（Bungee）を作成・設定する。
/// ・Assets/Fonts/Bungee-Regular SDF.asset：使う文字だけを入れた固定の文字データ（実行中に作り足さない＝負荷が増えない）。
///   入っていない文字（∞など）は、従来のNotoSansJPで表示される（予備フォントに登録）
/// ・大きさ：数字「0」の高さが従来のNotoSansJPと同じになるよう、フォントアセットの拡大率（Face Info > Scale）を自動で合わせる
///   （各所の文字サイズの値は変えない）。旧式TextMesh用の倍率も計算して設定アセットに入れる
/// ・Assets/Resources/NumberFontSettings.asset：GameNumberFontが実行時に読む設定（OFFにすると全部従来のフォントに戻る）
/// ・アイテム取得ポップアップの数字も、このフォントに設定し直す（文字の縁取り素材も作り直す）
/// Bungee は SIL Open Font License（ゲームへの同梱・販売可）。
/// </summary>
public static class GameNumberFontTool
{
    private const string TtfPath = "Assets/Fonts/Bungee-Regular.ttf";
    private const string FontAssetPath = "Assets/Fonts/Bungee-Regular SDF.asset";
    private const string NotoAssetPath = "Assets/Fonts/NotoSansJP-Regular SDF.asset";
    private const string NotoTtfPath = "Assets/Fonts/NotoSansJP-Regular.ttf";
    private const string SettingsPath = "Assets/Resources/NumberFontSettings.asset";
    private const string PopupPrefabPath = "Assets/Prefabs/BlockItemPopupText.prefab";
    private const string BodyTtfPath = "Assets/Fonts/Rajdhani-SemiBold.ttf";
    private const string BodyAssetPath = "Assets/Fonts/Rajdhani-SemiBold SDF.asset";
    // 英数字と記号（スペース〜~ の95文字）。数字・英語の見出し（Bungee）と英語の説明文（Rajdhani）の両方に使う
    private static readonly string Characters = BuildAscii();
    private const int AtlasSize = 1024;    // 文字の大きさ72＋余白8で1文字約88px → 1024×1024なら11×11＝121文字まで入る（今は95文字）
    private const int SamplingSize = 72;
    private const int Padding = 8;

    private static string BuildAscii()
    {
        var sb = new System.Text.StringBuilder();
        for (int c = 32; c <= 126; c++) sb.Append((char)c);
        return sb.ToString();
    }

    [MenuItem("Tools/数字のフォント/Bungeeの数字フォントを作成・設定")]
    private static void Run()
    {
        var ttf = AssetDatabase.LoadAssetAtPath<Font>(TtfPath);
        if (ttf == null) { Debug.LogError("[GameNumberFontTool] " + TtfPath + " が見つかりません"); return; }
        var noto = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(NotoAssetPath);

        // ===== Bungee（数字・英語の見出し）：数字「0」の高さを従来のNotoSansJPと同じに =====
        var fa = EnsureStaticFont(ttf, FontAssetPath, "Bungee-Regular SDF", noto, '0');
        if (fa == null) return;

        // ===== Rajdhani（英語の説明文）：大文字「H」の高さを従来のNotoSansJPと同じに（無ければ作らない） =====
        var bodyTtf = AssetDatabase.LoadAssetAtPath<Font>(BodyTtfPath);
        TMP_FontAsset body = bodyTtf != null ? EnsureStaticFont(bodyTtf, BodyAssetPath, "Rajdhani-SemiBold SDF", noto, 'H') : null;

        // ===== 旧式TextMesh用の倍率（同じく「0」の高さをそろえる） =====
        float legacy = LegacyRatio(AssetDatabase.LoadAssetAtPath<Font>(NotoTtfPath), ttf);

        // ===== 設定アセット =====
        var settings = AssetDatabase.LoadAssetAtPath<NumberFontSettings>(SettingsPath);
        if (settings == null)
        {
            if (!Directory.Exists("Assets/Resources")) Directory.CreateDirectory("Assets/Resources");
            settings = ScriptableObject.CreateInstance<NumberFontSettings>();
            AssetDatabase.CreateAsset(settings, SettingsPath);
        }
        settings.tmpFont = fa;
        settings.legacyFont = ttf;
        settings.legacySizeScale = legacy;
        settings.englishBodyFont = body;
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();

        // ===== アイテム取得ポップアップの数字（プレハブに直接設定。縁取り素材も作り直す） =====
        GameObject root = PrefabUtility.LoadPrefabContents(PopupPrefabPath);
        if (root != null)
        {
            try
            {
                var label = root.GetComponentInChildren<TextMeshPro>(true);
                if (label != null)
                {
                    label.font = fa;
                    label.fontSharedMaterial = fa.material;
                    EditorUtility.SetDirty(label);
                    PrefabUtility.SaveAsPrefabAsset(root, PopupPrefabPath);
                }
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            ItemCollectFxTool.Run();
        }

        Debug.Log($"[GameNumberFontTool] 数字・英語用フォントを設定しました：{FontAssetPath}" + (body != null ? "、" + BodyAssetPath : "（Rajdhaniは未作成）") +
                  $"（英数字・記号95文字、{AtlasSize}×{AtlasSize}・固定）、旧式TextMesh用の倍率={legacy:F3}、設定={SettingsPath}");
    }

    /// <summary>
    /// 使う文字だけを入れた固定（Static）の文字データを作る（足りない文字があれば作り直す）。
    /// 文字 refChar の高さが従来のフォント（NotoSansJP）と同じになるよう、拡大率（Face Info > Scale）を合わせる。
    /// 入っていない文字（日本語・∞など）は従来のフォントで出すよう予備フォントに登録する
    /// </summary>
    private static TMP_FontAsset EnsureStaticFont(Font ttf, string path, string name, TMP_FontAsset noto, char refChar)
    {
        var fa = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
        if (fa != null && !HasAll(fa)) { AssetDatabase.DeleteAsset(path); fa = null; }
        if (fa == null)
        {
            fa = TMP_FontAsset.CreateFontAsset(ttf, SamplingSize, Padding, GlyphRenderMode.SDFAA, AtlasSize, AtlasSize, AtlasPopulationMode.Dynamic, false);
            if (fa == null) { Debug.LogError("[GameNumberFontTool] フォントアセットを作れませんでした：" + name); return null; }
            fa.name = name;
            if (!fa.TryAddCharacters(Characters, out string missing))
                Debug.LogWarning($"[GameNumberFontTool] {name}：文字画像に入らなかった文字があります：" + missing);
            AssetDatabase.CreateAsset(fa, path);
            var atlas = fa.atlasTexture;
            if (atlas != null) { atlas.name = name + " Atlas"; AssetDatabase.AddObjectToAsset(atlas, fa); }
            var mat = fa.material;
            if (mat != null) { mat.name = name + " Material"; AssetDatabase.AddObjectToAsset(mat, fa); }
            fa.atlasPopulationMode = AtlasPopulationMode.Static;
            EditorUtility.SetDirty(fa);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(path);
            fa = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
        }

        float scale = 1f;
        float notoH = CharHeight(noto, refChar), myH = CharHeight(fa, refChar, ignoreScale: true);
        if (notoH > 0f && myH > 0f) scale = notoH / myH;
        var fi = fa.faceInfo; fi.scale = scale; fa.faceInfo = fi;
        if (noto != null)
        {
            if (fa.fallbackFontAssetTable == null) fa.fallbackFontAssetTable = new List<TMP_FontAsset>();
            if (!fa.fallbackFontAssetTable.Contains(noto)) fa.fallbackFontAssetTable.Add(noto);
        }
        EditorUtility.SetDirty(fa);
        Debug.Log($"[GameNumberFontTool] {name}：大きさの補正 Scale={scale:F3}（「{refChar}」の高さを従来のフォントにそろえた）");
        return fa;
    }

    private static bool HasAll(TMP_FontAsset fa)
    {
        foreach (char ch in Characters)
            if (!fa.HasCharacter(ch, false, false)) return false;
        return true;
    }

    /// <summary>そのフォントで文字 c を描いた時の高さ（文字サイズ1あたり）。取れなければ大文字の高さ（Cap Line）で代用</summary>
    private static float CharHeight(TMP_FontAsset fa, char c, bool ignoreScale = false)
    {
        if (fa == null) return 0f;
        var fi = fa.faceInfo;
        if (fi.pointSize <= 0f) return 0f;
        float s = ignoreScale ? 1f : (fi.scale > 0f ? fi.scale : 1f);
        if (fa.characterLookupTable != null && fa.characterLookupTable.TryGetValue(c, out TMP_Character ch) && ch.glyph != null)
            return ch.glyph.metrics.height / fi.pointSize * s;
        return fi.capLine / fi.pointSize * s;
    }

    /// <summary>旧式TextMesh：同じ文字サイズで「0」を描いた時の高さの比（従来÷Bungee）</summary>
    private static float LegacyRatio(Font noto, Font bungee)
    {
        if (noto == null || bungee == null) return 1f;
        const int size = 100;
        noto.RequestCharactersInTexture("0", size);
        bungee.RequestCharactersInTexture("0", size);
        if (noto.GetCharacterInfo('0', out CharacterInfo a, size) && bungee.GetCharacterInfo('0', out CharacterInfo b, size) && b.glyphHeight > 0)
            return (float)a.glyphHeight / b.glyphHeight;
        return 1f;
    }
}
