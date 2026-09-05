using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// AREA COMPLETE テキストをポップイン演出付きで表示する。
/// StageIntroController.PlayAreaComplete() から yield return StartCoroutine(Play()) で呼ばれる。
/// Hierarchy: Canvas > AreaCompleteTextUI (このコンポーネント + CanvasGroup) > Letter要素...
/// </summary>
public class AreaCompleteTextUI : MonoBehaviour
{
    [Header("Letters")]
    [Tooltip("ポップイン順に並べたRectTransform配列（1文字 or 1単語単位）")]
    [SerializeField] private GameObject[] letterElements;
    [Tooltip("各要素のポップイン間隔（秒）")]
    [SerializeField] private float letterStagger    = 0.05f;

    [Header("Bounce")]
    [Tooltip("ポップイン時のオーバーシュートスケール")]
    [SerializeField] private float bounceScale      = 1.35f;
    [Tooltip("1要素のバウンスイン時間（秒）")]
    [SerializeField] private float bounceInDuration = 0.25f;

    [Header("Timing")]
    [Tooltip("全文字表示後の維持時間（秒）")]
    [SerializeField] private float displayDuration  = 2.5f;
    [Tooltip("フェードアウト時間（秒）")]
    [SerializeField] private float fadeOutDuration  = 0.6f;

    [Header("Image Mode (letterElementsを画像に差し替える場合)")]
    [Tooltip("Convert Letters To Images実行時に設定する、各文字画像のLayoutElement幅(px)")]
    [SerializeField] private float imageLetterWidth  = 110f;
    [Tooltip("Convert Letters To Images実行時に設定する、各文字画像のLayoutElement高さ(px)")]
    [SerializeField] private float imageLetterHeight = 110f;

    private CanvasGroup canvasGroup;

    private void Awake()
    {
        InitCanvasGroup();
        canvasGroup.alpha = 0f;
    }

    private void InitCanvasGroup()
    {
        if (canvasGroup != null) return;
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();

        // ★alpha=0（見た目は透明）にしてもblocksRaycastsは既定でtrueのままのため、
        // 非表示中もこのテキストが他のUI（中断メニューのボタン等）のクリックを吸収してしまっていた。
        // このテキストはただの演出表示でクリック操作を必要としないため、常にfalseにする
        canvasGroup.blocksRaycasts = false;
    }

