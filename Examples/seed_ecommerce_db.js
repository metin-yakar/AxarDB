// 1. Clean up existing data (Optional: Comment out if you want to append)
db.users.findall().delete();
db.products.findall().delete();
db.orders.findall().delete();
db.categories.findall().delete();
db.reviews.findall().delete();
db.audit_logs.findall().delete();

// 2. Configuration for Scale
const SCALE = {
    USERS: 100,      // 10k users
    CATEGORIES: 5,    // 50 categories
    PRODUCTS: 500,   // 50k products
    ORDERS: 1000,    // 100k orders
    REVIEWS: 1000    // 100k reviews
};
// Total ~2605 records. Adjust as needed for performance testing.

// 3. Helper Functions
function getRandomInt(min, max) {
    return Math.floor(Math.random() * (max - min + 1)) + min;
}

function getRandomItem(array) {
    if (!array || array.length === 0) return null;
    return array[Math.floor(Math.random() * array.length)];
}

function getRandomDate(start, end) {
    return new Date(start.getTime() + Math.random() * (end.getTime() - start.getTime()));
}

function guid() {
    return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, function (c) {
        var r = Math.random() * 16 | 0, v = c == 'x' ? r : (r & 0x3 | 0x8);
        return v.toString(16);
    });
}

// 4. OpenAI Integration Example (Mock Setup)
// This view demonstrates how to use OpenAI to enrich product data
db.saveView("generateProductDesc", `
// @access private
var productName = @name;
var token = $OPENAI_KEY; // Assumes OPENAI_KEY is in Vault
if (!token) return "No OpenAI Token configured";

var llm = openai("https://api.openai.com/v1", token);
llm.addSysMsg("You are a creative copywriter. Write a 1-sentence product description.");
var desc = llm.msg("Write a description for: " + productName, {}, "gpt-3.5-turbo");
return desc;
`);

// 5. Queue Integration Example
// Queue a background job to 'process' a batch of orders (simulated)
function queueOrderProcessing(orderId) {
    // This script will run in the background
    var script = `
        var order = db.orders.find(o => o._id == @id);
        if (order) {
            // Simulate complex processing
            db.orders.update(o => o._id == @id, { 
                processedAt: new Date(), 
                status: 'Processed' 
            });
            console.log("Background processed order: " + @id);
        }
    `;
    // Queue with priority 1
    queue(script, { id: orderId }, { priority: 1 });
}


// 6. User Data Generation
console.log("Generating " + SCALE.USERS + " Users...");
var firstNames = ["James", "Mary", "John", "Patricia", "Robert", "Jennifer", "Michael", "Linda", "William", "Elizabeth", "David", "Barbara", "Richard", "Susan", "Joseph", "Jessica", "Thomas", "Sarah", "Charles", "Karen"];
var lastNames = ["Smith", "Johnson", "Williams", "Brown", "Jones", "Garcia", "Miller", "Davis", "Rodriguez", "Martinez", "Hernandez", "Lopez", "Gonzalez", "Wilson", "Anderson", "Thomas", "Taylor", "Moore", "Jackson", "Martin"];
var countries = ["USA", "UK", "Germany", "France", "Canada", "Australia", "Japan", "China", "Brazil", "India"];
var domains = ["gmail.com", "yahoo.com", "hotmail.com", "outlook.com", "icloud.com"];

for (var i = 0; i < SCALE.USERS; i++) {
    var firstName = getRandomItem(firstNames);
    var lastName = getRandomItem(lastNames);
    var email = firstName.toLowerCase() + "." + lastName.toLowerCase() + i + "@" + getRandomItem(domains);

    db.users.insert({
        firstName: firstName,
        lastName: lastName,
        email: email,
        password: sha256("password" + i),
        country: getRandomItem(countries),
        age: getRandomInt(18, 70),
        isActive: Math.random() > 0.1,
        createdAt: getRandomDate(new Date(2023, 0, 1), new Date()),
        isPremium: Math.random() > 0.8
    });
}
var userList = db.users.findall().toList();
if (userList.length === 0) throw new Error("User generation failed. userList is empty.");
console.log("Users loaded: " + userList.length);

// 7. Category Data Generation
console.log("Generating " + SCALE.CATEGORIES + " Categories...");
var catNames = ["Electronics", "Fashion", "Home", "Books", "Sports", "Toys", "Health", "Automotive", "Beauty", "Groceries", "Music", "Movies", "Games", "Tools", "Outdoors", "Computers", "Pet", "Kids", "Industrial", "Handmade"];
for (var i = 0; i < SCALE.CATEGORIES; i++) {
    var base = getRandomItem(catNames);
    db.categories.insert({
        name: base + " " + (i + 1),
        description: "Category for " + base,
        taxRate: getRandomInt(0, 20) / 100
    });
}
var categoryList = db.categories.findall().toList();
if (categoryList.length === 0) throw new Error("Category generation failed. categoryList is empty.");
console.log("Categories loaded: " + categoryList.length);

