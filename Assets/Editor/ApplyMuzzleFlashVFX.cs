using UnityEngine;
using UnityEditor;

/// <summary>
/// Tools > Apply Muzzle Flash VFX to All Enemies
/// Projectウィンドウで対象PrefabをSelectした状態で実行する
/// </summary>
public static class ApplyMuzzleFlashVFX
{
    [MenuItem("Tools/Apply Muzzle Flash VFX to All Enemies")]
    private static void Apply()
    {
        // 選択中のGameObjectをPrefabとして取得
        GameObject selectedPrefab = Selection.activeGameObject;
        if (selectedPrefab == null)
        {
            EditorUtility.DisplayDialog(
                "Muzzle Flash VFX",
                "ProjectウィンドウでマズルフラッシュのPrefabを選択してから実行してください。",
                "OK"
            );
            return;
        }

        if (!EditorUtility.IsPersistent(selectedPrefab))
        {
            EditorUtility.DisplayDialog(
                "Muzzle Flash VFX",
                "HierarchyではなくProjectウィンドウのPrefabを選択してください。",
                "OK"
            );
            return;
        }

        // 上書きモードを選択
        int option = EditorUtility.DisplayDialogComplex(
            "Muzzle Flash VFX",
            $"Prefab: {selectedPrefab.name}\n\n" +
            "空のスロットだけ設定しますか？\nそれとも既存の設定も上書きしますか？",
            "空のみ設定",
            "キャンセル",
            "すべて上書き"
        );

        if (option == 1) return; // キャンセル

        bool overwriteAll = (option == 2);

        // すべてのEnemyDataアセットを検索
        string[] guids = AssetDatabase.FindAssets("t:EnemyData");
        int totalBulletTypes = 0;
        int appliedCount = 0;
        int skippedCount = 0;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            EnemyData data = AssetDatabase.LoadAssetAtPath<EnemyData>(path);
            if (data == null || data.bulletTypes == null) continue;

            bool dirty = false;

            foreach (var bt in data.bulletTypes)
            {
                if (bt == null) continue;
                totalBulletTypes++;

                if (bt.fireVfxPrefab == null || overwriteAll)
                {
                    bt.fireVfxPrefab = selectedPrefab;
                    appliedCount++;
                    dirty = true;
                }
                else
                {
                    skippedCount++;
                }
            }

            if (dirty)
                EditorUtility.SetDirty(data);
        }

        AssetDatabase.SaveAssets();

        EditorUtility.DisplayDialog(
            "Muzzle Flash VFX - 完了",
            $"対象EnemyData: {guids.Length}件\n" +
            $"Bullet Types合計: {totalBulletTypes}個\n" +
            $"設定済み: {appliedCount}個\n" +
            $"スキップ: {skippedCount}個",
            "OK"
        );
    }

    [MenuItem("Tools/Apply Muzzle Flash VFX to All Enemies", validate = true)]
    private static bool ApplyValidate()
    {
        return Selection.activeGameObject != null && EditorUtility.IsPersistent(Selection.activeGameObject);
    }
}
