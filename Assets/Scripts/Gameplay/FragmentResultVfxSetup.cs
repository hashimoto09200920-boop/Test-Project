using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

/// <summary>
/// HalloweenBossController が持つ6体のFragmentに Match/Mismatch VFX をセットアップするエディタユーティリティ。
/// Balloon.prefab に一時アタッチ → ContextMenu実行 → Remove Component。
/// </summary>
[DisallowMultipleComponent]
public class FragmentResultVfxSetup : MonoBehaviour
{
    [ContextMenu("Setup Fragment VFX (全Fragmentに適用・初回のみ)")]
    private void SetupAll()
    {
#if UNITY_EDITOR
        var controller = GetComponent<HalloweenBossController>();
        if (controller == null)
        {
            Debug.LogError("[FragmentResultVfxSetup] HalloweenBossControllerが同じGameObjectに見つかりません");
            return;
        }

        Material mat = FindGlowMaterial();

        var controllerSO = new SerializedObject(controller);
        var fragProp = controllerSO.FindProperty("fragments");
        if (fragProp == null)
        {
            Debug.LogError("[FragmentResultVfxSetup] HalloweenBossController.fragments フィールドが見つかりません");
            return;
        }

        int count = 0;
        for (int i = 0; i < fragProp.arraySize; i++)
        {
            var frag = fragProp.GetArrayElementAtIndex(i).objectReferenceValue as HalloweenFragment;
            if (frag == null) continue;
            SetupFragmentVfx(frag, mat);
            count++;
        }

        // PrefabステージのシーンをdirtyにしてCtrl+S保存を促す
        var stage = PrefabStageUtility.GetCurrentPrefabStage();
        if (stage != null)
            EditorSceneManager.MarkSceneDirty(stage.scene);

        AssetDatabase.SaveAssets();
        Debug.Log($"[FragmentResultVfxSetup] {count}体のFragmentにVFXをセットアップしました。Ctrl+Sで保存 → Remove Componentで削除してください。");
#endif
    }

#if UNITY_EDITOR
    private void SetupFragmentVfx(HalloweenFragment frag, Material mat)
    {
        RemoveChild(frag.transform, "VFX_Match");
        RemoveChild(frag.transform, "VFX_Mismatch");

        // Match VFX: オレンジゴールドのコアオーラ + 放射スパーク
        var matchRoot = CreateChild(frag.transform, "VFX_Match");
        var matchAuraPS = SetupMatchAura(matchRoot, mat);
        SetupMatchSparks(CreateChild(matchRoot.transform, "PS_MatchSparks"), mat);

        // Mismatch VFX: 暗い紫のコアオーラ + 上昇するウィスプ
        var mismatchRoot = CreateChild(frag.transform, "VFX_Mismatch");
        var mismatchAuraPS = SetupMismatchAura(mismatchRoot, mat);
        SetupMismatchWisps(CreateChild(mismatchRoot.transform, "PS_MismatchWisps"), mat);

        // HalloweenFragment の matchVfx / mismatchVfx フィールドに直接アサイン
        frag.AssignVfxForSetup(matchAuraPS, mismatchAuraPS);
        EditorUtility.SetDirty(frag.gameObject);
    }

