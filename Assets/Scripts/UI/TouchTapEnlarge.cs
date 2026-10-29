using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace Game.UI
{
    /// <summary>
    /// 「1回タップで即実行」のボタン用。タッチ操作時、タップした瞬間にButtonHoverEffectのホバー拡大を
    /// 発生させる（PCのマウスホバーに相当する見た目をタッチでも出すため）。
    /// TouchTapToConfirmと違い2回目のタップを要求せず、本来のonClickはそのまま同じタップで即座に発火する。
    /// 決定（クリック）になった時の拡大状態は明示的には解除しない。PCのホバー維持と同じ見た目になり、パネルが閉じて
    /// SetActive(false)になればButtonHoverEffect.OnDisableで自動的に元に戻る。
    /// ★指をボタンの外へずらしてから離した（決定にならなかった）時は、拡大・点滅を元に戻す（2026/10/27）。
    ///   以前は戻さなかったため、なぞるように押すと決定していないボタンが押された見た目のまま残っていた。
    /// Button・ButtonHoverEffectと同じGameObjectに追加して使う。
    /// </summary>
    [RequireComponent(typeof(ButtonHoverEffect))]
    public class TouchTapEnlarge : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        private ButtonHoverEffect hoverEffect;
        private bool heldByThisTouch;

        private void Awake()
        {
            hoverEffect = GetComponent<ButtonHoverEffect>();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!(eventData is ExtendedPointerEventData extended) || extended.pointerType != UIPointerType.Touch) return;
            // ★1回のタップで拡大とonClickが同時に起きるため、ホバーSEは鳴らさない
            //   （クリック確定音と重なって二重に聞こえてしまうため）。
            if (hoverEffect != null) hoverEffect.SetHeld(true, playSE: false);
            heldByThisTouch = true;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!heldByThisTouch) return;
            heldByThisTouch = false;

            // 指を離した場所がこのボタン（またはその子）の上で、決定になるタップなら、拡大したまま（決定の処理に任せる）
            var released = eventData.pointerCurrentRaycast.gameObject;
            bool releasedOnThis = released != null && released.transform.IsChildOf(transform);
            if (releasedOnThis && eventData.eligibleForClick) return;

            // 決定にならなかった（ボタンの外で離した・ドラッグになった）時は元に戻す
            if (hoverEffect != null) hoverEffect.SetHeld(false);
        }
    }
}
