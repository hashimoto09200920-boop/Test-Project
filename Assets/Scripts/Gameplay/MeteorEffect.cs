using UnityEngine;
using System.Collections;

/// <summary>
/// Area09(Cosmos)背景用の流れ星演出。AreaSelect画面の流れ星(AreaConstellationFX.cs)と
/// 同じ見た目（発生地点フラッシュ→発光する頭→先細りする彗星の尾→通過後に残る光の粒子、
/// 二次ベジェ曲線の軌道、イーズアウト、序盤終盤のフェードイン/アウト）を
/// ワールド空間(SpriteRenderer)向けに移植したもの。
///
/// ★AreaSelect版との違い：
/// - UI(Image/RectTransform)ではなくワールド空間(SpriteRenderer)を使う
/// - 開始・終了位置は画面中心基準の「円」ではなく、カメラに映る四角い範囲を基準にする
/// - 「手前から奥へ（近くに大きく出現し、遠ざかりながら小さくなって消える）」という
///   要望を反映するため、飛行中に頭・尾・粒子のサイズが縮小していく演出を追加した
///   （元のAreaSelect版にはない、Area09専用の要素）
/// </summary>
public class MeteorEffect : MonoBehaviour
{
    [Header("発生間隔")]
    [SerializeField] private float meteorIntervalMin = 3f;
    [SerializeField] private float meteorIntervalMax = 8f;

    [Header("飛行")]
    [Tooltip("飛行時間を軌道の距離から自動計算するための基準速度（ワールド単位/秒）。" +
             "飛行時間＝距離÷この値。距離が短すぎて一瞬で消えることがないよう、" +
             "距離自体をMin Travel Distanceで保証しているため、この値を変えれば" +
             "常に速度に反映される")]
    [SerializeField] private float meteorSpeed = 4f;
    [Tooltip("始点から終点までの飛行距離（ワールド単位）の下限。実際の距離がこれより" +
             "短くなりそうな場合、画面内に収まる範囲でこの距離に近づくよう終点を調整する。" +
             "★以前は「飛行時間」自体に下限を設けていたため、Speedを上げても" +
             "下限に張り付いて変化しない不具合があった。距離側で保証する方式に変更した")]
    [SerializeField] private float minTravelDistance = 3f;
    [Tooltip("飛行時間の安全下限（秒）。極端な設定(速度がほぼ0など)による" +
             "異常値を防ぐためだけの保険であり、通常の速度調整には影響しない")]
    [SerializeField] private float meteorDurationSafetyMin = 0.15f;
    [Tooltip("飛行時間の安全上限（秒）。極端な設定による異常値を防ぐためだけの保険")]
    [SerializeField] private float meteorDurationSafetyMax = 8f;
    [Tooltip("デバッグ用：流星を発生させるたびに、選ばれた色・距離・飛行時間をConsoleに出力する")]
    [SerializeField] private bool debugLogMeteorInfo = false;
    [Tooltip("頭（発光点）のサイズ（ワールド単位、飛行開始時点）")]
    [SerializeField] private float meteorHeadSize = 0.3f;
    [Tooltip("軌道の弧の強さ（0=直線、大きいほど大きく弧を描く。軌道全長に対する比率）")]
    [Range(0f, 0.5f)]
    [SerializeField] private float meteorBowRatio = 0.12f;
    [Tooltip("基本の色（Area Colorsが空、または使わない場合のフォールバック）")]
    [SerializeField] private Color meteorColor = new Color(0.85f, 0.92f, 1f, 1f);
    [Tooltip("2枚以上設定すると、この中からランダムに1色選んで流れ星の色にする" +
             "（AreaSelectの10エリアカラーを想定）。RGBのみ使用し、透明度はMeteor Colorの値を引き継ぐ")]
    [SerializeField] private Color[] areaColors;
    [Tooltip("移動時間全体に対する、フェードイン／フェードアウトそれぞれの割合")]
    [Range(0.05f, 0.5f)]
    [SerializeField] private float meteorFadeRatio = 0.3f;

    [Header("手前から奥へ（縮小）")]
    [Tooltip("出現時（手前）のサイズ倍率")]
    [SerializeField] private float startScaleMultiplier = 1.4f;
    [Tooltip("消滅時（奥）のサイズ倍率。0に近いほど「画面内で点になって消える」見え方になる。" +
             "★画面の外へ飛び出して見えなくなるのではなく、画面内で縮んで消えることを狙っているため、" +
             "ほぼ0に近い値にする")]
    [SerializeField] private float endScaleMultiplier = 0.03f;

