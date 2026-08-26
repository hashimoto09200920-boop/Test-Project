using UnityEditor;
using UnityEngine;
using Game.UI;

namespace Game.EditorTools
{
    /// <summary>
    /// ButtonHoverEffectに追加した「クリック後もホバー拡大・点滅した見た目のまま固定する」機能
    /// (lockAfterClick)は、意図せず全ボタンに影響しないようデフォルトOFF（オプトイン）にした。
    /// この機能が本来必要な3箇所（AreaSelectのジェム/ドリンクボタン、ドリンク購入画面のEXITボタン）
    /// だけ、ONに設定し直す。03_AreaSelect.unityを開いた状態で実行すること。
    /// </summary>
    public static class ButtonHoverLockFixer
    {
        /// <summary>
        /// GameObject.Findは非アクティブなオブジェクトを見つけられない（GemPanel/ShopPanel配下の
        /// ボタンはPlay前は非表示＝非アクティブになっているため、これで何度も見つからず失敗していた）。
        /// Resources.FindObjectsOfTypeAllで非アクティブも含めて検索し、実際のシーンに存在するものだけに絞る。
        /// </summary>
        private static GameObject FindInScene(string name)
        {
            foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
            {
                if (t.name != name) continue;
                if (!t.gameObject.scene.IsValid()) continue; // プレハブアセット等を除外
                if (EditorUtility.IsPersistent(t.gameObject)) continue; // アセット本体を除外
                return t.gameObject;
            }
            return null;
        }

        private static readonly string[] TargetNames =
        {
            "GemManagementButton",
            "ShopButton",
            "CloseButton", // ドリンク購入画面(ShopPanel)のEXITボタン
            "SharedSellButton", // 売却確認ダイアログ表示中もホバー拡大を維持する（GemManagementUI.HideSellConfirmDialog()でForceReset）
        };

        [MenuItem("Tools/Fix ButtonHoverEffect Lock After Click (対象3ボタンだけON)")]
        public static void FixLockAfterClick()
        {
            int fixedCount = 0;
            foreach (var name in TargetNames)
            {
                var go = FindInScene(name);
                if (go == null)
                {
                    Debug.LogError($"[ButtonHoverLockFixer] シーン内に見つかりません: {name}");
                    continue;
                }

                var hover = go.GetComponent<ButtonHoverEffect>();
                if (hover == null)
                {
                    Debug.LogError($"[ButtonHoverLockFixer] ButtonHoverEffectが付いていません: {name}");
                    continue;
                }

                var so = new SerializedObject(hover);
                so.FindProperty("lockAfterClick").boolValue = true;
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(hover);
                fixedCount++;
                Debug.Log($"[ButtonHoverLockFixer] lockAfterClickをONにしました: {name}");
            }

            Debug.Log($"[ButtonHoverLockFixer] 完了。{fixedCount}/{TargetNames.Length}件設定しました。");
        }

        private static readonly string[] RequireInteractableTargetNames =
        {
            "SharedSellButton",
            "SharedEquipButton",
        };

        /// <summary>
        /// SharedSellButton/SharedEquipButtonは選択状況に応じてinteractableがfalseになるが、
        /// ButtonHoverEffect側のrequireInteractableがOFFだったため、無効状態でもホバーすると
        /// （暗い色のまま）点滅してしまっていた。ONにしてホバーエフェクト自体を無効時は発生させない。
        /// </summary>
        [MenuItem("Tools/Fix ButtonHoverEffect Require Interactable (Sell/Equipボタン)")]
        public static void FixRequireInteractable()
        {
            int fixedCount = 0;
            foreach (var name in RequireInteractableTargetNames)
            {
                var go = FindInScene(name);
                if (go == null)
                {
                    Debug.LogError($"[ButtonHoverLockFixer] シーン内に見つかりません: {name}");
                    continue;
                }

                var hover = go.GetComponent<ButtonHoverEffect>();
                if (hover == null)
                {
                    Debug.LogError($"[ButtonHoverLockFixer] ButtonHoverEffectが付いていません: {name}");
                    continue;
                }

                var so = new SerializedObject(hover);
                so.FindProperty("requireInteractable").boolValue = true;
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(hover);
                fixedCount++;
                Debug.Log($"[ButtonHoverLockFixer] requireInteractableをONにしました: {name}");
            }

            Debug.Log($"[ButtonHoverLockFixer] 完了。{fixedCount}/{RequireInteractableTargetNames.Length}件設定しました。");
        }
    }
}
