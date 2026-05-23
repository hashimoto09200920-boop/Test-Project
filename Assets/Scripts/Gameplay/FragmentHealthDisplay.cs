using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// HalloweenFragment 用 HP/Shield バー表示。
/// EnemyStats を持たないフラグメントに、ボスの共有 HP/Shield を表示する。
/// EnemyHealthDisplay と同じ座標計算・グラデーション方式。
/// 使い方: フラグメント GameObject にアタッチし、Init() を呼ぶ。
/// </summary>
public class FragmentHealthDisplay : MonoBehaviour
{
    [Header("Bar Settings")]
    [Tooltip("バーの幅（ワールド座標）")]
    [SerializeField] private float barWidth = 0.5f;
    [Tooltip("バーの高さ（ワールド座標）")]
    [SerializeField] private float barHeight = 0.06f;
    [Tooltip("ShieldバーとHPバーの縦間隔（ワールド座標）")]
    [SerializeField] private float barSpacing = 0.09f;
    [Tooltip("バー表示の Y オフセット（フラグメントからの距離）")]
    [SerializeField] private float displayOffsetY = 0.4f;
    [Tooltip("バーの X オフセット")]
    [SerializeField] private float barOffsetX = 0f;
    [Tooltip("HP/Shield数値テキストの X オフセット（バー右端からの距離）")]
    [SerializeField] private float numberOffsetX = 0.1f;
    [Tooltip("HP/Shield数値テキストのフォントサイズ")]
    [SerializeField] private int fontSize = 60;

    [Header("Background Colors")]
    [SerializeField] private Color hpBarBGColor    = new Color(0f, 0.2f, 0f,   1f);
    [SerializeField] private Color shieldBarBGColor = new Color(0f, 0.2f, 0.2f, 1f);

    // ─── 外部参照 ───────────────────────────────────────────
    private EnemyStats  sourceStats;
    private EnemyShield sourceShield;

    // ─── バー オブジェクト ───────────────────────────────────
    private GameObject      hpBarObject,     hpBarBGObject;
    private GameObject      shieldBarObject, shieldBarBGObject;
    private SpriteRenderer  hpBarRenderer,   shieldBarRenderer;
    private Transform       hpBarTransform,  hpBarBGTransform;
    private Transform       shieldBarTransform, shieldBarBGTransform;

    // ─── 数値テキスト ────────────────────────────────────────
    private TextMesh  hpNumberText;
    private TextMesh  shieldNumberText;
    private GameObject hpNumberObject;
    private GameObject shieldNumberObject;

    private readonly List<Texture2D> runtimeTextures = new();
    private readonly List<Sprite>    runtimeSprites  = new();

    private bool initialized;

    // ─── 初期化 ─────────────────────────────────────────────
    public void Init(EnemyStats stats, EnemyShield shield)
    {
        sourceStats  = stats;
        sourceShield = shield;
        if (!initialized)
        {
            CreateBars();
            initialized = true;
        }
    }

    public void SetBarSize(float width, float height, float spacing, float offsetY, float offsetX,
                           float numOffsetX, int fontSz)
    {
        barWidth       = width;
        barHeight      = height;
        barSpacing     = spacing;
        displayOffsetY = offsetY;
        barOffsetX     = offsetX;
        numberOffsetX  = numOffsetX;
        fontSize       = fontSz;
        if (hpNumberText     != null) hpNumberText.fontSize     = fontSz;
        if (shieldNumberText != null) shieldNumberText.fontSize = fontSz;
    }

    public void SetBarsVisible(bool visible)
    {
        hpBarObject?.SetActive(visible);
        hpBarBGObject?.SetActive(visible);
        hpNumberObject?.SetActive(visible);
        if (shieldBarObject   != null) shieldBarObject.SetActive(visible && sourceShield != null && sourceShield.IsEnabled);
        if (shieldBarBGObject != null) shieldBarBGObject.SetActive(visible && sourceShield != null && sourceShield.IsEnabled);
        if (shieldNumberObject != null) shieldNumberObject.SetActive(visible && sourceShield != null && sourceShield.IsEnabled);
    }

