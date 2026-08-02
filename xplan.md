# AxarDB Veri Kurtarma Özelliği Planı

## Hedef
Veritabanında değişiklik yapan veya veri silinmesine sebep olan tüm operasyonları (`db` update/delete, `bulk` update/delete, `trigger` değişimi, `view` değişimi) yakalayarak, bu işlemleri geri almak (undo) için gereken sorguları günlük log dosyalarına yazmak.

## Depolama
- Hedef dizin (`_basePath`) içerisinde `backup_queries` isimli bir klasör oluşturulacak.
- Loglar günlük periyotla, örneğin `2026-08-02.txt` formatında bu klasörde depolanacak.
- Sorgular, dosyaya her yeni işlemde bir alt satır (append) olarak eklenecek.

## Müdahale Edilecek Bileşenler

### 1. `db` Koleksiyonları (Collection.cs)
`AxarDB.Definitions.Collection` sınıfındaki `Insert`, `UpdateExisting` ve `Delete` metotlarında değişiklik yapılacak.
- **Insert:** Yeni eklenen verinin geri alınması için, oluşturulan `_id` kullanılarak `db.collectionName.findall(x => x._id == '...').delete()` sorgusu yazılacak.
- **Delete:** Silinmeden hemen önce silinen `oldDocument` nesnesi JSON formatına çevrilecek ve kurtarma dosyasına şu formatta yazılacak: 
  `db.collectionName.insert({ ...oldData... })`
- **Update:** Güncellenmeden önce `oldDocument` verisi alınacak ve eski halini geri döndüren şu formatta bir sorgu yazılacak:
  `db.collectionName.update(d => d._id == 'id_degeri', { ...oldData... })`

### 2. `bulk` Koleksiyonları (BulkStore.cs)
`AxarDB.Bridges.BulkStore` sınıfındaki ekleme, güncelleme ve silme metotlarına müdahale edilecek.
- Ekleme (`Insert`), silinme ve güncellenen öğelerin `_id`'lerine karşılık gelen eski verileri bulunup benzer şekilde `bulk.collectionName.findall(...).delete()`, `bulk.collectionName.insert(...)` ve `bulk.collectionName.update(...)` sorguları dosyaya eklenecek.

### 3. Görünümler / Views (DatabaseEngine.cs)
`AxarDB.Core.DatabaseEngine` içindeki `DeleteView` ve `SaveView` (üzerine yazma) metotlarında değişiklik yapılacak.
- İşlem gerçekleşmeden önce `GetViewContent(name)` ile görünümün eski kodu okunacak.
- Eğer önceden var olan bir view siliniyor veya güncelleniyorsa, kurtarma dosyasına şu formatta sorgu eklenecek:
  `db.saveView('viewName', 'oldContent')`

### 4. Tetikleyiciler / Triggers (DatabaseEngine.cs)
`AxarDB.Core.DatabaseEngine` içindeki `DeleteTrigger` ve `SaveTrigger` (üzerine yazma) metotlarında değişiklik yapılacak.
- İşlemden önce tetikleyicinin eski içeriği ve hedefi (`GetTriggerContent` kullanılarak) tespit edilecek.
- Kurtarma dosyasına şu formatta sorgu eklenecek:
  `db.saveTrigger('triggerName', 'target', 'oldContent')`

## Yardımcı Sınıf (BackupQuery)
Tüm bu modüllerden erişilebilecek ortak bir yardımcı sınıf veya `DatabaseEngine` içerisinde bir metot oluşturulacak. 
- Sorumluluğu: `_basePath/Data/backup_queries` klasörünün varlığını kontrol etmek, günlük isimlendirilmiş dosyanın sonuna thread-safe (lock kullanarak) gelen sorguyu eklemek olacak. (Tüm modüllerin aynı Data/backup_queries dizinine yazması için Path.Combine ile Data klasörü eklenecektir).
- Örnek: `BackupQuery.LogRecoveryQuery(string basePath, string query)`

## Diğer Veriler (Gözden Kaçan / Unutulan Operasyonlar)
Yukarıdakilere ek olarak, veritabanında yapısal veya sistemsel değişiklik yapan şu operasyonlar da kurtarma (backup) loglarına dahil edilecektir:

### 1. Tüm Koleksiyonun Silinmesi (Collection Drop)
- **`db.deleteCollection('name')` ve `bulk.collection.delete()`:** Bir koleksiyon tamamen silindiğinde, içerisindeki tüm kayıtlar kaybolur. Bu işlemin geri alınabilmesi için, koleksiyon silinmeden hemen önce içerdiği tüm dokümanlar bellek üzerinden okunacak ve kurtarma dosyasına tekil veya çoklu `insert` sorguları olarak eklenecektir. (Örn: `db.collectionName.insert([...tüm eski veriler...])`).

