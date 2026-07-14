using UnityEngine;

/// <summary>
/// GameSessionのAreaConfigからFarLayer・MidLayerスプライトとカメラ背景色を適用する。
/// Editor直接再生時はfallback設定を使用。
/// </summary>
[ExecuteAlways]
public class BackgroundManager : MonoBehaviour
{
    public static BackgroundManager Instance { get; private set; }

    [Header("Far Layer")]
    [SerializeField] private SpriteRenderer farLayer;
    [SerializeField] private FarLayerFade farLayerFade;
    [Tooltip("Editor直接Play時のフォールバック")]
    [SerializeField] private Sprite fallbackFarSprite;

    [Header("Mid Layer")]
    [SerializeField] private SpriteRenderer midLayer;
    [Tooltip("Editor直接Play時のフォールバック")]
    [SerializeField] private Sprite fallbackFogSprite;
    [Tooltip("AreaConfig.midLayerHideOnStage3がONの場合、Stage3切り替え時のMidLayerフェードアウト所要時間（秒）")]
    [SerializeField] private float midLayerFadeOutDuration = 1.0f;

    [Header("Extra Fog Slots")]
    [Tooltip("シーンに事前配置したExtraFogのSpriteRenderer（最大数分用意）")]
    [SerializeField] private SpriteRenderer[] extraFogSlots;

    [Header("Silhouette Layer")]
    [SerializeField] private SpriteRenderer silhouetteLayer;
    [Tooltip("Editor直接Play時のフォールバック")]
    [SerializeField] private Sprite fallbackSilhouetteSprite;

    [Header("Camera")]
    [SerializeField] private Camera targetCamera;

    private Sprite farSpriteB;
    private Vector3 farScaleB;
    private Vector3 farPositionB;
    private Sprite silhouetteSpriteB;
    private Vector3 silhouetteScaleB;
    private Vector3 silhouettePositionB;
    private SilhouetteFade silhouetteFade;
    private CloudCycleFade silhouetteCycleFade;
    private bool midLayerHideOnStage3Enabled;
    private Sprite[] silhouetteCyclePatterns;
    private Vector3[] silhouetteCycleOffsets;
    private float silhouetteCycleHoldDuration;
    private float silhouetteCycleFadeDuration;

    [Header("Preview (Editor Only)")]
    [Tooltip("SceneView確認用AreaConfig")]
    [SerializeField] private AreaConfig previewAreaConfig;
    [Tooltip("A=Stage1/2、B=Stage3のプレビュー切り替え")]
    [SerializeField] private bool previewStageB = false;
    [Tooltip("Stage3プレビュー中、Silhouette Cycle Patternsの何番目を表示するか（0始まり）。位置・サイズ確認用")]
    [SerializeField] private int previewCyclePatternIndex = 0;

#if UNITY_EDITOR
    private void Update()
    {
        if (Application.isPlaying) return;
        if (previewAreaConfig == null) return;

        if (silhouetteLayer != null)
        {
            if (previewStageB)
            {
                var cyclePatterns = previewAreaConfig.silhouetteCyclePatterns;
                var cycleOffsets = previewAreaConfig.silhouetteCycleOffsets;
                Vector3 offset = Vector3.zero;
                if (cyclePatterns != null && cyclePatterns.Length > 0)
                {
                    int idx = Mathf.Clamp(previewCyclePatternIndex, 0, cyclePatterns.Length - 1);
                    silhouetteLayer.sprite = cyclePatterns[idx];
                    if (cycleOffsets != null && idx < cycleOffsets.Length)
                        offset = cycleOffsets[idx];
                }
                else
                {
                    silhouetteLayer.sprite = previewAreaConfig.backgroundSilhouetteSpriteB;
                }
                silhouetteLayer.transform.localScale = previewAreaConfig.backgroundSilhouetteScaleB;
                silhouetteLayer.transform.localPosition = previewAreaConfig.backgroundSilhouettePositionB + offset;
            }
            else
            {
                silhouetteLayer.sprite = previewAreaConfig.backgroundSilhouetteSprite;
                silhouetteLayer.transform.localScale = previewAreaConfig.backgroundSilhouetteScaleA;
                silhouetteLayer.transform.localPosition = previewAreaConfig.backgroundSilhouettePositionA;
            }
        }

        if (farLayer != null)
        {
            if (previewStageB)
            {
                farLayer.sprite = previewAreaConfig.backgroundSpriteB;
                farLayer.transform.localScale = previewAreaConfig.backgroundSpriteBScale;
                farLayer.transform.localPosition = previewAreaConfig.backgroundSpriteBPosition;
            }
            else
            {
                farLayer.sprite = previewAreaConfig.backgroundSprite;
                farLayer.transform.localScale = Vector3.one;
                farLayer.transform.localPosition = Vector3.zero;
            }
        }

        if (midLayer != null)
        {
            midLayer.sprite = previewAreaConfig.backgroundFogSprite;
            midLayer.transform.localScale = previewAreaConfig.backgroundFogScale;
            midLayer.transform.localPosition = previewAreaConfig.backgroundFogPosition;

            Color mc = midLayer.color;
            mc.a = (previewStageB && previewAreaConfig.midLayerHideOnStage3) ? 0f : 1f;
            midLayer.color = mc;
        }
    }
#endif

