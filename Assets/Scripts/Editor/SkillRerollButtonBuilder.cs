using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ミッション報酬のパッシブ「スキル引き直し」のボタンを、05_Game の3択画面（SkillSelectionUI › SelectionPanel）に作る。
/// 作った後の位置・大きさ・色はすべてInspectorで調整できる。もう一度実行しても、既にあれば何もしない（作り直さない）。
/// </summary>
public static class SkillRerollButtonBuilder
{
    private const string ClaimPng = "Assets/Art/AreaSelect/「受け取る」ボタン.png"; // ネオンライムの枠（9スライス済み）
    private const string IconPng = "Assets/Art/Skill/引き直しアイコン.png";
    private const string FontAssetPath = "Assets/Fonts/NotoSansJP-Regular SDF.asset"; // 実際の文字はLanguageFontSwitch／GameNumberFontが差し替える
    private const string SePath = "Assets/Audio/GEM/決定ボタンを押す19.mp3";

    [MenuItem("Tools/ミッション/8. スキル引き直しボタンを作成（05_Gameを開いて実行）")]
    private static void Build()
    {
        var ui = Object.FindFirstObjectByType<Game.UI.SkillSelectionUI>(FindObjectsInactive.Include);
        if (ui == null) { EditorUtility.DisplayDialog("スキル引き直しボタン", "SkillSelectionUI が見つかりません。05_Game を開いてから実行してください。", "OK"); return; }

        var so = new SerializedObject(ui);
        var existing = so.FindProperty("rerollButton").objectReferenceValue as Button;
        if (existing != null) { EditorUtility.DisplayDialog("スキル引き直しボタン", "既に作成済みです（" + existing.name + "）。作り直しはしません。", "OK"); Selection.activeObject = existing.gameObject; return; }

        var panel = so.FindProperty("selectionPanel").objectReferenceValue as GameObject;
        if (panel == null) { EditorUtility.DisplayDialog("スキル引き直しボタン", "SkillSelectionUI の Selection Panel が未設定です。", "OK"); return; }

        // アイコン画像をSpriteとして読み込めるようにする
        var importer = AssetImporter.GetAtPath(IconPng) as TextureImporter;
        if (importer != null && (importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Single))
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }

        var sClaim = AssetDatabase.LoadAssetAtPath<Sprite>(ClaimPng);
        var sIcon = AssetDatabase.LoadAssetAtPath<Sprite>(IconPng);
        var sPill = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
        Color lime = new Color(0.72f, 1f, 0.24f, 1f);

        // ボタン本体（中央のカードの下）
        var btnGo = NewUI("RerollButton", panel.transform);
        Undo.RegisterCreatedObjectUndo(btnGo, "スキル引き直しボタンを作成");
        var rt = btnGo.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(140f, -430f); // SkillCard2（x=140、下端-320）の下
        rt.sizeDelta = new Vector2(440f, 104f);
        var bg = btnGo.AddComponent<Image>();
        bg.sprite = sClaim; bg.type = Image.Type.Sliced; bg.pixelsPerUnitMultiplier = 1.4f;
        var btn = btnGo.AddComponent<Button>();
        btn.transition = Selectable.Transition.None; // 見た目の反応は ButtonHoverEffect
        btn.targetGraphic = bg;

        var hlg = btnGo.AddComponent<HorizontalLayoutGroup>();
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.spacing = 16f;
        hlg.padding = new RectOffset(24, 24, 0, 0);
        hlg.childControlWidth = true; hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;

        // 矢印アイコン
        var iconGo = NewUI("Icon", btnGo.transform);
        var icon = iconGo.AddComponent<Image>();
        icon.sprite = sIcon; icon.preserveAspect = false; icon.raycastTarget = false;
        SetLayout(iconGo, 48f, 48f);