    // ─── バー生成 ────────────────────────────────────────────
    private void CreateBars()
    {
        Color[] hpColors = { new Color(0f, 0.5f, 0f), new Color(0f, 0.6f, 0f), new Color(0f, 0.8f, 0f), Color.green };
        hpBarObject    = CreateGradientBar("HPBar",   hpColors,                             displayOffsetY, 10);
        hpBarTransform = hpBarObject.transform;
        hpBarRenderer  = hpBarObject.GetComponent<SpriteRenderer>();

        hpBarBGObject    = CreateGradientBar("HPBarBG", new[] { hpBarBGColor, hpBarBGColor }, displayOffsetY, 9);
        hpBarBGTransform = hpBarBGObject.transform;

        Color[] shieldColors = { new Color(0f, 0.5f, 0.5f), new Color(0f, 0.6f, 0.6f), new Color(0f, 0.8f, 0.8f), Color.cyan };
        shieldBarObject    = CreateGradientBar("ShieldBar",   shieldColors,                                   displayOffsetY + barSpacing, 10);
        shieldBarTransform = shieldBarObject.transform;
        shieldBarRenderer  = shieldBarObject.GetComponent<SpriteRenderer>();

        shieldBarBGObject    = CreateGradientBar("ShieldBarBG", new[] { shieldBarBGColor, shieldBarBGColor }, displayOffsetY + barSpacing, 9);
        shieldBarBGTransform = shieldBarBGObject.transform;

        hpNumberObject     = CreateNumberText("HP_Number",     Color.green, displayOffsetY,              out hpNumberText);
        shieldNumberObject = CreateNumberText("Shield_Number", Color.cyan,  displayOffsetY + barSpacing, out shieldNumberText);
    }

    private GameObject CreateNumberText(string objName, Color color, float offsetY, out TextMesh textMesh)
    {
        GameObject obj = new(objName);
        obj.transform.SetParent(transform);
        obj.transform.localPosition = new Vector3(barWidth / 2f + numberOffsetX + barOffsetX, offsetY, -0.1f);

        TextMesh tm = obj.AddComponent<TextMesh>();
        tm.fontSize  = fontSize;
        tm.fontStyle = FontStyle.Normal;
        tm.color     = color;
        tm.anchor        = TextAnchor.MiddleLeft;
        tm.alignment     = TextAlignment.Left;

        MeshRenderer mr = obj.GetComponent<MeshRenderer>();
        if (mr != null) mr.sortingOrder = 11;

        textMesh = tm;
        return obj;
    }

