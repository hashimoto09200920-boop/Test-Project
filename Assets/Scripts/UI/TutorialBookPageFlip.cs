using System.Collections;
using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// チュートリアルボタンの本の右ページを、周期的にめくるアニメーション。
    /// RectTransformのpivotを綴じ目側(左端)に置いた状態で、localScale.xを 1→-1→1 と
    /// Cos波で滑らかに変化させることで、綴じ目を軸にページが立体的に裏返っているように見せる
    /// (疑似3Dページめくり。UnityのUIにはシェーダーなしで曲面変形はできないため、
    /// スケール反転でめくりの瞬間を表現する簡易的な手法)。
    /// </summary>
    public class TutorialBookPageFlip : MonoBehaviour
    {
        [Tooltip("1回のめくり動作にかかる時間(秒)")]
        [SerializeField] private float flipDuration = 0.6f;
        [Tooltip("次のめくり動作までの待ち時間(秒)の範囲")]
        [SerializeField] private float intervalMin = 3f;
        [SerializeField] private float intervalMax = 6f;

        private RectTransform rt;

        private void Awake()
        {
            rt = GetComponent<RectTransform>();
        }

        private void OnEnable()
        {
            StartCoroutine(FlipLoop());
        }

        private IEnumerator FlipLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(Random.Range(intervalMin, intervalMax));
                yield return StartCoroutine(DoFlip());
            }
        }

        private IEnumerator DoFlip()
        {
            float elapsed = 0f;
            while (elapsed < flipDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / flipDuration);
                float scaleX = Mathf.Cos(t * Mathf.PI * 2f); // 1 → -1 → 1 (綴じ目を軸に1往復)
                rt.localScale = new Vector3(scaleX, 1f, 1f);
                yield return null;
            }
            rt.localScale = new Vector3(1f, 1f, 1f);
        }
    }
}
