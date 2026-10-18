using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 05_Game のFinal Stageカットイン（StageCutInCanvas > CutInRoot > Stage4Text）に、文字の派手演出の部品を組み込む（2026/10/18）。
/// ・FS_Rays（後光）・FS_GlitchA / FS_GlitchB（色ずれの残像）：Stage4Textのすぐ後ろ
/// ・FS_Shockwave（衝撃波の輪）：Stage4Textのすぐ手前
/// ・FS_Flash（白い閃光）は目が痛いため廃止。前のバージョンで置いたものが残っていれば削除する
/// ・FS_ShineBar（光の帯）：Stage4Textの子。Stage4TextにMaskを付けて文字の形に切り抜く
/// ・StageCutInUI の「Final Stage 演出の部品」に参照を設定する
/// 何度実行しても安全（既にある部品は作り直さず、画像と参照だけ合わせる）。動きの数値は StageCutInUI のInspectorで調整する。
/// </summary>
public static class FinalStageTextFxSetupTool
{
    private const string ScenePath = "Assets/Scenes/05_Game.unity";
    private const string RaysPath = "Assets/Art/UI/CutIn/FinalStage_Rays.png";
    private const string ShinePath = "Assets/Art/UI/CutIn/FinalStage_ShineBar.png";
    private const string RingPath = "Assets/Art/UI/GemFX/Gem_Ring.png";

    [MenuItem("Tools/Final Stage/ファイナルステージの文字の派手演出を組み込む（05_Game）")]
    private static void Run()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        PrepareSprite(RaysPath);
        PrepareSprite(ShinePath);
        var raysSp = AssetDatabase.LoadAssetAtPath<Sprite>(RaysPath);
        var shineSp = AssetDatabase.LoadAssetAtPath<Sprite>(ShinePath);
        var ringSp = AssetDatabase.LoadAssetAtPath<Sprite>(RingPath);
        if (raysSp == null || shineSp == null || ringSp == null) { Debug.LogError("[FinalStageTextFxSetupTool] 演出用の画像が見つかりません（FinalStage_Rays / FinalStage_ShineBar / Gem_Ring）"); return; }

        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool wasLoaded = scene.IsValid() && scene.isLoaded;
        if (!wasLoaded) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        try
        {
            StageCutInUI ui = null;
            foreach (var root in scene.GetRootGameObjects())
                if (ui == null) ui = root.GetComponentInChildren<StageCutInUI>(true);
            if (ui == null) { Debug.LogError("[FinalStageTextFxSetupTool] 05_Game に StageCutInUI が見つかりません"); return; }

            var so = new SerializedObject(ui);
            var text = so.FindProperty("stage4Text").objectReferenceValue as Image;
            if (text == null || text.transform.parent == null) { Debug.LogError("[FinalStageTextFxSetupTool] StageCutInUI の Stage4 Text が未設定です"); return; }
            RectTransform parent = (RectTransform)text.transform.parent;

            // 文字の形に切り抜くためのMask（文字自体は今まで通り表示する）
            var mask = text.GetComponent<Mask>();
            if (mask == null) mask = text.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = true;

            var rays = Part(parent, "FS_Rays", raysSp, false);
            var glitchA = Part(parent, "FS_GlitchA", text.sprite, text.preserveAspect);
            var glitchB = Part(parent, "FS_GlitchB", text.sprite, text.preserveAspect);
            var shock = Part(parent, "FS_Shockwave", ringSp, true);
            var shine = Part((RectTransform)text.transform, "FS_ShineBar", shineSp, false);

            // 廃止した白い閃光（前のバージョンで置いたもの）を削除
            var oldFlash = parent.Find("FS_Flash");
            if (oldFlash != null) { Object.DestroyImmediate(oldFlash.gameObject); Debug.Log("[FinalStageTextFxSetupTool] 廃止した白い閃光（FS_Flash）を削除しました"); }

            // 並び順：後光 → 色ずれ2枚 → 文字 → 衝撃波
            int idx = text.transform.GetSiblingIndex();
            rays.transform.SetSiblingIndex(idx);
            glitchA.transform.SetSiblingIndex(idx + 1);
            glitchB.transform.SetSiblingIndex(idx + 2);
            shock.transform.SetSiblingIndex(text.transform.GetSiblingIndex() + 1);

            so.FindProperty("s4FxRays").objectReferenceValue = rays;
            so.FindProperty("s4FxGlitchA").objectReferenceValue = glitchA;
            so.FindProperty("s4FxGlitchB").objectReferenceValue = glitchB;
            so.FindProperty("s4FxShockwave").objectReferenceValue = shock;
            so.FindProperty("s4FxShineBar").objectReferenceValue = shine;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(ui);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[FinalStageTextFxSetupTool] 05_Game > {GetPath(text.transform)} に文字の派手演出の部品（後光・色ずれ・衝撃波・光の帯・Mask）を組み込んで保存しました");
        }
        finally
        {
            if (!wasLoaded) EditorSceneManager.CloseScene(scene, true);
        }
    }

    // 子の画像（クリックは通さない・最初は非表示）。既にあれば使い回す
    private static Image Part(RectTransform parent, string name, Sprite sprite, bool preserveAspect)
    {
        var t = parent.Find(name) as RectTransform;
        if (t == null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            t = (RectTransform)go.transform;
            t.SetParent(parent, false);
            t.anchorMin = t.anchorMax = t.pivot = new Vector2(0.5f, 0.5f);
        }
        var img = t.GetComponent<Image>();
        if (img == null) img = t.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.preserveAspect = preserveAspect;
        img.raycastTarget = false;
        t.gameObject.SetActive(false);
        EditorUtility.SetDirty(img);
        return img;
    }

    private static void PrepareSprite(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return;
        if (importer.textureType == TextureImporterType.Sprite && importer.spriteImportMode == SpriteImportMode.Single && importer.alphaIsTransparency) return;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.SaveAndReimport();
    }

    private static string GetPath(Transform t)
    {
        string p = t.name;
        while (t.parent != null) { t = t.parent; p = t.name + " > " + p; }
        return p;
    }
}
