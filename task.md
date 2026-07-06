﻿# AxarDB — Yeni Özellikler Görev Durumları

- [x] 1.1 BulkStore.cs — JSONL okuma, önbellek, FileSystemWatcher
- [x] 1.2 BulkResultSet.cs — findall/skip/take/select/count/delete
- [x] 1.3 BulkCollectionBridge.cs — insert([]) / findall / find / delete
- [x] 1.4 BulkBridge.cs — DynamicObject, bulk.colName
- [x] 1.5 DatabaseEngine.cs — BulkStore ekle, engine'e expose et
- [x] 1.6 Program.cs — GET /bulk/list endpoint

- [x] 2.1 Program.cs — GET /memory/list endpoint
- [x] 2.2 DatabaseEngine.cs — GetMemoryCollections() metodu
- [x] 2.3 app.js — Sidebar'a MEMORY bölümü
- [x] 2.4 app.js — Sidebar'a BULK bölümü

- [x] 3.1 Metrics/MetricsCollector.cs — Thread-safe, 50MB cap
- [x] 3.2 DatabaseEngine.cs — View/Trigger/Queue hook'ları
- [x] 3.3 Program.cs — Request middleware + GET /metrics endpoint
- [x] 3.4 wwwroot/monitoring.html — DB + Memory sekmeleri + Chart.js

## Doğrulama
- [x] dotnet build — 0 hata
- [x] bulk insert + findall testi
- [x] memory sidebar görünüm testi
- [x] monitoring grafik testi
