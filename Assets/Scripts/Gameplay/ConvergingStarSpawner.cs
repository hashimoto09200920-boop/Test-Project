using System.Collections;
using UnityEngine;

/// <summary>
/// Area10 Final Stage背景の吸い込まれる粒子演出。画面全体からFarLayer中心へ向けて粒子を継続的に
/// 発生させ、ConvergingStarControllerが1個ずつ「目標地点（中心からランダムな半径だけ離れた収束
/// ゾーン内の1点）に到達した瞬間に消滅」を保証する。色はAreaSelectの10エリアカラーからランダム選択
/// （StarFlareSpawner/Area10StarFieldSpawnerと同じ値）。Area9のMeteorEffectとは別の仕組み。
/// Play前のInspectorで全パラメータ調整可能。追加のアート素材は不要（手続き的に生成）。
/// Hierarchy: 空のGameObjectを作成しこのスクリプトをアタッチするだけで動作する。
/// </summary>
public class ConvergingStarSpawner : MonoBehaviour
{
    [Header("色（AreaSelectの10エリアカラー、MeteorEffect/StarFlareSpawnerと同じ値）")]
    [SerializeField] private Color[] areaColors = new Color[]
    {
        new Color(0.60784316f, 0.56078434f, 0.78039217f, 1f),
        new Color(0.29803923f, 0.6862745f, 0.49019608f, 1f),
        new Color(0.5529412f, 0.6f, 0.68235296f, 1f),
        new Color(0.8784314f, 0.47843137f, 0.24705882f, 1f),
        new Color(0.69803923f, 0.22745098f, 0.32156864f, 1f),
        new Color(0.8784314f, 0.6901961f, 0.30980393f, 1f),
        new Color(0.30980393f, 0.56078434f, 0.8784314f, 1f),
        new Color(0.37254903f, 0.8392157f, 0.8392157f, 1f),
        new Color(0.6392157f, 0.68235296f, 0.8784314f, 1f),
        new Color(0.91f, 0.79f, 0.42f, 1f),
    };

    [Header("描画順")]
    [SerializeField] private string sortingLayerName = "Background";
    [Tooltip("FarLayer=-10, MidLayer=-9, Silhouette=-8 より手前、Gameplayレイヤー（敵/プレイヤー）より奥")]
    [SerializeField] private int sortingOrder = -7;

    [Header("発生範囲（中心＝FarLayer中心からの半径、ワールド単位）")]
    [Tooltip("中心点（通常は原点＝FarLayer中心）")]
    [SerializeField] private Vector3 centerPoint = Vector3.zero;
    [Tooltip("粒子が発生する範囲の半径（画面対角の半分程度が目安）")]
    [SerializeField] private float spawnRadius = 11.8f;

    [Header("収束ゾーン（1点ではなく、この範囲内でランダムに消滅する）")]
    [SerializeField] private float convergeRadiusMin = 0.8f;
    [SerializeField] private float convergeRadiusMax = 2f;

    [Header("速度・サイズ")]
    [SerializeField] private float speedMin = 1.5f;
    [SerializeField] private float speedMax = 2.5f;
    [SerializeField] private float sizeMin = 0.25f;
    [SerializeField] private float sizeMax = 0.5f;
    [Range(0f, 1f)]
    [SerializeField] private float maxAlpha = 0.7f;

    [Tooltip("吸い込まれる速さ自体のカーブ：横軸=経過の生の進行度(0〜1)、縦軸=実際に移動させる位置の進行度(0〜1)。" +
        "既定値は数式 t² の滑らかな二次カーブ（最初は緩やかに動き、中心に近づくほど速く動く）。" +
        "縮小カーブはこの逆（1-値）として自動計算されるため、別途持たない")]
    [SerializeField] private AnimationCurve speedCurve = BuildSmoothCurve();

    // ★t²を6点サンプリングし、Keyframe(time,value)の2引数コンストラクタによる自動接線補間に任せることで、
    //   手打ちの少数点カーブにありがちなガタつきのない滑らかな形にする。
    private static AnimationCurve BuildSmoothCurve()
    {
        var keys = new Keyframe[6];
        for (int i = 0; i < 6; i++)
        {
            float t = i / 5f;
            keys[i] = new Keyframe(t, t * t);
        }
        return new AnimationCurve(keys);
    }

    [Header("発生頻度")]
    [SerializeField] private float spawnIntervalMin = 0.05f;
    [SerializeField] private float spawnIntervalMax = 0.15f;

    [Header("発生条件（★他Area・他Stageに絶対影響させないための判定）")]
    [Tooltip("Area10ボスラッシュのFinal Stageに該当するStageIndex（waveStagesの4番目=index3）")]
    [SerializeField] private int finalStageIndex = 3;

    private Coroutine spawnCoroutine;

    private float TimeScale =>
        SlowMotionManager.Instance != null ? SlowMotionManager.Instance.TimeScale : 1f;

    private void OnEnable()
    {
        EnemySpawner.OnStageStarted += OnStageStarted;
    }

    private void OnDisable()
    {
        EnemySpawner.OnStageStarted -= OnStageStarted;
        StopEffect();
    }

    private void OnStageStarted(int stageIndex)
    {
        bool shouldBeActive = GameSession.IsBossRushActive && stageIndex == finalStageIndex;
        if (shouldBeActive && spawnCoroutine == null)
            spawnCoroutine = StartCoroutine(SpawnLoop());
        else if (!shouldBeActive)
            StopEffect();
    }

    private void StopEffect()
    {
        if (spawnCoroutine != null)
        {
            StopCoroutine(spawnCoroutine);
            spawnCoroutine = null;
        }
        for (int i = transform.childCount - 1; i >= 0; i--)
            Destroy(transform.GetChild(i).gameObject);
    }

    private IEnumerator SpawnLoop()
    {
        while (true)
        {
            SpawnOne();

            float wait = Random.Range(spawnIntervalMin, spawnIntervalMax);
            float elapsed = 0f;
            while (elapsed < wait)
            {
                elapsed += Time.deltaTime * TimeScale;
                yield return null;
            }
        }
    }

    private void SpawnOne()
    {
        Vector2 offset = Random.insideUnitCircle * spawnRadius;
        Vector3 startPos = centerPoint + new Vector3(offset.x, offset.y, 0f);

        GameObject go = new GameObject("ConvergingStar");
        go.transform.SetParent(transform, false);

        var star = go.AddComponent<ConvergingStarController>();
        star.Init(
            startPos,
            centerPoint,
            Random.Range(convergeRadiusMin, convergeRadiusMax),
            RandomAreaColor(),
            Random.Range(speedMin, speedMax),
            Random.Range(sizeMin, sizeMax),
            maxAlpha,
            speedCurve,
            sortingLayerName, sortingOrder);
    }

    private Color RandomAreaColor()
    {
        return (areaColors != null && areaColors.Length > 0)
            ? areaColors[Random.Range(0, areaColors.Length)]
            : Color.white;
    }
}
