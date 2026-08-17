using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Game.Progress;
#if UNITY_EDITOR
using System.IO;
#endif

namespace Game.UI
{
    /// <summary>
    /// 03_AreaSelect背景演出：各エリアボタンを「結晶」に見立て、
    /// ネオンの糸で繋ぎ、光の粒子が流れる星座マップを描画する。
    /// ・星の瞬き（背景全体）
    /// ・本線（1→2→…→9の順で繋ぐ、常時点灯）
    /// ・収束線（1〜9からconvergeTargetへ、薄い点線的表現）
    /// ・本線上を流れる光の粒子
    /// ・各ノードのグロー（未解放は暗く・解放済みは色付きで明滅）
    /// 全てPlay前のInspectorで調整可能。[ContextMenu("Setup Constellation Layers")]でHierarchyを自動生成する。
    /// </summary>
    [DisallowMultipleComponent]
    public class AreaConstellationFX : MonoBehaviour
    {
        [System.Serializable]
        public class AreaNode
        {
            [Tooltip("このノードに対応するエリアボタンのRectTransform")]
            public RectTransform button;
            [Tooltip("進行状況判定に使うAreaId（例：Area_01）。空なら常に解放扱い")]
            public string areaId = "Area_01";
            [Tooltip("このエリアのテーマカラー（ノードのグロー・本線の色に使用）")]
            public Color color = Color.white;
        }

        [Header("Nodes (Area01〜10 のボタンとテーマカラー)")]
        [SerializeField] private AreaNode[] nodes;

        [Header("Chain (本線：nodesのインデックス順に0→1→2…と繋ぐ)")]
        [Tooltip("この範囲のノードだけ本線で順に繋ぐ。例：0〜8なら1〜9番目のノードを繋ぎ、最後のノード(Area10想定)は収束先専用にする")]
        [SerializeField] private int chainStartIndex = 0;
        [SerializeField] private int chainEndIndex = 8;

        [Header("Converge (収束先：通常はArea10)")]
        [Tooltip("nodes配列のインデックス。-1で収束線を描画しない")]
        [SerializeField] private int convergeTargetIndex = 9;

        [Header("Button Resize (画像・当たり判定を縮小)")]
        [Tooltip("縮小後のボタン当たり判定サイズ（ボタン本体のRectTransform。将来の結晶サイズを見越した値）")]
        [SerializeField] private Vector2 buttonHitboxSize = new Vector2(110f, 110f);
        [Tooltip("参考：元のGridLayoutGroupのセルサイズ（縮小比率の計算基準）")]
        [SerializeField] private Vector2 originalCellSize = new Vector2(200f, 200f);
        [Tooltip("参考：元のButtonImage（見た目のアイコン）サイズ（縮小比率の計算基準）")]
        [SerializeField] private Vector2 originalButtonImageSize = new Vector2(240f, 220f);

        [Header("Layer References (Setup ContextMenuで自動生成・自動アサイン)")]
        [SerializeField] private RectTransform starLayer;
        [SerializeField] private RectTransform threadLayer;
        [SerializeField] private RectTransform particleLayer;
        [SerializeField] private RectTransform glowLayer;

        [Header("Star Field")]
        [SerializeField] private int starCount = 70;
        [SerializeField] private float starMinSize = 2f;
        [SerializeField] private float starMaxSize = 4.5f;
        [SerializeField] private Color starColor = new Color(0.81f, 0.88f, 1f, 1f);
        [SerializeField] private float starTwinkleSpeedMin = 0.4f;
        [SerializeField] private float starTwinkleSpeedMax = 1.1f;
        [Tooltip("この割合の星は、単色ではなく10エリアの世界観カラー（nodesのcolor）を纏った、少し大きめ・淡いグロー付きの星になる")]
        [Range(0f, 1f)]
        [SerializeField] private float starTintedRatio = 0.2f;

        [Header("Threads (糸・曲線)")]
        [SerializeField] private float chainThreadWidth = 3f;
        [SerializeField] private Color chainThreadColor = new Color(0.56f, 0.89f, 1f, 0.55f);
        [Tooltip("本線の色を10エリアの世界観カラーでランダムに巡回させる（フェードアウト→フェードインで切り替え）")]
        [SerializeField] private bool chainColorCycleEnabled = true;
        [Tooltip("1つの世界観カラーを表示し続ける時間（秒）")]
        [SerializeField] private float chainColorHoldDuration = 5f;
        [Tooltip("次の世界観カラーへじわじわ切り替わる遷移時間（秒）。薄くはならず、色そのものがクロスフェードする")]
        [SerializeField] private float chainColorFadeDuration = 1.2f;
        [SerializeField] private float convergeThreadWidth = 1.5f;
        [SerializeField] private Color convergeThreadColor = new Color(0.3f, 0.34f, 0.55f, 0.25f);
        [Tooltip("そのノードのエリアがランクA以上を達成済みの場合、収束線をこの色で安定点灯させる")]
        [SerializeField] private Color convergeAchievedColor = new Color(0.91f, 0.79f, 0.42f, 0.6f);
        [Tooltip("ランクA未満の収束線の点滅の速さ（小さいほどゆっくり）")]
        [SerializeField] private float convergeBlinkSpeed = 0.3f;
        [Tooltip("ランクA未満の点滅の下限アルファ倍率（0に近いほど完全に消える瞬間ができる）")]
        [Range(0f, 1f)]
        [SerializeField] private float convergeBlinkMinRatio = 0.15f;
        [Tooltip("ランクA以上達成済みの収束線を、金一色ではなくArea1〜9の色を極小ドット単位で散りばめた煌めき表現にする")]
        [SerializeField] private bool convergeAchievedSparkleEnabled = true;
        [Tooltip("1本の収束線に並べる極小ドットの数")]
        [SerializeField] private int convergeSparkleDotCount = 28;
        [Tooltip("ドット1個のサイズ（px）")]
        [SerializeField] private float convergeSparkleDotSize = 5f;
        [Tooltip("1つの色配置を表示し続ける時間（秒）。短いほど激しく入れ替わる")]
        [SerializeField] private float convergeSparkleHoldDuration = 0.4f;
        [Tooltip("次の色配置へ切り替わるクロスフェード時間（秒）")]
        [SerializeField] private float convergeSparkleFadeDuration = 0.15f;
        [Tooltip("線を何本の直線セグメントで近似して曲線に見せるか")]
        [SerializeField] private int curveSegments = 10;
        [Tooltip("曲がりの強さ（線の長さに対する比率）")]
        [SerializeField] private float curveBowRatio = 0.12f;
        [Tooltip("曲がりの最大量（px）")]
        [SerializeField] private float curveBowMax = 40f;

        [Header("Flow Particles (糸を流れる光)")]
        [SerializeField] private float particleSize = 12f;
        [Tooltip("1秒あたりに線を何周するか")]
        [SerializeField] private float particleSpeed = 0.16f;
        [SerializeField] private Color particleColor = new Color(0.85f, 0.97f, 1f, 0.95f);
        [Tooltip("この割合の粒子は、基本色ではなく所属する線の始点エリアの世界観カラー（nodesのcolor）になる。Star Tinted Ratioと同じ考え方")]
        [Range(0f, 1f)]
        [SerializeField] private float particleTintedRatio = 0.35f;
        [Tooltip("粒子のゆっくりとした明滅の速さ")]
        [SerializeField] private float particleBlinkSpeed = 0.5f;
        [Tooltip("明滅の下限アルファ倍率（0に近いほど完全に消える瞬間ができる）")]
        [Range(0f, 1f)]
        [SerializeField] private float particleBlinkMinRatio = 0.25f;
        [Tooltip("粒子を加算合成（発光して見える）で描画するためのマテリアル。「Generate Additive Material」で自動生成・自動アサインされる")]
        [SerializeField] private Material additiveGlowMaterial;

        [Header("Node Glow (結晶の光)")]
        [Tooltip("ノードのグローに使う柔らかい円形Sprite。「Generate Glow Sprite」で自動生成・自動アサインされる")]
        [SerializeField] private Sprite glowSprite;
        [SerializeField] private float glowSize = 130f;
        [Tooltip("未解放（ロック中）ノードのグロー不透明度")]
        [SerializeField] private float lockedGlowAlpha = 0.12f;
        [Tooltip("解放済みノードのグロー基準不透明度（明滅の中心値）")]
        [SerializeField] private float unlockedGlowAlpha = 0.5f;
        [SerializeField] private float glowPulseSpeed = 1.0f;
        [Tooltip("明滅の振れ幅。大きいほどはっきり明滅する")]
        [SerializeField] private float glowPulseRange = 0.3f;

        [Header("Node Orbit Core (ボタンの結晶ビジュアル：案E オービットコア)")]
        [Tooltip("軌道リング用の中抜き円Sprite。「Generate Ring Sprite」で自動生成・自動アサインされる")]
        [SerializeField] private Sprite ringSprite;
        [Tooltip("軌道リング1のサイズ（buttonHitboxSizeに対する比率）")]
        [Range(0.3f, 1f)]
        [SerializeField] private float orbitRing1Inset = 0.85f;
        [Tooltip("軌道リング2のサイズ（buttonHitboxSizeに対する比率）")]
        [Range(0.3f, 1f)]
        [SerializeField] private float orbitRing2Inset = 0.6f;
        [Tooltip("楕円化の縦潰し比率（1で正円、小さいほど平たい楕円になる）")]
        [Range(0.1f, 1f)]
        [SerializeField] private float orbitEllipseRatio = 0.42f;
        [Tooltip("リング1が1周する秒数")]
        [SerializeField] private float orbitPeriod1 = 6.5f;
        [Tooltip("リング2が1周する秒数（リング1と逆回転）")]
        [SerializeField] private float orbitPeriod2 = 10f;
        [Tooltip("中心コアのサイズ（buttonHitboxSizeに対する比率）")]
        [Range(0.05f, 0.5f)]
        [SerializeField] private float orbitCoreSizeRatio = 0.16f;
        [Tooltip("未解放時のリング/コアの色（グレーで沈ませる）")]
        [SerializeField] private Color orbitLockedColor = new Color(0.5f, 0.53f, 0.62f, 0.45f);
        [Tooltip("解放済みリング1の不透明度（リング2はこの60%になる）")]
        [Range(0f, 1f)]
        [SerializeField] private float orbitRingAlpha = 0.85f;
        [Tooltip("ロック中に中央へ重ねる鍵マークの色")]
        [SerializeField] private Color orbitLockIconColor = new Color(0.85f, 0.87f, 0.92f, 0.6f);
        [Tooltip("番号ラベルのフォントサイズ")]
        [SerializeField] private int orbitNumberFontSize = 60;
        [Tooltip("ロック中の番号ラベルの色（沈んだ色に）")]
        [SerializeField] private Color orbitNumberLockedColor = new Color(0.5f, 0.53f, 0.62f, 0.6f);

