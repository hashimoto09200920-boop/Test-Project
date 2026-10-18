using TMPro;
using UnityEngine;

/// <summary>
/// 数字用フォント（Bungee）の設定。Assets/Resources/NumberFontSettings.asset に置く（GameNumberFontが実行時に読む）。
/// メニュー「Tools/数字のフォント/Bungeeの数字フォントを作成・設定」で作成・更新する。
/// </summary>
[CreateAssetMenu(menuName = "Game/Number Font Settings", fileName = "NumberFontSettings")]
public class NumberFontSettings : ScriptableObject
{
    [Tooltip("OFFにすると、全ての数字が従来のフォントに戻る")]
    public bool enabledNumberFont = true;

    [Tooltip("TMP（TextMeshPro）の数字に使うフォントアセット（Bungee-Regular SDF）。大きさはこのアセットのFace Info > Scaleで従来と同じ高さに補正済み")]
    public TMP_FontAsset tmpFont;

    [Tooltip("旧式TextMesh（敵のHPの数字等）に使うフォント（Bungee-Regular.ttf）")]
    public Font legacyFont;

    [Tooltip("旧式TextMeshの文字の大きさ（characterSize）に掛ける倍率。数字の高さを従来のフォント（NotoSansJP）と同じにするための値（メニューで自動計算）")]
    public float legacySizeScale = 1f;

    [Header("英語表示の時の文字（LanguageFontSwitchが使う）")]
    [Tooltip("英語の見出し・ボタン・短いラベルに使うフォント。数字用と同じBungee（tmpFont）を使う")]
    public bool useTmpFontForEnglishHeadings = true;
    [Tooltip("英語の説明文・長い文に使うフォントアセット（Rajdhani-SemiBold SDF）。大文字の高さは従来のフォント（NotoSansJP）と同じに補正済み")]
    public TMP_FontAsset englishBodyFont;

    [Header("日本語表示の時の見出し（LanguageFontSwitchが使う）")]
    [Tooltip("OFFにすると、日本語の見出しは従来のフォント（NotoSansJP）に戻る")]
    public bool useJapaneseHeadingFont = true;
    [Tooltip("日本語の見出し・ボタン・短いラベルに使うフォントアセット（MPLUS1-ExtraBold SDF）。漢字の高さは従来のフォント（NotoSansJP）と同じに補正済み。" +
             "メニュー「Tools/日本語のフォント/…」で作成・設定")]
    public TMP_FontAsset japaneseHeadingFont;
    [Tooltip("ON：日本語の見出しフォントにする時は太字（Bold）を外す（M PLUS 1 ExtraBoldは元々太いため、太字を重ねると潰れる）")]
    public bool removeBoldForJapaneseHeading = true;

    [Header("日本語表示の時の長文（説明文）（LanguageFontSwitch の種類＝Body が使う）")]
    [Tooltip("OFFにすると、日本語の説明文・長い文は従来のフォント（NotoSansJP）に戻る")]
    public bool useJapaneseBodyFont = true;
    [Tooltip("日本語の説明文・長い文に使うフォントアセット（MPLUS1-Medium SDF）。漢字の高さは従来のフォント（NotoSansJP）と同じに補正済み。" +
             "メニュー「Tools/日本語のフォント/…」で作成・設定")]
    public TMP_FontAsset japaneseBodyFont;
}
