using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace cafe_task
{
    public partial class Form1 : Form
    {
        public class CoffeeMenuItem
        {
            public string Name { get; set; }
            public string Description { get; set; }
            public decimal BasePrice { get; set; }
            public string Emoji { get; set; }
        }
        public class CoffeeDrinkItem : CoffeeMenuItem
        {
            public List<Syrup> AddedSyrups { get; set; } = new List<Syrup>();
            public decimal GetTotalPrice() => BasePrice + AddedSyrups.Sum(s => s.Price);
        }
        public class CoffeeDessertItem : CoffeeMenuItem { }
        public class Syrup
        {
            public string Name { get; set; }
            public decimal Price { get; set; }
        }
        public class CartItem
        {
            public CoffeeMenuItem Item { get; set; }
            public int Quantity { get; set; }
            public string SyrupsText { get; set; }
            public decimal ItemPrice { get; set; }
            public decimal GetTotalPrice() => ItemPrice * Quantity;
        }

        private Panel loginPanel, userPanel, adminPanel;
        private int currentUserId;
        private string currentRole;
        private List<CoffeeDrinkItem> drinks = new List<CoffeeDrinkItem>();
        private List<CoffeeDessertItem> desserts = new List<CoffeeDessertItem>();
        private List<CartItem> cart = new List<CartItem>();
        private List<Syrup> availableSyrups;

        private FlowLayoutPanel menuPanel;
        private Panel cartItemsContainer;
        private Label totalLabel;
        private Label userBalanceLabel;

        private FlowLayoutPanel adminLeftPanel;
        private Panel adminRightPanel;
        private CoffeeMenuItem selectedAdminItem;
        private TextBox orderSearchBox;

        public Form1()
        {
            InitializeComponent();

            // Включаем двойную буферизацию для предотвращения мерцания
            this.SetStyle(ControlStyles.AllPaintingInWmPaint |
                          ControlStyles.UserPaint |
                          ControlStyles.DoubleBuffer |
                          ControlStyles.ResizeRedraw, true);
            this.UpdateStyles();

            DatabaseHelper.Initialize();
            availableSyrups = DatabaseHelper.GetAvailableSyrups();

            this.Text = "☕ Уютная кофейня";
            this.Size = new Size(1100, 700);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(255, 248, 240);
            this.Font = new Font("Segoe UI", 10);

            BuildLoginPanel();
            BuildUserPanel();
            BuildAdminPanel();

            loginPanel.Visible = true;
            userPanel.Visible = false;
            adminPanel.Visible = false;

            this.Controls.Add(loginPanel);
            this.Controls.Add(userPanel);
            this.Controls.Add(adminPanel);
        }

        private void BuildLoginPanel()
        {
            loginPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(200, 0, 0, 0)
            };
            loginPanel.GetType().GetProperty("DoubleBuffered",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?.SetValue(loginPanel, true, null);
            RoundedPanel card = new RoundedPanel
            {
                Size = new Size(350, 280),
                BackColor = Color.FromArgb(255, 248, 240),
                BorderColor = Color.FromArgb(188, 170, 164),
                CornerRadius = 16,
                ShadowColor = Color.FromArgb(80, 0, 0, 0),
                BorderWidth = 1
            };
            card.GetType().GetProperty("DoubleBuffered",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?.SetValue(card, true, null);
            card.Location = new Point((this.ClientSize.Width - card.Width) / 2, (this.ClientSize.Height - card.Height) / 2);
            loginPanel.Controls.Add(card);

            Label title = new Label { Text = "☕ Уютная кофейня", Font = new Font("Segoe UI", 16, FontStyle.Bold), ForeColor = Color.FromArgb(78, 52, 46), Location = new Point(40, 20), AutoSize = true };
            Label lblUser = new Label { Text = "Логин:", Location = new Point(40, 70), AutoSize = true };
            var txtUser = new TextBox { Location = new Point(40, 95), Width = 260, Font = new Font("Segoe UI", 10) };
            Label lblPass = new Label { Text = "Пароль:", Location = new Point(40, 130), AutoSize = true };
            var txtPass = new TextBox { Location = new Point(40, 155), Width = 260, PasswordChar = '*', Font = new Font("Segoe UI", 10) };

            var btnLogin = new Button { Text = "Войти", Location = new Point(40, 200), Width = 120, Height = 35, BackColor = Color.FromArgb(109, 76, 65), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 10, FontStyle.Bold) };
            var btnRegister = new Button { Text = "Регистрация", Location = new Point(180, 200), Width = 120, Height = 35, BackColor = Color.White, ForeColor = Color.FromArgb(109, 76, 65), FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 10) };            
            txtUser.TabIndex = 0;
            txtPass.TabIndex = 1;
            btnLogin.TabIndex = 2;
            btnRegister.TabIndex = 3;
            var lblError = new Label { ForeColor = Color.Red, Location = new Point(40, 240), AutoSize = true };

            btnLogin.Click += (s, e) =>
            {
                lblError.ForeColor = Color.Red; // Всегда сбрасываем на красный перед проверкой
                var (success, role, userId) = DatabaseHelper.ValidateUser(txtUser.Text.Trim(), txtPass.Text);
                if (success)
                {
                    currentUserId = userId;
                    currentRole = role;
                    loginPanel.Visible = false;
                    if (role == "admin")
                    {
                        adminPanel.Visible = true;
                        ShowAdminTab("products");
                    }
                    else
                    {
                        userPanel.Visible = true;
                        LoadMenuFromDatabase();
                        decimal bal = DatabaseHelper.GetBalance(currentUserId);
                        userBalanceLabel.Text = $"Баланс: {bal:F2} ₽";
                    }
                }
                else
                {
                    lblError.ForeColor = Color.Red; 
                    lblError.Text = "Неверный логин или пароль";
                }
            };

            btnRegister.Click += (s, e) =>
            {
                lblError.ForeColor = Color.Red; 
                if (string.IsNullOrWhiteSpace(txtUser.Text) || string.IsNullOrWhiteSpace(txtPass.Text))
                {
                    lblError.ForeColor = Color.Red;
                    lblError.Text = "Заполните все поля";
                    return;
                }
                if (DatabaseHelper.RegisterUser(txtUser.Text.Trim(), txtPass.Text))
                {
                    lblError.ForeColor = Color.Green;
                    lblError.Text = "Регистрация успешна. Войдите.";
                    txtPass.Clear();
                }
                else
                {
                    lblError.ForeColor = Color.Red;
                    lblError.Text = "Пользователь уже существует";
                }
            };

            card.Controls.Add(title);
            card.Controls.Add(lblUser);
            card.Controls.Add(txtUser);
            card.Controls.Add(lblPass);
            card.Controls.Add(txtPass);
            card.Controls.Add(btnLogin);
            card.Controls.Add(btnRegister);
            card.Controls.Add(lblError);
        }

        private void BuildUserPanel()
        {
            userPanel = new Panel { Dock = DockStyle.Fill, Visible = false };

            TableLayoutPanel mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 2,
                Padding = new Padding(10)
            };
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65));
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 80));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            userPanel.Controls.Add(mainLayout);

            Panel headerPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(109, 76, 65) };
            TableLayoutPanel headerLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1
            };
            headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 200));  // баланс
            headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));   // заголовок
            headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 200));  // кнопки
            headerPanel.Controls.Add(headerLayout);

            userBalanceLabel = new Label
            {
                Text = "Баланс: 0.00 ₽",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
                Dock = DockStyle.Fill,
                Padding = new Padding(10, 0, 0, 0)
            };
            headerLayout.Controls.Add(userBalanceLabel, 0, 0);

            Label titleLabel = new Label
            {
                Text = "☕ УЮТНАЯ КОФЕЙНЯ ☕",
                Font = new Font("Segoe UI", 28, FontStyle.Bold),
                ForeColor = Color.FromArgb(255, 224, 178),
                TextAlign = ContentAlignment.MiddleCenter,
                Dock = DockStyle.Fill
            };
            headerLayout.Controls.Add(titleLabel, 1, 0);

            FlowLayoutPanel buttonsPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                BackColor = Color.Transparent,
                Padding = new Padding(0, 5, 10, 0),
                AutoSize = true
            };

            Button topUpBtn = new Button
            {
                Text = "Пополнить",
                Width = 90,
                Height = 35,
                BackColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                Margin = new Padding(0, 0, 5, 0)
            };
            topUpBtn.FlatAppearance.BorderColor = Color.FromArgb(215, 204, 200);
            topUpBtn.FlatAppearance.BorderSize = 1;
            topUpBtn.Click += (s, e) =>
            {
                Form dialog = new Form
                {
                    Text = "Пополнение баланса",
                    Size = new Size(320, 200),
                    StartPosition = FormStartPosition.CenterParent,
                    BackColor = Color.FromArgb(255, 248, 240),
                    FormBorderStyle = FormBorderStyle.FixedDialog,
                    MaximizeBox = false,
                    MinimizeBox = false
                };
                Label lbl = new Label { Text = "Введите сумму (₽):", Location = new Point(20, 25), Font = new Font("Segoe UI", 11), AutoSize = true };
                TextBox txtAmount = new TextBox { Location = new Point(20, 60), Width = 200, Font = new Font("Segoe UI", 12) };
                Button btnOk = new Button { Text = "Пополнить", Location = new Point(20, 110), Size = new Size(120, 35), BackColor = Color.FromArgb(109, 76, 65), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, DialogResult = DialogResult.OK };
                Button btnCancel = new Button { Text = "Отмена", Location = new Point(160, 110), Size = new Size(120, 35), FlatStyle = FlatStyle.Flat, DialogResult = DialogResult.Cancel };
                btnOk.Click += (ss, ev) =>
                {
                    if (string.IsNullOrWhiteSpace(txtAmount.Text))
                    {
                        MessageBox.Show("Введите сумму.", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        dialog.DialogResult = DialogResult.None;
                        return;
                    }
                    if (!decimal.TryParse(txtAmount.Text, out decimal amount) || amount <= 0)
                    {
                        MessageBox.Show("Введите положительное число.", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        dialog.DialogResult = DialogResult.None;
                        return;
                    }
                    decimal currentBalance = DatabaseHelper.GetBalance(currentUserId);
                    DatabaseHelper.UpdateBalance(currentUserId, currentBalance + amount);
                    userBalanceLabel.Text = $"Баланс: {currentBalance + amount:F2} ₽";
                };
                dialog.Controls.Add(lbl);
                dialog.Controls.Add(txtAmount);
                dialog.Controls.Add(btnOk);
                dialog.Controls.Add(btnCancel);
                dialog.AcceptButton = btnOk;
                dialog.CancelButton = btnCancel;
                dialog.ShowDialog();
            };

            Button logoutBtn = new Button
            {
                Text = "Выйти",
                Width = 80,
                Height = 35,
                BackColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                Margin = new Padding(0, 0, 0, 0)
            };
            logoutBtn.FlatAppearance.BorderColor = Color.FromArgb(215, 204, 200);
            logoutBtn.FlatAppearance.BorderSize = 1;
            logoutBtn.Click += (s, e) =>
            {
                userPanel.Visible = false;
                loginPanel.Visible = true;
                ResetLoginForm();
            };

            buttonsPanel.Controls.Add(topUpBtn);
            buttonsPanel.Controls.Add(logoutBtn);
            headerLayout.Controls.Add(buttonsPanel, 2, 0);

            mainLayout.Controls.Add(headerPanel, 0, 0);
            mainLayout.SetColumnSpan(headerPanel, 2);

            menuPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = Color.FromArgb(255, 248, 240),
                Padding = new Padding(10),
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false
            };
            mainLayout.Controls.Add(menuPanel, 0, 1);

            Panel rightPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
            TableLayoutPanel rightLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2
            };
            rightLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
            rightLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            rightPanel.Controls.Add(rightLayout);

            FlowLayoutPanel switchPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(245, 245, 245),
                Padding = new Padding(5)
            };
            Button cartTabBtn = new Button
            {
                Text = "🛒 Корзина",
                Width = 130,
                Height = 30,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(109, 76, 65),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9, FontStyle.Bold)
            };
            cartTabBtn.FlatAppearance.BorderSize = 0;

            Button ordersTabBtn = new Button
            {
                Text = "📋 Мои заказы",
                Width = 130,
                Height = 30,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.White,
                ForeColor = Color.FromArgb(109, 76, 65),
                Font = new Font("Segoe UI", 9, FontStyle.Bold)
            };
            ordersTabBtn.FlatAppearance.BorderColor = Color.FromArgb(109, 76, 65);
            ordersTabBtn.FlatAppearance.BorderSize = 1;
            switchPanel.Controls.Add(cartTabBtn);
            switchPanel.Controls.Add(ordersTabBtn);
            rightLayout.Controls.Add(switchPanel, 0, 0);

            Panel contentContainer = new Panel { Dock = DockStyle.Fill };
            rightLayout.Controls.Add(contentContainer, 0, 1);

            Panel cartContent = BuildUserCartPanel();
            cartContent.Visible = true;
            contentContainer.Controls.Add(cartContent);

            Panel ordersContent = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
            contentContainer.Controls.Add(ordersContent);
            ordersContent.Visible = false;

            cartTabBtn.Click += (s, e) =>
            {
                cartContent.Visible = true;
                ordersContent.Visible = false;
                cartTabBtn.BackColor = Color.FromArgb(109, 76, 65);
                cartTabBtn.ForeColor = Color.White;
                ordersTabBtn.BackColor = Color.White;
                ordersTabBtn.ForeColor = Color.FromArgb(109, 76, 65);
            };

            ordersTabBtn.Click += (s, e) =>
            {
                cartContent.Visible = false;
                ordersContent.Visible = true;
                LoadUserOrders(ordersContent);
                ordersTabBtn.BackColor = Color.FromArgb(109, 76, 65);
                ordersTabBtn.ForeColor = Color.White;
                cartTabBtn.BackColor = Color.White;
                cartTabBtn.ForeColor = Color.FromArgb(109, 76, 65);
            };

            mainLayout.Controls.Add(rightPanel, 1, 1);
        }

        private Panel BuildUserCartPanel()
        {
            Panel panel = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(10) };
            Label cartTitle = new Label { Text = "🛒 ВАША КОРЗИНА", Font = new Font("Segoe UI", 14, FontStyle.Bold), ForeColor = Color.FromArgb(78, 52, 46), Location = new Point(0, 0), Size = new Size(300, 30) };
            panel.Controls.Add(cartTitle);
            cartItemsContainer = new Panel { Location = new Point(0, 35), Size = new Size(320, 330), BackColor = Color.FromArgb(255, 248, 240), AutoScroll = true, BorderStyle = BorderStyle.FixedSingle };
            panel.Controls.Add(cartItemsContainer);
            Label totalTitle = new Label { Text = "ИТОГО:", Font = new Font("Segoe UI", 12, FontStyle.Bold), ForeColor = Color.FromArgb(78, 52, 46), Location = new Point(0, 375), Size = new Size(100, 25) };
            panel.Controls.Add(totalTitle);
            totalLabel = new Label { Text = "0.00 ₽", Font = new Font("Segoe UI", 14, FontStyle.Bold), ForeColor = Color.FromArgb(109, 76, 65), TextAlign = ContentAlignment.MiddleRight, Location = new Point(100, 375), Size = new Size(210, 25) };
            panel.Controls.Add(totalLabel);
            Button checkoutBtn = new Button { Text = "✨ ОФОРМИТЬ", Font = new Font("Segoe UI", 10, FontStyle.Bold), BackColor = Color.FromArgb(109, 76, 65), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Location = new Point(0, 410), Size = new Size(155, 40) };
            checkoutBtn.FlatAppearance.BorderSize = 0;
            checkoutBtn.Click += CheckoutButton_Click;
            panel.Controls.Add(checkoutBtn);
            Button printBtn = new Button { Text = "📄 ПЕЧАТЬ", Font = new Font("Segoe UI", 10), BackColor = Color.White, ForeColor = Color.FromArgb(109, 76, 65), FlatStyle = FlatStyle.Flat, Location = new Point(165, 410), Size = new Size(155, 40) };
            printBtn.FlatAppearance.BorderColor = Color.FromArgb(215, 204, 200);
            printBtn.FlatAppearance.BorderSize = 1;
            printBtn.Click += PrintReceiptButton_Click;
            panel.Controls.Add(printBtn);
            Button clearBtn = new Button { Text = "🗑️ ОЧИСТИТЬ КОРЗИНУ", Font = new Font("Segoe UI", 9), BackColor = Color.White, ForeColor = Color.FromArgb(141, 110, 99), FlatStyle = FlatStyle.Flat, Location = new Point(0, 460), Size = new Size(320, 35) };
            clearBtn.FlatAppearance.BorderColor = Color.White;
            clearBtn.FlatAppearance.BorderSize = 0;
            clearBtn.Click += ClearCartButton_Click;
            panel.Controls.Add(clearBtn);
            return panel;
        }

        private void LoadMenuFromDatabase()
        {
            var menu = DatabaseHelper.LoadMenu();
            drinks = menu.OfType<CoffeeDrinkItem>().ToList();
            desserts = menu.OfType<CoffeeDessertItem>().ToList();
            CreateMenu();
        }

        private void CreateMenu()
        {
            menuPanel.Controls.Clear();
            Label drinksLabel = new Label { Text = "☕ ГОРЯЧИЕ НАПИТКИ", Font = new Font("Segoe UI", 16, FontStyle.Bold), ForeColor = Color.FromArgb(78, 52, 46), Size = new Size(600, 35), Margin = new Padding(0, 0, 0, 10) };
            menuPanel.Controls.Add(drinksLabel);
            FlowLayoutPanel drinksGrid = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = true, Width = 600 };
            foreach (var drink in drinks) drinksGrid.Controls.Add(CreateCard(drink));
            menuPanel.Controls.Add(drinksGrid);
            Label dessertsLabel = new Label { Text = "🍰 ДЕСЕРТЫ", Font = new Font("Segoe UI", 16, FontStyle.Bold), ForeColor = Color.FromArgb(78, 52, 46), Size = new Size(600, 35), Margin = new Padding(0, 20, 0, 10) };
            menuPanel.Controls.Add(dessertsLabel);
            FlowLayoutPanel dessertsGrid = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = true, Width = 600 };
            foreach (var dessert in desserts) dessertsGrid.Controls.Add(CreateCard(dessert));
            menuPanel.Controls.Add(dessertsGrid);
        }

        private Panel CreateCard(CoffeeMenuItem item)
        {
            RoundedPanel card = new RoundedPanel
            {
                Size = new Size(180, 200),
                BackColor = Color.White,
                Margin = new Padding(10),
                Cursor = Cursors.Hand,
                BorderColor = Color.FromArgb(188, 170, 164),
                ShadowColor = Color.FromArgb(50, 0, 0, 0),
                CornerRadius = 12
            };

            Label emoji = new Label
            {
                Text = item.Emoji,
                Font = new Font("Segoe UI Emoji", 32),
                TextAlign = ContentAlignment.MiddleCenter,
                Size = new Size(180, 55),
                Location = new Point(0, 5),
                BackColor = Color.Transparent
            };
            card.Controls.Add(emoji);

            Label name = new Label
            {
                Text = item.Name,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(5, 62),
                Size = new Size(170, 22),
                AutoEllipsis = true
            };
            card.Controls.Add(name);

            Label desc = new Label
            {
                Text = item.Description,
                Font = new Font("Segoe UI", 7),
                ForeColor = Color.FromArgb(121, 85, 72),
                TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(5, 86),
                Size = new Size(170, 35),
                AutoEllipsis = true
            };
            card.Controls.Add(desc);

            if (item is CoffeeDessertItem dessert)
            {
                int stock = DatabaseHelper.GetDessertStock(dessert.Name);
                Label stockLabel = new Label
                {
                    Text = stock > 0 ? $"✓ В наличии: {stock} шт." : "✗ Нет в наличии",
                    Font = new Font("Segoe UI", 8, FontStyle.Bold),
                    ForeColor = stock > 0 ? Color.FromArgb(56, 142, 60) : Color.FromArgb(198, 40, 40),
                    TextAlign = ContentAlignment.MiddleCenter,
                    Location = new Point(5, 122),
                    Size = new Size(170, 18)
                };
                card.Controls.Add(stockLabel);
            }
            else if (item is CoffeeDrinkItem drink)
            {
                bool available = DatabaseHelper.CheckIngredientsAvailable(drink.Name);
                if (!available)
                {
                    Label stockLabel = new Label
                    {
                        Text = "✗ Нет ингредиентов",
                        Font = new Font("Segoe UI", 8, FontStyle.Bold),
                        ForeColor = Color.FromArgb(198, 40, 40),
                        TextAlign = ContentAlignment.MiddleCenter,
                        Location = new Point(5, 122),
                        Size = new Size(170, 18)
                    };
                    card.Controls.Add(stockLabel);
                    card.Cursor = Cursors.Default;
                    card.BackColor = Color.FromArgb(250, 240, 240);
                }
            }

            RoundedPanel pricePanel = new RoundedPanel
            {
                Location = new Point(10, 145),
                Size = new Size(160, 35),
                BackColor = Color.FromArgb(255, 224, 178),
                CornerRadius = 8,
                BorderWidth = 0
            };

            Label price = new Label
            {
                Text = $"{item.BasePrice:F2} ₽",
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                ForeColor = Color.FromArgb(109, 76, 65),
                TextAlign = ContentAlignment.MiddleCenter,
                Dock = DockStyle.Fill
            };
            pricePanel.Controls.Add(price);
            card.Controls.Add(pricePanel);

            EventHandler clickHandler = (s, e) => {
                if (item is CoffeeDessertItem d && DatabaseHelper.GetDessertStock(d.Name) == 0)
                {
                    MessageBox.Show($"❌ Товар \"{d.Name}\" закончился!", "Нет в наличии", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                if (item is CoffeeDrinkItem dr && !DatabaseHelper.CheckIngredientsAvailable(dr.Name))
                {
                    MessageBox.Show($"❌ Недостаточно ингредиентов для \"{dr.Name}\"!", "Нет в наличии", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                MenuItem_Click(item);
            };

            card.Click += clickHandler;
            emoji.Click += clickHandler;
            name.Click += clickHandler;
            desc.Click += clickHandler;
            pricePanel.Click += clickHandler;
            price.Click += clickHandler;

            EventHandler mouseEnterHandler = (s, e) =>
            {
                bool isAvailable = true;
                if (item is CoffeeDessertItem d) isAvailable = DatabaseHelper.GetDessertStock(d.Name) > 0;
                if (item is CoffeeDrinkItem dr) isAvailable = DatabaseHelper.CheckIngredientsAvailable(dr.Name);

                if (isAvailable)
                {
                    card.BackColor = Color.FromArgb(255, 253, 248);
                    card.BorderColor = Color.FromArgb(161, 136, 127);
                    card.Cursor = Cursors.Hand;
                }
                else
                {
                    card.Cursor = Cursors.No;
                }
            };

            EventHandler mouseLeaveHandler = (s, e) =>
            {
                if (!card.ClientRectangle.Contains(card.PointToClient(Cursor.Position)))
                {
                    bool isAvailable = true;
                    if (item is CoffeeDessertItem d) isAvailable = DatabaseHelper.GetDessertStock(d.Name) > 0;
                    if (item is CoffeeDrinkItem dr) isAvailable = DatabaseHelper.CheckIngredientsAvailable(dr.Name);

                    if (isAvailable)
                    {
                        card.BackColor = Color.White;
                        card.BorderColor = Color.FromArgb(188, 170, 164);
                    }
                }
            };

            card.MouseEnter += mouseEnterHandler;
            card.MouseLeave += mouseLeaveHandler;

            emoji.MouseEnter += mouseEnterHandler;
            emoji.MouseLeave += mouseLeaveHandler;
            name.MouseEnter += mouseEnterHandler;
            name.MouseLeave += mouseLeaveHandler;
            desc.MouseEnter += mouseEnterHandler;
            desc.MouseLeave += mouseLeaveHandler;
            pricePanel.MouseEnter += mouseEnterHandler;
            pricePanel.MouseLeave += mouseLeaveHandler;
            price.MouseEnter += mouseEnterHandler;
            price.MouseLeave += mouseLeaveHandler;

            foreach (Control ctrl in card.Controls)
            {
                if (ctrl is Label lbl && (lbl.Text.Contains("В наличии") || lbl.Text.Contains("Нет в наличии") || lbl.Text.Contains("Нет ингредиентов")))
                {
                    lbl.MouseEnter += mouseEnterHandler;
                    lbl.MouseLeave += mouseLeaveHandler;
                }
            }

            return card;
        }

        private void MenuItem_Click(CoffeeMenuItem item)
        {
            if (item is CoffeeDrinkItem drink)
            {
                if (!DatabaseHelper.CheckIngredientsAvailable(drink.Name))
                {
                    MessageBox.Show("❌ Недостаточно ингредиентов на складе!", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                ShowSyrupDialog(drink);
            }
            else if (item is CoffeeDessertItem dessert)
            {
                int stock = DatabaseHelper.GetDessertStock(dessert.Name);
                if (stock == 0)
                {
                    MessageBox.Show($"❌ К сожалению, \"{dessert.Name}\" закончился!", "Нет в наличии", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                int alreadyInCart = cart.Where(c => c.Item.Name == dessert.Name).Sum(c => c.Quantity);
                int available = stock - alreadyInCart;

                if (available <= 0)
                {
                    MessageBox.Show($"❌ Вы уже добавили все доступные \"{dessert.Name}\" (всего {stock} шт.)",
                                  "Лимит достигнут", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                AddToCart(dessert, "");
            }
        }

        private void ShowSyrupDialog(CoffeeDrinkItem drink)
        {
            Form dialog = new Form { Text = $"✨ Выберите сиропы для {drink.Name}", Size = new Size(380, 380), StartPosition = FormStartPosition.CenterParent, BackColor = Color.FromArgb(255, 248, 240), FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false, ShowInTaskbar = false };
            Label infoLabel = new Label { Text = "Выберите сиропы (можно несколько):", Font = new Font("Segoe UI", 10), Location = new Point(20, 15), Size = new Size(330, 25) };
            dialog.Controls.Add(infoLabel);
            CheckedListBox syrupList = new CheckedListBox { Location = new Point(20, 45), Size = new Size(320, 180), Font = new Font("Segoe UI", 11), CheckOnClick = true };
            foreach (var syrup in availableSyrups) syrupList.Items.Add($"{syrup.Name} (+{syrup.Price:F0} ₽)");
            dialog.Controls.Add(syrupList);
            Label priceInfoLabel = new Label { Text = $"Базовая цена: {drink.BasePrice:F2} ₽", Font = new Font("Segoe UI", 9), ForeColor = Color.FromArgb(109, 76, 65), Location = new Point(20, 235), Size = new Size(320, 20) };
            dialog.Controls.Add(priceInfoLabel);
            Label totalPriceLabel = new Label { Text = $"Итого: {drink.BasePrice:F2} ₽", Font = new Font("Segoe UI", 11, FontStyle.Bold), ForeColor = Color.FromArgb(78, 52, 46), Location = new Point(20, 255), Size = new Size(320, 25) };
            dialog.Controls.Add(totalPriceLabel);
            syrupList.ItemCheck += (s, e) =>
            {
                decimal totalPrice = drink.BasePrice;
                for (int i = 0; i < syrupList.Items.Count; i++)
                {
                    bool isChecked = (i == e.Index) ? (e.NewValue == CheckState.Checked) : syrupList.GetItemChecked(i);
                    if (isChecked) totalPrice += availableSyrups[i].Price;
                }
                totalPriceLabel.Text = $"Итого: {totalPrice:F2} ₽";
            };
            Button addBtn = new Button { Text = "✅ ДОБАВИТЬ В КОРЗИНУ", Font = new Font("Segoe UI", 11, FontStyle.Bold), BackColor = Color.FromArgb(109, 76, 65), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Location = new Point(20, 290), Size = new Size(320, 40) };
            addBtn.FlatAppearance.BorderSize = 0;
            addBtn.Click += (s, e) =>
            {
                string syrupsText = "";
                var selectedSyrups = new List<Syrup>();
                for (int i = 0; i < syrupList.Items.Count; i++)
                {
                    if (syrupList.GetItemChecked(i))
                    {
                        selectedSyrups.Add(availableSyrups[i]);
                        syrupsText += (syrupsText == "" ? "" : ", ") + availableSyrups[i].Name;
                    }
                }
                var drinkWithSyrups = new CoffeeDrinkItem { Name = drink.Name, Description = drink.Description, BasePrice = drink.BasePrice, Emoji = drink.Emoji, AddedSyrups = selectedSyrups };
                AddToCart(drinkWithSyrups, syrupsText);
                dialog.Close();
            };
            dialog.Controls.Add(addBtn);
            dialog.ShowDialog();
        }

        private void EditSyrupsForCartItem(CartItem cartItem, CoffeeDrinkItem drink)
        {
            Form dialog = new Form { Text = $"✨ Изменить сиропы для {drink.Name}", Size = new Size(380, 380), StartPosition = FormStartPosition.CenterParent, BackColor = Color.FromArgb(255, 248, 240), FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false, ShowInTaskbar = false };
            Label infoLabel = new Label { Text = "Выберите сиропы:", Font = new Font("Segoe UI", 10), Location = new Point(20, 15), Size = new Size(330, 25) };
            dialog.Controls.Add(infoLabel);
            CheckedListBox syrupList = new CheckedListBox { Location = new Point(20, 45), Size = new Size(320, 180), Font = new Font("Segoe UI", 11), CheckOnClick = true };
            for (int i = 0; i < availableSyrups.Count; i++)
            {
                syrupList.Items.Add($"{availableSyrups[i].Name} (+{availableSyrups[i].Price:F0} ₽)");
                if (drink.AddedSyrups.Any(s => s.Name == availableSyrups[i].Name)) syrupList.SetItemChecked(i, true);
            }
            dialog.Controls.Add(syrupList);
            Label totalPriceLabel = new Label { Text = $"Текущая цена: {drink.GetTotalPrice():F2} ₽", Font = new Font("Segoe UI", 11, FontStyle.Bold), ForeColor = Color.FromArgb(78, 52, 46), Location = new Point(20, 235), Size = new Size(320, 25) };
            dialog.Controls.Add(totalPriceLabel);
            syrupList.ItemCheck += (s, e) =>
            {
                decimal totalPrice = drink.BasePrice;
                for (int i = 0; i < syrupList.Items.Count; i++)
                {
                    bool isChecked = (i == e.Index) ? (e.NewValue == CheckState.Checked) : syrupList.GetItemChecked(i);
                    if (isChecked) totalPrice += availableSyrups[i].Price;
                }
                totalPriceLabel.Text = $"Будет: {totalPrice:F2} ₽";
            };
            Button saveBtn = new Button { Text = "💾 СОХРАНИТЬ ИЗМЕНЕНИЯ", Font = new Font("Segoe UI", 11, FontStyle.Bold), BackColor = Color.FromArgb(109, 76, 65), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Location = new Point(20, 270), Size = new Size(320, 40) };
            saveBtn.FlatAppearance.BorderSize = 0;
            saveBtn.Click += (s, e) =>
            {
                drink.AddedSyrups.Clear();
                string syrupsText = "";
                for (int i = 0; i < syrupList.Items.Count; i++)
                {
                    if (syrupList.GetItemChecked(i))
                    {
                        drink.AddedSyrups.Add(availableSyrups[i]);
                        syrupsText += (syrupsText == "" ? "" : ", ") + availableSyrups[i].Name;
                    }
                }
                cartItem.SyrupsText = syrupsText;
                cartItem.ItemPrice = drink.GetTotalPrice();
                UpdateCart();
                dialog.Close();
                MessageBox.Show($"✅ Сиропы для {drink.Name} обновлены!", "Успешно", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };
            dialog.Controls.Add(saveBtn);
            Button cancelBtn = new Button { Text = "Отмена", Font = new Font("Segoe UI", 10), BackColor = Color.White, ForeColor = Color.FromArgb(109, 76, 65), FlatStyle = FlatStyle.Flat, Location = new Point(20, 320), Size = new Size(320, 30) };
            cancelBtn.FlatAppearance.BorderColor = Color.FromArgb(215, 204, 200); cancelBtn.FlatAppearance.BorderSize = 1;
            cancelBtn.Click += (s, e) => dialog.Close();
            dialog.Controls.Add(cancelBtn);
            dialog.ShowDialog();
        }

        private void AddToCart(CoffeeMenuItem item, string syrupsText)
        {
            decimal itemPrice = item.BasePrice;
            if (item is CoffeeDrinkItem drink) itemPrice = drink.GetTotalPrice();

            if (item is CoffeeDessertItem dessert)
            {
                int stock = DatabaseHelper.GetDessertStock(dessert.Name);
                int alreadyInCart = cart.Where(c => c.Item.Name == dessert.Name).Sum(c => c.Quantity);

                if (alreadyInCart >= stock)
                {
                    MessageBox.Show($"❌ Достигнут лимит! В наличии только {stock} шт. \"{dessert.Name}\"",
                                  "Ограничение", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }

            if (item is CoffeeDrinkItem)
            {
                var existing = cart.FirstOrDefault(c => c.Item.Name == item.Name && c.SyrupsText == syrupsText);
                if (existing != null) existing.Quantity++;
                else cart.Add(new CartItem { Item = item, Quantity = 1, SyrupsText = syrupsText, ItemPrice = itemPrice });
            }
            else
            {
                var existing = cart.FirstOrDefault(c => c.Item.Name == item.Name);
                if (existing != null)
                {
                    if (item is CoffeeDessertItem)
                    {
                        int stock = DatabaseHelper.GetDessertStock(item.Name);
                        if (existing.Quantity >= stock)
                        {
                            MessageBox.Show($"❌ Достигнут лимит! В наличии только {stock} шт. \"{item.Name}\"",
                                          "Ограничение", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            return;
                        }
                    }
                    existing.Quantity++;
                }
                else
                    cart.Add(new CartItem { Item = item, Quantity = 1, SyrupsText = syrupsText, ItemPrice = itemPrice });
            }
            UpdateCart();
        }

        private void UpdateCart()
        {
            cartItemsContainer.Controls.Clear();
            decimal total = 0;
            int yPosition = 5;
            foreach (var item in cart)
            {
                Panel itemPanel = new Panel { Location = new Point(5, yPosition), Size = new Size(290, 85), BackColor = Color.White, Margin = new Padding(0, 0, 0, 5) };
                itemPanel.Paint += (sender, e) => ControlPaint.DrawBorder(e.Graphics, (sender as Panel).ClientRectangle, Color.FromArgb(215, 204, 200), 1, ButtonBorderStyle.Solid, Color.FromArgb(215, 204, 200), 1, ButtonBorderStyle.Solid, Color.FromArgb(215, 204, 200), 1, ButtonBorderStyle.Solid, Color.FromArgb(215, 204, 200), 1, ButtonBorderStyle.Solid);
                string syrups = string.IsNullOrEmpty(item.SyrupsText) ? "" : $"\n+ {item.SyrupsText}";
                Label nameLabel = new Label { Text = $"{item.Item.Emoji} {item.Item.Name}{syrups}", Font = new Font("Segoe UI", 9, FontStyle.Bold), ForeColor = Color.FromArgb(78, 52, 46), Location = new Point(8, 5), Size = new Size(200, 45), AutoSize = false };
                itemPanel.Controls.Add(nameLabel);
                Label priceLabel = new Label { Text = $"{item.ItemPrice:F2} ₽/шт", Font = new Font("Segoe UI", 8), ForeColor = Color.FromArgb(109, 76, 65), TextAlign = ContentAlignment.TopRight, Location = new Point(210, 5), Size = new Size(75, 20) };
                itemPanel.Controls.Add(priceLabel);
                Panel controlPanel = new Panel { Location = new Point(8, 50), Size = new Size(275, 30), BackColor = Color.Transparent };
                Button decreaseBtn = new Button { Text = "−", Font = new Font("Segoe UI", 10, FontStyle.Bold), BackColor = Color.FromArgb(255, 243, 224), ForeColor = Color.FromArgb(109, 76, 65), FlatStyle = FlatStyle.Flat, Location = new Point(0, 0), Size = new Size(30, 30), Tag = item };
                decreaseBtn.FlatAppearance.BorderSize = 0;
                decreaseBtn.Click += (snd, ev) => { var ci = (snd as Button).Tag as CartItem; if (ci.Quantity > 1) ci.Quantity--; else if (MessageBox.Show($"Удалить \"{ci.Item.Name}\" из корзины?", "Подтверждение", MessageBoxButtons.YesNo) == DialogResult.Yes) cart.Remove(ci); UpdateCart(); };
                controlPanel.Controls.Add(decreaseBtn);
                Label quantityLabel = new Label { Text = item.Quantity.ToString(), Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = Color.FromArgb(78, 52, 46), TextAlign = ContentAlignment.MiddleCenter, Location = new Point(32, 0), Size = new Size(30, 30) };
                controlPanel.Controls.Add(quantityLabel);
                Button increaseBtn = new Button
                {
                    Text = "+",
                    Font = new Font("Segoe UI", 10, FontStyle.Bold),
                    BackColor = Color.FromArgb(255, 243, 224),
                    ForeColor = Color.FromArgb(109, 76, 65),
                    FlatStyle = FlatStyle.Flat,
                    Location = new Point(64, 0),
                    Size = new Size(30, 30),
                    Tag = item
                };
                increaseBtn.FlatAppearance.BorderSize = 0;
                increaseBtn.Click += (snd, ev) =>
                {
                    var ci = (snd as Button).Tag as CartItem;

                    if (ci.Item is CoffeeDessertItem)
                    {
                        int stock = DatabaseHelper.GetDessertStock(ci.Item.Name);
                        if (ci.Quantity >= stock)
                        {
                            MessageBox.Show($"❌ В наличии только {stock} шт. \"{ci.Item.Name}\"",
                                          "Лимит", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            return;
                        }
                    }

                    ci.Quantity++;
                    UpdateCart();
                };
                controlPanel.Controls.Add(increaseBtn);
                Label totalPriceLabel = new Label { Text = $"= {item.GetTotalPrice():F2} ₽", Font = new Font("Segoe UI", 9, FontStyle.Bold), ForeColor = Color.FromArgb(109, 76, 65), TextAlign = ContentAlignment.MiddleLeft, Location = new Point(100, 5), Size = new Size(100, 20) };
                controlPanel.Controls.Add(totalPriceLabel);
                itemPanel.Controls.Add(controlPanel);
                if (item.Item is CoffeeDrinkItem)
                {
                    Button editBtn = new Button { Text = "✎", Font = new Font("Segoe UI", 9), BackColor = Color.FromArgb(232, 245, 233), ForeColor = Color.FromArgb(56, 142, 60), FlatStyle = FlatStyle.Flat, Location = new Point(230, 50), Size = new Size(25, 25), Tag = item };
                    editBtn.FlatAppearance.BorderSize = 0;
                    editBtn.Click += (snd, ev) => { var ci = (snd as Button).Tag as CartItem; EditSyrupsForCartItem(ci, ci.Item as CoffeeDrinkItem); };
                    itemPanel.Controls.Add(editBtn);
                    Button deleteBtn = new Button { Text = "✕", Font = new Font("Segoe UI", 9, FontStyle.Bold), BackColor = Color.FromArgb(255, 235, 238), ForeColor = Color.FromArgb(198, 40, 40), FlatStyle = FlatStyle.Flat, Location = new Point(258, 50), Size = new Size(25, 25), Tag = item };
                    deleteBtn.FlatAppearance.BorderSize = 0;
                    deleteBtn.Click += (snd, ev) => { var ci = (snd as Button).Tag as CartItem; if (MessageBox.Show($"Удалить \"{ci.Item.Name}\"?", "", MessageBoxButtons.YesNo) == DialogResult.Yes) { cart.Remove(ci); UpdateCart(); } };
                    itemPanel.Controls.Add(deleteBtn);
                }
                else
                {
                    Button deleteBtn = new Button { Text = "✕", Font = new Font("Segoe UI", 9, FontStyle.Bold), BackColor = Color.FromArgb(255, 235, 238), ForeColor = Color.FromArgb(198, 40, 40), FlatStyle = FlatStyle.Flat, Location = new Point(244, 50), Size = new Size(40, 25), TextAlign = ContentAlignment.MiddleCenter, Tag = item };
                    deleteBtn.FlatAppearance.BorderSize = 0;
                    deleteBtn.Click += (snd, ev) => { var ci = (snd as Button).Tag as CartItem; if (MessageBox.Show($"Удалить \"{ci.Item.Name}\"?", "", MessageBoxButtons.YesNo) == DialogResult.Yes) { cart.Remove(ci); UpdateCart(); } };
                    itemPanel.Controls.Add(deleteBtn);
                }
                cartItemsContainer.Controls.Add(itemPanel);
                yPosition += 90;
                total += item.GetTotalPrice();
            }
            if (cart.Count == 0)
            {
                Label emptyLabel = new Label { Text = "🛒 Корзина пуста\n\nДобавьте что-нибудь вкусное!", Font = new Font("Segoe UI", 11), ForeColor = Color.FromArgb(141, 110, 99), TextAlign = ContentAlignment.MiddleCenter, Location = new Point(10, 100), Size = new Size(290, 100) };
                cartItemsContainer.Controls.Add(emptyLabel);
            }
            totalLabel.Text = $"{total:F2} ₽";
        }

        private void CheckoutButton_Click(object sender, EventArgs e)
        {
            if (cart.Count == 0) { MessageBox.Show("Корзина пуста"); return; }
            decimal total = cart.Sum(item => item.GetTotalPrice());

            if (!DatabaseHelper.TrySpend(currentUserId, total))
            {
                MessageBox.Show("Недостаточно средств на балансе!", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            foreach (var item in cart)
            {
                if (item.Item is CoffeeDrinkItem drink)
                {
                    for (int i = 0; i < item.Quantity; i++)
                    {
                        if (!DatabaseHelper.CheckAndConsumeIngredients(drink.Name))
                        {
                            MessageBox.Show($"Не удалось списать ингредиенты для \"{drink.Name}\".", "Ошибка");
                            DatabaseHelper.UpdateBalance(currentUserId, DatabaseHelper.GetBalance(currentUserId) + total);
                            return;
                        }
                    }
                }
                else if (item.Item is CoffeeDessertItem dessert)
                {
                    if (!DatabaseHelper.CheckAndConsumeDessert(dessert.Name, item.Quantity))
                    {
                        MessageBox.Show($"Не удалось списать десерт \"{dessert.Name}\".", "Ошибка");
                        DatabaseHelper.UpdateBalance(currentUserId, DatabaseHelper.GetBalance(currentUserId) + total);
                        return;
                    }
                }
            }

            int orderId = DatabaseHelper.CreateOrder(currentUserId, total);
            foreach (var item in cart)
                DatabaseHelper.AddOrderItem(orderId, item.Item.Name, item.Quantity, item.ItemPrice, item.SyrupsText);

            MessageBox.Show($"✨ Заказ №{orderId} оформлен!\nСумма: {total:F2} ₽", "Заказ", MessageBoxButtons.OK, MessageBoxIcon.Information);
            cart.Clear();
            UpdateCart();
            userBalanceLabel.Text = $"Баланс: {DatabaseHelper.GetBalance(currentUserId):F2} ₽";
            LoadMenuFromDatabase();
        }

        private void PrintReceiptButton_Click(object sender, EventArgs e)
        {
            if (cart.Count == 0) { MessageBox.Show("Корзина пуста"); return; }
            SaveFileDialog sfd = new SaveFileDialog { Filter = "Текстовый файл (*.txt)|*.txt", FileName = $"Чек_Кофейня_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.txt", InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop) };
            if (sfd.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    CreateTextReceipt(sfd.FileName);
                    MessageBox.Show($"Чек сохранён: {Path.GetFileName(sfd.FileName)}", "Успех", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex) { MessageBox.Show($"Ошибка: {ex.Message}"); }
            }
        }

        private void CreateTextReceipt(string filePath)
        {
            using (StreamWriter writer = new StreamWriter(filePath, false, System.Text.Encoding.UTF8))
            {
                writer.WriteLine("═══════════════════════════════════════════");
                writer.WriteLine("           ☕ УЮТНАЯ КОФЕЙНЯ ☕");
                writer.WriteLine("═══════════════════════════════════════════");
                writer.WriteLine($"Дата: {DateTime.Now:dd.MM.yyyy HH:mm}");
                writer.WriteLine("───────────────────────────────────────────");
                decimal total = 0; int num = 1;
                foreach (var item in cart)
                {
                    string syrups = string.IsNullOrEmpty(item.SyrupsText) ? "" : $"\n   (+ {item.SyrupsText})";
                    writer.WriteLine($"{num}. {item.Item.Emoji} {item.Item.Name}{syrups}");
                    writer.WriteLine($"   {item.Quantity} x {item.ItemPrice:F2} ₽ = {item.GetTotalPrice():F2} ₽");
                    total += item.GetTotalPrice(); num++;
                }
                writer.WriteLine("───────────────────────────────────────────");
                writer.WriteLine($"ИТОГО: {total:F2} ₽");
                writer.WriteLine("═══════════════════════════════════════════");
                writer.WriteLine("      Спасибо за заказ! Ждём вас снова!");
            }
        }

        private void ClearCartButton_Click(object sender, EventArgs e)
        {
            if (cart.Count > 0 && MessageBox.Show("Очистить корзину?", "", MessageBoxButtons.YesNo) == DialogResult.Yes) { cart.Clear(); UpdateCart(); }
        }

        private void LoadUserOrders(Panel ordersContainer)
        {
            ordersContainer.Controls.Clear();
            DataTable orders = DatabaseHelper.GetUserOrders(currentUserId);

            int yPosition = 10; 

            foreach (DataRow row in orders.Rows)
            {
                int orderId = Convert.ToInt32(row["Id"]);
                string status = row["Status"].ToString();
                Color statusColor;
                switch (status)
                {
                    case "Готов": statusColor = Color.LimeGreen; break;
                    case "Отменён": statusColor = Color.Red; break;
                    case "Готовится": statusColor = Color.Gold; break;
                    case "Завершён": statusColor = Color.Silver; break;
                    default: statusColor = Color.Gray; break;
                }

                Panel orderCard = new Panel
                {
                    Height = 120,
                    BackColor = Color.White,
                    BorderStyle = BorderStyle.FixedSingle,
                    Width = ordersContainer.Width - 25,
                    Location = new Point(10, yPosition), 
                    Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top
                };

                Label lblId = new Label
                {
                    Text = $"Заказ №{orderId}",
                    Font = new Font("Segoe UI", 10, FontStyle.Bold),
                    Location = new Point(10, 10),
                    AutoSize = true
                };

                Label lblDate = new Label
                {
                    Text = $"Дата: {Convert.ToDateTime(row["OrderDate"]).ToString("g")}",
                    Font = new Font("Segoe UI", 9),
                    Location = new Point(10, 35),
                    AutoSize = true
                };

                Label lblSum = new Label
                {
                    Text = $"Сумма: {Convert.ToDecimal(row["TotalAmount"]):F2} ₽",
                    Font = new Font("Segoe UI", 10, FontStyle.Bold),
                    Location = new Point(10, 60),
                    AutoSize = true
                };

                Panel statusDot = new Panel
                {
                    Size = new Size(12, 12),
                    Location = new Point(10, 85),
                    BackColor = statusColor
                };

                Label lblStatus = new Label
                {
                    Text = status,
                    Font = new Font("Segoe UI", 9, FontStyle.Bold),
                    Location = new Point(28, 83),
                    AutoSize = true,
                    ForeColor = statusColor
                };

                Button btnDetails = new Button
                {
                    Text = "📋 Состав заказа",
                    Location = new Point(200, 80),
                    Size = new Size(120, 30),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Color.FromArgb(232, 245, 233),
                    Font = new Font("Segoe UI", 8)
                };

                btnDetails.Click += (s, e) =>
                {
                    DataTable items = DatabaseHelper.GetOrderItems(orderId);
                    string details = $"Состав заказа №{orderId}:\n\n";
                    foreach (DataRow itemRow in items.Rows)
                    {
                        string name = itemRow["ProductName"].ToString();
                        int qty = Convert.ToInt32(itemRow["Quantity"]);
                        decimal price = Convert.ToDecimal(itemRow["ItemPrice"]);
                        string syrups = itemRow["SyrupsText"]?.ToString();
                        details += $"• {name} x{qty} = {price * qty:F2} ₽";
                        if (!string.IsNullOrEmpty(syrups))
                            details += $"\n  (+ {syrups})";
                        details += "\n\n";
                    }
                    if (items.Rows.Count == 0) details += "Нет позиций в заказе";
                    MessageBox.Show(details, $"Заказ №{orderId}", MessageBoxButtons.OK, MessageBoxIcon.Information);
                };

                orderCard.Controls.Add(lblId);
                orderCard.Controls.Add(lblDate);
                orderCard.Controls.Add(lblSum);
                orderCard.Controls.Add(statusDot);
                orderCard.Controls.Add(lblStatus);
                orderCard.Controls.Add(btnDetails);

                ordersContainer.Controls.Add(orderCard);

                yPosition += 130; 
            }

            if (orders.Rows.Count == 0)
            {
                Label emptyLabel = new Label
                {
                    Text = "📋 У вас пока нет заказов\n\nСделайте первый заказ!",
                    Font = new Font("Segoe UI", 12),
                    ForeColor = Color.FromArgb(141, 110, 99),
                    TextAlign = ContentAlignment.MiddleCenter,
                    Location = new Point(10, 100),
                    Size = new Size(ordersContainer.Width - 30, 100)
                };
                ordersContainer.Controls.Add(emptyLabel);
            }
        }

        private void BuildAdminPanel()
        {
            adminPanel = new Panel { Dock = DockStyle.Fill, Visible = false, BackColor = Color.FromArgb(255, 248, 240) };
            TableLayoutPanel mainLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, Padding = new Padding(10) };
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65));
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            adminPanel.Controls.Add(mainLayout);

            Panel topBar = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(109, 76, 65) };
            Button btnProducts = new Button { Text = "Товары", FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(109, 76, 65), ForeColor = Color.White, Width = 120, Height = 50, Location = new Point(0, 0) };
            Button btnIngredients = new Button { Text = "Склад", FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(109, 76, 65), ForeColor = Color.White, Width = 120, Height = 50, Location = new Point(120, 0) };
            Button btnOrders = new Button { Text = "Заказы", FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(109, 76, 65), ForeColor = Color.White, Width = 120, Height = 50, Location = new Point(240, 0) };
            Button btnUsers = new Button { Text = "Пользователи", FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(109, 76, 65), ForeColor = Color.White, Width = 140, Height = 50, Location = new Point(360, 0) };

            Button btnBack = new Button
            {
                Text = "Выйти",
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.White,
                ForeColor = Color.FromArgb(109, 76, 65),
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                Width = 80,
                Height = 50,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            btnBack.Location = new Point(topBar.Width - btnBack.Width - 5, 0);
            btnBack.Click += (s, e) =>
            {
                adminPanel.Visible = false;
                loginPanel.Visible = true;
                ResetLoginForm();
            };
            topBar.Controls.Add(btnProducts);
            topBar.Controls.Add(btnIngredients);
            topBar.Controls.Add(btnOrders);
            topBar.Controls.Add(btnUsers);
            topBar.Controls.Add(btnBack);
            topBar.Resize += (s, e) => { btnBack.Location = new Point(topBar.Width - btnBack.Width - 5, 0); };
            mainLayout.Controls.Add(topBar, 0, 0);
            mainLayout.SetColumnSpan(topBar, 2);

            adminLeftPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.FromArgb(255, 248, 240), Padding = new Padding(10), FlowDirection = FlowDirection.LeftToRight, WrapContents = true, AutoSize = false };
            adminRightPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(20) };
            mainLayout.Controls.Add(adminLeftPanel, 0, 1);
            mainLayout.Controls.Add(adminRightPanel, 1, 1);

            btnProducts.Click += (s, e) => ShowAdminTab("products");
            btnIngredients.Click += (s, e) => ShowAdminTab("ingredients");
            btnOrders.Click += (s, e) => ShowAdminTab("orders");
            btnUsers.Click += (s, e) => ShowAdminTab("users");
        }
        private void ResetLoginForm()
        {
            foreach (Control c in loginPanel.Controls)
            {
                if (c is RoundedPanel card)
                {
                    foreach (Control inner in card.Controls)
                    {
                        if (inner is TextBox txt)
                        {
                            txt.Text = ""; 
                        }
                        else if (inner is Label lbl)
                        {
                            if (lbl.ForeColor == Color.Red || lbl.ForeColor == Color.Green)
                            {
                                lbl.Text = "";
                                lbl.ForeColor = Color.Red; 
                            }
                        }
                    }
                }
            }

            currentUserId = -1;
            currentRole = null;

            cart.Clear();
            if (cartItemsContainer != null)
            {
                cartItemsContainer.Controls.Clear();
            }
            if (totalLabel != null)
            {
                totalLabel.Text = "0.00 ₽";
            }
        }
        private void ShowAdminTab(string tab)
        {
            adminLeftPanel.Controls.Clear();
            adminRightPanel.Controls.Clear();

            orderSearchBox = null;


            switch (tab)
            {
                case "products": LoadProductsView(); break;
                case "ingredients": LoadIngredientsView(); break;
                case "orders": LoadOrdersView(); break;
                case "users": LoadUsersView(); break;
            }
        }

        private void LoadProductsView()
        {
            var products = DatabaseHelper.LoadMenu();
            foreach (var item in products)
                adminLeftPanel.Controls.Add(CreateAdminProductCard(item));

            Label title = new Label { Text = "Управление товаром", Font = new Font("Segoe UI", 14, FontStyle.Bold), ForeColor = Color.FromArgb(78, 52, 46), Location = new Point(10, 10), AutoSize = true };
            Button btnAdd = new Button { Text = "➕ Добавить", Font = new Font("Segoe UI", 10), BackColor = Color.FromArgb(109, 76, 65), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Location = new Point(10, 60), Size = new Size(200, 40) };
            Button btnEdit = new Button { Text = "✏️ Изменить", Font = new Font("Segoe UI", 10), BackColor = Color.FromArgb(33, 150, 243), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Location = new Point(10, 120), Size = new Size(200, 40), Enabled = false };
            Button btnDelete = new Button { Text = "🗑️ Удалить", Font = new Font("Segoe UI", 10), BackColor = Color.FromArgb(244, 67, 54), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Location = new Point(10, 180), Size = new Size(200, 40), Enabled = false };

            btnAdd.Click += (s, e) =>
            {
                string name = Microsoft.VisualBasic.Interaction.InputBox("Название:", "");
                if (string.IsNullOrWhiteSpace(name)) return;
                string desc = Microsoft.VisualBasic.Interaction.InputBox("Описание:", "");
                decimal price = 0;
                while (true)
                {
                    string priceStr = Microsoft.VisualBasic.Interaction.InputBox("Цена:", "Добавление товара", "");
                    if (string.IsNullOrWhiteSpace(priceStr)) return;
                    if (decimal.TryParse(priceStr, out price)) break;
                    MessageBox.Show("Введите корректное число!", "Ошибка");
                }
                string type = Microsoft.VisualBasic.Interaction.InputBox("Тип (Drink/Dessert):", "Drink");
                string emoji = Microsoft.VisualBasic.Interaction.InputBox("Эмодзи:", "☕");
                DatabaseHelper.AddProduct(name, desc, price, type, emoji);
                ShowAdminTab("products");
            };

            btnEdit.Click += (s, e) =>
            {
                if (selectedAdminItem == null) return;
                string newName = Microsoft.VisualBasic.Interaction.InputBox("Новое название:", "", selectedAdminItem.Name);
                if (string.IsNullOrWhiteSpace(newName)) return;
                string newDesc = Microsoft.VisualBasic.Interaction.InputBox("Новое описание:", "", selectedAdminItem.Description);
                decimal newPrice = 0;
                while (true)
                {
                    string priceStr = Microsoft.VisualBasic.Interaction.InputBox("Новая цена:", "", selectedAdminItem.BasePrice.ToString());
                    if (string.IsNullOrWhiteSpace(priceStr)) return;
                    if (decimal.TryParse(priceStr, out newPrice)) break;
                    MessageBox.Show("Введите корректное число!", "Ошибка");
                }
                int productId = GetProductIdByName(selectedAdminItem.Name);
                DatabaseHelper.DeleteProduct(productId);
                DatabaseHelper.AddProduct(newName, newDesc, newPrice, selectedAdminItem is CoffeeDrinkItem ? "Drink" : "Dessert", selectedAdminItem.Emoji);
                ShowAdminTab("products");
            };

            btnDelete.Click += (s, e) =>
            {
                if (selectedAdminItem == null) return;
                if (MessageBox.Show($"Удалить \"{selectedAdminItem.Name}\"?", "Подтверждение", MessageBoxButtons.YesNo) == DialogResult.Yes)
                {
                    DatabaseHelper.DeleteProduct(GetProductIdByName(selectedAdminItem.Name));
                    ShowAdminTab("products");
                }
            };

            adminRightPanel.Controls.Add(title);
            adminRightPanel.Controls.Add(btnAdd);
            adminRightPanel.Controls.Add(btnEdit);
            adminRightPanel.Controls.Add(btnDelete);
        }

        private int GetProductIdByName(string name)
        {
            DataTable dt = DatabaseHelper.GetAllProducts();
            foreach (DataRow row in dt.Rows)
                if (row["Name"].ToString() == name) return Convert.ToInt32(row["Id"]);
            return -1;
        }

        private Panel CreateAdminProductCard(CoffeeMenuItem item)
        {
            RoundedPanel card = new RoundedPanel
            {
                Size = new Size(180, 180), 
                BackColor = Color.White,
                Margin = new Padding(10),
                Cursor = Cursors.Hand,
                BorderColor = Color.FromArgb(188, 170, 164),
                ShadowColor = Color.FromArgb(50, 0, 0, 0),
                CornerRadius = 12
            };

            Label emoji = new Label
            {
                Text = item.Emoji,
                Font = new Font("Segoe UI Emoji", 32), 
                TextAlign = ContentAlignment.MiddleCenter,
                Size = new Size(180, 60),
                Location = new Point(0, 5)
            };
            card.Controls.Add(emoji);

            Label name = new Label
            {
                Text = item.Name,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(5, 70),
                Size = new Size(170, 20),
                AutoEllipsis = true
            };
            card.Controls.Add(name);

            Label price = new Label
            {
                Text = $"{item.BasePrice:F2} ₽",
                Font = new Font("Segoe UI", 10),
                TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(5, 95),
                Size = new Size(170, 20)
            };
            card.Controls.Add(price);

            EventHandler clickHandler = (s, e) =>
            {
                foreach (Control c in adminLeftPanel.Controls)
                {
                    if (c is RoundedPanel rp && rp != card)
                    {
                        rp.BackColor = Color.White;
                        rp.BorderColor = Color.FromArgb(188, 170, 164);
                    }
                }
                card.BackColor = Color.FromArgb(255, 224, 178);
                card.BorderColor = Color.FromArgb(109, 76, 65);
                selectedAdminItem = item;
                foreach (Control ctrl in adminRightPanel.Controls)
                    if (ctrl is Button btn && (btn.Text.Contains("Изменить") || btn.Text.Contains("Удалить")))
                        btn.Enabled = true;
            };

            card.Click += clickHandler;
            emoji.Click += clickHandler;
            name.Click += clickHandler;
            price.Click += clickHandler;

            return card;
        }

        private void LoadIngredientsView()
        {
            DataTable ings = DatabaseHelper.GetIngredients();

            Button addIngredientBtn = new Button
            {
                Text = "➕ Добавить ингредиент на склад",
                Width = 280,
                Height = 40,
                BackColor = Color.FromArgb(56, 142, 60),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                Margin = new Padding(10)
            };
            addIngredientBtn.Click += (s, e) =>
            {
                string ingredientName = Microsoft.VisualBasic.Interaction.InputBox("Название ингредиента:", "Новый ингредиент", "");
                if (string.IsNullOrWhiteSpace(ingredientName)) return;

                string unit = Microsoft.VisualBasic.Interaction.InputBox("Единица измерения (г, мл, шт):", "", "шт");
                if (string.IsNullOrWhiteSpace(unit)) return;

                string quantityStr = Microsoft.VisualBasic.Interaction.InputBox("Начальное количество:", "", "0");
                if (!double.TryParse(quantityStr, out double quantity) || quantity < 0)
                {
                    MessageBox.Show("Введите неотрицательное число!", "Ошибка");
                    return;
                }

                using (var conn = DatabaseHelper.GetConnection())
                {
                    var checkCmd = new SQLiteCommand("SELECT COUNT(*) FROM Ingredients WHERE Name = @name", conn);
                    checkCmd.Parameters.AddWithValue("@name", ingredientName);
                    int exists = Convert.ToInt32(checkCmd.ExecuteScalar());

                    if (exists > 0)
                    {
                        MessageBox.Show("Такой ингредиент уже существует!", "Ошибка");
                        return;
                    }

                    var cmd = new SQLiteCommand("INSERT INTO Ingredients (Name, Unit, StockQuantity) VALUES (@n, @u, @q)", conn);
                    cmd.Parameters.AddWithValue("@n", ingredientName);
                    cmd.Parameters.AddWithValue("@u", unit);
                    cmd.Parameters.AddWithValue("@q", quantity);
                    cmd.ExecuteNonQuery();
                }

                ShowAdminTab("ingredients");
                MessageBox.Show($"✅ Ингредиент \"{ingredientName}\" добавлен!", "Успех");
            };
            adminLeftPanel.Controls.Add(addIngredientBtn);

            foreach (DataRow row in ings.Rows)
            {
                Panel card = new Panel
                {
                    Width = 280,
                    Height = 150,
                    BackColor = Color.White,
                    Margin = new Padding(10),
                    Tag = row["Id"],
                    BorderStyle = BorderStyle.FixedSingle
                };

                Label name = new Label
                {
                    Text = $"{row["Name"]} ({row["Unit"]})",
                    Font = new Font("Segoe UI", 10, FontStyle.Bold),
                    Location = new Point(10, 10),
                    Size = new Size(250, 20),
                    AutoEllipsis = true
                };

                Label stockLabel = new Label
                {
                    Text = $"Остаток: {row["StockQuantity"]}",
                    Location = new Point(10, 40),
                    AutoSize = true
                };

                Button increaseBtn = new Button
                {
                    Text = "+ Пополнить",
                    Location = new Point(10, 70),
                    Size = new Size(120, 28),
                    BackColor = Color.FromArgb(232, 245, 233),
                    ForeColor = Color.FromArgb(27, 94, 32),
                    FlatStyle = FlatStyle.Flat,
                    Font = new Font("Segoe UI", 8, FontStyle.Bold)
                };
                increaseBtn.Click += (s, e) =>
                {
                    Button btn = s as Button;
                    Panel parentCard = btn.Parent as Panel;
                    int ingredientId = Convert.ToInt32(parentCard.Tag);

                    string input = Microsoft.VisualBasic.Interaction.InputBox("Количество для пополнения:", "Пополнить склад", "");
                    if (double.TryParse(input, out double qty) && qty > 0)
                    {
                        DatabaseHelper.UpdateIngredientStock(ingredientId, qty);
                        ShowAdminTab("ingredients");
                        MessageBox.Show($"✅ Склад пополнен на {qty} {row["Unit"]}", "Успех");
                    }
                    else if (!string.IsNullOrEmpty(input))
                    {
                        MessageBox.Show("Введите положительное число!", "Ошибка");
                    }
                };

                Button decreaseBtn = new Button
                {
                    Text = "- Списать",
                    Location = new Point(140, 70),
                    Size = new Size(120, 28),
                    BackColor = Color.FromArgb(255, 235, 238),
                    ForeColor = Color.FromArgb(198, 40, 40),
                    FlatStyle = FlatStyle.Flat,
                    Font = new Font("Segoe UI", 8, FontStyle.Bold)
                };
                decreaseBtn.Click += (s, e) =>
                {
                    Button btn = s as Button;
                    Panel parentCard = btn.Parent as Panel;
                    int ingredientId = Convert.ToInt32(parentCard.Tag);

                    double currentStock = Convert.ToDouble(row["StockQuantity"]);

                    if (currentStock <= 0)
                    {
                        MessageBox.Show("❌ На складе ничего нет! Нечего списывать.", "Пустой склад",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    string input = Microsoft.VisualBasic.Interaction.InputBox(
                        $"Текущий остаток: {currentStock} {row["Unit"]}\nВведите количество для списания:",
                        "Списать со склада", "");

                    if (double.TryParse(input, out double qty) && qty > 0)
                    {
                        if (qty > currentStock)
                        {
                            MessageBox.Show($"❌ Нельзя списать больше, чем есть на складе! (максимум: {currentStock})",
                                "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            return;
                        }

                        DatabaseHelper.UpdateIngredientStock(ingredientId, -qty);
                        ShowAdminTab("ingredients");
                        MessageBox.Show($"✅ Списано {qty} {row["Unit"]}. Осталось: {currentStock - qty}", "Успех");
                    }
                    else if (!string.IsNullOrEmpty(input))
                    {
                        MessageBox.Show("Введите положительное число!", "Ошибка");
                    }
                };

                Button setExactBtn = new Button
                {
                    Text = "✏ Точное кол-во",
                    Location = new Point(10, 105),
                    Size = new Size(120, 28),
                    BackColor = Color.FromArgb(255, 243, 224),
                    ForeColor = Color.FromArgb(109, 76, 65),
                    FlatStyle = FlatStyle.Flat,
                    Font = new Font("Segoe UI", 8, FontStyle.Bold)
                };
                setExactBtn.Click += (s, e) =>
                {
                    Button btn = s as Button;
                    Panel parentCard = btn.Parent as Panel;
                    int ingredientId = Convert.ToInt32(parentCard.Tag);

                    double currentStock = Convert.ToDouble(row["StockQuantity"]);

                    string input = Microsoft.VisualBasic.Interaction.InputBox(
                        $"Текущий остаток: {currentStock} {row["Unit"]}\nВведите точное количество:",
                        "Установить точное количество", currentStock.ToString());

                    if (double.TryParse(input, out double newQty) && newQty >= 0)
                    {
                        // Вычисляем разницу
                        double difference = newQty - currentStock;
                        DatabaseHelper.UpdateIngredientStock(ingredientId, difference);
                        ShowAdminTab("ingredients");
                        MessageBox.Show($"✅ Количество обновлено: {newQty} {row["Unit"]}", "Успех");
                    }
                    else if (!string.IsNullOrEmpty(input))
                    {
                        MessageBox.Show("Введите неотрицательное число!", "Ошибка");
                    }
                };

                Button removeBtn = new Button
                {
                    Text = "🗑 Удалить",
                    Location = new Point(140, 105),
                    Size = new Size(120, 28),
                    BackColor = Color.FromArgb(255, 235, 238),
                    ForeColor = Color.FromArgb(198, 40, 40),
                    FlatStyle = FlatStyle.Flat,
                    Font = new Font("Segoe UI", 8, FontStyle.Bold)
                };
                removeBtn.Click += (s, e) =>
                {
                    Button btn = s as Button;
                    Panel parentCard = btn.Parent as Panel;
                    int ingredientId = Convert.ToInt32(parentCard.Tag);
                    string ingredientName = row["Name"].ToString();

                    if (MessageBox.Show($"Удалить ингредиент \"{ingredientName}\" со склада?\nЭто действие нельзя отменить!",
                        "Подтверждение удаления", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                    {
                        using (var conn = DatabaseHelper.GetConnection())
                        {
                            var cmd = new SQLiteCommand("DELETE FROM Ingredients WHERE Id = @id", conn);
                            cmd.Parameters.AddWithValue("@id", ingredientId);
                            cmd.ExecuteNonQuery();
                        }
                        ShowAdminTab("ingredients");
                        MessageBox.Show($"🗑 Ингредиент \"{ingredientName}\" удалён со склада", "Удалено");
                    }
                };

                card.Controls.Add(name);
                card.Controls.Add(stockLabel);
                card.Controls.Add(increaseBtn);
                card.Controls.Add(decreaseBtn);
                card.Controls.Add(setExactBtn);
                card.Controls.Add(removeBtn);
                adminLeftPanel.Controls.Add(card);
            }
        }

        private void LoadOrdersView()
        {
            adminRightPanel.Controls.Clear();

            Label searchTitle = new Label
            {
                Text = "🔍 Поиск заказа",
                Font = new Font("Segoe UI", 14, FontStyle.Bold),
                ForeColor = Color.FromArgb(78, 52, 46),
                Location = new Point(10, 10),
                AutoSize = true
            };
            adminRightPanel.Controls.Add(searchTitle);

            orderSearchBox = new TextBox
            {
                Location = new Point(10, 50),
                Width = 250,
                Height = 30,
                Font = new Font("Segoe UI", 12),
                Text = "Введите номер заказа...",
                ForeColor = Color.Gray
            };

            orderSearchBox.Enter += (s, e) =>
            {
                if (orderSearchBox.Text == "Введите номер заказа...")
                {
                    orderSearchBox.Text = "";
                    orderSearchBox.ForeColor = Color.Black;
                }
            };

            orderSearchBox.Leave += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(orderSearchBox.Text))
                {
                    orderSearchBox.Text = "Введите номер заказа...";
                    orderSearchBox.ForeColor = Color.Gray;
                }
            };

            orderSearchBox.TextChanged += (s, e) => RefreshOrdersList();
            adminRightPanel.Controls.Add(orderSearchBox);

            Button searchBtn = new Button
            {
                Text = "🔍 Найти",
                Location = new Point(10, 90),
                Size = new Size(120, 35),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(109, 76, 65),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10, FontStyle.Bold)
            };
            searchBtn.Click += (s, e) => RefreshOrdersList();
            adminRightPanel.Controls.Add(searchBtn);

            Button resetBtn = new Button
            {
                Text = "🔄 Сбросить",
                Location = new Point(140, 90),
                Size = new Size(120, 35),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.White,
                ForeColor = Color.FromArgb(109, 76, 65),
                Font = new Font("Segoe UI", 10)
            };
            resetBtn.FlatAppearance.BorderColor = Color.FromArgb(109, 76, 65);
            resetBtn.FlatAppearance.BorderSize = 1;
            resetBtn.Click += (s, e) =>
            {
                orderSearchBox.Text = "Введите номер заказа...";
                orderSearchBox.ForeColor = Color.Gray;
                RefreshOrdersList();
            };
            adminRightPanel.Controls.Add(resetBtn);

            Label statsLabel = new Label
            {
                Text = "📊 Статистика",
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                ForeColor = Color.FromArgb(78, 52, 46),
                Location = new Point(10, 145),
                AutoSize = true
            };
            adminRightPanel.Controls.Add(statsLabel);

            DataTable allOrders = DatabaseHelper.GetOrders();
            int totalOrders = allOrders.Rows.Count;
            int newOrders = allOrders.AsEnumerable().Count(r => r["Status"].ToString() == "Новый");
            int inProgress = allOrders.AsEnumerable().Count(r => r["Status"].ToString() == "Готовится");
            int completed = allOrders.AsEnumerable().Count(r => r["Status"].ToString() == "Готов");
            int cancelled = allOrders.AsEnumerable().Count(r => r["Status"].ToString() == "Отменён");

            Label totalStatLabel = new Label
            {
                Text = $"Всего заказов: {totalOrders}",
                Font = new Font("Segoe UI", 9),
                Location = new Point(10, 175),
                AutoSize = true,
                Tag = "totalStat"
            };
            adminRightPanel.Controls.Add(totalStatLabel);

            Label newStatLabel = new Label
            {
                Text = $"🆕 Новых: {newOrders}",
                Font = new Font("Segoe UI", 9),
                Location = new Point(10, 195),
                AutoSize = true,
                Tag = "newStat"
            };
            adminRightPanel.Controls.Add(newStatLabel);

            Label progressStatLabel = new Label
            {
                Text = $"⏳ Готовятся: {inProgress}",
                Font = new Font("Segoe UI", 9),
                Location = new Point(10, 215),
                AutoSize = true,
                Tag = "progressStat"
            };
            adminRightPanel.Controls.Add(progressStatLabel);

            Label doneStatLabel = new Label
            {
                Text = $"✅ Готовы: {completed}",
                Font = new Font("Segoe UI", 9),
                Location = new Point(10, 235),
                AutoSize = true,
                Tag = "doneStat"
            };
            adminRightPanel.Controls.Add(doneStatLabel);

            Label cancelledStatLabel = new Label
            {
                Text = $"❌ Отменены: {cancelled}",
                Font = new Font("Segoe UI", 9),
                Location = new Point(10, 255),
                AutoSize = true,
                Tag = "cancelledStat"
            };
            adminRightPanel.Controls.Add(cancelledStatLabel);

            adminLeftPanel.Controls.Clear();
            RefreshOrdersList();
        }

        private void RefreshOrdersList()
        {
            if (adminLeftPanel == null || adminLeftPanel.IsDisposed) return;

            List<Control> toRemove = new List<Control>();
            foreach (Control ctrl in adminLeftPanel.Controls)
            {
                if (ctrl is Panel p && p.Tag is int)
                {
                    toRemove.Add(ctrl);
                }
                else if (ctrl is Label lbl && lbl.Text.Contains("Заказы"))
                {
                    toRemove.Add(ctrl);
                }
            }
            foreach (Control ctrl in toRemove)
            {
                adminLeftPanel.Controls.Remove(ctrl);
                ctrl.Dispose();
            }

            DataTable orders = DatabaseHelper.GetOrders();
            string searchText = orderSearchBox?.Text.Trim() ?? "";

            if (searchText == "Введите номер заказа..." || string.IsNullOrEmpty(searchText))
            {
                searchText = "";
            }

            int foundCount = 0;

            foreach (DataRow row in orders.Rows)
            {
                int orderId = Convert.ToInt32(row["Id"]);

                if (!string.IsNullOrEmpty(searchText) && !orderId.ToString().Contains(searchText))
                {
                    continue;
                }

                string status = row["Status"].ToString();
                Panel card = CreateOrderCard(orderId, row["Username"].ToString(),
                    Convert.ToDateTime(row["OrderDate"]), Convert.ToDecimal(row["TotalAmount"]), status);

                adminLeftPanel.Controls.Add(card);
                foundCount++;
            }

            if (foundCount == 0)
            {
                Label noResults = new Label
                {
                    Text = string.IsNullOrEmpty(searchText)
                        ? "📋 Нет заказов"
                        : $"🔍 Заказы с номером \"{searchText}\" не найдены",
                    Font = new Font("Segoe UI", 11),
                    ForeColor = Color.FromArgb(141, 110, 99),
                    TextAlign = ContentAlignment.MiddleCenter,
                    AutoSize = true,
                    Margin = new Padding(20)
                };
                adminLeftPanel.Controls.Add(noResults);
            }

            UpdateOrderStats(orders);
        }

        private void UpdateOrderStats(DataTable orders)
        {
            int totalOrders = orders.Rows.Count;
            int newOrders = orders.AsEnumerable().Count(r => r["Status"].ToString() == "Новый");
            int inProgress = orders.AsEnumerable().Count(r => r["Status"].ToString() == "Готовится");
            int completed = orders.AsEnumerable().Count(r => r["Status"].ToString() == "Готов");
            int cancelled = orders.AsEnumerable().Count(r => r["Status"].ToString() == "Отменён");

            foreach (Control ctrl in adminRightPanel.Controls)
            {
                if (ctrl is Label label && label.Tag != null)
                {
                    switch (label.Tag.ToString())
                    {
                        case "totalStat": label.Text = $"Всего заказов: {totalOrders}"; break;
                        case "newStat": label.Text = $"🆕 Новых: {newOrders}"; break;
                        case "progressStat": label.Text = $"⏳ Готовятся: {inProgress}"; break;
                        case "doneStat": label.Text = $"✅ Готовы: {completed}"; break;
                        case "cancelledStat": label.Text = $"❌ Отменены: {cancelled}"; break;
                    }
                }
            }
        }

        private Panel CreateOrderCard(int orderId, string username, DateTime orderDate, decimal totalAmount, string status)
        {
            Color statusColor;
            switch (status)
            {
                case "Готов": statusColor = Color.LimeGreen; break;
                case "Отменён": statusColor = Color.Red; break;
                case "Готовится": statusColor = Color.Gold; break;
                case "Завершён": statusColor = Color.Silver; break;
                default: statusColor = Color.Gray; break;
            }

            Panel card = new Panel
            {
                Width = 320,
                Height = 145,
                BackColor = Color.White,
                Margin = new Padding(8),
                Tag = orderId,
                BorderStyle = BorderStyle.FixedSingle
            };

            Panel statusStrip = new Panel
            {
                Width = 5,
                Height = 145,
                BackColor = statusColor,
                Location = new Point(0, 0)
            };
            card.Controls.Add(statusStrip);

            Label userLabel = new Label
            {
                Text = $"👤 {username}",
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                Location = new Point(12, 5),
                AutoSize = true
            };

            Label idLabel = new Label
            {
                Text = $"📋 Заказ №{orderId}",
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                ForeColor = Color.FromArgb(109, 76, 65),
                Location = new Point(12, 25),
                AutoSize = true
            };

            Label dateLabel = new Label
            {
                Text = $"📅 {orderDate:dd.MM.yyyy HH:mm}",
                Font = new Font("Segoe UI", 7),
                Location = new Point(12, 50),
                AutoSize = true
            };

            Label totalLabel = new Label
            {
                Text = $"💰 {totalAmount:F2} ₽",
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Location = new Point(12, 70),
                AutoSize = true
            };

            Panel dot = new Panel
            {
                Size = new Size(12, 12),
                Location = new Point(12, 100),
                BackColor = statusColor,
                Tag = "statusDot"
            };

            Label statusLabel = new Label
            {
                Text = status,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                Location = new Point(28, 98),
                AutoSize = true,
                ForeColor = statusColor,
                Tag = "statusLabel"
            };

            Button changeBtn = new Button
            {
                Text = "Сменить статус ▼",
                Location = new Point(180, 93),
                Size = new Size(125, 28),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(255, 243, 224),
                ForeColor = Color.FromArgb(109, 76, 65),
                Font = new Font("Segoe UI", 8, FontStyle.Bold),
                Tag = status
            };

            changeBtn.Click += (s, e) =>
            {
                try
                {
                    Button btn = s as Button;
                    if (btn == null || btn.IsDisposed || btn.Parent == null) return;

                    Panel parentCard = btn.Parent as Panel;
                    if (parentCard == null || parentCard.IsDisposed) return;

                    string currentStatus = btn.Tag?.ToString() ?? "Новый";
                    int orderIdFromCard = (int)parentCard.Tag;

                    ContextMenuStrip statusMenu = new ContextMenuStrip();

                    string[] allStatuses = { "Новый", "Готовится", "Готов", "Завершён", "Отменён" };

                    for (int i = 0; i < allStatuses.Length; i++)
                    {
                        string newStatus = allStatuses[i];
                        ToolStripMenuItem item = new ToolStripMenuItem(newStatus);

                        if (newStatus == currentStatus)
                        {
                            item.Font = new Font("Segoe UI", 9, FontStyle.Bold);
                            item.Text = $"✓ {newStatus}";
                        }

                        int capturedOrderId = orderIdFromCard;
                        Button capturedBtn = btn;
                        Panel capturedCard = parentCard;

                        item.Click += (ss, ee) =>
                        {
                            try
                            {
                                if (capturedBtn == null || capturedBtn.IsDisposed) return;

                                DatabaseHelper.UpdateOrderStatus(capturedOrderId, newStatus);

                                if (capturedCard != null && !capturedCard.IsDisposed)
                                {
                                    UpdateOrderCardStatus(capturedCard, newStatus);
                                }

                                capturedBtn.Tag = newStatus;
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show($"Ошибка обновления статуса: {ex.Message}", "Ошибка");
                            }
                        };

                        statusMenu.Items.Add(item);
                    }

                    statusMenu.Show(btn, new Point(0, btn.Height));
                }
                catch (Exception ex)
                {
                    if (!this.IsDisposed)
                    {
                        MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка");
                    }
                }
            };

            card.Controls.Add(userLabel);
            card.Controls.Add(idLabel);
            card.Controls.Add(dateLabel);
            card.Controls.Add(totalLabel);
            card.Controls.Add(dot);
            card.Controls.Add(statusLabel);
            card.Controls.Add(changeBtn);

            return card;
        }

        private void UpdateOrderCardStatus(Panel card, string newStatus)
        {
            Color newColor;
            switch (newStatus)
            {
                case "Готов": newColor = Color.LimeGreen; break;
                case "Отменён": newColor = Color.Red; break;
                case "Готовится": newColor = Color.Gold; break;
                case "Завершён": newColor = Color.Silver; break;
                default: newColor = Color.Gray; break;
            }

            foreach (Control control in card.Controls)
            {
                if (control.Tag?.ToString() == "statusDot" && control is Panel dot)
                {
                    dot.BackColor = newColor;
                }
                else if (control.Tag?.ToString() == "statusLabel" && control is Label label)
                {
                    label.Text = newStatus;
                    label.ForeColor = newColor;
                }
            }
        }

        private void LoadUsersView()
        {
            adminRightPanel.Controls.Clear();

            Label searchTitle = new Label
            {
                Text = "🔍 Поиск пользователя",
                Font = new Font("Segoe UI", 14, FontStyle.Bold),
                ForeColor = Color.FromArgb(78, 52, 46),
                Location = new Point(10, 10),
                AutoSize = true
            };
            adminRightPanel.Controls.Add(searchTitle);

            TextBox userSearchBox = new TextBox
            {
                Location = new Point(10, 50),
                Width = 250,
                Height = 30,
                Font = new Font("Segoe UI", 12),
                Text = "Введите имя пользователя...",
                ForeColor = Color.Gray,
                Tag = "userSearch"
            };

            userSearchBox.Enter += (s, e) =>
            {
                if (userSearchBox.Text == "Введите имя пользователя...")
                {
                    userSearchBox.Text = "";
                    userSearchBox.ForeColor = Color.Black;
                }
            };

            userSearchBox.Leave += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(userSearchBox.Text))
                {
                    userSearchBox.Text = "Введите имя пользователя...";
                    userSearchBox.ForeColor = Color.Gray;
                }
            };

            userSearchBox.TextChanged += (s, e) => RefreshUsersList(userSearchBox.Text);
            adminRightPanel.Controls.Add(userSearchBox);

            Button searchBtn = new Button
            {
                Text = "🔍 Найти",
                Location = new Point(10, 90),
                Size = new Size(120, 35),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(109, 76, 65),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10, FontStyle.Bold)
            };
            searchBtn.Click += (s, e) => RefreshUsersList(userSearchBox.Text);
            adminRightPanel.Controls.Add(searchBtn);

            Button resetBtn = new Button
            {
                Text = "🔄 Сбросить",
                Location = new Point(140, 90),
                Size = new Size(120, 35),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.White,
                ForeColor = Color.FromArgb(109, 76, 65),
                Font = new Font("Segoe UI", 10)
            };
            resetBtn.FlatAppearance.BorderColor = Color.FromArgb(109, 76, 65);
            resetBtn.FlatAppearance.BorderSize = 1;
            resetBtn.Click += (s, e) =>
            {
                userSearchBox.Text = "Введите имя пользователя...";
                userSearchBox.ForeColor = Color.Gray;
                RefreshUsersList("");
            };
            adminRightPanel.Controls.Add(resetBtn);

            Button addUserBtn = new Button
            {
                Text = "➕ Добавить пользователя",
                Location = new Point(10, 140),
                Size = new Size(250, 35),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(56, 142, 60),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9, FontStyle.Bold)
            };
            addUserBtn.Click += (s, e) =>
            {
                string newUsername = Microsoft.VisualBasic.Interaction.InputBox("Имя пользователя:", "Новый пользователь", "");
                if (string.IsNullOrWhiteSpace(newUsername)) return;

                string newPassword = Microsoft.VisualBasic.Interaction.InputBox("Пароль:", "Новый пользователь", "");
                if (string.IsNullOrWhiteSpace(newPassword)) return;

                if (DatabaseHelper.RegisterUser(newUsername, newPassword))
                {
                    RefreshUsersList("");
                    MessageBox.Show($"✅ Пользователь \"{newUsername}\" создан!", "Успех");
                }
                else
                {
                    MessageBox.Show("❌ Пользователь с таким именем уже существует!", "Ошибка");
                }
            };
            adminRightPanel.Controls.Add(addUserBtn);

            Label statsLabel = new Label
            {
                Text = "📊 Статистика",
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                ForeColor = Color.FromArgb(78, 52, 46),
                Location = new Point(10, 195),
                AutoSize = true
            };
            adminRightPanel.Controls.Add(statsLabel);

            DataTable allUsers = DatabaseHelper.GetUsers();
            int totalUsers = allUsers.Rows.Count;
            int adminCount = allUsers.AsEnumerable().Count(r => r["Role"].ToString() == "admin");
            int userCount = allUsers.AsEnumerable().Count(r => r["Role"].ToString() == "user");
            decimal totalBalance = allUsers.AsEnumerable().Sum(r => Convert.ToDecimal(r["Balance"]));

            Label totalUsersLabel = new Label
            {
                Text = $"👥 Всего: {totalUsers}",
                Font = new Font("Segoe UI", 9),
                Location = new Point(10, 225),
                AutoSize = true
            };
            adminRightPanel.Controls.Add(totalUsersLabel);

            Label adminCountLabel = new Label
            {
                Text = $"👑 Админов: {adminCount}",
                Font = new Font("Segoe UI", 9),
                ForeColor = Color.FromArgb(255, 87, 34),
                Location = new Point(10, 245),
                AutoSize = true
            };
            adminRightPanel.Controls.Add(adminCountLabel);

            Label userCountLabel = new Label
            {
                Text = $"👤 Пользователей: {userCount}",
                Font = new Font("Segoe UI", 9),
                ForeColor = Color.FromArgb(33, 150, 243),
                Location = new Point(10, 265),
                AutoSize = true
            };
            adminRightPanel.Controls.Add(userCountLabel);

            Label totalBalanceLabel = new Label
            {
                Text = $"💰 Общий баланс: {totalBalance:F2} ₽",
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                Location = new Point(10, 285),
                AutoSize = true
            };
            adminRightPanel.Controls.Add(totalBalanceLabel);

            adminLeftPanel.Controls.Clear();
            RefreshUsersList("");
        }

        private void RefreshUsersList(string searchText)
        {
            List<Control> toRemove = new List<Control>();
            foreach (Control ctrl in adminLeftPanel.Controls)
            {
                if (ctrl is Panel p && p.Tag is int)
                {
                    toRemove.Add(ctrl);
                }
                else if (ctrl is Label lbl && (lbl.Text.Contains("пользовател") || lbl.Text.Contains("Пользовател")))
                {
                    toRemove.Add(ctrl);
                }
            }
            foreach (Control ctrl in toRemove)
            {
                adminLeftPanel.Controls.Remove(ctrl);
                ctrl.Dispose();
            }

            DataTable users = DatabaseHelper.GetUsers();

            if (searchText == "Введите имя пользователя..." || string.IsNullOrWhiteSpace(searchText))
            {
                searchText = "";
            }

            int foundCount = 0;

            foreach (DataRow row in users.Rows)
            {
                int userId = Convert.ToInt32(row["Id"]);
                string username = row["Username"].ToString();
                string role = row["Role"].ToString();
                decimal balance = Convert.ToDecimal(row["Balance"]);

                if (!string.IsNullOrEmpty(searchText) && !username.ToLower().Contains(searchText.ToLower()))
                {
                    continue;
                }

                Panel card = CreateUserCard(userId, username, role, balance);
                adminLeftPanel.Controls.Add(card);
                foundCount++;
            }

            if (foundCount == 0)
            {
                Label noResults = new Label
                {
                    Text = string.IsNullOrEmpty(searchText)
                        ? "👥 Нет пользователей"
                        : $"🔍 Пользователи \"{searchText}\" не найдены",
                    Font = new Font("Segoe UI", 11),
                    ForeColor = Color.FromArgb(141, 110, 99),
                    TextAlign = ContentAlignment.MiddleCenter,
                    AutoSize = true,
                    Margin = new Padding(20)
                };
                adminLeftPanel.Controls.Add(noResults);
            }
        }

        private Panel CreateUserCard(int userId, string username, string role, decimal balance)
        {
            Color roleColor = role == "admin" ? Color.FromArgb(255, 87, 34) : Color.FromArgb(33, 150, 243);
            string roleIcon = role == "admin" ? "👑" : "👤";

            Panel card = new Panel
            {
                Width = 290,
                Height = 175,
                BackColor = Color.White,
                Margin = new Padding(8),
                Tag = userId,
                BorderStyle = BorderStyle.FixedSingle
            };

            Panel roleStrip = new Panel
            {
                Width = 290,
                Height = 4,
                BackColor = roleColor,
                Location = new Point(0, 0)
            };
            card.Controls.Add(roleStrip);

            Panel avatar = new Panel
            {
                Size = new Size(45, 45),
                Location = new Point(15, 15),
                BackColor = roleColor
            };
            System.Drawing.Drawing2D.GraphicsPath avatarPath = new System.Drawing.Drawing2D.GraphicsPath();
            avatarPath.AddEllipse(0, 0, 44, 44);
            avatar.Region = new Region(avatarPath);

            Label avatarText = new Label
            {
                Text = username.Length > 0 ? username[0].ToString().ToUpper() : "?",
                Font = new Font("Segoe UI", 16, FontStyle.Bold),
                ForeColor = Color.White,
                TextAlign = ContentAlignment.MiddleCenter,
                Dock = DockStyle.Fill
            };
            avatar.Controls.Add(avatarText);
            card.Controls.Add(avatar);

            Label nameLabel = new Label
            {
                Text = username,
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                Location = new Point(75, 12),
                AutoSize = true
            };
            card.Controls.Add(nameLabel);

            Label roleLabel = new Label
            {
                Text = $"{roleIcon} {role}",
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = roleColor,
                Location = new Point(75, 35),
                AutoSize = true
            };
            card.Controls.Add(roleLabel);

            Label separator = new Label
            {
                Text = "─────────────────────────",
                Font = new Font("Segoe UI", 7),
                ForeColor = Color.FromArgb(200, 200, 200),
                Location = new Point(10, 68),
                AutoSize = true
            };
            card.Controls.Add(separator);

            Label balanceTitle = new Label
            {
                Text = "💰 Баланс:",
                Font = new Font("Segoe UI", 8),
                ForeColor = Color.FromArgb(141, 110, 99),
                Location = new Point(15, 85),
                AutoSize = true
            };
            card.Controls.Add(balanceTitle);

            Label balanceLabel = new Label
            {
                Text = $"{balance:F2} ₽",
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                ForeColor = balance > 0 ? Color.FromArgb(56, 142, 60) : Color.FromArgb(198, 40, 40),
                Location = new Point(15, 100),
                AutoSize = true
            };
            card.Controls.Add(balanceLabel);

            Button changeRoleBtn = new Button
            {
                Text = role == "admin" ? "⬇ В пользователи" : "⬆ В админы",
                Location = new Point(10, 135),
                Size = new Size(130, 28),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(255, 243, 224),
                ForeColor = Color.FromArgb(109, 76, 65),
                Font = new Font("Segoe UI", 7, FontStyle.Bold)
            };
            changeRoleBtn.Click += (s, e) =>
            {
                if (username == "admin" && role == "admin")
                {
                    MessageBox.Show("❌ Нельзя изменить роль главного администратора!", "Ошибка",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                string newRole = (role == "admin") ? "user" : "admin";
                DatabaseHelper.UpdateUserRole(userId, newRole);
                ShowAdminTab("users");
                MessageBox.Show($"✅ Роль пользователя \"{username}\" изменена на \"{newRole}\"", "Успех");
            };
            card.Controls.Add(changeRoleBtn);

            Button changeBalanceBtn = new Button
            {
                Text = "💰 Баланс",
                Location = new Point(148, 135),
                Size = new Size(62, 28),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(232, 245, 233),
                ForeColor = Color.FromArgb(27, 94, 32),
                Font = new Font("Segoe UI", 7, FontStyle.Bold)
            };
            changeBalanceBtn.Click += (s, e) =>
            {
                Form balanceForm = new Form
                {
                    Text = $"💰 Баланс: {username}",
                    Size = new Size(350, 250),
                    StartPosition = FormStartPosition.CenterParent,
                    BackColor = Color.FromArgb(255, 248, 240),
                    FormBorderStyle = FormBorderStyle.FixedDialog,
                    MaximizeBox = false,
                    MinimizeBox = false
                };

                Label currentBalLabel = new Label
                {
                    Text = $"Текущий баланс: {balance:F2} ₽",
                    Font = new Font("Segoe UI", 11, FontStyle.Bold),
                    Location = new Point(20, 20),
                    AutoSize = true
                };
                balanceForm.Controls.Add(currentBalLabel);

                Label newBalLabel = new Label
                {
                    Text = "Новый баланс:",
                    Font = new Font("Segoe UI", 10),
                    Location = new Point(20, 60),
                    AutoSize = true
                };
                balanceForm.Controls.Add(newBalLabel);

                TextBox balanceInput = new TextBox
                {
                    Location = new Point(20, 85),
                    Width = 200,
                    Font = new Font("Segoe UI", 14),
                    Text = balance.ToString()
                };
                balanceForm.Controls.Add(balanceInput);

                Button add500 = new Button
                {
                    Text = "+500",
                    Location = new Point(20, 120),
                    Size = new Size(70, 30),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Color.FromArgb(232, 245, 233)
                };
                add500.Click += (ss, ee) =>
                {
                    if (decimal.TryParse(balanceInput.Text, out decimal val))
                        balanceInput.Text = (val + 500).ToString();
                };
                balanceForm.Controls.Add(add500);

                Button add1000 = new Button
                {
                    Text = "+1000",
                    Location = new Point(100, 120),
                    Size = new Size(70, 30),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Color.FromArgb(232, 245, 233)
                };
                add1000.Click += (ss, ee) =>
                {
                    if (decimal.TryParse(balanceInput.Text, out decimal val))
                        balanceInput.Text = (val + 1000).ToString();
                };
                balanceForm.Controls.Add(add1000);

                Button add5000 = new Button
                {
                    Text = "+5000",
                    Location = new Point(180, 120),
                    Size = new Size(70, 30),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Color.FromArgb(232, 245, 233)
                };
                add5000.Click += (ss, ee) =>
                {
                    if (decimal.TryParse(balanceInput.Text, out decimal val))
                        balanceInput.Text = (val + 5000).ToString();
                };
                balanceForm.Controls.Add(add5000);

                Button saveBtn = new Button
                {
                    Text = "💾 Сохранить",
                    Location = new Point(20, 165),
                    Size = new Size(140, 35),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Color.FromArgb(109, 76, 65),
                    ForeColor = Color.White,
                    Font = new Font("Segoe UI", 10, FontStyle.Bold),
                    DialogResult = DialogResult.OK
                };
                balanceForm.Controls.Add(saveBtn);

                Button cancelBtn = new Button
                {
                    Text = "Отмена",
                    Location = new Point(170, 165),
                    Size = new Size(140, 35),
                    FlatStyle = FlatStyle.Flat,
                    DialogResult = DialogResult.Cancel
                };
                balanceForm.Controls.Add(cancelBtn);

                if (balanceForm.ShowDialog() == DialogResult.OK)
                {
                    if (decimal.TryParse(balanceInput.Text, out decimal newBal) && newBal >= 0)
                    {
                        DatabaseHelper.UpdateBalance(userId, newBal);
                        ShowAdminTab("users");
                        MessageBox.Show($"✅ Баланс пользователя \"{username}\" обновлён: {newBal:F2} ₽", "Успех");
                    }
                    else
                    {
                        MessageBox.Show("❌ Введите неотрицательное число!", "Ошибка");
                    }
                }
            };
            card.Controls.Add(changeBalanceBtn);

            Button deleteUserBtn = new Button
            {
                Text = "🗑",
                Location = new Point(218, 135),
                Size = new Size(28, 28),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(255, 235, 238),
                ForeColor = Color.FromArgb(198, 40, 40),
                Font = new Font("Segoe UI", 8, FontStyle.Bold)
            };

            ToolTip toolTip = new ToolTip();
            toolTip.SetToolTip(deleteUserBtn, "Удалить пользователя");

            deleteUserBtn.Click += (s, e) =>
            {
                if (username == "admin")
                {
                    MessageBox.Show("❌ Нельзя удалить главного администратора!", "Ошибка",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                DialogResult result = MessageBox.Show(
                    $"Вы уверены, что хотите удалить пользователя \"{username}\"?\n\n" +
                    $"👤 Роль: {role}\n" +
                    $"💰 Баланс: {balance:F2} ₽\n\n" +
                    "⚠️ Это действие нельзя отменить! Все заказы пользователя будут сохранены.",
                    "⚠️ Подтверждение удаления",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (result == DialogResult.Yes)
                {
                    string adminPassword = Microsoft.VisualBasic.Interaction.InputBox(
                        "Для удаления пользователя введите пароль администратора:",
                        "🔐 Подтверждение администратора", "");

                    if (string.IsNullOrWhiteSpace(adminPassword))
                    {
                        MessageBox.Show("❌ Операция отменена.", "Отмена", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }

                    var (success, roleCheck, adminId) = DatabaseHelper.ValidateUser("admin", adminPassword);

                    if (!success || roleCheck != "admin")
                    {
                        MessageBox.Show("❌ Неверный пароль администратора!", "Ошибка",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    using (var conn = DatabaseHelper.GetConnection())
                    {
                        var deleteOrderItemsCmd = new SQLiteCommand(
                            @"DELETE FROM OrderItems WHERE OrderId IN (SELECT Id FROM Orders WHERE UserId = @uid)", conn);
                        deleteOrderItemsCmd.Parameters.AddWithValue("@uid", userId);
                        deleteOrderItemsCmd.ExecuteNonQuery();

                        var deleteOrdersCmd = new SQLiteCommand("DELETE FROM Orders WHERE UserId = @uid", conn);
                        deleteOrdersCmd.Parameters.AddWithValue("@uid", userId);
                        deleteOrdersCmd.ExecuteNonQuery();

                        var deleteUserCmd = new SQLiteCommand("DELETE FROM Users WHERE Id = @uid", conn);
                        deleteUserCmd.Parameters.AddWithValue("@uid", userId);
                        deleteUserCmd.ExecuteNonQuery();
                    }

                    ShowAdminTab("users");
                    MessageBox.Show($"🗑 Пользователь \"{username}\" успешно удалён!", "Успех",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            };
            card.Controls.Add(deleteUserBtn);

            return card;
        }

        public class RoundedPanel : Panel
        {
            public Color BorderColor { get; set; } = Color.Gray;
            public Color ShadowColor { get; set; } = Color.FromArgb(50, 0, 0, 0);
            public int CornerRadius { get; set; } = 10;
            public int BorderWidth { get; set; } = 2;

            public RoundedPanel()
            {
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.DoubleBuffer | ControlStyles.ResizeRedraw, true);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle rect = new Rectangle(BorderWidth / 2, BorderWidth / 2, Width - BorderWidth, Height - BorderWidth);
                using (GraphicsPath shadowPath = GetRoundedRectangle(rect, CornerRadius))
                using (var shadowBrush = new SolidBrush(ShadowColor))
                {
                    g.TranslateTransform(3, 3);
                    g.FillPath(shadowBrush, shadowPath);
                    g.TranslateTransform(-3, -3);
                }
                using (GraphicsPath path = GetRoundedRectangle(rect, CornerRadius))
                using (var brush = new SolidBrush(BackColor))
                    g.FillPath(brush, path);
                using (GraphicsPath borderPath = GetRoundedRectangle(rect, CornerRadius))
                using (var pen = new Pen(BorderColor, BorderWidth))
                    g.DrawPath(pen, borderPath);
            }

            private GraphicsPath GetRoundedRectangle(Rectangle rect, int radius)
            {
                GraphicsPath path = new GraphicsPath();
                path.AddArc(rect.X, rect.Y, radius * 2, radius * 2, 180, 90);
                path.AddLine(rect.X + radius, rect.Y, rect.Right - radius, rect.Y);
                path.AddArc(rect.Right - radius * 2, rect.Y, radius * 2, radius * 2, 270, 90);
                path.AddLine(rect.Right, rect.Y + radius, rect.Right, rect.Bottom - radius);
                path.AddArc(rect.Right - radius * 2, rect.Bottom - radius * 2, radius * 2, radius * 2, 0, 90);
                path.AddLine(rect.Right - radius, rect.Bottom, rect.X + radius, rect.Bottom);
                path.AddArc(rect.X, rect.Bottom - radius * 2, radius * 2, radius * 2, 90, 90);
                path.AddLine(rect.X, rect.Bottom - radius, rect.X, rect.Y + radius);
                path.CloseFigure();
                return path;
            }
        }
    }
}