        [Header("Node Idle Wobble (ノードの揺らぎ・Play中のみ・糸/番号/ランクも追従)")]
        [Tooltip("ノードが上下左右にゆっくり揺れる量（px）。0で無効")]
        [SerializeField] private float nodeWobbleAmplitude = 6f;
        [Tooltip("揺れの速さ")]
        [SerializeField] private float nodeWobbleSpeed = 0.3f;

        [Header("Area10 Special (Area10だけアーミラリー軌道＋色クロスフェード)")]
        [Tooltip("Area10を実際の解放条件（全エリアランクA）に関わらず常にロック表示にする（未実装のテスト用）")]
        [SerializeField] private bool orbitForceLockArea10 = true;
        [Tooltip("Area10の色が1つのエリアカラーを表示し続ける時間（秒）")]
        [SerializeField] private float area10ColorHoldDuration = 1.2f;
        [Tooltip("Area10の色が次のエリアカラーへ切り替わるクロスフェード時間（秒）")]
        [SerializeField] private float area10ColorFadeDuration = 0.6f;

        [System.Serializable]
        public class DebugAreaRank
        {
            public string areaId = "Area_01";
            [Tooltip("S / A / B / C / D / E。空文字にすると未クリア扱いにできる")]
            public string rank = "";
        }

        [Header("Debug: Area Rank Override (Editor専用テストツール)")]
        [Tooltip("「Apply Bulk Rank to All Areas」で全エリアに一括適用するランク")]
        [SerializeField] private string debugBulkRank = "A";
        [Tooltip("ここに入力したランクを「Apply Debug Ranks」でセーブデータへ直接書き込む（上位判定なし・降格やクリアも可）。Play中のみ実行可能")]
        [SerializeField]
        private DebugAreaRank[] debugAreaRanks = new DebugAreaRank[]
        {
            new DebugAreaRank { areaId = "Area_01" },
            new DebugAreaRank { areaId = "Area_02" },
            new DebugAreaRank { areaId = "Area_03" },
            new DebugAreaRank { areaId = "Area_04" },
            new DebugAreaRank { areaId = "Area_05" },
            new DebugAreaRank { areaId = "Area_06" },
            new DebugAreaRank { areaId = "Area_07" },
            new DebugAreaRank { areaId = "Area_08" },
            new DebugAreaRank { areaId = "Area_09" },
        };

        [Header("Background Drift & Pulse (背景画像自体の動き・Play中のみ)")]
        [Tooltip("動かす対象の背景Image（AreaPanel/Background）のRectTransform")]
        [SerializeField] private RectTransform backgroundImage;
        [Tooltip("背景がゆっくり漂う移動量（px）")]
        [SerializeField] private float bgDriftAmplitude = 35f;
        [Tooltip("背景ドリフトの速さ（小さいほどゆっくり・大きい周期）")]
        [SerializeField] private float bgDriftSpeed = 0.05f;
        [Tooltip("ドリフトで端が見えないようにする基準拡大率")]
        [SerializeField] private float bgBaseScale = 1.08f;
        [SerializeField] private float bgPulseMinBrightness = 0.85f;
        [SerializeField] private float bgPulseMaxBrightness = 1.08f;
        [SerializeField] private float bgPulseSpeed = 0.35f;

        // ---- runtime ----
        private readonly List<StarEntry> stars = new List<StarEntry>();
        private readonly List<LineEntry> chainLines = new List<LineEntry>();
        private readonly List<LineEntry> convergeLines = new List<LineEntry>();
        private readonly List<ParticleEntry> flowParticles = new List<ParticleEntry>();
        private readonly List<GlowEntry> glows = new List<GlowEntry>();
        private readonly List<OrbitEntry> orbitCores = new List<OrbitEntry>();
        private Vector2 backgroundBasePos;
        private Image backgroundImageComp;
        private bool backgroundInitialized;

        private class StarEntry
        {
            public RectTransform rt;
            public Image image;
            public Color color;
            public float baseAlpha;
            public float speed;
            public float phase;
        }

        private class LineEntry
        {
            public RectTransform[] segments;
            public Image[] segmentImages;
            public RectTransform[] sparkleDots;
            public Image[] sparkleDotImages;
            public RectTransform from;
            public RectTransform to;
            public AreaNode sourceNode;
            public float blinkPhase;
        }

        private class ParticleEntry
        {
            public RectTransform rt;
            public Image image;
            public RectTransform from;
            public RectTransform to;
            public float offset;
            public Color baseColor;
            public float blinkPhase;
        }

        private class GlowEntry
        {
            public RectTransform rt;
            public Image image;
            public AreaNode node;
            public float phase;
        }

        private class OrbitEntry
        {
            public AreaNode node;
            public RectTransform ring1Wrap;
            public RectTransform ring2Wrap;
            public Image ring1Image;
            public Image ring2Image;
            public Image ring3Image;
            public bool isArmillary;
            public RectTransform coreRt;
            public Image coreImage;
            public Text numberText;
            public Text rankText;
            public GameObject lockIcon;
            public Image lockShackleImage;
            public Image lockBodyImage;
            public float phase;
            public Vector2 basePosition;
            public float wobblePhase;
        }

        private void Start()
        {
            // GridLayoutGroup等のレイアウト確定を1フレーム待ってから、Editor時点の内容を最新化する
            // （Play前にContextMenuで生成済みの星・線・粒子・グローをそのまま使い、位置だけ再計算する想定）
            StartCoroutine(InitAfterLayout());
        }

        private System.Collections.IEnumerator InitAfterLayout()
        {
            yield return null;
            yield return new WaitForEndOfFrame();

            BuildAll();
        }

        private void BuildAll()
        {
            InitBackground();
            BuildStars();
            BuildChainLines();
            BuildConvergeLines();
            BuildFlowParticles();
            BuildGlows();
            BuildOrbitCores();

            // ★Update()はPlay中しか呼ばれないため、Edit中のプレビュー生成時は
            // ここで一度だけ明示的に位置計算を実行して、生成直後から正しい位置に見えるようにする
            UpdateNodeWobble();
            UpdateStars();
            UpdateChainLines();
            UpdateConvergeLines();
            UpdateFlowParticles();
            UpdateGlows();
            UpdateOrbitCores();
        }

        /// <summary>
        /// 背景画像の基準位置・拡大率を記録し、ドリフト時に端が見えないよう先に拡大しておく。
        /// </summary>
        private void InitBackground()
        {
            if (backgroundImage == null) { backgroundInitialized = false; return; }
            if (!backgroundInitialized)
            {
                backgroundBasePos = backgroundImage.anchoredPosition;
                backgroundImageComp = backgroundImage.GetComponent<Image>();
                backgroundInitialized = true;
            }
            backgroundImage.localScale = new Vector3(bgBaseScale, bgBaseScale, 1f);
        }

        /// <summary>
        /// Destroy()はPlay中専用（Edit中に呼ぶと警告になり正しく破棄されない）なので、
        /// Edit中はDestroyImmediate()を使うよう切り替えるヘルパー。
        /// </summary>
        private static void SafeDestroy(Object obj)
        {
            if (obj == null) return;
            if (Application.isPlaying) Destroy(obj);
            else DestroyImmediate(obj);
        }

        private void Update()
        {
            // ★揺らぎを最初に適用し、糸・粒子・グロー等が同じフレーム内で新しい位置を参照できるようにする
            UpdateNodeWobble();
            UpdateStars();
            UpdateChainLines();
            UpdateConvergeLines();
            UpdateFlowParticles();
            UpdateGlows();
            UpdateOrbitCores();
            // ★背景ドリフト・脈動はPlay中のみ（Edit中に動かすとシーンに保存されてしまうため）
            if (Application.isPlaying) UpdateBackground();
        }

        private void UpdateBackground()
        {
            if (backgroundImage == null || !backgroundInitialized) return;
            float t = Time.unscaledTime;
            float dx = Mathf.Sin(t * bgDriftSpeed * Mathf.PI * 2f) * bgDriftAmplitude;
            float dy = Mathf.Cos(t * bgDriftSpeed * Mathf.PI * 2f * 0.72f) * bgDriftAmplitude * 0.6f;
            backgroundImage.anchoredPosition = backgroundBasePos + new Vector2(dx, dy);

            if (backgroundImageComp != null)
            {
                float b = Mathf.Lerp(bgPulseMinBrightness, bgPulseMaxBrightness,
                    (Mathf.Sin(t * bgPulseSpeed * Mathf.PI * 2f) + 1f) * 0.5f);
                backgroundImageComp.color = new Color(b, b, b, 1f);
            }
        }

        // ================== Coordinate Helper ==================

        private Vector2 GetLocalPos(RectTransform node, RectTransform relativeTo)
        {
            if (node == null || relativeTo == null) return Vector2.zero;
            // ★スクリーン座標を経由するWorldToScreenPoint系はEdit中（Play前）に正しく機能しないことがあるため、
            // Transform階層だけで完結するInverseTransformPointを使う（Edit/Play両方で確実に動く）。
            // relativeTo側はpivot=(0.5,0.5)の全面ストレッチ、子（線・粒子・グロー・星）もanchor=(0.5,0.5)基準で
            // 統一しているため、この変換がそのままanchoredPositionとして使える。
            Vector3 local3 = relativeTo.InverseTransformPoint(node.position);
            return new Vector2(local3.x, local3.y);
        }

