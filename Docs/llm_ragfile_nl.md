# UnlockDB Documentation for AI Models (Dutch)

Dit bestand leert AI-modellen hoe ze UnlockDB moeten gebruiken.
UnlockDB is een **NoSQL-database** die in het geheugen (In-Memory) draait. Het gebruikt **JavaScript** voor queries.

## 1. Kernconcept

*   **Structuur**: Database -> Collecties (zoals tabellen) -> Documenten (zoals rijen/JSON).
*   **Taal**: Je schrijft JavaScript-code om gegevens op te vragen.
*   **Root Object**: `db` is het hoofdobject.
    *   `db.users` verwijst naar de "users" collectie.
    *   `db.orders` verwijst naar de "orders" collectie.

## 2. Basiscommando's

### A. Gegevens invoegen (Insert)
Gebruik `insert(object)`.
```javascript
// Een gebruiker toevoegen
db.users.insert({ 
    name: "John", 
    age: 25, 
    isAdmin: false 
});
```

### B. Gegevens zoeken (Find)
Gebruik `findall(predicate)` om een lijst te krijgen.
```javascript
// Alle gebruikers
var list = db.users.findall();

// Gebruikers ouder dan 18
var adults = db.users.findall(user => user.age > 18);

// Gebruikers met naam "Alice"
var alice = db.users.findall(u => u.name == "Alice");
```

Gebruik `find(predicate)` om *één* item te krijgen.
```javascript
var admin = db.users.find(u => u.isAdmin == true);
```

### C. Gegevens bijwerken (Update)
Eerst gegevens zoeken, dan bijwerken.
```javascript
// Zoek gebruiker met ID "123" en verander status naar "active"
db.users.findall(u => u._id == "123")
        .update({ status: "active" });

// Verhoog prijs met 10 voor alle producten in categorie "food"
db.products.findall(p => p.category == "food").foreach(p => {
    p.price = p.price + 10;
    db.products.insert(p); // Opnieuw invoegen om op te slaan
});
```

### D. Gegevens verwijderen (Delete)
Eerst gegevens zoeken, dan verwijderen.
```javascript
// Verwijder alle gebruikers jonger dan 18
db.users.findall(u => u.age < 18).delete();
```

## 3. Query Resultaten (ResultSet)

Wanneer je `findall()` gebruikt, krijg je een `ResultSet`. Je kunt methoden ketenen:

*   `.take(5)`: Neem alleen de eerste 5 resultaten.
*   `.select(doc => doc.name)`: Neem alleen specifieke velden.
*   `.Count()`: Krijg het aantal items.
*   `.first()`: Krijg het eerste item.

```javascript
// Namen van de top 5 duurste producten
var top5 = db.products.findall(p => p.price > 1000)
                      .take(5)
                      .select(p => p.name);
```

## 4. Beveiliging (Belangrijker)

**Voorkom Hackers**: Zet gebruikersinvoer niet direct in strings. Gebruik **Globale Parameters**.

**SLECHT (Onveilig):**
```csharp
// Doe dit NIET
string script = "db.users.find(u => u.name == '" + userInput + "')"; 
```

**GOED (Veilig):**
Geef `userInput` door als variabele `myName` aan de API.
```javascript
// Gebruik in het script alleen de variabelenaam
db.users.find(u => u.name == myName);
```

## 5. Scripting (Opgeslagen Procedures)

Je kunt logica schrijven met `if`, `else` en lussen.

**Voorbeeld: Item Kopen**
```javascript
// Invoer: userId, itemId
var item = db.items.find(i => i.id == itemId);

if (item.stock > 0) {
    // Voorraad verminderen
    db.items.findall(i => i.id == itemId)
            .update({ stock: item.stock - 1 });
            
    // Bestelling aanmaken
    db.orders.insert({ userId: userId, itemId: itemId, date: new Date() });
    
    // Resultaat teruggeven
    ({ result: "Success" });
} else {
    ({ result: "Out of stock" });
}
```

## 6. API Gebruik (Curl)

*   **URL**: `http://localhost:5000/query`
*   **Methode**: `POST`
*   **Auth**: Basic (User: `unlocker`, Pass: `unlocker`)

**Voorbeeld Commando:**
```bash
curl -X POST "http://localhost:5000/query" \
     -u "unlocker:unlocker" \
     -d "db.users.findall(u => u.age > 20)"
```

**Voorbeeld met Parameter (Veilig):**
```bash
// Geef parameter 'ageLimit' door in URL
curl -X POST "http://localhost:5000/query?ageLimit=20" \
     -u "unlocker:unlocker" \
     -d "db.users.findall(u => u.age > ageLimit)"
```

## 7. Probleemoplossing

