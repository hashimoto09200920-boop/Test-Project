using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// ★一時的な構築ツール（作業完了後は削除してよい）。ドリンク画面(ShopUI)を03_AreaSelectから
/// 独立シーン(04_Drink)へ分離する。
/// ★重要：03_AreaSelect側のオリジナルは絶対に削除・変更しない。Instantiateで複製を作り、
///   その複製だけを新シーンへ移動する（ジェム画面分離時の事故の教訓を踏まえた安全な手順）。
/// メニューの★01→★02→★03の順に実行すること。
/// </summary>
public static class CreateDrinkSceneTool
{
    private const string DrinkScenePath = "Assets/Scenes/04_Drink.unity";

    // 03_AreaSelectから複製する、ドリンク画面表示に必要なCanvas配下UIオブジェクト名
    private static readonly string[] NamesToClone = new[]
    {
        "ShopUI",
        "ShopDimPanel",
        "ShopBgImage",
        "ShopCharacterImage",
        "ShopCounter",
        "Customer",
        "GoldHUD",
        "GemSkillPreviewHUD",
        "HPStatusHUD",
        "SlowMotionGaugeBackground",
        "SlowMotionGauge",
        "SlowMotionGaugeInner",
        "SlowMotionButton",
    };

    // Canvas配下ではなく、シーン直下(ルート)に置く非UIのマネージャー類
    private static readonly string[] ManagerNamesToClone = new[]
    {
        "GoldManager",
    };

    [MenuItem("Tools/★01 Drink: 04_Drinkシーンを新規作成")]
    public static void CreateEmptyDrinkScene()
    {
        if (System.IO.File.Exists(DrinkScenePath))
        {
            Debug.LogWarning($"[CreateDrinkSceneTool] {DrinkScenePath} は既に存在します。上書きしません。");
            return;
        }

        var newScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorSceneManager.SaveScene(newScene, DrinkScenePath);

        var scenes = EditorBuildSettings.scenes.ToList();
        if (!scenes.Any(s => s.path == DrinkScenePath))
        {
            scenes.Add(new EditorBuildSettingsScene(DrinkScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            Debug.Log("[CreateDrinkSceneTool] Build Settingsに04_Drinkを追加しました。");
        }

        Debug.Log($"[CreateDrinkSceneTool] {DrinkScenePath} を作成しました。次はTools/★02を実行してください。");
    }

    [MenuItem("Tools/★02 Drink: Camera・Canvas・EventSystemを整備")]
    public static void SetupCameraAndCanvas()
    {
        var scene = EditorSceneManager.OpenScene(DrinkScenePath, OpenSceneMode.Single);

        // Main Camera（背景色は青フラッシュ再発防止のため黒にする。02_Gemと同じ対策）
        if (GameObject.Find("Main Camera") == null)
        {
            var camObj = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            var cam = camObj.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.orthographic = true;
            camObj.tag = "MainCamera";
        }

        // Canvas（03_AreaSelectと同じ設定：Screen Space - Overlay, Scale With Screen Size 1920x1080）
        GameObject canvasObj;
        if (GameObject.Find("Canvas") == null)
        {
            canvasObj = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasObj.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasObj.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;
        }
        else
        {
            canvasObj = GameObject.Find("Canvas");
        }

        // EventSystem
        if (GameObject.Find("EventSystem") == null)
        {
            var esObj = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        EditorSceneManager.SaveScene(scene);
        Debug.Log("[CreateDrinkSceneTool] Camera/Canvas/EventSystemを作成しました。次はTools/★03を実行してください（03_AreaSelectがビルド設定に含まれている状態で）。");
    }

    [MenuItem("Tools/★03 Drink: 03_AreaSelectからShopUI一式を複製してCanvasへ配置")]
    public static void CloneShopUIIntoDrinkScene()
    {
        const string areaSelectPath = "Assets/Scenes/03_AreaSelect.unity";

        // 04_Drinkをまず開いておき、Canvasの参照を確保する
        var drinkScene = EditorSceneManager.OpenScene(DrinkScenePath, OpenSceneMode.Single);
        var drinkCanvasObj = GameObject.Find("Canvas");
        if (drinkCanvasObj == null)
        {
            Debug.LogError("[CreateDrinkSceneTool] 04_DrinkにCanvasがありません。先にTools/★02を実行してください。");
            return;
        }
        var drinkCanvasTransform = drinkCanvasObj.transform;

        // 03_AreaSelectを追加(Additive)で開き、複製元を探す。04_Drink自体はメインシーンのまま維持される。
        var areaSelectScene = EditorSceneManager.OpenScene(areaSelectPath, OpenSceneMode.Additive);

        GameObject FindInAreaSelect(string name)
        {
            foreach (var root in areaSelectScene.GetRootGameObjects())
            {
                var found = FindDeepChild(root.transform, name);
                if (found != null) return found;
            }
            return null;
        }

        var uiRoots = new List<GameObject>();
        var managerRoots = new List<GameObject>();
        var missing = new List<string>();
        foreach (var name in NamesToClone)
        {
            var found = FindInAreaSelect(name);
            if (found == null) { missing.Add(name); continue; }
            uiRoots.Add(found);
        }
        foreach (var name in ManagerNamesToClone)
        {
            var found = FindInAreaSelect(name);
            if (found == null) { missing.Add(name); continue; }
            managerRoots.Add(found);
        }

        if (missing.Count > 0)
            Debug.LogWarning($"[CreateDrinkSceneTool] 見つからなかったオブジェクト: {string.Join(", ", missing)}（無くても続行します）");

        int cloned = 0;
        foreach (var root in uiRoots)
        {
            var clone = Object.Instantiate(root);
            clone.name = root.name; // "(Clone)" サフィックスを除去
            SceneManager.MoveGameObjectToScene(clone, drinkScene);
            clone.transform.SetParent(drinkCanvasTransform, false);
            cloned++;
        }
        foreach (var root in managerRoots)
        {
            var clone = Object.Instantiate(root);
            clone.name = root.name;
            SceneManager.MoveGameObjectToScene(clone, drinkScene);
            cloned++;
        }

        // 03_AreaSelectは読み取り専用で開いただけなので、保存せずにAdditiveシーンを閉じる
        // （ディスク上のAssets/Scenes/03_AreaSelect.unityは一切変更されない）
        EditorSceneManager.CloseScene(areaSelectScene, true);

        EditorSceneManager.SaveScene(drinkScene);
        Debug.Log($"[CreateDrinkSceneTool] {cloned}件のオブジェクトを04_DrinkのCanvas配下へ複製しました。03_AreaSelect.unityは変更していません。");
    }

    private static GameObject FindDeepChild(Transform parent, string name)
    {
        if (parent.name == name) return parent.gameObject;
        for (int i = 0; i < parent.childCount; i++)
        {
            var result = FindDeepChild(parent.GetChild(i), name);
            if (result != null) return result;
        }
        return null;
    }
}
