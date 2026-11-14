using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 05_Gameの中断メニューのHELP画面（PauseMenuUI.helpPanel。チュートリアルも共通）を、1ページ1項目のページ送り（HelpPagerUI）にする。
/// 旧来のスクロール表示（HelpScrollView）は削除せず非表示にして残す。何度実行しても安全（既存の部品は作り直さず設定を合わせる）。
/// 文言（pause.help.page0〜4のtitle/body、日本語・英語）はLocalizationTableに無い時だけ追加する
/// </summary>
public static class HelpPagerSetupTool
{
    private const string ScenePath = "Assets/Scenes/05_Game.unity";
    private const string ArtDir = "Assets/Art/UI/HelpFX";
    private const string FontPath = "Assets/Fonts/NotoSansJP-Regular SDF.asset";

    private static readonly (string jaTitle, string jaBody, string enTitle, string enBody)[] PageTexts =
    {
        ("赤線の特徴",
         "・ジャスト反射しやすい\n・ジャスト反射ダメージ・持続時間・反射加速・硬度が白線より高い\n・線ゲージの消費が大きく、回復も遅い\n・白線と組み合わせたり、ここぞという場面で使うと効果的",
         "Red Line Traits",
         "• Easier to land Just Reflects\n• Beats the White Line in Just Reflect damage, duration, reflection acceleration, and hardness\n• Uses more Line Gauge and recovers slowly\n• Best paired with the White Line or saved for key moments"),
        ("円の様々な効果",
         "・反射した弾と敵を同じ円で囲むと、短時間に何度もダメージを与えられる\n・HP0で抜け出したソウルを円で囲むと復活できる\n・特定の弾の効果を消せる\n・ブロックから出るアイテムを円で囲んで取ると、効果が大きくなる",
         "Circle Effects",
         "• Encircle a reflected bullet and an enemy together to hit it many times in a short time\n• At 0 HP, encircle the soul that comes loose to revive\n• Cancels certain bullet effects\n• Items from Blocks are more effective when you collect them by encircling"),
        ("ソウルの救済の注意点",
         "・救済するたびにHP全快で復活できる\n・ただし、再びソウルが抜け出すと落下が速くなり、救済が難しくなる",
         "Soul Rescue Caution",
         "• Each rescue revives you with full HP\n• But if the soul comes loose again, it falls faster and is harder to rescue"),
        // ★2026/11/12 スキル4ページ（4〜7）を追加
        ("線のスキル",
         "・ホワイト／レッドバッテリー：線ゲージの最大値が増える\n・ホワイト／レッドリカバリー：線ゲージの回復が速くなる\n・ホワイト／レッドホールド：線が長く残る\n・マルチライン：同時に引ける線の数が増える\n・ハードライン：線の硬度が上がる\n・ジェム画面・フード画面では、左のスキルを押すと説明が出る",
         "Line Skills",
         "• White/Red Battery: raises the Line Gauge max\n• White/Red Recovery: Line Gauge recovers faster\n• White/Red Hold: lines last longer\n• Multi Line: more lines at once\n• Hard Line: lines get harder\n• On the Gem and Food screens, tap a skill on the left to see its details"),
        ("攻撃のスキル",
         "・リフレクトブースト：反射弾が敵に当たるたびダメージが増える\n・ブロックバスター：ブロックへのダメージが増える\n・ジャストダメージ：ジャスト反射弾のダメージが増える\n・スロウインパクト：反射弾を当てた敵の動きが遅くなる\n・サイドワインダー：ジャスト反射弾が敵の弾を貫通する",
         "Attack Skills",
         "• Reflect Boost: reflected bullets deal more damage with each enemy hit\n• Block Buster: more damage to Blocks\n• Just Damage: Just-reflected bullets deal more damage\n• Slow Impact: enemies hit by reflected bullets slow down\n• Sidewinder: Just-reflected bullets pierce enemy bullets"),
        ("シールドと時間のスキル",
         "・シールドブレイカー：敵のシールドへのダメージが増える\n・ブレイクダメージ：シールドを壊した後、しばらく敵へのダメージが増える\n・シールドディレイ：壊したシールドの回復が遅くなる\n・クロックハック：スローモーションの時間と回復が増える",
         "Shield & Time Skills",
         "• Shield Breaker: more damage to enemy shields\n• Break Damage: after breaking a shield, damage to enemies rises for a while\n• Shield Delay: broken shields recover more slowly\n• Clock Hack: longer Slow Motion that recovers faster"),
        ("守りと猶予のスキル",
         "・フロアガード：フロアの硬度が上がる\n・ライフエクステンド：ライフが増える\n・セルフリストア：ダメージを受けていない間、ゆっくり回復する\n・ジャストマスター：ジャスト反射の猶予時間が延びる\n・サークルマスター：円の猶予時間が延び、円で取ったアイテムの効果も上がる",
         "Defense & Timing Skills",
         "• Floor Guard: the Floor gets harder\n• Life Extend: more Life\n• Self Restore: slowly recover while not taking damage\n• Just Master: wider Just Reflect timing\n• Circle Master: wider Circle timing, and stronger items collected by encircling"),
        // ★2026/11/12 アイテム3ページ（8〜10）とミッション（15）を追加し、ジェム・フードを13・14ページへ。ジェムにリロール、フードにお食事券を追記
        ("ゴールドと回復のアイテム",
         "・コイン：ゴールドが手に入る\n・金貨袋：コインより多くのゴールドが手に入る\n・宝箱：さらに多くのゴールドが手に入る\n・ハート：ダンサーとフロアのHPが回復する\n・お食事券：フードを1つ無料で買える\n・ゴールドとお食事券は、ゲームオーバーになっても持ち帰れる",
         "Gold & Healing Items",
         "• Coin: gives Gold\n• Gold Bag: gives more Gold than a Coin\n• Treasure Chest: gives even more Gold\n• Heart: restores the Dancer's and the Floor's HP\n• Food Ticket: lets you buy one Food for free\n• You keep your Gold and Food Tickets even on Game Over"),
        ("一定時間効くアイテム",
         "・ゴールドシューズ：線ゲージの消費が半分になる\n・アクセラレーター：跳ね返した弾が速くなる\n・マルチバウンド：跳ね返した弾が跳ね返れる回数が2倍になる\n・効果は20秒。円で囲んで取ると長くなる",
         "Timed Items",
         "• Gold Shoes: halves Line Gauge use\n• Accelerator: reflected bullets fly faster\n• Multi Bound: reflected bullets can bounce twice as many times\n• Lasts 20 seconds; longer if collected by encircling"),
        ("弾を変えるアイテム",
         "・スパイラルショット：跳ね返した弾がドリルになり、刺さって何度もダメージを与える\n・ブラストショット：跳ね返した弾が、敵・壁・ブロックに当たるか3秒たつと爆発する\n・スプリットショット：跳ね返した弾が3つに分かれる\n・4発分使える。円で囲んで取ると増える。別の種類を取ると入れ替わる",
         "Shot Items",
         "• Spiral Shot: reflected bullets become drills that stick in and hit again and again\n• Blast Shot: reflected bullets explode on hitting an enemy, wall, or Block, or after 3 seconds\n• Split Shot: reflected bullets split into three\n• Works for 4 shots; more if collected by encircling. Picking up another type replaces it"),
        // ★2026/11/12 リザルトの評価2ページ（11・12）を追加。点数は ResultScreenUI.CalcRankScore と同じ（変えたらここも直す）
        ("リザルトの評価",
         "・6つの項目の点数の合計（100点満点）でランクが決まる\n・S：90点以上　A：75点以上　B：58点以上\n・C：40点以上　D：20点以上　E：20点未満\n・各エリアの一番良いランクが記録される",
         "Result Rank",
         "• Your rank comes from the total of 6 categories (100 points max)\n• S: 90+　A: 75+　B: 58+\n• C: 40+　D: 20+　E: under 20\n• Your best rank in each Area is saved"),
        ("評価の点数",
         "・反射率（ジャスト反射の割合）30点：30%以上で満点\n・撃破数 25点：30体以上で満点\n・被弾数 20点：8以下で満点\n・オーバーヒート 12点：2回以下　ダウン回数 8点：1回以下で満点\n・ブロック破壊 5点：10個以上で満点",
         "Scoring",
         "• Accuracy (Just Reflect rate) 30 pts: full at 30% or more\n• Kills 25 pts: full at 30 or more\n• Received 20 pts: full at 8 or fewer\n• Overheat 12 pts: 2 or fewer / Downs 8 pts: 1 or fewer for full\n• Blocks 5 pts: full at 10 or more"),
        ("ジェムの取得と効果",
         "・スキル効果を得られる不思議な宝石\n・エリアセレクトで着け外しできる\n・新しいエリアを開放するたびに、装備できる数が増える\n・プレイするたびに使用可能回数が減り、0になると壊れて消える\n・ゴールドを払ってリロールすると、スキルの組み合わせを引き直せる（初期ジェムはできない）",
         "Gems",
         "• Mysterious jewels that grant skill effects\n• Equip and unequip them in Area Select\n• You can equip more each time you unlock a new Area\n• Uses decrease each play; at 0 the gem breaks and disappears\n• Pay Gold to Reroll a gem's skill combination (not available for Starter gems)"),
        ("フードの購入と効果",
         "・1プレイ限りでスキルが一時的に強化される食べ物\n・エリアセレクトで購入できる（最大5つまで）\n・同じフードは複数購入できない\n・お食事券を使うと、選んだフードを1つ無料で買える",
         "Food",
         "• Food that gives a temporary skill boost for one play\n• Buy it in Area Select (up to 5)\n• You can't buy more than one of the same food\n• Use a Food Ticket to get the Food you choose for free"),
        ("ミッション",
         "・エリアセレクトのミッション画面で、達成状況を確認できる\n・達成すると報酬を受け取れる（パッシブ効果・ゴールド・お食事券・虹の雫）\n・パッシブ効果は、受け取るとずっと効き続ける",
         "Missions",
         "• Check your progress on the Mission screen in Area Select\n• Claim rewards when you complete them (Passives, Gold, Food Tickets, Prism Drops)\n• Once claimed, Passives stay in effect permanently"),
    };

