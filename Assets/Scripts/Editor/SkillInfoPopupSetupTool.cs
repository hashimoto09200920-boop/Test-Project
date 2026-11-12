using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// ジェム画面・フード画面の左のスキルHUD（GemSkillPreviewHUD）に、スキルの説明の窓（SkillInfoPopupUI）と、
/// HUDの下の一言（スキルをタッチで説明）を作る（2026/11/12）。
/// 対象：GemSkillPreviewHUD がある 02_Gem・03_AreaSelect・04_Drink。何度実行しても安全（この窓と一言だけ作り直す）
/// </summary>
public static class SkillInfoPopupSetupTool
{
    private static readonly string[] ScenePaths =
    {
        "Assets/Scenes/02_Gem.unity",
        "Assets/Scenes/03_AreaSelect.unity",
        "Assets/Scenes/04_Drink.unity",
    };
    private const string FontPath = "Assets/Fonts/NotoSansJP-Regular SDF.asset";
    private const string PopupName = "SkillInfoPopup";
    private const string HintName = "SkillInfoHint";
    // 説明の窓の描く順番（画面の Canvas は0。エリアセレクトのHELP画面は100、横の黒帯は32000）
    private const int PopupSortingOrder = 200;

    [MenuItem("Tools/スキルHUD/ジェム・フード画面のスキル説明を作る（02_Gem・03_AreaSelect・04_Drink）")]
    private static void Run()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var setup = EditorSceneManager.GetSceneManagerSetup();
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        var pill = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        var log = new System.Text.StringBuilder();
        try
        {
            foreach (string path in ScenePaths)
            {
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null) { log.AppendLine($"・{path} がありません（とばしました）"); continue; }
                Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                int n = 0;
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var hud in root.GetComponentsInChildren<GemSkillPreviewHUD>(true))
                    {
                        Build(hud, font, pill);
                        n++;
                    }
                if (n > 0)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                }
                log.AppendLine($"・{scene.name}：スキルHUD {n}個にスキル説明を作りました");
            }
        }
        finally
        {
            if (setup != null && setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
        }
        Debug.Log("[SkillInfoPopupSetupTool]\n" + log);
        EditorUtility.DisplayDialog("スキル説明", log.ToString(), "OK");
    }

    private static void Build(GemSkillPreviewHUD hud, TMP_FontAsset font, Sprite pill)
    {
        var hudT = hud.transform;
        var oldPopup = hudT.Find(PopupName); if (oldPopup != null) Object.DestroyImmediate(oldPopup.gameObject);
        var oldHint = hudT.Find(HintName); if (oldHint != null) Object.DestroyImmediate(oldHint.gameObject);
        int layer = hud.gameObject.layer;

        // ---- HUDの下の一言 ----
        var hint = NewText(HintName, hudT, font, 22f, "スキルをタッチで説明", LanguageFontSwitch.Kind.Body, TextAlignmentOptions.Center, layer);
        var hrt = hint.rectTransform;
        hrt.anchorMin = hrt.anchorMax = new Vector2(0.5f, 0f); hrt.pivot = new Vector2(0.5f, 0f);
        hrt.anchoredPosition = new Vector2(0f, 14f); hrt.sizeDelta = new Vector2(260f, 34f);
        hint.color = new Color(0.6f, 0.9f, 1f, 0.85f);

        // ---- 説明の窓（いつも有効な入れ物＋中の窓）----
        var holder = NewUI(PopupName, hudT, layer);
        holder.anchorMin = Vector2.zero; holder.anchorMax = Vector2.one; holder.offsetMin = holder.offsetMax = Vector2.zero;
        holder.SetAsLastSibling(); // HUDのカードより手前に出す
        // ★2026/11/12 ジェム画面のパネル（ジェムの一覧など）より手前に出すため、窓だけ別の描く順番にする（押す操作は受け取らない）
        var popupCanvas = holder.gameObject.AddComponent<Canvas>();
        // ★HUD が非表示（SetActive(false)）の時に作ると、Canvas.overrideSorting に入れた値が保存されない（1回目はこれで0のままだった）。
        //   保存される値を直接書く。さらに SkillInfoPopupUI.OnEnable でも毎回オンにする
        var cso = new SerializedObject(popupCanvas);
        cso.FindProperty("m_OverrideSorting").boolValue = true;
        cso.FindProperty("m_SortingOrder").intValue = PopupSortingOrder;
        cso.ApplyModifiedPropertiesWithoutUndo();
        var ctrl = holder.gameObject.AddComponent<SkillInfoPopupUI>();

        var panel = NewUI("Panel", holder, layer);
        panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
        panel.pivot = new Vector2(0f, 0.5f);
        panel.sizeDelta = new Vector2(560f, 0f);
        var bg = panel.gameObject.AddComponent<Image>();
        bg.sprite = pill; bg.type = Image.Type.Sliced; bg.color = new Color(0.03f, 0.05f, 0.12f, 0.94f); bg.raycastTarget = false;
        var outline = panel.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.35f, 0.9f, 1f, 0.9f); outline.effectDistance = new Vector2(2f, -2f);
        var cg = panel.gameObject.AddComponent<CanvasGroup>();
        cg.blocksRaycasts = false; cg.interactable = false;
        var v = panel.gameObject.AddComponent<VerticalLayoutGroup>();
        v.padding = new RectOffset(22, 22, 18, 18); v.spacing = 10f;
        v.childControlWidth = true; v.childControlHeight = true; v.childForceExpandWidth = true; v.childForceExpandHeight = false;
        var fit = panel.gameObject.AddComponent<ContentSizeFitter>();
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // 見出し（アイコン＋スキル名）
        var header = NewUI("Header", panel, layer);
        var h = header.gameObject.AddComponent<HorizontalLayoutGroup>();
        h.spacing = 12f; h.childAlignment = TextAnchor.MiddleLeft;
        h.childControlWidth = true; h.childControlHeight = true; h.childForceExpandWidth = false; h.childForceExpandHeight = false;
        var icon = NewUI("Icon", header, layer);
        var iconImg = icon.gameObject.AddComponent<Image>(); iconImg.preserveAspect = true; iconImg.raycastTarget = false;
        var iconLe = icon.gameObject.AddComponent<LayoutElement>(); iconLe.preferredWidth = 48f; iconLe.preferredHeight = 48f;
        var name = NewText("Name", header, font, 34f, "スキル名", LanguageFontSwitch.Kind.Heading, TextAlignmentOptions.MidlineLeft, layer);
        name.enableAutoSizing = false;
        name.color = new Color(1f, 0.85f, 0.3f, 1f);
        var nameLe = name.gameObject.AddComponent<LayoutElement>(); nameLe.flexibleWidth = 1f;

        var desc = NewText("Description", panel, font, 28f, "説明", LanguageFontSwitch.Kind.Body, TextAlignmentOptions.TopLeft, layer);
        desc.enableAutoSizing = false; desc.textWrappingMode = TextWrappingModes.Normal;
        var brk = NewText("Breakdown", panel, font, 26f, "ジェム +1", LanguageFontSwitch.Kind.Body, TextAlignmentOptions.TopLeft, layer);
        brk.enableAutoSizing = false; brk.textWrappingMode = TextWrappingModes.Normal;

        ctrl.EditorWire(panel, iconImg, name, desc, brk, cg, hint);
        panel.gameObject.SetActive(false);
        EditorUtility.SetDirty(ctrl);

        // ★2026/11/12 押しても反応しない・反応が悪い対策
        //   ① HUDの飾り（Background・Line・Line (1)・カテゴリの見出し）は押す操作を受け止めない。
        //      Line・Line (1) はカードの一覧より手前の順番にあり、上の方のカード（バッテリー・リカバリー・ホールド）を覆っていた
        //   ② 各カードに、カード全体の大きさの透明な当たり判定を付ける（アイコンとマス目だけだと少しずれると反応しない）
        var skillHud = hudT.Find("SkillHUD");
        if (skillHud != null)
        {
            foreach (Transform ch in skillHud)
            {
                if (ch.GetComponentInChildren<SkillHUDCardUI>(true) != null) continue; // カードの一覧は除く
                foreach (var g in ch.GetComponentsInChildren<Graphic>(true))
                {
                    if (!g.raycastTarget) continue;
                    g.raycastTarget = false;
                    EditorUtility.SetDirty(g);
                }
            }
        }
        foreach (var card in hud.GetComponentsInChildren<SkillHUDCardUI>(true))
        {
            var hit = card.GetComponent<Image>();
            if (hit == null) hit = card.gameObject.AddComponent<Image>();
            hit.sprite = null;
            hit.color = new Color(0f, 0f, 0f, 0f);
            hit.raycastTarget = true;
            EditorUtility.SetDirty(hit);
        }

        // HUD に窓をつなぐ
        var so = new SerializedObject(hud);
        so.FindProperty("infoPopup").objectReferenceValue = ctrl;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(hud);
    }

    private static RectTransform NewUI(string name, Transform parent, int layer)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = layer;
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private static TextMeshProUGUI NewText(string name, Transform parent, TMP_FontAsset font, float size, string text, LanguageFontSwitch.Kind kind, TextAlignmentOptions align, int layer)
    {
        var rt = NewUI(name, parent, layer);
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) { t.font = font; t.fontSharedMaterial = font.material; }
        t.fontSize = size;
        t.text = text;
        t.alignment = align;
        t.color = Color.white;
        t.raycastTarget = false;
        rt.gameObject.AddComponent<LanguageFontSwitch>().kind = kind;
        return t;
    }
}
