using UnityEngine;
using System.Collections;

/// <summary>
/// 指定Area・指定Stageの間だけ、手前のシルエット付近から巡回ドローンをランダム出現させる背景演出。
/// CrowFlybySpawnerと違い、画面端からではなく画面内（シルエット付近の3ゾーン）のランダム位置に
/// 出現し、まず画面中央方向へ進む初速を与えることで、出現直後にすぐ画面外へ抜けないようにする。
/// Play前のInspectorで全パラメータ調整可能。
/// Hierarchy: 空のGameObjectを作成しこのスクリプトをアタッチするだけで動作する
/// （droneFramesフィールドにドローンのコマを割り当てること）。
/// 出現ゾーン（Left/Right/BottomCenter）はGameObject選択時にSceneビューで枠として可視化される。
/// ★[ExecuteAlways]により、Play前でもこのGameObjectの位置に実際のドローン絵+目の発光ドットの
///   プレビューが表示され、Eye Light Local Offset等をInspectorで調整しながら確認できる。
/// </summary>
[ExecuteAlways]
public class DroneFlybySpawner : MonoBehaviour
{
    [Header("発生条件")]
    [Tooltip("この演出を発生させるArea番号")]
    [SerializeField] private int targetAreaNumber = 3;

    [Tooltip("この演出を発生させるStageインデックス（0=Stage1, 1=Stage2, 2=Stage3）")]
    [SerializeField] private int[] activeStageIndices = { 0, 1 };

    [Header("素材（アニメーションコマ・ループ再生）")]
    [Tooltip("プロペラの回転・カメラ/機体の向きが変わる3コマ")]
    [SerializeField] private Sprite[] droneFrames = new Sprite[3];

    [Tooltip("コマ送り速度（1秒あたりのコマ数）")]
    [SerializeField] private float frameRate = 8f;

    [Tooltip("スプライトの表示スケール")]
    [SerializeField] private Vector3 droneScale = new Vector3(0.2f, 0.2f, 1f);

    [Tooltip("ドローンの色味（シルエット風にするなら黒のまま）")]
    [SerializeField] private Color droneTintColor = Color.black;

    [Tooltip("ドローンの不透明度（1=不透明、0.85なら少し透けて見える）")]
    [Range(0f, 1f)]
    [SerializeField] private float droneAlpha = 0.85f;

    [Header("描画順")]
    [SerializeField] private string sortingLayerName = "Default";
    [SerializeField] private int sortingOrder = -5;

    [Header("出現間隔（秒）")]
    [SerializeField] private float spawnIntervalMin = 5f;
    [SerializeField] private float spawnIntervalMax = 9f;

    [Header("出現ゾーン（ワールド座標・手前シルエットに合わせて調整）")]
    [SerializeField] private Vector2 leftZoneCenter = new Vector2(-4.5f, 1.5f);
    [SerializeField] private Vector2 leftZoneSize = new Vector2(2.5f, 4f);

    [SerializeField] private Vector2 rightZoneCenter = new Vector2(4.5f, 1.5f);
    [SerializeField] private Vector2 rightZoneSize = new Vector2(2.5f, 4f);

    [SerializeField] private Vector2 bottomCenterZoneCenter = new Vector2(0f, -2f);
    [SerializeField] private Vector2 bottomCenterZoneSize = new Vector2(3f, 1f);

    [Header("出現直後の初速（画面内側へ向かう角度のバラつき）")]
    [Tooltip("画面中央方向からどれだけランダムにずらすか（±この角度）")]
    [SerializeField] private float initialDirectionJitterDegrees = 45f;

    [Header("飛行速度（緩急あり）")]
    [SerializeField] private float baseSpeedMin = 1.0f;
    [SerializeField] private float baseSpeedMax = 1.8f;
    [SerializeField] private float speedMultiplierMin = 0.6f;
    [SerializeField] private float speedMultiplierMax = 1.5f;
    [SerializeField] private float speedChangeIntervalMin = 0.5f;
    [SerializeField] private float speedChangeIntervalMax = 2f;
    [SerializeField] private float speedTransitionRate = 2.5f;

    [Header("不規則な弧（旋回角速度自体がランダムな間隔で変わり続ける）")]
    [Tooltip("旋回角速度の最大値（度/秒）。この範囲内でプラスマイナスランダムに変わり続ける")]
    [SerializeField] private float turnRateMax = 40f;
    [Tooltip("次に旋回目標が変わるまでの間隔（秒）")]
    [SerializeField] private float turnChangeIntervalMin = 0.4f;
    [SerializeField] private float turnChangeIntervalMax = 1.2f;
    [Tooltip("旋回角速度が目標値へ近づく速さ")]
    [SerializeField] private float turnTransitionRate = 60f;

    [Header("フェードアウト（一定時間後に縮小しながら消える）")]
    [SerializeField] private float visibleDuration = 5f;
    [SerializeField] private float fadeOutDuration = 1.5f;
    [Range(0f, 1f)]
    [SerializeField] private float minScaleAtFadeEnd = 0.3f;

