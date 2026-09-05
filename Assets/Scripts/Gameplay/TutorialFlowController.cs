using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 05_Gameシーンで動くチュートリアル進行管理。
/// GameSession.StartInTutorialMode=trueで本シーンに入った場合のみ動作し、
/// 通常のEnemySpawnerを一時停止した状態でカードを順番に表示する。
/// 「線」セクションのみ、実際に安全な練習弾を発生させ、反射＋円成立の両方を検知してから次へ進めるようにする
/// （見せ方の相談で合意した「ハイブリッド型」：テキストカード＋線セクションだけ実践パート）。
/// Play前にInspectorでStepsとPractice関連の参照を全て設定すること。
/// </summary>
public class TutorialFlowController : MonoBehaviour
{
    [System.Serializable]
    public class TutorialStep
    {
        [TextArea(1, 2)] public string title;
        [TextArea(3, 8)] public string body;
        public Sprite illustration;
        [Tooltip("ONの場合、このステップは「次へ」ボタンが最初は押せない状態になり、hints全ての条件を順番通りにクリアするまで進めない")]
        public bool requiresPractice;
        [Tooltip("練習中に順番に表示するヒント文言。何をすると次に進むかはステップごとにコード側で判定する（順番はズラせない＝先のヒントの条件を先に満たしても無効）")]
        [TextArea(1, 3)] public string[] hints;
    }

    [Header("Enemy Spawner（チュートリアル中は無効化して通常湧きを止める）")]
    [SerializeField] private EnemySpawner enemySpawner;
    [Tooltip("PixelDancer/Floorの表示はEnemySpawnerのStage1イントロ演出（OnCutInComplete）で初めて有効化される仕様。" +
        "EnemySpawnerを止めるとこれも走らないため、ここで代わりに直接呼ぶ")]
    [SerializeField] private StageIntroController stageIntroController;