    [Header("尾")]
    [SerializeField] private float trailSpanMin = 0.15f;
    [SerializeField] private float trailSpanMax = 0.55f;
    [Tooltip("尾の太さ（ワールド単位、飛行開始時点）")]
    [SerializeField] private float trailWidth = 0.08f;
    [Range(2, 12)]
    [SerializeField] private int trailSegments = 6;

    [Header("発生地点フラッシュ")]
    [SerializeField] private bool spawnFlashEnabled = true;
    [SerializeField] private float spawnFlashDuration = 0.18f;
    [SerializeField] private float spawnFlashSize = 0.5f;

    [Header("通過後に残る粒子")]
    [SerializeField] private bool trailParticlesEnabled = true;
    [Range(0, 40)]
    [SerializeField] private int trailParticleCount = 14;
    [SerializeField] private float trailParticleLifetimeMin = 0.6f;
    [SerializeField] private float trailParticleLifetimeMax = 1.2f;
    [SerializeField] private float trailParticleSize = 0.18f;
    [SerializeField] private float trailParticleScatter = 0.1f;

    [Header("軌道範囲")]
    [Tooltip("カメラの可視範囲に対して、開始点をこの割合だけ内側に収める（0.8なら可視範囲の80%以内）")]
    [Range(0.3f, 1f)]
    [SerializeField] private float startAreaInset = 0.8f;
    [Tooltip("画面左のHUD(SkillHUD)が隠している幅を、軌道の計算対象から除外する割合" +
             "（カメラ横幅に対する比率）。実測: SkillHUD幅280(基準解像度1080基準)を" +
             "実際のカメラ横幅に対する比率に換算した値がデフォルト")]
    [Range(0f, 0.3f)]
    [SerializeField] private float leftHudExclusionRatio = 0.123f;
    [Tooltip("終了点も開始点と同じくカメラ可視範囲内のランダムな点にする。★以前は開始点から" +
             "一定方向・距離で計算していたため画面外に出てしまうことがあったが、終点も可視範囲内の" +
             "点として選ぶことで必ず画面内で消えるようにした")]
    [Range(0.3f, 1f)]
    [SerializeField] private float endAreaInset = 0.8f;

    [Header("描画設定")]
    [SerializeField] private string sortingLayerName = "Default";
    [SerializeField] private int sortingOrder = -5;
    [Tooltip("発光表現用の加算合成マテリアル（既存のM_OrbGlow_Additiveを想定）")]
    [SerializeField] private Material additiveMaterial;
    [Tooltip("頭・粒子・フラッシュ用のソフトな円形グラデーション画像")]
    [SerializeField] private Sprite glowSprite;

    private Sprite whiteSprite;

    private void Start()
    {
        // 尾は薄い帯状の四角なので、単純な白1x1スプライトで十分（ランタイム生成、アセット不要）
        whiteSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);

