# UnlockDB Documentation for AI Models (Chinese)

此文件教导 AI 模型如何使用 UnlockDB。
UnlockDB 是一个在内存中运行的 **NoSQL 数据库**。它使用 **JavaScript** 进行查询。

## 1. 核心概念

*   **结构**: 数据库 -> 集合 (类似表) -> 文档 (类似行/JSON)。
*   **语言**: 您编写 JavaScript 代码来查询数据。
*   **根对象**: `db` 是主要对象。
    *   `db.users` 指的是 "users" 集合。
    *   `db.orders` 指的是 "orders" 集合。

## 2. 基本命令

### A. 插入数据 (Insert)
使用 `insert(object)`。
```javascript
// 添加一个用户
db.users.insert({ 
    name: "John", 
    age: 25, 
    isAdmin: false 
});
```

### B. 查找数据 (Find)
使用 `findall(predicate)` 获取列表。
```javascript
// 所有用户
var list = db.users.findall();

// 18岁以上的用户
var adults = db.users.findall(user => user.age > 18);

// 名为 "Alice" 的用户
var alice = db.users.findall(u => u.name == "Alice");
```

使用 `find(predicate)` 获取 *一个* 项目。
```javascript
var admin = db.users.find(u => u.isAdmin == true);
```

### C. 更新数据 (Update)
先找到数据，然后更新。
```javascript
// 找到 ID 为 "123" 的用户并将状态更改为 "active"
db.users.findall(u => u._id == "123")
        .update({ status: "active" });

// 将 "food" 类别中所有商品的价格提高 10
db.products.findall(p => p.category == "food").foreach(p => {
    p.price = p.price + 10;
    db.products.insert(p); // 重新插入以保存
});
```

### D. 删除数据 (Delete)
先找到数据，然后删除。
```javascript
// 删除所有 18 岁以下的用户
db.users.findall(u => u.age < 18).delete();
```

## 3. 查询结果 (ResultSet)

当您使用 `findall()` 时，您会得到一个 `ResultSet`。您可以链接方法：

*   `.take(5)`: 仅获取前 5 个结果。
*   `.select(doc => doc.name)`: 仅获取特定字段。
*   `.Count()`: 获取项目数量。
*   `.first()`: 获取第一个项目。

```javascript
// 最昂贵的前 5 个产品的名称
var top5 = db.products.findall(p => p.price > 1000)
                      .take(5)
                      .select(p => p.name);
```

## 4. 安全 (重要)

**预防黑客**: 不要将用户输入直接放入字符串中。使用 **全局参数**。

**坏 (不安全):**
```csharp
// 不要这样做
string script = "db.users.find(u => u.name == '" + userInput + "')"; 
```

**好 (安全):**
将 `userInput` 作为变量 `myName` 传递给 API。
```javascript
// 在脚本中，仅使用变量名
db.users.find(u => u.name == myName);
```

## 5. 脚本 (存储过程)

您可以使用 `if`、`else` 和循环编写逻辑。

**示例: 购买物品**
```javascript
// 输入: userId, itemId
var item = db.items.find(i => i.id == itemId);

if (item.stock > 0) {
    // 减少库存
    db.items.findall(i => i.id == itemId)
            .update({ stock: item.stock - 1 });
            
    // 创建订单
    db.orders.insert({ userId: userId, itemId: itemId, date: new Date() });
    
    // 返回结果
    ({ result: "Success" });
} else {
    ({ result: "Out of stock" });
}
```

## 6. API 使用 (Curl)

*   **URL**: `http://localhost:5000/query`
*   **方法**: `POST`
*   **认证**: Basic (User: `unlocker`, Pass: `unlocker`)

**示例命令:**
```bash
curl -X POST "http://localhost:5000/query" \
     -u "unlocker:unlocker" \
     -d "db.users.findall(u => u.age > 20)"
```

**带参数的示例 (安全):**
```bash
// 在 URL 中传递参数 'ageLimit'
curl -X POST "http://localhost:5000/query?ageLimit=20" \
     -u "unlocker:unlocker" \
     -d "db.users.findall(u => u.age > ageLimit)"
```

## 7. 故障排除

