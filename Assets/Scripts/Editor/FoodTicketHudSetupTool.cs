using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 画面右上の虹の雫の下に、お食事券の所持数（FoodTicketHUD）を作る（2026/11/11）。
/// ・02_Gem・03_AreaSelect・04_Drink・07_Shop：InfiniteStoneHUD（下地 FX_Plate・アイコン・数字）をそのまま写して、
///   虹の雫の1段下（ゴールドと虹の雫の間隔と同じだけ下）に置く。アイコンをお食事券に、数字はお食事券の枚数に
/// ・02_Gem・03_AreaSelect：新しい表示と重ならないよう、右下のボタン（ジェム・フード・バザール）と、反対側のボタン（ミッション・戻る）を同じだけ下げる
///   （お食事券の所持数を初めて作った時だけ。何度実行しても二重に下がらない）
/// ・フード画面のお食事券ボタンの枚数（TicketButton > TicketCount）は消す（所持数は右上に出すため）
/// </summary>
public static class FoodTicketHudSetupTool
{
    private static readonly string[] ScenePaths =
    {
        "Assets/Scenes/02_Gem.unity",
        "Assets/Scenes/03_AreaSelect.unity",
        "Assets/Scenes/04_Drink.unity",
        "Assets/Scenes/07_Shop.unity",
    };
    private static readonly string[] ButtonsToLower = { "GemManagementButton", "ShopButton", "IapShopButton", "MissionButton", "BackButton" };

    [MenuItem("Tools/画面共通/右上にお食事券の所持数を作る（02_Gem・03_AreaSelect・04_Drink・07_Shop）")]
    private static void Run()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var setup = EditorSceneManager.GetSceneManagerSetup();
        Sprite ticket = LoadFirstSprite(BlockItemManager.FoodTicketSpritePath);
        var log = new System.Text.StringBuilder();
        try
        {
            foreach (string path in ScenePaths)
            {
                Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                string sn = scene.name;
                RectTransform stone = null, gold = null, existing = null;
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var rt in root.GetComponentsInChildren<RectTransform>(true))
                    {
                        string n = rt.name.Trim();
                        if (n == "InfiniteStoneHUD" && rt.GetComponent<InfiniteStoneHUD>() != null && stone == null) stone = rt;
                        else if (n == "GoldHUD" && gold == null) gold = rt;
                        else if (n == "FoodTicketHUD" && existing == null) existing = rt;
                    }
                if (stone == null) { log.AppendLine($"・{sn}：InfiniteStoneHUD が見つかりません（作りませんでした）"); continue; }

                Vector2 step = gold != null ? stone.anchoredPosition - gold.anchoredPosition : new Vector2(0f, -70f); // 1段の間隔（ゴールド→虹の雫と同じ）
                bool created = false;
                RectTransform hud = existing;
                if (hud == null)
                {
                    var go = Object.Instantiate(stone.gameObject, stone.parent);
                    go.name = "FoodTicketHUD";
                    go.transform.SetSiblingIndex(stone.GetSiblingIndex() + 1);
                    var old = go.GetComponent<InfiniteStoneHUD>();
                    if (old != null) Object.DestroyImmediate(old);
                    hud = (RectTransform)go.transform;
                    hud.anchoredPosition = stone.anchoredPosition + step;
                    created = true;
                }

                // アイコン（下地 FX_Plate 以外の Image）と数字
                Image icon = null; TextMeshProUGUI text = null;
                foreach (Transform c in hud)
                {
                    if (c.name == "FX_Plate") continue;
                    if (icon == null) icon = c.GetComponent<Image>();
                    if (text == null) text = c.GetComponent<TextMeshProUGUI>();
                }
                if (icon != null) { icon.name = "TicketIcon"; if (ticket != null) icon.sprite = ticket; icon.preserveAspect = true; }
                if (text != null) { text.name = "TicketText"; text.text = "0"; }
                var comp = hud.GetComponent<FoodTicketHUD>();
                if (comp == null) comp = hud.gameObject.AddComponent<FoodTicketHUD>();
                comp.EditorWire(icon, text);
                EditorUtility.SetDirty(comp);
                log.AppendLine($"・{sn}：お食事券の所持数を{(created ? "作りました" : "設定し直しました")}（位置 {hud.anchoredPosition}）");

                // 重なるボタンを下げる（初めて作った時だけ）
                if (created && (sn == "02_Gem" || sn == "03_AreaSelect"))
                {
                    float down = Mathf.Abs(step.y);
                    foreach (var root in scene.GetRootGameObjects())
                        foreach (var rt in root.GetComponentsInChildren<RectTransform>(true))
                        {
                            if (System.Array.IndexOf(ButtonsToLower, rt.name.Trim()) < 0) continue;
                            if (rt.GetComponent<Button>() == null) continue;
                            rt.anchoredPosition += new Vector2(0f, -down);
                            EditorUtility.SetDirty(rt);
                            log.AppendLine($"    ・{rt.name} を {down} 下げました（{rt.anchoredPosition}）");
                        }
                }

                // フード画面のお食事券ボタンの枚数（TicketCount）を消す
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var rt in root.GetComponentsInChildren<RectTransform>(true))
                    {
                        if (rt == null || rt.name != "TicketCount" || rt.parent == null || rt.parent.name != "TicketButton") continue;
                        Object.DestroyImmediate(rt.gameObject);
                        log.AppendLine("    ・お食事券ボタンの枚数（TicketButton > TicketCount）を消しました");
                        break;
                    }

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
        }
        finally
        {
            if (setup != null && setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
        }
        if (ticket == null) log.AppendLine("\n※お食事券の画像（" + BlockItemManager.FoodTicketSpritePath + "）が読めません。アイコンは虹の雫のままです。");
        Debug.Log("[FoodTicketHudSetupTool]\n" + log);
        EditorUtility.DisplayDialog("お食事券の所持数", log.ToString(), "OK");
    }

    private static Sprite LoadFirstSprite(string path)
    {
        var s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (s != null) return s;
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path)) if (o is Sprite sp) return sp;
        return null;
    }
}
