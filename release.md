# AxarDB Release Notes — Benchmark Tool Improvements & Documentation Refresh

This release fixes the multi-engine benchmark tool (`compare.py`), corrects terminology in the generated HTML report, removes all non-English content from benchmark-related files, and updates documentation across the project to reflect the latest features.

---

## 🔧 Fixes & Improvements

### 1. Benchmark Tool (`compare.py`) — Now Fully Runnable

- **Fixed script cancellation:** The benchmark previously failed for all AxarDB stores (`memory`, `db`, `bulk`) with `"The script execution was canceled."` because the default `queryTimeoutMinutes` in `sysconfig` was too short for the workload. The tool now calls `db.sysconfig.findall().update({ queryTimeoutMinutes: 30 })` immediately after AxarDB becomes ready, extending the allowed script execution time before any benchmark jobs are queued.
- **Extended queue job await timeout:** The `_await_duration` polling timeout was increased from **60 seconds** to **300 seconds**, preventing false `None` results for slow operations such as JavaScript-loop-based bulk inserts in the `db` store.
- **Removed all Turkish content:** City name placeholders in benchmark data records were changed from Turkish cities to neutral international cities (`London`, `Berlin`, `Paris`, `Madrid`). No Turkish strings remain anywhere in `compare.py`.

### 2. HTML Report (`output.html`) — Label Correction

- **Corrected speedup row label:** The table row and chart section previously labeled `"Speedup vs AxarDB (memory) (x)"` have been renamed to **`"How many times faster is AxarDB (memory)?"`** in both the HTML table and the Chart.js bar chart dataset label.
- **No Turkish content:** Verified that `output.html` contains no Turkish-language strings.

### 3. README.md — Benchmark Section Refresh

- Updated the performance benchmarks section with data from the latest `output.html` run.
- Added a **Feature Comparison Matrix** table highlighting AxarDB-unique capabilities vs. PostgreSQL, MariaDB, and MongoDB.
- Added a prominent link to the interactive `output.html` report.
- Replaced the old speedup row label with **"How many times faster is AxarDB (memory)?"** to match the updated report.

### 4. `Docs/llm_ragfile_en.md` — Benchmark Documentation Expanded

- Extended Section 17 (Multi-Engine Benchmark Tool) with details about:
  - Automatic `queryTimeoutMinutes` extension at startup.
  - The 300-second queue job await timeout.
  - The new **"How many times faster is AxarDB (memory)?"** speedup label.
  - A description of all report sections (`Engine Status`, `Operation Times`, speedup chart, feature matrix, configuration).

### 5. `wwwroot/docs.html` — Latest Updates Section

- Added two new bullet points to the **Latest Updates** info box:
  - **UUID v7 as Default ID Scheme:** Clarifies that all collection types now use RFC 9562 UUID v7 by default, and documents the `guidv7()`, `guidv7(datetime)`, and `guidv7CreatedAt(guid)` query functions.
  - **Multi-Engine Benchmark Tool (`compare.py`):** Documents the `queryTimeoutMinutes` auto-expansion, 300-second await timeout, and the `"How many times faster is AxarDB (memory)?"` speedup chart.

---

## 🎯 Backward Compatibility

- No breaking changes to the AxarDB server, SDK, or query language.
- The `compare.py` benchmark script remains fully optional and does not affect the database runtime.
- All `sysconfig` changes made by the benchmark tool are temporary runtime updates and do not persist across server restarts unless the server is restarted after the update.
