using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.IO;
using System.Windows.Forms;
using static cafe_task.Form1;

namespace cafe_task
{
    public static class DatabaseHelper
    {
        private static readonly string DbPath = Path.Combine(Application.StartupPath, "coffee.db");
        private static string ConnectionString => $"Data Source={DbPath};Version=3;";

        public static SQLiteConnection GetConnection()
        {
            var conn = new SQLiteConnection(ConnectionString);
            conn.Open();
            return conn;
        }

        public static void Initialize()
        {
            if (File.Exists(DbPath)) return;

            SQLiteConnection.CreateFile(DbPath);
            using (var conn = GetConnection())
            {
                string sql = @"
                    CREATE TABLE Users (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        Username TEXT UNIQUE NOT NULL,
                        Password TEXT NOT NULL,
                        Role TEXT NOT NULL DEFAULT 'user',
                        Balance REAL NOT NULL DEFAULT 0
                    );
                    CREATE TABLE Ingredients (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        Name TEXT NOT NULL,
                        Unit TEXT NOT NULL,
                        StockQuantity REAL NOT NULL DEFAULT 0
                    );
                    CREATE TABLE Products (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        Name TEXT NOT NULL,
                        Description TEXT,
                        Price REAL NOT NULL,
                        Type TEXT NOT NULL,
                        Emoji TEXT
                    );
                    CREATE TABLE ProductIngredients (
                        ProductId INTEGER NOT NULL,
                        IngredientId INTEGER NOT NULL,
                        Quantity REAL NOT NULL,
                        FOREIGN KEY(ProductId) REFERENCES Products(Id),
                        FOREIGN KEY(IngredientId) REFERENCES Ingredients(Id)
                    );
                    CREATE TABLE Orders (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        UserId INTEGER NOT NULL,
                        OrderDate TEXT NOT NULL,
                        TotalAmount REAL NOT NULL,
                        Status TEXT NOT NULL DEFAULT 'Новый',
                        FOREIGN KEY(UserId) REFERENCES Users(Id)
                    );
                    CREATE TABLE OrderItems (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        OrderId INTEGER NOT NULL,
                        ProductName TEXT NOT NULL,
                        Quantity INTEGER NOT NULL,
                        ItemPrice REAL NOT NULL,
                        SyrupsText TEXT,
                        FOREIGN KEY(OrderId) REFERENCES Orders(Id)
                    );
                    INSERT INTO Users (Username, Password, Role) VALUES ('admin', 'admin', 'admin');
                    INSERT INTO Users (Username, Password, Role) VALUES ('user', 'user', 'user');
                    INSERT INTO Ingredients (Name, Unit, StockQuantity) VALUES
                        ('Кофейные зёрна', 'г', 2000),
                        ('Молоко', 'мл', 5000),
                        ('Сахар', 'г', 3000),
                        ('Шоколадный сироп', 'мл', 1000),
                        ('Карамельный сироп', 'мл', 1000),
                        ('Ванильный сироп', 'мл', 1000),
                        ('Кокосовый сироп', 'мл', 1000),
                        ('Мятный сироп', 'мл', 1000),
                        ('Сироп Лесной орех', 'мл', 1000),
                        ('Чизкейк', 'шт', 10),
                        ('Круассан', 'шт', 20),
                        ('Маффин', 'шт', 20),
                        ('Панкейки', 'шт', 20),
                        ('Тирамису', 'шт', 10),
                        ('Печенье', 'шт', 30);
                    INSERT INTO Products (Name, Description, Price, Type, Emoji) VALUES
                        ('Эспрессо', 'Крепкий классический кофе', 120, 'Drink', '☕'),
                        ('Капучино', 'Эспрессо с молочной пенкой', 180, 'Drink', '☕'),
                        ('Латте', 'Нежный кофе с молоком', 200, 'Drink', '🥛'),
                        ('Раф', 'Сливочный кофе с ванилью', 250, 'Drink', '✨'),
                        ('Какао', 'Горячий шоколад', 170, 'Drink', '🍫'),
                        ('Чай облепиховый', 'С медом и имбирем', 150, 'Drink', '🍊'),
                        ('Чизкейк', 'Классический нью-йорк', 280, 'Dessert', '🍰'),
                        ('Круассан', 'Свежий, слоеный', 150, 'Dessert', '🥐'),
                        ('Маффин', 'С шоколадной крошкой', 120, 'Dessert', '🧁'),
                        ('Панкейки', 'С кленовым сиропом', 220, 'Dessert', '🥞'),
                        ('Тирамису', 'Итальянский десерт', 320, 'Dessert', '🍮'),
                        ('Печенье', 'Имбирное с корицей', 80, 'Dessert', '🍪');
                    INSERT INTO ProductIngredients (ProductId, IngredientId, Quantity) VALUES
                        (1, 1, 18),
                        (2, 1, 18), (2, 2, 150),
                        (3, 1, 18), (3, 2, 200),
                        (4, 1, 18), (4, 2, 150),
                        (5, 1, 10), (5, 4, 20),
                        (6, 3, 10);
                ";
                new SQLiteCommand(sql, conn).ExecuteNonQuery();
            }
        }