    // ─── Match Aura（オレンジゴールドのソフトグロー / Local空間でフラグメントに追従）───
    private ParticleSystem SetupMatchAura(GameObject go, Material mat)
    {
        var ps = go.AddComponent<ParticleSystem>();

        var main = ps.main;
        main.playOnAwake     = false;
        main.loop            = true;
        main.prewarm         = false;
        main.duration        = 2f;
        main.startLifetime   = new ParticleSystem.MinMaxCurve(0.8f, 1.4f);
        main.startSpeed      = new ParticleSystem.MinMaxCurve(0f);
        main.startSize       = new ParticleSystem.MinMaxCurve(0.6f, 1.2f);
        main.startColor      = new ParticleSystem.MinMaxGradient(new Color(1f, 0.6f, 0.1f, 0.6f));
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.maxParticles    = 30;

        var em = ps.emission;
        em.rateOverTime = 4f;

        var sh = ps.shape;
        sh.shapeType = ParticleSystemShapeType.Circle;
        sh.radius    = 0.01f;

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0f, 0f, 3f), new Keyframe(0.4f, 1f, 0f, 0f), new Keyframe(1f, 0f, -3f, 0f)));

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.8f, 0.2f), new GradientAlphaKey(0.8f, 0.8f), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(g);

        var ren = go.GetComponent<ParticleSystemRenderer>();
        ren.renderMode     = ParticleSystemRenderMode.Billboard;
        ren.sortingLayerID = 0;
        ren.sortingOrder   = 5;
        if (mat != null) ren.sharedMaterial = mat;

        return ps;
    }

    // ─── Match Sparks（ゴールドの放射スパーク / World空間で飛び散る）───
    private void SetupMatchSparks(GameObject go, Material mat)
    {
        var ps = go.AddComponent<ParticleSystem>();

        var main = ps.main;
        main.playOnAwake     = false;
        main.loop            = true;
        main.prewarm         = false;
        main.duration        = 1f;
        main.startLifetime   = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
        main.startSpeed      = new ParticleSystem.MinMaxCurve(0.8f, 2.0f);
        main.startSize       = new ParticleSystem.MinMaxCurve(0.05f, 0.12f);
        main.startColor      = new ParticleSystem.MinMaxGradient(new Color(1f, 0.9f, 0.4f, 1f));
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles    = 50;

        var em = ps.emission;
        em.rateOverTime = 10f;

        var sh = ps.shape;
        sh.shapeType = ParticleSystemShapeType.Circle;
        sh.radius    = 0.02f;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(new Color(1f, 0.97f, 0.7f), 0f), new GradientColorKey(new Color(1f, 0.85f, 0.1f), 0.5f), new GradientColorKey(new Color(1f, 0.4f, 0f), 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(g);

        var ren = go.GetComponent<ParticleSystemRenderer>();
        ren.renderMode     = ParticleSystemRenderMode.Stretch;
        ren.velocityScale  = 0.4f;
        ren.lengthScale    = 0.3f;
        ren.sortingLayerID = 0;
        ren.sortingOrder   = 6;
        if (mat != null) ren.sharedMaterial = mat;
    }

    // ─── Mismatch Aura（暗い紫のコアオーラ / Local空間でフラグメントに追従）───
    private ParticleSystem SetupMismatchAura(GameObject go, Material mat)
    {
        var ps = go.AddComponent<ParticleSystem>();

        var main = ps.main;
        main.playOnAwake     = false;
        main.loop            = true;
        main.prewarm         = false;
        main.duration        = 2f;
        main.startLifetime   = new ParticleSystem.MinMaxCurve(1.0f, 1.8f);
        main.startSpeed      = new ParticleSystem.MinMaxCurve(0f);
        main.startSize       = new ParticleSystem.MinMaxCurve(0.7f, 1.3f);
        main.startColor      = new ParticleSystem.MinMaxGradient(new Color(0.55f, 0f, 0.7f, 0.65f));
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.maxParticles    = 25;

        var em = ps.emission;
        em.rateOverTime = 3f;

        var sh = ps.shape;
        sh.shapeType = ParticleSystemShapeType.Circle;
        sh.radius    = 0.01f;

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0f, 0f, 3f), new Keyframe(0.4f, 1f, 0f, 0f), new Keyframe(1f, 0f, -3f, 0f)));

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(new Color(0.55f, 0f, 0.7f), 0f), new GradientColorKey(new Color(0.8f, 0.05f, 0.15f), 0.6f), new GradientColorKey(new Color(0.3f, 0f, 0.4f), 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.8f, 0.2f), new GradientAlphaKey(0.7f, 0.7f), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(g);

        var ren = go.GetComponent<ParticleSystemRenderer>();
        ren.renderMode     = ParticleSystemRenderMode.Billboard;
        ren.sortingLayerID = 0;
        ren.sortingOrder   = 5;
        if (mat != null) ren.sharedMaterial = mat;

        return ps;
    }

    // ─── Mismatch Wisps（紫の上昇するウィスプ / World空間で漂う）───
    private void SetupMismatchWisps(GameObject go, Material mat)
    {
        var ps = go.AddComponent<ParticleSystem>();

        var main = ps.main;
        main.playOnAwake     = false;
        main.loop            = true;
        main.prewarm         = false;
        main.duration        = 2f;
        main.startLifetime   = new ParticleSystem.MinMaxCurve(0.8f, 1.5f);
        main.startSpeed      = new ParticleSystem.MinMaxCurve(0.1f, 0.4f);
        main.startSize       = new ParticleSystem.MinMaxCurve(0.05f, 0.12f);
        main.startColor      = new ParticleSystem.MinMaxGradient(new Color(0.7f, 0.1f, 0.9f, 0.65f));
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles    = 40;

        var em = ps.emission;
        em.rateOverTime = 6f;

        var sh = ps.shape;
        sh.shapeType = ParticleSystemShapeType.Circle;
        sh.radius    = 0.2f;

        main.gravityModifier = new ParticleSystem.MinMaxCurve(-0.08f);

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(new Color(0.7f, 0.1f, 0.9f), 0f), new GradientColorKey(new Color(0.4f, 0f, 0.6f), 1f) },
            new[] { new GradientAlphaKey(0.8f, 0f), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(g);

        var ren = go.GetComponent<ParticleSystemRenderer>();
        ren.renderMode     = ParticleSystemRenderMode.Billboard;
        ren.sortingLayerID = 0;
        ren.sortingOrder   = 5;
        if (mat != null) ren.sharedMaterial = mat;
    }

    private GameObject CreateChild(Transform parent, string name)
    {
        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        GameObjectUtility.SetParentAndAlign(go, parent.gameObject);
        return go;
    }

    private void RemoveChild(Transform parent, string name)
    {
        var existing = parent.Find(name);
        if (existing != null) Undo.DestroyObjectImmediate(existing.gameObject);
    }

    private Material FindGlowMaterial()
    {
        var guids = AssetDatabase.FindAssets("t:Material M_OrbGlow_Additive");
        if (guids.Length == 0)
        {
            Debug.LogWarning("[FragmentResultVfxSetup] M_OrbGlow_Additive が見つかりません。ParticleSystemRendererのMaterialは手動でアサインしてください。");
            return null;
        }
        return AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guids[0]));
    }
#endif
}
