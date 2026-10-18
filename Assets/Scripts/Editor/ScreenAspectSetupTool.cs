using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 画面の比率の対応（2:1より縦長の画面では上下に黒帯）を全シーンに組み込む（2026/10/18）。
/// ・Assets/Resources/ScreenAspectSettings.asset を作る（既にあればそのまま。最小の比率・黒帯の色はここで調整）
/// ・各シーンの Main Camera に AspectLetterboxCamera を付ける
/// ・各シーンに黒帯を塗るカメラ「LetterboxBarsCamera」を置く（何も映さない・Main Cameraより先に描画・普段は無効）
/// ・画面全体に出すCanvas（CanvasScaler が Scale With Screen Size のもの）に CanvasLetterboxFit を付ける
/// 何度実行しても安全（既にある部品は作り直さない。CanvasScaler など既存の設定値は変えない）。
/// </summary>
public static class ScreenAspectSetupTool
{
    private const string SettingsPath = "Assets/Resources/ScreenAspectSettings.asset";
    private const string BarsCameraName = "LetterboxBarsCamera";

    [MenuItem("Tools/画面比率/黒帯の仕組みを全シーンに組み込む")]
    private static void Run()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        var settings = AssetDatabase.LoadAssetAtPath<ScreenAspectSettings>(SettingsPath);
        if (settings == null)
        {
            if (!Directory.Exists("Assets/Resources")) Directory.CreateDirectory("Assets/Resources");
            settings = ScriptableObject.CreateInstance<ScreenAspectSettings>();
            AssetDatabase.CreateAsset(settings, SettingsPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[ScreenAspectSetupTool] 設定アセットを作成しました：{SettingsPath}（最小の比率={settings.minAspect}）");
        }

        var setup = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                SetupScene(scene, settings);
            }
        }
        finally
        {
            if (setup != null && setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
        }
    }

    private static void SetupScene(Scene scene, ScreenAspectSettings settings)
    {
        Camera main = null;
        foreach (var root in scene.GetRootGameObjects())
            foreach (var cam in root.GetComponentsInChildren<Camera>(true))
                if (cam.CompareTag("MainCamera")) { main = cam; break; }
        if (main == null) { Debug.LogWarning($"[ScreenAspectSetupTool] {scene.name}：Main Camera が見つからないため飛ばしました"); return; }

        // Main Camera に黒帯の部品
        var letterbox = main.GetComponent<AspectLetterboxCamera>();
        if (letterbox == null) letterbox = main.gameObject.AddComponent<AspectLetterboxCamera>();

        // 黒帯を塗るカメラ（シーン直下）
        GameObject barsGo = null;
        foreach (var root in scene.GetRootGameObjects())
            if (root.name == BarsCameraName) { barsGo = root; break; }
        if (barsGo == null)
        {
            barsGo = new GameObject(BarsCameraName);
            SceneManager.MoveGameObjectToScene(barsGo, scene);
        }
        var bars = barsGo.GetComponent<Camera>();
        if (bars == null) bars = barsGo.AddComponent<Camera>();
        bars.clearFlags = CameraClearFlags.SolidColor;
        bars.backgroundColor = settings.barColor;
        bars.cullingMask = 0;              // 何も映さない（塗りつぶすだけ）
        bars.orthographic = true;
        bars.rect = new Rect(0f, 0f, 1f, 1f);
        bars.depth = main.depth - 10f;     // Main Camera より先に描画
        bars.enabled = false;              // 黒帯が要る時だけ AspectLetterboxCamera が有効にする
        barsGo.transform.position = main.transform.position;

        var so = new SerializedObject(letterbox);
        so.FindProperty("barsCamera").objectReferenceValue = bars;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(letterbox);
        EditorUtility.SetDirty(bars);

        SetupBarsUI(scene, settings);

        // 画面全体に出すCanvas（一番外側・Scale With Screen Size）にUIの縮小の部品
        int canvasCount = 0;
        var names = new System.Text.StringBuilder();
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var canvas in root.GetComponentsInChildren<Canvas>(true))
            {
                if (canvas.renderMode == RenderMode.WorldSpace) continue;
                if (canvas.transform.parent != null && canvas.transform.parent.GetComponentInParent<Canvas>(true) != null) continue; // 入れ子のCanvasは外側に従う
                var scaler = canvas.GetComponent<CanvasScaler>();
                if (scaler == null || scaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize) continue;
                if (canvas.GetComponent<CanvasLetterboxFit>() == null) canvas.gameObject.AddComponent<CanvasLetterboxFit>();
                canvasCount++;
                names.Append(canvas.name.Trim()).Append(" / ");
            }
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[ScreenAspectSetupTool] {scene.name}：Main Camera に AspectLetterboxCamera、{BarsCameraName}・{BarsUIName} を配置、Canvas {canvasCount}個に CanvasLetterboxFit（{names}）を付けて保存しました");
    }

    private const string BarsUIName = "LetterboxBarsUI";

    // 全てのUIより手前に黒帯を重ねるCanvas（シーン直下）。CanvasScalerは付けない（帯はアンカーの割合で決めるため。CanvasLetterboxFitの対象外になる）
    private static void SetupBarsUI(Scene scene, ScreenAspectSettings settings)
    {
        GameObject go = null;
        foreach (var root in scene.GetRootGameObjects())
            if (root.name == BarsUIName) { go = root; break; }
        if (go == null)
        {
            go = new GameObject(BarsUIName, typeof(RectTransform));
            SceneManager.MoveGameObjectToScene(go, scene);
        }
        var canvas = go.GetComponent<Canvas>();
        if (canvas == null) canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32000; // 全てのUIより手前
        if (go.GetComponent<GraphicRaycaster>() == null) go.AddComponent<GraphicRaycaster>(); // 帯の上のタップを裏のUIに通さない
        int uiLayer = LayerMask.NameToLayer("UI");
        if (uiLayer >= 0) go.layer = uiLayer;

        var top = Bar(go.transform, "BarTop", settings.barColor, uiLayer);
        var bottom = Bar(go.transform, "BarBottom", settings.barColor, uiLayer);
        var comp = go.GetComponent<LetterboxBarsUI>();
        if (comp == null) comp = go.AddComponent<LetterboxBarsUI>();
        var so = new SerializedObject(comp);
        so.FindProperty("barTop").objectReferenceValue = top;
        so.FindProperty("barBottom").objectReferenceValue = bottom;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(comp);
    }

    private static Image Bar(Transform parent, string name, Color color, int layer)
    {
        var t = parent.Find(name) as RectTransform;
        if (t == null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            t = (RectTransform)go.transform;
            t.SetParent(parent, false);
        }
        if (layer >= 0) t.gameObject.layer = layer;
        var img = t.GetComponent<Image>();
        if (img == null) img = t.gameObject.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = true;
        t.gameObject.SetActive(false); // 黒帯が要る画面の時だけ LetterboxBarsUI が表示する
        EditorUtility.SetDirty(img);
        return img;
    }
}
