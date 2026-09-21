using UnityEngine;
using System.Collections;

/// <summary>
/// 指定Area・指定Stageの間だけ、天井からマリオネットが降りてきて揺れ、
/// 引き上げられながらフェードアウトする背景演出をスポーンする。
/// Area5ボスFingers(DollController)と同じDoll_Idle/Jerk C系スプライトと、
/// 同じ紫→藍→ミントグリーンの糸グラデーションを使う（シルエット化はしない）。
/// Play前のInspectorで全パラメータ調整可能。
/// Hierarchy: 空のGameObjectを作成しこのスクリプトをアタッチするだけで動作する
/// （marionetteSpritesフィールドにDoll_Idle/Doll_Idle_Flip/Jerk C/Jerk C_Flipを割り当てること）。
/// ★[ExecuteAlways]により、Play前でもこのGameObjectの位置に吊り下げ位置のプレビューが表示される。
/// </summary>
[ExecuteAlways]
public class MarionetteSpawner : MonoBehaviour
{
    [Header("発生条件")]
    [Tooltip("この演出を発生させるArea番号")]
    [SerializeField] private int targetAreaNumber = 5;

    [Tooltip("この演出を発生させるStageインデックス（0=Stage1, 1=Stage2, 2=Stage3）")]
    [SerializeField] private int[] activeStageIndices = { 0, 1 };

    [Header("素材（ランダムに1枚選ぶ。シルエット化はしない）")]
    [Tooltip("Doll_Idle / Doll_Idle_Flip / Jerk C / Jerk C_Flip を割り当てる")]
    [SerializeField] private Sprite[] marionetteSprites = new Sprite[4];

    [SerializeField] private Vector3 marionetteScale = new Vector3(0.5f, 0.5f, 1f);

    [Tooltip("吊るされている間の不透明度（1=完全不透明）")]
    [Range(0f, 1f)]
    [SerializeField] private float baseAlpha = 1f;

    [Header("描画順")]
    [SerializeField] private string sortingLayerName = "Background";
    [SerializeField] private int sortingOrder = -4;

    [Header("出現間隔（秒）")]
    [SerializeField] private float spawnIntervalMin = 6f;
    [SerializeField] private float spawnIntervalMax = 12f;

    [Header("出現位置（ワールド座標）")]
    [Tooltip("吊るすX位置の範囲（左=SkillHUD手前、右=画面端手前を想定した非対称値）")]
    [SerializeField] private float spawnXMin = -6f;
    [SerializeField] private float spawnXMax = 8f;

    [Tooltip("画面上端からさらに上、糸の固定アンカー点までの距離")]
    [SerializeField] private float anchorHeightAboveCamera = 2f;

    [Tooltip("吊るされて止まる高さの範囲（差を広げるほど糸の長短がはっきり付く）")]
    [SerializeField] private float targetYMin = -1f;
    [SerializeField] private float targetYMax = 3f;

    [Header("動きのタイミング（秒）")]
    [SerializeField] private float descendDuration = 1.5f;
    [SerializeField] private float hangDuration = 3f;
    [Tooltip("引き上げ+フェードアウト+縮小にかける時間")]
    [SerializeField] private float retractFadeDuration = 1.2f;
    [Range(0f, 1f)]
    [SerializeField] private float minScaleAtFadeEnd = 0.3f;

    [Header("吊り下げ中の揺れ")]
    [SerializeField] private float swayAmplitude = 0.15f;
    [SerializeField] private float swayFrequency = 0.4f;

    [Header("糸（Area5ボスFingersと同じグラデーション）")]
    [SerializeField] private Gradient stringGradient;
    [SerializeField] private float stringWidth = 0.03f;
    [SerializeField] private int stringSegments = 12;
    [Tooltip("糸の中間のたわみ量")]
    [SerializeField] private float stringSagAmount = 0.15f;

    [Tooltip("糸の不透明度（1=完全不透明）")]
    [Range(0f, 1f)]
    [SerializeField] private float stringAlpha = 0.85f;

    [Tooltip("人形を必ず糸より前面にするためのsortingOrder差分（糸のsortingOrder = 人形のsortingOrder + この値）。マイナス値にする")]
    [SerializeField] private int stringSortingOrderOffset = -1;

    [Tooltip("Marionette Spritesと同じ並び順で、各スプライトごとの「糸が繋がって見える位置」のローカルオフセット")]
    [SerializeField] private Vector2[] stringAttachOffsets = new Vector2[4]
    {
        new Vector2(0f, 0.9f),
        new Vector2(0f, 0.9f),
        new Vector2(0.3f, 0.7f),
        new Vector2(-0.3f, 0.7f),
    };

    private Coroutine spawnCoroutine;
    private bool? lastSpawnWasRightSide; // null=まだ1回も出現していない
    private readonly System.Collections.Generic.List<MarionetteController> activeInstances = new System.Collections.Generic.List<MarionetteController>();

    private float TimeScale =>
        SlowMotionManager.Instance != null ? SlowMotionManager.Instance.TimeScale : 1f;

    private void Awake()
    {
        InitGradientIfDefault();
    }

