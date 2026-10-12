using Game.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 05_Gameに「THANK YOU FOR PLAYING」画面（ThankYouScreenUI）を作る。何度実行しても安全（既にある部品は作り直さず、設定だけ合わせる）。
/// ★背景画像（Background）・タイトル画像（TitleImage）・文字の大きさ等、手で設定した値は上書きしない（初めて作る時だけ既定値を入れる）。
/// </summary>
public static class ThankYouScreenSetupTool
{
    private const string ScenePath = "Assets/Scenes/05_Game.unity";
    private const string FontPath = "Assets/Fonts/NotoSansJP-Regular SDF.asset";

    [MenuItem("Tools/Thank you画面/05_Gameに作成")]
    private static void Run()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        var glow = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/SoundFX/Sound_Glow.png");
        if (font == null) { Debug.LogError("[ThankYouScreenSetupTool] フォントが見つかりません"); return; }

        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool wasLoaded = scene.IsValid() && scene.isLoaded;
        if (!wasLoaded) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);

        GameObject rootGo = null;
        foreach (var r in scene.GetRootGameObjects()) if (r.name == "ThankYouScreen") { rootGo = r; break; }
        bool isNew = rootGo == null;
        if (isNew)
        {
            rootGo = new GameObject("ThankYouScreen", typeof(RectTransform));
            SceneManager.MoveGameObjectToScene(rootGo, scene);
            rootGo.layer = LayerMask.NameToLayer("UI");
        }

        // 一番手前に出す専用のCanvas（リザルト・ジェム画面より手前）
        var canvas = Get<Canvas>(rootGo);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5000;
        var scaler = Get<CanvasScaler>(rootGo);
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 1f; // 縦を基準（横長の画面では左右が広く見える）
        Get<GraphicRaycaster>(rootGo);
        var group = Get<CanvasGroup>(rootGo);
        var rt = (RectTransform)rootGo.transform;

        // 背景：黒の下地 → 背景画像（画面を覆うように縦横比を保って拡大。はみ出た分は切れる）
        var baseRt = Child(rt, "Base"); Stretch(baseRt); baseRt.SetSiblingIndex(0);
        var baseImg = Img(baseRt, null, Color.black);
        baseImg.raycastTarget = true; // 表示中は後ろのボタン等を押せないようにする
        bool bgNew = rt.Find("Background") == null;
        var bgRt = Child(rt, "Background"); Stretch(bgRt); bgRt.SetSiblingIndex(1);
        var bg = bgRt.GetComponent<Image>();
        if (bg == null) { bg = bgRt.gameObject.AddComponent<Image>(); bg.color = Color.white; bg.raycastTarget = false; }
        var fitter = Get<AspectRatioFitter>(bgRt.gameObject);
        fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        if (bgNew) fitter.aspectRatio = 2560f / 1080f;

        // タイトル（入れ物の明るさで点灯させる）
        bool titleNew = rt.Find("Title") == null;
        var title = Child(rt, "Title"); title.SetSiblingIndex(2);
        var titleGroup = Get<CanvasGroup>(title.gameObject);
        titleGroup.blocksRaycasts = false; titleGroup.interactable = false;
        if (titleNew) { title.anchorMin = title.anchorMax = title.pivot = new Vector2(0.5f, 0.5f); title.anchoredPosition = new Vector2(0f, 330f); title.sizeDelta = new Vector2(1700f, 220f); }

        var gRt = Child(title, "TitleGlow"); gRt.SetSiblingIndex(0);
        if (titleNew) { gRt.anchorMin = gRt.anchorMax = gRt.pivot = new Vector2(0.5f, 0.5f); gRt.sizeDelta = new Vector2(1700f, 300f); }
        var gImg = gRt.GetComponent<Image>();
        if (gImg == null) { gImg = Img(gRt, glow, new Color(0.65f, 0.4f, 1f, 0.45f)); }

        var tRt = Child(title, "TitleText"); Stretch(tRt); tRt.SetSiblingIndex(1);
        var text = tRt.GetComponent<TextMeshProUGUI>();
        if (text == null)
        {
            text = tRt.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = font; text.fontSharedMaterial = font.material;
            text.text = "THANK YOU FOR PLAYING";
            text.fontSize = 100f; text.fontStyle = FontStyles.Bold; text.characterSpacing = 8f;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.color = Color.white;
            text.enableVertexGradient = true;
            text.colorGradient = new VertexGradient(new Color(0.55f, 1f, 1f), new Color(0.55f, 1f, 1f), new Color(1f, 0.5f, 0.95f), new Color(1f, 0.5f, 0.95f));
            text.raycastTarget = false;
        }

        var iRt = Child(title, "TitleImage"); Stretch(iRt); iRt.SetSiblingIndex(2);
        var tImg = iRt.GetComponent<Image>();
        if (tImg == null) { tImg = iRt.gameObject.AddComponent<Image>(); tImg.preserveAspect = true; tImg.raycastTarget = false; iRt.gameObject.SetActive(false); }

        // 戻る時の黒フェード（一番手前）
        var fRt = Child(rt, "Fade"); Stretch(fRt); fRt.SetAsLastSibling();
        var fade = Img(fRt, null, new Color(0f, 0f, 0f, 0f)); fade.enabled = false;

        var ui = Get<ThankYouScreenUI>(rootGo);
        var so = new SerializedObject(ui);
        so.FindProperty("canvas").objectReferenceValue = canvas;
        so.FindProperty("rootGroup").objectReferenceValue = group;
        so.FindProperty("background").objectReferenceValue = bg;
        so.FindProperty("titleGroup").objectReferenceValue = titleGroup;
        so.FindProperty("titleImage").objectReferenceValue = tImg;
        so.FindProperty("titleText").objectReferenceValue = text;
        so.FindProperty("titleGlow").objectReferenceValue = gImg;
        so.FindProperty("fadeImage").objectReferenceValue = fade;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(ui);

        // Play前の編集中は画面を隠しておく（Canvasを切るだけ。Play中はThankYouScreenUIが表示を切り替える）
        canvas.enabled = false;
        EditorUtility.SetDirty(rootGo);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[ThankYouScreenSetupTool] 05_Gameに「THANK YOU FOR PLAYING」画面を{(isNew ? "作成" : "更新")}して保存しました（Hierarchy：ThankYouScreen）");
        if (!wasLoaded) EditorSceneManager.CloseScene(scene, true);
    }

    [MenuItem("Tools/Thank you画面/編集用に表示する（05_Game・Play前）")]
    private static void ShowForEdit() => SetCanvasForEdit(true);

    [MenuItem("Tools/Thank you画面/編集用の表示を隠す（05_Game・Play前）")]
    private static void HideForEdit() => SetCanvasForEdit(false);

    /// <summary>背景画像や文字の位置をPlay前にGameビューで見ながら調整するため、Canvasだけを表示／非表示にする（保存はしない）</summary>
    private static void SetCanvasForEdit(bool on)
    {
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        if (!scene.IsValid() || !scene.isLoaded) { Debug.LogWarning("[ThankYouScreenSetupTool] 05_Gameを開いてから実行してください"); return; }
        foreach (var r in scene.GetRootGameObjects())
        {
            if (r.name != "ThankYouScreen") continue;
            var c = r.GetComponent<Canvas>();
            var g = r.GetComponent<CanvasGroup>();
            if (c != null) c.enabled = on;
            if (g != null) g.alpha = on ? 1f : 0f;
            var tg = r.transform.Find("Title")?.GetComponent<CanvasGroup>();
            if (tg != null) tg.alpha = 1f;
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log(on ? "[ThankYouScreenSetupTool] 編集用に表示しました。調整が終わったら「編集用の表示を隠す」を実行してから保存してください"
                         : "[ThankYouScreenSetupTool] 編集用の表示を隠しました");
            return;
        }
        Debug.LogWarning("[ThankYouScreenSetupTool] ThankYouScreenが見つかりません。先に「05_Gameに作成」を実行してください");
    }

    // ===== 小物 =====
    private static T Get<T>(GameObject go) where T : Component
    {
        var c = go.GetComponent<T>();
        if (c == null) c = go.AddComponent<T>();
        return c;
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
    private static void Stretch(RectTransform rt) { rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero; rt.pivot = new Vector2(0.5f, 0.5f); }
    private static Image Img(RectTransform rt, Sprite s, Color c)
    {
        var im = rt.GetComponent<Image>();
        if (im == null) im = rt.gameObject.AddComponent<Image>();
        im.sprite = s; im.color = c; im.type = Image.Type.Simple; im.raycastTarget = false; im.preserveAspect = false;
        return im;
    }
}
