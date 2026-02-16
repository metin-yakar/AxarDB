# AxarDB Documentation for AI Models

This file teaches AI models how to use AxarDB correctly. AxarDB is an **in-memory NoSQL database** that runs on ASP.NET Core. It uses **JavaScript** for all queries, powered by the Jint engine.

> **CRITICAL**: Read each section carefully. Follow the exact syntax shown. Incorrect usage (e.g., missing `.toList()`) will cause errors.

## 1. Core Concept

*   **Structure**: Database -> Collections (like tables) -> Documents (JSON objects).
*   **Language**: All queries are JavaScript code executed server-side.
*   **Root Object**: `db` is the main database object.
    *   `db.users` refers to the "users" collection.
    *   `db.orders` refers to the "orders" collection.
    *   Collections are created automatically on first write.

## 2. Data Operations (CRUD)

### A. Insert Data
Use `insert(object)`. Returns the inserted document with auto-generated `_id`.
```javascript
// Insert a single document
db.users.insert({ 
    name: "John", 
    age: 25, 
    isAdmin: false,
    tags: ["developer", "senior"]
});

// The _id field is generated automatically
db.products.insert({ name: "Laptop", price: 999.99, inStock: true });
```

### B. Find Data (Reading)

#### `findall()` — Returns a ResultSet (NOT an array)
> **CRITICAL RULE**: `findall()` returns a `ResultSet`, **NOT** an array. You **MUST** call `.toList()` or `.ToList()` to convert it to an array. Both casing variants work.

```javascript
// ✅ CORRECT — Get all users as array
var list = db.users.findall().toList();

// ✅ CORRECT — Filter with predicate
var adults = db.users.findall(u => u.age > 18).toList();

// ✅ CORRECT — Case variants both work
var items = db.orders.findall().ToList();

// ❌ WRONG — Returns ResultSet, not array!
var broken = db.users.findall();
// broken is NOT iterable as an array
```

#### `find()` — Returns one item (no `.toList()` needed)
```javascript
// Find first user matching condition
var admin = db.users.find(u => u.isAdmin == true);

// Returns null if not found — always check
var user = db.users.find(u => u.email == "john@test.com");
if (user) {
    console.log(user.name);
}
```

#### Boolean Filtering
Booleans are compared with `== true` or `== false`:
```javascript
var premiums = db.users.findall(u => u.isPremium == true).toList();
var freeUsers = db.users.findall(u => u.isPremium == false).toList();
```

### C. Update Data
Two ways to update:
```javascript
// 1. Direct update by condition (preferred for single field updates)
db.users.update(u => u._id == "abc123", { status: "active", updatedAt: new Date() });

// 2. Update via ResultSet chain
db.users.findall(u => u.inactive == true).update({ status: "archived" });
```

### D. Delete Data
```javascript
// Delete by filter
db.users.findall(u => u.age < 18).delete();

// Delete specific record
db.orders.findall(u => u._id == "order_123").delete();
```

## 3. ResultSet Chain Methods

After `findall()`, you can chain these methods **before** `.toList()`:

| Method | Description | Example |
|:---|:---|:---|
| `.toList()` / `.ToList()` | **Required** — Convert ResultSet to array | `findall().toList()` |
| `.take(n)` | Limit results to first N items | `findall().take(5).toList()` |
| `.select(fn)` | Project/transform each document | `findall().select(u => u.name).toList()` |
| `.count()` | Get total count (returns number, no `.toList()`) | `findall().count()` |
| `.first()` | Get first matching item (no `.toList()`) | `findall().first()` |
| `.foreach(fn)` | Execute callback for each item | `findall().foreach(u => console.log(u.name))` |
| `.update(obj)` | Update all matching records | `findall(u => u.old == true).update({old: false})` |
| `.delete()` | Delete all matching records | `findall(u => u.expired == true).delete()` |

