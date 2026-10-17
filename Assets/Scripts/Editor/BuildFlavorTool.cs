using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEngine;

/// <summary>
/// ビルドの種類（ベータ用／本番用）を切り替えるメニューと、Androidビルド時の設定（自動バックアップ・ベータ用のアプリ名）。
///
/// 【ビルド手順】メニュー「Tools/ビルド/ベータ用に切り替え」または「本番用に切り替え」→ 通常どおりBuild。
/// 切り替えで変わるもの：
///   ・Assets/Resources/BuildFlavorConfig.asset の Flavor（ゲーム内のジェム・スタミナ無制限の扱い）
///   ・Androidのパッケージ名（ベータ用は末尾に「.beta」＝本番用と別アプリとして1台に同時に入る）
///   ・デバッグボタンの表示（Assets/Data/DebugButtonsConfig.asset の Show Debug Buttons をOFF。どちらのビルドでも非表示）
/// Androidビルド時に自動で変わるもの（このファイルのOnPostGenerateGradleAndroidProject）：
///   ・自動バックアップ（BuildFlavorConfigの Allow Android Backup がOFFなら無効）
///   ・ベータ用のアプリ名（ホーム画面の表示名の末尾に「 β」）
/// </summary>
public class BuildFlavorTool : IPostGenerateGradleAndroidProject
{
    private const string ConfigPath = "Assets/Resources/BuildFlavorConfig.asset";
    private const string DebugButtonsConfigPath = "Assets/Data/DebugButtonsConfig.asset";

    public int callbackOrder => 100;

    [MenuItem("Tools/ビルド/ベータ用に切り替え（ジェム・スタミナ無制限）")]
    private static void SwitchToBeta() => Switch(BuildFlavorConfig.Flavor.Beta);

    [MenuItem("Tools/ビルド/本番用に切り替え（制限あり）")]
    private static void SwitchToRelease() => Switch(BuildFlavorConfig.Flavor.Release);

    [MenuItem("Tools/ビルド/今のビルドの種類を表示")]
    private static void ShowCurrent()
    {
        var c = AssetDatabase.LoadAssetAtPath<BuildFlavorConfig>(ConfigPath);
        string id = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
        Debug.Log(c == null
            ? $"[BuildFlavor] 設定アセットがありません（本番用として扱われます）。パッケージ名={id}"
            : $"[BuildFlavor] 今のビルドの種類：{(c.flavor == BuildFlavorConfig.Flavor.Beta ? "ベータ用（ジェム・スタミナ無制限）" : "本番用（制限あり）")}" +
              $"　パッケージ名={id}　自動バックアップ={(c.allowAndroidBackup ? "許可" : "無効")}");
    }

    private static BuildFlavorConfig LoadOrCreateConfig()
    {
        var c = AssetDatabase.LoadAssetAtPath<BuildFlavorConfig>(ConfigPath);
        if (c != null) return c;

        c = ScriptableObject.CreateInstance<BuildFlavorConfig>();
        // 本番用のパッケージ名は、今のAndroidのパッケージ名（末尾が.betaなら外す）を引き継ぐ
        string current = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
        if (string.IsNullOrEmpty(current))
            current = "com." + Sanitize(PlayerSettings.companyName) + "." + Sanitize(PlayerSettings.productName);
        if (current.EndsWith(c.betaPackageSuffix)) current = current.Substring(0, current.Length - c.betaPackageSuffix.Length);
        c.releaseAndroidPackageName = current;
        if (!Directory.Exists("Assets/Resources")) Directory.CreateDirectory("Assets/Resources");
        AssetDatabase.CreateAsset(c, ConfigPath);
        Debug.Log($"[BuildFlavor] 設定アセットを作成しました：{ConfigPath}（本番用パッケージ名={current}）");
        return c;
    }

    private static string Sanitize(string s) => Regex.Replace(s ?? "", "[^A-Za-z0-9_]", "");

    private static void Switch(BuildFlavorConfig.Flavor flavor)
    {
        var c = LoadOrCreateConfig();
        c.flavor = flavor;
        EditorUtility.SetDirty(c);

        string id = flavor == BuildFlavorConfig.Flavor.Beta
            ? c.releaseAndroidPackageName + c.betaPackageSuffix
            : c.releaseAndroidPackageName;
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, id);

        // デバッグボタンはどちらのビルドでも非表示
        var dbg = AssetDatabase.LoadAssetAtPath<ScriptableObject>(DebugButtonsConfigPath);
        string dbgMsg = "（DebugButtonsConfigが見つかりません）";
        if (dbg != null)
        {
            var so = new SerializedObject(dbg);
            var p = so.FindProperty("showDebugButtons");
            if (p != null)
            {
                p.boolValue = false;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(dbg);
                dbgMsg = "デバッグボタン=非表示";
            }
        }

        string iconMsg = ApplyAndroidIcons(c, flavor);

