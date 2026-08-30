using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

/// <summary>
/// Earth_SurfaceのEarthRotationScroll.groundPatternsに②(既存)/②2/②3を設定する一回限りの移行スクリプト。
/// </summary>
public static class GroundPatternsWireMigrator
{
    [MenuItem("Tools/Wire Earth Surface Ground Patterns")]
    public static void Run()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.name != "05_Game")
        {
            Debug.LogWarning("[GroundPatternsWireMigrator] 05_Gameシーンを開いた状態で実行してください。現在: " + scene.name);
            return;
        }

        GameObject earthSurface = null;
        foreach (var root in scene.GetRootGameObjects())
        {
            var t = FindDeep(root.transform, "Earth_Surface");
            if (t != null) { earthSurface = t.gameObject; break; }
        }
        if (earthSurface == null)
        {
            Debug.LogWarning("[GroundPatternsWireMigrator] Earth_Surfaceが見つかりません。");
            return;
        }

        var scroll = earthSurface.GetComponent<EarthRotationScroll>();
        if (scroll == null)
        {
            Debug.LogWarning("[GroundPatternsWireMigrator] Earth_SurfaceにEarthRotationScrollがありません。");
            return;
        }

        // 順番: 大陸(②2) → ジャングル(②3) → 海(②)
        Sprite continent = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Background/② 地表テクスチャ（再生成版）2.png");
        Sprite jungle = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Background/② 地表テクスチャ（再生成版）3.png");
        Sprite ocean = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Background/② 地表テクスチャ（再生成版）.png");
        if (continent == null || jungle == null || ocean == null)
        {
            Debug.LogWarning($"[GroundPatternsWireMigrator] スプライト読み込み失敗 continent={continent} jungle={jungle} ocean={ocean}");
            return;
        }

        var so = new SerializedObject(scroll);
        var prop = so.FindProperty("groundPatterns");
        prop.arraySize = 3;
        prop.GetArrayElementAtIndex(0).objectReferenceValue = continent;
        prop.GetArrayElementAtIndex(1).objectReferenceValue = jungle;
        prop.GetArrayElementAtIndex(2).objectReferenceValue = ocean;
        so.ApplyModifiedProperties();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[GroundPatternsWireMigrator] Earth_Surfaceのgroundpatternsを設定しました: " + continent.name + ", " + jungle.name + ", " + ocean.name);
    }

    [MenuItem("Tools/Wire Earth Surface Ground Patterns (Continent Only)")]
    public static void RunSingleContinent()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.name != "05_Game")
        {
            Debug.LogWarning("[GroundPatternsWireMigrator] 05_Gameシーンを開いた状態で実行してください。現在: " + scene.name);
            return;
        }

        GameObject earthSurface = null;
        foreach (var root in scene.GetRootGameObjects())
        {
            var t = FindDeep(root.transform, "Earth_Surface");
            if (t != null) { earthSurface = t.gameObject; break; }
        }
        if (earthSurface == null)
        {
            Debug.LogWarning("[GroundPatternsWireMigrator] Earth_Surfaceが見つかりません。");
            return;
        }

        var scroll = earthSurface.GetComponent<EarthRotationScroll>();
        var sr = earthSurface.GetComponent<SpriteRenderer>();
        if (scroll == null || sr == null)
        {
            Debug.LogWarning("[GroundPatternsWireMigrator] EarthRotationScroll/SpriteRendererが見つかりません。");
            return;
        }

        Sprite continent = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Background/② 地表テクスチャ（再生成版）2.png");
        if (continent == null)
        {
            Debug.LogWarning("[GroundPatternsWireMigrator] 大陸スプライトの読み込みに失敗しました。");
            return;
        }

        var srSO = new SerializedObject(sr);
        srSO.FindProperty("m_Sprite").objectReferenceValue = continent;
        srSO.ApplyModifiedProperties();

        var so = new SerializedObject(scroll);
        so.FindProperty("groundPatterns").arraySize = 0;
        so.ApplyModifiedProperties();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[GroundPatternsWireMigrator] Earth_Surfaceを大陸1枚構成にしました: " + continent.name);
    }

    [MenuItem("Tools/Wire Earth Surface Ground Patterns (Ocean Only)")]
    public static void RunSingleOcean()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.name != "05_Game")
        {
            Debug.LogWarning("[GroundPatternsWireMigrator] 05_Gameシーンを開いた状態で実行してください。現在: " + scene.name);
            return;
        }

        GameObject earthSurface = null;
        foreach (var root in scene.GetRootGameObjects())
        {
            var t = FindDeep(root.transform, "Earth_Surface");
            if (t != null) { earthSurface = t.gameObject; break; }
        }
        if (earthSurface == null)
        {
            Debug.LogWarning("[GroundPatternsWireMigrator] Earth_Surfaceが見つかりません。");
            return;
        }

        var scroll = earthSurface.GetComponent<EarthRotationScroll>();
        var sr = earthSurface.GetComponent<SpriteRenderer>();
        if (scroll == null || sr == null)
        {
            Debug.LogWarning("[GroundPatternsWireMigrator] EarthRotationScroll/SpriteRendererが見つかりません。");
            return;
        }

        Sprite ocean = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Background/② 地表テクスチャ（再生成版）4.png");
        if (ocean == null)
        {
            Debug.LogWarning("[GroundPatternsWireMigrator] 海スプライトの読み込みに失敗しました。");
            return;
        }

        var srSO = new SerializedObject(sr);
        srSO.FindProperty("m_Sprite").objectReferenceValue = ocean;
        srSO.ApplyModifiedProperties();

        var so = new SerializedObject(scroll);
        so.FindProperty("groundPatterns").arraySize = 0;
        so.ApplyModifiedProperties();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[GroundPatternsWireMigrator] Earth_Surfaceを海1枚構成にしました: " + ocean.name);
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
