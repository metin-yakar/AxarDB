// UnlockDB Web Interface - Refined Logic Phase 6 (Educational Fix)
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
                localStorage.setItem('unlockdb_auth', auth);
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
    if (!localStorage.getItem('unlockdb_auth')) {
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
        const response = await fetchWithAuth('/collections');
        if (!response.ok) throw new Error('Auth failed');
        const collections = await response.json();
        currentCollections = collections;
        tree.innerHTML = '';
        collections.forEach(name => {
            const item = document.createElement('div');
            item.className = 'tree-item';
            item.innerHTML = `<i data-lucide="database"></i> <span>${name}</span>`;
            item.onclick = () => {
                lastCollectionName = name;
                editor.setValue(`// Find, Filter, Limit and List\ndb.${name}.findall(x => true).take(10).ToList()`);
                executeSelectedQuery();
            };
            item.oncontextmenu = (e) => {
                e.preventDefault();
                showContextMenu(e, [
                    {
                        label: `Default Query (${name})`, action: () => {
                            lastCollectionName = name;
                            editor.setValue(`// Educational Query Example\ndb.${name}.findall(x => true)\n  .take(5)\n  .ToList()`);
                            executeSelectedQuery();
                        }
                    },
                    {
                        label: `Projection (select)`, action: () => {
                            lastCollectionName = name;
                            editor.setValue(`// Select specific fields\ndb.${name}.findall().select(x => ({ \n  id: x._id, \n  name: x.name \n}))`);
                        }
                    },
                    { label: `Add Record to ${name}`, action: () => { lastCollectionName = name; editor.setValue(`db.${name}.insert({ \n  created: "${new Date().toISOString()}"\n});`); } },
                    { label: `Show Indexes`, action: () => { editor.setValue(`getIndexes("${name}")`); executeSelectedQuery(); } },
                    { label: `Clear ${name}`, action: () => { lastCollectionName = name; editor.setValue(`db.${name}.findall().delete()`); } }
                ]);
            };
            tree.appendChild(item);
        });
        initIcons();
    } catch (err) { 
        // If auth failed, verify 401
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
            queryResults = Array.isArray(data) ? data : (data === null || data === undefined ? [] : (typeof data === 'object' ? [data] : []));
            filters = {};
            renderGrid();
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

    table.className = 'results-table';

    if (!queryResults || queryResults.length === 0) {
        tableHead.innerHTML = '';
        tableBody.innerHTML = '';
        noResults.style.display = 'block';
        return;
    }

    noResults.style.display = 'none';
    const keys = [...new Set(queryResults.flatMap(obj => Object.keys(obj || {})))];

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
    const auth = localStorage.getItem('unlockdb_auth');
    if (!auth) {
        document.getElementById('loginModal').style.display = 'flex';
        throw new Error('Auth failed');
    }
    const res = await fetch(url, { ...options, headers: { 'Authorization': `Basic ${auth}`, ...options.headers } });
    if (res.status === 401) {
        localStorage.removeItem('unlockdb_auth');
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
        if (width > 50) th.style.width = width + 'px';
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
        if (name) editor.setValue(`UnlockDB("${name}").insert({ created: "${new Date().toISOString()}" });`);
    };

    // Export buttons
    document.getElementById('btnExportJson').onclick = () => exportData(queryResults, 'json');
    document.getElementById('btnExportCsv').onclick = () => exportData(queryResults, 'csv');
}
