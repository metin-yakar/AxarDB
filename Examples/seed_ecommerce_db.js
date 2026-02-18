// 1. Clean up existing data
db.users.findall().delete(); // Delete all users
db.products.findall().delete(); // Delete all products
db.orders.findall().delete(); // Delete all orders
db.categories.findall().delete(); // Delete all categories
db.reviews.findall().delete(); // Delete all reviews

// 2. Helper Functions
function getRandomInt(min, max) {
    return Math.floor(Math.random() * (max - min + 1)) + min;
}

function getRandomItem(array) {
    return array[Math.floor(Math.random() * array.length)];
}

function getRandomDate(start, end) {
    return new Date(start.getTime() + Math.random() * (end.getTime() - start.getTime()));
}

// 3. User Data Generation
var firstNames = ["James", "Mary", "John", "Patricia", "Robert", "Jennifer", "Michael", "Linda", "William", "Elizabeth", "David", "Barbara", "Richard", "Susan", "Joseph", "Jessica", "Thomas", "Sarah", "Charles", "Karen"];
var lastNames = ["Smith", "Johnson", "Williams", "Brown", "Jones", "Garcia", "Miller", "Davis", "Rodriguez", "Martinez", "Hernandez", "Lopez", "Gonzalez", "Wilson", "Anderson", "Thomas", "Taylor", "Moore", "Jackson", "Martin"];
var countries = ["USA", "UK", "Germany", "France", "Canada", "Australia", "Japan", "China", "Brazil", "India"];
var domains = ["gmail.com", "yahoo.com", "hotmail.com", "outlook.com", "icloud.com"];

for (var i = 0; i < 50; i++) {
    var firstName = getRandomItem(firstNames);
    var lastName = getRandomItem(lastNames);
    var email = firstName.toLowerCase() + "." + lastName.toLowerCase() + getRandomInt(1, 999) + "@" + getRandomItem(domains);

    db.users.insert({
        firstName: firstName,
        lastName: lastName,
        email: email,
        password: sha256("password123"),
        country: getRandomItem(countries),
        age: getRandomInt(18, 70),
        isActive: Math.random() > 0.1, // 90% active
        createdAt: getRandomDate(new Date(2023, 0, 1), new Date()),
        isPremium: Math.random() > 0.8 // 20% premium
    });
}

// 4. Category Data Generation
var categories = [
    { name: "Electronics", description: "Gadgets and devices", taxRate: 0.18 },
    { name: "Fashion", description: "Clothing and accessories", taxRate: 0.08 },
    { name: "Home & Garden", description: "Furniture and decor", taxRate: 0.10 },
    { name: "Books", description: "Fiction and non-fiction", taxRate: 0.05 },
    { name: "Sports", description: "Equipment and apparel", taxRate: 0.12 },
    { name: "Toys", description: "Games and playthings", taxRate: 0.15 },
    { name: "Health", description: "Vitamins and supplements", taxRate: 0.00 },
    { name: "Automotive", description: "Car parts and accessories", taxRate: 0.20 },
    { name: "Beauty", description: "Makeup and skincare", taxRate: 0.12 },
    { name: "Groceries", description: "Food and beverages", taxRate: 0.01 }
];

for (var i = 0; i < categories.length; i++) {
    db.categories.insert(categories[i]);
}

var categoryList = db.categories.findall().toList();

// 5. Product Data Generation
var productPrefixes = ["Super", "Ultra", "Mega", "Pro", "Max", "Eco", "Smart", "Compact", "Luxury", "Budget"];
var productNouns = ["Phone", "Laptop", "Shirt", "Shoes", "Table", "Chair", "Book", "Ball", "Doll", "Vitamin", "Tire", "Lipstick", "Coffee", "Watch", "Headphones", "Camera", "Monitor", "Keyboard", "Mouse", "Speaker"];

for (var i = 0; i < 100; i++) {
    var category = getRandomItem(categoryList);
    var name = getRandomItem(productPrefixes) + " " + getRandomItem(productNouns) + " " + getRandomInt(100, 9000);
    var price = Math.round((Math.random() * 990 + 10) * 100) / 100; // 10.00 to 1000.00

    db.products.insert({
        name: name,
        description: "High quality " + name + " for your needs.",
        price: price,
        categoryId: category._id,
        categoryName: category.name,
        stock: getRandomInt(0, 500),
        rating: Math.round((Math.random() * 4 + 1) * 10) / 10, // 1.0 to 5.0
        tags: [category.name.toLowerCase(), "sale", "new"],
        createdAt: getRandomDate(new Date(2023, 0, 1), new Date())
    });
}

var productList = db.products.findall().toList();
var userList = db.users.findall().toList();

// 6. Order Data Generation
var orderStatuses = ["Pending", "Processing", "Shipped", "Delivered", "Cancelled"];

for (var i = 0; i < 100; i++) {
    var user = getRandomItem(userList);
    var itemCount = getRandomInt(1, 5);
    var items = [];
    var totalAmount = 0;

    for (var j = 0; j < itemCount; j++) {
        var product = getRandomItem(productList);
        var quantity = getRandomInt(1, 3);
        var itemTotal = product.price * quantity;

        items.push({
            productId: product._id,
            quantity: quantity
        });
        totalAmount += itemTotal;
    }

    var orderDate = getRandomDate(new Date(2023, 0, 1), new Date());

    db.orders.insert({
        userId: user._id,
        userEmail: user.email,
        items: items,
        totalAmount: Math.round(totalAmount * 100) / 100,
        status: getRandomItem(orderStatuses),
        shippingAddress: {
            street: getRandomInt(100, 999) + " Main St",
            city: "City " + getRandomInt(1, 20),
            country: user.country,
            zipCode: getRandomInt(10000, 99999).toString()
        },
        paymentMethod: getRandomItem(["Credit Card", "PayPal", "Bank Transfer"]),
        createdAt: orderDate,
        updatedAt: orderDate // Simplified
    });
}

