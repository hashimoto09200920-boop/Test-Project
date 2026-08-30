using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

/// <summary>
/// Area09(Cosmos)用の流れ星(MeteorEffect)をBackground_Root直下にセットアップする。
/// </summary>
public static class MeteorEffectSetupMigrator
{
    [MenuItem("Tools/Area09/Setup Meteor Effect")]
    public static void Run()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.name != "05_Game")
        {
            Debug.LogWarning("[MeteorEffectSetupMigrator] 05_Gameシーンを開いた状態で実行してください。現在: " + scene.name);
            return;
        }

        GameObject bgRoot = GameObject.Find("Background_Root");
        if (bgRoot == null)
        {
            Debug.LogWarning("[MeteorEffectSetupMigrator] Background_Rootが見つかりません。");
            return;
        }

        // ★GameObject.Find()は非アクティブなオブジェクトを見つけられないため、
        //   階層を直接たどって探す（再実行時、非アクティブ化済みのMeteorEffectを
        //   見失って重複生成しないようにするため）
        GameObject go = FindDeepInScene(scene, "MeteorEffect");
        if (go == null)
        {
            go = new GameObject("MeteorEffect");
            go.transform.SetParent(bgRoot.transform, false);
        }
        go.transform.localPosition = Vector3.zero;

        var meteor = go.GetComponent<MeteorEffect>();
        if (meteor == null) meteor = go.AddComponent<MeteorEffect>();

        Sprite glow = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Background/T_GlowDot.png");
        Material additive = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/M_OrbGlow_Additive.mat");
        if (glow == null || additive == null)
        {
            Debug.LogError($"[MeteorEffectSetupMigrator] アセット読み込み失敗 glow={glow} additive={additive}");
            return;
        }

        var so = new SerializedObject(meteor);
        so.FindProperty("glowSprite").objectReferenceValue = glow;
        so.FindProperty("additiveMaterial").objectReferenceValue = additive;

        // ★03_AreaSelect.unityのAreaConstellationFX.nodes[]から実測した、実際にAreaSelect画面で
        //   使われている10エリア分のカラー(RGBのみ、アルファは無視。メテオ側でmeteorColor.aを使う)
        Color[] areaColors = new Color[]
        {
            new Color(0.60784316f, 0.56078434f, 0.78039217f), // Area_01
            new Color(0.29803923f, 0.6862745f, 0.49019608f),  // Area_02
            new Color(0.5529412f, 0.6f, 0.68235296f),         // Area_03
            new Color(0.8784314f, 0.47843137f, 0.24705882f),  // Area_04
            new Color(0.69803923f, 0.22745098f, 0.32156864f), // Area_05
            new Color(0.8784314f, 0.6901961f, 0.30980393f),   // Area_06
            new Color(0.30980393f, 0.56078434f, 0.8784314f),  // Area_07
            new Color(0.37254903f, 0.8392157f, 0.8392157f),   // Area_08
            new Color(0.6392157f, 0.68235296f, 0.8784314f),   // Area_09
            new Color(0.91f, 0.79f, 0.42f),                   // Area_10
        };
        var colorsProp = so.FindProperty("areaColors");
        colorsProp.arraySize = areaColors.Length;
        for (int i = 0; i < areaColors.Length; i++)
            colorsProp.GetArrayElementAtIndex(i).colorValue = areaColors[i];

        // Earth_RimGlow等と同じSorting Layerに合わせる（非アクティブでも見つかるようFindDeepを使う）
        GameObject rimGo = FindDeepInScene(scene, "Earth_RimGlow");
        if (rimGo != null)
        {
            var rimSR = rimGo.GetComponent<SpriteRenderer>();
            if (rimSR != null)
            {
                so.FindProperty("sortingLayerName").stringValue = SortingLayer.IDToName(rimSR.sortingLayerID);
                so.FindProperty("sortingOrder").intValue = rimSR.sortingOrder - 1; // 輪郭グローよりわずかに奥
            }
        }
        so.ApplyModifiedProperties();

        go.SetActive(false); // BackgroundManagerがArea09選択時のみアクティブ化する想定

        BackgroundManager bgManager = Object.FindFirstObjectByType<BackgroundManager>(FindObjectsInactive.Include);
        if (bgManager != null)
        {
            var bmSo = new SerializedObject(bgManager);
            bmSo.FindProperty("meteorEffect").objectReferenceValue = go;
            bmSo.ApplyModifiedProperties();
            EditorUtility.SetDirty(bgManager);
        }
        else
        {
            Debug.LogWarning("[MeteorEffectSetupMigrator] BackgroundManagerが見つからず、参照の自動設定をスキップしました。");
        }

        EditorUtility.SetDirty(go);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[MeteorEffectSetupMigrator] MeteorEffectをセットアップしました。");
    }

    private static GameObject FindDeepInScene(Scene scene, string name)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            var t = FindDeep(root.transform, name);
            if (t != null) return t.gameObject;
        }
        return null;
    }

    private static Transform FindDeep(Transform t, string name)
    {
        if (t.name == name) return t;
        for (int i = 0; i < t.childCount; i++)
        {
            var r = FindDeep(t.GetChild(i), name);
            if (r != null) return r;
        }
        return null;
    }
}