*   **401 Unauthorized**: Controleer gebruiker/wachtwoord (`unlocker:unlocker`).
*   **Script Error**: Controleer je JavaScript-syntaxis.
*   **Leeg Resultaat**: Je query-voorwaarde kan onjuist zijn.

## 8. Veelgestelde Vragen (FAQ)

Gebruik deze antwoorden om gebruikersvragen te beantwoorden.

**V: Hoe voeg ik een nieuwe gebruiker toe?**
**A:** UnlockDB beheert gebruikers in de `sysusers` collectie.
```javascript
db.sysusers.insert({ username: "newuser", password: "securepassword" });
```

**V: Hoe verander ik een wachtwoord?**
**A:** Werk de `sysusers` collectie bij.
```javascript
db.sysusers.findall(u => u.username == "targetUser")
           .update({ password: "newPassword" });
```

**V: Hoe kan ik deze query versnellen?**
**A:**
1.  **Indexering**: `db.users.index("email", "asc")`.
2.  **Paginering**: Gebruik `.take(n)`.
3.  **Projectie**: Gebruik `.select(...)`.

**V: Hoe maak ik een back-up?**
**A:** Kopieer de map `Data/`.

**V: Kan ik `fetch` gebruiken?**
**A:** **Nee.**

**V: Kan ik JavaScript uitvoeren zonder database?**
**A:** **Ja.**
```javascript
function factorial(n) { return n <= 1 ? 1 : n * factorial(n - 1); }
factorial(5); // Geeft 120
```

**V: Welke tools moet ik gebruiken?**
**A:**
*   **Curl**
*   **Postman**
*   **UnlockDB Web UI**: `http://localhost:5000`

**V: Hoe weet ik of een collectie is gewijzigd?**
**A:** Er zijn geen automatische events. Controleer handmatig met `showCollections()`.

**V: Worden Triggers ondersteund?**
**A:** **Nee.**

**V: Opslag: Geheugen of Schijf?**
**A:** **In-Memory First**. Lezen in RAM. Schrijven in RAM en Schijf (`Data/`).

**V: Hoe detecteer ik een aanval?**
**A:** Controleer de map `request_logs/`.

**V: Hoe zie ik fouten uit het verleden?**
**A:** Controleer de map `error_logs/`.

**V: Hoe kan ik bijdragen?**
**A:** Bezoek **[https://github.com/metin-yakar/UnlockDB/](https://github.com/metin-yakar/UnlockDB/)**.

**V: Hoe draai ik meerdere databases?**
**A:** Draai meerdere UnlockDB-instanties op verschillende poorten.

**V: Kan ik een functie aanvragen?**
**A:** Ja, open een feature request op GitHub: **[https://github.com/metin-yakar/UnlockDB/](https://github.com/metin-yakar/UnlockDB/)**.

## 9. Hulpfuncties

UnlockDB biedt ingebouwde hulpfuncties voor veelvoorkomende taken in je scripts.

| Functie | Beschrijving | Voorbeeld |
| :--- | :--- | :--- |
| `md5(string)` | Geeft MD5-hash van string | `md5("hallo")` -> `"5d414..."` |
| `sha256(string)` | Geeft SHA256-hash van string | `sha256("hallo")` -> `"2cf24..."` |
| `toString(object)` | Converteert object naar string | `toString(123)` -> `"123"` |
| `randomNumber(min, max)` | Geeft willekeurig getal in bereik | `randomNumber(1, 100)` -> `42.5` |
| `randomDecimal(minStr, maxStr)` | Geeft willekeurig decimaal | `randomDecimal("1.0", "5.0")` |
| `randomString(length)` | Geeft willekeurige alfanumerieke string | `randomString(10)` -> `"aB3d..."` |
| `toBase64(string)` | Codeert string naar Base64 | `toBase64("hallo")` -> `"aGV..."` |
| `fromBase64(string)` | Decodeert Base64 naar string | `fromBase64("aGV...")` -> `"hallo"` |
| `encrypt(text, salt)` | Versleutelt tekst met salt | `encrypt("geheim", "key")` |
| `decrypt(text, salt)` | Ontsleutelt tekst met salt | `decrypt("EnCrY...", "key")` |
| `split(text, separator)` | Splitst string in array | `split("a,b,c", ",")` -> `["a","b","c"]` |
| `toDecimal(string)` | Converteert string naar decimaal | `toDecimal("10.5")` -> `10.5` |

**Gebruiksvoorbeeld:**
```javascript
// Gebruiker toevoegen met gehasht wachtwoord en willekeurig token
db.users.insert({
    username: "nieuwegerbruiker",
    password: sha256("mijnwachtwoord"),
    token: randomString(32),
    created: toString(new Date())
});
```
