# UnlockDB Documentation for AI Models (Russian)

Этот файл обучает модели ИИ использованию UnlockDB.
UnlockDB — это **NoSQL база данных**, работающая в памяти (In-Memory). Использует **JavaScript** для запросов.

## 1. Основная концепция

*   **Структура**: База данных -> Коллекции (как Таблицы) -> Документы (как Строки/JSON).
*   **Язык**: Вы пишете код JavaScript для запроса данных.
*   **Корневой объект**: `db` — это основной объект.
    *   `db.users` ссылается на коллекцию "users" (пользователи).
    *   `db.orders` ссылается на коллекцию "orders" (заказы).

## 2. Основные команды

### A. Вставка данных (Insert)
Используйте `insert(object)`.
```javascript
// Добавить одного пользователя
db.users.insert({ 
    name: "John", 
    age: 25, 
    isAdmin: false 
});
```

### B. Поиск данных (Find)
Используйте `findall(predicate)` для получения списка.
```javascript
// Все пользователи
var list = db.users.findall();

// Пользователи старше 18
var adults = db.users.findall(user => user.age > 18);

// Пользователи с именем "Alice"
var alice = db.users.findall(u => u.name == "Alice");
```

Используйте `find(predicate)` для получения *одного* элемента.
```javascript
var admin = db.users.find(u => u.isAdmin == true);
```

### C. Обновление данных (Update)
Сначала найдите данные, затем обновите их.
```javascript
// Найти пользователя с ID "123" и изменить статус на "active"
db.users.findall(u => u._id == "123")
        .update({ status: "active" });

// Увеличить цену на 10 для всех продуктов в категории "food"
db.products.findall(p => p.category == "food").foreach(p => {
    p.price = p.price + 10;
    db.products.insert(p); // Повторная вставка для сохранения
});
```

### D. Удаление данных (Delete)
Сначала найдите данные, затем удалите их.
```javascript
// Удалить всех пользователей младше 18
db.users.findall(u => u.age < 18).delete();
```

## 3. Результаты запроса (ResultSet)

При использовании `findall()` вы получаете `ResultSet`. Можно использовать цепочки методов:

*   `.take(5)`: Взять только первые 5 результатов.
*   `.select(doc => doc.name)`: Взять только определенные поля.
*   `.Count()`: Получить количество элементов.
*   `.first()`: Получить первый элемент.

```javascript
// Имена топ-5 самых дорогих продуктов
var top5 = db.products.findall(p => p.price > 1000)
                      .take(5)
                      .select(p => p.name);
```

## 4. Безопасность (Важно)

**Защита от хакеров**: Не вставляйте пользовательский ввод напрямую в строки. Используйте **Глобальные параметры**.

**ПЛОХО (Небезопасно):**
```csharp
// НЕ делайте этого
string script = "db.users.find(u => u.name == '" + userInput + "')"; 
```

**ХОРОШО (Безопасно):**
Передайте `userInput` как переменную `myName` в API.
```javascript
// В скрипте используйте только имя переменной
db.users.find(u => u.name == myName);
```

## 5. Скриптинг (Хранимые процедуры)

Вы можете писать логику с использованием `if`, `else` и циклов.

**Пример: Покупка товара**
```javascript
// Входные данные: userId, itemId
var item = db.items.find(i => i.id == itemId);

if (item.stock > 0) {
    // Уменьшить запас
    db.items.findall(i => i.id == itemId)
            .update({ stock: item.stock - 1 });
            
    // Создать заказ
    db.orders.insert({ userId: userId, itemId: itemId, date: new Date() });
    
    // Вернуть результат
    ({ result: "Success" });
} else {
    ({ result: "Out of stock" });
}
```

## 6. Использование API (Curl)

*   **URL**: `http://localhost:5000/query`
*   **Метод**: `POST`
*   **Auth**: Basic (User: `unlocker`, Pass: `unlocker`)

**Пример команды:**
```bash
curl -X POST "http://localhost:5000/query" \
     -u "unlocker:unlocker" \
     -d "db.users.findall(u => u.age > 20)"
```

**Пример с параметром (Безопасно):**
```bash
// Передача параметра 'ageLimit' в URL
curl -X POST "http://localhost:5000/query?ageLimit=20" \
     -u "unlocker:unlocker" \
     -d "db.users.findall(u => u.age > ageLimit)"
```

## 7. Устранение неполадок

