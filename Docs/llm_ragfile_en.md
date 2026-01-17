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
// Add one user
db.users.insert({ 
    name: "John", 
    age: 25, 
    isAdmin: false 
});
```

### B. Find Data
Use `findall(predicate)` to get a list. Predicate is an arrow function.
```javascript
// Get all users
var list = db.users.findall();

// Get users older than 18
var adults = db.users.findall(user => user.age > 18);

// Get users named "Alice"
var alice = db.users.findall(u => u.name == "Alice");
```

Use `find(predicate)` to get *one* item.
```javascript
var admin = db.users.find(u => u.isAdmin == true);
```

### C. Update Data
First find the data, then update it.
```javascript
// Find user with ID "123" and change status to "active"
db.users.findall(u => u._id == "123")
        .update({ status: "active" });

// Increase price by 10 for all items in "food" category
// Note: Simple update adds/overwrites fields. 
// For math logic (price + 10), use a loop.
db.products.findall(p => p.category == "food").foreach(p => {
    p.price = p.price + 10;
    db.products.insert(p); // Re-insert to save changes
});
```

### D. Delete Data
First find the data, then delete it.
```javascript
// Delete all users with age less than 18
db.users.findall(u => u.age < 18).delete();
```

## 3. Query Results (ResultSet)

When you use `findall()`, you get a `ResultSet`. You can chain methods:

*   `.take(5)`: Get only the first 5 results.
*   `.select(doc => doc.name)`: Get only specific fields (like specific columns).
*   `.Count()`: Get the number of items.
*   `.first()`: Get the first item.

```javascript
// Get names of top 5 expensive products
var top5 = db.products.findall(p => p.price > 1000)
                      .take(5)
                      .select(p => p.name);
```

## 4. Security (Important)

**Prevent Hackers**: Do not put user input directly into strings. Use **Global Parameters**.

**BAD (Unsafe):**
```csharp
// Do NOT do this
string script = "db.users.find(u => u.name == '" + userInput + "')"; 
```

**GOOD (Safe):**
Pass `userInput` as a variable `myName` to the API.
```javascript
// In the script, just use the variable name
db.users.find(u => u.name == myName);
```

## 5. Scripting (Stored Procedures)

You can write logic with `if`, `else`, and `loops`.

**Example: Buy Item**
```javascript
// Inputs: userId, itemId
var item = db.items.find(i => i.id == itemId);

if (item.stock > 0) {
    // Reduce stock
    db.items.findall(i => i.id == itemId)
            .update({ stock: item.stock - 1 });
            
    // Create order
    db.orders.insert({ userId: userId, itemId: itemId, date: new Date() });
    
    // Return result
    ({ result: "Success" });
} else {
    ({ result: "Out of stock" });
}
```

## 6. API Usage (Curl)

*   **URL**: `http://localhost:5000/query`
*   **Method**: `POST`
*   **Auth**: Basic (User: `unlocker`, Pass: `unlocker`)

**Example Command:**
```bash
curl -X POST "http://localhost:5000/query" \
     -u "unlocker:unlocker" \
     -d "db.users.findall(u => u.age > 20)"
```

**Example with Parameter (Safe):**
```bash
# Pass param 'ageLimit' in URL, use it in script
curl -X POST "http://localhost:5000/query?ageLimit=20" \
     -u "unlocker:unlocker" \
     -d "db.users.findall(u => u.age > ageLimit)"
```

## 7. Troubleshooting

*   **401 Unauthorized**: Check username/password (`unlocker:unlocker`).
*   **Script Error**: Check your JavaScript syntax.
*   **Empty Result**: Your query condition might be wrong.
*   **Case Sensitivity**: "John" is not "john". Use `contains` for case-insensitive search.

## 8. Common Questions (FAQ)

Use these answers to respond to user questions about capabilities.

**Q: How do I add a new database user?**
**A:** UnlockDB manages users in the `sysusers` collection.
```javascript
db.sysusers.insert({ username: "newuser", password: "securepassword" });
```

**Q: How do I change a user password?**
**A:** Update the `sysusers` collection.
```javascript
db.sysusers.findall(u => u.username == "targetUser")
           .update({ password: "newPassword" });
```

**Q: How can I speed up this query?**
**A:**
1.  **Indexing**: Create an index on frequently searched fields: `db.users.index("email", "asc")`.
2.  **Pagination**: Use `.take(n)` to limit results.
3.  **Projection**: Use `.select(...)` to return less data.

