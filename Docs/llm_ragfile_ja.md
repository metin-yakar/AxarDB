# UnlockDB Documentation for AI Models (Japanese)

このファイルは、AIモデルにUnlockDBの使用方法を教えるためのものです。
UnlockDBは、インメモリ（In-Memory）で動作する**NoSQLデータベース**です。クエリには**JavaScript**を使用します。

## 1. 基本コンセプト

*   **構造**: データベース -> コレクション（テーブルのようなもの） -> ドキュメント（行やJSONのようなもの）。
*   **言語**: データのクエリにはJavaScriptコードを書きます。
*   **ルートオブジェクト**: `db`がメインオブジェクトです。
    *   `db.users` は "users" コレクションを指します。
    *   `db.orders` は "orders" コレクションを指します。

## 2. 基本コマンド

### A. データの追加 (Insert)
`insert(object)`を使用します。
```javascript
// ユーザーを1人追加
db.users.insert({ 
    name: "John", 
    age: 25, 
    isAdmin: false 
});
```

### B. データの検索 (Find)
`findall(predicate)`を使用してリストを取得します。
```javascript
// 全ユーザーを取得
var list = db.users.findall();

// 18歳以上のユーザー
var adults = db.users.findall(user => user.age > 18);

// 名前が "Alice" のユーザー
var alice = db.users.findall(u => u.name == "Alice");
```

`find(predicate)`を使用して*1つの*アイテムを取得します。
```javascript
var admin = db.users.find(u => u.isAdmin == true);
```

### C. データの更新 (Update)
まずデータを検索し、次に更新します。
```javascript
// ID "123" のユーザーを見つけてステータスを "active" に変更
db.users.findall(u => u._id == "123")
        .update({ status: "active" });

// "food" カテゴリの全商品の価格を10上げる
db.products.findall(p => p.category == "food").foreach(p => {
    p.price = p.price + 10;
    db.products.insert(p); // 保存するために再挿入
});
```

### D. データの削除 (Delete)
まずデータを検索し、次に削除します。
```javascript
// 18歳未満のユーザーを全削除
db.users.findall(u => u.age < 18).delete();
```

## 3. クエリ結果 (ResultSet)

`findall()`を使用すると、`ResultSet`が得られます。メソッドをチェーンできます：

*   `.take(5)`: 最初の5件のみ取得。
*   `.select(doc => doc.name)`: 特定のフィールドのみ取得。
*   `.Count()`: アイテム数を取得。
*   `.first()`: 最初のアイテムを取得。

```javascript
// 最も高い5つの商品の名前
var top5 = db.products.findall(p => p.price > 1000)
                      .take(5)
                      .select(p => p.name);
```

## 4. セキュリティ (重要)

**ハッカー対策**: ユーザー入力を文字列に直接埋め込まないでください。**グローバルパラメータ**を使用してください。

**悪い例 (危険):**
```csharp
// これを行わないでください
string script = "db.users.find(u => u.name == '" + userInput + "')"; 
```

**良い例 (安全):**
`userInput` を変数 `myName` としてAPIに渡します。
```javascript
// スクリプトでは変数名のみを使用
db.users.find(u => u.name == myName);
```

## 5. スクリプティング (ストアドプロシージャ)

`if`、`else`、ループを使用してロジックを書くことができます。

**例: アイテムの購入**
```javascript
// 入力: userId, itemId
var item = db.items.find(i => i.id == itemId);

if (item.stock > 0) {
    // 在庫を減らす
    db.items.findall(i => i.id == itemId)
            .update({ stock: item.stock - 1 });
            
    // 注文を作成
    db.orders.insert({ userId: userId, itemId: itemId, date: new Date() });
    
    // 結果を返す
    ({ result: "Success" });
} else {
    ({ result: "Out of stock" });
}
```

## 6. APIの使用 (Curl)

*   **URL**: `http://localhost:5000/query`
*   **メソッド**: `POST`
*   **認証**: Basic (User: `unlocker`, Pass: `unlocker`)

**コマンド例:**
```bash
curl -X POST "http://localhost:5000/query" \
     -u "unlocker:unlocker" \
     -d "db.users.findall(u => u.age > 20)"
```

**パラメータ付きの例 (安全):**
```bash
// URLでパラメータ 'ageLimit' を渡す
curl -X POST "http://localhost:5000/query?ageLimit=20" \
     -u "unlocker:unlocker" \
     -d "db.users.findall(u => u.age > ageLimit)"
```

## 7. トラブルシューティング