        public static decimal GetBalance(int userId)
        {
            using (var conn = GetConnection())
            {
                var cmd = new SQLiteCommand("SELECT Balance FROM Users WHERE Id = @id", conn);
                cmd.Parameters.AddWithValue("@id", userId);
                var obj = cmd.ExecuteScalar();
                return obj != null ? Convert.ToDecimal(obj) : 0;
            }
        }

        public static void UpdateBalance(int userId, decimal newBalance)
        {
            using (var conn = GetConnection())
            {
                var cmd = new SQLiteCommand("UPDATE Users SET Balance = @bal WHERE Id = @id", conn);
                cmd.Parameters.AddWithValue("@bal", newBalance);
                cmd.Parameters.AddWithValue("@id", userId);
                cmd.ExecuteNonQuery();
            }
        }

        public static bool TrySpend(int userId, decimal amount)
        {
            using (var conn = GetConnection())
            {
                using (var transaction = conn.BeginTransaction())
                {
                    try
                    {
                        var cmd = new SQLiteCommand("SELECT Balance FROM Users WHERE Id = @id", conn);
                        cmd.Parameters.AddWithValue("@id", userId);
                        var obj = cmd.ExecuteScalar();
                        if (obj == null) return false;
                        decimal balance = Convert.ToDecimal(obj);
                        if (balance < amount) return false;

                        var updateCmd = new SQLiteCommand("UPDATE Users SET Balance = Balance - @amt WHERE Id = @id", conn);
                        updateCmd.Parameters.AddWithValue("@amt", amount);
                        updateCmd.Parameters.AddWithValue("@id", userId);
                        updateCmd.ExecuteNonQuery();

                        transaction.Commit();
                        return true;
                    }
                    catch
                    {
                        transaction.Rollback();
                        return false;
                    }
                }
            }
        }

        public static bool CheckAndConsumeDessert(string dessertName, int quantity = 1)
        {
            using (var conn = GetConnection())
            {
                var checkCmd = new SQLiteCommand("SELECT StockQuantity FROM Ingredients WHERE Name = @name AND Unit = 'шт'", conn);
                checkCmd.Parameters.AddWithValue("@name", dessertName);
                var stockObj = checkCmd.ExecuteScalar();
                if (stockObj == null) return false; 
                double stock = Convert.ToDouble(stockObj);
                if (stock < quantity) return false;

                var updateCmd = new SQLiteCommand("UPDATE Ingredients SET StockQuantity = StockQuantity - @qty WHERE Name = @name", conn);
                updateCmd.Parameters.AddWithValue("@qty", quantity);
                updateCmd.Parameters.AddWithValue("@name", dessertName);
                updateCmd.ExecuteNonQuery();
                return true;
            }
        }
        public static (bool success, string role, int userId) ValidateUser(string username, string password)
        {
            using (var conn = GetConnection())
            {
                var cmd = new SQLiteCommand("SELECT Id, Role FROM Users WHERE Username=@u AND Password=@p", conn);
                cmd.Parameters.AddWithValue("@u", username);
                cmd.Parameters.AddWithValue("@p", password);
                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                        return (true, reader.GetString(1), reader.GetInt32(0));
                }
            }
            return (false, null, -1);
        }

