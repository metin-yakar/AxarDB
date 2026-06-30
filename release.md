# AxarDB Release Notes — System Collection Security & Grouping

This release introduces grouping of system collections in the management console UI and enforces strict schema structure and type validation on the backend to prevent unauthorized or accidental modifications to system collections.

---

## 🌟 New Features & Improvements

### 1. Dedicated System Collections Group in Management Console
- System collections (collections starting with `sys`, e.g., `sysusers`, `sysvaults`, `sysqueue`) are now displayed under a dedicated **SYSTEM COLLECTIONS** header in the sidebar.
- Regular database collections remain under the **DATABASE COLLECTIONS** header, separating core application tables from system configuration tables.
- System collections retain their styling with the accent color and shield icons.

### 2. Strict Schema Structure Validation for System Collections
- Schema validation has been implemented on all inserts and updates to system collections.
- The system checks that the document keys match the expected keys of the predefined schema (or the first document's schema for dynamically created system collections).
- Any attempt to add new fields or remove existing ones will be blocked with an `InvalidOperationException`.

### 3. Strict Type Compatibility Check
- Standard system collections now enforce type checking on their fixed fields (e.g. `username`, `password`, `key`, `priority`, etc.).
- Dates (both C# `DateTime` and JSON strings) and numeric types (e.g. `double`, `int`, `long`) are verified for compatibility.
- Flexible fields (e.g. `value` in `sysvaults`, `parameters`, `options`, `successResult` in `sysqueue`) remain fully flexible to store dynamic payloads.

### 4. Deletion Protection
- Direct deletion of system collections via the backend is blocked.
- Any calls to delete a collection starting with `sys` will throw an error, preventing the loss of vital system structure and data.

