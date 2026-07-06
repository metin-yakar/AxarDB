# AxarDB Release Notes - System Collection Restrictions & Bulk Query Memory Optimization

This release introduces protection against custom `sys`-prefixed collection names, and a major optimization for Bulk collection queries using memory-bounded chunked processing with temporary lightweight indexing.

---

## 🌟 New Features & Improvements

### 1. System Collection Name Restrictions

- **Reserved Prefix Enforcement**: Users are now blocked from creating, accessing, or inserting into any collection whose name starts with `sys`, unless it is one of the pre-defined system collections (`sysusers`, `sysqueue`, `sysvaults`, `sysconfig`).
- **Multi-layer Protection**: Restrictions are enforced at three levels:
  - `db.sysnew` — throws `InvalidOperationException` via `AxarDBBridge.TryGetMember`
  - `AxarDB("sysnew")` — throws `InvalidOperationException` via `DatabaseEngine` constructor binding
  - `db.sysnew.insert(...)` — blocked via `CollectionBridge.insert` and `Collection.Insert`
- **Rationale**: Prevents accidental or malicious creation of collections that shadow internal database infrastructure.

### 2. Bulk Query Memory-Bounded Chunked Processing

- **Problem Solved**: Previously, Bulk (JSONL) collection queries loaded all data into memory at once — unsafe for very large files.
- **Chunked Iteration**: `BulkStore.QueryChunks` now reads JSONL files line-by-line, accumulating data up to the configured `BulkStoreMaxCacheBytes` limit per chunk, then:
  1. Builds a **temporary lightweight index** containing only the fields accessed by the predicate.
  2. Runs the predicate (prediction/filtering) against the temp index.
  3. For matched entries, loads the full document for the result set.
  4. Releases chunk memory and advances to the next chunk.
- **Memory Cleanup**: After each chunk is processed, `GC.Collect(0, GCCollectionMode.Optimized)` is called to reclaim memory promptly.

### 3. Temporary Index Optimization for Bulk Predicates

- **Field Extraction via QueryOptimizer**: `QueryOptimizer.ExtractAccessedProperties(JsValue predicate)` uses regex analysis of the JavaScript predicate code to identify accessed property names (dot notation and bracket notation).
- **Selective Parsing**: `BulkStore.ParseSelectedProperties` uses `System.Text.Json.JsonDocument` to parse only the required fields from each JSON line, without full deserialization — significantly reducing memory pressure for large collections.
- **Always includes `_id`**: The `_id` field is always extracted regardless of the predicate.

### 4. Bulk Collection Bridge Refactoring

- Updated `findall`, `find`, `contains`, and `startsWith` in `BulkCollectionBridge` to accept `JsValue` instead of `Func<object, bool>`.
- `findall()` without a predicate still falls back to the fast cached `GetDocuments()` path — no chunk overhead when not needed.
- `contains` and `startsWith` now correctly route through `QueryChunks` using the `CaseInsensitiveDocumentWrapper`.

### 5. Documentation Fix

- Corrected misleading documentation in `Docs/llm_ragfile_en.md` regarding `findall()`. The function does NOT produce a runtime error when used without `.toList()`. It returns a `ResultSet` that is fully chainable and iterable.

---

## 🔧 Files Changed

| File | Change |
|---|---|
| `Definitions/Collection.cs` | Added sys-prefix validation in `Insert` |
| `Bridges/CollectionBridge.cs` | Restricted insert on sys-prefixed collections |
| `Bridges/AxarDBBridge.cs` | Blocked access to reserved sys-prefixed names in `TryGetMember` |
| `Core/DatabaseEngine.cs` | Blocked `AxarDB("sysnew")` constructor calls |
| `Query/QueryOptimizer.cs` | Added `ExtractAccessedProperties(JsValue)` method |
| `Bridges/BulkStore.cs` | Added `QueryChunks`, `ProcessChunk`, `ParseSelectedProperties`, `GetJsonValue` |
| `Bridges/BulkCollectionBridge.cs` | Refactored `findall`, `find`, `contains`, `startsWith` to use `QueryChunks` |
| `Docs/llm_ragfile_en.md` | Corrected `findall()` description |
