using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 03_AreaSelect の HUD にスローモーションUI要素を視覚的に表示する（機能なし）
/// SlowMotionButton / SlowMotionGaugeInner / SlowMotionGauge / SlowMotionGaugeBackground
/// Editor拡張でPlay前にHierarchy生成、Inspector調整可能
/// </summary>
public class SlowMotionHUDVisualUI : MonoBehaviour
{
    [Header("References（Setup後に自動設定）")]
    [SerializeField] private Image slowMotionButton;
    [SerializeField] private Image gaugeInner;
    [SerializeField] private Image gauge;
    [SerializeField] private Image gaugeBackground;

    [Header("Layout Settings")]
    [Tooltip("砂時計ボタン・GaugeInner の位置（anchoredPosition）")]
    [SerializeField] private Vector2 buttonPosition = new Vector2(140f, -320f);

    [Tooltip("ゲージバー・背景の位置（anchoredPosition）")]
    [SerializeField] private Vector2 gaugePosition = new Vector2(140f, -220f);

    [Tooltip("砂時計ボタンのサイズ（px）")]
    [SerializeField] private Vector2 buttonSize = new Vector2(200f, 200f);

    [Tooltip("GaugeInner（円）のサイズ（px）")]
    [SerializeField] private Vector2 gaugeInnerSize = new Vector2(160f, 160f);

    [Tooltip("ゲージバーのサイズ（px）")]
    [SerializeField] private Vector2 gaugeBarSize = new Vector2(180f, 20f);

    // ========== Public API ==========

    public void Show()
    {
        SetObjectsActive(true);
    }

    public void Hide()
    {
        SetObjectsActive(false);
    }

    private void SetObjectsActive(bool active)
    {
        if (slowMotionButton != null) slowMotionButton.gameObject.SetActive(active);
        if (gaugeInner != null) gaugeInner.gameObject.SetActive(active);
        if (gauge != null) gauge.gameObject.SetActive(active);
        if (gaugeBackground != null) gaugeBackground.gameObject.SetActive(active);
    }

    private void Start()
    {
        // GemManagementPanel表示中のみ表示
        Hide();
    }

