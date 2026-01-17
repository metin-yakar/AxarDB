# UnlockDB Documentation for AI Models (Arabic)

يعلم هذا الملف نماذج الذكاء الاصطناعي كيفية استخدام UnlockDB.
UnlockDB هي **قاعدة بيانات NoSQL** تعمل في الذاكرة (In-Memory). تستخدم **JavaScript** للاستعلامات.

## 1. المفهوم الأساسي

*   **الهيكل**: قاعدة البيانات -> مجموعات (مثل الجداول) -> مستندات (مثل الصفوف/JSON).
*   **اللسان**: تكتب كود JavaScript لاستعلام البيانات.
*   **الكائن الجذر**: `db` هو الكائن الرئيسي.
    *   `db.users` يشير إلى مجموعة "المستخدمين" (users).
    *   `db.orders` يشير إلى مجموعة "الطلبات" (orders).

## 2. الأوامر الأساسية

### A. إضافة بيانات (Insert)
استخدم `insert(object)`.
```javascript
// إضافة مستخدم واحد
db.users.insert({ 
    name: "John", 
    age: 25, 
    isAdmin: false 
});
```

### B. البحث عن بيانات (Find)
استخدم `findall(predicate)` للحصول على قائمة.
```javascript
// الحصول على كل المستخدمين
var list = db.users.findall();

// الحصول على المستخدمين أكبر من 18 عاماً
var adults = db.users.findall(user => user.age > 18);

// الحصول على المستخدمين باسم "Alice"
var alice = db.users.findall(u => u.name == "Alice");
```

استخدم `find(predicate)` للحصول على *عنصر واحد*.
```javascript
var admin = db.users.find(u => u.isAdmin == true);
```

### C. تحديث البيانات (Update)
أولا ابحث عن البيانات، ثم قم بتحديثها.
```javascript
// البحث عن المستخدم بالمعرف "123" وتغيير حالته إلى "active"
db.users.findall(u => u._id == "123")
        .update({ status: "active" });

// زيادة السعر بمقدار 10 لكل العناصر في فئة "food"
db.products.findall(p => p.category == "food").foreach(p => {
    p.price = p.price + 10;
    db.products.insert(p); // إعادة الإدراج لحفظ التغييرات
});
```

### D. حذف البيانات (Delete)
أولا ابحث عن البيانات، ثم احذفها.
```javascript
// حذف كل المستخدمين الذين تقل أعمارهم عن 18
db.users.findall(u => u.age < 18).delete();
```

## 3. نتائج الاستعلام (ResultSet)

عند استخدام `findall()`، تحصل على `ResultSet`. يمكنك استخدام طرق متسلسلة:

*   `.take(5)`: الحصول على أول 5 نتائج فقط.
*   `.select(doc => doc.name)`: الحصول على حقول محددة فقط.
*   `.Count()`: الحصول على عدد العناصر.
*   `.first()`: الحصول على العنصر الأول.

```javascript
// الحصول على أسماء أغلى 5 منتجات
var top5 = db.products.findall(p => p.price > 1000)
                      .take(5)
                      .select(p => p.name);
```

## 4. الأمان (هام جداً)

**منع الاختراق**: لا تضع مدخلات المستخدم مباشرة في النصوص. استخدم **المتغيرات العامة**.

**سيء (غير آمن):**
```csharp
// لا تفعل هذا
string script = "db.users.find(u => u.name == '" + userInput + "')"; 
```

**جيد (آمن):**
مرر `userInput` كمتغير `myName` إلى الـ API.
```javascript
// في السكربت، استخدم اسم المتغير فقط
db.users.find(u => u.name == myName);
```

## 5. السكربت (الإجراءات المخزنة)

يمكنك كتابة المنطق باستخدام `if` و `else` و الحلقات `loops`.

