using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Area09(Cosmos)のEarth/Auroraレイヤーをセットアップする。
/// ★重要：Background_Farの実際のScale(3.2)は「カメラをちょうど覆うサイズ」ではなく、
/// 意図的にズームされた特殊な値だったため、それを基準にするとEarthが大幅に肥大化する
/// バグが起きた（実測して判明）。正しくは、Main Cameraの実測値
/// （Orthographic Size=5, 位置(0,0,-10)）から「画像を実際に何ワールド単位で表示すべきか」
/// を直接計算する。
/// </summary>
public static class Area09EarthSetupMigrator
{
    private const string Dir = "Assets/Art/Background/";

    // Main Camera実測値（05_Game.unity）
    private const float CameraOrthoSize = 5f;
    private const float CameraWorldHeight = CameraOrthoSize * 2f; // 10

    // 各PNGのPixels Per Unit（全て100で統一）とネイティブピクセルサイズ
    private const float Ppu = 100f;
    private const float WidthPxSpace = 666f,  HeightPxSpace = 375f;    // ①⑤
    private const float WidthPxGlowAurora = 1672f, HeightPxGlowAurora = 941f; // ③④
    private const float WidthPxSurface = 1774f, HeightPxSurface = 887f; // ②

    // ★重要：ゲーム画面の実際のアスペクト比は16:9ではなく、Camera.aspect実測で
    //   約2.11:1（Hand5G 1520x720相当）だった。16:9前提で「高さだけ画面に合わせる」
    //   スケールを組んだ結果、幅が足りず右側で輪郭・マスクが途切れる不具合が起きた。
    //   高さ基準・幅基準の両方のスケールを計算し、大きい方（＝両方とも確実に覆う方）を採用する。
    private static float ScaleSpace, ScaleGlowAurora, ScaleSurface;

    private static void ComputeScales(float cameraAspect)
    {
        float cameraWorldWidth = CameraWorldHeight * cameraAspect;

        float FitScale(float widthPx, float heightPx)
        {
            float wUnits = widthPx / Ppu;
            float hUnits = heightPx / Ppu;
            float scaleForHeight = CameraWorldHeight / hUnits;
            float scaleForWidth = cameraWorldWidth / wUnits;
            return Mathf.Max(scaleForHeight, scaleForWidth); // cover（はみ出しOK・隙間NG）
        }

        ScaleSpace = FitScale(WidthPxSpace, HeightPxSpace);
        ScaleGlowAurora = FitScale(WidthPxGlowAurora, HeightPxGlowAurora);
        ScaleSurface = FitScale(WidthPxSurface, HeightPxSurface);
    }

    // ★⑤(マスク)と③(輪郭グロー)は同じ曲線を参照して作ったはずだが、実測すると
    //   画像内での曲線の位置（画像上端からの割合）が微妙に異なる（⑤=0.568, ③=0.670）。
    //   これはAI生成の誤差。★重要：この位置合わせは「画像の実際の高さ（＝スケール）」に
    //   依存するため、固定のワールド単位オフセットにしてはいけない
    //   （cover方式でスケールが変わるたびにズレ量も変わってしまい、実際に再発した）。
    //   必ずその場のスケールから毎回計算し直す。
    private const float MaskCurveFraction = 0.568f; // ⑤の曲線位置（上端から）
    private const float RimCurveFraction = 0.670f;  // ③の曲線位置（上端から）
    private const float MaskOverlapScaleFactor = 1.08f; // マスクを少し大きくして輪郭に重ねる
    private const float MaskOverlapMargin = 0.15f;      // 重なり量（ワールド単位）

    // ★オーロラはAuroraWobbleで左右に±3%ほど伸縮するため、ちょうど画面を覆うスケールのままだと
    //   縮んだ瞬間に右端が切れる。安全マージンを載せておく。
    private const float AuroraSafetyMargin = 1.10f;

    /// <summary>
    /// 画像上端からfraction位置にある曲線の、ワールド座標Yを返す。
    /// centerYは画像の中心Y、renderedHeightは実際にスケール適用後のワールド単位の高さ。
    /// </summary>
    private static float CurveWorldY(float centerY, float renderedHeight, float fraction)
        => centerY + renderedHeight * (0.5f - fraction);

