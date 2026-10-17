using UnityEditor;
using UnityEngine;

/// <summary>
/// Area09ボス「スサノオ」の吸収モード（体が赤い間、反射弾を吸収してHP回復）で、吸収した瞬間に出す回復エフェクトを作る。
/// 見た目はArea05ボスFingersの「BurstEyeHitVFX」（BossHandController.SetupBurstEyeHitParticle）と同じ
/// （案A：紫の吸収の粒 ＋ 案B：白〜ピンクの火花）。共有コード・Fingersには一切触れない。
/// 作成先：Assets/Prefabs/Enemies/Susanoo.prefab > Susanoo > AbsorbHitVFX（＋子のSpark）。
/// 位置・色・量は作成後にInspectorで調整できる（再実行すると作り直すので、調整後は再実行しないこと）。
/// </summary>
public static class SusanooAbsorbHitVfxTool
{
    private const string PrefabPath = "Assets/Prefabs/Enemies/Susanoo.prefab";
    private const string VfxName = "AbsorbHitVFX";

    [MenuItem("Tools/Susanoo/吸収時の回復エフェクトを作成（Fingersと同じ見た目）")]
    private static void Create()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        if (root == null)
        {
            Debug.LogWarning("[SusanooAbsorbHitVfxTool] " + PrefabPath + " が見つかりません");
            return;
        }
        try
        {
            var susanoo = root.GetComponent<SusanooController>();
            if (susanoo == null)
            {
                Debug.LogWarning("[SusanooAbsorbHitVfxTool] Susanoo（ルート）にSusanooControllerがありません");
                return;
            }
            var so = new SerializedObject(susanoo);
            var bodySrProp = so.FindProperty("bodySpriteRenderer");
            SpriteRenderer bodySr = bodySrProp != null ? bodySrProp.objectReferenceValue as SpriteRenderer : null;
            if (bodySr == null) bodySr = root.GetComponent<SpriteRenderer>();
            int baseLayer = bodySr != null ? bodySr.sortingLayerID : 0;
            int baseOrder = bodySr != null ? bodySr.sortingOrder : 6;

            Transform existing = root.transform.Find(VfxName);
            if (existing != null) Object.DestroyImmediate(existing.gameObject);

            var wispMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Background/Mat_WispParticle.mat");

            var rootGo = new GameObject(VfxName);
            rootGo.transform.SetParent(root.transform, false);
            rootGo.transform.localPosition = Vector3.zero;

            // ===== 案A：紫の吸収の粒（Fingers BurstEyeHitVFX と同じ設定） =====
            var psA = rootGo.AddComponent<ParticleSystem>();
            {
                var main = psA.main;
                main.loop = false;
                main.duration = 0.5f;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(4f, 10f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.13f, 0.26f);
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.8f, 0.2f, 1f, 1f), new Color(0.5f, 0f, 0.8f, 1f));
                main.gravityModifier = 0f;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.maxParticles = 120;
                main.playOnAwake = false;
                main.stopAction = ParticleSystemStopAction.None;

                var emission = psA.emission;
                emission.rateOverTime = 0f;
                emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 40) });

                var shape = psA.shape;
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = 0.08f;

                var col = psA.colorOverLifetime;
                col.enabled = true;
                var grad = new Gradient();
                grad.SetKeys(
                    new[] {
                        new GradientColorKey(new Color(0.9f, 0.5f, 1f), 0f),
                        new GradientColorKey(new Color(0.7f, 0.1f, 1f), 0.5f),
                        new GradientColorKey(new Color(0.3f, 0f, 0.6f), 1f)
                    },
                    new[] {
                        new GradientAlphaKey(1f, 0f),
                        new GradientAlphaKey(0.6f, 0.4f),
                        new GradientAlphaKey(0f, 1f)
                    });
                col.color = new ParticleSystem.MinMaxGradient(grad);

                var rA = psA.GetComponent<ParticleSystemRenderer>();
                rA.sortingLayerID = baseLayer;
                rA.sortingOrder = baseOrder + 2;
                if (wispMat != null) rA.sharedMaterial = wispMat;
            }

            // ===== 案B：白〜ピンクの火花（Fingers BurstEyeHitVFX/Spark と同じ設定） =====
            var sparkGo = new GameObject("Spark");
            sparkGo.transform.SetParent(rootGo.transform, false);
            sparkGo.transform.localPosition = Vector3.zero;
            var psB = sparkGo.AddComponent<ParticleSystem>();
            {
                var main = psB.main;
                main.loop = false;
                main.duration = 0.3f;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.12f, 0.25f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(8f, 20f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.18f);
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 1f, 1f, 1f), new Color(1f, 0.8f, 1f, 1f));
                main.gravityModifier = 0.2f;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.maxParticles = 120;
                main.playOnAwake = false;
                main.stopAction = ParticleSystemStopAction.None;

                var emission = psB.emission;
                emission.rateOverTime = 0f;
                emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 60) });

                var shape = psB.shape;
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = 0.05f;

                var col = psB.colorOverLifetime;
                col.enabled = true;
                var grad = new Gradient();
                grad.SetKeys(
                    new[] {
                        new GradientColorKey(new Color(1f, 1f, 1f), 0f),
                        new GradientColorKey(new Color(1f, 0.6f, 1f), 0.4f),
                        new GradientColorKey(new Color(0.8f, 0.2f, 0.9f), 1f)
                    },
                    new[] {
                        new GradientAlphaKey(1f, 0f),
                        new GradientAlphaKey(0f, 1f)
                    });
                col.color = new ParticleSystem.MinMaxGradient(grad);

                var rB = psB.GetComponent<ParticleSystemRenderer>();
                rB.renderMode = ParticleSystemRenderMode.Stretch;
                rB.velocityScale = 0.08f;
                rB.lengthScale = 2.5f;
                rB.sortingLayerID = baseLayer;
                rB.sortingOrder = baseOrder + 3;
                if (wispMat != null) rB.sharedMaterial = wispMat;
            }

            so.FindProperty("absorbHitParticle").objectReferenceValue = psA;
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Debug.Log($"[SusanooAbsorbHitVfxTool] {PrefabPath} > Susanoo > {VfxName} を作成し、SusanooController.Absorb Hit Particle に設定しました" +
                      (wispMat == null ? "（Mat_WispParticleが見つからず、マテリアル未設定）" : ""));
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