    [Header("UI References")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private TextMeshProUGUI titleText;
    [Tooltip("タイトル下のアクセント下線。タイトル文字列の幅に合わせて毎回自動でリサイズする")]
    [SerializeField] private RectTransform titleUnderlineRect;
    [SerializeField] private TextMeshProUGUI bodyText;
    [SerializeField] private Image illustrationImage;
    [SerializeField] private Button nextButton;
    [SerializeField] private Button backButton;
    [SerializeField] private Button skipButton;
    [SerializeField] private TextMeshProUGUI pageIndicatorText;
    [SerializeField] private GameObject practiceHintRoot;
    [SerializeField] private TextMeshProUGUI practiceHintText;
    [Tooltip("カード全体（タイトル・本文・ボタン等）のフェードイン制御用")]
    [SerializeField] private CanvasGroup cardCanvasGroup;
    [Tooltip("ヒント文言だけのフェードイン制御用")]
    [SerializeField] private CanvasGroup hintCanvasGroup;
    [Tooltip("フェードインにかける秒数")]
    [SerializeField] private float fadeInDuration = 2f;

    [Header("Steps（Play前にInspectorで全項目編集可能）")]
    [SerializeField] private TutorialStep[] steps;

    [Header("Practice（線セクション用の安全な練習弾）")]
    [Tooltip("練習用の的。EnemyDamageReceiverが付いたプレハブを指定（反射弾のダメージ検知に使う）")]
    [SerializeField] private GameObject practiceDummyPrefab;
    [Tooltip("的のスプライト/スケールの取得元。通常のスポーン処理（EnemySpawner.ApplyEnemyData）を経由しないと" +
        "SpriteRendererにスプライトが入らないため、ここから直接コピーする。practiceDummyPrefabの元になったEnemyDataを指定すること")]
    [SerializeField] private EnemyData practiceDummyVisualData;
    [SerializeField] private Transform practiceDummySpawnPoint;
    [Tooltip("練習用の弾（既存のEnemyBulletプレハブを流用）")]
    [SerializeField] private EnemyBullet practiceBulletPrefab;
    [SerializeField] private Vector2 practiceBulletDirection = Vector2.down;
    [Tooltip("ステップ表示から最初の1発が出るまでの待機秒数（文章を読む時間を確保する）")]
    [SerializeField] private float practiceBulletInitialDelay = 2f;
    [SerializeField] private float practiceBulletInterval = 2.5f;
    [SerializeField] private float practiceBulletSpeed = 2f;
    [SerializeField] private float practiceBulletLifeTime = 10f;

    [Header("SE")]
    [Tooltip("ヒント通りの操作ができた時に鳴らすSE（反射成功／円成立それぞれで鳴る）")]
    [SerializeField] private AudioClip hintAdvanceSe;
    [Tooltip("次へ/戻る/スキップボタンを押した時に鳴らすSE")]
    [SerializeField] private AudioClip buttonClickSe;
    [SerializeField] private AudioSource audioSource;

    [Header("Next Button Blink（押せるようになった時に点滅）")]
    [Tooltip("点滅時の色。この色はボタン背景画像(BackBg、水色〜青グラデーション)に乗算されるため、" +
        "緑・黄色等の暖色系だと色相がぶつかって濁った色になる。青系の濃い色を推奨")]
    [SerializeField] private Color nextButtonBlinkColor = new Color(0.15f, 0.2f, 0.4f, 1f);
    [SerializeField] private float nextButtonBlinkInterval = 0.4f;

    private Image nextButtonImage;
    private Color nextButtonNormalColor;
    private Coroutine nextButtonBlinkCoroutine;

    private int currentIndex = -1;
    private int currentHintIndex = -1; // 現在表示中のヒントのインデックス（-1=練習不要ステップ）
    private readonly System.Collections.Generic.HashSet<int> completedPracticeSteps = new System.Collections.Generic.HashSet<int>();
    private GameObject activeDummy; // 1/10用（単体）
    private EnemyPart[] activeDummyParts;
    private Coroutine practiceBulletCoroutine;
    private Coroutine cardFadeCoroutine;
    private Coroutine hintFadeCoroutine;
    private readonly System.Collections.Generic.List<Coroutine> activeDummyHealCoroutines = new System.Collections.Generic.List<Coroutine>();

    // 2/10用（左右2体）
    private GameObject activeDummyLeft;
    private GameObject activeDummyRight;
    private Coroutine step1BulletCoroutine;
    private Game.Skills.SkillDefinition multiLineSkill;
    private bool multiLineSkillGranted;

    // 3/10用（単体・線の硬度とPenetrationの関係）
    private GameObject activeDummyStep2;
    private Coroutine step2BulletCoroutine;
    private SkillHUDManager skillHUDManager;
    private Game.Skills.SkillDefinition hardnessUpSkill;
    private bool step2HardnessSaved;
    private int step2SavedNormalHardness;

    // 4/10用（赤線について）
    private GameObject activeDummyStep3;
    private Coroutine step3BulletCoroutine;
    private bool step3HardnessSaved;
    private int step3SavedRedHardness;

    // 5/10用（線ゲージについて）
    private PaddleCostManager paddleCostManager;

    // 6/10用（2つの体力）
    private GameObject activeDummyStep5;
    private Coroutine step5BulletCoroutine;
    private FloorHealth floorHealth;
    private PixelDancerController pixelDancerController;
    private int step5TotalDamage;
    // 6/10・7/10共用（魂を扱う練習中のHP=1オーバーライド。両ステップにまたがって管理する）
    private bool soulPracticeOverridesActive;
    private int savedFloorMaxHP;
    private int savedDancerInitialHP;

    // 7/10用（魂の救出）
    private Coroutine step6SequenceCoroutine;

    // 8/10用（スローモーション）
    private GameObject activeDummyStep7;
    private Coroutine step7BulletCoroutine;
    private Coroutine step7SlowMotionHoldCoroutine;

    // 9/10用（スローモーションゲージ）
    private GameObject activeDummyStep8;
    private Coroutine step8BulletCoroutine;

    // 10/10用（中断メニュー）
    private Coroutine step9FinishCoroutine;

    // ================== Unity ライフサイクル ==================

    private void Awake()
    {
        // ★重要：Unityはシーン内の全AwakeをどのStartよりも先に完了させる保証があるため、
        // ここでEnemySpawnerを無効化すれば、EnemySpawner.Start()自体が発火しない
        // （再度enabled=trueにした時点で初めてStart()が走る）
        if (enemySpawner == null) enemySpawner = FindObjectOfType<EnemySpawner>();
        if (stageIntroController == null) stageIntroController = FindObjectOfType<StageIntroController>();

        // ★panelRootはScreen Space - OverlayのTutorialCanvas（メインHUDのScreen Space - CameraのCanvasより必ず手前）配下の
        // 全画面パネルで、raycastTargetがtrueのままだと画面全体のクリックを吸収してしまい、
        // 一時停止ボタン等メインHUD側のUIボタンが一切押せなくなる。見た目の暗転はそのまま活かし、クリックだけ透過させる
        if (panelRoot != null)
        {
            Image panelImage = panelRoot.GetComponent<Image>();
            if (panelImage != null) panelImage.raycastTarget = false;
        }

        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;

        // ★2/10で使う「マルチライン」スキル（線を同時に引ける本数+1）。Inspector設定不要でResourcesから直接読む
        multiLineSkill = Resources.Load<Game.Skills.SkillDefinition>("GameData/Skills/Skill_A7_MaxStrokesUp");

        // ★3/10で使う「ハードライン」スキル（線硬度アップ）。実際に取得させるのではなく、
        // HUDカードの表示レベルだけを直接書き換えて硬度2を視覚的に説明するために参照する
        hardnessUpSkill = Resources.Load<Game.Skills.SkillDefinition>("GameData/Skills/Skill_B1_HardnessUp");
        if (skillHUDManager == null) skillHUDManager = FindObjectOfType<SkillHUDManager>();

        // ★5/10で使う線ゲージ管理（消費・オーバーヒート検知用）
        if (paddleCostManager == null) paddleCostManager = FindObjectOfType<PaddleCostManager>();

        // ★6/10で使うFloor/PixelDancerのダメージ検知用
        if (floorHealth == null) floorHealth = FindObjectOfType<FloorHealth>();
        if (pixelDancerController == null) pixelDancerController = FindFirstObjectByType<PixelDancerController>();

        if (!GameSession.StartInTutorialMode)
        {
            if (panelRoot != null) panelRoot.SetActive(false);
            return;
        }

        // StartInTutorialModeはStart()で消費されfalseに戻るが、IsInTutorialは
        // チュートリアル終了までtrueのまま保つ（GameManager等が判定に使うため）
        GameSession.IsInTutorial = true;

        if (enemySpawner != null) enemySpawner.enabled = false;

        // ★EnemySpawnerが動かないためWaveTimerUIのゲージが常にtimeLimit<=0扱いで真っ暗になる。
        // 一時停止ボタン周りのネオンリングだけは満タン・初期色で表示させておく
        WaveTimerUI waveTimerUI = FindObjectOfType<WaveTimerUI>();
        if (waveTimerUI != null) waveTimerUI.SetTutorialNeutralDisplay(true);
    }

    private void Start()
    {
        if (!GameSession.StartInTutorialMode) return;
        GameSession.StartInTutorialMode = false; // 一度使ったら消費（多重起動防止）

        // ★PixelDancer/Floorは本来EnemySpawnerのStage1イントロ演出中に初めて表示される仕様。
        // EnemySpawnerを止めているためその演出が走らず、Player/Floorが非表示のままになってしまう。
        // イントロ演出そのものは不要なので、表示だけを行うメソッドを直接呼ぶ。
        // ★StageIntroControllerは[DefaultExecutionOrder(100)]でこのスクリプトより後にStart()が走る。
        // ここで即座に呼ぶと、直後に実行されるStageIntroController.Start()内のSetupInitialState()が
        // PixelDancer/Floorを再び非表示に戻してしまうため、1フレーム待ってから呼ぶ
        if (stageIntroController != null) StartCoroutine(RevealPlayerAndFloorNextFrame());

        if (nextButton != null) nextButton.onClick.AddListener(OnClickNext);
        if (backButton != null) backButton.onClick.AddListener(OnClickBack);
        if (skipButton != null) skipButton.onClick.AddListener(OnClickSkip);

        if (nextButton != null)
        {
            nextButtonImage = nextButton.GetComponent<Image>();
            if (nextButtonImage != null) nextButtonNormalColor = nextButtonImage.color;
        }

        StrokeManager.OnCircleFormedAnywhere += HandleCircleFormed;
        StrokeManager.OnActiveStrokeCountChanged += HandleActiveStrokeCountChanged;
        StrokeManager.OnStrokeCreated += HandleStrokeCreated;

        if (paddleCostManager != null)
        {
            paddleCostManager.OnLeftConsumed += HandleGaugeConsumed;
            paddleCostManager.OnRedConsumed += HandleGaugeConsumed;
            paddleCostManager.OnLeftDepleted += HandleGaugeDepleted;
            paddleCostManager.OnRedDepleted += HandleGaugeDepleted;
        }

        if (floorHealth != null) floorHealth.OnDamaged += HandleFloorDamaged;
        if (pixelDancerController != null) pixelDancerController.OnDamaged += HandlePixelDancerDamaged;
        if (pixelDancerController != null) pixelDancerController.OnRescued += HandleSoulRescued;
        if (pixelDancerController != null) pixelDancerController.OnFallSequenceEndedWithoutRescue += HandleSoulFallFailed;

        if (SlowMotionManager.Instance != null) SlowMotionManager.Instance.OnSlowMotionStarted += HandleSlowMotionStarted;
        if (SlowMotionManager.Instance != null) SlowMotionManager.Instance.OnDepleted += HandleSlowMotionDepleted;

        if (PauseManager.Instance != null) PauseManager.Instance.OnPauseStarted += HandlePauseMenuOpened;

        PreloadAllStepGlyphs();
        ShowStep(0);
    }

    /// <summary>
    /// ★文字化け対策：Steps内の全タイトル/本文/ヒント文字列をあらかじめDynamicフォントアトラスに
    /// 追加要求しておく。1文字ずつ表示時に追加させると（特にEditor拡張のようなタイミングでは）
    /// 反映されないことがあったため、Play中の最初のタイミングでまとめて先読みする
    /// </summary>
    private void PreloadAllStepGlyphs()
    {
        if (steps == null) return;
        var sb = new System.Text.StringBuilder();
        foreach (var step in steps)
        {
            if (step == null) continue;
            if (!string.IsNullOrEmpty(step.title)) sb.Append(step.title);
            if (!string.IsNullOrEmpty(step.body)) sb.Append(step.body);
            if (step.hints != null)
            {
                foreach (var hint in step.hints)
                {
                    if (!string.IsNullOrEmpty(hint)) sb.Append(hint);
                }
            }
        }

        TMPro.TMP_FontAsset font = titleText != null ? titleText.font : (bodyText != null ? bodyText.font : null);
        if (font != null && sb.Length > 0)
        {
            font.TryAddCharacters(sb.ToString());
        }
    }

    private void OnDestroy()
    {
        StrokeManager.OnCircleFormedAnywhere -= HandleCircleFormed;
        StrokeManager.OnActiveStrokeCountChanged -= HandleActiveStrokeCountChanged;
        StrokeManager.OnStrokeCreated -= HandleStrokeCreated;

        if (paddleCostManager != null)
        {
            paddleCostManager.OnLeftConsumed -= HandleGaugeConsumed;
            paddleCostManager.OnRedConsumed -= HandleGaugeConsumed;
            paddleCostManager.OnLeftDepleted -= HandleGaugeDepleted;
            paddleCostManager.OnRedDepleted -= HandleGaugeDepleted;
        }

        if (floorHealth != null) floorHealth.OnDamaged -= HandleFloorDamaged;
        if (pixelDancerController != null) pixelDancerController.OnDamaged -= HandlePixelDancerDamaged;
        if (pixelDancerController != null) pixelDancerController.OnRescued -= HandleSoulRescued;
        if (pixelDancerController != null) pixelDancerController.OnFallSequenceEndedWithoutRescue -= HandleSoulFallFailed;

        if (SlowMotionManager.Instance != null) SlowMotionManager.Instance.OnSlowMotionStarted -= HandleSlowMotionStarted;
        if (SlowMotionManager.Instance != null) SlowMotionManager.Instance.OnDepleted -= HandleSlowMotionDepleted;

        if (PauseManager.Instance != null) PauseManager.Instance.OnPauseStarted -= HandlePauseMenuOpened;
    }

    private IEnumerator RevealPlayerAndFloorNextFrame()
    {
        yield return null; // StageIntroController.Start()（DefaultExecutionOrder 100）が終わるのを待つ
        if (stageIntroController != null)
        {
            stageIntroController.OnCutInComplete();
            stageIntroController.RevealFloorInstant();
        }
    }

    // ================== ステップ進行 ==================

    /// <summary>ステップのタイトルをLocalizationManagerの現在言語で取得する。未登録時はfallback(日本語)を返す</summary>
    private string GetLocalizedStepTitle(int index, string fallback)
    {
        return Game.Localization.LocalizationManager.GetStatic($"tutorial.step{index}.title", fallback);
    }

    /// <summary>ステップの本文をLocalizationManagerの現在言語で取得する。未登録時はfallback(日本語)を返す</summary>
    private string GetLocalizedStepBody(int index, string fallback)
    {
        return Game.Localization.LocalizationManager.GetStatic($"tutorial.step{index}.body", fallback);
    }

    /// <summary>ステップのヒント文言をLocalizationManagerの現在言語で取得する。未登録時はfallback(日本語)を返す</summary>
    private string GetLocalizedStepHint(int index, int hintIndex, string fallback)
    {
        return Game.Localization.LocalizationManager.GetStatic($"tutorial.step{index}.hint{hintIndex}", fallback);
    }

    private void ShowStep(int index)
    {
        StopPractice();

        // ★6/10・7/10の両方から離れる時だけ、HP1/円救出禁止/ゲームオーバー禁止のオーバーライドを元に戻す。
        // 魂がまだ落下中の可能性があるため、即座にではなく安全になってから戻す（RestoreSoulPracticeOverrides内で待つ）
        if ((currentIndex == 5 || currentIndex == 6) && index != 5 && index != 6)
        {
            RestoreSoulPracticeOverrides();
        }

        if (steps == null || index < 0 || index >= steps.Length)
        {
            FinishTutorial();
            return;
        }

        currentIndex = index;
        var step = steps[index];
        string localizedTitle = GetLocalizedStepTitle(index, step.title);
        string localizedBody = GetLocalizedStepBody(index, step.body);

        if (panelRoot != null) panelRoot.SetActive(true);
        if (titleText != null) titleText.text = localizedTitle;
        if (titleUnderlineRect != null && titleText != null)
        {
            // タイトル文字列の実際の描画幅に合わせて下線の幅を揃える（左右に少し余白を持たせる）
            Vector2 preferred = titleText.GetPreferredValues(localizedTitle);
            titleUnderlineRect.sizeDelta = new Vector2(preferred.x + 16f, titleUnderlineRect.sizeDelta.y);
        }
        if (bodyText != null) bodyText.text = localizedBody;
        if (illustrationImage != null)
        {
            illustrationImage.sprite = step.illustration;
            illustrationImage.enabled = step.illustration != null;
        }
        if (backButton != null) backButton.interactable = index > 0;
        if (pageIndicatorText != null) pageIndicatorText.text = $"\n\n{index + 1} / {steps.Length}";

        StartCardFadeIn();

        if (step.requiresPractice && step.hints != null && step.hints.Length > 0)
        {
            // ★ヒント表示・弾の発射は毎回リセットして最初から出す（見た目上、常に「1/10に来た」通常の状態にする）。
            // 「次へ」のロックだけはクリア済みかどうかで決める（一度でもクリア済みなら、戻ってきても道を塞がない）
            bool alreadyCompleted = completedPracticeSteps.Contains(index);
            currentHintIndex = 0;
            SetNextButtonInteractable(alreadyCompleted);
            if (practiceHintRoot != null) practiceHintRoot.SetActive(true);
            if (practiceHintText != null) practiceHintText.text = GetLocalizedStepHint(index, 0, step.hints[0]);
            StartHintFadeIn();
            StartPractice();
        }
        else
        {
            currentHintIndex = -1;
            SetNextButtonInteractable(true);
            if (practiceHintRoot != null) practiceHintRoot.SetActive(false);
        }
    }

    /// <summary>
    /// 現在のヒントの条件を満たした時に呼ぶ。次のヒントへ進める、または全ヒント消化で完了扱いにする。
    /// stepIndex/expectedHintIndexで「今、そのステップのそのヒントを表示中か」を必ず確認してから呼ぶこと
    /// （先のヒントの条件を先に満たしても無効にするためのガード）
    /// </summary>
    private void AdvanceHint(int stepIndex, int expectedHintIndex)
    {
        if (currentIndex != stepIndex || currentHintIndex != expectedHintIndex) return;
        var hints = steps[currentIndex].hints;

        PlaySe(hintAdvanceSe);
        currentHintIndex++;

        if (currentHintIndex >= hints.Length)
        {
            completedPracticeSteps.Add(currentIndex);
            SetNextButtonInteractable(true);
            if (practiceHintRoot != null) practiceHintRoot.SetActive(false);
            StopPractice(fadeOutDummy: true); // ★全ヒットクリア時は的をその場でゆっくりフェードアウトさせる
            return;
        }

        if (practiceHintText != null) practiceHintText.text = GetLocalizedStepHint(currentIndex, currentHintIndex, hints[currentHintIndex]);
        StartHintFadeIn();

        // ★ヒントが切り替わるたびに、その新しいヒントに応じた敵/弾のセットアップを行う
        // （StartStep1Practice()等は「今表示中のヒントが何か」を見て敵を出す/出さないを判断するため）
        StartPractice();
    }

    private void SetNextButtonInteractable(bool value)
    {
        if (nextButton == null) return;
        nextButton.interactable = value;

        if (value)
        {
            if (nextButtonBlinkCoroutine == null)
                nextButtonBlinkCoroutine = StartCoroutine(NextButtonBlinkLoop());
        }
        else
        {
            if (nextButtonBlinkCoroutine != null)
            {
                StopCoroutine(nextButtonBlinkCoroutine);
                nextButtonBlinkCoroutine = null;
            }
            if (nextButtonImage != null) nextButtonImage.color = nextButtonNormalColor;
        }
    }

    private IEnumerator NextButtonBlinkLoop()
    {
        bool on = false;
        while (true)
        {
            on = !on;
            if (nextButtonImage != null)
                nextButtonImage.color = on ? nextButtonBlinkColor : nextButtonNormalColor;
            yield return new WaitForSeconds(nextButtonBlinkInterval);
        }
    }

    private void StartCardFadeIn()
    {
        if (cardCanvasGroup == null) return;
        if (cardFadeCoroutine != null) StopCoroutine(cardFadeCoroutine);
        cardCanvasGroup.alpha = 0f;
        cardFadeCoroutine = StartCoroutine(FadeCanvasGroup(cardCanvasGroup, fadeInDuration));
    }

    private void StartHintFadeIn()
    {
        if (hintCanvasGroup == null) return;
        if (hintFadeCoroutine != null) StopCoroutine(hintFadeCoroutine);
        hintCanvasGroup.alpha = 0f;
        hintFadeCoroutine = StartCoroutine(FadeCanvasGroup(hintCanvasGroup, fadeInDuration));
    }

    private IEnumerator FadeCanvasGroup(CanvasGroup cg, float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            cg.alpha = Mathf.Clamp01(t / duration);
            yield return null;
        }
        cg.alpha = 1f;
    }

