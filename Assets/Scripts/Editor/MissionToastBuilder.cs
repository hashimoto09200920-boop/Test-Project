using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 05_Game に、ミッション達成の通知（MissionToastUI）と、Result画面の「MISSION CLEAR」枠（MissionResultBoxUI）を作る。
/// 既にある方は作らない（作り直さない）。作った後の位置・大きさ・色・文言はすべてInspectorで調整できる。
/// </summary>
public static class MissionToastBuilder
{
    private const string FramePng = "Assets/Art/AreaSelect/「受け取る」ボタン.png"; // ネオンライムの枠（9スライス済み）
    private const string FontAssetPath = "Assets/Fonts/NotoSansJP-Regular SDF.asset"; // 実際の文字はLanguageFontSwitch／GameNumberFontが差し替える
    private const string SePath = "Assets/Audio/GEM/ひらめく2.mp3";

    private static readonly Color Lime = new Color(0.72f, 1f, 0.24f, 1f);
    private static readonly Color White = new Color(0.95f, 0.96f, 1f, 1f);
    private static readonly Color Muted = new Color(0.62f, 0.65f, 0.8f, 1f);

    [MenuItem("Tools/ミッション/9. ミッション達成の通知とResultの枠を作成（05_Gameを開いて実行）")]
    private static void Build()
    {
        var log = new System.Text.StringBuilder();
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
        var frame = AssetDatabase.LoadAssetAtPath<Sprite>(FramePng);

        // ---------- ① プレイ中の通知 ----------
        if (Object.FindFirstObjectByType<MissionToastUI>(FindObjectsInactive.Include) != null)
            log.AppendLine("・通知（MissionToastUI）は既にあるので作りませんでした");
        else
        {
            var skillUi = Object.FindFirstObjectByType<Game.UI.SkillSelectionUI>(FindObjectsInactive.Include);
            Canvas canvas = skillUi != null ? skillUi.GetComponentInParent<Canvas>(true) : null;
            if (canvas != null) canvas = canvas.rootCanvas;
            if (canvas == null) { EditorUtility.DisplayDialog("ミッション", "05_Game の Canvas が見つかりません。05_Game を開いてから実行してください。", "OK"); return; }

            var host = NewUI("MissionToast", canvas.transform);
            Undo.RegisterCreatedObjectUndo(host, "ミッション通知を作成");
            Stretch(host);
            var toastUi = host.AddComponent<MissionToastUI>();

            var toast = NewUI("Toast", host.transform);
            var trt = toast.GetComponent<RectTransform>();
            trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 1f);
            trt.pivot = new Vector2(0.5f, 1f);
            trt.anchoredPosition = new Vector2(140f, -60f);
            var bg = toast.AddComponent<Image>();
            bg.sprite = frame; bg.type = Image.Type.Sliced; bg.pixelsPerUnitMultiplier = 1.6f; bg.raycastTarget = false;
            var cg = toast.AddComponent<CanvasGroup>();
            cg.interactable = false; cg.blocksRaycasts = false;
            var hlg = toast.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment = TextAnchor.MiddleCenter; hlg.spacing = 28f;
            hlg.padding = new RectOffset(48, 48, 18, 18);
            hlg.childControlWidth = true; hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;
            var fit = toast.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var tag = NewText("Tag", toast.transform, font, "MISSION CLEAR", 34f, Lime, LanguageFontSwitch.Kind.Heading, true);
            var nm = NewText("Name", toast.transform, font, "ソウルを100体救出する", 34f, White, LanguageFontSwitch.Kind.Heading, false);
            var pr = NewText("Progress", toast.transform, font, "100 / 100", 30f, Lime, null, false);

            var so = new SerializedObject(toastUi);
            so.FindProperty("toastRoot").objectReferenceValue = trt;
            so.FindProperty("toastGroup").objectReferenceValue = cg;
            so.FindProperty("tagText").objectReferenceValue = tag;
            so.FindProperty("nameText").objectReferenceValue = nm;
            so.FindProperty("progressText").objectReferenceValue = pr;
            so.FindProperty("toastSE").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(SePath);
            so.ApplyModifiedPropertiesWithoutUndo();
            cg.alpha = 0f; toast.SetActive(false); // Play前の確認は MissionToastUI の「通知を表示して確認（Play前）」
            host.transform.SetAsLastSibling();
            log.AppendLine("・通知を作りました：" + canvas.name + " > MissionToast");
        }