        // 「スキル引き直し」
        var label = NewText("Label", btnGo.transform, font, "スキル引き直し", 34f, new Color(0.95f, 0.96f, 1f, 1f));
        label.gameObject.AddComponent<LanguageFontSwitch>().kind = LanguageFontSwitch.Kind.Heading;

        // 残りの回数（ライムの丸の中に暗い数字）
        var chipGo = NewUI("CountChip", btnGo.transform);
        var chip = chipGo.AddComponent<Image>();
        chip.sprite = sPill; chip.type = Image.Type.Sliced; chip.color = lime; chip.raycastTarget = false;
        SetLayout(chipGo, 52f, 42f);
        var count = NewText("Count", chipGo.transform, font, "2", 28f, new Color(0.04f, 0.05f, 0.1f, 1f));
        var crt = count.rectTransform;
        crt.anchorMin = Vector2.zero; crt.anchorMax = Vector2.one; crt.offsetMin = crt.offsetMax = Vector2.zero;
        count.alignment = TextAlignmentOptions.Center;

        // ホバー（拡大・点滅・効果音）：シーン内の既存のボタンの設定をコピー
        Game.UI.ButtonHoverEffect src = null;
        foreach (var h in Object.FindObjectsByType<Game.UI.ButtonHoverEffect>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var hso = new SerializedObject(h);
            if (hso.FindProperty("hoverSE").objectReferenceValue != null) { src = h; break; }
            if (src == null) src = h;
        }
        var hover = btnGo.AddComponent<Game.UI.ButtonHoverEffect>();
        if (src != null) EditorUtility.CopySerialized(src, hover);
        var hvso = new SerializedObject(hover);
        hvso.FindProperty("blinkTarget").objectReferenceValue = bg;
        var add = hvso.FindProperty("additionalBlinkTargets"); add.arraySize = 1; add.GetArrayElementAtIndex(0).objectReferenceValue = icon;
        hvso.FindProperty("requireInteractable").boolValue = true;
        hvso.FindProperty("lockAfterClick").boolValue = false;
        hvso.FindProperty("hoverScale").floatValue = 1.1f;
        hvso.FindProperty("blinkColor").colorValue = lime;
        hvso.ApplyModifiedPropertiesWithoutUndo();

        // SkillSelectionUI に設定
        so.FindProperty("rerollButton").objectReferenceValue = btn;
        so.FindProperty("rerollLabelText").objectReferenceValue = label;
        so.FindProperty("rerollCountText").objectReferenceValue = count;
        var se = AssetDatabase.LoadAssetAtPath<AudioClip>(SePath);
        if (se != null) so.FindProperty("rerollSE").objectReferenceValue = se;
        so.ApplyModifiedProperties();

        btnGo.SetActive(false); // Play中は、カードが出そろってから必要な時だけ表示（確認は SkillSelectionUI の「Show Test Cards」）
        EditorUtility.SetDirty(ui);
        EditorSceneManager.MarkSceneDirty(ui.gameObject.scene);
        SceneView.RepaintAll();
        Selection.activeObject = btnGo;
        Debug.Log("[SkillRerollButtonBuilder] RerollButton を SelectionPanel の下に作成しました。シーンを保存してください。");
    }

    private static GameObject NewUI(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(parent, false);
        return go;
    }

    private static void SetLayout(GameObject go, float w, float h)
    {
        var le = go.AddComponent<LayoutElement>();
        le.minWidth = le.preferredWidth = w;
        le.minHeight = le.preferredHeight = h;
    }

    private static TextMeshProUGUI NewText(string name, Transform parent, TMP_FontAsset font, string text, float size, Color color)
    {
        var go = NewUI(name, parent);
        var t = go.AddComponent<TextMeshProUGUI>();
        if (font != null) { t.font = font; t.fontSharedMaterial = font.material; }
        t.text = text; t.fontSize = size; t.color = color;
        t.alignment = TextAlignmentOptions.Center;
        t.fontStyle = FontStyles.Bold;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.raycastTarget = false;
        return t;
    }
}