    [Header("目（カメラ）の赤い点滅ライト")]
    [Tooltip("コマごと（Drone Framesと同じ並び順）にカメラ/目がある位置へのローカルオフセットを設定する。\n" +
             "3コマともカメラの向きが違う絵のため、コマ単位で個別に調整が必要")]
    [SerializeField] private Vector2[] eyeLightLocalOffsets = new Vector2[3]
    {
        new Vector2(-0.1f, -0.15f),
        new Vector2(0f, -0.15f),
        new Vector2(0.1f, -0.15f),
    };

    [Tooltip("発光ドットの大きさ。0にすると光を出さない")]
    [SerializeField] private float eyeLightSize = 0.06f;

    [SerializeField] private Color eyeLightColor = new Color(1f, 0.1f, 0.1f, 1f);

    [Tooltip("点滅の最小/最大不透明度")]
    [Range(0f, 1f)]
    [SerializeField] private float eyeLightMinAlpha = 0.3f;
    [Range(0f, 1f)]
    [SerializeField] private float eyeLightMaxAlpha = 1f;

    [Tooltip("点滅の速さ（1秒あたりの往復回数）")]
    [SerializeField] private float eyeLightPulseFrequency = 1.5f;

    [Header("エディタプレビュー（Play前確認用）")]
    [Tooltip("このGameObjectの位置にドローン本体+目の発光ドットのプレビューを表示する（Play中は非表示）")]
    [SerializeField] private bool showEditorPreview = true;

    private Coroutine spawnCoroutine;

    private float TimeScale =>
        SlowMotionManager.Instance != null ? SlowMotionManager.Instance.TimeScale : 1f;

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

#if UNITY_EDITOR
    [Header("エディタプレビュー: コマ間の横並び間隔")]
    [SerializeField] private float previewFrameSpacing = 1.5f;

    private GameObject previewRoot;
    private SpriteRenderer[] previewBodyRenderers;
    private SpriteRenderer[] previewEyeRenderers;
    private static Sprite cachedPreviewGlowSprite;

    private void OnValidate()
    {
        // OnValidate内での即時GameObject生成はコンソール警告になることがあるため1フレーム遅らせる
        UnityEditor.EditorApplication.delayCall += DelayedRefreshEditorPreview;
    }

    private void DelayedRefreshEditorPreview()
    {
        if (this == null) return; // 遅延中にオブジェクトが破棄された場合
        RefreshEditorPreview();
    }

    /// <summary>
    /// droneFramesの3コマを横並びで同時にプレビュー表示する。
    /// コマごとにカメラの向きが違う絵のため、1コマだけでは他コマのEye Light Local Offsetsを
    /// 調整できない問題があり、3コマ分すべてを並べて個別に確認できるようにしている。
    /// </summary>
    private void RefreshEditorPreview()
    {
        if (Application.isPlaying)
        {
            ClearEditorPreview();
            return;
        }

        if (!showEditorPreview || droneFrames == null || droneFrames.Length == 0)
        {
            ClearEditorPreview();
            return;
        }

        int count = droneFrames.Length;

        if (previewRoot == null || previewBodyRenderers == null || previewBodyRenderers.Length != count)
        {
            ClearEditorPreview();

            previewRoot = new GameObject("EditorPreview_DoNotSave (Drone x" + count + ")");
            previewRoot.hideFlags = HideFlags.HideAndDontSave;
            previewRoot.transform.SetParent(transform, false);

            previewBodyRenderers = new SpriteRenderer[count];
            previewEyeRenderers = new SpriteRenderer[count];

            for (int i = 0; i < count; i++)
            {
                GameObject slotGo = new GameObject($"Frame_{i}");
                slotGo.hideFlags = HideFlags.HideAndDontSave;
                slotGo.transform.SetParent(previewRoot.transform, false);
                slotGo.transform.localPosition = new Vector3((i - (count - 1) * 0.5f) * previewFrameSpacing, 0f, 0f);
                slotGo.transform.localScale = droneScale;

                GameObject bodyGo = new GameObject("Body");
                bodyGo.hideFlags = HideFlags.HideAndDontSave;
                bodyGo.transform.SetParent(slotGo.transform, false);
                previewBodyRenderers[i] = bodyGo.AddComponent<SpriteRenderer>();

                GameObject eyeGo = new GameObject("Eye");
                eyeGo.hideFlags = HideFlags.HideAndDontSave;
                eyeGo.transform.SetParent(slotGo.transform, false);
                previewEyeRenderers[i] = eyeGo.AddComponent<SpriteRenderer>();
            }
        }

        for (int i = 0; i < count; i++)
        {
            // 横並びの間隔はスケールの影響を受けないよう、スロット単位で位置決めしてから
            // droneScaleは各スロットのlocalScaleとして持たせる
            Transform slot = previewBodyRenderers[i].transform.parent;
            slot.localPosition = new Vector3((i - (count - 1) * 0.5f) * previewFrameSpacing, 0f, 0f);
            slot.localScale = droneScale;

            previewBodyRenderers[i].sprite = droneFrames[i];
            previewBodyRenderers[i].color = new Color(droneTintColor.r, droneTintColor.g, droneTintColor.b, droneAlpha);
            previewBodyRenderers[i].sortingLayerName = sortingLayerName;
            previewBodyRenderers[i].sortingOrder = sortingOrder;

            Vector2 offset = (eyeLightLocalOffsets != null && i < eyeLightLocalOffsets.Length) ? eyeLightLocalOffsets[i] : Vector2.zero;

            if (eyeLightSize > 0f)
            {
                previewEyeRenderers[i].enabled = true;
                previewEyeRenderers[i].sprite = GetPreviewGlowSprite();
                previewEyeRenderers[i].color = eyeLightColor;
                previewEyeRenderers[i].transform.localPosition = new Vector3(offset.x, offset.y, -0.01f);
                previewEyeRenderers[i].transform.localScale = Vector3.one * eyeLightSize;
                previewEyeRenderers[i].sortingLayerName = sortingLayerName;
                previewEyeRenderers[i].sortingOrder = sortingOrder + 1;
            }
            else
            {
                previewEyeRenderers[i].enabled = false;
            }
        }
    }

