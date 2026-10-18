using UnityEngine;

/// <summary>
/// Main Camera に付ける。画面が ScreenAspectSettings の「最小の比率」より縦長の時、カメラの映す範囲を上下中央の帯に絞る（上下に黒帯）。
/// 帯の中は「最小の比率」の画面と同じ範囲・同じ見た目になる（Camera.aspect も最小の比率になるため、比率を使う既存の計算はそのまま正しく動く）。
/// 黒帯の部分は barsCamera（何も映さず塗りつぶすだけのカメラ）が塗る。barsCamera は黒帯が要る時だけ有効にする。
/// 部品はメニュー「Tools/画面比率/黒帯の仕組みを全シーンに組み込む」で配置する。
/// </summary>
[RequireComponent(typeof(Camera))]
public class AspectLetterboxCamera : MonoBehaviour
{
    [Tooltip("黒帯の部分を塗りつぶすカメラ（メニューで作成・設定。何も映さない・Main Cameraより先に描画）")]
    [SerializeField] private Camera barsCamera;

    private Camera _cam;
    private int _lastW = -1, _lastH = -1;
    private bool _lastOn;

    private void Awake()
    {
        _cam = GetComponent<Camera>();
        Apply(true);
    }

    private void Update()
    {
        Apply(false);
    }

    private void OnDisable()
    {
        // 部品を外した・無効にした時は元の全画面に戻す
        if (_cam != null) _cam.rect = new Rect(0f, 0f, 1f, 1f);
        if (barsCamera != null) barsCamera.enabled = false;
        _lastW = -1;
    }

    private void Apply(bool force)
    {
        if (_cam == null) return;
        int w = Screen.width, h = Screen.height;
        bool on = ScreenAspectSettings.TryGetLetterbox(w, h, out Rect viewport);
        if (!force && w == _lastW && h == _lastH && on == _lastOn) return;
        _lastW = w; _lastH = h; _lastOn = on;

        _cam.rect = viewport;
        if (barsCamera != null)
        {
            barsCamera.enabled = on;
            var s = ScreenAspectSettings.Instance;
            if (s != null) barsCamera.backgroundColor = s.barColor;
        }
    }
}
