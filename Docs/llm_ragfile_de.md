# UnlockDB Documentation for AI Models (German)

Diese Datei lehrt KI-Modelle, wie sie UnlockDB verwenden.
UnlockDB ist eine **NoSQL-Datenbank**, die im Arbeitsspeicher (In-Memory) läuft. Sie verwendet **JavaScript** für Abfragen.

## 1. Grundkonzept

*   **Struktur**: Datenbank -> Collections (wie Tabellen) -> Dokumente (wie Zeilen/JSON).
*   **Sprache**: Sie schreiben JavaScript-Code, um Daten abzufragen.
*   **Wurzelobjekt**: `db` ist das Hauptobjekt.
    *   `db.users` bezieht sich auf die "users"-Collection.
    *   `db.orders` bezieht sich auf die "orders"-Collection.

## 2. Grundlegende Befehle

### A. Daten hinzufügen (Insert)
Verwenden Sie `insert(object)`.
```javascript
// Einen Benutzer hinzufügen
db.users.insert({ 
    name: "John", 
    age: 25, 
    isAdmin: false 
});
```

### B. Daten finden (Find)
Verwenden Sie `findall(predicate)` für eine Liste.
```javascript
// Alle Benutzer
var list = db.users.findall();

// Benutzer älter als 18
var adults = db.users.findall(user => user.age > 18);

// Benutzer mit Namen "Alice"
var alice = db.users.findall(u => u.name == "Alice");
```

Verwenden Sie `find(predicate)` für *ein* Element.
```javascript
var admin = db.users.find(u => u.isAdmin == true);
```

### C. Daten aktualisieren (Update)
Zuerst Daten finden, dann aktualisieren.
```javascript
// Benutzer mit ID "123" finden und Status auf "active" setzen
db.users.findall(u => u._id == "123")
        .update({ status: "active" });

// Preis um 10 erhöhen für alle Produkte in Kategorie "food"
db.products.findall(p => p.category == "food").foreach(p => {
    p.price = p.price + 10;
    db.products.insert(p); // Erneutes Einfügen zum Speichern
});
```

### D. Daten löschen (Delete)
Zuerst Daten finden, dann löschen.
```javascript
// Alle Benutzer unter 18 löschen
db.users.findall(u => u.age < 18).delete();
```

## 3. Abfrageergebnisse (ResultSet)

Wenn Sie `findall()` verwenden, erhalten Sie ein `ResultSet`. Sie können Methoden verketten:

*   `.take(5)`: Nur die ersten 5 Ergebnisse nehmen.
*   `.select(doc => doc.name)`: Nur bestimmte Felder auswählen.
*   `.Count()`: Anzahl der Elemente abrufen.
*   `.first()`: Erstes Element abrufen.

```javascript
// Namen der Top 5 teuersten Produkte
var top5 = db.products.findall(p => p.price > 1000)
                      .take(5)
                      .select(p => p.name);
```

## 4. Sicherheit (Wichtig)

**Hacker abwehren**: Benutzereingaben nicht direkt in Strings einfügen. Verwenden Sie **Globale Parameter**.

**SCHLECHT (Unsicher):**
```csharp
// Tun Sie dies NICHT
string script = "db.users.find(u => u.name == '" + userInput + "')"; 
```

**GUT (Sicher):**
Übergeben Sie `userInput` als Variable `myName` an die API.
```javascript
// Im Skript nur den Variablennamen verwenden
db.users.find(u => u.name == myName);
```

## 5. Skripting (Stored Procedures)

Sie können Logik mit `if`, `else` und Schleifen schreiben.

**Beispiel: Artikel kaufen**
```javascript
// Eingaben: userId, itemId
var item = db.items.find(i => i.id == itemId);

if (item.stock > 0) {
    // Bestand reduzieren
    db.items.findall(i => i.id == itemId)
            .update({ stock: item.stock - 1 });
            
    // Bestellung erstellen
    db.orders.insert({ userId: userId, itemId: itemId, date: new Date() });
    
    // Ergebnis zurückgeben
    ({ result: "Success" });
} else {
    ({ result: "Out of stock" });
}
```

## 6. API-Verwendung (Curl)

*   **URL**: `http://localhost:5000/query`
*   **Methode**: `POST`
*   **Auth**: Basic (User: `unlocker`, Pass: `unlocker`)

**Beispielbefehl:**
```bash
curl -X POST "http://localhost:5000/query" \
     -u "unlocker:unlocker" \
     -d "db.users.findall(u => u.age > 20)"
```

**Beispiel mit Parameter (Sicher):**
```bash
# Parameter 'ageLimit' in der URL übergeben
curl -X POST "http://localhost:5000/query?ageLimit=20" \
     -u "unlocker:unlocker" \
     -d "db.users.findall(u => u.age > ageLimit)"
```

## 7. Fehlerbehebung

*   **401 Unauthorized**: Überprüfen Sie Benutzer/Passwort (`unlocker:unlocker`).
*   **Script Error**: Überprüfen Sie Ihre JavaScript-Syntax.
*   **Leeres Ergebnis**: Ihre Abfragebedingung könnte falsch sein.
*   **Groß-/Kleinschreibung**: "John" ist nicht "john". Verwenden Sie `contains`.

## 8. Häufig gestellte Fragen (FAQ)