    /// <summary>DollController(Fingers)のデフォルト糸グラデーションと同じ紫→藍→ミントグリーン</summary>
    private void InitGradientIfDefault()
    {
        if (stringGradient == null) stringGradient = new Gradient();
        var keys = stringGradient.colorKeys;
        bool isEmpty = keys.Length == 0 || (keys.Length == 2 && keys[0].color == Color.white && keys[1].color == Color.white);
        if (!isEmpty) return;

        stringGradient.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(new Color(0.800f, 0.267f, 1.000f), 0.00f),
                new GradientColorKey(new Color(0.467f, 0.333f, 0.933f), 0.50f),
                new GradientColorKey(new Color(0.267f, 1.000f, 0.800f), 1.00f),
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(1f, 1f),
            }
        );
    }

    private void OnEnable()
    {
        EnemySpawner.OnStageStarted += OnStageStarted;
#if UNITY_EDITOR
        if (Application.isPlaying) ClearEditorPreview();
        else RefreshEditorPreview();
#endif
    }

    private void OnDisable()
    {
        EnemySpawner.OnStageStarted -= OnStageStarted;
        StopSpawning();
#if UNITY_EDITOR
        ClearEditorPreview();
#endif
    }

    private void OnStageStarted(int stageIndex)
    {
        bool areaMatches = !GameSession.IsBossRushActive && GameSession.HasValidArea() && GameSession.GetEffectiveAreaNumber() == targetAreaNumber;
        bool stageMatches = System.Array.IndexOf(activeStageIndices, stageIndex) >= 0;

        if (areaMatches && stageMatches)
            StartSpawning();
        else
        {
            StopSpawning();
            FadeOutAllActive();
        }
    }

    /// <summary>対象Stageから外れた瞬間に、既に画面上にいる個体を即座にフェードアウトさせる</summary>
    private void FadeOutAllActive()
    {
        for (int i = 0; i < activeInstances.Count; i++)
        {
            if (activeInstances[i] != null) activeInstances[i].ForceFadeOut();
        }
        activeInstances.Clear();
    }

    private void StartSpawning()
    {
        if (spawnCoroutine != null) return;
        spawnCoroutine = StartCoroutine(SpawnLoop());
    }

    private void StopSpawning()
    {
        if (spawnCoroutine == null) return;
        StopCoroutine(spawnCoroutine);
        spawnCoroutine = null;
    }

    private IEnumerator SpawnLoop()
    {
        while (true)
        {
            float wait = Random.Range(spawnIntervalMin, spawnIntervalMax);
            float elapsed = 0f;
            while (elapsed < wait)
            {
                elapsed += Time.deltaTime * TimeScale;
                yield return null;
            }
            SpawnMarionette();
        }
    }

    /// <summary>
    /// 出現位置が毎回被らないよう、前回が左右どちらの半分だったかを覚えておき、
    /// 次回は必ず逆側の半分からランダムに決める。
    /// </summary>
    private float PickNextSpawnX()
    {
        float centerX = (spawnXMin + spawnXMax) * 0.5f;
        bool spawnOnRightSide = lastSpawnWasRightSide.HasValue ? !lastSpawnWasRightSide.Value : (Random.value < 0.5f);
        lastSpawnWasRightSide = spawnOnRightSide;

        return spawnOnRightSide ? Random.Range(centerX, spawnXMax) : Random.Range(spawnXMin, centerX);
    }

    private void SpawnMarionette()
    {
        if (marionetteSprites == null || marionetteSprites.Length == 0 || Camera.main == null) return;

        int spriteIndex = Random.Range(0, marionetteSprites.Length);
        Sprite sprite = marionetteSprites[spriteIndex];
        if (sprite == null) return;

        Vector2 attachOffset = (stringAttachOffsets != null && spriteIndex < stringAttachOffsets.Length)
            ? stringAttachOffsets[spriteIndex] : Vector2.zero;

        Camera cam = Camera.main;
        float x = PickNextSpawnX();
        float camTop = cam.transform.position.y + cam.orthographicSize;
        Vector3 anchorPos = new Vector3(x, camTop + anchorHeightAboveCamera, 0f);
        float targetY = Random.Range(targetYMin, targetYMax);

        GameObject go = new GameObject("Marionette");
        go.transform.position = anchorPos;
        go.transform.localScale = marionetteScale;

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = new Color(1f, 1f, 1f, baseAlpha);
        sr.sortingLayerName = sortingLayerName;
        sr.sortingOrder = sortingOrder;

        MarionetteController controller = go.AddComponent<MarionetteController>();
        controller.Init(anchorPos, targetY,
            descendDuration, hangDuration, retractFadeDuration, minScaleAtFadeEnd,
            swayAmplitude, swayFrequency,
            stringGradient, stringWidth, stringSegments, stringSagAmount, stringAlpha,
            attachOffset, stringSortingOrderOffset);
        activeInstances.Add(controller);
    }