    [Header("Particles")]
    [Tooltip("インデックス = areaNumber。各エリアのParticleSystemを設定（不要なエリアはNone）")]
    [SerializeField] private ParticleSystem[] areaParticles;

    [Header("Stage Intro")]
    [Tooltip("StageIntroControllerを使う場合ON — Start時のParticle自動起動を抑制しStageIntroControllerが制御する")]
    [SerializeField] private bool suppressParticleOnStart = false;
    private ParticleSystem activeAreaParticle;

    /// <summary>StageIntroControllerのStep3でParticleを起動する</summary>
    public void ActivateAreaParticle()
    {
        if (activeAreaParticle != null)
            activeAreaParticle.gameObject.SetActive(true);
    }

    public bool IsTransitioning =>
        (farLayerFade != null && farLayerFade.IsTransitioning) ||
        (silhouetteFade != null && silhouetteFade.IsTransitioning);

    private void Awake()
    {
        Instance = this;
    }

    private void OnEnable()
    {
        EnemySpawner.OnStageStarted += OnStageStarted;
    }

    private void OnDisable()
    {
        EnemySpawner.OnStageStarted -= OnStageStarted;
    }

    private void OnStageStarted(int stageIndex)
    {
        if (stageIndex >= 2)
        {
            if (farSpriteB != null)
            {
                if (farLayerFade != null)
                    farLayerFade.TransitionToSprite(farSpriteB, farScaleB, farPositionB);
                else if (farLayer != null)
                {
                    farLayer.sprite = farSpriteB;
                    farLayer.transform.localScale = farScaleB;
                    farLayer.transform.localPosition = farPositionB;
                }
            }
            if (silhouetteFade != null && silhouetteSpriteB != null)
            {
                silhouetteFade.TransitionToSprite(silhouetteSpriteB, silhouetteScaleB, silhouettePositionB);

                if (silhouetteCyclePatterns != null && silhouetteCyclePatterns.Length >= 2 && silhouetteCycleFade != null)
                    StartCoroutine(StartSilhouetteCycleAfterFade());
            }

            if (midLayerHideOnStage3Enabled && midLayer != null)
                StartCoroutine(FadeOutMidLayer());
        }
    }

    // SilhouetteFadeのフェード完了（スプライトB切り替え完了）を待ってから
    // CloudCycleFadeによるクロスフェード巡回を開始する。SilhouetteFade自身の
    // アルファ制御と競合しないよう、開始時にSilhouetteFadeを無効化する
    private System.Collections.IEnumerator StartSilhouetteCycleAfterFade()
    {
        yield return new WaitUntil(() => silhouetteFade == null || !silhouetteFade.IsTransitioning);
        if (silhouetteFade != null)
            silhouetteFade.enabled = false;
        if (silhouetteCycleFade != null)
            silhouetteCycleFade.StartCycle(silhouetteCyclePatterns, silhouetteCycleOffsets, silhouetteCycleHoldDuration, silhouetteCycleFadeDuration);
    }