### 2. Sistem Koleksiyonlarındaki Değişiklikler (Kullanıcı, Kasa vb.)
- **`addSysUser`, `deleteSysUser`, `addVault` vb.:** Bu metodlar arka planda `sysusers` ve `sysvaults` isimli koleksiyonlarda işlem yapmaktadır. `Collection.cs` dosyasına eklenecek olan dinleyici (interceptor) kodlar, sistem koleksiyonlarındaki her türlü kullanıcı/şifre ve kasa/değer silme-güncelleme işlemlerini otomatik olarak yakalayacak ve `db.sysusers.insert(...)` / `db.sysusers.update(...)` gibi standart sorgularla geri döndürülebilir hale getirecektir.

### 3. Bellek (Memory) Koleksiyonları
- **Memory Store:** `memory` (bellek) üzerindeki veriler geçici (ephemeral) olduğu ve uygulama kapandığında silindiği için, performans kaybı yaşanmaması adına bu veriler kurtarma loglarına dahil edilmeyecektir.

## Uygulama Adımları
1. **Adım 1:** `BackupQuery` yardımcı sınıfının yazılması.
2. **Adım 2:** `Collection.cs` (db koleksiyonları) içindeki `Update`, `Delete` metotlarına kurtarma (recovery) çağrılarının eklenmesi.
3. **Adım 3:** `BulkStore.cs` içindeki `Update`, `Delete` (veya `BulkCollectionBridge`) operasyonlarına kurtarma çağrılarının eklenmesi.
4. **Adım 4:** `DatabaseEngine.cs` içindeki `DeleteCollection` (db) ve `BulkCollectionBridge.cs` içindeki `delete` (bulk) koleksiyon silme (drop) metodlarına kurtarma çağrılarının eklenmesi.
5. **Adım 5:** `DatabaseEngine.cs` içindeki View ve Trigger metotlarına kurtarma çağrılarının eklenmesi.
6. **Adım 6:** Özelliğin test edilmesi ve logların `backup_queries/{date}.txt` dizinine beklenen JS formatında düştüğünün teyidi.
7. **Adım 7:** Geliştirmeler ve testler tamamlandıktan sonra, bu yeni veri kurtarma (Data Recovery / Backup Query) özelliğinin aşağıdaki dökümantasyon dosyalarına eklenmesi:
   - `README.md`
   - `Docs/llm_ragfile_en.md`
   - `Docs/README.tr.md`
   - `wwwroot/docs.html`

## Güvenlik ve Mevcut Kodun Korunması
- Geliştirme esnasında mevcut projenin işleyişini bozacak herhangi bir kod değişikliği yapılmayacaktır. Mevcut özellikler (eski geliştirmeler) korunacak ve sadece istenilen özellikler kodlanacaktır.
- `BackupQuery` çağrıları, ana sorgunun veya uygulamanın hata verip kesintiye uğramasını önlemek adına ayrı bir **`try-catch`** bloğu içinde gerçekleştirilecek. Herhangi bir hata durumunda bu hata uygulamanın akışını bozmayacak şekilde tolere edilecek (fail-safe) **fakat sorunun izlenebilmesi için sistemin hata kayıtlarına (error log) yazılacaktır** (örn. `AxarDB.Logging.Logger.LogError`).
- Proje promptunda istenmeyen hiçbir dosyada veya alanda oynama yapılmayacaktır.

## Test Yönergeleri
Özellik tamamlandıktan sonra ekstra bir test projesi açılmadan, uygulamanın çalıştırılması ardından doğrudan `curl` komutlarıyla test edilecektir. Test senaryosu örnek komutları:

**1. db Koleksiyonu Testleri:**
```bash
curl -X POST http://localhost:5000/query -d "db.test_collection.insert({ test_data: 'ilk deger' })"
curl -X POST http://localhost:5000/query -d "db.test_collection.update(x => x.test_data == 'ilk deger', { test_data: 'guncel deger' })"
curl -X POST http://localhost:5000/query -d "db.test_collection.delete(x => x.test_data == 'guncel deger')"
```

**2. bulk Koleksiyonu Testleri:**
```bash
curl -X POST http://localhost:5000/query -d "bulk.test_bulk.insert([{ bulk_data: 'ilk deger' }])"
curl -X POST http://localhost:5000/query -d "bulk.test_bulk.findall(x => x.bulk_data == 'ilk deger').update({ bulk_data: 'guncel deger' })"
curl -X POST http://localhost:5000/query -d "bulk.test_bulk.findall(x => x.bulk_data == 'guncel deger').delete()"
```

**3. Sistem Koleksiyonları (sysusers, sysvaults) Testleri:**
```bash
curl -X POST http://localhost:5000/query -d "addSysUser('test_user', '12345')"
curl -X POST http://localhost:5000/query -d "deleteSysUser('test_user')"
curl -X POST http://localhost:5000/query -d "addVault('test_key', 'test_val')"
```

Bu komutların çalıştırılmasının ardından ilgili `_basePath/backup_queries/YYYY-MM-DD.txt` dosyasının içeriği okunarak, eski veriyi getirecek `insert` ve `update` scriptlerinin dosyaya sorunsuz yazıldığı teyit edilecektir.

Bu adımlar onaylandıktan sonra sırayla kodlanacaktır.
