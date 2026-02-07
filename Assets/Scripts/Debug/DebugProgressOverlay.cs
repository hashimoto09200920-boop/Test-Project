using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Game.Progress;

/// <summary>
/// Progress の現在値を画面右下にオーバーレイ表示するデバッグ用コンポーネント。
/// F1 で表示/非表示（Play中）。停止中（Editモード）でも表示される。
///
/// 変更点（今回）:
/// - 横幅にも固定/自動切替を追加（fixedWidth: 0以下=自動 / 0より大=固定）
///
/// 既存の仕様（前回まで）:
/// - [ExecuteAlways] で停止中も生成・表示
/// - 停止中に生成する Canvas/Back/Text は DontSaveInEditor（保存されない一時オブジェクト）
/// - 再生中のみ DontDestroyOnLoad（常駐）
/// - OnDisable 時、停止中は DestroyImmediate で確実に掃除
/// - F1 トグルは Play 中のみ。停止中の表示は visibleOnStart が反映
/// </summary>
[ExecuteAlways]
public class DebugProgressOverlay : MonoBehaviour
{
    private static DebugProgressOverlay s_instance;

    private Canvas overlayCanvas;
    private TextMeshProUGUI debugText;
    private Image backImage;

    [Header("表示設定")]
    public bool visibleOnStart = true;

    [Header("文字サイズ（1〜24）")]
    [Range(1, 24)]
    public int fontSize = 16;

    [Header("右下からのオフセット(px) 例: X=-24, Y=24")]
    public Vector2 offset = new Vector2(-24, 24);

    [Header("背景パネル")]
    public bool showBackground = true;
    public Color backgroundColor = new Color(0f, 0f, 0f, 0.6f);
    [Tooltip("背景と文字の内側余白(px)")]
    public float boxPadding = 12f;

    [Header("サイズ指定（0以下で自動）")]
    [Tooltip("横幅：0以下=自動計算 / 0より大=この幅で固定")]
    public float fixedWidth = 0f;

    [Tooltip("縦幅：0以下=自動計算 / 0より大=この高さで固定")]
    public float fixedHeight = 0f;

    // 内部状態
    private bool visible;

    private void Awake()
    {
        // Play中のみシングルトンを強制（Edit中は複数置かない運用前提）
        if (Application.isPlaying)
        {
            if (s_instance != null && s_instance != this)
            {
                Destroy(gameObject);
                return;
            }
            s_instance = this;
            DontDestroyOnLoad(gameObject);
        }

        EnsureUI();
        visible = visibleOnStart;
        SetOverlayActive(visible);

        // 起動直後から見えることを保証（Edit/Play 両方）
        if (debugText != null)
        {
            debugText.text = "(overlay active)";
            ApplyLayoutNow();
        }
    }

    private void OnEnable()
    {
        EnsureUI();
        SetOverlayActive(visibleOnStart);
        RefreshAndLayout();
    }

    private void OnDisable()
    {
        // 停止中は生成物を確実に掃除（シーン保存に混入させない）
        if (!Application.isPlaying)
        {
            SafeDestroyEditorOnly(overlayCanvas);
            overlayCanvas = null;
            debugText = null;
            backImage = null;
        }
    }

    private void Update()
    {
        // Play中のみF1でトグル
        if (Application.isPlaying && Input.GetKeyDown(KeyCode.F1))
        {
            visible = !visible;
            SetOverlayActive(visible);
        }

        if (!GetOverlayActive()) return;

        RefreshAndLayout();
    }

    // ===== 基本処理 =====

    private void EnsureUI()
    {
        if (overlayCanvas != null) return;

        // --- Canvas ---
        var canvasGO = new GameObject("DebugProgressOverlayCanvas");
        overlayCanvas = canvasGO.AddComponent<Canvas>();
        overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        overlayCanvas.sortingOrder = 5000;

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize; // 相対縮小の影響を受けにくい
        canvasGO.AddComponent<GraphicRaycaster>();

        if (Application.isPlaying)
        {
            DontDestroyOnLoad(canvasGO);
            canvasGO.hideFlags = HideFlags.None;
        }
        else
        {
            // 停止中は保存対象にしない（Hierarchyには見える）
            canvasGO.hideFlags = HideFlags.DontSaveInEditor;
        }

        // --- 背景 ---
        var backGO = new GameObject("DebugProgressBack");
        backGO.transform.SetParent(canvasGO.transform, false);
        backImage = backGO.AddComponent<Image>();
        backImage.color = backgroundColor;
        if (!Application.isPlaying)
        {
            backGO.hideFlags = HideFlags.DontSaveInEditor;
        }

        // --- 文字 ---
        var textGO = new GameObject("DebugProgressText");
        textGO.transform.SetParent(canvasGO.transform, false);
        debugText = textGO.AddComponent<TextMeshProUGUI>();
        debugText.alignment = TextAlignmentOptions.BottomRight;
        debugText.color = Color.white;
        debugText.textWrappingMode = TextWrappingModes.NoWrap; // Unity6 推奨
        debugText.fontSize = fontSize;
        if (!Application.isPlaying)
        {
            textGO.hideFlags = HideFlags.DontSaveInEditor;
        }

        // 右下固定アンカー
        SetupBottomRight(backImage.rectTransform);
        SetupBottomRight(debugText.rectTransform);
    }

