using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 03_AreaSelect に、無限化の石の獲得演出（InfiniteStoneRewardFX）の部品を作って組み込む。
/// ・通知パネル（GemLifecycleUI の noticePanel）と同じ親の下に「FX_InfiniteStoneReward」を作る（画面全体に広がる入れ物）。
///   石が通知パネルの外（右上の所持数）まで飛ぶため、パネルの子にはしない
/// ・中身：石（所持数表示 InfiniteStoneHUD のアイコンと同じ画像）・後ろの光（Gem_Glow）・虹色の輪（Gem_Ring）・光の粒×12（Gem_Star）
/// ・GemLifecycleUI と InfiniteStoneHUD への参照を設定する
/// 何度実行しても安全（既にある部品は作り直さず、画像と参照だけ合わせる）。動きの数値は FX_InfiniteStoneReward の InfiniteStoneRewardFX で調整する。
/// </summary>
public static class InfiniteStoneRewardFXSetupTool
{
    private const string ScenePath = "Assets/Scenes/03_AreaSelect.unity";
    private const int SparkCount = 12;

    [MenuItem("Tools/エリア選択画面/無限化の石の獲得演出を組み込む（03_AreaSelect）")]
    private static void Run()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var glowSp = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/GemFX/Gem_Glow.png");
        var ringSp = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/GemFX/Gem_Ring.png");
        var starSp = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/GemFX/Gem_Star.png");
        if (glowSp == null || ringSp == null || starSp == null) { Debug.LogError("[InfiniteStoneRewardFXSetupTool] Assets/Art/UI/GemFX の Gem_Glow / Gem_Ring / Gem_Star のどれかが見つかりません"); return; }

        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool wasLoaded = scene.IsValid() && scene.isLoaded;
        if (!wasLoaded) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        try
        {
            Game.UI.GemLifecycleUI life = null;
            InfiniteStoneHUD hud = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (life == null) life = root.GetComponentInChildren<Game.UI.GemLifecycleUI>(true);
                if (hud == null) hud = root.GetComponentInChildren<InfiniteStoneHUD>(true);
            }
            if (life == null) { Debug.LogError("[InfiniteStoneRewardFXSetupTool] 03_AreaSelect に GemLifecycleUI が見つかりません"); return; }
            if (hud == null) Debug.LogWarning("[InfiniteStoneRewardFXSetupTool] 03_AreaSelect に InfiniteStoneHUD が見つかりません（石は所持数へ飛ばず上へ消えます）");

            var lso = new SerializedObject(life);
            var panel = lso.FindProperty("noticePanel").objectReferenceValue as GameObject;
            if (panel == null || panel.transform.parent == null) { Debug.LogError("[InfiniteStoneRewardFXSetupTool] GemLifecycleUI の Notice Panel が未設定です"); return; }

            // 石の画像：所持数表示のアイコンと同じ
            Sprite stoneSp = null;
            if (hud != null)
            {
                var icon = new SerializedObject(hud).FindProperty("stoneIcon").objectReferenceValue as Image;
                if (icon != null) stoneSp = icon.sprite;
            }
            if (stoneSp == null) Debug.LogWarning("[InfiniteStoneRewardFXSetupTool] InfiniteStoneHUD のアイコン画像が取れないため、石の画像が空です（FX_InfiniteStoneReward > Stone の Image で設定してください）");

            // 入れ物：通知パネルと同じ親の下、画面全体に広げる
            Transform parent = panel.transform.parent;
            var fxT = parent.Find("FX_InfiniteStoneReward") as RectTransform;
            if (fxT == null)
            {
                var go = new GameObject("FX_InfiniteStoneReward", typeof(RectTransform));
                Undo.RegisterCreatedObjectUndo(go, "Infinite Stone Reward FX");
                fxT = (RectTransform)go.transform;
                fxT.SetParent(parent, false);
            }
            fxT.anchorMin = Vector2.zero; fxT.anchorMax = Vector2.one; fxT.offsetMin = fxT.offsetMax = Vector2.zero;
            fxT.SetAsLastSibling();
            var fx = fxT.GetComponent<InfiniteStoneRewardFX>();
            if (fx == null) fx = fxT.gameObject.AddComponent<InfiniteStoneRewardFX>();

            // 並び順：後ろの光 → 虹色の輪 → 光の粒 → 石（石が一番手前）
            var glow = Part(fxT, "Glow", glowSp, out Image glowImg);
            var ring = Part(fxT, "Ring", ringSp, out Image ringImg);
            var sparks = new RectTransform[SparkCount];
            var sparkImgs = new Image[SparkCount];
            for (int i = 0; i < SparkCount; i++) sparks[i] = Part(fxT, "Spark" + i, starSp, out sparkImgs[i]);
            var stone = Part(fxT, "Stone", stoneSp, out Image stoneImg);
            stoneImg.preserveAspect = true;
            glow.SetAsLastSibling(); ring.SetAsLastSibling();
            foreach (var s in sparks) s.SetAsLastSibling();
            stone.SetAsLastSibling();

            var so = new SerializedObject(fx);
            so.FindProperty("stone").objectReferenceValue = stone;
            so.FindProperty("glow").objectReferenceValue = glow;
            so.FindProperty("glowImage").objectReferenceValue = glowImg;
            so.FindProperty("ring").objectReferenceValue = ring;
            so.FindProperty("ringImage").objectReferenceValue = ringImg;
            var sp = so.FindProperty("sparks"); sp.arraySize = SparkCount;
            var si = so.FindProperty("sparkImages"); si.arraySize = SparkCount;
            for (int i = 0; i < SparkCount; i++)
            {
                sp.GetArrayElementAtIndex(i).objectReferenceValue = sparks[i];
                si.GetArrayElementAtIndex(i).objectReferenceValue = sparkImgs[i];
            }
            so.FindProperty("hud").objectReferenceValue = hud;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(fx);

            lso.FindProperty("infiniteStoneRewardFx").objectReferenceValue = fx;
            lso.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(life);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[InfiniteStoneRewardFXSetupTool] 03_AreaSelect に無限化の石の獲得演出を組み込んで保存しました（{parent.name} > FX_InfiniteStoneReward）");
        }
        finally
        {
            if (!wasLoaded) EditorSceneManager.CloseScene(scene, true);
        }
    }

    // 子の画像（クリックは通さない）。既にあれば使い回す。最初は非表示（演出の時だけ表示する）
    private static RectTransform Part(RectTransform parent, string name, Sprite sprite, out Image img)
    {
        var t = parent.Find(name) as RectTransform;
        if (t == null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            t = (RectTransform)go.transform;
            t.SetParent(parent, false);
        }
        t.anchorMin = t.anchorMax = t.pivot = new Vector2(0.5f, 0.5f);
        img = t.GetComponent<Image>();
        if (img == null) img = t.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.raycastTarget = false;
        t.gameObject.SetActive(false);
        return t;
    }
}