*   **401 Unauthorized**: 检查用户/密码 (`unlocker:unlocker`)。
*   **Script Error**: 检查您的 JavaScript 语法。
*   **Empty Result**: 您的查询条件可能错误。

## 8. 常见问题 (FAQ)

使用这些答案来回答用户问题。

**问: 如何添加新的数据库用户？**
**答:** UnlockDB 在 `sysusers` 集合中管理用户。
```javascript
db.sysusers.insert({ username: "newuser", password: "securepassword" });
```

**问: 如何更改用户密码？**
**答:** 更新 `sysusers` 集合。
```javascript
db.sysusers.findall(u => u.username == "targetUser")
           .update({ password: "newPassword" });
```

**问: 如何加快此查询速度？**
**答:**
1.  **索引**: `db.users.index("email", "asc")`.
2.  **分页**: 使用 `.take(n)`.
3.  **投影**: 使用 `.select(...)`.

**问: 如何备份数据库？**
**答:** 复制 `Data/` 文件夹。

**问: 我可以使用 `fetch` 吗？**
**答:** **不能。**

**问: 我可以运行不使用数据库的 JavaScript 吗？**
**答:** **可以。**
```javascript
function factorial(n) { return n <= 1 ? 1 : n * factorial(n - 1); }
factorial(5); // 返回 120
```

**问: 我应该使用什么工具？**
**答:**
*   **Curl**
*   **Postman**
*   **UnlockDB Web UI**: `http://localhost:5000`

**问: 如何知道集合是否已更改？**
**答:** 没有自动事件。使用 `showCollections()` 手动检查。

**问: 支持触发器吗？**
**答:** **不支持。**

**问: 存储: 内存还是磁盘？**
**答:** **内存优先**。读取在 RAM 中。写入在 RAM 和磁盘 (`Data/`)。

**问: 如何检测攻击？**
**答:** 检查 `request_logs/` 文件夹。

**问: 如何查看过去的错误？**
**答:** 检查 `error_logs/` 目录。

**问: 如何贡献？**
**答:** 访问 **[https://github.com/metin-yakar/UnlockDB/](https://github.com/metin-yakar/UnlockDB/)**。

**问: 如何运行多个数据库？**
**答:** 在不同端口上运行多个 UnlockDB 实例。

**问: 我可以请求功能吗？**
**答:** 可以，请在 GitHub 上打开功能请求：**[https://github.com/metin-yakar/UnlockDB/](https://github.com/metin-yakar/UnlockDB/)**。

## 9. 实用函数

UnlockDB 提供内置辅助函数，用于脚本中的常见任务。

| 函数 | 描述 | 示例 |
| :--- | :--- | :--- |
| `md5(string)` | 返回字符串的 MD5 哈希 | `md5("hello")` -> `"5d414..."` |
| `sha256(string)` | 返回字符串的 SHA256 哈希 | `sha256("hello")` -> `"2cf24..."` |
| `toString(object)` | 将对象转换为字符串 | `toString(123)` -> `"123"` |
| `randomNumber(min, max)` | 返回范围内的随机数 | `randomNumber(1, 100)` -> `42.5` |
| `randomDecimal(minStr, maxStr)` | 返回随机小数 | `randomDecimal("1.0", "5.0")` |
| `randomString(length)` | 返回随机字母数字字符串 | `randomString(10)` -> `"aB3d..."` |
| `toBase64(string)` | 将字符串编码为 Base64 | `toBase64("hello")` -> `"aGV..."` |
| `fromBase64(string)` | 将 Base64 解码为字符串 | `fromBase64("aGV...")` -> `"hello"` |
| `encrypt(text, salt)` | 使用盐加密文本 | `encrypt("secret", "key")` |
| `decrypt(text, salt)` | 使用盐解密文本 | `decrypt("EnCrY...", "key")` |
| `split(text, separator)` | 将字符串拆分为数组 | `split("a,b,c", ",")` -> `["a","b","c"]` |
| `toDecimal(string)` | 将字符串转换为十进制数 | `toDecimal("10.5")` -> `10.5` |

**使用示例:**
```javascript
// 添加具有哈希密码和随机令牌的用户
db.users.insert({
    username: "newuser",
    password: sha256("mypassword"),
    token: randomString(32),
    created: toString(new Date())
});
```
