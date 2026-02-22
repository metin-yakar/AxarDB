// AxarDB Web Interface
let editor;
let currentCollections = [];
let queryResults = [];
let currentDisplayData = []; // To easily reference data for grid actions
let sortCol = null;
let sortDir = 1;
let filters = {};
let lastCollectionName = "sysusers";
let activeHistoryId = null;
let _historyDebounceTimer = null;
let _suppressHistorySync = false;
let activeQueryController = null;

// --- Tab System ---
let tabs = [];
let activeTabId = null;
let _tabCounter = 0;

function generateTabId() {
    return 'tab_' + (++_tabCounter);
}

function createTab(title, script) {
    const id = generateTabId();
    const tab = {
        id,
        title: title || `Query ${_tabCounter}`,
        script: script || "// Type your JavaScript query here\n// Use 'db.collection' to access data\n",
        queryResults: [],
        filters: {},
        sortCol: null,
        sortDir: 1,
        lastCollectionName: 'sysusers'
    };
    tabs.push(tab);
    switchTab(id);
    return id;
}

function saveCurrentTabState() {
    if (!activeTabId) return;
    const tab = tabs.find(t => t.id === activeTabId);
    if (!tab) return;
    tab.script = editor ? editor.getValue() : '';
    tab.queryResults = queryResults;
    tab.filters = { ...filters };
    tab.sortCol = sortCol;
    tab.sortDir = sortDir;
    tab.lastCollectionName = lastCollectionName;
}

function restoreTabState(tab) {
    queryResults = tab.queryResults || [];
    filters = tab.filters || {};
    sortCol = tab.sortCol || null;
    sortDir = tab.sortDir || 1;
    lastCollectionName = tab.lastCollectionName || 'sysusers';
    if (editor) {
        _suppressHistorySync = true;
        editor.setValue(tab.script);
        _suppressHistorySync = false;
    }
    renderGrid();
}

function switchTab(id) {
    if (activeTabId === id) return;
    saveCurrentTabState();
    activeTabId = id;
    activeHistoryId = null;
    const tab = tabs.find(t => t.id === id);
    if (tab) restoreTabState(tab);
    renderTabBar();
}

function closeTab(id) {
    if (tabs.length <= 1) return;
    const idx = tabs.findIndex(t => t.id === id);
    if (idx === -1) return;
    tabs.splice(idx, 1);
    if (activeTabId === id) {
        const newIdx = Math.min(idx, tabs.length - 1);
        activeTabId = null;
        switchTab(tabs[newIdx].id);
    } else {
        renderTabBar();
    }
}

function renderTabBar() {
    const list = document.getElementById('tabBarList');
    if (!list) return;
    list.innerHTML = tabs.map(tab => {
        const isActive = tab.id === activeTabId;
        const titleHtml = escapeHtml(tab.title);
        return `<button class="tab-item ${isActive ? 'active' : ''}" data-tab="${tab.id}" onclick="switchTab('${tab.id}')">
            <span class="tab-item-title">${titleHtml}</span>
            ${tabs.length > 1 ? `<span class="tab-close" onclick="event.stopPropagation(); closeTab('${tab.id}')">&times;</span>` : ''}
        </button>`;
    }).join('');
}

document.addEventListener('DOMContentLoaded', () => {
    initResizers();
    initEditor();
    initButtons();
    initLogin();
    checkAuthAndLoad();
    initIcons();
    createTab('Query 1');
});

function initLogin() {
    const form = document.getElementById('loginForm');
    form.addEventListener('submit', async (e) => {
        e.preventDefault();
        const u = document.getElementById('username').value;
        const p = document.getElementById('password').value;
        const auth = btoa(`${u}:${p}`);

        // Quick verify
        try {
            const res = await fetch('/collections', { headers: { 'Authorization': `Basic ${auth}` } });
            if (res.ok) {
                localStorage.setItem('AxarDB_auth', auth);
                document.getElementById('loginModal').style.display = 'none';
                loadCollections();
            } else {
                showLoginError();
            }
        } catch (err) {
            showLoginError('Connection failed');
        }
    });
}

function showLoginError(msg = 'Invalid credentials') {
    const el = document.getElementById('loginError');
    el.textContent = msg;
    el.style.display = 'block';
}

function checkAuthAndLoad() {
    if (!localStorage.getItem('AxarDB_auth')) {
        document.getElementById('loginModal').style.display = 'flex';
    } else {
        loadCollections();
    }
}


