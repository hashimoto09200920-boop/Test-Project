using UnityEditor;
using UnityEngine;

public static class CleanMissingScripts
{
    [MenuItem("Tools/Clean/Remove Missing Scripts In Scene")]
    public static void RemoveInScene()
    {
        int goCount = 0, compCount = 0;
        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            int removed = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
            if (removed > 0) { goCount++; compCount += removed; }
        }
        Debug.Log($"[CleanMissingScripts] Removed {compCount} missing components on {goCount} GameObjects in scene.");
    }
}
