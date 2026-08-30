using UnityEngine;

/// <summary>
/// Area09(Cosmos)のEarth関連レイヤー（宇宙背景マスク・地表・輪郭グロー・オーロラ）を、
/// 実行時のカメラの実際のアスペクト比に合わせて配置・スケールし直す。
/// ★Editor時に固定のスケール・位置を焼き込む方式は、端末ごとに画面アスペクト比が
/// 異なる（Camera.aspectが変わる）と再現できずズレる。必ず起動時にその場で計算する。
///
/// ★⑤マスク画像は③輪郭グロー画像のアルファ形状から機械的に生成したものに置き換え済み
/// （同じ1672x941の画素グリッド上の曲線）。そのためマスクと輪郭グローは
/// 「同じスケール・同じ位置」で重ねるだけで曲線が必ず一致する。
/// 以前あった「実測した曲線位置の割合」を基準に個別オフセットを逆算する方式は、
/// ③と⑤が別々にAI生成された曲線同士だったために原理的にズレが解消できなかったため廃止。
/// </summary>
public class EarthLayerFitter : MonoBehaviour
{
    [SerializeField] private SpriteMask earthMask;
    [SerializeField] private SpriteRenderer earthSurface;
    [SerializeField] private SpriteRenderer earthRimGlow;
    [SerializeField] private SpriteRenderer aurora;

    [Header("安全マージン")]
    [Tooltip("マスクの端を輪郭グローの内側に確実に隠すための拡大率（マスクのみ）")]
    [SerializeField] private float maskSafetyMargin = 1.02f;
    [Header("オーロラ（輪郭グローの曲線から根元を生やす）")]
    [Tooltip("輪郭グロー(rimScale)を基準にしたオーロラの縮小率。1=輪郭グローと同じ大きさ、" +
             "0.5=半分の大きさ。画面を覆うためのcover方式ではなく、ここで直接サイズを指定する")]
    [SerializeField] private float auroraScaleFactor = 0.5f;
    [Tooltip("④画像内で、オーロラの根元(リボンの下端)が画像上端から何%の位置にあるか（実測値）")]
    [SerializeField] private float auroraRootFraction = 0.706f;
    [Tooltip("オーロラの根元を輪郭グローの曲線からさらに上下にずらす（ワールド単位）")]
    [SerializeField] private float auroraRootYOffset = 0f;
    [Tooltip("オーロラ全体を左右にずらす（ワールド単位）。左右非対称な絵柄で片側が余り片側が足りない場合の調整用")]
    [SerializeField] private float auroraXOffset = 0f;
    [Tooltip("オーロラ全体をZ軸回転させる（度）")]
    [SerializeField] private float auroraRotationZ = 0f;
    [Tooltip("③輪郭グロー画像内で、曲線（可視化された部分）が画像上端から何%の位置にあるか（実測値）")]
    [SerializeField] private float rimCurveFraction = 0.670f;
    [Tooltip("反転版オーロラ(AuroraWobbleのauroraPatterns[1]/子オブジェクトAuroraLayerB)だけを" +
             "さらに左右にずらす（ワールド単位、auroraXOffsetに加算）")]
    [SerializeField] private float auroraPatternBXOffset = 0f;
    [Tooltip("反転版オーロラだけをさらに上下にずらす（ワールド単位、auroraRootYOffsetの結果に加算）")]
    [SerializeField] private float auroraPatternBYOffset = 0f;

    [Header("個別位置調整")]
    [Tooltip("輪郭グロー(Earth_RimGlow)だけを独立して上下にずらす（ワールド単位）。" +
             "★マスク・地表とは同じ位置で重ねることで曲線を一致させているため、" +
             "この値を0以外にするとマスク・地表とのズレが生じる可能性がある")]
    [SerializeField] private float rimGlowYOffset = 0f;

    private void Start()
    {
        Fit();
    }

#if UNITY_EDITOR
    // ★Inspectorで値を変更した瞬間に自動でFit()を実行する。
    //   「値を変えたのにFit Nowを押し忘れて反映されない」を無くすため。
    private void OnValidate()
    {
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this != null) Fit();
        };
    }
