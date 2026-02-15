# PaddleCostBarUI セットアップ手順

白線・赤線・総本数をバー形式で表示するUIの作成手順です。
**全ての作業をPlay前のUnity Editorで行います。**

---

## 🚀 クイックスタート（自動セットアップ）

最も簡単な方法：

1. **Canvasに空のGameObjectを作成**
   - Hierarchy → Canvas内で右クリック → Create Empty
   - 名前: `PaddleCostBarUI`
   - RectTransform設定:
     - Anchor: Top-Left
     - Pivot: (0, 1)
     - Pos X: 350, Pos Y: -20
     - Width: 250, Height: 150

2. **PaddleCostBarUIコンポーネントをアタッチ**
   - Add Component → `PaddleCostBarUI`

3. **自動セットアップを実行**
   - PaddleCostBarUI Inspectorで右クリック
   - **Setup Hierarchy** を選択
   - マテリアル、プレハブ、UI階層が自動生成されます

4. **タイルを生成**
   - PaddleCostBarUI Inspectorで右クリック
   - **Generate Stroke Tiles** を選択

これで完了です！以下は手動セットアップの手順です（自動セットアップで問題がある場合のみ参照）。

---

## 📋 事前準備（手動セットアップ）

### 1. マテリアルの作成

#### 白線バー用マテリアル
1. Project → `Assets/Materials/` に移動（フォルダがなければ作成）
2. 右クリック → Create → Material
3. 名前: `WhiteBarGradient`
4. Inspector:
   - Shader: `UI/HorizontalGradient` を選択
   - Color Left: 白 `(1, 1, 1, 1)`
   - Color Right: ネオンシアン `(0, 2.5, 2.0, 1)` ※HDR有効

#### 赤線バー用マテリアル
1. 同様に新規マテリアル作成
2. 名前: `RedBarGradient`
3. Inspector:
   - Shader: `UI/HorizontalGradient` を選択
   - Color Left: ネオンオレンジ `(2.5, 1.0, 0, 1)` ※HDR有効
   - Color Right: ネオンレッド `(2.5, 0, 0, 1)` ※HDR有効

### 2. タイルプレハブの作成

1. Hierarchy → 右クリック → UI → Image
2. 名前: `StrokeTile`
3. RectTransform設定:
   - Width: 30
   - Height: 30
4. Image設定:
   - Color: 白（デフォルト）
   - Source Image: UI Sprite（デフォルトでOK）
5. Project内にドラッグしてPrefab化 → `Assets/Prefabs/UI/StrokeTile.prefab`
6. Hierarchyから削除

---

## 🎨 UI階層構造の作成

### 1. メインオブジェクト作成

Hierarchy → Canvas配下で作業

```
Canvas
└── PaddleCostBarUI (Empty GameObject)
```

1. Canvas内で右クリック → Create Empty
2. 名前: `PaddleCostBarUI`
3. RectTransform設定（画面左上、SkillHUDの右側を想定）:
   - Anchor: Top-Left
   - Pivot: (0, 1)
   - Pos X: 350 （SkillHUDの右側）
   - Pos Y: -20 （上端から20px下）
   - Width: 250
   - Height: 150
4. PaddleCostBarUIコンポーネントをアタッチ:
   - Add Component → `PaddleCostBarUI`

---

### 2. 白線バー (WhiteBar)

```
PaddleCostBarUI
└── WhiteBar (Empty GameObject)
    ├── Background (Image)
    ├── FillBar (Image)
    └── ValueText (TextMeshPro)
```

#### WhiteBar（親オブジェクト）
1. PaddleCostBarUI内で右クリック → Create Empty
2. 名前: `WhiteBar`
3. RectTransform:
   - Anchor: Top-Left
   - Pivot: (0, 1)
   - Pos X: 0, Pos Y: -10
   - Width: 200, Height: 20

#### Background（背景）
1. WhiteBar内で右クリック → UI → Image
2. 名前: `Background`
3. RectTransform:
   - Anchor: Stretch (横方向いっぱい)
   - Left: 0, Right: 0, Top: 0, Bottom: 0
4. Image:
   - Color: 黒 `(0, 0, 0, 0.5)` （半透明）

#### FillBar（グラデーションバー）
1. WhiteBar内で右クリック → UI → Image
2. 名前: `FillBar`
3. RectTransform:
   - Anchor: Stretch
   - Left: 0, Right: 0, Top: 0, Bottom: 0
4. Image:
   - Image Type: **Filled**
   - Fill Method: **Horizontal**
   - Fill Origin: **Left**
   - Fill Amount: 1.0（初期値）
   - Material: `WhiteBarGradient` を割り当て

#### ValueText（数値表示）
1. WhiteBar内で右クリック → UI → Text - TextMeshPro
2. 名前: `ValueText`
3. RectTransform:
   - Anchor: Middle-Right
   - Pivot: (0, 0.5)
   - Pos X: 210 （バーの右外側）
   - Pos Y: 0
   - Width: 60, Height: 20