    // 前の並び → 今の並び（15ページ）の番号。ページ数で前の並びを見分ける
    //   5ページ：赤線・円・ソウル・ジェム・フード
    //   9ページ：赤線・円・ソウル・アイテム3・ジェム・フード・ミッション
    private static int[] OldToNewIndex(int oldCount)
    {
        if (oldCount == 5) return new[] { 0, 1, 2, 12, 13 };
        if (oldCount == 9) return new[] { 0, 1, 2, 7, 8, 9, 12, 13, 14 };
        return null;
    }

    [MenuItem("Tools/中断画面/HELPをページ送りにする（05_Game）")]
    private static void Run()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool wasLoaded = scene.IsValid() && scene.isLoaded;
        if (!wasLoaded) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);

        PauseMenuUI pause = null;
        foreach (var root in scene.GetRootGameObjects())
        {
            pause = root.GetComponentInChildren<PauseMenuUI>(true);
            if (pause != null) break;
        }
        if (pause == null) { Debug.LogError("[HelpPagerSetupTool] PauseMenuUIが見つかりません"); }
        else if (Build(pause, scene))
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[HelpPagerSetupTool] 05_GameのHELP画面をページ送りにして保存しました（文言{AddLocalization()}件追加）");
        }
        if (!wasLoaded) EditorSceneManager.CloseScene(scene, true);
    }

    // 赤線（1ページ目）とジェム（4ページ目）のアイコンだけ：横長・余白の多い画像で小さく、見出しより上に見えるため、大きくして少し下げる。
    // ドリンク（5ページ目）は少し小さく見えるため少しだけ大きくする
    private static readonly (int page, float scale, Vector2 offset)[] IconAdjusts =
    {
        (0, 1.3f, new Vector2(0f, -12f)),
        (12, 1.3f, new Vector2(0f, -6f)),  // ジェム（2026/11/12 15ページ化で3→12）
        (13, 1.15f, Vector2.zero),         // フード（2026/11/12 15ページ化で4→13）
    };

    [MenuItem("Tools/中断画面/HELPの赤線・ジェム・ドリンクのアイコンを大きくする（05_Game）")]
    private static void RunIconAdjust()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool wasLoaded = scene.IsValid() && scene.isLoaded;
        if (!wasLoaded) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        try
        {
            int n = 0;
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var pager in root.GetComponentsInChildren<HelpPagerUI>(true))
                {
                    var so = new SerializedObject(pager);
                    var pages = so.FindProperty("pages");
                    foreach (var a in IconAdjusts)
                    {
                        if (a.page >= pages.arraySize) continue;
                        var el = pages.GetArrayElementAtIndex(a.page);
                        el.FindPropertyRelative("iconScale").floatValue = a.scale;
                        el.FindPropertyRelative("iconOffset").vector2Value = a.offset;
                        var sp = el.FindPropertyRelative("icon").objectReferenceValue;
                        Debug.Log($"[HelpPagerSetupTool] {pager.name} ページ{a.page + 1}（{(sp != null ? sp.name : "アイコン無し")}）：倍率{a.scale}・ずらし{a.offset}");
                    }
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(pager);
                    n++;
                }
            }
            if (n == 0) { Debug.LogError("[HelpPagerSetupTool] 05_GameにHelpPagerUIが見つかりません（先に「HELPをページ送りにする」を実行）"); return; }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        finally
        {
            if (!wasLoaded) EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static bool Build(PauseMenuUI pause, Scene scene)
    {
        var o = new SerializedObject(pause);
        var panel = o.FindProperty("helpPanel").objectReferenceValue as GameObject;
        if (panel == null) { Debug.LogError("[HelpPagerSetupTool] helpPanelが未設定です"); return false; }
        var scroll = panel.transform.Find("HelpScrollView") as RectTransform;
        var panelRt = panel.transform as RectTransform;
        if (panelRt != null && (Mathf.Approximately(panelRt.sizeDelta.x, 1100f) || Mathf.Approximately(panelRt.sizeDelta.x, 1600f)))
        {
            Debug.Log($"[HelpPagerSetupTool] HELPの文字の枠：幅 {panelRt.sizeDelta.x}→{HelpPanelWidth}・X {panelRt.anchoredPosition.x}→{HelpPanelX}");
            panelRt.sizeDelta = new Vector2(HelpPanelWidth, panelRt.sizeDelta.y);
            panelRt.anchoredPosition = new Vector2(HelpPanelX, panelRt.anchoredPosition.y);
            var bg = panel.transform.Find("HelpBg") as RectTransform;
            if (bg != null)
            {
                Debug.Log($"[HelpPagerSetupTool] HELPの背景の絵（HelpBg）：幅 {bg.sizeDelta.x}→{HelpBgWidth}");
                bg.sizeDelta = new Vector2(HelpBgWidth, bg.sizeDelta.y);
                EditorUtility.SetDirty(bg);
            }
            else Debug.LogWarning("[HelpPagerSetupTool] HelpPanel の子に HelpBg が見つかりません。背景の幅は手で合わせてください");
            EditorUtility.SetDirty(panelRt);
        }
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        var sp = EnsureSprites();

        // ---- 本体：旧スクロール表示と同じ位置・同じレイアウト設定で差し替える ----
        var pager = Child(panel.transform, "HelpPager");
        if (scroll != null) pager.SetSiblingIndex(scroll.GetSiblingIndex());
        var le = GetOrAdd<LayoutElement>(pager);
        var oldLe = scroll != null ? scroll.GetComponent<LayoutElement>() : null;
        le.preferredHeight = oldLe != null ? oldLe.preferredHeight : 360f;
        le.flexibleHeight = oldLe != null ? oldLe.flexibleHeight : 1f;
        // スワイプを受け取るための透明な当たり判定
        var hit = GetOrAdd<Image>(pager);
        hit.color = new Color(0f, 0f, 0f, 0f); hit.raycastTarget = true;
        if (scroll != null) scroll.gameObject.SetActive(false);

        var helper = GetOrAdd<HelpPagerUI>(pager);
        var so = new SerializedObject(helper);

        // 内容（見出し＋区切り線＋本文）：切り替え時にスライド＆フェード
        var content = Child(pager, "Content");
        content.anchorMin = Vector2.zero; content.anchorMax = Vector2.one; content.pivot = new Vector2(0.5f, 0.5f);
        content.offsetMin = new Vector2(0f, 64f); content.offsetMax = Vector2.zero;
        var cg = GetOrAdd<CanvasGroup>(content);
        cg.blocksRaycasts = false;

        var icon = Child(content, "Icon");
        icon.anchorMin = icon.anchorMax = new Vector2(0f, 1f); icon.pivot = new Vector2(0f, 1f);
        icon.anchoredPosition = new Vector2(40f, -4f); icon.sizeDelta = new Vector2(60f, 60f);
        var iconImg = Img(icon, null, Color.white); iconImg.preserveAspect = true;

        var title = Child(content, "Title");
        title.anchorMin = new Vector2(0f, 1f); title.anchorMax = new Vector2(1f, 1f); title.pivot = new Vector2(0f, 1f);
        title.offsetMin = new Vector2(116f, -66f); title.offsetMax = new Vector2(-40f, -4f);
        var titleT = Text(title, font, TitleFontSize, TextAlignmentOptions.MidlineLeft, new Color(1f, 0.85f, 0.3f, 1f));
        titleT.fontStyle = FontStyles.Bold;

        var line = Child(content, "Divider");
        line.anchorMin = new Vector2(0f, 1f); line.anchorMax = new Vector2(1f, 1f); line.pivot = new Vector2(0.5f, 1f);
        line.offsetMin = new Vector2(36f, -78f); line.offsetMax = new Vector2(-36f, -75f);
        Img(line, null, Color.white);
        var grad = GetOrAdd<UIHorizontalGradient>(line);
        grad.colorLeft = new Color(0.35f, 0.9f, 1f, 0.9f); grad.colorRight = new Color(1f, 0.4f, 1f, 0.15f);

        // ★2026/11/12 本文を大きく（27→34・自動で小さくしない）。収まらない時は上下にスクロールできるよう、
        //   本文を「はみ出した分を隠す枠」（BodyViewport）の中に入れる。本文の高さは HelpPagerUI が文字の量に合わせる
        var viewport = Child(content, "BodyViewport");
        viewport.anchorMin = Vector2.zero; viewport.anchorMax = Vector2.one; viewport.pivot = new Vector2(0.5f, 1f);
        viewport.offsetMin = new Vector2(48f, 8f); viewport.offsetMax = new Vector2(-48f, -96f);
        GetOrAdd<RectMask2D>(viewport);
        var body = viewport.Find("Body") as RectTransform;
        if (body == null)
        {
            var oldBody = content.Find("Body") as RectTransform; // 前の作り（content の直下）から移す
            body = oldBody != null ? oldBody : Child(viewport, "Body");
            body.SetParent(viewport, false);
        }
        body.anchorMin = new Vector2(0f, 1f); body.anchorMax = new Vector2(1f, 1f); body.pivot = new Vector2(0.5f, 1f);
        // 右端のつまみの分（16）だけ狭く。高さ（仮に400）は実行中に文字の量に合わせる
        body.offsetMin = new Vector2(0f, -400f); body.offsetMax = new Vector2(-16f, 0f);
        var bodyT = Text(body, font, BodyFontSize, TextAlignmentOptions.TopLeft, Color.white);
        bodyT.lineSpacing = 12f; bodyT.paragraphSpacing = 16f;
        bodyT.enableAutoSizing = false;
        bodyT.overflowMode = TextOverflowModes.Overflow;
        // スクロールのつまみ（右端の細い棒）
        var thumbRt = Child(viewport, "ScrollThumb");
        thumbRt.anchorMin = thumbRt.anchorMax = new Vector2(1f, 1f); thumbRt.pivot = new Vector2(1f, 1f);
        thumbRt.anchoredPosition = Vector2.zero; thumbRt.sizeDelta = new Vector2(6f, 120f);
        var thumbImg = Img(thumbRt, null, new Color(0.45f, 0.9f, 1f, 0.7f));
        thumbImg.enabled = false;

        // ナビ（◀ ● ○ ○ ○ ○ ▶）
        var nav = Child(pager, "Nav");
        nav.anchorMin = new Vector2(0f, 0f); nav.anchorMax = new Vector2(1f, 0f); nav.pivot = new Vector2(0.5f, 0f);
        nav.offsetMin = new Vector2(0f, 4f); nav.offsetMax = new Vector2(0f, 60f);
        var prev = ArrowButton(nav, "PrevButton", sp["arrow"], -190f, flip: true);
        var next = ArrowButton(nav, "NextButton", sp["arrow"], 190f, flip: false);
        var dotsP = so.FindProperty("dots"); dotsP.arraySize = PageTexts.Length;
        // ★2026/11/12 ページが増えたら点の間隔を狭める（左右の矢印（中心±190・幅56）と重ならないよう、点の列の幅を300以内に）
        float dotStep = Mathf.Min(30f, 300f / Mathf.Max(1, PageTexts.Length - 1));
        for (int i = 0; i < PageTexts.Length; i++)
        {
            var d = Child(nav, "Dot" + i);
            d.anchorMin = d.anchorMax = d.pivot = new Vector2(0.5f, 0.5f);
            d.anchoredPosition = new Vector2((i - (PageTexts.Length - 1) * 0.5f) * dotStep, 0f); d.sizeDelta = new Vector2(16f, 16f);
            dotsP.GetArrayElementAtIndex(i).objectReferenceValue = Img(d, sp["dot"], Color.white);
        }

        so.FindProperty("iconImage").objectReferenceValue = iconImg;
        so.FindProperty("titleText").objectReferenceValue = titleT;
        so.FindProperty("bodyText").objectReferenceValue = bodyT;
        so.FindProperty("bodyViewport").objectReferenceValue = viewport;
        so.FindProperty("scrollThumb").objectReferenceValue = thumbImg;
        so.FindProperty("contentGroup").objectReferenceValue = cg;
        so.FindProperty("contentRect").objectReferenceValue = content;
        so.FindProperty("prevButton").objectReferenceValue = prev;
        so.FindProperty("nextButton").objectReferenceValue = next;
        if (so.FindProperty("pageTurnSe").objectReferenceValue == null)
            so.FindProperty("pageTurnSe").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/GEM/カーソル移動2.mp3");

        // ページ（アイコン・日本語の予備文言）
        Sprite redLine = FindRedLineIcon(scene);
        Sprite[] icons =
        {
            redLine,
            sp["circle"],
            AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/PixelDancer/Soul.png"),
            // ★2026/11/12 スキル・アイテム・リザルトのページ（4〜12）は見出しのアイコンを出さない（スキル・アイテムは各行の左にアイコンを出す）
            null, null, null, null,   // スキル4ページ
            null, null, null,         // アイテム3ページ
            null, null,               // リザルト2ページ
            AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/AreaSelect/ジェムアイコン.png"),
            AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/AreaSelect/Shop/ボタンのアイコン.png"), // 2026/10/21 フード画面に変更（旧：ドリンクアイコン.png）
            ItemBuffBase.LoadSpriteForEditor("Assets/Art/AreaSelect/ミッションアイコン.png"), // ミッション（2026/11/12）
        };
        Sprite[][] lineIcons = LineIcons(scene);
        var pagesP = so.FindProperty("pages");
        pagesP.arraySize = PageTexts.Length;
        for (int i = 0; i < PageTexts.Length; i++)
        {
            var el = pagesP.GetArrayElementAtIndex(i);
            bool noTitleIcon = i >= NoTitleIconFrom && i <= NoTitleIconTo;
            if (noTitleIcon) el.FindPropertyRelative("icon").objectReferenceValue = null; // 前に付けた見出しのアイコンも外す
            else if (el.FindPropertyRelative("icon").objectReferenceValue == null) el.FindPropertyRelative("icon").objectReferenceValue = icons[i];
            el.FindPropertyRelative("fallbackTitle").stringValue = PageTexts[i].jaTitle;
            el.FindPropertyRelative("fallbackBody").stringValue = PageTexts[i].jaBody;
            // 各行のアイコン（スキル・アイテムのページだけ）
            var li = el.FindPropertyRelative("lineIcons");
            var src = i < lineIcons.Length ? lineIcons[i] : null;
            li.arraySize = src != null ? src.Length : 0;
            for (int k = 0; src != null && k < src.Length; k++)
            {
                li.GetArrayElementAtIndex(k).objectReferenceValue = src[k];
                if (src[k] == null && LineIconExpected(i, k)) Debug.LogWarning($"[HelpPagerSetupTool] ページ{i + 1}の{k + 1}行目のアイコン画像が見つかりません。HelpPagerUIのPages > Line Iconsで手動設定してください");
            }
            if (!noTitleIcon && icons[i] == null) Debug.LogWarning($"[HelpPagerSetupTool] ページ{i + 1}のアイコン画像が見つかりません。HelpPagerUIのPagesで手動設定してください");
        }

        // 各行のアイコンを置く入れ物（本文の子・6個。HelpPagerUI が位置と大きさを決める）
        var slotsP = so.FindProperty("lineIconSlots");
        slotsP.arraySize = LineIconSlotCount;
        for (int k = 0; k < LineIconSlotCount; k++)
        {
            var s = Child(body, "LineIcon" + k);
            s.anchorMin = s.anchorMax = s.pivot = new Vector2(0.5f, 0.5f);
            s.sizeDelta = new Vector2(36f, 36f);
            var img = Img(s, null, Color.white);
            img.preserveAspect = true;
            img.enabled = false;
            slotsP.GetArrayElementAtIndex(k).objectReferenceValue = img;
        }

        // キーワードの色：チュートリアルのガイドと同じ一覧をコピー（空の時だけ）
        var kw = so.FindProperty("keywords");
        if (kw.arraySize == 0)
        {
            TutorialGuideUI guide = null;
            foreach (var root in scene.GetRootGameObjects()) { guide = root.GetComponentInChildren<TutorialGuideUI>(true); if (guide != null) break; }
            if (guide != null)
            {
                var src = guide.EditorKeywords;
                kw.arraySize = src.Count;
                for (int i = 0; i < src.Count; i++)
                {
                    kw.GetArrayElementAtIndex(i).FindPropertyRelative("keyword").stringValue = src[i].keyword;
                    kw.GetArrayElementAtIndex(i).FindPropertyRelative("color").colorValue = src[i].color;
                }
            }
            else Debug.LogWarning("[HelpPagerSetupTool] TutorialGuideUIが無いため、キーワードの色はコピーしませんでした（HelpPagerUIのKeywordsで設定可）");
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(helper);
        EditorUtility.SetDirty(panel);
        return true;
    }

    // 見出しのアイコンを出さないページ（スキル4〜7・アイテム8〜10・リザルト11〜12。0始まりで3〜11）（2026/11/12）
    private const int NoTitleIconFrom = 3, NoTitleIconTo = 11;
    private const int LineIconSlotCount = 6;

    // 各行のアイコンを付ける行：アイテムのページの4行目（持ち帰り・効果時間・回数の説明）はアイコン無し
    private static bool LineIconExpected(int page, int line) => !(page == 7 ? line == 5 : (page >= 8 && page <= 9 && line == 3));

    // HELPの横幅（2026/11/12）。長い説明以外が1行に収まるように広げる。
    //   ・文字の入る枠（HelpPanel）1100→1460、背景の絵（HelpPanel > HelpBg）1500→1960
    //   ・右上の中断ボタンと左のスキル表示に重ならないよう、中心を左へ寄せる（HelpPanel の X 140→30）
    //   前の幅（1100・1600）の時だけ変える（Inspectorで調整した後は上書きしない）
    private const float HelpPanelWidth = 1460f, HelpBgWidth = 1960f, HelpPanelX = 30f;

    // 文字の大きさ（2026/11/12 小さすぎたので 見出し36→42・本文27→34。行のアイコンは本文の文字の大きさに合わせて自動で大きくなる）
    private const float TitleFontSize = 42f, BodyFontSize = 34f;

    /// <summary>
    /// ページごとの各行のアイコン（[ページ][行]）。スキルはスキルのアイコン（ホワイト／レッドの2つ組はホワイトの方）、
    /// アイテムは05_GameのBlockItemManagerに設定されているアイコン（ブロックから出るアイテムと同じ絵）
    /// </summary>
    private static Sprite[][] LineIcons(Scene scene)
    {
        BlockItemManager bim = null;
        foreach (var root in scene.GetRootGameObjects()) { bim = root.GetComponentInChildren<BlockItemManager>(true); if (bim != null) break; }
        Sprite Item(string field)
        {
            if (bim == null) return null;
            var p = new SerializedObject(bim).FindProperty(field);
            return p != null ? p.objectReferenceValue as Sprite : null;
        }
        if (bim == null) Debug.LogWarning("[HelpPagerSetupTool] 05_GameにBlockItemManagerが無いため、アイテムのページの行のアイコンを付けられません");
        var r = new Sprite[PageTexts.Length][];
        r[3] = new[] { SkillIcon("Skill_A1_LeftMaxCostUp"), SkillIcon("Skill_A3_LeftRecoveryUp"), SkillIcon("Skill_A5_LeftLifetimeUp"), SkillIcon("Skill_A7_MaxStrokesUp"), SkillIcon("Skill_B1_HardnessUp") };
        r[4] = new[] { SkillIcon("Skill_A8_ReflectedBulletSpeedUp"), SkillIcon("Skill_A9_BlockDamageUp"), SkillIcon("Skill_B5_JustDamageUp"), SkillIcon("Skill_B4_EnemySpeedDown"), SkillIcon("Skill_C2_JustPenetration") };
        r[5] = new[] { SkillIcon("Skill_B6_ShieldDamageUp"), SkillIcon("Skill_B7_ShieldBreakDamageBoost"), SkillIcon("Skill_B8_ShieldRecoveryDelay"), SkillIcon("Skill_A10_SlowMotionEffectUp") };
        r[6] = new[] { SkillIcon("Skill_B2_FloorHPUp"), SkillIcon("Skill_B3_PixelDancerHPUp"), SkillIcon("Skill_C3_SelfHeal"), SkillIcon("Skill_C1_JustWindowExtension"), SkillIcon("Skill_C4_CircleTimeExtension") };
        r[7] = new[] { Item("goldIcon"), Item("goldBagIcon"), Item("treasureChestIcon"), Item("lifeIcon"), Item("foodTicketIcon"), null };
        r[8] = new[] { Item("goldenShoesIcon"), Item("goldBoltIcon"), Item("multiBoundIcon"), null };
        r[9] = new[] { Item("spiralShotIcon"), Item("blastShotIcon"), Item("splitShotIcon"), null };
        return r;
    }

    /// <summary>スキルのアイコン（Resources/GameData/Skills の「icon」）。無ければnull</summary>
    private static Sprite SkillIcon(string assetName)
    {
        var a = AssetDatabase.LoadAssetAtPath<ScriptableObject>($"Assets/Resources/GameData/Skills/{assetName}.asset");
        if (a == null) return null;
        var p = new SerializedObject(a).FindProperty("icon");
        return p != null ? p.objectReferenceValue as Sprite : null;
    }

    /// <summary>ランクのバッジ（Result画面・エリアセレクトと同じ RankBadgeSet）。無ければnull</summary>
    private static Sprite RankBadge(string rank)
    {
        var set = AssetDatabase.LoadAssetAtPath<Game.Progress.RankBadgeSet>("Assets/RankBadgeSet.asset");
        return set != null ? set.GetSprite(rank) : null;
    }

    private static Sprite FindRedLineIcon(Scene scene)
    {
        foreach (var root in scene.GetRootGameObjects())
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == "Icon" && t.parent != null && t.parent.name == "RedBar" && t.parent.parent != null && t.parent.parent.name == "PaddleCostBarUI")
                {
                    var im = t.GetComponent<Image>();
                    if (im != null && im.sprite != null) return im.sprite;
                }
        return null;
    }

    private static Button ArrowButton(RectTransform parent, string name, Sprite arrow, float x, bool flip)
    {
        var rt = Child(parent, name);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(x, 0f); rt.sizeDelta = new Vector2(56f, 48f);
        rt.localScale = new Vector3(flip ? -1f : 1f, 1f, 1f);
        var img = Img(rt, arrow, new Color(0.45f, 0.95f, 1f, 1f));
        img.raycastTarget = true; img.preserveAspect = true;
        var b = GetOrAdd<Button>(rt);
        b.targetGraphic = img;
        var colors = b.colors; colors.disabledColor = Color.white; b.colors = colors; // 押せない時の濃さはHelpPagerUIが決める
        return b;
    }

    /// <summary>
    /// HELPを最新の15ページ（スキル4・アイテム3・リザルト2・ミッションを追加）にする（2026/11/12）。
    /// 今が5ページ・9ページの時は、今のページのアイコン・大きさ・ずらしをそのまま新しい並びへ移し、増えたページのアイコンと点（Dot）を作り、
    /// 文言（pause.help.page0〜14 の見出し・本文、日本語・英語）を最新に書き換える。もう15ページなら並べ替えはせず文言だけ合わせる
    /// </summary>
    [MenuItem("Tools/中断画面/HELPを最新のページにする（スキル・アイテム・リザルト・ミッション。05_Game）")]
    private static void RunNinePages()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool wasLoaded = scene.IsValid() && scene.isLoaded;
        if (!wasLoaded) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        try
        {
            PauseMenuUI pause = null;
            foreach (var root in scene.GetRootGameObjects()) { pause = root.GetComponentInChildren<PauseMenuUI>(true); if (pause != null) break; }
            if (pause == null) { Debug.LogError("[HelpPagerSetupTool] PauseMenuUIが見つかりません"); return; }

            // 今のページ（5ページの時だけ）を新しい並びへ移す
            HelpPagerUI pager = null;
            foreach (var root in scene.GetRootGameObjects()) { pager = root.GetComponentInChildren<HelpPagerUI>(true); if (pager != null) break; }
            string moved = "";
            if (pager != null)
            {
                var so = new SerializedObject(pager);
                var pages = so.FindProperty("pages");
                int[] map = pages.arraySize != PageTexts.Length ? OldToNewIndex(pages.arraySize) : null;
                if (map != null)
                {
                    int n = pages.arraySize;
                    var icons = new Object[n]; var scales = new float[n]; var offsets = new Vector2[n];
                    for (int i = 0; i < n; i++)
                    {
                        var el = pages.GetArrayElementAtIndex(i);
                        icons[i] = el.FindPropertyRelative("icon").objectReferenceValue;
                        scales[i] = el.FindPropertyRelative("iconScale").floatValue;
                        offsets[i] = el.FindPropertyRelative("iconOffset").vector2Value;
                    }
                    pages.arraySize = PageTexts.Length;
                    for (int i = 0; i < PageTexts.Length; i++)
                    {
                        var el = pages.GetArrayElementAtIndex(i);
                        el.FindPropertyRelative("icon").objectReferenceValue = null;
                        el.FindPropertyRelative("iconScale").floatValue = 1f;
                        el.FindPropertyRelative("iconOffset").vector2Value = Vector2.zero;
                    }
                    for (int i = 0; i < n; i++)
                    {
                        var el = pages.GetArrayElementAtIndex(map[i]);
                        el.FindPropertyRelative("icon").objectReferenceValue = icons[i];
                        el.FindPropertyRelative("iconScale").floatValue = scales[i];
                        el.FindPropertyRelative("iconOffset").vector2Value = offsets[i];
                    }
                    so.ApplyModifiedPropertiesWithoutUndo();
                    moved = $"（前の{n}ページの内容を新しい並びへ移しました）";
                }
                else if (pages.arraySize != PageTexts.Length)
                {
                    Debug.LogWarning($"[HelpPagerSetupTool] ページ数が {pages.arraySize} で、5・9・{PageTexts.Length} のどれでもありません。並べ替えはせず、足りないページを足します");
                }
            }

            // 増えたページのアイコン・日本語の予備文言・点（Dot）を作る（既存の部品は作り直さない）
            if (!Build(pause, scene)) return;
            int changed = AddLocalization(overwrite: true);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            string msg = $"HELPを{PageTexts.Length}ページにしました{moved}。文言を{changed}件書き換え・追加しました。";
            Debug.Log("[HelpPagerSetupTool] " + msg);
            EditorUtility.DisplayDialog("HELP", msg, "OK");
        }
        finally
        {
            if (!wasLoaded) EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static int AddLocalization(bool overwrite = false)
    {
        var table = Resources.Load<Game.Localization.LocalizationTable>("GameData/LocalizationTable");
        if (table == null) return 0;
        var so = new SerializedObject(table);
        var entries = so.FindProperty("entries");
        var existing = new Dictionary<string, int>();
        for (int i = 0; i < entries.arraySize; i++) existing[entries.GetArrayElementAtIndex(i).FindPropertyRelative("key").stringValue] = i;
        int added = 0;
        void Add(string key, string ja, string en)
        {
            if (existing.TryGetValue(key, out int at))
            {
                if (!overwrite) return;
                var cur = entries.GetArrayElementAtIndex(at);
                if (cur.FindPropertyRelative("japanese").stringValue == ja && cur.FindPropertyRelative("english").stringValue == en) return;
                cur.FindPropertyRelative("japanese").stringValue = ja;
                cur.FindPropertyRelative("english").stringValue = en;
                added++;
                return;
            }
            int idx = entries.arraySize;
            entries.InsertArrayElementAtIndex(idx);
            var el = entries.GetArrayElementAtIndex(idx);
            el.FindPropertyRelative("key").stringValue = key;
            el.FindPropertyRelative("japanese").stringValue = ja;
            el.FindPropertyRelative("english").stringValue = en;
            added++;
        }
        for (int i = 0; i < PageTexts.Length; i++)
        {
            Add($"pause.help.page{i}.title", PageTexts[i].jaTitle, PageTexts[i].enTitle);
            Add($"pause.help.page{i}.body", PageTexts[i].jaBody, PageTexts[i].enBody);
        }
        if (added > 0) { so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(table); AssetDatabase.SaveAssets(); }
        return added;
    }

    // ===== 小物 =====
    private static T GetOrAdd<T>(Component c) where T : Component
    {
        var x = c.GetComponent<T>();
        return x != null ? x : c.gameObject.AddComponent<T>();
    }
    private static RectTransform Child(Transform parent, string name)
    {
        var t = parent.Find(name) as RectTransform;
        if (t != null) return t;
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }
    private static Image Img(RectTransform rt, Sprite s, Color c)
    {
        var im = GetOrAdd<Image>(rt);
        im.sprite = s; im.color = c; im.raycastTarget = false;
        return im;
    }
    private static TextMeshProUGUI Text(RectTransform rt, TMP_FontAsset font, float size, TextAlignmentOptions align, Color c)
    {
        var t = GetOrAdd<TextMeshProUGUI>(rt);
        if (font != null) { t.font = font; t.fontSharedMaterial = font.material; }
        t.fontSize = size; t.alignment = align; t.color = c; t.raycastTarget = false;
        t.textWrappingMode = TextWrappingModes.Normal;
        return t;
    }

    private static Dictionary<string, Sprite> EnsureSprites()
    {
        if (!Directory.Exists(ArtDir)) Directory.CreateDirectory(ArtDir);
        return new Dictionary<string, Sprite>
        {
            ["arrow"] = Ensure("Help_Arrow", ArrowTex),
            ["dot"] = Ensure("Help_Dot", DotTex),
            ["circle"] = Ensure("Help_Circle", CircleTex),
        };
    }

    private static Sprite Ensure(string name, System.Func<Texture2D> make)
    {
        string path = $"{ArtDir}/{name}.png";
        if (!File.Exists(path))
        {
            var tex = make();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.mipmapEnabled = false;
            imp.alphaIsTransparency = true;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            var st = new TextureImporterSettings();
            imp.ReadTextureSettings(st);
            st.spriteMeshType = SpriteMeshType.FullRect;
            imp.SetTextureSettings(st);
            imp.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static Texture2D NewTex(int w, int h) { var t = new Texture2D(w, h, TextureFormat.RGBA32, false); t.SetPixels32(new Color32[w * h]); return t; }

    /// <summary>右向きの三角（角を少し丸めた矢印）＋外側の光</summary>
    private static Texture2D ArrowTex()
    {
        int s = 64; var t = NewTex(s, s);
        Vector2 a = new Vector2(16f, 8f), b = new Vector2(16f, 56f), c = new Vector2(54f, 32f);
        for (int y = 0; y < s; y++) for (int x = 0; x < s; x++)
        {
            Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
            float d = TriSd(p, a, b, c);
            float alpha = Mathf.Max(Mathf.Clamp01(0.5f - d), d > 0f ? Mathf.Clamp01(1f - d / 6f) * 0.35f : 0f);
            if (alpha > 0f) t.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
        }
        t.Apply(); return t;
    }

    private static Texture2D DotTex()
    {
        int s = 32; var t = NewTex(s, s); float cc = (s - 1) * 0.5f;
        for (int y = 0; y < s; y++) for (int x = 0; x < s; x++)
        {
            float r = Mathf.Sqrt((x - cc) * (x - cc) + (y - cc) * (y - cc));
            float a = Mathf.Max(Mathf.Clamp01(9f - r + 0.5f), Mathf.Clamp01(1f - (r - 9f) / 6f) * 0.4f);
            if (a > 0f) t.SetPixel(x, y, new Color(1f, 1f, 1f, a));
        }
        t.Apply(); return t;
    }

    /// <summary>「円」の見出し用：線で描いた円＋外側の光（ネオン風、シアン）</summary>
    private static Texture2D CircleTex()
    {
        int s = 96; var t = NewTex(s, s); float cc = (s - 1) * 0.5f;
        for (int y = 0; y < s; y++) for (int x = 0; x < s; x++)
        {
            float r = Mathf.Sqrt((x - cc) * (x - cc) + (y - cc) * (y - cc));
            float d = Mathf.Abs(r - 34f);
            float line = Mathf.Clamp01(3.5f - d + 0.5f);
            float glow = Mathf.Clamp01(1f - d / 12f) * 0.45f;
            float a = Mathf.Max(line, glow);
            Color col = Color.Lerp(new Color(0.4f, 0.95f, 1f, 1f), Color.white, line * 0.6f);
            if (a > 0f) t.SetPixel(x, y, new Color(col.r, col.g, col.b, a));
        }
        t.Apply(); return t;
    }

    private static float TriSd(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float d = Mathf.Min(Seg(p, a, b), Mathf.Min(Seg(p, b, c), Seg(p, c, a)));
        bool inside = Cross(b - a, p - a) <= 0f && Cross(c - b, p - b) <= 0f && Cross(a - c, p - c) <= 0f
                   || Cross(b - a, p - a) >= 0f && Cross(c - b, p - b) >= 0f && Cross(a - c, p - c) >= 0f;
        return inside ? -d : d;
    }
    private static float Cross(Vector2 u, Vector2 v) => u.x * v.y - u.y * v.x;
    private static float Seg(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a; float k = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
        return Vector2.Distance(p, a + ab * k);
    }
}
