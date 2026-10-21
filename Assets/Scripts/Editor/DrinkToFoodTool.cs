using Game.Localization;
using Game.Shop;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// ドリンク画面をフード画面に差し替える（2026/10/21。ドリンク名・お酒の絵でレーティングが18歳以上になったため）。
/// 効果・説明文・内部名（04_Drink・DrinkDefinition等）は変えず、見た目と名前だけを変える。
/// ・12個の DrinkDefinition（Resources/GameData/Drinks）：アイコンを Food_N.png に、名前（drinkName）をフード名に
/// ・翻訳表 LocalizationTable：drink.Drink_ N.name（日本語・英語）、中断メニューHELPの「ドリンク／飲み物」の文言
/// ・04_Drink と 03_AreaSelect の ShopUI：背景（ShopBgImage）をスイーツカフェに、バーテンダー・カウンター・お客を非表示に
/// 何度実行しても同じ結果（値をそのまま入れる）。
/// まだ作っていない画像（購入済みの印・個数アイコン・AreaSelectのボタン）は対象外。
/// </summary>
public static class DrinkToFoodTool
{
    private const string ShopArt = "Assets/Art/AreaSelect/Shop/";
    private const string BgPath = ShopArt + "背景 SweetsCafeBg.png";
    private const string DrinkDataFolder = "Assets/Resources/GameData/Drinks/";
    private const string LocPath = "Assets/Resources/GameData/LocalizationTable.asset";
    private static readonly string[] ScenePaths = { "Assets/Scenes/04_Drink.unity", "Assets/Scenes/03_AreaSelect.unity" };
    // アイコンの表示サイズ（フードの絵はすべて正方形500x500なので同じ大きさにそろえる。各DrinkDefinitionのInspectorで個別に調整可）
    private static readonly Vector2 IconSize = new Vector2(120f, 120f);

    private static readonly (string ja, string en)[] FoodNames =
    {
        ("ホワイト・マカロン", "White Macaron"),
        ("レッド・マカロン", "Red Macaron"),
        ("パラレル・スティック", "Parallel Sticks"),
        ("ミラー・キャンディ", "Mirror Candy"),
        ("クラッシュ・クッキー", "Crash Cookie"),
        ("クロノ・ドーナツ", "Chrono Donut"),
        ("スチール・バーガー", "Steel Burger"),
        ("サバイヴ・おにぎり", "Survive Onigiri"),
        ("グラビティ・パンケーキ", "Gravity Pancakes"),
        ("クリティカル・タルト", "Critical Tart"),
        ("ブレイク・パイ", "Break Pie"),
        ("ゼン・団子", "Zen Dango"),
    };

    [MenuItem("Tools/フード画面/ドリンク画面をフード画面に差し替える（名前・アイコン・背景・HELP）")]
    private static void Run()
    {
        if (!EditorUtility.DisplayDialog("フード画面に差し替え",
                "ドリンク12個の名前とアイコン、翻訳表（名前・HELP）、04_Drink／03_AreaSelect の背景を差し替え、バーテンダー・カウンター・お客を非表示にします。\n続けますか？",
                "差し替える", "キャンセル"))
            return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        var log = new System.Text.StringBuilder();
        if (!ApplyDrinkData(log)) return;
        ApplyLocalization(log);
        foreach (var path in ScenePaths) ApplyScene(path, log);
        AssetDatabase.SaveAssets();
        Debug.Log("[DrinkToFoodTool] フード画面への差し替えが終わりました\n" + log);
        EditorUtility.DisplayDialog("フード画面に差し替え", "差し替えました。内容はConsoleに出力しています。", "OK");
    }

    private static bool ApplyDrinkData(System.Text.StringBuilder log)
    {
        for (int i = 0; i < FoodNames.Length; i++)
        {
            int n = i + 1;
            var def = AssetDatabase.LoadAssetAtPath<DrinkDefinition>($"{DrinkDataFolder}Drink_ {n}.asset");
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{ShopArt}Food_{n}.png");
            if (def == null || sprite == null)
            {
                EditorUtility.DisplayDialog("フード画面に差し替え", $"Drink_ {n}.asset または Food_{n}.png が見つかりません。中止しました。", "OK");
                return false;
            }
            Undo.RecordObject(def, "Drink To Food");
            def.icon = sprite;
            def.drinkName = FoodNames[i].ja;
            def.iconDisplaySize = IconSize;
            EditorUtility.SetDirty(def);
            log.AppendLine($"Drink_ {n}：{FoodNames[i].ja}（Food_{n}.png、表示サイズ{IconSize.x}x{IconSize.y}）");
        }
        return true;
    }

