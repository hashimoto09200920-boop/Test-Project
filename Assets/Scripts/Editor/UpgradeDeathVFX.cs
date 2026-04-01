using UnityEngine;
using UnityEditor;

/// <summary>
/// VFX_Explosion_A_RingSparks に DeathVFXSettings と DeathRingColorCycle をアタッチする。
/// 数値の設定は DeathVFXSettings コンポーネントの Inspector で行う。
/// Tools > Setup Death VFX Components から実行（初回のみ。以後はInspectorで調整）。
/// </summary>
public class UpgradeDeathVFX
{
    private const string PrefabPath = "Assets/Prefabs/Effects/VFX_Explosion_A_RingSparks.prefab";

    [MenuItem("Tools/Setup Death VFX Components")]
    public static void Setup()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        if (root == null)
        {
            Debug.LogError($"[SetupDeathVFX] Prefab not found: {PrefabPath}");
            return;
        }

        // ルートに DeathVFXSettings を追加（既存なら保持）
        if (root.GetComponent<DeathVFXSettings>() == null)
        {
            root.AddComponent<DeathVFXSettings>();
            Debug.Log("[SetupDeathVFX] DeathVFXSettings を追加しました。");
        }
        else
        {
            Debug.Log("[SetupDeathVFX] DeathVFXSettings は既にアタッチ済み。");
        }

        // Ring に DeathRingColorCycle を追加（既存なら保持）
        Transform ringTf = root.transform.Find("Ring");
        if (ringTf != null)
        {
            if (ringTf.GetComponent<DeathRingColorCycle>() == null)
            {
                ringTf.gameObject.AddComponent<DeathRingColorCycle>();
                Debug.Log("[SetupDeathVFX] Ring に DeathRingColorCycle を追加しました。");
            }
            else
            {
                Debug.Log("[SetupDeathVFX] DeathRingColorCycle は既にアタッチ済み。");
            }
        }
        else
        {
            Debug.LogWarning("[SetupDeathVFX] Ring child が見つかりません。");
        }

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        PrefabUtility.UnloadPrefabContents(root);
        AssetDatabase.Refresh();

        Debug.Log("[SetupDeathVFX] 完了。VFX_Explosion_A_RingSparks の Inspector で各値を調整してください。");

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab != null)
        {
            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);
        }
    }
}