    [Header("Color Settings")]
    [SerializeField] private Color gaugeBarStartColor = new Color(0.0f, 0.8f, 1.0f, 1f);
    [SerializeField] private Color gaugeBarEndColor = new Color(0.0f, 0.2f, 0.7f, 1f);
    [SerializeField] private Color gaugeBackgroundColor = new Color(0.2f, 0.2f, 0.2f, 0.4f);
    [SerializeField] private Color gaugeInnerCenterColor = new Color(0.2f, 0.6f, 0.6f, 1f);
    [SerializeField] private Color gaugeInnerEdgeColor = new Color(0.05f, 0.05f, 0.05f, 1f);
    [SerializeField] private Color buttonColor = new Color(0.8f, 0.8f, 0.8f, 1f);

#if UNITY_EDITOR
    [ContextMenu("Setup Slow Motion HUD Visual")]
    private void SetupSlowMotionHUDVisual()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("[SlowMotionHUDVisualUI] Canvas not found!");
            return;
        }

        // 既存を削除して再生成
        foreach (string name in new[] { "SlowMotionGaugeBackground", "SlowMotionGauge", "SlowMotionGaugeInner", "SlowMotionButton" })
        {
            Transform existing = canvas.transform.Find(name);
            if (existing != null)
            {
                DestroyImmediate(existing.gameObject);
                Debug.Log($"[SlowMotionHUDVisualUI] Removed existing {name}.");
            }
        }

        Sprite hourglassSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Skill/A10.png");

        // === SlowMotionGaugeBackground ===
        GameObject bgObj = CreateUIObject("SlowMotionGaugeBackground", canvas.transform,
            gaugePosition, gaugeBarSize);
        Image bgImg = bgObj.AddComponent<Image>();
        bgImg.sprite = CreateWhiteRectSprite();
        bgImg.type = Image.Type.Simple;
        bgImg.color = gaugeBackgroundColor;
        gaugeBackground = bgImg;

        // === SlowMotionGauge ===
        GameObject gaugeObj = CreateUIObject("SlowMotionGauge", canvas.transform,
            gaugePosition, gaugeBarSize);
        Image gaugeImg = gaugeObj.AddComponent<Image>();
        gaugeImg.sprite = CreateHorizontalGradientSprite(gaugeBarStartColor, gaugeBarEndColor);
        gaugeImg.type = Image.Type.Filled;
        gaugeImg.fillMethod = Image.FillMethod.Horizontal;
        gaugeImg.fillOrigin = (int)Image.OriginHorizontal.Left;
        gaugeImg.fillAmount = 1f;
        gaugeImg.color = Color.white;
        gauge = gaugeImg;

        // === SlowMotionGaugeInner ===
        GameObject innerObj = CreateUIObject("SlowMotionGaugeInner", canvas.transform,
            buttonPosition, gaugeInnerSize);
        Image innerImg = innerObj.AddComponent<Image>();
        innerImg.sprite = CreateRadialGradientSprite(gaugeInnerCenterColor, gaugeInnerEdgeColor);
        innerImg.type = Image.Type.Simple;
        innerImg.color = Color.white;
        gaugeInner = innerImg;

        // === SlowMotionButton ===
        GameObject btnObj = CreateUIObject("SlowMotionButton", canvas.transform,
            buttonPosition, buttonSize);
        Image btnImg = btnObj.AddComponent<Image>();
        btnImg.sprite = hourglassSprite;
        btnImg.type = Image.Type.Simple;
        btnImg.color = buttonColor;
        slowMotionButton = btnImg;

        // 描画順: BG < Gauge < Inner < Button
        bgObj.transform.SetAsLastSibling();
        gaugeObj.transform.SetAsLastSibling();
        innerObj.transform.SetAsLastSibling();
        btnObj.transform.SetAsLastSibling();

        // Play前は非表示（GemManagementPanel Open時のみ表示）
        bgObj.SetActive(false);
        gaugeObj.SetActive(false);
        innerObj.SetActive(false);
        btnObj.SetActive(false);

        // 参照を保存
        UnityEditor.SerializedObject so = new UnityEditor.SerializedObject(this);
        so.Update();
        so.FindProperty("slowMotionButton").objectReferenceValue = btnImg;
        so.FindProperty("gaugeInner").objectReferenceValue = innerImg;
        so.FindProperty("gauge").objectReferenceValue = gaugeImg;
        so.FindProperty("gaugeBackground").objectReferenceValue = bgImg;
        so.ApplyModifiedProperties();

        UnityEditor.EditorUtility.SetDirty(this);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
        Debug.Log("[SlowMotionHUDVisualUI] Setup complete!");
    }

    [ContextMenu("Apply Layout Settings")]
    private void ApplyLayoutSettings()
    {
        if (slowMotionButton != null)
        {
            UnityEditor.Undo.RecordObject(slowMotionButton.rectTransform, "Apply SlowMotion HUD Layout");
            slowMotionButton.rectTransform.anchoredPosition = buttonPosition;
            slowMotionButton.rectTransform.sizeDelta = buttonSize;
            UnityEditor.EditorUtility.SetDirty(slowMotionButton.gameObject);
        }
        if (gaugeInner != null)
        {
            UnityEditor.Undo.RecordObject(gaugeInner.rectTransform, "Apply SlowMotion HUD Layout");
            gaugeInner.rectTransform.anchoredPosition = buttonPosition;
            gaugeInner.rectTransform.sizeDelta = gaugeInnerSize;
            UnityEditor.EditorUtility.SetDirty(gaugeInner.gameObject);
        }
        if (gauge != null)
        {
            UnityEditor.Undo.RecordObject(gauge.rectTransform, "Apply SlowMotion HUD Layout");
            gauge.rectTransform.anchoredPosition = gaugePosition;
            gauge.rectTransform.sizeDelta = gaugeBarSize;
            gauge.sprite = CreateHorizontalGradientSprite(gaugeBarStartColor, gaugeBarEndColor);
            UnityEditor.EditorUtility.SetDirty(gauge.gameObject);
        }
        if (gaugeBackground != null)
        {
            UnityEditor.Undo.RecordObject(gaugeBackground.rectTransform, "Apply SlowMotion HUD Layout");
            gaugeBackground.rectTransform.anchoredPosition = gaugePosition;
            gaugeBackground.rectTransform.sizeDelta = gaugeBarSize;
            gaugeBackground.color = gaugeBackgroundColor;
            UnityEditor.EditorUtility.SetDirty(gaugeBackground.gameObject);
        }
        if (gaugeInner != null)
        {
            gaugeInner.sprite = CreateRadialGradientSprite(gaugeInnerCenterColor, gaugeInnerEdgeColor);
            UnityEditor.EditorUtility.SetDirty(gaugeInner.gameObject);
        }

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null)
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
        Debug.Log("[SlowMotionHUDVisualUI] Layout applied.");
    }

    private static GameObject CreateUIObject(string name, Transform parent, Vector2 anchoredPos, Vector2 size)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        RectTransform rt = obj.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;
        return obj;
    }

    private static Sprite CreateWhiteRectSprite()
    {
        Texture2D tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        Color[] colors = new Color[16];
        for (int i = 0; i < 16; i++) colors[i] = Color.white;
        tex.SetPixels(colors);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
    }

    private static Sprite CreateHorizontalGradientSprite(Color startColor, Color endColor)
    {
        int width = 64, height = 4;
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                tex.SetPixel(x, y, Color.Lerp(startColor, endColor, (float)x / (width - 1)));
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f));
    }

    private static Sprite CreateRadialGradientSprite(Color centerColor, Color edgeColor)
    {
        int size = 128;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Vector2 center = new Vector2(size / 2f, size / 2f);
        float radius = size / 2f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), center);
                tex.SetPixel(x, y, d <= radius
                    ? Color.Lerp(centerColor, edgeColor, d / radius)
                    : Color.clear);
            }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }
#endif
}
