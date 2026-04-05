using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 被ダメージ時の画面フラッシュ（赤い半透明オーバーレイ）。
///
/// セットアップ手順:
///   1. 05_Game シーンの Canvas 内に空の GameObject を作成（名前例: DamageFlash）
///   2. このコンポーネントをアタッチ
///   3. Inspector で右クリック →「Setup Flash Image」を実行
///   4. EnemyData の shieldActiveEffectPrefab に設定する（不要）
/// </summary>
[DisallowMultipleComponent]
public class DamageFlashUI : MonoBehaviour
{
    public static DamageFlashUI Instance { get; private set; }

    [Header("Flash Settings")]
    [Tooltip("フラッシュの色（デフォルト: 赤）")]
    [SerializeField] private Color flashColor = new Color(1f, 0f, 0f, 0f);

    [Tooltip("フラッシュのピーク時アルファ値（0〜1）")]
    [SerializeField][Range(0f, 1f)] private float peakAlpha = 0.35f;

    [Tooltip("フラッシュの総時間（秒）")]
    [SerializeField] private float flashDuration = 0.25f;

    [Header("References")]
    [SerializeField] private Image flashImage;

    private Coroutine flashCo;

    private void Awake()
    {
        Instance = this;

        if (flashImage != null)
        {
            var c = flashColor;
            c.a = 0f;
            flashImage.color = c;
        }
    }

    /// <summary>フラッシュを再生する</summary>
    public static void Flash()
    {
        if (Instance != null)
            Instance.PlayFlash();
    }

    public void PlayFlash()
    {
        if (flashImage == null) return;
        if (flashCo != null) StopCoroutine(flashCo);
        flashCo = StartCoroutine(FlashCoroutine());
    }

    private IEnumerator FlashCoroutine()
    {
        float halfDuration = flashDuration * 0.4f; // フェードイン40%、フェードアウト60%
        float fadeOutDuration = flashDuration * 0.6f;

        // フェードイン
        float elapsed = 0f;
        while (elapsed < halfDuration)
        {
            float alpha = Mathf.Lerp(0f, peakAlpha, elapsed / halfDuration);
            SetAlpha(alpha);
            elapsed += Time.deltaTime;
            yield return null;
        }

        // フェードアウト
        elapsed = 0f;
        while (elapsed < fadeOutDuration)
        {
            float alpha = Mathf.Lerp(peakAlpha, 0f, elapsed / fadeOutDuration);
            SetAlpha(alpha);
            elapsed += Time.deltaTime;
            yield return null;
        }

        SetAlpha(0f);
        flashCo = null;
    }

    private void SetAlpha(float alpha)
    {
        if (flashImage == null) return;
        var c = flashColor;
        c.a = alpha;
        flashImage.color = c;
    }

#if UNITY_EDITOR
    [ContextMenu("Setup Flash Image")]
    private void SetupFlashImage()
    {
        // 既存のImageを探す
        flashImage = GetComponentInChildren<Image>();

        if (flashImage == null)
        {
            // 子にImageオブジェクトを作成
            var go = new GameObject("FlashImage");
            go.transform.SetParent(transform, false);
            flashImage = go.AddComponent<Image>();
        }

        // 自身（DamageFlash GameObject）もフルスクリーンに設定
        var selfRt = GetComponent<RectTransform>();
        if (selfRt != null)
        {
            selfRt.anchorMin = Vector2.zero;
            selfRt.anchorMax = Vector2.one;
            selfRt.offsetMin = Vector2.zero;
            selfRt.offsetMax = Vector2.zero;
        }

        // FlashImage子オブジェクトもフルスクリーン設定
        var rt = flashImage.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        // 初期アルファ0
        var c = flashColor;
        c.a = 0f;
        flashImage.color = c;
        flashImage.raycastTarget = false;

        // Canvas内で最前面に
        transform.SetAsLastSibling();

        UnityEditor.EditorUtility.SetDirty(this);
        UnityEditor.EditorUtility.SetDirty(gameObject);
        Debug.Log("[DamageFlashUI] Setup complete.");
    }
#endif
}
