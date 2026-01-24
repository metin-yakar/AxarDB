# AI Modelleri için AxarDB Dokümantasyonu (Türkçe)

Bu dosya, yapay zeka modellerine AxarDB'nin nasıl kullanılacağını öğretir.
AxarDB, bellekte (In-Memory) çalışan bir **NoSQL veritabanıdır**. Sorgular için **JavaScript** kullanır.

## 1. Temel Kavram

*   **Yapı**: Veritabanı -> Koleksiyonlar (Tablolar gibi) -> Dokümanlar (Satırlar/JSON gibi).
*   **Dil**: Veri sorgulamak için JavaScript kodu yazarsınız.
*   **Kök Nesne**: `db` ana nesnedir.
    *   `db.users` "users" koleksiyonunu ifade eder.
    *   `db.orders` "orders" koleksiyonunu ifade eder.

## 2. Temel Komutlar

### A. Veri Ekleme (Insert)
`insert(object)` kullanın.
```javascript
// Bir kullanıcı ekle
db.users.insert({ 
    name: "Ahmet", 
    age: 25, 
    isAdmin: false 
});
```

### B. Veri Bulma (Find)
Liste almak için `findall(predicate)` kullanın.
```javascript
// Tüm kullanıcılar
var list = db.users.findall();

// 18 yaşından büyük kullanıcılar
var adults = db.users.findall(user => user.age > 18);

// Adı "Ayşe" olan kullanıcılar
var ayse = db.users.findall(u => u.name == "Ayşe");
```

*Tek* bir öğe almak için `find(predicate)` kullanın.
```javascript
var admin = db.users.find(u => u.isAdmin == true);
```

### C. Veri Güncelleme (Update)
Önce veriyi bulun, sonra güncelleyin.
```javascript
// ID'si "123" olan kullanıcıyı bul ve durumunu "active" yap
db.users.findall(u => u._id == "123")
        .update({ status: "active" });

// "gıda" kategorisindeki tüm ürünlerin fiyatını 10 artır
db.products.findall(p => p.category == "gıda").foreach(p => {
    p.price = p.price + 10;
    db.products.insert(p); // Kaydetmek için tekrar ekle
});
```

### D. Veri Silme (Delete)
Önce veriyi bulun, sonra silin.
```javascript
// 18 yaşından küçük tüm kullanıcıları sil
db.users.findall(u => u.age < 18).delete();
```

## 3. Sorgu Sonuçları (ResultSet)

`findall()` kullandığınızda bir `ResultSet` alırsınız. Metotları zincirleyebilirsiniz:

*   `.take(5)`: Sadece ilk 5 sonucu al.
*   `.select(doc => doc.name)`: Sadece belirli alanları al.
*   `.Count()`: Öğe sayısını al.
*   `.first()`: İlk öğeyi al.

```javascript
// En pahalı 5 ürünün adları
var top5 = db.products.findall(p => p.price > 1000)
                      .take(5)
                      .select(p => p.name);
```

## 4. Güvenlik (Önemli)

**Hackerları Önleyin**: Kullanıcı girdisini doğrudan string içine koymayın. **Global Parametreler** kullanın.

**KÖTÜ (Güvensiz):**
```csharp
// Bunu YAPMAYIN
string script = "db.users.find(u => u.name == '" + userInput + "')"; 
```

**İYİ (Güvenli):**
`userInput` değerini `name` parametresi olarak API'ye gönderin ve script içinde `@name` yer tutucusunu kullanın.
```javascript
// Script içinde @name kullanın
db.users.find(u => u.name == @name);
```

## 5. Scripting (Saklı Yordamlar)

`if`, `else` ve döngüler kullanarak mantık yazabilirsiniz.

**Örnek: Ürün Satın Al**
```javascript
// Girdiler: userId, itemId
var item = db.items.find(i => i.id == itemId);

if (item.stock > 0) {
    // Stoğu azalt
    db.items.findall(i => i.id == itemId)
            .update({ stock: item.stock - 1 });
            
    // Sipariş oluştur
    db.orders.insert({ userId: userId, itemId: itemId, date: new Date() });
    
    // Sonuç döndür
    ({ result: "Başarılı" });
} else {
    ({ result: "Stokta yok" });
}
```

## 6. API Kullanımı (Curl)

*   **URL**: `http://localhost:5000/query`
*   **Metot**: `POST`
*   **Kimlik Doğrulama**: Basic (Kullanıcı: `unlocker`, Şifre: `unlocker`)

**Örnek Komut:**
```bash
curl -X POST "http://localhost:5000/query" \
     -u "unlocker:unlocker" \
     -d "db.users.findall(u => u.age > 20)"
```

**Parametreli Örnek (Güvenli):**
```bash
// URL'de 'ageLimit' parametresini gönder: ?ageLimit=20
curl -X POST "http://localhost:5000/query?ageLimit=20" \
     -u "unlocker:unlocker" \
     -d "db.users.findall(u => u.age > @ageLimit)"
```

