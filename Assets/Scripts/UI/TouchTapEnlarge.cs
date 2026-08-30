using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace Game.UI
{
    /// <summary>
    /// 「1回タップで即実行」のボタン用。タッチ操作時、タップした瞬間にButtonHoverEffectのホバー拡大を
    /// 発生させる（PCのマウスホバーに相当する見た目をタッチでも出すため）。
    /// TouchTapToConfirmと違い2回目のタップを要求せず、本来のonClickはそのまま同じタップで即座に発火する。
    /// 拡大状態は明示的には解除しない。PCのホバー維持と同じ見た目になり、パネルが閉じて
    /// SetActive(false)になればButtonHoverEffect.OnDisableで自動的に元に戻る。
    /// Button・ButtonHoverEffectと同じGameObjectに追加して使う。
    /// </summary>
    [RequireComponent(typeof(ButtonHoverEffect))]
    public class TouchTapEnlarge : MonoBehaviour, IPointerDownHandler
    {
        private ButtonHoverEffect hoverEffect;

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
        }
    }
}
