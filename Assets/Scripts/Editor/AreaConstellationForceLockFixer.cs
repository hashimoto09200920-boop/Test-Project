using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Game.UI;

namespace Game.EditorTools
{
    /// <summary>
    /// AreaConstellationFXの「Force Lock Area10」（orbitForceLockArea10）を
    /// 実行時の値に関わらずOFFにする。Area10の実際のアンロック状態をAreaSelectの
    /// 見た目に反映させたい時に使う（未実装テスト用として常時ONだった値を解除する）。
    /// </summary>
    public static class AreaConstellationForceLockFixer
    {
        [MenuItem("Tools/AreaSelect/Disable Force Lock Area10")]
        public static void DisableForceLockArea10()
        {
            var fx = Object.FindFirstObjectByType<AreaConstellationFX>(FindObjectsInactive.Include);
            if (fx == null)
            {
                Debug.LogError("[AreaConstellationForceLockFixer] AreaConstellationFXが見つかりません。03_AreaSelectシーンを開いてから実行してください。");
                return;
            }

            var so = new SerializedObject(fx);
            var prop = so.FindProperty("orbitForceLockArea10");
            if (prop == null)
            {
                Debug.LogError("[AreaConstellationForceLockFixer] orbitForceLockArea10フィールドが見つかりません。");
                return;
            }

            if (!prop.boolValue)
            {
                Debug.Log("[AreaConstellationForceLockFixer] 既にfalseです。変更なし。");
                return;
            }

            prop.boolValue = false;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(fx);
            EditorSceneManager.MarkSceneDirty(fx.gameObject.scene);
            Debug.Log("[AreaConstellationForceLockFixer] orbitForceLockArea10をfalseにしました。シーンを保存してください。");
        }
    }
}
