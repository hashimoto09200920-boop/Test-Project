using UnityEngine;
using System.Collections;

/// <summary>
/// Area7の背景演出：「Area7の上はArea8の上昇気流」という設定で、MarshalとDragonが
/// FarLayer_Silhouette_Area07_Aの位置から上空へ昇っていく様子をスポーンする。
/// シルエットより手前に出ないよう、sortingOrderをシルエットより奥（低い値）に設定すること。
/// Play前のInspectorで全パラメータ調整可能。
/// Hierarchy: 空のGameObjectを作成しこのスクリプトをアタッチするだけで動作する。
/// Marshal Move Frames には Burst1〜4.png（Assets/Art/Enemy/S1_19_Marshal/）を、
/// Dragon Move Frames には Idle1,3,5,6,7,8.png（Assets/Art/Enemy/S2_10_Dragon/、
/// reuseIdleFramesForMove仕様によりこれが実際のMoveアニメの実体）を割り当てること。
/// </summary>
public class RisingCreatureSpawner : MonoBehaviour
{
    [Header("発生条件")]
    [Tooltip("この演出を発生させるArea番号")]
    [SerializeField] private int targetAreaNumber = 7;

    [Tooltip("この演出を発生させるStageインデックス（0=Stage1, 1=Stage2, 2=Stage3）")]
    [SerializeField] private int[] activeStageIndices = { 0, 1 };

    [Header("出現比率")]
    [Tooltip("Marshalが選ばれる確率（0.7=70%）。残りはDragon")]
    [Range(0f, 1f)]
    [SerializeField] private float marshalRatio = 0.7f;

    [Header("Marshal（Burst1〜4.pngを割り当てる）")]
    [SerializeField] private Sprite[] marshalMoveFrames = new Sprite[4];
    [SerializeField] private float marshalFrameRate = 8f;
    [SerializeField] private Vector3 marshalScale = new Vector3(0.15f, 0.15f, 1f);
    [Tooltip("Marshalが選ばれた時の同時出現数（この範囲でランダム）")]
    [SerializeField] private int marshalCountMin = 1;
    [SerializeField] private int marshalCountMax = 2;
    [Tooltip("不規則な左右揺れの振れ幅")]
    [SerializeField] private float marshalSwayAmplitude = 0.4f;
    [SerializeField] private float marshalSwayChangeIntervalMin = 0.3f;
    [SerializeField] private float marshalSwayChangeIntervalMax = 1f;
    [SerializeField] private float marshalSwayTransitionRate = 1.5f;

    [Header("Dragon（Idle1,3,5,6,7,8.pngを割り当てる＝実質Move Animate）")]
    [SerializeField] private Sprite[] dragonMoveFrames = new Sprite[6];
    [SerializeField] private float dragonFrameRate = 6f;
    [SerializeField] private Vector3 dragonScale = new Vector3(0.2f, 0.2f, 1f);
    [Tooltip("S字グライドの振れ幅")]
    [SerializeField] private float dragonSerpentineAmplitude = 0.6f;
    [Tooltip("S字グライドの周期（1秒あたりの往復回数）")]
    [SerializeField] private float dragonSerpentineFrequency = 0.25f;
    [Tooltip("グライドに連動したバンク（傾き）の最大角度")]
    [SerializeField] private float dragonBankAmplitude = 15f;

    [Header("描画順（シルエットより奥＝低いsortingOrderにすること）")]
    [Tooltip("Background_SilhouetteはsortingOrder=-8のため、これより小さい値にする")]
    [SerializeField] private string sortingLayerName = "Background";
    [SerializeField] private int sortingOrder = -9;

    [Header("出現間隔（秒）")]
    [SerializeField] private float spawnIntervalMin = 4f;
    [SerializeField] private float spawnIntervalMax = 8f;

    [Header("出現位置（ワールド座標）")]
    [Tooltip("FarLayer_Silhouette_Area07_Aの地面ライン付近の高さ・X範囲")]
    [SerializeField] private float spawnYMin = -5f;
    [SerializeField] private float spawnYMax = -4.5f;
    [SerializeField] private float spawnXMin = -6f;
    [SerializeField] private float spawnXMax = 6f;