        StartCoroutine(MeteorLoop());
    }

    private IEnumerator MeteorLoop()
    {
        while (true)
        {
            float wait = Random.Range(meteorIntervalMin, meteorIntervalMax);
            yield return new WaitForSeconds(wait);
            StartCoroutine(SpawnOneMeteor());
        }
    }

    private Bounds GetCameraWorldRect()
    {
        Camera cam = Camera.main;
        if (cam == null) return new Bounds(Vector3.zero, Vector3.one * 10f);
        float h = cam.orthographicSize * 2f;
        float w = h * cam.aspect;
        return new Bounds(new Vector3(cam.transform.position.x, cam.transform.position.y, 0f), new Vector3(w, h, 0f));
    }

    // ★画面左のHUD(SkillHUD)が隠している帯を除いた、実際に流れ星が見える範囲を返す
    private Bounds GetUsableWorldRect()
    {
        Bounds full = GetCameraWorldRect();
        float excludeWidth = full.size.x * leftHudExclusionRatio;
        return new Bounds(
            new Vector3(full.center.x + excludeWidth * 0.5f, full.center.y, 0f),
            new Vector3(Mathf.Max(0.01f, full.size.x - excludeWidth), full.size.y, 0f));
    }

    private IEnumerator SpawnOneMeteor()
    {
        Bounds rect = GetUsableWorldRect();

        // ★開始点：カメラ可視範囲のstartAreaInset倍の範囲内からランダムに選ぶ（＝手前、画面内に現れる）
        Vector2 startPos = new Vector2(
            rect.center.x + Random.Range(-0.5f, 0.5f) * rect.size.x * startAreaInset,
            rect.center.y + Random.Range(-0.5f, 0.5f) * rect.size.y * startAreaInset);

        // ★終了点：カメラ可視範囲(endAreaInset)の外に絶対に出ない前提の上で、
        //   その範囲内で「始点からできるだけ離れた上向きの点」を選ぶ。
        //   数学角度: 0°=右, 90°=真上, 180°=左, 270°=真下。60〜120°は真上を中心とした上向きの範囲。
        //   ★以前は距離を「画面横幅」基準の乱数で決めてから範囲内にクランプしていたが、
        //     角度が縦方向中心なのに横幅基準で距離を決めていたため、縦方向の可視範囲（横幅より
        //     ずっと狭い）にすぐ到達してクランプされ、始点と終点が非常に近くなる不具合があった。
        //     角度候補を複数試し「範囲内に収まる最大距離」が一番大きい角度を採用することで、
        //     この不具合を解消し、Min Travel Distanceによる距離保証も機能するようにした。
        const int angleSampleCount = 8;
        float bestMaxTravel = -1f;
        float bestAngleRad = 90f * Mathf.Deg2Rad;
        for (int i = 0; i < angleSampleCount; i++)
        {
            float testAngleRad = Random.Range(60f, 120f) * Mathf.Deg2Rad;
            Vector2 testDir = new Vector2(Mathf.Cos(testAngleRad), Mathf.Sin(testAngleRad));
            float maxTravel = MaxTravelWithinBounds(startPos, testDir, rect, endAreaInset);
            if (maxTravel > bestMaxTravel)
            {
                bestMaxTravel = maxTravel;
                bestAngleRad = testAngleRad;
            }
        }

        float desiredTravel = Random.Range(minTravelDistance, Mathf.Max(minTravelDistance, rect.size.y * 1.2f));
        float travelDist = Mathf.Min(desiredTravel, Mathf.Max(0f, bestMaxTravel));
        Vector2 bestDir = new Vector2(Mathf.Cos(bestAngleRad), Mathf.Sin(bestAngleRad));
        Vector2 endPos = startPos + bestDir * travelDist;

        Vector2 diff = endPos - startPos;
        float pathLen = diff.magnitude;
        Vector2 normal = pathLen > 0.001f ? new Vector2(-diff.y, diff.x) / pathLen : Vector2.zero;
        float bow = Mathf.Min(pathLen * meteorBowRatio, pathLen * 0.4f);
        if (Random.value < 0.5f) bow = -bow;
        Vector2 control = (startPos + endPos) * 0.5f + normal * bow;

        // ★飛行時間＝距離÷速度。距離自体をminTravelDistanceで保証しているため、
        //   ここでのMin/Maxはあくまで異常値防止の安全装置（通常は発動しない）
        float duration = Mathf.Clamp(pathLen / Mathf.Max(0.01f, meteorSpeed), meteorDurationSafetyMin, meteorDurationSafetyMax);

        // ★色：areaColorsが設定されていればその中からランダムに1色選ぶ（AreaSelectの10色を想定）。
        //   RGBのみ使用し、透明度はmeteorColorの値を引き継ぐ（元のAreaConstellationFX版と同じ考え方）
        Color pickedColor = meteorColor;
        int pickedIndex = -1;
        if (areaColors != null && areaColors.Length > 0)
        {
            pickedIndex = Random.Range(0, areaColors.Length);
            Color picked = areaColors[pickedIndex];
            picked.a = meteorColor.a;
            pickedColor = picked;
        }

        if (debugLogMeteorInfo)
        {
            Debug.Log($"[MeteorEffect] index={pickedIndex} color=({pickedColor.r:F2},{pickedColor.g:F2},{pickedColor.b:F2}) " +
                      $"pathLen={pathLen:F2} bestMaxTravel={bestMaxTravel:F2} speed={meteorSpeed:F2} duration={duration:F2}");
        }

        if (spawnFlashEnabled)
            yield return StartCoroutine(PlaySpawnFlash(startPos, pickedColor));

        // 頭（発光点）
        var headGo = new GameObject("MeteorHead");
        headGo.transform.SetParent(transform, true);
        var headSR = headGo.AddComponent<SpriteRenderer>();
        headSR.sprite = glowSprite;
        if (additiveMaterial != null) headSR.material = additiveMaterial;
        headSR.sortingLayerName = sortingLayerName;
        headSR.sortingOrder = sortingOrder;

        int segCount = Mathf.Max(1, trailSegments);
        var trailGos = new GameObject[segCount];
        var trailSRs = new SpriteRenderer[segCount];
        for (int i = 0; i < segCount; i++)
        {
            var segGo = new GameObject($"MeteorTrail_{i}");
            segGo.transform.SetParent(transform, true);
            var segSR = segGo.AddComponent<SpriteRenderer>();
            segSR.sprite = whiteSprite;
            if (additiveMaterial != null) segSR.material = additiveMaterial;
            segSR.sortingLayerName = sortingLayerName;
            segSR.sortingOrder = sortingOrder;
            trailGos[i] = segGo;
            trailSRs[i] = segSR;
        }

        float trailSpan = Random.Range(trailSpanMin, trailSpanMax);

        float particleInterval = trailParticleCount > 0 ? duration / trailParticleCount : float.MaxValue;
        float elapsed = 0f;
        float particleTimer = 0f;
        bool pixelSampled = false;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float rawT = Mathf.Clamp01(elapsed / duration);
            float t = 1f - (1f - rawT) * (1f - rawT); // ease-out

            Vector2 pos = QuadraticBezier(startPos, control, endPos, t);

            // ★手前から奥へ：時間経過でサイズを縮小する
            // ★位置移動と同じイーズアウト(t)で縮小させる。rawT(線形)のままだと位置と
            //   縮小の速さが噛み合わず、動きが不自然になる不具合があったため統一した
            float scale = Mathf.Lerp(startScaleMultiplier, endScaleMultiplier, t);

            float fadeRatio = Mathf.Max(0.01f, meteorFadeRatio);
            float fade = Mathf.Clamp01(Mathf.Min(1f, rawT / fadeRatio, (1f - rawT) / fadeRatio));

            headGo.transform.position = new Vector3(pos.x, pos.y, 0f);
            headGo.transform.localScale = Vector3.one * (meteorHeadSize * scale);
            Color hc = pickedColor; hc.a = pickedColor.a * fade;
            headSR.color = hc;

            for (int i = 0; i < segCount; i++)
            {
                float segT = Mathf.Clamp01(t - trailSpan * (i + 1) / segCount);
                Vector2 segPosBack = QuadraticBezier(startPos, control, endPos, segT);
                float segT2 = Mathf.Clamp01(t - trailSpan * i / segCount);
                Vector2 segPosFront = QuadraticBezier(startPos, control, endPos, segT2);

                Vector2 segCenter = (segPosBack + segPosFront) * 0.5f;
                Vector2 segDiff = segPosFront - segPosBack;
                float segLen = Mathf.Max(0.02f, segDiff.magnitude);
                float segAngle = Mathf.Atan2(segDiff.y, segDiff.x) * Mathf.Rad2Deg;

                float taper = 1f - (float)i / segCount;
                float width = Mathf.Max(0.01f, trailWidth * scale * taper);

                trailGos[i].transform.position = new Vector3(segCenter.x, segCenter.y, 0f);
                trailGos[i].transform.localScale = new Vector3(segLen * 1.4f, width, 1f);
                trailGos[i].transform.rotation = Quaternion.Euler(0f, 0f, segAngle);

                float segAlpha = pickedColor.a * fade * 0.6f * taper;
                Color segColor = pickedColor;
                segColor.a = segAlpha;
                trailSRs[i].color = segColor;
            }

            if (debugLogMeteorInfo && !pixelSampled && rawT > 0.4f)
            {
                pixelSampled = true;
                StartCoroutine(SampleActualPixel(pos, pickedColor, pickedIndex));
            }

            if (trailParticlesEnabled && particleInterval < float.MaxValue)
            {
                particleTimer += Time.deltaTime;
                if (particleTimer >= particleInterval)
                {
                    particleTimer = 0f;
                    Vector2 scatter = Random.insideUnitCircle * trailParticleScatter;
                    StartCoroutine(SpawnTrailParticle(pos + scatter, fade, scale, pickedColor));
                }
            }

            yield return null;
        }

        Destroy(headGo);
        for (int i = 0; i < segCount; i++)
            Destroy(trailGos[i]);
    }

    // ★デバッグ用：意図したtint(intendedColor)と、実際に画面に描画されたピクセルの色を
    //   直接読み取って比較する。「色は正しく選ばれているのに白く見える」という報告が
    //   加算合成の重なりによる飽和なのか、それともシェーダー等でtint自体が反映されていないのか
    //   （＝どちらも同じ見た目=白になりうる）を、憶測ではなく実際のフレームバッファで切り分けるため
    private IEnumerator SampleActualPixel(Vector2 worldPos, Color intendedColor, int index)
    {
        yield return new WaitForEndOfFrame();
        Camera cam = Camera.main;
        if (cam == null) yield break;
        Vector3 screenPos = cam.WorldToScreenPoint(new Vector3(worldPos.x, worldPos.y, 0f));
        int x = Mathf.Clamp(Mathf.RoundToInt(screenPos.x), 0, Screen.width - 1);
        int y = Mathf.Clamp(Mathf.RoundToInt(screenPos.y), 0, Screen.height - 1);
        var tex = new Texture2D(1, 1, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(x, y, 1, 1), 0, 0);
        tex.Apply();
        Color actual = tex.GetPixel(0, 0);
        Destroy(tex);
        Debug.Log($"[MeteorEffect][PixelSample] index={index} intended=({intendedColor.r:F2},{intendedColor.g:F2},{intendedColor.b:F2}) " +
                  $"actualScreenPixel=({actual.r:F2},{actual.g:F2},{actual.b:F2}) screenPos=({x},{y})");
    }

    private IEnumerator SpawnTrailParticle(Vector2 pos, float spawnFade, float scaleAtSpawn, Color color)
    {
        var go = new GameObject("MeteorParticle");
        go.transform.SetParent(transform, true);
        go.transform.position = new Vector3(pos.x, pos.y, 0f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = glowSprite;
        if (additiveMaterial != null) sr.material = additiveMaterial;
        sr.sortingLayerName = sortingLayerName;
        sr.sortingOrder = sortingOrder;

        float startSize = trailParticleSize * scaleAtSpawn;
        float lifetime = Random.Range(trailParticleLifetimeMin, trailParticleLifetimeMax);
        float baseAlpha = color.a * spawnFade;
        float elapsed = 0f;
        while (elapsed < lifetime)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / lifetime);

            float fadeIn = Mathf.Clamp01(t / 0.04f);
            float fadeOut = t < 0.5f ? 1f : Mathf.Clamp01((1f - t) / 0.5f);
            Color c = color;
            c.a = baseAlpha * Mathf.Min(fadeIn, fadeOut);
            sr.color = c;
            go.transform.localScale = Vector3.one * (startSize * Mathf.Lerp(1f, 0.5f, t));

            yield return null;
        }
        Destroy(go);
    }

    private IEnumerator PlaySpawnFlash(Vector2 pos, Color color)
    {
        var go = new GameObject("MeteorSpawnFlash");
        go.transform.SetParent(transform, true);
        go.transform.position = new Vector3(pos.x, pos.y, 0f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = glowSprite;
        if (additiveMaterial != null) sr.material = additiveMaterial;
        sr.sortingLayerName = sortingLayerName;
        sr.sortingOrder = sortingOrder;

        float elapsed = 0f;
        while (elapsed < spawnFlashDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / spawnFlashDuration);
            float alpha = 1f - t;
            Color c = color; c.a = alpha;
            sr.color = c;
            go.transform.localScale = Vector3.one * (spawnFlashSize * (0.5f + t));
            yield return null;
        }
        Destroy(go);
    }

    // ★originから方向dirへ進んだとき、rect内(areaInsetで絞った矩形)の境界に
    //   到達するまでの距離を返す（originは矩形内にある前提）。
    //   これにより「範囲内で進める最大距離」を角度ごとに正確に把握できる。
    private static float MaxTravelWithinBounds(Vector2 origin, Vector2 dir, Bounds rect, float areaInset)
    {
        float halfW = rect.size.x * areaInset * 0.5f;
        float halfH = rect.size.y * areaInset * 0.5f;
        float minX = rect.center.x - halfW, maxX = rect.center.x + halfW;
        float minY = rect.center.y - halfH, maxY = rect.center.y + halfH;

        float maxT = float.MaxValue;
        if (Mathf.Abs(dir.x) > 0.0001f)
        {
            float tExitX = Mathf.Max((minX - origin.x) / dir.x, (maxX - origin.x) / dir.x);
            if (tExitX >= 0f) maxT = Mathf.Min(maxT, tExitX);
        }
        if (Mathf.Abs(dir.y) > 0.0001f)
        {
            float tExitY = Mathf.Max((minY - origin.y) / dir.y, (maxY - origin.y) / dir.y);
            if (tExitY >= 0f) maxT = Mathf.Min(maxT, tExitY);
        }
        return maxT == float.MaxValue ? 0f : Mathf.Max(0f, maxT);
    }

    private static Vector2 QuadraticBezier(Vector2 a, Vector2 b, Vector2 c, float t)
    {
        float u = 1f - t;
        return u * u * a + 2f * u * t * b + t * t * c;
    }
}
