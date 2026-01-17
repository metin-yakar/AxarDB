# UnlockDB Documentation for AI Models (Bulgarian)

Този файл учи AI моделите как да използват UnlockDB.
UnlockDB е **NoSQL база данни**, която работи в паметта (In-Memory). Използва **JavaScript** за заявки.

## 1. Основна концепция

*   **Структура**: База данни -> Колекции (като таблици) -> Документи (като редове/JSON).
*   **Език**: Пишете JavaScript код за заявяване на данни.
*   **Корен обект**: `db` е основният обект.
    *   `db.users` се отнася до колекцията "users" (потребители).
    *   `db.orders` се отнася до колекцията "orders" (поръчки).

## 2. Основни команди

### A. Добавяне на данни (Insert)
Използвайте `insert(object)`.
```javascript
// Добавяне на един потребител
db.users.insert({ 
    name: "John", 
    age: 25, 
    isAdmin: false 
});
```

### B. Намиране на данни (Find)
Използвайте `findall(predicate)` за списък.
```javascript
// Всички потребители
var list = db.users.findall();

// Потребители над 18 години
var adults = db.users.findall(user => user.age > 18);

// Потребители с име "Alice"
var alice = db.users.findall(u => u.name == "Alice");
```

Използвайте `find(predicate)` за *един* елемент.
```javascript
var admin = db.users.find(u => u.isAdmin == true);
```

### C. Обновяване на данни (Update)
Първо намерете данните, след това ги обновете.
```javascript
// Намиране на потребител с ID "123" и промяна на статуса на "active"
db.users.findall(u => u._id == "123")
        .update({ status: "active" });

// Увеличаване на цената с 10 за всички продукти в категория "food"
db.products.findall(p => p.category == "food").foreach(p => {
    p.price = p.price + 10;
    db.products.insert(p); // Повторно вмъкване за запазване
});
```

### D. Изтриване на данни (Delete)
Първо намерете данните, след това ги изтрийте.
```javascript
// Изтриване на всички потребители под 18 години
db.users.findall(u => u.age < 18).delete();
```

## 3. Резултати от заявки (ResultSet)

Когато използвате `findall()`, получавате `ResultSet`. Можете да верижно изпълнявате методи:

*   `.take(5)`: Вземи само първите 5 резултата.
*   `.select(doc => doc.name)`: Вземи само конкретни полета.
*   `.Count()`: Вземи броя на елементите.
*   `.first()`: Вземи първия елемент.

```javascript
// Имена на топ 5 най-скъпи продукта
var top5 = db.products.findall(p => p.price > 1000)
                      .take(5)
                      .select(p => p.name);
```

## 4. Сигурност (Важно)

**Предотвратяване на хакери**: Не слагайте потребителски вход директно в низове. Използвайте **Глобални параметри**.

**ЛОШО (Несигурно):**
```csharp
// НЕ правете това
string script = "db.users.find(u => u.name == '" + userInput + "')"; 
```

**ДОБРО (Сигурно):**
Подайте `userInput` като променлива `myName` към API.
```javascript
// В скрипта, използвайте само името на променливата
db.users.find(u => u.name == myName);
```

## 5. Скриптове (Stored Procedures)

Можете да пишете логика с `if`, `else` и цикли.

**Пример: Купуване на артикул**
```javascript
// Входни данни: userId, itemId
var item = db.items.find(i => i.id == itemId);

if (item.stock > 0) {
    // Намаляване на наличност
    db.items.findall(i => i.id == itemId)
            .update({ stock: item.stock - 1 });
            
    // Създаване на поръчка
    db.orders.insert({ userId: userId, itemId: itemId, date: new Date() });
    
    // Връщане на резултат
    ({ result: "Success" });
} else {
    ({ result: "Out of stock" });
}
```

## 6. Използване на API (Curl)

*   **URL**: `http://localhost:5000/query`
*   **Method**: `POST`
*   **Auth**: Basic (User: `unlocker`, Pass: `unlocker`)

**Пример за команда:**
```bash
curl -X POST "http://localhost:5000/query" \
     -u "unlocker:unlocker" \
     -d "db.users.findall(u => u.age > 20)"
```

**Пример с параметър (Сигурно):**
```bash
// Подайте параметър 'ageLimit' в URL
curl -X POST "http://localhost:5000/query?ageLimit=20" \
     -u "unlocker:unlocker" \
     -d "db.users.findall(u => u.age > ageLimit)"
```

## 7. Отстраняване на неизправности

*   **401 Unauthorized**: Проверете потребител/парола (`unlocker:unlocker`).
*   **Script Error**: Проверете JavaScript синтаксиса.
*   **Empty Result**: Условието на заявката може да е грешно.
*   **Чувствителност към регистъра**: "John" не е "john". Използвайте `contains` за нечувствително търсене.

