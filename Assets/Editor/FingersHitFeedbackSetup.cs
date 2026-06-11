using UnityEngine;
using UnityEditor;

/// <summary>
/// Fingers ボスの EnemyHitFeedback を Balloon ボスと同じ設定値で自動セットアップする。
/// EnemyStats コンポーネントを右クリック → "Setup EnemyHitFeedback (Fingers)" で実行。
/// ※ Prefab Editor で実行してください。
/// </summary>
public static class FingersHitFeedbackSetup
{
    [MenuItem("CONTEXT/EnemyStats/Setup EnemyHitFeedback (Fingers)")]
    static void Setup(MenuCommand cmd)
    {
        var root = ((EnemyStats)cmd.context).gameObject;

        var feedback = root.GetComponent<EnemyHitFeedback>();
        if (feedback == null)
            feedback = root.AddComponent<EnemyHitFeedback>();

        var so = new SerializedObject(feedback);

        // --- Damage Popup Prefab ---
        var popupPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefabs/Effects/DamagePopup.prefab");
        so.FindProperty("damagePopupPrefab").objectReferenceValue = popupPrefab;

        // --- Popup position ---
        so.FindProperty("popupOffset").vector3Value       = new Vector3(0f, 0.1f, 0f);
        so.FindProperty("popupPullToEnemy").floatValue    = 1f;
        so.FindProperty("anchorMode").enumValueIndex      = 1; // Collider2DBoundsCenter
        so.FindProperty("forcePopupAtAnchor").boolValue          = true;
        so.FindProperty("replaceFarHitPosWithAnchor").boolValue  = true;
        so.FindProperty("farHitDistance").floatValue      = 0.1f;
        so.FindProperty("forcePopupZToAnchor").boolValue  = true;
        so.FindProperty("popupLateralOffset").floatValue  = 0.3f;

        // --- Font size ---
        so.FindProperty("popupNormalFontSize").floatValue  = 10f;
        so.FindProperty("popupPoweredFontSize").floatValue = 16f;
        so.FindProperty("autoPopupSize").boolValue         = false;
        so.FindProperty("popupSizeRatio").floatValue       = 7f;
        so.FindProperty("poweredSizeMultiplier").floatValue = 1.3f;

        // --- Colors ---
        so.FindProperty("popupNormalColor").colorValue
            = new Color(1f, 1f, 1f, 1f);
        so.FindProperty("popupPoweredColor").colorValue
            = new Color(1f, 0.92156863f, 0.015686275f, 1f);
        so.FindProperty("popupShieldNormalColor").colorValue
            = new Color(0.55f, 0.78f, 1f, 1f);
        so.FindProperty("popupShieldPoweredColor").colorValue
            = new Color(0.1f, 0.35f, 0.9f, 1f);

        // --- VFX / SE (なし) ---
        so.FindProperty("hitVfxPrefab").objectReferenceValue        = null;
        so.FindProperty("poweredHitVfxPrefab").objectReferenceValue = null;
        so.FindProperty("hitSe").objectReferenceValue               = null;
        so.FindProperty("poweredHitSe").objectReferenceValue        = null;
        so.FindProperty("seVolume").floatValue = 1f;

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(root);

        var prefabStage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
        if (prefabStage != null)
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(prefabStage.scene);

        Debug.Log("[FingersHitFeedbackSetup] EnemyHitFeedback をセットアップしました。");
    }
}
