using UnityEngine;
using TMPro;

public class DamagePopup : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private TextMeshPro text;

    [Tooltip("Just成立時だけ表示する残像テキスト（任意。未設定なら残像演出はスキップ）")]
    [SerializeField] private TextMeshPro echoText;

    [Tooltip("シールドヒット時に再生する火花パーティクル（任意。未設定なら火花演出はスキップ）")]
    [SerializeField] private ParticleSystem shieldSparkParticles;

    [Header("Motion")]
    [SerializeField] private Vector3 moveVelocityHp = new Vector3(0f, 1.2f, 0f);
    [Tooltip("シールドヒット時の軌道（斜めに弾かれるイメージ）")]
    [SerializeField] private Vector3 moveVelocityShield = new Vector3(0.6f, 1.0f, 0f);
    [SerializeField] private float lifeTime = 0.6f;

    [Header("Fade")]
    [SerializeField] private bool fadeOut = true;

    [Header("Punch-in Scale")]
    [SerializeField] private AnimationCurve punchScaleNormal = AnimationCurve.EaseInOut(0f, 1.3f, 1f, 1f);
    [SerializeField] private AnimationCurve punchScaleJust = AnimationCurve.EaseInOut(0f, 1.6f, 1f, 1f);
    [SerializeField] private float punchDuration = 0.18f;

    [Header("Just Rotation Kick")]
    [SerializeField] private float justRotationKickDegrees = 14f;
    [SerializeField] private float rotationSettleDuration = 0.25f;

    [Header("Damage-based Size Scaling")]
    [Tooltip("この値を超えたダメージ1につき、フォントサイズがsizeScalingPerDamageずつ増える")]
    [SerializeField] private float sizeScalingReferenceDamage = 10f;
    [SerializeField] private float sizeScalingPerDamage = 0.05f;
    [SerializeField] private float maxSizeScaleMultiplier = 1.6f;

    [Header("Just Outline/Glow Material (Optional)")]
    [Tooltip("通常時に使うマテリアル。未設定ならtext本来のマテリアルのまま")]
    [SerializeField] private Material normalMaterial;
    [Tooltip("HPダメージのJust成立時に使うマテリアル（金色系アウトライン）。未設定ならnormalMaterialのまま")]
    [SerializeField] private Material justMaterial;
    [Tooltip("シールドダメージのJust成立時に使うマテリアル（水色系アウトライン、シールドの青と合わせる）。未設定ならjustMaterialのまま")]
    [SerializeField] private Material justMaterialShield;

    [Header("Just Echo (Optional)")]
    [SerializeField] private float echoScaleMultiplier = 1.35f;
    [SerializeField, Range(0f, 1f)] private float echoStartAlpha = 0.35f;

    private float timer;
    private Color baseColor;
    private Vector3 baseScale;
    private Vector3 currentMoveVelocity;
    private bool isJustActive;
    private float currentRotationKick;

    private void Awake()
    {
        if (text == null) text = GetComponentInChildren<TextMeshPro>();
        if (text != null) baseColor = text.color;
        baseScale = transform.localScale;

        if (echoText != null) echoText.gameObject.SetActive(false);
        if (shieldSparkParticles != null) shieldSparkParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    /// <summary>
    /// プールから取り出したこのポップアップを、指定の内容で再初期化して表示する。
    /// 新規Instantiateは行わず、既存のGameObject/コンポーネントを使い回す。
    /// </summary>
    public void Activate(int damage, bool isPowered, bool isShieldHit, float normalFontSize, float poweredFontSize, Color normalColor, Color poweredColor)
    {
        // ★プールで非アクティブ化されている状態からの再利用に対応するため、
        //   ParticleSystem.Play()等を呼ぶより前に必ずアクティブ化しておく
        //   （非アクティブなGameObject階層下のParticleSystemはPlay()を呼んでも再生されない）
        gameObject.SetActive(true);

        timer = 0f;
        transform.localRotation = Quaternion.identity;
        transform.localScale = baseScale;

        if (text == null)
        {
            return;
        }

        int absDamage = Mathf.Abs(damage);
        text.text = absDamage.ToString();

        float baseFontSize = isPowered ? poweredFontSize : normalFontSize;
        // ★ダメージ量が大きいほどフォントを少しずつ大きくする（迫力の演出）
        float scaleMul = 1f + Mathf.Clamp(
            (absDamage - sizeScalingReferenceDamage) * sizeScalingPerDamage,
            0f, maxSizeScaleMultiplier - 1f);
        float finalFontSize = baseFontSize * scaleMul;
        text.fontSize = finalFontSize;

        text.color = isPowered ? poweredColor : normalColor;
        baseColor = text.color;

        Material justMatForHit = isShieldHit && justMaterialShield != null ? justMaterialShield : justMaterial;
        Material mat = isPowered ? (justMatForHit != null ? justMatForHit : normalMaterial) : normalMaterial;
        if (mat != null) text.fontSharedMaterial = mat;

        currentMoveVelocity = isShieldHit ? moveVelocityShield : moveVelocityHp;
        isJustActive = isPowered;
        currentRotationKick = isPowered ? justRotationKickDegrees * (Random.value < 0.5f ? -1f : 1f) : 0f;

        // ★Just成立時だけ、薄く一回り大きい残像を同じ数字で重ねて表示する
        if (echoText != null)
        {
            if (isPowered)
            {
                echoText.text = text.text;
                echoText.fontSize = finalFontSize * echoScaleMultiplier;
                Color echoCol = poweredColor;
                echoCol.a = echoStartAlpha;
                echoText.color = echoCol;
                if (mat != null) echoText.fontSharedMaterial = mat;
                echoText.gameObject.SetActive(true);
            }
            else
            {
                echoText.gameObject.SetActive(false);
            }
        }

        // ★シールドヒットの火花は固定のParticleSystemをPlayするだけ（Instantiateしない）
        if (isShieldHit && shieldSparkParticles != null)
        {
            shieldSparkParticles.transform.localPosition = Vector3.zero;
            shieldSparkParticles.Play();
        }
    }

    private void Update()
    {
        timer += Time.deltaTime;
        transform.position += currentMoveVelocity * Time.deltaTime;

        // 出現時のパンチイン（拡大→収縮）
        if (timer <= punchDuration)
        {
            AnimationCurve curve = isJustActive ? punchScaleJust : punchScaleNormal;
            float t = punchDuration > 0f ? timer / punchDuration : 1f;
            float scale = curve.Evaluate(t);
            transform.localScale = baseScale * scale;
        }
        else
        {
            transform.localScale = baseScale;
        }

        // Just成立時だけの回転キック（時間とともに0度へ収束）
        if (isJustActive && currentRotationKick != 0f)
        {
            float t = rotationSettleDuration > 0f ? Mathf.Clamp01(timer / rotationSettleDuration) : 1f;
            float angle = Mathf.Lerp(currentRotationKick, 0f, t);
            transform.localRotation = Quaternion.Euler(0f, 0f, angle);
        }

        if (fadeOut && text != null)
        {
            float t = Mathf.Clamp01(timer / lifeTime);
            Color c = baseColor;
            c.a = 1f - t;
            text.color = c;

            if (echoText != null && echoText.gameObject.activeSelf)
            {
                Color ec = echoText.color;
                ec.a = Mathf.Max(0f, (1f - t) * echoStartAlpha);
                echoText.color = ec;
            }
        }

        if (timer >= lifeTime)
        {
            // ★Destroyせず非アクティブ化するだけ（プールに戻す）
            gameObject.SetActive(false);
        }
    }

#if UNITY_EDITOR
    // =========================================================
    // Editor専用：③マテリアル2種／⑥残像テキスト／⑦火花パーティクルを
    // ワンクリックで自動生成する。ビルドには含まれない（UNITY_EDITORガード）。
    // Inspectorでこのコンポーネントの歯車アイコン（コンテキストメニュー）から実行する。
    // =========================================================

    [ContextMenu("Setup Echo/Spark/Materials (Editor Only)")]
    private void EditorSetupEnhancements()
    {
        EditorSetupMaterials();
        EditorSetupEchoText();
        EditorSetupShieldSpark();

        UnityEditor.EditorUtility.SetDirty(this);
        UnityEditor.EditorUtility.SetDirty(gameObject);
        UnityEditor.AssetDatabase.SaveAssets();
        Debug.Log("[DamagePopup] Echo/Spark/Materialsのセットアップが完了しました。プレハブを保存してください。", this);
    }

    private void EditorSetupMaterials()
    {
        TextMeshPro mainText = text != null ? text : GetComponent<TextMeshPro>();
        if (mainText == null || mainText.fontSharedMaterial == null)
        {
            Debug.LogWarning("[DamagePopup] textが未設定のためマテリアルを作成できません。", this);
            return;
        }

        const string folder = "Assets/Prefabs/Effects";

        if (normalMaterial == null)
        {
            Material normalMat = new Material(mainText.fontSharedMaterial) { name = "DamagePopup_NormalMat" };
            UnityEditor.AssetDatabase.CreateAsset(normalMat, folder + "/DamagePopup_NormalMat.mat");
            normalMaterial = normalMat;
        }

        // ★最初に確認できた「見える」状態（Outline 0.25 + Underlay有効・Dilate/Softness 0.5）に
        //   固定する。この後に細くする／グローを弱める調整をする場合は、必ずこの値から少しずつ
        //   動かして都度見た目を確認すること（一気に複数プロパティを変えると原因の切り分けが難しくなる）
        if (justMaterial == null)
        {
            Material justMat = new Material(mainText.fontSharedMaterial) { name = "DamagePopup_JustMat" };
            justMat.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.4f);
            justMat.SetColor(ShaderUtilities.ID_OutlineColor, new Color(1f, 0f, 0f, 1f)); // 赤
            justMat.EnableKeyword("OUTLINE_ON"); // ★これが無いとThickness/Colorを設定してもアウトラインが描画されない
            justMat.EnableKeyword("UNDERLAY_ON");
            justMat.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(1f, 0.85f, 0.3f, 0.8f));
            justMat.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0.1f);
            justMat.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.1f);
            UnityEditor.AssetDatabase.CreateAsset(justMat, folder + "/DamagePopup_JustMat.mat");
            justMaterial = justMat;
        }

        if (justMaterialShield == null)
        {
            Material justMatShield = new Material(mainText.fontSharedMaterial) { name = "DamagePopup_JustMat_Shield" };
            justMatShield.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.6f);
            justMatShield.SetColor(ShaderUtilities.ID_OutlineColor, new Color(0.5f, 0.9f, 1f, 1f));
            justMatShield.EnableKeyword("OUTLINE_ON"); // ★これが無いとThickness/Colorを設定してもアウトラインが描画されない
            justMatShield.EnableKeyword("UNDERLAY_ON");
            justMatShield.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0.3f, 0.7f, 1f, 0.8f));
            justMatShield.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0.1f);
            justMatShield.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.1f);
            UnityEditor.AssetDatabase.CreateAsset(justMatShield, folder + "/DamagePopup_JustMat_Shield.mat");
            justMaterialShield = justMatShield;
        }
    }

    private void EditorSetupEchoText()
    {
        TextMeshPro mainText = text != null ? text : GetComponent<TextMeshPro>();
        if (mainText == null) return;

        Transform existing = transform.Find("EchoText");
        GameObject echoGo;
        if (existing != null)
        {
            echoGo = existing.gameObject;
        }
        else
        {
            echoGo = new GameObject("EchoText");
            UnityEditor.Undo.RegisterCreatedObjectUndo(echoGo, "Create EchoText");
            echoGo.transform.SetParent(transform, false);

            RectTransform mainRect = GetComponent<RectTransform>();
            RectTransform echoRect = echoGo.AddComponent<RectTransform>();
            if (mainRect != null)
            {
                echoRect.anchorMin = mainRect.anchorMin;
                echoRect.anchorMax = mainRect.anchorMax;
                echoRect.pivot = mainRect.pivot;
                echoRect.sizeDelta = mainRect.sizeDelta;
            }
            echoRect.anchoredPosition = Vector2.zero;
            echoRect.localPosition = Vector3.zero;

            TextMeshPro echoTmp = echoGo.AddComponent<TextMeshPro>();
            echoTmp.font = mainText.font;
            echoTmp.fontSharedMaterial = mainText.fontSharedMaterial;
            echoTmp.alignment = mainText.alignment;
            echoTmp.fontSize = mainText.fontSize;
            echoTmp.color = mainText.color;
            echoTmp.text = mainText.text;
            echoTmp.raycastTarget = false;
        }

        echoGo.SetActive(false);
        echoText = echoGo.GetComponent<TextMeshPro>();
    }

    private void EditorSetupShieldSpark()
    {
        // ★このプロジェクトはURP（2D Renderer）のため、Built-in RP用の
        //   AssetDatabase.GetBuiltinExtraResource<Material>("Default-Particle.mat")は
        //   描画パスに乗らず不可視になる。既存パーティクル（M_OrbGlow_Additive等）と同じ
        //   "Universal Render Pipeline/Particles/Unlit" + Additive合成のマテリアルを使う
        const string sparkMatPath = "Assets/Prefabs/Effects/DamagePopup_SparkMat.mat";
        Material sparkMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(sparkMatPath);
        if (sparkMat == null)
        {
            Shader urpParticleShader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (urpParticleShader != null)
            {
                sparkMat = new Material(urpParticleShader) { name = "DamagePopup_SparkMat" };
                sparkMat.SetFloat("_Surface", 1f); // Transparent
                sparkMat.SetFloat("_Blend", 2f);   // Additive
                sparkMat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                sparkMat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
                sparkMat.SetFloat("_ZWrite", 0f);
                sparkMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                sparkMat.SetColor("_BaseColor", new Color(0.6f, 0.85f, 1f, 1f));
                sparkMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

                // ★丸く柔らかい光点テクスチャを割り当てる（未設定だと単なる四角として描画される）
                Texture2D glowTex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Generated/UI/SoftGlowCircle.png");
                if (glowTex != null) sparkMat.SetTexture("_BaseMap", glowTex);

                UnityEditor.AssetDatabase.CreateAsset(sparkMat, sparkMatPath);
            }
            else
            {
                Debug.LogWarning("[DamagePopup] URPのParticles/Unlitシェーダーが見つかりませんでした。マテリアル未設定のまま進めます。", this);
            }
        }

        Transform existing = transform.Find("ShieldSpark");
        GameObject sparkGo;
        if (existing != null)
        {
            sparkGo = existing.gameObject;
        }
        else
        {
            sparkGo = new GameObject("ShieldSpark");
            UnityEditor.Undo.RegisterCreatedObjectUndo(sparkGo, "Create ShieldSpark");
            sparkGo.transform.SetParent(transform, false);
            sparkGo.transform.localPosition = Vector3.zero;
            sparkGo.AddComponent<ParticleSystem>();
        }

        // ★新規作成・既存どちらの場合も、見た目のチューニングは常に再適用する
        //   （既存のものを一度作った後の微調整もこのメニューの再実行だけで反映されるようにする）
        ParticleSystem existingPs = sparkGo.GetComponent<ParticleSystem>();
        if (existingPs != null)
        {
            var main = existingPs.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 0.3f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.35f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 3.0f);
            // ★小さく粒っぽいサイズに（大きい四角に見えないよう控えめ＋ばらつきを持たせる）
            main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.05f);
            main.startColor = new Color(0.6f, 0.85f, 1f, 1f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 24;

            var emission = existingPs.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 14) });

            // ★中心から全方位へ広範囲に飛び散るよう、円形の発生源から放射状に射出する
            var shape = existingPs.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.01f;
            shape.arc = 360f;

            // ★飛び散りながら少しずつ縮んで消えるようにする（火花らしさの演出）
            var sizeOverLifetime = existingPs.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.1f));

            existingPs.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        // ★新規作成・既存どちらの場合も、マテリアルは必ずURP対応のものに揃える
        //   （既にBuilt-in用のDefault-Particle.matが割り当てられていた場合の修正も兼ねる）
        ParticleSystemRenderer existingRenderer = sparkGo.GetComponent<ParticleSystemRenderer>();
        if (existingRenderer != null && sparkMat != null)
        {
            existingRenderer.sharedMaterial = sparkMat;
        }

        shieldSparkParticles = sparkGo.GetComponent<ParticleSystem>();
    }
#endif
}