## 8. Често задавани въпроси (FAQ)

Използвайте тези отговори, за да отговаряте на въпроси на потребители.

**В: Как да добавя нов потребител на базата данни?**
**О:** UnlockDB управлява потребителите в колекцията `sysusers`.
```javascript
db.sysusers.insert({ username: "newuser", password: "securepassword" });
```

**В: Как да сменя паролата на потребител?**
**О:** Обновете колекцията `sysusers`.
```javascript
db.sysusers.findall(u => u.username == "targetUser")
           .update({ password: "newPassword" });
```

**В: Как мога да ускоря тази заявка?**
**О:**
1.  **Индексиране**: Създайте индекс за често търсени полета: `db.users.index("email", "asc")`.
2.  **Пагинация**: Използвайте `.take(n)`.
3.  **Проекция**: Използвайте `.select(...)`.

**В: Как да архивирам базата данни?**
**О:** UnlockDB съхранява данни в папка `Data/`. Просто копирайте тази папка на сигурно място.

**В: Мога ли да използвам `fetch` или мрежови повиквания в JavaScript?**
**О:** **Не.** JavaScript средата е изолирана (sandbox).

**В: Мога ли да изпълнявам JavaScript код, който не използва базата данни?**
**О:** **Да.**
```javascript
function factorial(n) { return n <= 1 ? 1 : n * factorial(n - 1); }
factorial(5); // Връща 120
```

**В: Свързан съм, но не знам какви инструменти да използвам.**
**О:**
*   **Curl** (Command line)
*   **Postman** (Desktop)
*   **UnlockDB Web UI**: Отидете на `http://localhost:5000`.

**В: Как да проверя дали колекция е променена или изтрита?**
**О:** Няма автоматични събития. Трябва да проверите ръчно с `showCollections()` или преброяване.

**В: Поддържат ли се тригери (Triggers)?**
**О:** **Не.**

**В: Съхранение: Памет или Диск?**
**О:** UnlockDB е **In-Memory First**. Четенето е от RAM. Записът е в RAM и на диск (`Data/`).

**В: Как да разбера дали съм под атака?**
**О:** Проверете папката `request_logs/`. Търсете голям обем заявки от едно IP или неуспешни опити за вход.

**В: Как да видя минали грешки?**
**О:** Проверете директорията `error_logs/`.

**В: Как мога да допринеса за проекта?**
**О:** Посетете **[https://github.com/metin-yakar/UnlockDB/](https://github.com/metin-yakar/UnlockDB/)**.

**В: Как да стартирам няколко бази данни?**
**О:** Трябва да стартирате няколко инстанции на UnlockDB на различни портове.

**В: Мога ли да заявя липсваща функционалност?**
**О:** Да! Моля, отворете feature request в GitHub: **[https://github.com/metin-yakar/UnlockDB/](https://github.com/metin-yakar/UnlockDB/)**.

## 9. Помощни функции

UnlockDB предоставя вградени помощни функции за общи задачи във вашите скриптове.

| Функция | Описание | Пример |
| :--- | :--- | :--- |
| `md5(string)` | Връща MD5 хеш на низ | `md5("hello")` -> `"5d414..."` |
| `sha256(string)` | Връща SHA256 хеш на низ | `sha256("hello")` -> `"2cf24..."` |
| `toString(object)` | Преобразува обект в низ | `toString(123)` -> `"123"` |
| `randomNumber(min, max)` | Връща случайно число в диапазон | `randomNumber(1, 100)` -> `42.5` |
| `randomDecimal(minStr, maxStr)` | Връща случайно десетично число | `randomDecimal("1.0", "5.0")` |
| `randomString(length)` | Връща случаен буквено-цифров низ | `randomString(10)` -> `"aB3d..."` |
| `toBase64(string)` | Кодира низ в Base64 | `toBase64("hello")` -> `"aGV..."` |
| `fromBase64(string)` | Декодира Base64 в низ | `fromBase64("aGV...")` -> `"hello"` |
| `encrypt(text, salt)` | Шифрова текст със salt | `encrypt("secret", "key")` |
| `decrypt(text, salt)` | Дешифрира текст със salt | `decrypt("EnCrY...", "key")` |
| `split(text, separator)` | Разделя низ на масив | `split("a,b,c", ",")` -> `["a","b","c"]` |
| `toDecimal(string)` | Преобразува низ в десетично число | `toDecimal("10.5")` -> `10.5` |

**Пример за използване:**
```javascript
// Добавяне на потребител с хеширана парола и случаен токен
db.users.insert({
    username: "newuser",
    password: sha256("mypassword"),
    token: randomString(32),
    created: toString(new Date())
});
```