    private static void ApplyLocalization(System.Text.StringBuilder log)
    {
        var table = AssetDatabase.LoadAssetAtPath<LocalizationTable>(LocPath);
        if (table == null) { log.AppendLine("★翻訳表が見つからないため、翻訳表は変更していません"); return; }
        Undo.RecordObject(table, "Drink To Food");
        for (int i = 0; i < FoodNames.Length; i++)
            Set(table, $"drink.Drink_ {i + 1}.name", FoodNames[i].ja, FoodNames[i].en, log);

        foreach (var e in table.entries)
        {
            if (e == null || e.key == null) continue;
            if (e.key != "pause.help.body" && e.key != "pause.help.page4.title" && e.key != "pause.help.page4.body") continue;
            string ja = (e.japanese ?? "").Replace("ドリンク", "フード").Replace("飲み物", "食べ物");
            string en = (e.english ?? "")
                .Replace("Drinks that give", "Food that gives").Replace("Buy them in", "Buy it in")
                .Replace("Drinks", "Food").Replace("drinks", "food").Replace("Drink", "Food").Replace("drink", "food");
            if (ja != e.japanese || en != e.english) { e.japanese = ja; e.english = en; log.AppendLine($"翻訳表 {e.key}：ドリンク→フード"); }
        }
        EditorUtility.SetDirty(table);
    }

    private static void Set(LocalizationTable table, string key, string ja, string en, System.Text.StringBuilder log)
    {
        var e = table.entries.Find(x => x != null && x.key == key);
        if (e == null) { e = new LocalizationTable.Entry { key = key }; table.entries.Add(e); }
        e.japanese = ja; e.english = en;
        log.AppendLine($"翻訳表 {key}：{ja} / {en}");
    }

    // ===== アイコン3つ（カクテル→ナイフとフォーク）=====
    private const string OldButtonIcon = "Assets/Art/AreaSelect/ドリンクアイコン.png";
    private const string OldCountIcon = ShopArt + "DrinkCount.png";
    private const string OldStamp = ShopArt + "購入済アイコン.png";
    private const string NewButtonIcon = ShopArt + "ボタンのアイコン.png";
    private const string NewCountIcon = ShopArt + "買える数のアイコン.png";
    private const string NewStamp = ShopArt + "購入済みの印.png";
    private static readonly string[] IconScenePaths = { "Assets/Scenes/03_AreaSelect.unity", "Assets/Scenes/04_Drink.unity", "Assets/Scenes/05_Game.unity" };

