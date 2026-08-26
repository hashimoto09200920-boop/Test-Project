using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// VsIntroUIのDancer Name Plate Colorを白(乗算なし)に戻す。
    /// NeonDancerネームプレート画像がArea1〜10カラーで10色に塗り分けられたため、
    /// デフォルトのシアン着色(0.3,0.9,1,1)を掛けると色が潰れてしまう不具合の修正。
    /// </summary>
    public static class VsIntroDancerColorFixer
    {
        [MenuItem("Tools/VsIntro/Reset Dancer Name Plate Color To White")]
        public static void ResetToWhite()
        {
            var vsIntro = Object.FindFirstObjectByType<VsIntroUI>(FindObjectsInactive.Include);
            if (vsIntro == null)
            {
                Debug.LogError("[VsIntroDancerColorFixer] VsIntroUIが見つかりません。05_Gameシーンを開いてから実行してください。");
                return;
            }

            var so = new SerializedObject(vsIntro);
            var prop = so.FindProperty("dancerNamePlateColor");
            if (prop == null)
            {
                Debug.LogError("[VsIntroDancerColorFixer] dancerNamePlateColorフィールドが見つかりません。");
                return;
            }

            prop.colorValue = Color.white;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(vsIntro);
            EditorSceneManager.MarkSceneDirty(vsIntro.gameObject.scene);
            Debug.Log("[VsIntroDancerColorFixer] Dancer Name Plate Colorを白(1,1,1,1)にしました。シーンを保存してください。");
        }
    }
}
