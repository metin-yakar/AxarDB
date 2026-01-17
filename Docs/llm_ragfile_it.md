# UnlockDB Documentation for AI Models (Italian)

Questo file insegna ai modelli AI come utilizzare UnlockDB.
UnlockDB è un **database NoSQL** che viene eseguito in memoria (In-Memory). Utiliza **JavaScript** per le query.

## 1. Concetto Base

*   **Struttura**: Database -> Collezioni (come Tabelle) -> Documenti (come Righe/JSON).
*   **Linguaggio**: Scrivi codice JavaScript per interrogare i dati.
*   **Oggetto Root**: `db` è l'oggetto principale.
    *   `db.users` si riferisce alla collezione "users" (utenti).
    *   `db.orders` si riferisce alla collezione "orders" (ordini).

## 2. Comandi Base

### A. Inserire Dati (Insert)
Usa `insert(object)`.
```javascript
// Aggiungi un utente
db.users.insert({ 
    name: "John", 
    age: 25, 
    isAdmin: false 
});
```

### B. Trovare Dati (Find)
Usa `findall(predicate)` per ottenere una lista.
```javascript
// Ottieni tutti gli utenti
var list = db.users.findall();

// Utenti con più di 18 anni
var adults = db.users.findall(user => user.age > 18);

// Utenti di nome "Alice"
var alice = db.users.findall(u => u.name == "Alice");
```

Usa `find(predicate)` per ottenere *un* elemento.
```javascript
var admin = db.users.find(u => u.isAdmin == true);
```

### C. Aggiornare Dati (Update)
Prima trova i dati, poi aggiornali.
```javascript
// Trova utente con ID "123" e cambia stato in "active"
db.users.findall(u => u._id == "123")
        .update({ status: "active" });

// Aumenta prezzo di 10 per tutti i prodotti nella categoria "food"
db.products.findall(p => p.category == "food").foreach(p => {
    p.price = p.price + 10;
    db.products.insert(p); // Reinserisci per salvare
});
```

### D. Eliminare Dati (Delete)
Prima trova i dati, poi eliminali.
```javascript
// Elimina tutti gli utenti con meno di 18 anni
db.users.findall(u => u.age < 18).delete();
```

## 3. Risultati Query (ResultSet)

Quando usi `findall()`, ottieni un `ResultSet`. Puoi concatenare i metodi:

*   `.take(5)`: Prendi solo i primi 5 risultati.
*   `.select(doc => doc.name)`: Prendi solo campi specifici.
*   `.Count()`: Ottieni il numero di elementi.
*   `.first()`: Ottieni il primo elemento.

```javascript
// Nomi dei 5 prodotti più costosi
var top5 = db.products.findall(p => p.price > 1000)
                      .take(5)
                      .select(p => p.name);
```

## 4. Sicurezza (Importante)

**Prevenire Hacker**: Non inserire input utente direttamente nelle stringhe. Usa **Parametri Globali**.

**MALE (Insicuro):**
```csharp
// NON fare questo
string script = "db.users.find(u => u.name == '" + userInput + "')"; 
```

**BENE (Sicuro):**
Passa `userInput` come variabile `myName` all'API.
```javascript
// Nello script, usa solo il nome della variabile
db.users.find(u => u.name == myName);
```

## 5. Scripting (Stored Procedure)

Puoi scrivere logica con `if`, `else` e cicli.

**Esempio: Compra Oggetto**
```javascript
// Input: userId, itemId
var item = db.items.find(i => i.id == itemId);

if (item.stock > 0) {
    // Riduci stock
    db.items.findall(i => i.id == itemId)
            .update({ stock: item.stock - 1 });
            
    // Crea ordine
    db.orders.insert({ userId: userId, itemId: itemId, date: new Date() });
    
    // Ritorna risultato
    ({ result: "Success" });
} else {
    ({ result: "Out of stock" });
}
```

## 6. Uso API (Curl)

*   **URL**: `http://localhost:5000/query`
*   **Metodo**: `POST`
*   **Auth**: Basic (User: `unlocker`, Pass: `unlocker`)

**Esempio Comando:**
```bash
curl -X POST "http://localhost:5000/query" \
     -u "unlocker:unlocker" \
     -d "db.users.findall(u => u.age > 20)"
```

**Esempio con Parametro (Sicuro):**
```bash
# Passa parametro 'ageLimit' nell'URL
curl -X POST "http://localhost:5000/query?ageLimit=20" \
     -u "unlocker:unlocker" \
     -d "db.users.findall(u => u.age > ageLimit)"
```

## 7. Risoluzione Problemi