        /// <summary>
        /// 親の子を全て破棄する。foreach(Transform in parent)しながら破棄すると
        /// 削除のたびに子のインデックスがズレて一部が消し残るため、必ず末尾から逆順に処理する。
        /// </summary>
        private static void ClearChildren(Transform parent)
        {
            if (parent == null) return;
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                SafeDestroy(parent.GetChild(i).gameObject);
            }
        }

        private static void PositionLine(RectTransform line, Vector2 a, Vector2 b, float width)
        {
            Vector2 diff = b - a;
            float distance = diff.magnitude;
            float angle = Mathf.Atan2(diff.y, diff.x) * Mathf.Rad2Deg;

            line.pivot = new Vector2(0f, 0.5f);
            line.anchoredPosition = a;
            line.sizeDelta = new Vector2(distance, width);
            line.localRotation = Quaternion.Euler(0f, 0f, angle);
        }

        /// <summary>
        /// a→bを結ぶ二次ベジェ曲線の制御点を求める（中点から垂直方向にbowだけ盛り上げる）。
        /// 直線だと硬すぎるための丸みづけ。
        /// </summary>
        private Vector2 ComputeBowControlPoint(Vector2 a, Vector2 b)
        {
            Vector2 mid = (a + b) * 0.5f;
            Vector2 diff = b - a;
            float len = diff.magnitude;
            if (len < 0.001f) return mid;
            Vector2 normal = new Vector2(-diff.y, diff.x) / len;
            float bow = Mathf.Min(curveBowMax, len * curveBowRatio);
            return mid + normal * bow;
        }

        private static Vector2 QuadraticBezier(Vector2 a, Vector2 c, Vector2 b, float t)
        {
            float u = 1f - t;
            return u * u * a + 2f * u * t * c + t * t * b;
        }

        /// <summary>
        /// a→bの間を、曲線に近似した複数の直線セグメントで結ぶ。
        /// </summary>
        private void PositionCurve(RectTransform[] segments, Vector2 a, Vector2 b, float width)
        {
            if (segments == null || segments.Length == 0) return;
            Vector2 control = ComputeBowControlPoint(a, b);
            int n = segments.Length;
            Vector2 prev = a;
            for (int i = 0; i < n; i++)
            {
                float t = (float)(i + 1) / n;
                Vector2 next = QuadraticBezier(a, control, b, t);
                if (segments[i] != null) PositionLine(segments[i], prev, next, width);
                prev = next;
            }
        }

        // ================== Stars ==================

        private void BuildStars()
        {
            if (starLayer == null) return;
            ClearChildren(starLayer);
            stars.Clear();

            Rect area = starLayer.rect;
            for (int i = 0; i < starCount; i++)
            {
                var go = new GameObject("Star_" + i, typeof(RectTransform), typeof(Image));
                var rt = (RectTransform)go.transform;
                rt.SetParent(starLayer, false);
                float size = Random.Range(starMinSize, starMaxSize);
                rt.sizeDelta = new Vector2(size, size);
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(
                    Random.Range(area.xMin, area.xMax),
                    Random.Range(area.yMin, area.yMax));

                var img = go.GetComponent<Image>();

                // ★全ての星にグロースプライトを使い、柔らかく発光しているような見た目にする
                if (glowSprite != null) img.sprite = glowSprite;

                bool tinted = nodes != null && nodes.Length > 0 && Random.value < starTintedRatio;
                Color starTint = tinted ? nodes[Random.Range(0, nodes.Length)].color : starColor;
                if (tinted)
                {
                    // 世界観カラーを纏う星は少し大きく目立たせる
                    size = Mathf.Max(size, starMaxSize) * Random.Range(1.4f, 2.2f);
                    rt.sizeDelta = new Vector2(size, size);
                }

                img.color = starTint;
                img.raycastTarget = false;

                float baseAlpha = tinted ? Random.Range(0.35f, 0.7f) : Random.Range(0.15f, 0.5f);
                stars.Add(new StarEntry
                {
                    rt = rt,
                    image = img,
                    color = starTint,
                    baseAlpha = baseAlpha,
                    speed = Random.Range(starTwinkleSpeedMin, starTwinkleSpeedMax),
                    phase = Random.Range(0f, Mathf.PI * 2f)
                });
            }
        }

        private void UpdateStars()
        {
            for (int i = 0; i < stars.Count; i++)
            {
                var s = stars[i];
                if (s.image == null) continue;

                // ★サイン波をそのまま使うと明滅が単調なので、pow()で山を尖らせて
                // 「普段は控えめ→一瞬パッと輝く」というきらめき感を出す
                float raw01 = (Mathf.Sin(Time.unscaledTime * s.speed + s.phase) + 1f) * 0.5f;
                float sparkle = Mathf.Pow(raw01, 3f);

                Color c = s.color;
                c.a = Mathf.Clamp01(Mathf.Lerp(s.baseAlpha * 0.3f, 1f, sparkle));
                s.image.color = c;

                float scaleMul = Mathf.Lerp(0.75f, 1.5f, sparkle);
                if (s.rt != null) s.rt.localScale = new Vector3(scaleMul, scaleMul, 1f);
            }
        }

        // ================== Chain Threads ==================

        private RectTransform[] CreateCurveSegments(RectTransform parent, string namePrefix, Color color)
        {
            var segs = new RectTransform[Mathf.Max(1, curveSegments)];
            for (int i = 0; i < segs.Length; i++)
            {
                var go = new GameObject($"{namePrefix}_seg{i}", typeof(RectTransform), typeof(Image));
                var rt = (RectTransform)go.transform;
                rt.SetParent(parent, false);
                // ★アンカーはレイヤー中心基準(0.5,0.5)に統一。pivotをPositionLine側で(0,0.5)にすることで
                // 「始点にpivot（線の左端）を置き、そこから終点方向へ伸ばす」形にする
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                var img = go.GetComponent<Image>();
                img.color = color;
                img.raycastTarget = false;
                segs[i] = rt;
            }
            return segs;
        }

        private void BuildChainLines()
        {
            if (threadLayer == null || nodes == null) return;
            ClearChildren(threadLayer);
            chainLines.Clear();

            int start = Mathf.Clamp(chainStartIndex, 0, nodes.Length - 1);
            int end = Mathf.Clamp(chainEndIndex, 0, nodes.Length - 1);
            for (int i = start; i < end; i++)
            {
                var a = nodes[i];
                var b = nodes[i + 1];
                if (a?.button == null || b?.button == null) continue;

                var segs = CreateCurveSegments(threadLayer, $"Chain_{i}_{i + 1}", chainThreadColor);
                var imgs = new Image[segs.Length];
                for (int s = 0; s < segs.Length; s++) imgs[s] = segs[s].GetComponent<Image>();

                chainLines.Add(new LineEntry
                {
                    segments = segs,
                    segmentImages = imgs,
                    from = a.button,
                    to = b.button,
                    sourceNode = a,
                    blinkPhase = Random.Range(0f, 1000f) // ★色巡回の時間オフセット（秒）として使う。線ごとにズラして同時切り替えを防ぐ
                });
            }
        }

        /// <summary>
        /// 本線の色巡回で使う、あるスロット番号における世界観カラーを決める。
        /// Unity標準のRandomを使わず簡易ハッシュにすることで、状態を持たずに毎フレーム計算できるようにしている
        /// （同じ(lineIndex, slot)なら常に同じ色になる＝Editorプレビュー等で時間が飛んでもズレない）。
        /// </summary>
        private Color GetChainCycleColor(int lineIndex, int slot)
        {
            if (nodes == null || nodes.Length == 0) return chainThreadColor;
            int hash = (lineIndex * 9301 + slot * 49297) % nodes.Length;
            if (hash < 0) hash += nodes.Length;
            Color c = nodes[hash].color;
            c.a = chainThreadColor.a; // nodesのcolorはalpha=0で登録されているため、アルファは基本色から引き継ぐ
            return c;
        }

        private void UpdateChainLines()
        {
            float period = chainColorHoldDuration + chainColorFadeDuration;

            for (int i = 0; i < chainLines.Count; i++)
            {
                var l = chainLines[i];
                if (l.segments == null || l.from == null || l.to == null) continue;
                Vector2 a = GetLocalPos(l.from, threadLayer);
                Vector2 b = GetLocalPos(l.to, threadLayer);
                PositionCurve(l.segments, a, b, chainThreadWidth);

                if (chainColorCycleEnabled && period > 0.01f && l.segmentImages != null)
                {
                    float t = Time.unscaledTime + l.blinkPhase;
                    int slot = Mathf.FloorToInt(t / period);
                    float localT = t - slot * period;

                    Color c;
                    if (localT < chainColorHoldDuration)
                    {
                        // 表示中：現在のスロットの色をそのまま表示（アルファは落とさない）
                        c = GetChainCycleColor(i, slot);
                    }
                    else
                    {
                        // 遷移中：薄くせず、色そのものを次の世界観カラーへじわじわクロスフェードする
                        float ft = Mathf.Clamp01((localT - chainColorHoldDuration) / chainColorFadeDuration);
                        Color from = GetChainCycleColor(i, slot);
                        Color to = GetChainCycleColor(i, slot + 1);
                        c = Color.Lerp(from, to, ft);
                    }

                    for (int s = 0; s < l.segmentImages.Length; s++)
                    {
                        if (l.segmentImages[s] != null) l.segmentImages[s].color = c;
                    }
                }
            }
        }

        // ================== Converge Threads ==================

