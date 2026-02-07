# Enemy行動ルーチン設定ガイド

## 概要
Enemy Assetで複数の弾タイプを使い分ける行動ルーチンを設定する方法です。

## 設定手順

### ステップ1: Bullet Typesを設定
**重要**: 「Bullet Types」と「Sequence Entries」は**別物**です。

1. `EnemyData`アセットを選択
2. **「Bullet Types (Optional)」**セクションを開く
3. **「Bullet Types」**配列の**「Size」**を設定したい弾の種類数に設定（例: 2）
   - **注意**: この「Size」は「Sequence Entries」の「Size」とは別物です
   - 「Bullet Types」の「Size」を変更すると、配列の要素数が変わります
4. **「Bullet Types」**配列の各要素を展開して、弾の設定を行う：
   - **Element 0**（Bullet Types[0]）: 1つ目の弾タイプ（例: 通常弾）
     - `Name`: "通常弾" など分かりやすい名前を設定
     - `Speed`: 速度を設定
     - `Damage`: ダメージを設定
     - `Sprite Override`: 見た目を設定
     - その他の設定項目を必要に応じて設定
   - **Element 1**（Bullet Types[1]）: 2つ目の弾タイプ（例: 貫通弾）
     - `Name`: "貫通弾" など分かりやすい名前を設定
     - `Speed`: 速度を設定
     - `Damage`: ダメージを設定
     - `Sprite Override`: 見た目を設定
     - その他の設定項目を必要に応じて設定

**確認方法**:
- 「Bullet Types」の「Size」を2に設定すると、**Element 0**と**Element 1**の両方が表示されます
- もしElement 1が見えない場合は、配列が折りたたまれている可能性があります
- 「Bullet Types」配列の左側の▶をクリックして展開してください

### ステップ2: 行動ルーチンを有効化
1. **「Bullet Firing Routine (行動ルーチン)」**セクションを開く
2. **「Use Firing Routine」**にチェックを入れる
3. **「Firing Routine」**を展開

### ステップ3-A: 順番発射（Sequence）を設定
**重要**: 「Sequence Entries」は「Bullet Types」とは**別の配列**です。

1. **「Routine Type」**を **「Sequence」** に設定
2. **「Sequence Settings (順番発射設定)」**を展開
3. **「Sequence Entries」**配列の**「Size」**を設定（例: 3）
   - **＋－ボタンで自由に要素数を追加/削除できます**
   - 同じBullet Typesを複数回選択可能（例: 弾1>弾2>弾2>ループ）
4. **「Sequence Entries」**配列の各要素を展開して設定：
   - **Element 0**:
     - `Bullet Type Index`: 0（Bullet Types[0]を選択）
       - このフィールドで、使用するBullet Typesのインデックスを選択します
     - `表示用（読み取り専用）`: "通常弾"（自動表示・読み取り専用）
       - 選択された弾タイプ名が自動表示されます
     - `Min Shots`: 2（最小発射回数）
     - `Max Shots`: 3（最大発射回数）
   - **Element 1**:
     - `Bullet Type Index`: 1（Bullet Types[1]を選択）
     - `表示用（読み取り専用）`: "貫通弾"（自動表示・読み取り専用）
     - `Min Shots`: 1
     - `Max Shots`: 2
   - **Element 2**（同じ弾を2回発射する例）:
     - `Bullet Type Index`: 1（Bullet Types[1]を再度選択）
     - `表示用（読み取り専用）`: "貫通弾"（自動表示・読み取り専用）
     - `Min Shots`: 1
     - `Max Shots`: 1

**動作例**: 通常弾を2～3回発射 → 貫通弾を1～2回発射 → 貫通弾を1回発射 → ループ

### ステップ3-B: 割合発射（Probability）を設定
1. **「Routine Type」**を **「Probability」** に設定
2. **「Probability Settings (割合発射設定)」**を展開
3. **「Probability Entries」**配列の**「Size」**を設定（例: 2）
   - **＋－ボタンで自由に要素数を追加/削除できます**
   - 同じBullet Typesを複数回選択可能