    /// <summary>
    /// 03_AreaSelect／04_Drink／05_Game の中で、カクテルのアイコン3つを参照している所をすべて新しいアイコンに差し替える
    /// （Image の画像と、ShopUI.buyIconSprite・drinkCountIcon、DrinkCardUI.purchasedStampSprite、HELPのページのアイコンなどの欄）。
    /// ただし画面上部の買える数の表示（TitleText の DrinkIcon_0〜2）は、ボタンと同じ画像を使っていたので「買える数のアイコン」にする。
    /// </summary>
    [MenuItem("Tools/フード画面/アイコン3つをナイフとフォークに差し替える（購入済み・買える数・ボタン）")]
    private static void RunIcons()
    {
        var oldButton = AssetDatabase.LoadAssetAtPath<Sprite>(OldButtonIcon);
        var oldCount = AssetDatabase.LoadAssetAtPath<Sprite>(OldCountIcon);
        var oldStamp = AssetDatabase.LoadAssetAtPath<Sprite>(OldStamp);
        var newButton = AssetDatabase.LoadAssetAtPath<Sprite>(NewButtonIcon);
        var newCount = AssetDatabase.LoadAssetAtPath<Sprite>(NewCountIcon);
        var newStamp = AssetDatabase.LoadAssetAtPath<Sprite>(NewStamp);
        if (newButton == null || newCount == null || newStamp == null)
        {
            EditorUtility.DisplayDialog("アイコンの差し替え", "新しいアイコン（ボタンのアイコン・買える数のアイコン・購入済みの印）のどれかが見つかりません。中止しました。", "OK");
            return;
        }
        if (!EditorUtility.DisplayDialog("アイコンの差し替え",
                "03_AreaSelect／04_Drink／05_Game のカクテルのアイコンを、ナイフとフォークのアイコンに差し替えます。\n続けますか？", "差し替える", "キャンセル"))
            return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        var log = new System.Text.StringBuilder();
        foreach (var path in IconScenePaths)
        {
            Scene scene = SceneManager.GetSceneByPath(path);
            bool wasLoaded = scene.IsValid() && scene.isLoaded;
            if (!wasLoaded) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                int n = 0;
                foreach (var root in scene.GetRootGameObjects())
                {
                    foreach (var comp in root.GetComponentsInChildren<Component>(true))
                    {
                        if (comp == null || comp is Transform) continue;
                        bool isCountHeader = comp is Image && comp.name.StartsWith("DrinkIcon_");
                        var so = new SerializedObject(comp);
                        var it = so.GetIterator();
                        bool changed = false;
                        while (it.Next(true))
                        {
                            if (it.propertyType != SerializedPropertyType.ObjectReference) continue;
                            var v = it.objectReferenceValue;
                            if (v == null) continue;
                            Sprite to = null;
                            if (v == oldButton) to = isCountHeader ? newCount : newButton;
                            else if (v == oldCount) to = newCount;
                            else if (v == oldStamp) to = newStamp;
                            if (to == null) continue;
                            it.objectReferenceValue = to;
                            changed = true;
                            n++;
                            log.AppendLine($"{path}：{GetPath(comp.transform)}（{comp.GetType().Name}.{it.propertyPath}）→ {to.name}");
                        }
                        if (changed) so.ApplyModifiedProperties();
                    }
                }
                if (n > 0) { EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); }
                else log.AppendLine($"{path}：差し替える所なし");
            }
            finally
            {
                if (!wasLoaded) EditorSceneManager.CloseScene(scene, true);
            }
        }
        Debug.Log("[DrinkToFoodTool] アイコン3つを差し替えました\n" + log);
        EditorUtility.DisplayDialog("アイコンの差し替え", "差し替えました。内容はConsoleに出力しています。", "OK");
    }

    // ===== 仕上げ（02_Gem を含めて、名前で探してそろえる）=====
    private static readonly string[] FinishScenePaths = { "Assets/Scenes/02_Gem.unity", "Assets/Scenes/03_AreaSelect.unity", "Assets/Scenes/04_Drink.unity" };

    /// <summary>
    /// 02_Gem／03_AreaSelect／04_Drink の3つにある ShopUI（とエリアセレクトのボタン）を、フード画面の見た目にそろえる。
    /// 古いカクテル・バーの画像は削除済みで参照が「Missing」になっているため、画像ではなく GameObject の名前・欄の名前で探して入れ直す。
    /// ・背景（ShopBgImage）＝スイーツカフェ、バーテンダー・カウンター・お客＝非表示にして画像の参照も空に、バーテンダーのアニメの絵も空に
    /// ・購入ボタン（BuyButton/Icon）とエリアセレクトのボタン（ShopButton/ShopImage）＝ボタンのアイコン、ShopUI.buyIconSprite も同じ
    /// ・買える数の表示（DrinkIcon_0〜）＝買える数のアイコン、ShopUI.drinkCountIcon も同じ
    /// ・購入済みの印（DrinkCardUI.purchasedStampSprite と PurchasedStampImage）＝購入済みの印
    /// 何度実行しても同じ結果。
    /// </summary>
    [MenuItem("Tools/フード画面/仕上げ：02_Gemを含む3シーンのショップの画像をそろえる")]
    private static void RunFinish()
    {
        var bg = AssetDatabase.LoadAssetAtPath<Sprite>(BgPath);
        var button = AssetDatabase.LoadAssetAtPath<Sprite>(NewButtonIcon);
        var count = AssetDatabase.LoadAssetAtPath<Sprite>(NewCountIcon);
        var stamp = AssetDatabase.LoadAssetAtPath<Sprite>(NewStamp);
        if (bg == null || button == null || count == null || stamp == null)
        {
            EditorUtility.DisplayDialog("仕上げ", "背景かアイコン3つのどれかが見つかりません。中止しました。", "OK");
            return;
        }
        if (!EditorUtility.DisplayDialog("仕上げ",
                "02_Gem／03_AreaSelect／04_Drink のショップの背景・アイコンをフード画面の画像にそろえ、バーテンダー・カウンター・お客の画像の参照を空にします。\n続けますか？",
                "そろえる", "キャンセル"))
            return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        var log = new System.Text.StringBuilder();
        foreach (var path in FinishScenePaths)
        {
            Scene scene = SceneManager.GetSceneByPath(path);
            bool wasLoaded = scene.IsValid() && scene.isLoaded;
            if (!wasLoaded) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                int n = 0;
                foreach (var root in scene.GetRootGameObjects())
                {
                    foreach (var ui in root.GetComponentsInChildren<ShopUI>(true))
                    {
                        var so = new SerializedObject(ui);
                        so.FindProperty("showCharacter").boolValue = false;
                        so.FindProperty("showCounter").boolValue = false;
                        so.FindProperty("showCustomer").boolValue = false;
                        so.FindProperty("buyIconSprite").objectReferenceValue = button;
                        so.FindProperty("drinkCountIcon").objectReferenceValue = count;
                        so.FindProperty("characterAnimFrames").arraySize = 0;
                        so.ApplyModifiedProperties();
                        n++;
                        log.AppendLine($"{path}：{GetPath(ui.transform)}（ShopUI：表示OFF×3・ボタン/買える数のアイコン・アニメの絵を空に）");
                    }
                    foreach (var card in root.GetComponentsInChildren<DrinkCardUI>(true))
                    {
                        var so = new SerializedObject(card);
                        var p = so.FindProperty("purchasedStampSprite");
                        if (p != null) { p.objectReferenceValue = stamp; so.ApplyModifiedProperties(); n++; log.AppendLine($"{path}：{GetPath(card.transform)}（DrinkCardUI.purchasedStampSprite＝購入済みの印）"); }
                    }
                    foreach (var img in root.GetComponentsInChildren<Image>(true))
                    {
                        Sprite to = null; bool clear = false;
                        string parent = img.transform.parent != null ? img.transform.parent.name : "";
                        if (img.name == "ShopBgImage") to = bg;
                        else if (img.name.StartsWith("DrinkIcon_")) to = count;
                        else if (img.name == "Icon" && parent == "BuyButton") to = button;
                        else if (img.name == "ShopImage" && parent == "ShopButton") to = button;
                        else if (img.name == "PurchasedStampImage") to = stamp;
                        else if (img.name == "ShopCharacterImage" || img.name == "ShopCounter" || img.name == "Customer") clear = true;
                        if (to == null && !clear) continue;
                        // ★削除した画像を指したまま（Missing）の参照は、img.sprite=null では「すでにnull」とみなされて保存されないため、
                        //   SerializedObject の m_Sprite を直接書き換える
                        //   （null→null だと変更なしと判断されることがあるため、空にする時は一度別の画像を入れてから空にする）
                        var imgSo = new SerializedObject(img);
                        var spriteProp = imgSo.FindProperty("m_Sprite");
                        if (clear) { spriteProp.objectReferenceValue = bg; imgSo.ApplyModifiedPropertiesWithoutUndo(); }
                        spriteProp.objectReferenceValue = clear ? null : to;
                        imgSo.ApplyModifiedPropertiesWithoutUndo();
                        if (clear) img.gameObject.SetActive(false);
                        EditorUtility.SetDirty(img);
                        n++;
                        log.AppendLine($"{path}：{GetPath(img.transform)} → {(clear ? "画像を空にして非表示" : to.name)}");
                    }
                }
                if (n > 0) { EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); }
                else log.AppendLine($"{path}：対象なし");
            }
            finally
            {
                if (!wasLoaded) EditorSceneManager.CloseScene(scene, true);
            }
        }
        Debug.Log("[DrinkToFoodTool] 仕上げ：3シーンのショップの画像をそろえました\n" + log);
        EditorUtility.DisplayDialog("仕上げ", "そろえました。内容はConsoleに出力しています。", "OK");
    }

    private static string GetPath(Transform t)
    {
        string p = t.name;
        while (t.parent != null) { t = t.parent; p = t.name + " > " + p; }
        return p;
    }

    private static Image FindImageByName(Scene scene, string goName)
    {
        foreach (var root in scene.GetRootGameObjects())
            foreach (var img in root.GetComponentsInChildren<Image>(true))
                if (img.name == goName) return img;
        return null;
    }

    private static void ApplyScene(string scenePath, System.Text.StringBuilder log)
    {
        Scene scene = SceneManager.GetSceneByPath(scenePath);
        bool wasLoaded = scene.IsValid() && scene.isLoaded;
        if (!wasLoaded) scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
        try
        {
            var bg = AssetDatabase.LoadAssetAtPath<Sprite>(BgPath);
            int shops = 0;
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var ui in root.GetComponentsInChildren<ShopUI>(true))
                {
                    var so = new SerializedObject(ui);
                    so.FindProperty("showCharacter").boolValue = false;
                    so.FindProperty("showCounter").boolValue = false;
                    so.FindProperty("showCustomer").boolValue = false;
                    var bgImg = so.FindProperty("shopBgImage").objectReferenceValue as Image;
                    so.ApplyModifiedProperties();
                    // 04_Drink では Shop Bg Image の欄が空で、ShopUI が実行時に「ShopBgImage」という名前で探しているため、名前でも探す
                    if (bgImg == null) bgImg = FindImageByName(scene, "ShopBgImage");
                    if (bgImg != null && bg != null)
                    {
                        Undo.RecordObject(bgImg, "Drink To Food");
                        bgImg.sprite = bg;
                        EditorUtility.SetDirty(bgImg);
                    }
                    shops++;
                    log.AppendLine($"{scenePath}：{ui.name}（背景={(bgImg != null && bg != null ? "スイーツカフェ" : "★変更できず")}、バーテンダー・カウンター・お客＝非表示）");
                }
            }
            if (shops == 0) { log.AppendLine($"{scenePath}：ShopUI が無いため変更なし"); return; }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        finally
        {
            if (!wasLoaded) EditorSceneManager.CloseScene(scene, true);
        }
    }
}