    private void OnClickNext()
    {
        PlaySe(buttonClickSe);
        ShowStep(currentIndex + 1);
    }

    private void OnClickBack()
    {
        if (currentIndex <= 0) return;
        PlaySe(buttonClickSe);
        ShowStep(currentIndex - 1);
    }

    private void OnClickSkip()
    {
        // ★チュートリアル全体の強制終了ではなく、今のステップの練習未クリアでも次のステップへ進む機能
        PlaySe(buttonClickSe);
        ShowStep(currentIndex + 1);
    }

    private void PlaySe(AudioClip clip, float volumeMultiplier = 1f)
    {
        if (clip == null || audioSource == null) return;
        float masterVolume = SoundSettingsManager.Instance != null ? SoundSettingsManager.Instance.SEVolume : 1f;
        audioSource.PlayOneShot(clip, volumeMultiplier * masterVolume);
    }

    private void FinishTutorial()
    {
        StopPractice();
        if (panelRoot != null) panelRoot.SetActive(false);
        TutorialProgress.MarkSeen();

        // このシーン滞在中はEnemySpawnerを無効のままにし、03_AreaSelectへ戻って
        // 通常通りエリアを選び直してもらう（中断したAreaのゲームプレイをその場で再開はしない）
        StartCoroutine(ReturnToAreaSelect());
    }

