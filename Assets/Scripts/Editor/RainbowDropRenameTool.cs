using Game.Localization;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 「無限化の石」を「虹の雫（Prism Drop）」に改名する（2026/10/23。課金ショップの商品名に合わせる）。
/// 翻訳表の文言（獲得通知・リザルト画面の獲得行）を書き換える。何度実行しても同じ結果。
/// 内部名（InfiniteStoneManager・PlayerPrefsのキー等）は変えない（保存データの互換のため）。
/// </summary>
public static class RainbowDropRenameTool
{
    private const string LocPath = "Assets/Resources/GameData/LocalizationTable.asset";

    private static readonly (string key, string ja, string en)[] Entries =
    {
        ("gem.infiniteStoneReward", "虹の雫を{0}個獲得しました！", "Obtained {0} Prism Drop!"),
        ("result.infiniteStone", "虹の雫 獲得！", "PRISM DROP"),
    };

    [MenuItem("Tools/ショップ/「無限化の石」を「虹の雫」に改名する（翻訳表）")]
    private static void Run()
    {
        var table = AssetDatabase.LoadAssetAtPath<LocalizationTable>(LocPath);
        if (table == null) { EditorUtility.DisplayDialog("虹の雫に改名", "翻訳表が見つかりません。", "OK"); return; }
        Undo.RecordObject(table, "Rename Infinite Stone");
        var log = new System.Text.StringBuilder();
        foreach (var (key, ja, en) in Entries)
        {
            var e = table.entries.Find(x => x != null && x.key == key);
            if (e == null) { e = new LocalizationTable.Entry { key = key }; table.entries.Add(e); }
            e.japanese = ja; e.english = en;
            log.AppendLine($"{key}：{ja} / {en}");
        }
        EditorUtility.SetDirty(table);
        AssetDatabase.SaveAssets();
        Debug.Log("[RainbowDropRenameTool] 翻訳表を「虹の雫」に書き換えました\n" + log);
        EditorUtility.DisplayDialog("虹の雫に改名", "翻訳表を書き換えました。内容はConsoleに出力しています。", "OK");
    }
}
