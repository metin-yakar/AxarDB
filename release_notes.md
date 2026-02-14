# AxarDB Release Notes

This update addresses critical stability, security, and usability issues identified in the core database engine.

## 🚀 New Features

### 1. Dynamic Collection Reload
- **Requirement:** Ability to refresh a specific collection's data from disk without restarting the entire database server.
- **Solution:** Introduced `db.collection.reload()` command. This clears the in-memory cache for the specified collection and re-indexes the data from the disk storage.
- **Logging:** Execution of this command is logged to the system console for audit purposes.

### 2. Universal `toList()` Support
- **Requirement:** Script arrays and lists (e.g., from `Select` projections or JS arrays) needed a consistent way to be converted to a list format compatible with the template engine for HTML rendering.
- **Solution:** Extended the scripting engine to support `.toList()` on standard Arrays and generic lists, ensuring compatibility with the view engine.

### 3. Deep Copy Utility
- **Requirement:** Ability to duplicate objects and lists within a script to modify them without affecting the original references.
- **Solution:** Added a `deepcopy(obj)` function to the global script scope. This creates a full recursive copy of any object or list.

## 🐛 Bug Fixes & Improvements

### 4. Critical Memory Safety Fix (Immutable Reads)
- **Problem:** Modifying a document returned by a query (e.g., `db.users.find(...)`) would accidentally modify the underlying object in the database's in-memory cache. This persistent mutation would affect all subsequent queries until a restart, posing a severe data integrity and security risk.
- **Solution:** The engine now returns a **Deep Clone** of the cached document. Modifications to the returned object are isolated to the current script execution and do not corrupt the shared database state.

### 5. JSON Serialization Standardization
- **Problem:** JSON output sometimes contained internal .NET implementation details like `ValueKind` instead of the actual data, causing integration issues.
- **Solution:** Implemented a custom JSON converter in the storage layer to ensure all data is deserialized as primitive types (String, Number, Boolean, List, Dictionary) instead of restricted `JsonElement` structures. This guarantees standard, clean JSON output.
