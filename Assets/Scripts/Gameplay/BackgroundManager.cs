using UnityEngine;

/// <summary>
/// GameSessionのAreaConfigからFarLayer・MidLayerスプライトとカメラ背景色を適用する。
/// Editor直接再生時はfallback設定を使用。
/// </summary>
[ExecuteAlways]
public class BackgroundManager : MonoBehaviour
{
    [Header("Far Layer")]
    [SerializeField] private SpriteRenderer farLayer;
    [Tooltip("Editor直接Play時のフォールバック")]
    [SerializeField] private Sprite fallbackFarSprite;

    [Header("Mid Layer")]
    [SerializeField] private SpriteRenderer midLayer;
    [Tooltip("Editor直接Play時のフォールバック")]
    [SerializeField] private Sprite fallbackFogSprite;

    [Header("Silhouette Layer")]
    [SerializeField] private SpriteRenderer silhouetteLayer;
    [Tooltip("Editor直接Play時のフォールバック")]
    [SerializeField] private Sprite fallbackSilhouetteSprite;

    [Header("Camera")]
    [SerializeField] private Camera targetCamera;

    private Sprite farSpriteB;
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
        if (previewAreaConfig == null || silhouetteLayer == null) return;

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
#endif

    [Header("Particles")]
    [Tooltip("インデックス = areaNumber。各エリアのParticleSystemを設定（不要なエリアはNone）")]
    [SerializeField] private ParticleSystem[] areaParticles;

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
            if (farLayer != null && farSpriteB != null)
                farLayer.sprite = farSpriteB;
            if (silhouetteFade != null && silhouetteSpriteB != null)
                silhouetteFade.TransitionToSprite(silhouetteSpriteB, silhouetteScaleB, silhouettePositionB);
        }
    }

    private void Start()
    {
        if (targetCamera == null) targetCamera = Camera.main;

        foreach (var ps in areaParticles)
            if (ps != null) ps.gameObject.SetActive(false);

        if (GameSession.HasValidArea())
        {
            AreaConfig area = GameSession.SelectedArea;
            if (farLayer != null)
                farLayer.sprite = area.backgroundSprite;
            farSpriteB = area.backgroundSpriteB;
            if (midLayer != null)
            {
                midLayer.sprite = area.backgroundFogSprite;
                midLayer.transform.localScale = area.backgroundFogScale;
                midLayer.transform.localPosition = area.backgroundFogPosition;

                var fogScroll = midLayer.GetComponent<FogScroll>();
                var rainScroll = midLayer.GetComponent<RainScroll>();
                var steamScroll = midLayer.GetComponent<SteamScroll>();
                switch (area.midLayerScrollMode)
                {
                    case AreaConfig.MidLayerScrollMode.Fog:
                        if (fogScroll != null) fogScroll.enabled = true;
                        if (rainScroll != null) rainScroll.enabled = false;
                        if (steamScroll != null) steamScroll.enabled = false;
                        break;
                    case AreaConfig.MidLayerScrollMode.Rain:
                        if (fogScroll != null) fogScroll.enabled = false;
                        if (rainScroll != null) rainScroll.enabled = true;
                        if (steamScroll != null) steamScroll.enabled = false;
                        break;
                    case AreaConfig.MidLayerScrollMode.Steam:
                        if (fogScroll != null) fogScroll.enabled = false;
                        if (rainScroll != null) rainScroll.enabled = false;
                        if (steamScroll != null) steamScroll.enabled = true;
                        break;
                    case AreaConfig.MidLayerScrollMode.None:
                        if (fogScroll != null) fogScroll.enabled = false;
                        if (rainScroll != null) rainScroll.enabled = false;
                        if (steamScroll != null) steamScroll.enabled = false;
                        break;
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
                areaParticles[idx].gameObject.SetActive(true);
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