        private void BuildConvergeLines()
        {
            if (threadLayer == null || nodes == null) return;
            convergeLines.Clear();
            if (convergeTargetIndex < 0 || convergeTargetIndex >= nodes.Length) return;

            var target = nodes[convergeTargetIndex];
            if (target?.button == null) return;

            for (int i = 0; i < nodes.Length; i++)
            {
                if (i == convergeTargetIndex) continue;
                var n = nodes[i];
                if (n?.button == null) continue;

                var segs = CreateCurveSegments(threadLayer, $"Converge_{i}", convergeThreadColor);
                foreach (var seg in segs) seg.SetAsFirstSibling(); // 本線より奥に描画

                var imgs = new Image[segs.Length];
                for (int s = 0; s < segs.Length; s++) imgs[s] = segs[s].GetComponent<Image>();

                // ランクA達成済み用の極小煌めきドット（普段は非表示。達成時だけUpdateConvergeLinesで表示・彩色する）
                var dotRts = new RectTransform[Mathf.Max(0, convergeSparkleDotCount)];
                var dotImgs = new Image[dotRts.Length];
                for (int d = 0; d < dotRts.Length; d++)
                {
                    var dotGo = new GameObject($"SparkleDot_{i}_{d}", typeof(RectTransform), typeof(Image));
                    var dotRt = (RectTransform)dotGo.transform;
                    dotRt.SetParent(threadLayer, false);
                    dotRt.anchorMin = dotRt.anchorMax = new Vector2(0.5f, 0.5f);
                    dotRt.sizeDelta = new Vector2(convergeSparkleDotSize, convergeSparkleDotSize);
                    dotRt.SetAsFirstSibling();

                    var dotImg = dotGo.GetComponent<Image>();
                    dotImg.raycastTarget = false;
                    if (glowSprite != null) dotImg.sprite = glowSprite;
                    if (additiveGlowMaterial != null) dotImg.material = additiveGlowMaterial;
                    dotGo.SetActive(false);

                    dotRts[d] = dotRt;
                    dotImgs[d] = dotImg;
                }

                convergeLines.Add(new LineEntry
                {
                    segments = segs,
                    segmentImages = imgs,
                    sparkleDots = dotRts,
                    sparkleDotImages = dotImgs,
                    from = n.button,
                    to = target.button,
                    sourceNode = n,
                    blinkPhase = Random.Range(0f, Mathf.PI * 2f)
                });
            }
        }

        /// <summary>
        /// 指定エリアがランクA以上を達成済みか判定する。
        /// Editorプレビュー等でProgressManager未ロードの場合は未達成扱い（点滅表示）にする。
        /// </summary>
        private static bool IsAreaRankAchieved(string areaId)
        {
            if (string.IsNullOrEmpty(areaId)) return false;
            if (ProgressManager.Instance == null) return false;
            return ProgressManager.IsRankAOrBetter(ProgressManager.Instance.GetAreaBestRank(areaId));
        }

        /// <summary>
        /// 達成済み収束線の煌めき用：ある(線番号, ドット番号, スロット)におけるArea1〜9のいずれかの色を返す。
        /// GetChainCycleColorと同じ考え方で状態を持たず、時間から直接計算する。
        /// </summary>
        private Color GetConvergeSparkleColor(int lineIndex, int dotIndex, int slot)
        {
            if (nodes == null || nodes.Length == 0) return convergeAchievedColor;
            int hash = (lineIndex * 7349 + dotIndex * 2617 + slot * 49297) % nodes.Length;
            if (hash < 0) hash += nodes.Length;
            Color c = nodes[hash].color;
            c.a = convergeAchievedColor.a;
            return c;
        }

        private void UpdateConvergeLines()
        {
            float sparklePeriod = Mathf.Max(0.01f, convergeSparkleHoldDuration + convergeSparkleFadeDuration);

            for (int i = 0; i < convergeLines.Count; i++)
            {
                var l = convergeLines[i];
                if (l.segments == null || l.from == null || l.to == null) continue;
                Vector2 a = GetLocalPos(l.from, threadLayer);
                Vector2 b = GetLocalPos(l.to, threadLayer);
                PositionCurve(l.segments, a, b, convergeThreadWidth);

                bool achieved = IsAreaRankAchieved(l.sourceNode?.areaId);
                bool sparkleActive = achieved && convergeAchievedSparkleEnabled && l.sparkleDots != null && l.sparkleDots.Length > 0;

                // 通常のセグメント表示とドット表示は排他：煌めき中はセグメントを隠し、ドットだけを見せる
                if (l.segmentImages != null)
                {
                    bool showSegments = !sparkleActive;
                    for (int s = 0; s < l.segments.Length; s++)
                    {
                        if (l.segments[s] != null && l.segments[s].gameObject.activeSelf != showSegments)
                            l.segments[s].gameObject.SetActive(showSegments);
                    }
                }

                if (sparkleActive)
                {
                    // ★曲線上に極小ドットを等間隔で並べ、Area1〜9の色をバラバラに割り当てて短時間で入れ替える。
                    // 「金一色の線」ではなく「色とりどりの粒が並んで短時間で入れ替わる」煌めきに見せる
                    Vector2 control = ComputeBowControlPoint(a, b);
                    float t = Time.unscaledTime;
                    int slot = Mathf.FloorToInt(t / sparklePeriod);
                    float localT = t - slot * sparklePeriod;
                    float ft = localT < convergeSparkleHoldDuration
                        ? 0f
                        : Mathf.Clamp01((localT - convergeSparkleHoldDuration) / convergeSparkleFadeDuration);

                    int dotCount = l.sparkleDots.Length;
                    for (int d = 0; d < dotCount; d++)
                    {
                        if (l.sparkleDots[d] == null) continue;
                        if (!l.sparkleDots[d].gameObject.activeSelf) l.sparkleDots[d].gameObject.SetActive(true);

                        float dt = (d + 0.5f) / dotCount;
                        l.sparkleDots[d].anchoredPosition = QuadraticBezier(a, control, b, dt);

                        if (l.sparkleDotImages != null && l.sparkleDotImages[d] != null)
                        {
                            Color colA = GetConvergeSparkleColor(i, d, slot);
                            Color colB = GetConvergeSparkleColor(i, d, slot + 1);
                            l.sparkleDotImages[d].color = Color.Lerp(colA, colB, ft);
                        }
                    }
                    continue;
                }

                // 煌めき中でない場合はドットを隠す
                if (l.sparkleDots != null)
                {
                    for (int d = 0; d < l.sparkleDots.Length; d++)
                    {
                        if (l.sparkleDots[d] != null && l.sparkleDots[d].gameObject.activeSelf)
                            l.sparkleDots[d].gameObject.SetActive(false);
                    }
                }

                Color c;
                if (achieved)
                {
                    c = convergeAchievedColor;
                }
                else
                {
                    float t01 = (Mathf.Sin(Time.unscaledTime * convergeBlinkSpeed + l.blinkPhase) + 1f) * 0.5f;
                    float alphaMul = Mathf.Lerp(convergeBlinkMinRatio, 1f, t01);
                    c = convergeThreadColor;
                    c.a *= alphaMul;
                }

                if (l.segmentImages != null)
                {
                    for (int s = 0; s < l.segmentImages.Length; s++)
                    {
                        if (l.segmentImages[s] != null) l.segmentImages[s].color = c;
                    }
                }
            }
        }

        // ================== Flow Particles ==================

        private void BuildFlowParticles()
        {
            if (particleLayer == null) return;
            ClearChildren(particleLayer);
            flowParticles.Clear();

            for (int i = 0; i < chainLines.Count; i++)
            {
                var line = chainLines[i];
                var go = new GameObject($"Particle_{i}", typeof(RectTransform), typeof(Image));
                var rt = (RectTransform)go.transform;
                rt.SetParent(particleLayer, false);
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(particleSize, particleSize);

                var img = go.GetComponent<Image>();
                img.raycastTarget = false;
                if (glowSprite != null) img.sprite = glowSprite; // 四角ではなく柔らかい光の玉に見せる
                if (additiveGlowMaterial != null) img.material = additiveGlowMaterial; // 加算合成で発光しているように見せる

                // ★星のStar Tinted Ratioと同じ考え方：一定割合は始点エリアの世界観カラーを纏う
                bool tinted = line.sourceNode != null && Random.value < particleTintedRatio;
                Color baseColor = tinted ? line.sourceNode.color : particleColor;
                baseColor.a = particleColor.a; // nodesのcolorはalpha=0で登録されているため、アルファは基本色から引き継ぐ
                img.color = baseColor;

                flowParticles.Add(new ParticleEntry
                {
                    rt = rt,
                    image = img,
                    from = line.from,
                    to = line.to,
                    offset = (float)i / Mathf.Max(1, chainLines.Count),
                    baseColor = baseColor,
                    blinkPhase = Random.Range(0f, Mathf.PI * 2f)
                });
            }
        }

        private void UpdateFlowParticles()
        {
            for (int i = 0; i < flowParticles.Count; i++)
            {
                var p = flowParticles[i];
                if (p.rt == null || p.from == null || p.to == null) continue;
                float t = Mathf.Repeat(Time.unscaledTime * particleSpeed + p.offset, 1f);
                Vector2 a = GetLocalPos(p.from, particleLayer);
                Vector2 b = GetLocalPos(p.to, particleLayer);
                Vector2 control = ComputeBowControlPoint(a, b); // 線と同じ曲線に沿って流れるように
                p.rt.anchoredPosition = QuadraticBezier(a, control, b, t);

                if (p.image != null)
                {
                    float t01 = (Mathf.Sin(Time.unscaledTime * particleBlinkSpeed + p.blinkPhase) + 1f) * 0.5f;
                    float alphaMul = Mathf.Lerp(particleBlinkMinRatio, 1f, t01);
                    Color c = p.baseColor;
                    c.a *= alphaMul;
                    p.image.color = c;
                }
            }
        }

        // ================== Node Glow ==================

        private void BuildGlows()
        {
            if (glowLayer == null || nodes == null) return;
            ClearChildren(glowLayer);
            glows.Clear();

            for (int i = 0; i < nodes.Length; i++)
            {
                var n = nodes[i];
                if (n?.button == null) continue;

                var go = new GameObject($"Glow_{i}", typeof(RectTransform), typeof(Image));
                var rt = (RectTransform)go.transform;
                rt.SetParent(glowLayer, false);
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(glowSize, glowSize);

                var img = go.GetComponent<Image>();
                img.color = n.color;
                img.raycastTarget = false;
                img.sprite = glowSprite;

                glows.Add(new GlowEntry { rt = rt, image = img, node = n, phase = Random.Range(0f, Mathf.PI * 2f) });
            }
        }

