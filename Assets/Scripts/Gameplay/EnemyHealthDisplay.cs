using UnityEngine;

/// <summary>
/// 敵のShield/HP表示（本番仕様）
/// - グラデーションバー + 右側に数値
/// - Shield（上）→ HP（下） → 敵オブジェクト
/// </summary>
[RequireComponent(typeof(EnemyStats))]
public class EnemyHealthDisplay : MonoBehaviour
{
    [Header("Shield & HP Bar")]
    [Tooltip("バーの幅（ワールド座標）")]
    [SerializeField] private float barWidth = 0.5f;
    [Tooltip("バーの高さ（ワールド座標）")]
    [SerializeField] private float barHeight = 0.1f;
    [Tooltip("シールドバーとHPバーの縦間隔")]
    [SerializeField] private float barSpacing = 0.15f;
    [Tooltip("バーのX方向オフセット（左にずらす場合は負の値）")]
    [SerializeField] private float barOffsetX = 0.0f;
    [Tooltip("数値テキストのX方向オフセット（バーの右端からの距離）")]
    [SerializeField] private float numberOffsetX = 0.1f;
    [Tooltip("バー表示のY方向オフセット（敵からの距離）")]
    [SerializeField] private float displayOffsetY = 0.6f;
    [Tooltip("数値テキストのフォントサイズ")]
    [SerializeField] private int fontSize = 60;

    private EnemyStats stats;
    private EnemyShield shield;

    private TextMesh hpNumberText;
    private TextMesh shieldNumberText;
    private GameObject shieldNumberObject;

    // Shield & HP Bars
    private GameObject shieldBarObject;
    private GameObject hpBarObject;
    private Transform shieldBarTransform;
    private Transform hpBarTransform;
    private SpriteRenderer shieldBarRenderer;
    private SpriteRenderer hpBarRenderer;

    private void Awake()
    {
        stats = GetComponent<EnemyStats>();
        shield = GetComponent<EnemyShield>();

        // パターンA: Shield（上）→ HP → 敵オブジェクト
        // ※全てのバーと数値は displayOffsetY を基準に配置

        // ===== HPバー（下） =====
        Vector3 hpBarPosition = new Vector3(-barWidth / 2f + barOffsetX, displayOffsetY, 0f);
        Color[] hpColors = new Color[] { new Color(0.0f, 0.5f, 0.0f), new Color(0.0f, 0.6f, 0.0f), new Color(0.0f, 0.8f, 0.0f), Color.green };
        hpBarObject = CreateGradientBar("HPBar", hpColors, hpBarPosition);
        hpBarTransform = hpBarObject.transform;
        hpBarRenderer = hpBarObject.GetComponent<SpriteRenderer>();

        // ===== HP数値テキスト（バーの右側） =====
        GameObject hpNumberObject = new GameObject("HP_Number");
        hpNumberObject.transform.SetParent(transform);
        hpNumberObject.transform.localPosition = new Vector3(barWidth / 2f + numberOffsetX + barOffsetX, displayOffsetY, 0f);

        hpNumberText = hpNumberObject.AddComponent<TextMesh>();
        hpNumberText.anchor = TextAnchor.MiddleLeft;
        hpNumberText.alignment = TextAlignment.Left;
        hpNumberText.fontSize = fontSize;
        hpNumberText.characterSize = 0.05f;
        hpNumberText.color = Color.green;
        hpNumberText.text = "";

        // ===== Shieldバー（上） =====
        Vector3 shieldBarPosition = new Vector3(-barWidth / 2f + barOffsetX, displayOffsetY + barSpacing, 0f);
        Color[] shieldColors = new Color[] { new Color(0.0f, 0.5f, 0.5f), new Color(0.0f, 0.6f, 0.6f), new Color(0.0f, 0.8f, 0.8f), Color.cyan };
        shieldBarObject = CreateGradientBar("ShieldBar", shieldColors, shieldBarPosition);
        shieldBarTransform = shieldBarObject.transform;
        shieldBarRenderer = shieldBarObject.GetComponent<SpriteRenderer>();

        // ===== Shield数値テキスト（バーの右側） =====
        shieldNumberObject = new GameObject("Shield_Number");
        shieldNumberObject.transform.SetParent(transform);
        shieldNumberObject.transform.localPosition = new Vector3(barWidth / 2f + numberOffsetX + barOffsetX, displayOffsetY + barSpacing, 0f);

        shieldNumberText = shieldNumberObject.AddComponent<TextMesh>();
        shieldNumberText.anchor = TextAnchor.MiddleLeft;
        shieldNumberText.alignment = TextAlignment.Left;
        shieldNumberText.fontSize = fontSize;
        shieldNumberText.characterSize = 0.05f;
        shieldNumberText.color = Color.cyan;
        shieldNumberText.text = "";
    }