// 8. Product Data Generation
console.log("Generating " + SCALE.PRODUCTS + " Products...");
var productPrefixes = ["Super", "Ultra", "Mega", "Pro", "Max", "Eco", "Smart", "Compact", "Luxury", "Budget"];
var productNouns = ["Phone", "Laptop", "Shirt", "Shoes", "Table", "Chair", "Book", "Ball", "Doll", "Vitamin", "Tire", "Lipstick", "Coffee", "Watch", "Headphones", "Camera", "Monitor", "Keyboard", "Mouse", "Speaker"];

for (var i = 0; i < SCALE.PRODUCTS; i++) {
    var category = getRandomItem(categoryList);
    var name = getRandomItem(productPrefixes) + " " + getRandomItem(productNouns) + " " + i;

    db.products.insert({
        name: name,
        description: "High quality " + name + " for your needs.",
        price: Math.round((Math.random() * 990 + 10) * 100) / 100,
        categoryId: category._id,
        categoryName: category.name,
        stock: getRandomInt(0, 500),
        rating: Math.round((Math.random() * 4 + 1) * 10) / 10,
        tags: [category.name.toLowerCase().split(' ')[0], "sale", "new"],
        createdAt: getRandomDate(new Date(2023, 0, 1), new Date())
    });
}
var productList = db.products.findall().toList(); // Only take first few if too large, but we need random
if (productList.length === 0) throw new Error("Product generation failed. productList is empty.");
console.log("Products loaded: " + productList.length);

// 9. Order Data Generation
console.log("Generating " + SCALE.ORDERS + " Orders...");
var orderStatuses = ["Pending", "Processing", "Shipped", "Delivered", "Cancelled"];

for (var i = 0; i < SCALE.ORDERS; i++) {
    var user = getRandomItem(userList); // fast random access
    if (!user) { console.log("Warning: null user at order " + i); continue; }
    // Optimization: Don't fetch full product list if it's 50k keys. 
    // Just pick random index if possible, but we need _id. 
    // userList and productList are likely available in memory here.

    var itemCount = getRandomInt(1, 4);
    var items = [];
    var totalAmount = 0;

    for (var j = 0; j < itemCount; j++) {
        var product = getRandomItem(productList);
        if (!product) { console.log("Warning: null product at order " + i + " item " + j); continue; }
        var quantity = getRandomInt(1, 3);

        items.push({
            productId: product._id,
            productName: product.name, // Denormalize for read speed
            quantity: quantity,
            price: product.price
        });
        totalAmount += product.price * quantity;
    }

    var orderId = db.orders.insert({
        userId: user._id,
        userEmail: user.email,
        items: items,
        totalAmount: Math.round(totalAmount * 100) / 100,
        status: getRandomItem(orderStatuses),
        shippingAddress: {
            street: getRandomInt(100, 999) + " Main St",
            city: "City " + getRandomInt(1, 100),
            country: user.country,
            zipCode: getRandomInt(10000, 99999).toString()
        },
        paymentMethod: getRandomItem(["Credit Card", "PayPal", "Bank Transfer"]),
        createdAt: getRandomDate(new Date(2023, 0, 1), new Date())
    })._id;

    // Queue Example Usage:
    // Process 1% of orders via background queue to demonstrate functionality
    if (i % 100 === 0) {
        queueOrderProcessing(orderId);
    }
}

// 10. Reviews Generation
console.log("Generating " + SCALE.REVIEWS + " Reviews...");
var reviewTexts = ["Great!", "Bad.", "Okay.", "Love it.", "Hate it."];

for (var i = 0; i < SCALE.REVIEWS; i++) {
    var user = getRandomItem(userList);
    var product = getRandomItem(productList);

    if (!user || !product) {
        if (!user) console.log("Warning: null user at review " + i);
        if (!product) console.log("Warning: null product at review " + i);
        continue;
    }

    db.reviews.insert({
        userId: user._id,
        productId: product._id,
        rating: getRandomInt(1, 5),
        comment: getRandomItem(reviewTexts),
        createdAt: getRandomDate(new Date(2023, 0, 1), new Date())
    });
}

// 11. Views & Scripts (Updated)

// Public View: Search Products
db.saveView("searchProducts", `
// @access public
var keyword = @keyword;
if (!keyword) return [];
// Case-insensitive search
return db.products.findall(p => p.name.contains(keyword)).toList();
`);

// 12. View Using OpenAI (Demonstration)
db.saveView("askAI", `
// @access public
var question = @question;
var token = $OPENAI_KEY;
if (!token) return { error: "No API Key" };
var llm = openai("https://api.openai.com/v1", token);
return llm.msg(question, {}, "gpt-3.5-turbo");
`);

console.log("Seeding Complete!");
return {
    success: true,
    stats: {
        users: db.users.findall().count(),
        products: db.products.findall().count(),
        orders: db.orders.findall().count(),
        reviews: db.reviews.findall().count()
    },
    message: "Database hydrated with ~" + (SCALE.USERS + SCALE.PRODUCTS + SCALE.ORDERS + SCALE.REVIEWS) + " records."
};
