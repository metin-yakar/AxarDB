# Release Notes

## New Features
- Implemented core database engine with Query Optimizer and Bulk Storage capabilities.
- Added modular storage architecture featuring Bulk and Memory bridges for caching and JSONL collection monitoring.
- Integrated automated backup query recovery system and `db.logsys` features.
- Added multi-tab query editor with Monaco integration and authentication management.
- Implemented REST API endpoints for database management and AI query assistance.
- Added UUID v7 support and migrated default `_id` generation.
- Added support for database clock time zone configuration through settings.
- Resolved key duplication in `sysvaults` type collections.
- Implemented collection definitions and hybrid memory-disk caching, indexing, and thread-safe data operations.
- Added `CaseInsensitiveDocumentWrapper` for case-insensitive data access.

## Fixes & Refactoring
- Refactored `resultset` functions and added a default serializable `toList()` conversion for `select` functions used in JOINs.
- Optimized index operations: added range operators, debounced disk saves, cleanup on delete, and removed redundant predicates.
- Fixed `addVault` error messages.
- Added chart for performance comparisons and updated benchmark results.

## Documentation
- Added comprehensive LLM RAG documentation, multilingual READMEs, and an initial documentation page with sidebar navigation/search.
- Exposed helper extensions for API endpoints and documented AI models.