function initEditor() {
    require.config({ paths: { vs: 'https://cdn.jsdelivr.net/npm/monaco-editor@0.43.0/min/vs' } });
    require(['vs/editor/editor.main'], function () {
        editor = monaco.editor.create(document.getElementById('editorContainer'), {
            value: "// Type your JavaScript query here\n// Use 'db.collection' to access data\n",
            language: 'javascript',
            theme: 'vs-dark',
            automaticLayout: true,
            minimap: { enabled: false },
            fontSize: 14,
            padding: { top: 16 }
        });

        editor.addAction({
            id: 'execute-query',
            label: 'Execute Query',
            keybindings: [
                monaco.KeyMod.CtrlCmd | monaco.KeyCode.Enter
            ],
            run: () => {
                executeSelectedQuery();
            }
        });

        // Live sync: update localStorage on every keystroke (debounced)
        editor.onDidChangeModelContent(() => {
            if (_suppressHistorySync || !activeHistoryId) return;
            clearTimeout(_historyDebounceTimer);
            _historyDebounceTimer = setTimeout(() => {
                updateHistoryEntry(activeHistoryId, editor.getValue());
            }, 300);
        });
    });
}

function initResizers() {
    const sidebar = document.getElementById('sidebar');
    const resizerV = document.getElementById('resizerV');
    const querySection = document.getElementById('querySection');
    const resizerH = document.getElementById('resizerH');
    const overlay = document.getElementById('resizeOverlay');

    const startResizing = (cursor) => { overlay.style.display = 'block'; overlay.style.cursor = cursor; };
    const stopResizing = () => { overlay.style.display = 'none'; resizerV.classList.remove('active'); resizerH.classList.remove('active'); };

    resizerV.addEventListener('mousedown', (e) => {
        resizerV.classList.add('active');
        startResizing('col-resize');
        const move = (e) => { if (e.clientX > 150 && e.clientX < 800) sidebar.style.width = e.clientX + 'px'; };
        const up = () => { stopResizing(); document.removeEventListener('mousemove', move); document.removeEventListener('mouseup', up); };
        document.addEventListener('mousemove', move);
        document.addEventListener('mouseup', up);
    });

    resizerH.addEventListener('mousedown', (e) => {
        resizerH.classList.add('active');
        startResizing('row-resize');
        const move = (e) => {
            const height = e.clientY - sidebar.offsetTop;
            if (height > 100 && height < window.innerHeight - 100) querySection.style.height = height + 'px';
        };
        const up = () => { stopResizing(); document.removeEventListener('mousemove', move); document.removeEventListener('mouseup', up); };
        document.addEventListener('mousemove', move);
        document.addEventListener('mouseup', up);
    });
}

