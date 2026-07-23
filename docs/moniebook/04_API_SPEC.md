# Moniebook Clone — API / Endpoint Specification

REST-style endpoints needed to power the modules. In Sterlin Glams these are mostly **MVC
controller actions** today (server-rendered) rather than a JSON API; this lists the logical
operations either way. ✅ exists (as action/page) · 🟡 partial · 🔴 to build. Auth: cookie +
role/permission; branch-write authorization on all stock mutations.

Conventions: `GET` list/detail, `POST` create, `PUT/PATCH` update, `DELETE` remove. List
endpoints accept `?q=&page=&branchId=&categoryId=&from=&to=&sort=`.

---

## Items & catalogue
- `GET  /items` ✅ — search (name/SKU/barcode), filters, sort, paged
- `GET  /items/{id}` ✅ · `POST /items` ✅ · `PUT /items/{id}` ✅ · `DELETE /items/{id}` ✅
- `GET  /items/{id}/stock-history` ✅ (per-product ledger)
- `POST /items/{id}/variants` ✅ · variant CRUD ✅
- `POST /items/bundled` 🔴 (composite + components)
- `GET/POST /categories` ✅ · `GET/POST /measurement-units` 🔴 · `GET/POST /taxes` 🔴
- `GET/POST/PUT /pricing-groups` 🔴 · `PUT /items/{id}/group-prices` 🔴

## Inventory
- `GET  /inventory/stock` ✅ — grid (per product/variant/branch), search/sort
- `GET  /inventory/scan?code=` ✅ — exact barcode/SKU lookup
- `POST /inventory/adjust` ✅ — bulk set with reason (→ movements); 🔴 add header `BSA#####`, unit_cost, expiry
- `GET  /inventory/adjustments` 🔴 (list of adjustment headers) · `GET /inventory/adjustments/{id}` 🔴
- `GET  /inventory/movements` ✅ (new global ledger; filter type/branch/date/product)
- `GET  /inventory/valuation` 🔴 (at cost + selling + potential profit + margin)
- `GET  /inventory/expiring` 🔴 (needs batches)

## Transfers
- `GET  /transfers` ✅ · `GET /transfers/{id}` ✅
- `POST /transfers` ✅ (draft) · `POST /transfers/{id}/dispatch` 🟡 · `POST /transfers/{id}/receive` ✅
  (body: per-line received/damaged/wont_fulfil) 🟡
- `GET  /transfers/{id}/receipt` 🟡 (print)

## Suppliers & Purchasing (🔴 — OP-16)
- `GET/POST/PUT/DELETE /suppliers` 🔴
- `GET  /purchase-orders` 🔴 · `GET /purchase-orders/{id}` 🔴
- `POST /purchase-orders` 🔴 (supplier, branch, lines w/ unit_cost, expected_date)
- `POST /purchase-orders/{id}/send` 🔴
- `POST /purchase-orders/{id}/receive` 🔴 (lines received → Purchase movements + on-hand + avg-cost)
- `POST /purchase-orders/{id}/cancel` 🔴
- `GET  /reorder` ✅ · `POST /reorder/create-po` 🔴 (PO draft from reorder report)

## Production / recipes (🔴, optional)
- `GET/POST /recipes` 🔴 · `POST /production-runs` 🔴 (consume components, produce output)

## POS / registers / sales
- `GET  /registers` ✅ · `POST /registers/pair` 🟡
- `POST /till/sessions/open` ✅ · `POST /till/sessions/close` ✅ (Z-report ✅)
- `POST /till/cart` (park/save) ✅ · `GET /till/saved-carts` ✅
- `POST /till/checkout` ✅ (cart → payment → receipt → deduct stock → reports)
- `GET  /till/receipt/{orderId}` ✅
- `GET  /sales/completed` ✅ · `GET /sales/outstanding` 🟡 · `POST /refunds` ✅

## Customers / CRM
- `GET/POST/PUT /customers` ✅ · `GET/POST /discounts` ✅ · `GET /track` (order lookup) ✅

## Reports (read-only, all exportable)
- `GET /reports/sales-summary` 🟡 · `/sales-by-item` 🟡 · `/sales-by-category` 🟡
- `/sales-by-staff` 🔴 · `/sales-by-customer` 🟡 · `/sales-by-pricing-group` 🔴
- `/payment-method` 🟡 · `/discounts` 🟡 · `/taxes` 🔴
- `/inventory-valuation` 🔴 · `/stock-movement` ✅ · `/shrinkage` ✅ · `/expiring` 🔴
- each: `?export=csv` ✅ pattern

## Settings & admin
- `GET/PUT /settings` ✅ (grouped key/value) · `GET/PUT /settings/receipt` ✅
- `GET/POST /branches` ✅ · `GET/POST /staff` + roles ✅ · `GET /activity-log` ✅
- `GET/PUT /payment-methods` 🟡 · `/payment-restrictions` 🔴

---

## Suggested build order (highest value first)
1. **Cost price** on products/variants + **/inventory/valuation** report. 🔴
2. **Suppliers** CRUD + **Purchase Orders** (create→send→receive, avg-cost). 🔴 (OP-16)
3. **Adjustment headers** (`BSA#####`) with unit_cost + expiry; **transfer Damaged/Won't-fulfil** lines. 🟡→✅
4. **Pricing groups / markup / range / manual-at-till**; **Tax/VAT** + tax report. 🔴
5. (Optional) Bundled items, Batches/Expiry, Production/Recipes.
