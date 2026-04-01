using UnityEngine;
using UnityEditor;

/// <summary>
/// シールド破壊エフェクト（ガラス割れ・青系）のPrefabを生成するエディタースクリプト
/// GemRevealParticleの設定を流用し、色を青系グラデーションに変更
/// Tools > Create Shield Break VFX から実行（既存Prefabがあれば上書き）
/// </summary>
public class CreateShieldBreakVFX
{
    private const string PrefabPath = "Assets/Prefabs/Effects/VFX_ShieldBreak.prefab";

    [MenuItem("Tools/Create Shield Break VFX")]
    public static void Create()
    {
        GameObject go = new GameObject("VFX_ShieldBreak");
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ParticleSystemRenderer psRenderer = go.GetComponent<ParticleSystemRenderer>();

        // --- Main モジュール（GemRevealParticleから流用） ---
        var main = ps.main;
        main.duration = 1.0f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 3f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 4f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.2f);
        main.gravityModifier = new ParticleSystem.MinMaxCurve(0.05f);
        main.stopAction = ParticleSystemStopAction.Destroy;
        main.playOnAwake = true;

        // --- Emission: Burst 60粒（GemRevealParticleから流用） ---
        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new ParticleSystem.Burst[]
        {
            new ParticleSystem.Burst(0f, 60)
        });

        // --- Shape: Cone（GemRevealParticleから流用） ---
        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 25f;

        // --- Color over Lifetime: 青系グラデーション ---
        // 白青 → 水色 → シアン → 深青 と変化し、末尾でフェードアウト
        var col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(new Color(0.8f, 0.95f, 1f), 0f),    // 白青
                new GradientColorKey(new Color(0.27f, 0.67f, 1f), 0.25f), // スカイブルー
                new GradientColorKey(new Color(0f, 0.8f, 1f),    0.5f),   // シアン
                new GradientColorKey(new Color(0.13f, 0.13f, 0.9f), 0.75f) // 深青
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(1f, 0f),   // 出現時: 不透明
                new GradientAlphaKey(1f, 0.75f), // 75%まで不透明を維持
                new GradientAlphaKey(0f, 1f)    // 末尾でフェードアウト
            }
        );
        col.color = new ParticleSystem.MinMaxGradient(gradient);

        // --- Renderer: Mesh(Quad)でガラス破片っぽく ---
        psRenderer.renderMode = ParticleSystemRenderMode.Mesh;
        psRenderer.mesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");

        // --- Prefab保存（既存があれば上書き） ---
        bool success = false;
        try
        {
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, PrefabPath, out success);
            if (success)
            {
                Debug.Log($"[CreateShieldBreakVFX] Prefab作成完了: {PrefabPath}");
                Selection.activeObject = prefab;
                EditorGUIUtility.PingObject(prefab);
            }
            else
            {
                Debug.LogError("[CreateShieldBreakVFX] Prefab保存失敗");
            }
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }
}
