using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// タイトル画面の中間レイヤー演出。彗星が楕円軌道のパスに沿って移動しながらドットを生成し続け、
    /// 「線を描いて弾を反射する」というゲームの核を、実際のゲーム内の線(PaddleDot)と同じ考え方で表現する。
    /// 各ドットは実際のPaddleDotと同様に生成時刻を記録し、独立したLifeTimeで自動的に消えるため、
    /// 特別な処理をしなくても自然に「後ろ(最初に描いた側)から消えていく」動きになる。
    /// 軌道は複数の型(1本/2本交差/3本の複雑な絡み合い)からランダムに選び、
    /// 中心・半径・回転角度・弧の範囲もランダム化することで毎回違う形になるようにしている。
    /// 静止画は使わず、完全にコード生成のみで描画する。
    /// </summary>
    public class TitleOrbitTrailFX : MonoBehaviour
    {
        private enum PathPattern { Single, Cross, Complex }

        [Header("発生間隔")]
        [Tooltip("次の軌道が発生するまでの待ち時間(秒)の範囲")]
        [SerializeField] private float spawnIntervalMin = 4f;
        [SerializeField] private float spawnIntervalMax = 9f;

        [Header("軌道パターンの出現確率")]
        [Tooltip("Single(1本) / Cross(2本交差) / Complex(3本絡み合い)の出現重み。合計が0でなければ比率で正規化される")]
        [SerializeField] private float singleWeight = 1f;
        [SerializeField] private float crossWeight = 1.4f;
        [SerializeField] private float complexWeight = 0.8f;

        [Header("楕円のサイズ・位置(画面基準、Canvas論理解像度に対する割合)")]
        [SerializeField] private float centerXRangeRatio = 0.5f;
        [SerializeField] private float centerYRangeRatio = 0.32f;
        [SerializeField] private float radiusXMinRatio = 0.28f;
        [SerializeField] private float radiusXMaxRatio = 0.46f;
        [SerializeField] private float radiusYMinRatio = 0.16f;
        [SerializeField] private float radiusYMaxRatio = 0.30f;

        [Header("弧の範囲・移動")]
        [Tooltip("弧として使う角度幅(度)の範囲。360未満にすることで閉じた円ではなく開いた弧になる")]
        [SerializeField] private float arcSpanDegMin = 160f;
        [SerializeField] private float arcSpanDegMax = 300f;
        [Tooltip("彗星が弧を描き切るまでの時間(秒)の範囲")]
        [SerializeField] private float traceDurationMin = 3.5f;
        [SerializeField] private float traceDurationMax = 6.5f;
        [Tooltip("複数本の弧が同時発生するとき、次の弧の開始をどれだけ遅らせるか(秒)")]
        [SerializeField] private float multiArcStagger = 0.35f;

        [Header("ドット(実際の線のPaddleDotと同じ考え方)")]
        [Tooltip("ドットを生成する間隔(px、移動距離基準)。時間基準だと彗星の速度が速い区間で間隔が開いて隙間になるため、" +
            "移動距離で判定する。小さいほど滑らかな線になる")]
        [SerializeField] private float dotSpawnDistance = 6f;
        [Tooltip("1個のドットが生成されてから消えるまでの寿命(秒)。線の実際の長さを決める")]
        [SerializeField] private float dotLifeTime = 1.4f;
        [Tooltip("消える直前のフェードアウト時間(秒)")]
        [SerializeField] private float dotFadeOutTime = 0.25f;
        [SerializeField] private float dotSize = 10f;
        [Range(0f, 1f)]
        [SerializeField] private float dotAlpha = 0.85f;

        [Header("彗星の頭")]
        [SerializeField] private float headSize = 16f;
        [Range(0f, 1f)]
        [SerializeField] private float headAlpha = 1f;

        [Header("色 (虹色グラデーション、Area1〜10と同じ配色)")]
        [SerializeField]
        private Color[] rainbowColors = new Color[]
        {
            new Color(0.608f, 0.561f, 0.780f, 1f),
            new Color(0.298f, 0.686f, 0.490f, 1f),
            new Color(0.553f, 0.600f, 0.682f, 1f),
            new Color(0.878f, 0.478f, 0.247f, 1f),
            new Color(0.698f, 0.227f, 0.322f, 1f),
            new Color(0.878f, 0.690f, 0.310f, 1f),
            new Color(0.310f, 0.561f, 0.878f, 1f),
            new Color(0.373f, 0.839f, 0.839f, 1f),
            new Color(0.639f, 0.682f, 0.878f, 1f),
            new Color(0.910f, 0.788f, 0.416f, 1f),
        };

        [Header("素材(SoftGlowCircle/UIAdditiveGlow。Setup Orbit Trail FXで自動アサイン)")]
        [SerializeField] private Sprite glowSprite;
        [SerializeField] private Material additiveGlowMaterial;

        private RectTransform layer;
        private float canvasWidth = 1920f;
        private float canvasHeight = 1080f;

        private void Awake()
        {
            layer = GetComponent<RectTransform>();
        }

        private void OnEnable()
        {
            if (!Application.isPlaying) return;

            var canvas = GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                var r = canvas.GetComponent<RectTransform>().rect;
                canvasWidth = r.width;
                canvasHeight = r.height;
            }

            StartCoroutine(SpawnLoop());
        }

        private IEnumerator SpawnLoop()
        {
            while (true)
            {
                float wait = Random.Range(spawnIntervalMin, spawnIntervalMax);
                yield return new WaitForSecondsRealtime(wait);

                StartCoroutine(SpawnOneOrbit());
            }
        }

        private IEnumerator SpawnOneOrbit()
        {
            PathPattern pattern = PickPattern();
            int arcCount = pattern == PathPattern.Single ? 1 : (pattern == PathPattern.Cross ? 2 : 3);

            for (int i = 0; i < arcCount; i++)
            {
                StartCoroutine(TraceOneArc());
                if (arcCount > 1) yield return new WaitForSecondsRealtime(multiArcStagger);
            }
        }

        private PathPattern PickPattern()
        {
            float total = Mathf.Max(0.0001f, singleWeight + crossWeight + complexWeight);
            float r = Random.value * total;
            if (r < singleWeight) return PathPattern.Single;
            r -= singleWeight;
            if (r < crossWeight) return PathPattern.Cross;
            return PathPattern.Complex;
        }

        private IEnumerator TraceOneArc()
        {
            // ★毎回パラメータをランダム化することで、同じ軌道が繰り返し出ないようにする
            Vector2 center = new Vector2(
                Random.Range(-canvasWidth * centerXRangeRatio * 0.5f, canvasWidth * centerXRangeRatio * 0.5f),
                Random.Range(-canvasHeight * centerYRangeRatio * 0.5f, canvasHeight * centerYRangeRatio * 0.5f));
            float radiusX = Random.Range(canvasWidth * radiusXMinRatio, canvasWidth * radiusXMaxRatio);
            float radiusY = Random.Range(canvasHeight * radiusYMinRatio, canvasHeight * radiusYMaxRatio);
            float rotationDeg = Random.Range(0f, 360f);
            float startAngle = Random.Range(0f, 360f);
            float arcSpan = Random.Range(arcSpanDegMin, arcSpanDegMax);
            if (Random.value < 0.5f) arcSpan = -arcSpan; // 進行方向(時計回り/反時計回り)もランダム
            float endAngle = startAngle + arcSpan;

            float duration = Random.Range(traceDurationMin, traceDurationMax);

            // ★弧に沿って進むほど虹色が推移するよう、開始色をランダムに選び、そこから数色分先の色へ遷移させる
            int colorStartIdx = Random.Range(0, rainbowColors.Length);
            int colorSpan = Random.Range(3, rainbowColors.Length);

            float elapsed = 0f;
            GameObject headGo = CreateGlowObject("OrbitCometHead", headSize);
            var headRt = (RectTransform)headGo.transform;
            var headImg = headGo.GetComponent<Image>();

            // ★時間ではなく移動距離でドット生成を判定する(彗星の速度が変化しても間隔が一定になる)
            Vector2 lastDotPos = EllipsePoint(center, radiusX, radiusY, rotationDeg, startAngle);
            Color lastDotColor = SampleRainbow(colorStartIdx, colorSpan, 0f);
            bool firstDotSpawned = false;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float rawT = Mathf.Clamp01(elapsed / duration);
                float t = 1f - (1f - rawT) * (1f - rawT); // ease-out

                float angleDeg = Mathf.Lerp(startAngle, endAngle, t);
                Vector2 pos = EllipsePoint(center, radiusX, radiusY, rotationDeg, angleDeg);
                Color col = SampleRainbow(colorStartIdx, colorSpan, t);

                headRt.anchoredPosition = pos;
                float fadeRatio = 0.08f;
                float headFade = Mathf.Clamp01(Mathf.Min(1f, rawT / fadeRatio, (1f - rawT) / fadeRatio));
                Color hc = col; hc.a = headAlpha * headFade;
                headImg.color = hc;

                if (!firstDotSpawned)
                {
                    firstDotSpawned = true;
                    lastDotPos = pos;
                    lastDotColor = col;
                    StartCoroutine(SpawnOrbitDot(pos, col));
                }
                else
                {
                    float dist = Vector2.Distance(pos, lastDotPos);
                    if (dist >= dotSpawnDistance)
                    {
                        // ★ease-outの序盤(速度が速い区間)は1フレームでdotSpawnDistanceの何倍も進むことがあり、
                        //   そのまま1個だけ生成すると実質「1フレームの移動距離」が間隔になって隙間ができる。
                        //   前回位置との間を補間して、必要な個数を一度に生成することで間隔を一定に保つ。
                        int count = Mathf.Max(1, Mathf.FloorToInt(dist / dotSpawnDistance));
                        for (int i = 1; i <= count; i++)
                        {
                            float frac = (float)i / count;
                            Vector2 interpPos = Vector2.Lerp(lastDotPos, pos, frac);
                            Color interpCol = Color.Lerp(lastDotColor, col, frac);
                            StartCoroutine(SpawnOrbitDot(interpPos, interpCol));
                        }
                        lastDotPos = pos;
                        lastDotColor = col;
                    }
                }

                yield return null;
            }

            if (headGo != null) Destroy(headGo);
        }

        /// <summary>
        /// 実際のPaddleDotと同じ考え方：生成時刻を記録し、独立したLifeTimeが経過したら
        /// (少しフェードアウトしてから)自動的に消える。時間差で生成される多数のドットが
        /// それぞれ独立して寿命を迎えるため、特別な制御なしで「後ろ(最初に描いた側)から消える」動きになる。
        /// </summary>
        private IEnumerator SpawnOrbitDot(Vector2 pos, Color color)
        {
            GameObject go = CreateGlowObject("OrbitTrailDot", dotSize);
            var rt = (RectTransform)go.transform;
            rt.anchoredPosition = pos;
            var img = go.GetComponent<Image>();
            color.a = dotAlpha;
            img.color = color;

            float fadeStart = Mathf.Max(0.01f, dotLifeTime - dotFadeOutTime);
            float elapsed = 0f;
            while (elapsed < dotLifeTime)
            {
                elapsed += Time.unscaledDeltaTime;
                if (elapsed > fadeStart)
                {
                    float fadeT = Mathf.Clamp01((elapsed - fadeStart) / dotFadeOutTime);
                    Color c = color;
                    c.a = dotAlpha * (1f - fadeT);
                    img.color = c;
                }
                yield return null;
            }

            if (go != null) Destroy(go);
        }

        private GameObject CreateGlowObject(string name, float size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.layer = layer.gameObject.layer;
            var rt = (RectTransform)go.transform;
            rt.SetParent(layer, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(size, size);

            var img = go.GetComponent<Image>();
            img.raycastTarget = false;
            if (glowSprite != null) img.sprite = glowSprite;
            if (additiveGlowMaterial != null) img.material = additiveGlowMaterial;

            return go;
        }

        private static Vector2 EllipsePoint(Vector2 center, float radiusX, float radiusY, float rotationDeg, float angleDeg)
        {
            float rad = angleDeg * Mathf.Deg2Rad;
            float x = radiusX * Mathf.Cos(rad);
            float y = radiusY * Mathf.Sin(rad);
            float rot = rotationDeg * Mathf.Deg2Rad;
            float rx = x * Mathf.Cos(rot) - y * Mathf.Sin(rot);
            float ry = x * Mathf.Sin(rot) + y * Mathf.Cos(rot);
            return center + new Vector2(rx, ry);
        }

        private Color SampleRainbow(int startIdx, int span, float t)
        {
            if (rainbowColors == null || rainbowColors.Length == 0) return Color.white;
            float pos = t * span;
            int i0 = (startIdx + Mathf.FloorToInt(pos)) % rainbowColors.Length;
            int i1 = (i0 + 1) % rainbowColors.Length;
            float frac = pos - Mathf.Floor(pos);
            return Color.Lerp(rainbowColors[i0], rainbowColors[i1], frac);
        }
    }
}