#endif

    [ContextMenu("Fit Now")]
    public void Fit()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        float camWorldHeight = cam.orthographicSize * 2f;
        float camWorldWidth = camWorldHeight * cam.aspect;
        float camX = cam.transform.position.x;
        float camY = cam.transform.position.y;

        Sprite surfaceSprite = earthSurface != null ? earthSurface.sprite : null;
        Sprite rimSprite = earthRimGlow != null ? earthRimGlow.sprite : null;
        Sprite auroraSprite = aurora != null ? aurora.sprite : null;

        // ⑤マスクは③輪郭グローと同じ画素グリッドの曲線なので、同じFitScale・同じ位置で重ねるだけでよい
        float rimScale = FitScale(rimSprite, camWorldWidth, camWorldHeight);
        float maskScale = rimScale * maskSafetyMargin;
        float surfaceScale = FitScale(surfaceSprite, camWorldWidth, camWorldHeight) * maskSafetyMargin;

        // ★オーロラは画面を覆うcover方式にしない（大きくなりすぎるため）。
        //   輪郭グローのスケールを基準に直接サイズを指定し、根元（リボンの下端）を
        //   輪郭グローの曲線位置に合わせて生やす。
        float auroraScale = rimScale * auroraScaleFactor;

        if (earthMask != null)
        {
            var t = earthMask.transform;
            t.position = new Vector3(camX, camY, t.position.z);
            t.localScale = Vector3.one * maskScale;
        }
        if (earthSurface != null)
        {
            var t = earthSurface.transform;
            t.position = new Vector3(camX, camY, t.position.z);
            t.localScale = Vector3.one * surfaceScale;
        }
        if (earthRimGlow != null)
        {
            var t = earthRimGlow.transform;
            t.position = new Vector3(camX, camY + rimGlowYOffset, t.position.z);
            t.localScale = Vector3.one * rimScale;
        }
        if (aurora != null && rimSprite != null)
        {
            // 輪郭グローの曲線のワールドY座標（rimGlowYOffsetも考慮）
            float rimRenderedHeight = SpriteWorldHeight(rimSprite) * rimScale;
            float rimCurveY = (camY + rimGlowYOffset) + rimRenderedHeight * (0.5f - rimCurveFraction);
            float targetRootY = rimCurveY + auroraRootYOffset;

            // オーロラの中心Yを、根元がtargetRootYに来るよう逆算する
            float auroraRenderedHeight = SpriteWorldHeight(auroraSprite) * auroraScale;
            float auroraCenterY = targetRootY + auroraRenderedHeight * (auroraRootFraction - 0.5f);

            var t = aurora.transform;
            t.position = new Vector3(camX + auroraXOffset, auroraCenterY, t.position.z);
            t.localScale = Vector3.one * auroraScale;
            t.rotation = Quaternion.Euler(0f, 0f, auroraRotationZ);

            // ★反転版(AuroraLayerB)専用の位置調整。AuroraWobbleがクロスフェード用に
            //   子オブジェクトとして生成するため、存在すればここから直接ローカル位置を調整する
            //   （位置調整系の項目はすべてこのEarthLayerFitterに集約するため）
            Transform layerB = t.Find("AuroraLayerB");
            if (layerB != null)
                layerB.localPosition = new Vector3(auroraPatternBXOffset, auroraPatternBYOffset, 0f);

            Debug.Log($"[EarthLayerFitter] aurora: scale={auroraScale:F3} centerY={auroraCenterY:F3} rimCurveY={rimCurveY:F3}");
        }

        Debug.Log($"[EarthLayerFitter] camAspect={cam.aspect:F3} camWidth={camWorldWidth:F2} " +
                  $"rimScale={rimScale:F3} maskScale={maskScale:F3} surfaceScale={surfaceScale:F3}");
    }

    private static float SpriteWorldHeight(Sprite s) => s != null ? s.bounds.size.y : 0f;

    private static float FitScale(Sprite sprite, float camWidth, float camHeight)
    {
        if (sprite == null) return 1f;
        float w = sprite.bounds.size.x;
        float h = sprite.bounds.size.y;
        if (w <= 0f || h <= 0f) return 1f;
        // cover方式：縦横どちらも確実に覆う（はみ出しはOK、隙間はNG）
        return Mathf.Max(camHeight / h, camWidth / w);
    }
}
