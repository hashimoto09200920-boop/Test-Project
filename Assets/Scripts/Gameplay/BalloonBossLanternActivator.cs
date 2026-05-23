using UnityEngine;

/// <summary>
/// BalloonBossLanterns にアタッチする。
/// Area4 Stage3 が開始したときだけランタン群を表示する。
/// Additional Stages に追加することで任意のエリア/ステージでも表示可能（テスト用途など）。
/// </summary>
public class BalloonBossLanternActivator : MonoBehaviour
{
    [System.Serializable]
    public struct AreaStageEntry
    {
        [Tooltip("エリア番号（Area0=0、Area4=4 など）")]
        public int areaNumber;
        [Tooltip("ステージインデックス（0始まり、Stage1=0、Stage2=1、Stage3=2）")]
        public int stageIndex;
    }

    [Tooltip("本番の表示条件（Area4 Stage3）")]
    [SerializeField] private int targetAreaNumber = 4;
    [SerializeField] private int targetStageIndex = 2;

    [Tooltip("追加で表示するエリア/ステージ（テスト用途など、空欄で無効）")]
    [SerializeField] private AreaStageEntry[] additionalStages;

    private void Awake()
    {
        gameObject.SetActive(false);
        EnemySpawner.OnStageStarted += OnStageStarted;
    }

    private void OnDestroy()
    {
        EnemySpawner.OnStageStarted -= OnStageStarted;
    }

    private void OnStageStarted(int stageIndex)
    {
        if (!GameSession.HasValidArea() || GameSession.SelectedArea == null)
        {
            gameObject.SetActive(false);
            return;
        }

        int currentArea = GameSession.SelectedArea.areaNumber;

        bool isTarget = currentArea == targetAreaNumber && stageIndex == targetStageIndex;

        if (!isTarget && additionalStages != null)
        {
            foreach (var entry in additionalStages)
            {
                if (currentArea == entry.areaNumber && stageIndex == entry.stageIndex)
                {
                    isTarget = true;
                    break;
                }
            }
        }

        gameObject.SetActive(isTarget);
    }
}
