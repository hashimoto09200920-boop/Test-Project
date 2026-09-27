using UnityEngine;

/// <summary>
/// 全エネミー共通のHPゴーストバー（被弾直後、減った分をゆっくり追いかける演出）の
/// 色・追従速度を一括管理するScriptableObject。
/// Assets/Resources/GameData/EnemyHealthGhostBarSettings.asset を1つだけ作り、
/// このアセットのInspectorを調整するだけで、EnemyHealthDisplayを持つ全エネミーに反映される
/// （個別エネミーPrefabを1体ずつ開いて編集する必要が無い）。
/// </summary>
[CreateAssetMenu(fileName = "EnemyHealthGhostBarSettings", menuName = "Game/Enemy Health Ghost Bar Settings")]
public class EnemyHealthGhostBarSettings : ScriptableObject
{
    [Tooltip("ゴーストバーの色（全エネミー共通）")]
    public Color ghostColor = new Color(1f, 0.25f, 0.1f, 0.9f);

    [Tooltip("ゴーストバーが1秒あたりに追いつく割合（0〜1）。0.6なら1秒でHP全体の60%分に相当する差を追いつく。値が大きいほど早く追いつく（全エネミー共通）")]
    public float catchUpSpeedPerSecond = 0.6f;
}
