# Moniebook (Moniepoint) — Feature Audit

Reverse-engineered from product screenshots (brands seen: **Moniebook / Servetrade**,
sample tenants "Flavour City Creamery", "Bullock Industries", "Unique Fashion Hub",
"Sunday Lawrence"). This is a cloud **retail POS + multi-branch inventory + light ERP**.

> Purpose: catalogue every observed feature, then map it against what Sterlin Glams
> already has (see `04` for the gap-driven build list).

---

## 1. Navigation & menu structure

The sidebar changes slightly per plan/tenant; the consolidated tree:

```
Home (dashboard)
Sales ▾
 ├── Completed Sales
 ├── Outstanding Sales        (unpaid / credit / layaway)
 └── Saved Carts              (parked sales)
CRM ▾
 ├── Customers
 └── Discounts
Reports ▾
 ├── Sales summary
 ├── Sales by item
 ├── Sales by category
 ├── Sales by staff
 ├── Sales by customer
 ├── Sales by pricing groups
 ├── Payment method
 ├── Discounts
 ├── Taxes
 ├── Expiring inventory
 ├── Inventory valuation
 └── Stock movement
Settings
Inventory ▾
 ├── All items
 ├── Categories
 ├── Taxes
 ├── Stock management ▾
 │    ├── Stock adjustment
 │    ├── Stock history
 │    ├── Stock transfer
 │    ├── Batch management
 │    ├── Production & Recipes
 │    └── Purchasing orders
 ├── Measurement Units
 └── Suppliers
Administration ▾
 ├── Staff & Roles
 ├── Branches
 └── Registers
Sales channels
 └── Registers
Activity Log
```

**Settings** (own left-nav):
```
PERSONAL  → Preferences (name, email)
BUSINESS  → Business information · Billing & subscriptions
SALES     → Payment methods · Register configurations · Receipt settings · Payment restrictions
          → Pricing Groups (referenced from item pricing)
```

**Account menu**: Switch Business (multi-business), Settings, Support, Terms, Privacy, Logout.

---

## 2. Home / dashboard sections

- **Revenue Summary** cards: Gross Sales, Net Sales, Refunded Amount, Discounted Amount.
- **Transactions Summary**: Number of Sales, Gross profit, Number of refunds, Change Given;
  **Top payment methods** (e.g. Moniepoint, Cash, POS Transfer) with #sales + amount.
- **Performance**: Top products (sales count + amount), Top staff (sales count + amount).
- **Sales trend** chart (period-filtered, "0 sales… come back after your first sale" empty state).
- **My Registers** + **My Register Staff PIN** (masked, reveal) + Pair register / View all registers.
- **Business Relationship Manager (BRM) / Implementation Specialist** contact widget.
- Top controls: **Date** filter, **+ Branch** filter, **View all reports**, **+ Add item**.

---

## 3. Items / catalogue

**Item types (create wizard, "What do you want to create?")**
- **Standard item** — a standalone product sold on its own.
- **Variant / Variable item** — variations like size/colour (e.g. T-shirt S/M/L).
- **Bundled / Composite item** — a collection sold together as one (e.g. T-shirt + cap).

**Item fields**
- Name, Category (optional), Measurement unit (Per Item, Litre, KG, Carton, Pl., …),
  SKU (auto-generated, e.g. `3-1768330516-FQV`), Barcode (ISBN/UPC/GTIN, optional), Image (PNG/JPG ≤2MB).
- **Pricing types**: **Fixed** · **Manual** (cashier enters at checkout) · **Markup** · **Range**.
  - **Average cost price** — "updates automatically when you receive inventory".
  - **Pricing groups** (Default + custom) with per-group sell price (managed in Settings → Pricing Groups).
- **Track inventory** toggle → Stock qty + **Low stock alert** threshold.
- **Branches**: "Sell in all branches" toggle; per-branch **Sell item** toggle, **Branch price**,
  **Stock**, **Low stock**. "Available in all current and future branches" option.
- **Supplier** (default supplier + per-supplier price).
- Per-item **View stock history**, **Last updated** date.

**All items list**: search by name; filters Branch, Sell Status (Sold on register), Categories,
Inventory, Supplier; columns Item, Category, Unit (badge), Stock, Selling price, Cost price, Supplier.
Row → **Item details** drawer → Edit / Delete (confirm modal). Bulk **More actions** / **Export**.
"Price not set" shown when no sell price.

**Categories**, **Taxes** (e.g. VAT 7%), **Measurement Units** are first-class managed lists.

---

## 4. Stock management

