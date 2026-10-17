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

    [Header("Stamp Motion（スタンプ風の出現）")]
    [Tooltip("ON：数字が大きく出て一瞬で縮み、少しその場で止まってから上へ流れる（スタンプを押したような出方）。" +
             "OFF：従来の出方（Punch-in Scaleの曲線で拡大→収縮しながら、出た瞬間から上へ流れる）")]
    [SerializeField] private bool useStampMotion = true;
    [Tooltip("通常ヒットの大きさの変化（横軸0〜1＝stampDuration、縦軸＝倍率）。大きく出る→少し縮みすぎる→元の大きさ")]
    [SerializeField] private AnimationCurve stampScaleNormal = StampCurve(1.7f, 1.62f, 0.93f);
    [Tooltip("Justヒットの大きさの変化（横軸0〜1＝stampDurationJust、縦軸＝倍率）")]
    [SerializeField] private AnimationCurve stampScaleJust = StampCurve(1.7f, 1.62f, 0.93f);
    [Tooltip("通常ヒットで大きさが元に戻るまでの秒数")]
    [SerializeField] private float stampDuration = 0.2f;
    [Tooltip("Justヒットで大きさが元に戻るまでの秒数")]
    [SerializeField] private float stampDurationJust = 0.24f;
    [Tooltip("出てから上へ流れ始めるまで、その場で止まっている秒数（出現の秒数を含む）")]
    [SerializeField] private float stampHoldSeconds = 0.3f;
    [Tooltip("表示時間（lifeTime）のうち、この割合を過ぎてから薄くなり始める（0＝出た瞬間から薄くなる）")]
    [SerializeField, Range(0f, 0.95f)] private float stampFadeStartRatio = 0.5f;

    [Header("Just Rotation Kick")]
    [SerializeField] private float justRotationKickDegrees = 14f;
    [SerializeField] private float rotationSettleDuration = 0.25f;
    [Tooltip("ON：Justの傾きを消えるまでそのまま保つ（正面へ戻さない）。OFF：rotationSettleDurationの秒数で正面へ戻す")]
    [SerializeField] private bool keepRotationKick = true;
    [Tooltip("ON：Justの傾きを「左・正面・右」の3通りから、直前と同じにならないように選ぶ。OFF：常に左右どちらかに傾ける")]
    [SerializeField] private bool mixFrontAngle = true;
    private static int s_lastKickDir = 99;

    [Header("Normal Hit Font")]
    [Tooltip("通常ヒット（Justではない）の数字を太字にする（色はそのまま）。太さはNormal MaterialのWeight Bold（Inspectorの Debug Settings > Weight Bold）で調整できる")]
    [SerializeField] private bool normalBold = true;
    [Tooltip("通常ヒット（Justではない）の数字の大きさの倍率（EnemyHitFeedbackで決まる文字サイズに掛ける）。Justの数字には掛からない")]
    [SerializeField] private float normalSizeMultiplier = 1.2f;

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

    /// <summary>
    /// スタンプ風の大きさの曲線：出た瞬間に peak 倍 → 少しの間ほぼその大きさ（hold倍）を保つ → 一気に縮んで undershoot 倍 → 1倍
    /// </summary>
    private static AnimationCurve StampCurve(float peak, float hold, float undershoot)
    {
        var c = new AnimationCurve(
            new Keyframe(0f, peak),
            new Keyframe(0.3f, hold),
            new Keyframe(0.75f, undershoot),
            new Keyframe(1f, 1f));
        for (int i = 0; i < c.length; i++) c.SmoothTangents(i, 0f);
        return c;
    }

    private float timer;
    private float numberFontSizeMul = 1f; // 数字用フォント（Bungee）で元のフォントと数字の高さをそろえる倍率
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

        // ★数字用フォント（Bungee）に切り替え。文字サイズは表示のたびに設定し直すので、元のフォントと数字の高さをそろえる倍率を覚えておく
        numberFontSizeMul = GameNumberFont.Apply(text, false);
        if (echoText != null) GameNumberFont.Apply(echoText, false);

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

        float baseFontSize = isPowered ? poweredFontSize : normalFontSize * Mathf.Max(0.01f, normalSizeMultiplier);
        // ★ダメージ量が大きいほどフォントを少しずつ大きくする（迫力の演出）
        float scaleMul = 1f + Mathf.Clamp(
            (absDamage - sizeScalingReferenceDamage) * sizeScalingPerDamage,
            0f, maxSizeScaleMultiplier - 1f);
        float finalFontSize = baseFontSize * scaleMul;
        text.fontSize = finalFontSize * numberFontSizeMul;

        text.fontStyle = (!isPowered && normalBold) ? FontStyles.Bold : FontStyles.Normal;
        text.color = isPowered ? poweredColor : normalColor;
        baseColor = text.color;

        Material justMatForHit = isShieldHit && justMaterialShield != null ? justMaterialShield : justMaterial;
        Material mat = isPowered ? (justMatForHit != null ? justMatForHit : normalMaterial) : normalMaterial;
        mat = GameNumberFont.MaterialFor(mat); // 数字用フォントの文字画像に合わせた素材（初回だけ作って使い回す）
        if (mat != null) text.fontSharedMaterial = mat;

        currentMoveVelocity = isShieldHit ? moveVelocityShield : moveVelocityHp;
        isJustActive = isPowered;
        currentRotationKick = isPowered ? justRotationKickDegrees * NextKickDir() : 0f;

        // ★Just成立時だけ、薄く一回り大きい残像を同じ数字で重ねて表示する
        if (echoText != null)
        {
            if (isPowered)
            {
                echoText.text = text.text;
                echoText.fontSize = finalFontSize * echoScaleMultiplier * numberFontSizeMul;
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

    /// <summary>Justの傾きの向き（-1=左 0=正面 1=右）。mixFrontAngleなら3通りから直前と違うものを選ぶ</summary>
    private float NextKickDir()
    {
        if (!mixFrontAngle) return Random.value < 0.5f ? -1f : 1f;
        int d;
        do { d = Random.Range(-1, 2); } while (d == s_lastKickDir);
        s_lastKickDir = d;
        return d;
    }

    private void Update()
    {
        timer += Time.deltaTime;
        // スタンプ風：出てからしばらくはその場で止まり、その後に流れる
        if (!useStampMotion || timer >= stampHoldSeconds)
            transform.position += currentMoveVelocity * Time.deltaTime;

        if (useStampMotion)
        {
            // スタンプ風の出現（大きく出る→一瞬で縮む→元の大きさ）
            float dur = isJustActive ? stampDurationJust : stampDuration;
            AnimationCurve curve = isJustActive ? stampScaleJust : stampScaleNormal;
            float t = dur > 0f ? Mathf.Clamp01(timer / dur) : 1f;
            transform.localScale = baseScale * (t < 1f ? curve.Evaluate(t) : 1f);
        }
        // 出現時のパンチイン（拡大→収縮）
        else if (timer <= punchDuration)
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
            float t = keepRotationKick ? 0f : (rotationSettleDuration > 0f ? Mathf.Clamp01(timer / rotationSettleDuration) : 1f);
            float angle = Mathf.Lerp(currentRotationKick, 0f, t);
            transform.localRotation = Quaternion.Euler(0f, 0f, angle);
        }

        if (fadeOut && text != null)
        {
            // スタンプ風の時は、しっかり読める時間を確保するため後半から薄くする
            float fadeStart = useStampMotion ? lifeTime * stampFadeStartRatio : 0f;
            float t = Mathf.Clamp01((timer - fadeStart) / Mathf.Max(0.0001f, lifeTime - fadeStart));
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

    [ContextMenu("Animatorを無効化して大きさをスクリプトで動かす")]
    private void EditorDisableScaleAnimator()
    {
        // ★Animator（AC_DamagePopupPop／Anim_DamagePopupPop）がm_LocalScaleを毎フレーム0.42に固定しており、
        //   スクリプト側の出現演出（スタンプ・パンチイン）が上書きされて一度も見えていなかった。
        //   Animatorを無効にし、見た目の基本の大きさはアニメと同じ0.42にそろえる
        var anim = GetComponent<Animator>();
        if (anim != null)
        {
            UnityEditor.Undo.RecordObject(anim, "Disable DamagePopup Animator");
            anim.enabled = false;
            UnityEditor.EditorUtility.SetDirty(anim);
        }
        UnityEditor.Undo.RecordObject(transform, "DamagePopup Base Scale");
        transform.localScale = new Vector3(0.42f, 0.42f, 0.42f);
        UnityEditor.EditorUtility.SetDirty(transform);
        UnityEditor.EditorUtility.SetDirty(gameObject);
        if (UnityEditor.PrefabUtility.IsPartOfPrefabAsset(this)) UnityEditor.AssetDatabase.SaveAssets();
        UnityEditor.SceneView.RepaintAll();
        Debug.Log($"[DamagePopup] Animatorを{(anim != null ? "無効化" : "（無し）")}し、基本の大きさを0.42にしました。", this);
    }

    [ContextMenu("スタンプの強さを標準にする（通常1.7倍・Just1.7倍）")]
    private void EditorApplyStrongStamp()
    {
        UnityEditor.Undo.RecordObject(this, "Damage Popup Strong Stamp");
        useStampMotion = true;
        stampScaleNormal = StampCurve(1.7f, 1.62f, 0.93f);
        stampScaleJust = StampCurve(1.7f, 1.62f, 0.93f);
        stampDuration = 0.2f;
        stampDurationJust = 0.24f;
        stampHoldSeconds = 0.3f;
        keepRotationKick = true;
        UnityEditor.EditorUtility.SetDirty(this);
        UnityEditor.EditorUtility.SetDirty(gameObject);
        if (UnityEditor.PrefabUtility.IsPartOfPrefabAsset(this)) UnityEditor.AssetDatabase.SaveAssets();
        UnityEditor.SceneView.RepaintAll();
        Debug.Log("[DamagePopup] スタンプの強さを設定しました（通常1.7倍・Just1.7倍から、0.2秒/0.24秒で元の大きさ。0.3秒その場で止まる）", this);
    }

    [ContextMenu("通常の数字に影を付ける（Normal Materialを書き換え）")]
    private void EditorApplyNormalShadow()
    {
        if (normalMaterial == null)
        {
            Debug.LogWarning("[DamagePopup] Normal Materialが未設定です。", this);
            return;
        }
        UnityEditor.Undo.RecordObject(normalMaterial, "Damage Popup Normal Shadow");
        // ★数字の右下に濃い影を重ねる（Underlay）。明るいエフェクトの上でも数字が浮き出て読める。
        //   位置・濃さ・ぼかしはこのマテリアルのInspector > Underlay で調整できる
        normalMaterial.EnableKeyword("UNDERLAY_ON"); // ★これが無いと影が描画されない
        normalMaterial.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0f, 0f, 0f, 0.85f));
        normalMaterial.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0.6f);
        normalMaterial.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.6f);
        normalMaterial.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0.3f);
        normalMaterial.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.15f);
        UnityEditor.EditorUtility.SetDirty(normalMaterial);
        UnityEditor.AssetDatabase.SaveAssets();
        UnityEditor.SceneView.RepaintAll();
        Debug.Log("[DamagePopup] 通常の数字に影を付けました（" + normalMaterial.name + "）。Underlayの項目で調整できます。", normalMaterial);
    }

    [ContextMenu("通常の数字に縁取りを付ける（Normal Materialを書き換え）")]
    private void EditorApplyNormalOutline()
    {
        if (normalMaterial == null)
        {
            Debug.LogWarning("[DamagePopup] Normal Materialが未設定です。先に「Setup Echo/Spark/Materials」を実行してください。", this);
            return;
        }
        UnityEditor.Undo.RecordObject(normalMaterial, "Damage Popup Normal Outline");
        // ★明るい背景・エフェクトの上でも数字の形が読めるように、濃い紺の縁を付ける（白・黄・水色どの数字色にも合う）
        normalMaterial.EnableKeyword("OUTLINE_ON"); // ★これが無いと太さ・色を設定しても縁が描画されない
        normalMaterial.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.22f);
        normalMaterial.SetColor(ShaderUtilities.ID_OutlineColor, new Color(0.04f, 0.05f, 0.14f, 1f));
        // 縁で文字が細く見えないよう、文字本体を少し太らせる
        normalMaterial.SetFloat(ShaderUtilities.ID_FaceDilate, 0.15f);
        UnityEditor.EditorUtility.SetDirty(normalMaterial);
        UnityEditor.AssetDatabase.SaveAssets();
        UnityEditor.SceneView.RepaintAll();
        Debug.Log("[DamagePopup] 通常の数字の縁取りを設定しました（" + normalMaterial.name + "）。太さ・色はこのマテリアルのInspectorで調整できます。", normalMaterial);
    }

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