*   **401 Unauthorized**: Controlla utente/password (`unlocker:unlocker`).
*   **Script Error**: Controlla la sintassi JavaScript.
*   **Risultato Vuoto**: La condizione della query potrebbe essere errata.
*   **Case Sensitivity**: Usa `contains`.

## 8. Domande Frequenti (FAQ)

Usa queste risposte per rispondere alle domande degli utenti.

**D: Come aggiungo un nuovo utente del database?**
**R:** UnlockDB gestisce gli utenti nella collezione `sysusers`.
```javascript
db.sysusers.insert({ username: "newuser", password: "securepassword" });
```

**D: Come cambio la password di un utente?**
**R:** Aggiorna la collezione `sysusers`.
```javascript
db.sysusers.findall(u => u.username == "targetUser")
           .update({ password: "newPassword" });
```

**D: Come posso velocizzare questa query?**
**R:**
1.  **Indicizzazione**: `db.users.index("email", "asc")`.
2.  **Paginazione**: Usa `.take(n)`.
3.  **Proiezione**: Usa `.select(...)`.

**D: Come faccio il backup?**
**R:** Copia la cartella `Data/`.

**D: Posso usare `fetch` o chiamate di rete in JavaScript?**
**R:** **No.** L'ambiente è isolato (sandbox).

**D: Posso eseguire codice JavaScript che non usa il database?**
**R:** **Sì.**
```javascript
function factorial(n) { return n <= 1 ? 1 : n * factorial(n - 1); }
factorial(5); // Ritorna 120
```

**D: Che strumenti devo usare?**
**R:**
*   **Curl**
*   **Postman** / **Insomnia**
*   **UnlockDB Web UI**: Vai su `http://localhost:5000`.

**D: Come so se una collezione è stata modificata o eliminata?**
**R:** Non ci sono eventi automatici. Controlla manualmente con `showCollections()`.

**D: I Trigger sono supportati?**
**R:** **No.**

**D: Storage: Memoria o Disco?**
**R:** UnlockDB è **In-Memory First**. Letture su RAM. Scritture su RAM e Disco (`Data/`).

**D: Come rilevo un attacco?**
**R:** Controlla la cartella `request_logs/`.

**D: Come vedo gli errori passati?**
**R:** Controlla la directory `error_logs/`.

**D: Come posso contribuire al progetto?**
**R:** Visita **[https://github.com/metin-yakar/UnlockDB/](https://github.com/metin-yakar/UnlockDB/)**.

**D: Come eseguo più database?**
**R:** Devi eseguire più istanze di UnlockDB su porte diverse.

**D: Posso richiedere una funzionalità mancante?**
**R:** Sì! Apri una richiesta di funzionalità su GitHub: **[https://github.com/metin-yakar/UnlockDB/](https://github.com/metin-yakar/UnlockDB/)**.

## 9. Funzioni di Utilità

UnlockDB fornisce funzioni helper integrate per attività comuni nei tuoi script.

| Funzione | Descrizione | Esempio |
| :--- | :--- | :--- |
| `md5(string)` | Ritorna hash MD5 di stringa | `md5("ciao")` -> `"5d414..."` |
| `sha256(string)` | Ritorna hash SHA256 di stringa | `sha256("ciao")` -> `"2cf24..."` |
| `toString(object)` | Converte oggetto in stringa | `toString(123)` -> `"123"` |
| `randomNumber(min, max)` | Ritorna numero casuale in intervallo | `randomNumber(1, 100)` -> `42.5` |
| `randomDecimal(minStr, maxStr)` | Ritorna decimale casuale | `randomDecimal("1.0", "5.0")` |
| `randomString(length)` | Ritorna stringa alfanumerica casuale | `randomString(10)` -> `"aB3d..."` |
| `toBase64(string)` | Codifica stringa in Base64 | `toBase64("ciao")` -> `"aGV..."` |
| `fromBase64(string)` | Decodifica Base64 in stringa | `fromBase64("aGV...")` -> `"ciao"` |
| `encrypt(text, salt)` | Cifra testo usando salt | `encrypt("segreto", "key")` |
| `decrypt(text, salt)` | Decifra testo usando salt | `decrypt("EnCrY...", "key")` |
| `split(text, separator)` | Divide stringa in array | `split("a,b,c", ",")` -> `["a","b","c"]` |
| `toDecimal(string)` | Converte stringa in numero decimale | `toDecimal("10.5")` -> `10.5` |

**Esempio di Utilizzo:**
```javascript
// Aggiungi utente con password hashata e token casuale
db.users.insert({
    username: "newuser",
    password: sha256("mypassword"),
    token: randomString(32),
    created: toString(new Date())
});
```