### Chain Examples
```javascript
// Get names of top 5 expensive products
var top5 = db.products.findall(p => p.price > 1000)
                      .take(5)
                      .select(p => p.name)
                      .toList();

// Count active users
var count = db.users.findall(u => u.active == true).count();

// Get first admin
var firstAdmin = db.users.findall(u => u.isAdmin == true).first();

// Iterate and log each user
db.users.findall().foreach(u => {
    console.log(u.name + ": " + u.email);
});
```

### Case-Insensitive Search
Use `contains()` for fuzzy matching:
```javascript
var devs = db.users.contains(x => x.title == "developer");
// Matches "Developer", "DEVELOPER", "developer", etc.
```

## 4. Join Operations

Combine data from two collections:
```javascript
// Basic join
var result = db.join(db.users, db.orders)
    .where(x => x.userId == x.customerId)
    .toList();

// Join with projection
var data = db.join(db.products, db.categories)
    .select(x => ({
        name: x.productName,
        category: x.categoryName
    }));
```

## 5. Index Creation

Create indexes for faster queries:
```javascript
// ASC index (default)
db.users.index(x => x.email);

// DESC index
db.orders.index(x => x.createdAt, "DESC");

// Check existing indexes
getIndexes("users");
```

## 6. Views (Stored Queries)

Views are server-side stored JavaScript scripts saved as `.js` files in the `Views/` folder.

### Access Control
Every view **must** declare its access level on the first line as a comment:
- `// @access public` — Accessible via HTTP without authentication
- `// @access private` — Requires Basic Auth to access via HTTP

### Parameters in Views
Views use `@paramName` syntax for parameters. These are replaced with values from the HTTP query string.

```javascript
// Create a public view WITH parameters
db.saveView("getUsersByAge", `
// @access public
var minAge = @minAge;
var maxAge = @maxAge;
return db.users.findall(u => u.age >= minAge && u.age <= maxAge).toList();
`);

// Create a public view WITHOUT parameters
db.saveView("activeUsers", `
// @access public
return db.users.findall(u => u.active == true).toList();
`);

// Create a private view
db.saveView("internalReport", `
// @access private
return db.orders.findall().toList();
`);
```

### Using Views in JavaScript
```javascript
// Execute a view (no parameters)
var result = db.view("activeUsers");

// Execute a view with parameters
var result = db.view("getUsersByAge", { minAge: 18, maxAge: 65 });

// Read a view's source code
var code = db.getView("activeUsers");

// Delete a view
db.deleteView("oldView");
```

### Calling Views via HTTP
```bash
# Public view — NO authentication needed
curl "http://localhost:5000/views/activeUsers"

# Public view with parameters — values replace @param placeholders
curl "http://localhost:5000/views/getUsersByAge?minAge=18&maxAge=65"

# Private view — requires Basic Auth
curl -u "unlocker:unlocker" "http://localhost:5000/views/internalReport"
```

### View with Vault Variables and Encryption
Views can use vault variables (`$KEY_NAME`) and utility functions:
```javascript
db.saveView("login", `
// @access public
var email = @email;
var password = sha256(@password);
var deviceid = @deviceid;
var raw = email + "|" + password + "|" + deviceid;
var existing = db.users.find(u => u.email == email && u.password == password && u.deviceid == deviceid);
if (existing)
    return {status: true, token: encrypt(raw, $LOGIN_SALT)};
return {status: false, token: null};
`);
```
Call this view:
```bash
curl "http://localhost:5000/views/login?email=test@test.com&password=mypass&deviceid=DEV001"
```

## 7. Triggers (Event Handlers)

Triggers run automatically when data in a collection changes.

```javascript
// Create a trigger
db.saveTrigger("userNotifier", "users", `
// @target users
console.log("User changed: " + event.documentId);
webhook("https://api.example.com/notify", 
    { id: event.documentId, type: event.type },
    { "Authorization": "Bearer " + $API_TOKEN }
);
`);
```