async function loadCollections() {
    const tree = document.getElementById('collectionTree');
    try {
        // Fetch collections, views, and triggers in parallel
        const [colRes, viewRes, trigRes] = await Promise.all([
            fetchWithAuth('/collections'),
            fetchWithAuth('/query', { method: 'POST', body: 'db.listViews()' }),
            fetchWithAuth('/query', { method: 'POST', body: 'db.listTriggers()' })
        ]);

        if (!colRes.ok) throw new Error('Auth failed');
        const collections = await colRes.json();
        currentCollections = collections;

        let views = [];
        if (viewRes.ok) {
            views = await viewRes.json();
            if (!Array.isArray(views)) views = [];
        }

        let triggers = [];
        if (trigRes.ok) {
            triggers = await trigRes.json();
            if (!Array.isArray(triggers)) triggers = [];
        }

        // Render Tree Atomically
        tree.innerHTML = '';

        // 1. DATA HEADER
        const dataHeader = document.createElement('div');
        dataHeader.className = 'tree-header';
        dataHeader.innerHTML = '<span style="font-weight:bold; color:var(--text-secondary); font-size:0.8rem; margin: 10px 0 5px 5px; display:block">DATA</span>';
        tree.appendChild(dataHeader);

        // 2. COLLECTIONS
        collections.forEach(name => {
            const item = document.createElement('div');
            item.className = 'tree-item';

            if (name.startsWith('sys')) {
                item.style.color = 'var(--accent)';
                item.innerHTML = `<i data-lucide="shield"></i> <span>${name}</span>`;
            } else {
                item.innerHTML = `<i data-lucide="database"></i> <span>${name}</span>`;
            }

            item.onclick = () => {
                lastCollectionName = name;
                setEditorValue(`// Find, Filter, Limit and List for '${name}'
// Returns top 10 documents
db.${name}
  .findall(x => true) // No filter
  .take(10)
  .ToList()`);
                executeSelectedQuery();
            };
            item.oncontextmenu = (e) => {
                e.preventDefault();
                showContextMenu(e, [
                    { label: `Default Query`, action: () => { setEditorValue(`db.${name}.findall(x => true).take(5).ToList()`); executeSelectedQuery(); } },
                    { label: `Clear ${name}`, action: () => { setEditorValue(`db.${name}.findall().delete()`); } }
                ]);
            };
            tree.appendChild(item);
        });

        // 3. VIEWS HEADER
        const vHeader = document.createElement('div');
        vHeader.innerHTML = '<span style="font-weight:bold; color:var(--text-secondary); font-size:0.8rem; margin: 15px 0 5px 5px; display:block">VIEWS</span>';
        tree.appendChild(vHeader);

        // 4. ADD VIEW BUTTON
        const btnAddView = document.createElement('div');
        btnAddView.className = 'tree-item';
        btnAddView.style.opacity = '0.7';
        btnAddView.innerHTML = '<i data-lucide="plus-circle"></i> <span>New View</span>';
        btnAddView.onclick = () => {
            editor.setValue(`// Create/Update View with Projection, Filtering, and Parameters
// @access private
db.saveView("ActiveUsers", \`
    // Example: Find active users older than 'minAge' param
    var minAge = parameters.minAge || 18;
    var limit = parameters.limit || 50;

    // Use a Vault secret if needed (e.g. for external API enrichment)
    // var apiKey = $API_KEY; 

    return db.sysusers
        .findall(u => u.active == true && u.age >= minAge)
        .select(u => ({ 
            id: u._id, 
            fullName: u.firstName + " " + u.lastName, 
            role: u.role 
        }))
        .take(limit)
        .ToList();
\`);`);
        };
        tree.appendChild(btnAddView);

        // 5. VIEWS LIST
        views.forEach(vName => {
            const item = document.createElement('div');
            item.className = 'tree-item';
            item.innerHTML = `<i data-lucide="file-code"></i> <span>${vName}</span>`;
            item.onclick = async () => {
                // Fetch view code to detect parameters
                try {
                    const res = await fetchWithAuth('/query', { method: 'POST', body: `db.getView("${vName}")` });
                    if (res.ok) {
                        const code = await res.json();
                        const params = extractViewParams(code);
                        if (Object.keys(params).length > 0) {
                            setEditorValue(`db.view("${vName}", ${JSON.stringify(params, null, 2)})`);
                        } else {
                            setEditorValue(`db.view("${vName}")`);
                        }
                    } else {
                        setEditorValue(`db.view("${vName}")`);
                    }
                } catch {
                    setEditorValue(`db.view("${vName}")`);
                }
                executeSelectedQuery();
            };
            item.oncontextmenu = (e) => {
                e.preventDefault();
                showContextMenu(e, [
                    { label: 'Run View', action: () => { setEditorValue(`db.view("${vName}")`); executeSelectedQuery(); } },
                    {
                        label: 'Edit/View Code', action: async () => {
                            const res = await fetchWithAuth('/query', { method: 'POST', body: `db.getView("${vName}")` });
                            if (res.ok) {
                                const raw = await res.json();
                                const prettyCode = raw.replace(/\r\n/g, '\n').replace(/\r/g, '\n');
                                setEditorValue(`db.saveView("${vName}", \`\n${prettyCode}\n\`);`);
                            }
                        }
                    },
                    { label: 'Delete View', action: () => { setEditorValue(`db.deleteView("${vName}")`); executeSelectedQuery(); loadCollections(); } }
                ]);
            };
            tree.appendChild(item);
        });

        // 6. TRIGGERS HEADER
        const tHeader = document.createElement('div');
        tHeader.innerHTML = '<span style="font-weight:bold; color:var(--text-secondary); font-size:0.8rem; margin: 15px 0 5px 5px; display:block">TRIGGERS</span>';
        tree.appendChild(tHeader);

        // 7. ADD TRIGGER BUTTON
        const btnAddTrig = document.createElement('div');
        btnAddTrig.className = 'tree-item';
        btnAddTrig.style.opacity = '0.7';
        btnAddTrig.innerHTML = '<i data-lucide="zap"></i> <span>New Trigger</span>';
        btnAddTrig.onclick = () => {
            // New signature: saveTrigger(name, target, code)
            editor.setValue(`// Create Trigger - Responds to data changes
// Defines an async event listener for 'sysusers' collection
db.saveTrigger("NotifyAdminOnUserChange", "sysusers", \`
    // Event object contains: type (created/changed/deleted), collection, documentId
    console.log("Trigger [" + event.type + "] on " + event.collection + ": " + event.documentId);

    // Example logic:
    if (event.type == "created") {
        // Log to a specialized collection or call generic webhook
        // db.audit_logs.insert({ action: "user_created", targetId: event.documentId, time: new Date() });
    }
\`);`);
        };
        tree.appendChild(btnAddTrig);

        // 8. TRIGGERS LIST
        triggers.forEach(tName => {
            const item = document.createElement('div');
            item.className = 'tree-item';
            item.innerHTML = `<i data-lucide="zap" style="color:red"></i> <span>${tName}</span>`;
            item.onclick = async () => {
                // View Code
                try {
                    const res = await fetchWithAuth('/query', { method: 'POST', body: `db.getTrigger("${tName}")` });
                    if (res.ok) {
                        const code = await res.json();
                        if (code) {
                            // Format for editing: We wrap it in saveTrigger
                            // Extract target? Regex?
                            // Simple: Just let user edit body and re-save
                            // Or show full wrapper command
                            setEditorValue(`// Update Trigger\ndb.saveTrigger("${tName}", "sysusers", ${JSON.stringify(code)});\n// Note: Update "sysusers" to your target parameter if changed.`);
                        }
                    }
                } catch (e) { console.error(e); }
            };
            item.oncontextmenu = (e) => {
                e.preventDefault();
                showContextMenu(e, [
                    {
                        label: 'View Code', action: async () => {
                            const res = await fetchWithAuth('/query', { method: 'POST', body: `db.getTrigger("${tName}")` });
                            if (res.ok) setEditorValue(await res.json());
                        }
                    },
                    { label: 'Delete Trigger', action: () => { setEditorValue(`db.deleteTrigger("${tName}")`); executeSelectedQuery(); loadCollections(); } }
                ]);
            };
            tree.appendChild(item);
        });

        // 9. FILTER LOGIC
        const filterInput = document.getElementById('sidebarFilter');
        filterInput.oninput = () => {
            const val = filterInput.value.toLowerCase();
            const items = tree.querySelectorAll('.tree-item');
            const headers = tree.querySelectorAll('.tree-header');

            items.forEach(el => {
                const txt = el.textContent.toLowerCase();
                el.style.display = txt.includes(val) ? 'flex' : 'none';
            });

            // Optional: Hide headers if all children are hidden? 
            // That's complex because we flattened structure. 
            // Simple version: just hide items.
        };

        // Re-apply filter if value exists (e.g. after refresh)
        if (filterInput.value) filterInput.oninput();

        initIcons();
    } catch (err) {
        if (err.message === 'Auth failed') document.getElementById('loginModal').style.display = 'flex';
    }
}