    private void ClearEditorPreview()
    {
        if (previewRoot != null)
        {
            DestroyImmediate(previewRoot);
        }
        previewRoot = null;
        previewBodyRenderers = null;
        previewEyeRenderers = null;
    }

    private static Sprite GetPreviewGlowSprite()
    {
        if (cachedPreviewGlowSprite != null) return cachedPreviewGlowSprite;

        const int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Vector2 center = new Vector2(size / 2f, size / 2f);
        float radius = size / 2f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), center) / radius;
                float a = Mathf.Clamp01(1f - d);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
            }
        }
        tex.Apply();
        cachedPreviewGlowSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        return cachedPreviewGlowSprite;
    }
#endif

    private void OnStageStarted(int stageIndex)
    {
        bool areaMatches = GameSession.HasValidArea() && GameSession.SelectedArea.areaNumber == targetAreaNumber;
        bool stageMatches = System.Array.IndexOf(activeStageIndices, stageIndex) >= 0;

        if (areaMatches && stageMatches)
            StartSpawning();
        else
            StopSpawning();
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
            SpawnDrone();
        }
    }

    private void SpawnDrone()
    {
        if (droneFrames == null || droneFrames.Length == 0 || droneFrames[0] == null || Camera.main == null) return;

        Camera cam = Camera.main;

        Vector2 center;
        Vector2 size;
        switch (Random.Range(0, 3))
        {
            case 0: center = leftZoneCenter; size = leftZoneSize; break;
            case 1: center = rightZoneCenter; size = rightZoneSize; break;
            default: center = bottomCenterZoneCenter; size = bottomCenterZoneSize; break;
        }

        float x = center.x + Random.Range(-size.x * 0.5f, size.x * 0.5f);
        float y = center.y + Random.Range(-size.y * 0.5f, size.y * 0.5f);
        Vector3 spawnPos = new Vector3(x, y, 0f);

        // 画面中央方向へ向かう初速（±jitter度ランダムにずらす）ことで出現直後の即退場を防ぐ
        Vector3 camCenter = new Vector3(cam.transform.position.x, cam.transform.position.y, 0f);
        Vector3 towardCenter = (camCenter - spawnPos).normalized;
        if (towardCenter.sqrMagnitude < 0.0001f) towardCenter = Vector3.right;
        float jitter = Random.Range(-initialDirectionJitterDegrees, initialDirectionJitterDegrees);
        Vector3 initialDir = Quaternion.Euler(0f, 0f, jitter) * towardCenter;

        GameObject go = new GameObject("DroneFlyby");
        go.transform.position = spawnPos;
        go.transform.localScale = droneScale;

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = droneFrames[0];
        sr.color = new Color(droneTintColor.r, droneTintColor.g, droneTintColor.b, droneAlpha);
        sr.sortingLayerName = sortingLayerName;
        sr.sortingOrder = sortingOrder;

        DroneFlybyController controller = go.AddComponent<DroneFlybyController>();
        float baseSpeed = Random.Range(baseSpeedMin, baseSpeedMax);
        controller.Init(droneFrames, frameRate, initialDir, baseSpeed,
            speedMultiplierMin, speedMultiplierMax, speedChangeIntervalMin, speedChangeIntervalMax, speedTransitionRate,
            turnRateMax, turnChangeIntervalMin, turnChangeIntervalMax, turnTransitionRate,
            visibleDuration, fadeOutDuration, minScaleAtFadeEnd,
            eyeLightLocalOffsets, eyeLightSize, eyeLightColor, eyeLightMinAlpha, eyeLightMaxAlpha, eyeLightPulseFrequency);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0f, 1f, 1f, 0.5f);
        DrawZoneGizmo(leftZoneCenter, leftZoneSize);
        DrawZoneGizmo(rightZoneCenter, rightZoneSize);
        DrawZoneGizmo(bottomCenterZoneCenter, bottomCenterZoneSize);
    }

    private void DrawZoneGizmo(Vector2 center, Vector2 size)
    {
        Gizmos.DrawWireCube(new Vector3(center.x, center.y, 0f), new Vector3(size.x, size.y, 0.1f));
    }
}
