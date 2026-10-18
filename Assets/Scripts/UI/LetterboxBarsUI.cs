using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 黒帯（画面が ScreenAspectSettings の「最小の比率」より縦長の時の上下の帯）を、全てのUIより手前に黒で重ねる。
/// 画面全体に広がるUIの背景（ジェム画面・ドリンク画面・タイトル等）も帯の部分は隠れ、どのシーンも05_Gameと同じ見た目（上下が黒帯）になる。
/// 帯の上をタップしても裏のUIに反応しない（Raycast Target ON）。黒帯が要らない画面では帯を出さない。
/// 部品はメニュー「Tools/画面比率/黒帯の仕組みを全シーンに組み込む」で各シーンに配置する（Canvas・BarTop・BarBottom）。
/// </summary>
[RequireComponent(typeof(Canvas))]
public class LetterboxBarsUI : MonoBehaviour
{
    [Tooltip("上の帯（メニューで自動設定）")]
    [SerializeField] private Image barTop;
    [Tooltip("下の帯（メニューで自動設定）")]
    [SerializeField] private Image barBottom;

    private int _lastW = -1, _lastH = -1;
    private bool _lastOn = true;

    private void Awake()
    {
        Apply(true);
    }

    private void Update()
    {
        Apply(false);
    }

    private void Apply(bool force)
    {
        int w = Screen.width, h = Screen.height;
        bool on = ScreenAspectSettings.TryGetLetterbox(w, h, out Rect viewport);
        if (!force && w == _lastW && h == _lastH && on == _lastOn) return;
        _lastW = w; _lastH = h; _lastOn = on;

        float bar = on ? viewport.y : 0f; // 帯1本の高さ（画面に対する割合）
        var s = ScreenAspectSettings.Instance;
        Color color = s != null ? s.barColor : Color.black;
        SetBar(barTop, on, new Vector2(0f, 1f - bar), Vector2.one, color);
        SetBar(barBottom, on, Vector2.zero, new Vector2(1f, bar), color);
    }

    private static void SetBar(Image img, bool on, Vector2 anchorMin, Vector2 anchorMax, Color color)
    {
        if (img == null) return;
        img.gameObject.SetActive(on);
        if (!on) return;
        var rt = img.rectTransform;
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        img.color = color;
    }
}
