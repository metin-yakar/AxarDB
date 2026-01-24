# UnlockDB Documentation for AI Models

This file teaches AI models how to use UnlockDB. UnlockDB is a **NoSQL database** that runs in memory. It uses **JavaScript** for queries.

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
// Get all users as array
var list = db.users.findall().toList();

// Get users older than 18
var adults = db.users.findall(user => user.age > 18).toList();

// Get users named "Alice"
var alice = db.users.findall(u => u.name == "Alice").toList();
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
*   **Auth**: Basic (User: `unlocker`, Pass: `unlocker`)

```bash
# Basic query
curl -X POST "http://localhost:5000/query" \
     -u "unlocker:unlocker" \
     -d "db.users.findall().toList()"

# With parameter
curl -X POST "http://localhost:5000/query?ageLimit=20" \
     -u "unlocker:unlocker" \
     -d "db.users.findall(u => u.age > @ageLimit).toList()"

# Access public view
curl "http://localhost:5000/views/activeUsers"
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
- **UnlockDB Web UI**: Navigate to `http://localhost:5000`
