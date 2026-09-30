using UnityEditor;
using UnityEngine;

/// <summary>
/// ヒットVFXのプーリング対応：ParticleSystemのStop ActionがDestroyのままだと、
/// 再生が自然に終わった時点でUnity自身がGameObjectを破棄してしまい、
/// プールに返却する前に消えてプーリングが機能しなくなる。
/// 対象プレハブのStop ActionをNoneに変更し、生死の管理を完全にコード側
/// （HitVfxPool / EnemyBulletFeedback内のDisappearVfxPool）に委ねる。
/// </summary>
public static class FixVfxHitStopActionTool
{
    private static readonly string[] TargetPrefabPaths =
    {
        "Assets/Prefabs/Effects/VFX_EnemyHit_Normal.prefab",
        "Assets/Prefabs/Effects/VFX_BulletDestroy.prefab",
        "Assets/Prefabs/Effects/WallHitSpark_Orange.prefab",
        "Assets/Prefabs/Effects/VFX_JustReflect.prefab",
        "Assets/Prefabs/Effects/VFX_AttackBlock.prefab",
        "Assets/Prefabs/Effects/VFX_NormalReflect.prefab",
        "Assets/Prefabs/Effects/JustHitSpark_OrangeStrong.prefab",
    };

    [MenuItem("Tools/★ヒットVFXのStop ActionをNoneに変更")]
    public static void FixStopAction()
    {
        int fixedCount = 0;

        foreach (string path in TargetPrefabPaths)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogError($"[FixVfxHitStopActionTool] {path} が見つかりません。");
                continue;
            }

            ParticleSystem ps = prefab.GetComponent<ParticleSystem>();
            if (ps == null)
            {
                Debug.LogError($"[FixVfxHitStopActionTool] {path} にParticleSystemがありません。");
                continue;
            }

            ParticleSystem.MainModule main = ps.main;
            if (main.stopAction == ParticleSystemStopAction.Destroy)
            {
                main.stopAction = ParticleSystemStopAction.None;
                EditorUtility.SetDirty(prefab);
                fixedCount++;
                Debug.Log($"[FixVfxHitStopActionTool] {path} のStop ActionをNoneに変更しました。");
            }
            else
            {
                Debug.Log($"[FixVfxHitStopActionTool] {path} は既にDestroy以外（{main.stopAction}）のためスキップしました。");
            }
        }

        if (fixedCount > 0)
        {
            AssetDatabase.SaveAssets();
        }

        Debug.Log($"[FixVfxHitStopActionTool] 完了。{fixedCount}件変更しました。");
    }
}
