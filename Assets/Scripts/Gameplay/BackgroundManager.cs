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

    [Header("Preview (Editor Only)")]
    [Tooltip("SceneView確認用AreaConfig")]
    [SerializeField] private AreaConfig previewAreaConfig;
    [Tooltip("A=Stage1/2、B=Stage3のプレビュー切り替え")]
    [SerializeField] private bool previewStageB = false;

#if UNITY_EDITOR
    private void Update()
    {
        if (Application.isPlaying) return;
        if (previewAreaConfig == null) return;

        if (silhouetteLayer != null)
        {
            if (previewStageB)
            {
                silhouetteLayer.sprite = previewAreaConfig.backgroundSilhouetteSpriteB;
                silhouetteLayer.transform.localScale = previewAreaConfig.backgroundSilhouetteScaleB;
                silhouetteLayer.transform.localPosition = previewAreaConfig.backgroundSilhouettePositionB;
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
                silhouetteFade.TransitionToSprite(silhouetteSpriteB, silhouetteScaleB, silhouettePositionB);
        }
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
            if (midLayer != null)
            {
                midLayer.sprite = area.backgroundFogSprite;
                midLayer.transform.localScale = area.backgroundFogScale;
                midLayer.transform.localPosition = area.backgroundFogPosition;

                var fogScroll = midLayer.GetComponent<FogScroll>();
                var rainScroll = midLayer.GetComponent<RainScroll>();
                var steamScroll = midLayer.GetComponent<SteamScroll>();
                var groundFogScroll = midLayer.GetComponent<GroundFogScroll>();
                switch (area.midLayerScrollMode)
                {
                    case AreaConfig.MidLayerScrollMode.Fog:
                        if (fogScroll != null) fogScroll.enabled = true;
                        if (rainScroll != null) rainScroll.enabled = false;
                        if (steamScroll != null) steamScroll.enabled = false;
                        if (groundFogScroll != null) groundFogScroll.enabled = false;
                        break;
                    case AreaConfig.MidLayerScrollMode.Rain:
                        if (fogScroll != null) fogScroll.enabled = false;
                        if (rainScroll != null) rainScroll.enabled = true;
                        if (steamScroll != null) steamScroll.enabled = false;
                        if (groundFogScroll != null) groundFogScroll.enabled = false;
                        break;
                    case AreaConfig.MidLayerScrollMode.Steam:
                        if (fogScroll != null) fogScroll.enabled = false;
                        if (rainScroll != null) rainScroll.enabled = false;
                        if (steamScroll != null) steamScroll.enabled = true;
                        if (groundFogScroll != null) groundFogScroll.enabled = false;
                        break;
                    case AreaConfig.MidLayerScrollMode.GroundFog:
                        if (fogScroll != null) fogScroll.enabled = false;
                        if (rainScroll != null) rainScroll.enabled = false;
                        if (steamScroll != null) steamScroll.enabled = false;
                        if (groundFogScroll != null) groundFogScroll.enabled = true;
                        break;
                    case AreaConfig.MidLayerScrollMode.None:
                        if (fogScroll != null) fogScroll.enabled = false;
                        if (rainScroll != null) rainScroll.enabled = false;
                        if (steamScroll != null) steamScroll.enabled = false;
                        if (groundFogScroll != null) groundFogScroll.enabled = false;
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