        private void UpdateGlows()
        {
            for (int i = 0; i < glows.Count; i++)
            {
                var g = glows[i];
                if (g.rt == null || g.image == null || g.node?.button == null) continue;

                g.rt.anchoredPosition = GetLocalPos(g.node.button, glowLayer);

                bool unlocked = string.IsNullOrEmpty(g.node.areaId) || UnlockRules.IsAreaUnlocked(g.node.areaId);
                float baseAlpha = unlocked ? unlockedGlowAlpha : lockedGlowAlpha;
                float pulse = unlocked ? Mathf.Sin(Time.unscaledTime * glowPulseSpeed + g.phase) * glowPulseRange : 0f;

                Color c = g.node.color;
                c.a = Mathf.Clamp01(baseAlpha + pulse);
                g.image.color = c;
            }
        }

        // ================== Node Orbit Core (結晶ビジュアル：案E) ==================

        /// <summary>
        /// 各ボタンの中に「中心コア＋2本の楕円軌道リング」を生成する（案E：オービットコア）。
        /// StarLayer等とは違い、各ボタン自身の子として生成するため、ボタンの移動に自動追従し
        /// GetLocalPos()での座標変換が不要になる。
        /// 既存のButtonImage（旧プレースホルダー見た目）はここで透明化し、OrbitCoreだけが見えるようにする。
        /// </summary>
        private void BuildOrbitCores()
        {
            if (nodes == null) return;
            orbitCores.Clear();

            foreach (var n in nodes)
            {
                if (n?.button == null) continue;

                // 旧プレースホルダー見た目（ButtonImage）は透明化する（破棄はしない：他コードが参照しているため）
                var buttonImage = n.button.Find("ButtonImage")?.GetComponent<Image>();
                if (buttonImage != null)
                {
                    var ic = buttonImage.color;
                    ic.a = 0f;
                    buttonImage.color = ic;
                }

                // ★旧LockOverlay（ロック中にボタン全体を不透明な板で覆って隠す仕組み）は
                // オービットコアの意匠（沈んだ色＋鍵マーク）と競合するため無効化する。
                // StageButton側のlockOverlay参照ごとnullにして、StageButton.Start()等が
                // 後から再度ONにしてこないようにする（参照を切ってしまえば二度と触られない）。
                var stageButton = n.button.GetComponent<StageButton>();
                if (stageButton != null && stageButton.lockOverlay != null)
                {
                    stageButton.lockOverlay.SetActive(false);
                    stageButton.lockOverlay = null;
                }

                // 既存のOrbitCoreがあれば作り直す（何度再実行しても重複しない）
                var existing = n.button.Find("OrbitCore");
                if (existing != null) SafeDestroy(existing.gameObject);

                var root = new GameObject("OrbitCore", typeof(RectTransform));
                var rootRt = (RectTransform)root.transform;
                rootRt.SetParent(n.button, false);
                rootRt.anchorMin = Vector2.zero;
                rootRt.anchorMax = Vector2.one;
                rootRt.offsetMin = Vector2.zero;
                rootRt.offsetMax = Vector2.zero;
                rootRt.SetAsFirstSibling(); // LockOverlay/Textより奥（LockOverlayが最前面で暗転できるように）

                bool isArea10 = n.areaId == "Area_10";
                RectTransform ring1Wrap, ring2Wrap;
                Image ring1Image, ring2Image, ring3Image = null;

                if (isArea10)
                {
                    // ★Area10だけアーミラリー軌道（案K）：傾きの違う3本のリングを1つの枠にまとめて一緒に回転させる
                    var armWrap = CreateOrbitRingWrap(rootRt, "ArmillaryWrap", orbitRing1Inset);
                    ring1Wrap = armWrap;
                    ring2Wrap = armWrap;
                    ring1Image = CreateOrbitRingImage(armWrap, "ArmRing1", 0f);
                    ring2Image = CreateOrbitRingImage(armWrap, "ArmRing2", 60f);
                    ring3Image = CreateOrbitRingImage(armWrap, "ArmRing3", 120f);
                }
                else
                {
                    ring1Wrap = CreateOrbitRingWrap(rootRt, "Ring1Wrap", orbitRing1Inset);
                    ring1Image = CreateOrbitRingImage(ring1Wrap, "Ring1");

                    ring2Wrap = CreateOrbitRingWrap(rootRt, "Ring2Wrap", orbitRing2Inset);
                    ring2Image = CreateOrbitRingImage(ring2Wrap, "Ring2");
                }

                float coreSize = buttonHitboxSize.x * orbitCoreSizeRatio;
                var coreGo = new GameObject("CoreDot", typeof(RectTransform), typeof(Image));
                var coreRt = (RectTransform)coreGo.transform;
                coreRt.SetParent(rootRt, false);
                coreRt.anchorMin = coreRt.anchorMax = new Vector2(0.5f, 0.5f);
                coreRt.sizeDelta = new Vector2(coreSize, coreSize);

                var coreImg = coreGo.GetComponent<Image>();
                coreImg.raycastTarget = false;
                if (glowSprite != null) coreImg.sprite = glowSprite;
                if (additiveGlowMaterial != null) coreImg.material = additiveGlowMaterial;

                // 番号ラベル：既存のText (Legacy)を再利用し、結晶の真下にエリアカラーで表示する
                // （中央に置くと回転するリングと重なって読みづらいため、あえて外側の下に配置＝おすすめ位置）
                Text numberText = n.button.Find("Text (Legacy)")?.GetComponent<Text>();
                if (numberText != null)
                {
                    numberText.gameObject.SetActive(true);
                    numberText.text = ExtractAreaNumber(n.areaId);
                    numberText.fontSize = orbitNumberFontSize;
                    numberText.alignment = TextAnchor.MiddleCenter;
                    numberText.fontStyle = FontStyle.Bold;
                    // ★ボックス高さがフォントサイズより小さいとVertical Overflow(Truncate)で
                    // 文字ごと見えなくなるため、はみ出しを許可して確実に描画されるようにする
                    numberText.horizontalOverflow = HorizontalWrapMode.Overflow;
                    numberText.verticalOverflow = VerticalWrapMode.Overflow;

                    var numRt = numberText.rectTransform;
                    numRt.anchorMin = new Vector2(0.5f, 0f);
                    numRt.anchorMax = new Vector2(0.5f, 0f);
                    numRt.pivot = new Vector2(0.5f, 1f);
                    numRt.sizeDelta = new Vector2(buttonHitboxSize.x, orbitNumberFontSize * 1.3f);
                    numRt.anchoredPosition = new Vector2(0f, -6f);
                }

                // ランク表示：過去に獲得したそのエリアの最高ランクを結晶の「上」に表示する
                // （番号は下に置いているので、上に置くことで重ならずバランスが取れる＝おすすめ位置）
                var existingRank = n.button.Find("RankText");
                if (existingRank != null) SafeDestroy(existingRank.gameObject);

                string bestRank = (ProgressManager.Instance != null && !string.IsNullOrEmpty(n.areaId))
                    ? ProgressManager.Instance.GetAreaBestRank(n.areaId)
                    : "";

                var rankGo = new GameObject("RankText", typeof(RectTransform), typeof(Text));
                var rankRt = (RectTransform)rankGo.transform;
                rankRt.SetParent(n.button, false);
                rankRt.anchorMin = new Vector2(0.5f, 1f);
                rankRt.anchorMax = new Vector2(0.5f, 1f);
                rankRt.pivot = new Vector2(0.5f, 0f);
                rankRt.sizeDelta = new Vector2(buttonHitboxSize.x, orbitNumberFontSize * 1.3f);
                rankRt.anchoredPosition = new Vector2(0f, 6f);

                var rankText = rankGo.GetComponent<Text>();
                rankText.text = bestRank;
                rankText.font = numberText != null ? numberText.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                rankText.fontSize = orbitNumberFontSize;
                rankText.fontStyle = FontStyle.Bold;
                rankText.alignment = TextAnchor.MiddleCenter;
                rankText.raycastTarget = false;
                rankText.horizontalOverflow = HorizontalWrapMode.Overflow;
                rankText.verticalOverflow = VerticalWrapMode.Overflow;
                rankText.color = GetRankColor(bestRank);
                rankGo.SetActive(!string.IsNullOrEmpty(bestRank));

                // 鍵マーク：ロック中のみ表示する簡易アイコン（輪＝シャックル＋四角＝本体）
                var lockIconGo = new GameObject("LockIcon", typeof(RectTransform));
                var lockIconRt = (RectTransform)lockIconGo.transform;
                lockIconRt.SetParent(rootRt, false);
                lockIconRt.anchorMin = lockIconRt.anchorMax = new Vector2(0.5f, 0.5f);
                float lockIconSize = buttonHitboxSize.x * 0.34f;
                lockIconRt.sizeDelta = new Vector2(lockIconSize, lockIconSize);
                lockIconRt.SetAsLastSibling(); // リング・コアより手前に表示

                var shackleGo = new GameObject("Shackle", typeof(RectTransform), typeof(Image));
                var shackleRt = (RectTransform)shackleGo.transform;
                shackleRt.SetParent(lockIconRt, false);
                shackleRt.anchorMin = shackleRt.anchorMax = new Vector2(0.5f, 1f);
                shackleRt.pivot = new Vector2(0.5f, 0.5f);
                shackleRt.sizeDelta = new Vector2(lockIconSize * 0.6f, lockIconSize * 0.6f);
                shackleRt.anchoredPosition = new Vector2(0f, -lockIconSize * 0.18f);
                var shackleImg = shackleGo.GetComponent<Image>();
                shackleImg.raycastTarget = false;
                if (ringSprite != null) shackleImg.sprite = ringSprite;

                var bodyGo = new GameObject("Body", typeof(RectTransform), typeof(Image));
                var bodyRt = (RectTransform)bodyGo.transform;
                bodyRt.SetParent(lockIconRt, false);
                bodyRt.anchorMin = bodyRt.anchorMax = new Vector2(0.5f, 0f);
                bodyRt.pivot = new Vector2(0.5f, 0f);
                bodyRt.sizeDelta = new Vector2(lockIconSize * 0.8f, lockIconSize * 0.55f);
                bodyRt.anchoredPosition = Vector2.zero;
                var bodyImg = bodyGo.GetComponent<Image>();
                bodyImg.raycastTarget = false;

                orbitCores.Add(new OrbitEntry
                {
                    node = n,
                    ring1Wrap = ring1Wrap,
                    ring2Wrap = ring2Wrap,
                    ring1Image = ring1Image,
                    ring2Image = ring2Image,
                    ring3Image = ring3Image,
                    isArmillary = isArea10,
                    coreRt = coreRt,
                    coreImage = coreImg,
                    numberText = numberText,
                    rankText = rankText,
                    lockIcon = lockIconGo,
                    lockShackleImage = shackleImg,
                    lockBodyImage = bodyImg,
                    phase = Random.Range(0f, Mathf.PI * 2f),
                    basePosition = n.button.anchoredPosition,
                    wobblePhase = Random.Range(0f, Mathf.PI * 2f)
                });
            }
        }

