using System.Collections;
using UnityEngine;

// Area09 Stage3限定の「月」演出。
// Area09以外では常に非表示。Area09内でも、Stage3開始まで非表示にしておき、
// そのタイミングでフェードインする。
// ★このスクリプトはSpriteRenderer.colorを直接書き換えない。代わりにVisibilityAlpha
//   （0=非表示, 1=フル表示）を公開するだけにとどめ、実際の色反映はHeartbeatPulse側に
//   一本化する。表示制御と鳴動演出が同じcolorプロパティを取り合って競合するのを防ぐため
public class Area09MoonController : MonoBehaviour
{
    private const int TargetAreaNumber = 9;
    private const int Stage3Index = 2; // 0-based。Stage3開始

    [Tooltip("出現時のフェードインにかける時間（秒）")]
    [SerializeField] private float fadeInDuration = 4f;

    public float VisibilityAlpha { get; private set; } = 0f;

    private bool revealed = false;

    private void Start()
    {
        bool isArea09 = GameSession.HasValidArea() && GameSession.SelectedArea != null
            && GameSession.SelectedArea.areaNumber == TargetAreaNumber;

        if (!isArea09)
        {
            gameObject.SetActive(false);
            return;
        }

        VisibilityAlpha = 0f;
        EnemySpawner.OnStageStarted += HandleStageStarted;
    }

    private void OnDestroy()
    {
        EnemySpawner.OnStageStarted -= HandleStageStarted;
    }

    private void HandleStageStarted(int startedStageIndex)
    {
        if (revealed || startedStageIndex != Stage3Index) return;
        revealed = true;
        StartCoroutine(RevealSequence());
    }

    // ★Stage3開始直後はボス紹介のVS演出（PauseManager.SetPauseBlocked(true)で囲まれる）が
    //   挟まることがある。ここでフェードを即開始すると、演出中に完了してしまい、演出が終わって
    //   実際に画面が見える頃には既にフル表示済み＝「急に表示された」ように見えてしまう。
    //   そのため、演出が始まる→終わるのを待ってから、フェードインを開始する
    private IEnumerator RevealSequence()
    {
        // ★Area09は必ずVS演出（PauseBlocked）が入る前提のため、タイムアウトで諦めて
        //   途中でフェードを始めてしまわないよう、演出が始まるまで無期限に待つ
        //   （以前は最大待機時間で打ち切っていたが、背景遷移等の前段の待ち時間が長いと
        //   タイムアウトが先に来てしまい、演出中にフェードが始まる不具合があった）
        while (PauseManager.Instance == null || !PauseManager.Instance.IsPauseBlocked)
        {
            yield return null;
        }

        while (PauseManager.Instance != null && PauseManager.Instance.IsPauseBlocked)
        {
            yield return null;
        }

        yield return StartCoroutine(FadeIn());
    }

    private IEnumerator FadeIn()
    {
        float t = 0f;
        while (t < fadeInDuration)
        {
            t += Time.deltaTime;
            VisibilityAlpha = Mathf.Clamp01(t / fadeInDuration);
            yield return null;
        }
        VisibilityAlpha = 1f;
    }
}