```

## 7. Vaults (Sabit Değişkenler)

Genel sabitleri (API Key, Connection String vb.) saklamak için güvenli bir kasa (Vault) sistemi mevcuttur.

**Vault Ekleme:**
```javascript
db.AddVault("apiUrl", "https://api.example.com");
```

**Kullanım ($key):**
Sorgu içinde `$key` formatında kullanıldığında, sorgu çalışmadan önce bu değerle değiştirilir.

```javascript
/* Kullanım */
var url = "$apiUrl";
// Sunucu bunu şuna çevirir: var url = "https://api.example.com";
```

Sunucu tarafından dış dünyaya HTTP isteği (POST) göndermek için `webhook` fonksiyonunu kullanabilirsiniz.

```javascript
// Basit Webhook
webhook("https://api.example.com/notify", { message: "İşlem tamam" });

// Header ile Webhook
webhook("https://api.example.com/secure", 
    { data: 123 }, 
    [{ "Authorization": "Bearer token" }]
);
```

```

## 8. Views (Saklı Sorgular)

Views, sunucuda saklanan JavaScript dosyalarıdır (`Views/` klasörü). 

**View Oluşturma:**
```javascript
// db.saveView(name, content)
db.saveView("getUser", `
    // @access public
    return db.users.find(u => u.name == @name);
`);
```

**Erişim Kontrolü:**
Scriptin başına `// @access public` yazarsanız şifresiz HTTP erişimine açılır. Varsayılan `private`tir.

**Çalıştırma:**
1. **İçerden:** `db.view("getUser", {name: "Ali"})`
2. **HTTP:** `GET /views/getUser?name=Ali`

**Loglama:**
Her view çalışması `view_logs/` klasörüne (IP, Süre, Hata, Console çıktıları) kaydedilir.

## 9. Triggers (Asenkron Tetikleyiciler)

Veri değişikliklerini dinleyen arka plan fonksiyonlarıdır.

**Oluşturma:**
```javascript
db.saveTrigger("logUserChange", `
    // @target sysusers
    console.log("Değişiklik: " + event.type + " on " + event.documentId);
`);
```
*   `// @target [collectionName]` satırı zorunludur. `*` ile hepsi dinlenebilir.
*   `event` global nesnesi: `{ type: "changed|created|deleted", collection: "...", documentId: "..." }`
*   Loglar `trigger_logs/yyyy-MM-dd.log` dosyasına yazılır.

## 10. HTML Çıktı

Eğer sorgunuz bir **Dizi (Array)** yerine tek bir **String (HTML)** döndürürse, Yönetim Konsolu bu sonucu Grid yerine bir Iframe içinde web sayfası olarak gösterir. Raporlama için idealdir.

```javascript
return "<h1>Rapor</h1><p>Veri...</p>";
```

## 9. Sorun Giderme

*   **401 Unauthorized**: Kullanıcı adı/şifreyi kontrol edin (`unlocker:unlocker`).
*   **Script Error**: JavaScript sözdiziminizi kontrol edin.
*   **Boş Sonuç**: Sorgu koşulunuz yanlış olabilir.

## 8. Sıkça Sorulan Sorular (SSS)

Kullanıcı sorularını yanıtlamak için bu cevapları kullanın.

**S: Yeni bir veritabanı kullanıcısı nasıl eklerim?**
**C:** AxarDB kullanıcıları `sysusers` koleksiyonunda yönetir.
```javascript
db.sysusers.insert({ username: "yenikullanici", password: "guvenlisifre" });
```

**S: Kullanıcı şifresini nasıl değiştiririm?**
**C:** `sysusers` koleksiyonunu güncelleyin.
```javascript
db.sysusers.findall(u => u.username == "hedefKullanici")
           .update({ password: "yeniSifre" });
```

**S: Bu sorguyu nasıl hızlandırabilirim?**
**C:**
1.  **İndeksleme**: Sık aranan alanlara indeks oluşturun: `db.users.index("email", "asc")`.
2.  **Sayfalama**: `.take(n)` kullanın.
3.  **Projeksiyon**: `.select(...)` kullanarak daha az veri çekin.

**S: Veritabanını nasıl yedeklerim?**
**C:** AxarDB verileri `Data/` klasöründe saklar. Yedeklemek için bu klasörü kopyalamanız yeterlidir.

**S: JavaScript'te `fetch` veya ağ çağrıları kullanabilir miyim?**
**C:** **Hayır.** JavaScript ortamı güvenlik için izole edilmiştir (sandbox).

**S: Veritabanı işlemi yapmayan JavaScript kodu çalıştırabilir miyim?**
**C:** **Evet.** Sunucuyu bir hesaplama motoru olarak kullanabilirsiniz.
```javascript
function faktoriyel(n) { return n <= 1 ? 1 : n * faktoriyel(n - 1); }
faktoriyel(5); // 120 döndürür
```