### Event Object Properties
```javascript
event.type        // "created" | "changed" | "deleted"
event.collection  // Collection name that was modified
event.documentId  // The _id of the affected document
event.timestamp   // Server timestamp (ISO format)
```

### Manage Triggers
```javascript
db.deleteTrigger("oldTrigger");
```

## 8. Vaults (Secure Key-Value Storage)

Store API keys, secrets, and configuration values:
```javascript
// Add vault entries
addVault("API_KEY", "sk-xxxx...");
addVault("SLACK_WEBHOOK", "https://hooks.slack.com/...");
addVault("LOGIN_SALT", "mysecretkey123");

// Use in scripts with $KEY_NAME — replaced at runtime
webhook($SLACK_WEBHOOK, { text: "Alert!" }, {});
var token = encrypt("data", $LOGIN_SALT);
```

## 9. HTTP Functions

### webhook (POST)
Send HTTP POST requests to external services:
```javascript
// webhook(url, data, headers)
webhook("https://api.example.com/notify", 
    { userId: 123, action: "update" },
    { 
        "Authorization": "Bearer " + $API_TOKEN,
        "Content-Type": "application/json"
    }
);
// Returns: { success: true/false, status: 200, data: {...} }
```

### httpGet (GET)
Send HTTP GET requests:
```javascript
// httpGet(url, headers)
var response = httpGet("https://api.example.com/data", {
    "Authorization": "Bearer " + $API_TOKEN
});
// Returns: { success: true/false, status: 200, data: {...} }

// Without headers
var result = httpGet("https://api.example.com/public");
```

## 10. Utility Functions Reference

### Cryptographic & Encoding
| Function | Signature | Description | Example |
|:---|:---|:---|:---|
| `md5(str)` | `string -> string` | MD5 hash | `md5("hello")` → `"5d41402abc4b..."` |
| `sha256(str)` | `string -> string` | SHA256 hash | `sha256("hello")` → `"2cf24dba5fb..."` |
| `encrypt(text, salt)` | `string, string -> string` | AES encrypt (Base64 output) | `encrypt("secret", "mykey")` |
| `decrypt(text, salt)` | `string, string -> string` | AES decrypt | `decrypt("enc...", "mykey")` |
| `toBase64(str)` | `string -> string` | Base64 encode | `toBase64("hello")` → `"aGVsbG8="` |
| `fromBase64(str)` | `string -> string` | Base64 decode | `fromBase64("aGVsbG8=")` → `"hello"` |

### Random & ID Generation
| Function | Signature | Description | Example |
|:---|:---|:---|:---|
| `guid()` | `-> string` | Generate UUID | `guid()` → `"a1b2c3d4-..."` |
| `randomString(len)` | `int -> string` | Random alphanumeric | `randomString(10)` → `"kA9xPm3qZ2"` |
| `randomNumber(min, max)` | `int, int -> int` | Random integer in range | `randomNumber(1, 100)` → `42` |
| `randomDecimal(min, max)` | `string, string -> decimal` | Random decimal in range | `randomDecimal("0.01", "99.99")` |

### String & Conversion
| Function | Signature | Description | Example |
|:---|:---|:---|:---|
| `toString(obj)` | `object -> string` | Convert any value to string | `toString(42)` → `"42"` |
| `split(text, sep)` | `string, string -> string[]` | Split string by separator | `split("a,b,c", ",")` → `["a","b","c"]` |
| `toDecimal(str)` | `string -> decimal` | Parse string to decimal | `toDecimal("3.14")` → `3.14` |
| `toJson(obj)` | `object -> string` | Serialize object to JSON | `toJson({a:1})` → `'{"a":1}'` |
| `deepcopy(obj)` | `object -> object` | Deep clone an object | `var copy = deepcopy(original)` |