    // ─── 毎フレーム更新 ──────────────────────────────────────
    private void LateUpdate()
    {
        if (!initialized || sourceStats == null || hpBarObject == null) return;

        Vector3 basePos = transform.position;
        Vector3 ls      = transform.lossyScale;
        float   xSign   = ls.x < 0 ? -1f : 1f;

        const int texW = 256, texH = 1;

        float barStartX = (-barWidth / 2f + barOffsetX) * xSign;
        float numberX   = ( barWidth / 2f + numberOffsetX + barOffsetX) * xSign;
        float bgScaleX  = ls.x != 0 ? (barWidth * texH / texW) / ls.x : barWidth;
        float scaleY    = ls.y != 0 ? barHeight / ls.y : barHeight;

        // ── HP バー ─────────────────────────────────────────
        Vector3 hpPos = new(basePos.x + ls.x * barStartX, basePos.y + ls.y * displayOffsetY, basePos.z - 0.1f);
        hpBarObject.transform.SetPositionAndRotation(hpPos, Quaternion.identity);
        hpBarBGObject.transform.SetPositionAndRotation(hpPos, Quaternion.identity);

        float hpRatio  = sourceStats.MaxHP > 0 ? (float)sourceStats.HP / sourceStats.MaxHP : 0f;
        float hpScaleX = ls.x != 0 ? (barWidth * hpRatio * texH / texW) / ls.x : barWidth * hpRatio;
        hpBarTransform.localScale   = new Vector3(hpScaleX, scaleY, 1f);
        hpBarBGTransform.localScale = new Vector3(bgScaleX, scaleY, 1f);

        if (hpNumberText != null)
        {
            hpNumberText.text = $"{sourceStats.HP}";
            hpNumberObject.transform.SetPositionAndRotation(
                new Vector3(basePos.x + ls.x * numberX, basePos.y + ls.y * displayOffsetY, basePos.z),
                Quaternion.identity);
            hpNumberObject.transform.localScale = new Vector3(xSign, 1f, 1f);
        }

        // ── Shield バー ──────────────────────────────────────
        bool hasShield = sourceShield != null && sourceShield.IsEnabled;
        shieldBarObject.SetActive(hasShield);
        shieldBarBGObject.SetActive(hasShield);
        if (shieldNumberObject != null) shieldNumberObject.SetActive(hasShield);

        if (hasShield)
        {
            Vector3 shPos = new(basePos.x + ls.x * barStartX, basePos.y + ls.y * (displayOffsetY + barSpacing), basePos.z - 0.1f);
            shieldBarObject.transform.SetPositionAndRotation(shPos, Quaternion.identity);
            shieldBarBGObject.transform.SetPositionAndRotation(shPos, Quaternion.identity);

            float shieldRatio = sourceShield.IsBroken
                ? sourceShield.RecoveryProgress
                : (sourceShield.MaxShield > 0 ? (float)sourceShield.CurrentShield / sourceShield.MaxShield : 0f);

            float shScaleX = ls.x != 0 ? (barWidth * shieldRatio * texH / texW) / ls.x : barWidth * shieldRatio;
            shieldBarTransform.localScale   = new Vector3(shScaleX, scaleY, 1f);
            shieldBarBGTransform.localScale = new Vector3(bgScaleX, scaleY, 1f);

            Color shColor = shieldBarRenderer.color;
            shieldBarRenderer.color = new Color(shColor.r, shColor.g, shColor.b, sourceShield.IsBroken ? 0.05f : 1f);

            if (shieldNumberText != null)
            {
                shieldNumberText.text = sourceShield.IsBroken ? "---" : $"{sourceShield.CurrentShield}";
                shieldNumberObject.transform.SetPositionAndRotation(
                    new Vector3(basePos.x + ls.x * numberX, basePos.y + ls.y * (displayOffsetY + barSpacing), basePos.z),
                    Quaternion.identity);
                shieldNumberObject.transform.localScale = new Vector3(xSign, 1f, 1f);
            }
        }
    }

    // ─── ヘルパー ────────────────────────────────────────────
    private GameObject CreateGradientBar(string barName, Color[] colors, float offsetY, int sortOrder)
    {
        GameObject obj = new(barName);
        obj.transform.SetParent(transform);
        obj.transform.localPosition = new Vector3(-barWidth / 2f + barOffsetX, offsetY, -0.1f);

        Texture2D tex = CreateGradientTexture(colors);
        Sprite spr = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0f, 0.5f), tex.height);
        spr.hideFlags = HideFlags.DontSave;
        runtimeSprites.Add(spr);

        SpriteRenderer sr = obj.AddComponent<SpriteRenderer>();
        sr.sprite       = spr;
        sr.sortingOrder = sortOrder;

        Vector3 ls   = transform.lossyScale;
        float scaleX = ls.x != 0 ? (barWidth * tex.height / tex.width) / ls.x : barWidth;
        float scaleY = ls.y != 0 ? barHeight / ls.y : barHeight;
        obj.transform.localScale = new Vector3(scaleX, scaleY, 1f);

        return obj;
    }

    private Texture2D CreateGradientTexture(Color[] colors)
    {
        const int width = 256, height = 1;
        Texture2D tex = new(width, height) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };

        int segments = colors.Length - 1;
        for (int x = 0; x < width; x++)
        {
            float t       = (float)x / (width - 1);
            float scaledT = t * segments;
            int   idx     = Mathf.Clamp(Mathf.FloorToInt(scaledT), 0, segments - 1);
            tex.SetPixel(x, 0, Color.Lerp(colors[idx], colors[idx + 1], scaledT - idx));
        }
        tex.Apply();
        runtimeTextures.Add(tex);
        return tex;
    }

    private void OnDestroy()
    {
        foreach (var s in runtimeSprites)  if (s != null) Destroy(s);
        foreach (var t in runtimeTextures) if (t != null) Destroy(t);
    }
}