*   **401 Unauthorized**: Проверьте пользователя/пароль (`unlocker:unlocker`).
*   **Script Error**: Проверьте синтаксис JavaScript.
*   **Empty Result**: Условие запроса может быть неверным.

## 8. Часто задаваемые вопросы (FAQ)

Используйте эти ответы для ответов на вопросы пользователей.

**В: Как добавить нового пользователя базы данных?**
**О:** UnlockDB управляет пользователями в коллекции `sysusers`.
```javascript
db.sysusers.insert({ username: "newuser", password: "securepassword" });
```

**В: Как изменить пароль пользователя?**
**О:** Обновите коллекцию `sysusers`.
```javascript
db.sysusers.findall(u => u.username == "targetUser")
           .update({ password: "newPassword" });
```

**В: Как ускорить этот запрос?**
**О:**
1.  **Индексация**: `db.users.index("email", "asc")`.
2.  **Пагинация**: Используйте `.take(n)`.
3.  **Проекция**: Используйте `.select(...)`.

**В: Как сделать резервную копию?**
**О:** Скопируйте папку `Data/`.

**В: Могу ли я использовать `fetch`?**
**О:** **Нет.**

**В: Могу ли я выполнять JavaScript без базы данных?**
**О:** **Да.**
```javascript
function factorial(n) { return n <= 1 ? 1 : n * factorial(n - 1); }
factorial(5); // Возвращает 120
```

**В: Какие инструменты использовать?**
**О:**
*   **Curl**
*   **Postman**
*   **UnlockDB Web UI**: `http://localhost:5000`

**В: Как узнать, была ли коллекция изменена?**
**О:** Нет автоматических событий. Проверяйте вручную с `showCollections()`.

**В: Поддерживаются ли триггеры?**
**О:** **Нет.**

**В: Хранение: Память или Диск?**
**О:** **In-Memory First**. Чтение из ОЗУ. Запись в ОЗУ и на диск (`Data/`).

**В: Как обнаружить атаку?**
**О:** Проверьте папку `request_logs/`.

**В: Как увидеть прошлые ошибки?**
**О:** Проверьте директорию `error_logs/`.

**В: Как внести свой вклад?**
**О:** Посетите **[https://github.com/metin-yakar/UnlockDB/](https://github.com/metin-yakar/UnlockDB/)**.

**В: Как запустить несколько баз данных?**
**О:** Запустите несколько экземпляров UnlockDB на разных портах.

**В: Могу ли я запросить функцию?**
**О:** Да, откройте запрос функции на GitHub: **[https://github.com/metin-yakar/UnlockDB/](https://github.com/metin-yakar/UnlockDB/)**.

## 9. Служебные функции

UnlockDB предоставляет встроенные вспомогательные функции для общих задач в ваших скриптах.

| Функция | Описание | Пример |
| :--- | :--- | :--- |
| `md5(string)` | Возвращает MD5-хэш строки | `md5("hello")` -> `"5d414..."` |
| `sha256(string)` | Возвращает SHA256-хэш строки | `sha256("hello")` -> `"2cf24..."` |
| `toString(object)` | Преобразует объект в строку | `toString(123)` -> `"123"` |
| `randomNumber(min, max)` | Возвращает случайное число в диапазоне | `randomNumber(1, 100)` -> `42.5` |
| `randomDecimal(minStr, maxStr)` | Возвращает случайное десятичное число | `randomDecimal("1.0", "5.0")` |
| `randomString(length)` | Возвращает случайную буквенно-цифровую строку | `randomString(10)` -> `"aB3d..."` |
| `toBase64(string)` | Кодирует строку в Base64 | `toBase64("hello")` -> `"aGV..."` |
| `fromBase64(string)` | Декодирует Base64 в строку | `fromBase64("aGV...")` -> `"hello"` |
| `encrypt(text, salt)` | Шифрует текст с использованием salt | `encrypt("secret", "key")` |
| `decrypt(text, salt)` | Расшифровывает текст с использованием salt | `decrypt("EnCrY...", "key")` |
| `split(text, separator)` | Разделяет строку на массив | `split("a,b,c", ",")` -> `["a","b","c"]` |
| `toDecimal(string)` | Преобразует строку в десятичное число | `toDecimal("10.5")` -> `10.5` |

**Пример использования:**
```javascript
// Добавить пользователя с хэшированным паролем и случайным токеном
db.users.insert({
    username: "newuser",
    password: sha256("mypassword"),
    token: randomString(32),
    created: toString(new Date())
});
```
