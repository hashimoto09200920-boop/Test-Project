using TMPro;
using UnityEngine;

namespace Game.Shop
{
    /// <summary>
    /// 課金ショップのタイトル文字（プリズム・バザール）の演出（2026/10/23）。
    /// ・1文字ずつビビッドな虹色（ピンク→オレンジ→黄→緑→水色→青→紫）に塗り、色がゆっくり流れる（2026/10/24 案B）
    /// ・ときどき白い光の筋が左から右へ横切る
    /// 光る縁取り（グロー）は文字の素材（Assets/Art/AreaSelect/Shop/PrismBazaarTitle.mat）で付ける。
    /// エディタでも Play 前に見た目を確認できる（ExecuteAlways）。
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(TMP_Text))]
    public class IapShopTitleFX : MonoBehaviour
    {
        [Header("虹色（左から順に1文字ずつ。足りなければ繰り返す）")]
        [SerializeField] private Color[] colors = DefaultColors();
        [Tooltip("色を明るくする量（0＝そのまま、1＝白）")]
        [Range(0f, 1f)] [SerializeField] private float brighten = 0.1f;

        /// <summary>ビビッドな虹色（案B）：ピンク→オレンジ→黄→緑→水色→青→紫</summary>
        public static Color[] DefaultColors() => new Color[]
        {
            new Color32(0xFF, 0x4F, 0xD8, 0xFF), new Color32(0xFF, 0x8A, 0x3D, 0xFF), new Color32(0xFF, 0xE1, 0x4D, 0xFF),
            new Color32(0x5D, 0xFF, 0x7A, 0xFF), new Color32(0x3D, 0xE8, 0xFF, 0xFF), new Color32(0x6B, 0x8C, 0xFF, 0xFF),
            new Color32(0xB2, 0x6B, 0xFF, 0xFF),
        };
        public const float DefaultBrighten = 0.1f;
        [Tooltip("色が流れる速さ（1秒に何文字分ずれるか。0＝止める）")]
        [SerializeField] private float colorScrollSpeed = 0.8f;
        [Tooltip("1文字の中で左右の色を隣の文字へなめらかにつなぐ")]
        [SerializeField] private bool smoothGradient = true;

        [Header("光の筋")]
        [SerializeField] private bool sweepEnabled = true;
        [Tooltip("光の筋が横切る間隔（秒）")]
        [Min(0.1f)] [SerializeField] private float sweepInterval = 3f;
        [Tooltip("光の筋が左端から右端まで進む時間（秒）")]
        [Min(0.05f)] [SerializeField] private float sweepDuration = 0.7f;
        [Tooltip("光の筋の幅（文字列の幅に対する割合）")]
        [Range(0.02f, 1f)] [SerializeField] private float sweepWidth = 0.18f;
        [Tooltip("光の筋の明るさ（1＝真っ白）")]
        [Range(0f, 1f)] [SerializeField] private float sweepStrength = 0.85f;

        private TMP_Text text;

        private void OnEnable()
        {
            text = GetComponent<TMP_Text>();
        }

        private void OnDisable()
        {
            if (text != null) text.ForceMeshUpdate(); // 元の色に戻す
        }

        private void LateUpdate()
        {
            if (text == null || colors == null || colors.Length == 0) return;
            text.ForceMeshUpdate();
            var info = text.textInfo;
            if (info.characterCount == 0) return;

            float t = Application.isPlaying ? Time.unscaledTime : (float)UnityEditor_Time();
            float shift = t * colorScrollSpeed;

            // 光の筋の位置（文字列の左端0〜右端1。範囲外＝出ていない）
            float minX = float.MaxValue, maxX = float.MinValue;
            for (int i = 0; i < info.characterCount; i++)
            {
                var ch = info.characterInfo[i];
                if (!ch.isVisible) continue;
                minX = Mathf.Min(minX, ch.bottomLeft.x);
                maxX = Mathf.Max(maxX, ch.topRight.x);
            }
            float width = Mathf.Max(1f, maxX - minX);
            float sweepPos = -10f;
            if (sweepEnabled)
            {
                float phase = t % sweepInterval;
                if (phase < sweepDuration) sweepPos = Mathf.Lerp(-sweepWidth, 1f + sweepWidth, phase / sweepDuration);
            }

            int visibleIndex = 0;
            for (int i = 0; i < info.characterCount; i++)
            {
                var ch = info.characterInfo[i];
                if (!ch.isVisible) continue;
                var vertexColors = info.meshInfo[ch.materialReferenceIndex].colors32;
                var verts = info.meshInfo[ch.materialReferenceIndex].vertices;
                int v = ch.vertexIndex;
                Color left = ColorAt(visibleIndex + shift);
                Color right = smoothGradient ? ColorAt(visibleIndex + 1 + shift) : left;
                // 頂点の並び：0=左下 1=左上 2=右上 3=右下
                for (int k = 0; k < 4; k++)
                {
                    Color c = k < 2 ? left : right;
                    float x01 = (verts[v + k].x - minX) / width;
                    float d = Mathf.Abs(x01 - sweepPos) / sweepWidth;
                    if (d < 1f) c = Color.Lerp(c, Color.white, (1f - d) * sweepStrength);
                    c.a = vertexColors[v + k].a / 255f;
                    vertexColors[v + k] = c;
                }
                visibleIndex++;
            }
            text.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
        }

        private Color ColorAt(float index)
        {
            int n = colors.Length;
            float f = Mathf.Repeat(index, n);
            int a = Mathf.FloorToInt(f) % n;
            int b = (a + 1) % n;
            Color c = Color.Lerp(colors[a], colors[b], f - Mathf.Floor(f));
            return Color.Lerp(c, Color.white, brighten);
        }

        private static double UnityEditor_Time()
        {
#if UNITY_EDITOR
            return UnityEditor.EditorApplication.timeSinceStartup;
#else
            return Time.unscaledTime;
#endif
        }

#if UNITY_EDITOR
        // Play 前でも Scene/Game ビューで色が流れる様子を見られるよう、エディタでは毎フレーム再描画する
        private void OnValidate() { if (!Application.isPlaying) UnityEditor.EditorApplication.QueuePlayerLoopUpdate(); }
        private void Update() { if (!Application.isPlaying) UnityEditor.EditorApplication.QueuePlayerLoopUpdate(); }
#endif
    }
}
