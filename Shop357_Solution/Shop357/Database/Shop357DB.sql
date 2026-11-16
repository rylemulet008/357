/* =====================================================================
   357 Motorcycle Parts and Accessories Shop
   Database script for SQL Server Express (run in SSMS)

   How to run: open this file in SQL Server Management Studio, connect to
   .\SQLEXPRESS, and press F5. It is safe to run more than once.
   Default logins:  owner / owner123   and   staff / staff123
   ===================================================================== */

IF DB_ID('Shop357DB') IS NULL
    CREATE DATABASE Shop357DB;
GO

USE Shop357DB;
GO

/* ---------- Users ---------- */
IF OBJECT_ID('dbo.Users', 'U') IS NULL
CREATE TABLE dbo.Users (
    UserId       INT IDENTITY(1,1) PRIMARY KEY,
    Username     NVARCHAR(50)  NOT NULL UNIQUE,
    PasswordHash VARCHAR(64)   NOT NULL,
    FullName     NVARCHAR(100) NOT NULL,
    Role         VARCHAR(10)   NOT NULL CHECK (Role IN ('Owner', 'Staff')),
    IsActive     BIT           NOT NULL DEFAULT 1
);
GO

/* ---------- Categories and Products ---------- */
IF OBJECT_ID('dbo.Categories', 'U') IS NULL
CREATE TABLE dbo.Categories (
    CategoryId INT IDENTITY(1,1) PRIMARY KEY,
    Name       NVARCHAR(80) NOT NULL UNIQUE
);
GO

IF OBJECT_ID('dbo.Products', 'U') IS NULL
CREATE TABLE dbo.Products (
    ProductId       INT IDENTITY(1,1) PRIMARY KEY,
    Code            NVARCHAR(30)  NOT NULL UNIQUE,
    Name            NVARCHAR(150) NOT NULL,
    CategoryId      INT           NOT NULL REFERENCES dbo.Categories(CategoryId),
    Brand           NVARCHAR(80)  NULL,
    CompatibleModel NVARCHAR(120) NULL,
    Quantity        INT           NOT NULL DEFAULT 0  CHECK (Quantity >= 0),
    MinStock        INT           NOT NULL DEFAULT 5  CHECK (MinStock >= 0),
    ReorderQty      INT           NOT NULL DEFAULT 10 CHECK (ReorderQty >= 0),
    UnitCost        DECIMAL(12,2) NOT NULL DEFAULT 0  CHECK (UnitCost >= 0),
    SellingPrice    DECIMAL(12,2) NOT NULL DEFAULT 0  CHECK (SellingPrice >= 0),
    IsActive        BIT           NOT NULL DEFAULT 1
);
GO

/* ---------- Suppliers ---------- */
IF OBJECT_ID('dbo.Suppliers', 'U') IS NULL
CREATE TABLE dbo.Suppliers (
    SupplierId    INT IDENTITY(1,1) PRIMARY KEY,
    Name          NVARCHAR(120) NOT NULL UNIQUE,
    ContactPerson NVARCHAR(100) NULL,
    Phone         NVARCHAR(40)  NULL,
    Address       NVARCHAR(250) NULL,
    IsActive      BIT           NOT NULL DEFAULT 1
);
GO

/* ---------- Stock movements (every stock change is logged here) ---------- */
IF OBJECT_ID('dbo.StockMovements', 'U') IS NULL
CREATE TABLE dbo.StockMovements (
    MovementId INT IDENTITY(1,1) PRIMARY KEY,
    ProductId  INT           NOT NULL REFERENCES dbo.Products(ProductId),
    QtyChange  INT           NOT NULL,
    QtyAfter   INT           NOT NULL,
    Reason     NVARCHAR(200) NOT NULL,
    RefType    VARCHAR(20)   NULL,      -- 'Delivery', 'Adjustment', 'Initial'
    RefId      INT           NULL,
    CreatedBy  INT           NOT NULL REFERENCES dbo.Users(UserId),
    CreatedAt  DATETIME2     NOT NULL DEFAULT SYSDATETIME()
);
GO

/* ---------- Requisition ---------- */
IF OBJECT_ID('dbo.PurchaseRequests', 'U') IS NULL
CREATE TABLE dbo.PurchaseRequests (
    PrId         INT IDENTITY(1,1) PRIMARY KEY,
    PrNumber     VARCHAR(30)   NULL,
    Status       VARCHAR(20)   NOT NULL DEFAULT 'Draft'
                 CHECK (Status IN ('Draft','Pending Approval','Approved','Rejected','Cancelled','Ordered')),
    Remarks      NVARCHAR(300) NULL,
    CreatedBy    INT           NOT NULL REFERENCES dbo.Users(UserId),
    CreatedAt    DATETIME2     NOT NULL DEFAULT SYSDATETIME(),
    ApprovedBy   INT           NULL REFERENCES dbo.Users(UserId),
    ApprovedAt   DATETIME2     NULL,
    RejectReason NVARCHAR(300) NULL
);
GO

