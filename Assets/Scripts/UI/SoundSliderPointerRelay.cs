using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// スライダーを押した／離したことをSoundPanelFXへ伝える（ドラッグ中の演出・SEの試し鳴らし用）
/// </summary>
public class SoundSliderPointerRelay : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    public SoundPanelFX fx;
    [Tooltip("true=SE側のスライダー（離した時に試し鳴らしする）")]
    public bool isSE;

    public void OnPointerDown(PointerEventData eventData)
    {
        if (fx != null) fx.NotifySliderPointer(isSE, true);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (fx != null) fx.NotifySliderPointer(isSE, false);
    }
}