        /// <summary>
        /// "Area_09" → "9" のようにareaIdから表示用の番号文字列を取り出す（先頭の0は付けない）。
        /// </summary>
        private static string ExtractAreaNumber(string areaId)
        {
            if (string.IsNullOrEmpty(areaId)) return "";
            int idx = areaId.LastIndexOf('_');
            string raw = idx >= 0 ? areaId.Substring(idx + 1) : areaId;
            return int.TryParse(raw, out int num) ? num.ToString() : raw;
        }

        /// <summary>
        /// ランク文字("S"/"A"/...)から表示色を決める。ResultScreenUI.GetRankColorと同じ配色に揃えている。
        /// </summary>
        private static Color GetRankColor(string rank) => rank switch
        {
            "S" => new Color(1.0f, 0.84f, 0.0f),
            "A" => new Color(0.0f, 0.90f, 1.0f),
            "B" => new Color(0.2f, 0.90f, 0.3f),
            "C" => new Color(1.0f, 0.90f, 0.2f),
            "D" => new Color(1.0f, 0.50f, 0.1f),
            _ => new Color(0.7f, 0.30f, 0.3f),
        };

        /// <summary>
        /// 各ノードを上下左右にゆっくり揺らす（Play中のみ）。ボタン自身のanchoredPositionを直接動かすため、
        /// 糸（GetLocalPosで毎フレーム位置を取り直している）やオービットコア・番号・ランク（ボタンの子）は
        /// 追加のコードなしで自動的に追従する。Edit中は保存データを壊さないよう一切動かさない。
        /// </summary>
        private void UpdateNodeWobble()
        {
            if (!Application.isPlaying || nodeWobbleAmplitude <= 0.001f) return;

            for (int i = 0; i < orbitCores.Count; i++)
            {
                var o = orbitCores[i];
                if (o.node?.button == null) continue;

                float t = Time.unscaledTime;
                float dx = Mathf.Sin(t * nodeWobbleSpeed + o.wobblePhase) * nodeWobbleAmplitude;
                float dy = Mathf.Cos(t * nodeWobbleSpeed * 0.8f + o.wobblePhase * 1.3f) * nodeWobbleAmplitude * 0.7f;
                o.node.button.anchoredPosition = o.basePosition + new Vector2(dx, dy);
            }
        }

        private RectTransform CreateOrbitRingWrap(RectTransform parent, string name, float insetRatio)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            float size = buttonHitboxSize.x * insetRatio;
            rt.sizeDelta = new Vector2(size, size);
            return rt;
        }

        private Image CreateOrbitRingImage(RectTransform parent, string name, float baseRotationDeg = 0f)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.localScale = new Vector3(1f, orbitEllipseRatio, 1f); // 縦潰しで楕円に見せる
            rt.localRotation = Quaternion.Euler(0f, 0f, baseRotationDeg); // アーミラリー軌道用の固定傾き（通常は0）

            var img = go.GetComponent<Image>();
            img.raycastTarget = false;
            if (ringSprite != null) img.sprite = ringSprite;
            return img;
        }

        /// <summary>
        /// Area10専用：Area1〜9のnodes色を順に巡回した「あるスロットの色」を返す。
        /// GetChainCycleColorと同様、状態を持たず時間から直接計算する。
        /// </summary>
        private Color GetArea10CycleColor(int slot)
        {
            if (nodes == null || nodes.Length < 10) return orbitLockedColor;
            int idx = ((slot % 9) + 9) % 9; // 0..8 = Area_01..Area_09
            Color c = nodes[idx].color;
            c.a = orbitRingAlpha;
            return c;
        }

        private void UpdateOrbitCores()
        {
            for (int i = 0; i < orbitCores.Count; i++)
            {
                var o = orbitCores[i];
                if (o.node?.button == null) continue;

                bool isArea10 = o.node.areaId == "Area_10";
                bool unlocked = (isArea10 && orbitForceLockArea10)
                    ? false
                    : (string.IsNullOrEmpty(o.node.areaId) || UnlockRules.IsAreaUnlocked(o.node.areaId));

                // ★ロック中は回転させない（解放済みだけが「動いている」ことで違いを見せる）
                if (unlocked)
                {
                    if (o.isArmillary)
                    {
                        // アーミラリー軌道：3本のリングは同じ枠（ring1Wrap）にまとまっているので1回転だけでよい
                        float armAngle = (Time.unscaledTime / Mathf.Max(0.01f, orbitPeriod1)) * 360f;
                        if (o.ring1Wrap != null) o.ring1Wrap.localRotation = Quaternion.Euler(0f, 0f, armAngle);

                        // Area1〜9の色を短時間でクロスフェードしながら巡回させる
                        float period = Mathf.Max(0.01f, area10ColorHoldDuration + area10ColorFadeDuration);
                        float t = Time.unscaledTime;
                        int slot = Mathf.FloorToInt(t / period);
                        float localT = t - slot * period;

                        Color cycleColor;
                        if (localT < area10ColorHoldDuration)
                        {
                            cycleColor = GetArea10CycleColor(slot);
                        }
                        else
                        {
                            float ft = Mathf.Clamp01((localT - area10ColorHoldDuration) / area10ColorFadeDuration);
                            cycleColor = Color.Lerp(GetArea10CycleColor(slot), GetArea10CycleColor(slot + 1), ft);
                        }

                        if (o.ring1Image != null) { var c1 = cycleColor; c1.a = orbitRingAlpha; o.ring1Image.color = c1; }
                        if (o.ring2Image != null) { var c2 = cycleColor; c2.a = orbitRingAlpha * 0.65f; o.ring2Image.color = c2; }
                        if (o.ring3Image != null) { var c3 = cycleColor; c3.a = orbitRingAlpha * 0.4f; o.ring3Image.color = c3; }

                        float armPulse = Mathf.Sin(Time.unscaledTime * glowPulseSpeed + o.phase) * glowPulseRange;
                        Color armCore = cycleColor;
                        armCore.a = Mathf.Clamp01(unlockedGlowAlpha + armPulse);
                        if (o.coreImage != null) o.coreImage.color = armCore;

                        if (o.numberText != null) o.numberText.color = new Color(cycleColor.r, cycleColor.g, cycleColor.b, 1f);
                    }
                    else
                    {
                        // 位置合わせが不要な絶対時間ベースなのでEditorプレビューでもズレない
                        float angle1 = (Time.unscaledTime / Mathf.Max(0.01f, orbitPeriod1)) * 360f;
                        float angle2 = -(Time.unscaledTime / Mathf.Max(0.01f, orbitPeriod2)) * 360f;
                        if (o.ring1Wrap != null) o.ring1Wrap.localRotation = Quaternion.Euler(0f, 0f, angle1);
                        if (o.ring2Wrap != null) o.ring2Wrap.localRotation = Quaternion.Euler(0f, 0f, angle2);

                        Color ringColor = o.node.color;
                        ringColor.a = orbitRingAlpha;
                        if (o.ring1Image != null) o.ring1Image.color = ringColor;
                        if (o.ring2Image != null)
                        {
                            Color ring2Color = ringColor;
                            ring2Color.a = orbitRingAlpha * 0.6f;
                            o.ring2Image.color = ring2Color;
                        }

                        float pulse = Mathf.Sin(Time.unscaledTime * glowPulseSpeed + o.phase) * glowPulseRange;
                        Color coreColor = o.node.color;
                        coreColor.a = Mathf.Clamp01(unlockedGlowAlpha + pulse);
                        if (o.coreImage != null) o.coreImage.color = coreColor;

                        if (o.numberText != null) o.numberText.color = new Color(o.node.color.r, o.node.color.g, o.node.color.b, 1f);
                    }
                }
                else
                {
                    if (o.ring1Wrap != null) o.ring1Wrap.localRotation = Quaternion.identity;
                    if (o.ring2Wrap != null) o.ring2Wrap.localRotation = Quaternion.identity;

                    if (o.ring1Image != null) o.ring1Image.color = orbitLockedColor;
                    if (o.ring2Image != null) o.ring2Image.color = orbitLockedColor;
                    if (o.ring3Image != null) o.ring3Image.color = orbitLockedColor;
                    if (o.coreImage != null)
                    {
                        Color coreColor = orbitLockedColor;
                        coreColor.a = lockedGlowAlpha;
                        o.coreImage.color = coreColor;
                    }

                    if (o.numberText != null) o.numberText.color = orbitNumberLockedColor;
                }

                if (o.lockIcon != null && o.lockIcon.activeSelf != !unlocked) o.lockIcon.SetActive(!unlocked);
                if (!unlocked)
                {
                    if (o.lockShackleImage != null) o.lockShackleImage.color = orbitLockIconColor;
                    if (o.lockBodyImage != null) o.lockBodyImage.color = orbitLockIconColor;
                }
            }
        }