    private void LateUpdate()
    {
        if (stats == null || hpNumberText == null) return;

        // 親のワールドスケールを取得（ワールドスケール固定用）
        Vector3 parentLossyScale = transform.lossyScale;

        // テクスチャのサイズ（CreateGradientTextureと同じ）
        const int textureWidth = 256;
        const int textureHeight = 1;

        // ===== HP数値とバー更新 =====
        hpNumberText.text = $"{stats.HP}";

        if (hpBarTransform != null && hpBarRenderer != null)
        {
            float hpRatio = stats.MaxHP > 0 ? (float)stats.HP / stats.MaxHP : 0f;
            // スプライトのサイズ: 幅=textureWidth/textureHeight, 高さ=1
            float scaleX = parentLossyScale.x != 0 ? (barWidth * hpRatio * textureHeight / textureWidth) / parentLossyScale.x : barWidth * hpRatio;
            float scaleY = parentLossyScale.y != 0 ? barHeight / parentLossyScale.y : barHeight;
            hpBarTransform.localScale = new Vector3(scaleX, scaleY, 1f);
        }

        // ===== Shield数値とバー更新 =====
        if (shield != null && shield.IsEnabled && shieldNumberText != null)
        {
            if (shield.IsBroken)
            {
                // 破壊中：回復進行度に応じて徐々に表示
                float progress = shield.RecoveryProgress;

                // 数値は非表示（0を表示しない）
                shieldNumberText.text = "";

                // バーは回復進行度に応じて徐々に表示
                if (shieldBarTransform != null && shieldBarRenderer != null)
                {
                    float scaleX = parentLossyScale.x != 0 ? (barWidth * progress * textureHeight / textureWidth) / parentLossyScale.x : barWidth * progress;
                    float scaleY = parentLossyScale.y != 0 ? barHeight / parentLossyScale.y : barHeight;
                    shieldBarTransform.localScale = new Vector3(scaleX, scaleY, 1f);

                    // 透明度は0.05固定
                    Color barColor = shieldBarRenderer.color;
                    shieldBarRenderer.color = new Color(barColor.r, barColor.g, barColor.b, 0.05f);
                    shieldBarObject.SetActive(true);
                }

                shieldNumberObject.SetActive(true);
            }
            else
            {
                // 通常時：CurrentShieldに基づく表示
                shieldNumberText.text = $"{shield.CurrentShield}";
                shieldNumberObject.SetActive(true);

                if (shieldBarTransform != null && shieldBarRenderer != null)
                {
                    float shieldRatio = shield.MaxShield > 0 ? (float)shield.CurrentShield / shield.MaxShield : 0f;
                    float scaleX = parentLossyScale.x != 0 ? (barWidth * shieldRatio * textureHeight / textureWidth) / parentLossyScale.x : barWidth * shieldRatio;
                    float scaleY = parentLossyScale.y != 0 ? barHeight / parentLossyScale.y : barHeight;
                    shieldBarTransform.localScale = new Vector3(scaleX, scaleY, 1f);

                    // 透明度は完全不透明
                    Color barColor = shieldBarRenderer.color;
                    shieldBarRenderer.color = new Color(barColor.r, barColor.g, barColor.b, 1f);
                    shieldBarObject.SetActive(true);
                }
            }
        }
        else if (shieldNumberObject != null && shieldBarObject != null)
        {
            // Shieldオフの場合は非表示
            shieldNumberObject.SetActive(false);
            shieldBarObject.SetActive(false);
        }
    }

    /// <summary>
    /// グラデーションバーを作成する（複数色対応）
    /// </summary>
    /// <param name="name">GameObjectの名前</param>
    /// <param name="colors">グラデーションの色配列（左から右へ均等配置）</param>
    /// <param name="position">バーの位置</param>
    private GameObject CreateGradientBar(string name, Color[] colors, Vector3 position)
    {
        GameObject barObj = new GameObject(name);
        barObj.transform.SetParent(transform);
        barObj.transform.localPosition = new Vector3(position.x, position.y, -0.1f); // Z座標を手前に

        // グラデーションテクスチャを作成（横方向グラデーション）
        Texture2D texture = CreateGradientTexture(colors);

        // Pivot: 左中央（左端固定でゲージが減る）
        // pixelsPerUnit = texture.height に設定して、スプライトの高さを1ユニットにする
        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0f, 0.5f), texture.height);

        SpriteRenderer renderer = barObj.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = 10; // 前面に表示

        // 親のワールドスケールを取得して、その影響を打ち消す
        Vector3 parentLossyScale = transform.lossyScale;
        // スプライトのサイズ: 幅=texture.width/texture.height, 高さ=1
        float scaleX = parentLossyScale.x != 0 ? (barWidth * texture.height / texture.width) / parentLossyScale.x : barWidth;
        float scaleY = parentLossyScale.y != 0 ? barHeight / parentLossyScale.y : barHeight;
        barObj.transform.localScale = new Vector3(scaleX, scaleY, 1f);

        return barObj;
    }

    /// <summary>
    /// 横方向グラデーションテクスチャを生成する（複数色対応）
    /// </summary>
    /// <param name="colors">グラデーションの色配列（左から右へ均等配置）</param>
    private Texture2D CreateGradientTexture(Color[] colors)
    {
        if (colors == null || colors.Length < 2)
        {
            // フォールバック：白→黒
            colors = new Color[] { Color.white, Color.black };
        }

        int width = 256;  // テクスチャの幅
        int height = 1;   // テクスチャの高さ（1ピクセルで十分）

        Texture2D texture = new Texture2D(width, height);
        texture.wrapMode = TextureWrapMode.Clamp;

        for (int x = 0; x < width; x++)
        {
            float t = (float)x / (width - 1); // 0.0～1.0
            Color color = GetGradientColor(colors, t);
            texture.SetPixel(x, 0, color);
        }

        texture.Apply();
        return texture;
    }

    /// <summary>
    /// 複数色のグラデーションから指定位置の色を取得
    /// </summary>
    /// <param name="colors">色配列</param>
    /// <param name="t">位置（0.0～1.0）</param>
    private Color GetGradientColor(Color[] colors, float t)
    {
        if (colors.Length == 1) return colors[0];

        // 色と色の間の区間数
        int segments = colors.Length - 1;

        // 現在位置がどの区間にあるか計算
        float scaledT = t * segments;
        int index = Mathf.FloorToInt(scaledT);

        // 最後の色を超えないようにクランプ
        if (index >= segments)
        {
            return colors[colors.Length - 1];
        }

        // 区間内での相対位置（0.0～1.0）
        float localT = scaledT - index;

        // 2色間で補間
        return Color.Lerp(colors[index], colors[index + 1], localT);
    }
}
