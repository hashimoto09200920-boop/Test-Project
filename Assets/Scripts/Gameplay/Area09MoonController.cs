using System.Collections;
using UnityEngine;

// Area09 Stage3限定の「月面と地球」演出。
// Area09以外では常に非表示。Area09内でも、Stage3開始まで非表示にしておき、
// そのタイミングでフェードインする。
// 月本体（このGameObject）に加えて、Stage3専用に複製した小さい地球一式
// （fadeRenderers/areaGatedObjectsに登録）も同じタイミングで表示・フェードする。
public class Area09MoonController : MonoBehaviour
{
    private const int TargetAreaNumber = 9;
    private const int Stage3Index = 2; // 0-based。Stage3開始

    [Tooltip("出現時のフェードインにかける時間（秒）")]
    [SerializeField] private float fadeInDuration = 4f;

    [Tooltip("月本体を含め、フェードイン対象にする全SpriteRenderer（地球一式もここに登録する）")]
    [SerializeField] private SpriteRenderer[] fadeRenderers;

    [Tooltip("Area09以外では丸ごと非アクティブにするGameObject（fadeRenderers対象＋SpriteMask等、色を持たないものも含む）")]
    [SerializeField] private GameObject[] areaGatedObjects;

    [Tooltip("Stage3表示開始と同時に非表示にするGameObject（Stage1/2用の巨大な地球一式。月の円で隠れない画面四隅からはみ出て見えてしまうため）")]
    [SerializeField] private GameObject[] hideOnRevealObjects;

    // ★保留中のHeartbeatPulse/MoonRimGlow/MoonAfterimage（後日再導入予定）がまだこのプロパティを
    //   参照しているため、コンパイルを通すために残してある。ApplyAlpha()と同じ値に追従させる
    public float VisibilityAlpha { get; private set; } = 0f;

    private Color[] baseColors;
    private bool revealed = false;

    private void Awake()
    {
        baseColors = new Color[fadeRenderers.Length];
        for (int i = 0; i < fadeRenderers.Length; i++)
        {
            if (fadeRenderers[i] != null) baseColors[i] = fadeRenderers[i].color;
        }
    }

    private void Start()
    {
        bool isArea09 = GameSession.HasValidArea() && GameSession.SelectedArea != null
            && GameSession.SelectedArea.areaNumber == TargetAreaNumber;

        if (!isArea09)
        {
            gameObject.SetActive(false);
            foreach (GameObject go in areaGatedObjects)
            {
                if (go != null) go.SetActive(false);
            }
            return;
        }

        ApplyAlpha(0f);
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

    /// <summary>
    /// Area10ボスラッシュ専用：Area9ボスが有効になった瞬間に外部（Area10BossRushController）から直接呼ぶ。
    /// 通常のStart()/OnStageStarted経路（「セッション全体で1つのAreaに固定」という前提）を経由せず、
    /// VS演出のポーズ待ち（RevealSequence）も行わずに即座にフェードインを開始する
    /// （Area10ではVS演出自体が出ないため、ポーズ待ちループが永久に終わらなくなるのを避けるため）。
    /// </summary>
    public void ActivateForBossRush()
    {
        gameObject.SetActive(true);
        foreach (GameObject go in areaGatedObjects)
            if (go != null) go.SetActive(true);
        foreach (GameObject go in hideOnRevealObjects)
            if (go != null) go.SetActive(false);

        if (!revealed)
        {
            revealed = true;
            ApplyAlpha(0f);
            StartCoroutine(FadeInForBossRush());
        }
    }

    /// <summary>Area10ボスラッシュ専用：次のボスに切り替わる時に呼ぶ。</summary>
    public void DeactivateForBossRush()
    {
        StopAllCoroutines();
        gameObject.SetActive(false);
        foreach (GameObject go in areaGatedObjects)
            if (go != null) go.SetActive(false);
        revealed = false;
    }

    private IEnumerator FadeInForBossRush()
    {
        float t = 0f;
        while (t < fadeInDuration)
        {
            t += Time.deltaTime;
            ApplyAlpha(Mathf.Clamp01(t / fadeInDuration));
            yield return null;
        }
        ApplyAlpha(1f);
    }

    // ★Stage3開始直後はボス紹介のVS演出（PauseManager.SetPauseBlocked(true)で囲まれる）が
    //   挟まることがある。ここでフェードを即開始すると、演出中に完了してしまい、演出が終わって
    //   実際に画面が見える頃には既にフル表示済み＝「急に表示された」ように見えてしまう。
    //   そのため、演出が始まる→終わるのを待ってから、フェードインを開始する
    private IEnumerator RevealSequence()
    {
        while (PauseManager.Instance == null || !PauseManager.Instance.IsPauseBlocked)
        {
            yield return null;
        }

        while (PauseManager.Instance != null && PauseManager.Instance.IsPauseBlocked)
        {
            yield return null;
        }

        foreach (GameObject go in hideOnRevealObjects)
        {
            if (go != null) go.SetActive(false);
        }

        float t = 0f;
        while (t < fadeInDuration)
        {
            t += Time.deltaTime;
            ApplyAlpha(Mathf.Clamp01(t / fadeInDuration));
            yield return null;
        }
        ApplyAlpha(1f);
    }

    private void ApplyAlpha(float alpha)
    {
        VisibilityAlpha = alpha;
        for (int i = 0; i < fadeRenderers.Length; i++)
        {
            if (fadeRenderers[i] == null) continue;
            Color c = baseColors[i];
            c.a = baseColors[i].a * alpha;
            fadeRenderers[i].color = c;
        }
    }
}
