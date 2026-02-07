using UnityEngine;
using UnityEditor;

/// <summary>
/// AreaConfig を安全に複製するためのエディターコンテキストメニュー。
/// 手順: 空のアセットをディスクに作成してから、SerializedObject で内容だけコピーする。
/// </summary>
public static class AreaConfigDuplicate
{
    private const string MenuName = "Assets/Duplicate Area Config (Safe)";

    [MenuItem(MenuName, true)]
    private static bool ValidateDuplicate()
    {
        if (Selection.activeObject == null) return false;
        return Selection.activeObject is AreaConfig;
    }

    [MenuItem(MenuName, false, 0)]
    private static void DuplicateAreaConfig()
    {
        var source = Selection.activeObject as AreaConfig;
        if (source == null)
        {
            Debug.LogWarning("[AreaConfigDuplicate] No AreaConfig selected.");
            return;
        }

        string sourcePath = AssetDatabase.GetAssetPath(source);
        if (string.IsNullOrEmpty(sourcePath))
        {
            Debug.LogWarning("[AreaConfigDuplicate] Could not get asset path.");
            return;
        }

        string dir = System.IO.Path.GetDirectoryName(sourcePath).Replace('\\', '/');
        string baseName = System.IO.Path.GetFileNameWithoutExtension(sourcePath);
        string ext = System.IO.Path.GetExtension(sourcePath);

        int counter = 1;
        string newName = $"{baseName} {counter}";
        while (AssetDatabase.LoadAssetAtPath<AreaConfig>($"{dir}/{newName}{ext}") != null)
        {
            counter++;
            newName = $"{baseName} {counter}";
        }

        string newPath = $"{dir}/{newName}{ext}";

        // 1) 空の AreaConfig だけをディスクに作成（壊れない）
        AreaConfig empty = ScriptableObject.CreateInstance<AreaConfig>();
        AssetDatabase.CreateAsset(empty, newPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // 2) 作成したアセットをロードし、SerializedObject で元の内容をコピー
        AreaConfig newAsset = AssetDatabase.LoadAssetAtPath<AreaConfig>(newPath);
        if (newAsset == null)
        {
            Debug.LogError("[AreaConfigDuplicate] Failed to load created asset.");
            return;
        }

        SerializedObject soSource = new SerializedObject(source);
        SerializedObject soNew = new SerializedObject(newAsset);
        soSource.Update();
        soNew.Update();

        SerializedProperty it = soSource.GetIterator();
        it.Next(true); // m_Script をスキップ
        while (it.Next(true))
        {
            soNew.CopyFromSerializedPropertyIfDifferent(it);
        }

        soNew.ApplyModifiedPropertiesWithoutUndo();

        // オブジェクト名をファイル名に合わせる（Inspector の ! 警告を解消）
        newAsset.name = newName;
        EditorUtility.SetDirty(newAsset);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorGUIUtility.PingObject(newAsset);
        Selection.activeObject = newAsset;
        Debug.Log($"[AreaConfigDuplicate] Created: {newPath}");
    }
}