    // ★[DidReloadScripts]による自動実行は削除済み。EarthLayerFitter導入後もこのRun()が
    //   スクリプトコンパイルのたびに勝手に再実行され、Earth_Mask等のtransformを
    //   このファイル内の古い(⑤が666x375だった頃の)固定計算式で上書き→自動保存してしまい、
    //   EarthLayerFitterのmaskSafetyMargin等でのInspector調整が消える不具合の原因になっていた。
    [MenuItem("Tools/Area09/Setup Earth Layers In Background")]
    private static void Run()
    {
        GameObject bgRoot = GameObject.Find("Background_Root");
        if (bgRoot == null)
        {
            Debug.LogWarning("[Area09EarthSetupMigrator] Background_Root が見つかりません（05_Game.unityを開いてください）。");
            return;
        }

        GameObject bgFar = GameObject.Find("Background_Far");
        SpriteRenderer farSR = bgFar != null ? bgFar.GetComponent<SpriteRenderer>() : null;
        int sortingLayerID = farSR != null ? farSR.sortingLayerID : 0;
        int baseOrder = farSR != null ? farSR.sortingOrder : -10;

        // ★カメラ中心(X,Y)に合わせる。Background_Farの位置・スケールは意図的にズームされた
        //   別基準の値なので参照しない（実測で判明した過去の誤り）。
        Camera cam = Object.FindFirstObjectByType<Camera>();
        float camX = cam != null ? cam.transform.position.x : 0f;
        float camY = cam != null ? cam.transform.position.y : 0f;
        float baseZ = bgFar != null ? bgFar.transform.position.z : 0f;
        float cameraAspect = cam != null ? cam.aspect : 16f / 9f;
        ComputeScales(cameraAspect);
        Debug.Log($"[Area09EarthSetupMigrator] Camera.aspect={cameraAspect:F3} (width={CameraWorldHeight * cameraAspect:F2})");

        Sprite maskSprite = AssetDatabase.LoadAssetAtPath<Sprite>(Dir + "⑤ 地球マスク用シルエット（remove.bg対応版）.png");
        Sprite surfaceSprite = AssetDatabase.LoadAssetAtPath<Sprite>(Dir + "② 地表テクスチャ（再生成版）.png");
        Sprite rimSprite = AssetDatabase.LoadAssetAtPath<Sprite>(Dir + "③ 地球の輪郭グロー（静止させる部分）.png");
        Sprite auroraSprite = AssetDatabase.LoadAssetAtPath<Sprite>(Dir + "④ オーロラのみ（透過・独立レイヤー）.png");

        bool ok = true;
        if (maskSprite == null) { Debug.LogError("[Area09EarthSetupMigrator] ⑤マスク画像が読み込めません。"); ok = false; }
        if (surfaceSprite == null) { Debug.LogError("[Area09EarthSetupMigrator] ②地表画像が読み込めません。"); ok = false; }
        if (rimSprite == null) { Debug.LogError("[Area09EarthSetupMigrator] ③輪郭グロー画像が読み込めません。"); ok = false; }
        if (auroraSprite == null) { Debug.LogError("[Area09EarthSetupMigrator] ④オーロラ画像が読み込めません。"); ok = false; }
        if (!ok) return;

        Transform parent = bgRoot.transform;

        // --- 曲線位置合わせを毎回その場のスケールから動的に計算する ---
        float rimRenderedHeight = (HeightPxGlowAurora / Ppu) * ScaleGlowAurora;
        float rimCurveY = CurveWorldY(camY, rimRenderedHeight, RimCurveFraction);
        float targetMaskCurveY = rimCurveY + MaskOverlapMargin; // 輪郭グローに少しめり込ませる

        float maskScale = ScaleSpace * MaskOverlapScaleFactor;
        float maskRenderedHeight = (HeightPxSpace / Ppu) * maskScale;
        float maskCenterY = targetMaskCurveY - maskRenderedHeight * (0.5f - MaskCurveFraction);

        Debug.Log($"[Area09EarthSetupMigrator] rimCurveY={rimCurveY:F3}, targetMaskCurveY={targetMaskCurveY:F3}, maskCenterY={maskCenterY:F3}");

        // --- Earth_Mask ---
        GameObject maskGo = FindOrCreate(parent, "Earth_Mask");
        SpriteMask mask = maskGo.GetComponent<SpriteMask>();
        if (mask == null) mask = maskGo.AddComponent<SpriteMask>();
        mask.sprite = maskSprite;
        // ★AddComponent直後はSorting Layer=Default(0)のままになっており、Earth_Surface等が
        //   使っているカスタムSorting Layer（Background_Farと同じ）と食い違っていた。
        //   これが2枚目のタイルコピーが正しくクリップされない不具合の原因だった。
        mask.sortingLayerID = sortingLayerID;
        mask.sortingOrder = baseOrder;
        SetTransform(maskGo.transform, camX, maskCenterY, baseZ, maskScale);

        // --- Earth_Surface（地表テクスチャ。マスクでくり抜かれるので厳密な位置合わせは不要） ---
        GameObject surfGo = FindOrCreate(parent, "Earth_Surface");
        SpriteRenderer surfSR = surfGo.GetComponent<SpriteRenderer>();
        if (surfSR == null) surfSR = surfGo.AddComponent<SpriteRenderer>();
        surfSR.sprite = surfaceSprite;
        surfSR.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
        surfSR.sortingLayerID = sortingLayerID;
        surfSR.sortingOrder = baseOrder;
        // ★マスクを広げた分、地表もマスク全域を覆えるよう同じ倍率で少し大きくしておく
        SetTransform(surfGo.transform, camX, maskCenterY, baseZ - 0.1f, ScaleSurface * MaskOverlapScaleFactor);
        if (surfGo.GetComponent<EarthRotationScroll>() == null) surfGo.AddComponent<EarthRotationScroll>();

        // --- Earth_RimGlow（画面を縦にちょうど埋めるスケール。カメラ中心に配置） ---
        GameObject rimGo = FindOrCreate(parent, "Earth_RimGlow");
        SpriteRenderer rimSR = rimGo.GetComponent<SpriteRenderer>();
        if (rimSR == null) rimSR = rimGo.AddComponent<SpriteRenderer>();
        rimSR.sprite = rimSprite;
        rimSR.maskInteraction = SpriteMaskInteraction.None;
        rimSR.sortingLayerID = sortingLayerID;
        rimSR.sortingOrder = baseOrder;
        SetTransform(rimGo.transform, camX, camY, baseZ - 0.2f, ScaleGlowAurora);

        // --- Aurora（同上。AuroraWobbleの伸縮で端が切れないよう安全マージンを載せる） ---
        GameObject auroraGo = FindOrCreate(parent, "Aurora");
        SpriteRenderer auroraSR = auroraGo.GetComponent<SpriteRenderer>();
        if (auroraSR == null) auroraSR = auroraGo.AddComponent<SpriteRenderer>();
        auroraSR.sprite = auroraSprite;
        auroraSR.maskInteraction = SpriteMaskInteraction.None;
        auroraSR.sortingLayerID = sortingLayerID;
        auroraSR.sortingOrder = baseOrder;
        SetTransform(auroraGo.transform, camX, camY, baseZ - 0.3f, ScaleGlowAurora * AuroraSafetyMargin);
        if (auroraGo.GetComponent<AuroraWobble>() == null) auroraGo.AddComponent<AuroraWobble>();

        // ★Editor時の位置・スケール計算はあくまで初期プレビュー用。実行時は必ず
        //   EarthLayerFitterがCamera.aspectの実測値から毎回計算し直して上書きする
        //   （端末ごとにアスペクト比が異なっても正しく表示されるようにするため）。
        EarthLayerFitter fitter = maskGo.GetComponent<EarthLayerFitter>();
        if (fitter == null) fitter = maskGo.AddComponent<EarthLayerFitter>();
        var fitterSo = new SerializedObject(fitter);
        fitterSo.FindProperty("earthMask").objectReferenceValue = mask;
        fitterSo.FindProperty("earthSurface").objectReferenceValue = surfSR;
        fitterSo.FindProperty("earthRimGlow").objectReferenceValue = rimSR;
        fitterSo.FindProperty("aurora").objectReferenceValue = auroraSR;
        fitterSo.ApplyModifiedProperties();
        EditorUtility.SetDirty(fitter);

        // ★直接Play等でGameSession.HasValidArea()がfalseの場合、BackgroundManager.Start()の
        //   表示切り替えが走らないため、既定値は非表示にしておく（Area09選択時にのみ表示させる）
        maskGo.SetActive(false);
        surfGo.SetActive(false);
        rimGo.SetActive(false);
        auroraGo.SetActive(false);

        BackgroundManager bgManager = Object.FindFirstObjectByType<BackgroundManager>(FindObjectsInactive.Include);
        if (bgManager != null)
        {
            var so = new SerializedObject(bgManager);
            so.FindProperty("earthMask").objectReferenceValue = maskGo;
            so.FindProperty("earthSurface").objectReferenceValue = surfGo;
            so.FindProperty("earthRimGlow").objectReferenceValue = rimGo;
            so.FindProperty("aurora").objectReferenceValue = auroraGo;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(bgManager);
            Debug.Log("[Area09EarthSetupMigrator] BackgroundManagerにEarthレイヤー参照を設定しました。");
        }
        else
        {
            Debug.LogWarning("[Area09EarthSetupMigrator] BackgroundManagerが見つからず、参照の自動設定をスキップしました。手動でアサインしてください。");
        }

        EditorUtility.SetDirty(bgRoot);
        var scene = bgRoot.scene;
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[Area09EarthSetupMigrator] Earth_Mask / Earth_Surface / Earth_RimGlow / Aurora を Background_Root 直下にセットアップし、保存しました。" +
                  $" (ScaleSpace={ScaleSpace:F3}, ScaleGlowAurora={ScaleGlowAurora:F3}, ScaleSurface={ScaleSurface:F3})");
    }

    private static GameObject FindOrCreate(Transform parent, string name)
    {
        Transform t = parent.Find(name);
        if (t != null) return t.gameObject;
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go;
    }

    private static void SetTransform(Transform t, float x, float y, float z, float scale)
    {
        t.position = new Vector3(x, y, z);
        t.localScale = new Vector3(scale, scale, scale);
    }
}