        // ---------- ② Result画面の「MISSION CLEAR」枠 ----------
        if (Object.FindFirstObjectByType<MissionResultBoxUI>(FindObjectsInactive.Include) != null)
            log.AppendLine("・Resultの枠（MissionResultBoxUI）は既にあるので作りませんでした");
        else
        {
            var result = Object.FindFirstObjectByType<ResultScreenUI>(FindObjectsInactive.Include);
            GameObject panel = null;
            if (result != null) panel = new SerializedObject(result).FindProperty("resultPanel").objectReferenceValue as GameObject;
            if (panel == null) log.AppendLine("・ResultScreenUI の Result Panel が見つからないので、Resultの枠は作りませんでした");
            else
            {
                // 常に有効な入れ物（MissionResultBoxUI）＋その子に枠（達成0件なら枠だけ非表示）
                var area = NewUI("MissionClearArea", panel.transform);
                Undo.RegisterCreatedObjectUndo(area, "Resultのミッション枠を作成");
                Stretch(area);
                var boxUi = area.AddComponent<MissionResultBoxUI>();

                var box = NewUI("MissionClearBox", area.transform);
                var brt = box.GetComponent<RectTransform>();
                brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 0.5f);
                brt.pivot = new Vector2(0.5f, 1f);
                brt.anchoredPosition = new Vector2(140f, -50f); // ContentBg（中心140,200・高さ440、下端-20）のすぐ下
                brt.sizeDelta = new Vector2(1260f, 0f);
                var bimg = box.AddComponent<Image>();
                bimg.sprite = frame; bimg.type = Image.Type.Sliced; bimg.pixelsPerUnitMultiplier = 1.6f; bimg.raycastTarget = false;
                var vlg = box.AddComponent<VerticalLayoutGroup>();
                vlg.childAlignment = TextAnchor.UpperCenter; vlg.spacing = 4f;
                vlg.padding = new RectOffset(60, 60, 26, 26);
                vlg.childControlWidth = true; vlg.childControlHeight = true;
                vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
                var bfit = box.AddComponent<ContentSizeFitter>();
                bfit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                var title = NewText("Title", box.transform, font, "MISSION CLEAR", 38f, Lime, LanguageFontSwitch.Kind.Heading, true);
                title.alignment = TextAlignmentOptions.Left;
                SetHeight(title.gameObject, 56f);

                var bso = new SerializedObject(boxUi);
                bso.FindProperty("box").objectReferenceValue = box;
                bso.FindProperty("titleText").objectReferenceValue = title;
                var rowsProp = bso.FindProperty("rows");
                rowsProp.arraySize = 3;
                for (int i = 0; i < 3; i++)
                {
                    var row = NewUI("Row" + (i + 1), box.transform);
                    var rh = row.AddComponent<HorizontalLayoutGroup>();
                    rh.childAlignment = TextAnchor.MiddleLeft; rh.spacing = 24f;
                    rh.childControlWidth = true; rh.childControlHeight = true;
                    rh.childForceExpandWidth = false; rh.childForceExpandHeight = true;
                    SetHeight(row, 54f);
                    var n = NewText("Name", row.transform, font, "ソウルを100体救出する", 30f, White, LanguageFontSwitch.Kind.Body, false);
                    n.alignment = TextAlignmentOptions.Left;
                    n.textWrappingMode = TextWrappingModes.NoWrap; n.overflowMode = TextOverflowModes.Ellipsis;
                    n.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
                    var r = NewText("Reward", row.transform, font, "パッシブ：ソウルリバイブ", 30f, Lime, LanguageFontSwitch.Kind.Heading, false);
                    r.alignment = TextAlignmentOptions.Right;
                    var el = rowsProp.GetArrayElementAtIndex(i);
                    el.FindPropertyRelative("root").objectReferenceValue = row;
                    el.FindPropertyRelative("nameText").objectReferenceValue = n;
                    el.FindPropertyRelative("rewardText").objectReferenceValue = r;
                }
                var more = NewText("More", box.transform, font, "ほか2件", 26f, Muted, LanguageFontSwitch.Kind.Body, false);
                more.alignment = TextAlignmentOptions.Right;
                SetHeight(more.gameObject, 40f);
                bso.FindProperty("moreText").objectReferenceValue = more;
                bso.ApplyModifiedPropertiesWithoutUndo();
                ApplySingleLineRows(boxUi); // 各行は1行（左に名前・右に報酬）、枠の幅は1440
                log.AppendLine("・Resultの枠を作りました：ResultPanel > MissionClearArea > MissionClearBox");
            }
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        SceneView.RepaintAll();
        Debug.Log("[MissionToastBuilder]\n" + log + "シーンを保存してください。");
        EditorUtility.DisplayDialog("ミッション", log + "\nシーンを保存してください。", "OK");
    }

    // =====================================================================
    // 10. Resultの「MISSION CLEAR」枠の各行を2段にする（2026/11/08）
    //     1段目：ミッション名（左寄せ）／2段目：報酬（右寄せ）。報酬が長いミッション（M19〜M22・M25〜M30）で名前と重なっていたため
    // =====================================================================

    private const float TwoLineNameHeight = 50f;   // 文字30の1行に必要な高さ（約44）より大きく。足りないと「…で切る」設定のため1行まるごと消える
    private const float TwoLineRewardHeight = 44f; // 文字26の1行に必要な高さ（約38）より大きく
    private const float TwoLineGap = 0f;

    [MenuItem("Tools/ミッション/10. Resultの枠の行を2段にする（名前の下に報酬・05_Gameを開いて実行）")]
    private static void MakeResultRowsTwoLine()
    {
        var boxUi = Object.FindFirstObjectByType<MissionResultBoxUI>(FindObjectsInactive.Include);
        if (boxUi == null) { EditorUtility.DisplayDialog("ミッション", "MissionResultBoxUI が見つかりません。05_Game を開いてから実行してください。", "OK"); return; }
        int n = ApplyTwoLineRows(boxUi);
        EditorSceneManager.MarkSceneDirty(boxUi.gameObject.scene);
        SceneView.RepaintAll();
        string msg = $"Resultの「MISSION CLEAR」枠の {n} 行を2段（名前の下に報酬）にしました。";
        Debug.Log("[MissionToastBuilder] " + msg + "シーンを保存してください。");
        EditorUtility.DisplayDialog("ミッション", msg + "\nシーンを保存してください。", "OK");
    }

    private static int ApplyTwoLineRows(MissionResultBoxUI boxUi)
    {
        var so = new SerializedObject(boxUi);
        var rows = so.FindProperty("rows");
        int count = 0;
        for (int i = 0; i < rows.arraySize; i++)
        {
            var el = rows.GetArrayElementAtIndex(i);
            var root = el.FindPropertyRelative("root").objectReferenceValue as GameObject;
            var name = el.FindPropertyRelative("nameText").objectReferenceValue as TMP_Text;
            var reward = el.FindPropertyRelative("rewardText").objectReferenceValue as TMP_Text;
            if (root == null || name == null || reward == null) continue;

            // 横並び → 縦並び
            var hlg = root.GetComponent<HorizontalLayoutGroup>();
            if (hlg != null) Undo.DestroyObjectImmediate(hlg);
            var vlg = root.GetComponent<VerticalLayoutGroup>();
            if (vlg == null) vlg = Undo.AddComponent<VerticalLayoutGroup>(root);
            Undo.RecordObject(vlg, "Resultの行を2段に");
            vlg.childAlignment = TextAnchor.UpperLeft;
            vlg.spacing = TwoLineGap;
            vlg.childControlWidth = true; vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
            SetHeight(root, TwoLineNameHeight + TwoLineGap + TwoLineRewardHeight);

            // 1段目：ミッション名（左寄せ・入りきらない時は…）
            Undo.RecordObject(name, "Resultの行を2段に");
            name.alignment = TextAlignmentOptions.Left;
            name.textWrappingMode = TextWrappingModes.NoWrap;
            name.overflowMode = TextOverflowModes.Ellipsis;
            var nle = name.GetComponent<LayoutElement>();
            if (nle != null) { Undo.RecordObject(nle, "Resultの行を2段に"); nle.flexibleWidth = 1f; }
            SetHeight(name.gameObject, TwoLineNameHeight);

            // 2段目：報酬（右寄せ・少し小さく）
            Undo.RecordObject(reward, "Resultの行を2段に");
            reward.alignment = TextAlignmentOptions.Right;
            reward.textWrappingMode = TextWrappingModes.NoWrap;
            reward.overflowMode = TextOverflowModes.Ellipsis;
            reward.fontSize = 26f;
            var rle = reward.GetComponent<LayoutElement>();
            if (rle == null) rle = Undo.AddComponent<LayoutElement>(reward.gameObject);
            rle.flexibleWidth = 1f;
            SetHeight(reward.gameObject, TwoLineRewardHeight);

            EditorUtility.SetDirty(root);
            count++;
        }
        return count;
    }

    // =====================================================================
    // 11. Resultの「MISSION CLEAR」枠を1行（左に名前・右に報酬）に戻し、枠の幅を1440に広げる（2026/11/09）
    //     2段は段ずれして見えるため不採用。幅を広げ、長いM25〜M28の名前を短くして1行に収める
    // =====================================================================

    private const float SingleLineBoxWidth = 1440f; // 上のリザルトの枠（1260）より左右に90ずつ広い。1500以上は上の枠とのずれが目立つ
    private const float SingleLineRowHeight = 54f;  // メニュー9で作った時と同じ
    private const float SingleLineFontSize = 30f;   // 名前・報酬とも（メニュー9で作った時と同じ）

    [MenuItem("Tools/ミッション/11. Resultの枠を1行に戻して横幅を1440にする（05_Gameを開いて実行）")]
    private static void MakeResultRowsSingleLine()
    {
        var boxUi = Object.FindFirstObjectByType<MissionResultBoxUI>(FindObjectsInactive.Include);
        if (boxUi == null) { EditorUtility.DisplayDialog("ミッション", "MissionResultBoxUI が見つかりません。05_Game を開いてから実行してください。", "OK"); return; }
        int n = ApplySingleLineRows(boxUi);
        EditorSceneManager.MarkSceneDirty(boxUi.gameObject.scene);
        SceneView.RepaintAll();
        string msg = $"Resultの「MISSION CLEAR」枠の {n} 行を1行（左に名前・右に報酬）に戻し、枠の幅を {SingleLineBoxWidth} にしました。";
        Debug.Log("[MissionToastBuilder] " + msg + "シーンを保存してください。");
        EditorUtility.DisplayDialog("ミッション", msg + "\nシーンを保存してください。", "OK");
    }

    private static int ApplySingleLineRows(MissionResultBoxUI boxUi)
    {
        var so = new SerializedObject(boxUi);

        // 枠の幅
        var box = so.FindProperty("box").objectReferenceValue as GameObject;
        if (box != null)
        {
            var brt = (RectTransform)box.transform;
            Undo.RecordObject(brt, "Resultの枠の幅");
            brt.sizeDelta = new Vector2(SingleLineBoxWidth, brt.sizeDelta.y);
        }

        var rows = so.FindProperty("rows");
        int count = 0;
        for (int i = 0; i < rows.arraySize; i++)
        {
            var el = rows.GetArrayElementAtIndex(i);
            var root = el.FindPropertyRelative("root").objectReferenceValue as GameObject;
            var name = el.FindPropertyRelative("nameText").objectReferenceValue as TMP_Text;
            var reward = el.FindPropertyRelative("rewardText").objectReferenceValue as TMP_Text;
            if (root == null || name == null || reward == null) continue;

            // 縦並び → 横並び（メニュー9で作った時と同じ設定）
            var vlg = root.GetComponent<VerticalLayoutGroup>();
            if (vlg != null) Undo.DestroyObjectImmediate(vlg);
            var hlg = root.GetComponent<HorizontalLayoutGroup>();
            if (hlg == null) hlg = Undo.AddComponent<HorizontalLayoutGroup>(root);
            Undo.RecordObject(hlg, "Resultの行を1行に");
            hlg.childAlignment = TextAnchor.MiddleLeft; hlg.spacing = 24f;
            hlg.childControlWidth = true; hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = true;
            SetHeight(root, SingleLineRowHeight);

            // 左：名前（残りの幅を全部使う）
            Undo.RecordObject(name, "Resultの行を1行に");
            name.alignment = TextAlignmentOptions.Left;
            name.textWrappingMode = TextWrappingModes.NoWrap;
            name.overflowMode = TextOverflowModes.Ellipsis;
            name.fontSize = SingleLineFontSize;
            var nle = name.GetComponent<LayoutElement>();
            if (nle != null)
            {
                Undo.RecordObject(nle, "Resultの行を1行に");
                nle.flexibleWidth = 1f;
                nle.minHeight = nle.preferredHeight = -1f; // 2段の時に入れた高さを外す（行の高さに合わせる）
            }

            // 右：報酬（文字の長さの分だけ）
            Undo.RecordObject(reward, "Resultの行を1行に");
            reward.alignment = TextAlignmentOptions.Right;
            reward.textWrappingMode = TextWrappingModes.NoWrap;
            reward.overflowMode = TextOverflowModes.Overflow; // メニュー9で作った時と同じ
            reward.fontSize = SingleLineFontSize;
            var rle = reward.GetComponent<LayoutElement>();
            if (rle != null) Undo.DestroyObjectImmediate(rle); // 2段の時に足したもの。メニュー9で作った時は無かった

            EditorUtility.SetDirty(root);
            count++;
        }
        return count;
    }

    // =====================================================================
    // 12. 【Play中】長いミッション5件でResult画面を出す（実機と同じ字体で文字の重なりを確認）
    //     MissionResultBoxUI の右クリックメニューと同じ処理。上のメニューから選べるようにした
    // =====================================================================

    [MenuItem("Tools/ミッション/12. Play中：長いミッション5件でResult画面を出す（05_GameをPlayして実行）")]
    private static void PlayModeShowResult()
    {
        var boxUi = Object.FindFirstObjectByType<MissionResultBoxUI>(FindObjectsInactive.Include);
        if (boxUi == null) { EditorUtility.DisplayDialog("ミッション", "MissionResultBoxUI が見つかりません。05_Game を開いて Play してから実行してください。", "OK"); return; }
        boxUi.PlayModeShowResultWithLongest();
    }

    // 13・15. 【Play中】全ミッションを3件ずつ確認する（F8キー）・日本語と英語を切り替える（メニューのみ・キーなし）
    // ★F9は「Tools/Debug/今いる敵を即時撃破」、F10は録画で使うため割り当てない（2026/11/10 F9が重なり撃破ツールが効かなくなった）。前の3件（F7）はユーザー判断で削除
    [MenuItem("Tools/ミッション/13. Play中：全ミッション確認 次の3件 _F8")]
    private static void PlayModeNextPage() { var b = FindBox(); if (b != null) b.PlayModeShowPage(+1); }

    [MenuItem("Tools/ミッション/15. Play中：日本語と英語を切り替える")]
    private static void PlayModeToggleLanguage() { var b = FindBox(); if (b != null) b.PlayModeToggleLanguage(); }

    private static MissionResultBoxUI FindBox()
    {
        var boxUi = Object.FindFirstObjectByType<MissionResultBoxUI>(FindObjectsInactive.Include);
        if (boxUi == null) EditorUtility.DisplayDialog("ミッション", "MissionResultBoxUI が見つかりません。05_Game を開いて Play してから実行してください。", "OK");
        return boxUi;
    }

    private static GameObject NewUI(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(parent, false);
        return go;
    }

    private static void Stretch(GameObject go)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    private static void SetHeight(GameObject go, float h)
    {
        var le = go.GetComponent<LayoutElement>();
        if (le == null) le = go.AddComponent<LayoutElement>();
        le.minHeight = le.preferredHeight = h;
    }

    private static TextMeshProUGUI NewText(string name, Transform parent, TMP_FontAsset font, string text, float size, Color color,
        LanguageFontSwitch.Kind? kind, bool alwaysEnglish)
    {
        var go = NewUI(name, parent);
        var t = go.AddComponent<TextMeshProUGUI>();
        if (font != null) { t.font = font; t.fontSharedMaterial = font.material; }
        t.text = text; t.fontSize = size; t.color = color;
        t.alignment = TextAlignmentOptions.Center;
        t.fontStyle = FontStyles.Bold;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.raycastTarget = false;
        if (kind.HasValue)
        {
            var sw = go.AddComponent<LanguageFontSwitch>();
            sw.kind = kind.Value;
            sw.alwaysUseEnglishFont = alwaysEnglish;
        }
        return t;
    }
}
