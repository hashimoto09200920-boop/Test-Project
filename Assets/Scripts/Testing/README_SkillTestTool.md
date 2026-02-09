# SkillTestTool 使い方ガイド

## 初期設定

### 1. SkillTestToolオブジェクトを作成
1. Hierarchy → 右クリック → Create Empty
2. 名前を **"SkillTestTool"** に変更
3. Inspector → Add Component → "SkillTestTool" で検索してアタッチ

### 2. 全スキルを自動追加
1. Inspector → **Skill Test Tool (Script)** のタイトル部分を**右クリック**
2. メニューから **"Auto-populate All Skills"** を選択
3. Category A/B/C に全スキルが自動追加されます

## 基本的な使い方

### スキルレベルの設定
- Inspector でスライダーを動かして各スキルのレベルを設定（0～10）
- **0 = 未取得**、1～10 = 取得回数
- **リアルタイム適用ON**: プレイ中にスライダーを動かすと即座に反映
- **Apply On Start ON**: プレイ開始時に自動適用

### スキルのリセット方法
以下のいずれかの方法でリセットできます：

#### 方法1: Context Menuから（推奨）
1. Inspector → **Skill Test Tool (Script)** を**右クリック**
2. **"Reset All Levels"** を選択
3. 全スキルレベルが0にリセットされます

#### 方法2: 手動でスライダーを0に
- 各スライダーを手動で0にドラッグ

## プリセット機能

### プリセットの作成
1. Project ウィンドウ → 右クリック
2. **Create → Game → Testing → Skill Test Preset** を選択
3. 名前を付ける（例: "TestPreset_AllMax", "TestPreset_OnlyA"）

### プリセットへの保存
1. SkillTestTool の Inspector で好きなスキル設定を作る
2. **Current Preset** フィールドに作成したプリセットをドラッグ＆ドロップ
3. **Skill Test Tool (Script)** を**右クリック**
4. **"Save to Preset"** を選択
5. 現在の設定がプリセットに保存されます

### プリセットからの読み込み
1. **Current Preset** フィールドに読み込みたいプリセットをドラッグ
2. **Skill Test Tool (Script)** を**右クリック**
3. **"Load from Preset"** を選択
4. プリセットの設定が読み込まれます

## 右下の表示

プレイ中、画面右下に以下の形式で表示されます：

```
■カテゴリA
A1_LMaxCst×2 (+10.0)
A6_JDmgUp (x1.50)

■カテゴリB
B7_ShldBrkBst (x1.50 5s)

■カテゴリC
C1_JWndExt×2 (+0.050s)
C3_Heal (30s)
```

### 表示形式
- **A1_LMaxCst**: カテゴリ番号_省略名
- **×2**: 取得回数（2回以上の場合）
- **(+10.0)**: 現在の効果値
- **範囲表示**: ランダム値スキルは「+5.0~10.0」のように表示

## Context Menuの全機能

| メニュー項目 | 説明 |
|---|---|
| Apply All Skills | 手動で全スキルを適用（通常は不要） |
| Reset All Levels | 全スキルレベルを0にリセット |
| Auto-populate All Skills | Resources配下の全スキルを自動追加 |
| Save to Preset | 現在の設定をプリセットに保存 |
| Load from Preset | プリセットから設定を読み込み |

## トラブルシューティング

### Context Menuが表示されない
- Inspector で **"Skill Test Tool (Script)"** のタイトル部分を右クリック
- または、コンポーネント右端の **三点リーダー（⋮）** をクリック

### スキルが反映されない
- **Apply On Start** がONになっているか確認
- または Context Menu → **"Apply All Skills"** を実行

### 数値が変動する
- ランダム値スキルは範囲表示（例: +5.0~10.0）になります
- これは正常な動作です

### プレイ中にリセットしたい
1. Context Menu → **"Reset All Levels"** を実行
2. または全スライダーを0にドラッグ
