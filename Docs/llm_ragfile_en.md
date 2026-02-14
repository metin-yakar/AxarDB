# AxarDB Documentation for AI Models

This file teaches AI models how to use AxarDB. AxarDB is a **NoSQL database** that runs in memory. It uses **JavaScript** for queries.

## 1. Core Concept

*   **Structure**: Database -> Collections (like Tables) -> Documents (like Rows/JSON).
*   **Language**: You write JavaScript code to query data.
*   **Root Object**: `db` is the main object.
    *   `db.users` refers to the "users" collection.
    *   `db.orders` refers to the "orders" collection.

## 2. Basic Commands

### A. Insert Data
Use `insert(object)`.
```javascript
db.users.insert({ 
    name: "John", 
    age: 25, 
    isAdmin: false 
});
```

### B. Find Data
Use `findall(predicate)` to get a ResultSet. **IMPORTANT: Use `.toList()` to get an array.**
```javascript
// Get all users as array (Both .toList() and .ToList() work)
var list = db.users.findall().toList();

// Get users older than 18
var adults = db.users.findall(user => user.age > 18).ToList();

// Boolean filtering works automatically
var premiums = db.users.findall(u => u.isPremium == true).toList();
```

Use `find(predicate)` to get *one* item (no toList needed).
```javascript
var admin = db.users.find(u => u.isAdmin == true);
```

### C. Update Data
```javascript
// Update by condition
db.users.update(u => u._id == "123", { status: "active" });

// Update via ResultSet
db.users.findall(u => u.inactive == true).update({ status: "archived" });
```

### D. Delete Data
```javascript
db.users.findall(u => u.age < 18).delete();
```

## 3. Query Results (ResultSet)

When you use `findall()`, you get a `ResultSet`. Chain methods:

*   `.toList()`: **Required** - Convert to array for display/return
*   `.take(5)`: Get only the first 5 results
*   `.select(doc => doc.name)`: Get only specific fields
*   `.count()`: Get the number of items
*   `.first()`: Get the first item
*   `.foreach(callback)`: Iterate over each item

```javascript
// Get names of top 5 expensive products
var top5 = db.products.findall(p => p.price > 1000)
                      .take(5)
                      .select(p => p.name)
                      .toList();
```

## 4. Views (Stored Queries)

### Create Public View (HTTP accessible)
```javascript
db.saveView("activeUsers", `
// @access public
return db.users.findall(u => u.active == true).toList();
`);
```

### Create Private View (Internal only)
```javascript
db.saveView("internalReport", `
// @access private
return db.orders.findall().toList();
`);
```

### Use View
```javascript
var result = db.view("activeUsers");
var code = db.getView("activeUsers");
```

## 5. Triggers (Event Handlers)

Triggers run automatically when data changes.

```javascript
db.saveTrigger("notifyOnChange", "users", `
// @target users
console.log("User changed: " + event.documentId);
webhook("https://api.example.com/notify", 
    { id: event.documentId, type: event.type },
    { "Authorization": "Bearer token123" }
);
`);
```

### Event Object
```javascript
event.type        // "created" | "changed" | "deleted"
event.collection  // Collection name
event.documentId  // Document _id
event.timestamp   // ISO timestamp
```

## 6. Webhooks

Send HTTP POST requests to external services.

```javascript
// webhook(url, data, headers)
webhook("https://api.example.com/data", 
    { userId: 123, action: "update" },
    { 
        "Authorization": "Bearer " + $API_TOKEN,
        "Content-Type": "application/json"
    }
);

// Slack notification
webhook($SLACK_WEBHOOK, 
    { text: "New order received!" },
    { "Content-Type": "application/json" }
);
```

## 7. Vaults (Secure Storage)

Store API keys and secrets securely.

```javascript
// Add to vault
addVault("API_KEY", "sk-xxxx...");
addVault("SLACK_WEBHOOK", "https://hooks.slack.com/...");

// Use in queries with $KEY_NAME
webhook($SLACK_WEBHOOK, { text: "Hello!" }, {});
```

## 8. Security (Important)

**Prevent Hackers**: Use `@placeholder` parameters.

**BAD (Unsafe):**
```csharp
string script = "db.users.find(u => u.name == '" + userInput + "')"; 
```

**GOOD (Safe):**
```bash
# HTTP: POST /query?userName=John
# Body:
db.users.find(u => u.name == @userName);
```

## 9. API Usage (Curl)

*   **URL**: `http://localhost:5000/query`
*   **Method**: `POST`
*   **Auth**: Basic (User: `unlocker`, Pass: `unlocker`). Supports SHA256 hashed passwords.

```bash
# Basic query
curl -X POST "http://localhost:5000/query" \
     -u "unlocker:unlocker" \
     -d "db.users.findall().toList()"

# With parameter
curl -X POST "http://localhost:5000/query?ageLimit=20" \
     -u "unlocker:unlocker" \
     -d "db.users.findall(u => u.age > @ageLimit).toList()"

# Access public view (NO AUTH REQUIRED)
curl "http://localhost:5000/views/activeUsers"

# Access private view (AUTH REQUIRED)
curl -u "unlocker:unlocker" "http://localhost:5000/views/myPrivateView"

# With parameters (Works for both public/private)
curl "http://localhost:5000/views/myview?minAge=18"
```

