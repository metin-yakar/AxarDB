# AxarDB Release Notes — Case-Insensitivity, StartsWith & Turkish Character Normalization

This release resolves character encoding and casing issues with Turkish characters in query execution and parameters, and introduces case-insensitive `contains` and `startsWith` methods at both string and collection levels across all storage backends (`db`, `memory`, and `bulk`).

---

## 🌟 New Features & Improvements

### 1. Turkish Character Normalization & Casing Support
- **Jint JavaScript Engine Fix**: Overrode `String.prototype.toLowerCase` globally within Jint to correctly normalize Turkish characters (such as mapping `İ` -> `i`, `I` -> `i`, `ı` -> `i`, etc.) and eliminated Jint casing discrepancies caused by combining diacritical marks (`\u0307`).
- **C# Wrapper Casing Alignment**: Updated `CaseInsensitiveDocumentWrapper` to use a Turkish-normalized casing mapping and made property member access case-insensitive.

### 2. Case-Insensitive `contains` and `startsWith` APIs
- **String Prototype Methods**: Extended `String.prototype` inside the Jint environment with case-insensitive and Turkish-normalized versions of `contains` (aliased to includes) and `startsWith` for queries.
- **Collection-Level APIs**: Added case-insensitive `contains` and `startsWith` methods to `db.collection`, `memory.collection`, and `bulk.collection` to allow direct case-insensitive predicate filtering.

### 3. Jint Thread Safety & Query Optimization
- Addressed Jint engine corruption exceptions (such as `ReferenceError: x is not defined`) during parallel execution on the database level.
- Streamlined `Collection.FindAll` to load and deserialize documents in parallel while executing the Jint predicate evaluator sequentially on the main query thread.

### 4. JSONL/Bulk Integration & JsonElement Support
- Enabled `CaseInsensitiveDocumentWrapper` to inherit from `DocumentWrapper`, reusing the robust `Unwrap` logic to handle `JsonElement` values properly in JSONL-based Bulk collections.

### 5. UTF-8 HTTP Request Body Reading
- Configured the `/query` HTTP POST endpoint to read incoming HTTP request bodies explicitly using `Encoding.UTF8`, preventing encoding corruption from clients.