#if UNITY_EDITOR
    [Header("エディタプレビュー: スプライト間の横並び間隔")]
    [SerializeField] private float previewSpriteSpacing = 2f;

    private GameObject previewRoot;
    private SpriteRenderer[] previewBodyRenderers;
    private LineRenderer[] previewStringRenderers;

    private void OnValidate()
    {
        InitGradientIfDefault();
        UnityEditor.EditorApplication.delayCall += DelayedRefreshEditorPreview;
    }

    private void DelayedRefreshEditorPreview()
    {
        if (this == null) return;
        RefreshEditorPreview();
    }

    /// <summary>
    /// marionetteSpritesの4体を横並びで同時にプレビュー表示する。
    /// スプライトごとにstringAttachOffsetsが違うため、1体だけでは他3体の調整ができない問題があり、
    /// 4体分すべてを並べて個別に確認できるようにしている（DroneFlybySpawnerと同じ考え方）。
    /// </summary>
    private void RefreshEditorPreview()
    {
        if (Application.isPlaying)
        {
            ClearEditorPreview();
            return;
        }

        if (marionetteSprites == null || marionetteSprites.Length == 0 || Camera.main == null)
        {
            ClearEditorPreview();
            return;
        }

        int count = marionetteSprites.Length;

        if (previewRoot == null || previewBodyRenderers == null || previewBodyRenderers.Length != count)
        {
            ClearEditorPreview();
            DestroyStalePreviewChildren();

            previewRoot = new GameObject("EditorPreview_DoNotSave (Marionette x" + count + ")");
            previewRoot.hideFlags = HideFlags.HideAndDontSave;
            previewRoot.transform.SetParent(transform, false);

            previewBodyRenderers = new SpriteRenderer[count];
            previewStringRenderers = new LineRenderer[count];

            for (int i = 0; i < count; i++)
            {
                GameObject bodyGo = new GameObject($"Body_{i}");
                bodyGo.hideFlags = HideFlags.HideAndDontSave;
                bodyGo.transform.SetParent(previewRoot.transform, false);
                previewBodyRenderers[i] = bodyGo.AddComponent<SpriteRenderer>();

                GameObject stringGo = new GameObject($"String_{i}");
                stringGo.hideFlags = HideFlags.HideAndDontSave;
                stringGo.transform.SetParent(previewRoot.transform, false);
                previewStringRenderers[i] = stringGo.AddComponent<LineRenderer>();
                previewStringRenderers[i].useWorldSpace = true;
                previewStringRenderers[i].numCapVertices = 4;
                previewStringRenderers[i].material = new Material(Shader.Find("Sprites/Default"));
            }
        }

        Camera cam = Camera.main;
        float camTop = cam.transform.position.y + cam.orthographicSize;
        float targetY = (targetYMin + targetYMax) * 0.5f;
        float centerX = (spawnXMin + spawnXMax) * 0.5f;

        for (int i = 0; i < count; i++)
        {
            float x = centerX + (i - (count - 1) * 0.5f) * previewSpriteSpacing;
            Vector3 anchorPos = new Vector3(x, camTop + anchorHeightAboveCamera, 0f);
            Vector2 attachOffset = (stringAttachOffsets != null && i < stringAttachOffsets.Length) ? stringAttachOffsets[i] : Vector2.zero;
            Vector3 bodyPos = new Vector3(x, targetY, 0f);
            Vector3 stringBottom = bodyPos + new Vector3(attachOffset.x, attachOffset.y, 0f) * marionetteScale.x;

            var bodyR = previewBodyRenderers[i];
            bodyR.sprite = marionetteSprites[i];
            bodyR.color = new Color(1f, 1f, 1f, baseAlpha);
            bodyR.sortingLayerName = sortingLayerName;
            bodyR.sortingOrder = sortingOrder;
            bodyR.transform.position = bodyPos;
            bodyR.transform.localScale = marionetteScale;

            var stringR = previewStringRenderers[i];
            int segs = Mathf.Max(2, stringSegments);
            stringR.positionCount = segs;
            stringR.startWidth = stringWidth;
            stringR.endWidth = stringWidth;
            stringR.colorGradient = stringGradient;
            stringR.sortingLayerName = sortingLayerName;
            stringR.sortingOrder = sortingOrder + stringSortingOrderOffset;

            for (int s = 0; s < segs; s++)
            {
                float t = (segs <= 1) ? 0f : (float)s / (segs - 1);
                Vector3 p = Vector3.Lerp(anchorPos, stringBottom, t);
                p.y -= stringSagAmount * 4f * t * (1f - t);
                p.z = 0.05f;
                stringR.SetPosition(s, p);
            }
        }
    }

    private void ClearEditorPreview()
    {
        if (previewRoot != null) DestroyImmediate(previewRoot);
        previewRoot = null;
        previewBodyRenderers = null;
        previewStringRenderers = null;
    }

    /// <summary>
    /// スクリプトの再コンパイル（ドメインリロード）でprevieRoot等のフィールド参照だけが
    /// リセットされ、実体のプレビューGameObjectが子として残ったままになることがある。
    /// 新しいプレビューを作る前に、同名の古い残骸を掃除しておく。
    /// </summary>
    private void DestroyStalePreviewChildren()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (child != null && child.name.StartsWith("EditorPreview_DoNotSave"))
                DestroyImmediate(child.gameObject);
        }
    }
#endif
}
