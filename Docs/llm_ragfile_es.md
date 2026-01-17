# UnlockDB Documentation for AI Models (Spanish)

Este archivo enseña a los modelos de IA cómo usar UnlockDB.
UnlockDB es una **base de datos NoSQL** que se ejecuta en memoria (In-Memory). Utiliza **JavaScript** para las consultas.

## 1. Concepto Central

*   **Estructura**: Base de Datos -> Colecciones (como Tablas) -> Documentos (como Filas/JSON).
*   **Lenguaje**: Escribes código JavaScript para consultar datos.
*   **Objeto Raíz**: `db` es el objeto principal.
    *   `db.users` se refiere a la colección "users" (usuarios).
    *   `db.orders` se refiere a la colección "orders" (pedidos).

## 2. Comandos Básicos

### A. Insertar Datos (Insert)
Usa `insert(object)`.
```javascript
// Agregar un usuario
db.users.insert({ 
    name: "John", 
    age: 25, 
    isAdmin: false 
});
```

### B. Buscar Datos (Find)
Usa `findall(predicate)` para obtener una lista.
```javascript
// Obtener todos los usuarios
var list = db.users.findall();

// Usuarios mayores de 18
var adults = db.users.findall(user => user.age > 18);

// Usuarios llamados "Alice"
var alice = db.users.findall(u => u.name == "Alice");
```

Usa `find(predicate)` para obtener *un* elemento.
```javascript
var admin = db.users.find(u => u.isAdmin == true);
```

### C. Actualizar Datos (Update)
Primero encuentra los datos, luego actualízalos.
```javascript
// Buscar usuario con ID "123" y cambiar estado a "active"
db.users.findall(u => u._id == "123")
        .update({ status: "active" });

// Aumentar precio en 10 para todos los productos en categoría "food"
db.products.findall(p => p.category == "food").foreach(p => {
    p.price = p.price + 10;
    db.products.insert(p); // Reinsertar para guardar
});
```

### D. Eliminar Datos (Delete)
Primero encuentra los datos, luego elimínalos.
```javascript
// Eliminar todos los usuarios menores de 18
db.users.findall(u => u.age < 18).delete();
```

## 3. Resultados de Consulta (ResultSet)

Cuando usas `findall()`, obtienes un `ResultSet`. Puedes encadenar métodos:

*   `.take(5)`: Tomar solo los primeros 5 resultados.
*   `.select(doc => doc.name)`: Tomar solo campos específicos.
*   `.Count()`: Obtener la cantidad de elementos.
*   `.first()`: Obtener el primer elemento.

```javascript
// Nombres de los 5 productos más caros
var top5 = db.products.findall(p => p.price > 1000)
                      .take(5)
                      .select(p => p.name);
```

## 4. Seguridad (Importante)

**Prevenir Hackers**: No pongas la entrada del usuario directamente en las cadenas de texto (strings). Usa **Parámetros Globales**.

**MAL (Inseguro):**
```csharp
// NO hagas esto
string script = "db.users.find(u => u.name == '" + userInput + "')"; 
```

**BIEN (Seguro):**
Pasa `userInput` como una variable `myName` a la API.
```javascript
// En el script, usa solo el nombre de la variable
db.users.find(u => u.name == myName);
```

## 5. Scripting (Procedimientos Almacenados)

Puedes escribir lógica con `if`, `else` y bucles.

**Ejemplo: Comprar Artículo**
```javascript
// Entradas: userId, itemId
var item = db.items.find(i => i.id == itemId);

if (item.stock > 0) {
    // Reducir stock
    db.items.findall(i => i.id == itemId)
            .update({ stock: item.stock - 1 });
            
    // Crear pedido
    db.orders.insert({ userId: userId, itemId: itemId, date: new Date() });
    
    // Devolver resultado
    ({ result: "Success" });
} else {
    ({ result: "Out of stock" });
}
```

## 6. Uso de API (Curl)

*   **URL**: `http://localhost:5000/query`
*   **Método**: `POST`
*   **Auth**: Basic (User: `unlocker`, Pass: `unlocker`)

**Comando de Ejemplo:**
```bash
curl -X POST "http://localhost:5000/query" \
     -u "unlocker:unlocker" \
     -d "db.users.findall(u => u.age > 20)"
```

**Ejemplo con Parámetro (Seguro):**
```bash
# Pasar parámetro 'ageLimit' en URL
curl -X POST "http://localhost:5000/query?ageLimit=20" \
     -u "unlocker:unlocker" \
     -d "db.users.findall(u => u.age > ageLimit)"
```

## 7. Solución de Problemas

*   **401 Unauthorized**: Verifica usuario/contraseña (`unlocker:unlocker`).
*   **Script Error**: Verifica tu sintaxis de JavaScript.
*   **Resultado Vacío**: Tu condición de consulta podría ser incorrecta.
*   **Mayúsculas/Minúsculas**: "John" no es "john". Usa `contains`.

