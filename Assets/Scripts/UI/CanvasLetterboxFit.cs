using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 画面全体に出すCanvas（CanvasScaler が Scale With Screen Size のもの）に付ける。
/// 画面が ScreenAspectSettings の「最小の比率」より縦長の時、UIの大きさを「同じ横幅で最小の比率の画面」の時と同じにする
/// （＝ゲームの映像と同じ比率で縮み、右端・左端のUIも画面内に収まる）。
/// それ以外の時は CanvasScaler のInspectorの設定どおり（何も変えない）。
/// CanvasScaler の設定値は変えずに読むだけ。黒帯の間だけ、計算した倍率を一時的に Constant Pixel Size で当てる。
///
/// 画面全体に重ねるCanvas（Screen Space - Overlay）は、黒帯の間だけ、上端・下端を基準にしたUIの基準（アンカーの縦）を帯の上端・下端に移す
/// （＝黒帯にはみ出さず、最小の比率の画面と同じ見た目になる）。位置の値は変えないので、UIを動かす演出はそのまま動く。
/// 画面全体を覆うもの（縦に画面いっぱいに伸びる暗幕・フェード等）は対象外で、黒帯まで含めて覆う（その中の子は対象）。
/// カメラに映すCanvas（Screen Space - Camera）は、カメラと一緒に帯の中に収まるため、アンカーは変えない。
/// </summary>
[RequireComponent(typeof(CanvasScaler))]
[DefaultExecutionOrder(-1000)] // 他のスクリプトがCanvasの大きさを測るより先に倍率を当てる（画面を開いた最初の1フレームのずれを防ぐ）
public class CanvasLetterboxFit : MonoBehaviour
{
    [Tooltip("黒帯の間、ゲーム中に後から出てきたUIを確認する間隔（秒・実時間）")]
    [SerializeField] private float rescanInterval = 1f;

    private CanvasScaler _scaler;
    private Canvas _canvas;
    private CanvasScaler.ScaleMode _origMode;
    private float _origScaleFactor;
    private bool _overriding;

    // アンカーの縦を移したUI：元の値（min.y, max.y）と、移した後の値
    private struct Remap { public float origMin, origMax, appliedMin, appliedMax; }
    private readonly Dictionary<RectTransform, Remap> _remapped = new Dictionary<RectTransform, Remap>();
    private float _bandHeight = -1f;
    private float _nextScanTime;
    private void Awake()
    {
        _scaler = GetComponent<CanvasScaler>();
        _canvas = GetComponent<Canvas>();

        // ★画面を開いた時点で倍率を当て、Canvasの大きさをその場で確定させる。
        //   最初のUpdateまで待つと、その間に他のスクリプト（タイトルの星空など）が切り替え前の大きさで配置してしまい、1フレームだけ崩れる
        Apply();
        if (_overriding && _canvas != null)
        {
            _canvas.scaleFactor = _scaler.scaleFactor;
            Canvas.ForceUpdateCanvases();
        }
    }

    private void Update()
    {
        Apply();
    }

    private void Apply()
    {
        if (_scaler == null) return;
        int w = Screen.width, h = Screen.height;
        if (ScreenAspectSettings.TryGetLetterbox(w, h, out Rect viewport))
        {
            if (!_overriding)
            {
                if (_scaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize) return; // 対象外の設定
                _origMode = _scaler.uiScaleMode;
                _origScaleFactor = _scaler.scaleFactor;
                _overriding = true;
            }
            _scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            _scaler.scaleFactor = ScaleFor(w, w / ScreenAspectSettings.Instance.minAspect);

            if (_canvas != null && _canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                bool bandChanged = !Mathf.Approximately(_bandHeight, viewport.height);
                if (bandChanged || Time.unscaledTime >= _nextScanTime)
                {
                    _bandHeight = viewport.height;
                    _nextScanTime = Time.unscaledTime + Mathf.Max(0.1f, rescanInterval);
                    RemapChildren(transform, bandChanged);
                }
            }
        }
        else
        {
            Restore();
        }
    }

    private void OnDisable()
    {
        Restore();
    }

    private void Restore()
    {
        RestoreAnchors();
        if (!_overriding || _scaler == null) return;
        _scaler.uiScaleMode = _origMode;
        _scaler.scaleFactor = _origScaleFactor;
        _overriding = false;
    }

    // 縦に画面いっぱいに伸びるもの（入れ物・暗幕）はそのままにして中を見る。それ以外はアンカーの縦を帯に移し、中は親と一緒に動くので見ない
    private void RemapChildren(Transform parent, bool bandChanged)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            var rt = parent.GetChild(i) as RectTransform;
            if (rt == null) continue;

            if (_remapped.TryGetValue(rt, out Remap r))
            {
                Vector2 min = rt.anchorMin, max = rt.anchorMax;
                bool scriptChanged = !Mathf.Approximately(min.y, r.appliedMin) || !Mathf.Approximately(max.y, r.appliedMax);
                if (scriptChanged) { r.origMin = min.y; r.origMax = max.y; } // 他のスクリプトがアンカーを変えた時は、その値を元の値とする
                if (scriptChanged || bandChanged) Apply(rt, ref r);
                _remapped[rt] = r;
                continue;
            }

            float minY = rt.anchorMin.y, maxY = rt.anchorMax.y;
            if (minY <= 0.0001f && maxY >= 0.9999f) { RemapChildren(rt, bandChanged); continue; }
            if (Mathf.Approximately(minY, 0.5f) && Mathf.Approximately(maxY, 0.5f)) continue; // 中央基準は帯でも同じ位置

            var nr = new Remap { origMin = minY, origMax = maxY };
            Apply(rt, ref nr);
            _remapped[rt] = nr;
        }
    }

    private void Apply(RectTransform rt, ref Remap r)
    {
        r.appliedMin = 0.5f + (r.origMin - 0.5f) * _bandHeight;
        r.appliedMax = 0.5f + (r.origMax - 0.5f) * _bandHeight;
        rt.anchorMin = new Vector2(rt.anchorMin.x, r.appliedMin);
        rt.anchorMax = new Vector2(rt.anchorMax.x, r.appliedMax);
    }

    private void RestoreAnchors()
    {
        foreach (var kv in _remapped)
        {
            var rt = kv.Key;
            if (rt == null) continue;
            var r = kv.Value;
            // 移した後に他のスクリプトが変えていなければ元に戻す
            if (Mathf.Approximately(rt.anchorMin.y, r.appliedMin)) rt.anchorMin = new Vector2(rt.anchorMin.x, r.origMin);
            if (Mathf.Approximately(rt.anchorMax.y, r.appliedMax)) rt.anchorMax = new Vector2(rt.anchorMax.x, r.origMax);
        }
        _remapped.Clear();
        _bandHeight = -1f;
    }

    /// <summary>CanvasScaler（Scale With Screen Size）が、指定の大きさの画面で使う倍率（Unityの計算と同じ）</summary>
    private float ScaleFor(float screenW, float screenH)
    {
        Vector2 refRes = _scaler.referenceResolution;
        switch (_scaler.screenMatchMode)
        {
            case CanvasScaler.ScreenMatchMode.Expand:
                return Mathf.Min(screenW / refRes.x, screenH / refRes.y);
            case CanvasScaler.ScreenMatchMode.Shrink:
                return Mathf.Max(screenW / refRes.x, screenH / refRes.y);
            default:
                float logW = Mathf.Log(screenW / refRes.x, 2f);
                float logH = Mathf.Log(screenH / refRes.y, 2f);
                return Mathf.Pow(2f, Mathf.Lerp(logW, logH, _scaler.matchWidthOrHeight));
        }
    }
}