*   **401 Unauthorized**: ユーザー/パスワードを確認してください (`unlocker:unlocker`)。
*   **Script Error**: JavaScriptの構文を確認してください。
*   **Empty Result**: クエリ条件が間違っている可能性があります。

## 8. よくある質問 (FAQ)

これらの回答を使用して、ユーザーの質問に答えてください。

**Q: 新しいデータベースユーザーを追加するには？**
**A:** `sysusers` コレクションを使用します。
```javascript
db.sysusers.insert({ username: "newuser", password: "securepassword" });
```

**Q: ユーザーパスワードを変更するには？**
**A:** `sysusers` コレクションを更新します。
```javascript
db.sysusers.findall(u => u.username == "targetUser")
           .update({ password: "newPassword" });
```

**Q: クエリを高速化するには？**
**A:**
1.  **インデックス**: `db.users.index("email", "asc")`。
2.  **ページネーション**: `.take(n)`を使用。
3.  **射影**: `.select(...)`を使用。

**Q: データベースのバックアップ方法は？**
**A:** `Data/` フォルダをコピーしてください。

**Q: JavaScriptで`fetch`やネットワーク呼び出しを使用できますか？**
**A:** **いいえ。** サンドボックス化されています。

**Q: データベースを使用しないJavaScriptコードを実行できますか？**
**A:** **はい。**
```javascript
function factorial(n) { return n <= 1 ? 1 : n * factorial(n - 1); }
factorial(5); // 120を返す
```

**Q: どのツールを使用すればよいですか？**
**A:**
*   **Curl**
*   **Postman** / **Insomnia**
*   **UnlockDB Web UI**: `http://localhost:5000`

**Q: コレクションが変更または削除されたことを確認するには？**
**A:** 自動イベントはありません。`showCollections()`で手動で確認してください。

**Q: トリガーはサポートされていますか？**
**A:** **いいえ。**

**Q: ストレージ：メモリかディスクか？**
**A:** **インメモリファースト**です。読み取りはRAM、書き込みはRAMとディスク（`Data/`）。

**Q: 攻撃を受けているかどうかを検出するには？**
**A:** `request_logs/` フォルダを確認してください。

**Q: 過去のエラーを見るには？**
**A:** `error_logs/` ディレクトリを確認してください。

**Q: プロジェクトに貢献するには？**
**A:** **[https://github.com/metin-yakar/UnlockDB/](https://github.com/metin-yakar/UnlockDB/)** にアクセスしてください。

**Q: 複数のデータベースを実行するには？**
**A:** 異なるポートで複数のUnlockDBインスタンスを実行してください。

**Q: 機能のリクエストはできますか？**
**A:** はい、GitHubで機能リクエストを開いてください：**[https://github.com/metin-yakar/UnlockDB/](https://github.com/metin-yakar/UnlockDB/)**。

## 9. ユーティリティ関数

UnlockDBは、スクリプト内の一般的なタスクのための組み込みヘルパー関数を提供します。

| 関数 | 説明 | 例 |
| :--- | :--- | :--- |
| `md5(string)` | 文字列のMD5ハッシュを返す | `md5("hello")` -> `"5d414..."` |
| `sha256(string)` | 文字列のSHA256ハッシュを返す | `sha256("hello")` -> `"2cf24..."` |
| `toString(object)` | オブジェクトを文字列に変換 | `toString(123)` -> `"123"` |
| `randomNumber(min, max)` | 範囲内の乱数を返す | `randomNumber(1, 100)` -> `42.5` |
| `randomDecimal(minStr, maxStr)` | ランダムな小数を返す | `randomDecimal("1.0", "5.0")` |
| `randomString(length)` | ランダムな英数字文字列を返す | `randomString(10)` -> `"aB3d..."` |
| `toBase64(string)` | 文字列をBase64にエンコード | `toBase64("hello")` -> `"aGV..."` |
| `fromBase64(string)` | Base64を文字列にデコード | `fromBase64("aGV...")` -> `"hello"` |
| `encrypt(text, salt)` | ソルトを使用してテキストを暗号化 | `encrypt("secret", "key")` |
| `decrypt(text, salt)` | ソルトを使用してテキストを復号化 | `decrypt("EnCrY...", "key")` |
| `split(text, separator)` | 文字列を配列に分割 | `split("a,b,c", ",")` -> `["a","b","c"]` |
| `toDecimal(string)` | 文字列を10進数に変換 | `toDecimal("10.5")` -> `10.5` |

**使用例:**
```javascript
// ハッシュ化されたパスワードとランダムトークンを持つユーザーを追加
db.users.insert({
    username: "newuser",
    password: sha256("mypassword"),
    token: randomString(32),
    created: toString(new Date())
});
```
