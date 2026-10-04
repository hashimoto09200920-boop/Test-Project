using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// NeonDancer専用のShield/HP表示（EnemyHealthDisplayのフォーク。原本は全エネミー共通のため改修しない）。
/// 原本との違い：
/// - HPの数値・バー比率を「現在フェーズ内のHP / フェーズ最大HP」で描く（NeonDancerController.DisplayPhaseHp/DisplayPhaseMaxHp）。
///   前半は0まで減り、後半移行演出で0→満タンへ伸びて見える。
/// - Pin To Spawn Position=ONなら、Dancerが左右に歩いてもバーは出現位置に固定する。
/// シールドバー・ゴーストバー・B4/B7/B8デバフアイコンは原本と同一処理。
/// </summary>
[RequireComponent(typeof(EnemyStats))]
public class NeonDancerHealthDisplay : MonoBehaviour
{
    [Header("NeonDancer")]
    [Tooltip("ON: バーを出現位置に固定する（Dancerの左右移動でバーが揺れない）")]
    [SerializeField] private bool pinToSpawnPosition = true;

    private NeonDancerController neonDancer;

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
    [Tooltip("数値テキストのフォント。未指定だとPC/モバイルで異なるフォールバックフォントが使われ、文字幅の違いから表示位置がズレることがあるため、必ず指定する")]
    [SerializeField] private Font numberFont;

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

    [Header("Auto Bar Size")]
    [Tooltip("ONにするとスプライト幅に合わせてバー幅を自動調整する")]
    [SerializeField] private bool autoBarWidth = false;
    [Tooltip("自動調整の基準SpriteRenderer（空欄なら自動検索）")]
    [SerializeField] private SpriteRenderer enemySpriteRenderer;
    [Tooltip("スプライト幅に対するバー幅の比率（1.0=同じ幅）")]
    [SerializeField] private float barWidthRatio = 1.0f;
    [Tooltip("ONにするとバー幅に対してbarHeightとbarSpacingを同時に自動調整する")]
    [SerializeField] private bool autoBarHeight = false;
    [Tooltip("バー幅に対するバー高さの比率")]
    [SerializeField] private float barHeightRatio = 0.1f;
    [Tooltip("barSpacing = barHeight × この値（1より大きい値でバーが重ならない）")]
    [SerializeField] private float barSpacingMultiplier = 1.5f;
    [Tooltip("ONにするとbarHeightに合わせてHP/Shield数値テキストサイズを自動調整する")]
    [SerializeField] private bool autoTextSize = false;
    [Tooltip("barHeightに対するテキストcharacterSizeの比率（0.5=barHeightの半分）")]
    [SerializeField] private float textHeightRatio = 0.5f;

    [Header("Background Bars (Max HP/Shield)")]
    [Tooltip("HPバー下地の色（最大値表示）")]
    [SerializeField] private Color hpBarBGColor = new Color(0f, 0.2f, 0f, 1f);
    [Tooltip("Shieldバー下地の色（最大値表示）")]
    [SerializeField] private Color shieldBarBGColor = new Color(0f, 0.2f, 0.2f, 1f);

    [Header("Ghost Bar (Damage Trail)")]
    [Tooltip("ONで、被弾時にHP/Shieldの本体バーは瞬時に減り、その後ろのゴーストバーが減った分をゆっくり追いかける演出を有効にする（HP・Shield共通）。\n" +
             "色・追従速度は個別設定ではなく、全エネミー共通のAssets/Resources/GameData/EnemyHealthGhostBarSettings.assetで一括管理する。")]
    [SerializeField] private bool useGhostBar = true;

