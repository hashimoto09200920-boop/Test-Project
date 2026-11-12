using Game.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 新しく手に入れたジェムの「NEW」表示（2026/11/10）をPlay前に作るメニュー。開いているシーンに合わせて作る。
/// ・03_AreaSelect：ジェムボタン（GemManagementButton）の左上に「NEW」の札（FX_NewTag）、ボタンの後ろにライム色の光（FX_NewGlow）。
///   右上の消滅の「！」（FX_AlertBadge）とは反対の角。動きは AreaSelectGuideFX（⑥）
/// ・02_Gem：ジェムのカードの見本（GemItemTemplate）の左下に「NEW」の札（FX_NewTag。初期ジェムの「STARTER」の札を複製してライム色に）。表示は GemScreenFX
/// 既にあれば作り直さない。位置・大きさ・色はInspectorで調整できる
/// </summary>
public static class GemNewIndicatorSetupTool
{
    private const string PillPath = "Assets/Art/UI/GemFX/Gem_Pill.png";
    private const string GlowPath = "Assets/Art/UI/SoundFX/Sound_Glow.png";
    private const string FontAssetPath = "Assets/Fonts/NotoSansJP-Regular SDF.asset"; // 実際の文字は LanguageFontSwitch が差し替える
    private static readonly Color Lime = new Color(0.72f, 1f, 0.24f, 1f);
    private static readonly Color DarkText = new Color(0.05f, 0.06f, 0.16f, 1f);

    [MenuItem("Tools/ジェム画面/新しいジェムのNEW表示を作る（03_AreaSelect と 02_Gem で1回ずつ実行）")]
    private static void Build()
    {
        var log = new System.Text.StringBuilder();
        bool any = false;

        // ---------- 03_AreaSelect：ジェムボタン ----------
        var guide = Object.FindFirstObjectByType<AreaSelectGuideFX>(FindObjectsInactive.Include);
        if (guide != null)
        {
            any = true;
            GameObject btnGo = null;
            foreach (var t in guide.gameObject.scene.GetRootGameObjects())
            {
                foreach (var rt in t.GetComponentsInChildren<RectTransform>(true))
                    if (rt.name == "GemManagementButton") { btnGo = rt.gameObject; break; }
                if (btnGo != null) break;
            }
            if (btnGo == null) log.AppendLine("⚠ 03_AreaSelect の GemManagementButton が見つかりません");
            else
            {
                var btn = (RectTransform)btnGo.transform;
                var gso = new SerializedObject(guide);

                // 「NEW」の札（ボタンの子・左上）
                var tag = btn.Find("FX_NewTag") as RectTransform;
                if (tag == null)
                {
                    var go = NewUI("FX_NewTag", btn);
                    Undo.RegisterCreatedObjectUndo(go, "NEWの札");
                    tag = (RectTransform)go.transform;
                    tag.anchorMin = tag.anchorMax = tag.pivot = new Vector2(0.5f, 0.5f);
                    tag.anchoredPosition = new Vector2(-60f, 60f); // 「！」（60,60）の反対の角
                    tag.sizeDelta = new Vector2(92f, 38f);
                    var cg = go.AddComponent<CanvasGroup>(); cg.blocksRaycasts = false; cg.interactable = false;
                    var img = go.AddComponent<Image>();
                    img.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(PillPath); img.type = Image.Type.Sliced;
                    img.color = Lime; img.raycastTarget = false;
                    var txt = NewText("Text", tag, "NEW", 24f, DarkText);
                    var trt = txt.rectTransform; trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = trt.offsetMax = Vector2.zero;
                    go.SetActive(false);
                    gso.FindProperty("gemNewTag").objectReferenceValue = tag;
                    gso.FindProperty("gemNewTagGroup").objectReferenceValue = cg;
                    log.AppendLine("・03_AreaSelect：ジェムボタンの左上に「NEW」の札（FX_NewTag）を作りました");
                }
                else log.AppendLine("・03_AreaSelect：「NEW」の札は既にあります");
                tag.SetAsLastSibling();

                // ★ボタンの周りの光（案C）はユーザー判断でやめた（2026/11/10）。作らない（前に作った FX_NewGlow は AreaSelectGuideFX が常に隠す）

                if (CenterCapsText(tag)) log.AppendLine("・03_AreaSelect：ジェムボタンの「NEW」の文字を、札の縦の真ん中にそろえました（Capline）");

                gso.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.MarkSceneDirty(guide.gameObject.scene);
            }
        }

        // ---------- 02_Gem：カードの見本 ----------
        var gemUI = Object.FindFirstObjectByType<GemManagementUI>(FindObjectsInactive.Include);
        if (gemUI != null && gemUI.gameObject.scene.name == "02_Gem")
        {
            any = true;
            var tpl = new SerializedObject(gemUI).FindProperty("gemItemTemplate").objectReferenceValue as GameObject;
            var starter = tpl != null ? tpl.transform.Find("FX_StarterTag") : null;
            if (tpl == null) log.AppendLine("⚠ 02_Gem のカードの見本（GemItemTemplate）が見つかりません");
            else if (tpl.transform.Find("FX_NewTag") != null) log.AppendLine("・02_Gem：カードの「NEW」の札は既にあります");
            else if (starter == null) log.AppendLine("⚠ 02_Gem：先に「Tools/ジェム画面/初期ジェムを作成」を実行してください（「STARTER」の札を複製して作るため）");
            else
            {
                var go = Object.Instantiate(starter.gameObject, tpl.transform);
                go.name = "FX_NewTag";
                Undo.RegisterCreatedObjectUndo(go, "カードのNEWの札");
                go.transform.SetSiblingIndex(starter.GetSiblingIndex() + 1);
                var rt = (RectTransform)go.transform;
                rt.sizeDelta = new Vector2(80f, rt.sizeDelta.y);
                var bg = go.GetComponent<Image>(); if (bg != null) bg.color = Lime;
                foreach (var t in go.GetComponentsInChildren<TextMeshProUGUI>(true))
                {
                    t.text = "NEW";
                    if (t.gameObject.name != "Shadow") t.color = DarkText;
                }
                go.SetActive(false);
                EditorSceneManager.MarkSceneDirty(gemUI.gameObject.scene);
                log.AppendLine("・02_Gem：カードの見本の左下に「NEW」の札（FX_NewTag・ライム色）を作りました");
            }
            // 大文字だけの札（NEW・STARTER）の文字を、札の縦の真ん中にそろえる（既にある札も直す）
            if (tpl != null)
            {
                bool a = CenterCapsText(tpl.transform.Find("FX_NewTag"));
                bool b = CenterCapsText(starter);
                if (a || b)
                {
                    EditorSceneManager.MarkSceneDirty(gemUI.gameObject.scene);
                    log.AppendLine("・02_Gem：カードの「NEW」「STARTER」の文字を、札の縦の真ん中にそろえました（Capline）");
                }
            }
        }

        if (!any) log.AppendLine("⚠ 03_AreaSelect か 02_Gem を開いてから実行してください");
        else log.AppendLine("シーンを保存してください。");
        SceneView.RepaintAll();
        Debug.Log("[GemNewIndicatorSetupTool]\n" + log);
        EditorUtility.DisplayDialog("新しいジェムのNEW表示", log.ToString(), "OK");
    }

