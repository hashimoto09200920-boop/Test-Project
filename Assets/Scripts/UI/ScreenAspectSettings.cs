using UnityEngine;

/// <summary>
/// 画面の比率の対応（黒帯）の設定。Assets/Resources/ScreenAspectSettings.asset に置く（AspectLetterboxCamera・CanvasLetterboxFit が実行時に読む）。
/// 画面が「最小の比率」より縦長（16:9・タブレット等）の時は、ゲームの映像とUIを「最小の比率」の見た目のまま縮め、上下に黒帯を付ける。
/// それ以上横長の画面（20:9等）では何も変えない。
/// メニュー「Tools/画面比率/黒帯の仕組みを全シーンに組み込む」で作成・組み込みを行う。
/// </summary>
[CreateAssetMenu(menuName = "Game/Screen Aspect Settings", fileName = "ScreenAspectSettings")]
public class ScreenAspectSettings : ScriptableObject
{
    private const string ResourcePath = "ScreenAspectSettings";

    [Tooltip("OFFにすると黒帯を付けない（従来どおり、縦長の画面では左右が切れる）")]
    public bool enabledLetterbox = true;

    [Tooltip("これより縦長の画面では上下に黒帯を付ける（横÷縦）。2.0＝2:1。ゲームの中身・UIはこの比率の時と同じ見た目になる")]
    [Min(1f)] public float minAspect = 2f;

    [Tooltip("黒帯の色")]
    public Color barColor = Color.black;

    private static ScreenAspectSettings _cached;
    private static bool _loaded;

    /// <summary>設定アセット（無ければnull＝黒帯なし）</summary>
    public static ScreenAspectSettings Instance
    {
        get
        {
            if (!_loaded || (_cached == null && Application.isEditor))
            {
                _cached = Resources.Load<ScreenAspectSettings>(ResourcePath);
                _loaded = true;
            }
            return _cached;
        }
    }

    /// <summary>
    /// 今の画面で黒帯を付けるか。付ける時は、ゲームの映像を映す範囲（画面に対する割合。横幅いっぱい・上下中央）を返す
    /// </summary>
    public static bool TryGetLetterbox(int screenWidth, int screenHeight, out Rect viewport)
    {
        viewport = new Rect(0f, 0f, 1f, 1f);
        var s = Instance;
        if (s == null || !s.enabledLetterbox || screenWidth <= 0 || screenHeight <= 0) return false;
        float aspect = (float)screenWidth / screenHeight;
        if (aspect >= s.minAspect - 0.0001f) return false;
        float h = aspect / s.minAspect; // 映す範囲の高さ（画面に対する割合）
        viewport = new Rect(0f, (1f - h) * 0.5f, 1f, h);
        return true;
    }
}