    // ★色・追従速度は個別Prefabごとに持たず、全エネミー共通の1つのアセットから読む
    //   （EnemyHealthGhostBarSettings.asset）。これにより39体のPrefabを1体ずつ開かずに
    //   1箇所の調整だけで全エネミーへ一括反映できる。
    private static EnemyHealthGhostBarSettings s_ghostSettings;
    private static bool s_ghostSettingsLoadAttempted;
    private static EnemyHealthGhostBarSettings GhostSettings
    {
        get
        {
            if (!s_ghostSettingsLoadAttempted)
            {
                s_ghostSettingsLoadAttempted = true;
                s_ghostSettings = Resources.Load<EnemyHealthGhostBarSettings>("GameData/EnemyHealthGhostBarSettings");
                if (s_ghostSettings == null)
                    Debug.LogWarning("[NeonDancerHealthDisplay] Assets/Resources/GameData/EnemyHealthGhostBarSettings.asset が見つかりません。ゴーストバーはデフォルト値で動作します。");
            }
            return s_ghostSettings;
        }
    }

    [Header("Editor Preview (Scene View)")]
    [Tooltip("Play前Scene View可視化用: ShieldはEnemyDataから読み込む")]
    [SerializeField] private EnemyData enemyData;

    private EnemyStats stats;
    // ★毎回EnemyStats経由で解決する（フィールドにキャッシュしない）。TsukuyomiControllerがOnEnable後、
    //   数フレーム遅れてHPプールの共有リンクを結ぶため、Awake()時点で一度だけGetComponentしてキャッシュすると
    //   リンク前の（共有されていない）自分自身のEnemyShieldを掴んだまま更新されなくなる
    private EnemyShield shield => stats != null ? stats.GetEffectiveShield() : GetComponent<EnemyShield>();
    private EnemyMover enemyMover;

    // SlimeEnemy など、スケールが動的に変化する敵のために
    // バー・数値のワールドサイズをスプライトと連動させる倍率
    private float displayScaleMultiplier = 1f;

    private bool    _hasFixedBasePosition;
    private Vector3 _fixedBasePosition;

    public void SetDisplayScaleMultiplier(float multiplier)
    {
        displayScaleMultiplier = Mathf.Max(0.01f, multiplier);
    }