IF OBJECT_ID('dbo.PurchaseRequestItems', 'U') IS NULL
CREATE TABLE dbo.PurchaseRequestItems (
    PrItemId  INT IDENTITY(1,1) PRIMARY KEY,
    PrId      INT           NOT NULL REFERENCES dbo.PurchaseRequests(PrId),
    ProductId INT           NOT NULL REFERENCES dbo.Products(ProductId),
    Quantity  INT           NOT NULL CHECK (Quantity > 0),
    Specs     NVARCHAR(300) NULL
);
GO

/* ---------- Purchasing ---------- */
IF OBJECT_ID('dbo.Quotations', 'U') IS NULL
CREATE TABLE dbo.Quotations (
    QuotationId  INT IDENTITY(1,1) PRIMARY KEY,
    PrId         INT           NOT NULL REFERENCES dbo.PurchaseRequests(PrId),
    SupplierId   INT           NOT NULL REFERENCES dbo.Suppliers(SupplierId),
    DeliveryDays INT           NOT NULL DEFAULT 3 CHECK (DeliveryDays >= 0),
    Notes        NVARCHAR(300) NULL,
    IsSelected   BIT           NOT NULL DEFAULT 0,
    CreatedBy    INT           NOT NULL REFERENCES dbo.Users(UserId),
    CreatedAt    DATETIME2     NOT NULL DEFAULT SYSDATETIME(),
    CONSTRAINT UQ_Quotation_PR_Supplier UNIQUE (PrId, SupplierId)
);
GO

IF OBJECT_ID('dbo.QuotationItems', 'U') IS NULL
CREATE TABLE dbo.QuotationItems (
    QItemId     INT IDENTITY(1,1) PRIMARY KEY,
    QuotationId INT           NOT NULL REFERENCES dbo.Quotations(QuotationId),
    PrItemId    INT           NOT NULL REFERENCES dbo.PurchaseRequestItems(PrItemId),
    UnitPrice   DECIMAL(12,2) NOT NULL CHECK (UnitPrice >= 0),
    Specs       NVARCHAR(300) NULL,
    CONSTRAINT UQ_QuotationItem UNIQUE (QuotationId, PrItemId)
);
GO

IF OBJECT_ID('dbo.PurchaseOrders', 'U') IS NULL
CREATE TABLE dbo.PurchaseOrders (
    PoId          INT IDENTITY(1,1) PRIMARY KEY,
    PoNumber      VARCHAR(30)   NULL,
    PrId          INT           NOT NULL REFERENCES dbo.PurchaseRequests(PrId),
    QuotationId   INT           NOT NULL REFERENCES dbo.Quotations(QuotationId),
    SupplierId    INT           NOT NULL REFERENCES dbo.Suppliers(SupplierId),
    PaymentMethod VARCHAR(20)   NOT NULL CHECK (PaymentMethod IN ('COD', 'Bank Transfer')),
    IsPaid        BIT           NOT NULL DEFAULT 0,
    PaidAt        DATETIME2     NULL,
    Status        VARCHAR(20)   NOT NULL DEFAULT 'Ordered'
                  CHECK (Status IN ('Ordered','Partially Delivered','Delivered','Closed')),
    ExpectedDate  DATE          NOT NULL,
    TotalAmount   DECIMAL(14,2) NOT NULL DEFAULT 0,
    CreatedBy     INT           NOT NULL REFERENCES dbo.Users(UserId),
    CreatedAt     DATETIME2     NOT NULL DEFAULT SYSDATETIME()
);
GO

IF OBJECT_ID('dbo.PurchaseOrderItems', 'U') IS NULL
CREATE TABLE dbo.PurchaseOrderItems (
    PoItemId    INT IDENTITY(1,1) PRIMARY KEY,
    PoId        INT           NOT NULL REFERENCES dbo.PurchaseOrders(PoId),
    ProductId   INT           NOT NULL REFERENCES dbo.Products(ProductId),
    Quantity    INT           NOT NULL CHECK (Quantity > 0),
    UnitPrice   DECIMAL(12,2) NOT NULL CHECK (UnitPrice >= 0),
    ReceivedQty INT           NOT NULL DEFAULT 0 CHECK (ReceivedQty >= 0)  -- accepted (good) quantity so far
);
GO

/* ---------- Delivery ---------- */
IF OBJECT_ID('dbo.Deliveries', 'U') IS NULL
CREATE TABLE dbo.Deliveries (
    DeliveryId INT IDENTITY(1,1) PRIMARY KEY,
    PoId       INT           NOT NULL REFERENCES dbo.PurchaseOrders(PoId),
    ReceivedBy INT           NOT NULL REFERENCES dbo.Users(UserId),
    ReceivedAt DATETIME2     NOT NULL DEFAULT SYSDATETIME(),
    Status     VARCHAR(20)   NOT NULL CHECK (Status IN ('Accepted', 'Discrepancy')),
    Notes      NVARCHAR(300) NULL
);
GO

