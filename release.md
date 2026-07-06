# AxarDB Release Notes — Parameterized Database Configuration & Clean Architecture Refactoring

This release introduces parameterized runtime configuration management with CLI overrides, and completely reorganizes the bootstrapping layer into modular, clean components following SOLID and DRY principles.

---

## 🌟 New Features & Improvements

### 1. Parameterized Database Settings & CLI Overrides
- Moved all hardcoded database boundaries, memory limits, and query timeouts into `appsettings.json`.
- Implemented command-line argument bindings via `ConfigHelper` to easily override settings at startup.
- Introduced parameter mappings for:
  - `--memory-limit` (MemoryLimitPercentage)
  - `--bulk-cache-limit` (BulkStoreMaxCacheBytes)
  - `--max-recursion` (MaxRecursionDepth)
  - `--query-timeout` (QueryTimeoutMinutes)
  - `--queue-poll-seconds` (QueuePollIntervalSeconds)

### 2. Clean Architecture & Bootstrapping Simplification
- **Extreme Program.cs Simplification**: Reduced `Program.cs` into a clean 3-line entry point delegating all start orchestration to `AppBootstrap.Run(args)`.
- **Modular Middlewares**: Extracted inline middlewares into dedicated, isolated classes:
  - `GlobalExceptionHandlingMiddleware`: Centralizes error logging and Problem Details responses.
  - `RequestLoggingMiddleware`: Handles stopwatch profiling, request buffering, and metrics recording.
  - `BasicAuthenticationMiddleware`: Standardizes API security boundary checks.
- **Unified Endpoint Routing**: Extracted all API route mapping logic into a reusable `EndpointExtensions.MapDatabaseEndpoints()` method.
- **DRY Basic Authentication**: Consolidated redundant authentication parsing and decoding logic into `HttpContextExtensions.TryGetBasicCredentials()`.