### Date Functions
| Function | Signature | Description | Example |
|:---|:---|:---|:---|
| `addMinutes(date, n)` | `date, double -> DateTime` | Add N minutes | `addMinutes(new Date(), 30)` |
| `addHours(date, n)` | `date, double -> DateTime` | Add N hours | `addHours(new Date(), 2)` |
| `addDays(date, n)` | `date, double -> DateTime` | Add N days | `addDays(new Date(), 7)` |

### System Functions
| Function | Signature | Description | Example |
|:---|:---|:---|:---|
| `showCollections()` | `-> string[]` | List all collection names | `showCollections()` |
| `getIndexes(name)` | `string -> object[]` | List indexes for collection | `getIndexes("users")` |
| `console.log(msg)` | `object -> void` | Print to server console | `console.log("debug: " + x)` |

## 11. Security

### Authentication
*   **Method**: HTTP Basic Auth
*   **Default User**: `unlocker` / `unlocker`
*   **Password Hashing**: Supports SHA256 hashed passwords in the `sysusers` collection
*   **Required for**: `POST /query`, `GET /collections`, private views

### Adding Database Users
```javascript
// Plain text password
db.sysusers.insert({ username: "admin", password: "admin123" });

// SHA256 hashed password (recommended)
db.sysusers.insert({ username: "admin", password: sha256("admin123") });
```

### Query Parameter Safety
Use `@placeholder` parameters to prevent injection:

**❌ UNSAFE — Never concatenate user input:**
```csharp
string script = "db.users.find(u => u.name == '" + userInput + "')"; 
```

**✅ SAFE — Use @param placeholders:**
```bash
# HTTP: POST /query?userName=John
# Body:
db.users.find(u => u.name == @userName);
```

The server replaces `@userName` with the JSON-serialized value of the query parameter, preventing injection.

### Input Validation
AxarDB blocks dangerous patterns: `eval()`, `Function()`, `<script>` tags, and other common injection vectors are automatically rejected.

## 12. API Endpoints

| Endpoint | Method | Description | Auth Required |
|:---|:---|:---|:---|
| `/query` | POST | Execute JavaScript script | ✅ Basic Auth |
| `/collections` | GET | List all collections | ✅ Basic Auth |
| `/views/{name}` | GET | Execute a view | 🔓 Public = No, 🔒 Private = Yes |
| `/docs` | GET | Documentation page | ❌ No |

### Curl Examples
```bash
# Execute a query
curl -X POST "http://localhost:5000/query" \
     -u "unlocker:unlocker" \
     -d "db.users.findall().toList()"

# Query with safe parameters
curl -X POST "http://localhost:5000/query?ageLimit=20" \
     -u "unlocker:unlocker" \
     -d "db.users.findall(u => u.age > @ageLimit).toList()"

# Insert data
curl -X POST "http://localhost:5000/query" \
     -u "unlocker:unlocker" \
     -d 'db.users.insert({ name: "Alice", age: 28 })'

# Access public view (no auth)
curl "http://localhost:5000/views/activeUsers"

# Access public view with parameters
curl "http://localhost:5000/views/login?email=test@test.com&password=mypass&deviceid=DEV001"

# Access private view (with auth)
curl -u "unlocker:unlocker" "http://localhost:5000/views/myPrivateView"
```

## 13. Web Management Console

AxarDB includes a built-in web console at `http://localhost:5000`:

- **Monaco Editor**: Write and execute JavaScript queries with syntax highlighting
- **Tab System**: Open multiple query tabs, each with its own editor and results
- **Sidebar**: Browse collections, views, and triggers; click to interact
- **Results Grid**: Sortable, filterable, column-resizable data table with JSON/CSV export
- **Query History**: Access previous queries with search/filter, stored in localStorage
- **Context Menu**: Right-click on sidebar items for Edit/Delete, or on result rows for Update/Delete
- **HTML Rendering**: Non-array results (strings, objects, HTML) render in an iframe

