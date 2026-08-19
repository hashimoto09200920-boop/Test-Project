using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.UI
{
    /// <summary>
    /// 何もない場所（他のボタンに当たらなかった場所）がタップされた時に、
    /// TouchTapToConfirmで拡大中のボタンがあれば解除する。
    /// 透明・Raycast Target ONのフルストレッチImageと同じGameObjectに追加して使う。
    /// </summary>
    public class EmptySpaceTapToDismiss : MonoBehaviour, IPointerClickHandler
    {
        public void OnPointerClick(PointerEventData eventData)
        {
            TouchTapToConfirm.DisarmCurrentArmedButton();
        }
    }
}