- **Stock adjustment** ("Adjust Stock"): pick **Branch** + **Reason** (Receive items / Damage /
  Loss / Inventory count / Correction) + Note; **Items to adjust** via Add by category, Add by
  supplier, or search (name/SKU/barcode) — or **Create item** inline. Per row: Current stock,
  **Add stock** or **Removed stock**, **Unit cost price**, **Expiration date**, **Stock after**.
  "Only items with Track Stock enabled can be adjusted." Save adjustment. List view:
  Adjustment ID (`BSA00085`), Date, Reason, Branch, # Adjustments; filter Date/Reason/Branch.
- **Stock history** — per-item ledger of every change.
- **Stock transfer** — Order ID (`TO00075`), Status (Draft/Sent/Received), Date created,
  Expected date, Source branch, Destination branch, Transfer qty. **New order**.
  Order details drawer: Created by, Received by, Received at, items with **Ordered / Received /
  Won't fulfil / Damaged / Pending** columns. **Print receipt**. (Warehouses act as branches.)
- **Batch management** — lot/batch tracking with expiry.
- **Production & Recipes** — manufacture composite items from raw materials (BOM).
- **Purchasing orders** — supplier POs (promoted as the upgrade from manual "Receive items").
- **Suppliers** — supplier records (used by POs + item default supplier).

---

## 5. Reports & analytics

| Report | Key metrics | Filters |
|---|---|---|
| Sales summary | Total sales over time (line/bar, hour/day), # sales, Gross profit, Discounts | Date, Time, Branch, Staff, Sale Type |
| Sales by item | All/Top items; Total sold, Gross sale amt, **Cost price, Gross profit, Discount, Margin** | Date, Time, Branch, Staff, Sale Type |
| Sales by category | per-category sales/profit | same |
| Sales by staff | per-staff sales | same |
| Sales by customer | per-customer sales | same |
| Sales by pricing groups | sales per pricing group | same |
| Payment method | method, # payments, amount collected | Date, Time, Branch, Staff, Sale Type |
| Discounts | discount usage | date/branch |
| Taxes | tax collected | date/branch |
| Expiring inventory | items nearing expiry | date/branch |
| **Inventory valuation** | **Total inventory value (cost)**, Total selling-price value, **Potential profit**, **Margin %**; per item In stock/Cost/Inventory value/Selling value/Potential profit/Margin | Date, Branch, Category, Sell Status |
| **Stock movement** | Opening stock, Closing stock, Stock In, Stock Out | Date range, Sell Status (Sold on register) |

All reports: **Export Report** / **Print Report**; rich date presets (Today, Yesterday, Last 7/30
days, This/Last month, Year to date, Last year, custom range).

---

## 6. POS / registers

- **Registers** paired per branch (named, OS shown, online/offline status); **Pair register**.
- **Register Staff PIN** login per staff.
- **Register configurations** + **Receipt settings** (Header/Footer text, live receipt preview:
  business name/address, register, cashier, line items, subtotal, **VAT (7%)**, total, charged, change).
- **Payment methods** + **Payment restrictions**.
- Sales states: **Completed**, **Outstanding** (credit/unpaid), **Saved carts** (parked).
- Refunds + Change given tracked.

---

## 7. Customers / CRM

- **Customers** directory; **Sales by customer**; **Discounts** (CRM-managed).

---

## 8. Administration & roles

- **Staff & Roles** (role-based access), **Branches** (Name, Address, City, State; # staff, # registers),
  **Registers**. **Billing & subscriptions** (subscription grace-period banner observed).
- **Activity Log** (audit trail; "Created by/Received by" attribution throughout).

### Implied user roles
| Role | Evidence |
|---|---|
| **Owner / Admin** | full settings, billing, staff, branches |
| **Branch Manager** | per-branch dashboards, transfers, approvals |
| **Cashier / Sales Associate** | Register Staff PIN, POS sell, saved carts |
| **Inventory Officer** | stock adjustment, transfers, POs, suppliers |
| **Finance** | reports, payment methods, taxes, valuation |
| (External) **BRM / Implementation Specialist** | Moniepoint support contact, not a tenant role |

---

## 9. Multi-branch logic (most relevant to Sterlin Glams)

- Each **item × branch** carries its own **Sell toggle, price, stock, low-stock threshold**.
- "Available in all current and future branches" auto-enrols new branches.
- **Warehouses are branches** → stock flows Warehouse → Shop via **Stock transfer** (with
  Ordered/Received/Damaged/Won't-fulfil/Pending reconciliation).
- **Purchasing orders** bring stock in from suppliers (→ Receive → on-hand, updates avg cost).
- **Batches/expiry** + **Production/recipes** support FMCG/food + manufacturing.
- Valuation + stock-movement reporting is **per branch**.