## 14. .NET SDK (C#)

```csharp
using AxarDB.Sdk;

// Initialize
using var client = new AxarClient("http://localhost:5000", "unlocker", "unlocker");

// Insert
await client.InsertAsync("users", new { Name = "John", Age = 30 });

// Query with parameters
var script = "db.users.findall(u => u.name == @name).toList()";
var users = await client.QueryAsync<List<User>>(script, new { name = "John" });

// Update
await client.UpdateAsync("users", "u => u.name == 'John'", new { Age = 31 });

// Builder pattern
var count = await client.Collection<User>("users").Where("age", ">", 18).CountAsync();
var first = await client.Collection<User>("users").FirstAsync();

// View management
await client.CreateViewAsync("ActiveUsers", "return db.users.findall(u => u.active).toList()");
var list = await client.CallViewAsync<List<User>>("ActiveUsers");
var filtered = await client.CallViewAsync<List<User>>("myview", new { minAge = 18 });

// Vault
await client.AddVaultAsync("MY_SECRET", "12345");
```

## 15. Python SDK

```python
from axardb import AxarClient

client = AxarClient("http://localhost:5000", "unlocker", "unlocker")

# Insert
client.insert("users", {"name": "John", "age": 30})

# Query
users = client.collection("users").where("age", ">", 20).to_list()

# Count & Delete
count = client.collection("users").count()
client.collection("users").where("age", "<", 18).delete()

# View management
client.create_view("MyView", "return db.users.take(10).toList()")
res = client.call_view("MyView")
res = client.call_view("MyView", {"minAge": 18})
```

## 16. CLI Tool

```bash
# Interactive login
./AxarDB.Cli -s "db.users.count()"

# With file input
./AxarDB.Cli -f query.js

# Fully automated (CI/CD)
./AxarDB.Cli -u admin -p pass -f query.js -o result.json

# Show collections
./AxarDB.Cli --show-collections

# Insert
./AxarDB.Cli --insert users "{\"name\":\"Alice\"}"
```

## 17. Common Patterns & Troubleshooting

### Frequent Mistakes

| Mistake | Fix |
|:---|:---|
| `db.users.findall()` without `.toList()` | Always add `.toList()` when you need an array |
| `db.view("name", { param: "value" })` for parameterless view | Omit the second argument: `db.view("name")` |
| Forgetting `// @access public` in view | Always add access comment as the first line |
| Using single `=` instead of `==` in predicates | Use `==` for comparison: `u => u.age == 25` |

### Common Query Patterns
```javascript
// Pagination (skip/take pattern)
var page2 = db.products.findall().take(10).toList(); // First 10

// Aggregation by iterating
var total = 0;
db.orders.findall(o => o.status == "completed").foreach(o => {
    total += o.amount;
});
return { totalRevenue: total };

// Check if collection has any data
var hasUsers = db.users.findall().count() > 0;

// Search by ID
var user = db.users.find(u => u._id == "some-id");

// Multi-field update
db.users.update(u => u._id == "abc", { 
    name: "Updated Name", 
    age: 30, 
    updatedAt: new Date() 
});

// Create a token
var token = encrypt(guid(), $MY_SALT);

// Hash a password
var hashed = sha256("plaintext_password");
```

### Troubleshooting
*   **401 Unauthorized**: Check credentials. Default is `unlocker:unlocker`.
*   **Empty Result**: Did you forget `.toList()`? `findall()` returns ResultSet, not array.
*   **Script Error**: Check JavaScript syntax. Use `console.log()` for debugging.
*   **Case Sensitivity**: Use `contains()` for case-insensitive search instead of `findall`.
*   **View 404**: Verify view name exactly matches. Check spelling.
*   **View returns error with parameters**: Ensure HTTP query string keys match `@param` names in the view script exactly.

### Backup
Copy the `Data/` folder to a safe location. All collections and their documents are stored there as files.