**S: Sunucuya bağlıyım ama hangi araçları kullanacağımı bilmiyorum.**
**C:** Herhangi bir HTTP istemcisini kullanabilirsiniz:
*   **Curl** (Komut satırı)
*   **Postman** / **Insomnia** (Masaüstü uygulamaları)
*   **AxarDB Web Arayüzü**: Tarayıcınızda `http://localhost:5000` adresine gidin.

**S: Bir koleksiyonun değiştirildiğini veya silindiğini nasıl anlarım?**
**C:** Otomatik bir olay (event) sistemi yoktur. `showCollections()` fonksiyonuyla veya koleksiyon sayısını sorgulayarak manuel kontrol etmelisiniz.

**S: Trigger (Tetikleyici) destekleniyor mu?**
**C:** **Hayır.** AxarDB şu anda trigger desteklememektedir.

**S: Depolama: Bellek mi Disk mi?**
**C:** AxarDB **Önce-Bellek (In-Memory First)** prensibiyle çalışır.
*   **Okumalar**: Tamamen RAM'den yapılır (`FindAll` belleği tarar).
*   **Yazmalar**: RAM'e yazılır ve anında Diske (`Data/` klasörüne JSON olarak) kaydedilir.

**S: Saldırı altında olduğumu nasıl anlarım?**
**C:** `request_logs/` klasörünü kontrol edin. Tek bir IP'den gelen aşırı istekler veya başarısız giriş denemeleri (Durum: "Failed") saldırı belirtisi olabilir.

**S: Geçmiş hatalara nasıl ulaşırım?**
**C:** `error_logs/` dizinini kontrol edin. Günlük log dosyaları (örn. `2023-10-25.txt`) detaylı hata mesajlarını içerir.

**S: Projeye nasıl katkıda bulunabilirim?**
**C:** **[https://github.com/metin-yakar/AxarDB/](https://github.com/metin-yakar/AxarDB/)** adresini ziyaret ederek PR veya Issue oluşturabilirsiniz.

**S: Birden fazla veritabanı kullanmak için ne yapmam gerekiyor?**
**C:** AxarDB tek bir port üzerinde çalışır. Birden fazla ayrı veritabanı için, farklı portlarda (örn. 5000, 5001) çalışan birden fazla AxarDB uygulaması başlatmalısınız.
Başlatırken `-p` parametresi ile portu belirtebilirsiniz:
`dotnet run -- -p 5001` veya Docker'da `-p 5001:5001` mapping yaparak.

**S: Eksik bir özellik talep edebilir miyim?**
**C:** Evet! Lütfen GitHub'da bir özellik isteği (feature request) açın: **[https://github.com/metin-yakar/AxarDB/](https://github.com/metin-yakar/AxarDB/)**.

## 9. Yardımcı Fonksiyonlar

AxarDB, scriptlerinizde yaygın görevler için yerleşik yardımcı fonksiyonlar sağlar.

| Fonksiyon | Açıklama | Örnek |
| :--- | :--- | :--- |
| `md5(string)` | String'in MD5 özetini döndürür | `md5("merhaba")` -> `"5d414..."` |
| `sha256(string)` | String'in SHA256 özetini döndürür | `sha256("merhaba")` -> `"2cf24..."` |
| `toString(object)` | Nesneyi string'e çevirir | `toString(123)` -> `"123"` |
| `randomNumber(min, max)` | Aralıktaki rastgele sayıyı döndürür | `randomNumber(1, 100)` -> `42.5` |
| `randomDecimal(minStr, maxStr)` | Rastgele ondalık sayı döndürür (string girişi) | `randomDecimal("1.0", "5.0")` |
| `randomString(length)` | Rastgele alfanümerik string üretir | `randomString(10)` -> `"aB3d..."` |
| `toBase64(string)` | String'i Base64'e kodlar | `toBase64("merhaba")` -> `"aGV..."` |
| `fromBase64(string)` | Base64'ü string'e çözer | `fromBase64("aGV...")` -> `"merhaba"` |
| `encrypt(text, salt)` | Metni salt kullanarak şifreler | `encrypt("gizli", "anahtar")` |
| `decrypt(text, salt)` | Şifreli metni salt kullanarak çözer | `decrypt("EnCrY...", "anahtar")` |
| `split(text, separator)` | String'i ayraç ile diziye böler | `split("a,b,c", ",")` -> `["a","b","c"]` |
| `toDecimal(string)` | String'i ondalık sayıya çevirir | `toDecimal("10.5")` -> `10.5` |

**Örnek Kullanım:**
```javascript
// Şifrelenmiş parola ve rastgele token ile kullanıcı ekle
db.users.insert({
    username: "yenikullanici",
    password: sha256("parolam"),
    token: randomString(32),
    created: toString(new Date())
});
```