async function executeSelectedQuery() {
    const btn = document.getElementById('btnExecute');

    if (activeQueryController) {
        activeQueryController.abort();
        activeQueryController = null;
        return;
    }

    const script = editor.getValue();
    const originalText = `<i data-lucide="play"></i> Execute (Ctrl+Enter)`;

    const match = script.match(/db\.([a-zA-Z0-9_]+)\./);
    if (match) lastCollectionName = match[1];

    btn.innerHTML = '<i data-lucide="square" style="fill: currentColor; width: 14px; height: 14px;"></i> Cancel Executing';
    btn.style.backgroundColor = '#ef4444'; // Red background for cancel
    if (window.lucide) lucide.createIcons();

    activeQueryController = new AbortController();

    try {
        const response = await fetchWithAuth('/query', {
            method: 'POST',
            body: script,
            signal: activeQueryController.signal
        });
        const text = await response.text();
        let data = null;
        try { data = text ? JSON.parse(text) : null; } catch (e) { data = text; }

        if (response.ok) {
            if (Array.isArray(data)) {
                queryResults = data;
            } else if (data === null || data === undefined) {
                queryResults = [];
            } else {
                queryResults = [data];
            }
            filters = {};
            renderGrid();
            loadCollections();

            // Update active tab title
            if (activeTabId) {
                const tab = tabs.find(t => t.id === activeTabId);
                if (tab) {
                    const viewMatch = script.match(/db\.view\(["']([^"']+)["']/);
                    if (viewMatch) {
                        tab.title = viewMatch[1];
                    } else if (match) {
                        tab.title = match[1];
                    }
                    tab.queryResults = queryResults;
                    renderTabBar();
                }
            }

            // Save to history (always new entry)
            addHistoryEntry(script);
        } else {
            alert('Error: ' + (data?.detail || data?.error || data || 'Query failed'));
        }
    } catch (err) {
        if (err.name === 'AbortError') {
            console.log('Query cancelled by user');
        } else {
            alert('Server unreachable or request failed');
            console.error(err);
        }
    } finally {
        activeQueryController = null;
        btn.innerHTML = originalText;
        btn.style.backgroundColor = '';
        if (window.lucide) lucide.createIcons();
    }
}

function renderGrid() {
    const tableHead = document.getElementById('tableHead');
    const tableBody = document.getElementById('tableBody');
    const table = document.getElementById('resultsTable');
    const noResults = document.getElementById('noResults');
    const container = document.getElementById('gridContainer');

    // Preserve focus state if user is typing in filter
    const activeElement = document.activeElement;
    let focusedFilterCol = null;
    let selectionStart = 0;
    let selectionEnd = 0;
    if (activeElement && activeElement.classList.contains('filter-input')) {
        focusedFilterCol = activeElement.getAttribute('data-filter-col');
        selectionStart = activeElement.selectionStart;
        selectionEnd = activeElement.selectionEnd;
    }

    // Clear previous view
    tableHead.innerHTML = '';
    tableBody.innerHTML = '';
    noResults.style.display = 'none';

    // Clean up any existing iframe if present
    const existingFrame = container.querySelector('iframe');
    if (existingFrame) existingFrame.remove();
    table.style.display = 'table';

    if (!queryResults || queryResults.length === 0) {
        noResults.style.display = 'block';
        return;
    }

    // Check if data should be rendered as HTML in iframe
    // Conditions for iframe rendering:
    // 1. Single string result (e.g. "<h1>test</h1>")
    // 2. Single primitive (number, boolean)
    // 3. All items are not objects (non-table data)

    const isTableData = queryResults.length > 0 &&
        queryResults.every(item => item !== null && typeof item === 'object' && !Array.isArray(item));

    if (!isTableData) {
        // Render in iframe - convert to string representation
        table.style.display = 'none';
        const frame = document.createElement('iframe');
        frame.style.width = '100%';
        frame.style.height = '100%';
        frame.style.border = 'none';
        frame.style.background = 'white';
        container.appendChild(frame);

        let content = '';
        if (queryResults.length === 1) {
            content = String(queryResults[0]);
        } else {
            // Multiple non-object items
            content = queryResults.map(item => String(item)).join('<br>');
        }

        const doc = frame.contentWindow.document;
        doc.open();
        doc.write(content);
        doc.close();
        return;
    }

    // Table data - get all unique keys from objects
    const keys = [];
    const keySet = new Set();
    for (const obj of queryResults) {
        if (obj && typeof obj === 'object') {
            for (const key of Object.keys(obj)) {
                if (!keySet.has(key)) {
                    keySet.add(key);
                    keys.push(key);
                }
            }
        }
    }

    if (keys.length === 0) {
        noResults.style.display = 'block';
        noResults.textContent = 'No displayable columns found';
        return;
    }

    let displayData = [...queryResults];
    if (sortCol) {
        displayData.sort((a, b) => {
            const valA = a[sortCol]; const valB = b[sortCol];
            if (valA < valB) return -1 * sortDir; if (valA > valB) return 1 * sortDir;
            return 0;
        });
    }

    Object.keys(filters).forEach(key => {
        const query = filters[key].toLowerCase();
        if (query) displayData = displayData.filter(row => String(row[key] || '').toLowerCase().includes(query));
    });

    currentDisplayData = displayData;

    let hRow = `<tr>
        <th style="width: 50px">#</th>
        ${keys.map((k, i) => `
            <th style="position: relative; min-width: 100px;">
                <div class="sort-header" onclick="setSort('${k}')">
                    ${k} <i data-lucide="${sortCol === k ? (sortDir === 1 ? 'chevron-up' : 'chevron-down') : 'chevrons-up-down'}" size="12"></i>
                </div>
                <input class="filter-input" data-filter-col="${k}" placeholder="Filter..." value="${escapeHtml(filters[k] || '')}" oninput="setFilter('${k}', this.value)">
                <div class="col-resizer" onmousedown="initColResize(event, this)"></div>
            </th>`).join('')}
    </tr>`;

    tableHead.innerHTML = hRow;

    tableBody.innerHTML = displayData.map((row, idx) => `
        <tr>
            <td>
                <div class="row-action-btn" onclick="handleRowAction(event, ${idx})">${idx + 1}</div>
            </td>
            ${keys.map(k => `<td>${formatValue(row[k], k, idx)}</td>`).join('')}
        </tr>
    `).join('');
    initIcons();

    // Restore focus
    if (focusedFilterCol) {
        const inputToFocus = container.querySelector(`input[data-filter-col="${focusedFilterCol}"]`);
        if (inputToFocus) {
            inputToFocus.focus();
            try {
                inputToFocus.setSelectionRange(selectionStart, selectionEnd);
            } catch (e) { } // Ignore if types don't support selection
        }
    }
}

function formatValue(v, key, rowIdx = -1) {
    if (v === null || v === undefined) return '';
    if (key === '_id' || key.toLowerCase().endsWith('id')) return `<span class="badge badge-id">${escapeHtml(String(v))}</span>`;
    if (typeof v === 'boolean') return `<span class="badge badge-bool">${v}</span>`;
    if (typeof v === 'number') return `<span class="badge badge-number">${v}</span>`;
    if (typeof v === 'object') {
        const str = JSON.stringify(v);
        const shortStr = str.length > 40 ? str.substring(0, 40) + '...' : str;
        return `<div class="badge badge-json" onclick="openNestedJsonModal(${rowIdx}, '${key}')" style="cursor:pointer; border: 1px solid var(--accent); background: rgba(99, 102, 241, 0.1); color: var(--text-primary); text-transform:none; font-family:monospace; display:inline-flex; align-items:center; gap:4px;" title="Click to view details"><i data-lucide="braces" style="width:12px;height:12px;"></i>${escapeHtml(shortStr)}</div>`;
    }
    return `<span class="badge badge-string">${escapeHtml(String(v))}</span>`;
}

// --- Nested JSON Modal Logic ---
let nestedJsonCards = [];

window.openNestedJsonModal = function (rowIdx, key) {
    if (rowIdx < 0 || rowIdx >= currentDisplayData.length) return;
    const obj = currentDisplayData[rowIdx][key];
    if (!obj || typeof obj !== 'object') return;

    nestedJsonCards = [{ title: key, obj: obj }];
    renderNestedJsonModal();
    document.getElementById('nestedJsonModal').style.display = 'flex';
};

window.closeNestedJsonModal = function () {
    document.getElementById('nestedJsonModal').style.display = 'none';
};

window.pushNestedJsonCard = function (parentIdx, key) {
    nestedJsonCards = nestedJsonCards.slice(0, parentIdx + 1);
    const parentObj = nestedJsonCards[parentIdx].obj;
    const childObj = parentObj[key];
    nestedJsonCards.push({ title: Array.isArray(parentObj) ? `[${key}]` : key, obj: childObj });
    renderNestedJsonModal();
};

function renderNestedJsonModal() {
    const container = document.getElementById('nestedJsonCardsContainer');
    container.innerHTML = nestedJsonCards.map((card, idx) => {
        let itemsHtml = '';
        if (typeof card.obj === 'object' && card.obj !== null) {
            const isArray = Array.isArray(card.obj);
            Object.keys(card.obj).forEach(k => {
                const val = card.obj[k];
                const isObj = typeof val === 'object' && val !== null;
                const valStr = isObj ? (Array.isArray(val) ? `[Array(${val.length})]` : '{Object}') : escapeHtml(String(val));
                itemsHtml += `<div class="nested-json-item" ${isObj ? `onclick="pushNestedJsonCard(${idx}, '${escapeHtml(k).replace(/'/g, "\\'")}')"` : ''} style="padding: 10px 12px; border-bottom: 1px solid var(--border); display: flex; justify-content: space-between; align-items: center; transition: background 0.2s; ${isObj ? 'cursor:pointer;' : ''}" onmouseover="this.style.background='rgba(255,255,255,0.05)'" onmouseout="this.style.background='transparent'">
                    <span style="font-weight: 500; color: var(--accent); margin-right: 12px; font-size: 0.85rem;">${escapeHtml(k)}</span>
                    <span style="color: ${isObj ? 'var(--text-secondary)' : 'var(--text-primary)'}; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; max-width: 150px; font-size: 0.85rem;" title="${!isObj ? escapeHtml(String(val)) : ''}">${valStr}</span>
                    ${isObj ? '<i data-lucide="chevron-right" style="width:14px;height:14px;color:var(--text-secondary);margin-left:8px;flex-shrink:0;"></i>' : ''}
                </div>`;
            });
        }
        return `
            <div class="nested-json-card" style="min-width: 300px; width: 300px; height: 100%; border-right: 1px solid var(--border); display: flex; flex-direction: column;">
                <div class="card-header" style="padding: 12px 16px; background: rgba(0,0,0,0.3); border-bottom: 1px solid var(--border); font-weight: 600; font-size:0.9rem; position: sticky; top: 0; color: var(--text-primary); display:flex; align-items:center; gap:8px;">
                    ${escapeHtml(card.title)}
                </div>
                <div class="card-body" style="flex: 1; overflow-y: auto;">
                    ${itemsHtml || '<div style="padding:1rem;color:var(--text-secondary);text-align:center;">Empty</div>'}
                </div>
            </div>
        `;
    }).join('');
    if (window.lucide) lucide.createIcons();
    // Scroll right smoothly without blocking thread
    setTimeout(() => {
        container.scrollTo({ left: container.scrollWidth, behavior: 'smooth' });
    }, 10);
}

function setSort(col) {
    if (sortCol === col) sortDir *= -1; else { sortCol = col; sortDir = 1; }
    renderGrid();
}

function setFilter(col, val) { filters[col] = val; renderGrid(); }

function showContextMenu(e, items) {
    const menu = document.getElementById('contextMenu');
    menu.style.display = 'block'; menu.style.left = e.pageX + 'px'; menu.style.top = e.pageY + 'px';
    menu.innerHTML = items.map(i => `<div class="context-menu-item">${i.label}</div>`).join('');
    const elements = menu.querySelectorAll('.context-menu-item');
    elements.forEach((el, idx) => {
        el.onclick = () => { items[idx].action(); menu.style.display = 'none'; };
    });
    const closeHandler = () => { menu.style.display = 'none'; document.removeEventListener('click', closeHandler); };
    setTimeout(() => document.addEventListener('click', closeHandler), 10);
}

function handleRowAction(e, rowIdx) {
    e.preventDefault(); e.stopPropagation();
    const row = queryResults[rowIdx];
    const id = row._id;
    const updateObj = { ...row }; delete updateObj._id;

    showContextMenu(e, [
        {
            label: 'Edit Record', action: () => {
                setEditorValue(`db.${lastCollectionName}.update(x => x._id == "${id}", ${JSON.stringify(updateObj, null, 2)});`);
            }
        },
        {
            label: 'Delete Record', action: () => {
                setEditorValue(`db.${lastCollectionName}.findall(x => x._id == "${id}").delete();`);
            }
        },
        { label: 'Export specific record (JSON)', action: () => exportData([row], 'json') }
    ]);
}

function exportData(data, format) {
    if (!data || data.length === 0) return;
    let content = '';
    if (format === 'json') {
        content = JSON.stringify(data, null, 2);
    } else if (format === 'csv') {
        const keys = [...new Set(data.flatMap(obj => Object.keys(obj || {})))];
        content = keys.join(',') + '\n' + data.map(r => keys.map(k => {
            let v = r[k] === null || r[k] === undefined ? '' : r[k];
            if (typeof v === 'object') v = JSON.stringify(v);
            v = String(v).replace(/"/g, '""');
            return `"${v}"`;
        }).join(',')).join('\n');
    }
    const blob = new Blob([content], { type: format === 'json' ? 'application/json' : 'text/csv' });
    const url = window.URL.createObjectURL(blob);
    const a = document.createElement('a'); a.href = url; a.download = `export_${new Date().getTime()}.${format} `; a.click();
}

async function fetchWithAuth(url, options = {}) {
    const auth = localStorage.getItem('AxarDB_auth');
    if (!auth) {
        document.getElementById('loginModal').style.display = 'flex';
        throw new Error('Auth failed');
    }
    const res = await fetch(url, { ...options, headers: { 'Authorization': `Basic ${auth} `, ...options.headers } });
    if (res.status === 401) {
        localStorage.removeItem('AxarDB_auth');
        document.getElementById('loginModal').style.display = 'flex';
        throw new Error('Auth failed');
    }
    return res;
}

function initColResize(e, resizer) {
    e.preventDefault();
    e.stopPropagation();
    const th = resizer.parentElement;
    const startX = e.pageX;
    const startWidth = th.offsetWidth;

    resizer.classList.add('active');
    document.body.style.cursor = 'col-resize';

    const onMove = (moveEvent) => {
        const width = startWidth + (moveEvent.pageX - startX);
        if (width > 50) {
            th.style.width = width + 'px';
            th.style.minWidth = width + 'px'; // Enforce min-width
            th.style.maxWidth = width + 'px'; // Enforce max-width for fixed layout
        }
    };

    const onUp = () => {
        document.removeEventListener('mousemove', onMove);
        document.removeEventListener('mouseup', onUp);
        resizer.classList.remove('active');
        document.body.style.cursor = 'default';
    };

    document.addEventListener('mousemove', onMove);
    document.addEventListener('mouseup', onUp);
}


function initIcons() { if (window.lucide) lucide.createIcons(); }

// Programmatic editor.setValue wrapper — resets activeHistoryId
function setEditorValue(value) {
    _suppressHistorySync = true;
    activeHistoryId = null;
    editor.setValue(value);
    _suppressHistorySync = false;
}

function initButtons() {
    document.getElementById('btnExecute').onclick = executeSelectedQuery;
    document.getElementById('btnAddDb').onclick = () => {
        const name = prompt("Enter new collection name:");
        if (name) {
            setEditorValue(`// Create Collection '${name}' by inserting a document
    // Collections are created automatically on first write
    db.${name}.insert({
        created: new Date(),
        desc: "Initial document"
    });
    // List collections to confirm
    showCollections(); `);
        }
    };

    // Export buttons
    document.getElementById('btnExportJson').onclick = () => exportData(queryResults, 'json');
    document.getElementById('btnExportCsv').onclick = () => exportData(queryResults, 'csv');

    // History
    document.getElementById('btnHistory').onclick = openHistoryModal;
    document.getElementById('btnCloseHistory').onclick = closeHistoryModal;

    // Tab management
    document.getElementById('btnAddTab').onclick = () => createTab();
}

// --- View Parameter Extraction ---
function extractViewParams(code) {
    const params = {};

    // 1. Match @param patterns (e.g. @email, @password)
    const atRegex = /@([a-zA-Z_][a-zA-Z0-9_]*)/g;
    let match;
    while ((match = atRegex.exec(code)) !== null) {
        const paramName = match[1];
        // Skip known directives like @access
        if (paramName === 'access') continue;
        if (params[paramName] === undefined) {
            params[paramName] = '';
        }
    }

    // 2. Match parameters.xxx patterns
    const paramRegex = /parameters\.([a-zA-Z_][a-zA-Z0-9_]*)/g;
    while ((match = paramRegex.exec(code)) !== null) {
        const paramName = match[1];
        if (params[paramName] !== undefined) continue;
        // Try to find default: parameters.xxx || defaultValue
        const defaultRegex = new RegExp(`parameters\\.${paramName} \\s *\\|\\|\\s * (.+?) \\s *; `);
        const defaultMatch = code.match(defaultRegex);
        if (defaultMatch) {
            const raw = defaultMatch[1].trim();
            if (!isNaN(Number(raw))) {
                params[paramName] = Number(raw);
            } else if (raw.startsWith('"') || raw.startsWith("'")) {
                params[paramName] = raw.replace(/^["']|["']$/g, '');
            } else if (raw === 'true' || raw === 'false') {
                params[paramName] = raw === 'true';
            } else {
                params[paramName] = raw;
            }
        } else {
            params[paramName] = '';
        }
    }

    return params;
}

// --- Query History (localStorage) ---

function getHistory() {
    try {
        return JSON.parse(localStorage.getItem('AxarDB_queryHistory') || '[]');
    } catch { return []; }
}

function saveHistory(entries) {
    localStorage.setItem('AxarDB_queryHistory', JSON.stringify(entries));
}

function addHistoryEntry(script) {
    const entries = getHistory();
    const id = 'q_' + Date.now();
    entries.unshift({ id, script, timestamp: Date.now() });
    saveHistory(entries);
    activeHistoryId = id;
}

function updateHistoryEntry(id, script) {
    const entries = getHistory();
    const entry = entries.find(e => e.id === id);
    if (entry) {
        entry.script = script;
        saveHistory(entries);
    }
}

function deleteHistoryEntry(id) {
    let entries = getHistory();
    entries = entries.filter(e => e.id !== id);
    saveHistory(entries);
    if (activeHistoryId === id) activeHistoryId = null;
    renderHistoryList();
}

function loadHistoryItem(id) {
    const entries = getHistory();
    const entry = entries.find(e => e.id === id);
    if (!entry) return;
    _suppressHistorySync = true;
    activeHistoryId = id;
    editor.setValue(entry.script);
    _suppressHistorySync = false;
    closeHistoryModal();
}

function openHistoryModal() {
    document.getElementById('historyModal').style.display = 'flex';
    renderHistoryList();
    initIcons();
}

function closeHistoryModal() {
    document.getElementById('historyModal').style.display = 'none';
}

function renderHistoryList() {
    const list = document.getElementById('historyList');
    const entries = getHistory();
    const filterEl = document.getElementById('historyFilter');
    const filterVal = filterEl ? filterEl.value.toLowerCase() : '';

    const filtered = filterVal
        ? entries.filter(e => e.script.toLowerCase().includes(filterVal))
        : entries;

    if (entries.length === 0) {
        list.innerHTML = '<div style="padding: 2rem; color: var(--text-secondary); text-align: center;">No saved queries</div>';
        return;
    }

    if (filtered.length === 0) {
        list.innerHTML = '<div style="padding: 2rem; color: var(--text-secondary); text-align: center;">No matching queries</div>';
        return;
    }

    list.innerHTML = filtered.map(entry => {
        const preview = entry.script
            .replace(/\/\/.*$/gm, '')
            .replace(/\s+/g, ' ')
            .trim()
            .substring(0, 70) || '(empty)';
        const date = new Date(entry.timestamp);
        const timeStr = date.toLocaleString();
        const isActive = entry.id === activeHistoryId;

        return `< div class="history-item ${isActive ? 'active' : ''}" data - id="${entry.id}" >
            <div class="history-item-content" onclick="loadHistoryItem('${entry.id}')">
                <div class="preview">${escapeHtml(preview)}</div>
                <div class="timestamp">${timeStr}</div>
            </div>
            <button class="btn-delete" onclick="event.stopPropagation(); deleteHistoryEntry('${entry.id}')" title="Delete">
                <i data-lucide="trash-2" style="width:14px;height:14px"></i>
            </button>
        </div > `;
    }).join('');

    initIcons();
}

function escapeHtml(str) {
    const div = document.createElement('div');
    div.textContent = str;
    return div.innerHTML;
}