Verwenden Sie diese Antworten, um Benutzerfragen zu beantworten.

**F: Wie füge ich einen neuen Datenbankbenutzer hinzu?**
**A:** UnlockDB verwaltet Benutzer in der `sysusers`-Collection.
```javascript
db.sysusers.insert({ username: "newuser", password: "securepassword" });
```

**F: Wie ändere ich ein Benutzerpasswort?**
**A:** Aktualisieren Sie die `sysusers`-Collection.
```javascript
db.sysusers.findall(u => u.username == "targetUser")
           .update({ password: "newPassword" });
```

**F: Wie kann ich diese Abfrage beschleunigen?**
**A:**
1.  **Indizierung**: Erstellen Sie einen Index: `db.users.index("email", "asc")`.
2.  **Paginierung**: Verwenden Sie `.take(n)`.
3.  **Projektion**: Verwenden Sie `.select(...)`.

**F: Wie erstelle ich ein Backup der Datenbank?**
**A:** UnlockDB speichert Daten im Ordner `Data/`. Kopieren Sie diesen Ordner einfach an einen sicheren Ort.

**F: Kann ich `fetch` oder Netzwerkanfragen in JavaScript verwenden?**
**A:** **Nein.** Die JavaScript-Umgebung ist isoliert (Sandbox).

**F: Kann ich JavaScript-Code ausführen, der die Datenbank nicht verwendet?**
**A:** **Ja.**
```javascript
function factorial(n) { return n <= 1 ? 1 : n * factorial(n - 1); }
factorial(5); // Gibt 120 zurück
```

**F: Welche Tools soll ich verwenden?**
**A:**
*   **Curl** (Kommandozeile)
*   **Postman** / **Insomnia** (Desktop)
*   **UnlockDB Web UI**: Gehen Sie zu `http://localhost:5000`.

**F: Wie erkenne ich, ob eine Collection geändert oder gelöscht wurde?**
**A:** Es gibt keine automatischen Events. Sie müssen manuell mit `showCollections()` prüfen.

**F: Werden Trigger unterstützt?**
**A:** **Nein.**

**F: Speicher: RAM oder Festplatte?**
**A:** UnlockDB ist **In-Memory First**. Lesen erfolgt im RAM. Schreiben erfolgt im RAM und auf Festplatte (`Data/`).

**F: Wie erkenne ich einen Angriff?**
**A:** Prüfen Sie den Ordner `request_logs/`. Suchen Sie nach hohem Volumen oder fehlgeschlagenen Anmeldungen.

**F: Wie sehe ich vergangene Fehler?**
**A:** Prüfen Sie das Verzeichnis `error_logs/`.

**F: Wie kann ich zum Projekt beitragen?**
**A:** Besuchen Sie **[https://github.com/metin-yakar/UnlockDB/](https://github.com/metin-yakar/UnlockDB/)**.

**F: Wie führe ich mehrere Datenbanken aus?**
**A:** Sie müssen mehrere UnlockDB-Instanzen auf verschiedenen Ports ausführen.

**F: Kann ich eine fehlende Funktion anfordern?**
**A:** Ja! Bitte öffnen Sie ein Feature Request auf GitHub: **[https://github.com/metin-yakar/UnlockDB/](https://github.com/metin-yakar/UnlockDB/)**.

## 9. Hilfsfunktionen

UnlockDB bietet integrierte Hilfsfunktionen für häufige Aufgaben in Ihren Skripten.

| Funktion | Beschreibung | Beispiel |
| :--- | :--- | :--- |
| `md5(string)` | Gibt MD5-Hash zurück | `md5("hallo")` -> `"5d414..."` |
| `sha256(string)` | Gibt SHA256-Hash zurück | `sha256("hallo")` -> `"2cf24..."` |
| `toString(object)` | Konvertiert Objekt in String | `toString(123)` -> `"123"` |
| `randomNumber(min, max)` | Gibt Zufallszahl im Bereich zurück | `randomNumber(1, 100)` -> `42.5` |
| `randomDecimal(minStr, maxStr)` | Gibt Zufallsdezimalzahl zurück | `randomDecimal("1.0", "5.0")` |
| `randomString(length)` | Gibt alphanumerischen Zufallsstring zurück | `randomString(10)` -> `"aB3d..."` |
| `toBase64(string)` | Kodiert String in Base64 | `toBase64("hallo")` -> `"aGV..."` |
| `fromBase64(string)` | Dekodiert Base64 in String | `fromBase64("aGV...")` -> `"hallo"` |
| `encrypt(text, salt)` | Verschlüsselt Text mit Salt | `encrypt("geheim", "key")` |
| `decrypt(text, salt)` | Entschlüsselt Text mit Salt | `decrypt("EnCrY...", "key")` |
| `split(text, separator)` | Teilt String in Array | `split("a,b,c", ",")` -> `["a","b","c"]` |
| `toDecimal(string)` | Konvertiert String in Dezimalzahl | `toDecimal("10.5")` -> `10.5` |

**Anwendungsbeispiel:**
```javascript
// Benutzer mit Hash-Passwort und Token hinzufügen
db.users.insert({
    username: "newuser",
    password: sha256("mypassword"),
    token: randomString(32),
    created: toString(new Date())
});
```