4. **「Probability Entries」**配列の各要素を展開して設定：
   - **Element 0**:
     - `Bullet Type Index`: 0（Bullet Types[0]を選択）
       - このフィールドで、使用するBullet Typesのインデックスを選択します
     - `表示用（読み取り専用）`: "通常弾"（自動表示・読み取り専用）
       - 選択された弾タイプ名が自動表示されます
     - `Probability Percentage`: 80（80%確率）
   - **Element 1**:
     - `Bullet Type Index`: 1（Bullet Types[1]を選択）
     - `表示用（読み取り専用）`: "貫通弾"（自動表示・読み取り専用）
     - `Probability Percentage`: 20（20%確率）

**動作**: 発射のたびに、80%の確率で通常弾、20%の確率で貫通弾を発射

## よくある質問

### Q1: 配列の横の数字（[0], [1], [2]）は何？
**A**: 配列の要素番号（インデックス）を表します。
- `Sequence Entries[0]` = Sequence Entries配列の1つ目の要素
- `Sequence Entries[1]` = Sequence Entries配列の2つ目の要素
- 各要素内の`Bullet Type Index`で、使用するBullet Typesのインデックスを選択します

### Q2: ＋と－ボタンが機能しない
**A**: 
- **「Bullet Types」**配列の＋－ボタンは機能します。ここで弾の種類を追加/削除できます。
- **「Sequence Entries」**と**「Probability Entries」**配列の＋－ボタンも機能します。
  - これらの配列サイズは自由に変更できます。
  - 同じBullet Typesを複数回選択可能です（例: 弾1>弾2>弾2>ループ）

### Q3: Sequence SettingsのBullet Type Nameを更新できない
**A**: 
- `表示用（読み取り専用）`フィールドは自動表示のため、手動で編集できません。
- 弾タイプを変更するには、`Bullet Type Index`フィールドを変更してください。
- `Bullet Type Index`を変更すると、`表示用（読み取り専用）`フィールドが自動更新されます。

### Q4: Bullet Typesを2、Sequence Entriesを3にして「弾1>弾2>弾2>ループ」は可能？
**A**: はい、可能です。
1. Bullet TypesのSizeを2に設定
2. Sequence EntriesのSizeを3に設定
3. Element 0: `Bullet Type Index` = 0（弾1）
4. Element 1: `Bullet Type Index` = 1（弾2）
5. Element 2: `Bullet Type Index` = 1（弾2を再度選択）

### Q3: どの弾タイプに対応しているかわからない
**A**: Sequence Entriesの各要素内に**「対応する弾タイプ」**フィールドがあります。
- このフィールドには、対応するBullet Typesの`Name`が自動表示されます
- 例: "通常弾", "貫通弾" など

### Q4: 発射中の弾が設定値を反映しているか確認したい
**A**: GameScene上でEnemyのHPテキストの下に、デバッグ情報が表示されます。
- 表示例: `Seq Elem:0 (通常弾)` または `Prob Elem:1 (貫通弾)`
- `Elem:0` = Bullet Types[0]を使用中
- `Elem:1` = Bullet Types[1]を使用中
- デバッグ表示は`EnemyHpDebugText`コンポーネントの`Show Routine Debug`でON/OFFできます

## テスト方法

1. **EnemyDataアセットを設定**（上記手順に従う）
2. **Enemy Prefab**に`EnemyData`を設定
3. **GameScene**でEnemyを配置
4. **Play Mode**で実行
5. **デバッグ表示を確認**:
   - HPテキストの下に黄色のテキストで「Seq Elem:0 (通常弾)」などが表示される
   - 実際に発射される弾の見た目や動作が、設定したBullet Typesの値と一致しているか確認

## トラブルシューティング

- **配列サイズが合わない**: Bullet Typesの数を変更すると自動調整されます。Inspectorを一度閉じて再度開くと反映されます。
- **デバッグ表示が出ない**: `EnemyHpDebugText`コンポーネントの`Show Routine Debug`がONになっているか確認してください。
- **弾が発射されない**: `Use Firing Routine`がONになっているか、`Bullet Types`が正しく設定されているか確認してください。