IF OBJECT_ID('dbo.DeliveryItems', 'U') IS NULL
CREATE TABLE dbo.DeliveryItems (
    DeliveryItemId INT IDENTITY(1,1) PRIMARY KEY,
    DeliveryId     INT           NOT NULL REFERENCES dbo.Deliveries(DeliveryId),
    PoItemId       INT           NOT NULL REFERENCES dbo.PurchaseOrderItems(PoItemId),
    ReceivedQty    INT           NOT NULL CHECK (ReceivedQty >= 0),   -- everything that arrived
    DamagedQty     INT           NOT NULL DEFAULT 0 CHECK (DamagedQty >= 0),
    Notes          NVARCHAR(300) NULL
);
GO

/* ---------- Notifications ---------- */
IF OBJECT_ID('dbo.Notifications', 'U') IS NULL
CREATE TABLE dbo.Notifications (
    NotificationId INT IDENTITY(1,1) PRIMARY KEY,
    ForRole        VARCHAR(10)   NOT NULL CHECK (ForRole IN ('Owner', 'Staff', 'All')),
    Message        NVARCHAR(300) NOT NULL,
    RefType        VARCHAR(20)   NULL,
    RefId          INT           NULL,
    IsRead         BIT           NOT NULL DEFAULT 0,
    CreatedAt      DATETIME2     NOT NULL DEFAULT SYSDATETIME()
);
GO

/* =====================================================================
   Starter data (only inserted when the tables are empty)
   ===================================================================== */

-- Passwords are stored as SHA-256 hashes (upper-case hex), same as the C# code.
IF NOT EXISTS (SELECT 1 FROM dbo.Users)
BEGIN
    INSERT INTO dbo.Users (Username, PasswordHash, FullName, Role) VALUES
    ('owner', CONVERT(VARCHAR(64), HASHBYTES('SHA2_256', 'owner123'), 2), N'Shop Owner', 'Owner'),
    ('staff', CONVERT(VARCHAR(64), HASHBYTES('SHA2_256', 'staff123'), 2), N'Shop Staff', 'Staff');
END
GO

IF NOT EXISTS (SELECT 1 FROM dbo.Categories)
    INSERT INTO dbo.Categories (Name) VALUES
    (N'Engine Parts'), (N'Brakes'), (N'Tires & Wheels'), (N'Electrical'),
    (N'Accessories'), (N'Oils & Lubricants');
GO

IF NOT EXISTS (SELECT 1 FROM dbo.Suppliers)
    INSERT INTO dbo.Suppliers (Name, ContactPerson, Phone, Address) VALUES
    (N'Sample Supplier A', N'Contact A', N'0900-000-0001', N'Sample address A'),
    (N'Sample Supplier B', N'Contact B', N'0900-000-0002', N'Sample address B'),
    (N'Sample Supplier C', N'Contact C', N'0900-000-0003', N'Sample address C');
GO

IF NOT EXISTS (SELECT 1 FROM dbo.Products)
BEGIN
    INSERT INTO dbo.Products (Code, Name, CategoryId, Brand, CompatibleModel, Quantity, MinStock, ReorderQty, UnitCost, SellingPrice)
    SELECT v.Code, v.Name, c.CategoryId, v.Brand, v.Model, v.Qty, v.MinStock, v.ReorderQty, v.Cost, v.Price
    FROM (VALUES
        ('BRK-001', N'Front Brake Pad Set',   N'Brakes',           N'Sample Brand', N'Honda Click 125',   12, 5, 10,  180.00,  280.00),
        ('BRK-002', N'Rear Brake Shoe',       N'Brakes',           N'Sample Brand', N'Yamaha Mio',         3, 5, 10,  150.00,  240.00),
        ('ENG-001', N'Spark Plug',            N'Engine Parts',     N'Sample Brand', N'Universal 125cc',   25, 10, 20,   60.00,  110.00),
        ('ENG-002', N'Air Filter',            N'Engine Parts',     N'Sample Brand', N'Honda TMX 155',      0, 4, 8,   120.00,  200.00),
        ('TIR-001', N'Tubeless Tire 80/90-14',N'Tires & Wheels',   N'Sample Brand', N'Scooter 14 inch',    6, 4, 6,   850.00, 1250.00),
        ('ELC-001', N'LED Headlight Bulb',    N'Electrical',       N'Sample Brand', N'Universal',          4, 6, 12,  200.00,  350.00),
        ('ACC-001', N'Handle Grip Pair',      N'Accessories',      N'Sample Brand', N'Universal',         15, 5, 10,   90.00,  160.00),
        ('OIL-001', N'4T Engine Oil 1L',      N'Oils & Lubricants',N'Sample Brand', N'Universal 4-stroke',20, 10, 24, 250.00,  380.00)
    ) AS v(Code, Name, CategoryName, Brand, Model, Qty, MinStock, ReorderQty, Cost, Price)
    JOIN dbo.Categories c ON c.Name = v.CategoryName;

    INSERT INTO dbo.StockMovements (ProductId, QtyChange, QtyAfter, Reason, RefType, CreatedBy)
    SELECT ProductId, Quantity, Quantity, N'Initial stock', 'Initial', (SELECT TOP 1 UserId FROM dbo.Users WHERE Role = 'Owner')
    FROM dbo.Products WHERE Quantity > 0;
END
GO

PRINT 'Shop357DB is ready. Log in with owner / owner123 or staff / staff123.';
GO
