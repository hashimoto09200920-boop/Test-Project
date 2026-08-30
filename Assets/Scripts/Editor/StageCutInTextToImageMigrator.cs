using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Callbacks;
using TMPro;

public static class StageCutInTextToImageMigrator
{
    [DidReloadScripts]
    private static void OnScriptsReloaded()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        EditorApplication.delayCall += Migrate;
    }

    [MenuItem("Tools/StageCutIn/Migrate Stage Text To Image")]
    private static void Migrate()
    {
        var ui = Object.FindFirstObjectByType<StageCutInUI>(FindObjectsInactive.Include);
        if (ui == null) return;

        var so = new SerializedObject(ui);
        so.Update();

        var rootProp = so.FindProperty("cutInRoot");
        if (rootProp.objectReferenceValue == null) return;
        Transform cutInRoot = ((GameObject)rootProp.objectReferenceValue).transform;

        bool changed = false;
        changed |= TryMigrateOne(cutInRoot, "StageText",  "Assets/Art/Stage/①-1 STAGE 1.png", so, "stage1Text");
        changed |= TryMigrateOne(cutInRoot, "Stage2Text", "Assets/Art/Stage/①-2 STAGE 2.png", so, "stage2Text");
        changed |= TryMigrateOne(cutInRoot, "Stage3Text", "Assets/Art/Stage/①-3 STAGE 3.png", so, "stage3Text");

        if (!changed) return;

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(ui);
        EditorSceneManager.MarkSceneDirty(ui.gameObject.scene);
        EditorSceneManager.SaveScene(ui.gameObject.scene);

        Debug.Log("[StageCutInTextToImageMigrator] Stage1/2/3テキストを画像化し、シーンを保存しました。");
    }

    private static bool TryMigrateOne(Transform root, string childName, string spritePath, SerializedObject so, string fieldName)
    {
        try
        {
            return MigrateOne(root, childName, spritePath, so, fieldName);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[StageCutInTextToImageMigrator] {childName} の移行中にエラー: {e}");
            return false;
        }
    }

    private static bool MigrateOne(Transform root, string childName, string spritePath, SerializedObject so, string fieldName)
    {
        Transform t = root.Find(childName);
        if (t == null)
        {
            Debug.LogWarning($"[StageCutInTextToImageMigrator] {childName} が見つかりません。スキップします。");
            return false;
        }
        GameObject go = t.gameObject;

        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
        if (sprite == null)
        {
            Debug.LogError($"[StageCutInTextToImageMigrator] スプライトが見つかりません: {spritePath}");
            return false;
        }

        Image existingImg = go.GetComponent<Image>();
        bool alreadyDone = existingImg != null && existingImg.sprite == sprite && go.GetComponent<TextMeshProUGUI>() == null;
        if (alreadyDone) return false;

        var autoMat = go.GetComponent<Game.UI.TMPAutoFontMaterial>();
        if (autoMat != null) Object.DestroyImmediate(autoMat, true);

        var tmp = go.GetComponent<TextMeshProUGUI>();
        if (tmp != null) Object.DestroyImmediate(tmp, true);

        Image img = go.GetComponent<Image>();
        if (img == null) img = go.AddComponent<Image>();
        if (img == null)
        {
            Debug.LogError($"[StageCutInTextToImageMigrator] {childName} に Image を追加できませんでした。");
            return false;
        }
        img.sprite = sprite;
        img.color = Color.white;
        img.preserveAspect = true;
        img.raycastTarget = false;

        Debug.Log($"[StageCutInTextToImageMigrator] {childName} ← {spritePath} (rect={sprite.rect})");

        so.FindProperty(fieldName).objectReferenceValue = img;
        EditorUtility.SetDirty(go);
        return true;
    }
}