    private System.Collections.IEnumerator FadeOutMidLayer()
    {
        float elapsed = 0f;
        float startAlpha = midLayer.color.a;
        while (elapsed < midLayerFadeOutDuration)
        {
            elapsed += Time.deltaTime;
            Color c = midLayer.color;
            c.a = Mathf.Lerp(startAlpha, 0f, elapsed / midLayerFadeOutDuration);
            midLayer.color = c;
            yield return null;
        }
        Color final = midLayer.color;
        final.a = 0f;
        midLayer.color = final;
    }

    private void Start()
    {
        if (targetCamera == null) targetCamera = Camera.main;

        foreach (var ps in areaParticles)
            if (ps != null) ps.gameObject.SetActive(false);

        foreach (var slot in extraFogSlots)
            if (slot != null) slot.gameObject.SetActive(false);

        if (GameSession.HasValidArea())
        {
            AreaConfig area = GameSession.SelectedArea;
            if (farLayer != null)
                farLayer.sprite = area.backgroundSprite;
            farSpriteB = area.backgroundSpriteB;
            farScaleB = area.backgroundSpriteBScale;
            farPositionB = area.backgroundSpriteBPosition;
            midLayerHideOnStage3Enabled = area.midLayerHideOnStage3;
            if (midLayer != null)
            {
                midLayer.sprite = area.backgroundFogSprite;
                midLayer.transform.localScale = area.backgroundFogScale;
                midLayer.transform.localPosition = area.backgroundFogPosition;
                Color mc = midLayer.color;
                mc.a = 1f;
                midLayer.color = mc;

                var fogScroll = midLayer.GetComponent<FogScroll>();
                var rainScroll = midLayer.GetComponent<RainScroll>();
                var steamScroll = midLayer.GetComponent<SteamScroll>();
                var groundFogScroll = midLayer.GetComponent<GroundFogScroll>();
                var driftScroll = midLayer.GetComponent<DriftScroll>();
                var vortexScroll = midLayer.GetComponent<VortexScroll>();
                switch (area.midLayerScrollMode)
                {
                    case AreaConfig.MidLayerScrollMode.Fog:
                        if (fogScroll != null) fogScroll.enabled = true;
                        if (rainScroll != null) rainScroll.enabled = false;
                        if (steamScroll != null) steamScroll.enabled = false;
                        if (groundFogScroll != null) groundFogScroll.enabled = false;
                        if (driftScroll != null) driftScroll.enabled = false;
                        if (vortexScroll != null) vortexScroll.enabled = false;
                        break;
                    case AreaConfig.MidLayerScrollMode.Rain:
                        if (fogScroll != null) fogScroll.enabled = false;
                        if (rainScroll != null) rainScroll.enabled = true;
                        if (steamScroll != null) steamScroll.enabled = false;
                        if (groundFogScroll != null) groundFogScroll.enabled = false;
                        if (driftScroll != null) driftScroll.enabled = false;
                        if (vortexScroll != null) vortexScroll.enabled = false;
                        break;
                    case AreaConfig.MidLayerScrollMode.Steam:
                        if (fogScroll != null) fogScroll.enabled = false;
                        if (rainScroll != null) rainScroll.enabled = false;
                        if (steamScroll != null) steamScroll.enabled = true;
                        if (groundFogScroll != null) groundFogScroll.enabled = false;
                        if (driftScroll != null) driftScroll.enabled = false;
                        if (vortexScroll != null) vortexScroll.enabled = false;
                        break;
                    case AreaConfig.MidLayerScrollMode.GroundFog:
                        if (fogScroll != null) fogScroll.enabled = false;
                        if (rainScroll != null) rainScroll.enabled = false;
                        if (steamScroll != null) steamScroll.enabled = false;
                        if (groundFogScroll != null) groundFogScroll.enabled = true;
                        if (driftScroll != null) driftScroll.enabled = false;
                        if (vortexScroll != null) vortexScroll.enabled = false;
                        break;
                    case AreaConfig.MidLayerScrollMode.Drift:
                        if (fogScroll != null) fogScroll.enabled = false;
                        if (rainScroll != null) rainScroll.enabled = false;
                        if (steamScroll != null) steamScroll.enabled = false;
                        if (groundFogScroll != null) groundFogScroll.enabled = false;
                        if (driftScroll != null) driftScroll.enabled = true;
                        if (vortexScroll != null) vortexScroll.enabled = false;
                        break;
                    case AreaConfig.MidLayerScrollMode.Vortex:
                        if (fogScroll != null) fogScroll.enabled = false;
                        if (rainScroll != null) rainScroll.enabled = false;
                        if (steamScroll != null) steamScroll.enabled = false;
                        if (groundFogScroll != null) groundFogScroll.enabled = false;
                        if (driftScroll != null) driftScroll.enabled = false;
                        if (vortexScroll != null) vortexScroll.enabled = true;
                        break;
                    case AreaConfig.MidLayerScrollMode.None:
                        if (fogScroll != null) fogScroll.enabled = false;
                        if (rainScroll != null) rainScroll.enabled = false;
                        if (steamScroll != null) steamScroll.enabled = false;
                        if (groundFogScroll != null) groundFogScroll.enabled = false;
                        if (driftScroll != null) driftScroll.enabled = false;
                        if (vortexScroll != null) vortexScroll.enabled = false;
                        break;
                }
            }

            if (area.extraFogLayers != null)
            {
                SpriteRenderer midSR = midLayer != null ? midLayer.GetComponent<SpriteRenderer>() : null;
                for (int i = 0; i < extraFogSlots.Length; i++)
                {
                    if (extraFogSlots[i] == null) continue;
                    if (i < area.extraFogLayers.Length && area.extraFogLayers[i].sprite != null)
                    {
                        var data = area.extraFogLayers[i];
                        extraFogSlots[i].gameObject.SetActive(true);
                        extraFogSlots[i].sprite = data.sprite;
                        extraFogSlots[i].transform.localPosition = data.position;
                        extraFogSlots[i].transform.localScale = data.scale;
                        if (midSR != null)
                        {
                            extraFogSlots[i].material = midSR.material;
                            extraFogSlots[i].sortingLayerID = midSR.sortingLayerID;
                            extraFogSlots[i].sortingOrder = midSR.sortingOrder + data.sortingOrderOffset;
                        }
                        var fog = extraFogSlots[i].GetComponent<FogScroll>();
                        if (fog != null) fog.SetScrollParameters(data.scrollSpeed, data.waveAmplitude, data.waveFrequency);
                    }
                }
            }

            if (silhouetteLayer != null)
            {
                silhouetteLayer.sprite = area.backgroundSilhouetteSprite;
                silhouetteLayer.transform.localScale = area.backgroundSilhouetteScaleA;
                silhouetteLayer.transform.localPosition = area.backgroundSilhouettePositionA;
                silhouetteSpriteB = area.backgroundSilhouetteSpriteB;
                silhouetteScaleB = area.backgroundSilhouetteScaleB;
                silhouettePositionB = area.backgroundSilhouettePositionB;
                silhouetteFade = silhouetteLayer.GetComponent<SilhouetteFade>();
                if (silhouetteFade != null)
                    silhouetteFade.SetAlwaysFullAlpha(area.silhouetteAlwaysFullAlpha);

                silhouetteCycleFade = silhouetteLayer.GetComponent<CloudCycleFade>();
                silhouetteCyclePatterns = area.silhouetteCyclePatterns;
                silhouetteCycleOffsets = area.silhouetteCycleOffsets;
                silhouetteCycleHoldDuration = area.silhouetteCycleHoldDuration;
                silhouetteCycleFadeDuration = area.silhouetteCycleFadeDuration;
                if (silhouetteCycleFade != null)
                    silhouetteCycleFade.StopCycle();
            }
            if (targetCamera != null)
                targetCamera.backgroundColor = area.backgroundColor;

            int idx = area.areaNumber;
            if (idx >= 0 && idx < areaParticles.Length && areaParticles[idx] != null)
            {
                activeAreaParticle = areaParticles[idx];
                if (!suppressParticleOnStart)
                    areaParticles[idx].gameObject.SetActive(true);
            }
        }
        else
        {
            if (farLayer != null && fallbackFarSprite != null)
                farLayer.sprite = fallbackFarSprite;
            if (midLayer != null && fallbackFogSprite != null)
                midLayer.sprite = fallbackFogSprite;
            if (silhouetteLayer != null && fallbackSilhouetteSprite != null)
                silhouetteLayer.sprite = fallbackSilhouetteSprite;

            if (areaParticles.Length > 0 && areaParticles[0] != null)
                areaParticles[0].gameObject.SetActive(true);
        }
    }
}