// 7. Review Data Generation
var reviewTexts = [
    "Great product!", "Not what I expected.", "Excellent quality.", "Fast shipping.",
    "Would buy again.", "Terrible.", "Okay for the price.", "Loved it!",
    "Broken on arrival.", "Best purchase ever."
];

for (var i = 0; i < 150; i++) {
    var user = getRandomItem(userList);
    var product = getRandomItem(productList);

    db.reviews.insert({
        userId: user._id,
        userName: user.firstName + " " + user.lastName,
        productId: product._id,
        productName: product.name,
        rating: getRandomInt(1, 5),
        comment: getRandomItem(reviewTexts),
        createdAt: getRandomDate(new Date(2023, 0, 1), new Date()),
        helpfulVotes: getRandomInt(0, 50)
    });
}

// 8. Create Views

// Public View: Join Orders with Products (Refactored to use db.join)
db.saveView("orderDetails", `
// @access public
var orderId = @orderId;
var order = db.orders.find(o => o._id == orderId);
if (!order) return { error: "Order not found" };

// 4-Way Join with ALIASES: items + products + categories + reviews
return db.join(
    alias(order.items, "orderitems"), 
    alias(db.products, "products"), 
    alias(db.categories, "categories"), 
    alias(db.reviews, "reviews")
)
    .where(x => 
        x.orderitems.productId == x.products._id && 
        x.products.categoryId == x.categories._id &&
        x.reviews.productId == x.products._id
    )
    .select(x => ({
        productId: x.orderitems.productId,
        name: x.products.name,
        category: x.categories.name,
        price: x.products.price,
        quantity: x.orderitems.quantity,
        total: Math.round(x.products.price * x.orderitems.quantity * 100) / 100,
        recentReview: {
            rating: x.reviews.rating,
            comment: x.reviews.comment
        }
    })).toList();
`);

// Public View: Top Selling Products (Mock logic using rating for simplicity in demo)
db.saveView("topRatedProducts", `
// @access public
var items = db.products.findall().select(p => ({
    name: p.name,
    price: p.price,
    rating: p.rating,
    category: p.categoryName
})).toList();

var jsArray = [];
for(var i=0; i < items.length; i++) {
    jsArray.push(items[i]);
}

return jsArray.sort((a, b) => b.rating - a.rating).slice(0, 10);
`);

// Public View Search Products
// Public View: Search Products (Using new .contains() feature)
db.saveView("searchProducts", `
// @access public
var keyword = @keyword;
if (!keyword) return [];
// Case-insensitive search using .contains()
return db.products.findall(p => p.name.contains(keyword) || p.description.contains(keyword)).toList();
`);

// Private View: User Orders
db.saveView("userOrders", `
// @access private
var userId = @userId;
return db.orders.findall(o => o.userId == userId).toList();
`);


// Private View: Dashboard Stats
db.saveView("dashboardStats", `
// @access private
var totalUsers = db.users.findall().toList().length;
var totalOrders = db.orders.findall().toList().length;
var totalProducts = db.products.findall().toList().length;
var totalRevenue = 0;

db.orders.findall().foreach(o => {
    totalRevenue += o.totalAmount;
});

return {
    users: totalUsers,
    orders: totalOrders,
    products: totalProducts,
    revenue: Math.round(totalRevenue * 100) / 100
};
`);

// 9. Triggers

// Trigger 1: Audit Log for Product Price Changes
db.saveTrigger("auditProductPrice", "products", `
// @target products
if (event.type === 'changed') {
    var product = db.products.find(p => p._id == event.documentId);
    if (product) {
        // Log to a separate 'audit_logs' collection
        // Note: In real app we might want to compare old vs new, but here we just log the event.
        // To do that effectively we'd need 'oldValue' in event which isn't there yet, 
        // so we just log "Product updated".
        
        console.log("Product updated: " + product.name + " (" + event.documentId + ")");
        
        // Example: If price massive, alert?
        if (product.price > 5000) {
           console.log("High value product updated!");
        }
    }
}
`);

// Trigger 2: Low Stock Alert
db.saveTrigger("lowStockAlert", "products", `
// @target products
if (event.type === 'changed' || event.type === 'created') {
    var product = db.products.find(p => p._id == event.documentId);
    if (product && product.stock < 10) {
        console.log("ALERT: Low stock for " + product.name + "! Only " + product.stock + " left.");
        // Simulate webhook call to inventory system
        // webhook("https://inventory.example.com/alert", { productId: product._id, stock: product.stock }, {});
    }
}
`);

// Trigger 3: Welcome Email for New Users
db.saveTrigger("welcomeEmail", "users", `
// @target users
if (event.type === 'created') {
    var user = db.users.find(u => u._id == event.documentId);
    if (user) {
        console.log("Sending welcome email to: " + user.email);
        // Simulate email service webhook
        // webhook("https://email.example.com/send", { 
        //    to: user.email, 
        //    subject: "Welcome to AxarShop!", 
        //    body: "Hi " + user.firstName + "..." 
        // }, {});
    }
}
`);

// Return success message
return { success: true, message: "E-Commerce database seeded successfully with new Triggers and Views!" };