    private IEnumerator ReturnToAreaSelect()
    {
        yield return new WaitForSeconds(0.3f);

        // ★AreaSelectManager.FadeInOnStart()と対になるよう、黒画面へのフェードアウトを挟んでから遷移する
        // （TitleMenu.FadeOutAndLoadScene()と同じ構成：最前面の全画面黒Imageをalpha 0→1）
        GameObject fadeObj = new GameObject("TutorialFadeOut");
        Canvas fadeCanvas = fadeObj.AddComponent<Canvas>();
        fadeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        fadeCanvas.sortingOrder = 9999;

        UnityEngine.UI.CanvasScaler scaler = fadeObj.AddComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        GameObject imageObj = new GameObject("FadeImage");
        imageObj.transform.SetParent(fadeObj.transform, false);

        UnityEngine.UI.Image fadeImage = imageObj.AddComponent<UnityEngine.UI.Image>();
        fadeImage.color = new Color(0f, 0f, 0f, 0f);

        RectTransform rectTransform = imageObj.GetComponent<RectTransform>();
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.sizeDelta = Vector2.zero;

        float duration = 0.5f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            fadeImage.color = new Color(0f, 0f, 0f, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        UnityEngine.SceneManagement.SceneManager.LoadScene("03_AreaSelect");
    }

    // ================== Practice（線セクション） ==================

    private void StartPractice()
    {
        switch (currentIndex)
        {
            case 0: StartStep0Practice(); break;
            case 1: StartStep1Practice(); break;
            case 2: StartStep2Practice(); break;
            case 3: StartStep3Practice(); break;
            case 4: StartStep4Practice(); break;
            case 5: StartStep5Practice(); break;
            case 6: StartStep6Practice(); break;
            case 7: StartStep7Practice(); break;
            case 8: StartStep8Practice(); break;
            case 9: StartStep9Practice(); break;
        }
    }

    /// <summary>
    /// 練習専用の的を1体生成する共通処理（スプライト/スケール注入・HPバー非表示・レイヤー調整・HP9999維持）。
    /// practiceDummyPrefab自体や他Areaの敵には一切触れない（生成したインスタンスだけを操作する）
    /// </summary>
    private GameObject SpawnPracticeDummy(Vector3 position)
    {
        if (practiceDummyPrefab == null) return null;

        GameObject dummy = Instantiate(practiceDummyPrefab, position, Quaternion.identity);

        // ★通常の敵はEnemySpawner.SetLayerRecursivelyでEnemyレイヤーに設定される。
        // ここは直接Instantiateしているためprefab側のレイヤー（Default）のままになる点を揃えておく
        int enemyLayer = LayerMask.NameToLayer("Enemy");
        if (enemyLayer != -1)
        {
            foreach (Transform t in dummy.GetComponentsInChildren<Transform>(true))
            {
                t.gameObject.layer = enemyLayer;
            }
        }

        // ★通常の敵はEnemySpawner.ApplyEnemyData()がSpriteRendererにスプライトを注入して初めて見える仕様。
        // ここは直接Instantiateしているため、その注入処理を素通りしてしまい何も表示されない。ここで代わりに行う
        if (practiceDummyVisualData != null)
        {
            var sr = dummy.GetComponent<SpriteRenderer>();
            if (sr != null && practiceDummyVisualData.sprite != null) sr.sprite = practiceDummyVisualData.sprite;
            dummy.transform.localScale = new Vector3(practiceDummyVisualData.spriteScale.x, practiceDummyVisualData.spriteScale.y, 1f);
        }

        // ★このインスタンスだけHPバー/デバッグテキストを非表示にする。
        // どちらもAwake()で表示用の子オブジェクトを生成してから即SetActiveする実装のため、
        // enabled=falseだけでは既に生成済みの表示までは隠せない。専用の非表示メソッドを使う
        var healthDisplay = dummy.GetComponent<EnemyHealthDisplay>();
        if (healthDisplay != null) healthDisplay.SetBarsVisible(false);
        var hpDebugText = dummy.GetComponent<EnemyHpDebugText>();
        if (hpDebugText != null) hpDebugText.SetRoutineDebugEnabled(false);

        // ★体力は9999にした上で、万が一削れても詰まないよう定期的に全回復させ続ける
        var dummyStats = dummy.GetComponent<EnemyStats>();
        if (dummyStats != null)
        {
            dummyStats.ApplyMaxHp(9999);
            activeDummyHealCoroutines.Add(StartCoroutine(DummyAutoHealLoop(dummyStats)));
        }

        return dummy;
    }

    private void StartStep0Practice()
    {
        if (practiceDummyPrefab != null && practiceDummySpawnPoint != null && activeDummy == null)
        {
            activeDummy = SpawnPracticeDummy(practiceDummySpawnPoint.position);

            // ★1/10 ヒント2「反射した弾を敵に当てたら」の判定
            var receiver = activeDummy.GetComponent<EnemyDamageReceiver>();
            if (receiver != null) receiver.OnReflectedBulletHit += HandleStep0HitEnemy;

            // WeakPoint System採用の的（ルートにコライダーが無く、子のEnemyPartが直接ダメージ処理する構造）は
            // EnemyDamageReceiver.OnReflectedBulletHitが発火しないため、EnemyPart側のイベントも合わせて拾う
            activeDummyParts = activeDummy.GetComponentsInChildren<EnemyPart>();
            foreach (var part in activeDummyParts)
            {
                part.OnHitWithDamage += HandleStep0HitEnemyFromPart;
            }
        }

        // ★ヒント0〜2（反射／敵に当てる／ジャスト反射）の間だけ弾を出す。ヒント3（円）には弾は不要。
        // ★既に発射ループが動いている場合は再起動しない（AdvanceHint()からヒント切替のたびに呼ばれるため、
        // 二重起動すると弾が倍速で出てしまう。既存ループはcurrentHintIndexを毎周チェックしているので放置でよい）
        if (practiceBulletCoroutine == null && practiceBulletPrefab != null && practiceDummySpawnPoint != null && currentHintIndex >= 0 && currentHintIndex < 3)
        {
            practiceBulletCoroutine = StartCoroutine(PracticeBulletLoop());
        }
    }

    private void StartStep1Practice()
    {
        // ★線は基本1本しか引けない仕様（StrokeManager.maxStrokes=1）。ヒント0の「もう1本引く」を
        // 達成できるようにするため、マルチラインスキルを1回だけ付与して同時2本にする（HUDにも反映される）
        if (!multiLineSkillGranted && multiLineSkill != null && Game.Skills.SkillManager.Instance != null)
        {
            Game.Skills.SkillManager.Instance.AddSkill(multiLineSkill);
            multiLineSkillGranted = true;
        }

        // ★2/10 ヒント0「線が消える前にもう1本引いたら」：敵は不要。StrokeManagerの本数変化を購読するだけ
        // ★2/10 ヒント1「同時に2つ反射したら」：左右に敵を配置し、同時に弾を発射する
        if (currentHintIndex == 1 && practiceDummySpawnPoint != null && activeDummyLeft == null && activeDummyRight == null)
        {
            Vector3 basePos = practiceDummySpawnPoint.position;
            activeDummyLeft = SpawnPracticeDummy(basePos + new Vector3(-2.5f, 0f, 0f));
            activeDummyRight = SpawnPracticeDummy(basePos + new Vector3(2.5f, 0f, 0f));

            step1BulletCoroutine = StartCoroutine(Step1DualBulletLoop());
        }
    }

    // ★3/10：敵の弾のPenetrationは常に2固定。線の硬度だけがヒントごとに変わる
    //   ①硬度1→貫通される　②硬度2→反射できる（HUD硬度レベル1表示）　③硬度1→1本目は貫通、2本目で反射
    private void StartStep2Practice()
    {
        if (activeDummyStep2 == null && practiceDummySpawnPoint != null)
        {
            activeDummyStep2 = SpawnPracticeDummy(practiceDummySpawnPoint.position);
        }

        if (!step2HardnessSaved && PaddleDrawer.Instance != null)
        {
            step2SavedNormalHardness = PaddleDrawer.Instance.NormalHardness;
            step2HardnessSaved = true;
        }

        if (step2BulletCoroutine != null)
        {
            StopCoroutine(step2BulletCoroutine);
            step2BulletCoroutine = null;
        }

        switch (currentHintIndex)
        {
            case 0:
                PaddleDrawer.Instance?.SetNormalHardness(1);
                SetHardnessHudLevel(0);
                step2BulletCoroutine = StartCoroutine(Step2Hint0BulletLoop());
                break;
            case 1:
                PaddleDrawer.Instance?.SetNormalHardness(2);
                SetHardnessHudLevel(1); // ★線硬度が2であることを視覚的に説明する
                step2BulletCoroutine = StartCoroutine(Step2Hint1BulletLoop());
                break;
            case 2:
                PaddleDrawer.Instance?.SetNormalHardness(1);
                SetHardnessHudLevel(0); // ★ヒント3に進んだらレベル0に戻す
                step2BulletCoroutine = StartCoroutine(Step2Hint2BulletLoop());
                break;
        }
    }

    private void SetHardnessHudLevel(int level)
    {
        if (skillHUDManager == null || hardnessUpSkill == null) return;
        // ★UpdateLevelWithBlink：Play中の本物のスキル取得時と同じ点滅演出（blinkCount/blinkInterval）でタイルを点灯させる。
        // レベルを下げる時（ヒント3でLv0に戻す時）は新規点灯タイルが無いため自動的に点滅せず即座に消灯する
        skillHUDManager.GetSkillCard(hardnessUpSkill.name)?.UpdateLevelWithBlink(level);
    }

    private EnemyBullet SpawnStep2Bullet()
    {
        EnemyBullet bullet = Instantiate(practiceBulletPrefab, activeDummyStep2.transform.position, Quaternion.identity);
        bullet.SetDirection(practiceBulletDirection.normalized);
        bullet.ApplyBullet(practiceBulletSpeed, practiceBulletLifeTime);
        bullet.SetDamage(0);

        // ★3/10は敵弾のPenetrationを常に2固定（仕様指定）
        BulletPenetration pen = bullet.GetComponent<BulletPenetration>();
        if (pen == null) pen = bullet.gameObject.AddComponent<BulletPenetration>();
        pen.SetPenetration(2);

        if (practiceDummyVisualData != null && practiceDummyVisualData.fireSE != null)
            PlaySe(practiceDummyVisualData.fireSE, practiceDummyVisualData.fireSEVolume);

        foreach (Collider2D col in activeDummyStep2.GetComponentsInChildren<Collider2D>())
        {
            if (col != null) bullet.SetOwnerCollisionIgnore(col, 0.15f);
        }

        return bullet;
    }

    // ★①硬度1・Penetration2：必ず貫通する→線が破られたら成功
    private IEnumerator Step2Hint0BulletLoop()
    {
        yield return new WaitForSeconds(practiceBulletInitialDelay);

        while (currentIndex == 2 && currentHintIndex == 0)
        {
            EnemyBullet bullet = SpawnStep2Bullet();
            bullet.OnPenetratedLine += () => AdvanceHint(2, 0);

            yield return new WaitUntil(() => bullet == null);
            yield return new WaitForSeconds(practiceBulletInterval);
        }
    }

    // ★②硬度2・Penetration2：貫通されず反射される→反射できたら成功
    private IEnumerator Step2Hint1BulletLoop()
    {
        yield return new WaitForSeconds(practiceBulletInitialDelay);

        while (currentIndex == 2 && currentHintIndex == 1)
        {
            EnemyBullet bullet = SpawnStep2Bullet();
            bullet.OnReflected += () => AdvanceHint(2, 1);

            yield return new WaitUntil(() => bullet == null);
            yield return new WaitForSeconds(practiceBulletInterval);
        }
    }

    // ★③硬度1・Penetration2：1本目の線を貫通した後（Penetrationが1に減り）、2本目の線で反射できたら成功
    private IEnumerator Step2Hint2BulletLoop()
    {
        yield return new WaitForSeconds(practiceBulletInitialDelay);

        while (currentIndex == 2 && currentHintIndex == 2)
        {
            EnemyBullet bullet = SpawnStep2Bullet();
            bool hasPenetratedOnce = false;

            bullet.OnPenetratedLine += () => hasPenetratedOnce = true;
            bullet.OnReflected += () =>
            {
                if (hasPenetratedOnce) AdvanceHint(2, 2);
            };

            yield return new WaitUntil(() => bullet == null);
            yield return new WaitForSeconds(practiceBulletInterval);
        }
    }

    // ★4/10：①赤線を引いたら成功（エネミー非表示）　②赤線で反射できたら成功（敵の配置は1/10と同じ）
    //   ③赤線硬度2・Penetration2の弾でも赤線で反射できたら成功（敵の配置は1/10と同じ）
    private void StartStep3Practice()
    {
        if (step3BulletCoroutine != null)
        {
            StopCoroutine(step3BulletCoroutine);
            step3BulletCoroutine = null;
        }

        switch (currentHintIndex)
        {
            case 0:
                // ★エネミー非表示。赤線を引いたかどうかはStrokeManager.OnStrokeCreatedの購読だけで判定する
                break;
            case 1:
                if (activeDummyStep3 == null && practiceDummySpawnPoint != null)
                {
                    activeDummyStep3 = SpawnPracticeDummy(practiceDummySpawnPoint.position);
                }
                step3BulletCoroutine = StartCoroutine(Step3BulletLoop(usePenetration: false));
                break;
            case 2:
                if (activeDummyStep3 == null && practiceDummySpawnPoint != null)
                {
                    activeDummyStep3 = SpawnPracticeDummy(practiceDummySpawnPoint.position);
                }

                if (!step3HardnessSaved && PaddleDrawer.Instance != null)
                {
                    step3SavedRedHardness = PaddleDrawer.Instance.RedHardness;
                    step3HardnessSaved = true;
                }
                PaddleDrawer.Instance?.SetRedHardness(2);

                step3BulletCoroutine = StartCoroutine(Step3BulletLoop(usePenetration: true));
                break;
        }
    }

    // ★5/10：①②ともにエネミー非表示。線を引く/オーバーヒートさせるだけの練習なので敵・弾は一切出さない
    private void StartStep4Practice()
    {
        // 判定はPaddleCostManagerのイベント購読（HandleGaugeConsumed/HandleGaugeDepleted）だけで行う
    }

    private void HandleGaugeConsumed()
    {
        if (currentIndex != 4 || currentHintIndex != 0) return;
        AdvanceHint(4, 0);
    }

    private void HandleGaugeDepleted()
    {
        if (currentIndex != 4 || currentHintIndex != 1) return;
        AdvanceHint(4, 1);
    }

    // ★6/10・7/10共用：Floor/PixelDancerのHPを1にオーバーライドする（未適用の場合のみ）
    private void ApplySoulPracticeHpOverrideIfNeeded()
    {
        if (soulPracticeOverridesActive) return;

        if (floorHealth != null)
        {
            savedFloorMaxHP = floorHealth.MaxHP;
            floorHealth.SetMaxHP(1);
        }
        if (pixelDancerController != null)
        {
            savedDancerInitialHP = pixelDancerController.InitialHP;
            pixelDancerController.SetInitialHP(1);
        }
        soulPracticeOverridesActive = true;
    }

    // ★6/10：Floor/PixelDancerどちらかが1発被弾（合計ダメージ1）したら成功。
    //   敵の配置・弾は1/10と全く同じ（狙いを変えたりはしない）。反射せず避ければFloorに、
    //   反射せずあえて受ければPixelDancerに当たる、という実際の当たり方の違いをそのまま利用する。
    //   このステップだけHPを1にして実際に魂を抜けさせるため、円救出とゲームオーバーも一時的に無効化する
    private void StartStep5Practice()
    {
        step5TotalDamage = 0;

        // ★HP1で1発の被弾＝即HP0にして、実際に魂が抜け出す演出を見せる。
        // このステップ・7/10は円救出/ゲームオーバー無効化を必要とする間、両ステップにまたがってHPオーバーライドを維持する
        ApplySoulPracticeHpOverrideIfNeeded();
        if (pixelDancerController != null)
        {
            pixelDancerController.RescueDisabled = true;
            pixelDancerController.GameOverOnFallDisabled = true;
        }

        if (activeDummyStep5 == null && practiceDummySpawnPoint != null)
        {
            activeDummyStep5 = SpawnPracticeDummy(practiceDummySpawnPoint.position);
        }

        if (step5BulletCoroutine == null)
        {
            step5BulletCoroutine = StartCoroutine(Step5BulletLoop());
        }
    }

    private IEnumerator Step5BulletLoop()
    {
        yield return new WaitForSeconds(practiceBulletInitialDelay);

        while (currentIndex == 5 && currentHintIndex == 0)
        {
            EnemyBullet bullet = Instantiate(practiceBulletPrefab, activeDummyStep5.transform.position, Quaternion.identity);
            bullet.SetDirection(practiceBulletDirection.normalized);
            bullet.ApplyBullet(practiceBulletSpeed, practiceBulletLifeTime);
            // ★このステップはFloor/PixelDancerに実際にダメージを与える必要があるため、他ステップと違いSetDamage(0)にしない

            if (practiceDummyVisualData != null && practiceDummyVisualData.fireSE != null)
                PlaySe(practiceDummyVisualData.fireSE, practiceDummyVisualData.fireSEVolume);

            foreach (Collider2D col in activeDummyStep5.GetComponentsInChildren<Collider2D>())
            {
                if (col != null) bullet.SetOwnerCollisionIgnore(col, 0.15f);
            }

            yield return new WaitUntil(() => bullet == null);
            yield return new WaitForSeconds(practiceBulletInterval);
        }
    }

    private void HandleFloorDamaged(int dmg)
    {
        if (currentIndex != 5 || currentHintIndex != 0) return;
        // ★このステップは「1発受けて実際に魂が抜け出す」のが目的のため、ここでは全快させない
        step5TotalDamage += dmg;
        if (step5TotalDamage >= 1) AdvanceHint(5, 0);
    }

    private void HandlePixelDancerDamaged(int dmg)
    {
        if (currentIndex != 5 || currentHintIndex != 0) return;
        // ★このステップは「1発受けて実際に魂が抜け出す」のが目的のため、ここでは全快させない
        step5TotalDamage += dmg;
        if (step5TotalDamage >= 1) AdvanceHint(5, 0);
    }

    private void RestoreSoulPracticeOverrides()
    {
        if (!soulPracticeOverridesActive) return;
        StartCoroutine(RestoreSoulPracticeOverridesWhenSafe());
    }

    private IEnumerator RestoreSoulPracticeOverridesWhenSafe()
    {
        // ★魂の落下判定シーケンスがまだ動いている間にフラグを戻すと、ゲームオーバー判定が復活して事故る可能性があるため、
        // シーケンスが完全に終わる（IsFallSequenceActive=falseになる）まで待ってから元の値に戻す。
        // ※GameOverOnFallDisabled中に画面外へ出た場合はisFallingがtrueのまま維持される仕様のため、
        // 　IsFallingではなくIsFallSequenceActiveを見る必要がある。
        // ★フラグをfalseにするのはこの待機の後（待機中に6/10・7/10へ戻られても二重保存にならないようにするため）
        while (pixelDancerController != null && pixelDancerController.IsFallSequenceActive)
        {
            yield return null;
        }

        soulPracticeOverridesActive = false;

        // ★SetMaxHP/SetInitialHPは最大値を上書きするだけで現在値は回復しない（下方向のクランプのみ）ため、
        // 6/10・7/10以外に移った後はHPが1や0のまま残らないよう、明示的に全快させる
        if (floorHealth != null)
        {
            floorHealth.SetMaxHP(savedFloorMaxHP);
            floorHealth.RestoreToFullHP();
        }
        if (pixelDancerController != null)
        {
            pixelDancerController.SetInitialHP(savedDancerInitialHP);
            pixelDancerController.RestoreToFullHP();
            pixelDancerController.RescueDisabled = false;
            pixelDancerController.GameOverOnFallDisabled = false;
        }
    }

    // ★7/10：PixelDancerをフェードインで登場させ、2秒後に自動でHPを0にして魂を抜けさせる。
    //   円救出できたら成功。救出できず画面外に落ちたら、7/10開始時（フェードインから）を自動でやり直す。
    //   敵・弾は使わず、救出の練習だけに集中させる（元の「敵の配置は1/10と同じ」仕様から変更）
    private void StartStep6Practice()
    {
        ApplySoulPracticeHpOverrideIfNeeded();
        if (pixelDancerController != null)
        {
            pixelDancerController.RescueDisabled = false; // ★7/10は6/10と逆で円救出を有効にする
            pixelDancerController.GameOverOnFallDisabled = true;
        }

        if (step6SequenceCoroutine == null)
        {
            step6SequenceCoroutine = StartCoroutine(Step6IntroSequence());
        }
    }

    private IEnumerator Step6IntroSequence()
    {
        if (pixelDancerController != null)
        {
            yield return pixelDancerController.SilentResetAndFadeIn(1f);
        }

        yield return new WaitForSeconds(2f);

        step6SequenceCoroutine = null;
        if (currentIndex != 6 || currentHintIndex != 0) yield break;

        pixelDancerController?.ApplyExplosionDamage(1);
    }

    private void HandleSoulRescued()
    {
        if (currentIndex != 6 || currentHintIndex != 0) return;
        AdvanceHint(6, 0);
    }

    private void HandleSoulFallFailed()
    {
        if (currentIndex != 6 || currentHintIndex != 0) return;
        // ★救出に失敗して画面外に落ちた。7/10開始時（フェードインから）を自動でやり直す
        if (step6SequenceCoroutine == null)
        {
            step6SequenceCoroutine = StartCoroutine(Step6IntroSequence());
        }
    }

    // ★8/10：スローモーションを実行したら成功。敵の配置は1/10と同じ（成功判定には無関係だが、雰囲気作りのため出す）
    private void StartStep7Practice()
    {
        if (activeDummyStep7 == null && practiceDummySpawnPoint != null)
        {
            activeDummyStep7 = SpawnPracticeDummy(practiceDummySpawnPoint.position);
        }

        if (step7BulletCoroutine == null)
        {
            step7BulletCoroutine = StartCoroutine(Step7BulletLoop());
        }
    }

    private IEnumerator Step7BulletLoop()
    {
        yield return new WaitForSeconds(practiceBulletInitialDelay);

        while (currentIndex == 7 && currentHintIndex == 0)
        {
            EnemyBullet bullet = Instantiate(practiceBulletPrefab, activeDummyStep7.transform.position, Quaternion.identity);
            bullet.SetDirection(practiceBulletDirection.normalized);
            bullet.ApplyBullet(practiceBulletSpeed, practiceBulletLifeTime);
            bullet.SetDamage(0);

            if (practiceDummyVisualData != null && practiceDummyVisualData.fireSE != null)
                PlaySe(practiceDummyVisualData.fireSE, practiceDummyVisualData.fireSEVolume);

            foreach (Collider2D col in activeDummyStep7.GetComponentsInChildren<Collider2D>())
            {
                if (col != null) bullet.SetOwnerCollisionIgnore(col, 0.15f);
            }

            yield return new WaitUntil(() => bullet == null);
            yield return new WaitForSeconds(practiceBulletInterval);
        }
    }

    private void HandleSlowMotionStarted()
    {
        if (currentIndex == 7 && currentHintIndex == 0)
        {
            if (step7SlowMotionHoldCoroutine == null)
            {
                step7SlowMotionHoldCoroutine = StartCoroutine(Step7SlowMotionHoldCheck());
            }
        }
        else if (currentIndex == 8 && currentHintIndex == 0)
        {
            // ★9/10 ヒント0「スローモーションゲージを消費したら」：発動＝ゲージ消費が始まるため、開始で成功とする
            AdvanceHint(8, 0);
        }
    }

    private void HandleSlowMotionDepleted()
    {
        if (currentIndex != 8 || currentHintIndex != 1) return;
        AdvanceHint(8, 1);
    }

    // ★スローモーションを開始してから1秒間、押しっぱなし（=ONのまま）が続いたら成功。
    // 1秒経つ前に切られたら失敗扱いで、次にまた開始した時にやり直しになる
    private IEnumerator Step7SlowMotionHoldCheck()
    {
        float elapsed = 0f;
        while (elapsed < 1f)
        {
            if (SlowMotionManager.Instance == null || !SlowMotionManager.Instance.IsSlowMotionActive)
            {
                step7SlowMotionHoldCoroutine = null;
                yield break;
            }
            elapsed += Time.deltaTime;
            yield return null;
        }

        step7SlowMotionHoldCoroutine = null;
        if (currentIndex != 7 || currentHintIndex != 0) yield break;
        AdvanceHint(7, 0);
    }

    // ★9/10：①スローモーションゲージを消費したら成功　②スローモーションがオーバーヒートしたら成功。敵の配置は1/10と同じ
    private void StartStep8Practice()
    {
        if (activeDummyStep8 == null && practiceDummySpawnPoint != null)
        {
            activeDummyStep8 = SpawnPracticeDummy(practiceDummySpawnPoint.position);
        }

        if (step8BulletCoroutine == null)
        {
            step8BulletCoroutine = StartCoroutine(Step8BulletLoop());
        }
    }

    private IEnumerator Step8BulletLoop()
    {
        yield return new WaitForSeconds(practiceBulletInitialDelay);

        while (currentIndex == 8 && currentHintIndex >= 0 && currentHintIndex < 2)
        {
            EnemyBullet bullet = Instantiate(practiceBulletPrefab, activeDummyStep8.transform.position, Quaternion.identity);
            bullet.SetDirection(practiceBulletDirection.normalized);
            bullet.ApplyBullet(practiceBulletSpeed, practiceBulletLifeTime);
            bullet.SetDamage(0);

            if (practiceDummyVisualData != null && practiceDummyVisualData.fireSE != null)
                PlaySe(practiceDummyVisualData.fireSE, practiceDummyVisualData.fireSEVolume);

            foreach (Collider2D col in activeDummyStep8.GetComponentsInChildren<Collider2D>())
            {
                if (col != null) bullet.SetOwnerCollisionIgnore(col, 0.15f);
            }

            yield return new WaitUntil(() => bullet == null);
            yield return new WaitForSeconds(practiceBulletInterval);
        }
    }

    // ★10/10（最終ステップ）：中断メニューを開いたら、通常のNextボタン待ちフローには入らず、
    // 3秒間表示維持 → 「チュートリアル完了！」を3秒間表示 → フェードアウト(2秒) → 終了、を全自動で行う
    private void StartStep9Practice()
    {
        // ★判定はPauseManager.OnPauseStartedの購読（HandlePauseMenuOpened）だけで行う。ここでは何もしない
    }

    private void HandlePauseMenuOpened()
    {
        if (currentIndex != 9 || currentHintIndex != 0) return;
        if (step9FinishCoroutine != null) return;
        step9FinishCoroutine = StartCoroutine(Step9FinishSequence());
    }

    private IEnumerator Step9FinishSequence()
    {
        // ★ポーズ中はTime.timeScale=0のため、WaitForSecondsではなくWaitForSecondsRealtimeで待つ
        yield return new WaitForSecondsRealtime(3f);

        if (PauseManager.Instance != null && PauseManager.Instance.IsPaused)
        {
            PauseManager.Instance.Resume();
        }

        if (practiceHintText != null) practiceHintText.text = Game.Localization.LocalizationManager.GetStatic("tutorial.complete", "チュートリアル完了！");
        if (practiceHintRoot != null) practiceHintRoot.SetActive(true);
        if (hintCanvasGroup != null) hintCanvasGroup.alpha = 0f;
        StartHintFadeIn();

        yield return new WaitForSecondsRealtime(3f);

        yield return StartCoroutine(FadeOutCardAndHint(2f));

        step9FinishCoroutine = null;
        FinishTutorial();
    }

    /// <summary>カード・ヒント両方のCanvasGroupを同時にフェードアウトさせる（10/10終了演出用）</summary>
    private IEnumerator FadeOutCardAndHint(float duration)
    {
        float startCard = cardCanvasGroup != null ? cardCanvasGroup.alpha : 0f;
        float startHint = hintCanvasGroup != null ? hintCanvasGroup.alpha : 0f;
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / duration);
            if (cardCanvasGroup != null) cardCanvasGroup.alpha = Mathf.Lerp(startCard, 0f, k);
            if (hintCanvasGroup != null) hintCanvasGroup.alpha = Mathf.Lerp(startHint, 0f, k);
            yield return null;
        }
        if (cardCanvasGroup != null) cardCanvasGroup.alpha = 0f;
        if (hintCanvasGroup != null) hintCanvasGroup.alpha = 0f;
    }

    private void HandleStrokeCreated(Stroke stroke)
    {
        if (currentIndex != 3 || currentHintIndex != 0) return;
        if (stroke == null || stroke.Type != PaddleDot.LineType.RedAccel) return;
        AdvanceHint(3, 0);
    }

    private IEnumerator Step3BulletLoop(bool usePenetration)
    {
        yield return new WaitForSeconds(practiceBulletInitialDelay);
        int expectedHintIndex = usePenetration ? 2 : 1;

        while (currentIndex == 3 && currentHintIndex == expectedHintIndex)
        {
            EnemyBullet bullet = Instantiate(practiceBulletPrefab, activeDummyStep3.transform.position, Quaternion.identity);
            bullet.SetDirection(practiceBulletDirection.normalized);
            bullet.ApplyBullet(practiceBulletSpeed, practiceBulletLifeTime);
            bullet.SetDamage(0);

            if (usePenetration)
            {
                BulletPenetration pen = bullet.GetComponent<BulletPenetration>();
                if (pen == null) pen = bullet.gameObject.AddComponent<BulletPenetration>();
                pen.SetPenetration(2);
            }

            if (practiceDummyVisualData != null && practiceDummyVisualData.fireSE != null)
                PlaySe(practiceDummyVisualData.fireSE, practiceDummyVisualData.fireSEVolume);

            foreach (Collider2D col in activeDummyStep3.GetComponentsInChildren<Collider2D>())
            {
                if (col != null) bullet.SetOwnerCollisionIgnore(col, 0.15f);
            }

            // ★白線での反射は無視し、赤線で反射できた時だけ成功とする
            bullet.OnReflected += () =>
            {
                if (bullet.LastReflectedByStroke != null && bullet.LastReflectedByStroke.Type == PaddleDot.LineType.RedAccel)
                    AdvanceHint(3, expectedHintIndex);
            };

            yield return new WaitUntil(() => bullet == null);
            yield return new WaitForSeconds(practiceBulletInterval);
        }
    }

    private void StopPractice(bool fadeOutDummy = false)
    {
        if (practiceBulletCoroutine != null)
        {
            StopCoroutine(practiceBulletCoroutine);
            practiceBulletCoroutine = null;
        }

        if (step1BulletCoroutine != null)
        {
            StopCoroutine(step1BulletCoroutine);
            step1BulletCoroutine = null;
        }

        if (step2BulletCoroutine != null)
        {
            StopCoroutine(step2BulletCoroutine);
            step2BulletCoroutine = null;
        }

        if (step3BulletCoroutine != null)
        {
            StopCoroutine(step3BulletCoroutine);
            step3BulletCoroutine = null;
        }

        if (step5BulletCoroutine != null)
        {
            StopCoroutine(step5BulletCoroutine);
            step5BulletCoroutine = null;
        }

        if (step6SequenceCoroutine != null)
        {
            StopCoroutine(step6SequenceCoroutine);
            step6SequenceCoroutine = null;
        }

        if (step7BulletCoroutine != null)
        {
            StopCoroutine(step7BulletCoroutine);
            step7BulletCoroutine = null;
        }

        if (step7SlowMotionHoldCoroutine != null)
        {
            StopCoroutine(step7SlowMotionHoldCoroutine);
            step7SlowMotionHoldCoroutine = null;
        }

        if (step8BulletCoroutine != null)
        {
            StopCoroutine(step8BulletCoroutine);
            step8BulletCoroutine = null;
        }

        if (step9FinishCoroutine != null)
        {
            StopCoroutine(step9FinishCoroutine);
            step9FinishCoroutine = null;
        }

        foreach (var co in activeDummyHealCoroutines)
        {
            if (co != null) StopCoroutine(co);
        }
        activeDummyHealCoroutines.Clear();

        if (activeDummy != null)
        {
            var receiver = activeDummy.GetComponent<EnemyDamageReceiver>();
            if (receiver != null) receiver.OnReflectedBulletHit -= HandleStep0HitEnemy;

            if (activeDummyParts != null)
            {
                foreach (var part in activeDummyParts)
                {
                    if (part != null) part.OnHitWithDamage -= HandleStep0HitEnemyFromPart;
                }
                activeDummyParts = null;
            }

            if (fadeOutDummy) StartCoroutine(FadeOutAndDestroyDummy(activeDummy));
            else Destroy(activeDummy);
            activeDummy = null;
        }

        if (activeDummyLeft != null)
        {
            if (fadeOutDummy) StartCoroutine(FadeOutAndDestroyDummy(activeDummyLeft));
            else Destroy(activeDummyLeft);
            activeDummyLeft = null;
        }
        if (activeDummyRight != null)
        {
            if (fadeOutDummy) StartCoroutine(FadeOutAndDestroyDummy(activeDummyRight));
            else Destroy(activeDummyRight);
            activeDummyRight = null;
        }

        if (activeDummyStep2 != null)
        {
            if (fadeOutDummy) StartCoroutine(FadeOutAndDestroyDummy(activeDummyStep2));
            else Destroy(activeDummyStep2);
            activeDummyStep2 = null;
        }

        // ★3/10で一時的に変更した線の硬度・HUD表示を必ず元に戻す（シーン全体に効くグローバル設定のため）
        if (step2HardnessSaved)
        {
            PaddleDrawer.Instance?.SetNormalHardness(step2SavedNormalHardness);
            SetHardnessHudLevel(0);
            step2HardnessSaved = false;
        }

        if (activeDummyStep3 != null)
        {
            if (fadeOutDummy) StartCoroutine(FadeOutAndDestroyDummy(activeDummyStep3));
            else Destroy(activeDummyStep3);
            activeDummyStep3 = null;
        }

        if (activeDummyStep5 != null)
        {
            if (fadeOutDummy) StartCoroutine(FadeOutAndDestroyDummy(activeDummyStep5));
            else Destroy(activeDummyStep5);
            activeDummyStep5 = null;
        }

        if (activeDummyStep7 != null)
        {
            if (fadeOutDummy) StartCoroutine(FadeOutAndDestroyDummy(activeDummyStep7));
            else Destroy(activeDummyStep7);
            activeDummyStep7 = null;
        }

        if (activeDummyStep8 != null)
        {
            if (fadeOutDummy) StartCoroutine(FadeOutAndDestroyDummy(activeDummyStep8));
            else Destroy(activeDummyStep8);
            activeDummyStep8 = null;
        }

        // ★4/10で一時的に変更した赤線の硬度を必ず元に戻す（シーン全体に効くグローバル設定のため）
        if (step3HardnessSaved)
        {
            PaddleDrawer.Instance?.SetRedHardness(step3SavedRedHardness);
            step3HardnessSaved = false;
        }
    }

    /// <summary>練習完了など、通常のステップ切り替えとは別に「その場でゆっくり消す」演出用</summary>
    private IEnumerator FadeOutAndDestroyDummy(GameObject dummy)
    {
        const float duration = 2f;
        SpriteRenderer sr = dummy != null ? dummy.GetComponent<SpriteRenderer>() : null;

        if (sr != null)
        {
            Color c = sr.color;
            float elapsed = 0f;
            while (elapsed < duration && dummy != null)
            {
                elapsed += Time.deltaTime;
                c.a = Mathf.Lerp(1f, 0f, elapsed / duration);
                sr.color = c;
                yield return null;
            }
        }
        else
        {
            yield return new WaitForSeconds(duration);
        }

        if (dummy != null) Destroy(dummy);
    }

    private IEnumerator PracticeBulletLoop()
    {
        // 文章を読む時間を確保してから1発目を発射し、以降はintervalごとに発射し続ける
        yield return new WaitForSeconds(practiceBulletInitialDelay);

        // ★1/10のヒント0〜2（反射／敵に当てる／ジャスト反射）の間だけ弾を出し続ける。ヒント3（円）に進んだら止める
        while (currentIndex == 0 && currentHintIndex >= 0 && currentHintIndex < 3)
        {
            // ★オフセットは持たせず、常に的の位置ぴったりから発射する（見た目上「敵から弾が出る」形に固定）
            EnemyBullet bullet = Instantiate(practiceBulletPrefab, practiceDummySpawnPoint.position, Quaternion.identity);
            bullet.SetDirection(practiceBulletDirection.normalized);
            bullet.ApplyBullet(practiceBulletSpeed, practiceBulletLifeTime);
            // ★練習弾は威力0（的側のHPを操作するのではなく、弾自体を無害にする）
            bullet.SetDamage(0);

            // ★発射SE：practiceDummyVisualData（＝Minimo等、実際の敵のEnemyData）のfireSEをそのまま流用する
            if (practiceDummyVisualData != null && practiceDummyVisualData.fireSE != null)
                PlaySe(practiceDummyVisualData.fireSE, practiceDummyVisualData.fireSEVolume);

            // ★ヒント0「反射できたら」／ヒント2「ジャスト反射できたら」。
            // AdvanceHint側で「今そのヒントを表示中か」を必ず確認するので、両方常に購読してよい
            bullet.OnReflected += () => AdvanceHint(0, 0);
            bullet.OnJustReflect += () => AdvanceHint(0, 2);

            // ★EnemyShooterが実際の敵で使っているのと同じ仕組み：発射直後は自分自身（的）とは衝突しないようにする。
            // これが無いと、出現直後にまだ反射前の弾が的のコライダーをかすめてヒットSEが誤発火することがある
            if (activeDummy != null)
            {
                foreach (Collider2D col in activeDummy.GetComponentsInChildren<Collider2D>())
                {
                    if (col != null) bullet.SetOwnerCollisionIgnore(col, 0.15f);
                }
            }

            // ★次の弾は、今の弾が画面から消えてから出す（反射されて的に消される/寿命切れ/KillZoneで消える等）
            yield return new WaitUntil(() => bullet == null);

            yield return new WaitForSeconds(practiceBulletInterval);
        }
    }

    /// <summary>
    /// 練習用の的が万が一ダメージで削れても倒れないよう、定期的に体力を全回復させ続ける
    /// </summary>
    private IEnumerator DummyAutoHealLoop(EnemyStats stats)
    {
        while (stats != null)
        {
            stats.ApplyMaxHp(9999);
            yield return new WaitForSeconds(1f);
        }
    }

    // ★1/10 ヒント1「反射した弾を敵に当てたら」の判定（反射弾が的にヒットした時のみ発火する既存イベント）
    private void HandleStep0HitEnemy(int damage, bool isPowered) => AdvanceHint(0, 1);
    private void HandleStep0HitEnemyFromPart(EnemyBullet bullet, int damage) => AdvanceHint(0, 1);

    private void HandleCircleFormed()
    {
        // ★1/10 ヒント3「円で線を作ったら」の判定
        AdvanceHint(0, 3);
    }

    // ★2/10 ヒント0「線が消える前にもう1本引いたら」の判定（同時に2本以上になった瞬間）
    private void HandleActiveStrokeCountChanged(int count)
    {
        if (count >= 2) AdvanceHint(1, 0);
    }

    // ★2/10のタイトル通り「線の持続時間」がテーマ。同一フレームでの反射を要求するのは誤りで、
    // 正しくは「同じ1本の線が消えずに残っている間に、その線で2つの弾を反射できたか」を見る。
    // タイミングが多少ズレても、反射した線（Stroke）が両方とも同じであれば成立とする
    private Stroke step1ReflectStrokeLeft;
    private Stroke step1ReflectStrokeRight;

    private IEnumerator Step1DualBulletLoop()
    {
        yield return new WaitForSeconds(practiceBulletInitialDelay);

        while (currentIndex == 1 && currentHintIndex == 1)
        {
            // ★このペアの記録をリセット（前のペアの記録が誤って一致判定されないように）
            step1ReflectStrokeLeft = null;
            step1ReflectStrokeRight = null;

            EnemyBullet bulletLeft = SpawnStep1Bullet(activeDummyLeft);
            EnemyBullet bulletRight = SpawnStep1Bullet(activeDummyRight);

            // ★弾が複数回反射されても、その都度「直近どの線で反射されたか」を更新するだけなので問題ない
            // （多重反射を無視する特別なガードは不要。Stroke参照の一致だけで安全に判定できる）
            bulletLeft.OnReflected += () => HandleStep1BulletReflected(bulletLeft, isLeft: true);
            bulletRight.OnReflected += () => HandleStep1BulletReflected(bulletRight, isLeft: false);

            // ★次のペアは、今の弾が両方とも画面から自然に消えてから出す（1/10の弾ループと同じ考え方）
            yield return new WaitUntil(() => bulletLeft == null && bulletRight == null);

            yield return new WaitForSeconds(practiceBulletInterval);
        }
    }

    private EnemyBullet SpawnStep1Bullet(GameObject fromDummy)
    {
        EnemyBullet bullet = Instantiate(practiceBulletPrefab, fromDummy.transform.position, Quaternion.identity);
        // ★左右2体は中央からオフセットして配置されるため、固定方向（真下）だとプレイヤーに当たらない。
        // 実際のゲームプレイ（EnemyShooter.ComputeFinalDirection の TowardPlayer）と同じ方式でプレイヤーを狙う
        bullet.SetDirection(GetDirectionTowardPlayer(fromDummy.transform.position, practiceBulletDirection));
        bullet.ApplyBullet(practiceBulletSpeed, practiceBulletLifeTime);
        bullet.SetDamage(0);

        if (practiceDummyVisualData != null && practiceDummyVisualData.fireSE != null)
            PlaySe(practiceDummyVisualData.fireSE, practiceDummyVisualData.fireSEVolume);

        foreach (Collider2D col in fromDummy.GetComponentsInChildren<Collider2D>())
        {
            if (col != null) bullet.SetOwnerCollisionIgnore(col, 0.15f);
        }

        return bullet;
    }

    /// <summary>実際のゲームプレイと同じ方式（Playerタグ→無ければPixelDancerController）でプレイヤーを探し、その方向を返す</summary>
    private Vector2 GetDirectionTowardPlayer(Vector3 fromPos, Vector2 fallbackDirection)
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj == null)
        {
            PixelDancerController playerDancer = FindFirstObjectByType<PixelDancerController>();
            if (playerDancer != null) playerObj = playerDancer.gameObject;
        }

        if (playerObj != null)
        {
            Vector2 dir = ((Vector2)playerObj.transform.position - (Vector2)fromPos).normalized;
            if (dir.sqrMagnitude > 0.0001f) return dir;
        }

        return fallbackDirection.normalized;
    }

    private void HandleStep1BulletReflected(EnemyBullet bullet, bool isLeft)
    {
        if (currentIndex != 1 || currentHintIndex != 1) return;
        if (bullet == null) return;

        Stroke stroke = bullet.LastReflectedByStroke;
        if (isLeft) step1ReflectStrokeLeft = stroke;
        else step1ReflectStrokeRight = stroke;

        // ★左右どちらも同じ線（Stroke）で反射されていれば成立。タイミングは問わない
        if (step1ReflectStrokeLeft != null && step1ReflectStrokeLeft == step1ReflectStrokeRight)
        {
            AdvanceHint(1, 1);
        }
    }

