# AxarDB Release Notes

**Release Date**: 2026-02-16

---

## 🆕 New Features

### 🗂️ Tab System (Web Console)
Multi-tab support for the query editor. Users can now:
- **Open multiple query tabs** simultaneously, each with its own editor content and result grid
- **Add new tabs** with the **+** button at the bottom tab bar
- **Close tabs** individually with the **×** button (last tab cannot be closed)
- **Auto-naming**: tabs are automatically renamed based on the collection or view name detected in the executed query
- **Tab state persistence**: each tab independently stores its query, results, filters, and sort state

### 🔍 Smart View Parameter Detection (`extractViewParams`)
When clicking on a view in the sidebar, the system now:
- **Detects `@paramName` patterns** in the view script and prompts the user for parameter values
- **Detects `parameters.xxx` patterns** with their default values
- **Filters known directives** like `@access` so they are not treated as parameters
- Parameters with no explicit default receive an empty string default

### 📋 View Code Pretty-Print
When editing a view through the context menu, the code is now displayed in a **pretty-printed, human-readable format** using template literals instead of a single-line escaped string.

### 🌐 httpGet Function
New utility function for sending HTTP GET requests from within scripts:
```javascript
var response = httpGet("https://api.example.com/data", {
    "Authorization": "Bearer " + $API_TOKEN
});
// Returns: { success: true/false, status: 200, data: {...} }
```

### 📅 Date Utility Functions
New date manipulation functions available in scripts:
- `addMinutes(date, n)` — Add N minutes to a date
- `addHours(date, n)` — Add N hours to a date  
- `addDays(date, n)` — Add N days to a date

### 🧰 Additional Utility Functions
- `randomDecimal(min, max)` — Generate random decimal number within range
- `toJson(obj)` — Serialize any object to formatted JSON string
- `toString(obj)` — Convert any value to string
- `split(text, separator)` — Split string by separator
- `toDecimal(str)` — Parse string to decimal
- `deepcopy(obj)` — Deep clone an object (eliminates ValueKind artifacts)
- `getIndexes(name)` — List indexes for a given collection

---

## 🔧 Improvements

### Web Console UI
- **Tab bar** at the bottom of the main area with horizontal scrolling for many tabs
- **Query execution** now auto-updates the active tab title
- Tab bar styled consistently with the dark theme (glassmorphism, smooth transitions)

### View Sidebar Interaction
- Clicking a view now **loads the view's source code** into the editor with pretty-print formatting
- View parameters are extracted and displayed for the user to fill before execution
- Context menu on views provides **Edit** and **Delete** options

---

## 📚 Documentation Updates

### `Docs/llm_ragfile_en.md` — Complete Rewrite
Comprehensive rewrite to ensure AI models can generate error-free AxarDB queries:
- All utility functions documented with exact signatures and examples
- View `@param` syntax explained with real-world examples (login, filtered queries)
- ResultSet chain methods with correct `.toList()` usage rules
- Common patterns section (pagination, aggregation, token creation, password hashing)
- Troubleshooting guide for frequent errors
- ❌/✅ examples to highlight correct vs incorrect usage

### `PROJECT_PROMPT.md`
- Added `httpGet`, date functions, string utilities to the helper functions table
- Added Tab System, Query History, Smart View Click to Web Console section
- Updated Webhooks section to include both `webhook()` (POST) and `httpGet()` (GET)

### `README.md`
- Updated Key Features table: Webhooks & HTTP, Tab System, Query History, Smart View Click with `@param` detection
- Updated Management Console description with new capabilities