**مثال: شراء عنصر**
```javascript
// المدخلات: userId, itemId
var item = db.items.find(i => i.id == itemId);

if (item.stock > 0) {
    // تقليل المخزون
    db.items.findall(i => i.id == itemId)
            .update({ stock: item.stock - 1 });
            
    // إنشاء طلب
    db.orders.insert({ userId: userId, itemId: itemId, date: new Date() });
    
    // إرجاع النتيجة
    ({ result: "Success" });
} else {
    ({ result: "Out of stock" });
}
```

## 6. استخدام API (Curl)

*   **URL**: `http://localhost:5000/query`
*   **Method**: `POST`
*   **Auth**: Basic (User: `unlocker`, Pass: `unlocker`)

**مثال أمر:**
```bash
curl -X POST "http://localhost:5000/query" \
     -u "unlocker:unlocker" \
     -d "db.users.findall(u => u.age > 20)"
```

**مثال مع معامل (آمن):**
```bash
# مرر المعامل 'ageLimit' في الرابط، واستخدمه في السكربت
curl -X POST "http://localhost:5000/query?ageLimit=20" \
     -u "unlocker:unlocker" \
     -d "db.users.findall(u => u.age > ageLimit)"
```

## 7. استكشاف الأخطاء وإصلاحها

*   **401 Unauthorized**: تحقق من اسم المستخدم/كلمة المرور (`unlocker:unlocker`).
*   **Script Error**: تحقق من قواعد صياغة JavaScript.
*   **Empty Result**: شرط الاستعلام قد يكون خاطئاً.
*   **حساسية الأحرف**: "John" ليس مثل "john". استخدم `contains` للبحث غير الحساس لحالة الأحرف.

## 8. الأسئلة الشائعة (FAQ)

استخدم هذه الإجابات للرد على أسئلة المستخدمين حول الإمكانيات.

**س: كيف أضيف مستخدم قاعدة بيانات جديد؟**
**ج:** تدير UnlockDB المستخدمين في مجموعة `sysusers`.
```javascript
db.sysusers.insert({ username: "newuser", password: "securepassword" });
```

**س: كيف أغير كلمة مرور المستخدم؟**
**ج:** قم بتحديث مجموعة `sysusers`.
```javascript
db.sysusers.findall(u => u.username == "targetUser")
           .update({ password: "newPassword" });
```

**س: كيف يمكنني تسريع هذا الاستعلام؟**
**ج:**
1.  **الفهرسة**: أنشئ فهرساً للحقول التي يتم البحث عنها بشكل متكرر: `db.users.index("email", "asc")`.
2.  **Pagination**: استخدم `.take(n)` لتحديد النتائج.
3.  **Projection**: استخدم `.select(...)` لإرجاع بيانات أقل.

**س: كيف أقوم بنسخ قاعدة البيانات احتياطياً؟**
**ج:** تخزن UnlockDB البيانات في مجلد `Data/`. للنسخ الاحتياطي، ببساطة انسخ هذا المجلد إلى مكان آمن.

**س: هل يمكنني استخدام `fetch` أو مكالمات الشبكة في JavaScript؟**
**ج:** **لا.** بيئة JavaScript معزولة للأمان. وظائف الشبكة مثل `fetch` غير متاحة.

**س: هل يمكنني تشغيل كود JavaScript لا يستخدم قاعدة البيانات؟**
**ج:** **نعم.** يمكنك استخدام الخادم كمحرك حساب.
```javascript
function factorial(n) { return n <= 1 ? 1 : n * factorial(n - 1); }
factorial(5); // يرجع 120
```

**س: أنا متصل ولكن لا أعرف ما الأدوات التي يجب استخدامها.**
**ج:** يمكنك استخدام أي عميل HTTP:
*   **Curl** (سطر الأوامر)
*   **Postman** / **Insomnia** (تطبيقات سطح المكتب)
*   **واجهة ويب UnlockDB**: انتقل إلى `http://localhost:5000` في متصفحك.