#if UNITY_EDITOR
        [ContextMenu("Setup Constellation Layers")]
        private void SetupLayers()
        {
            starLayer = CreateOrGetLayer("StarLayer", 0);
            threadLayer = CreateOrGetLayer("ThreadLayer", 1);
            glowLayer = CreateOrGetLayer("GlowLayer", 2);
            particleLayer = CreateOrGetLayer("ParticleLayer", 3);

            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log("[AreaConstellationFX] レイヤーを生成しました（Star→Thread→Glow→Particleの順で手前に重なります）。nodes配列にエリアボタンとテーマカラーを設定し、「Generate Glow Sprite」も実行してください。");
        }

        private RectTransform CreateOrGetLayer(string name, int siblingIndex)
        {
            var existing = transform.Find(name);
            GameObject go = existing != null ? existing.gameObject : new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(transform, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.SetSiblingIndex(siblingIndex);
            return rt;
        }

        /// <summary>
        /// debugAreaRanksに入力した内容を、ProgressManagerのセーブデータへ直接書き込む（テスト専用）。
        /// UpdateAreaBestRankと違い上位判定を無視するため、降格やクリア（rank空欄）にも使える。
        /// ProgressManager.Instanceが必要なためPlay中のみ実行できる。
        /// </summary>
        [ContextMenu("Apply Debug Ranks (デバッグ用ランクをセーブデータに反映)")]
        private void ApplyDebugRanks()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[AreaConstellationFX] Apply Debug RanksはPlay中のみ実行できます（ProgressManager.Instanceの初期化が必要なため）。");
                return;
            }
            if (ProgressManager.Instance == null)
            {
                Debug.LogWarning("[AreaConstellationFX] ProgressManager.Instanceが見つかりません。");
                return;
            }
            if (debugAreaRanks == null) return;

            foreach (var d in debugAreaRanks)
            {
                if (d == null || string.IsNullOrEmpty(d.areaId)) continue;
                ProgressManager.Instance.DebugSetAreaBestRank(d.areaId, d.rank);
            }
            Debug.Log("[AreaConstellationFX] デバッグ用ランクを反映しました。「Build Constellation」を再実行すると表示に反映されます。");
        }

        /// <summary>
        /// debugBulkRankの値を全エリア（debugAreaRanksの各項目）に一括反映する。
        /// Inspector上のdebugAreaRanksの各Rank欄も同じ値に揃えるため、実際に何が保存されたか見た目でも分かる。
        /// </summary>
        [ContextMenu("Apply Bulk Rank to All Areas (全エリアに同じランクを一括適用)")]
        private void ApplyBulkRank()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[AreaConstellationFX] Apply Bulk Rank to All AreasはPlay中のみ実行できます（ProgressManager.Instanceの初期化が必要なため）。");
                return;
            }
            if (ProgressManager.Instance == null)
            {
                Debug.LogWarning("[AreaConstellationFX] ProgressManager.Instanceが見つかりません。");
                return;
            }
            if (debugAreaRanks == null) return;

            foreach (var d in debugAreaRanks)
            {
                if (d == null || string.IsNullOrEmpty(d.areaId)) continue;
                d.rank = debugBulkRank; // Inspector側の表示も実際の保存値に揃える
                ProgressManager.Instance.DebugSetAreaBestRank(d.areaId, debugBulkRank);
            }
            Debug.Log($"[AreaConstellationFX] 全エリアのランクを \"{debugBulkRank}\" に一括反映しました。「Build Constellation」を再実行すると表示に反映されます。");
        }

        /// <summary>
        /// ノードグロー用の柔らかい円形Spriteを一度だけ生成し、glowSpriteフィールドにアサインする。
        /// 実行時（ビルド後含む）はAssetDatabaseに依存せず、ここで保存済みのSpriteを参照するだけにするための
        /// Editor専用の事前生成ステップ。
        /// </summary>
        [ContextMenu("Generate Glow Sprite (グロー用ソフト円Spriteを生成)")]
        private void GenerateGlowSprite()
        {
            const string assetPath = "Assets/Generated/UI/SoftGlowCircle.png";

            int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Vector2 center = new Vector2(size / 2f, size / 2f);
            float radius = size / 2f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), center) / radius;
                    float a = Mathf.Clamp01(1f - d);
                    a = a * a; // 中心に寄せた柔らかい減衰
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            tex.Apply();

            string dir = Path.GetDirectoryName(assetPath);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllBytes(assetPath, tex.EncodeToPNG());
            UnityEditor.AssetDatabase.ImportAsset(assetPath);

            var importer = (UnityEditor.TextureImporter)UnityEditor.TextureImporter.GetAtPath(assetPath);
            if (importer != null)
            {
                importer.textureType = UnityEditor.TextureImporterType.Sprite;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }

            glowSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log($"[AreaConstellationFX] Glow sprite generated and assigned: {assetPath}");
        }

        /// <summary>
        /// オービットコアの軌道リング用に、中抜きの円（アニュラス）Spriteを一度だけ生成し、ringSpriteフィールドにアサインする。
        /// 内側・外側それぞれをソフトに減衰させることで、細く柔らかいリングに見せる。
        /// </summary>
        [ContextMenu("Generate Ring Sprite (軌道リング用の中抜き円Spriteを生成)")]
        private void GenerateRingSprite()
        {
            const string assetPath = "Assets/Generated/UI/OrbitRing.png";

            int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Vector2 center = new Vector2(size / 2f, size / 2f);
            float outerRadius = size / 2f;
            float innerRadius = outerRadius * 0.8f;
            float feather = outerRadius * 0.06f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), center);
                    float outerA = Mathf.Clamp01((outerRadius - d) / feather);
                    float innerA = Mathf.Clamp01((d - innerRadius) / feather);
                    float a = Mathf.Min(outerA, innerA);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            tex.Apply();

            string dir = Path.GetDirectoryName(assetPath);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllBytes(assetPath, tex.EncodeToPNG());
            UnityEditor.AssetDatabase.ImportAsset(assetPath);

            var ringImporter = (UnityEditor.TextureImporter)UnityEditor.TextureImporter.GetAtPath(assetPath);
            if (ringImporter != null)
            {
                ringImporter.textureType = UnityEditor.TextureImporterType.Sprite;
                ringImporter.alphaIsTransparency = true;
                ringImporter.SaveAndReimport();
            }

            ringSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log($"[AreaConstellationFX] Ring sprite generated and assigned: {assetPath}");
        }

        /// <summary>
        /// 流れる光の粒子を加算合成で描画するためのマテリアルを生成し、additiveGlowMaterialフィールドにアサインする。
        /// 通常のUI（SrcAlpha OneMinusSrcAlpha）ではなく加算合成（SrcAlpha One）にすることで、
        /// モックアップの「globalCompositeOperation = lighter」と同じ、背景に光を足し重ねる発光表現になる。
        /// </summary>
        [ContextMenu("Generate Additive Material (粒子用の加算合成マテリアルを生成)")]
        private void GenerateAdditiveMaterial()
        {
            const string assetPath = "Assets/Generated/UI/UIAdditiveGlow.mat";

            var shader = Shader.Find("UI/Additive");
            if (shader == null)
            {
                Debug.LogError("[AreaConstellationFX] Shader \"UI/Additive\" が見つかりません。Assets/Shaders/UI_Additive.shader が存在するか確認してください。");
                return;
            }

            string dir = Path.GetDirectoryName(assetPath);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            var existing = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            var mat = existing != null ? existing : new Material(shader);
            mat.shader = shader;

            if (existing == null)
            {
                UnityEditor.AssetDatabase.CreateAsset(mat, assetPath);
            }
            else
            {
                UnityEditor.EditorUtility.SetDirty(mat);
            }
            UnityEditor.AssetDatabase.SaveAssets();

            additiveGlowMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log($"[AreaConstellationFX] Additive material generated and assigned: {assetPath}");
        }

        /// <summary>
        /// Play前のEditor上で、星・本線・収束線・流れる粒子・ノードグローを実際に生成してプレビューする。
        /// nodes配列とレイヤー参照を設定した後にこれを実行すれば、Play前のSceneビューで見た目を確認・調整できる。
        /// 再実行すれば既存のものを削除して作り直すので、ボタン位置やテーマカラーを変更した後の再生成にも使える。
        /// </summary>
        [ContextMenu("Build Constellation (Play前プレビュー生成)")]
        private void BuildConstellationPreview()
        {
            BuildAll();
            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log("[AreaConstellationFX] 星座を生成しました。Sceneビューで確認できます。");
        }

        // 「案0：最初のモックアップ再現」の座標（Area10を上に、Area01〜09が三日月状に取り囲む構図）。
        // 1920x1080キャンバス上でタイトル・チュートリアル・右側アイコン列を避けた位置を計算し、
        // AreaButtonsローカルのanchoredPositionに変換済み。
        // ★実機確認の結果、ボタンのAnchorはGridLayoutGroupの初期化により(0,1)＝左上基準になっている
        // （GridLayoutGroupは無効化しても、一度でも動作すると子のAnchorを左上基準に書き換えたまま残すため）。
        // 左下基準だった旧計算からY = Y_old - 591.654（AreaButtonsの高さ）で補正済み。
        // nodes配列の並び順（Element0=Area01, Element1=Area02, …）と対応させて使う。
        private static readonly Vector2[] ScatteredLayoutPositions =
        {
            new Vector2(-190.00f,  -57.20f), // 01
            new Vector2( -62.50f, -349.60f), // 02
            new Vector2(-167.50f, -513.00f), // 03
            new Vector2( 125.00f, -564.60f), // 04
            new Vector2( 380.00f, -603.30f), // 05
            new Vector2( 635.00f, -564.60f), // 06
            new Vector2( 927.50f, -513.00f), // 07
            new Vector2( 822.50f, -349.60f), // 08
            new Vector2( 950.00f,  -57.20f), // 09
        };

        // Area10（未作成）の予約位置。ボタンを追加する際はこの値をそのanchoredPositionとして使う。
        // canvas(850,484) → AreaButtonsローカル (380, -169.00)（左上基準で補正済み）
        private static readonly Vector2 Area10ReservedPosition = new Vector2(380f, -169.00f);

        /// <summary>
        /// nodes配列に設定済みのボタンを、GridLayoutGroupによる3x3整列から解除し、
        /// モックアップの弧状配置に近い散らばった位置へ移動する。
        /// ボタンの見た目（画像・機能）は一切変更しない、位置移動のみの非破壊修正。
        /// GridLayoutGroupは削除せずenabled=falseで無効化するだけなので、元に戻したい場合は再度ONにすればよい。
        /// </summary>
        /// <summary>
        /// ApplyScatteredLayoutで無効化したGridLayoutGroupを再度有効化し、
        /// 元の3x3グリッド配置に戻す（散らばった配置の座標がおかしかった場合の復旧用）。
        /// </summary>
        [ContextMenu("Revert To Grid Layout (グリッド配置に戻す)")]
        private void RevertToGridLayout()
        {
            if (nodes == null || nodes.Length == 0) return;
            GridLayoutGroup grid = null;
            foreach (var n in nodes)
            {
                if (n?.button?.parent == null) continue;
                grid = n.button.parent.GetComponent<GridLayoutGroup>();
                if (grid != null) break;
            }
            if (grid == null)
            {
                Debug.LogError("[AreaConstellationFX] GridLayoutGroup が見つかりませんでした。");
                return;
            }
            grid.enabled = true;
            UnityEditor.EditorUtility.SetDirty(grid);
            Debug.Log("[AreaConstellationFX] GridLayoutGroupを再度有効化しました。元のグリッド配置に戻ります。");
        }

        /// <summary>
        /// ボタン本体（当たり判定用の透明Image）とButtonImage（見た目のアイコン）を、
        /// 将来の結晶サイズを見越して縮小する。
        /// ★重要：ボタン本体はGridLayoutGroupが無効な状態だと元のsizeDelta(0,0)に戻ってしまい、
        /// それにフルストレッチしているLockOverlay/Textも道連れで0サイズになる（＝クリック判定が消える）
        /// 不具合があったため、この処理はその修正も兼ねている。
        /// originalCellSize/originalButtonImageSizeという固定の基準値から毎回計算するため、
        /// 何度再実行しても縮小が重ならない（安全に再実行できる）。
        /// </summary>
        [ContextMenu("Resize Buttons (画像・当たり判定を縮小)")]
        private void ResizeButtons()
        {
            if (nodes == null || nodes.Length == 0)
            {
                Debug.LogError("[AreaConstellationFX] nodes が未設定です。");
                return;
            }

            float scaleX = buttonHitboxSize.x / originalCellSize.x;
            float scaleY = buttonHitboxSize.y / originalCellSize.y;
            int applied = 0;

            foreach (var n in nodes)
            {
                var button = n?.button;
                if (button == null) continue;

                button.sizeDelta = buttonHitboxSize;
                UnityEditor.EditorUtility.SetDirty(button);

                var buttonImage = button.Find("ButtonImage") as RectTransform;
                if (buttonImage != null)
                {
                    buttonImage.sizeDelta = new Vector2(
                        originalButtonImageSize.x * scaleX,
                        originalButtonImageSize.y * scaleY);
                    UnityEditor.EditorUtility.SetDirty(buttonImage);
                }
                applied++;
            }

            Debug.Log($"[AreaConstellationFX] {applied}個のボタンを縮小しました。当たり判定={buttonHitboxSize}、ButtonImage縮小率=({scaleX:F2}, {scaleY:F2})。" +
                      "LockOverlay/Textはボタン本体にフルストレッチしているため自動的に追従します。");
        }

        /// <summary>
        /// nodes[8]（Area09ボタン想定）を複製してArea10ボタンを作成する。
        /// ★見た目（ButtonImageのスプライト）はArea09と同じものを流用し、金色に色を変えて仮の区別としている。
        /// 専用アートができたら差し替えが必要。
        /// 位置はArea10ReservedPositionを使用し、サイズもResizeButtonsと同じbuttonHitboxSizeに揃える。
        /// 生成後、nodes配列の末尾に自動で追加する（Element9として登録）。
        /// </summary>
        [ContextMenu("Create Area10 Button (Btn_Area_09を複製して作成)")]
        private void CreateArea10Button()
        {
            if (nodes == null || nodes.Length < 9 || nodes[8]?.button == null)
            {
                Debug.LogError("[AreaConstellationFX] nodes[8]（Area09ボタン）が未設定です。複製元が必要です。");
                return;
            }

            var template = nodes[8].button;
            var parent = template.parent;

            var existing = parent.Find("Btn_Area_10");
            if (existing != null)
            {
                Debug.LogWarning("[AreaConstellationFX] Btn_Area_10 は既に存在します。処理をスキップします。");
                return;
            }

            var newObj = Instantiate(template.gameObject, parent);
            newObj.name = "Btn_Area_10";
            var newRect = newObj.GetComponent<RectTransform>();
            newRect.anchoredPosition = Area10ReservedPosition;
            newRect.sizeDelta = buttonHitboxSize;

            // StageButton設定
            var stageButton = newObj.GetComponent<StageButton>();
            if (stageButton != null)
            {
                stageButton.areaId = "Area_10";
                stageButton.stageNumber = 1;
            }

            // Button.onClick: 複製元(SelectArea9)の持続的リスナーを消し、SelectArea10を新たに登録
            var button = newObj.GetComponent<Button>();
            if (button != null)
            {
                while (button.onClick.GetPersistentEventCount() > 0)
                {
                    UnityEditor.Events.UnityEventTools.RemovePersistentListener(button.onClick, 0);
                }
                var areaSelectManager = FindObjectOfType<AreaSelectManager>();
                if (areaSelectManager != null)
                {
                    UnityEditor.Events.UnityEventTools.AddPersistentListener(button.onClick, areaSelectManager.SelectArea10);
                }
                else
                {
                    Debug.LogWarning("[AreaConstellationFX] AreaSelectManagerが見つからず、onClickの再設定ができませんでした。手動で設定してください。");
                }
            }

            // 見た目：ButtonImageのスプライトには"09"の数字が描き込まれているため、
            // スプライト参照を外して単色パネルにする（sprite=nullでも白い矩形として描画される）。金色に着色。
            var buttonImage = newRect.Find("ButtonImage")?.GetComponent<Image>();
            if (buttonImage != null)
            {
                buttonImage.sprite = null;
                buttonImage.color = new Color(0.91f, 0.79f, 0.42f, 1f); // 金色（仮）
            }

            // 番号ラベルを "10" に変更（Text (Legacy) が本来の番号表示。DebugTextはStageButton.Start()が上書きするので触らない）
            var legacyText = newRect.Find("Text (Legacy)")?.GetComponent<Text>();
            if (legacyText != null) legacyText.text = "10";

            // nodes配列を更新：既にArea_10のエントリ（前回の作り直しでボタン参照がMissingになったもの等）が
            // あればそれを置き換える。無ければ末尾に追加する（配列を複製しない・重複させない）。
            var newAreaNode = new AreaNode
            {
                button = newRect,
                areaId = "Area_10",
                color = new Color(0.91f, 0.79f, 0.42f, 1f)
            };

            int existingIndex = -1;
            for (int i = 0; i < nodes.Length; i++)
            {
                if (nodes[i] != null && nodes[i].areaId == "Area_10")
                {
                    existingIndex = i;
                    break;
                }
            }

            if (existingIndex >= 0)
            {
                nodes[existingIndex] = newAreaNode;
                // 万が一Area_10のエントリが複数残っていた場合は、2つ目以降を除去する
                var cleaned = new List<AreaNode>();
                bool keptFirst = false;
                foreach (var n in nodes)
                {
                    if (n != null && n.areaId == "Area_10")
                    {
                        if (keptFirst) continue;
                        keptFirst = true;
                    }
                    cleaned.Add(n);
                }
                nodes = cleaned.ToArray();
            }
            else
            {
                var newNodes = new AreaNode[nodes.Length + 1];
                System.Array.Copy(nodes, newNodes, nodes.Length);
                newNodes[nodes.Length] = newAreaNode;
                nodes = newNodes;
            }

            UnityEditor.EditorUtility.SetDirty(this);
            UnityEditor.EditorUtility.SetDirty(newObj);
            Debug.Log("[AreaConstellationFX] Btn_Area_10を作成し、nodes配列に追加しました。見た目はArea09流用＋金色着色の仮状態です。専用アート差し替えが今後必要です。");
        }

        [ContextMenu("Apply Scattered Layout (グリッド解除・散らばった配置に変更)")]
        private void ApplyScatteredLayout()
        {
            if (nodes == null || nodes.Length == 0)
            {
                Debug.LogError("[AreaConstellationFX] nodes が未設定です。");
                return;
            }

            bool gridDisabled = false;
            for (int i = 0; i < nodes.Length; i++)
            {
                var button = nodes[i]?.button;
                if (button == null) continue;

                var grid = button.parent != null ? button.parent.GetComponent<GridLayoutGroup>() : null;
                if (grid != null && grid.enabled)
                {
                    grid.enabled = false;
                    UnityEditor.EditorUtility.SetDirty(grid);
                    gridDisabled = true;
                }

                if (i < ScatteredLayoutPositions.Length)
                {
                    button.anchoredPosition = ScatteredLayoutPositions[i];
                    UnityEditor.EditorUtility.SetDirty(button);
                }
                else
                {
                    Debug.LogWarning($"[AreaConstellationFX] nodes[{i}]（{button.name}）に対応する配置座標がありません（ScatteredLayoutPositionsは9個分のみ）。");
                }
            }

            Debug.Log($"[AreaConstellationFX] 散らばった配置を適用しました（GridLayoutGroup無効化: {gridDisabled}）。ボタンのクリック・ロック表示が正常に動くか確認してください。");
        }
#endif
    }
}
