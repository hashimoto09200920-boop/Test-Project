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
    [SerializeField] private float numberOffsetX = 3f;
    [Tooltip("バー表示のY方向オフセット（敵からの距離）")]
    [SerializeField] private float displayOffsetY = 0.6f;
    [Tooltip("数値テキストのフォントサイズ")]
    [SerializeField] private int fontSize = 60;

    [Header("Debuff Icons (B4/B7/B8)")]
    [Tooltip("B4 スロウデバフアイコンスプライト（未設定時は非表示）")]
    [SerializeField] private Sprite slowDebuffSprite;
    [Tooltip("B7 シールド破壊ダメージブーストアイコンスプライト（未設定時は非表示）")]
    [SerializeField] private Sprite b7ShieldBreakSprite;
    [Tooltip("B8 シールド回復停止アイコンスプライト（未設定時は非表示）")]
    [SerializeField] private Sprite b8ShieldStopSprite;
    [Tooltip("アイコンのサイズ（ワールド座標）")]
    [SerializeField] private float debuffIconSize = 0.15f;
    [Tooltip("HPバー上端中央を基準とした B4アイコン（左端）のオフセット（X正=右、Y正=上）")]
    [SerializeField] private Vector2 debuffIconOffset = new Vector2(-0.18f, 0.08f);
    [Tooltip("アイコン間の横間隔（ワールド座標）")]
    [SerializeField] private float debuffIconSpacing = 0.18f;
    [Tooltip("持続時間テキストのフォントサイズ")]
    [SerializeField] private int debuffDurationFontSize = 40;
    [Tooltip("持続時間テキストのアイコン中央からのY方向オフセット（正=上）")]
    [SerializeField] private float debuffDurationTextOffsetY = 0.1f;

    [Header("Background Bars (Max HP/Shield)")]
    [Tooltip("HPバー下地の色（最大値表示）")]
    [SerializeField] private Color hpBarBGColor = new Color(0f, 0.2f, 0f, 1f);
    [Tooltip("Shieldバー下地の色（最大値表示）")]
    [SerializeField] private Color shieldBarBGColor = new Color(0f, 0.2f, 0.2f, 1f);

    private EnemyStats stats;
    private EnemyShield shield;
    private EnemyMover enemyMover;

    // SlimeEnemy など、スケールが動的に変化する敵のために
    // バー・数値のワールドサイズをスプライトと連動させる倍率
    private float displayScaleMultiplier = 1f;

    public void SetDisplayScaleMultiplier(float multiplier)
    {
        displayScaleMultiplier = Mathf.Max(0.01f, multiplier);
    }

    private TextMesh hpNumberText;
    private TextMesh shieldNumberText;
    private GameObject shieldNumberObject;

    // Debuff Icons (B4/B7/B8)
    private readonly GameObject[] debuffIconObjects = new GameObject[3];
    private readonly SpriteRenderer[] debuffIconRenderers = new SpriteRenderer[3];
    private readonly TextMesh[] debuffDurationTexts = new TextMesh[3];
    private readonly GameObject[] debuffDurationTextObjects = new GameObject[3];

    // Shield & HP Bars
    private GameObject shieldBarObject;
    private GameObject hpBarObject;
    private Transform shieldBarTransform;
    private Transform hpBarTransform;
    private SpriteRenderer shieldBarRenderer;
    private SpriteRenderer hpBarRenderer;

    // Background Bars (max value display)
    private GameObject hpBarBGObject;
    private GameObject shieldBarBGObject;
    private Transform hpBarBGTransform;
    private Transform shieldBarBGTransform;

    private void Awake()
    {
        stats = GetComponent<EnemyStats>();
        shield = GetComponent<EnemyShield>();
        enemyMover = GetComponentInParent<EnemyMover>();

        // パターンA: Shield（上）→ HP → 敵オブジェクト
        // ※全てのバーと数値は displayOffsetY を基準に配置

        // ===== HPバー（下） =====
        Vector3 hpBarPosition = new Vector3(-barWidth / 2f + barOffsetX, displayOffsetY, 0f);
        Color[] hpColors = new Color[] { new Color(0.0f, 0.5f, 0.0f), new Color(0.0f, 0.6f, 0.0f), new Color(0.0f, 0.8f, 0.0f), Color.green };
        hpBarObject = CreateGradientBar("HPBar", hpColors, hpBarPosition);
        hpBarTransform = hpBarObject.transform;
        hpBarRenderer = hpBarObject.GetComponent<SpriteRenderer>();

        // ===== HP下地バー（最大値表示） =====
        hpBarBGObject = CreateGradientBar("HPBarBG", new Color[] { hpBarBGColor, hpBarBGColor }, hpBarPosition);
        hpBarBGObject.GetComponent<SpriteRenderer>().sortingOrder = 9;
        hpBarBGTransform = hpBarBGObject.transform;

        // ===== HP数値テキスト（バーの右側） =====
        GameObject hpNumberObject = new GameObject("HP_Number");
        hpNumberObject.transform.SetParent(transform);
        hpNumberObject.transform.localPosition = new Vector3(barWidth / 2f + numberOffsetX + barOffsetX, displayOffsetY, 0f);

        hpNumberText = hpNumberObject.AddComponent<TextMesh>();
        hpNumberText.anchor = TextAnchor.MiddleRight;
        hpNumberText.alignment = TextAlignment.Right;
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

        // ===== Shield下地バー（最大値表示） =====
        shieldBarBGObject = CreateGradientBar("ShieldBarBG", new Color[] { shieldBarBGColor, shieldBarBGColor }, shieldBarPosition);
        shieldBarBGObject.GetComponent<SpriteRenderer>().sortingOrder = 9;
        shieldBarBGTransform = shieldBarBGObject.transform;

        // ===== Shield数値テキスト（バーの右側） =====
        shieldNumberObject = new GameObject("Shield_Number");
        shieldNumberObject.transform.SetParent(transform);
        shieldNumberObject.transform.localPosition = new Vector3(barWidth / 2f + numberOffsetX + barOffsetX, displayOffsetY + barSpacing, 0f);

        shieldNumberText = shieldNumberObject.AddComponent<TextMesh>();
        shieldNumberText.anchor = TextAnchor.MiddleRight;
        shieldNumberText.alignment = TextAlignment.Right;
        shieldNumberText.fontSize = fontSize;
        shieldNumberText.characterSize = 0.05f;
        shieldNumberText.color = Color.cyan;
        shieldNumberText.text = "";

        // ===== デバフアイコン（B4/B7/B8） =====
        Sprite[] debuffSprites = { slowDebuffSprite, b7ShieldBreakSprite, b8ShieldStopSprite };
        string[] debuffNames = { "DebuffIcon_B4", "DebuffIcon_B7", "DebuffIcon_B8" };
        for (int i = 0; i < 3; i++)
        {
            GameObject iconObj = new GameObject(debuffNames[i]);
            iconObj.transform.SetParent(transform);
            iconObj.transform.localPosition = Vector3.zero;
            SpriteRenderer sr = iconObj.AddComponent<SpriteRenderer>();
            sr.sprite = debuffSprites[i];
            sr.sortingOrder = 12;
            iconObj.SetActive(false);
            debuffIconObjects[i] = iconObj;
            debuffIconRenderers[i] = sr;

            GameObject textObj = new GameObject(debuffNames[i] + "_Duration");
            textObj.transform.SetParent(transform);
            textObj.transform.localPosition = Vector3.zero;
            TextMesh tm = textObj.AddComponent<TextMesh>();
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.fontSize = debuffDurationFontSize;
            tm.characterSize = 0.05f;
            tm.color = Color.white;
            tm.text = "";
            MeshRenderer tmr = textObj.GetComponent<MeshRenderer>();
            if (tmr != null) tmr.sortingOrder = 13;
            textObj.SetActive(false);
            debuffDurationTextObjects[i] = textObj;
            debuffDurationTexts[i] = tm;
        }
    }

    private void LateUpdate()
    {
        if (stats == null || hpNumberText == null) return;

        // ワールド座標で位置をセット（回転の影響だけを除外。スケールは乗算して反映）
        // world = enemy.pos + lossyScale * localOffset （rotation なし）
        // これにより SpriteSwimAnimation の回転起因の軌道ずれが消え、元のレイアウトが保たれる
        Vector3 basePos = transform.position;
        Vector3 ls = transform.lossyScale;
        float xSign = ls.x < 0 ? -1f : 1f;

        // displayScaleMultiplier でバー・数値のワールドサイズをスプライトに連動させる
        float effBarWidth  = barWidth  * displayScaleMultiplier;
        float effBarHeight = barHeight * displayScaleMultiplier;

        float barStartX = (-effBarWidth / 2f + barOffsetX) * xSign;
        float numberX   = (effBarWidth / 2f + numberOffsetX + barOffsetX) * xSign;
        if (hpBarObject != null)
        {
            hpBarObject.transform.position = new Vector3(basePos.x + ls.x * barStartX, basePos.y + ls.y * displayOffsetY, basePos.z - 0.1f);
            hpBarObject.transform.rotation = Quaternion.identity;
        }
        if (hpNumberText != null)
        {
            hpNumberText.transform.position = new Vector3(basePos.x + ls.x * numberX, basePos.y + ls.y * displayOffsetY, basePos.z);
            hpNumberText.transform.rotation = Quaternion.identity;
            hpNumberText.transform.localScale = new Vector3(xSign, 1f, 1f);
        }
        if (shieldBarObject != null)
        {
            shieldBarObject.transform.position = new Vector3(basePos.x + ls.x * barStartX, basePos.y + ls.y * (displayOffsetY + barSpacing), basePos.z - 0.1f);
            shieldBarObject.transform.rotation = Quaternion.identity;
        }
        if (shieldNumberObject != null)
        {
            shieldNumberObject.transform.position = new Vector3(basePos.x + ls.x * numberX, basePos.y + ls.y * (displayOffsetY + barSpacing), basePos.z);
            shieldNumberObject.transform.rotation = Quaternion.identity;
            shieldNumberObject.transform.localScale = new Vector3(xSign, 1f, 1f);
        }

        // 親のワールドスケールを取得（バーのビジュアルサイズ補正用）
        Vector3 parentLossyScale = transform.lossyScale;

        // テクスチャのサイズ（CreateGradientTextureと同じ）
        const int textureWidth = 256;
        const int textureHeight = 1;

        // ===== HP数値とバー更新 =====
        hpNumberText.text = $"{stats.HP}";

        if (hpBarTransform != null && hpBarRenderer != null)
        {
            float hpRatio = stats.MaxHP > 0 ? (float)stats.HP / stats.MaxHP : 0f;
            float scaleX = parentLossyScale.x != 0 ? (effBarWidth * hpRatio * textureHeight / textureWidth) / parentLossyScale.x : effBarWidth * hpRatio;
            float scaleY = parentLossyScale.y != 0 ? effBarHeight / parentLossyScale.y : effBarHeight;
            hpBarTransform.localScale = new Vector3(scaleX, scaleY, 1f);
        }

        // ===== HP下地バー（常に最大幅） =====
        if (hpBarBGObject != null)
        {
            hpBarBGObject.transform.position = new Vector3(basePos.x + ls.x * barStartX, basePos.y + ls.y * displayOffsetY, basePos.z - 0.1f);
            hpBarBGObject.transform.rotation = Quaternion.identity;
            float bgScaleX = parentLossyScale.x != 0 ? (effBarWidth * textureHeight / textureWidth) / parentLossyScale.x : effBarWidth;
            float bgScaleY = parentLossyScale.y != 0 ? effBarHeight / parentLossyScale.y : effBarHeight;
            hpBarBGTransform.localScale = new Vector3(bgScaleX, bgScaleY, 1f);
        }

        // ===== デバフアイコン更新（B4/B7/B8、HPバー上に横並び） =====
        {
            bool b4Active = enemyMover != null && enemyMover.IsSlowed;
            float b4Time = b4Active ? enemyMover.SlowTimeRemaining : 0f;

            bool b7Active = Game.Skills.SkillManager.Instance != null && Game.Skills.SkillManager.Instance.IsShieldBreakBoostActive;
            float b7Time = b7Active ? Game.Skills.SkillManager.Instance.ShieldBreakBoostTimeRemaining : 0f;

            bool b8Active = shield != null && shield.IsEnabled && shield.IsRecoveryStopActive;
            float b8Time = b8Active ? shield.RecoveryStopTimeRemaining : 0f;

            bool[] actives = { b4Active, b7Active, b8Active };
            float[] times = { b4Time, b7Time, b8Time };

            float absLsX = Mathf.Max(0.001f, Mathf.Abs(ls.x));
            float absLsY = Mathf.Max(0.001f, Mathf.Abs(ls.y));
            // アイコンサイズ: X/Y それぞれ親スケールを打ち消してワールド単位で debuffIconSize になるよう補正
            float iconScaleX = debuffIconSize / absLsX;
            float iconScaleY = debuffIconSize / absLsY;
            // アイコン基準Y: バーに追従する部分(ls.y倍)＋ワールド固定オフセット(倍率なし)
            float iconWorldBaseY = basePos.y + ls.y * (displayOffsetY + barHeight * 0.5f) + debuffIconOffset.y;

            for (int i = 0; i < 3; i++)
            {
                if (debuffIconObjects[i] == null) continue;
                bool hasSprite = debuffIconRenderers[i] != null && debuffIconRenderers[i].sprite != null;
                bool show = actives[i] && hasSprite;

                debuffIconObjects[i].SetActive(show);
                if (debuffDurationTextObjects[i] != null)
                    debuffDurationTextObjects[i].SetActive(show);

                if (show)
                {
                    // X: バーオフセットはls.x倍(バーに追従)、アイコン間隔はワールド固定
                    float iconWorldX = basePos.x + ls.x * barOffsetX * xSign + (debuffIconOffset.x + i * debuffIconSpacing) * xSign;
                    Vector3 iconPos = new Vector3(iconWorldX, iconWorldBaseY, basePos.z - 0.05f);

                    debuffIconObjects[i].transform.position = iconPos;
                    debuffIconObjects[i].transform.rotation = Quaternion.identity;
                    debuffIconObjects[i].transform.localScale = new Vector3(iconScaleX * xSign, iconScaleY, 1f);

                    if (debuffDurationTextObjects[i] != null)
                    {
                        debuffDurationTextObjects[i].transform.position = new Vector3(
                            iconPos.x,
                            iconPos.y + debuffDurationTextOffsetY,
                            basePos.z - 0.06f);
                        debuffDurationTextObjects[i].transform.rotation = Quaternion.identity;
                        debuffDurationTextObjects[i].transform.localScale = new Vector3(xSign / absLsX, 1f / absLsY, 1f);
                        debuffDurationTexts[i].text = $"{Mathf.CeilToInt(times[i])}s";
                    }
                }
            }
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
                    float scaleX = parentLossyScale.x != 0 ? (effBarWidth * progress * textureHeight / textureWidth) / parentLossyScale.x : effBarWidth * progress;
                    float scaleY = parentLossyScale.y != 0 ? effBarHeight / parentLossyScale.y : effBarHeight;
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
                    float scaleX = parentLossyScale.x != 0 ? (effBarWidth * shieldRatio * textureHeight / textureWidth) / parentLossyScale.x : effBarWidth * shieldRatio;
                    float scaleY = parentLossyScale.y != 0 ? effBarHeight / parentLossyScale.y : effBarHeight;
                    shieldBarTransform.localScale = new Vector3(scaleX, scaleY, 1f);

                    // 透明度は完全不透明
                    Color barColor = shieldBarRenderer.color;
                    shieldBarRenderer.color = new Color(barColor.r, barColor.g, barColor.b, 1f);
                    shieldBarObject.SetActive(true);
                }
            }

            // Shield下地バー（シールド有効時は常に最大幅で表示）
            if (shieldBarBGObject != null)
            {
                shieldBarBGObject.transform.position = new Vector3(basePos.x + ls.x * barStartX, basePos.y + ls.y * (displayOffsetY + barSpacing), basePos.z - 0.1f);
                shieldBarBGObject.transform.rotation = Quaternion.identity;
                float bgScaleX = parentLossyScale.x != 0 ? (effBarWidth * textureHeight / textureWidth) / parentLossyScale.x : effBarWidth;
                float bgScaleY = parentLossyScale.y != 0 ? effBarHeight / parentLossyScale.y : effBarHeight;
                shieldBarBGTransform.localScale = new Vector3(bgScaleX, bgScaleY, 1f);
                shieldBarBGObject.SetActive(true);
            }
        }
        else if (shieldNumberObject != null && shieldBarObject != null)
        {
            // Shieldオフの場合は非表示
            shieldNumberObject.SetActive(false);
            shieldBarObject.SetActive(false);
            if (shieldBarBGObject != null) shieldBarBGObject.SetActive(false);
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