        AssetDatabase.SaveAssets();
        Debug.Log($"[BuildFlavor] {iconMsg}");
        Debug.Log($"[BuildFlavor] {(flavor == BuildFlavorConfig.Flavor.Beta ? "ベータ用（ジェム・スタミナ無制限）" : "本番用（制限あり）")}に切り替えました。" +
                  $"パッケージ名={id}　{dbgMsg}　自動バックアップ={(c.allowAndroidBackup ? "許可" : "無効")}。このままBuildしてください");
    }

    /// <summary>Androidのアプリアイコン（Adaptive・Round・Legacy）を、ビルドの種類に合わせて設定する</summary>
    private static string ApplyAndroidIcons(BuildFlavorConfig c, BuildFlavorConfig.Flavor flavor)
    {
        string iconPath = flavor == BuildFlavorConfig.Flavor.Beta ? c.betaIconPath : c.releaseIconPath;
        var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(iconPath);
        if (icon == null) return $"アイコン画像が見つからないため、アイコンは変更していません（{iconPath}）";
        var fg = AssetDatabase.LoadAssetAtPath<Texture2D>(c.adaptiveForegroundPath);

        var target = NamedBuildTarget.Android;
        int n = 0;
        // Adaptive：1枚目＝背景（アイコンの絵）、2枚目＝前景（透明）
        var adaptive = PlayerSettings.GetPlatformIcons(target, UnityEditor.Android.AndroidPlatformIconKind.Adaptive);
        foreach (var pi in adaptive) { pi.SetTextures(icon, fg); n++; }
        PlayerSettings.SetPlatformIcons(target, UnityEditor.Android.AndroidPlatformIconKind.Adaptive, adaptive);
        var round = PlayerSettings.GetPlatformIcons(target, UnityEditor.Android.AndroidPlatformIconKind.Round);
        foreach (var pi in round) { pi.SetTextures(icon); n++; }
        PlayerSettings.SetPlatformIcons(target, UnityEditor.Android.AndroidPlatformIconKind.Round, round);
        var legacy = PlayerSettings.GetPlatformIcons(target, UnityEditor.Android.AndroidPlatformIconKind.Legacy);
        foreach (var pi in legacy) { pi.SetTextures(icon); n++; }
        PlayerSettings.SetPlatformIcons(target, UnityEditor.Android.AndroidPlatformIconKind.Legacy, legacy);
        return $"Androidのアイコンを{(flavor == BuildFlavorConfig.Flavor.Beta ? "ベータ用" : "本番用")}に設定しました（{iconPath}、{n}サイズ分）" +
               (fg == null ? "。※Adaptiveの前景画像が見つからないため前景なし" : "");
    }

    [MenuItem("Tools/ビルド/アイコンだけ今のビルドの種類に合わせて設定し直す")]
    private static void ReapplyIcons()
    {
        var c = LoadOrCreateConfig();
        string msg = ApplyAndroidIcons(c, c.flavor);
        AssetDatabase.SaveAssets();
        Debug.Log($"[BuildFlavor] {msg}");
    }

    // ===== Androidビルド時：自動バックアップ・ベータ用のアプリ名 =====
    public void OnPostGenerateGradleAndroidProject(string unityLibraryPath)
    {
        var c = AssetDatabase.LoadAssetAtPath<BuildFlavorConfig>(ConfigPath);
        bool allowBackup = c != null && c.allowAndroidBackup;
        bool isBeta = c != null && c.flavor == BuildFlavorConfig.Flavor.Beta;
        string launcherPath = Path.Combine(Path.GetDirectoryName(unityLibraryPath), "launcher");

        // 自動バックアップ：アプリ本体（launcher）のマニフェストで明示し、ライブラリ側の指定より優先させる
        string manifest = Path.Combine(launcherPath, "src", "main", "AndroidManifest.xml");
        if (File.Exists(manifest))
        {
            string xml = File.ReadAllText(manifest);
            if (!xml.Contains("xmlns:tools="))
                xml = Regex.Replace(xml, "<manifest\\b", "<manifest xmlns:tools=\"http://schemas.android.com/tools\"", RegexOptions.None, System.TimeSpan.FromSeconds(1));
            xml = Regex.Replace(xml, "\\s+android:allowBackup=\"[^\"]*\"", "");
            xml = Regex.Replace(xml, "\\s+android:fullBackupContent=\"[^\"]*\"", "");
            xml = Regex.Replace(xml, "\\s+tools:replace=\"[^\"]*\"", "");
            string attrs = $" android:allowBackup=\"{(allowBackup ? "true" : "false")}\"" +
                           (allowBackup ? "" : " android:fullBackupContent=\"false\"") +
                           (allowBackup ? " tools:replace=\"android:allowBackup\"" : " tools:replace=\"android:allowBackup,android:fullBackupContent\"");
            xml = Regex.Replace(xml, "<application\\b", "<application" + attrs);
            File.WriteAllText(manifest, xml);
            Debug.Log($"[BuildFlavor] Androidの自動バックアップ：{(allowBackup ? "許可" : "無効")}（{manifest}）");
        }
        else
        {
            Debug.LogWarning($"[BuildFlavor] launcherのAndroidManifest.xmlが見つからず、自動バックアップの設定をできませんでした：{manifest}");
        }

        // ベータ用のアプリ名（ホーム画面の表示名）
        if (isBeta)
        {
            string strings = Path.Combine(launcherPath, "src", "main", "res", "values", "strings.xml");
            if (File.Exists(strings))
            {
                string xml = File.ReadAllText(strings);
                string suffix = System.Security.SecurityElement.Escape(c.betaAppNameSuffix ?? "");
                xml = Regex.Replace(xml, "(<string name=\"app_name\">)(.*?)(</string>)", m =>
                    m.Groups[2].Value.EndsWith(suffix) ? m.Value : m.Groups[1].Value + m.Groups[2].Value + suffix + m.Groups[3].Value);
                File.WriteAllText(strings, xml);
                Debug.Log($"[BuildFlavor] ベータ用のアプリ名に「{c.betaAppNameSuffix}」を付けました");
            }
            else
            {
                Debug.LogWarning($"[BuildFlavor] strings.xmlが見つからず、ベータ用のアプリ名を変更できませんでした：{strings}");
            }
        }
    }
}
