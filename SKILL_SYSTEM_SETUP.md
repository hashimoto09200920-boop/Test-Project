# スキルシステム セットアップガイド

## 実装完了したコンポーネント

### ✅ スクリプト
- `SkillType.cs` - スキルのカテゴリと効果タイプの定義
- `SkillDefinition.cs` - スキルのScriptableObject
- `SkillManager.cs` - スキル管理とeffect適用
- `SkillSelectionUI.cs` - スキル選択画面（3択UI）
- `SkillCardUI.cs` - 個別のスキルカード

### ✅ システム拡張
- `EnemySpawner` - 敵撃破カウント追加、Stage進行にスキル選択組み込み
- `PaddleCostManager` - スキル用セッター追加
- `StrokeManager` - スキル用セッター追加
- `PaddleDrawer` - Lifetime/Hardness/JustDamage用セッター追加
- `PixelDancerController` - HP用セッター追加
- `FloorHealth` - HP用セッター追加

---

## Unity Editorでの設定手順

### 1. スキルDefinitionの作成

Projectウィンドウで以下を実行：

#### カテゴリA（Stage 1クリア後）のスキル

1. **白線最大値アップ**
   ```
   右クリック > Create > Game/Skills/Skill Definition
   名前: Skill_A1_LeftMaxCostUp
   ```
   - Skill Name: `白線最大値アップ`
   - Description: `白線の最大値が増加する`
   - Category: `CategoryA`
   - Effect Type: `LeftMaxCostUp`
   - Effect Value: `5` (加算)
   - Is Multiplier: `false`

2. **赤線最大値アップ**
   ```
   名前: Skill_A2_RedMaxCostUp
   ```
   - Skill Name: `赤線最大値アップ`
   - Description: `赤線の最大値が増加する`
   - Category: `CategoryA`
   - Effect Type: `RedMaxCostUp`
   - Effect Value: `2` (加算)
   - Is Multiplier: `false`

3. **白線回復量アップ**
   ```
   名前: Skill_A3_LeftRecoveryUp
   ```
   - Skill Name: `白線回復速度アップ`
   - Description: `白線の回復速度が増加する`
   - Category: `CategoryA`
   - Effect Type: `LeftRecoveryUp`
   - Effect Value: `1` (加算)
   - Is Multiplier: `false`

4. **赤線回復量アップ**
   ```
   名前: Skill_A4_RedRecoveryUp
   ```
   - Skill Name: `赤線回復速度アップ`
   - Description: `赤線の回復速度が増加する`
   - Category: `CategoryA`
   - Effect Type: `RedRecoveryUp`
   - Effect Value: `0.5` (加算)
   - Is Multiplier: `false`

5. **最大ストローク数アップ**
   ```
   名前: Skill_A5_MaxStrokesUp
   ```
   - Skill Name: `線本数アップ`
   - Description: `同時に引ける線の数が増加する`
   - Category: `CategoryA`
   - Effect Type: `MaxStrokesUp`
   - Effect Value: `1` (加算)
   - Is Multiplier: `false`

6. **Just反射ダメージアップ**
   ```
   名前: Skill_A6_JustDamageUp
   ```
   - Skill Name: `Just反射強化`
   - Description: `Just反射時のダメージが増加する`
   - Category: `CategoryA`
   - Effect Type: `JustDamageUp`
   - Effect Value: `0.2` (加算)
   - Is Multiplier: `false`

#### カテゴリB（Stage 2クリア後）のスキル

7. **白線持続時間延長**
   ```
   名前: Skill_B1_LeftLifetimeUp
   ```
   - Skill Name: `白線持続時間延長`
   - Description: `白線が消えるまでの時間が延長される`
   - Category: `CategoryB`
   - Effect Type: `LeftLifetimeUp`
   - Effect Value: `0.3` (加算)
   - Is Multiplier: `false`

8. **赤線持続時間延長**
   ```
   名前: Skill_B2_RedLifetimeUp
   ```
   - Skill Name: `赤線持続時間延長`
   - Description: `赤線が消えるまでの時間が延長される`
   - Category: `CategoryB`
   - Effect Type: `RedLifetimeUp`
   - Effect Value: `0.3` (加算)
   - Is Multiplier: `false`

9. **Hardnessアップ**
   ```
   名前: Skill_B3_HardnessUp
   ```
   - Skill Name: `線の硬度アップ`
   - Description: `線が弾に強くなる`
   - Category: `CategoryB`
   - Effect Type: `HardnessUp`
   - Effect Value: `1` (加算)
   - Is Multiplier: `false`

