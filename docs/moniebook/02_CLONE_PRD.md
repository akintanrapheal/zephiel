# Moniebook Clone — Product Requirements Document (PRD)

Scope: a multi-branch retail **POS + inventory + light ERP**, modelled on Moniebook, framed for
**Sterlin Glams** (ASP.NET Core 9 + EF Core + PostgreSQL). Each module notes **Status** vs what
Sterlin Glams already has: ✅ built · 🟡 partial · 🔴 to build.

---

## 1. Modules & functional requirements

### 1.1 Items / Catalogue  — 🟡
**Purpose:** define everything sold/stocked.
- Item types: **Standard** ✅, **Variant** ✅, **Bundled/Composite** 🔴 (BOM of child items).
- Fields: name, category ✅, measurement unit 🟡 (we have unit text), SKU ✅, barcode ✅, image ✅.
- Pricing: Fixed ✅; **Manual (cashier enters)** 🟡; **Markup** 🔴; **Range** 🔴; **pricing groups** 🔴.
- **Average cost price** auto-updated on receipt 🔴 (no cost field today).
- Track inventory + low-stock threshold ✅; per-branch sell/price/stock ✅ (variant-level done).
**Rules:** SKU unique; barcode unique; deleting an item with stock/history blocked or soft-deleted;
"price not set" cannot be sold via Fixed pricing.

### 1.2 Stock adjustment  — ✅ (parity)
**Inputs:** branch, reason (Receive/Damage/Loss/Inventory count/Correction), note, items (add by
search/category/supplier), per-item add/remove qty, **unit cost**, **expiry**.
**Outputs:** balance update + ledger movement (typed) + adjustment record id.
**Rules:** only tracked items; non-negative result; writes a `StockMovement`; branch-write authz.
*Sterlin Glams:* Stock grid + reasons ✅; **unit cost + expiry on receive** 🔴.

### 1.3 Stock transfer (branch ↔ branch / warehouse)  — ✅
**Inputs:** source branch, destination branch, items + qty, expected date.
**Workflow:** Draft → Sent/Dispatched → **Received** (reconcile Ordered vs Received vs Damaged vs
Won't-fulfil vs Pending). Reference `TO#####`. Created by / Received by / Received at. Print receipt.
**Rules:** can't transfer more than source on-hand; partial receipt supported; stock decremented at
source on dispatch, incremented at destination on receive.
*Sterlin Glams:* TransferWorkflow exists ✅; add **per-line Damaged/Won't-fulfil/Pending** columns 🟡.

### 1.4 Purchasing Orders (PO) + Suppliers  — 🔴 (biggest gap, = OP-16)
**Purpose:** order stock from suppliers; receive against the PO.
**Inputs:** supplier, branch/warehouse, items + qty + **unit cost**, expected date.
**Workflow:** Draft PO → Sent → **Partially/Fully Received** → Closed. Receiving creates `Purchase`
movements, raises on-hand, **recomputes average cost**, records "on order" qty.
**Outputs:** PO ref, goods-receipt note, supplier ledger.
**Rules:** received ≤ ordered (allow over with flag); cost flows to valuation/margin.

### 1.5 Batches & Expiry  — 🔴
Track lot/batch + expiry per receipt; **Expiring inventory** report; FEFO suggestion at sale.
(Lower priority for jewellery; relevant only if SG sells consumables.)

### 1.6 Production & Recipes (BOM)  — 🔴
Build a composite/manufactured item from raw materials; consuming components, producing finished
goods (relevant for "Flavour City Creamery", less so for jewellery — **optional for SG**).

### 1.7 POS / Registers  — ✅
Cart → payment (cash/transfer/card) → receipt → inventory deduction → reports. Register pairing,
staff PIN, **saved carts (park)** ✅, **outstanding/credit sales** 🟡, refunds + change ✅.
**Receipt header/footer** ✅ (just added). **Manual pricing at checkout** 🟡.

### 1.8 Customers / CRM  — ✅
Customer directory ✅, discounts ✅, sales-by-customer 🟡.

### 1.9 Reports  — 🟡
See §3. Have: stock/sales/reorder/value/movements/shrinkage. Missing: **cost-based margin
everywhere**, inventory **valuation at cost**, sales-by-staff/pricing-group, expiring, payment-method (admin has some).

### 1.10 Settings  — ✅ (strong)
General, shipping, notifications, loyalty, emails, store, **Inventory/Orders/POS/Security** (added).
Add: **Pricing Groups**, **Tax/VAT**, **Receipt header/footer in settings** (POS done), **Measurement Units** list 🟡.

### 1.11 Administration  — ✅
Staff & roles ✅ (role+section permissions), branches ✅, registers ✅, audit log ✅, billing 🔴 (SaaS-only; N/A for in-house).

---

## 2. Cross-cutting business rules
- Every stock change is an **append-only ledger entry** with type, qty, balance-after, branch,
  variant, user, reference, timestamp (✅ `StockMovement`).
- **Variants own stock; product = roll-up; no pool fallback** (✅ adopted).
- Stock can't go negative (✅ clamp + concurrency lock).
- Branch-scoped **write** authorization; reads open (✅).
- **Cost price** is the spine of valuation + margin + PO receiving (🔴 to add).

---

## 3. Reports matrix (target)
| Report | Have | Add |
|---|---|---|
| Sales summary (trend, gross profit) | 🟡 admin | gross **profit** needs cost |
| Sales by item/category/**staff**/customer/**pricing group** | 🟡 | staff, pricing-group, margin cols |
| Payment method | 🟡 admin | parity |
| Discounts / Taxes | 🟡 / 🔴 | tax report (needs VAT) |
| **Inventory valuation (at cost)** | 🔴 | total cost value, selling value, potential profit, margin% |
| Stock movement (opening/closing/in/out) | ✅ (new) | add opening/closing snapshot framing |
| Shrinkage (damage/loss) | ✅ (new) | — |
| Reorder | ✅ | "create PO from reorder" |
| Expiring inventory | 🔴 | needs batches |

---

## 4. Sterlin Glams workflow alignment

Target flow (your spec) and where we stand:
```
Real Stock          ✅ per-(product,variant,branch) on-hand ledgered
  ↓
Smart Allocation    ✅ OrderFulfilmentService allocates across active branches
  ↓
Stock Reservation   ✅ StockReservation holds + ReservationSweeper (timeout now configurable)
  ↓
Transfer Planning   🟡 transfers exist; auto "transfer-then-sell" suggestion partial
  ↓
Fulfilment          ✅ reserve → (transfer) → deduct on payment, per variant
  ↓
Customer ETA        🟡 delivery zone fees/timeframes shown; per-order ETA not surfaced
```

**Net:** Sterlin Glams already matches Moniebook on the *core* multi-branch inventory spine
(variant stock, transfers, reservations, ledger, reports). The **clone gaps that add real value**:
1. **Cost price + Inventory valuation/margin** (unlocks profit everywhere) — *high*.
2. **Purchase Orders + Receiving** (proper inbound, auto avg-cost) — *high* (= OP-16).
3. **Pricing groups / Markup & Range pricing / Manual pricing at till** — *medium*.
4. **Tax/VAT + tax report** — *medium*.
5. **Bundled/Composite items**, **Batches/Expiry**, **Production/Recipes** — *low for jewellery*.

---

## 5. Non-functional
- Multi-tenant **not** required (single business) — drop "Switch Business", BRM, billing.
- Keep current stack (ASP.NET Core 9 / EF / Postgres), Render hosting.
- Role-based authz already in place; extend permissions for PO/valuation pages.
- All money in ₦ (configurable currency symbol ✅).