        public static bool RegisterUser(string username, string password)
        {
            try
            {
                using (var conn = GetConnection())
                {
                    var cmd = new SQLiteCommand("INSERT INTO Users (Username, Password, Role) VALUES (@u, @p, 'user')", conn);
                    cmd.Parameters.AddWithValue("@u", username);
                    cmd.Parameters.AddWithValue("@p", password);
                    cmd.ExecuteNonQuery();
                    return true;
                }
            }
            catch { return false; }
        }

        public static List<cafe_task.Form1.CoffeeMenuItem> LoadMenu()
        {
            var items = new List<cafe_task.Form1.CoffeeMenuItem>();
            using (var conn = GetConnection())
            {
                var cmd = new SQLiteCommand("SELECT Name, Description, Price, Type, Emoji FROM Products", conn);
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string type = reader.GetString(3);
                        if (type == "Drink")
                            items.Add(new cafe_task.Form1.CoffeeDrinkItem
                            {
                                Name = reader.GetString(0),
                                Description = reader.GetString(1),
                                BasePrice = reader.GetDecimal(2),
                                Emoji = reader.GetString(4)
                            });
                        else
                            items.Add(new cafe_task.Form1.CoffeeDessertItem
                            {
                                Name = reader.GetString(0),
                                Description = reader.GetString(1),
                                BasePrice = reader.GetDecimal(2),
                                Emoji = reader.GetString(4)
                            });
                    }
                }
            }
            return items;
        }
        public static DataTable GetUsers()
        {
            var dt = new DataTable();
            using (var conn = GetConnection())
            {
                var adapter = new SQLiteDataAdapter("SELECT Id, Username, Role, Balance FROM Users", conn);
                adapter.Fill(dt);
            }
            return dt;
        }
        public static bool CheckIngredientsAvailable(string drinkName)
        {
            using (var conn = GetConnection())
            {
                var cmd = new SQLiteCommand(@"
            SELECT i.StockQuantity, pi.Quantity
            FROM Ingredients i
            JOIN ProductIngredients pi ON i.Id = pi.IngredientId
            JOIN Products p ON pi.ProductId = p.Id
            WHERE p.Name = @name AND p.Type = 'Drink'", conn);
                cmd.Parameters.AddWithValue("@name", drinkName);
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        if (reader.GetDouble(0) < reader.GetDouble(1))
                            return false;
                    }
                }
                return true;
            }
        }

        public static bool CheckDessertAvailable(string dessertName, int quantity = 1)
        {
            using (var conn = GetConnection())
            {
                var cmd = new SQLiteCommand("SELECT StockQuantity FROM Ingredients WHERE Name = @name AND Unit = 'шт'", conn);
                cmd.Parameters.AddWithValue("@name", dessertName);
                var stockObj = cmd.ExecuteScalar();
                if (stockObj == null) return false;
                double stock = Convert.ToDouble(stockObj);
                return stock >= quantity;
            }
        }
        public static void UpdateUserRole(int userId, string newRole)
        {
            using (var conn = GetConnection())
            {
                var cmd = new SQLiteCommand("UPDATE Users SET Role=@r WHERE Id=@id", conn);
                cmd.Parameters.AddWithValue("@r", newRole);
                cmd.Parameters.AddWithValue("@id", userId);
                cmd.ExecuteNonQuery();
            }
        }

        public static DataTable GetOrders()
        {
            var dt = new DataTable();
            using (var conn = GetConnection())
            {
                var cmd = new SQLiteCommand(
                    @"SELECT o.Id, o.UserId, u.Username, o.OrderDate, o.TotalAmount, o.Status
              FROM Orders o JOIN Users u ON o.UserId = u.Id
              ORDER BY o.Id DESC", conn);
                var adapter = new SQLiteDataAdapter(cmd);
                adapter.Fill(dt);
            }
            return dt;
        }
        public static List<Syrup> GetAvailableSyrups()
        {
            return new List<Syrup>
            {
                new Syrup { Name = "Карамель", Price = 30 },
                new Syrup { Name = "Ваниль", Price = 30 },
                new Syrup { Name = "Кокос", Price = 40 },
                new Syrup { Name = "Мята", Price = 30 },
                new Syrup { Name = "Лесной орех", Price = 35 },
                new Syrup { Name = "Шоколад", Price = 35 }
            };
        }

        public static DataTable GetIngredients()
        {
            var dt = new DataTable();
            using (var conn = GetConnection())
            {
                var adapter = new SQLiteDataAdapter("SELECT Id, Name, Unit, StockQuantity FROM Ingredients", conn);
                adapter.Fill(dt);
            }
            return dt;
        }

        public static bool CheckAndConsumeIngredients(string drinkName)
        {
            using (var conn = GetConnection())
            {
                var cmdCheck = new SQLiteCommand(@"
                    SELECT i.StockQuantity, pi.Quantity
                    FROM Ingredients i
                    JOIN ProductIngredients pi ON i.Id = pi.IngredientId
                    JOIN Products p ON pi.ProductId = p.Id
                    WHERE p.Name = @name AND p.Type = 'Drink'", conn);
                cmdCheck.Parameters.AddWithValue("@name", drinkName);
                using (var reader = cmdCheck.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        if (reader.GetDouble(0) < reader.GetDouble(1))
                            return false;
                    }
                }

                var cmdConsume = new SQLiteCommand(@"
                    UPDATE Ingredients
                    SET StockQuantity = StockQuantity - (
                        SELECT pi.Quantity
                        FROM ProductIngredients pi
                        JOIN Products p ON pi.ProductId = p.Id
                        WHERE p.Name = @name AND pi.IngredientId = Ingredients.Id
                    )
                    WHERE Id IN (
                        SELECT IngredientId
                        FROM ProductIngredients pi
                        JOIN Products p ON pi.ProductId = p.Id
                        WHERE p.Name = @name
                    )", conn);
                cmdConsume.Parameters.AddWithValue("@name", drinkName);
                cmdConsume.ExecuteNonQuery();
                return true;
            }
        }

        public static void UpdateIngredientStock(int ingredientId, double additionalQuantity)
        {
            using (var conn = GetConnection())
            {
                var cmd = new SQLiteCommand("UPDATE Ingredients SET StockQuantity = StockQuantity + @qty WHERE Id = @id", conn);
                cmd.Parameters.AddWithValue("@qty", additionalQuantity);
                cmd.Parameters.AddWithValue("@id", ingredientId);
                cmd.ExecuteNonQuery();
            }
        }

        public static DataTable GetAllProducts()
        {
            var dt = new DataTable();
            using (var conn = GetConnection())
            {
                var adapter = new SQLiteDataAdapter("SELECT Id, Name, Description, Price, Type, Emoji FROM Products", conn);
                adapter.Fill(dt);
            }
            return dt;
        }

        public static void AddProduct(string name, string desc, decimal price, string type, string emoji)
        {
            using (var conn = GetConnection())
            {
                var cmd = new SQLiteCommand("INSERT INTO Products (Name, Description, Price, Type, Emoji) VALUES (@n, @d, @p, @t, @e)", conn);
                cmd.Parameters.AddWithValue("@n", name);
                cmd.Parameters.AddWithValue("@d", desc);
                cmd.Parameters.AddWithValue("@p", price);
                cmd.Parameters.AddWithValue("@t", type);
                cmd.Parameters.AddWithValue("@e", emoji);
                cmd.ExecuteNonQuery();

                if (type.Equals("Dessert", StringComparison.OrdinalIgnoreCase))
                {
                    var checkCmd = new SQLiteCommand("SELECT COUNT(*) FROM Ingredients WHERE Name = @name AND Unit = 'шт'", conn);
                    checkCmd.Parameters.AddWithValue("@name", name);
                    int exists = Convert.ToInt32(checkCmd.ExecuteScalar());

                    if (exists == 0)
                    {
                        var addIngredientCmd = new SQLiteCommand(
                            "INSERT INTO Ingredients (Name, Unit, StockQuantity) VALUES (@name, 'шт', 0)", conn);
                        addIngredientCmd.Parameters.AddWithValue("@name", name);
                        addIngredientCmd.ExecuteNonQuery();
                    }
                }
            }
        }

        public static void DeleteProduct(int id)
        {
            using (var conn = GetConnection())
            {
                var cmd = new SQLiteCommand("DELETE FROM Products WHERE Id = @id", conn);
                cmd.Parameters.AddWithValue("@id", id);
                cmd.ExecuteNonQuery();
            }
        }

        public static void UpdateProductPrice(int id, decimal newPrice)
        {
            using (var conn = GetConnection())
            {
                var cmd = new SQLiteCommand("UPDATE Products SET Price = @p WHERE Id = @id", conn);
                cmd.Parameters.AddWithValue("@p", newPrice);
                cmd.Parameters.AddWithValue("@id", id);
                cmd.ExecuteNonQuery();
            }
        }

        public static int CreateOrder(int userId, decimal total)
        {
            using (var conn = GetConnection())
            {
                var cmd = new SQLiteCommand("INSERT INTO Orders (UserId, OrderDate, TotalAmount) VALUES (@uid, @date, @total); SELECT last_insert_rowid();", conn);
                cmd.Parameters.AddWithValue("@uid", userId);
                cmd.Parameters.AddWithValue("@date", DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
                cmd.Parameters.AddWithValue("@total", total);
                return Convert.ToInt32(cmd.ExecuteScalar());
            }
        }

        public static void AddOrderItem(int orderId, string productName, int quantity, decimal itemPrice, string syrupsText)
        {
            using (var conn = GetConnection())
            {
                var cmd = new SQLiteCommand("INSERT INTO OrderItems (OrderId, ProductName, Quantity, ItemPrice, SyrupsText) VALUES (@oid, @name, @qty, @price, @syrup)", conn);
                cmd.Parameters.AddWithValue("@oid", orderId);
                cmd.Parameters.AddWithValue("@name", productName);
                cmd.Parameters.AddWithValue("@qty", quantity);
                cmd.Parameters.AddWithValue("@price", itemPrice);
                cmd.Parameters.AddWithValue("@syrup", syrupsText);
                cmd.ExecuteNonQuery();
            }
        }
        public static int GetDessertStock(string dessertName)
        {
            using (var conn = GetConnection())
            {
                var cmd = new SQLiteCommand("SELECT StockQuantity FROM Ingredients WHERE Name = @name AND Unit = 'шт'", conn);
                cmd.Parameters.AddWithValue("@name", dessertName);
                var obj = cmd.ExecuteScalar();
                return obj != null ? Convert.ToInt32(obj) : 0;
            }
        }
        public static DataTable GetOrderItems(int orderId)
        {
            var dt = new DataTable();
            using (var conn = GetConnection())
            {
                var cmd = new SQLiteCommand("SELECT ProductName, Quantity, ItemPrice, SyrupsText FROM OrderItems WHERE OrderId = @oid", conn);
                cmd.Parameters.AddWithValue("@oid", orderId);
                var adapter = new SQLiteDataAdapter(cmd);
                adapter.Fill(dt);
            }
            return dt;
        }
        public static void UpdateOrderStatus(int orderId, string newStatus)
        {
            using (var conn = GetConnection())
            {
                var cmd = new SQLiteCommand("UPDATE Orders SET Status = @s WHERE Id = @id", conn);
                cmd.Parameters.AddWithValue("@s", newStatus);
                cmd.Parameters.AddWithValue("@id", orderId);
                cmd.ExecuteNonQuery();
            }
        }
        public static DataTable GetUserOrders(int userId)
        {
            var dt = new DataTable();
            using (var conn = GetConnection())
            {
                var cmd = new SQLiteCommand(
                    @"SELECT o.Id, o.OrderDate, o.TotalAmount, o.Status
              FROM Orders o
              WHERE o.UserId = @uid
              ORDER BY o.Id DESC", conn);
                cmd.Parameters.AddWithValue("@uid", userId);
                var adapter = new SQLiteDataAdapter(cmd);
                adapter.Fill(dt);
            }
            return dt;
        }
    }
}