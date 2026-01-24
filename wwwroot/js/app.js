// AxarDB Web Interface - Refined Logic Phase 6 (Educational Fix)
let editor;
let currentCollections = [];
let queryResults = [];
let sortCol = null;
let sortDir = 1;
let filters = {};
let lastCollectionName = "sysusers";

document.addEventListener('DOMContentLoaded', () => {
    initResizers();
    initEditor();
    initButtons();
    initLogin();
    checkAuthAndLoad();
    initIcons();
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
                editor.setValue(`// Find, Filter, Limit and List for '${name}'
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
                    { label: `Default Query`, action: () => { editor.setValue(`db.${name}.findall(x => true).take(5).ToList()`); executeSelectedQuery(); } },
                    { label: `Clear ${name}`, action: () => { editor.setValue(`db.${name}.findall().delete()`); } }
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
                // Fetch code on click? Or Execute default?
                // Prompt implied "view and trigger code visualization option".
                // Left click -> Execute usage example. Right click -> View Code?
                // Or Left click -> Load Code into Editor to edit?
                // Editors usually load code. Let's load code by default or provide easy wrapper.
                // Let's do: Left click = Template to Run. Right Click = View/Edit Code (Raw).
                editor.setValue(`db.view("${vName}", { param: "value" })`);
                executeSelectedQuery();
            };
            item.oncontextmenu = (e) => {
                e.preventDefault();
                showContextMenu(e, [
                    { label: 'Run View', action: () => { editor.setValue(`db.view("${vName}")`); executeSelectedQuery(); } },
                    {
                        label: 'Edit/View Code', action: async () => {
                            const res = await fetchWithAuth('/query', { method: 'POST', body: `db.getView("${vName}")` });
                            if (res.ok) editor.setValue(`db.saveView("${vName}", ${JSON.stringify(await res.json())})`);
                        }
                    },
                    { label: 'Delete View', action: () => { editor.setValue(`db.deleteView("${vName}")`); executeSelectedQuery(); loadCollections(); } }
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
                            editor.setValue(`// Update Trigger\ndb.saveTrigger("${tName}", "sysusers", ${JSON.stringify(code)});\n// Note: Update "sysusers" to your target parameter if changed.`);
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
                            if (res.ok) editor.setValue(await res.json());
                        }
                    },
                    { label: 'Delete Trigger', action: () => { editor.setValue(`db.deleteTrigger("${tName}")`); executeSelectedQuery(); loadCollections(); } }
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
    const script = editor.getValue();
    const btn = document.getElementById('btnExecute');
    const originalText = btn.innerHTML;

    const match = script.match(/db\.([a-zA-Z0-9_]+)\./);
    if (match) lastCollectionName = match[1];

    btn.disabled = true;
    btn.innerHTML = 'Executing...';

    try {
        const response = await fetchWithAuth('/query', { method: 'POST', body: script });
        const text = await response.text();
        let data = null;
        try { data = text ? JSON.parse(text) : null; } catch (e) { data = text; }

        if (response.ok) {
            // Handle all response types properly
            if (Array.isArray(data)) {
                queryResults = data;
            } else if (data === null || data === undefined) {
                queryResults = [];
            } else {
                // Wrap string, number, boolean, or object in array
                queryResults = [data];
            }
            filters = {};
            renderGrid();
            // Auto-refresh sidebar to show new collections/views/vaults
            loadCollections();
        } else {
            alert('Error: ' + (data?.detail || data?.error || data || 'Query failed'));
        }
    } catch (err) {
        alert('Server unreachable or request failed');
        console.error(err);
    } finally {
        btn.disabled = false;
        btn.innerHTML = originalText;
    }
}

function renderGrid() {
    const tableHead = document.getElementById('tableHead');
    const tableBody = document.getElementById('tableBody');
    const table = document.getElementById('resultsTable');
    const noResults = document.getElementById('noResults');
    const container = document.getElementById('gridContainer');

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

    let hRow = `<tr>
        <th style="width: 50px">#</th>
        ${keys.map((k, i) => `
            <th style="position: relative; min-width: 100px;">
                <div class="sort-header" onclick="setSort('${k}')">
                    ${k} <i data-lucide="${sortCol === k ? (sortDir === 1 ? 'chevron-up' : 'chevron-down') : 'chevrons-up-down'}" size="12"></i>
                </div>
                <input class="filter-input" placeholder="Filter..." value="${filters[k] || ''}" oninput="setFilter('${k}', this.value)">
                <div class="col-resizer" onmousedown="initColResize(event, this)"></div>
            </th>`).join('')}
    </tr>`;

    tableHead.innerHTML = hRow;

    tableBody.innerHTML = displayData.map((row, idx) => `
        <tr>
            <td>
                <div class="row-action-btn" onclick="handleRowAction(event, ${idx})">${idx + 1}</div>
            </td>
            ${keys.map(k => `<td>${formatValue(row[k], k)}</td>`).join('')}
        </tr>
    `).join('');
    initIcons();
}

function formatValue(v, key) {
    if (v === null || v === undefined) return '';
    if (key === '_id' || key.toLowerCase().endsWith('id')) return `<span class="badge badge-id">${v}</span>`;
    if (typeof v === 'boolean') return `<span class="badge badge-bool">${v}</span>`;
    if (typeof v === 'number') return `<span class="badge badge-number">${v}</span>`;
    if (typeof v === 'object') return `<pre style="font-size:10px; margin:0">${JSON.stringify(v)}</pre>`;
    return `<span class="badge badge-string">${v}</span>`;
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
                editor.setValue(`db.${lastCollectionName}.update(x => x._id == "${id}", ${JSON.stringify(updateObj, null, 2)});`);
            }
        },
        {
            label: 'Delete Record', action: () => {
                editor.setValue(`db.${lastCollectionName}.findall(x => x._id == "${id}").delete();`);
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
    const a = document.createElement('a'); a.href = url; a.download = `export_${new Date().getTime()}.${format}`; a.click();
}

async function fetchWithAuth(url, options = {}) {
    const auth = localStorage.getItem('AxarDB_auth');
    if (!auth) {
        document.getElementById('loginModal').style.display = 'flex';
        throw new Error('Auth failed');
    }
    const res = await fetch(url, { ...options, headers: { 'Authorization': `Basic ${auth}`, ...options.headers } });
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
function initButtons() {
    document.getElementById('btnExecute').onclick = executeSelectedQuery;
    document.getElementById('btnAddDb').onclick = () => {
        const name = prompt("Enter new collection name:");
        if (name) {
            editor.setValue(`// Create Collection '${name}' by inserting a document
// Collections are created automatically on first write
db.${name}.insert({ 
    created: new Date(), 
    desc: "Initial document" 
});
// List collections to confirm
showCollections();`);
        }
    };

    // Export buttons
    document.getElementById('btnExportJson').onclick = () => exportData(queryResults, 'json');
    document.getElementById('btnExportCsv').onclick = () => exportData(queryResults, 'csv');
}