## 10. Utility Functions

| Function | Description | Example |
| :--- | :--- | :--- |
| `guid()` | Generate unique ID (UUID) | `guid()` -> `"a1b2c3..."` |
| `console.log(msg)` | Log to server console | `console.log("Hello")` |
| `webhook(url, data, headers)` | Send HTTP POST | See section 6 |
| `addVault(key, value)` | Store secret | `addVault("KEY", "val")` |
| `showCollections()` | List all collections | `showCollections()` |
| `md5(string)` | MD5 hash | `md5("hello")` |
| `sha256(string)` | SHA256 hash | `sha256("hello")` |
| `encrypt(text, salt)` | Encrypt text | `encrypt("secret", "key")` |
| `decrypt(text, salt)` | Decrypt text | `decrypt("enc...", "key")` |
| `randomString(length)` | Random alphanumeric | `randomString(10)` |
| `randomNumber(min, max)` | Random number | `randomNumber(1, 100)` |
| `toBase64(string)` | Encode to Base64 | `toBase64("hello")` |
| `fromBase64(string)` | Decode from Base64 | `fromBase64("aGV...")` |

## 11. Troubleshooting

*   **401 Unauthorized**: Check username/password (`unlocker:unlocker`).
*   **Empty Result**: Did you forget `.toList()`? `findall()` returns ResultSet, not array.
*   **Case Sensitivity**: Use `contains()` for case-insensitive search.
*   **Script Error**: Check JavaScript syntax.

## 12. Common FAQ

**Q: How do I add a new database user?**
Passwords can be stored as plain text or as a SHA256 hash for better security.
```javascript
db.sysusers.insert({ username: "newuser", password: sha256("password") });
```

**Q: How do I create an index?**
```javascript
db.users.index(x => x.email);       // ASC
db.users.index(x => x.createdAt, "DESC");  // DESC
```

**Q: How do I join collections?**
```javascript
var result = db.join(db.users, db.orders)
    .where(x => x.userId == x.customerId)
    .toList();
```

**Q: How do I backup the database?**
Copy the `Data/` folder to a safe location.

**Q: What tools can I use?**
- **Curl** (Command line)
- **Postman** / **Insomnia** (Desktop apps)

## 13. AxarDB .NET SDK

For .NET projects (Web, Console, MAUI), usage of the official SDK is recommended.

### Installation
Add the `AxarDB.Sdk` reference to your project.

### Basic Usage
```csharp
using AxarDB.Sdk;

// 1. Initialize
using var client = new AxarClient("http://localhost:5000", "unlocker", "unlocker");

// 2. Insert Data
await client.InsertAsync("users", new { Name = "John", Age = 30 });

// 3. Query Data (Safe with Parameters)
var script = "db.users.findall(u => u.name == @name).toList()";
var users = await client.QueryAsync<List<User>>(script, new { name = "John" });

// 4. Update Data
await client.UpdateAsync("users", "u => u.name == 'John'", new { Age = 31 });

// 6. Advanced Builder (Count, First, Select)
var count = await client.Collection<User>("users").Where("age", ">", 18).CountAsync();
var first = await client.Collection<User>("users").FirstAsync();

// 7. Management Functions
await client.CreateViewAsync("ActiveUsers", "return db.users.findall(u => u.active).toList()");
// Call without parameters
var list = await client.CallViewAsync<List<User>>("ActiveUsers");
// Call with parameters
var filtered = await client.CallViewAsync<List<User>>("myview", new { minAge = 18 });
await client.AddVaultAsync("MY_SECRET", "12345");
```

### Best Practices
- **Dependency Injection**: Register `AxarClient` as `Scoped` or `Singleton` in ASP.NET Core.
- **Async/Await**: Always use async methods.
- **Type Safety**: Create C# classes that match your AxarDB documents for better type safety.


## 14. AxarDB Python SDK

### Installation
```bash
pip install axardb-sdk
```

### Usage
```python
from axardb import AxarClient

client = AxarClient("http://localhost:5000", "unlocker", "unlocker")

# Insert
client.insert("users", {"name": "John", "age": 30})

# Query
users = client.collection("users").where("age", ">", 20).to_list()

# Advanced Query
count = client.collection("users").count()
client.collection("users").where("age", "<", 18).delete()

# Management
client.create_view("MyView", "return db.users.take(10).toList()")
# Call without parameters
res = client.call_view("MyView")
# Call with parameters
res = client.call_view("MyView", {"minAge": 18})
```


## 15. AxarDB CLI

Cross-platform command-line tool for managing AxarDB.

### Installation
Build from source:
```bash
cd SDKs/cli/AxarDB.Cli
dotnet publish -c Release
```

### Usage
```bash
# 1. Interactive Login
./AxarDB.Cli -s "db.users.count()"

# 2. Interactive with File Input (Prompts for credentials)
./AxarDB.Cli -f query.js

# 3. Fully Automated (CI/CD)
./AxarDB.Cli -u admin -p pass -f query.js -o result.json
```