10. **Pixel DancerのHPアップ**
    ```
    名前: Skill_B4_PixelDancerHPUp
    ```
    - Skill Name: `プレイヤーHP増加`
    - Description: `Pixel DancerのHPが増加する`
    - Category: `CategoryB`
    - Effect Type: `PixelDancerHPUp`
    - Effect Value: `1` (加算)
    - Is Multiplier: `false`

11. **FloorのHPアップ**
    ```
    名前: Skill_B5_FloorHPUp
    ```
    - Skill Name: `床HP増加`
    - Description: `床のHPが増加する`
    - Category: `CategoryB`
    - Effect Type: `FloorHPUp`
    - Effect Value: `2` (加算)
    - Is Multiplier: `false`

### 2. 05_Gameシーンの設定

1. **SkillManagerの追加**
   - 05_Gameシーンを開く
   - 空のGameObject作成 → 名前: `SkillManager`
   - `SkillManager` コンポーネントをアタッチ
   - Referencesは自動検索されるので設定不要

2. **SkillSelectionUIの作成**
   - Canvas作成（なければ既存のものを使用）
   - Canvas配下に空のGameObject作成 → 名前: `SkillSelectionUI`
   - `SkillSelectionUI` コンポーネントをアタッチ
   - UI構築:
     ```
     SkillSelectionUI
     ├─ SelectionPanel (Panel)
     │  ├─ TitleText (TextMeshPro)
     │  ├─ SkillCard1 (Button + SkillCardUI)
     │  │  ├─ SkillNameText (TextMeshPro)
     │  │  ├─ DescriptionText (TextMeshPro)
     │  │  ├─ EffectValueText (TextMeshPro)
     │  │  └─ IconImage (Image)
     │  ├─ SkillCard2 (同上)
     │  └─ SkillCard3 (同上)
     ```

3. **SkillSelectionUIの設定**
   - `Selection Panel`: 作成したPanel
   - `Skill Cards`: SkillCard1, 2, 3の配列
   - `Title Text`: TitleTextコンポーネント
   - `Category A Skills`: カテゴリAのスキル6個を配列に設定
   - `Category B Skills`: カテゴリBのスキル5個を配列に設定

4. **EnemySpawnerの設定**
   - EnemySpawnerを選択
   - `Skill Selection UI`: 作成したSkillSelectionUIをアサイン

### 3. スキルカードUIの詳細設定

各SkillCardUIコンポーネントに以下を設定：
- `Button`: 自身のButtonコンポーネント
- `Skill Name Text`: スキル名表示用TMP
- `Description Text`: 説明文表示用TMP
- `Effect Value Text`: 効果値表示用TMP
- `Icon Image`: アイコン表示用Image（Optional）
- `Background Image`: 背景Image（Optional）

---

## 動作フロー

1. **Stage 1クリア**
   - 敵撃破数をカウント
   - スキル選択画面表示（カテゴリA、撃破数回）
   - プレイヤーがスキルを選択
   - SkillManagerに追加 → 即座に効果適用

2. **Stage 2開始**
   - 選択したスキルの効果が適用された状態でプレイ

3. **Stage 2クリア**
   - 敵撃破数をカウント
   - スキル選択画面表示（カテゴリB、撃破数回）
   - プレイヤーがスキルを選択
   - SkillManagerに追加 → 即座に効果適用

4. **Stage 3開始**
   - カテゴリA + カテゴリBのスキルが適用された状態でプレイ

5. **Stage 3クリア**
   - スキル選択なし
   - リザルト画面へ

6. **03_AreaSelectへ戻る**
   - 05_Gameシーンが破棄される
   - SkillManagerも破棄され、全スキル効果がリセット

---

## テスト方法

1. 05_Gameシーンでプレイ開始
2. Stage 1で適当に敵を倒す（例：3体）
3. Stage 1クリア後、スキル選択が3回表示されることを確認
4. スキルを選択し、効果が適用されることを確認
5. Stage 2で同様にテスト
6. Stage 3クリア後、スキル選択が表示されないことを確認

---

## 調整可能なパラメータ

各スキルのScriptableObjectで調整可能：
- **Effect Value**: 効果の強さ
- **Is Multiplier**: 乗算 or 加算
- **Rarity Color**: スキルカードの背景色
- **Icon**: スキルアイコン（Optional）

### 推奨設定例

**強力なスキル（乗算）:**
```
Effect Value: 1.5 (1.5倍)
Is Multiplier: true
Rarity Color: 金色 (1, 0.84, 0, 1)
```

**通常スキル（加算）:**
```
Effect Value: 適度な値
Is Multiplier: false
Rarity Color: 白 (1, 1, 1, 1)
```

---

## 完了！

これでスキルシステムの実装は完了です。Unity Editorで上記の設定を行えば、ヴァンパイアサバイバー風のスキル選択システムが動作します。
