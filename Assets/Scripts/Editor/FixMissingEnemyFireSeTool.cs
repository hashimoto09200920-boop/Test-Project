using UnityEditor;
using UnityEngine;

/// <summary>
/// 発射SEが未設定だった敵の各フィールドに、指定のクリップを一括で割り当てる。
/// プレハブの直接編集は禁止のため、このEditor拡張経由でSerializedObjectを使って設定する。
/// ★フィールド名だけで検索すると、同名フィールドを持つ無効化済みEnemyShooter側に
///   誤って設定してしまう事故が起きたため、必ずコンポーネントの型を指定して特定する。
/// </summary>
public static class FixMissingEnemyFireSeTool
{
    private const string TestBulletSePath = "Assets/Audio/Bullet/テスト弾.mp3";
    private const string FireMagic2SePath = "Assets/Audio/Bullet/火炎魔法2.mp3";

    [MenuItem("Tools/★エネミー発射SE未設定分を一括修正")]
    public static void FixMissingFireSe()
    {
        AudioClip testBulletSe = AssetDatabase.LoadAssetAtPath<AudioClip>(TestBulletSePath);
        AudioClip fireMagic2Se = AssetDatabase.LoadAssetAtPath<AudioClip>(FireMagic2SePath);

        if (testBulletSe == null) { Debug.LogError($"[FixMissingEnemyFireSeTool] {TestBulletSePath} が見つかりません。"); return; }
        if (fireMagic2Se == null) { Debug.LogError($"[FixMissingEnemyFireSeTool] {FireMagic2SePath} が見つかりません。"); return; }

        int fixedCount = 0;

        // ★EnemyShooterも同名"fireSE"フィールドを持つため、誤爆防止のため必ずコンポーネント名を明示指定する
        fixedCount += SetClip("Assets/Prefabs/Enemies/WalkerMech.prefab", "WalkerMechController", "fireSE", testBulletSe);
        fixedCount += SetClip("Assets/Prefabs/Enemies/GuardBeast.prefab", "GuardBeastController", "fireSE", testBulletSe);
        fixedCount += SetClip("Assets/Prefabs/Enemies/ArcGuard.prefab", "ArcGuardController", "fireSE", testBulletSe);
        fixedCount += SetClip("Assets/Prefabs/Enemies/Gyrorb.prefab", "GyrorbController", "bombardFireSE", testBulletSe);
        fixedCount += SetClip("Assets/Prefabs/ThunderCloud.prefab", "ThunderCloud", "fireSE", testBulletSe);
        fixedCount += SetClip("Assets/Prefabs/TornadoCloud.prefab", "TornadoCloud", "fireSE", testBulletSe);

        // Dragon：Beam(breath)だけ火炎魔法2、他はテスト弾（palm/missileは既存設定のため触らない）
        fixedCount += SetClip("Assets/Prefabs/Enemies/Dragon.prefab", "MarshalController", "biteFireSE", testBulletSe);
        fixedCount += SetClip("Assets/Prefabs/Enemies/Dragon.prefab", "MarshalController", "wingFireSE", testBulletSe);
        fixedCount += SetClip("Assets/Prefabs/Enemies/Dragon.prefab", "MarshalController", "roarFireSE", testBulletSe);
        fixedCount += SetClip("Assets/Prefabs/Enemies/Dragon.prefab", "MarshalController", "breathFireSE", fireMagic2Se);

        if (fixedCount > 0)
        {
            AssetDatabase.SaveAssets();
        }

        Debug.Log($"[FixMissingEnemyFireSeTool] 完了。{fixedCount}件設定しました。");
    }

    /// <summary>componentTypeNameに一致する型のコンポーネントだけを対象にフィールドを設定する（誤爆防止）。</summary>
    private static int SetClip(string prefabPath, string componentTypeName, string fieldName, AudioClip clip)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
        {
            Debug.LogError($"[FixMissingEnemyFireSeTool] {prefabPath} が見つかりません。");
            return 0;
        }

        foreach (var comp in prefab.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (comp == null) continue;
            if (comp.GetType().Name != componentTypeName) continue;

            var so = new SerializedObject(comp);
            var prop = so.FindProperty(fieldName);
            if (prop == null || prop.propertyType != SerializedPropertyType.ObjectReference)
            {
                Debug.LogError($"[FixMissingEnemyFireSeTool] {prefabPath} の {componentTypeName} に {fieldName} フィールドが見つかりません。");
                return 0;
            }

            prop.objectReferenceValue = clip;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(prefab);
            Debug.Log($"[FixMissingEnemyFireSeTool] {prefabPath} / {componentTypeName}.{fieldName} = {clip.name}");
            return 1;
        }

        Debug.LogError($"[FixMissingEnemyFireSeTool] {prefabPath} に {componentTypeName} コンポーネントが見つかりません。");
        return 0;
    }
}