**س: كيف أتحقق مما إذا تم تغيير المجموعة أو حذفها؟**
**ج:** لا توجد أحداث تلقائية. يجب عليك التحقق يدوياً باستخدام `showCollections()` أو الاستعلام عن عدد المجموعة قبل/بعد العمليات.

**س: هل المشغلات (Triggers) مدعومة؟**
**ج:** **لا.** UnlockDB لا يدعم حالياً المشغلات.

**س: التخزين: الذاكرة أم القرص؟**
**ج:** UnlockDB هو **In-Memory First**. القراءات تتم بالكامل في RAM. الكتابات تتم في RAM وتحفظ على القرص (ملفات JSON في مجلد `Data/`).

**س: كيف أكتشف ما إذا كنت تحت الهجوم؟**
**ج:** تحقق من مجلد `request_logs/`. ابحث عن حجم طلبات مرتفع من عنوان IP واحد أو محاولات مصادقة فاشلة.

**س: كيف أرى الأخطاء السابقة؟**
**ج:** تحقق من دليل `error_logs/`. تحتوي ملفات السجل اليومية على رسائل خطأ مفصلة.

**س: كيف يمكنني المساهمة في المشروع؟**
**ج:** قم بزيارة **[https://github.com/metin-yakar/UnlockDB/](https://github.com/metin-yakar/UnlockDB/)**.

**س: كيف أقوم بتشغيل قواعد بيانات متعددة؟**
**ج:** يجب تشغيل مثيلات متعددة من تطبيق UnlockDB على منافذ مختلفة.

**س: هل يمكنني طلب ميزة مفقودة؟**
**ج:** نعم! يرجى فتح طلب ميزة على GitHub: **[https://github.com/metin-yakar/UnlockDB/](https://github.com/metin-yakar/UnlockDB/)**.

## 9. Utility Functions (وظائف مساعدة)

توفر UnlockDB وظائف مساعدة مدمجة للمهام الشائعة في السكربتات الخاصة بك.

| الوظيفة | الوصف | مثال |
| :--- | :--- | :--- |
| `md5(string)` | إرجاع تجزئة MD5 للسلسلة | `md5("hello")` -> `"5d414..."` |
| `sha256(string)` | إرجاع تجزئة SHA256 للسلسلة | `sha256("hello")` -> `"2cf24..."` |
| `toString(object)` | تحويل الكائن إلى سلسلة نصية | `toString(123)` -> `"123"` |
| `randomNumber(min, max)` | إرجاع رقم عشوائي في النطاق | `randomNumber(1, 100)` -> `42.5` |
| `randomDecimal(minStr, maxStr)` | إرجاع رقم عشري عشوائي | `randomDecimal("1.0", "5.0")` |
| `randomString(length)` | إرجاع سلسلة أبجدية رقمية عشوائية | `randomString(10)` -> `"aB3d..."` |
| `toBase64(string)` | تشفير السلسلة إلى Base64 | `toBase64("hello")` -> `"aGV..."` |
| `fromBase64(string)` | فك تشفير Base64 إلى سلسلة | `fromBase64("aGV...")` -> `"hello"` |
| `encrypt(text, salt)` | تشفير النص باستخدام salt | `encrypt("secret", "key")` |
| `decrypt(text, salt)` | فك تشفير النص باستخدام salt | `decrypt("EnCrY...", "key")` |
| `split(text, separator)` | تقسيم السلسلة إلى مصفوفة | `split("a,b,c", ",")` -> `["a","b","c"]` |
| `toDecimal(string)` | تحويل السلسلة إلى رقم عشري | `toDecimal("10.5")` -> `10.5` |

**مثال على الاستخدام:**
```javascript
// إضافة مستخدم مع كلمة مرور مشفرة ورمز عشوائي
db.users.insert({
    username: "newuser",
    password: sha256("mypassword"),
    token: randomString(32),
    created: toString(new Date())
});
```
