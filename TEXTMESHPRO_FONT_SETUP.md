# TextMeshPro 日本語フォント設定ガイド

---

## 注意（既存の NotoSansJP-Regular SDF について）

**現在の状態:** スキルカードで一部の日本語（例: 時間制御、線の硬度アップ）が空白や□になることがあります。NotoSansJP-Regular SDF のアトラスに含まれる文字が限られているためです。

**既存アセットの上書きは危険です:** このプロジェクトでは、既存の「NotoSansJP-Regular SDF」のアトラスを Font Asset Creator で再生成して上書き保存したところ、**全体の文字が文字化けする事象**が発生しました。そのため **既存の NotoSansJP-Regular SDF を Font Asset Creator で上書きする手順は推奨しません。**

改善する場合は、**新しい名前で別の Font Asset を生成**し、問題なければシーン側でその新しいアセットを参照するように切り替える方法を検討してください。

---

## 問題

スキルカードに日本語が表示されるはずが、文字化けして読めない状態になっています。

## 原因

TextMeshProのデフォルトフォントは日本語をサポートしていません。日本語を表示するには、日本語フォントアセットを作成して設定する必要があります。

---

## 解決方法

### 方法1: 日本語フォントアセットを作成する（推奨）

#### 1. 日本語フォントファイルを用意

プロジェクトに日本語フォント（.ttf または .otf）を配置します。

**推奨フォント:**
- **Noto Sans CJK JP** (Google製、商用利用可)
- **源ノ角ゴシック** (Adobe製、商用利用可)
- Windows標準の「メイリオ」や「Yu Gothic」など

**配置場所:**
```
Assets/Fonts/NotoSansCJKjp-Regular.ttf
```

#### 2. Font Assetを作成

1. Projectウィンドウで日本語フォントファイルを選択
2. 右クリック → `Create > TextMeshPro > Font Asset`
3. 作成されたFont Assetを選択
4. Inspectorで以下を設定:
   - **Atlas Resolution**: `4096 x 4096` (日本語は文字数が多いため大きめに)
   - **Character Set**: `Unicode Range (Hex)` を選択
   - **Character Sequence (Hex)**: 以下を入力
     ```
     20-7E,3000-303F,3040-309F,30A0-30FF,4E00-9FFF,FF00-FFEF
     ```
     （基本ラテン文字、日本語句読点、ひらがな、カタカナ、漢字、全角記号）
5. `Generate Font Atlas` ボタンをクリック

#### 3. TextMeshProのデフォルトフォントとして設定

**Option A: グローバル設定（全てのTextMeshProに適用）**
1. メニューから `Edit > Project Settings > TextMesh Pro > Settings`
2. `Default Font Asset` に作成したFont Assetを設定

**Option B: 既存のUIテキストに個別に適用**
1. Hierarchyで各SkillCardUIの子要素を選択:
   - SkillNameText
   - DescriptionText
   - EffectValueText
2. Inspectorで `Font Asset` を作成した日本語フォントに変更
3. SkillCard1, SkillCard2, SkillCard3 全てに対して実行

---

### 方法2: Unityの日本語サンプルフォントを使用する（簡易）

1. **Package Manager から日本語フォントをインポート**
   - Window > Package Manager
   - Packages: In Project > TextMeshPro を選択
   - Import TMP Examples & Extras（もしあれば）

2. **または、TMPの基本フォントで一時的に対処**
   - TextMeshProのデフォルトフォント "LiberationSans SDF" は一部の日本語（カタカナ・ひらがな）しか表示できません
   - 漢字は `□` のように四角で表示されます

---

## 最も簡単な解決方法（テスト用）

日本語フォントアセットの作成が難しい場合、**一時的に英語表記に変更**することもできます。

### スキル定義を英語に変更

`Tools > Skills > Create All Skill Definitions` の代わりに、英語版の作成スクリプトを実行:

```csharp
// CreateAllSkillDefinitions.cs の skillName, description を英語に変更
skillName = "White Line Max Up"
description = "Increases white line max capacity"
```

---

## 確認方法

1. 日本語フォントアセットを設定後、Unityエディタでプレイ
2. Stage 1をクリア後、スキルカードが表示される
3. スキル名「白線最大値アップ」が正しく表示されるか確認

---

## トラブルシューティング

### 日本語フォントを設定したのに文字化けする

**原因:** Font Atlas に必要な文字が含まれていない

**解決策:**
1. Font Asset を選択
2. Inspector の下部に `Character Search` ボックスがある
3. 「白線最大値アップ」などのスキル名を入力
4. 「Missing」と表示される文字がある場合は、Character Sequence に追加
5. `Generate Font Atlas` を再実行

### フォントアセットが巨大になってしまう

**原因:** 全ての漢字（数万文字）を含めようとしている

**解決策:**
1. **Dynamic Font Asset** を使用する:
   - Font Asset の `Atlas Population Mode` を `Dynamic` に変更
   - これで必要な文字だけが動的に追加されます
2. または、スキルで使用する文字だけを含める（軽量化）:
   ```
   白線最大値アップ赤線回復速度増加本数反射強化持続時間延長硬度プレイヤーH床
   ```

---

## 推奨設定（まとめ）

1. **Noto Sans CJK JP** をダウンロード（無料・商用可）
2. `Assets/Fonts/` に配置
3. Font Asset を作成（Atlas Resolution: 4096x4096, Dynamic モード推奨）
4. `Edit > Project Settings > TextMesh Pro > Settings` でデフォルトに設定
5. 既存のシーンを再度 `Tools > Skills > Setup Skill System in Current Scene` で再セットアップ

これで日本語が正しく表示されるようになります。