    [Header("遠近感（最初は小さく出現）")]
    [Tooltip("marshalScale/dragonScaleに対する初期スケール倍率（小さいほど遠くに見える）")]
    [Range(0.05f, 1f)]
    [SerializeField] private float initialScaleMultiplier = 0.35f;

    [Header("上昇速度")]
    [SerializeField] private float riseSpeedMin = 0.5f;
    [SerializeField] private float riseSpeedMax = 1f;

    [Header("フェードアウト（一定時間上昇後、縮小しながら消える）")]
    [SerializeField] private float visibleDurationMin = 5f;
    [SerializeField] private float visibleDurationMax = 9f;
    [SerializeField] private float fadeOutDuration = 1.5f;
    [Range(0f, 1f)]
    [SerializeField] private float minScaleAtFadeEnd = 0.3f;

    [Tooltip("不透明度（1=不透明）")]
    [Range(0f, 1f)]
    [SerializeField] private float alpha = 0.9f;

    private Coroutine spawnCoroutine;
    private readonly System.Collections.Generic.List<RisingCreatureController> activeInstances = new System.Collections.Generic.List<RisingCreatureController>();

    private float TimeScale =>
        SlowMotionManager.Instance != null ? SlowMotionManager.Instance.TimeScale : 1f;

    private void OnEnable()
    {
        EnemySpawner.OnStageStarted += OnStageStarted;
    }

    private void OnDisable()
    {
        EnemySpawner.OnStageStarted -= OnStageStarted;
        StopSpawning();
    }

    private void OnStageStarted(int stageIndex)
    {
        bool areaMatches = GameSession.HasValidArea() && GameSession.SelectedArea.areaNumber == targetAreaNumber;
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
            SpawnWave();
        }
    }

    private void SpawnWave()
    {
        bool isMarshal = Random.value < marshalRatio;

        if (isMarshal)
        {
            int count = Random.Range(marshalCountMin, marshalCountMax + 1);
            for (int i = 0; i < count; i++)
                SpawnOne(isMarshal: true);
        }
        else
        {
            SpawnOne(isMarshal: false);
        }
    }

    private void SpawnOne(bool isMarshal)
    {
        Sprite[] moveFrames = isMarshal ? marshalMoveFrames : dragonMoveFrames;
        if (moveFrames == null || moveFrames.Length == 0 || moveFrames[0] == null) return;

        float x = Random.Range(spawnXMin, spawnXMax);
        float y = Random.Range(spawnYMin, spawnYMax);

        Vector3 baseScale = isMarshal ? marshalScale : dragonScale;
        Vector3 spawnScale = baseScale * initialScaleMultiplier;

        GameObject go = new GameObject(isMarshal ? "RisingMarshal" : "RisingDragon");
        go.transform.position = new Vector3(x, y, 0f);
        go.transform.localScale = spawnScale;

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = moveFrames[0];
        sr.color = new Color(1f, 1f, 1f, alpha);
        sr.sortingLayerName = sortingLayerName;
        sr.sortingOrder = sortingOrder;

        RisingCreatureController controller = go.AddComponent<RisingCreatureController>();
        float riseSpeed = Random.Range(riseSpeedMin, riseSpeedMax);
        float visibleDuration = Random.Range(visibleDurationMin, visibleDurationMax);

        RisingCreatureController.SwayStyle style = isMarshal
            ? RisingCreatureController.SwayStyle.Irregular
            : RisingCreatureController.SwayStyle.Serpentine;

        controller.Init(moveFrames, isMarshal ? marshalFrameRate : dragonFrameRate, riseSpeed,
            style,
            marshalSwayAmplitude, marshalSwayChangeIntervalMin, marshalSwayChangeIntervalMax, marshalSwayTransitionRate,
            dragonSerpentineAmplitude, dragonSerpentineFrequency, dragonBankAmplitude,
            visibleDuration, fadeOutDuration, minScaleAtFadeEnd);
        activeInstances.Add(controller);
    }
}
