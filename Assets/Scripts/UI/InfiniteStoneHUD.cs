using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 「無限化の石」の所持数表示UI（03_AreaSelect用）。
/// GoldHUDと同じ構成（Icon + TextMeshProUGUI）。0個でも常に表示する。
/// 獲得演出（InfiniteStoneRewardFX）の間は、石が届くまで獲得前の数を表示し、届いたら数え上がって弾む。
/// </summary>
public class InfiniteStoneHUD : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Image stoneIcon;
    [SerializeField] private TextMeshProUGUI stoneText;

    private bool holding;          // 獲得演出の間、表示を獲得前の数のまま止めている
    private Coroutine arriveRoutine;

    /// <summary>獲得演出で石が飛んでいく先（アイコン。無ければこのオブジェクト）</summary>
    public RectTransform IconRect => stoneIcon != null ? stoneIcon.rectTransform : (RectTransform)transform;

    private void Start()
    {
        if (InfiniteStoneManager.Instance == null)
        {
            Debug.LogWarning("[InfiniteStoneHUD] InfiniteStoneManager.Instance is null. Count display will not update.");
            return;
        }

        InfiniteStoneManager.Instance.OnCountChanged += UpdateText;
        UpdateText(InfiniteStoneManager.Instance.Count);
    }

    private void OnDestroy()
    {
        if (InfiniteStoneManager.Instance != null)
            InfiniteStoneManager.Instance.OnCountChanged -= UpdateText;
    }

    private void UpdateText(int amount)
    {
        if (holding) return; // 獲得演出の間は、石が届くまで表示を止める
        SetText(amount);
    }

    private void SetText(int amount)
    {
        GameNumberFont.Apply(stoneText); // ★数字用フォント（Bungee）に切り替え（初回だけ。数字の高さは従来と同じに補正）
        if (stoneText != null)
            stoneText.text = amount.ToString();
    }

    /// <summary>獲得演出：石が届くまで value（獲得前の数）を表示したままにする</summary>
    public void HoldDisplay(int value)
    {
        holding = true;
        SetText(value);
    }

    /// <summary>獲得演出を途中でやめた時：今の所持数の表示に戻す</summary>
    public void ReleaseHold()
    {
        holding = false;
        if (InfiniteStoneManager.Instance != null) SetText(InfiniteStoneManager.Instance.Count);
    }

    /// <summary>獲得演出：石が届いた時。所持数が今の数まで数え上がり、アイコンと数字が弾む</summary>
    public void PlayArrive(float punchScale, float punchSeconds, float countUpSeconds)
    {
        if (arriveRoutine != null) StopCoroutine(arriveRoutine);
        arriveRoutine = StartCoroutine(ArriveRoutine(punchScale, punchSeconds, countUpSeconds));
    }

    private IEnumerator ArriveRoutine(float punchScale, float punchSeconds, float countUpSeconds)
    {
        int from = 0;
        if (stoneText != null) int.TryParse(stoneText.text, out from);
        int to = InfiniteStoneManager.Instance != null ? InfiniteStoneManager.Instance.Count : from;
        Transform iconT = stoneIcon != null ? stoneIcon.transform : null;
        Transform textT = stoneText != null ? stoneText.transform : null;
        Vector3 iconBase = iconT != null ? iconT.localScale : Vector3.one;
        Vector3 textBase = textT != null ? textT.localScale : Vector3.one;
        float dur = Mathf.Max(punchSeconds, countUpSeconds);
        float t = 0f;
        int shown = from;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float pk = punchSeconds > 0f ? Mathf.Clamp01(t / punchSeconds) : 1f;
            float s = 1f + (punchScale - 1f) * Mathf.Sin(pk * Mathf.PI); // 大きくなって元に戻る
            if (iconT != null) iconT.localScale = iconBase * s;
            if (textT != null) textT.localScale = textBase * s;
            float ck = countUpSeconds > 0f ? Mathf.Clamp01(t / countUpSeconds) : 1f;
            int v = Mathf.RoundToInt(Mathf.Lerp(from, to, ck));
            if (v != shown) { shown = v; SetText(v); }
            yield return null;
        }
        if (iconT != null) iconT.localScale = iconBase;
        if (textT != null) textT.localScale = textBase;
        holding = false;
        SetText(to);
        arriveRoutine = null;
    }
}