    public IEnumerator Play()
    {
        // Awake() が未実行の場合（親が非アクティブで遅延）に備えて遅延初期化
        InitCanvasGroup();
        canvasGroup.alpha = 1f;

        // 全要素のRectTransformを収集しスケール0で初期化
        if (letterElements == null || letterElements.Length == 0)
        {
            yield return new WaitForSeconds(displayDuration);
        }
        else
        {
            var rts = new RectTransform[letterElements.Length];
            for (int i = 0; i < letterElements.Length; i++)
            {
                if (letterElements[i] != null)
                {
                    rts[i] = letterElements[i].GetComponent<RectTransform>();
                    if (rts[i] != null) rts[i].localScale = Vector3.zero;
                }
            }

            // サブコルーチンを使わず1ループで全文字を並列アニメーション
            // (StartCoroutine を this に対して呼ぶと非アクティブ時に失敗するため)
            float totalAnimTime = (letterElements.Length - 1) * letterStagger + bounceInDuration;
            float elapsed = 0f;
            while (elapsed < totalAnimTime)
            {
                elapsed += Time.deltaTime;
                for (int i = 0; i < rts.Length; i++)
                {
                    if (rts[i] == null) continue;
                    float t = Mathf.Clamp01((elapsed - i * letterStagger) / bounceInDuration);
                    if (t <= 0f) continue;
                    float s = t < 0.6f
                        ? Mathf.Lerp(0f, bounceScale, t / 0.6f)
                        : Mathf.Lerp(bounceScale, 1f, (t - 0.6f) / 0.4f);
                    rts[i].localScale = Vector3.one * s;
                }
                yield return null;
            }
            for (int i = 0; i < rts.Length; i++)
            {
                if (rts[i] != null) rts[i].localScale = Vector3.one;
            }

            yield return new WaitForSeconds(displayDuration);
        }

        // フェードアウト
        float elapsed2 = 0f;
        while (elapsed2 < fadeOutDuration)
        {
            elapsed2 += Time.deltaTime;
            canvasGroup.alpha = 1f - Mathf.Clamp01(elapsed2 / fadeOutDuration);
            yield return null;
        }
        canvasGroup.alpha = 0f;
    }

#if UNITY_EDITOR
    /// <summary>
    /// letterElements(12個、A・R・E・A・C・O・M・P・L・E・T・Eの順)の各GameObjectに、
    /// Assets/Art/Result配下の対応するネオン文字画像をImageとして追加する。
    /// 既存のTextMeshProUGUIは削除せずenabled=falseにするだけ（復元できるように残す）。
    /// ポップイン/フェードアウトのアニメーションはRectTransform.localScaleとCanvasGroup.alphaしか
    /// 触らないため、TextでもImageでも同じ演出のまま動く。
    /// 親のHorizontalLayoutGroup(ChildControlWidth/Height=true)がサイズを管理しているため、
    /// LayoutElementでimageLetterWidth/Heightを明示的に指定する。
    /// 非破壊的な処理で再実行しても安全。
    /// </summary>
    [ContextMenu("Convert Letters To Images (letterElementsを画像に差し替え)")]
    private void ConvertLettersToImages()
    {
        if (letterElements == null || letterElements.Length == 0)
        {
            Debug.LogWarning("[AreaCompleteTextUI] letterElementsが未設定です。");
            return;
        }

        string[] files =
        {
            "①A_AreaComplete", "②R_AreaComplete", "③E_AreaComplete", "④A_AreaComplete",
            "⑤C_AreaComplete", "⑥O_AreaComplete", "⑦M_AreaComplete", "⑧P_AreaComplete",
            "⑨L_AreaComplete", "⑩E_AreaComplete", "⑪T_AreaComplete", "⑫E_AreaComplete",
        };

        if (letterElements.Length != files.Length)
        {
            Debug.LogError($"[AreaCompleteTextUI] letterElementsの数({letterElements.Length})が想定({files.Length})と一致しません。");
            return;
        }

        int converted = 0;
        for (int i = 0; i < letterElements.Length; i++)
        {
            var go = letterElements[i];
            if (go == null)
            {
                Debug.LogWarning($"[AreaCompleteTextUI] letterElements[{i}]({files[i]})が未設定(None)です。スキップします。");
                continue;
            }

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Art/Result/{files[i]}.png");
            if (sprite == null)
            {
                Debug.LogWarning($"[AreaCompleteTextUI] {files[i]}.png が見つかりません。スキップします。");
                continue;
            }

            var tmp = go.GetComponent<TextMeshProUGUI>();
            if (tmp != null) tmp.enabled = false;

            // ★既存のgoには既にTextMeshProUGUI(Graphic)が乗っており、同じGameObjectへ
            //   AddComponent<Image>()すると（Editor上のGraphic登録処理と衝突するためか）nullが返ってきて
            //   追加できない現象を確認した。回避のため、Imageは新規の子GameObjectとして追加する
            //   （親のRectTransform.localScaleアニメーションは子にもそのまま効くため演出は変わらない）。
            var imgTf = go.transform.Find("LetterImage");
            GameObject imgGo = imgTf != null ? imgTf.gameObject : new GameObject("LetterImage", typeof(RectTransform), typeof(Image));
            imgGo.transform.SetParent(go.transform, false);
            var imgRt = (RectTransform)imgGo.transform;
            imgRt.anchorMin = Vector2.zero;
            imgRt.anchorMax = Vector2.one;
            imgRt.offsetMin = Vector2.zero;
            imgRt.offsetMax = Vector2.zero;

            var img = imgGo.GetComponent<Image>();
            if (img == null)
            {
                Debug.LogError($"[AreaCompleteTextUI] letterElements[{i}]({go.name})の子にImageを追加できませんでした。スキップします。");
                continue;
            }
            img.sprite = sprite;
            img.type = Image.Type.Simple;
            img.preserveAspect = true;
            img.raycastTarget = false;

            var le = go.GetComponent<LayoutElement>();
            if (le == null) le = go.AddComponent<LayoutElement>();
            if (le == null)
            {
                Debug.LogError($"[AreaCompleteTextUI] letterElements[{i}]({go.name})にLayoutElementを追加できませんでした。スキップします。");
                continue;
            }
            le.preferredWidth  = imageLetterWidth;
            le.preferredHeight = imageLetterHeight;

            EditorUtility.SetDirty(go);
            EditorUtility.SetDirty(imgGo);
            converted++;
        }

        EditorUtility.SetDirty(this);
        Debug.Log($"[AreaCompleteTextUI] {converted}/{letterElements.Length}文字を画像に差し替えました。");
    }
#endif
}
