# 357 Motorcycle Parts and Accessories Shop
Procurement and Inventory System: C# Windows Forms (.NET 8) + SQL Server Express

## What you need
- Visual Studio 2022 (17.8 or newer) with the **.NET desktop development** workload
- SQL Server Express and SQL Server Management Studio (SSMS)

## Setup (3 steps)
1. **Create the database.** Open SSMS, connect to `.\SQLEXPRESS`, open `Database\Shop357DB.sql`, press **F5**.
   It creates `Shop357DB` with all tables and starter data. It is safe to run again.
2. **Open the project.** Double-click `Shop357.sln` in Visual Studio. NuGet restores `Microsoft.Data.SqlClient` automatically.
3. **Run.** Press **F5**.

If your SQL Server instance is not called `SQLEXPRESS`, change `ConnectionString` in `Shop357\Data\Db.cs`
(for example `Server=localhost;...` or `Server=.\MSSQLSERVER01;...`).

## Logins
| Username | Password | Role |
|---|---|---|
| owner | owner123 | Owner (approves PRs, manages users) |
| staff | staff123 | Staff |

Change these in the Users tab (Owner) after the first run.

## How to test the whole flow
1. **Staff:** Inventory tab, click **Restock Report**, then **Send to Requisition**. A Draft PR is created with details auto-filled.
2. **Staff:** Requisition tab, select the PR, **Submit for Approval**. The Owner is notified.
3. **Owner:** log out, log in as owner, Requisition tab, **Approve** (or **Reject** with a reason; staff can then revise and resubmit, or cancel).
4. **Staff:** Purchasing tab, select the approved PR, **Add Quotation** for at least 2 suppliers, **Compare Quotations**, select one, **Create PO from Selected** (choose COD or Bank Transfer).
5. **Staff:** Delivery tab, select the PO, **Receive Delivery**. Enter arrived and damaged quantities. Only good items are added to inventory; problems are flagged as a discrepancy and the Owner is notified.
6. Purchasing, Purchase Orders sub-tab: **Mark as Paid**, then **Close PO** once delivered and paid.
7. Reports tab: view and export any report to CSV (opens in Excel).

## Business rules built in
- Low stock = quantity at or below minimum stock (rows turn red; alert shown at the top)
- A PR must be Approved before Purchasing can use it
- A PO needs at least 2 supplier quotations, and the quotation must price every item
- Inventory changes only when a delivery is accepted (good quantity only) or through a logged manual adjustment
- Every stock change writes a StockMovements record; quantities are never edited directly
- Nothing is deleted from the main records; products, suppliers and users are deactivated, PRs are cancelled
- Delivered quantity above the ordered quantity needs the Owner's approval

## Project layout
```
Database\Shop357DB.sql        tables + starter data
Shop357\Program.cs            start-up and login loop
Shop357\Data\Db.cs            SQL helper and transactions
Shop357\Data\Services.cs      session, notifications, stock rule
Shop357\UI\Ui.cs             colours, fonts and shared styling (class Theme: change colours here)
Shop357\UI\MainForm.cs       sidebar menu + page title
Shop357\UI\DashboardControl.cs  home screen with clickable number tiles
Shop357\UI\*.cs               one file per page (Inventory, Requisition, Purchasing, Delivery, Suppliers, Reports, Users)
```

## Logo
The shop logo is in `Shop357\Assets\logo.png` (login screen and sidebar) and `Shop357\Assets\app.ico` (program and window icon).
To change it, replace those two files and rebuild.

## Look and feel
- Sidebar menu on the left; click a dashboard tile to jump to that page
- Orange buttons = main action, red outline = reject / cancel / delete, white = everything else
- To re-colour the whole app, edit the `Theme` class at the top of `UI\Ui.cs`
