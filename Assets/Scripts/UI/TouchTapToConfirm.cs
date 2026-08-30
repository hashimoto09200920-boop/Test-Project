using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// スマホ操作時、1回目のタップでは本来のonClick（画面遷移・パネルオープン等）を発火させず、
    /// ButtonHoverEffectと同じ「ホバーで拡大」状態にするだけにする。
    /// 拡大中（armed）の同じボタンへの2回目のタップで、初めて本来のonClickを発火させる。
    /// 他のボタンをタップ、または何もない場所をタップ（EmptySpaceTapToDismiss）すると拡大は解除される。
    /// マウス操作時は何もせず、従来通りホバーで拡大→クリックで即遷移のまま変更しない。
    /// Button・ButtonHoverEffectと同じGameObjectに追加して使う。
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class TouchTapToConfirm : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        private static TouchTapToConfirm armedInstance;

        private Button button;
        private ButtonHoverEffect hoverEffect;
        private bool suppressingThisPress;

        private void Awake()
        {
            button = GetComponent<Button>();
            hoverEffect = GetComponent<ButtonHoverEffect>();
        }

        private void OnDisable()
        {
            if (armedInstance == this) armedInstance = null;
            suppressingThisPress = false;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            // ★InputPointerBehaviorSetupでPointer BehaviorをSingleUnifiedPointerに統一しているため、
            //   pointerId(統一ポインタでは意味を持たない)ではなく、実際の入力デバイス種別を示す
            //   pointerTypeでタッチかどうかを判定する。pointerTypeはExtendedPointerEventData
            //   （InputSystemUIInputModuleが実際に渡してくる型）にしかないため、そちらへキャストする。
            if (!(eventData is ExtendedPointerEventData extended) || extended.pointerType != UIPointerType.Touch) return;

            // ★button.interactable=false（ロック中等）のボタンはタップ処理自体を始めから無視する。
            //   これが無いと、後段のRestoreInteractableNextFrame()が無条件にinteractableをtrueへ
            //   戻してしまい、ロックされていたはずのボタンがタッチ操作でだけ押せるようになってしまう。
            if (button != null && !button.interactable) return;

            if (armedInstance == this)
            {
                // 2回目のタップ：このまま本来のonClickを発火させる。
                // ★lockAfterClick（PCではクリック後も拡大維持）なボタンは、ここで縮小せず拡大したままにする。
                //   縮小すると、確定タップの瞬間だけPCと見た目が食い違ってしまうため。
                //   lockAfterClick=falseのボタンは従来通りここで即座に縮小する。
                armedInstance = null;
                bool keepEnlarged = hoverEffect != null && hoverEffect.LockAfterClick;
                if (hoverEffect != null && !keepEnlarged) hoverEffect.SetHeld(false);
                return;
            }

            // 1回目のタップ：他に拡大中のボタンがあれば解除し、自分を拡大状態にする
            if (armedInstance != null) armedInstance.Disarm();
            armedInstance = this;
            if (hoverEffect != null) hoverEffect.SetHeld(true);

            // このタップ分のonClickは発火させない
            suppressingThisPress = true;
            if (button != null) button.interactable = false;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!suppressingThisPress) return;
            suppressingThisPress = false;
            StartCoroutine(RestoreInteractableNextFrame());
        }

        private IEnumerator RestoreInteractableNextFrame()
        {
            yield return null; // このタップのPointerClick判定が終わるまで待ってから戻す
            if (button != null) button.interactable = true;
        }

        private void Disarm()
        {
            if (armedInstance == this) armedInstance = null;
            if (hoverEffect != null) hoverEffect.SetHeld(false);
        }

        /// <summary>他のボタン、または何もない場所がタップされた時に呼ぶ。拡大中のボタンがあれば解除する。</summary>
        public static void DisarmCurrentArmedButton()
        {
            if (armedInstance != null) armedInstance.Disarm();
        }
    }
}