## 8. Preguntas Frecuentes (FAQ)

Usa estas respuestas para responder preguntas de los usuarios.

**P: ¿Cómo agrego un nuevo usuario de base de datos?**
**R:** UnlockDB administra usuarios en la colección `sysusers`.
```javascript
db.sysusers.insert({ username: "newuser", password: "securepassword" });
```

**P: ¿Cómo cambio la contraseña de un usuario?**
**R:** Actualiza la colección `sysusers`.
```javascript
db.sysusers.findall(u => u.username == "targetUser")
           .update({ password: "newPassword" });
```

**P: ¿Cómo puedo acelerar esta consulta?**
**R:**
1.  **Indexación**: Crea un índice: `db.users.index("email", "asc")`.
2.  **Paginación**: Usa `.take(n)`.
3.  **Proyección**: Usa `.select(...)`.

**P: ¿Cómo hago una copia de seguridad?**
**R:** UnlockDB almacena datos en la carpeta `Data/`. Simplemente copia esta carpeta.

**P: ¿Puedo usar `fetch` o llamadas de red en JavaScript?**
**R:** **No.** El entorno JavaScript está aislado (sandbox).

**P: ¿Puedo ejecutar código JavaScript que no use la base de datos?**
**R:** **Sí.**
```javascript
function factorial(n) { return n <= 1 ? 1 : n * factorial(n - 1); }
factorial(5); // Devuelve 120
```

**P: ¿Qué herramientas debo usar?**
**R:**
*   **Curl** (Línea de comandos)
*   **Postman** / **Insomnia** (Escritorio)
*   **UnlockDB Web UI**: Ve a `http://localhost:5000`.

**P: ¿Cómo sé si una colección fue modificada o eliminada?**
**R:** No hay eventos automáticos. Debes verificar manualmente con `showCollections()`.

**P: ¿Se admiten Triggers?**
**R:** **No.**

**P: Almacenamiento: ¿Memoria o Disco?**
**R:** UnlockDB es **In-Memory First**. Lecturas en RAM. Escrituras en RAM y Disco (`Data/`).

**P: ¿Cómo detecto si estoy bajo ataque?**
**R:** Revisa la carpeta `request_logs/`. Busca alto volumen o fallos de autenticación.

**P: ¿Cómo veo errores pasados?**
**R:** Revisa el directorio `error_logs/`.

**P: ¿Cómo puedo contribuir al proyecto?**
**R:** Visita **[https://github.com/metin-yakar/UnlockDB/](https://github.com/metin-yakar/UnlockDB/)**.

**P: ¿Cómo ejecuto múltiples bases de datos?**
**R:** Debes ejecutar múltiples instancias de UnlockDB en diferentes puertos.

**P: ¿Puedo solicitar una función faltante?**
**R:** ¡Sí! Por favor, abre una solicitud de función en GitHub: **[https://github.com/metin-yakar/UnlockDB/](https://github.com/metin-yakar/UnlockDB/)**.

## 9. Funciones de Utilidad

UnlockDB proporciona funciones auxiliares integradas para tareas comunes en tus scripts.

| Función | Descripción | Ejemplo |
| :--- | :--- | :--- |
| `md5(string)` | Devuelve hash MD5 | `md5("hola")` -> `"5d414..."` |
| `sha256(string)` | Devuelve hash SHA256 | `sha256("hola")` -> `"2cf24..."` |
| `toString(object)` | Convierte objeto a string | `toString(123)` -> `"123"` |
| `randomNumber(min, max)` | Devuelve número aleatorio en rango | `randomNumber(1, 100)` -> `42.5` |
| `randomDecimal(minStr, maxStr)` | Devuelve decimal aleatorio | `randomDecimal("1.0", "5.0")` |
| `randomString(length)` | Devuelve string alfanumérico aleatorio | `randomString(10)` -> `"aB3d..."` |
| `toBase64(string)` | Codifica string a Base64 | `toBase64("hola")` -> `"aGV..."` |
| `fromBase64(string)` | Decodifica Base64 a string | `fromBase64("aGV...")` -> `"hola"` |
| `encrypt(text, salt)` | Cifra texto usando salt | `encrypt("secreto", "key")` |
| `decrypt(text, salt)` | Descifra texto usando salt | `decrypt("EnCrY...", "key")` |
| `split(text, separator)` | Divide string en array | `split("a,b,c", ",")` -> `["a","b","c"]` |
| `toDecimal(string)` | Convierte string a número decimal | `toDecimal("10.5")` -> `10.5` |

**Ejemplo de Uso:**
```javascript
// Agregar usuario con contraseña hasheada y token aleatorio
db.users.insert({
    username: "newuser",
    password: sha256("mypassword"),
    token: randomString(32),
    created: toString(new Date())
});
```
