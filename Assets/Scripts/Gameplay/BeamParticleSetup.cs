using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

// EnemyBeamBullet用の火花パーティクルを設定するEditor専用ヘルパー。
// BeamParticleオブジェクト（ParticleSystemコンポーネント済み）にこのスクリプトを一時的にアタッチし、
// ContextMenu「Setup Beam Spark Particle」を実行して初期値を適用する。確認後は本スクリプトを削除してよい。
public class BeamParticleSetup : MonoBehaviour
{
    [ContextMenu("Setup Beam Spark Particle")]
    private void Setup()
    {
        var ps = GetComponent<ParticleSystem>();
        if (ps == null)
        {
            Debug.LogError("[BeamParticleSetup] ParticleSystem not found.");
            return;
        }

        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.duration = 1f;
        main.loop = true;
        main.prewarm = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.3f);
        main.startSpeed = 0f; // 移動はVelocity over LifetimeのローカルY（ビームに対して垂直＝外向き）だけで作る
        main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.08f); // 少し大きめに
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.95f, 0.85f, 1f), new Color(1f, 0.15f, 0.05f, 1f)); // 白熱〜濃い赤（ビーム本体の赤よりも濃くする）
        main.gravityModifier = 0f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 300;

        // Rate over TimeはConstant=1にしておく。実行時にEnemyBeamBulletが
        // rateOverTimeMultiplier = ビーム長 × sparkDensityPerUnit を設定して実際の発生数を決める
        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 1f;

        // Box。Xはビームの長さに応じてEnemyBeamBulletが毎フレーム自動調整する（＝発生位置はビームの内側そのもの）
        // このオブジェクトのローカルX軸はEnemyBeamBulletが毎フレームビームの向きに回転させているため、
        // ローカルX＝ビームに沿った方向、ローカルY＝ビームに対して垂直（外向き）になる
        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(1f, 0.05f, 0.05f);
        shape.randomDirectionAmount = 0f; // 3D球状のランダム方向は使わない（ビームに沿う方向まで混ざってしまうため）

        // 移動はここで作る：メインはローカルY方向（ビームに対して垂直＝外向き）。
        // Xにも小さめのランダムを混ぜて角度にばらつきを持たせる（Xを0固定だと全粒子が
        // きっちり90度で飛び、魚の骨のように規則正しく見えてしまうため）。
        // XはYより小さい範囲に抑えることで、ビームに沿う方向（＝ビームの中を通っているように見える方向）
        // が支配的にならないようにする
        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.Local;
        vel.x = new ParticleSystem.MinMaxCurve(-0.6f, 0.6f);
        vel.y = new ParticleSystem.MinMaxCurve(-2f, 2f);
        vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        // 白熱→濃い赤に冷めながら透明にフェード（ビーム本体の赤{1,0.45,0.05}よりも濃い赤にする）
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(new Color(1f, 0.98f, 0.9f), 0f),
                new GradientColorKey(new Color(1f, 0.1f, 0.03f), 0.35f),
                new GradientColorKey(new Color(0.45f, 0.02f, 0.01f), 1f)
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0.7f, 0.35f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        col.color = new ParticleSystem.MinMaxGradient(grad);

        // 出た瞬間が一番大きく、縮みながら消える
        var sizeOL = ps.sizeOverLifetime;
        sizeOL.enabled = true;
        var sizeCurve = new AnimationCurve(
            new Keyframe(0f, 1f),
            new Keyframe(1f, 0f)
        );
        sizeOL.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        if (renderer != null)
        {
            renderer.sortingOrder = 11; // Beam本体(LineRenderer, sortingOrder=10)より前面

            // 金属加工の火花のような、各粒子の飛んでいく方向に細長く伸びる形状にする。
            // Stretched Billboardは「今の位置から速度と逆方向に尻尾を伸ばす」描画のため、
            // Velocity Scale(速度に比例した尻尾)を使うと、遅い粒子ほど「尻尾の長さ ÷ 速度」の時間が
            // 長くなり、寿命の大半（遅い粒子は寿命いっぱい）発生位置(ビーム上)を追い越して反対側まで
            // はみ出し続けてしまう。速度に依存しないSize×Length Scaleだけの固定の短い尻尾にする
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0f;
            renderer.lengthScale = 1.2f;
#if UNITY_EDITOR
            var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/M_BeamGlow_Additive.mat");
            if (mat != null) renderer.sharedMaterial = mat;
#endif
        }

#if UNITY_EDITOR
        EditorUtility.SetDirty(gameObject);
#endif
        Debug.Log("[BeamParticleSetup] 完了。確認後、このコンポーネント自体は削除してからPrefab化してください。");
    }
}
