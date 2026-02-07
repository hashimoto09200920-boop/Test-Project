using UnityEngine;

/// <summary>
/// サブパーツ（体、尻尾など）に配置し、ダメージをメインパーツ（頭など）に転送する
/// HingeJoint2D で物理的につながった複数パーツ敵で使用
/// </summary>
public class EnemyPartProxy : MonoBehaviour
{
    [Header("Main Part Reference")]
    [Tooltip("メインパーツ（EnemyStats を持つオブジェクト）への参照")]
    [SerializeField] private GameObject mainPartObject;

    private EnemyStats mainStats;
    private EnemyHitFeedback mainFeedback;
    private EnemyDamageReceiver mainDamageReceiver;

    private void Awake()
    {
        if (mainPartObject != null)
        {
            mainStats = mainPartObject.GetComponent<EnemyStats>();
            mainFeedback = mainPartObject.GetComponent<EnemyHitFeedback>();
            mainDamageReceiver = mainPartObject.GetComponent<EnemyDamageReceiver>();

            // メインパーツに自分（サブパーツ）を登録
            // メインパーツが破壊される時、一緒に破壊される
            if (mainStats != null)
            {
                mainStats.RegisterSubPart(gameObject);
            }
        }
    }

    /// <summary>
    /// メインパーツの EnemyStats を取得（EnemyPart から呼ばれる）
    /// </summary>
    public EnemyStats GetMainStats()
    {
        return mainStats;
    }

    /// <summary>
    /// メインパーツの EnemyHitFeedback を取得（EnemyPart から呼ばれる）
    /// </summary>
    public EnemyHitFeedback GetMainFeedback()
    {
        return mainFeedback;
    }

    /// <summary>
    /// メインパーツの EnemyDamageReceiver を取得（EnemyPart から呼ばれる）
    /// </summary>
    public EnemyDamageReceiver GetMainDamageReceiver()
    {
        return mainDamageReceiver;
    }
}
