using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Game.UI
{
    /// <summary>
    /// 03_AreaSelectのチュートリアルボタン（ViewTutorialButton）に、
    /// 生成済みの結晶石板アート（Tutorial_1.png等）を適用するEditor専用スタイラー。
    /// アイコンだけで意味を伝える方針のため、文字ラベルは非表示にする。
    /// 以前試した手続き的な「硝子のルーンパネル」装飾（Border/Glow/AccentDot）は
    /// 実画像に置き換わって不要になったため、残っていれば非表示化する。
    /// ViewTutorialButtonに直接アタッチして使う想定（Target Buttonを空にすれば自分自身を対象にする）。
    /// </summary>
    [DisallowMultipleComponent]
    public class TutorialButtonStyler : MonoBehaviour
    {
        [Header("Target")]
        [Tooltip("スタイルを適用するボタンのRectTransform。空なら自分自身（このコンポーネントが付いているGameObject）を使う")]
        [SerializeField] private RectTransform targetButton;

        [Header("Art")]
        [Tooltip("ボタンに表示する結晶石板の画像（例：Tutorial_1.png）")]
        [SerializeField] private Sprite artSprite;

#if UNITY_EDITOR
        [ContextMenu("Apply Art (画像を反映し、文字と旧装飾パーツを整理)")]
        private void ApplyArt()
        {
            var target = targetButton != null ? targetButton : GetComponent<RectTransform>();
            if (target == null)
            {
                Debug.LogError("[TutorialButtonStyler] Target Buttonが見つかりません。");
                return;
            }
            if (artSprite == null)
            {
                Debug.LogError("[TutorialButtonStyler] Art Spriteが未設定です。Tutorial_1.pngなどをインポートして指定してください。");
                return;
            }

            // 1) 背景Imageに石板画像を反映（アスペクト比を保つ）
            var img = target.GetComponent<Image>();
            if (img != null)
            {
                img.sprite = artSprite;
                img.type = Image.Type.Simple;
                img.preserveAspect = true;
                img.color = Color.white;
                img.material = null;
            }

            // 2) 以前試した手続き的な装飾（Border/Glow/AccentDot）が残っていれば非表示化する
            HideChildIfExists(target, "Border");
            HideChildIfExists(target, "Glow");
            HideChildIfExists(target, "AccentDot");

            // 3) アイコンだけで意味を伝える方針のため、文字ラベルは非表示にする
            var label = target.Find("Label");
            if (label != null) label.gameObject.SetActive(false);

            EditorUtility.SetDirty(target);
            Debug.Log("[TutorialButtonStyler] 画像を反映しました。");
        }

        private static void HideChildIfExists(RectTransform parent, string name)
        {
            var child = parent.Find(name);
            if (child != null && child.gameObject.activeSelf) child.gameObject.SetActive(false);
        }

        [Tooltip("チュートリアルボタンのホバー拡大率。ジェム/ドリンク/戻るボタン(1.05)より大きめにしている")]
        [SerializeField] private float tutorialHoverScale = 1.3f;

        /// <summary>
        /// ジェム/ドリンク/戻るボタンと同じ「ButtonHoverEffect」（ホバー拡大＋SE＋点滅）を追加する。
        /// 既に付いている場合も再設定する（拡大率などを変更後に再実行して反映できるように）。
        /// </summary>
        [ContextMenu("Add Hover Effect (ホバー拡大＋SEを追加)")]
        private void AddHoverEffect()
        {
            var target = targetButton != null ? targetButton : GetComponent<RectTransform>();
            if (target == null)
            {
                Debug.LogError("[TutorialButtonStyler] Target Buttonが見つかりません。");
                return;
            }

            var go = target.gameObject;
            var effect = go.GetComponent<ButtonHoverEffect>();
            if (effect == null) effect = go.AddComponent<ButtonHoverEffect>();

            var hoverSE = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/GEM/カーソル移動1.mp3");
            AreaConstellationFX.ApplyStandardHoverEffect(effect, hoverSE, null, tutorialHoverScale);
            EditorUtility.SetDirty(go);
            Debug.Log("[TutorialButtonStyler] ButtonHoverEffectを設定しました。");
        }

        /// <summary>
        /// スマホのタップ確定待ち（TouchTapToConfirm）を追加する。
        /// 1回目のタップでは遷移させずButtonHoverEffectと同じ拡大状態にし、2回目のタップで遷移させる。
        /// マウス操作時は何もせず従来通り。ButtonHoverEffectが既についている前提（先にAdd Hover Effectを実行）。
        /// </summary>
        [ContextMenu("Add Touch Tap-To-Confirm (スマホのタップToConfirmを追加)")]
        private void AddTouchTapToConfirm()
        {
            var target = targetButton != null ? targetButton : GetComponent<RectTransform>();
            if (target == null)
            {
                Debug.LogError("[TutorialButtonStyler] Target Buttonが見つかりません。");
                return;
            }

            var go = target.gameObject;
            if (go.GetComponent<TouchTapToConfirm>() != null)
            {
                Debug.Log("[TutorialButtonStyler] 既にTouchTapToConfirmが付いています。");
                return;
            }

            go.AddComponent<TouchTapToConfirm>();
            EditorUtility.SetDirty(go);
            Debug.Log("[TutorialButtonStyler] TouchTapToConfirmを追加しました。");
        }
#endif
    }
}