4. TextMeshPro:
   - Text: "20.0/20.0" （プレースホルダー）
   - Font Size: 14
   - Color: 白
   - Alignment: Left, Middle
   - Font Asset: NotoSansJP-Regular SDF（日本語対応）

---

### 3. 赤線バー (RedBar)

WhiteBarと同じ構造を複製して作成します。

1. Hierarchy内でWhiteBarを複製（Ctrl+D）
2. 名前を`RedBar`に変更
3. RectTransform:
   - Pos Y: -40 （白線バーの下）
4. FillBar:
   - Material: `RedBarGradient` に変更

---

### 4. 総本数バー (StrokeBar)

```
PaddleCostBarUI
└── StrokeBar (Empty GameObject)
    └── TileContainer (HorizontalLayoutGroup)
        ├── (タイルは後で自動生成)
```

#### StrokeBar（親オブジェクト）
1. PaddleCostBarUI内で右クリック → Create Empty
2. 名前: `StrokeBar`
3. RectTransform:
   - Anchor: Top-Left
   - Pivot: (0, 1)
   - Pos X: 0, Pos Y: -80
   - Width: 200, Height: 40

#### TileContainer（タイルコンテナ）
1. StrokeBar内で右クリック → Create Empty
2. 名前: `TileContainer`
3. RectTransform:
   - Anchor: Stretch
   - Left: 0, Right: 0, Top: 0, Bottom: 0
4. コンポーネント追加: **Horizontal Layout Group**
   - Child Alignment: Middle Left
   - Child Force Expand: Width OFF, Height OFF
   - Spacing: 5
5. コンポーネント追加: **Content Size Fitter** (オプション)
   - Horizontal Fit: Preferred Size

---

## ⚙️ Inspector設定

PaddleCostBarUIコンポーネントのInspector設定:

### References
- Cost Manager: `PaddleCostManager` をドラッグ
- Stroke Manager: `StrokeManager` をドラッグ

### White Line Bar
- White Bar Fill: `WhiteBar/FillBar` のImageをドラッグ
- White Bar Text: `WhiteBar/ValueText` のTextMeshProUGUIをドラッグ
- White Bar Material: `WhiteBarGradient` をドラッグ
- White Bar Color Left: (1, 1, 1, 1)
- White Bar Color Right: (0, 2.5, 2.0, 1) ※HDR有効にする

### Red Line Bar
- Red Bar Fill: `RedBar/FillBar` のImageをドラッグ
- Red Bar Text: `RedBar/ValueText` のTextMeshProUGUIをドラッグ
- Red Bar Material: `RedBarGradient` をドラッグ
- Red Bar Color Left: (2.5, 1.0, 0, 1) ※HDR有効
- Red Bar Color Right: (2.5, 0, 0, 1) ※HDR有効

### Stroke Tiles Bar
- Tile Container: `StrokeBar/TileContainer` のTransformをドラッグ
- Tile Prefab: `StrokeTile` プレハブをドラッグ
- Tile Active Color: (2.0, 0, 2.5, 1) ※HDR有効（ネオンパープル）
- Tile Inactive Color: (0.3, 0.3, 0.3, 1)

### Text Settings
- Text Color: 白 (1, 1, 1, 1)
- Font Size: 14
- Number Format: "F1" （小数点第1位まで表示）

---

## 🔧 タイル自動生成

1. PaddleCostBarUIコンポーネントのInspectorで右クリック
2. **Generate Stroke Tiles** を実行
3. StrokeManager.MaxStrokesの数だけタイルが自動生成されます

---

## ✅ 動作確認

1. Unity Editorで再生
2. 白線・赤線を描いて、バーが減るか確認
3. 時間経過でバーが回復するか確認
4. ストロークを描いて、タイルが明るくなるか確認
5. ストロークが消えて、タイルが暗くなるか確認

---

## 🎨 調整ポイント

### 位置・サイズ調整
- PaddleCostBarUIのPos X/Yで全体の位置を調整
- 各バーのPos Yで縦間隔を調整
- ValueTextのPos Xで数値位置を調整

### 色調整
- InspectorのColor設定でHDR有効にして、値を2.0以上にするとネオン感が出ます
- Materialのカラー変更後は、ContextMenu → **Refresh Material Colors** を実行

### タイル数変更
- StrokeManager.MaxStrokesを変更
- PaddleCostBarUI → ContextMenu → **Generate Stroke Tiles** を再実行

---

## 🔴 注意事項

- **ランタイム生成は使用していません**（CRITICAL DESIGN PRINCIPLE準拠）
- 全ての調整はPlay前のInspectorで可能です
- マテリアルはインスタンス化されるため、元のマテリアルは変更されません
- HDRカラーを有効にするには、ColorフィールドをクリックしてHDRチェックボックスをONにします

---

## 📝 補足

### 既存のCostTextを無効化
新しいバーUIを使用する場合、既存のPaddleCostUIコンポーネントまたはCostTextオブジェクトを無効化してください。

1. Hierarchy → Canvas → CostText を探す
2. Inspectorでチェックボックスを外して無効化

### フォント設定
TextMeshProで日本語を表示する場合、Font AssetにNotoSansJP-Regular SDFなど日本語対応フォントを指定してください。