    /// <summary>HP/Shieldバーの基準位置をワールド座標で固定する（Golem等、ROOT位置がアニメーションで動く敵用）</summary>
    public void SetFixedBasePosition(Vector3 worldPos)
    {
        _fixedBasePosition    = worldPos;
        _hasFixedBasePosition = true;
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

    private bool introHidden;

    /// <summary>
    /// Area10 Final Stageの登場演出中（NeonDancerが待機中）はHPバー・シールドバー・デバフアイコンを全て隠す。
    /// falseに戻すと通常表示に戻る
    /// </summary>
    public void SetIntroHidden(bool hidden)
    {
        if (introHidden == hidden) return;
        introHidden = hidden;
        if (hidden)
        {
            SetBarsVisible(false);
            HideDebuffIcons();
        }
        else
        {
            SetBarsVisible(true);
            if (hpGhostBarObject != null) hpGhostBarObject.SetActive(useGhostBar);
            if (shieldGhostBarObject != null) shieldGhostBarObject.SetActive(useGhostBar);
        }
    }

    private void HideDebuffIcons()
    {
        for (int i = 0; i < debuffIconObjects.Length; i++)
        {
            if (debuffIconObjects[i] != null) debuffIconObjects[i].SetActive(false);
            if (debuffDurationTextObjects[i] != null) debuffDurationTextObjects[i].SetActive(false);
        }
    }

    public void SetBarsVisible(bool visible)
    {
        hpBarObject?.SetActive(visible);
        hpBarBGObject?.SetActive(visible);
        hpGhostBarObject?.SetActive(visible);
        hpNumberObject?.SetActive(visible);
        if (shieldBarObject      != null) shieldBarObject.SetActive(visible);
        if (shieldBarBGObject    != null) shieldBarBGObject.SetActive(visible);
        if (shieldGhostBarObject != null) shieldGhostBarObject.SetActive(visible);
        if (shieldNumberObject   != null) shieldNumberObject.SetActive(visible);
    }

    private TextMesh hpNumberText;
    private TextMesh shieldNumberText;
    private GameObject hpNumberObject;
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

    // HP/Shield Ghost Bar (damage trail)
    private GameObject hpGhostBarObject;
    private Transform hpGhostBarTransform;
    private SpriteRenderer hpGhostBarRenderer;
    private float hpGhostRatio = -1f; // -1 = 未初期化（初回LateUpdateで現在値に合わせる）
    private GameObject shieldGhostBarObject;
    private Transform shieldGhostBarTransform;
    private SpriteRenderer shieldGhostBarRenderer;
    private float shieldGhostRatio = -1f; // -1 = 未初期化（初回LateUpdateで現在値に合わせる）

    // ★フォント未指定だとPC/モバイルで異なるフォールバックフォントが使われ、文字幅の違いから
    //   数値の表示位置(右寄せ基準)がプラットフォームごとにズレる。必ず同じフォントを明示的に使わせる。
    private void ApplyNumberFont(GameObject textObject, TextMesh textMesh)
    {
        if (numberFont == null) return;
        textMesh.font = numberFont;
        var renderer = textObject.GetComponent<MeshRenderer>();
        if (renderer != null) renderer.sharedMaterial = numberFont.material;
    }

    private void Start()
    {
        if (pinToSpawnPosition) SetFixedBasePosition(transform.position);
    }

    private void Awake()
    {
        stats = GetComponent<EnemyStats>();
        neonDancer = GetComponent<NeonDancerController>();
        enemyMover = GetComponentInParent<EnemyMover>();

        // autoBarWidth用: Inspector未指定なら自動検索（バー生成前に取得する）
        if (autoBarWidth && enemySpriteRenderer == null)
            enemySpriteRenderer = GetComponent<SpriteRenderer>()
                ?? GetComponentInChildren<SpriteRenderer>();

        // パターンA: Shield（上）→ HP → 敵オブジェクト
        // ※全てのバーと数値は displayOffsetY を基準に配置

        // ===== HPバー（下） =====
        // ★配色は全エネミー完全共通のハードコード値のため、テクスチャはプロセス全体で1回だけ
        //   生成して使い回す（39体分の個別生成コストを無くす）。
        Vector3 hpBarPosition = new Vector3(-barWidth / 2f + barOffsetX, displayOffsetY, 0f);
        hpBarObject = CreateBarVisual("HPBar", GetSharedHpBarSprite(), hpBarPosition, 10);
        hpBarTransform = hpBarObject.transform;
        hpBarRenderer = hpBarObject.GetComponent<SpriteRenderer>();

        // ===== HP下地バー（最大値表示） =====
        // ★色はhpBarBGColor（Inspectorで個別設定可能）だが、全39体が同じデフォルト値のため
        //   色ごとにキャッシュする方式にし、同じ色を使う限り1枚を使い回す。
        hpBarBGObject = CreateBarVisual("HPBarBG", GetSharedFlatColorSprite(hpBarBGColor), hpBarPosition, 8);
        hpBarBGTransform = hpBarBGObject.transform;

        // ===== HPゴーストバー（被弾直後、減った分をゆっくり追いかける演出。下地の前・本体の後ろに配置） =====
        // ★色はテクスチャに焼き込まず白一色のテクスチャにし、SpriteRenderer.colorで着色する。
        //   これによりEnemyHealthGhostBarSettings.assetの色をPlay中でも毎フレーム反映でき、
        //   Prefabごとにテクスチャを焼き直す必要が無い（全エネミー共通設定を即座に反映するため）。
        //   さらにテクスチャ自体も全エネミー共通の1枚を使い回し、個別生成コストを無くしている。
        hpGhostBarObject = CreateBarVisual("HPGhostBar", GetSharedFlatColorSprite(Color.white), hpBarPosition, 9);
        hpGhostBarObject.SetActive(useGhostBar);
        hpGhostBarTransform = hpGhostBarObject.transform;
        hpGhostBarRenderer = hpGhostBarObject.GetComponent<SpriteRenderer>();

        // ===== HP数値テキスト（バーの右側） =====
        hpNumberObject = new GameObject("HP_Number");
        hpNumberObject.transform.SetParent(transform);
        hpNumberObject.transform.localPosition = new Vector3(barWidth / 2f + numberOffsetX + barOffsetX, displayOffsetY, 0f);

        hpNumberText = hpNumberObject.AddComponent<TextMesh>();
        hpNumberText.anchor = TextAnchor.MiddleRight;
        hpNumberText.alignment = TextAlignment.Right;
        hpNumberText.fontSize = fontSize;
        hpNumberText.characterSize = 0.05f;
        hpNumberText.color = Color.green;
        hpNumberText.text = "";
        ApplyNumberFont(hpNumberObject, hpNumberText);

        // ===== Shieldバー（上） =====
        Vector3 shieldBarPosition = new Vector3(-barWidth / 2f + barOffsetX, displayOffsetY + barSpacing, 0f);
        shieldBarObject = CreateBarVisual("ShieldBar", GetSharedShieldBarSprite(), shieldBarPosition, 10);
        shieldBarTransform = shieldBarObject.transform;
        shieldBarRenderer = shieldBarObject.GetComponent<SpriteRenderer>();

        // ===== Shield下地バー（最大値表示） =====
        shieldBarBGObject = CreateBarVisual("ShieldBarBG", GetSharedFlatColorSprite(shieldBarBGColor), shieldBarPosition, 8);
        shieldBarBGTransform = shieldBarBGObject.transform;

        // ===== Shieldゴーストバー（被弾直後、減った分をゆっくり追いかける演出。下地の前・本体の後ろに配置） =====
        shieldGhostBarObject = CreateBarVisual("ShieldGhostBar", GetSharedFlatColorSprite(Color.white), shieldBarPosition, 9);
        shieldGhostBarObject.SetActive(useGhostBar);
        shieldGhostBarTransform = shieldGhostBarObject.transform;
        shieldGhostBarRenderer = shieldGhostBarObject.GetComponent<SpriteRenderer>();

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
        ApplyNumberFont(shieldNumberObject, shieldNumberText);

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
        if (introHidden) return; // 登場演出中は何も表示しない（SetIntroHidden(true)で非表示済み）

        // ワールド座標で位置をセット（回転の影響だけを除外。スケールは乗算して反映）
        // world = enemy.pos + lossyScale * localOffset （rotation なし）
        // これにより SpriteSwimAnimation の回転起因の軌道ずれが消え、元のレイアウトが保たれる
        Vector3 basePos = _hasFixedBasePosition ? _fixedBasePosition : transform.position;
        Vector3 ls = transform.lossyScale;
        float xSign = ls.x < 0 ? -1f : 1f;

        // autoBarWidth: スプライトのワールド幅に合わせてbarWidthを更新
        if (autoBarWidth && enemySpriteRenderer != null)
            barWidth = enemySpriteRenderer.bounds.size.x * barWidthRatio;

        // displayScaleMultiplier でバー幅を確定
        float effBarWidth = barWidth * displayScaleMultiplier;

        // autoBarHeight: barHeightとbarSpacingを常にセットで自動計算する
        // barSpacingを別フラグにすると「heightだけON」で必ず重なるため、同ブロックで強制更新
        if (autoBarHeight)
        {
            barHeight = effBarWidth * barHeightRatio;
            barSpacing = barHeight * Mathf.Max(1.01f, barSpacingMultiplier);
        }

        float effBarHeight = barHeight * displayScaleMultiplier;

        // autoTextSize: barHeightに合わせてHP/Shield数値テキストのcharacterSizeを更新
        if (autoTextSize && hpNumberText != null)
        {
            float charSize = barHeight * textHeightRatio;
            hpNumberText.characterSize = charSize;
            if (shieldNumberText != null) shieldNumberText.characterSize = charSize;
        }

        float barStartX = (-effBarWidth / 2f + barOffsetX) * xSign;
        float numberX   = (effBarWidth / 2f + numberOffsetX + barOffsetX) * xSign;
        if (hpBarObject != null)
        {
            hpBarObject.transform.position = new Vector3(basePos.x + ls.x * barStartX, basePos.y + ls.y * displayOffsetY, basePos.z - 0.1f);
            hpBarObject.transform.rotation = Quaternion.identity;
        }
        if (hpGhostBarObject != null)
        {
            hpGhostBarObject.transform.position = new Vector3(basePos.x + ls.x * barStartX, basePos.y + ls.y * displayOffsetY, basePos.z - 0.1f);
            hpGhostBarObject.transform.rotation = Quaternion.identity;
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
        if (shieldGhostBarObject != null)
        {
            shieldGhostBarObject.transform.position = new Vector3(basePos.x + ls.x * barStartX, basePos.y + ls.y * (displayOffsetY + barSpacing), basePos.z - 0.1f);
            shieldGhostBarObject.transform.rotation = Quaternion.identity;
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
        // ★原本との差分：フェーズ内のHPで描く（コントローラーが無い場合は原本と同じ）
        int shownHp    = neonDancer != null ? neonDancer.DisplayPhaseHp    : stats.HP;
        int shownMaxHp = neonDancer != null ? neonDancer.DisplayPhaseMaxHp : stats.MaxHP;
        hpNumberText.text = $"{shownHp}";

        float hpRatio = shownMaxHp > 0 ? Mathf.Clamp01((float)shownHp / shownMaxHp) : 0f;
        if (hpBarTransform != null && hpBarRenderer != null)
        {
            float scaleX = parentLossyScale.x != 0 ? (effBarWidth * hpRatio * textureHeight / textureWidth) / parentLossyScale.x : effBarWidth * hpRatio;
            float scaleY = parentLossyScale.y != 0 ? effBarHeight / parentLossyScale.y : effBarHeight;
            hpBarTransform.localScale = new Vector3(scaleX, scaleY, 1f);
        }

        // ===== HPゴーストバー（被弾直後、本体より遅れて追従する） =====
        if (useGhostBar && hpGhostBarTransform != null && hpGhostBarRenderer != null)
        {
            // ★色・速度は全エネミー共通アセットから毎フレーム読む（Play中の調整も即座に反映するため）
            EnemyHealthGhostBarSettings settings = GhostSettings;
            Color ghostColor = settings != null ? settings.ghostColor : new Color(1f, 0.25f, 0.1f, 0.9f);
            float catchUpSpeed = settings != null ? settings.catchUpSpeedPerSecond : 0.6f;

            hpGhostBarRenderer.color = ghostColor;

            if (hpGhostRatio < 0f) hpGhostRatio = hpRatio; // 初回のみ現在値に合わせて開始

            if (hpGhostRatio > hpRatio)
                hpGhostRatio = Mathf.Max(hpRatio, hpGhostRatio - catchUpSpeed * Time.deltaTime);
            else
                hpGhostRatio = hpRatio; // HP増加時（回復等）は追従演出せず即座に合わせる

            float ghostScaleX = parentLossyScale.x != 0 ? (effBarWidth * hpGhostRatio * textureHeight / textureWidth) / parentLossyScale.x : effBarWidth * hpGhostRatio;
            float ghostScaleY = parentLossyScale.y != 0 ? effBarHeight / parentLossyScale.y : effBarHeight;
            hpGhostBarTransform.localScale = new Vector3(ghostScaleX, ghostScaleY, 1f);
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

            bool b7Active = Game.Skills.SkillManager.Instance != null
                && Game.Skills.SkillManager.Instance.IsShieldBreakBoostActive
                && shield != null
                && shield == Game.Skills.SkillManager.Instance.CurrentBoostShield;
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
            // アイコン基準Y: 最上位のバー（Shield有効ならShieldBar、なければHPBar）の上端基準
            bool hasActiveShield = shield != null && shield.IsEnabled;
            float topBarCenterY = hasActiveShield ? (displayOffsetY + barSpacing) : displayOffsetY;
            float iconWorldBaseY = basePos.y + ls.y * (topBarCenterY + barHeight * 0.5f) + debuffIconOffset.y;

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
                    // X: バーオフセットはls.x倍(バーに追従)、アイコン間隔はワールド固定（xSignに依存しない）
                    float iconWorldX = basePos.x + ls.x * barOffsetX * xSign + debuffIconOffset.x + i * debuffIconSpacing;
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
            float shieldTargetRatio;

            if (shield.IsBroken)
            {
                // 破壊中：回復進行度に応じて徐々に表示
                float progress = shield.RecoveryProgress;
                shieldTargetRatio = progress;

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
                float shieldRatio = shield.MaxShield > 0 ? (float)shield.CurrentShield / shield.MaxShield : 0f;
                shieldTargetRatio = shieldRatio;

                shieldNumberText.text = $"{shield.CurrentShield}";
                shieldNumberObject.SetActive(true);

                if (shieldBarTransform != null && shieldBarRenderer != null)
                {
                    float scaleX = parentLossyScale.x != 0 ? (effBarWidth * shieldRatio * textureHeight / textureWidth) / parentLossyScale.x : effBarWidth * shieldRatio;
                    float scaleY = parentLossyScale.y != 0 ? effBarHeight / parentLossyScale.y : effBarHeight;
                    shieldBarTransform.localScale = new Vector3(scaleX, scaleY, 1f);

                    // 透明度は完全不透明
                    Color barColor = shieldBarRenderer.color;
                    shieldBarRenderer.color = new Color(barColor.r, barColor.g, barColor.b, 1f);
                    shieldBarObject.SetActive(true);
                }
            }

            // ===== Shieldゴーストバー（被弾直後、本体より遅れて追従する。破壊時の一気減りにも追従する。
            //   IsBroken中の回復進行はダメージではないため、追いつく演出をせず即座に合わせる） =====
            if (useGhostBar && shieldGhostBarTransform != null && shieldGhostBarRenderer != null)
            {
                EnemyHealthGhostBarSettings settings = GhostSettings;
                Color ghostColor = settings != null ? settings.ghostColor : new Color(1f, 0.25f, 0.1f, 0.9f);
                float catchUpSpeed = settings != null ? settings.catchUpSpeedPerSecond : 0.6f;

                shieldGhostBarRenderer.color = ghostColor;

                if (shieldGhostRatio < 0f) shieldGhostRatio = shieldTargetRatio; // 初回のみ現在値に合わせて開始

                if (shieldGhostRatio > shieldTargetRatio)
                    shieldGhostRatio = Mathf.Max(shieldTargetRatio, shieldGhostRatio - catchUpSpeed * Time.deltaTime);
                else
                    shieldGhostRatio = shieldTargetRatio; // 増加時（回復等）は追従演出せず即座に合わせる

                float ghostScaleX = parentLossyScale.x != 0 ? (effBarWidth * shieldGhostRatio * textureHeight / textureWidth) / parentLossyScale.x : effBarWidth * shieldGhostRatio;
                float ghostScaleY = parentLossyScale.y != 0 ? effBarHeight / parentLossyScale.y : effBarHeight;
                shieldGhostBarTransform.localScale = new Vector3(ghostScaleX, ghostScaleY, 1f);
                shieldGhostBarObject.SetActive(true);
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
            if (shieldGhostBarObject != null) shieldGhostBarObject.SetActive(false);
        }
    }

    /// <summary>
    /// バー用GameObjectを作成し、既に用意されたSpriteを割り当てる（テクスチャ生成は行わない）
    /// </summary>
    /// <param name="name">GameObjectの名前</param>
    /// <param name="sprite">割り当てるSprite（全エネミー共通で使い回すことを想定）</param>
    /// <param name="position">バーの位置</param>
    /// <param name="sortingOrder">描画順</param>
    private GameObject CreateBarVisual(string name, Sprite sprite, Vector3 position, int sortingOrder)
    {
        GameObject barObj = new GameObject(name);
        barObj.transform.SetParent(transform);
        barObj.transform.localPosition = new Vector3(position.x, position.y, -0.1f); // Z座標を手前に

        SpriteRenderer renderer = barObj.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = sortingOrder;

        // 親のワールドスケールを取得して、その影響を打ち消す
        Vector3 parentLossyScale = transform.lossyScale;
        // スプライトのサイズ: 幅=texture.width/texture.height, 高さ=1
        float scaleX = parentLossyScale.x != 0 ? (barWidth * sprite.texture.height / sprite.texture.width) / parentLossyScale.x : barWidth;
        float scaleY = parentLossyScale.y != 0 ? barHeight / parentLossyScale.y : barHeight;
        barObj.transform.localScale = new Vector3(scaleX, scaleY, 1f);

        return barObj;
    }

    // ★HP/Shieldバー本体・下地バー・ゴーストバーは、いずれもエネミー間で色が同じであれば
    //   見た目が完全に同一になる（HP/Shieldの配色は完全ハードコード、下地色は
    //   Inspectorで個別設定可能だが現状全39体が同じデフォルト値）。
    //   1体ごとにTexture2D/Spriteを新規生成せず、プロセス全体で使い回すことで
    //   生成コストとSprite数を削減する（SkillHUDCardUIの共有平行四辺形Spriteと同じ考え方）。
    //   staticなので、個別インスタンスのOnDestroyでは破棄しない（他のエネミーも使用中のため）。
    private static Sprite s_sharedHpBarSprite;
    private static Sprite s_sharedShieldBarSprite;
    private static readonly System.Collections.Generic.Dictionary<Color, Sprite> s_sharedFlatColorSpriteCache = new();

    private static Sprite GetSharedHpBarSprite()
    {
        if (s_sharedHpBarSprite == null)
        {
            Color[] hpColors = { new Color(0.0f, 0.5f, 0.0f), new Color(0.0f, 0.6f, 0.0f), new Color(0.0f, 0.8f, 0.0f), Color.green };
            s_sharedHpBarSprite = CreateSharedGradientSprite(hpColors);
        }
        return s_sharedHpBarSprite;
    }

    private static Sprite GetSharedShieldBarSprite()
    {
        if (s_sharedShieldBarSprite == null)
        {
            Color[] shieldColors = { new Color(0.0f, 0.5f, 0.5f), new Color(0.0f, 0.6f, 0.6f), new Color(0.0f, 0.8f, 0.8f), Color.cyan };
            s_sharedShieldBarSprite = CreateSharedGradientSprite(shieldColors);
        }
        return s_sharedShieldBarSprite;
    }

    /// <summary>
    /// 指定した単色のSpriteを返す（色ごとにキャッシュし、同じ色なら使い回す）。
    /// 下地バー・ゴーストバーはInspectorで色を個別設定できる余地を残しつつ、
    /// 同じ色を使う限り複数エネミー間でSpriteを共有できるようにする。
    /// </summary>
    private static Sprite GetSharedFlatColorSprite(Color color)
    {
        if (s_sharedFlatColorSpriteCache.TryGetValue(color, out Sprite cached) && cached != null)
            return cached;

        Sprite sprite = CreateSharedGradientSprite(new Color[] { color, color });
        s_sharedFlatColorSpriteCache[color] = sprite;
        return sprite;
    }

    /// <summary>
    /// 横方向グラデーションのSpriteを生成する（複数色対応）。全エネミー共通で使い回すためstatic。
    /// </summary>
    private static Sprite CreateSharedGradientSprite(Color[] colors)
    {
        if (colors == null || colors.Length < 2)
        {
            // フォールバック：白→黒
            colors = new Color[] { Color.white, Color.black };
        }

        const int width = 256;  // テクスチャの幅
        const int height = 1;   // テクスチャの高さ（1ピクセルで十分）

        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.hideFlags = HideFlags.DontSave;

        for (int x = 0; x < width; x++)
        {
            float t = (float)x / (width - 1); // 0.0～1.0
            texture.SetPixel(x, 0, GetGradientColor(colors, t));
        }
        texture.Apply();

        // Pivot: 左中央（左端固定でゲージが減る）
        // pixelsPerUnit = texture.height に設定して、スプライトの高さを1ユニットにする
        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0f, 0.5f), height);
        sprite.hideFlags = HideFlags.DontSave;
        return sprite;
    }

    /// <summary>
    /// 複数色のグラデーションから指定位置の色を取得
    /// </summary>
    /// <param name="colors">色配列</param>
    /// <param name="t">位置（0.0～1.0）</param>
    private static Color GetGradientColor(Color[] colors, float t)
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

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        var st = GetComponent<EnemyStats>();
        if (st == null) return;

        // ランタイムのLateUpdateと同じ計算式で描画
        // - バーの「位置」はlossyScaleで倍率換算（LateUpdateと同一）
        // - バーの「幅・高さ」はworld空間で一定（lossyScaleで補正されるため非スケール）
        Vector3 basePos = transform.position;
        Vector3 ls      = transform.lossyScale;
        float xSign = ls.x < 0 ? -1f : 1f;

        int maxHp   = st.MaxHP;
        float barStartX = (-barWidth * 0.5f + barOffsetX) * xSign;
        float numXLocal = ( barWidth * 0.5f + numberOffsetX + barOffsetX) * xSign;

        DrawGizmoBar(basePos, ls, barStartX, numXLocal, displayOffsetY,
            barWidth, barHeight,
            new Color(0f, 0.15f, 0f, 0.9f), new Color(0f, 0.8f, 0.2f, 0.85f), new Color(0f, 0.5f, 0f, 1f),
            $"HP:{maxHp}", Color.green);

        if (enemyData != null && enemyData.enableShield)
        {
            int maxShield = Mathf.CeilToInt(maxHp * enemyData.shieldPercentage);
            DrawGizmoBar(basePos, ls, barStartX, numXLocal, displayOffsetY + barSpacing,
                barWidth, barHeight,
                new Color(0f, 0.1f, 0.2f, 0.9f), new Color(0f, 0.7f, 1f, 0.85f), new Color(0f, 0.4f, 0.8f, 1f),
                $"SH:{maxShield}", Color.cyan);
        }
    }

    private void DrawGizmoBar(Vector3 basePos, Vector3 ls,
        float barStartXLocal, float numXLocal, float barYLocal, float w, float h,
        Color bg, Color fill, Color outline, string labelText, Color labelColor)
    {
        // 位置: lossyScale倍（LateUpdateと同一）
        float worldLeftX   = basePos.x + ls.x * barStartXLocal;
        float worldRightX  = worldLeftX + w;          // 幅はworld定数（非スケール）
        float worldCenterY = basePos.y + ls.y * barYLocal;
        float worldTopY    = worldCenterY + h * 0.5f; // 高さはworld定数（非スケール）
        float worldBotY    = worldCenterY - h * 0.5f;
        float worldNumX    = basePos.x + ls.x * numXLocal;

        var verts = new Vector3[] {
            new Vector3(worldLeftX,  worldBotY, 0f),
            new Vector3(worldLeftX,  worldTopY, 0f),
            new Vector3(worldRightX, worldTopY, 0f),
            new Vector3(worldRightX, worldBotY, 0f),
        };
        Handles.DrawSolidRectangleWithOutline(verts, bg, outline);
        Handles.DrawSolidRectangleWithOutline(verts, fill, Color.clear);

        var style = new GUIStyle { fontSize = Mathf.Max(10, fontSize / 5) };
        style.normal.textColor = labelColor;
        Handles.Label(new Vector3(worldNumX, worldCenterY, 0f), labelText, style);
    }
#endif
}