#if UNITY_EDITOR
    // ★追加：Stepsに下書き文面を自動入力する。実行するたびに全件上書きされる。
    // 内容はあくまで下書きなので、実行後はInspectorで自由に編集してよい。
    [ContextMenu("Fill Default Steps (下書き文面を自動入力)")]
    private void FillDefaultSteps()
    {
        steps = new TutorialStep[]
        {
            new TutorialStep
            {
                title = "線を引いてみよう",
                body = "画面をなぞると線が引けます。\n敵の弾に当てると跳ね返してダメージを与えられます。\nタイミングよくジャスト反射すると、ダメージが強化されます。\n円を作る際は素早く引き、始点と終点を近づけると成功しやすいです。",
                requiresPractice = true,
                hints = new string[]
                {
                    "弾に向かって線を引く",
                    "反射した弾を敵に当てる",
                    "タイミング良く線を引き、ジャスト反射する",
                    "円になるように線を素早く引く",
                },
            },
            new TutorialStep
            {
                title = "線の本数と持続時間",
                body = "線はふだん1本しか引けません。\n「マルチライン」のスキルを取得すると、同時に引ける線の数が増えます。\n引いた線は時間経過で消えます。",
                requiresPractice = true,
                hints = new string[]
                {
                    "線が消える前に2本目の線を引く",
                    "1本の線で2つの弾を反射する",
                },
            },
            new TutorialStep
            {
                title = "線の硬度について",
                body = "線には「硬度」があります。\n弾の貫通力が線の硬度を上回ると、線は破られ、貫通します。\n「ハードライン」のスキルを取得すると、線の硬度が上がります。\n貫通した弾を、2本目の線で反射することができます。",
                requiresPractice = true,
                hints = new string[]
                {
                    "貫通力の高い弾に白線を引き、弾が線を貫通することを確認する",
                    "硬度が上がった白線で、弾を反射する",
                    "1本目の白線が破られたら、すぐに2本目の白線を引き弾を反射する",
                },
            },
            new TutorialStep
            {
                title = "白線と赤線の違い",
                body = "白線は線ゲージの消費が少なく、手軽に使うことができます。\n赤線は線ゲージの消費が多い代わりに、線の持続時間・弾の加速・線の硬度、いずれも白線を上回ります。\n状況に応じて使い分けましょう。",
                requiresPractice = true,
                hints = new string[]
                {
                    "長押しかダブルタップで、赤線を引く",
                    "赤線で弾を反射し、勢いよく弾が跳ね返るのを確認する",
                    "貫通力の高い弾を赤線で反射し、白線より赤線の硬度が高いことを確認する",
                },
            },
            new TutorialStep
            {
                title = "線ゲージについて",
                body = "線を引くとゲージを消費します。\n使い切るとオーバーヒートし、しばらく同じ色の線が引けなくなります。",
                requiresPractice = true,
                hints = new string[]
                {
                    "線を引いて線ゲージを消費する",
                    "線ゲージを使い切り、オーバーヒートを確認する",
                },
            },
            new TutorialStep
            {
                title = "2つの体力",
                body = "ピクセルダンサーとフロアにそれぞれ体力があります。\nどちらか片方がHP0になると、ピクセルダンサーが倒れて魂が抜け出します。",
                requiresPractice = true,
                hints = new string[]
                {
                    "敵の弾を反射せずダメージを受ける",
                },
            },
            new TutorialStep
            {
                title = "魂の救出",
                body = "HP0になると魂が画面下へ落ちていきます。\n画面外に落ちる前に、魂を円で囲むことで救出できます。\n救出成功すると体力が全回復します。",
                requiresPractice = true,
                hints = new string[]
                {
                    "魂を円で囲み、救出する",
                },
            },
            new TutorialStep
            {
                title = "スローモーション",
                body = "砂時計ボタンでスローモーションにできます。\n様々な活用方法があるので上手く活用しましょう。",
                requiresPractice = true,
                hints = new string[]
                {
                    "砂時計ボタンでスローモーションを使う",
                },
            },
            new TutorialStep
            {
                title = "スローモーションゲージ",
                body = "スローモーションを使うとスローモーションゲージを消費します。\n使い切るとオーバーヒートし、しばらくスローモーションが使えなくなります。",
                requiresPractice = true,
                hints = new string[]
                {
                    "スローモーションを使い、スローモーションゲージを消費する",
                    "スローモーションゲージを使い切り、オーバーヒートを確認する",
                },
            },
            new TutorialStep
            {
                title = "中断メニュー",
                body = "画面右上の一時停止ボタンをタップすると中断メニューが開きます。",
                requiresPractice = true,
                hints = new string[]
                {
                    "画面右上の一時停止ボタンをタップし、中断メニューを開く",
                },
            },
        };

        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log($"[TutorialFlowController] FillDefaultSteps: 下書き文面を{steps.Length}件Stepsに反映しました。内容はInspectorで自由に編集できます。", this);

        RegisterStepCharactersToFontAssetPermanently();
    }

    /// <summary>
    /// ★文字化け対策（本格版）：Steps内の全文字を、Editor時にNotoSansJP-Regular SDF.assetの
    /// 文字テーブルへ直接追加し、アセットファイルに保存する。Font Asset Creatorで手動登録するのと同じ効果。
    /// Play中のTryAddCharacters()は反映されなかったため、確実に永続化するこちらの方式に切り替えた
    /// </summary>
    private void RegisterStepCharactersToFontAssetPermanently()
    {
        var font = UnityEditor.AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>("Assets/Fonts/NotoSansJP-Regular SDF.asset");
        if (font == null)
        {
            Debug.LogError("[TutorialFlowController] NotoSansJP-Regular SDF.asset が見つかりませんでした。");
            return;
        }

        var sb = new System.Text.StringBuilder();
        foreach (var step in steps)
        {
            if (step == null) continue;
            if (!string.IsNullOrEmpty(step.title)) sb.Append(step.title);
            if (!string.IsNullOrEmpty(step.body)) sb.Append(step.body);
            if (step.hints != null)
            {
                foreach (var hint in step.hints)
                {
                    if (!string.IsNullOrEmpty(hint)) sb.Append(hint);
                }
            }
        }

        bool success = font.TryAddCharacters(sb.ToString(), out string missingCharacters);

        UnityEditor.EditorUtility.SetDirty(font);
        UnityEditor.AssetDatabase.SaveAssets();

        if (success)
        {
            Debug.Log("[TutorialFlowController] RegisterStepCharactersToFontAssetPermanently: 全文字を登録し、フォントアセットを保存しました。", this);
        }
        else
        {
            Debug.LogWarning($"[TutorialFlowController] RegisterStepCharactersToFontAssetPermanently: 一部の文字が追加できませんでした（アトラス容量不足の可能性）。未登録: {missingCharacters}", this);
        }
    }

    // ★追加：チュートリアル用のCanvas/パネル/テキスト/ボタンを自動生成する。
    // 何度実行しても安全（既存の"TutorialCanvas"があれば削除して作り直す）
    [ContextMenu("Setup Tutorial UI (Canvas以下を自動生成)")]
    private void SetupTutorialUI()
    {
        Transform existing = transform.Find("TutorialCanvas");
        if (existing != null) DestroyImmediate(existing.gameObject);

        GameObject canvasObj = new GameObject("TutorialCanvas", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(GraphicRaycaster));
        canvasObj.transform.SetParent(transform, false);
        var canvas = canvasObj.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        // ★05_Game本体のCanvas（m_SortingOrder: 1200）より必ず手前にする。
        // 500のままだと本体UIにクリックを奪われ、ボタンが反応しなくなる
        canvas.sortingOrder = 2000;
        var scaler = canvasObj.GetComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        // ★05_Game本体のCanvas（横持ち1920x1080・高さ基準）に合わせる。ここがズレるとカードサイズが破綻する
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 1f;

        // Panel（背景暗幕＋カード）
        GameObject panelObj = CreateUIObject("Panel", canvasObj.transform);
        var panelRect = panelObj.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.sizeDelta = Vector2.zero;
        var panelBg = panelObj.AddComponent<Image>();
        panelBg.color = new Color(0f, 0f, 0f, 0.35f);

        // Card（画面左側に固定する縦長パネル。的(SP_05)・PixelDancerは共に画面X座標の中央〜右寄りにいるため、
        // 左側に置けば上下位置に関係なく重ならない。実際のワールド座標とカメラ設定から計算して決めた配置。
        // ★既存のSkillHUDが画面左端0〜280pxを占有しているため、その右（X=300〜）から配置する）
        GameObject cardObj = CreateUIObject("Card", panelObj.transform);
        var cardRect = cardObj.GetComponent<RectTransform>();
        cardRect.anchorMin = new Vector2(0f, 0.5f);
        cardRect.anchorMax = new Vector2(0f, 0.5f);
        cardRect.pivot = new Vector2(0f, 0.5f);
        cardRect.sizeDelta = new Vector2(640, 720);
        cardRect.anchoredPosition = new Vector2(300f, 0f);
        var cardBg = cardObj.AddComponent<Image>();
        cardBg.color = new Color(0.12f, 0.12f, 0.16f, 0.9f);
        var cardCg = cardObj.AddComponent<CanvasGroup>();

        // ★背景の外周だけを縁取るネオン枠（内側は完全透過なので、テキストの可読性はcardBgの単色背景のまま維持される）
        // ★画像自体に透過マージンがあるため、Card全体にジャストフィットさせると実際のネオン管の枠線が
        //   Card背景(グレー)より内側に来てしまい、グレーが枠の外側にはみ出て見える。マージン分だけ大きく
        //   配置し、ネオン管の実際の枠線をCardの外周に一致させる。値はUnity Editor上でLeft/Right=-15,
        //   Top/Bottom=-20に手動調整した結果を反映したもの(sizeDelta.x = 15+15, sizeDelta.y = 20+20)。
        GameObject cardFrameObj = CreateUIObject("CardFrame", cardObj.transform);
        var cardFrameRect = cardFrameObj.GetComponent<RectTransform>();
        cardFrameRect.anchorMin = Vector2.zero;
        cardFrameRect.anchorMax = Vector2.one;
        cardFrameRect.sizeDelta = new Vector2(30f, 40f);
        cardFrameRect.anchoredPosition = Vector2.zero;
        var cardFrameImg = cardFrameObj.AddComponent<Image>();
        cardFrameImg.sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Tutorial/チュートリアルCard用ネオン枠3.png");
        cardFrameImg.type = Image.Type.Simple;
        cardFrameImg.preserveAspect = false;
        cardFrameImg.raycastTarget = false;

        // Title（太字＋下に黄色のアクセント下線を敷いて目立たせる）
        var titleObj = CreateTMPText("TitleText", cardObj.transform, 32, TextAlignmentOptions.Center);
        var titleRect = titleObj.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -20f);
        titleRect.sizeDelta = new Vector2(-40f, 70f);
        titleObj.GetComponent<TextMeshProUGUI>().fontStyle = FontStyles.Bold;

        GameObject titleUnderlineObj = CreateUIObject("TitleUnderline", cardObj.transform);
        var titleUnderlineObjRect = titleUnderlineObj.GetComponent<RectTransform>();
        titleUnderlineObjRect.anchorMin = new Vector2(0.5f, 1f);
        titleUnderlineObjRect.anchorMax = new Vector2(0.5f, 1f);
        titleUnderlineObjRect.pivot = new Vector2(0.5f, 1f);
        titleUnderlineObjRect.anchoredPosition = new Vector2(0f, -92f);
        titleUnderlineObjRect.sizeDelta = new Vector2(220f, 4f);
        var titleUnderlineImg = titleUnderlineObj.AddComponent<Image>();
        titleUnderlineImg.color = new Color(1f, 0.85f, 0.3f, 1f);

        // Illustration（デフォルトでは非表示。illustrationを設定したステップのみ、タイトル下に表示）
        GameObject illustObj = CreateUIObject("IllustrationImage", cardObj.transform);
        var illustRect = illustObj.GetComponent<RectTransform>();
        illustRect.anchorMin = new Vector2(0.5f, 1f);
        illustRect.anchorMax = new Vector2(0.5f, 1f);
        illustRect.pivot = new Vector2(0.5f, 1f);
        illustRect.anchoredPosition = new Vector2(0f, -100f);
        illustRect.sizeDelta = new Vector2(160f, 160f);
        var illustImg = illustObj.AddComponent<Image>();
        illustImg.preserveAspect = true;
        illustImg.enabled = false;

        // Body
        var bodyObj = CreateTMPText("BodyText", cardObj.transform, 26, TextAlignmentOptions.TopLeft);
        var bodyRect = bodyObj.GetComponent<RectTransform>();
        bodyRect.anchorMin = new Vector2(0f, 0f);
        bodyRect.anchorMax = new Vector2(1f, 1f);
        bodyRect.offsetMin = new Vector2(40f, 280f);
        // ★タイトル/下線と本文の間に1行分の空きを持たせる（-100 → -140）
        bodyRect.offsetMax = new Vector2(-40f, -140f);

        // Practice hint
        GameObject hintRootObj = CreateUIObject("PracticeHintRoot", cardObj.transform);
        var hintRootRect = hintRootObj.GetComponent<RectTransform>();
        hintRootRect.anchorMin = new Vector2(0f, 0f);
        hintRootRect.anchorMax = new Vector2(1f, 0f);
        hintRootRect.pivot = new Vector2(0.5f, 0f);
        hintRootRect.anchoredPosition = new Vector2(0f, 210f);
        // ★Left/Right=30相当(横方向ストレッチのsizeDelta.x = -(Left+Right))にして、PracticeHintTextの表示幅を狭くする
        hintRootRect.sizeDelta = new Vector2(-60f, 50f);
        var hintText = CreateTMPText("PracticeHintText", hintRootObj.transform, 24, TextAlignmentOptions.Center);
        var hintTextRect = hintText.GetComponent<RectTransform>();
        hintTextRect.anchorMin = Vector2.zero;
        hintTextRect.anchorMax = Vector2.one;
        hintTextRect.sizeDelta = Vector2.zero;
        hintText.GetComponent<TextMeshProUGUI>().color = new Color(1f, 0.85f, 0.3f, 1f);
        var hintCg = hintRootObj.AddComponent<CanvasGroup>();
        hintRootObj.SetActive(false);

        // Page indicator
        var pageObj = CreateTMPText("PageIndicatorText", cardObj.transform, 28, TextAlignmentOptions.Center);
        var pageRect = pageObj.GetComponent<RectTransform>();
        pageRect.anchorMin = new Vector2(0.5f, 0f);
        pageRect.anchorMax = new Vector2(0.5f, 0f);
        pageRect.pivot = new Vector2(0.5f, 0f);
        pageRect.anchoredPosition = new Vector2(0f, 160f);
        pageRect.sizeDelta = new Vector2(300f, 36f);

        // Buttons row（カード幅640に収まるよう小さめ）。文字ラベルではなく、中断画面と同じBackBg背景+
        // 絵柄アイコンに差し替える(戻る=既存BackIcon、次へ/スキップ=新規作成した専用アイコン)。
        // ★BackBg.pngの実際のアスペクト比(612:408≈1.5)に近づけるため、高さを広げる。
        //   BackBgはボタン自身のImage(=クリック判定を持つRectTransformそのもの)なので、
        //   このサイズを変えるだけで見た目とクリック判定は自動的に連動する。
        //   pivotが下端(0.5,0)のため、Y位置を30→20に下げつつ高さを90→100にすることで、
        //   上端の位置は変えずに下方向にだけ伸ばし、ネオン枠との余白を詰めている。
        Vector2 buttonSize = new Vector2(150f, 100f);
        Button backBtn = CreateButton("BackButton", cardObj.transform, "", new Vector2(-180f, 20f), buttonSize);
        Button nextBtn = CreateButton("NextButton", cardObj.transform, "", new Vector2(0f, 20f), buttonSize);
        Button skipBtn = CreateButton("SkipButton", cardObj.transform, "", new Vector2(180f, 20f), buttonSize);
        ApplyIconButtonVisual(backBtn.gameObject, "Assets/Art/中断画面/BackIcon.png");
        ApplyIconButtonVisual(nextBtn.gameObject, "Assets/Art/Tutorial/次へアイコン.png");
        ApplyIconButtonVisual(skipBtn.gameObject, "Assets/Art/Tutorial/スキップアイコン.png");

        // 参照アサイン
        panelRoot = panelObj;
        titleText = titleObj.GetComponent<TextMeshProUGUI>();
        bodyText = bodyObj.GetComponent<TextMeshProUGUI>();
        illustrationImage = illustImg;
        nextButton = nextBtn;
        backButton = backBtn;
        skipButton = skipBtn;
        pageIndicatorText = pageObj.GetComponent<TextMeshProUGUI>();
        practiceHintRoot = hintRootObj;
        practiceHintText = hintText.GetComponent<TextMeshProUGUI>();
        cardCanvasGroup = cardCg;
        hintCanvasGroup = hintCg;
        titleUnderlineRect = titleUnderlineObjRect;

        panelObj.SetActive(false);

        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log("[TutorialFlowController] SetupTutorialUI: UI一式を自動生成し、参照をアサインしました。", this);
    }

    private static GameObject CreateUIObject(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    private static GameObject CreateTMPText(string name, Transform parent, float fontSize, TextAlignmentOptions alignment)
    {
        GameObject go = CreateUIObject(name, parent);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = fontSize;
        tmp.alignment = alignment;
        tmp.color = Color.white;
        tmp.text = name;

        // ★AddComponent直後はtmp.fontがTMP_Settingsのデフォルトからまだ解決されていない可能性があるため、
        // 「tmp.fontがnullでなければ同期する」という間接的な対処ではなく、実際に使われているフォント
        // （SkillCardUI等と同じNotoSansJP-Regular SDF）を直接ロードしてfont・fontSharedMaterialを明示指定する
        var notoSansJP = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/NotoSansJP-Regular SDF.asset");
        if (notoSansJP != null)
        {
            tmp.font = notoSansJP;
            tmp.fontSharedMaterial = notoSansJP.material;
        }

        return go;
    }

    private static Button CreateButton(string name, Transform parent, string label, Vector2 anchoredPos, Vector2? size = null)
    {
        GameObject go = CreateUIObject(name, parent);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta = size ?? new Vector2(220f, 90f);

        var img = go.AddComponent<Image>();
        img.color = new Color(0.25f, 0.25f, 0.32f, 1f);
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;

        var labelObj = CreateTMPText("Label", go.transform, 26, TextAlignmentOptions.Center);
        var labelRect = labelObj.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.sizeDelta = Vector2.zero;
        labelObj.GetComponent<TextMeshProUGUI>().text = label;

        return btn;
    }

    /// <summary>
    /// CreateButton()で生成したボタンの見た目を、中断画面のBackButtonと同じ構成
    /// (BackBg背景+絵柄アイコン、文字ラベルなし)に差し替える。背景は3ボタンとも共通のBackBgで統一し、
    /// アイコン画像だけ引数で切り替える。
    /// </summary>
    private static void ApplyIconButtonVisual(GameObject buttonGo, string iconAssetPath)
    {
        var bgImg = buttonGo.GetComponent<Image>();
        if (bgImg != null)
        {
            bgImg.sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/中断画面/BackBg.png");
            bgImg.color = Color.white;
            bgImg.type = Image.Type.Simple;
            bgImg.preserveAspect = false;
        }

        // ★"戻る"等の文字ラベル(CreateButton内で"Label"という名前で生成される)は、
        //   絵柄アイコンだけを見せるため非表示にする(削除はしない)。
        var labelTf = buttonGo.transform.Find("Label");
        if (labelTf != null) labelTf.gameObject.SetActive(false);

        GameObject iconObj = CreateUIObject("Icon", buttonGo.transform);
        var iconRect = iconObj.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0.5f, 0.5f);
        iconRect.anchorMax = new Vector2(0.5f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.anchoredPosition = Vector2.zero;
        iconRect.sizeDelta = new Vector2(100f, 67f);

        var iconImg = iconObj.AddComponent<Image>();
        iconImg.sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(iconAssetPath);
        iconImg.preserveAspect = true;
        iconImg.raycastTarget = false;
    }
#endif
}