    /// <summary>
    /// 大文字だけの札の文字を、札の縦の真ん中にそろえる（2026/11/10）。
    /// 縦の揃え方「Middle」は小文字の下に出る部分の分まで含めて真ん中を決めるため、大文字だけだと下にずれて見えた。「Capline」は大文字の高さの真ん中で揃う。
    /// 変えた時だけ true
    /// </summary>
    private static bool CenterCapsText(Transform tag)
    {
        if (tag == null) return false;
        bool changed = false;
        foreach (var t in tag.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            if (t.verticalAlignment == VerticalAlignmentOptions.Capline) continue;
            Undo.RecordObject(t, "札の文字を縦の真ん中に");
            t.verticalAlignment = VerticalAlignmentOptions.Capline;
            EditorUtility.SetDirty(t);
            changed = true;
        }
        return changed;
    }

    private static GameObject NewUI(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(parent, false);
        return go;
    }

    private static TextMeshProUGUI NewText(string name, Transform parent, string text, float size, Color color)
    {
        var go = NewUI(name, parent);
        var t = go.AddComponent<TextMeshProUGUI>();
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
        if (font != null) { t.font = font; t.fontSharedMaterial = font.material; }
        t.text = text; t.fontSize = size; t.color = color;
        t.alignment = TextAlignmentOptions.Center;
        t.fontStyle = FontStyles.Bold;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.raycastTarget = false;
        var sw = go.AddComponent<LanguageFontSwitch>();
        sw.kind = LanguageFontSwitch.Kind.Heading;
        sw.alwaysUseEnglishFont = true; // 「NEW」は日本語の画面でも英語の見出しの字体
        return t;
    }
}