    private static void SetupBottomRight(RectTransform rt)
    {
        rt.anchorMin = new Vector2(1, 0);
        rt.anchorMax = new Vector2(1, 0);
        rt.pivot = new Vector2(1, 0);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(600, 200);
    }

    private void SetOverlayActive(bool on)
    {
        if (overlayCanvas != null && overlayCanvas.gameObject.activeSelf != on)
            overlayCanvas.gameObject.SetActive(on);
    }
    private bool GetOverlayActive() => overlayCanvas != null && overlayCanvas.gameObject.activeSelf;

    private void RefreshAndLayout()
    {
        RefreshProgressText();
        ApplyLayoutNow();
    }

    private void RefreshProgressText()
    {
        if (debugText == null) return;

        var pm = ProgressManager.Instance;
        if (pm == null || pm.Data == null)
        {
            // Editモードなど Progress が無いとき
            debugText.fontSize = fontSize;
            debugText.text = "(overlay active / waiting Progress)";
            return;
        }

        var data = pm.Data;
        string area01 = string.Join(",", data.GetClearedStages(ProgressIds.Area_01));
        string area02 = string.Join(",", data.GetClearedStages(ProgressIds.Area_02));

        string basics = (data.ownedBasicUnitIds != null && data.ownedBasicUnitIds.Count > 0)
            ? string.Join(",", data.ownedBasicUnitIds) : "-";
        string relics = (data.ownedRelicUnitIds != null && data.ownedRelicUnitIds.Count > 0)
            ? string.Join(",", data.ownedRelicUnitIds) : "-";

        int gold = data.gold;

        debugText.fontSize = fontSize;
        debugText.text =
            $"Area_01: [{area01}]\n" +
            $"Area_02: [{area02}]\n" +
            $"Unit_Basic: [{basics}]\n" +
            $"Unit_Relic: [{relics}]\n" +
            $"Gold: {gold:N0}G";
    }

    private void ApplyLayoutNow()
    {
        if (debugText == null || backImage == null) return;

        // 文字列の推奨サイズ
        var preferred = debugText.GetPreferredValues(debugText.text, 1000, 1000);

        // --- 横幅 W ---
        float w;
        if (fixedWidth > 0f)
        {
            // 固定幅（Inspector 指定）
            w = fixedWidth;
        }
        else
        {
            // 自動計算（文字幅 + 余白）
            w = Mathf.Clamp(preferred.x + boxPadding * 2f, 300f, 1200f);
        }

        // --- 縦幅 H ---
        float h;
        if (fixedHeight > 0f)
        {
            h = fixedHeight;
        }
        else
        {
            // 自動計算（行数 × 行高 + 余白）
            int lines = Mathf.Max(1, debugText.text.Split('\n').Length);
            float lineH = fontSize * 1.4f;
            h = Mathf.Clamp(lineH * lines + boxPadding * 2f, 80f, 600f);
        }

        var rtText = debugText.rectTransform;
        var rtBack = backImage.rectTransform;

        // 背景サイズ
        rtBack.sizeDelta = new Vector2(w, h);

        // テキストは内側にパディングぶん小さく
        rtText.sizeDelta = new Vector2(
            Mathf.Max(0f, w - boxPadding * 2f),
            Mathf.Max(0f, h - boxPadding * 2f)
        );

        // 右下から内側へオフセット
        Vector2 inset = new Vector2(Mathf.Abs(offset.x), Mathf.Abs(offset.y));
        rtBack.anchoredPosition = new Vector2(-inset.x, inset.y);
        rtText.anchoredPosition = new Vector2(-inset.x - boxPadding, inset.y + boxPadding);

        // 背景の有効/色
        backImage.enabled = showBackground;
        backImage.color = backgroundColor;
    }

    // ===== Utilities =====

    private void SafeDestroyEditorOnly(Object obj)
    {
        if (obj == null) return;
        if (!Application.isPlaying)
        {
            // 停止中は即時破棄
            DestroyImmediate(obj is Component c ? c.gameObject : obj);
        }
    }
}