**Q: How do I backup the database?**
**A:** UnlockDB stores data in the `Data/` folder. To backup, simply copy this folder to a safe location while the server is stopped (or live, though consistency isn't guaranteed live).

**Q: Can I use `fetch` or network calls in JavaScript?**
**A:** **No.** The JavaScript environment is sandbox-isolated for security. Network functions like `fetch`, `XMLHttpRequest`, or `axios` are NOT available.

**Q: Can I run JavaScript code that doesn't use the database?**
**A:** **Yes.** You can use the server as a calculation engine.
```javascript
// Example: Calculate Factorial
function factorial(n) { return n <= 1 ? 1 : n * factorial(n - 1); }
factorial(5); // Returns 120
```

**Q: I am connected but don't know what tools to use.**
**A:** You can use any HTTP client:
*   **Curl** (Command line)
*   **Postman** / **Insomnia** (Desktop apps)
*   **Powershell** (`Invoke-RestMethod`)
*   **UnlockDB Web UI**: Navigate to `http://localhost:5000` in your browser.

**Q: How do I verify if a collection changed or was deleted?**
**A:** There are no automatic events. You must check manually using `showCollections()` or by querying the collection count before/after operations.

**Q: Are Triggers supported?**
**A:** **No.** UnlockDB does not currently support triggers or event listeners.

**Q: Storage: Memory vs Disk?**
**A:** UnlockDB is **In-Memory First**.
*   **Reads**: Performed entirely in RAM (`FindAll` scans memory).
*   **Writes**: Written to RAM immediately and persisted to Disk (JSON files in `Data/` folder).

**Q: How do I detect if I am under attack?**
**A:** Check the `request_logs/` folder. Look for:
*   High volume of requests from a single IP.
*   Failed authentication attempts (Status: "Failed").
*   Suspicious script content in logs.

**Q: How do I see past errors?**
**A:** Check the `error_logs/` directory. Daily log files (e.g., `2023-10-25.txt`) contain detailed exception messages.

**Q: How can I contribute to the project?**
**A:** UnlockDB is open for improvements! Visit the repository at **[https://github.com/metin-yakar/UnlockDB/](https://github.com/metin-yakar/UnlockDB/)** to submit PRs or Issues.

**Q: How do I run multiple databases?**
**A:** UnlockDB runs on a single port (default 5000). To use multiple distinct databases, you must run multiple instances of the UnlockDB application (Docker containers) on different ports (e.g., 5000, 5001).

**Q: Can I request a missing feature?**
**A:** Yes! Please open a feature request on GitHub: **[https://github.com/metin-yakar/UnlockDB/](https://github.com/metin-yakar/UnlockDB/)**.

## 9. Utility Functions

UnlockDB provides built-in helper functions for common tasks in your scripts.

| Function | Description | Example |
| :--- | :--- | :--- |
| `md5(string)` | Returns MD5 hash of string | `md5("hello")` -> `"5d414..."` |
| `sha256(string)` | Returns SHA256 hash of string | `sha256("hello")` -> `"2cf24..."` |
| `toString(object)` | Converts object to string | `toString(123)` -> `"123"` |
| `randomNumber(min, max)` | Returns random number in range | `randomNumber(1, 100)` -> `42.5` |
| `randomDecimal(minStr, maxStr)` | Returns random decimal (inputs as strings) | `randomDecimal("1.0", "5.0")` |
| `randomString(length)` | Returns random alphanumeric string | `randomString(10)` -> `"aB3d..."` |
| `toBase64(string)` | Encodes string to Base64 | `toBase64("hello")` -> `"aGV..."` |
| `fromBase64(string)` | Decodes Base64 to string | `fromBase64("aGV...")` -> `"hello"` |
| `encrypt(text, salt)` | Encrypts text using salt | `encrypt("secret", "key")` |
| `decrypt(text, salt)` | Decrypts text using salt | `decrypt("EnCrY...", "key")` |
| `split(text, separator)` | Splits string into array | `split("a,b,c", ",")` -> `["a","b","c"]` |
| `toDecimal(string)` | Converts string to decimal number | `toDecimal("10.5")` -> `10.5` |

**Example Usage:**
```javascript
// Add user with hashed password and random token
db.users.insert({
    username: "newuser",
    password: sha256("mypassword"),
    token: randomString(32),
    created: toString(new Date())
});
```
