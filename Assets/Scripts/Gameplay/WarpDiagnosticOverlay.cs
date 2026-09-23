using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ★一時的な調査用ツール（スローモーション中の直線ワープ不具合の原因特定ができ次第削除する）。
/// PaddleDrawerが異常なジャンプ（ワープ直線バグ）を検知した瞬間の状態を画面に表示し続ける。
/// スマホ実機ではEditor.logを直接見る方法が無いため、発生時にその場の状態を画面表示し、
/// ユーザーが落ち着いてスクリーンショットを撮れるよう自動では消さない（タップで閉じるまで残る）。
/// Hierarchy/Prefab設定は不要。初回Report()呼び出し時に自分自身でUIを生成する。
/// </summary>
public class WarpDiagnosticOverlay : MonoBehaviour
{
    public static WarpDiagnosticOverlay Instance { get; private set; }

    private Text reportText;
    private GameObject panelRoot;
    private bool hasReport;

    public static void Report(string message)
    {
        if (Instance == null)
        {
            GameObject go = new GameObject("WarpDiagnosticOverlay");
            Instance = go.AddComponent<WarpDiagnosticOverlay>();
        }
        Instance.ShowReport(message);
    }

    private void ShowReport(string message)
    {
        // 最初の1回分だけ保持する（連続発生しても上書きしない。スクショを撮るまで内容を固定するため）
        if (hasReport) return;
        hasReport = true;

        EnsureUI();
        reportText.text = message;
        panelRoot.SetActive(true);
    }

    private void EnsureUI()
    {
        if (panelRoot != null) return;

        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32760; // 最前面に出す
        gameObject.AddComponent<CanvasScaler>();
        gameObject.AddComponent<GraphicRaycaster>();

        panelRoot = new GameObject("Panel");
        panelRoot.transform.SetParent(transform, false);
        RectTransform panelRect = panelRoot.AddComponent<RectTransform>();
        // 画面下半分は描画中の指の位置と重なりやすいため、上側に表示する
        panelRect.anchorMin = new Vector2(0f, 0.62f);
        panelRect.anchorMax = new Vector2(1f, 1f);
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        Image bg = panelRoot.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.85f);

        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(panelRoot.transform, false);
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0.04f, 0.16f);
        textRect.anchorMax = new Vector2(0.96f, 0.97f);
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        reportText = textObj.AddComponent<Text>();
        reportText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        reportText.fontSize = 26;
        reportText.color = Color.white;
        reportText.alignment = TextAnchor.UpperLeft;
        reportText.horizontalOverflow = HorizontalWrapMode.Wrap;
        reportText.verticalOverflow = VerticalWrapMode.Overflow;

        GameObject closeBtnObj = new GameObject("CloseButton");
        closeBtnObj.transform.SetParent(panelRoot.transform, false);
        RectTransform closeRect = closeBtnObj.AddComponent<RectTransform>();
        closeRect.anchorMin = new Vector2(0.28f, 0.03f);
        closeRect.anchorMax = new Vector2(0.72f, 0.13f);
        closeRect.offsetMin = Vector2.zero;
        closeRect.offsetMax = Vector2.zero;

        Image closeBg = closeBtnObj.AddComponent<Image>();
        closeBg.color = new Color(0.8f, 0.2f, 0.2f, 1f);
        Button closeBtn = closeBtnObj.AddComponent<Button>();
        closeBtn.onClick.AddListener(Hide);

        GameObject closeTextObj = new GameObject("Text");
        closeTextObj.transform.SetParent(closeBtnObj.transform, false);
        RectTransform closeTextRect = closeTextObj.AddComponent<RectTransform>();
        closeTextRect.anchorMin = Vector2.zero;
        closeTextRect.anchorMax = Vector2.one;
        closeTextRect.offsetMin = Vector2.zero;
        closeTextRect.offsetMax = Vector2.zero;
        Text closeText = closeTextObj.AddComponent<Text>();
        closeText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        closeText.fontSize = 30;
        closeText.color = Color.white;
        closeText.alignment = TextAnchor.MiddleCenter;
        closeText.text = "閉じる（スクショ後にタップ）";

        panelRoot.SetActive(false);
    }

    private void Hide()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
        hasReport = false; // 閉じたら次の検知も記録できるように再度受付可能にする
    }
}
