using System.IO;
using Game.Shop;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 課金ショップ（IapSettings）・リワード広告（AdSettings）の設定アセットを作成・選択する（2026/10/23）。
/// 既にあれば選択するだけ（値は変えない）。
/// </summary>
public static class ShopSettingsMenu
{
    [MenuItem("Tools/ショップ/課金ショップの設定を開く（無ければ作成）")]
    private static void OpenIap()
    {
        OpenOrCreate<IapSettings>("Assets/Resources/IapSettings.asset");
        // 後から足した「英語の予備の値段」が空なら既定値を入れる（入っていれば変えない）
        var s = Selection.activeObject as IapSettings;
        if (s == null) return;
        bool changed = false;
        if (string.IsNullOrEmpty(s.premiumPassFallbackPriceEn)) { s.premiumPassFallbackPriceEn = IapSettings.DefaultFallbackPriceEn(s.premiumPassId); changed = true; }
        foreach (var p in s.stoneProducts)
            if (p != null && string.IsNullOrEmpty(p.fallbackPriceEn)) { p.fallbackPriceEn = IapSettings.DefaultFallbackPriceEn(p.productId); changed = true; }
        if (!changed) { Debug.Log("[ShopSettingsMenu] 英語の予備の値段は入力済みです（変更なし）"); return; }
        EditorUtility.SetDirty(s);
        AssetDatabase.SaveAssets();
        Debug.Log("[ShopSettingsMenu] 英語の予備の値段を入れました（Inspector で変更できます）");
    }

    [MenuItem("Tools/ショップ/広告の設定を開く（無ければ作成）")]
    private static void OpenAd() => OpenOrCreate<AdSettings>("Assets/Resources/AdSettings.asset");

    private static void OpenOrCreate<T>(string path) where T : ScriptableObject
    {
        var s = AssetDatabase.LoadAssetAtPath<T>(path);
        if (s == null)
        {
            if (!Directory.Exists("Assets/Resources")) Directory.CreateDirectory("Assets/Resources");
            s = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(s, path);
            AssetDatabase.SaveAssets();
            Debug.Log($"[ShopSettingsMenu] 設定を作成しました：{path}");
        }
        Selection.activeObject = s;
        EditorGUIUtility.PingObject(s);
    }
}
