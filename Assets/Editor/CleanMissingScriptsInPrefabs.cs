using UnityEditor;
using UnityEngine;

public static class CleanMissingScriptsInPrefabs
{
    [MenuItem("Tools/Clean/Remove Missing Scripts In Prefabs (Assets folder)")]
    public static void RemoveInPrefabs()
    {
        var guids = AssetDatabase.FindAssets("t:Prefab");
        int goCount = 0, compCount = 0;

        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (!go) continue;

            int removed = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
            if (removed > 0)
            {
                goCount++;
                compCount += removed;
                EditorUtility.SetDirty(go);
            }
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"[CleanMissingScriptsInPrefabs] Removed {compCount} missing components on {goCount} Prefab(s).");
    }
}
